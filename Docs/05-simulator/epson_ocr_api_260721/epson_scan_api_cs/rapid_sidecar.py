"""
rapid_sidecar.py  -  RapidOCR part_no 사이드카 (C# 서버용, 납품 동봉).

C# 서버는 Epson DLL 때문에 x86으로 도는데 .NET용 onnxruntime은 x86 네이티브가 없어
인프로세스로 못 쓴다. 그래서 RapidOCR(파이썬/ONNX)을 별도 프로세스로 띄워 HTTP로
부품번호만 받아온다. C# 서버가 앱 폴더의 번들 파이썬으로 이 스크립트를 자동 기동한다.

실행:
    python rapid_sidecar.py            # 기본 127.0.0.1:8011
    set RAPID_SIDECAR_PORT=8011        # 포트 변경 시

API:
    GET  /health           -> {"ok": true, "available": <RapidOCR 로드 여부>, "reason": ...}
    POST /part_no  {"image_path": "...png"}  -> {"part_no": "...", "conf": 0.93}

미설치/오류여도 죽지 않고 part_no=""를 돌려준다(C#은 그러면 Epson 결과로 폴백).
"""

import json
import os
import re
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

_ENGINE = None
_LOAD_ERROR = None

def _engine():
    global _ENGINE, _LOAD_ERROR
    if _ENGINE is not None or _LOAD_ERROR is not None:
        return _ENGINE
    try:
        from rapidocr_onnxruntime import RapidOCR
        _ENGINE = RapidOCR()
    except Exception as e:
        _LOAD_ERROR = str(e)
        _ENGINE = None
    return _ENGINE


def _clean(s):
    return re.sub(r"\s+", "", (s or "").upper())

# ── 튜닝값을 rapid_sidecar.config.json(스크립트/exe 옆)에서 읽는다. 없으면 기본값 사용. ──
_DEFAULTS = {
    # 부품번호 정규식: 글자로 시작하는 품번(예: S018-16070D)도 잡도록 맨 앞 글자 허용.
    #   (기존 "[0-9]..." 는 숫자로 시작 강제라 S 가 떨어졌음)
    "part_no_pattern": r"[A-Z0-9]+(?:-[A-Z0-9]+)*",
    "noise": ["AI", "AL", "2EA", "2E4", "IT", "EA", "RCV", "RH", "WORKING", "미", "검", "수", "검수"],
    "min_len": 6,          # part_no 최소 길이
    "min_digits": 4,       # 최소 숫자 개수
    "top_frac": 0.40,      # 상단 몇 % 영역을 부품번호 후보로 볼지
}

def _cfg_dir():
    # PyInstaller exe 로 실행돼도 config 를 exe 옆에서 찾도록.
    if getattr(sys, "frozen", False):
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))

def _load_cfg():
    cfg = dict(_DEFAULTS)
    try:
        path = os.path.join(_cfg_dir(), "rapid_sidecar.config.json")
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                user = json.load(f)
            for k in _DEFAULTS:
                if k in user and user[k] not in (None, ""):
                    cfg[k] = user[k]
    except Exception:
        pass  # 설정 파일이 깨졌거나 없으면 기본값 사용
    return cfg

_CFG = _load_cfg()
_NOISE = {str(x).upper() for x in _CFG["noise"]}
_PAT = re.compile(_CFG["part_no_pattern"])
_MIN_LEN = int(_CFG["min_len"])
_MIN_DIGITS = int(_CFG["min_digits"])
_TOP_FRAC = float(_CFG["top_frac"])

def _candidate(tok):
    cc = _clean(tok)
    if cc in _NOISE:
        return ""
    m = _PAT.search(cc)
    if m and len(m.group()) >= _MIN_LEN and sum(ch.isdigit() for ch in m.group()) >= _MIN_DIGITS:
        return m.group()
    return ""

def _extract(res, img_h):
    """(part_no, part_no_sub, conf) 반환.
    part_no_sub = '(숫자)' 괄호번호만 깔끔하게(예: (181420252)). 앞 토큰이 붙어도 분리."""
    if not res:
        return "", "", 0.0
    items = []
    for box, txt, conf in res:
        ys = [p[1] for p in box]; xs = [p[0] for p in box]
        items.append({"t": txt, "c": float(conf), "y": min(ys), "x": min(xs)})
    top = [it for it in items if it["y"] < img_h * _TOP_FRAC] or items
    top.sort(key=lambda it: (it["y"], it["x"]))   # 위→아래, 왼→오
    line = " ".join(it["t"] for it in top)

    # 괄호번호(sub)는 따로 추출만 한다(부품번호 '선택' 기준으로는 쓰지 않음).
    sub = ""
    sub_digits = ""
    m = re.search(r"[\(/]\s*(\d{4,})", line)   # 괄호번호(숫자만 캡처) → 깔끔한 (숫자)로 재구성
    if m:
        sub_digits = m.group(1)
        sub = "(%s)" % sub_digits

    # 부품번호: 상단 영역에서 '위→아래, 왼→오' 순으로 첫 후보 = 제일 위/왼쪽.
    #   단, 괄호 sub 숫자 그 자체는 부품번호로 쓰지 않는다.
    for it in top:
        c = _candidate(it["t"])
        if c and c != sub_digits:
            return c, sub, round(it["c"], 3)
    return "", sub, 0.0

def read_part_no(image_path):
    eng = _engine()
    if eng is None:
        return "", "", 0.0
    try:
        import numpy as np
        from PIL import Image
        im = Image.open(image_path).convert("RGB")
        res, _ = eng(np.array(im))
        return _extract(res, im.height)
    except Exception:
        return "", "", 0.0


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, obj):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path.startswith("/health"):
            self._send(200, {"ok": True, "available": _engine() is not None, "reason": _LOAD_ERROR})
        else:
            self._send(404, {"error": "not found"})

    def do_POST(self):
        if not self.path.startswith("/part_no"):
            self._send(404, {"error": "not found"}); return
        try:
            n = int(self.headers.get("Content-Length", 0))
            data = json.loads(self.rfile.read(n) or b"{}")
            path = data.get("image_path", "")
            pn, sub, cf = read_part_no(path) if path else ("", "", 0.0)
            self._send(200, {"part_no": pn, "part_no_sub": sub, "conf": cf})
        except Exception as e:
            self._send(200, {"part_no": "", "conf": 0.0, "error": str(e)})

    def log_message(self, *a):
        pass


if __name__ == "__main__":
    host = os.getenv("RAPID_SIDECAR_HOST", "127.0.0.1")
    port = int(os.getenv("RAPID_SIDECAR_PORT", "8011"))
    _engine()  # 시작 시 모델 미리 로드
    print(f"[rapid_sidecar] http://{host}:{port}  (RapidOCR available={_ENGINE is not None})")
    HTTPServer((host, port), Handler).serve_forever()
