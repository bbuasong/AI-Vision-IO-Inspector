namespace AI.Vision.IOInspector.Infrastructure.Repositories
{
    /// <summary>
    /// 로컬 검사 이력 파일의 보관 정책입니다.
    /// 운영 현장 HDD 용량과 고객 보관 기간 기준이 확정되면 이 값을 설정 파일로 분리합니다.
    /// </summary>
    public class InspectionHistoryRetentionOptions
    {
        public InspectionHistoryRetentionOptions()
        {
            RetentionDays = 0;
            MinimumFreeSpaceBytes = 0;
        }

        public int RetentionDays { get; set; }

        public long MinimumFreeSpaceBytes { get; set; }
    }
}
