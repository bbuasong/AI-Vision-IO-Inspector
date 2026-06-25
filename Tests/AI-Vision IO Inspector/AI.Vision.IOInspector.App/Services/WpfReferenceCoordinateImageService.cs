using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AI.Vision.IOInspector.App.ViewModels;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// Thickness 원본 이미지 위에 현재 등록된 모든 측정부 선을 그려 품번_coordinate.png로 저장합니다.
    /// 좌표는 원본 이미지 픽셀 좌표를 그대로 사용하며 실제 치수 계산에는 관여하지 않습니다.
    /// </summary>
    public class WpfReferenceCoordinateImageService : IReferenceCoordinateImageService
    {
        public void SaveCoordinateImage(
            string thicknessImagePath,
            string outputFilePath,
            IList<MeasurementPointViewModel> measurementPoints)
        {
            if (string.IsNullOrWhiteSpace(thicknessImagePath) || !File.Exists(thicknessImagePath))
            {
                throw new FileNotFoundException(
                    "coordinate 이미지를 생성할 Thickness 기준 이미지를 찾을 수 없습니다.",
                    thicknessImagePath);
            }

            BitmapSource source = LoadBitmap(thicknessImagePath);
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                drawingContext.DrawImage(
                    source,
                    new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                DrawMeasurementLines(drawingContext, measurementPoints);
            }

            RenderTargetBitmap renderedImage = new RenderTargetBitmap(
                source.PixelWidth,
                source.PixelHeight,
                96,
                96,
                PixelFormats.Pbgra32);
            renderedImage.Render(visual);
            renderedImage.Freeze();

            string directoryPath = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderedImage));
            using (FileStream stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                encoder.Save(stream);
            }
        }

        private void DrawMeasurementLines(
            DrawingContext drawingContext,
            IList<MeasurementPointViewModel> measurementPoints)
        {
            if (measurementPoints == null)
            {
                return;
            }

            foreach (MeasurementPointViewModel point in measurementPoints)
            {
                if (point == null || !point.HasCoordinates)
                {
                    continue;
                }

                Brush brush = ResolveBrush(point.LineColor);
                Pen pen = new Pen(brush, 4.0);
                if (pen.CanFreeze)
                {
                    pen.Freeze();
                }

                Point start = new Point(point.X1.Value, point.Y1.Value);
                Point end = new Point(point.X2.Value, point.Y2.Value);
                drawingContext.DrawLine(pen, start, end);
                drawingContext.DrawEllipse(brush, null, start, 6, 6);
                drawingContext.DrawEllipse(brush, null, end, 6, 6);
            }
        }

        private BitmapSource LoadBitmap(string filePath)
        {
            using (FileStream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache,
                    BitmapCacheOption.OnLoad);
                if (decoder.Frames == null || decoder.Frames.Count == 0)
                {
                    throw new InvalidDataException("Thickness 이미지에서 표시 가능한 프레임을 찾을 수 없습니다.");
                }

                BitmapFrame frame = decoder.Frames[0];
                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                return frame;
            }
        }

        private Brush ResolveBrush(string colorText)
        {
            try
            {
                object converted = ColorConverter.ConvertFromString(colorText);
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
    }
}
