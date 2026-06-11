using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_SDK.dll에서 export 되는 네이티브 함수를 한곳에 모아둔 P/Invoke 진입점입니다.
    /// 기존 VLAD_Ops 담당자가 함수명을 그대로 검색할 수 있도록 SDK 원래 함수명과 인자 순서를 유지합니다.
    /// </summary>
    public static class VladNativeMethods
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
        public static extern IntPtr VLAD_Custom_Inference_Mat(
            IntPtr vladId,
            IntPtr rawData,
            float threshold,
            int drawMode,
            IntPtr customParameter);

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
        public static extern bool VLAD_Custom_InferenceData_V1(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Rect_IntersectionArea(Rectangle destination, Rectangle source);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern int VLAD_Custom_InferenceData_V1_Draw(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter);

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
        public static extern IntPtr VLAD_WONIK_Registration(string modelPath);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_WONIK_Inference_Mat(
            IntPtr vladId,
            IntPtr rawData,
            float threshold,
            int drawMode,
            string valveType);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_Registration(string uiName, string kindName, string modelPath, int gpuId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_BOD_Registration(string uiName, string kindName, string modelPath, int gpuId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int location);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_BKG_Monitor_Display(
            IntPtr vladId,
            IntPtr display,
            IntPtr mainImage,
            IntPtr bottomLeft,
            IntPtr bottomCenter,
            IntPtr bottomRight);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_BKG_Monitor(IntPtr vladId, int index, IntPtr display);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_MPS_Registration_V2(
            string executeType,
            string modelPath,
            int kindCamera,
            int viewMode,
            int gpuId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_OPS_MPS_Registration_V2(
            string uiName,
            string executeType,
            string modelPath,
            int kindCamera,
            int viewMode,
            int gpuId);

        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_MPS_Inference_Mat(
            IntPtr vladId,
            IntPtr rawData,
            float threshold,
            int drawMode,
            int viewLocation,
            int limitOverflow,
            int limitProtrusion);

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
