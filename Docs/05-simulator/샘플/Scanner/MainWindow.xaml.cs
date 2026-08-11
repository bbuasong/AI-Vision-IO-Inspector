using System.Windows;
using ScannerSample.ViewModels;

namespace ScannerSample
{
    /// <summary>
    /// Scanner OCR 샘플 화면의 DataContext를 연결합니다.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
