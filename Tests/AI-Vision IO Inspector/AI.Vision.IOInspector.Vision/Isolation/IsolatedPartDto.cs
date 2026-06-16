using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Vision.Isolation
{
    /// <summary>
    /// 외부 추론 프로세스로 전달할 부품 기준정보입니다.
    /// JSON 역직렬화 안정성을 위해 Part의 private set 컬렉션을 명시적으로 풀어서 관리합니다.
    /// </summary>
    public class IsolatedPartDto
    {
        public IsolatedPartDto()
        {
            Images = new List<PartImage>();
            MeasurementRegions = new List<MeasurementRegion>();
        }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string PartType { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public IList<PartImage> Images { get; set; }

        public IList<MeasurementRegion> MeasurementRegions { get; set; }

        public static IsolatedPartDto FromPart(Part source)
        {
            IsolatedPartDto dto = new IsolatedPartDto();
            if (source == null)
            {
                return dto;
            }

            dto.PartNo = source.PartNo;
            dto.PartName = source.PartName;
            dto.CategoryCode = source.CategoryCode;
            dto.CategoryDescription = source.CategoryDescription;
            dto.PartType = source.PartType;
            dto.CreatedAt = source.CreatedAt;
            dto.UpdatedAt = source.UpdatedAt;

            CopyImages(dto.Images, source.Images);
            CopyMeasurementRegions(dto.MeasurementRegions, source.MeasurementRegions);
            return dto;
        }

        public Part ToPart()
        {
            Part part = new Part();
            part.PartNo = PartNo;
            part.PartName = PartName;
            part.CategoryCode = CategoryCode;
            part.CategoryDescription = CategoryDescription;
            part.PartType = PartType;
            part.CreatedAt = CreatedAt;
            part.UpdatedAt = UpdatedAt;

            CopyImages(part.Images, Images);
            CopyMeasurementRegions(part.MeasurementRegions, MeasurementRegions);
            return part;
        }

        private static void CopyImages(IList<PartImage> target, IList<PartImage> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (PartImage sourceImage in source)
            {
                if (sourceImage == null)
                {
                    continue;
                }

                PartImage image = new PartImage();
                image.Id = sourceImage.Id;
                image.PartNo = sourceImage.PartNo;
                image.ViewType = sourceImage.ViewType;
                image.FilePath = sourceImage.FilePath;
                image.CapturedAt = sourceImage.CapturedAt;
                target.Add(image);
            }
        }

        private static void CopyMeasurementRegions(IList<MeasurementRegion> target, IList<MeasurementRegion> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (MeasurementRegion sourceRegion in source)
            {
                if (sourceRegion == null)
                {
                    continue;
                }

                MeasurementRegion region = new MeasurementRegion();
                region.Id = sourceRegion.Id;
                region.PartNo = sourceRegion.PartNo;
                region.Name = sourceRegion.Name;
                region.ViewType = sourceRegion.ViewType;
                region.Coordinates = sourceRegion.Coordinates;
                region.NominalValue = sourceRegion.NominalValue;
                region.ToleranceMin = sourceRegion.ToleranceMin;
                region.ToleranceMax = sourceRegion.ToleranceMax;
                region.Unit = sourceRegion.Unit;
                target.Add(region);
            }
        }
    }
}
