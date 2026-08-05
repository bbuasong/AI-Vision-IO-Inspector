using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Domain.Enums;
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
    public class VladVisionInferenceEngine : IVisionInferenceEngine, IDisposable
    {
        private readonly object _syncRoot;
        private readonly MeasurementCalibrationService _calibrationService;
        private readonly VladInferenceResultParser _resultParser;
        private readonly VladSimilaritySearchResultParser _similaritySearchResultParser;
        private readonly VladMeasurementMapper _measurementMapper;
        // 전체 이미지와 Crop 이미지용 VLAD ID는 같은 수명주기로 관리합니다.
        private IntPtr _fullImageVladId;
        private IntPtr _croppedImageVladId;
        private readonly VladCamModeRuntime _camModeRuntime;
        private readonly VladRuntimeLifecycleService _runtimeLifecycleService;
        private readonly TrainingProcessService _trainingProcessService;
        private readonly VladVisionSettings _settings;
        private long _inspectionRequestSequence;
        private long _similaritySearchRequestSequence;

        public VladVisionInferenceEngine(
            string applicationRootPath,
            VladCamModeRuntime camModeRuntime,
            VladRuntimeLifecycleService runtimeLifecycleService)
        {
            _syncRoot = new object();
            _calibrationService = new MeasurementCalibrationService(applicationRootPath);
            _resultParser = new VladInferenceResultParser();
            _similaritySearchResultParser = new VladSimilaritySearchResultParser();
            _measurementMapper = new VladMeasurementMapper(_calibrationService);

            _camModeRuntime = camModeRuntime ?? throw new ArgumentNullException(nameof(camModeRuntime));
            _runtimeLifecycleService = runtimeLifecycleService ?? throw new ArgumentNullException("runtimeLifecycleService");
            _settings = _camModeRuntime.Settings;
            _trainingProcessService = new TrainingProcessService();
            _trainingProcessService.OutputReceived += OnTrainingOutputReceived;
            _trainingProcessService.ErrorReceived += OnTrainingErrorReceived;
            _trainingProcessService.Exited += OnTrainingProcessExited;
        }

        public event EventHandler<TrainingProcessDataEventArgs> TrainingOutputReceived;

        public event EventHandler<TrainingProcessDataEventArgs> TrainingErrorReceived;

        public event EventHandler<TrainingProcessExitedEventArgs> TrainingExited;

        public VisionInspectionOutput Inspect(VisionInspectionInput input)
        {
            lock (_runtimeLifecycleService.OperationSyncRoot)
            {
                if (_trainingProcessService.IsRunning)
                {
                    return CreateFailure("이미지 학습이 진행 중이므로 검사를 시작할 수 없습니다.");
                }

                return InspectCore(input);
            }
        }

        private VisionInspectionOutput InspectCore(VisionInspectionInput input)
        {
            // Eng->>Eng: EnsureRegistered() (최초 1회 VLAD_Ops_Ai_Env_Start 호출)
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
                // RTSP callback은 최신 프레임 캐시만 담당하고, 검사용 추론은 저장된 캡처 파일을 대상으로만 수행합니다.
                return InspectCapturedImages(input, requestSequence);
            }
            catch (Exception ex)
            {
                return CreateFailure("VLAD 추론 준비 또는 실행 실패: " + ex.Message);
            }
        }

        public IntPtr InspectMat(IntPtr rawMatPointer, int drawMode)
        {
            return InspectMat(rawMatPointer, drawMode, null, null);
        }

        public IntPtr InspectMat(IntPtr rawMatPointer, int drawMode, VisionInspectionInput input, CapturedImage capturedImage)
        {
            TraceInferenceReadinessDiagnostics();
            EnsureRegistered();
            if (rawMatPointer == IntPtr.Zero)
            {
                throw new ArgumentException("OpenCV Mat 포인터가 비어 있습니다.", "rawMatPointer");
            }

            string inspectionContextJson = BuildInspectionContextJsonV11(input, capturedImage);
            lock (VLAD_Ops_Ai.NativeInferenceSyncRoot)
            {
                return VLAD_Ops_Ai.VLAD_HD_Inference_Mat(
                    _fullImageVladId,
                    _croppedImageVladId,
                    rawMatPointer,
                    drawMode,
                    inspectionContextJson);
            }
        }

        /// <summary>
        /// 단일품목 등록 화면의 기준이미지로 유사도 검색을 실행합니다.
        /// 각 방향 이미지는 Mat 1장씩 VLAD_Search_Mat으로 전달하고, 반환 결과는 VLAD_Search_ResultData의 UTF-8 JSON으로 받습니다.
        /// </summary>
        public ReferenceImageSimilarityResult SearchReferenceImages(ReferenceImageSimilarityRequest request)
        {
            lock (_runtimeLifecycleService.OperationSyncRoot)
            {
                if (_trainingProcessService.IsRunning)
                {
                    return CreateSimilarityFailure("이미지 학습이 진행 중이므로 유사도 검색을 시작할 수 없습니다.");
                }

                return SearchReferenceImagesWithContextCore(request);
            }
        }

        /// <summary>
        /// 저장된 기준이미지를 학습 DB와 비교하고 후보 목록 JSON을 받습니다.
        /// 카메라 촬영, RTSP 재연결, 검사 이력 저장은 수행하지 않습니다.
        /// </summary>
        private ReferenceImageSimilarityResult SearchReferenceImagesWithContextCore(ReferenceImageSimilarityRequest request)
        {
            if (request == null)
            {
                return CreateSimilarityFailure("유사도 검색에 필요한 부품 기준정보가 없습니다.");
            }

            IList<CapturedImage> sourceImages = GetValidCapturedImages(request.SourceImages);
            if (sourceImages.Count == 0)
            {
                return CreateSimilarityFailure("유사도 검색에 사용할 등록 기준이미지가 없습니다.");
            }

            decimal scoreThreshold = request.ScoreThreshold;
            if (scoreThreshold < 0m)
            {
                scoreThreshold = 0m;
            }
            else if (scoreThreshold > 100m)
            {
                scoreThreshold = 100m;
            }

            try
            {
                EnsureRegistered();
                long requestSequence = Interlocked.Increment(ref _similaritySearchRequestSequence);
                ReferenceImageSimilarityResult output = new ReferenceImageSimilarityResult();

                foreach (CapturedImage sourceImage in sourceImages)
                {
                    using (OpenCvSharpMatImage matImage = OpenCvSharpMatImage.LoadFromFile(sourceImage.FilePath))
                    {
                        string searchContextJson = BuildSimilaritySearchContextJsonV11(sourceImage, scoreThreshold);

                        IntPtr searchData = VLAD_Ops_Ai.VLAD_Search_Mat(
                            _fullImageVladId, _croppedImageVladId, matImage.CvPtr, 0, searchContextJson);

                        if (searchData == IntPtr.Zero)
                        {
                            return CreateSimilarityFailure(
                                "VLAD_Search_Mat이 검색 결과 포인터를 반환하지 않았습니다. AI DLL, 모델, 입력 이미지를 확인하십시오.");
                        }

                        string resultJson = VLAD_Ops_Ai.VLAD_Search_ResultData(
                            _fullImageVladId, _croppedImageVladId, searchData);

                        IList<ReferenceImageSimilarityCandidate> candidates;
                        string parseErrorMessage;
                        if (!_similaritySearchResultParser.TryParse(
                            resultJson,
                            sourceImage.ViewType.ToString(),
                            out candidates,
                            out parseErrorMessage))
                        {
                            return CreateSimilarityFailure(parseErrorMessage);
                        }

                        foreach (ReferenceImageSimilarityCandidate candidate in candidates)
                        {
                            // 기준 Score 적용과 순위 결정은 AI 결과 계약의 책임입니다.
                            candidate.ExistsInLearningDatabase = true;
                            output.Candidates.Add(candidate);
                        }

                        Debug.WriteLine(
                            "VLAD_Search_ResultData 완료. Sequence=" +
                            requestSequence.ToString(CultureInfo.InvariantCulture) +
                            ", View=" + sourceImage.ViewType +
                            ", CandidateCount=" + candidates.Count.ToString(CultureInfo.InvariantCulture));
                    }
                }

                output.IsSuccess = true;
                output.Message = "학습 DB 유사도 확인 완료. 처리 이미지 " +
                                 sourceImages.Count.ToString(CultureInfo.InvariantCulture) +
                                 "장, 후보 " +
                                 output.Candidates.Count.ToString(CultureInfo.InvariantCulture) +
                                 "건";
                return output;
            }
            catch (EntryPointNotFoundException)
            {
                return CreateSimilarityFailure(
                    "현재 VLAD_SDK.dll에 VLAD_Search_Mat 또는 VLAD_Search_ResultData export가 없습니다. AI 담당자가 후보 목록 JSON 검색 export를 포함한 DLL을 배포해야 합니다.");
            }
            catch (Exception ex)
            {
                return CreateSimilarityFailure("VLAD 유사도 검색 실행 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// 신규 VLAD Search 계약에 맞춰 숫자 View, 기준 Score, 최대 후보 수를 전달합니다.
        /// 이미지 본문은 rawData의 cv::Mat 포인터로 이미 전달되므로 JSON에 경로를 중복해서 넣지 않습니다.
        /// </summary>
        private string BuildSimilaritySearchContextJsonV11(CapturedImage sourceImage, decimal scoreThreshold)
        {
            StringBuilder builder = new StringBuilder();
            bool hasProperty = false;
            builder.Append("{");
            AppendJsonNumberProperty(builder, "viewName", GetViewCode(sourceImage), ref hasProperty);
            AppendJsonDecimalProperty(builder, "scoreThreshold", scoreThreshold, ref hasProperty);
            AppendJsonNumberProperty(builder, "topK", 3, ref hasProperty);
            AppendJsonBooleanProperty(builder, "hasAlternatives", false, ref hasProperty);
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"candidates\":[]");
            builder.Append("}");
            return builder.ToString();
        }

        public string StartImageTraining()
        {
            lock (_runtimeLifecycleService.OperationSyncRoot)
            {
                EnsureRegistered();

                // VladId는 동일 프로세스 안에서만 유효합니다. TrainingProcessService에는 두 ID의 준비 상태만 전달합니다.
                string message = _trainingProcessService.Start(_fullImageVladId, _croppedImageVladId);
                Debug.WriteLine(message);
                return message;
            }
        }

        public void Dispose()
        {
            _trainingProcessService.OutputReceived -= OnTrainingOutputReceived;
            _trainingProcessService.ErrorReceived -= OnTrainingErrorReceived;
            _trainingProcessService.Exited -= OnTrainingProcessExited;
            _trainingProcessService.Dispose();
        }

        /// <summary>
        /// VLAD HD 고정 JSON 계약에 맞춘 최소 검사 요청을 생성합니다.
        /// 모든 View가 같은 구조를 사용하고 Thickness만 최대 5개 측정부를 채웁니다.
        /// </summary>
        private string BuildInspectionContextJsonV11(VisionInspectionInput input, CapturedImage capturedImage)
        {
            if (input == null)
            {
                return "{}";
            }

            Part part = input.Part;
            string partNo = part == null ? string.Empty : part.PartNo;
            ValidateUtf8FieldLength(partNo, 63, "partNo");

            StringBuilder builder = new StringBuilder();
            bool hasProperty = false;
            builder.Append("{");
            AppendJsonStringProperty(builder, "partNo", partNo, ref hasProperty);
            AppendJsonNumberProperty(builder, "viewName", GetViewCode(capturedImage), ref hasProperty);
            AppendJsonDecimalProperty(builder, "scoreThreshold", input.InspectionPassScoreThreshold, ref hasProperty);

            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"dimensions\":{");
            bool hasDimensionProperty = false;
            AppendJsonDecimalProperty(builder, "width", 0m, ref hasDimensionProperty);
            AppendJsonDecimalProperty(builder, "depth", 0m, ref hasDimensionProperty);
            AppendJsonDecimalProperty(builder, "height", 0m, ref hasDimensionProperty);
            builder.Append("}");

            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"measurementPoints\":[");
            bool hasMeasurement = false;
            int measurementCount = 0;
            if (capturedImage != null &&
                capturedImage.ViewType == ImageViewType.Thickness &&
                input.MeasurementPoints != null)
            {
                foreach (VisionMeasurementPointInput point in input.MeasurementPoints)
                {
                    if (point == null || point.ViewType != ImageViewType.Thickness || measurementCount >= 5)
                    {
                        continue;
                    }

                    measurementCount++;
                    AppendJsonComma(builder, ref hasMeasurement);
                    builder.Append("{");
                    bool hasMeasurementProperty = false;
                    AppendJsonNumberProperty(builder, "indexNo", measurementCount, ref hasMeasurementProperty);
                    AppendJsonDecimalProperty(builder, "nominalValue", point.NominalValue, ref hasMeasurementProperty);
                    AppendJsonDecimalProperty(builder, "toleranceMin", point.ToleranceMin, ref hasMeasurementProperty);
                    AppendJsonDecimalProperty(builder, "toleranceMax", point.ToleranceMax, ref hasMeasurementProperty);
                    AppendJsonDoubleProperty(builder, "x1", point.X1.GetValueOrDefault(), ref hasMeasurementProperty);
                    AppendJsonDoubleProperty(builder, "y1", point.Y1.GetValueOrDefault(), ref hasMeasurementProperty);
                    AppendJsonDoubleProperty(builder, "x2", point.X2.GetValueOrDefault(), ref hasMeasurementProperty);
                    AppendJsonDoubleProperty(builder, "y2", point.Y2.GetValueOrDefault(), ref hasMeasurementProperty);
                    builder.Append("}");
                }
            }

            builder.Append("]}");
            return builder.ToString();
        }

        /// <summary>
        /// DLL 계약의 카메라 위치 코드는 1부터 6까지 고정입니다.
        /// 미분류 이미지는 AI가 위치를 오해하지 않도록 호출 전에 차단합니다.
        /// </summary>
        private int GetViewCode(CapturedImage capturedImage)
        {
            if (capturedImage == null)
            {
                throw new ArgumentNullException("capturedImage");
            }

            switch (capturedImage.ViewType)
            {
                case ImageViewType.Top:
                    return 1;
                case ImageViewType.Front:
                    return 2;
                case ImageViewType.Back:
                    return 3;
                case ImageViewType.Left:
                    return 4;
                case ImageViewType.Right:
                    return 5;
                case ImageViewType.Thickness:
                    return 6;
                default:
                    throw new InvalidOperationException("VLAD 요청에 사용할 수 없는 카메라 위치입니다. ViewType=" + capturedImage.ViewType);
            }
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

        private void AppendJsonDoubleProperty(StringBuilder builder, string propertyName, double value, ref bool hasProperty)
        {
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"");
            builder.Append(propertyName);
            builder.Append("\":");
            builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void AppendJsonBooleanProperty(StringBuilder builder, string propertyName, bool value, ref bool hasProperty)
        {
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"");
            builder.Append(propertyName);
            builder.Append("\":");
            builder.Append(value ? "true" : "false");
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

        /// <summary>
        /// AI DLL과 합의한 고정 char 배열 크기를 UTF-8 byte 기준으로 검증합니다.
        /// </summary>
        private void ValidateUtf8FieldLength(string value, int maximumBytes, string fieldName)
        {
            int byteCount = Encoding.UTF8.GetByteCount(value ?? string.Empty);
            if (byteCount > maximumBytes)
            {
                throw new InvalidOperationException(
                    "VLAD JSON " + fieldName + " 값이 UTF-8 " +
                    maximumBytes.ToString(CultureInfo.InvariantCulture) +
                    " byte 제한을 초과했습니다. Actual=" +
                    byteCount.ToString(CultureInfo.InvariantCulture));
            }
        }

        private VisionInspectionOutput InspectCapturedImages(VisionInspectionInput input, long requestSequence)
        {
            IList<CapturedImage> capturedImages = GetValidCapturedImages(input);
            if (capturedImages.Count == 0)
            {
                return CreateFailure("VLAD 추론에 사용할 촬영 이미지가 없습니다.");
            }

            List<VladDetection> allDetections = new List<VladDetection>();
            List<VladInferenceResult> inferenceResults = new List<VladInferenceResult>();
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
                        Debug.WriteLine(
                            "VLAD 입력 이미지. ViewType=" +
                            capturedImage.ViewType +
                            ", Size=" +
                            matImage.Width.ToString(CultureInfo.InvariantCulture) +
                            "x" +
                            matImage.Height.ToString(CultureInfo.InvariantCulture) +
                            ", Type=" +
                            matImage.TypeText +
                            ", Path=" +
                            capturedImage.FilePath);

                        // Sample_VLAD_SDK와 동일하게 drawMode=1로 VLAD_Inference_Mat을 호출합니다.
                        IntPtr detectData = InspectMat(matImage.CvPtr, 1, input, capturedImage);
                        if (detectData == IntPtr.Zero)
                        {
                            return CreateFailure("VLAD_Inference_Mat이 detectData를 반환하지 않았습니다. GPU_ID, 모델, 입력 이미지 구성을 확인하십시오.");
                        }

                        // 결과값 받기
                        //  Native-->>Eng: detectData 포인터 반환
                        result = _resultParser.Parse(
                            _fullImageVladId,
                            _croppedImageVladId,
                            detectData,
                            matImage.CvPtr,
                            capturedImage.ViewType,
                            capturedImage.FilePath);

                        // 새 HD DLL을 사용한 경우에는 결과 JSON API가 반드시 함께 제공되어야 합니다.
                        // JSON 파싱 실패를 빈 검출 결과로 취급하면 정상 PASS로 오판할 수 있으므로 즉시 검사 실패로 반환합니다.
                        if (result == null || result.IsSuccess == false)
                        {
                            string resultMessage = result == null ? "VLAD 결과 객체가 생성되지 않았습니다." : result.Message;
                            return CreateFailure("VLAD 검사 결과를 해석하지 못했습니다. " + resultMessage);
                        }
                    }

                    AppendResultText(detectTextBuilder, capturedImage, result);
                    inferenceResults.Add(result);
                    CopyDetections(allDetections, result.Detections);
                    highestConfidence = GetHighestConfidence(highestConfidence, result.Detections);
                    if (result.Score > highestConfidence)
                    {
                        highestConfidence = result.Score;
                    }
                    processedCount++;
                }
            }

            VladStandardAiResult standardResult;
            bool hasStandardResult = _measurementMapper.TryParseStandardAiResult(detectTextBuilder.ToString(), out standardResult);
            bool hasAuthoritativeJudgment = HasAuthoritativeJudgments(inferenceResults, processedCount);

            VisionInspectionOutput output = new VisionInspectionOutput();
            output.IsSuccess = true;
            output.HasAuthoritativeJudgment = hasAuthoritativeJudgment;
            output.IsMatched = hasAuthoritativeJudgment
                ? AreAllViewsPassed(inferenceResults)
                : hasStandardResult ? standardResult.IsMatched : allDetections.Count == 0;
            output.PredictedClass = BuildPredictedClass(input.Part, allDetections);
            output.Confidence = hasAuthoritativeJudgment
                ? highestConfidence
                : hasStandardResult ? standardResult.Confidence : highestConfidence > 0m ? highestConfidence : 1m;
            output.HasScore = hasAuthoritativeJudgment || hasStandardResult;
            output.Message = BuildMessage(processedCount, allDetections.Count, detectTextBuilder.ToString());
            output.ModelVersion = "VLAD";
            ApplyDimensions(inferenceResults, output);

            // VLAD 표준 결과 문자열의 측정값을 IndexNo 순서로 DB 측정부에 매핑합니다. 측정값 단위는 mm 고정입니다.
            IList<VisionMeasurementValue> measurements = _measurementMapper.BuildMeasurements(input, allDetections, detectTextBuilder.ToString());
            foreach (VisionMeasurementValue measurement in measurements)
            {
                output.Measurements.Add(measurement);
            }
            ApplyAiMeasurementJudgments(input, inferenceResults, output.Measurements);

            Debug.WriteLine(
                "InspectCapturedImages 요청 완료. Sequence=" +
                requestSequence.ToString(CultureInfo.InvariantCulture) +
                ", ProcessedImages=" +
                processedCount.ToString(CultureInfo.InvariantCulture));
            return output;
        }

        /// <summary>
        /// DLL이 반환한 W/D/H를 검사 결과로 전달합니다.
        /// Thickness 결과를 우선 사용하고, 없으면 치수가 포함된 첫 번째 View 결과를 사용합니다.
        /// </summary>
        private void ApplyDimensions(IList<VladInferenceResult> results, VisionInspectionOutput output)
        {
            if (results == null || output == null)
            {
                return;
            }

            VladInferenceDimensions dimensions = null;
            foreach (VladInferenceResult result in results)
            {
                if (result == null || result.Dimensions == null)
                {
                    continue;
                }

                dimensions = result.Dimensions;
                if (string.Equals(result.ViewName, "Thickness", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            if (dimensions == null)
            {
                return;
            }

            output.DimensionWidth = dimensions.Width;
            output.DimensionDepth = dimensions.Depth;
            output.DimensionHeight = dimensions.Height;
            output.DimensionUnit = dimensions.Unit;
        }

        private bool HasAuthoritativeJudgments(IList<VladInferenceResult> results, int processedCount)
        {
            if (results == null || results.Count == 0 || results.Count != processedCount)
            {
                return false;
            }

            foreach (VladInferenceResult result in results)
            {
                if (result == null || string.IsNullOrWhiteSpace(result.ViewJudge))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreAllViewsPassed(IList<VladInferenceResult> results)
        {
            foreach (VladInferenceResult result in results)
            {
                if (!IsPassJudge(result.ViewJudge))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPassJudge(string judge)
        {
            return string.Equals(judge, "PASS", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(judge, "OK", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(judge, "TRUE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Thickness 결과의 측정부별 judge를 DB MeasurementRegionId에 연결합니다.
        /// DLL이 ID를 생략한 경우에는 같은 indexNo의 입력 측정부 ID를 사용합니다.
        /// </summary>
        private void ApplyAiMeasurementJudgments(
            VisionInspectionInput input,
            IList<VladInferenceResult> results,
            IList<VisionMeasurementValue> targetMeasurements)
        {
            if (results == null || targetMeasurements == null)
            {
                return;
            }

            foreach (VladInferenceResult result in results)
            {
                if (result == null || result.Measurements == null)
                {
                    continue;
                }

                foreach (VladInferenceMeasurement source in result.Measurements)
                {
                    int measurementRegionId = source.MeasurementRegionId;
                    if (measurementRegionId <= 0)
                    {
                        measurementRegionId = FindMeasurementRegionIdByIndex(input, source.IndexNo);
                    }

                    foreach (VisionMeasurementValue target in targetMeasurements)
                    {
                        if (target.MeasurementRegionId != measurementRegionId)
                        {
                            continue;
                        }

                        target.HasAiJudge = !string.IsNullOrWhiteSpace(source.Judge);
                        target.IsAiPass = IsPassJudge(source.Judge);
                        target.AiJudge = source.Judge;
                        break;
                    }
                }
            }
        }

        private int FindMeasurementRegionIdByIndex(VisionInspectionInput input, int indexNo)
        {
            if (input == null || input.MeasurementPoints == null)
            {
                return 0;
            }

            foreach (VisionMeasurementPointInput point in input.MeasurementPoints)
            {
                if (point != null && point.IndexNo == indexNo)
                {
                    return point.MeasurementRegionId;
                }
            }

            return 0;
        }

        private IList<CapturedImage> GetValidCapturedImages(VisionInspectionInput input)
        {
            if (input == null)
            {
                return new List<CapturedImage>();
            }

            return GetValidCapturedImages(input.CapturedImages);
        }

        /// <summary>
        /// 실제 파일이 존재하는 이미지만 VLAD DLL 입력으로 사용합니다.
        /// 검사와 유사도 검색이 같은 검증 규칙을 사용하도록 공통화합니다.
        /// </summary>
        private IList<CapturedImage> GetValidCapturedImages(IList<CapturedImage> sourceImages)
        {
            List<CapturedImage> images = new List<CapturedImage>();
            if (sourceImages == null)
            {
                return images;
            }

            foreach (CapturedImage image in sourceImages)
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

        private ReferenceImageSimilarityResult CreateSimilarityFailure(string message)
        {
            ReferenceImageSimilarityResult result = new ReferenceImageSimilarityResult();
            result.IsSuccess = false;
            result.Message = message;
            return result;
        }

        private void EnsureRegistered()
        {
            lock (_syncRoot)
            {
                TraceInferenceReadinessDiagnostics();

                // 학습 후 런타임이 재등록되면 엔진 내부에 보관한 전체/Crop VladId를 함께 교체합니다.
                VladCamModeState state = _camModeRuntime.EnsureLoaded();
                if (_fullImageVladId != state.FullImageVladId ||
                    _croppedImageVladId != state.CroppedImageVladId)
                {
                    _fullImageVladId = state.FullImageVladId;
                    _croppedImageVladId = state.CroppedImageVladId;
                }

                if (_fullImageVladId == IntPtr.Zero || _croppedImageVladId == IntPtr.Zero)
                {
                    throw new InvalidOperationException("VLAD_Ops_Ai_Env_Start가 전체 이미지 또는 Crop 이미지용 VLAD_ID를 반환하지 않았습니다. 모델 경로와 VLAD 런타임 DLL 구성을 확인하세요.");
                }
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

        private void OnTrainingProcessExited(object sender, TrainingProcessExitedEventArgs e)
        {
            bool reloadAttempted = false;
            bool reloadSucceeded = false;
            string reloadMessage = string.Empty;

            // DONE 출력 이후 프로세스가 정상 종료된 시점에는 모델 파일 쓰기가 모두 끝났다고 판단합니다.
            bool canReload = e.ExitCode.HasValue &&
                             e.ExitCode.Value == 0 &&
                             e.CompletionMessageReceived &&
                             !e.TerminalErrorMessageReceived;
            if (canReload)
            {
                reloadAttempted = true;
                try
                {
                    VladCamModeState state = _runtimeLifecycleService.ReloadAfterTraining();
                    lock (_syncRoot)
                    {
                        _fullImageVladId = state.FullImageVladId;
                        _croppedImageVladId = state.CroppedImageVladId;
                    }

                    reloadSucceeded = true;
                    reloadMessage = "학습 모델 적용 완료. FullImageVladId=" +
                                    state.FullImageVladId.ToInt64().ToString(CultureInfo.InvariantCulture) +
                                    ", CroppedImageVladId=" +
                                    state.CroppedImageVladId.ToInt64().ToString(CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    reloadMessage = "학습 완료 후 VLAD 재초기화 실패: " + ex.Message;
                }
            }
            else
            {
                reloadMessage = "DONE 수신과 정상 종료가 함께 확인되지 않아 VLAD 재초기화를 수행하지 않았습니다.";
            }

            EventHandler<TrainingProcessExitedEventArgs> handler = TrainingExited;
            if (handler != null)
            {
                handler(
                    this,
                    new TrainingProcessExitedEventArgs(
                        e.ExitCode,
                        e.CompletionMessageReceived,
                        e.TerminalErrorMessageReceived,
                        reloadAttempted,
                        reloadSucceeded,
                        reloadMessage));
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
