using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 측정부 위치 지정 창의 좌표, 누적 표시, 선 색상을 관리합니다.
    /// </summary>
    public class MeasurementPositionViewModel : ObservableObject
    {
        private readonly MeasurementPointViewModel _currentPoint;
        private readonly double? _originalX1;
        private readonly double? _originalY1;
        private readonly double? _originalX2;
        private readonly double? _originalY2;
        private readonly string _originalLineColor;

        private bool _showAllPoints;
        private double? _x1;
        private double? _y1;
        private double? _x2;
        private double? _y2;
        private string _lineColor;
        private int _red;
        private int _green;
        private int _blue;

        public MeasurementPositionViewModel(
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints)
        {
            _currentPoint = currentPoint;
            AllPoints = allPoints ?? new List<MeasurementPointViewModel>();
            _originalX1 = currentPoint.X1;
            _originalY1 = currentPoint.Y1;
            _originalX2 = currentPoint.X2;
            _originalY2 = currentPoint.Y2;
            _originalLineColor = currentPoint.LineColor;

            _x1 = _originalX1;
            _y1 = _originalY1;
            _x2 = _originalX2;
            _y2 = _originalY2;
            _lineColor = string.IsNullOrWhiteSpace(_originalLineColor)
                ? MeasurementPointViewModel.GetDefaultColor(currentPoint.IndexNo)
                : _originalLineColor;
            ShowAllPoints = true;
            UpdateRgbFromColor();

            SelectColorCommand = new RelayCommand(ExecuteSelectColor);
            ApplyRgbCommand = new RelayCommand(ExecuteApplyRgb);
        }

        public IList<MeasurementPointViewModel> AllPoints { get; private set; }

        public ICommand SelectColorCommand { get; private set; }

        public ICommand ApplyRgbCommand { get; private set; }

        public bool ShowAllPoints
        {
            get { return _showAllPoints; }
            set { SetProperty(ref _showAllPoints, value); }
        }

        public int CurrentIndex
        {
            get { return _currentPoint.IndexNo; }
        }

        public string CurrentPointName
        {
            get { return _currentPoint.PointName; }
        }

        public double? X1
        {
            get { return _x1; }
            set { SetProperty(ref _x1, value); }
        }

        public double? Y1
        {
            get { return _y1; }
            set { SetProperty(ref _y1, value); }
        }

        public double? X2
        {
            get { return _x2; }
            set { SetProperty(ref _x2, value); }
        }

        public double? Y2
        {
            get { return _y2; }
            set { SetProperty(ref _y2, value); }
        }

        public string LineColor
        {
            get { return _lineColor; }
            set
            {
                if (SetProperty(ref _lineColor, NormalizeColor(value)))
                {
                    UpdateRgbFromColor();
                }
            }
        }

        public int Red
        {
            get { return _red; }
            set { SetProperty(ref _red, ClampColorValue(value)); }
        }

        public int Green
        {
            get { return _green; }
            set { SetProperty(ref _green, ClampColorValue(value)); }
        }

        public int Blue
        {
            get { return _blue; }
            set { SetProperty(ref _blue, ClampColorValue(value)); }
        }

        public bool HasStartPoint
        {
            get { return X1.HasValue && Y1.HasValue; }
        }

        public bool HasCompleteLine
        {
            get { return HasStartPoint && X2.HasValue && Y2.HasValue; }
        }

        public void SelectPoint(double x, double y)
        {
            if (!HasStartPoint || HasCompleteLine)
            {
                X1 = x;
                Y1 = y;
                X2 = null;
                Y2 = null;
                return;
            }

            X2 = x;
            Y2 = y;
        }

        public void CancelCurrentDrawing()
        {
            X1 = _originalX1;
            Y1 = _originalY1;
            X2 = _originalX2;
            Y2 = _originalY2;
            LineColor = _originalLineColor;
        }

        public bool ApplyToCurrentPoint()
        {
            if (!HasCompleteLine)
            {
                return false;
            }

            _currentPoint.SetCoordinates(X1.Value, Y1.Value, X2.Value, Y2.Value, LineColor);
            return true;
        }

        private void ExecuteSelectColor(object parameter)
        {
            string color = parameter as string;
            if (!string.IsNullOrWhiteSpace(color))
            {
                LineColor = color;
            }
        }

        private void ExecuteApplyRgb(object parameter)
        {
            LineColor = "#" + Red.ToString("X2", CultureInfo.InvariantCulture) +
                        Green.ToString("X2", CultureInfo.InvariantCulture) +
                        Blue.ToString("X2", CultureInfo.InvariantCulture);
        }

        private void UpdateRgbFromColor()
        {
            string color = NormalizeColor(_lineColor).TrimStart('#');
            int value;
            if (color.Length != 6 || !int.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
            {
                return;
            }

            _red = (value >> 16) & 0xFF;
            _green = (value >> 8) & 0xFF;
            _blue = value & 0xFF;
            OnPropertyChanged("Red");
            OnPropertyChanged("Green");
            OnPropertyChanged("Blue");
        }

        private string NormalizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return MeasurementPointViewModel.GetDefaultColor(CurrentIndex);
            }

            string normalized = color.Trim();
            if (!normalized.StartsWith("#", StringComparison.Ordinal))
            {
                normalized = "#" + normalized;
            }

            return normalized;
        }

        private int ClampColorValue(int value)
        {
            if (value < 0)
            {
                return 0;
            }

            if (value > 255)
            {
                return 255;
            }

            return value;
        }
    }
}
