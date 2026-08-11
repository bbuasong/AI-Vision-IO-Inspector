namespace ScannerSample.Services.Ocr.Common
{
    /// <summary>
    /// 개별 OCR 엔진이 반환한 원본 인식 결과입니다.
    /// </summary>
    public class OcrTextReadResult
    {
        public OcrTextReadResult()
        {
            EngineName = string.Empty;
            Text = string.Empty;
            ExtractedCode = string.Empty;
            ErrorMessage = string.Empty;
            Diagnostics = string.Empty;
        }

        public string EngineName { get; set; }

        public string Text { get; set; }

        public string ExtractedCode { get; set; }

        public bool IsSuccess { get; set; }

        public int Score { get; set; }

        public string ErrorMessage { get; set; }

        public string Diagnostics { get; set; }

        public static OcrTextReadResult CreateSuccess(string engineName, string text)
        {
            OcrTextReadResult result = new OcrTextReadResult();
            result.EngineName = engineName;
            result.Text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            result.IsSuccess = true;
            return result;
        }

        public static OcrTextReadResult CreateFailure(string engineName, string errorMessage)
        {
            OcrTextReadResult result = new OcrTextReadResult();
            result.EngineName = engineName;
            result.IsSuccess = false;
            result.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "OCR 실패" : errorMessage;
            return result;
        }
    }
}
