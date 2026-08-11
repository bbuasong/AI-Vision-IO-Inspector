"""
redact.py  -  이미지 특정 영역을 박스로 덮기(가림). OCR 품질 보존이 핵심.

OCR 영향 최소화 포인트:
  - 원본 해상도(픽셀) 유지 — 다운스케일 금지.
  - 원본 DPI 메타데이터 유지 — 없으면 기본 300 (엔진이 글자크기 추정에 DPI 사용).
  - 무손실 저장(PNG/BMP) — JPEG 재압축 잡티 금지.
  - 박스로 덮는 건 그 영역만 가릴 뿐 나머지 인식엔 영향 없음.
"""

from pathlib import Path
from PIL import Image, ImageDraw

DEFAULT_DPI = 300


def cover_regions(in_path, out_path, rects, fill="white"):
    """
    rects: [[x, y, w, h], ...] (픽셀 좌표). 각 영역을 fill 색 박스로 덮음.
    fill : "white" | "black" | (r,g,b)
    DPI/해상도 보존, 무손실 저장. 반환: out_path
    """
    im = Image.open(in_path)
    src_dpi = im.info.get("dpi")
    dx = int(src_dpi[0]) if (src_dpi and src_dpi[0]) else 0
    dpi = dx if dx >= 150 else DEFAULT_DPI

    if im.mode not in ("L", "RGB", "RGBA"):
        im = im.convert("RGB")
    color = {"white": (255, 255, 255), "black": (0, 0, 0)}.get(
        str(fill).lower(), fill if isinstance(fill, (tuple, list)) else (255, 255, 255))
    if im.mode == "L":
        color = 255 if color == (255, 255, 255) else (0 if color == (0, 0, 0) else int(sum(color) / 3))

    draw = ImageDraw.Draw(im)
    for r in (rects or []):
        x, y, w, h = [int(v) for v in r]
        draw.rectangle([x, y, x + w, y + h], fill=color)

    out_path = str(out_path)
    ext = Path(out_path).suffix.lower()
    if ext in (".jpg", ".jpeg"):     # 무손실 강제: 확장자만 jpg여도 png로 저장 권장
        out_path = str(Path(out_path).with_suffix(".png"))
    im.save(out_path, dpi=(dpi, dpi))
    return out_path
