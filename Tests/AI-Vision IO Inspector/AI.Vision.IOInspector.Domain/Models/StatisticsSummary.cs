namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 통계 화면에서 표시할 집계 결과입니다.
    /// </summary>
    public class StatisticsSummary
    {
        public int TotalPartCount { get; set; }

        public int TotalInspectionCount { get; set; }

        public int OkCount { get; set; }

        public int NgCount { get; set; }

        public int ErrorCount { get; set; }
    }
}
