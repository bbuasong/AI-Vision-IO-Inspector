using System;
using System.Globalization;
using System.IO;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// RTSP 검사 캡처의 시도별 결과를 DB\Logs\{일자}\rtsp-capture-*.log 에 남깁니다.
    ///
    /// 캡처는 검사할 때마다 RTSP 연결을 새로 열고 첫 키프레임을 기다립니다.
    /// 접속 시점이 키프레임 주기(GOP)의 어디에 걸리느냐에 따라 소요 시간이 달라지고,
    /// 타임아웃보다 길어지면 실패합니다. 그래서 간헐적으로만 실패합니다.
    ///
    /// 이 로그의 목적은 두 가지입니다.
    ///   1. 실패했을 때 어느 단계(ffmpeg / VLC / OpenCV)에서 왜 떨어졌는지 남긴다.
    ///      기존에는 ffmpeg의 오류 출력이 예외 메시지에만 담겨, 재시도로 성공하면 그대로 사라졌습니다.
    ///   2. 성공한 캡처의 소요 시간을 남긴다.
    ///      성공 시간의 최댓값이 대략 그 채널의 키프레임 주기이므로,
    ///      별도 측정 없이 운영 로그만으로 타임아웃을 얼마나 잡아야 하는지 판단할 수 있습니다.
    /// </summary>
    public static class RtspCaptureLog
    {
        private const string LogName = "rtsp-capture";
        private static readonly object SyncRoot = new object();

        /// <summary>
        /// 캡처 시도 한 건을 남깁니다.
        /// </summary>
        /// <param name="applicationRootPath">로그 폴더를 찾을 기준 경로입니다.</param>
        /// <param name="cameraName">채널 표시 이름입니다.</param>
        /// <param name="attemptNumber">몇 번째 시도인지입니다.</param>
        /// <param name="method">사용한 방식입니다. ffmpeg, LibVLC, OpenCvSharp</param>
        /// <param name="elapsedMilliseconds">그 방식에 걸린 시간입니다.</param>
        /// <param name="isSuccess">캡처에 성공했는지입니다.</param>
        /// <param name="detail">실패 사유나 참고 내용입니다. ffmpeg 오류 원문이 여기 들어갑니다.</param>
        public static void WriteAttempt(
            string applicationRootPath,
            string cameraName,
            int attemptNumber,
            string method,
            long elapsedMilliseconds,
            bool isSuccess,
            string detail)
        {
            string status = isSuccess ? "SUCCESS" : "FAILED";
            string message =
                "Camera=" + NormalizeText(cameraName) +
                " Attempt=" + attemptNumber.ToString(CultureInfo.InvariantCulture) +
                " Method=" + NormalizeText(method) +
                " Elapsed=" + elapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms";

            if (!string.IsNullOrWhiteSpace(detail))
            {
                message = message + " Detail=" + NormalizeText(detail);
            }

            Write(applicationRootPath, status, message);
        }

        /// <summary>
        /// 모든 방식과 재시도가 끝난 뒤 채널 하나의 최종 결과를 남깁니다.
        /// </summary>
        public static void WriteResult(
            string applicationRootPath,
            string cameraName,
            bool isSuccess,
            long totalElapsedMilliseconds,
            string detail)
        {
            string status = isSuccess ? "RESULT-OK" : "RESULT-FAIL";
            string message =
                "Camera=" + NormalizeText(cameraName) +
                " TotalElapsed=" + totalElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms";

            if (!string.IsNullOrWhiteSpace(detail))
            {
                message = message + " Detail=" + NormalizeText(detail);
            }

            Write(applicationRootPath, status, message);
        }

        /// <summary>
        /// 원본 해상도 캡처가 실패해 VLAD callback 캐시(1920x1080)로 내려간 사실을 남깁니다.
        ///
        /// 이 전환은 검사 결과 이미지의 해상도를 바꾸므로 측정에 영향이 갈 수 있는데,
        /// 기존에는 Debug.WriteLine 으로만 남아 Release 빌드에서는 아무 흔적이 없었습니다.
        /// </summary>
        public static void WriteFallbackToCallback(
            string applicationRootPath,
            string cameraName,
            int configuredWidth,
            int configuredHeight,
            string reason)
        {
            string message =
                "Camera=" + NormalizeText(cameraName) +
                " ConfiguredResolution=" +
                configuredWidth.ToString(CultureInfo.InvariantCulture) + "x" +
                configuredHeight.ToString(CultureInfo.InvariantCulture) +
                " 원본 해상도 캡처에 실패해 VLAD callback 캐시로 전환합니다." +
                " 이번 검사 이미지는 callback 버퍼 해상도로 저장됩니다.";

            if (!string.IsNullOrWhiteSpace(reason))
            {
                message = message + " Detail=" + NormalizeText(reason);
            }

            Write(applicationRootPath, "FALLBACK", message);
        }

        /// <summary>
        /// 상시 연결(ffmpeg 지속 실행) 관련 사건을 남깁니다.
        /// 시작, 종료, 재기동, 최신 프레임 사용 여부를 이 로그로 추적합니다.
        /// </summary>
        public static void WritePersistent(
            string applicationRootPath,
            string cameraName,
            string status,
            string detail)
        {
            Write(
                applicationRootPath,
                "PERSIST-" + NormalizeText(status),
                "Camera=" + NormalizeText(cameraName) + " " + NormalizeText(detail));
        }

        private static void Write(string applicationRootPath, string status, string message)
        {
            try
            {
                string logFilePath = ApplicationLogFileResolver.GetLogFilePath(applicationRootPath, LogName);
                string line =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    " [" + status + "] " +
                    message +
                    Environment.NewLine;

                // 6채널이 동시에 캡처되므로 파일 쓰기를 직렬화합니다.
                lock (SyncRoot)
                {
                    File.AppendAllText(logFilePath, line);
                }
            }
            catch (Exception oEx)
            {
                // 로그 기록 실패가 검사를 막으면 안 됩니다. 원인만 개발 중 확인할 수 있게 남깁니다.
                System.Diagnostics.Debug.WriteLine("RTSP 캡처 로그 기록 실패: " + oEx.Message);
            }
        }

        /// <summary>
        /// 로그 한 줄이 여러 줄로 깨지지 않도록 줄바꿈을 공백으로 바꾸고 길이를 제한합니다.
        /// </summary>
        private static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            string compact = value.Replace("\r", " ").Replace("\n", " ").Trim();
            if (compact.Length > 500)
            {
                compact = compact.Substring(0, 500);
            }

            return compact;
        }
    }
}
