namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 6방향 이미지 영역에 표시할 슬롯 정보입니다.
    /// 현재는 실제 이미지 대신 촬영/기준 상태 텍스트를 표시합니다.
    /// </summary>
    public class ImageSlotViewModel : ObservableObject
    {
        private string _title;
        private string _filePath;
        private string _statusText;

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public string FilePath
        {
            get { return _filePath; }
            set { SetProperty(ref _filePath, value); }
        }

        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value); }
        }
    }
}
