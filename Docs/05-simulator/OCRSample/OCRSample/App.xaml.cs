using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OCRSample
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            WriteExceptionLog("UI", e.Exception);
            MessageBox.Show(
                "처리 중 오류가 발생했습니다. 프로그램은 계속 실행됩니다." +
                Environment.NewLine + Environment.NewLine + e.Exception.Message,
                "OCRSample 오류",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            WriteExceptionLog("AppDomain", e.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            WriteExceptionLog("Task", e.Exception);
            e.SetObserved();
        }

        private static void WriteExceptionLog(string source, Exception exception)
        {
            try
            {
                string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "error_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                File.AppendAllText(
                    path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                    " [" + source + "]" + Environment.NewLine +
                    (exception == null ? "Unknown exception" : exception.ToString()) +
                    Environment.NewLine + Environment.NewLine);
            }
            catch
            {
                // 로깅 실패는 원래 예외 처리에 영향을 주지 않는다.
            }
        }
    }
}
