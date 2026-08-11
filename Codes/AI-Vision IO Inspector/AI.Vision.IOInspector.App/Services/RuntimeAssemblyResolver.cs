using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AI.Vision.IOInspector.App.Services
{
    /// <summary>
    /// 실행 폴더 하위 Native\VLAD에 배치된 관리 DLL과 네이티브 DLL을 앱 시작 초기에 찾을 수 있도록 등록합니다.
    /// .NET Framework는 기본적으로 하위 폴더의 관리 어셈블리를 자동 검색하지 않기 때문에 OpenCvSharp.dll 같은 참조 DLL을 직접 연결합니다.
    /// </summary>
    internal static class RuntimeAssemblyResolver
    {
        private static readonly object SyncRoot = new object();
        private static bool _isRegistered;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        public static void Register()
        {
            lock (SyncRoot)
            {
                if (_isRegistered)
                {
                    return;
                }

                string nativeVladPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Native", "VLAD");
                RegisterNativeSearchPath(nativeVladPath);
                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssemblyFromNativeVlad;
                _isRegistered = true;
            }
        }

        private static Assembly ResolveAssemblyFromNativeVlad(object sender, ResolveEventArgs args)
        {
            AssemblyName assemblyName = new AssemblyName(args.Name);
            string assemblyPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Native",
                "VLAD",
                assemblyName.Name + ".dll");

            if (!File.Exists(assemblyPath))
            {
                return null;
            }

            return Assembly.LoadFrom(assemblyPath);
        }

        private static void RegisterNativeSearchPath(string nativeVladPath)
        {
            if (string.IsNullOrWhiteSpace(nativeVladPath) || !Directory.Exists(nativeVladPath))
            {
                return;
            }

            try
            {
                SetDllDirectory(nativeVladPath);
            }
            catch
            {
                // DLL 탐색 경로 등록 실패가 앱 시작 자체를 막으면 안 됩니다. 이후 실제 SDK 호출 시 상세 오류를 확인합니다.
            }

            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string normalizedNativePath = nativeVladPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] pathItems = currentPath.Split(Path.PathSeparator);

            foreach (string pathItem in pathItems)
            {
                string normalizedPathItem = pathItem.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(normalizedPathItem, normalizedNativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(currentPath))
            {
                Environment.SetEnvironmentVariable("PATH", nativeVladPath);
                return;
            }

            Environment.SetEnvironmentVariable("PATH", nativeVladPath + Path.PathSeparator + currentPath);
        }
    }
}
