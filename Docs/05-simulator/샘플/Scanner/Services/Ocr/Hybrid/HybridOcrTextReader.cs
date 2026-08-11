using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ScannerSample.Services.Ocr.Common;

namespace ScannerSample.Services.Ocr.Hybrid
{
    /// <summary>
    /// Windows OCR과 PaddleOCR을 모두 실행한 뒤 품번 추출 점수가 높은 결과를 선택합니다.
    /// </summary>
    public class HybridOcrTextReader : IOcrTextReader
    {
        private readonly IList<IOcrTextReader> _readers;
        private readonly OcrCandidateEvaluator _evaluator;

        public HybridOcrTextReader(IList<IOcrTextReader> readers, OcrCandidateEvaluator evaluator)
        {
            if (readers == null)
            {
                throw new ArgumentNullException("readers");
            }

            if (evaluator == null)
            {
                throw new ArgumentNullException("evaluator");
            }

            _readers = readers;
            _evaluator = evaluator;
        }

        public string EngineName
        {
            get { return "Hybrid OCR"; }
        }

        public async Task<OcrTextReadResult> ReadAsync(string imageFilePath)
        {
            OcrTextReadResult bestResult = null;
            StringBuilder diagnostics = new StringBuilder();

            for (int i = 0; i < _readers.Count; i++)
            {
                IOcrTextReader reader = _readers[i];
                OcrTextReadResult result = await reader.ReadAsync(imageFilePath);
                result.Score = _evaluator.CalculateScore(result);

                diagnostics.Append(reader.EngineName);
                diagnostics.Append(": ");
                diagnostics.Append(result.IsSuccess ? "OK" : "FAIL");
                diagnostics.Append(", Score=");
                diagnostics.Append(result.Score.ToString());

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    diagnostics.Append(", Error=");
                    diagnostics.Append(result.ErrorMessage);
                }

                diagnostics.AppendLine();

                if (bestResult == null || result.Score > bestResult.Score)
                {
                    bestResult = result;
                }
            }

            if (bestResult == null)
            {
                return OcrTextReadResult.CreateFailure(EngineName, "사용 가능한 OCR 엔진이 없습니다.");
            }

            bestResult.Diagnostics = diagnostics.ToString();
            return bestResult;
        }
    }
}
