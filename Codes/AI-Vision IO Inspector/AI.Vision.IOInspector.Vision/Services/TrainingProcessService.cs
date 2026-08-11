using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Vision.LegacyVlad;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// Study.bat를 별도 프로세스로 실행하고 StandardOutput, StandardError, Exited를 수신합니다.
    /// 학습 프로토콜의 DONE/ERROR/CANCELED 수신 여부도 보관하여 종료 후 재초기화 판단에 사용합니다.
    /// </summary>
    public sealed class TrainingProcessService : IDisposable
    {
        private readonly object _syncRoot;
        private Process _process;
        private bool _completionMessageReceived;
        private bool _terminalErrorMessageReceived;
        private bool _disposed;

        public TrainingProcessService()
        {
            _syncRoot = new object();
        }

        public event EventHandler<TrainingProcessDataEventArgs> OutputReceived;

        public event EventHandler<TrainingProcessDataEventArgs> ErrorReceived;

        public event EventHandler<TrainingProcessExitedEventArgs> Exited;

        public bool IsRunning
        {
            get
            {
                lock (_syncRoot)
                {
                    return _process != null && !_process.HasExited;
                }
            }
        }

        /// <summary>
        /// 기존 StartImageTraining 경로에서 사용하는 Study.bat를 출력 리디렉션 방식으로 실행합니다.
        /// 두 VladId는 학습 시작 전 전체/Crop 런타임이 모두 준비됐는지 확인하기 위한 동일 프로세스 핸들입니다.
        /// IntPtr은 외부 학습 프로세스에서 유효하지 않으므로 명령행/환경변수로 전달하지 않습니다.
        /// </summary>
        public string Start(IntPtr fullImageVladId, IntPtr croppedImageVladId)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();
                if (_process != null && !_process.HasExited)
                {
                    throw new InvalidOperationException("이미지 학습 프로그램이 이미 실행 중입니다.");
                }

                DisposeProcess();
                _completionMessageReceived = false;
                _terminalErrorMessageReceived = false;

                if (fullImageVladId == IntPtr.Zero || croppedImageVladId == IntPtr.Zero)
                {
                    throw new ArgumentException("이미지 학습 시작 전에 전체 이미지와 Crop 이미지용 VLAD_ID가 모두 초기화되어야 합니다.");
                }

                ProcessStartInfo startInfo = VLAD_Ops_Ai.CreateImageTrainingStartInfo(fullImageVladId, croppedImageVladId);
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.UseShellExecute = false;
                startInfo.CreateNoWindow = true;
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.StandardOutputEncoding = Encoding.UTF8;
                startInfo.StandardErrorEncoding = Encoding.UTF8;

                Process process = new Process();
                process.StartInfo = startInfo;
                process.EnableRaisingEvents = false;
                process.OutputDataReceived += OnOutputDataReceived;
                process.ErrorDataReceived += OnErrorDataReceived;

                if (!process.Start())
                {
                    process.Dispose();
                    throw new InvalidOperationException("이미지 학습 프로세스를 시작하지 못했습니다.");
                }

                _process = process;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.Exited += OnProcessExited;
                // 이미 종료된 프로세스도 EnableRaisingEvents를 켜는 시점에 Exited가 발생합니다.
                process.EnableRaisingEvents = true;

                return "이미지 학습 배치 파일을 실행했습니다. ProcessId=" +
                       process.Id.ToString(CultureInfo.InvariantCulture) +
                       ", Path=" +
                       startInfo.FileName +
                       " " +
                       startInfo.Arguments +
                       ", FullImageVladId=" +
                       fullImageVladId.ToInt64().ToString(CultureInfo.InvariantCulture) +
                       ", CroppedImageVladId=" +
                       croppedImageVladId.ToInt64().ToString(CultureInfo.InvariantCulture);
            }
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                DisposeProcess();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            UpdateTerminalMessageState(e.Data);
            RaiseOutputReceived(e.Data);
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            RaiseErrorReceived(e.Data);
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            Process exitedProcess = sender as Process;
            int? exitCode = null;
            bool completionMessageReceived;
            bool terminalErrorMessageReceived;

            if (exitedProcess != null)
            {
                try
                {
                    // 비동기 StandardOutput/StandardError의 마지막 줄까지 수신한 후 종료 이벤트를 전달합니다.
                    exitedProcess.WaitForExit();
                    exitCode = exitedProcess.ExitCode;
                }
                catch (InvalidOperationException)
                {
                }
            }

            lock (_syncRoot)
            {
                completionMessageReceived = _completionMessageReceived;
                terminalErrorMessageReceived = _terminalErrorMessageReceived;
            }

            EventHandler<TrainingProcessExitedEventArgs> handler = Exited;
            if (handler != null)
            {
                handler(
                    this,
                    new TrainingProcessExitedEventArgs(
                        exitCode,
                        completionMessageReceived,
                        terminalErrorMessageReceived,
                        false,
                        false,
                        string.Empty));
            }
        }

        private void UpdateTerminalMessageState(string data)
        {
            string messageType = GetMessageType(data);
            lock (_syncRoot)
            {
                if (string.Equals(messageType, "DONE", StringComparison.OrdinalIgnoreCase))
                {
                    _completionMessageReceived = true;
                }
                else if (string.Equals(messageType, "ERROR", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(messageType, "CANCELED", StringComparison.OrdinalIgnoreCase))
                {
                    _terminalErrorMessageReceived = true;
                }
            }
        }

        private static string GetMessageType(string data)
        {
            int separatorIndex = data.IndexOf('|');
            if (separatorIndex < 0)
            {
                return data.Trim();
            }

            return data.Substring(0, separatorIndex).Trim();
        }

        private void RaiseOutputReceived(string data)
        {
            EventHandler<TrainingProcessDataEventArgs> handler = OutputReceived;
            if (handler != null)
            {
                handler(this, new TrainingProcessDataEventArgs(data));
            }
        }

        private void RaiseErrorReceived(string data)
        {
            EventHandler<TrainingProcessDataEventArgs> handler = ErrorReceived;
            if (handler != null)
            {
                handler(this, new TrainingProcessDataEventArgs(data));
            }
        }

        private void DisposeProcess()
        {
            if (_process == null)
            {
                return;
            }

            _process.OutputDataReceived -= OnOutputDataReceived;
            _process.ErrorDataReceived -= OnErrorDataReceived;
            _process.Exited -= OnProcessExited;
            _process.Dispose();
            _process = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
