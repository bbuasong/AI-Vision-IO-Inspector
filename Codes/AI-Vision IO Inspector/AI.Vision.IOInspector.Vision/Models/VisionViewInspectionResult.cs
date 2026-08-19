using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// Vision 계층에서 방향 하나의 검사 결과를 담습니다.
    /// 엔진이 합치기 전의 값을 그대로 실어 상위로 올리기 위한 그릇입니다.
    /// </summary>
    public class VisionViewInspectionResult
    {
        public ImageViewType ViewType { get; set; }

        public bool IsPass { get; set; }

        public decimal Score { get; set; }

        public bool HasScore { get; set; }

        public decimal? DimensionWidth { get; set; }

        public decimal? DimensionDepth { get; set; }

        public decimal? DimensionHeight { get; set; }

        public string DimensionUnit { get; set; }
    }
}
