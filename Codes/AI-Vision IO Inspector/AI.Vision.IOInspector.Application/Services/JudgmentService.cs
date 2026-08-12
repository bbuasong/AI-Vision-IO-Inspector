using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// 하나의 검사 실행에서 나온 이미지 AI 검사 결과와 기준값 비교 결과를 함께 보고 최종 PASS/FAIL을 판단합니다.
    /// 시스템 오류는 InspectionWorkflowService에서 Error로 분리 처리합니다.
    /// </summary>
    public class JudgmentService
    {
        public InspectionResult Judge(AiInferenceResult inferenceResult, IList<MeasurementResult> measurements)
        {
            return Judge(inferenceResult, measurements, 95m);
        }

        /// <summary>
        /// 이미지 AI의 일치 여부, 실제 Score 기준, 측정값 기준 비교를 함께 적용해 최종 판정합니다.
        /// AI DLL이 Score를 반환하지 않은 경우에는 기존 IsMatched 판정을 유지합니다.
        /// </summary>
        public InspectionResult Judge(
            AiInferenceResult inferenceResult,
            IList<MeasurementResult> measurements,
            decimal inspectionPassScoreThreshold)
        {
            if (inferenceResult.HasAuthoritativeJudgment)
            {
                return inferenceResult.IsMatched ? InspectionResult.Pass : InspectionResult.Fail;
            }

            if (!inferenceResult.IsMatched)
            {
                return InspectionResult.Fail;
            }

            if (inferenceResult.HasScore && GetDisplayScore(inferenceResult.Confidence) < inspectionPassScoreThreshold)
            {
                return InspectionResult.Fail;
            }

            foreach (MeasurementResult measurement in measurements)
            {
                if (!measurement.IsPass)
                {
                    return InspectionResult.Fail;
                }
            }

            return InspectionResult.Pass;
        }

        public string BuildResultMessage(InspectionResult result, AiInferenceResult inferenceResult, IList<MeasurementResult> measurements)
        {
            return BuildResultMessage(result, inferenceResult, measurements, 95m);
        }

        /// <summary>
        /// 최종 판정 메시지에 Score 기준 미달 원인을 우선 표시합니다.
        /// </summary>
        public string BuildResultMessage(
            InspectionResult result,
            AiInferenceResult inferenceResult,
            IList<MeasurementResult> measurements,
            decimal inspectionPassScoreThreshold)
        {
            if (inferenceResult.HasAuthoritativeJudgment)
            {
                if (!string.IsNullOrWhiteSpace(inferenceResult.Message))
                {
                    return inferenceResult.Message;
                }

                return result == InspectionResult.Pass
                    ? "AI 최종 판정 PASS"
                    : "AI 최종 판정 FAIL";
            }

            if (result == InspectionResult.Pass)
            {
                return "이미지 AI 검사와 기준값 비교 결과가 모두 일치합니다.";
            }

            if (!inferenceResult.IsMatched)
            {
                return "AI 추론 결과가 등록 부품과 일치하지 않습니다.";
            }

            if (inferenceResult.HasScore && GetDisplayScore(inferenceResult.Confidence) < inspectionPassScoreThreshold)
            {
                return "AI Score " + GetDisplayScore(inferenceResult.Confidence).ToString("0.##") +
                       "점이 기준 Score " + inspectionPassScoreThreshold.ToString("0.##") + "점 미만입니다.";
            }

            foreach (MeasurementResult measurement in measurements)
            {
                if (!measurement.IsPass)
                {
                    return measurement.Name + " 측정값이 기준 범위를 벗어났습니다.";
                }
            }

            return "검사 결과 확인이 필요합니다.";
        }

        private decimal GetDisplayScore(decimal confidence)
        {
            return confidence >= 0m && confidence <= 1m
                ? confidence * 100m
                : confidence;
        }
    }
}
