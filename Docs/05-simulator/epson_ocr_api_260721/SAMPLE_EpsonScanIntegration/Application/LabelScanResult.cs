namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 라벨 스캔 + OCR 결과입니다. Epson Scan API 의 POST /scan-to-pdf 응답을
    /// 검사 흐름에서 쓰기 좋은 표준 형태로 정리한 DTO 입니다.
    /// </summary>
    public class LabelScanResult
    {
        /// <summary>스캔 + OCR 자체가 정상 완료되었는지. (HTTP 성공 + status != "error")</summary>
        public bool IsSuccess { get; set; }

        /// <summary>Epson API job 상태 원본값. "done" | "low_quality" | "error".</summary>
        public string Status { get; set; }

        /// <summary>추출된 부품번호. 이 값을 InspectionWorkflowService.RunInspection(inputCode) 의 inputCode 로 넘깁니다.</summary>
        public string PartNo { get; set; }

        /// <summary>괄호 안 보조 번호 등 보조 부품번호(있을 때만). 없으면 빈 문자열.</summary>
        public string PartNoSub { get; set; }

        /// <summary>OCR 품질 신뢰도 0.0 ~ 1.0 (정상글자 비율). 임계값 미만이면 확인 필요로 판단.</summary>
        public double Confidence { get; set; }

        /// <summary>Epson 엔진이 품질 OK 로 판정했는지(필수항목/글자수/비율 충족).</summary>
        public bool QualityOk { get; set; }

        /// <summary>품질이 낮을 때의 사유(라벨 방향/초점/위치 등). 정상이면 빈 문자열.</summary>
        public string QualityReason { get; set; }

        /// <summary>
        /// true 이면 추출값을 그대로 믿지 말고 작업자 확인/수정이 필요합니다.
        /// (PartNo 가 비었거나, QualityOk=false, 또는 Confidence 가 임계값 미만일 때 true)
        /// </summary>
        public bool NeedsConfirmation { get; set; }

        /// <summary>생성된 검색가능 PDF 경로(있으면). 이력 보관용으로 활용 가능.</summary>
        public string PdfPath { get; set; }

        /// <summary>스캔 원본 이미지 경로(있으면).</summary>
        public string ImagePath { get; set; }

        /// <summary>OCR 전체 텍스트. 작업자가 수동으로 부품번호를 골라낼 때 참고용.</summary>
        public string RawText { get; set; }

        /// <summary>실패 시 사용자에게 보여줄 메시지(스캐너 용지 없음/오프라인/통신오류 등).</summary>
        public string ErrorMessage { get; set; }

        public LabelScanResult()
        {
            Status = string.Empty;
            PartNo = string.Empty;
            PartNoSub = string.Empty;
            QualityReason = string.Empty;
            PdfPath = string.Empty;
            ImagePath = string.Empty;
            RawText = string.Empty;
            ErrorMessage = string.Empty;
        }
    }
}
