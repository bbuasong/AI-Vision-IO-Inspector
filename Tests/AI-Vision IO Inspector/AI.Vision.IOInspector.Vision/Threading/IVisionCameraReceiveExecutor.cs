using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// 연속 미리보기 Worker가 최신 프레임을 가져올 때 사용하는 실행 경계입니다.
    /// 실제 IMV SDK 구현에서는 파일 저장 없이 메모리 프레임을 복사해 CapturedImage 또는 VisionFrame으로 변환해야 합니다.
    /// </summary>
    public interface IVisionCameraReceiveExecutor
    {
        CapturedImage ReceiveLatestFrame(ImageViewType viewType);
    }
}
