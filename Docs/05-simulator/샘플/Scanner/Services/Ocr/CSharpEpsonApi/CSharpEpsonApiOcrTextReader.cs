using ScannerSample.Services.Ocr.Http;

namespace ScannerSample.Services.Ocr.CSharpEpsonApi
{
    public class CSharpEpsonApiOcrTextReader : HttpOcrApiTextReader
    {
        public CSharpEpsonApiOcrTextReader()
            : base("C# Epson API", "SCANNER_CSHARP_EPSON_API_BASE_URL", "http://127.0.0.1:8001")
        {
        }
    }
}
