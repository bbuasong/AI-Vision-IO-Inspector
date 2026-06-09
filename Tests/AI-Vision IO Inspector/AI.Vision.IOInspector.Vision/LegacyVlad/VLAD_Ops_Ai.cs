using System;
using System.Collections.Generic;
using System.Text;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    public enum SDK_USER
    {
        USER_VLAD,
        USER_STD,
        USER_CUS_STD,
        USER_SRD,
        USER_MPS,
        USER_ATS
    }

    public enum SDK_MSG
    {
        MSG_V0,
        MSG_V1,
        MSG_V2
    }

    public enum SDK_MAJ
    {
        MAJ_V0,
        MAJ_V1,
        MAJ_V2
    }

    public enum SDK_AI_MSG
    {
        AI_MSG_V0,
        AI_MSG_V1,
        AI_MSG_V2,
        AI_MSG_V3
    }

    /// <summary>
    /// 기존 VLAD_Ops_Ai.cs 파일명과 함수명을 기준으로 작업하던 담당자를 위한 진입점입니다.
    /// WPF/MVVM 구조에서는 이 클래스가 UI를 직접 만지지 않고, VLAD SDK 등록/추론/RTSP callback 연결만 담당합니다.
    /// </summary>
    public static class VLAD_Ops_Ai
    {
        public delegate void RTSP_Callback(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display);

        private static readonly List<RtspCallbackBridge> RtspCallbackBridges = new List<RtspCallbackBridge>();

        public static IntPtr VLAD_Ops_Ai_Env_Start(
            int user,
            string rootName,
            string siteName,
            int messageVersion,
            int majorVersion,
            string modelPath,
            int gpuId)
        {
            return VLAD_Ops_Ai_Compat.VLAD_Ops_Ai_Env_Start(
                user,
                rootName,
                siteName,
                messageVersion,
                majorVersion,
                modelPath,
                gpuId);
        }

        public static IntPtr VLAD_Registration(int user, int messageVersion, int majorVersion)
        {
            return VladNativeMethods.VLAD_Registration(user, messageVersion, majorVersion);
        }

        public static long VLAD_Custom_ID_Generate(int user, int messageVersion, int majorVersion, int minorVersion)
        {
            return VladNativeMethods.VLAD_Custom_ID_Generate(user, messageVersion, majorVersion, minorVersion);
        }

        public static IntPtr VLAD_Custom_Registration(
            long customId,
            string uiName,
            string rootName,
            string siteName,
            string modelPath,
            string customInfo,
            int gpuId)
        {
            return VladNativeMethods.VLAD_Custom_Registration(customId, uiName, rootName, siteName, modelPath, customInfo, gpuId);
        }

        public static IntPtr VLAD_Ops_Inference_Registration(
            IntPtr vladId,
            string kindName,
            string siteName,
            string modelPath,
            string customInfo,
            int gpuId)
        {
            return VladNativeMethods.VLAD_Ops_Inference_Registration(vladId, kindName, siteName, modelPath, customInfo, gpuId);
        }

        public static VisionInspectionOutput VLAD_Ops_Ai_Inference_Mat(
            VisionInspectionInput input,
            float threshold,
            int drawMode)
        {
            return VLAD_Ops_Ai_Compat.VLAD_Ops_Ai_Inference_Mat(input, threshold, drawMode);
        }

        public static IntPtr VLAD_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode)
        {
            if (vladId == IntPtr.Zero)
            {
                return VLAD_Ops_Ai_Compat.VLAD_Inference_Mat(rawData, threshold, drawMode);
            }

            return VladNativeMethods.VLAD_Inference_Mat(vladId, rawData, threshold, drawMode);
        }

        public static IntPtr VLAD_Custom_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, IntPtr customParameter)
        {
            return VladNativeMethods.VLAD_Custom_Inference_Mat(vladId, rawData, threshold, drawMode, customParameter);
        }

        public static int VLAD_InferenceData_Get_Valid_Count(IntPtr vladId, IntPtr detectData)
        {
            if (vladId == IntPtr.Zero)
            {
                return VLAD_Ops_Ai_Compat.VLAD_InferenceData_Get_Valid_Count(detectData);
            }

            return VladNativeMethods.VLAD_InferenceData_Get_Valid_Count(vladId, detectData);
        }

        public static int VLAD_InferenceData_Get_Valid_Count(IntPtr detectData)
        {
            return VLAD_Ops_Ai_Compat.VLAD_InferenceData_Get_Valid_Count(detectData);
        }

        public static int VLAD_InferenceData_V1_Draw(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize)
        {
            return VladNativeMethods.VLAD_InferenceData_V1_Draw(vladId, detectData, rawData, classCount, detectText, customParameter, tlvInfo, tlvSize);
        }

        public static int VLAD_InferenceData_V2_Draw(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText)
        {
            return VladNativeMethods.VLAD_InferenceData_V2_Draw(vladId, detectData, rawData, classCount, detectText);
        }

        public static int VLAD_Get_Ai_Ver(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Get_Ai_Ver(vladId);
        }

        public static int VLAD_Get_Msg_Ver(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Get_Msg_Ver(vladId);
        }

        public static void VLAD_Rtsp_Info_Monitoring_Registration(IntPtr vladId, int portNo)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Monitoring_Registration(vladId, portNo);
        }

        public static void VLAD_Rtsp_Info_Monitoring_SetFrame(IntPtr vladId, IntPtr rawData)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Monitoring_SetFrame(vladId, rawData);
        }

        public static void VLAD_Rtsp_Info_Client_Registration(
            IntPtr vladId,
            string urlInfo,
            string userName,
            int uiType,
            int monitorIndex,
            RTSP_Callback callback)
        {
            RtspCallbackBridge bridge = new RtspCallbackBridge(callback);
            RtspCallbackBridges.Add(bridge);
            VladNativeMethods.VLAD_Rtsp_Info_Client_Registration(vladId, urlInfo, userName, uiType, monitorIndex, bridge.Invoke);
        }

        public static void VLAD_Rtsp_Info_Client_Monitoring_Registration(
            IntPtr vladId,
            string urlInfo,
            int width,
            int height,
            RTSP_Callback callback)
        {
            RtspCallbackBridge bridge = new RtspCallbackBridge(callback);
            RtspCallbackBridges.Add(bridge);
            VladNativeMethods.VLAD_Rtsp_Info_Client_Monitoring_Registration(vladId, urlInfo, width, height, bridge.Invoke);
        }

        private sealed class RtspCallbackBridge
        {
            private readonly RTSP_Callback _callback;

            public RtspCallbackBridge(RTSP_Callback callback)
            {
                _callback = callback;
            }

            public void Invoke(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display)
            {
                if (_callback != null)
                {
                    _callback(vladId, userName, uiType, monitorIndex, display);
                }
            }
        }
    }
}
