"""T.Stage4 씬 → 격자/복도 데이터 추출 (Unity 밖, 빌드 비포함).

SSOT: Assets/Docs/TStage4TrapRandomization.md

씬 YAML을 직접 읽어 판(T4_Grid10_*) 5장의 타일 격자와 MovingCorridor 파라미터를
t4boards.json으로 덤프한다. T5MazeGen의 t5verify.py가 scene_dump.txt를 요구하는 것과 달리
여기서는 에디터에서 손으로 덤프할 것이 없다 — 판이 축 정렬 격자라 좌표만 읽으면 된다.

주의: 에디터에 저장하지 않은 변경이 있으면 이 파일은 그만큼 낡는다.
      돌리기 전에 씬을 저장할 것(Ctrl+S).

사용:
    python t4scene.py                 # → t4boards.json + 요약 출력
    python t4scene.py --ascii         # 판별 ASCII 지도까지
"""
import argparse
import json
import os
import re
import sys

SCENE = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                     "..", "..", "Assets", "Scenes", "T.Stage4.unity")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "t4boards.json")

# 컴포넌트 GUID — 타일 역할 판정은 이름 접미사(_c/_b)가 아니라 이 GUID로 한다.
# 문서 §2.4 ⚠️대로 타일 이름 규약이 이미 깨져 있어서 이름을 믿을 수 없다.
GUID_BREAK_TILE = "b39dab1a35543c7419c553b1d3a3fd8e"   # BreakTile.cs
GUID_CAP_TILE = "669ae7d49efc03e41a2c2b3a43da96c2"     # CapacityTile.cs
GUID_MOVING_CORRIDOR = "953111b2c63ea084db3e27b1b521f9a9"  # MovingCorridor.cs

COLS = [-12.0, -4.0, 4.0, 12.0]   # §2.4 4열
PITCH = 8.0                       # 8m 타일


def parse_scene(path):
    text = open(path, encoding="utf-8", errors="replace").read()
    docs = re.split(r"\n--- ", text)

    names, transforms, scripts = {}, {}, {}
    corridor = None

    for doc in docs[1:]:
        head = re.match(r"!u!(\d+) &(\d+)", doc)
        if not head:
            continue
        cls, fid = head.group(1), head.group(2)
        owner = re.search(r"m_GameObject: \{fileID: (\d+)\}", doc)

        if cls == "1":  # GameObject
            name = re.search(r"m_Name: (.*)", doc)
            active = re.search(r"m_IsActive: (\d)", doc)
            names[fid] = {
                "name": name.group(1).strip() if name else "",
                "active": active.group(1) == "1" if active else True,
            }
        elif cls == "4":  # Transform
            pos = re.search(
                r"m_LocalPosition: \{x: ([-\d.e+]+), y: ([-\d.e+]+), z: ([-\d.e+]+)\}", doc)
            father = re.search(r"m_Father: \{fileID: (\d+)\}", doc)
            if owner and pos:
                transforms[fid] = {
                    "go": owner.group(1),
                    "pos": tuple(float(v) for v in pos.groups()),
                    "father": father.group(1) if father else "0",
                }
        elif cls == "114":  # MonoBehaviour
            guid = re.search(r"m_Script: \{fileID: \d+, guid: ([0-9a-f]+)", doc)
            if not (owner and guid):
                continue
            scripts.setdefault(owner.group(1), set()).add(guid.group(1))
            if guid.group(1) == GUID_MOVING_CORRIDOR:
                corridor = parse_corridor(doc)

    return names, transforms, scripts, corridor


