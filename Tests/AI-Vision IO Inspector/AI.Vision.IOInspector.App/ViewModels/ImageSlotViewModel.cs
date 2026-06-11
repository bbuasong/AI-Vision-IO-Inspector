using System.IO;

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
                return string.IsNullOrWhiteSpace(_referenceImagePath) || !File.Exists(_referenceImagePath);
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value); }
        }

        public string ResultText
        {
            get { return _resultText; }
            set { SetProperty(ref _resultText, value); }
        }

        public string ResultBrush
        {
            get { return _resultBrush; }
            set { SetProperty(ref _resultBrush, value); }
        }
    }
}
