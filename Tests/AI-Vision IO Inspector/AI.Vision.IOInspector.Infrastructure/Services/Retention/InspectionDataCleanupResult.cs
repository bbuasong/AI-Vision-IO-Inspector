namespace AI.Vision.IOInspector.Infrastructure.Services.Retention
{
    /// <summary>
    /// 검사 데이터 삭제 실행 결과입니다.
    /// </summary>
    public class InspectionDataCleanupResult
    {
        public int DeletedInspectionCount { get; set; }

        public int DeletedFolderCount { get; set; }

        public string Message
        {
            get
            {
                return "삭제된 검사 이력 " + DeletedInspectionCount.ToString() +
                       "건, 삭제된 시간 폴더 " + DeletedFolderCount.ToString() + "개";
            }
        }
    }
}
