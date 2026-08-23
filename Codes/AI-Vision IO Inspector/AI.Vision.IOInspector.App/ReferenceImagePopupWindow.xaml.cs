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
            ImageViewType? currentCoordinateViewType = GetCurrentCoordinateViewType();
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

            // 검사 화면에서 어느 칸을 눌러 연 것이면 그 방향을 봅니다.
            // 그때는 좌표 그림이 아니라 그 방향의 사진을 보여 주는 것이 맞습니다.
            ImageViewType? targetViewType = requestedViewType ?? currentViewType;
            ImageViewType? targetCoordinateViewType = requestedViewType.HasValue ? null : currentCoordinateViewType;
            _currentIndex = ResolveDisplayIndex(targetViewType, targetCoordinateViewType);

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

            // DB에는 검사에 쓸 최신 한 벌만 보관합니다. 이전 벌은 기준 이미지 폴더에만
            // 남아 있으므로, 팝업에서는 먼저 실제 파일을 읽어 저장 시각별로 묶습니다.
            // 이 순서가 아니면 DB의 [001] 한 벌만 계속 표시됩니다.
            if (!BuildSetsFromSavedFiles(part))
            {
                BuildSetsFromDatabaseImages(part);
            }

            // 예전 버전은 모든 파일을 [001]로 저장했을 수 있습니다. 파일명의 번호가
            // 중복되어도 저장 시각은 각 버튼 클릭마다 다르므로, 실제 저장 순서로 화면 번호를
            // 다시 매겨 각 벌을 구분합니다.
            _sets.Sort(delegate(ReferenceImageSet left, ReferenceImageSet right)
            {
                return left.SavedAt.CompareTo(right.SavedAt);
            });
            for (int index = 0; index < _sets.Count; index++)
            {
                _sets[index].SetNo = index + 1;
            }
            _sets.Reverse();

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

        private bool BuildSetsFromSavedFiles(Part part)
        {
            string folderPath = ResolveReferenceImageFolderPath(part);
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return false;
            }

            Dictionary<DateTime, ReferenceImageSet> setMap = new Dictionary<DateTime, ReferenceImageSet>();
            try
            {
                foreach (string filePath in Directory.GetFiles(folderPath))
                {
                    ImageViewType viewType;
                    int ignoredSetNo;
                    DateTime savedAt;
                    if (!ReferenceImageFileNamePolicy.TryParseSavedImageFileName(
                            Path.GetFileName(filePath),
                            out viewType,
                            out ignoredSetNo,
                            out savedAt))
                    {
                        continue;
                    }

                    ReferenceImageSet set;
                    if (!setMap.TryGetValue(savedAt, out set))
                    {
                        set = new ReferenceImageSet();
                        set.SavedAt = savedAt;
                        setMap[savedAt] = set;
                        _sets.Add(set);
                    }

                    set.ImagePaths[viewType] = filePath;
                }
            }
            catch (IOException)
            {
                _sets.Clear();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                _sets.Clear();
                return false;
            }

            return _sets.Count > 0;
        }

        private void BuildSetsFromDatabaseImages(Part part)
        {
            if (part == null || part.Images == null)
            {
                return;
            }

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

                set.ImagePaths[image.ViewType] = image.FilePath;
                if (image.CapturedAt != DateTime.MinValue &&
                    (set.SavedAt == DateTime.MinValue || image.CapturedAt < set.SavedAt))
                {
                    set.SavedAt = image.CapturedAt;
                }
            }
        }

        private string ResolveReferenceImageFolderPath(Part part)
        {
            if (part == null || part.Images == null)
            {
                return string.Empty;
            }

            foreach (PartImage image in part.Images)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                string folderPath = Path.GetDirectoryName(image.FilePath);
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    return folderPath;
                }
            }

            return string.Empty;
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

            ReferenceImageSet selectedSet = selectedSetIndex >= 0 && selectedSetIndex < _sets.Count
                ? _sets[selectedSetIndex]
                : null;

            AddImage(selectedSet, ImageViewType.Top, "Top View");
            AddImage(selectedSet, ImageViewType.Front, "Front View");
            AddImage(selectedSet, ImageViewType.Back, "Back View");
            AddImage(selectedSet, ImageViewType.Left, "Left View");
            AddImage(selectedSet, ImageViewType.Right, "Right View");
            AddImage(selectedSet, ImageViewType.Thickness, "Thickness");
            AddCoordinateImageIfAvailable(part);
        }

        private void OnSetSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isUpdatingSetList)
            {
                return;
            }

            // 보고 있던 장을 그대로 두고 벌만 바꿉니다.
            // 측정부 좌표 그림을 보고 있었다면 그 그림으로 되돌아가야 합니다.
            ImageViewType? currentViewType = GetCurrentViewType();
            ImageViewType? currentCoordinateViewType = GetCurrentCoordinateViewType();
            BuildItems(_part, SetListBox.SelectedIndex);

            _currentIndex = ResolveDisplayIndex(currentViewType, currentCoordinateViewType);

            UpdateDisplay();
        }

        private void AddImage(ReferenceImageSet set, ImageViewType viewType, string displayName)
        {
            ReferenceImageItem item = new ReferenceImageItem();
            item.ViewType = viewType;
            item.DisplayName = displayName;
            item.FilePath = set == null || !set.ImagePaths.ContainsKey(viewType)
                ? string.Empty
                : set.ImagePaths[viewType];
            _items.Add(item);
        }

        /// <summary>
        /// 측정부 선을 그린 좌표 이미지를 6장 뒤에 덧붙입니다.
        ///
        /// <para>
        /// 측정부를 카메라마다 두므로 좌표 이미지도 카메라마다 한 장씩 있습니다.
        /// 예전에는 Thickness 한 장만 붙였는데, Top에 측정부를 두어도 그 그림을 볼 수 없었습니다.
        /// </para>
        ///
        /// <para>
        /// 좌표를 등록하지 않은 카메라는 파일이 없으므로 그만큼 페이지가 줄어듭니다.
        /// </para>
        /// </summary>
        private void AddCoordinateImageIfAvailable(Part part)
        {
            foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                string coordinateImagePath = ResolveCoordinateImagePath(part, viewType);
                if (string.IsNullOrWhiteSpace(coordinateImagePath))
                {
                    continue;
                }

                ReferenceImageItem item = new ReferenceImageItem();
                item.ViewType = null;

                // 어느 카메라의 좌표 그림인지 남깁니다.
                // 벌을 바꿔도 보던 좌표 그림을 그대로 두려면 이 값이 있어야 합니다.
                item.CoordinateViewType = viewType;
                item.DisplayName = "측정부좌표 " + viewType.ToString();
                item.FilePath = coordinateImagePath;
                _items.Add(item);
            }
        }

        private string ResolveCoordinateImagePath(Part part, ImageViewType viewType)
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

            // 옛 이름(품번_coordinate.png)도 함께 찾아 줍니다.
            // 카메라를 나누기 전에 저장한 자료가 그 이름으로 남아 있습니다.
            return ReferenceImageFileNamePolicy.FindCoordinateFilePath(
                imageDirectoryPath, viewType, part.PartNo);
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

        /// <summary>
        /// 보여 줄 장을 고릅니다.
        ///
        /// <para>
        /// 보고 있던 방향을 그대로 둡니다. 벌을 바꿨다고 해서 Top으로 되돌아가면,
        /// Thickness를 견주어 보던 사람이 벌마다 다시 여섯 번을 넘겨야 합니다.
        /// </para>
        ///
        /// <para>
        /// 그 방향에 사진이 없으면 앞에서부터 순서대로(01 Top · 02 Front · 03 Back …)
        /// 사진이 있는 첫 장을 보여 줍니다. 빈 장을 짚고 "이미지가 없습니다"만 띄우는 것보다
        /// 볼 수 있는 것을 먼저 보여 주는 편이 낫습니다.
        /// </para>
        /// </summary>
        private int ResolveDisplayIndex(ImageViewType? viewType)
        {
            return ResolveDisplayIndex(viewType, null);
        }

        /// <param name="coordinateViewType">
        /// 측정부 좌표 그림을 보고 있었다면 그 카메라입니다. 좌표 그림은 6방향 어디에도
        /// 속하지 않아 방향만으로는 되찾을 수 없습니다. 이 값이 없으면 벌을 바꿀 때마다
        /// 첫 장(Top)으로 튕겨 나갑니다.
        /// </param>
        private int ResolveDisplayIndex(ImageViewType? viewType, ImageViewType? coordinateViewType)
        {
            if (coordinateViewType.HasValue)
            {
                int coordinateIndex = FindCoordinateItemIndex(coordinateViewType.Value);
                if (coordinateIndex >= 0)
                {
                    return coordinateIndex;
                }
            }

            int index = FindItemIndex(viewType);
            if (index >= 0)
            {
                return index;
            }

            return FindFirstAvailableIndex();
        }

        /// <summary>
        /// 그 카메라의 측정부 좌표 그림을 찾습니다.
        /// </summary>
        private int FindCoordinateItemIndex(ImageViewType coordinateViewType)
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index].CoordinateViewType == coordinateViewType && HasImageFile(_items[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// 지금 보고 있는 장이 측정부 좌표 그림이면 그 카메라를 돌려줍니다.
        /// </summary>
        private ImageViewType? GetCurrentCoordinateViewType()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count)
            {
                return null;
            }

            return _items[_currentIndex].CoordinateViewType;
        }

        /// <summary>
        /// 그 방향의 장을 찾습니다. 사진이 실제로 있는 장만 고릅니다.
        /// </summary>
        private int FindItemIndex(ImageViewType? viewType)
        {
            if (!viewType.HasValue)
            {
                return -1;
            }

            for (int index = 0; index < _items.Count; index++)
            {
                if (_items[index].ViewType == viewType.Value && HasImageFile(_items[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// 사진이 있는 첫 장입니다. 목록이 이미 01 Top · 02 Front … 순서로 만들어져 있으므로
        /// 앞에서부터 훑으면 그대로 순번 순서가 됩니다.
        /// </summary>
        private int FindFirstAvailableIndex()
        {
            for (int index = 0; index < _items.Count; index++)
            {
                if (HasImageFile(_items[index]))
                {
                    return index;
                }
            }

            // 한 장도 없으면 첫 장을 짚어 "이미지가 없습니다"를 보여 줍니다.
            return _items.Count > 0 ? 0 : -1;
        }

        private static bool HasImageFile(ReferenceImageItem item)
        {
            return item != null &&
                   !string.IsNullOrWhiteSpace(item.FilePath) &&
                   File.Exists(item.FilePath);
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
            public ReferenceImageSet()
            {
                ImagePaths = new Dictionary<ImageViewType, string>();
            }

            public int SetNo { get; set; }

            public DateTime SavedAt { get; set; }

            public IDictionary<ImageViewType, string> ImagePaths { get; private set; }
        }

        private class ReferenceImageItem
        {
            /// <summary>
            /// 측정부 좌표 이미지 페이지는 6방향 어디에도 속하지 않으므로 null을 사용합니다.
            /// </summary>
            public ImageViewType? ViewType { get; set; }

            /// <summary>
            /// 측정부 좌표 그림이면 어느 카메라의 것인지입니다. 일반 사진이면 null 입니다.
            /// </summary>
            public ImageViewType? CoordinateViewType { get; set; }

            public string DisplayName { get; set; }

            public string FilePath { get; set; }
        }
    }
}
