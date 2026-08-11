using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// 실제 한 채널에서 프레임을 가져와 파일로 저장하는 어댑터 경계입니다.
    /// IMV SDK, RTSP, NVR, 테스트 파일 방식은 이 인터페이스의 구현체로 교체합니다.
    /// </summary>
    public interface ICameraFrameSource
    {
        CapturedImage Capture(CameraChannelConfig channel, Part part, string outputFilePath);
    }
}
