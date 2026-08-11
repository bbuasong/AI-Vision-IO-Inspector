"""
card_extract.py  -  스캔본에서 명함/문서 영역 4꼭짓점 검출 + perspective warp.
==============================================================================
OCR 서버(32비트, Epson 엔진)와 분리된 **별도 단계**. OpenCV가 깔린 파이썬
(64비트 권장)에서 실행해 PNG@300dpi 를 만들고, 그 파일을 OCR 단계가 소비한다.

설치(이 도구 전용, OCR venv32 와 별개 환경 권장):
    pip install opencv-python numpy pillow

사용:
    python card_extract.py <입력이미지> <출력.png> [--dpi 300] [--debug]
      --debug : 검출된 사각형을 그린 <출력>_debug.png 도 저장

OCR 품질 보존 규칙 (코드에 반영됨):
  1) 검출은 축소본에서(속도), 와프는 **원본 풀해상도**에 적용 (좌표만 배율복원).
  2) 출력 크기 = 검출된 카드의 **실제 픽셀 치수** (다운스케일 금지).
  3) 보간 = INTER_CUBIC (텍스트용; NEAREST 금지).
  4) 저장 시 **DPI=300 태깅** (warp하면 DPI가 날아가 96이 됨 → 인식 저하 방지).
  5) **이진화/threshold 안 함** — 흑백화는 OCR 엔진(kRecPreprocessImg)이 함.
  6) **무손실 PNG** 저장 (JPEG 금지).
  7) 카드 못 찾으면 원본을 (DPI 보정해) 그대로 저장 → 파이프라인 안 끊김.
"""

import sys
from pathlib import Path

import numpy as np
import cv2
from PIL import Image

DETECT_LONG = 1600     # 검출용 축소 기준(긴 변)
MIN_AREA_FRAC = 0.02   # 라벨 최소 면적(이미지 대비; 라벨이 작아도 잡게 낮춤)
MARGIN_FRAC = 0.02     # 검출 사각형 둘레 여유(가장자리 글자 안 잘리게)


def _imread_unicode(path):
    """유니코드 경로 안전 읽기."""
    data = np.fromfile(str(path), dtype=np.uint8)
    return cv2.imdecode(data, cv2.IMREAD_COLOR)


def _src_dpi(path, fallback=300):
    try:
        d = Image.open(path).info.get("dpi")
        if d and d[0] and int(d[0]) >= 150:
            return int(d[0])
    except Exception:
        pass
    return fallback


def order_points(pts):
    """4점 -> TL,TR,BR,BL."""
    rect = np.zeros((4, 2), dtype="float32")
    s = pts.sum(axis=1)
    rect[0] = pts[np.argmin(s)]      # TL (x+y 최소)
    rect[2] = pts[np.argmax(s)]      # BR (x+y 최대)
    d = np.diff(pts, axis=1)
    rect[1] = pts[np.argmin(d)]      # TR (x-y 최소... y-x 최대)
    rect[3] = pts[np.argmax(d)]      # BL
    return rect


def _expand_rect(box, frac):
    """minAreaRect 박스(4점)를 중심 기준 frac 만큼 확대(여백)."""
    c = box.mean(axis=0)
    return (c + (box - c) * (1.0 + frac)).astype("float32")


