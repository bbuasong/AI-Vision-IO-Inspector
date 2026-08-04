using System.Collections.Generic;
using System.Threading.Tasks;
using AI.Vision.IOInspector.Application.Models;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// Epson ES-C320W의 WIA 스캔과 로컬 Epson OCR 작업자 호출을 분리한 인터페이스입니다.
    /// </summary>
    public interface IOcrScanService
    {
        OcrScanConfiguration LoadConfiguration();

        void SaveConfiguration(OcrScanConfiguration configuration);

        IList<OcrScannerDevice> RefreshScanners();

        Task<OcrScanExecutionResult> ScanAsync(OcrScanConfiguration configuration);

        /// <summary>
        /// OCR 사용 목적에 맞는 저장 경로로 스캔과 인식을 수행합니다.
        /// 등록 OCR은 OCR_PATH의 임시 파일을 사용하고, 검사 OCR은 OUTPUT_PATH의 이력 경로를 사용합니다.
        /// </summary>
        Task<OcrScanExecutionResult> ScanAsync(OcrScanConfiguration configuration, OcrScanUsage usage);

        /// <summary>
        /// 등록 OCR에서 생성한 임시 이미지와 OCR 결과 JSON을 삭제합니다.
        /// OCR_PATH 하위 파일만 삭제하여 다른 검사 이력 파일에는 영향을 주지 않습니다.
        /// </summary>
        void DeleteTemporaryFiles(OcrScanExecutionResult result);
    }
}
