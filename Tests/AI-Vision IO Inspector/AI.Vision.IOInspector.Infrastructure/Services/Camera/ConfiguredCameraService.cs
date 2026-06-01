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
        private readonly IList<CameraChannelConfig> _channels;
        private readonly Dictionary<ImageViewType, CameraChannelStatus> _statuses;

        public ConfiguredCameraService(string rootPath)
        {
            _rootPath = ProjectDataRootResolver.Resolve(rootPath);
            _configurationStore = new CameraConfigurationStore(rootPath);
            _frameSourceFactory = new CameraFrameSourceFactory();
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

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            IList<CameraChannelStatus> statuses = new List<CameraChannelStatus>();
            foreach (CameraChannelConfig channel in GetOrderedChannels())
            {
                if (_statuses.ContainsKey(channel.ViewType))
                {
                    statuses.Add(_statuses[channel.ViewType]);
                }
            }

            return statuses;
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

        private CameraChannelStatus BuildInitialStatus(CameraChannelConfig channel)
        {
            return BuildStatus(channel, channel.ConnectionType == CameraConnectionType.Simulated || channel.ConnectionType == CameraConnectionType.File, "설정 로드 완료", string.Empty);
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
            status.SerialNumber = channel.SerialNumber;
            status.DeviceUserId = channel.DeviceUserId;
            status.CameraKey = channel.CameraKey;
            status.RtspUrl = channel.RtspUrl;
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

        private string BuildCaptureFilePath(CameraChannelConfig channel, Part part)
        {
            string partNo = part == null ? "UNKNOWN" : part.PartNo;
            string dayPath = Path.Combine(_rootPath, "RuntimeData", "CameraCaptures", DateTime.Now.ToString("yyyyMMdd"));
            string fileName = SanitizeFileName(partNo) + "_" + channel.ViewType.ToString() + "_" + DateTime.Now.ToString("HHmmssfff") + ResolveOutputExtension(channel);
            return Path.Combine(dayPath, fileName);
        }

        private string ResolveOutputExtension(CameraChannelConfig channel)
        {
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
