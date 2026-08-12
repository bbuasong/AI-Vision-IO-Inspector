namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 검사 시점의 측정부별 측정값과 판정 결과입니다.
    /// </summary>
    public class MeasurementResult
    {
        public int MeasurementRegionId { get; set; }

        public string Name { get; set; }

        public decimal NominalValue { get; set; }

        public decimal MeasuredValue { get; set; }

        public decimal ToleranceMin { get; set; }

        public decimal ToleranceMax { get; set; }

        public string Unit { get; set; }

        public bool IsPass { get; set; }

        public string Message { get; set; }
    }
}
