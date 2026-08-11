using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 검사 결과 Thickness 이미지에 측정부 위치와 측정값을 그려 넣습니다.
    ///
    /// 촬영 화면을 가리지 않도록 측정 정보 문자는 이미지 위가 아니라 <b>이미지 아래에 덧붙인 영역</b>에 적습니다.
    /// 이미지 위에는 어느 측정부인지 알 수 있도록 측정 선과 번호만 얇게 표시합니다.
    /// 좌표는 등록 시 저장한 원본 픽셀 좌표를 그대로 사용하므로 별도 변환이 없습니다.
    /// </summary>
    public class WpfInspectionMeasurementImageService : IInspectionMeasurementImageService
    {
        private static readonly Color BandBackgroundColor = Color.FromRgb(0x0A, 0x10, 0x16);
        private static readonly Color BandTextColor = Colors.White;
        private static readonly Color PassColor = Color.FromRgb(0x4C, 0xC3, 0x8A);
        private static readonly Color FailColor = Color.FromRgb(0xE5, 0x5B, 0x5B);
        private const string FontFamilyName = "Malgun Gothic";

        public string CreateMeasurementResultImage(
            string thicknessImagePath,
            IList<MeasurementRegion> regions,
            IList<MeasurementResult> results)
        {
            if (string.IsNullOrWhiteSpace(thicknessImagePath) ||
                !File.Exists(thicknessImagePath) ||
                results == null ||
                results.Count == 0)
            {
                return string.Empty;
            }

            BitmapSource source = LoadBitmap(thicknessImagePath);
            List<MeasurementLine> lines = BuildMeasurementLines(regions, results);
            if (lines.Count == 0)
            {
                return string.Empty;
            }

            // 글자 크기는 이미지 폭에 비례시켜, 2592px 같은 큰 이미지에서도 축소 표시 시 읽히게 합니다.
            double fontSize = Math.Max(14.0, source.PixelWidth / 70.0);
            double rowHeight = fontSize * 1.6;
            double bandPadding = fontSize * 0.8;
            double bandHeight = bandPadding * 2 + rowHeight * lines.Count;
            int totalHeight = source.PixelHeight + (int)Math.Ceiling(bandHeight);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                drawingContext.DrawImage(
                    source,
                    new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                DrawMeasurementMarkers(drawingContext, lines, fontSize);
                DrawMeasurementBand(
                    drawingContext,
                    lines,
                    new Rect(0, source.PixelHeight, source.PixelWidth, bandHeight),
                    fontSize,
                    rowHeight,
                    bandPadding);
            }

            RenderTargetBitmap renderedImage = new RenderTargetBitmap(
                source.PixelWidth,
                totalHeight,
                96,
                96,
                PixelFormats.Pbgra32);
            renderedImage.Render(visual);
            renderedImage.Freeze();

            // 원본 Thickness 이미지는 그대로 두고 별도 파일로 저장합니다.
            string outputFilePath = BuildMeasurementImagePath(thicknessImagePath);
            SavePng(renderedImage, outputFilePath);
            return outputFilePath;
        }

        /// <summary>
        /// 원본과 같은 폴더에 "_measured"를 붙인 표시용 이미지 경로를 만듭니다.
        /// 예: 01100-51430_Thickness.png -> 01100-51430_Thickness_measured.png
        /// </summary>
        private string BuildMeasurementImagePath(string thicknessImagePath)
        {
            string directoryPath = Path.GetDirectoryName(thicknessImagePath);
            string fileName = Path.GetFileNameWithoutExtension(thicknessImagePath) + "_measured.png";
            return string.IsNullOrWhiteSpace(directoryPath)
                ? fileName
                : Path.Combine(directoryPath, fileName);
        }

        /// <summary>
        /// 측정부 등록 정보와 측정 결과를 MeasurementRegionId로 연결합니다.
        /// 측정 결과에 대응하는 측정부가 없으면 좌표 없이 문자만 남깁니다.
        /// </summary>
        private List<MeasurementLine> BuildMeasurementLines(
            IList<MeasurementRegion> regions,
            IList<MeasurementResult> results)
        {
            List<MeasurementLine> lines = new List<MeasurementLine>();
            foreach (MeasurementResult result in results)
            {
                if (result == null)
                {
                    continue;
                }

                MeasurementRegion region = FindRegion(regions, result.MeasurementRegionId);
                MeasurementLine line = new MeasurementLine();
                line.IndexNo = region == null ? lines.Count + 1 : region.IndexNo;
                line.Name = string.IsNullOrWhiteSpace(result.Name)
                    ? (region == null ? string.Empty : region.Name)
                    : result.Name;
                line.MeasuredValue = result.MeasuredValue;
                line.NominalValue = result.NominalValue;
                line.ToleranceMin = result.ToleranceMin;
                line.ToleranceMax = result.ToleranceMax;
                line.Unit = string.IsNullOrWhiteSpace(result.Unit) ? "mm" : result.Unit;
                line.IsOk = result.IsOk;
                line.Region = region;
                lines.Add(line);
            }

            lines.Sort(CompareByIndexNo);
            return lines;
        }

        private static int CompareByIndexNo(MeasurementLine left, MeasurementLine right)
        {
            return left.IndexNo.CompareTo(right.IndexNo);
        }

        private MeasurementRegion FindRegion(IList<MeasurementRegion> regions, int measurementRegionId)
        {
            if (regions == null)
            {
                return null;
            }

            foreach (MeasurementRegion region in regions)
            {
                if (region != null && region.Id == measurementRegionId)
                {
                    return region;
                }
            }

            return null;
        }

        /// <summary>
        /// 이미지 위에는 측정 선과 번호만 얇게 그려, 아래 표의 어느 항목인지 알 수 있게 합니다.
        /// </summary>
        private void DrawMeasurementMarkers(DrawingContext drawingContext, List<MeasurementLine> lines, double fontSize)
        {
            foreach (MeasurementLine line in lines)
            {
                MeasurementRegion region = line.Region;
                if (region == null ||
                    !region.X1.HasValue || !region.Y1.HasValue ||
                    !region.X2.HasValue || !region.Y2.HasValue)
                {
                    continue;
                }

                Brush brush = ResolveBrush(region.LineColor, line.IsOk);
                Pen pen = new Pen(brush, Math.Max(2.0, fontSize / 8.0));
                if (pen.CanFreeze)
                {
                    pen.Freeze();
                }

                Point start = new Point(region.X1.Value, region.Y1.Value);
                Point end = new Point(region.X2.Value, region.Y2.Value);
                drawingContext.DrawLine(pen, start, end);

                double markerRadius = Math.Max(4.0, fontSize / 3.0);
                drawingContext.DrawEllipse(brush, null, start, markerRadius, markerRadius);
                drawingContext.DrawEllipse(brush, null, end, markerRadius, markerRadius);

                FormattedText indexText = CreateText(
                    line.IndexNo.ToString(CultureInfo.InvariantCulture),
                    fontSize,
                    Brushes.White,
                    FontWeights.Bold);
                Point labelOrigin = new Point(
                    (start.X + end.X) / 2 - indexText.Width / 2,
                    (start.Y + end.Y) / 2 - indexText.Height - markerRadius);
                DrawTextWithBackground(drawingContext, indexText, labelOrigin, brush, fontSize * 0.25);
            }
        }

        /// <summary>
        /// 이미지 아래에 붙인 영역에 측정부별 번호, 측정값, 기준값, 허용오차, 판정을 적습니다.
        /// </summary>
        private void DrawMeasurementBand(
            DrawingContext drawingContext,
            List<MeasurementLine> lines,
            Rect bandArea,
            double fontSize,
            double rowHeight,
            double bandPadding)
        {
            SolidColorBrush background = new SolidColorBrush(BandBackgroundColor);
            background.Freeze();
            drawingContext.DrawRectangle(background, null, bandArea);

            SolidColorBrush textBrush = new SolidColorBrush(BandTextColor);
            textBrush.Freeze();

            double y = bandArea.Y + bandPadding;
            foreach (MeasurementLine line in lines)
            {
                FormattedText rowText = CreateText(
                    BuildRowText(line),
                    fontSize,
                    textBrush,
                    FontWeights.SemiBold);
                drawingContext.DrawText(rowText, new Point(bandArea.X + bandPadding, y));

                // 판정은 색으로 바로 구분되도록 행 오른쪽 끝에 따로 적습니다.
                SolidColorBrush judgeBrush = new SolidColorBrush(line.IsOk ? PassColor : FailColor);
                judgeBrush.Freeze();
                FormattedText judgeText = CreateText(
                    line.IsOk ? "OK" : "NG",
                    fontSize,
                    judgeBrush,
                    FontWeights.Bold);
                drawingContext.DrawText(
                    judgeText,
                    new Point(bandArea.Right - bandPadding - judgeText.Width, y));

                y += rowHeight;
            }
        }

        private string BuildRowText(MeasurementLine line)
        {
            string name = string.IsNullOrWhiteSpace(line.Name) ? string.Empty : " " + line.Name;
            return line.IndexNo.ToString(CultureInfo.InvariantCulture) + ")" +
                   name +
                   "  측정 " + FormatValue(line.MeasuredValue) + line.Unit +
                   "   기준 " + FormatValue(line.NominalValue) + line.Unit +
                   " (" + FormatSignedValue(line.ToleranceMin) + " ~ " + FormatSignedValue(line.ToleranceMax) + ")";
        }

        private string FormatValue(decimal value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private string FormatSignedValue(decimal value)
        {
            string text = value.ToString("0.00", CultureInfo.InvariantCulture);
            return value > 0m ? "+" + text : text;
        }

        private void DrawTextWithBackground(
            DrawingContext drawingContext,
            FormattedText text,
            Point origin,
            Brush background,
            double padding)
        {
            Rect backgroundArea = new Rect(
                origin.X - padding,
                origin.Y - padding,
                text.Width + padding * 2,
                text.Height + padding * 2);
            drawingContext.DrawRectangle(background, null, backgroundArea);
            drawingContext.DrawText(text, origin);
        }

        private FormattedText CreateText(string text, double fontSize, Brush brush, FontWeight fontWeight)
        {
            // 결과 이미지는 화면 DPI와 무관한 원본 픽셀 좌표로 그리므로 PixelsPerDip은 1.0으로 고정합니다.
            return new FormattedText(
                text ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily(FontFamilyName), FontStyles.Normal, fontWeight, FontStretches.Normal),
                fontSize,
                brush,
                1.0);
        }

        private Brush ResolveBrush(string colorText, bool isOk)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(colorText))
                {
                    object converted = ColorConverter.ConvertFromString(colorText);
                    if (converted is Color)
                    {
                        SolidColorBrush brush = new SolidColorBrush((Color)converted);
                        brush.Freeze();
                        return brush;
                    }
                }
            }
            catch (FormatException)
            {
            }

            SolidColorBrush judgeBrush = new SolidColorBrush(isOk ? PassColor : FailColor);
            judgeBrush.Freeze();
            return judgeBrush;
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
                    throw new InvalidDataException("Thickness 검사 이미지에서 표시 가능한 프레임을 찾을 수 없습니다.");
                }

                BitmapFrame frame = decoder.Frames[0];
                if (frame.CanFreeze)
                {
                    frame.Freeze();
                }

                return frame;
            }
        }

        private void SavePng(BitmapSource image, string outputFilePath)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (FileStream stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                encoder.Save(stream);
            }
        }

        private sealed class MeasurementLine
        {
            public int IndexNo { get; set; }
            public string Name { get; set; }
            public decimal MeasuredValue { get; set; }
            public decimal NominalValue { get; set; }
            public decimal ToleranceMin { get; set; }
            public decimal ToleranceMax { get; set; }
            public string Unit { get; set; }
            public bool IsOk { get; set; }
            public MeasurementRegion Region { get; set; }
        }
    }
}
