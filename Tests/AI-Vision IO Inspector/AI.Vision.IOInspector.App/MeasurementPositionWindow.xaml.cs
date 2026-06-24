using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AI.Vision.IOInspector.App.ViewModels;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// Thickness 기준 이미지 위의 마우스 좌표를 원본 이미지 픽셀 좌표로 변환합니다.
    /// 실제 치수 계산은 수행하지 않고 사용자가 선택한 두 점과 선 색상만 반환합니다.
    /// </summary>
    public partial class MeasurementPositionWindow : Window
    {
        private readonly MeasurementPositionViewModel _viewModel;
        private readonly BitmapSource _imageSource;

        public MeasurementPositionWindow(
            string imageFilePath,
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints)
        {
            InitializeComponent();
            _viewModel = new MeasurementPositionViewModel(currentPoint, allPoints);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;

            _imageSource = LoadBitmap(imageFilePath);
            ThicknessImage.Source = _imageSource;
            Loaded += MeasurementPositionWindow_Loaded;
        }

        private void MeasurementPositionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RedrawLines();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RedrawLines();
        }

        private void ImageHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point imagePoint;
            if (!TryConvertToImagePoint(e.GetPosition(ImageHost), out imagePoint))
            {
                return;
            }

            _viewModel.SelectPoint(imagePoint.X, imagePoint.Y);
            RedrawLines();
        }

        private void ImageHost_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel.CancelCurrentDrawing();
            RedrawLines();
            e.Handled = true;
        }

        private void ImageHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawLines();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.ApplyToCurrentPoint())
            {
                MessageBox.Show(
                    "Thickness 이미지에서 측정부 시작점과 끝점을 모두 선택하세요.",
                    "측정부 위치 미지정",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void RedrawLines()
        {
            if (OverlayCanvas == null || _imageSource == null)
            {
                return;
            }

            OverlayCanvas.Children.Clear();
            if (_viewModel.ShowAllPoints)
            {
                foreach (MeasurementPointViewModel point in _viewModel.AllPoints)
                {
                    if (point.IndexNo == _viewModel.CurrentIndex || !point.HasCoordinates)
                    {
                        continue;
                    }

                    DrawLine(point.X1.Value, point.Y1.Value, point.X2.Value, point.Y2.Value, point.LineColor, 2.5);
                }
            }

            if (_viewModel.HasStartPoint)
            {
                double endX = _viewModel.X2.HasValue ? _viewModel.X2.Value : _viewModel.X1.Value;
                double endY = _viewModel.Y2.HasValue ? _viewModel.Y2.Value : _viewModel.Y1.Value;
                DrawLine(_viewModel.X1.Value, _viewModel.Y1.Value, endX, endY, _viewModel.LineColor, 4.0);
                DrawPoint(_viewModel.X1.Value, _viewModel.Y1.Value, _viewModel.LineColor);
                if (_viewModel.X2.HasValue && _viewModel.Y2.HasValue)
                {
                    DrawPoint(_viewModel.X2.Value, _viewModel.Y2.Value, _viewModel.LineColor);
                }
            }
        }

        private void DrawLine(double x1, double y1, double x2, double y2, string color, double thickness)
        {
            Point start;
            Point end;
            if (!TryConvertToDisplayPoint(x1, y1, out start) || !TryConvertToDisplayPoint(x2, y2, out end))
            {
                return;
            }

            Line line = new Line();
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;
            line.Stroke = ResolveBrush(color);
            line.StrokeThickness = thickness;
            line.SnapsToDevicePixels = true;
            OverlayCanvas.Children.Add(line);
        }

        private void DrawPoint(double x, double y, string color)
        {
            Point displayPoint;
            if (!TryConvertToDisplayPoint(x, y, out displayPoint))
            {
                return;
            }

            Ellipse marker = new Ellipse();
            marker.Width = 12;
            marker.Height = 12;
            marker.Fill = ResolveBrush(color);
            marker.Stroke = Brushes.Black;
            marker.StrokeThickness = 1;
            Canvas.SetLeft(marker, displayPoint.X - 6);
            Canvas.SetTop(marker, displayPoint.Y - 6);
            OverlayCanvas.Children.Add(marker);
        }

        private bool TryConvertToImagePoint(Point displayPoint, out Point imagePoint)
        {
            imagePoint = new Point();
            double scale;
            double offsetX;
            double offsetY;
            if (!TryGetImageTransform(out scale, out offsetX, out offsetY))
            {
                return false;
            }

            double imageX = (displayPoint.X - offsetX) / scale;
            double imageY = (displayPoint.Y - offsetY) / scale;
            if (imageX < 0 || imageY < 0 || imageX > _imageSource.PixelWidth || imageY > _imageSource.PixelHeight)
            {
                return false;
            }

            imagePoint = new Point(imageX, imageY);
            return true;
        }

        private bool TryConvertToDisplayPoint(double imageX, double imageY, out Point displayPoint)
        {
            displayPoint = new Point();
            double scale;
            double offsetX;
            double offsetY;
            if (!TryGetImageTransform(out scale, out offsetX, out offsetY))
            {
                return false;
            }

            displayPoint = new Point(offsetX + (imageX * scale), offsetY + (imageY * scale));
            return true;
        }

        private bool TryGetImageTransform(out double scale, out double offsetX, out double offsetY)
        {
            scale = 0;
            offsetX = 0;
            offsetY = 0;
            if (_imageSource == null || ImageHost.ActualWidth <= 0 || ImageHost.ActualHeight <= 0)
            {
                return false;
            }

            double scaleX = ImageHost.ActualWidth / _imageSource.PixelWidth;
            double scaleY = ImageHost.ActualHeight / _imageSource.PixelHeight;
            scale = Math.Min(scaleX, scaleY);
            double displayedWidth = _imageSource.PixelWidth * scale;
            double displayedHeight = _imageSource.PixelHeight * scale;
            offsetX = (ImageHost.ActualWidth - displayedWidth) / 2.0;
            offsetY = (ImageHost.ActualHeight - displayedHeight) / 2.0;
            return scale > 0;
        }

        private Brush ResolveBrush(string color)
        {
            try
            {
                object converted = ColorConverter.ConvertFromString(color);
                if (converted is Color)
                {
                    SolidColorBrush brush = new SolidColorBrush((Color)converted);
                    brush.Freeze();
                    return brush;
                }
            }
            catch (FormatException)
            {
            }

            return Brushes.DeepSkyBlue;
        }

        private BitmapSource LoadBitmap(string imageFilePath)
        {
            using (FileStream stream = new FileStream(imageFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                // .NET Framework 4.7.2의 BitmapImage는 StreamSource만 지정해도
                // EndInit 내부 이미지 캐시에서 null Uri를 처리하며 ArgumentNullException을 낼 수 있습니다.
                // BitmapDecoder로 프레임을 메모리에 완전히 적재해 파일 잠금과 Uri 캐시를 모두 피합니다.
                BitmapDecoder decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache,
                    BitmapCacheOption.OnLoad);

                if (decoder.Frames == null || decoder.Frames.Count == 0)
                {
                    throw new InvalidDataException("Thickness 이미지에서 표시할 프레임을 찾을 수 없습니다.");
                }

                BitmapFrame frame = decoder.Frames[0];
                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                return frame;
            }
        }
    }
}
