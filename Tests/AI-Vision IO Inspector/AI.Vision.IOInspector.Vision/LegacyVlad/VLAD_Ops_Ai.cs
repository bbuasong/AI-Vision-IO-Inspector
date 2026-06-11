using System;
using System.Drawing;
using System.Runtime.InteropServices;
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
        public static IntPtr VLAD_Registration(int user, int msg, int maj)
        {
            return VladNativeMethods.VLAD_Registration(user, msg, maj);
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

        public static long VLAD_Custom_ID_Generate(int userId, int msgVer, int majVer, int minVer)
        {
            return VladNativeMethods.VLAD_Custom_ID_Generate(userId, msgVer, majVer, minVer);
        }

        public static IntPtr VLAD_Custom_Registration(
            long customId,
            string uiName,
            string rootName,
            string site,
            string modelPath,
            string customInfo,
            int gpuId)
        {
            return VladNativeMethods.VLAD_Custom_Registration(customId, uiName, rootName, site, modelPath, customInfo, gpuId);
        }

        public static int VLAD_Get_Class_Count(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Get_Class_Count(vladId);
        }

        public static IntPtr VLAD_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode)
        {
            return VladNativeMethods.VLAD_Inference_Mat(vladId, rawData, threshold, drawMode);
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
            return VladNativeMethods.VLAD_InferenceData_V1_Draw(
                vladId,
                detectData,
                rawData,
                classCount,
                detectText,
                customParameter,
                tlvInfo,
                tlvSize);
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

        public static bool VLAD_Custom_InferenceData_V1(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize)
        {
            return VladNativeMethods.VLAD_Custom_InferenceData_V1(
                vladId,
                detectData,
                rawData,
                classCount,
                detectText,
                customParameter,
                tlvInfo,
                tlvSize);
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
            return VladNativeMethods.VLAD_Custom_InferenceData_V1_Draw(
                vladId,
                detectData,
                rawData,
                classCount,
                detectText,
                customParameter);
        }

        public static IntPtr VLAD_WONIK_Registration(string modelPath)
        {
            return VladNativeMethods.VLAD_WONIK_Registration(modelPath);
        }

        public static IntPtr VLAD_WONIK_Inference_Mat(
            IntPtr vladId,
            IntPtr rawData,
            float threshold,
            int drawMode,
            string valveType)
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

        public static IntPtr VLAD_Corning_BKG_Monitor_Display(
            IntPtr vladId,
            IntPtr display,
            IntPtr mainImage,
            IntPtr bottomLeft,
            IntPtr bottomCenter,
            IntPtr bottomRight)
        {
            return VladNativeMethods.VLAD_Corning_BKG_Monitor_Display(
                vladId,
                display,
                mainImage,
                bottomLeft,
                bottomCenter,
                bottomRight);
        }

        public static IntPtr VLAD_Corning_BKG_Monitor(IntPtr vladId, int index, IntPtr display)
        {
            return VladNativeMethods.VLAD_Corning_BKG_Monitor(vladId, index, display);
        }

        public static IntPtr VLAD_MPS_Registration_V2(
            string executeType,
            string modelPath,
            int kindCamera,
            int viewMode,
            int gpuId)
        {
            return VladNativeMethods.VLAD_MPS_Registration_V2(executeType, modelPath, kindCamera, viewMode, gpuId);
        }

        public static IntPtr VLAD_OPS_MPS_Registration_V2(
            string uiName,
            string executeType,
            string modelPath,
            int kindCamera,
            int viewMode,
            int gpuId)
        {
            return VladNativeMethods.VLAD_OPS_MPS_Registration_V2(uiName, executeType, modelPath, kindCamera, viewMode, gpuId);
        }

        public static IntPtr VLAD_MPS_Inference_Mat(
            IntPtr vladId,
            IntPtr rawData,
            float threshold,
            int drawMode,
            int viewLocation,
            int limitOverflow,
            int limitProtrusion)
        {
            return VladNativeMethods.VLAD_MPS_Inference_Mat(
                vladId,
                rawData,
                threshold,
                drawMode,
                viewLocation,
                limitOverflow,
                limitProtrusion);
        }

        public static void VLAD_Rtsp_Info_Monitoring_Registration(IntPtr vladId, int portNo)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Monitoring_Registration(vladId, portNo);
        }

        // ☆★☆★☆★☆★ RTSP 모니터링 등록 시, 사용자 정의 콜백 함수를 통해 RTSP 스트림의 프레임을 실시간으로 처리 가능 ☆★☆★☆★☆★
        public static void VLAD_Rtsp_Info_Client_Registration(
            IntPtr vladId,
            string urlInfo,
            string userName,
            int uiType,
            int monitorIndex,
            VladNativeMethods.RTSP_Callback callback)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Client_Registration(vladId, urlInfo, userName, uiType, monitorIndex, callback);
        }

        public static void VLAD_Rtsp_Info_Client_Monitoring_Registration(
            IntPtr vladId,
            string urlInfo,
            int width,
            int height,
            VladNativeMethods.RTSP_Callback callback)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Client_Monitoring_Registration(vladId, urlInfo, width, height, callback);
        }

        public static void VLAD_Rtsp_Info_Monitoring_SetFrame(IntPtr vladId, IntPtr rawData)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Monitoring_SetFrame(vladId, rawData);
        }

        public static IntPtr VLAD_Ops_Ai_Env_Start(int user, string rootName, string siteName, int msgVer, int majVer, string modelPath, int gpuId)
        {
            if (user == (int)SDK_USER.USER_CUS_STD)
            {
                long customId = VLAD_Custom_ID_Generate(user, msgVer, majVer, 0);
                if (siteName == "WONIK")
                {
                    return VLAD_WONIK_Registration(modelPath);
                }

                if (siteName == "CORNING")
                {
                    return VLAD_Corning_Registration("VLAD_OPS", rootName, modelPath, gpuId);
                }

                if (siteName == "BOD")
                {
                    return VLAD_Corning_BOD_Registration("VLAD_OPS", rootName, modelPath, gpuId);
                }

                if (siteName == "MPS")
                {
                    // 기존 VLAD_Ops는 전역 Config.json에서 MPS 카메라/뷰 값을 다시 계산했습니다.
                    // 현재 프로젝트에는 해당 전역 객체가 없으므로 기본값 0을 사용하고, 필요 시 옵션 UI 설정으로 확장합니다.
                    return VLAD_OPS_MPS_Registration_V2("VLAD_OPS", rootName, modelPath, 0, 0, gpuId);
                }

                // HD업체의 경우, 모델 종류(MODEL)와 카메라 종류(CAM)를 JSON 형태의
                string parameter = "{\"MODEL\":0,\"CAM\":0}";
                return VLAD_Custom_Registration(customId, "CUSTOM", rootName, siteName, modelPath, parameter, gpuId);
            }

            if (modelPath != null)
            {
                IntPtr vladId = VLAD_Registration(user, msgVer, majVer);
                if (vladId != IntPtr.Zero)
                {
                    string parameter = "{}";
                    VLAD_Ops_Inference_Registration(vladId, rootName, siteName, EnsureTrailingSlash(modelPath), parameter, gpuId);
                }

                return vladId;
            }

            if (rootName == "MONITOR")
            {
                return VLAD_Registration(user, msgVer, majVer);
            }

            return IntPtr.Zero;
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
                int messageVersion = VLAD_Get_Msg_Ver(vladId);
                if (messageVersion == (int)SDK_MSG.MSG_V2)
                {
                    VLAD_InferenceData_V2_Draw(
                        vladId,
                        detectData,
                        outputImage.CvPtr,
                        classListHandle.AddrOfPinnedObject(),
                        detectText);
                }
                else
                {
                    VLAD_InferenceData_V1_Draw(
                        vladId,
                        detectData,
                        outputImage.CvPtr,
                        classListHandle.AddrOfPinnedObject(),
                        detectText,
                        customParameter ?? string.Empty,
                        tlvInfo,
                        tlvSize);
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
    }
}
