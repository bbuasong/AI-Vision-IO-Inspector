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

        /// <summary>
        /// 허용 범위를 벗어난 크기입니다. 범위 안이면 0입니다.
        /// 하한 미달이면 음수, 상한 초과면 양수로 남겨 어느 쪽으로 벗어났는지 알 수 있게 합니다.
        /// 통계와 이력에서 "어느 측정부가 얼마나 벗어났는지"를 보기 위한 값입니다.
        /// </summary>
        public decimal Deviation { get; set; }

        public string Message { get; set; }
    }
}
