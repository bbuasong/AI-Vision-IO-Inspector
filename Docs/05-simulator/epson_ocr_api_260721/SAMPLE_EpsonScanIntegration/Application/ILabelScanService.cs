using System.Threading;
using System.Threading.Tasks;

namespace AI.Vision.IOInspector.Application.Interfaces
{
    /// <summary>
    /// 라벨 스캔(스캐너 → OCR → 부품번호 추출) 호출 경계입니다.
    /// 실제 스캐너 연동 방식(현재는 Epson Scan API HTTP)이 바뀌어도 구현체만 교체하면
    /// UI / 검사 흐름은 그대로 재사용할 수 있습니다.
    /// (참고: 기존 ICameraService / IAiInferenceService 와 동일한 인터페이스 경계 패턴입니다.)
    /// </summary>
    public interface ILabelScanService
    {
        /// <summary>
        /// 스캐너에 올려둔 라벨 1장을 스캔하고 OCR하여 부품번호(part_no)를 추출합니다.
        /// 스캐너 제어 / OCR 은 외부 프로세스(Epson Scan API)에서 수행되므로 네트워크 I/O 입니다.
        /// 결과의 NeedsConfirmation 이 true 이면, 추출값이 비어있거나 품질이 낮으니
        /// 작업자에게 확인/수정을 받은 뒤 검사를 진행하세요.
        /// </summary>
        Task<LabelScanResult> ScanPartNoAsync(CancellationToken cancellationToken);
    }
}
