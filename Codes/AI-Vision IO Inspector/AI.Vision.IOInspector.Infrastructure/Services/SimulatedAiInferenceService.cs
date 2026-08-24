using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;

using System;
using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// AI 모델 연동 전 검사 판정 흐름을 확인하기 위한 시뮬레이션 추론 서비스입니다.
    /// 04026346 부품은 첫 번째 측정부를 일부러 기준 범위 밖으로 만들어 NG 표시를 검증합니다.
    /// </summary>
    public class SimulatedAiInferenceService : IAiInferenceService, IInspectionScoreSettings
    {
        public event EventHandler<TrainingProcessDataEventArgs> TrainingOutputReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<TrainingProcessDataEventArgs> TrainingErrorReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<TrainingProcessExitedEventArgs> TrainingExited
        {
            add { }
            remove { }
        }

        public AiInferenceResult Inspect(Part part, IList<CapturedImage> capturedImages)
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = true;
            result.IsMatched = true;
            result.HasScore = true;
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

        /// <summary>
        /// 시뮬레이션은 고정 결과를 사용하지만 실제 Vision 서비스와 같은 설정 인터페이스를 제공합니다.
        /// 최종 Score 판정은 InspectionWorkflowService가 동일하게 수행합니다.
        /// </summary>
        public void SetInspectionPassScoreThreshold(decimal scoreThreshold)
        {
        }

        /// <summary>
        /// 흉내내기에는 깨울 것이 없습니다. 규약을 맞추기 위해 자리만 둡니다.
        /// </summary>
        /// <summary>흉내내기에는 학습이 없습니다.</summary>
        public bool IsTrainingRunning
        {
            get { return false; }
        }

        public void BeginWarmup()
        {
        }

        public void BeginWarmup(Part warmupPart, string imageFilePath)
        {
        }

        public string StartImageTraining()
        {
            return "시뮬레이션 이미지 학습 시작 이벤트를 수신했습니다.";
        }
    }
}
