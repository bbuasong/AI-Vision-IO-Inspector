using System.Windows;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// 검사 화면 OCR이 DB에 없는 품번을 읽었을 때, 신규 등록으로 진행할지 확인하는 팝업입니다.
    /// </summary>
    public partial class OcrUnregisteredPromptWindow : Window
    {
        public OcrUnregisteredPromptWindow(string title, string message)
        {
            InitializeComponent();
            Title = title;
            MessageTextBlock.Text = message;
        }

        private void OnProceedClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
