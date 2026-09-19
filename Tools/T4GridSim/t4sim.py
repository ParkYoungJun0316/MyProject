"""T.Stage4 복도+격자 시뮬레이터 — 인원별 난이도 추정 (Unity 밖, 빌드 비포함).

SSOT: Assets/Docs/TStage4TrapRandomization.md
입력: t4scene.py가 뽑은 t4boards.json

[무엇을 재는가]
 "판이 통행 가능한가"(t4scene 격자 BFS로 충분)가 아니라, **움직이는 벽 사이의 창(window) 안에서
 N명이 서로 칸을 비켜 가며 끝까지 갈 수 있는가**를 잰다. T4의 난이도는 지형 자체가 아니라
 "창 안에 안전한 칸이 인원수만큼 있는가"에서 나오기 때문이다(문서 §1.7 감수 1번의 그 축).

[모델 — 코드에서 그대로 옮긴 것]
 · 벽 시뮬: MovingCorridor.StepSimulation을 그대로. dt=0.02(ProjectSettings Fixed Timestep),
   속도는 틱 번호로 예약, 간격은 균등난수, front만 min/maxWallDistance로 클램프.
 · 벽 두께 2.5m(월드 실측 — Ring은 x축 90° 회전이라 scale.z가 두께다) →
   통과 가능 구간 = [back+1.25, front-1.25]. 중심거리 25~35 → 창 22.5~32.5m(2.8~4행).
 · 플레이어 속도 10 m/s(Kkultteok.prefab speed=10, runMultiplier=1) → 한 칸(8m) 이동 0.8초.
 · CapacityTile: capacity 1, sinkSpeed 1, dropoutDepth 2.5 → 2명이 2.5초 겹치면 바닥 빠짐.
   restoreSpeed 8로 빠르게 복귀(비대칭).
 · BreakTile: 밟는 순간 arm → warnSeconds(씬 1초) 뒤 영구 붕괴. 복구 없음.
 · 벽 접촉: ContactKnockback 25~100, mass 1, drag 0, 억제 0.25초 → 6.25~25m 밀림(즉사 아님).
   밀려 간 칸이 구멍이면 낙사.

[모델의 한계 — 결과를 읽을 때 반드시 같이 읽을 것]
 · 플레이어 정책은 탐욕적 국소 선택(앞으로·안전칸 우선·남의 칸 회피)이다. 사람은 이보다
   잘 하기도 못 하기도 한다 → 절대 확률이 아니라 **스펙 간/인원 간 상대 비교**로만 쓸 것.
 · 이동은 4방향, 대각선·점프 없음. 안개·시야·조작 실수·핑은 모델에 없다.
 · 사망 1건 = 런 실패로 센다(문서 §2.3 현재 전제. 부활 리팩토링 코드는 아직 없음).
 · System.Random과 Python random은 시퀀스가 달라 같은 시드로 씬과 같은 패턴이 나오지는 않는다.
   분포만 같다.

사용:
    python t4sim.py                          # 현재 스펙 vs 신 스펙, 판 5장 × 1~4인
    python t4sim.py --seeds 200              # 시드 수 늘리기
    python t4sim.py --board "T4_Grid10_P1 (4)" --spec new --trace   # 한 런 추적
"""
import argparse
import json
import math
import os
import random
import statistics

HERE = os.path.dirname(os.path.abspath(__file__))
BOARDS_JSON = os.path.join(HERE, "t4boards.json")

PHYS_DT = 0.02          # ProjectSettings/TimeManager Fixed Timestep
SIM_DT = 0.1            # 플레이어 의사결정 간격(1m 이동마다)
PITCH = 8.0
COLS = [-12.0, -4.0, 4.0, 12.0]
WALL_HALF = 1.25        # 벽 두께 2.5m의 절반
PLAYER_SPEED = 10.0
STEP_TIME = PITCH / PLAYER_SPEED      # 한 칸 0.8초
TILE_HALF_USABLE = 3.5                # 8m 타일에서 발을 놓을 수 있는 반폭(가장자리 0.5 여유)

