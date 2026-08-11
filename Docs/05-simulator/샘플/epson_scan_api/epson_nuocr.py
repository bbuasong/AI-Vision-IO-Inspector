"""
epson_nuocr.py  -  Epson 번들 OmniPage CSDK v18 (Kofax/Nuance) 커널 API 하니스
============================================================================
경로 확정 결과:
  - 엔진 = C:\\Program Files (x86)\\EPSON Software\\Scan OCR Cmponent Pro\\NuOCR\\
           KernelAPI.dll (+ RecoCore=THOCR 아시아 커널, 한국어).
  - 라이선스 = kRecSetLicenseW(epson.lcxz, "7609C")  -> rc 0 (확인됨).
       * "7609C" = OmniPage OEM publickey (epson.lcxz 내부 .lcx 파일명에서 추출).
  - 상위 RecAPIPlus(RecInitPlus)는 OEM 라이선스에 미포함(rc 0x8004C413). 사용 X.
       -> Epson 자체 툴 chrinfo.exe 도 RecAPIPlus 가 아니라 kRecInitW(커널)을 씀.
  - 따라서 정식 경로 = **커널 API**:
       kRecSetLicenseW -> kRecInitW -> kRecLoadImgFW -> kRecRecognizeW
       -> kRecGetLetters(글자+좌표) -> kRecFreeImg -> kRecQuit
    검색가능 PDF 는 글자+좌표로 우리가 합성(다음 단계).

함수 인자수는 디스어셈블 ret imm 로 확정:
  kRecSetLicenseW(2) kRecInitW(2) kRecQuit(0)
  kRecLoadImgFW(4) kRecRecognizeW(3) kRecFreeImg(1) kRecGetLetters(4)

** 32비트 파이썬 필수 **

사용:
  python epson_nuocr.py                 # 라이선스+초기화 확인만
  python epson_nuocr.py <이미지.bmp>    # + 인식해서 글자수/미리보기 출력
"""

import ctypes
import os
import struct
import sys
from ctypes import wintypes as wt
from pathlib import Path

NUOCR_DIR = Path(r"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\NuOCR")
DCP_DIR   = Path(r"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\DCP")
FFMT_DIR  = DCP_DIR / "ffmt"
KERNEL_DLL = NUOCR_DIR / "KernelAPI.dll"
LICENSE_FILE = str(NUOCR_DIR / "epson.lcxz")
# NuZonalOCRWrapper.dll!EngineHandle::Initialize 디스어셈블로 확정한 정식 시퀀스:
#   kRecSetLicenseW(<epson.lcxz경로>, "247ECFD6055D")
#   kRecInitW("Seiko Epson", "Document Capture")
LICENSE_CODE = "247ECFD6055D"     # 진짜 OEM publickey (ScanSmart가 쓰는 값)
INIT_USERKEY = "Seiko Epson"
INIT_COMPANY = "Document Capture"
LANG_KRN = 0x7A      # OmniPage LANGUAGES enum: 한국어 (chrinfo 디스어셈블로 확정)
LANG_ENG = 0x01      # 영어(통상값); 필요시 kRecManageLanguages(0,1,LANG_ENG)로 추가
REC_OK = 0


def ok(rc):
    """RECERR: 음수만 에러. 0 또는 양수(warning)는 성공."""
    return rc >= 0


def is_32bit():
    return struct.calcsize("P") == 4


def _setup_dll_dirs():
    for d in (NUOCR_DIR, DCP_DIR, FFMT_DIR):
        if d.exists() and hasattr(os, "add_dll_directory"):
            try: os.add_dll_directory(str(d))
            except Exception: pass
    os.environ["PATH"] = ";".join(str(d) for d in (NUOCR_DIR, DCP_DIR, FFMT_DIR)) + ";" + os.environ.get("PATH", "")
    try: os.chdir(str(NUOCR_DIR))
    except Exception: pass


