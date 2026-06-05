using System;
using System.IO;

namespace AI.Vision.IOInspector.Infrastructure.Services.Camera
{
    /// <summary>
    /// RTSP 프레임 캡처에 사용할 ffmpeg.exe 위치를 찾습니다.
    /// 배포 시에는 Native\FFmpeg\ffmpeg.exe에 넣는 것을 우선합니다.
    /// </summary>
    internal class FfmpegToolLocator
    {
        private readonly string _rootPath;

        public FfmpegToolLocator(string rootPath)
        {
            _rootPath = ProjectDataRootResolver.Resolve(rootPath);
        }

        public string FindFfmpegPath()
        {
            string nativePath = Path.Combine(_rootPath, "Native", "FFmpeg", "ffmpeg.exe");
            if (File.Exists(nativePath))
            {
                return nativePath;
            }

            string pathFromEnvironment = FindFromPathEnvironment();
            if (!string.IsNullOrWhiteSpace(pathFromEnvironment))
            {
                return pathFromEnvironment;
            }

            string ezCamPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MobSoft", "ezCam", "FFmpeg", "ffmpeg.exe");
            if (File.Exists(ezCamPath))
            {
                return ezCamPath;
            }

            return string.Empty;
        }

        private string FindFromPathEnvironment()
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return string.Empty;
            }

            string[] paths = pathValue.Split(Path.PathSeparator);
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                string candidate = Path.Combine(path.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }
    }
}
