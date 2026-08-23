using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using AI.Vision.IOInspector.Vision.Models;
using OpenCvSharp;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// 화면에 그리기 전에 프레임에서 제품 영역만 잘라 내는 단계입니다.
    ///
    /// <para>
    /// 그리는 코드와 떼어 놓았습니다. 크롭을 매 프레임 부를 수 있을지는 실제로 재봐야 알 수 있는데,
    /// 그리는 코드 안에 넣어 두면 나중에 빈도를 바꿀 때 그리기까지 손대야 합니다.
    /// </para>
    ///
    /// <para>
    /// 실패하면 잘라내지 않은 원본을 그대로 씁니다. 담당자 안내대로 크롭이 실패한 프레임은
    /// 화면을 비우지 않고 원본으로 보여 줍니다.
    /// </para>
    /// </summary>
    public sealed class CallbackFrameCropStage : IDisposable
    {
        /// <summary>
        /// 잘라 낸 자리를 받을 버퍼 크기입니다.
        /// 값이 다섯 개뿐이라 작아도 되지만, 검사 JSON과 같은 크기로 맞춰 둡니다.
        /// </summary>
        private const int CropJsonBufferSize = 8192;

        /// <summary>
        /// 자를 차례를 기다리는 줄입니다.
        ///
        /// <para>
        /// 카메라 여섯이 GPU 를 함께 쓰므로 한 번에 하나씩만 자릅니다. 자물쇠로 막으면
        /// 차례가 지켜지지 않아 몇 대가 독차지하므로, 들어온 순서를 그대로 지키는 줄을 씁니다.
        /// </para>
        /// </summary>
        private static readonly Queue<CropWorkItem> CropQueue = new Queue<CropWorkItem>();

        private static readonly object CropQueueSync = new object();

        /// <summary>줄을 처리하는 일꾼이 돌고 있는지입니다. 하나만 돕니다.</summary>
        private static bool _cropWorkerRunning;

        /// <summary>
        /// 카메라마다 가장 최근에 알아낸 자를 자리입니다.
        ///
        /// <para>
        /// 저장하는 파일은 원본이지만 화면에는 잘라서 보여 줍니다. 화면 쪽에서 그 자리를 알아야
        /// 저장한 원본을 같은 자리로 잘라 보여 줄 수 있어, 여기에 채널별로 놓아 둡니다.
        /// </para>
        /// </summary>
        private static readonly Dictionary<int, CropRegion> LatestRegionsByMonitorIndex =
            new Dictionary<int, CropRegion>();

        private static readonly object LatestRegionSync = new object();

        /// <summary>
        /// 자를 자리를 새로 찾지 않고 지금 값에 묶어 둘지입니다.
        ///
        /// <para>
        /// 검사가 도는 동안에는 화면이 흔들리면 안 됩니다. SAM 은 프레임마다 조금씩 다른
        /// 곳을 잡을 수 있어, 그대로 두면 검사 중 화면이 계속 들썩입니다.
        /// 검사에 쓰는 사진과 화면이 같은 자리를 보도록 잠가 둡니다.
        /// </para>
        /// </summary>
        public static bool IsRegionLocked
        {
            get { return _isRegionLocked; }
            set
            {
                _isRegionLocked = value;

                if (value)
                {
                    // 묶는 순간 줄에 남아 있던 일감은 버립니다.
                    //
                    // 새로 넣지 않는 것만으로는 모자랍니다. 검사 버튼을 누를 때 이미 줄에
                    // 여섯 장이 서 있으면 그것들이 차례로 SAM 을 돌려 화면이 계속 들썩이고,
                    // 검사가 쓰려는 GPU 도 그만큼 늦게 비었습니다.
                    ClearPendingCropQueue();
                }
            }
        }

        private static bool _isRegionLocked;

        /// <summary>
        /// 줄 서 있는 크롭 일감을 모두 버리고, 버린 몫의 표시를 되돌립니다.
        /// </summary>
        private static void ClearPendingCropQueue()
        {
            List<CropWorkItem> discarded = new List<CropWorkItem>();
            lock (CropQueueSync)
            {
                while (CropQueue.Count > 0)
                {
                    discarded.Add(CropQueue.Dequeue());
                }
            }

            // 버린 일감의 "자르는 중" 표시를 풀지 않으면 그 카메라는 다시는 자르지 않습니다.
            foreach (CropWorkItem item in discarded)
            {
                if (item != null && item.Stage != null)
                {
                    lock (item.Stage._syncRoot)
                    {
                        item.Stage._cropRunning = false;
                    }
                }
            }
        }

        /// <summary>
        /// 그 카메라의 마지막 크롭 자리입니다. 아직 한 번도 자르지 못했으면 null 입니다.
        /// </summary>
        public static CropRegion GetLatestRegion(int monitorIndex)
        {
            lock (LatestRegionSync)
            {
                CropRegion region;
                return LatestRegionsByMonitorIndex.TryGetValue(monitorIndex, out region) ? region : null;
            }
        }

        private static void PublishLatestRegion(int monitorIndex, CropRegion region)
        {
            lock (LatestRegionSync)
            {
                LatestRegionsByMonitorIndex[monitorIndex] = region;
            }
        }

        private readonly object _syncRoot = new object();
        private readonly int _monitorIndex;

                private CropRegion _croppedRegion;

        /// <summary>지금 뒤에서 자르는 중인지입니다. 겹쳐 부르지 않으려고 둡니다.</summary>
        private bool _cropRunning;

        /// <summary>자리로 잘라 낸 그림을 담는 배열입니다. 화면 스레드에서만 씁니다.</summary>
        private byte[] _regionBuffer;

        private DateTime _lastAttemptedAt;
        private long _totalElapsedTicks;
        private long _attemptCount;
        private long _successCount;
        private bool _disposed;

        public CallbackFrameCropStage(int monitorIndex)
        {
            _monitorIndex = monitorIndex;
            _lastAttemptedAt = DateTime.MinValue;
        }

        /// <summary>
        /// 크롭을 시도할 최소 간격입니다. 0이면 프레임마다 시도합니다.
        ///
        /// <para>
        /// 고정된 자리에 놓인 제품을 보므로 잘라 낼 자리가 자주 바뀌지 않습니다.
        /// 매 프레임 부르지 않아도 화면은 같아 보이는데, 부담은 크게 줄어듭니다.
        /// </para>
        /// </summary>
        public int MinimumIntervalMilliseconds { get; set; }

        /// <summary>크롭 1회에 걸린 평균 시간입니다. 실측용입니다.</summary>
        public double AverageElapsedMilliseconds
        {
            get
            {
                long count = _attemptCount;
                if (count <= 0)
                {
                    return 0;
                }

                return TimeSpan.FromTicks(_totalElapsedTicks).TotalMilliseconds / count;
            }
        }

        public long AttemptCount
        {
            get { return _attemptCount; }
        }

        public long SuccessCount
        {
            get { return _successCount; }
        }

        /// <summary>
        /// 마지막으로 잘라 낸 자리입니다. 원본 이미지 기준입니다.
        ///
        /// <para>
        /// 이 값이 있으면 잘라 낸 그림 위의 좌표를 원본 좌표로 되돌릴 수 있습니다.
        /// 측정부 좌표는 원본 기준으로 저장하므로, 화면에 크롭을 보여 주면서도
        /// 좌표를 어긋나지 않게 하려면 이 값이 필요합니다.
        /// </para>
        /// </summary>
        public CropRegion LastRegion
        {
            get
            {
                lock (_syncRoot)
                {
                    return _croppedRegion;
                }
            }
        }

        /// <summary>
        /// 프레임을 잘라 봅니다. 성공하면 잘린 그림이 out 인자로 나옵니다.
        ///
        /// <para>
        /// 돌려주는 배열은 이 단계가 들고 있는 것이라 받은 쪽이 바로 화면 버퍼로 옮기고
        /// 참조를 놓아야 합니다. 다음 크롭에서 다시 채워지기 때문입니다.
        /// </para>
        /// </summary>
        /// <returns>잘라 낸 그림을 쓸 수 있으면 true, 원본을 써야 하면 false입니다.</returns>
        public bool TryCrop(
            byte[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            out byte[] croppedPixels,
            out int croppedWidth,
            out int croppedHeight)
        {
            croppedPixels = null;
            croppedWidth = 0;
            croppedHeight = 0;

            if (_disposed || sourcePixels == null || sourceWidth <= 0 || sourceHeight <= 0)
            {
                return false;
            }

            byte[] pendingPixels = null;
            int pendingWidth = 0;
            int pendingHeight = 0;
            CropRegion region;

            lock (_syncRoot)
            {
                DateTime now = DateTime.Now;
                bool shouldAttempt =
                    !IsRegionLocked &&
                    !_cropRunning &&
                    (MinimumIntervalMilliseconds <= 0 ||
                     _lastAttemptedAt == DateTime.MinValue ||
                     (now - _lastAttemptedAt).TotalMilliseconds >= MinimumIntervalMilliseconds);

                if (shouldAttempt)
                {
                    _lastAttemptedAt = now;
                    _cropRunning = true;

                    // 넘겨받은 배열은 화면이 계속 쓰는 것이라 그대로 들고 가면 안 됩니다.
                    // 자르는 동안 다음 프레임이 덮어쓰기 때문입니다.
                    pendingPixels = new byte[checked(sourceWidth * sourceHeight * 3)];
                    Buffer.BlockCopy(sourcePixels, 0, pendingPixels, 0, pendingPixels.Length);
                    pendingWidth = sourceWidth;
                    pendingHeight = sourceHeight;
                }

                region = _croppedRegion;
            }

            // 들어온 프레임을 지난번에 알아낸 자리로 곧바로 잘라 냅니다.
            //
            // SAM 크롭은 한 번에 0.7초가 걸립니다. 그 결과 그림만 화면에 쓰면, 잘라 낸 그림이
            // 갱신될 때만 화면이 바뀌어 2~10초에 한 번 움직이는 정지 화면이 됩니다.
            //
            // 그런데 제품은 고정된 자리에 놓여 있어 잘라 낼 자리가 자주 바뀌지 않습니다.
            // 그래서 자리는 가끔 알아내고, 들어오는 모든 프레임은 그 자리로 직접 잘라 그립니다.
            // 이 자르기는 배열 복사라 1ms 도 걸리지 않아, 카메라가 주는 만큼 화면이 살아납니다.
            bool cropped =
                region != null &&
                region.IsValid &&
                TryCropByRegion(
                    sourcePixels, sourceWidth, sourceHeight, region,
                    out croppedPixels, out croppedWidth, out croppedHeight);

            // 자를 자리를 다시 알아낼 때가 되었으면 뒤에서 SAM 크롭을 돌립니다.
            //
            // 이 일은 화면을 그리는 스레드 밖에서 합니다. 그리는 스레드에서 부르면 그동안
            // 창 전체가 멈추고, 카메라가 여섯이면 서로 밀려 계속 얼어붙습니다.
            if (pendingPixels != null)
            {
                QueueCrop(pendingPixels, pendingWidth, pendingHeight);
            }

            return cropped;
        }

        /// <summary>
        /// 프레임에서 그 자리만 잘라 냅니다. 배열을 줄 단위로 옮기는 것이 전부입니다.
        ///
        /// <para>
        /// 자리가 프레임 밖으로 나가 있으면 안쪽으로 다듬습니다. 카메라 해상도가 바뀌거나
        /// 자리를 알아낸 뒤 프레임 크기가 달라지면 그대로 쓰다가 범위를 넘길 수 있습니다.
        /// </para>
        /// </summary>
        private bool TryCropByRegion(
            byte[] sourcePixels,
            int sourceWidth,
            int sourceHeight,
            CropRegion region,
            out byte[] croppedPixels,
            out int croppedWidth,
            out int croppedHeight)
        {
            croppedPixels = null;
            croppedWidth = 0;
            croppedHeight = 0;

            int left = Clamp(region.X, 0, sourceWidth - 1);
            int top = Clamp(region.Y, 0, sourceHeight - 1);
            int width = Clamp(region.Width, 1, sourceWidth - left);
            int height = Clamp(region.Height, 1, sourceHeight - top);

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            int sourceStride = checked(sourceWidth * 3);
            int targetStride = checked(width * 3);
            if (sourcePixels.Length < checked(sourceStride * sourceHeight))
            {
                return false;
            }

            byte[] target = RentCropBuffer(checked(targetStride * height));
            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(
                    sourcePixels,
                    checked((top + row) * sourceStride + left * 3),
                    target,
                    checked(row * targetStride),
                    targetStride);
            }

            croppedPixels = target;
            croppedWidth = width;
            croppedHeight = height;
            return true;
        }

        /// <summary>
        /// 잘라 낸 그림을 담을 배열입니다. 크기가 그대로면 다시 씁니다.
        /// 초당 여섯 대가 다섯 장씩 자르므로 매번 새로 만들면 쓰레기가 많이 생깁니다.
        /// </summary>
        private byte[] RentCropBuffer(int byteLength)
        {
            if (_regionBuffer == null || _regionBuffer.Length != byteLength)
            {
                _regionBuffer = new byte[byteLength];
            }

            return _regionBuffer;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        /// <summary>
        /// 뒤에서 한 장을 자릅니다.
        ///
        /// <para>
        /// 한 번에 하나씩만 자릅니다. 카메라 여섯이 한꺼번에 부르면 GPU가 몰려
        /// 한 번에 3초를 넘기기도 했습니다. 줄을 세우면 한 번에 걸리는 시간이 고르게 됩니다.
        /// </para>
        /// </summary>
        private void QueueCrop(byte[] sourcePixels, int sourceWidth, int sourceHeight)
        {
            CropWorkItem item = new CropWorkItem();
            item.Stage = this;
            item.Pixels = sourcePixels;
            item.Width = sourceWidth;
            item.Height = sourceHeight;

            lock (CropQueueSync)
            {
                CropQueue.Enqueue(item);
                if (_cropWorkerRunning)
                {
                    return;
                }

                _cropWorkerRunning = true;
            }

            ThreadPool.QueueUserWorkItem(ProcessCropQueue);
        }

        /// <summary>
        /// 줄 서 있는 크롭을 들어온 차례대로 하나씩 처리합니다.
        ///
        /// <para>
        /// 예전에는 카메라마다 따로 스레드를 띄우고 자물쇠 하나로 줄을 세웠습니다. 그런데
        /// 자물쇠는 차례를 지켜 주지 않습니다. 방금 놓은 스레드가 곧바로 다시 잡기 쉬워,
        /// 여섯 중 둘이 GPU 를 독차지하고 나머지 넷은 10초 내내 한 번도 못 도는 일이
        /// 벌어졌습니다.
        /// </para>
        ///
        /// <para>
        /// 한 줄로 세워 차례대로 꺼내면 여섯이 고르게 돌아갑니다. 자를 자리는 한 번 잡으면
        /// 계속 쓰므로 카메라마다 가끔 한 번이면 충분합니다.
        /// </para>
        /// </summary>
        private static void ProcessCropQueue(object unused)
        {
            while (true)
            {
                CropWorkItem item;
                lock (CropQueueSync)
                {
                    if (CropQueue.Count == 0)
                    {
                        _cropWorkerRunning = false;
                        return;
                    }

                    item = CropQueue.Dequeue();
                }

                CallbackFrameCropStage stage = item.Stage;
                try
                {
                    if (stage != null && !stage._disposed)
                    {
                        stage.RunCrop(item.Pixels, item.Width, item.Height, DateTime.Now);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("콜백 프레임 크롭 실패. " + ex.Message);
                }
                finally
                {
                    if (stage != null)
                    {
                        lock (stage._syncRoot)
                        {
                            stage._cropRunning = false;
                        }
                    }
                }
            }
        }

        private sealed class CropWorkItem
        {
            public CallbackFrameCropStage Stage;
            public byte[] Pixels;
            public int Width;
            public int Height;
        }

        private void RunCrop(byte[] sourcePixels, int sourceWidth, int sourceHeight, DateTime now)
        {
            IntPtr vladId = VLAD_Ops_RTSP.GetActiveVladId();
            if (vladId == IntPtr.Zero)
            {
                // AI 준비가 끝나기 전에는 자를 수 없습니다. 화면은 원본으로 나갑니다.
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                // 원본을 담을 Mat 은 이 자리에서 만들고 이 자리에서 버립니다.
                //
                // 예전에는 필드에 두고 다시 썼습니다. 자르는 일이 화면 스레드에서 돌 때는
                // 괜찮았지만, 뒤에서 돌게 되면서 창을 닫거나 화면을 초기화할 때 Dispose 가
                // 이 Mat 을 해제해 버렸습니다. 그 순간 자르던 스레드가 이미 풀린 메모리에
                // 쓰다가 AccessViolationException 으로 죽었습니다.
                //
                // 그 예외는 붙잡을 수 없는 종류라 finally 도 지나치지 못했고, 그래서
                // "자르는 중" 표시가 참으로 남아 그 카메라는 다시는 자르지 않았습니다.
                // 여섯 중 몇 대만 크롭되던 까닭이 이것입니다.
                //
                // 한 장 만드는 데 몇 ms 면 됩니다. 자르는 데 1초가 걸리므로 아깝지 않습니다.
                using (Mat sourceMat = new Mat(sourceHeight, sourceWidth, MatType.CV_8UC3))
                {
                Marshal.Copy(
                    sourcePixels, 0, sourceMat.Data, checked(sourceWidth * sourceHeight * 3));

                ImageViewType viewType = RtspMonitorIndexPolicy.ToViewType(_monitorIndex);
                int viewCode = VladViewCodePolicy.FromViewType(viewType);

                // 받는 Mat은 매번 새로 만들어야 합니다. 앞선 결과가 남아 있으면 섞입니다.
                //
                // 잘라 낸 그림과 그 자리를 한 번에 받습니다. 자리를 따로 물으면 SAM이 두 번 도는데,
                // 담당자가 그 낭비를 없애려고 인자를 합쳐 주었습니다.
                IntPtr jsonBuffer = AllocateJsonBuffer();
                try
                {
                    using (Mat cropped = new Mat())
                    {
                        // 자르기와 검사는 같은 VLAD 세션을 씁니다. 그래서 같은 자물쇠를 씁니다.
                        //
                        // 두 호출 모두 FullImageVladId 하나로 들어갑니다. 검사 쪽만 자물쇠를
                        // 걸어 두었더니, 검사 버튼을 누른 순간 아직 GPU 를 잡고 있던 SAM 과
                        // 추론이 같은 세션으로 겹쳐 들어가 서로 물렸습니다. SDK 로그가 SAM
                        // 인코더에 들어간 자리에서 몇 분씩 멈추고 검사가 끝나지 않았습니다.
                        //
                        // 자르는 데 1 초쯤 걸리므로 검사가 그만큼 늦어질 수 있습니다.
                        // 검사가 아예 끝나지 않는 것보다는 낫습니다.
                        bool succeeded;
                        lock (VLAD_Ops_Ai.NativeInferenceSyncRoot)
                        {
                            succeeded = VladNativeMethods.VLAD_HD_Crop_Mat(
                                vladId, sourceMat.CvPtr, viewCode, cropped.CvPtr, jsonBuffer);
                        }

                        watch.Stop();
                        _totalElapsedTicks += watch.Elapsed.Ticks;
                        _attemptCount++;

                        bool usable = succeeded && !cropped.Empty();
                        RtspFrameMetrics.RecordCrop(_monitorIndex, watch.Elapsed.Ticks, usable);

                        if (!usable)
                        {
                            lock (_syncRoot) { _croppedRegion = null; }
                            return;
                        }

                        // 잘라 낸 그림 자체는 쓰지 않습니다. 자리만 있으면 들어오는 프레임을
                        // 그 자리로 곧바로 자를 수 있고, 그래야 화면이 카메라 속도로 움직입니다.
                        StoreRegion(jsonBuffer);
                        _successCount++;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(jsonBuffer);
                }
                }
            }
            catch (Exception ex)
            {
                watch.Stop();
                _totalElapsedTicks += watch.Elapsed.Ticks;
                _attemptCount++;
                RtspFrameMetrics.RecordCrop(_monitorIndex, watch.Elapsed.Ticks, false);
                lock (_syncRoot) { _croppedRegion = null; }

                Debug.WriteLine(
                    "콜백 프레임 크롭 실패. MonitorIndex=" +
                    _monitorIndex.ToString(CultureInfo.InvariantCulture) + ", " + ex.Message);
            }
        }

        /// <summary>
        /// SDK가 잘라 낸 자리를 채워 줄 버퍼를 만듭니다.
        /// 검사 JSON과 같은 방식으로, 0으로 채운 고정 크기 버퍼를 넘깁니다.
        /// </summary>
        private static IntPtr AllocateJsonBuffer()
        {
            IntPtr buffer = Marshal.AllocHGlobal(CropJsonBufferSize);
            byte[] empty = new byte[CropJsonBufferSize];
            Marshal.Copy(empty, 0, buffer, CropJsonBufferSize);
            return buffer;
        }

        private void StoreRegion(IntPtr jsonBuffer)
        {
            string json = Marshal.PtrToStringAnsi(jsonBuffer);
            CropRegion region;
            CropRegion parsed = CropRegion.TryParse(json, out region) ? region : null;

            // LastRegion 은 화면 쪽에서 읽으므로 자물쇠 안에서 갈아 끼웁니다.
            lock (_syncRoot)
            {
                _croppedRegion = parsed;
            }

            // 저장한 원본을 같은 자리로 잘라 보여 주려면 화면 쪽에서도 이 자리를 알아야 합니다.
            PublishLatestRegion(_monitorIndex, parsed);
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _regionBuffer = null;
            }
        }
    }
}
