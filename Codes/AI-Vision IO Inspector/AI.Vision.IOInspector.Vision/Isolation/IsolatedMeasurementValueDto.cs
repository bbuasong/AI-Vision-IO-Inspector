using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Isolation
{
    /// <summary>
    /// 외부 추론 프로세스가 반환하는 측정부별 측정값입니다.
    /// </summary>
    public class IsolatedMeasurementValueDto
    {
        public int MeasurementRegionId { get; set; }

        public string Name { get; set; }

        public decimal Value { get; set; }

        public string Unit { get; set; }

        public decimal RawPixelValue { get; set; }

        public static IsolatedMeasurementValueDto FromVisionMeasurementValue(VisionMeasurementValue source)
        {
            IsolatedMeasurementValueDto dto = new IsolatedMeasurementValueDto();
            if (source == null)
            {
                return dto;
            }

            dto.MeasurementRegionId = source.MeasurementRegionId;
            dto.Name = source.Name;
            dto.Value = source.Value;
            dto.Unit = source.Unit;
            dto.RawPixelValue = source.RawPixelValue;
            return dto;
        }
    }
}
