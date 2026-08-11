using System;
using System.Threading;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Vision.Engines;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// 기준이미지 유사도 검색 요청 1건입니다.
    /// 검사 요청과 동일한 Vision 작업 스레드에서 실행되어 VLAD DLL 호출을 직렬화합니다.
    /// </summary>
    internal class VisionSimilaritySearchRequest : IVisionInferenceRequest
    {
        private readonly object _syncRoot;
        private readonly ManualResetEvent _completedEvent;
        private bool _isAbandoned;

        public VisionSimilaritySearchRequest(ReferenceImageSimilarityRequest input)
        {
            _syncRoot = new object();
            Input = input;
            _completedEvent = new ManualResetEvent(false);
        }

        public ReferenceImageSimilarityRequest Input { get; private set; }

        public ReferenceImageSimilarityResult Result { get; set; }

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
        /// 유사도 검색 요청을 Vision 전용 작업 스레드에서 실행합니다.
        /// </summary>
        public void Process(IVisionInferenceEngine inferenceEngine)
        {
            Result = inferenceEngine.SearchReferenceImages(Input);
        }

        public void Dispose()
        {
            _completedEvent.Dispose();
        }
    }
}
