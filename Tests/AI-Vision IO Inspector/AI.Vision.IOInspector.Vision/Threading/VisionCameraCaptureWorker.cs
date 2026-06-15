using System;
using System.Collections.Generic;
using System.Threading;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// 6방향 카메라 촬영 요청을 전용 Worker Thread에서 순차 처리합니다.
    /// 기존 VLAD/IMV 코드의 카메라별 Thread 구조를 현재 WPF/MVVM 구조에 맞게 분리한 클래스입니다.
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
        private bool _disposeRequested;
        private bool _disposed;
        private bool _workSignalDisposed;
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
                ThrowIfDisposed();

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

            CompleteRemainingRequestsAsStopped();

            if (stopped)
            {
                lock (_syncRoot)
                {
                    _state = VisionWorkerState.Stopped;
                    _workerThread = null;
                }
            }
        }

        public VisionCameraCaptureRequest EnqueueCapture(Part part)
        {
            Start();

            VisionCameraCaptureRequest request = new VisionCameraCaptureRequest(_viewType, part);
            lock (_syncRoot)
            {
                ThrowIfDisposed();

                if (_stopRequested)
                {
                    request.Dispose();
                    throw new InvalidOperationException("카메라 촬영 Worker가 종료 중입니다.");
                }

                _requestQueue.Enqueue(request);
                SetWorkSignalIfAvailable();
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
                // 타임아웃된 요청은 워커 스레드가 아직 처리 중일 수 있습니다.
                // 여기서 CompletedEvent를 확인하거나 Dispose하면 워커의 Set 호출과 경합하여 ObjectDisposedException이 발생할 수 있습니다.
                request.Abandon();
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

        private void WorkerThreadProc()
        {
            try
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

                    WaitForWorkSignal(100);
                }
            }
            catch (ObjectDisposedException)
            {
                if (!IsDisposeRequested())
                {
                    SetLastErrorMessage("카메라 촬영 Worker 신호 객체가 예기치 않게 Dispose되었습니다.");
                    SetState(VisionWorkerState.Faulted);
                }
            }
            finally
            {
                MarkWorkerStopped();
                DisposeWorkSignalIfWorkerStopped();
            }
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
                SetLastErrorMessage(string.Empty);
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
