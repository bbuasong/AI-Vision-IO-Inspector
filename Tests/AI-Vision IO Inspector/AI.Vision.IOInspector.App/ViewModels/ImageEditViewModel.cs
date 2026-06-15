using System;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 기준 이미지 관리 그리드에 표시할 이미지 정보입니다.
    /// 실제 파일 접근 경로와 화면 표시용 관리 경로를 분리합니다.
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
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
            return pathSettings.BuildReferenceDisplayPath(filePath);
        }
    }
}
