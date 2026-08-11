using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// VLAD detectData에서 추출한 1개 검출 결과입니다.
    /// 기본 VLAD 메시지는 class, score, x, y, width, height 중심이므로 치수 변환은 별도 보정 단계에서 처리합니다.
    /// </summary>
    public class VladDetection
    {
        public ImageViewType ViewType { get; set; }

        public int ClassId { get; set; }

        public string ClassName { get; set; }

        public decimal Score { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public string SourceImagePath { get; set; }
    }
}
