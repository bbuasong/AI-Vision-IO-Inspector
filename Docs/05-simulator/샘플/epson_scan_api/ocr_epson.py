"""
ocr_epson.py  -  Epson 번들 OmniPage CSDK(=Kofax/Nuance) 엔진 래퍼 (importable)
==============================================================================
ScanSmart 와 동일한 엔진/시퀀스로 이미지 -> 검색가능 PDF.

핵심 사실 (NUANCE_FINDINGS.md, 디스어셈블로 확정):
  - 엔진 DLL: C:\\Program Files (x86)\\EPSON Software\\Scan OCR Cmponent Pro\\NuOCR\\KernelAPI.dll
  - 라이선스: kRecSetLicenseW(epson.lcxz, "247ECFD6055D")  + kRecInitW("Seiko Epson","Document Capture")
  - 언어: kRecManageLanguages(0, 0, 0x7A)  (LANG_KRN = 한국어)
  - 인식: kRecPreprocessImg -> kRecRecognizeW -> kRecGetLetters
  - LETTER(56바이트): +0 left, +2 top, +4 width, +6 height, +0x12 code(UTF-16)
  - 32비트 파이썬 필수.

interface:
  available() -> (bool, reason)
  image_to_searchable_pdf(images, out_pdf, lang="kor") -> info dict   # ocr_pdf 호환
  image_to_text(image, lang="kor") -> str
"""

import ctypes
import os
import struct
from ctypes import wintypes as wt
from pathlib import Path

# ---- 경로/상수 ----
NUOCR_DIR = Path(r"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\NuOCR")
DCP_DIR   = Path(r"C:\Program Files (x86)\EPSON Software\Scan OCR Cmponent Pro\DCP")
FFMT_DIR  = DCP_DIR / "ffmt"
KERNEL_DLL = NUOCR_DIR / "KernelAPI.dll"
LICENSE_FILE = str(NUOCR_DIR / "epson.lcxz")
LICENSE_CODE = "247ECFD6055D"
INIT_USERKEY = "Seiko Epson"
INIT_COMPANY = "Document Capture"
LANG_KRN = 0x7A
# 주의: enum에서 0은 '영어'가 아니라 '전체 언어(ALL)'로 보임 -> 추가하면 CJK 오염됨. 사용 금지.
# (참고 enum: GER=1, FRE=2, DUT=3, POR=9, SPA=10, ITA=13, JPN=0x77, CHS=0x78, KRN=0x7A)
LETTER_SIZE = 0x38
DEFAULT_DPI = 300

class EpsonOcrError(Exception):
    pass

_K = None            # WinDLL
_STATUS = None       # None=미시도, "ok", 또는 실패사유
_INITED = False


def python_is_32bit():
    return struct.calcsize("P") == 4


def _ok(rc):
    return rc >= 0


def _setup_dirs():
    for d in (NUOCR_DIR, DCP_DIR, FFMT_DIR):
        if d.exists() and hasattr(os, "add_dll_directory"):
            try: os.add_dll_directory(str(d))
            except Exception: pass
    os.environ["PATH"] = ";".join(str(d) for d in (NUOCR_DIR, DCP_DIR, FFMT_DIR)) + ";" + os.environ.get("PATH", "")


def _bind(k):
    sig = {
        "kRecSetLicenseW": ([wt.LPCWSTR, wt.LPCWSTR], ctypes.c_int),
        "kRecInitW":       ([wt.LPCWSTR, wt.LPCWSTR], ctypes.c_int),
        "kRecQuit":        ([], ctypes.c_int),
        "kRecSetDefaults": ([ctypes.c_int], ctypes.c_int),
        "kRecManageLanguages": ([ctypes.c_int, ctypes.c_int, ctypes.c_uint], ctypes.c_int),
        "kRecLoadImgFW":   ([ctypes.c_int, wt.LPCWSTR, ctypes.POINTER(ctypes.c_void_p), ctypes.c_int], ctypes.c_int),
        "kRecPreprocessImg": ([ctypes.c_int, ctypes.c_void_p], ctypes.c_int),
        "kRecRecognizeW":  ([ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p], ctypes.c_int),
        "kRecGetLetters":  ([ctypes.c_void_p, ctypes.c_int, ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_int)], ctypes.c_int),
        "kRecFreeImg":     ([ctypes.c_void_p], ctypes.c_int),
    }
    for name, (argt, ret) in sig.items():
        fn = getattr(k, name)
        fn.argtypes = argt; fn.restype = ret


