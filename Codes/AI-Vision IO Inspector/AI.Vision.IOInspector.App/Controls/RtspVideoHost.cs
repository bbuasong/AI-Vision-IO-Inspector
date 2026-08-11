using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AI.Vision.IOInspector.Infrastructure;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.Controls
{
    /// <summary>
    /// LibVLC가 RTSP 영상을 직접 렌더링할 수 있도록 WPF 내부에 네이티브 영상 창을 제공합니다.
    /// 검사 화면에서는 파일 스냅샷이 아니라 지속 스트리밍 화면을 표시하기 위해 사용합니다.
    /// </summary>
    public class RtspVideoHost : HwndHost
    {
        /// <summary>
        /// 좌측 상단 기준 이미지 오버레이를 클릭했을 때만 발생합니다.
        /// 영상 영역 클릭으로는 발생하지 않습니다 — 기준 이미지 확대 팝업은 기준 이미지 칸에서만 열립니다.
        /// HwndHost 내부 HWND의 마우스 입력은 일반 WPF MouseBinding으로 전달되지 않으므로 별도 이벤트를 제공합니다.
        /// </summary>
        public static readonly RoutedEvent ReferenceImageClickEvent =
            EventManager.RegisterRoutedEvent(
                "ReferenceImageClick",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(RtspVideoHost));

        public static readonly DependencyProperty StreamUrlProperty =
            DependencyProperty.Register(
                "StreamUrl",
                typeof(string),
                typeof(RtspVideoHost),
                new FrameworkPropertyMetadata(string.Empty, OnStreamPropertyChanged));

        public static readonly DependencyProperty IsStreamingProperty =
            DependencyProperty.Register(
                "IsStreaming",
                typeof(bool),
                typeof(RtspVideoHost),
                new FrameworkPropertyMetadata(false, OnStreamPropertyChanged));

        public static readonly DependencyProperty FrameWidthProperty =
            DependencyProperty.Register(
                "FrameWidth",
                typeof(int),
                typeof(RtspVideoHost),
                new FrameworkPropertyMetadata(0, OnFrameGeometryChanged));

        public static readonly DependencyProperty FrameHeightProperty =
            DependencyProperty.Register(
                "FrameHeight",
                typeof(int),
                typeof(RtspVideoHost),
                new FrameworkPropertyMetadata(0, OnFrameGeometryChanged));

        public static readonly DependencyProperty ReferenceImagePathProperty =
            DependencyProperty.Register(
                "ReferenceImagePath",
                typeof(string),
                typeof(RtspVideoHost),
                new FrameworkPropertyMetadata(string.Empty, OnReferenceImagePathChanged));

        private const int WsChild = 0x40000000;
        private const int WsVisible = 0x10000000;
        private const int WsClipSiblings = 0x04000000;
        private const int WsClipChildren = 0x02000000;
        private const int SsNotify = 0x0100;
        private const int SsBitmap = 0x0000000E;
        private const int WmCommand = 0x0111;
        private const int StnClicked = 0;
        private const int StnDoubleClick = 1;
        private const int SsCenter = 0x00000001;
        private const int SsCenterImage = 0x00000200;
        private const int ImageBitmap = 0;
        private const int StmSetImage = 0x0172;
        private const int WmEraseBkgnd = 0x0014;
        private const int WmPaint = 0x000F;
        private const int WmCtlColorStatic = 0x0138;
        private const int SwHide = 0;
        private const int SwShow = 5;
        private const int DibRgbColors = 0;
        private const int BiRgb = 0;
        private const int SwpNoActivate = 0x0010;
        private const int SwpShowWindow = 0x0040;
        // COLORREF는 0x00BBGGRR 순서입니다. 화면 여백은 #0A1016으로 고정합니다.
        private const uint VideoBackgroundColor = 0x0016100A;
        // 영상이 없을 때 표시하는 안내 문구 색입니다(#7B8790, COLORREF는 0x00BBGGRR).
        private const uint VideoPlaceholderTextColor = 0x0090877B;

        /// <summary>
        /// LibVLC의 Play()는 연결을 시도하기도 전에 즉시 true를 반환하는 비동기 호출입니다.
        /// 따라서 Play() 성공만으로 재생중이라고 기록하면 카메라가 완전히 죽어 있어도
        /// 로그와 툴팁에 "재생중"이 남아 장애를 못 알아채게 됩니다.
        /// 실제로 영상이 나오기 시작했는지 이 시간만큼 확인한 뒤에 성공으로 판정합니다.
        /// </summary>
        private const int PlaybackConfirmTimeoutMilliseconds = 10000;
        private const int PlaybackConfirmPollMilliseconds = 200;

        /// <summary>
        /// 카메라가 과열/재부팅 등으로 끊겼다가 돌아오는 경우를 앱 재시작 없이 복구하기 위한 재시도 주기입니다.
        /// </summary>
        private const int ConnectionCheckIntervalSeconds = 15;

        private IntPtr _childHandle;
        private IntPtr _videoHandle;
        private IntPtr _referenceOverlayHandle;
        private IntPtr _referenceBitmapHandle;
        private IntPtr _videoBackgroundBrush;
        private VlcVideoSession _session;
        private string _activeStreamUrl;
        private string _pendingStreamUrl;
        private int _startSequence;
        private DispatcherTimer _connectionCheckTimer;

        public RtspVideoHost()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// LibVLC 엔진 최초 로딩(리플렉션 어셈블리 로드 + 네이티브 Core.Initialize)을 미리 실행해 둡니다.
        /// 이 초기화는 프로세스당 한 번만 필요한데, 예열하지 않으면 검사 화면이 처음 표시될 때
        /// 6개 카메라 중 가장 먼저 시작한 하나가 이 비용을 통째로 떠안고 나머지는 잠금 대기하게 되어
        /// 카메라가 순차적으로, 그리고 크게 벌어져 나타나는 것처럼 보입니다.
        /// 앱 시작 직후 백그라운드 스레드에서 호출하면, 검사 화면 진입 시점에는 이미 엔진이 준비되어 있습니다.
        /// </summary>
        public static void WarmUpEngine()
        {
            try
            {
                VlcVideoSession.WarmUp();
            }
            catch
            {
                // 예열 실패는 무시합니다. 실제 스트림 시작 시 동일한 오류가 다시 표면화되어 정상적으로 로그/툴팁에 표시됩니다.
            }
        }

        /// <summary>
        /// 기준 이미지 오버레이 클릭을 WPF 상위 화면으로 전달합니다.
        /// </summary>
        public event RoutedEventHandler ReferenceImageClick
        {
            add { AddHandler(ReferenceImageClickEvent, value); }
            remove { RemoveHandler(ReferenceImageClickEvent, value); }
        }

        public string StreamUrl
        {
            get { return (string)GetValue(StreamUrlProperty); }
            set { SetValue(StreamUrlProperty, value); }
        }

        public bool IsStreaming
        {
            get { return (bool)GetValue(IsStreamingProperty); }
            set { SetValue(IsStreamingProperty, value); }
        }

        /// <summary>
        /// Config.json의 CAM_WIDTH/CAM_HEIGHT입니다.
        /// LibVLC 영상 창과 기준 이미지 오버레이가 실제 카메라 프레임 비율을 유지하도록 사용합니다.
        /// </summary>
        public int FrameWidth
        {
            get { return (int)GetValue(FrameWidthProperty); }
            set { SetValue(FrameWidthProperty, value); }
        }

        public int FrameHeight
        {
            get { return (int)GetValue(FrameHeightProperty); }
            set { SetValue(FrameHeightProperty, value); }
        }

        public string ReferenceImagePath
        {
            get { return (string)GetValue(ReferenceImagePathProperty); }
            set { SetValue(ReferenceImagePathProperty, value); }
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            int width = Math.Max(1, (int)ActualWidth);
            int height = Math.Max(1, (int)ActualHeight);
            _childHandle = CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                WsChild | WsVisible | WsClipSiblings | WsClipChildren,
                0,
                0,
                width,
                height,
                hwndParent.Handle,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            // 영상이 없을 때 이 STATIC이 그대로 노출되므로, 가운데 정렬 스타일을 주고
            // 상태 문구를 표시할 수 있게 합니다(흰 백지 대신 무슨 상태인지 보이도록).
            _videoHandle = CreateWindowEx(
                0,
                "STATIC",
                "카메라 연결 중...",
                WsChild | WsVisible | WsClipSiblings | WsClipChildren | SsCenter | SsCenterImage,
                0,
                0,
                width,
                height,
                _childHandle,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            _referenceOverlayHandle = CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                WsChild | WsVisible | SsBitmap | SsNotify,
                6,
                6,
                Math.Max(1, width / 4),
                Math.Max(1, height / 4),
                _childHandle,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            UpdateChildWindowLayout(width, height);
            UpdateReferenceOverlay();
            RestartSession();
            return new HandleRef(this, _childHandle);
        }

        /// <summary>
        /// 네이티브 영상의 두 번 클릭을 WPF 이벤트로 전달하고 비어 있는 여백을 #0A1016으로 칠합니다.
        /// </summary>
        protected override IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // LibVLC가 영상을 그리지 못하면 영상용 STATIC 컨트롤이 그대로 노출되는데,
            // 기본 STATIC 배경 브러시는 흰색(COLOR_WINDOW)이라 카메라 칸이 백지로 보였습니다.
            // 이 메시지를 처리해 영상이 없을 때도 화면 배경색과 같은 어두운 색이 유지되게 합니다.
            if (message == WmCtlColorStatic &&
                (lParam == _videoHandle || lParam == _referenceOverlayHandle))
            {
                SetBkColor(wParam, VideoBackgroundColor);
                SetTextColor(wParam, VideoPlaceholderTextColor);
                handled = true;
                return EnsureVideoBackgroundBrush();
            }

            // 기준 이미지 확대 팝업은 좌측 상단 기준 이미지 칸에서만 열립니다.
            // 이전에는 영상 영역(_videoHandle) 클릭으로도 팝업이 열려, 영상이 안 나올 때
            // 화면을 눌러 보면 의도치 않게 팝업이 떴습니다.
            if (message == WmCommand &&
                lParam == _referenceOverlayHandle &&
                (GetHighWord(wParam) == StnClicked || GetHighWord(wParam) == StnDoubleClick))
            {
                RaiseEvent(new RoutedEventArgs(ReferenceImageClickEvent, this));
                handled = true;
                return IntPtr.Zero;
            }

            if (hwnd == _childHandle && (message == WmEraseBkgnd || message == WmPaint))
            {
                if (message == WmEraseBkgnd)
                {
                    PaintVideoBackground(hwnd, wParam);
                }
                else
                {
                    PaintStruct paintStruct;
                    IntPtr deviceContext = BeginPaint(hwnd, out paintStruct);
                    try
                    {
                        PaintVideoBackground(hwnd, deviceContext);
                    }
                    finally
                    {
                        EndPaint(hwnd, ref paintStruct);
                    }
                }

                handled = true;
                return IntPtr.Zero;
            }

            return base.WndProc(hwnd, message, wParam, lParam, ref handled);
        }
        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            StopConnectionCheckTimer();
            CancelPendingStart();
            StopSession();
            ReleaseReferenceBitmap();
            ReleaseVideoBackgroundBrush();

            if (hwnd.Handle != IntPtr.Zero)
            {
                DestroyWindow(hwnd.Handle);
            }

            _childHandle = IntPtr.Zero;
            _videoHandle = IntPtr.Zero;
            _referenceOverlayHandle = IntPtr.Zero;
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            if (_childHandle != IntPtr.Zero)
            {
                int width = Math.Max(1, (int)rcBoundingBox.Width);
                int height = Math.Max(1, (int)rcBoundingBox.Height);
                UpdateChildWindowLayout(width, height);
                UpdateReferenceOverlay();
            }
        }

        private static void OnStreamPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            RtspVideoHost host = dependencyObject as RtspVideoHost;
            if (host != null)
            {
                host.RestartSession();
            }
        }

        private static void OnReferenceImagePathChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            RtspVideoHost host = dependencyObject as RtspVideoHost;
            if (host != null)
            {
                host.UpdateReferenceOverlay();
            }
        }

        private static void OnFrameGeometryChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            RtspVideoHost host = dependencyObject as RtspVideoHost;
            if (host != null)
            {
                host.UpdateFrameGeometry();
            }
        }

        private void UpdateFrameGeometry()
        {
            int width = Math.Max(1, (int)ActualWidth);
            int height = Math.Max(1, (int)ActualHeight);
            UpdateChildWindowLayout(width, height);

            if (_session != null)
            {
                _session.SetAspectRatio(FrameWidth, FrameHeight);
            }

            UpdateReferenceOverlay();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RestartSession();
            StartConnectionCheckTimer();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopConnectionCheckTimer();
            CancelPendingStart();
            StopSession();
        }

        private void StartConnectionCheckTimer()
        {
            if (_connectionCheckTimer != null)
            {
                return;
            }

            _connectionCheckTimer = new DispatcherTimer();
            _connectionCheckTimer.Interval = TimeSpan.FromSeconds(ConnectionCheckIntervalSeconds);
            _connectionCheckTimer.Tick += OnConnectionCheckTick;
            _connectionCheckTimer.Start();
        }

        private void StopConnectionCheckTimer()
        {
            if (_connectionCheckTimer == null)
            {
                return;
            }

            _connectionCheckTimer.Stop();
            _connectionCheckTimer.Tick -= OnConnectionCheckTick;
            _connectionCheckTimer = null;
        }

        /// <summary>
        /// 연결에 실패했거나 재생 도중 끊긴 채널을 주기적으로 다시 연결합니다.
        /// 카메라가 과열이나 재부팅으로 잠시 내려갔다가 돌아왔을 때 앱을 재시작하지 않아도 복구됩니다.
        /// </summary>
        private void OnConnectionCheckTick(object sender, EventArgs e)
        {
            string streamUrl = StreamUrl == null ? string.Empty : StreamUrl.Trim();
            if (!IsStreaming || string.IsNullOrWhiteSpace(streamUrl))
            {
                return;
            }

            // 아직 시작 시도가 진행 중이면 겹쳐서 재시도하지 않습니다.
            if (!string.IsNullOrWhiteSpace(_pendingStreamUrl))
            {
                return;
            }

            if (_session != null && _session.IsPlaying)
            {
                return;
            }

            if (_session != null)
            {
                AppendRtspVideoLog(
                    "DROPPED",
                    "RTSP stream stopped playing. Reconnecting. Url=" + MaskRtspUrl(streamUrl));
                SetVideoPlaceholderText("카메라 영상 끊김 - 재연결 시도 중");
            }

            // 죽은 세션을 정리해야 RestartSession의 "같은 URL이면 유지" 조건을 통과해 재연결됩니다.
            StopSession();
            RestartSession();
        }

        private void RestartSession()
        {
            if (_videoHandle == IntPtr.Zero)
            {
                return;
            }

            string streamUrl = StreamUrl == null ? string.Empty : StreamUrl.Trim();
            if (!IsStreaming || string.IsNullOrWhiteSpace(streamUrl))
            {
                CancelPendingStart();
                StopSession();
                ToolTip = "RTSP 스트림 URL이 설정되지 않았습니다.";
                SetVideoPlaceholderText("카메라 RTSP 주소가 설정되지 않았습니다");
                return;
            }

            if (_session != null && string.Equals(_activeStreamUrl, streamUrl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_pendingStreamUrl) &&
                string.Equals(_pendingStreamUrl, streamUrl, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            int sequence = Interlocked.Increment(ref _startSequence);
            _pendingStreamUrl = streamUrl;
            StopSession();
            ToolTip = "RTSP 스트림 연결 중";
            SetVideoPlaceholderText("카메라 연결 중...");
            AppendRtspVideoLog("START", "RTSP stream start requested. Url=" + MaskRtspUrl(streamUrl));
            VlcStartRequest request = new VlcStartRequest();
            request.Host = this;
            request.Sequence = sequence;
            request.StreamUrl = streamUrl;
            request.VideoHandle = _videoHandle;
            request.FrameWidth = FrameWidth;
            request.FrameHeight = FrameHeight;
            ThreadPool.QueueUserWorkItem(StartSessionOnWorker, request);
        }

        private static void StartSessionOnWorker(object state)
        {
            VlcStartRequest request = state as VlcStartRequest;
            if (request == null || request.Host == null)
            {
                return;
            }

            VlcVideoSession session = null;
            Exception error = null;
            try
            {
                session = VlcVideoSession.Start(request.StreamUrl, request.VideoHandle, request.FrameWidth, request.FrameHeight);

                // Play()는 비동기라 연결 실패와 성공을 구분해 주지 않습니다.
                // 실제 재생이 시작되는지 확인해야 카메라 장애를 "재생중"으로 잘못 기록하지 않습니다.
                if (!session.WaitUntilPlaying(PlaybackConfirmTimeoutMilliseconds, PlaybackConfirmPollMilliseconds))
                {
                    error = new TimeoutException(
                        "RTSP 서버가 응답하지 않아 " +
                        (PlaybackConfirmTimeoutMilliseconds / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture) +
                        "초 안에 영상 재생이 시작되지 않았습니다. 카메라 전원과 네트워크 연결을 확인하십시오.");
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }

            try
            {
                request.Host.Dispatcher.BeginInvoke(
                    new Action(delegate { request.Host.CompleteSessionStart(request, session, error); }));
            }
            catch
            {
                if (session != null)
                {
                    session.Dispose();
                }
            }
        }

        private void CompleteSessionStart(VlcStartRequest request, VlcVideoSession session, Exception error)
        {
            if (request == null || request.Sequence != _startSequence || request.VideoHandle != _videoHandle)
            {
                if (session != null)
                {
                    session.Dispose();
                }

                return;
            }

            _pendingStreamUrl = string.Empty;
            if (error != null)
            {
                if (session != null)
                {
                    session.Dispose();
                }

                ToolTip = "카메라 연결 실패 (" + ConnectionCheckIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                          "초마다 자동 재시도): " + error.Message;
                SetVideoPlaceholderText("카메라 영상 없음 - 재연결 시도 중");
                AppendRtspVideoLog("FAIL", "RTSP stream start failed. Url=" + MaskRtspUrl(request.StreamUrl) + ", Error=" + error.Message);
                UpdateReferenceOverlay();
                return;
            }

            _session = session;
            _activeStreamUrl = request.StreamUrl;
            _session.SetAspectRatio(FrameWidth, FrameHeight);
            ToolTip = "RTSP 스트림 재생중";
            // 영상이 실제로 나오는 상태이므로 안내 문구는 비웁니다(영상에 가려지지만 잔상 방지).
            SetVideoPlaceholderText(string.Empty);
            AppendRtspVideoLog("SUCCESS", "RTSP stream started. Url=" + MaskRtspUrl(request.StreamUrl));
            UpdateReferenceOverlay();
        }

        private void CancelPendingStart()
        {
            Interlocked.Increment(ref _startSequence);
            _pendingStreamUrl = string.Empty;
        }

        private void StopSession()
        {
            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }

            _activeStreamUrl = string.Empty;
        }

        private static void AppendRtspVideoLog(string stage, string message)
        {
            try
            {
                string logPath = ApplicationLogFileResolver.GetLogFilePath(AppContext.BaseDirectory, "rtsp-video-host");
                File.AppendAllText(
                    logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + stage + "] " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static string MaskRtspUrl(string streamUrl)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
            {
                return string.Empty;
            }

            Uri uri;
            if (!Uri.TryCreate(streamUrl, UriKind.Absolute, out uri) || string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                return streamUrl;
            }

            string authority = string.IsNullOrWhiteSpace(uri.Authority) ? uri.Host : uri.Authority;
            int atIndex = authority.IndexOf('@');
            if (atIndex < 0)
            {
                authority = "***@" + authority;
            }
            else
            {
                authority = "***@" + authority.Substring(atIndex + 1);
            }

            return uri.Scheme + "://" + authority + uri.PathAndQuery;
        }

        private void UpdateChildWindowLayout(int width, int height)
        {
            Rect videoArea = BuildVideoArea(width, height);
            if (_videoHandle != IntPtr.Zero)
            {
                MoveWindow(
                    _videoHandle,
                    Math.Max(0, (int)Math.Round(videoArea.X)),
                    Math.Max(0, (int)Math.Round(videoArea.Y)),
                    Math.Max(1, (int)Math.Round(videoArea.Width)),
                    Math.Max(1, (int)Math.Round(videoArea.Height)),
                    true);
            }

            if (_referenceOverlayHandle != IntPtr.Zero)
            {
                Rect overlayArea = BuildReferenceOverlayArea(videoArea);
                SetWindowPos(
                    _referenceOverlayHandle,
                    IntPtr.Zero,
                    Math.Max(0, (int)Math.Round(overlayArea.X)),
                    Math.Max(0, (int)Math.Round(overlayArea.Y)),
                    Math.Max(1, (int)Math.Round(overlayArea.Width)),
                    Math.Max(1, (int)Math.Round(overlayArea.Height)),
                    SwpNoActivate | SwpShowWindow);
            }
        }

        /// <summary>
        /// 영상 표시 영역 밖의 부모 HWND를 지정한 어두운 색으로 다시 그립니다.
        /// </summary>
        private static void PaintVideoBackground(IntPtr hwnd, IntPtr deviceContext)
        {
            if (deviceContext == IntPtr.Zero)
            {
                return;
            }

            RectNative clientRect;
            if (!GetClientRect(hwnd, out clientRect))
            {
                return;
            }

            IntPtr brush = CreateSolidBrush(VideoBackgroundColor);
            if (brush == IntPtr.Zero)
            {
                return;
            }

            try
            {
                FillRect(deviceContext, ref clientRect, brush);
            }
            finally
            {
                DeleteObject(brush);
            }
        }

        /// <summary>
        /// WM_COMMAND wParam의 상위 16비트에 있는 STATIC 알림 코드를 반환합니다.
        /// </summary>
        private static int GetHighWord(IntPtr value)
        {
            long numericValue = value.ToInt64();
            return (int)((numericValue >> 16) & 0xFFFF);
        }

        private void UpdateReferenceOverlay()
        {
            if (_referenceOverlayHandle == IntPtr.Zero)
            {
                return;
            }

            // HwndHost 위에는 일반 WPF 오버레이가 올라오지 않으므로,
            // 기준 이미지를 같은 부모 HWND의 네이티브 STATIC 컨트롤로 직접 표시합니다.
            string filePath = ResolveReferenceImagePath();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                ReleaseReferenceBitmap();
                ShowWindow(_referenceOverlayHandle, SwHide);
                return;
            }

            Rect videoArea = BuildVideoArea(Math.Max(1, (int)ActualWidth), Math.Max(1, (int)ActualHeight));
            Rect overlayArea = BuildReferenceOverlayArea(videoArea);
            int overlayWidth = Math.Max(1, (int)Math.Round(overlayArea.Width));
            int overlayHeight = Math.Max(1, (int)Math.Round(overlayArea.Height));
            IntPtr bitmapHandle = CreateReferenceBitmap(filePath, overlayWidth, overlayHeight);
            if (bitmapHandle == IntPtr.Zero)
            {
                ReleaseReferenceBitmap();
                ShowWindow(_referenceOverlayHandle, SwHide);
                return;
            }

            IntPtr previousHandle = SendMessage(_referenceOverlayHandle, StmSetImage, new IntPtr(ImageBitmap), bitmapHandle);
            if (previousHandle != IntPtr.Zero && previousHandle != _referenceBitmapHandle)
            {
                DeleteObject(previousHandle);
            }

            ReleaseReferenceBitmap();
            _referenceBitmapHandle = bitmapHandle;
            ShowWindow(_referenceOverlayHandle, SwShow);
            SetWindowPos(
                _referenceOverlayHandle,
                IntPtr.Zero,
                Math.Max(0, (int)Math.Round(overlayArea.X)),
                Math.Max(0, (int)Math.Round(overlayArea.Y)),
                overlayWidth,
                overlayHeight,
                SwpNoActivate | SwpShowWindow);
        }

        private string ResolveReferenceImagePath()
        {
            string filePath = ReferenceImagePath == null ? string.Empty : ReferenceImagePath.Trim();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return string.Empty;
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
            return pathSettings.ResolveImageFilePath(filePath);
        }

        private IntPtr CreateReferenceBitmap(string filePath, int width, int height)
        {
            try
            {
                BitmapSource source = LoadReferenceBitmapSource(filePath);
                if (source == null)
                {
                    return IntPtr.Zero;
                }

                RenderTargetBitmap renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                DrawingVisual visual = new DrawingVisual();
                using (DrawingContext context = visual.RenderOpen())
                {
                    Rect fullArea = new Rect(0, 0, width, height);
                    context.DrawRectangle(new SolidColorBrush(Color.FromRgb(18, 24, 32)), null, fullArea);
                    Rect destination = BuildUniformRectangle(source.PixelWidth, source.PixelHeight, width, height);
                    context.DrawImage(source, destination);
                }

                renderBitmap.Render(visual);
                FormatConvertedBitmap converted = new FormatConvertedBitmap(renderBitmap, PixelFormats.Bgra32, null, 0);
                int stride = width * 4;
                byte[] pixels = new byte[stride * height];
                converted.CopyPixels(pixels, stride, 0);
                return CreateDeviceIndependentBitmap(pixels, width, height);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        private BitmapSource LoadReferenceBitmapSource(string filePath)
        {
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.IgnoreColorProfile | BitmapCreateOptions.IgnoreImageCache,
                    BitmapCacheOption.OnLoad);

                if (decoder.Frames.Count <= 0)
                {
                    return null;
                }

                BitmapSource source = decoder.Frames[0];
                if (source.CanFreeze)
                {
                    source.Freeze();
                }

                return source;
            }
        }

        /// <summary>
        /// 원본 프레임의 종횡비를 유지한 표시 영역을 계산합니다.
        /// 채널별 해상도와 화면 칸의 비율이 달라도 이미지를 늘리거나 자르지 않습니다.
        /// </summary>
        private static Rect BuildUniformRectangle(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
            {
                return new Rect(0, 0, targetWidth, targetHeight);
            }

            double widthScale = (double)targetWidth / sourceWidth;
            double heightScale = (double)targetHeight / sourceHeight;
            double scale = Math.Min(widthScale, heightScale);
            double scaledWidth = Math.Floor(sourceWidth * scale);
            double scaledHeight = Math.Floor(sourceHeight * scale);
            double x = (targetWidth - scaledWidth) / 2;
            double y = (targetHeight - scaledHeight) / 2;
            return new Rect(x, y, scaledWidth, scaledHeight);
        }

        private Rect BuildVideoArea(int targetWidth, int targetHeight)
        {
            return BuildUniformRectangle(FrameWidth, FrameHeight, targetWidth, targetHeight);
        }

        private static Rect BuildReferenceOverlayArea(Rect videoArea)
        {
            int overlayWidth = Math.Max(72, (int)Math.Floor(videoArea.Width / 4));
            int overlayHeight = Math.Max(54, (int)Math.Floor(videoArea.Height / 4));
            return new Rect(videoArea.X + 6, videoArea.Y + 6, overlayWidth, overlayHeight);
        }

        private IntPtr CreateDeviceIndependentBitmap(byte[] pixels, int width, int height)
        {
            if (pixels == null || pixels.Length == 0 || width <= 0 || height <= 0)
            {
                return IntPtr.Zero;
            }

            BitmapInfo info = new BitmapInfo();
            info.Header.Size = (uint)Marshal.SizeOf(typeof(BitmapInfoHeader));
            info.Header.Width = width;
            info.Header.Height = -height;
            info.Header.Planes = 1;
            info.Header.BitCount = 32;
            info.Header.Compression = BiRgb;
            info.Header.SizeImage = (uint)pixels.Length;

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr bits;
            IntPtr bitmapHandle = CreateDIBSection(screenDc, ref info, DibRgbColors, out bits, IntPtr.Zero, 0);
            if (screenDc != IntPtr.Zero)
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }

            if (bitmapHandle == IntPtr.Zero || bits == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            Marshal.Copy(pixels, 0, bits, pixels.Length);
            return bitmapHandle;
        }

        /// <summary>
        /// WM_CTLCOLORSTATIC 응답으로 돌려줄 배경 브러시입니다.
        /// 이 브러시는 메시지 처리 후에도 Windows가 계속 사용하므로 매번 만들지 않고 보관했다가
        /// 창을 파괴할 때 한 번만 해제합니다.
        /// </summary>
        /// <summary>
        /// 영상 영역에 표시할 안내 문구를 바꿉니다.
        /// LibVLC가 실제로 그리기 시작하면 이 문구는 영상에 가려지고, 영상이 없을 때만 보입니다.
        /// 문구를 비워 두면 예전처럼 아무 정보 없는 빈 칸이 되므로 항상 현재 상태를 넣습니다.
        /// </summary>
        private void SetVideoPlaceholderText(string text)
        {
            if (_videoHandle == IntPtr.Zero)
            {
                return;
            }

            SetWindowText(_videoHandle, text ?? string.Empty);
        }

        private IntPtr EnsureVideoBackgroundBrush()
        {
            if (_videoBackgroundBrush == IntPtr.Zero)
            {
                _videoBackgroundBrush = CreateSolidBrush(VideoBackgroundColor);
            }

            return _videoBackgroundBrush;
        }

        private void ReleaseVideoBackgroundBrush()
        {
            if (_videoBackgroundBrush != IntPtr.Zero)
            {
                DeleteObject(_videoBackgroundBrush);
                _videoBackgroundBrush = IntPtr.Zero;
            }
        }

        private void ReleaseReferenceBitmap()
        {
            if (_referenceBitmapHandle != IntPtr.Zero)
            {
                DeleteObject(_referenceBitmapHandle);
                _referenceBitmapHandle = IntPtr.Zero;
            }
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr BeginPaint(IntPtr hwnd, out PaintStruct paintStruct);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EndPaint(IntPtr hwnd, ref PaintStruct paintStruct);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hwnd, out RectNative rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int FillRect(IntPtr deviceContext, ref RectNative rectangle, IntPtr brush);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo bitmapInfo, int usage, out IntPtr bits, IntPtr section, int offset);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateSolidBrush(uint color);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern uint SetBkColor(IntPtr deviceContext, uint color);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern uint SetTextColor(IntPtr deviceContext, uint color);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hwnd, string text);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr objectHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public int Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ClrUsed;
            public uint ClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RectNative
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PaintStruct
        {
            public IntPtr DeviceContext;
            public int Erase;
            public RectNative PaintRectangle;
            public int Restore;
            public int IncrementalUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] Reserved;
        }

        /// <summary>
        /// LibVLCSharp를 직접 참조하지 않고 VLAD 배포 DLL을 리플렉션으로 로드해서 영상 재생 세션을 관리합니다.
        /// </summary>
        private sealed class VlcVideoSession : IDisposable
        {
            private static readonly object LoadLock = new object();
            private static Assembly _libVlcSharpAssembly;
            private static Type _coreType;
            private static Type _libVlcType;
            private static Type _mediaType;
            private static Type _mediaPlayerType;
            private static Type _fromTypeType;
            private static string _baseDirectory;
            private static string _nativeDirectory;
            private static string _pluginDirectory;
            private static object _sharedLibVlc;
            private static bool _isLoaded;
            private static bool _assemblyResolverAttached;

            private object _libVlc;
            private object _media;
            private object _mediaPlayer;

            private VlcVideoSession()
            {
            }

            public static VlcVideoSession Start(string streamUrl, IntPtr videoHandle, int frameWidth, int frameHeight)
            {
                EnsureLoaded();

                VlcVideoSession session = new VlcVideoSession();
                session.StartCore(streamUrl, videoHandle, frameWidth, frameHeight);
                return session;
            }

            public static void WarmUp()
            {
                EnsureLoaded();
            }

            /// <summary>
            /// LibVLC가 실제로 영상을 재생하고 있는지 확인합니다.
            /// Play()의 반환값은 재생 시작 여부가 아니라 재생 요청 접수 여부만 알려주므로,
            /// 카메라 장애 판정에는 이 속성을 사용해야 합니다.
            /// </summary>
            public bool IsPlaying
            {
                get
                {
                    if (_mediaPlayer == null)
                    {
                        return false;
                    }

                    try
                    {
                        PropertyInfo isPlayingProperty = _mediaPlayerType.GetProperty("IsPlaying");
                        if (isPlayingProperty == null)
                        {
                            // 상태를 확인할 수 없는 배포 버전에서는 기존처럼 재생중으로 간주합니다.
                            return true;
                        }

                        object value = isPlayingProperty.GetValue(_mediaPlayer, null);
                        return value is bool && (bool)value;
                    }
                    catch
                    {
                        return true;
                    }
                }
            }

            /// <summary>
            /// 재생이 실제로 시작될 때까지 기다립니다. UI 스레드가 아닌 작업 스레드에서만 호출합니다.
            /// </summary>
            public bool WaitUntilPlaying(int timeoutMilliseconds, int pollMilliseconds)
            {
                int waitedMilliseconds = 0;
                while (waitedMilliseconds < timeoutMilliseconds)
                {
                    if (IsPlaying)
                    {
                        return true;
                    }

                    Thread.Sleep(pollMilliseconds);
                    waitedMilliseconds += pollMilliseconds;
                }

                return IsPlaying;
            }

            /// <summary>
            /// LibVLC가 네이티브 영상 창 전체를 임의로 채우면서 프레임을 늘리지 않도록 비율을 지정합니다.
            /// 해당 속성을 지원하지 않는 구버전 LibVLCSharp도 있으므로, 없는 경우에는 영상 창의 크기 조정만 사용합니다.
            /// </summary>
            public void SetAspectRatio(int frameWidth, int frameHeight)
            {
                ApplyAspectRatio(_mediaPlayer, frameWidth, frameHeight);
            }

            public void Dispose()
            {
                StopPlayer(_mediaPlayer);
                DisposeObject(_mediaPlayer);
                DisposeObject(_media);
                _mediaPlayer = null;
                _media = null;
                _libVlc = null;
            }

            private static void EnsureLoaded()
            {
                if (_isLoaded)
                {
                    return;
                }

                lock (LoadLock)
                {
                    if (_isLoaded)
                    {
                        return;
                    }

                    ResolveRuntimeDirectories();
                    if (!IsLibVlcRuntimeAvailable(_baseDirectory, _nativeDirectory, _pluginDirectory))
                    {
                        throw new FileNotFoundException("LibVLC RTSP 런타임을 찾을 수 없습니다. Native\\VLAD 폴더의 LibVLCSharp.dll, libvlc.dll, libvlccore.dll, plugins 폴더를 확인하세요.");
                    }

                    ApplyNativeSearchPath();
                    AttachAssemblyResolver();

                    _libVlcSharpAssembly = Assembly.UnsafeLoadFrom(Path.Combine(_baseDirectory, "LibVLCSharp.dll"));
                    _coreType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.Core", true);
                    _libVlcType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.LibVLC", true);
                    _mediaType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.Media", true);
                    _mediaPlayerType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.MediaPlayer", true);
                    _fromTypeType = _libVlcSharpAssembly.GetType("LibVLCSharp.Shared.FromType", true);
                    InitializeCore();
                    _sharedLibVlc = CreateLibVlc();
                    _isLoaded = true;
                }
            }

            private static void ResolveRuntimeDirectories()
            {
                string projectRoot = ProjectDataRootResolver.Resolve(AppContext.BaseDirectory);
                string vladDirectory = Path.Combine(projectRoot, "Native", "VLAD");
                string runtimeDataDirectory = Path.Combine(projectRoot, "RuntimeData", "Native", "LibVLC");

                if (IsLibVlcRuntimeAvailable(vladDirectory, vladDirectory, Path.Combine(vladDirectory, "plugins")))
                {
                    _baseDirectory = vladDirectory;
                    _nativeDirectory = vladDirectory;
                    _pluginDirectory = Path.Combine(vladDirectory, "plugins");
                    return;
                }

                _baseDirectory = runtimeDataDirectory;
                _nativeDirectory = Path.Combine(runtimeDataDirectory, "win-x64");
                _pluginDirectory = Path.Combine(_nativeDirectory, "plugins");
            }

            private static void ApplyNativeSearchPath()
            {
                PrependProcessPath(_baseDirectory);
                PrependProcessPath(_nativeDirectory);
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", _pluginDirectory);
                SetDllDirectory(_nativeDirectory);
            }

            private static void PrependProcessPath(string directoryPath)
            {
                if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                {
                    return;
                }

                string pathValue = Environment.GetEnvironmentVariable("PATH");
                if (string.IsNullOrWhiteSpace(pathValue))
                {
                    Environment.SetEnvironmentVariable("PATH", directoryPath);
                    return;
                }

                string[] pathItems = pathValue.Split(Path.PathSeparator);
                foreach (string pathItem in pathItems)
                {
                    if (string.Equals(pathItem.TrimEnd(Path.DirectorySeparatorChar), directoryPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                Environment.SetEnvironmentVariable("PATH", directoryPath + Path.PathSeparator + pathValue);
            }

            private static void AttachAssemblyResolver()
            {
                if (_assemblyResolverAttached)
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += ResolveManagedAssembly;
                _assemblyResolverAttached = true;
            }

            private static Assembly ResolveManagedAssembly(object sender, ResolveEventArgs args)
            {
                string assemblyName = new AssemblyName(args.Name).Name + ".dll";
                string candidatePath = Path.Combine(_baseDirectory, assemblyName);
                if (File.Exists(candidatePath))
                {
                    return Assembly.LoadFrom(candidatePath);
                }

                return null;
            }

            private static void InitializeCore()
            {
                MethodInfo initializeMethod = _coreType.GetMethod("Initialize", new Type[] { typeof(string) });
                if (initializeMethod == null)
                {
                    throw new MissingMethodException("LibVLCSharp.Shared.Core.Initialize(string)을 찾을 수 없습니다.");
                }

                initializeMethod.Invoke(null, new object[] { _nativeDirectory });
            }

            private static object CreateLibVlc()
            {
                ConstructorInfo constructor = _libVlcType.GetConstructor(new Type[] { typeof(string[]) });
                if (constructor == null)
                {
                    throw new MissingMethodException("LibVLC(string[]) 생성자를 찾을 수 없습니다.");
                }

                string[] options = new string[]
                {
                    "--no-video-title-show",
                    "--rtsp-tcp",
                    "--rtsp-frame-buffer-size=5000000",
                    // 하드웨어 디코딩(--avcodec-hw=any)으로 6채널 영상 끊김은 개선됐지만, 앱 시작 시
                    // 백그라운드 워밍업(WarmUpEngine -> CreateLibVlc)이 메인 스레드의 VLAD CUDA/TensorFlow
                    // 초기화와 동시에 GPU 관련 네이티브 작업을 시도하게 되어 VLAD_Custom_Registration이
                    // 멈추는 문제와 시점이 일치했습니다. 앱이 아예 안 뜨는 것이 더 치명적이라 되돌립니다.
                    "--avcodec-hw=none",
                    "--network-caching=300",
                    "--live-caching=300",
                    "--no-audio",
                    "--drop-late-frames",
                    "--skip-frames"
                };
                return constructor.Invoke(new object[] { options });
            }

            private static object CreateMedia(object libVlc, string streamUrl)
            {
                ConstructorInfo constructor = _mediaType.GetConstructor(new Type[] { _libVlcType, typeof(string), _fromTypeType, typeof(string[]) });
                if (constructor == null)
                {
                    throw new MissingMethodException("Media(LibVLC, string, FromType, string[]) 생성자를 찾을 수 없습니다.");
                }

                object fromLocation = Enum.Parse(_fromTypeType, "FromLocation");
                return constructor.Invoke(new object[] { libVlc, streamUrl, fromLocation, new string[0] });
            }

            private static object CreateMediaPlayer(object libVlc)
            {
                ConstructorInfo constructor = _mediaPlayerType.GetConstructor(new Type[] { _libVlcType });
                if (constructor == null)
                {
                    throw new MissingMethodException("MediaPlayer(LibVLC) 생성자를 찾을 수 없습니다.");
                }

                return constructor.Invoke(new object[] { libVlc });
            }

            private static void ApplyAspectRatio(object mediaPlayer, int frameWidth, int frameHeight)
            {
                if (mediaPlayer == null || frameWidth <= 0 || frameHeight <= 0)
                {
                    return;
                }

                try
                {
                    PropertyInfo aspectRatioProperty = _mediaPlayerType.GetProperty("AspectRatio");
                    if (aspectRatioProperty != null && aspectRatioProperty.CanWrite)
                    {
                        aspectRatioProperty.SetValue(
                            mediaPlayer,
                            frameWidth.ToString() + ":" + frameHeight.ToString(),
                            null);
                    }
                }
                catch
                {
                    // LibVLCSharp 배포 버전에 속성이 없거나 설정을 거부해도 영상 재생 자체는 유지합니다.
                }
            }

            private static void AddMediaOption(object media, string option)
            {
                MethodInfo addOptionMethod = _mediaType.GetMethod("AddOption", new Type[] { typeof(string) });
                if (addOptionMethod != null)
                {
                    addOptionMethod.Invoke(media, new object[] { option });
                }
            }

            private static void SetVideoHandle(object mediaPlayer, IntPtr videoHandle)
            {
                PropertyInfo hwndProperty = _mediaPlayerType.GetProperty("Hwnd");
                if (hwndProperty == null)
                {
                    throw new MissingMemberException(_mediaPlayerType.FullName, "Hwnd");
                }

                hwndProperty.SetValue(mediaPlayer, videoHandle, null);
            }

            private static bool Play(object mediaPlayer, object media)
            {
                MethodInfo playMethod = _mediaPlayerType.GetMethod("Play", new Type[] { _mediaType });
                if (playMethod == null)
                {
                    throw new MissingMethodException("MediaPlayer.Play(Media)를 찾을 수 없습니다.");
                }

                object result = playMethod.Invoke(mediaPlayer, new object[] { media });
                return result is bool && (bool)result;
            }

            private static void StopPlayer(object mediaPlayer)
            {
                if (mediaPlayer == null)
                {
                    return;
                }

                try
                {
                    MethodInfo stopMethod = _mediaPlayerType.GetMethod("Stop", Type.EmptyTypes);
                    if (stopMethod != null)
                    {
                        stopMethod.Invoke(mediaPlayer, null);
                    }
                }
                catch
                {
                }
            }

            private static void DisposeObject(object instance)
            {
                IDisposable disposable = instance as IDisposable;
                if (disposable == null)
                {
                    return;
                }

                try
                {
                    disposable.Dispose();
                }
                catch
                {
                }
            }

            private static bool IsLibVlcRuntimeAvailable(string baseDirectory, string nativeDirectory, string pluginDirectory)
            {
                return File.Exists(Path.Combine(baseDirectory, "LibVLCSharp.dll"))
                       && File.Exists(Path.Combine(nativeDirectory, "libvlc.dll"))
                       && File.Exists(Path.Combine(nativeDirectory, "libvlccore.dll"))
                       && Directory.Exists(pluginDirectory);
            }

            private void StartCore(string streamUrl, IntPtr videoHandle, int frameWidth, int frameHeight)
            {
                // 하나의 LibVLC 엔진을 공유하고 채널별 MediaPlayer만 분리합니다.
                // 카메라마다 LibVLC 엔진을 새로 만들 때 발생하던 플러그인/스레드 중복 부하를 줄입니다.
                _libVlc = _sharedLibVlc;
                _media = CreateMedia(_libVlc, streamUrl);
                AddMediaOption(_media, ":rtsp-tcp");
                AddMediaOption(_media, ":rtsp-frame-buffer-size=5000000");
                AddMediaOption(_media, ":avcodec-hw=none");
                AddMediaOption(_media, ":network-caching=300");
                AddMediaOption(_media, ":live-caching=300");
                AddMediaOption(_media, ":no-audio");

                _mediaPlayer = CreateMediaPlayer(_libVlc);
                ApplyAspectRatio(_mediaPlayer, frameWidth, frameHeight);
                SetVideoHandle(_mediaPlayer, videoHandle);
                if (!Play(_mediaPlayer, _media))
                {
                    throw new InvalidOperationException("LibVLC RTSP 재생 시작에 실패했습니다.");
                }
            }

            [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern bool SetDllDirectory(string lpPathName);
        }

        private sealed class VlcStartRequest
        {
            public RtspVideoHost Host;
            public int Sequence;
            public string StreamUrl;
            public IntPtr VideoHandle;
            public int FrameWidth;
            public int FrameHeight;
        }
    }
}
