using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 카메라 채널 하나에 대해 ffmpeg를 <b>상시 실행</b>해 최신 프레임 1장을 계속 갱신합니다.
    ///
    /// 기존 방식은 검사할 때마다 RTSP 연결을 새로 열고 첫 키프레임을 기다렸습니다.
    /// 그 대기가 타임아웃(10초)을 넘기면 실패했고, 현장 로그 13회차 누적에서 ffmpeg 실패율이
    /// Top 24%, Thickness 17.7%로 나왔습니다. 성공은 0.4~1.4초, 실패는 10.3초로
    /// 중간값이 거의 없어 "연결 수립이 되느냐 마느냐"의 문제였습니다.
    ///
    /// 연결을 한 번만 열고 계속 유지하면 그 대기 자체가 사라집니다.
    /// 화면(LibVLC)과 VLAD callback은 이미 상시 연결로 동작하고 있어, 캡처만 예외였습니다.
    ///
    /// 동작
    ///   Open()   ffmpeg를 -update 1로 띄워 지정한 파일 하나를 계속 덮어쓰게 합니다.
    ///   Grab()   그 파일을 검사용 경로로 복사만 합니다. RTSP 연결을 새로 열지 않습니다.
    ///   Close()  프로세스를 종료합니다.
    ///
    /// 프로세스가 죽으면 감시 스레드가 다시 띄웁니다. 카메라가 빠졌다 들어오는 상황을 견디기 위함입니다.
    /// </summary>
    public class PersistentRtspFrameGrabber : IDisposable
    {
        /// <summary>최신 프레임을 몇 초에 한 장 갱신할지입니다. 검사에는 1초면 충분합니다.</summary>
        private const int FramesPerSecondDivisor = 1;

        /// <summary>프로세스 상태를 확인하는 주기입니다.</summary>
        private const int MonitorIntervalMilliseconds = 2000;

        /// <summary>재기동을 너무 자주 시도하지 않도록 두는 최소 간격입니다.</summary>
        private const int RestartDelayMilliseconds = 3000;

        /// <summary>파일 복사가 실패했을 때 다시 시도하는 횟수입니다. 쓰기 도중과 겹친 경우를 위한 것입니다.</summary>
        private const int CopyRetryCount = 3;

        private const int CopyRetryDelayMilliseconds = 150;

        /// <summary>
        /// 검사 요청 시각 이후의 새 프레임을 기다리는 최대 시간입니다.
        ///
        /// 초당 1장 갱신이므로 정상이면 1초 안에 새 프레임이 나옵니다.
        /// 여기서 시간을 다 쓰면 스트림이 멈춘 것이므로 기존 방식(새 연결 캡처)으로 넘깁니다.
        /// </summary>
        private const int FreshFrameWaitMilliseconds = 2000;

        /// <summary>새 프레임이 나왔는지 확인하는 간격입니다.</summary>
        private const int FreshFrameCheckIntervalMilliseconds = 100;

        /// <summary>
        /// 파일 기록 시각과 요청 시각을 비교할 때 두는 여유입니다.
        /// 파일 시스템의 시각 기록과 프로그램의 시각이 미세하게 어긋나는 경우를 흡수합니다.
        /// </summary>
        private const int RequestTimeGraceMilliseconds = 200;

        /// <summary>
        /// 프레임이 이 시간 동안 갱신되지 않으면 스트림이 멈춘 것으로 보고 프로세스를 다시 띄웁니다.
        ///
        /// ffmpeg는 RTSP 연결이 끊겨도 프로세스가 살아 있는 채로 멈춰 있는 경우가 많습니다.
        /// 프로세스 생존(HasExited)만 확인하면 이 상태를 영원히 알아채지 못합니다.
        /// </summary>
        private const int StallTimeoutMilliseconds = 10000;

        /// <summary>RTSP 소켓 I/O가 응답하지 않을 때 ffmpeg가 포기하는 시간입니다. 마이크로초 단위입니다.</summary>
        private const long SocketTimeoutMicroseconds = 5000000;

        private readonly object m_oSyncRoot = new object();
        private readonly string m_sCameraName;
        private readonly string m_sRtspUrl;
        private readonly string m_sFfmpegPath;
        private readonly string m_sLatestFramePath;
        private readonly string m_sRootPath;

        private Process m_oProcess = null;
        private Thread m_oMonitorThread = null;
        private volatile bool m_bRun = false;
        private DateTime m_dtLastStartedAt = DateTime.MinValue;
        private int m_nRestartCount = 0;
        private string m_sLastProcessError = string.Empty;
        private readonly object m_oErrorSyncRoot = new object();
        private readonly Queue<string> m_oRecentErrors = new Queue<string>();
        private const int RecentErrorLineCount = 4;

        public PersistentRtspFrameGrabber(
            string sRootPath,
            string sCameraName,
            string sRtspUrl,
            string sFfmpegPath,
            string sLatestFramePath)
        {
            m_sRootPath = sRootPath;
            m_sCameraName = sCameraName;
            m_sRtspUrl = sRtspUrl;
            m_sFfmpegPath = sFfmpegPath;
            m_sLatestFramePath = sLatestFramePath;
        }

        public string CameraName
        {
            get { return m_sCameraName; }
        }

        /// <summary>최신 프레임 파일 경로입니다.</summary>
        public string LatestFramePath
        {
            get { return m_sLatestFramePath; }
        }

        public bool IsRunning
        {
            get
            {
                lock (m_oSyncRoot)
                {
                    return m_oProcess != null && !m_oProcess.HasExited;
                }
            }
        }

        /// <summary>지금까지 프로세스를 다시 띄운 횟수입니다. 불안정한 채널을 찾는 데 씁니다.</summary>
        public int RestartCount
        {
            get { return m_nRestartCount; }
        }

        /// <summary>
        /// ffmpeg를 띄우고 감시 스레드를 시작합니다. 이미 열려 있으면 아무것도 하지 않습니다.
        /// </summary>
        public void Open()
        {
            lock (m_oSyncRoot)
            {
                if (m_bRun)
                {
                    return;
                }

                m_bRun = true;
            }

            StartProcess();

            m_oMonitorThread = new Thread(Thread_Monitor);
            m_oMonitorThread.IsBackground = true;
            m_oMonitorThread.Start();

            RtspCaptureLog.WritePersistent(
                m_sRootPath,
                m_sCameraName,
                "OPEN",
                "상시 연결을 시작했습니다." +
                " ffmpeg=" + m_sFfmpegPath +
                " / LatestFrame=" + m_sLatestFramePath +
                " / 인자=" + RtspUrlMasker.MaskAllInText(BuildArguments()));
        }

        /// <summary>
        /// 검사 요청 시각 이후에 만들어진 프레임을 검사용 경로로 복사합니다.
        /// RTSP 연결을 새로 열지 않습니다.
        ///
        /// <para>
        /// 파일이 있다고 해서 쓸 수 있는 것이 아닙니다. ffmpeg가 죽었거나 스트림이 끊기면
        /// 마지막 프레임이 그대로 남아 있어, 확인하지 않으면 <b>과거 이미지로 검사</b>하게 됩니다.
        /// 캡처 실패보다 나쁜 결과이므로, 검사 버튼을 누른 시점 이후에 기록된 프레임만 인정합니다.
        /// </para>
        ///
        /// <param name="dtRequestedAt">검사를 요청한 시각입니다. 이 시각 이후의 프레임만 사용합니다.</param>
        /// </summary>
        public bool TryGrabLatest(
            string sOutputFilePath,
            DateTime dtRequestedAt,
            out DateTime dtFrameCapturedAt,
            out string sMessage)
        {
            dtFrameCapturedAt = DateTime.MinValue;
            sMessage = string.Empty;

            DateTime dtThreshold = dtRequestedAt.AddMilliseconds(-RequestTimeGraceMilliseconds);
            if (!WaitForFrameNewerThan(dtThreshold, out sMessage))
            {
                RtspCaptureLog.WritePersistent(m_sRootPath, m_sCameraName, "STALE_FRAME", sMessage);
                return false;
            }

            string sDirectoryPath = Path.GetDirectoryName(sOutputFilePath);
            if (!string.IsNullOrWhiteSpace(sDirectoryPath))
            {
                Directory.CreateDirectory(sDirectoryPath);
            }

            // ffmpeg가 파일을 쓰는 중일 수 있어 몇 번 다시 시도합니다.
            // 초당 1장 갱신이라 겹칠 확률은 낮지만, 겹치면 잘린 이미지가 저장됩니다.
            for (int nAttempt = 1; nAttempt <= CopyRetryCount; nAttempt++)
            {
                try
                {
                    FileInfo oInfo = new FileInfo(m_sLatestFramePath);
                    long nLengthBefore = oInfo.Length;
                    DateTime dtWriteBefore = oInfo.LastWriteTime;
                    if (nLengthBefore == 0)
                    {
                        Thread.Sleep(CopyRetryDelayMilliseconds);
                        continue;
                    }

                    File.Copy(m_sLatestFramePath, sOutputFilePath, true);

                    // 복사 도중에 파일이 바뀌지 않았는지 확인합니다.
                    // 크기만 보면 우연히 같을 때 놓치므로 기록 시각도 함께 봅니다.
                    oInfo.Refresh();
                    if (oInfo.Length != nLengthBefore || oInfo.LastWriteTime != dtWriteBefore)
                    {
                        Thread.Sleep(CopyRetryDelayMilliseconds);
                        continue;
                    }

                    dtFrameCapturedAt = dtWriteBefore;
                    return true;
                }
                catch (IOException oEx)
                {
                    sMessage = oEx.Message;
                    Thread.Sleep(CopyRetryDelayMilliseconds);
                }
                catch (UnauthorizedAccessException oEx)
                {
                    sMessage = oEx.Message;
                    Thread.Sleep(CopyRetryDelayMilliseconds);
                }
            }

            if (string.IsNullOrWhiteSpace(sMessage))
            {
                sMessage = "최신 프레임 복사가 " + CopyRetryCount.ToString(CultureInfo.InvariantCulture) + "회 모두 실패했습니다.";
            }

            return false;
        }

        /// <summary>
        /// 지정한 시각보다 나중에 기록된 프레임이 나올 때까지 짧게 기다립니다.
        ///
        /// 초당 1장 갱신이므로 대개 1초 안에 새 프레임이 나옵니다.
        /// 이미 조건을 만족하는 프레임이 있으면 기다리지 않고 바로 돌아옵니다.
        /// </summary>
        private bool WaitForFrameNewerThan(DateTime dtThreshold, out string sMessage)
        {
            sMessage = string.Empty;

            int nWaited = 0;
            while (true)
            {
                FileInfo oInfo = new FileInfo(m_sLatestFramePath);
                if (oInfo.Exists && oInfo.Length > 0 && oInfo.LastWriteTime >= dtThreshold)
                {
                    return true;
                }

                if (nWaited >= FreshFrameWaitMilliseconds)
                {
                    if (!oInfo.Exists)
                    {
                        sMessage = "상시 연결 최신 프레임이 아직 만들어지지 않았습니다.";
                    }
                    else
                    {
                        sMessage = "검사 요청 시각 이후의 새 프레임이 " +
                                   FreshFrameWaitMilliseconds.ToString(CultureInfo.InvariantCulture) +
                                   "ms 안에 오지 않았습니다. 마지막 프레임 기록 시각=" +
                                   oInfo.LastWriteTime.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                                   ", 기준 시각=" +
                                   dtThreshold.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                    }

                    return false;
                }

                Thread.Sleep(FreshFrameCheckIntervalMilliseconds);
                nWaited += FreshFrameCheckIntervalMilliseconds;
            }
        }

        /// <summary>
        /// 중지 신호만 보내고 즉시 돌아옵니다.
        /// 여러 채널을 닫을 때 먼저 전부 신호를 보내면 대기 시간이 겹쳐 전체 종료가 빨라집니다.
        /// </summary>
        public void RequestStop()
        {
            m_bRun = false;
        }

        public void Close()
        {
            m_bRun = false;

            if (m_oMonitorThread != null)
            {
                // 타임아웃 없이 기다리지 않습니다. 감시 스레드가 멈춰 있어도 프로그램이 닫혀야 합니다.
                bool bEnded = m_oMonitorThread.Join(1500);
                if (!bEnded)
                {
                    RtspCaptureLog.WritePersistent(
                        m_sRootPath, m_sCameraName, "CLOSE", "감시 스레드가 1.5초 안에 종료되지 않았습니다.");
                }
                m_oMonitorThread = null;
            }

            StopProcess();

            RtspCaptureLog.WritePersistent(
                m_sRootPath,
                m_sCameraName,
                "CLOSE",
                "상시 연결을 종료했습니다. 재기동 횟수=" + m_nRestartCount.ToString(CultureInfo.InvariantCulture));
        }

        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// ffmpeg 프로세스가 살아 있는지 확인하고, 죽었으면 다시 띄웁니다.
        /// </summary>
        private void Thread_Monitor()
        {
            while (m_bRun)
            {
                try
                {
                    bool bNeedRestart = false;
                    string sReason = string.Empty;

                    lock (m_oSyncRoot)
                    {
                        if (m_oProcess == null || m_oProcess.HasExited)
                        {
                            bNeedRestart = true;
                            sReason = "ffmpeg 프로세스가 종료되었습니다.";
                        }
                    }

                    // 프로세스가 살아 있어도 프레임이 오지 않으면 의미가 없습니다.
                    // RTSP가 끊겼는데 ffmpeg가 멈춘 채 버티는 경우가 여기에 해당합니다.
                    if (!bNeedRestart && IsFrameStalled(out sReason))
                    {
                        bNeedRestart = true;
                        StopProcess();
                    }

                    if (bNeedRestart && m_bRun)
                    {
                        TimeSpan oElapsed = DateTime.Now - m_dtLastStartedAt;
                        if (oElapsed.TotalMilliseconds >= RestartDelayMilliseconds)
                        {
                            m_nRestartCount++;
                            RtspCaptureLog.WritePersistent(
                                m_sRootPath,
                                m_sCameraName,
                                "RESTART",
                                sReason + " 다시 시작합니다. 누적 재기동=" +
                                m_nRestartCount.ToString(CultureInfo.InvariantCulture) +
                                (string.IsNullOrWhiteSpace(m_sLastProcessError)
                                    ? string.Empty
                                    : " / ffmpeg 마지막 오류: " + m_sLastProcessError));
                            StartProcess();
                        }
                    }
                }
                catch (Exception oEx)
                {
                    // 감시 실패가 프로그램을 멈추면 안 됩니다.
                    RtspCaptureLog.WritePersistent(m_sRootPath, m_sCameraName, "MONITOR_ERROR", oEx.Message);
                }

                SleepUntilStopRequested(MonitorIntervalMilliseconds);
            }
        }

        /// <summary>
        /// 프레임 갱신이 멈췄는지 확인합니다.
        /// 프로세스를 막 띄운 직후에는 아직 첫 프레임이 없을 수 있으므로 그 시간은 봐줍니다.
        /// </summary>
        private bool IsFrameStalled(out string sReason)
        {
            sReason = string.Empty;

            double dSinceStart = (DateTime.Now - m_dtLastStartedAt).TotalMilliseconds;
            if (dSinceStart < StallTimeoutMilliseconds)
            {
                return false;
            }

            FileInfo oInfo = new FileInfo(m_sLatestFramePath);
            if (!oInfo.Exists)
            {
                sReason = "기동 후 " + ((int)dSinceStart).ToString(CultureInfo.InvariantCulture) +
                          "ms 동안 프레임이 한 장도 만들어지지 않았습니다.";
                return true;
            }

            double dAge = (DateTime.Now - oInfo.LastWriteTime).TotalMilliseconds;
            if (dAge > StallTimeoutMilliseconds)
            {
                sReason = "프로세스는 살아 있으나 프레임이 " +
                          ((int)dAge).ToString(CultureInfo.InvariantCulture) + "ms 동안 갱신되지 않았습니다.";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 중지 신호를 빠르게 알아채도록 대기를 잘게 나눕니다.
        /// 한 번에 길게 자면 Close()가 그 시간만큼 기다리게 되어 프로그램 종료가 느려집니다.
        /// </summary>
        private void SleepUntilStopRequested(int nTotalMilliseconds)
        {
            const int StepMilliseconds = 100;
            int nElapsed = 0;
            while (nElapsed < nTotalMilliseconds)
            {
                if (!m_bRun)
                {
                    return;
                }

                Thread.Sleep(StepMilliseconds);
                nElapsed += StepMilliseconds;
            }
        }

        private void StartProcess()
        {
            try
            {
                string sDirectoryPath = Path.GetDirectoryName(m_sLatestFramePath);
                if (!string.IsNullOrWhiteSpace(sDirectoryPath))
                {
                    Directory.CreateDirectory(sDirectoryPath);
                }

                ProcessStartInfo oStartInfo = new ProcessStartInfo();
                oStartInfo.FileName = m_sFfmpegPath;
                oStartInfo.Arguments = BuildArguments();
                oStartInfo.CreateNoWindow = true;
                oStartInfo.UseShellExecute = false;
                oStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                oStartInfo.RedirectStandardError = true;
                oStartInfo.RedirectStandardOutput = true;

                Process oProcess = Process.Start(oStartInfo);
                if (oProcess == null)
                {
                    RtspCaptureLog.WritePersistent(m_sRootPath, m_sCameraName, "START_FAILED", "ffmpeg 프로세스를 시작하지 못했습니다.");
                    return;
                }

                // 표준 출력/오류를 읽지 않고 두면 파이프 버퍼가 차서 ffmpeg가 멈춥니다.
                // 상시 실행이라 이 문제가 반드시 나타나므로 비동기로 계속 비웁니다.
                oProcess.ErrorDataReceived += Process_OutputReceived;
                oProcess.OutputDataReceived += Process_OutputReceived;
                oProcess.BeginErrorReadLine();
                oProcess.BeginOutputReadLine();

                lock (m_oSyncRoot)
                {
                    m_oProcess = oProcess;
                }

                m_dtLastStartedAt = DateTime.Now;
            }
            catch (Exception oEx)
            {
                RtspCaptureLog.WritePersistent(m_sRootPath, m_sCameraName, "START_FAILED", oEx.Message);
            }
        }

        /// <summary>
        /// ffmpeg 출력을 계속 읽어 파이프 버퍼가 차는 것을 막습니다.
        /// 마지막 한 줄은 남겨 두었다가 재기동 로그에 함께 적습니다. 원인 파악에 필요합니다.
        /// </summary>
        private void Process_OutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            lock (m_oErrorSyncRoot)
            {
                m_oRecentErrors.Enqueue(e.Data.Trim());
                while (m_oRecentErrors.Count > RecentErrorLineCount)
                {
                    m_oRecentErrors.Dequeue();
                }

                m_sLastProcessError = string.Join(" | ", m_oRecentErrors.ToArray());
            }
        }

        private void StopProcess()
        {
            Process oProcess;
            lock (m_oSyncRoot)
            {
                oProcess = m_oProcess;
                m_oProcess = null;
            }

            if (oProcess == null)
            {
                return;
            }

            try
            {
                if (!oProcess.HasExited)
                {
                    oProcess.Kill();
                    oProcess.WaitForExit(1000);
                }
            }
            catch (Exception oEx)
            {
                RtspCaptureLog.WritePersistent(m_sRootPath, m_sCameraName, "STOP_ERROR", oEx.Message);
            }
            finally
            {
                try
                {
                    oProcess.Dispose();
                }
                catch (Exception)
                {
                    // 이미 정리된 프로세스면 무시합니다.
                }
            }
        }

        /// <summary>
        /// 최신 프레임 1장을 계속 덮어쓰는 ffmpeg 인자를 만듭니다.
        ///
        /// -update 1  같은 파일에 계속 덮어씁니다. 파일이 쌓이지 않습니다.
        /// -vf fps=1  초당 1장만 저장합니다. 검사에는 충분하고 디스크 쓰기를 줄입니다.
        /// -an        오디오를 받지 않습니다.
        ///
        /// 인코더는 출력 확장자로 정해집니다(.png이면 무손실 png).
        /// 화질 옵션(-q:v)은 JPEG 전용이라 붙이지 않습니다.
        /// 연결 수립은 한 번뿐이므로 -analyzeduration 을 넉넉히 주어 첫 프레임을 확실히 잡습니다.
        /// </summary>
        protected virtual string BuildArguments()
        {
            // -rtsp_transport 와 -timeout 은 RTSP 입력에서만 유효합니다. 로컬 파일을 입력으로 주는
            // 검증 환경에서는 "Option not found"로 즉시 실패하므로 입력 종류를 보고 붙입니다.
            //
            // -timeout 은 소켓 I/O가 응답하지 않을 때 ffmpeg가 포기하는 시간입니다(마이크로초).
            // 이것이 없으면 NVR이 응답을 끊었을 때 ffmpeg가 무한정 매달려 있어,
            // 프로세스는 살아 있는데 프레임은 오지 않는 상태가 됩니다.
            string sInputOption;
            if (IsRtspUrl(m_sRtspUrl))
            {
                sInputOption = "-rtsp_transport tcp -timeout " +
                               SocketTimeoutMicroseconds.ToString(CultureInfo.InvariantCulture) + " ";
            }
            else
            {
                // 로컬 파일은 실시간 스트림이 아닙니다. 그대로 읽으면 ffmpeg가 파일 끝까지
                // 최대 속도로 읽고 종료해 프레임 갱신이 멈춥니다. 상시 연결은 "프레임이 계속 온다"를
                // 전제로 하므로, 검증 환경에서도 같은 조건이 되도록 실시간 속도로 반복 재생합니다.
                //   -re            실제 재생 속도로 읽습니다.
                //   -stream_loop -1  파일 끝에 닿으면 처음부터 다시 읽습니다.
                sInputOption = "-re -stream_loop -1 ";
            }

            return "-nostdin -loglevel error " +
                   sInputOption +
                   "-analyzeduration 5000000 -probesize 5000000 " +
                   "-i " + Quote(m_sRtspUrl) + " " +
                   "-an " +
                   "-vf fps=" + FramesPerSecondDivisor.ToString(CultureInfo.InvariantCulture) + " " +
                   "-update 1 -y " + Quote(m_sLatestFramePath);
        }

        private static bool IsRtspUrl(string sUrl)
        {
            if (string.IsNullOrWhiteSpace(sUrl))
            {
                return false;
            }

            return sUrl.TrimStart().StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase);
        }

        private string Quote(string sValue)
        {
            return "\"" + sValue.Replace("\"", "\\\"") + "\"";
        }
    }
}
