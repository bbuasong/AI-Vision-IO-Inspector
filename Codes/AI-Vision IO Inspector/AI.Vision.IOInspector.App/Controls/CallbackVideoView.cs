using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using AI.Vision.IOInspector.Vision.Services;

namespace AI.Vision.IOInspector.App.Controls
{
    /// <summary>
    /// VLAD RTSP 콜백으로 들어온 프레임을 화면에 그립니다.
    ///
    /// <para>
    /// LibVLC는 영상을 네이티브 창에 직접 그려 주었기 때문에 프레임이 우리 손에 오지 않았습니다.
    /// 그래서 그 위에 크롭한 그림을 얹을 수가 없었습니다.
    /// 이 컨트롤은 콜백으로 받은 프레임을 우리가 직접 그리므로, 중간에 크롭을 끼워 넣을 수 있습니다.
    /// </para>
    ///
    /// <para>
    /// 평범한 WPF <see cref="Image"/>를 쓰기 때문에 위에 다른 요소를 겹쳐 놓을 수 있습니다.
    /// 네이티브 창을 쓰던 시절에는 기준 이미지를 별도 창으로 띄워야 했는데 그럴 필요가 없습니다.
    /// </para>
    /// </summary>
    public class CallbackVideoView : Image
    {
        public static readonly DependencyProperty MonitorIndexProperty =
            DependencyProperty.Register(
                "MonitorIndex",
                typeof(int),
                typeof(CallbackVideoView),
                new PropertyMetadata(-1, OnMonitorIndexChanged));

        public static readonly DependencyProperty IsStreamingProperty =
            DependencyProperty.Register(
                "IsStreaming",
                typeof(bool),
                typeof(CallbackVideoView),
                new PropertyMetadata(false, OnIsStreamingChanged));

        public static readonly DependencyProperty UseCropProperty =
            DependencyProperty.Register(
                "UseCrop",
                typeof(bool),
                typeof(CallbackVideoView),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CropIntervalMillisecondsProperty =
            DependencyProperty.Register(
                "CropIntervalMilliseconds",
                typeof(int),
                typeof(CallbackVideoView),
                new PropertyMetadata(0, OnCropIntervalChanged));

        /// <summary>
        /// 지금 이 칸에 영상이 실제로 흐르고 있는지입니다.
        ///
        /// <para>
        /// 화면 아래 상태 문구를 상황에 맞게 두기 위한 값입니다. 채널을 붙였다는 것과
        /// 프레임이 실제로 들어온다는 것은 다른 이야기라, 그리는 쪽만 알 수 있습니다.
        /// 바깥에서는 OneWayToSource 로 받아 갑니다.
        /// </para>
        /// </summary>
        public static readonly DependencyProperty HasLiveFrameProperty =
            DependencyProperty.Register(
                "HasLiveFrame",
                typeof(bool),
                typeof(CallbackVideoView),
                new PropertyMetadata(false));

        /// <summary>
        /// 프레임이 이 시간 넘게 오지 않으면 흐르지 않는 것으로 봅니다.
        ///
        /// <para>
        /// 여섯 채널일 때 한 채널이 초당 5장 남짓이라 한두 장 걸러도 1초를 넘지 않습니다.
        /// 너무 짧게 잡으면 문구가 깜빡이므로 넉넉히 둡니다.
        /// </para>
        /// </summary>
        private const double LiveFrameTimeoutSeconds = 2.0;

        private CallbackFrameCropStage _cropStage;
        private DateTime _lastDrawnAtUtc;

        /// <summary>이 컨트롤 전용 프레임 사본입니다. 크기가 같으면 계속 돌려씁니다.</summary>
        private byte[] _displayBuffer;
        private WriteableBitmap _bitmap;
        private DateTime _lastCapturedAt;
        private bool _renderingHooked;
        private int _bitmapWidth;
        private int _bitmapHeight;

        public CallbackVideoView()
        {
            // 원본 비율을 지키고 남는 자리는 비워 둡니다.
            // 늘려서 채우면 4:3, 6:5 카메라가 찌그러져 보입니다.
            Stretch = Stretch.Uniform;
            _lastCapturedAt = DateTime.MinValue;
            _lastDrawnAtUtc = DateTime.MinValue;

            Loaded += OnLoaded;

            // 실제로 보일 때만 그립니다.
            //
            // 검사 화면은 탭을 옮겨도 Visibility 만 바뀌고 트리에서 빠지지 않아, 통계나 이력을
            // 보는 동안에도 여섯 채널이 초당 수십 MB 를 복사하고 SAM 크롭까지 계속 돌았습니다.
            // 크롭 한 번이 1 초 가까이 걸려 GPU 가 쉬지 못했고, 검사 추론과도 다투었습니다.
            // 안 보이는 화면을 그릴 까닭이 없습니다. 다시 보이면 곧바로 재개됩니다.
            IsVisibleChanged += OnIsVisibleChanged;
            Unloaded += OnUnloaded;
        }

        /// <summary>어느 카메라의 프레임을 그릴지입니다. RtspMonitorIndexPolicy의 번호를 씁니다.</summary>
        public int MonitorIndex
        {
            get { return (int)GetValue(MonitorIndexProperty); }
            set { SetValue(MonitorIndexProperty, value); }
        }

        /// <summary>꺼 두면 그리기를 멈춥니다. 검사 중처럼 정지 화면을 보여줄 때 씁니다.</summary>
        public bool IsStreaming
        {
            get { return (bool)GetValue(IsStreamingProperty); }
            set { SetValue(IsStreamingProperty, value); }
        }

        /// <summary>영상이 실제로 흐르고 있으면 참입니다. 읽기 전용으로 쓰십시오.</summary>
        public bool HasLiveFrame
        {
            get { return (bool)GetValue(HasLiveFrameProperty); }
            set { SetValue(HasLiveFrameProperty, value); }
        }

        /// <summary>제품 영역만 잘라 크게 보일지입니다.</summary>
        public bool UseCrop
        {
            get { return (bool)GetValue(UseCropProperty); }
            set { SetValue(UseCropProperty, value); }
        }

        /// <summary>크롭을 시도할 최소 간격입니다. 0이면 프레임마다 시도합니다.</summary>
        public int CropIntervalMilliseconds
        {
            get { return (int)GetValue(CropIntervalMillisecondsProperty); }
            set { SetValue(CropIntervalMillisecondsProperty, value); }
        }

        private static void OnCropIntervalChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            CallbackVideoView view = dependencyObject as CallbackVideoView;
            if (view != null && view._cropStage != null)
            {
                view._cropStage.MinimumIntervalMilliseconds = view.CropIntervalMilliseconds;
            }
        }

