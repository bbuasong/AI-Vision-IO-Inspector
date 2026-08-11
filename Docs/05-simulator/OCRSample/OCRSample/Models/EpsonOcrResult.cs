namespace OCRSample.Models
{
    /// <summary>
    /// 로컬 x86 Epson OCR 작업자가 반환한 OCR 결과입니다.
    /// </summary>
    public sealed class EpsonOcrResult
    {
        public EpsonOcrResult()
        {
            Engine = string.Empty;
            PartNo = string.Empty;
            PartNoSub = string.Empty;
            RawText = string.Empty;
            QualityReason = string.Empty;
        }

        public string Engine { get; set; }
        public string PartNo { get; set; }
        public string PartNoSub { get; set; }
        public string RawText { get; set; }
        public double Confidence { get; set; }
        public bool QualityOk { get; set; }
        public string QualityReason { get; set; }
        public bool NeedsConfirmation { get; set; }
    }
}
