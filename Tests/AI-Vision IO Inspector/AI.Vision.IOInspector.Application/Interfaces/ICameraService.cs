using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 카메라 SDK/RTSP/NVR 연동 경계입니다.
    /// UI와 검사 흐름은 이 인터페이스만 사용하고, 실제 장비별 제어는 Infrastructure 어댑터가 담당합니다.
    /// </summary>
    public interface ICameraService
    {
        void ReloadConfiguration();

        IList<CameraChannelStatus> GetChannelStatuses();

        CapturedImage Capture(ImageViewType viewType, Part part);

        IList<CapturedImage> CaptureAll(Part part);
    }
}
