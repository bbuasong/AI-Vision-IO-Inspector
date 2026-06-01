using System;
using System.Collections.Generic;
using System.Diagnostics;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

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
                    SaveInspection(inspection, stopwatch);
                    return inspection;
                }

                inspection.PartNo = part.PartNo;
                inspection.PartName = part.PartName;
                inspection.CategoryCode = part.CategoryCode;
                inspection.CategoryDescription = part.CategoryDescription;
                inspection.PartType = part.PartType;

                // 카메라 촬영 단계는 실제 SDK가 들어와도 이 경계만 유지하면 UI와 판정 로직은 그대로 재사용할 수 있습니다.
                AddEvent(inspection, EventSeverity.Info, "Camera", "6방향 이미지 촬영을 시작합니다.");
                IList<CapturedImage> capturedImages = _cameraService.CaptureAll(part);
                CopyImages(inspection, capturedImages);

                AddEvent(inspection, EventSeverity.Info, "AI", "AI 추론을 시작합니다.");
                AiInferenceResult inferenceResult = _aiInferenceService.Inspect(part, capturedImages);
                if (!inferenceResult.IsSuccess)
                {
                    inspection.Result = InspectionResult.Error;
                    inspection.ResultMessage = inferenceResult.Message;
                    AddEvent(inspection, EventSeverity.Error, "AI", inferenceResult.Message);
                    SaveInspection(inspection, stopwatch);
                    return inspection;
                }

                AddEvent(inspection, EventSeverity.Info, "Measurement", "측정부 기준값과 측정값을 비교합니다.");
                IList<MeasurementResult> measurements = _measurementService.CompareMeasurements(part, inferenceResult);
                CopyMeasurements(inspection, measurements);

                inspection.Result = _judgmentService.Judge(inferenceResult, measurements);
                inspection.ResultMessage = _judgmentService.BuildResultMessage(inspection.Result, inferenceResult, measurements);
                AddEvent(inspection, EventSeverity.Info, "Judgment", inspection.ResultMessage);

                SaveInspection(inspection, stopwatch);
                return inspection;
            }
            catch (Exception ex)
            {
                inspection.Result = InspectionResult.Error;
                inspection.ResultMessage = "검사 중 시스템 오류가 발생했습니다: " + ex.Message;
                AddEvent(inspection, EventSeverity.Error, "System", inspection.ResultMessage);
                SaveInspection(inspection, stopwatch);
                return inspection;
            }
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
