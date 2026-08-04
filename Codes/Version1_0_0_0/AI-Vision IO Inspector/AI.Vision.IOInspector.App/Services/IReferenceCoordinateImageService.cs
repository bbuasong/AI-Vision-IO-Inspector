using System.Collections.Generic;
using AI.Vision.IOInspector.App.ViewModels;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// Thickness 기준 이미지에 등록된 모든 측정부 선을 합성한 좌표 확인 이미지를 생성합니다.
    /// 선은 측정 위치 안내용이며 실제 길이 계산은 수행하지 않습니다.
    /// </summary>
    public interface IReferenceCoordinateImageService
    {
        void SaveCoordinateImage(
            string thicknessImagePath,
            string outputFilePath,
            IList<MeasurementPointViewModel> measurementPoints);
    }
}
