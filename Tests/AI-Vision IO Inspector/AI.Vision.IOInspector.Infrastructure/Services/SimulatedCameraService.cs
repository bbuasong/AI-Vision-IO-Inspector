using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 카메라 SDK가 확정되기 전 UI와 검사 흐름을 검증하기 위한 시뮬레이션 카메라입니다.
    /// </summary>
    public class SimulatedCameraService : ICameraService
    {
        public IList<CapturedImage> CaptureAll(Part part)
        {
            IList<CapturedImage> images = new List<CapturedImage>();

            foreach (ImageViewType viewType in Enum.GetValues(typeof(ImageViewType)))
            {
                CapturedImage image = new CapturedImage();
                image.ViewType = viewType;
                image.DisplayName = viewType.ToString() + " View";
                image.FilePath = "SIMULATED_CAPTURE://" + part.PartNo + "/" + viewType.ToString();
                image.CapturedAt = DateTime.Now;
                images.Add(image);
            }

            return images;
        }
    }
}
