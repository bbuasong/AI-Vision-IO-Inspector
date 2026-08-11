using System;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 실제 카메라가 연결되기 전 사용하는 파일 기반 시뮬레이션 소스입니다.
    /// CaptureAll 실행 시 각 방향별 BMP 파일을 생성하므로 UI 미리보기와 기준 이미지 저장 기능을 실제 파일 흐름으로 검증할 수 있습니다.
    /// </summary>
    public class SimulatedCameraFrameSource : ICameraFrameSource
    {
        public CapturedImage Capture(CameraChannelConfig channel, Part part, string outputFilePath)
        {
            SimpleBitmapWriter.WriteGradient(outputFilePath, ResolvePreviewWidth(channel.Width), ResolvePreviewHeight(channel.Height), (int)channel.ViewType + 1);

            CapturedImage image = new CapturedImage();
            image.ViewType = channel.ViewType;
            image.DisplayName = channel.DisplayName;
            image.FilePath = outputFilePath;
            image.CapturedAt = DateTime.Now;
            return image;
        }

        private int ResolvePreviewWidth(int configuredWidth)
        {
            if (configuredWidth <= 0)
            {
                return 640;
            }

            return Math.Min(configuredWidth, 960);
        }

        private int ResolvePreviewHeight(int configuredHeight)
        {
            if (configuredHeight <= 0)
            {
                return 480;
            }

            return Math.Min(configuredHeight, 720);
        }
    }
}
