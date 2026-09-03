using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// AI에서 받은 측정값을 기준값/허용오차와 비교합니다.
    /// 부품별 측정부가 동적으로 늘어날 수 있으므로 MeasurementRegion 목록을 기준으로 계산합니다.
    ///
    /// <para>
    /// 역할 분담이 명확합니다.
    ///   AI(VLAD)  측정값(measuredValue)과 View 단위 최종 판정(viewJudge)을 줍니다.
    ///   여기(C#)  받은 측정값을 기준정보와 비교해 측정부별 합불을 스스로 판단합니다.
    /// </para>
    ///
    /// <para>
    /// 측정부별 판정은 <b>최종 합불에 관여하지 않습니다</b>. 최종 판정은 카메라(View) 단위로
    /// AI의 viewJudge를 따릅니다(JudgmentService 참고). 여기서 내는 판정은 통계와 이력에서
    /// "어느 측정부가 어느 정도로 벗어났는지" 남기기 위한 내부 기록입니다.
    /// </para>
    ///
    /// <para>
    /// 예전에는 AI가 측정부별 judge를 준다고 보고 그 값에 의존했습니다. 그러나 계약상
    /// measurements에는 indexNo와 measuredValue만 있어 judge가 오지 않고, 그 결과
    /// 모든 측정부가 "AI 측정부 판정 없음"과 함께 불합격으로 기록되고 있었습니다.
    /// </para>
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

                // 판정은 사용자가 좌표를 긋고 기준값을 넣은 측정부에만 합니다.
                //
                // 현장에서 기준값 등록이 어려워, 좌표만 있거나 기준값만 있거나 둘 다 없는
                // 측정부는 AI 가 참고값(measuredValue)만 만들어 줍니다. 그런 값을 기준값 0 과
                // 견주면 무조건 FAIL 이 되므로, 판정하지 않고 값만 기록합니다.
                // 기준값은 우리 DB 의 값만 씁니다. AI 가 JSON 으로 다른 기준값을 돌려주더라도
                // 판정 여부가 바뀌면 안 됩니다.
                bool isJudgable = region.HasDrawnCoordinates && region.NominalValue != 0m;

                // 허용오차는 부호가 아니라 크기로 해석합니다.
                // 저장된 Min이 양수이거나 Max가 음수로 잘못 들어와도 범위가 뒤집히지 않도록
                // MeasurementRegion의 LowerLimit/UpperLimit만 사용합니다.
                bool isPass = !isJudgable || (hasMeasurementValue && region.IsWithinTolerance(measuredValue));
                decimal deviation = isJudgable && hasMeasurementValue ? CalculateDeviation(region, measuredValue) : 0m;

                MeasurementResult result = new MeasurementResult();
                result.MeasurementRegionId = region.Id;

                // 판정하지 않는 측정부의 기준값·허용오차 표시는 AI 가 정해 준 값을 씁니다.
                // 우리 DB 값은 비어 있거나(0) 무의미해서, 그대로 적으면 측정값이 크게
                // 벗어난 것처럼 읽힙니다. 판정 자체는 위에서 이미 하지 않기로 정해졌으므로
                // 이 값은 보여 주기에만 쓰입니다.
                decimal displayNominal = region.NominalValue;
                decimal displayToleranceMin = region.ToleranceMin;
                decimal displayToleranceMax = region.ToleranceMax;
                if (!isJudgable && inferenceResult.AiNominalValues.ContainsKey(region.Id))
                {
                    displayNominal = inferenceResult.AiNominalValues[region.Id];
                    displayToleranceMin = inferenceResult.AiToleranceMins[region.Id];
                    displayToleranceMax = inferenceResult.AiToleranceMaxs[region.Id];
                }

                // 이름과 단위는 이력 테이블에서 NOT NULL입니다.
                // 여기서 null이 넘어가면 저장 시점에 제약 위반이 나고,
                // 측정부 한 줄 때문에 검사 이력 전체가 저장되지 않습니다.
                result.Name = region.Name == null ? string.Empty : region.Name;
                result.NominalValue = displayNominal;
                result.MeasuredValue = measuredValue;
                result.ToleranceMin = displayToleranceMin;
                result.ToleranceMax = displayToleranceMax;
                result.Unit = region.Unit == null ? string.Empty : region.Unit;
                result.IsPass = isPass;
                result.IsJudged = isJudgable;
                result.Deviation = deviation;
                result.Message = isJudgable
                    ? BuildMessage(region, measuredValue, hasMeasurementValue, isPass, deviation)
                    : (hasMeasurementValue ? "참고값 (판정 없음)" : "AI 측정값 없음 (판정 없음)");

                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// 허용 범위를 벗어난 크기를 구합니다.
        /// 범위 안이면 0, 하한 미달이면 음수, 상한 초과면 양수입니다.
        /// </summary>
        private static decimal CalculateDeviation(MeasurementRegion region, decimal measuredValue)
        {
            if (measuredValue < region.LowerLimit)
            {
                return measuredValue - region.LowerLimit;
            }

            if (measuredValue > region.UpperLimit)
            {
                return measuredValue - region.UpperLimit;
            }

            return 0m;
        }

        /// <summary>
        /// 이력에서 그대로 읽을 수 있도록 벗어난 방향과 크기를 함께 적습니다.
        /// </summary>
        private static string BuildMessage(
            MeasurementRegion region,
            decimal measuredValue,
            bool hasMeasurementValue,
            bool isPass,
            decimal deviation)
        {
            if (!hasMeasurementValue)
            {
                return "AI 측정값 없음";
            }

            if (isPass)
            {
                return "기준 범위 내";
            }

            string unit = string.IsNullOrWhiteSpace(region.Unit) ? string.Empty : region.Unit;

            if (deviation < 0m)
            {
                return "하한 미달 " + FormatValue(-deviation) + unit +
                       " (하한 " + FormatValue(region.LowerLimit) + unit +
                       ", 측정 " + FormatValue(measuredValue) + unit + ")";
            }

            return "상한 초과 " + FormatValue(deviation) + unit +
                   " (상한 " + FormatValue(region.UpperLimit) + unit +
                   ", 측정 " + FormatValue(measuredValue) + unit + ")";
        }

        private static string FormatValue(decimal value)
        {
            return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
