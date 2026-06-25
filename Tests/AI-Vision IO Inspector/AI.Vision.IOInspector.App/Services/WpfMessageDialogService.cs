using System.Threading;
using System.Windows;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// WPF MessageBox를 사용해 저장 차단 사유를 사용자에게 명확히 보여줍니다.
    /// </summary>
    public class WpfMessageDialogService : IMessageDialogService
    {
        private int _confirmationVisible;

        public void ShowWarning(string title, string message)
        {
            Window owner = GetDialogOwner();
            BringWindowToFront(owner);
            if (owner != null)
            {
                MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public bool ShowConfirmation(string title, string message)
        {
            Window owner = GetDialogOwner();
            if (Interlocked.CompareExchange(ref _confirmationVisible, 1, 0) != 0)
            {
                BringWindowToFront(owner);
                return false;
            }

            try
            {
                BringWindowToFront(owner);
                MessageBoxResult result;
                if (owner != null)
                {
                    result = MessageBox.Show(
                        owner,
                        message,
                        title,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No);
                }
                else
                {
                    result = MessageBox.Show(
                        message,
                        title,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning,
                        MessageBoxResult.No);
                }

                return result == MessageBoxResult.Yes;
            }
            finally
            {
                Interlocked.Exchange(ref _confirmationVisible, 0);
            }
        }

        private Window GetDialogOwner()
        {
            if (System.Windows.Application.Current == null)
            {
                return null;
            }

            return System.Windows.Application.Current.MainWindow;
        }

        private void BringWindowToFront(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        }
    }
}
