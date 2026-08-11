namespace BarcodeScannerSample.Services
{
    /// <summary>
    /// 이미지 스캔/디코딩 결과를 ViewModel로 전달하기 위한 결과 모델입니다.
    /// </summary>
    public class BarcodeDecodeResult
    {
        public BarcodeDecodeResult()
        {
            BarcodeText = string.Empty;
            ImageFilePath = string.Empty;
            Message = string.Empty;
        }

        public bool IsSuccess { get; set; }

        public string BarcodeText { get; set; }

        public string ImageFilePath { get; set; }

        public string Message { get; set; }

        public static BarcodeDecodeResult CreateSuccess(string barcodeText, string imageFilePath)
        {
            BarcodeDecodeResult result = new BarcodeDecodeResult();
            result.IsSuccess = true;
            result.BarcodeText = barcodeText;
            result.ImageFilePath = imageFilePath;
            result.Message = "바코드를 읽었습니다.";
            return result;
        }

        public static BarcodeDecodeResult CreateFailure(string message, string imageFilePath)
        {
            BarcodeDecodeResult result = new BarcodeDecodeResult();
            result.IsSuccess = false;
            result.ImageFilePath = imageFilePath;
            result.Message = message;
            return result;
        }
    }
}
