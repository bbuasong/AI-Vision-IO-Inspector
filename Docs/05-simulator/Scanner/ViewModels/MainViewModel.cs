using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using ScannerSample.Commands;
using ScannerSample.Models;
using ScannerSample.Services;

namespace ScannerSample.ViewModels
{
    /// <summary>
    /// Scanner OCR 샘플의 스캔 명령과 ListBox 표시 데이터를 관리합니다.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly ScanSettings _scanSettings;
        private readonly ScannerWorkflowService _scannerWorkflowService;
        private readonly RelayCommand _startScanCommand;
        private readonly RelayCommand _readImageFileCommand;
        private readonly RelayCommand _clearResultsCommand;
        private string _statusMessage;
        private string _lastExtractedCode;
        private bool _isBusy;
        private int _sequence;

        public MainViewModel()
        {
            _scanSettings = ScanSettings.CreateDefault();
            _scannerWorkflowService = new ScannerWorkflowService(_scanSettings);
            _statusMessage = "Start Scan을 누르면 EPSON ES-C320W로 스캔 후 OCR로 검수 라벨 상단 코드를 읽습니다.";
            _lastExtractedCode = string.Empty;
            ScanResults = new ObservableCollection<ScanTextItem>();
            _startScanCommand = new RelayCommand(ExecuteStartScan, CanStartScan);
            _readImageFileCommand = new RelayCommand(ExecuteReadImageFile, CanReadImageFile);
            _clearResultsCommand = new RelayCommand(ExecuteClearResults, CanClearResults);
        }

        public ObservableCollection<ScanTextItem> ScanResults { get; private set; }

        public string ScanSettingsSummary
        {
            get { return _scanSettings.Summary; }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        public string LastExtractedCode
        {
            get { return _lastExtractedCode; }
            private set { SetProperty(ref _lastExtractedCode, value); }
        }

        public ICommand StartScanCommand
        {
            get { return _startScanCommand; }
        }

        public ICommand ReadImageFileCommand
        {
            get { return _readImageFileCommand; }
        }

        public ICommand ClearResultsCommand
        {
            get { return _clearResultsCommand; }
        }

        private bool CanStartScan(object parameter)
        {
            return !_isBusy;
        }

        private async void ExecuteStartScan(object parameter)
        {
            _isBusy = true;
            RaiseCommandStates();
            StatusMessage = "스캔 중입니다. EPSON ES-C320W에서 이미지를 취득하고 OCR을 실행합니다.";

            try
            {
                ScanReadResult result = await _scannerWorkflowService.ScanAndReadAsync();
                ApplyResult(result);
            }
            finally
            {
                _isBusy = false;
                RaiseCommandStates();
            }
        }

        private bool CanReadImageFile(object parameter)
        {
            return !_isBusy;
        }

        private async void ExecuteReadImageFile(object parameter)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Read Scanned Label Image";
            dialog.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All Files|*.*";
            dialog.Multiselect = false;

            bool? dialogResult = dialog.ShowDialog();
            if (dialogResult != true)
            {
                return;
            }

            _isBusy = true;
            RaiseCommandStates();
            StatusMessage = "이미지 파일에서 OCR을 실행합니다.";

            try
            {
                ScanReadResult result = await _scannerWorkflowService.ReadImageFileAsync(dialog.FileName);
                ApplyResult(result);
            }
            finally
            {
                _isBusy = false;
                RaiseCommandStates();
            }
        }

        private bool CanClearResults(object parameter)
        {
            return ScanResults.Count > 0;
        }

        private void ExecuteClearResults(object parameter)
        {
            ScanResults.Clear();
            _sequence = 0;
            LastExtractedCode = string.Empty;
            StatusMessage = "ListBox를 초기화했습니다.";
            _clearResultsCommand.RaiseCanExecuteChanged();
        }

        private void ApplyResult(ScanReadResult result)
        {
            if (result == null)
            {
                StatusMessage = "OCR 결과가 없습니다.";
                return;
            }

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                return;
            }

            _sequence++;
            ScanResults.Add(new ScanTextItem(_sequence, result.CodeText, DateTime.Now, result.ImageFilePath, result.RotationAngle));
            LastExtractedCode = result.CodeText;
            StatusMessage = "OCR 완료: " + result.CodeText;
            _clearResultsCommand.RaiseCanExecuteChanged();
        }

        private void RaiseCommandStates()
        {
            _startScanCommand.RaiseCanExecuteChanged();
            _readImageFileCommand.RaiseCanExecuteChanged();
            _clearResultsCommand.RaiseCanExecuteChanged();
        }
    }
}
