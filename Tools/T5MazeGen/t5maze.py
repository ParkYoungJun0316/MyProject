"""T.Stage5 미로 생성기 — **문 없는 버전** (2026-09-19 재제작).

기존 `t5gen.py`는 미로 + 색 문까지 같이 뽑는 버전이다. 이 스크립트는 **미로만** 만든다.
문은 사용자가 씬에서 직접 배치하고, 배치가 끝나면 `t5gen.py`의 `min_switches()`로 검증한다.
그래서 `t5gen.py`는 지우지 않는다 — 검증기로 계속 쓴다.

[좌표계 — 2026-09-19 변경]
  미로는 **0..120**에 놓는다(구 버전은 -50..+50, 그 다음 0..100). 칸 피치 10 = 통로 8 + 벽 2.
  `r -> z`, `c -> x`. 칸 중심 = (S*(c+0.5), S*(r+0.5)).
  start = 좌하단 칸 (0,0) = 중심 **(5, 5)** / goal = 우상단 칸 (11,11) = 중심 **(115, 115)**.
  격자선(벽·문이 서는 줄) 0,10,…,120 · 칸 중심 5,15,…,115 — **전부 정수**다.

[시작 홀이 미로 (0,0) 칸과 겹친다]
  시작 홀은 원점(±5)에 있고 미로는 0부터 시작하므로 0..5 구간이 겹친다. 그래서 **(0,0) 칸의
  남쪽·서쪽 외벽을 뚫어** 홀이 미로 첫 칸과 그대로 이어지게 한다. 겹침을 없애려고 좌표를
  옮기면 §1.2가 못박은 스폰 4자리 (0,0,±5)/(±5,0,0)가 따라 움직여야 한다.

[문이 빠지면서 사라진 합격 기준을 무엇으로 대체했나]
  구 버전의 합격 조건 7개 중 5개가 문 기반이었다(경로 위 문 수·연속 색 다름·인원별 전수 BFS·
  루프 우회 불가). 남는 것이 "경로 길이" 하나뿐이라 7장 난이도를 맞출 수단이 없어진다. 그래서:

    · 정답 경로 52~64칸        — 구 버전과 같은 값. 맵 간 난이도 정렬의 기준축
    · 막다른 길 10~14개        — §3.1 표에서 이미 쓰던 값
    · 루프 10~14개             — 0이면(완전 트리) 막다른 길에서 체이서에 갇혀 즉사한다.
                                 문 우회 방지 제약이 사라졌으므로 이제 자유롭게 정할 수 있다
    · 오답 가지 최대 깊이 >= 6 — 잘못 들어가면 6칸 이상 되돌아 나와야 한다. 이게 0에 가까우면
                                 러너가 아무렇게나 달려도 되고 **안내자가 할 일이 없어진다**
    · 오답 가지 평균 깊이 >= 2

[체이서 스폰 4점 — 씬 루트라 7장 공통]
  §2대로 스폰 4점은 맵이 아니라 씬 루트에 있다. 그러면 **같은 4칸이 7장 전부에서** 통행
  가능해야 한다. 칸 중앙이라 벽에 박힐 일은 없지만 **막다른 길이면 한 방향으로만 나올 수 있어**
  체이서가 초반에 갇힌다. 그래서 4칸 전부 `degree >= 2`를 생성 조건으로 걸고, 하나라도
  막다른 길이면 그 시드를 버린다.

실행:
    python t5maze.py t5maze.json
"""
import collections
import json
import random
import sys

N = 12
S = 10.0        # 칸 피치 = 통로 8 + 벽 두께 2. 미로 0..120

# 2026-09-19: 100/12 = 8.3333을 버리고 **정수 격자**로 갔다.
# 문을 사람이 직접 놓기로 했는데(§3.0) 문 자리가 (격자선, 칸 중심)이라 두 축이 다 정수여야 한다.
# 칸 중심이 정수가 되려면 피치가 **짝수**여야 하고, 통로 8보다 큰 최소 짝수 피치가 10이다.
#   격자선 0,10,20…120 / 칸 중심 5,15,25…115 — 둘 다 정수.
# 벽이 0.5 -> 2로 두꺼워지지만 통로 폭 8은 그대로다(체이서 정면 회피 불가 유지).
WALL_T = 2.0
WALL_H = 15.0
EXTENT = S * N  # 120

START = (0, 0)          # 좌하단
GOAL = (N - 1, N - 1)   # 우상단

