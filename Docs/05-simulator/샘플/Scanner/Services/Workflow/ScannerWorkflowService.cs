using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ScannerSample.Services.ImageProcessing;
using ScannerSample.Services.Ocr.CSharpEpsonApi;
using ScannerSample.Services.Ocr.Common;
using ScannerSample.Services.Ocr.Paddle;
using ScannerSample.Services.Ocr.WindowsBuiltIn;
using ScannerSample.Services.Scanning;

namespace ScannerSample.Services.Workflow
{
    /// <summary>
    /// 스캔, PNG 저장, 방향 보정, 품번 영역 crop, OCR 전처리, OCR, 품번 추출을 순서대로 수행합니다.
    /// </summary>
    public class ScannerWorkflowService
    {
        private readonly WiaScannerService _scannerService;
        private readonly ImageOrientationService _imageOrientationService;
        private readonly IOcrTextReader _orientationOcrTextReader;
        private readonly IList<OcrEngineRunner> _ocrReaders;
        private readonly InspectionCodeExtractor _codeExtractor;

        public ScannerWorkflowService(ScanSettings settings)
        {
            _scannerService = new WiaScannerService(settings);
            _imageOrientationService = new ImageOrientationService();
            _codeExtractor = new InspectionCodeExtractor();
            _orientationOcrTextReader = new WindowsOcrTextReader();
            _ocrReaders = CreateOcrReaders();
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
                _scannerService.ResetScannerSession();
                return ScanReadResult.CreateFailure("스캔 또는 OCR 처리 중 오류가 발생했습니다. 장비 연결 상태를 정리했으므로 다시 스캔을 시도할 수 있습니다. " + ex.Message, rawImagePath, string.Empty);
            }
        }

