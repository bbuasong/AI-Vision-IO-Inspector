using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        /// LibVLC 네이티브 영상 영역 또는 기준 이미지 오버레이를 두 번 클릭했을 때 발생합니다.
        /// HwndHost 내부 HWND의 마우스 입력은 일반 WPF MouseBinding으로 전달되지 않으므로 별도 이벤트를 제공합니다.
        /// </summary>
        public static readonly RoutedEvent VideoDoubleClickEvent =
            EventManager.RegisterRoutedEvent(
                "VideoDoubleClick",
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
        private const int StnDoubleClick = 1;
        private const int ImageBitmap = 0;
        private const int StmSetImage = 0x0172;
        private const int WmEraseBkgnd = 0x0014;
        private const int WmPaint = 0x000F;
        private const int SwHide = 0;
        private const int SwShow = 5;
        private const int DibRgbColors = 0;
        private const int BiRgb = 0;
        private const int SwpNoActivate = 0x0010;
        private const int SwpShowWindow = 0x0040;
        // COLORREF는 0x00BBGGRR 순서입니다. 화면 여백은 #0A1016으로 고정합니다.
        private const uint VideoBackgroundColor = 0x0016100A;

        private IntPtr _childHandle;
        private IntPtr _videoHandle;
        private IntPtr _referenceOverlayHandle;
        private IntPtr _referenceBitmapHandle;
        private VlcVideoSession _session;
        private string _activeStreamUrl;
        private string _pendingStreamUrl;
        private int _startSequence;

        public RtspVideoHost()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// 네이티브 영상 영역의 두 번 클릭을 WPF 상위 화면으로 전달합니다.
        /// </summary>
        public event RoutedEventHandler VideoDoubleClick
        {
            add { AddHandler(VideoDoubleClickEvent, value); }
            remove { RemoveHandler(VideoDoubleClickEvent, value); }
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

            _videoHandle = CreateWindowEx(
                0,
                "STATIC",
                string.Empty,
                WsChild | WsVisible | WsClipSiblings | WsClipChildren | SsNotify,
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
            if (message == WmCommand && GetHighWord(wParam) == StnDoubleClick)
            {
                if (lParam == _videoHandle || lParam == _referenceOverlayHandle)
                {
                    RaiseEvent(new RoutedEventArgs(VideoDoubleClickEvent, this));
                    handled = true;
                    return IntPtr.Zero;
                }
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
            CancelPendingStart();
            StopSession();
            ReleaseReferenceBitmap();

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
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelPendingStart();
            StopSession();
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

                ToolTip = "RTSP 스트림 재생 실패: " + error.Message;
                AppendRtspVideoLog("FAIL", "RTSP stream start failed. Url=" + MaskRtspUrl(request.StreamUrl) + ", Error=" + error.Message);
                UpdateReferenceOverlay();
                return;
            }

            _session = session;
            _activeStreamUrl = request.StreamUrl;
            _session.SetAspectRatio(FrameWidth, FrameHeight);
            ToolTip = "RTSP 스트림 재생중";
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
                string projectRoot = ProjectDataRootResolver.Resolve(AppContext.BaseDirectory);
                string logDirectory = Path.Combine(projectRoot, "DB", "Logs");
                Directory.CreateDirectory(logDirectory);
                string logPath = Path.Combine(logDirectory, "rtsp-video-host.log");
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
