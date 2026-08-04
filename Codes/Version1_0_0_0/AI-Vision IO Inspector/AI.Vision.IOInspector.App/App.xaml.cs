using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.Vision;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// WPF 애플리케이션 진입점입니다.
    /// 검사 중 UI 및 백그라운드 작업에서 발생한 예외를 기록하고 프로그램 수명을 관리합니다.
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const string SingleInstanceMutexName = @"Local\AI.Vision.IOInspector.App.SingleInstance";
        private Mutex _singleInstanceMutex;
        private bool _ownsSingleInstanceMutex;

        static App()
        {
            RuntimeAssemblyResolver.Register();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            RuntimeAssemblyResolver.Register();
            if (!TryAcquireSingleInstanceMutex())
            {
                MessageBox.Show(
                    "AI-Vision IO Inspector 프로그램이 이미 실행 중입니다.",
                    "중복 실행 방지",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                Shutdown(0);
                return;
            }

            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            ShowMainWindow();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            VisionRuntimeFactory.ShutdownVladRuntime(AppContext.BaseDirectory);
            ReleaseSingleInstanceMutex();
            base.OnExit(e);
        }

        /// <summary>
        /// 같은 Windows 사용자 세션에서 Mutex를 먼저 확보한 프로세스만 실행을 계속합니다.
        /// </summary>
        private bool TryAcquireSingleInstanceMutex()
        {
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
            _ownsSingleInstanceMutex = createdNew;
            return createdNew;
        }

        private void ReleaseSingleInstanceMutex()
        {
            if (_singleInstanceMutex == null)
            {
                return;
            }

            if (_ownsSingleInstanceMutex)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }
            }

            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            _ownsSingleInstanceMutex = false;
        }

        /// <summary>
        /// StartupUri에 의존하지 않고 메인 창을 명시적으로 생성하고 표시합니다.
        /// 창 생성 실패 시 보이지 않는 프로세스가 남지 않도록 오류를 알린 후 종료합니다.
        /// </summary>
        private void ShowMainWindow()
        {
            try
            {
                AppBootstrapper.AppendStartupTrace(AppContext.BaseDirectory, "APP_MAIN_WINDOW_SHOW_START");

                MainWindow window = new MainWindow();
                MainWindow = window;
                window.Show();
                window.Activate();

                AppBootstrapper.AppendStartupTrace(AppContext.BaseDirectory, "APP_MAIN_WINDOW_SHOW_END");
            }
            catch (Exception exception)
            {
                AppBootstrapper.AppendStartupTrace(
                    AppContext.BaseDirectory,
                    "APP_MAIN_WINDOW_SHOW_FAILED: " + exception.GetBaseException().Message);

                MessageBox.Show(
                    "프로그램 화면을 열지 못했습니다.\r\n" + exception.GetBaseException().Message,
                    "프로그램 시작 오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine("UI 처리 중 예외가 발생했습니다. " + e.Exception.Message);
            e.Handled = true;
        }

        private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine("백그라운드 작업의 예외가 관찰되지 않았습니다. " + e.Exception.GetBaseException().Message);
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
