using System;
using System.IO;

namespace ScannerSample.Services
{
    /// <summary>
    /// EPSON ES-C320W를 WIA 장치 목록에서 자동으로 찾아 PNG 스캔 파일을 생성합니다.
    /// </summary>
    public class WiaScannerService
    {
        private const int ScannerDeviceType = 1;
        private const int WiaIntentGrayscale = 2;
        private const int WiaIntentText = 4;
        private const int WiaPropertyCurrentIntent = 6146;
        private const int WiaPropertyXResolution = 6147;
        private const int WiaPropertyYResolution = 6148;
        private const string PngFormatId = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";

        private readonly ScanSettings _settings;
        private readonly string _rawScanFolderPath;

        public WiaScannerService(ScanSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            _settings = settings;
            _rawScanFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scans", "Raw");
        }

        public string ScanToPng()
        {
            dynamic deviceInfo = FindScannerDeviceInfo();
            if (deviceInfo == null)
            {
                throw new InvalidOperationException("EPSON ES-C320W 스캐너를 찾을 수 없습니다. Windows 장치 목록과 전원/연결 상태를 확인하세요.");
            }

            Directory.CreateDirectory(_rawScanFolderPath);

            dynamic device = deviceInfo.Connect();
            dynamic scanItem = device.Items[1];
            ApplyScannerSettings(scanItem);

            dynamic imageFile = scanItem.Transfer(PngFormatId);
            if (imageFile == null)
            {
                throw new InvalidOperationException("스캔 이미지가 생성되지 않았습니다.");
            }

            string rawFilePath = BuildRawFilePath();
            imageFile.SaveFile(rawFilePath);
            return rawFilePath;
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
                // WIA 드라이버가 속성 변경을 거부하면 드라이버 기본값으로 스캔합니다.
            }
        }

        private string BuildRawFilePath()
        {
            string fileName = "raw_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png";
            return Path.Combine(_rawScanFolderPath, fileName);
        }
    }
}
