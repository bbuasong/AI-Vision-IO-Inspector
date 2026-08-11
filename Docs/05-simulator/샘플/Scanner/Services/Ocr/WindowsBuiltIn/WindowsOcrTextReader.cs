using System;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using ScannerSample.Services.Ocr.Common;

namespace ScannerSample.Services.Ocr.WindowsBuiltIn
{
    /// <summary>
    /// Windows 내장 OCR 엔진으로 이미지에서 텍스트를 읽습니다.
    /// </summary>
    public class WindowsOcrTextReader : IOcrTextReader
    {
        public string EngineName
        {
            get { return "Windows OCR"; }
        }

        public async Task<OcrTextReadResult> ReadAsync(string imageFilePath)
        {
            try
            {
                string text = await ReadTextAsync(imageFilePath);
                return OcrTextReadResult.CreateSuccess(EngineName, text);
            }
            catch (Exception ex)
            {
                return OcrTextReadResult.CreateFailure(EngineName, ex.Message);
            }
        }

        public async Task<string> ReadTextAsync(string imageFilePath)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath))
            {
                return string.Empty;
            }

            OcrEngine engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"));
            if (engine == null)
            {
                engine = OcrEngine.TryCreateFromUserProfileLanguages();
            }

            if (engine == null)
            {
                throw new InvalidOperationException("Windows OCR 엔진을 생성할 수 없습니다. Windows OCR 언어팩을 확인하세요.");
            }

            StorageFile file = await StorageFile.GetFileFromPathAsync(imageFilePath);
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                OcrResult result = await engine.RecognizeAsync(bitmap);
                return result.Text;
            }
        }
    }
}
