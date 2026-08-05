using System;
using System.Drawing;
using System.IO;
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
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private const string DllName = "VLAD_SDK.dll";

        private static readonly object DllDirectorySyncRoot = new object();
        private static string _appliedDllDirectoryPath;

        static VladNativeMethods()
        {
            EnsureVladSdkDllDirectoryFromSettings();
        }

        public static bool EnsureVladSdkDllDirectoryFromSettings()
        {
            VladRuntimeSettings settings = VladRuntimeSettings.Load();
            return SetVladSdkDllDirectory(settings.ResolvedVladSdkDirectoryPath);
        }

        public static bool SetVladSdkDllDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return false;
            }

            lock (DllDirectorySyncRoot)
            {
                if (string.Equals(_appliedDllDirectoryPath, directoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                bool result = SetDllDirectory(directoryPath);
                if (result)
                {
                    _appliedDllDirectoryPath = directoryPath;
                }

                return result;
            }
        }

        [DllImport(DllName)]
        extern public static long VLAD_Custom_ID_Generate(int USER_ID, int MSG_VER, int MAJ_VER, int MIN_VER);

        [DllImport(DllName)]
        extern public static IntPtr VLAD_Custom_Registration(long custom_id, string ui_name, string root_name, string site, string modelPath, string custom_info, int gpu_id);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void RTSP_Callback(IntPtr vlad_id, string user_name, int ui_type, int mon_idx, IntPtr display);

        [DllImport(DllName, CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        extern public static void VLAD_Rtsp_Info_Client_Registration(IntPtr vlad_id, string url_info, string user_name, int ui_type, int mon_idx, RTSP_Callback callback);

        [DllImport(DllName)]
        extern public static IntPtr VLAD_Inference_Mat(IntPtr vlad_id, IntPtr raw_data, float threshold, int draw_mode);

        /// <summary>
        /// 전체 이미지/Crop 이미지 ID와 UTF-8 검사 Context를 받는 목표 HD export입니다.
        /// 현재 배포 DLL의 export 여부는 확인되지 않았으며, 새 DLL 배포 전에는 호출하면 안 됩니다.
        /// </summary>
        [DllImport(DllName, EntryPoint = "VLAD_HD_Inference_Mat", ExactSpelling = true)]
        extern public static IntPtr VLAD_HD_Inference_Mat(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr rawData,
            int drawMode,
            IntPtr requestJsonUtf8);

        /// <summary>
        /// 전체 이미지/Crop 이미지 ID를 사용해 UTF-8 검사 결과 JSON을 읽는 목표 HD export입니다.
        /// resultJsonUtf8는 호출자가 0으로 초기화한 8192 byte UTF-8 버퍼입니다.
        /// </summary>
        [DllImport(DllName, EntryPoint = "VLAD_HD_InferenceData_Result", ExactSpelling = true)]
        extern public static void VLAD_HD_InferenceData_Result(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr detectData,
            IntPtr resultJsonUtf8);
        /// <summary>
        /// 현재 배포 VLAD_SDK.dll의 레거시 단일 ID 유사도 검색 export입니다.
        /// 새 HD DLL이 배포되기 전의 호환 호출이므로 두 ID/UTF-8 JSON 계약에는 사용하지 않습니다.
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, EntryPoint = "VLAD_Search_Mat")]
        extern public static IntPtr VLAD_Search_Mat(IntPtr vlad_id, IntPtr raw_data, float threshold, int draw_mode, string searchContextJson);

        /// <summary>
        /// 전체 이미지/Crop 이미지 ID와 UTF-8 검색 Context를 받는 목표 HD 유사도 검색 export입니다.
        /// native EntryPoint 이름은 기존 계약과 동일한 VLAD_Search_Mat이며, 인자 수가 다른 overload로 구분합니다.
        /// 현재 배포 DLL의 단일 ID export와 ABI가 다르므로 새 DLL로 교체한 뒤에만 호출해야 합니다.
        /// </summary>
        [DllImport(DllName, EntryPoint = "VLAD_Search_Mat", ExactSpelling = true)]
        extern public static IntPtr VLAD_Search_Mat(IntPtr fullImageVladId, IntPtr croppedImageVladId, IntPtr rawData, int drawMode, IntPtr requestJsonUtf8);
        [DllImport(DllName)]
        extern public static int VLAD_InferenceData_Get_Valid_Count(IntPtr vlad_id, IntPtr detect_data);
        [DllImport(DllName)]
        extern public static int VLAD_InferenceData_V1_Draw(IntPtr vlad_id, IntPtr Detect_Data, IntPtr raw_data, IntPtr Class_cnt, StringBuilder Detect_Str, string Custom_Para, IntPtr Tlv_Info, int Tlv_Size);
        [DllImport(DllName)]
        extern public static unsafe bool VLAD_Custom_InferenceData_V1(IntPtr vlad_id, IntPtr Detect_Data, IntPtr raw_data, IntPtr Class_cnt, StringBuilder Detect_Str, string Custom_Para, IntPtr Tlv_Info, int Tlv_Size);

        /// <summary>
        /// 현재 배포 VLAD_SDK.dll의 레거시 단일 ID 검색 결과 export입니다.
        /// StringBuilder ANSI marshaling을 사용하므로 UTF-8 JSON 기반 HD 계약에는 사용하지 않습니다.
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, EntryPoint = "VLAD_Search_Data")]
        extern public static int VLAD_Search_Data(IntPtr vlad_id, IntPtr search_data, [Out] StringBuilder resultJson, int resultJsonCapacity);

        /// <summary>
        /// 전체 이미지/Crop 이미지 ID로 검색 결과 UTF-8 JSON을 읽는 목표 HD export입니다.
        /// resultJsonUtf8는 호출자가 0으로 초기화한 8192 byte UTF-8 버퍼입니다.
        /// </summary>
        [DllImport(DllName, EntryPoint = "VLAD_Search_ResultData", ExactSpelling = true)]
        extern public static void VLAD_Search_ResultData(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr searchData,
            IntPtr resultJsonUtf8);

        /// <summary>
        /// inputPath의 Top/Front/Back/Left/Right/Thickness 6장을 keyId 이름의 한 이미지로 병합합니다.
        /// outputPath는 병합 이미지가 생성될 대상 폴더입니다.
        /// </summary>
        [DllImport(DllName, CharSet = CharSet.Ansi, EntryPoint = "VLAD_HD_ImageMerge", ExactSpelling = true)]
        extern public static void VLAD_HD_ImageMerge(
            [MarshalAs(UnmanagedType.LPStr)] string inputPath,
            [MarshalAs(UnmanagedType.LPStr)] string keyId,
            [MarshalAs(UnmanagedType.LPStr)] string outputPath);


        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Ai_Ver(IntPtr vladId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Msg_Ver(IntPtr vladId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int VLAD_InferenceData_V2_Draw(IntPtr vladId, IntPtr detectData, IntPtr rawData, IntPtr classCount, StringBuilder detectText);


        // ====================================================================================================== //


        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Registration(int userId, int messageVersion, int majorVersion);


        [DllImport(DllName, CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall)]
        public static extern IntPtr VLAD_Ops_Inference_Registration(IntPtr vladId, string kindName, string siteName, string modelPath, string customInfo, int gpuId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern bool VLAD_Get_Log(IntPtr vladId, int logType);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Set_Log(IntPtr vladId, int logType);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Unset_Log(IntPtr vladId, int logType);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern bool VLAD_Unregistration(IntPtr vladId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern bool VLAD_Warm_Up(IntPtr vladId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Class_Count(IntPtr vladId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Inference_Registration(IntPtr vladId, string modelPath, int gpuId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Custom_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, IntPtr customParameter);



        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Rect_IntersectionArea(Rectangle destination, Rectangle source);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int VLAD_Custom_InferenceData_V1_Draw(IntPtr vladId, IntPtr detectData, IntPtr rawData, IntPtr classCount,
            StringBuilder detectText, string customParameter);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Get_Class_Color(IntPtr vladId, int classId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Get_Class_Str(IntPtr vladId, int classId);


        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_WONIK_Registration(string modelPath);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_WONIK_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, string valveType);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_Registration(string uiName, string kindName, string modelPath, int gpuId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_BOD_Registration(string uiName, string kindName, string modelPath, int gpuId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int location);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_BKG_Monitor_Display(IntPtr vladId, IntPtr display, IntPtr mainImage, IntPtr bottomLeft, IntPtr bottomCenter, IntPtr bottomRight);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_Corning_BKG_Monitor(IntPtr vladId, int index, IntPtr display);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_MPS_Registration_V2(string executeType, string modelPath, int kindCamera, int viewMode, int gpuId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_OPS_MPS_Registration_V2(string uiName, string executeType, string modelPath, int kindCamera, int viewMode, int gpuId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern IntPtr VLAD_MPS_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, int viewLocation, int limitOverflow, int limitProtrusion);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern void VLAD_Rtsp_Info_Monitoring_Registration(IntPtr vladId, int portNo);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern void VLAD_Rtsp_Info_Monitoring_SetFrame(IntPtr vladId, IntPtr rawData);

        [DllImport(DllName, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        public static extern void VLAD_Rtsp_Info_Client_Monitoring_Registration(IntPtr vladId, string urlInfo, int width, int height, RTSP_Callback callback);
    }
}
