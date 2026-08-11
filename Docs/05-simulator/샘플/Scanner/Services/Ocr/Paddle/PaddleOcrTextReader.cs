using System;
using System.Threading.Tasks;
using OpenCvSharp;
using Sdcb.PaddleInference;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models.Local;
using ScannerSample.Services.Ocr.Common;

namespace ScannerSample.Services.Ocr.Paddle
{
    /// <summary>
    /// Sdcb.PaddleOCR 기반 OCR 엔진입니다. Python을 사용하지 않고 WPF 프로세스 안에서 실행합니다.
    /// </summary>
    public class PaddleOcrTextReader : IOcrTextReader, IDisposable
    {
        private readonly object _syncRoot;
        private PaddleOcrAll _ocr;
        private bool _disposed;

        public PaddleOcrTextReader()
        {
            _syncRoot = new object();
        }

        public string EngineName
        {
            get { return "PaddleOCR"; }
        }

        public Task<OcrTextReadResult> ReadAsync(string imageFilePath)
        {
            return Task.Run(delegate
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(imageFilePath))
                    {
                        return OcrTextReadResult.CreateSuccess(EngineName, string.Empty);
                    }

                    using (Mat source = Cv2.ImRead(imageFilePath, ImreadModes.Color))
                    {
                        if (source.Empty())
                        {
                            return OcrTextReadResult.CreateFailure(EngineName, "이미지 파일을 OpenCV Mat로 읽지 못했습니다.");
                        }

                        lock (_syncRoot)
                        {
                            PaddleOcrResult result = GetOrCreateOcr().Run(source);
                            return OcrTextReadResult.CreateSuccess(EngineName, result.Text);
                        }
                    }
                }
                catch (Exception ex)
                {
                    return OcrTextReadResult.CreateFailure(EngineName, ex.Message);
                }
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_ocr != null)
                {
                    _ocr.Dispose();
                    _ocr = null;
                }
            }

            _disposed = true;
        }

        private PaddleOcrAll GetOrCreateOcr()
        {
            if (_ocr != null)
            {
                return _ocr;
            }

            _ocr = new PaddleOcrAll(LocalFullModels.ChineseV5, ConfigurePaddleCpu)
            {
                AllowRotateDetection = true,
                Enable180Classification = true,
            };

            return _ocr;
        }

        private void ConfigurePaddleCpu(PaddleConfig config)
        {
            config.OneDnnEnabled = false;
            config.GLogEnabled = false;
            config.CpuMathThreadCount = 2;
        }
    }
}
