using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

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
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

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
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
