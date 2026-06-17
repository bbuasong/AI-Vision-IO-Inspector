using System;
using System.Threading.Tasks;

namespace ScannerSample.Services
{
    /// <summary>
    /// 스캔, 방향 보정, OCR, 코드 추출을 하나의 작업 흐름으로 묶습니다.
    /// </summary>
    public class ScannerWorkflowService
    {
        private readonly WiaScannerService _scannerService;
        private readonly ImageOrientationService _imageOrientationService;
        private readonly WindowsOcrTextReader _ocrTextReader;
        private readonly InspectionCodeExtractor _codeExtractor;

        public ScannerWorkflowService(ScanSettings settings)
        {
            _scannerService = new WiaScannerService(settings);
            _imageOrientationService = new ImageOrientationService();
            _ocrTextReader = new WindowsOcrTextReader();
            _codeExtractor = new InspectionCodeExtractor();
        }

        public async Task<ScanReadResult> ScanAndReadAsync()
        {
            string rawImagePath = string.Empty;

            try
            {
                rawImagePath = _scannerService.ScanToPng();
                return await ReadImageFileAsync(rawImagePath);
            }
            catch (Exception ex)
            {
                return ScanReadResult.CreateFailure("스캔 또는 OCR 처리 중 오류가 발생했습니다. " + ex.Message, rawImagePath, string.Empty);
            }
        }

        public async Task<ScanReadResult> ReadImageFileAsync(string imageFilePath)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath))
            {
                return ScanReadResult.CreateFailure("이미지 파일 경로가 없습니다.", string.Empty, string.Empty);
            }

            int[] rotations = new int[] { 0, 90, 180, 270 };
            string bestText = string.Empty;
            string bestCode = string.Empty;
            int bestRotation = 0;

            foreach (int rotation in rotations)
            {
                string candidateImagePath = string.Empty;

                try
                {
                    candidateImagePath = _imageOrientationService.CreateOcrCandidateImage(imageFilePath, rotation);
                    string ocrText = await _ocrTextReader.ReadTextAsync(candidateImagePath);
                    string code = _codeExtractor.ExtractCode(ocrText);

                    if (string.IsNullOrWhiteSpace(bestText) || ocrText.Length > bestText.Length)
                    {
                        bestText = ocrText;
                        bestRotation = rotation;
                    }

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        bestText = ocrText;
                        bestCode = code;
                        bestRotation = rotation;
                        break;
                    }
                }
                finally
                {
                    _imageOrientationService.DeleteQuietly(candidateImagePath);
                }
            }

            string finalImagePath = _imageOrientationService.SaveFinalUprightImage(imageFilePath, bestRotation);
            if (string.IsNullOrWhiteSpace(bestCode))
            {
                return ScanReadResult.CreateFailure("OCR은 수행했지만 품번 코드 형식 예: 31S7-12020을 찾지 못했습니다.", finalImagePath, bestText);
            }

            return ScanReadResult.CreateSuccess(bestCode, finalImagePath, bestRotation, bestText);
        }
    }
}
