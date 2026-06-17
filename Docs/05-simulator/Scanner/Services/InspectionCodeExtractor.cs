using System.Text.RegularExpressions;

namespace ScannerSample.Services
{
    /// <summary>
    /// OCR 결과 텍스트에서 검수 글자 옆 최상단 코드 형식을 추출합니다.
    /// </summary>
    public class InspectionCodeExtractor
    {
        private static readonly Regex CodePattern = new Regex("(?<![A-Z0-9])([A-Z0-9](?:\\s?[A-Z0-9]){2,5})\\s*[-–—_]\\s*([0-9]{4,8})(?![A-Z0-9])", RegexOptions.Compiled);
        private static readonly Regex HasLetterPattern = new Regex("[A-Z]", RegexOptions.Compiled);
        private static readonly Regex HasDigitPattern = new Regex("[0-9]", RegexOptions.Compiled);

        public string ExtractCode(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                return string.Empty;
            }

            string normalized = NormalizeOcrText(ocrText);
            MatchCollection matches = CodePattern.Matches(normalized);
            foreach (Match match in matches)
            {
                string left = match.Groups[1].Value;
                string right = match.Groups[2].Value;
                left = left.Replace(" ", string.Empty);

                if (!IsValidLeftCode(left))
                {
                    continue;
                }

                return left + "-" + right;
            }

            return string.Empty;
        }

        private string NormalizeOcrText(string text)
        {
            string normalized = text.ToUpperInvariant();
            normalized = normalized.Replace("–", "-");
            normalized = normalized.Replace("—", "-");
            normalized = normalized.Replace("_", "-");
            return normalized;
        }

        private bool IsValidLeftCode(string left)
        {
            if (string.IsNullOrWhiteSpace(left))
            {
                return false;
            }

            // AOU-LSLT 같은 OCR 오인식 후보를 막기 위해 왼쪽 코드는 문자와 숫자를 모두 포함해야 합니다.
            return HasLetterPattern.IsMatch(left) && HasDigitPattern.IsMatch(left);
        }
    }
}
