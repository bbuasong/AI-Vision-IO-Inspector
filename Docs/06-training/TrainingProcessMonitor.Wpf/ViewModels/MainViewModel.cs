using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TrainingProcessMonitor.Wpf.Models;
using TrainingProcessMonitor.Wpf.Services;

namespace TrainingProcessMonitor.Wpf.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private readonly TrainingProcessService _trainingProcessService;
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _elapsedTimer;

        private string _externalTrainingExePath;
        private string _jobRootPath;
        private TrainingStatus _status;
        private int _progress;
        private string _currentMessage;
        private string _errorCode;
        private string _errorMessage;
        private DateTime? _startedAt;
        private DateTime? _endedAt;
        private bool _isTraining;
        private string _currentJobId;
        private string _currentJobDirectory;
        private string _monitorLogPath;
        private bool _cancelRequested;
        private bool _autoEmitOnStart;
        private bool _autoCloseOnProcessExit;

        public MainViewModel()
        {
            _dispatcher = Application.Current.Dispatcher;
            _trainingProcessService = new TrainingProcessService();
            _trainingProcessService.OutputReceived += TrainingProcessService_OutputReceived;
            _trainingProcessService.ErrorReceived += TrainingProcessService_ErrorReceived;
            _trainingProcessService.Exited += TrainingProcessService_Exited;

            _status = TrainingStatus.Idle;
            _progress = 0;
            _currentMessage = "대기 중";
            _errorCode = string.Empty;
            _errorMessage = string.Empty;
            _currentJobId = string.Empty;
            _externalTrainingExePath = ResolveDefaultExternalTrainingExePath();
            _jobRootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TrainingJobs");

            ReceivedMessages = new ObservableCollection<ReceivedTrainingMessage>();
            StartTrainingCommand = new RelayCommand(_ => StartImageTraining(), _ => !IsTraining);
            CancelTrainingCommand = new RelayCommand(_ => CancelTraining(), _ => IsTraining);
            BrowseExternalExeCommand = new RelayCommand(_ => BrowseExternalExe(), _ => !IsTraining);
            ClearReceivedMessagesCommand = new RelayCommand(_ => ReceivedMessages.Clear(), _ => !IsTraining);

            _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _elapsedTimer.Tick += (sender, args) => OnPropertyChanged("TimeSummary");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ReceivedTrainingMessage> ReceivedMessages { get; private set; }

        public ICommand StartTrainingCommand { get; private set; }
        public ICommand CancelTrainingCommand { get; private set; }
        public ICommand BrowseExternalExeCommand { get; private set; }
        public ICommand ClearReceivedMessagesCommand { get; private set; }

        public string ExternalTrainingExePath
        {
            get { return _externalTrainingExePath; }
            set
            {
                if (_externalTrainingExePath == value)
                {
                    return;
                }

                _externalTrainingExePath = value;
                OnPropertyChanged("ExternalTrainingExePath");
            }
        }

        public string JobRootPath
        {
            get { return _jobRootPath; }
            set
            {
                if (_jobRootPath == value)
                {
                    return;
                }

                _jobRootPath = value;
                OnPropertyChanged("JobRootPath");
            }
        }

        public TrainingStatus Status
        {
            get { return _status; }
            private set
            {
                if (_status == value)
                {
                    return;
                }

                _status = value;
                OnPropertyChanged("Status");
            }
        }

        public int Progress
        {
            get { return _progress; }
            private set
            {
                if (_progress == value)
                {
                    return;
                }

                _progress = value;
                OnPropertyChanged("Progress");
                OnPropertyChanged("ProgressText");
            }
        }

        public string ProgressText
        {
            get { return Progress + "%"; }
        }

        public string CurrentMessage
        {
            get { return _currentMessage; }
            private set
            {
                if (_currentMessage == value)
                {
                    return;
                }

                _currentMessage = value;
                OnPropertyChanged("CurrentMessage");
            }
        }

        public string ErrorCode
        {
            get { return _errorCode; }
            private set
            {
                if (_errorCode == value)
                {
                    return;
                }

                _errorCode = value;
                OnPropertyChanged("ErrorCode");
            }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            private set
            {
                if (_errorMessage == value)
                {
                    return;
                }

                _errorMessage = value;
                OnPropertyChanged("ErrorMessage");
            }
        }

        public string CurrentJobId
        {
            get { return _currentJobId; }
            private set
            {
                if (_currentJobId == value)
                {
                    return;
                }

                _currentJobId = value;
                OnPropertyChanged("CurrentJobId");
            }
        }

        public bool IsTraining
        {
            get { return _isTraining; }
            private set
            {
                if (_isTraining == value)
                {
                    return;
                }

                _isTraining = value;
                OnPropertyChanged("IsTraining");
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string TimeSummary
        {
            get
            {
                var started = _startedAt.HasValue ? _startedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
                var ended = _endedAt.HasValue ? _endedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "-";
                var elapsedEnd = _endedAt ?? DateTime.Now;
                var elapsed = _startedAt.HasValue ? (elapsedEnd - _startedAt.Value).ToString(@"hh\:mm\:ss") : "-";
                return "시작: " + started + " / 종료: " + ended + " / 경과: " + elapsed;
            }
        }

        public void RunAutoTest()
        {
            _autoEmitOnStart = true;
            _autoCloseOnProcessExit = true;
            StartImageTraining();
        }

        public void StartImageTraining()
        {
            if (IsTraining)
            {
                AddReceivedRow("WPF", "", "", "이미 Training 프로그램이 실행 중입니다.", "");
                return;
            }

            try
            {
                ResetForNewTraining();
                CreateJobDirectories();

                var arguments = BuildArguments();
                var workingDirectory = Path.GetDirectoryName(ExternalTrainingExePath);

                AddReceivedRow("PROCESS", "START", "", "StartImageTraining 호출", "");
                AppendMonitorLog("[PROCESS] StartImageTraining");
                AppendMonitorLog("[PROCESS] EXE: " + ExternalTrainingExePath);
                AppendMonitorLog("[PROCESS] ARGS: " + arguments);

                var processId = _trainingProcessService.Start(ExternalTrainingExePath, arguments, workingDirectory);
                AddReceivedRow("PROCESS", "PID", processId.ToString(), "Training 프로그램 실행됨", "");
                AppendMonitorLog("[PROCESS] Started. PID=" + processId);
            }
            catch (Exception ex)
            {
                Status = TrainingStatus.Failed;
                CurrentMessage = "Training 프로그램 실행에 실패했습니다.";
                ErrorCode = "START_FAILED";
                ErrorMessage = ex.Message;
                _endedAt = DateTime.Now;
                IsTraining = false;
                _elapsedTimer.Stop();
                OnPropertyChanged("TimeSummary");
                AddReceivedRow("ERROR", "START_FAILED", "", ex.Message, ex.ToString());
                AppendMonitorLog("[ERROR] " + ex);

                if (_autoCloseOnProcessExit)
                {
                    Application.Current.Shutdown(1);
                }
            }
        }

        private void CancelTraining()
        {
            if (!IsTraining)
            {
                return;
            }

            _cancelRequested = true;
            CurrentMessage = "Training 프로그램 종료를 요청했습니다.";

            try
            {
                if (!string.IsNullOrWhiteSpace(_currentJobDirectory))
                {
                    File.WriteAllText(Path.Combine(_currentJobDirectory, "cancel.flag"), DateTime.Now.ToString("O"), Encoding.UTF8);
                    AddReceivedRow("WPF", "CANCEL", "", "cancel.flag 생성", "cancel.flag");
                    AppendMonitorLog("[WPF] cancel.flag created");
                }
            }
            catch (Exception ex)
            {
                AddReceivedRow("WPF", "CANCEL", "", "cancel.flag 생성 실패", ex.Message);
            }

            Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(task =>
            {
                if (!IsTraining)
                {
                    return;
                }

                AddReceivedRow("PROCESS", "KILL", "", "Training 프로그램을 종료합니다.", "Kill");
                AppendMonitorLog("[PROCESS] Kill requested");
                _trainingProcessService.Kill();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void BrowseExternalExe()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Training 프로그램 선택",
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                ExternalTrainingExePath = dialog.FileName;
            }
        }

        private void ResetForNewTraining()
        {
            _cancelRequested = false;
            _startedAt = DateTime.Now;
            _endedAt = null;
            Status = TrainingStatus.Starting;
            Progress = 0;
            CurrentMessage = "Training 프로그램 실행 준비 중";
            ErrorCode = string.Empty;
            ErrorMessage = string.Empty;
            CurrentJobId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            IsTraining = true;
            _elapsedTimer.Start();
            OnPropertyChanged("TimeSummary");
        }

        private void CreateJobDirectories()
        {
            _currentJobDirectory = Path.Combine(JobRootPath, CurrentJobId);
            Directory.CreateDirectory(Path.Combine(_currentJobDirectory, "input"));
            Directory.CreateDirectory(Path.Combine(_currentJobDirectory, "output"));
            Directory.CreateDirectory(Path.Combine(_currentJobDirectory, "log"));
            _monitorLogPath = Path.Combine(_currentJobDirectory, "log", "wpf_training_monitor.log");
        }

        private string BuildArguments()
        {
            var inputDirectory = Path.Combine(_currentJobDirectory, "input");
            var outputDirectory = Path.Combine(_currentJobDirectory, "output");
            var logDirectory = Path.Combine(_currentJobDirectory, "log");

            var arguments = "--jobId " + Quote(CurrentJobId) +
                            " --input " + Quote(inputDirectory) +
                            " --output " + Quote(outputDirectory) +
                            " --log " + Quote(logDirectory);

            if (_autoEmitOnStart)
            {
                arguments += " --autoEmit true";
            }

            return arguments;
        }

        private static string Quote(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void TrainingProcessService_OutputReceived(object sender, TrainingProcessDataEventArgs e)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                var parsed = TrainingProtocolMessage.Parse(e.Data);
                if (parsed.IsProtocolMessage)
                {
                    AddReceivedRow("STDOUT", parsed.Type, parsed.Value, parsed.Message, e.Data);
                    ParseTrainingOutput(parsed);
                }
                else
                {
                    AddReceivedRow("STDOUT", "", "", e.Data, e.Data);
                }

                AppendMonitorLog("[STDOUT] " + e.Data);
            }));
        }

        private void TrainingProcessService_ErrorReceived(object sender, TrainingProcessDataEventArgs e)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                var parsed = TrainingProtocolMessage.Parse(e.Data);
                if (parsed.IsProtocolMessage)
                {
                    AddReceivedRow("STDERR", parsed.Type, parsed.Value, parsed.Message, e.Data);
                }
                else
                {
                    AddReceivedRow("STDERR", "", "", e.Data, e.Data);
                }

                AppendMonitorLog("[STDERR] " + e.Data);
            }));
        }

        private void TrainingProcessService_Exited(object sender, TrainingProcessExitedEventArgs e)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                var exitText = e.ExitCode.HasValue ? e.ExitCode.Value.ToString() : "unknown";
                AddReceivedRow("PROCESS", "EXITED", exitText, "Training 프로그램 종료", "ExitCode=" + exitText);
                AppendMonitorLog("[PROCESS] Exited. ExitCode=" + exitText);

                if (Status != TrainingStatus.Completed &&
                    Status != TrainingStatus.Failed &&
                    Status != TrainingStatus.Canceled)
                {
                    Status = _cancelRequested ? TrainingStatus.Canceled : TrainingStatus.Failed;
                    CurrentMessage = _cancelRequested
                        ? "사용자 요청으로 Training 프로그램이 종료되었습니다."
                        : "Training 프로그램이 DONE/ERROR 메시지 없이 종료되었습니다.";
                    ErrorCode = _cancelRequested ? string.Empty : "NO_TERMINAL_MESSAGE";
                    ErrorMessage = _cancelRequested ? string.Empty : CurrentMessage;
                }

                _endedAt = DateTime.Now;
                IsTraining = false;
                _elapsedTimer.Stop();
                OnPropertyChanged("TimeSummary");

                if (_autoCloseOnProcessExit)
                {
                    Application.Current.Shutdown(Status == TrainingStatus.Completed ? 0 : 1);
                }
            }));
        }

        private void ParseTrainingOutput(TrainingProtocolMessage parsed)
        {
            switch (parsed.Type)
            {
                case "START":
                    Status = TrainingStatus.Running;
                    Progress = 0;
                    CurrentMessage = parsed.Message;
                    break;

                case "PROGRESS":
                    Status = TrainingStatus.Running;
                    Progress = parsed.GetProgressOrDefault(Progress);
                    CurrentMessage = parsed.Message;
                    break;

                case "DONE":
                    Status = TrainingStatus.Completed;
                    Progress = 100;
                    CurrentMessage = parsed.Message;
                    _endedAt = DateTime.Now;
                    OnPropertyChanged("TimeSummary");
                    break;

                case "ERROR":
                    Status = TrainingStatus.Failed;
                    ErrorCode = parsed.Value;
                    ErrorMessage = parsed.Message;
                    CurrentMessage = parsed.Message;
                    _endedAt = DateTime.Now;
                    OnPropertyChanged("TimeSummary");
                    break;

                case "CANCELED":
                    Status = TrainingStatus.Canceled;
                    CurrentMessage = parsed.Message;
                    _endedAt = DateTime.Now;
                    OnPropertyChanged("TimeSummary");
                    break;

                case "WARN":
                case "LOG":
                    CurrentMessage = parsed.Message;
                    break;
            }
        }

        private void AddReceivedRow(string source, string type, string value, string message, string raw)
        {
            ReceivedMessages.Add(new ReceivedTrainingMessage
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Source = source,
                Type = type,
                Value = value,
                Message = message,
                Raw = raw
            });

            while (ReceivedMessages.Count > 1000)
            {
                ReceivedMessages.RemoveAt(0);
            }
        }

        private void AppendMonitorLog(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_monitorLogPath))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(_monitorLogPath));
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
                File.AppendAllText(_monitorLogPath, line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string ResolveDefaultExternalTrainingExePath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var directPath = Path.Combine(baseDirectory, "ExternalTraining.Sample.exe");
            if (File.Exists(directPath))
            {
                return directPath;
            }

            var debugPath = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\ExternalTraining.Sample\bin\Debug\ExternalTraining.Sample.exe"));
            if (File.Exists(debugPath))
            {
                return debugPath;
            }

            var releasePath = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\ExternalTraining.Sample\bin\Release\ExternalTraining.Sample.exe"));
            if (File.Exists(releasePath))
            {
                return releasePath;
            }

            return directPath;
        }

        private void OnPropertyChanged(string propertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
