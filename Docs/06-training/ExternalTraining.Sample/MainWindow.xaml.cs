using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ExternalTraining.Sample
{
    public sealed class SentTrainingMessage
    {
        public string Time { get; set; }
        public string Stream { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Message { get; set; }
    }

    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly TextWriter _standardOutput;
        private readonly TextWriter _standardError;

        public MainWindow()
        {
            InitializeComponent();

            var arguments = ParseArguments(Environment.GetCommandLineArgs());
            JobId = GetArgument(arguments, "jobId", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            InputPath = GetArgument(arguments, "input", string.Empty);
            OutputPath = GetArgument(arguments, "output", string.Empty);
            LogPath = GetArgument(arguments, "log", string.Empty);

            _standardOutput = CreateWriter(Console.OpenStandardOutput());
            _standardError = CreateWriter(Console.OpenStandardError());

            SentMessages = new ObservableCollection<SentTrainingMessage>();
            DataContext = this;

            if (!string.IsNullOrWhiteSpace(LogPath))
            {
                Directory.CreateDirectory(LogPath);
            }

            AppendOwnLog("External training UI started. JobId=" + JobId);

            if (IsTrue(GetArgument(arguments, "autoEmit", "false")))
            {
                Dispatcher.BeginInvoke(new Action(async () => await RunAutoEmitAsync()));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string JobId { get; private set; }
        public string InputPath { get; private set; }
        public string OutputPath { get; private set; }
        public string LogPath { get; private set; }
        public ObservableCollection<SentTrainingMessage> SentMessages { get; private set; }

        private async Task RunAutoEmitAsync()
        {
            await Task.Delay(300);
            SendStdout("START", "0", "작업 시작");
            await Task.Delay(300);
            SendStdout("PROGRESS", "10", "이미지로딩중");
            await Task.Delay(300);
            SendStdout("PROGRESS", "30", "데이터셋 구성 중");
            await Task.Delay(300);
            SendStdout("PROGRESS", "60", "모델 학습 중");
            await Task.Delay(300);
            SendStdout("PROGRESS", "90", "모델 저장 중");
            await Task.Delay(300);
            SendStdout("DONE", "100", "완료");
            await Task.Delay(300);
            Close();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("START", "0", "작업 시작");
        }

        private void Progress10_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("PROGRESS", "10", "이미지로딩중");
        }

        private void LogImage_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("LOG", "0", "이미지 120장을 로딩했습니다");
        }

        private void Progress30_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("PROGRESS", "30", "데이터셋 구성 중");
        }

        private void Warn_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("WARN", "W001", "일부 이미지가 제외되었습니다");
        }

        private void Progress60_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("PROGRESS", "60", "모델 학습 중");
        }

        private void Stderr_Click(object sender, RoutedEventArgs e)
        {
            SendStderr("DEBUG", "0", "학습 내부 진단 로그 예시입니다");
        }

        private void ErrorRuntime_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("ERROR", "E004", "학습 중 예외가 발생했습니다");
        }

        private void ErrorInput_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("ERROR", "E001", "학습 이미지 개수 부족");
        }

        private void Progress90_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("PROGRESS", "90", "모델 저장 중");
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("DONE", "100", "완료");
        }

        private void Canceled_Click(object sender, RoutedEventArgs e)
        {
            SendStdout("CANCELED", "0", "사용자 요청으로 학습이 취소되었습니다");
        }

        private void CloseNoMessage_Click(object sender, RoutedEventArgs e)
        {
            AppendOwnLog("Closed without DONE/ERROR message.");
            Close();
        }

        private void SendStdout(string type, string value, string message)
        {
            var line = type + "|" + value + "|" + message;
            _standardOutput.WriteLine(line);
            _standardOutput.Flush();
            AddSentMessage("STDOUT", type, value, message);
            AppendOwnLog("[STDOUT] " + line);
        }

        private void SendStderr(string type, string value, string message)
        {
            var line = type + "|" + value + "|" + message;
            _standardError.WriteLine(line);
            _standardError.Flush();
            AddSentMessage("STDERR", type, value, message);
            AppendOwnLog("[STDERR] " + line);
        }

        private void AddSentMessage(string stream, string type, string value, string message)
        {
            SentMessages.Add(new SentTrainingMessage
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Stream = stream,
                Type = type,
                Value = value,
                Message = message
            });
        }

        private void AppendOwnLog(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(LogPath))
                {
                    return;
                }

                Directory.CreateDirectory(LogPath);
                var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
                File.AppendAllText(Path.Combine(LogPath, "external_training_ui.log"), line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static TextWriter CreateWriter(Stream stream)
        {
            return new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 1; i < args.Length; i++)
            {
                var token = args[i];
                if (!token.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                var key = token.Substring(2);
                var value = string.Empty;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = args[++i];
                }

                result[key] = value;
            }

            return result;
        }

        private static string GetArgument(IDictionary<string, string> arguments, string key, string defaultValue)
        {
            string value;
            return arguments.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : defaultValue;
        }

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
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
