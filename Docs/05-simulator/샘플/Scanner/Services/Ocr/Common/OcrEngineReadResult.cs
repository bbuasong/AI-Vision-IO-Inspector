namespace ScannerSample.Services.Ocr.Common
{
    public class OcrEngineReadResult
    {
        public OcrEngineReadResult()
        {
            SlotKey = string.Empty;
            DisplayName = string.Empty;
            EngineName = string.Empty;
            CodeText = string.Empty;
            OcrText = string.Empty;
            ErrorMessage = string.Empty;
            Diagnostics = string.Empty;
        }

        public string SlotKey { get; set; }

        public string DisplayName { get; set; }

        public string EngineName { get; set; }

        public bool IsSuccess { get; set; }

        public string CodeText { get; set; }

        public string OcrText { get; set; }

        public string ErrorMessage { get; set; }

        public string Diagnostics { get; set; }

        public string DisplayText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(CodeText))
                {
                    return CodeText;
                }

                return IsSuccess ? "-" : "ERR";
            }
        }

        public static OcrEngineReadResult FromOcrResult(string slotKey, string displayName, OcrTextReadResult ocrResult, string codeText)
        {
            OcrEngineReadResult result = new OcrEngineReadResult();
            result.SlotKey = slotKey;
            result.DisplayName = displayName;
            result.EngineName = ocrResult == null || string.IsNullOrWhiteSpace(ocrResult.EngineName) ? displayName : ocrResult.EngineName;
            result.IsSuccess = ocrResult != null && ocrResult.IsSuccess;
            result.CodeText = string.IsNullOrWhiteSpace(codeText) ? string.Empty : codeText;
            result.OcrText = ocrResult == null || string.IsNullOrWhiteSpace(ocrResult.Text) ? string.Empty : ocrResult.Text;
            result.ErrorMessage = ocrResult == null || string.IsNullOrWhiteSpace(ocrResult.ErrorMessage) ? string.Empty : ocrResult.ErrorMessage;
            result.Diagnostics = ocrResult == null || string.IsNullOrWhiteSpace(ocrResult.Diagnostics) ? string.Empty : ocrResult.Diagnostics;
            return result;
        }
    }
}
