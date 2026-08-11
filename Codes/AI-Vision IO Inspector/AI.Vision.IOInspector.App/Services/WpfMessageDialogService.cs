using System;
using System.Threading;
using System.Windows;
using AI.Vision.IOInspector.App;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// WPF MessageBox를 사용해 저장 차단 사유를 사용자에게 명확히 보여줍니다.
    /// </summary>
    public class WpfMessageDialogService : IMessageDialogService
    {
        private int _confirmationVisible;
        private int _trainingPromptVisible;
        private int _ocrUnregisteredPromptVisible;

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

        public bool ShowOcrUnregisteredConfirmation(string title, string message)
        {
            Window owner = GetDialogOwner();
            if (Interlocked.CompareExchange(ref _ocrUnregisteredPromptVisible, 1, 0) != 0)
            {
                BringWindowToFront(owner);
                return false;
            }

            try
            {
                BringWindowToFront(owner);
                OcrUnregisteredPromptWindow window = new OcrUnregisteredPromptWindow(title, message);
                if (owner != null)
                {
                    window.Owner = owner;
                }

                bool? dialogResult = window.ShowDialog();
                return dialogResult == true;
            }
            finally
            {
                Interlocked.Exchange(ref _ocrUnregisteredPromptVisible, 0);
            }
        }

        public ImageTrainingPromptResult ShowImageTrainingPrompt(string title, string message, DateTime defaultScheduleTime)
        {
            ImageTrainingPromptResult result = new ImageTrainingPromptResult();
            Window owner = GetDialogOwner();
            if (Interlocked.CompareExchange(ref _trainingPromptVisible, 1, 0) != 0)
            {
                BringWindowToFront(owner);
                return result;
            }

            try
            {
                BringWindowToFront(owner);
                ImageTrainingPromptWindow window = new ImageTrainingPromptWindow(title, message, defaultScheduleTime);
                if (owner != null)
                {
                    window.Owner = owner;
                }

                bool? dialogResult = window.ShowDialog();
                if (dialogResult == true)
                {
                    return window.Result;
                }

                return result;
            }
            finally
            {
                Interlocked.Exchange(ref _trainingPromptVisible, 0);
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
