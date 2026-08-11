using System;
using System.Windows;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 기준 이미지 확대 창을 하나만 유지하는 WPF 구현체입니다.
    /// </summary>
    public class WpfReferenceImagePopupService : IReferenceImagePopupService
    {
        private ReferenceImagePopupWindow _window;
        private bool _windowClosed;

        public void Show(Part part, ImageViewType selectedViewType)
        {
            EnsureWindow();
            _window.SetPart(part, selectedViewType);
            if (!_window.IsVisible)
            {
                try
                {
                    _window.Show();
                }
                catch (InvalidOperationException)
                {
                    // WPF 창은 Close 후 다시 Show할 수 없습니다. 닫힌 창 참조가 남은 경우 새 창을 만듭니다.
                    ReleaseWindow();
                    EnsureWindow();
                    _window.SetPart(part, selectedViewType);
                    _window.Show();
                }
            }

            _window.Activate();
        }

        public void Update(Part part)
        {
            if (_window == null || !_window.IsVisible)
            {
                return;
            }

            _window.SetPart(part, null);
        }

        public void Close()
        {
            if (_window != null)
            {
                _window.Close();
                ReleaseWindow();
            }
        }

        private void EnsureWindow()
        {
            if (_window != null && !_windowClosed)
            {
                return;
            }

            _window = new ReferenceImagePopupWindow();
            _windowClosed = false;
            Window mainWindow = System.Windows.Application.Current == null
                ? null
                : System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow != _window)
            {
                _window.Owner = mainWindow;
            }

            _window.Closing += OnWindowClosing;
            _window.Closed += OnWindowClosed;
        }

        /// <summary>
        /// WPF Window은 Closing과 Closed 사이에도 IsVisible이 true일 수 있습니다.
        /// 이 구간에 사용자가 바로 다시 두 번 클릭하면 닫히는 창을 재사용하지 않도록 참조를 먼저 해제합니다.
        /// </summary>
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ReferenceImagePopupWindow closingWindow = sender as ReferenceImagePopupWindow;
            if (ReferenceEquals(_window, closingWindow))
            {
                _windowClosed = true;
                _window = null;
            }
        }

        private void OnWindowClosed(object sender, System.EventArgs e)
        {
            ReferenceImagePopupWindow closedWindow = sender as ReferenceImagePopupWindow;
            Window ownerWindow = closedWindow == null ? null : closedWindow.Owner;
            if (closedWindow != null)
            {
                closedWindow.Closing -= OnWindowClosing;
                closedWindow.Closed -= OnWindowClosed;
            }

            if (_window == null || ReferenceEquals(_window, closedWindow))
            {
                _windowClosed = true;
                _window = null;
            }

            RestoreOwnerFocus(ownerWindow);
        }

        /// <summary>
        /// 닫힌 창을 재사용하지 않도록 참조와 이벤트를 정리합니다.
        /// </summary>
        private void ReleaseWindow()
        {
            if (_window != null)
            {
                _window.Closing -= OnWindowClosing;
                _window.Closed -= OnWindowClosed;
            }

            _window = null;
            _windowClosed = true;
        }

        /// <summary>
        /// 모델리스 팝업을 닫은 직후 첫 마우스 입력이 메인 창 활성화에만 소비되지 않도록
        /// 소유자 창을 명시적으로 다시 활성화합니다.
        /// </summary>
        private void RestoreOwnerFocus(Window ownerWindow)
        {
            if (ownerWindow == null || !ownerWindow.IsVisible)
            {
                return;
            }

            ownerWindow.Activate();
            ownerWindow.Focus();
        }
    }
}
