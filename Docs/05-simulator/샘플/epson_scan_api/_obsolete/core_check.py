"""
core_check.py  -  can the OCR core DLLs load standalone?

Esocr_Init loads Ydocrd.dll (the recognition core) from ocrlib, and falls back
to a hardcoded dev path if that fails. If Ydocrd.dll (or a dependency) can't
load in our process, Init fails -> recognition always returns rc=7.

This tries LoadLibrary on each ocrlib DLL and reports which fail + the OS error.
A failure here = the root cause (likely a missing dependency).

Run with 32-bit Python:
    python core_check.py
"""

import ctypes
import os
import struct
from ctypes import wintypes
from pathlib import Path

ESCNOCR = Path(r"C:\Program Files (x86)\epson\ESCNOCR")
OCRLIB = ESCNOCR / "ocrlib"

CORE = ["Ydocrd.dll", "YdrecXX.dll", "Ydblock.dll", "Ydline.dll", "Ydcorr.dll",
        "Ydstyle.dll", "Ydetc.dll", "Ydprof.dll", "ydtable.dll",
        "Col_bin.dll", "Edt_img.dll", "Skw_mem.dll", "Rot_mem.dll",
        "Bmp_mem.dll", "Jpg_mem.dll", "Mem_pdf.dll", "mem_txt.dll",
        "Lngdic.dll", "Usrdic.dll", "Cnv_res.dll", "Enclcsid.dll",
        "Mem_bmp.dll", "Mem_jpg.dll", "Mem_wak.dll", "Wak_mem.dll"]

print("python %d-bit" % (struct.calcsize("P") * 8))
print("ocrlib:", OCRLIB, "exists:", OCRLIB.exists())

# set search path so dependencies in ocrlib resolve
if hasattr(os, "add_dll_directory"):
    os.add_dll_directory(str(ESCNOCR))
    if OCRLIB.exists():
        os.add_dll_directory(str(OCRLIB))
os.environ["PATH"] = str(ESCNOCR) + ";" + str(OCRLIB) + ";" + os.environ.get("PATH", "")

LoadLibraryExW = ctypes.windll.kernel32.LoadLibraryExW
LoadLibraryExW.restype = wintypes.HMODULE
LoadLibraryExW.argtypes = [wintypes.LPCWSTR, wintypes.HANDLE, wintypes.DWORD]
LOAD_WITH_ALTERED_SEARCH_PATH = 0x8

ok, fail = [], []
for name in CORE:
    p = OCRLIB / name
    if not p.exists():
        print("  [missing file] %s" % name); fail.append((name, "file missing")); continue
    h = LoadLibraryExW(str(p), None, LOAD_WITH_ALTERED_SEARCH_PATH)
    if h:
        ok.append(name)
        print("  [OK]   %s" % name)
    else:
        err = ctypes.get_last_error() or ctypes.GetLastError()
        we = ctypes.WinError(ctypes.get_last_error())
        print("  [FAIL] %s   err=%s" % (name, we))
        fail.append((name, str(we)))

print()
print("loaded OK : %d / %d" % (len(ok), len(CORE)))
if fail:
    print("FAILED:")
    for n, e in fail:
        print("   %s : %s" % (n, e))
    print("\n-> A failing core DLL (esp. Ydocrd.dll) is the likely reason Init fails.")
else:
    print("All core DLLs load standalone -> Init failure is NOT a DLL-load problem.")
