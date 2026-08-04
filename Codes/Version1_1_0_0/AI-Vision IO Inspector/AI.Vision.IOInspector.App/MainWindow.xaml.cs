using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AI.Vision.IOInspector.App.Controls;
using AI.Vision.IOInspector.App.ViewModels;

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
            AppBootstrapper.AppendStartupTrace(AppContext.BaseDirectory, "MAIN_WINDOW_CONSTRUCTOR_START");
            InitializeComponent();
            AppBootstrapper.AppendStartupTrace(AppContext.BaseDirectory, "MAIN_WINDOW_INITIALIZE_COMPONENT_END");
            DataContext = AppBootstrapper.CreateMainWindowViewModel(AppContext.BaseDirectory);
            AppBootstrapper.AppendStartupTrace(AppContext.BaseDirectory, "MAIN_WINDOW_DATA_CONTEXT_END");
            Loaded += OnMainWindowLoaded;
        }

        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnMainWindowLoaded;
            AppBootstrapper.AppendStartupTrace(AppContext.BaseDirectory, "MAIN_WINDOW_LOADED");

            MainWindowViewModel viewModel = DataContext as MainWindowViewModel;
            if (viewModel != null)
            {
                // 첫 화면을 즉시 표시한 후 시간이 걸릴 수 있는 장치 초기화를 백그라운드에서 수행합니다.
                viewModel.BeginInitialCameraStatusRefresh();
                viewModel.BeginInitialOcrStatusRefresh();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            MainWindowViewModel viewModel = DataContext as MainWindowViewModel;
            if (viewModel != null)
            {
                viewModel.Dispose();
            }

            base.OnClosed(e);
        }

        private void OnSingleCellDataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.C || (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            DataGrid dataGrid = sender as DataGrid;
            if (dataGrid == null)
            {
                return;
            }

            DataGridCellInfo currentCell = dataGrid.CurrentCell;
            if (!currentCell.IsValid || currentCell.Column == null || currentCell.Item == null)
            {
                return;
            }

            Clipboard.SetText(GetCurrentCellText(currentCell));
            e.Handled = true;
        }

        /// <summary>
        /// LibVLC 네이티브 영상 영역의 두 번 클릭을 기준 이미지 팝업 Command로 전달합니다.
        /// HwndHost 내부 HWND는 Border의 WPF MouseBinding을 우회하므로 이 진입점이 필요합니다.
        /// </summary>
        private void OnRtspVideoHostDoubleClick(object sender, RoutedEventArgs e)
        {
            RtspVideoHost videoHost = sender as RtspVideoHost;
            MainWindowViewModel viewModel = DataContext as MainWindowViewModel;
            if (videoHost == null || viewModel == null || viewModel.ShowReferenceImagePopupCommand == null)
            {
                return;
            }

            object slot = videoHost.DataContext;
            if (viewModel.ShowReferenceImagePopupCommand.CanExecute(slot))
            {
                viewModel.ShowReferenceImagePopupCommand.Execute(slot);
                e.Handled = true;
            }
        }

        private static string GetCurrentCellText(DataGridCellInfo cell)
        {
            DataGridBoundColumn boundColumn = cell.Column as DataGridBoundColumn;
            if (boundColumn != null)
            {
                Binding binding = boundColumn.Binding as Binding;
                if (binding != null && binding.Path != null && !string.IsNullOrWhiteSpace(binding.Path.Path))
                {
                    object value = GetPropertyValue(cell.Item, binding.Path.Path);
                    return value == null ? string.Empty : Convert.ToString(value, CultureInfo.CurrentCulture);
                }
            }

            FrameworkElement content = cell.Column.GetCellContent(cell.Item);
            TextBlock textBlock = content as TextBlock;
            if (textBlock != null)
            {
                return textBlock.Text ?? string.Empty;
            }

            TextBox textBox = content as TextBox;
            if (textBox != null)
            {
                return textBox.Text ?? string.Empty;
            }

            return string.Empty;
        }

        private static object GetPropertyValue(object source, string propertyPath)
        {
            object value = source;
            string[] propertyNames = propertyPath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string propertyName in propertyNames)
            {
                if (value == null)
                {
                    return null;
                }

                PropertyInfo property = value.GetType().GetProperty(propertyName);
                if (property == null)
                {
                    return null;
                }

                value = property.GetValue(value, null);
            }

            return value;
        }
    }
}
