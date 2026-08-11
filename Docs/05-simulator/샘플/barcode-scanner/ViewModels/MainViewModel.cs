using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using BarcodeScannerSample.Commands;
using BarcodeScannerSample.Models;
using BarcodeScannerSample.Services;

namespace BarcodeScannerSample.ViewModels
{
    /// <summary>
    /// 바코드 입력값을 ListBox에 누적하고 초기화 명령을 처리합니다.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly IBarcodeScanService _barcodeScanService;
        private readonly ScanSettings _scanSettings;
        private readonly RelayCommand _startBarcodeReadingCommand;
        private readonly RelayCommand _decodeImageFileCommand;
        private readonly RelayCommand _addBarcodeCommand;
        private readonly RelayCommand _clearBarcodesCommand;
        private string _currentBarcode;
        private string _statusMessage;
        private string _lastImageFilePath;
        private bool _isReadingActive;
        private bool _isBusy;
        private int _sequence;

        public MainViewModel()
            : this(ScanSettings.CreateDefault())
        {
        }

        public MainViewModel(ScanSettings scanSettings)
            : this(new WiaBarcodeScanService(scanSettings), scanSettings)
        {
        }

        public MainViewModel(IBarcodeScanService barcodeScanService)
            : this(barcodeScanService, ScanSettings.CreateDefault())
        {
        }

        public MainViewModel(IBarcodeScanService barcodeScanService, ScanSettings scanSettings)
        {
            if (barcodeScanService == null)
            {
                throw new ArgumentNullException("barcodeScanService");
            }

            if (scanSettings == null)
            {
                throw new ArgumentNullException("scanSettings");
            }

            _barcodeScanService = barcodeScanService;
            _scanSettings = scanSettings;
            BarcodeItems = new ObservableCollection<BarcodeItem>();
            _currentBarcode = string.Empty;
            _lastImageFilePath = string.Empty;
            _statusMessage = "Start Reading 버튼을 누르면 스캔 이미지 저장 후 ZXing으로 바코드를 디코딩합니다.";
            _startBarcodeReadingCommand = new RelayCommand(ExecuteStartBarcodeReading, CanStartBarcodeReading);
            _decodeImageFileCommand = new RelayCommand(ExecuteDecodeImageFile, CanDecodeImageFile);
            _addBarcodeCommand = new RelayCommand(ExecuteAddBarcode, CanAddBarcode);
            _clearBarcodesCommand = new RelayCommand(ExecuteClearBarcodes, CanClearBarcodes);
        }

        public event EventHandler ReadingStarted;

        public ObservableCollection<BarcodeItem> BarcodeItems { get; private set; }

        public string ScanSettingsSummary
        {
            get { return _scanSettings.Summary; }
        }

        public string CurrentBarcode
        {
            get { return _currentBarcode; }
            set
            {
                if (SetProperty(ref _currentBarcode, value))
                {
                    _addBarcodeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            private set { SetProperty(ref _statusMessage, value); }
        }

        public string LastImageFilePath
        {
            get { return _lastImageFilePath; }
            private set { SetProperty(ref _lastImageFilePath, value); }
        }

        public bool IsReadingActive
        {
            get { return _isReadingActive; }
            private set
            {
                if (SetProperty(ref _isReadingActive, value))
                {
                    _startBarcodeReadingCommand.RaiseCanExecuteChanged();
                    _addBarcodeCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand StartBarcodeReadingCommand
        {
            get { return _startBarcodeReadingCommand; }
        }

        public ICommand DecodeImageFileCommand
        {
            get { return _decodeImageFileCommand; }
        }

        public ICommand AddBarcodeCommand
        {
            get { return _addBarcodeCommand; }
        }

        public ICommand ClearBarcodesCommand
        {
            get { return _clearBarcodesCommand; }
        }

        private bool CanStartBarcodeReading(object parameter)
        {
            return !_isBusy;
        }

        private void ExecuteStartBarcodeReading(object parameter)
        {
            _isBusy = true;
            RaiseCommandStates();

            IsReadingActive = true;
            CurrentBarcode = string.Empty;
            StatusMessage = "스캐너에서 이미지를 취득하는 중입니다.";

            try
            {
                BarcodeDecodeResult result = _barcodeScanService.ScanAndDecode();
                ApplyDecodeResult(result);
            }
            finally
            {
                _isBusy = false;
                RaiseCommandStates();
                OnReadingStarted();
            }
        }

        private bool CanDecodeImageFile(object parameter)
        {
            return !_isBusy;
        }

        private void ExecuteDecodeImageFile(object parameter)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Decode Barcode Image";
            dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*";
            dialog.Multiselect = false;

            bool? dialogResult = dialog.ShowDialog();
            if (dialogResult != true)
            {
                return;
            }

            _isBusy = true;
            RaiseCommandStates();

            try
            {
                BarcodeDecodeResult result = _barcodeScanService.DecodeImageFile(dialog.FileName);
                ApplyDecodeResult(result);
            }
            finally
            {
                _isBusy = false;
                RaiseCommandStates();
            }
        }

        private bool CanAddBarcode(object parameter)
        {
            return IsReadingActive && !string.IsNullOrWhiteSpace(CurrentBarcode);
        }

        private void ExecuteAddBarcode(object parameter)
        {
            string barcodeText = CurrentBarcode.Trim();
            if (string.IsNullOrWhiteSpace(barcodeText))
            {
                return;
            }

            _sequence++;
            BarcodeItems.Add(new BarcodeItem(_sequence, barcodeText, DateTime.Now, string.Empty));
            CurrentBarcode = string.Empty;
            StatusMessage = "마지막 스캔: " + barcodeText;
            _clearBarcodesCommand.RaiseCanExecuteChanged();
        }

        private bool CanClearBarcodes(object parameter)
        {
            return BarcodeItems.Count > 0;
        }

        private void ExecuteClearBarcodes(object parameter)
        {
            BarcodeItems.Clear();
            _sequence = 0;
            LastImageFilePath = string.Empty;
            StatusMessage = "ListBox를 초기화했습니다.";
            _clearBarcodesCommand.RaiseCanExecuteChanged();
        }

        private void ApplyDecodeResult(BarcodeDecodeResult result)
        {
            if (result == null)
            {
                StatusMessage = "디코딩 결과가 없습니다.";
                return;
            }

            LastImageFilePath = result.ImageFilePath;

            if (!result.IsSuccess)
            {
                StatusMessage = result.Message;
                return;
            }

            _sequence++;
            BarcodeItems.Add(new BarcodeItem(_sequence, result.BarcodeText, DateTime.Now, result.ImageFilePath));
            CurrentBarcode = result.BarcodeText;
            StatusMessage = "디코딩 완료: " + result.BarcodeText;
            _clearBarcodesCommand.RaiseCanExecuteChanged();
        }

        private void RaiseCommandStates()
        {
            _startBarcodeReadingCommand.RaiseCanExecuteChanged();
            _decodeImageFileCommand.RaiseCanExecuteChanged();
            _addBarcodeCommand.RaiseCanExecuteChanged();
            _clearBarcodesCommand.RaiseCanExecuteChanged();
        }

        private void OnReadingStarted()
        {
            EventHandler handler = ReadingStarted;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
