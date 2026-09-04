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
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// Thickness 기준 이미지 위의 마우스 좌표를 원본 이미지 픽셀 좌표로 변환합니다.
    /// 실제 치수 계산은 수행하지 않고 사용자가 선택한 두 점과 선 색상만 반환합니다.
    /// </summary>
    public partial class MeasurementPositionWindow : Window
    {
        private readonly MeasurementPositionViewModel _viewModel;

        private BitmapSource _imageSource;

        /// <summary>
        /// 배경으로 쓴 그림이 원본에서 어디부터 잘렸는지입니다.
        ///
        /// <para>
        /// 화면에는 잘라 낸 그림을 보여 줍니다. 검사 화면이 그렇게 보여 주는데 여기만 원본이면
        /// 같은 카메라인데도 다른 그림처럼 보여 어디에 선을 그어야 할지 알기 어렵습니다.
        /// </para>
        ///
        /// <para>
        /// 다만 좌표는 원본 기준으로 남깁니다. 자를 자리는 제품에 따라 달라지므로,
        /// 잘라 낸 그림 기준으로 적어 두면 다음에 자리가 바뀌었을 때 엉뚱한 곳을 가리킵니다.
        /// 그래서 화면에서 읽은 값에는 이 값을 더하고, 그릴 때는 뺍니다.
        /// </para>
        /// </summary>
        private int _cropOffsetX;
        private int _cropOffsetY;
        private string _loadedImagePath;

        public MeasurementPositionWindow(
            IDictionary<ImageViewType, string> imageFilePathByViewType,
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints)
        {
            InitializeComponent();
            _viewModel = new MeasurementPositionViewModel(imageFilePathByViewType, currentPoint, allPoints);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;

            ApplyCurrentImage();
            Loaded += MeasurementPositionWindow_Loaded;
        }

        private void MeasurementPositionWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RedrawLines();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 다른 카메라의 측정부로 옮기면 배경 사진부터 바꿉니다.
            // 옛 사진 위에 새 좌표를 그리면 엉뚱한 자리에 선이 찍힙니다.
            if (string.Equals(e.PropertyName, "CurrentImagePath", StringComparison.Ordinal))
            {
                ApplyCurrentImage();
            }

            RedrawLines();
        }

        /// <summary>
        /// 지금 편집 중인 측정부의 카메라 사진을 배경으로 올립니다.
        /// 같은 사진이면 다시 읽지 않습니다.
        /// </summary>
        private void ApplyCurrentImage()
        {
            string imagePath = _viewModel.CurrentImagePath;
            if (string.IsNullOrWhiteSpace(imagePath) ||
                string.Equals(imagePath, _loadedImagePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                BitmapSource loaded = LoadBitmap(imagePath);
                _imageSource = ApplyCrop(loaded, _viewModel.CurrentViewType);
                _loadedImagePath = imagePath;
                ThicknessImage.Source = _imageSource;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (InvalidDataException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        /// <summary>
        /// 선 항목은 두 점을 차례로 찍습니다. 내경·외경은 눌러서 끌어 네모를 그립니다.
        ///
        /// <para>
        /// 두 방식을 나눈 이유가 있습니다. 선은 시작과 끝을 정확히 찍는 편이 낫고, 네모는
        /// 끌어서 잡는 편이 빠릅니다. 사람이 원을 정확히 그리는 것은 어려우므로 원이 들어가는
        /// 범위만 잡게 합니다.
        /// </para>
        /// </summary>
        private void ImageHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point imagePoint;
            if (!TryConvertToImagePoint(e.GetPosition(ImageHost), out imagePoint))
            {
                return;
            }

            if (_viewModel.IsRectangleShape)
            {
                // 누른 자리를 한 모서리로 잡고 끌기 시작합니다.
                // 반대 모서리는 놓는 자리입니다. 좌상단·우하단 정렬은 저장할 때 맞춥니다.
                _viewModel.CancelCurrentDrawing();
                _viewModel.X1 = imagePoint.X;
                _viewModel.Y1 = imagePoint.Y;
                _viewModel.X2 = imagePoint.X;
                _viewModel.Y2 = imagePoint.Y;
                _isDraggingRectangle = true;
                ImageHost.CaptureMouse();
                RedrawLines();
                return;
            }

            _viewModel.SelectPoint(imagePoint.X, imagePoint.Y);
            RedrawLines();
        }

        private void ImageHost_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingRectangle)
            {
                return;
            }

            Point imagePoint;
            if (!TryConvertToImagePoint(e.GetPosition(ImageHost), out imagePoint))
            {
                return;
            }

            _viewModel.X2 = imagePoint.X;
            _viewModel.Y2 = imagePoint.Y;
            RedrawLines();
        }

        private void ImageHost_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingRectangle)
            {
                return;
            }

            _isDraggingRectangle = false;
            ImageHost.ReleaseMouseCapture();

            Point imagePoint;
            if (TryConvertToImagePoint(e.GetPosition(ImageHost), out imagePoint))
            {
                _viewModel.X2 = imagePoint.X;
                _viewModel.Y2 = imagePoint.Y;
            }

            // 저장은 좌상단·우하단으로 맞춰 둡니다. AI 에 그 순서로 보내기로 했습니다.
            _viewModel.NormalizeRectangleCoordinates();
            RedrawLines();
        }

        private bool _isDraggingRectangle;

        private void ImageHost_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingRectangle)
            {
                _isDraggingRectangle = false;
                ImageHost.ReleaseMouseCapture();
            }

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
            // 옮겨 다니며 다른 측정부에 이미 남긴 것이 있으면, 지금 보고 있는 측정부의 선이
            // 덜 그려졌더라도 닫을 수 있어야 합니다. 막으면 앞서 그린 것까지 갇힙니다.
            if (!_viewModel.ApplyToCurrentPoint() && !_viewModel.HasAppliedAnyPoint)
            {
                MessageBox.Show(
                    "기준 이미지에서 측정부 시작점과 끝점을 모두 선택하세요.",
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

        /// <summary>
        /// 확인으로 닫은 것이 아니면 창을 열기 전 상태로 되돌립니다.
        ///
        /// <para>
        /// 색은 고르는 즉시 측정부에 넣어 목록과 선에 바로 보이게 합니다. 그래서 "취소"는
        /// 물론이고 창 오른쪽 위 X로 닫을 때도 되돌려 놓아야 고른 색이 남지 않습니다.
        /// </para>
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (DialogResult != true)
            {
                _viewModel.RestoreAllPoints();
            }

            base.OnClosed(e);
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
                    // 배경은 지금 카메라의 기준 이미지입니다.
                    // 다른 카메라의 측정부는 좌표계가 달라 엉뚱한 자리에 그려지므로 건너뜁니다.
                    if (point.ViewType != _viewModel.CurrentViewType)
                    {
                        continue;
                    }

                    // 지금 편집 중인 것은 아래에서 굵은 선으로 따로 그립니다.
                    // 번호만 견주면 다른 카메라의 같은 번호까지 함께 사라집니다.
                    if (point.IndexNo == _viewModel.CurrentIndex || !point.HasCoordinates)
                    {
                        continue;
                    }

                    // 항목마다 그리는 모양이 다릅니다. 내경·외경은 네모와 그 안의 원입니다.
                    if (MeasurementItemTypePolicy.IsRectangleShape(point.ItemType))
                    {
                        DrawRectangleWithCircle(
                            point.X1.Value, point.Y1.Value, point.X2.Value, point.Y2.Value,
                            point.LineColor, 2.0);
                    }
                    else
                    {
                        DrawLine(point.X1.Value, point.Y1.Value, point.X2.Value, point.Y2.Value, point.LineColor, 2.5);
                    }
                }
            }

            if (_viewModel.HasStartPoint)
            {
                double endX = _viewModel.X2.HasValue ? _viewModel.X2.Value : _viewModel.X1.Value;
                double endY = _viewModel.Y2.HasValue ? _viewModel.Y2.Value : _viewModel.Y1.Value;

                if (_viewModel.IsRectangleShape)
                {
                    DrawRectangleWithCircle(
                        _viewModel.X1.Value, _viewModel.Y1.Value, endX, endY,
                        _viewModel.LineColor, 3.0);
                }
                else
                {
                    DrawLine(_viewModel.X1.Value, _viewModel.Y1.Value, endX, endY, _viewModel.LineColor, 4.0);
                }

                DrawPoint(_viewModel.X1.Value, _viewModel.Y1.Value, _viewModel.LineColor);
                if (_viewModel.X2.HasValue && _viewModel.Y2.HasValue)
                {
                    DrawPoint(_viewModel.X2.Value, _viewModel.Y2.Value, _viewModel.LineColor);
                }
            }
        }

        /// <summary>
        /// 내경·외경 측정부를 그립니다. 지정한 네모와, 그 안에 들어가는 원을 함께 그립니다.
        ///
        /// <para>
        /// 사용자는 원을 직접 그리지 않고 원이 들어가는 네모만 끕니다. 무엇을 재게 되는지
        /// 눈으로 보이도록 네모 안에 원을 함께 그려 줍니다. 네모가 정사각이 아닐 수 있으므로
        /// 원은 짧은 변을 지름으로 삼습니다. AI 에 보내는 좌표는 네모의 좌상단·우하단입니다.
        /// </para>
        /// </summary>
        private void DrawRectangleWithCircle(double x1, double y1, double x2, double y2, string color, double thickness)
        {
            Point topLeft;
            Point bottomRight;
            if (!TryConvertToDisplayPoint(Math.Min(x1, x2), Math.Min(y1, y2), out topLeft) ||
                !TryConvertToDisplayPoint(Math.Max(x1, x2), Math.Max(y1, y2), out bottomRight))
            {
                return;
            }

            double width = bottomRight.X - topLeft.X;
            double height = bottomRight.Y - topLeft.Y;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            System.Windows.Media.Brush brush = ResolveBrush(color);

            System.Windows.Shapes.Rectangle box = new System.Windows.Shapes.Rectangle();
            box.Width = width;
            box.Height = height;
            box.Stroke = brush;
            box.StrokeThickness = thickness;
            box.StrokeDashArray = new DoubleCollection(new double[] { 4, 3 });
            box.SnapsToDevicePixels = true;
            Canvas.SetLeft(box, topLeft.X);
            Canvas.SetTop(box, topLeft.Y);
            OverlayCanvas.Children.Add(box);

            double diameter = Math.Min(width, height);
            Ellipse circle = new Ellipse();
            circle.Width = diameter;
            circle.Height = diameter;
            circle.Stroke = brush;
            circle.StrokeThickness = thickness;
            circle.SnapsToDevicePixels = true;
            Canvas.SetLeft(circle, topLeft.X + (width - diameter) / 2.0);
            Canvas.SetTop(circle, topLeft.Y + (height - diameter) / 2.0);
            OverlayCanvas.Children.Add(circle);
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

            // 화면은 잘라 낸 그림이지만 좌표는 원본 기준으로 남깁니다.
            imagePoint = new Point(imageX + _cropOffsetX, imageY + _cropOffsetY);
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

            // 들어오는 값은 원본 기준이므로 잘라 낸 만큼 빼고 그립니다.
            displayPoint = new Point(
                offsetX + ((imageX - _cropOffsetX) * scale),
                offsetY + ((imageY - _cropOffsetY) * scale));
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

        /// <summary>
        /// 그 카메라의 자를 자리로 배경을 잘라 냅니다. 자리를 모르면 원본을 그대로 씁니다.
        /// </summary>
        private BitmapSource ApplyCrop(BitmapSource source, ImageViewType viewType)
        {
            _cropOffsetX = 0;
            _cropOffsetY = 0;

            if (source == null)
            {
                return null;
            }

            AI.Vision.IOInspector.Vision.Models.CropRegion region =
                AI.Vision.IOInspector.App.Services.CroppedImageSourceFactory.GetRegion(viewType);
            if (region == null || !region.IsValid)
            {
                return source;
            }

            int x = Math.Max(0, region.X);
            int y = Math.Max(0, region.Y);
            if (region.Width <= 0 ||
                region.Height <= 0 ||
                x + region.Width > source.PixelWidth ||
                y + region.Height > source.PixelHeight)
            {
                // 자리가 그림 밖으로 나갑니다. 원본을 그대로 씁니다.
                return source;
            }

            try
            {
                CroppedBitmap cropped = new CroppedBitmap(source, new Int32Rect(x, y, region.Width, region.Height));
                cropped.Freeze();
                _cropOffsetX = x;
                _cropOffsetY = y;
                return cropped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("측정부 배경을 자르지 못했습니다: " + ex.Message);
                return source;
            }
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
