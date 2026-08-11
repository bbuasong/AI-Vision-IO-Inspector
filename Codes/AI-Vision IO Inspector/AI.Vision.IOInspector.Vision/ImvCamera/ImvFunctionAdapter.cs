using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// 기존 IMV SDK 함수명을 그대로 노출하는 호환용 파사드입니다.
    /// 실제 SDK 래퍼는 ImvCameraManager와 ImvCameraDevice 뒤에 구현하되, 검색하기 쉬운 함수명 대응표는 이 클래스에 유지합니다.
    /// </summary>
    public class ImvFunctionAdapter
    {
        private readonly ImvCameraManager _cameraManager;

        public ImvFunctionAdapter()
        {
            _cameraManager = new ImvCameraManager();
        }

        public IList<ImvCameraDevice> IMV_EnumDevices()
        {
            return _cameraManager.EnumDevices();
        }

        public void IMV_OpenDevice(ImvCameraDevice cameraDevice)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.OpenDevice();
        }

        public void IMV_StartGrabbing(ImvCameraDevice cameraDevice)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.StartGrabbing();
        }

        public void IMV_SetBufferCount(ImvCameraDevice cameraDevice, uint bufferCount)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.SetBufferCount(bufferCount);
        }

        public VisionFrame IMV_GetFrame(ImvCameraDevice cameraDevice, int timeoutMilliseconds)
        {
            ValidateCameraDevice(cameraDevice);
            return cameraDevice.GetFrame(timeoutMilliseconds);
        }

        public void IMV_ReleaseFrame(ImvCameraDevice cameraDevice, VisionFrame frame)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.ReleaseFrame(frame);
        }

        public void IMV_StopGrabbing(ImvCameraDevice cameraDevice)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.StopGrabbing();
        }

        public void IMV_CloseDevice(ImvCameraDevice cameraDevice)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.CloseDevice();
        }

        public void IMV_SetEnumFeatureSymbol(ImvCameraDevice cameraDevice, string featureName, string featureValue)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.SetEnumFeatureSymbol(featureName, featureValue);
        }

        public void IMV_ExecuteCommandFeature(ImvCameraDevice cameraDevice, string commandName)
        {
            ValidateCameraDevice(cameraDevice);
            cameraDevice.ExecuteCommandFeature(commandName);
        }

        private void ValidateCameraDevice(ImvCameraDevice cameraDevice)
        {
            if (cameraDevice == null)
            {
                throw new ArgumentNullException("cameraDevice");
            }
        }
    }
}
