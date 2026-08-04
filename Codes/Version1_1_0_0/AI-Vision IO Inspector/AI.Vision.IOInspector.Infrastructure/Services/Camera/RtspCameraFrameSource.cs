using System;
using System.Diagnostics;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// RTSP 스트림에서 현재 프레임 1장을 파일로 캡처합니다.
    /// 저장 버튼에서는 최신 프레임이 중요하므로 ffmpeg.exe를 우선 사용하고, LibVLCSharp/OpenCvSharp은 대체 경로로 사용합니다.
    /// </summary>
    public class RtspCameraFrameSource : ICameraFrameSource
    {
        private const int CaptureTimeoutMilliseconds = 10000;

        private readonly VlcRtspFrameGrabber _vlcGrabber;
        private readonly OpenCvSharpRtspFrameGrabber _openCvSharpGrabber;
        private readonly FfmpegToolLocator _ffmpegToolLocator;

        public RtspCameraFrameSource(string rootPath)
        {
            _vlcGrabber = new VlcRtspFrameGrabber(rootPath);
            _openCvSharpGrabber = new OpenCvSharpRtspFrameGrabber(rootPath);
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
            Exception ffmpegFailure = TryCaptureWithFfmpeg(channel, rtspUrl, outputFilePath, ffmpegPath);
            if (ffmpegFailure == null && HasCapturedFile(outputFilePath))
            {
                return BuildCapturedImage(channel, outputFilePath);
            }

            Exception vlcFailure = TryCaptureWithVlc(channel, rtspUrl, outputFilePath);
            if (vlcFailure == null && HasCapturedFile(outputFilePath))
            {
                return BuildCapturedImage(channel, outputFilePath);
            }

            Exception openCvFailure = TryCaptureWithOpenCvSharp(channel, rtspUrl, outputFilePath);
            if (openCvFailure == null && HasCapturedFile(outputFilePath))
            {
                return BuildCapturedImage(channel, outputFilePath);
            }

            throw BuildCaptureFailureException(channel, ffmpegFailure, vlcFailure, openCvFailure);
        }

        private Exception TryCaptureWithFfmpeg(CameraChannelConfig channel, string rtspUrl, string outputFilePath, string ffmpegPath)
        {
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                return new FileNotFoundException(_ffmpegToolLocator.BuildMissingRuntimeMessage());
            }

            try
            {
                DeleteOutputFileIfExists(outputFilePath);
                CaptureWithFfmpeg(channel, rtspUrl, outputFilePath, ffmpegPath);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private Exception TryCaptureWithVlc(CameraChannelConfig channel, string rtspUrl, string outputFilePath)
        {
            if (!_vlcGrabber.IsAvailable())
            {
                return new FileNotFoundException(_vlcGrabber.BuildMissingRuntimeMessage());
            }

            try
            {
                DeleteOutputFileIfExists(outputFilePath);
                _vlcGrabber.CaptureFrame(rtspUrl, outputFilePath, channel.DisplayName);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private Exception TryCaptureWithOpenCvSharp(CameraChannelConfig channel, string rtspUrl, string outputFilePath)
        {
            if (!_openCvSharpGrabber.IsAvailable())
            {
                return new FileNotFoundException(_openCvSharpGrabber.BuildMissingRuntimeMessage());
            }

            try
            {
                DeleteOutputFileIfExists(outputFilePath);
                _openCvSharpGrabber.CaptureFrame(rtspUrl, outputFilePath, channel.DisplayName);
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private void CaptureWithFfmpeg(CameraChannelConfig channel, string rtspUrl, string outputFilePath, string ffmpegPath)
        {
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
        }

        private CapturedImage BuildCapturedImage(CameraChannelConfig channel, string outputFilePath)
        {
            CapturedImage image = new CapturedImage();
            image.ViewType = channel.ViewType;
            image.DisplayName = channel.DisplayName;
            image.FilePath = outputFilePath;
            image.CapturedAt = DateTime.Now;
            return image;
        }

        private bool HasCapturedFile(string outputFilePath)
        {
            if (!File.Exists(outputFilePath))
            {
                return false;
            }

            FileInfo fileInfo = new FileInfo(outputFilePath);
            return fileInfo.Length > 0;
        }

        private void DeleteOutputFileIfExists(string outputFilePath)
        {
            if (!File.Exists(outputFilePath))
            {
                return;
            }

            File.Delete(outputFilePath);
        }

        private Exception BuildCaptureFailureException(CameraChannelConfig channel, Exception ffmpegFailure, Exception vlcFailure, Exception openCvFailure)
        {
            string ffmpegMessage = ffmpegFailure == null ? _ffmpegToolLocator.BuildMissingRuntimeMessage() : ffmpegFailure.Message;
            string vlcMessage = vlcFailure == null ? _vlcGrabber.BuildMissingRuntimeMessage() : vlcFailure.Message;
            string openCvMessage = openCvFailure == null ? _openCvSharpGrabber.BuildMissingRuntimeMessage() : openCvFailure.Message;
            string message = channel.DisplayName + " RTSP 프레임 캡처 실패. "
                             + "LibVLC 확인: " + vlcMessage
                             + " / OpenCvSharp 확인: " + openCvMessage
                             + " / ffmpeg 확인: " + ffmpegMessage;
            return new InvalidOperationException(message);
        }

        private string BuildArguments(string rtspUrl, string outputFilePath)
        {
            return "-y -v error -rtsp_transport tcp -fflags nobuffer -flags low_delay -analyzeduration 0 -probesize 32768 -timeout 3000000 -i " + Quote(rtspUrl) + " -frames:v 1 -q:v 2 " + Quote(outputFilePath);
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
