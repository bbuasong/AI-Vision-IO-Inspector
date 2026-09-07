using System;
using System.Collections.Generic;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 입고검사 대상 부품의 기준정보입니다.
    /// 화면의 Part No, Part Name, 분류코드, 분류설명, 구분 항목과 연결됩니다.
    /// </summary>
    public class Part
    {
        public Part()
        {
            Images = new List<PartImage>();
            MeasurementRegions = new List<MeasurementRegion>();
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
            _partCount = DefaultPartCount;
        }

        /// <summary>한 검사에 올리는 기본 개수입니다. 대부분의 부품이 한 개입니다.</summary>
        public const int DefaultPartCount = 1;

        /// <summary>품목개수로 넣을 수 있는 가장 작은 값입니다.</summary>
        public const int MinPartCount = 1;

        /// <summary>품목개수로 넣을 수 있는 가장 큰 값입니다.</summary>
        public const int MaxPartCount = 255;

        public string PartNo { get; set; }

        public string PartName { get; set; }

        /// <summary>
        /// 한 세트로 함께 검사하는 개수입니다. 화면의 「품목개수」입니다.
        ///
        /// <para>
        /// 오링처럼 여러 개가 한 세트인 부품이 있습니다. 검사대에 세 개를 올려야 하는데 두 개만
        /// 올라온 것을 잡으려면, AI 가 기준 개수를 알아야 합니다. 그 기준값을 여기에 둡니다.
        /// 실제 개수를 세고 판정하는 일은 AI 가 합니다. 앱은 기준값만 넘깁니다.
        /// </para>
        ///
        /// <para>
        /// 1~255 를 벗어난 값은 기본값 1 로 되돌립니다. 잘라서 255 로 만들면 사용자가 넣으려던
        /// 값과도 다르고 화면에 뜬 값과도 달라집니다. 화면과 CSV 양쪽에서 들어오는 값이라
        /// 규칙을 한곳에 두고 같게 다룹니다.
        /// </para>
        /// </summary>
        public int PartCount
        {
            get { return _partCount; }
            set
            {
                if (value < MinPartCount || value > MaxPartCount)
                {
                    _partCount = DefaultPartCount;
                }
                else
                {
                    _partCount = value;
                }
            }
        }

        private int _partCount;

        public string CategoryCode { get; set; }

        public string CategoryDescription { get; set; }

        public string Memo { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public IList<PartImage> Images { get; private set; }

        public IList<MeasurementRegion> MeasurementRegions { get; private set; }
    }
}
