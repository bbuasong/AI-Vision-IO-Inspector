using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using EventLogEntry = AI.Vision.IOInspector.Domain.Models.EventLogEntry;

namespace AI.Vision.IOInspector.Application.Services
{
    /// <summary>
    /// 메인 검사 흐름을 한 곳에서 제어합니다.
    /// 품번 조회, 촬영, AI 추론, 측정, 판정, 저장 순서를 유지하기 위한 Application 핵심 서비스입니다.
    /// </summary>
    public class InspectionWorkflowService
    {
        private readonly IPartRepository _partRepository;
        private readonly IInspectionRepository _inspectionRepository;
        private readonly ICameraService _cameraService;
        private readonly IAiInferenceService _aiInferenceService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IImageMergeService _imageMergeService;
        private readonly MeasurementService _measurementService;
        private readonly JudgmentService _judgmentService;
        private readonly IInspectionMeasurementImageService _measurementImageService;
        private decimal _inspectionPassScoreThreshold = 95m;

        /// <summary>
        /// 검사 작업 스레드의 단계 변경을 UI에 알립니다.
        /// 구독자의 예외가 실제 검사 흐름을 중단하지 않도록 ReportProgress에서 보호합니다.
        /// </summary>
        public event EventHandler<InspectionProgressEventArgs> ProgressChanged;

        public InspectionWorkflowService(
            IPartRepository partRepository,
            IInspectionRepository inspectionRepository,
            ICameraService cameraService,
            IAiInferenceService aiInferenceService,
            IFileStorageService fileStorageService,
            IImageMergeService imageMergeService,
            MeasurementService measurementService,
            JudgmentService judgmentService,
            IInspectionMeasurementImageService measurementImageService)
        {
            _measurementImageService = measurementImageService;
            _partRepository = partRepository;
            _inspectionRepository = inspectionRepository;
            _cameraService = cameraService;
            _aiInferenceService = aiInferenceService;
            _fileStorageService = fileStorageService;
            _imageMergeService = imageMergeService;
            _measurementService = measurementService;
            _judgmentService = judgmentService;
        }

        public Inspection RunInspection(string inputCode)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Inspection inspection = CreateInspectionShell(inputCode);

