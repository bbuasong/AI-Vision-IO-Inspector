namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// ViewModel이 WPF MessageBox에 직접 의존하지 않도록 분리한 메시지 팝업 서비스입니다.
    /// </summary>
    public interface IMessageDialogService
    {
        void ShowWarning(string title, string message);

        bool ShowConfirmation(string title, string message);
    }
}
