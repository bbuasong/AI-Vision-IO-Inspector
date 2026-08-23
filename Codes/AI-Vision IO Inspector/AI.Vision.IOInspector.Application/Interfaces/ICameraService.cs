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

        /// <summary>
        /// 설정에 있는 카메라가 모두 실시간 프레임을 보내도록 연결을 확인하고, 빠진 것을 잇습니다.
        /// 여러 번 불러도 안전하며, 이미 이어진 카메라는 건너뜁니다.
        /// </summary>
        void EnsureLiveFrameSources();

        IList<CameraChannelConfig> GetChannelConfigurations();

        void SaveChannelConfigurations(IList<CameraChannelConfig> channels);

        IList<CameraChannelStatus> GetChannelStatuses();

        CameraChannelStatus TestChannelConnection(ImageViewType viewType);

        CapturedImage Capture(ImageViewType viewType, Part part);

        IList<CapturedImage> CaptureAll(Part part);
    }
}