            try
            {
                ReportProgress(InspectionStatus.PartLookup, "부품 기준정보를 조회하고 있습니다.");
                AddEvent(inspection, EventSeverity.Info, "Workflow", "기준정보 조회를 시작합니다.");
                Part part = _partRepository.GetByPartNo(inputCode);
                if (part == null)
                {
                    inspection.Result = InspectionResult.Error;
                    inspection.ResultMessage = "입력값과 일치하는 부품 기준정보가 없습니다.";
                    AddEvent(inspection, EventSeverity.Error, "PartRepository", inspection.ResultMessage);
                    ReportProgress(InspectionStatus.Error, inspection.ResultMessage);
                    TrySaveInspection(inspection, stopwatch);
                    return inspection;
                }

                inspection.PartNo = part.PartNo;
                inspection.PartName = part.PartName;
                inspection.CategoryCode = part.CategoryCode;
                inspection.CategoryDescription = part.CategoryDescription;
                inspection.Memo = part.Memo;

                string coordinateImagePath = ReplaceMeasurementReferencePathsWithCoordinate(part);
                if (!string.IsNullOrWhiteSpace(coordinateImagePath))
                {
                    AddEvent(
                        inspection,
                        EventSeverity.Info,
                        "ReferenceImage",
                        "결과 이미지 배경으로 쓸 기준 이미지 경로를 coordinate 이미지로 연결했습니다. " +
                        coordinateImagePath);
                }
                else if (part.MeasurementRegions.Count > 0)
                {
                    AddEvent(
                        inspection,
                        EventSeverity.Warning,
                        "ReferenceImage",
                        "측정부가 등록되어 있지만 coordinate 이미지를 찾지 못해 " +
                        "결과 이미지 배경으로 기존 기준 이미지를 그대로 씁니다.");
                }

                // 학습이 도는지는 여기서 보지 않습니다.
                //
                // 예전에는 학습 중이면 여기서 검사를 ERROR로 끝냈습니다. 이제는 학습 여부를
                // 요청 JSON의 trainingRunning으로 SDK에 알려 주기만 하고, 앱의 검사 흐름은
                // 학습 중이든 아니든 완전히 같습니다. 캡처도 판정도 이력도 구분하지 않습니다.
                // 값 하나만 다르고 나머지는 같은 검사라는 것이 이번 결정입니다.
                // 사양은 VLAD_HD_Inference_Mat_학습중상태전달-2026-08-24.md 입니다.

                ReportProgress(InspectionStatus.Capturing, "카메라 최신 프레임을 검사 이미지로 저장하고 있습니다.");
                IList<CapturedImage> capturedImages = CaptureAll(part, inspection);

                // 찍은 사진을 화면 쪽에 함께 넘깁니다.
                // 이때부터 판정이 끝날 때까지 화면은 이 사진에 붙박여 있어야 합니다.
                ReportProgress(
                    InspectionStatus.Inferencing,
                    "캡처 이미지를 AI에서 검사하고 있습니다.",
                    capturedImages);
                AiInferenceResult inferenceResult = RunAiInspection(part, capturedImages, inspection);
                CopyViewResults(inspection, inferenceResult);
                ApplyAiScore(inspection, inferenceResult);
                if (!inferenceResult.IsSuccess)
                {
                    inspection.Result = InspectionResult.Error;
                    inspection.ResultMessage = inferenceResult.Message;
                    ReportProgress(InspectionStatus.Error, inspection.ResultMessage);
                    TrySaveInspection(inspection, stopwatch);
                    return inspection;
                }

                ReportProgress(InspectionStatus.Measuring, "AI가 반환한 측정값을 기준정보와 연결하고 있습니다.");
                IList<MeasurementResult> measurements = CompareReference(part, inferenceResult, inspection);
                ReportProgress(InspectionStatus.Judging, "이미지 판정과 측정 결과로 최종 판정을 생성하고 있습니다.");
                BuildFinalInspectionResult(inspection, inferenceResult, measurements);

                // 결과 기록 이미지는 최종 판정이 정해진 뒤에 만듭니다.
                // 판정 전에 만들면 이미지에 적히는 PASS/FAIL이 아직 확정되지 않은 값이 됩니다.
                DrawResultImages(part, capturedImages, measurements, inferenceResult, inspection);

                ReportProgress(InspectionStatus.Saving, "검사 이미지와 이력을 저장하고 있습니다.");
                TrySaveInspection(inspection, stopwatch);
                ReportProgress(InspectionStatus.Completed, inspection.ResultMessage);
                return inspection;
            }
            catch (Exception ex)
            {
                inspection.Result = InspectionResult.Error;
                inspection.ResultMessage = "검사 중 시스템 오류가 발생했습니다: " + ex.Message;
                AddEvent(inspection, EventSeverity.Error, "System", inspection.ResultMessage);
                ReportProgress(InspectionStatus.Error, inspection.ResultMessage);
                TrySaveInspection(inspection, stopwatch);
                return inspection;
            }
        }

        private void ReportProgress(InspectionStatus status, string message)
        {
            ReportProgress(status, message, null);
        }

