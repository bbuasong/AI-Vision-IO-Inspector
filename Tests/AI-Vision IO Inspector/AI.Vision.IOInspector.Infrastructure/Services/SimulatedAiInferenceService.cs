using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// AI 모델 연동 전 검사 판정 흐름을 확인하기 위한 시뮬레이션 추론 서비스입니다.
    /// 04026346 부품은 첫 번째 측정부를 일부러 기준 범위 밖으로 만들어 NG 표시를 검증합니다.
    /// </summary>
    public class SimulatedAiInferenceService : IAiInferenceService
    {
        public AiInferenceResult Inspect(Part part, IList<CapturedImage> capturedImages)
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = true;
            result.IsMatched = true;
            result.PredictedClass = part.PartName;
            result.Confidence = 0.98m;
            result.Message = "시뮬레이션 AI 추론 완료";

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                decimal measuredValue = region.NominalValue;
                if (part.PartNo == "04026346" && region.Id == 1)
                {
                    measuredValue = region.NominalValue + 1.2m;
                }
                else if (region.Id == 4)
                {
                    measuredValue = region.NominalValue + 0.1m;
                }

                result.MeasurementValues[region.Id] = measuredValue;
            }

            return result;
        }

        public string StartImageTraining()
        {
            return "시뮬레이션 이미지 학습 시작 이벤트를 수신했습니다.";
        }
    }
}
