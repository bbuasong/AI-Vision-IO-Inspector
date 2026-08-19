using System;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 부품별 기준 이미지입니다. 실제 파일 저장 정책이 확정되면 FilePath를 실제 경로로 연결합니다.
    /// </summary>
    public class PartImage
    {
        public int Id { get; set; }

        public string PartNo { get; set; }

        public ImageViewType ViewType { get; set; }

        public string FilePath { get; set; }

        public DateTime CapturedAt { get; set; }

        /// <summary>
        /// DB 저장 전 DB\Image\Temp에 보관 중인 작업 이미지인지 구분합니다.
        /// 이 값은 실행 중 파일 확정 흐름에만 사용하며 DB 컬럼으로 저장하지 않습니다.
        /// </summary>
        public bool IsTemporary { get; set; }

        /// <summary>
        /// 이 이미지가 속한 벌의 번호입니다. 부품마다 1부터 세며, 저장할 때마다 하나씩 늘어납니다.
        /// 한 번의 저장에서 나온 이미지들은 같은 번호를 갖습니다.
        /// 파일명과 화면의 벌 목록에 그대로 쓰입니다.
        /// </summary>
        public int SetNo { get; set; }
    }
}
