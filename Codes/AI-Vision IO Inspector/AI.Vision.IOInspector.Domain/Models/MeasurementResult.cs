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
        /// 이 측정값이 판정 대상인지입니다.
        ///
        /// <para>
        /// 판정은 사용자가 좌표를 긋고 기준값을 넣은 측정부에만 합니다(AI 담당자 협의,
        /// 2026-09-03). 그 밖의 측정값은 AI 가 참고로 만들어 준 값이라, 판정 없이 값만
        /// 보여 주고 전체 판정에도 넣지 않습니다. 참고값의 IsPass 는 언제나 true 로 두어
        /// 옛 코드가 실수로 읽어도 FAIL 을 만들지 않게 합니다.
        /// </para>
        /// </summary>
        public bool IsJudged { get; set; } = true;

        /// <summary>
        /// 허용 범위를 벗어난 크기입니다. 범위 안이면 0입니다.
        /// 하한 미달이면 음수, 상한 초과면 양수로 남겨 어느 쪽으로 벗어났는지 알 수 있게 합니다.
        /// 통계와 이력에서 "어느 측정부가 얼마나 벗어났는지"를 보기 위한 값입니다.
        /// </summary>
        public decimal Deviation { get; set; }

        public string Message { get; set; }
    }
}
