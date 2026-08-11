namespace BarcodeScannerSample.Services
{
    /// <summary>
    /// 스캐너 선택과 이미지 취득 조건을 보관합니다.
    /// </summary>
    public class ScanSettings
    {
        public ScanSettings()
        {
            TargetDeviceKeyword = "EPSON ES-C320W";
            ResolutionDpi = 300;
            UseBlackAndWhite = false;
            SaveFormat = "PNG";
            PageSizeMode = "Auto Detect";
        }

        public string TargetDeviceKeyword { get; set; }

        public int ResolutionDpi { get; set; }

        public bool UseBlackAndWhite { get; set; }

        public string SaveFormat { get; set; }

        public string PageSizeMode { get; set; }

        public string ScanModeText
        {
            get
            {
                if (UseBlackAndWhite)
                {
                    return "Black/White";
                }

                return "Grayscale";
            }
        }

        public string Summary
        {
            get
            {
                return TargetDeviceKeyword + " / " + PageSizeMode + " / " + ScanModeText + " / " + ResolutionDpi.ToString() + " dpi / " + SaveFormat;
            }
        }

        public static ScanSettings CreateDefault()
        {
            return new ScanSettings();
        }
    }
}
