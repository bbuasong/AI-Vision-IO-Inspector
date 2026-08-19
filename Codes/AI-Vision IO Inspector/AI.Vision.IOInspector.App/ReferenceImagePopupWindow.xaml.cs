using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// Search DB의 현재 부품에 등록된 Top~Thickness 기준 이미지를 확대 표시합니다.
    /// 창 인스턴스 관리는 WpfReferenceImagePopupService가 담당하고, 이 창은 이미지 전환만 담당합니다.
    /// </summary>
    public partial class ReferenceImagePopupWindow : Window
    {
        private readonly List<ReferenceImageItem> _items;
        private readonly List<ReferenceImageSet> _sets;
        private int _currentIndex;
        private Part _part;

        /// <summary>
        /// 벌 목록을 코드에서 바꾸는 동안에는 선택 변경 처리를 건너뜁니다.
        /// 목록을 다시 채우면 WPF가 선택 변경을 알리는데, 그때마다 화면을 다시 그리면
        /// 아직 준비되지 않은 상태를 읽게 됩니다.
        /// </summary>
        private bool _isUpdatingSetList;

        public ReferenceImagePopupWindow()
        {
            InitializeComponent();
            _items = new List<ReferenceImageItem>();
            _sets = new List<ReferenceImageSet>();
            _currentIndex = -1;
            UpdateDisplay();
        }

        /// <summary>
        /// 현재 Search DB 품목 이미지로 목록을 새로 만들고, 요청한 위치 또는 이전 위치를 표시합니다.
        /// </summary>
        public void SetPart(Part part, ImageViewType? requestedViewType)
        {
            ImageViewType? currentViewType = GetCurrentViewType();
            _part = part;

            BuildSetList(part);

            // 고른 벌이 없으면 가장 최근 벌을 봅니다.
            int selectedSetIndex = SetListBox.SelectedIndex;
            if (selectedSetIndex < 0 && _sets.Count > 0)
            {
                selectedSetIndex = 0;
            }

            BuildItems(part, selectedSetIndex);

            if (part != null)
            {
                PartInfoText.Text = "품번: " + SafeText(part.PartNo) + Environment.NewLine +
                                    "품명: " + SafeText(part.PartName);
            }
            else
            {
                PartInfoText.Text = "선택된 품목이 없습니다.";
            }

            ImageViewType? targetViewType = requestedViewType ?? currentViewType;
            _currentIndex = FindItemIndex(targetViewType);
            if (_currentIndex < 0 && _items.Count > 0)
            {
                _currentIndex = 0;
            }

            UpdateDisplay();
        }

        private void OnPreviousClick(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                return;
            }

            _currentIndex = (_currentIndex - 1 + _items.Count) % _items.Count;
            UpdateDisplay();
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0)
            {
                return;
            }

            _currentIndex = (_currentIndex + 1) % _items.Count;
            UpdateDisplay();
        }

        /// <summary>
        /// 이 부품에 저장된 벌을 모아 목록으로 만듭니다. 최근 벌이 위에 옵니다.
        /// </summary>
        private void BuildSetList(Part part)
        {
            _sets.Clear();

            if (part != null && part.Images != null)
            {
                Dictionary<int, ReferenceImageSet> setMap = new Dictionary<int, ReferenceImageSet>();
                foreach (PartImage image in part.Images)
                {
                    if (image == null || image.IsTemporary)
                    {
                        continue;
                    }

                    int setNo = image.SetNo < 1 ? 1 : image.SetNo;

                    ReferenceImageSet set;
                    if (!setMap.TryGetValue(setNo, out set))
                    {
                        set = new ReferenceImageSet();
                        set.SetNo = setNo;
                        set.SavedAt = image.CapturedAt;
                        setMap[setNo] = set;
                        _sets.Add(set);
                    }

                    // 같은 벌 안에서 시각이 조금 다르면 이른 쪽을 그 벌의 저장 시각으로 봅니다.
                    if (image.CapturedAt != DateTime.MinValue &&
                        (set.SavedAt == DateTime.MinValue || image.CapturedAt < set.SavedAt))
                    {
                        set.SavedAt = image.CapturedAt;
                    }
                }

                _sets.Sort(delegate(ReferenceImageSet left, ReferenceImageSet right)
                {
                    return right.SetNo.CompareTo(left.SetNo);
                });
            }

            _isUpdatingSetList = true;
            try
            {
                SetListBox.Items.Clear();
                foreach (ReferenceImageSet set in _sets)
                {
                    SetListBox.Items.Add(ReferenceImageFileNamePolicy.BuildSetDisplayName(set.SetNo, set.SavedAt));
                }

                if (_sets.Count > 0)
                {
                    SetListBox.SelectedIndex = 0;
                }
            }
            finally
            {
                _isUpdatingSetList = false;
            }
        }

        /// <summary>
        /// 고른 벌의 이미지들로 화면 목록을 만듭니다.
        /// 벌이 없으면 예전처럼 방향마다 한 장씩(가장 최근) 보여줍니다.
        /// </summary>
        private void BuildItems(Part part, int selectedSetIndex)
        {
            _items.Clear();
            if (part == null)
            {
                return;
            }

            int selectedSetNo = selectedSetIndex >= 0 && selectedSetIndex < _sets.Count
                ? _sets[selectedSetIndex].SetNo
                : 0;

            AddImage(part, selectedSetNo, ImageViewType.Top, "Top View");
            AddImage(part, selectedSetNo, ImageViewType.Front, "Front View");
            AddImage(part, selectedSetNo, ImageViewType.Back, "Back View");
            AddImage(part, selectedSetNo, ImageViewType.Left, "Left View");
            AddImage(part, selectedSetNo, ImageViewType.Right, "Right View");
            AddImage(part, selectedSetNo, ImageViewType.Thickness, "Thickness");
            AddCoordinateImageIfAvailable(part);
        }

        private void OnSetSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isUpdatingSetList)
            {
                return;
            }

            // 보고 있던 방향을 그대로 두고 벌만 바꿉니다.
            ImageViewType? currentViewType = GetCurrentViewType();
            BuildItems(_part, SetListBox.SelectedIndex);

            _currentIndex = FindItemIndex(currentViewType);
            if (_currentIndex < 0 && _items.Count > 0)
            {
                _currentIndex = 0;
            }

            UpdateDisplay();
        }

        private void AddImage(Part part, int setNo, ImageViewType viewType, string displayName)
        {
            ReferenceImageItem item = new ReferenceImageItem();
            item.ViewType = viewType;
            item.DisplayName = displayName;
            item.FilePath = FindImagePath(part, setNo, viewType);
            _items.Add(item);
        }

        /// <summary>
        /// 측정부 좌표가 그려진 Thickness 파생 이미지(&lt;품번&gt;_coordinate.png)가 있으면
        /// 6장 뒤에 7번째 페이지로 추가합니다. 좌표를 등록하지 않은 품번은 페이지가 6장 그대로입니다.
        /// </summary>
        private void AddCoordinateImageIfAvailable(Part part)
        {
            string coordinateImagePath = ResolveCoordinateImagePath(part);
            if (string.IsNullOrWhiteSpace(coordinateImagePath))
            {
                return;
            }

            ReferenceImageItem item = new ReferenceImageItem();
            item.ViewType = null;
            item.DisplayName = "측정부 좌표";
            item.FilePath = coordinateImagePath;
            _items.Add(item);
        }

        private string ResolveCoordinateImagePath(Part part)
        {
            if (part == null || part.MeasurementRegions == null || part.MeasurementRegions.Count == 0)
            {
                return string.Empty;
            }

            // 좌표 이미지는 벌과 무관하게 한 개만 유지되므로 벌을 가리지 않고 찾습니다.
            string thicknessImagePath = FindImagePath(part, 0, ImageViewType.Thickness);
            string imageDirectoryPath = string.IsNullOrWhiteSpace(thicknessImagePath)
                ? string.Empty
                : Path.GetDirectoryName(thicknessImagePath);
            if (string.IsNullOrWhiteSpace(imageDirectoryPath))
            {
                return string.Empty;
            }

            string coordinateImagePath = Path.Combine(
                imageDirectoryPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(part.PartNo));
            return File.Exists(coordinateImagePath) ? coordinateImagePath : string.Empty;
        }

        /// <summary>
        /// 고른 벌에서 이 방향의 이미지를 찾습니다.
        /// setNo가 0이면 벌을 가리지 않고 가장 최근 것을 씁니다.
        /// </summary>
        private string FindImagePath(Part part, int setNo, ImageViewType viewType)
        {
            if (part == null || part.Images == null)
            {
                return string.Empty;
            }

            PartImage found = null;
            foreach (PartImage image in part.Images)
            {
                if (image == null || image.ViewType != viewType)
                {
                    continue;
                }

                if (setNo > 0)
                {
                    int imageSetNo = image.SetNo < 1 ? 1 : image.SetNo;
                    if (imageSetNo != setNo)
                    {
                        continue;
                    }

                    return image.FilePath;
                }

                if (found == null || image.CapturedAt > found.CapturedAt)
                {
                    found = image;
                }
            }

            return found == null ? string.Empty : found.FilePath;
        }

        private ImageViewType? GetCurrentViewType()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count)
            {
                return null;
            }

            return _items[_currentIndex].ViewType;
        }

        private int FindItemIndex(ImageViewType? viewType)
        {
            if (!viewType.HasValue)
            {
                return -1;
            }

            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index].ViewType == viewType.Value)
                {
                    return index;
                }
            }

            return -1;
        }

        private void UpdateDisplay()
        {
            ReferenceImage.Source = null;
            EmptyImageText.Visibility = Visibility.Visible;
            LocationText.Text = "-";
            PositionText.Text = "0 / 0";
            ImageNameText.Text = string.Empty;
            CurrentSetText.Text = BuildCurrentSetText();

            if (_currentIndex < 0 || _currentIndex >= _items.Count)
            {
                return;
            }

            ReferenceImageItem item = _items[_currentIndex];
            LocationText.Text = item.DisplayName;
            PositionText.Text = (_currentIndex + 1).ToString() + " / " + _items.Count.ToString();
            ImageNameText.Text = item.DisplayName;

            BitmapSource imageSource = LoadBitmap(item.FilePath);
            if (imageSource == null)
            {
                EmptyImageText.Text = "등록된 " + item.DisplayName + " 이미지가 없습니다.";
                return;
            }

            ReferenceImage.Source = imageSource;
            EmptyImageText.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 지금 보고 있는 6장(+좌표 1장)이 어느 벌의 것인지 알려주는 문구입니다.
        /// 벌을 고르지 않았거나 저장된 벌이 없으면 그 사실을 그대로 적습니다.
        /// </summary>
        private string BuildCurrentSetText()
        {
            if (_sets.Count == 0)
            {
                return "저장된 벌이 없습니다.";
            }

            int selectedIndex = SetListBox.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= _sets.Count)
            {
                return "벌을 고르지 않았습니다.";
            }

            ReferenceImageSet set = _sets[selectedIndex];
            string text = "현재 벌  " +
                          ReferenceImageFileNamePolicy.BuildSetDisplayName(set.SetNo, set.SavedAt);

            if (selectedIndex == 0)
            {
                text = text + "   (가장 최근)";
            }

            return text + "      전체 " + _sets.Count.ToString() + "벌";
        }

        private BitmapSource LoadBitmap(string imagePath)
        {
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
            string resolvedPath = pathSettings.ResolveImageFilePath(imagePath);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                return null;
            }

            try
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.UriSource = new Uri(resolvedPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "-" : value;
        }

        /// <summary>
        /// 한 번의 저장으로 만들어진 이미지 묶음입니다.
        /// </summary>
        private class ReferenceImageSet
        {
            public int SetNo { get; set; }

            public DateTime SavedAt { get; set; }
        }

        private class ReferenceImageItem
        {
            /// <summary>
            /// 측정부 좌표 이미지 페이지는 6방향 어디에도 속하지 않으므로 null을 사용합니다.
            /// </summary>
            public ImageViewType? ViewType { get; set; }

            public string DisplayName { get; set; }

            public string FilePath { get; set; }
        }
    }
}
