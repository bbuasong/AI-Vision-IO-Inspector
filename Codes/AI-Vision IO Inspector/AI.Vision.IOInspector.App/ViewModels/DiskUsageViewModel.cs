using System;
using System.Globalization;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 옵션 화면에 표시할 로컬 디스크 사용량 정보입니다.
    /// 검사 이미지와 이력 이미지 저장 여유 공간을 작업자가 바로 확인하기 위한 표시 모델입니다.
    /// </summary>
    public class DiskUsageViewModel
    {
        public DiskUsageViewModel(string driveName, string volumeLabel, long totalBytes, long availableBytes)
        {
            DriveName = driveName;
            VolumeLabel = string.IsNullOrWhiteSpace(volumeLabel) ? "-" : volumeLabel;
            TotalBytes = totalBytes;
            AvailableBytes = availableBytes;
            UsedBytes = totalBytes > availableBytes ? totalBytes - availableBytes : 0;
        }

        public string DriveName { get; private set; }

        public string VolumeLabel { get; private set; }

        public long TotalBytes { get; private set; }

        public long UsedBytes { get; private set; }

        public long AvailableBytes { get; private set; }

        public string TotalText
        {
            get { return FormatBytes(TotalBytes); }
        }

        public string UsedText
        {
            get { return FormatBytes(UsedBytes); }
        }

        public string AvailableText
        {
            get { return FormatBytes(AvailableBytes); }
        }

        public string UsagePercentText
        {
            get
            {
                if (TotalBytes <= 0)
                {
                    return "0%";
                }

                double percent = (double)UsedBytes / TotalBytes * 100d;
                return percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
            }
        }

        private static string FormatBytes(long bytes)
        {
            const double gib = 1024d * 1024d * 1024d;
            if (bytes <= 0)
            {
                return "0 GB";
            }

            return (bytes / gib).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        }
    }
}
