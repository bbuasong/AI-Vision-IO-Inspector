using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AI.Vision.IOInspector.App.Services;
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
        private readonly PartCatalogService _partCatalogService;
        private readonly InspectionWorkflowService _inspectionWorkflowService;
        private readonly StatisticsService _statisticsService;
        private readonly IInspectionRepository _inspectionRepository;
        private readonly IReferenceImageFileService _referenceImageFileService;
        private readonly IFileDialogService _fileDialogService;

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
        private ImageEditViewModel _selectedRegistrationImage;
        private int _totalPartCount;
        private int _totalInspectionCount;
        private int _okCount;
        private int _ngCount;
        private int _errorCount;
        private string _averageInspectionTime;
        private bool _deleteRequested;

        public MainWindowViewModel(
            PartCatalogService partCatalogService,
            InspectionWorkflowService inspectionWorkflowService,
            StatisticsService statisticsService,
            IInspectionRepository inspectionRepository,
            IReferenceImageFileService referenceImageFileService,
            IFileDialogService fileDialogService)
        {
            _partCatalogService = partCatalogService;
            _inspectionWorkflowService = inspectionWorkflowService;
            _statisticsService = statisticsService;
            _inspectionRepository = inspectionRepository;
            _referenceImageFileService = referenceImageFileService;
            _fileDialogService = fileDialogService;

            Parts = new ObservableCollection<PartViewModel>();
            DbParts = new ObservableCollection<PartViewModel>();
            SearchSuggestions = new ObservableCollection<string>();
            ImageSlots = new ObservableCollection<ImageSlotViewModel>();
            InspectionMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailMeasurements = new ObservableCollection<MeasurementRowViewModel>();
            DbDetailImages = new ObservableCollection<ImageEditViewModel>();
            RegistrationMeasurementSets = new ObservableCollection<MeasurementSetViewModel>();
            RegistrationImages = new ObservableCollection<ImageEditViewModel>();
            InspectionHistory = new ObservableCollection<InspectionRowViewModel>();
            EventRows = new ObservableCollection<EventRowViewModel>();

            RunInspectionCommand = new RelayCommand(ExecuteRunInspection, CanRunInspection);
            SavePartCommand = new RelayCommand(ExecuteSavePart);
            NewPartCommand = new RelayCommand(ExecuteNewPart);
            DeletePartCommand = new RelayCommand(ExecuteDeletePart);
            SearchCommand = new RelayCommand(ExecuteSearch);
            AddMeasurementSetCommand = new RelayCommand(ExecuteAddMeasurementSet);
            RemoveMeasurementSetCommand = new RelayCommand(ExecuteRemoveMeasurementSet);
            AddReferenceImageCommand = new RelayCommand(ExecuteAddReferenceImage);
            RemoveReferenceImageCommand = new RelayCommand(ExecuteRemoveReferenceImage);
            RefreshStatisticsCommand = new RelayCommand(ExecuteRefreshStatistics);
            ShowInspectionTabCommand = new RelayCommand(ExecuteShowInspectionTab);
            ShowRegistrationTabCommand = new RelayCommand(ExecuteShowRegistrationTab);
            ShowDbTabCommand = new RelayCommand(ExecuteShowDbTab);
            ShowHistoryTabCommand = new RelayCommand(ExecuteShowHistoryTab);
            ShowStatisticsTabCommand = new RelayCommand(ExecuteShowStatisticsTab);

            StatusText = "대기";
            ResultText = "검사 전";
            InitializeImageSlots();
            InitializeEmptyRegistrationSets();
            LoadParts();
            RefreshHistory();
            RefreshStatistics();
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

        public ObservableCollection<InspectionRowViewModel> InspectionHistory { get; private set; }

        public ObservableCollection<EventRowViewModel> EventRows { get; private set; }

        public ICommand RunInspectionCommand { get; private set; }

        public ICommand SavePartCommand { get; private set; }

        public ICommand NewPartCommand { get; private set; }

        public ICommand DeletePartCommand { get; private set; }

        public ICommand SearchCommand { get; private set; }

        public ICommand AddMeasurementSetCommand { get; private set; }

        public ICommand RemoveMeasurementSetCommand { get; private set; }

        public ICommand AddReferenceImageCommand { get; private set; }

        public ICommand RemoveReferenceImageCommand { get; private set; }

        public ICommand RefreshStatisticsCommand { get; private set; }

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
                    ApplySearchFilters();
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
                    ApplySearchFilters();
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
                    ApplySearchFilters();
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
                    ApplySearchFilters();
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

        public ImageEditViewModel SelectedRegistrationImage
        {
            get { return _selectedRegistrationImage; }
            set { SetProperty(ref _selectedRegistrationImage, value); }
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

        private void LoadParts()
        {
            Parts.Clear();
            foreach (Part part in _partCatalogService.GetParts())
            {
                Parts.Add(new PartViewModel(part));
            }

            ApplySearchFilters();
            if (Parts.Count > 0 && SelectedPart == null)
            {
                SelectedPart = Parts[0];
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
            InspectionMeasurements.Clear();
            InitializeImageSlots();
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
            slot.StatusText = "기준 이미지 대기";
            slot.FilePath = "-";
            ImageSlots.Add(slot);
        }

        private void LoadReferenceImages(Part part)
        {
            InitializeImageSlots();
            int index = 0;
            foreach (PartImage image in part.Images)
            {
                if (index >= ImageSlots.Count)
                {
                    break;
                }

                ImageSlots[index].StatusText = "기준 이미지";
                ImageSlots[index].FilePath = image.FilePath;
                index++;
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

            int order = 1;
            foreach (PartImage image in part.Images)
            {
                DbDetailImages.Add(new ImageEditViewModel(image, order));
                order++;
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

            foreach (MeasurementRegion region in part.MeasurementRegions)
            {
                int setIndex = ResolveMeasurementSetIndex(region.Name);
                if (!sets.ContainsKey(setIndex))
                {
                    MeasurementSetViewModel set = new MeasurementSetViewModel();
                    set.SetName = "측정부 " + setIndex;
                    sets[setIndex] = set;
                }

                ApplyRegionToSet(sets[setIndex], region);
            }

            foreach (int key in sets.Keys)
            {
                RegistrationMeasurementSets.Add(sets[key]);
            }

            if (RegistrationMeasurementSets.Count == 0)
            {
                AddDefaultMeasurementSet();
            }
        }

        private int ResolveMeasurementSetIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return 1;
            }

            string[] parts = name.Split(' ');
            if (parts.Length >= 2)
            {
                int parsed;
                if (int.TryParse(parts[1], out parsed))
                {
                    return parsed;
                }
            }

            return 1;
        }

        private void ApplyRegionToSet(MeasurementSetViewModel set, MeasurementRegion region)
        {
            string value = region.NominalValue.ToString("0.###");
            if (region.Name.Contains("길이"))
            {
                set.LengthValue = value;
            }
            else if (region.Name.Contains("너비"))
            {
                set.WidthValue = value;
            }
            else if (region.Name.Contains("높이"))
            {
                set.HeightValue = value;
            }
            else if (region.Name.Contains("두께"))
            {
                set.ThicknessValue = value;
            }
        }

        private void LoadRegistrationImages(Part part)
        {
            RegistrationImages.Clear();
            int order = 1;
            foreach (PartImage image in part.Images)
            {
                RegistrationImages.Add(new ImageEditViewModel(image, order));
                order++;
            }
        }

        private void InitializeEmptyRegistrationSets()
        {
            RegistrationMeasurementSets.Clear();
            AddDefaultMeasurementSet();
        }

        private void AddDefaultMeasurementSet()
        {
            MeasurementSetViewModel set = new MeasurementSetViewModel();
            set.SetName = "측정부 " + (RegistrationMeasurementSets.Count + 1);
            RegistrationMeasurementSets.Add(set);
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

            LoadCapturedImages(inspection);
            LoadInspectionMeasurements(inspection);
            LoadEvents(inspection);
            RefreshHistory();
            RefreshStatistics();
        }

        private void LoadCapturedImages(Inspection inspection)
        {
            InitializeImageSlots();
            int index = 0;
            foreach (CapturedImage image in inspection.Images)
            {
                if (index >= ImageSlots.Count)
                {
                    break;
                }

                ImageSlots[index].StatusText = "촬영 완료";
                ImageSlots[index].FilePath = image.FilePath;
                index++;
            }
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
                RegistrationMessage = _partCatalogService.DeletePart(RegistrationPartNo);
                _deleteRequested = false;
                ExecuteNewPart(null);
                LoadParts();
                RefreshStatistics();
                return;
            }

            Part part = BuildRegistrationPart();
            RegistrationMessage = _partCatalogService.SavePart(part);
            LoadParts();
            SelectedPart = FindPartViewModel(part.PartNo);
            RefreshStatistics();
        }

        private void ExecuteDeletePart(object parameter)
        {
            if (string.IsNullOrWhiteSpace(RegistrationPartNo))
            {
                RegistrationMessage = "삭제할 Part No.가 없습니다.";
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

            int imageOrder = 1;
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                PartImage image = imageViewModel.Image;
                image.PartNo = part.PartNo;
                part.Images.Add(image);
                imageOrder++;
            }

            int regionId = 1;
            int setIndex = 1;
            foreach (MeasurementSetViewModel set in RegistrationMeasurementSets)
            {
                if (set.HasAnyValue())
                {
                    set.AddRegionsToPart(part, setIndex, ref regionId);
                }

                setIndex++;
            }

            return part;
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
            InitializeEmptyRegistrationSets();
            _deleteRequested = false;
            RegistrationMessage = "신규 부품 정보를 입력하세요.";
            SelectedTabIndex = 2;
        }

        private void ExecuteSearch(object parameter)
        {
            ApplySearchFilters();
        }

        private void ApplySearchFilters()
        {
            DbParts.Clear();
            IList<Part> allParts = _partCatalogService.GetParts();
            foreach (Part part in allParts)
            {
                if (IsPartMatched(part))
                {
                    DbParts.Add(new PartViewModel(part));
                }
            }

            BuildSearchSuggestions(allParts);
        }

        private bool IsPartMatched(Part part)
        {
            if (!ContainsKeyword(part.PartNo, SearchPartNo))
            {
                return false;
            }

            if (!ContainsKeyword(part.PartName, SearchPartName))
            {
                return false;
            }

            if (!ContainsKeyword(part.CategoryCode, SearchCategoryCode))
            {
                return false;
            }

            if (!ContainsKeyword(part.CategoryDescription, SearchCategoryDescription))
            {
                return false;
            }

            return true;
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

        private void BuildSearchSuggestions(IList<Part> allParts)
        {
            SearchSuggestions.Clear();
            AddSuggestions(allParts, SearchPartNo, "PartNo");
            AddSuggestions(allParts, SearchPartName, "PartName");
            AddSuggestions(allParts, SearchCategoryCode, "분류코드");
            AddSuggestions(allParts, SearchCategoryDescription, "분류설명");
        }

        private void AddSuggestions(IList<Part> allParts, string keyword, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            foreach (Part part in allParts)
            {
                string value = GetFieldValue(part, fieldName);
                if (ContainsKeyword(value, keyword) && !SuggestionExists(value))
                {
                    SearchSuggestions.Add(value);
                }
            }
        }

        private string GetFieldValue(Part part, string fieldName)
        {
            if (fieldName == "PartNo")
            {
                return part.PartNo;
            }

            if (fieldName == "PartName")
            {
                return part.PartName;
            }

            if (fieldName == "분류코드")
            {
                return part.CategoryCode;
            }

            return part.CategoryDescription;
        }

        private bool SuggestionExists(string value)
        {
            foreach (string suggestion in SearchSuggestions)
            {
                if (string.Equals(suggestion, value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
                set.SetName = "측정부 " + index;
                index++;
            }
        }

        private void ExecuteAddReferenceImage(object parameter)
        {
            if (string.IsNullOrWhiteSpace(RegistrationPartNo) || string.IsNullOrWhiteSpace(RegistrationPartName))
            {
                RegistrationMessage = "이미지를 추가하기 전에 Part No.와 Part Name을 입력하세요.";
                return;
            }

            string sourceFilePath = _fileDialogService.SelectImageFile();
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                RegistrationMessage = "이미지 추가가 취소되었습니다.";
                return;
            }

            Part tempPart = new Part();
            tempPart.PartNo = RegistrationPartNo;
            tempPart.PartName = RegistrationPartName;
            int order = RegistrationImages.Count + 1;
            PartImage image = _referenceImageFileService.AddReferenceImage(tempPart, sourceFilePath, order);
            RegistrationImages.Add(new ImageEditViewModel(image, order));
            RegistrationMessage = "기준 이미지를 추가했습니다.";
        }

        private void ExecuteRemoveReferenceImage(object parameter)
        {
            if (SelectedRegistrationImage == null)
            {
                RegistrationMessage = "삭제할 기준 이미지를 선택하세요.";
                return;
            }

            _referenceImageFileService.DeleteReferenceImage(SelectedRegistrationImage.Image);
            RegistrationImages.Remove(SelectedRegistrationImage);
            ReorderRegistrationImages();
            RegistrationMessage = "기준 이미지를 삭제했습니다.";
        }

        private void ReorderRegistrationImages()
        {
            IList<PartImage> images = new List<PartImage>();
            foreach (ImageEditViewModel imageViewModel in RegistrationImages)
            {
                images.Add(imageViewModel.Image);
            }

            RegistrationImages.Clear();
            int order = 1;
            foreach (PartImage image in images)
            {
                RegistrationImages.Add(new ImageEditViewModel(image, order));
                order++;
            }
        }

        private void RefreshHistory()
        {
            InspectionHistory.Clear();
            foreach (Inspection inspection in _inspectionRepository.GetAll())
            {
                InspectionHistory.Add(new InspectionRowViewModel(inspection));
            }
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


