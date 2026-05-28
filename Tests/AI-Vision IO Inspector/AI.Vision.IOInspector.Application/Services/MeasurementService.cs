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
                decimal measuredValue = region.NominalValue;
                if (inferenceResult.MeasurementValues.ContainsKey(region.Id))
                {
                    measuredValue = inferenceResult.MeasurementValues[region.Id];
                }

                decimal minValue = region.NominalValue + region.ToleranceMin;
                decimal maxValue = region.NominalValue + region.ToleranceMax;
                bool isOk = measuredValue >= minValue && measuredValue <= maxValue;

                MeasurementResult result = new MeasurementResult();
                result.MeasurementRegionId = region.Id;
                result.Name = region.Name;
                result.NominalValue = region.NominalValue;
                result.MeasuredValue = measuredValue;
                result.ToleranceMin = region.ToleranceMin;
                result.ToleranceMax = region.ToleranceMax;
                result.Unit = region.Unit;
                result.IsOk = isOk;
                result.Message = isOk ? "기준 범위 내" : "기준 범위 초과";
                results.Add(result);
            }

            return results;
        }
    }
}
