using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 검사 화면의 기준 이미지를 하나의 확대 창에서 확인하기 위한 UI 서비스입니다.
    /// Search DB 선택 품목이 바뀌어도 창을 중복 생성하지 않고 현재 창의 내용만 갱신합니다.
    /// </summary>
    public interface IReferenceImagePopupService
    {
        void Show(Part part, ImageViewType selectedViewType);

        void Update(Part part);

        void Close();
    }
}
