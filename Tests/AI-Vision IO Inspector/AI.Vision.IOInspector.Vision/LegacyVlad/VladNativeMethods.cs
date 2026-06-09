using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD_SDK.dll의 export 함수 선언을 한 곳에 모아둔 P/Invoke 진입점입니다.
    /// 기존 VLAD_Ops 담당자가 함수명을 그대로 검색할 수 있도록 SDK 원 함수명과 순서를 최대한 유지합니다.
    /// </summary>
    internal static class VladNativeMethods
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RTSP_Callback(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Registration(int userId, int messageVersion, int majorVersion);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern long VLAD_Custom_ID_Generate(int userId, int messageVersion, int majorVersion, int minorVersion);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Custom_Registration(
            long customId,
            string uiName,
            string rootName,
            string siteName,
            string modelPath,
            string customInfo,
            int gpuId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Ops_Inference_Registration(
            IntPtr vladId,
            string kindName,
            string siteName,
            string modelPath,
            string customInfo,
            int gpuId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern bool VLAD_Get_Log(IntPtr vladId, int logType);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Set_Log(IntPtr vladId, int logType);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Unset_Log(IntPtr vladId, int logType);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Class_Count(IntPtr vladId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Inference_Registration(IntPtr vladId, string modelPath, int gpuId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Custom_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, IntPtr customParameter);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_InferenceData_V1_Draw(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_InferenceData_V2_Draw(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_InferenceData_Get_Valid_Count(IntPtr vladId, IntPtr detectData);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Get_Class_Color(IntPtr vladId, int classId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Get_Class_Str(IntPtr vladId, int classId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Ai_Ver(IntPtr vladId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Msg_Ver(IntPtr vladId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern void VLAD_Rtsp_Info_Monitoring_Registration(IntPtr vladId, int portNo);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern void VLAD_Rtsp_Info_Monitoring_SetFrame(IntPtr vladId, IntPtr rawData);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void VLAD_Rtsp_Info_Client_Registration(
            IntPtr vladId,
            string urlInfo,
            string userName,
            int uiType,
            int monitorIndex,
            RTSP_Callback callback);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void VLAD_Rtsp_Info_Client_Monitoring_Registration(
            IntPtr vladId,
            string urlInfo,
            int width,
            int height,
            RTSP_Callback callback);
    }
}
