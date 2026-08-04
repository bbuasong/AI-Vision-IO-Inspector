using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.Engines;
using AI.Vision.IOInspector.Vision.Models;
using AI.Vision.IOInspector.Vision.Threading;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// Vision 프로젝트의 추론 엔진 결과를 애플리케이션 계층의 AI 결과 계약으로 변환합니다.
    /// 실제 AI 담당자 작업은 ViewModel을 수정하지 말고 IVisionInferenceEngine 구현체에 연결하는 방향을 기본으로 합니다.
    /// </summary>
    public class VisionAiInferenceService : IAiInferenceService, IReferenceImageSimilarityService, IInspectionScoreSettings, IDisposable
    {
        private readonly IVisionInferenceEngine _inferenceEngine;
        private readonly VisionInferenceWorker _inferenceWorker;
        private decimal _inspectionPassScoreThreshold = 95m;

        public VisionAiInferenceService(IVisionInferenceEngine inferenceEngine)
        {
            _inferenceEngine = inferenceEngine ?? throw new ArgumentNullException("inferenceEngine");
            _inferenceEngine.TrainingOutputReceived += OnTrainingOutputReceived;
            _inferenceEngine.TrainingErrorReceived += OnTrainingErrorReceived;
            _inferenceEngine.TrainingExited += OnTrainingExited;
            _inferenceWorker = new VisionInferenceWorker(_inferenceEngine);
            _inferenceWorker.Start();
        }

        public event EventHandler<TrainingProcessDataEventArgs> TrainingOutputReceived;

        public event EventHandler<TrainingProcessDataEventArgs> TrainingErrorReceived;

        public event EventHandler<TrainingProcessExitedEventArgs> TrainingExited;

        public AiInferenceResult Inspect(Part part, IList<CapturedImage> capturedImages)
        {
            try
            {
                VisionInspectionInput input = BuildInput(part, capturedImages);
                //Inf->>Inf: EnqueueRequest() -> 추론 Worker 전용 스레드로 위임
                VisionInspectionOutput output = _inferenceWorker.Inspect(input);
                if (output == null)
                {
                    return CreateFailureResult("AI 추론 결과가 비어 있습니다.");
                }

                return ConvertToApplicationResult(output);
            }
            catch (Exception ex)
            {
                // VLAD SDK/Worker 오류는 검사 흐름을 죽이지 않고 검사 결과 로그로 반환합니다.
                return CreateFailureResult("AI 추론 실행 실패: " + ex.Message);
            }
        }

        public string StartImageTraining()
        {
            // 시작 실패는 UI가 실행 중 상태로 남지 않도록 호출자에게 예외로 전달합니다.
            return _inferenceWorker.StartImageTraining();
        }

        /// <summary>
        /// 단일품목 등록 화면의 기준이미지 유사도 검색을 Vision 작업 스레드로 전달합니다.
        /// 이전 DLL에 검색 export가 없으면 앱을 중단하지 않고 안내 가능한 실패 결과를 반환합니다.
        /// </summary>
        public ReferenceImageSimilarityResult SearchReferenceImages(ReferenceImageSimilarityRequest request)
        {
            try
            {
                ReferenceImageSimilarityResult result = _inferenceWorker.SearchReferenceImages(request);
                if (result == null)
                {
                    return CreateSimilarityFailureResult("AI 유사도 검색 결과가 비어 있습니다.");
                }

                return result;
            }
            catch (EntryPointNotFoundException)
            {
                return CreateSimilarityFailureResult(
                    "현재 VLAD_SDK.dll에 VLAD_Search_Mat 또는 VLAD_Search_Data export가 없습니다. AI 담당자가 새 검색 DLL을 배포해야 합니다.");
            }
            catch (Exception ex)
            {
                return CreateSimilarityFailureResult("AI 유사도 검색 실행 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 옵션의 검사 PASS/FAIL Score 기준을 다음 추론 요청의 inspectionContextJson에 포함합니다.
        /// 현재 배포 VLAD DLL은 4인자 export만 사용하므로 네이티브 호출 인자는 바꾸지 않습니다.
        /// </summary>
        public void SetInspectionPassScoreThreshold(decimal scoreThreshold)
        {
            if (scoreThreshold < 0m)
            {
                scoreThreshold = 0m;
            }
            else if (scoreThreshold > 100m)
            {
                scoreThreshold = 100m;
            }

            _inspectionPassScoreThreshold = decimal.Round(scoreThreshold, 2, MidpointRounding.AwayFromZero);
        }

        public void Dispose()
        {
            _inferenceEngine.TrainingOutputReceived -= OnTrainingOutputReceived;
            _inferenceEngine.TrainingErrorReceived -= OnTrainingErrorReceived;
            _inferenceEngine.TrainingExited -= OnTrainingExited;
            _inferenceWorker.Dispose();

            IDisposable disposableEngine = _inferenceEngine as IDisposable;
            if (disposableEngine != null)
            {
                disposableEngine.Dispose();
            }
        }

        private void OnTrainingOutputReceived(object sender, TrainingProcessDataEventArgs e)
        {
            EventHandler<TrainingProcessDataEventArgs> handler = TrainingOutputReceived;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void OnTrainingErrorReceived(object sender, TrainingProcessDataEventArgs e)
        {
            EventHandler<TrainingProcessDataEventArgs> handler = TrainingErrorReceived;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private void OnTrainingExited(object sender, TrainingProcessExitedEventArgs e)
        {
            EventHandler<TrainingProcessExitedEventArgs> handler = TrainingExited;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        private VisionInspectionInput BuildInput(Part part, IList<CapturedImage> capturedImages)
        {
            VisionInspectionInput input = new VisionInspectionInput();
            input.Part = part;
            input.InspectionId = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            input.CaptureTime = DateTime.Now;
            input.InspectionPassScoreThreshold = _inspectionPassScoreThreshold;
            input.LoadMeasurementPointsFromPart();

            if (capturedImages != null)
            {
                foreach (CapturedImage image in capturedImages)
                {
                    input.CapturedImages.Add(image);
                }
            }

            return input;
        }

        private AiInferenceResult ConvertToApplicationResult(VisionInspectionOutput output)
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = output.IsSuccess;
            result.IsMatched = output.IsMatched;
            result.HasAuthoritativeJudgment = output.HasAuthoritativeJudgment;
            result.PredictedClass = output.PredictedClass;
            result.Confidence = output.Confidence;
            result.HasScore = output.HasScore;
            result.Message = output.Message;
            result.ModelVersion = output.ModelVersion;
            result.DimensionWidth = output.DimensionWidth;
            result.DimensionDepth = output.DimensionDepth;
            result.DimensionHeight = output.DimensionHeight;
            result.DimensionUnit = output.DimensionUnit;

            foreach (VisionMeasurementValue measurement in output.Measurements)
            {
                result.MeasurementValues[measurement.MeasurementRegionId] = measurement.Value;
                result.MeasurementUnits[measurement.MeasurementRegionId] = measurement.Unit;
                result.RawPixelValues[measurement.MeasurementRegionId] = measurement.RawPixelValue;
                if (measurement.HasAiJudge)
                {
                    result.MeasurementJudgments[measurement.MeasurementRegionId] = measurement.IsAiPass;
                    result.MeasurementJudgeTexts[measurement.MeasurementRegionId] = measurement.AiJudge;
                }
            }

            return result;
        }

        private AiInferenceResult CreateFailureResult(string message)
        {
            AiInferenceResult result = new AiInferenceResult();
            result.IsSuccess = false;
            result.IsMatched = false;
            result.PredictedClass = string.Empty;
            result.Confidence = 0m;
            result.Message = message;
            result.ModelVersion = "VLAD";
            return result;
        }

        private ReferenceImageSimilarityResult CreateSimilarityFailureResult(string message)
        {
            ReferenceImageSimilarityResult result = new ReferenceImageSimilarityResult();
            result.IsSuccess = false;
            result.Message = message;
            return result;
        }
    }
}
