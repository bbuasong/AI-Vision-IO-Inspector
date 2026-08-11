"""
scan_props.py  -  dump ALL WIA properties (device + item) for the scanner.

Tells us exactly what the ES-C320W WIA driver exposes, so we can drive it
correctly (paper detection, source select, supported formats/resolutions).

Run with paper loaded:  python scan_props.py
"""

import pythoncom
import win32com.client


def dump_props(coll, indent="    "):
    try:
        n = coll.Count
    except Exception as e:
        print(indent, "(no properties: %s)" % e); return
    for i in range(1, n + 1):
        try:
            p = coll.Item(i)
        except Exception as e:
            print(indent, "[%d] <err %s>" % (i, e)); continue
        try:
            pid = p.PropertyID
        except Exception:
            pid = "?"
        try:
            name = p.Name
        except Exception:
            name = "?"
        try:
            val = p.Value
        except Exception as e:
            val = "<err>"
        print("%s%-6s %-32s = %r" % (indent, pid, name, val))


def main():
    pythoncom.CoInitialize()
    mgr = win32com.client.Dispatch("WIA.DeviceManager")
    for i in range(1, mgr.DeviceInfos.Count + 1):
        info = mgr.DeviceInfos.Item(i)
        if info.Type != 1:
            continue
        print("=" * 60)
        print("SCANNER:", info.DeviceID)
        dev = info.Connect()
        print("--- DEVICE properties ---")
        dump_props(dev.Properties)
        print("--- ITEMS: %d ---" % dev.Items.Count)
        for k in range(1, dev.Items.Count + 1):
            it = dev.Items.Item(k)
            try:
                nm = it.Properties.Item(2).Value  # WIA_IPA_ITEM_NAME
            except Exception:
                nm = "item%d" % k
            print("  ITEM %d (%s) properties:" % (k, nm))
            dump_props(it.Properties, indent="      ")
    pythoncom.CoUninitialize()


if __name__ == "__main__":
    main()
