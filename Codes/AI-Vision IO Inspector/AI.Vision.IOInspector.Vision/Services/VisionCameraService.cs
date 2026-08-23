using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.LegacyVlad;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// Vision 프로젝트가 소유하는 카메라 서비스 경계입니다.
    /// CAM 모드 초기화는 공용 VladCamModeRuntime을 통해 한 번만 수행합니다.
    /// </summary>
    public class VisionCameraService : ICameraService, IDisposable
    {
        private readonly VisionCameraCoordinator _cameraCoordinator;
        private readonly VladCamModeRuntime _camModeRuntime;

        public VisionCameraService(string applicationRootPath, VladCamModeRuntime camModeRuntime)
        {
            _camModeRuntime = camModeRuntime ?? throw new ArgumentNullException(nameof(camModeRuntime));
            _cameraCoordinator = new VisionCameraCoordinator(applicationRootPath, _camModeRuntime);
        }

        public void ReloadConfiguration()
        {
            _cameraCoordinator.ReloadConfiguration();
        }

        public void EnsureLiveFrameSources()
        {
            _cameraCoordinator.EnsureVladRtspRegistrations();
        }

        public IList<CameraChannelConfig> GetChannelConfigurations()
        {
            return _cameraCoordinator.GetChannelConfigurations();
        }

        public void SaveChannelConfigurations(IList<CameraChannelConfig> channels)
        {
            _cameraCoordinator.SaveChannelConfigurations(channels);
        }

        public IList<CameraChannelStatus> GetChannelStatuses()
        {
            return _cameraCoordinator.GetChannelStatuses();
        }

        public CameraChannelStatus TestChannelConnection(ImageViewType viewType)
        {
            return _cameraCoordinator.TestChannelConnection(viewType);
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

        public void PrepareForVladRuntimeReload()
        {
            _cameraCoordinator.PrepareForVladRuntimeReload();
        }

        public void ResumeAfterVladRuntimeReload()
        {
            _cameraCoordinator.ResumeAfterVladRuntimeReload();
        }

        public void Dispose()
        {
            _cameraCoordinator.Dispose();
        }
    }
}
