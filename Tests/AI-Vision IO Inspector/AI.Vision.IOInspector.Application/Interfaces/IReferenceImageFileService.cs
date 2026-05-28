using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 기준 이미지 파일을 DB\Image 폴더로 복사하고 PartImage 정보를 생성하는 서비스입니다.
    /// 실제 DB/스토리지 정책이 확정되면 구현체만 교체합니다.
    /// </summary>
    public interface IReferenceImageFileService
    {
        PartImage AddReferenceImage(Part part, string sourceFilePath, int imageOrder);

        void DeleteReferenceImage(PartImage image);
    }
}
