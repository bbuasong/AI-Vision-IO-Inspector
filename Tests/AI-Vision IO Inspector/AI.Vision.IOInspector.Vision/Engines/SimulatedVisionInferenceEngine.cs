using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Engines
{
    /// <summary>
    /// 실제 AI 모델과 카메라 SDK가 연결되기 전에도 검사 흐름을 테스트할 수 있게 하는 시뮬레이션 엔진입니다.
    /// 기존 시뮬레이션 동작과 맞추기 위해 품번 04026346의 첫 번째 측정부는 의도적으로 NG가 되도록 만듭니다.
    /// </summary>
    public class SimulatedVisionInferenceEngine : IVisionInferenceEngine
    {
        public VisionInspectionOutput Inspect(VisionInspectionInput input)
        {
            VisionInspectionOutput output = new VisionInspectionOutput();
            output.IsSuccess = true;
            output.IsMatched = true;
            output.PredictedClass = input.Part.PartName;
            output.Confidence = 0.98m;
            output.Message = "Vision project simulation completed";
            output.ModelVersion = "simulation";

            foreach (MeasurementRegion region in input.Part.MeasurementRegions)
            {
                VisionMeasurementValue measurement = BuildMeasurement(input, region);
                output.Measurements.Add(measurement);
            }

            return output;
        }

        private VisionMeasurementValue BuildMeasurement(VisionInspectionInput input, MeasurementRegion region)
        {
            decimal measuredValue = region.NominalValue;
            if (input.Part.PartNo == "04026346" && region.Id == 1)
            {
                measuredValue = region.NominalValue + 1.2m;
            }
            else if (region.Id == 4)
            {
                measuredValue = region.NominalValue + 0.1m;
            }

            VisionMeasurementValue measurement = new VisionMeasurementValue();
            measurement.MeasurementRegionId = region.Id;
            measurement.Name = region.Name;
            measurement.ViewType = region.ViewType;
            measurement.Value = measuredValue;
            measurement.Unit = region.Unit;
            measurement.RawPixelValue = 0m;
            measurement.SourceImagePath = FindSourceImagePath(input, region);
            measurement.CalibrationId = string.Empty;
            return measurement;
        }

        private string FindSourceImagePath(VisionInspectionInput input, MeasurementRegion region)
        {
            foreach (CapturedImage image in input.CapturedImages)
            {
                if (image.ViewType == region.ViewType)
                {
                    return image.FilePath;
                }
            }

            return string.Empty;
        }
    }
}
