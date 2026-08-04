namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 옵션 OCR 탭에 표시하는 최근 스캔 이력 한 건입니다.
    /// OCR 원문은 가장 최근 건만 별도 영역에 표시하고, Grid는 확인에 필요한 항목만 유지합니다.
    /// </summary>
    public class OcrHistoryRowViewModel
    {
        public OcrHistoryRowViewModel()
        {
            Time = string.Empty;
            PartNo = string.Empty;
            Status = string.Empty;
            Resolution = string.Empty;
            ColorMode = string.Empty;
            Usage = string.Empty;
            ImagePath = string.Empty;
        }

        public string Time { get; set; }

        public string PartNo { get; set; }

        public string Status { get; set; }

        /// <summary>
        /// 해당 OCR 작업에 적용한 스캔 해상도입니다. 프로그램 종료 후에는 보존하지 않습니다.
        /// </summary>
        public string Resolution { get; set; }

        /// <summary>
        /// 해당 OCR 작업에 적용한 색상 모드입니다. 프로그램 종료 후에는 보존하지 않습니다.
        /// </summary>
        public string ColorMode { get; set; }

        /// <summary>
        /// OCR 결과를 검사 Search DB 또는 부품 등록 중 어느 기능에서 사용했는지 표시합니다.
        /// </summary>
        public string Usage { get; set; }

        public string ImagePath { get; set; }
    }
}
