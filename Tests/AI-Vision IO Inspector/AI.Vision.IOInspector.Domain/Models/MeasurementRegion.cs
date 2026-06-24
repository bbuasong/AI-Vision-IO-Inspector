using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 부품마다 달라질 수 있는 측정부 정보입니다.
    /// 길이/너비/높이/두께 고정 컬럼 대신 측정부 목록으로 확장할 수 있게 분리합니다.
    /// </summary>
    public class MeasurementRegion
    {
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

        public decimal ToleranceMin { get; set; }

        public decimal ToleranceMax { get; set; }

        public string Unit { get; set; }
    }
}
