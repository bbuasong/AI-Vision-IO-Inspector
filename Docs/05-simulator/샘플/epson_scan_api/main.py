"""
Epson Scan API  -  main.py  (FastAPI)
=====================================
스캔(수동) -> 작업 목록 -> (선택)전처리 -> 검색가능 PDF 저장.

엔드포인트:
  GET  /scanners                  연결된 스캐너 목록 (WIA)
  GET  /languages                 사용 가능한 OCR 언어 (tessdata)
  POST /scan                      스캔 실행 -> 작업 생성 (종이 없으면 에러)
  GET  /jobs                       작업(스캔) 목록
  GET  /jobs/{id}                  작업 상세
  POST /jobs/{id}/extract-card     명함/문서 영역 검출+와프 (OpenCV 별도단계, CARD_EXTRACT_PY)
  POST /jobs/{id}/preprocess       이미지 전처리 (그레이/이진화/디스큐 등)
  POST /jobs/{id}/redact           특정 영역 덮기(가림) — DPI/해상도 보존
  POST /jobs/{id}/pdf              검색가능 PDF 생성·저장 (engine: auto|epson|tesseract)
  POST /scan-to-pdf                스캔→(카드)→(전처리)→(덮기)→PDF 한 번에
  가공 단계는 선형 누적: 각 단계가 직전 결과 위에 적용, 마지막 결과로 OCR.
  GET  /jobs/{id}/download/{kind}  image|processed|pdf 다운로드
  DELETE /jobs/{id}                작업 삭제

실행:
  pip install -r requirements.txt
  python main.py        (또는: uvicorn main:app --port 8000)
  Swagger: http://localhost:8000/docs
"""

import logging
import os
import subprocess
import sys
import traceback
import uuid
from pathlib import Path
from typing import Optional

from fastapi import FastAPI, HTTPException
from fastapi.responses import FileResponse
from pydantic import BaseModel

import jobs
import ocr_engine
import preprocess as PP
import redact as RD
import card_pil

# 스캐너 모듈은 Windows(pywin32)에서만 import 가능 -> 지연 import
def _scanner():
    import scanner_wia
    return scanner_wia

OUTPUT_DIR = Path(os.getenv("SCAN_OUTPUT_DIR", r"D:\epson_scans"))
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
jobs.configure(OUTPUT_DIR / "jobs.json")

app = FastAPI(title="Epson Scan API", version="2.0.0")


# ---------- models ----------
class ScanRequest(BaseModel):
    device_id: Optional[str] = None
    dpi: int = 300
    mode: str = "gray"          # color|gray|bw
    source: str = "flatbed"     # flatbed|feeder
    fmt: str = "bmp"            # bmp|png|jpeg

class PreprocessRequest(BaseModel):
    grayscale: bool = False
    autocontrast: bool = False
    binarize: str = "none"      # none|otsu|fixed
    threshold: int = 160
    deskew: bool = False
    rotate: int = 0
    resize_maxdim: int = 0
    denoise: bool = False
    border_crop: int = 0

class PdfRequest(BaseModel):
    lang: str = "kor+eng"
    use_processed: bool = True  # 전처리본/덮기본이 있으면 그걸로 OCR
    engine: str = "auto"        # auto(Epson 우선)|epson|tesseract

class OcrImageRequest(BaseModel):
    image_path: str
    lang: str = "kor+eng"
    engine: str = "auto"

class RedactRequest(BaseModel):
    rects: list = []            # [[x,y,w,h], ...] 픽셀 좌표
    fill: str = "white"         # white|black

class CardRequest(BaseModel):
    dpi: int = 300              # warp 결과 DPI 태깅 (OCR 품질 핵심)
    debug: bool = False         # 검출 사각형 표시본도 저장

class ScanToPdfRequest(BaseModel):
    scan: ScanRequest = ScanRequest()
    card: Optional[CardRequest] = CardRequest()   # 기본 ON: 라벨 자동 deskew+크롭 ("card":null 로 끄기)
    preprocess: Optional[PreprocessRequest] = None  # Epson 엔진엔 보통 불필요(이진화 금지)
    redact: Optional[RedactRequest] = None
    pdf: PdfRequest = PdfRequest()


# ---------- scanner ----------
@app.get("/scanners", summary="연결된 스캐너 목록")
def scanners():
    try:
        return {"scanners": _scanner().list_scanners()}
    except Exception as e:
        raise HTTPException(500, "스캐너 조회 실패: %s" % e)

@app.get("/languages", summary="사용 가능한 OCR 언어 / 엔진 상태")
def languages():
    return {"languages": ocr_engine.available_languages(),
            "engines": ocr_engine.engine_status()}


