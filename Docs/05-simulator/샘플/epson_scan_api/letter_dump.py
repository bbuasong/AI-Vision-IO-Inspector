"""
letter_dump.py  -  엔진이 주는 letter 구조 덤프 (Level-1 레이아웃 업그레이드용 진단).
==============================================================================
ScanSmart급 텍스트레이어를 만들려면 엔진이 letter 스트림에 담아주는
'공백/줄끝/단어' 구조를 써야 한다. 그 구조를 해독하기 위해 실제 데이터를 덤프.

사용 (32비트 파이썬, 크롭된 라벨 이미지로):
    python letter_dump.py "D:\\epson_scans\\<jobid>_card.png"

출력 전체를 복사해 전달해줘:
  - ALL LETTERS: 공백(0x20)·줄바꿈(0x0A/0x0D) 코드가 끼어있는지, 순서가 어떤지
  - RAW structs: 56바이트 안의 미해독 필드(줄끝/단어끝 플래그·신뢰도 등) 확인용
"""

import ctypes
import sys
from pathlib import Path

import ocr_epson as E


def main():
    if len(sys.argv) < 2:
        print("사용: python letter_dump.py <크롭된라벨이미지>")
        return 2
    img = sys.argv[1]
    if not Path(img).exists():
        print("이미지 없음:", img); return 2
    if not E._ensure_init():
        print("엔진 init 실패:", E._STATUS); return 1
    k = E._K; sid = 0

    norm, _dpi = E._normalize(img)
    hPage = ctypes.c_void_p(0)
    rc = k.kRecLoadImgFW(sid, norm, ctypes.byref(hPage), 0)
    print("kRecLoadImgFW rc=%d" % rc)
    if not E._ok(rc) or not hPage.value:
        return 1
    k.kRecPreprocessImg(sid, hPage)
    rc = k.kRecRecognizeW(sid, hPage, None)
    print("kRecRecognizeW rc=%d" % rc)

    pLet = ctypes.c_void_p(0); n = ctypes.c_int(0)
    rc = k.kRecGetLetters(hPage, -3, ctypes.byref(pLet), ctypes.byref(n))
    print("kRecGetLetters rc=%d n=%d" % (rc, n.value))
    if not E._ok(rc) or n.value <= 0 or not pLet.value:
        k.kRecFreeImg(hPage); return 1

    raw = ctypes.string_at(pLet.value, n.value * E.LETTER_SIZE)

    def u16(b, o):
        return b[o] | (b[o + 1] << 8)

    print("\n=== ALL LETTERS (idx: codeHEX char  L T W H) ===")
    for i in range(n.value):
        b = raw[i * E.LETTER_SIZE:(i + 1) * E.LETTER_SIZE]
        code = u16(b, 0x12)
        L, T, W, H = u16(b, 0), u16(b, 2), u16(b, 4), u16(b, 6)
        try:
            ch = chr(code)
        except Exception:
            ch = "?"
        printable = ch if (31 < code < 0x110000) else "."
        print("%3d: %04X %r  L%5d T%5d W%4d H%4d" % (i, code, printable, L, T, W, H))

    print("\n=== RAW first 12 structs (hex, 56 bytes each) ===")
    for i in range(min(12, n.value)):
        b = raw[i * E.LETTER_SIZE:(i + 1) * E.LETTER_SIZE]
        print("%3d: %s" % (i, b.hex()))

    k.kRecFreeImg(hPage)
    try:
        import os
        if os.path.exists(norm):
            os.remove(norm)
    except Exception:
        pass
    return 0


if __name__ == "__main__":
    sys.exit(main())
