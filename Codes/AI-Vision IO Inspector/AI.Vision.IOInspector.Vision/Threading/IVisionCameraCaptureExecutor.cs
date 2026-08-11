using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// 카메라 Worker가 실제 촬영 동작을 호출할 때 사용하는 실행 경계입니다.
    /// 지금은 설정 기반 카메라 서비스를 호출하고, 추후 IMV/RTSP 구현체로 교체할 수 있습니다.
    /// </summary>
    public interface IVisionCameraCaptureExecutor
    {
        CapturedImage ExecuteCapture(ImageViewType viewType, Part part);
    }
}
