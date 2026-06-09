namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 6방향 검사 화면의 기준 이미지, 스냅샷 이미지, RTSP 스트림, 판정 상태를 관리합니다.
    /// </summary>
    public class ImageSlotViewModel : ObservableObject
    {
        private string _title;
        private string _referenceImagePath;
        private string _liveImagePath;
        private string _liveStreamUrl;
        private bool _isLiveStreamEnabled;
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
            set { SetProperty(ref _referenceImagePath, value); }
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
