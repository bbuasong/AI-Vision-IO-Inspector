using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 설정 파일을 사용하지 않는 단순 시뮬레이션 카메라입니다.
    /// 기본 앱은 ConfiguredCameraService를 사용하지만, 단위 테스트나 빠른 흐름 확인을 위해 남겨둡니다.
    /// </summary>
    public class SimulatedCameraService : ICameraService
    {
        public void ReloadConfiguration()
        {
        }

        /// <summary>흉내내는 카메라는 늘 프레임을 만들 수 있어 이을 것이 없습니다.</summary>
        public void EnsureLiveFrameSources()
        {
        }

        public IList<CameraChannelConfig> GetChannelConfigurations()
        {
            IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
            foreach (ImageViewType viewType in Enum.GetValues(typeof(ImageViewType)))
            {
                if (viewType == ImageViewType.Unclassified)
                {
                    continue;
                }

                CameraChannelConfig channel = new CameraChannelConfig();
                channel.ChannelId = viewType.ToString().ToUpperInvariant();
                channel.ViewType = viewType;
                channel.DisplayName = viewType.ToString() + " View";
                channel.ConnectionType = CameraConnectionType.Simulated;
                channel.IsEnabled = true;
                channel.Port = 554;
                channel.StreamPath = "trackID=1";
                channels.Add(channel);
            }

            return channels;
        }

        public void SaveChannelConfigurations(IList<CameraChannelConfig> channels)
        {
        }

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            IList<CameraChannelStatus> statuses = new List<CameraChannelStatus>();
            foreach (ImageViewType viewType in Enum.GetValues(typeof(ImageViewType)))
            {
                if (viewType == ImageViewType.Unclassified)
                {
                    continue;
                }

                CameraChannelStatus status = new CameraChannelStatus();
                status.ChannelId = viewType.ToString().ToUpperInvariant();
                status.ViewType = viewType;
                status.DisplayName = viewType.ToString() + " View";
                status.ConnectionType = CameraConnectionType.Simulated;
                status.IsEnabled = true;
                status.IsConnected = false;
                status.Port = 554;
                status.StreamPath = "trackID=1";
                status.Message = "단순 시뮬레이션 모드입니다. 실제 카메라 연결 상태가 아닙니다.";
                status.LastFramePath = string.Empty;
                status.CheckedAt = DateTime.Now;
                statuses.Add(status);
            }

            return statuses;
        }

        public CameraChannelStatus TestChannelConnection(ImageViewType viewType)
        {
            foreach (CameraChannelStatus status in GetChannelStatuses())
            {
                if (status.ViewType == viewType)
                {
                    return status;
                }
            }

            return null;
        }

        public CapturedImage Capture(ImageViewType viewType, Part part)
        {
            CapturedImage image = new CapturedImage();
            image.ViewType = viewType;
            image.DisplayName = viewType.ToString() + " View";
            image.FilePath = "SIMULATED_CAPTURE://" + part.PartNo + "/" + viewType.ToString();
            image.CapturedAt = DateTime.Now;
            return image;
        }

        public IList<CapturedImage> CaptureAll(Part part)
        {
            IList<CapturedImage> images = new List<CapturedImage>();

            foreach (ImageViewType viewType in Enum.GetValues(typeof(ImageViewType)))
            {
                if (viewType == ImageViewType.Unclassified)
                {
                    continue;
                }

                images.Add(Capture(viewType, part));
            }

            return images;
        }
    }
}
