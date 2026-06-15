using System;
using System.IO;
using System.Text;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Infrastructure;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// Stores one current reference image per part/view type under DB\Image\CategoryCode\PartNo.
    /// Replacing a view type keeps the previous current image as an OldVer backup file.
    /// </summary>
    public class LocalReferenceImageFileService : IReferenceImageFileService
    {
        private readonly string _imageFolderPath;

        public LocalReferenceImageFileService(string rootPath)
        {
            RuntimeImagePathSettings pathSettings = RuntimeImagePathSettings.Load(rootPath);
            _imageFolderPath = pathSettings.ReferenceImageRootPath;
            Directory.CreateDirectory(_imageFolderPath);
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
                    BackupCurrentImageIfNeeded(part, viewType, existingImage, targetPath);
                    File.Move(temporaryPath, targetPath);
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

        public bool DeleteReferenceImage(PartImage image, out string message)
        {
            message = string.Empty;
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return true;
            }

            if (!File.Exists(image.FilePath))
            {
                return true;
            }

            try
            {
                File.Delete(image.FilePath);
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

        private string BuildBackupFileName(Part part, ImageViewType viewType, string extension, int attempt)
        {
            string safePartNo = MakeSafeFileName(part.PartNo);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            string suffix = attempt <= 0 ? string.Empty : "_" + attempt.ToString();
            return safePartNo + "_" + viewType.ToString() + "_OldVer_" + timestamp + suffix + extension;
        }

        private string BuildTemporaryFilePath(string folderPath, string extension)
        {
            string fileName = "Copying_" + Guid.NewGuid().ToString("N") + extension;
            return Path.Combine(folderPath, fileName);
        }

        private void BackupCurrentImageIfNeeded(Part part, ImageViewType viewType, PartImage existingImage, string targetPath)
        {
            if (existingImage != null && !string.IsNullOrWhiteSpace(existingImage.FilePath) && File.Exists(existingImage.FilePath))
            {
                BackupFile(part, viewType, existingImage.FilePath);
            }

            if (File.Exists(targetPath) && (existingImage == null || !IsSamePath(existingImage.FilePath, targetPath)))
            {
                BackupFile(part, viewType, targetPath);
            }
        }

        private void BackupFile(Part part, ImageViewType viewType, string filePath)
        {
            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".img";
            }

            string folderPath = Path.GetDirectoryName(filePath);
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                string backupFileName = BuildBackupFileName(part, viewType, extension, attempt);
                string backupPath = Path.Combine(folderPath, backupFileName);
                if (!File.Exists(backupPath))
                {
                    File.Move(filePath, backupPath);
                    return;
                }
            }

            throw new IOException("기존 기준 이미지 백업 파일명을 만들 수 없습니다.");
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
