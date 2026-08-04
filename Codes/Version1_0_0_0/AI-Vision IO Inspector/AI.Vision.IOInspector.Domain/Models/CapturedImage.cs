using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 카메라 촬영 결과입니다. 현재는 시뮬레이션 데이터를 사용하고, 추후 SDK 결과로 교체합니다.
    /// </summary>
    public class CapturedImage
    {
        public ImageViewType ViewType { get; set; }

        public string DisplayName { get; set; }

        public string FilePath { get; set; }

        public DateTime CapturedAt { get; set; }
    }
}
