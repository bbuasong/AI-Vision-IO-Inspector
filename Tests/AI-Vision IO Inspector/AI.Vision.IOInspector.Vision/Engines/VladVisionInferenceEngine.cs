using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using AI.Vision.IOInspector.Vision.Models;
using AI.Vision.IOInspector.Vision.Services;

namespace AI.Vision.IOInspector.Vision.Engines
{
    /// <summary>
    /// VLAD_SDK 기반 AI 추론 엔진입니다.
    /// 촬영된 이미지 파일을 OpenCV Mat로 변환한 뒤 기존 VLAD_Ops 함수명과 같은 경로로 VLAD_Inference_Mat을 호출합니다.
    /// </summary>
    public class VladVisionInferenceEngine : IVisionInferenceEngine
    {
        private readonly object _syncRoot;
        private readonly MeasurementCalibrationService _calibrationService;
        private readonly VladInferenceResultParser _resultParser;
        private readonly VladMeasurementMapper _measurementMapper;
        private IntPtr _vladId;
        private readonly VladCamModeRuntime _camModeRuntime;
        private readonly VladVisionSettings _settings;
        private long _inspectionRequestSequence;

        public VladVisionInferenceEngine(string applicationRootPath, VladCamModeRuntime camModeRuntime)
        {
            _syncRoot = new object();
            _calibrationService = new MeasurementCalibrationService(applicationRootPath);
            _resultParser = new VladInferenceResultParser();
            _measurementMapper = new VladMeasurementMapper(_calibrationService);

            _camModeRuntime = camModeRuntime ?? throw new ArgumentNullException(nameof(camModeRuntime));
            _settings = _camModeRuntime.Settings;
        }

