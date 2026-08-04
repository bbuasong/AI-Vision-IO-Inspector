using System;
using System.Threading;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// AI 작업 스레드에서 처리할 추론 요청 1건입니다.
    /// 호출 흐름은 CompletedEvent로 결과를 기다리고, 무거운 AI 작업은 UI 스레드 밖에서 수행됩니다.
    /// </summary>
    internal class VisionInferenceRequest : IVisionInferenceRequest
    {
        private readonly object _syncRoot;
        private readonly ManualResetEvent _completedEvent;
        private bool _isAbandoned;

        public VisionInferenceRequest(VisionInspectionInput input)
        {
            _syncRoot = new object();
            Input = input;
            _completedEvent = new ManualResetEvent(false);
        }

        public VisionInspectionInput Input { get; private set; }

        public VisionInspectionOutput Output { get; set; }

        public Exception Error { get; set; }

        public ManualResetEvent CompletedEvent
        {
            get { return _completedEvent; }
        }

        public bool IsAbandoned
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isAbandoned;
                }
            }
        }

        public void Abandon()
        {
            lock (_syncRoot)
            {
                _isAbandoned = true;
            }
        }

        /// <summary>
        /// 검사 요청을 Vision 전용 작업 스레드에서 실행합니다.
        /// UI 스레드에서는 이 메서드를 직접 호출하지 않습니다.
        /// </summary>
        public void Process(IVisionInferenceEngine inferenceEngine)
        {
            Output = inferenceEngine.Inspect(Input);
        }

        public void Dispose()
        {
            _completedEvent.Dispose();
        }
    }
}
