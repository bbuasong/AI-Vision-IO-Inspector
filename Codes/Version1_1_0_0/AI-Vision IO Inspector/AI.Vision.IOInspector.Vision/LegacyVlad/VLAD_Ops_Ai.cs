using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using OpenCvSharp;
using AI.Vision.IOInspector.Infrastructure;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct Custom_Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
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
        // 0=아직 확인 전, 1=새 HD export 사용 가능, -1=현재 DLL에는 export 없음입니다.
        // DLL을 교체한 뒤에는 프로세스를 다시 시작해 export 확인을 처음부터 수행합니다.
        private static int HdInferenceApiAvailability;
        private static int HdSearchApiAvailability;
        private static int TestResultJsonEnabled;
        private static readonly IntPtr TestInferenceData = new IntPtr(1);
        private static readonly IntPtr TestSearchData = new IntPtr(2);
        public const int HdJsonBufferSize = 8192;
        private const float LegacyInferenceThreshold = 0.5f;
        private const string TestCustomInferenceDetectText = "true,98.50,150.00,60.00,290.00,10.00";
        private const string TestHdInferenceResultJson = "{\"partNo\":\"TEST-001\",\"viewName\":6,\"viewJudge\":0,\"score\":97.23,\"scoreThreshold\":95.00,\"dimensions\":{\"width\":100.00,\"depth\":30.00,\"height\":120.00},\"measurements\":[{\"indexNo\":1,\"measuredValue\":150.10},{\"indexNo\":2,\"measuredValue\":60.00}]}";
        private const string TestSearchResultJson = "{\"viewName\":1,\"scoreThreshold\":99.00,\"topK\":3,\"hasAlternatives\":true,\"candidates\":[{\"rank\":1,\"partNo\":\"TEST-001\",\"score\":99.52},{\"rank\":2,\"partNo\":\"TEST-002\",\"score\":98.91}]}";

        /// <summary>
        /// VLAD_SDK 추론 함수는 같은 VladId에 대한 재진입 안전성이 확인되지 않았습니다.
        /// RTSP callback과 검사 버튼 경로가 동시에 들어와도 네이티브 메모리가 겹치지 않도록 공유 lock을 사용합니다.
        /// </summary>
        public static object NativeInferenceSyncRoot
        {
            get { return NativeInferenceLock; }
        }

        /// <summary>
        /// 현재 프로세스에서 두 ID를 받는 VLAD_HD_Inference_Mat export가 실제 호출된 상태인지 반환합니다.
        /// false이면 기존 단일 ID VLAD_Inference_Mat + Draw/TLV 호환 경로를 사용합니다.
        /// </summary>
        public static bool IsHdInferenceApiActive
        {
            get { return Interlocked.CompareExchange(ref HdInferenceApiAvailability, 0, 0) == 1; }
        }

        /// <summary>
        /// 현재 프로세스에서 두 ID를 받는 VLAD_Search_Mat/VLAD_Search_ResultData export가 실제 호출된 상태인지 반환합니다.
        /// </summary>
        public static bool IsHdSearchApiActive
        {
            get { return Interlocked.CompareExchange(ref HdSearchApiAvailability, 0, 0) == 1; }
        }

        /// <summary>
        /// 테스트 JSON 사용 여부입니다. true이면 실제 VLAD DLL 결과 대신
        /// TEST_VLAD_HD_InferenceData_Result와 TEST_VLAD_Search_ResultData를 사용합니다.
        /// </summary>
        public static bool IsTestResultJsonEnabled
        {
            get { return Interlocked.CompareExchange(ref TestResultJsonEnabled, 0, 0) == 1; }
        }

        /// <summary>
        /// EXE의 CFG/VladRuntimeSettings.json 설정에 따라 결과 JSON 테스트 모드를 설정합니다.
        /// 테스트 모드는 프로세스 시작 시에만 적용하며, 운영 검사에서는 false여야 합니다.
        /// </summary>
        public static void SetTestResultJsonEnabled(bool enabled)
        {
            Interlocked.Exchange(ref TestResultJsonEnabled, enabled ? 1 : 0);
        }

        public static void BlockNativeInference(string message)
        {
            NativeInferenceBlocked = true;
            AppendRegistrationLog("INFERENCE_BLOCKED", message);
        }

        /// <summary>
        /// 새 모델로 VLAD 등록을 다시 완료한 뒤 이전 세션의 추론 차단 상태를 해제합니다.
        /// </summary>
        public static void ResetNativeInferenceBlock()
        {
            NativeInferenceBlocked = false;
            AppendRegistrationLog("INFERENCE_BLOCK_RESET", "VLAD 재초기화 완료 후 네이티브 추론 차단 상태를 해제했습니다.");
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

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
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
            catch (AccessViolationException ex)
            {
                AppendRegistrationLog(
                    "CUSTOM_REGISTRATION_ACCESS_VIOLATION",
                    "VLAD_Custom_Registration 내부에서 보호된 메모리 예외가 발생했습니다. " +
                    "현재 프로세스를 종료하지 않고 VladId=0으로 실패 처리합니다. " +
                    "CustomId=" + customId.ToString() +
                    ", UiName=" + SafeText(uiName) +
                    ", RootName=" + SafeText(rootName) +
                    ", Site=" + SafeText(site) +
                    ", ModelPath=" + SafeText(modelPath) +
                    ", CustomInfo=" + SafeText(customInfo) +
                    ", GpuId=" + gpuId.ToString() +
                    ", Message=" + ex.Message);
                return IntPtr.Zero;
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

        /// <summary>
        /// 전체 이미지와 Crop 이미지용 VLAD ID를 함께 받는 HD 검사 진입점입니다.
        /// 신규 DLL은 별도 threshold 인자 없이 requestJson의 scoreThreshold를 사용합니다.
        /// 신규 export가 없는 기존 DLL에서만 임시 단일 ID 호환 경로를 사용합니다.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static IntPtr VLAD_HD_Inference_Mat(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr rawData,
            int drawMode,
            string requestJson)
        {
            EnsureDualVladIds(fullImageVladId, croppedImageVladId, "VLAD_HD_Inference_Mat");

            if (rawData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_HD_Inference_Mat 호출 실패: OpenCV Mat 포인터가 비어 있습니다.", "rawData");
            }

            if (IsTestResultJsonEnabled)
            {
                // 실제 Mat 추론은 수행하지 않고, 결과 수신 이후 처리 전용 포인터를 반환합니다.
                return TestInferenceData;
            }

            IntPtr requestJsonUtf8 = IntPtr.Zero;
            try
            {
                requestJsonUtf8 = AllocateFixedUtf8JsonBuffer(requestJson, "VLAD_HD_Inference_Mat requestJson");

                // 새 DLL에는 두 VladId와 고정 8192 byte UTF-8 요청 버퍼만 전달합니다.
                if (IsNativeApiKnownUnavailable(ref HdInferenceApiAvailability) == false)
                {
                    try
                    {
                        IntPtr detectData;
                        lock (NativeInferenceLock)
                        {
                            detectData = VladNativeMethods.VLAD_HD_Inference_Mat(
                                fullImageVladId, croppedImageVladId, rawData, drawMode, requestJsonUtf8);
                        }

                        Interlocked.Exchange(ref HdInferenceApiAvailability, 1);
                        AppendRegistrationLog("HD_INFERENCE_CALL", "VLAD_HD_Inference_Mat 두 ID export 호출 완료.");
                        return detectData;
                    }
                    catch (EntryPointNotFoundException)
                    {
                        Interlocked.Exchange(ref HdInferenceApiAvailability, -1);
                        AppendRegistrationLog("HD_INFERENCE_FALLBACK", "현재 VLAD_SDK.dll에 VLAD_HD_Inference_Mat export가 없어 단일 ID 호환 경로를 사용합니다.");
                    }
                }

                // 새 DLL 적용 전의 단일 ID SDK만 위한 임시 호환 경로입니다.
                // 신규 업무 기준 Score는 requestJson의 scoreThreshold만 사용합니다.
                return VLAD_Inference_Mat(fullImageVladId, rawData, LegacyInferenceThreshold, drawMode, requestJson);
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_HD_Inference_Mat 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 추론을 중지합니다.";
                BlockNativeInference(message);
                throw new InvalidOperationException(message, ex);
            }
            finally
            {
                FreeHGlobal(ref requestJsonUtf8);
            }
        }

        /// <summary>
        /// 전체 이미지/Crop 이미지 ID를 사용해 고정 8192 byte UTF-8 결과 JSON을 읽습니다.
        /// C#이 버퍼를 할당·초기화하고, DLL 호출 직후 관리 문자열로 복사한 다음 버퍼를 해제합니다.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static string VLAD_HD_InferenceData_Result(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr detectData)
        {
            EnsureDualVladIds(fullImageVladId, croppedImageVladId, "VLAD_HD_InferenceData_Result");

            if (detectData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_HD_InferenceData_Result 호출에 필요한 detectData 포인터가 비어 있습니다.", "detectData");
            }

            if (IsTestResultJsonEnabled)
            {
                return TEST_VLAD_HD_InferenceData_Result(fullImageVladId, croppedImageVladId, detectData);
            }

            IntPtr resultJsonUtf8 = IntPtr.Zero;
            try
            {
                resultJsonUtf8 = AllocateEmptyBuffer(HdJsonBufferSize);
                lock (NativeInferenceLock)
                {
                    VladNativeMethods.VLAD_HD_InferenceData_Result(
                        fullImageVladId, croppedImageVladId, detectData, resultJsonUtf8);
                }

                string resultJson = CopyUtf8BufferToString(resultJsonUtf8, HdJsonBufferSize);
                if (string.IsNullOrWhiteSpace(resultJson))
                {
                    throw new InvalidOperationException("VLAD_HD_InferenceData_Result가 결과 JSON을 기록하지 않았습니다.");
                }

                return resultJson;
            }
            catch (EntryPointNotFoundException ex)
            {
                Interlocked.Exchange(ref HdInferenceApiAvailability, -1);
                throw new NotSupportedException("VLAD_SDK.dll에 VLAD_HD_InferenceData_Result export가 없습니다.", ex);
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_HD_InferenceData_Result 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 추론을 중지합니다.";
                BlockNativeInference(message);
                AppendRegistrationLog(
                    "HD_RESULT_ACCESS_VIOLATION",
                    message +
                    " FullImageVladId=" + FormatPointer(fullImageVladId) +
                    ", CroppedImageVladId=" + FormatPointer(croppedImageVladId) +
                    ", DetectData=" + FormatPointer(detectData) +
                    ", Message=" + ex.Message);
                throw new InvalidOperationException(message, ex);
            }
            finally
            {
                FreeHGlobal(ref resultJsonUtf8);
            }
        }

        /// <summary>
        /// 새 HD DLL의 VLAD_HD_InferenceData_Result 응답을 대신하는 임시 결과 함수입니다.
        /// 반환 JSON은 실제 API 계약과 같으며, 결과 JSON 파싱 이후의 측정값 비교와 이력 저장을 검증할 때만 사용합니다.
        /// </summary>
        public static string TEST_VLAD_HD_InferenceData_Result(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr detectData)
        {
            EnsureDualVladIds(fullImageVladId, croppedImageVladId, "TEST_VLAD_HD_InferenceData_Result");
            return TestHdInferenceResultJson;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static IntPtr VLAD_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode)
        {
            if (NativeInferenceBlocked)
            {
                AppendRegistrationLog("INFERENCE_SKIP", "이전 VLAD_Inference_Mat 보호 메모리 예외로 현재 프로세스의 VLAD 네이티브 추론을 건너뜁니다.");
                return IntPtr.Zero;
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
                return IntPtr.Zero;
            }
        }

        public static IntPtr VLAD_Inference_Mat(IntPtr vladId, IntPtr rawData, float threshold, int drawMode, string inspectionContextJson)
        {
            // 현재 VLAD_SDK.dll의 필수 export는 vlad_id, mat.CvPtr, threshold, drawMode 4개 인자입니다.
            // AI 담당자가 기준정보 인자를 DLL에 추가하면 이 지점에서 inspectionContextJson을 새 네이티브 인자로 전달합니다.
            if (!string.IsNullOrWhiteSpace(inspectionContextJson))
            {
                AppendRegistrationLog(
                    "INFERENCE_CONTEXT_READY",
                    "VLAD_Inference_Mat 기준정보 JSON 준비 완료. Length=" +
                    inspectionContextJson.Length.ToString(CultureInfo.InvariantCulture));
            }

            try
            {
                lock (NativeInferenceLock)
                {
                    //return VladNativeMethods.VLAD_Inference_Mat(vladId, rawData, threshold, drawMode, inspectionContextJson);
                    return VLAD_Inference_Mat(vladId, rawData, threshold, drawMode);
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

        /// <summary>
        /// 등록 기준이미지를 학습 DB에 전달하는 검색 Mat 호출입니다.
        /// requestJson에는 위치 코드, 기준 Score, topK와 빈 후보 배열을 전달합니다.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static IntPtr VLAD_Search_Mat(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr rawData,
            int drawMode,
            string requestJson)
        {
            if (NativeInferenceBlocked)
            {
                throw new InvalidOperationException("이전 VLAD 네이티브 호출에서 보호 메모리 예외가 발생해 유사도 검색을 실행할 수 없습니다. 앱을 재시작한 뒤 다시 시도하십시오.");
            }

            EnsureDualVladIds(fullImageVladId, croppedImageVladId, "VLAD_Search_Mat");

            if (rawData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_Search_Mat 호출 실패: OpenCV Mat 포인터가 비어 있습니다.", "rawData");
            }

            if (IsTestResultJsonEnabled)
            {
                // 실제 검색 추론은 수행하지 않고 테스트 결과용 포인터를 반환합니다.
                return TestSearchData;
            }

            IntPtr requestJsonUtf8 = IntPtr.Zero;
            try
            {
                requestJsonUtf8 = AllocateFixedUtf8JsonBuffer(requestJson, "VLAD_Search_Mat requestJson");
                lock (NativeInferenceLock)
                {
                    IntPtr searchData = VladNativeMethods.VLAD_Search_Mat(
                        fullImageVladId, croppedImageVladId, rawData, drawMode, requestJsonUtf8);
                    Interlocked.Exchange(ref HdSearchApiAvailability, 1);
                    AppendRegistrationLog("HD_SEARCH_CALL", "VLAD_Search_Mat 두 ID export 호출 완료.");
                    return searchData;
                }
            }
            catch (EntryPointNotFoundException ex)
            {
                Interlocked.Exchange(ref HdSearchApiAvailability, -1);
                throw new NotSupportedException("VLAD_SDK.dll에 새 두 ID VLAD_Search_Mat export가 없습니다.", ex);
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_Search_Mat 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 호출을 중지합니다.";
                BlockNativeInference(message);
                AppendRegistrationLog(
                    "SEARCH_ACCESS_VIOLATION",
                    message +
                    " FullImageVladId=" + FormatPointer(fullImageVladId) +
                    ", CroppedImageVladId=" + FormatPointer(croppedImageVladId) +
                    ", RawData=" + FormatPointer(rawData) +
                    ", DrawMode=" + drawMode.ToString(CultureInfo.InvariantCulture) +
                    ", Message=" + ex.Message);
                throw new InvalidOperationException(message, ex);
            }
            finally
            {
                FreeHGlobal(ref requestJsonUtf8);
            }
        }

        /// <summary>
        /// VLAD_Search_Mat 반환 포인터에서 고정 8192 byte 후보 목록 JSON을 읽습니다.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static string VLAD_Search_ResultData(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr searchData)
        {
            if (NativeInferenceBlocked)
            {
                throw new InvalidOperationException("이전 VLAD 네이티브 호출에서 보호 메모리 예외가 발생해 유사도 검색 결과를 읽을 수 없습니다. 앱을 재시작한 뒤 다시 시도하십시오.");
            }

            EnsureDualVladIds(fullImageVladId, croppedImageVladId, "VLAD_Search_ResultData");

            if (searchData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_Search_ResultData 호출에 필요한 searchData 포인터가 비어 있습니다.", "searchData");
            }

            if (IsTestResultJsonEnabled)
            {
                return TEST_VLAD_Search_ResultData(fullImageVladId, croppedImageVladId, searchData);
            }

            IntPtr resultJsonUtf8 = IntPtr.Zero;
            try
            {
                resultJsonUtf8 = AllocateEmptyBuffer(HdJsonBufferSize);
                lock (NativeInferenceLock)
                {
                    VladNativeMethods.VLAD_Search_ResultData(
                        fullImageVladId, croppedImageVladId, searchData, resultJsonUtf8);
                }

                string resultJson = CopyUtf8BufferToString(resultJsonUtf8, HdJsonBufferSize);
                if (string.IsNullOrWhiteSpace(resultJson))
                {
                    throw new InvalidOperationException("VLAD_Search_ResultData가 결과 JSON을 기록하지 않았습니다.");
                }

                return resultJson;
            }
            catch (EntryPointNotFoundException ex)
            {
                Interlocked.Exchange(ref HdSearchApiAvailability, -1);
                throw new NotSupportedException("VLAD_SDK.dll에 VLAD_Search_ResultData export가 없습니다.", ex);
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_Search_ResultData 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 호출을 중지합니다.";
                BlockNativeInference(message);
                AppendRegistrationLog(
                    "SEARCH_DATA_ACCESS_VIOLATION",
                    message +
                    " FullImageVladId=" + FormatPointer(fullImageVladId) +
                    ", CroppedImageVladId=" + FormatPointer(croppedImageVladId) +
                    ", SearchData=" + FormatPointer(searchData) +
                    ", Message=" + ex.Message);
                throw new InvalidOperationException(message, ex);
            }
            finally
            {
                FreeHGlobal(ref resultJsonUtf8);
            }
        }

        /// <summary>
        /// VLAD SDK가 정한 Top/Front/Back/Left/Right/Thickness 순서로 6장을 병합합니다.
        /// 네이티브 함수는 결과 상태를 반환하지 않으므로 호출자는 outputPath에서 keyId 이름의 파일 생성을 확인해야 합니다.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static void VLAD_HD_ImageMerge(string inputPath, string keyId, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath))
            {
                throw new ArgumentException("VLAD_HD_ImageMerge 입력 폴더가 비어 있습니다.", "inputPath");
            }

            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new ArgumentException("VLAD_HD_ImageMerge 품번 keyId가 비어 있습니다.", "keyId");
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("VLAD_HD_ImageMerge 출력 폴더가 비어 있습니다.", "outputPath");
            }

            try
            {
                lock (NativeInferenceLock)
                {
                    VladNativeMethods.VLAD_HD_ImageMerge(inputPath, keyId, outputPath);
                }
            }
            catch (EntryPointNotFoundException exception)
            {
                throw new NotSupportedException("VLAD_SDK.dll에 VLAD_HD_ImageMerge export가 없습니다.", exception);
            }
            catch (AccessViolationException exception)
            {
                string message = "VLAD_HD_ImageMerge 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 호출을 중지합니다.";
                BlockNativeInference(message);
                AppendRegistrationLog("IMAGE_MERGE_ACCESS_VIOLATION", message + " InputPath=" + inputPath + " KeyId=" + keyId + " OutputPath=" + outputPath);
                throw new InvalidOperationException(message, exception);
            }
        }

        public static string StartImageTraining(IntPtr fullImageVladId, IntPtr croppedImageVladId)
        {
            ProcessStartInfo startInfo = CreateImageTrainingStartInfo(fullImageVladId, croppedImageVladId);

            Process process = Process.Start(startInfo);
            string message = "이미지 학습 배치 파일을 실행했습니다. Path=" + startInfo.Arguments +
                             ", ProcessId=" + (process == null ? "-" : process.Id.ToString(CultureInfo.InvariantCulture)) +
                             ", FullImageVladId=" + FormatPointer(fullImageVladId) +
                             ", CroppedImageVladId=" + FormatPointer(croppedImageVladId);
            AppendRegistrationLog("START_IMAGE_TRAINING_REQUEST", message);
            return message;
        }

        /// <summary>
        /// CFG의 Study 경로를 사용하여 StartImageTraining 프로세스 실행 정보를 생성합니다.
        /// 호출자는 필요에 따라 StandardOutput/StandardError 리디렉션과 Exited 이벤트를 연결합니다.
        /// </summary>
        public static ProcessStartInfo CreateImageTrainingStartInfo(IntPtr fullImageVladId, IntPtr croppedImageVladId)
        {
            EnsureDualVladIds(fullImageVladId, croppedImageVladId, "CreateImageTrainingStartInfo");

            VladRuntimeSettings runtimeSettings = VladRuntimeSettings.Load();
            string studyDirectoryPath = runtimeSettings.ResolvedStudyDirectoryPath;
            string studyBatchFilePath = runtimeSettings.ResolvedStudyBatchFilePath;

            if (!File.Exists(studyBatchFilePath))
            {
                string missingMessage = "이미지 학습 배치 파일을 찾을 수 없습니다. Path=" + studyBatchFilePath +
                                        ", FullImageVladId=" + FormatPointer(fullImageVladId) +
                                        ", CroppedImageVladId=" + FormatPointer(croppedImageVladId);
                AppendRegistrationLog("START_IMAGE_TRAINING_MISSING_BAT", missingMessage);
                throw new FileNotFoundException(missingMessage, studyBatchFilePath);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            if (IsPortableExecutable(studyBatchFilePath))
            {
                // 현재 현장 ai_train.bat는 확장자와 달리 MZ 헤더를 가진 실행 파일이므로 직접 실행합니다.
                startInfo.FileName = studyBatchFilePath;
                startInfo.Arguments = string.Empty;
            }
            else
            {
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/c \"" + studyBatchFilePath + "\"";
            }

            startInfo.WorkingDirectory = studyDirectoryPath;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            return startInfo;
        }

        private static bool IsPortableExecutable(string filePath)
        {
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < 2)
                {
                    return false;
                }

                int first = stream.ReadByte();
                int second = stream.ReadByte();
                return first == 'M' && second == 'Z';
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

        /// <summary>
        /// 내부 파싱 및 UI 검증을 위해 detectText에 표준 검사 결과를 기록합니다.
        /// 실제 DLL 연동을 재개할 때는 VLAD_Custom_InferenceData_V1의 네이티브 호출로 되돌립니다.
        /// </summary>
        public static bool TEST_VLAD_Custom_InferenceData_V1(
            IntPtr vladId,
            IntPtr detectData,
            IntPtr rawData,
            IntPtr classCount,
            StringBuilder detectText,
            string customParameter,
            IntPtr tlvInfo,
            int tlvSize)
        {
            if (detectText == null)
            {
                return false;
            }

            detectText.Clear();
            detectText.Append(TestCustomInferenceDetectText);
            return true;
        }

        /// <summary>
        /// 내부 후보 목록 파서 및 유사도 UI 검증을 위해 결과 JSON을 반환합니다.
        /// </summary>
        public static string TEST_VLAD_Search_ResultData(
            IntPtr fullImageVladId,
            IntPtr croppedImageVladId,
            IntPtr searchData)
        {
            EnsureDualVladIds(fullImageVladId, croppedImageVladId, "TEST_VLAD_Search_ResultData");
            return TestSearchResultJson;
        }

        // 결과값
        public static unsafe bool VLAD_Custom_InferenceData_V1(IntPtr vladId, IntPtr detectData, IntPtr rawData, IntPtr classCount, StringBuilder detectText, string customParameter, IntPtr tlvInfo, int tlvSize)
        {
            return VladNativeMethods.VLAD_Custom_InferenceData_V1(vladId, detectData, rawData, classCount, detectText, customParameter, tlvInfo, tlvSize);
            //return TEST_VLAD_Custom_InferenceData_V1(vladId, detectData, rawData, classCount, detectText, customParameter, tlvInfo, tlvSize);
        }

        public static int VLAD_Get_Rect_IntersectionArea(Rectangle destination, Rectangle source)
        {
            return VladNativeMethods.VLAD_Get_Rect_IntersectionArea(destination, source);
        }

        public static int VLAD_Custom_InferenceData_V1_Draw(IntPtr vladId, IntPtr detectData, IntPtr rawData, IntPtr classCount, StringBuilder detectText, string customParameter)
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

        /// <summary>
        /// 기존 단일 VLAD 초기화 호출과의 호환을 위한 overload입니다.
        /// </summary>
        public static IntPtr VLAD_Ops_Ai_Env_Start(int user, string rootName, string siteName, int msgVer, int majVer, string modelPath, int gpuId)
        {
            return VLAD_Ops_Ai_Env_Start(user, rootName, siteName, msgVer, majVer, modelPath, gpuId, true);
        }

        /// <summary>
        /// VLAD_Custom_Registration으로 ID 하나를 생성합니다.
        /// 전체 이미지 ID만 RTSP callback을 등록하고, Crop 이미지 ID는 registerRtsp를 false로 호출합니다.
        /// </summary>
        public static IntPtr VLAD_Ops_Ai_Env_Start(int user, string rootName, string siteName, int msgVer, int majVer, string modelPath, int gpuId, bool registerRtsp)
        {
            AppendRegistrationLog(
                "ENV_START_ENTER",
                "VLAD_Ops_Ai_Env_Start 진입. User=" + user.ToString() +
                ", RootName=" + SafeText(rootName) +
                ", SiteName=" + SafeText(siteName) +
                ", MsgVer=" + msgVer.ToString() +
                ", MajVer=" + majVer.ToString() +
                ", ModelPath=" + SafeText(modelPath) +
                ", GpuId=" + gpuId.ToString() +
                ", RegisterRtsp=" + registerRtsp.ToString());

            string url_info = ResolveDefaultRtspUrl();
            string modelPathWithTrailingSlash = EnsureTrailingSlash(modelPath);
            VladRuntimeSettings runtimeSettings = VladRuntimeSettings.Load();
            runtimeSettings.ApplyVladSdkDllDirectory();
            AppendRegistrationLog("SET_VLAD_DLL_DIRECTORY", "VLAD SDK DLL 설정 확인. Settings=" +
                                  SafeText(runtimeSettings.SettingsFilePath) +
                                  ", DllPath=" + SafeText(runtimeSettings.ResolvedVladSdkDllPath) +
                                  ", Directory=" + SafeText(runtimeSettings.ResolvedVladSdkDirectoryPath));

            long customId = VLAD_Custom_ID_Generate((int)SDK_USER.USER_CUS_STD, (int)SDK_MSG.MSG_V1, (int)SDK_MAJ.MAJ_V1, gpuId);
            string parameter = "{\"MODEL\":0,\"CAM\":0}";

            AppendRegistrationLog("CUSTOM_ID", "VLAD_Custom_ID_Generate 반환. CustomId=" + customId.ToString() + ", MinVersion(GPU_ID)=" + gpuId.ToString());
            AppendRegistrationLog(
                "CUSTOM_REGISTRATION_CALL",
                "VLAD_Custom_Registration 호출. CustomId=" + customId.ToString() +
                ", Ui=CUSTOM, Root=null, Site=HD, ModelPath=" + SafeText(modelPathWithTrailingSlash) +
                ", GpuId=" + gpuId.ToString());

            IntPtr vladId = VLAD_Custom_Registration(customId, "CUSTOM", null, "HD", modelPathWithTrailingSlash, parameter, gpuId);
            AppendRegistrationLog("CUSTOM_REGISTRATION_RETURN", "VLAD_Custom_Registration 반환. VladId=" + FormatPointer(vladId));

            if (registerRtsp && vladId != IntPtr.Zero && !string.IsNullOrWhiteSpace(url_info))
            {
                const int rtspUiType = 7;
                const int monitorIndex = 0;
                const string rtspUserName = "HD";

                VLAD_Ops_RTSP.StoreCallbackParameterForClient(vladId, url_info, rtspUserName, rtspUiType, monitorIndex);
                VLAD_Rtsp_Info_Client_Registration(vladId, url_info, rtspUserName, rtspUiType, monitorIndex, VLAD_Ops_RTSP.RTSP_Frame_Proc);
                VLAD_Ops_RTSP.MarkClientRegistered(vladId, url_info, rtspUserName, rtspUiType, monitorIndex);
                AppendRegistrationLog("RTSP_REGISTRATION_RETURN", "VLAD_Rtsp_Info_Client_Registration 호출 완료. Url=" + SafeText(url_info));
            }
            else if (registerRtsp)
            {
                AppendRegistrationLog("RTSP_REGISTRATION_SKIP", "VladId가 0이거나 RTSP URL이 비어 있어 RTSP 등록을 건너뜁니다.");
            }
            else
            {
                AppendRegistrationLog("RTSP_REGISTRATION_SKIP", "Crop 이미지용 VLAD ID는 전체 이미지 ID의 RTSP callback을 재사용하므로 RTSP 등록을 건너뜁니다.");
            }

            AppendRegistrationLog("ENV_START_RETURN", "VLAD_Ops_Ai_Env_Start 반환. VladId=" + FormatPointer(vladId));
            return vladId;
        }

        /// <summary>
        /// native export가 없다고 한 번 확인된 경우에만 같은 프로세스에서 재시도를 생략합니다.
        /// 새 DLL 반영은 프로세스 재시작을 전제로 하므로, DLL 교체 후에는 다시 0(미확인) 상태로 시작합니다.
        /// </summary>
        private static bool IsNativeApiKnownUnavailable(ref int availability)
        {
            return Interlocked.CompareExchange(ref availability, 0, 0) == -1;
        }

        /// <summary>
        /// 요청 JSON을 0으로 초기화한 고정 8192 byte UTF-8 버퍼에 기록합니다.
        /// 널 종료 1 byte를 포함해 계약 크기를 넘으면 native 함수를 호출하지 않습니다.
        /// </summary>
        private static IntPtr AllocateFixedUtf8JsonBuffer(string value, string parameterName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length + 1 > HdJsonBufferSize)
            {
                throw new ArgumentException(
                    parameterName + "이 " + HdJsonBufferSize.ToString(CultureInfo.InvariantCulture) +
                    " byte 계약을 초과했습니다. UTF-8 Bytes=" + (bytes.Length + 1).ToString(CultureInfo.InvariantCulture),
                    parameterName);
            }

            IntPtr buffer = AllocateEmptyBuffer(HdJsonBufferSize);
            if (bytes.Length > 0)
            {
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
            }

            return buffer;
        }

        /// <summary>
        /// native DLL이 채울 널 종료 UTF-8 출력 buffer를 할당합니다.
        /// </summary>
        private static IntPtr AllocateEmptyBuffer(int bufferCapacity)
        {
            if (bufferCapacity < 1)
            {
                throw new ArgumentOutOfRangeException("bufferCapacity");
            }

            IntPtr buffer = Marshal.AllocHGlobal(bufferCapacity);
            byte[] empty = new byte[bufferCapacity];
            Marshal.Copy(empty, 0, buffer, bufferCapacity);
            return buffer;
        }

        /// <summary>
        /// native UTF-8 출력 buffer를 관리 문자열로 즉시 복사합니다.
        /// native buffer는 호출자 finally에서 해제되므로 이 메서드 밖으로 포인터를 보관하지 않습니다.
        /// </summary>
        private static string CopyUtf8BufferToString(IntPtr source, int sourceCapacity)
        {
            if (source == IntPtr.Zero || sourceCapacity < 1)
            {
                return string.Empty;
            }

            byte[] bytes = new byte[sourceCapacity];
            Marshal.Copy(source, bytes, 0, sourceCapacity);

            int length = 0;
            while (length < bytes.Length && bytes[length] != 0)
            {
                length++;
            }

            return length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes, 0, length);
        }

        /// <summary>
        /// AllocHGlobal로 할당한 포인터를 한 번만 해제하고 호출자 변수도 0으로 초기화합니다.
        /// </summary>
        private static void FreeHGlobal(ref IntPtr pointer)
        {
            if (pointer == IntPtr.Zero)
            {
                return;
            }

            Marshal.FreeHGlobal(pointer);
            pointer = IntPtr.Zero;
        }

        /// <summary>
        /// 전체 이미지와 Crop 이미지용 VLAD ID가 모두 준비되었는지 확인합니다.
        /// </summary>
        private static void EnsureDualVladIds(IntPtr fullImageVladId, IntPtr croppedImageVladId, string operationName)
        {
            if (fullImageVladId == IntPtr.Zero)
            {
                throw new ArgumentException(operationName + " 호출 실패: 전체 이미지용 VladId가 비어 있습니다.", "fullImageVladId");
            }

            if (croppedImageVladId == IntPtr.Zero)
            {
                throw new ArgumentException(operationName + " 호출 실패: Crop 이미지용 VladId가 비어 있습니다.", "croppedImageVladId");
            }
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
            return RuntimeConfigurationPathResolver.GetConfigFilePath("Config.json");
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
