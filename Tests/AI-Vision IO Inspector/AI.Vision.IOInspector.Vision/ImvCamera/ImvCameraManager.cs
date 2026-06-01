using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// 카메라 검색과 장치 생성을 담당할 어댑터 자리입니다.
    /// 향후 IMV_EnumDevices 호출과 Serial/IP 기반 카메라 매칭을 이 클래스에 구현합니다.
    /// </summary>
    public class ImvCameraManager
    {
        public IList<ImvCameraDevice> EnumDevices()
        {
            // 기존 IMV 대응 함수:
            // IMV_EnumDevices(...)
            return new List<ImvCameraDevice>();
        }

        public ImvCameraDevice CreateDevice(CameraChannelConfig channelConfig)
        {
            return new ImvCameraDevice(channelConfig);
        }
    }
}
