using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using ScannerSample.Commands;
using ScannerSample.Models;
using ScannerSample.Services.Ocr.Common;
using ScannerSample.Services.Scanning;
using ScannerSample.Services.Workflow;

namespace ScannerSample.ViewModels
{
    /// <summary>
    /// Scanner OCR 샘플의 스캔 명령과 OCR 결과 표시 데이터를 관리합니다.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly ScanSettings _scanSettings;
        private readonly ScannerWorkflowService _scannerWorkflowService;
        private readonly RelayCommand _startScanCommand;
        private readonly RelayCommand _readImageFileCommand;
        private readonly RelayCommand _openImageFolderCommand;
        private readonly RelayCommand _clearResultsCommand;
        private string _statusMessage;
        private string _lastExtractedCode;
        private string _lastPaddleOcrCode;
        private string _lastWindowsOcrCode;
        private string _lastCSharpEpsonApiCode;
        private string _lastImageFilePath;
        private bool _isBusy;
        private int _sequence;

        public MainViewModel()
        {
            _scanSettings = ScanSettings.CreateDefault();
            _scannerWorkflowService = new ScannerWorkflowService(_scanSettings);
            _statusMessage = "Start Scan을 누르면 EPSON ES-C320W로 스캔 후 괄호 앞 품번을 OCR로 읽습니다.";
            _lastExtractedCode = string.Empty;
            _lastPaddleOcrCode = string.Empty;
            _lastWindowsOcrCode = string.Empty;
            _lastCSharpEpsonApiCode = string.Empty;
            _lastImageFilePath = string.Empty;
            ScanResults = new ObservableCollection<ScanTextItem>();
            _startScanCommand = new RelayCommand(ExecuteStartScan, CanStartScan);
            _readImageFileCommand = new RelayCommand(ExecuteReadImageFile, CanReadImageFile);
            _openImageFolderCommand = new RelayCommand(ExecuteOpenImageFolder, CanOpenImageFolder);
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

        public string LastPaddleOcrCode
        {
            get { return _lastPaddleOcrCode; }
            private set { SetProperty(ref _lastPaddleOcrCode, value); }
        }

        public string LastWindowsOcrCode
        {
            get { return _lastWindowsOcrCode; }
            private set { SetProperty(ref _lastWindowsOcrCode, value); }
        }

        public string LastCSharpEpsonApiCode
        {
            get { return _lastCSharpEpsonApiCode; }
            private set { SetProperty(ref _lastCSharpEpsonApiCode, value); }
        }

        public ICommand StartScanCommand
        {
            get { return _startScanCommand; }
        }

        public ICommand ReadImageFileCommand
        {
            get { return _readImageFileCommand; }
        }

        public ICommand OpenImageFolderCommand
        {
            get { return _openImageFolderCommand; }
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
            StatusMessage = "스캔 중입니다. EPSON ES-C320W에서 이미지를 취득하고 OCR을 수행합니다.";

            try
            {
                ScanReadResult result = await _scannerWorkflowService.ScanAndReadAsync();
                ApplyResult(result);
            }
            catch (Exception ex)
            {
                StatusMessage = "스캔 처리 중 예외가 발생했습니다. 프로그램 상태를 복구했으므로 다시 스캔할 수 있습니다. " + ex.Message;
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
            StatusMessage = "선택한 이미지 파일에서 OCR을 수행합니다.";

            try
            {
                ScanReadResult result = await _scannerWorkflowService.ReadImageFileAsync(dialog.FileName);
                ApplyResult(result);
            }
            catch (Exception ex)
            {
                StatusMessage = "이미지 OCR 처리 중 예외가 발생했습니다. " + ex.Message;
            }
            finally
            {
                _isBusy = false;
                RaiseCommandStates();
            }
        }

        private bool CanOpenImageFolder(object parameter)
        {
            return !_isBusy;
        }

        private void ExecuteOpenImageFolder(object parameter)
        {
            string scanFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scans");
            Directory.CreateDirectory(scanFolderPath);

            string arguments = scanFolderPath;
            if (!string.IsNullOrWhiteSpace(_lastImageFilePath) && File.Exists(_lastImageFilePath))
            {
                arguments = "/select,\"" + _lastImageFilePath + "\"";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "explorer.exe";
            startInfo.Arguments = arguments;
            startInfo.UseShellExecute = true;
            Process.Start(startInfo);
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
            LastPaddleOcrCode = string.Empty;
            LastWindowsOcrCode = string.Empty;
            LastCSharpEpsonApiCode = string.Empty;
            StatusMessage = "결과를 초기화했습니다.";
            _clearResultsCommand.RaiseCanExecuteChanged();
        }

        private void ApplyResult(ScanReadResult result)
        {
            if (result == null)
            {
                StatusMessage = "OCR 결과가 없습니다.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.ImageFilePath))
            {
                _lastImageFilePath = result.ImageFilePath;
            }

            if (result.EngineResults.Count > 0)
            {
                OcrEngineReadResult paddleOcrResult = result.GetEngineResult(OcrEngineSlots.PaddleOcr);
                OcrEngineReadResult windowsOcrResult = result.GetEngineResult(OcrEngineSlots.WindowsBuiltIn);
                OcrEngineReadResult csharpEpsonApiResult = result.GetEngineResult(OcrEngineSlots.CSharpEpsonApi);

                _sequence++;
                ScanResults.Add(new ScanTextItem(
                    _sequence,
                    result.CodeText,
                    DateTime.Now,
                    result.ImageFilePath,
                    result.RotationAngle,
                    result.OcrEngineName,
                    paddleOcrResult,
                    windowsOcrResult,
                    csharpEpsonApiResult));

                LastExtractedCode = result.CodeText;
                LastPaddleOcrCode = paddleOcrResult.DisplayText;
                LastWindowsOcrCode = windowsOcrResult.DisplayText;
                LastCSharpEpsonApiCode = csharpEpsonApiResult.DisplayText;

                StatusMessage = result.IsSuccess
                    ? "OCR 완료: " + result.CodeText + " / 선택 엔진: " + result.OcrEngineName
                    : result.Message;
                _clearResultsCommand.RaiseCanExecuteChanged();
                return;
            }

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                return;
            }

            _sequence++;
            ScanResults.Add(new ScanTextItem(_sequence, result.CodeText, DateTime.Now, result.ImageFilePath, result.RotationAngle, result.OcrEngineName, null, null, null));
            LastExtractedCode = result.CodeText;
            StatusMessage = "OCR 완료: " + result.CodeText + " / 선택 엔진: " + result.OcrEngineName;
            _clearResultsCommand.RaiseCanExecuteChanged();
        }

        private void RaiseCommandStates()
        {
            _startScanCommand.RaiseCanExecuteChanged();
            _readImageFileCommand.RaiseCanExecuteChanged();
            _openImageFolderCommand.RaiseCanExecuteChanged();
            _clearResultsCommand.RaiseCanExecuteChanged();
        }
    }
}
