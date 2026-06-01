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

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            IList<CameraChannelStatus> statuses = new List<CameraChannelStatus>();
            foreach (ImageViewType viewType in Enum.GetValues(typeof(ImageViewType)))
            {
                CameraChannelStatus status = new CameraChannelStatus();
                status.ChannelId = viewType.ToString().ToUpperInvariant();
                status.ViewType = viewType;
                status.DisplayName = viewType.ToString() + " View";
                status.ConnectionType = CameraConnectionType.Simulated;
                status.IsEnabled = true;
                status.IsConnected = true;
                status.Message = "단순 시뮬레이션";
                status.LastFramePath = string.Empty;
                status.CheckedAt = DateTime.Now;
                statuses.Add(status);
            }

            return statuses;
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
                images.Add(Capture(viewType, part));
            }

            return images;
        }
    }
}
