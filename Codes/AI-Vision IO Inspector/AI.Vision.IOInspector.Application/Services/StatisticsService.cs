using System.Collections.Generic;
using System;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// 통계 화면의 요약 지표를 계산합니다.
    /// 실제 DB 도입 후에는 집계 쿼리 기반 구현으로 교체할 수 있습니다.
    /// </summary>
    public class StatisticsService
    {
        private readonly IPartRepository _partRepository;
        private readonly IInspectionRepository _inspectionRepository;

        public StatisticsService(IPartRepository partRepository, IInspectionRepository inspectionRepository)
        {
            _partRepository = partRepository;
            _inspectionRepository = inspectionRepository;
        }

        public StatisticsSummary BuildSummary()
        {
            return BuildSummary(null, null);
        }

        public StatisticsSummary BuildSummary(DateTime? startTime, DateTime? endTime)
        {
            IList<Part> parts = _partRepository.GetAll();
            IList<Inspection> inspections = _inspectionRepository.GetAll();

            StatisticsSummary summary = new StatisticsSummary();
            summary.TotalPartCount = parts.Count;

            foreach (Inspection inspection in inspections)
            {
                if (startTime.HasValue && inspection.InspectedAt < startTime.Value)
                {
                    continue;
                }

                if (endTime.HasValue && inspection.InspectedAt > endTime.Value)
                {
                    continue;
                }

                summary.TotalInspectionCount++;

                if (inspection.Result == InspectionResult.Pass)
                {
                    summary.PassCount++;
                }
                else if (inspection.Result == InspectionResult.Fail)
                {
                    summary.FailCount++;
                }
                else if (inspection.Result == InspectionResult.Error)
                {
                    summary.ErrorCount++;
                }
            }

            return summary;
        }
    }
}