@app.post("/ocr-image", summary="기존 이미지 파일 OCR")
def ocr_image(req: OcrImageRequest):
    src = Path(req.image_path)
    if not src.exists():
        raise HTTPException(404, "이미지 파일이 없습니다: %s" % src)

    out_pdf = OUTPUT_DIR / ("external_%s.pdf" % uuid.uuid4().hex)
    try:
        info = ocr_engine.image_to_searchable_pdf(str(src), str(out_pdf), lang=req.lang, engine=req.engine)
    except ocr_engine.OcrError as e:
        raise HTTPException(409, str(e))
    except Exception as e:
        raise HTTPException(500, "OCR/PDF 실패: %s" % e)

    return {
        "image_path": str(src),
        "pdf_path": str(out_pdf),
        "engine": info.get("engine", req.engine),
        "text": info.get("text", ""),
        "part_no": info.get("part_no", ""),
        "ocr": info,
    }


@app.post("/scan", summary="스캔 실행 (작업 생성)")
def scan(req: ScanRequest):
    sc = _scanner()
    # Swagger sends "string" as a placeholder; treat empty/placeholder as auto-select.
    dev = req.device_id
    if not dev or dev.strip().lower() in ("string", "none", "null"):
        dev = None
    job = jobs.create(status="scanning", params=req.dict())
    ext = {"jpeg": "jpg"}.get(req.fmt, req.fmt)
    img_path = OUTPUT_DIR / ("%s_raw.%s" % (job["id"], ext))
    try:
        sc.scan(img_path, device_id=dev, dpi=req.dpi,
                mode=req.mode, source=req.source, fmt=req.fmt)
    except sc.ScannerError as e:
        logging.warning("scan failed (job %s): %s", job["id"], e)
        jobs.update(job["id"], status="error", error=str(e))
        raise HTTPException(409, str(e))
    except Exception as e:
        logging.error("scan crashed (job %s): %s\n%s", job["id"], e, traceback.format_exc())
        jobs.update(job["id"], status="error", error=str(e))
        raise HTTPException(500, "스캔 실패: %s" % e)
    return jobs.update(job["id"], status="scanned", image_path=str(img_path))


# ---------- jobs ----------
@app.get("/jobs", summary="스캔 작업 목록")
def list_jobs():
    return {"count": len(jobs.list_all()), "jobs": jobs.list_all()}

@app.get("/jobs/{jid}", summary="작업 상세")
def get_job(jid: str):
    j = jobs.get(jid)
    if not j:
        raise HTTPException(404, "작업을 찾을 수 없습니다.")
    return j

@app.delete("/jobs/{jid}", summary="작업 삭제")
def del_job(jid: str):
    return {"deleted": jobs.delete(jid)}


@app.post("/jobs/{jid}/preprocess", summary="이미지 전처리")
def do_preprocess(jid: str, opts: PreprocessRequest):
    j = jobs.get(jid)
    if not j:
        raise HTTPException(404, "작업을 찾을 수 없습니다.")
    base = _base(j)
    if not base or not Path(base).exists():
        raise HTTPException(409, "스캔 이미지가 없습니다.")
    out = OUTPUT_DIR / ("%s_proc.png" % jid)
    try:
        PP.preprocess_file(base, str(out), opts.dict())
    except Exception as e:
        raise HTTPException(500, "전처리 실패: %s" % e)
    return jobs.update(jid, status="preprocessed", processed_path=str(out), ocr_src_path=str(out))


@app.post("/jobs/{jid}/redact", summary="이미지 특정 영역 덮기(가림) — DPI/해상도 보존")
def do_redact(jid: str, req: RedactRequest):
    j = jobs.get(jid)
    if not j:
        raise HTTPException(404, "작업을 찾을 수 없습니다.")
    base = _base(j)
    if not base or not Path(base).exists():
        raise HTTPException(409, "원본 이미지가 없습니다.")
    out = OUTPUT_DIR / ("%s_redacted.png" % jid)
    try:
        saved = RD.cover_regions(base, str(out), req.rects, fill=req.fill)
    except Exception as e:
        raise HTTPException(500, "영역 덮기 실패: %s" % e)
    return jobs.update(jid, status="redacted", redacted_path=str(saved), ocr_src_path=str(saved))


# 카드 검출+와프는 OpenCV 별도 환경에서 수행. 그 파이썬 경로를 환경변수로 지정.
#   예) set CARD_EXTRACT_PY=C:\Python311-64\python.exe   (opencv-python 설치된 64비트)
CARD_EXTRACT_PY = os.getenv("CARD_EXTRACT_PY", "")
_CARD_SCRIPT = str(Path(__file__).with_name("card_extract.py"))


