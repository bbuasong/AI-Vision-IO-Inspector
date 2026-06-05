using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services.Camera;
using AI.Vision.IOInspector.Vision.Models;
using AI.Vision.IOInspector.Vision.Threading;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// Vision 프로젝트의 카메라 제어 중심 클래스입니다.
    /// 카메라별 촬영 Worker를 관리하고, 향후 IMV 직접 SDK/RTSP/NVR 수신 구조를 이 위치에서 조율합니다.
    /// </summary>
    public class VisionCameraCoordinator : ICameraService, IVisionCameraCaptureExecutor, IDisposable
    {
        private const int CaptureTimeoutMilliseconds = 10000;

        private readonly object _syncRoot;
        private readonly object _configuredCameraServiceSyncRoot;
        private readonly ConfiguredCameraService _configuredCameraService;
        private readonly Dictionary<ImageViewType, VisionCameraCaptureWorker> _captureWorkers;
        private VisionWorkerState _state;

        public VisionCameraCoordinator(string applicationRootPath)
        {
            _syncRoot = new object();
            _configuredCameraServiceSyncRoot = new object();
            _configuredCameraService = new ConfiguredCameraService(applicationRootPath);
            _captureWorkers = new Dictionary<ImageViewType, VisionCameraCaptureWorker>();
            _state = VisionWorkerState.Stopped;
            BuildWorkersFromConfiguration();
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

        public void Start()
        {
            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            foreach (VisionCameraCaptureWorker worker in workers)
            {
                worker.Start();
            }

            lock (_syncRoot)
            {
                _state = VisionWorkerState.Running;
            }
        }

        public void Stop()
        {
            StopWorkers();
            lock (_syncRoot)
            {
                _state = VisionWorkerState.Stopped;
            }
        }

        public void ReloadConfiguration()
        {
            bool restartWorkers = State == VisionWorkerState.Running;
            StopWorkers();

            lock (_configuredCameraServiceSyncRoot)
            {
                _configuredCameraService.ReloadConfiguration();
            }

            BuildWorkersFromConfiguration();

            if (restartWorkers)
            {
                Start();
            }
        }

        public IList<CameraChannelConfig> GetChannelConfigurations()
        {
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.GetChannelConfigurations();
            }
        }

        public void SaveChannelConfigurations(IList<CameraChannelConfig> channels)
        {
            bool restartWorkers = State == VisionWorkerState.Running;
            StopWorkers();

            lock (_configuredCameraServiceSyncRoot)
            {
                _configuredCameraService.SaveChannelConfigurations(channels);
            }

            BuildWorkersFromConfiguration();

            if (restartWorkers)
            {
                Start();
            }
        }

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.GetChannelStatuses();
            }
        }

        public CameraChannelStatus TestChannelConnection(ImageViewType viewType)
        {
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.TestChannelConnection(viewType);
            }
        }

        public IList<CapturedImage> GetLatestCapturedImages()
        {
            IList<CapturedImage> images = new List<CapturedImage>();
            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            foreach (VisionCameraCaptureWorker worker in workers)
            {
                CapturedImage image = worker.LatestImage;
                if (image != null)
                {
                    images.Add(image);
                }
            }

            return images;
        }

        public CapturedImage Capture(ImageViewType viewType, Part part)
        {
            Start();

            VisionCameraCaptureWorker worker = FindWorker(viewType);
            if (worker == null)
            {
                return ExecuteCapture(viewType, part);
            }

            return worker.Capture(part, CaptureTimeoutMilliseconds);
        }

        public IList<CapturedImage> CaptureAll(Part part)
        {
            Start();

            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            IList<VisionCameraCaptureRequest> requests = new List<VisionCameraCaptureRequest>();
            IList<CapturedImage> images = new List<CapturedImage>();

            try
            {
                foreach (VisionCameraCaptureWorker worker in workers)
                {
                    requests.Add(worker.EnqueueCapture(part));
                }

                foreach (VisionCameraCaptureRequest request in requests)
                {
                    CapturedImage image = WaitCaptureRequest(request);
                    if (image != null)
                    {
                        images.Add(image);
                    }
                }
            }
            finally
            {
                DisposeCompletedRequests(requests);
            }

            return images;
        }

        public CapturedImage ExecuteCapture(ImageViewType viewType, Part part)
        {
            // 현재 ConfiguredCameraService는 상태 Dictionary를 내부에 가지므로 실제 SDK 도입 전까지는 단일 진입으로 보호합니다.
            lock (_configuredCameraServiceSyncRoot)
            {
                return _configuredCameraService.Capture(viewType, part);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private CapturedImage WaitCaptureRequest(VisionCameraCaptureRequest request)
        {
            VisionCameraCaptureWorker worker = FindWorker(request.ViewType);
            if (worker == null)
            {
                request.Abandon();
                throw new InvalidOperationException(request.ViewType.ToString() + " 카메라 Worker를 찾을 수 없습니다.");
            }

            try
            {
                return worker.WaitForRequest(request, CaptureTimeoutMilliseconds);
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
        }

        private void BuildWorkersFromConfiguration()
        {
            IList<CameraChannelStatus> statuses;
            lock (_configuredCameraServiceSyncRoot)
            {
                statuses = _configuredCameraService.GetChannelStatuses();
            }

            lock (_syncRoot)
            {
                _captureWorkers.Clear();
                foreach (CameraChannelStatus status in statuses)
                {
                    if (status.IsEnabled)
                    {
                        _captureWorkers[status.ViewType] = new VisionCameraCaptureWorker(status.ViewType, this);
                    }
                }
            }
        }

        private void StopWorkers()
        {
            IList<VisionCameraCaptureWorker> workers = GetOrderedWorkers();
            foreach (VisionCameraCaptureWorker worker in workers)
            {
                worker.Dispose();
            }

            lock (_syncRoot)
            {
                _captureWorkers.Clear();
            }
        }

        private VisionCameraCaptureWorker FindWorker(ImageViewType viewType)
        {
            lock (_syncRoot)
            {
                if (_captureWorkers.ContainsKey(viewType))
                {
                    return _captureWorkers[viewType];
                }
            }

            return null;
        }

        private IList<VisionCameraCaptureWorker> GetOrderedWorkers()
        {
            IList<VisionCameraCaptureWorker> workers = new List<VisionCameraCaptureWorker>();
            AddWorkerIfExists(workers, ImageViewType.Top);
            AddWorkerIfExists(workers, ImageViewType.Front);
            AddWorkerIfExists(workers, ImageViewType.Back);
            AddWorkerIfExists(workers, ImageViewType.Left);
            AddWorkerIfExists(workers, ImageViewType.Right);
            AddWorkerIfExists(workers, ImageViewType.Thickness);
            return workers;
        }

        private void AddWorkerIfExists(IList<VisionCameraCaptureWorker> workers, ImageViewType viewType)
        {
            VisionCameraCaptureWorker worker = FindWorker(viewType);
            if (worker != null)
            {
                workers.Add(worker);
            }
        }

        private void DisposeCompletedRequests(IList<VisionCameraCaptureRequest> requests)
        {
            foreach (VisionCameraCaptureRequest request in requests)
            {
                if (request.IsAbandoned)
                {
                    continue;
                }

                if (request.CompletedEvent.WaitOne(0))
                {
                    request.Dispose();
                }
                else
                {
                    request.Abandon();
                    if (request.CompletedEvent.WaitOne(0))
                    {
                        request.Dispose();
                    }
                }
            }
        }
    }
}
