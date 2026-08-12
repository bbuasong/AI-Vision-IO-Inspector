using System;
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
        /// <summary>
        /// <paramref name="inspectionStartedAt"/>은 검사 한 번을 구분하는 시각입니다.
        /// 이 값으로 저장 폴더와 파일 이름을 만들기 때문에 같은 검사의 6방향은 모두 같은 값을 받아야 합니다.
        /// </summary>
        CapturedImage ExecuteCapture(ImageViewType viewType, Part part, DateTime inspectionStartedAt);
    }
}
