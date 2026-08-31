using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.App.Stores;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Models;
using AI.Vision.IOInspector.Application.Services;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services;
using AI.Vision.IOInspector.Infrastructure.Services.Camera;
using AI.Vision.IOInspector.Infrastructure.Services.Retention;
using AI.Vision.IOInspector.Vision.LegacyVlad;
using AI.Vision.IOInspector.Vision.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 메인 화면의 전체 상태를 관리합니다.
    /// DB 조회/확인, 부품 생성/변경/삭제, 검사, 이력, 통계를 화면별로 연결하되 업무 로직은 서비스로 위임합니다.
    /// </summary>
    public class MainWindowViewModel : ObservableObject, IDisposable
    {
        private const int MaxSearchSuggestionCount = 10;
        private const int LivePreviewRefreshIntervalMilliseconds = 1000;
        private const string SearchFieldPartNo = "PartNo";
        private const string SearchFieldPartName = "PartName";
        private const string SearchFieldCategoryCode = "CategoryCode";
        private const string SearchFieldCategoryDescription = "CategoryDescription";

        private readonly PartDataStore _partDataStore;
        private readonly InspectionWorkflowService _inspectionWorkflowService;
        private readonly IAiInferenceService _aiInferenceService;
        private readonly IReferenceImageSimilarityService _referenceImageSimilarityService;
        private readonly StatisticsService _statisticsService;
        private readonly IInspectionRepository _inspectionRepository;
        private readonly ICameraService _cameraService;
        private readonly IReferenceImageFileService _referenceImageFileService;
        private readonly IImageMergeService _imageMergeService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IMessageDialogService _messageDialogService;
        private readonly IMeasurementPositionDialogService _measurementPositionDialogService;
        private readonly IReferenceCoordinateImageService _referenceCoordinateImageService;
        private readonly IReferenceImagePopupService _referenceImagePopupService;
        private readonly CameraConfigurationStore _cameraConfigurationStore;
        private readonly InspectionDataRetentionSettingsStore _retentionSettingsStore;
        private readonly InspectionDataRetentionService _inspectionDataRetentionService;
        private readonly IOcrScanService _ocrScanService;
        private readonly IList<InspectionRowViewModel> _allInspectionHistory;
        private readonly IList<Part> _pendingBulkParts;
        private readonly DispatcherTimer _mainSearchDelayTimer;
        private readonly DispatcherTimer _searchDelayTimer;
        private readonly DispatcherTimer _livePreviewTimer;
        private readonly DispatcherTimer _trainingScheduleTimer;
        private readonly DispatcherTimer _retentionMonitorTimer;
        private readonly Dispatcher _uiDispatcher;

        private PartViewModel _selectedPart;
        private PartViewModel _selectedDbPart;
        private PartViewModel _selectedRegistrationPart;
        private int _selectedTabIndex;
        private int _selectedRegistrationSubTabIndex;
        private string _inputCode;
        private string _statusText;
        private string _resultText;
        private string _searchKeyword;
        private string _searchPartNo;
        private string _searchPartName;
        private string _searchCategoryCode;
        private string _searchCategoryDescription;
        private string _activeDbSearchFieldName;
        private string _registrationPartNo;
        private string _registrationPartName;
        private string _registrationCategoryCode;
        private string _registrationCategoryDescription;
        private string _registrationMemo;
        private string _registrationMessage;
        private MeasurementPointViewModel _selectedRegistrationMeasurementPoint;
        private ImageEditViewModel _selectedDbDetailImage;
        private ImageEditViewModel _selectedRegistrationImage;
        private string _selectedReferenceImageViewType;
        private string _registrationCoordinateImagePath;
        private System.Windows.Media.ImageSource _registrationCoordinateImageSource;

        // 측정부를 넣을 카메라입니다. 예전에는 Thickness 하나뿐이라 그것을 기본으로 둡니다.
        private Part _dbDetailPart;
        private IList<int> _dbDetailSetNumbers = new List<int>();
        private int _dbDetailSetIndex;
        private bool _useCallbackVideoCrop;
        private int _callbackVideoCropIntervalMilliseconds;
        private ImageViewType _selectedMeasurementViewType = ImageViewType.Thickness;
        private string _bulkRegistrationMessage;
        private int _totalPartCount;
        private int _totalInspectionCount;
        private int _okCount;
        private int _ngCount;
        private int _errorCount;
        private string _statisticsStartTimeKeyword;
        private string _statisticsEndTimeKeyword;
        private string _statisticsMessage;
        private string _historyMessage;
        private string _historyStartTimeKeyword;
        private string _historyEndTimeKeyword;
        private string _historyPartNoKeyword;
        private string _historyPartNameKeyword;
        private string _historyCategoryCodeKeyword;
        private string _historyCategoryDescriptionKeyword;
        private string _historyMemoKeyword;
        private string _historyNgResultKeyword;
        private string _cameraStatusMessage;
        private string _vladGpuStatusText;
        private string _trainingScheduleText;
        private string _trainingStatusMessage;
        private string _dailyTrainingTimeText;
        private string _trainingCurrentStatus;
        private string _trainingCurrentMessage;
        private string _trainingErrorCode;
        private string _trainingErrorMessage;
        private string _applicationVersionText;
        private string _minimumFreeSpacePercentText;
        private string _retentionDaysText;
        private string _retentionStatusMessage;
        private string _inspectionScoreSettingsMessage;
        private string _similaritySearchMessage;
        private string _ocrScannerStatusText;
        private string _ocrScannerDeviceId;
        private string _ocrStatusText;
        private string _ocrLatestImagePath;
        private string _ocrLatestPartNo;
        private string _ocrLatestRawText;
        private string _ocrLatestUsageText;
        private readonly IList<OcrScanExecutionResult> _registrationOcrTemporaryResults;
        private int _ocrResolutionDpi;
        private string _ocrColorMode;
        private CameraChannelStatusViewModel _selectedCameraChannel;
        private DateTime? _scheduledImageTrainingAt;
        private DateTime? _trainingStartedAt;
        private DateTime? _trainingEndedAt;
        private DateTime? _lastDailyTrainingDate;
        private TimeSpan? _appliedDailyTrainingTime;
        private bool _deleteRequested;
        private bool _bulkImportHasError;
        private bool _isLivePreviewAutoRefreshEnabled;
        private bool _isLivePreviewRefreshRunning;
        private bool _isInitialCameraStatusRefreshRunning;
        private bool _isInitialOcrStatusRefreshRunning;
        private bool _isInspectionRunning;

        /// <summary>
        /// 검사에 쓰는 사진을 화면에 붙박아 두었는지입니다.
        ///
        /// <para>
        /// 찍기가 끝난 다음부터 판정이 나올 때까지는 화면이 그 사진에서 움직이면 안 됩니다.
        /// 영상이 계속 흐르면 보고 있는 것과 판정에 쓰인 것이 서로 달라집니다.
        /// </para>
        /// </summary>
        private bool _isInspectionStillPinned;
        private bool _isSimilaritySearchRunning;
        private bool _isDeletingAllReferenceImages;
        private bool _isTrainingReservationEnabled;
        private bool _isDailyTrainingEnabled;
        private bool _isImageTrainingRunning;
        private int _trainingProgress;
        private bool _isFreeSpaceAutoCleanupEnabled;
        private bool _isRetentionPeriodCleanupEnabled;
        private bool _isRetentionCleanupPromptVisible;
        private bool _isOcrScanRunning;
        private bool _isLoadingOcrConfiguration;
        private bool _isOcrConfigurationLoaded;
        private bool _isDisposed;
        private double _inspectionPassScoreThreshold;
        private double _singlePartSimilarityThreshold;

        public MainWindowViewModel(
            PartDataStore partDataStore,
            InspectionWorkflowService inspectionWorkflowService,
            IAiInferenceService aiInferenceService,
            IReferenceImageSimilarityService referenceImageSimilarityService,
            StatisticsService statisticsService,
            IInspectionRepository inspectionRepository,
            ICameraService cameraService,
            IReferenceImageFileService referenceImageFileService,
            IImageMergeService imageMergeService,
            IFileDialogService fileDialogService,
            IMessageDialogService messageDialogService,
            IMeasurementPositionDialogService measurementPositionDialogService,
            IReferenceCoordinateImageService referenceCoordinateImageService,
            IReferenceImagePopupService referenceImagePopupService,
            CameraConfigurationStore cameraConfigurationStore,
            InspectionDataRetentionSettingsStore retentionSettingsStore,
            InspectionDataRetentionService inspectionDataRetentionService,
            IOcrScanService ocrScanService)
        {
            _partDataStore = partDataStore;
            _inspectionWorkflowService = inspectionWorkflowService;
            _aiInferenceService = aiInferenceService;
            _referenceImageSimilarityService = referenceImageSimilarityService;
            _statisticsService = statisticsService;
            _inspectionRepository = inspectionRepository;
            _cameraService = cameraService;
            _referenceImageFileService = referenceImageFileService;
            _imageMergeService = imageMergeService;
            _fileDialogService = fileDialogService;
            _messageDialogService = messageDialogService;
            _measurementPositionDialogService = measurementPositionDialogService;
            _referenceCoordinateImageService = referenceCoordinateImageService;
            _referenceImagePopupService = referenceImagePopupService;
            _cameraConfigurationStore = cameraConfigurationStore;
            _retentionSettingsStore = retentionSettingsStore;
            _inspectionDataRetentionService = inspectionDataRetentionService;
            _ocrScanService = ocrScanService;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _allInspectionHistory = new List<InspectionRowViewModel>();
            _pendingBulkParts = new List<Part>();
            _registrationOcrTemporaryResults = new List<OcrScanExecutionResult>();

            Parts = new ObservableCollection<PartViewModel>();
            DbParts = new ObservableCollection<PartViewModel>();
            MainSearchSuggestions = new ObservableCollection<string>();
            DbSearchSuggestions = new ObservableCollection<string>();
            ImageSlots = new ObservableCollection<ImageSlotViewModel>();
            InspectionMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailImages = new ObservableCollection<ImageEditViewModel>();
            DbDetailImagePreviews = new ObservableCollection<ReferenceImagePreviewViewModel>();
            RegistrationMeasurementPoints = new ObservableCollection<MeasurementPointViewModel>();
            MeasurementItemTypes = new ObservableCollection<string>();
            RegistrationImages = new ObservableCollection<ImageEditViewModel>();
            RegistrationImagePreviews = new ObservableCollection<ReferenceImagePreviewViewModel>();
            ReferenceImageViewTypes = new ObservableCollection<string>();
            BulkPartRows = new ObservableCollection<BulkPartCsvRowViewModel>();
            InspectionHistory = new ObservableCollection<InspectionRowViewModel>();
            StatisticsOkRows = new ObservableCollection<InspectionRowViewModel>();
            StatisticsNgRows = new ObservableCollection<InspectionRowViewModel>();
            StatisticsErrorRows = new ObservableCollection<InspectionRowViewModel>();
            EventRows = new ObservableCollection<EventRowViewModel>();
            CameraChannels = new ObservableCollection<CameraChannelStatusViewModel>();
            DiskUsages = new ObservableCollection<DiskUsageViewModel>();
            RetentionPeriodOptions = new ObservableCollection<string>();
            TrainingProcessMessages = new ObservableCollection<TrainingProcessMessageRowViewModel>();
            SimilarityCandidates = new ObservableCollection<SimilarityCandidateViewModel>();
            OcrResolutionOptions = new ObservableCollection<OcrResolutionOption>();
            OcrColorModes = new ObservableCollection<OcrColorModeOption>();
            OcrHistory = new ObservableCollection<OcrHistoryRowViewModel>();

            RunInspectionCommand = new RelayCommand(ExecuteRunInspection, CanRunInspection);
            ResetInspectionScreenCommand = new RelayCommand(ExecuteResetInspectionScreen, CanResetInspectionScreen);
            SavePartCommand = new RelayCommand(ExecuteSavePart);
            NewPartCommand = new RelayCommand(ExecuteNewPart);
            RegistrationOcrInputCommand = new RelayCommand(ExecuteRegistrationOcrInput, CanStartOcrScan);
            DeletePartCommand = new RelayCommand(ExecuteDeletePart);
            SearchCommand = new RelayCommand(ExecuteSearch);
            ApplyMainSearchSuggestionCommand = new RelayCommand(ExecuteApplyMainSearchSuggestion);
            ApplyPartNameSearchSuggestionCommand = new RelayCommand(ExecuteApplyPartNameSearchSuggestion);
            ApplyDbSearchSuggestionCommand = new RelayCommand(ExecuteApplyDbSearchSuggestion);
            AddMeasurementPointCommand = new RelayCommand(ExecuteAddMeasurementPoint);
            RemoveMeasurementPointCommand = new RelayCommand(ExecuteRemoveMeasurementPoint);
            EditMeasurementPositionCommand = new RelayCommand(ExecuteEditMeasurementPosition);
            ShowReferenceImagePopupCommand = new RelayCommand(ExecuteShowReferenceImagePopup);
            ShowDbDetailImagePopupCommand = new RelayCommand(ExecuteShowDbDetailImagePopup);
            AddReferenceImageCommand = new RelayCommand(ExecuteAddReferenceImage);
            SaveCurrentCameraImagesCommand = new RelayCommand(ExecuteSaveCurrentCameraImages);
            CheckReferenceImageSimilarityCommand = new RelayCommand(ExecuteCheckReferenceImageSimilarity);
            ClearReferenceImageSimilarityCommand = new RelayCommand(ExecuteClearReferenceImageSimilarity);
            RefreshLivePreviewCommand = new RelayCommand(ExecuteRefreshLivePreview);
            DeleteAllReferenceImagesCommand = new RelayCommand(ExecuteDeleteAllReferenceImages);
            ShowPreviousDbDetailSetCommand = new RelayCommand(ExecuteShowPreviousDbDetailSet);
            ShowNextDbDetailSetCommand = new RelayCommand(ExecuteShowNextDbDetailSet);
            ImportPartsCsvCommand = new RelayCommand(ExecuteImportPartsCsv);
            ExportAllPartsCsvCommand = new RelayCommand(ExecuteExportAllPartsCsv);
            SaveBulkPartsCommand = new RelayCommand(ExecuteSaveBulkParts);
            SaveHistoryCsvCommand = new RelayCommand(ExecuteSaveHistoryCsv);
            ClearHistorySearchCommand = new RelayCommand(ExecuteClearHistorySearch);
            RefreshStatisticsCommand = new RelayCommand(ExecuteRefreshStatistics);
            ResetStatisticsCommand = new RelayCommand(ExecuteResetStatistics);
            SetHistoryStartDateCommand = new RelayCommand(ExecuteSetHistoryStartDate);
            SetHistoryEndDateCommand = new RelayCommand(ExecuteSetHistoryEndDate);
            SetStatisticsStartDateCommand = new RelayCommand(ExecuteSetStatisticsStartDate);
            SetStatisticsEndDateCommand = new RelayCommand(ExecuteSetStatisticsEndDate);
            RefreshCameraStatusCommand = new RelayCommand(ExecuteRefreshCameraStatus);
            ReloadCameraConfigurationCommand = new RelayCommand(ExecuteReloadCameraConfiguration);
            SaveCameraConfigurationCommand = new RelayCommand(ExecuteSaveCameraConfiguration);
            TestSelectedCameraConnectionCommand = new RelayCommand(ExecuteTestSelectedCameraConnection);
            StartImageTrainingCommand = new RelayCommand(ExecuteStartImageTraining, CanStartImageTraining);
            ApplyImageTrainingScheduleCommand = new RelayCommand(ExecuteApplyImageTrainingSchedule);
            ApplyDailyImageTrainingScheduleCommand = new RelayCommand(ExecuteApplyDailyImageTrainingSchedule);
            ClearTrainingProcessMessagesCommand = new RelayCommand(ExecuteClearTrainingProcessMessages);
            SaveInspectionScoreSettingsCommand = new RelayCommand(ExecuteSaveInspectionScoreSettings);
            SaveRetentionSettingsCommand = new RelayCommand(ExecuteSaveRetentionSettings);
            RefreshOcrScannerCommand = new RelayCommand(ExecuteRefreshOcrScanner);
            StartOcrScanCommand = new RelayCommand(ExecuteStartOcrScan, CanStartOcrScan);
            ShowInspectionTabCommand = new RelayCommand(ExecuteShowInspectionTab);
            ShowRegistrationTabCommand = new RelayCommand(ExecuteShowRegistrationTab);
            ShowDbTabCommand = new RelayCommand(ExecuteShowDbTab);
            ShowHistoryTabCommand = new RelayCommand(ExecuteShowHistoryTab);
            ShowStatisticsTabCommand = new RelayCommand(ExecuteShowStatisticsTab);

            _mainSearchDelayTimer = new DispatcherTimer();
            _mainSearchDelayTimer.Interval = TimeSpan.FromMilliseconds(250);
            _mainSearchDelayTimer.Tick += OnMainSearchDelayTimerTick;

            _searchDelayTimer = new DispatcherTimer();
            _searchDelayTimer.Interval = TimeSpan.FromMilliseconds(250);
            _searchDelayTimer.Tick += OnSearchDelayTimerTick;

            _livePreviewTimer = new DispatcherTimer();
            _livePreviewTimer.Interval = TimeSpan.FromMilliseconds(LivePreviewRefreshIntervalMilliseconds);
            _livePreviewTimer.Tick += OnLivePreviewTimerTick;

            _trainingScheduleTimer = new DispatcherTimer();
            _trainingScheduleTimer.Interval = TimeSpan.FromSeconds(30);
            _trainingScheduleTimer.Tick += OnTrainingScheduleTimerTick;

            _retentionMonitorTimer = new DispatcherTimer();
            _retentionMonitorTimer.Interval = TimeSpan.FromHours(1);
            _retentionMonitorTimer.Tick += OnRetentionMonitorTimerTick;

            StatusText = "대기";
            ResultText = "검사 전";
            TrainingScheduleText = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            DailyTrainingTimeText = "02:00";
            TrainingStatusMessage = "이미지 학습 대기";
            TrainingCurrentStatus = "대기";
            TrainingCurrentMessage = "학습 시작 대기";
            TrainingErrorCode = string.Empty;
            TrainingErrorMessage = string.Empty;
            _aiInferenceService.TrainingOutputReceived += OnTrainingOutputReceived;
            _aiInferenceService.TrainingErrorReceived += OnTrainingErrorReceived;
            _aiInferenceService.TrainingExited += OnTrainingExited;
            _inspectionWorkflowService.ProgressChanged += OnInspectionProgressChanged;
            ApplicationVersionText = BuildApplicationVersionText();
            LoadVladGpuStatus();
            LoadInspectionScoreSettings();
            _activeDbSearchFieldName = SearchFieldPartName;
            InitializeReferenceImageViewTypes();
            InitializeImageSlots();
            InitializeMeasurementItemTypes();
            InitializeRetentionPeriodOptions();
            LoadRetentionSettings();
            InitializeOcrOptions();
            LoadOcrConfiguration();
            InitializeEmptyRegistrationPoints();
            LoadParts();
            RefreshHistory();
            RefreshStatistics();
            RefreshCameraStatuses(false);
            RefreshDiskUsages();
            OcrScannerStatusText = "시작 대기";
            OcrScannerDeviceId = "메인 화면 표시 후 Epson ES-C320W 연결을 확인합니다.";
            StartRetentionMonitorTimer();
        }

        public ObservableCollection<PartViewModel> Parts { get; private set; }

        public ObservableCollection<PartViewModel> DbParts { get; private set; }

        public ObservableCollection<string> MainSearchSuggestions { get; private set; }

        public ObservableCollection<string> DbSearchSuggestions { get; private set; }

        public bool HasMainSearchSuggestions
        {
            get { return MainSearchSuggestions.Count > 0; }
        }

        public bool HasDbSearchSuggestions
        {
            get { return DbSearchSuggestions.Count > 0; }
        }

        public ObservableCollection<ImageSlotViewModel> ImageSlots { get; private set; }

        public ObservableCollection<MeasurementRowViewModel> InspectionMeasurements { get; private set; }

        public ObservableCollection<MeasurementRowViewModel> DbDetailMeasurements { get; private set; }

        public ObservableCollection<ImageEditViewModel> DbDetailImages { get; private set; }

        public ObservableCollection<ReferenceImagePreviewViewModel> DbDetailImagePreviews { get; private set; }

        /// <summary>
        /// 조회 화면에서 보고 있는 벌의 이름입니다. 예) [003] 2026-08-06 16:27:10
        ///
        /// <para>
        /// 기준 이미지는 저장할 때마다 벌이 쌓입니다. 조회에서 최근 벌만 보이면
        /// 이전에 어떤 그림으로 등록했는지 확인할 길이 없어 화살표로 넘겨 볼 수 있게 합니다.
        /// </para>
        /// </summary>
        public string DbDetailSetDisplayName
        {
            get
            {
                if (_dbDetailSetNumbers.Count == 0)
                {
                    return "저장된 벌 없음";
                }

                int setNo = _dbDetailSetNumbers[_dbDetailSetIndex];
                DateTime savedAt = FindDbDetailSetSavedAt(setNo);
                return ReferenceImageFileNamePolicy.BuildSetDisplayName(setNo, savedAt) +
                       "   (" + (_dbDetailSetIndex + 1).ToString(CultureInfo.InvariantCulture) +
                       "/" + _dbDetailSetNumbers.Count.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        /// <summary>벌이 둘 이상일 때만 화살표를 보입니다.</summary>
        public bool HasMultipleDbDetailSets
        {
            get { return _dbDetailSetNumbers.Count > 1; }
        }

        public bool CanShowPreviousDbDetailSet
        {
            get { return _dbDetailSetIndex > 0; }
        }

        public bool CanShowNextDbDetailSet
        {
            get { return _dbDetailSetIndex < _dbDetailSetNumbers.Count - 1; }
        }

        public ICommand ShowPreviousDbDetailSetCommand { get; private set; }

        public ICommand ShowNextDbDetailSetCommand { get; private set; }

        public ObservableCollection<MeasurementPointViewModel> RegistrationMeasurementPoints { get; private set; }

        public ObservableCollection<string> MeasurementItemTypes { get; private set; }

        public ObservableCollection<ImageEditViewModel> RegistrationImages { get; private set; }

        public ObservableCollection<ReferenceImagePreviewViewModel> RegistrationImagePreviews { get; private set; }

        public ObservableCollection<string> ReferenceImageViewTypes { get; private set; }

        public ObservableCollection<BulkPartCsvRowViewModel> BulkPartRows { get; private set; }

        public ObservableCollection<InspectionRowViewModel> InspectionHistory { get; private set; }

        public ObservableCollection<InspectionRowViewModel> StatisticsOkRows { get; private set; }

        public ObservableCollection<InspectionRowViewModel> StatisticsNgRows { get; private set; }

        public ObservableCollection<InspectionRowViewModel> StatisticsErrorRows { get; private set; }

        public ObservableCollection<EventRowViewModel> EventRows { get; private set; }

        public ObservableCollection<CameraChannelStatusViewModel> CameraChannels { get; private set; }

        public ObservableCollection<DiskUsageViewModel> DiskUsages { get; private set; }

        public ObservableCollection<string> RetentionPeriodOptions { get; private set; }

        public ObservableCollection<TrainingProcessMessageRowViewModel> TrainingProcessMessages { get; private set; }

        public ObservableCollection<SimilarityCandidateViewModel> SimilarityCandidates { get; private set; }

        public ObservableCollection<OcrResolutionOption> OcrResolutionOptions { get; private set; }

        public ObservableCollection<OcrColorModeOption> OcrColorModes { get; private set; }

        public ObservableCollection<OcrHistoryRowViewModel> OcrHistory { get; private set; }

        public ICommand RunInspectionCommand { get; private set; }

        public ICommand ResetInspectionScreenCommand { get; private set; }

        public ICommand SavePartCommand { get; private set; }

        public ICommand NewPartCommand { get; private set; }

        public ICommand RegistrationOcrInputCommand { get; private set; }

        public ICommand DeletePartCommand { get; private set; }

        public ICommand SearchCommand { get; private set; }

        public ICommand ApplyMainSearchSuggestionCommand { get; private set; }

        public ICommand ApplyPartNameSearchSuggestionCommand { get; private set; }

        public ICommand ApplyDbSearchSuggestionCommand { get; private set; }

        public ICommand AddMeasurementPointCommand { get; private set; }

        public ICommand RemoveMeasurementPointCommand { get; private set; }

        public ICommand EditMeasurementPositionCommand { get; private set; }

        public ICommand ShowReferenceImagePopupCommand { get; private set; }

        /// <summary>
        /// 조회 화면의 기준 이미지 미리보기를 눌렀을 때 확대 창을 엽니다.
        ///
        /// <para>
        /// 검사 화면에서 쓰던 창을 그대로 씁니다. 그 창은 벌을 넘겨 볼 수 있어,
        /// 예전에 어떤 그림으로 등록했는지 조회 화면에서도 확인할 수 있습니다.
        /// </para>
        /// </summary>
        public ICommand ShowDbDetailImagePopupCommand { get; private set; }

        public ICommand AddReferenceImageCommand { get; private set; }

        public ICommand SaveCurrentCameraImagesCommand { get; private set; }

        public ICommand CheckReferenceImageSimilarityCommand { get; private set; }

        public ICommand ClearReferenceImageSimilarityCommand { get; private set; }

        public ICommand RefreshLivePreviewCommand { get; private set; }

        public ICommand DeleteAllReferenceImagesCommand { get; private set; }

        public ICommand ImportPartsCsvCommand { get; private set; }

        public ICommand ExportAllPartsCsvCommand { get; private set; }

        public ICommand SaveBulkPartsCommand { get; private set; }

        public ICommand SaveHistoryCsvCommand { get; private set; }

        public ICommand ClearHistorySearchCommand { get; private set; }

        public ICommand RefreshStatisticsCommand { get; private set; }

        public ICommand ResetStatisticsCommand { get; private set; }

        /// <summary>
        /// 조회 기간 칸을 한 번에 채우는 단추입니다.
        /// Start는 어제, End는 오늘을 넣습니다. 어제부터 오늘까지가 가장 자주 보는 구간입니다.
        /// </summary>
        public ICommand SetHistoryStartDateCommand { get; private set; }

        public ICommand SetHistoryEndDateCommand { get; private set; }

        public ICommand SetStatisticsStartDateCommand { get; private set; }

        public ICommand SetStatisticsEndDateCommand { get; private set; }

        public ICommand RefreshCameraStatusCommand { get; private set; }

        public ICommand ReloadCameraConfigurationCommand { get; private set; }

        public ICommand SaveCameraConfigurationCommand { get; private set; }

        public ICommand TestSelectedCameraConnectionCommand { get; private set; }

        public ICommand StartImageTrainingCommand { get; private set; }

        public ICommand ApplyImageTrainingScheduleCommand { get; private set; }

        public ICommand ApplyDailyImageTrainingScheduleCommand { get; private set; }

        public ICommand ClearTrainingProcessMessagesCommand { get; private set; }

        public ICommand SaveInspectionScoreSettingsCommand { get; private set; }

        public ICommand SaveRetentionSettingsCommand { get; private set; }

        public ICommand RefreshOcrScannerCommand { get; private set; }

        public ICommand StartOcrScanCommand { get; private set; }

        public ICommand ShowInspectionTabCommand { get; private set; }

        public ICommand ShowRegistrationTabCommand { get; private set; }

        public ICommand ShowDbTabCommand { get; private set; }

        public ICommand ShowHistoryTabCommand { get; private set; }

        public ICommand ShowStatisticsTabCommand { get; private set; }

        /// <summary>
        /// 부품 목록을 갈아끼우는 중인지입니다.
        ///
        /// <para>
        /// 목록을 비우거나 새 컬렉션으로 바꾸면 그리드가 TwoWay 바인딩으로 선택값에 null 이나
        /// 옛 항목을 도로 밀어 넣습니다. 그 되밀림이 setter 의 부수 효과(입력폼 다시 채우기,
        /// 임시 이미지 정리)를 엉뚱한 시점에 일으켜, 품번을 바꿔 저장했는데 입력폼이 옛 품번으로
        /// 되돌아가는 문제를 만들었습니다. 갈아끼우는 동안에는 되밀림을 무시하고 부수 효과를
        /// 멈춥니다. 끝난 뒤의 선택 복원은 코드가 명시적으로 합니다.
        /// </para>
        /// </summary>
        private bool _isSynchronizingPartSelection;

        public PartViewModel SelectedPart
        {
            get { return _selectedPart; }
            set
            {
                if (_isSynchronizingPartSelection && value == null)
                {
                    // 목록 교체가 미는 null 은 사용자의 선택 해제가 아닙니다.
                    return;
                }

                if (SetProperty(ref _selectedPart, value))
                {
                    if (!_isSynchronizingPartSelection)
                    {
                        ApplySelectedPart();
                    }
                }
            }
        }

        public PartViewModel SelectedDbPart
        {
            get { return _selectedDbPart; }
            set
            {
                if (_isSynchronizingPartSelection && value == null)
                {
                    return;
                }

                if (SetProperty(ref _selectedDbPart, value))
                {
                    if (!_isSynchronizingPartSelection)
                    {
                        ApplySelectedDbPart();
                    }
                }
            }
        }

        public PartViewModel SelectedRegistrationPart
        {
            get { return _selectedRegistrationPart; }
            set
            {
                if (_isSynchronizingPartSelection && value == null)
                {
                    // 목록 교체가 미는 null 은 사용자의 선택 해제가 아닙니다.
                    // 여기서 받아 주면 임시 이미지까지 지워져 저장 흐름이 끊깁니다.
                    return;
                }

                PartViewModel previousPart = _selectedRegistrationPart;
                if (!_isSynchronizingPartSelection &&
                    previousPart != null &&
                    (value == null || !IsSamePartNo(previousPart.PartNo, value.PartNo)))
                {
                    // 화면 전환은 저장하지 않은 등록 작업을 취소하는 동작입니다.
                    // UI가 이미 이전 임시 이미지를 버리므로 파일도 함께 정리해야 다음 품번에 남지 않습니다.
                    ClearTemporaryReferenceImagesForPart(previousPart.Part);
                }

                if (SetProperty(ref _selectedRegistrationPart, value))
                {
                    if (!_isSynchronizingPartSelection)
                    {
                        ApplySelectedRegistrationPart();
                    }
                }
            }
        }

        public int SelectedTabIndex
        {
            get { return _selectedTabIndex; }
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    OnPropertyChanged("IsInspectionTabActive");
                }
            }
        }

        /// <summary>
        /// "부품 등록" 탭 안의 서브탭(단일품목 등록=0, 다중품목 등록=1) 선택 인덱스입니다.
        /// 코드에서 "단일품목 등록" 서브탭으로 강제 전환할 때(OCR 미등록 신규 등록 등) 사용합니다.
        /// </summary>
        public int SelectedRegistrationSubTabIndex
        {
            get { return _selectedRegistrationSubTabIndex; }
            set { SetProperty(ref _selectedRegistrationSubTabIndex, value); }
        }

        /// <summary>
        /// 검사 화면(6개 카메라 CallbackVideoView)을 TabControl 밖에 항상 살려두고 이 값으로만 표시 여부를 전환합니다.
        /// 탭 전환 시 TabItem 콘텐츠가 시각 트리에서 탈부착되면서 RTSP 스트림이 매번 재연결되는 문제를 피하기 위함입니다.
        /// </summary>
        public bool IsInspectionTabActive
        {
            get { return SelectedTabIndex == 0; }
        }

        public string InputCode
        {
            get { return _inputCode; }
            set
            {
                if (SetProperty(ref _inputCode, value))
                {
                    RaiseRunCommandState();
                }
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value); }
        }

        public string ResultText
        {
            get { return _resultText; }
            set { SetProperty(ref _resultText, value); }
        }

        public bool IsLivePreviewAutoRefreshEnabled
        {
            get { return _isLivePreviewAutoRefreshEnabled; }
            set
            {
                if (SetProperty(ref _isLivePreviewAutoRefreshEnabled, value))
                {
                    ApplyLivePreviewAutoRefreshState();
                }
            }
        }

        public string SearchKeyword
        {
            get { return _searchKeyword; }
            set
            {
                if (SetProperty(ref _searchKeyword, value))
                {
                    QueueMainSearchRefresh();
                }
            }
        }

        public int OcrResolutionDpi
        {
            get { return _ocrResolutionDpi; }
            set
            {
                if (SetProperty(ref _ocrResolutionDpi, value))
                {
                    SaveOcrConfigurationAfterSelectionChanged();
                }
            }
        }

        public string OcrColorMode
        {
            get { return _ocrColorMode; }
            set
            {
                if (SetProperty(ref _ocrColorMode, value))
                {
                    SaveOcrConfigurationAfterSelectionChanged();
                }
            }
        }

        public string OcrScannerStatusText
        {
            get { return _ocrScannerStatusText; }
            set { SetProperty(ref _ocrScannerStatusText, value); }
        }

        public string OcrScannerDeviceId
        {
            get { return _ocrScannerDeviceId; }
            set { SetProperty(ref _ocrScannerDeviceId, value); }
        }

        public string OcrStatusText
        {
            get { return _ocrStatusText; }
            set { SetProperty(ref _ocrStatusText, value); }
        }

        public string OcrLatestImagePath
        {
            get { return _ocrLatestImagePath; }
            set { SetProperty(ref _ocrLatestImagePath, value); }
        }

        /// <summary>
        /// 가장 최근 OCR이 API 응답의 part_no에서 판정한 부품번호입니다.
        /// OCR 원문과 별도로 읽기 전용 TextBox에 표시해 작업자가 즉시 확인할 수 있게 합니다.
        /// </summary>
        public string OcrLatestPartNo
        {
            get { return _ocrLatestPartNo; }
            set { SetProperty(ref _ocrLatestPartNo, value); }
        }

        public string OcrLatestRawText
        {
            get { return _ocrLatestRawText; }
            set { SetProperty(ref _ocrLatestRawText, value); }
        }

        /// <summary>
        /// 가장 최근 OCR 결과가 검사 검색인지 신규 부품 등록인지 화면에 표시합니다.
        /// </summary>
        public string OcrLatestUsageText
        {
            get { return _ocrLatestUsageText; }
            set { SetProperty(ref _ocrLatestUsageText, value); }
        }

        public bool IsOcrScanRunning
        {
            get { return _isOcrScanRunning; }
            private set
            {
                if (SetProperty(ref _isOcrScanRunning, value))
                {
                    RaiseOcrCommandState();
                }
            }
        }

        public string SearchPartNo
        {
            get { return _searchPartNo; }
            set
            {
                if (SetProperty(ref _searchPartNo, value))
                {
                    _activeDbSearchFieldName = SearchFieldPartNo;
                    QueueSearchFilterRefresh();
                }
            }
        }

        public string SearchPartName
        {
            get { return _searchPartName; }
            set
            {
                if (SetProperty(ref _searchPartName, value))
                {
                    _activeDbSearchFieldName = SearchFieldPartName;
                    QueueSearchFilterRefresh();
                }
            }
        }

        public string SearchCategoryCode
        {
            get { return _searchCategoryCode; }
            set
            {
                if (SetProperty(ref _searchCategoryCode, value))
                {
                    _activeDbSearchFieldName = SearchFieldCategoryCode;
                    QueueSearchFilterRefresh();
                }
            }
        }

        public string SearchCategoryDescription
        {
            get { return _searchCategoryDescription; }
            set
            {
                if (SetProperty(ref _searchCategoryDescription, value))
                {
                    _activeDbSearchFieldName = SearchFieldCategoryDescription;
                    QueueSearchFilterRefresh();
                }
            }
        }

        public string RegistrationPartNo
        {
            get { return _registrationPartNo; }
            set { SetProperty(ref _registrationPartNo, value); }
        }

        public string RegistrationPartName
        {
            get { return _registrationPartName; }
            set { SetProperty(ref _registrationPartName, value); }
        }

        public string RegistrationCategoryCode
        {
            get { return _registrationCategoryCode; }
            set { SetProperty(ref _registrationCategoryCode, value); }
        }

        public string RegistrationCategoryDescription
        {
            get { return _registrationCategoryDescription; }
            set { SetProperty(ref _registrationCategoryDescription, value); }
        }

        public string RegistrationMemo
        {
            get { return _registrationMemo; }
            set { SetProperty(ref _registrationMemo, value); }
        }

        public string RegistrationMessage
        {
            get { return _registrationMessage; }
            set { SetProperty(ref _registrationMessage, value); }
        }

        public MeasurementPointViewModel SelectedRegistrationMeasurementPoint
        {
            get { return _selectedRegistrationMeasurementPoint; }
            set { SetProperty(ref _selectedRegistrationMeasurementPoint, value); }
        }

        public ImageEditViewModel SelectedDbDetailImage
        {
            get { return _selectedDbDetailImage; }
            set { SetProperty(ref _selectedDbDetailImage, value); }
        }

        public ImageEditViewModel SelectedRegistrationImage
        {
            get { return _selectedRegistrationImage; }
            set { SetProperty(ref _selectedRegistrationImage, value); }
        }

        public string SelectedReferenceImageViewType
        {
            get { return _selectedReferenceImageViewType; }
            set { SetProperty(ref _selectedReferenceImageViewType, value); }
        }

        /// <summary>
        /// 화면에 띄울 측정부 좌표 그림입니다. 그 카메라의 자를 자리로 잘라 둡니다.
        ///
        /// <para>
        /// 파일은 원본 크기 그대로 둡니다. 자르는 것은 보여 줄 때뿐입니다.
        /// 검사 화면이 잘라 보여 주므로 여기만 원본이면 같은 카메라인데도 다른 그림처럼 보입니다.
        /// </para>
        /// </summary>
        public System.Windows.Media.ImageSource RegistrationCoordinateImageSource
        {
            get { return _registrationCoordinateImageSource; }
            private set { SetProperty(ref _registrationCoordinateImageSource, value, "RegistrationCoordinateImageSource"); }
        }

        public string RegistrationCoordinateImagePath
        {
            get { return _registrationCoordinateImagePath; }
            private set
            {
                if (SetProperty(ref _registrationCoordinateImagePath, value))
                {
                    RegistrationCoordinateImageSource =
                        AI.Vision.IOInspector.App.Services.CroppedImageSourceFactory
                            .Build(value, SelectedMeasurementViewType);
                    OnPropertyChanged("HasRegistrationCoordinateImage");
                    RefreshRegistrationImagePreviews();
                }
            }
        }

        public bool HasRegistrationCoordinateImage
        {
            get
            {
                return !string.IsNullOrWhiteSpace(RegistrationCoordinateImagePath) &&
                       File.Exists(RegistrationCoordinateImagePath);
            }
        }

        public string BulkRegistrationMessage
        {
            get { return _bulkRegistrationMessage; }
            set { SetProperty(ref _bulkRegistrationMessage, value); }
        }

        public int TotalPartCount
        {
            get { return _totalPartCount; }
            set { SetProperty(ref _totalPartCount, value); }
        }

        public int TotalInspectionCount
        {
            get { return _totalInspectionCount; }
            set { SetProperty(ref _totalInspectionCount, value); }
        }

        public int PassCount
        {
            get { return _okCount; }
            set { SetProperty(ref _okCount, value); }
        }

        public int FailCount
        {
            get { return _ngCount; }
            set { SetProperty(ref _ngCount, value); }
        }

        public int ErrorCount
        {
            get { return _errorCount; }
            set { SetProperty(ref _errorCount, value); }
        }

        public string HistoryMessage
        {
            get { return _historyMessage; }
            set { SetProperty(ref _historyMessage, value); }
        }

        public string StatisticsStartTimeKeyword
        {
            get { return _statisticsStartTimeKeyword; }
            set { SetProperty(ref _statisticsStartTimeKeyword, value); }
        }

        public string StatisticsEndTimeKeyword
        {
            get { return _statisticsEndTimeKeyword; }
            set { SetProperty(ref _statisticsEndTimeKeyword, value); }
        }

        public string StatisticsMessage
        {
            get { return _statisticsMessage; }
            set { SetProperty(ref _statisticsMessage, value); }
        }

        public string HistoryStartTimeKeyword
        {
            get { return _historyStartTimeKeyword; }
            set
            {
                if (SetProperty(ref _historyStartTimeKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string HistoryEndTimeKeyword
        {
            get { return _historyEndTimeKeyword; }
            set
            {
                if (SetProperty(ref _historyEndTimeKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string HistoryPartNoKeyword
        {
            get { return _historyPartNoKeyword; }
            set
            {
                if (SetProperty(ref _historyPartNoKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string HistoryPartNameKeyword
        {
            get { return _historyPartNameKeyword; }
            set
            {
                if (SetProperty(ref _historyPartNameKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string HistoryCategoryCodeKeyword
        {
            get { return _historyCategoryCodeKeyword; }
            set
            {
                if (SetProperty(ref _historyCategoryCodeKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string HistoryCategoryDescriptionKeyword
        {
            get { return _historyCategoryDescriptionKeyword; }
            set
            {
                if (SetProperty(ref _historyCategoryDescriptionKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string HistoryMemoKeyword
        {
            get { return _historyMemoKeyword; }
            set
            {
                if (SetProperty(ref _historyMemoKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string HistoryNgResultKeyword
        {
            get { return _historyNgResultKeyword; }
            set
            {
                if (SetProperty(ref _historyNgResultKeyword, value))
                {
                    ApplyHistoryFilters();
                }
            }
        }

        public string CameraStatusMessage
        {
            get { return _cameraStatusMessage; }
            set { SetProperty(ref _cameraStatusMessage, value); }
        }

        /// <summary>
        /// 프로그램 시작 시 VLAD 초기화에 사용한 GPU 설정을 표시합니다.
        /// 실행 중 GPU를 바꾸면 기존 VladId와 GPU 컨텍스트가 달라질 수 있으므로 읽기 전용으로 둡니다.
        /// </summary>
        public string VladGpuStatusText
        {
            get { return _vladGpuStatusText; }
            private set { SetProperty(ref _vladGpuStatusText, value); }
        }

        /// <summary>
        /// AI가 반환한 Score의 최소 합격 기준입니다. 값은 0~100 범위로 관리하며 Config.json에 저장합니다.
        /// </summary>
        public double InspectionPassScoreThreshold
        {
            get { return _inspectionPassScoreThreshold; }
            set
            {
                double normalized = Math.Max(0d, Math.Min(100d, value));
                normalized = Math.Round(normalized, 2, MidpointRounding.AwayFromZero);
                if (SetProperty(ref _inspectionPassScoreThreshold, normalized))
                {
                    OnPropertyChanged("InspectionPassScoreThresholdText");
                }
            }
        }

        public string InspectionPassScoreThresholdText
        {
            get { return InspectionPassScoreThreshold.ToString("0.00", CultureInfo.InvariantCulture); }
            set
            {
                double parsedValue;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
                {
                    InspectionPassScoreThreshold = parsedValue;
                }
            }
        }

        /// <summary>
        /// 단일품목 등록의 기준이미지 유사도 기준입니다. 실제 VLAD 검색 API가 제공되면 이 값을 전달합니다.
        /// </summary>
        public double SinglePartSimilarityThreshold
        {
            get { return _singlePartSimilarityThreshold; }
            set
            {
                double normalized = Math.Max(0d, Math.Min(100d, value));
                normalized = Math.Round(normalized, 2, MidpointRounding.AwayFromZero);
                if (SetProperty(ref _singlePartSimilarityThreshold, normalized))
                {
                    OnPropertyChanged("SinglePartSimilarityThresholdText");
                }
            }
        }

        public string SinglePartSimilarityThresholdText
        {
            get { return SinglePartSimilarityThreshold.ToString("0.00", CultureInfo.InvariantCulture); }
            set
            {
                double parsedValue;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
                {
                    SinglePartSimilarityThreshold = parsedValue;
                }
            }
        }

        public string InspectionScoreSettingsMessage
        {
            get { return _inspectionScoreSettingsMessage; }
            private set { SetProperty(ref _inspectionScoreSettingsMessage, value); }
        }

        public string SimilaritySearchMessage
        {
            get { return _similaritySearchMessage; }
            private set { SetProperty(ref _similaritySearchMessage, value); }
        }

        public bool IsTrainingReservationEnabled
        {
            get { return _isTrainingReservationEnabled; }
            set
            {
                if (SetProperty(ref _isTrainingReservationEnabled, value) && !value)
                {
                    CancelImageTrainingSchedule("이미지 학습 예약을 해제했습니다.");
                }
            }
        }

        public string TrainingScheduleText
        {
            get { return _trainingScheduleText; }
            set { SetProperty(ref _trainingScheduleText, value); }
        }

        public string TrainingStatusMessage
        {
            get { return _trainingStatusMessage; }
            set { SetProperty(ref _trainingStatusMessage, value); }
        }

        public bool IsDailyTrainingEnabled
        {
            get { return _isDailyTrainingEnabled; }
            set
            {
                if (SetProperty(ref _isDailyTrainingEnabled, value))
                {
                    if (!value)
                    {
                        _lastDailyTrainingDate = null;
                        _appliedDailyTrainingTime = null;
                        TrainingStatusMessage = "매일 학습 예약을 해제했습니다.";
                    }

                    UpdateTrainingScheduleTimerState();
                }
            }
        }

        public string DailyTrainingTimeText
        {
            get { return _dailyTrainingTimeText; }
            set { SetProperty(ref _dailyTrainingTimeText, value); }
        }

        public string TrainingCurrentStatus
        {
            get { return _trainingCurrentStatus; }
            private set { SetProperty(ref _trainingCurrentStatus, value); }
        }

        /// <summary>
        /// 이미지 학습 프로세스가 시작되어 아직 종료되지 않은 동안 true입니다.
        /// 메인 화면 상단의 "학습중" 점멸 표시가 이 값을 구독합니다.
        /// </summary>
        public bool IsImageTrainingRunning
        {
            get { return _isImageTrainingRunning; }
        }

        public int TrainingProgress
        {
            get { return _trainingProgress; }
            private set
            {
                if (SetProperty(ref _trainingProgress, value))
                {
                    OnPropertyChanged("TrainingProgressText");
                }
            }
        }

        public string TrainingProgressText
        {
            get { return TrainingProgress.ToString(CultureInfo.InvariantCulture) + "%"; }
        }

        public string TrainingCurrentMessage
        {
            get { return _trainingCurrentMessage; }
            private set { SetProperty(ref _trainingCurrentMessage, value); }
        }

        public string TrainingErrorCode
        {
            get { return _trainingErrorCode; }
            private set { SetProperty(ref _trainingErrorCode, value); }
        }

        public string TrainingErrorMessage
        {
            get { return _trainingErrorMessage; }
            private set { SetProperty(ref _trainingErrorMessage, value); }
        }

        public string TrainingTimeSummary
        {
            get
            {
                string started = _trainingStartedAt.HasValue
                    ? _trainingStartedAt.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : "-";
                string ended = _trainingEndedAt.HasValue
                    ? _trainingEndedAt.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    : "-";
                DateTime elapsedEnd = _trainingEndedAt ?? DateTime.Now;
                string elapsed = _trainingStartedAt.HasValue
                    ? (elapsedEnd - _trainingStartedAt.Value).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                    : "-";
                return "시작: " + started + " / 종료: " + ended + " / 경과: " + elapsed;
            }
        }

        public CameraChannelStatusViewModel SelectedCameraChannel
        {
            get { return _selectedCameraChannel; }
            set { SetProperty(ref _selectedCameraChannel, value); }
        }

        public string ApplicationVersionText
        {
            get { return _applicationVersionText; }
            private set { SetProperty(ref _applicationVersionText, value); }
        }

        public bool IsFreeSpaceAutoCleanupEnabled
        {
            get { return _isFreeSpaceAutoCleanupEnabled; }
            set { SetProperty(ref _isFreeSpaceAutoCleanupEnabled, value); }
        }

        public string MinimumFreeSpacePercentText
        {
            get { return _minimumFreeSpacePercentText; }
            set { SetProperty(ref _minimumFreeSpacePercentText, value); }
        }

        public bool IsRetentionPeriodCleanupEnabled
        {
            get { return _isRetentionPeriodCleanupEnabled; }
            set { SetProperty(ref _isRetentionPeriodCleanupEnabled, value); }
        }

        public string RetentionDaysText
        {
            get { return _retentionDaysText; }
            set { SetProperty(ref _retentionDaysText, value); }
        }

        public string RetentionStatusMessage
        {
            get { return _retentionStatusMessage; }
            set { SetProperty(ref _retentionStatusMessage, value); }
        }

        private void LoadParts()
        {
            _partDataStore.LoadFromDatabase();
            MigrateReferenceImageFileNames();
            RefreshPartCollectionsFromDataStore();
        }

        /// <summary>
        /// 예전 이름으로 저장된 기준 이미지 파일을 현재 규칙으로 바꿉니다.
        ///
        /// <para>
        /// 프로그램을 시작할 때 한 번 돕니다. 이미 새 규칙인 파일은 건드리지 않으므로
        /// 두 번째 실행부터는 아무 일도 하지 않습니다.
        /// </para>
        ///
        /// <para>
        /// 파일 이름만 바꾸면 DB의 경로가 어긋나므로, 바뀐 부품은 곧바로 다시 저장해
        /// 경로를 맞춥니다. 이 작업이 실패해도 프로그램은 계속 떠야 하므로
        /// 사유만 남기고 넘어갑니다.
        /// </para>
        /// </summary>
        private void MigrateReferenceImageFileNames()
        {
            ReferenceImageFileNameMigrator migrator = new ReferenceImageFileNameMigrator();
            int totalRenamed = 0;
            IList<string> totalErrors = new List<string>();

            foreach (Part part in _partDataStore.GetParts())
            {
                int renamedCount;
                IList<string> errors;

                try
                {
                    if (!migrator.MigratePart(part, out renamedCount, out errors))
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    totalErrors.Add((part == null ? "-" : part.PartNo) + " : " + ex.Message);
                    continue;
                }

                totalRenamed += renamedCount;
                foreach (string error in errors)
                {
                    totalErrors.Add(error);
                }

                // 바뀐 경로를 DB에 반영합니다. 여기서 실패하면 파일과 DB가 어긋나므로
                // 사유를 남겨 두어야 나중에 원인을 찾을 수 있습니다.
                string saveMessage = _partDataStore.SavePart(part);
                if (saveMessage != PartCatalogService.SaveSuccessMessage)
                {
                    totalErrors.Add(part.PartNo + " 경로 갱신 실패 : " + saveMessage);
                }
            }

            if (totalRenamed > 0 || totalErrors.Count > 0)
            {
                _partDataStore.LoadFromDatabase();
            }

            if (totalRenamed > 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    "기준 이미지 파일 이름을 현재 규칙으로 바꿨습니다. 변경 " + totalRenamed + "건.");
            }

            foreach (string error in totalErrors)
            {
                System.Diagnostics.Debug.WriteLine("기준 이미지 이름 변경 실패: " + error);
            }
        }

        private void RefreshPartCollectionsFromDataStore()
        {
            RefreshPartCollectionsFromDataStore(null);
        }

        /// <summary>
        /// DB 를 다시 읽고, 보고 있던 부품을 그대로 다시 고릅니다.
        /// </summary>
        /// <param name="savedPartNo">
        /// 방금 저장한 품번입니다. 저장이 아닌 곳에서 부를 때는 비웁니다.
        ///
        /// <para>
        /// 이 값이 없으면 저장 '전' 품번으로 다시 고르게 됩니다. 품번을 바꿔 저장했을 때
        /// 그 옛 품번이 DB 에 그대로 남아 있으면 그것을 골라 화면이 예전 부품 정보로 되돌아갔고,
        /// 옛 품번이 없으면 아무것도 못 찾아 목록 맨 위 부품으로 바뀌었습니다.
        /// 저장한 품번을 알려 주면 그것을 먼저 찾습니다.
        /// </para>
        /// </param>
        private void RefreshPartCollectionsFromDataStore(string savedPartNo)
        {
            bool hasSavedPartNo = !string.IsNullOrWhiteSpace(savedPartNo);
            string selectedPartNo = SelectedPart == null ? string.Empty : SelectedPart.PartNo;
            string selectedDbPartNo = hasSavedPartNo
                ? savedPartNo
                : (SelectedDbPart == null ? string.Empty : SelectedDbPart.PartNo);
            string selectedRegistrationPartNo = hasSavedPartNo
                ? savedPartNo
                : (SelectedRegistrationPart == null ? string.Empty : SelectedRegistrationPart.PartNo);

            // 목록을 비우고 다시 채우는 동안 그리드의 선택 되밀림을 막습니다.
            // 선택 복원은 이 블록이 끝난 뒤 아래에서 명시적으로 합니다.
            _isSynchronizingPartSelection = true;
            try
            {
                Parts.Clear();
                foreach (Part part in _partDataStore.GetParts())
                {
                    Parts.Add(new PartViewModel(part));
                }

                ApplySearchFilters();
            }
            finally
            {
                _isSynchronizingPartSelection = false;
            }
            if (!string.IsNullOrWhiteSpace(selectedPartNo))
            {
                SelectedPart = FindPartViewModel(selectedPartNo);
            }

            if (!string.IsNullOrWhiteSpace(selectedDbPartNo))
            {
                SelectedDbPart = FindDbPartViewModel(selectedDbPartNo);
            }

            if (!string.IsNullOrWhiteSpace(selectedRegistrationPartNo))
            {
                SelectedRegistrationPart = FindDbPartViewModel(selectedRegistrationPartNo);
            }

            if (Parts.Count > 0 && SelectedPart == null)
            {
                SelectedPart = Parts[0];
            }

            // 방금 저장한 품번을 알고 있으면 맨 위 부품으로 대신 고르지 않습니다.
            //
            // 저장한 것이 목록에 없다는 것은 뜻밖의 일입니다. 그런 때 엉뚱한 부품을 골라 두면
            // 사용자는 방금 저장한 것이 그렇게 바뀐 줄로 봅니다. 차라리 비워 두는 편이 낫습니다.
            if (!hasSavedPartNo && DbParts.Count > 0 && SelectedDbPart == null)
            {
                SelectedDbPart = DbParts[0];
            }

            if (!hasSavedPartNo && DbParts.Count > 0 && SelectedRegistrationPart == null)
            {
                SelectedRegistrationPart = DbParts[0];
            }

            if (Parts.Count == 0)
            {
                SelectedPart = null;
                SelectedDbPart = null;
                SelectedRegistrationPart = null;
            }
            else
            {
                // 선택 객체가 동일한 경우에도 DB를 다시 읽은 최신 측정부와 이미지를 확실히 연결합니다.
                ApplySelectedPart();
            }
        }

        private void ApplySelectedPart()
        {
            if (SelectedPart == null)
            {
                ClearSelectedPartDetails();
                return;
            }

            InputCode = SelectedPart.PartNo;
            LoadReferenceImages(SelectedPart.Part);
            LoadInspectionMeasurementRegions(SelectedPart.Part);
        }

        private void ApplySelectedDbPart()
        {
            if (SelectedDbPart == null)
            {
                ClearDbDetail();
                return;
            }

            LoadDbDetail(SelectedDbPart.Part);
        }

        private void ApplySelectedRegistrationPart()
        {
            if (SelectedRegistrationPart == null)
            {
                return;
            }

            LoadRegistrationForm(SelectedRegistrationPart.Part);
        }

        private void ClearDbDetail()
        {
            DbDetailMeasurements.Clear();
            DbDetailImages.Clear();
            DbDetailImagePreviews.Clear();
            SelectedDbDetailImage = null;
        }

        private void ClearSelectedPartDetails()
        {
            ClearDbDetail();
            InspectionMeasurements.Clear();
            InitializeImageSlots();
        }

        private void InitializeReferenceImageViewTypes()
        {
            ReferenceImageViewTypes.Clear();
            ReferenceImageViewTypes.Add(ImageViewType.Top.ToString());
            ReferenceImageViewTypes.Add(ImageViewType.Front.ToString());
            ReferenceImageViewTypes.Add(ImageViewType.Back.ToString());
            ReferenceImageViewTypes.Add(ImageViewType.Left.ToString());
            ReferenceImageViewTypes.Add(ImageViewType.Right.ToString());
            ReferenceImageViewTypes.Add(ImageViewType.Thickness.ToString());
            SelectedReferenceImageViewType = ImageViewType.Top.ToString();
        }

        private void InitializeImageSlots()
        {
            ImageSlots.Clear();

            // 영상은 콜백 프레임으로만 그립니다. LibVLC 경로는 콜백 단일화로 걷어냈습니다.
            bool useVideoCrop = false;
            int cropIntervalMilliseconds = 3000;
            try
            {
                VladRuntimeSettings runtimeSettings = VladRuntimeSettings.Load();
                // 크롭은 정해진 사양이라 끄지 않습니다.
                // 잘라 내지 못한 프레임은 원본 그대로 그리므로 화면이 비지 않습니다.
                useVideoCrop = true;
                cropIntervalMilliseconds = runtimeSettings.CallbackVideoCropIntervalMilliseconds;
            }
            catch
            {
                // 설정을 읽지 못하면 지금까지 쓰던 방식을 그대로 씁니다.
            }

            _useCallbackVideoCrop = useVideoCrop;
            _callbackVideoCropIntervalMilliseconds = cropIntervalMilliseconds;

            AddImageSlot("Top View");
            AddImageSlot("Front View");
            AddImageSlot("Back View");
            AddImageSlot("Left View");
            AddImageSlot("Right View");
            AddImageSlot("Thickness");
            ApplyLiveStreamUrls();
        }

        private void AddImageSlot(string title)
        {
            ImageSlotViewModel slot = new ImageSlotViewModel();
            slot.Title = title;

            // 슬롯을 넣는 차례가 카메라 번호와 같습니다.
            // Top, Front, Back, Left, Right, Thickness 순서로 0부터 매깁니다.
            slot.MonitorIndex = RtspMonitorIndexPolicy.FromViewType((ImageViewType)ImageSlots.Count);
            slot.UseVideoCrop = _useCallbackVideoCrop;
            slot.VideoCropIntervalMilliseconds = _callbackVideoCropIntervalMilliseconds;
            slot.ReferenceImagePath = string.Empty;
            slot.LiveImagePath = string.Empty;
            slot.LiveStreamUrl = string.Empty;
            slot.IsLiveStreamEnabled = false;
            slot.IsCapturedStillVisible = false;
            slot.StatusText = "카메라 대기";
            slot.ResultText = "READY";
            slot.ResultBrush = "#66788A";
            slot.ScoreText = "Score: -";
            slot.ScoreBrush = "#253747";
            slot.DimensionText = "W: -  H: -  D: -";
            ImageSlots.Add(slot);
        }

        private void LoadReferenceImages(Part part)
        {
            InitializeImageSlots();

            foreach (PartImage image in BuildOrderedUniqueImages(part.Images))
            {
                int index = GetImageViewTypeSortOrder(image.ViewType);
                if (index >= ImageSlots.Count)
                {
                    continue;
                }

                // 측정부가 있는 카메라는 선이 그려진 좌표 이미지를 보여줍니다.
                // 좌표를 아직 찍지 않았으면 선만 없을 뿐 같은 사진이므로 원본을 그대로 씁니다.
                string coordinateImagePath = ResolveSlotCoordinateImagePath(part, image.ViewType);

                ImageSlots[index].ReferenceImagePath = string.IsNullOrWhiteSpace(coordinateImagePath)
                    ? image.FilePath
                    : coordinateImagePath;
            }

            // 기준 이미지 상태 문구를 적용한 뒤 스트림 설정을 다시 반영해
            // 중복 URL 또는 RTSP 준비 상태가 화면에서 덮어써지지 않게 합니다.
            ApplyLiveStreamUrls();
            _referenceImagePopupService.Update(part);
        }

        private void LoadInspectionMeasurementRegions(Part part)
        {
            InspectionMeasurements.Clear();
            foreach (MeasurementRegion region in GetMeasurementRegionsInDisplayOrder(part))
            {
                InspectionMeasurements.Add(new MeasurementRowViewModel(region));
            }
        }

        private void LoadDbDetail(Part part)
        {
            DbDetailMeasurements.Clear();

            foreach (MeasurementRegion region in GetMeasurementRegionsInDisplayOrder(part))
            {
                DbDetailMeasurements.Add(new MeasurementRowViewModel(region));
            }

            _dbDetailPart = part;
            _dbDetailSetNumbers = BuildDbDetailSetNumbers(part);

            // 처음에는 가장 최근 벌을 보여 줍니다.
            _dbDetailSetIndex = _dbDetailSetNumbers.Count == 0 ? 0 : _dbDetailSetNumbers.Count - 1;
            ApplyDbDetailSet();
        }

        /// <summary>
        /// 이 부품에 저장된 벌 번호를 오래된 것부터 모읍니다.
        /// </summary>
        private IList<int> BuildDbDetailSetNumbers(Part part)
        {
            List<int> setNumbers = new List<int>();
            if (part == null || part.Images == null)
            {
                return setNumbers;
            }

            foreach (PartImage image in part.Images)
            {
                if (image == null)
                {
                    continue;
                }

                // 옛 자료에는 회차가 비어 있어 1벌로 봅니다.
                int setNo = image.SetNo < 1 ? 1 : image.SetNo;
                if (!setNumbers.Contains(setNo))
                {
                    setNumbers.Add(setNo);
                }
            }

            setNumbers.Sort();
            return setNumbers;
        }

        private DateTime FindDbDetailSetSavedAt(int setNo)
        {
            if (_dbDetailPart == null || _dbDetailPart.Images == null)
            {
                return DateTime.MinValue;
            }

            DateTime savedAt = DateTime.MinValue;
            foreach (PartImage image in _dbDetailPart.Images)
            {
                if (image == null)
                {
                    continue;
                }

                int imageSetNo = image.SetNo < 1 ? 1 : image.SetNo;
                if (imageSetNo != setNo)
                {
                    continue;
                }

                // 한 벌 안에서 시각이 조금씩 다르면 가장 이른 쪽을 그 벌의 저장 시각으로 봅니다.
                if (savedAt == DateTime.MinValue || image.CapturedAt < savedAt)
                {
                    savedAt = image.CapturedAt;
                }
            }

            return savedAt;
        }

        /// <summary>
        /// 지금 고른 벌의 이미지 여섯 장을 화면에 올립니다.
        /// </summary>
        private void ApplyDbDetailSet()
        {
            DbDetailImages.Clear();

            if (_dbDetailPart != null)
            {
                IList<PartImage> imagesInSet = FilterImagesBySet(_dbDetailPart.Images, CurrentDbDetailSetNo());
                foreach (ImageEditViewModel imageViewModel in BuildImageEditViewModels(imagesInSet))
                {
                    DbDetailImages.Add(imageViewModel);
                }
            }

            RefreshDbDetailImagePreviews(_dbDetailPart);
            SelectedDbDetailImage = DbDetailImages.Count > 0 ? DbDetailImages[0] : null;

            OnPropertyChanged("DbDetailSetDisplayName");
            OnPropertyChanged("HasMultipleDbDetailSets");
            OnPropertyChanged("CanShowPreviousDbDetailSet");
            OnPropertyChanged("CanShowNextDbDetailSet");
        }

        private int CurrentDbDetailSetNo()
        {
            if (_dbDetailSetNumbers.Count == 0)
            {
                return 0;
            }

            return _dbDetailSetNumbers[_dbDetailSetIndex];
        }

        private IList<PartImage> FilterImagesBySet(IList<PartImage> images, int setNo)
        {
            List<PartImage> result = new List<PartImage>();
            if (images == null)
            {
                return result;
            }

            foreach (PartImage image in images)
            {
                if (image == null)
                {
                    continue;
                }

                int imageSetNo = image.SetNo < 1 ? 1 : image.SetNo;
                if (imageSetNo == setNo)
                {
                    result.Add(image);
                }
            }

            return result;
        }

        private void ExecuteShowPreviousDbDetailSet(object parameter)
        {
            if (_dbDetailSetIndex <= 0)
            {
                return;
            }

            _dbDetailSetIndex--;
            ApplyDbDetailSet();
        }

        private void ExecuteShowNextDbDetailSet(object parameter)
        {
            if (_dbDetailSetIndex >= _dbDetailSetNumbers.Count - 1)
            {
                return;
            }

            _dbDetailSetIndex++;
            ApplyDbDetailSet();
        }

        private void LoadRegistrationForm(Part part)
        {
            RegistrationPartNo = part.PartNo;
            RegistrationPartName = part.PartName;
            RegistrationCategoryCode = part.CategoryCode;
            RegistrationCategoryDescription = part.CategoryDescription;
            RegistrationMemo = part.Memo;

            LoadRegistrationMeasurementPoints(part);
            LoadRegistrationImages(part);
            _deleteRequested = false;
            RegistrationMessage = "선택한 부품 정보를 편집할 수 있습니다.";
        }

        /// <summary>
        /// 등록 OCR이 DB에 없는 품번을 반환한 경우 신규 입력 폼을 준비합니다.
        /// OCR 임시 이미지와 JSON은 DB 저장 전까지 유지하므로 이 메서드에서는 정리하지 않습니다.
        /// </summary>
        private void PrepareRegistrationFormForNewOcrPart(string partNo)
        {
            SelectedRegistrationPart = null;
            RegistrationPartNo = partNo ?? string.Empty;
            RegistrationPartName = string.Empty;
            RegistrationCategoryCode = string.Empty;
            RegistrationCategoryDescription = string.Empty;
            RegistrationMemo = string.Empty;
            RegistrationImages.Clear();
            SelectedRegistrationImage = null;
            RegistrationCoordinateImagePath = string.Empty;
            RefreshRegistrationImagePreviews();
            InitializeEmptyRegistrationPoints();
            _deleteRequested = false;
        }

        private void LoadRegistrationMeasurementPoints(Part part)
        {
            RegistrationMeasurementPoints.Clear();
            int fallbackIndex = 1;
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                // 카메라마다 다섯 개까지입니다.
                // 전체를 세면 Top 다섯 개를 읽은 뒤 Thickness 가 한 개도 화면에 오르지 않습니다.
                if (CountMeasurementPoints(region.ViewType) >= MeasurementPointPolicy.MaxCount)
                {
                    continue;
                }

                MeasurementPointViewModel point = MeasurementPointViewModel.FromRegion(region, fallbackIndex);
                RegistrationMeasurementPoints.Add(point);
                fallbackIndex++;
            }

            ReindexMeasurementPoints();
            if (RegistrationMeasurementPoints.Count > 0)
            {
                SelectedRegistrationMeasurementPoint = RegistrationMeasurementPoints[0];
            }
            else
            {
                SelectedRegistrationMeasurementPoint = null;
            }
        }

        private int ResolveMeasurementSetIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 1;
            }

            StringBuilder numberBuilder = new StringBuilder();
            bool numberStarted = false;
            foreach (char character in name)
            {
                if (char.IsDigit(character))
                {
                    numberBuilder.Append(character);
                    numberStarted = true;
                }
                else if (numberStarted)
                {
                    break;
                }
            }

            int parsed;
            if (int.TryParse(numberBuilder.ToString(), out parsed) && parsed > 0)
            {
                return parsed;
            }

            return 1;
        }

        private string BuildMeasurementSetName(int setIndex)
        {
            if (setIndex <= 1)
            {
                return "측정부";
            }

            return "측정부" + setIndex.ToString();
        }

        private void ApplyRegionToSet(MeasurementSetViewModel set, MeasurementRegion region)
        {
            string value = region.NominalValue.ToString("0.###");
            string tolerance = FormatTolerance(region);
            string unit = string.IsNullOrWhiteSpace(region.Unit) ? "mm" : region.Unit;
            if (region.Name.Contains("길이"))
            {
                set.LengthValue = value;
                set.LengthTolerance = tolerance;
                set.Unit = unit;
            }
            else if (region.Name.Contains("너비"))
            {
                set.WidthValue = value;
                set.WidthTolerance = tolerance;
                set.Unit = unit;
            }
            else if (region.Name.Contains("높이"))
            {
                set.HeightValue = value;
                set.HeightTolerance = tolerance;
                set.Unit = unit;
            }
            else if (region.Name.Contains("두께"))
            {
                set.ThicknessValue = value;
                set.ThicknessTolerance = tolerance;
                set.Unit = unit;
            }
        }

        /// <summary>
        /// 한 칸에 요약해 보여줄 대표 허용값입니다. Min과 Max 중 큰 쪽을 씁니다.
        /// MeasurementRegion은 허용값을 크기(양수)로만 들고 있으므로 부호 처리가 필요 없습니다.
        /// </summary>
        private string FormatTolerance(MeasurementRegion region)
        {
            decimal tolerance = region.ToleranceMax >= region.ToleranceMin
                ? region.ToleranceMax
                : region.ToleranceMin;
            return tolerance.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 허용 범위를 사람이 읽기 쉽게 "-Min ~ +Max"로 표기합니다.
        /// 부호는 여기서만 붙입니다. 저장 값 자체는 크기입니다.
        /// </summary>
        private string FormatToleranceRange(MeasurementRegion region)
        {
            return "-" + region.ToleranceMin.ToString("0.###", CultureInfo.InvariantCulture) +
                   " ~ +" +
                   region.ToleranceMax.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void LoadRegistrationImages(Part part)
        {
            RegistrationImages.Clear();
            foreach (ImageEditViewModel imageViewModel in BuildImageEditViewModels(part.Images))
            {
                RegistrationImages.Add(imageViewModel);
            }

            if (RegistrationImages.Count > 0)
            {
                SelectedRegistrationImage = RegistrationImages[0];
            }
            else
            {
                SelectedRegistrationImage = null;
            }

            RegistrationCoordinateImagePath = ResolveRegistrationCoordinateImagePath(part);
            RefreshRegistrationImagePreviews();
        }

        private void RefreshDbDetailImagePreviews(Part part)
        {
            if (part == null)
            {
                DbDetailImagePreviews.Clear();
                return;
            }

            BuildReferenceImagePreviews(
                DbDetailImagePreviews,
                DbDetailImages,
                delegate (ImageViewType viewType) { return ResolveCommittedCoordinateImagePath(part, viewType); },
                true);
        }

        private void RefreshRegistrationImagePreviews()
        {
            if (RegistrationImagePreviews == null || RegistrationImages == null)
            {
                return;
            }

            BuildReferenceImagePreviews(
                RegistrationImagePreviews,
                RegistrationImages,
                null,
                false);
        }

        /// <summary>
        /// 기준 이미지와 측정부 좌표 이미지를 미리보기 목록으로 만듭니다.
        ///
        /// <para>
        /// 좌표 이미지는 카메라마다 한 장씩 있습니다. 예전에는 Thickness 한 장만 붙여,
        /// Top에 측정부를 두어도 그 그림이 목록에 나오지 않았습니다.
        /// </para>
        /// </summary>
        /// <summary>
        /// 저장한 원본을 그 카메라의 크롭 자리로 잘라 미리보기용 그림을 만듭니다.
        ///
        /// <para>
        /// 파일은 원본 그대로 두고 보여 줄 때만 자릅니다. 자를 자리를 아직 모르거나
        /// 자리가 그림 밖으로 나가면 원본을 그대로 돌려줍니다.
        /// </para>
        /// </summary>
        private static System.Windows.Media.ImageSource BuildCroppedPreviewSource(
            string filePath, ImageViewType viewType)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                BitmapImage source = new BitmapImage();
                source.BeginInit();
                source.CacheOption = BitmapCacheOption.OnLoad;
                source.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                source.UriSource = new Uri(filePath, UriKind.Absolute);
                source.EndInit();
                source.Freeze();

                if (!MeasurementPointPolicy.IsSupportedViewType(viewType) &&
                    viewType == ImageViewType.Unclassified)
                {
                    // 측정부 좌표 그림은 이미 원본 위에 선을 그린 것이라 자르지 않습니다.
                    return source;
                }

                int monitorIndex = RtspMonitorIndexPolicy.FromViewType(viewType);
                CropRegion region = AI.Vision.IOInspector.Vision.Services.CallbackFrameCropStage.GetLatestRegion(monitorIndex);
                if (region == null || !region.IsValid)
                {
                    return source;
                }

                System.Windows.Int32Rect rect = new System.Windows.Int32Rect(
                    Math.Max(0, region.X),
                    Math.Max(0, region.Y),
                    region.Width,
                    region.Height);

                if (rect.X + rect.Width > source.PixelWidth ||
                    rect.Y + rect.Height > source.PixelHeight ||
                    rect.Width <= 0 ||
                    rect.Height <= 0)
                {
                    // 자리가 그림 밖으로 나가면 자르지 않습니다. 카메라 해상도가 바뀐 경우입니다.
                    return source;
                }

                CroppedBitmap cropped = new CroppedBitmap(source, rect);
                cropped.Freeze();
                return cropped;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("미리보기 크롭 실패: " + ex.Message);
                return null;
            }
        }

        private void BuildReferenceImagePreviews(
            ObservableCollection<ReferenceImagePreviewViewModel> target,
            IEnumerable<ImageEditViewModel> images,
            Func<ImageViewType, string> resolveCoordinateImagePath,
            bool includeCoordinateImage)
        {
            target.Clear();
            int order = 1;
            foreach (ImageViewType viewType in GetReferenceImageViewOrder())
            {
                ReferenceImagePreviewViewModel preview = new ReferenceImagePreviewViewModel();
                preview.Order = order;
                preview.Title = GetReferenceImageDisplayName(viewType);
                preview.ViewType = viewType;
                preview.FilePath = FindImageFilePath(images, viewType);
                preview.PreviewSource = BuildCroppedPreviewSource(preview.FilePath, viewType);
                preview.ClearSimilarityCandidates(preview.HasImage ? "검색 전" : "이미지 없음");
                target.Add(preview);
                order++;
            }

            if (!includeCoordinateImage || resolveCoordinateImagePath == null)
            {
                return;
            }

            foreach (ImageViewType coordinateViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                string coordinateImagePath = resolveCoordinateImagePath(coordinateViewType);
                if (string.IsNullOrWhiteSpace(coordinateImagePath))
                {
                    continue;
                }

                ReferenceImagePreviewViewModel coordinatePreview = new ReferenceImagePreviewViewModel();
                coordinatePreview.Order = order;
                coordinatePreview.Title = "측정부좌표 " + coordinateViewType.ToString();
                coordinatePreview.ViewType = ImageViewType.Unclassified;
                coordinatePreview.FilePath = coordinateImagePath;
                coordinatePreview.PreviewSource = BuildCroppedPreviewSource(coordinateImagePath, ImageViewType.Unclassified);
                coordinatePreview.ClearSimilarityCandidates("유사도 검색 제외");
                target.Add(coordinatePreview);
                order++;
            }
        }

        /// <summary>
        /// 미리보기에 쓸 그 카메라의 이미지를 고릅니다. 벌이 여러 개면 가장 최근 것입니다.
        /// </summary>
        private string FindImageFilePath(
            IEnumerable<ImageEditViewModel> images,
            ImageViewType viewType)
        {
            if (images == null)
            {
                return string.Empty;
            }

            ImageEditViewModel latest = null;
            foreach (ImageEditViewModel image in images)
            {
                if (image == null || image.Image == null || image.Image.ViewType != viewType)
                {
                    continue;
                }

                if (latest == null || image.Image.SetNo >= latest.Image.SetNo)
                {
                    latest = image;
                }
            }

            return latest == null ? string.Empty : latest.FilePath;
        }

        private string GetReferenceImageDisplayName(ImageViewType viewType)
        {
            if (viewType == ImageViewType.Top)
            {
                return "Top";
            }

            if (viewType == ImageViewType.Front)
            {
                return "Front";
            }

            if (viewType == ImageViewType.Back)
            {
                return "Back";
            }

            if (viewType == ImageViewType.Left)
            {
                return "Left";
            }

            if (viewType == ImageViewType.Right)
            {
                return "Right";
            }

            if (viewType == ImageViewType.Thickness)
            {
                return "Thickness";
            }

            return "미분류";
        }

        private IList<ImageEditViewModel> BuildImageEditViewModels(IList<PartImage> images)
        {
            IList<ImageEditViewModel> viewModels = new List<ImageEditViewModel>();
            int order = 1;
            foreach (PartImage image in BuildOrderedUniqueImages(images))
            {
                viewModels.Add(new ImageEditViewModel(image, order));
                order++;
            }

            return viewModels;
        }

        private IList<PartImage> BuildOrderedUniqueImages(IList<PartImage> images)
        {
            IList<PartImage> orderedImages = new List<PartImage>();
            if (images == null)
            {
                return orderedImages;
            }

            ImageViewType[] viewOrder = GetReferenceImageViewOrder();
            foreach (ImageViewType viewType in viewOrder)
            {
                PartImage image = FindFirstImageByViewType(images, viewType);
                if (image != null)
                {
                    orderedImages.Add(image);
                }
            }

            return orderedImages;
        }

        /// <summary>
        /// 이 방향의 대표 기준 이미지를 찾습니다.
        ///
        /// <para>
        /// 기준 이미지는 저장할 때마다 그 시각의 것이 한 벌씩 쌓입니다. 화면 미리보기와
        /// 등록 완료 판단에는 <b>가장 최근에 저장한 것</b>을 씁니다. 목록의 첫 번째를 그냥
        /// 집으면 예전 이미지가 대표로 잡힐 수 있습니다.
        /// </para>
        ///
        /// <para>
        /// 임시 이미지(IsTemporary)는 아직 확정 전이라 저장 시각이 없습니다.
        /// 부품등록 화면에서 작업 중인 상태이므로 확정된 것보다 우선해서 보여줍니다.
        /// </para>
        /// </summary>
        private PartImage FindFirstImageByViewType(IList<PartImage> images, ImageViewType viewType)
        {
            if (images == null)
            {
                return null;
            }

            PartImage latestImage = null;
            PartImage temporaryImage = null;

            foreach (PartImage image in images)
            {
                if (image == null || image.ViewType != viewType)
                {
                    continue;
                }

                if (image.IsTemporary)
                {
                    temporaryImage = image;
                    continue;
                }

                if (latestImage == null || image.CapturedAt > latestImage.CapturedAt)
                {
                    latestImage = image;
                }
            }

            return temporaryImage != null ? temporaryImage : latestImage;
        }

        /// <summary>
        /// 부품등록 화면에 올라와 있는 기준 이미지들을 모읍니다.
        /// 다음 벌 번호를 정할 때 씁니다.
        /// </summary>
        private IList<PartImage> BuildRegistrationPartImages()
        {
            IList<PartImage> images = new List<PartImage>();
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                if (imageViewModel != null && imageViewModel.Image != null)
                {
                    images.Add(imageViewModel.Image);
                }
            }

            return images;
        }

        private ImageViewType[] GetReferenceImageViewOrder()
        {
            return new ImageViewType[]
            {
                ImageViewType.Top,
                ImageViewType.Front,
                ImageViewType.Back,
                ImageViewType.Left,
                ImageViewType.Right,
                ImageViewType.Thickness
            };
        }

        private int GetImageViewTypeSortOrder(ImageViewType viewType)
        {
            ImageViewType[] viewOrder = GetReferenceImageViewOrder();
            for (int index = 0; index < viewOrder.Length; index++)
            {
                if (viewOrder[index] == viewType)
                {
                    return index;
                }
            }

            return viewOrder.Length;
        }

        /// <summary>
        /// 사용 설정된 RTSP/NVR RTSP 채널을 검사 UI의 6방향 슬롯에 연결합니다.
        /// 실제 영상은 XAML의 CallbackVideoView가 콜백 프레임으로 그립니다.
        /// </summary>
        private void ApplyLiveStreamUrls()
        {
            // 화면에 영상을 올리기 전에 카메라 연결이 모두 살아 있는지 확인합니다.
            //
            // 콜백 프레임은 카메라마다 따로 등록해야 들어옵니다. 등록이 빠진 카메라는
            // 화면이 영영 비어 있는데, 비어 있다는 것 말고는 단서가 없어 원인을 찾기 어렵습니다.
            // 여기서 한 번 더 확인해 두면 어느 길로 들어와도 여섯 대가 모두 붙습니다.
            // 이미 붙어 있는 카메라는 그냥 넘어가므로 여러 번 불러도 부담이 없습니다.
            try
            {
                _cameraService.EnsureLiveFrameSources();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("카메라 실시간 연결 확인 실패: " + ex.Message);
            }

            foreach (ImageSlotViewModel slot in ImageSlots)
            {
                slot.LiveStreamUrl = string.Empty;
                slot.FrameWidth = 0;
                slot.FrameHeight = 0;
                slot.IsLiveStreamEnabled = false;
            }

            HashSet<string> assignedStreamUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IList<CameraChannelConfig> channels;
            try
            {
                channels = _cameraService.GetChannelConfigurations();
            }
            catch
            {
                return;
            }

            foreach (CameraChannelConfig channel in channels)
            {
                if (!IsRtspStreamChannel(channel))
                {
                    continue;
                }

                int slotIndex = GetImageViewTypeSortOrder(channel.ViewType);
                if (slotIndex >= ImageSlots.Count)
                {
                    continue;
                }

                string streamUrl = BuildLiveStreamUrl(channel);
                if (string.IsNullOrWhiteSpace(streamUrl))
                {
                    continue;
                }

                ImageSlotViewModel slot = ImageSlots[slotIndex];
                if (assignedStreamUrls.Contains(streamUrl))
                {
                    slot.LiveStreamUrl = string.Empty;
                    slot.FrameWidth = 0;
                    slot.FrameHeight = 0;
                    slot.IsLiveStreamEnabled = false;
                    slot.StatusText = "중복 RTSP URL - 최초 채널만 재생";
                    continue;
                }

                assignedStreamUrls.Add(streamUrl);
                // Config.json의 실제 RTSP 프레임 해상도를 영상 호스트에도 전달합니다.
                // 6:5, 4:3, 16:9가 혼재해도 화면에서 비율이 늘어나지 않게 하기 위한 값입니다.
                slot.FrameWidth = channel.Width;
                slot.FrameHeight = channel.Height;
                slot.LiveStreamUrl = streamUrl;
                slot.IsLiveStreamEnabled = true;
                if (string.IsNullOrWhiteSpace(slot.StatusText) || string.Equals(slot.StatusText, "카메라 대기", StringComparison.OrdinalIgnoreCase))
                {
                    slot.StatusText = "RTSP 스트림 준비";
                }
            }
        }

        private bool IsRtspStreamChannel(CameraChannelConfig channel)
        {
            if (channel == null || !channel.IsEnabled)
            {
                return false;
            }

            return channel.ConnectionType == CameraConnectionType.Rtsp
                   || channel.ConnectionType == CameraConnectionType.NvrRtsp;
        }

        private string BuildLiveStreamUrl(CameraChannelConfig channel)
        {
            if (channel == null)
            {
                return string.Empty;
            }

            // 미리보기는 전용 주소(서브 스트림)를 우선 사용합니다.
            // 6채널을 모두 메인 스트림으로 계속 열어두면 NVR 동시 전송 한계에 걸려 일부 채널이 끊깁니다.
            // 검사 촬영은 이 값을 쓰지 않고 CAM_RTSP_IP(메인 스트림) 그대로 사용하므로 화질에 영향이 없습니다.
            if (!string.IsNullOrWhiteSpace(channel.PreviewRtspUrl))
            {
                return channel.PreviewRtspUrl.Trim();
            }

            if (!string.IsNullOrWhiteSpace(channel.RtspUrl))
            {
                return channel.RtspUrl.Trim();
            }

            if (string.IsNullOrWhiteSpace(channel.IpAddress))
            {
                return string.Empty;
            }

            int port = channel.Port <= 0 ? 554 : channel.Port;
            string streamPath = string.IsNullOrWhiteSpace(channel.StreamPath) ? "trackID=1" : channel.StreamPath.Trim();
            while (streamPath.StartsWith("/"))
            {
                streamPath = streamPath.Substring(1);
            }

            string credential = string.Empty;
            if (!string.IsNullOrWhiteSpace(channel.UserName))
            {
                credential = Uri.EscapeDataString(channel.UserName.Trim());
                if (!string.IsNullOrEmpty(channel.Password))
                {
                    credential = credential + ":" + Uri.EscapeDataString(channel.Password);
                }

                credential = credential + "@";
            }

            return "rtsp://" + credential + channel.IpAddress.Trim() + ":" + port.ToString() + "/" + streamPath;
        }

        private void InitializeEmptyRegistrationPoints()
        {
            RegistrationMeasurementPoints.Clear();
            SelectedRegistrationMeasurementPoint = null;
        }

        private void InitializeMeasurementItemTypes()
        {
            MeasurementItemTypes.Clear();
            MeasurementItemTypes.Add("미설정");
            MeasurementItemTypes.Add("길이");
            MeasurementItemTypes.Add("너비");
            MeasurementItemTypes.Add("높이");
            MeasurementItemTypes.Add("두께");
        }

        private bool CanRunInspection(object parameter)
        {
            return !string.IsNullOrWhiteSpace(InputCode) && !_isInspectionRunning;
        }

        private bool CanResetInspectionScreen(object parameter)
        {
            return !_isInspectionRunning;
        }

        private Part ResolveInspectionPart(string inputCode)
        {
            Part part = _partDataStore.GetPart(inputCode);
            if (part != null)
            {
                return part;
            }

            if (SelectedPart != null &&
                string.Equals(SelectedPart.PartNo, inputCode, StringComparison.OrdinalIgnoreCase))
            {
                return SelectedPart.Part;
            }

            return null;
        }

        private bool HasRequiredReferenceImages(Part part, out string message)
        {
            if (part == null)
            {
                message = "검사할 부품 기준정보를 찾을 수 없습니다. 품번 검색 후 부품을 선택하세요.";
                return false;
            }

            IList<ImageViewType> requiredViewTypes = GetEnabledCameraViewTypes();
            if (requiredViewTypes.Count == 0)
            {
                if (HasAnyReferenceImageFile(part))
                {
                    message = string.Empty;
                    return true;
                }

                message = "기준 이미지가 등록되어 있지 않습니다. 부품등록 또는 검사UI의 기준 이미지 저장으로 이미지를 등록할 수 있습니다.";
                return false;
            }

            IList<string> missingViewNames = new List<string>();
            foreach (ImageViewType viewType in requiredViewTypes)
            {
                PartImage image = FindFirstImageByViewType(part.Images, viewType);
                if (!IsReferenceImageFileReady(image))
                {
                    missingViewNames.Add(BuildImageViewDisplayName(viewType));
                }
            }

            if (missingViewNames.Count == 0)
            {
                message = string.Empty;
                return true;
            }

            message = "기준 이미지가 없는 카메라 위치: " +
                      string.Join(", ", missingViewNames) +
                      ". 필요한 경우 기준 이미지를 먼저 저장하세요.";
            return false;
        }

        private bool TryRegisterCurrentFramesAsReferenceImagesForInspection(Part inspectionPart, out string message)
        {
            message = string.Empty;
            if (inspectionPart == null)
            {
                message = "기준 이미지를 등록할 부품 기준정보가 없습니다.";
                return false;
            }

            Part partToSave = ClonePart(inspectionPart);
            string validationMessage = _partDataStore.ValidatePartForSave(partToSave);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                message = validationMessage;
                return false;
            }

            // 저장 버튼을 누를 때마다 그 시각의 기준 이미지를 한 벌 새로 남깁니다.
            // 예전에는 아직 없는 방향만 채웠기 때문에, 이미 등록된 부품은 버튼을 눌러도
            // 아무것도 저장되지 않았습니다. 지금은 사용 중인 카메라 전부를 다시 담습니다.
            IList<ImageViewType> targetViewTypes = GetEnabledCameraViewTypes();
            if (targetViewTypes.Count == 0)
            {
                message = "사용 중인 카메라가 없어 기준 이미지를 저장할 수 없습니다.";
                return false;
            }

            int captureFailureCount;
            string captureFailureMessage;
            IList<CapturedImage> capturedImages = CaptureCurrentImagesForReference(partToSave, true, out captureFailureCount, out captureFailureMessage);
            if (capturedImages.Count == 0)
            {
                message = "현재 프레임을 기준 이미지로 등록하지 못했습니다. 카메라 연결 상태를 확인하세요." + captureFailureMessage;
                return false;
            }

            // 한 번의 저장에서 나온 이미지들이 같은 시각과 같은 벌 번호를 쓰도록
            // 여기서 한 번만 정합니다. 이미지마다 새로 읽으면 한 벌로 묶이지 않습니다.
            DateTime savedAt = DateTime.Now;
            int setNo = ReferenceImageFileNamePolicy.ResolveNextSetNo(partToSave.Images);

            int savedCount = 0;
            IList<string> failedViewNames = new List<string>();
            foreach (ImageViewType viewType in targetViewTypes)
            {
                CapturedImage capturedImage = FindCapturedImageByViewType(capturedImages, viewType);
                if (!IsCapturedImageFileReady(capturedImage))
                {
                    failedViewNames.Add(BuildImageViewDisplayName(viewType));
                    continue;
                }

                try
                {
                    PartImage savedImage = _referenceImageFileService.AddReferenceImage(
                        partToSave, capturedImage.FilePath, viewType, setNo, savedAt);

                    // 같은 방향의 예전 이미지를 지우지 않고 나란히 보관합니다.
                    partToSave.Images.Add(savedImage);
                    ApplySavedReferenceImageToSlot(savedImage);
                    savedCount++;
                }
                catch (IOException)
                {
                    failedViewNames.Add(BuildImageViewDisplayName(viewType));
                }
                catch (UnauthorizedAccessException)
                {
                    failedViewNames.Add(BuildImageViewDisplayName(viewType));
                }
            }

            if (savedCount == 0)
            {
                message = "현재 프레임을 기준 이미지로 저장하지 못했습니다. 실패 위치: " +
                          string.Join(", ", failedViewNames) + captureFailureMessage;
                return false;
            }

            string saveMessage = _partDataStore.SavePart(partToSave);
            if (saveMessage != PartCatalogService.SaveSuccessMessage)
            {
                message = saveMessage;
                return false;
            }

            RefreshPartCollectionsFromDataStore();
            RefreshInspectionPartSelection(partToSave.PartNo);
            message = "현재 프레임 " + savedCount.ToString() + "장을 기준 이미지로 등록하고 DB에 반영했습니다.";
            if (failedViewNames.Count > 0 || captureFailureCount > 0)
            {
                message = message + " 저장 실패 위치: " + string.Join(", ", failedViewNames) + captureFailureMessage;
            }

            return true;
        }

        private IList<ImageViewType> BuildMissingReferenceViewTypes(Part part)
        {
            IList<ImageViewType> missingViewTypes = new List<ImageViewType>();
            IList<ImageViewType> requiredViewTypes = GetEnabledCameraViewTypes();
            foreach (ImageViewType viewType in requiredViewTypes)
            {
                PartImage image = FindFirstImageByViewType(part.Images, viewType);
                if (!IsReferenceImageFileReady(image) && !ContainsImageViewType(missingViewTypes, viewType))
                {
                    missingViewTypes.Add(viewType);
                }
            }

            return missingViewTypes;
        }

        private void ReplacePartImage(IList<PartImage> images, PartImage savedImage)
        {
            if (images == null || savedImage == null)
            {
                return;
            }

            for (int index = images.Count - 1; index >= 0; index--)
            {
                if (images[index] != null && images[index].ViewType == savedImage.ViewType)
                {
                    images.RemoveAt(index);
                }
            }

            images.Add(savedImage);
        }

        private Part ClonePart(Part source)
        {
            Part target = new Part();
            if (source == null)
            {
                return target;
            }

            target.PartNo = source.PartNo;
            target.PartName = source.PartName;
            target.CategoryCode = source.CategoryCode;
            target.CategoryDescription = source.CategoryDescription;
            target.Memo = source.Memo;
            target.CreatedAt = source.CreatedAt;
            target.UpdatedAt = source.UpdatedAt;

            foreach (PartImage sourceImage in source.Images)
            {
                if (sourceImage == null)
                {
                    continue;
                }

                PartImage targetImage = new PartImage();
                targetImage.Id = sourceImage.Id;
                targetImage.PartNo = sourceImage.PartNo;
                targetImage.ViewType = sourceImage.ViewType;
                targetImage.FilePath = sourceImage.FilePath;
                targetImage.CapturedAt = sourceImage.CapturedAt;
                targetImage.IsTemporary = sourceImage.IsTemporary;

                // 벌 번호를 빠뜨리면 복제본의 번호가 모두 0이 되어,
                // 다음 저장이 늘 1번 벌로 잡히고 벌 구분이 무너집니다.
                targetImage.SetNo = sourceImage.SetNo;
                target.Images.Add(targetImage);
            }

            foreach (MeasurementRegion sourceRegion in source.MeasurementRegions)
            {
                if (sourceRegion == null)
                {
                    continue;
                }

                MeasurementRegion targetRegion = new MeasurementRegion();
                targetRegion.Id = sourceRegion.Id;
                targetRegion.PartNo = sourceRegion.PartNo;
                targetRegion.IndexNo = sourceRegion.IndexNo;
                targetRegion.Name = sourceRegion.Name;
                targetRegion.ItemType = sourceRegion.ItemType;
                targetRegion.ViewType = sourceRegion.ViewType;
                targetRegion.Coordinates = sourceRegion.Coordinates;
                targetRegion.X1 = sourceRegion.X1;
                targetRegion.Y1 = sourceRegion.Y1;
                targetRegion.X2 = sourceRegion.X2;
                targetRegion.Y2 = sourceRegion.Y2;
                targetRegion.LineColor = sourceRegion.LineColor;
                targetRegion.NominalValue = sourceRegion.NominalValue;
                targetRegion.ToleranceMin = sourceRegion.ToleranceMin;
                targetRegion.ToleranceMax = sourceRegion.ToleranceMax;
                targetRegion.Unit = sourceRegion.Unit;
                target.MeasurementRegions.Add(targetRegion);
            }

            return target;
        }

        private IList<ImageViewType> GetEnabledCameraViewTypes()
        {
            IList<ImageViewType> viewTypes = new List<ImageViewType>();
            IList<CameraChannelConfig> channels;
            try
            {
                channels = _cameraService.GetChannelConfigurations();
            }
            catch
            {
                return viewTypes;
            }

            foreach (CameraChannelConfig channel in channels)
            {
                if (channel == null || !channel.IsEnabled)
                {
                    continue;
                }

                if (!ContainsImageViewType(viewTypes, channel.ViewType))
                {
                    viewTypes.Add(channel.ViewType);
                }
            }

            return viewTypes;
        }

        private bool HasAnyReferenceImageFile(Part part)
        {
            if (part == null || part.Images == null)
            {
                return false;
            }

            foreach (PartImage image in part.Images)
            {
                if (IsReferenceImageFileReady(image))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsReferenceImageFileReady(PartImage image)
        {
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return false;
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
            return pathSettings.ImageFileExists(image.FilePath);
        }

        private string BuildImageViewDisplayName(ImageViewType viewType)
        {
            if (viewType == ImageViewType.Top)
            {
                return "Top";
            }

            if (viewType == ImageViewType.Front)
            {
                return "Front";
            }

            if (viewType == ImageViewType.Back)
            {
                return "Back";
            }

            if (viewType == ImageViewType.Left)
            {
                return "Left";
            }

            if (viewType == ImageViewType.Right)
            {
                return "Right";
            }

            if (viewType == ImageViewType.Thickness)
            {
                return "Thickness";
            }

            return viewType.ToString();
        }

        private void ExecuteRunInspection(object parameter)
        {
            if (_isInspectionRunning)
            {
                return;
            }

            Part inspectionPart = ResolveInspectionPart(InputCode);
            bool continueWithoutFullReferenceImages = false;
            string continuedReferenceImageMessage = string.Empty;
            string registeredReferenceImageMessage = string.Empty;
            string referenceImageMessage;
            if (!HasRequiredReferenceImages(inspectionPart, out referenceImageMessage))
            {
                EventRows.Clear();
                if (inspectionPart == null)
                {
                    StatusText = "부품 기준정보 없음";
                    ResultText = "검사 불가 - 부품 기준정보 없음";
                    AddInspectionEvent(EventSeverity.Warning, referenceImageMessage);
                    PrepareRegistrationForMissingPartCode(InputCode);
                    _messageDialogService.ShowWarning("부품 기준정보 등록 필요", referenceImageMessage);
                    return;
                }

                StatusText = "기준 이미지 확인 필요";
                ResultText = "검사 대기 - 기준 이미지 일부 없음";
                AddInspectionEvent(EventSeverity.Warning, referenceImageMessage);

                bool continueInspection = _messageDialogService.ShowConfirmation(
                    "기준 이미지 확인",
                    referenceImageMessage + Environment.NewLine + Environment.NewLine +
                    "현재 카메라 프레임을 기준 이미지로 등록한 뒤 검사를 진행하시겠습니까?" + Environment.NewLine +
                    "아니오를 선택하면 기준 이미지 누락 상태로 검사를 계속 시도합니다.");
                if (continueInspection)
                {
                    string registrationMessage;
                    if (!TryRegisterCurrentFramesAsReferenceImagesForInspection(inspectionPart, out registrationMessage))
                    {
                        AddInspectionEvent(EventSeverity.Error, registrationMessage);
                        _messageDialogService.ShowWarning("기준 이미지 자동 등록 실패", registrationMessage);
                        return;
                    }

                    registeredReferenceImageMessage = registrationMessage;
                    inspectionPart = ResolveInspectionPart(InputCode);
                    if (!HasRequiredReferenceImages(inspectionPart, out referenceImageMessage))
                    {
                        continueWithoutFullReferenceImages = true;
                        continuedReferenceImageMessage = referenceImageMessage;
                    }
                }
                else
                {
                    continueWithoutFullReferenceImages = true;
                    continuedReferenceImageMessage = referenceImageMessage;
                }
            }

            // 기준값이 비어 있는 측정부가 있으면 검사 전에 알립니다.
            // 알리기만 하고 검사는 그대로 진행합니다.
            WarnIfMeasurementValuesMissing(inspectionPart);

            BeginRunInspection(InputCode);
            if (!string.IsNullOrWhiteSpace(registeredReferenceImageMessage))
            {
                AddInspectionEvent(EventSeverity.Info, registeredReferenceImageMessage);
            }

            if (continueWithoutFullReferenceImages)
            {
                AddInspectionEvent(EventSeverity.Warning, continuedReferenceImageMessage);
                AddInspectionEvent(EventSeverity.Warning, "기준 이미지 누락 상태에서 사용자가 검사를 계속 진행했습니다.");
            }
        }

        /// <summary>
        /// 기준값이 비어 있는 측정부가 있으면 검사 전에 알립니다.
        ///
        /// <para>
        /// 측정부가 <b>아예 없는 부품은 정상</b>입니다. 측정할 것이 없다는 뜻이므로 아무것도 알리지 않습니다.
        /// 문제가 되는 것은 <b>측정부를 넣어 놓고 기준값을 비워 둔</b> 경우입니다.
        /// </para>
        ///
        /// <para>
        /// 좌표가 없는 측정부도 정상입니다. 좌표가 없으면 AI가 스스로 값을 견주어 합불을 냅니다.
        /// 하지만 기준값이 비어 있으면 견줄 대상이 없어 AI가 제대로 판단하지 못하고,
        /// 대개 불합격으로 나옵니다.
        /// </para>
        ///
        /// <para>
        /// 알리기만 하고 검사는 그대로 진행합니다. 결과가 왜 그렇게 나왔는지 알 수 있으면
        /// 충분하고, 여기서 막으면 확인용 검사조차 못 하게 되기 때문입니다.
        /// </para>
        /// </summary>
        private void WarnIfMeasurementValuesMissing(Part inspectionPart)
        {
            if (inspectionPart == null || inspectionPart.MeasurementRegions == null)
            {
                return;
            }

            IList<string> missingNames = new List<string>();
            foreach (MeasurementRegion region in inspectionPart.MeasurementRegions)
            {
                if (region == null || region.HasMeasurementValue)
                {
                    continue;
                }

                string name = string.IsNullOrWhiteSpace(region.Name)
                    ? "측정부 " + region.IndexNo.ToString(CultureInfo.InvariantCulture)
                    : region.Name;
                missingNames.Add(name);
            }

            if (missingNames.Count == 0)
            {
                return;
            }

            string message =
                "측정부 값이 저장되어 있지 않습니다. 부품등록에서 기준값과 허용오차를 확인하십시오." +
                Environment.NewLine + Environment.NewLine +
                "값이 없는 측정부: " + string.Join(", ", missingNames) +
                Environment.NewLine + Environment.NewLine +
                "이대로 검사하면 AI가 견줄 값이 없어 불합격으로 나올 수 있습니다.";

            AddInspectionEvent(EventSeverity.Warning, message);
            _messageDialogService.ShowWarning("측정부 값 확인 필요", message);
        }

        private void BeginRunInspection(string inputCode)
        {
            _isInspectionRunning = true;
            _isInspectionStillPinned = false;
            RaiseRunCommandState();

            // 검사가 도는 동안에는 자를 자리를 그대로 묶어 둡니다.
            // 그러지 않으면 SAM 이 프레임마다 다른 곳을 잡아 화면이 계속 들썩입니다.
            AI.Vision.IOInspector.Vision.Services.CallbackFrameCropStage.IsRegionLocked = true;

            StatusText = "검사중";
            ResultText = "검사 준비";
            EventRows.Clear();

            _livePreviewTimer.Stop();
            PrepareInspectionRunningSlots("검사 요청을 준비하고 있습니다.", "READY");

            Task<Inspection>.Factory.StartNew(
                    RunInspectionOnWorker,
                    inputCode,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default)
                .ContinueWith(OnRunInspectionCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// 검사 중에는 정지 이미지를 영상 위에 덮지 않고 기존 RTSP 스트리밍을 계속 표시합니다.
        /// 각 슬롯에는 현재 검사 단계만 표시하여 처리 중인지 오류인지 구분합니다.
        /// </summary>
        private void PrepareInspectionRunningSlots(string statusMessage, string resultText)
        {
            foreach (ImageSlotViewModel slot in ImageSlots)
            {
                // 검사에 쓸 사진을 이미 올려 두었으면 그대로 둡니다.
                if (!_isInspectionStillPinned)
                {
                    slot.IsCapturedStillVisible = false;
                }

                slot.StatusText = statusMessage;
                slot.ResultText = resultText;
                slot.ResultBrush = "#0A86D8";

                // 새 검사가 시작되면 판정 표시를 일단 되살립니다.
                // 감출지는 결과가 나올 때 그 품목 기준으로 다시 정합니다.
                slot.IsJudgmentVisible = true;
            }
        }

        private void OnInspectionProgressChanged(object sender, InspectionProgressEventArgs e)
        {
            if (_isDisposed || e == null)
            {
                return;
            }

            if (_uiDispatcher.CheckAccess())
            {
                ApplyInspectionProgress(e);
                return;
            }

            _uiDispatcher.BeginInvoke(new Action<InspectionProgressEventArgs>(ApplyInspectionProgress), e);
        }

        private void ApplyInspectionProgress(InspectionProgressEventArgs progress)
        {
            if (_isDisposed || !_isInspectionRunning || progress == null)
            {
                return;
            }

            string resultText = BuildInspectionProgressText(progress.Status);
            StatusText = progress.Message;
            ResultText = resultText;

            if (progress.Status == InspectionStatus.Completed || progress.Status == InspectionStatus.Error)
            {
                return;
            }

            PinCapturedImages(progress.CapturedImages);
            PrepareInspectionRunningSlots(progress.Message, resultText);
        }

        /// <summary>
        /// 검사에 쓸 사진을 화면에 붙박아 둡니다. 판정이 끝날 때까지 여기서 움직이지 않습니다.
        ///
        /// <para>
        /// 파일은 잘라 내지 않은 원본이지만 칸에는 잘라서 보여 줍니다.
        /// 경로만 넣으면 칸이 알아서 그 카메라의 크롭 자리로 잘라 그립니다.
        /// </para>
        /// </summary>
        private void PinCapturedImages(IList<CapturedImage> capturedImages)
        {
            if (capturedImages == null || capturedImages.Count == 0)
            {
                return;
            }

            foreach (CapturedImage image in capturedImages)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                int index = GetImageViewTypeSortOrder(image.ViewType);
                if (index < 0 || index >= ImageSlots.Count)
                {
                    continue;
                }

                Part part = SelectedPart == null ? null : SelectedPart.Part;
                ImageSlots[index].LiveImagePath = ResolveSlotDisplayImagePath(part, image);
                ImageSlots[index].IsCapturedStillVisible = true;
            }

            _isInspectionStillPinned = true;
        }

        private static string BuildInspectionProgressText(InspectionStatus status)
        {
            switch (status)
            {
                case InspectionStatus.PartLookup:
                    return "LOOKUP";
                case InspectionStatus.Capturing:
                    return "CAPTURE";
                case InspectionStatus.Inferencing:
                    return "INFERENCE";
                case InspectionStatus.Measuring:
                    return "MEASUREMENT";
                case InspectionStatus.Judging:
                    return "JUDGING";
                case InspectionStatus.Saving:
                    return "SAVING";
                case InspectionStatus.Completed:
                    return "COMPLETED";
                case InspectionStatus.Error:
                    return "ERROR";
                default:
                    return "READY";
            }
        }

        private void ExecuteResetInspectionScreen(object parameter)
        {
            if (_isInspectionRunning)
            {
                return;
            }

            EventRows.Clear();
            StatusText = "대기";
            ResultText = "검사 대기";

            if (SelectedPart != null)
            {
                LoadReferenceImages(SelectedPart.Part);
                LoadInspectionMeasurementRegions(SelectedPart.Part);
            }
            else
            {
                InspectionMeasurements.Clear();
                InitializeImageSlots();
            }

            ResumeLivePreviewTimerIfNeeded();
            RefreshCameraStatuses(false);
        }

        /// <summary>
        /// 검사 화면의 기준이미지를 두 번 클릭하면 선택된 부품의 6방향 기준이미지를 한 창에서 확인합니다.
        /// 팝업 창은 서비스에서 단일 인스턴스로 관리합니다.
        /// </summary>
        private void ExecuteShowReferenceImagePopup(object parameter)
        {
            ImageSlotViewModel slot = parameter as ImageSlotViewModel;
            if (slot == null || SelectedPart == null || SelectedPart.Part == null)
            {
                return;
            }

            int slotIndex = ImageSlots.IndexOf(slot);
            ImageViewType[] viewOrder = GetReferenceImageViewOrder();
            if (slotIndex < 0 || slotIndex >= viewOrder.Length)
            {
                return;
            }

            _referenceImagePopupService.Show(SelectedPart.Part, viewOrder[slotIndex]);
        }

        /// <summary>
        /// 조회 화면에서 고른 부품의 기준 이미지를 확대 창으로 엽니다.
        /// </summary>
        private void ExecuteShowDbDetailImagePopup(object parameter)
        {
            ReferenceImagePreviewViewModel preview = parameter as ReferenceImagePreviewViewModel;
            if (preview == null || _dbDetailPart == null)
            {
                return;
            }

            // 좌표 그림 칸은 카메라가 정해져 있지 않습니다. 그 칸에서는 열지 않습니다.
            if (preview.ViewType == ImageViewType.Unclassified)
            {
                return;
            }

            _referenceImagePopupService.Show(_dbDetailPart, preview.ViewType);
        }

        /// <summary>
        /// 유사도 검색은 일반 검사와 목적과 반환 형식이 다릅니다.
        /// 현재 제공받은 VLAD_SDK.dll에는 전용 Search export가 없어, UI가 임의 추론으로 오판하지 않도록 실행을 차단합니다.
        /// </summary>
        private void ExecuteCheckReferenceImageSimilarity(object parameter)
        {
            if (_isSimilaritySearchRunning)
            {
                SimilaritySearchMessage = "유사도 검색이 이미 실행 중입니다.";
                return;
            }

            SimilarityCandidates.Clear();
            SetRegistrationSimilarityStatus("검색 준비 중", "이미지 없음");

            if (_referenceImageSimilarityService == null)
            {
                SimilaritySearchMessage = "현재 AI 서비스는 기준이미지 유사도 검색을 지원하지 않습니다.";
                SetRegistrationSimilarityStatus("검색 지원 안 함", "이미지 없음");
                _messageDialogService.ShowWarning("유사도 체크", SimilaritySearchMessage);
                return;
            }

            ReferenceImageSimilarityRequest request;
            string requestErrorMessage;
            if (!TryBuildReferenceImageSimilarityRequest(out request, out requestErrorMessage))
            {
                SimilaritySearchMessage = requestErrorMessage;
                SetRegistrationSimilarityStatus("검색할 이미지 없음", "이미지 없음");
                _messageDialogService.ShowWarning("유사도 체크", SimilaritySearchMessage);
                return;
            }

            if (request.SourceImages.Count == 0)
            {
                SimilaritySearchMessage = "이미지 저장으로 비교할 등록 기준이미지를 준비하세요.";
                SetRegistrationSimilarityStatus("검색할 이미지 없음", "이미지 없음");
                return;
            }

            _isSimilaritySearchRunning = true;
            SimilaritySearchMessage = "VLAD 기준이미지 유사도 검색을 실행 중입니다.";
            SetRegistrationSimilarityStatus("검색 중", "이미지 없음");

            Task<ReferenceImageSimilarityResult> task =
                Task<ReferenceImageSimilarityResult>.Factory.StartNew(
                    SearchReferenceImagesOnWorker,
                    request);
            task.ContinueWith(
                OnReferenceImageSimilaritySearchCompleted,
                TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>
        /// 화면에 등록된 이미지 파일을 VLAD_Search_Mat 입력 모델로 변환합니다.
        /// 이미지 1장마다 1회의 Mat 검색 호출이 발생하며, 최대 6개 방향이 순서대로 처리됩니다.
        /// </summary>
        private bool TryBuildReferenceImageSimilarityRequest(
            out ReferenceImageSimilarityRequest request,
            out string errorMessage)
        {
            request = null;
            errorMessage = string.Empty;

            request = new ReferenceImageSimilarityRequest();
            request.ScoreThreshold = Convert.ToDecimal(SinglePartSimilarityThreshold, CultureInfo.InvariantCulture);

            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                if (imageViewModel == null || imageViewModel.Image == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(imageViewModel.Image.FilePath) ||
                    !File.Exists(imageViewModel.Image.FilePath))
                {
                    continue;
                }

                CapturedImage sourceImage = new CapturedImage();
                sourceImage.ViewType = imageViewModel.Image.ViewType;
                sourceImage.DisplayName = imageViewModel.Image.ViewType.ToString();
                sourceImage.FilePath = imageViewModel.Image.FilePath;
                sourceImage.CapturedAt = imageViewModel.Image.CapturedAt;
                request.SourceImages.Add(sourceImage);
            }

            if (request.SourceImages.Count == 0)
            {
                errorMessage = "유사도 체크에 사용할 등록 기준 이미지가 없습니다.";
                return false;
            }

            return true;
        }

        private ReferenceImageSimilarityResult SearchReferenceImagesOnWorker(object state)
        {
            ReferenceImageSimilarityRequest request = state as ReferenceImageSimilarityRequest;
            return _referenceImageSimilarityService.SearchReferenceImages(request);
        }

        /// <summary>
        /// Vision 작업 스레드가 반환한 유사도 검색 결과만 UI 스레드에서 목록에 반영합니다.
        /// </summary>
        private void OnReferenceImageSimilaritySearchCompleted(Task<ReferenceImageSimilarityResult> task)
        {
            _isSimilaritySearchRunning = false;

            if (task.IsFaulted)
            {
                string errorMessage = task.Exception == null
                    ? "알 수 없는 유사도 검색 오류"
                    : task.Exception.GetBaseException().Message;
                SimilaritySearchMessage = "유사도 검색 실패: " + errorMessage;
                SetRegistrationSimilarityStatus("검색 실패", "이미지 없음");
                _messageDialogService.ShowWarning("유사도 체크", SimilaritySearchMessage);
                return;
            }

            if (task.IsCanceled || task.Result == null)
            {
                SimilaritySearchMessage = "유사도 검색 결과를 받지 못했습니다.";
                SetRegistrationSimilarityStatus("결과 없음", "이미지 없음");
                _messageDialogService.ShowWarning("유사도 체크", SimilaritySearchMessage);
                return;
            }

            ReferenceImageSimilarityResult result = task.Result;
            if (!result.IsSuccess)
            {
                SimilaritySearchMessage = result.Message;
                SetRegistrationSimilarityStatus("검색 실패", "이미지 없음");
                _messageDialogService.ShowWarning("유사도 체크", SimilaritySearchMessage);
                return;
            }

            ApplySimilarityCandidatesToRegistrationPreviews(result.Candidates);
            SimilaritySearchMessage = result.Message;
        }

        /// <summary>
        /// AI가 기준 Score를 적용해 반환한 후보를 View별로 최대 3개 표시합니다.
        /// 순위와 Score는 AI 응답을 그대로 사용하며 UI에서 다시 판정하지 않습니다.
        /// </summary>
        private void ApplySimilarityCandidatesToRegistrationPreviews(
            IList<ReferenceImageSimilarityCandidate> candidates)
        {
            SimilarityCandidates.Clear();
            foreach (ReferenceImagePreviewViewModel preview in RegistrationImagePreviews)
            {
                if (!preview.HasImage)
                {
                    preview.ClearSimilarityCandidates("이미지 없음");
                    continue;
                }

                List<SimilarityCandidateViewModel> topCandidates =
                    BuildTopSimilarityCandidates(preview, candidates);
                if (topCandidates.Count == 0)
                {
                    preview.ClearSimilarityCandidates(
                        "AI 기준 이상 후보 없음");
                    continue;
                }

                foreach (SimilarityCandidateViewModel candidateViewModel in topCandidates)
                {
                    SimilarityCandidates.Add(candidateViewModel);
                }

                preview.SetSimilarityCandidates(topCandidates, string.Empty);
            }
        }

        private List<SimilarityCandidateViewModel> BuildTopSimilarityCandidates(
            ReferenceImagePreviewViewModel preview,
            IList<ReferenceImageSimilarityCandidate> candidates)
        {
            List<SimilarityCandidateViewModel> topCandidates =
                new List<SimilarityCandidateViewModel>();
            if (candidates == null)
            {
                return topCandidates;
            }

            foreach (ReferenceImageSimilarityCandidate candidate in candidates)
            {
                if (candidate == null ||
                    !IsSameSimilarityView(preview, candidate.ViewName))
                {
                    continue;
                }

                SimilarityCandidateViewModel viewModel = new SimilarityCandidateViewModel();
                viewModel.Rank = candidate.Rank;
                viewModel.ViewName = candidate.ViewName;
                viewModel.PartNo = candidate.PartNo;
                viewModel.PartName = ResolveSimilarityCandidatePartName(candidate);
                viewModel.MatchStatusText = "존재";
                viewModel.Score = candidate.Score;

                topCandidates.Add(viewModel);
                if (topCandidates.Count >= 3)
                {
                    break;
                }
            }

            return topCandidates;
        }

        /// <summary>
        /// 신규 유사도 API는 전송량을 줄이기 위해 후보 품명 없이 품번만 반환합니다.
        /// 화면에 표시할 품명은 현재 DataStore에서 품번으로 조회합니다.
        /// </summary>
        private string ResolveSimilarityCandidatePartName(ReferenceImageSimilarityCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(candidate.PartName))
            {
                return candidate.PartName;
            }

            Part part = _partDataStore.GetPart(candidate.PartNo);
            return part == null ? string.Empty : part.PartName;
        }

        private bool IsSameSimilarityView(
            ReferenceImagePreviewViewModel preview,
            string candidateViewName)
        {
            if (preview == null || string.IsNullOrWhiteSpace(candidateViewName))
            {
                return false;
            }

            return string.Equals(
                       preview.ViewType.ToString(),
                       candidateViewName.Trim(),
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       preview.Title,
                       candidateViewName.Trim(),
                       StringComparison.OrdinalIgnoreCase);
        }

        private void SetRegistrationSimilarityStatus(
            string imageStatusText,
            string missingImageStatusText)
        {
            foreach (ReferenceImagePreviewViewModel preview in RegistrationImagePreviews)
            {
                preview.ClearSimilarityCandidates(
                    preview.HasImage ? imageStatusText : missingImageStatusText);
            }
        }

        private void ExecuteClearReferenceImageSimilarity(object parameter)
        {
            SimilarityCandidates.Clear();
            SetRegistrationSimilarityStatus("검색 전", "이미지 없음");
            SimilaritySearchMessage = "유사도 검색 결과를 초기화했습니다.";
        }

        private Inspection RunInspectionOnWorker(object state)
        {
            string inputCode = state as string;
            return _inspectionWorkflowService.RunInspection(inputCode);
        }

        private void OnRunInspectionCompleted(Task<Inspection> task)
        {
            try
            {
                _isInspectionRunning = false;
                RaiseRunCommandState();

                // 검사가 끝났으니 자를 자리를 다시 찾을 수 있게 풉니다.
                AI.Vision.IOInspector.Vision.Services.CallbackFrameCropStage.IsRegionLocked = false;

                if (task.IsFaulted)
                {
                    string message = task.Exception == null ? "알 수 없는 오류" : task.Exception.GetBaseException().Message;
                    StatusText = "오류";
                    ResultText = "Error - " + message;
                    AddInspectionEvent(EventSeverity.Error, "검사 실행 중 시스템 오류가 발생했습니다. " + message);
                    return;
                }

                if (task.IsCanceled)
                {
                    StatusText = "검사 취소";
                    ResultText = "Canceled";
                    AddInspectionEvent(EventSeverity.Warning, "검사 작업이 취소되었습니다.");
                    return;
                }

                ApplyInspectionResult(task.Result);
            }
            catch (Exception ex)
            {
                StatusText = "오류";
                ResultText = "Error - 검사 결과 표시 실패";
                AddInspectionEvent(EventSeverity.Error, "검사 결과를 화면에 표시하는 중 오류가 발생했습니다. " + ex.Message);
            }
            finally
            {
                ResumeLivePreviewTimerIfNeeded();
            }
        }

        /// <summary>
        /// 그 카메라의 Pass/Fail 판정을 화면에 보일지 정합니다.
        ///
        /// <para>
        /// 현장 작업자들이 좌표를 지정하지 않은 Thickness 에서도 Pass/Fail 이 떠서 무엇을
        /// 판정한 것인지 헷갈린다고 했습니다. 판정은 AI 가 그대로 내리고 이력에도 남습니다.
        /// Thickness 에 측정부 좌표가 하나도 없을 때만 그 칸의 표시를 감춥니다.
        /// 다른 카메라는 언제나 보입니다.
        /// </para>
        /// </summary>
        private static bool ShouldShowJudgment(Part part, ImageViewType viewType)
        {
            if (viewType != ImageViewType.Thickness)
            {
                return true;
            }

            if (part == null || part.MeasurementRegions == null)
            {
                return false;
            }

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                // 측정부 행이 있다는 것과 위치를 지정했다는 것은 다릅니다.
                // 기준값만 넣고 위치표시를 하지 않은 측정부는 좌표가 비어 있는데,
                // 행 존재만 보고 판정을 보여 주면 현장에서 다시 헷갈립니다.
                // 네 좌표가 모두 입력된 측정부가 하나라도 있을 때만 판정을 보입니다.
                if (region != null &&
                    region.ViewType == ImageViewType.Thickness &&
                    region.X1.HasValue && region.Y1.HasValue &&
                    region.X2.HasValue && region.Y2.HasValue)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 감춘 판정의 방향 줄을 종합 결과 문구에서 뺍니다. 저장 문구는 건드리지 않습니다.
        /// </summary>
        private static string RemoveHiddenJudgmentLines(string message, Part part)
        {
            if (string.IsNullOrEmpty(message) || ShouldShowJudgment(part, ImageViewType.Thickness))
            {
                return message;
            }

            string[] lines = message.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            foreach (string line in lines)
            {
                if (line.TrimStart().StartsWith("[Thickness]", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(Environment.NewLine);
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        private void ApplyInspectionResult(Inspection inspection)
        {
            if (inspection == null)
            {
                StatusText = "오류";
                ResultText = "Error - 검사 결과 없음";
                AddInspectionEvent(EventSeverity.Error, "검사 결과가 없습니다.");
                return;
            }

            // 좌표 없는 Thickness 는 상단 종합 문구에서도 그 방향 줄을 지웁니다.
            // 저장되는 결과와 이력은 그대로 두고, 보여 주는 글에서만 뺍니다.
            ResultText = BuildSlotResultText(inspection.Result) + " - " +
                RemoveHiddenJudgmentLines(inspection.ResultMessage, SelectedPart == null ? null : SelectedPart.Part);
            StatusText = inspection.Result == InspectionResult.Error ? "오류" : "검사 완료";

            ApplyInspectionPartContext(inspection);
            LoadCapturedImages(inspection);
            LoadInspectionMeasurements(inspection);
            LoadEvents(inspection);
            PrepareRegistrationWhenPartIsMissing(inspection);
            RefreshHistory();
            RefreshStatistics();
            RefreshCameraStatuses(false);
        }

        private void ResumeLivePreviewTimerIfNeeded()
        {
            if (IsLivePreviewAutoRefreshEnabled)
            {
                _livePreviewTimer.Start();
            }
        }

        /// <summary>
        /// 검사 로직을 실행하지 않고 현재 사용 설정된 카메라의 화면만 메인 화면에 갱신합니다.
        /// Top 한 대만 연결한 초기 셋업 단계에서 RTSP 연결과 화면 표시를 분리해서 확인하기 위한 기능입니다.
        /// </summary>
        private void ExecuteRefreshLivePreview(object parameter)
        {
            BeginLivePreviewRefresh();
        }

        private void ApplyLivePreviewAutoRefreshState()
        {
            if (IsLivePreviewAutoRefreshEnabled)
            {
                BeginLivePreviewRefresh();
                _livePreviewTimer.Start();
                return;
            }

            _livePreviewTimer.Stop();
        }

        private void OnLivePreviewTimerTick(object sender, EventArgs e)
        {
            BeginLivePreviewRefresh();
        }

        private void BeginLivePreviewRefresh()
        {
            if (_isLivePreviewRefreshRunning)
            {
                return;
            }

            _isLivePreviewRefreshRunning = true;
            StatusText = "카메라 화면 수신중";
            ResultText = "LIVE 수신중";
            EventRows.Clear();
            PrepareLivePreviewSlots();

            Task.Factory.StartNew(CaptureLivePreviewFrames)
                .ContinueWith(OnLivePreviewRefreshCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void PrepareLivePreviewSlots()
        {
            foreach (ImageSlotViewModel slot in ImageSlots)
            {
                slot.StatusText = "프레임 수신 대기";
                slot.ResultText = "LIVE";
                slot.ResultBrush = "#0A86D8";
                slot.IsCapturedStillVisible = false;
            }
        }

        private LivePreviewRefreshResult CaptureLivePreviewFrames()
        {
            LivePreviewRefreshResult result = new LivePreviewRefreshResult();
            IList<CameraChannelConfig> channels;
            try
            {
                channels = _cameraService.GetChannelConfigurations();
            }
            catch (Exception ex)
            {
                result.ConfigurationErrorMessage = "카메라 설정을 읽을 수 없습니다. " + TrimLivePreviewMessage(ex.Message);
                return result;
            }

            Part previewPart = BuildLivePreviewPart();

            // 채널을 하나씩 순서대로 Capture()하면 채널 수만큼 타임아웃이 누적되어 화면이 순차적으로 채워지는 것처럼 보입니다.
            // CaptureAll()은 Direct/File 채널을 워커에 동시 요청하고 RTSP 채널은 한 번에 배치 조회하므로 이 문제를 피할 수 있습니다.
            IList<CapturedImage> capturedImages;
            try
            {
                capturedImages = _cameraService.CaptureAll(previewPart);
            }
            catch (Exception ex)
            {
                result.ConfigurationErrorMessage = "카메라 프레임을 수신할 수 없습니다. " + TrimLivePreviewMessage(ex.Message);
                return result;
            }

            IDictionary<ImageViewType, CapturedImage> imagesByViewType = new Dictionary<ImageViewType, CapturedImage>();
            foreach (CapturedImage capturedImage in capturedImages)
            {
                imagesByViewType[capturedImage.ViewType] = capturedImage;
            }

            IDictionary<ImageViewType, CameraChannelStatus> statusesByViewType = new Dictionary<ImageViewType, CameraChannelStatus>();
            foreach (CameraChannelStatus status in _cameraService.GetChannelStatuses())
            {
                statusesByViewType[status.ViewType] = status;
            }

            foreach (CameraChannelConfig channel in channels)
            {
                if (!channel.IsEnabled)
                {
                    continue;
                }

                result.EnabledChannelCount++;
                LivePreviewFrameResult frameResult = new LivePreviewFrameResult();
                frameResult.ViewType = channel.ViewType;
                frameResult.DisplayName = channel.DisplayName;

                CapturedImage image;
                if (imagesByViewType.TryGetValue(channel.ViewType, out image))
                {
                    frameResult.IsSuccess = true;
                    frameResult.FilePath = image.FilePath;
                    frameResult.Message = "프레임 수신 완료";
                    result.SuccessCount++;
                }
                else
                {
                    CameraChannelStatus status;
                    string failureMessage = "프레임을 수신하지 못했습니다.";
                    if (statusesByViewType.TryGetValue(channel.ViewType, out status) && !string.IsNullOrWhiteSpace(status.Message))
                    {
                        failureMessage = status.Message;
                    }

                    frameResult.IsSuccess = false;
                    frameResult.Message = TrimLivePreviewMessage(failureMessage);
                    result.FailureCount++;
                }

                result.Frames.Add(frameResult);
            }

            return result;
        }

        private void OnLivePreviewRefreshCompleted(Task<LivePreviewRefreshResult> task)
        {
            _isLivePreviewRefreshRunning = false;

            if (task.IsFaulted)
            {
                StatusText = "카메라 수신 오류";
                ResultText = "ERROR";
                AddLivePreviewEvent(EventSeverity.Error, "카메라 수신 처리 중 오류가 발생했습니다. " + TrimLivePreviewMessage(task.Exception == null ? string.Empty : task.Exception.Message));
                RefreshCameraStatuses(false);
                return;
            }

            ApplyLivePreviewRefreshResult(task.Result);
            RefreshCameraStatuses(false);
        }

        private void ApplyLivePreviewRefreshResult(LivePreviewRefreshResult result)
        {
            if (result == null)
            {
                StatusText = "카메라 수신 오류";
                ResultText = "ERROR";
                AddLivePreviewEvent(EventSeverity.Error, "카메라 수신 결과가 없습니다.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.ConfigurationErrorMessage))
            {
                StatusText = "카메라 설정 오류";
                ResultText = "ERROR";
                AddLivePreviewEvent(EventSeverity.Error, result.ConfigurationErrorMessage);
                return;
            }

            foreach (LivePreviewFrameResult frameResult in result.Frames)
            {
                int imageSlotIndex = GetImageViewTypeSortOrder(frameResult.ViewType);
                if (imageSlotIndex >= ImageSlots.Count)
                {
                    continue;
                }

                ImageSlotViewModel slot = ImageSlots[imageSlotIndex];
                if (frameResult.IsSuccess)
                {
                    slot.LiveImagePath = frameResult.FilePath;
                    slot.IsCapturedStillVisible = true;
                    slot.StatusText = "카메라 화면 갱신";
                    slot.ResultText = "LIVE";
                    slot.ResultBrush = "#128A45";
                    AddLivePreviewEvent(EventSeverity.Info, frameResult.DisplayName + " 프레임 수신 완료");
                }
                else
                {
                    ApplyLivePreviewFailure(slot, frameResult.DisplayName, frameResult.Message);
                }
            }

            if (result.SuccessCount > 0)
            {
                StatusText = "카메라 화면 갱신 완료";
                ResultText = "LIVE " + result.SuccessCount.ToString() + "개 수신";
            }
            else if (result.FailureCount > 0)
            {
                StatusText = "카메라 미연결";
                ResultText = "수신 실패";
            }
            else if (result.EnabledChannelCount == 0)
            {
                StatusText = "사용 카메라 없음";
                ResultText = "READY";
                AddLivePreviewEvent(EventSeverity.Warning, "사용 설정된 카메라 채널이 없습니다.");
            }
            else
            {
                StatusText = "카메라 수신 대기";
                ResultText = "READY";
            }
        }

        private void ApplyInspectionPartContext(Inspection inspection)
        {
            if (inspection == null || string.IsNullOrWhiteSpace(inspection.PartNo))
            {
                return;
            }

            foreach (PartViewModel partViewModel in Parts)
            {
                if (string.Equals(partViewModel.PartNo, inspection.PartNo, StringComparison.OrdinalIgnoreCase))
                {
                    if (SelectedPart != partViewModel)
                    {
                        SelectedPart = partViewModel;
                    }
                    else
                    {
                        LoadReferenceImages(partViewModel.Part);
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// 그 칸에 띄울 사진을 고릅니다.
        ///
        /// <para>
        /// 측정부가 있는 카메라는 선을 그은 좌표 그림을 씁니다. 어디를 재어 판정했는지
        /// 그 그림에만 나옵니다. 측정부가 없거나 좌표 그림을 찾지 못하면 찍은 사진을 그대로 씁니다.
        /// </para>
        /// </summary>
        private string ResolveSlotDisplayImagePath(Part part, CapturedImage image)
        {
            if (image == null)
            {
                return string.Empty;
            }

            string coordinateImagePath = ResolveSlotCoordinateImagePath(part, image.ViewType);
            if (!string.IsNullOrWhiteSpace(coordinateImagePath))
            {
                return coordinateImagePath;
            }

            return image.FilePath;
        }

        private void LoadCapturedImages(Inspection inspection)
        {
            ClearLiveImageSlots();
            Part part = SelectedPart == null ? null : SelectedPart.Part;
            foreach (CapturedImage image in inspection.Images)
            {
                int index = GetImageViewTypeSortOrder(image.ViewType);
                if (index >= ImageSlots.Count)
                {
                    continue;
                }

                // 측정부가 있는 카메라는 선을 그은 좌표 그림을 대신 보여 줍니다.
                //
                // 예전에는 Thickness 만 그렇게 했습니다. Top 에도 측정부를 둘 수 있게 되면서
                // Top 은 선 없는 사진이 나와, 무엇을 재고 판정했는지 알 수 없었습니다.
                string displayImagePath = ResolveSlotDisplayImagePath(part, image);

                // 좌표 없는 Thickness 는 캡처한 사진만 올리고 결과는 올리지 않습니다.
                //
                // 판정 배지·점수·치수가 있는 결과 화면 대신, 검사 중처럼 캡처 정지사진을
                // 스트리밍 화면 위에 올려 둡니다. 무엇을 찍었는지는 보이되 판정은 보이지
                // 않습니다. AI 판정과 결과 이미지 파일은 워크플로 쪽에서 이미 저장을
                // 마쳤으므로 기록에는 빠짐이 없습니다.
                if (!ShouldShowJudgment(part, image.ViewType))
                {
                    ImageSlots[index].IsJudgmentVisible = false;
                    ImageSlots[index].LiveImagePath = displayImagePath;
                    ImageSlots[index].IsCapturedStillVisible = true;
                    ImageSlots[index].StatusText = "측정부 미지정 - 판정은 파일에만 저장";
                    continue;
                }

                ImageSlots[index].StatusText = "촬영 완료";
                ImageSlots[index].LiveImagePath = displayImagePath;
                ImageSlots[index].IsCapturedStillVisible = true;
                ImageSlots[index].IsJudgmentVisible = true;

                AiViewInferenceResult viewResult = FindViewResult(inspection, image.ViewType);
                if (viewResult != null)
                {
                    ImageSlots[index].ResultText = BuildSlotResultText(viewResult);
                    ImageSlots[index].ResultBrush = BuildSlotResultBrush(viewResult);
                    ImageSlots[index].ScoreText = BuildSlotScoreText(viewResult, inspection.AiScoreThreshold);
                    ImageSlots[index].ScoreBrush = BuildSlotScoreBrush(viewResult);
                    ImageSlots[index].DimensionText = BuildSlotDimensionText(viewResult);
                }
                else
                {
                    // AI가 해당 방향의 결과를 반환하지 않은 예외 상황에서만 기존 전체 결과를 표시합니다.
                    ImageSlots[index].ResultText = BuildSlotResultText(inspection.Result);
                    ImageSlots[index].ResultBrush = BuildSlotResultBrush(inspection.Result);
                    ImageSlots[index].ScoreText = BuildSlotScoreText(inspection);
                    ImageSlots[index].ScoreBrush = BuildSlotScoreBrush(inspection);
                    ImageSlots[index].DimensionText = BuildSlotDimensionText(inspection);
                }
            }
        }

        private AiViewInferenceResult FindViewResult(Inspection inspection, ImageViewType viewType)
        {
            if (inspection == null || inspection.ViewResults == null)
            {
                return null;
            }

            AiViewInferenceResult viewResult;
            return inspection.ViewResults.TryGetValue(viewType, out viewResult) ? viewResult : null;
        }

        private void ClearLiveImageSlots()
        {
            foreach (ImageSlotViewModel slot in ImageSlots)
            {
                slot.LiveImagePath = string.Empty;
                slot.IsCapturedStillVisible = false;
                slot.ResultText = "READY";
                slot.ResultBrush = "#66788A";
                slot.ScoreText = "Score: -";
                slot.ScoreBrush = "#253747";
                slot.DimensionText = "W: -  H: -  D: -";
                // 영상이 이미 나오고 있으면 굳이 상태를 적지 않습니다.
                // "기준 이미지 준비" 같은 문구가 남아 있으면 화면과 어긋나 보입니다.
                slot.StatusText = string.IsNullOrWhiteSpace(slot.ReferenceImagePath)
                    ? "카메라 대기"
                    : string.Empty;
            }
        }

        private string BuildSlotResultText(InspectionResult result)
        {
            if (result == InspectionResult.Pass)
            {
                return "PASS";
            }

            if (result == InspectionResult.Fail)
            {
                return "FAIL";
            }

            if (result == InspectionResult.Error)
            {
                return "ERROR";
            }

            return "LIVE";
        }

        private string BuildSlotResultBrush(InspectionResult result)
        {
            if (result == InspectionResult.Pass)
            {
                return "#128A45";
            }

            if (result == InspectionResult.Fail)
            {
                return "#B73535";
            }

            if (result == InspectionResult.Error)
            {
                return "#A96F16";
            }

            return "#0A86D8";
        }

        private string BuildSlotResultText(AiViewInferenceResult viewResult)
        {
            return viewResult != null && viewResult.IsPass ? "PASS" : "FAIL";
        }

        private string BuildSlotResultBrush(AiViewInferenceResult viewResult)
        {
            return BuildSlotResultBrush(viewResult != null && viewResult.IsPass
                ? InspectionResult.Pass
                : InspectionResult.Fail);
        }

        private string BuildSlotScoreText(Inspection inspection)
        {
            if (inspection == null || !inspection.HasAiScore)
            {
                return "Score: -";
            }

            return "Score: " + inspection.AiScore.ToString("0.00", CultureInfo.InvariantCulture) +
                   " / " + inspection.AiScoreThreshold.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private string BuildSlotScoreBrush(Inspection inspection)
        {
            if (inspection == null || !inspection.HasAiScore)
            {
                return "#253747";
            }

            return BuildSlotResultBrush(inspection.Result);
        }

        private string BuildSlotScoreText(AiViewInferenceResult viewResult, decimal scoreThreshold)
        {
            if (viewResult == null || !viewResult.HasScore)
            {
                return "Score: -";
            }

            decimal score = NormalizeScoreForDisplay(viewResult.Score);
            return "Score: " + score.ToString("0.00", CultureInfo.InvariantCulture) +
                   " / " + scoreThreshold.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private string BuildSlotScoreBrush(AiViewInferenceResult viewResult)
        {
            return viewResult == null ? "#253747" : BuildSlotResultBrush(viewResult);
        }

        private string BuildSlotDimensionText(Inspection inspection)
        {
            if (inspection == null ||
                (!inspection.DimensionWidth.HasValue &&
                 !inspection.DimensionHeight.HasValue &&
                 !inspection.DimensionDepth.HasValue))
            {
                return "W: -  H: -  D: -";
            }

            string unit = string.IsNullOrWhiteSpace(inspection.DimensionUnit) ? "mm" : inspection.DimensionUnit;
            return "W: " + FormatDimension(inspection.DimensionWidth) + " " + unit +
                   "  H: " + FormatDimension(inspection.DimensionHeight) + " " + unit +
                   "  D: " + FormatDimension(inspection.DimensionDepth) + " " + unit;
        }

        private string BuildSlotDimensionText(AiViewInferenceResult viewResult)
        {
            if (viewResult == null ||
                (!viewResult.DimensionWidth.HasValue &&
                 !viewResult.DimensionHeight.HasValue &&
                 !viewResult.DimensionDepth.HasValue))
            {
                return "W: -  H: -  D: -";
            }

            string unit = string.IsNullOrWhiteSpace(viewResult.DimensionUnit) ? "mm" : viewResult.DimensionUnit;
            return "W: " + FormatDimension(viewResult.DimensionWidth) + " " + unit +
                   "  H: " + FormatDimension(viewResult.DimensionHeight) + " " + unit +
                   "  D: " + FormatDimension(viewResult.DimensionDepth) + " " + unit;
        }

        private decimal NormalizeScoreForDisplay(decimal score)
        {
            return score >= 0m && score <= 1m ? score * 100m : score;
        }

        private string FormatDimension(decimal? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.00", CultureInfo.InvariantCulture)
                : "-";
        }

        private Part BuildLivePreviewPart()
        {
            Part part = new Part();
            part.PartNo = "LIVE_PREVIEW";
            part.PartName = "Live Preview";
            return part;
        }

        private void ApplyLivePreviewFailure(ImageSlotViewModel slot, string cameraName, string message)
        {
            string displayMessage = TrimLivePreviewMessage(message);
            slot.LiveImagePath = string.Empty;
            slot.IsCapturedStillVisible = false;
            slot.StatusText = "수신 실패: " + displayMessage;
            slot.ResultText = "ERROR";
            slot.ResultBrush = "#A96F16";
            AddLivePreviewEvent(EventSeverity.Error, cameraName + " 수신 실패: " + displayMessage);
        }

        private void AddLivePreviewEvent(EventSeverity severity, string message)
        {
            EventLogEntry entry = new EventLogEntry();
            entry.Severity = severity;
            entry.Source = "Camera";
            entry.Message = message;
            EventRows.Add(new EventRowViewModel(entry));
        }

        private void AddInspectionEvent(EventSeverity severity, string message)
        {
            EventLogEntry entry = new EventLogEntry();
            entry.Severity = severity;
            entry.Source = "Inspection";
            entry.Message = message;
            EventRows.Add(new EventRowViewModel(entry));
        }

        private string TrimLivePreviewMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "상세 오류 없음";
            }

            string compact = message.Replace("\r", " ").Replace("\n", " ").Trim();
            if (compact.Length > 80)
            {
                return compact.Substring(0, 80) + "...";
            }

            return compact;
        }

        private void LoadInspectionMeasurements(Inspection inspection)
        {
            InspectionMeasurements.Clear();
            Part referencePart = ResolvePartForInspection(inspection);
            if (referencePart != null)
            {
                foreach (MeasurementRegion region in GetMeasurementRegionsInDisplayOrder(referencePart))
                {
                    MeasurementResult measurement = FindMeasurementResult(inspection.Measurements, region.Id);
                    InspectionMeasurements.Add(new MeasurementRowViewModel(region, measurement));
                }

                foreach (MeasurementResult measurement in inspection.Measurements)
                {
                    if (!HasMeasurementRegion(referencePart, measurement.MeasurementRegionId))
                    {
                        InspectionMeasurements.Add(new MeasurementRowViewModel(measurement));
                    }
                }

                return;
            }

            foreach (MeasurementResult measurement in inspection.Measurements)
            {
                InspectionMeasurements.Add(new MeasurementRowViewModel(measurement));
            }
        }

        /// <summary>
        /// 측정부는 카메라별로 번호가 1부터 다시 시작합니다. 저장 순서는 생성한 순서라
        /// Top 1, Thk 1, Top 2처럼 섞일 수 있으므로, 화면에서는 카메라별로 묶어
        /// Top 1~N 다음 Thk 1~N 순서로 보여 줍니다.
        ///
        /// <para>원본 Part.MeasurementRegions의 순서는 변경하지 않습니다.</para>
        /// </summary>
        private static IList<MeasurementRegion> GetMeasurementRegionsInDisplayOrder(Part part)
        {
            List<MeasurementRegion> regions = new List<MeasurementRegion>();
            if (part == null || part.MeasurementRegions == null)
            {
                return regions;
            }

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region != null)
                {
                    regions.Add(region);
                }
            }

            regions.Sort(CompareMeasurementRegionsForDisplay);
            return regions;
        }

        private static int CompareMeasurementRegionsForDisplay(
            MeasurementRegion left,
            MeasurementRegion right)
        {
            int viewTypeComparison = left.ViewType.CompareTo(right.ViewType);
            if (viewTypeComparison != 0)
            {
                return viewTypeComparison;
            }

            int indexComparison = left.IndexNo.CompareTo(right.IndexNo);
            if (indexComparison != 0)
            {
                return indexComparison;
            }

            return left.Id.CompareTo(right.Id);
        }

        private Part ResolvePartForInspection(Inspection inspection)
        {
            if (inspection == null || string.IsNullOrWhiteSpace(inspection.PartNo))
            {
                return null;
            }

            if (SelectedPart != null &&
                string.Equals(SelectedPart.PartNo, inspection.PartNo, StringComparison.OrdinalIgnoreCase))
            {
                return SelectedPart.Part;
            }

            return _partDataStore.GetPart(inspection.PartNo);
        }

        private MeasurementResult FindMeasurementResult(IList<MeasurementResult> measurements, int measurementRegionId)
        {
            if (measurements == null)
            {
                return null;
            }

            foreach (MeasurementResult measurement in measurements)
            {
                if (measurement.MeasurementRegionId == measurementRegionId)
                {
                    return measurement;
                }
            }

            return null;
        }

        private bool HasMeasurementRegion(Part part, int measurementRegionId)
        {
            if (part == null || part.MeasurementRegions == null)
            {
                return false;
            }

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region.Id == measurementRegionId)
                {
                    return true;
                }
            }

            return false;
        }

        private void LoadEvents(Inspection inspection)
        {
            EventRows.Clear();
            foreach (EventLogEntry entry in inspection.Events)
            {
                EventRows.Add(new EventRowViewModel(entry));
            }
        }

        private void PrepareRegistrationWhenPartIsMissing(Inspection inspection)
        {
            if (inspection == null || inspection.Result != InspectionResult.Error)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(inspection.PartNo))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(inspection.InputCode) || !ContainsKeyword(inspection.ResultMessage, "부품 기준정보"))
            {
                return;
            }

            PrepareRegistrationForMissingPartCode(inspection.InputCode);
        }

        private void PrepareRegistrationForMissingPartCode(string inputCode)
        {
            if (string.IsNullOrWhiteSpace(inputCode))
            {
                return;
            }

            RegistrationPartNo = inputCode;
            RegistrationPartName = string.Empty;
            RegistrationCategoryCode = string.Empty;
            RegistrationCategoryDescription = string.Empty;
            RegistrationMemo = string.Empty;
            RegistrationImages.Clear();
            SelectedRegistrationImage = null;
            RegistrationCoordinateImagePath = string.Empty;
            RefreshRegistrationImagePreviews();
            SelectedRegistrationPart = null;
            InitializeEmptyRegistrationPoints();
            _deleteRequested = false;
            SelectedTabIndex = 2;
            SelectedRegistrationSubTabIndex = 0;
            RegistrationMessage = "DB에 없는 품번입니다. 부품 정보를 추가한 뒤 DB 저장을 진행하세요.";
            AddInspectionEvent(EventSeverity.Warning, "DB 미등록 품번을 부품 등록 화면에 입력했습니다. 품명/분류/측정부 기준정보를 먼저 등록해야 검사할 수 있습니다. 기준 이미지는 등록을 권장하지만 검사 시도 자체를 차단하지 않습니다.");
        }

        private void ExecuteSavePart(object parameter)
        {
            string selectedInspectionPartNo = GetPartNo(SelectedPart);
            string originalRegistrationPartNo = GetPartNo(SelectedRegistrationPart);

            // 삭제도 생성/수정과 동일하게 DB 저장 버튼에서 실제 반영합니다.
            // 사용자가 실수로 삭제 버튼을 누른 경우 즉시 데이터가 사라지지 않게 하기 위한 흐름입니다.
            if (_deleteRequested)
            {
                string deletePartNo = RegistrationPartNo;
                bool shouldRefreshInspectionPart = IsSamePartNo(selectedInspectionPartNo, deletePartNo) ||
                                                   IsSamePartNo(selectedInspectionPartNo, originalRegistrationPartNo);

                RegistrationMessage = _partDataStore.DeletePart(RegistrationPartNo);
                if (RegistrationMessage == PartCatalogService.DeleteSuccessMessage)
                {
                    RegistrationMessage = RegistrationMessage + " 기준 이미지 파일은 삭제하지 않았습니다.";
                }

                _deleteRequested = false;
                ExecuteNewPart(null);
                RefreshPartCollectionsFromDataStore();
                if (shouldRefreshInspectionPart)
                {
                    RefreshInspectionPartSelectionAfterDelete(selectedInspectionPartNo);
                }

                RefreshStatistics();
                return;
            }

            Part part;
            string buildErrorMessage;
            if (!TryBuildRegistrationPart(out part, out buildErrorMessage))
            {
                RegistrationMessage = buildErrorMessage;
                ShowSaveBlockedPopup(buildErrorMessage);
                return;
            }

            string validationMessage = _partDataStore.ValidatePartForSave(part);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                RegistrationMessage = validationMessage;
                ShowSaveBlockedPopup(validationMessage);
                return;
            }

            string coordinateErrorMessage;
            if (!TrySaveTemporaryCoordinateImage(part, out coordinateErrorMessage))
            {
                RegistrationMessage = coordinateErrorMessage;
                ShowSaveBlockedPopup(coordinateErrorMessage);
                return;
            }

            bool hadTemporaryImages = HasTemporaryReferenceImages(part.Images);
            bool hadTemporaryCoordinateImage = HasTemporaryCoordinateImage(part);
            bool hadReferenceImageChanges = hadTemporaryImages || hadTemporaryCoordinateImage;
            try
            {
                IList<PartImage> committedImages = _referenceImageFileService.CommitTemporaryReferenceImages(part, part.Images);
                part.Images.Clear();
                foreach (PartImage committedImage in committedImages)
                {
                    part.Images.Add(committedImage);
                }

                // 좌표 이미지는 카메라마다 한 장씩 있으므로 모두 확정합니다.
                foreach (ImageViewType coordinateViewType in MeasurementPointPolicy.GetSupportedViewTypes())
                {
                    _referenceImageFileService.CommitTemporaryCoordinateImage(part, coordinateViewType);
                }
            }
            catch (Exception ex)
            {
                RegistrationMessage = "임시 기준 이미지를 최종 이미지 폴더로 확정하지 못했습니다. Temp 파일은 유지됩니다. 상세: " + ex.Message;
                _messageDialogService.ShowWarning("기준 이미지 저장 실패", RegistrationMessage);
                return;
            }

            bool isRegistrationPartSelectedInSearchDb = IsRegistrationPartSelectedInSearchDb(part.PartNo, originalRegistrationPartNo, selectedInspectionPartNo);
            RegistrationMessage = _partDataStore.SavePart(part);
            if (RegistrationMessage != PartCatalogService.SaveSuccessMessage)
            {
                ShowSaveBlockedPopup(RegistrationMessage);
                return;
            }

            // 여섯 장을 하나로 합치는 일은 뒤에서 합니다.
            //
            // 이 일은 SDK 안에서 도는데 앞선 실측에서 10초를 넘긴 적이 있습니다. 저장 단추를
            // 누른 자리에서 그대로 부르면 그동안 창이 통째로 멈춥니다. 저장 자체는 이미 끝났고
            // 합친 그림은 나중에 쓰는 것이라, 기다릴 까닭이 없습니다.
            //
            // 결과는 안내 문구에만 쓰였으므로 끝난 뒤 문구에 덧붙입니다.
            string referenceImageMergeMessage = string.Empty;
            if (hadTemporaryImages && _imageMergeService != null)
            {
                StartReferenceImageMergeInBackground(part);
            }

            string ocrTemporaryCleanupWarning = ClearRegistrationOcrTemporaryFiles();
            if (string.IsNullOrWhiteSpace(ocrTemporaryCleanupWarning))
            {
                ClearLatestRegistrationOcrResult();
            }

            _referenceImageFileService.ClearTemporaryReferenceImages(part);
            LoadRegistrationImages(part);

            System.Diagnostics.Stopwatch refreshWatch = System.Diagnostics.Stopwatch.StartNew();
            RefreshPartCollectionsFromDataStore(part.PartNo);
            if (isRegistrationPartSelectedInSearchDb)
            {
                RefreshInspectionPartSelection(part.PartNo);
            }

            RefreshStatistics();
            refreshWatch.Stop();

            RegistrationMessage = hadTemporaryImages
                ? PartCatalogService.SaveSuccessMessage + " 임시 기준 이미지를 최종 폴더로 확정하고 등록시간을 갱신했습니다." +
                  BuildSaveTimingText(0, refreshWatch.ElapsedMilliseconds)
                : PartCatalogService.SaveSuccessMessage +
                  BuildSaveTimingText(0, refreshWatch.ElapsedMilliseconds);

            if (!string.IsNullOrWhiteSpace(ocrTemporaryCleanupWarning))
            {
                RegistrationMessage = RegistrationMessage + " " + ocrTemporaryCleanupWarning;
            }

            if (!string.IsNullOrWhiteSpace(referenceImageMergeMessage))
            {
                RegistrationMessage = RegistrationMessage + " " + referenceImageMergeMessage;
            }

            if (hadReferenceImageChanges)
            {
                PromptImageTrainingAfterImageChange("DB 기준 이미지가 등록 또는 변경되었습니다.");
            }
        }

        /// <summary>
        /// 등록 OCR 버튼으로 생성한 OCR_PATH 하위 이미지와 JSON을 DB 저장 성공 후 정리합니다.
        /// 삭제 실패 파일은 목록에 유지하여 다음 저장 성공 시 다시 정리할 수 있습니다.
        /// </summary>
        private string ClearRegistrationOcrTemporaryFiles()
        {
            string firstErrorMessage = string.Empty;
            for (int index = _registrationOcrTemporaryResults.Count - 1; index >= 0; index--)
            {
                OcrScanExecutionResult result = _registrationOcrTemporaryResults[index];
                try
                {
                    _ocrScanService.DeleteTemporaryFiles(result);
                    _registrationOcrTemporaryResults.RemoveAt(index);
                }
                catch (Exception exception)
                {
                    if (string.IsNullOrWhiteSpace(firstErrorMessage))
                    {
                        firstErrorMessage = exception.Message;
                    }
                }
            }

            return string.IsNullOrWhiteSpace(firstErrorMessage)
                ? string.Empty
                : "등록 OCR 임시 이미지 또는 JSON 일부를 삭제하지 못했습니다: " + firstErrorMessage;
        }

        /// <summary>
        /// 등록 OCR의 임시 이미지와 JSON이 정상적으로 정리된 뒤에만 최근 OCR 결과 영역을 비웁니다.
        /// 검사 OCR의 최근 결과와 OCR 이력 Grid는 유지합니다.
        /// </summary>
        private void ClearLatestRegistrationOcrResult()
        {
            if (!string.Equals(OcrLatestUsageText, "(등록 기능)", StringComparison.Ordinal))
            {
                return;
            }

            OcrLatestImagePath = string.Empty;
            OcrLatestPartNo = string.Empty;
            OcrLatestRawText = string.Empty;
            OcrLatestUsageText = string.Empty;
        }

        private void ExecuteDeletePart(object parameter)
        {
            if (string.IsNullOrWhiteSpace(RegistrationPartNo))
            {
                RegistrationMessage = "삭제할 품번이 없습니다.";
                return;
            }

            _deleteRequested = true;
            RegistrationMessage = "삭제 예정 상태입니다. DB 저장을 누르면 실제 DB에서 삭제됩니다.";
        }

        private bool TryBuildRegistrationPart(out Part part, out string errorMessage)
        {
            part = new Part();
            errorMessage = string.Empty;
            part.PartNo = RegistrationPartNo;
            part.PartName = RegistrationPartName;
            part.CategoryCode = RegistrationCategoryCode;
            part.CategoryDescription = RegistrationCategoryDescription;
            part.Memo = RegistrationMemo;

            IList<ImageViewType> addedImageViewTypes = new List<ImageViewType>();
            IList<PartImage> orderedImages = new List<PartImage>();
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                orderedImages.Add(imageViewModel.Image);
            }

            foreach (PartImage image in BuildOrderedUniqueImages(orderedImages))
            {
                if (!ContainsImageViewType(addedImageViewTypes, image.ViewType))
                {
                    image.PartNo = part.PartNo;
                    part.Images.Add(image);
                    addedImageViewTypes.Add(image.ViewType);
                }
            }

            // 측정부 수는 카메라마다 셉니다.
            //
            // 예전에는 전체를 세어 다섯 개를 넘으면 막았습니다. 측정부를 카메라별로 두면서
            // Top 다섯 개와 Thickness 다섯 개를 함께 등록할 수 있게 됐는데, 그 검사가 그대로 남아
            // 열 개를 저장하려 하면 통째로 거부됐습니다.
            foreach (ImageViewType measurementViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                int countForView = 0;
                foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
                {
                    if (point != null && point.ViewType == measurementViewType)
                    {
                        countForView++;
                    }
                }

                if (countForView > MeasurementPointPolicy.MaxCount)
                {
                    errorMessage = MeasurementPointPolicy.GetViewShortName(measurementViewType) +
                                   " 측정부는 최대 " +
                                   MeasurementPointPolicy.MaxCount.ToString(CultureInfo.InvariantCulture) +
                                   "개까지만 등록할 수 있습니다.";
                    return false;
                }
            }

            int regionId = 1;
            foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
            {
                MeasurementRegion region;
                if (!point.TryBuildRegion(part.PartNo, regionId, out region, out errorMessage))
                {
                    return false;
                }

                part.MeasurementRegions.Add(region);
                regionId++;
            }

            return true;
        }

        private bool ContainsImageViewType(IList<ImageViewType> viewTypes, ImageViewType viewType)
        {
            foreach (ImageViewType existingViewType in viewTypes)
            {
                if (existingViewType == viewType)
                {
                    return true;
                }
            }

            return false;
        }

        private PartViewModel FindPartViewModel(string partNo)
        {
            foreach (PartViewModel part in Parts)
            {
                if (IsSamePartNo(part.PartNo, partNo))
                {
                    return part;
                }
            }

            return null;
        }

        private PartViewModel FindDbPartViewModel(string partNo)
        {
            foreach (PartViewModel part in DbParts)
            {
                if (IsSamePartNo(part.PartNo, partNo))
                {
                    return part;
                }
            }

            return null;
        }

        private string GetPartNo(PartViewModel partViewModel)
        {
            if (partViewModel == null)
            {
                return string.Empty;
            }

            return partViewModel.PartNo;
        }

        private bool IsSamePartNo(string leftPartNo, string rightPartNo)
        {
            if (string.IsNullOrWhiteSpace(leftPartNo) || string.IsNullOrWhiteSpace(rightPartNo))
            {
                return false;
            }

            return string.Equals(leftPartNo.Trim(), rightPartNo.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRegistrationPartSelectedInSearchDb(string savedPartNo, string originalRegistrationPartNo, string selectedInspectionPartNo)
        {
            return IsSamePartNo(selectedInspectionPartNo, savedPartNo) ||
                   IsSamePartNo(selectedInspectionPartNo, originalRegistrationPartNo);
        }

        private void RefreshInspectionPartSelection(string partNo)
        {
            PartViewModel partViewModel = FindPartViewModel(partNo);
            if (partViewModel == null)
            {
                SelectedPart = null;
                InputCode = string.Empty;
                ClearSelectedPartDetails();
                return;
            }

            if (SelectedPart != partViewModel)
            {
                SelectedPart = partViewModel;
            }
            else
            {
                ApplySelectedPart();
            }
        }

        private void RefreshInspectionPartSelectionAfterDelete(string deletedPartNo)
        {
            if (FindPartViewModel(deletedPartNo) != null)
            {
                RefreshInspectionPartSelection(deletedPartNo);
                return;
            }

            SelectedPart = null;
            InputCode = string.Empty;
            ClearSelectedPartDetails();
        }

        private void ExecuteNewPart(object parameter)
        {
            // 신규 입력 전환은 현재 등록 작업을 취소하는 동작입니다.
            // OCR뿐 아니라 기준/좌표 Temp 파일도 남기지 않습니다.
            ClearTemporaryReferenceImagesForCurrentRegistrationPart();

            // 신규 입력으로 전환하면 이전 등록 OCR의 임시 이미지와 JSON은 더 이상 사용할 수 없습니다.
            // OCR_PATH에 임시 파일이 남지 않도록 저장 완료와 동일하게 정리합니다.
            string ocrTemporaryCleanupWarning = ClearRegistrationOcrTemporaryFiles();
            if (string.IsNullOrWhiteSpace(ocrTemporaryCleanupWarning))
            {
                ClearLatestRegistrationOcrResult();
            }

            RegistrationPartNo = string.Empty;
            RegistrationPartName = string.Empty;
            RegistrationCategoryCode = string.Empty;
            RegistrationCategoryDescription = string.Empty;
            RegistrationMemo = string.Empty;
            RegistrationImages.Clear();
            SelectedRegistrationImage = null;
            RegistrationCoordinateImagePath = string.Empty;
            RefreshRegistrationImagePreviews();
            SelectedReferenceImageViewType = ImageViewType.Top.ToString();
            InitializeEmptyRegistrationPoints();
            _deleteRequested = false;
            RegistrationMessage = "신규 부품 정보를 입력하세요.";
            SelectedTabIndex = 2;
        }

        private void ExecuteSearch(object parameter)
        {
            _searchDelayTimer.Stop();
            ApplySearchFilters();
        }

        /// <summary>
        /// 옵션 OCR 탭에서 Epson ES-C320W의 WIA 연결 상태를 다시 확인합니다.
        /// </summary>
        private void ExecuteRefreshOcrScanner(object parameter)
        {
            BeginOcrStatusRefresh(false);
        }

        /// <summary>
        /// 메인 Search DB의 돋보기 버튼에서 ADF 스캔, Epson OCR, 부품 선택까지 차례로 처리합니다.
        /// </summary>
        private async void ExecuteStartOcrScan(object parameter)
        {
            if (IsOcrScanRunning)
            {
                return;
            }

            IsOcrScanRunning = true;
            OcrScannerStatusText = "스캔 중: Epson ES-C320W";
            OcrStatusText = "Epson ES-C320W에서 OCR 스캔을 진행 중입니다.";
            try
            {
                OcrScanConfiguration configuration = new OcrScanConfiguration();
                configuration.ResolutionDpi = OcrResolutionDpi;
                configuration.ColorMode = OcrColorMode;
                OcrScanExecutionResult result = await _ocrScanService.ScanAsync(configuration, OcrScanUsage.Inspection);
                if (result.IsSuccess)
                {
                    OcrSearchApplyResult searchResult = TryApplyOcrPartNoToSearch(result.PartNo);
                    string historyStatus = GetInspectionOcrHistoryStatus(searchResult);
                    ApplyOcrLatestResult(result, OcrScanUsage.Inspection, historyStatus);
                    AddOcrHistory(result, historyStatus, configuration.ResolutionDpi, configuration.ColorMode, OcrScanUsage.Inspection);
                }
                else
                {
                    OcrStatusText = result.Message;
                    ApplyOcrLatestResult(result, OcrScanUsage.Inspection, "오류");
                    AddOcrHistory(result, "오류", configuration.ResolutionDpi, configuration.ColorMode, OcrScanUsage.Inspection);
                }
            }
            catch (Exception exception)
            {
                OcrScannerStatusText = "오류";
                OcrStatusText = "OCR 처리 중 예외가 발생했습니다: " + exception.Message;
                OcrScanExecutionResult failedResult = new OcrScanExecutionResult
                {
                    IsSuccess = false,
                    Message = OcrStatusText
                };
                // 예외 결과도 최근 OCR 영역의 사용 기능과 오류 원인을 일관되게 표시합니다.
                ApplyOcrLatestResult(failedResult, OcrScanUsage.Inspection, "오류");
                AddOcrHistory(failedResult, "오류", OcrResolutionDpi, OcrColorMode, OcrScanUsage.Inspection);
            }
            finally
            {
                IsOcrScanRunning = false;
                RestoreOcrReadyStatus();
            }
        }

        private bool CanStartOcrScan(object parameter)
        {
            return !IsOcrScanRunning;
        }

        /// <summary>
        /// 단일품목 등록 화면의 OCR 입력 버튼 처리입니다.
        /// Search DB 선택 상태를 변경하지 않고, 신규 부품 입력 영역의 품번만 채웁니다.
        /// </summary>
        private async void ExecuteRegistrationOcrInput(object parameter)
        {
            if (IsOcrScanRunning)
            {
                return;
            }

            IsOcrScanRunning = true;
            OcrScannerStatusText = "스캔 중: Epson ES-C320W";
            OcrStatusText = "Epson ES-C320W에서 등록용 OCR 스캔을 진행 중입니다.";
            try
            {
                OcrScanConfiguration configuration = new OcrScanConfiguration();
                configuration.ResolutionDpi = OcrResolutionDpi;
                configuration.ColorMode = OcrColorMode;

                OcrScanExecutionResult result = await _ocrScanService.ScanAsync(configuration, OcrScanUsage.Registration);
                TrackRegistrationOcrTemporaryResult(result);

                if (!result.IsSuccess)
                {
                    RegistrationMessage = "등록 OCR 입력 실패: " + result.Message;
                    OcrStatusText = RegistrationMessage;
                    ApplyOcrLatestResult(result, OcrScanUsage.Registration, "오류");
                    AddOcrHistory(result, "오류", configuration.ResolutionDpi, configuration.ColorMode, OcrScanUsage.Registration);
                    _messageDialogService.ShowWarning(
                        "등록 OCR 입력 실패",
                        RegistrationMessage + Environment.NewLine + "OCR 원문: " + (result.RawText ?? string.Empty));
                    return;
                }

                string partNo = result.PartNo == null ? string.Empty : result.PartNo.Trim();
                if (string.IsNullOrWhiteSpace(partNo))
                {
                    RegistrationMessage = "등록 OCR에서 품번을 추출하지 못했습니다.";
                    OcrStatusText = RegistrationMessage;
                    ApplyOcrLatestResult(result, OcrScanUsage.Registration, "오류");
                    AddOcrHistory(result, "오류", configuration.ResolutionDpi, configuration.ColorMode, OcrScanUsage.Registration);
                    _messageDialogService.ShowWarning("등록 OCR 품번 인식 실패", RegistrationMessage);
                    return;
                }

                Part storedPart = _partDataStore.GetPart(partNo);
                if (storedPart != null)
                {
                    // 이미 등록된 품번은 등록 화면의 Information Input, 측정부, 기준 이미지를 함께 불러옵니다.
                    // 현재 DB 조회 목록에 같은 행이 있으면 선택 상태도 맞춰 편집 대상을 명확히 표시합니다.
                    PartViewModel registrationPartViewModel = FindDbPartViewModel(storedPart.PartNo);
                    if (registrationPartViewModel != null)
                    {
                        SelectedRegistrationPart = registrationPartViewModel;
                    }
                    else
                    {
                        LoadRegistrationForm(storedPart);
                    }

                    RegistrationMessage = "OCR로 인식한 품번 '" + partNo + "'은 이미 등록되어 있습니다.";
                    OcrStatusText = RegistrationMessage;
                    ApplyOcrLatestResult(result, OcrScanUsage.Registration, "(기등록)");
                    AddOcrHistory(result, "(기등록)", configuration.ResolutionDpi, configuration.ColorMode, OcrScanUsage.Registration);
                    _messageDialogService.ShowWarning("이미 등록된 품번", RegistrationMessage);
                    return;
                }

                // 등록 OCR은 검사 작업대(Search DB)를 건드리지 않고 신규 입력 품번만 설정합니다.
                PrepareRegistrationFormForNewOcrPart(partNo);
                RegistrationMessage = "OCR 인식 품번 '" + partNo + "'을 신규 부품 입력란에 적용했습니다.";
                OcrStatusText = RegistrationMessage;
                ApplyOcrLatestResult(result, OcrScanUsage.Registration, "정상");
                AddOcrHistory(result, "정상", configuration.ResolutionDpi, configuration.ColorMode, OcrScanUsage.Registration);
            }
            catch (Exception exception)
            {
                OcrScannerStatusText = "오류";
                OcrStatusText = "등록 OCR 처리 중 예외가 발생했습니다: " + exception.Message;
                RegistrationMessage = OcrStatusText;
                OcrScanExecutionResult failedResult = new OcrScanExecutionResult
                {
                    IsSuccess = false,
                    Message = OcrStatusText
                };
                // 등록 OCR 예외도 옵션의 최근 결과 영역을 갱신해 이전 결과가 남지 않게 합니다.
                ApplyOcrLatestResult(failedResult, OcrScanUsage.Registration, "오류");
                AddOcrHistory(failedResult, "오류", OcrResolutionDpi, OcrColorMode, OcrScanUsage.Registration);
                _messageDialogService.ShowWarning("등록 OCR 입력 실패", OcrStatusText);
            }
            finally
            {
                IsOcrScanRunning = false;
                RestoreOcrReadyStatus();
            }
        }

        /// <summary>
        /// DB 저장 전까지 등록 OCR이 생성한 임시 파일 목록을 기억합니다.
        /// 같은 이미지가 중복 등록되지 않도록 이미지 경로 기준으로 확인합니다.
        /// </summary>
        private void TrackRegistrationOcrTemporaryResult(OcrScanExecutionResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.ImagePath))
            {
                return;
            }

            foreach (OcrScanExecutionResult existingResult in _registrationOcrTemporaryResults)
            {
                if (existingResult != null &&
                    string.Equals(existingResult.ImagePath, result.ImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _registrationOcrTemporaryResults.Add(result);
        }

        /// <summary>
        /// 최근 OCR 결과 30건만 메모리에 유지합니다.
        /// 원본 이미지 파일은 OUTPUT_PATH 보존 정책이 기간 또는 HDD 여유 공간 기준으로 관리합니다.
        /// </summary>
        private void AddOcrHistory(
            OcrScanExecutionResult result,
            string status,
            int resolutionDpi,
            string colorMode,
            OcrScanUsage usage)
        {
            OcrHistoryRowViewModel row = new OcrHistoryRowViewModel();
            row.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            row.PartNo = result == null || string.IsNullOrWhiteSpace(result.PartNo) ? "-" : result.PartNo;
            // 최근 OCR 이력의 상태는 EpsonScanApi가 반환한 status를 그대로 표시합니다.
            // DB 등록 여부, 품질 참고 값, WPF의 후속 선택 결과로 status를 다시 판단하지 않습니다.
            row.Status = GetOcrApiStatusText(result);
            row.Resolution = resolutionDpi.ToString(CultureInfo.InvariantCulture) + " DPI";
            row.ColorMode = GetOcrColorModeDisplayName(colorMode);
            row.Usage = GetOcrUsageDisplayName(usage);
            row.ImagePath = result == null ? string.Empty : result.ImagePath;
            OcrHistory.Insert(0, row);
            while (OcrHistory.Count > 30)
            {
                OcrHistory.RemoveAt(OcrHistory.Count - 1);
            }
        }

        /// <summary>
        /// 검사 검색과 신규 부품 등록이 같은 최근 OCR 결과 영역을 공유하도록 갱신합니다.
        /// </summary>
        private void ApplyOcrLatestResult(OcrScanExecutionResult result, OcrScanUsage usage, string classification)
        {
            OcrLatestImagePath = result == null ? string.Empty : result.ImagePath;
            OcrLatestPartNo = result == null ? string.Empty : (result.PartNo ?? string.Empty).Trim();
            OcrLatestRawText = BuildOcrLatestDisplayText(result, usage, classification);
            OcrLatestUsageText = "(" + GetOcrLatestUsageDisplayName(usage) + ")";
        }

        /// <summary>
        /// 최근 OCR 영역에 원문만 표시하지 않고 판정 품번과 DB 조회 결과를 함께 표시합니다.
        /// OCR 성공 후 DB에 없는 경우는 스캔 오류가 아닌 '(미등록)' 상태로 명확히 구분합니다.
        /// </summary>
        private static string BuildOcrLatestDisplayText(
            OcrScanExecutionResult result,
            OcrScanUsage usage,
            string databaseClassification)
        {
            if (result == null)
            {
                return string.Empty;
            }

            string partNo = string.IsNullOrWhiteSpace(result.PartNo) ? "-" : result.PartNo.Trim();
            string source = string.IsNullOrWhiteSpace(result.PartNoSource) ? "-" : result.PartNoSource;
            StringBuilder text = new StringBuilder();

            text.AppendLine("[OCR 판정 정보]");
            text.AppendLine("기능: " + GetOcrLatestUsageDisplayName(usage));
            text.AppendLine("API 상태: " + GetOcrApiStatusText(result));
            text.AppendLine("API 오류: " + (string.IsNullOrWhiteSpace(result.ApiErrorMessage) ? "없음" : result.ApiErrorMessage));
            text.AppendLine("부품번호: " + partNo);
            text.AppendLine("DB 조회: " + GetOcrDatabaseLookupText(usage, databaseClassification));
            text.AppendLine("판독 소스: " + source);
            if (result.Confidence > 0.0)
            {
                text.AppendLine("품질 신뢰도: " + (result.Confidence * 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "%");
            }

            if (!string.IsNullOrWhiteSpace(result.QualityReason))
            {
                text.AppendLine("품질 참고: " + result.QualityReason);
            }

            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                text.AppendLine("처리 메시지: " + result.Message);
            }

            text.AppendLine();
            text.AppendLine("[OCR 원문]");
            text.Append(ResolveOcrLatestRawText(result));
            return text.ToString();
        }

        /// <summary>
        /// EpsonScanApi가 준 status를 화면과 이력에 그대로 표시합니다.
        /// API status가 없는 로컬 예외만 오류로 표시합니다.
        /// </summary>
        private static string GetOcrApiStatusText(OcrScanExecutionResult result)
        {
            if (result != null && !string.IsNullOrWhiteSpace(result.ApiStatus))
            {
                return result.ApiStatus.Trim();
            }

            return result != null && result.IsSuccess ? "done" : "오류";
        }

        private static string GetOcrDatabaseLookupText(OcrScanUsage usage, string classification)
        {
            if (usage == OcrScanUsage.Registration)
            {
                return string.Equals(classification, "(기등록)", StringComparison.Ordinal)
                    ? "이미 등록된 품번"
                    : "신규 등록 입력란 사용";
            }

            if (string.Equals(classification, "정상", StringComparison.Ordinal))
            {
                return "등록됨 / 검사 대상 선택 완료";
            }

            if (string.Equals(classification, "(미등록)", StringComparison.Ordinal))
            {
                return "미등록 / 부품 등록 필요";
            }

            return "조회하지 못함";
        }

        private static string GetInspectionOcrHistoryStatus(OcrSearchApplyResult result)
        {
            switch (result)
            {
                case OcrSearchApplyResult.Selected:
                    return "정상";
                case OcrSearchApplyResult.Unregistered:
                    return "(미등록)";
                default:
                    return "오류";
            }
        }

        /// <summary>
        /// OCR API 응답 객체의 원문을 우선 사용하고, 전달 과정에서 누락된 경우
        /// 저장된 결과 JSON의 ocr.text를 다시 읽어 옵션 화면에 표시합니다.
        /// </summary>
        private static string ResolveOcrLatestRawText(OcrScanExecutionResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(result.RawText))
            {
                return result.RawText;
            }

            if (!string.IsNullOrWhiteSpace(result.ResultJsonPath) &&
                File.Exists(result.ResultJsonPath))
            {
                try
                {
                    string responseJson = File.ReadAllText(result.ResultJsonPath);
                    Match textMatch = Regex.Match(
                        responseJson,
                        "\\\"text\\\"\\s*:\\s*\\\"(?<value>(?:\\\\.|[^\\\"\\\\])*)\\\"",
                        RegexOptions.CultureInvariant);
                    if (textMatch.Success)
                    {
                        return Regex.Unescape(textMatch.Groups["value"].Value);
                    }
                }
                catch (Exception)
                {
                    // 원문 표시 보조 복원 실패는 OCR 판정 결과를 변경하지 않습니다.
                }
            }

            return string.IsNullOrWhiteSpace(result.Message)
                ? "OCR 원문이 API 응답에 없습니다."
                : "OCR 원문이 API 응답에 없습니다. " + result.Message;
        }

        private static string GetOcrLatestUsageDisplayName(OcrScanUsage usage)
        {
            return usage == OcrScanUsage.Registration ? "등록 기능" : "검사 기능";
        }

        private static string GetOcrUsageDisplayName(OcrScanUsage usage)
        {
            return usage == OcrScanUsage.Registration ? "등록" : "검사";
        }

        /// <summary>
        /// 설정 파일의 내부 색상 모드 값을 작업자가 확인하기 쉬운 한글 이름으로 변환합니다.
        /// </summary>
        private static string GetOcrColorModeDisplayName(string colorMode)
        {
            if (string.Equals(colorMode, "bw", StringComparison.OrdinalIgnoreCase))
            {
                return "흑백";
            }

            if (string.Equals(colorMode, "color", StringComparison.OrdinalIgnoreCase))
            {
                return "컬러";
            }

            return "회색조";
        }

        /// <summary>
        /// OCR이 판정한 품번을 Search DB에 반영하고, 정확히 같은 품번이 있을 때만 검사 대상 부품으로 선택합니다.
        /// OCR 결과가 DB에 없을 때 유사 품번을 임의로 선택하지 않아 오검사를 방지합니다.
        /// </summary>
        private OcrSearchApplyResult TryApplyOcrPartNoToSearch(string partNo)
        {
            string normalizedPartNo = partNo == null ? string.Empty : partNo.Trim();
            SearchKeyword = normalizedPartNo;
            _mainSearchDelayTimer.Stop();
            RefreshMainSearchSuggestions();

            if (string.IsNullOrWhiteSpace(normalizedPartNo))
            {
                OcrStatusText = "OCR 원문에서 부품번호를 판정하지 못했습니다. 스캔 이미지와 OCR 원문을 확인하세요.";
                _messageDialogService.ShowWarning("OCR 부품번호 판정 실패", OcrStatusText);
                return OcrSearchApplyResult.RecognitionFailed;
            }

            Part part = _partDataStore.GetPart(normalizedPartNo);
            if (part == null)
            {
                OcrStatusText = "OCR 인식 품번 '" + normalizedPartNo + "'가 DB에 없습니다. 부품 등록 탭에서 새 제품을 등록하세요.";
                bool moveToRegistration = _messageDialogService.ShowOcrUnregisteredConfirmation(
                    "OCR 부품 미등록",
                    OcrStatusText + Environment.NewLine + Environment.NewLine +
                    "'등록진행'을 누르면 단일품목 등록 화면으로 이동해 이 품번(" + normalizedPartNo + ")으로 신규 등록을 시작합니다.");
                if (moveToRegistration)
                {
                    PrepareRegistrationForMissingPartCode(normalizedPartNo);
                }

                return OcrSearchApplyResult.Unregistered;
            }

            PartViewModel partViewModel = FindPartViewModel(part.PartNo);
            if (partViewModel == null)
            {
                OcrStatusText = "OCR 인식 품번 '" + normalizedPartNo + "'의 화면 목록을 새로고침하지 못했습니다.";
                _messageDialogService.ShowWarning("OCR Search DB 반영 실패", OcrStatusText);
                return OcrSearchApplyResult.ListRefreshFailed;
            }

            if (SelectedPart != partViewModel)
            {
                SelectedPart = partViewModel;
            }
            else
            {
                ApplySelectedPart();
            }

            OcrStatusText = "OCR 인식 품번 '" + normalizedPartNo + "'를 Search DB에서 선택했습니다.";
            return OcrSearchApplyResult.Selected;
        }

        /// <summary>
        /// 검사용 OCR 후속 처리 결과입니다. OCR 스캔 오류와 DB 미등록을 분리해 화면과 이력에 표시합니다.
        /// </summary>
        private enum OcrSearchApplyResult
        {
            Selected,
            Unregistered,
            RecognitionFailed,
            ListRefreshFailed
        }

        private void ExecuteApplyMainSearchSuggestion(object parameter)
        {
            string suggestion = parameter as string;
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                return;
            }

            SearchKeyword = suggestion;
            RefreshMainSearchSuggestions();
            ApplyInspectionPartFromMainSearch(suggestion, true);
        }

        private void ExecuteApplyPartNameSearchSuggestion(object parameter)
        {
            ExecuteApplyDbSearchSuggestion(parameter);
        }

        private void ExecuteApplyDbSearchSuggestion(object parameter)
        {
            string suggestion = parameter as string;
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                return;
            }

            ApplyDbSearchSuggestionValue(suggestion);
            ExecuteSearch(null);
        }

        private void ApplyDbSearchSuggestionValue(string suggestion)
        {
            if (_activeDbSearchFieldName == SearchFieldPartNo)
            {
                SearchPartNo = suggestion;
                return;
            }

            if (_activeDbSearchFieldName == SearchFieldCategoryCode)
            {
                SearchCategoryCode = suggestion;
                return;
            }

            if (_activeDbSearchFieldName == SearchFieldCategoryDescription)
            {
                SearchCategoryDescription = suggestion;
                return;
            }

            SearchPartName = suggestion;
        }

        private void QueueMainSearchRefresh()
        {
            _mainSearchDelayTimer.Stop();
            _mainSearchDelayTimer.Start();
        }

        private void QueueSearchFilterRefresh()
        {
            // 키 입력마다 즉시 전체 UI를 갱신하지 않고 짧게 모아 처리하여 검색창 입력 지연을 줄입니다.
            _searchDelayTimer.Stop();
            _searchDelayTimer.Start();
        }

        private void OnMainSearchDelayTimerTick(object sender, EventArgs e)
        {
            _mainSearchDelayTimer.Stop();
            RefreshMainSearchSuggestions();
            ApplyInspectionPartFromMainSearch(SearchKeyword, false);
        }

        private void OnSearchDelayTimerTick(object sender, EventArgs e)
        {
            _searchDelayTimer.Stop();
            ApplySearchFilters();
        }

        private void ApplySearchFilters()
        {
            string selectedDbPartNo = SelectedDbPart == null ? string.Empty : SelectedDbPart.PartNo;
            string selectedRegistrationPartNo = SelectedRegistrationPart == null ? string.Empty : SelectedRegistrationPart.PartNo;

            PartSearchCriteria criteria = BuildPartSearchCriteria();
            IList<PartViewModel> filteredParts = new List<PartViewModel>();
            foreach (Part part in _partDataStore.SearchParts(criteria))
            {
                filteredParts.Add(new PartViewModel(part));
            }

            // 수천 건 검색 결과를 한 줄씩 추가하면 UI 알림이 반복되어 입력 지연이 발생하므로 컬렉션을 한 번에 교체합니다.
            // 교체 순간 그리드가 선택값에 null 을 되밀 수 있어, 이 구간도 표식으로 감쌉니다.
            bool wasSynchronizing = _isSynchronizingPartSelection;
            _isSynchronizingPartSelection = true;
            try
            {
                DbParts = new ObservableCollection<PartViewModel>(filteredParts);
                OnPropertyChanged("DbParts");
            }
            finally
            {
                _isSynchronizingPartSelection = wasSynchronizing;
            }

            // 부품 목록 전체 갱신(RefreshPartCollectionsFromDataStore) 중에는 여기서 복원하지 않습니다.
            // 그 호출자가 끝에서 저장한 품번으로 직접 복원하는데, 여기서 갱신 전 스냅샷으로 먼저
            // 복원하면 품번을 바꿔 저장한 직후 입력폼이 옛 품번으로 잠깐 되돌아갔습니다.
            if (!wasSynchronizing)
            {
                SelectedDbPart = string.IsNullOrWhiteSpace(selectedDbPartNo) ? null : FindDbPartViewModel(selectedDbPartNo);
                SelectedRegistrationPart = string.IsNullOrWhiteSpace(selectedRegistrationPartNo) ? null : FindDbPartViewModel(selectedRegistrationPartNo);
            }

            RefreshDbSearchSuggestions(criteria);
        }

        private void RefreshMainSearchSuggestions()
        {
            MainSearchSuggestions.Clear();
            if (string.IsNullOrWhiteSpace(SearchKeyword))
            {
                OnPropertyChanged("HasMainSearchSuggestions");
                return;
            }

            PartSearchCriteria criteria = new PartSearchCriteria();
            criteria.GlobalKeyword = SearchKeyword;
            foreach (string suggestion in _partDataStore.BuildSearchSuggestions(criteria, MaxSearchSuggestionCount))
            {
                MainSearchSuggestions.Add(suggestion);
            }

            OnPropertyChanged("HasMainSearchSuggestions");
        }

        private void RefreshDbSearchSuggestions(PartSearchCriteria criteria)
        {
            DbSearchSuggestions.Clear();
            if (!HasDbSearchCriteria(criteria))
            {
                OnPropertyChanged("HasDbSearchSuggestions");
                return;
            }

            foreach (string suggestion in _partDataStore.BuildFieldSearchSuggestions(criteria, _activeDbSearchFieldName, MaxSearchSuggestionCount))
            {
                DbSearchSuggestions.Add(suggestion);
            }

            OnPropertyChanged("HasDbSearchSuggestions");
        }

        private bool HasDbSearchCriteria(PartSearchCriteria criteria)
        {
            if (criteria == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(criteria.PartNo) ||
                   !string.IsNullOrWhiteSpace(criteria.PartName) ||
                   !string.IsNullOrWhiteSpace(criteria.CategoryCode) ||
                   !string.IsNullOrWhiteSpace(criteria.CategoryDescription) ||
                   !string.IsNullOrWhiteSpace(criteria.Memo);
        }

        private void ApplyInspectionPartFromMainSearch(string keyword, bool allowFirstMatchedPart)
        {
            Part part = FindInspectionPartFromMainSearch(keyword, allowFirstMatchedPart);
            if (part == null)
            {
                return;
            }

            PartViewModel partViewModel = FindPartViewModel(part.PartNo);
            if (partViewModel == null)
            {
                return;
            }

            // 좌측 Search DB는 검사 대상 선택용입니다. DB 조회/부품등록 선택 상태와는 분리합니다.
            if (SelectedPart != partViewModel)
            {
                SelectedPart = partViewModel;
            }
            else
            {
                ApplySelectedPart();
            }
        }

        private Part FindInspectionPartFromMainSearch(string keyword, bool allowFirstMatchedPart)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return null;
            }

            string trimmedKeyword = keyword.Trim();
            Part exactPartNoPart = _partDataStore.GetPart(trimmedKeyword);
            if (exactPartNoPart != null)
            {
                return exactPartNoPart;
            }

            Part exactMatchedPart = null;
            Part firstMatchedPart = null;
            int matchedCount = 0;
            foreach (Part part in _partDataStore.GetParts())
            {
                if (IsExactMainSearchMatch(part, trimmedKeyword))
                {
                    exactMatchedPart = part;
                    break;
                }

                if (IsGlobalMainSearchMatch(part, trimmedKeyword))
                {
                    matchedCount++;
                    if (firstMatchedPart == null)
                    {
                        firstMatchedPart = part;
                    }
                }
            }

            if (exactMatchedPart != null)
            {
                return exactMatchedPart;
            }

            if (matchedCount == 1 || allowFirstMatchedPart)
            {
                return firstMatchedPart;
            }

            return null;
        }

        private bool IsExactMainSearchMatch(Part part, string keyword)
        {
            if (part == null)
            {
                return false;
            }

            return string.Equals(part.PartNo, keyword, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(part.PartName, keyword, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsGlobalMainSearchMatch(Part part, string keyword)
        {
            if (part == null)
            {
                return false;
            }

            return ContainsKeyword(part.PartNo, keyword) ||
                   ContainsKeyword(part.PartName, keyword) ||
                   ContainsKeyword(part.CategoryCode, keyword) ||
                   ContainsKeyword(part.CategoryDescription, keyword) ||
                   ContainsKeyword(part.Memo, keyword);
        }

        private PartSearchCriteria BuildPartSearchCriteria()
        {
            PartSearchCriteria criteria = new PartSearchCriteria();
            criteria.PartNo = SearchPartNo;
            criteria.PartName = SearchPartName;
            criteria.CategoryCode = SearchCategoryCode;
            criteria.CategoryDescription = SearchCategoryDescription;
            return criteria;
        }

        private bool ContainsKeyword(string source, string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return source.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 측정부를 넣을 카메라입니다. 화면 위쪽 탭에서 고릅니다.
        ///
        /// <para>
        /// 측정부는 카메라마다 따로 관리하고 개수도 각각 셉니다.
        /// 이 값이 바뀌면 아래 목록과 좌표 이미지가 함께 그 카메라의 것으로 바뀝니다.
        /// </para>
        /// </summary>
        public ImageViewType SelectedMeasurementViewType
        {
            get { return _selectedMeasurementViewType; }
            set
            {
                if (SetProperty(ref _selectedMeasurementViewType, value))
                {
                    OnPropertyChanged("VisibleMeasurementPoints");
                    OnPropertyChanged("MeasurementPointCountText");
                    RefreshRegistrationCoordinateImage();
                }
            }
        }

        /// <summary>측정부를 둘 수 있는 카메라 목록입니다. 화면 탭이 이 목록을 씁니다.</summary>
        public IList<ImageViewType> MeasurementViewTypes
        {
            get { return MeasurementPointPolicy.GetSupportedViewTypes(); }
        }

        /// <summary>
        /// 지금 고른 카메라의 측정부만 모아 보여줍니다.
        /// </summary>
        public IList<MeasurementPointViewModel> VisibleMeasurementPoints
        {
            get
            {
                IList<MeasurementPointViewModel> visible = new List<MeasurementPointViewModel>();
                foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
                {
                    if (point != null && point.ViewType == SelectedMeasurementViewType)
                    {
                        visible.Add(point);
                    }
                }

                return visible;
            }
        }

        /// <summary>이 카메라에 몇 개를 넣었는지 알려 줍니다.</summary>
        public string MeasurementPointCountText
        {
            get
            {
                return CountMeasurementPoints(SelectedMeasurementViewType).ToString(CultureInfo.InvariantCulture) +
                       " / " + MeasurementPointPolicy.MaxCount.ToString(CultureInfo.InvariantCulture);
            }
        }

        private int CountMeasurementPoints(ImageViewType viewType)
        {
            int count = 0;
            foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
            {
                if (point != null && point.ViewType == viewType)
                {
                    count++;
                }
            }

            return count;
        }

        private void ExecuteAddMeasurementPoint(object parameter)
        {
            ImageViewType viewType = SelectedMeasurementViewType;

            // 개수는 카메라마다 따로 셉니다. Top이 가득 차도 Thickness는 계속 넣을 수 있습니다.
            if (CountMeasurementPoints(viewType) >= MeasurementPointPolicy.MaxCount)
            {
                RegistrationMessage = MeasurementPointPolicy.GetViewShortName(viewType) +
                                      " 측정부는 최대 " + MeasurementPointPolicy.MaxCount.ToString() +
                                      "개까지만 추가할 수 있습니다.";
                _messageDialogService.ShowWarning("측정부 추가 제한", RegistrationMessage);
                return;
            }

            MeasurementPointViewModel point = new MeasurementPointViewModel();
            point.ViewType = viewType;

            // 번호도 카메라마다 1부터 셉니다.
            point.ApplyIndex(CountMeasurementPoints(viewType) + 1);
            point.LineColor = MeasurementPointViewModel.GetDefaultColor(point.IndexNo);
            RegistrationMeasurementPoints.Add(point);
            SelectedRegistrationMeasurementPoint = point;
            OnPropertyChanged("VisibleMeasurementPoints");
            OnPropertyChanged("MeasurementPointCountText");
            RegistrationMessage = point.PointName + "를 추가했습니다.";
        }

        private void ExecuteRemoveMeasurementPoint(object parameter)
        {
            MeasurementPointViewModel point = parameter as MeasurementPointViewModel;
            if (point == null)
            {
                point = SelectedRegistrationMeasurementPoint;
            }

            if (point == null)
            {
                RegistrationMessage = "삭제할 측정부를 선택하세요.";
                return;
            }

            ImageViewType removedViewType = point.ViewType;
            RegistrationMeasurementPoints.Remove(point);

            // 번호는 카메라 안에서만 다시 이어 붙입니다.
            // AI에는 이미지 한 장마다 1부터 순차로 보내야 하기 때문입니다.
            ReindexMeasurementPoints(removedViewType);

            IList<MeasurementPointViewModel> remaining = VisibleMeasurementPoints;
            SelectedRegistrationMeasurementPoint = remaining.Count > 0
                ? remaining[Math.Min(point.IndexNo - 1, remaining.Count - 1)]
                : null;

            OnPropertyChanged("VisibleMeasurementPoints");
            OnPropertyChanged("MeasurementPointCountText");
            RegistrationMessage = MeasurementPointPolicy.GetViewShortName(removedViewType) +
                                  " 측정부를 삭제하고 이후 번호를 다시 정렬했습니다.";
        }

        /// <summary>
        /// 카메라 안에서 번호를 1부터 다시 이어 붙입니다.
        ///
        /// <para>
        /// 중간을 지우면 번호가 비는데, AI에는 이미지 한 장마다 1부터 순차로 보내야 합니다.
        /// 카메라를 넘나들며 번호를 매기면 Top이 1·3번처럼 흩어지므로 카메라 안에서만 셉니다.
        /// </para>
        /// </summary>
        private void ReindexMeasurementPoints(ImageViewType viewType)
        {
            int index = 1;
            foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
            {
                if (point == null || point.ViewType != viewType)
                {
                    continue;
                }

                point.ApplyIndex(index);
                index++;
            }
        }

        /// <summary>모든 카메라의 번호를 정리합니다. 목록을 새로 불러온 뒤에 씁니다.</summary>
        private void ReindexMeasurementPoints()
        {
            foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                ReindexMeasurementPoints(viewType);
            }
        }

        private void ExecuteEditMeasurementPosition(object parameter)
        {
            MeasurementPointViewModel point = parameter as MeasurementPointViewModel;
            if (point == null)
            {
                point = SelectedRegistrationMeasurementPoint;
            }

            if (point == null)
            {
                RegistrationMessage = "위치를 지정할 측정부를 선택하세요.";
                return;
            }

            // 선을 긋는 배경은 그 측정부가 속한 카메라의 기준 이미지입니다.
            ImageViewType viewType = point.ViewType;
            ImageEditViewModel backgroundImage = FindRegistrationImageByViewType(viewType);
            if (backgroundImage == null || string.IsNullOrWhiteSpace(backgroundImage.FilePath))
            {
                RegistrationMessage = viewType.ToString() +
                                      " 이미지가 없어서 측정부 위치를 등록할 수 없습니다.";
                _messageDialogService.ShowWarning(viewType.ToString() + " 이미지 필요", RegistrationMessage);
                return;
            }

            IList<MeasurementPointViewModel> allPoints = new List<MeasurementPointViewModel>();
            foreach (MeasurementPointViewModel registeredPoint in RegistrationMeasurementPoints)
            {
                allPoints.Add(registeredPoint);
            }

            // 창 안에서 다른 측정부로 옮겨 다닐 수 있으므로 카메라별 기준 이미지를 모두 넘깁니다.
            // 카메라가 다른 측정부로 옮기면 그 카메라의 사진이 배경이 되어야 합니다.
            IDictionary<ImageViewType, string> imagePathByViewType = new Dictionary<ImageViewType, string>();
            foreach (ImageViewType measurementViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                ImageEditViewModel imageForView = FindRegistrationImageByViewType(measurementViewType);
                if (imageForView != null && !string.IsNullOrWhiteSpace(imageForView.FilePath))
                {
                    imagePathByViewType[measurementViewType] = imageForView.FilePath;
                }
            }

            // 어느 사진을 배경으로 열었는지 남깁니다.
            // 옛 사진이 나왔다는 말이 나왔을 때 무엇을 열었는지 알 길이 없었습니다.
            AppendMeasurementPositionLog(point, imagePathByViewType);

            // 배경으로 쓸 수 있는 사진이 하나도 없으면 창 자체가 열리지 않습니다.
            // 취소와 구분해 알리려고 미리 봐 둡니다.
            bool hasBackgroundImage = imagePathByViewType.ContainsKey(viewType);

            bool isSaved;
            try
            {
                isSaved = _measurementPositionDialogService.Show(imagePathByViewType, point, allPoints);
            }
            catch (Exception ex)
            {
                RegistrationMessage = viewType.ToString() + " 이미지 위치 지정 창을 열 수 없습니다. " + ex.Message;
                _messageDialogService.ShowWarning("측정부 위치 지정 실패", RegistrationMessage);
                return;
            }

            if (isSaved)
            {
                Part coordinatePart = BuildRegistrationImagePart();
                string coordinateErrorMessage;
                if (!TrySaveTemporaryCoordinateImage(coordinatePart, out coordinateErrorMessage))
                {
                    RegistrationMessage = point.PointName + " 위치 좌표는 적용했지만 coordinate 이미지를 저장하지 못했습니다. " + coordinateErrorMessage;
                    _messageDialogService.ShowWarning("coordinate 이미지 저장 실패", RegistrationMessage);
                    return;
                }

                // 창 안에서 여러 측정부를 옮겨 다니며 고쳤을 수 있어 한 측정부 이름만 적지 않습니다.
                RegistrationMessage = "창에서 지정한 측정부 위치와 선 색상을 적용하고 모든 측정부 선을 coordinate 이미지에 저장했습니다.";
            }
            else if (!hasBackgroundImage)
            {
                RegistrationMessage = viewType.ToString() +
                                      " 카메라의 기준 이미지를 찾지 못해 측정부 위치 지정 창을 열지 못했습니다.";
            }
            else
            {
                // 취소는 잘못된 일이 아닙니다. 경고처럼 적으면 무언가 실패한 것으로 읽힙니다.
                RegistrationMessage = point.PointName + " 측정부 위치 지정을 취소했습니다. 기존 좌표는 그대로입니다.";
            }
        }

        /// <summary>
        /// 측정부 위치 창이 배경으로 삼은 사진을 카메라마다 남깁니다. 파일이 만들어진 시각도 함께 적습니다.
        /// </summary>
        private static void AppendMeasurementPositionLog(
            MeasurementPointViewModel point,
            IDictionary<ImageViewType, string> imagePathByViewType)
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
                builder.Append(" [위치표시] 측정부=");
                builder.Append(point == null ? "-" : point.PointName);
                builder.Append(" 카메라=");
                builder.Append(point == null ? "-" : point.ViewType.ToString());
                builder.AppendLine();

                foreach (KeyValuePair<ImageViewType, string> pair in imagePathByViewType)
                {
                    builder.Append("    ");
                    builder.Append(pair.Key.ToString());
                    builder.Append(" = ");
                    builder.Append(pair.Value);

                    try
                    {
                        if (File.Exists(pair.Value))
                        {
                            FileInfo info = new FileInfo(pair.Value);
                            builder.Append("  (만든 때 ");
                            builder.Append(info.LastWriteTime.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                            builder.Append(", ");
                            builder.Append((info.Length / 1024).ToString(CultureInfo.InvariantCulture));
                            builder.Append("KB)");
                        }
                        else
                        {
                            builder.Append("  (파일 없음)");
                        }
                    }
                    catch (IOException)
                    {
                    }

                    builder.AppendLine();
                }

                string logFilePath = AI.Vision.IOInspector.Infrastructure.ApplicationLogFileResolver
                    .GetLogFilePath(AppContext.BaseDirectory, "measurement-position");
                File.AppendAllText(logFilePath, builder.ToString());
            }
            catch
            {
                // 기록하려다 창이 안 열리면 안 됩니다.
            }
        }

        private void ExecuteAddReferenceImage(object parameter)
        {
            if (string.IsNullOrWhiteSpace(RegistrationPartNo) || string.IsNullOrWhiteSpace(RegistrationPartName))
            {
                RegistrationMessage = "이미지를 추가하기 전에 품번과 품명을 입력하세요.";
                return;
            }

            if (string.IsNullOrWhiteSpace(RegistrationCategoryCode))
            {
                RegistrationMessage = "이미지를 추가하기 전에 분류코드를 입력하세요.";
                return;
            }

            ImageViewType selectedViewType = ResolveSelectedReferenceImageViewType();
            ImageEditViewModel existingImageViewModel = FindRegistrationImageByViewType(selectedViewType);
            if (existingImageViewModel == null && RegistrationImages.Count >= ReferenceImageViewTypes.Count)
            {
                RegistrationMessage = "기준 이미지는 Top/Front/Back/Left/Right/Thickness 최대 6개까지만 등록할 수 있습니다.";
                return;
            }

            string sourceFilePath = _fileDialogService.SelectImageFile();
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                RegistrationMessage = "이미지 추가가 취소되었습니다.";
                return;
            }

            string validationMessage;
            if (!CanLoadReferenceImage(sourceFilePath, out validationMessage))
            {
                RegistrationMessage = validationMessage;
                return;
            }

            Part tempPart = new Part();
            tempPart.PartNo = RegistrationPartNo;
            tempPart.PartName = RegistrationPartName;
            tempPart.CategoryCode = RegistrationCategoryCode;
            PartImage image;
            try
            {
                // 부품등록 화면에서 파일을 골라 넣는 경로입니다.
                // 이쪽은 한 장씩 추가하므로 그 시점의 시각과 다음 벌 번호를 씁니다.
                image = _referenceImageFileService.AddReferenceImage(
                    tempPart,
                    sourceFilePath,
                    selectedViewType,
                    ReferenceImageFileNamePolicy.ResolveNextSetNo(BuildRegistrationPartImages()),
                    DateTime.Now);
            }
            catch (IOException ex)
            {
                RegistrationMessage = "이미지 파일을 추가할 수 없습니다. 파일이 열려 있거나 저장 위치에 접근할 수 없습니다. 상세: " + ex.Message;
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                RegistrationMessage = "이미지 파일 추가 권한이 없습니다. 원본 파일 또는 DB\\Image 폴더 권한을 확인하세요. 상세: " + ex.Message;
                return;
            }

            if (existingImageViewModel != null)
            {
                int existingIndex = FindRegistrationImageIndex(existingImageViewModel);
                int order = existingIndex >= 0 && existingIndex < RegistrationImages.Count ? existingIndex + 1 : RegistrationImages.Count + 1;
                ImageEditViewModel updatedImage = new ImageEditViewModel(image, order);
                if (existingIndex >= 0 && existingIndex < RegistrationImages.Count)
                {
                    RegistrationImages[existingIndex] = updatedImage;
                }
                else
                {
                    RegistrationImages.Add(updatedImage);
                }

                SelectedRegistrationImage = updatedImage;
                ReorderRegistrationImages(image);
                RegistrationMessage = "기준 이미지를 " + image.ViewType.ToString() + " 위치로 교체했습니다.";
                return;
            }

            ImageEditViewModel newImageViewModel = new ImageEditViewModel(image, RegistrationImages.Count + 1);
            RegistrationImages.Add(newImageViewModel);
            SelectedRegistrationImage = newImageViewModel;
            ReorderRegistrationImages(image);
            RegistrationMessage = "기준 이미지를 " + image.ViewType.ToString() + " 위치로 추가했습니다.";
        }

        /// <summary>
        /// 버튼을 누른 시점의 활성 카메라 프레임을 새로 캡처한 뒤 기준 이미지로 저장합니다.
        /// 화면에 남아 있는 이전 LiveImagePath를 재사용하지 않아 검사/등록 시점이 섞이지 않게 합니다.
        /// </summary>
        private void ExecuteSaveCurrentCameraImages(object parameter)
        {
            // 검사 탭의 "기준 이미지 저장"은 부품등록 탭의 "이미지 저장"과 같은 Command를 재사용하지만,
            // 촬영 직후 곧바로 DB에 반영되어야 합니다(부품등록 탭은 Temp 임시저장 후 별도 "DB 저장" 필요).
            bool isInspectionImmediateCommit = string.Equals(parameter as string, "Inspection", StringComparison.Ordinal);
            if (isInspectionImmediateCommit)
            {
                if (SelectedPart == null)
                {
                    RegistrationMessage = "검사 중인 부품이 선택되지 않았습니다. 먼저 부품을 선택하세요.";
                    return;
                }

                // 등록 화면 상태(RegistrationPartNo/측정부 등)는 검사 중인 부품과 별개로 관리되어
                // 왔습니다. 동기화하지 않고 저장하면 검사 중인 부품이 아니라 직전에 부품등록
                // 탭에서 열려 있던 다른 부품을 대상으로 저장되어 그 부품의 기존 측정부/이미지가
                // 유실될 위험이 있습니다. 부품등록 탭에서 이 부품을 직접 선택한 것과 동일하게
                // 동기화한 뒤 진행합니다.
                //
                // 다만 같은 부품이 이미 올라와 있으면 다시 불러오지 않습니다. 다시 불러오면
                // 화면에서 편집 중이던 측정부가 DB 내용으로 덮여 사라집니다.
                if (SelectedRegistrationPart == null ||
                    !IsSamePartNo(SelectedRegistrationPart.PartNo, SelectedPart.PartNo))
                {
                    SelectedRegistrationPart = SelectedPart;
                }
            }

            if (string.IsNullOrWhiteSpace(RegistrationPartNo) || string.IsNullOrWhiteSpace(RegistrationPartName))
            {
                RegistrationMessage = "현재 화면 이미지를 저장하려면 품번과 품명을 먼저 입력하세요.";
                return;
            }

            if (string.IsNullOrWhiteSpace(RegistrationCategoryCode))
            {
                RegistrationMessage = "현재 화면 이미지를 저장하려면 분류코드를 먼저 입력하세요.";
                return;
            }

            Part tempPart = new Part();
            tempPart.PartNo = RegistrationPartNo;
            tempPart.PartName = RegistrationPartName;
            tempPart.CategoryCode = RegistrationCategoryCode;
            tempPart.CategoryDescription = RegistrationCategoryDescription;
            tempPart.Memo = RegistrationMemo;

            try
            {
                // 재촬영은 현재 품번의 Temp 파일만 초기화합니다.
                // 최종 기준 이미지는 DB 저장 전까지 변경하지 않습니다.
                _referenceImageFileService.ClearTemporaryReferenceImages(tempPart);
                RestoreCommittedRegistrationImages(tempPart.PartNo);
            }
            catch (Exception ex)
            {
                RegistrationMessage = "임시 기준 이미지 폴더를 초기화하지 못했습니다. 상세: " + ex.Message;
                return;
            }

            RegistrationMessage = "현재 카메라 이미지를 촬영해 Temp 폴더에 저장중입니다.";

            // 기준 이미지 저장이 오래 걸린다는 말이 있어 구간마다 시간을 재 둡니다.
            // 어느 구간에서 시간을 쓰는지 로그가 없으면 짐작밖에 할 수 없습니다.
            System.Diagnostics.Stopwatch totalWatch = System.Diagnostics.Stopwatch.StartNew();
            System.Diagnostics.Stopwatch stepWatch = System.Diagnostics.Stopwatch.StartNew();

            int captureFailureCount;
            string captureFailureMessage;
            IList<CapturedImage> capturedImages = CaptureCurrentImagesForReference(tempPart, isInspectionImmediateCommit, out captureFailureCount, out captureFailureMessage);
            AppendReferenceSaveLog("촬영", stepWatch, totalWatch);

            // 방금 찍은 사진으로 자를 자리를 새로 구합니다.
            //
            // 자리는 여태 화면을 그리는 김에 구했습니다. 그런데 검사 결과가 남아 있거나 찍어 둔
            // 사진을 띄워 두면 그리기가 멈추고 자리 갱신도 함께 멈춥니다. 그 상태에서 다시 찍으면
            // 제품이 옮겨졌는데도 예전 자리로 잘려, 미리보기에 아무것도 안 나왔습니다.
            RefreshCropRegionsFromCapturedImages(capturedImages);
            AppendReferenceSaveLog("자를 자리 갱신", stepWatch, totalWatch);
            if (capturedImages.Count == 0)
            {
                RegistrationMessage = "저장할 현재 카메라 이미지 파일이 없습니다. 카메라 연결 상태를 확인하세요." + captureFailureMessage;
                return;
            }

            int savedCount = 0;
            int skippedCount = captureFailureCount;
            ImageEditViewModel lastSavedImageViewModel = null;
            ImageViewType[] viewOrder = GetReferenceImageViewOrder();
            for (int index = 0; index < viewOrder.Length; index++)
            {
                ImageViewType viewType = viewOrder[index];
                CapturedImage capturedImage = FindCapturedImageByViewType(capturedImages, viewType);
                if (!IsCapturedImageFileReady(capturedImage))
                {
                    skippedCount++;
                    continue;
                }

                ImageEditViewModel existingImageViewModel = FindRegistrationImageByViewType(viewType);
                try
                {
                    PartImage stagedImage = _referenceImageFileService.StageReferenceImage(tempPart, capturedImage.FilePath, viewType);
                    UpsertRegistrationImage(stagedImage, existingImageViewModel, out lastSavedImageViewModel);
                    // 검사 화면의 여섯 칸은 검사 탭에서 찍었을 때만 건드립니다.
                    //
                    // 부품등록 탭에서 찍은 것까지 검사 화면에 올리면, DB에 저장하지도 않았는데
                    // 검사 화면이 영상 대신 임시 사진과 "TEMP" 글자로 덮입니다.
                    // 부품등록 탭에는 이 칸이 없으므로 여기서 반영할 까닭도 없습니다.
                    if (isInspectionImmediateCommit)
                    {
                        ApplyStagedReferenceImageToSlot(stagedImage);
                    }
                    savedCount++;
                }
                catch (IOException)
                {
                    skippedCount++;
                }
                catch (UnauthorizedAccessException)
                {
                    skippedCount++;
                }
            }

            AppendReferenceSaveLog("Temp 저장", stepWatch, totalWatch);

            if (savedCount > 0)
            {
                ReorderRegistrationImages(lastSavedImageViewModel == null ? null : lastSavedImageViewModel.Image);
            }

            if (savedCount == 0)
            {
                RegistrationMessage = "저장할 현재 카메라 이미지 파일이 없습니다. 카메라 연결 상태와 저장 권한을 확인하세요." + captureFailureMessage;
                return;
            }

            // 다시 찍어도 측정부는 지우지 않습니다. 새 사진과 좌표가 어긋날 수 있지만 그 판단은
            // 작업자 몫입니다. 대신 방금 찍은 사진 위에 지금 측정부 선을 다시 그려, 좌표 이미지가
            // 새 사진을 보여 주도록 합니다.
            //
            // 촬영 직전 ClearTemporaryReferenceImages가 Temp 폴더를 통째로 비우면서 작업 중이던
            // 좌표 이미지도 함께 지워집니다. 다시 만들지 않으면 화면이 예전에 저장한 좌표 이미지로
            // 되돌아가 측정부가 사라진 것처럼 보입니다.
            RefreshCoordinateImagesAfterCapture();
            AppendReferenceSaveLog("좌표 이미지 다시 그리기", stepWatch, totalWatch);

            if (isInspectionImmediateCommit)
            {
                // 부품등록 탭의 "DB 저장"과 완전히 동일하게 동작시킵니다. ExecuteSavePart는 저장 후
                // 같은 부품이 검사 탭에 선택되어 있으면 그 화면도 함께 새로고침하므로, 검사 탭에서
                // 저장한 새 기준 이미지와 부품등록 탭 표시 내용이 서로 어긋나지 않습니다.
                ExecuteSavePart(parameter);
                AppendReferenceSaveLog("DB 저장", stepWatch, totalWatch);
                return;
            }

            AppendReferenceSaveLog("마무리", stepWatch, totalWatch);
            RegistrationMessage = "현재 카메라 이미지 " + savedCount.ToString() +
                                  "개를 Temp에 임시 저장했습니다. DB 저장을 누르면 최종 이미지 폴더와 DB에 반영됩니다. 저장 제외 " +
                                  skippedCount.ToString() + "개." + captureFailureMessage;
        }

        /// <summary>
        /// 방금 찍은 사진들로 카메라마다 자를 자리를 새로 구합니다.
        ///
        /// <para>
        /// 사진 한 장에 1 초쯤 걸립니다. 저장 버튼은 사람이 한 번씩 누르는 것이라 그만큼은
        /// 치를 만합니다. 실패해도 저장 자체는 그대로 두고 넘어갑니다. 그때는 예전 자리로
        /// 잘리는데, 저장이 안 되는 것보다는 낫습니다.
        /// </para>
        /// </summary>
        private void RefreshCropRegionsFromCapturedImages(IList<CapturedImage> capturedImages)
        {
            if (capturedImages == null || capturedImages.Count == 0)
            {
                return;
            }

            foreach (CapturedImage image in capturedImages)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                try
                {
                    int monitorIndex = RtspMonitorIndexPolicy.FromViewType(image.ViewType);
                    AI.Vision.IOInspector.Vision.Services.CallbackFrameCropStage
                        .TryUpdateRegionFromFile(monitorIndex, image.FilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        image.ViewType + " 자를 자리를 구하지 못했습니다. " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 기준 이미지 저장의 한 구간이 얼마나 걸렸는지 남깁니다.
        ///
        /// <para>
        /// 재고 나면 그 구간의 시계를 다시 돌립니다. 그래서 부를 때마다 직전 구간의
        /// 시간이 나오고, 마지막에 전체 시간과 견주어 어디에서 시간을 썼는지 알 수 있습니다.
        /// </para>
        /// </summary>
        private static void AppendReferenceSaveLog(
            string stepName,
            System.Diagnostics.Stopwatch stepWatch,
            System.Diagnostics.Stopwatch totalWatch)
        {
            try
            {
                long stepMilliseconds = stepWatch.ElapsedMilliseconds;
                stepWatch.Restart();

                string logFilePath = AI.Vision.IOInspector.Infrastructure.ApplicationLogFileResolver
                    .GetLogFilePath(AppContext.BaseDirectory, "reference-save");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
                              " [" + stepName + "] " +
                              stepMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms, 누적 " +
                              totalWatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture) + "ms" +
                              Environment.NewLine;
                System.IO.File.AppendAllText(logFilePath, line);
            }
            catch
            {
                // 시간을 재려다 저장을 막으면 안 됩니다.
            }
        }

        /// <summary>
        /// 지금 카메라 화면을 기준 이미지로 쓰려고 한 장씩 찍습니다.
        /// </summary>
        /// <param name="updateInspectionSlots">
        /// 검사 화면의 여섯 칸에 찍은 사진을 올릴지입니다.
        /// 부품등록 탭에서 찍을 때는 올리면 안 됩니다. 그 칸은 검사 화면의 것이라,
        /// 올리면 영상이 정지 사진으로 덮여 카메라를 볼 수 없게 됩니다.
        /// </param>
        private IList<CapturedImage> CaptureCurrentImagesForReference(
            Part part, bool updateInspectionSlots, out int failureCount, out string failureMessage)
        {
            IList<CapturedImage> capturedImages = new List<CapturedImage>();
            failureCount = 0;
            StringBuilder failureBuilder = new StringBuilder();

            IList<CameraChannelConfig> channels;
            try
            {
                channels = _cameraService.GetChannelConfigurations();
            }
            catch (Exception ex)
            {
                failureCount++;
                failureMessage = " 카메라 설정 읽기 실패: " + TrimLivePreviewMessage(ex.Message);
                return capturedImages;
            }

            // 채널을 하나씩 순서대로 Capture()하면 특정 채널이 응답을 늦게 주거나 멈출 때 나머지 채널까지
            // 함께 대기하게 되어 "몇 장만 캡처되고 멈추는" 것처럼 보입니다. CaptureAll()로 한 번에 요청해
            // 이 문제를 피합니다(라이브 프리뷰 캡처에 적용한 것과 동일한 방식).
            // 6채널 동시 RTSP 디코딩 부하로 VLAD 프레임 캐시가 일시적으로 몇 초 밀리는 경우가 있어,
            // 아직 못 찍은 채널만 짧은 간격으로 최대 3회까지 자동 재시도합니다. 기준 이미지 저장은
            // 사용자가 명시적으로 누르는 동작이라 몇 초 더 기다리더라도 6장을 다 채우는 쪽이 낫습니다.
            const int maxCaptureAttempts = 3;
            IDictionary<ImageViewType, CapturedImage> capturedByViewType = new Dictionary<ImageViewType, CapturedImage>();
            string lastCaptureFailureMessage = string.Empty;

            for (int attempt = 1; attempt <= maxCaptureAttempts; attempt++)
            {
                IList<CapturedImage> allCapturedImages;
                try
                {
                    allCapturedImages = _cameraService.CaptureAll(part);
                }
                catch (Exception ex)
                {
                    allCapturedImages = new List<CapturedImage>();
                    lastCaptureFailureMessage = " 카메라 프레임 수신 실패: " + TrimLivePreviewMessage(ex.Message);
                }

                foreach (CapturedImage image in allCapturedImages)
                {
                    capturedByViewType[image.ViewType] = image;
                }

                bool allEnabledChannelsCaptured = true;
                foreach (CameraChannelConfig channel in channels)
                {
                    if (channel == null || !channel.IsEnabled)
                    {
                        continue;
                    }

                    CapturedImage capturedImage;
                    if (!capturedByViewType.TryGetValue(channel.ViewType, out capturedImage) || !IsCapturedImageFileReady(capturedImage))
                    {
                        allEnabledChannelsCaptured = false;
                        break;
                    }
                }

                if (allEnabledChannelsCaptured || attempt == maxCaptureAttempts)
                {
                    break;
                }

                System.Threading.Thread.Sleep(500);
            }

            if (capturedByViewType.Count == 0 && !string.IsNullOrEmpty(lastCaptureFailureMessage))
            {
                failureCount++;
                failureMessage = lastCaptureFailureMessage;
                return capturedImages;
            }

            IDictionary<ImageViewType, CameraChannelStatus> statusesByViewType = new Dictionary<ImageViewType, CameraChannelStatus>();
            foreach (CameraChannelStatus status in _cameraService.GetChannelStatuses())
            {
                statusesByViewType[status.ViewType] = status;
            }

            foreach (CameraChannelConfig channel in channels)
            {
                if (channel == null || !channel.IsEnabled)
                {
                    continue;
                }

                CapturedImage capturedImage;
                if (capturedByViewType.TryGetValue(channel.ViewType, out capturedImage) && IsCapturedImageFileReady(capturedImage))
                {
                    capturedImages.Add(capturedImage);
                    if (updateInspectionSlots)
                    {
                        ApplyCapturedImageToSlot(capturedImage, "기준 저장용 촬영 완료", "CAPTURE", "#128A45");
                    }

                    continue;
                }

                failureCount++;
                CameraChannelStatus channelStatus;
                string failureReason = "캡처 파일이 생성되지 않았습니다.";
                if (statusesByViewType.TryGetValue(channel.ViewType, out channelStatus) && !string.IsNullOrWhiteSpace(channelStatus.Message))
                {
                    failureReason = channelStatus.Message;
                }
                AppendCaptureFailureMessage(failureBuilder, channel.DisplayName, failureReason);
            }

            failureMessage = failureBuilder.Length == 0 ? string.Empty : " 실패: " + failureBuilder.ToString();
            return capturedImages;
        }

        private void ApplyCapturedImageToSlot(CapturedImage image, string statusText, string resultText, string resultBrush)
        {
            if (image == null)
            {
                return;
            }

            int imageSlotIndex = GetImageViewTypeSortOrder(image.ViewType);
            if (imageSlotIndex >= ImageSlots.Count)
            {
                return;
            }

            ImageSlotViewModel slot = ImageSlots[imageSlotIndex];
            slot.LiveImagePath = image.FilePath;
            slot.IsCapturedStillVisible = true;
            slot.StatusText = statusText;
            slot.ResultText = resultText;
            slot.ResultBrush = resultBrush;
        }

        private void ApplySavedReferenceImageToSlot(PartImage image)
        {
            if (image == null)
            {
                return;
            }

            int imageSlotIndex = GetImageViewTypeSortOrder(image.ViewType);
            if (imageSlotIndex >= ImageSlots.Count)
            {
                return;
            }

            ImageSlotViewModel slot = ImageSlots[imageSlotIndex];
            slot.ReferenceImagePath = image.FilePath;
            slot.LiveImagePath = image.FilePath;
            slot.IsCapturedStillVisible = true;
            slot.StatusText = "기준 이미지 저장";
            slot.ResultText = "REF";
            slot.ResultBrush = "#128A45";
        }

        private void ApplyStagedReferenceImageToSlot(PartImage image)
        {
            if (image == null)
            {
                return;
            }

            int imageSlotIndex = GetImageViewTypeSortOrder(image.ViewType);
            if (imageSlotIndex >= ImageSlots.Count)
            {
                return;
            }

            ImageSlotViewModel slot = ImageSlots[imageSlotIndex];
            slot.ReferenceImagePath = image.FilePath;
            slot.LiveImagePath = image.FilePath;
            slot.IsCapturedStillVisible = true;
            slot.StatusText = "기준 이미지 임시 저장";
            slot.ResultText = "TEMP";
            slot.ResultBrush = "#E69F00";
        }

        private void RestoreCommittedRegistrationImages(string partNo)
        {
            Part committedPart = _partDataStore.GetPart(partNo);
            if (committedPart != null)
            {
                LoadRegistrationImages(committedPart);
                return;
            }

            RegistrationImages.Clear();
            SelectedRegistrationImage = null;
            RegistrationCoordinateImagePath = string.Empty;
            RefreshRegistrationImagePreviews();
        }

        private Part BuildRegistrationImagePart()
        {
            Part part = new Part();
            part.PartNo = RegistrationPartNo;
            part.PartName = RegistrationPartName;
            part.CategoryCode = RegistrationCategoryCode;
            part.CategoryDescription = RegistrationCategoryDescription;
            part.Memo = RegistrationMemo;
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                part.Images.Add(imageViewModel.Image);
            }

            return part;
        }

        /// <summary>
        /// 측정부 선을 그린 좌표 이미지를 카메라마다 한 장씩 만듭니다.
        ///
        /// <para>
        /// 측정부를 카메라별로 두므로 Thickness 한 장만 만들면, 화면에서 Top으로 바꿔도
        /// Thickness 그림이 그대로 보입니다. 카메라마다 배경도 좌표계도 다릅니다.
        /// </para>
        ///
        /// <para>
        /// 그 카메라에 측정부가 없으면 남아 있던 좌표 이미지를 지웁니다.
        /// 지우지 않으면 측정부를 모두 뺀 뒤에도 옛 선이 남아 보입니다.
        /// </para>
        /// </summary>
        /// <summary>
        /// 다시 촬영한 직후, 화면에 남아 있는 측정부를 새 사진 위에 다시 그립니다.
        /// 측정부 목록 자체는 건드리지 않습니다.
        /// </summary>
        private void RefreshCoordinateImagesAfterCapture()
        {
            if (RegistrationMeasurementPoints.Count == 0)
            {
                return;
            }

            Part coordinatePart;
            string buildErrorMessage;
            if (!TryBuildRegistrationPart(out coordinatePart, out buildErrorMessage))
            {
                // 좌표를 아직 다 찍지 않은 측정부가 있을 수 있습니다. 촬영 자체는 이미 끝났으므로
                // 여기서 막지 않고, 부족한 값은 DB 저장 때 다시 확인합니다.
                return;
            }

            string coordinateErrorMessage;
            TrySaveTemporaryCoordinateImage(coordinatePart, out coordinateErrorMessage);
        }

        private bool TrySaveTemporaryCoordinateImage(Part part, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
                {
                    if (!TrySaveTemporaryCoordinateImageForView(part, viewType, out errorMessage))
                    {
                        return false;
                    }
                }

                // 화면에는 지금 고른 카메라의 것을 보여 줍니다.
                RegistrationCoordinateImagePath = ResolveRegistrationCoordinateImagePath(part);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "coordinate 이미지를 생성할 수 없습니다. " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 그 카메라의 기준 이미지를 고릅니다. 벌이 여러 개면 가장 최근 것을 씁니다.
        /// </summary>
        private PartImage FindPartImageByViewType(IList<PartImage> images, ImageViewType viewType)
        {
            return ReferenceImageFileNamePolicy.FindLatestByViewType(images, viewType);
        }

        /// <summary>
        /// 이 카메라에 측정부가 있으면 좌표 이미지 경로를 돌려줍니다.
        /// 측정부가 없거나 좌표를 아직 찍지 않았으면 빈 문자열이며, 호출한 쪽이 원본을 씁니다.
        /// </summary>
        private string ResolveSlotCoordinateImagePath(Part part, ImageViewType viewType)
        {
            if (part == null || part.MeasurementRegions == null)
            {
                return string.Empty;
            }

            bool hasRegion = false;
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region != null && region.ViewType == viewType)
                {
                    hasRegion = true;
                    break;
                }
            }

            if (!hasRegion)
            {
                return string.Empty;
            }

            return ResolveCommittedCoordinateImagePath(part, viewType);
        }

        /// <summary>
        /// 지금 고른 카메라의 좌표 이미지를 다시 잡습니다.
        /// 탭을 바꾸면 그 카메라의 좌표가 보여야 합니다.
        /// </summary>
        private void RefreshRegistrationCoordinateImage()
        {
            Part part = SelectedRegistrationPart == null ? null : SelectedRegistrationPart.Part;
            RegistrationCoordinateImagePath = ResolveRegistrationCoordinateImagePath(part);
        }

        /// <summary>
        /// 한 카메라의 좌표 이미지를 만듭니다.
        /// </summary>
        private bool TrySaveTemporaryCoordinateImageForView(Part part, ImageViewType viewType, out string errorMessage)
        {
            errorMessage = string.Empty;

            bool hasCoordinates = false;
            foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
            {
                if (point.ViewType == viewType && point.HasCoordinates)
                {
                    hasCoordinates = true;
                    break;
                }
            }

            if (!hasCoordinates)
            {
                _referenceImageFileService.DeleteTemporaryCoordinateImage(part, viewType);
                return true;
            }

            PartImage backgroundImage = FindPartImageByViewType(part.Images, viewType);
            if (backgroundImage == null ||
                string.IsNullOrWhiteSpace(backgroundImage.FilePath) ||
                !File.Exists(backgroundImage.FilePath))
            {
                errorMessage = "측정부 선을 저장할 " + viewType.ToString() + " 기준 이미지가 없습니다.";
                return false;
            }

            // 그 카메라의 측정부만 그립니다. 다른 카메라 선은 좌표계가 달라 엉뚱한 자리에 찍힙니다.
            IList<MeasurementPointViewModel> pointsForView = new List<MeasurementPointViewModel>();
            foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
            {
                if (point.ViewType == viewType)
                {
                    pointsForView.Add(point);
                }
            }

            string coordinatePath = _referenceImageFileService.GetTemporaryCoordinateImagePath(part, viewType);
            _referenceCoordinateImageService.SaveCoordinateImage(
                backgroundImage.FilePath,
                coordinatePath,
                pointsForView);
            return true;
        }

        private string ResolveRegistrationCoordinateImagePath(Part part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            // 작업 중인 임시 좌표가 있으면 그것을 먼저 보여줍니다.
            string temporaryPath = _referenceImageFileService.GetTemporaryCoordinateImagePath(
                part, SelectedMeasurementViewType);
            if (!string.IsNullOrWhiteSpace(temporaryPath) && File.Exists(temporaryPath))
            {
                return temporaryPath;
            }

            return ResolveCommittedCoordinateImagePath(part, SelectedMeasurementViewType);
        }

        /// <summary>
        /// 저장된 좌표 이미지 경로입니다. 카메라마다 한 장씩 있습니다.
        /// 예전 이름으로 남은 파일도 함께 찾습니다(ReferenceImageFileNamePolicy).
        /// </summary>
        private string ResolveCommittedCoordinateImagePath(Part part, ImageViewType viewType)
        {
            if (part == null)
            {
                return string.Empty;
            }

            // 이미지가 놓인 폴더를 알아내려고 그 카메라의 기준 이미지를 씁니다.
            PartImage referenceImage = FindPartImageByViewType(part.Images, viewType);
            if (referenceImage == null || string.IsNullOrWhiteSpace(referenceImage.FilePath))
            {
                return string.Empty;
            }

            string imageDirectoryPath = Path.GetDirectoryName(referenceImage.FilePath);
            return ReferenceImageFileNamePolicy.FindCoordinateFilePath(
                imageDirectoryPath, viewType, part.PartNo);
        }

        private bool HasTemporaryReferenceImages(IList<PartImage> images)
        {
            if (images == null)
            {
                return false;
            }

            foreach (PartImage image in images)
            {
                if (image != null && image.IsTemporary)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasTemporaryCoordinateImage(Part part)
        {
            if (part == null)
            {
                return false;
            }

            // 어느 카메라든 임시 좌표가 하나라도 있으면 확정할 것이 있다는 뜻입니다.
            foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                string coordinatePath = _referenceImageFileService.GetTemporaryCoordinateImagePath(part, viewType);
                if (!string.IsNullOrWhiteSpace(coordinatePath) && File.Exists(coordinatePath))
                {
                    return true;
                }
            }

            return false;
        }

        private CapturedImage FindCapturedImageByViewType(IList<CapturedImage> capturedImages, ImageViewType viewType)
        {
            foreach (CapturedImage image in capturedImages)
            {
                if (image.ViewType == viewType)
                {
                    return image;
                }
            }

            return null;
        }

        private bool IsCapturedImageFileReady(CapturedImage image)
        {
            return image != null && !string.IsNullOrWhiteSpace(image.FilePath) && File.Exists(image.FilePath);
        }

        private void AppendCaptureFailureMessage(StringBuilder builder, string displayName, string message)
        {
            if (builder.Length > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(string.IsNullOrWhiteSpace(displayName) ? "Camera" : displayName);
            builder.Append("=");
            builder.Append(message);
        }

        private void UpsertRegistrationImage(PartImage image, ImageEditViewModel existingImageViewModel, out ImageEditViewModel savedImageViewModel)
        {
            savedImageViewModel = null;
            ImageEditViewModel updatedImage = new ImageEditViewModel(image, RegistrationImages.Count + 1);
            if (existingImageViewModel != null)
            {
                int existingIndex = FindRegistrationImageIndex(existingImageViewModel);
                if (existingIndex >= 0 && existingIndex < RegistrationImages.Count)
                {
                    updatedImage = new ImageEditViewModel(image, existingIndex + 1);
                    RegistrationImages[existingIndex] = updatedImage;
                    savedImageViewModel = updatedImage;
                    return;
                }
            }

            RegistrationImages.Add(updatedImage);
            savedImageViewModel = updatedImage;
        }

        private bool CanLoadReferenceImage(string filePath, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                message = "이미지 파일을 찾을 수 없습니다.";
                return false;
            }

            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    BitmapImage image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                }

                return true;
            }
            catch (IOException ex)
            {
                message = "이미지 파일을 읽을 수 없습니다. 파일이 열려 있는지 확인하세요. 상세: " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                message = "이미지 파일을 읽을 권한이 없습니다. 상세: " + ex.Message;
                return false;
            }
            catch (ArgumentException ex)
            {
                message = "지원하지 않거나 손상된 이미지 파일입니다. PNG/JPG/JPEG/BMP 파일인지 확인하세요. 상세: " + ex.Message;
                return false;
            }
            catch (NotSupportedException ex)
            {
                message = "지원하지 않는 이미지 형식입니다. PNG/JPG/JPEG/BMP 파일을 선택하세요. 상세: " + ex.Message;
                return false;
            }
            catch (InvalidOperationException ex)
            {
                message = "이미지 파일을 해석할 수 없습니다. 파일이 손상되지 않았는지 확인하세요. 상세: " + ex.Message;
                return false;
            }
        }

        private ImageViewType ResolveSelectedReferenceImageViewType()
        {
            ImageViewType viewType;
            if (Enum.TryParse(SelectedReferenceImageViewType, out viewType))
            {
                return viewType;
            }

            return ImageViewType.Top;
        }

        private ImageEditViewModel FindRegistrationImageByViewType(ImageViewType viewType)
        {
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                if (imageViewModel.Image.ViewType == viewType)
                {
                    return imageViewModel;
                }
            }

            return null;
        }

        private int FindRegistrationImageIndex(ImageEditViewModel target)
        {
            for (int index = 0; index < RegistrationImages.Count; index++)
            {
                if (ReferenceEquals(RegistrationImages[index], target))
                {
                    return index;
                }
            }

            return -1;
        }

        private void ExecuteDeleteAllReferenceImages(object parameter)
        {
            if (_isDeletingAllReferenceImages)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(RegistrationPartNo))
            {
                RegistrationMessage = "이미지를 삭제할 품번이 없습니다.";
                return;
            }

            if (SelectedRegistrationPart != null &&
                !IsSamePartNo(SelectedRegistrationPart.PartNo, RegistrationPartNo))
            {
                RegistrationMessage =
                    "선택한 품목의 품번과 입력 품번이 다릅니다. 삭제할 품목을 다시 선택한 후 진행하세요.";
                _messageDialogService.ShowWarning("삭제 대상 품번 확인", RegistrationMessage);
                return;
            }

            _isDeletingAllReferenceImages = true;
            try
            {
                bool confirmed = _messageDialogService.ShowConfirmation(
                    "등록 이미지 및 측정부 정보 삭제",
                    BuildDeleteAllConfirmationMessage());
                if (!confirmed)
                {
                    RegistrationMessage = "이미지 및 측정부 정보 삭제를 취소했습니다.";
                    return;
                }

                DeleteAllReferenceImagesAndMeasurements();
            }
            finally
            {
                _isDeletingAllReferenceImages = false;
            }
        }

        /// <summary>
        /// 무엇이 지워지는지 그 품목의 실제 상태로 알려 줍니다.
        ///
        /// <para>
        /// 예전에는 "기준 이미지 6장과 coordinate 이미지 1장"이라고 못 박아 두었습니다.
        /// 기준 이미지는 저장할 때마다 벌이 쌓여 여섯 장을 넘고, 좌표 이미지도 카메라마다
        /// 한 장씩이라 두 장이 될 수 있습니다. 실제와 다른 수를 보여 주면 무엇이 지워지는지
        /// 잘못 짐작하게 됩니다.
        /// </para>
        /// </summary>
        private string BuildDeleteAllConfirmationMessage()
        {
            Part storedPart = _partDataStore.GetPart(RegistrationPartNo);
            IList<string> committedImageFolders = BuildCommittedReferenceImageFolderPaths(storedPart);
            int imageCount;
            IList<int> setNumbers;
            CountCommittedReferenceImages(committedImageFolders, out imageCount, out setNumbers);
            int coordinateCount = CountCommittedCoordinateImages(committedImageFolders);

            // 측정부는 카메라별로 나눠 셉니다.
            // 전체 개수만 보여 주면 어느 카메라의 것이 지워지는지 알 수 없습니다.
            Dictionary<ImageViewType, int> measurementCountByView = new Dictionary<ImageViewType, int>();
            foreach (ImageViewType measurementViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                measurementCountByView[measurementViewType] = 0;
            }

            int measurementCount = 0;
            if (storedPart != null && storedPart.MeasurementRegions != null)
            {
                foreach (MeasurementRegion region in storedPart.MeasurementRegions)
                {
                    if (region == null)
                    {
                        continue;
                    }

                    measurementCount++;
                    if (measurementCountByView.ContainsKey(region.ViewType))
                    {
                        measurementCountByView[region.ViewType]++;
                    }
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.Append(RegistrationPartNo);
            builder.AppendLine("의 등록 자료를 모두 삭제합니다.");
            builder.AppendLine();

            if (setNumbers.Count > 0)
            {
                builder.AppendLine(
                    "  기준 이미지 : " + setNumbers.Count.ToString(CultureInfo.InvariantCulture) + "벌 (" +
                    imageCount.ToString(CultureInfo.InvariantCulture) + "장, 저장한 벌이 모두 지워집니다)");
            }
            else
            {
                builder.AppendLine("  기준 이미지 : 없음");
            }

            builder.AppendLine(
                "  측정부 좌표 이미지 : " + coordinateCount.ToString(CultureInfo.InvariantCulture) +
                "장 / 총 " + MeasurementPointPolicy.GetSupportedViewTypes().Count.ToString(CultureInfo.InvariantCulture) +
                "장 (Top·Thk 각 1장)");
            if (measurementCount > 0)
            {
                List<string> measurementParts = new List<string>();
                foreach (ImageViewType measurementViewType in MeasurementPointPolicy.GetSupportedViewTypes())
                {
                    measurementParts.Add(
                        MeasurementPointPolicy.GetViewShortName(measurementViewType) + " " +
                        measurementCountByView[measurementViewType].ToString(CultureInfo.InvariantCulture) + "개");
                }

                builder.AppendLine(
                    "  삭제되는 측정부 : " + measurementCount.ToString(CultureInfo.InvariantCulture) + "개  (" +
                    string.Join(", ", measurementParts.ToArray()) + ")");
            }
            else
            {
                builder.AppendLine("  측정부 : 없음");
            }
            builder.AppendLine();
            builder.Append("계속 진행하시겠습니까?");

            return builder.ToString();
        }

        /// <summary>
        /// 삭제 안내는 DB에 연결된 마지막 한 벌이나 Temp가 아니라, 실제 최종 기준 이미지 폴더를 기준으로 합니다.
        /// </summary>
        private IList<string> BuildCommittedReferenceImageFolderPaths(Part storedPart)
        {
            IList<string> folderPaths = new List<string>();
            ISet<string> uniqueFolderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (storedPart == null || storedPart.Images == null)
            {
                return folderPaths;
            }

            foreach (PartImage image in storedPart.Images)
            {
                if (image == null || image.IsTemporary || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                string folderPath = Path.GetDirectoryName(image.FilePath);
                if (!string.IsNullOrWhiteSpace(folderPath) &&
                    !IsTemporaryReferenceFolderPath(folderPath) &&
                    uniqueFolderPaths.Add(folderPath))
                {
                    folderPaths.Add(folderPath);
                }
            }

            return folderPaths;
        }

        private bool IsTemporaryReferenceFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            try
            {
                string normalizedPath = Path.GetFullPath(folderPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string temporaryPathToken = Path.DirectorySeparatorChar + "Temp" + Path.DirectorySeparatorChar;
                return normalizedPath.IndexOf(temporaryPathToken, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception)
            {
                // 오래된 DB에 잘못된 경로가 있어도 삭제 확인창 자체는 열려야 합니다.
                return false;
            }
        }

        private void CountCommittedReferenceImages(
            IList<string> folderPaths,
            out int imageCount,
            out IList<int> setNumbers)
        {
            imageCount = 0;
            setNumbers = new List<int>();
            ISet<string> uniqueFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folderPath in folderPaths)
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    continue;
                }

                string[] filePaths;
                try
                {
                    filePaths = Directory.GetFiles(folderPath);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string filePath in filePaths)
                {
                    if (!uniqueFilePaths.Add(filePath))
                    {
                        continue;
                    }

                    ImageViewType ignoredViewType;
                    int setNo;
                    DateTime ignoredSavedAt;
                    if (!ReferenceImageFileNamePolicy.TryParseSavedImageFileName(
                            Path.GetFileName(filePath),
                            out ignoredViewType,
                            out setNo,
                            out ignoredSavedAt))
                    {
                        continue;
                    }

                    imageCount++;
                    if (!setNumbers.Contains(setNo))
                    {
                        setNumbers.Add(setNo);
                    }
                }
            }
        }

        private int CountCommittedCoordinateImages(IList<string> folderPaths)
        {
            ISet<ImageViewType> existingViewTypes = new HashSet<ImageViewType>();
            foreach (string folderPath in folderPaths)
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                {
                    continue;
                }

                foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
                {
                    string coordinatePath = ReferenceImageFileNamePolicy.FindCoordinateFilePath(
                        folderPath,
                        viewType,
                        RegistrationPartNo);
                    if (!string.IsNullOrWhiteSpace(coordinatePath) && File.Exists(coordinatePath))
                    {
                        existingViewTypes.Add(viewType);
                    }
                }
            }

            return existingViewTypes.Count;
        }

        private void ClearTemporaryReferenceImagesForCurrentRegistrationPart()
        {
            Part currentPart = BuildRegistrationImagePart();
            ClearTemporaryReferenceImagesForPart(currentPart);
        }

        private void ClearTemporaryReferenceImagesForPart(Part part)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.PartNo))
            {
                return;
            }

            try
            {
                _referenceImageFileService.ClearTemporaryReferenceImages(part);
            }
            catch (Exception ex)
            {
                RegistrationMessage = "임시 기준 이미지 정리 실패: " + ex.Message;
            }
        }

        private void DeleteAllReferenceImagesAndMeasurements()
        {
            Part storedPart = _partDataStore.GetPart(RegistrationPartNo);
            IList<PartImage> imagesToDelete = BuildReferenceImagesForDeletion(storedPart);
            IList<string> coordinatePathsToDelete = BuildCoordinatePathsForDeletion(imagesToDelete);

            if (storedPart != null)
            {
                Part clearedPart = BuildPartWithoutImagesAndMeasurements(storedPart);
                string saveMessage = _partDataStore.SavePart(clearedPart);
                if (saveMessage != PartCatalogService.SaveSuccessMessage)
                {
                    RegistrationMessage = "이미지 및 측정부 정보의 DB 삭제에 실패했습니다. " + saveMessage;
                    _messageDialogService.ShowWarning("이미지 삭제 실패", RegistrationMessage);
                    return;
                }
            }

            IList<string> deleteErrors = new List<string>();
            DeleteReferenceImageFiles(imagesToDelete, deleteErrors);
            DeleteCoordinateImageFiles(coordinatePathsToDelete, deleteErrors);

            // 목록에 있는 파일만 지우면 DB와 연결이 끊긴 파일이 폴더에 남습니다.
            // 저장할 때마다 벌이 쌓이는 구조에서는 그런 파일이 계속 늘어나므로
            // 마지막에 부품 폴더를 통째로 비웁니다.
            if (storedPart != null)
            {
                int folderDeletedCount;
                IList<string> folderErrors;
                _referenceImageFileService.DeleteAllReferenceImageFiles(
                    storedPart, out folderDeletedCount, out folderErrors);

                foreach (string folderError in folderErrors)
                {
                    deleteErrors.Add(folderError);
                }
            }
            string mergedImageDeleteMessage;
            if (_imageMergeService != null &&
                !_imageMergeService.TryDeleteReferenceMergedImage(
                    RegistrationPartNo,
                    imagesToDelete,
                    out mergedImageDeleteMessage))
            {
                deleteErrors.Add(mergedImageDeleteMessage);
            }
            ClearTemporaryRegistrationFiles(deleteErrors);

            RegistrationImages.Clear();
            SelectedRegistrationImage = null;
            RegistrationCoordinateImagePath = string.Empty;
            RegistrationMeasurementPoints.Clear();
            SelectedRegistrationMeasurementPoint = null;
            RefreshRegistrationImagePreviews();

            RefreshPartCollectionsFromDataStore();
            if (deleteErrors.Count > 0)
            {
                RegistrationMessage =
                    "DB의 기준 이미지 및 측정부 정보는 삭제했지만 일부 이미지 파일을 삭제하지 못했습니다. " +
                    string.Join(" | ", deleteErrors);
                _messageDialogService.ShowWarning("일부 이미지 파일 삭제 실패", RegistrationMessage);
                return;
            }

            RegistrationMessage = "등록 기준 이미지, 품번 병합 이미지, coordinate 이미지, 측정부 정보를 모두 삭제했습니다.";
            PromptImageTrainingAfterImageChange("DB 기준 이미지가 삭제되었습니다.");
        }

        private Part BuildPartWithoutImagesAndMeasurements(Part source)
        {
            Part part = new Part();
            part.PartNo = source.PartNo;
            part.PartName = source.PartName;
            part.CategoryCode = source.CategoryCode;
            part.CategoryDescription = source.CategoryDescription;
            part.Memo = source.Memo;
            part.CreatedAt = source.CreatedAt;
            part.UpdatedAt = source.UpdatedAt;
            return part;
        }

        private IList<PartImage> BuildReferenceImagesForDeletion(Part storedPart)
        {
            IList<PartImage> images = new List<PartImage>();
            ISet<string> filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                AddReferenceImageForDeletion(images, filePaths, imageViewModel.Image);
            }

            if (storedPart != null)
            {
                foreach (PartImage image in storedPart.Images)
                {
                    AddReferenceImageForDeletion(images, filePaths, image);
                }
            }

            if (SelectedRegistrationPart != null &&
                IsSamePartNo(SelectedRegistrationPart.PartNo, RegistrationPartNo))
            {
                foreach (PartImage image in SelectedRegistrationPart.Part.Images)
                {
                    AddReferenceImageForDeletion(images, filePaths, image);
                }
            }

            return images;
        }

        private void AddReferenceImageForDeletion(
            IList<PartImage> images,
            ISet<string> filePaths,
            PartImage image)
        {
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return;
            }

            if (filePaths.Add(image.FilePath))
            {
                images.Add(image);
            }
        }

        private IList<string> BuildCoordinatePathsForDeletion(IList<PartImage> images)
        {
            IList<string> paths = new List<string>();
            ISet<string> uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddCoordinatePathForDeletion(paths, uniquePaths, RegistrationCoordinateImagePath);

            foreach (PartImage image in images)
            {
                string folderPath = Path.GetDirectoryName(image.FilePath);
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    continue;
                }

                // 좌표 이미지는 카메라마다 한 장씩 있으므로 모두 지울 목록에 넣습니다.
                // Thickness 만 지우면 Top 좌표 이미지가 남아, 다음에 그 품번을 열었을 때
                // 지웠다고 생각한 선이 다시 보입니다.
                foreach (ImageViewType coordinateViewType in MeasurementPointPolicy.GetSupportedViewTypes())
                {
                    AddCoordinatePathForDeletion(
                        paths,
                        uniquePaths,
                        Path.Combine(
                            folderPath,
                            ReferenceImageFileNamePolicy.BuildCoordinateFileName(coordinateViewType, RegistrationPartNo)));
                }

                // 카메라를 나누기 전에 저장한 옛 이름도 함께 지웁니다.
                AddCoordinatePathForDeletion(
                    paths,
                    uniquePaths,
                    Path.Combine(
                        folderPath,
                        ReferenceImageFileNamePolicy.BuildLegacyCoordinateFileName(RegistrationPartNo)));
                AddCoordinatePathForDeletion(
                    paths,
                    uniquePaths,
                    Path.Combine(folderPath, ReferenceImageFileNamePolicy.LegacyCoordinateFileName));
            }

            return paths;
        }

        private void AddCoordinatePathForDeletion(
            IList<string> paths,
            ISet<string> uniquePaths,
            string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && uniquePaths.Add(filePath))
            {
                paths.Add(filePath);
            }
        }

        private void DeleteReferenceImageFiles(IList<PartImage> images, IList<string> errors)
        {
            foreach (PartImage image in images)
            {
                string deleteMessage;
                if (!_referenceImageFileService.DeleteReferenceImage(image, out deleteMessage))
                {
                    errors.Add(deleteMessage);
                }
            }
        }

        private void DeleteCoordinateImageFiles(IList<string> filePaths, IList<string> errors)
        {
            foreach (string filePath in filePaths)
            {
                PartImage coordinateImage = new PartImage();
                coordinateImage.PartNo = RegistrationPartNo;
                coordinateImage.ViewType = ImageViewType.Thickness;
                coordinateImage.FilePath = filePath;

                string deleteMessage;
                if (!_referenceImageFileService.DeleteReferenceImage(coordinateImage, out deleteMessage))
                {
                    errors.Add(deleteMessage);
                }
            }
        }

        private void ClearTemporaryRegistrationFiles(IList<string> errors)
        {
            Part part = BuildRegistrationImagePart();
            try
            {
                _referenceImageFileService.ClearTemporaryReferenceImages(part);
            }
            catch (Exception ex)
            {
                errors.Add("Temp 이미지 정리 실패: " + ex.Message);
            }
        }

        private void ReorderRegistrationImages(PartImage selectedImage)
        {
            IList<PartImage> images = new List<PartImage>();
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                images.Add(imageViewModel.Image);
            }

            RegistrationImages.Clear();
            ImageEditViewModel selectedImageViewModel = null;
            foreach (ImageEditViewModel imageViewModel in BuildImageEditViewModels(images))
            {
                RegistrationImages.Add(imageViewModel);
                if (selectedImage != null && imageViewModel.Image.ViewType == selectedImage.ViewType)
                {
                    selectedImageViewModel = imageViewModel;
                }
            }

            SelectedRegistrationImage = selectedImageViewModel;
            RefreshRegistrationImagePreviews();
        }

        /// <summary>
        /// CSV 파일의 여러 부품 기준정보를 읽어 DB 저장 전 미리보기 목록으로 보관합니다.
        /// 실제 DB 반영은 다중품목 등록 화면의 DB 저장 버튼에서 진행합니다.
        /// </summary>
        private void ExecuteImportPartsCsv(object parameter)
        {
            string filePath = _fileDialogService.SelectCsvOpenFile();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                BulkRegistrationMessage = "CSV 불러오기가 취소되었습니다.";
                return;
            }

            string[] lines;
            string readErrorMessage;
            if (!TryReadCsvFile(filePath, out lines, out readErrorMessage))
            {
                BulkRegistrationMessage = readErrorMessage;
                _messageDialogService.ShowWarning("CSV 불러오기 실패", readErrorMessage);
                return;
            }

            if (lines.Length < 2)
            {
                BulkRegistrationMessage = "CSV에 저장할 데이터 행이 없습니다.";
                return;
            }

            BulkPartRows.Clear();
            _pendingBulkParts.Clear();
            _bulkImportHasError = false;
            IList<string> headers = NormalizeCsvCells(ParseCsvLine(lines[0]));
            if (!HasRequiredBulkPartCsvHeaders(headers))
            {
                BulkRegistrationMessage = "CSV 필수 헤더를 찾을 수 없습니다. 품번/품명/단위 컬럼을 확인하세요.";
                return;
            }

            HashSet<string> importedPartNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int normalCount = 0;
            int failedCount = 0;
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineIndex]))
                {
                    continue;
                }

                Part part = null;
                string saveMessage;
                try
                {
                    IList<string> values = NormalizeCsvCells(ParseCsvLine(lines[lineIndex]));
                    part = BuildPartFromBulkCsv(headers, values);
                    saveMessage = ValidateBulkImportedPart(part, importedPartNumbers);
                    if (string.IsNullOrEmpty(saveMessage))
                    {
                        importedPartNumbers.Add(part.PartNo.Trim());
                        _pendingBulkParts.Add(part);
                        normalCount++;
                        saveMessage = "정상";
                    }
                    else
                    {
                        failedCount++;
                        _bulkImportHasError = true;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _bulkImportHasError = true;
                    saveMessage = "CSV " + (lineIndex + 1).ToString() + "행 처리 중 오류: " + ex.Message;
                    part = new Part();
                    part.PartNo = "CSV 행 " + (lineIndex + 1).ToString();
                    part.PartName = "-";
                }

                BulkPartRows.Add(BuildBulkPartCsvRow(part, saveMessage));
            }

            if (normalCount == 0)
            {
                BulkRegistrationMessage = "CSV 불러오기 완료: 정상 0건, 오류 " + failedCount.ToString() + "건. DB에 저장할 수 있는 데이터가 없습니다.";
                return;
            }

            if (_bulkImportHasError)
            {
                BulkRegistrationMessage = "CSV 불러오기 완료: 정상 " + normalCount.ToString() + "건, 오류 " + failedCount.ToString() + "건. 오류가 있어 DB 저장을 진행할 수 없습니다.";
                return;
            }

            BulkRegistrationMessage = "CSV 불러오기 완료: 정상 " + normalCount.ToString() + "건. DB 저장을 누르면 현재 DB 기준정보를 교체합니다.";
        }

        private void ExecuteSaveBulkParts(object parameter)
        {
            if (_pendingBulkParts.Count == 0)
            {
                BulkRegistrationMessage = "DB에 저장할 다중품목 데이터가 없습니다. CSV를 먼저 불러오세요.";
                ShowSaveBlockedPopup(BulkRegistrationMessage);
                return;
            }

            if (_bulkImportHasError)
            {
                BulkRegistrationMessage = "CSV 오류가 남아 있어 DB 저장을 진행할 수 없습니다. CSV를 수정한 후 다시 불러오세요.";
                ShowSaveBlockedPopup(BulkRegistrationMessage);
                return;
            }

            string message = _partDataStore.ReplaceAllParts(_pendingBulkParts);
            if (message != PartCatalogService.ReplaceAllSuccessMessage)
            {
                BulkRegistrationMessage = message;
                ShowSaveBlockedPopup(message);
                return;
            }

            RefreshPartCollectionsFromDataStore();
            RefreshStatistics();
            BulkRegistrationMessage = message + " 저장 " + _pendingBulkParts.Count.ToString() + "건. 기준 이미지 파일은 삭제하지 않았습니다.";
        }

        /// <summary>
        /// 여섯 장을 하나로 합치는 일을 뒤에서 돌립니다.
        ///
        /// <para>
        /// SDK 호출이 10초를 넘길 수 있어 저장 자리에서 부르면 창이 그동안 멈춥니다.
        /// 합친 그림은 저장이 끝난 뒤 쓰는 것이라 기다릴 까닭이 없습니다.
        /// </para>
        /// </summary>
        private void StartReferenceImageMergeInBackground(Part part)
        {
            if (part == null || _imageMergeService == null)
            {
                return;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object unused)
            {
                string mergedFilePath;
                string mergeMessage;
                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    _imageMergeService.TryMergeReferenceImages(part, out mergedFilePath, out mergeMessage);
                }
                catch (Exception ex)
                {
                    mergeMessage = "통합 이미지를 만들지 못했습니다. " + ex.Message;
                }

                watch.Stop();
                if (string.IsNullOrWhiteSpace(mergeMessage))
                {
                    return;
                }

                // 화면 문구는 UI 스레드에서만 건드립니다.
                string finalMessage = mergeMessage + BuildSaveTimingText(watch.ElapsedMilliseconds, 0);
                System.Windows.Application current = System.Windows.Application.Current;
                if (current == null)
                {
                    return;
                }

                current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    RegistrationMessage = RegistrationMessage + " " + finalMessage;
                }));
            });
        }

        /// <summary>
        /// 저장이 어디에서 시간을 썼는지 화면 문구에 덧붙입니다.
        /// 1초를 넘긴 단계만 적어, 빠를 때는 문구가 길어지지 않게 합니다.
        /// </summary>
        private static string BuildSaveTimingText(long mergeMilliseconds, long refreshMilliseconds)
        {
            StringBuilder builder = new StringBuilder();
            if (mergeMilliseconds >= 1000)
            {
                builder.Append(" 통합 이미지 ");
                builder.Append((mergeMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture));
                builder.Append("초");
            }

            if (refreshMilliseconds >= 1000)
            {
                builder.Append(" 목록 갱신 ");
                builder.Append((refreshMilliseconds / 1000.0).ToString("0.0", CultureInfo.InvariantCulture));
                builder.Append("초");
            }

            return builder.Length == 0 ? string.Empty : " (" + builder.ToString().Trim() + ")";
        }

        private void ShowSaveBlockedPopup(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            _messageDialogService.ShowWarning("DB 저장 차단", message);
        }

        private string ValidateBulkImportedPart(Part part, HashSet<string> importedPartNumbers)
        {
            if (part == null)
            {
                return "부품 정보가 없습니다.";
            }

            if (string.IsNullOrWhiteSpace(part.PartNo))
            {
                return "품번 누락";
            }

            if (string.IsNullOrWhiteSpace(part.PartName))
            {
                return "품명 누락";
            }

            string partNo = part.PartNo.Trim();
            if (importedPartNumbers.Contains(partNo))
            {
                return "중복 품번: " + partNo;
            }

            return string.Empty;
        }

        /// <summary>
        /// 현재 DB에 등록된 전체 부품 기준정보를 다중품목 등록용 CSV 형식으로 내보냅니다.
        /// 측정부 컬럼은 전체 부품 중 가장 많은 측정부 세트 수를 기준으로 생성합니다.
        /// </summary>
        private void ExecuteExportAllPartsCsv(object parameter)
        {
            IList<Part> parts = _partDataStore.GetParts();
            if (parts.Count == 0)
            {
                BulkRegistrationMessage = "내보낼 부품 기준정보가 없습니다.";
                return;
            }

            string filePath = _fileDialogService.SelectCsvSaveFile();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                BulkRegistrationMessage = "CSV 내보내기가 취소되었습니다.";
                return;
            }

            IList<string> lines = BuildAllPartsCsvLines(parts);
            string writeErrorMessage;
            if (!TryWriteCsvFile(filePath, lines, out writeErrorMessage))
            {
                BulkRegistrationMessage = writeErrorMessage;
                _messageDialogService.ShowWarning("CSV 내보내기 실패", writeErrorMessage);
                return;
            }

            BulkRegistrationMessage = "전체 부품 기준정보 " + parts.Count.ToString() + "건을 CSV 파일로 내보냈습니다.";
        }

        private Part BuildPartFromBulkCsv(IList<string> headers, IList<string> values)
        {
            Part part = new Part();
            part.PartNo = GetCsvValue(headers, values, "품번", "PartNo", "Part No", "Part No.");
            part.PartName = GetCsvValue(headers, values, "품명", "PartName", "Part Name");
            part.CategoryCode = GetCsvValue(headers, values, "분류코드", "CategoryCode", "Category Code");
            part.CategoryDescription = GetCsvValue(headers, values, "분류설명", "CategoryDescription", "Category Description");
            // 머리글을 "구분"에서 "메모"로 바꿨습니다. 예전에 내보낸 CSV도 그대로 읽히도록
            // 옛 이름을 함께 받습니다.
            part.Memo = GetCsvValue(headers, values, "메모", "구분", "Memo", "PartType", "Type");
            AddBulkCsvMeasurementRegions(part, headers, values);
            return part;
        }

        private bool HasRequiredBulkPartCsvHeaders(IList<string> headers)
        {
            return HasCsvHeader(headers, "품번", "PartNo", "Part No", "Part No.") &&
                   HasCsvHeader(headers, "품명", "PartName", "Part Name") &&
                   HasCsvHeader(headers, "단위", "Unit", "MeasurementUnit");
        }

        private void AddBulkCsvMeasurementRegions(Part part, IList<string> headers, IList<string> values)
        {
            string unit = NormalizeBulkMetadataValue(GetCsvValue(headers, values, "단위", "Unit", "MeasurementUnit"), "mm");
            if (!string.Equals(unit, "mm", StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException("측정부 단위는 mm만 사용할 수 있습니다.");
            }

            // 측정부는 카메라마다 따로 관리하므로 카메라별로 열을 훑습니다.
            // 예전 파일에는 카메라 구분이 없는 "측정부N…" 열만 있는데,
            // 그때는 Thickness 하나뿐이었으므로 Thickness를 읽을 때 그 열도 함께 봅니다.
            int outputIndex = 1;
            foreach (ImageViewType csvViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
            int viewIndex = 1;
            for (int csvIndex = 1; csvIndex <= MeasurementPointPolicy.MaxCount; csvIndex++)
            {
                string itemType = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "항목");
                string nominalText = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "기준");
                string toleranceMinText = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "Min");
                string toleranceMaxText = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "Max");
                string toleranceRangeText = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "MinMax");
                string legacyToleranceText = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "허용");
                ApplyCsvToleranceAliases(csvIndex, toleranceRangeText, legacyToleranceText, ref toleranceMinText, ref toleranceMaxText);

                string lineColor = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "색상");
                string x1Text = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "X1");
                string y1Text = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "Y1");
                string x2Text = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "X2");
                string y2Text = GetMeasurementCsvValue(headers, values, csvViewType, csvIndex, "Y2");

                if (AreMeasurementCsvValuesUnused(
                    itemType,
                    nominalText,
                    toleranceMinText,
                    toleranceMaxText,
                    lineColor,
                    x1Text,
                    y1Text,
                    x2Text,
                    y2Text))
                {
                    continue;
                }

                MeasurementRegion region = new MeasurementRegion();
                region.Id = outputIndex;
                region.PartNo = part.PartNo;

                // 번호는 카메라 안에서 1부터 셉니다.
                region.IndexNo = viewIndex;
                region.ItemType = NormalizeBulkMetadataValue(itemType, "미설정");
                region.Name = MeasurementPointPolicy.BuildPointName(csvViewType, viewIndex) + " - " + region.ItemType;
                region.ViewType = csvViewType;
                region.NominalValue = ParseRequiredCsvDecimal(nominalText, csvIndex, "기준");

                decimal toleranceMin = ParseOptionalCsvDecimal(toleranceMinText, csvIndex, "Min", 0m);
                decimal toleranceMax = ParseOptionalCsvDecimal(toleranceMaxText, csvIndex, "Max", 0m);
                region.ToleranceMin = toleranceMin;
                region.ToleranceMax = toleranceMax;
                region.Unit = "mm";
                region.LineColor = NormalizeBulkMetadataValue(lineColor, MeasurementPointPolicy.GetDefaultColor(outputIndex));
                ApplyCsvCoordinates(region, csvIndex, x1Text, y1Text, x2Text, y2Text);
                part.MeasurementRegions.Add(region);
                outputIndex++;
                viewIndex++;
            }
            }
        }

        private string NormalizeBulkMetadataValue(string value, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            {
                return defaultValue;
            }

            return value.Trim();
        }

        private void ApplyCsvToleranceAliases(
            int indexNo,
            string toleranceRangeText,
            string legacyToleranceText,
            ref string toleranceMinText,
            ref string toleranceMaxText)
        {
            if (!IsUnusedCsvValue(toleranceRangeText))
            {
                decimal toleranceMin;
                decimal toleranceMax;
                ParseCsvToleranceRange(toleranceRangeText, indexNo, out toleranceMin, out toleranceMax);

                if (IsUnusedCsvValue(toleranceMinText))
                {
                    toleranceMinText = toleranceMin.ToString("0.###", CultureInfo.InvariantCulture);
                }

                if (IsUnusedCsvValue(toleranceMaxText))
                {
                    toleranceMaxText = toleranceMax.ToString("0.###", CultureInfo.InvariantCulture);
                }

                return;
            }

            if (IsUnusedCsvValue(legacyToleranceText))
            {
                return;
            }

            if (IsUnusedCsvValue(toleranceMinText))
            {
                toleranceMinText = legacyToleranceText;
            }

            if (IsUnusedCsvValue(toleranceMaxText))
            {
                toleranceMaxText = legacyToleranceText;
            }
        }

        /// <summary>
        /// 측정부 열 값을 읽습니다. 카메라별 열을 먼저 보고, 없으면 예전 열도 봅니다.
        ///
        /// <para>
        /// 지금 열 이름은 Top1항목, Thk1항목 처럼 카메라를 앞에 답니다.
        /// 예전 파일에는 카메라 구분이 없는 측정부1항목 만 있는데, 그때는 Thickness
        /// 하나뿐이었으므로 Thickness를 읽을 때만 그 열을 함께 봅니다.
        /// Top이 남의 열을 가져가면 안 되기 때문입니다.
        /// </para>
        /// </summary>
        private string GetMeasurementCsvValue(
            IList<string> headers,
            IList<string> values,
            ImageViewType viewType,
            int indexNo,
            string fieldName)
        {
            string indexText = indexNo.ToString(CultureInfo.InvariantCulture);
            string viewPrefix = MeasurementPointPolicy.GetViewShortName(viewType) + indexText;

            string current = GetCsvValue(headers, values, viewPrefix + fieldName, viewPrefix + "_" + fieldName);
            if (!IsUnusedCsvValue(current))
            {
                return current;
            }

            if (viewType != ImageViewType.Thickness)
            {
                return current;
            }

            string legacyPrefix = "측정부" + indexText;
            return GetCsvValue(
                headers,
                values,
                legacyPrefix + fieldName,
                legacyPrefix + "_" + fieldName,
                "Measurement" + indexText + fieldName);
        }

        private bool AreMeasurementCsvValuesUnused(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && value.Trim() != "-")
                {
                    return false;
                }
            }

            return true;
        }

        private decimal ParseRequiredCsvDecimal(string value, int indexNo, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            {
                throw new FormatException("측정부" + indexNo.ToString() + " " + fieldName + "값이 없습니다.");
            }

            return ParseCsvDecimal(value, indexNo, fieldName);
        }

        private decimal ParseOptionalCsvDecimal(string value, int indexNo, string fieldName, decimal defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            {
                return defaultValue;
            }

            return ParseCsvDecimal(value, indexNo, fieldName);
        }

        private void ParseCsvToleranceRange(string value, int indexNo, out decimal toleranceMin, out decimal toleranceMax)
        {
            MatchCollection matches = Regex.Matches(value, @"[-+]?\d+(?:[\.,]\d+)?");
            if (matches.Count == 0)
            {
                throw new FormatException(
                    "측정부" + indexNo.ToString() + " MinMax 값을 숫자로 입력하세요. 입력값=" + value);
            }

            toleranceMin = Math.Abs(ParseCsvDecimal(matches[0].Value, indexNo, "MinMax"));
            toleranceMax = toleranceMin;
            if (matches.Count > 1)
            {
                toleranceMax = Math.Abs(ParseCsvDecimal(matches[1].Value, indexNo, "MinMax"));
            }
        }

        private decimal ParseCsvDecimal(string value, int indexNo, string fieldName)
        {
            decimal parsed;
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed) ||
                decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            throw new FormatException(
                "측정부" + indexNo.ToString() + " " + fieldName + "값을 숫자로 입력하세요. 입력값=" + value);
        }

        private void ApplyCsvCoordinates(
            MeasurementRegion region,
            int indexNo,
            string x1Text,
            string y1Text,
            string x2Text,
            string y2Text)
        {
            bool hasAnyCoordinate = !IsUnusedCsvValue(x1Text) ||
                                    !IsUnusedCsvValue(y1Text) ||
                                    !IsUnusedCsvValue(x2Text) ||
                                    !IsUnusedCsvValue(y2Text);
            if (!hasAnyCoordinate)
            {
                region.Coordinates = "미지정";
                return;
            }

            if (IsUnusedCsvValue(x1Text) ||
                IsUnusedCsvValue(y1Text) ||
                IsUnusedCsvValue(x2Text) ||
                IsUnusedCsvValue(y2Text))
            {
                throw new FormatException("측정부" + indexNo.ToString() + " 좌표는 X1, Y1, X2, Y2를 모두 입력해야 합니다.");
            }

            region.X1 = ParseCsvDouble(x1Text, indexNo, "X1");
            region.Y1 = ParseCsvDouble(y1Text, indexNo, "Y1");
            region.X2 = ParseCsvDouble(x2Text, indexNo, "X2");
            region.Y2 = ParseCsvDouble(y2Text, indexNo, "Y2");
            region.Coordinates =
                region.X1.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                region.Y1.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                region.X2.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                region.Y2.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private double ParseCsvDouble(string value, int indexNo, string fieldName)
        {
            double parsed;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) ||
                double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            throw new FormatException(
                "측정부" + indexNo.ToString() + " " + fieldName + " 좌표를 숫자로 입력하세요. 입력값=" + value);
        }

        private bool IsUnusedCsvValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Trim() == "-";
        }

        private BulkPartCsvRowViewModel BuildBulkPartCsvRow(Part part, string resultMessage)
        {
            BulkPartCsvRowViewModel row = new BulkPartCsvRowViewModel();
            row.PartNo = part.PartNo;
            row.PartName = part.PartName;
            row.CategoryCode = part.CategoryCode;
            row.CategoryDescription = part.CategoryDescription;
            row.Memo = part.Memo;
            // 카메라마다 다섯 칸씩 따로 채웁니다.
            // 앞에서부터 다섯 개만 채우면 Top 이 다 차지해 Thickness 가 한 칸도 안 보입니다.
            row.Top1Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Top, 1));
            row.Top2Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Top, 2));
            row.Top3Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Top, 3));
            row.Top4Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Top, 4));
            row.Top5Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Top, 5));
            row.Thk1Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Thickness, 1));
            row.Thk2Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Thickness, 2));
            row.Thk3Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Thickness, 3));
            row.Thk4Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Thickness, 4));
            row.Thk5Summary = BuildMeasurementCsvSummary(FindMeasurementRegion(part, ImageViewType.Thickness, 5));
            row.MeasurementUnit = "mm";
            row.ResultMessage = string.IsNullOrWhiteSpace(resultMessage) ? "정상" : resultMessage;
            return row;
        }

        /// <summary>
        /// 그 카메라의 그 번호를 가져옵니다. 없으면 null 입니다.
        /// </summary>
        private MeasurementRegion FindMeasurementRegion(Part part, ImageViewType viewType, int indexNo)
        {
            if (part == null || part.MeasurementRegions == null)
            {
                return null;
            }

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region != null && region.ViewType == viewType && region.IndexNo == indexNo)
                {
                    return region;
                }
            }

            return null;
        }

        /// <summary>
        /// 카메라를 가리지 않고 앞에서부터 세어 가져옵니다. 요약 칸을 채울 때 씁니다.
        /// </summary>
        private MeasurementRegion GetMeasurementRegionByOrder(Part part, int order)
        {
            if (part == null || part.MeasurementRegions == null)
            {
                return null;
            }

            int current = 1;
            foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                foreach (MeasurementRegion region in part.MeasurementRegions)
                {
                    if (region == null || region.ViewType != viewType)
                    {
                        continue;
                    }

                    if (current == order)
                    {
                        return region;
                    }

                    current++;
                }
            }

            return null;
        }

        private string BuildMeasurementCsvSummary(MeasurementRegion region)
        {
            if (region == null)
            {
                return "-";
            }

            // 어느 카메라의 몇 번인지는 열 제목이 알려 주므로 값만 적습니다.
            return NormalizeBulkMetadataValue(region.ItemType, "미설정") + " / " +
                   region.NominalValue.ToString("0.###", CultureInfo.InvariantCulture) + " " +
                   FormatToleranceRange(region) + " / " +
                   NormalizeBulkMetadataValue(region.LineColor, MeasurementPointPolicy.GetDefaultColor(region.IndexNo));
        }

        private IList<string> BuildAllPartsCsvLines(IList<Part> parts)
        {
            IList<string> lines = new List<string>();
            IList<string> headers = BuildPartCsvHeaders();
            lines.Add(BuildCsvLine(headers));

            foreach (Part part in parts)
            {
                lines.Add(BuildCsvLine(BuildPartCsvValues(part)));
            }

            return lines;
        }

        private IList<string> BuildPartCsvHeaders()
        {
            IList<string> headers = new List<string>();
            headers.Add("품번");
            headers.Add("품명");
            headers.Add("분류코드");
            headers.Add("분류설명");
            headers.Add("메모");

            // 측정부는 카메라마다 따로 관리하므로 열도 카메라마다 다섯 벌씩 나갑니다.
            //   Top1항목 … Top5Y2, Thk1항목 … Thk5Y2
            foreach (ImageViewType headerViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
            for (int indexNo = 1; indexNo <= MeasurementPointPolicy.MaxCount; indexNo++)
            {
                string prefix = MeasurementPointPolicy.GetViewShortName(headerViewType) +
                                indexNo.ToString(CultureInfo.InvariantCulture);
                headers.Add(prefix + "항목");
                headers.Add(prefix + "기준");
                headers.Add(prefix + "Min");
                headers.Add(prefix + "Max");
                headers.Add(prefix + "색상");
                headers.Add(prefix + "X1");
                headers.Add(prefix + "Y1");
                headers.Add(prefix + "X2");
                headers.Add(prefix + "Y2");
            }
            }

            headers.Add("단위");
            return headers;
        }

        private IList<string> BuildPartCsvValues(Part part)
        {
            IList<string> values = new List<string>();
            values.Add(part.PartNo);
            values.Add(part.PartName);
            values.Add(part.CategoryCode);
            values.Add(part.CategoryDescription);
            values.Add(part.Memo);

            foreach (ImageViewType valueViewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                for (int indexNo = 1; indexNo <= MeasurementPointPolicy.MaxCount; indexNo++)
                {
                    AddMeasurementPointCsvValues(
                        values, GetMeasurementRegionForCsv(part, valueViewType, indexNo), indexNo);
                }
            }

            values.Add("mm");
            return values;
        }

        private void AddMeasurementPointCsvValues(
            IList<string> values,
            MeasurementRegion region,
            int indexNo)
        {
            if (region == null)
            {
                for (int fieldIndex = 0; fieldIndex < 9; fieldIndex++)
                {
                    values.Add("-");
                }

                return;
            }

            values.Add(NormalizeBulkMetadataValue(region.ItemType, "미설정"));
            values.Add(region.NominalValue.ToString("0.###", CultureInfo.InvariantCulture));
            values.Add(region.ToleranceMin.ToString("0.###", CultureInfo.InvariantCulture));
            values.Add(Math.Abs(region.ToleranceMax).ToString("0.###", CultureInfo.InvariantCulture));
            values.Add(NormalizeBulkMetadataValue(region.LineColor, MeasurementPointPolicy.GetDefaultColor(indexNo)));
            values.Add(FormatCsvCoordinate(region.X1));
            values.Add(FormatCsvCoordinate(region.Y1));
            values.Add(FormatCsvCoordinate(region.X2));
            values.Add(FormatCsvCoordinate(region.Y2));
        }

        private string FormatCsvCoordinate(double? value)
        {
            return value.HasValue
                ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
                : "-";
        }

        /// <summary>
        /// 내보낼 측정부를 찾습니다. 번호는 카메라마다 1부터 세므로 카메라도 함께 봅니다.
        /// </summary>
        private MeasurementRegion GetMeasurementRegionForCsv(Part part, ImageViewType viewType, int indexNo)
        {
            if (part == null || part.MeasurementRegions == null)
            {
                return null;
            }

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region != null && region.ViewType == viewType && region.IndexNo == indexNo)
                {
                    return region;
                }
            }

            // 아래는 번호가 비어 있는 예전 자료를 위한 처리입니다.
            // 그 시절 측정부는 모두 Thickness였으므로 다른 카메라에는 해당하지 않습니다.
            if (viewType != ImageViewType.Thickness)
            {
                return null;
            }

            // 번호가 제대로 매겨진 측정부가 하나라도 있으면 이 처리를 쓰지 않습니다.
            // 그것까지 순서로 세면 다른 카메라의 측정부를 Thickness 자리에 넣게 됩니다.
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region != null && region.IndexNo > 0)
                {
                    return null;
                }
            }

            int currentIndex = 1;
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region == null)
                {
                    continue;
                }

                if (currentIndex == indexNo)
                {
                    return region;
                }

                currentIndex++;
            }

            return null;
        }

        private string GetCsvValue(IList<string> headers, IList<string> values, params string[] headerNames)
        {
            for (int headerIndex = 0; headerIndex < headers.Count; headerIndex++)
            {
                string normalizedHeader = NormalizeCsvCell(headers[headerIndex]);
                foreach (string headerName in headerNames)
                {
                    if (string.Equals(normalizedHeader, NormalizeCsvCell(headerName), StringComparison.OrdinalIgnoreCase))
                    {
                        return GetCsvValue(values, headerIndex);
                    }
                }
            }

            return string.Empty;
        }

        private string GetCsvValue(IList<string> values, int index)
        {
            if (index < 0 || index >= values.Count)
            {
                return string.Empty;
            }

            return NormalizeCsvCell(values[index]);
        }

        private IList<string> NormalizeCsvCells(IList<string> values)
        {
            IList<string> normalizedValues = new List<string>();
            foreach (string value in values)
            {
                normalizedValues.Add(NormalizeCsvCell(value));
            }

            return normalizedValues;
        }

        private string NormalizeCsvCell(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value.Trim().TrimStart('\uFEFF');
        }

        private bool HasCsvHeader(IList<string> headers, params string[] headerNames)
        {
            foreach (string header in headers)
            {
                string normalizedHeader = NormalizeCsvCell(header);
                foreach (string headerName in headerNames)
                {
                    if (string.Equals(normalizedHeader, NormalizeCsvCell(headerName), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private IList<string> ParseCsvLine(string line)
        {
            IList<string> values = new List<string>();
            StringBuilder fieldBuilder = new StringBuilder();
            bool isInQuotes = false;
            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];
                if (character == '"')
                {
                    if (isInQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        fieldBuilder.Append('"');
                        index++;
                    }
                    else
                    {
                        isInQuotes = !isInQuotes;
                    }
                }
                else if (character == ',' && !isInQuotes)
                {
                    values.Add(fieldBuilder.ToString());
                    fieldBuilder.Clear();
                }
                else
                {
                    fieldBuilder.Append(character);
                }
            }

            values.Add(fieldBuilder.ToString());
            return values;
        }

        private void RefreshHistory()
        {
            _allInspectionHistory.Clear();
            foreach (Inspection inspection in _inspectionRepository.GetAll())
            {
                _allInspectionHistory.Add(new InspectionRowViewModel(inspection));
            }

            ApplyHistoryFilters();
        }

        /// <summary>
        /// 이력 화면 검색 조건을 적용해 화면 목록을 갱신합니다.
        /// CSV 저장은 이 화면 목록을 그대로 사용하므로 사용자가 추린 항목만 저장됩니다.
        /// </summary>
        private void ApplyHistoryFilters()
        {
            InspectionHistory.Clear();
            foreach (InspectionRowViewModel historyRow in _allInspectionHistory)
            {
                if (IsHistoryRowMatched(historyRow))
                {
                    InspectionHistory.Add(historyRow);
                }
            }

            HistoryMessage = "조회 " + InspectionHistory.Count.ToString() + "건 / 전체 " + _allInspectionHistory.Count.ToString() + "건";
        }

        private bool IsHistoryRowMatched(InspectionRowViewModel historyRow)
        {
            if (!IsHistoryTimeRangeMatched(historyRow))
            {
                return false;
            }

            if (!ContainsKeyword(historyRow.PartNo, HistoryPartNoKeyword))
            {
                return false;
            }

            if (!ContainsKeyword(historyRow.PartName, HistoryPartNameKeyword))
            {
                return false;
            }

            if (!ContainsKeyword(historyRow.CategoryCode, HistoryCategoryCodeKeyword))
            {
                return false;
            }

            if (!ContainsKeyword(historyRow.CategoryDescription, HistoryCategoryDescriptionKeyword))
            {
                return false;
            }

            if (!ContainsKeyword(historyRow.Memo, HistoryMemoKeyword))
            {
                return false;
            }

            if (!IsHistoryNgResultMatched(historyRow))
            {
                return false;
            }

            return true;
        }

        private bool IsHistoryTimeRangeMatched(InspectionRowViewModel historyRow)
        {
            DateTime inspectedAt = historyRow.InspectedAtValue;
            DateTime startTime;
            if (!string.IsNullOrWhiteSpace(HistoryStartTimeKeyword))
            {
                if (!DateTime.TryParse(HistoryStartTimeKeyword, out startTime))
                {
                    return false;
                }

                if (inspectedAt < startTime)
                {
                    return false;
                }
            }

            DateTime endTime;
            if (!string.IsNullOrWhiteSpace(HistoryEndTimeKeyword))
            {
                if (!DateTime.TryParse(HistoryEndTimeKeyword, out endTime))
                {
                    return false;
                }

                if (!HistoryEndTimeKeyword.Contains(":"))
                {
                    endTime = endTime.Date.AddDays(1).AddTicks(-1);
                }

                if (inspectedAt > endTime)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsHistoryNgResultMatched(InspectionRowViewModel historyRow)
        {
            if (string.IsNullOrWhiteSpace(HistoryNgResultKeyword))
            {
                return true;
            }

            if (ContainsKeyword(historyRow.NgResult, HistoryNgResultKeyword))
            {
                return true;
            }

            return ContainsKeyword(historyRow.Result, HistoryNgResultKeyword);
        }

        /// <summary>
        /// 이력 검색 조건을 모두 비우고 전체 이력을 다시 표시합니다.
        /// </summary>
        private void ExecuteClearHistorySearch(object parameter)
        {
            HistoryStartTimeKeyword = string.Empty;
            HistoryEndTimeKeyword = string.Empty;
            HistoryPartNoKeyword = string.Empty;
            HistoryPartNameKeyword = string.Empty;
            HistoryCategoryCodeKeyword = string.Empty;
            HistoryCategoryDescriptionKeyword = string.Empty;
            HistoryMemoKeyword = string.Empty;
            HistoryNgResultKeyword = string.Empty;
            ApplyHistoryFilters();
        }

        private void ExecuteSaveHistoryCsv(object parameter)
        {
            if (InspectionHistory.Count == 0)
            {
                HistoryMessage = "저장할 검사 이력이 없습니다.";
                return;
            }

            string filePath = _fileDialogService.SelectCsvSaveFile();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                HistoryMessage = "CSV 저장이 취소되었습니다.";
                return;
            }

            IList<string> lines = BuildHistoryCsvLines();
            string writeErrorMessage;
            if (!TryWriteCsvFile(filePath, lines, out writeErrorMessage))
            {
                HistoryMessage = writeErrorMessage;
                _messageDialogService.ShowWarning("CSV 저장 실패", writeErrorMessage);
                return;
            }

            HistoryMessage = "조회된 검사 이력 " + InspectionHistory.Count.ToString() + "건을 CSV 파일로 저장했습니다.";
        }

        private bool TryReadCsvFile(
            string filePath,
            out string[] lines,
            out string errorMessage)
        {
            lines = new string[0];
            errorMessage = string.Empty;

            try
            {
                lines = File.ReadAllLines(filePath, Encoding.UTF8);
                return true;
            }
            catch (IOException ex)
            {
                errorMessage = "CSV 파일을 읽을 수 없습니다. 같은 파일을 Excel이나 다른 프로그램에서 열어 잠근 경우 파일을 닫고 다시 시도하세요. 상세: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                errorMessage = "CSV 파일을 읽을 권한이 없습니다. 파일과 폴더의 접근 권한을 확인하세요. 상세: " + ex.Message;
            }
            catch (NotSupportedException ex)
            {
                errorMessage = "CSV 파일 경로 형식이 올바르지 않습니다. 상세: " + ex.Message;
            }
            catch (Exception ex)
            {
                errorMessage = "CSV 파일을 불러오는 중 오류가 발생했습니다. 상세: " + ex.Message;
            }

            return false;
        }

        private bool TryWriteCsvFile(
            string filePath,
            IList<string> lines,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using (FileStream stream = new FileStream(
                    filePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read))
                using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(true)))
                {
                    foreach (string line in lines)
                    {
                        writer.WriteLine(line);
                    }
                }

                return true;
            }
            catch (IOException ex)
            {
                errorMessage = "CSV 파일을 저장할 수 없습니다. 같은 이름의 파일을 Excel이나 다른 프로그램에서 열어 잠근 경우 파일을 닫거나 다른 이름으로 저장하세요. 상세: " + ex.Message;
            }
            catch (UnauthorizedAccessException ex)
            {
                errorMessage = "CSV 파일을 저장할 권한이 없습니다. 저장 폴더의 접근 권한을 확인하세요. 상세: " + ex.Message;
            }
            catch (NotSupportedException ex)
            {
                errorMessage = "CSV 저장 경로 형식이 올바르지 않습니다. 상세: " + ex.Message;
            }
            catch (Exception ex)
            {
                errorMessage = "CSV 파일을 저장하는 중 오류가 발생했습니다. 상세: " + ex.Message;
            }

            return false;
        }

        private IList<string> BuildHistoryCsvLines()
        {
            IList<string> lines = new List<string>();
            lines.Add(BuildCsvLine(BuildHistoryGridCsvHeaders()));

            foreach (InspectionRowViewModel row in InspectionHistory)
            {
                lines.Add(BuildCsvLine(BuildHistoryGridCsvRow(row)));
            }

            return lines;
        }

        private IList<string> BuildHistoryGridCsvHeaders()
        {
            IList<string> headers = new List<string>();
            headers.Add("시간");
            headers.Add("품번");
            headers.Add("품명");
            headers.Add("분류코드");
            headers.Add("분류설명");
            headers.Add("메모");
            headers.Add("결과");
            headers.Add("NG결과");
            headers.Add("측정값");
            headers.Add("기준값");
            headers.Add("메시지");
            return headers;
        }

        private IList<string> BuildHistoryGridCsvRow(InspectionRowViewModel row)
        {
            IList<string> values = new List<string>();
            values.Add(row.InspectedAt);
            values.Add(row.PartNo);
            values.Add(row.PartName);
            values.Add(row.CategoryCode);
            values.Add(row.CategoryDescription);
            values.Add(row.Memo);
            values.Add(row.Result);
            values.Add(row.NgResult);
            values.Add(row.MeasuredValues);
            values.Add(row.NominalValues);
            values.Add(row.Message);
            return values;
        }

        private string BuildCsvLine(IList<string> values)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < values.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(",");
                }

                builder.Append(EscapeCsvValue(values[index]));
            }

            return builder.ToString();
        }

        private string EscapeCsvValue(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string escaped = value.Replace("\"", "\"\"");
            if (escaped.Contains(",") || escaped.Contains("\"") || escaped.Contains("\r") || escaped.Contains("\n"))
            {
                return "\"" + escaped + "\"";
            }

            return escaped;
        }

        private void RefreshStatistics()
        {
            DateTime? startTime;
            DateTime? endTime;
            string validationMessage;
            if (!TryBuildStatisticsPeriod(out startTime, out endTime, out validationMessage))
            {
                StatisticsMessage = validationMessage;
                ClearStatisticsDetailRows();
                return;
            }

            StatisticsSummary summary = _statisticsService.BuildSummary(startTime, endTime);
            TotalPartCount = summary.TotalPartCount;
            TotalInspectionCount = summary.TotalInspectionCount;
            PassCount = summary.PassCount;
            FailCount = summary.FailCount;
            ErrorCount = summary.ErrorCount;
            RefreshStatisticsDetailRows(startTime, endTime);
            StatisticsMessage = BuildStatisticsMessage(startTime, endTime, summary.TotalInspectionCount);
        }

        private void ExecuteRefreshStatistics(object parameter)
        {
            RefreshStatistics();
        }

        /// <summary>
        /// 조회 기간 칸에 넣을 날짜와 시각입니다. 지금 시각을 그대로 씁니다.
        ///   예) 2026-08-21 15:00
        ///
        /// <para>
        /// 시각이 붙으므로 End 칸은 그날 끝까지로 넓혀지지 않고 누른 그 시각까지만 봅니다.
        /// Start에 어제 이 시각, End에 오늘 이 시각을 넣으면 꼭 하루치가 걸립니다.
        /// </para>
        /// </summary>
        private static string BuildSearchDateTimeText(int dayOffset)
        {
            return DateTime.Now.AddDays(dayOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        private void ExecuteSetHistoryStartDate(object parameter)
        {
            HistoryStartTimeKeyword = BuildSearchDateTimeText(-1);
        }

        private void ExecuteSetHistoryEndDate(object parameter)
        {
            HistoryEndTimeKeyword = BuildSearchDateTimeText(0);
        }

        private void ExecuteSetStatisticsStartDate(object parameter)
        {
            StatisticsStartTimeKeyword = BuildSearchDateTimeText(-1);
        }

        private void ExecuteSetStatisticsEndDate(object parameter)
        {
            StatisticsEndTimeKeyword = BuildSearchDateTimeText(0);
        }

        private void ExecuteResetStatistics(object parameter)
        {
            TotalPartCount = 0;
            TotalInspectionCount = 0;
            PassCount = 0;
            FailCount = 0;
            ErrorCount = 0;
            ClearStatisticsDetailRows();
            StatisticsMessage = "통계 표시를 초기화했습니다. DB 검사 이력은 삭제하지 않습니다.";
        }

        private bool TryBuildStatisticsPeriod(out DateTime? startTime, out DateTime? endTime, out string validationMessage)
        {
            startTime = null;
            endTime = null;
            validationMessage = string.Empty;

            DateTime parsedStartTime;
            if (!TryParseOptionalStatisticsTime(StatisticsStartTimeKeyword, false, out parsedStartTime))
            {
                validationMessage = "Start 시간을 확인하세요. 예: 2026-07-07 08:00";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(StatisticsStartTimeKeyword))
            {
                startTime = parsedStartTime;
            }

            DateTime parsedEndTime;
            if (!TryParseOptionalStatisticsTime(StatisticsEndTimeKeyword, true, out parsedEndTime))
            {
                validationMessage = "End 시간을 확인하세요. 예: 2026-07-07 18:00";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(StatisticsEndTimeKeyword))
            {
                endTime = parsedEndTime;
            }

            if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            {
                validationMessage = "Start 시간이 End 시간보다 늦습니다.";
                return false;
            }

            return true;
        }

        private bool TryParseOptionalStatisticsTime(string value, bool isEndTime, out DateTime parsedTime)
        {
            parsedTime = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string trimmedValue = value.Trim();
            if (!DateTime.TryParse(trimmedValue, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out parsedTime) &&
                !DateTime.TryParse(trimmedValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsedTime))
            {
                return false;
            }

            if (isEndTime && IsDateOnlyText(trimmedValue))
            {
                parsedTime = parsedTime.Date.AddDays(1).AddTicks(-1);
            }

            return true;
        }

        private static bool IsDateOnlyText(string value)
        {
            return Regex.IsMatch(value, @"^\d{4}[-/.]\d{1,2}[-/.]\d{1,2}$");
        }

        private string BuildStatisticsMessage(DateTime? startTime, DateTime? endTime, int inspectionCount)
        {
            if (!startTime.HasValue && !endTime.HasValue)
            {
                return "전체 기간 검사 이력 " + inspectionCount.ToString() + "건을 집계했습니다.";
            }

            string startText = startTime.HasValue ? startTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "처음";
            string endText = endTime.HasValue ? endTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "현재";
            return startText + " ~ " + endText + " 검사 이력 " + inspectionCount.ToString() + "건을 집계했습니다.";
        }

        /// <summary>
        /// 통계 화면 하단의 OK/NG/Error 상세 목록을 기간 조건에 맞춰 갱신합니다.
        /// 이력 화면과 같은 InspectionRowViewModel을 사용해 시간, 품번, 측정값, 메시지 표시 형식을 맞춥니다.
        /// </summary>
        private void RefreshStatisticsDetailRows(DateTime? startTime, DateTime? endTime)
        {
            ClearStatisticsDetailRows();

            List<Inspection> inspections = new List<Inspection>(_inspectionRepository.GetAll());
            inspections.Sort(delegate(Inspection left, Inspection right)
            {
                return right.InspectedAt.CompareTo(left.InspectedAt);
            });

            foreach (Inspection inspection in inspections)
            {
                if (!IsStatisticsInspectionInPeriod(inspection, startTime, endTime))
                {
                    continue;
                }

                InspectionRowViewModel row = new InspectionRowViewModel(inspection);
                if (inspection.Result == InspectionResult.Pass)
                {
                    StatisticsOkRows.Add(row);
                }
                else if (inspection.Result == InspectionResult.Fail)
                {
                    StatisticsNgRows.Add(row);
                }
                else if (inspection.Result == InspectionResult.Error)
                {
                    StatisticsErrorRows.Add(row);
                }
            }
        }

        private void ClearStatisticsDetailRows()
        {
            StatisticsOkRows.Clear();
            StatisticsNgRows.Clear();
            StatisticsErrorRows.Clear();
        }

        private bool IsStatisticsInspectionInPeriod(Inspection inspection, DateTime? startTime, DateTime? endTime)
        {
            if (inspection == null)
            {
                return false;
            }

            if (startTime.HasValue && inspection.InspectedAt < startTime.Value)
            {
                return false;
            }

            if (endTime.HasValue && inspection.InspectedAt > endTime.Value)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 화면 상단에 표시할 프로그램 버전과 실행 파일 빌드 시간을 구성합니다.
        /// </summary>
        private string BuildApplicationVersionText()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Version version = assembly.GetName().Version;
            string versionText = version == null ? "1.0.0" : version.ToString();
            DateTime buildTime = File.GetLastWriteTime(assembly.Location);
            return "버전 " + versionText + " / 빌드 " + buildTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Config.json 및 환경변수 규칙을 동일하게 적용하여 현재 실행에 사용된 GPU 번호를 표시합니다.
        /// 설정을 바꾸려면 프로그램을 완전히 종료한 뒤 다시 시작해야 합니다.
        /// </summary>
        private void LoadVladGpuStatus()
        {
            try
            {
                VladVisionSettings settings = VladVisionSettings.Load(AppDomain.CurrentDomain.BaseDirectory);
                VladGpuStatusText = "GPU ID " + settings.GpuId.ToString(CultureInfo.InvariantCulture) + " (실행 시작 시 적용된 설정)";
            }
            catch (Exception ex)
            {
                VladGpuStatusText = "GPU 설정을 읽지 못했습니다: " + ex.Message;
            }
        }

        private void RefreshDiskUsages()
        {
            DiskUsages.Clear();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed)
                    {
                        continue;
                    }

                    DiskUsages.Add(new DiskUsageViewModel(
                        drive.Name,
                        drive.VolumeLabel,
                        drive.TotalSize,
                        drive.AvailableFreeSpace));
                }
                catch
                {
                    // 드라이브 상태가 순간적으로 바뀌어도 옵션 화면 갱신은 계속 진행합니다.
                }
            }
        }

        private void InitializeRetentionPeriodOptions()
        {
            RetentionPeriodOptions.Clear();
            RetentionPeriodOptions.Add("30");
            RetentionPeriodOptions.Add("90");
            RetentionPeriodOptions.Add("180");
            RetentionPeriodOptions.Add("365");
            RetentionPeriodOptions.Add("730");
        }

        private void LoadRetentionSettings()
        {
            InspectionDataRetentionSettings settings = _retentionSettingsStore.Load();
            IsFreeSpaceAutoCleanupEnabled = settings.IsFreeSpaceCleanupEnabled;
            MinimumFreeSpacePercentText = settings.MinimumFreeSpacePercent.ToString("0.###", CultureInfo.InvariantCulture);
            IsRetentionPeriodCleanupEnabled = settings.IsRetentionPeriodCleanupEnabled;
            RetentionDaysText = settings.RetentionDays.ToString(CultureInfo.InvariantCulture);
            RetentionStatusMessage = "검사 데이터 자동삭제 설정을 읽었습니다.";
        }

        /// <summary>
        /// Config.json의 활성 CUSTOM 사이트에서 검사 Score와 단일품목 유사도 기준을 읽습니다.
        /// 유사도 기준은 현재 SDK 검색 API가 제공될 때까지 저장 및 화면 표시 용도로만 유지합니다.
        /// </summary>
        private void LoadInspectionScoreSettings()
        {
            InspectionRuntimeSettings settings = _cameraConfigurationStore.LoadInspectionRuntimeSettings();
            InspectionPassScoreThreshold = (double)settings.InspectionPassScoreThreshold;
            SinglePartSimilarityThreshold = (double)settings.SinglePartSimilarityThreshold;
            ApplyInspectionPassScoreThreshold();
            InspectionScoreSettingsMessage = "Config.json 검사 Score 설정을 읽었습니다.";
        }

        private void ExecuteSaveInspectionScoreSettings(object parameter)
        {
            try
            {
                InspectionRuntimeSettings settings = new InspectionRuntimeSettings();
                settings.InspectionPassScoreThreshold = (decimal)InspectionPassScoreThreshold;
                settings.SinglePartSimilarityThreshold = (decimal)SinglePartSimilarityThreshold;
                _cameraConfigurationStore.SaveInspectionRuntimeSettings(settings);
                ApplyInspectionPassScoreThreshold();
                InspectionScoreSettingsMessage = "검사 Score 설정을 Config.json에 저장했습니다.";
            }
            catch (Exception ex)
            {
                InspectionScoreSettingsMessage = "검사 Score 설정 저장 실패: " + ex.Message;
                _messageDialogService.ShowWarning("검사 Score 설정", InspectionScoreSettingsMessage);
            }
        }

        private void ApplyInspectionPassScoreThreshold()
        {
            decimal threshold = (decimal)InspectionPassScoreThreshold;
            _inspectionWorkflowService.SetInspectionPassScoreThreshold(threshold);

            IInspectionScoreSettings scoreSettings = _aiInferenceService as IInspectionScoreSettings;
            if (scoreSettings != null)
            {
                scoreSettings.SetInspectionPassScoreThreshold(threshold);
            }
        }

        /// <summary>
        /// OCR 옵션에서 선택할 수 있는 해상도와 색상 모드를 초기화합니다.
        /// ADF, PNG, OCR 언어는 현장 표준으로 고정하므로 선택 목록에 노출하지 않습니다.
        /// </summary>
        private void InitializeOcrOptions()
        {
            OcrResolutionOptions.Clear();
            OcrResolutionOptions.Add(new OcrResolutionOption("300 DPI(기본)", 300));
            OcrResolutionOptions.Add(new OcrResolutionOption("400 DPI", 400));
            OcrResolutionOptions.Add(new OcrResolutionOption("600 DPI", 600));

            OcrColorModes.Clear();
            OcrColorModes.Add(new OcrColorModeOption("회색조(기본)", "gray"));
            OcrColorModes.Add(new OcrColorModeOption("흑백", "bw"));
            OcrColorModes.Add(new OcrColorModeOption("컬러", "color"));
        }

        /// <summary>
        /// 실행 폴더 CFG\OcrScannerSettings.json에서 해상도와 색상 모드만 복원합니다.
        /// </summary>
        private void LoadOcrConfiguration()
        {
            _isLoadingOcrConfiguration = true;
            try
            {
                OcrScanConfiguration configuration = _ocrScanService.LoadConfiguration();
                OcrResolutionDpi = configuration.ResolutionDpi;
                OcrColorMode = configuration.ColorMode;
            }
            finally
            {
                _isLoadingOcrConfiguration = false;
                _isOcrConfigurationLoaded = true;
            }

            OcrStatusText = "OCR 설정을 읽었습니다.";
            OcrLatestImagePath = string.Empty;
            OcrLatestPartNo = string.Empty;
            OcrLatestRawText = string.Empty;
            OcrLatestUsageText = string.Empty;
        }

        /// <summary>
        /// OCR 옵션을 사용자가 변경하면 별도 저장 버튼 없이 실행 폴더 CFG에 즉시 기록합니다.
        /// 초기 설정을 읽는 과정에서는 기존 값을 다시 저장하지 않습니다.
        /// </summary>
        private void SaveOcrConfigurationAfterSelectionChanged()
        {
            if (_isLoadingOcrConfiguration || !_isOcrConfigurationLoaded)
            {
                return;
            }

            if (OcrResolutionDpi <= 0 || string.IsNullOrWhiteSpace(OcrColorMode))
            {
                return;
            }

            try
            {
                OcrScanConfiguration configuration = new OcrScanConfiguration();
                configuration.ResolutionDpi = OcrResolutionDpi;
                configuration.ColorMode = OcrColorMode;
                _ocrScanService.SaveConfiguration(configuration);
                OcrStatusText = "OCR 설정을 자동 저장했습니다.";
            }
            catch (Exception exception)
            {
                OcrStatusText = "OCR 설정 자동 저장 실패: " + exception.Message;
            }
        }

        /// <summary>
        /// 메인 창이 표시된 뒤 OCR API와 USB 스캐너를 백그라운드에서 준비합니다.
        /// 생성자에서 동기 실행하지 않아 API 시작 시간이 메인 화면 표시를 막지 않게 합니다.
        /// </summary>
        /// <summary>
        /// 프로그램이 뜬 직후 AI 를 미리 깨워 둡니다.
        ///
        /// <para>
        /// 첫 검사의 첫 장에만 얹히는 준비 시간이 있습니다. 현장에서 8 초, 사무실 노트북에서
        /// 71 초였습니다. 사람이 검사 버튼을 누르고 기다리는 자리에서 그 값을 치르지 않도록
        /// 아무도 기다리지 않는 지금 미리 치릅니다.
        /// </para>
        /// </summary>
        public void BeginWarmup()
        {
            if (_aiInferenceService == null)
            {
                return;
            }

            try
            {
                Part warmupPart;
                string warmupImagePath;
                FindWarmupSample(out warmupPart, out warmupImagePath);

                _aiInferenceService.BeginWarmup(warmupPart, warmupImagePath);
            }
            catch (Exception ex)
            {
                // 깨우기는 없어도 되는 일입니다. 실패로 화면이 뜨지 않으면 안 됩니다.
                System.Diagnostics.Debug.WriteLine("AI 깨우기를 시작하지 못했습니다: " + ex.Message);
            }
        }

        /// <summary>
        /// 깨우기에 쓸 부품과 사진을 하나 고릅니다.
        ///
        /// <para>
        /// 실제 검사와 같은 조합으로 지나가야 그 자리에서 하는 준비가 끝납니다.
        /// 등록된 부품 중 기준 이미지가 실제로 있는 첫 번째를 씁니다.
        /// 하나도 없으면 둘 다 비워 돌려주고, 그때는 빈 그림으로 지나갑니다.
        /// </para>
        ///
        /// <para>
        /// 부품이 만 건이 넘으므로 처음 몇 건만 봅니다. 깨우려고 목록 전체를 훑을 까닭이 없습니다.
        /// </para>
        /// </summary>
        private void FindWarmupSample(out Part warmupPart, out string imageFilePath)
        {
            warmupPart = null;
            imageFilePath = string.Empty;

            IList<Part> parts = _partDataStore.GetParts();
            if (parts == null || parts.Count == 0)
            {
                return;
            }

            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(AppContext.BaseDirectory);
            int examined = 0;
            foreach (Part part in parts)
            {
                if (examined >= WarmupSampleSearchLimit)
                {
                    return;
                }

                examined++;
                if (part == null || part.Images == null || part.Images.Count == 0)
                {
                    continue;
                }

                foreach (PartImage image in part.Images)
                {
                    if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
                    {
                        continue;
                    }

                    string resolvedPath = pathSettings.ResolveImageFilePath(image.FilePath);
                    if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
                    {
                        warmupPart = part;
                        imageFilePath = resolvedPath;
                        return;
                    }
                }
            }
        }

        /// <summary>깨우기에 쓸 부품을 찾을 때 살펴볼 최대 건수입니다.</summary>
        private const int WarmupSampleSearchLimit = 50;

        public void BeginInitialOcrStatusRefresh()
        {
            BeginOcrStatusRefresh(true);
        }

        private void BeginOcrStatusRefresh(bool isInitialRefresh)
        {
            if (_isDisposed || _isInitialOcrStatusRefreshRunning || IsOcrScanRunning)
            {
                return;
            }

            _isInitialOcrStatusRefreshRunning = true;
            OcrScannerStatusText = "시작 중";
            OcrStatusText = isInitialRefresh
                ? "OCR API를 백그라운드에서 준비하고 있습니다."
                : "Epson ES-C320W 연결 상태를 확인하고 있습니다.";

            Task.Factory.StartNew(
                    delegate { return _ocrScanService.RefreshScanners(); },
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default)
                .ContinueWith(OnOcrStatusRefreshCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnOcrStatusRefreshCompleted(Task<IList<OcrScannerDevice>> task)
        {
            _isInitialOcrStatusRefreshRunning = false;
            if (_isDisposed)
            {
                return;
            }

            if (task.IsFaulted)
            {
                string errorMessage = task.Exception == null
                    ? "알 수 없는 OCR 초기화 오류"
                    : task.Exception.GetBaseException().Message;
                OcrScannerStatusText = "오류";
                OcrScannerDeviceId = "Epson ES-C320W 상태를 확인하지 못했습니다.";
                OcrStatusText = "OCR API 준비 실패: " + errorMessage;
                return;
            }

            IList<OcrScannerDevice> scanners = task.Result;
            if (scanners == null || scanners.Count == 0)
            {
                OcrScannerStatusText = "오류";
                OcrScannerDeviceId = "Epson ES-C320W를 찾을 수 없습니다.";
                OcrStatusText = "OCR API는 응답했지만 Epson ES-C320W가 연결되지 않았습니다.";
                return;
            }

            OcrScannerStatusText = "준비 완료: " + scanners[0].DisplayName;
            OcrScannerDeviceId = scanners[0].DeviceId;
            OcrStatusText = "OCR API와 Epson ES-C320W가 준비되었습니다.";
        }

        private void RestoreOcrReadyStatus()
        {
            if (OcrScannerStatusText.StartsWith("스캔 중", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(OcrScannerDeviceId) &&
                !OcrScannerDeviceId.StartsWith("Epson ES-C320W를 찾을 수 없습니다", StringComparison.Ordinal))
            {
                OcrScannerStatusText = "준비 완료: Epson ES-C320W";
            }
        }

        private void ExecuteSaveRetentionSettings(object parameter)
        {
            InspectionDataRetentionSettings settings;
            string validationMessage;
            if (!TryBuildRetentionSettings(out settings, out validationMessage))
            {
                RetentionStatusMessage = validationMessage;
                _messageDialogService.ShowWarning("검사 데이터 자동삭제 설정", validationMessage);
                return;
            }

            try
            {
                _retentionSettingsStore.Save(settings);
                StartRetentionMonitorTimer();
                RetentionStatusMessage = "검사 데이터 자동삭제 설정을 저장했습니다.";
            }
            catch (Exception ex)
            {
                RetentionStatusMessage = "검사 데이터 자동삭제 설정 저장 실패: " + ex.Message;
            }
        }

        private bool TryBuildRetentionSettings(out InspectionDataRetentionSettings settings, out string validationMessage)
        {
            settings = new InspectionDataRetentionSettings();
            validationMessage = string.Empty;

            decimal freeSpacePercent;
            if (!decimal.TryParse(MinimumFreeSpacePercentText, NumberStyles.Number, CultureInfo.InvariantCulture, out freeSpacePercent) &&
                !decimal.TryParse(MinimumFreeSpacePercentText, NumberStyles.Number, CultureInfo.CurrentCulture, out freeSpacePercent))
            {
                validationMessage = "HDD 여유공간 기준은 숫자로 입력하세요.";
                return false;
            }

            if (freeSpacePercent < 1m || freeSpacePercent > 99m)
            {
                validationMessage = "HDD 여유공간 기준은 1~99% 사이로 입력하세요.";
                return false;
            }

            int retentionDays;
            if (!int.TryParse(RetentionDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out retentionDays) &&
                !int.TryParse(RetentionDaysText, NumberStyles.Integer, CultureInfo.CurrentCulture, out retentionDays))
            {
                validationMessage = "설정기간은 일 단위 숫자로 입력하세요.";
                return false;
            }

            if (retentionDays <= 0)
            {
                validationMessage = "설정기간은 1일 이상이어야 합니다.";
                return false;
            }

            settings.IsFreeSpaceCleanupEnabled = IsFreeSpaceAutoCleanupEnabled;
            settings.MinimumFreeSpacePercent = freeSpacePercent;
            settings.IsRetentionPeriodCleanupEnabled = IsRetentionPeriodCleanupEnabled;
            settings.RetentionDays = retentionDays;
            return true;
        }

        private void StartRetentionMonitorTimer()
        {
            if (_retentionMonitorTimer == null)
            {
                return;
            }

            _retentionMonitorTimer.Stop();
            _referenceImagePopupService.Close();
            if (IsFreeSpaceAutoCleanupEnabled || IsRetentionPeriodCleanupEnabled)
            {
                _retentionMonitorTimer.Start();
            }
        }

        private void OnRetentionMonitorTimerTick(object sender, EventArgs e)
        {
            CheckInspectionDataRetentionPolicy();
        }

        private void CheckInspectionDataRetentionPolicy()
        {
            if (_isRetentionCleanupPromptVisible)
            {
                return;
            }

            InspectionDataRetentionSettings settings;
            string validationMessage;
            if (!TryBuildRetentionSettings(out settings, out validationMessage))
            {
                RetentionStatusMessage = validationMessage;
                return;
            }

            if (!settings.IsFreeSpaceCleanupEnabled && !settings.IsRetentionPeriodCleanupEnabled)
            {
                return;
            }

            InspectionDataCleanupCandidate candidate = _inspectionDataRetentionService.BuildCleanupCandidate(settings);
            if (candidate == null)
            {
                RetentionStatusMessage = "검사 데이터 자동삭제 후보가 없습니다.";
                return;
            }

            _isRetentionCleanupPromptVisible = true;
            try
            {
                bool confirmed = _messageDialogService.ShowConfirmation(
                    "검사 데이터 자동삭제 확인",
                    candidate.BuildConfirmationMessage());
                if (!confirmed)
                {
                    RetentionStatusMessage = "검사 데이터 자동삭제를 취소했습니다.";
                    return;
                }

                InspectionDataCleanupResult result = _inspectionDataRetentionService.DeleteCandidate(candidate);
                RetentionStatusMessage = result.Message;
                RefreshHistory();
                RefreshStatistics();
                RefreshDiskUsages();
            }
            catch (Exception ex)
            {
                RetentionStatusMessage = "검사 데이터 자동삭제 실패: " + ex.Message;
            }
            finally
            {
                _isRetentionCleanupPromptVisible = false;
            }
        }

        /// <summary>
        /// 옵션 화면의 카메라 연결/설정 상태를 최신 값으로 갱신합니다.
        /// 실제 SDK가 연결되면 이 목록에서 6대 카메라의 연결 성공 여부와 마지막 프레임 정보를 확인합니다.
        /// </summary>
        private void RefreshCameraStatuses(bool verifyVideoSignal)
        {
            try
            {
                IList<CameraChannelStatus> statuses = verifyVideoSignal ? BuildVerifiedCameraStatuses() : _cameraService.GetChannelStatuses();
                ApplyCameraStatuses(statuses);

                CameraStatusMessage = verifyVideoSignal
                    ? "카메라 채널 " + CameraChannels.Count.ToString() + "개 영상 수신 상태를 확인했습니다."
                    : "카메라 채널 " + CameraChannels.Count.ToString() + "개 설정 상태를 읽었습니다.";
            }
            catch (Exception ex)
            {
                CameraStatusMessage = "카메라 상태 조회 실패: " + ex.Message;
            }
        }

        /// <summary>
        /// 창이 화면에 표시된 뒤 실제 카메라 영상 수신 여부를 백그라운드에서 확인합니다.
        /// 생성자에서 실행하면 RTSP 연결 대기 시간만큼 첫 화면이 늦게 열리므로, Loaded 이후에 실행합니다.
        /// </summary>
        public void BeginInitialCameraStatusRefresh()
        {
            if (_isDisposed || _isInitialCameraStatusRefreshRunning)
            {
                return;
            }

            _isInitialCameraStatusRefreshRunning = true;
            CameraStatusMessage = "카메라 실제 영상 수신 상태를 확인하고 있습니다.";

            Task.Factory.StartNew(BuildVerifiedCameraStatuses, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                .ContinueWith(OnInitialCameraStatusRefreshCompleted, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void OnInitialCameraStatusRefreshCompleted(Task<IList<CameraChannelStatus>> task)
        {
            _isInitialCameraStatusRefreshRunning = false;
            if (_isDisposed)
            {
                return;
            }

            if (task.IsFaulted)
            {
                CameraStatusMessage = "시작 시 카메라 상태 확인 실패: " +
                                      TrimLivePreviewMessage(task.Exception == null ? string.Empty : task.Exception.Message);
                return;
            }

            ApplyCameraStatuses(task.Result);
            CameraStatusMessage = "프로그램 시작 시 카메라 채널 " + CameraChannels.Count.ToString() + "개 영상 수신 상태를 확인했습니다.";
        }

        private void ApplyCameraStatuses(IList<CameraChannelStatus> statuses)
        {
            // 이 메서드는 검사 진행, 라이브 미리보기 타이머 등 백그라운드 흐름에서도 호출됩니다.
            // 무조건 첫 채널로 선택을 되돌리면 사용자가 옵션-카메라에서 다른 채널을 보고 있다가
            // 갑자기 Top 채널로 튕기면서 확인하던 정보가 사라집니다. 기존 선택을 유지합니다.
            ImageViewType? previousViewType = SelectedCameraChannel == null
                ? (ImageViewType?)null
                : SelectedCameraChannel.ViewTypeValue;

            CameraChannels.Clear();
            if (statuses == null)
            {
                return;
            }

            foreach (CameraChannelStatus status in statuses)
            {
                CameraChannels.Add(new CameraChannelStatusViewModel(status));
            }

            if (CameraChannels.Count == 0)
            {
                return;
            }

            if (previousViewType.HasValue)
            {
                foreach (CameraChannelStatusViewModel channel in CameraChannels)
                {
                    if (channel.ViewTypeValue == previousViewType.Value)
                    {
                        SelectedCameraChannel = channel;
                        return;
                    }
                }
            }

            SelectedCameraChannel = CameraChannels[0];
        }

        private IList<CameraChannelStatus> BuildVerifiedCameraStatuses()
        {
            IList<CameraChannelStatus> verifiedStatuses = new List<CameraChannelStatus>();
            IList<CameraChannelStatus> currentStatuses = _cameraService.GetChannelStatuses();

            foreach (CameraChannelStatus status in currentStatuses)
            {
                if (status.IsEnabled)
                {
                    try
                    {
                        verifiedStatuses.Add(_cameraService.TestChannelConnection(status.ViewType));
                    }
                    catch (Exception ex)
                    {
                        status.IsConnected = false;
                        status.Message = "영상 수신 확인 실패: " + TrimLivePreviewMessage(ex.Message);
                        status.CheckedAt = DateTime.Now;
                        verifiedStatuses.Add(status);
                    }
                }
                else
                {
                    verifiedStatuses.Add(status);
                }
            }

            return verifiedStatuses;
        }

        private void ExecuteRefreshCameraStatus(object parameter)
        {
            RefreshDiskUsages();
            RefreshCameraStatuses(true);
        }

        private void ExecuteReloadCameraConfiguration(object parameter)
        {
            try
            {
                _cameraService.ReloadConfiguration();
                ApplyLiveStreamUrls();
                RefreshDiskUsages();
                RefreshCameraStatuses(false);
                CameraStatusMessage = "카메라 설정을 다시 읽었습니다.";
            }
            catch (Exception ex)
            {
                CameraStatusMessage = "카메라 설정 다시 읽기 실패: " + ex.Message;
            }
        }

        private void ExecuteSaveCameraConfiguration(object parameter)
        {
            try
            {
                IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
                foreach (CameraChannelStatusViewModel channel in CameraChannels)
                {
                    channels.Add(channel.ToConfig());
                }

                _cameraService.SaveChannelConfigurations(channels);
                ApplyLiveStreamUrls();
                RefreshDiskUsages();
                // 설정 저장 단계에서 RTSP 연결 및 프레임 수신을 동기 검사하면
                // 채널별 네트워크 대기 시간 동안 UI 스레드가 멈춥니다.
                // 실제 연결 검증은 상태 새로고침 또는 선택 카메라 연결 테스트에서 명시적으로 수행합니다.
                RefreshCameraStatuses(false);
                CameraStatusMessage = "카메라 설정을 저장했습니다. 실제 영상 연결 상태는 상태 새로고침에서 확인하세요.";
            }
            catch (Exception ex)
            {
                CameraStatusMessage = "카메라 설정 저장 실패: " + ex.Message;
            }
        }

        private void ExecuteTestSelectedCameraConnection(object parameter)
        {
            try
            {
                if (SelectedCameraChannel == null)
                {
                    CameraStatusMessage = "연결 테스트할 카메라 채널을 선택하세요.";
                    return;
                }

                _cameraService.SaveChannelConfigurations(BuildCameraConfigurationList());
                CameraChannelStatus status = _cameraService.TestChannelConnection(SelectedCameraChannel.ViewTypeValue);
                SelectedCameraChannel.ApplyStatus(status);
                ApplyLiveStreamUrls();
                CameraStatusMessage = SelectedCameraChannel.DisplayName + " 연결 테스트: " + SelectedCameraChannel.Message;
            }
            catch (Exception ex)
            {
                CameraStatusMessage = "카메라 연결 테스트 실패: " + ex.Message;
            }
        }

        private bool CanStartImageTraining(object parameter)
        {
            return !_isImageTrainingRunning;
        }

        private void ExecuteStartImageTraining(object parameter)
        {
            StartImageTraining("옵션 학습 바로시작");
        }

        private void ExecuteApplyImageTrainingSchedule(object parameter)
        {
            if (!IsTrainingReservationEnabled)
            {
                CancelImageTrainingSchedule("이미지 학습 예약이 비활성 상태입니다.");
                return;
            }

            DateTime scheduledAt;
            string validationMessage;
            if (!TryParseTrainingScheduleText(out scheduledAt, out validationMessage))
            {
                TrainingStatusMessage = validationMessage;
                _messageDialogService.ShowWarning("이미지 학습 예약 실패", validationMessage);
                return;
            }

            ScheduleImageTraining(scheduledAt, "옵션 예약설정");
        }

        private void ExecuteApplyDailyImageTrainingSchedule(object parameter)
        {
            if (!IsDailyTrainingEnabled)
            {
                TrainingStatusMessage = "매일 학습 예약이 비활성 상태입니다.";
                UpdateTrainingScheduleTimerState();
                return;
            }

            TimeSpan dailyTime;
            string validationMessage;
            if (!TryParseDailyTrainingTime(out dailyTime, out validationMessage))
            {
                TrainingStatusMessage = validationMessage;
                _messageDialogService.ShowWarning("매일 이미지 학습 예약 실패", validationMessage);
                return;
            }

            _lastDailyTrainingDate = null;
            _appliedDailyTrainingTime = dailyTime;
            UpdateTrainingScheduleTimerState();
            TrainingStatusMessage = "매일 이미지 학습 예약: " + dailyTime.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        private void ExecuteClearTrainingProcessMessages(object parameter)
        {
            TrainingProcessMessages.Clear();
        }

        private void OnTrainingScheduleTimerTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            if (_scheduledImageTrainingAt.HasValue &&
                now >= _scheduledImageTrainingAt.Value &&
                !_isImageTrainingRunning)
            {
                DateTime scheduledAt = _scheduledImageTrainingAt.Value;
                _scheduledImageTrainingAt = null;
                _isTrainingReservationEnabled = false;
                OnPropertyChanged("IsTrainingReservationEnabled");
                StartImageTraining("예약 이미지 학습 " + scheduledAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            }

            if (IsDailyTrainingEnabled && _appliedDailyTrainingTime.HasValue && !_isImageTrainingRunning)
            {
                if (now.TimeOfDay >= _appliedDailyTrainingTime.Value &&
                    (!_lastDailyTrainingDate.HasValue || _lastDailyTrainingDate.Value.Date != now.Date))
                {
                    StartImageTraining("매일 이미지 학습 " + now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    if (_isImageTrainingRunning)
                    {
                        _lastDailyTrainingDate = now.Date;
                    }
                }
            }

            UpdateTrainingScheduleTimerState();
        }

        private void StartImageTraining(string source)
        {
            if (_isImageTrainingRunning)
            {
                TrainingStatusMessage = "이미지 학습 시작 요청이 이미 진행 중입니다.";
                return;
            }

            _isImageTrainingRunning = true;
            OnPropertyChanged("IsImageTrainingRunning");
            _trainingStartedAt = DateTime.Now;
            _trainingEndedAt = null;
            TrainingCurrentStatus = "시작 중";
            TrainingProgress = 0;
            TrainingCurrentMessage = source;
            TrainingErrorCode = string.Empty;
            TrainingErrorMessage = string.Empty;
            OnPropertyChanged("TrainingTimeSummary");
            RaiseStartImageTrainingCommandState();
            TrainingStatusMessage = source + " 요청을 VLAD_AI로 전달 중입니다.";
            try
            {
                string message = _aiInferenceService.StartImageTraining();
                AddTrainingProcessMessage("PROCESS", "START", string.Empty, message, message);
                TrainingCurrentStatus = "실행 중";
                TrainingCurrentMessage = message;
                TrainingStatusMessage = source + " 시작: " + message;
            }
            catch (Exception ex)
            {
                _isImageTrainingRunning = false;
                OnPropertyChanged("IsImageTrainingRunning");
                _trainingEndedAt = DateTime.Now;
                TrainingCurrentStatus = "실패";
                TrainingErrorCode = "START_FAILED";
                TrainingErrorMessage = ex.Message;
                TrainingCurrentMessage = "학습 프로그램 실행 실패";
                TrainingStatusMessage = source + " 실패: " + ex.Message;
                AddTrainingProcessMessage("PROCESS", "START_FAILED", string.Empty, ex.Message, ex.ToString());
                OnPropertyChanged("TrainingTimeSummary");
                RaiseStartImageTrainingCommandState();
            }
        }

        private void PromptImageTrainingAfterImageChange(string reason)
        {
            ImageTrainingPromptResult promptResult = _messageDialogService.ShowImageTrainingPrompt(
                "이미지 학습 실행",
                reason + "\r\n\r\n기준 이미지 변경 사항을 학습에 반영하시겠습니까?",
                GetDefaultImageTrainingScheduleTime());

            if (promptResult == null || !promptResult.IsAccepted)
            {
                TrainingStatusMessage = "이미지 학습 실행 선택을 취소했습니다.";
                return;
            }

            if (promptResult.StartNow)
            {
                StartImageTraining("기준 이미지 변경 후 즉시 학습");
                return;
            }

            if (promptResult.ScheduledAt.HasValue)
            {
                IsTrainingReservationEnabled = true;
                ScheduleImageTraining(promptResult.ScheduledAt.Value, "기준 이미지 변경 후 예약 학습");
            }
        }

        private void ScheduleImageTraining(DateTime scheduledAt, string source)
        {
            if (scheduledAt <= DateTime.Now)
            {
                TrainingStatusMessage = "이미지 학습 예약시간은 현재시간 이후로 설정하세요.";
                return;
            }

            _scheduledImageTrainingAt = scheduledAt;
            TrainingScheduleText = scheduledAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            IsTrainingReservationEnabled = true;
            UpdateTrainingScheduleTimerState();
            TrainingStatusMessage = source + " 예약: " + TrainingScheduleText;
        }

        private void CancelImageTrainingSchedule(string message)
        {
            _scheduledImageTrainingAt = null;
            UpdateTrainingScheduleTimerState();
            TrainingStatusMessage = message;
        }

        private DateTime GetDefaultImageTrainingScheduleTime()
        {
            DateTime scheduledAt;
            string validationMessage;
            if (TryParseTrainingScheduleText(out scheduledAt, out validationMessage) && scheduledAt > DateTime.Now)
            {
                return scheduledAt;
            }

            return DateTime.Now.AddHours(1);
        }

        private bool TryParseTrainingScheduleText(out DateTime scheduledAt, out string validationMessage)
        {
            scheduledAt = DateTime.MinValue;
            validationMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(TrainingScheduleText))
            {
                validationMessage = "이미지 학습 예약시간을 입력하세요.";
                return false;
            }

            string[] formats = new[]
            {
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd H:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd H:mm:ss"
            };

            if (!DateTime.TryParseExact(
                    TrainingScheduleText.Trim(),
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out scheduledAt) &&
                !DateTime.TryParse(TrainingScheduleText.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out scheduledAt))
            {
                validationMessage = "이미지 학습 예약시간 형식은 yyyy-MM-dd HH:mm 입니다.";
                return false;
            }

            if (scheduledAt <= DateTime.Now)
            {
                validationMessage = "이미지 학습 예약시간은 현재시간 이후로 설정하세요.";
                return false;
            }

            return true;
        }

        private bool TryParseDailyTrainingTime(out TimeSpan dailyTime, out string validationMessage)
        {
            dailyTime = TimeSpan.Zero;
            validationMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(DailyTrainingTimeText))
            {
                validationMessage = "매일 이미지 학습 시간을 입력하세요.";
                return false;
            }

            string[] formats = new[] { @"hh\:mm", @"h\:mm" };
            if (!TimeSpan.TryParseExact(
                    DailyTrainingTimeText.Trim(),
                    formats,
                    CultureInfo.InvariantCulture,
                    out dailyTime))
            {
                validationMessage = "매일 이미지 학습 시간 형식은 HH:mm 입니다.";
                return false;
            }

            if (dailyTime < TimeSpan.Zero || dailyTime >= TimeSpan.FromDays(1))
            {
                validationMessage = "매일 이미지 학습 시간은 00:00부터 23:59 사이여야 합니다.";
                return false;
            }

            return true;
        }

        private void UpdateTrainingScheduleTimerState()
        {
            if (_trainingScheduleTimer == null)
            {
                return;
            }

            bool shouldRun = _scheduledImageTrainingAt.HasValue ||
                             (IsDailyTrainingEnabled && _appliedDailyTrainingTime.HasValue);
            if (shouldRun && !_trainingScheduleTimer.IsEnabled)
            {
                _trainingScheduleTimer.Start();
            }
            else if (!shouldRun && _trainingScheduleTimer.IsEnabled)
            {
                _trainingScheduleTimer.Stop();
            }
        }

        private void OnTrainingOutputReceived(object sender, TrainingProcessDataEventArgs e)
        {
            if (_uiDispatcher.CheckAccess())
            {
                HandleTrainingOutputReceived(e);
                return;
            }

            _uiDispatcher.BeginInvoke(new Action<TrainingProcessDataEventArgs>(HandleTrainingOutputReceived), e);
        }

        private void OnTrainingErrorReceived(object sender, TrainingProcessDataEventArgs e)
        {
            if (_uiDispatcher.CheckAccess())
            {
                HandleTrainingErrorReceived(e);
                return;
            }

            _uiDispatcher.BeginInvoke(new Action<TrainingProcessDataEventArgs>(HandleTrainingErrorReceived), e);
        }

        private void OnTrainingExited(object sender, TrainingProcessExitedEventArgs e)
        {
            if (_uiDispatcher.CheckAccess())
            {
                HandleTrainingExited(e);
                return;
            }

            _uiDispatcher.BeginInvoke(new Action<TrainingProcessExitedEventArgs>(HandleTrainingExited), e);
        }

        private void HandleTrainingOutputReceived(TrainingProcessDataEventArgs e)
        {
            string type;
            string value;
            string message;
            ParseTrainingProtocol(e.Data, out type, out value, out message);
            AddTrainingProcessMessage("STDOUT", type, value, message, e.Data);

            switch (type)
            {
                case "START":
                    TrainingCurrentStatus = "실행 중";
                    TrainingProgress = 0;
                    TrainingCurrentMessage = message;
                    break;

                case "PROGRESS":
                    int progress;
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out progress))
                    {
                        TrainingProgress = Math.Max(0, Math.Min(100, progress));
                    }

                    TrainingCurrentStatus = "실행 중";
                    TrainingCurrentMessage = message;
                    break;

                case "DONE":
                    TrainingCurrentStatus = "프로세스 종료 대기";
                    TrainingProgress = 100;
                    TrainingCurrentMessage = message;
                    TrainingStatusMessage = "학습 완료 메시지를 수신했습니다. 프로세스 종료 후 VLAD를 재초기화합니다.";
                    break;

                case "ERROR":
                    TrainingCurrentStatus = "실패";
                    TrainingErrorCode = value;
                    TrainingErrorMessage = message;
                    TrainingCurrentMessage = message;
                    break;

                case "CANCELED":
                    TrainingCurrentStatus = "취소";
                    TrainingCurrentMessage = message;
                    break;

                case "WARN":
                case "LOG":
                    TrainingCurrentMessage = message;
                    break;
            }

            OnPropertyChanged("TrainingTimeSummary");
        }

        private void HandleTrainingErrorReceived(TrainingProcessDataEventArgs e)
        {
            string type;
            string value;
            string message;
            ParseTrainingProtocol(e.Data, out type, out value, out message);
            AddTrainingProcessMessage("STDERR", type, value, message, e.Data);
            TrainingCurrentMessage = message;
        }

        private void HandleTrainingExited(TrainingProcessExitedEventArgs e)
        {
            _isImageTrainingRunning = false;
            OnPropertyChanged("IsImageTrainingRunning");
            _trainingEndedAt = DateTime.Now;
            string exitCode = e.ExitCode.HasValue
                ? e.ExitCode.Value.ToString(CultureInfo.InvariantCulture)
                : "unknown";
            AddTrainingProcessMessage("PROCESS", "EXITED", exitCode, e.ReloadMessage, "ExitCode=" + exitCode);

            bool completed = e.ExitCode.HasValue &&
                             e.ExitCode.Value == 0 &&
                             e.CompletionMessageReceived &&
                             !e.TerminalErrorMessageReceived &&
                             e.ReloadAttempted &&
                             e.ReloadSucceeded;
            if (completed)
            {
                TrainingCurrentStatus = "완료";
                TrainingProgress = 100;
                TrainingCurrentMessage = e.ReloadMessage;
                TrainingStatusMessage = e.ReloadMessage;
                TrainingErrorCode = string.Empty;
                TrainingErrorMessage = string.Empty;
            }
            else
            {
                TrainingCurrentStatus = e.TerminalErrorMessageReceived ? "실패" : "비정상 종료";
                TrainingErrorCode = e.ReloadAttempted && !e.ReloadSucceeded
                    ? "VLAD_RELOAD_FAILED"
                    : "TRAINING_NOT_COMPLETED";
                TrainingErrorMessage = e.ReloadMessage;
                TrainingCurrentMessage = e.ReloadMessage;
                TrainingStatusMessage = e.ReloadMessage;
            }

            OnPropertyChanged("TrainingTimeSummary");
            RaiseStartImageTrainingCommandState();

            RestoreCameraStreamsAfterTraining();
        }

        /// <summary>
        /// 학습이 끝난 뒤 카메라 화면을 다시 붙입니다.
        ///
        /// <para>
        /// 학습이 끝나면 VLAD 런타임을 다시 올립니다. 그때 예전 세션의 callback 등록과 프레임
        /// 두는 자리가 모두 지워지고 새 세션으로 다시 등록됩니다. 검사 쪽은 그 일을 스스로
        /// 하지만, 화면 쪽은 아무도 다시 붙여 주지 않았습니다. 그래서 학습 뒤에는 영상이
        /// 멈춘 채로 있었고 프로그램을 다시 켜야 했습니다.
        /// </para>
        ///
        /// <para>
        /// 카메라 설정을 다시 읽는 것과 같은 일을 합니다. 실패해도 학습 결과를 알리는 데는
        /// 지장이 없어야 하므로 여기서 막지 않습니다.
        /// </para>
        /// </summary>
        private void RestoreCameraStreamsAfterTraining()
        {
            try
            {
                ApplyLiveStreamUrls();
                RefreshCameraStatuses(false);
                AddTrainingProcessMessage("SYSTEM", "STREAM", string.Empty,
                    "학습이 끝나 카메라 화면을 다시 붙였습니다.", "STREAM_RESTORED");
            }
            catch (Exception ex)
            {
                AddTrainingProcessMessage("SYSTEM", "STREAM", string.Empty,
                    "카메라 화면을 다시 붙이지 못했습니다. " + ex.Message, "STREAM_RESTORE_FAILED");
            }
        }

        private static void ParseTrainingProtocol(
            string raw,
            out string type,
            out string value,
            out string message)
        {
            type = string.Empty;
            value = string.Empty;
            message = raw ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            string[] parts = raw.Split(new[] { '|' }, 3);
            if (parts.Length < 2)
            {
                return;
            }

            type = parts[0].Trim().ToUpperInvariant();
            value = parts[1].Trim();
            message = parts.Length > 2 ? parts[2].Trim() : string.Empty;
        }

        private void AddTrainingProcessMessage(
            string source,
            string type,
            string value,
            string message,
            string raw)
        {
            // 새 소식을 맨 위에 놓습니다.
            //
            // 아래로 쌓으면 학습이 어디까지 갔는지 보려고 매번 끝까지 내려야 했습니다.
            // 화면은 목록을 따라 내려가지 않으므로 보고 있던 자리는 그대로 있습니다.
            TrainingProcessMessages.Insert(0, new TrainingProcessMessageRowViewModel
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                Source = source ?? string.Empty,
                Type = type ?? string.Empty,
                Value = value ?? string.Empty,
                Message = message ?? string.Empty,
                Raw = raw ?? string.Empty
            });

            // 오래된 것부터 버립니다. 맨 위가 새 것이므로 버릴 것은 맨 아래에 있습니다.
            while (TrainingProcessMessages.Count > 1000)
            {
                TrainingProcessMessages.RemoveAt(TrainingProcessMessages.Count - 1);
            }
        }

        private void RaiseStartImageTrainingCommandState()
        {
            RelayCommand command = StartImageTrainingCommand as RelayCommand;
            if (command != null)
            {
                command.RaiseCanExecuteChanged();
            }
        }

        private IList<CameraChannelConfig> BuildCameraConfigurationList()
        {
            IList<CameraChannelConfig> channels = new List<CameraChannelConfig>();
            foreach (CameraChannelStatusViewModel channel in CameraChannels)
            {
                channels.Add(channel.ToConfig());
            }

            return channels;
        }

        private void ExecuteShowInspectionTab(object parameter)
        {
            SelectedTabIndex = 0;
        }

        private void ExecuteShowDbTab(object parameter)
        {
            SelectedTabIndex = 1;
        }

        private void ExecuteShowRegistrationTab(object parameter)
        {
            SelectedTabIndex = 2;
        }

        private void ExecuteShowHistoryTab(object parameter)
        {
            SelectedTabIndex = 3;
        }

        private void ExecuteShowStatisticsTab(object parameter)
        {
            RefreshStatistics();
            SelectedTabIndex = 4;
        }

        private void RaiseRunCommandState()
        {
            RelayCommand runCommand = RunInspectionCommand as RelayCommand;
            if (runCommand != null)
            {
                runCommand.RaiseCanExecuteChanged();
            }

            RelayCommand resetCommand = ResetInspectionScreenCommand as RelayCommand;
            if (resetCommand != null)
            {
                resetCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// OCR 스캔 중에는 같은 ADF 장치에 중복 요청하지 않도록 검색 버튼의 실행 상태를 갱신합니다.
        /// </summary>
        private void RaiseOcrCommandState()
        {
            RelayCommand ocrCommand = StartOcrScanCommand as RelayCommand;
            if (ocrCommand != null)
            {
                ocrCommand.RaiseCanExecuteChanged();
            }

            RelayCommand registrationOcrCommand = RegistrationOcrInputCommand as RelayCommand;
            if (registrationOcrCommand != null)
            {
                registrationOcrCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 창 종료 시 타이머, 외부 학습 프로세스 이벤트, Vision worker를 순서대로 해제합니다.
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;
            // 정상 종료 시에도 저장하지 않은 기준/좌표 작업 파일은 남기지 않습니다.
            ClearTemporaryReferenceImagesForCurrentRegistrationPart();
            // DB 저장 전 등록 OCR을 취소하고 프로그램을 닫는 경우에도 OCR_PATH 임시 파일을 남기지 않습니다.
            ClearRegistrationOcrTemporaryFiles();
            ClearLatestRegistrationOcrResult();

            _mainSearchDelayTimer.Stop();
            _searchDelayTimer.Stop();
            _livePreviewTimer.Stop();
            _trainingScheduleTimer.Stop();
            _retentionMonitorTimer.Stop();

            _aiInferenceService.TrainingOutputReceived -= OnTrainingOutputReceived;
            _aiInferenceService.TrainingErrorReceived -= OnTrainingErrorReceived;
            _aiInferenceService.TrainingExited -= OnTrainingExited;
            _inspectionWorkflowService.ProgressChanged -= OnInspectionProgressChanged;

            // OCR API는 외부 x86 프로세스이므로 종료 요청 초기에 먼저 정리합니다.
            // Vision/카메라 네이티브 종료가 지연되더라도 EpsonScanApi.exe가 남지 않게 하기 위한 순서입니다.
            IDisposable disposableOcrService = _ocrScanService as IDisposable;
            if (disposableOcrService != null)
            {
                disposableOcrService.Dispose();
            }

            IDisposable disposableAiService = _aiInferenceService as IDisposable;
            if (disposableAiService != null)
            {
                disposableAiService.Dispose();
            }

            IDisposable disposableCameraService = _cameraService as IDisposable;
            if (disposableCameraService != null)
            {
                disposableCameraService.Dispose();
            }
        }

        private class LivePreviewRefreshResult
        {
            public LivePreviewRefreshResult()
            {
                Frames = new List<LivePreviewFrameResult>();
            }

            public IList<LivePreviewFrameResult> Frames { get; private set; }

            public int EnabledChannelCount { get; set; }

            public int SuccessCount { get; set; }

            public int FailureCount { get; set; }

            public string ConfigurationErrorMessage { get; set; }
        }

        private class LivePreviewFrameResult
        {
            public ImageViewType ViewType { get; set; }

            public string DisplayName { get; set; }

            public bool IsSuccess { get; set; }

            public string FilePath { get; set; }

            public string Message { get; set; }
        }
    }
}
