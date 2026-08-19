using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 기준 이미지 파일을 설정된 기준 이미지 저장소로 복사하고 PartImage 정보를 생성하는 서비스입니다.
    /// 실제 DB/스토리지 정책이 확정되면 구현체만 교체합니다.
    /// </summary>
    public interface IReferenceImageFileService
    {
        /// <summary>
        /// 기준 이미지를 한 장 보관합니다.
        /// </summary>
        /// <param name="savedAt">
        /// 저장 버튼을 누른 시각입니다. 한 번의 저장에서 나온 이미지들이 같은 값을 써야
        /// 파일명과 이력에서 한 벌로 묶입니다.
        /// </param>
        /// <param name="setNo">
        /// 이 이미지가 속한 벌의 번호입니다. 한 번의 저장에서 나온 이미지들이 같은 값을 씁니다.
        /// </param>
        PartImage AddReferenceImage(
            Part part,
            string sourceFilePath,
            ImageViewType viewType,
            int setNo,
            DateTime savedAt);

        void ClearTemporaryReferenceImages(Part part);

        PartImage StageReferenceImage(Part part, string sourceFilePath, ImageViewType viewType);

        IList<PartImage> CommitTemporaryReferenceImages(Part part, IList<PartImage> images);

        string GetTemporaryCoordinateImagePath(Part part);

        void DeleteTemporaryCoordinateImage(Part part);

        void CommitTemporaryCoordinateImage(Part part);

        bool DeleteReferenceImage(PartImage image, out string message);
    }
}