# 동서남북 각 변의 중앙 칸. 씬 루트에 두는 체이서 스폰 4점이라 7장 공통이다.
SPAWN_CELLS = {
    "S": (0, 5),
    "W": (5, 0),
    "N": (N - 1, 5),
    "E": (5, N - 1),
}

PATH_MIN, PATH_MAX = 52, 64
DEAD_MIN, DEAD_MAX = 10, 14
LOOP_MIN, LOOP_MAX = 10, 14
OFF_MAX_MIN = 6      # 오답 가지 최대 깊이 하한
OFF_AVG_MIN = 2.0    # 오답 가지 평균 깊이 하한


def nbrs(r, c):
    for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        rr, cc = r + dr, c + dc
        if 0 <= rr < N and 0 <= cc < N:
            yield (rr, cc)


def ekey(a, b):
    return (a, b) if a < b else (b, a)


ALL_EDGES = set()
for _r in range(N):
    for _c in range(N):
        for _n in nbrs(_r, _c):
            ALL_EDGES.add(ekey((_r, _c), _n))


def spanning_tree(rng):
    """재귀 백트래커 — 긴 복도가 나온다(Kruskal/Prim보다 정답 경로가 길다)."""
    start = (rng.randrange(N), rng.randrange(N))
    seen = {start}
    stack = [start]
    tree = set()
    while stack:
        cur = stack[-1]
        opts = [n for n in nbrs(*cur) if n not in seen]
        if not opts:
            stack.pop()
            continue
        n = rng.choice(opts)
        seen.add(n)
        tree.add(ekey(cur, n))
        stack.append(n)
    return tree


def adj_of(open_edges):
    adj = collections.defaultdict(list)
    for a, b in open_edges:
        adj[a].append(b)
        adj[b].append(a)
    return adj


def shortest_path(adj, s, g):
    prev = {s: None}
    q = collections.deque([s])
    while q:
        u = q.popleft()
        if u == g:
            break
        for v in adj[u]:
            if v not in prev:
                prev[v] = u
                q.append(v)
    if g not in prev:
        return None
    path = []
    u = g
    while u is not None:
        path.append(u)
        u = prev[u]
    return path[::-1]


def off_path_depth(adj, path):
    """정답 경로에서 갈라진 칸들이 경로로부터 얼마나 깊은가 — 안내자의 존재 가치 지표."""
    dist = {p: 0 for p in path}
    q = collections.deque(path)
    while q:
        u = q.popleft()
        for v in adj[u]:
            if v not in dist:
                dist[v] = dist[u] + 1
                q.append(v)
    off = [d for cell, d in dist.items() if d > 0]
    if not off:
        return 0, 0.0
    return max(off), sum(off) / len(off)


def build(seed):
    rng = random.Random(seed)

    tree = spanning_tree(rng)
    open_edges = set(tree)

    # braid — 루프를 넣는다. 구 버전은 문 우회를 막으려 "같은 region 안에서만" 넣었지만
    # 문이 없어졌으므로 그 제약이 사라졌다. 대신 루프가 경로를 질러버릴 수 있으니
    # **루프를 넣은 뒤의 최단 경로**로 길이를 재검사한다(아래).
    non_tree = [e for e in ALL_EDGES if e not in tree]
    rng.shuffle(non_tree)
    n_loops = rng.randint(LOOP_MIN, LOOP_MAX)
    for e in non_tree[:n_loops]:
        open_edges.add(e)

    adj = adj_of(open_edges)

    path = shortest_path(adj, START, GOAL)
    if path is None or not (PATH_MIN <= len(path) <= PATH_MAX):
        return None

    dead_ends = [(r, c) for r in range(N) for c in range(N) if len(adj[(r, c)]) == 1]
    if not (DEAD_MIN <= len(dead_ends) <= DEAD_MAX):
        return None

    # 체이서 스폰 4칸이 막다른 길이면 탈락 — 씬 루트라 7장 전부에서 성립해야 한다.
    if any(len(adj[cell]) < 2 for cell in SPAWN_CELLS.values()):
        return None
    # 스폰이 start/goal 칸과 겹치면 안 된다
    if any(cell in (START, GOAL) for cell in SPAWN_CELLS.values()):
        return None

    off_max, off_avg = off_path_depth(adj, path)
    if off_max < OFF_MAX_MIN or off_avg < OFF_AVG_MIN:
        return None

    walls = sorted(e for e in ALL_EDGES if e not in open_edges)

    return {
        "seed": seed,
        "start": list(START),
        "goal": list(GOAL),
        "walls": [[list(a), list(b)] for a, b in walls],
        "stats": {
            "pathLen": len(path),
            "deadEnds": len(dead_ends),
            "loops": n_loops,
            "offMaxDepth": off_max,
            "offAvgDepth": round(off_avg, 2),
        },
    }


