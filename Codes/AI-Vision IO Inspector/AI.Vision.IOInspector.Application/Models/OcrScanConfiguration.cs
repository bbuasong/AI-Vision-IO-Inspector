namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// Epson ES-C320W OCR 스캔에 사용하는 사용자 설정입니다.
    /// 급지 방식(ADF), 파일 형식(PNG), OCR 언어는 현장 표준으로 고정합니다.
    /// </summary>
    public class OcrScanConfiguration
    {
        public OcrScanConfiguration()
        {
            ResolutionDpi = 400;
            ColorMode = "gray";
        }

        public int ResolutionDpi { get; set; }

        /// <summary>
        /// gray, bw, color 중 하나입니다.
        /// </summary>
        public string ColorMode { get; set; }
    }
}
