using System.Windows;
using BarcodeScannerSample.ViewModels;

namespace BarcodeScannerSample
{
    /// <summary>
    /// 샘플 화면의 DataContext를 연결하고, 시작 시 바코드 입력창에 포커스를 줍니다.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            _viewModel.ReadingStarted += ViewModelReadingStarted;
            DataContext = _viewModel;
            Loaded += MainWindowLoaded;
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            StartReadingButton.Focus();
        }

        private void ViewModelReadingStarted(object sender, System.EventArgs e)
        {
            BarcodeInputBox.Focus();
            BarcodeInputBox.SelectAll();
        }
    }
}
