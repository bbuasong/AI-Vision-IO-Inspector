"""
ocr.py  -  OCR engine wrapper (rewritten)

The previous version failed for 3 real reasons (all fixed here):
  1. The called function names (OcrInit2 / OcrExecuteDoc2 / ...) do NOT exist
     in the DLL. Real exports: Ocrsys.dll = Ynd* family, EsOCR.dll = Esocr_*.
     dll.OcrInit2 raised AttributeError -> silently fell back to Tesseract.
  2. Ocrsys.dll / EsOCR.dll are 32-bit (x86). 64-bit Python cannot load them
     at all (WinError 193). -> must run 32-bit Python.
  3. Language code was guessed as Windows LCID (0x0412) -> unrelated to engine.

Engine priority:
  1. Epson EsOCR.dll  (only when EPSON_OCR=1; requires 32-bit Python)
  2. Tesseract        (default / fallback - actually usable today)

EsOCR.dll signatures were recovered by disassembly (see EsOCR_FINDINGS.md);
verify behaviour on the real PC (32-bit Python) with diagnose.py.
"""

import ctypes
import ctypes.wintypes as wt
import os
import struct
from pathlib import Path

from PIL import Image

# --- paths ---
_ESCNOCR_DIR = Path(r"C:\Program Files (x86)\epson\ESCNOCR")
_ESOCR_DLL   = _ESCNOCR_DIR / "EsOCR.dll"
_OCRSYS_DLL  = _ESCNOCR_DIR / "Ocrsys.dll"
_OCRLIB_DIR  = _ESCNOCR_DIR / "ocrlib"

# Use EsOCR only when explicitly enabled; default engine is Tesseract.
_USE_EPSON = os.getenv("EPSON_OCR", "0") == "1"


def python_is_32bit() -> bool:
    return struct.calcsize("P") == 4  # 4-byte pointer = 32-bit


# --- Epson EsOCR.dll wrapper ---
_esocr = None
_esocr_status = None  # None=not tried, str=failure reason, "ok"=loaded


def _load_esocr():
    """Load EsOCR.dll and set recovered signatures. Returns True on success."""
    global _esocr, _esocr_status
    if _esocr_status is not None:
        return _esocr_status == "ok"

    if not _ESOCR_DLL.exists():
        _esocr_status = f"DLL not found: {_ESOCR_DLL}"
        return False

    if not python_is_32bit():
        _esocr_status = (
            "Python is 64-bit. EsOCR.dll/Ocrsys.dll are 32-bit and cannot load. "
            "Run under 32-bit Python."
        )
        return False

    try:
        # Add dependency search dirs (ocrlib has Ydrec*, Mem_pdf, etc.)
        if hasattr(os, "add_dll_directory"):
            os.add_dll_directory(str(_ESCNOCR_DIR))
            if _OCRLIB_DIR.exists():
                os.add_dll_directory(str(_OCRLIB_DIR))
        os.environ["PATH"] = f"{_ESCNOCR_DIR};{_OCRLIB_DIR};" + os.environ.get("PATH", "")

        dll = ctypes.WinDLL(str(_ESOCR_DLL))  # stdcall

        # Recovered signatures (EsOCR_FINDINGS.md). All __stdcall.
        # Arg counts are confirmed via 'ret imm'; some types are inferred.

        # Esocr_Init(p) -> int  (1 arg)
        dll.Esocr_Init.argtypes = [ctypes.c_void_p]
        dll.Esocr_Init.restype = ctypes.c_int

        # Esocr_End(p) -> int  (1 arg)
        dll.Esocr_End.argtypes = [ctypes.c_void_p]
        dll.Esocr_End.restype = ctypes.c_int

        # Esocr_GetVer(WORD* pVer) -> int : writes version into *pVer  (1 arg)
        dll.Esocr_GetVer.argtypes = [ctypes.POINTER(ctypes.c_ushort)]
        dll.Esocr_GetVer.restype = ctypes.c_int

        # Esocr_SetHandle(handle) : stores engine handle in global  (1 arg)
        #   NOTE: this handle is likely the Ocrsys.dll YndInit handle.
        dll.Esocr_SetHandle.argtypes = [ctypes.c_void_p]
        dll.Esocr_SetHandle.restype = ctypes.c_int

        # Esocr_GetLangInfo(void* out16, int idx) -> int  (2 args)
        #   out16: 16-byte (4 DWORD) buffer zeroed then filled.
        dll.Esocr_GetLangInfo.argtypes = [ctypes.c_void_p, ctypes.c_int]
        dll.Esocr_GetLangInfo.restype = ctypes.c_int

        # Esocr_SetRecogFile(a1, WORD angle, LPCWSTR str, a4, a5) -> int  (5 args)
        #   angle: normalized to 0/90/180/270. 3rd arg is a wide string
        #   (empty-check via *(WORD*)==0). a1/a4/a5 meaning TBD (see FINDINGS).
        dll.Esocr_SetRecogFile.argtypes = [
            ctypes.c_void_p, ctypes.c_ushort, wt.LPCWSTR,
            ctypes.c_void_p, ctypes.c_void_p,
        ]
        dll.Esocr_SetRecogFile.restype = ctypes.c_int

        # Esocr_SetPDFInfo(a1, WORD* xy, a3, a4, WORD quality0_100, a6,a7,a8,a9)  (9 args)
        dll.Esocr_SetPDFInfo.argtypes = [
            ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p,
            ctypes.c_ushort,
            ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p,
        ]
        dll.Esocr_SetPDFInfo.restype = ctypes.c_int

        _esocr = dll
        _esocr_status = "ok"
        return True

    except Exception as e:
        _esocr_status = f"load exception: {e}"
        return False


