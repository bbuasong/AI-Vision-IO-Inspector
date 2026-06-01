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
        private static bool _isConfigured;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(int directoryFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string newDirectory);

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

                TryEnableUserDllDirectories();
                foreach (string nativeDirectoryPath in nativeDirectoryPaths)
                {
                    RegisterDirectoryIfExists(nativeDirectoryPath);
                }

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
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "IMV", "x64"));
            nativeDirectoryPaths.Add(Path.Combine(dataRootPath, "Native", "AI", "x64"));
            return nativeDirectoryPaths;
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
