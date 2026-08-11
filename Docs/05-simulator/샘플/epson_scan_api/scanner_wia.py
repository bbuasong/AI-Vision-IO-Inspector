"""
scanner_wia.py  -  Epson scanner control via WIA (pywin32).

WIA works well with the EPSON ES-C320W (detected as a WIA Scanner).
Scanning is manual/on-demand: call scan() when paper is on the glass/ADF.
If there's no paper, the driver raises an error -> we surface it cleanly.

Needs: pip install pywin32   (run on the Windows PC)
"""

import logging
import os
import time
from pathlib import Path

# WIA constants
WIA_FORMAT_BMP = "{B96B3CAB-0728-11D3-9D7B-0000F81EF32E}"
WIA_FORMAT_PNG = "{B96B3CAF-0728-11D3-9D7B-0000F81EF32E}"
WIA_FORMAT_JPEG = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}"

# WIA item property IDs
WIA_IPS_CUR_INTENT = 6146   # 1=color 2=grayscale 4=text(bw)
WIA_IPS_XRES = 6147
WIA_IPS_YRES = 6148
WIA_IPA_DATATYPE = 4103     # 0=bw threshold, 2=grayscale, 3=color
WIA_DPS_DOCUMENT_HANDLING_SELECT = 3088  # 1=FEEDER, 2=FLATBED

INTENT = {"color": 1, "gray": 2, "grayscale": 2, "bw": 4, "text": 4}
DATATYPE = {"color": 3, "gray": 2, "grayscale": 2, "bw": 0, "text": 0}


import contextlib


@contextlib.contextmanager
def _com():
    """Per-thread COM init (FastAPI runs requests on a threadpool)."""
    import pythoncom
    try:
        pythoncom.CoInitialize()
        inited = True
    except Exception:
        inited = False
    try:
        yield
    finally:
        if inited:
            try:
                pythoncom.CoUninitialize()
            except Exception:
                pass


def _dispatch(prog):
    import win32com.client
    return win32com.client.Dispatch(prog)


def list_scanners():
    """Return [{id, name}] of connected WIA scanners (Type==1)."""
    with _com():
        mgr = _dispatch("WIA.DeviceManager")
        out = []
        for i in range(1, mgr.DeviceInfos.Count + 1):
            info = mgr.DeviceInfos.Item(i)
            if info.Type != 1:  # 1 = Scanner
                continue
            name = ""
            try:
                for p in info.Properties:
                    if p.Name == "Name":
                        name = p.Value
            except Exception:
                pass
            out.append({"id": info.DeviceID, "name": name or info.DeviceID})
        return out


def _find_prop(props, pid):
    """This driver indexes Properties.Item() by POSITION, not by PropertyID,
    so locate the property by iterating and matching PropertyID."""
    try:
        n = props.Count
    except Exception:
        return None
    for i in range(1, n + 1):
        try:
            pr = props.Item(i)
            if int(pr.PropertyID) == int(pid):
                return pr
        except Exception:
            continue
    return None


def _set_prop(props, pid, value):
    pr = _find_prop(props, pid)
    if pr is None:
        return False
    try:
        pr.Value = value
        return True
    except Exception:
        return False


def _get_prop(props, pid, default=None):
    pr = _find_prop(props, pid)
    if pr is None:
        return default
    try:
        return pr.Value
    except Exception:
        return default


# device-level document-handling property IDs
WIA_DPS_DOCUMENT_HANDLING_CAPABILITIES = 3086
WIA_DPS_DOCUMENT_HANDLING_STATUS = 3087
CAP_FEEDER = 1
CAP_FLATBED = 2
STATUS_FEED_READY = 1
STATUS_FLAT_READY = 2


