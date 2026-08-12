using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 카메라 촬영 결과입니다. 현재는 시뮬레이션 데이터를 사용하고, 추후 SDK 결과로 교체합니다.
    /// </summary>
    public class CapturedImage
    {
        public ImageViewType ViewType { get; set; }

        public string DisplayName { get; set; }

        public string FilePath { get; set; }

        public DateTime CapturedAt { get; set; }

        /// <summary>
        /// 카메라에서 실제 프레임을 받지 못해 검정 이미지로 대체한 경우 true입니다.
        ///
        /// 카메라 고장은 검사 오류로 처리하지 않습니다(2026-08-12 정책).
        /// 대신 설정 해상도의 검정 이미지를 만들어 6장을 채운 뒤 AI에 전달하고,
        /// PASS/FAIL 판정은 AI 결과 파싱 값만 따릅니다.
        /// 다만 작업자가 상황을 알 수 있어야 하므로 이 값으로 구분해 검사 이벤트에 경고를 남깁니다.
        /// </summary>
        public bool IsPlaceholder { get; set; }

        /// <summary>검정 이미지로 대체한 사유입니다. IsPlaceholder가 true일 때만 채워집니다.</summary>
        public string PlaceholderReason { get; set; }
    }
}
