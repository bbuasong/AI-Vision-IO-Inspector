using System;
using System.IO;

namespace EpsonScanner.Services
{
    public class FilePathService
    {
        public string BaseScanDirectory { get; private set; }
        public string RawDirectory { get; private set; }
        public string LabelDirectory { get; private set; }
        public string CropDirectory { get; private set; }

        public FilePathService()
        {
            BaseScanDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scans");
            RawDirectory = Path.Combine(BaseScanDirectory, "Raw");
            LabelDirectory = Path.Combine(BaseScanDirectory, "Label");
            CropDirectory = Path.Combine(BaseScanDirectory, "Crop");
            EnsureDirectories();
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(RawDirectory);
            Directory.CreateDirectory(LabelDirectory);
            Directory.CreateDirectory(CropDirectory);
        }

        public ScanFilePaths CreateNewPaths()
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss_fff");
            return new ScanFilePaths
            {
                RawPath = Path.Combine(RawDirectory, "Raw_" + stamp + ".png"),
                LabelPath = Path.Combine(LabelDirectory, "Label_" + stamp + ".png"),
                CropPath = Path.Combine(CropDirectory, "Crop_" + stamp + ".png")
            };
        }
    }

    public class ScanFilePaths
    {
        public string RawPath { get; set; }
        public string LabelPath { get; set; }
        public string CropPath { get; set; }
    }
}
