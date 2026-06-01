using System;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// IMV 카메라 1대를 표현하는 어댑터 자리입니다.
    /// 기존 IMV SDK 흐름을 아는 담당자가 찾기 쉽도록 OpenDevice, StartGrabbing, GetFrame, ReleaseFrame, StopGrabbing 이름을 유지합니다.
    /// </summary>
    public class ImvCameraDevice
    {
        public ImvCameraDevice(CameraChannelConfig channelConfig)
        {
            ChannelConfig = channelConfig;
        }

        public CameraChannelConfig ChannelConfig { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsGrabbing { get; private set; }

        public void OpenDevice()
        {
            // 기존 IMV 대응 함수:
            // IMV_CreateHandle / IMV_Open 또는 IMV_FG_OpenDevice
            throw new NotSupportedException("IMV OpenDevice is not implemented yet.");
        }

        public void SetBufferCount(uint bufferCount)
        {
            // 기존 IMV 대응 함수:
            // IMV_SetBufferCount(bufferCount)
            throw new NotSupportedException("IMV SetBufferCount is not implemented yet.");
        }

        public void StartGrabbing()
        {
            // 기존 IMV 대응 함수:
            // IMV_StartGrabbing 또는 IMV_FG_StartGrabbing
            throw new NotSupportedException("IMV StartGrabbing is not implemented yet.");
        }

        public void SetEnumFeatureSymbol(string featureName, string featureValue)
        {
            // 기존 IMV 대응 함수:
            // IMV_SetEnumFeatureSymbol("TriggerMode", "On")
            // IMV_SetEnumFeatureSymbol("TriggerSource", "Software" 또는 "Line1")
            throw new NotSupportedException("IMV SetEnumFeatureSymbol is not implemented yet: " + featureName + "=" + featureValue);
        }

        public void ExecuteCommandFeature(string commandName)
        {
            // 기존 IMV 대응 함수:
            // IMV_ExecuteCommandFeature("TriggerSoftware")
            throw new NotSupportedException("IMV ExecuteCommandFeature is not implemented yet: " + commandName);
        }

        public VisionFrame GetFrame(int timeoutMilliseconds)
        {
            // 기존 IMV 대응 함수:
            // IMV_GetFrame(ref frame, timeoutMilliseconds)
            throw new NotSupportedException("IMV GetFrame is not implemented yet.");
        }

        public void ReleaseFrame(VisionFrame frame)
        {
            // 기존 IMV 대응 함수:
            // IMV_ReleaseFrame(ref frame)
        }

        public void StopGrabbing()
        {
            // 기존 IMV 대응 함수:
            // IMV_StopGrabbing 또는 IMV_FG_StopGrabbing
            IsGrabbing = false;
        }

        public void CloseDevice()
        {
            // 기존 IMV 대응 함수:
            // IMV_Close / IMV_DestroyHandle 또는 IMV_FG_CloseDevice
            IsOpen = false;
        }
    }
}