CAP_SINK_SPEED = 1.0
CAP_DROPOUT_DEPTH = 2.5
CAP_RESTORE_SPEED = 8.0
BREAK_WARN = 1.0        # 씬 BreakTileDirector.warnSeconds
KNOCK_MIN, KNOCK_MAX = 25.0, 100.0
KNOCK_SUPPRESS = 0.25   # NetworkPlayerSetup knockbackSuppressDuration
GOAL_Z = 300.0          # Ground/Ground (z 300~332) 앞면
TIMEOUT = 900.0

# 벽을 첫 행보다 앞으로 밀어 시작한다 — 씬의 초기 위치(back −10 / front +10)는 격자 첫 행(z16)보다
# 뒤라, 활성화 순간 플레이어가 창 밖(앞 벽보다 앞)에 있게 된다. 스폰 좌표(z −5~+5)에 바닥이
# 아예 없는 것과 같은 뿌리의 문제이고, 시뮬의 관심사가 아니므로 창이 첫 행을 덮은 상태에서 시작한다.
WALL_START_BACK_BEFORE_FIRST_ROW = 12.0

SPECS = {
    # 씬 현재값 (t4scene.py가 읽은 것과 일치해야 한다)
    "cur": {"back": ([0.5, 1, 2, 3], 1.5, 2.5), "front": ([-1, 1.5, 2.5], 1.0, 2.0),
            "dist": (25.0, 35.0)},
    # 사용자 요청 1안 (2026-09-19) — 뒤 벽 후퇴 포함
    "new": {"back": ([-2, -1, 1, 2, 3], 1.5, 2.5), "front": ([-2, -1, 1.5, 2.5], 1.0, 2.0),
            "dist": (25.0, 35.0)},
    # 확정 후보 (2026-09-19) — 240m · 평균 2.5 m/s · 후퇴 없음 · 창 26~34
    "v25": {"back": ([1.5, 2, 3, 3.5], 1.5, 2.5), "front": ([-2, 2, 3, 4], 1.0, 2.0),
            "dist": (26.0, 34.0)},
}


# ── 벽 시뮬 (MovingCorridor.StepSimulation 이식) ───────────────────────────

_TRACE_CACHE = {}


def wall_trace_cached(spec_name, seed, back0, front0, min_d, max_d, ticks):
    """같은 (스펙, 시드)면 판·인원이 달라도 벽 궤적은 같다 — 한 번만 돌린다."""
    key = (spec_name, seed, back0, front0, min_d, max_d, ticks)
    hit = _TRACE_CACHE.get(key)
    if hit is None:
        hit = wall_trace(SPECS[spec_name], seed, back0, front0, min_d, max_d, ticks)
        _TRACE_CACHE[key] = hit
    return hit


def wall_trace(spec, seed, back0, front0, min_d, max_d, ticks):
    """틱마다의 (back, front) 중심 z. 속도 추출 순서(back → front)까지 코드와 같게 유지한다."""
    rng = random.Random(seed)
    (bs, bmin, bmax) = spec["back"]
    (fs, fmin, fmax) = spec["front"]

    back, front = back0, front0
    b_speed = f_speed = 0.0
    b_next = f_next = 0
    out_back = [0.0] * ticks
    out_front = [0.0] * ticks
    b_neg = 0  # 뒤 벽이 후퇴한 틱 수

    for tick in range(ticks):
        if tick >= b_next:
            b_speed = bs[rng.randrange(len(bs))]
            b_next += max(1, round(rng.uniform(bmin, bmax) / PHYS_DT))
        if tick >= f_next:
            f_speed = fs[rng.randrange(len(fs))]
            f_next += max(1, round(rng.uniform(fmin, fmax) / PHYS_DT))

        back_next = back + b_speed * PHYS_DT
        front_next = front + f_speed * PHYS_DT

        d = front_next - back_next
        if min_d > 0 and d < min_d:
            front_next = back_next + min_d
        elif max_d > 0 and d > max_d:
            front_next = back_next + max_d

        back, front = back_next, front_next
        out_back[tick] = back
        out_front[tick] = front
        if b_speed < 0:
            b_neg += 1

    return out_back, out_front, b_neg / float(ticks)


# ── 판 ────────────────────────────────────────────────────────────────────