        public VisionInspectionOutput Inspect(VisionInspectionInput input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (input.Part == null)
            {
                return CreateFailure("검사 대상 부품 정보가 없습니다.");
            }

            try
            {
                long requestSequence = Interlocked.Increment(ref _inspectionRequestSequence);
                Debug.WriteLine("InspectCapturedImages 요청 시작. Sequence=" +
                    requestSequence.ToString(CultureInfo.InvariantCulture) +
                    ", PartNo=" +
                    input.Part.PartNo);
                EnsureRegistered();
                VLAD_Ops_RTSP.StartFrameProcessing();
                try
                {
                    return InspectCapturedImages(input, requestSequence);
                }
                finally
                {
                    VLAD_Ops_RTSP.StopFrameProcessing("검사 요청 처리 완료. Sequence=" + requestSequence.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex)
            {
                return CreateFailure("VLAD 추론 준비 또는 실행 실패: " + ex.Message);
            }
        }

        public IntPtr InspectMat(IntPtr rawMatPointer, float threshold, int drawMode)
        {
            return InspectMat(rawMatPointer, threshold, drawMode, null, null);
        }

        public IntPtr InspectMat(IntPtr rawMatPointer, float threshold, int drawMode, VisionInspectionInput input, CapturedImage capturedImage)
        {
            TraceInferenceReadinessDiagnostics();
            EnsureRegistered();
            if (rawMatPointer == IntPtr.Zero)
            {
                throw new ArgumentException("OpenCV Mat 포인터가 비어 있습니다.", "rawMatPointer");
            }

            string inspectionContextJson = BuildInspectionContextJson(input, capturedImage);
            lock (VLAD_Ops_Ai.NativeInferenceSyncRoot)
            {
                return VLAD_Ops_Ai.VLAD_Inference_Mat(_vladId, rawMatPointer, threshold, drawMode, inspectionContextJson);
            }
        }

        public string StartImageTraining()
        {
            EnsureRegistered();

            // AI 담당자가 VLAD DLL 내부 학습 함수를 제공하면 이 지점에서 실제 네이티브 호출로 연결합니다.
            string message = VLAD_Ops_Ai.StartImageTraining(_vladId);
            Debug.WriteLine(message);
            return message;
        }

        private string BuildInspectionContextJson(VisionInspectionInput input, CapturedImage capturedImage)
        {
            if (input == null)
            {
                return "{}";
            }

            StringBuilder builder = new StringBuilder();
            bool hasProperty = false;
            Part part = input.Part;

            builder.Append("{");
            AppendJsonStringProperty(builder, "partNo", part == null ? string.Empty : part.PartNo, ref hasProperty);
            AppendJsonStringProperty(builder, "partName", part == null ? string.Empty : part.PartName, ref hasProperty);
            AppendJsonStringProperty(builder, "categoryCode", part == null ? string.Empty : part.CategoryCode, ref hasProperty);
            AppendJsonStringProperty(builder, "categoryDescription", part == null ? string.Empty : part.CategoryDescription, ref hasProperty);
            AppendJsonStringProperty(builder, "partType", part == null ? string.Empty : part.PartType, ref hasProperty);
            AppendJsonStringProperty(builder, "capturedViewType", capturedImage == null ? string.Empty : capturedImage.ViewType.ToString(), ref hasProperty);
            AppendJsonStringProperty(builder, "capturedImagePath", capturedImage == null ? string.Empty : capturedImage.FilePath, ref hasProperty);

            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"measurements\":[");
            bool hasMeasurement = false;
            if (input.MeasurementPoints != null)
            {
                foreach (VisionMeasurementPointInput point in input.MeasurementPoints)
                {
                    if (point == null)
                    {
                        continue;
                    }

                    AppendJsonComma(builder, ref hasMeasurement);
                    builder.Append("{");
                    bool hasMeasurementProperty = false;
                    AppendJsonNumberProperty(builder, "measurementRegionId", point.MeasurementRegionId, ref hasMeasurementProperty);
                    AppendJsonNumberProperty(builder, "indexNo", point.IndexNo, ref hasMeasurementProperty);
                    AppendJsonStringProperty(builder, "itemType", point.ItemType, ref hasMeasurementProperty);
                    AppendJsonStringProperty(builder, "viewType", point.ViewType.ToString(), ref hasMeasurementProperty);
                    AppendJsonStringProperty(builder, "lineColor", point.LineColor, ref hasMeasurementProperty);
                    AppendJsonDecimalProperty(builder, "nominalValue", point.NominalValue, ref hasMeasurementProperty);
                    AppendJsonDecimalProperty(builder, "toleranceMin", point.ToleranceMin, ref hasMeasurementProperty);
                    AppendJsonDecimalProperty(builder, "toleranceMax", point.ToleranceMax, ref hasMeasurementProperty);
                    AppendJsonDecimalProperty(builder, "tolerance", point.Tolerance, ref hasMeasurementProperty);
                    AppendJsonNullableDoubleProperty(builder, "x1", point.X1, ref hasMeasurementProperty);
                    AppendJsonNullableDoubleProperty(builder, "y1", point.Y1, ref hasMeasurementProperty);
                    AppendJsonNullableDoubleProperty(builder, "x2", point.X2, ref hasMeasurementProperty);
                    AppendJsonNullableDoubleProperty(builder, "y2", point.Y2, ref hasMeasurementProperty);
                    AppendJsonStringProperty(builder, "unit", point.Unit, ref hasMeasurementProperty);
                    builder.Append("}");
                }
            }

            builder.Append("]}");
            return builder.ToString();
        }

        private void AppendJsonStringProperty(StringBuilder builder, string propertyName, string value, ref bool hasProperty)
        {
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"");
            builder.Append(propertyName);
            builder.Append("\":\"");
            builder.Append(EscapeJson(value));
            builder.Append("\"");
        }

