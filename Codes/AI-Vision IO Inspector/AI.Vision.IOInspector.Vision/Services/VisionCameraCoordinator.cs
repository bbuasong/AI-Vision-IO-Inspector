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

            // 상시 연결 ffmpeg는 별도 프로세스입니다. 여기서 정리하지 않으면
            // 프로그램을 닫아도 ffmpeg가 남아 카메라 스트림을 계속 점유합니다.
            lock (_configuredCameraServiceSyncRoot)
            {
                try
                {
                    _configuredCameraService.StopPersistentCapture();
                }
                catch (Exception)
                {
                    // 종료 정리 실패가 프로그램 종료를 막으면 안 됩니다.
                }
            }

            lock (_syncRoot)
            {
                _state = VisionWorkerState.Stopped;
            }

            // MarkSong:
            // VLAD RTSP 등록 이후 수신 루프는 Native SDK 내부에서 관리합니다.
            // 현재 단계에서는 강제 종료하지 않습니다.
            // 추후 VLAD SDK의 RTSP 해제 API가 확인되면 여기에서 정상 종료 처리합니다.
        }

        /// <summary>
        /// 설정에 있는 RTSP 채널이 모두 콜백 등록되어 있는지 확인하고, 빠진 것을 채웁니다.
        ///
        /// <para>
        /// 여러 번 불러도 안전합니다. 이미 등록된 채널은 등록 지문(URL·해상도)을 견주어
        /// 그냥 넘어갑니다.
        /// </para>
        ///
        /// <para>
        /// 예전에는 이 등록이 <see cref="Start"/> 와 학습 뒤 재적재에만 걸려 있었습니다.
        /// 그런데 <see cref="Start"/> 를 부르는 곳이 없어, 실제로는 학습을 돌린 실행에서만
        /// 여섯 채널이 붙었습니다. 학습 없이 검사만 하면 SDK 초기화가 등록한 첫 채널
        /// 하나만 살아 있어 Top 화면만 갱신되었습니다.
        /// </para>
        /// </summary>
        public void EnsureVladRtspRegistrations()
        {
            EnsureVladRtspRegistrationsForConfiguredChannels();
        }

        /// <summary>ICameraService 계약입니다. 이 구현에서는 VLAD RTSP 콜백 등록을 뜻합니다.</summary>
        public void EnsureLiveFrameSources()
        {
            EnsureVladRtspRegistrations();
        }

        public void ReloadConfiguration()
        {
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

            // 설정을 읽은 뒤에는 언제나 시작합니다.
            //
            // 예전에는 "이미 돌고 있었을 때만" 다시 시작했는데, 처음 한 번을 시작해 주는
            // 곳이 없어 그 조건이 참이 되는 일이 없었습니다. 결국 워커도 콜백 등록도
            // 이 길로는 살아나지 못했습니다.
            Start();
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
            StopWorkers();

            lock (_configuredCameraServiceSyncRoot)
            {
                _configuredCameraService.SaveChannelConfigurations(channels);
            }

            lock (_syncRoot)
            {
                _directSdkStatuses.Clear();
            }

            // 카메라 주소나 해상도가 바뀌었을 수 있으므로 등록을 새로 맺어야 합니다.
            // 지문이 달라진 채널만 다시 등록되고 나머지는 그대로 둡니다.
            lock (_vladRtspRegistrationSyncRoot)
            {
                _vladRtspRegistrations.Clear();
            }

            BuildWorkersFromConfiguration();

            // 설정을 저장한 뒤에도 언제나 시작합니다. ReloadConfiguration과 같은 까닭입니다.
            Start();
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

            // RTSP 채널은 callback 프레임이 들어오는지로 봅니다.
            //
            // 예전에는 ffmpeg 로 실제 프레임을 한 장 받아 확인했습니다. 그런데 현장에서 여섯 채널이
            // 모두 실패하며 64 초를 썼습니다. ffmpeg 는 trackID 형태의 주소를 열지 못했고,
            // LibVLC 는 채널마다 5 초씩 기다리다 시간을 넘겼습니다. 정작 callback 은 그동안
            // 멀쩡히 프레임을 넣고 있었습니다.
            //
            // 검사도 화면도 callback 으로 돕니다. 그러니 연결이 살아 있는지도 callback 으로 보면
            // 됩니다. 확인에 시간이 들지 않고, 실제로 쓰는 길이 살아 있는지를 곧장 봅니다.
            if (channel.ConnectionType == CameraConnectionType.Rtsp ||
                channel.ConnectionType == CameraConnectionType.NvrRtsp)
            {
                return BuildCallbackConnectionStatus(channel, viewType);
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
                // 연결 테스트는 검사가 아니므로 이 시점 시각을 그대로 사용합니다.
                CapturedImage image = CaptureDirectSdk(channel, testPart, PreviewTimeoutMilliseconds, "영상 프레임 수신 완료", DateTime.Now);
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
                CapturedImage directImage = ExecuteCapture(viewType, part, DateTime.Now);
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

            // 검사 시작 시각을 여기서 한 번만 정합니다.
            // 이 값이 저장 폴더(HH-mm-ss)와 파일 이름이 되므로, 채널별로 다시 시각을 읽으면
            // 초가 넘어가는 순간 6방향 이미지가 서로 다른 폴더로 갈라집니다.
            DateTime inspectionStartedAt = DateTime.Now;

            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            IList<VisionCameraCaptureRequest> requests = new List<VisionCameraCaptureRequest>();
            IList<CapturedImage> images = new List<CapturedImage>();

            try
            {
                // 카메라별 worker에 먼저 요청을 모두 넣어 각 방향 캡처가 독립적으로 진행되게 합니다.
                foreach (VisionCameraCaptureWorker worker in workers)
                {
                    requests.Add(worker.EnqueueCapture(part, inspectionStartedAt));
                }

                // 결과 수집은 고정 순서(Top/Front/Back/Left/Right/Thickness)로 기다립니다.
                // 채널 하나가 실패해도(카메라 고장, 일시적 응답 없음 등) 나머지 채널 저장/검사가
                // 막히면 안 되므로, 실패한 채널만 검정 이미지로 대체하고 계속 진행합니다.
                foreach (VisionCameraCaptureRequest request in requests)
                {
                    CapturedImage image;
                    try
                    {
                        image = WaitCaptureRequest(request);
                    }
                    catch (Exception captureException)
                    {
                        CameraChannelConfig channel = FindChannelConfig(request.ViewType);
                        image = SavePlaceholderBlackImage(channel, request.ViewType, part, captureException, inspectionStartedAt);
                    }

                    if (image != null)
                    {
                        images.Add(image);
                        RecordLatestCapturedImage(image);
                    }
                }
            }
            finally
            {
                DisposeCompletedRequests(requests);
            }

            return images;
        }

        public CapturedImage ExecuteCapture(ImageViewType viewType, Part part, DateTime inspectionStartedAt)
        {
            CameraChannelConfig channel = FindChannelConfig(viewType);

            if (channel != null && !channel.IsEnabled)
            {
                throw new InvalidOperationException(channel.DisplayName + " 카메라 채널이 비활성화되어 있습니다.");
            }

            if (channel != null && channel.ConnectionType == CameraConnectionType.DirectSdk)
            {
                return CaptureDirectSdk(channel, part, CaptureTimeoutMilliseconds, "촬영 완료", inspectionStartedAt);
            }

            if ((channel != null && channel.ConnectionType == CameraConnectionType.Rtsp) || (channel != null && channel.ConnectionType == CameraConnectionType.NvrRtsp))
            {
                return CaptureVladRtsp(channel, part, inspectionStartedAt);
            }

            // 설정에 없는 예외 채널은 기존 ConfiguredCameraService 경로를 사용합니다.
            //
            // 검사 시작 시각을 그대로 넘깁니다. 넘기지 않으면 촬영 시점의 DateTime.Now가 쓰여
            // 6방향이 초 단위로 갈리고, 한 검사의 이미지가 서로 다른 폴더에 나뉘어 저장됩니다.
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.Capture(viewType, part, inspectionStartedAt);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private CapturedImage CaptureDirectSdk(CameraChannelConfig channel, Part part, int timeoutMilliseconds, string successMessage, DateTime inspectionStartedAt)
        {
            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            if (!channel.IsEnabled)
            {
                throw new InvalidOperationException(channel.DisplayName + " 카메라 채널이 비활성화되어 있습니다.");
            }

            string outputFilePath = BuildDirectSdkCaptureFilePath(channel, part, inspectionStartedAt);
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

        /// <summary>
        /// callback 프레임이 들어오고 있는지로 그 카메라의 연결 상태를 판단합니다.
        ///
        /// <para>
        /// 아직 한 장도 오지 않았으면 연결되지 않은 것으로 봅니다. 프로그램을 켠 직후에는
        /// 그럴 수 있으므로, 잠시 뒤 새로고침하면 연결됨으로 바뀝니다.
        /// </para>
        /// </summary>
        private CameraChannelStatus BuildCallbackConnectionStatus(
            CameraChannelConfig channel,
            ImageViewType viewType)
        {
            if (!channel.IsEnabled)
            {
                return BuildDirectSdkStatus(channel, viewType, false, "카메라 채널이 비활성화되어 있습니다.", string.Empty);
            }

            int monitorIndex = ResolveMonitorIndex(viewType);
            int frameWidth;
            int frameHeight;
            if (!VLAD_Ops_RTSP.TryGetLatestFrameSize(monitorIndex, out frameWidth, out frameHeight))
            {
                return BuildDirectSdkStatus(
                    channel,
                    viewType,
                    false,
                    "아직 callback 프레임이 들어오지 않았습니다. 잠시 뒤 다시 확인하십시오.",
                    string.Empty);
            }

            return BuildDirectSdkStatus(
                channel,
                viewType,
                true,
                "callback 프레임 수신 중 (" +
                    frameWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x" +
                    frameHeight.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")",
                string.Empty);
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

        private string BuildDirectSdkCaptureFilePath(CameraChannelConfig channel, Part part, DateTime inspectionStartedAt)
        {
            return InspectionHistoryImagePathBuilder.BuildCaptureFilePath(
                _projectRootPath,
                channel,
                part,
                ".bmp",
                inspectionStartedAt);
        }

        private string BuildVladRtspCaptureFilePath(CameraChannelConfig channel, Part part, DateTime inspectionStartedAt)
        {
            return InspectionHistoryImagePathBuilder.BuildCaptureFilePath(
                _projectRootPath,
                channel,
                part,
                ".png",
                inspectionStartedAt);
        }

        private CapturedImage CaptureVladRtsp(CameraChannelConfig channel, Part part, DateTime inspectionStartedAt)
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

            // 검사 캡처는 RTSP를 다시 열지 않고, VLAD RTSP callback이 보관한 최신 프레임을 파일로 저장합니다.
            // 실제 프레임 수신은 VLAD_Rtsp_Info_Client_Registration으로 등록된 RTSP_Frame_Proc callback에서 수행합니다.
            int monitorIndex = ResolveMonitorIndex(channel.ViewType);
            DateTime requestedAt = DateTime.Now;
            string outputFilePath = BuildVladRtspCaptureFilePath(channel, part, inspectionStartedAt);
            DateTime capturedAt;
            string message;

            // VLAD RTSP callback은 공식 샘플 기준 1920x1080 BGR 버퍼입니다(VLAD_Ops_RTSP.CallbackFrameWidth/Height).
            // Config가 이보다 큰 해상도를 요구하면 callback을 확대 해석하지 않고, RTSP 원본 스트림을
            // 직접 캡처하여 실제 NVR 출력 해상도를 보존합니다. (여기서 callback 버퍼 크기로 채널의
            // 설정 해상도를 그대로 쓰면 Marshal.Copy가 실제 버퍼보다 큰 범위를 읽어 콜백 스레드가
            // 멈추는 문제가 있었습니다.)
            // 검사 이미지는 callback 프레임만 씁니다.
            //
            // 예전에는 상시 연결(ffmpeg)과 해상도 판단으로 갈래를 나누었습니다. 한 카메라에
            // callback 과 ffmpeg 가 함께 붙어 RTSP 연결이 열두 개가 되었고, 그 탓에 상시 연결이
            // 20~30초씩 프레임을 놓치고 되살아나기를 반복했습니다.
            //
            // 지금은 카메라마다 연결이 하나뿐입니다. callback 이 넘겨 주는 cv::Mat 에서 크기를
            // 그대로 읽으므로 Config 해상도와 견줄 일도 없습니다.

            // 검사 버튼을 누른 뒤에 들어온 프레임만 씁니다.
            //
            // 누르기 전 그림을 저장하면 그 순간의 제품이 아닌 것을 검사하게 됩니다.
            // 아직 안 왔으면 3초까지 기다립니다. 5fps 면 200ms 마다 새 프레임이 오므로
            // 3초는 열다섯 장을 기다리는 셈이라 넉넉합니다. 그래도 안 오면 검정 이미지로 남깁니다.
            //
            // 나이 제한(3초)은 그대로 둡니다. 기준 시각을 넘겼더라도 시계가 어긋난 프레임이
            // 섞여 들어올 수 있어 두 관문을 함께 둡니다.
            // 검사 시작 시각이 넘어오면 그것을 기준으로 삼습니다. 여섯 카메라가 같은 기준을 봅니다.
            DateTime minimumCapturedAt =
                inspectionStartedAt == DateTime.MinValue ? requestedAt : inspectionStartedAt;
            Stopwatch callbackSaveWatch = Stopwatch.StartNew();
            bool saved = VLAD_Ops_RTSP.TrySaveLatestFrame(
                monitorIndex,
                outputFilePath,
                3000,
                minimumCapturedAt,
                LatestFrameWaitTimeoutMilliseconds,
                out capturedAt,
                out message);
            callbackSaveWatch.Stop();

            // 어느 길로 저장했는지 남깁니다.
            //
            // 이 자리에 기록이 없어서, 캡처가 느릴 때 callback 을 쓰다 실패해 ffmpeg 로 넘어간 것인지
            // 처음부터 ffmpeg 로 간 것인지 가릴 수 없었습니다. 캡처 로그에는 ffmpeg 시도만 남습니다.
            AppendVladRtspLog(
                saved ? "CALLBACK-SAVE" : "CALLBACK-FAIL",
                channel.DisplayName +
                " callback 프레임 저장 " + (saved ? "성공" : "실패") +
                ". 걸린 시간=" + callbackSaveWatch.ElapsedMilliseconds.ToString() + "ms" +
                (saved ? string.Empty : ", 사유=" + (message == null ? "(없음)" : message)));

            if (!saved)
            {
                // callback 프레임을 얻지 못하면 그대로 실패로 둡니다.
                //
                // 예전에는 ffmpeg 원본 캡처와 LibVLC 스냅샷으로 차례로 복구했습니다. callback 이
                // 해상도를 알려 주지 못하던 때, 큰 해상도 채널을 건지려고 둔 길이었습니다.
                // 지금은 callback 이 cv::Mat 을 넘겨 주므로 그 길이 필요 없고, 오히려 한 카메라에
                // RTSP 연결이 겹쳐 붙어 스트림 자체를 흔들었습니다.
                //
                // 되살리려 애쓰다 12초를 흘려보내느니, 못 받았다는 사실을 그대로 남기는 편이 낫습니다.
                // 검정 이미지를 남겨 두면 어느 카메라가 언제 못 받았는지 나중에 확인할 수 있습니다.
                string callbackFailureMessage = channel.DisplayName +
                    " callback 프레임을 받지 못해 이미지를 저장하지 못했습니다. " + message;

                AppendVladRtspLog("CALLBACK-EMPTY", callbackFailureMessage);

                try
                {
                    if (File.Exists(outputFilePath))
                    {
                        File.Delete(outputFilePath);
                    }
                }
                catch (IOException)
                {
                    // 지우지 못해도 아래에서 검정 이미지를 따로 남깁니다.
                }

                CapturedImage placeholder = SavePlaceholderBlackImage(
                    channel,
                    channel.ViewType,
                    part,
                    new InvalidOperationException(callbackFailureMessage),
                    inspectionStartedAt);

                if (placeholder != null)
                {
                    return placeholder;
                }

                throw new InvalidOperationException(callbackFailureMessage);
            }

            CapturedImage image = new CapturedImage();
            image.ViewType = channel.ViewType;
            image.DisplayName = channel.DisplayName;
            image.FilePath = outputFilePath;
            image.CapturedAt = capturedAt == DateTime.MinValue ? requestedAt : capturedAt;

            SetDirectSdkStatus(BuildDirectSdkStatus(channel, channel.ViewType, true, message, image.FilePath));
            return image;
        }

        /// <summary>
        /// 카메라 캡처가 (모든 경로를 시도한 뒤에도) 실패했을 때, 저장 자체가 막히지 않도록 검정
        /// 이미지를 대신 저장합니다. 카메라 한 대가 고장나도 나머지 채널 저장/검사는 계속 진행되어야
        /// 하기 때문입니다.
        /// </summary>
        private CapturedImage SavePlaceholderBlackImage(CameraChannelConfig channel, ImageViewType viewType, Part part, Exception captureException, DateTime inspectionStartedAt)
        {
            if (channel == null)
            {
                return null;
            }

            string failureReason = captureException == null ? "알 수 없는 오류" : captureException.Message;
            string failureMessage = channel.DisplayName + " 카메라 캡처 실패로 검정 이미지로 저장합니다: " + failureReason;
            AppendVladRtspLog("FAILED", failureMessage);

            try
            {
                int width = channel.Width > 0 ? channel.Width : VLAD_Ops_RTSP.CallbackFrameWidth;
                int height = channel.Height > 0 ? channel.Height : VLAD_Ops_RTSP.CallbackFrameHeight;
                string outputFilePath = BuildVladRtspCaptureFilePath(channel, part, inspectionStartedAt);

                using (Mat blackMat = new Mat(height, width, MatType.CV_8UC3, Scalar.Black))
                {
                    Cv2.ImWrite(outputFilePath, blackMat);
                }

                CapturedImage image = new CapturedImage();
                image.ViewType = viewType;
                image.DisplayName = channel.DisplayName;
                image.FilePath = outputFilePath;
                image.CapturedAt = DateTime.Now;
                image.IsPlaceholder = true;
                image.PlaceholderReason = failureReason;

                SetDirectSdkStatus(BuildDirectSdkStatus(channel, viewType, false, failureMessage, outputFilePath));
                return image;
            }
            catch (Exception blackImageException)
            {
                SetDirectSdkStatus(BuildDirectSdkStatus(channel, viewType, false, failureMessage + " / 검정 이미지 저장도 실패: " + blackImageException.Message, string.Empty));
                return null;
            }
        }

        // 프로그램 실행 또는 설정 재로드 시 RTSP/NVR 채널을 VLAD SDK에 등록합니다.
        // 등록 이후부터 VLAD_Ops_RTSP_Frame_Proc callback이 지속적으로 프레임을 받습니다.
        private void EnsureVladRtspRegistrationsForConfiguredChannels()
        {
            // 이 등록은 채널마다 메인 스트림 연결을 상시로 붙듭니다.
            // 채널 해상도가 callback 버퍼를 넘는 현장에서는 검사 캡처에 쓰이지 않고
            // 복구 경로로만 남으면서 NVR 출력 대역폭을 계속 차지합니다.
            // 대역폭이 부족한 현장에서 회수할 수 있도록 CFG에서 끌 수 있게 했습니다.
            // RTSP callback 은 화면과 검사가 모두 기대는 기본 경로라 끄지 않습니다.
            //
            // 예전에는 설정으로 끌 수 있게 두었습니다. LibVLC 스트리밍 6개와 callback 6개가
            // 함께 붙어 NVR 대역폭을 나눠 쓰던 때, 어느 쪽이 원인인지 보려던 것이었습니다.
            // 화면을 callback 프레임으로 그리게 되면 LibVLC 연결이 없어져 그럴 이유도 사라집니다.
            //
            // 설정으로 남겨 두면 실수로 꺼졌을 때 프레임이 한 장도 오지 않는데,
            // 화면이 비는 것 말고는 단서가 없어 원인을 찾기 어렵습니다.

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
                if (monitorIndex < 0)
                {
                    // 방향을 알 수 없는 채널은 등록하지 않습니다.
                    //
                    // 예전에는 이런 채널이 0 번, 곧 Top 으로 등록되었습니다. 그러면 Top 화면에
                    // 다른 카메라 그림이 올라오고, 검사도 그 그림을 Top 으로 찍습니다.
                    // 화면만 보고는 알아채기 어려우므로 등록을 막고 기록을 남깁니다.
                    AppendVladRtspLog(
                        "SKIP",
                        channel.DisplayName + " 카메라 방향을 알 수 없어 VLAD RTSP 등록을 건너뜁니다. " +
                        "Config 의 CAM_VIEW 값을 확인하십시오. ViewType=" + channel.ViewType);
                    return;
                }

                string cameraName = string.IsNullOrWhiteSpace(channel.CameraKey) ? channel.DisplayName : channel.CameraKey;

                // frame_width/frame_height는 이제 프레임을 읽는 데 쓰지 않습니다.
                // callback이 cv::Mat을 넘겨 주므로 크기를 Mat에서 직접 읽습니다.
                //
                // 예전에는 display가 픽셀 주소뿐이라 크기를 짐작해야 했고, Config 해상도(2592x1944)로
                // 읽었다가 실제 버퍼보다 크게 읽어 Marshal.Copy가 돌아오지 않는 일이 있었습니다.
                // 그래서 1920x1080으로 고정해 두었는데 이제 그럴 필요가 없습니다.
                // 설정한 해상도를 계측에 알려 둡니다.
                // 실제로 들어오는 크기와 다르면 로그에 함께 적혀 현장에서 알아챌 수 있습니다.
                RtspFrameMetrics.RegisterExpectedSize(monitorIndex, channel.Width, channel.Height);

                var param = new VLAD_Ops_RTSP.VLAD_Ops_RTSP_ThreadParam(
                    vladId,
                    _settings.SiteName,
                    VLAD_Ops_RTSP.MODE_TYPE_CAM,
                    monitorIndex,
                    rtspUrl,
                    cameraName,
                    _settings.Threshold,
                    VLAD_Ops_RTSP.CallbackFrameWidth,
                    VLAD_Ops_RTSP.CallbackFrameHeight);

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
                    ", CallbackResolution=" +
                    VLAD_Ops_RTSP.CallbackFrameWidth.ToString() +
                    "x" +
                    VLAD_Ops_RTSP.CallbackFrameHeight.ToString());
                Debug.WriteLine(
                    "VLAD RTSP 등록 완료: " +
                    channel.ViewType +
                    ", URL=" +
                    rtspUrl +
                    ", ConfiguredResolution=" +
                    channel.Width.ToString() +
                    "x" +
                    channel.Height.ToString() +
                    ", CallbackResolution=" +
                    VLAD_Ops_RTSP.CallbackFrameWidth.ToString() +
                    "x" +
                    VLAD_Ops_RTSP.CallbackFrameHeight.ToString());
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
                string logFilePath = ApplicationLogFileResolver.GetLogFilePath(_projectRootPath, "vlad-rtsp");
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
            // 번호 규칙은 RtspMonitorIndexPolicy 한 곳에서만 정합니다.
            // 화면을 그리는 쪽도 같은 규칙으로 프레임을 찾아가야 합니다.
            return RtspMonitorIndexPolicy.FromViewType(viewType);
        }
    }
}
