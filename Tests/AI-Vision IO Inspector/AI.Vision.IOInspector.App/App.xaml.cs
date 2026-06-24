using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AI.Vision.IOInspector.App.Services;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// WPF 애플리케이션 진입점입니다.
    /// 검사 중 UI/Task 예외가 발생해도 가능한 범위에서는 프로그램을 유지합니다.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        static App()
        {
            RuntimeAssemblyResolver.Register();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            RuntimeAssemblyResolver.Register();
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine("UI 처리 중 예외가 발생했습니다. " + e.Exception.Message);
            e.Handled = true;
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine("Background Task 예외가 관찰되지 않았습니다. " + e.Exception.GetBaseException().Message);
            e.SetObserved();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            if (exception != null)
            {
                Debug.WriteLine("프로세스 전역 미처리 예외가 발생했습니다. " + exception.Message);
            }
        }
    }
}
