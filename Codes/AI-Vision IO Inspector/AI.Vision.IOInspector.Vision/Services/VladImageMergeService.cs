using System;
using System.Collections.Generic;
using System.IO;
using AI.Vision.IOInspector.Application.Interfaces;
using AI.Vision.IOInspector.Domain.Enums;
using AI.Vision.IOInspector.Domain.Models;
using AI.Vision.IOInspector.Vision.LegacyVlad;

namespace AI.Vision.IOInspector.Vision.Services
{
    /// <summary>
    /// 기준 이미지 또는 한 번의 검사에서 생성된 6방향 이미지를 VLAD SDK로 병합합니다.
    /// 검사 이미지는 같은 시간 폴더에 이전 검사 파일이 있을 수 있으므로 현재 6장만 임시 폴더에 복사한 뒤 병합합니다.
    /// </summary>
    public class VladImageMergeService : IImageMergeService
    {
        private static readonly ImageViewType[] MergeViewOrder =
        {
            ImageViewType.Top,
            ImageViewType.Front,
            ImageViewType.Back,
            ImageViewType.Left,
            ImageViewType.Right,
            ImageViewType.Thickness
        };

        private static readonly string[] MergedImageExtensions =
        {
            ".png",
            ".bmp",
            ".jpg",
            ".jpeg"
        };

        public bool TryMergeReferenceImages(Part part, out string mergedFilePath, out string message)
        {
            mergedFilePath = string.Empty;
            message = string.Empty;
            if (part == null || string.IsNullOrWhiteSpace(part.PartNo))
            {
                message = "기준 이미지 병합에 필요한 품번이 없습니다.";
                return false;
            }

            Dictionary<ImageViewType, string> sourceFiles = BuildReferenceSourceFiles(part.Images);
            string inputDirectoryPath;
            if (!TryGetCompleteSourceDirectory(sourceFiles, out inputDirectoryPath, out message))
            {
                return false;
            }

            return TryInvokeImageMerge(inputDirectoryPath, part.PartNo, inputDirectoryPath, out mergedFilePath, out message);
        }

        public bool TryMergeInspectionImages(Inspection inspection, out string mergedFilePath, out string message)
        {
            mergedFilePath = string.Empty;
            message = string.Empty;
            if (inspection == null || string.IsNullOrWhiteSpace(inspection.PartNo))
            {
                message = "검사 이미지 병합에 필요한 품번이 없습니다.";
                return false;
            }

            Dictionary<ImageViewType, string> sourceFiles = BuildInspectionSourceFiles(inspection.Images);
            string outputDirectoryPath;
            if (!TryGetCompleteOutputDirectory(sourceFiles, out outputDirectoryPath, out message))
            {
                return false;
            }

            string temporaryInputDirectoryPath = Path.Combine(
                outputDirectoryPath,
                ".VLAD_Merge_" + inspection.Id.ToString("000000") + "_" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(temporaryInputDirectoryPath);
                CopyInspectionSources(inspection.PartNo, sourceFiles, temporaryInputDirectoryPath);
                return TryInvokeImageMerge(
                    temporaryInputDirectoryPath,
                    inspection.PartNo,
                    outputDirectoryPath,
                    out mergedFilePath,
                    out message);
            }
            catch (Exception exception)
            {
                message = "검사 이미지 병합 입력 파일을 준비하지 못했습니다. " + exception.Message;
                return false;
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryInputDirectoryPath);
            }
        }

        public bool TryDeleteReferenceMergedImage(
            string partNo,
            IList<PartImage> referenceImages,
            out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(partNo) || referenceImages == null)
            {
                return true;
            }

