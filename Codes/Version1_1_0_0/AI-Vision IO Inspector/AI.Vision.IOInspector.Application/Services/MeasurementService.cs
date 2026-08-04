using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// AI 또는 측정 알고리즘에서 받은 측정값을 기준값/허용오차와 비교합니다.
    /// 부품별 측정부가 동적으로 늘어날 수 있으므로 MeasurementRegion 목록을 기준으로 계산합니다.
    /// </summary>
    public class MeasurementService
    {
        public IList<MeasurementResult> CompareMeasurements(Part part, AiInferenceResult inferenceResult)
        {
            IList<MeasurementResult> results = new List<MeasurementResult>();

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                bool hasMeasurementValue = inferenceResult.MeasurementValues.ContainsKey(region.Id);
                decimal measuredValue = 0m;
                if (hasMeasurementValue)
                {
                    measuredValue = inferenceResult.MeasurementValues[region.Id];
                }

                bool hasAiJudge = inferenceResult.MeasurementJudgments.ContainsKey(region.Id);
                bool isOk;
                if (inferenceResult.HasAuthoritativeJudgment)
                {
                    // 신규 HD 계약에서는 허용오차를 C#에서 다시 판단하지 않고 AI의 측정부별 judge를 사용합니다.
                    isOk = hasMeasurementValue && hasAiJudge && inferenceResult.MeasurementJudgments[region.Id];
                }
                else
                {
                    decimal minValue = region.NominalValue + region.ToleranceMin;
                    decimal maxValue = region.NominalValue + region.ToleranceMax;
                    isOk = hasMeasurementValue && measuredValue >= minValue && measuredValue <= maxValue;
                }

                MeasurementResult result = new MeasurementResult();
                result.MeasurementRegionId = region.Id;
                result.Name = region.Name;
                result.NominalValue = region.NominalValue;
                result.MeasuredValue = measuredValue;
                result.ToleranceMin = region.ToleranceMin;
                result.ToleranceMax = region.ToleranceMax;
                result.Unit = region.Unit;
                result.IsOk = isOk;
                if (!hasMeasurementValue)
                {
                    result.Message = "AI 측정값 없음";
                }
                else if (inferenceResult.HasAuthoritativeJudgment && !hasAiJudge)
                {
                    result.Message = "AI 측정부 판정 없음";
                }
                else if (inferenceResult.HasAuthoritativeJudgment)
                {
                    result.Message = inferenceResult.MeasurementJudgeTexts.ContainsKey(region.Id)
                        ? "AI 판정 " + inferenceResult.MeasurementJudgeTexts[region.Id]
                        : (isOk ? "AI 판정 PASS" : "AI 판정 FAIL");
                }
                else
                {
                    result.Message = isOk ? "기준 범위 내" : "기준 범위 초과";
                }

                results.Add(result);
            }

            return results;
        }
    }
}