        private void ReportProgress(InspectionStatus status, string message, IList<CapturedImage> capturedImages)
        {
            EventHandler<InspectionProgressEventArgs> handler = ProgressChanged;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(this, new InspectionProgressEventArgs(status, message, capturedImages));
            }
            catch (Exception progressException)
            {
                Debug.WriteLine("검사 진행 상태 전달 실패: " + progressException.Message);
            }
        }

        /// <summary>
        /// 옵션에서 읽은 Score 기준을 현재 이후 검사에 적용합니다.
        /// 검사 실행 중에는 값이 바뀌지 않도록 ViewModel이 저장 시점에만 호출합니다.
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

        private IList<CapturedImage> CaptureAll(Part part, Inspection inspection)
        {
            AddEvent(inspection, EventSeverity.Info, "CaptureAll", "검사 이미지 획득을 시작합니다.");
            IList<CapturedImage> capturedImages = _cameraService.CaptureAll(part);
            CopyImages(inspection, capturedImages);
            AddEvent(inspection, EventSeverity.Info, "CaptureAll", "검사 이미지 " + capturedImages.Count + "장을 획득했습니다.");
            WarnWhenPlaceholderImagesUsed(capturedImages, inspection);
            return capturedImages;
        }

        /// <summary>
        /// 프레임을 받지 못해 검정 이미지로 대체한 채널을 검사 이벤트에 경고로 남깁니다.
        ///
        /// 카메라 고장은 검사 오류로 처리하지 않습니다. 검정 이미지도 그대로 AI에 전달하고
        /// 최종 PASS/FAIL은 AI 결과 파싱 값을 따릅니다. 다만 판정 근거가 실제 촬영본이 아니라는 점을
        /// 작업자가 알 수 있어야 하므로 어느 방향이 대체되었는지 남깁니다.
        /// </summary>
        private void WarnWhenPlaceholderImagesUsed(IList<CapturedImage> capturedImages, Inspection inspection)
        {
            if (capturedImages == null)
            {
                return;
            }

            List<string> placeholderViews = new List<string>();
            foreach (CapturedImage image in capturedImages)
            {
                if (image != null && image.IsPlaceholder)
                {
                    placeholderViews.Add(image.ViewType.ToString());
                }
            }

            if (placeholderViews.Count == 0)
            {
                return;
            }

            AddEvent(
                inspection,
                EventSeverity.Warning,
                "CaptureAll",
                string.Join(", ", placeholderViews.ToArray()) +
                " 방향에서 카메라 프레임을 받지 못해 검정 이미지로 대체했습니다. " +
                "판정은 AI 결과를 그대로 따르므로 결과 해석 시 참고하십시오.");
        }

        private AiInferenceResult RunAiInspection(Part part, IList<CapturedImage> capturedImages, Inspection inspection)
        {
            AddEvent(inspection, EventSeverity.Info, "RunAiInspection", "이미지 AI 검사와 측정정보 확인을 시작합니다.");

            // VisionAiInferenceService -> Inspect
            AiInferenceResult inferenceResult = _aiInferenceService.Inspect(part, capturedImages);
            if (!inferenceResult.IsSuccess)
            {
                AddEvent(inspection, EventSeverity.Error, "RunAiInspection", inferenceResult.Message);
                return inferenceResult;
            }

            if (!inferenceResult.IsMatched)
            {
                AddEvent(inspection, EventSeverity.Warning, "RunAiInspection", "이미지 AI 검사 결과가 등록 기준과 일치하지 않습니다.");
            }
            else
            {
                AddEvent(inspection, EventSeverity.Info, "RunAiInspection", "이미지 AI 검사 결과가 등록 기준과 일치합니다.");
            }

            return inferenceResult;
        }

        private IList<MeasurementResult> CompareReference(Part part, AiInferenceResult inferenceResult, Inspection inspection)
        {
            string compareMessage = inferenceResult != null && inferenceResult.HasAuthoritativeJudgment
                ? "AI가 반환한 측정부별 판정을 검사 결과에 적용합니다."
                : "구형 AI 결과의 측정값을 DB 기준값/허용값과 비교합니다.";
            AddEvent(inspection, EventSeverity.Info, "CompareReference", compareMessage);
            WarnWhenMeasurementCountMismatched(part, inferenceResult, inspection);
            IList<MeasurementResult> measurements = _measurementService.CompareMeasurements(part, inferenceResult);
            CopyMeasurements(inspection, measurements);
            AddEvent(inspection, EventSeverity.Info, "CompareReference", "측정부 결과 " + measurements.Count + "개를 생성했습니다.");
            return measurements;
        }

        /// <summary>
        /// 촬영한 6방향 이미지마다 판정 결과를 기록한 복사본을 만듭니다.
        ///
        /// 원본은 수정하지 않습니다. 결과 문자를 넣으려면 이미지 아래에 영역을 덧붙여야 해서
        /// 세로 크기가 달라지는데, coordinate 이미지와 Thickness 이미지는 해상도가 같아야 하고
        /// 6방향 병합도 원본 크기를 전제로 하기 때문입니다.
        ///
        /// 측정부가 등록된 품목은 coordinate 이미지에 대해서도 결과본을 하나 더 만듭니다.
        /// 검사 시 Thickness 자리에 실제로 쓰인 기준이 coordinate 이미지이므로,
        /// 나중에 결과를 다시 볼 때 어떤 기준과 비교했는지 확인할 수 있어야 합니다.
        ///
        /// 그리기에 실패해도 검사 판정 자체는 이미 끝났으므로 경고만 남기고 진행합니다.
        /// </summary>
        private void DrawResultImages(
            Part part,
            IList<CapturedImage> capturedImages,
            IList<MeasurementResult> measurements,
            AiInferenceResult inferenceResult,
            Inspection inspection)
        {
            if (_measurementImageService == null || part == null || capturedImages == null)
            {
                return;
            }

            int createdCount = 0;

            foreach (CapturedImage capturedImage in capturedImages)
            {
                if (capturedImage == null || string.IsNullOrWhiteSpace(capturedImage.FilePath))
                {
                    continue;
                }

                // 결과 정보는 방향마다 새로 만듭니다.
                // 예전에는 루프 밖에서 하나만 만들어 6장에 돌려썼기 때문에,
                // AI가 방향별로 돌려준 판정과 Score, 치수가 모두 같은 값으로 적혔습니다.
                InspectionImageResultInfo resultInfo =
                    BuildResultInfo(part, inspection, inferenceResult, capturedImage.ViewType);

                // 측정부 선과 측정값은 그 카메라에 측정부가 있을 때만 표시합니다.
                // 측정부를 카메라마다 따로 두므로 Thickness로 고정하면 Top 측정부가 그려지지 않습니다.
                IList<MeasurementRegion> regions = FilterRegionsByViewType(part, capturedImage.ViewType);
                IList<MeasurementResult> results = regions.Count > 0
                    ? FilterMeasurementsByRegions(measurements, regions)
                    : null;
                if (regions.Count == 0)
                {
                    regions = null;
                }

                resultInfo.IsPlaceholder = capturedImage.IsPlaceholder;
                string outputFilePath = InspectionImageFileNamePolicy.BuildResultFilePathFromCapturePath(
                    capturedImage.FilePath,
                    capturedImage.ViewType);

                if (CreateResultImageSafely(
                        capturedImage.FilePath,
                        outputFilePath,
                        capturedImage.ViewType,
                        resultInfo,
                        regions,
                        results,
                        inspection))
                {
                    createdCount++;
                }
            }

            // 측정부 좌표 이미지는 측정부가 있는 카메라마다 한 장씩 만듭니다.
            // 각 카메라의 결과를 적어야 본문 이미지와 값이 어긋나지 않습니다.
            foreach (ImageViewType coordinateViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                if (FilterRegionsByViewType(part, coordinateViewType).Count == 0)
                {
                    continue;
                }

                InspectionImageResultInfo coordinateResultInfo =
                    BuildResultInfo(part, inspection, inferenceResult, coordinateViewType);

                if (CreateCoordinateResultImage(
                        part, capturedImages, measurements, coordinateResultInfo, inspection, coordinateViewType))
                {
                    createdCount++;
                }
            }

            if (createdCount > 0)
            {
                AddEvent(
                    inspection,
                    EventSeverity.Info,
                    "ResultImage",
                    "판정 결과를 기록한 이미지 " + createdCount.ToString(CultureInfo.InvariantCulture) + "장을 만들었습니다.");
            }
        }

        /// <summary>
        /// 측정부가 등록된 품목의 coordinate 이미지에 대해 결과 기록본을 만듭니다.
        /// 측정부가 없거나 coordinate 이미지를 찾지 못하면 아무것도 하지 않습니다.
        /// </summary>
        /// <summary>
        /// 이 카메라에 속한 측정부만 골라 냅니다.
        /// 측정부를 카메라마다 따로 두므로, 결과 이미지에도 그 카메라 것만 그려야 합니다.
        /// </summary>
        private IList<MeasurementRegion> FilterRegionsByViewType(Part part, ImageViewType viewType)
        {
            IList<MeasurementRegion> filtered = new List<MeasurementRegion>();
            if (part == null || part.MeasurementRegions == null)
            {
                return filtered;
            }

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region != null && region.ViewType == viewType)
                {
                    filtered.Add(region);
                }
            }

            return filtered;
        }

        /// <summary>
        /// 고른 측정부에 해당하는 측정 결과만 남깁니다.
        /// 다른 카메라의 결과가 섞이면 이미지에 엉뚱한 줄이 적힙니다.
        /// </summary>
        private IList<MeasurementResult> FilterMeasurementsByRegions(
            IList<MeasurementResult> measurements,
            IList<MeasurementRegion> regions)
        {
            IList<MeasurementResult> filtered = new List<MeasurementResult>();
            if (measurements == null || regions == null)
            {
                return filtered;
            }

            foreach (MeasurementResult measurement in measurements)
            {
                if (measurement == null)
                {
                    continue;
                }

                foreach (MeasurementRegion region in regions)
                {
                    if (region != null && region.Id == measurement.MeasurementRegionId)
                    {
                        filtered.Add(measurement);
                        break;
                    }
                }
            }

            return filtered;
        }

        private bool CreateCoordinateResultImage(
            Part part,
            IList<CapturedImage> capturedImages,
            IList<MeasurementResult> measurements,
            InspectionImageResultInfo resultInfo,
            Inspection inspection,
            ImageViewType viewType)
        {
            if (part.MeasurementRegions == null || part.MeasurementRegions.Count == 0)
            {
                return false;
            }

            // 검사 시작 단계에서 그 카메라의 기준 이미지 경로가 coordinate 이미지로 바뀌어 있습니다.
            // 따라서 여기서 읽는 기준 경로가 실제 비교에 쓰인 coordinate 이미지입니다.
            PartImage viewReference = FindReferenceImage(part, viewType);
            if (viewReference == null)
            {
                return false;
            }

            string coordinateImagePath = viewReference.FilePath;
            if (string.IsNullOrWhiteSpace(coordinateImagePath) || !File.Exists(coordinateImagePath))
            {
                return false;
            }

            // 결과본은 검사 이미지와 같은 폴더에 두어야 한 번의 검사 결과를 한자리에서 볼 수 있습니다.
            string viewCapturePath = FindCapturedImagePath(capturedImages, viewType);
            if (string.IsNullOrWhiteSpace(viewCapturePath))
            {
                return false;
            }

            string targetFolderPath = Path.GetDirectoryName(viewCapturePath);
            if (string.IsNullOrWhiteSpace(targetFolderPath))
            {
                return false;
            }

            string outputFilePath = Path.Combine(
                targetFolderPath,
                InspectionImageFileNamePolicy.BuildCoordinateResultFileName(
                    viewType,
                    part.PartNo,
                    part.PartName,
                    resultInfo.InspectionStartedAt,
                    Path.GetExtension(coordinateImagePath)));

            // coordinate 이미지는 등록 기준 이미지라 카메라 수신 여부와 무관합니다.
            resultInfo.IsPlaceholder = false;

            IList<MeasurementRegion> regions = FilterRegionsByViewType(part, viewType);
            return CreateResultImageSafely(
                coordinateImagePath,
                outputFilePath,
                viewType,
                resultInfo,
                regions,
                FilterMeasurementsByRegions(measurements, regions),
                inspection);
        }

        private bool CreateResultImageSafely(
            string sourceImagePath,
            string outputFilePath,
            ImageViewType viewType,
            InspectionImageResultInfo resultInfo,
            IList<MeasurementRegion> regions,
            IList<MeasurementResult> results,
            Inspection inspection)
        {
            try
            {
                string createdPath = _measurementImageService.CreateResultImage(
                    sourceImagePath,
                    outputFilePath,
                    viewType,
                    resultInfo,
                    regions,
                    results);
                return !string.IsNullOrWhiteSpace(createdPath);
            }
            catch (Exception ex)
            {
                AddEvent(
                    inspection,
                    EventSeverity.Warning,
                    "ResultImage",
                    viewType + " 결과 기록 이미지를 만들지 못했습니다. " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 결과 이미지에 적을 검사 정보를 모읍니다.
        ///
        /// <para>
        /// AI는 이미지 6장을 각각 검사해 방향마다 판정과 Score, 치수를 돌려줍니다.
        /// 그 값이 있으면 방향별 값을 씁니다. 예전에는 검사 단위로 합쳐진 값
        /// (판정은 6방향 AND, Score는 최대값) 하나를 6장에 공통으로 적어서,
        /// 서로 다른 방향의 이미지에 같은 숫자가 찍혔습니다.
        /// </para>
        ///
        /// <para>
        /// 방향별 값이 없으면 예전처럼 검사 단위 값으로 되돌아갑니다.
        /// AI가 결과를 주지 못한 경우나 검정 이미지로 대체된 방향이 여기에 해당합니다.
        /// </para>
        /// </summary>
        private InspectionImageResultInfo BuildResultInfo(
            Part part,
            Inspection inspection,
            AiInferenceResult inferenceResult,
            ImageViewType viewType)
        {
            InspectionImageResultInfo resultInfo = new InspectionImageResultInfo();
            resultInfo.PartNo = part.PartNo;
            resultInfo.PartName = part.PartName;
            resultInfo.InspectionStartedAt = inspection.InspectedAt;
            resultInfo.IsPass = inspection.Result == InspectionResult.Pass;
            resultInfo.ScoreThreshold = _inspectionPassScoreThreshold;

            if (inferenceResult == null)
            {
                return resultInfo;
            }

            AiViewInferenceResult viewResult = FindViewResult(inferenceResult, viewType);
            if (viewResult != null)
            {
                resultInfo.IsPass = viewResult.IsPass;
                resultInfo.HasScore = viewResult.HasScore;
                resultInfo.Score = GetDisplayScore(viewResult.Score);
                resultInfo.DimensionWidth = viewResult.DimensionWidth;
                resultInfo.DimensionDepth = viewResult.DimensionDepth;
                resultInfo.DimensionHeight = viewResult.DimensionHeight;
                resultInfo.DimensionUnit = viewResult.DimensionUnit;
                return resultInfo;
            }

            resultInfo.HasScore = inferenceResult.HasScore;
            resultInfo.Score = GetDisplayScore(inferenceResult.Confidence);
            resultInfo.DimensionWidth = inferenceResult.DimensionWidth;
            resultInfo.DimensionDepth = inferenceResult.DimensionDepth;
            resultInfo.DimensionHeight = inferenceResult.DimensionHeight;
            resultInfo.DimensionUnit = inferenceResult.DimensionUnit;

            return resultInfo;
        }

        /// <summary>
        /// 이 방향의 AI 결과를 찾습니다. 없으면 null을 돌려줍니다.
        /// </summary>
        private AiViewInferenceResult FindViewResult(AiInferenceResult inferenceResult, ImageViewType viewType)
        {
            if (inferenceResult.ViewResults == null)
            {
                return null;
            }

            AiViewInferenceResult viewResult;
            if (inferenceResult.ViewResults.TryGetValue(viewType, out viewResult))
            {
                return viewResult;
            }

            return null;
        }

        /// <summary>
        /// 화면과 이미지가 같은 Score를 보이도록 공용 규칙(InspectionScoreFormat)에 맡깁니다.
        /// </summary>
        private decimal GetDisplayScore(decimal confidence)
        {
            return InspectionScoreFormat.Normalize(confidence);
        }

        private string FindCapturedImagePath(IList<CapturedImage> capturedImages, ImageViewType viewType)
        {
            if (capturedImages == null)
            {
                return string.Empty;
            }

            foreach (CapturedImage image in capturedImages)
            {
                if (image != null && image.ViewType == viewType)
                {
                    return image.FilePath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// AI가 돌려준 측정값 개수가 DB에 등록된 측정부 개수와 다른지 확인해 경고를 남깁니다.
        ///
        /// 측정값은 요청한 측정부 순서대로 1:1 대응한다는 전제(요청 indexNo 1..N 연속, AI는 같은 개수 반환)로
        /// 순서대로 배정합니다. 개수가 어긋나면 적은 쪽에 맞춰 잘리면서 값이 엉뚱한 측정부에 들어가는데,
        /// 그대로 두면 오류 없이 통과하므로 여기서 눈에 보이게 남깁니다.
        ///
        /// <para>
        /// 세는 단위를 맞추는 것이 중요합니다. 등록 측정부는 여섯 카메라의 것을 모두 합한 수이므로,
        /// 견줄 쪽도 여섯 장에서 AI가 돌려준 measurements 를 모두 더한 수여야 합니다.
        /// 한 장의 개수나 측정부 번호로 묶은 뒤의 개수와 견주면, 아무 문제가 없는 검사에도
        /// 개수가 다르다는 경고가 남아 없는 문제를 있다고 오해하게 만듭니다.
        /// </para>
        /// </summary>
        private void WarnWhenMeasurementCountMismatched(Part part, AiInferenceResult inferenceResult, Inspection inspection)
        {
            if (part == null || part.MeasurementRegions == null || inferenceResult == null)
            {
                return;
            }

            int regionCount = part.MeasurementRegions.Count;
            if (regionCount == 0)
            {
                return;
            }

            // 여섯 장에서 AI 가 돌려준 측정값을 모두 더한 수입니다.
            int measuredCount = inferenceResult.AiReportedMeasurementCount;
            if (regionCount == measuredCount)
            {
                return;
            }

            if (measuredCount == 0)
            {
                AddEvent(
                    inspection,
                    EventSeverity.Warning,
                    "CompareReference",
                    "등록 측정부 " + regionCount.ToString(CultureInfo.InvariantCulture) +
                    "개에 대해 AI가 측정값을 하나도 돌려주지 않았습니다. " +
                    "학습된 정보가 없으면 이렇게 나올 수 있습니다. " +
                    "DB\\Logs의 vlad-hd-json 로그에서 measurements 배열을 확인하십시오.");
                return;
            }

            AddEvent(
                inspection,
                EventSeverity.Warning,
                "CompareReference",
                "등록 측정부 " + regionCount.ToString(CultureInfo.InvariantCulture) +
                "개와 AI 측정값 " + measuredCount.ToString(CultureInfo.InvariantCulture) +
                "개의 수가 다릅니다. 측정값이 측정부 순서대로 배정되므로 값이 밀려 들어갈 수 있습니다. " +
                "DB\\Logs의 vlad-hd-json 로그에서 measurements 배열을 확인하십시오.");
        }

        private void BuildFinalInspectionResult(Inspection inspection, AiInferenceResult inferenceResult, IList<MeasurementResult> measurements)
        {
            inspection.Result = _judgmentService.Judge(inferenceResult, measurements, _inspectionPassScoreThreshold);
            inspection.ResultMessage = _judgmentService.BuildResultMessage(
                inspection.Result,
                inferenceResult,
                measurements,
                _inspectionPassScoreThreshold);
            ApplyDimensions(inspection, inferenceResult);
            AddEvent(inspection, EventSeverity.Info, "BuildFinalInspectionResult", inspection.ResultMessage);
        }

        private void ApplyDimensions(Inspection inspection, AiInferenceResult inferenceResult)
        {
            if (inspection == null || inferenceResult == null)
            {
                return;
            }

            inspection.DimensionWidth = inferenceResult.DimensionWidth;
            inspection.DimensionDepth = inferenceResult.DimensionDepth;
            inspection.DimensionHeight = inferenceResult.DimensionHeight;
            inspection.DimensionUnit = inferenceResult.DimensionUnit;
        }

        /// <summary>
        /// AI 내부 Confidence(0~1)를 화면/Config가 사용하는 0~100 Score로 변환해 검사 이력 객체에 보관합니다.
        /// </summary>
        private void ApplyAiScore(Inspection inspection, AiInferenceResult inferenceResult)
        {
            if (inspection == null)
            {
                return;
            }

            inspection.AiScoreThreshold = _inspectionPassScoreThreshold;
            inspection.HasAiScore = inferenceResult != null && inferenceResult.HasScore;
            if (!inspection.HasAiScore)
            {
                inspection.AiScore = 0m;
                return;
            }

            decimal score = inferenceResult.Confidence;
            if (score >= 0m && score <= 1m)
            {
                score = score * 100m;
            }

            // AI 원본 Score는 보존 가능한 범위에서 소수점 둘째 자리로 통일해 이력과 UI에 전달합니다.
            inspection.AiScore = decimal.Round(score, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 검사 시점에 조회한 Part에서, 측정부가 있는 카메라의 기준 이미지 경로를
        /// coordinate 이미지로 바꿉니다. DB와 실제 이미지 파일은 건드리지 않습니다.
        ///
        /// <para>
        /// 이 경로는 결과 이미지의 배경으로만 씁니다. AI에는 좌표 선이 없는 원본 캡처 이미지가
        /// 그대로 가고, 좌표 값은 Mat JSON으로 따로 전달합니다. 선을 그린 이미지를 AI에 넣으면
        /// 없던 무늬가 판정에 섞이므로 그렇게 하지 않습니다.
        /// </para>
        ///
        /// <para>
        /// 측정부를 카메라마다 따로 두므로 Thickness만 바꾸면 Top 측정부가 있어도
        /// Top 결과 이미지에는 선이 나오지 않습니다. 그래서 카메라마다 돌립니다.
        /// </para>
        /// </summary>
        /// <returns>바꾼 경로들입니다. 하나도 없으면 빈 문자열입니다.</returns>
        private string ReplaceMeasurementReferencePathsWithCoordinate(Part part)
        {
            if (part == null || part.MeasurementRegions.Count == 0)
            {
                return string.Empty;
            }

            List<string> replacedPaths = new List<string>();
            foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                // 그 카메라에 측정부가 없으면 좌표 이미지도 없습니다.
                if (FilterRegionsByViewType(part, viewType).Count == 0)
                {
                    continue;
                }

                string replacedPath = ReplaceReferencePathWithCoordinate(part, viewType);
                if (!string.IsNullOrWhiteSpace(replacedPath))
                {
                    replacedPaths.Add(replacedPath);
                }
            }

            return string.Join(", ", replacedPaths.ToArray());
        }

        private string ReplaceReferencePathWithCoordinate(Part part, ImageViewType viewType)
        {
            PartImage referenceImage = FindReferenceImage(part, viewType);
            if (referenceImage == null || string.IsNullOrWhiteSpace(referenceImage.FilePath))
            {
                return string.Empty;
            }

            string imageDirectoryPath = Path.GetDirectoryName(referenceImage.FilePath);
            if (string.IsNullOrWhiteSpace(imageDirectoryPath))
            {
                return string.Empty;
            }

            string coordinateImagePath = ReferenceImageFileNamePolicy.FindCoordinateFilePath(
                imageDirectoryPath, viewType, part.PartNo);
            if (string.IsNullOrWhiteSpace(coordinateImagePath))
            {
                return string.Empty;
            }

            referenceImage.FilePath = coordinateImagePath;
            return coordinateImagePath;
        }

        /// <summary>
        /// 그 카메라의 기준 이미지를 고릅니다. 벌이 여러 개면 가장 최근 것을 씁니다.
        /// </summary>
        private PartImage FindReferenceImage(Part part, ImageViewType viewType)
        {
            return part == null
                ? null
                : ReferenceImageFileNamePolicy.FindLatestByViewType(part.Images, viewType);
        }

        private Inspection CreateInspectionShell(string inputCode)
        {
            Inspection inspection = new Inspection();
            inspection.Id = _inspectionRepository.GetNextId();
            inspection.InputCode = inputCode;
            inspection.Result = InspectionResult.NotInspected;
            return inspection;
        }

        private void CopyImages(Inspection inspection, IList<CapturedImage> capturedImages)
        {
            foreach (CapturedImage image in capturedImages)
            {
                inspection.Images.Add(image);
            }
        }

        /// <summary>
        /// MAT API의 방향별 판정을 검사 결과와 함께 UI까지 전달합니다.
        /// 원본 AI 결과 객체와 참조를 공유하지 않도록 복사하며, 전체 Result/AiScore는 별도의 최종 요약값으로 유지합니다.
        /// </summary>
        private void CopyViewResults(Inspection inspection, AiInferenceResult inferenceResult)
        {
            if (inspection == null || inferenceResult == null || inferenceResult.ViewResults == null)
            {
                return;
            }

            foreach (KeyValuePair<ImageViewType, AiViewInferenceResult> pair in inferenceResult.ViewResults)
            {
                AiViewInferenceResult source = pair.Value;
                if (source == null)
                {
                    continue;
                }

                AiViewInferenceResult copied = new AiViewInferenceResult();
                copied.ViewType = source.ViewType;
                copied.IsPass = source.IsPass;
                copied.Score = source.Score;
                copied.HasScore = source.HasScore;
                copied.DimensionWidth = source.DimensionWidth;
                copied.DimensionDepth = source.DimensionDepth;
                copied.DimensionHeight = source.DimensionHeight;
                copied.DimensionUnit = source.DimensionUnit;
                inspection.ViewResults[pair.Key] = copied;
            }
        }

        private void CopyMeasurements(Inspection inspection, IList<MeasurementResult> measurements)
        {
            foreach (MeasurementResult measurement in measurements)
            {
                inspection.Measurements.Add(measurement);
            }
        }

        private void SaveInspection(Inspection inspection, Stopwatch stopwatch)
        {
            stopwatch.Stop();
            inspection.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            TryMergeInspectionImages(inspection);
            _fileStorageService.StoreInspection(inspection);
            _inspectionRepository.Save(inspection);
        }

        /// <summary>
        /// 한 검사에서 촬영한 6방향 이미지를 품번 이름의 한 이미지로 병합합니다.
        /// 병합 실패는 검사 결과와 원본 6장 저장을 취소하지 않고 이력 이벤트로 남깁니다.
        /// </summary>
        private void TryMergeInspectionImages(Inspection inspection)
        {
            if (_imageMergeService == null || inspection == null || inspection.Images.Count == 0)
            {
                return;
            }

            string mergedFilePath;
            string mergeMessage;
            bool merged = _imageMergeService.TryMergeInspectionImages(
                inspection,
                out mergedFilePath,
                out mergeMessage);
            AddEvent(
                inspection,
                merged ? EventSeverity.Info : EventSeverity.Warning,
                "ImageMerge",
                mergeMessage);
        }

        private void TrySaveInspection(Inspection inspection, Stopwatch stopwatch)
        {
            try
            {
                SaveInspection(inspection, stopwatch);
            }
            catch (Exception ex)
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }

                inspection.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                AddEvent(inspection, EventSeverity.Error, "History", "검사 이력 저장 중 오류가 발생했습니다. " + ex.Message);

                try
                {
                    _inspectionRepository.Save(inspection);
                }
                catch (Exception repositoryException)
                {
                    Debug.WriteLine("검사 이력 DB 저장 실패: " + repositoryException.Message);
                }
            }
        }

        private void AddEvent(Inspection inspection, EventSeverity severity, string source, string message)
        {
            EventLogEntry entry = new EventLogEntry();
            entry.Severity = severity;
            entry.Source = source;
            entry.Message = message;
            inspection.Events.Add(entry);
        }
    }
}
