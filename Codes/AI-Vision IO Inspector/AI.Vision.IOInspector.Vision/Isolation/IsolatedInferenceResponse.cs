using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Isolation
{
    /// <summary>
    /// 외부 VLAD 추론 프로세스가 WPF 앱으로 돌려주는 결과 파일 모델입니다.
    /// </summary>
    public class IsolatedInferenceResponse
    {
        public IsolatedInferenceResponse()
        {
            Measurements = new List<IsolatedMeasurementValueDto>();
        }

        public bool IsSuccess { get; set; }

        public bool IsMatched { get; set; }

        public string PredictedClass { get; set; }

        public decimal Confidence { get; set; }

        public string Message { get; set; }

        public string ModelVersion { get; set; }

        public IList<IsolatedMeasurementValueDto> Measurements { get; set; }

        public static IsolatedInferenceResponse FromVisionOutput(VisionInspectionOutput output)
        {
            IsolatedInferenceResponse response = new IsolatedInferenceResponse();
            if (output == null)
            {
                response.IsSuccess = false;
                response.IsMatched = false;
                response.Message = "VLAD 추론 결과가 비어 있습니다.";
                response.ModelVersion = "VLAD";
                return response;
            }

            response.IsSuccess = output.IsSuccess;
            response.IsMatched = output.IsMatched;
            response.PredictedClass = output.PredictedClass;
            response.Confidence = output.Confidence;
            response.Message = output.Message;
            response.ModelVersion = output.ModelVersion;

            if (output.Measurements != null)
            {
                foreach (VisionMeasurementValue measurement in output.Measurements)
                {
                    response.Measurements.Add(IsolatedMeasurementValueDto.FromVisionMeasurementValue(measurement));
                }
            }

            return response;
        }

        public AiInferenceResult ToAiInferenceResult()
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = IsSuccess;
            result.IsMatched = IsMatched;
            result.PredictedClass = PredictedClass;
            result.Confidence = Confidence;
            result.Message = Message;
            result.ModelVersion = string.IsNullOrWhiteSpace(ModelVersion) ? "VLAD" : ModelVersion;

            if (Measurements != null)
            {
                foreach (IsolatedMeasurementValueDto measurement in Measurements)
                {
                    if (measurement == null)
                    {
                        continue;
                    }

                    result.MeasurementValues[measurement.MeasurementRegionId] = measurement.Value;
                    result.MeasurementUnits[measurement.MeasurementRegionId] = measurement.Unit;
                    result.RawPixelValues[measurement.MeasurementRegionId] = measurement.RawPixelValue;
                }
            }

            return result;
        }

        public static IsolatedInferenceResponse CreateFailure(string message)
        {
            IsolatedInferenceResponse response = new IsolatedInferenceResponse();
            response.IsSuccess = false;
            response.IsMatched = false;
            response.PredictedClass = string.Empty;
            response.Confidence = 0m;
            response.Message = message;
            response.ModelVersion = "VLAD";
            return response;
        }
    }
}
