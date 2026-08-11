using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;

namespace BarcodeScannerSample.Services
{
    /// <summary>
    /// EPSON ES-C320W를 자동으로 찾아 WIA로 이미지를 취득하고, ZXing으로 바코드를 디코딩합니다.
    /// </summary>
    public class WiaBarcodeScanService : IBarcodeScanService
    {
        private const int ScannerDeviceType = 1;
        private const int WiaIntentGrayscale = 2;
        private const int WiaIntentText = 4;
        private const int WiaPropertyCurrentIntent = 6146;
        private const int WiaPropertyXResolution = 6147;
        private const int WiaPropertyYResolution = 6148;
        private const string PngFormatId = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";

        private readonly ScanSettings _settings;
        private readonly string _scanFolderPath;

        public WiaBarcodeScanService()
            : this(ScanSettings.CreateDefault())
        {
        }

        public WiaBarcodeScanService(ScanSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            _settings = settings;
            _scanFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scans");
        }

        public BarcodeDecodeResult ScanAndDecode()
        {
            string scannedFilePath = string.Empty;

            try
            {
                scannedFilePath = AcquireImageByWia();
                return DecodeImageFile(scannedFilePath);
            }
            catch (Exception ex)
            {
                return BarcodeDecodeResult.CreateFailure("스캔 또는 디코딩 중 오류가 발생했습니다. " + ex.Message, scannedFilePath);
            }
        }

