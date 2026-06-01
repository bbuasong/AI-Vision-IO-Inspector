using System;
using System.Collections.Generic;
using System.Threading;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// 한 방향의 카메라 촬영 요청을 전용 Worker Thread에서 순차 처리합니다.
    /// 기존 VLAD/IMV 코드의 카메라별 Thread 구조를 현재 WPF/MVVM 구조에 맞게 옮긴 뼈대입니다.
    /// </summary>
    public class VisionCameraCaptureWorker : IDisposable
    {
        private readonly object _syncRoot;
        private readonly Queue<VisionCameraCaptureRequest> _requestQueue;
        private readonly AutoResetEvent _workSignal;
        private readonly IVisionCameraCaptureExecutor _captureExecutor;
        private readonly ImageViewType _viewType;
        private Thread _workerThread;
        private bool _stopRequested;
        private VisionWorkerState _state;
        private string _lastErrorMessage;
        private CapturedImage _latestImage;

        public VisionCameraCaptureWorker(ImageViewType viewType, IVisionCameraCaptureExecutor captureExecutor)
        {
            _syncRoot = new object();
            _requestQueue = new Queue<VisionCameraCaptureRequest>();
            _workSignal = new AutoResetEvent(false);
            _viewType = viewType;
            _captureExecutor = captureExecutor;
            _state = VisionWorkerState.Stopped;
            _lastErrorMessage = string.Empty;
        }

        public ImageViewType ViewType
        {
            get { return _viewType; }
        }

        public VisionWorkerState State
        {
            get { return _state; }
        }

        public string LastErrorMessage
        {
            get { return _lastErrorMessage; }
        }

        public CapturedImage LatestImage
        {
            get
            {
                lock (_syncRoot)
                {
                    return _latestImage;
                }
            }
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
                _workerThread.Name = "VisionCameraCaptureWorker_" + _viewType.ToString();
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

            CompleteRemainingRequestsAsStopped();

            lock (_syncRoot)
            {
                _state = VisionWorkerState.Stopped;
                _workerThread = null;
            }
        }

        public VisionCameraCaptureRequest EnqueueCapture(Part part)
        {
            Start();

            VisionCameraCaptureRequest request = new VisionCameraCaptureRequest(_viewType, part);
            lock (_syncRoot)
            {
                if (_stopRequested)
                {
                    request.Dispose();
                    throw new InvalidOperationException("카메라 촬영 Worker가 종료 중입니다.");
                }

                _requestQueue.Enqueue(request);
                _workSignal.Set();
            }

            return request;
        }

        public CapturedImage Capture(Part part, int timeoutMilliseconds)
        {
            VisionCameraCaptureRequest request = EnqueueCapture(part);
            try
            {
                return WaitForRequest(request, timeoutMilliseconds);
            }
            catch (TimeoutException)
            {
                request.Abandon();
                if (request.CompletedEvent.WaitOne(0))
                {
                    request.Dispose();
                }

                throw;
            }
            finally
            {
                if (!request.IsAbandoned)
                {
                    request.Dispose();
                }
            }
        }

        public CapturedImage WaitForRequest(VisionCameraCaptureRequest request, int timeoutMilliseconds)
        {
            bool completed = request.CompletedEvent.WaitOne(timeoutMilliseconds);
            if (!completed)
            {
                throw new TimeoutException(_viewType.ToString() + " 카메라 촬영 시간이 초과되었습니다.");
            }

            if (request.Error != null)
            {
                throw request.Error;
            }

            return request.Output;
        }

        public void Dispose()
        {
            Stop();
            _workSignal.Dispose();
        }

        private void WorkerThreadProc()
        {
            SetState(VisionWorkerState.Running);

            while (true)
            {
                VisionCameraCaptureRequest request = DequeueRequest();
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

        private VisionCameraCaptureRequest DequeueRequest()
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

        private void ProcessRequest(VisionCameraCaptureRequest request)
        {
            try
            {
                CapturedImage image = _captureExecutor.ExecuteCapture(request.ViewType, request.Part);
                request.Output = image;
                SetLatestImage(image);
                _lastErrorMessage = string.Empty;
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
                if (request.IsAbandoned)
                {
                    request.Dispose();
                }
            }
        }

        private void CompleteRemainingRequestsAsStopped()
        {
            while (true)
            {
                VisionCameraCaptureRequest request = DequeueRequest();
                if (request == null)
                {
                    return;
                }

                request.Error = new InvalidOperationException("카메라 촬영 Worker가 종료되었습니다.");
                request.CompletedEvent.Set();
                if (request.IsAbandoned)
                {
                    request.Dispose();
                }
            }
        }

        private void SetLatestImage(CapturedImage image)
        {
            lock (_syncRoot)
            {
                _latestImage = image;
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
