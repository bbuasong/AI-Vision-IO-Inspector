using System;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Engines
{
    /// <summary>
    /// AI 담당자가 구현해야 하는 추론 엔진 계약입니다.
    /// VLAD DLL, ONNX, OpenCV 또는 별도 카메라/AI 파이프라인은 이 인터페이스 뒤에 구현합니다.
    /// Inspect()를 직접 실행하는 곳이라기보다, 이런 형태의 Inspect 함수가 반드시 있어야 한다고 정의한 곳입니다.
    /// </summary>
    public interface IVisionInferenceEngine
    {
        event EventHandler<TrainingProcessDataEventArgs> TrainingOutputReceived;

        event EventHandler<TrainingProcessDataEventArgs> TrainingErrorReceived;

        event EventHandler<TrainingProcessExitedEventArgs> TrainingExited;

        VisionInspectionOutput Inspect(VisionInspectionInput input);

        /// <summary>
        /// 첫 검사가 느리지 않도록 AI 를 미리 한 번 깨워 둡니다.
        ///
        /// <para>
        /// 사람이 기다리지 않는 시작 직후에 부릅니다. 실패해도 검사에는 지장이 없습니다.
        /// </para>
        /// </summary>
        void Warmup();

        /// <summary>지금 이미지 학습이 도는 중인지입니다. 검사를 시작하기 전에 봅니다.</summary>
        bool IsTrainingRunning { get; }

        /// <summary>
        /// 등록 기준이미지를 기준으로 VLAD 유사도 검색을 실행합니다.
        /// 실제 DLL 호출은 VLAD_Search_Mat / VLAD_Search_Data export를 사용합니다.
        /// </summary>
        ReferenceImageSimilarityResult SearchReferenceImages(ReferenceImageSimilarityRequest request);

        string StartImageTraining();
    }
}
