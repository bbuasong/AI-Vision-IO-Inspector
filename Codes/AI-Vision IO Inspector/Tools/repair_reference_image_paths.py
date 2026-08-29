# -*- coding: utf-8 -*-
"""기준 이미지 경로 정정 도구

DB의 PartList_ReferenceImages 가 가리키는 파일이 이미지 폴더에 없을 때,
그 품번 폴더에서 여섯 방향이 모두 있는 최신 벌을 찾아 다시 연결합니다.

왜 필요한가
    2026-08-21 현장에서 기준 이미지 파일명을 새 규칙([01_Top][001][품번]_시각)으로
    바꿨는데 DB 의 file_path 는 옛 이름(품번_Top.png)이나 다른 컴퓨터의 절대 경로를
    그대로 갖고 있어, 43개 품번 254행이 실제 파일과 어긋났습니다. 사진은 폴더에
    멀쩡히 있으므로 연결만 고치면 재촬영이 필요 없습니다.

사용법 (개발 PC 에서 실행)
    1. 현장 DB(DataBase.db)와 이미지 폴더(Temp_Image)를 복사해 옵니다.
    2. 미리보기:  python repair_reference_image_paths.py --db <DB경로> --image-root <이미지폴더>
    3. 목록을 확인한 뒤 반영:  위 명령에 --apply 를 붙입니다.

원칙
    - 반영 전에 DB 를 같은 폴더에 시각을 붙여 복사해 둡니다.
    - 여섯 방향이 모두 있는 벌만 씁니다. 방향마다 다른 벌을 섞지 않습니다.
    - 완전한 벌이 없거나 폴더 자체가 없는 품번은 손대지 않고 보고만 합니다.
    - 새 경로는 REFERENCE:\\분류\\품번\\파일명 상대 형태로 적습니다.
      (2026-08-28 부터 프로그램이 이 형태를 읽고 씁니다)
"""
import argparse
import datetime
import io
import os
import re
import shutil
import sqlite3
import sys

VIEWS = ["Top", "Front", "Back", "Left", "Right", "Thickness"]
NEW_NAME = re.compile(
    r"^\[(\d{2})_(\w+)\]\[(\d+)\]\[(.+?)\]_(\d{8}-\d{6})\.(png|jpg|jpeg|bmp)$",
    re.IGNORECASE)
PREFIX = "REFERENCE:\\\\"


def scan_image_root(image_root):
    """품번 -> (분류, {벌번호: {방향: (파일명, 시각)}}) 을 만듭니다."""
    parts = {}
    for category in os.listdir(image_root):
        category_path = os.path.join(image_root, category)
        if not os.path.isdir(category_path) or category == "Temp":
            continue
        for part_no in os.listdir(category_path):
            part_path = os.path.join(category_path, part_no)
            if not os.path.isdir(part_path):
                continue
            sets = {}
            for file_name in os.listdir(part_path):
                m = NEW_NAME.match(file_name)
                if not m:
                    continue
                view, set_no, stamp = m.group(2), int(m.group(3)), m.group(5)
                if view in VIEWS:
                    sets.setdefault(set_no, {})[view] = (file_name, stamp)
            parts[part_no] = (category, sets)
    return parts


def choose_full_set(sets):
    """여섯 방향이 모두 있는 벌 중 가장 최근 것을 고릅니다. 없으면 None."""
    full = [(no, views) for no, views in sets.items() if len(views) == len(VIEWS)]
    if not full:
        return None
    return max(full, key=lambda item: (max(v[1] for v in item[1].values()), item[0]))


def resolve_stored(stored_path, image_root):
    """DB 에 담긴 경로를 이 컴퓨터 기준 절대 경로로 풀어 봅니다."""
    p = (stored_path or "").strip()
    if p.upper().startswith("REFERENCE:"):
        tail = p[len("REFERENCE:"):].lstrip("\\/")
        return os.path.join(image_root, tail)
    # 절대 경로: Temp_Image 꼬리를 떼어 현재 루트에 붙여 봅니다.
    low = p.replace("/", "\\").lower()
    marker = low.find("temp_image\\")
    if marker >= 0:
        tail = p.replace("/", "\\")[marker + len("temp_image\\"):]
        return os.path.join(image_root, tail)
    return p


