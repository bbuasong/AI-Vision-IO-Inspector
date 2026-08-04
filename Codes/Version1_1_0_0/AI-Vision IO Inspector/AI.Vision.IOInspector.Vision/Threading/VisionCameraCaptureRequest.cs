using System;
using System.Threading;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Threading
{
    /// <summary>
    /// 카메라 Worker Thread에서 처리할 촬영 요청 1건입니다.
    /// 요청한 쪽은 CompletedEvent를 기다리고, 실제 촬영은 UI 스레드 밖에서 수행됩니다.
    /// </summary>
    public class VisionCameraCaptureRequest : IDisposable
    {
        private readonly object _syncRoot;
        private readonly ManualResetEvent _completedEvent;
        private bool _isAbandoned;

        public VisionCameraCaptureRequest(ImageViewType viewType, Part part)
        {
            _syncRoot = new object();
            _completedEvent = new ManualResetEvent(false);
            ViewType = viewType;
            Part = part;
            RequestedAt = DateTime.Now;
        }

        public ImageViewType ViewType { get; private set; }

        public Part Part { get; private set; }

        public DateTime RequestedAt { get; private set; }

        public ManualResetEvent CompletedEvent
        {
            get { return _completedEvent; }
        }

        public CapturedImage Output { get; set; }

        public Exception Error { get; set; }

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

        public void Dispose()
        {
            _completedEvent.Dispose();
        }
    }
}
