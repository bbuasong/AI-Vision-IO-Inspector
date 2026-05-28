using System.Collections.Generic;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// AI 추론 결과입니다. 실제 AI 출력 스키마가 확정되기 전까지 Application 계층에서 사용할 표준 형태입니다.
    /// </summary>
    public class AiInferenceResult
    {
        public AiInferenceResult()
        {
            MeasurementValues = new Dictionary<int, decimal>();
        }

        public bool IsSuccess { get; set; }

        public bool IsMatched { get; set; }

        public string PredictedClass { get; set; }

        public decimal Confidence { get; set; }

        public string Message { get; set; }

        public IDictionary<int, decimal> MeasurementValues { get; private set; }
    }
}