def scan(out_path, device_id=None, dpi=300, mode="gray", source="flatbed", fmt="bmp"):
    """
    Acquire one page to out_path. Returns out_path.
    Raises ScannerError with a clear message (e.g. no paper / no device).
    mode: color|gray|bw   source: flatbed|feeder   fmt: bmp|png|jpeg
    """
    import pythoncom
    with _com():
        mgr = _dispatch("WIA.DeviceManager")
        target = None
        for i in range(1, mgr.DeviceInfos.Count + 1):
            info = mgr.DeviceInfos.Item(i)
            if info.Type != 1:
                continue
            if device_id is None or info.DeviceID == device_id:
                target = info
                break
        if target is None:
            raise ScannerError("연결된 스캐너를 찾을 수 없습니다 (device_id=%s)." % device_id)

        device = target.Connect()

        caps = _get_prop(device.Properties, WIA_DPS_DOCUMENT_HANDLING_CAPABILITIES, 0) or 0
        has_feeder = bool(caps & CAP_FEEDER)
        has_flatbed = bool(caps & CAP_FLATBED)

        # choose source. This device may be ADF-only -> force feeder.
        want_feeder = str(source).lower() in ("feeder", "adf") or (has_feeder and not has_flatbed)
        sel = CAP_FEEDER if want_feeder else CAP_FLATBED
        _set_prop(device.Properties, WIA_DPS_DOCUMENT_HANDLING_SELECT, sel)

        # if using the feeder, make sure paper is actually detected
        if want_feeder:
            status = _get_prop(device.Properties, WIA_DPS_DOCUMENT_HANDLING_STATUS, 0) or 0
            if not (status & STATUS_FEED_READY):
                raise ScannerError("급지구에 용지가 감지되지 않습니다. 용지를 가이드에 맞춰 "
                                   "롤러가 물 때까지 끝까지 밀어 넣고 다시 시도하세요. (status=%s)" % status)

        item = device.Items.Item(1)
        xr = _set_prop(item.Properties, WIA_IPS_XRES, int(dpi))
        _set_prop(item.Properties, WIA_IPS_YRES, int(dpi))
        dt = _set_prop(item.Properties, WIA_IPA_DATATYPE, DATATYPE.get(mode, 2))
        try:
            _set_prop(item.Properties, WIA_IPS_CUR_INTENT, INTENT.get(mode, 2))
        except Exception:
            pass

        sel_now = _get_prop(device.Properties, WIA_DPS_DOCUMENT_HANDLING_SELECT)
        st_now = _get_prop(device.Properties, WIA_DPS_DOCUMENT_HANDLING_STATUS)
        logging.info("WIA scan: caps=%s select=%s status=%s xres_set=%s dtype_set=%s mode=%s dpi=%s",
                     caps, sel_now, st_now, xr, dt, mode, dpi)

        fmt_guid = {"bmp": WIA_FORMAT_BMP, "png": WIA_FORMAT_PNG, "jpeg": WIA_FORMAT_JPEG,
                    "jpg": WIA_FORMAT_JPEG}.get(str(fmt).lower(), WIA_FORMAT_BMP)

        image = None
        last_err = None
        for attempt in range(2):
            try:
                image = item.Transfer(fmt_guid)
                break
            except pythoncom.com_error as e:
                last_err = e
                hr = _extract_hresult(e)
                logging.warning("Transfer attempt %d failed: scode=0x%08X",
                                attempt + 1, (hr or 0) & 0xFFFFFFFF)
                if (hr or 0) & 0xFFFFFFFF == 0x80210003 and attempt == 0:
                    time.sleep(1.2)   # let the feeder settle, then retry once
                    continue
                raise ScannerError(_wia_error_text(hr) +
                                   (" [scode=0x%08X]" % (hr & 0xFFFFFFFF) if hr else "")) from e
        if image is None:
            hr = _extract_hresult(last_err) if last_err else None
            raise ScannerError(_wia_error_text(hr) +
                               (" [scode=0x%08X]" % (hr & 0xFFFFFFFF) if hr else ""))

        out_path = str(out_path)
        if Path(out_path).exists():
            Path(out_path).unlink()
        image.SaveFile(out_path)
        return out_path


def _extract_hresult(e):
    """Pull the HRESULT/scode out of a pywin32 com_error in a robust way."""
    try:
        if getattr(e, "excepinfo", None) and e.excepinfo[5]:
            return int(e.excepinfo[5])
    except Exception:
        pass
    for attr in ("hresult", "strerror"):
        v = getattr(e, attr, None)
        if isinstance(v, int):
            return v
    try:
        if e.args and isinstance(e.args[0], int):
            return e.args[0]
    except Exception:
        pass
    return None


def _wia_error_text(hr):
    table = {
        0x80210003: "스캐너에 용지가 없습니다 (ADF empty). 종이를 올리고 다시 시도하세요.",
        0x80210006: "스캐너가 준비되지 않았습니다 (busy/warming up).",
        0x80210001: "일반 스캔 오류입니다.",
        0x80210015: "스캐너를 사용할 수 없습니다 (offline/disconnected).",
        0x8021000A: "스캔 통신 오류입니다.",
        0x80210067: "스캔이 취소되었습니다.",
    }
    if hr is None:
        return "스캔 실패 (알 수 없는 오류)."
    return table.get(hr & 0xFFFFFFFF, "스캔 실패 (HRESULT 0x%08X)." % (hr & 0xFFFFFFFF))


class ScannerError(Exception):
    pass
