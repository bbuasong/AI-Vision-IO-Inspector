using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScannerSample.Services.ImageProcessing
{
    /// <summary>
    /// OCR 방향 판정용 후보 이미지를 만들고, 최종 정방향 PNG 파일을 저장합니다.
    /// </summary>
    public class ImageOrientationService
    {
        private const int OcrMaxDimension = 2400;
        private const int BrightPixelThreshold = 120;
        private const int MinimumWhiteChannelValue = 105;
        private const int MaxWhiteColorSpread = 115;
        private const int DarkContentThreshold = 145;
        private const double OcrScaleFactor = 3.0;
        private const double WhitePageRatioThreshold = 0.55;
        private const double MinimumBrightAreaRatio = 0.01;
        private const double MaximumBrightAreaRatio = 0.97;
        private const double MinimumDarkAreaRatio = 0.001;
        private const double MaximumDarkAreaRatio = 0.65;

        private readonly string _scanFolderPath;
        private readonly string _tempFolderPath;
        private readonly string _labelFolderPath;
        private readonly string _partNumberFolderPath;
        private readonly string _ocrInputFolderPath;
        private readonly string _engineInputRootFolderPath;

        public ImageOrientationService()
        {
            _scanFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scans");
            _tempFolderPath = Path.Combine(_scanFolderPath, "Temp");
            _labelFolderPath = Path.Combine(_scanFolderPath, "Processed", "Label");
            _partNumberFolderPath = Path.Combine(_scanFolderPath, "Processed", "PartNo");
            _ocrInputFolderPath = Path.Combine(_scanFolderPath, "Processed", "OcrInput", "Enhanced");
            _engineInputRootFolderPath = Path.Combine(_scanFolderPath, "ApiInput");
        }

        public string CreateOcrCandidateImage(string sourceImagePath, int rotationAngle)
        {
            Directory.CreateDirectory(_tempFolderPath);

            string fileName = "ocr_" + rotationAngle.ToString() + "_" + Guid.NewGuid().ToString("N") + ".png";
            string targetPath = Path.Combine(_tempFolderPath, fileName);
            SavePreparedPng(sourceImagePath, targetPath, rotationAngle, false, OcrMaxDimension);
            return targetPath;
        }

        public string CreatePartNumberOcrCandidateImage(string sourceImagePath, int rotationAngle)
        {
            Directory.CreateDirectory(_tempFolderPath);

            string fileName = "partno_" + rotationAngle.ToString() + "_" + Guid.NewGuid().ToString("N") + ".png";
            string targetPath = Path.Combine(_tempFolderPath, fileName);
            SavePreparedPng(sourceImagePath, targetPath, rotationAngle, true, OcrMaxDimension);
            return targetPath;
        }

        public string SaveFinalUprightImage(string sourceImagePath, int rotationAngle)
        {
            return SaveUprightLabelImage(sourceImagePath, rotationAngle);
        }

        public string SaveUprightLabelImage(string sourceImagePath, int rotationAngle)
        {
            Directory.CreateDirectory(_scanFolderPath);
            Directory.CreateDirectory(_labelFolderPath);

            string fileName = "label_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_upright.png";
            string targetPath = Path.Combine(_labelFolderPath, fileName);
            SavePreparedPng(sourceImagePath, targetPath, rotationAngle, false, 0);
            return targetPath;
        }

        public string SavePartNumberAreaImage(string uprightLabelImagePath)
        {
            Directory.CreateDirectory(_scanFolderPath);
            Directory.CreateDirectory(_partNumberFolderPath);

            string fileName = "partno_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
            string targetPath = Path.Combine(_partNumberFolderPath, fileName);
            BitmapSource source = LoadBitmapSource(uprightLabelImagePath);
            BitmapSource partNumberArea = CropPartNumberArea(source);
            SaveBitmapAsPng(partNumberArea, targetPath);
            return targetPath;
        }

        public string SaveEnhancedOcrInputImage(string croppedImagePath)
        {
            Directory.CreateDirectory(_scanFolderPath);
            Directory.CreateDirectory(_ocrInputFolderPath);

            string fileName = "ocr_input_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
            string targetPath = Path.Combine(_ocrInputFolderPath, fileName);
            BitmapSource source = LoadBitmapSource(croppedImagePath);
            BitmapSource scaled = ScaleBitmapByFactor(source, OcrScaleFactor);
            BitmapSource enhanced = CreateBinaryContrastBitmap(scaled);
            SaveBitmapAsPng(enhanced, targetPath);
            return targetPath;
        }

        public string SaveEngineInputImage(string sourceImagePath, string engineFolderName, string stageName)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
            {
                return sourceImagePath;
            }

            string engineSegment = SanitizePathSegment(engineFolderName);
            string stageSegment = SanitizePathSegment(stageName);
            string targetFolderPath = Path.Combine(_engineInputRootFolderPath, engineSegment, stageSegment);
            Directory.CreateDirectory(targetFolderPath);

            string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Path.GetFileName(sourceImagePath);
            string targetPath = Path.Combine(targetFolderPath, fileName);
            File.Copy(sourceImagePath, targetPath, true);
            return targetPath;
        }

        public void DeleteQuietly(string filePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch
            {
            }
        }

        private void SavePreparedPng(string sourceImagePath, string targetPath, int rotationAngle, bool cropPartNumberArea, int maxDimension)
        {
            BitmapSource source = LoadBitmapSource(sourceImagePath);
            BitmapSource rotated = RotateBitmap(source, rotationAngle);
            BitmapSource validArea = CropToDetectedValidArea(rotated);
            BitmapSource prepared = cropPartNumberArea ? CropPartNumberArea(validArea) : validArea;
            BitmapSource output = ScaleBitmapIfNeeded(prepared, maxDimension);

            SaveBitmapAsPng(output, targetPath);
        }

        private void SaveBitmapAsPng(BitmapSource source, string targetPath)
        {
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (FileStream stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
            }
        }

        private BitmapSource CropPartNumberArea(BitmapSource source)
        {
            source = CropToLikelyBrightLabelArea(source);

            int width = source.PixelWidth;
            int height = source.PixelHeight;

            if (width < 80 || height < 40)
            {
                return source;
            }

            // 정방향 라벨에서는 검수 박스가 왼쪽에 있고, 품번은 그 오른쪽 상단에서 괄호 앞까지 위치합니다.
            int cropX = (int)(width * 0.08);
            int cropY = (int)(height * 0.10);
            int cropWidth = (int)(width * 0.65);
            int cropHeight = (int)(height * 0.30);

            cropWidth = Math.Min(cropWidth, width - cropX);
            cropHeight = Math.Min(cropHeight, height - cropY);
            return CropBitmap(source, cropX, cropY, cropWidth, cropHeight);
        }

        private string SanitizePathSegment(string value)
        {
            string segment = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                segment = segment.Replace(invalidChar, '_');
            }

            segment = segment.Replace(' ', '_');
            return string.IsNullOrWhiteSpace(segment) ? "Unknown" : segment;
        }

        private BitmapSource CropToLikelyBrightLabelArea(BitmapSource source)
        {
            BitmapSource readable = ConvertToBgra32(source);
            int width = readable.PixelWidth;
            int height = readable.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            readable.CopyPixels(pixels, stride, 0);

            int[] rowCounts = new int[height];
            int[] columnCounts = new int[width];

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int index = row + (x * 4);
                    byte blue = pixels[index];
                    byte green = pixels[index + 1];
                    byte red = pixels[index + 2];
                    byte alpha = pixels[index + 3];

                    if (!IsProjectedLabelPixel(red, green, blue, alpha))
                    {
                        continue;
                    }

                    rowCounts[y]++;
                    columnCounts[x]++;
                }
            }

            int minimumRowPixels = Math.Max(10, width / 8);
            int minimumColumnPixels = Math.Max(10, height / 12);
            int minY = FindFirstIndexOverThreshold(rowCounts, minimumRowPixels);
            int maxY = FindLastIndexOverThreshold(rowCounts, minimumRowPixels);
            int minX = FindFirstIndexOverThreshold(columnCounts, minimumColumnPixels);
            int maxX = FindLastIndexOverThreshold(columnCounts, minimumColumnPixels);

            return CropByBounds(readable, minX, minY, maxX, maxY, 0.02, 0.90, 0.01, 4);
        }

        private BitmapSource LoadBitmapSource(string imageFilePath)
        {
            BitmapFrame frame = BitmapFrame.Create(new Uri(imageFilePath, UriKind.Absolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            int orientationAngle = ReadExifOrientationAngle(frame.Metadata as BitmapMetadata);

            if (frame.CanFreeze)
            {
                frame.Freeze();
            }

            return RotateBitmap(frame, orientationAngle);
        }

        private int ReadExifOrientationAngle(BitmapMetadata metadata)
        {
            if (metadata == null)
            {
                return 0;
            }

            try
            {
                object orientationValue = metadata.GetQuery("/app1/ifd/{ushort=274}");
                if (orientationValue == null)
                {
                    return 0;
                }

                int orientation = Convert.ToInt32(orientationValue);
                if (orientation == 3)
                {
                    return 180;
                }

                if (orientation == 6)
                {
                    return 90;
                }

                if (orientation == 8)
                {
                    return 270;
                }
            }
            catch
            {
            }

            return 0;
        }

        private BitmapSource RotateBitmap(BitmapSource source, int rotationAngle)
        {
            if (rotationAngle == 0)
            {
                return source;
            }

            TransformedBitmap rotated = new TransformedBitmap();
            rotated.BeginInit();
            rotated.Source = source;
            rotated.Transform = new RotateTransform(rotationAngle);
            rotated.EndInit();
            rotated.Freeze();
            return rotated;
        }

        private BitmapSource CropToDetectedValidArea(BitmapSource source)
        {
            BitmapSource readable = ConvertToBgra32(source);
            int width = readable.PixelWidth;
            int height = readable.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            readable.CopyPixels(pixels, stride, 0);

            double whiteRatio = CalculateWhitePageRatio(pixels, width, height, stride);
            if (whiteRatio >= WhitePageRatioThreshold)
            {
                return CropByDarkContent(readable, pixels, width, height, stride);
            }

            return CropByBrightLabel(readable, pixels, width, height, stride);
        }

        private double CalculateWhitePageRatio(byte[] pixels, int width, int height, int stride)
        {
            int whiteCount = 0;
            int totalCount = width * height;

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int index = row + (x * 4);
                    byte blue = pixels[index];
                    byte green = pixels[index + 1];
                    byte red = pixels[index + 2];
                    byte alpha = pixels[index + 3];

                    if (IsWhitePagePixel(red, green, blue, alpha))
                    {
                        whiteCount++;
                    }
                }
            }

            return (double)whiteCount / (double)totalCount;
        }

        private BitmapSource CropByDarkContent(BitmapSource source, byte[] pixels, int width, int height, int stride)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int index = row + (x * 4);
                    byte blue = pixels[index];
                    byte green = pixels[index + 1];
                    byte red = pixels[index + 2];
                    byte alpha = pixels[index + 3];

                    if (!IsDarkContentPixel(red, green, blue, alpha))
                    {
                        continue;
                    }

                    UpdateBounds(x, y, ref minX, ref minY, ref maxX, ref maxY);
                }
            }

            BitmapSource cropped = CropByBounds(source, minX, minY, maxX, maxY, MinimumDarkAreaRatio, MaximumDarkAreaRatio, 0.02, 8);
            return TrimOuterNearWhiteBorder(cropped);
        }

        private BitmapSource CropByBrightLabel(BitmapSource source, byte[] pixels, int width, int height, int stride)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int index = row + (x * 4);
                    byte blue = pixels[index];
                    byte green = pixels[index + 1];
                    byte red = pixels[index + 2];
                    byte alpha = pixels[index + 3];

                    if (!IsBrightLabelPixel(red, green, blue, alpha))
                    {
                        continue;
                    }

                    UpdateBounds(x, y, ref minX, ref minY, ref maxX, ref maxY);
                }
            }

            return CropByBounds(source, minX, minY, maxX, maxY, MinimumBrightAreaRatio, MaximumBrightAreaRatio, 0.015, 6);
        }

        private BitmapSource CropByBounds(BitmapSource source, int minX, int minY, int maxX, int maxY, double minimumRatio, double maximumRatio, double marginRatio, int minimumMargin)
        {
            if (maxX < minX || maxY < minY)
            {
                return source;
            }

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            double detectedArea = (double)(maxX - minX + 1) * (double)(maxY - minY + 1);
            double sourceArea = (double)width * (double)height;
            double detectedRatio = detectedArea / sourceArea;

            if (detectedRatio < minimumRatio || detectedRatio > maximumRatio)
            {
                return source;
            }

            int margin = Math.Max(minimumMargin, (int)(Math.Min(width, height) * marginRatio));
            int cropX = Math.Max(0, minX - margin);
            int cropY = Math.Max(0, minY - margin);
            int cropRight = Math.Min(width - 1, maxX + margin);
            int cropBottom = Math.Min(height - 1, maxY + margin);
            return CropBitmap(source, cropX, cropY, cropRight - cropX + 1, cropBottom - cropY + 1);
        }

        private void UpdateBounds(int x, int y, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            if (x < minX)
            {
                minX = x;
            }

            if (y < minY)
            {
                minY = y;
            }

            if (x > maxX)
            {
                maxX = x;
            }

            if (y > maxY)
            {
                maxY = y;
            }
        }

        private bool IsWhitePagePixel(byte red, byte green, byte blue, byte alpha)
        {
            return alpha >= 10 && red >= 235 && green >= 235 && blue >= 235;
        }

        private bool IsDarkContentPixel(byte red, byte green, byte blue, byte alpha)
        {
            if (alpha < 10)
            {
                return false;
            }

            int brightness = (red + green + blue) / 3;
            return brightness <= DarkContentThreshold;
        }

        private bool IsBrightLabelPixel(byte red, byte green, byte blue, byte alpha)
        {
            if (alpha < 10)
            {
                return false;
            }

            int max = Math.Max(red, Math.Max(green, blue));
            int min = Math.Min(red, Math.Min(green, blue));
            int brightness = (red + green + blue) / 3;

            return brightness >= BrightPixelThreshold && min >= MinimumWhiteChannelValue && (max - min) <= MaxWhiteColorSpread;
        }

        private int FindFirstIndexOverThreshold(int[] values, int threshold)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] >= threshold)
                {
                    return i;
                }
            }

            return values.Length;
        }

        private int FindLastIndexOverThreshold(int[] values, int threshold)
        {
            for (int i = values.Length - 1; i >= 0; i--)
            {
                if (values[i] >= threshold)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsProjectedLabelPixel(byte red, byte green, byte blue, byte alpha)
        {
            if (alpha < 10)
            {
                return false;
            }

            int max = Math.Max(red, Math.Max(green, blue));
            int min = Math.Min(red, Math.Min(green, blue));

            return red >= 145 && green >= 145 && blue >= 130 && (max - min) <= 100;
        }

        private BitmapSource TrimOuterNearWhiteBorder(BitmapSource source)
        {
            BitmapSource readable = ConvertToBgra32(source);
            int width = readable.PixelWidth;
            int height = readable.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            readable.CopyPixels(pixels, stride, 0);

            int minContentPerRow = Math.Max(2, width / 100);
            int minContentPerColumn = Math.Max(2, height / 100);
            int top = 0;
            int bottom = height - 1;
            int left = 0;
            int right = width - 1;

            while (top < height && CountNonWhitePixelsInRow(pixels, width, stride, top) < minContentPerRow)
            {
                top++;
            }

            while (bottom > top && CountNonWhitePixelsInRow(pixels, width, stride, bottom) < minContentPerRow)
            {
                bottom--;
            }

            while (left < width && CountNonWhitePixelsInColumn(pixels, height, stride, left) < minContentPerColumn)
            {
                left++;
            }

            while (right > left && CountNonWhitePixelsInColumn(pixels, height, stride, right) < minContentPerColumn)
            {
                right--;
            }

            if (right <= left || bottom <= top)
            {
                return source;
            }

            return CropBitmap(readable, left, top, right - left + 1, bottom - top + 1);
        }

        private int CountNonWhitePixelsInRow(byte[] pixels, int width, int stride, int y)
        {
            int count = 0;
            int row = y * stride;
            for (int x = 0; x < width; x++)
            {
                int index = row + (x * 4);
                if (!IsNearWhitePixel(pixels[index + 2], pixels[index + 1], pixels[index], pixels[index + 3]))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountNonWhitePixelsInColumn(byte[] pixels, int height, int stride, int x)
        {
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                int index = (y * stride) + (x * 4);
                if (!IsNearWhitePixel(pixels[index + 2], pixels[index + 1], pixels[index], pixels[index + 3]))
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsNearWhitePixel(byte red, byte green, byte blue, byte alpha)
        {
            return alpha < 10 || (red >= 245 && green >= 245 && blue >= 245);
        }

        private BitmapSource CropBitmap(BitmapSource source, int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return source;
            }

            Int32Rect rectangle = new Int32Rect(x, y, width, height);
            CroppedBitmap cropped = new CroppedBitmap(source, rectangle);
            cropped.Freeze();
            return cropped;
        }

        private BitmapSource ConvertToBgra32(BitmapSource source)
        {
            if (source.Format == PixelFormats.Bgra32)
            {
                return source;
            }

            FormatConvertedBitmap converted = new FormatConvertedBitmap();
            converted.BeginInit();
            converted.Source = source;
            converted.DestinationFormat = PixelFormats.Bgra32;
            converted.EndInit();
            converted.Freeze();
            return converted;
        }

        private BitmapSource CreateBinaryContrastBitmap(BitmapSource source)
        {
            BitmapSource readable = ConvertToBgra32(source);
            int width = readable.PixelWidth;
            int height = readable.PixelHeight;
            int stride = width * 4;
            byte[] sourcePixels = new byte[stride * height];
            byte[] grayPixels = new byte[width * height];
            readable.CopyPixels(sourcePixels, stride, 0);

            int[] histogram = new int[256];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * stride;
                int targetRow = y * width;
                for (int x = 0; x < width; x++)
                {
                    int sourceIndex = sourceRow + (x * 4);
                    byte blue = sourcePixels[sourceIndex];
                    byte green = sourcePixels[sourceIndex + 1];
                    byte red = sourcePixels[sourceIndex + 2];
                    byte gray = (byte)((red * 299 + green * 587 + blue * 114) / 1000);
                    grayPixels[targetRow + x] = gray;
                    histogram[gray]++;
                }
            }

            int threshold = CalculateOtsuThreshold(histogram, grayPixels.Length);
            byte[] binaryPixels = new byte[grayPixels.Length];
            for (int i = 0; i < grayPixels.Length; i++)
            {
                binaryPixels[i] = grayPixels[i] <= threshold ? (byte)0 : (byte)255;
            }

            BitmapSource binary = BitmapSource.Create(width, height, readable.DpiX, readable.DpiY, PixelFormats.Gray8, null, binaryPixels, width);
            binary.Freeze();
            return binary;
        }

        private int CalculateOtsuThreshold(int[] histogram, int totalPixelCount)
        {
            double totalSum = 0;
            for (int i = 0; i < histogram.Length; i++)
            {
                totalSum += i * histogram[i];
            }

            double backgroundSum = 0;
            int backgroundWeight = 0;
            int foregroundWeight = 0;
            double maxVariance = 0;
            int threshold = 128;

            for (int i = 0; i < histogram.Length; i++)
            {
                backgroundWeight += histogram[i];
                if (backgroundWeight == 0)
                {
                    continue;
                }

                foregroundWeight = totalPixelCount - backgroundWeight;
                if (foregroundWeight == 0)
                {
                    break;
                }

                backgroundSum += i * histogram[i];
                double backgroundMean = backgroundSum / backgroundWeight;
                double foregroundMean = (totalSum - backgroundSum) / foregroundWeight;
                double variance = backgroundWeight * foregroundWeight * Math.Pow(backgroundMean - foregroundMean, 2);

                if (variance > maxVariance)
                {
                    maxVariance = variance;
                    threshold = i;
                }
            }

            return threshold;
        }

        private BitmapSource ScaleBitmapByFactor(BitmapSource source, double scaleFactor)
        {
            if (scaleFactor <= 1.0)
            {
                return source;
            }

            TransformedBitmap scaled = new TransformedBitmap();
            scaled.BeginInit();
            scaled.Source = source;
            scaled.Transform = new ScaleTransform(scaleFactor, scaleFactor);
            scaled.EndInit();
            scaled.Freeze();
            return scaled;
        }

        private BitmapSource ScaleBitmapIfNeeded(BitmapSource source, int maxDimension)
        {
            if (maxDimension <= 0)
            {
                return source;
            }

            int currentMax = Math.Max(source.PixelWidth, source.PixelHeight);
            if (currentMax <= maxDimension)
            {
                return source;
            }

            double scale = (double)maxDimension / (double)currentMax;
            TransformedBitmap scaled = new TransformedBitmap();
            scaled.BeginInit();
            scaled.Source = source;
            scaled.Transform = new ScaleTransform(scale, scale);
            scaled.EndInit();
            scaled.Freeze();
            return scaled;
        }
    }
}
