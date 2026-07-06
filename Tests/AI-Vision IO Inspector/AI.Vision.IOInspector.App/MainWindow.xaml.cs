using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

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
