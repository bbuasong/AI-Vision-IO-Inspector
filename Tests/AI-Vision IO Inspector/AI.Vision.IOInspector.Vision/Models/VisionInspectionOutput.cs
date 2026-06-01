using System.Collections.Generic;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// 카메라/AI Vision 엔진이 반환하는 표준 출력 모델입니다.
    /// 서비스 어댑터는 이 결과를 현재 애플리케이션 결과 모델로 변환합니다.
    /// </summary>
    public class VisionInspectionOutput
    {
        public VisionInspectionOutput()
        {
            Measurements = new List<VisionMeasurementValue>();
        }

        public bool IsSuccess { get; set; }

        public bool IsMatched { get; set; }

        public string PredictedClass { get; set; }

        public decimal Confidence { get; set; }

        public string Message { get; set; }

        public string ModelVersion { get; set; }

        public IList<VisionMeasurementValue> Measurements { get; private set; }
    }
}