class Board:
    def __init__(self, data, trim_rows=0):
        """trim_rows: 뒤쪽(골 쪽) 행을 이만큼 잘라낸다 — 복도 길이를 줄이는 안을 재기 위한 것."""
        self.name = data["name"]
        self.role = {}
        for key, role in data["cells"].items():
            r, c = key.split(",")
            self.role[(int(r), int(c))] = role
        rows = [r for r, _ in self.role]
        self.r0, self.r1 = min(rows), max(rows)
        if trim_rows > 0:
            self.r1 -= trim_rows
            self.role = {k: v for k, v in self.role.items() if k[0] <= self.r1}
            self.name += " −%d행" % trim_rows

    @property
    def break_count(self):
        return sum(1 for v in self.role.values() if v == "b")


# ── 런 1회 ────────────────────────────────────────────────────────────────

def goal_potential(board, solid):
    """각 칸에서 골(마지막 행 밖)까지의 남은 칸 수. 플레이어 정책이 이 값을 내려가는 쪽으로 움직인다.
    국소 탐욕만 쓰면 '앞 칸이 구멍인 열'에서 비켜서지 않고 벽에 깔린다 — 그걸 막는 장치다."""
    dist = {}
    queue = []
    for c in range(4):
        cell = (board.r1, c)
        if solid.get(cell):
            dist[cell] = 1
            queue.append(cell)
    head = 0
    while head < len(queue):
        r, c = queue[head]
        head += 1
        for nr, nc in ((r - 1, c), (r + 1, c), (r, c - 1), (r, c + 1)):
            if not (0 <= nc <= 3) or not solid.get((nr, nc)):
                continue
            if (nr, nc) in dist:
                continue
            dist[(nr, nc)] = dist[(r, c)] + 1
            queue.append((nr, nc))
    return dist


class Player:
    __slots__ = ("cell", "moving_to", "move_left", "alive", "done", "wait", "on_break")

    def __init__(self, cell):
        self.cell = cell
        self.moving_to = None
        self.move_left = 0.0
        self.alive = True
        self.done = False
        self.wait = 0.0
        self.on_break = 0

    def z(self, hi=None):
        """실제 발 위치. 타일이 8m라 중심에 서 있을 이유가 없다 — 사람은 앞 벽에 붙어
        뒤 벽과 거리를 최대로 벌린다. 그래서 타일 안에서 앞쪽으로 최대한 붙인 값을 쓴다."""
        if self.moving_to is None:
            center = self.cell[0] * PITCH
        else:
            t = 1.0 - self.move_left / STEP_TIME
            center = (self.cell[0] * PITCH) * (1 - t) + (self.moving_to[0] * PITCH) * t
        forward = center + TILE_HALF_USABLE
        if hi is None:
            return forward
        return max(center - TILE_HALF_USABLE, min(forward, hi - 0.5))

    def occupied(self):
        """점유로 세는 칸 — 이동 중엔 절반을 넘긴 쪽."""
        if self.moving_to is None:
            return self.cell
        return self.moving_to if self.move_left < STEP_TIME * 0.5 else self.cell


