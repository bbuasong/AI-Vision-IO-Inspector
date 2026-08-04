namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// OCR 인식 결과가 사용되는 업무 화면을 구분합니다.
    /// 검사 검색과 신규 부품 등록은 서로 다른 흐름으로 처리해야 합니다.
    /// </summary>
    public enum OcrScanUsage
    {
        Inspection,
        Registration
    }
}