def _bind(k):
    k.kRecSetLicenseW.argtypes = [wt.LPCWSTR, wt.LPCWSTR]; k.kRecSetLicenseW.restype = ctypes.c_int
    k.kRecInitW.argtypes = [wt.LPCWSTR, wt.LPCWSTR];       k.kRecInitW.restype = ctypes.c_int
    k.kRecQuit.argtypes = [];                              k.kRecQuit.restype = ctypes.c_int
    k.kRecLoadImgFW.argtypes = [ctypes.c_int, wt.LPCWSTR, ctypes.POINTER(ctypes.c_void_p), ctypes.c_int]
    k.kRecLoadImgFW.restype = ctypes.c_int
    k.kRecRecognizeW.argtypes = [ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p]
    k.kRecRecognizeW.restype = ctypes.c_int
    k.kRecFreeImg.argtypes = [ctypes.c_void_p];           k.kRecFreeImg.restype = ctypes.c_int
    # kRecGetLetters(HPAGE hPage, IMAGEINDEX ii, LETTER** ppLet, int* pnLet)
    k.kRecGetLetters.argtypes = [ctypes.c_void_p, ctypes.c_int,
                                 ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_int)]
    k.kRecGetLetters.restype = ctypes.c_int
    if hasattr(k, "kRecFree"):
        k.kRecFree.argtypes = [ctypes.c_void_p]; k.kRecFree.restype = ctypes.c_int
    # ScanSmart 정확도 재현용 (디스어셈블로 확정)
    k.kRecSetDefaults.argtypes = [ctypes.c_int]; k.kRecSetDefaults.restype = ctypes.c_int
    k.kRecManageLanguages.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_uint]
    k.kRecManageLanguages.restype = ctypes.c_int
    k.kRecPreprocessImg.argtypes = [ctypes.c_int, ctypes.c_void_p]
    k.kRecPreprocessImg.restype = ctypes.c_int
    # kRecGetLastErrorA(LPRECERR pErr, LPSTR buf, int* pLen) - 실패 사유 텍스트
    if hasattr(k, "kRecGetLastErrorA"):
        k.kRecGetLastErrorA.argtypes = [ctypes.POINTER(ctypes.c_int), ctypes.c_char_p, ctypes.POINTER(ctypes.c_int)]
        k.kRecGetLastErrorA.restype = ctypes.c_int
    return k


def last_error(k):
    """엔진의 마지막 에러 텍스트를 여러 인자배치로 시도해 출력."""
    if not hasattr(k, "kRecGetLastErrorA"):
        return
    # 배치1: (pErr, buf, &len)
    try:
        err = ctypes.c_int(0); buf = ctypes.create_string_buffer(512); ln = ctypes.c_int(512)
        rc = k.kRecGetLastErrorA(ctypes.byref(err), buf, ctypes.byref(ln))
        txt = buf.value.decode("ascii", "replace")
        print("    [lastError] rc=%d err=0x%X text=%r" % (rc, err.value & 0xFFFFFFFF, txt))
    except Exception as e:
        print("    [lastError] 배치1 예외 %r" % e)


def init_engine(k):
    rc = k.kRecSetLicenseW(LICENSE_FILE, LICENSE_CODE)
    print("[init] kRecSetLicenseW('%s','%s') rc=%d (0x%X)"
          % (Path(LICENSE_FILE).name, LICENSE_CODE, rc, rc & 0xFFFFFFFF))
    if not ok(rc):
        last_error(k); return False
    rc = k.kRecInitW(INIT_USERKEY, INIT_COMPANY)
    print("[init] kRecInitW('%s','%s') rc=%d (0x%X)"
          % (INIT_USERKEY, INIT_COMPANY, rc, rc & 0xFFFFFFFF))
    if not ok(rc):
        last_error(k); return False
    # ScanSmart 재현: 기본설정 초기화 + 언어를 '한국어'로 한정.
    #   kRecManageLanguages(sid, op, lang): op0=활성집합 리셋(이 언어), op1=추가.
    #   LANGUAGES enum (chrinfo 디스어셈블): LANG_KRN=0x7A, JPN=0x77, CHS=0x78, CHT=0x79.
    #   NuZonal 기본값은 전체 언어(0xFFFFFC00)라 한자/중국어가 섞임 -> 한국어로 고정.
    try:
        k.kRecSetDefaults(0)
        rcl = k.kRecManageLanguages(0, 0, LANG_KRN)   # 활성언어 = 한국어
        print("[init] kRecSetDefaults + 언어=한국어(0x%X) rc=%d" % (LANG_KRN, rcl))
    except Exception as e:
        print("[init] 언어/기본설정 적용 경고: %r" % e)
    print("       => 엔진 초기화 성공!")
    return True


