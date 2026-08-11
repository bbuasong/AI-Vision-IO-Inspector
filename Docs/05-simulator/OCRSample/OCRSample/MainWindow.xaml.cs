using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OCRSample.Models;
using OCRSample.Services;

namespace OCRSample
{
    public partial class MainWindow : Window
    {
        private readonly EpsonOcrWorkerClient _ocrWorker = new EpsonOcrWorkerClient();
        private string _lastImagePath = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshScannersAsync();
        }

        private async void RefreshScannersButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshScannersAsync();
        }

        private async Task RefreshScannersAsync()
        {
            try
            {
                SetBusy(true, "USB/WIA 스캐너를 찾고 있습니다...");

                List<ScannerDevice> scanners = await Task.Factory.StartNew(
                    delegate { return DirectWiaScanner.ListScanners(); });

                if (scanners.Count == 0)
                {
                    DeviceComboBox.ItemsSource = new List<ScannerDevice>
                    {
                        new ScannerDevice(null, "연결된 WIA 스캐너 없음")
                    };
                    DeviceComboBox.SelectedIndex = 0;
                    SetResultMessage("연결된 WIA 스캐너를 찾지 못했습니다.", Brushes.DarkOrange);
                    ActivityTextBlock.Text = "스캐너 없음";
                    return;
                }

                ScannerDevice previous = DeviceComboBox.SelectedItem as ScannerDevice;
                string previousId = previous == null ? null : previous.Id;
                scanners.Insert(0, new ScannerDevice(null, "자동 선택 (첫 번째 스캐너)"));
                DeviceComboBox.ItemsSource = scanners;

                int selectedIndex = 0;
                for (int i = 0; i < scanners.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(previousId) && scanners[i].Id == previousId)
                    {
                        selectedIndex = i;
                        break;
                    }
                }
                DeviceComboBox.SelectedIndex = selectedIndex;

                SetResultMessage((scanners.Count - 1) + "개의 USB/WIA 스캐너를 찾았습니다.", Brushes.ForestGreen);
                ActivityTextBlock.Text = "스캔 준비 완료";
            }
            catch (Exception ex)
            {
                SetResultMessage("스캐너 조회 실패: " + ex.Message, Brushes.Firebrick);
                ActivityTextBlock.Text = "스캐너 조회 실패";
            }
            finally
            {
                SetBusy(false, ActivityTextBlock.Text);
            }
        }

        private async void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ScannerDevice device = DeviceComboBox.SelectedItem as ScannerDevice;
                string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scans");

                SetBusy(true, "USB에서 라벨을 스캔하고 있습니다...");
                ClearCurrentScanDisplay();
                DirectWiaScanResult scan = await DirectWiaScanner.TryScanAsync(
                    outputDirectory,
                    device == null ? null : device.Id,
                    ParseDpi(SelectedComboBoxValue(DpiComboBox)),
                    SelectedComboBoxValue(ModeComboBox),
                    SelectedComboBoxValue(SourceComboBox),
                    "png");

                if (!scan.IsSuccess)
                {
                    SetResultMessage(
                        scan.ErrorMessage,
                        scan.IsPaperEmpty ? Brushes.DarkOrange : Brushes.Firebrick);
                    ActivityTextBlock.Text = scan.IsPaperEmpty ? "\uC6A9\uC9C0 \uC5C6\uC74C" : "\uC2A4\uCEA4 \uC2E4\uD328";
                    return;
                }

                string imagePath = scan.ImagePath;
                _lastImagePath = imagePath;
                ImagePathTextBox.Text = imagePath;
                LoadPreviewImage(imagePath);
                SetResultMessage("스캔 완료. 로컬 Epson OCR을 실행하고 있습니다...", Brushes.DarkSlateBlue);
                ActivityTextBlock.Text = "Epson OCR 실행 중...";

                EpsonOcrResult result = await _ocrWorker.RecognizeAsync(
                    OcrWorkerPathTextBox.Text.Trim(),
                    imagePath,
                    LanguageTextBox.Text.Trim());

                ShowOcrResult(result);
            }
            catch (Exception ex)
            {
                SetResultMessage("스캔/OCR 처리 실패: " + ex.Message, Brushes.Firebrick);
                ActivityTextBlock.Text = "처리 실패";
            }
            finally
            {
                SetBusy(false, ActivityTextBlock.Text);
            }
        }

        private void ShowOcrResult(EpsonOcrResult result)
        {
            PartNoTextBox.Text = result.PartNo;
            PartNoSubTextBox.Text = result.PartNoSub;
            ConfidenceTextBox.Text = result.Confidence.ToString("P0");
            RawTextTextBox.Text = result.RawText;

            string quality = string.IsNullOrWhiteSpace(result.QualityReason)
                ? "OCR 엔진: " + result.Engine
                : "OCR 엔진: " + result.Engine + " · " + result.QualityReason;
            QualityTextBlock.Text = quality;

            if (result.NeedsConfirmation)
            {
                string message = string.IsNullOrWhiteSpace(result.PartNo)
                    ? "OCR 원문은 인식됐지만 부품번호 형식을 찾지 못했습니다. 원문을 확인하거나 부품번호를 직접 입력하세요."
                    : "OCR 결과를 확인하세요. 부품번호 품질 또는 신뢰도가 기준에 미달했습니다.";
                SetResultMessage(message, Brushes.DarkOrange);
                ActivityTextBlock.Text = "OCR 완료 · 작업자 확인 필요";
            }
            else
            {
                SetResultMessage("스캔과 OCR이 완료되었습니다.", Brushes.ForestGreen);
                ActivityTextBlock.Text = "스캔 및 OCR 완료";
            }
        }

        private void OpenImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastImagePath))
            {
                SetResultMessage("열 스캔 이미지가 없습니다.", Brushes.DarkOrange);
                return;
            }

            try
            {
                if (File.Exists(_lastImagePath))
                {
                    Process.Start(new ProcessStartInfo(_lastImagePath) { UseShellExecute = true });
                }
                else
                {
                    string directory = Path.GetDirectoryName(_lastImagePath);
                    if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    {
                        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
                    }
                    else
                    {
                        SetResultMessage("이미지 경로를 찾을 수 없습니다: " + _lastImagePath, Brushes.DarkOrange);
                    }
                }
            }
            catch (Exception ex)
            {
                SetResultMessage("이미지를 열 수 없습니다: " + ex.Message, Brushes.Firebrick);
            }
        }

        private void LoadPreviewImage(string imagePath)
        {
            ScanImage.Source = null;
            ImagePlaceholderTextBlock.Visibility = Visibility.Visible;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(imagePath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();

                ScanImage.Source = image;
                ImagePlaceholderTextBlock.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                SetResultMessage("스캔 이미지 미리보기 실패: " + ex.Message, Brushes.DarkOrange);
            }
        }

        private void ClearCurrentScanDisplay()
        {
            _lastImagePath = string.Empty;
            ImagePathTextBox.Text = string.Empty;
            ScanImage.Source = null;
            ImagePlaceholderTextBlock.Visibility = Visibility.Visible;
            PartNoTextBox.Text = string.Empty;
            PartNoSubTextBox.Text = string.Empty;
            ConfidenceTextBox.Text = string.Empty;
            RawTextTextBox.Text = string.Empty;
            QualityTextBlock.Text = "\u004F\u0043\u0052 \uACB0\uACFC \uB300\uAE30";
        }

        private void SetBusy(bool busy, string activity)
        {
            ScanButton.IsEnabled = !busy;
            RefreshScannersButton.IsEnabled = !busy;
            OcrWorkerPathTextBox.IsEnabled = !busy;
            ActivityTextBlock.Text = activity;
        }

        private void SetResultMessage(string message, Brush color)
        {
            ResultMessageTextBlock.Text = message;
            ResultMessageTextBlock.Foreground = color;
        }

        private static string SelectedComboBoxValue(ComboBox comboBox)
        {
            ComboBoxItem item = comboBox.SelectedItem as ComboBoxItem;
            if (item == null)
            {
                return string.Empty;
            }

            string value = item.Tag as string;
            return string.IsNullOrWhiteSpace(value) ? item.Content.ToString() : value;
        }

        private static int ParseDpi(string value)
        {
            int dpi;
            return int.TryParse(value, out dpi) ? dpi : 300;
        }
    }
}
