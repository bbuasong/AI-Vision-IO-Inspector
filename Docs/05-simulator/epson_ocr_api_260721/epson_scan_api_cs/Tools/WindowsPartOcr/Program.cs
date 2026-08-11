using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WindowsPartOcr
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                return RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static async Task<int> RunAsync(string[] args)
        {
            if (args == null || args.Length != 1 || !File.Exists(args[0]))
            {
                Console.Error.WriteLine("사용법: WindowsPartOcr.exe <part-crop-image>");
                return 2;
            }

            StorageFile file = await StorageFile.GetFileFromPathAsync(args[0]).AsTask();
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read).AsTask())
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask();
                using (SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync().AsTask())
                {
                    OcrEngine engine = OcrEngine.TryCreateFromUserProfileLanguages();
                    if (engine == null)
                    {
                        Console.Error.WriteLine("Windows OCR 언어 엔진을 사용할 수 없습니다.");
                        return 3;
                    }

                    OcrResult result = await engine.RecognizeAsync(bitmap).AsTask();
                    Console.Write(result.Text ?? string.Empty);
                    return 0;
                }
            }
        }
    }
}
