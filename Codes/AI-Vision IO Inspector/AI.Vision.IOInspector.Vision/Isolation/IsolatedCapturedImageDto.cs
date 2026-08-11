using System;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Isolation
{
    /// <summary>
    /// 외부 추론 프로세스로 전달할 촬영 이미지 정보입니다.
    /// </summary>
    public class IsolatedCapturedImageDto
    {
        public ImageViewType ViewType { get; set; }

        public string DisplayName { get; set; }

        public string FilePath { get; set; }

        public DateTime CapturedAt { get; set; }

        public static IsolatedCapturedImageDto FromCapturedImage(CapturedImage source)
        {
            IsolatedCapturedImageDto dto = new IsolatedCapturedImageDto();
            if (source == null)
            {
                return dto;
            }

            dto.ViewType = source.ViewType;
            dto.DisplayName = source.DisplayName;
            dto.FilePath = source.FilePath;
            dto.CapturedAt = source.CapturedAt;
            return dto;
        }

        public CapturedImage ToCapturedImage()
        {
            CapturedImage image = new CapturedImage();
            image.ViewType = ViewType;
            image.DisplayName = DisplayName;
            image.FilePath = FilePath;
            image.CapturedAt = CapturedAt;
            return image;
        }
    }
}
