"""T.Stage5 문 색 배치 생성기 (G단계).

`Assets/Docs/TStage5RunnerRedesign.md` §3이 SSOT.

10x10 격자의 **내부 경계 180개가 전부 문**이고, 이 스크립트는 그 180개에 색을 칠한다.
벽 미로 생성기(t5maze.py)는 폐기됐다 — 여기서 만드는 것은 지형이 아니라 **색 배치**다.

파이프라인
  1. 의도 경로(27/29칸) + 흑백 안전망 경로(41~45칸)를 **변이 안 겹치게** 깐다
  2. 의도 경로를 13~14구간으로 잘라 색을 배정 (연속 구간은 다른 색, 고유 4색 각 2회 이상)
  3. 남은 변을 **같은 색이 이어지지 않게**(매칭) 채운다
  4. **지름길 보정** — 하한 미달이면 제일 싼 길의 색 사슬을 끊는다
  5. **오답 가지**를 심고, 하나 심을 때마다 하한을 다시 본다 (깨지면 그 가지만 롤백)
  6. `t5gen.min_switches()`로 인원별 전수 검증

만들면서 실측으로 드러난 것 셋 — 전부 해당 함수 주석에 근거를 박아 뒀다
  * 격자 기하가 하한을 정한다. (0,0)→(9,9)는 최소 18수이고 한 색으로 L칸을 달릴 수 있으면
    전환은 대략 18/L이다. 2칸짜리 사슬이 깔려 있으면 **무엇을 그리든 9~10전환 길이 생긴다**
    → 시드 뽑기로는 12를 못 넘는다. 그래서 `repair()`로 고친다.
  * **복도는 양방향이다.** 오답 가지를 목표 반대로 뻗어도 러너가 먼 끝에서 뿌리로 타면
    그게 곧 전진이다 → 뿌리 반경 안에 똬리를 틀게 묶는다(`grow_decoy`).
  * 최소 전환은 **구간 수 S를 넘을 수 없다.** 의도 경로가 언제나 S짜리 길이기 때문이다.

실행:
    python t5doors.py              # 시드 탐색 → t5doors.json
    python t5doors.py --seed 7042  # 특정 시드
"""
import collections
import json
import random
import sys

from t5gen import min_switches  # §3.3 검증기 — 재사용

# ── 격자 (§1.4 좌표계) ────────────────────────────────────────
N      = 10       # 칸 수
PITCH  = 12.0     # 통로 10 + 문 두께 2
EXTENT = 120.0    # N * PITCH
HALF   = 6.0      # 칸 중심 = 6, 18, ... , 114
WIDTH  = 10.0     # 문 폭 = 통로 폭
THICK  = 2.0      # 문 두께
HEIGHT = 15.0     # 문 높이

START = (0, 0)
GOAL  = (N - 1, N - 1)

UNIQUE = ["Blue", "Purple", "Green", "Yellow"]
BW     = ["Black", "White"]
COLORS = UNIQUE + BW

# ── 합격 기준 (§3.3) ──────────────────────────────────────────
MIN_SW_4P      = 12          # 4인 — 12 이상 (상한 없음)
MIN_SW_3P      = 10
MIN_SW_2P      = 8
PATH_TARGETS   = (27, 29)    # 의도 경로 칸 수 — 격자가 이분그래프라 홀수만 가능
BWPATH_TARGETS = (41, 43, 45)
SEGMENTS       = (14, 15, 16)  # 보정이 올릴 수 있는 천장이 곧 S다(repair 주석)
BW_LEN         = (25, 45)    # 솔로(흑·백 동시 개방) 최단 경로 칸 수 = 솔로 주행 거리
BW_ONLY_SW     = 20          # 흑백만 고집할 때의 전환 — 이만큼 비싸야 고유색을 쓴다
BW_RUN         = (1, 2)      # 흑↔백을 몇 변마다 뒤집는가
DECOY_DEPTH    = 4           # 오답 가지 깊이(칸). 왕복 8칸 = 96m = 약 10초 손해
                             # ⚠️ 문서 초안은 6이었는데 기하가 안 받아준다 — 지름길이 안 되게
                             # 뿌리 반경 3 안에 가두면(grow_decoy) 6칸짜리는 1231번 시도에 23번만
                             # 자라고 그중 6번만 하한을 안 깨뜨린다(실측). 4면 통과율이 쓸 만해진다
