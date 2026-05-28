using System;
using System.IO;
using System.Text;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;

namespace AI.Vision.IOInspector.Infrastructure.Services
{
    /// <summary>
    /// 기준 이미지를 로컬 DB\Image 폴더에 저장합니다.
    /// 파일명은 PartNo_PartName_0001 형식으로 관리하여 사용자가 어떤 부품의 이미지인지 바로 확인할 수 있게 합니다.
    /// </summary>
    public class LocalReferenceImageFileService : IReferenceImageFileService
    {
        private readonly string _imageFolderPath;

        public LocalReferenceImageFileService(string rootPath)
        {
            _imageFolderPath = Path.Combine(rootPath, "DB", "Image");
            Directory.CreateDirectory(_imageFolderPath);
        }

        public PartImage AddReferenceImage(Part part, string sourceFilePath, int imageOrder)
        {
            string extension = Path.GetExtension(sourceFilePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            string fileName = BuildImageFileName(part, imageOrder, extension);
            string targetPath = Path.Combine(_imageFolderPath, fileName);
            File.Copy(sourceFilePath, targetPath, true);

            PartImage image = new PartImage();
            image.PartNo = part.PartNo;
            image.ViewType = ImageViewType.Top;
            image.FilePath = targetPath;
            image.CapturedAt = DateTime.Now;
            return image;
        }

        public void DeleteReferenceImage(PartImage image)
        {
            if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
            {
                return;
            }

            if (File.Exists(image.FilePath))
            {
                File.Delete(image.FilePath);
            }
        }

        private string BuildImageFileName(Part part, int imageOrder, string extension)
        {
            string safePartNo = MakeSafeFileName(part.PartNo);
            string safePartName = MakeSafeFileName(part.PartName);
            return safePartNo + "_" + safePartName + "_" + imageOrder.ToString("0000") + extension;
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
