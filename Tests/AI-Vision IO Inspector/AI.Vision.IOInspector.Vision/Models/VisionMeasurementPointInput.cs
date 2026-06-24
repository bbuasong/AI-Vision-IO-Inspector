using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// UI/DB 측정부 정보를 Vision 계층과 VLAD 연동 어댑터까지 전달하는 입력 계약입니다.
    /// 실제 네이티브 함수 인수 연결은 VLAD SDK의 측정부 메타데이터 API가 확인된 뒤 이 계약을 사용합니다.
    /// </summary>
    public class VisionMeasurementPointInput
    {
        public int IndexNo { get; set; }

        public string ItemType { get; set; }

        public ImageViewType ViewType { get; set; }

        public string LineColor { get; set; }

        public decimal NominalValue { get; set; }

        public decimal Tolerance { get; set; }

        public double? X1 { get; set; }

        public double? Y1 { get; set; }

        public double? X2 { get; set; }

        public double? Y2 { get; set; }

        public string Unit { get; set; }
    }
}
