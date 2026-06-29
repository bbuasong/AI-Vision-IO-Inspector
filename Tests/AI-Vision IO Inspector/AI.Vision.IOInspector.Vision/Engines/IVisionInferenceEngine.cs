using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Engines
{
    /// <summary>
    /// AI 담당자가 구현해야 하는 추론 엔진 계약입니다.
    /// VLAD DLL, ONNX, OpenCV 또는 별도 카메라/AI 파이프라인은 이 인터페이스 뒤에 구현합니다.
    /// </summary>
    public interface IVisionInferenceEngine
    {
        VisionInspectionOutput Inspect(VisionInspectionInput input);

        string StartImageTraining();
    }
}
