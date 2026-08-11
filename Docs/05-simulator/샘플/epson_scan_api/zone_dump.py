"""
zone_dump.py  -  엔진의 존(zone)/줄(line) 구조 덤프 (ScanSmart급 레이아웃용 진단).
==============================================================================
kRecGetOCRZoneInfo / kRecGetLineInfo 가 돌려주는 구조체를 해독하기 위해 실제 덤프.
이걸로 '바코드/QR 존 제외 + 줄별 정확한 박스'를 만들 수 있다.

사용 (32비트 파이썬, 크롭된 라벨 이미지로):
    python zone_dump.py "D:\\epson_scans\\<jobid>_card.png"

출력 전체를 복사해 전달:
  - ZONE COUNT / 각 ZONE 의 raw 바이트(타입·rect 디코드용) + 가능하면 텍스트
  - LINE COUNT / 각 LINE 의 raw 바이트(줄 rect 디코드용)
"""

import ctypes
import sys
from ctypes import wintypes as wt
from pathlib import Path

import ocr_epson as E


def _bindz(k):
    sig = {
        "kRecGetOCRZoneCount": ([ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)], ctypes.c_int),
        "kRecGetOCRZoneInfo":  ([ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_int], ctypes.c_int),
        "kRecGetLineCount":    ([ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)], ctypes.c_int),
        "kRecGetLineInfo":     ([ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_int], ctypes.c_int),
    }
    for n, (a, r) in sig.items():
        if hasattr(k, n):
            fn = getattr(k, n); fn.argtypes = a; fn.restype = r


def main():
    if len(sys.argv) < 2:
        print("사용: python zone_dump.py <크롭된라벨이미지>"); return 2
    img = sys.argv[1]
    if not Path(img).exists():
        print("이미지 없음:", img); return 2
    if not E._ensure_init():
        print("엔진 init 실패:", E._STATUS); return 1
    k = E._K; sid = 0
    _bindz(k)

    norm, _ = E._normalize(img)
    hPage = ctypes.c_void_p(0)
    rc = k.kRecLoadImgFW(sid, norm, ctypes.byref(hPage), 0)
    print("load rc=%d" % rc)
    k.kRecPreprocessImg(sid, hPage)
    rc = k.kRecRecognizeW(sid, hPage, None)
    print("recognize rc=%d" % rc)

    BUF = 1024

    import struct
    n = ctypes.c_int(0)
    rc = k.kRecGetOCRZoneCount(hPage, ctypes.byref(n))
    print("\n=== ZONE COUNT rc=%d n=%d  (variant B: idx@4) ===" % (rc, n.value))
    print(" idx  left  top right  bot  | d@0x10 d@14 d@18 d@1c d@20 d@24")
    for i in range(min(n.value, 30)):
        buf = (ctypes.c_ubyte * BUF)()
        try:
            rc = k.kRecGetOCRZoneInfo(hPage, -3, ctypes.byref(buf), i)
        except Exception as e:
            print("  zone %d 예외 %r" % (i, e)); break
        d = struct.unpack_from("<16I", bytes(buf[:64]))
        print("  %2d  %5d %4d %5d %4d | %5d %4d %4d %4d %4d %4d"
              % (i, d[0], d[1], d[2], d[3], d[4], d[5], d[6], d[7], d[8], d[9]))

    try:
        k.kRecFreeImg(hPage)
        import os
        if os.path.exists(norm): os.remove(norm)
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
