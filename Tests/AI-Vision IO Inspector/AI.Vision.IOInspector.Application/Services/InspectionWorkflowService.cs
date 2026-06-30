using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;
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
                AddEvent(inspection, EventSeverity.Info, "Workflow", "기준정보 조회를 시작합니다.");
                Part part = _partRepository.GetByPartNo(inputCode);
                if (part == null)
                {
                    inspection.Result = InspectionResult.Error;
                    inspection.ResultMessage = "입력값과 일치하는 부품 기준정보가 없습니다.";
                    AddEvent(inspection, EventSeverity.Error, "PartRepository", inspection.ResultMessage);
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

                IList<CapturedImage> capturedImages = CaptureAll(part, inspection);
                AiInferenceResult inferenceResult = RunAiInspection(part, capturedImages, inspection);
                if (!inferenceResult.IsSuccess)
                {
                    inspection.Result = InspectionResult.Error;
                    inspection.ResultMessage = inferenceResult.Message;
                    TrySaveInspection(inspection, stopwatch);
                    return inspection;
                }

                IList<MeasurementResult> measurements = CompareReference(part, inferenceResult, inspection);
                BuildFinalInspectionResult(inspection, inferenceResult, measurements);

                TrySaveInspection(inspection, stopwatch);
                return inspection;
            }
            catch (Exception ex)
            {
                inspection.Result = InspectionResult.Error;
                inspection.ResultMessage = "검사 중 시스템 오류가 발생했습니다: " + ex.Message;
                AddEvent(inspection, EventSeverity.Error, "System", inspection.ResultMessage);
                TrySaveInspection(inspection, stopwatch);
                return inspection;
            }
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
            AddEvent(inspection, EventSeverity.Info, "CompareReference", "AI가 반환한 측정정보를 DB 기준값/허용값과 비교합니다.");
            IList<MeasurementResult> measurements = _measurementService.CompareMeasurements(part, inferenceResult);
            CopyMeasurements(inspection, measurements);
            AddEvent(inspection, EventSeverity.Info, "CompareReference", "기준값 비교 결과 " + measurements.Count + "개를 생성했습니다.");
            return measurements;
        }

        private void BuildFinalInspectionResult(Inspection inspection, AiInferenceResult inferenceResult, IList<MeasurementResult> measurements)
        {
            inspection.Result = _judgmentService.Judge(inferenceResult, measurements);
            inspection.ResultMessage = _judgmentService.BuildResultMessage(inspection.Result, inferenceResult, measurements);
            AddEvent(inspection, EventSeverity.Info, "BuildFinalInspectionResult", inspection.ResultMessage);
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
