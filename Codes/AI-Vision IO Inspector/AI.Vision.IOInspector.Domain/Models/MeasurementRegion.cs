using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 부품마다 달라질 수 있는 측정부 정보입니다.
    /// 길이/너비/높이/두께 고정 컬럼 대신 측정부 목록으로 확장할 수 있게 분리합니다.
    ///
    /// <b>허용오차 기준 (2026-08-12 확정)</b>
    ///
    /// Min과 Max는 <b>기준값에서 얼마나 벗어나도 되는지를 나타내는 크기</b>입니다. 부호가 없는 값입니다.
    ///
    ///   기준값 100, Min 0.5, Max 0.5  ->  허용 범위 99.5 ~ 100.5
    ///
    /// 이 객체는 <b>항상 양수로만</b> 값을 들고 있습니다. 설정자가 절대값으로 정규화하므로
    /// 어떤 경로로 값이 들어와도 음수가 남지 않습니다.
    /// 부호는 사람이 읽는 자리와 밖으로 내보내는 자리에서만 붙입니다.
    ///
    ///   화면 표기      "-0.5 ~ +0.5"
    ///   SQLite 저장    tolerance_min = -0.5, tolerance_max = 0.5
    ///   AI 요청 JSON   toleranceMin = -0.5, toleranceMax = 0.5
    ///
    /// 이렇게 두면 Max에 -10 같은 값이 잘못 입력되어도 "기준값에서 10만큼 위쪽까지 허용"으로
    /// 해석되어, 범위가 뒤집히거나 아무 값도 통과하지 못하는 상황이 생기지 않습니다.
    /// </summary>
    public class MeasurementRegion
    {
        private decimal _toleranceMin;
        private decimal _toleranceMax;

        public int Id { get; set; }

        public string PartNo { get; set; }

        public int IndexNo { get; set; }

        public string Name { get; set; }

        public string ItemType { get; set; }

        public ImageViewType ViewType { get; set; }

        public string Coordinates { get; set; }

        public double? X1 { get; set; }

        public double? Y1 { get; set; }

        public double? X2 { get; set; }

        public double? Y2 { get; set; }

        public string LineColor { get; set; }

        public decimal NominalValue { get; set; }

        /// <summary>
        /// 아래쪽 허용 크기입니다. 음수를 넣어도 절대값으로 보관합니다.
        /// 기준값보다 작은 쪽으로 이만큼까지 허용한다는 뜻입니다.
        /// </summary>
        public decimal ToleranceMin
        {
            get { return _toleranceMin; }
            set { _toleranceMin = Math.Abs(value); }
        }

        /// <summary>
        /// 위쪽 허용 크기입니다. 음수를 넣어도 절대값으로 보관합니다.
        /// 기준값보다 큰 쪽으로 이만큼까지 허용한다는 뜻입니다.
        /// </summary>
        public decimal ToleranceMax
        {
            get { return _toleranceMax; }
            set { _toleranceMax = Math.Abs(value); }
        }

        public string Unit { get; set; }

        /// <summary>판정에 사용할 하한값입니다. 기준값 - Min</summary>
        public decimal LowerLimit
        {
            get { return NominalValue - _toleranceMin; }
        }

        /// <summary>판정에 사용할 상한값입니다. 기준값 + Max</summary>
        public decimal UpperLimit
        {
            get { return NominalValue + _toleranceMax; }
        }

        /// <summary>
        /// SQLite와 AI 요청 JSON에 내보낼 아래쪽 허용값입니다. 관례에 따라 음수로 표기합니다.
        /// </summary>
        public decimal SignedToleranceMin
        {
            get { return -_toleranceMin; }
        }

        /// <summary>
        /// SQLite와 AI 요청 JSON에 내보낼 위쪽 허용값입니다. 양수 그대로입니다.
        /// </summary>
        public decimal SignedToleranceMax
        {
            get { return _toleranceMax; }
        }

        /// <summary>
        /// 측정값이 허용 범위 안에 있는지 판단합니다. 경계값은 합격으로 봅니다.
        /// </summary>
        /// <summary>
        /// 이 측정부에 기준값이 채워져 있는지입니다.
        ///
        /// <para>
        /// 좌표(X1/Y1/X2/Y2)는 없어도 됩니다. 좌표가 없으면 AI가 스스로 값을 견주어
        /// 합불을 냅니다. 반면 <b>기준값이 비어 있으면 견줄 대상이 없어</b> AI가 제대로
        /// 판단하지 못합니다. 그래서 좌표 없음은 정상, 기준값 없음은 비정상으로 봅니다.
        /// </para>
        ///
        /// <para>
        /// 판단은 <b>기준값만</b> 봅니다. 허용오차가 0/0인 것은 정상입니다.
        /// 오차를 두지 않겠다는 뜻으로 실제로 쓰는 설정이며, 비어 있는 상태가 아닙니다.
        /// 예전에는 허용오차가 모두 0이면 아직 입력하지 않은 것으로 봐서,
        /// 오차를 0으로 정한 측정부까지 경고가 떴습니다.
        /// </para>
        ///
        /// <para>
        /// 기준값이 0인 측정부는 견줄 치수가 없다는 뜻이라 의미가 없습니다.
        /// 그래서 기준값 0을 아직 넣지 않은 것으로 봅니다.
        /// </para>
        /// </summary>
        public bool HasMeasurementValue
        {
            get
            {
                return NominalValue != 0m;
            }
        }

        public bool IsWithinTolerance(decimal measuredValue)
        {
            return measuredValue >= LowerLimit && measuredValue <= UpperLimit;
        }
    }
}
