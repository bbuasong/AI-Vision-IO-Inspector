using System;
using System.Collections.Generic;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 입고검사 대상 부품의 기준정보입니다.
    /// 화면의 Part No, Part Name, 분류코드, 분류설명, 구분 항목과 연결됩니다.
    /// </summary>
    public class Part
    {
        public Part()
        {
            Images = new List<PartImage>();
            MeasurementRegions = new List<MeasurementRegion>();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string PartType { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public IList<PartImage> Images { get; private set; }

        public IList<MeasurementRegion> MeasurementRegions { get; private set; }
    }
}
