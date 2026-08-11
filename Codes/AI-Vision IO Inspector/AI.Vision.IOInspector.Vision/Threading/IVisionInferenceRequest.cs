using System;
using System.Threading;
using AI.Vision.IOInspector.Vision.Engines;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// VLAD 네이티브 DLL을 호출하는 작업의 공통 계약입니다.
    /// 검사와 기준이미지 유사도 검색을 하나의 작업 스레드에서 순서대로 실행합니다.
    /// </summary>
    internal interface IVisionInferenceRequest : IDisposable
    {
        ManualResetEvent CompletedEvent { get; }

        Exception Error { get; set; }

        bool IsAbandoned { get; }

        void Abandon();

        void Process(IVisionInferenceEngine inferenceEngine);
    }
}
