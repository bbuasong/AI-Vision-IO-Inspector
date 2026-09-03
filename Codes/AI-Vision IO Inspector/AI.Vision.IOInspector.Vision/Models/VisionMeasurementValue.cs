using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// AI 또는 영상 처리에서 추출한 측정값 1건입니다.
    /// MeasurementRegionId는 이 값을 DB에 등록된 기준 측정부와 연결합니다.
    /// </summary>
    public class VisionMeasurementValue
    {
        public int MeasurementRegionId { get; set; }

        public string Name { get; set; }

        public ImageViewType ViewType { get; set; }

        public decimal Value { get; set; }

        public string Unit { get; set; }

        public decimal RawPixelValue { get; set; }

        /// <summary>AI 가 응답에 실어 준 기준값입니다. 없으면 null 입니다.</summary>
        public decimal? AiNominalValue { get; set; }

        /// <summary>AI 가 응답에 실어 준 허용오차입니다. 기준값이 있을 때만 의미가 있습니다.</summary>
        public decimal AiToleranceMin { get; set; }

        public decimal AiToleranceMax { get; set; }

        public string SourceImagePath { get; set; }

        public string CalibrationId { get; set; }

        /// <summary>
        /// 신규 HD 결과 JSON에 측정부별 AI 판정이 포함되었는지 여부입니다.
        /// </summary>
        public bool HasAiJudge { get; set; }

        public bool IsAiPass { get; set; }

        public string AiJudge { get; set; }
    }
}
