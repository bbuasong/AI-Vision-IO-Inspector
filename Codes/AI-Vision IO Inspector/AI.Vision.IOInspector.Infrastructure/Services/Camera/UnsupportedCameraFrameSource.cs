using System;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 아직 실제 런타임 DLL이 연결되지 않은 카메라 방식에 대해 명확한 오류를 돌려주는 소스입니다.
    /// 설정만 먼저 만들어 두고, 현장 SDK 확인 후 구현체를 교체하기 위한 보호 장치입니다.
    /// </summary>
    public class UnsupportedCameraFrameSource : ICameraFrameSource
    {
        private readonly CameraConnectionType _connectionType;

        public UnsupportedCameraFrameSource(CameraConnectionType connectionType)
        {
            _connectionType = connectionType;
        }

        public CapturedImage Capture(CameraChannelConfig channel, Part part, string outputFilePath)
        {
            if (_connectionType == CameraConnectionType.DirectSdk)
            {
                throw new NotSupportedException(channel.DisplayName + " DirectSdk 캡처는 MVSDK_Net.dll, CLIDelegate.dll, ThridLibray.dll의 배포 위치가 확정된 뒤 활성화해야 합니다.");
            }

            if (_connectionType == CameraConnectionType.Rtsp || _connectionType == CameraConnectionType.NvrRtsp)
            {
                throw new NotSupportedException(channel.DisplayName + " RTSP 캡처는 VLAD_SDK.dll/libVLC 또는 별도 RTSP 프레임 캡처 어댑터가 연결된 뒤 활성화해야 합니다.");
            }

            throw new NotSupportedException(channel.DisplayName + " 카메라 연결 방식이 지원되지 않습니다: " + _connectionType.ToString());
        }
    }
}
