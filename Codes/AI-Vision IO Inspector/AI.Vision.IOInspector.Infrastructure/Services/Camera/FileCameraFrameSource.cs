using System;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 지정된 이미지 파일을 카메라 프레임처럼 복사하는 테스트 소스입니다.
    /// 현장 카메라 연결 전 실제 샘플 이미지를 반복 입력해 AI/측정 흐름을 검증할 때 사용합니다.
    /// </summary>
    public class FileCameraFrameSource : ICameraFrameSource
    {
        public CapturedImage Capture(CameraChannelConfig channel, Part part, string outputFilePath)
        {
            if (string.IsNullOrWhiteSpace(channel.SnapshotFilePath) || !File.Exists(channel.SnapshotFilePath))
            {
                throw new InvalidOperationException(channel.DisplayName + " 샘플 이미지 파일을 찾을 수 없습니다.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath));
            File.Copy(channel.SnapshotFilePath, outputFilePath, true);

            CapturedImage image = new CapturedImage();
            image.ViewType = channel.ViewType;
            image.DisplayName = channel.DisplayName;
            image.FilePath = outputFilePath;
            image.CapturedAt = DateTime.Now;
            return image;
        }
    }
}
