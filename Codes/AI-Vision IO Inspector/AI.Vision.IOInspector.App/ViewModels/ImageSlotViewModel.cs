using System;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 검사 화면의 6방향 이미지 슬롯 상태를 관리합니다.
    /// 기준 이미지, 검사 시점 캡처 이미지, RTSP 스트림 상태를 UI에 전달합니다.
    /// </summary>
    public class ImageSlotViewModel : ObservableObject
    {
        private string _title;
        private string _referenceImagePath;
        private string _liveImagePath;
        private System.Windows.Media.ImageSource _liveImageSource;
        private string _liveStreamUrl;
        private int _frameWidth;
        private int _frameHeight;
        private bool _isLiveStreamEnabled;
        private bool _useCallbackVideo;
        private bool _useVideoCrop;
        private int _videoCropIntervalMilliseconds;
        private int _monitorIndex = -1;
        private bool _isCapturedStillVisible;
        private bool _isLiveFrameArrived;
        private string _statusText;
        private string _resultText;
        private string _resultBrush;
        private string _resultBorderBrush;
        private string _resultBorderThickness;
        private string _scoreText;
        private string _scoreBrush;
        private string _dimensionText;

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public string ReferenceImagePath
        {
            get { return _referenceImagePath; }
            set
            {
                if (SetProperty(ref _referenceImagePath, value))
                {
                    OnPropertyChanged("IsReferenceImageMissing");
                    OnPropertyChanged("HasReferenceImage");
                }
            }
        }

        /// <summary>
        /// 칸에 띄울 사진의 파일 경로입니다. 파일은 잘라 내지 않은 원본입니다.
        /// </summary>
        public string LiveImagePath
        {
            get { return _liveImagePath; }
            set
            {
                if (SetProperty(ref _liveImagePath, value))
                {
                    // 보여 줄 때만 제품 영역으로 잘라 크게 보여 줍니다.
                    LiveImageSource = AI.Vision.IOInspector.App.Services.CroppedImageSourceFactory
                        .BuildByMonitorIndex(value, _monitorIndex);
                }
            }
        }

        /// <summary>칸에 실제로 그릴 그림입니다. 원본을 크롭 자리로 잘라 둔 것입니다.</summary>
        public System.Windows.Media.ImageSource LiveImageSource
        {
            get { return _liveImageSource; }
            private set { SetProperty(ref _liveImageSource, value, "LiveImageSource"); }
        }

        public string LiveStreamUrl
        {
            get { return _liveStreamUrl; }
            set { SetProperty(ref _liveStreamUrl, value); }
        }

        /// <summary>
        /// Config.json의 CAM_WIDTH/CAM_HEIGHT에서 읽은 실제 RTSP 프레임 크기입니다.
        /// 네이티브 RTSP 영상 창이 비율을 유지해 렌더링하도록 전달합니다.
        /// </summary>
        public int FrameWidth
        {
            get { return _frameWidth; }
            set { SetProperty(ref _frameWidth, value); }
        }

        public int FrameHeight
        {
            get { return _frameHeight; }
            set { SetProperty(ref _frameHeight, value); }
        }

        public bool IsLiveStreamEnabled
        {
            get { return _isLiveStreamEnabled; }
            set
            {
                if (SetProperty(ref _isLiveStreamEnabled, value))
                {
                    OnPropertyChanged("IsNativeStreamVisible");
                    OnPropertyChanged("IsCallbackStreamVisible");
                }
            }
        }

        /// <summary>찍어 둔 사진을 칸에 띄울지입니다. 띄우는 동안에는 영상을 감춥니다.</summary>
        public bool IsCapturedStillVisible
        {
            get { return _isCapturedStillVisible; }
            set
            {
                if (SetProperty(ref _isCapturedStillVisible, value))
                {
                    OnPropertyChanged("IsNativeStreamVisible");
                    OnPropertyChanged("IsCallbackStreamVisible");
                }
            }
        }

        public bool IsReferenceImageMissing
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_referenceImagePath))
                {
                    return true;
                }

                RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
                return !pathSettings.ImageFileExists(_referenceImagePath);
            }
        }

        public bool HasReferenceImage
        {
            get { return !IsReferenceImageMissing; }
        }

        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value); }
        }

        /// <summary>
        /// 채널을 붙일 때 보여 주는 안내 문구입니다.
        ///
        /// <para>
        /// 영상이 실제로 흐르기 시작하면 이 문구는 상황에 맞지 않으므로 지웁니다.
        /// 지울 대상을 알아보려고 문구를 한곳에 모아 둡니다.
        /// </para>
        /// </summary>
        public const string StreamPreparingStatusText = "RTSP 스트림 준비";

        /// <summary>
        /// 이 칸에 영상이 실제로 흐르고 있는지입니다. 화면을 그리는 쪽에서 알려 줍니다.
        ///
        /// <para>
        /// 흐르기 시작하면 준비 문구를 지우고, 끊기면 다시 세웁니다.
        /// 검사 중 문구처럼 다른 곳에서 넣은 글은 절대 건드리지 않습니다.
        /// 준비 문구일 때와 빈칸일 때만 손대기 때문입니다.
        /// </para>
        /// </summary>
        public bool IsLiveFrameArrived
        {
            get { return _isLiveFrameArrived; }
            set
            {
                if (!SetProperty(ref _isLiveFrameArrived, value))
                {
                    return;
                }

                if (value)
                {
                    if (string.Equals(StatusText, StreamPreparingStatusText, StringComparison.Ordinal))
                    {
                        StatusText = string.Empty;
                    }
                }
                else if (string.IsNullOrWhiteSpace(StatusText) && IsLiveStreamEnabled)
                {
                    StatusText = StreamPreparingStatusText;
                }
            }
        }

        public string ResultText
        {
            get { return _resultText; }
            set
            {
                if (SetProperty(ref _resultText, value))
                {
                    UpdateResultVisualState();
                    OnPropertyChanged("IsResultOverlayVisible");
                    OnPropertyChanged("IsNativeStreamVisible");
                    OnPropertyChanged("IsCallbackStreamVisible");
                    OnPropertyChanged("IsInspectionCompletedViewVisible");
                    OnPropertyChanged("IsLiveInspectionViewVisible");
                }
            }
        }

        public string ResultBrush
        {
            get { return _resultBrush; }
            set { SetProperty(ref _resultBrush, value); }
        }

        public string ResultBorderBrush
        {
            get { return _resultBorderBrush; }
            private set { SetProperty(ref _resultBorderBrush, value); }
        }

        public string ResultBorderThickness
        {
            get { return _resultBorderThickness; }
            private set { SetProperty(ref _resultBorderThickness, value); }
        }

        /// <summary>
        /// 현재 검사 요청의 AI Score와 합격 기준을 표시합니다. SDK가 Score를 반환하지 않으면 '-'로 표시합니다.
        /// </summary>
        public string ScoreText
        {
            get { return _scoreText; }
            set { SetProperty(ref _scoreText, value); }
        }

        public string ScoreBrush
        {
            get { return _scoreBrush; }
            set { SetProperty(ref _scoreBrush, value); }
        }

        /// <summary>
        /// VLAD가 반환한 제품 가로·높이·깊이 정보입니다. 현재 SDK 결과에 값이 없으면 '-'로 유지합니다.
        /// </summary>
        public string DimensionText
        {
            get { return _dimensionText; }
            set { SetProperty(ref _dimensionText, value); }
        }

        public bool IsResultOverlayVisible
        {
            get
            {
                return string.Equals(_resultText, "PASS", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(_resultText, "FAIL", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(_resultText, "ERROR", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// RTSP 콜백에서 이 슬롯의 프레임을 찾을 때 쓰는 카메라 번호입니다.
        /// </summary>
        public int MonitorIndex
        {
            get { return _monitorIndex; }
            set
            {
                if (_monitorIndex != value)
                {
                    _monitorIndex = value;
                    OnPropertyChanged("MonitorIndex");
                }
            }
        }

        /// <summary>
        /// 콜백 프레임으로 그릴지입니다. 꺼져 있으면 LibVLC 영상 창이 나옵니다.
        /// </summary>
        public bool UseCallbackVideo
        {
            get { return _useCallbackVideo; }
            set
            {
                if (_useCallbackVideo != value)
                {
                    _useCallbackVideo = value;
                    OnPropertyChanged("UseCallbackVideo");
                    OnPropertyChanged("IsNativeStreamVisible");
                    OnPropertyChanged("IsCallbackStreamVisible");
                }
            }
        }

        /// <summary>화면에 그릴 때 제품 영역만 잘라 낼지입니다.</summary>
        public bool UseVideoCrop
        {
            get { return _useVideoCrop; }
            set
            {
                if (_useVideoCrop != value)
                {
                    _useVideoCrop = value;
                    OnPropertyChanged("UseVideoCrop");
                }
            }
        }

        /// <summary>크롭을 다시 시도할 최소 간격입니다.</summary>
        public int VideoCropIntervalMilliseconds
        {
            get { return _videoCropIntervalMilliseconds; }
            set
            {
                if (_videoCropIntervalMilliseconds != value)
                {
                    _videoCropIntervalMilliseconds = value;
                    OnPropertyChanged("VideoCropIntervalMilliseconds");
                }
            }
        }

        /// <summary>LibVLC 영상 창을 보일지입니다.</summary>
        /// <summary>
        /// 영상 화면을 보일지입니다.
        ///
        /// <para>
        /// 찍어 둔 사진을 보여 주는 동안에는 영상을 감춥니다. 사진은 종횡비를 지켜 그리므로
        /// 칸을 다 채우지 못하는데, 뒤에서 영상이 계속 돌면 그 여백으로 비쳐 보입니다.
        /// 크롭을 켜면 사진과 영상의 크기가 더 달라져서 눈에 띕니다.
        /// </para>
        /// </summary>
        public bool IsNativeStreamVisible
        {
            get
            {
                return _isLiveStreamEnabled &&
                       !IsResultOverlayVisible &&
                       !_isCapturedStillVisible &&
                       !_useCallbackVideo;
            }
        }

        /// <summary>콜백 프레임 화면을 보일지입니다.</summary>
        public bool IsCallbackStreamVisible
        {
            get
            {
                return _isLiveStreamEnabled &&
                       !IsResultOverlayVisible &&
                       !_isCapturedStillVisible &&
                       _useCallbackVideo;
            }
        }

        /// <summary>
        /// 검사 완료 후에는 측정 이미지를 전체로 표시하고 기준 이미지를 좌측 상단 1/4에 표시합니다.
        /// </summary>
        public bool IsInspectionCompletedViewVisible
        {
            get { return IsResultOverlayVisible; }
        }

        public bool IsLiveInspectionViewVisible
        {
            get { return !IsInspectionCompletedViewVisible; }
        }

        private void UpdateResultVisualState()
        {
            if (string.Equals(_resultText, "PASS", StringComparison.OrdinalIgnoreCase))
            {
                ResultBorderBrush = "#31FF1E";
                ResultBorderThickness = "4";
                return;
            }

            if (string.Equals(_resultText, "FAIL", StringComparison.OrdinalIgnoreCase))
            {
                ResultBorderBrush = "#FF2222";
                ResultBorderThickness = "4";
                return;
            }

            if (string.Equals(_resultText, "ERROR", StringComparison.OrdinalIgnoreCase))
            {
                ResultBorderBrush = "#E1A81B";
                ResultBorderThickness = "4";
                return;
            }

            ResultBorderBrush = "#111820";
            ResultBorderThickness = "2";
        }
    }
}