            ISet<string> directoryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PartImage image in referenceImages)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath))
                {
                    continue;
                }

                string directoryPath = Path.GetDirectoryName(image.FilePath);
                if (!string.IsNullOrWhiteSpace(directoryPath))
                {
                    directoryPaths.Add(directoryPath);
                }
            }

            try
            {
                foreach (string directoryPath in directoryPaths)
                {
                    DeleteMergedFiles(directoryPath, partNo);
                    DeleteDirectoryIfEmpty(directoryPath);
                }

                return true;
            }
            catch (Exception exception)
            {
                message = "병합 기준 이미지 삭제 실패: " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 방향마다 합칠 원본 한 장을 고릅니다.
        ///
        /// <para>
        /// 벌이 여러 개면 가장 최근 것을 씁니다. 예전에는 목록에서 처음 나온 것을 담아
        /// <b>가장 오래된 벌</b>이 합쳐졌습니다. 목록은 회차 오름차순으로 오기 때문입니다.
        /// </para>
        /// </summary>
        private Dictionary<ImageViewType, string> BuildReferenceSourceFiles(IList<PartImage> images)
        {
            Dictionary<ImageViewType, string> sourceFiles = new Dictionary<ImageViewType, string>();
            if (images == null)
            {
                return sourceFiles;
            }

            Dictionary<ImageViewType, int> chosenSetNo = new Dictionary<ImageViewType, int>();
            foreach (PartImage image in images)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath) || !File.Exists(image.FilePath))
                {
                    continue;
                }

                if (!IsMergeView(image.ViewType))
                {
                    continue;
                }

                // 회차가 같으면 나중에 담긴 것을 씁니다.
                // 옛 자료에는 회차가 비어 있어 목록 순서가 유일한 단서입니다.
                if (!chosenSetNo.ContainsKey(image.ViewType) || image.SetNo >= chosenSetNo[image.ViewType])
                {
                    sourceFiles[image.ViewType] = image.FilePath;
                    chosenSetNo[image.ViewType] = image.SetNo;
                }
            }

            return sourceFiles;
        }

        private Dictionary<ImageViewType, string> BuildInspectionSourceFiles(IList<CapturedImage> images)
        {
            Dictionary<ImageViewType, string> sourceFiles = new Dictionary<ImageViewType, string>();
            if (images == null)
            {
                return sourceFiles;
            }

            foreach (CapturedImage image in images)
            {
                if (image == null || string.IsNullOrWhiteSpace(image.FilePath) || !File.Exists(image.FilePath))
                {
                    continue;
                }

                if (IsMergeView(image.ViewType) && !sourceFiles.ContainsKey(image.ViewType))
                {
                    sourceFiles.Add(image.ViewType, image.FilePath);
                }
            }

            return sourceFiles;
        }

        private bool TryGetCompleteSourceDirectory(
            IDictionary<ImageViewType, string> sourceFiles,
            out string directoryPath,
            out string message)
        {
            directoryPath = string.Empty;
            message = string.Empty;
            foreach (ImageViewType viewType in MergeViewOrder)
            {
                string filePath;
                if (!sourceFiles.TryGetValue(viewType, out filePath))
                {
                    message = "6방향 기준 이미지가 모두 있을 때 병합 이미지를 생성할 수 있습니다. 누락 위치: " + viewType;
                    return false;
                }

                string currentDirectoryPath = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(currentDirectoryPath))
                {
                    message = viewType + " 기준 이미지 폴더를 확인할 수 없습니다.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    directoryPath = currentDirectoryPath;
                }
                else if (!string.Equals(directoryPath, currentDirectoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    message = "6방향 기준 이미지가 서로 다른 폴더에 있어 병합할 수 없습니다.";
                    return false;
                }
            }

            return true;
        }

        private bool TryGetCompleteOutputDirectory(
            IDictionary<ImageViewType, string> sourceFiles,
            out string directoryPath,
            out string message)
        {
            directoryPath = string.Empty;
            message = string.Empty;
            foreach (ImageViewType viewType in MergeViewOrder)
            {
                string filePath;
                if (!sourceFiles.TryGetValue(viewType, out filePath))
                {
                    message = "검사 이미지 6장이 모두 있을 때 병합 이미지를 생성할 수 있습니다. 누락 위치: " + viewType;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    directoryPath = Path.GetDirectoryName(filePath);
                }
            }

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                message = "검사 이미지 병합 결과 폴더를 확인할 수 없습니다.";
                return false;
            }

            return true;
        }

        private void CopyInspectionSources(
            string partNo,
            IDictionary<ImageViewType, string> sourceFiles,
            string temporaryInputDirectoryPath)
        {
            string safePartNo = MakeSafeFileName(partNo);
            foreach (ImageViewType viewType in MergeViewOrder)
            {
                string sourceFilePath = sourceFiles[viewType];
                string extension = Path.GetExtension(sourceFilePath);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = ".png";
                }

                string targetFilePath = Path.Combine(
                    temporaryInputDirectoryPath,
                    safePartNo + "_" + viewType + extension);
                File.Copy(sourceFilePath, targetFilePath, true);
            }
        }

        private bool TryInvokeImageMerge(
            string inputDirectoryPath,
            string partNo,
            string outputDirectoryPath,
            out string mergedFilePath,
            out string message)
        {
            mergedFilePath = string.Empty;
            message = string.Empty;
            string temporaryOutputDirectoryPath = Path.Combine(
                outputDirectoryPath,
                ".VLAD_MergeOutput_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(outputDirectoryPath);
                Directory.CreateDirectory(temporaryOutputDirectoryPath);
                // 병합 안에서 크롭을 하므로 등록 핸들을 함께 넘깁니다.
                VLAD_Ops_Ai.VLAD_HD_ImageMerge(
                    VLAD_Ops_RTSP.GetActiveVladId(), inputDirectoryPath, partNo, temporaryOutputDirectoryPath);

                string temporaryMergedFilePath = FindMergedFilePath(temporaryOutputDirectoryPath, partNo);
                if (string.IsNullOrWhiteSpace(temporaryMergedFilePath))
                {
                    message = "VLAD_HD_ImageMerge 호출은 완료됐지만 품번 이름의 병합 이미지 파일을 찾지 못했습니다.";
                    return false;
                }

                mergedFilePath = Path.Combine(outputDirectoryPath, Path.GetFileName(temporaryMergedFilePath));
                File.Copy(temporaryMergedFilePath, mergedFilePath, true);
                DeleteOtherMergedFiles(outputDirectoryPath, partNo, mergedFilePath);
                message = "6방향 병합 이미지를 생성했습니다. " + mergedFilePath;
                return true;
            }
            catch (Exception exception)
            {
                message = "6방향 이미지 병합에 실패했습니다. " + exception.Message;
                return false;
            }
            finally
            {
                DeleteTemporaryDirectory(temporaryOutputDirectoryPath);
            }
        }

        private string FindMergedFilePath(string directoryPath, string partNo)
        {
            string safePartNo = MakeSafeFileName(partNo);
            foreach (string extension in MergedImageExtensions)
            {
                string candidatePath = Path.Combine(directoryPath, safePartNo + extension);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
            }

            return string.Empty;
        }

        private void DeleteMergedFiles(string directoryPath, string partNo)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            string safePartNo = MakeSafeFileName(partNo);
            foreach (string extension in MergedImageExtensions)
            {
                string filePath = Path.Combine(directoryPath, safePartNo + extension);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }

        private void DeleteOtherMergedFiles(string directoryPath, string partNo, string preservedFilePath)
        {
            string safePartNo = MakeSafeFileName(partNo);
            foreach (string extension in MergedImageExtensions)
            {
                string filePath = Path.Combine(directoryPath, safePartNo + extension);
                if (File.Exists(filePath) &&
                    !string.Equals(filePath, preservedFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(filePath);
                }
            }
        }

        private void DeleteDirectoryIfEmpty(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            if (Directory.GetFiles(directoryPath).Length == 0 &&
                Directory.GetDirectories(directoryPath).Length == 0)
            {
                Directory.Delete(directoryPath, false);
            }
        }

        private bool IsMergeView(ImageViewType viewType)
        {
            foreach (ImageViewType expectedViewType in MergeViewOrder)
            {
                if (viewType == expectedViewType)
                {
                    return true;
                }
            }

            return false;
        }

        private string MakeSafeFileName(string value)
        {
            string safeValue = string.IsNullOrWhiteSpace(value) ? "UNKNOWN" : value.Trim();
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                safeValue = safeValue.Replace(invalidCharacter, '_');
            }

            return safeValue;
        }

        private void DeleteTemporaryDirectory(string directoryPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(directoryPath) && Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }
            }
            catch
            {
                // 병합 입력 임시 폴더 정리 실패가 검사 결과 저장을 중단시키면 안 됩니다.
            }
        }
    }
}
