using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;
using AI.Vision.IOInspector.Vision.Services;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 저장한 원본을 화면에 보여 줄 때만 제품 영역으로 잘라 주는 곳입니다.
    ///
    /// <para>
    /// 파일은 잘라 내지 않은 원본으로 남깁니다. AI 추론도 원본을 씁니다.
    /// 사람이 보는 자리에서만 잘라 크게 보여 줍니다.
    /// </para>
    ///
    /// <para>
    /// 자를 자리는 스트리밍이 돌면서 알아낸 값을 씁니다. 아직 모르거나 자리가 그림 밖으로
    /// 나가면 원본을 그대로 돌려주므로 화면이 비지 않습니다.
    /// </para>
    /// </summary>
    public static class CroppedImageSourceFactory
    {
        /// <summary>
        /// 그 카메라의 크롭 자리로 잘라 낸 그림을 만듭니다.
        /// </summary>
        /// <param name="viewType">
        /// 어느 카메라의 그림인지입니다. <see cref="ImageViewType.Unclassified"/> 이면
        /// 측정부 좌표 그림으로 보고 자르지 않습니다. 원본 위에 선을 그린 것이라
        /// 자르면 선 위치가 어긋납니다.
        /// </param>
        public static ImageSource Build(string filePath, ImageViewType viewType)
        {
            BitmapImage source = LoadFrozen(filePath);
            if (source == null)
            {
                return null;
            }

            if (viewType == ImageViewType.Unclassified)
            {
                return source;
            }

            return Crop(source, ResolveRegion(viewType));
        }

        /// <summary>
        /// 카메라 번호로 자를 자리를 찾아 잘라 냅니다.
        /// 슬롯처럼 번호만 아는 곳에서 씁니다.
        /// </summary>
        public static ImageSource BuildByMonitorIndex(string filePath, int monitorIndex)
        {
            BitmapImage source = LoadFrozen(filePath);
            if (source == null)
            {
                return null;
            }

            if (monitorIndex < 0)
            {
                return source;
            }

            return Crop(source, CallbackFrameCropStage.GetLatestRegion(monitorIndex));
        }

        private static CropRegion ResolveRegion(ImageViewType viewType)
        {
            if (!MeasurementPointPolicy.IsSupportedViewType(viewType) &&
                viewType != ImageViewType.Top &&
                viewType != ImageViewType.Front &&
                viewType != ImageViewType.Back &&
                viewType != ImageViewType.Left &&
                viewType != ImageViewType.Right)
            {
                return null;
            }

            return CallbackFrameCropStage.GetLatestRegion(RtspMonitorIndexPolicy.FromViewType(viewType));
        }

        private static BitmapImage LoadFrozen(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                BitmapImage source = new BitmapImage();
                source.BeginInit();

                // 파일을 붙들지 않도록 한 번에 읽어 들이고, 예전에 읽은 그림이 남지 않게 합니다.
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                source.UriSource = new Uri(filePath, UriKind.Absolute);
                source.EndInit();
                source.Freeze();
                return source;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("그림을 읽지 못했습니다: " + filePath + " / " + ex.Message);
                return null;
            }
        }

        private static ImageSource Crop(BitmapSource source, CropRegion region)
        {
            if (source == null)
            {
                return null;
            }

            if (region == null || !region.IsValid)
            {
                return source;
            }

            try
            {
                Int32Rect rect = new Int32Rect(
                    Math.Max(0, region.X),
                    Math.Max(0, region.Y),
                    region.Width,
                    region.Height);

                if (rect.Width <= 0 ||
                    rect.Height <= 0 ||
                    rect.X + rect.Width > source.PixelWidth ||
                    rect.Y + rect.Height > source.PixelHeight)
                {
                    // 자리가 그림 밖으로 나갑니다. 카메라 해상도가 바뀌면 이렇게 됩니다.
                    return source;
                }

                CroppedBitmap cropped = new CroppedBitmap(source, rect);
                cropped.Freeze();
                return cropped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("그림을 자르지 못했습니다: " + ex.Message);
                return source;
            }
        }
    }
}
