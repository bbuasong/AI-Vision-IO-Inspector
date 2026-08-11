namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// ViewModel이 WPF MessageBox에 직접 의존하지 않도록 분리한 메시지 팝업 서비스입니다.
    /// </summary>
    public interface IMessageDialogService
    {
        void ShowWarning(string title, string message);

        bool ShowConfirmation(string title, string message);

        bool ShowOcrUnregisteredConfirmation(string title, string message);

        ImageTrainingPromptResult ShowImageTrainingPrompt(string title, string message, System.DateTime defaultScheduleTime);
    }

    public class ImageTrainingPromptResult
    {
        public bool IsAccepted { get; set; }

        public bool StartNow { get; set; }

        public System.DateTime? ScheduledAt { get; set; }
    }
}
