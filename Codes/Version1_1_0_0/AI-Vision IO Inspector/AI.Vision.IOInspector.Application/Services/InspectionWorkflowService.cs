using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly MeasurementService _measurementService;
        private readonly JudgmentService _judgmentService;
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
            MeasurementService measurementService,
            JudgmentService judgmentService)
        {
            _partRepository = partRepository;
            _inspectionRepository = inspectionRepository;
            _cameraService = cameraService;
            _aiInferenceService = aiInferenceService;
            _fileStorageService = fileStorageService;
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
                inspection.PartType = part.PartType;

                string coordinateImagePath = ReplaceThicknessReferencePathWithCoordinate(part);
                if (!string.IsNullOrWhiteSpace(coordinateImagePath))
                {
                    AddEvent(
                        inspection,
                        EventSeverity.Info,
                        "ReferenceImage",
                        "측정부 검사용 Thickness 경로를 coordinate 이미지로 연결했습니다. " +
                        coordinateImagePath);
                }
                else if (part.MeasurementRegions.Count > 0)
                {
                    AddEvent(
                        inspection,
                        EventSeverity.Warning,
                        "ReferenceImage",
                        "측정부가 등록되어 있지만 coordinate 이미지를 찾지 못해 기존 Thickness 이미지를 사용합니다.");
                }

                ReportProgress(InspectionStatus.Capturing, "카메라 최신 프레임을 검사 이미지로 저장하고 있습니다.");
                IList<CapturedImage> capturedImages = CaptureAll(part, inspection);
                ReportProgress(InspectionStatus.Inferencing, "캡처 이미지를 AI에서 검사하고 있습니다.");
                AiInferenceResult inferenceResult = RunAiInspection(part, capturedImages, inspection);
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
            EventHandler<InspectionProgressEventArgs> handler = ProgressChanged;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(this, new InspectionProgressEventArgs(status, message));
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
            return capturedImages;
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
            IList<MeasurementResult> measurements = _measurementService.CompareMeasurements(part, inferenceResult);
            CopyMeasurements(inspection, measurements);
            AddEvent(inspection, EventSeverity.Info, "CompareReference", "측정부 결과 " + measurements.Count + "개를 생성했습니다.");
            return measurements;
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
        /// 검사 시점에 조회한 Part의 Thickness 이미지 경로만 coordinate 이미지로 변경합니다.
        /// DB 데이터와 실제 이미지 파일은 수정하지 않습니다.
        /// </summary>
        private string ReplaceThicknessReferencePathWithCoordinate(Part part)
        {
            if (part == null || part.MeasurementRegions.Count == 0)
            {
                return string.Empty;
            }

            PartImage thicknessImage = FindReferenceImage(part, ImageViewType.Thickness);
            if (thicknessImage == null || string.IsNullOrWhiteSpace(thicknessImage.FilePath))
            {
                return string.Empty;
            }

            string imageDirectoryPath = Path.GetDirectoryName(thicknessImage.FilePath);
            if (string.IsNullOrWhiteSpace(imageDirectoryPath))
            {
                return string.Empty;
            }

            string coordinateImagePath = Path.Combine(imageDirectoryPath, ReferenceImageFileNamePolicy.BuildCoordinateFileName(part.PartNo));
            if (!File.Exists(coordinateImagePath))
            {
                coordinateImagePath = Path.Combine(imageDirectoryPath, ReferenceImageFileNamePolicy.LegacyCoordinateFileName);
                if (!File.Exists(coordinateImagePath))
                {
                    return string.Empty;
                }
            }

            thicknessImage.FilePath = coordinateImagePath;
            return coordinateImagePath;
        }

        private PartImage FindReferenceImage(Part part, ImageViewType viewType)
        {
            foreach (PartImage image in part.Images)
            {
                if (image != null && image.ViewType == viewType)
                {
                    return image;
                }
            }

            return null;
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
            _fileStorageService.StoreInspection(inspection);
            _inspectionRepository.Save(inspection);
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
