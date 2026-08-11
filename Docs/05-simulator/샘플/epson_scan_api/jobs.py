"""
jobs.py  -  in-memory scan-job registry (the "scan list").

A scanner has no native job queue to poll; this tracks the jobs THIS server
creates. Thread-safe enough for a single-worker dev server. Optional JSON
persistence so the list survives restarts.
"""

import json
import threading
import time
import uuid
from pathlib import Path

_LOCK = threading.Lock()
_JOBS = {}
_PERSIST = None  # Path to a json file, or None


def configure(persist_path=None):
    global _PERSIST
    _PERSIST = Path(persist_path) if persist_path else None
    if _PERSIST and _PERSIST.exists():
        try:
            data = json.loads(_PERSIST.read_text(encoding="utf-8"))
            with _LOCK:
                _JOBS.update({j["id"]: j for j in data})
        except Exception:
            pass


def _save():
    if not _PERSIST:
        return
    try:
        _PERSIST.write_text(json.dumps(list(_JOBS.values()), ensure_ascii=False, indent=2),
                            encoding="utf-8")
    except Exception:
        pass


def create(**fields):
    jid = uuid.uuid4().hex[:12]
    job = {
        "id": jid,
        "status": "created",   # created->scanning->scanned->preprocessed->ocr->done / error
        "created_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        "updated_at": time.strftime("%Y-%m-%d %H:%M:%S"),
        "image_path": None,
        "processed_path": None,
        "pdf_path": None,
        "error": None,
    }
    job.update(fields)
    with _LOCK:
        _JOBS[jid] = job
        _save()
    return dict(job)


def update(jid, **fields):
    with _LOCK:
        job = _JOBS.get(jid)
        if not job:
            return None
        job.update(fields)
        job["updated_at"] = time.strftime("%Y-%m-%d %H:%M:%S")
        _save()
        return dict(job)


def get(jid):
    with _LOCK:
        j = _JOBS.get(jid)
        return dict(j) if j else None


def list_all():
    with _LOCK:
        return sorted((dict(j) for j in _JOBS.values()),
                      key=lambda x: x["created_at"], reverse=True)


def delete(jid):
    with _LOCK:
        j = _JOBS.pop(jid, None)
        _save()
        return bool(j)