        public BarcodeDecodeResult DecodeImageFile(string imageFilePath)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath) || !File.Exists(imageFilePath))
            {
                return BarcodeDecodeResult.CreateFailure("이미지 파일을 찾을 수 없습니다.", imageFilePath);
            }

            try
            {
                Result result = DecodeWithRetryRegions(imageFilePath);
                if (result == null || string.IsNullOrWhiteSpace(result.Text))
                {
                    return BarcodeDecodeResult.CreateFailure("이미지에서 바코드를 찾지 못했습니다.", imageFilePath);
                }

                return BarcodeDecodeResult.CreateSuccess(result.Text, imageFilePath);
            }
            catch (Exception ex)
            {
                return BarcodeDecodeResult.CreateFailure("ZXing 디코딩 중 오류가 발생했습니다. " + ex.Message, imageFilePath);
            }
        }

        private string AcquireImageByWia()
        {
            dynamic deviceInfo = FindScannerDeviceInfo();
            if (deviceInfo == null)
            {
                throw new InvalidOperationException("EPSON ES-C320W 스캐너를 찾을 수 없습니다. Windows 스캐너 장치 목록을 확인하세요.");
            }

            Directory.CreateDirectory(_scanFolderPath);

            dynamic device = deviceInfo.Connect();
            dynamic scanItem = device.Items[1];
            ApplyScannerSettings(scanItem);

            dynamic imageFile = scanItem.Transfer(PngFormatId);
            if (imageFile == null)
            {
                throw new InvalidOperationException("스캔 이미지가 생성되지 않았습니다.");
            }

            string scannedFilePath = BuildScanFilePath();
            imageFile.SaveFile(scannedFilePath);
            return scannedFilePath;
        }

        private dynamic FindScannerDeviceInfo()
        {
            Type managerType = Type.GetTypeFromProgID("WIA.DeviceManager");
            if (managerType == null)
            {
                throw new InvalidOperationException("Windows WIA DeviceManager를 사용할 수 없습니다.");
            }

            dynamic manager = Activator.CreateInstance(managerType);
            foreach (dynamic deviceInfo in manager.DeviceInfos)
            {
                if (!IsScannerDevice(deviceInfo))
                {
                    continue;
                }

                string deviceText = BuildDeviceSearchText(deviceInfo);
                if (deviceText.IndexOf(_settings.TargetDeviceKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return deviceInfo;
                }
            }

            return null;
        }

        private bool IsScannerDevice(dynamic deviceInfo)
        {
            try
            {
                return Convert.ToInt32(deviceInfo.Type) == ScannerDeviceType;
            }
            catch
            {
                return false;
            }
        }

        private string BuildDeviceSearchText(dynamic deviceInfo)
        {
            string name = ReadPropertyText(deviceInfo.Properties, "Name");
            string description = ReadPropertyText(deviceInfo.Properties, "Description");
            string manufacturer = ReadPropertyText(deviceInfo.Properties, "Manufacturer");
            return name + " " + description + " " + manufacturer;
        }

        private string ReadPropertyText(dynamic properties, string propertyName)
        {
            try
            {
                return Convert.ToString(properties[propertyName].Value);
            }
            catch
            {
                return string.Empty;
            }
        }

        private void ApplyScannerSettings(dynamic scanItem)
        {
            int intent = _settings.UseBlackAndWhite ? WiaIntentText : WiaIntentGrayscale;

            TrySetWiaProperty(scanItem.Properties, WiaPropertyCurrentIntent, intent);
            TrySetWiaProperty(scanItem.Properties, WiaPropertyXResolution, _settings.ResolutionDpi);
            TrySetWiaProperty(scanItem.Properties, WiaPropertyYResolution, _settings.ResolutionDpi);
        }

        private void TrySetWiaProperty(dynamic properties, int propertyId, object value)
        {
            try
            {
                foreach (dynamic property in properties)
                {
                    int currentPropertyId = Convert.ToInt32(property.PropertyID);
                    if (currentPropertyId == propertyId)
                    {
                        property.Value = value;
                        return;
                    }
                }
            }
            catch
            {
                // 일부 WIA 드라이버는 속성 변경을 허용하지 않습니다. 이 경우 드라이버 기본값으로 스캔합니다.
            }
        }

        private string BuildScanFilePath()
        {
            string fileName = "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
            return Path.Combine(_scanFolderPath, fileName);
        }

        private BarcodeReaderGeneric CreateBarcodeReader()
        {
            BarcodeReaderGeneric reader = new BarcodeReaderGeneric();
            reader.AutoRotate = true;
            reader.Options = new DecodingOptions();
            reader.Options.TryHarder = true;
            reader.Options.TryInverted = true;
            reader.Options.PossibleFormats = new List<BarcodeFormat>
            {
                BarcodeFormat.CODE_128,
                BarcodeFormat.CODE_39,
                BarcodeFormat.CODE_93,
                BarcodeFormat.EAN_13,
                BarcodeFormat.EAN_8,
                BarcodeFormat.ITF,
                BarcodeFormat.CODABAR,
                BarcodeFormat.UPC_A,
                BarcodeFormat.UPC_E,
                BarcodeFormat.QR_CODE
            };

            return reader;
        }

        private Result DecodeWithRetryRegions(string imageFilePath)
        {
            BitmapSource source = LoadBitmapSource(imageFilePath);

            Result result = TryDecode(source);
            if (result != null)
            {
                return result;
            }

            IList<Int32Rect> cropRegions = BuildRetryCropRegions(source.PixelWidth, source.PixelHeight);
            foreach (Int32Rect cropRegion in cropRegions)
            {
                CroppedBitmap croppedBitmap = new CroppedBitmap(source, cropRegion);
                croppedBitmap.Freeze();

                result = TryDecode(croppedBitmap);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private IList<Int32Rect> BuildRetryCropRegions(int width, int height)
        {
            List<Int32Rect> regions = new List<Int32Rect>();

            int lowerHalfY = height / 2;
            regions.Add(new Int32Rect(0, lowerHalfY, width, height - lowerHalfY));

            int lowerThirdY = height * 2 / 3;
            regions.Add(new Int32Rect(0, lowerThirdY, width, height - lowerThirdY));

            int centerLowerY = height * 45 / 100;
            int centerLowerHeight = height * 45 / 100;
            int centerX = width / 10;
            int centerWidth = width * 8 / 10;
            regions.Add(new Int32Rect(centerX, centerLowerY, centerWidth, centerLowerHeight));

            return regions;
        }

        private Result TryDecode(BitmapSource source)
        {
            LuminanceSource luminanceSource = ConvertBitmapSourceToLuminanceSource(source);
            BarcodeReaderGeneric reader = CreateBarcodeReader();
            return reader.Decode(luminanceSource);
        }

        private BitmapSource LoadBitmapSource(string imageFilePath)
        {
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.UriSource = new Uri(imageFilePath, UriKind.Absolute);
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

        private LuminanceSource ConvertBitmapSourceToLuminanceSource(BitmapSource source)
        {
            FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
            convertedBitmap.BeginInit();
            convertedBitmap.Source = source;
            convertedBitmap.DestinationFormat = PixelFormats.Rgb24;
            convertedBitmap.EndInit();
            convertedBitmap.Freeze();

            int width = convertedBitmap.PixelWidth;
            int height = convertedBitmap.PixelHeight;
            int stride = (width * convertedBitmap.Format.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[stride * height];
            convertedBitmap.CopyPixels(pixels, stride, 0);

            return new RGBLuminanceSource(pixels, width, height, RGBLuminanceSource.BitmapFormat.RGB24);
        }
    }
}