DECOY_COUNT    = 6           # 오답 가지 개수
COLOR_SPREAD   = (18, 45)    # 색당 문 개수 허용 범위

# 없는 색 문을 어떻게 다룰 것인가 — 이 스테이지의 성립 여부가 여기 달려 있다.
#   True  : **벽**(영영 안 열림). 인원이 줄면 격자가 촘촘해진다
#   False : 구 규칙대로 **Common**(누구나 엶) — 2인이면 문 180개 중 89개가 한 묶음이 되고
#           그 묶음의 최대 성분이 24칸이라 **전환 5회에 골인**한다(실측). 격자가 사라진다
MISSING_IS_WALL = True


def nbrs(cell):
    i, j = cell
    for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        a, b = i + di, j + dj
        if 0 <= a < N and 0 <= b < N:
            yield (a, b)


def ekey(a, b):
    return (a, b) if a < b else (b, a)


ALL_EDGES = set()
for _i in range(N):
    for _j in range(N):
        for _n in nbrs((_i, _j)):
            ALL_EDGES.add(ekey((_i, _j), _n))
assert len(ALL_EDGES) == 2 * N * (N - 1) == 180


def edge_nbrs(e):
    """변 e와 칸을 공유하는 변들. 같은 색이 **이어지는지**를 보는 단위다."""
    for cell in e:
        for n in nbrs(cell):
            f = ekey(cell, n)
            if f != e:
                yield f


def edges_of(path):
    return [ekey(path[k], path[k + 1]) for k in range(len(path) - 1)]


def center(i):
    return HALF + PITCH * i


# ── 1. 경로 ───────────────────────────────────────────────────

def random_path(rng, target, blocked=frozenset(), start=START, goal=GOAL, tries=200):
    """start→goal 자기회피 경로를 **정확히 target칸**으로 만든다.

    "길이가 맞는 경로"를 DFS로 찾으면 지수 폭발한다(목표에 일찍 닿은 경로를 전부 버린다).
    그래서 **막힌 변을 피한 랜덤 최단경로**를 깔고, 거기에 우회(bump)를 끼워 넣어 늘린다 —
    한 번에 2칸씩 늘어나 목표 길이에 정확히 맞는다.

    ⚠️ 격자가 이분그래프라 (0,0)→(9,9) 경로의 **칸 수는 언제나 홀수**다."""
    if (target - (abs(goal[0] - start[0]) + abs(goal[1] - start[1])) - 1) % 2:
        return None

    for _ in range(tries):
        prev = {start: None}
        q = collections.deque([start])
        while q:
            u = q.popleft()
            if u == goal:
                break
            opts = [v for v in nbrs(u) if v not in prev and ekey(u, v) not in blocked]
            rng.shuffle(opts)
            for v in opts:
                prev[v] = u
                q.append(v)
        if goal not in prev:
            return None                     # 막힌 변이 갈라놨다 — 이 조합은 포기
        path = []
        u = goal
        while u is not None:
            path.append(u)
            u = prev[u]
        path.reverse()
        if len(path) > target:
            continue

        occupied = set(path)
        stall = 0
        while len(path) < target and stall < 400:
            stall += 1
            k = rng.randrange(len(path) - 1)
            u, v = path[k], path[k + 1]
            d = (v[0] - u[0], v[1] - u[1])
            perp = rng.choice([(d[1], d[0]), (-d[1], -d[0])])
            a = (u[0] + perp[0], u[1] + perp[1])
            b = (v[0] + perp[0], v[1] + perp[1])
            if not (0 <= a[0] < N and 0 <= a[1] < N and 0 <= b[0] < N and 0 <= b[1] < N):
                continue
            if a in occupied or b in occupied:
                continue
            if any(e in blocked for e in (ekey(u, a), ekey(a, b), ekey(b, v))):
                continue
            path[k + 1:k + 1] = [a, b]
            occupied.update((a, b))
            stall = 0
        if len(path) == target:
            return path
    return None


# ── 2. 구간 색 ────────────────────────────────────────────────