def _ensure_init():
    """엔진을 프로세스당 1회 로드/라이선스/초기화/언어설정. 성공 시 True."""
    global _K, _STATUS, _INITED
    if _STATUS == "ok":
        return True
    if _STATUS is not None:
        return False
    if not python_is_32bit():
        _STATUS = "64-bit Python (엔진 DLL이 32비트라 로드 불가; 32비트 파이썬 필요)"
        return False
    if not KERNEL_DLL.exists():
        _STATUS = f"KernelAPI.dll 없음: {KERNEL_DLL}"
        return False
    try:
        _setup_dirs()
        k = ctypes.WinDLL(str(KERNEL_DLL))
        _bind(k)
        rc = k.kRecSetLicenseW(LICENSE_FILE, LICENSE_CODE)
        if not _ok(rc):
            _STATUS = f"kRecSetLicenseW 실패 rc=0x{rc & 0xFFFFFFFF:X}"; return False
        rc = k.kRecInitW(INIT_USERKEY, INIT_COMPANY)
        if not _ok(rc):
            _STATUS = f"kRecInitW 실패 rc=0x{rc & 0xFFFFFFFF:X}"; return False
        try:
            k.kRecSetDefaults(0)
            k.kRecManageLanguages(0, 0, LANG_KRN)   # 활성언어 = 한국어만(리셋)
            # 주의: LANG_ENG=0 추가 시 '전체 언어'가 켜져 영문을 한글/한자로 오인식함 -> 한국어 단독 유지.
        except Exception:
            pass
        _K = k; _INITED = True; _STATUS = "ok"
        return True
    except Exception as e:
        _STATUS = f"로드/초기화 예외: {e}"
        return False


def available():
    """(사용가능?, 사유). 엔진을 초기화 시도하고 결과 반환."""
    okk = _ensure_init()
    return okk, (None if okk else _STATUS)


def set_languages(*langs):
    """활성 언어 재설정. 첫 언어로 리셋(op0) 후 나머지 추가(op1). 예: set_languages(0x7A, 0x01)"""
    if not _ensure_init():
        return False
    for i, lg in enumerate(langs):
        _K.kRecManageLanguages(0, 0 if i == 0 else 1, lg)
    return True


def _normalize(img_path, force_dpi=0):
    """엔진 로더 호환 + 정확도: grayscale + 올바른 DPI BMP. (이진화 금지: 엔진이 함)
    외부 자르기 툴이 DPI를 96으로 떨구면 인식 저하 -> 150 미만이면 force_dpi(기본 300)로 보정."""
    from PIL import Image
    im = Image.open(img_path)
    sd = im.info.get("dpi")
    dx = int(sd[0]) if (sd and sd[0]) else 0
    use = force_dpi or (dx if dx >= 150 else DEFAULT_DPI)
    if im.mode not in ("L", "RGB"):
        im = im.convert("L")
    tmp = str(Path(img_path).with_suffix("")) + "._engine.bmp"
    im.save(tmp, "BMP", dpi=(use, use))
    return tmp, use


def recognize(img_path, ii=-3):
    """이미지 -> [(code, left, top, w, h), ...]. 엔진 미사용시 EpsonOcrError."""
    if not _ensure_init():
        raise EpsonOcrError(_STATUS or "엔진 사용 불가")
    sid = 0
    norm, _dpi = _normalize(img_path)
    cands = [norm, str(img_path)]
    hPage = None
    for cand in cands:
        h = ctypes.c_void_p(0)
        rc = _K.kRecLoadImgFW(sid, cand, ctypes.byref(h), 0)
        if _ok(rc) and h.value:
            hPage = h; break
    if hPage is None:
        raise EpsonOcrError("이미지 로드 실패(kRecLoadImgFW)")
    try:
        _K.kRecPreprocessImg(sid, hPage)            # ScanSmart 동일: 엔진 전처리
        rc = _K.kRecRecognizeW(sid, hPage, None)
        if not _ok(rc):
            raise EpsonOcrError(f"kRecRecognizeW rc=0x{rc & 0xFFFFFFFF:X}")
        pLet = ctypes.c_void_p(0); n = ctypes.c_int(0)
        rc = _K.kRecGetLetters(hPage, ii, ctypes.byref(pLet), ctypes.byref(n))
        letters = []
        if _ok(rc) and n.value > 0 and pLet.value:
            raw = ctypes.string_at(pLet.value, n.value * LETTER_SIZE)
            for i in range(n.value):
                b = raw[i * LETTER_SIZE:(i + 1) * LETTER_SIZE]
                if len(b) < LETTER_SIZE:
                    break
                code = b[0x12] | (b[0x13] << 8)
                left = b[0] | (b[1] << 8); top = b[2] | (b[3] << 8)
                w = b[4] | (b[5] << 8); h = b[6] | (b[7] << 8)
                letters.append((code, left, top, w, h))
        return letters
    finally:
        try: _K.kRecFreeImg(hPage)
        except Exception: pass
        try:
            if os.path.exists(norm): os.remove(norm)
        except Exception: pass


