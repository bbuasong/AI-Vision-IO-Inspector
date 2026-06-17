using System;

namespace BarcodeScannerSample.Models
{
    /// <summary>
    /// ListBox에 표시할 바코드 스캔 결과입니다.
    /// </summary>
    public class BarcodeItem
    {
        public BarcodeItem(int sequence, string barcodeText, DateTime scannedAt)
        {
            Sequence = sequence;
            BarcodeText = barcodeText;
            ScannedAt = scannedAt;
        }

        public int Sequence { get; private set; }

        public string BarcodeText { get; private set; }

        public DateTime ScannedAt { get; private set; }

        public string ScannedAtText
        {
            get { return Sequence.ToString("000") + "  " + ScannedAt.ToString("HH:mm:ss"); }
        }
    }
}
