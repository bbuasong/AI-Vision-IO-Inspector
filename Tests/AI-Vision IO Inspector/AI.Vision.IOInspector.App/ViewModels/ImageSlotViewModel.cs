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
        private bool _isLiveStreamEnabled;
        private bool _isCapturedStillVisible;
        private string _statusText;
        private string _resultText;
        private string _resultBrush;
        private string _resultBorderBrush;
        private string _resultBorderThickness;

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

        public bool IsLiveStreamEnabled
        {
            get { return _isLiveStreamEnabled; }
            set { SetProperty(ref _isLiveStreamEnabled, value); }
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

        public bool IsResultOverlayVisible
        {
            get
            {
                return string.Equals(_resultText, "PASS", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(_resultText, "FAIL", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(_resultText, "ERROR", StringComparison.OrdinalIgnoreCase);
            }
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
