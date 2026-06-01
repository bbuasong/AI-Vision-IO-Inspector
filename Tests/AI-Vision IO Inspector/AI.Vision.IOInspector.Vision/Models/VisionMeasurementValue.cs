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

        public string SourceImagePath { get; set; }

        public string CalibrationId { get; set; }
    }
}
