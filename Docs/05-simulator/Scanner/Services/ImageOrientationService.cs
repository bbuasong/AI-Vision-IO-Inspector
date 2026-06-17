using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScannerSample.Services
{
    /// <summary>
    /// OCR 방향 판정용 후보 이미지를 만들고, 최종 정방향 PNG 파일을 저장합니다.
    /// </summary>
    public class ImageOrientationService
    {
        private const int OcrMaxDimension = 2400;
        private const int BrightPixelThreshold = 150;
        private const int MinimumWhiteChannelValue = 130;
        private const int MaxWhiteColorSpread = 85;
        private const int DarkContentThreshold = 145;
        private const double WhitePageRatioThreshold = 0.55;
        private const double MinimumBrightAreaRatio = 0.04;
        private const double MaximumBrightAreaRatio = 0.97;
        private const double MinimumDarkAreaRatio = 0.001;
        private const double MaximumDarkAreaRatio = 0.65;

        private readonly string _scanFolderPath;
        private readonly string _tempFolderPath;

        public ImageOrientationService()
        {
            _scanFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scans");
            _tempFolderPath = Path.Combine(_scanFolderPath, "Temp");
        }

        public string CreateOcrCandidateImage(string sourceImagePath, int rotationAngle)
        {
            Directory.CreateDirectory(_tempFolderPath);

            string fileName = "ocr_" + rotationAngle.ToString() + "_" + Guid.NewGuid().ToString("N") + ".png";
            string targetPath = Path.Combine(_tempFolderPath, fileName);
            SavePreparedPng(sourceImagePath, targetPath, rotationAngle, OcrMaxDimension);
            return targetPath;
        }

        public string SaveFinalUprightImage(string sourceImagePath, int rotationAngle)
        {
            Directory.CreateDirectory(_scanFolderPath);

            string fileName = "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_upright.png";
            string targetPath = Path.Combine(_scanFolderPath, fileName);
            SavePreparedPng(sourceImagePath, targetPath, rotationAngle, 0);
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

        private void SavePreparedPng(string sourceImagePath, string targetPath, int rotationAngle, int maxDimension)
        {
            BitmapSource source = LoadBitmapSource(sourceImagePath);
            BitmapSource rotated = RotateBitmap(source, rotationAngle);
            BitmapSource validArea = CropToDetectedValidArea(rotated);
            BitmapSource output = ScaleBitmapIfNeeded(validArea, maxDimension);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));

            using (FileStream stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
            }
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