def cut(rng, n_edges, n_seg):
    """n_edges개를 n_seg구간으로 자른다. 각 구간 1개 이상."""
    if n_seg > n_edges:
        return None
    cuts = sorted(rng.sample(range(1, n_edges), n_seg - 1))
    sizes = []
    prev = 0
    for c in cuts + [n_edges]:
        sizes.append(c - prev)
        prev = c
    return sizes


def switches_of(seq, cmap):
    """색 시퀀스를 cmap으로 접었을 때의 전환 횟수.
    첫 구간을 열어주는 것도 1회다(시작은 아무 색도 안 열려 있다)."""
    folded = [cmap[c] for c in seq]
    return 1 + sum(1 for k in range(len(folded) - 1) if folded[k] != folded[k + 1])


def color_sequence(rng, n_seg, parts):
    """구간 색 시퀀스.

    색 **구성을 먼저 고정**한다 — 고유 4색 각각 2회(§3.3-7) + 나머지를 흑·백에.
    그 다음 연속 구간이 서로 다르게 섞고, 인원 매핑별 전환 하한을 선검사한다
    (진짜 판정은 min_switches가 한다)."""
    bag = [u for u in UNIQUE for _ in range(2)]
    left = n_seg - len(bag)
    if left < 2:
        return None
    bag += ["Black"] * (left - left // 2) + ["White"] * (left // 2)

    for _ in range(600):
        seq = bag[:]
        rng.shuffle(seq)
        if any(seq[k] == seq[k + 1] for k in range(len(seq) - 1)):
            continue
        if all(switches_of(seq, cmap) >= floor for _l, cmap, floor, _w in parts):
            return seq
    return None


# ── 3. 인원별 색 매핑 (§3.3) ──────────────────────────────────

def partitions():
    """(label, cmap, 전환 하한, 벽이 된 색). cmap은 문 색 → 런타임 그룹.

    러너 색은 Common으로 떨어진다(러너는 1층이라 자기 패드를 못 밟는다).
    없는 색은 `MISSING_IS_WALL`에 따라 벽이 되거나 Common으로 합쳐진다.
    색 이름 자체는 전환 수에 영향이 없으므로(그룹 구조만 본다),
    **몇 개가 벽이 되는가**만 경우의 수로 돌린다."""
    out = []

    def make(walled, label, floor):
        cmap = {c: c for c in COLORS}
        cmap[UNIQUE[0]] = "Common"                 # 러너 색
        for w in walled:
            cmap[w] = "Wall" if MISSING_IS_WALL else "Common"
        return (label, cmap, floor, set(walled) if MISSING_IS_WALL else set())

    out.append(make((), "4p", MIN_SW_4P))
    for w in UNIQUE[1:]:                           # 3인 — 없는 색 1개
        out.append(make((w,), "3p", MIN_SW_3P))
    for a in range(1, len(UNIQUE)):                # 2인 — 없는 색 2개
        for b in range(a + 1, len(UNIQUE)):
            out.append(make((UNIQUE[a], UNIQUE[b]), "2p", MIN_SW_2P))
    return out


PARTS = partitions()


def passable(doors, walled):
    """벽이 된 색의 변을 뺀 통행 가능 집합.

    이걸 `min_switches`의 open_edges로 넘기면 벽은 인접에서 아예 빠진다 —
    검증기를 새로 만들지 않고 벽을 표현하는 방법이다."""
    if not walled:
        return ALL_EDGES
    return {e for e in ALL_EDGES if doors[e] not in walled}


def floors_ok(doors):
    for _l, cmap, floor, walled in PARTS:
        v = min_switches(passable(doors, walled), doors, cmap, START, GOAL)
        if v is None or v < floor:
            return False
    return True


# ── 4. 솔로 = 흑 ∪ 백 (§1.6) ──────────────────────────────────

def bw_path_len(doors):
    """흑·백 문만 통로일 때의 start→goal 최단 칸 수. 못 가면 None.

    솔로는 흑·백이 **동시에** 열리고, 다인승이 흑↔백만 번갈아 밟을 때의 도달 범위도
    같은 집합이다 — 그래서 이 한 번의 BFS가 둘 다 검증한다."""
    adj = collections.defaultdict(list)
    for e, c in doors.items():
        if c in BW:
            adj[e[0]].append(e[1])
            adj[e[1]].append(e[0])
    prev = {START: None}
    q = collections.deque([START])
    while q:
        u = q.popleft()
        if u == GOAL:
            n = 0
            while u is not None:
                n += 1
                u = prev[u]
            return n
        for v in adj[u]:
            if v not in prev:
                prev[v] = u
                q.append(v)
    return None


def bw_only_switches(doors):
    """다인승이 **흑·백만** 밟아서 갈 때의 최소 전환.

    흑백은 누구나 밟을 수 있는 안전망이라 언제나 성립한다(§1.6). 이 값이 낮으면
    4인에서도 흑백만 번갈아 밟는 게 정답이 되고 고유색을 쥔 안내자 셋이 논다."""
    cmap = {c: (c if c in BW else "Wall") for c in COLORS}
    return min_switches({e for e in ALL_EDGES if doors[e] in BW},
                        doors, cmap, START, GOAL)


# ── 5. 오답 가지 (§3.3-8) ─────────────────────────────────────

def grow_decoy(rng, doors, color, root, on_path, protected, depth, radius=3):
    """root에서 경로 밖으로 color 복도를 depth칸 **되칠한다.**
    바꾼 (변, 원래색) 목록을 반환 — 검증에 걸리면 그대로 되돌린다.

    ⚠️ **복도는 양방향이다.** 그냥 뻗으면 그 6칸이 한 색으로 내리 달리는 고속도로가 되어
    의도 경로를 통째로 건너뛴다(실측: 전환 5회짜리 지름길이 나왔다). 목표에서 멀어지게만
    뻗어도 소용없다 — 러너가 먼 끝에서 뿌리 쪽으로 타면 그게 곧 전진이다.
    그래서 두 겹으로 묶는다: 뿌리 반경 `radius` 안에 **똬리를 틀게** 하고,
    어느 칸도 뿌리보다 목표에 **가까워지지 않게** 한다 → 진행 이득이 0이 된다."""
    def to_goal(c):
        return abs(GOAL[0] - c[0]) + abs(GOAL[1] - c[1])

    cur = root
    root_d = to_goal(root)
    changed = []
    visited = {root}
    for _ in range(depth):
        opts = [n for n in nbrs(cur)
                if n not in visited and n not in on_path
                and ekey(cur, n) not in protected
                and abs(n[0] - root[0]) + abs(n[1] - root[1]) <= radius
                and to_goal(n) >= root_d]
        if not opts:
            break
        rng.shuffle(opts)
        nxt = opts[0]
        e = ekey(cur, nxt)
        changed.append((e, doors[e]))
        doors[e] = color
        visited.add(nxt)
        cur = nxt
    return changed


def decoy_depths(doors, path, seg_color):
    """구간 색이 열려 있는 동안 경로 밖으로 몇 칸까지 끌려 들어가는가 (통계용)."""
    on_path = set(path)
    out = []
    for color in seg_color:
        adj = collections.defaultdict(list)
        for e, c in doors.items():
            if c == color:
                adj[e[0]].append(e[1])
                adj[e[1]].append(e[0])
        best = 0
        for root in path:
            dist = {root: 0}
            q = collections.deque([root])
            while q:
                u = q.popleft()
                for v in adj[u]:
                    if v in dist:
                        continue
                    dist[v] = dist[u] + 1
                    q.append(v)
                    if v not in on_path:
                        best = max(best, dist[v])
        out.append(best)
    return out


# ── 6. 지름길 보정 ────────────────────────────────────────────

def cheapest_route(doors, cmap, pass_edges):
    """`min_switches`와 같은 0-1 BFS인데 **경로까지 복원**한다.

    합격/불합격은 끝까지 `t5gen.min_switches()`가 판정한다. 이건 보정용이다 —
    "지금 제일 싼 길이 어디로 가는가"를 알아야 그 길을 끊을 수 있다."""
    adj = collections.defaultdict(list)
    for a, b in pass_edges:
        adj[a].append(b)
        adj[b].append(a)
    groups = sorted(g for g in set(cmap.values()) if g != "Wall")
    INF = 10 ** 9
    dist = {(START, None): 0}
    par = {(START, None): None}
    dq = collections.deque([(0, START, None)])
    while dq:
        d, u, oc = dq.popleft()
        if dist.get((u, oc), INF) < d:
            continue
        if u == GOAL:
            out = []
            st = (u, oc)
            while st is not None:
                out.append(st)
                st = par[st]
            return d, out[::-1]
        for nc in groups:
            if nc != oc and dist.get((u, nc), INF) > d + 1:
                dist[(u, nc)] = d + 1
                par[(u, nc)] = (u, oc)
                dq.append((d + 1, u, nc))
        for v in adj[u]:
            if cmap[doors[ekey(u, v)]] != oc:
                continue
            if dist.get((v, oc), INF) > d:
                dist[(v, oc)] = d
                par[(v, oc)] = (u, oc)
                dq.appendleft((d, v, oc))
    return None, None


def repair(rng, doors, path_edges, bw_edges, rounds=600):
    """모든 인원 매핑이 하한을 넘을 때까지 **제일 싼 길의 색 사슬을 끊는다.**

    왜 뽑기(rejection sampling)로는 안 되는가 — 격자 기하가 하한을 정한다.
    최소 18수이고 한 색으로 L칸을 달릴 수 있으면 전환은 대략 18/L이라,
    2칸짜리 사슬이 깔려 있으면 **무엇을 그리든 9~10전환 길이 생긴다.** 시드를 아무리
    바꿔도 12가 안 나온다. 그래서 뽑지 말고 고친다.

    끊는 방법은 변의 종류마다 다르다:
      - **일반 변**      : 이웃이 안 쓰는 색으로 바꾼다
      - **흑백 경로 변** : **흑↔백으로 뒤집는다.** 색은 바뀌어도 흑 ∪ 백 집합은 그대로라
                           솔로 연결성(§1.6)이 안 깨진다 — 이 보정의 핵심 장치다
      - **의도 경로 변** : 건드리지 않는다 (구간 구조가 곧 설계다)

    ⚠️ 올릴 수 있는 천장은 **구간 수 S**다. 의도 경로가 언제나 S짜리 길이라
       그 이상으로는 못 올라간다."""
    for _ in range(rounds):
        worst = None
        for _l, cmap, floor, walled in PARTS:
            d, rt = cheapest_route(doors, cmap, passable(doors, walled))
            if d is None:
                return False
            if d < floor and (worst is None or d - floor < worst[0]):
                worst = (d - floor, rt)
        if worst is None:
            return True

        runs = []
        cur = []
        for k in range(1, len(worst[1])):
            (cu, _), (cv, _) = worst[1][k - 1], worst[1][k]
            if cu == cv:                       # 전환 지점 — 사슬이 끊긴다
                if len(cur) >= 2:
                    runs.append(cur)
                cur = []
            else:
                cur.append(ekey(cu, cv))
        if len(cur) >= 2:
            runs.append(cur)

        done = False
        for run in sorted(runs, key=len, reverse=True):
            free = [e for e in run if e not in path_edges and e not in bw_edges]
            if free:
                e = free[len(free) // 2]
                old = doors[e]
                used = {doors[f] for f in edge_nbrs(e)}
                cand = ([c for c in COLORS if c != old and c not in used]
                        or [c for c in COLORS if c != old])
                doors[e] = rng.choice(cand)
                done = True
                break
            flip = [e for e in run if e in bw_edges]
            if flip:
                e = flip[len(flip) // 2]
                doors[e] = "White" if doors[e] == "Black" else "Black"
                done = True
                break
        if not done:
            return False                       # 의도 경로만으로 된 사슬 — 이 시드는 포기
    return False


# ── 7. 한 판 만들기 ───────────────────────────────────────────

LAST_FAIL = None


def _fail(code):
    """build()가 어디서 떨어졌는지 — 진단용. 항상 None을 돌려준다."""
    global LAST_FAIL
    LAST_FAIL = code
    return None


def build(seed):
    """한 판. 실패하면 None (시드를 바꿔 다시 부른다)."""
    rng = random.Random(seed)

    # (1) 두 경로를 변이 안 겹치게. ⚠️ 순서가 반대면 안 된다 — 긴 흑백 경로를 먼저 깔면
    #     그 변들이 시작 코너((0,0)은 변이 2개뿐)를 가둬 의도 경로가 아예 안 나온다.
    path = bw = None
    for _ in range(12):
        path = random_path(rng, rng.choice(PATH_TARGETS))
        if path is None:
            continue
        bw = random_path(rng, rng.choice(BWPATH_TARGETS), blocked=set(edges_of(path)))
        if bw is not None:
            break
        bw = None
    if path is None or bw is None:
        return _fail(1)

    # 흑↔백을 1~2변마다 뒤집는다 — 짧게 끊어야 "흑백만 쓰면 비싸다"가 성립한다(§1.6)
    doors = {}
    cur_bw = rng.choice(BW)
    run = 0
    for e in edges_of(bw):
        doors[e] = cur_bw
        run += 1
        if run >= rng.randint(*BW_RUN):
            cur_bw = "White" if cur_bw == "Black" else "Black"
            run = 0

    # (2) 의도 경로를 구간으로 자르고 색을 준다
    pedges = edges_of(path)
    on_path = set(path)
    n_seg = rng.choice(SEGMENTS)
    sizes = cut(rng, len(pedges), n_seg)
    if sizes is None:
        return _fail(2)
    seq = color_sequence(rng, n_seg, PARTS)
    if seq is None:
        return _fail(3)

    seg_color = []
    seg_cells = []
    idx = 0
    for k, size in enumerate(sizes):
        cells = [path[idx]]
        for _ in range(size):
            doors[pedges[idx]] = seq[k]
            idx += 1
            cells.append(path[idx])
        seg_color.append(seq[k])
        seg_cells.append(cells)

    # (3) 남은 변 — **같은 색이 이어지지 않게** 칠한다.
    #     랜덤으로 채우면 색 사슬이 생겨 경로를 통째로 건너뛰는 지름길이 뚫린다.
    #     경로 밖에서는 한 색으로 한 칸만 가게 막아야 의도 경로가 최적해가 된다.
    rest = [e for e in ALL_EDGES if e not in doors]
    rng.shuffle(rest)
    count = collections.Counter(doors.values())
    for e in rest:
        used = {doors[f] for f in edge_nbrs(e) if f in doors}
        cand = [c for c in COLORS if c not in used] or COLORS
        color = min(cand, key=lambda c: (count[c], rng.random()))
        doors[e] = color
        count[color] += 1
    assert len(doors) == len(ALL_EDGES)

    # (4) 지름길 보정
    protected = set(pedges) | set(edges_of(bw))
    if not repair(rng, doors, set(pedges), set(edges_of(bw))):
        return _fail(4)

    # (5) 오답 가지 — **보정 뒤에** 심는다. 보정은 긴 사슬부터 끊으므로 먼저 심으면
    #     보정이 도로 다 지운다(실측: 12개 중 0~3개만 생존). 하나 심을 때마다 하한을
    #     다시 보고, 깨지면 그 가지만 되돌린다.
    cell_color = {}
    for color, cells in zip(seg_color, seg_cells):
        for c in cells:
            cell_color.setdefault(c, color)     # 그 칸에서 열려 있는 색

    roots = [c for c in path if c in cell_color]
    rng.shuffle(roots)
    planted = 0
    for root in roots:
        if planted >= DECOY_COUNT + 2:
            break
        for _try in range(3):               # 뻗는 방향이 랜덤이라 같은 뿌리도 재시도할 값이 있다
            changed = grow_decoy(rng, doors, cell_color[root], root,
                                 on_path, protected, DECOY_DEPTH + 1)
            if len(changed) >= DECOY_DEPTH and floors_ok(doors):
                planted += 1
                break
            for e, old_color in changed:
                doors[e] = old_color
    if planted < DECOY_COUNT:
        return _fail(5)

    # ── 검증 (§3.3) ──
    count = collections.Counter(doors.values())
    if (min(count[c] for c in COLORS) < COLOR_SPREAD[0]
            or max(count[c] for c in COLORS) > COLOR_SPREAD[1]):
        return _fail(6)

    sw = {}
    for label, cmap, floor, walled in PARTS:
        v = min_switches(passable(doors, walled), doors, cmap, START, GOAL)
        if v is None or v < floor:
            return _fail(7)
        lo, hi = sw.get(label, (99, -1))
        sw[label] = (min(lo, v), max(hi, v))

    bwlen = bw_path_len(doors)
    if bwlen is None or not (BW_LEN[0] <= bwlen <= BW_LEN[1]):
        return _fail(8)
    bwsw = bw_only_switches(doors)
    if bwsw is None or bwsw < BW_ONLY_SW:
        return _fail(9)

    return {
        "seed": seed,
        "rule": {"missingColorIsWall": MISSING_IS_WALL},
        "grid": {"n": N, "pitch": PITCH, "extent": EXTENT, "doorWidth": WIDTH,
                 "doorThickness": THICK, "doorHeight": HEIGHT},
        "startCell": list(START), "goalCell": list(GOAL),
        "startCenter": [HALF, HALF], "goalCenter": [center(N - 1), center(N - 1)],
        "path": [list(c) for c in path],
        "bwPath": [list(c) for c in bw],
        "segments": [{"color": c, "cells": [list(x) for x in cells]}
                     for c, cells in zip(seg_color, seg_cells)],
        "doors": door_boxes(doors),
        "pads": pad_ring(),
        "stats": {
            "pathLen": len(path), "segments": n_seg, "switches": sw,
            "soloPathLen": bwlen, "bwOnlySwitches": bwsw,
            "decoysPlanted": planted,
            "decoyDepths": decoy_depths(doors, path, seg_color),
            "colorCount": {c: count[c] for c in COLORS},
        },
    }


# ── 8. 좌표 산출 ──────────────────────────────────────────────

def door_boxes(doors):
    """문 하나 = [name, cx, cz, sx, sz, color]. y=7.5 / 높이 15는 빌더 상수."""
    out = []
    for (a, b), color in sorted(doors.items()):
        (ai, aj), (bi, bj) = a, b
        if ai != bi:                          # 세로문 — x 격자선 위, z는 칸 중심
            out.append(["DoorV_{0}_{1}".format(min(ai, bi), aj),
                        PITCH * max(ai, bi), center(aj), THICK, WIDTH, color])
        else:                                 # 가로문 — z 격자선 위, x는 칸 중심
            out.append(["DoorH_{0}_{1}".format(ai, min(aj, bj)),
                        center(ai), PITCH * max(aj, bj), WIDTH, THICK, color])
    return out


def pad_ring():
    """2층 패드 54개 = 9구간 x 6색. 구간 중심에 반경 4m 링 (§1.5).
    구간 번호는 넘버패드 배치(1 = 좌하단, 9 = 우상단). y는 빌더 상수."""
    import math
    out = []
    for zi, cz in enumerate((20.0, 60.0, 100.0)):
        for xi, cx in enumerate((20.0, 60.0, 100.0)):
            zone = zi * 3 + xi + 1
            for k, color in enumerate(COLORS):
                ang = math.radians(60 * k)
                out.append(["Pad_Z{0}_{1}".format(zone, color),
                            round(cx + 4.0 * math.cos(ang), 3),
                            round(cz + 4.0 * math.sin(ang), 3),
                            color, zone])
    return out


# ── 9. 시드 탐색 ──────────────────────────────────────────────

def main():
    try:
        sys.stdout.reconfigure(encoding="utf-8")   # 콘솔이 cp949여도 안 깨지게
    except AttributeError:
        pass

    args = sys.argv[1:]
    seeds = ([int(args[args.index("--seed") + 1])] if "--seed" in args
             else range(7001, 7001 + 6000))

    tried = 0
    for seed in seeds:
        tried += 1
        m = build(seed)
        if not m:
            continue
        st = m["stats"]
        print("seed={0}  의도 경로 {1}칸 / {2}구간  (시도 {3}회)".format(
            seed, st["pathLen"], st["segments"], tried))
        print("  최소 전환 " + " · ".join(
            "{0} {1}".format(k, v[0] if v[0] == v[1] else "{0}~{1}".format(*v))
            for k, v in sorted(st["switches"].items())))
        print("  솔로(흑∪백) {0}칸 = {1:.0f}초 주행 · 흑백만 쓰면 {2}전환".format(
            st["soloPathLen"], st["soloPathLen"] * PITCH / 10.0, st["bwOnlySwitches"]))
        print("  오답 가지 {0}개 · 색당 문 {1}".format(
            st["decoysPlanted"], st["colorCount"]))
        print("  문 {0}개 · 패드 {1}개".format(len(m["doors"]), len(m["pads"])))
        json.dump(m, open("t5doors.json", "w"), indent=1)
        print("  -> t5doors.json")
        return 0

    print("실패 - {0}개 시드에서 합격 배치를 못 찾았다".format(tried))
    return 1


if __name__ == "__main__":
    sys.exit(main())
