using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using AI.Vision.IOInspector.Infrastructure;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// RTSP 콜백으로 프레임이 실제로 얼마나 들어오는지 세어 두는 곳입니다.
    ///
    /// <para>
    /// 화면을 콜백 프레임으로 그리려면 초당 몇 장이 오는지, 크기가 얼마인지,
    /// 카메라 6대가 번호로 제대로 나뉘어 오는지를 알아야 합니다.
    /// 이 값들은 코드만 봐서는 알 수 없고 카메라를 붙여 재봐야 합니다.
    /// </para>
    ///
    /// <para>
    /// 세는 일은 콜백 스레드에서 일어나므로 최대한 가볍게 둡니다.
    /// 숫자만 올리고, 파일에 적는 일은 별도 타이머가 맡습니다.
    /// 콜백 안에서 파일을 건드리면 그동안 SDK가 다음 프레임을 넘기지 못합니다.
    /// </para>
    /// </summary>
    public static class RtspFrameMetrics
    {
        private const string LogName = "rtsp-frame-metrics";

        /// <summary>
        /// 설정이 없을 때 쓰는 보고 주기입니다.
        ///
        /// <para>
        /// 처음에는 프레임이 실제로 어떻게 들어오는지 봐야 해서 짧게 잡습니다.
        /// 값이 확인되고 나면 설정으로 늘리거나 꺼서 로그가 쌓이지 않게 합니다.
        /// </para>
        /// </summary>
        private const int DefaultReportIntervalSeconds = 10;

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, ChannelCounter> CountersByMonitorIndex =
            new Dictionary<int, ChannelCounter>();

        private static Timer _reportTimer;
        private static bool _enabled;
        private static DateTime _lastReportedAt;

        /// <summary>
        /// 기본 주기로 세기를 시작합니다.
        /// </summary>
        public static void Start()
        {
            Start(DefaultReportIntervalSeconds);
        }

        /// <summary>
        /// 세기를 시작합니다. 이미 켜져 있으면 아무것도 하지 않습니다.
        /// </summary>
        /// <param name="reportIntervalSeconds">
        /// 로그에 남기는 주기입니다. 0 이하이면 아예 세지 않습니다.
        /// 현장 확인이 끝난 뒤에는 길게 두거나 꺼서 로그가 불어나지 않게 합니다.
        /// </param>
        public static void Start(int reportIntervalSeconds)
        {
            if (reportIntervalSeconds <= 0)
            {
                // 꺼 두면 세는 일 자체를 하지 않습니다.
                // 콜백마다 부르는 자리라 조건 하나라도 줄이는 편이 낫습니다.
                return;
            }

            int intervalMilliseconds = reportIntervalSeconds * 1000;
            lock (SyncRoot)
            {
                if (_enabled)
                {
                    return;
                }

                _enabled = true;
                _lastReportedAt = DateTime.Now;
                CountersByMonitorIndex.Clear();

                _reportTimer = new Timer(
                    OnReportTick, null, intervalMilliseconds, intervalMilliseconds);
            }

            Append(
                "START",
                "RTSP 프레임 계측을 시작했습니다. 보고 주기 " +
                reportIntervalSeconds.ToString(CultureInfo.InvariantCulture) + "초");
        }

        public static void Stop()
        {
            Timer timer;
            lock (SyncRoot)
            {
                if (!_enabled)
                {
                    return;
                }

                _enabled = false;
                timer = _reportTimer;
                _reportTimer = null;
            }

            if (timer != null)
            {
                timer.Dispose();
            }

            WriteReport("STOP");
        }

        /// <summary>
        /// 설정한 해상도를 알려 둡니다. 실제로 들어온 크기와 견주기 위한 것입니다.
        ///
        /// <para>
        /// 설정과 실제가 다르면 검사에 쓸 수 없는 크기가 들어오고 있다는 뜻인데,
        /// 그림만 봐서는 알아채기 어렵습니다. 로그에 함께 적어 눈에 띄게 합니다.
        /// </para>
        /// </summary>
        public static void RegisterExpectedSize(int monitorIndex, int frameWidth, int frameHeight)
        {
            ChannelCounter counter = GetCounter(monitorIndex);
            counter.ExpectedWidth = frameWidth;
            counter.ExpectedHeight = frameHeight;
        }

        /// <summary>
        /// 콜백이 들어온 즉시 부릅니다. 솎아내기 전에 세야 실제 수신량을 알 수 있습니다.
        /// </summary>
        public static void RecordReceived(int monitorIndex, int frameWidth, int frameHeight)
        {
            if (!_enabled)
            {
                return;
            }

            ChannelCounter counter = GetCounter(monitorIndex);
            Interlocked.Increment(ref counter.Received);
            counter.RecordArrival(DateTime.Now.Ticks, Thread.CurrentThread.ManagedThreadId);

            // 크기가 바뀌면 마지막 값만 남깁니다. 채널마다 한 가지로 고정될 것으로 보지만,
            // 달라진다면 그 사실 자체가 확인해야 할 내용입니다.
            counter.LastWidth = frameWidth;
            counter.LastHeight = frameHeight;
        }

        /// <summary>최소 간격에 걸려 버린 프레임입니다.</summary>
        public static void RecordSkippedByInterval(int monitorIndex)
        {
            if (!_enabled)
            {
                return;
            }

            Interlocked.Increment(ref GetCounter(monitorIndex).SkippedByInterval);
        }

        /// <summary>크기를 알 수 없어 읽지 않고 버린 프레임입니다.</summary>
        public static void RecordSkippedByUnknownSize(int monitorIndex)
        {
            if (!_enabled)
            {
                return;
            }

            Interlocked.Increment(ref GetCounter(monitorIndex).SkippedByUnknownSize);
        }

        /// <summary>캐시에 실제로 담긴 프레임입니다.</summary>
        public static void RecordPublished(int monitorIndex)
        {
            if (!_enabled)
            {
                return;
            }

            Interlocked.Increment(ref GetCounter(monitorIndex).Published);
        }

        /// <summary>
        /// 크롭 한 번의 결과를 남깁니다.
        ///
        /// <para>
        /// 크롭을 매 프레임 부를 수 있을지는 1회에 얼마나 걸리는지에 달렸습니다.
        /// 그 값을 재려고 여기에 함께 담습니다.
        /// </para>
        /// </summary>
        public static void RecordCrop(int monitorIndex, long elapsedTicks, bool succeeded)
        {
            if (!_enabled)
            {
                return;
            }

            ChannelCounter counter = GetCounter(monitorIndex);
            Interlocked.Increment(ref counter.CropAttempts);
            Interlocked.Add(ref counter.CropElapsedTicks, elapsedTicks);

            if (succeeded)
            {
                Interlocked.Increment(ref counter.CropSucceeded);
            }
        }

        /// <summary>
        /// 네이티브 자물쇠를 못 잡아 이번 크롭을 거른 횟수를 남깁니다.
        ///
        /// <para>
        /// 이 수가 계속 오르면 검사나 병합이 자물쇠를 오래 쥐고 있다는 뜻입니다.
        /// 거른 것 자체는 문제가 아니지만, 자리 갱신이 늦어지는 까닭은 알아야 합니다.
        /// </para>
        /// </summary>
        public static void RecordCropSkipped(int monitorIndex)
        {
            if (!_enabled)
            {
                return;
            }

            Interlocked.Increment(ref GetCounter(monitorIndex).CropSkippedByLock);
        }

        public static void RecordFailed(int monitorIndex)
        {
            if (!_enabled)
            {
                return;
            }

            Interlocked.Increment(ref GetCounter(monitorIndex).Failed);
        }

        private static ChannelCounter GetCounter(int monitorIndex)
        {
            ChannelCounter counter;
            lock (SyncRoot)
            {
                if (!CountersByMonitorIndex.TryGetValue(monitorIndex, out counter))
                {
                    counter = new ChannelCounter();
                    CountersByMonitorIndex[monitorIndex] = counter;
                }
            }

            return counter;
        }

        private static void OnReportTick(object state)
        {
            try
            {
                WriteReport("REPORT");
            }
            catch
            {
                // 계측 때문에 프로그램이 흔들리면 안 됩니다.
            }
        }

        private static void WriteReport(string stage)
        {
            DateTime now = DateTime.Now;
            double elapsedSeconds;
            List<KeyValuePair<int, ChannelSnapshot>> snapshots = new List<KeyValuePair<int, ChannelSnapshot>>();

            lock (SyncRoot)
            {
                elapsedSeconds = (now - _lastReportedAt).TotalSeconds;
                _lastReportedAt = now;

                foreach (KeyValuePair<int, ChannelCounter> pair in CountersByMonitorIndex)
                {
                    snapshots.Add(new KeyValuePair<int, ChannelSnapshot>(pair.Key, pair.Value.TakeSnapshot()));
                }
            }

            if (snapshots.Count == 0)
            {
                Append(stage, "들어온 프레임이 없습니다. 콜백 등록과 NVR 연결을 확인해야 합니다.");
                return;
            }

            snapshots.Sort(delegate (KeyValuePair<int, ChannelSnapshot> left, KeyValuePair<int, ChannelSnapshot> right)
            {
                return left.Key.CompareTo(right.Key);
            });

            StringBuilder builder = new StringBuilder();
            builder.Append("구간 ");
            builder.Append(elapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture));
            builder.Append("초");

            foreach (KeyValuePair<int, ChannelSnapshot> pair in snapshots)
            {
                ChannelSnapshot snapshot = pair.Value;
                double framesPerSecond = elapsedSeconds > 0 ? snapshot.Received / elapsedSeconds : 0;

                builder.Append(" | mon");
                builder.Append(pair.Key.ToString(CultureInfo.InvariantCulture));
                builder.Append(" ");
                builder.Append(snapshot.LastWidth.ToString(CultureInfo.InvariantCulture));
                builder.Append("x");
                builder.Append(snapshot.LastHeight.ToString(CultureInfo.InvariantCulture));
                if (snapshot.ExpectedWidth > 0 &&
                    (snapshot.LastWidth != snapshot.ExpectedWidth || snapshot.LastHeight != snapshot.ExpectedHeight))
                {
                    // 설정과 다른 크기가 들어오고 있습니다.
                    // 이대로면 검사에는 원본을 따로 받아야 하므로 눈에 띄게 적습니다.
                    builder.Append("(설정 ");
                    builder.Append(snapshot.ExpectedWidth.ToString(CultureInfo.InvariantCulture));
                    builder.Append("x");
                    builder.Append(snapshot.ExpectedHeight.ToString(CultureInfo.InvariantCulture));
                    builder.Append(" 와 다름)");
                }

                builder.Append(" 수신 ");
                builder.Append(snapshot.Received.ToString(CultureInfo.InvariantCulture));
                builder.Append("장(");
                builder.Append(framesPerSecond.ToString("0.0", CultureInfo.InvariantCulture));
                builder.Append("fps) 담김 ");
                builder.Append(snapshot.Published.ToString(CultureInfo.InvariantCulture));

                // 간격이 고른지, 어느 스레드가 물어다 주는지 함께 적습니다.
                //
                // 평균만으로는 프레임이 고르게 오는지 몰렸다 끊겼다 하는지 알 수 없습니다.
                // 스레드 번호는 여섯 채널이 한 줄로 처리되는지(같은 번호) 따로 도는지(다른 번호)를
                // 가릅니다. 어느 쪽인지에 따라 손댈 곳이 달라집니다.
                if (snapshot.GapCount > 0)
                {
                    double averageGapMilliseconds =
                        TimeSpan.FromTicks(snapshot.GapTotalTicks).TotalMilliseconds / snapshot.GapCount;

                    builder.Append(" 간격 평균 ");
                    builder.Append(averageGapMilliseconds.ToString("0", CultureInfo.InvariantCulture));
                    builder.Append("ms 최대 ");
                    builder.Append(TimeSpan.FromTicks(snapshot.GapMaxTicks).TotalMilliseconds
                        .ToString("0", CultureInfo.InvariantCulture));
                    builder.Append("ms");
                }

                builder.Append(" 스레드#");
                builder.Append(snapshot.LastThreadId.ToString(CultureInfo.InvariantCulture));

                if (snapshot.SkippedByInterval > 0)
                {
                    builder.Append(" 간격버림 ");
                    builder.Append(snapshot.SkippedByInterval.ToString(CultureInfo.InvariantCulture));
                }

                if (snapshot.SkippedByUnknownSize > 0)
                {
                    builder.Append(" 크기모름 ");
                    builder.Append(snapshot.SkippedByUnknownSize.ToString(CultureInfo.InvariantCulture));
                }

                if (snapshot.Failed > 0)
                {
                    builder.Append(" 실패 ");
                    builder.Append(snapshot.Failed.ToString(CultureInfo.InvariantCulture));
                }

                if (snapshot.CropAttempts > 0)
                {
                    double averageMilliseconds =
                        TimeSpan.FromTicks(snapshot.CropElapsedTicks).TotalMilliseconds / snapshot.CropAttempts;

                    builder.Append(" 크롭 ");
                    builder.Append(snapshot.CropAttempts.ToString(CultureInfo.InvariantCulture));
                    builder.Append("회 평균 ");
                    builder.Append(averageMilliseconds.ToString("0.0", CultureInfo.InvariantCulture));
                    builder.Append("ms 성공 ");
                    builder.Append(snapshot.CropSucceeded.ToString(CultureInfo.InvariantCulture));
                }

                if (snapshot.CropSkippedByLock > 0)
                {
                    builder.Append(" 크롭거름(자물쇠) ");
                    builder.Append(snapshot.CropSkippedByLock.ToString(CultureInfo.InvariantCulture));
                }
            }

            Append(stage, builder.ToString());
        }

        private static void Append(string stage, string message)
        {
            try
            {
                string logFilePath = ApplicationLogFileResolver.GetLogFilePath(
                    AppDomain.CurrentDomain.BaseDirectory, LogName);
                string line =
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                    " [" + stage + "] " + message + Environment.NewLine;

                File.AppendAllText(logFilePath, line, Encoding.UTF8);
            }
            catch
            {
                // 로그를 남기지 못해도 계측은 계속합니다.
            }
        }

        private sealed class ChannelCounter
        {
            public long Received;
            public long Published;
            public long SkippedByInterval;
            public long SkippedByUnknownSize;
            public long Failed;
            public long CropAttempts;
            public long CropSucceeded;
            public long CropSkippedByLock;
            public long CropElapsedTicks;

            /// <summary>
            /// 콜백이 들어온 간격입니다. 평균만으로는 고르게 오는지 몰려 오는지 알 수 없습니다.
            /// </summary>
            public long GapCount;
            public long GapTotalTicks;
            public long GapMaxTicks;

            /// <summary>
            /// 콜백을 물어다 준 스레드입니다. 여섯 채널이 같은 스레드면 SDK가 한 줄로 처리하는 것이고,
            /// 채널마다 다르면 따로 돌고 있는 것입니다. 어느 쪽인지에 따라 손댈 곳이 달라집니다.
            /// </summary>
            public int LastThreadId;

            private long _lastReceivedTicks;

            /// <summary>
            /// 이번 콜백이 지난번과 얼마나 벌어졌는지 기록합니다.
            /// 콜백 스레드에서 도는 코드라 값 몇 개만 갱신합니다.
            /// </summary>
            public void RecordArrival(long nowTicks, int threadId)
            {
                LastThreadId = threadId;

                long previous = Interlocked.Exchange(ref _lastReceivedTicks, nowTicks);
                if (previous <= 0)
                {
                    return;
                }

                long gap = nowTicks - previous;
                if (gap <= 0)
                {
                    return;
                }

                Interlocked.Increment(ref GapCount);
                Interlocked.Add(ref GapTotalTicks, gap);

                long currentMax = Interlocked.CompareExchange(ref GapMaxTicks, 0, 0);
                while (gap > currentMax)
                {
                    long exchanged = Interlocked.CompareExchange(ref GapMaxTicks, gap, currentMax);
                    if (exchanged == currentMax)
                    {
                        break;
                    }

                    currentMax = exchanged;
                }
            }

            private int _lastWidth;
            private int _lastHeight;
            private int _expectedWidth;
            private int _expectedHeight;

            public int ExpectedWidth
            {
                get { return Interlocked.CompareExchange(ref _expectedWidth, 0, 0); }
                set { Interlocked.Exchange(ref _expectedWidth, value); }
            }

            public int ExpectedHeight
            {
                get { return Interlocked.CompareExchange(ref _expectedHeight, 0, 0); }
                set { Interlocked.Exchange(ref _expectedHeight, value); }
            }

            public int LastWidth
            {
                get { return Interlocked.CompareExchange(ref _lastWidth, 0, 0); }
                set { Interlocked.Exchange(ref _lastWidth, value); }
            }

            public int LastHeight
            {
                get { return Interlocked.CompareExchange(ref _lastHeight, 0, 0); }
                set { Interlocked.Exchange(ref _lastHeight, value); }
            }

            /// <summary>
            /// 구간 값을 읽고 0으로 되돌립니다. 다음 구간을 처음부터 세기 위해서입니다.
            /// </summary>
            public ChannelSnapshot TakeSnapshot()
            {
                ChannelSnapshot snapshot = new ChannelSnapshot();
                snapshot.Received = Interlocked.Exchange(ref Received, 0);
                snapshot.Published = Interlocked.Exchange(ref Published, 0);
                snapshot.SkippedByInterval = Interlocked.Exchange(ref SkippedByInterval, 0);
                snapshot.SkippedByUnknownSize = Interlocked.Exchange(ref SkippedByUnknownSize, 0);
                snapshot.Failed = Interlocked.Exchange(ref Failed, 0);
                snapshot.CropAttempts = Interlocked.Exchange(ref CropAttempts, 0);
                snapshot.CropSucceeded = Interlocked.Exchange(ref CropSucceeded, 0);
                snapshot.CropSkippedByLock = Interlocked.Exchange(ref CropSkippedByLock, 0);
                snapshot.CropElapsedTicks = Interlocked.Exchange(ref CropElapsedTicks, 0);
                snapshot.LastWidth = LastWidth;
                snapshot.LastHeight = LastHeight;
                snapshot.ExpectedWidth = ExpectedWidth;
                snapshot.ExpectedHeight = ExpectedHeight;
                snapshot.GapCount = Interlocked.Exchange(ref GapCount, 0);
                snapshot.GapTotalTicks = Interlocked.Exchange(ref GapTotalTicks, 0);
                snapshot.GapMaxTicks = Interlocked.Exchange(ref GapMaxTicks, 0);
                snapshot.LastThreadId = LastThreadId;
                return snapshot;
            }
        }

        private sealed class ChannelSnapshot
        {
            public long Received;
            public long Published;
            public long SkippedByInterval;
            public long SkippedByUnknownSize;
            public long Failed;
            public long CropAttempts;
            public long CropSucceeded;
            public long CropSkippedByLock;
            public long CropElapsedTicks;
            public int LastWidth;
            public int LastHeight;
            public int ExpectedWidth;
            public int ExpectedHeight;
            public long GapCount;
            public long GapTotalTicks;
            public long GapMaxTicks;
            public int LastThreadId;
        }
    }
}
