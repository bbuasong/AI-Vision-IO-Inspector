namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// WIA에서 검색한 Epson ES-C320W 장치 정보입니다.
    /// </summary>
    public class OcrScannerDevice
    {
        public OcrScannerDevice()
        {
            DeviceId = string.Empty;
            DisplayName = string.Empty;
        }

        public string DeviceId { get; set; }

        public string DisplayName { get; set; }
    }
}
