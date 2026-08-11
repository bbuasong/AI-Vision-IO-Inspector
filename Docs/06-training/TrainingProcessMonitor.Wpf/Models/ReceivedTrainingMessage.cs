namespace TrainingProcessMonitor.Wpf.Models
{
    public sealed class ReceivedTrainingMessage
    {
        public string Time { get; set; }
        public string Source { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Message { get; set; }
        public string Raw { get; set; }
    }
}
