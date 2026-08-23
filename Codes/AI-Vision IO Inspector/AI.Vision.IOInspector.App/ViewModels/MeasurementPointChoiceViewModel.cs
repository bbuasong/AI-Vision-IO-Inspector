namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 측정부 위치 지정 창에서 카메라별 줄에 놓이는 칸 하나입니다.
    ///
    /// <para>
    /// 지금 편집 중인 측정부인지를 이 칸이 스스로 압니다(<see cref="IsCurrent"/>).
    /// 예전에는 카메라마다 ListBox를 두고 각자 SelectedItem을 관리했는데, 그러면 고른 것이
    /// 카메라마다 하나씩 남습니다. Top을 편집하는 동안에도 Thk 줄의 어떤 칸이 골라진 채로
    /// 남아, 그 칸을 다시 누르면 "이미 골라진 것"이라 아무 일도 일어나지 않았습니다.
    /// 배경 사진도 그대로였습니다.
    /// </para>
    ///
    /// <para>
    /// 골라진 칸은 창 전체에서 언제나 하나뿐이어야 합니다.
    /// </para>
    /// </summary>
    public class MeasurementPointChoiceViewModel : ObservableObject
    {
        private bool _isCurrent;

        public MeasurementPointChoiceViewModel(MeasurementPointViewModel point)
        {
            Point = point;
        }

        public MeasurementPointViewModel Point { get; private set; }

        /// <summary>이 칸이 지금 편집 중인 측정부인지입니다. 창 전체에서 하나만 참입니다.</summary>
        public bool IsCurrent
        {
            get { return _isCurrent; }
            set { SetProperty(ref _isCurrent, value); }
        }
    }
}
