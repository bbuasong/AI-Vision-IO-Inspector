using System;
using ScannerSample.Services.Ocr.Common;

namespace ScannerSample.Services.Ocr.Hybrid
{
    /// <summary>
    /// 여러 OCR 엔진의 결과 중 품번 추출에 더 적합한 결과를 점수화합니다.
    /// </summary>
    public class OcrCandidateEvaluator
    {
        private readonly InspectionCodeExtractor _codeExtractor;

        public OcrCandidateEvaluator(InspectionCodeExtractor codeExtractor)
        {
            if (codeExtractor == null)
            {
                throw new ArgumentNullException("codeExtractor");
            }

            _codeExtractor = codeExtractor;
        }

        public int CalculateScore(OcrTextReadResult result)
        {
            if (result == null)
            {
                return int.MinValue;
            }

            if (!result.IsSuccess)
            {
                return -1000;
            }

            string text = string.IsNullOrWhiteSpace(result.Text) ? string.Empty : result.Text.ToUpperInvariant();
            string code = _codeExtractor.ExtractCode(text);
            int score = 0;

            if (!string.IsNullOrWhiteSpace(code))
            {
                score += 300;
                score += Math.Min(code.Length, 20) * 3;
            }

            if (code.Contains("-"))
            {
                score += 30;
            }

            if (text.Contains("RCV"))
            {
                score += 25;
            }

            if (text.Contains("WORKING"))
            {
                score += 25;
            }

            if (text.Contains("WALVOIL"))
            {
                score += 20;
            }

            if (text.Contains("IT0003"))
            {
                score += 10;
            }

            if (text.Contains("AOU") || text.Contains("OSL-OGL") || text.Contains("LOOOV") || text.Contains("TOLOO"))
            {
                score -= 80;
            }

            score += Math.Min(text.Length, 120) / 20;
            return score;
        }
    }
}