        private void AppendJsonNumberProperty(StringBuilder builder, string propertyName, int value, ref bool hasProperty)
        {
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"");
            builder.Append(propertyName);
            builder.Append("\":");
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private void AppendJsonDecimalProperty(StringBuilder builder, string propertyName, decimal value, ref bool hasProperty)
        {
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"");
            builder.Append(propertyName);
            builder.Append("\":");
            builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void AppendJsonNullableDoubleProperty(StringBuilder builder, string propertyName, double? value, ref bool hasProperty)
        {
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"");
            builder.Append(propertyName);
            builder.Append("\":");
            builder.Append(value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "null");
        }

        private void AppendJsonComma(StringBuilder builder, ref bool hasProperty)
        {
            if (hasProperty)
            {
                builder.Append(",");
            }

            hasProperty = true;
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private VisionInspectionOutput InspectCapturedImages(VisionInspectionInput input, long requestSequence)
        {
            IList<CapturedImage> capturedImages = GetValidCapturedImages(input);
            if (capturedImages.Count == 0)
            {
                return CreateFailure("VLAD 추론에 사용할 촬영 이미지가 없습니다.");
            }

            List<VladDetection> allDetections = new List<VladDetection>();
            StringBuilder detectTextBuilder = new StringBuilder();
            int processedCount = 0;
            decimal highestConfidence = 0m;

            foreach (CapturedImage capturedImage in capturedImages)
            {
                using (OpenCvSharpMatImage matImage = OpenCvSharpMatImage.LoadFromFile(capturedImage.FilePath))
                {
                    VladInferenceResult result;
                    lock (VLAD_Ops_Ai.NativeInferenceSyncRoot)
                    {
                        IntPtr detectData = InspectMat(matImage.CvPtr, _settings.Threshold, 0, input, capturedImage);
                        if (detectData == IntPtr.Zero)
                        {
                            throw new InvalidOperationException("VLAD_Inference_Mat이 빈 detectData를 반환했습니다. 모델/GPU/입력 이미지 구성을 확인하십시오.");
                        }

                        // 결과값 파싱
                        result = _resultParser.Parse(_vladId, detectData, matImage.CvPtr, capturedImage.ViewType, capturedImage.FilePath);
                    }

                    AppendResultText(detectTextBuilder, capturedImage, result);
                    CopyDetections(allDetections, result.Detections);
                    highestConfidence = GetHighestConfidence(highestConfidence, result.Detections);
                    processedCount++;
                }
            }

            VisionInspectionOutput output = new VisionInspectionOutput();
            output.IsSuccess = true;
            output.IsMatched = allDetections.Count == 0;
            output.PredictedClass = BuildPredictedClass(input.Part, allDetections);
            output.Confidence = highestConfidence > 0m ? highestConfidence : 1m;
            output.Message = BuildMessage(processedCount, allDetections.Count, detectTextBuilder.ToString());
            output.ModelVersion = "VLAD";

            IList<VisionMeasurementValue> measurements = _measurementMapper.BuildMeasurements(input, allDetections, detectTextBuilder.ToString());
            foreach (VisionMeasurementValue measurement in measurements)
            {
                output.Measurements.Add(measurement);
            }

            Debug.WriteLine(
                "InspectCapturedImages 요청 완료. Sequence=" +
                requestSequence.ToString(CultureInfo.InvariantCulture) +
                ", ProcessedImages=" +
                processedCount.ToString(CultureInfo.InvariantCulture));
            return output;
        }

        private IList<CapturedImage> GetValidCapturedImages(VisionInspectionInput input)
        {
            List<CapturedImage> images = new List<CapturedImage>();
            if (input.CapturedImages == null)
            {
                return images;
            }

            foreach (CapturedImage image in input.CapturedImages)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                if (!File.Exists(image.FilePath))
                {
                    continue;
                }

                images.Add(image);
            }

            return images;
        }

        private void AppendResultText(StringBuilder builder, CapturedImage image, VladInferenceResult result)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("[");
            builder.Append(image.ViewType);
            builder.Append("] ");

            if (result == null || string.IsNullOrWhiteSpace(result.DetectText))
            {
                builder.Append("DetectText 없음");
                return;
            }

