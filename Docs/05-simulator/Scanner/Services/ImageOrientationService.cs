using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScannerSample.Services
{
    /// <summary>
    /// OCR 방향 판별을 위해 이미지를 회전 저장하고, 최종 정방향 PNG 파일을 생성합니다.
    /// </summary>
    public class ImageOrientationService
    {
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
            SaveRotatedPng(sourceImagePath, targetPath, rotationAngle, 2400);
            return targetPath;
        }

        public string SaveFinalUprightImage(string sourceImagePath, int rotationAngle)
        {
            Directory.CreateDirectory(_scanFolderPath);

            string fileName = "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_upright.png";
            string targetPath = Path.Combine(_scanFolderPath, fileName);
            SaveRotatedPng(sourceImagePath, targetPath, rotationAngle, 0);
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

        private void SaveRotatedPng(string sourceImagePath, string targetPath, int rotationAngle, int maxDimension)
        {
            BitmapSource source = LoadBitmapSource(sourceImagePath);
            BitmapSource rotated = RotateBitmap(source, rotationAngle);
            BitmapSource output = ScaleBitmapIfNeeded(rotated, maxDimension);

            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(output));

            using (FileStream stream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                encoder.Save(stream);
            }
        }

        private BitmapSource LoadBitmapSource(string imageFilePath)
        {
            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(imageFilePath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
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