# LETTER 구조체 (NuZonalOCRWrapper 디스어셈블로 확정): 56바이트, 좌표/코드 오프셋
LETTER_SIZE = 0x38
def _u16(b, off):
    return b[off] | (b[off + 1] << 8)


def _normalize_for_engine(img_path, force_dpi=0):
    """엔진 로더 호환 + 인식정확도용 정규화.
    - 1-bit/팔레트 등은 8-bit grayscale로 (이진화 금지: 엔진이 직접 함).
    - DPI는 인식 정확도에 직결. 원본 DPI 보존, 없거나 96이면 force_dpi(기본 300).
      (외부 자르기 툴이 DPI를 96으로 리셋/삭제하면 인식이 나빠짐.)
    """
    from PIL import Image
    im = Image.open(img_path)
    src_dpi = im.info.get("dpi")
    dx = int(src_dpi[0]) if (src_dpi and src_dpi[0]) else 0
    use = force_dpi or (dx if dx >= 150 else 300)   # 96 등 의심값이면 300으로
    print("[ocr] 원본 DPI=%s -> 사용 DPI=%d (mode=%s, size=%s)"
          % (src_dpi, use, im.mode, im.size))
    if im.mode not in ("L", "RGB"):
        im = im.convert("L")
    tmp = str(Path(img_path).with_suffix("")) + "._engine.bmp"
    im.save(tmp, "BMP", dpi=(use, use))
    return tmp


def recognize(k, img_path, ii=-3):
    """이미지 -> 인식 -> [(char, left, top, w, h), ...] 반환."""
    sid = 0
    # 엔진 로더 호환을 위해 정규화본을 먼저 시도, 실패 시 원본.
    candidates = []
    try:
        candidates.append(_normalize_for_engine(img_path))
    except Exception as e:
        print("[ocr] 정규화 실패(%r), 원본 사용" % e)
    candidates.append(str(img_path))

    hPage = ctypes.c_void_p(0); used = None
    for cand in candidates:
        hPage = ctypes.c_void_p(0)
        rc = k.kRecLoadImgFW(sid, cand, ctypes.byref(hPage), 0)
        print("[ocr] kRecLoadImgFW('%s') rc=%d (0x%X) hPage=%s"
              % (Path(cand).name, rc, rc & 0xFFFFFFFF, hPage.value))
        if ok(rc) and hPage.value:
            used = cand; break
        last_error(k)
    if not used:
        return None
    # ScanSmart 재현: 엔진 자체 전처리(디스큐/디스페클/적응 이진화) 후 인식
    rcp = k.kRecPreprocessImg(sid, hPage)
    print("[ocr] kRecPreprocessImg rc=%d (0x%X)" % (rcp, rcp & 0xFFFFFFFF))
    rc = k.kRecRecognizeW(sid, hPage, None)
    print("[ocr] kRecRecognizeW rc=%d (0x%X)" % (rc, rc & 0xFFFFFFFF))
    if not ok(rc):
        last_error(k); k.kRecFreeImg(hPage); return None

    pLet = ctypes.c_void_p(0); n = ctypes.c_int(0)
    rc = k.kRecGetLetters(hPage, ii, ctypes.byref(pLet), ctypes.byref(n))
    print("[ocr] kRecGetLetters(ii=%d) rc=%d n=%d" % (ii, rc, n.value))
    letters = []
    if ok(rc) and n.value > 0 and pLet.value:
        raw = ctypes.string_at(pLet.value, n.value * LETTER_SIZE)
        for i in range(n.value):
            b = raw[i * LETTER_SIZE:(i + 1) * LETTER_SIZE]
            if len(b) < LETTER_SIZE:
                break
            code = _u16(b, 0x12)
            left = _u16(b, 0x00); top = _u16(b, 0x02)
            w = _u16(b, 0x04); h = _u16(b, 0x06)
            letters.append((code, left, top, w, h))
    try: k.kRecFreeImg(hPage)
    except Exception: pass
    return letters


