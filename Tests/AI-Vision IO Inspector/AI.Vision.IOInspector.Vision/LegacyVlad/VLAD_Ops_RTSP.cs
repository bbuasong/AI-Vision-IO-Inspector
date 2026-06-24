using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using OpenCvSharp;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD_Ops의 RTSP 등록 및 프레임 콜백 흐름을 현재 프로젝트에서 호출하기 위한 호환 클래스입니다.
    /// 네이티브 SDK 콜백은 같은 VladId에 대해 재진입될 수 있으므로, 프레임 처리 구간은 직렬화합니다.
    /// </summary>
    public static class VLAD_Ops_RTSP
    {
        public const int MODE_TYPE_CAM = VLAD_Ops_Mode.MODE_TYPE_CAM;

        private const int DefaultFrameWidth = 1920;
        private const int DefaultFrameHeight = 1080;
        private const float DefaultThreshold = 0.5f;
        private const int DefaultRtspUiType = 7;
        private const int MaxValidCount = 1024;

        private static readonly object CallbackStateSync = new object();
        private static readonly object FrameProcSyncRoot = new object();
        private static readonly Dictionary<string, VLAD_Ops_RTSP_ThreadParam> CallbackParameters =
            new Dictionary<string, VLAD_Ops_RTSP_ThreadParam>();
        private static readonly HashSet<string> RegisteredClients = new HashSet<string>();
        private static readonly VladNativeMethods.RTSP_Callback FrameCallback = VLAD_Ops_RTSP_Frame_Proc;
        private static readonly VladNativeMethods.RTSP_Callback MonitorFrameCallback = VLAD_Ops_RTSP_Monitor_Frame_Proc;

        private static bool DisableCustomInferenceDataRead;

        public static VladNativeMethods.RTSP_Callback RTSP_Frame_Proc
        {
            get { return FrameCallback; }
        }

        public class VLAD_Ops_RTSP_ThreadParam
        {
            public IntPtr vlad_id;
            public string user_name;
            public int ui_type;
            public int mon_idx;
            public string rtsp_url;
            public string cam_name;
            public float threshold;
            public int frame_width;
            public int frame_height;

            public VLAD_Ops_RTSP_ThreadParam(
                IntPtr ptr,
                string userName,
                int uiType,
                int monitorIndex,
                string rtspUrl,
                string cameraName,
                float threshold)
                : this(ptr, userName, uiType, monitorIndex, rtspUrl, cameraName, threshold, DefaultFrameWidth, DefaultFrameHeight)
            {
            }

            public VLAD_Ops_RTSP_ThreadParam(
                IntPtr ptr,
                string userName,
                int uiType,
                int monitorIndex,
                string rtspUrl,
                string cameraName,
                float threshold,
                int frameWidth,
                int frameHeight)
            {
                vlad_id = ptr;
                user_name = userName;
                ui_type = uiType;
                mon_idx = monitorIndex;
                rtsp_url = rtspUrl;
                cam_name = cameraName;
                this.threshold = threshold;
                frame_width = frameWidth > 0 ? frameWidth : DefaultFrameWidth;
                frame_height = frameHeight > 0 ? frameHeight : DefaultFrameHeight;
            }
        }

        /// <summary>
        /// VLAD SDK가 RTSP 프레임을 받을 때 호출하는 콜백입니다.
        /// display 포인터는 SDK가 제공하는 BGR 프레임 버퍼로 보고 Mat 헤더만 감싸서 사용합니다.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static void VLAD_Ops_RTSP_Frame_Proc(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display)
        {
            if (vladId == IntPtr.Zero || display == IntPtr.Zero)
            {
                Debug.WriteLine("VLAD RTSP 콜백 건너뜀: VladId 또는 display 포인터가 비어 있습니다.");
                return;
            }

            if (!System.Threading.Monitor.TryEnter(FrameProcSyncRoot))
            {
                return;
            }

            try
            {
                VLAD_Ops_RTSP_ThreadParam threadParam = ResolveCallbackParameter(vladId, userName, uiType, monitorIndex);
                int frameWidth = threadParam == null ? DefaultFrameWidth : threadParam.frame_width;
                int frameHeight = threadParam == null ? DefaultFrameHeight : threadParam.frame_height;

                using (Mat mat = new Mat(frameHeight, frameWidth, MatType.CV_8UC3, display))
                {
                    lock (VLAD_Ops_Ai.NativeInferenceSyncRoot)
                    {
                        float threshold = threadParam == null ? 0.1f : threadParam.threshold;
                        IntPtr detectData = VLAD_Ops_Ai.VLAD_Inference_Mat(vladId, mat.CvPtr, threshold, 1);
                        if (detectData == IntPtr.Zero)
                        {
                            return;
                        }

                        int validCount = VLAD_Ops_Ai.VLAD_InferenceData_Get_Valid_Count(vladId, detectData);
                        if (validCount <= 0)
                        {
                            Cv2.WaitKey(30);
                            return;
                        }

                        if (validCount > MaxValidCount)
                        {
                            Debug.WriteLine("VLAD RTSP 콜백 건너뜀: validCount가 비정상적으로 큽니다. validCount=" + validCount.ToString());
                            return;
                        }

                        if (!DisableCustomInferenceDataRead)
                        {
                            // 검사 완료 Data
                            ReadCustomInferenceData(vladId, detectData, mat.CvPtr, validCount);
                        }
                    }
                }
            }
            catch (AccessViolationException ex)
            {
                DisableCustomInferenceDataRead = true;
                VLAD_Ops_Ai.BlockNativeInference("VLAD RTSP callback 결과 처리 중 보호 메모리 예외가 발생해 이후 VLAD 네이티브 추론을 중지합니다. Message=" + ex.Message);
                Debug.WriteLine("VLAD_Custom_InferenceData_V1 보호 메모리 예외: 이후 TLV 읽기를 중지합니다. " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP 콜백 처리 실패: " + ex.Message);
            }
            finally
            {
                System.Threading.Monitor.Exit(FrameProcSyncRoot);
            }
        }

        /// <summary>
        /// 기존 샘플과 같은 VLAD_Custom_InferenceData_V1 호출을 수행합니다.
        /// 네이티브 함수가 버퍼를 채우므로 호출 전 TLV 영역을 0으로 초기화합니다.
        /// </summary>
        private static void ReadCustomInferenceData(IntPtr vladId, IntPtr detectData, IntPtr rawData, int validCount)
        {
            int tlv_size = Marshal.SizeOf(typeof(Custom_Info_Struct));
            int bufferSize = checked(tlv_size * validCount);
            IntPtr tlv_info = Marshal.AllocHGlobal(bufferSize);

            try
            {
                byte[] empty = new byte[bufferSize];
                Marshal.Copy(empty, 0, tlv_info, bufferSize);

                StringBuilder detect_str = new StringBuilder(16384);
                VLAD_Ops_Ai.VLAD_Custom_InferenceData_V1(vladId, detectData, rawData, IntPtr.Zero, detect_str, string.Empty, tlv_info, tlv_size);
            }
            finally
            {
                Marshal.FreeHGlobal(tlv_info);
            }
        }

        /// <summary>
        /// 기존 VLAD_Ops의 RTSP 스레드 진입점 이름을 유지합니다.
        /// </summary>
        public static void VLAD_Ops_RTSP_Thread(object obj)
        {
            try
            {
                VLAD_Ops_RTSP_ThreadParam threadParam = obj as VLAD_Ops_RTSP_ThreadParam;
                if (threadParam == null)
                {
                    throw new ArgumentException("VLAD_Ops_RTSP_ThreadParam is required.", "obj");
                }

                VLAD_Ops_RTSP_Client_Registration(threadParam);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP Thread 실행 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// Sample_VLAD_SDK의 SetDllDirectory -> VLAD_Rtsp_Info_Client_Registration 흐름을 수행합니다.
        /// 이미 같은 VladId/URL/monitorIndex가 등록되어 있으면 중복 등록하지 않습니다.
        /// </summary>
        public static void VLAD_Ops_RTSP_Client_Registration(VLAD_Ops_RTSP_ThreadParam threadParam)
        {
            if (threadParam == null)
            {
                throw new ArgumentException("VLAD_Ops_RTSP_ThreadParam is required.", "threadParam");
            }

            if (string.IsNullOrWhiteSpace(threadParam.rtsp_url))
            {
                throw new ArgumentException("rtsp_url is required.", "threadParam");
            }

            IntPtr vladId = threadParam.vlad_id;
            string urlInfo = threadParam.rtsp_url;
            int monitorIndex = threadParam.mon_idx;

            if (IsClientRegistered(vladId, urlInfo, "HD", DefaultRtspUiType, monitorIndex))
            {
                StoreCallbackParameter(threadParam);
                return;
            }

            SetCurrentDirectoryAsDllDirectory();
            StoreCallbackParameter(threadParam);
            VLAD_Ops_Ai.VLAD_Rtsp_Info_Client_Registration(vladId, urlInfo, "HD", DefaultRtspUiType, monitorIndex, FrameCallback);
            MarkClientRegistered(vladId, urlInfo, "HD", DefaultRtspUiType, monitorIndex);
        }

        public static void VLAD_Ops_RTSP_Monitor_Frame_Proc(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display)
        {
            if (vladId == IntPtr.Zero || display == IntPtr.Zero)
            {
                return;
            }

            try
            {
                VLAD_Ops_RTSP_ThreadParam threadParam = ResolveCallbackParameter(vladId, userName, uiType, monitorIndex);
                int frameWidth = threadParam == null ? DefaultFrameWidth : threadParam.frame_width;
                int frameHeight = threadParam == null ? DefaultFrameHeight : threadParam.frame_height;

                using (Mat mat = new Mat(frameHeight, frameWidth, MatType.CV_8UC3, display))
                {
                    VLAD_Ops_Ai.VLAD_Rtsp_Info_Monitoring_SetFrame(vladId, mat.CvPtr);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP 모니터링 콜백 처리 실패: " + ex.Message);
            }
        }

        public static void VLAD_Ops_RTSP_Monitor_Thread(object obj)
        {
            try
            {
                SetCurrentDirectoryAsDllDirectory();

                VLAD_Ops_RTSP_ThreadParam threadParam = obj as VLAD_Ops_RTSP_ThreadParam;
                if (threadParam == null)
                {
                    throw new ArgumentException("VLAD_Ops_RTSP_ThreadParam is required.", "obj");
                }

                string rtspUrl = threadParam.rtsp_url;
                if (string.IsNullOrWhiteSpace(rtspUrl))
                {
                    throw new ArgumentException("rtsp_url is required.", "obj");
                }

                if (rtspUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) == false)
                {
                    rtspUrl = "rtsp://" + rtspUrl + ":8554/vlad";
                }

                StoreCallbackParameter(threadParam);
                VLAD_Ops_Ai.VLAD_Rtsp_Info_Client_Monitoring_Registration(
                    threadParam.vlad_id,
                    rtspUrl,
                    threadParam.frame_width,
                    threadParam.frame_height,
                    MonitorFrameCallback);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP Monitoring Thread 실행 실패: " + ex.Message);
            }
        }

        private static void StoreCallbackParameter(VLAD_Ops_RTSP_ThreadParam threadParam)
        {
            lock (CallbackStateSync)
            {
                CallbackParameters[BuildCallbackKey(threadParam.vlad_id, threadParam.user_name, threadParam.ui_type, threadParam.mon_idx)] = threadParam;
                CallbackParameters[BuildCallbackKey(threadParam.vlad_id, null, threadParam.ui_type, threadParam.mon_idx)] = threadParam;
            }
        }

        public static void StoreCallbackParameterForClient(IntPtr vladId, string urlInfo, string userName, int uiType, int monitorIndex)
        {
            VLAD_Ops_RTSP_ThreadParam threadParam = new VLAD_Ops_RTSP_ThreadParam(
                vladId,
                userName,
                uiType,
                monitorIndex,
                urlInfo,
                "CAM" + monitorIndex.ToString(),
                DefaultThreshold,
                DefaultFrameWidth,
                DefaultFrameHeight);

            StoreCallbackParameter(threadParam);
        }

        public static bool IsClientRegistered(IntPtr vladId, string urlInfo, string userName, int uiType, int monitorIndex)
        {
            lock (CallbackStateSync)
            {
                return RegisteredClients.Contains(BuildRtspRegistrationKey(vladId, urlInfo, userName, uiType, monitorIndex));
            }
        }

        public static void MarkClientRegistered(IntPtr vladId, string urlInfo, string userName, int uiType, int monitorIndex)
        {
            lock (CallbackStateSync)
            {
                RegisteredClients.Add(BuildRtspRegistrationKey(vladId, urlInfo, userName, uiType, monitorIndex));
            }
        }

        private static VLAD_Ops_RTSP_ThreadParam ResolveCallbackParameter(IntPtr vladId, string userName, int uiType, int monitorIndex)
        {
            lock (CallbackStateSync)
            {
                VLAD_Ops_RTSP_ThreadParam threadParam;
                if (CallbackParameters.TryGetValue(BuildCallbackKey(vladId, userName, uiType, monitorIndex), out threadParam))
                {
                    return threadParam;
                }

                if (CallbackParameters.TryGetValue(BuildCallbackKey(vladId, null, uiType, monitorIndex), out threadParam))
                {
                    return threadParam;
                }
            }

            return null;
        }

        private static string BuildCallbackKey(IntPtr vladId, string userName, int uiType, int monitorIndex)
        {
            return vladId.ToInt64().ToString() + "|" + (userName ?? string.Empty) + "|" + uiType.ToString() + "|" + monitorIndex.ToString();
        }

        private static string BuildRtspRegistrationKey(IntPtr vladId, string urlInfo, string userName, int uiType, int monitorIndex)
        {
            return vladId.ToInt64().ToString() + "|" + (urlInfo ?? string.Empty) + "|" + (userName ?? string.Empty) + "|" + uiType.ToString() + "|" + monitorIndex.ToString();
        }

        private static void SetCurrentDirectoryAsDllDirectory()
        {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrWhiteSpace(path) == false)
            {
                SetDllDirectory(path);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string pathName);
    }
}
