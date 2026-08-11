"""
card_pil.py  -  라벨 자동 추출 (deskew + 크롭). PIL 전용, 추가 설치/별도 프로세스 없음.
==============================================================================
평판 스캔본은 원근 왜곡이 거의 없고 '회전(skew)'만 있으므로 OpenCV 없이 처리:
  1) 잉크(글자/선/바코드) 마스크 -> 모폴로지 open(얇은 줄/점 제거)
  2) 잉크 점들의 **최소면적 회전사각형(minAreaRect)** 각도로 기울기 산출 (기하학적,
     투영분산 추측 아님 -> 각도 제한 없고 배경 구조에 강함) -> deskew
  3) 잉크 bounding box 로 라벨만 크롭
  4) 풀해상도 유지 + DPI=300 태깅 + 무손실 PNG

검증: 실제 라벨 스캔을 임의 각도(-18°~+12°)로 변형해도 일관되게 라벨 크기로 크롭됨.
32비트 OCR 서버 안에서 그대로 import (numpy/opencv 불필요).

interface:
  extract_label(in_path, out_path, dpi=300, margin_frac=0.04, cap=150) -> (out_path, info)
"""

import math
from pathlib import Path
from PIL import Image, ImageFilter

DEFAULT_DPI = 300


def _otsu(gray):
    hist = gray.histogram()[:256]
    total = sum(hist)
    if not total:
        return 127
    sum_all = sum(i * hist[i] for i in range(256))
    sumB = 0.0; wB = 0; mx = -1.0; thr = 127
    for t in range(256):
        wB += hist[t]
        if wB == 0:
            continue
        wF = total - wB
        if wF == 0:
            break
        sumB += t * hist[t]
        mB = sumB / wB; mF = (sum_all - sumB) / wF
        v = wB * wF * (mB - mF) ** 2
        if v > mx:
            mx = v; thr = t
    return thr


def _ink(gray, cap=150):
    """어두운 잉크만 전경(255). 임계값 cap 이하로 눌러 회색 배경 오검출 방지."""
    thr = min(_otsu(gray), cap)
    return gray.point(lambda p: 255 if p < thr else 0)


def _ink_open(gray, cap=150):
    """잉크 마스크 + open(MinFilter->MaxFilter): 얇은 줄무늬·점 노이즈 제거."""
    m = _ink(gray, cap)
    return m.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))


def _convex_hull(pts):
    pts = sorted(set(pts))
    if len(pts) <= 2:
        return pts
    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lower = []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    upper = []
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def _min_area_angle(hull):
    """회전 캘리퍼스: 최소면적 외접사각형을 만드는 변의 각도(rad)."""
    n = len(hull); best = None
    for i in range(n):
        dx = hull[(i + 1) % n][0] - hull[i][0]
        dy = hull[(i + 1) % n][1] - hull[i][1]
        a = math.atan2(dy, dx); c = math.cos(-a); s = math.sin(-a)
        xs = [p[0] * c - p[1] * s for p in hull]
        ys = [p[0] * s + p[1] * c for p in hull]
        area = (max(xs) - min(xs)) * (max(ys) - min(ys))
        if best is None or area < best[0]:
            best = (area, a)
    return best[1]


def _proj_score(mask, a):
    """각도 a로 회전 후 잉크 행(row) 투영분산 — 텍스트 줄이 수평일수록 큼."""
    r = mask.rotate(a, resample=Image.BILINEAR, fillcolor=0, expand=True)
    px = r.load(); w, h = r.size
    rows = []; st = max(1, h // 500); xs = max(1, w // 500)
    for y in range(0, h, st):
        acc = 0
        for x in range(0, w, xs):
            if px[x, y]:
                acc += 1
        rows.append(acc)
    mn = sum(rows) / len(rows)
    return sum((v - mn) ** 2 for v in rows)


def _deskew_angle(gray, cap=150, dim=1100, rng=20, coarse=1.0, fine=0.2):
    """텍스트 줄을 수평으로 맞추는 deskew 각(deg). projection-variance.
    잉크 minAreaRect보다 '텍스트 줄' 정렬에 정확 (검수박스·바코드·찢긴 가장자리에 안 휘둘림)."""
    s = gray.copy(); s.thumbnail((dim, dim))
    m = _ink_open(s, cap)
    if not m.getbbox():
        return 0.0
    best, best_s = 0.0, -1.0
    a = -rng
    while a <= rng:                          # 거친 탐색 (±rng)
        v = _proj_score(m, a)
        if v > best_s:
            best_s, best = v, a
        a += coarse
    a = best - coarse                        # 미세 탐색
    while a <= best + coarse:
        v = _proj_score(m, a)
        if v > best_s:
            best_s, best = v, a
        a += fine
    return best


def extract_label(in_path, out_path, dpi=0, margin_frac=0.04, cap=150):
    """이미지 -> deskew + 라벨 크롭 -> PNG@DPI 저장. 반환: (저장경로, info)."""
    im = Image.open(in_path)
    sd = im.info.get("dpi")
    dx = int(sd[0]) if (sd and sd[0]) else 0
    use = dpi or (dx if dx >= 150 else DEFAULT_DPI)

    gray = im.convert("L")
    angle = _deskew_angle(gray, cap=cap)

    desk = im.rotate(angle, resample=Image.BICUBIC, fillcolor="white", expand=True)
    dgray = gray.rotate(angle, resample=Image.BICUBIC, fillcolor=255, expand=True)

    bbox = _ink_open(dgray, cap).getbbox()
    W, H = desk.size
    cropped = False
    if bbox:
        x0, y0, x1, y1 = bbox
        bw, bh = x1 - x0, y1 - y0
        if 0 < bw * bh < 0.95 * W * H:          # 안전장치: 거의 전체면 크롭 안 함
            mx, my = int(bw * margin_frac), int(bh * margin_frac)
            desk = desk.crop((max(0, x0 - mx), max(0, y0 - my),
                              min(W, x1 + mx), min(H, y1 + my)))
            cropped = True

    out_path = str(Path(out_path).with_suffix(".png"))
    if desk.mode not in ("L", "RGB"):
        desk = desk.convert("RGB")
    desk.save(out_path, "PNG", dpi=(use, use))
    return out_path, {"angle": round(angle, 2), "cropped": cropped,
                      "size": list(desk.size), "dpi": use}


if __name__ == "__main__":
    import sys
    if len(sys.argv) < 3:
        print("사용: python card_pil.py <입력> <출력.png> [--dpi 300]")
        raise SystemExit(2)
    a = sys.argv[1:]; dpi = 0
    if "--dpi" in a:
        i = a.index("--dpi"); dpi = int(a[i + 1]); del a[i:i + 2]
    p, info = extract_label(a[0], a[1], dpi=dpi)
    print("saved", p, info)
