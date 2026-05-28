using System.IO;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 기준 이미지 관리 그리드에 표시할 이미지 정보입니다.
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

        public string FileName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_image.FilePath))
                {
                    return "-";
                }

                return Path.GetFileName(_image.FilePath);
            }
        }

        public string ViewType
        {
            get { return _image.ViewType.ToString(); }
        }

        public string FilePath
        {
            get { return _image.FilePath; }
        }
    }
}
