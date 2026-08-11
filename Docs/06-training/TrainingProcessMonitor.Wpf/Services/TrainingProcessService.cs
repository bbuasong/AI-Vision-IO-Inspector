using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TrainingProcessMonitor.Wpf.Services
{
    public sealed class TrainingProcessDataEventArgs : EventArgs
    {
        public TrainingProcessDataEventArgs(string data)
        {
            Data = data;
        }

        public string Data { get; private set; }
    }

    public sealed class TrainingProcessExitedEventArgs : EventArgs
    {
        public TrainingProcessExitedEventArgs(int? exitCode)
        {
            ExitCode = exitCode;
        }

        public int? ExitCode { get; private set; }
    }

    public sealed class TrainingProcessService : IDisposable
    {
        private Process _process;

        public event EventHandler<TrainingProcessDataEventArgs> OutputReceived;
        public event EventHandler<TrainingProcessDataEventArgs> ErrorReceived;
        public event EventHandler<TrainingProcessExitedEventArgs> Exited;

        public int Start(string trainingExePath, string arguments, string workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(trainingExePath))
            {
                throw new ArgumentException("외부 학습 프로그램 경로가 비어 있습니다.", "trainingExePath");
            }

            if (!File.Exists(trainingExePath))
            {
                throw new FileNotFoundException("외부 학습 프로그램을 찾을 수 없습니다.", trainingExePath);
            }

            DisposeProcess();

            var startInfo = new ProcessStartInfo
            {
                FileName = trainingExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                    ? Path.GetDirectoryName(trainingExePath)
                    : workingDirectory,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            _process.OutputDataReceived += Process_OutputDataReceived;
            _process.ErrorDataReceived += Process_ErrorDataReceived;
            _process.Exited += Process_Exited;

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            return _process.Id;
        }

        public void Kill()
        {
            if (_process == null)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void Dispose()
        {
            DisposeProcess();
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                OnOutputReceived(e.Data);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                OnErrorReceived(e.Data);
            }
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            var process = (Process)sender;
            int? exitCode = null;

            try
            {
                process.WaitForExit(1000);
                exitCode = process.ExitCode;
            }
            catch (InvalidOperationException)
            {
            }

            OnExited(exitCode);
        }

        private void DisposeProcess()
        {
            if (_process == null)
            {
                return;
            }

            _process.OutputDataReceived -= Process_OutputDataReceived;
            _process.ErrorDataReceived -= Process_ErrorDataReceived;
            _process.Exited -= Process_Exited;
            _process.Dispose();
            _process = null;
        }

        private void OnOutputReceived(string data)
        {
            var handler = OutputReceived;
            if (handler != null)
            {
                handler(this, new TrainingProcessDataEventArgs(data));
            }
        }

        private void OnErrorReceived(string data)
        {
            var handler = ErrorReceived;
            if (handler != null)
            {
                handler(this, new TrainingProcessDataEventArgs(data));
            }
        }

        private void OnExited(int? exitCode)
        {
            var handler = Exited;
            if (handler != null)
            {
                handler(this, new TrainingProcessExitedEventArgs(exitCode));
            }
        }
    }
}
