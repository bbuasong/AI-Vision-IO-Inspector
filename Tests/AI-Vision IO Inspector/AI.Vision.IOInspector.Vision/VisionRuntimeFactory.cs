using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.Services;

namespace AI.Vision.IOInspector.Vision
{
    /// <summary>
    /// Vision 프로젝트가 소유하는 카메라/AI 서비스 구현체를 생성합니다.
    /// 앱 계층은 이 팩토리만 호출하므로 AI/카메라 작업 범위를 Vision 프로젝트 안에 묶어둘 수 있습니다.
    /// </summary>
    public static class VisionRuntimeFactory
    {
        public static ICameraService CreateCameraService(string applicationRootPath)
        {
            return new VisionCameraService(applicationRootPath);
        }

        public static IAiInferenceService CreateAiInferenceService(string applicationRootPath)
        {
            IVisionInferenceEngine inferenceEngine = new SimulatedVisionInferenceEngine();
            return new VisionAiInferenceService(inferenceEngine);
        }
    }
}
