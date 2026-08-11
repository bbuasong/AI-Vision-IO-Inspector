using System;
using System.IO;
using ScannerSample.Services.Ocr.Common;

namespace ScannerSample.Models
{
    /// <summary>
    /// OCR로 추출한 검사 라벨 코드 결과입니다.
    /// </summary>
    public class ScanTextItem
    {
        public ScanTextItem(
            int sequence,
            string codeText,
            DateTime scannedAt,
            string imageFilePath,
            int rotationAngle,
            string ocrEngineName,
            OcrEngineReadResult paddleOcrResult,
            OcrEngineReadResult windowsOcrResult,
            OcrEngineReadResult csharpEpsonApiResult)
        {
            Sequence = sequence;
            CodeText = codeText;
            ScannedAt = scannedAt;
            ImageFilePath = imageFilePath;
            RotationAngle = rotationAngle;
            OcrEngineName = string.IsNullOrWhiteSpace(ocrEngineName) ? "-" : ocrEngineName;
            PaddleOcrCodeText = ReadDisplayText(paddleOcrResult);
            WindowsOcrCodeText = ReadDisplayText(windowsOcrResult);
            CSharpEpsonApiCodeText = ReadDisplayText(csharpEpsonApiResult);
            PaddleOcrDetails = ReadDetails(paddleOcrResult);
            WindowsOcrDetails = ReadDetails(windowsOcrResult);
            CSharpEpsonApiDetails = ReadDetails(csharpEpsonApiResult);
        }

        public int Sequence { get; private set; }

        public string CodeText { get; private set; }

        public DateTime ScannedAt { get; private set; }

        public string ImageFilePath { get; private set; }

        public int RotationAngle { get; private set; }

        public string OcrEngineName { get; private set; }

        public string PaddleOcrCodeText { get; private set; }

        public string WindowsOcrCodeText { get; private set; }

        public string CSharpEpsonApiCodeText { get; private set; }

        public string PaddleOcrDetails { get; private set; }

        public string WindowsOcrDetails { get; private set; }

        public string CSharpEpsonApiDetails { get; private set; }

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

        private static string ReadDisplayText(OcrEngineReadResult result)
        {
            return result == null ? "-" : result.DisplayText;
        }

        private static string ReadDetails(OcrEngineReadResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return result.ErrorMessage;
            }

            return result.Diagnostics;
        }
    }
}
