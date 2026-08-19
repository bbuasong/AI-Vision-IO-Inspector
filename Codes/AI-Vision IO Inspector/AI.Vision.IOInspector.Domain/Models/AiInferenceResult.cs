using System.Collections.Generic;

using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// AI 추론 결과입니다. 실제 AI 출력 스키마가 확정되기 전까지 Application 계층에서 사용할 표준 형태입니다.
    /// </summary>
    public class AiInferenceResult
    {
        public AiInferenceResult()
        {
            ViewResults = new Dictionary<ImageViewType, AiViewInferenceResult>();
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

        /// <summary>
        /// 방향별 검사 결과입니다. 키는 카메라 방향입니다.
        /// IsMatched와 Confidence는 6방향을 합친 값이라 방향마다 다르게 보여줄 수 없습니다.
        /// 결과 기록 이미지처럼 방향별 값이 필요한 곳에서 씁니다.
        /// </summary>
        public IDictionary<ImageViewType, AiViewInferenceResult> ViewResults { get; private set; }
    }
}
