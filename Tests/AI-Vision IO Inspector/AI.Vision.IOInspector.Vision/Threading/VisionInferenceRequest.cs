using System;
using System.Threading;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// AI 작업 스레드에서 처리할 추론 요청 1건입니다.
    /// 호출 흐름은 CompletedEvent로 결과를 기다리고, 무거운 AI 작업은 UI 스레드 밖에서 수행됩니다.
    /// </summary>
    internal class VisionInferenceRequest : IDisposable
    {
        public VisionInferenceRequest(VisionInspectionInput input)
        {
            Input = input;
            CompletedEvent = new ManualResetEvent(false);
        }

        public VisionInspectionInput Input { get; private set; }

        public VisionInspectionOutput Output { get; set; }

        public Exception Error { get; set; }

        public ManualResetEvent CompletedEvent { get; private set; }

        public void Dispose()
        {
            CompletedEvent.Dispose();
        }
    }
}
