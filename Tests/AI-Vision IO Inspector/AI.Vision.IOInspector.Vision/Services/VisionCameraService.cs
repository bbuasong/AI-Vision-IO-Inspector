using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// Vision 프로젝트가 소유하는 카메라 서비스 경계입니다.
    /// 앱은 이 클래스를 통해서만 카메라 상태 조회와 촬영 요청을 수행합니다.
    /// </summary>
    public class VisionCameraService : ICameraService, IDisposable
    {
        private readonly VisionCameraCoordinator _cameraCoordinator;

        public VisionCameraService(string applicationRootPath)
        {
            _cameraCoordinator = new VisionCameraCoordinator(applicationRootPath);
        }

        public void ReloadConfiguration()
        {
            _cameraCoordinator.ReloadConfiguration();
        }

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            return _cameraCoordinator.GetChannelStatuses();
        }

        public CapturedImage Capture(ImageViewType viewType, Part part)
        {
            return _cameraCoordinator.Capture(viewType, part);
        }

        public IList<CapturedImage> CaptureAll(Part part)
        {
            return _cameraCoordinator.CaptureAll(part);
        }

        public IList<CapturedImage> GetLatestCapturedImages()
        {
            return _cameraCoordinator.GetLatestCapturedImages();
        }

        public void Dispose()
        {
            _cameraCoordinator.Dispose();
        }
    }
}
