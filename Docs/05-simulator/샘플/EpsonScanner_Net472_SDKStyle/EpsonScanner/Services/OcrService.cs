using System.Text.RegularExpressions;

namespace EpsonScanner.Services
{
    public class OcrService
    {
        public OcrResult DetectId(string cropImagePath)
        {
            // Placeholder OCR for project-load/build stability.
            // Replace this with Tesseract, PaddleOCR, Windows OCR, or your own OCR module.
            string rawText = RunOcrPlaceholder(cropImagePath);
            string judgeId = ExtractJudgeId(rawText);

            return new OcrResult
            {
                JudgeId = judgeId,
                RawText = rawText,
                Rotation = 0
            };
        }

        private string RunOcrPlaceholder(string cropImagePath)
        {
            return "31S7-12020";
        }

        private string ExtractJudgeId(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

            string text = rawText.ToUpperInvariant()
                .Replace(" ", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("–", "-")
                .Replace("—", "-")
                .Replace("_", "-");

            Match match = Regex.Match(text, @"[A-Z0-9]{4}-[0-9]{5}");
            if (match.Success) return match.Value;

            Match matchWithoutDash = Regex.Match(text, @"[A-Z0-9]{4}[0-9]{5}");
            if (matchWithoutDash.Success)
            {
                string value = matchWithoutDash.Value;
                return value.Substring(0, 4) + "-" + value.Substring(4);
            }

            return string.Empty;
        }
    }

    public class OcrResult
    {
        public string JudgeId { get; set; }
        public string RawText { get; set; }
        public int Rotation { get; set; }
    }
}
