using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OCRSample.Models;

namespace OCRSample.Services
{
    /// <summary>
    /// 64비트 WPF 프로세스에서 WIA COM을 통해 USB 스캐너를 직접 제어합니다.
    /// WIA COM 호출은 반드시 STA 스레드에서 수행합니다.
    /// </summary>
    public static class DirectWiaScanner
    {
        private const string FormatBmp = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}";
        private const string FormatPng = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";
        private const string FormatJpeg = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";

        private const int PropertyIntent = 6146;
        private const int PropertyXResolution = 6147;
        private const int PropertyYResolution = 6148;
        private const int PropertyDataType = 4103;
        private const int PropertyDocumentHandlingSelect = 3088;
        private const int PropertyDocumentHandlingCapabilities = 3086;
        private const int PropertyDocumentHandlingStatus = 3087;

        private const int CapabilityFeeder = 1;
        private const int CapabilityFlatbed = 2;
        private const int StatusFeedReady = 1;

        public static List<ScannerDevice> ListScanners()
        {
            return RunInSta(ListScannersCore);
        }

        public static Task<string> ScanAsync(
            string outputDirectory,
            string deviceId,
            int dpi,
            string mode,
            string source,
            string format)
        {
            return Task.Factory.StartNew(
                delegate { return Scan(outputDirectory, deviceId, dpi, mode, source, format); },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public static string Scan(
            string outputDirectory,
            string deviceId,
            int dpi,
            string mode,
            string source,
            string format)
        {
            return RunInSta(delegate
            {
                return ScanCore(outputDirectory, deviceId, dpi, mode, source, format);
            });
        }

        /// <summary>
        /// Scans without exposing expected device conditions as exceptions to
        /// the WPF event handler.
        /// </summary>
        public static Task<DirectWiaScanResult> TryScanAsync(
            string outputDirectory,
            string deviceId,
            int dpi,
            string mode,
            string source,
            string format)
        {
            return Task.Factory.StartNew(
                delegate { return TryScan(outputDirectory, deviceId, dpi, mode, source, format); },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public static DirectWiaScanResult TryScan(
            string outputDirectory,
            string deviceId,
            int dpi,
            string mode,
            string source,
            string format)
        {
            return RunInSta(delegate
            {
                try
                {
                    return TryScanCore(outputDirectory, deviceId, dpi, mode, source, format);
                }
                catch (Exception ex)
                {
                    return DirectWiaScanResult.Failed(
                        DirectWiaScanFailure.Unexpected,
                        "\uC2A4\uCEA4 \uC900\uBE44 \uB610\uB294 \uC800\uC7A5 \uC911 \uC624\uB958\uAC00 \uBC1C\uC0DD\uD588\uC2B5\uB2C8\uB2E4: " + ex.Message);
                }
            });
        }

        private static DirectWiaScanResult TryScanCore(
            string outputDirectory,
            string deviceId,
            int dpi,
            string mode,
            string source,
            string format)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return DirectWiaScanResult.Failed(
                    DirectWiaScanFailure.Unexpected,
                    "\uC2A4\uCEA4 \uC774\uBBF8\uC9C0 \uC800\uC7A5 \uD3F4\uB354\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4.");
            }

            Directory.CreateDirectory(outputDirectory);

            dynamic manager = CreateWiaManager();
            dynamic target = null;
            int deviceCount = (int)manager.DeviceInfos.Count;
            for (int index = 1; index <= deviceCount; index++)
            {
                dynamic info = manager.DeviceInfos.Item(index);
                if ((int)info.Type == 1 &&
                    (string.IsNullOrWhiteSpace(deviceId) || (string)info.DeviceID == deviceId))
                {
                    target = info;
                    break;
                }
            }

            if (target == null)
            {
                return DirectWiaScanResult.Failed(
                    DirectWiaScanFailure.DeviceNotFound,
                    "\uC120\uD0DD\uD55C USB \uC2A4\uCEA4\uB108\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. USB \uC5F0\uACB0\uACFC \uC804\uC6D0\uC744 \uD655\uC778\uD55C \uB4A4 \uC7A5\uCE58 \uC0C8\uB85C\uACE0\uCE68\uC744 \uC2E4\uD589\uD558\uC138\uC694.");
            }

            dynamic device = target.Connect();
            int capabilities = GetProperty(device.Properties, PropertyDocumentHandlingCapabilities, 0);
            bool hasFeeder = (capabilities & CapabilityFeeder) != 0;
            bool hasFlatbed = (capabilities & CapabilityFlatbed) != 0;
            bool useFeeder = IsFeeder(source) || (hasFeeder && !hasFlatbed);

            SetProperty(
                device.Properties,
                PropertyDocumentHandlingSelect,
                useFeeder ? CapabilityFeeder : CapabilityFlatbed);

            if (useFeeder)
            {
                int status = GetProperty(device.Properties, PropertyDocumentHandlingStatus, 0);
                if ((status & StatusFeedReady) == 0)
                {
                    return DirectWiaScanResult.Failed(
                        DirectWiaScanFailure.PaperEmpty,
                        "\uC2A4\uCEA4\uB108 ADF\uC5D0 \uC6A9\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uBB38\uC11C\uB97C \uB123\uC740 \uB4A4 \uB2E4\uC2DC \uC2A4\uCEA4\uD558\uC138\uC694.");
                }
            }

            dynamic item = device.Items.Item(1);
            SetProperty(item.Properties, PropertyXResolution, dpi);
            SetProperty(item.Properties, PropertyYResolution, dpi);
            SetProperty(item.Properties, PropertyDataType, DataTypeFor(mode));
            try
            {
                SetProperty(item.Properties, PropertyIntent, IntentFor(mode));
            }
            catch
            {
                // Some WIA drivers do not support changing the scan intent.
            }

            string extension = ExtensionFor(format);
            string outputPath = Path.Combine(
                outputDirectory,
                "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "." + extension);

            try
            {
                dynamic image = item.Transfer(FormatGuidFor(format));
                image.SaveFile(outputPath);
                return DirectWiaScanResult.Success(outputPath);
            }
            catch (COMException ex)
            {
                return WiaFailure(ex.HResult);
            }
        }

        private static List<ScannerDevice> ListScannersCore()
        {
            dynamic manager = CreateWiaManager();
            var scanners = new List<ScannerDevice>();
            int count = (int)manager.DeviceInfos.Count;

            for (int index = 1; index <= count; index++)
            {
                dynamic info = manager.DeviceInfos.Item(index);
                if ((int)info.Type != 1)
                {
                    continue;
                }

                string id = (string)info.DeviceID;
                string name = string.Empty;
                try
                {
                    foreach (dynamic property in info.Properties)
                    {
                        if ((string)property.Name == "Name")
                        {
                            name = (string)property.Value;
                            break;
                        }
                    }
                }
                catch
                {
                    // 장치 이름을 읽지 못하면 ID를 표시한다.
                }

                scanners.Add(new ScannerDevice(id, string.IsNullOrWhiteSpace(name) ? id : name));
            }

            return scanners;
        }

        private static string ScanCore(
            string outputDirectory,
            string deviceId,
            int dpi,
            string mode,
            string source,
            string format)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("스캔 이미지 저장 폴더가 필요합니다.", "outputDirectory");
            }

            Directory.CreateDirectory(outputDirectory);

            dynamic manager = CreateWiaManager();
            dynamic target = null;
            int deviceCount = (int)manager.DeviceInfos.Count;
            for (int index = 1; index <= deviceCount; index++)
            {
                dynamic info = manager.DeviceInfos.Item(index);
                if ((int)info.Type != 1)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(deviceId) || (string)info.DeviceID == deviceId)
                {
                    target = info;
                    break;
                }
            }

            if (target == null)
            {
                throw new InvalidOperationException("선택한 USB 스캐너를 찾을 수 없습니다.");
            }

            dynamic device = target.Connect();
            int capabilities = GetProperty(
                device.Properties,
                PropertyDocumentHandlingCapabilities,
                0);

            bool hasFeeder = (capabilities & CapabilityFeeder) != 0;
            bool hasFlatbed = (capabilities & CapabilityFlatbed) != 0;
            bool useFeeder = IsFeeder(source) || (hasFeeder && !hasFlatbed);

            SetProperty(
                device.Properties,
                PropertyDocumentHandlingSelect,
                useFeeder ? CapabilityFeeder : CapabilityFlatbed);

            if (useFeeder)
            {
                int status = GetProperty(device.Properties, PropertyDocumentHandlingStatus, 0);
                if ((status & StatusFeedReady) == 0)
                {
                    throw new InvalidOperationException(
                        "ADF 급지구에 용지가 감지되지 않습니다. 라벨 또는 문서를 넣고 다시 시도하세요.");
                }
            }

            dynamic item = device.Items.Item(1);
            SetProperty(item.Properties, PropertyXResolution, dpi);
            SetProperty(item.Properties, PropertyYResolution, dpi);
            SetProperty(item.Properties, PropertyDataType, DataTypeFor(mode));
            try
            {
                SetProperty(item.Properties, PropertyIntent, IntentFor(mode));
            }
            catch
            {
                // 일부 WIA 드라이버는 intent 변경을 지원하지 않는다.
            }

            string extension = ExtensionFor(format);
            string outputPath = Path.Combine(
                outputDirectory,
                "scan_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "." + extension);
            string formatGuid = FormatGuidFor(format);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    dynamic image = item.Transfer(formatGuid);
                    image.SaveFile(outputPath);
                    return outputPath;
                }
                catch (COMException ex)
                {
                    if (attempt == 0 && unchecked((uint)ex.HResult) == 0x80210003)
                    {
                        Thread.Sleep(1200);
                        continue;
                    }

                    throw new InvalidOperationException(WiaErrorText(ex.HResult), ex);
                }
            }

