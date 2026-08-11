using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AI.Vision.IOInspector.Infrastructure
{
    /// <summary>
    /// 프로그램 로그 파일의 저장 위치와 보관 기간을 한곳에서 정합니다.
    ///
    /// 저장 구조는 DB\Logs\{일자}\{로그이름}-{일자}-{프로그램 시작 시각}.log 입니다.
    ///   DB\Logs\20260810\app-startup-20260810-100419.log
    ///   DB\Logs\20260810\vlad-hd-json-20260810-100419.log
    ///   DB\Logs\20260811\app-startup-20260811-100419.log   (자정을 넘겨 계속 실행한 경우)
    ///
    /// - 폴더는 일자별로 나뉘므로 하루치 로그만 열게 되어 파일 열기가 느려지지 않습니다.
    /// - 파일 이름의 시각은 프로그램을 시작한 시각이라, 하루에 여러 번 실행해도 실행 회차별로 구분됩니다.
    /// - 자정을 넘겨 실행이 이어지면 다음 날 폴더에 새 파일이 만들어져 파일 하나가 무한히 커지지 않습니다.
    ///
    /// 이전에는 모든 로그가 DB\Logs 아래 파일 하나에 계속 덧붙기만 하고 삭제 기준이 없었습니다.
    /// </summary>
    public static class ApplicationLogFileResolver
    {
        /// <summary>
        /// 로그 보관 기간 기본값입니다. 이 기간이 지난 일자 폴더는 프로그램 시작 시 삭제합니다.
        /// </summary>
        public const int DefaultRetentionDays = 30;

        /// <summary>
        /// 한 번 실행에서 지울 로그 파일의 최대 개수입니다.
        /// 일자 폴더를 통째로 지우면 파일이 많거나 클 때 디스크 작업이 한꺼번에 몰리므로,
        /// 파일 단위로 이 개수까지만 지우고 나머지는 다음 실행으로 넘깁니다.
        /// </summary>
        public const int MaxDeletionsPerRun = 500;

        private const string LogRootFolderName = "Logs";
        private const string DataFolderName = "DB";
        private const string RetentionLogFolderName = "RetentionLog";
        private const string DateFolderFormat = "yyyyMMdd";

        /// <summary>
        /// 프로그램을 시작한 시각입니다. 같은 실행에서 만들어지는 모든 로그 파일이 이 시각을 공유하므로
        /// 파일 이름만 보고 어떤 실행에서 남은 로그인지 구분할 수 있습니다.
        /// </summary>
        private static readonly string SessionStartTime = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);

        /// <summary>
        /// 오늘 일자 폴더 아래의 로그 파일 전체 경로를 돌려주고 폴더를 만들어 둡니다.
        /// logName에는 확장자 없이 "app-startup"처럼 로그 종류만 넘깁니다.
        /// </summary>
        public static string GetLogFilePath(string applicationRootPath, string logName)
        {
            string directoryPath = GetLogDirectoryPath(applicationRootPath);
            return Path.Combine(directoryPath, BuildLogFileName(logName));
        }

        /// <summary>
        /// 오늘 일자 로그 폴더(DB\Logs\yyyyMMdd)를 만들고 경로를 돌려줍니다.
        /// </summary>
        public static string GetLogDirectoryPath(string applicationRootPath)
        {
            string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
            string directoryPath = Path.Combine(
                projectRootPath,
                DataFolderName,
                LogRootFolderName,
                DateTime.Now.ToString(DateFolderFormat, CultureInfo.InvariantCulture));
            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        /// <summary>
        /// 일자와 실행 시작 시각이 붙은 로그 파일 이름을 만듭니다.
        /// 이미 로그 폴더를 알고 있는 호출부가 같은 이름 규칙을 쓰도록 공개합니다.
        /// </summary>
        public static string BuildLogFileName(string logName)
        {
            string safeLogName = string.IsNullOrWhiteSpace(logName) ? "app" : logName.Trim();
            return safeLogName +
                   "-" +
                   DateTime.Now.ToString(DateFolderFormat, CultureInfo.InvariantCulture) +
                   "-" +
                   SessionStartTime +
                   ".log";
        }

        /// <summary>
        /// 보관 기간이 지난 로그를 삭제합니다. 프로그램 시작 시 한 번 호출합니다.
        /// 이름이 일자 형식(yyyyMMdd)으로 해석되는 항목만 지우므로, 형식이 다른 파일이나 폴더는 건드리지 않습니다.
        ///
        /// 삭제는 일자 폴더를 통째로 지우지 않고 <b>파일 하나씩</b> 진행하며, 한 번 실행에서
        /// MaxDeletionsPerRun개까지만 지웁니다. 오래 방치해 지울 파일이 수만 개 쌓여 있어도
        /// 프로그램 시작이 그만큼 느려지거나 디스크에 부하가 몰리지 않고, 남은 파일은 다음 실행에서 이어서 정리합니다.
        /// 가장 오래된 일자부터 지우므로 오래된 로그가 계속 남는 일은 없습니다.
        /// </summary>
        public static int CleanupExpiredLogs(string applicationRootPath, int retentionDays)
        {
            return CleanupExpiredLogs(applicationRootPath, retentionDays, MaxDeletionsPerRun);
        }

        /// <summary>
        /// 한 번에 지울 파일 수를 직접 정해 정리합니다. 실제로 지운 파일 수를 돌려줍니다.
        /// </summary>
        public static int CleanupExpiredLogs(string applicationRootPath, int retentionDays, int maxDeletionCount)
        {
            if (retentionDays <= 0 || maxDeletionCount <= 0)
            {
                return 0;
            }

            int remainingBudget = maxDeletionCount;
            try
            {
                string projectRootPath = ProjectDataRootResolver.Resolve(applicationRootPath);
                DateTime deleteBefore = DateTime.Now.Date.AddDays(-retentionDays);

                string logRootPath = Path.Combine(projectRootPath, DataFolderName, LogRootFolderName);
                DeleteExpiredDateFolders(logRootPath, deleteBefore, ref remainingBudget);

                // 일자별 폴더로 나누기 전에 쓰던 로그 파일이 DB\Logs 바로 아래 남아 있습니다.
                // 이름에 일자가 없으므로 마지막 기록 시각을 기준으로 정리합니다.
                DeleteExpiredLooseLogFiles(logRootPath, deleteBefore, ref remainingBudget);

                // 검사 데이터 삭제 이력은 RetentionLog\yyyyMMdd.log 파일로 남습니다.
                DeleteExpiredDateFiles(
                    Path.Combine(projectRootPath, RetentionLogFolderName),
                    deleteBefore,
                    ref remainingBudget);
            }
            catch
            {
                // 로그 정리 실패가 프로그램 시작을 막으면 안 됩니다.
            }

            return maxDeletionCount - remainingBudget;
        }

        /// <summary>
        /// 보관 기간이 지난 일자 폴더를 오래된 순서로 정리합니다.
        /// 폴더를 재귀 삭제하지 않고 파일 단위로 지우며, 파일이 모두 없어진 폴더만 마지막에 제거합니다.
        /// </summary>
        private static void DeleteExpiredDateFolders(string logRootPath, DateTime deleteBefore, ref int remainingBudget)
        {
            if (!Directory.Exists(logRootPath))
            {
                return;
            }

            List<DateFolder> expiredFolders = new List<DateFolder>();
            foreach (string directoryPath in Directory.GetDirectories(logRootPath))
            {
                DateTime folderDate;
                if (!TryParseDateName(Path.GetFileName(directoryPath), out folderDate) || folderDate >= deleteBefore)
                {
                    continue;
                }

                expiredFolders.Add(new DateFolder(folderDate, directoryPath));
            }

            // 삭제 개수 제한에 걸려 중간에 멈추더라도 가장 오래된 로그부터 없어지도록 정렬합니다.
            expiredFolders.Sort(CompareByDate);

            foreach (DateFolder folder in expiredFolders)
            {
                if (remainingBudget <= 0)
                {
                    return;
                }

                DeleteFilesWithinBudget(folder.Path, ref remainingBudget);
                RemoveDirectoryIfEmpty(folder.Path);
            }
        }

        /// <summary>
        /// 폴더 안의 파일을 남은 삭제 예산만큼만 하나씩 지웁니다.
        /// 예산이 떨어지면 남은 파일은 그대로 두고 다음 실행에서 이어서 지웁니다.
        /// </summary>
        private static void DeleteFilesWithinBudget(string folderPath, ref int remainingBudget)
        {
            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                if (remainingBudget <= 0)
                {
                    return;
                }

                if (TryDeleteFile(filePath))
                {
                    remainingBudget--;
                }
            }
        }

        /// <summary>
        /// 파일이 모두 지워진 일자 폴더만 제거합니다. 아직 파일이 남아 있으면 그대로 둡니다.
        /// </summary>
        private static void RemoveDirectoryIfEmpty(string folderPath)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    return;
                }

                using (IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(folderPath).GetEnumerator())
                {
                    if (entries.MoveNext())
                    {
                        return;
                    }
                }

                Directory.Delete(folderPath, false);
            }
            catch
            {
                // 사용 중인 파일이 있으면 다음 시작 때 다시 시도합니다.
            }
        }

        /// <summary>
        /// 로그 폴더 바로 아래에 있는 옛 방식의 로그 파일을 마지막 기록 시각 기준으로 정리합니다.
        /// 현재 방식의 로그는 일자 폴더 안에 만들어지므로 실행 중인 파일이 지워질 일은 없습니다.
        /// </summary>
        private static void DeleteExpiredLooseLogFiles(string logRootPath, DateTime deleteBefore, ref int remainingBudget)
        {
            if (!Directory.Exists(logRootPath))
            {
                return;
            }

            foreach (string filePath in Directory.EnumerateFiles(logRootPath, "*.log"))
            {
                if (remainingBudget <= 0)
                {
                    return;
                }

                try
                {
                    if (File.GetLastWriteTime(filePath) >= deleteBefore)
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }

                if (TryDeleteFile(filePath))
                {
                    remainingBudget--;
                }
            }
        }

        private static void DeleteExpiredDateFiles(string folderPath, DateTime deleteBefore, ref int remainingBudget)
        {
            if (!Directory.Exists(folderPath))
            {
                return;
            }

            foreach (string filePath in Directory.EnumerateFiles(folderPath, "*.log"))
            {
                if (remainingBudget <= 0)
                {
                    return;
                }

                DateTime fileDate;
                if (!TryParseDateName(Path.GetFileNameWithoutExtension(filePath), out fileDate) || fileDate >= deleteBefore)
                {
                    continue;
                }

                if (TryDeleteFile(filePath))
                {
                    remainingBudget--;
                }
            }
        }

        private static bool TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                // 사용 중이거나 권한이 없는 파일은 건너뛰고 다음 시작 때 다시 시도합니다.
                return false;
            }
        }

        private static int CompareByDate(DateFolder left, DateFolder right)
        {
            return left.Date.CompareTo(right.Date);
        }

        private sealed class DateFolder
        {
            public DateFolder(DateTime date, string path)
            {
                Date = date;
                Path = path;
            }

            public DateTime Date { get; private set; }

            public string Path { get; private set; }
        }

        private static bool TryParseDateName(string name, out DateTime value)
        {
            return DateTime.TryParseExact(
                name,
                DateFolderFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value);
        }
    }
}
