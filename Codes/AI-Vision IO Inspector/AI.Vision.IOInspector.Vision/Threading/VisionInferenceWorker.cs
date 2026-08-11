using System;
using System.Collections.Generic;
using System.Threading;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// AI 추론 요청을 전용 작업 스레드 하나에서 순차 실행합니다.
    /// 카메라 수신, 화면 표시, 추론 작업을 UI 스레드와 분리하기 위한 구조입니다.
    /// </summary>
    public class VisionInferenceWorker : IDisposable
    {
        private const int InferenceTimeoutMilliseconds = 180000;
        //private const int InferenceTimeoutMilliseconds = 60000;

        private readonly object _syncRoot;
        private readonly Queue<IVisionInferenceRequest> _requestQueue;
        private readonly AutoResetEvent _workSignal;
        private readonly IVisionInferenceEngine _inferenceEngine;
        private Thread _workerThread;
        private bool _stopRequested;
        private bool _disposeRequested;
        private bool _disposed;
        private bool _workSignalDisposed;
        private VisionWorkerState _state;
        private string _lastErrorMessage;

        public VisionInferenceWorker(IVisionInferenceEngine inferenceEngine)
        {
            _syncRoot = new object();
            _requestQueue = new Queue<IVisionInferenceRequest>();
            _workSignal = new AutoResetEvent(false);
            _inferenceEngine = inferenceEngine;
            _state = VisionWorkerState.Stopped;
            _lastErrorMessage = string.Empty;
        }

        public VisionWorkerState State
        {
            get { return _state; }
        }

        public string LastErrorMessage
        {
            get { return _lastErrorMessage; }
        }

        public void Start()
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (_state == VisionWorkerState.Running || _state == VisionWorkerState.Starting)
                {
                    return;
                }

                if (_workerThread != null && _workerThread.IsAlive)
                {
                    return;
                }

                _stopRequested = false;
                _state = VisionWorkerState.Starting;
                 _workerThread = new Thread(new ThreadStart(WorkerThreadProc));
                _workerThread.Name = "VisionInferenceWorker";
                _workerThread.IsBackground = true;
                _workerThread.Start();
            }
        }

        public void Stop()
        {
            Thread threadToJoin;
            bool stopped = true;
            lock (_syncRoot)
            {
                if (_state == VisionWorkerState.Stopped)
                {
                    return;
                }

                _state = VisionWorkerState.Stopping;
                _stopRequested = true;
                threadToJoin = _workerThread;
                SetWorkSignalIfAvailable();
            }

            if (threadToJoin != null && threadToJoin.IsAlive)
            {
                stopped = threadToJoin.Join(3000);
            }

            if (stopped)
            {
                lock (_syncRoot)
                {
                    _state = VisionWorkerState.Stopped;
                    _workerThread = null;
                }
            }
        }

        public VisionInspectionOutput Inspect(VisionInspectionInput input)
        {
            Start();

            VisionInferenceRequest request = new VisionInferenceRequest(input);
            try
            {
                EnqueueRequest(request);
                bool completed = request.CompletedEvent.WaitOne(InferenceTimeoutMilliseconds);
                if (!completed)
                {
                    request.Abandon();
                    SetLastErrorMessage("AI 추론 대기 시간이 초과되었습니다.");
                    SetState(VisionWorkerState.Faulted);
                    throw new TimeoutException("AI 추론 대기 시간이 초과되었습니다. VLAD_Custom_Registration 또는 VLAD_Inference_Mat 호출이 반환되지 않았을 가능성이 큽니다.");
                }

                if (request.Error != null)
                {
                    throw request.Error;
                }

                return request.Output;
            }
            finally
            {
                if (!request.IsAbandoned)
                {
                    request.Dispose();
                }
            }
        }

        /// <summary>
        /// 기준이미지 유사도 검색을 검사와 같은 Vision 작업 스레드에서 실행합니다.
        /// VLAD DLL은 동시에 두 요청을 처리하지 않는 전제로 직렬화합니다.
        /// </summary>
        public ReferenceImageSimilarityResult SearchReferenceImages(ReferenceImageSimilarityRequest input)
        {
            Start();

            VisionSimilaritySearchRequest request = new VisionSimilaritySearchRequest(input);
            try
            {
                EnqueueRequest(request);
                bool completed = request.CompletedEvent.WaitOne(InferenceTimeoutMilliseconds);
                if (!completed)
                {
                    request.Abandon();
                    SetLastErrorMessage("AI 유사도 검색 대기 시간이 초과되었습니다.");
                    SetState(VisionWorkerState.Faulted);
                    throw new TimeoutException("AI 유사도 검색 대기 시간이 초과되었습니다. VLAD_Search_Mat 또는 VLAD_Search_Data 호출이 반환되지 않았을 가능성이 있습니다.");
                }

                if (request.Error != null)
                {
                    throw request.Error;
                }

                return request.Result;
            }
            finally
            {
                if (!request.IsAbandoned)
                {
                    request.Dispose();
                }
            }
        }

        public string StartImageTraining()
        {
            Start();
            return _inferenceEngine.StartImageTraining();
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _disposeRequested = true;
            }

            Stop();
            DisposeWorkSignalIfWorkerStopped();
        }

        private void EnqueueRequest(IVisionInferenceRequest request)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (_stopRequested)
                {
                    throw new InvalidOperationException("Vision inference worker is stopping.");
                }

                _requestQueue.Enqueue(request);
                SetWorkSignalIfAvailable();
            }
        }

        // 계속 돌면서 요청을 기다리는 구조입니다.
        private void WorkerThreadProc()
        {
            try
            {
                SetState(VisionWorkerState.Running);

                while (true)
                {
                    IVisionInferenceRequest request = DequeueRequest();
                    if (request != null)
                    {
                        ProcessRequest(request);    // 실제 AI 추론 처리 담당
                        continue;
                    }

                    if (IsStopRequested())
                    {
                        break;
                    }

                    WaitForWorkSignal(100);
                }
            }
            catch (ObjectDisposedException)
            {
                if (!IsDisposeRequested())
                {
                    SetLastErrorMessage("AI 추론 Worker 신호 객체가 예기치 않게 Dispose되었습니다.");
                    SetState(VisionWorkerState.Faulted);
                }
            }
            finally
            {
                MarkWorkerStopped();
                DisposeWorkSignalIfWorkerStopped();
            }
        }

        private IVisionInferenceRequest DequeueRequest()
        {
            lock (_syncRoot)
            {
                if (_requestQueue.Count == 0)
                {
                    return null;
                }

                return _requestQueue.Dequeue();
            }
        }

        private void ProcessRequest(IVisionInferenceRequest request)
        {
            try
            {
                //일반 실행 모드의 비동기 검사 처리 담당
                // Eng->>Eng: EnsureRegistered() (최초 1회 VLAD_Ops_Ai_Env_Start 호출)
                request.Process(_inferenceEngine);
            }
            catch (Exception ex)
            {
                request.Error = ex;
                SetLastErrorMessage(ex.Message);
                SetState(VisionWorkerState.Faulted);
            }
            finally
            {
                try
                {
                    request.CompletedEvent.Set();
                }
                catch (ObjectDisposedException)
                {
                }

                if (request.IsAbandoned)
                {
                    request.Dispose();
                }
            }
        }

        private void WaitForWorkSignal(int millisecondsTimeout)
        {
            AutoResetEvent waitHandle;
            lock (_syncRoot)
            {
                if (_workSignalDisposed)
                {
                    return;
                }

                waitHandle = _workSignal;
            }

            waitHandle.WaitOne(millisecondsTimeout);
        }

        private bool IsStopRequested()
        {
            lock (_syncRoot)
            {
                return _stopRequested;
            }
        }

        private bool IsDisposeRequested()
        {
            lock (_syncRoot)
            {
                return _disposeRequested;
            }
        }

        private void SetState(VisionWorkerState state)
        {
            lock (_syncRoot)
            {
                _state = state;
            }
        }

        private void SetLastErrorMessage(string message)
        {
            lock (_syncRoot)
            {
                _lastErrorMessage = message;
            }
        }

        private void SetWorkSignalIfAvailable()
        {
            if (!_workSignalDisposed)
            {
                _workSignal.Set();
            }
        }

        private void MarkWorkerStopped()
        {
            lock (_syncRoot)
            {
                _state = VisionWorkerState.Stopped;
                if (Thread.CurrentThread == _workerThread)
                {
                    _workerThread = null;
                }
            }
        }

        private void DisposeWorkSignalIfWorkerStopped()
        {
            bool shouldDispose;
            lock (_syncRoot)
            {
                shouldDispose = _disposeRequested
                                && !_workSignalDisposed
                                && (_workerThread == null || !_workerThread.IsAlive);
                if (shouldDispose)
                {
                    _workSignalDisposed = true;
                }
            }

            if (shouldDispose)
            {
                _workSignal.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}
