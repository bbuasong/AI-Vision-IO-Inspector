using System;
using System.Globalization;
using System.IO;
using AI.Vision.IOInspector.Domain.Enums;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 검사 결과 이미지의 폴더 이름과 파일 이름을 프로젝트 전체에서 동일하게 생성합니다.
    ///
    /// 2026-08-12 규칙입니다.
    ///   폴더   : {품번폴더}\HH-mm-ss                       검사 시작 시각 기준. 한 번의 검사 이미지는 모두 같은 폴더에 모입니다.
    ///   원본   : [1_Top]품번_품명_HH-mm-ss-fff.png
    ///   결과본 : [1_Top][Result]품번_품명_HH-mm-ss-fff.png
    ///
    /// 방향 앞에 번호를 붙이는 이유는 탐색기에서 이름순으로 정렬했을 때
    /// 화면 표시 순서(Top -> Front -> Back -> Left -> Right -> Thickness)와 같아지도록 하기 위해서입니다.
    /// 번호가 없으면 Back, Front, Left, Right, Thickness, Top 순으로 서서 Top이 맨 뒤로 갑니다.
    ///
    /// 시각은 폴더와 파일 이름 모두 검사 시작 시각 하나를 사용합니다.
    /// 채널별 촬영 시각을 쓰면 초가 넘어가는 순간 6장이 서로 다른 폴더로 갈라집니다.
    /// </summary>
    public static class InspectionImageFileNamePolicy
    {
        /// <summary>결과 기록용 복사본임을 나타내는 태그입니다.</summary>
        public const string ResultTag = "Result";

        /// <summary>측정부 좌표가 표시된 이미지임을 나타내는 태그입니다.</summary>
        public const string CoordinateTag = "Coordinate";

        private const string FolderTimeFormat = "HH-mm-ss";
        private const string FileTimeFormat = "HH-mm-ss-fff";

        /// <summary>
        /// 검사 시작 시각으로 검사 단위 폴더 이름을 만듭니다. 예: 16-30-17
        /// </summary>
        public static string BuildInspectionFolderName(DateTime inspectionStartedAt)
        {
            return inspectionStartedAt.ToString(FolderTimeFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 방향 접두어를 만듭니다. 예: [1_Top]
        /// 번호는 ImageViewType 선언 순서(Top=0)에 1을 더한 값이며, 화면 표시 순서와 같습니다.
        /// </summary>
        public static string BuildViewPrefix(ImageViewType viewType)
        {
            return "[" + GetViewOrder(viewType).ToString(CultureInfo.InvariantCulture) + "_" + viewType + "]";
        }

        /// <summary>
        /// 검사 원본 이미지 파일 이름을 만듭니다.
        /// 예: [1_Top]0445120236_INJECTOR-FUEL_16-30-17-211.png
        /// </summary>
        public static string BuildCaptureFileName(
            ImageViewType viewType,
            string partNo,
            string partName,
            DateTime inspectionStartedAt,
            string extension)
        {
            return BuildFileName(viewType, null, partNo, partName, inspectionStartedAt, extension);
        }

        /// <summary>
        /// 결과 기록용 복사본 파일 이름을 만듭니다.
        /// 예: [1_Top][Result]0445120236_INJECTOR-FUEL_16-30-17-211.png
        /// 원본과 이름이 이어지므로 정렬했을 때 두 파일이 나란히 놓입니다.
        /// </summary>
        public static string BuildResultFileName(
            ImageViewType viewType,
            string partNo,
            string partName,
            DateTime inspectionStartedAt,
            string extension)
        {
            return BuildFileName(viewType, ResultTag, partNo, partName, inspectionStartedAt, extension);
        }

        /// <summary>
        /// 측정부 좌표 이미지(coordinate)의 결과 기록본 파일 이름을 만듭니다.
        /// 예: [6_Thickness][Coordinate][Result]0445120236_INJECTOR-FUEL_16-30-17-211.png
        ///
        /// 측정부가 등록된 품목은 검사에서 Thickness 촬영본과 coordinate 이미지를 함께 다루므로
        /// 결과본도 두 개가 나옵니다. Coordinate 태그로 어느 쪽 기준인지 구분합니다.
        /// </summary>
        public static string BuildCoordinateResultFileName(
            ImageViewType viewType,
            string partNo,
            string partName,
            DateTime inspectionStartedAt,
            string extension)
        {
            string safePartNo = Sanitize(partNo, "UNKNOWN_PARTNO");
            string safePartName = Sanitize(partName, "UNKNOWN_PARTNAME");
            string timeSegment = inspectionStartedAt.ToString(FileTimeFormat, CultureInfo.InvariantCulture);

            return BuildViewPrefix(viewType) +
                   "[" + CoordinateTag + "]" +
                   "[" + ResultTag + "]" +
                   safePartNo + "_" + safePartName + "_" + timeSegment +
                   NormalizeExtension(extension);
        }

        /// <summary>
        /// 원본 파일 경로에서 같은 폴더의 결과 기록본 경로를 만듭니다.
        /// 접두어 규칙을 따르지 않는 예전 파일이면 이름 뒤에 태그를 붙여 되돌려 줍니다.
        /// </summary>
        public static string BuildResultFilePathFromCapturePath(string captureFilePath, ImageViewType viewType)
        {
            if (string.IsNullOrWhiteSpace(captureFilePath))
            {
                return string.Empty;
            }

            string directoryPath = Path.GetDirectoryName(captureFilePath);
            string fileName = Path.GetFileName(captureFilePath);
            string viewPrefix = BuildViewPrefix(viewType);
            string resultFileName;

            if (fileName.StartsWith(viewPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // [1_Top]이름.png -> [1_Top][Result]이름.png
                resultFileName = viewPrefix + "[" + ResultTag + "]" + fileName.Substring(viewPrefix.Length);
            }
            else
            {
                // 접두어가 없는 예전 파일은 이름 앞에 두 태그를 모두 붙입니다.
                resultFileName = viewPrefix + "[" + ResultTag + "]" + fileName;
            }

            return string.IsNullOrWhiteSpace(directoryPath)
                ? resultFileName
                : Path.Combine(directoryPath, resultFileName);
        }

        /// <summary>
        /// 화면 표시 순서와 같은 방향 번호입니다. Top=1 ... Thickness=6, 미분류=7
        /// </summary>
        public static int GetViewOrder(ImageViewType viewType)
        {
            return (int)viewType + 1;
        }

        private static string BuildFileName(
            ImageViewType viewType,
            string tag,
            string partNo,
            string partName,
            DateTime inspectionStartedAt,
            string extension)
        {
            string safePartNo = Sanitize(partNo, "UNKNOWN_PARTNO");
            string safePartName = Sanitize(partName, "UNKNOWN_PARTNAME");
            string tagSegment = string.IsNullOrWhiteSpace(tag) ? string.Empty : "[" + tag + "]";
            string timeSegment = inspectionStartedAt.ToString(FileTimeFormat, CultureInfo.InvariantCulture);

            return BuildViewPrefix(viewType) +
                   tagSegment +
                   safePartNo + "_" + safePartName + "_" + timeSegment +
                   NormalizeExtension(extension);
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return ".png";
            }

            string normalized = extension.Trim();
            return normalized.StartsWith(".", StringComparison.Ordinal) ? normalized : "." + normalized;
        }

        private static string Sanitize(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string sanitized = value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }

            while (sanitized.Contains("  "))
            {
                sanitized = sanitized.Replace("  ", " ");
            }

            if (sanitized.Length > 80)
            {
                sanitized = sanitized.Substring(0, 80).Trim();
            }

            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }
    }
}
