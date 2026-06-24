using System;
using System.Globalization;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 부품별로 최대 10개까지 등록하는 독립 측정부 한 개를 편집합니다.
    /// 좌표는 Thickness 기준 이미지에서 사용자가 지정하며 실제 길이 계산에는 사용하지 않습니다.
    /// </summary>
    public class MeasurementPointViewModel : ObservableObject
    {
        private static readonly string[] DefaultColors =
        {
            "#0072B2",
            "#E69F00",
            "#009E73",
            "#D55E00",
            "#CC79A7",
            "#56B4E9",
            "#F0E442",
            "#7F3C8D",
            "#11A579",
            "#FFFFFF"
        };

        private int _indexNo;
        private string _nominalValue;
        private string _tolerance;
        private string _itemType;
        private double? _x1;
        private double? _y1;
        private double? _x2;
        private double? _y2;
        private string _lineColor;

        public MeasurementPointViewModel()
        {
            NominalValue = string.Empty;
            Tolerance = "0";
            ItemType = "미설정";
            Unit = "mm";
            LineColor = DefaultColors[0];
        }

        public int IndexNo
        {
            get { return _indexNo; }
            set
            {
                if (SetProperty(ref _indexNo, value))
                {
                    OnPropertyChanged("PointName");
                }
            }
        }

        public string PointName
        {
            get { return "측정부" + IndexNo.ToString(CultureInfo.InvariantCulture); }
        }

        public string NominalValue
        {
            get { return _nominalValue; }
            set { SetProperty(ref _nominalValue, value); }
        }

        public string Tolerance
        {
            get { return _tolerance; }
            set { SetProperty(ref _tolerance, value); }
        }

        public string ItemType
        {
            get { return _itemType; }
            set { SetProperty(ref _itemType, string.IsNullOrWhiteSpace(value) ? "미설정" : value.Trim()); }
        }

        public double? X1
        {
            get { return _x1; }
            set { SetCoordinate(ref _x1, value, "X1"); }
        }

        public double? Y1
        {
            get { return _y1; }
            set { SetCoordinate(ref _y1, value, "Y1"); }
        }

        public double? X2
        {
            get { return _x2; }
            set { SetCoordinate(ref _x2, value, "X2"); }
        }

        public double? Y2
        {
            get { return _y2; }
            set { SetCoordinate(ref _y2, value, "Y2"); }
        }

        public string Unit { get; private set; }

        public string LineColor
        {
            get { return _lineColor; }
            set
            {
                string resolved = string.IsNullOrWhiteSpace(value) ? GetDefaultColor(IndexNo) : value.Trim();
                SetProperty(ref _lineColor, resolved);
            }
        }

        public bool HasCoordinates
        {
            get { return X1.HasValue && Y1.HasValue && X2.HasValue && Y2.HasValue; }
        }

        public void ApplyIndex(int indexNo)
        {
            IndexNo = indexNo;
            if (string.IsNullOrWhiteSpace(LineColor))
            {
                LineColor = GetDefaultColor(indexNo);
            }
        }

        public void SetCoordinates(double x1, double y1, double x2, double y2, string lineColor)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            LineColor = lineColor;
        }

        public bool TryBuildRegion(string partNo, int regionId, out MeasurementRegion region, out string errorMessage)
        {
            region = null;
            errorMessage = string.Empty;

            decimal nominalValue;
            if (!decimal.TryParse(NominalValue, NumberStyles.Number, CultureInfo.CurrentCulture, out nominalValue) &&
                !decimal.TryParse(NominalValue, NumberStyles.Number, CultureInfo.InvariantCulture, out nominalValue))
            {
                errorMessage = PointName + " 기준값을 숫자로 입력하세요.";
                return false;
            }

            decimal tolerance;
            if (!decimal.TryParse(Tolerance, NumberStyles.Number, CultureInfo.CurrentCulture, out tolerance) &&
                !decimal.TryParse(Tolerance, NumberStyles.Number, CultureInfo.InvariantCulture, out tolerance))
            {
                errorMessage = PointName + " 허용값을 숫자로 입력하세요.";
                return false;
            }

            if (tolerance < 0)
            {
                tolerance = -tolerance;
            }

            region = new MeasurementRegion();
            region.Id = regionId;
            region.PartNo = partNo;
            region.IndexNo = IndexNo;
            region.ItemType = string.IsNullOrWhiteSpace(ItemType) ? "미설정" : ItemType.Trim();
            region.Name = PointName + " - " + region.ItemType;
            region.ViewType = ImageViewType.Thickness;
            region.NominalValue = nominalValue;
            region.ToleranceMin = -tolerance;
            region.ToleranceMax = tolerance;
            region.Unit = "mm";
            region.X1 = X1;
            region.Y1 = Y1;
            region.X2 = X2;
            region.Y2 = Y2;
            region.LineColor = LineColor;
            region.Coordinates = BuildCoordinatesText();
            return true;
        }

        public static MeasurementPointViewModel FromRegion(MeasurementRegion region, int fallbackIndex)
        {
            MeasurementPointViewModel point = new MeasurementPointViewModel();
            point.IndexNo = region.IndexNo > 0 ? region.IndexNo : fallbackIndex;
            point.NominalValue = region.NominalValue.ToString(CultureInfo.InvariantCulture);
            point.Tolerance = Math.Max(Math.Abs(region.ToleranceMin), Math.Abs(region.ToleranceMax)).ToString(CultureInfo.InvariantCulture);
            point.ItemType = ResolveItemType(region);
            point.X1 = region.X1;
            point.Y1 = region.Y1;
            point.X2 = region.X2;
            point.Y2 = region.Y2;
            point.LineColor = string.IsNullOrWhiteSpace(region.LineColor) ? GetDefaultColor(point.IndexNo) : region.LineColor;
            return point;
        }

        public static string GetDefaultColor(int indexNo)
        {
            int colorIndex = indexNo <= 0 ? 0 : (indexNo - 1) % DefaultColors.Length;
            return DefaultColors[colorIndex];
        }

        private static string ResolveItemType(MeasurementRegion region)
        {
            if (!string.IsNullOrWhiteSpace(region.ItemType))
            {
                return region.ItemType.Trim();
            }

            string name = region.Name ?? string.Empty;
            int separatorIndex = name.LastIndexOf('-');
            if (separatorIndex >= 0 && separatorIndex < name.Length - 1)
            {
                return name.Substring(separatorIndex + 1).Trim();
            }

            return "미설정";
        }

        private string BuildCoordinatesText()
        {
            if (!HasCoordinates)
            {
                return "미지정";
            }

            return X1.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   Y1.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   X2.Value.ToString("0.###", CultureInfo.InvariantCulture) + "," +
                   Y2.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void SetCoordinate(ref double? storage, double? value, string propertyName)
        {
            if (SetProperty(ref storage, value, propertyName))
            {
                OnPropertyChanged("HasCoordinates");
            }
        }
    }
}
