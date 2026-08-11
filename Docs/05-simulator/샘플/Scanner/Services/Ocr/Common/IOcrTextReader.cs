using System.Threading.Tasks;

namespace ScannerSample.Services.Ocr.Common
{
    /// <summary>
    /// OCR 엔진 교체를 쉽게 하기 위한 공통 인터페이스입니다.
    /// </summary>
    public interface IOcrTextReader
    {
        string EngineName { get; }

        Task<OcrTextReadResult> ReadAsync(string imageFilePath);
    }
}
