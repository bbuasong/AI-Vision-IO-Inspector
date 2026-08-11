using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// VLAD SDK가 제공하는 6방향 이미지 병합 기능을 Application 흐름에 연결합니다.
    /// 병합 실패는 원본 이미지와 DB 저장을 취소하지 않도록 성공 여부와 메시지로 반환합니다.
    /// </summary>
    public interface IImageMergeService
    {
        bool TryMergeReferenceImages(Part part, out string mergedFilePath, out string message);

        bool TryMergeInspectionImages(Inspection inspection, out string mergedFilePath, out string message);

        bool TryDeleteReferenceMergedImage(
            string partNo,
            IList<PartImage> referenceImages,
            out string message);
    }
}
