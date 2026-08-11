"""
scan_status.py  -  does the scanner actually see paper? (WIA, run on PC)

Reads the ADF/flatbed document-handling capabilities & status so we know
whether the feeder sensor detects your page before scanning.

Run:  python scan_status.py
"""

import pythoncom
import win32com.client

CAP = 3086     # WIA_DPS_DOCUMENT_HANDLING_CAPABILITIES
STATUS = 3087  # WIA_DPS_DOCUMENT_HANDLING_STATUS
SELECT = 3088  # WIA_DPS_DOCUMENT_HANDLING_SELECT

CAP_BITS = {1: "FEEDER", 2: "FLATBED", 4: "DUPLEX", 8: "ADVANCED_DUPLEX",
            16: "DETECT_FLATBED", 32: "DETECT_FEEDER", 256: "DETECT_FEED_AVAIL"}
STATUS_BITS = {1: "FEED_READY(용지있음)", 2: "FLAT_READY", 4: "DUP_READY",
               8: "PATH_COVER_OPEN", 16: "PAPER_JAM"}


def bits(val, table):
    return [name for b, name in table.items() if val & b] or ["(none)"]


def getp(props, pid):
    try:
        return props.Item(pid).Value
    except Exception as e:
        return "n/a (%s)" % e


def main():
    pythoncom.CoInitialize()
    mgr = win32com.client.Dispatch("WIA.DeviceManager")
    found = False
    for i in range(1, mgr.DeviceInfos.Count + 1):
        info = mgr.DeviceInfos.Item(i)
        if info.Type != 1:
            continue
        found = True
        name = info.DeviceID
        try:
            for p in info.Properties:
                if p.Name == "Name":
                    name = p.Value
        except Exception:
            pass
        print("scanner:", name)
        print("  DeviceID:", info.DeviceID)
        dev = info.Connect()
        cap = getp(dev.Properties, CAP)
        st = getp(dev.Properties, STATUS)
        sel = getp(dev.Properties, SELECT)
        print("  capabilities:", cap, bits(cap, CAP_BITS) if isinstance(cap, int) else "")
        print("  current select:", sel)
        print("  STATUS       :", st, bits(st, STATUS_BITS) if isinstance(st, int) else "")
        if isinstance(st, int):
            if st & 1:
                print("  => 급지구에 용지 감지됨 (FEED_READY). 스캔 가능.")
            else:
                print("  => 급지구에 용지 미감지. 용지를 롤러가 물 때까지 더 밀어 넣으세요.")
        # items
        try:
            print("  items:", dev.Items.Count)
        except Exception:
            pass
    if not found:
        print("연결된 WIA 스캐너가 없습니다.")
    pythoncom.CoUninitialize()


if __name__ == "__main__":
    main()
