using System;

namespace TrainingProcessMonitor.Wpf.Models
{
    public sealed class TrainingProtocolMessage
    {
        public string Type { get; private set; }
        public string Value { get; private set; }
        public string Message { get; private set; }
        public bool IsProtocolMessage { get; private set; }

        public static TrainingProtocolMessage Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return new TrainingProtocolMessage
                {
                    Type = string.Empty,
                    Value = string.Empty,
                    Message = string.Empty,
                    IsProtocolMessage = false
                };
            }

            var parts = line.Split(new[] { '|' }, 3);
            if (parts.Length < 2)
            {
                return new TrainingProtocolMessage
                {
                    Type = string.Empty,
                    Value = string.Empty,
                    Message = line,
                    IsProtocolMessage = false
                };
            }

            return new TrainingProtocolMessage
            {
                Type = parts[0].Trim().ToUpperInvariant(),
                Value = parts[1].Trim(),
                Message = parts.Length >= 3 ? parts[2].Trim() : string.Empty,
                IsProtocolMessage = true
            };
        }

        public int GetProgressOrDefault(int defaultValue)
        {
            int progress;
            if (!int.TryParse(Value, out progress))
            {
                return defaultValue;
            }

            return Math.Max(0, Math.Min(100, progress));
        }
    }
}
