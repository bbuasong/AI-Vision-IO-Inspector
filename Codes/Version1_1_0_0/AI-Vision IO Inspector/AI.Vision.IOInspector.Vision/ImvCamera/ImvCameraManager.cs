using System.Collections.Generic;
using System.Runtime.InteropServices;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using MVSDK_Net;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// IMV 카메라 검색과 장치 객체 생성을 담당합니다.
    /// 기존 VLAD_Ops의 Discovery_Cam_Devices 흐름과 동일하게 IMV_EnumDevices를 먼저 호출할 수 있습니다.
    /// </summary>
    public class ImvCameraManager
    {
        public IList<ImvCameraDevice> EnumDevices()
        {
            IList<ImvCameraDevice> devices = new List<ImvCameraDevice>();
            IMVDefine.IMV_DeviceList deviceList = new IMVDefine.IMV_DeviceList();
            uint interfaceType = (uint)IMVDefine.IMV_EInterfaceType.interfaceTypeAll;

            int result = MVSDK_Net_Compat.IMV_EnumDevices(ref deviceList, interfaceType);
            MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_EnumDevices");

            int deviceCount = (int)deviceList.nDevNum;
            int index = 0;
            while (index < deviceCount)
            {
                IMVDefine.IMV_DeviceInfo deviceInfo =
                    (IMVDefine.IMV_DeviceInfo)Marshal.PtrToStructure(
                        deviceList.pDevInfo + Marshal.SizeOf(typeof(IMVDefine.IMV_DeviceInfo)) * index,
                        typeof(IMVDefine.IMV_DeviceInfo));

                CameraChannelConfig config = new CameraChannelConfig();
                config.ChannelId = "IMV_" + index.ToString();
                config.ViewType = ImageViewType.Top;
                config.DisplayName = BuildDisplayName(deviceInfo, index);
                config.CameraModel = deviceInfo.modelName;
                config.ConnectionType = CameraConnectionType.DirectSdk;
                config.IsEnabled = true;
                config.SerialNumber = deviceInfo.serialNumber;
                config.CameraKey = deviceInfo.serialNumber;

                devices.Add(new ImvCameraDevice(config));
                index++;
            }

            return devices;
        }

        public ImvCameraDevice CreateDevice(CameraChannelConfig channelConfig)
        {
            return new ImvCameraDevice(channelConfig);
        }

        private string BuildDisplayName(IMVDefine.IMV_DeviceInfo deviceInfo, int index)
        {
            if (!string.IsNullOrWhiteSpace(deviceInfo.cameraName))
            {
                return deviceInfo.cameraName;
            }

            if (!string.IsNullOrWhiteSpace(deviceInfo.modelName))
            {
                return deviceInfo.modelName + " (" + deviceInfo.serialNumber + ")";
            }

            return "IMV Camera " + index.ToString();
        }
    }
}
