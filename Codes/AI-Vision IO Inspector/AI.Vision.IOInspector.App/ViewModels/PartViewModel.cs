using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 부품 목록과 DB 조회 화면에서 사용할 표시 모델입니다.
    /// </summary>
    public class PartViewModel : ObservableObject
    {
        private readonly Part _part;

        public PartViewModel(Part part)
        {
            _part = part;
        }

        public Part Part
        {
            get { return _part; }
        }

        public string PartNo
        {
            get { return _part.PartNo; }
        }

        public string PartName
        {
            get { return _part.PartName; }
        }

        public string CategoryCode
        {
            get { return _part.CategoryCode; }
        }

        public string CategoryDescription
        {
            get { return _part.CategoryDescription; }
        }

        public string Memo
        {
            get { return _part.Memo; }
        }

        public string DisplayName
        {
            get { return _part.PartNo + "_" + _part.PartName; }
        }
    }
}
