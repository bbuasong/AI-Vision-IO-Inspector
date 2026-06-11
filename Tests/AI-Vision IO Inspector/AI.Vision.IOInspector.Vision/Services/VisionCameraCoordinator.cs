using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
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
    public class VisionCameraCoordinator : ICameraService, IVisionCameraCaptureExecutor, IVisionCameraReceiveExecutor, IDisposable
    {
        private const int CaptureTimeoutMilliseconds = 10000;
        private const int PreviewTimeoutMilliseconds = 3000;

        private readonly object _syncRoot;
        private readonly object _configuredCameraServiceSyncRoot;
        private readonly object _vladRtspThreadSyncRoot;
        private readonly string _applicationRootPath;
        private readonly string _projectRootPath;
        private readonly ConfiguredCameraService _configuredCameraService;
        private readonly Dictionary<ImageViewType, VisionCameraCaptureWorker> _captureWorkers;
        private readonly Dictionary<ImageViewType, CameraChannelStatus> _directSdkStatuses;
        private readonly Dictionary<ImageViewType, Thread> _vladRtspThreads;                    // Rtsp Thread 관리용
        private VisionWorkerState _state;
        private readonly VladSdkSession _vladSdkSession;
        private readonly VladVisionSettings _settings;


        public VisionCameraCoordinator(string applicationRootPath, VladSdkSession vladSdkSession, VladVisionSettings settings)
        {
            _vladSdkSession = vladSdkSession ?? throw new ArgumentNullException(nameof(vladSdkSession));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            _syncRoot = new object();
            _configuredCameraServiceSyncRoot = new object();
            _vladRtspThreadSyncRoot = new object();

            _applicationRootPath = applicationRootPath;
            _projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            _configuredCameraService = new ConfiguredCameraService(applicationRootPath);

            _captureWorkers = new Dictionary<ImageViewType, VisionCameraCaptureWorker>();
            _directSdkStatuses = new Dictionary<ImageViewType, CameraChannelStatus>();
            _vladRtspThreads = new Dictionary<ImageViewType, Thread>();

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
            // VLAD RTSP Thread는 Native SDK 내부에서 수신 루프를 관리할 수 있으므로
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
            foreach (VisionCameraCaptureWorker worker in workers)
            {
                CapturedImage image = worker.LatestImage;
                if (image != null)
                {
                    images.Add(image);
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
                return ExecuteCapture(viewType, part);
            }

            return worker.Capture(part, CaptureTimeoutMilliseconds);
        }

        public IList<CapturedImage> CaptureAll(Part part)
        {
            Start();

            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            IList<VisionCameraCaptureRequest> requests = new List<VisionCameraCaptureRequest>();
            IList<CapturedImage> images = new List<CapturedImage>();

            try
            {
                foreach (VisionCameraCaptureWorker worker in workers)
                {
                    requests.Add(worker.EnqueueCapture(part));
                }

                foreach (VisionCameraCaptureRequest request in requests)
                {
                    CapturedImage image = WaitCaptureRequest(request);
                    if (image != null)
                    {
                        images.Add(image);
                    }
                }
            }
            finally
            {
                DisposeCompletedRequests(requests);
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

            // 임시 VLAD RTSP 호출 테스트 위치
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.Capture(viewType, part);
            }
        }

        public CapturedImage ReceiveLatestFrame(ImageViewType viewType)
        {
            CameraChannelConfig channel = FindChannelConfig(viewType);
            if (channel == null || channel.ConnectionType != CameraConnectionType.DirectSdk || !channel.IsEnabled)
            {
                return null;
            }

            Part previewPart = new Part();
            previewPart.PartNo = "PREVIEW";
            return CaptureDirectSdk(channel, previewPart, PreviewTimeoutMilliseconds, "미리보기 프레임 수신 완료");
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
                request.Abandon();
                if (request.CompletedEvent.WaitOne(0))
                {
                    request.Dispose();
                }

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
                worker.Dispose();
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

                if (request.CompletedEvent.WaitOne(0))
                {
                    request.Dispose();
                }
                else
                {
                    request.Abandon();
                    if (request.CompletedEvent.WaitOne(0))
                    {
                        request.Dispose();
                    }
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

        private CapturedImage CaptureVladRtsp(CameraChannelConfig channel, Part part)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            string rtspUrl = RtspUrlBuilder.Build(channel);
            if (string.IsNullOrWhiteSpace(rtspUrl))
            {
                throw new InvalidOperationException(channel.DisplayName + " RTSP URL을 만들 수 없습니다. IP/Port/StreamPath 설정을 확인하십시오.");
            }

            TryStartVladRtspThread(channel, rtspUrl);

            // 검사 이미지 저장은 현재 ConfiguredCameraService의 RTSP 캡처 경로를 사용합니다.
            // VLAD RTSP Thread는 기존 VLAD_Ops 호환/실시간 처리 경로이며, 캡처 파일 반환 경로와 분리되어 있습니다.
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.Capture(channel.ViewType, part);
            }
        }

        private void TryStartVladRtspThread(CameraChannelConfig channel, string rtspUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_settings.ModelPath) || !Directory.Exists(_settings.ModelPath))
                {
                    Debug.WriteLine("VLAD RTSP Thread 시작 생략: 모델 경로가 없습니다. " + (_settings.ModelPath ?? string.Empty));
                    return;
                }

                lock (_syncRoot)
                {
                    IntPtr vladId = _vladSdkSession.EnsureStarted(
                        (int)SDK_USER.USER_CUS_STD,
                        _settings.RootName,
                        _settings.SiteName,
                        (int)SDK_MSG.MSG_V1,
                        (int)SDK_MAJ.MAJ_V1,
                        _settings.ModelPath,
                        _settings.GpuId);

                    if (vladId == IntPtr.Zero)
                    {
                        Debug.WriteLine("VLAD RTSP Thread 시작 생략: VLAD SDK 초기화 결과가 비어 있습니다.");
                        return;
                    }

                    StartVladRtspThreadIfNeeded(channel, rtspUrl, vladId);
                }
            }
            catch (Exception ex)
            {
                // RTSP 캡처와 AI 추론은 별도 경로이므로, VLAD RTSP 보조 스레드 실패만으로 카메라 캡처를 중단하지 않습니다.
                Debug.WriteLine("VLAD RTSP Thread 시작 실패: " + ex.Message);
            }
        }

        private void StartVladRtspThreadIfNeeded(CameraChannelConfig channel, string rtspUrl, IntPtr vladId)
        {
            lock (_vladRtspThreadSyncRoot)
            {
                Thread existingThread;
                if (_vladRtspThreads.TryGetValue(channel.ViewType, out existingThread))
                {
                    if (existingThread != null && existingThread.IsAlive)
                    {
                        return;
                    }

                    _vladRtspThreads.Remove(channel.ViewType);
                }

                int monitorIndex = ResolveMonitorIndex(channel.ViewType);

                var param = new VLAD_Ops_RTSP.VLAD_Ops_RTSP_ThreadParam(
                    vladId,
                    _settings.SiteName,
                    VLAD_Ops_RTSP.MODE_TYPE_CAM,
                    monitorIndex,
                    rtspUrl,
                    channel.DisplayName,
                    _settings.Threshold,
                    channel.Width,
                    channel.Height);

                Thread thread = new Thread(VLAD_Ops_RTSP.VLAD_Ops_RTSP_Thread);
                thread.Name = "VLAD_RTSP_" + channel.ViewType.ToString();
                thread.IsBackground = true;

                _vladRtspThreads[channel.ViewType] = thread;

                Debug.WriteLine("VLAD RTSP Thread 시작: " + channel.ViewType + ", URL=" + rtspUrl);
                thread.Start(param);
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
