"""
scan_diagnose.py  -  Scanner detection diagnostic (run on the PC)

Tries 3 detection paths and reports which one sees your Epson scanner:
  1. WIA   (via pywin32 / win32com)        - most reliable on Windows
  2. TWAIN (via the `twain` python module)  - Epson Scan 2 registers here
  3. Epson Ocrsys.dll (YndSelectSource exists; enumeration not exposed)

Run with the SAME 32-bit Python you use for the OCR server:
    python scan_diagnose.py

The scanner must be powered on and connected (USB or network).
"""

import struct
import sys


def line(c="-"):
    print(c * 60)


def header():
    line("=")
    print("Scanner detection diagnostic")
    line("=")
    print(f"Python: {sys.version.split()[0]} / {struct.calcsize('P') * 8}-bit")
    line()


def try_wia():
    print("[1] WIA (win32com)")
    try:
        import win32com.client
    except ImportError:
        print("    x pywin32 not installed  ->  pip install pywin32")
        return []
    found = []
    try:
        mgr = win32com.client.Dispatch("WIA.DeviceManager")
        for i in range(1, mgr.DeviceInfos.Count + 1):
            info = mgr.DeviceInfos.Item(i)
            # Type 1 = Scanner, 2 = Camera
            dtype = info.Type
            props = {}
            for p in info.Properties:
                try:
                    props[p.Name] = p.Value
                except Exception:
                    pass
            name = props.get("Name", "?")
            devid = info.DeviceID
            kind = {1: "Scanner", 2: "Camera"}.get(dtype, f"Type{dtype}")
            print(f"    - [{kind}] {name}")
            print(f"        DeviceID: {devid}")
            if dtype == 1:
                found.append({"name": name, "id": devid})
        if mgr.DeviceInfos.Count == 0:
            print("    (no WIA devices found)")
    except Exception as e:
        print(f"    x WIA error: {e}")
    return found


def try_twain():
    print("[2] TWAIN (twain module)")
    try:
        import twain
    except ImportError:
        print("    x `twain` not installed  ->  pip install twain")
        return []
    found = []
    sm = None
    try:
        sm = twain.SourceManager(0)
        sources = sm.GetSourceList()
        if not sources:
            print("    (no TWAIN sources)")
        for s in sources:
            print(f"    - {s}")
            found.append(s)
    except Exception as e:
        print(f"    x TWAIN error: {e}")
    finally:
        try:
            if sm:
                sm.destroy()
        except Exception:
            pass
    return found


def try_ocrsys():
    print("[3] Epson Ocrsys.dll")
    try:
        import ctypes
        from pathlib import Path
        dll_path = Path(r"C:\Program Files (x86)\epson\ESCNOCR\Ocrsys.dll")
        if not dll_path.exists():
            print("    x Ocrsys.dll not found")
            return
        if struct.calcsize("P") != 4:
            print("    x needs 32-bit Python")
            return
        import os
        esc = str(dll_path.parent)
        if hasattr(os, "add_dll_directory"):
            os.add_dll_directory(esc)
            ocrlib = dll_path.parent / "ocrlib"
            if ocrlib.exists():
                os.add_dll_directory(str(ocrlib))
        dll = ctypes.WinDLL(str(dll_path))
        has = [f for f in ("YndInit", "YndSelectSource", "YndScanImage",
                           "YndScanImageADF") if hasattr(dll, f)]
        print(f"    ok loaded. scan-related exports present: {has}")
        print("    note: YndSelectSource(1 arg) selects a TWAIN source; it does")
        print("          not enumerate. Use WIA/TWAIN above to list devices.")
    except Exception as e:
        print(f"    x error: {e}")


if __name__ == "__main__":
    header()
    wia = try_wia(); line()
    tw = try_twain(); line()
    try_ocrsys(); line("=")
    print("Summary:")
    print(f"  WIA scanners  : {len(wia)}")
    print(f"  TWAIN sources : {len(tw)}")
    if not wia and not tw:
        print("  -> No scanner detected. Check: powered on, cable/network,")
        print("     Epson Scan 2 can see it, driver installed.")
    else:
        print("  -> Use whichever path found your scanner to drive scanning.")
