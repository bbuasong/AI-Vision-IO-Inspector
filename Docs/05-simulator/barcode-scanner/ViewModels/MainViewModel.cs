using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using BarcodeScannerSample.Commands;
using BarcodeScannerSample.Models;

namespace BarcodeScannerSample.ViewModels
{
    /// <summary>
    /// 바코드 입력값을 ListBox에 누적하고 초기화 명령을 처리합니다.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly RelayCommand _startBarcodeReadingCommand;
        private readonly RelayCommand _addBarcodeCommand;
        private readonly RelayCommand _clearBarcodesCommand;
        private string _currentBarcode;
        private string _statusMessage;
        private bool _isReadingActive;
        private int _sequence;

        public MainViewModel()
        {
            BarcodeItems = new ObservableCollection<BarcodeItem>();
            _currentBarcode = string.Empty;
            _statusMessage = "Start Reading 버튼을 누른 뒤 바코드를 스캔하거나 값을 입력하고 Enter를 누르세요.";
            _startBarcodeReadingCommand = new RelayCommand(ExecuteStartBarcodeReading, CanStartBarcodeReading);
            _addBarcodeCommand = new RelayCommand(ExecuteAddBarcode, CanAddBarcode);
            _clearBarcodesCommand = new RelayCommand(ExecuteClearBarcodes, CanClearBarcodes);
        }

        public event EventHandler ReadingStarted;

        public ObservableCollection<BarcodeItem> BarcodeItems { get; private set; }

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
            return !IsReadingActive;
        }

        private void ExecuteStartBarcodeReading(object parameter)
        {
            IsReadingActive = true;
            CurrentBarcode = string.Empty;
            StatusMessage = "바코드 리딩을 시작했습니다. 스캔 후 Enter 입력이 들어오면 ListBox에 추가됩니다.";
            OnReadingStarted();
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
            BarcodeItems.Add(new BarcodeItem(_sequence, barcodeText, DateTime.Now));
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
            StatusMessage = "ListBox를 초기화했습니다.";
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
