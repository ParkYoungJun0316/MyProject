"""t5doors.json -> t5doors.txt (Unity 에디터 빌더가 읽는 평문) + 눈으로 보는 미리보기.

`execute_code`의 C#이 JsonUtility로는 중첩 배열을 못 읽어서 평문으로 내린다
(`t5buildemit.py`와 같은 형식). 한 줄 = 한 오브젝트, 공백 구분.

  GRID <n> <pitch> <extent> <doorW> <doorT> <doorH>
  DOOR <name> <cx> <cz> <sx> <sz> <color>      1층 문 (y = doorH/2)
  PAD  <name> <cx> <cz> <color> <zone>         2층 패드 (y는 빌더 상수 = 2층 윗면)

실행:
    python t5doorsemit.py                # t5doors.json -> t5doors.txt + 미리보기
    python t5doorsemit.py --quiet        # 미리보기 없이
"""
import json
import sys

LETTER = {"Blue": "B", "Purple": "P", "Green": "G",
          "Yellow": "Y", "Black": "K", "White": "W"}


def emit(d):
    g = d["grid"]
    out = ["GRID {0} {1} {2} {3} {4} {5}".format(
        g["n"], g["pitch"], g["extent"], g["doorWidth"],
        g["doorThickness"], g["doorHeight"])]
    for name, cx, cz, sx, sz, color in d["doors"]:
        out.append("DOOR {0} {1} {2} {3} {4} {5}".format(name, cx, cz, sx, sz, color))
    for name, cx, cz, color, zone in d["pads"]:
        out.append("PAD {0} {1} {2} {3} {4}".format(name, cx, cz, color, zone))
    return out


def preview(d):
    """문 색을 격자 그림으로. 칸 사이 글자가 그 경계의 문 색이다.

      B/P/G/Y = 고유 4색 · K = 흑 · W = 백
      대문자 = 의도 경로 위의 문 · 소문자 = 그 밖의 문
      o = 의도 경로가 지나는 칸 · S/G = 시작/골
    """
    n = d["grid"]["n"]
    pitch = d["grid"]["pitch"]
    half = pitch / 2.0

    # 좌표 -> 격자 인덱스
    vert, horz = {}, {}
    for name, cx, cz, sx, sz, color in d["doors"]:
        if sx < sz:                                    # 세로문 (x 격자선 위)
            vert[(int(cx / pitch), int((cz - half) / pitch))] = color
        else:                                          # 가로문 (z 격자선 위)
            horz[(int((cx - half) / pitch), int(cz / pitch))] = color

    pedges = set()
    path = [tuple(c) for c in d["path"]]
    for k in range(len(path) - 1):
        pedges.add(tuple(sorted((path[k], path[k + 1]))))

    seg_of = {}
    for si, seg in enumerate(d["segments"]):
        cells = [tuple(c) for c in seg["cells"]]
        for c in cells:
            seg_of.setdefault(c, si)

    lines = []
    for j in range(n - 1, -1, -1):
        row = []
        for i in range(n):
            c = (i, j)
            if c == tuple(d["startCell"]):
                row.append("S")
            elif c == tuple(d["goalCell"]):
                row.append("G")
            elif c in seg_of:
                row.append("o")           # 의도 경로가 지나는 칸
            else:
                row.append(".")
            if i < n - 1:
                col = vert.get((i + 1, j))
                on = tuple(sorted((c, (i + 1, j)))) in pedges
                row.append(LETTER[col].upper() if on else LETTER[col].lower())
        lines.append(" ".join(row))
        if j > 0:
            row = []
            for i in range(n):
                col = horz.get((i, j))
                on = tuple(sorted(((i, j), (i, j - 1)))) in pedges
                row.append(LETTER[col].upper() if on else LETTER[col].lower())
                if i < n - 1:
                    row.append(" ")
            lines.append(" ".join(row))
    return lines


def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except AttributeError:
        pass

    src = "t5doors.json"
    d = json.load(open(src))
    out = emit(d)
    open("t5doors.txt", "w").write("\n".join(out) + "\n")
    print("{0} lines -> t5doors.txt  (문 {1} · 패드 {2} · seed {3})".format(
        len(out), len(d["doors"]), len(d["pads"]), d["seed"]))

    if "--quiet" not in sys.argv[1:]:
        print()
        for line in preview(d):
            print("  " + line)
        print()
        st = d["stats"]
        print("  구간 색 순서: " + " ".join(
            LETTER[seg["color"]] for seg in d["segments"]))
        print("  최소 전환 " + " · ".join(
            "{0} {1}".format(k, v[0] if v[0] == v[1] else "{0}~{1}".format(*v))
            for k, v in sorted(st["switches"].items())))
        print("  솔로(흑∪백) {0}칸 · 흑백만 쓰면 {1}전환 · 오답 가지 {2}개".format(
            st["soloPathLen"], st["bwOnlySwitches"], st["decoysPlanted"]))


if __name__ == "__main__":
    main()
