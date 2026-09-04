using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 측정부 위치 지정 창의 좌표, 누적 표시, 선 색상을 관리합니다.
    ///
    /// <para>
    /// 창을 닫지 않고 다른 측정부로 옮겨 다닐 수 있습니다. 옮길 때마다 좌표와 색은 그
    /// 측정부의 것으로 갈아 끼우고, 카메라가 다른 측정부로 옮기면 배경 사진도 그 카메라의
    /// 기준 이미지로 바꿉니다. 배경과 좌표계가 어긋난 채로 선이 그려지면 안 됩니다.
    /// </para>
    ///
    /// <para>
    /// 옮길 때 그리던 선이 완성되어 있으면 그 측정부에 남기고 넘어갑니다. 창에서 "취소"를
    /// 누르면 옮겨 다니며 고친 것까지 모두 창을 열기 전 상태로 되돌립니다.
    /// </para>
    /// </summary>
    public class MeasurementPositionViewModel : ObservableObject
    {
        private readonly IDictionary<ImageViewType, string> _imagePathByViewType;
        private readonly IList<MeasurementPointViewModel> _editablePoints;
        private readonly IList<MeasurementPointGroupViewModel> _pointGroups;
        private readonly IDictionary<MeasurementPointViewModel, PointSnapshot> _snapshots;

        private MeasurementPointViewModel _currentPoint;
        private int _currentPointIndex;
        private bool _appliedAnyPoint;

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
            IDictionary<ImageViewType, string> imagePathByViewType,
            MeasurementPointViewModel currentPoint,
            IList<MeasurementPointViewModel> allPoints)
        {
            _imagePathByViewType = imagePathByViewType ?? new Dictionary<ImageViewType, string>();
            AllPoints = allPoints ?? new List<MeasurementPointViewModel>();

            // 창을 열기 전 상태를 모두 적어 둡니다. "취소"로 한 번에 되돌리기 위해서입니다.
            _snapshots = new Dictionary<MeasurementPointViewModel, PointSnapshot>();
            foreach (MeasurementPointViewModel point in AllPoints)
            {
                if (point != null && !_snapshots.ContainsKey(point))
                {
                    _snapshots.Add(point, PointSnapshot.Capture(point));
                }
            }

            if (currentPoint != null && !_snapshots.ContainsKey(currentPoint))
            {
                _snapshots.Add(currentPoint, PointSnapshot.Capture(currentPoint));
            }

            _editablePoints = BuildEditablePoints(currentPoint);
            _pointGroups = BuildPointGroups(_editablePoints);
            _currentPoint = currentPoint;
            _currentPointIndex = IndexOfPoint(_editablePoints, currentPoint);

            ShowAllPoints = true;
            LoadFromCurrentPoint();
            RefreshChoiceSelection();

            SelectColorCommand = new RelayCommand(ExecuteSelectColor);
            ApplyRgbCommand = new RelayCommand(ExecuteApplyRgb);
            MoveToPreviousPointCommand = new RelayCommand(ExecuteMoveToPreviousPoint, CanMoveToPreviousPoint);
            MoveToNextPointCommand = new RelayCommand(ExecuteMoveToNextPoint, CanMoveToNextPoint);
            MoveToPointCommand = new RelayCommand(ExecuteMoveToPoint);
        }

        public IList<MeasurementPointViewModel> AllPoints { get; private set; }

        /// <summary>창에서 옮겨 다닐 수 있는 측정부입니다. 카메라 순서, 그 안에서 번호 순입니다.</summary>
        public IList<MeasurementPointViewModel> EditablePoints
        {
            get { return _editablePoints; }
        }

        /// <summary>
        /// 위 목록을 카메라별로 묶은 것입니다. 화면에서는 카메라마다 한 줄씩 보여 줍니다.
        ///
        /// <para>
        /// 한 줄에 모두 늘어놓으면 Top과 Thk가 섞여 어디까지가 어느 카메라인지 알기 어렵습니다.
        /// 측정부가 없는 카메라는 줄을 만들지 않습니다.
        /// </para>
        /// </summary>
        public IList<MeasurementPointGroupViewModel> PointGroups
        {
            get { return _pointGroups; }
        }

        public ICommand SelectColorCommand { get; private set; }

        public ICommand ApplyRgbCommand { get; private set; }

        public ICommand MoveToPreviousPointCommand { get; private set; }

        public ICommand MoveToNextPointCommand { get; private set; }

        public ICommand MoveToPointCommand { get; private set; }

        public bool ShowAllPoints
        {
            get { return _showAllPoints; }
            set { SetProperty(ref _showAllPoints, value); }
        }

        /// <summary>
        /// 지금 편집 중인 측정부입니다. 목록에서 고르면 그 측정부로 옮깁니다.
        /// </summary>
        public MeasurementPointViewModel CurrentPoint
        {
            get { return _currentPoint; }
            set
            {
                if (value == null || ReferenceEquals(value, _currentPoint))
                {
                    return;
                }

                MoveToPointAt(IndexOfPoint(_editablePoints, value));
            }
        }

        public int CurrentIndex
        {
            get { return _currentPoint.IndexNo; }
        }

        /// <summary>지금 편집 중인 측정부가 몇 번째인지 사람이 읽을 수 있게 적습니다.</summary>
        public string CurrentPointPositionText
        {
            get
            {
                if (_editablePoints.Count <= 1)
                {
                    return string.Empty;
                }

                return (_currentPointIndex + 1).ToString(CultureInfo.InvariantCulture) + " / " +
                       _editablePoints.Count.ToString(CultureInfo.InvariantCulture);
            }
        }

        public bool HasMultipleEditablePoints
        {
            get { return _editablePoints.Count > 1; }
        }

        /// <summary>지금 편집 중인 측정부의 배경 사진입니다. 카메라를 옮기면 이 값이 바뀝니다.</summary>
        public string CurrentImagePath
        {
            get { return ResolveImagePath(CurrentViewType); }
        }

        /// <summary>
        /// 지금 선을 긋고 있는 측정부의 카메라입니다.
        ///
        /// <para>
        /// 누적 표시는 이 카메라의 측정부만 그려야 합니다.
        /// 번호는 카메라마다 1부터 세므로 Top 1번과 Thickness 1번이 함께 있는데,
        /// 번호만 보고 가리면 다른 카메라의 선이 배경 위에 섞여 그려집니다.
        /// 배경은 이 카메라의 기준 이미지라 좌표계도 다릅니다.
        /// </para>
        /// </summary>
        public ImageViewType CurrentViewType
        {
            get { return _currentPoint.ViewType; }
        }

        public string CurrentPointName
        {
            get { return _currentPoint.PointName; }
        }

        /// <summary>
        /// 왼쪽에서 고르는 측정 항목 목록입니다. 목록과 코드값은
        /// <see cref="MeasurementItemTypePolicy"/> 한 곳에서 나옵니다.
        /// </summary>
        public IList<string> MeasurementItemTypeNames
        {
            get { return _measurementItemTypeNames; }
        }

        private readonly IList<string> _measurementItemTypeNames = BuildMeasurementItemTypeNames();

        private static IList<string> BuildMeasurementItemTypeNames()
        {
            IList<string> names = new List<string>();
            foreach (MeasurementItemType itemType in MeasurementItemTypePolicy.GetSelectableItemTypes())
            {
                names.Add(MeasurementItemTypePolicy.GetDisplayName(itemType));
            }

            return names;
        }

        /// <summary>
        /// 지금 측정부의 항목입니다.
        ///
        /// <para>
        /// 고르는 즉시 측정부에 넣습니다. 색과 같은 이유입니다 — 고른 그대로가 답이고,
        /// 넣지 않으면 측정부를 옮겨 다닐 때 옛 값이 남습니다.
        /// </para>
        ///
        /// <para>
        /// 항목을 바꾸면 그리는 모양도 함께 바뀝니다. 길이·너비·높이·두께는 두 점을 찍어 선을
        /// 긋고, 내경·외경은 원이 들어가는 네모를 끌어서 그립니다.
        /// </para>
        /// </summary>
        public string CurrentItemType
        {
            get { return _currentPoint.ItemType; }
            set
            {
                if (string.Equals(_currentPoint.ItemType, value, StringComparison.Ordinal))
                {
                    return;
                }

                bool wasRectangle = IsRectangleShape;
                _currentPoint.ItemType = value;

                // 선과 사각은 좌표의 뜻이 다릅니다(선은 두 끝점, 사각은 좌상단·우하단).
                // 모양이 바뀌면 예전 좌표를 그대로 두면 안 되므로 지웁니다.
                if (wasRectangle != IsRectangleShape)
                {
                    CancelCurrentDrawing();
                }

                OnPropertyChanged("CurrentItemType");
                OnPropertyChanged("IsRectangleShape");
                OnPropertyChanged("ShapeGuideText");
            }
        }

        /// <summary>
        /// 네모 좌표를 좌상단·우하단 순서로 맞춥니다.
        ///
        /// <para>
        /// 사용자는 어느 모서리에서 끌든 상관없지만, AI 에는 좌상단·우하단 순서로 보내기로
        /// 했습니다(사양 2.3절). 끌기가 끝난 시점에 한 번 정리해 두면 저장·전송·그리기가
        /// 모두 같은 뜻의 좌표를 씁니다.
        /// </para>
        /// </summary>
        public void NormalizeRectangleCoordinates()
        {
            if (!IsRectangleShape ||
                !_x1.HasValue || !_y1.HasValue || !_x2.HasValue || !_y2.HasValue)
            {
                return;
            }

            double left = Math.Min(_x1.Value, _x2.Value);
            double top = Math.Min(_y1.Value, _y2.Value);
            double right = Math.Max(_x1.Value, _x2.Value);
            double bottom = Math.Max(_y1.Value, _y2.Value);

            X1 = left;
            Y1 = top;
            X2 = right;
            Y2 = bottom;
        }

        /// <summary>내경·외경처럼 네모를 끌어서 지정하는 항목인지입니다.</summary>
        public bool IsRectangleShape
        {
            get { return MeasurementItemTypePolicy.IsRectangleShape(_currentPoint.ItemType); }
        }

        /// <summary>지금 항목을 어떻게 그리는지 알려 주는 한 줄입니다.</summary>
        public string ShapeGuideText
        {
            get
            {
                if (IsRectangleShape)
                {
                    return "원이 들어가는 네모를 끌어서 그립니다. 좌측 상단에서 우측 하단으로 끕니다.";
                }

                return "잴 두 점을 차례로 찍습니다. 오른쪽 버튼을 누르면 다시 시작합니다.";
            }
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

        /// <summary>
        /// 지금 긋고 있는 선의 색입니다.
        ///
        /// <para>
        /// 색을 고르는 즉시 측정부에도 넣습니다. 좌표는 선이 완성되어야 뜻이 있지만 색은
        /// 고른 그대로가 답이고, 넣지 않으면 옮겨 다니는 목록의 색 네모가 옛 색으로 남습니다.
        /// "취소"로 닫으면 이 색도 창을 열기 전으로 되돌아갑니다.
        /// </para>
        /// </summary>
        public string LineColor
        {
            get { return _lineColor; }
            set
            {
                if (SetProperty(ref _lineColor, NormalizeColor(value)))
                {
                    if (_currentPoint != null)
                    {
                        _currentPoint.LineColor = _lineColor;
                    }

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

        /// <summary>
        /// 지금 그리던 선을 이 측정부를 열었을 때의 상태로 되돌립니다.
        /// 다른 측정부에 이미 남긴 것은 건드리지 않습니다.
        /// </summary>
        public void CancelCurrentDrawing()
        {
            PointSnapshot snapshot;
            if (!_snapshots.TryGetValue(_currentPoint, out snapshot))
            {
                return;
            }

            X1 = snapshot.X1;
            Y1 = snapshot.Y1;
            X2 = snapshot.X2;
            Y2 = snapshot.Y2;
            LineColor = snapshot.LineColor;
        }

        public bool ApplyToCurrentPoint()
        {
            if (!HasCompleteLine)
            {
                return false;
            }

            _currentPoint.SetCoordinates(X1.Value, Y1.Value, X2.Value, Y2.Value, LineColor);
            _appliedAnyPoint = true;
            return true;
        }

        /// <summary>옮겨 다니며 하나라도 남긴 것이 있으면 참입니다.</summary>
        public bool HasAppliedAnyPoint
        {
            get { return _appliedAnyPoint; }
        }

        /// <summary>
        /// 창을 열기 전 상태로 모두 되돌립니다. "취소"로 닫을 때 씁니다.
        /// 옮겨 다니며 다른 측정부에 남긴 것도 함께 되돌려야 합니다.
        /// </summary>
        public void RestoreAllPoints()
        {
            foreach (KeyValuePair<MeasurementPointViewModel, PointSnapshot> pair in _snapshots)
            {
                pair.Value.RestoreTo(pair.Key);
            }

            _appliedAnyPoint = false;
        }

        /// <summary>
        /// 다른 측정부로 옮깁니다. 그리던 선이 완성되어 있으면 남기고 넘어갑니다.
        /// 시작점만 찍어 둔 채로 옮기면 그 측정부는 원래 값 그대로 둡니다.
        /// </summary>
        public void MoveToPointAt(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= _editablePoints.Count || pointIndex == _currentPointIndex)
            {
                return;
            }

            ApplyToCurrentPoint();

            _currentPointIndex = pointIndex;
            _currentPoint = _editablePoints[pointIndex];
            LoadFromCurrentPoint();
        }

        private void ExecuteMoveToPreviousPoint(object parameter)
        {
            MoveToPointAt(_currentPointIndex - 1);
        }

        private void ExecuteMoveToNextPoint(object parameter)
        {
            MoveToPointAt(_currentPointIndex + 1);
        }

        private void ExecuteMoveToPoint(object parameter)
        {
            MeasurementPointViewModel point = parameter as MeasurementPointViewModel;
            if (point == null)
            {
                return;
            }

            MoveToPointAt(IndexOfPoint(_editablePoints, point));
        }

        private bool CanMoveToPreviousPoint(object parameter)
        {
            return _currentPointIndex > 0;
        }

        private bool CanMoveToNextPoint(object parameter)
        {
            return _currentPointIndex >= 0 && _currentPointIndex < _editablePoints.Count - 1;
        }

        /// <summary>
        /// 옮겨 간 측정부의 좌표와 색을 화면에 올립니다.
        ///
        /// <para>
        /// 색을 빠뜨리면 이전 측정부의 색이 그대로 남아 다른 선과 같은 색으로 그려집니다.
        /// 번호(CurrentIndex)와 카메라(CurrentViewType)도 함께 알려야 누적 표시가 지금
        /// 편집 중인 선을 굵게, 나머지를 얇게 제대로 가릅니다.
        /// </para>
        /// </summary>
        private void LoadFromCurrentPoint()
        {
            _x1 = _currentPoint.X1;
            _y1 = _currentPoint.Y1;
            _x2 = _currentPoint.X2;
            _y2 = _currentPoint.Y2;
            _lineColor = string.IsNullOrWhiteSpace(_currentPoint.LineColor)
                ? MeasurementPointViewModel.GetDefaultColor(_currentPoint.IndexNo)
                : _currentPoint.LineColor;
            UpdateRgbFromColor();

            OnPropertyChanged("X1");
            OnPropertyChanged("Y1");
            OnPropertyChanged("X2");
            OnPropertyChanged("Y2");
            OnPropertyChanged("LineColor");
            OnPropertyChanged("HasStartPoint");
            OnPropertyChanged("HasCompleteLine");
            OnPropertyChanged("CurrentPoint");
            OnPropertyChanged("CurrentIndex");
            OnPropertyChanged("CurrentViewType");
            OnPropertyChanged("CurrentPointName");
            OnPropertyChanged("CurrentPointPositionText");
            OnPropertyChanged("CurrentItemType");
            OnPropertyChanged("IsRectangleShape");
            OnPropertyChanged("ShapeGuideText");
            OnPropertyChanged("CurrentImagePath");

            RefreshChoiceSelection();
            RaiseMoveCommandsChanged();
        }

        private void RaiseMoveCommandsChanged()
        {
            RelayCommand previousCommand = MoveToPreviousPointCommand as RelayCommand;
            if (previousCommand != null)
            {
                previousCommand.RaiseCanExecuteChanged();
            }

            RelayCommand nextCommand = MoveToNextPointCommand as RelayCommand;
            if (nextCommand != null)
            {
                nextCommand.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 옮겨 다닐 수 있는 측정부를 고릅니다.
        /// 배경으로 쓸 기준 이미지가 없는 카메라는 선을 그릴 수 없으므로 뺍니다.
        /// 지금 편집 중인 것은 어떤 경우에도 남깁니다.
        /// </summary>
        private IList<MeasurementPointViewModel> BuildEditablePoints(MeasurementPointViewModel currentPoint)
        {
            List<MeasurementPointViewModel> points = new List<MeasurementPointViewModel>();
            foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                bool hasImage = !string.IsNullOrWhiteSpace(ResolveImagePath(viewType));
                List<MeasurementPointViewModel> pointsForView = new List<MeasurementPointViewModel>();
                foreach (MeasurementPointViewModel point in AllPoints)
                {
                    if (point == null || point.ViewType != viewType)
                    {
                        continue;
                    }

                    if (!hasImage && point != currentPoint)
                    {
                        continue;
                    }

                    pointsForView.Add(point);
                }

                pointsForView.Sort(CompareByIndexNo);
                points.AddRange(pointsForView);
            }

            if (currentPoint != null && IndexOfPoint(points, currentPoint) < 0)
            {
                points.Insert(0, currentPoint);
            }

            return points;
        }

        /// <summary>
        /// 옮겨 다닐 측정부를 카메라별로 묶습니다. 화면은 이 묶음마다 한 줄을 그립니다.
        /// 측정부가 하나도 없는 카메라는 줄을 만들지 않습니다.
        /// </summary>
        private static IList<MeasurementPointGroupViewModel> BuildPointGroups(
            IList<MeasurementPointViewModel> points)
        {
            List<MeasurementPointGroupViewModel> groups = new List<MeasurementPointGroupViewModel>();
            if (points == null)
            {
                return groups;
            }

            foreach (ImageViewType viewType in MeasurementPointPolicy.GetSupportedViewTypes())
            {
                List<MeasurementPointChoiceViewModel> choicesForView = new List<MeasurementPointChoiceViewModel>();
                foreach (MeasurementPointViewModel point in points)
                {
                    if (point != null && point.ViewType == viewType)
                    {
                        choicesForView.Add(new MeasurementPointChoiceViewModel(point));
                    }
                }

                if (choicesForView.Count > 0)
                {
                    groups.Add(new MeasurementPointGroupViewModel(viewType, choicesForView));
                }
            }

            // 어느 카메라에도 속하지 않는 측정부가 섞여 있으면 따로 한 줄로 보여 줍니다.
            // 카메라를 넣어 주지 않은 측정부인데, 목록에서 빠지면 고칠 길이 없습니다.
            List<MeasurementPointChoiceViewModel> others = new List<MeasurementPointChoiceViewModel>();
            ImageViewType otherViewType = ImageViewType.Unclassified;
            foreach (MeasurementPointViewModel point in points)
            {
                if (point != null && !MeasurementPointPolicy.IsSupportedViewType(point.ViewType))
                {
                    otherViewType = point.ViewType;
                    others.Add(new MeasurementPointChoiceViewModel(point));
                }
            }

            if (others.Count > 0)
            {
                groups.Add(new MeasurementPointGroupViewModel(otherViewType, others));
            }

            return groups;
        }

        /// <summary>
        /// 골라진 칸을 지금 편집 중인 측정부 하나로 맞춥니다.
        ///
        /// <para>
        /// 창 전체에서 골라진 칸은 언제나 하나여야 합니다. 카메라마다 하나씩 남으면,
        /// 이미 골라진 것처럼 보이는 칸을 눌렀을 때 아무 일도 일어나지 않습니다.
        /// </para>
        /// </summary>
        private void RefreshChoiceSelection()
        {
            foreach (MeasurementPointGroupViewModel group in _pointGroups)
            {
                foreach (MeasurementPointChoiceViewModel choice in group.Choices)
                {
                    choice.IsCurrent = ReferenceEquals(choice.Point, _currentPoint);
                }
            }
        }

        private static int CompareByIndexNo(MeasurementPointViewModel left, MeasurementPointViewModel right)
        {
            return left.IndexNo.CompareTo(right.IndexNo);
        }

        private static int IndexOfPoint(IList<MeasurementPointViewModel> points, MeasurementPointViewModel point)
        {
            for (int index = 0; index < points.Count; index++)
            {
                if (ReferenceEquals(points[index], point))
                {
                    return index;
                }
            }

            return -1;
        }

        private string ResolveImagePath(ImageViewType viewType)
        {
            string imagePath;
            if (_imagePathByViewType.TryGetValue(viewType, out imagePath))
            {
                return imagePath;
            }

            return string.Empty;
        }

        /// <summary>창을 열었을 때의 측정부 좌표와 색입니다.</summary>
        private class PointSnapshot
        {
            private double? _x1;
            private double? _y1;
            private double? _x2;
            private double? _y2;
            private string _lineColor;

            public double? X1
            {
                get { return _x1; }
            }

            public double? Y1
            {
                get { return _y1; }
            }

            public double? X2
            {
                get { return _x2; }
            }

            public double? Y2
            {
                get { return _y2; }
            }

            public string LineColor
            {
                get { return _lineColor; }
            }

            public static PointSnapshot Capture(MeasurementPointViewModel point)
            {
                PointSnapshot snapshot = new PointSnapshot();
                snapshot._x1 = point.X1;
                snapshot._y1 = point.Y1;
                snapshot._x2 = point.X2;
                snapshot._y2 = point.Y2;
                snapshot._lineColor = point.LineColor;
                return snapshot;
            }

            /// <summary>
            /// 좌표를 아직 찍지 않았던 측정부는 다시 비워 둡니다.
            /// SetCoordinates는 값을 지울 수 없어 속성에 직접 넣습니다.
            /// </summary>
            public void RestoreTo(MeasurementPointViewModel point)
            {
                point.X1 = _x1;
                point.Y1 = _y1;
                point.X2 = _x2;
                point.Y2 = _y2;
                point.LineColor = _lineColor;
            }
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
