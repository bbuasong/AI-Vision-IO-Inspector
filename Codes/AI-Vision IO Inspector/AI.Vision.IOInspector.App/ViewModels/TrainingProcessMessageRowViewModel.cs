namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 외부 학습 프로그램의 StandardOutput, StandardError, Process.Exited 한 건을 화면 Grid에 표시합니다.
    /// </summary>
    public sealed class TrainingProcessMessageRowViewModel
    {
        public string Time { get; set; }

        public string Source { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }

        public string Message { get; set; }

        public string Raw { get; set; }
    }
}
