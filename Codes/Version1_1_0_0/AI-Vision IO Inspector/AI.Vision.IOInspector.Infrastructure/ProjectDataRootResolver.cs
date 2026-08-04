using System;
using System.IO;

namespace AI.Vision.IOInspector.Infrastructure
{
    /// <summary>
    /// Resolves the folder that owns runtime data such as SQLite DB, reference images, and logs.
    /// During Tests development this is the folder that contains AI.Vision.IOInspector.sln.
    /// </summary>
    public static class ProjectDataRootResolver
    {
        public static string Resolve(string startPath)
        {
            DirectoryInfo directory = BuildStartDirectory(startPath);
            while (directory != null)
            {
                string solutionPath = Path.Combine(directory.FullName, "AI.Vision.IOInspector.sln");
                if (File.Exists(solutionPath))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return AppContext.BaseDirectory;
        }

        private static DirectoryInfo BuildStartDirectory(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
            {
                return new DirectoryInfo(AppContext.BaseDirectory);
            }

            if (File.Exists(startPath))
            {
                string parentPath = Path.GetDirectoryName(startPath);
                if (!string.IsNullOrWhiteSpace(parentPath))
                {
                    return new DirectoryInfo(parentPath);
                }
            }

            return new DirectoryInfo(startPath);
        }
    }
}
