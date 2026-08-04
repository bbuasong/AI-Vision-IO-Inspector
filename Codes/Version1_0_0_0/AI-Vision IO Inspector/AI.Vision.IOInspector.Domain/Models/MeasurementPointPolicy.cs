namespace AI.Vision.IOInspector.Domain.Models
{
    /// <summary>
    /// 측정부 개수와 기본 표시 색상을 모든 프로젝트에서 동일하게 사용하기 위한 정책입니다.
    /// </summary>
    public static class MeasurementPointPolicy
    {
        public const int MaxCount = 5;

        private static readonly string[] DefaultColors =
        {
            "#E53935",
            "#FB8C00",
            "#FDD835",
            "#43A047",
            "#1E88E5"
        };

        public static string GetDefaultColor(int indexNo)
        {
            int colorIndex = indexNo <= 0 ? 0 : (indexNo - 1) % DefaultColors.Length;
            return DefaultColors[colorIndex];
        }
    }
}
