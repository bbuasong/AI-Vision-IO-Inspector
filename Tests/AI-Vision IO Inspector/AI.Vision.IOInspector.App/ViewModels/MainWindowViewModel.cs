using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.App.Stores;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Services;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure.Services;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 메인 화면의 전체 상태를 관리합니다.
    /// DB 조회/확인, 부품 생성/변경/삭제, 검사, 이력, 통계를 화면별로 연결하되 업무 로직은 서비스로 위임합니다.
    /// </summary>
    public class MainWindowViewModel : ObservableObject
    {
        private const int MaxSearchSuggestionCount = 10;
        private const int LivePreviewRefreshIntervalMilliseconds = 1000;
        private const string SearchFieldPartNo = "PartNo";
        private const string SearchFieldPartName = "PartName";
        private const string SearchFieldCategoryCode = "CategoryCode";
        private const string SearchFieldCategoryDescription = "CategoryDescription";

        private readonly PartDataStore _partDataStore;
        private readonly InspectionWorkflowService _inspectionWorkflowService;
        private readonly StatisticsService _statisticsService;
        private readonly IInspectionRepository _inspectionRepository;
        private readonly ICameraService _cameraService;
        private readonly IReferenceImageFileService _referenceImageFileService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IMessageDialogService _messageDialogService;
        private readonly IMeasurementPositionDialogService _measurementPositionDialogService;
        private readonly IReferenceCoordinateImageService _referenceCoordinateImageService;
        private readonly IList<InspectionRowViewModel> _allInspectionHistory;
        private readonly IList<Part> _pendingBulkParts;
        private readonly DispatcherTimer _mainSearchDelayTimer;
        private readonly DispatcherTimer _searchDelayTimer;
        private readonly DispatcherTimer _livePreviewTimer;

        private PartViewModel _selectedPart;
        private PartViewModel _selectedDbPart;
        private PartViewModel _selectedRegistrationPart;
        private int _selectedTabIndex;
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
        private string _registrationPartType;
        private string _registrationMessage;
        private MeasurementPointViewModel _selectedRegistrationMeasurementPoint;
        private ImageEditViewModel _selectedDbDetailImage;
        private ImageEditViewModel _selectedRegistrationImage;
        private string _selectedReferenceImageViewType;
        private string _bulkRegistrationMessage;
        private int _totalPartCount;
        private int _totalInspectionCount;
        private int _okCount;
        private int _ngCount;
        private int _errorCount;
        private string _averageInspectionTime;
        private string _historyMessage;
        private string _historyStartTimeKeyword;
        private string _historyEndTimeKeyword;
        private string _historyPartNoKeyword;
        private string _historyPartNameKeyword;
        private string _historyCategoryCodeKeyword;
        private string _historyCategoryDescriptionKeyword;
        private string _historyPartTypeKeyword;
        private string _historyNgResultKeyword;
        private string _cameraStatusMessage;
        private CameraChannelStatusViewModel _selectedCameraChannel;
        private bool _deleteRequested;
        private bool _bulkImportHasError;
        private bool _isLivePreviewAutoRefreshEnabled;
        private bool _isLivePreviewRefreshRunning;
        private bool _isInspectionRunning;

        public MainWindowViewModel(
            PartDataStore partDataStore,
            InspectionWorkflowService inspectionWorkflowService,
            StatisticsService statisticsService,
            IInspectionRepository inspectionRepository,
            ICameraService cameraService,
            IReferenceImageFileService referenceImageFileService,
            IFileDialogService fileDialogService,
            IMessageDialogService messageDialogService,
            IMeasurementPositionDialogService measurementPositionDialogService,
            IReferenceCoordinateImageService referenceCoordinateImageService)
        {
            _partDataStore = partDataStore;
            _inspectionWorkflowService = inspectionWorkflowService;
            _statisticsService = statisticsService;
            _inspectionRepository = inspectionRepository;
            _cameraService = cameraService;
            _referenceImageFileService = referenceImageFileService;
            _fileDialogService = fileDialogService;
            _messageDialogService = messageDialogService;
            _measurementPositionDialogService = measurementPositionDialogService;
            _referenceCoordinateImageService = referenceCoordinateImageService;
            _allInspectionHistory = new List<InspectionRowViewModel>();
            _pendingBulkParts = new List<Part>();

            Parts = new ObservableCollection<PartViewModel>();
            DbParts = new ObservableCollection<PartViewModel>();
            MainSearchSuggestions = new ObservableCollection<string>();
            DbSearchSuggestions = new ObservableCollection<string>();
            ImageSlots = new ObservableCollection<ImageSlotViewModel>();
            InspectionMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailImages = new ObservableCollection<ImageEditViewModel>();
            RegistrationMeasurementPoints = new ObservableCollection<MeasurementPointViewModel>();
            MeasurementItemTypes = new ObservableCollection<string>();
            RegistrationImages = new ObservableCollection<ImageEditViewModel>();
            ReferenceImageViewTypes = new ObservableCollection<string>();
            BulkPartRows = new ObservableCollection<BulkPartCsvRowViewModel>();
            InspectionHistory = new ObservableCollection<InspectionRowViewModel>();
            EventRows = new ObservableCollection<EventRowViewModel>();
            CameraChannels = new ObservableCollection<CameraChannelStatusViewModel>();

            RunInspectionCommand = new RelayCommand(ExecuteRunInspection, CanRunInspection);
            ResetInspectionScreenCommand = new RelayCommand(ExecuteResetInspectionScreen, CanResetInspectionScreen);
            SavePartCommand = new RelayCommand(ExecuteSavePart);
            NewPartCommand = new RelayCommand(ExecuteNewPart);
            DeletePartCommand = new RelayCommand(ExecuteDeletePart);
            SearchCommand = new RelayCommand(ExecuteSearch);
            ApplyMainSearchSuggestionCommand = new RelayCommand(ExecuteApplyMainSearchSuggestion);
            ApplyPartNameSearchSuggestionCommand = new RelayCommand(ExecuteApplyPartNameSearchSuggestion);
            ApplyDbSearchSuggestionCommand = new RelayCommand(ExecuteApplyDbSearchSuggestion);
            AddMeasurementPointCommand = new RelayCommand(ExecuteAddMeasurementPoint);
            RemoveMeasurementPointCommand = new RelayCommand(ExecuteRemoveMeasurementPoint);
            EditMeasurementPositionCommand = new RelayCommand(ExecuteEditMeasurementPosition);
            AddReferenceImageCommand = new RelayCommand(ExecuteAddReferenceImage);
            SaveCurrentCameraImagesCommand = new RelayCommand(ExecuteSaveCurrentCameraImages);
            RefreshLivePreviewCommand = new RelayCommand(ExecuteRefreshLivePreview);
            RemoveReferenceImageCommand = new RelayCommand(ExecuteRemoveReferenceImage);
            ImportPartsCsvCommand = new RelayCommand(ExecuteImportPartsCsv);
            ExportAllPartsCsvCommand = new RelayCommand(ExecuteExportAllPartsCsv);
            SaveBulkPartsCommand = new RelayCommand(ExecuteSaveBulkParts);
            SaveHistoryCsvCommand = new RelayCommand(ExecuteSaveHistoryCsv);
            ClearHistorySearchCommand = new RelayCommand(ExecuteClearHistorySearch);
            RefreshStatisticsCommand = new RelayCommand(ExecuteRefreshStatistics);
            RefreshCameraStatusCommand = new RelayCommand(ExecuteRefreshCameraStatus);
            ReloadCameraConfigurationCommand = new RelayCommand(ExecuteReloadCameraConfiguration);
            SaveCameraConfigurationCommand = new RelayCommand(ExecuteSaveCameraConfiguration);
            TestSelectedCameraConnectionCommand = new RelayCommand(ExecuteTestSelectedCameraConnection);
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

            StatusText = "대기";
            ResultText = "검사 전";
            _activeDbSearchFieldName = SearchFieldPartName;
            InitializeReferenceImageViewTypes();
            InitializeImageSlots();
            InitializeMeasurementItemTypes();
            InitializeEmptyRegistrationPoints();
            LoadParts();
            RefreshHistory();
            RefreshStatistics();
            RefreshCameraStatuses(false);
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

        public ObservableCollection<MeasurementPointViewModel> RegistrationMeasurementPoints { get; private set; }

        public ObservableCollection<string> MeasurementItemTypes { get; private set; }

        public ObservableCollection<ImageEditViewModel> RegistrationImages { get; private set; }

        public ObservableCollection<string> ReferenceImageViewTypes { get; private set; }

        public ObservableCollection<BulkPartCsvRowViewModel> BulkPartRows { get; private set; }

        public ObservableCollection<InspectionRowViewModel> InspectionHistory { get; private set; }

        public ObservableCollection<EventRowViewModel> EventRows { get; private set; }

        public ObservableCollection<CameraChannelStatusViewModel> CameraChannels { get; private set; }

        public ICommand RunInspectionCommand { get; private set; }

        public ICommand ResetInspectionScreenCommand { get; private set; }

        public ICommand SavePartCommand { get; private set; }

        public ICommand NewPartCommand { get; private set; }

        public ICommand DeletePartCommand { get; private set; }

        public ICommand SearchCommand { get; private set; }

        public ICommand ApplyMainSearchSuggestionCommand { get; private set; }

        public ICommand ApplyPartNameSearchSuggestionCommand { get; private set; }

        public ICommand ApplyDbSearchSuggestionCommand { get; private set; }

        public ICommand AddMeasurementPointCommand { get; private set; }

        public ICommand RemoveMeasurementPointCommand { get; private set; }

        public ICommand EditMeasurementPositionCommand { get; private set; }

        public ICommand AddReferenceImageCommand { get; private set; }

        public ICommand SaveCurrentCameraImagesCommand { get; private set; }

        public ICommand RefreshLivePreviewCommand { get; private set; }

        public ICommand RemoveReferenceImageCommand { get; private set; }

        public ICommand ImportPartsCsvCommand { get; private set; }

        public ICommand ExportAllPartsCsvCommand { get; private set; }

        public ICommand SaveBulkPartsCommand { get; private set; }

        public ICommand SaveHistoryCsvCommand { get; private set; }

        public ICommand ClearHistorySearchCommand { get; private set; }

        public ICommand RefreshStatisticsCommand { get; private set; }

        public ICommand RefreshCameraStatusCommand { get; private set; }

        public ICommand ReloadCameraConfigurationCommand { get; private set; }

        public ICommand SaveCameraConfigurationCommand { get; private set; }

        public ICommand TestSelectedCameraConnectionCommand { get; private set; }

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
            set { SetProperty(ref _selectedTabIndex, value); }
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

        public string RegistrationPartType
        {
            get { return _registrationPartType; }
            set { SetProperty(ref _registrationPartType, value); }
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

        public int OkCount
        {
            get { return _okCount; }
            set { SetProperty(ref _okCount, value); }
        }

        public int NgCount
        {
            get { return _ngCount; }
            set { SetProperty(ref _ngCount, value); }
        }

        public int ErrorCount
        {
            get { return _errorCount; }
            set { SetProperty(ref _errorCount, value); }
        }

        public string AverageInspectionTime
        {
            get { return _averageInspectionTime; }
            set { SetProperty(ref _averageInspectionTime, value); }
        }

        public string HistoryMessage
        {
            get { return _historyMessage; }
            set { SetProperty(ref _historyMessage, value); }
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

        public string HistoryPartTypeKeyword
        {
            get { return _historyPartTypeKeyword; }
            set
            {
                if (SetProperty(ref _historyPartTypeKeyword, value))
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

        public CameraChannelStatusViewModel SelectedCameraChannel
        {
            get { return _selectedCameraChannel; }
            set { SetProperty(ref _selectedCameraChannel, value); }
        }

        private void LoadParts()
        {
            _partDataStore.LoadFromDatabase();
            RefreshPartCollectionsFromDataStore();
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

                ImageSlots[index].StatusText = "기준 이미지 준비";
                ImageSlots[index].ReferenceImagePath = image.FilePath;
            }

            // 기준 이미지 상태 문구를 적용한 뒤 스트림 설정을 다시 반영해
            // 중복 URL 또는 RTSP 준비 상태가 화면에서 덮어써지지 않게 합니다.
            ApplyLiveStreamUrls();
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
            RegistrationPartType = part.PartType;

            LoadRegistrationMeasurementPoints(part);
            LoadRegistrationImages(part);
            _deleteRequested = false;
            RegistrationMessage = "선택한 부품 정보를 편집할 수 있습니다.";
        }

        private void LoadRegistrationMeasurementPoints(Part part)
        {
            RegistrationMeasurementPoints.Clear();
            int fallbackIndex = 1;
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (RegistrationMeasurementPoints.Count >= 10)
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

        private string FormatTolerance(MeasurementRegion region)
        {
            decimal minTolerance = region.ToleranceMin;
            if (minTolerance < 0)
            {
                minTolerance = -minTolerance;
            }

            decimal maxTolerance = region.ToleranceMax;
            if (maxTolerance < 0)
            {
                maxTolerance = -maxTolerance;
            }

            decimal tolerance = maxTolerance >= minTolerance ? maxTolerance : minTolerance;
            return tolerance.ToString("0.###");
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

        private PartImage FindFirstImageByViewType(IList<PartImage> images, ImageViewType viewType)
        {
            foreach (PartImage image in images)
            {
                if (image.ViewType == viewType)
                {
                    return image;
                }
            }

            return null;
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
                    slot.IsLiveStreamEnabled = false;
                    slot.StatusText = "중복 RTSP URL - 최초 채널만 재생";
                    continue;
                }

                assignedStreamUrls.Add(streamUrl);
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
                    "기준 이미지를 먼저 등록하지 않고 현재 카메라 이미지 기준으로 검사를 계속 진행하시겠습니까?");
                if (!continueInspection)
                {
                    AddInspectionEvent(EventSeverity.Warning, "사용자가 기준 이미지 등록을 위해 검사를 취소했습니다.");
                    return;
                }

                continueWithoutFullReferenceImages = true;
                continuedReferenceImageMessage = referenceImageMessage;
            }

            BeginRunInspection(InputCode);
            if (continueWithoutFullReferenceImages)
            {
                AddInspectionEvent(EventSeverity.Warning, continuedReferenceImageMessage);
                AddInspectionEvent(EventSeverity.Warning, "기준 이미지 누락 상태에서 사용자가 검사를 계속 진행했습니다.");
            }
        }

        private void BeginRunInspection(string inputCode)
        {
            _isInspectionRunning = true;
            RaiseRunCommandState();

            StatusText = "검사중";
            EventRows.Clear();

            _livePreviewTimer.Stop();

            Task<Inspection>.Factory.StartNew(
                    RunInspectionOnWorker,
                    inputCode,
                    CancellationToken.None,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default)
                .ContinueWith(OnRunInspectionCompleted, TaskScheduler.FromCurrentSynchronizationContext());
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

            ResultText = inspection.Result + " - " + inspection.ResultMessage;
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

                try
                {
                    CapturedImage image = _cameraService.Capture(channel.ViewType, previewPart);
                    frameResult.IsSuccess = true;
                    frameResult.FilePath = image.FilePath;
                    frameResult.Message = "프레임 수신 완료";
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    frameResult.IsSuccess = false;
                    frameResult.Message = TrimLivePreviewMessage(ex.Message);
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
            foreach (CapturedImage image in inspection.Images)
            {
                int index = GetImageViewTypeSortOrder(image.ViewType);
                if (index >= ImageSlots.Count)
                {
                    continue;
                }

                ImageSlots[index].StatusText = "촬영 완료";
                ImageSlots[index].LiveImagePath = image.FilePath;
                ImageSlots[index].IsCapturedStillVisible = true;
                ImageSlots[index].ResultText = BuildSlotResultText(inspection.Result);
                ImageSlots[index].ResultBrush = BuildSlotResultBrush(inspection.Result);
            }
        }

        private void ClearLiveImageSlots()
        {
            foreach (ImageSlotViewModel slot in ImageSlots)
            {
                slot.LiveImagePath = string.Empty;
                slot.IsCapturedStillVisible = false;
                slot.ResultText = "READY";
                slot.ResultBrush = "#66788A";
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
            if (result == InspectionResult.Ok)
            {
                return "PASS";
            }

            if (result == InspectionResult.Ng)
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
            if (result == InspectionResult.Ok)
            {
                return "#128A45";
            }

            if (result == InspectionResult.Ng)
            {
                return "#B73535";
            }

            if (result == InspectionResult.Error)
            {
                return "#A96F16";
            }

            return "#0A86D8";
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
            slot.ResultBrush = "#B73535";
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
            foreach (MeasurementResult measurement in inspection.Measurements)
            {
                InspectionMeasurements.Add(new MeasurementRowViewModel(measurement));
            }
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
            RegistrationPartType = string.Empty;
            RegistrationImages.Clear();
            SelectedRegistrationImage = null;
            SelectedRegistrationPart = null;
            InitializeEmptyRegistrationPoints();
            _deleteRequested = false;
            SelectedTabIndex = 2;
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
            part.PartType = RegistrationPartType;

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

            if (RegistrationMeasurementPoints.Count > 10)
            {
                errorMessage = "측정부는 최대 10개까지만 등록할 수 있습니다.";
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
            RegistrationPartNo = string.Empty;
            RegistrationPartName = string.Empty;
            RegistrationCategoryCode = string.Empty;
            RegistrationCategoryDescription = string.Empty;
            RegistrationPartType = string.Empty;
            RegistrationImages.Clear();
            SelectedRegistrationImage = null;
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
                   !string.IsNullOrWhiteSpace(criteria.PartType);
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
                   ContainsKeyword(part.PartType, keyword);
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
            if (RegistrationMeasurementPoints.Count >= 10)
            {
                RegistrationMessage = "측정부는 최대 10개까지만 추가할 수 있습니다.";
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
                PartImage existingImage = existingImageViewModel == null ? null : existingImageViewModel.Image;
                image = _referenceImageFileService.AddReferenceImage(tempPart, sourceFilePath, selectedViewType, existingImage);
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
            tempPart.PartType = RegistrationPartType;

            try
            {
                // 재촬영은 현재 품번의 Temp 파일만 초기화합니다.
                // 최종 기준 이미지와 OldVer 백업은 DB 저장 전까지 변경하지 않습니다.
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

            foreach (CameraChannelConfig channel in channels)
            {
                if (channel == null || !channel.IsEnabled)
                {
                    continue;
                }

                try
                {
                    CapturedImage capturedImage = _cameraService.Capture(channel.ViewType, part);
                    if (!IsCapturedImageFileReady(capturedImage))
                    {
                        failureCount++;
                        AppendCaptureFailureMessage(failureBuilder, channel.DisplayName, "캡처 파일이 생성되지 않았습니다.");
                        continue;
                    }

                    capturedImages.Add(capturedImage);
                    ApplyCapturedImageToSlot(capturedImage, "기준 저장용 촬영 완료", "CAPTURE", "#128A45");
                }
                catch (Exception ex)
                {
                    failureCount++;
                    AppendCaptureFailureMessage(failureBuilder, channel.DisplayName, TrimLivePreviewMessage(ex.Message));
                }
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
        }

        private Part BuildRegistrationImagePart()
        {
            Part part = new Part();
            part.PartNo = RegistrationPartNo;
            part.PartName = RegistrationPartName;
            part.CategoryCode = RegistrationCategoryCode;
            part.CategoryDescription = RegistrationCategoryDescription;
            part.PartType = RegistrationPartType;
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                part.Images.Add(imageViewModel.Image);
            }

            return part;
        }

        /// <summary>
        /// 좌표가 하나 이상 등록된 경우 Thickness 이미지 위에 모든 측정부 선을 누적해
        /// DB\Image\Temp\품번\coordinate.png를 생성합니다.
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

        private void ExecuteRemoveReferenceImage(object parameter)
        {
            if (SelectedRegistrationImage == null)
            {
                RegistrationMessage = "삭제할 기준 이미지를 선택하세요.";
                return;
            }

            ImageEditViewModel deleteTarget = SelectedRegistrationImage;
            SelectedRegistrationImage = null;

            string deleteMessage;
            if (!_referenceImageFileService.DeleteReferenceImage(deleteTarget.Image, out deleteMessage))
            {
                SelectedRegistrationImage = deleteTarget;
                RegistrationMessage = deleteMessage;
                return;
            }

            RegistrationImages.Remove(deleteTarget);
            ReorderRegistrationImages(null);
            RegistrationMessage = "기준 이미지를 삭제했습니다.";
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

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            if (lines.Length < 2)
            {
                BulkRegistrationMessage = "CSV에 저장할 데이터 행이 없습니다.";
                return;
            }

            BulkPartRows.Clear();
            _pendingBulkParts.Clear();
            _bulkImportHasError = false;
            IList<string> headers = NormalizeCsvCells(ParseCsvLine(lines[0]));
            if (!HasCsvHeader(headers, "품번", "PartNo", "Part No", "Part No.") ||
                !HasCsvHeader(headers, "품명", "PartName", "Part Name"))
            {
                BulkRegistrationMessage = "CSV 필수 헤더를 찾을 수 없습니다. 품번/품명 컬럼을 확인하세요.";
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
            File.WriteAllLines(filePath, lines, new UTF8Encoding(true));
            BulkRegistrationMessage = "전체 부품 기준정보 " + parts.Count.ToString() + "건을 CSV 파일로 내보냈습니다.";
        }

        private Part BuildPartFromBulkCsv(IList<string> headers, IList<string> values)
        {
            Part part = new Part();
            part.PartNo = GetCsvValue(headers, values, "품번", "PartNo", "Part No", "Part No.");
            part.PartName = GetCsvValue(headers, values, "품명", "PartName", "Part Name");
            part.CategoryCode = GetCsvValue(headers, values, "분류코드", "CategoryCode", "Category Code");
            part.CategoryDescription = GetCsvValue(headers, values, "분류설명", "CategoryDescription", "Category Description");
            part.PartType = GetCsvValue(headers, values, "구분", "PartType", "Type");
            AddBulkCsvMeasurementRegions(part, headers, values);
            return part;
        }

        private void AddBulkCsvMeasurementRegions(Part part, IList<string> headers, IList<string> values)
        {
            Dictionary<int, MeasurementSetViewModel> sets = new Dictionary<int, MeasurementSetViewModel>();
            int maxSetIndex = 0;
            string globalUnit = NormalizeBulkMetadataValue(GetCsvValue(headers, values, "단위", "Unit", "측정부_단위", "MeasurementUnit"), "mm");
            for (int headerIndex = 0; headerIndex < headers.Count; headerIndex++)
            {
                string header = headers[headerIndex];
                string itemName = ResolveMeasurementItemName(header);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                int setIndex = ResolveMeasurementSetIndexFromHeader(header);
                if (setIndex > 1)
                {
                    continue;
                }

                if (!sets.ContainsKey(setIndex))
                {
                    MeasurementSetViewModel set = new MeasurementSetViewModel();
                    set.SetName = "측정부";
                    set.Unit = globalUnit;
                    sets[setIndex] = set;
                }

                ApplyBulkMeasurementFieldToSet(sets[setIndex], itemName, ResolveMeasurementFieldKind(header), GetCsvValue(values, headerIndex));
                if (setIndex > maxSetIndex)
                {
                    maxSetIndex = setIndex;
                }
            }

            int regionId = 1;
            int activeSetCount = 0;
            for (int setIndex = 1; setIndex <= maxSetIndex; setIndex++)
            {
                if (sets.ContainsKey(setIndex) && sets[setIndex].HasAnyValue())
                {
                    activeSetCount++;
                }
            }

            bool useSingleSetName = activeSetCount <= 1;
            int outputSetIndex = 1;
            for (int setIndex = 1; setIndex <= maxSetIndex; setIndex++)
            {
                if (sets.ContainsKey(setIndex) && sets[setIndex].HasAnyValue())
                {
                    sets[setIndex].AddRegionsToPart(part, outputSetIndex, useSingleSetName, ref regionId);
                    outputSetIndex++;
                }
            }
        }

        private string ResolveMeasurementItemName(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return string.Empty;
            }

            if (header.Contains("길이"))
            {
                return "길이";
            }

            if (header.Contains("너비"))
            {
                return "너비";
            }

            if (header.Contains("높이"))
            {
                return "높이";
            }

            if (header.Contains("두께"))
            {
                return "두께";
            }

            return string.Empty;
        }

        private string ResolveMeasurementFieldKind(string header)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return "값";
            }

            if (header.Contains("허용"))
            {
                return "허용";
            }

            if (header.Contains("단위"))
            {
                return "단위";
            }

            return "값";
        }

        private int ResolveMeasurementSetIndexFromHeader(string header)
        {
            StringBuilder numberBuilder = new StringBuilder();
            bool numberStarted = false;
            foreach (char character in header)
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

            int setIndex;
            if (int.TryParse(numberBuilder.ToString(), out setIndex) && setIndex > 0)
            {
                return setIndex;
            }

            return 1;
        }

        private void ApplyBulkMeasurementFieldToSet(MeasurementSetViewModel set, string itemName, string fieldKind, string value)
        {
            if (itemName == "길이")
            {
                ApplyLengthField(set, fieldKind, value);
            }
            else if (itemName == "너비")
            {
                ApplyWidthField(set, fieldKind, value);
            }
            else if (itemName == "높이")
            {
                ApplyHeightField(set, fieldKind, value);
            }
            else if (itemName == "두께")
            {
                ApplyThicknessField(set, fieldKind, value);
            }
        }

        private void ApplyLengthField(MeasurementSetViewModel set, string fieldKind, string value)
        {
            if (fieldKind == "허용")
            {
                set.LengthTolerance = NormalizeBulkMetadataValue(value, "0");
            }
            else if (fieldKind == "단위")
            {
                set.Unit = NormalizeBulkMetadataValue(value, "mm");
            }
            else
            {
                set.LengthValue = NormalizeBulkMeasurementValue(value);
            }
        }

        private void ApplyWidthField(MeasurementSetViewModel set, string fieldKind, string value)
        {
            if (fieldKind == "허용")
            {
                set.WidthTolerance = NormalizeBulkMetadataValue(value, "0");
            }
            else if (fieldKind == "단위")
            {
                set.Unit = NormalizeBulkMetadataValue(value, "mm");
            }
            else
            {
                set.WidthValue = NormalizeBulkMeasurementValue(value);
            }
        }

        private void ApplyHeightField(MeasurementSetViewModel set, string fieldKind, string value)
        {
            if (fieldKind == "허용")
            {
                set.HeightTolerance = NormalizeBulkMetadataValue(value, "0");
            }
            else if (fieldKind == "단위")
            {
                set.Unit = NormalizeBulkMetadataValue(value, "mm");
            }
            else
            {
                set.HeightValue = NormalizeBulkMeasurementValue(value);
            }
        }

        private void ApplyThicknessField(MeasurementSetViewModel set, string fieldKind, string value)
        {
            if (fieldKind == "허용")
            {
                set.ThicknessTolerance = NormalizeBulkMetadataValue(value, "0");
            }
            else if (fieldKind == "단위")
            {
                set.Unit = NormalizeBulkMetadataValue(value, "mm");
            }
            else
            {
                set.ThicknessValue = NormalizeBulkMeasurementValue(value);
            }
        }

        private string NormalizeBulkMeasurementValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value.Trim();
        }

        private string NormalizeBulkMetadataValue(string value, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            {
                return defaultValue;
            }

            return value.Trim();
        }

        private BulkPartCsvRowViewModel BuildBulkPartCsvRow(Part part, string resultMessage)
        {
            BulkPartCsvRowViewModel row = new BulkPartCsvRowViewModel();
            row.PartNo = part.PartNo;
            row.PartName = part.PartName;
            row.CategoryCode = part.CategoryCode;
            row.CategoryDescription = part.CategoryDescription;
            row.PartType = part.PartType;
            row.MeasurementSummary = BuildMeasurementRegionSummary(part);
            ApplyFirstMeasurementSetToBulkRow(row, part);
            row.ResultMessage = string.IsNullOrWhiteSpace(resultMessage) ? "정상" : resultMessage;
            return row;
        }

        private void ApplyFirstMeasurementSetToBulkRow(BulkPartCsvRowViewModel row, Part part)
        {
            Dictionary<int, MeasurementSetViewModel> sets = BuildMeasurementSetsByIndex(part);
            if (!sets.ContainsKey(1))
            {
                return;
            }

            MeasurementSetViewModel set = sets[1];
            row.Measurement1LengthValue = set.LengthValue;
            row.Measurement1LengthTolerance = set.LengthTolerance;
            row.Measurement1WidthValue = set.WidthValue;
            row.Measurement1WidthTolerance = set.WidthTolerance;
            row.Measurement1HeightValue = set.HeightValue;
            row.Measurement1HeightTolerance = set.HeightTolerance;
            row.Measurement1ThicknessValue = set.ThicknessValue;
            row.Measurement1ThicknessTolerance = set.ThicknessTolerance;
            row.MeasurementUnit = set.Unit;
        }

        private string BuildMeasurementRegionSummary(Part part)
        {
            if (part.MeasurementRegions.Count == 0)
            {
                return "-";
            }

            StringBuilder builder = new StringBuilder();
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(region.Name);
                builder.Append(": ");
                builder.Append(region.NominalValue.ToString("0.###"));
                builder.Append(" / 허용 ");
                builder.Append(FormatTolerance(region));
                builder.Append(" / ");
                builder.Append(string.IsNullOrWhiteSpace(region.Unit) ? "mm" : region.Unit);
            }

            return builder.ToString();
        }

        private IList<string> BuildAllPartsCsvLines(IList<Part> parts)
        {
            IList<string> lines = new List<string>();
            int maxSetCount = ResolveMaxMeasurementSetCount(parts);
            IList<string> headers = BuildPartCsvHeaders(maxSetCount);
            lines.Add(BuildCsvLine(headers));

            foreach (Part part in parts)
            {
                lines.Add(BuildCsvLine(BuildPartCsvValues(part, maxSetCount)));
            }

            return lines;
        }

        private int ResolveMaxMeasurementSetCount(IList<Part> parts)
        {
            return 1;
        }

        private IList<string> BuildPartCsvHeaders(int maxSetCount)
        {
            IList<string> headers = new List<string>();
            headers.Add("품번");
            headers.Add("품명");
            headers.Add("분류코드");
            headers.Add("분류설명");
            headers.Add("구분");

            for (int setIndex = 1; setIndex <= maxSetCount; setIndex++)
            {
                string prefix = BuildMeasurementCsvPrefix(setIndex);
                headers.Add(prefix + "_길이");
                headers.Add(prefix + "_길이_허용");
                headers.Add(prefix + "_너비");
                headers.Add(prefix + "_너비_허용");
                headers.Add(prefix + "_높이");
                headers.Add(prefix + "_높이_허용");
                headers.Add(prefix + "_두께");
                headers.Add(prefix + "_두께_허용");
                headers.Add(setIndex <= 1 ? "단위" : prefix + "_단위");
            }

            return headers;
        }

        private string BuildMeasurementCsvPrefix(int setIndex)
        {
            if (setIndex <= 1)
            {
                return "측정부";
            }

            return "측정부" + setIndex.ToString();
        }

        private IList<string> BuildPartCsvValues(Part part, int maxSetCount)
        {
            IList<string> values = new List<string>();
            values.Add(part.PartNo);
            values.Add(part.PartName);
            values.Add(part.CategoryCode);
            values.Add(part.CategoryDescription);
            values.Add(part.PartType);

            Dictionary<int, MeasurementSetViewModel> sets = BuildMeasurementSetsByIndex(part);
            for (int setIndex = 1; setIndex <= maxSetCount; setIndex++)
            {
                if (sets.ContainsKey(setIndex))
                {
                    MeasurementSetViewModel set = sets[setIndex];
                    AddMeasurementCsvValues(values, set.LengthValue, set.LengthTolerance);
                    AddMeasurementCsvValues(values, set.WidthValue, set.WidthTolerance);
                    AddMeasurementCsvValues(values, set.HeightValue, set.HeightTolerance);
                    AddMeasurementCsvValues(values, set.ThicknessValue, set.ThicknessTolerance);
                    values.Add(NormalizeBulkMetadataValue(set.Unit, "mm"));
                }
                else
                {
                    AddUnusedMeasurementCsvValues(values);
                    AddUnusedMeasurementCsvValues(values);
                    AddUnusedMeasurementCsvValues(values);
                    AddUnusedMeasurementCsvValues(values);
                    values.Add("mm");
                }
            }

            return values;
        }

        private void AddMeasurementCsvValues(IList<string> values, string value, string tolerance)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            {
                AddUnusedMeasurementCsvValues(values);
                return;
            }

            values.Add(value);
            values.Add(NormalizeBulkMetadataValue(tolerance, "0"));
        }

        private void AddUnusedMeasurementCsvValues(IList<string> values)
        {
            values.Add("-");
            values.Add("-");
        }

        private Dictionary<int, MeasurementSetViewModel> BuildMeasurementSetsByIndex(Part part)
        {
            Dictionary<int, MeasurementSetViewModel> sets = new Dictionary<int, MeasurementSetViewModel>();
            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                int setIndex = ResolveMeasurementSetIndex(region.Name);
                if (!sets.ContainsKey(setIndex))
                {
                    MeasurementSetViewModel set = new MeasurementSetViewModel();
                    set.SetName = "측정부";
                    sets[setIndex] = set;
                }

                ApplyRegionToSet(sets[setIndex], region);
            }

            return sets;
        }

        private string GetCsvValue(IList<string> headers, IList<string> values, params string[] headerNames)
        {
            for (int headerIndex = 0; headerIndex < headers.Count; headerIndex++)
            {
                string normalizedHeader = NormalizeCsvCell(headers[headerIndex]);
                foreach (string headerName in headerNames)
                {
                    if (string.Equals(normalizedHeader, headerName, StringComparison.OrdinalIgnoreCase))
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
                    if (string.Equals(normalizedHeader, headerName, StringComparison.OrdinalIgnoreCase))
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

            if (!ContainsKeyword(historyRow.PartType, HistoryPartTypeKeyword))
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
            HistoryPartTypeKeyword = string.Empty;
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
            File.WriteAllLines(filePath, lines, new UTF8Encoding(true));
            HistoryMessage = "조회된 검사 이력 " + InspectionHistory.Count.ToString() + "건을 CSV 파일로 저장했습니다.";
        }

        private IList<string> BuildHistoryCsvLines()
        {
            IList<string> lines = new List<string>();
            IList<string> measurementKeys = BuildCsvMeasurementKeys();
            lines.Add(BuildCsvLine(BuildCsvHeaders(measurementKeys)));

            foreach (InspectionRowViewModel row in InspectionHistory)
            {
                lines.Add(BuildCsvLine(BuildCsvRow(row, measurementKeys)));
            }

            return lines;
        }

        private IList<string> BuildCsvMeasurementKeys()
        {
            IList<string> measurementKeys = new List<string>();
            foreach (InspectionRowViewModel row in InspectionHistory)
            {
                foreach (string measurementKey in row.MeasurementKeys)
                {
                    if (!ContainsText(measurementKeys, measurementKey))
                    {
                        measurementKeys.Add(measurementKey);
                    }
                }
            }

            return measurementKeys;
        }

        private IList<string> BuildCsvHeaders(IList<string> measurementKeys)
        {
            IList<string> headers = new List<string>();
            headers.Add("ID");
            headers.Add("검사일");
            headers.Add("품번");
            headers.Add("품명");
            headers.Add("분류코드");
            headers.Add("분류설명");
            headers.Add("구분");
            headers.Add("NG결과");
            headers.Add("불일치 항목");

            foreach (string measurementKey in measurementKeys)
            {
                headers.Add(measurementKey + "_측정값");
                headers.Add(measurementKey + "_기준값");
                headers.Add(measurementKey + "_판정");
            }

            headers.Add("결과");
            headers.Add("검사시간");
            headers.Add("메시지");
            return headers;
        }

        private IList<string> BuildCsvRow(InspectionRowViewModel row, IList<string> measurementKeys)
        {
            IList<string> values = new List<string>();
            values.Add(row.Id.ToString());
            values.Add(row.InspectedAt);
            values.Add(row.PartNo);
            values.Add(row.PartName);
            values.Add(row.CategoryCode);
            values.Add(row.CategoryDescription);
            values.Add(row.PartType);
            values.Add(row.NgResult);
            values.Add(row.MismatchItems);

            foreach (string measurementKey in measurementKeys)
            {
                values.Add(row.GetMeasuredValue(measurementKey));
                values.Add(row.GetNominalValue(measurementKey));
                values.Add(row.GetMeasurementResult(measurementKey));
            }

            values.Add(row.Result);
            values.Add(row.Elapsed);
            values.Add(row.Message);
            return values;
        }

        private bool ContainsText(IList<string> values, string text)
        {
            foreach (string value in values)
            {
                if (string.Equals(value, text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
            StatisticsSummary summary = _statisticsService.BuildSummary();
            TotalPartCount = summary.TotalPartCount;
            TotalInspectionCount = summary.TotalInspectionCount;
            OkCount = summary.OkCount;
            NgCount = summary.NgCount;
            ErrorCount = summary.ErrorCount;
            AverageInspectionTime = summary.AverageInspectionMilliseconds.ToString("0.0") + " ms";
        }

        private void ExecuteRefreshStatistics(object parameter)
        {
            RefreshStatistics();
        }

        /// <summary>
        /// 옵션 화면의 카메라 연결/설정 상태를 최신 값으로 갱신합니다.
        /// 실제 SDK가 연결되면 이 목록에서 6대 카메라의 연결 성공 여부와 마지막 프레임 정보를 확인합니다.
        /// </summary>
        private void RefreshCameraStatuses(bool verifyVideoSignal)
        {
            CameraChannels.Clear();

            try
            {
                IList<CameraChannelStatus> statuses = verifyVideoSignal ? BuildVerifiedCameraStatuses() : _cameraService.GetChannelStatuses();
                foreach (CameraChannelStatus status in statuses)
                {
                    CameraChannels.Add(new CameraChannelStatusViewModel(status));
                }

                if (CameraChannels.Count > 0)
                {
                    SelectedCameraChannel = CameraChannels[0];
                }

                CameraStatusMessage = verifyVideoSignal
                    ? "카메라 채널 " + CameraChannels.Count.ToString() + "개 영상 수신 상태를 확인했습니다."
                    : "카메라 채널 " + CameraChannels.Count.ToString() + "개 설정 상태를 읽었습니다.";
            }
            catch (Exception ex)
            {
                CameraStatusMessage = "카메라 상태 조회 실패: " + ex.Message;
            }
        }

        private IList<CameraChannelStatus> BuildVerifiedCameraStatuses()
        {
            IList<CameraChannelStatus> verifiedStatuses = new List<CameraChannelStatus>();
            IList<CameraChannelStatus> currentStatuses = _cameraService.GetChannelStatuses();

            foreach (CameraChannelStatus status in currentStatuses)
            {
                if (status.IsEnabled)
                {
                    verifiedStatuses.Add(_cameraService.TestChannelConnection(status.ViewType));
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
            RefreshCameraStatuses(true);
        }

        private void ExecuteReloadCameraConfiguration(object parameter)
        {
            try
            {
                _cameraService.ReloadConfiguration();
                ApplyLiveStreamUrls();
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
