using System;
using System.Threading;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// 기존 VLAD_Ops_imvCam.cs의 클래스명과 함수명을 현재 프로젝트에 맞춰 보존한 호환 계층입니다.
    /// 실제 화면 연동은 VisionCameraCoordinator가 담당하지만, 기존 담당자가 VLAD_Ops 방식으로 추적할 수 있도록 같은 진입점을 제공합니다.
    /// </summary>
    public static class VLAD_Ops_imvCam
    {
        private static readonly object SyncRoot = new object();
        private static bool _stopRequested;
        private static VisionFrame _latestFrame;
        private static string _lastErrorMessage = string.Empty;

        /// <summary>
        /// 기존 스레드 시작 파라미터입니다.
        /// channel_config가 있으면 그 설정으로 열고, 없으면 cam_name을 CameraKey/표시명으로 사용합니다.
        /// </summary>
        public class VLAD_Ops_imvCam_ThreadParam
        {
            public string cam_name;
            public float threshold;
            public CameraChannelConfig channel_config;

            public VLAD_Ops_imvCam_ThreadParam(string cam_name, float threshold)
            {
                this.cam_name = cam_name;
                this.threshold = threshold;
            }

            public VLAD_Ops_imvCam_ThreadParam(string cam_name, float threshold, CameraChannelConfig channelConfig)
            {
                this.cam_name = cam_name;
                this.threshold = threshold;
                this.channel_config = channelConfig;
            }
        }

        public static VisionFrame LatestFrame
        {
            get
            {
                lock (SyncRoot)
                {
                    return _latestFrame;
                }
            }
        }

        public static string LastErrorMessage
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastErrorMessage;
                }
            }
        }

        public static void RequestStop()
        {
            lock (SyncRoot)
            {
                _stopRequested = true;
            }
        }

        public static void ResetStop()
        {
            lock (SyncRoot)
            {
                _stopRequested = false;
            }
        }

        /// <summary>
        /// 기존 VLAD_Ops_imvCam_Thread 진입점입니다.
        /// IMV_Open, IMV_GetFrame, Cam_Proc, IMV_ReleaseFrame 흐름을 현재 ImvCameraDevice로 수행합니다.
        /// </summary>
        public static void VLAD_Ops_imvCam_Thread(object obj)
        {
            VLAD_Ops_imvCam_ThreadParam threadParam = obj as VLAD_Ops_imvCam_ThreadParam;
            if (threadParam == null)
            {
                throw new ArgumentException("VLAD_Ops_imvCam_ThreadParam 값이 필요합니다.", "obj");
            }

            CameraChannelConfig channelConfig = threadParam.channel_config;
            if (channelConfig == null)
            {
                channelConfig = BuildDefaultChannelConfig(threadParam.cam_name);
            }

            ResetStop();
            ImvCameraDevice device = null;

            try
            {
                device = VLAD_Ops_imvCam_IMV_Open(channelConfig);
                while (!IsStopRequested())
                {
                    VisionFrame frame = device.GetFrame(1000);
                    try
                    {
                        Cam_Proc(frame, threadParam.threshold);
                    }
                    finally
                    {
                        device.ReleaseFrame(frame);
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                SetLastErrorMessage(ex.Message);
                throw;
            }
            finally
            {
                if (device != null)
                {
                    device.CloseDevice();
                }
            }
        }

        /// <summary>
        /// 기존 VLAD_Ops_imvCam_IMV_Open 흐름입니다.
        /// IMV_CreateHandle, IMV_Open, IMV_SetBufferCount, IMV_StartGrabbing 순서로 카메라를 준비합니다.
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
            device.SetBufferCount(8);
            device.StartGrabbing();
            return device;
        }

        /// <summary>
        /// 기존 Cam_Proc 위치입니다.
        /// 기존 VLAD_Ops는 여기서 Bitmap/Mat 변환 후 VLAD_Inference_Mat을 호출했습니다.
        /// 현재 함수는 최신 프레임을 보관하고, 실제 VLAD Mat 추론은 VLAD_Ops_Ai와 VladVisionInferenceEngine 경로에서 담당합니다.
        /// </summary>
        public static void Cam_Proc(VisionFrame frame, float threshold)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            lock (SyncRoot)
            {
                _latestFrame = frame;
                _lastErrorMessage = string.Empty;
            }
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

            throw new ArgumentException("알 수 없는 카메라 위치입니다. " + camName, "camName");
        }

        private static CameraChannelConfig BuildDefaultChannelConfig(string camName)
        {
            CameraChannelConfig config = new CameraChannelConfig();
            config.ChannelId = camName;
            config.ViewType = ConvertCamNameToViewType(camName);
            config.DisplayName = camName;
            config.CameraModel = "IMV";
            config.ConnectionType = CameraConnectionType.DirectSdk;
            config.IsEnabled = true;
            config.CameraKey = camName;
            config.TriggerMode = CameraTriggerMode.Continuous;
            return config;
        }

        private static bool IsStopRequested()
        {
            lock (SyncRoot)
            {
                return _stopRequested;
            }
        }

        private static void SetLastErrorMessage(string message)
        {
            lock (SyncRoot)
            {
                _lastErrorMessage = message;
            }
        }
    }
}
