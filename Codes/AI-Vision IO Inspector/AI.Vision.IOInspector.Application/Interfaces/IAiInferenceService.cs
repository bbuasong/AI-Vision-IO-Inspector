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
    }
}
