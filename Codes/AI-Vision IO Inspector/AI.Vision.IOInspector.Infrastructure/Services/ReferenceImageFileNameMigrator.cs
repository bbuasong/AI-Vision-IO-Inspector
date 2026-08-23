using System;
using System.Collections.Generic;
using System.IO;
using AI.Vision.IOInspector.Domain.Enums;
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

            NormalizeSavedImageSetNumbers(part, ref renamedCount, errors);
            MigrateCoordinateFile(part, ref renamedCount, errors);

            return renamedCount > 0;
        }

        /// <summary>
        /// DB에는 최신 한 벌만 남기는 구조에서 과거 저장본의 벌 번호가 모두 001로 남은 경우를
        /// 복구합니다. 실제 파일의 저장 시각이 한 번의 이미지 저장 단위이므로, 그 시각순으로
        /// 001, 002, 003...을 다시 부여합니다. DB에 남아 있는 최신 이미지의 경로와 벌 번호도
        /// 같이 갱신되어 검사 화면은 계속 마지막 벌을 기준으로 사용합니다.
        /// </summary>
        private void NormalizeSavedImageSetNumbers(Part part, ref int renamedCount, IList<string> errors)
        {
            string folderPath = FindPartFolderPath(part);
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            Dictionary<DateTime, IList<SavedImageFile>> filesBySavedAt =
                new Dictionary<DateTime, IList<SavedImageFile>>();
            try
            {
                foreach (string filePath in Directory.GetFiles(folderPath))
                {
                    ImageViewType viewType;
                    int ignoredSetNo;
                    DateTime savedAt;
                    if (!ReferenceImageFileNamePolicy.TryParseSavedImageFileName(
                            Path.GetFileName(filePath),
                            out viewType,
                            out ignoredSetNo,
                            out savedAt))
                    {
                        continue;
                    }

                    IList<SavedImageFile> filesInSet;
                    if (!filesBySavedAt.TryGetValue(savedAt, out filesInSet))
                    {
                        filesInSet = new List<SavedImageFile>();
                        filesBySavedAt[savedAt] = filesInSet;
                    }

                    SavedImageFile file = new SavedImageFile();
                    file.FilePath = filePath;
                    file.ViewType = viewType;
                    file.SavedAt = savedAt;
                    filesInSet.Add(file);
                }
            }
            catch (IOException ex)
            {
                errors.Add((part == null ? "-" : part.PartNo) + " 저장 벌 번호 확인 실패: " + ex.Message);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add((part == null ? "-" : part.PartNo) + " 저장 벌 번호 확인 실패: " + ex.Message);
                return;
            }

            List<DateTime> savedTimes = new List<DateTime>(filesBySavedAt.Keys);
            savedTimes.Sort();
            for (int index = 0; index < savedTimes.Count; index++)
            {
                int setNo = index + 1;
                foreach (SavedImageFile file in filesBySavedAt[savedTimes[index]])
                {
                    string extension = Path.GetExtension(file.FilePath);
                    string targetPath = Path.Combine(
                        folderPath,
                        ReferenceImageFileNamePolicy.BuildImageFileName(
                            file.ViewType,
                            setNo,
                            part.PartNo,
                            file.SavedAt,
                            extension));

                    if (string.Equals(file.FilePath, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateRegisteredImagePath(part, file.FilePath, targetPath, setNo);
                        continue;
                    }

                    if (File.Exists(targetPath))
                    {
                        errors.Add(Path.GetFileName(file.FilePath) +
                                   " : 대상 파일이 이미 있어 벌 번호를 바꾸지 않았습니다.");
                        continue;
                    }

                    try
                    {
                        File.Move(file.FilePath, targetPath);
                        UpdateRegisteredImagePath(part, file.FilePath, targetPath, setNo);
                        renamedCount++;
                    }
                    catch (IOException ex)
                    {
                        errors.Add(Path.GetFileName(file.FilePath) + " : " + ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        errors.Add(Path.GetFileName(file.FilePath) + " : " + ex.Message);
                    }
                }
            }
        }

        private void UpdateRegisteredImagePath(Part part, string sourcePath, string targetPath, int setNo)
        {
            if (part == null || part.Images == null)
            {
                return;
            }

            foreach (PartImage image in part.Images)
            {
                if (image == null ||
                    !string.Equals(image.FilePath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                image.FilePath = targetPath;
                image.SetNo = setNo;
            }
        }

        /// <summary>
        /// 좌표 이미지 이름도 현재 규칙으로 바꿉니다.
        ///
        /// <para>
        /// 예전에는 카메라를 나누지 않아 부품마다 한 장이었습니다.
        ///   품번_coordinate.png  또는  coordinate.png
        /// 지금은 카메라마다 한 장이므로 Thickness 것으로 옮깁니다.
        /// 그때는 Thickness 말고는 측정부가 없었기 때문입니다.
        /// </para>
        /// </summary>
        private void MigrateCoordinateFile(Part part, ref int renamedCount, IList<string> errors)
        {
            string folderPath = FindPartFolderPath(part);
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            string targetPath = Path.Combine(
                folderPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(ImageViewType.Thickness, part.PartNo));
            if (File.Exists(targetPath))
            {
                return;
            }

            string sourcePath = Path.Combine(
                folderPath, ReferenceImageFileNamePolicy.BuildLegacyCoordinateFileName(part.PartNo));
            if (!File.Exists(sourcePath))
            {
                sourcePath = Path.Combine(folderPath, ReferenceImageFileNamePolicy.LegacyCoordinateFileName);
                if (!File.Exists(sourcePath))
                {
                    return;
                }
            }

            try
            {
                File.Move(sourcePath, targetPath);
                renamedCount++;
            }
            catch (IOException ex)
            {
                errors.Add(Path.GetFileName(sourcePath) + " : " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                errors.Add(Path.GetFileName(sourcePath) + " : " + ex.Message);
            }
        }

        /// <summary>
        /// 이 부품의 이미지가 놓인 폴더입니다. 기준 이미지 경로에서 알아냅니다.
        /// </summary>
        private string FindPartFolderPath(Part part)
        {
            foreach (PartImage image in part.Images)
            {
                if (image == null || image.IsTemporary || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                string folderPath = Path.GetDirectoryName(image.FilePath);
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    return folderPath;
                }
            }

            return string.Empty;
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

        private class SavedImageFile
        {
            public string FilePath { get; set; }

            public ImageViewType ViewType { get; set; }

            public DateTime SavedAt { get; set; }
        }
    }
}
