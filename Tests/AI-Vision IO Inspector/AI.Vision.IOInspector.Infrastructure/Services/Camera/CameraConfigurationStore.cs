using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 6개 카메라와 검사 방향의 매핑 설정을 JSON 파일로 관리합니다.
    /// 추후 Option UI는 이 저장소를 통해 카메라 IP, RTSP URL, SDK 식별자를 수정하게 됩니다.
    /// </summary>
    public class CameraConfigurationStore
    {
        private readonly string _configFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public CameraConfigurationStore(string rootPath)
        {
            string resolvedRootPath = ProjectDataRootResolver.Resolve(rootPath);
            _configFilePath = Path.Combine(resolvedRootPath, "RuntimeData", "Camera", "camera-config.json");
            _jsonOptions = new JsonSerializerOptions();
            _jsonOptions.WriteIndented = true;
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public IList<CameraChannelConfig> Load()
        {
            if (!File.Exists(_configFilePath))
            {
                IList<CameraChannelConfig> defaultChannels = BuildDefaultChannels();
                Save(defaultChannels);
                return defaultChannels;
            }

            try
            {
                string json = File.ReadAllText(_configFilePath);
                IList<CameraChannelConfig> loadedChannels = JsonSerializer.Deserialize<List<CameraChannelConfig>>(json, _jsonOptions);
                return NormalizeChannels(loadedChannels);
            }
            catch
            {
                IList<CameraChannelConfig> defaultChannels = BuildDefaultChannels();
                Save(defaultChannels);
                return defaultChannels;
            }
        }

        public void Save(IList<CameraChannelConfig> channels)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configFilePath));
            string json = JsonSerializer.Serialize(NormalizeChannels(channels), _jsonOptions);
            File.WriteAllText(_configFilePath, json);
        }

        private IList<CameraChannelConfig> NormalizeChannels(IList<CameraChannelConfig> channels)
        {
            IList<CameraChannelConfig> normalizedChannels = new List<CameraChannelConfig>();
            IList<CameraChannelConfig> defaultChannels = BuildDefaultChannels();
            foreach (CameraChannelConfig defaultChannel in defaultChannels)
            {
                CameraChannelConfig channel = FindChannel(channels, defaultChannel.ViewType);
                if (channel == null)
                {
                    normalizedChannels.Add(defaultChannel);
                }
                else
                {
                    FillMissingValues(channel, defaultChannel);
                    normalizedChannels.Add(channel);
                }
            }

            return normalizedChannels;
        }

        private CameraChannelConfig FindChannel(IList<CameraChannelConfig> channels, ImageViewType viewType)
        {
            if (channels == null)
            {
                return null;
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

        private void FillMissingValues(CameraChannelConfig channel, CameraChannelConfig defaultChannel)
        {
            if (string.IsNullOrWhiteSpace(channel.ChannelId))
            {
                channel.ChannelId = defaultChannel.ChannelId;
            }

            if (string.IsNullOrWhiteSpace(channel.DisplayName))
            {
                channel.DisplayName = defaultChannel.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(channel.CameraModel))
            {
                channel.CameraModel = defaultChannel.CameraModel;
            }

            if (channel.Width <= 0)
            {
                channel.Width = defaultChannel.Width;
            }

            if (channel.Height <= 0)
            {
                channel.Height = defaultChannel.Height;
            }

            if (channel.Fps <= 0)
            {
                channel.Fps = defaultChannel.Fps;
            }

            if (channel.Port <= 0)
            {
                channel.Port = defaultChannel.Port;
            }

            if (string.IsNullOrWhiteSpace(channel.StreamPath))
            {
                channel.StreamPath = defaultChannel.StreamPath;
            }

            if (string.IsNullOrWhiteSpace(channel.UserName))
            {
                channel.UserName = defaultChannel.UserName;
            }

            if (channel.Password == null)
            {
                channel.Password = string.Empty;
            }
        }

        private IList<CameraChannelConfig> BuildDefaultChannels()
        {
            IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
            channels.Add(BuildChannel(ImageViewType.Top, "Top View", "DC-T3145G", 2448, 2048, 30, 1));
            channels.Add(BuildChannel(ImageViewType.Front, "Front View", "DC-T3145R", 2592, 1944, 30, 2));
            channels.Add(BuildChannel(ImageViewType.Back, "Back View", "DC-T3145R", 2592, 1944, 30, 3));
            channels.Add(BuildChannel(ImageViewType.Left, "Left View", "DC-T3145R", 2592, 1944, 30, 4));
            channels.Add(BuildChannel(ImageViewType.Right, "Right View", "DC-T3145R", 2592, 1944, 30, 5));
            channels.Add(BuildChannel(ImageViewType.Thickness, "Thickness", "DC-T3145G", 2448, 2048, 30, 6));
            return channels;
        }

        private CameraChannelConfig BuildChannel(ImageViewType viewType, string displayName, string cameraModel, int width, int height, int fps, int nvrChannel)
        {
            CameraChannelConfig channel = new CameraChannelConfig();
            channel.ChannelId = viewType.ToString().ToUpperInvariant();
            channel.ViewType = viewType;
            channel.DisplayName = displayName;
            channel.CameraModel = cameraModel;
            channel.ConnectionType = CameraConnectionType.Simulated;
            channel.IsEnabled = true;
            channel.NvrChannel = nvrChannel;
            channel.Width = width;
            channel.Height = height;
            channel.Fps = fps;
            channel.ExposureTime = 0;
            channel.Gain = 0;
            channel.TriggerMode = CameraTriggerMode.Continuous;
            channel.IpAddress = string.Empty;
            channel.Port = 554;
            channel.UserName = "admin";
            channel.Password = string.Empty;
            channel.SerialNumber = string.Empty;
            channel.DeviceUserId = string.Empty;
            channel.CameraKey = string.Empty;
            channel.RtspUrl = string.Empty;
            channel.StreamPath = "trackID=1";
            channel.SdkDllPath = string.Empty;
            channel.NativeDllDirectory = string.Empty;
            channel.SnapshotFilePath = string.Empty;
            return channel;
        }
    }
}
