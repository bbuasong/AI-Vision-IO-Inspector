namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// OCR 해상도의 표시 이름과 실제 DPI 값을 연결합니다.
    /// 기본 권장값은 UI에만 표시하며 설정 파일에는 숫자 DPI 값만 저장합니다.
    /// </summary>
    public class OcrResolutionOption
    {
        public OcrResolutionOption(string displayName, int value)
        {
            DisplayName = displayName;
            Value = value;
        }

        public string DisplayName { get; private set; }

        public int Value { get; private set; }
    }
}
