using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.Vision.Isolation
{
    /// <summary>
    /// WPF 앱에서 외부 VLAD 추론 프로세스로 넘기는 요청 파일 모델입니다.
    /// </summary>
    public class IsolatedInferenceRequest
    {
        public IsolatedInferenceRequest()
        {
            CapturedImages = new List<IsolatedCapturedImageDto>();
        }

        public string ApplicationRootPath { get; set; }

        public IsolatedPartDto Part { get; set; }

        public IList<IsolatedCapturedImageDto> CapturedImages { get; set; }

        public static IsolatedInferenceRequest FromInspectionInput(string applicationRootPath, Part part, IList<CapturedImage> capturedImages)
        {
            IsolatedInferenceRequest request = new IsolatedInferenceRequest();
            request.ApplicationRootPath = applicationRootPath;
            request.Part = IsolatedPartDto.FromPart(part);

            if (capturedImages != null)
            {
                foreach (CapturedImage image in capturedImages)
                {
                    request.CapturedImages.Add(IsolatedCapturedImageDto.FromCapturedImage(image));
                }
            }

            return request;
        }

        public VisionInspectionInput ToVisionInspectionInput()
        {
            VisionInspectionInput input = new VisionInspectionInput();
            if (Part != null)
            {
                input.Part = Part.ToPart();
            }

            if (CapturedImages != null)
            {
                foreach (IsolatedCapturedImageDto image in CapturedImages)
                {
                    if (image != null)
                    {
                        input.CapturedImages.Add(image.ToCapturedImage());
                    }
                }
            }

            return input;
        }
    }
}
