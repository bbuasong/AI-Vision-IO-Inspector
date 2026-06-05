using System;
using System.Collections.Generic;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 설정 파일을 기준으로 6개 카메라 채널을 제어하는 카메라 서비스입니다.
    /// 현재는 Simulated/File 소스를 안정적으로 제공하고, DirectSdk/RTSP는 동일한 경계에 실제 어댑터를 연결하는 구조입니다.
    /// </summary>
    public class ConfiguredCameraService : ICameraService
    {
        private readonly string _rootPath;
        private readonly CameraConfigurationStore _configurationStore;
        private readonly CameraFrameSourceFactory _frameSourceFactory;
        private readonly RtspConnectionTester _connectionTester;
        private readonly IList<CameraChannelConfig> _channels;
        private readonly Dictionary<ImageViewType, CameraChannelStatus> _statuses;

        public ConfiguredCameraService(string rootPath)
        {
            _rootPath = ProjectDataRootResolver.Resolve(rootPath);
            _configurationStore = new CameraConfigurationStore(rootPath);
            _frameSourceFactory = new CameraFrameSourceFactory(rootPath);
            _connectionTester = new RtspConnectionTester();
            _channels = new List<CameraChannelConfig>();
            _statuses = new Dictionary<ImageViewType, CameraChannelStatus>();
            ReloadConfiguration();
        }

        public void ReloadConfiguration()
        {
            _channels.Clear();
            foreach (CameraChannelConfig channel in _configurationStore.Load())
            {
                _channels.Add(channel);
                _statuses[channel.ViewType] = BuildInitialStatus(channel);
            }
        }

        public IList<CameraChannelConfig> GetChannelConfigurations()
        {
            IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
            foreach (CameraChannelConfig channel in GetOrderedChannels())
            {
                channels.Add(CloneChannel(channel));
            }

            return channels;
        }

        public void SaveChannelConfigurations(IList<CameraChannelConfig> channels)
        {
            _configurationStore.Save(channels);
            ReloadConfiguration();
        }

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            IList<CameraChannelStatus> statuses = new List<CameraChannelStatus>();
            foreach (CameraChannelConfig channel in GetOrderedChannels())
            {
                if (_statuses.ContainsKey(channel.ViewType))
                {
                    statuses.Add(_statuses[channel.ViewType]);
                }
                else
                {
                    CameraChannelStatus status = BuildInitialStatus(channel);
                    _statuses[channel.ViewType] = status;
                    statuses.Add(status);
                }
            }

            return statuses;
        }

        public CameraChannelStatus TestChannelConnection(ImageViewType viewType)
        {
            return TestChannelConnection(viewType, string.Empty);
        }

        public CapturedImage Capture(ImageViewType viewType, Part part)
        {
            CameraChannelConfig channel = FindChannel(viewType);
            if (channel == null)
            {
                throw new InvalidOperationException(viewType.ToString() + " 카메라 설정을 찾을 수 없습니다.");
            }

            if (!channel.IsEnabled)
            {
                throw new InvalidOperationException(channel.DisplayName + " 카메라 채널이 비활성화되어 있습니다.");
            }

            string outputFilePath = BuildCaptureFilePath(channel, part);
            ICameraFrameSource frameSource = _frameSourceFactory.Create(channel.ConnectionType);
            try
            {
                CapturedImage image = frameSource.Capture(channel, part, outputFilePath);
                _statuses[channel.ViewType] = BuildStatus(channel, true, "촬영 완료", image.FilePath);
                return image;
            }
            catch (Exception ex)
            {
                _statuses[channel.ViewType] = BuildStatus(channel, false, ex.Message, string.Empty);
                throw;
            }
        }

        public IList<CapturedImage> CaptureAll(Part part)
        {
            IList<CapturedImage> images = new List<CapturedImage>();
            foreach (CameraChannelConfig channel in GetOrderedChannels())
            {
                if (channel.IsEnabled)
                {
                    images.Add(Capture(channel.ViewType, part));
                }
            }

            return images;
        }

        private IList<CameraChannelConfig> GetOrderedChannels()
        {
            IList<CameraChannelConfig> orderedChannels = new List<CameraChannelConfig>();
            AddChannelIfExists(orderedChannels, ImageViewType.Top);
            AddChannelIfExists(orderedChannels, ImageViewType.Front);
            AddChannelIfExists(orderedChannels, ImageViewType.Back);
            AddChannelIfExists(orderedChannels, ImageViewType.Left);
            AddChannelIfExists(orderedChannels, ImageViewType.Right);
            AddChannelIfExists(orderedChannels, ImageViewType.Thickness);
            return orderedChannels;
        }

        private void AddChannelIfExists(IList<CameraChannelConfig> orderedChannels, ImageViewType viewType)
        {
            CameraChannelConfig channel = FindChannel(viewType);
            if (channel != null)
            {
                orderedChannels.Add(channel);
            }
        }

        private CameraChannelConfig FindChannel(ImageViewType viewType)
        {
            foreach (CameraChannelConfig channel in _channels)
            {
                if (channel.ViewType == viewType)
                {
                    return channel;
                }
            }

            return null;
        }

        private CameraChannelStatus TestChannelConnection(ImageViewType viewType, string lastFramePath)
        {
            CameraChannelConfig channel = FindChannel(viewType);
            if (channel == null)
            {
                CameraChannelStatus missingStatus = new CameraChannelStatus();
                missingStatus.ViewType = viewType;
                missingStatus.DisplayName = viewType.ToString();
                missingStatus.IsConnected = false;
                missingStatus.Message = "카메라 설정을 찾을 수 없습니다.";
                missingStatus.CheckedAt = DateTime.Now;
                return missingStatus;
            }

            CameraConnectionTestResult result = _connectionTester.Test(channel);
            CameraChannelStatus status;
            if (!result.IsConnected)
            {
                status = BuildStatus(channel, false, result.Message, string.Empty);
                _statuses[channel.ViewType] = status;
                return status;
            }

            status = TestVideoFrameReception(channel, result.Message);
            _statuses[channel.ViewType] = status;
            return status;
        }

        private CameraChannelStatus TestVideoFrameReception(CameraChannelConfig channel, string connectionMessage)
        {
            if (channel.ConnectionType == CameraConnectionType.Simulated)
            {
                return BuildStatus(channel, false, "시뮬레이션 모드는 실제 카메라 영상 연결 상태로 보지 않습니다.", string.Empty);
            }

            Part statusCheckPart = new Part();
            statusCheckPart.PartNo = "CONNECTION_TEST";

            string outputFilePath = BuildCaptureFilePath(channel, statusCheckPart);
            ICameraFrameSource frameSource = _frameSourceFactory.Create(channel.ConnectionType);

            try
            {
                CapturedImage image = frameSource.Capture(channel, statusCheckPart, outputFilePath);
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath) || !File.Exists(image.FilePath))
                {
                    return BuildStatus(channel, false, "영상 프레임 수신 실패: 캡처 파일이 생성되지 않았습니다.", string.Empty);
                }

                return BuildStatus(channel, true, "영상 프레임 수신 완료", image.FilePath);
            }
            catch (Exception ex)
            {
                return BuildStatus(channel, false, BuildVideoFrameFailureMessage(connectionMessage, ex), string.Empty);
            }
        }

        private string BuildVideoFrameFailureMessage(string connectionMessage, Exception ex)
        {
            string frameMessage = ex == null ? "상세 오류 없음" : ex.Message;
            if (string.IsNullOrWhiteSpace(connectionMessage))
            {
                return "영상 프레임 수신 실패: " + frameMessage;
            }

            return "영상 프레임 수신 실패: " + frameMessage + " / 포트 확인: " + connectionMessage;
        }

        private CameraChannelStatus BuildInitialStatus(CameraChannelConfig channel)
        {
            return BuildStatus(channel, false, "설정 로드 완료. 상태 새로고침으로 실제 연결을 확인하세요.", string.Empty);
        }

        private CameraChannelStatus BuildStatus(CameraChannelConfig channel, bool isConnected, string message, string lastFramePath)
        {
            CameraChannelStatus status = new CameraChannelStatus();
            status.ChannelId = channel.ChannelId;
            status.ViewType = channel.ViewType;
            status.DisplayName = channel.DisplayName;
            status.CameraModel = channel.CameraModel;
            status.ConnectionType = channel.ConnectionType;
            status.IsEnabled = channel.IsEnabled;
            status.IsConnected = isConnected;
            status.IpAddress = channel.IpAddress;
            status.Port = channel.Port;
            status.UserName = channel.UserName;
            status.Password = channel.Password;
            status.SerialNumber = channel.SerialNumber;
            status.DeviceUserId = channel.DeviceUserId;
            status.CameraKey = channel.CameraKey;
            status.RtspUrl = RtspUrlBuilder.Build(channel);
            status.StreamPath = channel.StreamPath;
            status.NvrChannel = channel.NvrChannel;
            status.Width = channel.Width;
            status.Height = channel.Height;
            status.Fps = channel.Fps;
            status.ExposureTime = channel.ExposureTime;
            status.Gain = channel.Gain;
            status.TriggerMode = channel.TriggerMode;
            status.Message = message;
            status.LastFramePath = lastFramePath;
            status.CheckedAt = DateTime.Now;
            return status;
        }

        private CameraChannelConfig CloneChannel(CameraChannelConfig source)
        {
            CameraChannelConfig channel = new CameraChannelConfig();
            channel.ChannelId = source.ChannelId;
            channel.ViewType = source.ViewType;
            channel.DisplayName = source.DisplayName;
            channel.CameraModel = source.CameraModel;
            channel.ConnectionType = source.ConnectionType;
            channel.IsEnabled = source.IsEnabled;
            channel.IpAddress = source.IpAddress;
            channel.Port = source.Port;
            channel.UserName = source.UserName;
            channel.Password = source.Password;
            channel.SerialNumber = source.SerialNumber;
            channel.DeviceUserId = source.DeviceUserId;
            channel.CameraKey = source.CameraKey;
            channel.RtspUrl = source.RtspUrl;
            channel.StreamPath = source.StreamPath;
            channel.NvrChannel = source.NvrChannel;
            channel.Width = source.Width;
            channel.Height = source.Height;
            channel.Fps = source.Fps;
            channel.ExposureTime = source.ExposureTime;
            channel.Gain = source.Gain;
            channel.TriggerMode = source.TriggerMode;
            channel.SdkDllPath = source.SdkDllPath;
            channel.NativeDllDirectory = source.NativeDllDirectory;
            channel.SnapshotFilePath = source.SnapshotFilePath;
            return channel;
        }

        private string BuildCaptureFilePath(CameraChannelConfig channel, Part part)
        {
            string partNo = part == null ? "UNKNOWN" : part.PartNo;
            string dayPath = Path.Combine(_rootPath, "RuntimeData", "CameraCaptures", DateTime.Now.ToString("yyyyMMdd"));
            string fileName = SanitizeFileName(partNo) + "_" + channel.ViewType.ToString() + "_" + DateTime.Now.ToString("HHmmssfff") + ResolveOutputExtension(channel);
            return Path.Combine(dayPath, fileName);
        }

        private string ResolveOutputExtension(CameraChannelConfig channel)
        {
            if (channel.ConnectionType == CameraConnectionType.Rtsp || channel.ConnectionType == CameraConnectionType.NvrRtsp)
            {
                return ".jpg";
            }

            if (channel.ConnectionType == CameraConnectionType.File && !string.IsNullOrWhiteSpace(channel.SnapshotFilePath))
            {
                string extension = Path.GetExtension(channel.SnapshotFilePath);
                if (!string.IsNullOrWhiteSpace(extension))
                {
                    return extension;
                }
            }

            return ".bmp";
        }

        private string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UNKNOWN";
            }

            string sanitized = value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }

            return sanitized;
        }
    }
}
