using System;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 기준 이미지 관리 그리드에 표시할 이미지 정보입니다.
    /// 실제 파일 접근 경로와 화면 표시용 관리 경로를 분리합니다.
    /// </summary>
    public class ImageEditViewModel : ObservableObject
    {
        private const string ReferencePathPrefix = "REFERENCE:\\\\";
        private readonly PartImage _image;

        public ImageEditViewModel(PartImage image, int order)
        {
            _image = image;
            Order = order;
        }

        public PartImage Image
        {
            get { return _image; }
        }

        public int Order { get; set; }

        public string ViewType
        {
            get { return _image.ViewType.ToString(); }
        }

        public string FilePath
        {
            get { return _image.FilePath; }
        }

        public string DisplayPath
        {
            get { return BuildDisplayPath(_image.FilePath); }
        }

        private string BuildDisplayPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "-";
            }

            string folderPath = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return filePath;
            }

            string normalizedPath = folderPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string[] pathItems = normalizedPath.Split(Path.DirectorySeparatorChar);
            for (int index = 0; index < pathItems.Length - 3; index++)
            {
                if (string.Equals(pathItems[index], "DB", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(pathItems[index + 1], "Image", StringComparison.OrdinalIgnoreCase))
                {
                    return ReferencePathPrefix + pathItems[index + 2] + "\\" + pathItems[index + 3];
                }
            }

            return folderPath;
        }
    }
}
