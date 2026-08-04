using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 검사 이미지와 로그 파일 저장 경계입니다.
    /// </summary>
    public interface IFileStorageService
    {
        void StoreInspection(Inspection inspection);
    }
}
