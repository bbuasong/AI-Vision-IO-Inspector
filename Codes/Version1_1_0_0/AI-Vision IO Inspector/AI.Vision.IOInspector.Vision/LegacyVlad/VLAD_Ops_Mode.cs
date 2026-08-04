namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// 기존 VLAD_Ops의 ACTION_MODE 상수를 그대로 유지합니다.
    /// 현재 프로젝트는 CAM 모드를 중심으로 사용하지만, 값 자체는 원본과 맞춥니다.
    /// </summary>
    public static class VLAD_Ops_Mode
    {
        public const int MODE_TYPE_MAP = 0;
        public const int MODE_TYPE_CAM = 1;
        public const int MODE_TYPE_FILE = 2;
        public const int MODE_TYPE_MOVIE = 3;
        public const int MODE_TYPE_MONITOR = 4;

        public static string GetRootName(int actionMode)
        {
            switch (actionMode)
            {
                case MODE_TYPE_MAP:
                    return "MAP";

                case MODE_TYPE_CAM:
                    return "CAM";

                case MODE_TYPE_FILE:
                    return "FILE";

                case MODE_TYPE_MOVIE:
                    return "MOVIE";

                case MODE_TYPE_MONITOR:
                    return "MONITOR";

                default:
                    return string.Empty;
            }
        }
    }
}
