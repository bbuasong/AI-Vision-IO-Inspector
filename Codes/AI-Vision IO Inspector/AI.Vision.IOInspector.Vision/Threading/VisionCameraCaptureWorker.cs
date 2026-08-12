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
            RequestStop();
            WaitForStop(3000);
        }

        /// <summary>
        /// 종료 요청만 전달합니다. 여러 카메라 Worker에는 먼저 동시에 종료 신호를 보낸 뒤 대기해야
        /// 채널 수만큼 종료 시간이 누적되지 않습니다.
        /// </summary>
        public void RequestStop()
        {
            lock (_syncRoot)
            {
                if (_state == VisionWorkerState.Stopped)
                {
                    return;
                }

                _state = VisionWorkerState.Stopping;
                _stopRequested = true;
                SetWorkSignalIfAvailable();
            }
        }

        /// <summary>
        /// 이미 전달된 종료 요청에 대해 Worker 종료를 기다립니다.
        /// RequestStop과 분리하여 모든 카메라에 종료 신호를 먼저 전달할 수 있습니다.
        /// </summary>
        public void WaitForStop(int timeoutMilliseconds)
        {
            Thread threadToJoin;
            bool stopped = true;
            lock (_syncRoot)
            {
                threadToJoin = _workerThread;
            }

            if (threadToJoin != null && threadToJoin.IsAlive)
            {
                stopped = threadToJoin.Join(timeoutMilliseconds);
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
            return EnqueueCapture(part, DateTime.Now);
        }

        /// <summary>
        /// 검사 시작 시각을 함께 전달합니다. 6방향 이미지가 같은 폴더에 저장되도록
        /// 호출자가 검사마다 하나의 값을 정해 모든 채널에 같은 값을 넘깁니다.
        /// </summary>
        public VisionCameraCaptureRequest EnqueueCapture(Part part, DateTime inspectionStartedAt)
        {
            Start();

            VisionCameraCaptureRequest request = new VisionCameraCaptureRequest(_viewType, part, inspectionStartedAt);
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

        /// <summary>
        /// Coordinator가 RequestStop/WaitForStop 순서로 이미 종료 처리를 수행한 경우 사용합니다.
        /// Stop을 다시 호출하지 않아 종료 대기 시간이 중복되지 않도록 합니다.
        /// </summary>
        public void DisposeAfterStop()
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
                CapturedImage image = _captureExecutor.ExecuteCapture(request.ViewType, request.Part, request.InspectionStartedAt);
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
