using System;

namespace EpsonScanner.Models
{
    public class ScanLogItem
    {
        public DateTime Time { get; set; }
        public string JudgeId { get; set; }
        public string RawImagePath { get; set; }
        public string LabelImagePath { get; set; }
        public string CropImagePath { get; set; }
        public int Rotation { get; set; }
        public string RawOcrText { get; set; }
        public string Message { get; set; }
    }
}
