namespace ScannerSample.Services
{
    /// <summary>
    /// 스캔 이미지 OCR 처리 결과입니다.
    /// </summary>
    public class ScanReadResult
    {
        public ScanReadResult()
        {
            CodeText = string.Empty;
            ImageFilePath = string.Empty;
            OcrText = string.Empty;
            Message = string.Empty;
        }

        public bool IsSuccess { get; set; }

        public string CodeText { get; set; }

        public string ImageFilePath { get; set; }

        public int RotationAngle { get; set; }

        public string OcrText { get; set; }

        public string Message { get; set; }

        public static ScanReadResult CreateSuccess(string codeText, string imageFilePath, int rotationAngle, string ocrText)
        {
            ScanReadResult result = new ScanReadResult();
            result.IsSuccess = true;
            result.CodeText = codeText;
            result.ImageFilePath = imageFilePath;
            result.RotationAngle = rotationAngle;
            result.OcrText = ocrText;
            result.Message = "텍스트를 읽었습니다.";
            return result;
        }

        public static ScanReadResult CreateFailure(string message, string imageFilePath, string ocrText)
        {
            ScanReadResult result = new ScanReadResult();
            result.IsSuccess = false;
            result.ImageFilePath = imageFilePath;
            result.OcrText = ocrText;
            result.Message = message;
            return result;
        }
    }
}
