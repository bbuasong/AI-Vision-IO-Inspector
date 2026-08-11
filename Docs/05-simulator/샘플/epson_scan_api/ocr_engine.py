"""
ocr_engine.py  -  OCR 엔진 디스패처: Epson(OmniPage) 우선, Tesseract 폴백.

main.py 는 이 모듈만 호출한다.
  image_to_searchable_pdf(images, out_pdf, lang, engine="auto")
    engine: "auto"  -> Epson 시도, 실패 시 Tesseract
            "epson" -> Epson 만 (실패 시 에러)
            "tesseract" -> Tesseract 만
"""

import ocr_pdf            # Tesseract 경로 (기존)
import ocr_epson          # Epson OmniPage 경로 (신규)

OcrError = ocr_pdf.OcrError


def engine_status():
    ep_ok, ep_reason = ocr_epson.available()
    return {
        "epson": {"available": ep_ok, "reason": ep_reason},
        "tesseract_languages": ocr_pdf.available_languages(),
    }


def available_languages():
    langs = ocr_pdf.available_languages()
    ep_ok, _ = ocr_epson.available()
    return {"epson": (["kor"] if ep_ok else []), "tesseract": langs}


def image_to_searchable_pdf(images, out_pdf, lang="kor+eng", engine="auto"):
    engine = (engine or "auto").lower()
    if engine in ("auto", "epson"):
        try:
            return ocr_epson.image_to_searchable_pdf(images, out_pdf, lang=lang)
        except ocr_epson.EpsonOcrError as e:
            if engine == "epson":
                raise OcrError(f"Epson 엔진 실패: {e}")
            # auto -> Tesseract 폴백
            info = ocr_pdf.image_to_searchable_pdf(images, out_pdf, lang=lang)
            info["engine"] = "Tesseract (Epson 폴백: %s)" % e
            return info
    # tesseract
    info = ocr_pdf.image_to_searchable_pdf(images, out_pdf, lang=lang)
    info.setdefault("engine", "Tesseract")
    return info
