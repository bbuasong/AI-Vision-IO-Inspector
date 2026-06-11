using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using AI.Vision.IOInspector.Vision.Services;

namespace AI.Vision.IOInspector.Vision
{
    /// <summary>
    /// Vision 프로젝트가 소유하는 카메라/AI 서비스 구현체를 생성합니다.
    /// UI와 ViewModel은 이 팩토리만 호출하고, 실제 카메라/AI 구현은 Vision 프로젝트 내부에서 교체합니다.
    /// </summary>
    public static class VisionRuntimeFactory
    {
        private static readonly VladSdkSession SharedVladSdkSession = new VladSdkSession();

        public static ICameraService CreateCameraService(string applicationRootPath)
        {
            VladVisionSettings settings = VladVisionSettings.Load(applicationRootPath);
            return new VisionCameraService(applicationRootPath, SharedVladSdkSession, settings);
        }

        public static IAiInferenceService CreateAiInferenceService(string applicationRootPath)
        {
            VladVisionSettings settings = VladVisionSettings.Load(applicationRootPath);

            IVisionInferenceEngine inferenceEngine = new VladVisionInferenceEngine(applicationRootPath, SharedVladSdkSession, settings);
            return new VisionAiInferenceService(inferenceEngine);
        }
    }
}
