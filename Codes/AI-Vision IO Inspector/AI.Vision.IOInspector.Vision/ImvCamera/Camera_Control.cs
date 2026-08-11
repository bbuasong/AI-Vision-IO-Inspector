using System;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// 기존 Camera_Control.cs를 알던 담당자가 Open_Cam, Close_Cam, Is_Open 흐름을 찾을 수 있게 하는 호환 클래스입니다.
    /// 실제 구현은 ImvCameraDevice에 SDK 호출을 넣고, 이 클래스는 기존 이름을 유지하는 얇은 연결부로 사용합니다.
    /// </summary>
    public class Camera_Control
    {
        private readonly CameraChannelConfig _channelConfig;
        private readonly ImvCameraManager _cameraManager;
        private ImvCameraDevice _cameraDevice;

        public Camera_Control(CameraChannelConfig channelConfig)
        {
            if (channelConfig == null)
            {
                throw new ArgumentNullException("channelConfig");
            }

            _channelConfig = channelConfig;
            _cameraManager = new ImvCameraManager();
        }

        public event EventHandler<GetFrame_Args> OnGetFrameEvent;

        public event EventHandler OnOpenCameraEvent;

        public event EventHandler OnCloseCameraEvent;

        public class GetFrame_Args : EventArgs
        {
            public VisionFrame Frame { get; set; }
        }

        /// <summary>
        /// 기존 Open_Cam 흐름입니다.
        /// 기존 코드 기준: 핸들 생성, Open, AttachGrabbing 또는 수신 Thread 등록, StartGrabbing 순서입니다.
        /// </summary>
        public bool Open_Cam(int cam_idx)
        {
            if (_cameraDevice != null && _cameraDevice.IsOpen)
            {
                return true;
            }

            _cameraDevice = _cameraManager.CreateDevice(_channelConfig);
            _cameraDevice.OpenDevice();
            _cameraDevice.StartGrabbing();

            RaiseOpenCameraEvent();
            return _cameraDevice.IsOpen;
        }

        public bool Open_Cam()
        {
            return Open_Cam(-1);
        }

        /// <summary>
        /// 기존 Close_Cam 흐름입니다.
        /// 기존 코드와 같이 수신 이벤트를 끊고 StopGrabbing, Close 순서로 종료합니다.
        /// </summary>
        public void Close_Cam()
        {
            OnGetFrameEvent = null;

            if (_cameraDevice != null)
            {
                if (_cameraDevice.IsGrabbing)
                {
                    _cameraDevice.StopGrabbing();
                }

                if (_cameraDevice.IsOpen)
                {
                    _cameraDevice.CloseDevice();
                }
            }

            RaiseCloseCameraEvent();
        }

        public bool Is_Open()
        {
            if (_cameraDevice == null)
            {
                return false;
            }

            return _cameraDevice.IsOpen;
        }

        /// <summary>
        /// 기존 onGetFrame callback에서 하던 일을 현재 VisionFrame 이벤트로 표현합니다.
        /// 실제 SDK callback에서는 frame buffer를 복사한 뒤 즉시 ReleaseFrame 해야 합니다.
        /// </summary>
        public void OnGetFrame(VisionFrame frame)
        {
            if (frame == null)
            {
                return;
            }

            EventHandler<GetFrame_Args> handler = OnGetFrameEvent;
            if (handler != null)
            {
                GetFrame_Args args = new GetFrame_Args();
                args.Frame = frame;
                handler(this, args);
            }
        }

        private void RaiseOpenCameraEvent()
        {
            EventHandler handler = OnOpenCameraEvent;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RaiseCloseCameraEvent()
        {
            EventHandler handler = OnCloseCameraEvent;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
