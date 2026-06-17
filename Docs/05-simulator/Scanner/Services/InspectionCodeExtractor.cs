using System.Text.RegularExpressions;

namespace ScannerSample.Services
{
    /// <summary>
    /// OCR 전체 텍스트에서 검수 라벨의 상단 품번 코드 형식을 추출합니다.
    /// </summary>
    public class InspectionCodeExtractor
    {
        private static readonly Regex CodePattern = new Regex("[A-Z0-9]{2,6}\\s*[-–—]\\s*[A-Z0-9]{4,8}", RegexOptions.Compiled);

        public string ExtractCode(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                return string.Empty;
            }

            string normalized = NormalizeOcrText(ocrText);
            Match match = CodePattern.Match(normalized);
            if (!match.Success)
            {
                return string.Empty;
            }

            return NormalizeCode(match.Value);
        }

        private string NormalizeOcrText(string text)
        {
            string normalized = text.ToUpperInvariant();
            normalized = normalized.Replace("—", "-");
            normalized = normalized.Replace("–", "-");
            normalized = normalized.Replace("_", "-");
            return normalized;
        }

        private string NormalizeCode(string code)
        {
            string normalized = code.ToUpperInvariant();
            normalized = normalized.Replace(" ", string.Empty);
            normalized = normalized.Replace("—", "-");
            normalized = normalized.Replace("–", "-");
            return normalized;
        }
    }
}
