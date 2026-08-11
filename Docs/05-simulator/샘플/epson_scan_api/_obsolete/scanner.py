"""
scanner.py  –  TWAIN 기반 스캐너 제어 (Epson Scan 2 호환)
WIA 대신 TWAIN 사용 — Epson Scan 2 드라이버가 TWAIN으로 등록됨

의존성: pip install twain
"""

from typing import Optional


def _open_source_manager():
    import twain
    return twain.SourceManager(0)


def get_scanner_list() -> list[dict]:
    """연결된 TWAIN 스캐너 목록 반환."""
    sm = _open_source_manager()
    try:
        sources = sm.GetSourceList()
        return [{"id": name, "name": name} for name in sources]
    finally:
        sm.destroy()


def scan_image(device_id: Optional[str] = None, dpi: int = 300) -> bytes:
    """
    TWAIN으로 스캔 실행 후 BMP bytes 반환.
    device_id=None 이면 첫 번째 스캐너 자동 선택.
    """
    import twain

    sm = _open_source_manager()
    try:
        sources = sm.GetSourceList()
        if not sources:
            raise ValueError("연결된 스캐너가 없습니다.")

        target = device_id if device_id in sources else sources[0]
        source = sm.OpenSource(target)

        try:
            # DPI 설정
            source.SetCapability(twain.ICAP_XRESOLUTION, twain.TWTY_FIX32, float(dpi))
            source.SetCapability(twain.ICAP_YRESOLUTION, twain.TWTY_FIX32, float(dpi))
            # 컬러 모드: RGB
            source.SetCapability(twain.ICAP_PIXELTYPE, twain.TWTY_UINT16, twain.TWPT_RGB)
            # 단위: 인치
            source.SetCapability(twain.ICAP_UNITS, twain.TWTY_UINT16, twain.TWUN_INCHES)

            source.RequestAcquire(0, 0)  # UI 없이 스캔
            rv, handle = source.XferImageNatively()

            # DIB handle → BMP bytes
            bmp_bytes = twain.DIBToBMFile(handle)
            twain.GlobalHandleFree(handle)
            return bmp_bytes

        finally:
            source.destroy()
    finally:
        sm.destroy()
