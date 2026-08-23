using System.Collections.Generic;
using AI.Vision.IOInspector.App.ViewModels;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 기준 이미지에서 측정부의 시작점과 끝점을 지정하는 창을 추상화합니다.
    ///
    /// <para>
    /// 창 안에서 다른 측정부로 옮겨 다닐 수 있으므로 카메라별 기준 이미지를 모두 넘깁니다.
    /// 카메라가 다른 측정부로 옮기면 그 카메라의 사진을 배경으로 깔아야 합니다.
    /// </para>
    /// </summary>
    public interface IMeasurementPositionDialogService
    {
        bool Show(
            IDictionary<ImageViewType, string> imageFilePathByViewType,
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints);
    }
}
