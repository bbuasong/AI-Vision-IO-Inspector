using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AI.Vision.IOInspector.App.Services;
using AI.Vision.IOInspector.App.Stores;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Application.Services;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 메인 화면의 전체 상태를 관리합니다.
    /// DB 조회/확인, 부품 생성/변경/삭제, 검사, 이력, 통계를 화면별로 연결하되 업무 로직은 서비스로 위임합니다.
    /// </summary>
    public class MainWindowViewModel : ObservableObject
    {
        private const int MaxSearchSuggestionCount = 10;

        private readonly PartDataStore _partDataStore;
        private readonly InspectionWorkflowService _inspectionWorkflowService;
        private readonly StatisticsService _statisticsService;
        private readonly IInspectionRepository _inspectionRepository;
        private readonly ICameraService _cameraService;
        private readonly IReferenceImageFileService _referenceImageFileService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IList<InspectionRowViewModel> _allInspectionHistory;
        private readonly DispatcherTimer _searchDelayTimer;

        private PartViewModel _selectedPart;
        private int _selectedTabIndex;
        private string _inputCode;
        private string _statusText;
        private string _resultText;
        private string _searchPartNo;
        private string _searchPartName;
        private string _searchCategoryCode;
        private string _searchCategoryDescription;
        private string _registrationPartNo;
        private string _registrationPartName;
        private string _registrationCategoryCode;
        private string _registrationCategoryDescription;
        private string _registrationPartType;
        private string _registrationMessage;
        private MeasurementSetViewModel _selectedRegistrationMeasurementSet;
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
        private string _historyTimeKeyword;
        private string _historyPartNoKeyword;
        private string _historyPartNameKeyword;
        private string _historyCategoryCodeKeyword;
        private string _historyCategoryDescriptionKeyword;
        private string _historyPartTypeKeyword;
        private string _historyNgResultKeyword;
        private string _cameraStatusMessage;
        private bool _deleteRequested;

        public MainWindowViewModel(
            PartDataStore partDataStore,
            InspectionWorkflowService inspectionWorkflowService,
            StatisticsService statisticsService,
            IInspectionRepository inspectionRepository,
            ICameraService cameraService,
            IReferenceImageFileService referenceImageFileService,
            IFileDialogService fileDialogService)
        {
            _partDataStore = partDataStore;
            _inspectionWorkflowService = inspectionWorkflowService;
            _statisticsService = statisticsService;
            _inspectionRepository = inspectionRepository;
            _cameraService = cameraService;
            _referenceImageFileService = referenceImageFileService;
            _fileDialogService = fileDialogService;
            _allInspectionHistory = new List<InspectionRowViewModel>();

            Parts = new ObservableCollection<PartViewModel>();
            DbParts = new ObservableCollection<PartViewModel>();
            SearchSuggestions = new ObservableCollection<string>();
            ImageSlots = new ObservableCollection<ImageSlotViewModel>();
            InspectionMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailImages = new ObservableCollection<ImageEditViewModel>();
            RegistrationMeasurementSets = new ObservableCollection<MeasurementSetViewModel>();
            RegistrationImages = new ObservableCollection<ImageEditViewModel>();
            ReferenceImageViewTypes = new ObservableCollection<string>();
            BulkPartRows = new ObservableCollection<BulkPartCsvRowViewModel>();
            InspectionHistory = new ObservableCollection<InspectionRowViewModel>();
            EventRows = new ObservableCollection<EventRowViewModel>();
            CameraChannels = new ObservableCollection<CameraChannelStatusViewModel>();

            RunInspectionCommand = new RelayCommand(ExecuteRunInspection, CanRunInspection);
            SavePartCommand = new RelayCommand(ExecuteSavePart);
            NewPartCommand = new RelayCommand(ExecuteNewPart);
            DeletePartCommand = new RelayCommand(ExecuteDeletePart);
            SearchCommand = new RelayCommand(ExecuteSearch);
            AddMeasurementSetCommand = new RelayCommand(ExecuteAddMeasurementSet);
            RemoveMeasurementSetCommand = new RelayCommand(ExecuteRemoveMeasurementSet);
            AddReferenceImageCommand = new RelayCommand(ExecuteAddReferenceImage);
            SaveCurrentCameraImagesCommand = new RelayCommand(ExecuteSaveCurrentCameraImages);
            RemoveReferenceImageCommand = new RelayCommand(ExecuteRemoveReferenceImage);
            ImportPartsCsvCommand = new RelayCommand(ExecuteImportPartsCsv);
            ExportAllPartsCsvCommand = new RelayCommand(ExecuteExportAllPartsCsv);
            SaveHistoryCsvCommand = new RelayCommand(ExecuteSaveHistoryCsv);
            ClearHistorySearchCommand = new RelayCommand(ExecuteClearHistorySearch);
            RefreshStatisticsCommand = new RelayCommand(ExecuteRefreshStatistics);
            RefreshCameraStatusCommand = new RelayCommand(ExecuteRefreshCameraStatus);
            ReloadCameraConfigurationCommand = new RelayCommand(ExecuteReloadCameraConfiguration);
            ShowInspectionTabCommand = new RelayCommand(ExecuteShowInspectionTab);
            ShowRegistrationTabCommand = new RelayCommand(ExecuteShowRegistrationTab);
            ShowDbTabCommand = new RelayCommand(ExecuteShowDbTab);
            ShowHistoryTabCommand = new RelayCommand(ExecuteShowHistoryTab);
            ShowStatisticsTabCommand = new RelayCommand(ExecuteShowStatisticsTab);

            _searchDelayTimer = new DispatcherTimer();
            _searchDelayTimer.Interval = TimeSpan.FromMilliseconds(250);
            _searchDelayTimer.Tick += OnSearchDelayTimerTick;

            StatusText = "대기";
            ResultText = "검사 전";
            InitializeReferenceImageViewTypes();
            InitializeImageSlots();
            InitializeEmptyRegistrationSets();
            LoadParts();
            RefreshHistory();
            RefreshStatistics();
            RefreshCameraStatuses();
        }

        public ObservableCollection<PartViewModel> Parts { get; private set; }

        public ObservableCollection<PartViewModel> DbParts { get; private set; }

        public ObservableCollection<string> SearchSuggestions { get; private set; }

        public ObservableCollection<ImageSlotViewModel> ImageSlots { get; private set; }

        public ObservableCollection<MeasurementRowViewModel> InspectionMeasurements { get; private set; }

        public ObservableCollection<MeasurementRowViewModel> DbDetailMeasurements { get; private set; }

        public ObservableCollection<ImageEditViewModel> DbDetailImages { get; private set; }

        public ObservableCollection<MeasurementSetViewModel> RegistrationMeasurementSets { get; private set; }

        public ObservableCollection<ImageEditViewModel> RegistrationImages { get; private set; }

        public ObservableCollection<string> ReferenceImageViewTypes { get; private set; }

        public ObservableCollection<BulkPartCsvRowViewModel> BulkPartRows { get; private set; }

        public ObservableCollection<InspectionRowViewModel> InspectionHistory { get; private set; }

        public ObservableCollection<EventRowViewModel> EventRows { get; private set; }

        public ObservableCollection<CameraChannelStatusViewModel> CameraChannels { get; private set; }

        public ICommand RunInspectionCommand { get; private set; }

        public ICommand SavePartCommand { get; private set; }

        public ICommand NewPartCommand { get; private set; }

        public ICommand DeletePartCommand { get; private set; }

        public ICommand SearchCommand { get; private set; }

        public ICommand AddMeasurementSetCommand { get; private set; }

        public ICommand RemoveMeasurementSetCommand { get; private set; }

        public ICommand AddReferenceImageCommand { get; private set; }

        public ICommand SaveCurrentCameraImagesCommand { get; private set; }

        public ICommand RemoveReferenceImageCommand { get; private set; }

        public ICommand ImportPartsCsvCommand { get; private set; }

        public ICommand ExportAllPartsCsvCommand { get; private set; }

        public ICommand SaveHistoryCsvCommand { get; private set; }

        public ICommand ClearHistorySearchCommand { get; private set; }

        public ICommand RefreshStatisticsCommand { get; private set; }

        public ICommand RefreshCameraStatusCommand { get; private set; }

        public ICommand ReloadCameraConfigurationCommand { get; private set; }

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

        public string SearchPartNo
        {
            get { return _searchPartNo; }
            set
            {
                if (SetProperty(ref _searchPartNo, value))
                {
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

        public MeasurementSetViewModel SelectedRegistrationMeasurementSet
        {
            get { return _selectedRegistrationMeasurementSet; }
            set { SetProperty(ref _selectedRegistrationMeasurementSet, value); }
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

        public string HistoryTimeKeyword
        {
            get { return _historyTimeKeyword; }
            set
            {
                if (SetProperty(ref _historyTimeKeyword, value))
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

        private void LoadParts()
        {
            _partDataStore.LoadFromDatabase();
            RefreshPartCollectionsFromDataStore();
        }

        private void RefreshPartCollectionsFromDataStore()
        {
            string selectedPartNo = SelectedPart == null ? string.Empty : SelectedPart.PartNo;
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

            if (Parts.Count > 0 && SelectedPart == null)
            {
                SelectedPart = Parts[0];
            }

            if (Parts.Count == 0)
            {
                SelectedPart = null;
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
            LoadDbDetail(SelectedPart.Part);
            LoadRegistrationForm(SelectedPart.Part);
        }

        private void ClearSelectedPartDetails()
        {
            DbDetailMeasurements.Clear();
            DbDetailImages.Clear();
            SelectedDbDetailImage = null;
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
        }

        private void AddImageSlot(string title)
        {
            ImageSlotViewModel slot = new ImageSlotViewModel();
            slot.Title = title;
            slot.ReferenceImagePath = string.Empty;
            slot.LiveImagePath = string.Empty;
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

            LoadRegistrationMeasurementSets(part);
            LoadRegistrationImages(part);
            _deleteRequested = false;
            RegistrationMessage = "선택한 부품 정보를 편집할 수 있습니다.";
        }

        private void LoadRegistrationMeasurementSets(Part part)
        {
            RegistrationMeasurementSets.Clear();
            Dictionary<int, MeasurementSetViewModel> sets = new Dictionary<int, MeasurementSetViewModel>();
            int maxSetIndex = 0;

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
                if (setIndex > maxSetIndex)
                {
                    maxSetIndex = setIndex;
                }
            }

            for (int setIndex = 1; setIndex <= maxSetIndex; setIndex++)
            {
                if (sets.ContainsKey(setIndex))
                {
                    RegistrationMeasurementSets.Add(sets[setIndex]);
                }
            }

            if (RegistrationMeasurementSets.Count == 0)
            {
                AddDefaultMeasurementSet();
            }
            else
            {
                RenameMeasurementSets();
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
                set.LengthUnit = unit;
            }
            else if (region.Name.Contains("너비"))
            {
                set.WidthValue = value;
                set.WidthTolerance = tolerance;
                set.WidthUnit = unit;
            }
            else if (region.Name.Contains("높이"))
            {
                set.HeightValue = value;
                set.HeightTolerance = tolerance;
                set.HeightUnit = unit;
            }
            else if (region.Name.Contains("두께"))
            {
                set.ThicknessValue = value;
                set.ThicknessTolerance = tolerance;
                set.ThicknessUnit = unit;
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

        private void InitializeEmptyRegistrationSets()
        {
            RegistrationMeasurementSets.Clear();
            AddDefaultMeasurementSet();
        }

        private void AddDefaultMeasurementSet()
        {
            MeasurementSetViewModel set = new MeasurementSetViewModel();
            set.SetName = BuildMeasurementSetName(RegistrationMeasurementSets.Count + 1);
            RegistrationMeasurementSets.Add(set);
            RenameMeasurementSets();
            SelectedRegistrationMeasurementSet = set;
        }

        private bool CanRunInspection(object parameter)
        {
            return !string.IsNullOrWhiteSpace(InputCode);
        }

        private void ExecuteRunInspection(object parameter)
        {
            StatusText = "검사중";
            EventRows.Clear();

            Inspection inspection = _inspectionWorkflowService.RunInspection(InputCode);
            ResultText = inspection.Result + " - " + inspection.ResultMessage;
            StatusText = inspection.Result == InspectionResult.Error ? "오류" : "검사 완료";

            ApplyInspectionPartContext(inspection);
            LoadCapturedImages(inspection);
            LoadInspectionMeasurements(inspection);
            LoadEvents(inspection);
            RefreshHistory();
            RefreshStatistics();
            RefreshCameraStatuses();
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
                ImageSlots[index].ResultText = BuildSlotResultText(inspection.Result);
                ImageSlots[index].ResultBrush = BuildSlotResultBrush(inspection.Result);
            }
        }

        private void ClearLiveImageSlots()
        {
            foreach (ImageSlotViewModel slot in ImageSlots)
            {
                slot.LiveImagePath = string.Empty;
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

        private void ExecuteSavePart(object parameter)
        {
            // 삭제도 생성/수정과 동일하게 DB 저장 버튼에서 실제 반영합니다.
            // 사용자가 실수로 삭제 버튼을 누른 경우 즉시 데이터가 사라지지 않게 하기 위한 흐름입니다.
            if (_deleteRequested)
            {
                Part deleteTarget = _partDataStore.GetPart(RegistrationPartNo);
                if (deleteTarget == null)
                {
                    deleteTarget = BuildRegistrationPart();
                }

                string imageDeleteMessage;
                if (!_referenceImageFileService.DeleteReferenceImagesForPart(deleteTarget, out imageDeleteMessage))
                {
                    RegistrationMessage = imageDeleteMessage;
                    return;
                }

                RegistrationMessage = _partDataStore.DeletePart(RegistrationPartNo);
                _deleteRequested = false;
                ExecuteNewPart(null);
                RefreshPartCollectionsFromDataStore();
                RefreshStatistics();
                return;
            }

            Part part = BuildRegistrationPart();
            RegistrationMessage = _partDataStore.SavePart(part);
            RefreshPartCollectionsFromDataStore();
            SelectedPart = FindPartViewModel(part.PartNo);
            RefreshStatistics();
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

        private Part BuildRegistrationPart()
        {
            Part part = new Part();
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

            int regionId = 1;
            IList<MeasurementSetViewModel> activeSets = new List<MeasurementSetViewModel>();
            foreach (MeasurementSetViewModel set in RegistrationMeasurementSets)
            {
                if (set.HasAnyValue())
                {
                    activeSets.Add(set);
                }
            }

            bool useSingleSetName = activeSets.Count <= 1;
            int setIndex = 1;
            foreach (MeasurementSetViewModel set in activeSets)
            {
                set.AddRegionsToPart(part, setIndex, useSingleSetName, ref regionId);
                setIndex++;
            }

            return part;
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
                if (part.PartNo == partNo)
                {
                    return part;
                }
            }

            return null;
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
            InitializeEmptyRegistrationSets();
            _deleteRequested = false;
            RegistrationMessage = "신규 부품 정보를 입력하세요.";
            SelectedTabIndex = 2;
        }

        private void ExecuteSearch(object parameter)
        {
            _searchDelayTimer.Stop();
            ApplySearchFilters();
        }

        private void QueueSearchFilterRefresh()
        {
            // 키 입력마다 즉시 전체 UI를 갱신하지 않고 짧게 모아 처리하여 검색창 입력 지연을 줄입니다.
            _searchDelayTimer.Stop();
            _searchDelayTimer.Start();
        }

        private void OnSearchDelayTimerTick(object sender, EventArgs e)
        {
            _searchDelayTimer.Stop();
            ApplySearchFilters();
        }

        private void ApplySearchFilters()
        {
            PartSearchCriteria criteria = BuildPartSearchCriteria();
            IList<PartViewModel> filteredParts = new List<PartViewModel>();
            foreach (Part part in _partDataStore.SearchParts(criteria))
            {
                filteredParts.Add(new PartViewModel(part));
            }

            // 수천 건 검색 결과를 한 줄씩 추가하면 UI 알림이 반복되어 입력 지연이 발생하므로 컬렉션을 한 번에 교체합니다.
            DbParts = new ObservableCollection<PartViewModel>(filteredParts);
            OnPropertyChanged("DbParts");

            SearchSuggestions.Clear();
            foreach (string suggestion in _partDataStore.BuildSearchSuggestions(criteria, MaxSearchSuggestionCount))
            {
                SearchSuggestions.Add(suggestion);
            }
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

        private void ExecuteAddMeasurementSet(object parameter)
        {
            AddDefaultMeasurementSet();
        }

        private void ExecuteRemoveMeasurementSet(object parameter)
        {
            if (SelectedRegistrationMeasurementSet == null)
            {
                RegistrationMessage = "삭제할 측정부 세트를 선택하세요.";
                return;
            }

            RegistrationMeasurementSets.Remove(SelectedRegistrationMeasurementSet);
            RenameMeasurementSets();
        }

        private void RenameMeasurementSets()
        {
            int index = 1;
            foreach (MeasurementSetViewModel set in RegistrationMeasurementSets)
            {
                set.SetName = BuildMeasurementSetName(index);
                index++;
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
        /// 현재 메인 화면의 6개 실시간 카메라 이미지를 기준 이미지로 저장합니다.
        /// 실제 카메라 연동 후 LiveImagePath가 파일 경로로 들어오면 이 기능이 기준 이미지 일괄 캡처 역할을 합니다.
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

            int savedCount = 0;
            int skippedCount = 0;
            ImageEditViewModel lastSavedImageViewModel = null;
            ImageViewType[] viewOrder = GetReferenceImageViewOrder();
            for (int index = 0; index < viewOrder.Length; index++)
            {
                if (index >= ImageSlots.Count)
                {
                    skippedCount++;
                    continue;
                }

                string liveImagePath = ImageSlots[index].LiveImagePath;
                if (string.IsNullOrWhiteSpace(liveImagePath) || !File.Exists(liveImagePath))
                {
                    skippedCount++;
                    continue;
                }

                ImageViewType viewType = viewOrder[index];
                ImageEditViewModel existingImageViewModel = FindRegistrationImageByViewType(viewType);
                PartImage existingImage = existingImageViewModel == null ? null : existingImageViewModel.Image;
                try
                {
                    PartImage savedImage = _referenceImageFileService.AddReferenceImage(tempPart, liveImagePath, viewType, existingImage);
                    UpsertRegistrationImage(savedImage, existingImageViewModel, out lastSavedImageViewModel);
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
                RegistrationMessage = "저장할 현재 화면 이미지 파일이 없습니다. 실제 카메라 연동 후 파일 경로가 있는 프레임에서 사용할 수 있습니다.";
                return;
            }

            RegistrationMessage = "현재 화면 이미지 " + savedCount.ToString() + "개를 기준 이미지로 저장했습니다. 저장 제외 " + skippedCount.ToString() + "개.";
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
        /// CSV 파일의 여러 부품 기준정보를 읽어 현재 DB 저장소에 생성/수정합니다.
        /// 컬럼명은 품번/품명/분류코드/분류설명/구분과 측정부N_길이/너비/높이/두께 형식을 기준으로 합니다.
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
            IList<string> headers = NormalizeCsvCells(ParseCsvLine(lines[0]));
            if (!HasCsvHeader(headers, "품번", "PartNo", "Part No", "Part No.") ||
                !HasCsvHeader(headers, "품명", "PartName", "Part Name"))
            {
                BulkRegistrationMessage = "CSV 필수 헤더를 찾을 수 없습니다. 품번/품명 컬럼을 확인하세요.";
                return;
            }

            int savedCount = 0;
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
                    saveMessage = _partDataStore.SavePart(part);
                    if (saveMessage == PartCatalogService.SaveSuccessMessage)
                    {
                        savedCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    saveMessage = "CSV " + (lineIndex + 1).ToString() + "행 처리 중 오류: " + ex.Message;
                    part = new Part();
                    part.PartNo = "CSV 행 " + (lineIndex + 1).ToString();
                    part.PartName = "-";
                }

                BulkPartRows.Add(BuildBulkPartCsvRow(part, saveMessage));
            }

            RefreshPartCollectionsFromDataStore();
            RefreshStatistics();
            BulkRegistrationMessage = "CSV 불러오기 완료: 저장 " + savedCount.ToString() + "건, 실패 " + failedCount.ToString() + "건";
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
            for (int headerIndex = 0; headerIndex < headers.Count; headerIndex++)
            {
                string header = headers[headerIndex];
                string itemName = ResolveMeasurementItemName(header);
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    continue;
                }

                int setIndex = ResolveMeasurementSetIndexFromHeader(header);
                if (!sets.ContainsKey(setIndex))
                {
                    MeasurementSetViewModel set = new MeasurementSetViewModel();
                    set.SetName = "측정부";
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
                set.LengthUnit = NormalizeBulkMetadataValue(value, "mm");
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
                set.WidthUnit = NormalizeBulkMetadataValue(value, "mm");
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
                set.HeightUnit = NormalizeBulkMetadataValue(value, "mm");
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
                set.ThicknessUnit = NormalizeBulkMetadataValue(value, "mm");
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
            row.ResultMessage = resultMessage;
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
            row.Measurement1LengthUnit = set.LengthUnit;
            row.Measurement1WidthValue = set.WidthValue;
            row.Measurement1WidthTolerance = set.WidthTolerance;
            row.Measurement1WidthUnit = set.WidthUnit;
            row.Measurement1HeightValue = set.HeightValue;
            row.Measurement1HeightTolerance = set.HeightTolerance;
            row.Measurement1HeightUnit = set.HeightUnit;
            row.Measurement1ThicknessValue = set.ThicknessValue;
            row.Measurement1ThicknessTolerance = set.ThicknessTolerance;
            row.Measurement1ThicknessUnit = set.ThicknessUnit;
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
            int maxSetCount = 1;
            foreach (Part part in parts)
            {
                foreach (MeasurementRegion region in part.MeasurementRegions)
                {
                    int setIndex = ResolveMeasurementSetIndex(region.Name);
                    if (setIndex > maxSetCount)
                    {
                        maxSetCount = setIndex;
                    }
                }
            }

            return maxSetCount;
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
                headers.Add(prefix + "_길이_단위");
                headers.Add(prefix + "_너비");
                headers.Add(prefix + "_너비_허용");
                headers.Add(prefix + "_너비_단위");
                headers.Add(prefix + "_높이");
                headers.Add(prefix + "_높이_허용");
                headers.Add(prefix + "_높이_단위");
                headers.Add(prefix + "_두께");
                headers.Add(prefix + "_두께_허용");
                headers.Add(prefix + "_두께_단위");
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
                    AddMeasurementCsvValues(values, set.LengthValue, set.LengthTolerance, set.LengthUnit);
                    AddMeasurementCsvValues(values, set.WidthValue, set.WidthTolerance, set.WidthUnit);
                    AddMeasurementCsvValues(values, set.HeightValue, set.HeightTolerance, set.HeightUnit);
                    AddMeasurementCsvValues(values, set.ThicknessValue, set.ThicknessTolerance, set.ThicknessUnit);
                }
                else
                {
                    AddUnusedMeasurementCsvValues(values);
                    AddUnusedMeasurementCsvValues(values);
                    AddUnusedMeasurementCsvValues(values);
                    AddUnusedMeasurementCsvValues(values);
                }
            }

            return values;
        }

        private void AddMeasurementCsvValues(IList<string> values, string value, string tolerance, string unit)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == "-")
            {
                AddUnusedMeasurementCsvValues(values);
                return;
            }

            values.Add(value);
            values.Add(NormalizeBulkMetadataValue(tolerance, "0"));
            values.Add(NormalizeBulkMetadataValue(unit, "mm"));
        }

        private void AddUnusedMeasurementCsvValues(IList<string> values)
        {
            values.Add("-");
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
            if (!ContainsKeyword(historyRow.InspectedAt, HistoryTimeKeyword))
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
            HistoryTimeKeyword = string.Empty;
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
        private void RefreshCameraStatuses()
        {
            CameraChannels.Clear();

            try
            {
                IList<CameraChannelStatus> statuses = _cameraService.GetChannelStatuses();
                foreach (CameraChannelStatus status in statuses)
                {
                    CameraChannels.Add(new CameraChannelStatusViewModel(status));
                }

                CameraStatusMessage = "카메라 채널 " + CameraChannels.Count.ToString() + "개 상태를 갱신했습니다.";
            }
            catch (Exception ex)
            {
                CameraStatusMessage = "카메라 상태 조회 실패: " + ex.Message;
            }
        }

        private void ExecuteRefreshCameraStatus(object parameter)
        {
            RefreshCameraStatuses();
        }

        private void ExecuteReloadCameraConfiguration(object parameter)
        {
            try
            {
                _cameraService.ReloadConfiguration();
                RefreshCameraStatuses();
                CameraStatusMessage = "카메라 설정을 다시 읽었습니다.";
            }
            catch (Exception ex)
            {
                CameraStatusMessage = "카메라 설정 다시 읽기 실패: " + ex.Message;
            }
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
            RelayCommand command = RunInspectionCommand as RelayCommand;
            if (command != null)
            {
                command.RaiseCanExecuteChanged();
            }
        }
    }
}


