"""
epson_ocr_probe.py  v8  -  image-variant sweep (rc=7 == image error)

Finding: public rc=7 maps to internal image-status errors 0x6f-0x74
(statuses 104-201 in the image mapper). The engine works; it just rejects
the input image. The test input is a 4624x3468 color JPG (a photo, not a
scan). OCR engines expect scan-like images (moderate DPI, gray/1-bit).

This makes several preprocessed variants and tries each through the
recovered EsOCR pipeline, reporting rc + whether a PDF appeared.

Run (32-bit Python, needs Pillow):
  python epson_ocr_probe.py "<input_image>" "<out_dir>"
Paste probe_log.txt back; check <out_dir> for result_*.pdf
"""

import ctypes
import ctypes.wintypes as wt
import os
import struct
import sys
from datetime import datetime
from pathlib import Path

ESCNOCR = Path(r"C:\Program Files (x86)\epson\ESCNOCR")
ESOCR = ESCNOCR / "EsOCR.dll"
OCRLIB = ESCNOCR / "ocrlib"
LOG = Path(__file__).resolve().with_name("probe_log.txt")
_logf = open(LOG, "w", encoding="utf-8")


def log(m=""):
    line = "%s  %s" % (datetime.now().strftime("%H:%M:%S"), m)
    print(line); _logf.write(line + "\n"); _logf.flush()


def ansi(s):
    try:
        return s.encode("mbcs")
    except Exception:
        return s.encode("utf-8", "replace")


def make_variants(in_path, out_dir):
    """Return list of (label, path). Needs Pillow."""
    from PIL import Image
    variants = []
    im0 = Image.open(in_path)
    w, h = im0.size

    def scaled(im, maxdim):
        s = min(1.0, maxdim / max(im.size))
        if s < 1.0:
            return im.resize((max(1, int(im.size[0] * s)), max(1, int(im.size[1] * s))), Image.LANCZOS)
        return im.copy()

    # 1. grayscale BMP, downscaled to ~2480 (≈A4@300)
    p = out_dir / "v_gray2480.bmp"
    scaled(im0.convert("L"), 2480).save(p); variants.append(("gray BMP 2480", p))
    # 2. 1-bit BMP, downscaled
    p = out_dir / "v_bw2480.bmp"
    scaled(im0.convert("L"), 2480).convert("1").save(p); variants.append(("1-bit BMP 2480", p))
    # 3. grayscale BMP full size
    p = out_dir / "v_grayfull.bmp"
    im0.convert("L").save(p); variants.append(("gray BMP full", p))
    # 4. grayscale JPG downscaled
    p = out_dir / "v_gray2480.jpg"
    scaled(im0.convert("L"), 2480).save(p, quality=90); variants.append(("gray JPG 2480", p))
    # 5. 24-bit BMP downscaled (color)
    p = out_dir / "v_rgb2480.bmp"
    scaled(im0.convert("RGB"), 2480).save(p); variants.append(("rgb BMP 2480", p))
    # 6. original
    variants.append(("ORIGINAL", Path(in_path)))
    return variants


def main():
    log("=== Epson EsOCR image-variant sweep v8 ===")
    log("python %s / %d-bit" % (sys.version.split()[0], struct.calcsize("P") * 8))
    if struct.calcsize("P") != 4:
        log("ABORT: need 32-bit Python."); return
    if len(sys.argv) < 3:
        log("USAGE: python epson_ocr_probe.py <input_image> <out_dir>"); return
    in_path = str(Path(sys.argv[1]).resolve())
    out_dir = Path(sys.argv[2]).resolve(); out_dir.mkdir(parents=True, exist_ok=True)
    log("input : " + in_path)
    log("outdir: " + str(out_dir))

    try:
        variants = make_variants(in_path, out_dir)
    except Exception as e:
        log("CANNOT preprocess (need Pillow: pip install pillow): %r" % (e,)); return

    if hasattr(os, "add_dll_directory"):
        os.add_dll_directory(str(ESCNOCR))
        if OCRLIB.exists():
            os.add_dll_directory(str(OCRLIB))
    os.environ["PATH"] = str(ESCNOCR) + ";" + str(OCRLIB) + ";" + os.environ.get("PATH", "")
    os.chdir(str(ESCNOCR))

    es = ctypes.WinDLL(str(ESOCR))
    es.Esocr_Init.argtypes = [ctypes.c_void_p]; es.Esocr_Init.restype = ctypes.c_int
    es.Esocr_End.argtypes = [ctypes.c_void_p]; es.Esocr_End.restype = ctypes.c_int
    es.Esocr_SetRecogFile.argtypes = [wt.LPCWSTR, ctypes.c_ushort, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p]
    es.Esocr_SetRecogFile.restype = ctypes.c_int
    es.Esocr_SetPDFInfo.argtypes = [ctypes.c_char_p, ctypes.c_void_p, ctypes.c_ushort, ctypes.c_ushort,
                                    ctypes.c_ushort, ctypes.c_ushort, ctypes.c_ushort, ctypes.c_char_p, ctypes.c_char_p]
    es.Esocr_SetPDFInfo.restype = ctypes.c_int

    log("Esocr_Init rc=%s" % es.Esocr_Init(0))

    for i, (label, src) in enumerate(variants):
        out_pdf = str(out_dir / ("result_%d.pdf" % i))
        if Path(out_pdf).exists():
            Path(out_pdf).unlink()
        xy = (ctypes.c_ushort * 4)(300, 0, 300, 0)
        mode = (ctypes.c_ushort * 8)()
        try:
            rp = es.Esocr_SetPDFInfo(ansi(out_pdf), ctypes.byref(xy), 0, 0, 80, 0, 0, None, None)
            rr = es.Esocr_SetRecogFile(str(Path(src).resolve()), 0, ctypes.byref(mode), None, None)
            ex = Path(out_pdf).exists(); sz = Path(out_pdf).stat().st_size if ex else 0
            log("[%s] SetPDFInfo=%s SetRecogFile=%s  PDF=%s size=%d  (%s)"
                % (label, rp, rr, ex, sz, src))
        except Exception as e:
            log("[%s] EXCEPTION %r" % (label, e))

    log("Esocr_End rc=%s" % es.Esocr_End(0))
    log("DONE. Paste probe_log.txt back. Check %s" % out_dir)
    _logf.close()


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        log("TOP-LEVEL EXCEPTION: %r" % (e,)); _logf.close()
