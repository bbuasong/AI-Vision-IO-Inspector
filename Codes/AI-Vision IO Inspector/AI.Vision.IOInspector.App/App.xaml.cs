using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AI.Vision.IOInspector.App.Controls;
using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.Vision.Services;
using AI.Vision.IOInspector.Infrastructure;
using AI.Vision.IOInspector.Vision;
using AI.Vision.IOInspector.Vision.LegacyVlad;

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

            // 프레임을 얼마나 자주 담을지 설정에서 읽어 넣습니다.
            // 콜백마다 설정 파일을 읽으면 그동안 다음 프레임이 밀립니다.
            try
            {
                VLAD_Ops_RTSP.ApplyFrameCacheMinimumInterval(
                    VladRuntimeSettings.Load().CallbackFrameMinimumIntervalMilliseconds);
            }
            catch
            {
                // 설정을 읽지 못하면 기본값을 그대로 씁니다.
            }

            // RTSP 콜백으로 프레임이 얼마나 들어오는지 세기 시작합니다.
            // 화면을 이 프레임으로 그릴 수 있는지는 현장에서 재봐야 알 수 있어,
            // 계측을 항상 켜 두고 10초마다 rtsp-frame-metrics 로그에 남깁니다.
            try
            {
                RtspFrameMetrics.Start(VladRuntimeSettings.Load().RtspFrameMetricsIntervalSeconds);
            }
            catch
            {
                RtspFrameMetrics.Start();
            }

            // 보관 기간이 지난 일자별 로그 폴더를 먼저 정리합니다.
            // 로그는 DB\Logs\{일자}\{로그이름}-{일자}-{시작시각}.log로 남으므로,
            // 지난 폴더를 지우는 것만으로 오래된 로그가 정리됩니다.
            ApplicationLogFileResolver.CleanupExpiredLogs(
                AppContext.BaseDirectory,
                VladRuntimeSettings.Load().LogRetentionDays);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // VLAD_Custom_Registration은 네이티브 전역 상태 때문에 시작 스레드에서 동기 호출해야 하고,
            // 최초 TensorFlow/CUDA 로딩까지 겹치면 수 초~수 분이 걸릴 수 있습니다(DB\Logs\vlad-startup.log 참고).
            // 이 초기화가 끝나기 전까지 MainWindow 생성자 자체가 반환되지 않아 창이 전혀 뜨지 않으므로,
            // 창이 없어 보여 "실행이 안 된다"고 오인하지 않도록 가벼운 스플래시 창을 먼저 띄워 둡니다.
            Window splash = ShowStartupSplash();
            ShowMainWindow();
            splash.Close();

            // 검사 화면의 6개 카메라가 LibVLC 엔진 최초 로딩 비용을 서로 기다리며 순차적으로,
            // 크게 시간차를 두고 나타나는 문제를 막기 위해 미리 예열합니다.
            // VLAD_Custom_Registration이 끝난 뒤에 예열하도록 순서를 바꿨습니다 — 백그라운드 예열이
            // LibVLC 네이티브 DLL을 로드하는 시점이 VLAD_SDK.dll/CUDA/TensorFlow 로딩과 겹치면
            // 네이티브 DLL 로더 잠금 경합으로 VLAD_Custom_Registration이 멈추는 것과 시점이
            // 일치했기 때문입니다.
            Task.Run(new Action(RtspVideoHost.WarmUpEngine));
        }

        /// <summary>
        /// MainWindow 생성자의 VLAD/DB 초기화가 끝나기 전까지 화면에 아무것도 보이지 않는 공백 구간을
        /// 없애기 위한 최소 스플래시 창입니다. MainWindowViewModel 등 무거운 초기화에는 의존하지 않습니다.
        /// </summary>
        private static Window ShowStartupSplash()
        {
            Window splash = new Window
            {
                Title = "AI-Vision IO Inspector",
                Width = 420,
                Height = 150,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = true,
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x14, 0x1B)),
                Content = new TextBlock
                {
                    Text = "AI-Vision IO Inspector" + Environment.NewLine + "초기화 중입니다. 잠시만 기다려 주세요...",
                    Foreground = Brushes.White,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            splash.Show();
            // MainWindow 생성자의 동기 초기화가 UI 스레드를 점유하기 전에, 지금까지 구성한 화면을
            // 실제로 한 번 렌더링해 화면에 표시되도록 강제합니다(WPF 합성 스레드는 UI 스레드와 분리되어
            // 있으므로, 이후 UI 스레드가 블로킹되어도 이미 전달된 프레임은 계속 화면에 남습니다).
            splash.Dispatcher.Invoke(new Action(delegate { }), DispatcherPriority.Render);
            return splash;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            DispatcherUnhandledException -= OnDispatcherUnhandledException;
            RtspFrameMetrics.Stop();
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
