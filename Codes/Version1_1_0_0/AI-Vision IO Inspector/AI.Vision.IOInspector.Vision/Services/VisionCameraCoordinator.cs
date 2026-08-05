using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure;
using AI.Vision.IOInspector.Infrastructure.Services.Camera;
using AI.Vision.IOInspector.Vision.ImvCamera;
using AI.Vision.IOInspector.Vision.Models;
using AI.Vision.IOInspector.Vision.Threading;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using OpenCvSharp;
using System.Runtime;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// Vision 프로젝트의 카메라 제어 중심 클래스입니다.
    /// RTSP/File/NVR은 기존 ConfiguredCameraService를 사용하고, DirectSdk는 IMV SDK를 직접 호출합니다.
    /// </summary>
    public class VisionCameraCoordinator : ICameraService, IVisionCameraCaptureExecutor, IDisposable
    {
        private const int CaptureTimeoutMilliseconds = 10000;
        private const int PreviewTimeoutMilliseconds = 3000;
        private const int LatestFrameWaitTimeoutMilliseconds = 3000;

        private readonly object _syncRoot;
        private readonly object _configuredCameraServiceSyncRoot;
        private readonly object _vladRtspRegistrationSyncRoot;
        private readonly string _applicationRootPath;
        private readonly string _projectRootPath;
        private readonly ConfiguredCameraService _configuredCameraService;
        private readonly Dictionary<ImageViewType, VisionCameraCaptureWorker> _captureWorkers;
        private readonly Dictionary<ImageViewType, CapturedImage> _latestCapturedImages;
        private readonly Dictionary<ImageViewType, CameraChannelStatus> _directSdkStatuses;
        private readonly Dictionary<ImageViewType, string> _vladRtspRegistrations;
        private VisionWorkerState _state;
        private readonly VladCamModeRuntime _camModeRuntime;
        private readonly VladVisionSettings _settings;


        public VisionCameraCoordinator(string applicationRootPath, VladCamModeRuntime camModeRuntime)
        {
            _camModeRuntime = camModeRuntime ?? throw new ArgumentNullException(nameof(camModeRuntime));
            _settings = _camModeRuntime.Settings;

            _syncRoot = new object();
            _configuredCameraServiceSyncRoot = new object();
            _vladRtspRegistrationSyncRoot = new object();

            _applicationRootPath = applicationRootPath;
            _projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            _configuredCameraService = new ConfiguredCameraService(applicationRootPath);

            _captureWorkers = new Dictionary<ImageViewType, VisionCameraCaptureWorker>();
            _latestCapturedImages = new Dictionary<ImageViewType, CapturedImage>();
            _directSdkStatuses = new Dictionary<ImageViewType, CameraChannelStatus>();
            _vladRtspRegistrations = new Dictionary<ImageViewType, string>();

            _state = VisionWorkerState.Stopped;
            BuildWorkersFromConfiguration();
        }

        public VisionWorkerState State
        {
            get
            {
                lock (_syncRoot)
                {
                    return _state;
                }
            }
        }

        public void Start()
        {
            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            foreach (VisionCameraCaptureWorker worker in workers)
            {
                worker.Start();
            }

            EnsureVladRtspRegistrationsForConfiguredChannels();

            lock (_syncRoot)
            {
                _state = VisionWorkerState.Running;
            }
        }

        public void Stop()
        {
            StopWorkers();
            lock (_syncRoot)
            {
                _state = VisionWorkerState.Stopped;
            }

            // MarkSong:
            // VLAD RTSP 등록 이후 수신 루프는 Native SDK 내부에서 관리합니다.
            // 현재 단계에서는 강제 종료하지 않습니다.
            // 추후 VLAD SDK의 RTSP 해제 API가 확인되면 여기에서 정상 종료 처리합니다.
        }

        public void ReloadConfiguration()
        {
            bool restartWorkers = State == VisionWorkerState.Running;
            StopWorkers();

            lock (_configuredCameraServiceSyncRoot)
            {
                _configuredCameraService.ReloadConfiguration();
            }

            lock (_syncRoot)
            {
                _directSdkStatuses.Clear();
            }

            BuildWorkersFromConfiguration();

            if (restartWorkers)
            {
                Start();
            }
        }

        /// <summary>
        /// 기존 VladId 해제 전에 로컬/정적 RTSP 등록 상태와 프레임 캐시를 무효화합니다.
        /// </summary>
        public void PrepareForVladRuntimeReload()
        {
            lock (_vladRtspRegistrationSyncRoot)
            {
                _vladRtspRegistrations.Clear();
            }

            VLAD_Ops_RTSP.PrepareForVladRuntimeReload();
        }

        /// <summary>
        /// 새 VladId가 발급된 뒤 Config.json의 활성 RTSP 채널을 새 세션에 다시 등록합니다.
        /// </summary>
        public void ResumeAfterVladRuntimeReload()
        {
            EnsureVladRtspRegistrationsForConfiguredChannels();
        }

        public IList<CameraChannelConfig> GetChannelConfigurations()
        {
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.GetChannelConfigurations();
            }
        }

        public void SaveChannelConfigurations(IList<CameraChannelConfig> channels)
        {
            bool restartWorkers = State == VisionWorkerState.Running;
            StopWorkers();

            lock (_configuredCameraServiceSyncRoot)
            {
                _configuredCameraService.SaveChannelConfigurations(channels);
            }

            lock (_syncRoot)
            {
                _directSdkStatuses.Clear();
            }

            BuildWorkersFromConfiguration();

            if (restartWorkers)
            {
                Start();
            }
        }

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            IList<CameraChannelStatus> statuses;
            lock (_configuredCameraServiceSyncRoot)
            {
                statuses = _configuredCameraService.GetChannelStatuses();
            }

            lock (_syncRoot)
            {
                int index = 0;
                while (index < statuses.Count)
                {
                    CameraChannelStatus status = statuses[index];
                    if (_directSdkStatuses.ContainsKey(status.ViewType))
                    {
                        statuses[index] = _directSdkStatuses[status.ViewType];
                    }

                    index++;
                }
            }

            return statuses;
        }

        public CameraChannelStatus TestChannelConnection(ImageViewType viewType)
        {
            CameraChannelConfig channel = FindChannelConfig(viewType);
            if (channel == null)
            {
                return BuildDirectSdkStatus(null, viewType, false, "카메라 설정을 찾을 수 없습니다.", string.Empty);
            }

            if (channel.ConnectionType != CameraConnectionType.DirectSdk)
            {
                lock (_configuredCameraServiceSyncRoot)
                {
                    return _configuredCameraService.TestChannelConnection(viewType);
                }
            }

            if (!channel.IsEnabled)
            {
                CameraChannelStatus disabledStatus = BuildDirectSdkStatus(channel, viewType, false, "카메라 채널이 비활성화되어 있습니다.", string.Empty);
                SetDirectSdkStatus(disabledStatus);
                return disabledStatus;
            }

            Part testPart = new Part();
            testPart.PartNo = "CONNECTION_TEST";

            try
            {
                CapturedImage image = CaptureDirectSdk(channel, testPart, PreviewTimeoutMilliseconds, "영상 프레임 수신 완료");
                CameraChannelStatus status = BuildDirectSdkStatus(channel, viewType, true, "영상 프레임 수신 완료", image.FilePath);
                SetDirectSdkStatus(status);
                return status;
            }
            catch (Exception ex)
            {
                CameraChannelStatus status = BuildDirectSdkStatus(channel, viewType, false, "영상 프레임 수신 실패: " + ex.Message, string.Empty);
                SetDirectSdkStatus(status);
                return status;
            }
        }

        public IList<CapturedImage> GetLatestCapturedImages()
        {
            IList<CapturedImage> images = new List<CapturedImage>();
            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            lock (_syncRoot)
            {
                foreach (VisionCameraCaptureWorker worker in workers)
                {
                    CapturedImage image;
                    if (_latestCapturedImages.TryGetValue(worker.ViewType, out image) && image != null)
                    {
                        images.Add(image);
                    }
                }
            }

            return images;
        }

        public CapturedImage Capture(ImageViewType viewType, Part part)
        {
            Start();

            VisionCameraCaptureWorker worker = FindWorker(viewType);
            if (worker == null)
            {
                CapturedImage directImage = ExecuteCapture(viewType, part);
                RecordLatestCapturedImage(directImage);
                return directImage;
            }

            CapturedImage image = worker.Capture(part, CaptureTimeoutMilliseconds);
            RecordLatestCapturedImage(image);
            return image;
        }

        public IList<CapturedImage> CaptureAll(Part part)
        {
            Start();

            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            IList<VisionCameraCaptureRequest> requests = new List<VisionCameraCaptureRequest>();
            IList<CameraChannelConfig> rtspChannels = new List<CameraChannelConfig>();
            IDictionary<ImageViewType, CapturedImage> imagesByViewType = new Dictionary<ImageViewType, CapturedImage>();

            try
            {
                // Direct SDK/File 채널은 기존 worker에 먼저 요청하고, RTSP 채널은 callback 캐시에서 일괄 확보합니다.
                foreach (VisionCameraCaptureWorker worker in workers)
                {
                    CameraChannelConfig channel = FindChannelConfig(worker.ViewType);
                    if (IsVladRtspChannel(channel))
                    {
                        rtspChannels.Add(channel);
                    }
                    else
                    {
                        requests.Add(worker.EnqueueCapture(part));
                    }
                }

                // 6개 RTSP LatestFrame 참조를 한 번에 얻은 뒤 각각 독립 byte[]로 복제합니다.
                // 파일 저장은 callback 잠금이 해제된 상태에서 실행되므로 프레임 갱신과 경합하지 않습니다.
                IList<CapturedImage> rtspImages = CaptureVladRtspBatch(rtspChannels, part);
                foreach (CapturedImage rtspImage in rtspImages)
                {
                    imagesByViewType[rtspImage.ViewType] = rtspImage;
                }

                foreach (VisionCameraCaptureRequest request in requests)
                {
                    CapturedImage image = WaitCaptureRequest(request);
                    if (image != null)
                    {
                        imagesByViewType[image.ViewType] = image;
                    }
                }
            }
            finally
            {
                DisposeCompletedRequests(requests);
            }

            IList<CapturedImage> images = new List<CapturedImage>();
            foreach (VisionCameraCaptureWorker worker in workers)
            {
                CapturedImage image;
                if (imagesByViewType.TryGetValue(worker.ViewType, out image))
                {
                    images.Add(image);
                    RecordLatestCapturedImage(image);
                }
            }

            return images;
        }

        public CapturedImage ExecuteCapture(ImageViewType viewType, Part part)
        {
            CameraChannelConfig channel = FindChannelConfig(viewType);

            if (channel != null && !channel.IsEnabled)
            {
                throw new InvalidOperationException(channel.DisplayName + " 카메라 채널이 비활성화되어 있습니다.");
            }

            if (channel != null && channel.ConnectionType == CameraConnectionType.DirectSdk)
            {
                return CaptureDirectSdk(channel, part, CaptureTimeoutMilliseconds, "촬영 완료");
            }

            if ((channel != null && channel.ConnectionType == CameraConnectionType.Rtsp) || (channel != null && channel.ConnectionType == CameraConnectionType.NvrRtsp))
            {
                return CaptureVladRtsp(channel, part);
            }

            // 설정에 없는 예외 채널은 기존 ConfiguredCameraService 경로를 사용합니다.
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.Capture(viewType, part);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private CapturedImage CaptureDirectSdk(CameraChannelConfig channel, Part part, int timeoutMilliseconds, string successMessage)
        {
            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            if (!channel.IsEnabled)
            {
                throw new InvalidOperationException(channel.DisplayName + " 카메라 채널이 비활성화되어 있습니다.");
            }

            string outputFilePath = BuildDirectSdkCaptureFilePath(channel, part);
            ImvCameraDevice device = new ImvCameraDevice(channel);

            try
            {
                device.OpenDevice();
                ApplyTriggerMode(device, channel);
                device.SetBufferCount(8);
                device.StartGrabbing();

                if (channel.TriggerMode == CameraTriggerMode.Software)
                {
                    device.ExecuteCommandFeature("TriggerSoftware");
                }

                VisionFrame frame = device.GetFrame(timeoutMilliseconds);
                ImvBitmapWriter.WriteBgr24(outputFilePath, frame.Width, frame.Height, frame.Buffer);

                CapturedImage image = new CapturedImage();
                image.ViewType = channel.ViewType;
                image.DisplayName = channel.DisplayName;
                image.FilePath = outputFilePath;
                image.CapturedAt = frame.CapturedAt;

                SetDirectSdkStatus(BuildDirectSdkStatus(channel, channel.ViewType, true, successMessage, image.FilePath));
                return image;
            }
            catch (Exception ex)
            {
                SetDirectSdkStatus(BuildDirectSdkStatus(channel, channel.ViewType, false, ex.Message, string.Empty));
                throw;
            }
            finally
            {
                device.CloseDevice();
            }
        }

        private void ApplyTriggerMode(ImvCameraDevice device, CameraChannelConfig channel)
        {
            if (channel.TriggerMode == CameraTriggerMode.Continuous)
            {
                device.SetEnumFeatureSymbol("TriggerMode", "Off");
                return;
            }

            device.SetEnumFeatureSymbol("TriggerMode", "On");
            if (channel.TriggerMode == CameraTriggerMode.Software)
            {
                device.SetEnumFeatureSymbol("TriggerSource", "Software");
                return;
            }

            if (channel.TriggerMode == CameraTriggerMode.Line1)
            {
                device.SetEnumFeatureSymbol("TriggerSource", "Line1");
            }
        }

        private CameraChannelConfig FindChannelConfig(ImageViewType viewType)
        {
            IList<CameraChannelConfig> channels;
            lock (_configuredCameraServiceSyncRoot)
            {
                channels = _configuredCameraService.GetChannelConfigurations();
            }

            foreach (CameraChannelConfig channel in channels)
            {
                if (channel.ViewType == viewType)
                {
                    return channel;
                }
            }

            return null;
        }

        private CameraChannelStatus BuildDirectSdkStatus(
            CameraChannelConfig channel,
            ImageViewType viewType,
            bool isConnected,
            string message,
            string lastFramePath)
        {
            CameraChannelStatus status = new CameraChannelStatus();
            status.ViewType = viewType;
            status.DisplayName = channel == null ? viewType.ToString() : channel.DisplayName;
            status.ChannelId = channel == null ? string.Empty : channel.ChannelId;
            status.CameraModel = channel == null ? string.Empty : channel.CameraModel;
            status.ConnectionType = channel == null ? CameraConnectionType.DirectSdk : channel.ConnectionType;
            status.IsEnabled = channel != null && channel.IsEnabled;
            status.IsConnected = isConnected;
            status.IpAddress = channel == null ? string.Empty : channel.IpAddress;
            status.Port = channel == null ? 0 : channel.Port;
            status.UserName = channel == null ? string.Empty : channel.UserName;
            status.Password = channel == null ? string.Empty : channel.Password;
            status.SerialNumber = channel == null ? string.Empty : channel.SerialNumber;
            status.DeviceUserId = channel == null ? string.Empty : channel.DeviceUserId;
            status.CameraKey = channel == null ? string.Empty : channel.CameraKey;
            status.RtspUrl = channel == null ? string.Empty : channel.RtspUrl;
            status.StreamPath = channel == null ? string.Empty : channel.StreamPath;
            status.NvrChannel = channel == null ? 0 : channel.NvrChannel;
            status.Width = channel == null ? 0 : channel.Width;
            status.Height = channel == null ? 0 : channel.Height;
            status.Fps = channel == null ? 0 : channel.Fps;
            status.ExposureTime = channel == null ? 0 : channel.ExposureTime;
            status.Gain = channel == null ? 0 : channel.Gain;
            status.TriggerMode = channel == null ? CameraTriggerMode.Continuous : channel.TriggerMode;
            status.Message = message;
            status.LastFramePath = lastFramePath;
            status.CheckedAt = DateTime.Now;
            return status;
        }

        private void SetDirectSdkStatus(CameraChannelStatus status)
        {
            lock (_syncRoot)
            {
                _directSdkStatuses[status.ViewType] = status;
            }
        }

        private void RecordLatestCapturedImage(CapturedImage image)
        {
            if (image == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                _latestCapturedImages[image.ViewType] = image;
            }
        }

        private CapturedImage WaitCaptureRequest(VisionCameraCaptureRequest request)
        {
            VisionCameraCaptureWorker worker = FindWorker(request.ViewType);
            if (worker == null)
            {
                request.Abandon();
                throw new InvalidOperationException(request.ViewType.ToString() + " 카메라 Worker를 찾을 수 없습니다.");
            }

            try
            {
                return worker.WaitForRequest(request, CaptureTimeoutMilliseconds);
            }
            catch (TimeoutException)
            {
                // 타임아웃된 요청은 워커가 뒤늦게 완료할 수 있으므로 여기서 완료 이벤트를 확인하거나 Dispose하지 않습니다.
                request.Abandon();
                throw;
            }
        }

        private void BuildWorkersFromConfiguration()
        {
            IList<CameraChannelStatus> statuses;
            lock (_configuredCameraServiceSyncRoot)
            {
                statuses = _configuredCameraService.GetChannelStatuses();
            }

            lock (_syncRoot)
            {
                _captureWorkers.Clear();
                _latestCapturedImages.Clear();
                foreach (CameraChannelStatus status in statuses)
                {
                    if (status.IsEnabled)
                    {
                        _captureWorkers[status.ViewType] = new VisionCameraCaptureWorker(status.ViewType, this);
                    }
                }
            }
        }

        private void StopWorkers()
        {
            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            foreach (VisionCameraCaptureWorker worker in workers)
            {
                // 모든 채널에 먼저 중지 신호를 전달합니다. 기존에는 카메라별 Join이 순차 실행되어
                // 응답하지 않는 채널이 여러 개면 종료 대기 시간이 채널 수만큼 누적될 수 있었습니다.
                worker.RequestStop();
            }

            foreach (VisionCameraCaptureWorker worker in workers)
            {
                worker.WaitForStop(3000);
                worker.DisposeAfterStop();
            }

            lock (_syncRoot)
            {
                _captureWorkers.Clear();
            }
        }

        private VisionCameraCaptureWorker FindWorker(ImageViewType viewType)
        {
            lock (_syncRoot)
            {
                if (_captureWorkers.ContainsKey(viewType))
                {
                    return _captureWorkers[viewType];
                }
            }

            return null;
        }

        private IList<VisionCameraCaptureWorker> GetOrderedWorkers()
        {
            IList<VisionCameraCaptureWorker> workers = new List<VisionCameraCaptureWorker>();
            AddWorkerIfExists(workers, ImageViewType.Top);
            AddWorkerIfExists(workers, ImageViewType.Front);
            AddWorkerIfExists(workers, ImageViewType.Back);
            AddWorkerIfExists(workers, ImageViewType.Left);
            AddWorkerIfExists(workers, ImageViewType.Right);
            AddWorkerIfExists(workers, ImageViewType.Thickness);
            return workers;
        }

        private void AddWorkerIfExists(IList<VisionCameraCaptureWorker> workers, ImageViewType viewType)
        {
            VisionCameraCaptureWorker worker = FindWorker(viewType);
            if (worker != null)
            {
                workers.Add(worker);
            }
        }

        private void DisposeCompletedRequests(IList<VisionCameraCaptureRequest> requests)
        {
            foreach (VisionCameraCaptureRequest request in requests)
            {
                if (request.IsAbandoned)
                {
                    continue;
                }

                try
                {
                    if (request.CompletedEvent.WaitOne(0))
                    {
                        request.Dispose();
                    }
                    else
                    {
                        // 아직 처리 중인 요청은 워커가 완료 후 정리하게 두어 Dispose 경합을 피합니다.
                        request.Abandon();
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private string BuildDirectSdkCaptureFilePath(CameraChannelConfig channel, Part part)
        {
            DateTime capturedAt = DateTime.Now;
            return InspectionHistoryImagePathBuilder.BuildCaptureFilePath(
                _projectRootPath,
                channel,
                part,
                ".bmp",
                capturedAt);
        }

        private string BuildVladRtspCaptureFilePath(CameraChannelConfig channel, Part part, DateTime capturedAt)
        {
            return InspectionHistoryImagePathBuilder.BuildCaptureFilePath(
                _projectRootPath,
                channel,
                part,
                ".png",
                capturedAt);
        }

        private CapturedImage CaptureVladRtsp(CameraChannelConfig channel, Part part)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
            channels.Add(channel);
            return CaptureVladRtspBatch(channels, part)[0];
        }

        /// <summary>
        /// 검사 시점의 RTSP 채널들을 동일한 LatestFrame 스냅샷 묶음으로 확보하고 저장합니다.
        /// 이 경로에서는 ffmpeg/LibVLC/OpenCV로 RTSP를 다시 열지 않습니다.
        /// </summary>
        private IList<CapturedImage> CaptureVladRtspBatch(IList<CameraChannelConfig> channels, Part part)
        {
            IList<CapturedImage> images = new List<CapturedImage>();
            if (channels == null || channels.Count == 0)
            {
                return images;
            }

            IList<int> monitorIndices = new List<int>();
            IDictionary<int, CameraChannelConfig> channelsByMonitorIndex = new Dictionary<int, CameraChannelConfig>();
            foreach (CameraChannelConfig channel in channels)
            {
                string rtspUrl = RtspUrlBuilder.Build(channel);
                if (string.IsNullOrWhiteSpace(rtspUrl))
                {
                    throw new InvalidOperationException(channel.DisplayName + " RTSP URL을 만들 수 없습니다. IP/Port/StreamPath 설정을 확인하십시오.");
                }

                int monitorIndex = ResolveMonitorIndex(channel.ViewType);
                monitorIndices.Add(monitorIndex);
                channelsByMonitorIndex[monitorIndex] = channel;
            }

            IDictionary<int, VladRtspLatestFrame> snapshots;
            string cloneMessage;
            if (!VLAD_Ops_RTSP.TryCloneLatestFrames(
                    monitorIndices,
                    3000,
                    LatestFrameWaitTimeoutMilliseconds,
                    out snapshots,
                    out cloneMessage))
            {
                string failureMessage = "RTSP 최신 프레임 일괄 확보 실패: " + cloneMessage;
                foreach (CameraChannelConfig channel in channels)
                {
                    SetDirectSdkStatus(BuildDirectSdkStatus(channel, channel.ViewType, false, failureMessage, string.Empty));
                }

                throw new InvalidOperationException(failureMessage);
            }

            DateTime requestedAt = DateTime.Now;
            IList<string> savedFilePaths = new List<string>();
            try
            {
                foreach (int monitorIndex in monitorIndices)
                {
                    CameraChannelConfig channel = channelsByMonitorIndex[monitorIndex];
                    VladRtspLatestFrame snapshot = snapshots[monitorIndex];
                    string outputFilePath = BuildVladRtspCaptureFilePath(channel, part, requestedAt);
                    string saveMessage;
                    if (!VLAD_Ops_RTSP.TrySaveFrameSnapshot(snapshot, outputFilePath, out saveMessage))
                    {
                        throw new InvalidOperationException(channel.DisplayName + " RTSP 스냅샷 저장 실패: " + saveMessage);
                    }

                    savedFilePaths.Add(outputFilePath);

                    CapturedImage image = new CapturedImage();
                    image.ViewType = channel.ViewType;
                    image.DisplayName = channel.DisplayName;
                    image.FilePath = outputFilePath;
                    image.CapturedAt = snapshot.CapturedAt;
                    images.Add(image);

                    SetDirectSdkStatus(BuildDirectSdkStatus(channel, channel.ViewType, true, saveMessage, outputFilePath));
                }
            }
            catch (Exception exception)
            {
                foreach (string savedFilePath in savedFilePaths)
                {
                    try
                    {
                        if (File.Exists(savedFilePath))
                        {
                            File.Delete(savedFilePath);
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

                foreach (CameraChannelConfig channel in channels)
                {
                    SetDirectSdkStatus(BuildDirectSdkStatus(channel, channel.ViewType, false, exception.Message, string.Empty));
                }

                throw;
            }

            return images;
        }

        // 프로그램 실행 또는 설정 재로드 시 RTSP/NVR 채널을 VLAD SDK에 등록합니다.
        // 등록 이후부터 VLAD_Ops_RTSP_Frame_Proc callback이 지속적으로 프레임을 받습니다.
        private void EnsureVladRtspRegistrationsForConfiguredChannels()
        {
            IList<CameraChannelConfig> channels;
            lock (_configuredCameraServiceSyncRoot)
            {
                channels = _configuredCameraService.GetChannelConfigurations();
            }

            HashSet<string> registeredUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CameraChannelConfig channel in channels)
            {
                if (channel == null || !channel.IsEnabled || !IsVladRtspChannel(channel))
                {
                    continue;
                }

                string rtspUrl = RtspUrlBuilder.Build(channel);
                if (string.IsNullOrWhiteSpace(rtspUrl))
                {
                    AppendVladRtspLog("SKIP", channel.ViewType.ToString() + " RTSP URL이 비어 있어 VLAD RTSP 등록을 수행하지 않습니다.");
                    continue;
                }

                if (registeredUrls.Contains(rtspUrl))
                {
                    AppendVladRtspLog(
                        "SKIP",
                        channel.ViewType.ToString() +
                        " VLAD RTSP 등록 생략: 다른 채널과 동일한 URL입니다. URL=" +
                        rtspUrl);
                    continue;
                }

                registeredUrls.Add(rtspUrl);
                TryRegisterVladRtspClient(channel, rtspUrl);
            }
        }

        private static bool IsVladRtspChannel(CameraChannelConfig channel)
        {
            if (channel == null)
            {
                return false;
            }

            return channel.ConnectionType == CameraConnectionType.Rtsp ||
                   channel.ConnectionType == CameraConnectionType.NvrRtsp;
        }

        private void TryRegisterVladRtspClient(CameraChannelConfig channel, string rtspUrl)
        {
            if (!IsInProcessVladRtspEnabled())
            {
                AppendVladRtspLog("SKIP", channel.ViewType.ToString() + " VLAD RTSP 등록 생략: AI_VISION_ENABLE_INPROCESS_VLAD_RTSP가 꺼져 있습니다.");
                Debug.WriteLine("VLAD RTSP 등록 생략: AI_VISION_ENABLE_INPROCESS_VLAD_RTSP가 꺼져 있습니다.");
                return;
            }

            try
            {
                // 현재 VLAD_Ops_RTSP는 활성 VladId 하나의 프레임 캐시를 관리하므로 전체 이미지용 ID만 RTSP에 등록합니다.
                IntPtr vladId = _camModeRuntime.EnsureLoaded().FullImageVladId;
                if (vladId == IntPtr.Zero)
                {
                    AppendVladRtspLog("FAILED", channel.ViewType.ToString() + " VLAD RTSP 등록 실패: VLAD SDK 초기화 결과가 비어 있습니다.");
                    Debug.WriteLine("VLAD RTSP 등록 생략: VLAD SDK 초기화 결과가 비어 있습니다.");
                    return;
                }

                RegisterVladRtspClientIfNeeded(channel, rtspUrl, vladId);
            }
            catch (Exception ex)
            {
                // RTSP 캡처와 AI 추론은 별도 경로이므로, VLAD RTSP 보조 스레드 실패만으로 카메라 캡처를 중단하지 않습니다.
                AppendVladRtspLog("FAILED", channel.ViewType.ToString() + " VLAD RTSP 등록 실패: " + ex.Message);
                Debug.WriteLine("VLAD RTSP 등록 실패: " + ex.Message);
            }
        }

        private static bool IsInProcessVladRtspEnabled()
        {
            string enabled = Environment.GetEnvironmentVariable("AI_VISION_ENABLE_INPROCESS_VLAD_RTSP");
            if (string.IsNullOrWhiteSpace(enabled))
            {
                return true;
            }

            return !string.Equals(enabled, "0", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(enabled, "no", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(enabled, "off", StringComparison.OrdinalIgnoreCase);
        }

        private void RegisterVladRtspClientIfNeeded(CameraChannelConfig channel, string rtspUrl, IntPtr vladId)
        {
            lock (_vladRtspRegistrationSyncRoot)
            {
                string registrationSignature = BuildVladRtspRegistrationSignature(channel, rtspUrl);
                string existingSignature;
                if (_vladRtspRegistrations.TryGetValue(channel.ViewType, out existingSignature))
                {
                    if (string.Equals(existingSignature, registrationSignature, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    _vladRtspRegistrations.Remove(channel.ViewType);
                }

                int monitorIndex = ResolveMonitorIndex(channel.ViewType);
                string cameraName = string.IsNullOrWhiteSpace(channel.CameraKey) ? channel.DisplayName : channel.CameraKey;

                if (channel.Width <= 0 || channel.Height <= 0)
                {
                    throw new InvalidOperationException(
                        channel.ViewType.ToString() +
                        " 카메라의 CAM_WIDTH/CAM_HEIGHT가 올바르지 않습니다. Config.json에 실제 RTSP 해상도를 설정해야 합니다.");
                }

                // frame_width/frame_height는 callback display 포인터를 byte[]로 복사할 때 사용됩니다.
                // Config.json의 CAM_WIDTH/CAM_HEIGHT는 실제 RTSP 해상도와 반드시 맞아야 합니다.
                var param = new VLAD_Ops_RTSP.VLAD_Ops_RTSP_ThreadParam(
                    vladId,
                    _settings.SiteName,
                    VLAD_Ops_RTSP.MODE_TYPE_CAM,
                    monitorIndex,
                    rtspUrl,
                    cameraName,
                    _settings.Threshold,
                    channel.Width,
                    channel.Height);

                VLAD_Ops_RTSP.VLAD_Ops_RTSP_Client_Registration(param);

                _vladRtspRegistrations[channel.ViewType] = registrationSignature;

                AppendVladRtspLog(
                    "START",
                    channel.ViewType.ToString() +
                    " VLAD RTSP 등록 완료: URL=" +
                    rtspUrl +
                    ", ConfiguredResolution=" +
                    channel.Width.ToString() +
                    "x" +
                    channel.Height.ToString() +
                    ", FrameBufferResolution=" +
                    channel.Width.ToString() +
                    "x" +
                    channel.Height.ToString());
                Debug.WriteLine(
                    "VLAD RTSP 등록 완료: " +
                    channel.ViewType +
                    ", URL=" +
                    rtspUrl +
                    ", ConfiguredResolution=" +
                    channel.Width.ToString() +
                    "x" +
                    channel.Height.ToString() +
                    ", FrameBufferResolution=" +
                    channel.Width.ToString() +
                    "x" +
                    channel.Height.ToString());
            }
        }

        /// <summary>
        /// RTSP URL이 같아도 프레임 해상도가 바뀌면 callback 버퍼 해석 기준을 갱신해야 합니다.
        /// display 포인터에는 가로/세로 정보가 포함되지 않으므로 URL과 해상도를 함께 등록 키로 관리합니다.
        /// </summary>
        private static string BuildVladRtspRegistrationSignature(CameraChannelConfig channel, string rtspUrl)
        {
            int width = channel == null ? 0 : channel.Width;
            int height = channel == null ? 0 : channel.Height;
            return (rtspUrl ?? string.Empty) + "|" + width.ToString() + "x" + height.ToString();
        }

        private void AppendVladRtspLog(string status, string message)
        {
            try
            {
                string logDirectoryPath = Path.Combine(_projectRootPath, "DB", "Logs");
                Directory.CreateDirectory(logDirectoryPath);
                string logFilePath = Path.Combine(logDirectoryPath, "vlad-rtsp.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                              " [" + status + "] " +
                              message +
                              Environment.NewLine;
                File.AppendAllText(logFilePath, line);
            }
            catch
            {
            }
        }

        private int ResolveMonitorIndex(ImageViewType viewType)
        {
            switch (viewType)
            {
                case ImageViewType.Top:
                    return 0;

                case ImageViewType.Front:
                    return 1;

                case ImageViewType.Back:
                    return 2;

                case ImageViewType.Left:
                    return 3;

                case ImageViewType.Right:
                    return 4;

                case ImageViewType.Thickness:
                    return 5;

                default:
                    return 0;
            }
        }
    }
}
