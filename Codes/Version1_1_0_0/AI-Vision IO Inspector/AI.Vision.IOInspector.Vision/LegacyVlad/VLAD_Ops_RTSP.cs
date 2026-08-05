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

        private const float DefaultThreshold = 0.5f;
        private const int DefaultRtspUiType = 7;
        private const int MaxValidCount = 1024;
        private const int LatestFrameCacheMinimumIntervalMilliseconds = 200;

        // VLAD 공식 Sample_VLAD_SDK의 RTSP_Frame_Proc는 display 버퍼를
        // 1920x1080 BGR 3채널로 전달합니다. Config의 카메라 원본 해상도를
        // callback 버퍼 크기로 사용하면 고해상도 채널에서 버퍼 범위를 벗어납니다.
        public const int CallbackFrameWidth = 1920;
        public const int CallbackFrameHeight = 1080;

        private static readonly object CallbackStateSync = new object();
        private static readonly Dictionary<string, VLAD_Ops_RTSP_ThreadParam> CallbackParameters =
            new Dictionary<string, VLAD_Ops_RTSP_ThreadParam>();
        private static readonly Dictionary<int, VLAD_Ops_RTSP_ThreadParam> CallbackParametersByMonitorIndex =
            new Dictionary<int, VLAD_Ops_RTSP_ThreadParam>();
        private static readonly HashSet<int> MissingFrameConfigurationMonitorIndices = new HashSet<int>();
        private static readonly Dictionary<int, VladRtspFrameCache> LatestFramesByMonitorIndex =
            new Dictionary<int, VladRtspFrameCache>();
        private static readonly HashSet<string> RegisteredClients = new HashSet<string>();
        private static readonly VladNativeMethods.RTSP_Callback FrameCallback = VLAD_Ops_RTSP_Frame_Proc;
        private static readonly VladNativeMethods.RTSP_Callback MonitorFrameCallback = VLAD_Ops_RTSP_Monitor_Frame_Proc;

        private static bool FrameProcessingEnabled;
        private static IntPtr ActiveVladId;
        private static VladRtspCustomInferenceSnapshot LastCustomInferenceSnapshot;

        public static VladNativeMethods.RTSP_Callback RTSP_Frame_Proc
        {
            get { return FrameCallback; }
        }

        public static VladRtspCustomInferenceSnapshot LastCustomInferenceData
        {
            get
            {
                lock (CallbackStateSync)
                {
                    return LastCustomInferenceSnapshot;
                }
            }
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
                : this(ptr, userName, uiType, monitorIndex, rtspUrl, cameraName, threshold, 0, 0)
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
                frame_width = frameWidth > 0 ? frameWidth : 0;
                frame_height = frameHeight > 0 ? frameHeight : 0;
            }
        }

        /// <summary>
        /// VLAD SDK가 RTSP 프레임을 받을 때 호출하는 콜백입니다.
        /// display 포인터는 SDK 내부 버퍼이므로 오래 보관하지 않고 즉시 byte[]로 복사해 최신 프레임 캐시에 저장합니다.
        /// 검사 버튼 경로에서는 이 캐시를 파일로 저장하고, 저장된 파일을 VLAD_Inference_Mat에 전달합니다.
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

            if (!IsActiveVladSession(vladId))
            {
                // 학습 후 Unregistration된 이전 세션의 지연 콜백은 새 프레임 캐시에 섞지 않습니다.
                return;
            }

            VLAD_Ops_RTSP_ThreadParam threadParam = ResolveCallbackParameter(vladId, userName, uiType, monitorIndex);
            try
            {
                // callback은 프레임 수신/캐시가 기본 역할입니다. 여기서는 추론을 수행하지 않습니다.
                CacheLatestFrame(threadParam, monitorIndex, display);
            }
            catch (AccessViolationException ex)
            {
                Debug.WriteLine("VLAD RTSP 최신 프레임 캐시 보호 메모리 예외: " + ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP 최신 프레임 캐시 예외: " + ex.Message);
                return;
            }

        }

        /// <summary>
        /// VLAD RTSP callback이 캐시한 최신 프레임을 지정된 파일 경로에 저장합니다.
        /// 검사 시작 시 RTSP를 다시 열지 않기 위해 CaptureVladRtsp에서 호출합니다.
        /// </summary>
        public static bool TrySaveLatestFrame(
            int monitorIndex,
            string outputFilePath,
            int maximumFrameAgeMilliseconds,
            int waitTimeoutMilliseconds,
            out DateTime capturedAt,
            out string message)
        {
            capturedAt = DateTime.MinValue;
            message = string.Empty;

            IDictionary<int, VladRtspLatestFrame> snapshots;
            if (!TryCloneLatestFrames(
                    new[] { monitorIndex },
                    maximumFrameAgeMilliseconds,
                    waitTimeoutMilliseconds,
                    out snapshots,
                    out message))
            {
                return false;
            }

            VladRtspLatestFrame snapshot = snapshots[monitorIndex];
            capturedAt = snapshot.CapturedAt;
            return TrySaveFrameSnapshot(snapshot, outputFilePath, out message);
        }

        /// <summary>
        /// 요청한 카메라의 최신 프레임을 한 번에 확보합니다.
        /// 전역 잠금 안에서는 카메라별 캐시 참조만 가져오고, 실제 byte[] 복제는
        /// 카메라별 짧은 잠금에서 수행하여 다른 채널의 RTSP callback을 막지 않습니다.
        /// </summary>
        public static bool TryCloneLatestFrames(
            IList<int> monitorIndices,
            int maximumFrameAgeMilliseconds,
            int waitTimeoutMilliseconds,
            out IDictionary<int, VladRtspLatestFrame> snapshots,
            out string message)
        {
            DateTime waitStartedAt = DateTime.Now;
            snapshots = new Dictionary<int, VladRtspLatestFrame>();
            message = string.Empty;

            while (true)
            {
                if (TryCloneLatestFramesOnce(monitorIndices, maximumFrameAgeMilliseconds, out snapshots, out message))
                {
                    return true;
                }

                if ((DateTime.Now - waitStartedAt).TotalMilliseconds >= waitTimeoutMilliseconds)
                {
                    return false;
                }

                System.Threading.Thread.Sleep(50);
            }
        }

        private static void CacheLatestFrame(VLAD_Ops_RTSP_ThreadParam threadParam, int monitorIndex, IntPtr display)
        {
            // threadParam의 해상도는 Config.json의 CAM_WIDTH/CAM_HEIGHT에서 넘어옵니다.
            // 실제 RTSP 프레임 해상도와 다르면 Marshal.Copy 범위가 맞지 않아 프레임 저장에 실패할 수 있습니다.
            int frameWidth;
            int frameHeight;
            if (!TryGetConfiguredFrameSize(threadParam, monitorIndex, out frameWidth, out frameHeight))
            {
                return;
            }

            string cameraName = threadParam == null ? string.Empty : threadParam.cam_name;
            int frameByteLength = checked(frameWidth * frameHeight * 3);
            VladRtspFrameCache frameCache;

            lock (CallbackStateSync)
            {
                if (!LatestFramesByMonitorIndex.TryGetValue(monitorIndex, out frameCache) ||
                    frameCache.Width != frameWidth ||
                    frameCache.Height != frameHeight)
                {
                    frameCache = new VladRtspFrameCache(monitorIndex, frameWidth, frameHeight, frameByteLength);
                    LatestFramesByMonitorIndex[monitorIndex] = frameCache;
                }
            }

            try
            {
                // SDK 소유 display 포인터는 callback 반환 이후 유효하지 않을 수 있습니다.
                // 카메라별 이중 버퍼의 비활성 배열에 복사한 뒤 현재 배열과 교체합니다.
                // 이 방식은 SDK 메모리 소유권을 분리하면서 프레임마다 대형 byte[]를 새로 만들지 않습니다.
                lock (frameCache.SyncRoot)
                {
                    DateTime capturedAt = DateTime.Now;
                    if (frameCache.CapturedAt != DateTime.MinValue &&
                        (capturedAt - frameCache.CapturedAt).TotalMilliseconds < LatestFrameCacheMinimumIntervalMilliseconds)
                    {
                        return;
                    }

                    Marshal.Copy(display, frameCache.WriteBuffer, 0, frameByteLength);
                    frameCache.SwapBuffers(
                        string.IsNullOrWhiteSpace(cameraName) ? "CAM" + monitorIndex.ToString() : cameraName,
                        capturedAt);
                }

                lock (CallbackStateSync)
                {
                    // Unregistration 중 복사된 이전 세션 프레임은 현재 캐시에서 제거합니다.
                    if (threadParam == null || ActiveVladId == IntPtr.Zero || ActiveVladId != threadParam.vlad_id)
                    {
                        VladRtspFrameCache currentCache;
                        if (LatestFramesByMonitorIndex.TryGetValue(monitorIndex, out currentCache) &&
                            object.ReferenceEquals(currentCache, frameCache))
                        {
                            LatestFramesByMonitorIndex.Remove(monitorIndex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("VLAD RTSP 최신 프레임 캐시 실패: " + ex.Message);
            }
        }

        private static bool TryCloneLatestFramesOnce(
            IList<int> monitorIndices,
            int maximumFrameAgeMilliseconds,
            out IDictionary<int, VladRtspLatestFrame> snapshots,
            out string message)
        {
            Dictionary<int, VladRtspFrameCache> frameCaches = new Dictionary<int, VladRtspFrameCache>();
            snapshots = new Dictionary<int, VladRtspLatestFrame>();

            if (monitorIndices == null || monitorIndices.Count == 0)
            {
                message = "RTSP 최신 프레임 요청 목록이 비어 있습니다.";
                return false;
            }

            lock (CallbackStateSync)
            {
                foreach (int monitorIndex in monitorIndices)
                {
                    VladRtspFrameCache frameCache;
                    if (!LatestFramesByMonitorIndex.TryGetValue(monitorIndex, out frameCache) || frameCache == null)
                    {
                        message = "RTSP 최신 프레임이 아직 수신되지 않았습니다. VLAD_Rtsp_Info_Client_Registration 상태와 RTSP URL을 확인하십시오. MonitorIndex=" + monitorIndex.ToString();
                        return false;
                    }

                    frameCaches[monitorIndex] = frameCache;
                }
            }

            Dictionary<int, VladRtspLatestFrame> clonedFrames = new Dictionary<int, VladRtspLatestFrame>();
            foreach (KeyValuePair<int, VladRtspFrameCache> item in frameCaches)
            {
                VladRtspFrameCache frameCache = item.Value;
                lock (frameCache.SyncRoot)
                {
                    if (frameCache.CapturedAt == DateTime.MinValue ||
                        frameCache.CurrentBuffer == null ||
                        frameCache.CurrentBuffer.Length == 0)
                    {
                        message = "RTSP 최신 프레임 버퍼가 비어 있습니다. MonitorIndex=" + item.Key.ToString();
                        return false;
                    }

                    // 너무 오래된 프레임을 검사 이미지로 저장하면 실제 검사 시점과 화면이 달라질 수 있으므로 차단합니다.
                    double frameAgeMilliseconds = (DateTime.Now - frameCache.CapturedAt).TotalMilliseconds;
                    if (maximumFrameAgeMilliseconds > 0 && frameAgeMilliseconds > maximumFrameAgeMilliseconds)
                    {
                        message = "RTSP 최신 프레임이 오래되었습니다. MonitorIndex=" +
                            item.Key.ToString() +
                            ", AgeMs=" +
                            ((int)frameAgeMilliseconds).ToString() +
                            ", Size=" +
                            frameCache.Width.ToString() +
                            "x" +
                            frameCache.Height.ToString();
                        return false;
                    }

                    byte[] clonedPixels = new byte[frameCache.CurrentBuffer.Length];
                    Buffer.BlockCopy(frameCache.CurrentBuffer, 0, clonedPixels, 0, clonedPixels.Length);
                    clonedFrames[item.Key] = new VladRtspLatestFrame(
                        frameCache.MonitorIndex,
                        frameCache.CameraName,
                        frameCache.Width,
                        frameCache.Height,
                        clonedPixels,
                        frameCache.CapturedAt);
                }
            }

            snapshots = clonedFrames;
            message = "RTSP 최신 프레임 일괄 복제 완료. Count=" + clonedFrames.Count.ToString();
            return true;
        }

        /// <summary>
        /// 이미 소유권이 분리된 프레임 스냅샷을 PNG로 저장합니다.
        /// 파일 I/O와 OpenCV 처리는 callback 잠금과 무관하게 실행됩니다.
        /// </summary>
        public static bool TrySaveFrameSnapshot(
            VladRtspLatestFrame frame,
            string outputFilePath,
            out string message)
        {
            if (frame == null)
            {
                message = "저장할 RTSP 프레임 스냅샷이 없습니다.";
                return false;
            }

            try
            {
                string directoryPath = Path.GetDirectoryName(outputFilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                int expectedByteLength = checked(frame.Width * frame.Height * 3);
                if (frame.BgrPixels == null || frame.BgrPixels.Length != expectedByteLength)
                {
                    message = "RTSP 최신 프레임 크기가 해상도와 일치하지 않습니다. MonitorIndex=" +
                        frame.MonitorIndex.ToString() +
                        ", Size=" +
                        frame.Width.ToString() +
                        "x" +
                        frame.Height.ToString() +
                        ", Bytes=" +
                        (frame.BgrPixels == null ? 0 : frame.BgrPixels.Length).ToString();
                    return false;
                }

                using (Mat mat = new Mat(frame.Height, frame.Width, MatType.CV_8UC3))
                {
                    // 캐시된 BGR byte[]를 OpenCV Mat로 복사한 뒤 PNG 파일로 저장합니다.
                    Marshal.Copy(frame.BgrPixels, 0, mat.Data, frame.BgrPixels.Length);
                    if (!Cv2.ImWrite(outputFilePath, mat))
                    {
                        message = "RTSP 최신 프레임 파일 저장에 실패했습니다. Path=" + outputFilePath;
                        return false;
                    }
                }

                message = "RTSP 최신 프레임 저장 완료. Camera=" +
                    frame.CameraName +
                    ", Resolution=" +
                    frame.Width.ToString() +
                    "x" +
                    frame.Height.ToString();
                return true;
            }
            catch (Exception ex)
            {
                message = "RTSP 최신 프레임 저장 실패: " +
                    ex.Message +
                    " CAM_WIDTH/CAM_HEIGHT가 실제 RTSP 해상도와 같은지 확인하십시오.";
                return false;
            }
        }

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

                // 네이티브 TLV 버퍼는 finally에서 해제되므로, 해제 전에 관리 메모리로 복사합니다.
                string detectText = detect_str.ToString();
                Custom_Info_Struct[] customInfos = ReadCustomInfoStructs(tlv_info, tlv_size, validCount);
                SaveCustomInferenceSnapshot(validCount, detectText, customInfos);
            }
            finally
            {
                Marshal.FreeHGlobal(tlv_info);
            }
        }

        private static Custom_Info_Struct[] ReadCustomInfoStructs(IntPtr tlvInfo, int tlvSize, int validCount)
        {
            Custom_Info_Struct[] customInfos = new Custom_Info_Struct[validCount];
            for (int index = 0; index < validCount; index++)
            {
                IntPtr itemPointer = IntPtr.Add(tlvInfo, tlvSize * index);
                customInfos[index] = (Custom_Info_Struct)Marshal.PtrToStructure(itemPointer, typeof(Custom_Info_Struct));
            }

            return customInfos;
        }

        private static void SaveCustomInferenceSnapshot(int validCount, string detectText, Custom_Info_Struct[] customInfos)
        {
            VladRtspCustomInferenceSnapshot snapshot = new VladRtspCustomInferenceSnapshot();
            snapshot.CapturedAt = DateTime.Now;
            snapshot.ValidCount = validCount;
            snapshot.DetectText = detectText ?? string.Empty;
            snapshot.CustomInfos = customInfos ?? new Custom_Info_Struct[0];

            lock (CallbackStateSync)
            {
                LastCustomInferenceSnapshot = snapshot;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.DetectText))
            {
                Debug.WriteLine("VLAD RTSP detectText: " + snapshot.DetectText);
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
            ActivateVladSession(vladId);

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

            if (!IsActiveVladSession(vladId))
            {
                return;
            }

            try
            {
                VLAD_Ops_RTSP_ThreadParam threadParam = ResolveCallbackParameter(vladId, userName, uiType, monitorIndex);
                // 모니터링 callback으로 들어오는 프레임도 검사 캡처 후보가 될 수 있으므로 동일한 캐시에 저장합니다.
                CacheLatestFrame(threadParam, monitorIndex, display);
                int frameWidth;
                int frameHeight;
                if (!TryGetConfiguredFrameSize(threadParam, monitorIndex, out frameWidth, out frameHeight))
                {
                    return;
                }

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
                ActivateVladSessionUnsafe(threadParam.vlad_id);
                VLAD_Ops_RTSP_ThreadParam existingThreadParam;
                if (CallbackParametersByMonitorIndex.TryGetValue(threadParam.mon_idx, out existingThreadParam) &&
                    HasValidFrameSize(existingThreadParam) &&
                    !HasValidFrameSize(threadParam))
                {
                    // Env_Start의 기본 RTSP 등록은 해상도 없이 먼저 들어올 수 있습니다.
                    // 이후 또는 이전에 Config 해상도로 등록한 값이 있다면 0x0 기본값으로 덮어쓰지 않습니다.
                    threadParam = existingThreadParam;
                }

                CallbackParameters[BuildCallbackKey(threadParam.vlad_id, threadParam.user_name, threadParam.ui_type, threadParam.mon_idx)] = threadParam;
                CallbackParameters[BuildCallbackKey(threadParam.vlad_id, null, threadParam.ui_type, threadParam.mon_idx)] = threadParam;

                // VLAD SDK의 실제 RTSP callback은 등록 호출에 사용한 "HD", uiType=7을 반환합니다.
                // Coordinator의 MODE_TYPE_CAM 값과 달라도 Config 해상도를 찾도록 실제 callback 키도 함께 갱신합니다.
                CallbackParameters[BuildCallbackKey(threadParam.vlad_id, "HD", DefaultRtspUiType, threadParam.mon_idx)] = threadParam;
                CallbackParameters[BuildCallbackKey(threadParam.vlad_id, null, DefaultRtspUiType, threadParam.mon_idx)] = threadParam;
                CallbackParametersByMonitorIndex[threadParam.mon_idx] = threadParam;
                MissingFrameConfigurationMonitorIndices.Remove(threadParam.mon_idx);
            }
        }

        private static bool HasValidFrameSize(VLAD_Ops_RTSP_ThreadParam threadParam)
        {
            return threadParam != null &&
                   threadParam.frame_width > 0 &&
                   threadParam.frame_height > 0;
        }

        /// <summary>
        /// VLAD_Unregistration 전에 이전 세션 콜백과 최신 프레임 캐시를 무효화합니다.
        /// </summary>
        public static void PrepareForVladRuntimeReload()
        {
            lock (CallbackStateSync)
            {
                ActiveVladId = IntPtr.Zero;
                CallbackParameters.Clear();
                CallbackParametersByMonitorIndex.Clear();
                MissingFrameConfigurationMonitorIndices.Clear();
                RegisteredClients.Clear();
                LatestFramesByMonitorIndex.Clear();
                LastCustomInferenceSnapshot = null;
                FrameProcessingEnabled = false;
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
                0,
                0);

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
                ActivateVladSessionUnsafe(vladId);
                RegisteredClients.Add(BuildRtspRegistrationKey(vladId, urlInfo, userName, uiType, monitorIndex));
            }
        }

        public static void StartFrameProcessing()
        {
            lock (CallbackStateSync)
            {
                FrameProcessingEnabled = true;
            }
        }

        public static void StopFrameProcessing(string reason)
        {
            lock (CallbackStateSync)
            {
                FrameProcessingEnabled = false;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                Debug.WriteLine("VLAD RTSP frame processing stopped. Reason=" + reason);
            }
        }

        private static bool IsFrameProcessingEnabled()
        {
            lock (CallbackStateSync)
            {
                return FrameProcessingEnabled;
            }
        }

        private static bool IsActiveVladSession(IntPtr vladId)
        {
            lock (CallbackStateSync)
            {
                return ActiveVladId != IntPtr.Zero && ActiveVladId == vladId;
            }
        }

        private static void ActivateVladSession(IntPtr vladId)
        {
            lock (CallbackStateSync)
            {
                ActivateVladSessionUnsafe(vladId);
            }
        }

        private static void ActivateVladSessionUnsafe(IntPtr vladId)
        {
            if (vladId == IntPtr.Zero || ActiveVladId == vladId)
            {
                return;
            }

            // VladId가 바뀌면 이전 세션의 등록 키와 프레임을 모두 제거한 뒤 새 세션만 허용합니다.
            ActiveVladId = vladId;
            CallbackParameters.Clear();
            CallbackParametersByMonitorIndex.Clear();
            MissingFrameConfigurationMonitorIndices.Clear();
            RegisteredClients.Clear();
            LatestFramesByMonitorIndex.Clear();
            LastCustomInferenceSnapshot = null;
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

                // 일부 VLAD SDK 버전은 callback의 userName/uiType을 등록 요청값과 다르게 전달합니다.
                // 같은 활성 VladId와 monitorIndex에 마지막으로 등록한 Config 값을 최종 기준으로 사용합니다.
                if (CallbackParametersByMonitorIndex.TryGetValue(monitorIndex, out threadParam) &&
                    threadParam != null &&
                    threadParam.vlad_id == vladId)
                {
                    return threadParam;
                }
            }

            return null;
        }

        /// <summary>
        /// callback 포인터에는 Width/Height 메타데이터가 없으므로 Config에서 등록한 해상도만 사용합니다.
        /// 설정을 찾지 못했을 때 1920x1080 같은 임의 기본값으로 Marshal.Copy를 수행하면
        /// 원본 버퍼 범위를 벗어날 수 있으므로 해당 프레임은 저장하지 않습니다.
        /// </summary>
        private static bool TryGetConfiguredFrameSize(
            VLAD_Ops_RTSP_ThreadParam threadParam,
            int monitorIndex,
            out int frameWidth,
            out int frameHeight)
        {
            frameWidth = threadParam == null ? 0 : threadParam.frame_width;
            frameHeight = threadParam == null ? 0 : threadParam.frame_height;
            if (frameWidth > 0 && frameHeight > 0)
            {
                return true;
            }

            lock (CallbackStateSync)
            {
                if (MissingFrameConfigurationMonitorIndices.Add(monitorIndex))
                {
                    Debug.WriteLine(
                        "VLAD RTSP 프레임 저장 보류: Config CAM_WIDTH/CAM_HEIGHT를 찾지 못했습니다. MonitorIndex=" +
                        monitorIndex.ToString());
                }
            }

            return false;
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
            VladRuntimeSettings runtimeSettings = VladRuntimeSettings.Load();
            runtimeSettings.ApplyVladSdkDllDirectory();
        }

    }

    public class VladRtspCustomInferenceSnapshot
    {
        public DateTime CapturedAt { get; set; }

        public int ValidCount { get; set; }

        public string DetectText { get; set; }

        public Custom_Info_Struct[] CustomInfos { get; set; }
    }

    /// <summary>
    /// 카메라별 callback 수신 버퍼입니다.
    /// SDK 포인터는 WriteBuffer로 복사되고 복사 완료 후 CurrentBuffer와 원자적으로 역할을 교체합니다.
    /// </summary>
    internal sealed class VladRtspFrameCache
    {
        private readonly object _syncRoot;
        private byte[] _currentBuffer;
        private byte[] _writeBuffer;

        public VladRtspFrameCache(int monitorIndex, int width, int height, int frameByteLength)
        {
            _syncRoot = new object();
            MonitorIndex = monitorIndex;
            Width = width;
            Height = height;
            _currentBuffer = new byte[frameByteLength];
            _writeBuffer = new byte[frameByteLength];
            CameraName = "CAM" + monitorIndex.ToString();
            CapturedAt = DateTime.MinValue;
        }

        public object SyncRoot
        {
            get { return _syncRoot; }
        }

        public int MonitorIndex { get; private set; }

        public string CameraName { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public byte[] CurrentBuffer
        {
            get { return _currentBuffer; }
        }

        public byte[] WriteBuffer
        {
            get { return _writeBuffer; }
        }

        public DateTime CapturedAt { get; private set; }

        public void SwapBuffers(string cameraName, DateTime capturedAt)
        {
            byte[] previousCurrentBuffer = _currentBuffer;
            _currentBuffer = _writeBuffer;
            _writeBuffer = previousCurrentBuffer;
            CameraName = cameraName ?? CameraName;
            CapturedAt = capturedAt;
        }
    }

    public class VladRtspLatestFrame
    {
        public VladRtspLatestFrame(
            int monitorIndex,
            string cameraName,
            int width,
            int height,
            byte[] bgrPixels,
            DateTime capturedAt)
        {
            MonitorIndex = monitorIndex;
            CameraName = cameraName ?? string.Empty;
            Width = width;
            Height = height;
            BgrPixels = bgrPixels;
            CapturedAt = capturedAt;
        }

        public int MonitorIndex { get; private set; }

        public string CameraName { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public byte[] BgrPixels { get; private set; }

        public DateTime CapturedAt { get; private set; }

        /// <summary>
        /// 파일 저장과 검사 단계가 callback 캐시 배열을 공유하지 않도록 독립 복사본을 만듭니다.
        /// </summary>
        public VladRtspLatestFrame Clone()
        {
            byte[] clonedPixels = null;
            if (BgrPixels != null)
            {
                clonedPixels = new byte[BgrPixels.Length];
                Buffer.BlockCopy(BgrPixels, 0, clonedPixels, 0, BgrPixels.Length);
            }

            return new VladRtspLatestFrame(
                MonitorIndex,
                CameraName,
                Width,
                Height,
                clonedPixels,
                CapturedAt);
        }
    }
}
