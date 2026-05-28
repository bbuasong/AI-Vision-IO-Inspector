using System;
using System.Collections.Generic;
using System.Linq;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Repositories
{
    /// <summary>
    /// 개발 초기 UI와 검사 흐름을 검증하기 위한 메모리 기반 부품 저장소입니다.
    /// 실제 DB가 확정되면 동일 인터페이스로 DB Repository를 추가합니다.
    /// </summary>
    public class InMemoryPartRepository : IPartRepository
    {
        private readonly IList<Part> _parts;

        public InMemoryPartRepository()
        {
            _parts = new List<Part>();
            Seed();
        }

        public IList<Part> GetAll()
        {
            return _parts.OrderBy(part => part.PartNo).ToList();
        }

        public Part GetByPartNo(string partNo)
        {
            if (string.IsNullOrWhiteSpace(partNo))
            {
                return null;
            }

            return _parts.FirstOrDefault(part => string.Equals(part.PartNo, partNo.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public void Save(Part part)
        {
            Part existing = GetByPartNo(part.PartNo);
            if (existing != null)
            {
                _parts.Remove(existing);
            }

            _parts.Add(part);
        }

        public void Delete(string partNo)
        {
            Part existing = GetByPartNo(partNo);
            if (existing != null)
            {
                _parts.Remove(existing);
            }
        }

        private void Seed()
        {
            Save(CreatePart("101040246C", "PAD-LOWER", "D98", "일반부품-구조그룹", "-", 55m, 15m, 190m, 5m));
            Save(CreatePart("101050301C", "CAP-UPPER", "D98", "일반부품-구조그룹", "-", 42m, 18m, 120m, 4m));
            Save(CreatePart("101-12-00", "CABLE-CONTROL", "C12", "케이블 제어부품", "케이블", 80m, 12m, 240m, 3m));
            Save(CreatePart("102-06-00L", "SHOE ASSY-BRAKE LH", "B77", "브레이크 조립품", "LH", 110m, 30m, 210m, 12m));
            Save(CreatePart("04026346", "NOZZLE-AIR", "D98", "일반부품-구조그룹", "노즐", 10m, 10m, 50m, 5m));
        }

        private Part CreatePart(string partNo, string partName, string categoryCode, string categoryDescription, string partType, decimal length, decimal width, decimal height, decimal thickness)
        {
            Part part = new Part();
            part.PartNo = partNo;
            part.PartName = partName;
            part.CategoryCode = categoryCode;
            part.CategoryDescription = categoryDescription;
            part.PartType = partType;

            AddImages(part);
            AddMeasurement(part, 1, "측정부 1 - 길이", ImageViewType.Top, length, -0.5m, 0.5m, "mm");
            AddMeasurement(part, 2, "측정부 1 - 너비", ImageViewType.Front, width, -0.5m, 0.5m, "mm");
            AddMeasurement(part, 3, "측정부 1 - 높이", ImageViewType.Back, height, -0.8m, 0.8m, "mm");
            AddMeasurement(part, 4, "측정부 1 - 두께", ImageViewType.Thickness, thickness, -0.3m, 0.3m, "mm");
            return part;
        }

        private void AddImages(Part part)
        {
            foreach (ImageViewType viewType in Enum.GetValues(typeof(ImageViewType)))
            {
                PartImage image = new PartImage();
                image.PartNo = part.PartNo;
                image.ViewType = viewType;
                image.FilePath = "REFERENCE://" + part.PartNo + "/" + viewType.ToString();
                image.CapturedAt = DateTime.Now;
                part.Images.Add(image);
            }
        }

        private void AddMeasurement(Part part, int id, string name, ImageViewType viewType, decimal nominal, decimal toleranceMin, decimal toleranceMax, string unit)
        {
            MeasurementRegion region = new MeasurementRegion();
            region.Id = id;
            region.PartNo = part.PartNo;
            region.Name = name;
            region.ViewType = viewType;
            region.Coordinates = "미정";
            region.NominalValue = nominal;
            region.ToleranceMin = toleranceMin;
            region.ToleranceMax = toleranceMax;
            region.Unit = unit;
            part.MeasurementRegions.Add(region);
        }
    }
}

