using System;
using System.Globalization;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 부품별로 최대 5개까지 등록하는 독립 측정부 한 개를 편집합니다.
    /// 좌표는 Thickness 기준 이미지에서 사용자가 지정하며 실제 길이 계산에는 사용하지 않습니다.
    /// </summary>
    public class MeasurementPointViewModel : ObservableObject
    {
        // 측정부가 속한 카메라입니다. 아직 정해지지 않은 상태로 시작합니다.
        //
        // 예전에는 Thickness 를 기본값으로 두었습니다. 측정부가 Thickness 하나뿐이던 시절의
        // 흔적인데, 카메라별로 관리하게 되면서 위험한 기본값이 되었습니다. 어디선가 카메라를
        // 넣어 주는 것을 빠뜨리면 그 측정부가 조용히 Thickness 로 흘러가, Top 측정부가
        // 화면에서 사라지고 저장할 때 DB의 Top 자료까지 덮어썼습니다.
        //
        // Unclassified 로 두면 빠뜨렸을 때 조용히 넘어가지 않습니다.
        // TryBuildRegion 이 저장 직전에 막고 어느 측정부인지 알려 줍니다.
        private ImageViewType _viewType = ImageViewType.Unclassified;

        private int _indexNo;
        private string _nominalValue;
        private string _toleranceMin;
        private string _toleranceMax;
        private string _itemType;
        private double? _x1;
        private double? _y1;
        private double? _x2;
        private double? _y2;
        private string _lineColor;

        public MeasurementPointViewModel()
        {
            NominalValue = string.Empty;
            ToleranceMin = "0";
            ToleranceMax = "0";
            ItemType = "미설정";
            Unit = "mm";
            LineColor = MeasurementPointPolicy.GetDefaultColor(1);
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

        /// <summary>
        /// 이 측정부가 속한 카메라입니다. 측정부는 카메라마다 따로 관리합니다.
        /// </summary>
        public ImageViewType ViewType
        {
            get { return _viewType; }
            set
            {
                if (SetProperty(ref _viewType, value))
                {
                    OnPropertyChanged("PointName");
                    OnPropertyChanged("ViewShortName");
                }
            }
        }

        /// <summary>목록에 적을 카메라 이름입니다. Top, Thk 처럼 짧게 적습니다.</summary>
        public string ViewShortName
        {
            get { return MeasurementPointPolicy.GetViewShortName(ViewType); }
        }

        /// <summary>
        /// 화면에 적을 이름입니다. 카메라와 번호를 함께 보여줍니다.
        ///   예) Top 1, Thk 2
        ///
        /// <para>
        /// 번호는 카메라마다 1부터 세므로 번호만으로는 어느 카메라의 것인지 알 수 없습니다.
        /// </para>
        /// </summary>
        public string PointName
        {
            get { return MeasurementPointPolicy.BuildPointName(ViewType, IndexNo); }
        }

        public string NominalValue
        {
            get { return _nominalValue; }
            set { SetProperty(ref _nominalValue, value); }
        }

        public string ToleranceMin
        {
            get { return _toleranceMin; }
            set
            {
                if (SetProperty(ref _toleranceMin, value))
                {
                    OnPropertyChanged("Tolerance");
                }
            }
        }

        public string ToleranceMax
        {
            get { return _toleranceMax; }
            set
            {
                if (SetProperty(ref _toleranceMax, value))
                {
                    OnPropertyChanged("Tolerance");
                }
            }
        }

        public string Tolerance
        {
            get { return "-" + ToleranceMin + " ~ +" + ToleranceMax; }
            set
            {
                ToleranceMin = value;
                ToleranceMax = value;
            }
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

            decimal toleranceMin;
            if (!TryParseAbsoluteDecimal(ToleranceMin, out toleranceMin))
            {
                errorMessage = PointName + " Min 허용값을 숫자로 입력하세요.";
                return false;
            }

            decimal toleranceMax;
            if (!TryParseAbsoluteDecimal(ToleranceMax, out toleranceMax))
            {
                errorMessage = PointName + " Max 허용값을 숫자로 입력하세요.";
                return false;
            }

            // 카메라가 정해지지 않은 측정부는 저장하지 않습니다.
            // 여기서 막지 않으면 어느 카메라의 것인지 모른 채 DB에 들어가고,
            // 다시 읽을 때 화면 어느 탭에도 나타나지 않습니다.
            if (!MeasurementPointPolicy.IsSupportedViewType(ViewType))
            {
                errorMessage = PointName + " 측정부의 카메라가 정해지지 않았습니다. 측정부를 지우고 다시 추가하세요.";
                return false;
            }

            region = new MeasurementRegion();
            region.Id = regionId;
            region.PartNo = partNo;
            region.IndexNo = IndexNo;
            region.ItemType = string.IsNullOrWhiteSpace(ItemType) ? "미설정" : ItemType.Trim();
            region.Name = PointName + " - " + region.ItemType;
            region.ViewType = ViewType;
            region.NominalValue = nominalValue;
            region.ToleranceMin = toleranceMin;
            region.ToleranceMax = toleranceMax;
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

            // 어느 카메라의 측정부인지 먼저 옮깁니다.
            //
            // 이것을 빠뜨리면 기본값 Thickness 로 남아, DB에 Top 으로 저장해 둔 측정부가
            // 화면에 올라오는 순간 모두 Thickness 가 됩니다. Top 탭은 비어 보이고,
            // 카메라마다 다섯 개를 세는 규칙에 걸려 나머지가 버려집니다. 그 상태로 다시
            // 저장하면 DB의 Top 측정부까지 Thickness 로 덮여 되돌릴 수 없게 됩니다.
            //
            // 이름(PointName)도 카메라를 보고 만들므로 번호보다 먼저 넣습니다.
            point.ViewType = region.ViewType;
            point.IndexNo = region.IndexNo > 0 ? region.IndexNo : fallbackIndex;
            point.NominalValue = region.NominalValue.ToString(CultureInfo.InvariantCulture);
            point.ToleranceMin = region.ToleranceMin.ToString(CultureInfo.InvariantCulture);
            point.ToleranceMax = region.ToleranceMax.ToString(CultureInfo.InvariantCulture);
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
            return MeasurementPointPolicy.GetDefaultColor(indexNo);
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

        private static bool TryParseAbsoluteDecimal(string value, out decimal parsedValue)
        {
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out parsedValue) &&
                !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue))
            {
                return false;
            }

            if (parsedValue < 0)
            {
                parsedValue = -parsedValue;
            }

            return true;
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