def find_label_quad_mask(small_gray, scale, margin=MARGIN_FRAC):
    """[기본] 잉크(글자/선/바코드) 덩어리 기준. 평판 스캔 라벨/스티커에 robust.
    흰 스티커 경계·찢긴 가장자리 무시하고 '내용물'을 잡는다."""
    # 어두운 잉크만 전경으로. Otsu가 회색배경을 전경으로 오분류하는 걸 막기 위해
    # 임계값을 어두운 쪽으로 캡(<=otsu, 최대 150). (검은 글자/선/바코드만 잡힘)
    otsu, _ = cv2.threshold(small_gray, 0, 255, cv2.THRESH_BINARY | cv2.THRESH_OTSU)
    thr = min(otsu, 150)
    _, th = cv2.threshold(small_gray, thr, 255, cv2.THRESH_BINARY_INV)
    # 라벨 내용(글자+선+바코드)을 '한 덩어리'로 강하게 묶음
    k = cv2.getStructuringElement(cv2.MORPH_RECT, (45, 35))
    closed = cv2.morphologyEx(th, cv2.MORPH_CLOSE, k, iterations=3)
    cnts, _ = cv2.findContours(closed, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not cnts:
        return None
    H, W = small_gray.shape[:2]
    c = max(cnts, key=cv2.contourArea)               # 최대 단일 덩어리 = 라벨 내용
    area = cv2.contourArea(c)
    if area < MIN_AREA_FRAC * H * W:
        return None
    (cx, cy), (rw, rh), ang = cv2.minAreaRect(c)
    # 안전장치: 검출 박스가 이미지 거의 전체면 배경을 잡은 것 → 실패 처리(원본 유지).
    if (rw * rh) > 0.9 * H * W:
        return None
    box = cv2.boxPoints(((cx, cy), (rw, rh), ang)).astype("float32")
    box = _expand_rect(box, margin)
    return box / scale


def find_card_quad_edge(small_gray, scale):
    """[옵션] 에지-사각형. 대비 뚜렷한 배경 위 문서/카드에 적합."""
    blur = cv2.GaussianBlur(small_gray, (5, 5), 0)
    edges = cv2.dilate(cv2.Canny(blur, 50, 150), np.ones((3, 3), np.uint8), 1)
    cnts, _ = cv2.findContours(edges, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    cnts = sorted(cnts, key=cv2.contourArea, reverse=True)[:10]
    H, W = small_gray.shape[:2]; img_area = float(H * W)
    for c in cnts:
        if cv2.contourArea(c) < 0.15 * img_area:
            continue
        approx = cv2.approxPolyDP(c, 0.02 * cv2.arcLength(c, True), True)
        if len(approx) == 4 and cv2.isContourConvex(approx):
            return approx.reshape(4, 2).astype("float32") / scale
    if cnts and cv2.contourArea(cnts[0]) >= 0.15 * img_area:
        return cv2.boxPoints(cv2.minAreaRect(cnts[0])).astype("float32") / scale
    return None


def find_card_quad(small_gray, scale, mode="mask"):
    if mode == "edge":
        return find_card_quad_edge(small_gray, scale)
    return find_label_quad_mask(small_gray, scale)


def warp_card(orig, quad):
    """원본 풀해상도에 perspective warp. 출력크기 = 카드 실제 픽셀치수."""
    rect = order_points(quad)
    (tl, tr, br, bl) = rect
    wA = np.linalg.norm(br - bl); wB = np.linalg.norm(tr - tl)
    hA = np.linalg.norm(tr - br); hB = np.linalg.norm(tl - bl)
    W = int(round(max(wA, wB))); H = int(round(max(hA, hB)))
    if W < 10 or H < 10:
        return None
    dst = np.array([[0, 0], [W - 1, 0], [W - 1, H - 1], [0, H - 1]], dtype="float32")
    M = cv2.getPerspectiveTransform(rect, dst)
    return cv2.warpPerspective(orig, M, (W, H), flags=cv2.INTER_CUBIC,
                               borderMode=cv2.BORDER_REPLICATE)


def extract_card(in_path, out_path, dpi=0, debug=False, mode="mask"):
    orig = _imread_unicode(in_path)
    if orig is None:
        raise SystemExit("이미지를 읽을 수 없습니다: %s" % in_path)
    dpi = dpi or _src_dpi(in_path, 300)

    h, w = orig.shape[:2]
    long = max(h, w)
    scale = (DETECT_LONG / long) if long > DETECT_LONG else 1.0
    small = (cv2.resize(orig, (int(w * scale), int(h * scale)), interpolation=cv2.INTER_AREA)
             if scale < 1.0 else orig)
    gray = cv2.cvtColor(small, cv2.COLOR_BGR2GRAY)

    quad = find_card_quad(gray, scale, mode=mode)
    found = quad is not None
    warped = warp_card(orig, quad) if found else None
    if warped is None:
        warped = orig                              # 못 찾으면 원본 유지

    # 무손실 PNG + DPI 태깅 (PIL 경유: 유니코드/ DPI 안전)
    rgb = cv2.cvtColor(warped, cv2.COLOR_BGR2RGB)
    out_path = str(Path(out_path).with_suffix(".png"))
    Image.fromarray(rgb).save(out_path, "PNG", dpi=(dpi, dpi))

    if debug and found:
        dbg = orig.copy()
        cv2.polylines(dbg, [order_points(quad).astype(int)], True, (0, 0, 255), 6)
        dp = str(Path(out_path).with_suffix("")) + "_debug.png"
        cv2.imwrite(dp, dbg)
    print("card=%s  out=%s  size=%dx%d  dpi=%d" %
          (found, out_path, warped.shape[1], warped.shape[0], dpi))
    return out_path, found


def main():
    a = sys.argv[1:]
    dpi = 0; debug = False; mode = "mask"
    if "--debug" in a:
        debug = True; a.remove("--debug")
    if "--dpi" in a:
        i = a.index("--dpi"); dpi = int(a[i + 1]); del a[i:i + 2]
    if "--mode" in a:
        i = a.index("--mode"); mode = a[i + 1]; del a[i:i + 2]   # mask|edge
    if len(a) < 2:
        print("사용: python card_extract.py <입력이미지> <출력.png> [--dpi 300] [--mode mask|edge] [--debug]")
        return 2
    extract_card(a[0], a[1], dpi=dpi, debug=debug, mode=mode)
    return 0


if __name__ == "__main__":
    sys.exit(main())
