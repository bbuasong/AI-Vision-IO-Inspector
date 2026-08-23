using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 기준 이미지 파일의 이름을 프로젝트 전체에서 동일하게 생성합니다.
    ///
    /// <para>
    /// 형식은 [순번_방향][벌번호][품번]_저장시각.png 입니다.
    ///   예) [01_Top][001][01100-51430]_20260819-103015.png
    /// </para>
    ///
    /// <para>
    /// 벌 번호는 부품마다 1부터 세며 저장할 때마다 하나씩 늘어납니다.
    /// 시각만으로도 벌을 구분할 수 있지만, 번호가 있으면 몇 번째로 저장한 것인지
    /// 파일 목록에서 바로 읽히고 화면의 벌 목록과도 같은 이름으로 맞출 수 있습니다.
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

        private static readonly Regex SavedImageFileNamePattern = new Regex(
            @"^\[(?<order>\d{2})_(?<view>[A-Za-z]+)\]\[(?<setNo>\d+)\]\[[^\]]+\]_(?<savedAt>\d{8}-\d{6})(?<extension>\.[^.]+)$",
            RegexOptions.CultureInvariant);

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
            int setNo,
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
                   "[" + BuildSetNoText(setNo) + "]" +
                   "[" + MakeSafeFileNamePart(partNo) + "]" +
                   "_" + savedAt.ToString(SavedAtFormat, CultureInfo.InvariantCulture) +
                   safeExtension;
        }

        /// <summary>
        /// 현재 기준 이미지 파일명에서 카메라 방향, 저장 벌 번호, 저장 시각을 읽습니다.
        /// DB에는 최신 한 벌만 보관하므로, 기준 이미지 팝업은 이 정보를 이용해
        /// 실제 이미지 폴더에 쌓인 이전 벌까지 함께 표시합니다.
        /// </summary>
        public static bool TryParseSavedImageFileName(
            string fileName,
            out ImageViewType viewType,
            out int setNo,
            out DateTime savedAt)
        {
            viewType = ImageViewType.Top;
            setNo = 0;
            savedAt = DateTime.MinValue;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            Match match = SavedImageFileNamePattern.Match(Path.GetFileName(fileName));
            if (!match.Success ||
                !Enum.TryParse(match.Groups["view"].Value, out viewType) ||
                !int.TryParse(match.Groups["setNo"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out setNo) ||
                setNo < 1)
            {
                return false;
            }

            return DateTime.TryParseExact(
                match.Groups["savedAt"].Value,
                SavedAtFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out savedAt);
        }

        /// <summary>
        /// 벌 번호를 세 자리 문자열로 만듭니다. 첫 벌이 001입니다.
        /// 세 자리를 넘으면 그대로 늘어납니다(1000번째 벌은 1000).
        /// </summary>
        /// <summary>
        /// 아직 벌이 정해지지 않은 임시 이미지의 벌 번호 자리입니다.
        ///
        /// <para>
        /// Temp 에 놓이는 동안에는 몇 번째 벌이 될지 알 수 없습니다. 벌 번호는 DB 에 저장할 때
        /// 정해지기 때문입니다. 그래서 그 자리를 000 으로 비워 둡니다. 파일명 읽는 규칙이
        /// 숫자를 기대하므로 글자 대신 숫자로 비웁니다.
        /// </para>
        /// </summary>
        public const int TemporarySetNo = 0;

        /// <summary>
        /// Temp 폴더에 놓을 기준 이미지의 파일 이름을 만듭니다.
        ///
        /// <para>
        /// 최종 이름과 같은 규칙을 씁니다. 예전에는 품번_방향 만 붙여 다시 찍어도 이름이 같았고,
        /// 그래서 어느 것이 방금 찍은 것인지 파일만 봐서는 알 수 없었습니다. 예전 폴더에 남은
        /// 같은 이름의 파일과도 구분되지 않았습니다. 시각을 넣어 매번 다른 이름이 되게 합니다.
        /// </para>
        /// </summary>
        public static string BuildTemporaryImageFileName(
            ImageViewType viewType,
            string partNo,
            DateTime savedAt,
            string extension)
        {
            return BuildImageFileName(viewType, TemporarySetNo, partNo, savedAt, extension);
        }

        /// <summary>
        /// 그 카메라의 파일임을 알아보는 앞머리입니다. 예) [01_Top]
        /// </summary>
        public static string BuildViewPrefix(ImageViewType viewType)
        {
            return "[" + BuildViewOrderText(viewType) + "_" + viewType.ToString() + "]";
        }

        public static string BuildSetNoText(int setNo)
        {
            // 0 은 아직 벌이 정해지지 않은 임시 이미지라는 뜻이라 그대로 둡니다.
            int safeSetNo = setNo < 0 ? 1 : setNo;
            return safeSetNo.ToString("000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 화면의 벌 목록에 적을 이름입니다.
        ///   예) [003] 2026-08-19 10:30:15
        /// </summary>
        public static string BuildSetDisplayName(int setNo, DateTime savedAt)
        {
            return "[" + BuildSetNoText(setNo) + "] " +
                   savedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 다음에 저장할 벌의 번호를 구합니다. 기존에 없으면 1입니다.
        ///
        /// <para>
        /// 이미 있는 벌 번호 중 가장 큰 값에 하나를 더합니다. 중간 이미지를 지우는 일이 없으므로
        /// 번호는 계속 늘어나기만 하고 비어 있는 번호가 생기지 않습니다.
        /// </para>
        /// </summary>
        /// <summary>
        /// 그 카메라에서 가장 최근에 저장한 벌의 이미지를 고릅니다.
        ///
        /// <para>
        /// 기준 이미지는 저장할 때마다 벌이 하나씩 쌓입니다. 목록은 저장한 차례대로 담겨 오므로
        /// 앞에서부터 찾으면 <b>가장 오래된 벌</b>이 잡힙니다. 검사와 통합 이미지는 최신 기준으로
        /// 견주어야 하므로 회차가 가장 큰 것을 고릅니다.
        /// </para>
        ///
        /// <para>
        /// 회차가 같으면 나중에 담긴 것을 씁니다. 옛 자료에는 회차가 채워지지 않은 것이 있어
        /// 그때는 목록 순서가 유일한 단서입니다.
        /// </para>
        /// </summary>
        public static PartImage FindLatestByViewType(IEnumerable<PartImage> images, ImageViewType viewType)
        {
            if (images == null)
            {
                return null;
            }

            PartImage latest = null;
            foreach (PartImage image in images)
            {
                if (image == null || image.ViewType != viewType)
                {
                    continue;
                }

                if (latest == null || image.SetNo >= latest.SetNo)
                {
                    latest = image;
                }
            }

            return latest;
        }

        public static int ResolveNextSetNo(IEnumerable<PartImage> images)
        {
            int maxSetNo = 0;
            if (images != null)
            {
                foreach (PartImage image in images)
                {
                    if (image == null || image.IsTemporary)
                    {
                        continue;
                    }

                    if (image.SetNo > maxSetNo)
                    {
                        maxSetNo = image.SetNo;
                    }
                }
            }

            return maxSetNo + 1;
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
        ///   예) [01_Top][01100-51430]_coordinate.png
        ///
        /// <para>
        /// 측정부를 카메라마다 따로 관리하므로 좌표 이미지도 카메라마다 한 장씩 둡니다.
        /// 기준 이미지와 같은 자리에 같은 규칙으로 놓이도록 앞머리를 맞췄습니다.
        /// 좌표는 부품의 측정부 정의라 저장할 때마다 달라지지 않으므로,
        /// 기준 이미지와 달리 벌 번호와 시각은 붙이지 않습니다.
        /// </para>
        /// </summary>
        public static string BuildCoordinateFileName(ImageViewType viewType, string partNo)
        {
            return "[" + BuildViewOrderText(viewType) + "_" + viewType.ToString() + "]" +
                   "[" + MakeSafeFileNamePart(partNo) + "]" +
                   "_coordinate.png";
        }

        /// <summary>
        /// 이 카메라의 좌표 이미지를 폴더에서 찾습니다. 없으면 빈 문자열입니다.
        ///
        /// <para>
        /// 이름 규칙이 두 번 바뀌었습니다. 이미 만들어 둔 파일이 계속 보이도록 옛 이름도 함께 봅니다.
        ///   1) [01_Top][품번]_coordinate.png   지금 규칙
        ///   2) 품번_coordinate.png             카메라를 나누기 전 (Thickness 전용)
        ///   3) coordinate.png                  품번을 붙이기 전
        /// 옛 이름은 Thickness에서만 찾습니다. 그때는 Thickness 말고는 측정부가 없었습니다.
        /// </para>
        /// </summary>
        public static string FindCoordinateFilePath(string folderPath, ImageViewType viewType, string partNo)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return string.Empty;
            }

            string currentPath = Path.Combine(folderPath, BuildCoordinateFileName(viewType, partNo));
            if (File.Exists(currentPath))
            {
                return currentPath;
            }

            if (viewType != ImageViewType.Thickness)
            {
                return string.Empty;
            }

            string legacyPath = Path.Combine(folderPath, BuildLegacyCoordinateFileName(partNo));
            if (File.Exists(legacyPath))
            {
                return legacyPath;
            }

            string oldestPath = Path.Combine(folderPath, LegacyCoordinateFileName);
            if (File.Exists(oldestPath))
            {
                return oldestPath;
            }

            return string.Empty;
        }

        /// <summary>
        /// 카메라를 나누기 전에 쓰던 좌표 이미지 이름입니다.
        /// 그때는 Thickness 하나뿐이라 카메라를 적지 않았습니다.
        /// 이미 있는 파일을 계속 읽기 위해 남겨 둡니다.
        /// </summary>
        public static string BuildLegacyCoordinateFileName(string partNo)
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
