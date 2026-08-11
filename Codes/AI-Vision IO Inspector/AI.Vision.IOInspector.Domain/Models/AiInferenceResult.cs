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
            MeasurementUnits = new Dictionary<int, string>();
            RawPixelValues = new Dictionary<int, decimal>();
            MeasurementJudgments = new Dictionary<int, bool>();
            MeasurementJudgeTexts = new Dictionary<int, string>();
        }

        public bool IsSuccess { get; set; }

        public bool IsMatched { get; set; }

        /// <summary>
        /// 최종 판정과 측정부 판정이 AI DLL에서 직접 반환되었는지 여부입니다.
        /// </summary>
        public bool HasAuthoritativeJudgment { get; set; }

        public string PredictedClass { get; set; }

        public decimal Confidence { get; set; }

        /// <summary>
        /// AI 결과에 Score 값이 실제로 포함되었는지 여부입니다.
        /// </summary>
        public bool HasScore { get; set; }

        public string Message { get; set; }

        public decimal? DimensionWidth { get; set; }

        public decimal? DimensionDepth { get; set; }

        public decimal? DimensionHeight { get; set; }

        public string DimensionUnit { get; set; }

        public IDictionary<int, decimal> MeasurementValues { get; private set; }

        public IDictionary<int, string> MeasurementUnits { get; private set; }

        public IDictionary<int, decimal> RawPixelValues { get; private set; }

        public IDictionary<int, bool> MeasurementJudgments { get; private set; }

        public IDictionary<int, string> MeasurementJudgeTexts { get; private set; }

        public string ModelVersion { get; set; }
    }
}
