using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 검사 이력 저장소입니다. DB 저장 전까지는 메모리 저장소로 동작합니다.
    /// </summary>
    public interface IInspectionRepository
    {
        IList<Inspection> GetAll();

        void Save(Inspection inspection);

        int GetNextId();

        DateTime? GetOldestInspectedAt();

        int DeleteInspectionsBefore(DateTime cutoffExclusive);
    }
}
