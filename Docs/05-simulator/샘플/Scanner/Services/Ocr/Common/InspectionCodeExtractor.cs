using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ScannerSample.Services.Ocr.Common
{
    /// <summary>
    /// OCR 결과 텍스트에서 괄호 '(' 바로 앞에 있는 품번만 추출합니다.
    /// </summary>
    public class InspectionCodeExtractor
    {
        private static readonly Regex CodeBeforeParenthesisPattern = new Regex("([A-Z0-9][A-Z0-9\\s\\-_]{1,30}[A-Z0-9])\\s*[\\(（]", RegexOptions.Compiled);
        private static readonly Regex HasDigitPattern = new Regex("[0-9]", RegexOptions.Compiled);

        public string ExtractCode(string ocrText)
        {
            if (string.IsNullOrWhiteSpace(ocrText))
            {
                return string.Empty;
            }

            string normalizedText = NormalizeOcrText(ocrText);
            MatchCollection matches = CodeBeforeParenthesisPattern.Matches(normalizedText);
            foreach (Match match in matches)
            {
                string candidate = NormalizeCode(match.Groups[1].Value);
                string correctedCandidate = CorrectLikelyOcrConfusions(candidate);
                if (IsValidCode(correctedCandidate))
                {
                    return correctedCandidate;
                }
            }

            return string.Empty;
        }

        private string NormalizeOcrText(string text)
        {
            string normalized = text.ToUpperInvariant();
            normalized = normalized.Replace("_", "-");
            normalized = normalized.Replace("（", "(");
            return normalized;
        }

        private string NormalizeCode(string code)
        {
            string normalized = code.ToUpperInvariant();
            normalized = normalized.Replace(" ", string.Empty);
            normalized = normalized.Replace("_", "-");

            while (normalized.StartsWith("-"))
            {
                normalized = normalized.Substring(1);
            }

            while (normalized.EndsWith("-"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private string CorrectLikelyOcrConfusions(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            string[] parts = code.Split(new char[] { '-' }, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                if (ShouldCorrectAsNumericSection(parts[i], i, parts.Length))
                {
                    parts[i] = CorrectNumericSection(parts[i]);
                }
            }

            return string.Join("-", parts);
        }

        private bool ShouldCorrectAsNumericSection(string section, int sectionIndex, int sectionCount)
        {
            if (string.IsNullOrWhiteSpace(section))
            {
                return false;
            }

            int digitCount = 0;
            int numericLikeCount = 0;
            for (int i = 0; i < section.Length; i++)
            {
                char value = section[i];
                if (char.IsDigit(value))
                {
                    digitCount++;
                    numericLikeCount++;
                    continue;
                }

                if (IsNumericLikeOcrCharacter(value))
                {
                    numericLikeCount++;
                }
            }

            if (sectionCount > 1 && sectionIndex > 0 && numericLikeCount >= section.Length - 1)
            {
                return true;
            }

            if (sectionCount > 1)
            {
                return false;
            }

            return digitCount >= Math.Max(2, section.Length * 3 / 4) && numericLikeCount >= section.Length - 1;
        }

        private bool IsNumericLikeOcrCharacter(char value)
        {
            return value == 'O'
                || value == 'Q'
                || value == 'D'
                || value == 'I'
                || value == 'L'
                || value == 'S'
                || value == 'B'
                || value == 'Z';
        }

        private string CorrectNumericSection(string section)
        {
            StringBuilder builder = new StringBuilder(section.Length);
            for (int i = 0; i < section.Length; i++)
            {
                char value = section[i];
                if (value == 'O' || value == 'Q' || value == 'D')
                {
                    builder.Append('0');
                }
                else if (value == 'I' || value == 'L')
                {
                    builder.Append('1');
                }
                else if (value == 'S')
                {
                    builder.Append('5');
                }
                else if (value == 'B')
                {
                    builder.Append('8');
                }
                else if (value == 'Z')
                {
                    builder.Append('2');
                }
                else
                {
                    builder.Append(value);
                }
            }

            return builder.ToString();
        }

        private bool IsValidCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            if (code.Length < 3 || code.Length > 24)
            {
                return false;
            }

            // 품번은 숫자만 있을 수도 있고 문자/숫자 혼합일 수도 있으므로 숫자 포함만 필수로 봅니다.
            return HasDigitPattern.IsMatch(code);
        }
    }
}
