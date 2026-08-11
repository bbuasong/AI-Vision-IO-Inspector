using System;
using System.IO;

namespace AI.Vision.IOInspector.Infrastructure
{
    /// <summary>
    /// 배포 실행 파일과 같은 위치의 CFG 폴더에서 런타임 JSON 설정 파일을 찾습니다.
    /// 개발 프로젝트 루트나 현재 작업 폴더를 탐색하지 않아 배포본과 개발본의 설정이 섞이지 않도록 합니다.
    /// </summary>
    public static class RuntimeConfigurationPathResolver
    {
        /// <summary>
        /// 현재 실행 중인 EXE의 폴더를 반환합니다.
        /// </summary>
        public static string GetExecutableDirectoryPath()
        {
            string baseDirectoryPath = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectoryPath))
            {
                baseDirectoryPath = AppDomain.CurrentDomain.BaseDirectory;
            }

            return Path.GetFullPath(baseDirectoryPath);
        }

        /// <summary>
        /// 지정한 런타임 JSON 설정의 절대 경로를 반환합니다.
        /// 모든 런타임 JSON은 EXE\\CFG 아래에서만 읽고 저장합니다.
        /// </summary>
        public static string GetConfigFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("설정 파일 이름이 필요합니다.", "fileName");
            }

            return Path.Combine(GetExecutableDirectoryPath(), "CFG", fileName.Trim());
        }
    }
}
