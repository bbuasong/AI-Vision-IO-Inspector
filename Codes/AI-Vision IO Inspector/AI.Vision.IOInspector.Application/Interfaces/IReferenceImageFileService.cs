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
        PartImage AddReferenceImage(Part part, string sourceFilePath, ImageViewType viewType, PartImage existingImage);

        void ClearTemporaryReferenceImages(Part part);

        PartImage StageReferenceImage(Part part, string sourceFilePath, ImageViewType viewType);

        IList<PartImage> CommitTemporaryReferenceImages(Part part, IList<PartImage> images);

        string GetTemporaryCoordinateImagePath(Part part);

        void DeleteTemporaryCoordinateImage(Part part);

        void CommitTemporaryCoordinateImage(Part part);

        bool DeleteReferenceImage(PartImage image, out string message);
    }
}
