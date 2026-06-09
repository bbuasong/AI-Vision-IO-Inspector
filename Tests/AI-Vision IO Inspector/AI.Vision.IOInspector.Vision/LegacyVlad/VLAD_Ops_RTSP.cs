using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD_Ops_RTSP.cs의 스레드 파라미터, 프레임 callback, 등록 함수 흐름을 유지한 호환 진입점입니다.
    /// 현재 검사 UI의 연속 영상 표시는 WPF 전용 RtspVideoHost/LibVLC 경로를 사용하고, 이 클래스는 VLAD SDK 방식 RTSP 연동 검증용으로 둡니다.
    /// </summary>
    public static class VLAD_Ops_RTSP
    {
        public const int MODE_TYPE_CAM = 0;

        public class VLAD_Ops_RTSP_ThreadParam
        {
            public IntPtr vlad_id;
            public string user_name;
            public int ui_type;
            public int mon_idx;
            public string rtsp_url;
            public string cam_name;
            public float threshold;

            public VLAD_Ops_RTSP_ThreadParam(
                IntPtr ptr,
                string userName,
                int uiType,
                int monitorIndex,
                string rtspUrl,
                string cameraName,
                float threshold)
            {
                vlad_id = ptr;
                user_name = userName;
                ui_type = uiType;
                mon_idx = monitorIndex;
                rtsp_url = rtspUrl;
                cam_name = cameraName;
                this.threshold = threshold;
            }
        }

        public static void VLAD_Ops_RTSP_Frame_Proc(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display)
        {
            if (display == IntPtr.Zero)
            {
                return;
            }

            // 기존 VLAD_Ops는 display 포인터를 OpenCV Mat으로 감싸고 VLAD_Inference_Mat에 전달했습니다.
            // 현재 프로젝트에서는 이 함수가 SDK callback 진입 여부를 확인하는 기준점이며,
            // 실제 측정값 변환은 AI 담당자가 rawData 포맷을 확정한 뒤 VisionAiInferenceService로 연결해야 합니다.
            if (uiType == MODE_TYPE_CAM)
            {
                IntPtr detectData = VLAD_Ops_Ai.VLAD_Inference_Mat(vladId, display, 0.5f, 0);
                VLAD_Ops_Ai.VLAD_InferenceData_Get_Valid_Count(vladId, detectData);
            }
        }

        public static void VLAD_Ops_RTSP_Thread(object obj)
        {
            SetCurrentDirectoryAsDllDirectory();

            VLAD_Ops_RTSP_ThreadParam threadParam = obj as VLAD_Ops_RTSP_ThreadParam;
            if (threadParam == null)
            {
                throw new ArgumentException("VLAD_Ops_RTSP_ThreadParam 값이 필요합니다.", "obj");
            }

            VLAD_Ops_Ai.VLAD_Rtsp_Info_Client_Registration(
                threadParam.vlad_id,
                threadParam.rtsp_url,
                threadParam.user_name,
                threadParam.ui_type,
                threadParam.mon_idx,
                VLAD_Ops_RTSP_Frame_Proc);
        }

        public static void VLAD_Ops_RTSP_Monitor_Frame_Proc(IntPtr vladId, string userName, int uiType, int monitorIndex, IntPtr display)
        {
            // 모니터링 callback은 화면 표시 전용입니다. 현재 WPF UI 표시는 RtspVideoHost가 담당합니다.
        }

        public static void VLAD_Ops_RTSP_Monitor_Thread(object obj)
        {
            SetCurrentDirectoryAsDllDirectory();

            VLAD_Ops_RTSP_ThreadParam threadParam = obj as VLAD_Ops_RTSP_ThreadParam;
            if (threadParam == null)
            {
                throw new ArgumentException("VLAD_Ops_RTSP_ThreadParam 값이 필요합니다.", "obj");
            }

            string rtspUrl = threadParam.rtsp_url;
            if (rtspUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase) == false)
            {
                rtspUrl = "rtsp://" + rtspUrl + ":8554/vlad";
            }

            VLAD_Ops_Ai.VLAD_Rtsp_Info_Client_Monitoring_Registration(
                threadParam.vlad_id,
                rtspUrl,
                1920,
                1080,
                VLAD_Ops_RTSP_Monitor_Frame_Proc);
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