            throw new InvalidOperationException("스캔에 실패했습니다.");
        }

        private static T RunInSta<T>(Func<T> operation)
        {
            T result = default(T);
            Exception error = null;
            var thread = new Thread(delegate()
            {
                try
                {
                    result = operation();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (error != null)
            {
                ExceptionDispatchInfo.Capture(error).Throw();
            }

            return result;
        }

        private static dynamic CreateWiaManager()
        {
            Type type = Type.GetTypeFromProgID("WIA.DeviceManager", true);
            if (type == null)
            {
                throw new InvalidOperationException("Windows WIA 구성요소를 찾지 못했습니다.");
            }

            object manager = Activator.CreateInstance(type);
            if (manager == null)
            {
                throw new InvalidOperationException("WIA 장치 관리자를 만들 수 없습니다.");
            }

            return manager;
        }

        private static void SetProperty(dynamic properties, int propertyId, int value)
        {
            int count = (int)properties.Count;
            for (int index = 1; index <= count; index++)
            {
                dynamic property = properties.Item(index);
                if ((int)property.PropertyID == propertyId)
                {
                    property.Value = value;
                    return;
                }
            }
        }

        private static int GetProperty(dynamic properties, int propertyId, int defaultValue)
        {
            try
            {
                int count = (int)properties.Count;
                for (int index = 1; index <= count; index++)
                {
                    dynamic property = properties.Item(index);
                    if ((int)property.PropertyID == propertyId)
                    {
                        return Convert.ToInt32(property.Value);
                    }
                }
            }
            catch
            {
                // 드라이버가 속성을 노출하지 않는 경우 기본값을 쓴다.
            }

            return defaultValue;
        }

        private static bool IsFeeder(string source)
        {
            return string.Equals(source, "feeder", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "adf", StringComparison.OrdinalIgnoreCase);
        }

        private static int IntentFor(string mode)
        {
            if (string.Equals(mode, "color", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (string.Equals(mode, "bw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "text", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }
            return 2;
        }

        private static int DataTypeFor(string mode)
        {
            if (string.Equals(mode, "color", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }
            if (string.Equals(mode, "bw", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "text", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            return 2;
        }

        private static string FormatGuidFor(string format)
        {
            if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
            {
                return FormatPng;
            }
            if (string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase))
            {
                return FormatJpeg;
            }
            return FormatBmp;
        }

        private static string ExtensionFor(string format)
        {
            if (string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase))
            {
                return "jpg";
            }
            if (string.Equals(format, "png", StringComparison.OrdinalIgnoreCase))
            {
                return "png";
            }
            return "bmp";
        }

        private static string WiaErrorText(int hresult)
        {
            switch (unchecked((uint)hresult))
            {
                case 0x80210003:
                    return "스캐너에 용지가 없습니다 (ADF empty). 용지를 넣고 다시 시도하세요.";
                case 0x80210006:
                    return "스캐너가 준비되지 않았습니다 (busy 또는 warming up).";
                case 0x80210001:
                    return "일반 스캔 오류입니다.";
                case 0x80210015:
                    return "스캐너를 사용할 수 없습니다 (offline 또는 disconnected).";
                case 0x8021000A:
                    return "스캔 통신 오류입니다.";
                case 0x80210067:
                    return "스캔이 취소되었습니다.";
                default:
                    return "스캔 실패 (HRESULT 0x" + unchecked((uint)hresult).ToString("X8") + ").";
            }
        }

        private static DirectWiaScanResult WiaFailure(int hresult)
        {
            DirectWiaScanFailure failure;
            switch (unchecked((uint)hresult))
            {
                case 0x80210003:
                    failure = DirectWiaScanFailure.PaperEmpty;
                    break;
                case 0x80210006:
                    failure = DirectWiaScanFailure.Busy;
                    break;
                case 0x80210015:
                    failure = DirectWiaScanFailure.Offline;
                    break;
                case 0x80210067:
                    failure = DirectWiaScanFailure.Cancelled;
                    break;
                default:
                    failure = DirectWiaScanFailure.Unexpected;
                    break;
            }

            return DirectWiaScanResult.Failed(failure, WiaErrorText(hresult));
        }
    }
}