def simulate(board, n_players, spec_name, seed, trace=False):
    spec = SPECS[spec_name]
    back0 = board.r0 * PITCH - WALL_START_BACK_BEFORE_FIRST_ROW
    front0 = back0 + 20.0   # 씬과 같은 초기 간격(20) — 첫 틱에 minWallDistance로 25로 벌어진다
    ticks = int(TIMEOUT / PHYS_DT) + 2
    min_d, max_d = spec.get("dist", (25.0, 35.0))
    wb, wf, back_neg_ratio = wall_trace_cached(
        spec_name, seed, back0, front0, min_d, max_d, ticks)

    rng = random.Random(seed ^ 0x54345F53)

    solid = {cell: True for cell in board.role}
    armed = {}          # (r,c) -> 붕괴 예정 시각
    sink = {}           # (r,c) -> 침강 깊이
    broken = set()

    # 시작: 첫 행의 열에 나눠 선다(첫 두 행은 5장 모두 꽉 차 있다 — 사실상 시작 발판)
    start_cells = [(board.r0, c) for c in range(4) if solid.get((board.r0, c))]
    players = [Player(start_cells[i % len(start_cells)]) for i in range(n_players)]

    stats = {
        "clear": False, "time": 0.0, "death_cause": None, "death_z": None,
        "breaks": 0, "overload_sec": 0.0, "knock": 0, "forced_break": 0, "stack": 0,
        "wait_ratio": 0.0, "window_cells_mean": 0.0, "window_cells_min": 99,
        "back_neg_ratio": back_neg_ratio, "window_len_mean": 0.0,
    }
    win_cells_acc, win_len_acc, samples = 0.0, 0.0, 0
    potential = None          # 바닥이 바뀔 때만 다시 계산한다

    step = 0
    t = 0.0
    while t < TIMEOUT:
        tick = int(t / PHYS_DT)
        lo = wb[tick] + WALL_HALF
        hi = wf[tick] - WALL_HALF

        # 1) 붕괴 처리 — 영구 구멍
        for cell, when in list(armed.items()):
            if t >= when:
                del armed[cell]
                solid[cell] = False
                broken.add(cell)
                potential = None
                stats["breaks"] += 1

        # 2) 점유 집계
        occ = {}
        for p in players:
            if p.alive and not p.done:
                occ.setdefault(p.occupied(), []).append(p)

        # 3) 용량 타일 침강/복귀
        for cell, role in board.role.items():
            if role != "c" or cell in broken:
                continue
            count = len(occ.get(cell, ()))
            depth = sink.get(cell, 0.0)
            if count > 1:
                depth += CAP_SINK_SPEED * SIM_DT
                stats["overload_sec"] += SIM_DT
                if depth >= CAP_DROPOUT_DEPTH:
                    for p in occ.get(cell, ()):
                        p.alive = False
                        stats["death_cause"] = stats["death_cause"] or "capacity"
                        stats["death_z"] = stats["death_z"] or cell[0] * PITCH
                    depth = 0.0
                    solid[cell] = False          # 붕괴 구간 — 잠시 구멍
                    sink[cell] = -1.3            # 음수 = 복귀 대기 시간 표시
                    potential = None
                    continue
            else:
                if depth < 0:
                    depth += SIM_DT
                    if depth >= 0:
                        depth = 0.0
                        solid[cell] = cell not in broken
                        potential = None
                else:
                    depth = max(0.0, depth - CAP_RESTORE_SPEED * SIM_DT)
            sink[cell] = depth

        # 4) 밟힌 파괴 타일 arm
        for cell, ps in occ.items():
            if board.role.get(cell) == "b" and cell not in armed and cell not in broken:
                armed[cell] = t + BREAK_WARN

        # 5) 창 안 안전 칸 수 기록
        usable_rows = [r for r in range(board.r0, board.r1 + 2)
                       if (r * PITCH + 4.0) > lo + 0.5 and (r * PITCH - 4.0) < hi - 0.5]
        win_cells = sum(1 for r in usable_rows for c in range(4)
                        if solid.get((r, c)) and (r, c) not in armed)
        win_cells_acc += win_cells
        win_len_acc += (hi - lo)
        stats["window_cells_min"] = min(stats["window_cells_min"], win_cells)
        samples += 1

        # 6) 이동 중인 사람 먼저 진행시키고, 그들이 쓰는 칸을 전부 예약해 둔다.
        #    (예약을 결정 루프 안에서 하면 순서에 따라 같은 칸을 두 명이 잡는다 — capacity 1 위반)
        active = [q for q in players if q.alive and not q.done]
        claimed = set()
        deciders = []
        for p in active:
            if p.moving_to is not None:
                p.move_left -= SIM_DT
                if p.move_left <= 0:
                    p.cell, p.moving_to, p.move_left = p.moving_to, None, 0.0
                    claimed.add(p.cell)
                else:
                    claimed.add(p.cell)
                    claimed.add(p.moving_to)
            else:
                deciders.append(p)
                claimed.add(p.cell)

        # 7) 멈춰 있는 사람의 선택.
        #    후보를 먼저 다 모아 놓고 (비용, 사람) 순으로 배정한다 — 사람 순서대로 최선을 집어가면
        #    뒤 사람이 남은 칸 중 뒤로 가는 칸을 골라 벽에 깔리는 쪽으로 몰린다.
        wish = {}                                # p -> [(key, cell), ...]
        for p in list(deciders):
            r, c = p.cell
            if r > board.r1:                     # 격자를 벗어남 = 골 발판
                p.done = True
                deciders.remove(p)
                continue

            # 현재 칸이 사라졌으면 낙사
            if not solid.get(p.cell, False):
                p.alive = False
                deciders.remove(p)
                stats["death_cause"] = stats["death_cause"] or (
                    "break" if p.cell in broken else "fall")
                stats["death_z"] = stats["death_z"] or r * PITCH
                continue

            if potential is None:
                potential = goal_potential(board, solid)

            # 넉백으로 남의 칸에 떨어졌다면 제자리는 선택지가 아니다 — 비켜야 침강이 멈춘다
            crowded = len(occ.get(p.cell, ())) > 1

            options = []
            for (nr, nc) in ((r + 1, c), (r, c - 1), (r, c + 1), (r - 1, c), (r, c)):
                if not (0 <= nc <= 3):
                    continue
                if crowded and (nr, nc) == p.cell:
                    continue
                if nr > board.r1:                # 골 발판으로 나가기
                    if (nr * PITCH - 4.0) < hi - 0.5:
                        options.append((-1, -nr, 0, 0, (nr, nc)))
                    continue
                if not solid.get((nr, nc), False):
                    continue
                # 타일 뒤쪽 절반만 앞 벽 안에 들어와 있어도 올라설 수 있다
                if (nr * PITCH - TILE_HALF_USABLE) > hi - 0.5:
                    continue
                if (nr, nc) in armed:
                    continue                     # 경고 중인 칸은 피한다 — 내가 밟아 발동시킨 칸도
                if (nr, nc) in claimed and (nr, nc) != p.cell:
                    continue                     # 우선은 남의 칸을 피한다(겹치면 침강)
                d = potential.get((nr, nc))
                if d is None:                    # 골에서 끊긴 칸 — 마지막 수단
                    d = 999
                is_break = 1 if board.role.get((nr, nc)) == "b" else 0
                # 우선순위: 골까지 가까운 칸 → 앞선 행 → 같은 열 유지.
                # 파괴 타일을 피하는 항은 넣지 않는다 — §1.4대로 _b는 _c와 같은 흰색이라
                # 밟기 전에는 플레이어가 구분할 수 없다(발동 후에는 armed로 피한다).
                # 남이 이미 쓰는 열은 피한다 — 한 줄로 따라붙으면 앞사람이 밟아 발동시킨 칸
                # 앞에서 갇힌다(문서 §1.4의 "밟은 사람 vs 뒷사람"). 4열이니 나눠 서는 게 정상 플레이다.
                lane = 1 if any(q is not p and q.cell[1] == nc for q in active) else 0
                options.append((d, -nr, lane, 0 if nc == c else 1, (nr, nc)))

            options.sort()
            wish[p] = options

        # 배정 — (비용, 앞선 사람) 순으로 훑으며 먼저 비는 칸을 잡는다
        pending = []
        for idx, p in enumerate(deciders):
            for d, negr, lane, lat, cell in wish.get(p, ()):
                pending.append((d, negr, lane, lat, -p.z(hi), idx, cell))
        pending.sort()

        taken = set(claimed)
        assigned = {}
        for d, negr, lane, lat, negz, idx, cell in pending:
            p = deciders[idx]
            if p in assigned or (cell in taken and cell != p.cell):
                continue
            assigned[p] = cell
            taken.add(cell)

        # 구제 패스 — 뒤 벽이 타일 끝까지 닿았거나 내가 밟아 발동시킨 칸 위에 있으면,
        # 남이 있는 칸이라도 올라간다. 겹치면 2.5초 유예가 있고 복귀는 8 m/s라
        # "잠깐 겹쳐 서기"는 죽는 선택이 아니다(용량 타일은 게이트가 아니라 처벌 — §1.1).
        for p in deciders:
            if assigned.get(p, p.cell) != p.cell:
                continue
            r, c = p.cell
            in_danger = (r * PITCH + TILE_HALF_USABLE) < lo + 2.0 or p.cell in armed
            if not in_danger:
                continue
            best = None
            for (nr, nc) in ((r + 1, c), (r, c - 1), (r, c + 1), (r - 1, c)):
                if not (0 <= nc <= 3) or not solid.get((nr, nc), False):
                    continue
                if (nr, nc) in armed:
                    continue
                if (nr * PITCH - TILE_HALF_USABLE) > hi - 0.5:
                    continue
                key = (potential.get((nr, nc), 999), -nr, (nr, nc))
                if best is None or key < best:
                    best = key
            if best is not None:
                assigned[p] = best[2]
                stats["stack"] += 1

        for p in deciders:
            target = assigned.get(p, p.cell)
            if target == p.cell:
                p.wait += SIM_DT
                claimed.add(p.cell)
            else:
                if board.role.get(target) == "b":
                    stats["forced_break"] += 1
                    p.on_break += 1
                p.moving_to = target
                p.move_left = STEP_TIME
                claimed.add(target)

        # 8) 뒤 벽 접촉 — 넉백(즉사가 아니라 6.25~25m 밀림. 밀려 간 칸이 구멍이면 낙사)
        for p in deciders:
            if not p.alive or p.done:
                continue
            if p.z(hi) < lo:
                stats["knock"] += 1
                dist = rng.uniform(KNOCK_MIN, KNOCK_MAX) * KNOCK_SUPPRESS
                nr = int(round((p.z(hi) + dist) / PITCH))
                nr = min(nr, board.r1 + 1)
                landing = (nr, p.cell[1])
                p.moving_to, p.move_left = None, 0.0
                if nr > board.r1:
                    p.done = True
                elif solid.get(landing, False):
                    p.cell = landing
                else:
                    p.alive = False
                    stats["death_cause"] = stats["death_cause"] or "knockback"
                    stats["death_z"] = stats["death_z"] or nr * PITCH

        alive = [p for p in players if p.alive]
        if len(alive) < n_players:               # 사망 1건 = 런 실패(§2.3)
            stats["time"] = t
            break
        if all(p.done for p in alive):
            stats["clear"] = True
            stats["time"] = t
            break

        if trace and step % max(1, int(round(trace / SIM_DT))) == 0:
            print("  t=%5.1f  창 z %6.1f~%6.1f (%4.1fm) 안전칸 %2d  위치 %s"
                  % (t, lo, hi, hi - lo, win_cells,
                     [("%d,%d" % p.cell if p.alive else "DEAD") for p in players]))

        step += 1
        t += SIM_DT
    else:
        stats["time"] = TIMEOUT
        stats["death_cause"] = "timeout"

    if samples:
        stats["window_cells_mean"] = win_cells_acc / samples
        stats["window_len_mean"] = win_len_acc / samples
        stats["wait_ratio"] = (sum(p.wait for p in players)
                               / (n_players * max(stats["time"], SIM_DT)))
    return stats