            builder.Append(result.DetectText);
        }

        private void CopyDetections(List<VladDetection> target, IList<VladDetection> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (VladDetection detection in source)
            {
                if (detection != null)
                {
                    target.Add(detection);
                }
            }
        }

        private decimal GetHighestConfidence(decimal currentHighest, IList<VladDetection> detections)
        {
            decimal highest = currentHighest;
            if (detections == null)
            {
                return highest;
            }

            foreach (VladDetection detection in detections)
            {
                if (detection != null && detection.Score > highest)
                {
                    highest = detection.Score;
                }
            }

            return highest;
        }

        private string BuildPredictedClass(Part part, IList<VladDetection> detections)
        {
            if (detections == null || detections.Count == 0)
            {
                return part.PartName;
            }

            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (VladDetection detection in detections)
            {
                string name = detection.ClassName;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "Class" + detection.ClassId.ToString(CultureInfo.InvariantCulture);
                }

                if (!counts.ContainsKey(name))
                {
                    counts[name] = 0;
                }

                counts[name]++;
            }

            string bestName = string.Empty;
            int bestCount = -1;
            foreach (KeyValuePair<string, int> pair in counts)
            {
                if (pair.Value > bestCount)
                {
                    bestName = pair.Key;
                    bestCount = pair.Value;
                }
            }

            return bestName;
        }

        private string BuildMessage(int processedCount, int detectionCount, string detectText)
        {
            string message = "VLAD 추론 완료. 처리 이미지 " +
                processedCount.ToString(CultureInfo.InvariantCulture) +
                "장, 검출 " +
                detectionCount.ToString(CultureInfo.InvariantCulture) +
                "건.";

            if (!string.IsNullOrWhiteSpace(detectText))
            {
                message = message + " " + detectText;
            }

            return message;
        }

        private VisionInspectionOutput CreateFailure(string message)
        {
            VisionInspectionOutput output = new VisionInspectionOutput();
            output.IsSuccess = false;
            output.IsMatched = false;
            output.PredictedClass = string.Empty;
            output.Confidence = 0m;
            output.Message = message;
            output.ModelVersion = "VLAD";
            return output;
        }

        private void EnsureRegistered()
        {
            lock (_syncRoot)
            {
                if (_vladId != IntPtr.Zero)
                {
                    return;
                }

                TraceInferenceReadinessDiagnostics();

                // 공유 세션을 통해 원본 VLAD_Ops의 전역 Vlad_id 흐름과 같은 형태로 한 번만 등록합니다.
                _vladId = _camModeRuntime.EnsureLoaded().VladId;

                if (_vladId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD_Ops_Ai_Env_Start가 빈 VLAD_ID를 반환했습니다. 모델 경로와 VLAD 런타임 DLL 구성을 확인하세요.");
                }
            }
        }

        private void TraceInferenceReadinessDiagnostics()
        {
            string diagnosticMessage = BuildInferenceReadinessDiagnosticMessage();
            if (!string.IsNullOrWhiteSpace(diagnosticMessage))
            {
                Debug.WriteLine(diagnosticMessage);
            }
        }

        private string BuildInferenceReadinessDiagnosticMessage()
        {
            if (string.IsNullOrWhiteSpace(_settings.ModelPath))
            {
                return _settings.BuildModelPathMissingMessage();
            }

            VladModelPathInspection inspection = VladModelPathInspector.Inspect(_settings.ModelPath);
            if (!inspection.PathExists)
            {
                return _settings.BuildModelPathMissingMessage();
            }

            if (!inspection.IsLoadableCandidate)
            {
                string diagnosticMessage = VladModelPathInspector.BuildDiagnosticMessage(_settings.ModelPath);
                if (!string.IsNullOrWhiteSpace(diagnosticMessage))
                {
                    return diagnosticMessage;
                }

                return "VLAD 모델 경로가 추론 가능한 구조인지 확인되지 않았습니다. 현재 경로: " + _settings.ModelPath;
            }

            return string.Empty;
        }
    }
}
