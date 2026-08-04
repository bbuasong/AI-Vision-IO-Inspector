using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 기준 이미지와 AI 기준 이미지 DB 간 유사도 검색 호출 경계입니다.
    /// 검사 판정용 IAiInferenceService와 목적이 달라 별도 계약으로 관리합니다.
    /// </summary>
    public interface IReferenceImageSimilarityService
    {
        ReferenceImageSimilarityResult SearchReferenceImages(ReferenceImageSimilarityRequest request);
    }
}
