using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 카메라 설정의 연결 방식에 맞는 프레임 소스를 선택합니다.
    /// 실제 IMV SDK/RTSP 어댑터가 완성되면 이 팩토리의 반환 구현체만 교체하면 됩니다.
    /// </summary>
    public class CameraFrameSourceFactory
    {
        private readonly SimulatedCameraFrameSource _simulatedCameraFrameSource;
        private readonly FileCameraFrameSource _fileCameraFrameSource;

        public CameraFrameSourceFactory()
        {
            _simulatedCameraFrameSource = new SimulatedCameraFrameSource();
            _fileCameraFrameSource = new FileCameraFrameSource();
        }

        public ICameraFrameSource Create(CameraConnectionType connectionType)
        {
            if (connectionType == CameraConnectionType.Simulated)
            {
                return _simulatedCameraFrameSource;
            }

            if (connectionType == CameraConnectionType.File)
            {
                return _fileCameraFrameSource;
            }

            return new UnsupportedCameraFrameSource(connectionType);
        }
    }
}
