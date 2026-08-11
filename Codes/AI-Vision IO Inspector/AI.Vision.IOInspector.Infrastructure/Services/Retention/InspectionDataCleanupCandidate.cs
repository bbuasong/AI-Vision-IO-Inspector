using System;
using System.Globalization;

namespace AI.Vision.IOInspector.Infrastructure.Services.Retention
{
    /// <summary>
    /// 삭제 전 사용자 확인을 받기 위해 계산된 삭제 후보입니다.
    /// </summary>
    public class InspectionDataCleanupCandidate
    {
        public string Reason { get; set; }

        public DateTime DeleteFrom { get; set; }

        public DateTime DeleteBefore { get; set; }

        public string DataRootPath { get; set; }

        public decimal CurrentFreeSpacePercent { get; set; }

        public decimal MinimumFreeSpacePercent { get; set; }

        public string BuildConfirmationMessage()
        {
            string deleteTargetText;
            string deleteBeforeText = DeleteBefore.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            if (DeleteFrom != DateTime.MinValue && DeleteFrom < DeleteBefore)
            {
                deleteTargetText = "삭제 대상 기간: " +
                                   DeleteFrom.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                                   " ~ " +
                                   deleteBeforeText +
                                   " 이전";
            }
            else
            {
                deleteTargetText = "삭제 기준: " + deleteBeforeText + " 이전 검사 이력/이미지/로그";
            }

            return Reason + Environment.NewLine +
                   deleteTargetText + Environment.NewLine +
                   "대상 폴더: " + DataRootPath + Environment.NewLine +
                   "삭제를 진행하시겠습니까?";
        }
    }
}
