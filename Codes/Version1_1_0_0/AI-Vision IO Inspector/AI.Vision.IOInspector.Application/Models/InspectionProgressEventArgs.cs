using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// 백그라운드 검사 흐름의 현재 단계를 WPF 화면에 전달합니다.
    /// 카메라 영상은 계속 표시하고 화면에는 진행 상태만 겹쳐 보여주기 위해 사용합니다.
    /// </summary>
    public class InspectionProgressEventArgs : EventArgs
    {
        public InspectionProgressEventArgs(InspectionStatus status, string message)
        {
            Status = status;
            Message = message ?? string.Empty;
        }

        public InspectionStatus Status { get; private set; }

        public string Message { get; private set; }
    }
}
