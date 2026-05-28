using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 기준정보 엑셀 일괄등록 경계입니다. 현재는 샘플 Import 기능으로 UI 흐름을 검증합니다.
    /// </summary>
    public interface IExcelImportService
    {
        ImportResult ImportSampleParts();
    }
}