# ── 집계 ──────────────────────────────────────────────────────────────────

def run_matrix(boards, specs, counts, seeds, base_seed):
    rows = []
    for spec_name in specs:
        for board in boards:
            for n in counts:
                res = [simulate(board, n, spec_name, base_seed + s) for s in range(seeds)]
                clears = [r for r in res if r["clear"]]
                causes = {}
                for r in res:
                    if not r["clear"]:
                        causes[r["death_cause"]] = causes.get(r["death_cause"], 0) + 1
                rows.append({
                    "spec": spec_name, "board": board.name, "n": n,
                    "clear": 100.0 * len(clears) / len(res),
                    "time": statistics.mean(r["time"] for r in clears) if clears else float("nan"),
                    "win_len": statistics.mean(r["window_len_mean"] for r in res),
                    "win_cells": statistics.mean(r["window_cells_mean"] for r in res),
                    "win_cells_min": min(r["window_cells_min"] for r in res),
                    "wait": 100.0 * statistics.mean(r["wait_ratio"] for r in res),
                    "breaks": statistics.mean(r["breaks"] for r in res),
                    "overload": statistics.mean(r["overload_sec"] for r in res),
                    "knock": statistics.mean(r["knock"] for r in res),
                    "back_neg": 100.0 * statistics.mean(r["back_neg_ratio"] for r in res),
                    "causes": causes,
                })
    return rows


