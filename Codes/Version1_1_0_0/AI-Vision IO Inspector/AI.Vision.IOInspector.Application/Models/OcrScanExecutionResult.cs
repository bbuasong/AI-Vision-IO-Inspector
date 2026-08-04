namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// 한 번의 스캔과 OCR 처리 결과입니다.
    /// 장치/용지/OCR 오류도 예외 대신 결과로 반환하여 다음 스캔을 계속할 수 있게 합니다.
    /// </summary>
    public class OcrScanExecutionResult
    {
        public OcrScanExecutionResult()
        {
            ApiStatus = string.Empty;
            ApiErrorMessage = string.Empty;
            PartNo = string.Empty;
            ImagePath = string.Empty;
            ResultJsonPath = string.Empty;
            RawText = string.Empty;
            PartNoSource = string.Empty;
            QualityReason = string.Empty;
            Message = string.Empty;
        }

        public bool IsSuccess { get; set; }

        /// <summary>
        /// EpsonScanApi가 반환한 원본 status 값입니다.
        /// 예: done, low_quality. WPF는 이 값을 정상/오류로 재해석하지 않고 표시합니다.
        /// </summary>
        public string ApiStatus { get; set; }

        /// <summary>
        /// EpsonScanApi 응답의 error 또는 detail 값입니다.
        /// 값이 없으면 API가 오류를 반환하지 않은 것입니다.
        /// </summary>
        public string ApiErrorMessage { get; set; }

        public string PartNo { get; set; }

        public string ImagePath { get; set; }

        /// <summary>
        /// Epson OCR 작업자가 이미지 옆에 생성한 결과 JSON 파일 경로입니다.
        /// 등록 OCR은 DB 저장 성공 후 이 파일과 이미지 파일을 함께 정리합니다.
        /// </summary>
        public string ResultJsonPath { get; set; }

        public string RawText { get; set; }

        /// <summary>
        /// OCR API가 최종 품번을 선택할 때 사용한 판독 소스입니다.
        /// 예: epson, rapid
        /// </summary>
        public string PartNoSource { get; set; }

        /// <summary>
        /// OCR API가 계산한 품번 판독 신뢰도입니다. 0.0 ~ 1.0 범위를 사용합니다.
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// 재스캔 또는 확인이 필요한 경우 OCR API가 반환한 사유입니다.
        /// </summary>
        public string QualityReason { get; set; }

        public string Message { get; set; }
    }
}
