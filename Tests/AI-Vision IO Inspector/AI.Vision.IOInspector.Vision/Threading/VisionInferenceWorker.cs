using System;
using System.Collections.Generic;
using System.Threading;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// AI 추론을 전용 작업 스레드 하나에서 실행합니다.
    /// 기존 VLAD/IMV 코드처럼 카메라 수신, 화면 표시, 추론 작업을 UI 스레드와 분리하기 위한 구조입니다.
    /// </summary>
    public class VisionInferenceWorker : IDisposable
    {
        private readonly object _syncRoot;
        private readonly Queue<VisionInferenceRequest> _requestQueue;
        private readonly AutoResetEvent _workSignal;
        private readonly IVisionInferenceEngine _inferenceEngine;
        private Thread _workerThread;
        private bool _stopRequested;
        private VisionWorkerState _state;
        private string _lastErrorMessage;

        public VisionInferenceWorker(IVisionInferenceEngine inferenceEngine)
        {
            _syncRoot = new object();
            _requestQueue = new Queue<VisionInferenceRequest>();
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
                if (_state == VisionWorkerState.Running || _state == VisionWorkerState.Starting)
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
            lock (_syncRoot)
            {
                if (_state == VisionWorkerState.Stopped)
                {
                    return;
                }

                _state = VisionWorkerState.Stopping;
                _stopRequested = true;
                threadToJoin = _workerThread;
                _workSignal.Set();
            }

            if (threadToJoin != null && threadToJoin.IsAlive)
            {
                threadToJoin.Join(3000);
            }

            lock (_syncRoot)
            {
                _state = VisionWorkerState.Stopped;
                _workerThread = null;
            }
        }

        public VisionInspectionOutput Inspect(VisionInspectionInput input)
        {
            Start();

            using (VisionInferenceRequest request = new VisionInferenceRequest(input))
            {
                EnqueueRequest(request);
                request.CompletedEvent.WaitOne();

                if (request.Error != null)
                {
                    throw request.Error;
                }

                return request.Output;
            }
        }

        public void Dispose()
        {
            Stop();
            _workSignal.Dispose();
        }

        private void EnqueueRequest(VisionInferenceRequest request)
        {
            lock (_syncRoot)
            {
                if (_stopRequested)
                {
                    throw new InvalidOperationException("Vision inference worker is stopping.");
                }

                _requestQueue.Enqueue(request);
                _workSignal.Set();
            }
        }

        private void WorkerThreadProc()
        {
            SetState(VisionWorkerState.Running);

            while (true)
            {
                VisionInferenceRequest request = DequeueRequest();
                if (request != null)
                {
                    ProcessRequest(request);
                    continue;
                }

                if (IsStopRequested())
                {
                    break;
                }

                _workSignal.WaitOne(100);
            }

            SetState(VisionWorkerState.Stopped);
        }

        private VisionInferenceRequest DequeueRequest()
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

        private void ProcessRequest(VisionInferenceRequest request)
        {
            try
            {
                request.Output = _inferenceEngine.Inspect(request.Input);
            }
            catch (Exception ex)
            {
                request.Error = ex;
                _lastErrorMessage = ex.Message;
                SetState(VisionWorkerState.Faulted);
            }
            finally
            {
                request.CompletedEvent.Set();
            }
        }

        private bool IsStopRequested()
        {
            lock (_syncRoot)
            {
                return _stopRequested;
            }
        }

        private void SetState(VisionWorkerState state)
        {
            lock (_syncRoot)
            {
                _state = state;
            }
        }
    }
}
