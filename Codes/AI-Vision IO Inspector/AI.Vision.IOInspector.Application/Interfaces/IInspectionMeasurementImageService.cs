using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 검사 촬영 이미지의 복사본에 판정 결과를 기록해 별도 파일로 남깁니다.
    /// 원본 이미지는 절대 수정하지 않습니다.
    /// </summary>
    public interface IInspectionMeasurementImageService
    {
        /// <summary>
        /// 한 방향의 결과 기록 이미지를 만듭니다.
        ///
        /// 원본 아래에 영역을 덧붙여 Score, 치수(W/D/H), 최종 판정을 적습니다.
        /// 문자를 이미지 위에 얹지 않는 이유는 촬영 화면을 가리지 않기 위해서입니다.
        /// 원본 이미지는 그대로 두어야 합니다. coordinate 이미지와 Thickness 이미지는
        /// 해상도가 같아야 하고 6방향 병합도 원본 크기를 전제로 하기 때문입니다.
        ///
        /// 측정부가 있는 방향(Thickness, coordinate)이면 측정 선과 번호를 함께 표시하고
        /// 아래 영역에 측정부별 측정값/기준값/허용오차/판정을 추가로 적습니다.
        /// </summary>
        /// <param name="sourceImagePath">기록 대상 원본 이미지 경로입니다. 이 파일은 변경하지 않습니다.</param>
        /// <param name="outputFilePath">만들 결과 기록본 경로입니다.</param>
        /// <param name="viewType">이 이미지가 어느 방향인지입니다.</param>
        /// <param name="resultInfo">Score, 치수, 판정 등 이미지에 적을 검사 정보입니다.</param>
        /// <param name="regions">DB에 등록된 측정부입니다. 측정부가 없으면 null 또는 빈 목록을 넘깁니다.</param>
        /// <param name="results">측정부별 측정값과 판정입니다. 측정부가 없으면 null 또는 빈 목록을 넘깁니다.</param>
        /// <returns>만든 파일 경로입니다. 만들지 못하면 빈 문자열입니다.</returns>
        string CreateResultImage(
            string sourceImagePath,
            string outputFilePath,
            ImageViewType viewType,
            InspectionImageResultInfo resultInfo,
            IList<MeasurementRegion> regions,
            IList<MeasurementResult> results);
    }
}
