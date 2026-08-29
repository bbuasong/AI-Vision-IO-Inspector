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

using AI.Vision.IOInspector.Vision.Services;

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
        /// <summary>
        /// 콜백 프레임을 담는 최소 간격입니다. 설정을 읽지 못했을 때 쓰는 값입니다.
        /// </summary>
        private const int DefaultFrameCacheMinimumIntervalMilliseconds = 200;

        private static int _frameCacheMinimumIntervalMilliseconds = DefaultFrameCacheMinimumIntervalMilliseconds;

        /// <summary>
        /// 프레임을 얼마나 자주 담을지 정합니다. 시작할 때 설정에서 읽어 한 번 넣어 둡니다.
        /// 콜백마다 설정 파일을 읽으면 그동안 다음 프레임이 밀립니다.
        /// </summary>
        public static void ApplyFrameCacheMinimumInterval(int intervalMilliseconds)
        {
            _frameCacheMinimumIntervalMilliseconds = intervalMilliseconds < 0 ? 0 : intervalMilliseconds;
        }

        // VLAD 공식 Sample_VLAD_SDK의 RTSP_Frame_Proc는 display 버퍼를
        // 1920x1080 BGR 3채널로 전달합니다. Config의 카메라 원본 해상도를
        // callback 버퍼 크기로 사용하면 고해상도 채널에서 버퍼 범위를 벗어납니다.
        /// <summary>
        /// 등록할 때 넘겨 두는 기준 해상도입니다.
        ///
        /// <para>
        /// 프레임 크기는 이제 callback이 넘겨주는 cv::Mat에서 직접 읽으므로 이 값으로 읽지 않습니다.
        /// 다만 어느 채널을 callback으로 받을지 판단하는 기준으로는 아직 씁니다
        /// (VisionCameraCoordinator.RequiresNativeResolutionCapture).
        /// 실제로 원본 해상도가 그대로 오는지 현장에서 확인한 뒤 그 판단도 걷어낼 수 있습니다.
        /// </para>
        /// </summary>
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

        private static bool FrameProcessingEnabled;
        private static IntPtr ActiveVladId;

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
            DateTime minimumCapturedAt,
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
                    minimumCapturedAt,
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
        /// <summary>
        /// 요청한 채널들의 최신 프레임을 채널별로 독립적으로 확보합니다.
        /// 특정 채널의 프레임이 아직 없거나 오래됐다는 이유로 나머지 채널까지 전부 실패 처리되지 않도록,
        /// 시간 안에 확보되지 못한 채널만 message에 보고하고 확보된 채널은 snapshots에 그대로 반환합니다.
        /// (최소 1개 채널이라도 확보되면 true를 반환합니다.)
        /// </summary>
        public static bool TryCloneLatestFrames(
            IList<int> monitorIndices,
            int maximumFrameAgeMilliseconds,
            DateTime minimumCapturedAt,
            int waitTimeoutMilliseconds,
            out IDictionary<int, VladRtspLatestFrame> snapshots,
            out string message)
        {
            DateTime waitStartedAt = DateTime.Now;
            snapshots = new Dictionary<int, VladRtspLatestFrame>();
            IDictionary<int, string> failureMessagesByMonitorIndex = new Dictionary<int, string>();

            while (true)
            {
                IList<int> missingMonitorIndices = new List<int>();
                foreach (int monitorIndex in monitorIndices)
                {
                    if (!snapshots.ContainsKey(monitorIndex))
                    {
                        missingMonitorIndices.Add(monitorIndex);
                    }
                }

                if (missingMonitorIndices.Count == 0)
                {
                    break;
                }

                TryCloneLatestFramesOnce(missingMonitorIndices, maximumFrameAgeMilliseconds, minimumCapturedAt, snapshots, failureMessagesByMonitorIndex);

                if ((DateTime.Now - waitStartedAt).TotalMilliseconds >= waitTimeoutMilliseconds)
                {
                    break;
                }

                if (snapshots.Count < monitorIndices.Count)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }

            StringBuilder messageBuilder = new StringBuilder();
            foreach (int monitorIndex in monitorIndices)
            {
                if (snapshots.ContainsKey(monitorIndex))
                {
                    continue;
                }

                string failureMessage;
                if (!failureMessagesByMonitorIndex.TryGetValue(monitorIndex, out failureMessage))
                {
                    failureMessage = "RTSP 최신 프레임 확보 시간 초과. MonitorIndex=" + monitorIndex.ToString();
                }

                if (messageBuilder.Length > 0)
                {
                    messageBuilder.Append(" / ");
                }

                messageBuilder.Append(failureMessage);
            }

            message = messageBuilder.Length > 0
                ? messageBuilder.ToString()
                : "RTSP 최신 프레임 일괄 복제 완료. Count=" + snapshots.Count.ToString();

            return snapshots.Count > 0;
        }

        /// <summary>
        /// callback이 넘겨준 프레임을 최신 캐시에 담습니다.
        ///
        /// <para>
        /// display는 cv::Mat*입니다. 예전에는 픽셀 시작 주소만 와서 크기를 알 수 없었고,
        /// Config 해상도를 짐작해 읽다가 실제 버퍼보다 크게 읽으면 멈추는 일이 있었습니다.
        /// 이제 Mat이 rows/cols/type을 함께 들고 오므로 짐작할 필요가 없습니다.
        /// </para>
        /// </summary>
        private static void CacheLatestFrame(VLAD_Ops_RTSP_ThreadParam threadParam, int monitorIndex, IntPtr display)
        {
            int frameWidth;
            int frameHeight;
            IntPtr pixelPointer;
            int frameByteLength;

            if (!TryReadFrameFromMat(display, out frameWidth, out frameHeight, out pixelPointer, out frameByteLength))
            {
                RtspFrameMetrics.RecordSkippedByUnknownSize(monitorIndex);
                return;
            }

            // 솎아내기 전에 셉니다. 실제로 초당 몇 장이 오는지 알아야
            // 화면을 이 프레임으로 그릴 수 있는지 판단할 수 있습니다.
            RtspFrameMetrics.RecordReceived(monitorIndex, frameWidth, frameHeight);

            string cameraName = threadParam == null ? string.Empty : threadParam.cam_name;
            VladRtspFrameCache frameCache;

            lock (CallbackStateSync)
            {
                if (!LatestFramesByMonitorIndex.TryGetValue(monitorIndex, out frameCache) ||
                    frameCache.Width != frameWidth ||
                    frameCache.Height != frameHeight)
                {
                    frameCache = new VladRtspFrameCache(monitorIndex, frameWidth, frameHeight);
                    LatestFramesByMonitorIndex[monitorIndex] = frameCache;
                }
            }

            try
            {
                DateTime now = DateTime.Now;
                lock (frameCache.SyncRoot)
                {
                    if (frameCache.CapturedAt != DateTime.MinValue &&
                        (now - frameCache.CapturedAt).TotalMilliseconds < _frameCacheMinimumIntervalMilliseconds)
                    {
                        RtspFrameMetrics.RecordSkippedByInterval(monitorIndex);
                        return;
                    }
                }

                // SDK 소유 display 포인터는 callback 반환 이후 유효하지 않을 수 있어 즉시 복사합니다.
                // 이 복사는 락 밖에서 합니다. 읽는 쪽이 "캐시에 있는 최신 사진을 가져다 쓰는" 단순한
                // 동작인데도 이 Marshal.Copy가 끝나기를 기다리다 멈추는 일이 없도록 하기 위함입니다.
                //
                // 담을 자리는 채널마다 미리 만들어 둔 버퍼를 돌려씁니다.
                // 프레임마다 새로 만들면 대형 객체 힙이 불어나 전체를 멈추는 수집이 잦아집니다.
                byte[] newBuffer = frameCache.AcquireWriteBuffer(frameByteLength);
                if (newBuffer == null)
                {
                    return;
                }

                Marshal.Copy(pixelPointer, newBuffer, 0, frameByteLength);

                lock (frameCache.SyncRoot)
                {
                    frameCache.PublishBuffer(
                        newBuffer,
                        string.IsNullOrWhiteSpace(cameraName) ? "CAM" + monitorIndex.ToString() : cameraName,
                        now);
                }

                RtspFrameMetrics.RecordPublished(monitorIndex);

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
                RtspFrameMetrics.RecordFailed(monitorIndex);
                Debug.WriteLine("VLAD RTSP 최신 프레임 캐시 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 그 카메라의 최신 프레임이 실제로 어떤 크기로 들어와 있는지 알려 줍니다.
        ///
        /// <para>
        /// 예전에는 callback이 픽셀 주소만 넘겨 크기를 알 수 없었고, 그래서 1920x1080으로
        /// 짐작해 두고 그보다 큰 채널은 아예 callback을 쓰지 않았습니다.
        /// 이제 callback이 cv::Mat을 넘겨 주므로 실제로 받은 크기를 보고 판단할 수 있습니다.
        /// </para>
        ///
        /// <para>
        /// 아직 한 장도 들어오지 않았으면 false입니다. 그때는 판단할 근거가 없으므로
        /// 부르는 쪽이 안전한 쪽(원본 캡처)을 골라야 합니다.
        /// </para>
        /// </summary>
        public static bool TryGetLatestFrameSize(int monitorIndex, out int frameWidth, out int frameHeight)
        {
            frameWidth = 0;
            frameHeight = 0;

            VladRtspFrameCache frameCache;
            lock (CallbackStateSync)
            {
                if (!LatestFramesByMonitorIndex.TryGetValue(monitorIndex, out frameCache) || frameCache == null)
                {
                    return false;
                }
            }

            bool lockTaken = false;
            try
            {
                System.Threading.Monitor.TryEnter(frameCache.SyncRoot, 0, ref lockTaken);
                if (!lockTaken || frameCache.CapturedAt == DateTime.MinValue)
                {
                    return false;
                }

                frameWidth = frameCache.Width;
                frameHeight = frameCache.Height;
            }
            finally
            {
                if (lockTaken)
                {
                    System.Threading.Monitor.Exit(frameCache.SyncRoot);
                }
            }

            return frameWidth > 0 && frameHeight > 0;
        }

        /// <summary>
        /// 지금 살아 있는 등록 핸들입니다. 크롭처럼 이 핸들이 필요한 곳에서 씁니다.
        /// 준비 전이거나 등록이 풀린 뒤에는 IntPtr.Zero입니다.
        /// </summary>
        public static IntPtr GetActiveVladId()
        {
            lock (CallbackStateSync)
            {
                return ActiveVladId;
            }
        }

        /// <summary>
        /// 화면에 그릴 최신 프레임을 내어 줍니다. 값을 복사하지 않고 버퍼 참조만 넘깁니다.
        ///
        /// <para>
        /// 검사용 <see cref="TryCloneLatestFramesOnce"/>와 달리 여기서는 복사를 하지 않습니다.
        /// 화면은 초당 수십 번 그리는데 그때마다 6MB를 한 번 더 복사하면 그만큼이 낭비입니다.
        /// 받은 쪽은 곧바로 화면 버퍼에 옮기고 참조를 놓아야 합니다.
        /// </para>
        ///
        /// <para>
        /// 쓰는 쪽이 버퍼 세 장을 돌려쓰므로, 30fps 기준 한 바퀴에 100ms가 걸립니다.
        /// 그 안에 옮기기만 하면 덮어쓰일 일이 없습니다.
        /// </para>
        ///
        /// <para>
        /// knownCapturedAt보다 새 프레임이 없으면 false를 돌려줍니다.
        /// 같은 그림을 다시 그리지 않게 하려는 것입니다.
        /// </para>
        /// </summary>
        public static bool TryCopyLatestFrameForDisplay(
            int monitorIndex,
            DateTime knownCapturedAt,
            ref byte[] reusableBuffer,
            out int frameWidth,
            out int frameHeight,
            out DateTime capturedAt)
        {
            frameWidth = 0;
            frameHeight = 0;
            capturedAt = DateTime.MinValue;

            VladRtspFrameCache frameCache;
            lock (CallbackStateSync)
            {
                if (!LatestFramesByMonitorIndex.TryGetValue(monitorIndex, out frameCache) || frameCache == null)
                {
                    return false;
                }
            }

            // 참조가 아니라 사본을 내어 줍니다.
            //
            // 예전에는 발행된 배열의 참조를 그대로 내어 주고 "쓰는 쪽이 다시 손대지 않으므로
            // 안전하다"고 적어 두었습니다. 배열을 매번 새로 만들던 시절 이야기입니다. 지금은
            // 버퍼 세 장을 돌려쓰므로 발행된 배열도 세 프레임 뒤에는 덮어써집니다. 읽는 쪽이
            // 복사를 마치기 전에 덮이면 위아래가 다른 프레임인 그림이 됩니다. 확률은 낮지만
            // GC 멈춤이 겹치면 일어날 수 있고, 일어나면 화면과 검사가 함께 속습니다.
            //
            // 그래서 발행 횟수를 함께 재어, 복사하는 동안 두 장 이상 발행되었으면 덮였을 수
            // 있다고 보고 다시 복사합니다. 초당 다섯 장 페이스에서는 사실상 한 번에 끝납니다.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                byte[] sourceReference;
                long publishCountBefore;

                bool lockTaken = false;
                try
                {
                    // 화면 갱신은 늦어도 다음 차례가 있으므로, 잠금을 기다리지 않고 그냥 넘깁니다.
                    System.Threading.Monitor.TryEnter(frameCache.SyncRoot, 0, ref lockTaken);
                    if (!lockTaken)
                    {
                        return false;
                    }

                    if (frameCache.CapturedAt == DateTime.MinValue ||
                        frameCache.CapturedAt <= knownCapturedAt ||
                        frameCache.CurrentBuffer == null ||
                        frameCache.CurrentBuffer.Length == 0)
                    {
                        return false;
                    }

                    sourceReference = frameCache.CurrentBuffer;
                    frameWidth = frameCache.Width;
                    frameHeight = frameCache.Height;
                    capturedAt = frameCache.CapturedAt;
                    publishCountBefore = frameCache.PublishCount;
                }
                finally
                {
                    if (lockTaken)
                    {
                        System.Threading.Monitor.Exit(frameCache.SyncRoot);
                    }
                }

                if (reusableBuffer == null || reusableBuffer.Length != sourceReference.Length)
                {
                    reusableBuffer = new byte[sourceReference.Length];
                }

                Buffer.BlockCopy(sourceReference, 0, reusableBuffer, 0, sourceReference.Length);

                // 복사하는 동안 링이 한 바퀴 가까이 돌았으면 읽던 자리가 덮였을 수 있습니다.
                if (frameCache.PublishCount - publishCountBefore < 2)
                {
                    return frameWidth > 0 && frameHeight > 0;
                }
            }

            return false;
        }

        /// <summary>
        /// monitorIndices에 대해 확보 가능한 채널만 snapshots에 채워 넣습니다.
        /// 채널 하나가 아직 없거나 오래됐다고 해서 나머지 채널까지 실패 처리하지 않고,
        /// 그 채널의 사유만 failureMessagesByMonitorIndex에 기록합니다.
        /// </summary>
        // 네이티브 RTSP 콜백 스레드 등 다른 스레드가 이 락들을 오래 붙잡고 있어도 캡처 버튼이
        // 무한정 멈추지 않도록, 무조건 대기하는 lock 대신 타임아웃이 있는 TryEnter를 사용합니다.
        private const int CallbackStateLockTimeoutMilliseconds = 500;
        private const int FrameCacheLockTimeoutMilliseconds = 500;

        private static void TryCloneLatestFramesOnce(
            IList<int> monitorIndices,
            int maximumFrameAgeMilliseconds,
            DateTime minimumCapturedAt,
            IDictionary<int, VladRtspLatestFrame> snapshots,
            IDictionary<int, string> failureMessagesByMonitorIndex)
        {
            Dictionary<int, VladRtspFrameCache> frameCaches = new Dictionary<int, VladRtspFrameCache>();

            bool callbackLockTaken = false;
            try
            {
                System.Threading.Monitor.TryEnter(CallbackStateSync, CallbackStateLockTimeoutMilliseconds, ref callbackLockTaken);
                if (!callbackLockTaken)
                {
                    foreach (int monitorIndex in monitorIndices)
                    {
                        failureMessagesByMonitorIndex[monitorIndex] = "RTSP 프레임 캐시가 다른 작업으로 잠시 사용 중이라 시간 안에 확인하지 못했습니다. MonitorIndex=" + monitorIndex.ToString();
                    }

                    return;
                }

                foreach (int monitorIndex in monitorIndices)
                {
                    VladRtspFrameCache frameCache;
                    if (!LatestFramesByMonitorIndex.TryGetValue(monitorIndex, out frameCache) || frameCache == null)
                    {
                        failureMessagesByMonitorIndex[monitorIndex] = "RTSP 최신 프레임이 아직 수신되지 않았습니다. VLAD_Rtsp_Info_Client_Registration 상태와 RTSP URL을 확인하십시오. MonitorIndex=" + monitorIndex.ToString();
                        continue;
                    }

                    frameCaches[monitorIndex] = frameCache;
                }
            }
            finally
            {
                if (callbackLockTaken)
                {
                    System.Threading.Monitor.Exit(CallbackStateSync);
                }
            }

            foreach (KeyValuePair<int, VladRtspFrameCache> item in frameCaches)
            {
                VladRtspFrameCache frameCache = item.Value;

                // 락 안에서는 참조/값만 짧게 꺼내고, 실제 나이 검사와 배열 복사는 락 밖에서 수행합니다.
                // 발행된 배열은 링을 돌려쓰므로 나중에 덮어써질 수 있습니다. 복사가 온전했는지는
                // 아래에서 발행 횟수로 가립니다.
                byte[] bufferSnapshot;
                DateTime capturedAtSnapshot;
                int widthSnapshot;
                int heightSnapshot;
                string cameraNameSnapshot;

                bool frameLockTaken = false;
                try
                {
                    System.Threading.Monitor.TryEnter(frameCache.SyncRoot, FrameCacheLockTimeoutMilliseconds, ref frameLockTaken);
                    if (!frameLockTaken)
                    {
                        failureMessagesByMonitorIndex[item.Key] = "RTSP 프레임 버퍼가 다른 작업으로 잠시 사용 중이라 시간 안에 확인하지 못했습니다. MonitorIndex=" + item.Key.ToString();
                        continue;
                    }

                    bufferSnapshot = frameCache.CurrentBuffer;
                    capturedAtSnapshot = frameCache.CapturedAt;
                    widthSnapshot = frameCache.Width;
                    heightSnapshot = frameCache.Height;
                    cameraNameSnapshot = frameCache.CameraName;
                }
                finally
                {
                    if (frameLockTaken)
                    {
                        System.Threading.Monitor.Exit(frameCache.SyncRoot);
                    }
                }

                if (capturedAtSnapshot == DateTime.MinValue || bufferSnapshot == null || bufferSnapshot.Length == 0)
                {
                    failureMessagesByMonitorIndex[item.Key] = "RTSP 최신 프레임 버퍼가 비어 있습니다. MonitorIndex=" + item.Key.ToString();
                    continue;
                }

                // 검사 버튼을 누른 뒤에 들어온 프레임만 씁니다.
                //
                // 누르기 전 그림을 저장하면 그 순간의 제품이 아닌 것을 검사하게 됩니다.
                // 5fps 면 200ms 마다 새 프레임이 오므로 대개 곧바로 걸리고, 잠깐 끊긴 때만
                // 기다리게 됩니다.
                if (minimumCapturedAt != DateTime.MinValue && capturedAtSnapshot < minimumCapturedAt)
                {
                    failureMessagesByMonitorIndex[item.Key] = "검사 시작 이후의 프레임이 아직 오지 않았습니다. MonitorIndex=" +
                        item.Key.ToString() +
                        ", 마지막 프레임=" +
                        capturedAtSnapshot.ToString("HH:mm:ss.fff") +
                        ", 기준=" +
                        minimumCapturedAt.ToString("HH:mm:ss.fff");
                    continue;
                }

                // 너무 오래된 프레임을 검사 이미지로 저장하면 실제 검사 시점과 화면이 달라질 수 있으므로 차단합니다.
                double frameAgeMilliseconds = (DateTime.Now - capturedAtSnapshot).TotalMilliseconds;
                if (maximumFrameAgeMilliseconds > 0 && frameAgeMilliseconds > maximumFrameAgeMilliseconds)
                {
                    failureMessagesByMonitorIndex[item.Key] = "RTSP 최신 프레임이 오래되었습니다. MonitorIndex=" +
                        item.Key.ToString() +
                        ", AgeMs=" +
                        ((int)frameAgeMilliseconds).ToString() +
                        ", Size=" +
                        widthSnapshot.ToString() +
                        "x" +
                        heightSnapshot.ToString();
                    continue;
                }

                // 발행 횟수를 재어 복사가 온전했는지 가립니다. 버퍼는 세 장을 돌려쓰므로
                // 복사하는 동안 두 장 이상 발행되면 읽던 자리가 덮였을 수 있습니다.
                // 검사에 들어가는 사진이므로 덮였을 가능성이 있으면 버리고 다시 복사합니다.
                byte[] clonedPixels = null;
                for (int attempt = 0; attempt < 3 && clonedPixels == null; attempt++)
                {
                    long publishCountBefore = frameCache.PublishCount;
                    byte[] candidate = new byte[bufferSnapshot.Length];
                    Buffer.BlockCopy(bufferSnapshot, 0, candidate, 0, candidate.Length);
                    if (frameCache.PublishCount - publishCountBefore < 2)
                    {
                        clonedPixels = candidate;
                        break;
                    }

                    // 덮였을 수 있으니 최신 발행본을 다시 집어 재시도합니다.
                    lock (frameCache.SyncRoot)
                    {
                        bufferSnapshot = frameCache.CurrentBuffer;
                        capturedAtSnapshot = frameCache.CapturedAt;
                    }
                }

                if (clonedPixels == null)
                {
                    failureMessagesByMonitorIndex[item.Key] = "RTSP 프레임 복사가 계속 덮어쓰기와 겹쳐 온전한 사진을 얻지 못했습니다. MonitorIndex=" + item.Key.ToString();
                    continue;
                }

                snapshots[item.Key] = new VladRtspLatestFrame(
                    frameCache.MonitorIndex,
                    cameraNameSnapshot,
                    widthSnapshot,
                    heightSnapshot,
                    clonedPixels,
                    capturedAtSnapshot);
                failureMessagesByMonitorIndex.Remove(item.Key);
            }
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
        /// callback이 넘긴 cv::Mat*에서 크기와 픽셀 주소를 꺼냅니다.
        ///
        /// <para>
        /// Mat 객체를 감싸되 우리가 해제하지 않도록 막습니다. 그 메모리는 SDK 것이라
        /// 우리가 지우면 SDK가 다음 프레임을 쓸 때 이미 없는 자리를 건드리게 됩니다.
        /// </para>
        ///
        /// <para>
        /// 이어 붙은 픽셀만 다룹니다. 줄 사이에 빈 자리가 있는 Mat은 한 번에 복사할 수 없어
        /// 건너뜁니다. 화면은 다음 프레임에 다시 그리면 됩니다.
        /// </para>
        /// </summary>
        private static bool TryReadFrameFromMat(
            IntPtr display, out int frameWidth, out int frameHeight, out IntPtr pixelPointer, out int frameByteLength)
        {
            frameWidth = 0;
            frameHeight = 0;
            pixelPointer = IntPtr.Zero;
            frameByteLength = 0;

            if (display == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                Mat frameMat = new Mat(display);
                try
                {
                    // SDK가 들고 있는 Mat이므로 우리 쪽에서 메모리를 놓지 않습니다.
                    frameMat.IsEnabledDispose = false;

                    int cols = frameMat.Cols;
                    int rows = frameMat.Rows;
                    IntPtr data = frameMat.Data;
                    if (cols <= 0 || rows <= 0 || data == IntPtr.Zero)
                    {
                        return false;
                    }

                    int channels = frameMat.Channels();
                    if (channels != 3)
                    {
                        // 화면과 크롭 모두 BGR 3채널을 전제로 합니다.
                        return false;
                    }

                    long step = frameMat.Step();
                    if (step != (long)cols * channels)
                    {
                        return false;
                    }

                    frameWidth = cols;
                    frameHeight = rows;
                    pixelPointer = data;
                    frameByteLength = checked(cols * rows * channels);
                    return true;
                }
                finally
                {
                    frameMat.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RTSP callback Mat 해석 실패: " + ex.Message);
                return false;
            }
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

    /// <summary>
    /// 카메라별 callback 수신 버퍼입니다.
    /// 쓰는 쪽은 새로 할당한 배열에 락 없이 복사한 뒤, 이 배열의 참조만 락 안에서 발행(Publish)합니다.
    /// 발행된 배열은 이후 다시 수정되지 않으므로, 읽는 쪽도 락 밖에서 안전하게 읽을 수 있습니다.
    /// 이 덕분에 읽기/쓰기 어느 쪽도 대용량 배열 복사 때문에 락을 오래 붙잡지 않습니다.
    /// </summary>
    internal sealed class VladRtspFrameCache
    {
        /// <summary>
        /// 돌려쓸 버퍼 장수입니다.
        ///
        /// <para>
        /// 쓰는 쪽이 한 장을 채우는 동안 읽는 쪽이 다른 장을 보고 있을 수 있어 여러 장을 둡니다.
        /// 30fps면 한 바퀴 도는 데 100ms가 걸리는데, 읽는 쪽은 복사만 하고 바로 놓으므로
        /// 3장이면 겹칠 일이 없습니다.
        /// </para>
        /// </summary>
        private const int BufferRingLength = 3;

        private readonly object _syncRoot;
        private readonly byte[][] _bufferRing;
        private byte[] _currentBuffer;
        private int _writeIndex;

        public VladRtspFrameCache(int monitorIndex, int width, int height)
        {
            _syncRoot = new object();
            _bufferRing = new byte[BufferRingLength][];
            _writeIndex = 0;
            MonitorIndex = monitorIndex;
            Width = width;
            Height = height;
            CameraName = "CAM" + monitorIndex.ToString();
            CapturedAt = DateTime.MinValue;
        }

        /// <summary>
        /// 이번 프레임을 담을 버퍼를 내어 줍니다. 처음 한 번만 만들고 이후에는 돌려씁니다.
        ///
        /// <para>
        /// 프레임마다 새로 만들면 6MB짜리 배열이 초당 180개 생겨 대형 객체 힙에 쌓입니다.
        /// 그러면 전체를 멈추는 수집이 자주 일어나 화면이 주기적으로 끊깁니다.
        /// 실측으로도 새로 만들 때가 돌려쓸 때보다 다섯 배 느렸습니다.
        /// </para>
        ///
        /// <para>
        /// 읽는 쪽(TryCloneLatestFramesOnce)이 값을 복사해 가므로, 발행한 버퍼를
        /// 나중에 다시 채워도 읽는 쪽이 들고 있는 자료가 바뀌지 않습니다.
        /// </para>
        /// </summary>
        public byte[] AcquireWriteBuffer(int byteLength)
        {
            if (byteLength <= 0)
            {
                return null;
            }

            int index = _writeIndex;
            _writeIndex = (index + 1) % BufferRingLength;

            byte[] buffer = _bufferRing[index];
            if (buffer == null || buffer.Length != byteLength)
            {
                buffer = new byte[byteLength];
                _bufferRing[index] = buffer;
            }

            return buffer;
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

        public DateTime CapturedAt { get; private set; }

        /// <summary>
        /// 이미 복사가 끝난(락 밖에서 채운) 배열의 참조만 원자적으로 교체합니다. 호출자가 SyncRoot를
        /// 짧게 잡고 호출하도록 설계되어 있어, 이 메서드 자체는 대용량 복사를 하지 않습니다.
        /// </summary>
        public void PublishBuffer(byte[] buffer, string cameraName, DateTime capturedAt)
        {
            _currentBuffer = buffer;
            CameraName = cameraName ?? CameraName;
            CapturedAt = capturedAt;
            _publishCount++;
        }

        /// <summary>
        /// 지금까지 발행한 프레임 수입니다. 읽는 쪽이 복사가 온전했는지 가리는 데 씁니다.
        ///
        /// <para>
        /// 버퍼는 세 장을 돌려쓰므로, 발행된 배열도 세 프레임 뒤에는 같은 자리에 덮어써집니다.
        /// 복사를 시작할 때와 끝냈을 때의 이 값 차이가 두 장 이상이면 읽던 자리가 덮였을 수
        /// 있으니 그 복사는 버려야 합니다.
        /// </para>
        /// </summary>
        public long PublishCount
        {
            get { return _publishCount; }
        }

        private long _publishCount;
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
