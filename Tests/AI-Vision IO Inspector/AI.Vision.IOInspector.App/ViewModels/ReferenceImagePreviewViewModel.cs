using System;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 등록 기준 이미지와 파생 coordinate 이미지를 고정 순서로 미리보기 위한 표시 모델입니다.
    /// </summary>
    public class ReferenceImagePreviewViewModel : ObservableObject
    {
        public int Order { get; set; }

        public string Title { get; set; }

        public string FilePath { get; set; }

        public bool HasImage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(FilePath))
                {
                    return false;
                }

                RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
                return pathSettings.ImageFileExists(FilePath);
            }
        }
    }
}
