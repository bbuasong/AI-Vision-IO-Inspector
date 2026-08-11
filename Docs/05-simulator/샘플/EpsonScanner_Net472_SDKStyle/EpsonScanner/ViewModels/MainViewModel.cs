using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EpsonScanner.Helpers;
using EpsonScanner.Models;
using EpsonScanner.Services;

namespace EpsonScanner.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly FilePathService _filePathService;
        private readonly ScannerService _scannerService;
        private readonly ImageProcessingService _imageProcessingService;
        private readonly OcrService _ocrService;
        private readonly RelayCommand _startScanCommand;
        private readonly RelayCommand _sampleCommand;

        private string _detectedText;
        private string _statusText;
        private bool _isBusy;

        public ObservableCollection<string> DetectedList { get; private set; }
        public ObservableCollection<ScanLogItem> Logs { get; private set; }

        public ICommand StartScanCommand { get { return _startScanCommand; } }
        public ICommand SampleCommand { get { return _sampleCommand; } }

        public string DetectedText
        {
            get { return _detectedText; }
            set { _detectedText = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get { return _statusText; }
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                _startScanCommand.RaiseCanExecuteChanged();
                _sampleCommand.RaiseCanExecuteChanged();
            }
        }

        public MainViewModel()
        {
            _filePathService = new FilePathService();
            _scannerService = new ScannerService();
            _imageProcessingService = new ImageProcessingService();
            _ocrService = new OcrService();

            DetectedList = new ObservableCollection<string>();
            Logs = new ObservableCollection<ScanLogItem>();

            _startScanCommand = new RelayCommand(StartScan, CanStart);
            _sampleCommand = new RelayCommand(CreateSample, CanStart);
            StatusText = "Ready";
        }

        private bool CanStart()
        {
            return !IsBusy;
        }

        private void StartScan()
        {
            RunWorkflow(false);
        }

        private void CreateSample()
        {
            RunWorkflow(true);
        }

        private async void RunWorkflow(bool useSample)
        {
            IsBusy = true;
            StatusText = useSample ? "Creating sample..." : "Scanning...";

            ScanFilePaths paths = _filePathService.CreateNewPaths();

            try
            {
                await Task.Run(delegate
                {
                    if (useSample)
                        _scannerService.CreateSampleRawImage(paths.RawPath);
                    else
                        _scannerService.ScanToRawPng(paths.RawPath);

                    _imageProcessingService.SaveLabelImage(paths.RawPath, paths.LabelPath);
                    _imageProcessingService.SaveCropImage(paths.LabelPath, paths.CropPath);
                });

                OcrResult result = await Task.Run(delegate
                {
                    return _ocrService.DetectId(paths.CropPath);
                });

                string judgeId = string.IsNullOrWhiteSpace(result.JudgeId) ? "판별 실패" : result.JudgeId;
                DetectedText = judgeId;
                DetectedList.Insert(0, judgeId);

                Logs.Insert(0, new ScanLogItem
                {
                    Time = DateTime.Now,
                    JudgeId = judgeId,
                    RawImagePath = paths.RawPath,
                    LabelImagePath = paths.LabelPath,
                    CropImagePath = paths.CropPath,
                    Rotation = result.Rotation,
                    RawOcrText = result.RawText,
                    Message = useSample ? "Sample image workflow completed" : "Scan workflow completed"
                });

                StatusText = "Completed";
            }
            catch (Exception ex)
            {
                DetectedText = "ERROR";
                Logs.Insert(0, new ScanLogItem
                {
                    Time = DateTime.Now,
                    JudgeId = "ERROR",
                    RawImagePath = paths.RawPath,
                    LabelImagePath = paths.LabelPath,
                    CropImagePath = paths.CropPath,
                    Rotation = 0,
                    RawOcrText = string.Empty,
                    Message = ex.Message
                });

                StatusText = "Error";
                MessageBox.Show(ex.Message, "Epson Scanner", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
