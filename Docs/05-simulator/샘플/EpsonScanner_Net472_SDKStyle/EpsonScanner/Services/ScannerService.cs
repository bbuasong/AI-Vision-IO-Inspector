using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace EpsonScanner.Services
{
    public class ScannerService
    {
        public string ScanToRawPng(string rawPath)
        {
            Type dialogType = Type.GetTypeFromProgID("WIA.CommonDialog");
            if (dialogType == null)
                throw new InvalidOperationException("WIA.CommonDialog를 찾을 수 없습니다. Windows WIA 서비스 또는 Epson 스캐너 드라이버를 확인하세요.");

            dynamic dialog = Activator.CreateInstance(dialogType);
            dynamic device = dialog.ShowSelectDevice(1, true, false); // 1 = ScannerDeviceType
            if (device == null)
                throw new InvalidOperationException("스캐너가 선택되지 않았습니다.");

            dynamic item = device.Items[1];
            TrySetWiaProperty(item, "6147", 300); // Horizontal Resolution
            TrySetWiaProperty(item, "6148", 300); // Vertical Resolution
            TrySetWiaProperty(item, "6146", 2);   // Color Intent: 2 = Grayscale

            const string wiaFormatPng = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}";
            dynamic image = dialog.ShowTransfer(item, wiaFormatPng, false);

            if (File.Exists(rawPath)) File.Delete(rawPath);
            image.SaveFile(rawPath);
            return rawPath;
        }

        private void TrySetWiaProperty(dynamic item, string propertyId, object value)
        {
            try
            {
                dynamic prop = item.Properties[propertyId];
                prop.Value = value;
            }
            catch
            {
                // Some scanner drivers do not support every WIA property.
            }
        }

        public string CreateSampleRawImage(string rawPath)
        {
            using (var bitmap = new Bitmap(1200, 800))
            using (var g = Graphics.FromImage(bitmap))
            using (var backBrush = new SolidBrush(Color.White))
            using (var labelBrush = new SolidBrush(Color.FromArgb(245, 245, 245)))
            using (var pen = new Pen(Color.Black, 3))
            using (var fontLarge = new Font("Arial", 44, FontStyle.Bold))
            using (var fontSmall = new Font("Arial", 18, FontStyle.Regular))
            {
                g.FillRectangle(backBrush, 0, 0, bitmap.Width, bitmap.Height);
                g.DrawRectangle(pen, 70, 60, 1040, 340);
                g.FillRectangle(labelBrush, 72, 62, 1036, 336);
                g.DrawString("LABEL AREA", fontSmall, Brushes.Black, 100, 90);
                g.DrawString("31S7-12020", fontLarge, Brushes.Black, 130, 180);
                g.DrawString("OCR CROP SAMPLE", fontSmall, Brushes.Black, 130, 270);

                if (File.Exists(rawPath)) File.Delete(rawPath);
                bitmap.Save(rawPath, ImageFormat.Png);
            }
            return rawPath;
        }
    }
}
