using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 등록 기준 이미지와 파생 coordinate 이미지를 고정 순서로 미리보기 위한 표시 모델입니다.
    /// </summary>
    public class ReferenceImagePreviewViewModel : ObservableObject
    {
        private string _similarityStatusText;

        public ReferenceImagePreviewViewModel()
        {
            SimilarityCandidates = new ObservableCollection<SimilarityCandidateViewModel>();
            _similarityStatusText = "검색 전";
        }

        public int Order { get; set; }

        public string Title { get; set; }

        public ImageViewType ViewType { get; set; }

        public string FilePath { get; set; }

        /// <summary>
        /// 현재 방향 이미지에서 유사도 기준점수 이상으로 검색된 상위 후보입니다.
        /// 화면에는 점수 내림차순으로 최대 3개만 표시합니다.
        /// </summary>
        public ObservableCollection<SimilarityCandidateViewModel> SimilarityCandidates { get; private set; }

        public string SimilarityStatusText
        {
            get { return _similarityStatusText; }
            private set { SetProperty(ref _similarityStatusText, value); }
        }

        public bool HasSimilarityCandidates
        {
            get { return SimilarityCandidates.Count > 0; }
        }

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

        /// <summary>
        /// 방향별 검색 결과를 새 목록으로 교체하고 빈 결과 안내 문구를 갱신합니다.
        /// </summary>
        public void SetSimilarityCandidates(
            IEnumerable<SimilarityCandidateViewModel> candidates,
            string emptyStatusText)
        {
            SimilarityCandidates.Clear();
            if (candidates != null)
            {
                foreach (SimilarityCandidateViewModel candidate in candidates)
                {
                    if (candidate != null)
                    {
                        SimilarityCandidates.Add(candidate);
                    }
                }
            }

            SimilarityStatusText = SimilarityCandidates.Count > 0
                ? string.Empty
                : (emptyStatusText ?? string.Empty);
            OnPropertyChanged("HasSimilarityCandidates");
        }

        public void ClearSimilarityCandidates(string statusText)
        {
            SetSimilarityCandidates(null, statusText);
        }
    }
}
