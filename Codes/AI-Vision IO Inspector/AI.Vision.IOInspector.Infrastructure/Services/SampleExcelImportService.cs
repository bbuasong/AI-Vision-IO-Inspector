using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 엑셀 업로드 UI 흐름을 먼저 검증하기 위한 샘플 Import 서비스입니다.
    /// 실제 엑셀 파일 파싱은 컬럼 정의가 확정되면 이 구현체를 교체합니다.
    /// </summary>
    public class SampleExcelImportService : IExcelImportService
    {
        private readonly IPartRepository _partRepository;

        public SampleExcelImportService(IPartRepository partRepository)
        {
            _partRepository = partRepository;
        }

        public ImportResult ImportSampleParts()
        {
            ImportResult result = new ImportResult();
            Part importedPart = CreateImportedPart();
            _partRepository.Save(importedPart);
            result.ImportedParts.Add(importedPart);
            result.TotalCount = 1;
            result.SuccessCount = 1;
            result.FailCount = 0;
            result.Message = "샘플 엑셀 기준정보 1건을 등록했습니다.";
            return result;
        }

        private Part CreateImportedPart()
        {
            Part part = new Part();
            part.PartNo = "EXCEL-0001";
            part.PartName = "IMPORTED-PART";
            part.CategoryCode = "IMP";
            part.CategoryDescription = "엑셀 업로드 샘플";
            part.Memo = "Sample";

            AddMeasurement(part, 1, "측정부 - 길이", ImageViewType.Top, 25m);
            AddMeasurement(part, 2, "측정부 - 너비", ImageViewType.Front, 12m);
            AddMeasurement(part, 3, "측정부 - 높이", ImageViewType.Back, 40m);
            AddMeasurement(part, 4, "측정부 - 두께", ImageViewType.Thickness, 4m);
            return part;
        }

        private void AddMeasurement(Part part, int id, string name, ImageViewType viewType, decimal value)
        {
            MeasurementRegion region = new MeasurementRegion();
            region.Id = id;
            region.PartNo = part.PartNo;
            region.IndexNo = id;
            region.ItemType = name.Substring(name.LastIndexOf('-') + 1).Trim();
            region.Name = "측정부" + id.ToString() + " - " + region.ItemType;
            // 넘겨받은 카메라를 그대로 씁니다.
            // 예전에는 측정부가 Thickness뿐이라 값을 박아 두었는데, 그러면 인자로 준 카메라가 무시됩니다.
            region.ViewType = viewType;
            region.NominalValue = value;
            region.ToleranceMin = 0m;
            region.ToleranceMax = 0m;
            region.Unit = "mm";
            region.Coordinates = "미지정";
            region.LineColor = MeasurementPointPolicy.GetDefaultColor(region.IndexNo);
            part.MeasurementRegions.Add(region);
        }
    }
}
