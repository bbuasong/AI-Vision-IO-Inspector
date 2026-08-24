using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

using System;
using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// AI 추론 호출 경계입니다. DLL, SDK, REST 등 실제 방식이 확정되면 구현체만 교체합니다.
    /// </summary>
    public interface IAiInferenceService
    {
        event EventHandler<TrainingProcessDataEventArgs> TrainingOutputReceived;

        event EventHandler<TrainingProcessDataEventArgs> TrainingErrorReceived;

        event EventHandler<TrainingProcessExitedEventArgs> TrainingExited;

        AiInferenceResult Inspect(Part part, IList<CapturedImage> capturedImages);

        string StartImageTraining();

        /// <summary>
        /// 지금 이미지 학습이 도는 중인지입니다.
        ///
        /// <para>
        /// 학습 중에는 검사를 할 수 없습니다. 예전에는 사진을 여섯 장 다 찍고 병합까지 만든 뒤에야
        /// 그 사실을 알렸습니다. 쓰지도 못할 파일이 디스크에 남았습니다. 찍기 전에 보려고 둡니다.
        /// </para>
        /// </summary>
        bool IsTrainingRunning { get; }

        /// <summary>
        /// 첫 검사가 느리지 않도록 AI 를 미리 한 번 깨워 둡니다. 뒤에서 돌며 부르는 쪽을 붙잡지 않습니다.
        /// </summary>
        void BeginWarmup();

        /// <summary>
        /// 실제 검사에 쓰는 품번과 사진으로 AI 를 미리 깨웁니다.
        /// </summary>
        void BeginWarmup(Part warmupPart, string imageFilePath);
    }
}
