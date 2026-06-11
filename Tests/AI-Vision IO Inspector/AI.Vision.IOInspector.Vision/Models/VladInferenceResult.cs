using System.Collections.Generic;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// VLAD_SDK detectData에서 해석한 1회 추론 결과입니다.
    /// 현재 VLAD 기본 결과는 불량 검출 정보 중심이며, 치수값은 detectText 또는 보정 매핑으로 변환합니다.
    /// </summary>
    public class VladInferenceResult
    {
        public VladInferenceResult()
        {
            Detections = new List<VladDetection>();
            ClassCounts = new int[0];
        }

        public bool IsSuccess { get; set; }

        public int ValidDetectionCount { get; set; }

        public string DetectText { get; set; }

        public IList<VladDetection> Detections { get; private set; }

        public int[] ClassCounts { get; set; }

        public string Message { get; set; }
    }
}
