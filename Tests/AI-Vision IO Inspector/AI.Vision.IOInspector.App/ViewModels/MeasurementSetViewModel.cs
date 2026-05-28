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
        private string _widthValue;
        private string _heightValue;
        private string _thicknessValue;

        public MeasurementSetViewModel()
        {
            LengthValue = "-";
            WidthValue = "-";
            HeightValue = "-";
            ThicknessValue = "-";
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

        public string WidthValue
        {
            get { return _widthValue; }
            set { SetProperty(ref _widthValue, value); }
        }

        public string HeightValue
        {
            get { return _heightValue; }
            set { SetProperty(ref _heightValue, value); }
        }

        public string ThicknessValue
        {
            get { return _thicknessValue; }
            set { SetProperty(ref _thicknessValue, value); }
        }

        public bool HasAnyValue()
        {
            return HasValue(LengthValue) || HasValue(WidthValue) || HasValue(HeightValue) || HasValue(ThicknessValue);
        }

        public bool HasValue(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Trim() != "-";
        }

        public void AddRegionsToPart(Part part, int setIndex, ref int regionId)
        {
            AddRegion(part, setIndex, "길이", ImageViewType.Top, LengthValue, ref regionId);
            AddRegion(part, setIndex, "너비", ImageViewType.Front, WidthValue, ref regionId);
            AddRegion(part, setIndex, "높이", ImageViewType.Back, HeightValue, ref regionId);
            AddRegion(part, setIndex, "두께", ImageViewType.Thickness, ThicknessValue, ref regionId);
        }

        private void AddRegion(Part part, int setIndex, string itemName, ImageViewType viewType, string value, ref int regionId)
        {
            decimal parsedValue;
            if (!HasValue(value) || !decimal.TryParse(value, out parsedValue))
            {
                return;
            }

            MeasurementRegion region = new MeasurementRegion();
            region.Id = regionId;
            region.PartNo = part.PartNo;
            region.Name = "측정부 " + setIndex + " - " + itemName;
            region.ViewType = viewType;
            region.NominalValue = parsedValue;
            region.ToleranceMin = -0.5m;
            region.ToleranceMax = 0.5m;
            region.Unit = "mm";
            region.Coordinates = "미정";
            part.MeasurementRegions.Add(region);
            regionId++;
        }
    }
}
