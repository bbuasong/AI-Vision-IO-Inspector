namespace AI.Vision.IOInspector.Infrastructure.Services.Retention
{
    /// <summary>
    /// 검사 결과 데이터 자동삭제 정책입니다.
    /// 실제 삭제는 WPF 확인 팝업에서 사용자가 승인한 경우에만 수행합니다.
    /// </summary>
    public class InspectionDataRetentionSettings
    {
        public InspectionDataRetentionSettings()
        {
            IsFreeSpaceCleanupEnabled = false;
            MinimumFreeSpacePercent = 30m;
            IsRetentionPeriodCleanupEnabled = false;
            RetentionDays = 365;
        }

        public bool IsFreeSpaceCleanupEnabled { get; set; }

        public decimal MinimumFreeSpacePercent { get; set; }

        public bool IsRetentionPeriodCleanupEnabled { get; set; }

        public int RetentionDays { get; set; }
    }
}
