using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// 하나의 검사 실행에서 나온 이미지 AI 검사 결과와 기준값 비교 결과를 함께 보고 최종 OK/NG를 판단합니다.
    /// 시스템 오류는 InspectionWorkflowService에서 Error로 분리 처리합니다.
    /// </summary>
    public class JudgmentService
    {
        public InspectionResult Judge(AiInferenceResult inferenceResult, IList<MeasurementResult> measurements)
        {
            if (!inferenceResult.IsMatched)
            {
                return InspectionResult.Ng;
            }

            foreach (MeasurementResult measurement in measurements)
            {
                if (!measurement.IsOk)
                {
                    return InspectionResult.Ng;
                }
            }

            return InspectionResult.Ok;
        }

        public string BuildResultMessage(InspectionResult result, AiInferenceResult inferenceResult, IList<MeasurementResult> measurements)
        {
            if (result == InspectionResult.Ok)
            {
                return "이미지 AI 검사와 기준값 비교 결과가 모두 일치합니다.";
            }

            if (!inferenceResult.IsMatched)
            {
                return "AI 추론 결과가 등록 부품과 일치하지 않습니다.";
            }

            foreach (MeasurementResult measurement in measurements)
            {
                if (!measurement.IsOk)
                {
                    return measurement.Name + " 측정값이 기준 범위를 벗어났습니다.";
                }
            }

            return "검사 결과 확인이 필요합니다.";
        }
    }
}
