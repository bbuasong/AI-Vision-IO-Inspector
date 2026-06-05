using System;
using System.Diagnostics;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// RTSP 스트림에서 현재 프레임 1장을 파일로 캡처합니다.
    /// 현재 단계에서는 연속 디코딩 UI가 아니라, 프로그램이 실제 RTSP 영상을 받을 수 있는지 검증하는 캡처 경로입니다.
    /// </summary>
    public class RtspCameraFrameSource : ICameraFrameSource
    {
        private const int CaptureTimeoutMilliseconds = 10000;

        private readonly FfmpegToolLocator _ffmpegToolLocator;

        public RtspCameraFrameSource(string rootPath)
        {
            _ffmpegToolLocator = new FfmpegToolLocator(rootPath);
        }

        public CapturedImage Capture(CameraChannelConfig channel, Part part, string outputFilePath)
        {
            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            string rtspUrl = RtspUrlBuilder.Build(channel);
            if (string.IsNullOrWhiteSpace(rtspUrl))
            {
                throw new InvalidOperationException(channel.DisplayName + " RTSP URL 또는 IP/Port 설정이 없습니다.");
            }

            string ffmpegPath = _ffmpegToolLocator.FindFfmpegPath();
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                throw new FileNotFoundException("RTSP 캡처에 필요한 ffmpeg.exe를 찾을 수 없습니다. Native\\FFmpeg\\ffmpeg.exe에 배치하세요.");
            }

            string outputDirectory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string arguments = BuildArguments(rtspUrl, outputFilePath);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = ffmpegPath;
            startInfo.Arguments = arguments;
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("ffmpeg 프로세스를 시작하지 못했습니다.");
                }

                bool exited = process.WaitForExit(CaptureTimeoutMilliseconds);
                string errorText = process.StandardError.ReadToEnd();

                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    throw new TimeoutException(channel.DisplayName + " RTSP 프레임 캡처 시간이 초과되었습니다.");
                }

                if (process.ExitCode != 0 || !File.Exists(outputFilePath))
                {
                    throw new InvalidOperationException(channel.DisplayName + " RTSP 프레임 캡처 실패: " + TrimMessage(errorText));
                }
            }

            CapturedImage image = new CapturedImage();
            image.ViewType = channel.ViewType;
            image.DisplayName = channel.DisplayName;
            image.FilePath = outputFilePath;
            image.CapturedAt = DateTime.Now;
            return image;
        }

        private string BuildArguments(string rtspUrl, string outputFilePath)
        {
            return "-y -v error -rtsp_transport tcp -timeout 3000000 -i " + Quote(rtspUrl) + " -frames:v 1 -q:v 2 " + Quote(outputFilePath);
        }

        private string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private string TrimMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "상세 오류 없음";
            }

            string compact = message.Replace("\r", " ").Replace("\n", " ").Trim();
            if (compact.Length > 500)
            {
                return compact.Substring(0, 500);
            }

            return compact;
        }
    }
}
