using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.ViewModels
{
    /// <summary>
    /// 이력 화면 목록에 표시할 검사 결과 모델입니다.
    /// </summary>
    public class InspectionRowViewModel : ObservableObject
    {
        public InspectionRowViewModel(Inspection inspection)
        {
            Id = inspection.Id;
            PartNo = inspection.PartNo;
            PartName = inspection.PartName;
            Result = inspection.Result.ToString();
            InspectedAt = inspection.InspectedAt.ToString("yyyy-MM-dd HH:mm:ss");
            Elapsed = inspection.ElapsedMilliseconds.ToString("0") + " ms";
            Message = inspection.ResultMessage;
        }

        public int Id { get; set; }

        public string PartNo { get; set; }

        public string PartName { get; set; }

        public string Result { get; set; }

        public string InspectedAt { get; set; }

        public string Elapsed { get; set; }

        public string Message { get; set; }
    }
}
