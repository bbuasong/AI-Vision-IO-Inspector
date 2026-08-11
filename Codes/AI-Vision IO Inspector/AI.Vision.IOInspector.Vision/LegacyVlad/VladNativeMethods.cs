using System;
using System.Runtime.InteropServices;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_SDK.dll에서 export 되는 네이티브 함수를 한곳에 모아둔 P/Invoke 진입점입니다.
    /// 이 앱(HD)에서 실제로 쓰는 export만 남겨두었습니다.
    /// 계열 전용 함수와, HD 결과 수신에 더 이상 쓰지 않는 구버전 Draw/TLV 호환 함수는
    /// 2026-08-07에 정리했습니다.
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

        /// <summary>
        /// HD 검사 export입니다. requestJsonUtf8는 C#이 0으로 초기화해 할당한 8192 byte 버퍼이며,
        /// 요청 JSON을 채워 넘기면 AI DLL이 같은 버퍼에 viewJudge/score/dimensions/measurements를
        /// 갱신해 돌려줍니다(in-place 업데이트). 리턴값(void*)은 사용하지 않습니다.
        /// (vlad-hd-api-v1.3-correction-2026-08-07.md 참고)
        /// </summary>
        [DllImport(DllName, EntryPoint = "VLAD_HD_Inference_Mat", ExactSpelling = true)]
        extern public static IntPtr VLAD_HD_Inference_Mat(IntPtr fullImageVladId, IntPtr rawData, IntPtr requestJsonUtf8);

        /// <summary>
        /// HD 유사도 검색 export입니다. requestJsonUtf8는 VLAD_HD_Inference_Mat과 같은 in-place 버퍼
        /// 방식을 따릅니다. 리턴값(void*)은 사용하지 않습니다.
        /// </summary>
        [DllImport(DllName, EntryPoint = "VLAD_Search_Mat", ExactSpelling = true)]
        extern public static IntPtr VLAD_Search_Mat(IntPtr fullImageVladId, IntPtr rawData, IntPtr requestJsonUtf8);

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
        public static extern bool VLAD_Unregistration(IntPtr vladId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern bool VLAD_Warm_Up(IntPtr vladId);

        [DllImport(DllName, CharSet = CharSet.Ansi)]
        public static extern int VLAD_Get_Class_Count(IntPtr vladId);
    }
}