        public async Task<ScanReadResult> ReadImageFileAsync(string imageFilePath)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath))
            {
                return ScanReadResult.CreateFailure("이미지 파일 경로가 없습니다.", string.Empty, string.Empty);
            }

            if (!File.Exists(imageFilePath))
            {
                return ScanReadResult.CreateFailure("이미지 파일을 찾을 수 없습니다. " + imageFilePath, string.Empty, string.Empty);
            }

            OrientationDecision orientation = await DecideUprightOrientationAsync(imageFilePath);
            string labelImagePath = _imageOrientationService.SaveUprightLabelImage(imageFilePath, orientation.RotationAngle);
            string partNumberImagePath = _imageOrientationService.SavePartNumberAreaImage(labelImagePath);
            string enhancedOcrImagePath = _imageOrientationService.SaveEnhancedOcrInputImage(partNumberImagePath);

            Dictionary<string, string> stageImages = new Dictionary<string, string>();
            stageImages["Raw"] = imageFilePath;
            stageImages["Label"] = labelImagePath;
            stageImages["PartNo"] = partNumberImagePath;
            stageImages["Enhanced"] = enhancedOcrImagePath;
            return await ReadAllEnginesWithCandidatesAsync(stageImages, orientation.RotationAngle);

        }

        private async Task<ScanReadResult> ReadAllEnginesWithCandidatesAsync(IDictionary<string, string> stageImages, int rotationAngle)
        {
            string defaultImagePath = stageImages.ContainsKey("Enhanced") ? stageImages["Enhanced"] : string.Empty;
            ScanReadResult result = ScanReadResult.CreateFailure("OCR result did not contain a part number.", defaultImagePath, string.Empty);
            result.RotationAngle = rotationAngle;

            StringBuilder diagnostics = new StringBuilder();

            for (int i = 0; i < _ocrReaders.Count; i++)
            {
                OcrEngineRunner runner = _ocrReaders[i];
                OcrEngineReadResult bestEngineResult = null;
                string[] stageOrder = GetCandidateStageOrder(runner.SlotKey);

                for (int stageIndex = 0; stageIndex < stageOrder.Length; stageIndex++)
                {
                    string stageName = stageOrder[stageIndex];
                    if (!stageImages.ContainsKey(stageName))
                    {
                        continue;
                    }

                    string inputImagePath = _imageOrientationService.SaveEngineInputImage(stageImages[stageName], runner.SlotKey, stageName);
                    OcrTextReadResult ocrResult;

                    try
                    {
                        ocrResult = await runner.Reader.ReadAsync(inputImagePath);
                    }
                    catch (Exception ex)
                    {
                        ocrResult = OcrTextReadResult.CreateFailure(runner.DisplayName, ex.Message);
                    }

                    string code = string.IsNullOrWhiteSpace(ocrResult.ExtractedCode)
                        ? _codeExtractor.ExtractCode(ocrResult.Text)
                        : ocrResult.ExtractedCode;

                    OcrEngineReadResult engineResult = OcrEngineReadResult.FromOcrResult(runner.SlotKey, runner.DisplayName, ocrResult, code);
                    string inputDiagnostics = "Stage=" + stageName + "; Input=" + inputImagePath;
                    engineResult.Diagnostics = string.IsNullOrWhiteSpace(engineResult.Diagnostics)
                        ? inputDiagnostics
                        : inputDiagnostics + "; " + engineResult.Diagnostics;

                    diagnostics.Append(runner.DisplayName);
                    diagnostics.Append("[");
                    diagnostics.Append(stageName);
                    diagnostics.Append("]: ");
                    diagnostics.Append(engineResult.DisplayText);
                    diagnostics.Append(" / Input=");
                    diagnostics.Append(inputImagePath);
                    if (!string.IsNullOrWhiteSpace(engineResult.ErrorMessage))
                    {
                        diagnostics.Append(" / ");
                        diagnostics.Append(engineResult.ErrorMessage);
                    }

                    diagnostics.AppendLine();

                    if (bestEngineResult == null || (!bestEngineResult.IsSuccess && engineResult.IsSuccess))
                    {
                        bestEngineResult = engineResult;
                    }

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        bestEngineResult = engineResult;
                        if (string.IsNullOrWhiteSpace(result.CodeText))
                        {
                            result.IsSuccess = true;
                            result.CodeText = code;
                            result.OcrEngineName = runner.DisplayName;
                            result.OcrText = ocrResult.Text;
                            result.ImageFilePath = inputImagePath;
                            result.Message = "Text was read.";
                        }

                        break;
                    }

                    if (string.IsNullOrWhiteSpace(result.OcrText) && !string.IsNullOrWhiteSpace(ocrResult.Text))
                    {
                        result.OcrText = ocrResult.Text;
                    }
                }

                result.EngineResults.Add(bestEngineResult ?? new OcrEngineReadResult { SlotKey = runner.SlotKey, DisplayName = runner.DisplayName });
            }

            result.OcrDiagnostics = diagnostics.ToString();
            return result;
        }

        private string[] GetCandidateStageOrder(string slotKey)
        {
            if (slotKey == OcrEngineSlots.CSharpEpsonApi)
            {
                return new string[] { "Raw", "Label", "PartNo", "Enhanced" };
            }

            return new string[] { "Enhanced", "PartNo", "Label", "Raw" };
        }

        private IDictionary<string, string> CreateEngineInputImages(string sourceImagePath, string stageName)
        {
            Dictionary<string, string> paths = new Dictionary<string, string>();
            for (int i = 0; i < _ocrReaders.Count; i++)
            {
                OcrEngineRunner runner = _ocrReaders[i];
                paths[runner.SlotKey] = _imageOrientationService.SaveEngineInputImage(sourceImagePath, runner.SlotKey, stageName);
            }

            return paths;
        }

        private async Task<ScanReadResult> TryReadAllEnginesAsync(IDictionary<string, string> imageFilePathsBySlot, string defaultImageFilePath, int rotationAngle)
        {
            string imageFilePath = defaultImageFilePath;
            ScanReadResult result = ScanReadResult.CreateFailure("OCR 결과에서 괄호 앞 품번을 찾지 못했습니다.", imageFilePath, string.Empty);
            result.RotationAngle = rotationAngle;

            StringBuilder diagnostics = new StringBuilder();

            for (int i = 0; i < _ocrReaders.Count; i++)
            {
                OcrEngineRunner runner = _ocrReaders[i];
                OcrTextReadResult ocrResult;
                imageFilePath = defaultImageFilePath;
                if (imageFilePathsBySlot != null && imageFilePathsBySlot.ContainsKey(runner.SlotKey))
                {
                    imageFilePath = imageFilePathsBySlot[runner.SlotKey];
                }

                try
                {
                    ocrResult = await runner.Reader.ReadAsync(imageFilePath);
                }
                catch (Exception ex)
                {
                    ocrResult = OcrTextReadResult.CreateFailure(runner.DisplayName, ex.Message);
                }

                string code = string.IsNullOrWhiteSpace(ocrResult.ExtractedCode)
                    ? _codeExtractor.ExtractCode(ocrResult.Text)
                    : ocrResult.ExtractedCode;

                OcrEngineReadResult engineResult = OcrEngineReadResult.FromOcrResult(runner.SlotKey, runner.DisplayName, ocrResult, code);
                result.EngineResults.Add(engineResult);

                if (string.IsNullOrWhiteSpace(result.OcrText) && !string.IsNullOrWhiteSpace(ocrResult.Text))
                {
                    result.OcrText = ocrResult.Text;
                }

                diagnostics.Append(runner.DisplayName);
                diagnostics.Append(": ");
                diagnostics.Append(engineResult.DisplayText);
                diagnostics.Append(" / Input=");
                diagnostics.Append(imageFilePath);
                if (!string.IsNullOrWhiteSpace(engineResult.ErrorMessage))
                {
                    diagnostics.Append(" / ");
                    diagnostics.Append(engineResult.ErrorMessage);
                }
                diagnostics.AppendLine();

                if (!string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(result.CodeText))
                {
                    result.IsSuccess = true;
                    result.CodeText = code;
                    result.OcrEngineName = runner.DisplayName;
                    result.OcrText = ocrResult.Text;
                    result.Message = "텍스트를 읽었습니다.";
                }
            }

            result.OcrDiagnostics = diagnostics.ToString();
            return result;
        }

        private async Task<OrientationDecision> DecideUprightOrientationAsync(string imageFilePath)
        {
            int[] rotations = new int[] { 0, 90, 180, 270 };
            OrientationDecision bestDecision = new OrientationDecision(0, int.MinValue, string.Empty, string.Empty);

            foreach (int rotation in rotations)
            {
                string candidateImagePath = string.Empty;

                try
                {
                    candidateImagePath = _imageOrientationService.CreateOcrCandidateImage(imageFilePath, rotation);
                    OcrTextReadResult ocrResult = await _orientationOcrTextReader.ReadAsync(candidateImagePath);
                    string ocrText = ocrResult.Text;
                    string code = _codeExtractor.ExtractCode(ocrText);
                    int score = CalculateOrientationScore(ocrText, code);

                    if (score > bestDecision.Score)
                    {
                        bestDecision = new OrientationDecision(rotation, score, code, ocrText);
                    }
                }
                finally
                {
                    _imageOrientationService.DeleteQuietly(candidateImagePath);
                }
            }

            return bestDecision;
        }

        private IList<OcrEngineRunner> CreateOcrReaders()
        {
            List<OcrEngineRunner> readers = new List<OcrEngineRunner>();
            readers.Add(new OcrEngineRunner(
                OcrEngineSlots.PaddleOcr,
                "Sdcb.PaddleOCR",
                new PaddleOcrTextReader()));
            readers.Add(new OcrEngineRunner(
                OcrEngineSlots.WindowsBuiltIn,
                "Windows Built-in",
                new WindowsOcrTextReader()));
            readers.Add(new OcrEngineRunner(
                OcrEngineSlots.CSharpEpsonApi,
                "C# Epson API",
                new CSharpEpsonApiOcrTextReader()));
            return readers;
        }

        private int CalculateOrientationScore(string ocrText, string code)
        {
            string text = string.IsNullOrWhiteSpace(ocrText) ? string.Empty : ocrText.ToUpperInvariant();
            int score = 0;

            if (!string.IsNullOrWhiteSpace(code))
            {
                score += 100;
            }

            if (text.Contains("RCV"))
            {
                score += 25;
            }

            if (text.Contains("WORKING"))
            {
                score += 25;
            }

            if (text.Contains("WALVOIL"))
            {
                score += 20;
            }

            if (text.Contains("IT0003"))
            {
                score += 10;
            }

            if (text.Contains("AOU") || text.Contains("OSL-OGL") || text.Contains("LOOOV") || text.Contains("TOLOO"))
            {
                score -= 40;
            }

            score += Math.Min(text.Length, 120) / 20;
            return score;
        }

        private class OrientationDecision
        {
            public OrientationDecision(int rotationAngle, int score, string codeText, string ocrText)
            {
                RotationAngle = rotationAngle;
                Score = score;
                CodeText = codeText;
                OcrText = ocrText;
            }

            public int RotationAngle { get; private set; }

            public int Score { get; private set; }

            public string CodeText { get; private set; }

            public string OcrText { get; private set; }
        }
    }
}
