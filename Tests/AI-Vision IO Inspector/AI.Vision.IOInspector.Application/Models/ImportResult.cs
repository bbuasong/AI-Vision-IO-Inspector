using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Application.Models
{
    /// <summary>
    /// 엑셀 일괄등록 결과입니다. 실제 엑셀 파싱 시 성공/실패 행 정보를 이 모델로 확장합니다.
    /// </summary>
    public class ImportResult
    {
        public ImportResult()
        {
            ImportedParts = new List<Part>();
        }

        public int TotalCount { get; set; }

        public int SuccessCount { get; set; }

        public int FailCount { get; set; }

        public string Message { get; set; }

        public IList<Part> ImportedParts { get; private set; }
    }
}
