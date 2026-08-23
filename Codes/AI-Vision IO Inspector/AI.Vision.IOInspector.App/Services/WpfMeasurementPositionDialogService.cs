using System.Collections.Generic;
using System;
using System.IO;
using System.Windows;
using AI.Vision.IOInspector.App.ViewModels;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// WPF 모달 창을 사용해 Thickness 이미지의 측정부 좌표를 편집합니다.
    /// </summary>
    public class WpfMeasurementPositionDialogService : IMeasurementPositionDialogService
    {
        public bool Show(
            IDictionary<ImageViewType, string> imageFilePathByViewType,
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints)
        {
            if (currentPoint == null || imageFilePathByViewType == null)
            {
                return false;
            }

            // 카메라마다 실제로 열 수 있는 사진만 남깁니다. 창은 이 목록을 보고
            // 어느 측정부로 옮겨 다닐 수 있는지 정합니다.
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(System.AppContext.BaseDirectory);
            IDictionary<ImageViewType, string> resolvedPaths = new Dictionary<ImageViewType, string>();
            foreach (KeyValuePair<ImageViewType, string> pair in imageFilePathByViewType)
            {
                string resolvedPath = pathSettings.ResolveImageFilePath(pair.Value);
                if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                {
                    resolvedPaths[pair.Key] = resolvedPath;
                }
            }

            // 지금 지정하려는 측정부의 배경 사진이 없으면 선을 그릴 수 없습니다.
            if (!resolvedPaths.ContainsKey(currentPoint.ViewType))
            {
                return false;
            }

            try
            {
                MeasurementPositionWindow window = new MeasurementPositionWindow(resolvedPaths, currentPoint, allPoints);
                if (System.Windows.Application.Current != null &&
                    System.Windows.Application.Current.MainWindow != null)
                {
                    window.Owner = System.Windows.Application.Current.MainWindow;
                }

                bool? result = window.ShowDialog();
                return result.HasValue && result.Value;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
