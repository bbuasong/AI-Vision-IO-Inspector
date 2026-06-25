using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.Models;
using AI.Vision.IOInspector.Vision.Threading;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// Vision 프로젝트의 추론 엔진 결과를 애플리케이션 계층의 AI 결과 계약으로 변환합니다.
    /// 실제 AI 담당자 작업은 ViewModel을 수정하지 말고 IVisionInferenceEngine 구현체에 연결하는 방향을 기본으로 합니다.
    /// </summary>
    public class VisionAiInferenceService : IAiInferenceService
    {
        private readonly VisionInferenceWorker _inferenceWorker;

        public VisionAiInferenceService(IVisionInferenceEngine inferenceEngine)
        {
            _inferenceWorker = new VisionInferenceWorker(inferenceEngine);
            _inferenceWorker.Start();
        }

        public AiInferenceResult Inspect(Part part, IList<CapturedImage> capturedImages)
        {
            try
            {
                VisionInspectionInput input = BuildInput(part, capturedImages);
                VisionInspectionOutput output = _inferenceWorker.Inspect(input);
                if (output == null)
                {
                    return CreateFailureResult("AI 추론 결과가 비어 있습니다.");
                }

                return ConvertToApplicationResult(output);
            }
            catch (Exception ex)
            {
                // VLAD SDK/Worker 오류는 검사 흐름을 죽이지 않고 검사 결과 로그로 반환합니다.
                return CreateFailureResult("AI 추론 실행 실패: " + ex.Message);
            }
        }

        private VisionInspectionInput BuildInput(Part part, IList<CapturedImage> capturedImages)
        {
            VisionInspectionInput input = new VisionInspectionInput();
            input.Part = part;
            input.LoadMeasurementPointsFromPart();

            if (capturedImages != null)
            {
                foreach (CapturedImage image in capturedImages)
                {
                    input.CapturedImages.Add(image);
                }
            }

            return input;
        }

        private AiInferenceResult ConvertToApplicationResult(VisionInspectionOutput output)
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = output.IsSuccess;
            result.IsMatched = output.IsMatched;
            result.PredictedClass = output.PredictedClass;
            result.Confidence = output.Confidence;
            result.Message = output.Message;
            result.ModelVersion = output.ModelVersion;

            foreach (VisionMeasurementValue measurement in output.Measurements)
            {
                result.MeasurementValues[measurement.MeasurementRegionId] = measurement.Value;
                result.MeasurementUnits[measurement.MeasurementRegionId] = measurement.Unit;
                result.RawPixelValues[measurement.MeasurementRegionId] = measurement.RawPixelValue;
            }

            return result;
        }

        private AiInferenceResult CreateFailureResult(string message)
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = false;
            result.IsMatched = false;
            result.PredictedClass = string.Empty;
            result.Confidence = 0m;
            result.Message = message;
            result.ModelVersion = "VLAD";
            return result;
        }
    }
}
