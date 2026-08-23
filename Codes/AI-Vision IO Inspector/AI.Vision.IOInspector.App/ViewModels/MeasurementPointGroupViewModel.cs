using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 측정부 위치 지정 창에서 카메라별로 묶어 보여줄 측정부 한 줄입니다.
    ///
    /// <para>
    /// 한 줄에 모두 늘어놓으면 Top과 Thk가 섞여 어디까지가 어느 카메라인지 알기 어렵습니다.
    /// 카메라마다 한 줄씩 나눕니다.
    /// </para>
    /// </summary>
    public class MeasurementPointGroupViewModel
    {
        public MeasurementPointGroupViewModel(ImageViewType viewType, IList<MeasurementPointChoiceViewModel> choices)
        {
            ViewType = viewType;
            Choices = choices ?? new List<MeasurementPointChoiceViewModel>();
        }

        public ImageViewType ViewType { get; private set; }

        /// <summary>이 줄에 놓이는 칸들입니다. 골라진 칸은 창 전체에서 하나뿐입니다.</summary>
        public IList<MeasurementPointChoiceViewModel> Choices { get; private set; }

        /// <summary>줄 앞에 붙일 카메라 이름입니다. 예) Top, Thk</summary>
        public string Header
        {
            get { return MeasurementPointPolicy.GetViewShortName(ViewType); }
        }

        public bool HasChoices
        {
            get { return Choices.Count > 0; }
        }
    }
}
