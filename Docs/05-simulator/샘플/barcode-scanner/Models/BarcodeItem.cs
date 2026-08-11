using System;

namespace BarcodeScannerSample.Models
{
    /// <summary>
    /// ListBox에 표시할 바코드 스캔 결과입니다.
    /// </summary>
    public class BarcodeItem
    {
        public BarcodeItem(int sequence, string barcodeText, DateTime scannedAt, string imageFilePath)
        {
            Sequence = sequence;
            BarcodeText = barcodeText;
            ScannedAt = scannedAt;
            ImageFilePath = imageFilePath;
        }

        public int Sequence { get; private set; }

        public string BarcodeText { get; private set; }

        public DateTime ScannedAt { get; private set; }

        public string ImageFilePath { get; private set; }

        public string ScannedAtText
        {
            get { return Sequence.ToString("000") + "  " + ScannedAt.ToString("HH:mm:ss"); }
        }

        public string ImageFileName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(ImageFilePath))
                {
                    return "Manual input";
                }

                return System.IO.Path.GetFileName(ImageFilePath);
            }
        }
    }
}
