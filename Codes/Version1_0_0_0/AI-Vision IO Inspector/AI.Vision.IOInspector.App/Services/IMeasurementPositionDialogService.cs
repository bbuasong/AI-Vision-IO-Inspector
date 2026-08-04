using System.Collections.Generic;
using AI.Vision.IOInspector.App.ViewModels;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// Thickness 기준 이미지에서 측정부의 시작점과 끝점을 지정하는 창을 추상화합니다.
    /// </summary>
    public interface IMeasurementPositionDialogService
    {
        bool Show(
            string imageFilePath,
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints);
    }
}
