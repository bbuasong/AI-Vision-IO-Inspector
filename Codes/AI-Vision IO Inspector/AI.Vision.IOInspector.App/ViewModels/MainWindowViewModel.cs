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
            AddReferenceImageCommand = new RelayCommand(ExecuteAddReferenceImage);
            SaveCurrentCameraImagesCommand = new RelayCommand(ExecuteSaveCurrentCameraImages);
            CheckReferenceImageSimilarityCommand = new RelayCommand(ExecuteCheckReferenceImageSimilarity);
            ClearReferenceImageSimilarityCommand = new RelayCommand(ExecuteClearReferenceImageSimilarity);
            RefreshLivePreviewCommand = new RelayCommand(ExecuteRefreshLivePreview);
            DeleteAllReferenceImagesCommand = new RelayCommand(ExecuteDeleteAllReferenceImages);
            ImportPartsCsvCommand = new RelayCommand(ExecuteImportPartsCsv);
            ExportAllPartsCsvCommand = new RelayCommand(ExecuteExportAllPartsCsv);
            SaveBulkPartsCommand = new RelayCommand(ExecuteSaveBulkParts);
            SaveHistoryCsvCommand = new RelayCommand(ExecuteSaveHistoryCsv);
            ClearHistorySearchCommand = new RelayCommand(ExecuteClearHistorySearch);
            RefreshStatisticsCommand = new RelayCommand(ExecuteRefreshStatistics);
            ResetStatisticsCommand = new RelayCommand(ExecuteResetStatistics);
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

        public PartViewModel SelectedPart
        {
            get { return _selectedPart; }
            set
            {
                if (SetProperty(ref _selectedPart, value))
                {
                    ApplySelectedPart();
                }
            }
        }

        public PartViewModel SelectedDbPart
        {
            get { return _selectedDbPart; }
            set
            {
                if (SetProperty(ref _selectedDbPart, value))
                {
                    ApplySelectedDbPart();
                }
            }
        }

        public PartViewModel SelectedRegistrationPart
        {
            get { return _selectedRegistrationPart; }
            set
            {
                if (SetProperty(ref _selectedRegistrationPart, value))
                {
                    ApplySelectedRegistrationPart();
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
        /// 검사 화면(6개 카메라 RtspVideoHost)을 TabControl 밖에 항상 살려두고 이 값으로만 표시 여부를 전환합니다.
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

        public string RegistrationCoordinateImagePath
        {
            get { return _registrationCoordinateImagePath; }
            private set
            {
                if (SetProperty(ref _registrationCoordinateImagePath, value))
                {
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
            string selectedPartNo = SelectedPart == null ? string.Empty : SelectedPart.PartNo;
            string selectedDbPartNo = SelectedDbPart == null ? string.Empty : SelectedDbPart.PartNo;
            string selectedRegistrationPartNo = SelectedRegistrationPart == null ? string.Empty : SelectedRegistrationPart.PartNo;

            Parts.Clear();
            foreach (Part part in _partDataStore.GetParts())
            {
                Parts.Add(new PartViewModel(part));
            }

            ApplySearchFilters();
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

            if (DbParts.Count > 0 && SelectedDbPart == null)
            {
                SelectedDbPart = DbParts[0];
            }

            if (DbParts.Count > 0 && SelectedRegistrationPart == null)
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
            string coordinateImagePath = string.Empty;
            if (part != null && part.MeasurementRegions != null && part.MeasurementRegions.Count > 0)
            {
                coordinateImagePath = ResolveCommittedCoordinateImagePath(part);
            }

            foreach (PartImage image in BuildOrderedUniqueImages(part.Images))
            {
                int index = GetImageViewTypeSortOrder(image.ViewType);
                if (index >= ImageSlots.Count)
                {
                    continue;
                }

                ImageSlots[index].StatusText = "기준 이미지 준비";
                ImageSlots[index].ReferenceImagePath = image.ViewType == ImageViewType.Thickness &&
                                                       !string.IsNullOrWhiteSpace(coordinateImagePath)
                    ? coordinateImagePath
                    : image.FilePath;
            }

            // 기준 이미지 상태 문구를 적용한 뒤 스트림 설정을 다시 반영해
            // 중복 URL 또는 RTSP 준비 상태가 화면에서 덮어써지지 않게 합니다.
            ApplyLiveStreamUrls();
            _referenceImagePopupService.Update(part);
        }

        private void LoadInspectionMeasurementRegions(Part part)
        {
            InspectionMeasurements.Clear();
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                InspectionMeasurements.Add(new MeasurementRowViewModel(region));
            }
        }

        private void LoadDbDetail(Part part)
        {
            DbDetailMeasurements.Clear();
            DbDetailImages.Clear();

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                DbDetailMeasurements.Add(new MeasurementRowViewModel(region));
            }

            foreach (ImageEditViewModel imageViewModel in BuildImageEditViewModels(part.Images))
            {
                DbDetailImages.Add(imageViewModel);
            }
            RefreshDbDetailImagePreviews(part);

            if (DbDetailImages.Count > 0)
            {
                SelectedDbDetailImage = DbDetailImages[0];
            }
            else
            {
                SelectedDbDetailImage = null;
            }
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
                if (RegistrationMeasurementPoints.Count >= MeasurementPointPolicy.MaxCount)
                {
                    break;
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
            BuildReferenceImagePreviews(
                DbDetailImagePreviews,
                DbDetailImages,
                ResolveCommittedCoordinateImagePath(part),
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
                string.Empty,
                false);
        }

        private void BuildReferenceImagePreviews(
            ObservableCollection<ReferenceImagePreviewViewModel> target,
            IEnumerable<ImageEditViewModel> images,
            string coordinateImagePath,
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
                preview.ClearSimilarityCandidates(preview.HasImage ? "검색 전" : "이미지 없음");
                target.Add(preview);
                order++;
            }

            if (!includeCoordinateImage)
            {
                return;
            }

            ReferenceImagePreviewViewModel coordinatePreview = new ReferenceImagePreviewViewModel();
            coordinatePreview.Order = order;
            coordinatePreview.Title = "측정부 좌표";
            coordinatePreview.ViewType = ImageViewType.Unclassified;
            coordinatePreview.FilePath = coordinateImagePath;
            coordinatePreview.ClearSimilarityCandidates("유사도 검색 제외");
            target.Add(coordinatePreview);
        }

        private string FindImageFilePath(
            IEnumerable<ImageEditViewModel> images,
            ImageViewType viewType)
        {
            if (images == null)
            {
                return string.Empty;
            }

            foreach (ImageEditViewModel image in images)
            {
                if (image != null && image.Image.ViewType == viewType)
                {
                    return image.FilePath;
                }
            }

            return string.Empty;
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
        /// 실제 영상 재생은 XAML의 RtspVideoHost가 LiveStreamUrl을 받아 수행합니다.
        /// </summary>
        private void ApplyLiveStreamUrls()
        {
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
            IList<CapturedImage> capturedImages = CaptureCurrentImagesForReference(partToSave, out captureFailureCount, out captureFailureMessage);
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
            RaiseRunCommandState();

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
                slot.IsCapturedStillVisible = false;
                slot.StatusText = statusMessage;
                slot.ResultText = resultText;
                slot.ResultBrush = "#0A86D8";
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

            PrepareInspectionRunningSlots(progress.Message, resultText);
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

        private void ApplyInspectionResult(Inspection inspection)
        {
            if (inspection == null)
            {
                StatusText = "오류";
                ResultText = "Error - 검사 결과 없음";
                AddInspectionEvent(EventSeverity.Error, "검사 결과가 없습니다.");
                return;
            }

            ResultText = BuildSlotResultText(inspection.Result) + " - " + inspection.ResultMessage;
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

        private void LoadCapturedImages(Inspection inspection)
        {
            ClearLiveImageSlots();
            Part part = SelectedPart == null ? null : SelectedPart.Part;
            string thicknessCoordinateImagePath = ResolveCommittedCoordinateImagePath(part);
            foreach (CapturedImage image in inspection.Images)
            {
                int index = GetImageViewTypeSortOrder(image.ViewType);
                if (index >= ImageSlots.Count)
                {
                    continue;
                }

                string displayImagePath = image.ViewType == ImageViewType.Thickness &&
                                          !string.IsNullOrWhiteSpace(thicknessCoordinateImagePath)
                    ? thicknessCoordinateImagePath
                    : image.FilePath;

                ImageSlots[index].StatusText = "촬영 완료";
                ImageSlots[index].LiveImagePath = displayImagePath;
                ImageSlots[index].IsCapturedStillVisible = true;

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
                if (string.IsNullOrWhiteSpace(slot.ReferenceImagePath))
                {
                    slot.StatusText = "카메라 대기";
                }
                else
                {
                    slot.StatusText = "기준 이미지 준비";
                }
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
                foreach (MeasurementRegion region in referencePart.MeasurementRegions)
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

                _referenceImageFileService.CommitTemporaryCoordinateImage(part);
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

            string referenceImageMergeMessage = string.Empty;
            if (hadTemporaryImages && _imageMergeService != null)
            {
                string mergedFilePath;
                _imageMergeService.TryMergeReferenceImages(
                    part,
                    out mergedFilePath,
                    out referenceImageMergeMessage);
            }

            string ocrTemporaryCleanupWarning = ClearRegistrationOcrTemporaryFiles();
            if (string.IsNullOrWhiteSpace(ocrTemporaryCleanupWarning))
            {
                ClearLatestRegistrationOcrResult();
            }

            _referenceImageFileService.ClearTemporaryReferenceImages(part);
            LoadRegistrationImages(part);
            RefreshPartCollectionsFromDataStore();
            if (isRegistrationPartSelectedInSearchDb)
            {
                RefreshInspectionPartSelection(part.PartNo);
            }

            RefreshStatistics();
            RegistrationMessage = hadTemporaryImages
                ? PartCatalogService.SaveSuccessMessage + " 임시 기준 이미지를 최종 폴더로 확정하고 등록시간을 갱신했습니다."
                : PartCatalogService.SaveSuccessMessage;

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

            if (RegistrationMeasurementPoints.Count > MeasurementPointPolicy.MaxCount)
            {
                errorMessage = "측정부는 최대 " + MeasurementPointPolicy.MaxCount.ToString() + "개까지만 등록할 수 있습니다.";
                return false;
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
            DbParts = new ObservableCollection<PartViewModel>(filteredParts);
            OnPropertyChanged("DbParts");

            SelectedDbPart = string.IsNullOrWhiteSpace(selectedDbPartNo) ? null : FindDbPartViewModel(selectedDbPartNo);
            SelectedRegistrationPart = string.IsNullOrWhiteSpace(selectedRegistrationPartNo) ? null : FindDbPartViewModel(selectedRegistrationPartNo);

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

        private void ExecuteAddMeasurementPoint(object parameter)
        {
            if (RegistrationMeasurementPoints.Count >= MeasurementPointPolicy.MaxCount)
            {
                RegistrationMessage = "측정부는 최대 " + MeasurementPointPolicy.MaxCount.ToString() + "개까지만 추가할 수 있습니다.";
                _messageDialogService.ShowWarning("측정부 추가 제한", RegistrationMessage);
                return;
            }

            MeasurementPointViewModel point = new MeasurementPointViewModel();
            point.ApplyIndex(RegistrationMeasurementPoints.Count + 1);
            point.LineColor = MeasurementPointViewModel.GetDefaultColor(point.IndexNo);
            RegistrationMeasurementPoints.Add(point);
            SelectedRegistrationMeasurementPoint = point;
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

            RegistrationMeasurementPoints.Remove(point);
            ReindexMeasurementPoints();
            SelectedRegistrationMeasurementPoint = RegistrationMeasurementPoints.Count > 0
                ? RegistrationMeasurementPoints[Math.Min(point.IndexNo - 1, RegistrationMeasurementPoints.Count - 1)]
                : null;
            RegistrationMessage = "선택한 측정부를 삭제하고 이후 번호를 다시 정렬했습니다.";
        }

        private void ReindexMeasurementPoints()
        {
            int index = 1;
            foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
            {
                point.ApplyIndex(index);
                index++;
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

            ImageEditViewModel thicknessImage = FindRegistrationImageByViewType(ImageViewType.Thickness);
            if (thicknessImage == null || string.IsNullOrWhiteSpace(thicknessImage.FilePath))
            {
                RegistrationMessage = "Thickness 이미지가 없어서 측정부 위치를 등록할 수 없습니다.";
                _messageDialogService.ShowWarning("Thickness 이미지 필요", RegistrationMessage);
                return;
            }

            IList<MeasurementPointViewModel> allPoints = new List<MeasurementPointViewModel>();
            foreach (MeasurementPointViewModel registeredPoint in RegistrationMeasurementPoints)
            {
                allPoints.Add(registeredPoint);
            }

            bool isSaved;
            try
            {
                isSaved = _measurementPositionDialogService.Show(thicknessImage.FilePath, point, allPoints);
            }
            catch (Exception ex)
            {
                RegistrationMessage = "Thickness 이미지 위치 지정 창을 열 수 없습니다. " + ex.Message;
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

                RegistrationMessage = point.PointName + " 위치 좌표와 선 색상을 적용하고 모든 측정부 선을 coordinate 이미지에 저장했습니다.";
            }
            else
            {
                RegistrationMessage = "측정부 위치 지정을 취소했거나 Thickness 이미지 파일을 찾을 수 없습니다.";
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
                SelectedRegistrationPart = SelectedPart;
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
            int captureFailureCount;
            string captureFailureMessage;
            IList<CapturedImage> capturedImages = CaptureCurrentImagesForReference(tempPart, out captureFailureCount, out captureFailureMessage);
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
                    ApplyStagedReferenceImageToSlot(stagedImage);
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

            if (savedCount > 0)
            {
                ReorderRegistrationImages(lastSavedImageViewModel == null ? null : lastSavedImageViewModel.Image);
            }

            if (savedCount == 0)
            {
                RegistrationMessage = "저장할 현재 카메라 이미지 파일이 없습니다. 카메라 연결 상태와 저장 권한을 확인하세요." + captureFailureMessage;
                return;
            }

            if (isInspectionImmediateCommit)
            {
                // 부품등록 탭의 "DB 저장"과 완전히 동일하게 동작시킵니다. ExecuteSavePart는 저장 후
                // 같은 부품이 검사 탭에 선택되어 있으면 그 화면도 함께 새로고침하므로, 검사 탭에서
                // 저장한 새 기준 이미지와 부품등록 탭 표시 내용이 서로 어긋나지 않습니다.
                ExecuteSavePart(parameter);
                return;
            }

            RegistrationMessage = "현재 카메라 이미지 " + savedCount.ToString() +
                                  "개를 Temp에 임시 저장했습니다. DB 저장을 누르면 최종 이미지 폴더와 DB에 반영됩니다. 저장 제외 " +
                                  skippedCount.ToString() + "개." + captureFailureMessage;
        }

        private IList<CapturedImage> CaptureCurrentImagesForReference(Part part, out int failureCount, out string failureMessage)
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
                    ApplyCapturedImageToSlot(capturedImage, "기준 저장용 촬영 완료", "CAPTURE", "#128A45");
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
        /// 좌표가 하나 이상 등록된 경우 Thickness 이미지 위에 모든 측정부 선을 누적해
        /// DB\Image\Temp\품번\품번_coordinate.png를 생성합니다.
        /// </summary>
        private bool TrySaveTemporaryCoordinateImage(Part part, out string errorMessage)
        {
            errorMessage = string.Empty;
            bool hasCoordinates = false;
            foreach (MeasurementPointViewModel point in RegistrationMeasurementPoints)
            {
                if (point.HasCoordinates)
                {
                    hasCoordinates = true;
                    break;
                }
            }

            try
            {
                if (!hasCoordinates)
                {
                    _referenceImageFileService.DeleteTemporaryCoordinateImage(part);
                    RegistrationCoordinateImagePath = string.Empty;
                    return true;
                }

                PartImage thicknessImage = FindPartImageByViewType(part.Images, ImageViewType.Thickness);
                if (thicknessImage == null ||
                    string.IsNullOrWhiteSpace(thicknessImage.FilePath) ||
                    !File.Exists(thicknessImage.FilePath))
                {
                    errorMessage = "측정부 선을 저장할 Thickness 기준 이미지가 없습니다.";
                    return false;
                }

                string coordinatePath = _referenceImageFileService.GetTemporaryCoordinateImagePath(part);
                _referenceCoordinateImageService.SaveCoordinateImage(
                    thicknessImage.FilePath,
                    coordinatePath,
                    RegistrationMeasurementPoints);
                RegistrationCoordinateImagePath = coordinatePath;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "coordinate 이미지를 생성할 수 없습니다. " + ex.Message;
                return false;
            }
        }

        private PartImage FindPartImageByViewType(IList<PartImage> images, ImageViewType viewType)
        {
            if (images == null)
            {
                return null;
            }

            foreach (PartImage image in images)
            {
                if (image != null && image.ViewType == viewType)
                {
                    return image;
                }
            }

            return null;
        }

        private string ResolveRegistrationCoordinateImagePath(Part part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            string temporaryPath = _referenceImageFileService.GetTemporaryCoordinateImagePath(part);
            if (!string.IsNullOrWhiteSpace(temporaryPath) && File.Exists(temporaryPath))
            {
                return temporaryPath;
            }

            return ResolveCommittedCoordinateImagePath(part);
        }

        private string ResolveCommittedCoordinateImagePath(Part part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            PartImage thicknessImage = FindPartImageByViewType(part.Images, ImageViewType.Thickness);
            if (thicknessImage == null || string.IsNullOrWhiteSpace(thicknessImage.FilePath))
            {
                return string.Empty;
            }

            string imageDirectoryPath = Path.GetDirectoryName(thicknessImage.FilePath);
            if (string.IsNullOrWhiteSpace(imageDirectoryPath))
            {
                return string.Empty;
            }

            string committedPath = Path.Combine(
                imageDirectoryPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(part.PartNo));
            if (File.Exists(committedPath))
            {
                return committedPath;
            }

            // 기존 저장 파일은 다음 DB 저장 전까지 미리보기 호환 대상으로만 사용합니다.
            string legacyPath = Path.Combine(
                imageDirectoryPath,
                ReferenceImageFileNamePolicy.LegacyCoordinateFileName);
            return File.Exists(legacyPath) ? legacyPath : string.Empty;
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

            string coordinatePath = _referenceImageFileService.GetTemporaryCoordinateImagePath(part);
            return !string.IsNullOrWhiteSpace(coordinatePath) && File.Exists(coordinatePath);
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
                    "등록된 기준 이미지 6장과 coordinate 이미지 1장을 모두 삭제합니다.\r\n" +
                    "해당 품목의 측정부 정보도 함께 삭제됩니다.\r\n\r\n" +
                    "계속 진행하시겠습니까?");
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

                AddCoordinatePathForDeletion(
                    paths,
                    uniquePaths,
                    Path.Combine(
                        folderPath,
                        ReferenceImageFileNamePolicy.BuildCoordinateFileName(RegistrationPartNo)));
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

            int outputIndex = 1;
            for (int csvIndex = 1; csvIndex <= MeasurementPointPolicy.MaxCount; csvIndex++)
            {
                string itemType = GetMeasurementCsvValue(headers, values, csvIndex, "항목");
                string nominalText = GetMeasurementCsvValue(headers, values, csvIndex, "기준");
                string toleranceMinText = GetMeasurementCsvValue(headers, values, csvIndex, "Min");
                string toleranceMaxText = GetMeasurementCsvValue(headers, values, csvIndex, "Max");
                string toleranceRangeText = GetMeasurementCsvValue(headers, values, csvIndex, "MinMax");
                string legacyToleranceText = GetMeasurementCsvValue(headers, values, csvIndex, "허용");
                ApplyCsvToleranceAliases(csvIndex, toleranceRangeText, legacyToleranceText, ref toleranceMinText, ref toleranceMaxText);

                string lineColor = GetMeasurementCsvValue(headers, values, csvIndex, "색상");
                string x1Text = GetMeasurementCsvValue(headers, values, csvIndex, "X1");
                string y1Text = GetMeasurementCsvValue(headers, values, csvIndex, "Y1");
                string x2Text = GetMeasurementCsvValue(headers, values, csvIndex, "X2");
                string y2Text = GetMeasurementCsvValue(headers, values, csvIndex, "Y2");

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
                region.IndexNo = outputIndex;
                region.ItemType = NormalizeBulkMetadataValue(itemType, "미설정");
                region.Name = "측정부" + outputIndex.ToString(CultureInfo.InvariantCulture) + " - " + region.ItemType;
                region.ViewType = ImageViewType.Thickness;
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

        private string GetMeasurementCsvValue(
            IList<string> headers,
            IList<string> values,
            int indexNo,
            string fieldName)
        {
            string prefix = "측정부" + indexNo.ToString(CultureInfo.InvariantCulture);
            return GetCsvValue(
                headers,
                values,
                prefix + fieldName,
                prefix + "_" + fieldName,
                "Measurement" + indexNo.ToString(CultureInfo.InvariantCulture) + fieldName);
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
            row.Measurement1Summary = BuildMeasurementCsvSummary(GetMeasurementRegionForCsv(part, 1));
            row.Measurement2Summary = BuildMeasurementCsvSummary(GetMeasurementRegionForCsv(part, 2));
            row.Measurement3Summary = BuildMeasurementCsvSummary(GetMeasurementRegionForCsv(part, 3));
            row.Measurement4Summary = BuildMeasurementCsvSummary(GetMeasurementRegionForCsv(part, 4));
            row.Measurement5Summary = BuildMeasurementCsvSummary(GetMeasurementRegionForCsv(part, 5));
            row.MeasurementUnit = "mm";
            row.ResultMessage = string.IsNullOrWhiteSpace(resultMessage) ? "정상" : resultMessage;
            return row;
        }

        private string BuildMeasurementCsvSummary(MeasurementRegion region)
        {
            if (region == null)
            {
                return "-";
            }

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

            for (int indexNo = 1; indexNo <= MeasurementPointPolicy.MaxCount; indexNo++)
            {
                string prefix = "측정부" + indexNo.ToString(CultureInfo.InvariantCulture);
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

            for (int indexNo = 1; indexNo <= MeasurementPointPolicy.MaxCount; indexNo++)
            {
                AddMeasurementPointCsvValues(values, GetMeasurementRegionForCsv(part, indexNo), indexNo);
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

        private MeasurementRegion GetMeasurementRegionForCsv(Part part, int indexNo)
        {
            if (part == null || part.MeasurementRegions == null)
            {
                return null;
            }

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (region != null && region.IndexNo == indexNo)
                {
                    return region;
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
            TrainingProcessMessages.Add(new TrainingProcessMessageRowViewModel
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                Source = source ?? string.Empty,
                Type = type ?? string.Empty,
                Value = value ?? string.Empty,
                Message = message ?? string.Empty,
                Raw = raw ?? string.Empty
            });

            while (TrainingProcessMessages.Count > 1000)
            {
                TrainingProcessMessages.RemoveAt(0);
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
