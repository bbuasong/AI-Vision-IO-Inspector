using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace EpsonScanner.Services
{
    public class ImageProcessingService
    {
        // Ratio coordinates: X, Y, Width, Height.
        // Adjust these after checking real ES-C320W scan images.
        private readonly RectangleF _labelRegionRatio = new RectangleF(0.05f, 0.05f, 0.90f, 0.45f);
        private readonly RectangleF _cropRegionRatio = new RectangleF(0.05f, 0.20f, 0.55f, 0.35f);

        public string SaveLabelImage(string rawImagePath, string labelImagePath)
        {
            using (Bitmap source = new Bitmap(rawImagePath))
            using (Bitmap label = CropByRatio(source, _labelRegionRatio))
            {
                label.Save(labelImagePath, ImageFormat.Png);
            }
            return labelImagePath;
        }

        public string SaveCropImage(string labelImagePath, string cropImagePath)
        {
            using (Bitmap source = new Bitmap(labelImagePath))
            using (Bitmap crop = CropByRatio(source, _cropRegionRatio))
            {
                crop.Save(cropImagePath, ImageFormat.Png);
            }
            return cropImagePath;
        }

        private Bitmap CropByRatio(Bitmap source, RectangleF ratio)
        {
            int x = Math.Max(0, (int)(source.Width * ratio.X));
            int y = Math.Max(0, (int)(source.Height * ratio.Y));
            int width = Math.Min(source.Width - x, (int)(source.Width * ratio.Width));
            int height = Math.Min(source.Height - y, (int)(source.Height * ratio.Height));

            if (width <= 0 || height <= 0)
                throw new InvalidOperationException("Crop 영역이 잘못되었습니다.");

            Rectangle rect = new Rectangle(x, y, width, height);
            return source.Clone(rect, PixelFormat.Format24bppRgb);
        }
    }
}
