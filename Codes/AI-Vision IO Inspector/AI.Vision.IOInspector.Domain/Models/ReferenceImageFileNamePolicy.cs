using System;
using System.Globalization;
using System.IO;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 기준 이미지 파일의 이름을 프로젝트 전체에서 동일하게 생성합니다.
    ///
    /// <para>
    /// 형식은 [순번_방향][품번]_저장시각.png 입니다.
    ///   예) [01_Top][01100-51430]_20260819-103015.png
    /// </para>
    ///
    /// <para>
    /// 저장 시각을 붙이는 이유는 기준 이미지를 여러 벌 보관하기 위해서입니다.
    /// 예전에는 방향마다 파일 하나를 덮어써서 마지막 것만 남았습니다.
    /// 이제 저장 버튼을 누를 때마다 그 시각의 이미지가 한 벌로 쌓이고,
    /// <b>한 번의 저장에서 나온 6장은 같은 시각 문자열을 공유</b>해 한 벌로 묶입니다.
    /// </para>
    ///
    /// <para>
    /// 순번은 방향 순서(Top=1, Front=2, Back=3, Left=4, Right=5, Thickness=6)입니다.
    /// 파일 목록을 이름순으로 정렬했을 때 화면과 같은 차례로 보이게 하려고 두 자리로 적습니다.
    /// </para>
    /// </summary>
    public static class ReferenceImageFileNamePolicy
    {
        public const string LegacyCoordinateFileName = "coordinate.png";

        /// <summary>파일명에 적는 저장 시각 형식입니다. 초 단위까지 씁니다.</summary>
        public const string SavedAtFormat = "yyyyMMdd-HHmmss";

        /// <summary>
        /// 기준 이미지 파일 이름을 만듭니다.
        /// </summary>
        /// <param name="viewType">카메라 방향입니다. 순번과 이름에 함께 쓰입니다.</param>
        /// <param name="partNo">품번입니다. 파일명에 쓸 수 없는 글자는 밑줄로 바뀝니다.</param>
        /// <param name="savedAt">저장 버튼을 누른 시각입니다. 한 벌의 6장이 같은 값을 써야 합니다.</param>
        /// <param name="extension">확장자입니다. 비어 있으면 .png를 씁니다.</param>
        public static string BuildImageFileName(
            ImageViewType viewType,
            string partNo,
            DateTime savedAt,
            string extension)
        {
            string safeExtension = string.IsNullOrWhiteSpace(extension) ? ".png" : extension;
            if (!safeExtension.StartsWith(".", StringComparison.Ordinal))
            {
                safeExtension = "." + safeExtension;
            }

            return "[" + BuildViewOrderText(viewType) + "_" + viewType.ToString() + "]" +
                   "[" + MakeSafeFileNamePart(partNo) + "]" +
                   "_" + savedAt.ToString(SavedAtFormat, CultureInfo.InvariantCulture) +
                   safeExtension;
        }

        /// <summary>
        /// 방향 순번을 두 자리 문자열로 만듭니다. Top이 01, Thickness가 06입니다.
        /// </summary>
        public static string BuildViewOrderText(ImageViewType viewType)
        {
            int order = (int)viewType + 1;
            return order.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 측정부 좌표 이미지의 이름입니다.
        /// 좌표는 부품의 측정부 정의라 저장할 때마다 달라지지 않으므로 한 개만 유지합니다.
        /// </summary>
        public static string BuildCoordinateFileName(string partNo)
        {
            return MakeSafeFileNamePart(partNo) + "_coordinate.png";
        }

        /// <summary>
        /// 파일 이름에 쓸 수 없는 글자를 밑줄로 바꿉니다.
        /// </summary>
        public static string MakeSafeFileNamePart(string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "EMPTY" : value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                safeValue = safeValue.Replace(invalidCharacter, '_');
            }

            return safeValue;
        }
    }
}
