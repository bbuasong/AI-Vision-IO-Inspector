using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.Converters
{
    /// <summary>
    /// Loads preview images into memory so WPF does not keep the source file locked.
    /// </summary>
    public class ImageFilePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string filePath = value as string;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
            filePath = pathSettings.ResolveImageFilePath(filePath);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            object image = TryLoadWithWpf(filePath);
            if (image != null)
            {
                return image;
            }

            return TryLoadWithBitmapDecoder(filePath);
        }

        private object TryLoadWithWpf(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return null;
        }

        private object TryLoadWithBitmapDecoder(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    // 일부 LibVLC PNG 스냅샷은 BitmapImage에서 메타데이터 예외를 내지만 BitmapDecoder로는 프레임을 읽을 수 있습니다.
                    BitmapDecoder decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache,
                        BitmapCacheOption.OnLoad);

                    if (decoder.Frames == null || decoder.Frames.Count == 0)
                    {
                        return null;
                    }

                    BitmapFrame frame = decoder.Frames[0];
                    if (frame.CanFreeze)
                    {
                        frame.Freeze();
                    }

                    return frame;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