def parse_corridor(doc):
    """MovingCorridor 인스펙터 값 — 시뮬레이터가 그대로 쓴다."""
    def one(pattern, cast=float, default=None):
        m = re.search(pattern, doc)
        return cast(m.group(1)) if m else default

    def block(field):
        m = re.search(field + r":\n((?:    .*\n)+)", doc)
        if not m:
            return None
        body = m.group(1)
        speeds = [float(v) for v in re.findall(r"^    - ([-\d.e+]+)$", body, re.M)]
        return {
            "enabled": (re.search(r"enabled: (\d)", body).group(1) == "1"
                        if re.search(r"enabled: (\d)", body) else False),
            "minInterval": float(re.search(r"minInterval: ([-\d.e+]+)", body).group(1)),
            "maxInterval": float(re.search(r"maxInterval: ([-\d.e+]+)", body).group(1)),
            "speeds": speeds,
        }

    return {
        "baseSpeed": one(r"baseSpeed: ([-\d.e+]+)"),
        "minWallDistance": one(r"minWallDistance: ([-\d.e+]+)"),
        "maxWallDistance": one(r"maxWallDistance: ([-\d.e+]+)"),
        "back": block("  backRandomSpeed"),
        "front": block("  frontRandomSpeed"),
    }


def build_boards(names, transforms, scripts):
    by_go = {t["go"]: (fid, t) for fid, t in transforms.items()}
    boards = []

    for go_id, info in names.items():
        if not info["name"].startswith("T4_Grid10"):
            continue
        tr_id = by_go[go_id][0]
        cells, offgrid = {}, []

        for t in transforms.values():
            if t["father"] != tr_id:
                continue
            comps = scripts.get(t["go"], set())
            if GUID_BREAK_TILE in comps:
                role = "b"
            elif GUID_CAP_TILE in comps:
                role = "c"
            else:
                continue  # 타일이 아닌 자식(Lump 등)
            x, _, z = t["pos"]
            if x not in COLS or abs(z / PITCH - round(z / PITCH)) > 1e-6:
                offgrid.append([names[t["go"]]["name"], x, z, role])
                continue
            key = "%d,%d" % (int(round(z / PITCH)), COLS.index(x))
            if key in cells:      # 같은 칸에 두 장 — 배치 사고
                offgrid.append([names[t["go"]]["name"], x, z, "dup:" + role])
            else:
                cells[key] = role

        non_tiles = [names[t["go"]]["name"] for t in transforms.values()
                     if t["father"] == tr_id and not (scripts.get(t["go"], set())
                                                      & {GUID_BREAK_TILE, GUID_CAP_TILE})]
        boards.append({
            "name": info["name"],
            "activeInScene": info["active"],
            "cells": cells,
            "offgrid": offgrid,
            "nonTileChildren": non_tiles,
        })

    boards.sort(key=lambda b: b["name"])
    return boards


def ascii_map(board):
    cells = board["cells"]
    rows = sorted({int(k.split(",")[0]) for k in cells})
    out = ["   z  | " + " ".join("%5d" % int(x) for x in COLS)]
    for r in range(max(rows), min(rows) - 1, -1):
        line = " ".join("   %s " % cells.get("%d,%d" % (r, c), ".") for c in range(4))
        out.append("%5d |%s" % (r * PITCH, line))
    return "\n".join(out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ascii", action="store_true")
    args = ap.parse_args()

    names, transforms, scripts, corridor = parse_scene(SCENE)
    boards = build_boards(names, transforms, scripts)
    data = {"scene": "Assets/Scenes/T.Stage4.unity", "cols": COLS, "pitch": PITCH,
            "corridor": corridor, "boards": boards}
    json.dump(data, open(OUT, "w", encoding="utf-8"), indent=1)

    print("corridor:", json.dumps(corridor, ensure_ascii=False))
    print()
    print("%-20s %6s %5s %5s %5s  %s" % ("판", "칸", "_c", "_b", "구멍", "씬활성"))
    for b in boards:
        cells = b["cells"]
        nb = sum(1 for v in cells.values() if v == "b")
        rows = sorted({int(k.split(",")[0]) for k in cells})
        holes = (max(rows) - min(rows) + 1) * 4 - len(cells)
        print("%-20s %6d %5d %5d %5d  %s%s" % (
            b["name"], len(cells), len(cells) - nb, nb, holes,
            "O" if b["activeInScene"] else "-",
            ("  ⚠ 비격자/중복 %d" % len(b["offgrid"])) if b["offgrid"] else ""))
        if args.ascii:
            print(ascii_map(b))
            print()
    print("\n→", OUT)


if __name__ == "__main__":
    sys.exit(main())
