using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 부품 기준정보 저장소입니다. 실제 DB가 확정되면 Infrastructure 구현만 교체합니다.
    /// </summary>
    public interface IPartRepository
    {
        IList<Part> GetAll();

        Part GetByPartNo(string partNo);

        string GetCategoryDescription(string categoryCode);

        void Save(Part part);

        void ReplaceAll(IList<Part> parts);

        void Delete(string partNo);
    }
}
