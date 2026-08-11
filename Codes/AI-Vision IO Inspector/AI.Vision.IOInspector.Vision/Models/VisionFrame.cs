using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Vision.Models
{
    /// <summary>
    /// AI, 보정, 이력 저장에 필요한 메타데이터를 포함한 카메라 프레임 1장입니다.
    /// 실제 IMV/RTSP 구현체는 너비, 높이, stride, 픽셀 포맷, 촬영 시각을 반드시 채워야 합니다.
    /// </summary>
    public class VisionFrame
    {
        public string CameraId { get; set; }

        public ImageViewType ViewType { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Stride { get; set; }

        public string PixelFormat { get; set; }

        public long FrameId { get; set; }

        public DateTime CapturedAt { get; set; }

        public byte[] Buffer { get; set; }

        public string FilePath { get; set; }
    }
}
