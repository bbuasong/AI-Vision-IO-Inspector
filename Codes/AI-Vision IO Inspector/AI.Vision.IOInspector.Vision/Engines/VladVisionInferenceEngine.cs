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
        private readonly string _applicationRootPath;
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
        /// <summary>깨우기에 쓰는 빈 그림 크기입니다. 실제 callback 프레임과 같게 둡니다.</summary>
        private const int WarmupImageWidth = 1920;
        private const int WarmupImageHeight = 1080;

        /// <summary>깨우기 요청임을 로그에서 알아볼 수 있게 두는 이름입니다.</summary>
        private const string WarmupPartNo = "__WARMUP__";

        private long _inspectionRequestSequence;
        private long _similaritySearchRequestSequence;

        public VladVisionInferenceEngine(
            string applicationRootPath,
            VladCamModeRuntime camModeRuntime,
            VladRuntimeLifecycleService runtimeLifecycleService)
        {
            _syncRoot = new object();
            _applicationRootPath = applicationRootPath;
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

        /// <summary>
        /// 학습이 도는 중에도 검사를 SDK까지 넘깁니다.
        ///
        /// <para>
        /// 예전에는 여기서 막았습니다. 이제는 요청 JSON의 <c>trainingRunning</c>으로 학습 여부를
        /// 알려주고, SDK가 그 구간만 OpenCV 간이 검사로 대체합니다. 여기서 막아 버리면 그 값이
        /// 1로 나가는 경우가 아예 생기지 않아 간이 검사가 동작할 자리가 없습니다.
        /// </para>
        ///
        /// <para>
        /// 학습이 끝난 뒤 VLAD를 다시 올리는 구간은 이 잠금이 막아 줍니다. 그 구간에 들어온 검사는
        /// 잠금 앞에서 기다렸다가 재초기화가 끝난 뒤에 들어가므로, 준비되지 않은 SDK를 부르지
        /// 않습니다. 사양은 VLAD_HD_Inference_Mat_학습중상태전달-2026-08-24.md 4.1절입니다.
        /// </para>
        /// </summary>
        public VisionInspectionOutput Inspect(VisionInspectionInput input)
        {
            lock (_runtimeLifecycleService.OperationSyncRoot)
            {
                return InspectCore(input);
            }
        }

        /// <summary>
        /// 프로그램을 켠 뒤 AI 를 한 번 깨워 둡니다. 첫 검사가 느린 것을 없애기 위한 것입니다.
        ///
        /// <para>
        /// 현장에서 잰 값입니다. 켠 뒤 첫 검사의 첫 장은 8 초가 걸렸고, 그다음 검사의 첫 장은
        /// 0.9 초였습니다. 나머지 다섯 장은 처음부터 0.6~0.7 초였습니다. 첫 호출 한 번에만
        /// GPU 커널 준비와 모델 적재가 얹히는 것입니다. 사람이 기다리는 자리에서 그 값을
        /// 치르지 않도록, 아무도 기다리지 않는 시작 직후에 미리 치릅니다.
        /// </para>
        ///
        /// <para>
        /// 검사에 쓰는 길을 그대로 지나갑니다. 다른 길로 깨우면 정작 검사할 때 쓰는 부분이
        /// 준비되지 않을 수 있습니다. 사진만 빈 그림을 씁니다. 결과는 쓰지 않습니다.
        /// </para>
        ///
        /// <para>
        /// 실패해도 조용히 넘어갑니다. 이 일은 빨라지자고 하는 것이지 없으면 안 되는 것이
        /// 아닙니다. 다만 무슨 일이 있었는지는 남깁니다. 첫 검사가 느리다는 말이 다시 나왔을 때
        /// 이 기록이 없으면 깨우기가 돌았는지조차 알 수 없습니다.
        /// </para>
        /// </summary>
        /// <summary>지금 이미지 학습이 도는 중인지입니다.</summary>
        public bool IsTrainingRunning
        {
            get { return _trainingProcessService.IsRunning; }
        }

        public void Warmup()
        {
            Warmup(null, null);
        }

        /// <summary>
        /// 실제 검사에 쓰는 품번과 사진으로 깨웁니다.
        ///
        /// <para>
        /// 빈 그림에 이름뿐인 품번으로 깨워 보았더니 첫 검사가 여전히 49 초 걸렸습니다.
        /// 깨우기 자체는 8.7 초를 썼는데도 그랬습니다. 두 호출의 다른 점은 품번과 사진뿐이니,
        /// SDK 가 그 조합에서 처음 하는 준비가 따로 있는 것으로 보입니다. 그래서 실제와
        /// 같은 것으로 지나갑니다.
        /// </para>
        ///
        /// <para>
        /// 등록된 부품이 없거나 사진을 찾지 못하면 예전처럼 빈 그림으로 지나갑니다.
        /// 아무것도 안 하는 것보다는 낫기 때문입니다.
        /// </para>
        /// </summary>
        public void Warmup(Part warmupPart, string imageFilePath)
        {
            Stopwatch watch = Stopwatch.StartNew();

            try
            {
                lock (_runtimeLifecycleService.OperationSyncRoot)
                {
                    if (_trainingProcessService.IsRunning)
                    {
                        AppendWarmupLog("건너뜀 - 이미지 학습이 도는 중입니다", watch);
                        return;
                    }

                    EnsureRegistered();
                    AppendWarmupLog("준비 확인", watch);

                    bool useRealImage =
                        warmupPart != null &&
                        !string.IsNullOrWhiteSpace(warmupPart.PartNo) &&
                        !string.IsNullOrWhiteSpace(imageFilePath) &&
                        File.Exists(imageFilePath);

                    OpenCvSharpMatImage image = null;
                    try
                    {
                        if (useRealImage)
                        {
                            image = OpenCvSharpMatImage.LoadFromFile(imageFilePath);
                            AppendWarmupLog("사진 읽기 (" + warmupPart.PartNo + ")", watch);
                        }
                        else
                        {
                            image = OpenCvSharpMatImage.CreateBlank(WarmupImageWidth, WarmupImageHeight);
                            AppendWarmupLog("빈 그림 준비 (등록된 사진을 찾지 못했습니다)", watch);
                        }

                        string resultJson;
                        lock (VLAD_Ops_Ai.NativeInferenceSyncRoot)
                        {
                            resultJson = VLAD_Ops_Ai.VLAD_HD_Inference_Mat(
                                _fullImageVladId,
                                image.CvPtr,
                                BuildWarmupContextJson(warmupPart));
                        }

                        AppendWarmupLog(
                            "깨우기 추론 (돌아온 글자 수 " +
                            (resultJson == null ? 0 : resultJson.Length).ToString(CultureInfo.InvariantCulture) +
                            ")",
                            watch);
                    }
                    finally
                    {
                        if (image != null)
                        {
                            image.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 깨우기가 실패해도 검사는 그대로 됩니다. 첫 검사가 그만큼 느릴 뿐입니다.
                AppendWarmupLog("실패 - " + ex.Message, watch);
            }
        }

        /// <summary>
        /// 깨우기에 쓸 최소한의 검사 요청 글입니다.
        ///
        /// <para>
        /// 등록된 품번을 쓰지 않습니다. 실제 품번을 넣으면 그 품번의 검사 기록처럼 보일 수 있고,
        /// 학습 자료에 섞일 여지도 생깁니다. 알아보기 쉬운 이름을 두어 로그에서 구분되게 합니다.
        /// </para>
        /// </summary>
        private string BuildWarmupContextJson(Part warmupPart)
        {
            Part contextPart = warmupPart;
            if (contextPart == null || string.IsNullOrWhiteSpace(contextPart.PartNo))
            {
                contextPart = new Part();
                contextPart.PartNo = WarmupPartNo;
            }

            VisionInspectionInput warmupInput = new VisionInspectionInput();
            warmupInput.Part = contextPart;

            CapturedImage warmupImage = new CapturedImage();
            warmupImage.ViewType = ImageViewType.Top;

            return BuildInspectionContextJsonV11(warmupInput, warmupImage);
        }

        private void AppendWarmupLog(string stepName, Stopwatch watch)
        {
            try
            {
                long elapsed = watch.ElapsedMilliseconds;
                watch.Restart();

                string logFilePath = AI.Vision.IOInspector.Infrastructure.ApplicationLogFileResolver
                    .GetLogFilePath(_applicationRootPath, "inspection-timing");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                              " [깨우기] " + stepName + " " +
                              elapsed.ToString(CultureInfo.InvariantCulture) + "ms" +
                              Environment.NewLine;
                File.AppendAllText(logFilePath, line);
            }
            catch
            {
                // 기록하려다 깨우기를 막으면 안 됩니다.
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
                // 검사가 오래 걸린다는 말이 나왔을 때 어느 구간인지 알 길이 없었습니다.
                Stopwatch inspectWatch = Stopwatch.StartNew();
                AppendInspectionTimingLog("검사 시작", inspectWatch, requestSequence);

                EnsureRegistered();
                AppendInspectionTimingLog("준비 확인", inspectWatch, requestSequence);

                // RTSP callback은 최신 프레임 캐시만 담당하고, 검사용 추론은 저장된 캡처 파일을 대상으로만 수행합니다.
                VisionInspectionOutput result = InspectCapturedImages(input, requestSequence);
                AppendInspectionTimingLog("추론 전체", inspectWatch, requestSequence);
                return result;
            }
            catch (Exception ex)
            {
                return CreateFailure("VLAD 추론 준비 또는 실행 실패: " + ex.Message);
            }
        }

        /// <summary>
        /// VLAD_HD_Inference_Mat을 호출해 결과 JSON 문자열을 그대로 반환합니다.
        /// 요청과 결과가 같은 버퍼를 공유하는 구조라 detectData 핸들이나 별도 결과 조회가 필요 없습니다.
        /// </summary>
        public string InspectMat(IntPtr rawMatPointer, VisionInspectionInput input, CapturedImage capturedImage)
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
                    rawMatPointer,
                    inspectionContextJson);
            }
        }

        /// <summary>
        /// 단일품목 등록 화면의 기준이미지로 유사도 검색을 실행합니다.
        /// 각 방향 이미지는 Mat 1장씩 VLAD_Search_Mat으로 전달하고, 요청과 결과가 같은 버퍼를 공유하는
        /// in-place 방식으로 UTF-8 JSON 결과를 받습니다.
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

                        string resultJson = VLAD_Ops_Ai.VLAD_Search_Mat(
                            _fullImageVladId, matImage.CvPtr, searchContextJson);

                        if (string.IsNullOrWhiteSpace(resultJson))
                        {
                            return CreateSimilarityFailure(
                                "VLAD_Search_Mat이 검색 결과 JSON을 반환하지 않았습니다. AI DLL, 모델, 입력 이미지를 확인하십시오.");
                        }

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
                            "VLAD_Search_Mat 완료. Sequence=" +
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
                    "현재 VLAD_SDK.dll에 VLAD_Search_Mat export가 없습니다. AI 담당자가 후보 목록 JSON 검색 export를 포함한 DLL을 배포해야 합니다.");
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
            AppendJsonFixedDecimalProperty(builder, "scoreThreshold", scoreThreshold, ref hasProperty);
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

            // 지금 이미지 학습이 도는 중인지 SDK에 알려 줍니다.
            //
            // 학습이 도는 동안에는 GPU와 모델 파일을 학습이 잡고 있어 정식 추론을 그대로 돌릴 수
            // 없습니다. SDK는 이 값이 1이면 그 구간만 OpenCV 간이 검사로 대체합니다.
            // 학습 프로세스를 띄우고 관리하는 쪽이 앱이라 이 상태를 가장 정확히 아는 것도 앱입니다.
            // 사양은 VLAD_HD_Inference_Mat_학습중상태전달-2026-08-24.md 3절입니다.
            int trainingRunning = 0;
            if (_trainingProcessService.IsRunning)
            {
                trainingRunning = 1;
            }

            AppendJsonNumberProperty(builder, "trainingRunning", trainingRunning, ref hasProperty);

            // viewJudge/score/dimensions/measurements는 DLL이 같은 버퍼에 덮어쓰는 "결과 자리"입니다.
            // 요청 단계에서 키가 없으면 DLL은 값을 덮어쓰는 대신 없는 키를 새로 삽입해야 하고,
            // 그 결과 viewJudge가 채워지지 않아 결과 파싱이 실패합니다.
            // 사양 1.5절대로 호출 전에 0 / 0.00 / 빈 배열을 미리 채워 보냅니다.
            AppendJsonNumberProperty(builder, "viewJudge", 0, ref hasProperty);
            AppendJsonFixedDecimalProperty(builder, "score", 0m, ref hasProperty);
            AppendJsonFixedDecimalProperty(builder, "scoreThreshold", input.InspectionPassScoreThreshold, ref hasProperty);

            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"dimensions\":{");
            bool hasDimensionProperty = false;
            AppendJsonFixedDecimalProperty(builder, "width", 0m, ref hasDimensionProperty);
            AppendJsonFixedDecimalProperty(builder, "depth", 0m, ref hasDimensionProperty);
            AppendJsonFixedDecimalProperty(builder, "height", 0m, ref hasDimensionProperty);
            builder.Append("}");

            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"measurementPoints\":[");
            bool hasMeasurement = false;
            int measurementCount = 0;
            // 이미지 한 장에는 그 방향의 측정부만 담습니다.
            //
            // 측정부는 카메라마다 따로 관리하고 번호도 각각 1부터 셉니다.
            // 부품 전체에서 번호를 매기면 Top이 1·3번, Thickness가 2번처럼 흩어져
            // "1부터 순차로 이어진다"는 AI 계약을 어기게 됩니다.
            // 여기서 다시 세어 붙이므로 화면에서 중간을 지워도 순차가 유지됩니다.
            if (capturedImage != null &&
                MeasurementPointPolicy.IsSupportedViewType(capturedImage.ViewType) &&
                input.MeasurementPoints != null)
            {
                foreach (VisionMeasurementPointInput point in input.MeasurementPoints)
                {
                    if (point == null ||
                        point.ViewType != capturedImage.ViewType ||
                        measurementCount >= MeasurementPointPolicy.MaxCount)
                    {
                        continue;
                    }

                    measurementCount++;
                    AppendJsonComma(builder, ref hasMeasurement);
                    builder.Append("{");
                    bool hasMeasurementProperty = false;
                    AppendJsonNumberProperty(builder, "indexNo", measurementCount, ref hasMeasurementProperty);

                    // 무엇을 재는 항목인지 알려 줍니다.
                    //
                    // 예전에는 좌표만 보냈습니다. 그러면 SDK 가 길이인지 높이인지 알 수 없어
                    // 항상 width 를 돌려줬습니다. 내경·외경을 넣으면서 같은 문제가 커지므로
                    // 항목 코드를 함께 보냅니다. 값은 MeasurementItemType enum 그대로입니다.
                    // 사양: VLAD_HD_Inference_Mat_요청JSON확장-2026-09-03.md 2.2절
                    AppendJsonNumberProperty(
                        builder,
                        "itemType",
                        MeasurementItemTypePolicy.GetItemTypeCode(point.ItemType),
                        ref hasMeasurementProperty);

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

            // measurements도 DLL이 채우는 결과 자리이므로 빈 배열로 미리 보냅니다.
            builder.Append("],\"measurements\":[]}");
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

        /// <summary>
        /// 사양 문서가 소수점 둘째 자리로 표기한 항목(score, scoreThreshold, dimensions)을
        /// 문서와 같은 "0.00" 형태로 기록합니다. 벤더 C++ 파서가 문서 예시와 다른 표기를
        /// 다르게 처리할 여지를 남기지 않기 위한 것입니다.
        /// </summary>
        private void AppendJsonFixedDecimalProperty(StringBuilder builder, string propertyName, decimal value, ref bool hasProperty)
        {
            AppendJsonComma(builder, ref hasProperty);
            builder.Append("\"");
            builder.Append(propertyName);
            builder.Append("\":");
            builder.Append(value.ToString("0.00", CultureInfo.InvariantCulture));
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
                Stopwatch imageWatch = Stopwatch.StartNew();
                using (OpenCvSharpMatImage matImage = OpenCvSharpMatImage.LoadFromFile(capturedImage.FilePath))
                {
                    AppendInspectionTimingLog(
                        capturedImage.ViewType.ToString() + " 사진 읽기", imageWatch, requestSequence);

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

                        string resultJson = InspectMat(matImage.CvPtr, input, capturedImage);
                        if (string.IsNullOrWhiteSpace(resultJson))
                        {
                            return CreateFailure("VLAD_HD_Inference_Mat이 결과 JSON을 반환하지 않았습니다. GPU_ID, 모델, 입력 이미지 구성을 확인하십시오.");
                        }

                        // 결과값 받기 — 요청과 결과가 같은 버퍼를 공유하므로 별도 조회 없이 바로 파싱합니다.
                        result = _resultParser.Parse(resultJson, capturedImage.ViewType, capturedImage.FilePath);

                        // 새 HD DLL을 사용한 경우에는 결과 JSON API가 반드시 함께 제공되어야 합니다.
                        // JSON 파싱 실패를 빈 검출 결과로 취급하면 정상 PASS로 오판할 수 있으므로 즉시 검사 실패로 반환합니다.
                        if (result == null || result.IsSuccess == false)
                        {
                            string resultMessage = result == null ? "VLAD 결과 객체가 생성되지 않았습니다." : result.Message;
                            return CreateFailure("VLAD 검사 결과를 해석하지 못했습니다. " + resultMessage);
                        }
                    }

                    AppendInspectionTimingLog(
                        capturedImage.ViewType.ToString() + " 추론", imageWatch, requestSequence);

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

            // 방향별 결과를 합치기 전 상태로 함께 올립니다.
            // IsMatched(6방향 AND)와 Confidence(최대값)만 올리면 결과 이미지 6장에
            // 같은 값이 적히기 때문입니다.
            AppendViewResults(output, capturedImages, inferenceResults);
            ApplyDimensions(inferenceResults, output);

            // VLAD 표준 결과 문자열의 측정값을 IndexNo 순서로 DB 측정부에 매핑합니다. 측정값 단위는 mm 고정입니다.
            IList<VisionMeasurementValue> measurements = _measurementMapper.BuildMeasurements(input, allDetections, detectTextBuilder.ToString());
            foreach (VisionMeasurementValue measurement in measurements)
            {
                output.Measurements.Add(measurement);
            }
            ApplyAiMeasurementJudgments(input, inferenceResults, output.Measurements);

            // AI 가 여섯 장에서 돌려준 측정값을 모두 셉니다.
            //
            // 측정부는 카메라마다 따로 있어 한 장에서 오는 개수가 등록 측정부 전체와 다릅니다.
            // 한 장의 개수를 전체와 견주면 늘 어긋나 보여, 없는 문제를 있다고 알리게 됩니다.
            output.AiReportedMeasurementCount = CountReportedMeasurements(inferenceResults);

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

        /// <summary>
        /// 촬영 이미지와 검사 결과를 순서대로 짝지어 방향별 결과를 만듭니다.
        /// 두 목록은 같은 루프에서 함께 채워지므로 인덱스가 일치합니다.
        /// </summary>
        private void AppendViewResults(
            VisionInspectionOutput output,
            IList<CapturedImage> capturedImages,
            IList<VladInferenceResult> inferenceResults)
        {
            if (output == null || capturedImages == null || inferenceResults == null)
            {
                return;
            }

            int count = capturedImages.Count < inferenceResults.Count
                ? capturedImages.Count
                : inferenceResults.Count;

            for (int index = 0; index < count; index++)
            {
                CapturedImage capturedImage = capturedImages[index];
                VladInferenceResult inferenceResult = inferenceResults[index];
                if (capturedImage == null || inferenceResult == null)
                {
                    continue;
                }

                // 우리가 보낸 방향과 AI가 돌려준 viewName이 같은지 확인합니다.
                //
                // 계약상 viewName은 1~6이고 Top=1, Front=2, Back=3, Left=4, Right=5, Thickness=6입니다.
                // 어긋나면 다른 방향의 판정과 치수가 이 이미지에 적히게 되는데,
                // 값만 보고는 알아챌 수 없습니다. 결과 JSON 로그에 함께 남겨 둡니다.
                //
                // 진행은 우리가 보낸 방향을 기준으로 합니다. 어느 이미지를 넘겼는지는
                // 우리가 확실히 알고 있는 사실이기 때문입니다.
                int expectedViewCode = VladViewCodePolicy.FromViewType(capturedImage.ViewType);
                if (inferenceResult.ViewCode != expectedViewCode)
                {
                    VLAD_Ops_Ai.WriteHdJsonNote(
                        "VIEW_NAME_MISMATCH",
                        "보낸 방향과 결과 viewName이 다릅니다. 보낸 방향=" +
                        capturedImage.ViewType.ToString() +
                        "(기대 viewName=" + expectedViewCode.ToString(CultureInfo.InvariantCulture) + ")" +
                        ", 결과 viewName=" + inferenceResult.ViewCode.ToString(CultureInfo.InvariantCulture) +
                        "(" + (inferenceResult.ViewName == null ? "-" : inferenceResult.ViewName) + ")" +
                        ", 이미지=" + (capturedImage.FilePath == null ? "-" : capturedImage.FilePath));
                }

                VisionViewInspectionResult viewResult = new VisionViewInspectionResult();
                viewResult.ViewType = capturedImage.ViewType;
                viewResult.IsPass = IsPassJudge(inferenceResult.ViewJudge);
                viewResult.Score = inferenceResult.Score;
                viewResult.HasScore = !string.IsNullOrWhiteSpace(inferenceResult.ViewJudge);

                if (inferenceResult.Dimensions != null)
                {
                    viewResult.DimensionWidth = inferenceResult.Dimensions.Width;
                    viewResult.DimensionDepth = inferenceResult.Dimensions.Depth;
                    viewResult.DimensionHeight = inferenceResult.Dimensions.Height;
                    viewResult.DimensionUnit = inferenceResult.Dimensions.Unit;
                }

                output.ViewResults.Add(viewResult);
            }
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
        /// 여섯 장에서 AI 가 돌려준 측정값 개수를 모두 더합니다.
        /// </summary>
        private static int CountReportedMeasurements(IList<VladInferenceResult> results)
        {
            if (results == null)
            {
                return 0;
            }

            int total = 0;
            foreach (VladInferenceResult result in results)
            {
                if (result != null && result.Measurements != null)
                {
                    total += result.Measurements.Count;
                }
            }

            return total;
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
                        // 번호는 카메라마다 1부터 세므로 번호만으로는 찾을 수 없습니다.
                        // Top 1번과 Thickness 1번이 동시에 있기 때문입니다.
                        // 이 결과가 어느 방향의 것인지 함께 봐야 올바른 측정부에 연결됩니다.
                        measurementRegionId = FindMeasurementRegionIdByIndex(
                            input, source.IndexNo, ResolveResultViewType(result));
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

                        // AI 가 정한 기준값과 허용오차입니다. 판정하지 않는 측정부는
                        // 우리 DB 값이 비어 있으므로 화면 표시는 이 값을 씁니다.
                        if (source.SpecValue != 0)
                        {
                            target.AiNominalValue = source.SpecValue;
                            target.AiToleranceMin = source.ToleranceMin;
                            target.AiToleranceMax = source.ToleranceMax;
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 방향과 번호로 측정부를 찾습니다.
        ///
        /// <para>
        /// 번호는 카메라마다 1부터 세므로 번호만으로는 가릴 수 없습니다.
        /// Top 1번과 Thickness 1번이 함께 있으면 먼저 찾은 쪽으로 잘못 연결됩니다.
        /// </para>
        /// </summary>
        private int FindMeasurementRegionIdByIndex(
            VisionInspectionInput input,
            int indexNo,
            ImageViewType viewType)
        {
            if (input == null || input.MeasurementPoints == null)
            {
                return 0;
            }

            foreach (VisionMeasurementPointInput point in input.MeasurementPoints)
            {
                if (point != null && point.IndexNo == indexNo && point.ViewType == viewType)
                {
                    return point.MeasurementRegionId;
                }
            }

            return 0;
        }

        /// <summary>
        /// 이 결과가 어느 방향의 것인지 알아냅니다.
        /// 결과 JSON의 viewName(1~6)을 방향으로 되돌립니다.
        /// </summary>
        private ImageViewType ResolveResultViewType(VladInferenceResult result)
        {
            if (result == null)
            {
                return ImageViewType.Unclassified;
            }

            return VladViewCodePolicy.ToViewType(result.ViewCode);
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
                // 방향별 결과는 [Top] [Front] ... 처럼 한 줄씩 이어 붙입니다.
                // 여기서 공백으로 붙이면 첫 방향(Top)만 앞 문구와 같은 줄에 나와,
                // 화면에서 Top 한 줄만 다르게 보입니다. 줄을 바꿔 방향을 나란히 맞춥니다.
                message = message + Environment.NewLine + FormatDetectTextForDisplay(detectText);
            }

            return message;
        }

        /// <summary>
        /// 방향별 결과 줄의 맨 앞 판정을 사람이 읽는 말로 바꿉니다.
        ///
        /// <para>
        /// DetectText 는 예전 형식이라 판정을 true / false 로 적습니다. 이 문자열은
        /// 측정값을 읽어 내는 데도 쓰이므로 원본을 건드리면 안 됩니다. 그래서 화면에
        /// 내보낼 때만 PASS / FAIL 로 바꿉니다. 화면 다른 곳이 모두 PASS / FAIL 로
        /// 적고 있어 여기만 true 로 남으면 같은 것을 두 말로 부르는 셈이 됩니다.
        /// </para>
        /// </summary>
        private static string FormatDetectTextForDisplay(string detectText)
        {
            if (string.IsNullOrWhiteSpace(detectText))
            {
                return detectText;
            }

            string[] lines = detectText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                lines[index] = FormatDetectTextLineForDisplay(lines[index]);
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string FormatDetectTextLineForDisplay(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return line;
            }

            // "[Top] true,95.00,12.34" 에서 판정 자리만 바꿉니다.
            int bracketEnd = line.IndexOf("] ", StringComparison.Ordinal);
            if (bracketEnd < 0)
            {
                return line;
            }

            int judgeStart = bracketEnd + 2;
            int judgeEnd = line.IndexOf(',', judgeStart);
            string judge = judgeEnd < 0
                ? line.Substring(judgeStart)
                : line.Substring(judgeStart, judgeEnd - judgeStart);

            string replacement;
            if (string.Equals(judge, "true", StringComparison.OrdinalIgnoreCase))
            {
                replacement = "PASS";
            }
            else if (string.Equals(judge, "false", StringComparison.OrdinalIgnoreCase))
            {
                replacement = "FAIL";
            }
            else
            {
                return line;
            }

            return line.Substring(0, judgeStart) + replacement +
                   (judgeEnd < 0 ? string.Empty : line.Substring(judgeEnd));
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

        /// <summary>
        /// 검사 한 구간이 얼마나 걸렸는지 남깁니다. 재고 나면 시계를 다시 돌립니다.
        /// </summary>
        private void AppendInspectionTimingLog(string stepName, Stopwatch watch, long requestSequence)
        {
            try
            {
                long elapsed = watch.ElapsedMilliseconds;
                watch.Restart();

                string logFilePath = AI.Vision.IOInspector.Infrastructure.ApplicationLogFileResolver
                    .GetLogFilePath(_applicationRootPath, "inspection-timing");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                              " [#" + requestSequence.ToString(CultureInfo.InvariantCulture) + "] " +
                              stepName + " " +
                              elapsed.ToString(CultureInfo.InvariantCulture) + "ms" +
                              Environment.NewLine;
                File.AppendAllText(logFilePath, line);
            }
            catch
            {
                // 시간을 재려다 검사를 막으면 안 됩니다.
            }
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
