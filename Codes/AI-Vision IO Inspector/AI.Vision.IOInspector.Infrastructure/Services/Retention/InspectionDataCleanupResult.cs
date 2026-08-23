using System;
using System.Globalization;

namespace AI.Vision.IOInspector.Infrastructure.Services.Retention
{
    /// <summary>
    /// 검사 데이터 삭제 실행 결과입니다.
    /// </summary>
    public class InspectionDataCleanupResult
    {
        public DateTime DeleteFrom { get; set; }

        public DateTime DeleteBefore { get; set; }

        public string DataRootPath { get; set; }

        public int DeletedInspectionCount { get; set; }

        public int DeletedFolderCount { get; set; }

        public decimal? FreeSpacePercentBefore { get; set; }

        public decimal? FreeSpacePercentAfter { get; set; }

        public string Message
        {
            get
            {
                string message = "삭제된 검사 이력 " + DeletedInspectionCount.ToString(CultureInfo.InvariantCulture) +
                                 "건, 삭제된 폴더 " + DeletedFolderCount.ToString(CultureInfo.InvariantCulture) + "개";
                if (FreeSpacePercentAfter.HasValue)
                {
                    message += ", 삭제 후 HDD 여유공간 " +
                               FreeSpacePercentAfter.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
                }

                return message;
            }
        }
    }
}
