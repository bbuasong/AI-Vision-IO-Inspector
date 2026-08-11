using System.Collections.Generic;
using ScannerSample.Services.Ocr.Common;

namespace ScannerSample.Services.Workflow
{
    /// <summary>
    /// 스캔 이미지 OCR 처리 결과입니다.
    /// </summary>
    public class ScanReadResult
    {
        public ScanReadResult()
        {
            CodeText = string.Empty;
            ImageFilePath = string.Empty;
            OcrText = string.Empty;
            OcrEngineName = string.Empty;
            OcrDiagnostics = string.Empty;
            Message = string.Empty;
            EngineResults = new List<OcrEngineReadResult>();
        }

        public bool IsSuccess { get; set; }

        public string CodeText { get; set; }

        public string ImageFilePath { get; set; }

        public int RotationAngle { get; set; }

        public string OcrText { get; set; }

        public string OcrEngineName { get; set; }

        public string OcrDiagnostics { get; set; }

        public string Message { get; set; }

        public IList<OcrEngineReadResult> EngineResults { get; private set; }

        public static ScanReadResult CreateSuccess(string codeText, string imageFilePath, int rotationAngle, OcrTextReadResult ocrResult)
        {
            ScanReadResult result = new ScanReadResult();
            result.IsSuccess = true;
            result.CodeText = codeText;
            result.ImageFilePath = imageFilePath;
            result.RotationAngle = rotationAngle;
            result.OcrText = ocrResult == null ? string.Empty : ocrResult.Text;
            result.OcrEngineName = ocrResult == null ? string.Empty : ocrResult.EngineName;
            result.OcrDiagnostics = ocrResult == null ? string.Empty : ocrResult.Diagnostics;
            result.Message = "텍스트를 읽었습니다.";
            return result;
        }

        public static ScanReadResult CreateFailure(string message, string imageFilePath, string ocrText)
        {
            ScanReadResult result = new ScanReadResult();
            result.IsSuccess = false;
            result.ImageFilePath = imageFilePath;
            result.OcrText = ocrText;
            result.Message = message;
            return result;
        }

        public OcrEngineReadResult GetEngineResult(string slotKey)
        {
            for (int i = 0; i < EngineResults.Count; i++)
            {
                if (EngineResults[i].SlotKey == slotKey)
                {
                    return EngineResults[i];
                }
            }

            return new OcrEngineReadResult { SlotKey = slotKey };
        }
    }
}
