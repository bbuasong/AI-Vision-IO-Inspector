using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AI.Vision.IOInspector.Vision.LegacyVlad
{
    /// <summary>
    /// VLAD 네이티브 호출 전에 프로세스가 바로 종료될 수 있는 런타임 결함만 진단합니다.
    /// 제품 기준 이미지 유무 같은 검사 업무 조건은 여기서 차단하지 않습니다.
    /// </summary>
    public static class VladRuntimePreflight
    {
        public static VladRuntimePreflightResult Inspect(VladVisionSettings settings)
        {
            VladRuntimePreflightResult result = new VladRuntimePreflightResult();

            if (settings == null)
            {
                result.AddBlockingIssue("VLAD 설정을 읽을 수 없습니다.");
                return result;
            }

            result.CudaBinPath = ResolveCudaBinPath();
            result.CudartPath = FindFirstLibrary("cudart64_110.dll", result.CudaBinPath);
            result.CublasPath = FindFirstLibrary("cublas64_11.dll", result.CudaBinPath);
            result.CublasLtPath = FindFirstLibrary("cublasLt64_11.dll", result.CudaBinPath);
            result.CudnnPath = FindFirstLibrary("cudnn64_8.dll", result.CudaBinPath);
            result.CudartAvailable = !string.IsNullOrWhiteSpace(result.CudartPath);
            result.CublasAvailable = !string.IsNullOrWhiteSpace(result.CublasPath);
            result.CublasLtAvailable = !string.IsNullOrWhiteSpace(result.CublasLtPath);
            result.CudnnAvailable = !string.IsNullOrWhiteSpace(result.CudnnPath);

            if (!result.CudartAvailable)
            {
                result.AddBlockingIssue("cudart64_110.dll을 찾을 수 없습니다. CUDA 11.0 bin 경로를 확인하세요.");
            }

            if (!result.CublasAvailable || !result.CublasLtAvailable)
            {
                result.AddBlockingIssue("cuBLAS DLL을 찾을 수 없습니다. CUDA 11.0 설치 또는 PATH 구성을 확인하세요.");
            }

            if (!result.CudnnAvailable)
            {
                result.AddBlockingIssue("cudnn64_8.dll을 찾을 수 없습니다. VLAD/TensorFlow GPU 런타임에 필요한 cuDNN 8.x DLL을 CUDA 11.0 bin, 실행 경로, 또는 AI_VISION_CUDNN_PATH/CUDNN_PATH 경로에 배치해야 합니다.");
            }

            result.ModelInspection = VladModelPathInspector.Inspect(settings.ModelPath);
            if (string.IsNullOrWhiteSpace(settings.ModelPath))
            {
                result.AddBlockingIssue("VLAD 모델 경로가 비어 있습니다.");
            }
            else if (!result.ModelInspection.PathExists)
            {
                result.AddBlockingIssue("VLAD 모델 경로를 찾을 수 없습니다. " + settings.ModelPath);
            }
            else if (!result.ModelInspection.IsLoadableCandidate)
            {
                result.AddBlockingIssue("현재 MODEL 경로에는 VLAD_SDK가 직접 로드할 추론 모델 파일이 없습니다. checkpoint 학습 산출물은 AI 담당자가 SavedModel/ONNX/PT/T7 추론 모델로 export해야 합니다.");
            }

            return result;
        }

        public static string BuildCudaDependencyMessage(VladRuntimePreflightResult result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            return "cudart64_110.dll=" + ExistsText(result.CudartAvailable) +
                   ", cublas64_11.dll=" + ExistsText(result.CublasAvailable) +
                   ", cublasLt64_11.dll=" + ExistsText(result.CublasLtAvailable) +
                   ", cudnn64_8.dll=" + ExistsText(result.CudnnAvailable) +
                   ", CUDA_BIN=" + result.CudaBinPath +
                   ", CUDNN_PATH=" + EmptyText(result.CudnnPath);
        }

        private static string ResolveCudaBinPath()
        {
            string cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH_V11_0");
            if (string.IsNullOrWhiteSpace(cudaPath))
            {
                cudaPath = Environment.GetEnvironmentVariable("CUDA_PATH");
            }

            if (string.IsNullOrWhiteSpace(cudaPath))
            {
                string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                cudaPath = Path.Combine(programFilesPath, "NVIDIA GPU Computing Toolkit", "CUDA", "v11.0");
            }

            return cudaPath.EndsWith("bin", StringComparison.OrdinalIgnoreCase)
                ? cudaPath
                : Path.Combine(cudaPath, "bin");
        }

        private static string FindFirstLibrary(string libraryFileName, string preferredDirectoryPath)
        {
            IList<string> searchDirectories = BuildLibrarySearchDirectories(preferredDirectoryPath);
            foreach (string searchDirectory in searchDirectories)
            {
                if (string.IsNullOrWhiteSpace(searchDirectory))
                {
                    continue;
                }

                string libraryPath = Path.Combine(searchDirectory, libraryFileName);
                if (File.Exists(libraryPath))
                {
                    return libraryPath;
                }
            }

            return string.Empty;
        }

        private static IList<string> BuildLibrarySearchDirectories(string preferredDirectoryPath)
        {
            IList<string> searchDirectories = new List<string>();
            AddUniqueDirectoryPath(searchDirectories, preferredDirectoryPath);
            AddUniqueDirectoryPath(searchDirectories, AppContext.BaseDirectory);
            AddUniqueDirectoryPath(searchDirectories, Path.Combine(AppContext.BaseDirectory, "Native", "VLAD"));
            AddCuDnnRuntimeDirectoryPath(searchDirectories, Environment.GetEnvironmentVariable("AI_VISION_CUDNN_PATH"));
            AddCuDnnRuntimeDirectoryPath(searchDirectories, Environment.GetEnvironmentVariable("AI_VISION_CUDNN_PATH", EnvironmentVariableTarget.User));
            AddCuDnnRuntimeDirectoryPath(searchDirectories, Environment.GetEnvironmentVariable("AI_VISION_CUDNN_PATH", EnvironmentVariableTarget.Machine));
            AddCuDnnRuntimeDirectoryPath(searchDirectories, Environment.GetEnvironmentVariable("CUDNN_PATH"));
            AddCuDnnRuntimeDirectoryPath(searchDirectories, Environment.GetEnvironmentVariable("CUDNN_PATH", EnvironmentVariableTarget.User));
            AddCuDnnRuntimeDirectoryPath(searchDirectories, Environment.GetEnvironmentVariable("CUDNN_PATH", EnvironmentVariableTarget.Machine));
            AddProcessPathDirectories(searchDirectories);

            string programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFilesPath))
            {
                AddCuDnnInstallDirectories(searchDirectories, Path.Combine(programFilesPath, "NVIDIA", "CUDNN"));
                AddCuDnnInstallDirectories(searchDirectories, Path.Combine(programFilesPath, "NVIDIA GPU Computing Toolkit", "CUDNN"));
            }

            return searchDirectories;
        }

        private static void AddProcessPathDirectories(IList<string> searchDirectories)
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
            {
                return;
            }

            string[] pathItems = pathValue.Split(Path.PathSeparator);
            foreach (string pathItem in pathItems)
            {
                AddUniqueDirectoryPath(searchDirectories, pathItem);
            }
        }

        private static void AddCuDnnRuntimeDirectoryPath(IList<string> searchDirectories, string cudnnRootPath)
        {
            if (string.IsNullOrWhiteSpace(cudnnRootPath))
            {
                return;
            }

            string normalizedPath = cudnnRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            AddUniqueDirectoryPath(searchDirectories, normalizedPath);
            if (!string.Equals(Path.GetFileName(normalizedPath), "bin", StringComparison.OrdinalIgnoreCase))
            {
                AddUniqueDirectoryPath(searchDirectories, Path.Combine(normalizedPath, "bin"));
            }
        }

        private static void AddCuDnnInstallDirectories(IList<string> searchDirectories, string cudnnRootPath)
        {
            if (string.IsNullOrWhiteSpace(cudnnRootPath) || !Directory.Exists(cudnnRootPath))
            {
                return;
            }

            AddCuDnnRuntimeDirectoryPath(searchDirectories, cudnnRootPath);

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
                AddCuDnnRuntimeDirectoryPath(searchDirectories, versionDirectory);
            }
        }

        private static void AddUniqueDirectoryPath(IList<string> searchDirectories, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            string normalizedDirectoryPath = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (string searchDirectory in searchDirectories)
            {
                string normalizedSearchDirectory = searchDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(normalizedSearchDirectory, normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            searchDirectories.Add(directoryPath);
        }

        private static string ExistsText(bool exists)
        {
            return exists ? "FOUND" : "MISSING";
        }

        private static string EmptyText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(empty)" : value;
        }
    }

    public class VladRuntimePreflightResult
    {
        private readonly IList<string> _blockingIssues = new List<string>();

        public string CudaBinPath { get; set; }

        public string CudartPath { get; set; }

        public string CublasPath { get; set; }

        public string CublasLtPath { get; set; }

        public string CudnnPath { get; set; }

        public bool CudartAvailable { get; set; }

        public bool CublasAvailable { get; set; }

        public bool CublasLtAvailable { get; set; }

        public bool CudnnAvailable { get; set; }

        public VladModelPathInspection ModelInspection { get; set; }

        public bool CanCallNative
        {
            get { return _blockingIssues.Count == 0; }
        }

        public IList<string> BlockingIssues
        {
            get { return _blockingIssues; }
        }

        public void AddBlockingIssue(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                _blockingIssues.Add(message);
            }
        }

        public string BuildBlockingMessage()
        {
            if (_blockingIssues.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("VLAD 네이티브 초기화를 건너뜁니다. 현재 환경에서 호출하면 Worker가 종료될 수 있는 필수 항목이 누락되었습니다.");
            for (int index = 0; index < _blockingIssues.Count; index++)
            {
                builder.Append(index + 1);
                builder.Append(". ");
                builder.AppendLine(_blockingIssues[index]);
            }

            return builder.ToString().Trim();
        }
    }
}
