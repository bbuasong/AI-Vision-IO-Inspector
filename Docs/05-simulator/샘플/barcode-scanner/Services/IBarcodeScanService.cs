namespace BarcodeScannerSample.Services
{
    /// <summary>
    /// 스캐너 이미지 취득과 이미지 파일 바코드 디코딩 기능의 경계입니다.
    /// </summary>
    public interface IBarcodeScanService
    {
        BarcodeDecodeResult ScanAndDecode();

        BarcodeDecodeResult DecodeImageFile(string imageFilePath);
    }
}
