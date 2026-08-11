using System;
using System.Globalization;
using System.Windows;
using AI.Vision.IOInspector.App.Services;

namespace AI.Vision.IOInspector.App
{
    /// <summary>
    /// 기준 이미지 변경 후 학습 실행 방식을 선택하는 팝업입니다.
    /// </summary>
    public partial class ImageTrainingPromptWindow : Window
    {
        public ImageTrainingPromptWindow(string title, string message, DateTime defaultScheduleTime)
        {
            InitializeComponent();
            Title = title;
            MessageTextBlock.Text = message;
            ScheduleDatePicker.SelectedDate = defaultScheduleTime.Date;
            ScheduleTimeTextBox.Text = defaultScheduleTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            Result = new ImageTrainingPromptResult();
            UpdateScheduleControls();
        }

        public ImageTrainingPromptResult Result { get; private set; }

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            UpdateScheduleControls();
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (ScheduleRadioButton.IsChecked == true)
            {
                DateTime scheduledAt;
                if (!TryBuildScheduleTime(out scheduledAt))
                {
                    MessageBox.Show(this, "예약시간을 올바르게 입력하세요.", "예약시간 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (scheduledAt <= DateTime.Now)
                {
                    MessageBox.Show(this, "예약시간은 현재시간 이후로 설정하세요.", "예약시간 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Result.IsAccepted = true;
                Result.StartNow = false;
                Result.ScheduledAt = scheduledAt;
                DialogResult = true;
                return;
            }

            Result.IsAccepted = true;
            Result.StartNow = true;
            Result.ScheduledAt = null;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            Result.IsAccepted = false;
            DialogResult = false;
        }

        private void UpdateScheduleControls()
        {
            bool isScheduleMode = ScheduleRadioButton != null && ScheduleRadioButton.IsChecked == true;
            if (ScheduleDatePicker != null)
            {
                ScheduleDatePicker.IsEnabled = isScheduleMode;
            }

            if (ScheduleTimeTextBox != null)
            {
                ScheduleTimeTextBox.IsEnabled = isScheduleMode;
            }
        }

        private bool TryBuildScheduleTime(out DateTime scheduledAt)
        {
            scheduledAt = DateTime.MinValue;
            if (!ScheduleDatePicker.SelectedDate.HasValue)
            {
                return false;
            }

            DateTime timeValue;
            if (!DateTime.TryParseExact(
                    ScheduleTimeTextBox.Text,
                    new[] { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out timeValue))
            {
                return false;
            }

            DateTime date = ScheduleDatePicker.SelectedDate.Value.Date;
            scheduledAt = date.AddHours(timeValue.Hour).AddMinutes(timeValue.Minute).AddSeconds(timeValue.Second);
            return true;
        }
    }
}
