using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 검사 흐름 중 발생한 사용자 안내 또는 오류 로그입니다.
    /// </summary>
    public class EventLogEntry
    {
        public EventLogEntry()
        {
            CreatedAt = DateTime.Now;
        }

        public EventSeverity Severity { get; set; }

        public string Source { get; set; }

        public string Message { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
