using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD_SDK 진입점에 대한 P/Invoke 선언을 모아둔 클래스입니다.
    /// 기존 함수명을 최대한 유지해 VLAD 담당자가 예전 코드와 새 코드를 쉽게 비교할 수 있게 합니다.
    /// </summary>
    internal static class VladNativeMethods
    {
        [DllImport("VLAD_SDK.dll", CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Registration(int userId, int messageVersion, int majorVersion);

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
    }
}
