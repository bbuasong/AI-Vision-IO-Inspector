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

        /// <summary>
        /// 이 카메라의 좌표 이미지를 임시로 만들 경로입니다.
        /// 측정부를 카메라마다 따로 관리하므로 좌표 이미지도 카메라마다 한 장씩 둡니다.
        /// </summary>
        string GetTemporaryCoordinateImagePath(Part part, ImageViewType viewType);

        void DeleteTemporaryCoordinateImage(Part part, ImageViewType viewType);

        void CommitTemporaryCoordinateImage(Part part, ImageViewType viewType);

        bool DeleteReferenceImage(PartImage image, out string message);

        /// <summary>
        /// 이 부품의 기준 이미지 폴더를 통째로 비웁니다.
        ///
        /// <para>
        /// 목록에 있는 파일만 지우면, 어떤 이유로 DB와 연결이 끊긴 파일이 폴더에 남습니다.
        /// 저장할 때마다 벌이 쌓이는 구조에서는 그런 파일이 계속 늘어나므로,
        /// 전체 삭제는 폴더를 기준으로 합니다.
        /// </para>
        /// </summary>
        /// <param name="deletedCount">지운 파일 수입니다.</param>
        /// <param name="errors">지우지 못한 파일의 사유입니다.</param>
        bool DeleteAllReferenceImageFiles(Part part, out int deletedCount, out IList<string> errors);
    }
}
