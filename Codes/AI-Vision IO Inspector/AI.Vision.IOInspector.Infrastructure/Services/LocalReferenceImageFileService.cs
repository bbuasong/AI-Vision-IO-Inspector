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
            DeleteEmptyTemporaryDirectories(Path.Combine(_imageFolderPath, "Temp"));
        }

        public PartImage AddReferenceImage(Part part, string sourceFilePath, ImageViewType viewType, PartImage existingImage)
        {
            string extension = ResolveImageExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string partFolderPath = BuildPartFolderPath(part);
            Directory.CreateDirectory(partFolderPath);

            string targetPath = Path.Combine(partFolderPath, BuildImageFileName(part, viewType, extension));
            if (!IsSamePath(sourceFilePath, targetPath))
            {
                string temporaryPath = BuildTemporaryFilePath(partFolderPath, extension);
                try
                {
                    File.Copy(sourceFilePath, temporaryPath, false);
                    ReplaceFileWithoutBackup(temporaryPath, targetPath);
                    DeleteReplacedImageIfNeeded(existingImage, targetPath);
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
            image.CapturedAt = DateTime.Now;
            return image;
        }

        /// <summary>
        /// 같은 품번으로 다시 촬영하기 전에 해당 품번의 임시 기준 이미지 작업 폴더만 비웁니다.
        /// 최종 IMAGE_PATH\분류코드\품번 폴더는 변경하지 않습니다.
        /// </summary>
        public void ClearTemporaryReferenceImages(Part part)
        {
            string temporaryFolderPath = BuildTemporaryPartFolderPath(part);
            if (!Directory.Exists(temporaryFolderPath) || !IsPathInsideImageFolder(temporaryFolderPath))
            {
                return;
            }

            foreach (string filePath in Directory.GetFiles(temporaryFolderPath))
            {
                File.Delete(filePath);
            }

            DeleteEmptyTemporaryDirectories(temporaryFolderPath);
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

            string targetPath = Path.Combine(
                temporaryFolderPath,
                BuildImageFileName(part, viewType, extension));
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

                PartImage committedImage = AddReferenceImage(part, image.FilePath, image.ViewType, null);
                committedImage.CapturedAt = DateTime.Now;
                committedImage.IsTemporary = false;
                committedImages.Add(committedImage);
            }

            return committedImages;
        }

        public string GetTemporaryCoordinateImagePath(Part part)
        {
            string temporaryFolderPath = BuildTemporaryPartFolderPath(part);
            Directory.CreateDirectory(temporaryFolderPath);
            return Path.Combine(
                temporaryFolderPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(part == null ? string.Empty : part.PartNo));
        }

        public void DeleteTemporaryCoordinateImage(Part part)
        {
            string temporaryFolderPath = BuildTemporaryPartFolderPath(part);
            DeleteFileIfExists(Path.Combine(
                temporaryFolderPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(part == null ? string.Empty : part.PartNo)));
            DeleteFileIfExists(Path.Combine(temporaryFolderPath, ReferenceImageFileNamePolicy.LegacyCoordinateFileName));
            DeleteEmptyTemporaryDirectories(temporaryFolderPath);
        }

        /// <summary>
        /// Temp에 생성된 좌표 확인 이미지를 최종 품번 폴더로 확정합니다.
        /// 파일명은 품번_coordinate.png이며 기존 파일은 백업 없이 교체합니다.
        /// 측정부를 모두 삭제해 Temp에 좌표 이미지가 없으면, 이전에 저장된 최종 coordinate 이미지도 함께 삭제해
        /// 더 이상 존재하지 않는 측정부의 선이 계속 표시되지 않게 합니다.
        /// </summary>
        public void CommitTemporaryCoordinateImage(Part part)
        {
            string partFolderPath = BuildPartFolderPath(part);
            string targetPath = Path.Combine(
                partFolderPath,
                ReferenceImageFileNamePolicy.BuildCoordinateFileName(part.PartNo));

            string sourcePath = GetTemporaryCoordinateImagePath(part);
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
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(
                BuildImageFileName(part, viewType, ".png"));
            foreach (string filePath in Directory.GetFiles(temporaryFolderPath))
            {
                if (string.Equals(
                    Path.GetFileNameWithoutExtension(filePath),
                    fileNameWithoutExtension,
                    StringComparison.OrdinalIgnoreCase))
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