        private static void OnMonitorIndexChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            CallbackVideoView view = dependencyObject as CallbackVideoView;
            if (view != null)
            {
                view._lastCapturedAt = DateTime.MinValue;
                view.UpdateRenderingHook();
            }
        }

        private static void OnIsStreamingChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            CallbackVideoView view = dependencyObject as CallbackVideoView;
            if (view != null)
            {
                view.UpdateRenderingHook();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateRenderingHook();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateRenderingHook();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            HookRendering(false);

            if (_cropStage != null)
            {
                _cropStage.Dispose();
                _cropStage = null;
            }
        }

        private CallbackFrameCropStage EnsureCropStage()
        {
            if (_cropStage == null)
            {
                _cropStage = new CallbackFrameCropStage(MonitorIndex);
                _cropStage.MinimumIntervalMilliseconds = CropIntervalMilliseconds;
            }

            return _cropStage;
        }

        private void UpdateRenderingHook()
        {
            bool shouldRender = IsLoaded && IsVisible && IsStreaming && MonitorIndex >= 0;
            HookRendering(shouldRender);
        }

        /// <summary>
        /// WPF가 화면을 그리는 시점에 맞춰 갱신합니다.
        ///
        /// <para>
        /// 별도 타이머를 두면 WPF가 그리는 시점과 어긋나 한 프레임이 반쯤 그려진 채로
        /// 보일 수 있습니다. 이 시점에 맞추면 그런 일이 없습니다.
        /// </para>
        /// </summary>
        private void HookRendering(bool hook)
        {
            if (hook == _renderingHooked)
            {
                return;
            }

            if (hook)
            {
                CompositionTarget.Rendering += OnRendering;
            }
            else
            {
                CompositionTarget.Rendering -= OnRendering;
            }

            _renderingHooked = hook;

            if (!hook)
            {
                // 그리기를 멈추면 흐르지 않는 것입니다. 멈춘 채로 참이 남아 있으면 안 됩니다.
                _lastDrawnAtUtc = DateTime.MinValue;
                HasLiveFrame = false;
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            try
            {
                DrawLatestFrame();
                UpdateLiveFrameState();
            }
            catch (Exception ex)
            {
                // 한 프레임을 못 그렸다고 화면 전체가 멈추면 안 됩니다.
                System.Diagnostics.Debug.WriteLine(
                    "콜백 프레임 그리기 실패. MonitorIndex=" +
                    MonitorIndex.ToString(CultureInfo.InvariantCulture) + ", " + ex.Message);
            }
        }

        private void DrawLatestFrame()
        {
            int frameWidth;
            int frameHeight;
            DateTime capturedAt;

            // 지난번에 그린 것보다 새 프레임이 없으면 아무것도 하지 않습니다.
            // 받은 것은 이 컨트롤 전용 사본이라 이후 어느 단계에서도 덮어써질 일이 없습니다.
            if (!VLAD_Ops_RTSP.TryCopyLatestFrameForDisplay(
                    MonitorIndex, _lastCapturedAt, ref _displayBuffer, out frameWidth, out frameHeight, out capturedAt))
            {
                return;
            }

            byte[] bgrPixels = _displayBuffer;
            int expectedLength = checked(frameWidth * frameHeight * 3);
            if (bgrPixels == null || bgrPixels.Length < expectedLength)
            {
                // 크기가 어긋나면 그 프레임은 건너뜁니다. 잘못 읽으면 화면이 깨집니다.
                return;
            }

            // 제품 영역만 잘라 크게 보이도록 합니다.
            // 자르지 못하면 원본을 그대로 그립니다. 화면이 비는 편보다 낫습니다.
            if (UseCrop)
            {
                byte[] croppedPixels;
                int croppedWidth;
                int croppedHeight;

                if (EnsureCropStage().TryCrop(
                        bgrPixels, frameWidth, frameHeight,
                        out croppedPixels, out croppedWidth, out croppedHeight))
                {
                    int croppedLength = checked(croppedWidth * croppedHeight * 3);
                    if (croppedPixels.Length >= croppedLength)
                    {
                        bgrPixels = croppedPixels;
                        frameWidth = croppedWidth;
                        frameHeight = croppedHeight;
                        expectedLength = croppedLength;
                    }
                }
            }

            EnsureBitmap(frameWidth, frameHeight);

            _bitmap.Lock();
            try
            {
                // 받은 버퍼를 화면 버퍼로 곧장 옮깁니다. 중간에 다른 그림을 만들지 않습니다.
                Marshal.Copy(bgrPixels, 0, _bitmap.BackBuffer, expectedLength);
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, frameWidth, frameHeight));
            }
            finally
            {
                _bitmap.Unlock();
            }

