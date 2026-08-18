using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// RTSP 스트림에서 현재 프레임 1장을 파일로 캡처합니다.
    /// 저장 버튼에서는 최신 프레임이 중요하므로 ffmpeg.exe를 우선 사용하고, LibVLCSharp/OpenCvSharp을 대체 경로로 사용합니다.
    ///
    /// 상시 연결(PersistentCaptureRegistry) 대상 채널은 여기까지 오지 않고 최신 프레임 복사로 끝납니다.
    /// 이 경로는 상시 연결을 쓰지 않는 채널과, 상시 연결 프레임을 아직 못 쓰는 경우의 보조 수단입니다.
    ///
    /// OpenCvSharp은 세 번째 대체 경로입니다. 현장 로그 13회차 누적에서는 55회 시도가 모두 실패했는데,
    /// 배포된 OpenCvSharp.dll이 2018년 빌드의 .NET Framework 전용(System.Web 참조) 어셈블리라
    /// 런타임에서 로드되지 않은 것이 원인입니다. 경로 자체의 결함이 아니라 배포된 DLL의 문제이므로,
    /// DLL을 최신 빌드로 교체하면 다시 동작합니다. 앞의 두 경로가 모두 실패한 뒤에만 호출되므로
    /// 평상시 비용은 없습니다.
    /// </summary>
    public class RtspCameraFrameSource : ICameraFrameSource
    {
        // ffmpeg 캡처의 최대 대기 시간입니다.
        //
        // 현장 로그 13회차에서 성공한 ffmpeg 캡처 498건의 소요 시간은
        //   최소 431ms / 중앙값 990ms / 99% 1,532ms / 최대 1,637ms
        // 였습니다. 1.7초를 넘겨 성공한 사례가 한 건도 없으므로, 그 이상 기다려도 얻는 것이 없습니다.
        // 반면 실패는 타임아웃 10초를 그대로 소비해 채널당 10.3초, 6채널이면 1분 가까이 걸렸습니다.
        //
        // 3초는 관측된 최대값의 약 1.8배로, 성공 사례를 자르지 않으면서 실패를 빨리 포기합니다.
        // 상시 연결(PersistentCaptureRegistry)을 쓰는 채널은 애초에 이 경로로 오지 않습니다.
        private const int CaptureTimeoutMilliseconds = 3000;

        private readonly VlcRtspFrameGrabber _vlcGrabber;
        private readonly OpenCvSharpRtspFrameGrabber _openCvSharpGrabber;
        private readonly FfmpegToolLocator _ffmpegToolLocator;
        private readonly string _rootPath;
        private PersistentCaptureRegistry _persistentRegistry;

        public RtspCameraFrameSource(string rootPath)
        {
            _rootPath = rootPath;
            _vlcGrabber = new VlcRtspFrameGrabber(rootPath);
            _openCvSharpGrabber = new OpenCvSharpRtspFrameGrabber(rootPath);
            _ffmpegToolLocator = new FfmpegToolLocator(rootPath);
        }

        /// <summary>
        /// 상시 연결 레지스트리를 연결합니다. 설정된 채널은 새 연결 대신 최신 프레임을 씁니다.
        /// null이면 기존 방식으로만 동작합니다.
        /// </summary>
        public void AttachPersistentRegistry(PersistentCaptureRegistry oRegistry)
        {
            _persistentRegistry = oRegistry;
        }

        // ffmpeg 캡처는 지연시간을 줄이기 위해 -analyzeduration 0 -probesize 32768처럼 스트림 분석을
        // 최소화합니다. 이 설정은 "막 새로 연" 콜드 RTSP 연결에서는 첫 키프레임을 찾기에 너무 촉박해
        // 실패하기 쉽지만, 재시도(새 연결)에서는 대체로 성공합니다(사용자가 버튼을 두 번 눌러야 첫
        // 캡처가 되던 증상과 일치). 사용자가 다시 누르지 않아도 되도록 내부에서 한 번 더 재시도합니다.
        private const int MaxCaptureAttempts = 2;

        // 재시도 사이의 대기입니다. 간격 없이 곧바로 다시 연결하면 직전과 거의 같은
        // 키프레임 위상에 걸려 같은 이유로 다시 실패합니다.
        private const int RetryDelayMilliseconds = 400;

        /// <summary>타임아웃으로 강제 종료한 뒤 프로세스가 정리되기를 기다리는 시간입니다.</summary>
        private const int ProcessKillWaitMilliseconds = 2000;

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

            // 상시 연결 대상 채널이면 새 연결을 열지 않고 최신 프레임을 복사만 합니다.
            // 연결 수립 대기가 없으므로 실패 원인 자체가 사라집니다.
            //
            // 이 시각을 기준으로 "지금 이후에 만들어진 프레임"만 인정합니다.
            // 그래야 스트림이 멈췄을 때 과거 이미지로 검사하는 일이 생기지 않습니다.
            DateTime dtRequestedAt = DateTime.Now;
            CapturedImage oPersistentImage = TryCaptureFromPersistent(channel, outputFilePath, dtRequestedAt);
            if (oPersistentImage != null)
            {
                return oPersistentImage;
            }

            string ffmpegPath = _ffmpegToolLocator.FindFfmpegPath();
            Exception ffmpegFailure = null;
            Exception vlcFailure = null;
            Exception openCvFailure = null;

            // 시도별 결과와 소요 시간을 남깁니다. 성공한 캡처의 소요 시간을 모으면
            // 그 채널의 키프레임 주기(GOP)를 역산할 수 있어 타임아웃 설정 근거가 됩니다.
            Stopwatch totalWatch = Stopwatch.StartNew();

            for (int attempt = 1; attempt <= MaxCaptureAttempts; attempt++)
            {
                // 재시도 사이에 잠깐 쉽니다. 간격 없이 바로 다시 붙으면 직전과 거의 같은
                // 키프레임 위상에 걸려 같은 이유로 다시 실패하기 쉽습니다.
                if (attempt > 1)
                {
                    Thread.Sleep(RetryDelayMilliseconds);
                }

                Stopwatch methodWatch = Stopwatch.StartNew();
                ffmpegFailure = TryCaptureWithFfmpeg(channel, rtspUrl, outputFilePath, ffmpegPath);
                methodWatch.Stop();
                bool bFfmpegCaptured = ffmpegFailure == null && HasCapturedFile(outputFilePath);
                WriteAttemptLog(channel, attempt, "ffmpeg", methodWatch.ElapsedMilliseconds, bFfmpegCaptured, ffmpegFailure);
                if (bFfmpegCaptured)
                {
                    totalWatch.Stop();
                    WriteResultLog(channel, true, totalWatch.ElapsedMilliseconds, "ffmpeg 성공");
                    return BuildCapturedImage(channel, outputFilePath);
                }

                methodWatch = Stopwatch.StartNew();
                vlcFailure = TryCaptureWithVlc(channel, rtspUrl, outputFilePath);
                methodWatch.Stop();
                bool bVlcCaptured = vlcFailure == null && HasCapturedFile(outputFilePath);
                WriteAttemptLog(channel, attempt, "LibVLC", methodWatch.ElapsedMilliseconds, bVlcCaptured, vlcFailure);
                if (bVlcCaptured)
                {
                    totalWatch.Stop();
                    WriteResultLog(channel, true, totalWatch.ElapsedMilliseconds, "LibVLC 성공 (ffmpeg 실패 후 대체)");
                    return BuildCapturedImage(channel, outputFilePath);
                }

                methodWatch = Stopwatch.StartNew();
                openCvFailure = TryCaptureWithOpenCvSharp(channel, rtspUrl, outputFilePath);
                methodWatch.Stop();
                bool bOpenCvCaptured = openCvFailure == null && HasCapturedFile(outputFilePath);
                WriteAttemptLog(channel, attempt, "OpenCvSharp", methodWatch.ElapsedMilliseconds, bOpenCvCaptured, openCvFailure);
                if (bOpenCvCaptured)
                {
                    totalWatch.Stop();
                    WriteResultLog(channel, true, totalWatch.ElapsedMilliseconds, "OpenCvSharp 성공 (ffmpeg/LibVLC 실패 후 대체)");
                    return BuildCapturedImage(channel, outputFilePath);
                }
            }

            totalWatch.Stop();
            WriteResultLog(channel, false, totalWatch.ElapsedMilliseconds, "모든 방식과 재시도가 실패했습니다.");
            throw BuildCaptureFailureException(channel, ffmpegFailure, vlcFailure, openCvFailure);
        }

        /// <summary>
        /// 상시 연결이 보관 중인 최신 프레임으로 캡처를 끝냅니다.
        /// 대상이 아니거나, 요청 시각 이후의 새 프레임이 오지 않으면 null을 돌려주고,
        /// 호출자는 기존 방식으로 넘어갑니다.
        /// </summary>
        private CapturedImage TryCaptureFromPersistent(
            CameraChannelConfig channel,
            string outputFilePath,
            DateTime dtRequestedAt)
        {
            if (_persistentRegistry == null || channel == null)
            {
                return null;
            }

            if (!_persistentRegistry.IsPersistentChannel(channel.ViewType))
            {
                return null;
            }

            Stopwatch oWatch = Stopwatch.StartNew();
            DateTime dtFrameCapturedAt;
            string sMessage;
            bool bGrabbed = _persistentRegistry.TryGrabLatest(
                channel.ViewType,
                outputFilePath,
                dtRequestedAt,
                out dtFrameCapturedAt,
                out sMessage);
            oWatch.Stop();

            RtspCaptureLog.WriteAttempt(
                _rootPath,
                channel.DisplayName,
                1,
                "Persistent",
                oWatch.ElapsedMilliseconds,
                bGrabbed,
                bGrabbed
                    ? "최신 프레임 시각=" + dtFrameCapturedAt.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    : sMessage);

            if (!bGrabbed || !HasCapturedFile(outputFilePath))
            {
                // 최신 프레임을 못 쓰면 기존 방식으로 넘어갑니다. 검사를 막지 않습니다.
                RtspCaptureLog.WritePersistent(
                    _rootPath,
                    channel.DisplayName,
                    "FALLBACK",
                    "상시 연결 최신 프레임을 쓰지 못해 기존 캡처 방식으로 넘어갑니다. " + sMessage);
                return null;
            }

            RtspCaptureLog.WriteResult(
                _rootPath,
                channel.DisplayName,
                true,
                oWatch.ElapsedMilliseconds,
                "상시 연결 최신 프레임 사용");

            return BuildCapturedImage(channel, outputFilePath);
        }

        private void WriteAttemptLog(
            CameraChannelConfig channel,
            int attemptNumber,
            string method,
            long elapsedMilliseconds,
            bool isSuccess,
            Exception failure)
        {
            string detail = failure == null ? string.Empty : TrimMessage(failure.Message);
            RtspCaptureLog.WriteAttempt(
                _rootPath,
                channel == null ? string.Empty : channel.DisplayName,
                attemptNumber,
                method,
                elapsedMilliseconds,
                isSuccess,
                detail);
        }

        private void WriteResultLog(
            CameraChannelConfig channel,
            bool isSuccess,
            long totalElapsedMilliseconds,
            string detail)
        {
            RtspCaptureLog.WriteResult(
                _rootPath,
                channel == null ? string.Empty : channel.DisplayName,
                isSuccess,
                totalElapsedMilliseconds,
                detail);
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
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("ffmpeg 프로세스를 시작하지 못했습니다.");
                }

                bool exited = process.WaitForExit(CaptureTimeoutMilliseconds);

                // 프로세스가 아직 살아 있는데 StandardError.ReadToEnd()를 먼저 부르면
                // ffmpeg가 스스로 끝날 때까지 무한정 막힙니다. 타임아웃을 둔 의미가 사라지므로
                // 먼저 종료시키고, 그다음에 남은 출력을 읽습니다.
                if (!exited)
                {
                    try
                    {
                        process.Kill();
                        process.WaitForExit(ProcessKillWaitMilliseconds);
                    }
                    catch (Exception)
                    {
                        // 이미 끝난 프로세스면 무시합니다.
                    }

                    throw new TimeoutException(channel.DisplayName + " RTSP 프레임 캡처 시간이 초과되었습니다.");
                }

                string errorText = process.StandardError.ReadToEnd();

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

        private Exception BuildCaptureFailureException(CameraChannelConfig channel, Exception ffmpegFailure, Exception vlcFailure, Exception openCvFailure)
        {
            string ffmpegMessage = ffmpegFailure == null ? _ffmpegToolLocator.BuildMissingRuntimeMessage() : ffmpegFailure.Message;
            string vlcMessage = vlcFailure == null ? _vlcGrabber.BuildMissingRuntimeMessage() : vlcFailure.Message;
            string openCvMessage = openCvFailure == null ? _openCvSharpGrabber.BuildMissingRuntimeMessage() : openCvFailure.Message;
            string message = channel.DisplayName + " RTSP 프레임 캡처 실패. "
                             + "LibVLC 확인: " + vlcMessage
                             + " / ffmpeg 확인: " + ffmpegMessage
                             + " / OpenCvSharp 확인: " + openCvMessage;
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
