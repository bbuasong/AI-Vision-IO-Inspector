using System;
using System.Diagnostics;
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
        private static int TestResultJsonEnabled;
        /// <summary>
        /// HD 요청/결과가 공유하는 고정 버퍼 크기입니다(계약값).
        ///
        /// <para>
        /// 이 크기가 측정부 개수를 제한합니다. 실측 기준입니다.
        ///   기본 JSON        183 byte
        ///   측정부 1개        78 byte
        ///   수용 가능 개수   약 102개 (103개부터 초과)
        /// </para>
        ///
        /// <para>
        /// 초과하면 AllocateFixedUtf8JsonBuffer에서 ArgumentException으로 막고
        /// 네이티브 함수를 호출하지 않습니다. 메모리 손상은 없지만 그 품목은 검사할 수 없습니다.
        /// 측정부가 100개를 넘는 품목을 다뤄야 하면 AI 담당자와 버퍼 크기를 다시 정해야 합니다.
        /// </para>
        /// </summary>
        public const int HdJsonBufferSize = 8192;
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
        /// 테스트 JSON 사용 여부입니다. true이면 실제 VLAD DLL 호출 대신 고정된 테스트 결과 JSON을
        /// 반환합니다.
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

        /// <summary>
        /// VLAD_Unregistration 호출 상태를 백그라운드 스레드와 주고받기 위한 캡슐입니다.
        /// </summary>
        private sealed class UnregistrationCallState
        {
            public UnregistrationCallState(IntPtr vladId)
            {
                VladId = vladId;
            }

            public IntPtr VladId { get; private set; }
            public bool Result;
            public AccessViolationException AccessViolation;
            public Exception Exception;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static void RunUnregistrationOnBackgroundThread(object state)
        {
            UnregistrationCallState callState = (UnregistrationCallState)state;
            try
            {
                callState.Result = VladNativeMethods.VLAD_Unregistration(callState.VladId);
            }
            catch (AccessViolationException ex)
            {
                callState.AccessViolation = ex;
            }
            catch (Exception ex)
            {
                callState.Exception = ex;
            }
        }

        /// <summary>
        /// VLAD_Unregistration은 드물게 응답 없이 멈출 수 있습니다. 별도 스레드에서 호출하고
        /// CFG\VladRuntimeSettings.json의 UnregistrationTimeoutMilliseconds만큼만 기다립니다.
        /// 이 호출이 시간 제한 없이 무한 대기하면 앱 종료(App.OnExit)가 끝나지 않아 프로세스가
        /// 화면 없이 남고, 다음 실행이 단일 실행 Mutex에 막히는 문제로 이어지므로 반드시 제한을 둡니다.
        /// </summary>
        public static bool VLAD_Unregistration(IntPtr vladId)
        {
            int timeoutMilliseconds = VladRuntimeSettings.Load().UnregistrationTimeoutMilliseconds;
            UnregistrationCallState callState = new UnregistrationCallState(vladId);
            Thread nativeThread = new Thread(RunUnregistrationOnBackgroundThread);
            nativeThread.IsBackground = true;
            nativeThread.Name = "VLAD_Unregistration";
            nativeThread.Start(callState);

            bool completedInTime = nativeThread.Join(timeoutMilliseconds);
            if (!completedInTime)
            {
                AppendRegistrationLog(
                    "UNREGISTRATION_TIMEOUT",
                    "VLAD_Unregistration이 " + (timeoutMilliseconds / 1000).ToString(CultureInfo.InvariantCulture) +
                    "초 안에 끝나지 않아 시간 초과로 처리합니다. VladId=" + FormatPointer(vladId));
                return false;
            }

            if (callState.AccessViolation != null)
            {
                AppendRegistrationLog(
                    "UNREGISTRATION_ACCESS_VIOLATION",
                    "VLAD_Unregistration 내부에서 보호된 메모리 예외가 발생했습니다. VladId=" + FormatPointer(vladId) +
                    ", Message=" + callState.AccessViolation.Message);
                return false;
            }

            if (callState.Exception != null)
            {
                AppendRegistrationLog("UNREGISTRATION_EXCEPTION", callState.Exception.ToString());
                ExceptionDispatchInfo.Capture(callState.Exception).Throw();
            }

            return callState.Result;
        }

        public static bool VLAD_Warm_Up(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Warm_Up(vladId);
        }

        public static long VLAD_Custom_ID_Generate(int userId, int msgVer, int majVer, int minVer)
        {
            return VladNativeMethods.VLAD_Custom_ID_Generate(userId, msgVer, majVer, minVer);
        }

        /// <summary>
        /// VLAD_Custom_Registration 호출 상태를 백그라운드 스레드와 주고받기 위한 캡슐입니다.
        /// </summary>
        private sealed class CustomRegistrationCallState
        {
            public CustomRegistrationCallState(long customId, string uiName, string rootName, string site, string modelPath, string customInfo, int gpuId)
            {
                CustomId = customId;
                UiName = uiName;
                RootName = rootName;
                Site = site;
                ModelPath = modelPath;
                CustomInfo = customInfo;
                GpuId = gpuId;
            }

            public long CustomId { get; private set; }
            public string UiName { get; private set; }
            public string RootName { get; private set; }
            public string Site { get; private set; }
            public string ModelPath { get; private set; }
            public string CustomInfo { get; private set; }
            public int GpuId { get; private set; }

            public IntPtr Result;
            public AccessViolationException AccessViolation;
            public Exception Exception;
        }

        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private static void RunCustomRegistrationOnBackgroundThread(object state)
        {
            CustomRegistrationCallState callState = (CustomRegistrationCallState)state;
            try
            {
                callState.Result = VladNativeMethods.VLAD_Custom_Registration(
                    callState.CustomId,
                    callState.UiName,
                    callState.RootName,
                    callState.Site,
                    callState.ModelPath,
                    callState.CustomInfo,
                    callState.GpuId);
            }
            catch (AccessViolationException ex)
            {
                callState.AccessViolation = ex;
            }
            catch (Exception ex)
            {
                callState.Exception = ex;
            }
        }

        /// <summary>
        /// VLAD_Custom_Registration은 TensorFlow/CUDA 전역 상태를 초기화하며 드물게 응답 없이
        /// 멈출 수 있습니다(주로 GPU/드라이버 상태 문제). 별도 스레드에서 호출하고 CFG의
        /// CustomRegistrationTimeoutMilliseconds만큼만 기다려, 시작 스레드가 무한 대기하며
        /// 창이 전혀 뜨지 않는 상태로 남지 않게 합니다. 이 상태로 강제 종료되면 VLAD_Unregistration이
        /// 실행되지 못해 다음 실행에서 같은 증상이 반복되므로, 여기서 시간 제한으로 끊어 프로세스가
        /// 예측 가능한 시간 안에 항상 종료(성공 또는 실패)될 수 있게 합니다.
        /// </summary>
        public static IntPtr VLAD_Custom_Registration(long customId, string uiName, string rootName, string site, string modelPath, string customInfo, int gpuId)
        {
            int timeoutMilliseconds = VladRuntimeSettings.Load().CustomRegistrationTimeoutMilliseconds;
            CustomRegistrationCallState callState = new CustomRegistrationCallState(customId, uiName, rootName, site, modelPath, customInfo, gpuId);
            Thread nativeThread = new Thread(RunCustomRegistrationOnBackgroundThread);
            nativeThread.IsBackground = true;
            nativeThread.Name = "VLAD_Custom_Registration";
            nativeThread.Start(callState);

            bool completedInTime = nativeThread.Join(timeoutMilliseconds);
            if (!completedInTime)
            {
                string timeoutMessage =
                    "VLAD_Custom_Registration이 " + (timeoutMilliseconds / 1000).ToString(CultureInfo.InvariantCulture) +
                    "초 안에 끝나지 않아 시간 초과로 처리합니다. CustomId=" + customId.ToString() +
                    ", UiName=" + SafeText(uiName) +
                    ", RootName=" + SafeText(rootName) +
                    ", Site=" + SafeText(site) +
                    ", ModelPath=" + SafeText(modelPath) +
                    ", GpuId=" + gpuId.ToString();
                AppendRegistrationLog("CUSTOM_REGISTRATION_TIMEOUT", timeoutMessage);
                throw new TimeoutException(timeoutMessage);
            }

            if (callState.AccessViolation != null)
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
                    ", Message=" + callState.AccessViolation.Message);
                return IntPtr.Zero;
            }

            if (callState.Exception != null)
            {
                AppendRegistrationLog("CUSTOM_REGISTRATION_EXCEPTION", callState.Exception.ToString());
                ExceptionDispatchInfo.Capture(callState.Exception).Throw();
            }

            return callState.Result;
        }

        public static int VLAD_Get_Class_Count(IntPtr vladId)
        {
            return VladNativeMethods.VLAD_Get_Class_Count(vladId);
        }

        /// <summary>
        /// HD 검사 진입점입니다. requestJson을 8192 byte 버퍼에 채워 넘기면, DLL이 같은 버퍼의
        /// viewJudge/score/dimensions/measurements만 갱신해 돌려줍니다(in-place 업데이트).
        /// 리턴값(void*)은 사용하지 않으므로 결과 JSON 문자열을 바로 반환합니다.
        /// (vlad-hd-api-v1.3-correction-2026-08-07.md, VLAD_HD_Inference_Mat수정-2026-08-07.md 참고)
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static string VLAD_HD_Inference_Mat(
            IntPtr fullImageVladId,
            IntPtr rawData,
            string requestJson)
        {
            if (fullImageVladId == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_HD_Inference_Mat 호출 실패: VladId가 비어 있습니다.", "fullImageVladId");
            }

            if (rawData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_HD_Inference_Mat 호출 실패: OpenCV Mat 포인터가 비어 있습니다.", "rawData");
            }

            if (IsTestResultJsonEnabled)
            {
                return TestHdInferenceResultJson;
            }

            IntPtr requestResultBuffer = IntPtr.Zero;
            try
            {
                AppendHdJsonLog("HD_INFERENCE_REQUEST", requestJson);
                requestResultBuffer = AllocateFixedUtf8JsonBuffer(requestJson, "VLAD_HD_Inference_Mat requestJson");
                lock (NativeInferenceLock)
                {
                    VladNativeMethods.VLAD_HD_Inference_Mat(fullImageVladId, rawData, requestResultBuffer);
                }

                AppendRegistrationLog("HD_INFERENCE_CALL", "VLAD_HD_Inference_Mat 호출 완료.");
                string resultJson = CopyUtf8BufferToString(requestResultBuffer, HdJsonBufferSize);
                AppendHdJsonLog("HD_INFERENCE_RESULT", resultJson);
                if (string.IsNullOrWhiteSpace(resultJson))
                {
                    throw new InvalidOperationException("VLAD_HD_Inference_Mat이 결과 JSON을 기록하지 않았습니다.");
                }

                return resultJson;
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new NotSupportedException("VLAD_SDK.dll에 VLAD_HD_Inference_Mat export가 없습니다.", ex);
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_HD_Inference_Mat 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 추론을 중지합니다.";
                BlockNativeInference(message);
                AppendRegistrationLog(
                    "HD_INFERENCE_ACCESS_VIOLATION",
                    message +
                    " FullImageVladId=" + FormatPointer(fullImageVladId) +
                    ", RawData=" + FormatPointer(rawData) +
                    ", Message=" + ex.Message);
                throw new InvalidOperationException(message, ex);
            }
            finally
            {
                FreeHGlobal(ref requestResultBuffer);
            }
        }

        /// <summary>
        /// 등록 기준이미지를 학습 DB와 비교하는 검색 진입점입니다. requestJson을 8192 byte 버퍼에
        /// 채워 넘기면, DLL이 같은 버퍼의 hasAlternatives/candidates만 갱신해 돌려줍니다
        /// (in-place 업데이트). 리턴값(void*)은 사용하지 않습니다.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static string VLAD_Search_Mat(
            IntPtr fullImageVladId,
            IntPtr rawData,
            string requestJson)
        {
            if (NativeInferenceBlocked)
            {
                throw new InvalidOperationException("이전 VLAD 네이티브 호출에서 보호 메모리 예외가 발생해 유사도 검색을 실행할 수 없습니다. 앱을 재시작한 뒤 다시 시도하십시오.");
            }

            if (fullImageVladId == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_Search_Mat 호출 실패: VladId가 비어 있습니다.", "fullImageVladId");
            }

            if (rawData == IntPtr.Zero)
            {
                throw new ArgumentException("VLAD_Search_Mat 호출 실패: OpenCV Mat 포인터가 비어 있습니다.", "rawData");
            }

            if (IsTestResultJsonEnabled)
            {
                return TestSearchResultJson;
            }

            IntPtr requestResultBuffer = IntPtr.Zero;
            try
            {
                AppendHdJsonLog("HD_SEARCH_REQUEST", requestJson);
                requestResultBuffer = AllocateFixedUtf8JsonBuffer(requestJson, "VLAD_Search_Mat requestJson");
                lock (NativeInferenceLock)
                {
                    VladNativeMethods.VLAD_Search_Mat(fullImageVladId, rawData, requestResultBuffer);
                }

                AppendRegistrationLog("HD_SEARCH_CALL", "VLAD_Search_Mat 호출 완료.");
                string resultJson = CopyUtf8BufferToString(requestResultBuffer, HdJsonBufferSize);
                AppendHdJsonLog("HD_SEARCH_RESULT", resultJson);
                if (string.IsNullOrWhiteSpace(resultJson))
                {
                    throw new InvalidOperationException("VLAD_Search_Mat이 결과 JSON을 기록하지 않았습니다.");
                }

                return resultJson;
            }
            catch (EntryPointNotFoundException ex)
            {
                throw new NotSupportedException("VLAD_SDK.dll에 VLAD_Search_Mat export가 없습니다.", ex);
            }
            catch (AccessViolationException ex)
            {
                string message = "VLAD_Search_Mat 보호 메모리 예외가 발생했습니다. 같은 프로세스에서 이후 VLAD 네이티브 호출을 중지합니다.";
                BlockNativeInference(message);
                AppendRegistrationLog(
                    "SEARCH_ACCESS_VIOLATION",
                    message +
                    " FullImageVladId=" + FormatPointer(fullImageVladId) +
                    ", RawData=" + FormatPointer(rawData) +
                    ", Message=" + ex.Message);
                throw new InvalidOperationException(message, ex);
            }
            finally
            {
                FreeHGlobal(ref requestResultBuffer);
            }
        }

        /// <summary>
        /// VLAD SDK가 정한 Top/Front/Back/Left/Right/Thickness 순서로 6장을 병합합니다.
        /// 네이티브 함수는 결과 상태를 반환하지 않으므로 호출자는 outputPath에서 keyId 이름의 파일 생성을 확인해야 합니다.
        ///
        /// <para>
        /// 병합 안에서 크롭을 하고 그러려면 SAM 준비가 필요해서 등록 핸들을 함께 넘깁니다.
        /// 핸들이 비어 있으면 SDK가 크롭 준비를 하지 못하므로 부르지 않고 막습니다.
        /// </para>
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public static void VLAD_HD_ImageMerge(IntPtr vladId, string inputPath, string keyId, string outputPath)
        {
            if (vladId == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "VLAD_HD_ImageMerge를 부르려면 등록 핸들이 필요합니다. AI 준비가 끝난 뒤에 호출해야 합니다.");
            }

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
                    VladNativeMethods.VLAD_HD_ImageMerge(vladId, inputPath, keyId, outputPath);
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

        // ☆★☆★☆★☆★ RTSP 모니터링 등록 시, 사용자 정의 콜백 함수를 통해 RTSP 스트림의 프레임을 실시간으로 처리 가능 ☆★☆★☆★☆★
        public static void VLAD_Rtsp_Info_Client_Registration(IntPtr vladId, string urlInfo, string userName, int uiType, int monitorIndex, VladNativeMethods.RTSP_Callback callback)
        {
            VladNativeMethods.VLAD_Rtsp_Info_Client_Registration(vladId, urlInfo, userName, uiType, monitorIndex, callback);
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
                                  ", Directory=" + SafeText(runtimeSettings.ResolvedVladSdkDirectoryPath) +
                                  ", CudaCachePath=" + SafeText(runtimeSettings.CudaCacheDirectoryPath) +
                                  ", ActiveCudaCachePath=" + SafeText(Environment.GetEnvironmentVariable("CUDA_CACHE_PATH")));

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
                // 이제 registerRtsp=false 로 오는 경로는 하나뿐입니다.
                // Crop 이미지용 VLAD ID 는 전체 이미지 ID 의 callback 을 재사용하므로 따로 등록하지 않습니다.
                AppendRegistrationLog(
                    "RTSP_REGISTRATION_SKIP",
                    "Crop 이미지용 VLAD ID는 전체 이미지 ID의 RTSP callback을 재사용하므로 RTSP 등록을 건너뜁니다.");
            }

            AppendRegistrationLog("ENV_START_RETURN", "VLAD_Ops_Ai_Env_Start 반환. VladId=" + FormatPointer(vladId));
            return vladId;
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

        /// <summary>
        /// VLAD HD 요청/결과 JSON 원문을 DB\Logs\vlad-hd-json.log에 그대로 남깁니다.
        /// 결과 파싱이 실패했을 때 어떤 JSON을 보냈고 DLL이 무엇을 돌려줬는지 형식 그대로 확인하고,
        /// 필요하면 AI 담당자에게 그대로 전달하기 위한 진단 로그입니다.
        /// 등록 추적 로그와 섞이면 JSON이 묻히므로 별도 파일로 분리합니다.
        /// </summary>
        /// <summary>
        /// HD 결과 JSON 로그에 한 줄 남깁니다. 결과 해석 중에 발견한 이상을 같은 파일에
        /// 적어 두면, 요청/응답과 나란히 보면서 원인을 찾을 수 있습니다.
        /// </summary>
        public static void WriteHdJsonNote(string stage, string message)
        {
            AppendHdJsonLog(stage, message);
        }

        private static void AppendHdJsonLog(string stage, string json)
        {
            try
            {
                string registrationLogPath = Environment.GetEnvironmentVariable(RegistrationLogEnvironmentVariableName);
                if (string.IsNullOrWhiteSpace(registrationLogPath))
                {
                    return;
                }

                string directoryPath = Path.GetDirectoryName(registrationLogPath);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    return;
                }

                Directory.CreateDirectory(directoryPath);
                string logFilePath = Path.Combine(
                    directoryPath,
                    ApplicationLogFileResolver.BuildLogFileName("vlad-hd-json"));
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                              " [" + stage + "] " +
                              "Length=" + (json == null ? 0 : json.Length).ToString(CultureInfo.InvariantCulture) +
                              ", Utf8Bytes=" + (json == null ? 0 : Encoding.UTF8.GetByteCount(json)).ToString(CultureInfo.InvariantCulture) +
                              Environment.NewLine +
                              (string.IsNullOrEmpty(json) ? "(비어 있음)" : json) +
                              Environment.NewLine;

                Debug.WriteLine(line);
                lock (RegistrationLogLock)
                {
                    File.AppendAllText(logFilePath, line, Encoding.UTF8);
                }
            }
            catch
            {
                // 진단 로그 실패가 검사 흐름을 막으면 안 됩니다.
            }
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