            _lastCapturedAt = capturedAt;
            _lastDrawnAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// 마지막으로 그린 때를 보고 영상이 흐르는지 판단합니다.
        /// </summary>
        private void UpdateLiveFrameState()
        {
            bool flowing = _lastDrawnAtUtc != DateTime.MinValue &&
                           (DateTime.UtcNow - _lastDrawnAtUtc).TotalSeconds < LiveFrameTimeoutSeconds;

            if (HasLiveFrame != flowing)
            {
                HasLiveFrame = flowing;
            }
        }

        /// <summary>
        /// 화면 버퍼를 준비합니다. 크기가 그대로면 쓰던 것을 계속 씁니다.
        ///
        /// <para>
        /// 프레임마다 새로 만들면 큰 그림이 계속 쌓여 화면이 주기적으로 끊깁니다.
        /// </para>
        /// </summary>
        private void EnsureBitmap(int frameWidth, int frameHeight)
        {
            if (_bitmap != null && _bitmapWidth == frameWidth && _bitmapHeight == frameHeight)
            {
                return;
            }

            // 콜백은 BGR 3채널로 넘어옵니다.
            _bitmap = new WriteableBitmap(frameWidth, frameHeight, 96, 96, PixelFormats.Bgr24, null);
            _bitmapWidth = frameWidth;
            _bitmapHeight = frameHeight;
            Source = _bitmap;
        }
    }
}
