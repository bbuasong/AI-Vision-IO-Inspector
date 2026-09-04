using System;
using System.Collections.Generic;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 측정부 항목의 이름과 그리는 모양을 한곳에서 정합니다.
    ///
    /// <para>
    /// 예전에는 화면 코드에 "길이", "너비" 문자열을 직접 박아 두었습니다. 항목이 늘 때마다
    /// 여러 자리를 찾아 고쳐야 했고, AI 로 보낼 코드값과 화면 이름이 서로 다른 곳에 흩어졌습니다.
    /// 이제 <see cref="MeasurementItemType"/> 하나에서 코드값·이름·모양이 모두 나옵니다.
    /// </para>
    ///
    /// <para>
    /// DB 에는 예전부터 한글 이름이 문자열로 저장돼 있습니다(11,000 행 이상). 그래서 이름은
    /// 바꾸지 않고 그대로 쓰며, <see cref="Parse"/> 가 저장된 문자열을 항목으로 되돌립니다.
    /// </para>
    /// </summary>
    public static class MeasurementItemTypePolicy
    {
        private const string NoneName = "미설정";
        private const string LengthName = "길이";
        private const string WidthName = "너비";
        private const string HeightName = "높이";
        private const string ThicknessName = "두께";
        private const string InnerDiameterName = "내경";
        private const string OuterDiameterName = "외경";

        /// <summary>
        /// 부품 등록 화면과 위치지정 팝업의 항목 목록입니다. 표시 순서가 이 순서입니다.
        /// </summary>
        public static IList<MeasurementItemType> GetSelectableItemTypes()
        {
            return new List<MeasurementItemType>
            {
                MeasurementItemType.None,
                MeasurementItemType.Length,
                MeasurementItemType.Width,
                MeasurementItemType.Height,
                MeasurementItemType.Thickness,
                MeasurementItemType.InnerDiameter,
                MeasurementItemType.OuterDiameter
            };
        }

        /// <summary>화면과 DB 에 쓰는 한글 이름입니다.</summary>
        public static string GetDisplayName(MeasurementItemType itemType)
        {
            switch (itemType)
            {
                case MeasurementItemType.Length:
                    return LengthName;
                case MeasurementItemType.Width:
                    return WidthName;
                case MeasurementItemType.Height:
                    return HeightName;
                case MeasurementItemType.Thickness:
                    return ThicknessName;
                case MeasurementItemType.InnerDiameter:
                    return InnerDiameterName;
                case MeasurementItemType.OuterDiameter:
                    return OuterDiameterName;
                default:
                    return NoneName;
            }
        }

        /// <summary>
        /// DB 나 화면에 저장된 이름을 항목으로 되돌립니다. 알아볼 수 없으면 <c>None</c> 입니다.
        /// </summary>
        public static MeasurementItemType Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return MeasurementItemType.None;
            }

            string trimmed = name.Trim();

            if (string.Equals(trimmed, LengthName, StringComparison.OrdinalIgnoreCase))
            {
                return MeasurementItemType.Length;
            }

            if (string.Equals(trimmed, WidthName, StringComparison.OrdinalIgnoreCase))
            {
                return MeasurementItemType.Width;
            }

            if (string.Equals(trimmed, HeightName, StringComparison.OrdinalIgnoreCase))
            {
                return MeasurementItemType.Height;
            }

            if (string.Equals(trimmed, ThicknessName, StringComparison.OrdinalIgnoreCase))
            {
                return MeasurementItemType.Thickness;
            }

            if (string.Equals(trimmed, InnerDiameterName, StringComparison.OrdinalIgnoreCase))
            {
                return MeasurementItemType.InnerDiameter;
            }

            if (string.Equals(trimmed, OuterDiameterName, StringComparison.OrdinalIgnoreCase))
            {
                return MeasurementItemType.OuterDiameter;
            }

            return MeasurementItemType.None;
        }

        /// <summary>AI 요청 JSON 의 <c>itemType</c> 으로 보낼 값입니다.</summary>
        public static int GetItemTypeCode(MeasurementItemType itemType)
        {
            return (int)itemType;
        }

        /// <summary>저장된 이름을 그대로 코드값으로 바꿉니다.</summary>
        public static int GetItemTypeCode(string name)
        {
            return (int)Parse(name);
        }

        /// <summary>
        /// 사각을 드래그해 지정하는 항목인지입니다. 내경·외경은 원을 감싸는 사각 범위를 받습니다.
        /// </summary>
        public static bool IsRectangleShape(MeasurementItemType itemType)
        {
            return itemType == MeasurementItemType.InnerDiameter ||
                   itemType == MeasurementItemType.OuterDiameter;
        }

        /// <summary>저장된 이름 기준으로 사각 항목인지 봅니다.</summary>
        public static bool IsRectangleShape(string name)
        {
            return IsRectangleShape(Parse(name));
        }

        /// <summary>
        /// 선 두 점으로 지정하는 항목인지입니다. 길이·너비·높이·두께가 여기 해당합니다.
        /// </summary>
        public static bool IsLineShape(MeasurementItemType itemType)
        {
            return itemType != MeasurementItemType.None && !IsRectangleShape(itemType);
        }

        /// <summary>저장된 이름 기준으로 선 항목인지 봅니다.</summary>
        public static bool IsLineShape(string name)
        {
            return IsLineShape(Parse(name));
        }
    }
}