@app.post("/jobs/{jid}/extract-card", summary="라벨 자동 검출+deskew+크롭 (기본: PIL 인프로세스)")
def extract_card(jid: str, req: CardRequest):
    j = jobs.get(jid)
    if not j:
        raise HTTPException(404, "작업을 찾을 수 없습니다.")
    src = j.get("image_path")
    if not src or not Path(src).exists():
        raise HTTPException(409, "스캔 이미지가 없습니다.")
    out = str(OUTPUT_DIR / ("%s_card.png" % jid))

    # OpenCV(원근 보정까지)가 설정돼 있으면 우선 사용, 실패/미설정이면 PIL 인프로세스로 폴백.
    if CARD_EXTRACT_PY and Path(CARD_EXTRACT_PY).exists():
        cmd = [CARD_EXTRACT_PY, _CARD_SCRIPT, src, out, "--dpi", str(req.dpi)]
        if req.debug:
            cmd.append("--debug")
        try:
            r = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
            if r.returncode == 0 and Path(out).exists():
                return jobs.update(jid, status="card", card_path=out, ocr_src_path=out,
                                   card_log=("opencv: " + (r.stdout or "").strip()))
        except Exception:
            pass  # PIL 폴백

    # 기본: PIL 전용 (deskew + 라벨 크롭). 설치/별도 프로세스 불필요.
    try:
        saved, info = card_pil.extract_label(src, out, dpi=req.dpi)
    except Exception as e:
        raise HTTPException(500, "카드 추출 실패: %s" % e)
    return jobs.update(jid, status="card", card_path=saved, ocr_src_path=saved,
                       card_log="pil: %s" % info)


def _base(j):
    """현재까지 가공된 최신 이미지(카드/전처리/덮기 체인의 마지막). 없으면 원본."""
    p = j.get("ocr_src_path")
    if p and Path(p).exists():
        return p
    return j.get("image_path")


def _ocr_source(j, use_processed=True):
    """OCR 입력 = 가공 체인의 최신 결과(없으면 원본). 단계들은 선형으로 누적됨."""
    return _base(j)


@app.post("/jobs/{jid}/pdf", summary="검색가능 PDF 생성")
def make_pdf(jid: str, req: PdfRequest):
    j = jobs.get(jid)
    if not j:
        raise HTTPException(404, "작업을 찾을 수 없습니다.")
    src = _ocr_source(j, req.use_processed)
    if not src or not Path(src).exists():
        raise HTTPException(409, "OCR할 이미지가 없습니다.")
    out_pdf = OUTPUT_DIR / ("%s.pdf" % jid)
    try:
        info = ocr_engine.image_to_searchable_pdf(src, str(out_pdf), lang=req.lang, engine=req.engine)
    except ocr_engine.OcrError as e:
        jobs.update(jid, status="error", error=str(e))
        raise HTTPException(409, str(e))
    except Exception as e:
        jobs.update(jid, status="error", error=str(e))
        raise HTTPException(500, "OCR/PDF 실패: %s" % e)
    # 품질 낮으면 status=low_quality (UI가 재스캔 유도). PDF/필드는 그대로 제공.
    q = (info.get("quality") or {})
    status = "done" if q.get("ok", True) else "low_quality"
    return jobs.update(jid, status=status, pdf_path=str(out_pdf), ocr=info)


@app.post("/scan-to-pdf", summary="스캔→(전처리)→PDF 한 번에")
def scan_to_pdf(req: ScanToPdfRequest):
    job = scan(req.scan)               # may raise
    jid = job["id"]
    if req.card is not None:
        extract_card(jid, req.card)    # 카드 검출+와프 (CARD_EXTRACT_PY 필요)
    if req.preprocess is not None:
        do_preprocess(jid, req.preprocess)
    if req.redact is not None:
        do_redact(jid, req.redact)
    return make_pdf(jid, req.pdf)


@app.get("/jobs/{jid}/download/{kind}", summary="파일 다운로드")
def download(jid: str, kind: str):
    j = jobs.get(jid)
    if not j:
        raise HTTPException(404, "작업을 찾을 수 없습니다.")
    key = {"image": "image_path", "processed": "processed_path", "card": "card_path",
           "redacted": "redacted_path", "pdf": "pdf_path"}.get(kind)
    if not key:
        raise HTTPException(400, "kind는 image|processed|card|redacted|pdf 중 하나여야 합니다.")
    path = j.get(key)
    if not path or not Path(path).exists():
        raise HTTPException(404, "파일이 없습니다.")
    return FileResponse(path, filename=Path(path).name)


@app.get("/jobs/{jid}/fields", summary="OCR 파싱 필드(줄/단어 + bbox)")
def get_fields(jid: str):
    j = jobs.get(jid)
    if not j:
        raise HTTPException(404, "작업을 찾을 수 없습니다.")
    ocr = j.get("ocr") or {}
    return {"fields": ocr.get("fields", {}),   # {"line_1_1": "...", ...}
            "lines": ocr.get("lines", []),     # [{line,text,words:[{text,bbox:[x,y,w,h]}]}]
            "text": ocr.get("text", "")}


@app.get("/health")
def health():
    return {"status": "ok", "output_dir": str(OUTPUT_DIR),
            "engines": ocr_engine.engine_status()}


if __name__ == "__main__":
    import uvicorn
    # 같은 PC에서만 쓰면 127.0.0.1(로컬 전용·안전). 다른 PC에서 접속하려면 SCAN_API_HOST=0.0.0.0
    host = os.getenv("SCAN_API_HOST", "127.0.0.1")
    port = int(os.getenv("SCAN_API_PORT", "8000"))
    uvicorn.run("main:app", host=host, port=port, reload=False)
