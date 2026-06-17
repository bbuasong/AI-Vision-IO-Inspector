using System;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ScannerSample.Services
{
    /// <summary>
    /// Windows 내장 OCR 엔진으로 이미지의 텍스트를 읽습니다.
    /// </summary>
    public class WindowsOcrTextReader
    {
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
