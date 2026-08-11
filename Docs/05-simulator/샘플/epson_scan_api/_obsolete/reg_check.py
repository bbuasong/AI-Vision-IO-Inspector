"""
reg_check.py  v2  -  inspect / fix the Epson OCR registry profile.

EsOCR.dll resolves its config like this (recovered by disassembly):
  1. read  <EsOCR.dll dir>\Ocrsys.ini  [General] ProfileName
        -> e.g.  Software\EPSON\EPSON Scan\EsOCR\1.0
        (fallback "Software\EPSON\Ocrsys" only if ProfileName empty)
  2. registry key = HKCU\<ProfileName>\<section>   value <name>
        section "General", value "ProgramDir"  -> dictionary folder (ocrlib)

If HKCU\<ProfileName>\General\ProgramDir is missing, recognition fails
(SetRecogFile rc=2). Epson Scan normally populates this on first run.

Run:
    python reg_check.py          # read-only dump
    python reg_check.py --fix    # create the key with ProgramDir
"""

import configparser
import sys
import winreg
from pathlib import Path

INI = Path(r"C:\Program Files (x86)\epson\ESCNOCR\Ocrsys.ini")
PROGRAMDIR = r"C:\Program Files (x86)\epson\ESCNOCR\ocrlib" + "\\"
FALLBACK_PROFILE = r"Software\EPSON\Ocrsys"


def read_profile_name():
    try:
        txt = INI.read_text(encoding="mbcs", errors="replace")
    except Exception as e:
        print("  cannot read ini: %r" % (e,)); return FALLBACK_PROFILE
    cp = configparser.ConfigParser()
    cp.read_string(txt)
    pn = cp.get("General", "ProfileName", fallback="").strip()
    print("  ocrsys.ini ProfileName = %r" % pn)
    print("  ocrsys.ini ProgramDir  = %r" % cp.get("General", "ProgramDir", fallback=""))
    return pn or FALLBACK_PROFILE


def dump_key(hive_name, hive, subkey, depth=0, maxdepth=2):
    pad = "    " * depth
    try:
        k = winreg.OpenKey(hive, subkey, 0, winreg.KEY_READ)
    except FileNotFoundError:
        print("  %s[missing] %s\\%s" % (pad, hive_name, subkey)); return
    except Exception as e:
        print("  %s[error] %s\\%s : %r" % (pad, hive_name, subkey, e)); return
    print("  %s[FOUND] %s\\%s" % (pad, hive_name, subkey))
    i = 0
    while True:
        try:
            name, val, typ = winreg.EnumValue(k, i)
        except OSError:
            break
        print("  %s    %-16s = %r" % (pad, name, val)); i += 1
    if depth < maxdepth:
        j = 0
        while True:
            try:
                sk = winreg.EnumKey(k, j)
            except OSError:
                break
            dump_key(hive_name, hive, subkey + "\\" + sk, depth + 1, maxdepth)
            j += 1
    winreg.CloseKey(k)


def main():
    print("=== Epson OCR registry profile ===")
    profile = read_profile_name()
    print()
    print("--- HKCU\\Software\\EPSON tree ---")
    dump_key("HKCU", winreg.HKEY_CURRENT_USER, r"Software\EPSON", maxdepth=4)
    print()
    print("--- target profile key: HKCU\\%s ---" % profile)
    dump_key("HKCU", winreg.HKEY_CURRENT_USER, profile, maxdepth=2)

    if "--fix" in sys.argv:
        key = profile + r"\General"
        print()
        print("Creating HKCU\\%s  ProgramDir ..." % key)
        k = winreg.CreateKeyEx(winreg.HKEY_CURRENT_USER, key, 0, winreg.KEY_WRITE)
        winreg.SetValueEx(k, "ProgramDir", 0, winreg.REG_SZ, PROGRAMDIR)
        winreg.CloseKey(k)
        # also set at profile root, just in case
        k2 = winreg.CreateKeyEx(winreg.HKEY_CURRENT_USER, profile, 0, winreg.KEY_WRITE)
        winreg.SetValueEx(k2, "ProgramDir", 0, winreg.REG_SZ, PROGRAMDIR)
        winreg.CloseKey(k2)
        print("  set ProgramDir = %s" % PROGRAMDIR)
        print("  (both at \\General and profile root)")
        print("  now re-run: python epson_ocr_probe.py <img> <out.pdf>")


if __name__ == "__main__":
    main()
