using System;
using System.Threading;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// 한 방향 카메라의 연속 미리보기 프레임을 백그라운드에서 갱신하는 Worker입니다.
    /// 촬영 요청용 VisionCameraCaptureWorker와 분리해, 실시간 화면은 최신 프레임 1장만 유지하도록 설계합니다.
    /// </summary>
    public class VisionCameraReceiveWorker : IDisposable
    {
        private readonly object _syncRoot;
        private readonly AutoResetEvent _wakeSignal;
        private readonly IVisionCameraReceiveExecutor _receiveExecutor;
        private readonly ImageViewType _viewType;
        private readonly int _intervalMilliseconds;
        private Thread _workerThread;
        private bool _stopRequested;
        private VisionWorkerState _state;
        private CapturedImage _latestImage;
        private string _lastErrorMessage;

        public VisionCameraReceiveWorker(
            ImageViewType viewType,
            IVisionCameraReceiveExecutor receiveExecutor,
            int intervalMilliseconds)
        {
            if (receiveExecutor == null)
            {
                throw new ArgumentNullException("receiveExecutor");
            }

            _syncRoot = new object();
            _wakeSignal = new AutoResetEvent(false);
            _viewType = viewType;
            _receiveExecutor = receiveExecutor;
            _intervalMilliseconds = intervalMilliseconds <= 0 ? 100 : intervalMilliseconds;
            _state = VisionWorkerState.Stopped;
            _lastErrorMessage = string.Empty;
        }

        public ImageViewType ViewType
        {
            get { return _viewType; }
        }

        public VisionWorkerState State
        {
            get
            {
                lock (_syncRoot)
                {
                    return _state;
                }
            }
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

        public string LastErrorMessage
        {
            get
            {
                lock (_syncRoot)
                {
                    return _lastErrorMessage;
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
                _workerThread.Name = "VisionCameraReceiveWorker_" + _viewType.ToString();
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
                _wakeSignal.Set();
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

        public void Dispose()
        {
            Stop();
            _wakeSignal.Dispose();
        }

        private void WorkerThreadProc()
        {
            SetState(VisionWorkerState.Running);

            while (!IsStopRequested())
            {
                ReceiveOnce();
                _wakeSignal.WaitOne(_intervalMilliseconds);
            }

            SetState(VisionWorkerState.Stopped);
        }

        private void ReceiveOnce()
        {
            try
            {
                CapturedImage image = _receiveExecutor.ReceiveLatestFrame(_viewType);
                if (image != null)
                {
                    SetLatestImage(image);
                }

                SetLastErrorMessage(string.Empty);
            }
            catch (Exception ex)
            {
                SetLastErrorMessage(ex.Message);
                SetState(VisionWorkerState.Faulted);
            }
        }

        private bool IsStopRequested()
        {
            lock (_syncRoot)
            {
                return _stopRequested;
            }
        }

        private void SetLatestImage(CapturedImage image)
        {
            lock (_syncRoot)
            {
                _latestImage = image;
            }
        }

        private void SetLastErrorMessage(string message)
        {
            lock (_syncRoot)
            {
                _lastErrorMessage = message;
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
