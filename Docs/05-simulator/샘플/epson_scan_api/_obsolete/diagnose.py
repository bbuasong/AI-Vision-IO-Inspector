"""
diagnose.py  –  Epson OCR DLL 진단 (사용자 PC에서 실행)
========================================================
이 스크립트는 "왜 OCR DLL 이 안 먹히는가"를 한 번에 진단한다.
리눅스가 아니라 DLL 이 설치된 Windows PC 에서 돌려야 한다.

실행:
    python diagnose.py

확인 항목:
    1) 파이썬 비트 (32 vs 64)  ← DLL 은 32비트라 64면 무조건 실패
    2) DLL/의존폴더 존재 여부
    3) EsOCR.dll 로드 + Esocr_GetVer 호출 (안전 프로브)
    4) Tesseract 설치 여부 + 한국어 데이터
"""

import ctypes
import struct
import sys
from pathlib import Path

ESCNOCR = Path(r"C:\Program Files (x86)\epson\ESCNOCR")


def line(c="-"):
    print(c * 60)


def check_python_bits():
    bits = struct.calcsize("P") * 8
    print(f"[1] Python : {sys.version.split()[0]} / {bits}-bit")
    if bits == 64:
        print("    ✗ 64비트입니다. EsOCR.dll/Ocrsys.dll(32비트) 로드 불가.")
        print("      → 32비트 파이썬 설치 후 그 파이썬으로 이 서버를 실행하세요.")
        print("      (python.org → Windows installer (32-bit))")
        return False
    print("    ✓ 32비트 — DLL 로드 가능 조건 충족")
    return True


def check_files():
    print("[2] 파일/폴더")
    ok = True
    for p in [ESCNOCR / "EsOCR.dll", ESCNOCR / "Ocrsys.dll",
              ESCNOCR / "ocrlib", ESCNOCR / "ocrlib" / "dic"]:
        mark = "✓" if p.exists() else "✗"
        if not p.exists():
            ok = False
        print(f"    {mark} {p}")
    return ok


def check_esocr_load():
    print("[3] EsOCR.dll 로드 + GetVer 프로브")
    try:
        from ocr import esocr_probe
        info = esocr_probe()
        for k, v in info.items():
            print(f"    {k}: {v}")
        return info.get("loaded", False)
    except Exception as e:
        print(f"    ✗ 예외: {e}")
        return False


def check_tesseract():
    print("[4] Tesseract")
    try:
        import pytesseract
    except ImportError:
        print("    ✗ pytesseract 미설치 (pip install pytesseract)")
        return False
    from ocr import _tesseract_exe
    exe = _tesseract_exe()
    if exe:
        print(f"    ✓ tesseract.exe: {exe}")
    else:
        print("    ? PATH 에서 tesseract 탐색 (명시 경로 못 찾음)")
    try:
        langs = pytesseract.get_languages()
        print(f"    설치된 언어: {langs}")
        if "kor" not in langs:
            print("    ✗ 한국어(kor) 미설치 → kor.traineddata 추가 필요")
    except Exception as e:
        print(f"    ? 언어 조회 실패: {e}")
    return True


if __name__ == "__main__":
    line("=")
    print("Epson OCR 진단")
    line("=")
    b = check_python_bits(); line()
    check_files(); line()
    if b:
        check_esocr_load()
    else:
        print("[3] (64비트라 EsOCR 프로브 생략)")
    line()
    check_tesseract()
    line("=")
    print("요약: [1]이 32비트 + [3] loaded=True 여야 EsOCR 경로 진행 가능.")
    print("      아니면 Tesseract 경로([4])로 OCR 동작.")
