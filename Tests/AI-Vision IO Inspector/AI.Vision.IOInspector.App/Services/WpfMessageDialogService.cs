using System.Windows;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// WPF MessageBox를 사용해 저장 차단 사유를 사용자에게 명확히 보여줍니다.
    /// </summary>
    public class WpfMessageDialogService : IMessageDialogService
    {
        public void ShowWarning(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
