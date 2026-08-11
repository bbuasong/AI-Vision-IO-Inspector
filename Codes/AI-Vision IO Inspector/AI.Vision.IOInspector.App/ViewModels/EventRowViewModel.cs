using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 검사 이벤트 로그를 화면에 표시하기 위한 모델입니다.
    /// </summary>
    public class EventRowViewModel : ObservableObject
    {
        public EventRowViewModel(EventLogEntry entry)
        {
            Time = entry.CreatedAt.ToString("HH:mm:ss");
            Severity = entry.Severity.ToString();
            Source = entry.Source;
            Message = entry.Message;
        }

        public string Time { get; set; }

        public string Severity { get; set; }

        public string Source { get; set; }

        public string Message { get; set; }
    }
}
