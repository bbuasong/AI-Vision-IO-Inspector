using System;
using System.IO;

namespace ScannerSample.Models
{
    /// <summary>
    /// OCR로 추출한 검수 라벨 코드 결과입니다.
    /// </summary>
    public class ScanTextItem
    {
        public ScanTextItem(int sequence, string codeText, DateTime scannedAt, string imageFilePath, int rotationAngle)
        {
            Sequence = sequence;
            CodeText = codeText;
            ScannedAt = scannedAt;
            ImageFilePath = imageFilePath;
            RotationAngle = rotationAngle;
        }

        public int Sequence { get; private set; }

        public string CodeText { get; private set; }

        public DateTime ScannedAt { get; private set; }

        public string ImageFilePath { get; private set; }

        public int RotationAngle { get; private set; }

        public string ScannedAtText
        {
            get { return Sequence.ToString("000") + "  " + ScannedAt.ToString("HH:mm:ss"); }
        }

        public string ImageFileName
        {
            get { return Path.GetFileName(ImageFilePath); }
        }

        public string RotationText
        {
            get { return RotationAngle.ToString() + " deg"; }
        }
    }
}
