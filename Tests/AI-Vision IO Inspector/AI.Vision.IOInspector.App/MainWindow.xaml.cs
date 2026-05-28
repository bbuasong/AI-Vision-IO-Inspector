using System;
using System.Windows;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// 메인 윈도우 코드비하인드입니다.
    /// 화면 로직은 ViewModel이 담당하고, 여기서는 시작 시점의 DataContext 연결만 수행합니다.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = AppBootstrapper.CreateMainWindowViewModel(AppContext.BaseDirectory);
        }
    }
}
