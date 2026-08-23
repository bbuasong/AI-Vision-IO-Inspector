using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// 백그라운드 검사 흐름의 현재 단계를 WPF 화면에 전달합니다.
    /// 카메라 영상은 계속 표시하고 화면에는 진행 상태만 겹쳐 보여주기 위해 사용합니다.
    /// </summary>
    public class InspectionProgressEventArgs : EventArgs
    {
        public InspectionProgressEventArgs(InspectionStatus status, string message)
            : this(status, message, null)
        {
        }

        public InspectionProgressEventArgs(
            InspectionStatus status,
            string message,
            IList<CapturedImage> capturedImages)
        {
            Status = status;
            Message = message ?? string.Empty;
            CapturedImages = capturedImages;
        }

        public InspectionStatus Status { get; private set; }

        public string Message { get; private set; }

        /// <summary>
        /// 이 단계에서 막 찍은 사진들입니다. 찍은 직후가 아니면 null 입니다.
        ///
        /// <para>
        /// 검사가 도는 동안 화면에 무엇을 보여 줄지 때문에 필요합니다. 찍기가 끝나면
        /// 판정에 쓰이는 사진이 정해지는데, 그때부터 그 사진을 붙박이로 보여 주어야
        /// 무엇을 보고 판정했는지 알 수 있습니다. 검사가 끝날 때까지 영상이 계속 흐르면
        /// 화면에 보이는 것과 판정에 쓰인 것이 서로 달라집니다.
        /// </para>
        /// </summary>
        public IList<CapturedImage> CapturedImages { get; private set; }
    }
}
