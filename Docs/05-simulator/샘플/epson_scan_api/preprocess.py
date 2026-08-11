"""
preprocess.py  -  optional image preprocessing before OCR / PDF.

All steps are opt-in via an options dict (or PreprocessOptions). Returns a new
PIL image; never mutates the original file. Good defaults for scanned docs.

Options (all optional):
  grayscale   : bool  -> convert to gray
  autocontrast: bool  -> stretch contrast (PIL ImageOps.autocontrast)
  binarize    : "none"|"otsu"|"adaptive"  -> black/white text
  threshold   : int 0-255 (used when binarize == "fixed")
  deskew      : bool  -> auto straighten using text angle
  rotate      : int   -> rotate degrees (90/180/270 or any)
  resize_maxdim: int  -> longest side capped to this (keeps aspect)
  denoise     : bool  -> light median filter
  border_crop : int   -> crop N px border (removes scan edges)
"""

from PIL import Image, ImageOps, ImageFilter


def _otsu_threshold(gray):
    """Compute Otsu threshold from a PIL 'L' image histogram."""
    hist = gray.histogram()[:256]
    total = sum(hist)
    if total == 0:
        return 127
    sum_all = sum(i * hist[i] for i in range(256))
    sumB = 0.0
    wB = 0
    max_var = -1.0
    thr = 127
    for t in range(256):
        wB += hist[t]
        if wB == 0:
            continue
        wF = total - wB
        if wF == 0:
            break
        sumB += t * hist[t]
        mB = sumB / wB
        mF = (sum_all - sumB) / wF
        var = wB * wF * (mB - mF) ** 2
        if var > max_var:
            max_var = var
            thr = t
    return thr


def _estimate_skew(gray):
    """Estimate small skew angle (deg) by maximizing row-variance of ink over angles."""
    import math
    small = gray.copy()
    small.thumbnail((800, 800))
    best_a, best_score = 0.0, -1.0
    for a in [x * 0.5 for x in range(-8, 9)]:  # -4..+4 deg, 0.5 steps
        rot = small.rotate(a, resample=Image.BILINEAR, fillcolor=255, expand=False)
        px = rot.load()
        w, h = rot.size
        rows = []
        step = max(1, h // 200)
        for y in range(0, h, step):
            s = 0
            for x in range(0, w, max(1, w // 200)):
                s += 255 - px[x, y]
            rows.append(s)
        mean = sum(rows) / len(rows)
        score = sum((r - mean) ** 2 for r in rows)
        if score > best_score:
            best_score, best_a = score, a
    return best_a


def preprocess_image(img, opts):
    o = dict(opts or {})
    im = img

    if o.get("border_crop"):
        n = int(o["border_crop"])
        w, h = im.size
        if w > 2 * n and h > 2 * n:
            im = im.crop((n, n, w - n, h - n))

    if o.get("rotate"):
        im = im.rotate(-int(o["rotate"]), expand=True, fillcolor="white")

    if o.get("grayscale") or o.get("binarize", "none") not in ("none", None) or o.get("deskew"):
        im = im.convert("L")

    if o.get("denoise"):
        im = im.filter(ImageFilter.MedianFilter(size=3))

    if o.get("autocontrast"):
        im = ImageOps.autocontrast(im, cutoff=1)

    if o.get("deskew"):
        ang = _estimate_skew(im if im.mode == "L" else im.convert("L"))
        if abs(ang) >= 0.5:
            im = im.rotate(ang, resample=Image.BICUBIC, fillcolor=255, expand=True)

    mode = o.get("binarize", "none")
    if mode and mode != "none":
        g = im if im.mode == "L" else im.convert("L")
        if mode == "fixed":
            t = int(o.get("threshold", 160))
        else:  # otsu (default) / adaptive falls back to otsu here
            t = _otsu_threshold(g)
        im = g.point(lambda p, t=t: 255 if p > t else 0, mode="L").convert("1")

    if o.get("resize_maxdim"):
        m = int(o["resize_maxdim"])
        if max(im.size) > m:
            s = m / max(im.size)
            im = im.resize((max(1, int(im.size[0] * s)), max(1, int(im.size[1] * s))), Image.LANCZOS)

    return im


def preprocess_file(in_path, out_path, opts):
    im = Image.open(in_path)
    out = preprocess_image(im, opts)
    # 1-bit images save well as PNG/BMP; keep PNG for size
    if out.mode == "1":
        out.save(out_path)
    else:
        out.save(out_path)
    return out_path
