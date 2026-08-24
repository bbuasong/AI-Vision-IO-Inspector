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
        /// 첫 검사가 느리지 않도록 AI 를 미리 한 번 깨워 둡니다. 뒤에서 돌며 부르는 쪽을 붙잡지 않습니다.
        /// </summary>
        void BeginWarmup();
    }
}
