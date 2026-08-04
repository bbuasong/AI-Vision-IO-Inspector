using System.IO;

namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 기준 이미지 파생 파일의 이름을 프로젝트 전체에서 동일하게 생성합니다.
    /// </summary>
    public static class ReferenceImageFileNamePolicy
    {
        public const string LegacyCoordinateFileName = "coordinate.png";

        public static string BuildCoordinateFileName(string partNo)
        {
            string safePartNo = string.IsNullOrWhiteSpace(partNo) ? "EMPTY" : partNo.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                safePartNo = safePartNo.Replace(invalidCharacter, '_');
            }

            return safePartNo + "_coordinate.png";
        }
    }
}