def _is_cjk_junk(code):
    """도장/QR 오인식 글자 (라벨=한글+ASCII 전제). 괄호 등은 건드리지 않음."""
    return (0x3400 <= code <= 0x9FFF) or (0xFF00 <= code <= 0xFFEF)


def _thin(h, w, medh):
    """길쭉(바코드 막대 후보). 단독이면 괄호/I 일 수 있으니 밴드 밀집도로 최종판정."""
    return h > medh * 1.3 and h >= 2 * max(w, 1)


def _barcode_bands(letters, medh):
    """길쭉 글자가 같은 y밴드에 많이(>=8) 몰린 곳 = 바코드 밴드. (괄호 2~3개는 제외)"""
    cnt = {}
    for code, _l, t, w, h in letters:
        if code in (0x20, 0) or _is_cjk_junk(code):
            continue
        if _thin(h, w, medh):
            b = round(t / max(medh, 1))
            cnt[b] = cnt.get(b, 0) + 1
    return {b for b, n in cnt.items() if n >= 8}


def _structure_lines(letters):
    """엔진 구조: 0x20(W>0)=단어구분, 0x20(W==0)=줄바꿈, 그 외=글자. 잡음 제외.
    반환: (lines, medh). 각 line = [(text, x_left, x_right, top, bottom), ...] (단어 단위).
    단어를 한 덩어리로 찍어야 복사/검색 시 글자 사이 공백이 안 끼고 깨끗함."""
    texty = [h for c, _l, _t, _w, h in letters
             if h > 0 and ((0x30 <= c <= 0x39) or (0x41 <= c <= 0x5A)
                           or (0x61 <= c <= 0x7A) or (0xAC00 <= c <= 0xD7A3))]
    medh = sorted(texty)[len(texty) // 2] if texty else 20
    bc_bands = _barcode_bands(letters, medh)

    lines, line = [], []
    wc, wl, wr, wt, wb = [], None, None, None, None

    def flush_word():
        nonlocal wc, wl, wr, wt, wb
        if wc:
            line.append(("".join(wc), wl, wr, wt, wb))
        wc, wl, wr, wt, wb = [], None, None, None, None

    def flush_line():
        nonlocal line
        flush_word()
        if line:
            lines.append(line)
        line = []

    for code, l, t, w, h in letters:
        if code == 0x20:
            if w == 0:
                flush_line()                  # 줄 끝
            else:
                flush_word()                  # 단어 구분
            continue
        if not code or _is_cjk_junk(code):
            continue
        if _thin(h, w, medh) and round(t / max(medh, 1)) in bc_bands:
            continue                          # 바코드 막대만 제거
        wc.append(chr(code))
        if wl is None:
            wl, wt, wb = l, t, t + h
        wr = l + w; wt = min(wt, t); wb = max(wb, t + h)
    flush_line()

    # 엔진은 존 순서로 줄을 내보냄(오른쪽 컬럼 (IT)/2EA가 뒤로) -> 좌표로 재정렬:
    # 같은 행(높이 기준 묶음)끼리는 좌->우, 행은 위->아래.
    def _line_top(ws):
        ts = sorted(w[3] for w in ws); return ts[len(ts) // 2]
    rh = medh if medh > 0 else 30
    lines.sort(key=lambda ws: (round(_line_top(ws) / (rh * 1.2)), min(w[1] for w in ws)))
    return lines, medh


def letters_to_text(letters):
    """엔진 구조 그대로 줄/단어 재구성한 텍스트."""
    lines, _ = _structure_lines(letters)
    return "\n".join(" ".join(wd[0] for wd in ln) for ln in lines)


def _build_pdf_page(c, img_path, letters):
    from PIL import Image
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.cidfonts import UnicodeCIDFont
    font = "HYSMyeongJo-Medium"
    try:
        pdfmetrics.registerFont(UnicodeCIDFont(font))
    except Exception:
        font = "Helvetica"
    # 폰트 수직 메트릭(비율). baseline을 정확히 잡아 박스가 글자에 정렬되게 함.
    try:
        asc, dsc = pdfmetrics.getAscentDescent(font, 1000)
        asc_r, dsc_r = asc / 1000.0, -dsc / 1000.0
        if asc_r <= 0:
            raise ValueError
    except Exception:
        asc_r, dsc_r = 0.75, 0.25
    tot_r = asc_r + dsc_r                           # 글자높이 = (ascent+descent)*size

    im = Image.open(img_path); iw, ih = im.size
    c.setPageSize((iw, ih))
    c.drawImage(str(img_path), 0, 0, width=iw, height=ih)
    lines, _medh = _structure_lines(letters)
    for words in lines:
        if not words:
            continue
        # 단어별 폰트크기 + 타이포그래픽 baseline (글자 밑변 아니라 ascent 기준)
        sized, bls = [], []
        for text, wl, wr, wt, wb in words:
            wsize = max((wb - wt) / tot_r, 4.0)
            sized.append((text, wl, wr, wsize))
            bls.append(wt + asc_r * wsize)
        baseline = sorted(bls)[len(bls) // 2]      # 줄 공통 baseline -> 추출 시 한 줄
        try:
            to = c.beginText()
            to.setTextRenderMode(3)                # 투명(검색/선택만)
            for text, wl, wr, wsize in sized:      # 단어 단위(한 덩어리) -> 복사/검색 깨끗
                wpx = max(wr - wl, 1)
                rw = c.stringWidth(text, font, wsize)
                to.setFont(font, wsize)
                to.setHorizScale((100.0 * wpx / rw) if rw > 0 else 100.0)
                to.setTextOrigin(wl, ih - baseline)
                to.textOut(text)
            c.drawText(to)
        except Exception:
            pass
    c.showPage()


def _is_text_code(code):
    """실제 글자(숫자/영문/한글/CJK)인지 — 방향 점수용."""
    return ((0x30 <= code <= 0x39) or (0x41 <= code <= 0x5A) or (0x61 <= code <= 0x7A)
            or (0xAC00 <= code <= 0xD7A3) or (0x3000 <= code <= 0x9FFF))


# 품질용: 정상 글자 = 숫자/영문/한글 + 흔한 기호. (한자·전각 등은 비정상으로 봄)
_PUNCT_OK = set(ord(ch) for ch in "-/().,:%#*+_=' ")

def _is_plausible(code):
    return ((0x30 <= code <= 0x39) or (0x41 <= code <= 0x5A) or (0x61 <= code <= 0x7A)
            or (0xAC00 <= code <= 0xD7A3) or code in _PUNCT_OK)


# 라벨에 항상 들어가는 토큰(검증 앵커). 없으면 잘못된 스캔으로 간주. 필요시 추가.
REQUIRED_ANCHORS = ["EA"]

def assess_quality(letters, min_letters=40, min_ratio=0.80, anchors=None):
    """스캔 품질 점수. ok=False면 '재스캔 권장'(거꾸로/엉뚱/흐림 의심).
    판정: 글자수 + 정상글자비율 + 필수앵커(EA 등) 존재."""
    anchors = REQUIRED_ANCHORS if anchors is None else anchors
    real = [c for c, *_ in letters if c not in (0x20, 0, 0xFFFF)]
    n = len(real)
    if n == 0:
        return {"letters": 0, "valid_ratio": 0.0, "confidence": 0.0, "ok": False,
                "missing_anchors": list(anchors), "reason": "인식된 글자 없음"}
    valid = sum(1 for c in real if _is_plausible(c))
    ratio = valid / n
    text = "".join(chr(c) for c, *_ in letters if c not in (0, 0xFFFF)).upper().replace(" ", "")
    missing = [a for a in anchors if a.upper().replace(" ", "") not in text]

    ok = (n >= min_letters and ratio >= min_ratio and not missing)
    if ok:
        reason = ""
    elif missing:
        reason = "필수 항목 미검출(%s) — 라벨 방향/위치 확인" % ", ".join(missing)
    elif n < min_letters:
        reason = "글자 수 부족(%d)" % n
    else:
        reason = "정상글자 비율 낮음(%.0f%%) — 방향/위치/초점 의심" % (ratio * 100)
    return {"letters": n, "valid_ratio": round(ratio, 2), "confidence": round(ratio, 2),
            "missing_anchors": missing, "ok": ok, "reason": reason}


def recognize_oriented(img_path, detect_orientation=True):
    """0/90/180/270 중 '가장 잘 읽힌' 방향을 골라 (letters, 올바른방향 이미지경로) 반환.
    상하반전·90° 회전 자동 보정. detect_orientation=False면 회전 없이 1회만."""
    if not detect_orientation:
        return recognize(img_path), str(img_path)
    from PIL import Image
    base = Image.open(img_path)
    sd = base.info.get("dpi")
    dpi = int(sd[0]) if (sd and sd[0] and sd[0] >= 150) else 300
    stem = Path(img_path).with_suffix("")
    best = None  # (score, rot, letters, path)
    tmps = []
    for rot in (0, 90, 180, 270):
        im = base if rot == 0 else base.rotate(-rot, expand=True)  # 시계방향 rot
        if im.mode not in ("L", "RGB"):
            im = im.convert("RGB")
        tp = "%s._ori%d.png" % (stem, rot)
        im.save(tp, "PNG", dpi=(dpi, dpi)); tmps.append(tp)
        try:
            letters = recognize(tp)
        except Exception:
            letters = []
        score = sum(1 for c, *_ in letters if _is_text_code(c))
        if best is None or score > best[0]:
            best = (score, rot, letters, tp)
    # 선택 안 된 임시본 정리
    for tp in tmps:
        if tp != best[3]:
            try: os.remove(tp)
            except Exception: pass
    return best[2], best[3]


def _structure_to_fields(letters):
    """letters -> 줄/단어 파싱 결과.
    fields: {"line_1_1": "...", "line_1_2": "...", ...}  (줄R 단어C)
    lines : [{line, text, words:[{text, bbox:[x,y,w,h]}]}]"""
    lines, _ = _structure_lines(letters)
    fields, out_lines, full = {}, [], []
    for li, words in enumerate(lines, 1):
        line_text = " ".join(w[0] for w in words)
        full.append(line_text)
        out_lines.append({
            "line": li,
            "text": line_text,
            "words": [{"text": w[0], "bbox": [w[1], w[3], w[2] - w[1], w[4] - w[3]]}
                      for w in words],
        })
        for wi, w in enumerate(words, 1):
            fields["line_%d_%d" % (li, wi)] = w[0]

    # 부품번호: 첫 줄에서 '괄호류' 단어 앞까지를 공백 없이 결합.
    #   괄호 '(' 가 '<<'·'['·'≪' 등으로 오인식돼도 끊기게 괄호류 문자 전체로 판정.
    #   예: ["7338-", "1026546", "(181420252)"] 또는 [..., "<<181420252"] -> part_no="7338-1026546"
    _BRACKETS = set("()[]{}<>«»‹›＜＞｛｝〈〉《》")
    part_no, part_no_sub = "", ""
    if lines:
        pn = []
        for w in lines[0]:
            if any(ch in _BRACKETS for ch in w[0]):
                part_no_sub = w[0]
                break
            pn.append(w[0])
        part_no = "".join(pn)
    fields["part_no"] = part_no
    if part_no_sub:
        fields["part_no_sub"] = part_no_sub

    return {"text": "\n".join(full), "lines": out_lines, "fields": fields,
            "part_no": part_no, "part_no_sub": part_no_sub}


def image_to_searchable_pdf(images, out_pdf, lang="kor", detect_orientation=False):
    """images: 경로 또는 경로 리스트 -> 검색가능 PDF + 파싱필드 (ocr_pdf 호환 + 확장)."""
    if not _ensure_init():
        raise EpsonOcrError(_STATUS or "엔진 사용 불가")
    from reportlab.pdfgen import canvas
    if isinstance(images, (str, Path)):
        images = [images]
    c = canvas.Canvas(str(out_pdf))
    total = 0
    used = []
    struct = None
    quality = None
    for src in images:
        letters, oriented = recognize_oriented(src, detect_orientation)
        total += len(letters)
        _build_pdf_page(c, oriented, letters)
        used.append(oriented)
        if struct is None:                     # 첫(주) 페이지 파싱필드 + 품질
            struct = _structure_to_fields(letters)
            quality = assess_quality(letters)
    c.save()
    for p in used:                             # 방향보정 임시본 정리
        if str(p) not in [str(x) for x in images]:
            try: os.remove(p)
            except Exception: pass
    info = {"out_pdf": str(out_pdf), "engine": "Epson OmniPage",
            "pages": len(images), "letters": total}
    if struct:
        info.update(struct)                    # text, lines, fields
    if quality:
        info["quality"] = quality              # {letters, valid_ratio, confidence, ok, reason}
    return info


def recognize_structured(img_path):
    """OCR만 (PDF 없이) -> 파싱필드 dict."""
    return _structure_to_fields(recognize(img_path))


def image_to_text(image, lang="kor"):
    return letters_to_text(recognize(image))
