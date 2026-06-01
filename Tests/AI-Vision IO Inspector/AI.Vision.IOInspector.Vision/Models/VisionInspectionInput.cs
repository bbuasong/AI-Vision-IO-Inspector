using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// 애플리케이션 검사 흐름에서 Vision 추론 엔진으로 전달하는 입력 묶음입니다.
    /// 선택된 부품 기준정보와 촬영된 카메라 이미지를 함께 담습니다.
    /// </summary>
    public class VisionInspectionInput
    {
        public VisionInspectionInput()
        {
            CapturedImages = new List<CapturedImage>();
        }

        public Part Part { get; set; }

        public IList<CapturedImage> CapturedImages { get; private set; }
    }
}
