namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// VLAD 유사도 검색 결과의 방향별 후보를 화면에 표시합니다.
    /// </summary>
    public class SimilarityCandidateViewModel : ObservableObject
    {
        private int _rank;
        private string _viewName;
        private string _partNo;
        private string _partName;
        private string _matchStatusText;
        private decimal _score;

        public int Rank
        {
            get { return _rank; }
            set { SetProperty(ref _rank, value); }
        }

        /// <summary>
        /// 검색에 사용한 등록 기준이미지 위치입니다. 예: Top, Front, Thickness.
        /// </summary>
        public string ViewName
        {
            get { return _viewName; }
            set { SetProperty(ref _viewName, value); }
        }

        public string PartNo
        {
            get { return _partNo; }
            set { SetProperty(ref _partNo, value); }
        }

        public string PartName
        {
            get { return _partName; }
            set { SetProperty(ref _partName, value); }
        }

        /// <summary>
        /// 학습 DB에 존재하는지 여부를 사람이 확인하기 쉬운 문구로 표시합니다.
        /// </summary>
        public string MatchStatusText
        {
            get { return _matchStatusText; }
            set { SetProperty(ref _matchStatusText, value); }
        }

        public decimal Score
        {
            get { return _score; }
            set { SetProperty(ref _score, value); }
        }
    }
}
