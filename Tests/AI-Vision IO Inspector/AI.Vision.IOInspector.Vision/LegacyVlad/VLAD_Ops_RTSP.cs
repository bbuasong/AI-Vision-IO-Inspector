using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    public static class VLAD_Ops_RTSP
    {
        public const int MODE_TYPE_CAM = 0;
        private const int DefaultFrameWidth = 1920;
        private const int DefaultFrameHeight = 1080;
        private const float DefaultThreshold = 0.5f;

        private static readonly object CallbackStateSync = new object();
        private static readonly Dictionary<string, VLAD_Ops_RTSP_ThreadParam> CallbackParameters =
            new Dictionary<string, VLAD_Ops_RTSP_ThreadParam>();
        private static readonly VladNativeMethods.RTSP_Callback FrameCallback = VLAD_Ops_RTSP_Frame_Proc;
        private static readonly VladNativeMethods.RTSP_Callback MonitorFrameCallback = VLAD_Ops_RTSP_Monitor_Frame_Proc;

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
        /// RTSP 스트림에서 프레임을 받을 때 VLAD SDK가 호출하는 콜백입니다.
        /// display는 SDK가 넘겨주는 OpenCV Mat 데이터 포인터이며, 예외가 밖으로 나가면 프로세스가 종료될 수 있으므로 내부에서 처리합니다.
        /// </summary>
        public static void VLAD_Ops_RTSP_Frame_Proc(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display)
        {
            if (display == IntPtr.Zero)
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
                    if (uiType != MODE_TYPE_CAM)
                    {
                        return;
                    }

                    float threshold = threadParam == null ? DefaultThreshold : threadParam.threshold;
                    IntPtr detectData = VLAD_Ops_Ai.VLAD_Inference_Mat(vladId, mat.CvPtr, threshold, 0);
                    int validCount = VLAD_Ops_Ai.VLAD_InferenceData_Get_Valid_Count(vladId, detectData);
                    if (validCount <= 0)
                    {
                        return;
                    }

                    int classCount = VLAD_Ops_Ai.VLAD_Get_Class_Count(vladId);
                    if (classCount < 1)
                    {
                        classCount = validCount;
                    }

                    int[] classList = new int[classCount];
                    VLAD_Ops_Ai.VLAD_Corning_BKG_Monitor(vladId, monitorIndex, mat.CvPtr);
                    VLAD_Ops_Ai.VLAD_Ops_Ai_Cam_InferenceData(vladId, detectData, mat, classList, null, IntPtr.Zero, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP 콜백 처리 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// RTSP 스트림 수신과 콜백 등록을 시작하는 기존 VLAD_Ops 호환 스레드 진입점입니다.
        /// </summary>
        public static void VLAD_Ops_RTSP_Thread(object obj)
        {
            try
            {
                SetCurrentDirectoryAsDllDirectory();

                VLAD_Ops_RTSP_ThreadParam threadParam = obj as VLAD_Ops_RTSP_ThreadParam;
                if (threadParam == null)
                {
                    throw new ArgumentException("VLAD_Ops_RTSP_ThreadParam is required.", "obj");
                }

                if (string.IsNullOrWhiteSpace(threadParam.rtsp_url))
                {
                    throw new ArgumentException("rtsp_url is required.", "obj");
                }

                StoreCallbackParameter(threadParam);
                VLAD_Ops_Ai.VLAD_Rtsp_Info_Client_Registration(
                    threadParam.vlad_id,
                    threadParam.rtsp_url,
                    threadParam.user_name,
                    threadParam.ui_type,
                    threadParam.mon_idx,
                    FrameCallback);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP Thread 실행 실패: " + ex.Message);
            }
        }

        public static void VLAD_Ops_RTSP_Monitor_Frame_Proc(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display)
        {
            if (display == IntPtr.Zero)
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
