using System;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// 기존 VLAD_Ops_imvCam.cs의 클래스명과 함수명을 현재 프로젝트에 남겨두는 호환 계층입니다.
    /// 실제 6대 카메라 운용은 VisionCameraCoordinator와 Worker들이 담당하고, 이 파일은 기존 코드 담당자의 진입점 역할을 합니다.
    /// </summary>
    public static class VLAD_Ops_imvCam
    {
        /// <summary>
        /// 기존 스레드 시작 파라미터입니다.
        /// cam_name은 현재 CameraChannelConfig의 CameraKey 또는 DeviceUserId와 매칭할 수 있습니다.
        /// </summary>
        public class VLAD_Ops_imvCam_ThreadParam
        {
            public string cam_name;
            public float threshold;

            public VLAD_Ops_imvCam_ThreadParam(string cam_name, float threshold)
            {
                this.cam_name = cam_name;
                this.threshold = threshold;
            }
        }

        /// <summary>
        /// 기존 VLAD_Ops_imvCam_Thread 진입점입니다.
        /// 기존 구조는 이 스레드 안에서 IMV_Open, IMV_GetFrame, Cam_Proc, IMV_ReleaseFrame을 반복했습니다.
        /// 현재 구조에서는 반복 루프를 VisionCameraCaptureWorker와 VisionCameraReceiveWorker로 분리했습니다.
        /// </summary>
        public static void VLAD_Ops_imvCam_Thread(object obj)
        {
            VLAD_Ops_imvCam_ThreadParam threadParam = obj as VLAD_Ops_imvCam_ThreadParam;
            if (threadParam == null)
            {
                throw new ArgumentException("VLAD_Ops_imvCam_ThreadParam 값이 필요합니다.", "obj");
            }

            throw new NotSupportedException("기존 VLAD_Ops_imvCam_Thread 직접 실행은 현재 구조에서 사용하지 않습니다. VisionCameraCoordinator와 Worker 구현부에 IMV 수신 루프를 연결해야 합니다.");
        }

        /// <summary>
        /// 기존 VLAD_Ops_imvCam_IMV_Open 흐름입니다.
        /// IMV_EnumDevices, IMV_CreateHandle, IMV_Open, IMV_SetBufferCount, IMV_StartGrabbing 순서를 유지합니다.
        /// </summary>
        public static ImvCameraDevice VLAD_Ops_imvCam_IMV_Open(CameraChannelConfig channelConfig)
        {
            if (channelConfig == null)
            {
                throw new ArgumentNullException("channelConfig");
            }

            ImvCameraManager manager = new ImvCameraManager();
            ImvCameraDevice device = manager.CreateDevice(channelConfig);
            device.OpenDevice();
            device.StartGrabbing();
            return device;
        }

        /// <summary>
        /// 기존 Cam_Proc 역할입니다.
        /// SDK 프레임을 AI 추론 입력으로 넘기고 결과를 집계하는 위치를 명확히 표시하기 위해 함수명을 유지합니다.
        /// </summary>
        public static void Cam_Proc(VisionFrame frame, float threshold)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            throw new NotSupportedException("Cam_Proc의 실제 AI 추론 연결은 VisionAiInferenceService 또는 IVisionInferenceEngine 구현으로 옮겨야 합니다.");
        }

        public static ImageViewType ConvertCamNameToViewType(string camName)
        {
            if (string.Equals(camName, "Top", StringComparison.OrdinalIgnoreCase))
            {
                return ImageViewType.Top;
            }

            if (string.Equals(camName, "Front", StringComparison.OrdinalIgnoreCase))
            {
                return ImageViewType.Front;
            }

            if (string.Equals(camName, "Back", StringComparison.OrdinalIgnoreCase))
            {
                return ImageViewType.Back;
            }

            if (string.Equals(camName, "Left", StringComparison.OrdinalIgnoreCase))
            {
                return ImageViewType.Left;
            }

            if (string.Equals(camName, "Right", StringComparison.OrdinalIgnoreCase))
            {
                return ImageViewType.Right;
            }

            if (string.Equals(camName, "Thickness", StringComparison.OrdinalIgnoreCase))
            {
                return ImageViewType.Thickness;
            }

            throw new ArgumentException("알 수 없는 카메라 위치입니다: " + camName, "camName");
        }
    }
}