def static_table(boards):
    """정책과 무관한 지형 지표. 한 행의 안전 칸이 k개면 그 행엔 동시에 k명까지만 설 수 있다 —
    k < 인원이면 그 행은 구조적 병목이고, 벽이 밀어붙이는 동안 줄을 서서 통과해야 한다."""
    hdr = ("%-20s %5s %5s | %-21s | %-21s" %
           ("board", "cells", "_b", "행별 안전칸 최소(정상)", "행별 안전칸 최소(_b 붕괴)"))
    print(hdr)
    print("-" * len(hdr))
    for b in boards:
        for worst in (False, True):
            widths = []
            for r in range(b.r0, b.r1 + 1):
                k = sum(1 for c in range(4)
                        if b.role.get((r, c)) and not (worst and b.role[(r, c)] == "b"))
                widths.append(k)
            tag = "_b 붕괴" if worst else "정상"
            counts = {k: widths.count(k) for k in sorted(set(widths))}
            print("%-20s %5s %5s | %-6s min=%d  폭별 행수 %s" %
                  (b.name if not worst else "", "" if worst else len(b.role),
                   "" if worst else b.break_count, tag, min(widths), counts))
    print()


def print_rows(rows):
    hdr = ("%-5s %-20s %2s | %6s %7s %6s %6s %4s %6s %6s %6s | %s"
           % ("spec", "board", "N", "clear%", "time s", "win m", "cells", "min",
              "wait%", "break", "knock", "fail causes"))
    print(hdr)
    print("-" * len(hdr))
    for r in rows:
        causes = ", ".join("%s:%d" % (k, v) for k, v in
                           sorted(r["causes"].items(), key=lambda kv: -kv[1]))
        print("%-5s %-20s %2d | %6.1f %7s %6.1f %6.1f %4d %6.1f %6.1f %6.1f | %s"
              % (r["spec"], r["board"], r["n"], r["clear"],
                 ("%.0f" % r["time"]) if not math.isnan(r["time"]) else "-",
                 r["win_len"], r["win_cells"], r["win_cells_min"],
                 r["wait"], r["breaks"], r["knock"], causes))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seeds", type=int, default=60)
    ap.add_argument("--base-seed", type=int, default=7000)
    ap.add_argument("--spec", action="append", choices=list(SPECS), default=None)
    ap.add_argument("--board", action="append", default=None)
    ap.add_argument("--players", action="append", type=int, default=None)
    ap.add_argument("--trim-rows", type=int, default=0, help="뒤쪽 행을 잘라 복도를 줄인다")
    ap.add_argument("--trace", action="store_true", help="한 런을 5초마다 출력")
    args = ap.parse_args()

    data = json.load(open(BOARDS_JSON, encoding="utf-8"))
    boards = [Board(b, args.trim_rows) for b in data["boards"]]
    if args.board:
        boards = [b for b in boards
                  if any(b.name.startswith(want) for want in args.board)]
    specs = args.spec or ["cur", "new"]
    counts = args.players or [1, 2, 3, 4]

    scene = data["corridor"]
    print("씬 현재값: back %s / interval %.1f~%.1f · front %s / interval %.1f~%.1f · 간격 %g~%g"
          % (scene["back"]["speeds"], scene["back"]["minInterval"], scene["back"]["maxInterval"],
             scene["front"]["speeds"], scene["front"]["minInterval"], scene["front"]["maxInterval"],
             scene["minWallDistance"], scene["maxWallDistance"]))
    for name in specs:
        s = SPECS[name]
        print("  %-4s back %s · front %s" % (name, s["back"][0], s["front"][0]))
    print("시드 %d개 × 판 %d장 × 인원 %s\n" % (args.seeds, len(boards), counts))

    if args.trace:
        for board in boards:
            for n in counts:
                print("=== %s / %s / %d인 ===" % (specs[0], board.name, n))
                st = simulate(board, n, specs[0], args.base_seed, trace=True)
                print("  →", {k: v for k, v in st.items() if k in
                              ("clear", "time", "death_cause", "death_z", "breaks", "knock")})
        return 0

    static_table(boards)
    rows = run_matrix(boards, specs, counts, args.seeds, args.base_seed)
    print_rows(rows)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
