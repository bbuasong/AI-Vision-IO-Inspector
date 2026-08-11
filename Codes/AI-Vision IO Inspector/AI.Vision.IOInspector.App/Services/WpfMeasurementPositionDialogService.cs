using System.Collections.Generic;
using System;
using System.IO;
using System.Windows;
using AI.Vision.IOInspector.App.ViewModels;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// WPF 모달 창을 사용해 Thickness 이미지의 측정부 좌표를 편집합니다.
    /// </summary>
    public class WpfMeasurementPositionDialogService : IMeasurementPositionDialogService
    {
        public bool Show(
            string imageFilePath,
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints)
        {
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(System.AppContext.BaseDirectory);
            string resolvedPath = pathSettings.ResolveImageFilePath(imageFilePath);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                return false;
            }

            try
            {
                MeasurementPositionWindow window = new MeasurementPositionWindow(resolvedPath, currentPoint, allPoints);
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
