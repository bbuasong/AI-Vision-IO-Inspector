using System;
using System.IO;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 기준 이미지 관리 그리드에 표시할 이미지 정보입니다.
    /// 실제 파일 접근 경로를 화면에도 전체 경로로 표시합니다.
    /// </summary>
    public class ImageEditViewModel : ObservableObject
    {
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
            get
            {
                return _image.ViewType == ImageViewType.Unclassified
                    ? "미분류"
                    : _image.ViewType.ToString();
            }
        }

        public string FilePath
        {
            get { return _image.FilePath; }
        }

        public string DisplayPath
        {
            get { return BuildDisplayPath(_image.FilePath); }
        }

        public string RegisteredAt
        {
            get
            {
                if (_image.IsTemporary || _image.CapturedAt == DateTime.MinValue)
                {
                    return "DB 저장 대기";
                }

                return _image.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        private string BuildDisplayPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return "-";
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
            string resolvedPath = pathSettings.ResolveImageFilePath(filePath);

            // 오래된 DB 값은 REFERENCE:\\ 로 시작할 수 있습니다. 파일이 이미 지워졌어도
            // 화면에서는 현재 IMAGE_PATH 기준의 실제 전체 경로를 보여 주어야 합니다.
            if (resolvedPath.StartsWith(RuntimeImagePathSettings.ReferencePathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = resolvedPath.Substring(RuntimeImagePathSettings.ReferencePathPrefix.Length)
                    .TrimStart('\\', '/')
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);
                resolvedPath = Path.Combine(pathSettings.ReferenceImageRootPath, relativePath);
            }
            else if (!Path.IsPathRooted(resolvedPath))
            {
                resolvedPath = Path.Combine(pathSettings.ProjectRootPath, resolvedPath);
            }

            try
            {
                return Path.GetFullPath(resolvedPath);
            }
            catch (ArgumentException)
            {
                // 잘못된 옛 경로는 가공하지 않고 원문을 보여 주어 원인을 확인할 수 있게 합니다.
                return resolvedPath;
            }
            catch (NotSupportedException)
            {
                return resolvedPath;
            }
        }
    }
}