def esocr_probe() -> dict:
    """Safe probe: load + Esocr_GetVer. Used by diagnose.py."""
    info = {
        "python_bits": 32 if python_is_32bit() else 64,
        "esocr_path": str(_ESOCR_DLL),
        "exists": _ESOCR_DLL.exists(),
    }
    if not _load_esocr():
        info["loaded"] = False
        info["reason"] = _esocr_status
        return info
    info["loaded"] = True
    try:
        ver = ctypes.c_ushort(0)
        rc = _esocr.Esocr_GetVer(ctypes.byref(ver))
        info["GetVer_rc"] = rc
        info["version_raw"] = hex(ver.value)
    except Exception as e:
        info["GetVer_error"] = str(e)
    return info


def _run_epson_ocr(img: "Image.Image", lang: str) -> str:
    """
    OCR via EsOCR.dll.
    a1/a4/a5 of SetRecogFile are not yet confirmed, so this path is
    experimental. Verify return values on the real PC with diagnose.py,
    then implement. For now raise NotImplemented to trigger Tesseract.
    """
    raise NotImplementedError(
        "EsOCR full OCR sequence pending SetRecogFile arg confirmation "
        "(verify with diagnose.py). Using Tesseract for now."
    )


# --- Tesseract (real working engine) ---
_TESS_CANDIDATES = [
    r"C:\Program Files\Tesseract-OCR\tesseract.exe",
    r"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
    str(Path(os.getenv("LOCALAPPDATA", "")) / "Programs" / "Tesseract-OCR" / "tesseract.exe"),
]


def _tesseract_exe():
    for p in _TESS_CANDIDATES:
        if p and Path(p).exists():
            return p
    return None  # rely on PATH


def _run_tesseract_ocr(img: "Image.Image", lang: str) -> str:
    import pytesseract
    exe = _tesseract_exe()
    if exe:
        pytesseract.pytesseract.tesseract_cmd = exe
    return pytesseract.image_to_string(img, lang=lang)


# --- public interface ---
def ocr_engine_name() -> str:
    if _USE_EPSON and _load_esocr():
        return "Epson EsOCR.dll"
    return "Tesseract"


def run_ocr(img: "Image.Image", lang: str = "kor+eng") -> str:
    """Image -> text. EsOCR first if EPSON_OCR=1 and loaded, else Tesseract."""
    if _USE_EPSON and _load_esocr():
        try:
            return _run_epson_ocr(img, lang)
        except Exception as e:
            print(f"[OCR] EsOCR path failed -> Tesseract fallback: {e}")
    return _run_tesseract_ocr(img, lang)