def stamp_to_iso(stamp):
    t = datetime.datetime.strptime(stamp, "%Y%m%d-%H%M%S")
    return t.strftime("%Y-%m-%dT%H:%M:%S") + ".0000000+09:00"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--db", required=True)
    ap.add_argument("--image-root", required=True)
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    if not os.path.isfile(args.db):
        sys.exit("DB 가 없습니다: " + args.db)
    if not os.path.isdir(args.image_root):
        sys.exit("이미지 폴더가 없습니다: " + args.image_root)

    parts = scan_image_root(args.image_root)
    con = sqlite3.connect(args.db)
    cur = con.cursor()
    cur.execute("SELECT id, part_no, view_type, file_path, set_no FROM PartList_ReferenceImages ORDER BY part_no, view_type")
    rows = cur.fetchall()

    changes = []          # (id, part, view, old, new_rel, set_no, iso)
    unrepairable = {}     # part -> 사유
    ok = 0
    for row_id, part_no, view_type, stored, _set in rows:
        resolved = resolve_stored(stored, args.image_root)
        if os.path.isfile(resolved):
            ok += 1
            continue
        view = VIEWS[view_type] if 0 <= view_type < len(VIEWS) else None
        if view is None:
            unrepairable[part_no] = "방향 값이 범위 밖입니다 (view_type=%d)" % view_type
            continue
        if part_no not in parts:
            unrepairable[part_no] = "이미지 폴더에 품번 폴더가 없습니다"
            continue
        category, sets = parts[part_no]
        chosen = choose_full_set(sets)
        if chosen is None:
            unrepairable[part_no] = "여섯 방향이 모두 있는 벌이 없습니다"
            continue
        set_no, views = chosen
        file_name, stamp = views[view]
        new_rel = PREFIX + category + "\\" + part_no + "\\" + file_name
        changes.append((row_id, part_no, view, stored, new_rel, set_no,
                        stamp_to_iso(stamp), category))

    print("행 %d개 중  정상 %d  /  고칠 것 %d  /  못 고침 %d 품번" %
          (len(rows), ok, len(changes), len(unrepairable)))
    print()

    by_part = {}
    for c in changes:
        by_part.setdefault(c[1], []).append(c)
    for part_no in sorted(by_part):
        first = by_part[part_no][0]
        print("  %-16s -> 벌 %03d  (%d장)  [%s]" %
              (part_no, first[5], len(by_part[part_no]), first[7]))
    if unrepairable:
        print()
        print("  ── 손대지 않은 품번 (재촬영 또는 확인 필요) ──")
        for part_no in sorted(unrepairable):
            print("  %-16s %s" % (part_no, unrepairable[part_no]))

    if not args.apply:
        print()
        print("미리보기입니다. 반영하려면 --apply 를 붙이십시오.")
        return

    backup = args.db + ".before-path-repair-" + \
        datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    shutil.copy2(args.db, backup)
    print()
    print("백업: " + backup)

    cur.execute("BEGIN")
    for row_id, part_no, view, old, new_rel, set_no, iso, _cat in changes:
        cur.execute(
            "UPDATE PartList_ReferenceImages "
            "SET file_path=?, set_no=?, captured_at=?, "
            "display_path=? WHERE id=?",
            (new_rel, set_no, iso,
             PREFIX + _cat + "\\" + part_no, row_id))
    con.commit()

    # 반영 결과를 스스로 확인합니다.
    cur.execute("SELECT file_path FROM PartList_ReferenceImages")
    broken_after = 0
    for (stored,) in cur.fetchall():
        if not os.path.isfile(resolve_stored(stored, args.image_root)):
            broken_after += 1
    print("반영 완료: %d행 수정, 반영 후 파일 못 찾는 행 %d (못 고침 품번 몫)" %
          (len(changes), broken_after))
    con.close()


if __name__ == "__main__":
    main()
