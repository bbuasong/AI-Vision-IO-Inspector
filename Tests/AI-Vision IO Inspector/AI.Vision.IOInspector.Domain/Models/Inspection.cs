using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 한 번의 입고검사 이력입니다. DB 저장과 이력 화면 조회의 기준 데이터입니다.
    /// </summary>
    public class Inspection
    {
        public Inspection()
        {
            Measurements = new List<MeasurementResult>();
            Images = new List<CapturedImage>();
            Events = new List<EventLogEntry>();
            InspectedAt = DateTime.Now;
        }

        public int Id { get; set; }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string PartType { get; set; }

        public string InputCode { get; set; }

        public InspectionResult Result { get; set; }

        public DateTime InspectedAt { get; set; }

        public decimal ElapsedMilliseconds { get; set; }

        public string ResultMessage { get; set; }

        public IList<MeasurementResult> Measurements { get; private set; }

        public IList<CapturedImage> Images { get; private set; }

        public IList<EventLogEntry> Events { get; private set; }
    }
}
