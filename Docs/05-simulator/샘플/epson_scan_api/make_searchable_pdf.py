"""
make_searchable_pdf.py  -  image(s) -> searchable PDF (Tesseract 4/5)

Shows the scanned image with an invisible, selectable OCR text layer
(Ctrl-F / text selection works).

Setup:
  pip install pytesseract pillow pypdf
  Install Tesseract 5.x (UB Mannheim) and put kor.traineddata / eng.traineddata
  in its tessdata folder. This script auto-finds the tessdata folder that
  actually contains your language files (and you can force it with --tessdata).

Usage:
  python make_searchable_pdf.py "<image>" "<out.pdf>" [--lang kor+eng] [--tessdata "<dir>"]
  python make_searchable_pdf.py "<img1>" "<img2>" ... "<out.pdf>"   # multi-page
"""

import io
import os
import subprocess
import sys
from pathlib import Path

import pytesseract
from PIL import Image

TESS_EXE_CANDIDATES = [
    r"C:\Program Files\Tesseract-OCR\tesseract.exe",
    r"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe",
    str(Path.home() / "AppData/Local/Programs/Tesseract-OCR/tesseract.exe"),
]

TESSDATA_CANDIDATES = [
    r"C:\Program Files\Tesseract-OCR\tessdata",
    r"C:\Program Files (x86)\Tesseract-OCR\tessdata",
    str(Path.home() / "AppData/Local/Programs/Tesseract-OCR/tessdata"),
]


def find_tesseract():
    for p in TESS_EXE_CANDIDATES:
        if Path(p).exists():
            pytesseract.pytesseract.tesseract_cmd = p
            return p
    return None


def find_tessdata(explicit, want_langs):
    """Pick a tessdata dir that actually has the wanted .traineddata files."""
    cands = []
    if explicit:
        cands.append(explicit)
    if os.environ.get("TESSDATA_PREFIX"):
        cands.append(os.environ["TESSDATA_PREFIX"])
    cands += TESSDATA_CANDIDATES
    best = None
    for d in cands:
        dp = Path(d)
        if not dp.is_dir():
            continue
        langs = {f.stem for f in dp.glob("*.traineddata")}
        if not langs:
            continue
        if best is None:
            best = (d, langs)
        if any(l in langs for l in want_langs):
            return d, langs  # has what we want
    return best if best else (None, set())


def tess_version_major():
    exe = pytesseract.pytesseract.tesseract_cmd or "tesseract"
    try:
        out = subprocess.run([exe, "--version"], capture_output=True, text=True)
        line = (out.stdout or out.stderr).splitlines()[0]
        print("version :", line.strip())
        digits = "".join(ch for ch in line.split()[-1] if ch.isdigit() or ch == ".")
        return int(digits.split(".")[0]) if digits else 0
    except Exception as e:
        print("version : (could not run tesseract: %r)" % (e,))
        return 0


def make_pdf(images, out_pdf, lang, tessdata_dir):
    # Use TESSDATA_PREFIX env (robust with spaces in the path); avoids the
    # quoting issues of passing --tessdata-dir through pytesseract's config.
    if tessdata_dir:
        os.environ["TESSDATA_PREFIX"] = str(tessdata_dir)
    pages = []
    for src in images:
        im = Image.open(src).convert("L")
        pages.append(pytesseract.image_to_pdf_or_hocr(im, lang=lang, extension="pdf"))
    if len(pages) == 1:
        Path(out_pdf).write_bytes(pages[0])
    else:
        try:
            from pypdf import PdfWriter, PdfReader
        except ImportError:
            from PyPDF2 import PdfWriter, PdfReader
        w = PdfWriter()
        for b in pages:
            for pg in PdfReader(io.BytesIO(b)).pages:
                w.add_page(pg)
        with open(out_pdf, "wb") as f:
            w.write(f)


def main():
    args = list(sys.argv[1:])
    lang = "kor+eng"
    tessdata = None
    if "--lang" in args:
        i = args.index("--lang"); lang = args[i + 1]; del args[i:i + 2]
    if "--tessdata" in args:
        i = args.index("--tessdata"); tessdata = args[i + 1]; del args[i:i + 2]
    if len(args) < 2:
        print('USAGE: python make_searchable_pdf.py <image>... <out.pdf> [--lang kor+eng] [--tessdata "<dir>"]')
        return

    exe = find_tesseract()
    print("tesseract:", exe or "(from PATH)")
    major = tess_version_major()
    if major and major < 4:
        print("  [STOP] Tesseract too old for PDF output. Install 5.x (UB Mannheim).")
        return

    want = lang.split("+")
    tdir, have = find_tessdata(tessdata, want)
    print("tessdata :", tdir or "(default)", "| found langs:", ", ".join(sorted(have)) or "(none)")
    if not have:
        print("  [STOP] No .traineddata found. Put eng/kor.traineddata in the tessdata folder,")
        print("         or pass --tessdata \"C:\\path\\to\\tessdata\".")
        return

    use = [l for l in want if l in have] or [l for l in ("eng",) if l in have]
    if not use:
        print("  [STOP] none of requested langs (%s) present; available: %s" % (lang, ", ".join(sorted(have))))
        return
    lang = "+".join(use)
    if set(want) - have:
        print("  [warn] missing %s -> using %s" % (",".join(set(want) - have), lang))
    print("using language:", lang)

    *images, out_pdf = args
    try:
        make_pdf(images, out_pdf, lang, tdir)
    except Exception as e:
        print("Failed: %r" % (e,))
        return
    p = Path(out_pdf)
    if p.exists() and p.stat().st_size > 0:
        print("OK -> %s  (%d bytes). Try Ctrl-F / select text to confirm the OCR layer." % (out_pdf, p.stat().st_size))
    else:
        print("No output produced.")


if __name__ == "__main__":
    main()
