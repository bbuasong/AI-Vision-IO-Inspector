using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using OpenCvSharp;

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

    public struct Custom_Point
    {
        public int x;
        public int y;
    }

    public struct Custom_Info_Struct
    {
        public int class_id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cls_name;
        public float score;
        public Custom_Point p1;
        public Custom_Point p2;
    }

    public enum SDK_AI_MSG
    {
        AI_MSG_V0,
        AI_MSG_V1,
        AI_MSG_V2,
        AI_MSG_V3
    }

    /// <summary>
    /// 기존 VLAD_Ops_Ai.cs의 함수명을 현재 프로젝트에서 그대로 검색하고 호출할 수 있게 만든 호환 클래스입니다.
    /// 실제 P/Invoke 선언은 VladNativeMethods에 모아두고, 이 클래스는 기존 코드와 같은 이름의 진입점 역할을 합니다.
    /// </summary>
    public static class VLAD_Ops_Ai
    {
        public const string RegistrationLogEnvironmentVariableName = "AI_VISION_VLAD_REGISTRATION_LOG";
        private static readonly object RegistrationLogLock = new object();
        private static readonly object NativeInferenceLock = new object();
        private static volatile bool NativeInferenceBlocked;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string pathName);

        /// <summary>
        /// VLAD_SDK 추론 함수는 같은 VladId에 대한 재진입 안전성이 확인되지 않았습니다.
        /// RTSP callback과 검사 버튼 경로가 동시에 들어와도 네이티브 메모리가 겹치지 않도록 공유 lock을 사용합니다.
        /// </summary>
        public static object NativeInferenceSyncRoot
        {
            get { return NativeInferenceLock; }
        }

        public static void BlockNativeInference(string message)
        {
            NativeInferenceBlocked = true;
            AppendRegistrationLog("INFERENCE_BLOCKED", message);
        }

        public static IntPtr VLAD_Registration(int user, int msg, int maj)
        {
            return VladNativeMethods.VLAD_Registration(user, msg, maj);
        }

        public static IntPtr VLAD_Ops_Inference_Registration(IntPtr vladId, string kindName, string siteName, string modelPath, string customInfo, int gpuId)
        {
            return VladNativeMethods.VLAD_Ops_Inference_Registration(vladId, kindName, siteName, modelPath, customInfo, gpuId);
        }

        public static bool VLAD_Get_Log(IntPtr vladId, int logType)
        {
            return VladNativeMethods.VLAD_Get_Log(vladId, logType);
        }

        public static IntPtr VLAD_Set_Log(IntPtr vladId, int logType)
        {
            return VladNativeMethods.VLAD_Set_Log(vladId, logType);
        }

        public static IntPtr VLAD_Unset_Log(IntPtr vladId, int logType)
        {
            return VladNativeMethods.VLAD_Unset_Log(vladId, logType);
        }

        public static bool VLAD_Unregistration(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Unregistration(vladId);
        }

        public static bool VLAD_Warm_Up(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Warm_Up(vladId);
        }

        public static long VLAD_Custom_ID_Generate(int userId, int msgVer, int majVer, int minVer)
        {
            return VladNativeMethods.VLAD_Custom_ID_Generate(userId, msgVer, majVer, minVer);
        }

        public static IntPtr VLAD_Custom_Registration(long customId, string uiName, string rootName, string site, string modelPath, string customInfo, int gpuId)
        {
            // Log 추가... (환경 변수 AI_VISION_VLAD_REGISTRATION_LOG이 설정된 경우, 등록 시도와 결과를 파일로 기록) // for Test..
            //AppendRegistrationLog(
            //    "CUSTOM_REGISTRATION_CALL",
            //    "VLAD_Custom_Registration 호출. CustomId=" + customId.ToString() +
            //    ", UiName=" + SafeText(uiName) +
            //    ", RootName=" + SafeText(rootName) +
            //    ", Site=" + SafeText(site) +
            //    ", ModelPath=" + SafeText(modelPath) +
            //    ", CustomInfo=" + SafeText(customInfo) +
            //    ", GpuId=" + gpuId.ToString());

            try
            {
                IntPtr vladId = VladNativeMethods.VLAD_Custom_Registration(customId, uiName, rootName, site, modelPath, customInfo, gpuId);
                //AppendRegistrationLog(
                //    "CUSTOM_REGISTRATION_RETURN",
                //    "VLAD_Custom_Registration 반환. VladId=" + FormatPointer(vladId) +
                //    ", IsZero=" + (vladId == IntPtr.Zero).ToString());
                return vladId;
            }
            catch (Exception ex)
            {
                AppendRegistrationLog("CUSTOM_REGISTRATION_EXCEPTION", ex.ToString());
                throw;
            }
        }

        public static int VLAD_Get_Class_Count(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Get_Class_Count(vladId);
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static IntPtr VLAD_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode)
        {
            if (NativeInferenceBlocked)
            {
                throw new InvalidOperationException("이전 VLAD_Inference_Mat 호출에서 보호 메모리 예외가 발생해 현재 프로세스의 VLAD 네이티브 추론을 중지했습니다. 앱을 재시작하고 모델/GPU/입력 Mat 구성을 확인하십시오.");
            }

            if (vladId == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_Inference_Mat 호출 실패: VladId가 비어 있습니다.", "vladId");
            }

            if (rawData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_Inference_Mat 호출 실패: OpenCV Mat 포인터가 비어 있습니다.", "rawData");
            }

            try
            {
                lock (NativeInferenceLock)
                {
                    return VladNativeMethods.VLAD_Inference_Mat(vladId, rawData, threshold, drawMode);
                }
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_Inference_Mat 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 추론을 중지합니다.";
                BlockNativeInference(message);
                AppendRegistrationLog(
                    "INFERENCE_ACCESS_VIOLATION",
                    message +
                    " VladId=" + FormatPointer(vladId) +
                    ", RawData=" + FormatPointer(rawData) +
                    ", Threshold=" + threshold.ToString() +
                    ", DrawMode=" + drawMode.ToString() +
                    ", Message=" + ex.Message);
                throw new InvalidOperationException(message, ex);
            }
        }

        public static IntPtr VLAD_Custom_Inference_Mat(
            IntPtr vladId,
            IntPtr rawData,
            float threshold,
            int drawMode,
            IntPtr customParameter)
        {
            return VladNativeMethods.VLAD_Custom_Inference_Mat(vladId, rawData, threshold, drawMode, customParameter);
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
            return VladNativeMethods.VLAD_InferenceData_V1_Draw(vladId, detectData, rawData, classCount,
                detectText, customParameter, tlvInfo, tlvSize);
        }

        public static IntPtr VLAD_Get_Class_Color(IntPtr vladId, int classId)
        {
            return VladNativeMethods.VLAD_Get_Class_Color(vladId, classId);
        }

        public static IntPtr VLAD_Get_Class_Str(IntPtr vladId, int classId)
        {
            return VladNativeMethods.VLAD_Get_Class_Str(vladId, classId);
        }

        public static int VLAD_InferenceData_Get_Valid_Count(IntPtr vladId, IntPtr detectData)
        {
            return VladNativeMethods.VLAD_InferenceData_Get_Valid_Count(vladId, detectData);
        }

        public static int VLAD_Get_Ai_Ver(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Get_Ai_Ver(vladId);
        }

        public static int VLAD_Get_Msg_Ver(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Get_Msg_Ver(vladId);
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

        // 결과값
        public static unsafe bool VLAD_Custom_InferenceData_V1(IntPtr vladId, IntPtr detectData, IntPtr rawData, IntPtr classCount, StringBuilder detectText, string customParameter, IntPtr tlvInfo, int tlvSize)
        {
            return VladNativeMethods.VLAD_Custom_InferenceData_V1(vladId, detectData, rawData, classCount, detectText, customParameter, tlvInfo, tlvSize);
        }

        public static int VLAD_Get_Rect_IntersectionArea(Rectangle destination, Rectangle source)
        {
            return VladNativeMethods.VLAD_Get_Rect_IntersectionArea(destination, source);
        }

        public static int VLAD_Custom_InferenceData_V1_Draw(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter)
        {
            return VladNativeMethods.VLAD_Custom_InferenceData_V1_Draw(vladId, detectData, rawData, classCount,
                detectText, customParameter);
        }

        public static IntPtr VLAD_WONIK_Registration(string modelPath)
        {
            return VladNativeMethods.VLAD_WONIK_Registration(modelPath);
        }

        public static IntPtr VLAD_WONIK_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, string valveType)
        {
            return VladNativeMethods.VLAD_WONIK_Inference_Mat(vladId, rawData, threshold, drawMode, valveType);
        }

        public static IntPtr VLAD_Corning_Registration(string uiName, string kindName, string modelPath, int gpuId)
        {
            return VladNativeMethods.VLAD_Corning_Registration(uiName, kindName, modelPath, gpuId);
        }

        public static IntPtr VLAD_Corning_BOD_Registration(string uiName, string kindName, string modelPath, int gpuId)
        {
            return VladNativeMethods.VLAD_Corning_BOD_Registration(uiName, kindName, modelPath, gpuId);
        }

        public static IntPtr VLAD_Corning_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int location)
        {
            return VladNativeMethods.VLAD_Corning_Inference_Mat(vladId, rawData, threshold, location);
        }

        public static IntPtr VLAD_Corning_BKG_Monitor_Display(IntPtr vladId, IntPtr display, IntPtr mainImage, IntPtr bottomLeft, IntPtr bottomCenter, IntPtr bottomRight)
        {
            return VladNativeMethods.VLAD_Corning_BKG_Monitor_Display(vladId, display, mainImage, bottomLeft, bottomCenter, bottomRight);
        }

        public static IntPtr VLAD_Corning_BKG_Monitor(IntPtr vladId, int index, IntPtr display)
        {
            return VladNativeMethods.VLAD_Corning_BKG_Monitor(vladId, index, display);
        }

        public static IntPtr VLAD_MPS_Registration_V2(string executeType, string modelPath, int kindCamera, int viewMode, int gpuId)
        {
            return VladNativeMethods.VLAD_MPS_Registration_V2(executeType, modelPath, kindCamera, viewMode, gpuId);
        }

        public static IntPtr VLAD_OPS_MPS_Registration_V2(string uiName, string executeType, string modelPath, int kindCamera, int viewMode, int gpuId)
        {
            return VladNativeMethods.VLAD_OPS_MPS_Registration_V2(uiName, executeType, modelPath, kindCamera, viewMode, gpuId);
        }

        public static IntPtr VLAD_MPS_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, int viewLocation, int limitOverflow, int limitProtrusion)
        {
            return VladNativeMethods.VLAD_MPS_Inference_Mat(vladId, rawData, threshold, drawMode,
                viewLocation, limitOverflow, limitProtrusion);
        }

        public static void VLAD_Rtsp_Info_Monitoring_Registration(IntPtr vladId, int portNo)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Monitoring_Registration(vladId, portNo);
        }

        // ☆★☆★☆★☆★ RTSP 모니터링 등록 시, 사용자 정의 콜백 함수를 통해 RTSP 스트림의 프레임을 실시간으로 처리 가능 ☆★☆★☆★☆★
        public static void VLAD_Rtsp_Info_Client_Registration(IntPtr vladId, string urlInfo, string userName, int uiType, int monitorIndex, VladNativeMethods.RTSP_Callback callback)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Client_Registration(vladId, urlInfo, userName, uiType, monitorIndex, callback);
        }

        public static void VLAD_Rtsp_Info_Client_Monitoring_Registration(IntPtr vladId, string urlInfo, int width, int height, VladNativeMethods.RTSP_Callback callback)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Client_Monitoring_Registration(vladId, urlInfo, width, height, callback);
        }

        public static void VLAD_Rtsp_Info_Monitoring_SetFrame(IntPtr vladId, IntPtr rawData)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Monitoring_SetFrame(vladId, rawData);
        }

        public static IntPtr VLAD_Ops_Ai_Env_Start(int user, string rootName, string siteName, int msgVer, int majVer, string modelPath, int gpuId)
        {
            AppendRegistrationLog(
                "ENV_START_ENTER",
                "VLAD_Ops_Ai_Env_Start 진입. User=" + user.ToString() +
                ", RootName=" + SafeText(rootName) +
                ", SiteName=" + SafeText(siteName) +
                ", MsgVer=" + msgVer.ToString() +
                ", MajVer=" + majVer.ToString() +
                ", ModelPath=" + SafeText(modelPath) +
                ", GpuId=" + gpuId.ToString());

            string url_info = ResolveDefaultRtspUrl();
            string modelPathWithTrailingSlash = EnsureTrailingSlash(modelPath);

            long customId = VLAD_Custom_ID_Generate((int)SDK_USER.USER_CUS_STD, (int)SDK_MSG.MSG_V1, (int)SDK_MAJ.MAJ_V1, gpuId);
            string parameter = "{\"MODEL\":0,\"CAM\":0}";

            AppendRegistrationLog("CUSTOM_ID", "VLAD_Custom_ID_Generate 반환. CustomId=" + customId.ToString());
            AppendRegistrationLog(
                "CUSTOM_REGISTRATION_CALL",
                "VLAD_Custom_Registration 호출. CustomId=" + customId.ToString() +
                ", Ui=CUSTOM, Root=null, Site=HD, ModelPath=" + SafeText(modelPathWithTrailingSlash) +
                ", GpuId=" + gpuId.ToString());

            IntPtr vladId = VLAD_Custom_Registration(customId, "CUSTOM", null, "HD", modelPathWithTrailingSlash, parameter, gpuId);
            AppendRegistrationLog("CUSTOM_REGISTRATION_RETURN", "VLAD_Custom_Registration 반환. VladId=" + FormatPointer(vladId));

            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrWhiteSpace(path))
            {
                SetDllDirectory(path);
                AppendRegistrationLog("SET_DLL_DIRECTORY", "SetDllDirectory 호출. Path=" + SafeText(path));
            }

            if (vladId != IntPtr.Zero && !string.IsNullOrWhiteSpace(url_info))
            {
                const int rtspUiType = 7;
                const int monitorIndex = 0;
                const string rtspUserName = "HD";

                VLAD_Ops_RTSP.StoreCallbackParameterForClient(vladId, url_info, rtspUserName, rtspUiType, monitorIndex);
                VLAD_Rtsp_Info_Client_Registration(vladId, url_info, rtspUserName, rtspUiType, monitorIndex, VLAD_Ops_RTSP.RTSP_Frame_Proc);
                VLAD_Ops_RTSP.MarkClientRegistered(vladId, url_info, rtspUserName, rtspUiType, monitorIndex);
                AppendRegistrationLog("RTSP_REGISTRATION_RETURN", "VLAD_Rtsp_Info_Client_Registration 호출 완료. Url=" + SafeText(url_info));
            }
            else
            {
                AppendRegistrationLog("RTSP_REGISTRATION_SKIP", "VladId가 0이거나 RTSP URL이 비어 있어 RTSP 등록을 건너뜁니다.");
            }

            AppendRegistrationLog("ENV_START_RETURN", "VLAD_Ops_Ai_Env_Start 반환. VladId=" + FormatPointer(vladId));
            return vladId;
        }

        /// <summary>
        /// 기존 VLAD_Ops_Ai_Cam_InferenceData 함수명과 역할을 유지하기 위한 호환 진입점입니다.
        /// detectData를 SDK Draw 함수로 넘겨 classList/detectText/TLV 정보를 채우고, 호출자는 기존 코드처럼 결과 존재 여부만 확인할 수 있습니다.
        /// </summary>
        public static bool VLAD_Ops_Ai_Cam_InferenceData(
            IntPtr vladId,
            IntPtr detectData,
            Mat outputImage,
            int[] classList,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize)
        {
            if (vladId == IntPtr.Zero || detectData == IntPtr.Zero || outputImage == null)
            {
                return false;
            }

            if (classList == null || classList.Length == 0)
            {
                int classCount = VLAD_Get_Class_Count(vladId);
                if (classCount < 1)
                {
                    classCount = 1;
                }

                classList = new int[classCount];
            }

            StringBuilder detectText = new StringBuilder(8192);
            GCHandle classListHandle = GCHandle.Alloc(classList, GCHandleType.Pinned);
            try
            {
                int aiVersion = VLAD_Get_Ai_Ver(vladId);
                int messageVersion = VLAD_Get_Msg_Ver(vladId);
                IntPtr classListPointer = classListHandle.AddrOfPinnedObject();

                if (messageVersion == (int)SDK_MSG.MSG_V2)
                {
                    VLAD_InferenceData_V2_Draw(vladId, detectData, outputImage.CvPtr, classListPointer, detectText);
                }
                else if (aiVersion == (int)SDK_USER.USER_CUS_STD ||
                         aiVersion == (int)SDK_USER.USER_SRD ||
                         aiVersion == (int)SDK_USER.USER_MPS ||
                         aiVersion == (int)SDK_USER.USER_ATS)
                {
                    VLAD_Custom_InferenceData_V1(vladId, detectData, outputImage.CvPtr, classListPointer,
                        detectText, customParameter ?? string.Empty, tlvInfo, tlvSize);
                }
                else
                {
                    VLAD_InferenceData_V1_Draw(vladId, detectData, outputImage.CvPtr,
                        classListPointer, detectText, customParameter ?? string.Empty, tlvInfo, tlvSize);
                }
            }
            finally
            {
                classListHandle.Free();
            }

            return VLAD_InferenceData_Get_Valid_Count(vladId, detectData) > 0;
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return path;
            }

            if (path.EndsWith("\\", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
            {
                return path;
            }

            return path + "\\";
        }

        private static string ResolveDefaultRtspUrl()
        {
            string rtspUrlFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_VLAD_RTSP_URL");
            if (string.IsNullOrWhiteSpace(rtspUrlFromEnvironment) == false)
            {
                return rtspUrlFromEnvironment;
            }

            string configPath = FindConfigJsonPath();
            if (File.Exists(configPath) == false)
            {
                AppendRegistrationLog("RTSP_URL_NOT_FOUND", "CFG\\Config.json 파일을 찾을 수 없습니다. Path=" + SafeText(configPath));
                return string.Empty;
            }

            string text = File.ReadAllText(configPath, Encoding.UTF8);
            string rtspUrl = ExtractJsonText(text, "CAM_RTSP_IP", string.Empty);
            AppendRegistrationLog("RTSP_URL_RESOLVED", "Config.json RTSP URL 확인. Path=" + configPath + ", Url=" + SafeText(rtspUrl));
            return rtspUrl;
        }

        private static string FindConfigJsonPath()
        {
            string currentDirectoryConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "CFG", "Config.json");
            if (File.Exists(currentDirectoryConfigPath))
            {
                return currentDirectoryConfigPath;
            }

            string currentPath = AppContext.BaseDirectory;
            int depth = 0;

            while (string.IsNullOrWhiteSpace(currentPath) == false && depth < 12)
            {
                string candidatePath = Path.Combine(currentPath, "CFG", "Config.json");
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }

                DirectoryInfo parent = Directory.GetParent(currentPath);
                if (parent == null)
                {
                    break;
                }

                currentPath = parent.FullName;
                depth++;
            }

            return Path.Combine(AppContext.BaseDirectory, "CFG", "Config.json");
        }

        private static string ExtractJsonText(string text, string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return defaultValue;
            }

            int colonIndex = text.IndexOf(':', keyIndex);
            if (colonIndex < 0)
            {
                return defaultValue;
            }

            int firstQuoteIndex = text.IndexOf('"', colonIndex + 1);
            if (firstQuoteIndex < 0)
            {
                return defaultValue;
            }

            int secondQuoteIndex = text.IndexOf('"', firstQuoteIndex + 1);
            if (secondQuoteIndex < 0)
            {
                return defaultValue;
            }

            return text.Substring(firstQuoteIndex + 1, secondQuoteIndex - firstQuoteIndex - 1);
        }

        private static void AppendRegistrationLog(string status, string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                              " [" + status + "] " +
                              message +
                              Environment.NewLine;

                Debug.WriteLine(line);

                string logFilePath = Environment.GetEnvironmentVariable(RegistrationLogEnvironmentVariableName);
                if (string.IsNullOrWhiteSpace(logFilePath))
                {
                    return;
                }

                string directoryPath = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                lock (RegistrationLogLock)
                {
                    File.AppendAllText(logFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 등록 추적 로그 실패가 VLAD 원본 호출 흐름을 막으면 안 됩니다.
            }
        }

        private static string FormatPointer(IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return "0x0";
            }

            return "0x" + value.ToInt64().ToString("X");
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }
    }
}
