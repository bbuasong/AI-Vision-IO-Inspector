using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 부품 등록 화면에서 측정부 한 세트를 편집하기 위한 모델입니다.
    /// 길이/너비/높이/두께는 사용하지 않는 항목을 '-'로 유지하고 저장 시 제외합니다.
    /// </summary>
    public class MeasurementSetViewModel : ObservableObject
    {
        private string _setName;
        private string _lengthValue;
        private string _lengthTolerance;
        private string _widthValue;
        private string _widthTolerance;
        private string _heightValue;
        private string _heightTolerance;
        private string _thicknessValue;
        private string _thicknessTolerance;
        private string _unit;

        public MeasurementSetViewModel()
        {
            LengthValue = "-";
            LengthTolerance = "0";
            WidthValue = "-";
            WidthTolerance = "0";
            HeightValue = "-";
            HeightTolerance = "0";
            ThicknessValue = "-";
            ThicknessTolerance = "0";
            Unit = "mm";
        }

        public string SetName
        {
            get { return _setName; }
            set { SetProperty(ref _setName, value); }
        }

        public string LengthValue
        {
            get { return _lengthValue; }
            set { SetProperty(ref _lengthValue, value); }
        }

        public string LengthTolerance
        {
            get { return _lengthTolerance; }
            set { SetProperty(ref _lengthTolerance, value); }
        }

        public string WidthValue
        {
            get { return _widthValue; }
            set { SetProperty(ref _widthValue, value); }
        }

        public string WidthTolerance
        {
            get { return _widthTolerance; }
            set { SetProperty(ref _widthTolerance, value); }
        }

        public string HeightValue
        {
            get { return _heightValue; }
            set { SetProperty(ref _heightValue, value); }
        }

        public string HeightTolerance
        {
            get { return _heightTolerance; }
            set { SetProperty(ref _heightTolerance, value); }
        }

        public string ThicknessValue
        {
            get { return _thicknessValue; }
            set { SetProperty(ref _thicknessValue, value); }
        }

        public string ThicknessTolerance
        {
            get { return _thicknessTolerance; }
            set { SetProperty(ref _thicknessTolerance, value); }
        }

        public string Unit
        {
            get { return _unit; }
            set { SetProperty(ref _unit, ResolveUnit(value)); }
        }

        public bool HasAnyValue()
        {
            return HasValue(LengthValue) || HasValue(WidthValue) || HasValue(HeightValue) || HasValue(ThicknessValue);
        }

        public bool HasValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim() != "-";
        }

        public void AddRegionsToPart(Part part, int setIndex, bool useSingleSetName, ref int regionId)
        {
            AddRegion(part, setIndex, useSingleSetName, "길이", ImageViewType.Top, LengthValue, LengthTolerance, Unit, ref regionId);
            AddRegion(part, setIndex, useSingleSetName, "너비", ImageViewType.Front, WidthValue, WidthTolerance, Unit, ref regionId);
            AddRegion(part, setIndex, useSingleSetName, "높이", ImageViewType.Back, HeightValue, HeightTolerance, Unit, ref regionId);
            AddRegion(part, setIndex, useSingleSetName, "두께", ImageViewType.Thickness, ThicknessValue, ThicknessTolerance, Unit, ref regionId);
        }

        private void AddRegion(Part part, int setIndex, bool useSingleSetName, string itemName, ImageViewType viewType, string value, string tolerance, string unit, ref int regionId)
        {
            decimal parsedValue;
            if (!HasValue(value) || !decimal.TryParse(value, out parsedValue))
            {
                return;
            }

            decimal parsedTolerance = ResolveTolerance(tolerance);
            MeasurementRegion region = new MeasurementRegion();
            region.Id = regionId;
            region.PartNo = part.PartNo;
            region.IndexNo = regionId;
            region.ItemType = itemName;
            region.Name = "측정부" + region.IndexNo.ToString() + " - " + itemName;
            region.ViewType = ImageViewType.Thickness;
            region.NominalValue = parsedValue;
            region.ToleranceMin = -parsedTolerance;
            region.ToleranceMax = parsedTolerance;
            region.Unit = ResolveUnit(unit);
            region.Coordinates = "미지정";
            region.LineColor = MeasurementPointViewModel.GetDefaultColor(region.IndexNo);
            part.MeasurementRegions.Add(region);
            regionId++;
        }

        private decimal ResolveTolerance(string tolerance)
        {
            decimal parsedTolerance;
            if (string.IsNullOrWhiteSpace(tolerance) || !decimal.TryParse(tolerance, out parsedTolerance))
            {
                return 0m;
            }

            if (parsedTolerance < 0)
            {
                return -parsedTolerance;
            }

            return parsedTolerance;
        }

        private string ResolveUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return "mm";
            }

            return unit.Trim();
        }
    }
}
