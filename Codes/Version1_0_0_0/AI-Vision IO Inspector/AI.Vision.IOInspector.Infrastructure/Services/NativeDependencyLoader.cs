using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// Registers app-local native DLL folders before camera or AI SDK calls are made.
    /// This keeps the deployed program independent from Visual Studio install paths.
    /// </summary>
    public static class NativeDependencyLoader
    {
        private const int LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const int LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

        private static readonly object SyncRoot = new object();
        private static readonly IList<IntPtr> DirectoryCookies = new List<IntPtr>();
        private static readonly IList<IntPtr> PreloadedLibraryHandles = new List<IntPtr>();
        private static bool _isConfigured;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(int directoryFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string newDirectory);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string libraryFileName);

        /// <summary>
        /// Adds the expected native runtime folders to the current process DLL search path.
        /// Missing folders are ignored so the app can still run in simulated camera mode.
        /// </summary>
        public static void Configure(string applicationRootPath)
        {
            lock (SyncRoot)
            {
                if (_isConfigured)
                {
                    return;
                }

                string dataRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
                IList<string> nativeDirectoryPaths = BuildNativeDirectoryPaths(dataRootPath);

                // VLAD_SDK 샘플은 SetDefaultDllDirectories를 사용하지 않고 기존 Windows DLL 검색 흐름으로 동작합니다.
                // 이 API를 켜면 current directory와 PATH 기반 탐색이 제한되어 VLAD 내부 지연 로딩 DLL 탐색 순서가 달라질 수 있습니다.
                foreach (string nativeDirectoryPath in nativeDirectoryPaths)
                {
                    RegisterDirectoryIfExists(nativeDirectoryPath);
                }

                // CUDA/cuDNN DLL은 TensorFlow/VLAD가 실제로 필요할 때 직접 로드하도록 둡니다.
                // 선로딩은 샘플과 다른 초기화 순서를 만들고, GPU 런타임 충돌 원인 추적을 어렵게 합니다.
                ConfigureVlcPluginPath(dataRootPath);
                _isConfigured = true;
            }
        }

        /// <summary>
        /// Returns the deployment folders that should contain vendor/native DLLs.
        /// The method is kept public so diagnostics or an Option UI can show the expected paths.
        /// </summary>
        public static IList<string> BuildNativeDirectoryPaths(string dataRootPath)
        {
            IList<string> nativeDirectoryPaths = new List<string>();
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "VLAD"));
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "VLAD", "plugins"));
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "VLAD", "dll"));
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "VLAD", "libvlc"));
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "IMV", "x64"));
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "AI", "x64"));
            AddCudaRuntimeDirectoryPaths(nativeDirectoryPaths);
            AddCuDnnRuntimeDirectoryPaths(nativeDirectoryPaths);
            return nativeDirectoryPaths;
        }

        /// <summary>
        /// VLAD 내부 TensorFlow가 CUDA 11.0 런타임 DLL을 찾을 수 있도록 CUDA bin 폴더를 추가합니다.
        /// 설치 직후 IDE를 재시작하지 않아도 Machine 환경변수를 읽어 보강합니다.
        /// </summary>
        private static void AddCudaRuntimeDirectoryPaths(IList<string> nativeDirectoryPaths)
        {
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("AI_VISION_CUDA_PATH"));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("AI_VISION_CUDA_PATH", EnvironmentVariableTarget.User));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("AI_VISION_CUDA_PATH", EnvironmentVariableTarget.Machine));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDA_PATH_V11_0"));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDA_PATH_V11_0", EnvironmentVariableTarget.User));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDA_PATH_V11_0", EnvironmentVariableTarget.Machine));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDA_PATH"));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDA_PATH", EnvironmentVariableTarget.User));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDA_PATH", EnvironmentVariableTarget.Machine));

            string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFilesPath))
            {
                AddCudaRuntimeDirectoryPath(
                    nativeDirectoryPaths,
                    Path.Combine(programFilesPath, "NVIDIA GPU Computing Toolkit", "CUDA", "v11.0"));
            }
        }

        private static void AddCudaRuntimeDirectoryPath(IList<string> nativeDirectoryPaths, string cudaRootPath)
        {
            if (string.IsNullOrWhiteSpace(cudaRootPath))
            {
                return;
            }

            string normalizedCudaPath = cudaRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string cudaBinPath = normalizedCudaPath;
            if (!string.Equals(Path.GetFileName(normalizedCudaPath), "bin", StringComparison.OrdinalIgnoreCase))
            {
                cudaBinPath = Path.Combine(normalizedCudaPath, "bin");
            }

            AddUniqueDirectoryPath(nativeDirectoryPaths, cudaBinPath);
        }

        /// <summary>
        /// cuDNN은 CUDA Toolkit에 포함되지 않고 별도 설치되는 경우가 많으므로 cuDNN 전용 경로도 추가합니다.
        /// </summary>
        private static void AddCuDnnRuntimeDirectoryPaths(IList<string> nativeDirectoryPaths)
        {
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("AI_VISION_CUDNN_PATH"));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("AI_VISION_CUDNN_PATH", EnvironmentVariableTarget.User));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("AI_VISION_CUDNN_PATH", EnvironmentVariableTarget.Machine));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDNN_PATH"));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDNN_PATH", EnvironmentVariableTarget.User));
            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, Environment.GetEnvironmentVariable("CUDNN_PATH", EnvironmentVariableTarget.Machine));

            string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (string.IsNullOrWhiteSpace(programFilesPath))
            {
                return;
            }

            AddCuDnnInstallDirectories(nativeDirectoryPaths, Path.Combine(programFilesPath, "NVIDIA", "CUDNN"));
            AddCuDnnInstallDirectories(nativeDirectoryPaths, Path.Combine(programFilesPath, "NVIDIA GPU Computing Toolkit", "CUDNN"));
        }

        private static void AddCuDnnInstallDirectories(IList<string> nativeDirectoryPaths, string cudnnRootPath)
        {
            if (string.IsNullOrWhiteSpace(cudnnRootPath) || !Directory.Exists(cudnnRootPath))
            {
                return;
            }

            AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, cudnnRootPath);

            string[] versionDirectories;
            try
            {
                versionDirectories = Directory.GetDirectories(cudnnRootPath);
            }
            catch
            {
                return;
            }

            foreach (string versionDirectory in versionDirectories)
            {
                AddCudaRuntimeDirectoryPath(nativeDirectoryPaths, versionDirectory);
            }
        }

        private static void AddUniqueDirectoryPath(IList<string> directoryPaths, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            string normalizedDirectoryPath = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string currentDirectoryPath in directoryPaths)
            {
                string normalizedCurrentDirectoryPath = currentDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(normalizedCurrentDirectoryPath, normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            directoryPaths.Add(directoryPath);
        }

        private static void TryEnableUserDllDirectories()
        {
            try
            {
                SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);
            }
            catch (EntryPointNotFoundException)
            {
                // Older Windows versions may not support this API. PATH fallback below still helps.
            }
        }

        private static void RegisterDirectoryIfExists(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            try
            {
                IntPtr cookie = AddDllDirectory(directoryPath);
                if (cookie != IntPtr.Zero)
                {
                    DirectoryCookies.Add(cookie);
                }
            }
            catch (EntryPointNotFoundException)
            {
                // PATH fallback is used when AddDllDirectory is unavailable.
            }

            AppendProcessPath(directoryPath);
        }

        /// <summary>
        /// TensorFlow가 내부 로더에서 CUDA 런타임을 찾기 전에 핵심 CUDA DLL을 먼저 로드합니다.
        /// CUDA가 없는 PC에서는 조용히 건너뛰고, 실제 추론 단계에서 남은 의존성 오류를 확인합니다.
        /// </summary>
        private static void PreloadCudaRuntimeIfAvailable(IList<string> nativeDirectoryPaths)
        {
            string[] cudaRuntimeFileNames =
            {
                "cudart64_110.dll",
                "cublas64_11.dll",
                "cublasLt64_11.dll",
                "cudnn64_8.dll"
            };

            foreach (string cudaRuntimeFileName in cudaRuntimeFileNames)
            {
                PreloadFirstExistingLibrary(nativeDirectoryPaths, cudaRuntimeFileName);
            }
        }

        private static void PreloadFirstExistingLibrary(IList<string> directoryPaths, string libraryFileName)
        {
            foreach (string directoryPath in directoryPaths)
            {
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    continue;
                }

                string libraryPath = Path.Combine(directoryPath, libraryFileName);
                if (!File.Exists(libraryPath))
                {
                    continue;
                }

                try
                {
                    IntPtr libraryHandle = LoadLibrary(libraryPath);
                    if (libraryHandle != IntPtr.Zero)
                    {
                        PreloadedLibraryHandles.Add(libraryHandle);
                        return;
                    }
                }
                catch
                {
                    return;
                }
            }
        }
        private static void ConfigureVlcPluginPath(string dataRootPath)
        {
            string pluginPath = Path.Combine(dataRootPath, "Native", "VLAD", "plugins");
            if (Directory.Exists(pluginPath))
            {
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginPath);
            }
        }

        private static void AppendProcessPath(string directoryPath)
        {
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string normalizedDirectoryPath = directoryPath.TrimEnd(Path.DirectorySeparatorChar);

            string[] pathItems = currentPath.Split(Path.PathSeparator);
            foreach (string pathItem in pathItems)
            {
                string normalizedPathItem = pathItem.TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(normalizedPathItem, normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            string newPath;
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                newPath = directoryPath;
            }
            else
            {
                newPath = directoryPath + Path.PathSeparator + currentPath;
            }

            Environment.SetEnvironmentVariable("PATH", newPath);
        }
    }
}
