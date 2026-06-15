using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure;
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
        private readonly string _projectRootPath;
        private readonly MeasurementCalibrationService _calibrationService;
        private readonly VladInferenceResultParser _resultParser;
        private readonly VladMeasurementMapper _measurementMapper;
        private IntPtr _vladId;
        private readonly VladSdkSession _vladSdkSession;
        private readonly VladVisionSettings _settings;

        public VladVisionInferenceEngine(string applicationRootPath, VladSdkSession vladSdkSession, VladVisionSettings settings)
        {
            _syncRoot = new object();
            _projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            _calibrationService = new MeasurementCalibrationService(applicationRootPath);
            _resultParser = new VladInferenceResultParser();
            _measurementMapper = new VladMeasurementMapper(_calibrationService);

            _vladSdkSession = vladSdkSession ?? throw new ArgumentNullException(nameof(vladSdkSession));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
                EnsureRegistered();
                return InspectCapturedImages(input);
            }
            catch (Exception ex)
            {
                return CreateFailure("VLAD 추론 준비 또는 실행 실패: " + ex.Message);
            }
        }

        public IntPtr InspectMat(IntPtr rawMatPointer, float threshold, int drawMode)
        {
            EnsureInferenceReadinessOrThrow();
            EnsureRegistered();
            if (rawMatPointer == IntPtr.Zero)
            {
                throw new ArgumentException("OpenCV Mat 포인터가 비어 있습니다.", "rawMatPointer");
            }

            return VLAD_Ops_Ai.VLAD_Inference_Mat(_vladId, rawMatPointer, threshold, drawMode);
        }

        private VisionInspectionOutput InspectCapturedImages(VisionInspectionInput input)
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
                    IntPtr detectData = InspectMat(matImage.CvPtr, _settings.Threshold, 0);
                    VladInferenceResult result = _resultParser.Parse(
                        _vladId,
                        detectData,
                        matImage.CvPtr,
                        capturedImage.ViewType,
                        capturedImage.FilePath);

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

            IList<VisionMeasurementValue> measurements = _measurementMapper.BuildMeasurements(
                input,
                allDetections,
                detectTextBuilder.ToString());
            foreach (VisionMeasurementValue measurement in measurements)
            {
                output.Measurements.Add(measurement);
            }

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

                EnsureInferenceReadinessOrThrow();

                // 공유 세션을 통해 원본 VLAD_Ops의 전역 Vlad_id 흐름과 같은 형태로 한 번만 등록합니다.
                _vladId = _vladSdkSession.EnsureStarted(
                    (int)SDK_USER.USER_CUS_STD,
                    _settings.RootName,
                    _settings.SiteName,
                    (int)SDK_MSG.MSG_V1,
                    (int)SDK_MAJ.MAJ_V1,
                    _settings.ModelPath,
                    _settings.GpuId);

                if (_vladId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD_Ops_Ai_Env_Start가 빈 VLAD_ID를 반환했습니다. 모델 경로와 VLAD 런타임 DLL 구성을 확인하세요.");
                }
            }
        }

        private void EnsureInferenceReadinessOrThrow()
        {
            string failureMessage = BuildInferenceReadinessFailureMessage();
            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                throw new InvalidOperationException(failureMessage);
            }
        }

        private string BuildInferenceReadinessFailureMessage()
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

            if (!inspection.IsLoadableCandidate && !IsUnverifiedModelAllowed())
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

        private bool IsUnverifiedModelAllowed()
        {
            string value = Environment.GetEnvironmentVariable("AI_VISION_VLAD_ALLOW_UNVERIFIED_MODEL");
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private VladEngineSettings LoadSettings()
        {
            VladEngineSettings settings = new VladEngineSettings();
            settings.RootName = "CAM";
            settings.SiteName = "HD";
            settings.GpuId = 0;
            settings.Threshold = 0.5f;

            string configPath = Path.Combine(_projectRootPath, "CFG", "Config.json");
            if (File.Exists(configPath))
            {
                string text = File.ReadAllText(configPath);
                settings.RootName = ExtractJsonText(text, "LAST_MODE", settings.RootName);
                settings.SiteName = ExtractJsonText(text, "LAST_USER", settings.SiteName);
                settings.ModelPath = ExtractJsonText(text, "MODEL", settings.ModelPath);
                settings.RootName = ExtractJsonText(text, "ROOT_NAME", settings.RootName);
                settings.SiteName = ExtractJsonText(text, "SITE_NAME", settings.SiteName);
                settings.Threshold = ExtractJsonFloat(text, "THRESHOLD", settings.Threshold);
            }

            ApplyEnvironmentSettings(settings);
            return settings;
        }

        private void ApplyEnvironmentSettings(VladEngineSettings settings)
        {
            string modelPathFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_VLAD_MODEL_PATH");
            if (!string.IsNullOrWhiteSpace(modelPathFromEnvironment))
            {
                settings.ModelPath = modelPathFromEnvironment;
            }

            string siteNameFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_VLAD_SITE");
            if (!string.IsNullOrWhiteSpace(siteNameFromEnvironment))
            {
                settings.SiteName = siteNameFromEnvironment;
            }

            string rootNameFromEnvironment = Environment.GetEnvironmentVariable("AI_VISION_VLAD_ROOT");
            if (!string.IsNullOrWhiteSpace(rootNameFromEnvironment))
            {
                settings.RootName = rootNameFromEnvironment;
            }

            int gpuId;
            string gpuIdText = Environment.GetEnvironmentVariable("AI_VISION_VLAD_GPU");
            if (!string.IsNullOrWhiteSpace(gpuIdText) && int.TryParse(gpuIdText, out gpuId))
            {
                settings.GpuId = gpuId;
            }

            float threshold;
            string thresholdText = Environment.GetEnvironmentVariable("AI_VISION_VLAD_THRESHOLD");
            if (!string.IsNullOrWhiteSpace(thresholdText) &&
                float.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold))
            {
                settings.Threshold = threshold;
            }
        }

        private string ExtractJsonText(string text, string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return defaultValue;
            }

            int colonIndex = text.IndexOf(':', keyIndex);
            if (colonIndex < 0)
            {
                return defaultValue;
            }

            int firstQuoteIndex = text.IndexOf('"', colonIndex + 1);
            if (firstQuoteIndex < 0)
            {
                return defaultValue;
            }

            int secondQuoteIndex = text.IndexOf('"', firstQuoteIndex + 1);
            if (secondQuoteIndex < 0)
            {
                return defaultValue;
            }

            return text.Substring(firstQuoteIndex + 1, secondQuoteIndex - firstQuoteIndex - 1);
        }

        private float ExtractJsonFloat(string text, string key, float defaultValue)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            string pattern = "\"" + key + "\"";
            int keyIndex = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0)
            {
                return defaultValue;
            }

            int colonIndex = text.IndexOf(':', keyIndex);
            if (colonIndex < 0)
            {
                return defaultValue;
            }

            int valueStart = colonIndex + 1;
            while (valueStart < text.Length && char.IsWhiteSpace(text[valueStart]))
            {
                valueStart++;
            }

            int valueEnd = valueStart;
            while (valueEnd < text.Length &&
                   (char.IsDigit(text[valueEnd]) || text[valueEnd] == '.' || text[valueEnd] == '-'))
            {
                valueEnd++;
            }

            if (valueEnd <= valueStart)
            {
                return defaultValue;
            }

            float value;
            string valueText = text.Substring(valueStart, valueEnd - valueStart);
            if (!float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return defaultValue;
            }

            return value;
        }

        private class VladEngineSettings
        {
            public string RootName { get; set; }

            public string SiteName { get; set; }

            public string ModelPath { get; set; }

            public int GpuId { get; set; }

            public float Threshold { get; set; }
        }
    }
}
