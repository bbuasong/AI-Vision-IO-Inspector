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
        private int _currentIndex;

        public ReferenceImagePopupWindow()
        {
            InitializeComponent();
            _items = new List<ReferenceImageItem>();
            _currentIndex = -1;
            UpdateDisplay();
        }

        /// <summary>
        /// 현재 Search DB 품목 이미지로 목록을 새로 만들고, 요청한 위치 또는 이전 위치를 표시합니다.
        /// </summary>
        public void SetPart(Part part, ImageViewType? requestedViewType)
        {
            ImageViewType? currentViewType = GetCurrentViewType();
            _items.Clear();

            if (part != null)
            {
                AddImage(part, ImageViewType.Top, "Top View");
                AddImage(part, ImageViewType.Front, "Front View");
                AddImage(part, ImageViewType.Back, "Back View");
                AddImage(part, ImageViewType.Left, "Left View");
                AddImage(part, ImageViewType.Right, "Right View");
                AddImage(part, ImageViewType.Thickness, "Thickness");
                AddCoordinateImageIfAvailable(part);
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

        private void AddImage(Part part, ImageViewType viewType, string displayName)
        {
            ReferenceImageItem item = new ReferenceImageItem();
            item.ViewType = viewType;
            item.DisplayName = displayName;
            item.FilePath = FindImagePath(part, viewType);
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

            string thicknessImagePath = FindImagePath(part, ImageViewType.Thickness);
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

        private string FindImagePath(Part part, ImageViewType viewType)
        {
            if (part == null || part.Images == null)
            {
                return string.Empty;
            }

            foreach (PartImage image in part.Images)
            {
                if (image != null && image.ViewType == viewType)
                {
                    return image.FilePath;
                }
            }

            return string.Empty;
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
