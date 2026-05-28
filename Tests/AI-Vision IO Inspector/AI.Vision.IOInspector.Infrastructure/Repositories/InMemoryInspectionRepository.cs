using System.Collections.Generic;
using System.Linq;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Repositories
{
    /// <summary>
    /// 검사 이력을 메모리에 보관합니다. UI와 통계 기능 검증용이며 DB 도입 시 교체합니다.
    /// </summary>
    public class InMemoryInspectionRepository : IInspectionRepository
    {
        private readonly IList<Inspection> _inspections;
        private int _nextId;

        public InMemoryInspectionRepository()
        {
            _inspections = new List<Inspection>();
            _nextId = 1;
        }

        public IList<Inspection> GetAll()
        {
            return _inspections.OrderByDescending(inspection => inspection.InspectedAt).ToList();
        }

        public void Save(Inspection inspection)
        {
            _inspections.Add(inspection);
        }

        public int GetNextId()
        {
            int id = _nextId;
            _nextId++;
            return id;
        }
    }
}
