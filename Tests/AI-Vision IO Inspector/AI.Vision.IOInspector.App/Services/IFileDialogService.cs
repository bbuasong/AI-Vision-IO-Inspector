namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// ViewModel이 WPF 파일 다이얼로그에 직접 의존하지 않도록 분리한 서비스입니다.
    /// </summary>
    public interface IFileDialogService
    {
        string SelectImageFile();

        string SelectCsvOpenFile();

        string SelectCsvSaveFile();
    }
}
