using System;
using System.Collections.Generic;
using System.IO;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 예전 이름으로 저장된 기준 이미지 파일을 현재 규칙으로 바꿉니다.
    ///
    /// <para>
    /// 예전 이름은 품번_방향.png 하나뿐이었습니다.
    ///   01100-51430_Top.png
    /// 지금은 벌 번호와 저장 시각이 들어갑니다.
    ///   [01_Top][001][01100-51430]_20260819-103015.png
    /// </para>
    ///
    /// <para>
    /// 파일만 바꾸면 DB의 경로가 어긋나므로, 바꾼 결과를 <see cref="PartImage.FilePath"/>에
    /// 반영해 돌려줍니다. 호출한 쪽이 그 부품을 저장하면 DB도 새 경로를 갖게 됩니다.
    /// </para>
    ///
    /// <para>
    /// 이미 새 규칙으로 되어 있는 파일은 건드리지 않습니다. 여러 번 돌려도 안전합니다.
    /// </para>
    /// </summary>
    public class ReferenceImageFileNameMigrator
    {
        /// <summary>
        /// 한 부품의 기준 이미지 파일 이름을 현재 규칙으로 맞춥니다.
        /// </summary>
        /// <param name="part">대상 부품입니다. Images의 FilePath가 갱신됩니다.</param>
        /// <param name="renamedCount">이름을 바꾼 파일 수입니다.</param>
        /// <param name="errors">바꾸지 못한 파일의 사유입니다.</param>
        /// <returns>하나라도 바꿨으면 true입니다. 저장이 필요한지 판단하는 데 씁니다.</returns>
        public bool MigratePart(Part part, out int renamedCount, out IList<string> errors)
        {
            renamedCount = 0;
            errors = new List<string>();

            if (part == null || part.Images == null || part.Images.Count == 0)
            {
                return false;
            }

            // 벌 번호가 비어 있는 예전 자료는 저장 시각이 이른 것부터 1, 2, 3... 으로 셉니다.
            // 같은 시각이면 한 벌이므로 같은 번호를 받습니다.
            IDictionary<string, int> setNoByTime = BuildSetNoByCapturedTime(part.Images);

            foreach (PartImage image in part.Images)
            {
                if (image == null || image.IsTemporary || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                if (!File.Exists(image.FilePath))
                {
                    // 파일이 없으면 이름을 바꿀 수 없습니다. DB만 남은 경우로, 여기서 손대지 않습니다.
                    continue;
                }

                int setNo = image.SetNo;
                if (setNo < 1)
                {
                    setNo = ResolveSetNo(setNoByTime, image.CapturedAt);
                    image.SetNo = setNo;
                }

                string directoryPath = Path.GetDirectoryName(image.FilePath);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    continue;
                }

                string extension = Path.GetExtension(image.FilePath);
                DateTime savedAt = image.CapturedAt == DateTime.MinValue
                    ? File.GetLastWriteTime(image.FilePath)
                    : image.CapturedAt;

                string targetName = ReferenceImageFileNamePolicy.BuildImageFileName(
                    image.ViewType, setNo, part.PartNo, savedAt, extension);
                string targetPath = Path.Combine(directoryPath, targetName);

                if (string.Equals(image.FilePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    // 이미 현재 규칙입니다.
                    continue;
                }

                try
                {
                    if (File.Exists(targetPath))
                    {
                        // 같은 이름이 이미 있으면 덮어쓰지 않고 경로만 그쪽으로 맞춥니다.
                        // 같은 벌의 같은 방향이므로 내용이 같다고 보고, 남은 원본은 지웁니다.
                        File.Delete(image.FilePath);
                    }
                    else
                    {
                        File.Move(image.FilePath, targetPath);
                    }

                    image.FilePath = targetPath;
                    image.CapturedAt = savedAt;
                    renamedCount++;
                }
                catch (IOException ex)
                {
                    errors.Add(Path.GetFileName(image.FilePath) + " : " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    errors.Add(Path.GetFileName(image.FilePath) + " : " + ex.Message);
                }
            }

            return renamedCount > 0;
        }

        /// <summary>
        /// 저장 시각별로 벌 번호를 매깁니다. 이른 시각이 1번입니다.
        /// </summary>
        private IDictionary<string, int> BuildSetNoByCapturedTime(IList<PartImage> images)
        {
            List<DateTime> times = new List<DateTime>();
            foreach (PartImage image in images)
            {
                if (image == null || image.IsTemporary)
                {
                    continue;
                }

                if (!times.Contains(image.CapturedAt))
                {
                    times.Add(image.CapturedAt);
                }
            }

            times.Sort();

            IDictionary<string, int> setNoByTime = new Dictionary<string, int>();
            for (int index = 0; index < times.Count; index++)
            {
                setNoByTime[BuildTimeKey(times[index])] = index + 1;
            }

            return setNoByTime;
        }

        private int ResolveSetNo(IDictionary<string, int> setNoByTime, DateTime capturedAt)
        {
            int setNo;
            if (setNoByTime.TryGetValue(BuildTimeKey(capturedAt), out setNo))
            {
                return setNo;
            }

            return 1;
        }

        private string BuildTimeKey(DateTime value)
        {
            return value.ToString("O");
        }
    }
}
