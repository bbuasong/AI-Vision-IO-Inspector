namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// OCR 스캔 색상 모드의 표시 이름과 WIA 설정 값을 연결합니다.
    /// </summary>
    public class OcrColorModeOption
    {
        public OcrColorModeOption(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }

        public string DisplayName { get; private set; }

        public string Value { get; private set; }
    }
}
