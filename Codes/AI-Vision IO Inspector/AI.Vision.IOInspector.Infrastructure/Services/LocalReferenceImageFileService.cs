using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// Stores one current reference image per part/view type under Config.json IMAGE_PATH\CategoryCode\PartNo.
    /// 같은 방향 이미지를 다시 저장하면 현재 파일을 교체하며 별도 OldVer 백업은 만들지 않습니다.
    /// </summary>
    public class LocalReferenceImageFileService : IReferenceImageFileService
    {
        private readonly string _imageFolderPath;

        public LocalReferenceImageFileService(string rootPath)
        {
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(rootPath);
            _imageFolderPath = pathSettings.ReferenceImageRootPath;
            // Temp는 DB 저장 전 작업 상태일 뿐, 앱을 다시 열어도 복원하지 않습니다.
            // 이전 실행에서 저장/취소가 끝나지 않아 남은 파일이 다음 품번에 섞이지 않게 시작 시 정리합니다.
            ClearAllTemporaryReferenceImages();
        }

        /// <summary>
        /// 기준 이미지를 한 장 보관합니다.
        ///
        /// <para>
        /// 파일 이름에 저장 시각이 들어가므로 예전 이미지를 덮어쓰지 않고 새 파일로 쌓입니다.
        /// 저장 버튼을 누를 때마다 그 시각의 이미지가 한 벌로 늘어납니다.
        /// 예전에는 방향마다 파일 하나를 덮어써서 마지막 것만 남았습니다.
        /// </para>
        /// </summary>
        public PartImage AddReferenceImage(
            Part part,
            string sourceFilePath,
            ImageViewType viewType,
            int setNo,
            DateTime savedAt)
        {
            string extension = ResolveImageExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string partFolderPath = BuildPartFolderPath(part);
            Directory.CreateDirectory(partFolderPath);

            string targetPath = Path.Combine(
                partFolderPath,
                ReferenceImageFileNamePolicy.BuildImageFileName(viewType, setNo, part.PartNo, savedAt, extension));

            if (!IsSamePath(sourceFilePath, targetPath))
            {
                string temporaryPath = BuildTemporaryFilePath(partFolderPath, extension);
                try
                {
                    File.Copy(sourceFilePath, temporaryPath, false);
                    ReplaceFileWithoutBackup(temporaryPath, targetPath);
                }
                catch
                {
                    DeleteTemporaryFile(temporaryPath);
                    throw;
                }
            }

            PartImage image = new PartImage();
            image.PartNo = part.PartNo;
            image.ViewType = viewType;
            image.FilePath = targetPath;

            // 파일명에 적힌 시각과 같은 값을 씁니다. 한 벌로 묶는 기준이 되므로
            // 여기서 DateTime.Now를 다시 읽으면 6장이 초 단위로 갈릴 수 있습니다.
            image.CapturedAt = savedAt;
            image.SetNo = setNo;
            return image;
        }

        /// <summary>
        /// 같은 품번으로 다시 촬영하기 전에 해당 품번의 임시 기준 이미지 작업 폴더만 비웁니다.
        /// 최종 IMAGE_PATH\분류코드\품번 폴더는 변경하지 않습니다.
        /// </summary>
        public void ClearTemporaryReferenceImages(Part part)
        {
            // 빈 품번이면 Temp\품번이 아니라 Temp 루트가 계산됩니다.
            // 한 품목을 정리하는 호출이 다른 품목의 작업 파일까지 지워서는 안 됩니다.
            if (part == null || string.IsNullOrWhiteSpace(part.PartNo))
            {
                return;
            }

            string temporaryFolderPath = BuildTemporaryPartFolderPath(part);
            if (!Directory.Exists(temporaryFolderPath) || !IsPathInsideImageFolder(temporaryFolderPath))
            {
                return;
            }

            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(temporaryFolderPath);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            foreach (string filePath in filePaths)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                    // 잠긴 파일은 다음 저장/재촬영/시작 정리 때 다시 시도합니다.
                }
                catch (UnauthorizedAccessException)
                {
                    // 권한이 회복된 뒤 다음 정리 때 다시 시도합니다.
                }
            }

            DeleteEmptyTemporaryDirectories(temporaryFolderPath);
        }

        /// <summary>
        /// 비정상 종료 또는 화면 전환 중 남은 모든 임시 기준/좌표 파일을 정리합니다.
        /// Temp 루트만 대상으로 하므로 최종 기준 이미지 폴더에는 영향을 주지 않습니다.
        /// </summary>
        private void ClearAllTemporaryReferenceImages()
        {
            string temporaryRootPath = Path.Combine(_imageFolderPath, "Temp");
            if (!Directory.Exists(temporaryRootPath) || !IsPathInsideImageFolder(temporaryRootPath))
            {
                return;
            }

            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(temporaryRootPath, "*", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            foreach (string filePath in filePaths)
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                    // 다른 프로세스가 잠시 점유한 Temp 파일은 다음 정리 시 다시 시도합니다.
                    // 시작 자체가 실패해서는 안 됩니다.
                }
                catch (UnauthorizedAccessException)
                {
                    // 권한이 회복된 뒤 다음 정리 시 다시 시도합니다.
                }
            }

            DeleteEmptyTemporaryDirectories(temporaryRootPath);
        }

        /// <summary>
        /// 촬영 이미지를 최종 기준 이미지 폴더에 즉시 반영하지 않고 Temp\품번 폴더에 보관합니다.
        /// DB 저장 시점 전에는 등록시간을 확정하지 않습니다.
        /// </summary>
        public PartImage StageReferenceImage(Part part, string sourceFilePath, ImageViewType viewType)
        {
            string extension = ResolveImageExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string temporaryFolderPath = BuildTemporaryPartFolderPath(part);
            Directory.CreateDirectory(temporaryFolderPath);
            DeleteTemporaryViewFiles(temporaryFolderPath, part, viewType);

            // 최종 이름과 같은 규칙에 촬영 시각을 넣습니다.
            // 다시 찍을 때마다 이름이 달라져야 어느 것이 방금 찍은 것인지 알 수 있습니다.
            string targetPath = Path.Combine(
                temporaryFolderPath,
                ReferenceImageFileNamePolicy.BuildTemporaryImageFileName(
                    viewType, part.PartNo, DateTime.Now, extension));
            File.Copy(sourceFilePath, targetPath, true);

            PartImage image = new PartImage();
            image.PartNo = part.PartNo;
            image.ViewType = viewType;
            image.FilePath = targetPath;
            image.CapturedAt = DateTime.MinValue;
            image.IsTemporary = true;
            return image;
        }

        /// <summary>
        /// DB 저장 직전에 Temp 이미지들을 최종 폴더로 복사하고 기존 동일 방향 이미지는 현재 파일로 교체합니다.
        /// Temp 삭제는 DB 저장 성공 후 ClearTemporaryReferenceImages에서 수행합니다.
        /// </summary>
        public IList<PartImage> CommitTemporaryReferenceImages(Part part, IList<PartImage> images)
        {
            IList<PartImage> committedImages = new List<PartImage>();
            if (images == null)
            {
                return committedImages;
            }

            // 확정 시각을 한 번만 정해 모든 이미지가 같은 값을 쓰게 합니다.
            // 이미지마다 DateTime.Now를 읽으면 파일명의 시각이 초 단위로 갈려
            // 한 벌로 묶이지 않습니다.
            DateTime savedAt = DateTime.Now;

            // 이번에 확정되는 것들이 한 벌이므로 번호도 하나만 씁니다.
            // 이미 확정된 이미지들의 최대 번호 다음 값입니다.
            int setNo = ResolveNextSetNo(part, images);

            foreach (PartImage image in images)
            {
                if (image == null)
                {
                    continue;
                }

                if (!image.IsTemporary)
                {
                    committedImages.Add(image);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(image.FilePath) || !File.Exists(image.FilePath))
                {
                    throw new FileNotFoundException("임시 기준 이미지 파일을 찾을 수 없습니다.", image.FilePath);
                }

                PartImage committedImage = AddReferenceImage(part, image.FilePath, image.ViewType, setNo, savedAt);
                committedImage.IsTemporary = false;
                committedImages.Add(committedImage);
            }

            return committedImages;
        }

        /// <summary>
        /// DB에는 최신 한 벌만 보관하므로 DB 목록만으로 다음 벌 번호를 계산하면 매번 001이 됩니다.
        /// 최종 이미지 폴더의 저장 시각 묶음도 함께 세어, 이전 벌을 DB에 넣지 않아도
        /// 파일명 벌 번호는 저장할 때마다 증가하게 합니다.
        /// </summary>
        private int ResolveNextSetNo(Part part, IList<PartImage> images)
        {
            int nextSetNo = ReferenceImageFileNamePolicy.ResolveNextSetNo(images);
            if (part == null)
            {
                return nextSetNo;
            }

            string folderPath = BuildPartFolderPath(part);
            if (!Directory.Exists(folderPath))
            {
                return nextSetNo;
            }

            IList<DateTime> savedTimes = new List<DateTime>();
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
                            out savedAt) ||
                        savedTimes.Contains(savedAt))
                    {
                        continue;
                    }

                    savedTimes.Add(savedAt);
                }
            }
            catch (IOException)
            {
                return nextSetNo;
            }
            catch (UnauthorizedAccessException)
            {
                return nextSetNo;
            }

            return Math.Max(nextSetNo, savedTimes.Count + 1);
        }

        public string GetTemporaryCoordinateImagePath(Part part, ImageViewType viewType)
        {
            string temporaryFolderPath = BuildTemporaryPartFolderPath(part);
            Directory.CreateDirectory(temporaryFolderPath);
            return Path.Combine(
                temporaryFolderPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(
                    viewType, part == null ? string.Empty : part.PartNo));
        }

        public void DeleteTemporaryCoordinateImage(Part part, ImageViewType viewType)
        {
            string temporaryFolderPath = BuildTemporaryPartFolderPath(part);
            string partNo = part == null ? string.Empty : part.PartNo;

            DeleteFileIfExists(Path.Combine(
                temporaryFolderPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(viewType, partNo)));

            // 이름 규칙을 바꾸기 전에 남은 파일도 함께 치웁니다.
            // 그때는 Thickness 하나뿐이라 다른 카메라에는 해당하지 않습니다.
            if (viewType == ImageViewType.Thickness)
            {
                DeleteFileIfExists(Path.Combine(
                    temporaryFolderPath,
                    ReferenceImageFileNamePolicy.BuildLegacyCoordinateFileName(partNo)));
                DeleteFileIfExists(Path.Combine(
                    temporaryFolderPath, ReferenceImageFileNamePolicy.LegacyCoordinateFileName));
            }

            DeleteEmptyTemporaryDirectories(temporaryFolderPath);
        }

        /// <summary>
        /// Temp에 생성된 좌표 확인 이미지를 최종 품번 폴더로 확정합니다.
        /// 파일명은 카메라마다 다릅니다(ReferenceImageFileNamePolicy.BuildCoordinateFileName). 기존 파일은 백업 없이 교체합니다.
        /// 측정부를 모두 삭제해 Temp에 좌표 이미지가 없으면, 이전에 저장된 최종 coordinate 이미지도 함께 삭제해
        /// 더 이상 존재하지 않는 측정부의 선이 계속 표시되지 않게 합니다.
        /// </summary>
        public void CommitTemporaryCoordinateImage(Part part, ImageViewType viewType)
        {
            string partFolderPath = BuildPartFolderPath(part);
            string targetPath = Path.Combine(
                partFolderPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(viewType, part.PartNo));

            string sourcePath = GetTemporaryCoordinateImagePath(part, viewType);
            if (!File.Exists(sourcePath))
            {
                DeleteFileIfExists(targetPath);
                DeleteLegacyCoordinateImage(partFolderPath, targetPath);
                return;
            }

            Directory.CreateDirectory(partFolderPath);
            string copyingPath = BuildTemporaryFilePath(partFolderPath, ".png");
            try
            {
                File.Copy(sourcePath, copyingPath, false);
                ReplaceFileWithoutBackup(copyingPath, targetPath);
                DeleteLegacyCoordinateImage(partFolderPath, targetPath);
            }
            catch
            {
                DeleteTemporaryFile(copyingPath);
                throw;
            }
        }

        /// <summary>
        /// 이 부품의 기준 이미지 폴더를 통째로 비웁니다.
        ///
        /// <para>
        /// 폴더는 이미지 저장소\분류코드\품번 이라 이 부품 전용입니다.
        /// 그래서 안에 있는 파일을 모두 지워도 다른 부품에 영향이 없습니다.
        /// DB와 연결이 끊긴 파일도 여기서 함께 사라집니다.
        /// </para>
        ///
        /// <para>
        /// 임시 작업 폴더(Temp\품번)도 함께 비웁니다. 등록 도중에 남은 파일이
        /// 다음 작업에 섞이지 않게 하기 위해서입니다.
        /// </para>
        /// </summary>
        public bool DeleteAllReferenceImageFiles(Part part, out int deletedCount, out IList<string> errors)
        {
            deletedCount = 0;
            errors = new List<string>();

            if (part == null)
            {
                return true;
            }

            DeleteFilesInFolder(BuildPartFolderPath(part), ref deletedCount, errors);
            DeleteFilesInFolder(BuildTemporaryPartFolderPath(part), ref deletedCount, errors);

            return errors.Count == 0;
        }

        private void DeleteFilesInFolder(string folderPath, ref int deletedCount, IList<string> errors)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            string[] filePaths;
            try
            {
                filePaths = Directory.GetFiles(folderPath);
            }
            catch (Exception ex)
            {
                errors.Add(folderPath + " : " + ex.Message);
                return;
            }

            foreach (string filePath in filePaths)
            {
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                }
                catch (IOException ex)
                {
                    errors.Add(Path.GetFileName(filePath) + " : " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    errors.Add(Path.GetFileName(filePath) + " : " + ex.Message);
                }
            }

            DeleteEmptyTemporaryDirectories(folderPath);
        }

        public bool DeleteReferenceImage(PartImage image, out string message)
        {
            message = string.Empty;
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return true;
            }

            if (!File.Exists(image.FilePath))
            {
                DeleteEmptyTemporaryDirectories(Path.GetDirectoryName(image.FilePath));
                return true;
            }

            try
            {
                File.Delete(image.FilePath);
                DeleteEmptyTemporaryDirectories(Path.GetDirectoryName(image.FilePath));
                return true;
            }
            catch (IOException ex)
            {
                message = "이미지 파일이 다른 프로세스에서 사용 중이라 삭제할 수 없습니다. 미리보기, 이미지 편집기, 탐색기 미리보기 창을 닫은 뒤 다시 시도하세요. 상세: " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                message = "이미지 파일 삭제 권한이 없거나 읽기 전용 상태입니다. 상세: " + ex.Message;
                return false;
            }
        }

        private bool PreserveReferenceImagesForPart(Part part, out string message)
        {
            message = string.Empty;
            if (part == null || string.IsNullOrWhiteSpace(part.PartNo))
            {
                return true;
            }

            foreach (PartImage image in part.Images)
            {
                if (image == null)
                {
                    continue;
                }
            }

            string partFolderPath = BuildPartFolderPath(part);
            if (!Directory.Exists(partFolderPath))
            {
                return true;
            }

            try
            {
                message = string.Empty;
                return true;
            }
            catch (IOException ex)
            {
                message = "부품 이미지 폴더를 삭제할 수 없습니다. 이미지 미리보기 또는 외부 프로그램을 닫은 뒤 다시 시도하세요. 상세: " + ex.Message;
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                message = "부품 이미지 폴더 삭제 권한이 없습니다. 상세: " + ex.Message;
                return false;
            }
        }

        private string BuildPartFolderPath(Part part)
        {
            string safeCategoryCode = MakeSafeFileName(part.CategoryCode);
            string safePartNo = MakeSafeFileName(part.PartNo);
            return Path.Combine(_imageFolderPath, safeCategoryCode, safePartNo);
        }

        private string BuildTemporaryPartFolderPath(Part part)
        {
            string safePartNo = MakeSafeFileName(part == null ? string.Empty : part.PartNo);
            return Path.Combine(_imageFolderPath, "Temp", safePartNo);
        }

        private string BuildImageFileName(Part part, ImageViewType viewType, string extension)
        {
            string safePartNo = MakeSafeFileName(part.PartNo);
            return safePartNo + "_" + viewType.ToString() + extension;
        }

        private string ResolveImageExtension(string sourceFilePath)
        {
            string detectedExtension = DetectImageExtension(sourceFilePath);
            if (!string.IsNullOrWhiteSpace(detectedExtension))
            {
                return detectedExtension;
            }

            return Path.GetExtension(sourceFilePath);
        }

        private string DetectImageExtension(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            {
                return string.Empty;
            }

            byte[] header = new byte[8];
            int readLength;
            using (FileStream stream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                readLength = stream.Read(header, 0, header.Length);
            }

            if (readLength >= 2 && header[0] == 0xFF && header[1] == 0xD8)
            {
                return ".jpg";
            }

            if (readLength >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A)
            {
                return ".png";
            }

            if (readLength >= 2 && header[0] == 0x42 && header[1] == 0x4D)
            {
                return ".bmp";
            }

            return string.Empty;
        }

        private string BuildTemporaryFilePath(string folderPath, string extension)
        {
            string fileName = "Copying_" + Guid.NewGuid().ToString("N") + extension;
            return Path.Combine(folderPath, fileName);
        }

        private void ReplaceFileWithoutBackup(string sourcePath, string targetPath)
        {
            if (File.Exists(targetPath))
            {
                File.Replace(sourcePath, targetPath, null, true);
                return;
            }

            File.Move(sourcePath, targetPath);
        }

        private void DeleteReplacedImageIfNeeded(PartImage existingImage, string targetPath)
        {
            if (existingImage == null ||
                string.IsNullOrWhiteSpace(existingImage.FilePath) ||
                IsSamePath(existingImage.FilePath, targetPath))
            {
                return;
            }

            DeleteFileIfExists(existingImage.FilePath);
        }

        private void DeleteLegacyCoordinateImage(string folderPath, string currentTargetPath)
        {
            string legacyPath = Path.Combine(folderPath, ReferenceImageFileNamePolicy.LegacyCoordinateFileName);
            if (!IsSamePath(legacyPath, currentTargetPath))
            {
                DeleteFileIfExists(legacyPath);
            }
        }

        private void DeleteFileIfExists(string filePath)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        /// <summary>
        /// Temp 하위에서 이미지가 모두 제거된 품번 폴더와 비어 있는 Temp 루트 폴더를 정리합니다.
        /// 최종 기준 이미지 폴더는 이 함수의 삭제 대상이 아닙니다.
        /// </summary>
        private void DeleteEmptyTemporaryDirectories(string startDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(startDirectoryPath))
            {
                return;
            }

            string temporaryRootPath = Path.GetFullPath(Path.Combine(_imageFolderPath, "Temp"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string currentPath = Path.GetFullPath(startDirectoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!IsSamePath(currentPath, temporaryRootPath) &&
                !IsPathInsideDirectory(currentPath, temporaryRootPath))
            {
                return;
            }

            DeleteEmptyChildDirectories(currentPath);
            while (Directory.Exists(currentPath) &&
                   Directory.GetFileSystemEntries(currentPath).Length == 0)
            {
                Directory.Delete(currentPath, false);
                if (IsSamePath(currentPath, temporaryRootPath))
                {
                    break;
                }

                string parentPath = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrWhiteSpace(parentPath) ||
                    (!IsSamePath(parentPath, temporaryRootPath) &&
                     !IsPathInsideDirectory(parentPath, temporaryRootPath)))
                {
                    break;
                }

                currentPath = parentPath;
            }
        }

        private void DeleteEmptyChildDirectories(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            foreach (string childDirectoryPath in Directory.GetDirectories(directoryPath))
            {
                DeleteEmptyChildDirectories(childDirectoryPath);
                if (Directory.Exists(childDirectoryPath) &&
                    Directory.GetFileSystemEntries(childDirectoryPath).Length == 0)
                {
                    Directory.Delete(childDirectoryPath, false);
                }
            }
        }

        private bool IsPathInsideDirectory(string path, string parentDirectoryPath)
        {
            string parentFullPath = Path.GetFullPath(parentDirectoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string targetFullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return targetFullPath.StartsWith(parentFullPath, StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteTemporaryViewFiles(
            string temporaryFolderPath,
            Part part,
            ImageViewType viewType)
        {
            // 이름에 시각이 들어가면서 파일마다 이름이 달라졌습니다.
            // 그래서 이름이 똑같은 것만 지우면 옛 것이 계속 쌓입니다. 그 카메라의 것이면 모두 지웁니다.
            //
            // 예전 이름(품번_방향)으로 남아 있는 파일도 함께 지웁니다.
            // 남겨 두면 어느 것이 최신인지 알 수 없어 옛 사진이 화면에 올라옵니다.
            string viewPrefix = ReferenceImageFileNamePolicy.BuildViewPrefix(viewType);
            string legacyFileNameWithoutExtension = Path.GetFileNameWithoutExtension(
                BuildImageFileName(part, viewType, ".png"));

            foreach (string filePath in Directory.GetFiles(temporaryFolderPath))
            {
                string fileName = Path.GetFileName(filePath);
                if (fileName == null)
                {
                    continue;
                }

                // 좌표 이미지는 이 자리에서 건드리지 않습니다. 따로 관리합니다.
                if (fileName.IndexOf("_coordinate", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                bool isSameView =
                    fileName.StartsWith(viewPrefix, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        Path.GetFileNameWithoutExtension(filePath),
                        legacyFileNameWithoutExtension,
                        StringComparison.OrdinalIgnoreCase);

                if (isSameView)
                {
                    File.Delete(filePath);
                }
            }
        }

        private void DeleteTemporaryFile(string temporaryPath)
        {
            if (string.IsNullOrWhiteSpace(temporaryPath) || !File.Exists(temporaryPath))
            {
                return;
            }

            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private bool IsSamePath(string leftPath, string rightPath)
        {
            if (string.IsNullOrWhiteSpace(leftPath) || string.IsNullOrWhiteSpace(rightPath))
            {
                return false;
            }

            string leftFullPath = Path.GetFullPath(leftPath).TrimEnd(Path.DirectorySeparatorChar);
            string rightFullPath = Path.GetFullPath(rightPath).TrimEnd(Path.DirectorySeparatorChar);
            return string.Equals(leftFullPath, rightFullPath, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsPathInsideImageFolder(string path)
        {
            string imageRootPath = Path.GetFullPath(_imageFolderPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string targetPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return targetPath.StartsWith(imageRootPath, StringComparison.OrdinalIgnoreCase);
        }

        private string MakeSafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "EMPTY";
            }

            StringBuilder builder = new StringBuilder();
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char character in value)
            {
                bool isInvalid = false;
                foreach (char invalidChar in invalidChars)
                {
                    if (character == invalidChar)
                    {
                        isInvalid = true;
                        break;
                    }
                }

                builder.Append(isInvalid ? '_' : character);
            }

            return builder.ToString();
        }
    }
}
