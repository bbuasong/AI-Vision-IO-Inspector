using System;
using System.Runtime.InteropServices;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;
using MVSDK_Net;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// IMV 카메라 1대를 표현하는 어댑터입니다.
    /// 기존 VLAD_Ops 담당자가 추적하기 쉽도록 OpenDevice, StartGrabbing, GetFrame, ReleaseFrame 이름을 유지합니다.
    /// </summary>
    public class ImvCameraDevice
    {
        private readonly object _syncRoot;
        private readonly MyCamera _camera;
        private bool _handleCreated;
        private uint _bufferCount;

        public ImvCameraDevice(CameraChannelConfig channelConfig)
        {
            if (channelConfig == null)
            {
                throw new ArgumentNullException("channelConfig");
            }

            _syncRoot = new object();
            _camera = MVSDK_Net_Compat.CreateCamera();
            ChannelConfig = channelConfig;
            _bufferCount = 8;
        }

        public CameraChannelConfig ChannelConfig { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsGrabbing { get; private set; }

        public void OpenDevice()
        {
            lock (_syncRoot)
            {
                if (IsOpen)
                {
                    return;
                }

                IMVDefine.IMV_ECreateHandleMode mode;
                int cameraIndex;
                string cameraKey;
                ResolveCreateHandleParameter(out mode, out cameraIndex, out cameraKey);

                // 기존 VLAD_Ops 순서: IMV_CreateHandle -> IMV_Open
                int result = MVSDK_Net_Compat.IMV_CreateHandle(_camera, mode, cameraIndex, cameraKey);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_CreateHandle");
                _handleCreated = true;

                result = MVSDK_Net_Compat.IMV_Open(_camera);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_Open");
                IsOpen = true;
            }
        }

        public void SetBufferCount(uint bufferCount)
        {
            lock (_syncRoot)
            {
                EnsureOpen();
                _bufferCount = bufferCount == 0 ? 8 : bufferCount;
                int result = MVSDK_Net_Compat.IMV_SetBufferCount(_camera, _bufferCount);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_SetBufferCount");
            }
        }

        public void StartGrabbing()
        {
            lock (_syncRoot)
            {
                if (!IsOpen)
                {
                    OpenDevice();
                }

                if (IsGrabbing)
                {
                    return;
                }

                int result = MVSDK_Net_Compat.IMV_SetBufferCount(_camera, _bufferCount);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_SetBufferCount");

                result = MVSDK_Net_Compat.IMV_StartGrabbing(_camera);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_StartGrabbing");
                IsGrabbing = true;
            }
        }

        public void SetEnumFeatureSymbol(string featureName, string featureValue)
        {
            lock (_syncRoot)
            {
                EnsureOpen();
                int result = MVSDK_Net_Compat.IMV_SetEnumFeatureSymbol(_camera, featureName, featureValue);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_SetEnumFeatureSymbol(" + featureName + ")");
            }
        }

        public void ExecuteCommandFeature(string commandName)
        {
            lock (_syncRoot)
            {
                EnsureOpen();
                int result = MVSDK_Net_Compat.IMV_ExecuteCommandFeature(_camera, commandName);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_ExecuteCommandFeature(" + commandName + ")");
            }
        }

        public VisionFrame GetFrame(int timeoutMilliseconds)
        {
            lock (_syncRoot)
            {
                if (!IsGrabbing)
                {
                    StartGrabbing();
                }

                IMVDefine.IMV_Frame nativeFrame = new IMVDefine.IMV_Frame();
                int timeout = timeoutMilliseconds <= 0 ? 1000 : timeoutMilliseconds;
                int result = MVSDK_Net_Compat.IMV_GetFrame(_camera, ref nativeFrame, (uint)timeout);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_GetFrame");

                try
                {
                    return ConvertNativeFrame(nativeFrame);
                }
                finally
                {
                    MVSDK_Net_Compat.IMV_ReleaseFrame(_camera, ref nativeFrame);
                }
            }
        }

        public void ReleaseFrame(VisionFrame frame)
        {
            // 현재 구현은 GetFrame 안에서 SDK 버퍼를 즉시 복사하고 해제합니다.
            // 기존 VLAD_Ops 호출 흐름과 이름 호환을 위해 메서드는 유지합니다.
        }

        public void StopGrabbing()
        {
            lock (_syncRoot)
            {
                if (!IsGrabbing)
                {
                    return;
                }

                int result = MVSDK_Net_Compat.IMV_StopGrabbing(_camera);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_StopGrabbing");
                IsGrabbing = false;
            }
        }

        public void CloseDevice()
        {
            lock (_syncRoot)
            {
                if (IsGrabbing)
                {
                    StopGrabbing();
                }

                if (IsOpen)
                {
                    int closeResult = MVSDK_Net_Compat.IMV_Close(_camera);
                    MVSDK_Net_Compat.ThrowIfFailed(closeResult, "IMV_Close");
                    IsOpen = false;
                }

                if (_handleCreated)
                {
                    int destroyResult = MVSDK_Net_Compat.IMV_DestroyHandle(_camera);
                    MVSDK_Net_Compat.ThrowIfFailed(destroyResult, "IMV_DestroyHandle");
                    _handleCreated = false;
                }
            }
        }

        private VisionFrame ConvertNativeFrame(IMVDefine.IMV_Frame nativeFrame)
        {
            int width = Convert.ToInt32(nativeFrame.frameInfo.width);
            int height = Convert.ToInt32(nativeFrame.frameInfo.height);
            byte[] bgrBuffer = CopyFrameToBgr24(nativeFrame, width, height);

            VisionFrame frame = new VisionFrame();
            frame.CameraId = ResolveCameraId();
            frame.ViewType = ChannelConfig.ViewType;
            frame.Width = width;
            frame.Height = height;
            frame.Stride = width * 3;
            frame.PixelFormat = "BGR24";
            frame.FrameId = Convert.ToInt64(nativeFrame.frameInfo.blockId);
            frame.CapturedAt = DateTime.Now;
            frame.Buffer = bgrBuffer;
            return frame;
        }

        private byte[] CopyFrameToBgr24(IMVDefine.IMV_Frame nativeFrame, int width, int height)
        {
            int outputLength = checked(width * height * 3);
            bool canCopyDirectly = nativeFrame.frameInfo.pixelFormat == IMVDefine.IMV_EPixelType.gvspPixelBGR8
                                   && Convert.ToInt32(nativeFrame.frameInfo.paddingX) == 0
                                   && Convert.ToInt32(nativeFrame.frameInfo.paddingY) == 0;

            if (canCopyDirectly)
            {
                byte[] directBuffer = new byte[outputLength];
                Marshal.Copy(nativeFrame.pData, directBuffer, 0, outputLength);
                return directBuffer;
            }

            IntPtr destinationBuffer = IntPtr.Zero;
            try
            {
                destinationBuffer = Marshal.AllocHGlobal(outputLength);
                IMVDefine.IMV_PixelConvertParam convertParameter = new IMVDefine.IMV_PixelConvertParam();
                convertParameter.nWidth = nativeFrame.frameInfo.width;
                convertParameter.nHeight = nativeFrame.frameInfo.height;
                convertParameter.ePixelFormat = nativeFrame.frameInfo.pixelFormat;
                convertParameter.pSrcData = nativeFrame.pData;
                convertParameter.nSrcDataLen = nativeFrame.frameInfo.size;
                convertParameter.nPaddingX = nativeFrame.frameInfo.paddingX;
                convertParameter.nPaddingY = nativeFrame.frameInfo.paddingY;
                convertParameter.eBayerDemosaic = IMVDefine.IMV_EBayerDemosaic.demosaicNearestNeighbor;
                convertParameter.eDstPixelFormat = IMVDefine.IMV_EPixelType.gvspPixelBGR8;
                convertParameter.pDstBuf = destinationBuffer;
                convertParameter.nDstBufSize = (uint)outputLength;

                int result = MVSDK_Net_Compat.IMV_PixelConvert(_camera, ref convertParameter);
                MVSDK_Net_Compat.ThrowIfFailed(result, "IMV_PixelConvert");

                byte[] convertedBuffer = new byte[outputLength];
                Marshal.Copy(destinationBuffer, convertedBuffer, 0, outputLength);
                return convertedBuffer;
            }
            finally
            {
                if (destinationBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(destinationBuffer);
                }
            }
        }

        private void ResolveCreateHandleParameter(
            out IMVDefine.IMV_ECreateHandleMode mode,
            out int cameraIndex,
            out string cameraKey)
        {
            cameraIndex = 0;
            cameraKey = string.Empty;

            if (!string.IsNullOrWhiteSpace(ChannelConfig.CameraKey))
            {
                mode = IMVDefine.IMV_ECreateHandleMode.modeByCameraKey;
                cameraKey = ChannelConfig.CameraKey;
                return;
            }

            if (!string.IsNullOrWhiteSpace(ChannelConfig.DeviceUserId))
            {
                mode = IMVDefine.IMV_ECreateHandleMode.modeByDeviceUserID;
                cameraKey = ChannelConfig.DeviceUserId;
                return;
            }

            if (!string.IsNullOrWhiteSpace(ChannelConfig.IpAddress))
            {
                mode = IMVDefine.IMV_ECreateHandleMode.modeByIPAddress;
                cameraKey = ChannelConfig.IpAddress;
                return;
            }

            mode = IMVDefine.IMV_ECreateHandleMode.modeByIndex;
        }

        private string ResolveCameraId()
        {
            if (!string.IsNullOrWhiteSpace(ChannelConfig.ChannelId))
            {
                return ChannelConfig.ChannelId;
            }

            if (!string.IsNullOrWhiteSpace(ChannelConfig.CameraKey))
            {
                return ChannelConfig.CameraKey;
            }

            if (!string.IsNullOrWhiteSpace(ChannelConfig.IpAddress))
            {
                return ChannelConfig.IpAddress;
            }

            return ChannelConfig.ViewType.ToString();
        }

        private void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException(ChannelConfig.DisplayName + " 카메라가 열려 있지 않습니다.");
            }
        }
    }
}
