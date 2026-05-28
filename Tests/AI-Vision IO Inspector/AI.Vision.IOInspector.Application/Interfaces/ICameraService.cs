using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 카메라 SDK 연동 경계입니다. 현재 개발 단계에서는 시뮬레이션 구현을 사용합니다.
    /// </summary>
    public interface ICameraService
    {
        IList<CapturedImage> CaptureAll(Part part);
    }
}
