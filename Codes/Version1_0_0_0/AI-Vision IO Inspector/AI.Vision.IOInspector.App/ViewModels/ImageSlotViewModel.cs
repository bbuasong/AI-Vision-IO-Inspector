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
        private string _liveStreamUrl;
        private int _frameWidth;
        private int _frameHeight;
        private bool _isLiveStreamEnabled;
        private bool _isCapturedStillVisible;
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

        public string LiveImagePath
        {
            get { return _liveImagePath; }
            set { SetProperty(ref _liveImagePath, value); }
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
                }
            }
        }

        public bool IsCapturedStillVisible
        {
            get { return _isCapturedStillVisible; }
            set { SetProperty(ref _isCapturedStillVisible, value); }
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

        public bool IsNativeStreamVisible
        {
            get { return _isLiveStreamEnabled && !IsResultOverlayVisible; }
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
                ResultBorderBrush = "#D94A2E";
                ResultBorderThickness = "4";
                return;
            }

            ResultBorderBrush = "#111820";
            ResultBorderThickness = "2";
        }
    }
}
