"""
ocr_pdf.py  -  image(s) -> searchable PDF via Tesseract (importable).

Same engine validated in make_searchable_pdf.py. Auto-finds the tessdata
folder that has the requested languages and sets TESSDATA_PREFIX (robust
with spaces in the path).
"""

import io
import os
import subprocess
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


class OcrError(Exception):
    pass


def find_tesseract():
    for p in TESS_EXE_CANDIDATES:
        if Path(p).exists():
            pytesseract.pytesseract.tesseract_cmd = p
            return p
    return None  # rely on PATH


def find_tessdata(want_langs):
    cands = []
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
            return d, langs
    return best if best else (None, set())


def available_languages():
    find_tesseract()
    tdir, langs = find_tessdata(["eng"])
    return sorted(langs)


def resolve_lang(requested):
    want = requested.split("+")
    tdir, have = find_tessdata(want)
    use = [l for l in want if l in have] or [l for l in ("eng",) if l in have]
    return ("+".join(use) if use else ""), tdir, have


def image_to_searchable_pdf(images, out_pdf, lang="kor+eng"):
    """images: path or list of paths. Writes a searchable PDF to out_pdf."""
    find_tesseract()
    if isinstance(images, (str, Path)):
        images = [images]
    use_lang, tdir, have = resolve_lang(lang)
    if not use_lang:
        raise OcrError("사용 가능한 언어 데이터가 없습니다. tessdata에 eng/kor.traineddata를 넣으세요. (발견: %s)"
                       % (", ".join(sorted(have)) or "없음"))
    if tdir:
        os.environ["TESSDATA_PREFIX"] = str(tdir)

    pages = []
    for src in images:
        im = Image.open(src)
        if im.mode not in ("L", "1", "RGB"):
            im = im.convert("L")
        pages.append(pytesseract.image_to_pdf_or_hocr(im, lang=use_lang, extension="pdf"))

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
    return {"out_pdf": str(out_pdf), "lang": use_lang, "pages": len(pages)}


def image_to_text(image, lang="kor+eng"):
    find_tesseract()
    use_lang, tdir, have = resolve_lang(lang)
    if not use_lang:
        raise OcrError("사용 가능한 언어 데이터 없음.")
    if tdir:
        os.environ["TESSDATA_PREFIX"] = str(tdir)
    im = Image.open(image)
    return pytesseract.image_to_string(im, lang=use_lang)
