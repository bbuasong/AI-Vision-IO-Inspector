using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 검사 촬영 이미지의 복사본에 판정 결과를 기록합니다.
    ///
    /// 촬영 화면을 가리지 않도록 결과 문자는 이미지 위가 아니라 <b>이미지 아래에 덧붙인 영역</b>에 적습니다.
    /// 이미지 위에는 측정부가 있을 때만 측정 선과 번호를 얇게 표시합니다.
    /// 좌표는 등록 시 저장한 원본 픽셀 좌표를 그대로 사용하므로 별도 변환이 없습니다.
    ///
    /// 원본 이미지는 수정하지 않습니다. 결과 문자를 넣으려면 아래에 영역을 덧붙여야 해서
    /// 세로 크기가 달라지는데, coordinate 이미지와 Thickness 이미지는 해상도가 같아야 하고
    /// 6방향 병합도 원본 크기를 전제로 하기 때문입니다.
    /// </summary>
    public class WpfInspectionMeasurementImageService : IInspectionMeasurementImageService
    {
        private static readonly Color BandBackgroundColor = Color.FromRgb(0x0A, 0x10, 0x16);
        private static readonly Color BandTextColor = Colors.White;
        private static readonly Color PassColor = Color.FromRgb(0x4C, 0xC3, 0x8A);
        private static readonly Color FailColor = Color.FromRgb(0xE5, 0x5B, 0x5B);
        private static readonly Color WarnColor = Color.FromRgb(0xE8, 0xB3, 0x39);
        private const string FontFamilyName = "Malgun Gothic";

        public string CreateResultImage(
            string sourceImagePath,
            string outputFilePath,
            ImageViewType viewType,
            InspectionImageResultInfo resultInfo,
            IList<MeasurementRegion> regions,
            IList<MeasurementResult> results)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) ||
                !File.Exists(sourceImagePath) ||
                string.IsNullOrWhiteSpace(outputFilePath) ||
                resultInfo == null)
            {
                return string.Empty;
            }

            BitmapSource source = LoadBitmap(sourceImagePath);
            List<MeasurementLine> lines = BuildMeasurementLines(regions, results);

            // 글자 크기는 이미지 폭에 비례시켜, 2592px 같은 큰 이미지에서도 축소 표시 시 읽히게 합니다.
            double fontSize = Math.Max(14.0, source.PixelWidth / 70.0);
            double rowHeight = fontSize * 1.6;
            double bandPadding = fontSize * 0.8;

            // 헤더 2줄(방향/품번/판정, Score/치수) 다음에 측정부 줄이 이어집니다.
            const int HeaderRowCount = 2;
            double bandHeight = bandPadding * 2 + rowHeight * (HeaderRowCount + lines.Count);
            int totalHeight = source.PixelHeight + (int)Math.Ceiling(bandHeight);

            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext drawingContext = visual.RenderOpen())
            {
                drawingContext.DrawImage(
                    source,
                    new Rect(0, 0, source.PixelWidth, source.PixelHeight));
                DrawMeasurementMarkers(drawingContext, lines, fontSize);
                DrawResultBand(
                    drawingContext,
                    viewType,
                    resultInfo,
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

            string directoryPath = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            SavePng(renderedImage, outputFilePath);
            return outputFilePath;
        }

        /// <summary>
        /// 이미지 아래 영역에 방향, 품번, 판정, Score, 치수, 측정부 결과를 차례로 적습니다.
        /// </summary>
        private void DrawResultBand(
            DrawingContext drawingContext,
            ImageViewType viewType,
            InspectionImageResultInfo resultInfo,
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

            double left = bandArea.X + bandPadding;
            double y = bandArea.Y + bandPadding;

            // 1행: 방향과 품번/품명/검사시각, 오른쪽 끝에 최종 판정
            FormattedText headerText = CreateText(
                InspectionImageFileNamePolicy.BuildViewPrefix(viewType) + " " + BuildPartText(resultInfo),
                fontSize,
                textBrush,
                FontWeights.Bold);
            drawingContext.DrawText(headerText, new Point(left, y));

            SolidColorBrush judgeBrush = new SolidColorBrush(resultInfo.IsPass ? PassColor : FailColor);
            judgeBrush.Freeze();
            FormattedText judgeText = CreateText(
                resultInfo.IsPass ? "PASS" : "FAIL",
                fontSize * 1.15,
                judgeBrush,
                FontWeights.Bold);
            drawingContext.DrawText(
                judgeText,
                new Point(bandArea.Right - bandPadding - judgeText.Width, y));
            y += rowHeight;

            // 2행: Score와 치수. 검정 이미지로 대체한 방향이면 그 사실을 오른쪽에 덧붙입니다.
            FormattedText detailText = CreateText(
                BuildScoreAndDimensionText(resultInfo),
                fontSize,
                textBrush,
                FontWeights.SemiBold);
            drawingContext.DrawText(detailText, new Point(left, y));

            if (resultInfo.IsPlaceholder)
            {
                SolidColorBrush warnBrush = new SolidColorBrush(WarnColor);
                warnBrush.Freeze();
                FormattedText warnText = CreateText(
                    "카메라 미수신 - 검정 이미지",
                    fontSize,
                    warnBrush,
                    FontWeights.Bold);
                drawingContext.DrawText(
                    warnText,
                    new Point(bandArea.Right - bandPadding - warnText.Width, y));
            }

            y += rowHeight;

            // 3행 이후: 측정부별 결과
            foreach (MeasurementLine line in lines)
            {
                FormattedText rowText = CreateText(
                    BuildRowText(line),
                    fontSize,
                    textBrush,
                    FontWeights.SemiBold);
                drawingContext.DrawText(rowText, new Point(left, y));

                SolidColorBrush rowJudgeBrush = new SolidColorBrush(line.IsPass ? PassColor : FailColor);
                rowJudgeBrush.Freeze();
                FormattedText rowJudgeText = CreateText(
                    line.IsPass ? "PASS" : "FAIL",
                    fontSize,
                    rowJudgeBrush,
                    FontWeights.Bold);
                drawingContext.DrawText(
                    rowJudgeText,
                    new Point(bandArea.Right - bandPadding - rowJudgeText.Width, y));

                y += rowHeight;
            }
        }

        private string BuildPartText(InspectionImageResultInfo resultInfo)
        {
            string partNo = string.IsNullOrWhiteSpace(resultInfo.PartNo) ? "-" : resultInfo.PartNo;
            string partName = string.IsNullOrWhiteSpace(resultInfo.PartName) ? string.Empty : "  " + resultInfo.PartName;
            string inspectedAt = resultInfo.InspectionStartedAt == DateTime.MinValue
                ? string.Empty
                : "   " + resultInfo.InspectionStartedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            return partNo + partName + inspectedAt;
        }

        /// <summary>
        /// Score와 W/D/H를 한 줄로 만듭니다. 값이 없는 항목은 표시하지 않습니다.
        /// </summary>
        private string BuildScoreAndDimensionText(InspectionImageResultInfo resultInfo)
        {
            List<string> parts = new List<string>();

            if (resultInfo.HasScore)
            {
                parts.Add("Score " + FormatValue(resultInfo.Score) + " / " + FormatValue(resultInfo.ScoreThreshold));
            }

            if (resultInfo.HasDimensions)
            {
                string unit = string.IsNullOrWhiteSpace(resultInfo.DimensionUnit) ? "mm" : resultInfo.DimensionUnit;
                List<string> dimensions = new List<string>();
                if (resultInfo.DimensionWidth.HasValue)
                {
                    dimensions.Add("W " + FormatValue(resultInfo.DimensionWidth.Value) + unit);
                }

                if (resultInfo.DimensionDepth.HasValue)
                {
                    dimensions.Add("D " + FormatValue(resultInfo.DimensionDepth.Value) + unit);
                }

                if (resultInfo.DimensionHeight.HasValue)
                {
                    dimensions.Add("H " + FormatValue(resultInfo.DimensionHeight.Value) + unit);
                }

                parts.Add(string.Join("   ", dimensions.ToArray()));
            }

            return parts.Count == 0
                ? "AI Score와 치수 정보가 없습니다."
                : string.Join("      ", parts.ToArray());
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
            if (results == null)
            {
                return lines;
            }

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
                line.IsPass = result.IsPass;
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

                Brush brush = ResolveBrush(region.LineColor, line.IsPass);
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

        private string BuildRowText(MeasurementLine line)
        {
            string name = string.IsNullOrWhiteSpace(line.Name) ? string.Empty : " " + line.Name;
            return line.IndexNo.ToString(CultureInfo.InvariantCulture) + ")" +
                   name +
                   "  측정 " + FormatValue(line.MeasuredValue) + line.Unit +
                   "   기준 " + FormatValue(line.NominalValue) + line.Unit +
                   " (-" + FormatValue(Math.Abs(line.ToleranceMin)) + " ~ +" + FormatValue(Math.Abs(line.ToleranceMax)) + ")";
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

        private Brush ResolveBrush(string colorText, bool isPass)
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

            SolidColorBrush judgeBrush = new SolidColorBrush(isPass ? PassColor : FailColor);
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
                    throw new InvalidDataException("결과 기록 대상 이미지에서 표시 가능한 프레임을 찾을 수 없습니다.");
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
            public bool IsPass { get; set; }
            public MeasurementRegion Region { get; set; }
        }
    }
}
