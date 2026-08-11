using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 검사 결과 Thickness 이미지에 측정부 정보와 측정값을 그려 넣습니다.
    /// 측정값은 파싱/비교/이력에만 남고 결과 이미지에는 표시되지 않던 것을 보완하는 용도입니다.
    /// </summary>
    public interface IInspectionMeasurementImageService
    {
        /// <summary>
        /// 측정 결과를 표시한 이미지를 원본 옆에 별도 파일(품번_Thickness_measured.png)로 만듭니다.
        ///
        /// 원본 Thickness 이미지는 수정하지 않습니다. 측정 정보 문자를 넣으려면 이미지 아래에
        /// 영역을 덧붙여야 해서 세로 크기가 달라지는데, coordinate 이미지와 Thickness 이미지는
        /// 해상도가 같아야 하고 6방향 병합도 원본 크기를 전제로 하기 때문입니다.
        /// </summary>
        /// <param name="thicknessImagePath">검사로 촬영한 Thickness 이미지 경로입니다. 이 파일은 변경하지 않습니다.</param>
        /// <param name="regions">DB에 등록된 측정부입니다. 좌표와 기준값/허용오차를 사용합니다.</param>
        /// <param name="results">측정부별 측정값과 판정입니다.</param>
        /// <returns>생성한 표시용 이미지 경로입니다. 만들지 못하면 빈 문자열입니다.</returns>
        string CreateMeasurementResultImage(
            string thicknessImagePath,
            IList<MeasurementRegion> regions,
            IList<MeasurementResult> results);
    }
}