# ── 씬 배치용 좌표로 변환 ────────────────────────────────────────────

def runs(idx):
    """연속한 인덱스를 구간으로 묶는다 — 벽 오브젝트 수를 줄이는 유일한 수단이다."""
    idx = sorted(idx)
    res = []
    for i in idx:
        if res and res[-1][1] == i - 1:
            res[-1][1] = i
        else:
            res.append([i, i])
    return res


def emit_walls(m):
    """격자 벽 -> 병합된 박스 [name, cx, cz, sx, sz]. 외벽 포함, 시작 홀 쪽만 뚫는다."""
    V = {k: set() for k in range(N + 1)}   # x = S*k 인 세로벽, 값은 행(r) 집합
    H = {k: set() for k in range(N + 1)}   # z = S*k 인 가로벽, 값은 열(c) 집합

    # 외벽. (0,0) 칸의 남(z=0, c=0)·서(x=0, r=0)는 비운다 — 시작 홀이 거기로 이어진다.
    for r in range(N):
        if r != 0:
            V[0].add(r)
        V[N].add(r)
    for c in range(N):
        if c != 0:
            H[0].add(c)
        H[N].add(c)

    for a, b in m["walls"]:
        (r1, c1), (r2, c2) = tuple(a), tuple(b)
        if r1 == r2:
            V[max(c1, c2)].add(r1)
        else:
            H[max(r1, r2)].add(c1)

    out = []
    for k, rows in V.items():
        for r0, r1 in runs(rows):
            length = S * (r1 - r0 + 1) + WALL_T
            out.append([f"WallV_{k:02d}_{r0:02d}",
                        round(S * k, 3), round(S * (r0 + r1 + 1) / 2, 3),
                        WALL_T, round(length, 3)])
    for k, cols in H.items():
        for c0, c1 in runs(cols):
            length = S * (c1 - c0 + 1) + WALL_T
            out.append([f"WallH_{k:02d}_{c0:02d}",
                        round(S * (c0 + c1 + 1) / 2, 3), round(S * k, 3),
                        round(length, 3), WALL_T])
    return out


def cell_center(cell):
    r, c = cell
    return [round(S * (c + 0.5), 3), round(S * (r + 0.5), 3)]


def main():
    out_path = sys.argv[1] if len(sys.argv) > 1 else "t5maze.json"

    maps = []
    seed = 5000
    tried = 0
    while len(maps) < 7:
        seed += 1
        tried += 1
        if tried > 200000:
            print("FAILED: 조건을 만족하는 시드를 못 찾았다. 기준을 완화할 것.")
            return
        m = build(seed)
        if m:
            m["wallBoxes"] = emit_walls(m)
            maps.append(m)

    for i, m in enumerate(maps):
        st = m["stats"]
        print(f"Map_{i+1:02d} seed={m['seed']:>6} path={st['pathLen']:>2}칸 "
              f"deadEnds={st['deadEnds']:>2} loops={st['loops']:>2} "
              f"offMax={st['offMaxDepth']:>2} offAvg={st['offAvgDepth']:>4} "
              f"walls={len(m['wallBoxes']):>3}개")

    data = {
        "grid": {"n": N, "cell": round(S, 4), "extent": EXTENT,
                 "wallHeight": WALL_H, "wallThickness": WALL_T},
        "startCell": list(START), "goalCell": list(GOAL),
        "startCenter": cell_center(START), "goalCenter": cell_center(GOAL),
        "chaserSpawns": {k: cell_center(v) for k, v in SPAWN_CELLS.items()},
        "maps": maps,
    }
    json.dump(data, open(out_path, "w"), indent=1)
    print(f"\nstart={data['startCenter']} goal={data['goalCenter']}")
    print("chaserSpawns=" + ", ".join(f"{k}{v}" for k, v in data["chaserSpawns"].items()))
    print(f"-> {out_path} ({tried} seeds tried)")


if __name__ == "__main__":
    main()