def letters_to_text(letters):
    """좌표로 줄/띄어쓰기 재구성 (ScanSmart 처럼 라인별 정렬)."""
    items = [(c, l, t, w, h) for (c, l, t, w, h) in letters if c]
    if not items:
        return ""
    hs = sorted(h for _, _, _, _, h in items if h > 0)
    medh = hs[len(hs) // 2] if hs else 12
    # 읽기순서: top(라인) -> left
    items.sort(key=lambda x: (round(x[2] / max(medh * 0.6, 1)), x[1]))
    lines, cur, last_top, last_right = [], [], None, None
    for c, l, t, w, h in items:
        if last_top is None or abs(t - last_top) <= medh * 0.6:
            if last_right is not None and l - last_right > medh * 0.6:
                cur.append(" ")
            cur.append(chr(c))
        else:
            lines.append("".join(cur)); cur = [chr(c)]
        last_top = t if last_top is None else (last_top if abs(t-last_top)<=medh*0.6 else t)
        last_right = l + w
    if cur:
        lines.append("".join(cur))
    return "\n".join(lines)


def build_searchable_pdf(img_path, letters, out_pdf):
    """원본 이미지 위에 투명 텍스트레이어를 얹어 검색가능 PDF 생성.
    좌표계: LETTER 좌표 = 인식 이미지 픽셀. PDF 페이지를 픽셀=포인트로 두면 1:1 정렬."""
    from PIL import Image
    from reportlab.pdfgen import canvas
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.cidfonts import UnicodeCIDFont

    FONT = "HYSMyeongJo-Medium"   # reportlab 내장 한국어 CID 폰트(투명레이어용)
    try:
        pdfmetrics.registerFont(UnicodeCIDFont(FONT))
    except Exception as e:
        print("[pdf] 한국어 폰트 등록 실패(%r) -> Helvetica로 대체(검색은 됨)" % e)
        FONT = "Helvetica"

    im = Image.open(img_path)
    iw, ih = im.size
    c = canvas.Canvas(str(out_pdf), pagesize=(iw, ih))
    c.drawImage(str(img_path), 0, 0, width=iw, height=ih)
    c.setFillAlpha(0)                     # 투명 (보이지 않음)
    placed = 0
    for code, left, top, w, h in letters:
        if not code or code in (0x20, 0xFFFF):
            continue
        ch = chr(code)
        size = max(h, 4)
        try:
            c.setFont(FONT, size)
            c.drawString(left, ih - top - h, ch)   # PDF 원점=좌하단
            placed += 1
        except Exception:
            pass
    c.showPage()
    c.save()
    p = Path(out_pdf)
    okp = p.exists() and p.stat().st_size > 0
    print("[pdf] %s (%d바이트, 글자 %d개 레이어) -> %s"
          % (p.name, p.stat().st_size if okp else 0, placed,
             "성공: Ctrl+F/선택 확인" if okp else "실패"))


def main():
    print("=== Epson NuOCR 커널 API 하니스 ===")
    print("python bits:", 32 if is_32bit() else 64)
    if not is_32bit():
        print("[STOP] 32비트 파이썬 필요"); return 2
    print("DLL:", KERNEL_DLL, "exists=", KERNEL_DLL.exists())
    if not KERNEL_DLL.exists():
        print("[STOP] KernelAPI.dll 없음"); return 2
    _setup_dll_dirs()
    try:
        k = ctypes.WinDLL(str(KERNEL_DLL))
    except OSError as e:
        print("[STOP] 로드 실패 %r" % e); return 2
    _bind(k)

    if not init_engine(k):
        print("\n[결과] kRecInitW 실패. 위 rc 공유해줘.")
        return 1
    print("\n[결과] 라이선스+초기화 성공. 엔진 사용 가능.")

    args = sys.argv[1:]
    if args:
        img = args[0]
        out_pdf = args[1] if len(args) > 1 else None
        if not Path(img).exists():
            print("입력 이미지 없음:", img)
        else:
            print("\n--- 인식: %s ---" % img)
            letters = recognize(k, img)
            if letters:
                text = letters_to_text(letters)
                print("[ocr] 글자 %d개. 텍스트 미리보기:" % len(letters))
                print("    >>> " + text[:200])
                if out_pdf:
                    build_searchable_pdf(img, letters, out_pdf)
                else:
                    print("(2번째 인자로 출력 PDF 경로를 주면 검색가능 PDF 생성)")
    else:
        print("(사용: python epson_nuocr.py <img.bmp> [out.pdf])")

    try: k.kRecQuit()
    except Exception: pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
