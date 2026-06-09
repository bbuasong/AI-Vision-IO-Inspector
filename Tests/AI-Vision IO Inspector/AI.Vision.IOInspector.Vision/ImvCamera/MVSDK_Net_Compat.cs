using System;
using MVSDK_Net;

namespace AI.Vision.IOInspector.Vision.ImvCamera
{
    /// <summary>
    /// 기존 VLAD_Ops의 IMV 카메라 코드가 사용하던 MVSDK_Net 진입점을 한 곳에 모아둔 호환 래퍼입니다.
    /// MVSDK_Net 내부의 IMVApi는 internal 타입이므로 직접 호출하지 않고, 기존 샘플과 동일하게 공개 클래스 MyCamera를 사용합니다.
    /// </summary>
    public static class MVSDK_Net_Compat
    {
        public const int Success = 0;

        public static string IMV_GetVersion()
        {
            try
            {
                return MyCamera.IMV_GetVersion();
            }
            catch (DllNotFoundException ex)
            {
                throw BuildMissingNativeException(ex);
            }
            catch (BadImageFormatException ex)
            {
                throw new InvalidOperationException("MVSDK_Net 또는 MVSDKmd.dll의 x86/x64 비트수가 현재 프로세스와 맞지 않습니다.", ex);
            }
        }

        public static int IMV_EnumDevices(ref IMVDefine.IMV_DeviceList deviceList, uint interfaceType)
        {
            try
            {
                return MyCamera.IMV_EnumDevices(ref deviceList, interfaceType);
            }
            catch (DllNotFoundException ex)
            {
                throw BuildMissingNativeException(ex);
            }
        }

        public static MyCamera CreateCamera()
        {
            return new MyCamera();
        }

        public static int IMV_CreateHandle(MyCamera camera, IMVDefine.IMV_ECreateHandleMode mode, int cameraIndex)
        {
            EnsureCamera(camera);
            return camera.IMV_CreateHandle(mode, cameraIndex);
        }

        public static int IMV_Open(MyCamera camera)
        {
            EnsureCamera(camera);
            return camera.IMV_Open();
        }

        public static int IMV_Close(MyCamera camera)
        {
            EnsureCamera(camera);
            return camera.IMV_Close();
        }

        public static int IMV_DestroyHandle(MyCamera camera)
        {
            EnsureCamera(camera);
            return camera.IMV_DestroyHandle();
        }

        public static int IMV_SetBufferCount(MyCamera camera, uint bufferCount)
        {
            EnsureCamera(camera);
            return camera.IMV_SetBufferCount(bufferCount);
        }

        public static int IMV_StartGrabbing(MyCamera camera)
        {
            EnsureCamera(camera);
            return camera.IMV_StartGrabbing();
        }

        public static int IMV_StopGrabbing(MyCamera camera)
        {
            EnsureCamera(camera);
            return camera.IMV_StopGrabbing();
        }

        public static int IMV_GetFrame(MyCamera camera, ref IMVDefine.IMV_Frame frame, uint timeoutMilliseconds)
        {
            EnsureCamera(camera);
            return camera.IMV_GetFrame(ref frame, timeoutMilliseconds);
        }

        public static int IMV_ReleaseFrame(MyCamera camera, ref IMVDefine.IMV_Frame frame)
        {
            EnsureCamera(camera);
            return camera.IMV_ReleaseFrame(ref frame);
        }

        public static int IMV_SetEnumFeatureSymbol(MyCamera camera, string featureName, string featureValue)
        {
            EnsureCamera(camera);
            return camera.IMV_SetEnumFeatureSymbol(featureName, featureValue);
        }

        public static int IMV_ExecuteCommandFeature(MyCamera camera, string commandName)
        {
            EnsureCamera(camera);
            return camera.IMV_ExecuteCommandFeature(commandName);
        }

        public static void ThrowIfFailed(int resultCode, string functionName)
        {
            if (resultCode == Success)
            {
                return;
            }

            throw new InvalidOperationException(functionName + " 실패. IMV 결과 코드: " + resultCode.ToString());
        }

        private static void EnsureCamera(MyCamera camera)
        {
            if (camera == null)
            {
                throw new ArgumentNullException("camera");
            }
        }

        private static InvalidOperationException BuildMissingNativeException(DllNotFoundException ex)
        {
            return new InvalidOperationException(
                "MVSDK_Net.dll은 참조되었지만 내부 네이티브 DLL인 MVSDKmd.dll 또는 제조사 종속 DLL을 찾지 못했습니다. "
                + "MVSDKmd.dll과 제조사 SDK 종속 DLL을 Native\\IMV\\x64 또는 Native\\VLAD 경로에 배치해야 합니다.",
                ex);
        }
    }
}
