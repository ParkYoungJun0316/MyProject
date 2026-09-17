"""T.Stage5 runner maze generator + BFS verifier (M1).
Grid 12x12, cell = 100/12. Output: t5maps.json (layout for the editor builder) + stats printout.
"""
import json, random, itertools, collections, sys

N = 12
SLOTS = ["Blue", "Purple", "Green", "Yellow"]
BUDGET = {"Blue": 2, "Purple": 2, "Green": 2, "Yellow": 2, "Black": 4, "White": 3}
MIN_SW, MAX_SW = 8, 12

def nbrs(r, c):
    for dr, dc in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        rr, cc = r + dr, c + dc
        if 0 <= rr < N and 0 <= cc < N:
            yield (rr, cc)

def ekey(a, b):
    return (a, b) if a < b else (b, a)

ALL_EDGES = set()
for r in range(N):
    for c in range(N):
        for n in nbrs(r, c):
            ALL_EDGES.add(ekey((r, c), n))

def spanning_tree(rng):
    # recursive backtracker -> long corridors
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

def tree_path(adj, s, g):
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
    path = []
    u = g
    while u is not None:
        path.append(u)
        u = prev[u]
    return path[::-1]

def min_switches(open_edges, doors, cmap, s, g):
    """0-1 BFS. state=(cell, open color idx or None). switching open color costs 1."""
    adj = adj_of(open_edges)
    colors = sorted(set(cmap[c] for c in doors.values()))
    states = [None] + colors
    INF = 10 ** 9
    dist = {}
    dq = collections.deque()
    dist[(s, None)] = 0
    dq.append((0, s, None))
    while dq:
        d, u, oc = dq.popleft()
        if dist.get((u, oc), INF) < d:
            continue
        if u == g:
            return d
        for nc in states:
            if nc != oc and nc is not None and dist.get((u, nc), INF) > d + 1:
                dist[(u, nc)] = d + 1
                dq.append((d + 1, u, nc))
        for v in adj[u]:
            e = ekey(u, v)
            if e in doors and cmap[doors[e]] != oc:
                continue
            if dist.get((v, oc), INF) > d:
                dist[(v, oc)] = d
                dq.appendleft((d, v, oc))
    return None

def mappings():
    """yield (label, cmap) for every player-count mapping."""
    base = {"Black": "Black", "White": "White"}
    # 2p: one guide; all slots -> guide color
    m = dict(base); m.update({s: "G1" for s in SLOTS})
    yield ("2p", m)
    # 3p: guides = 2 of 4 colors; other 2 slots randomly filled with guide colors
    for guides in itertools.combinations(SLOTS, 2):
        rest = [s for s in SLOTS if s not in guides]
        for fill in itertools.product(guides, repeat=len(rest)):
            m = dict(base); m.update({g: g for g in guides}); m.update(dict(zip(rest, fill)))
            yield ("3p", m)
    # 4p: guides = 3 of 4; runner slot filled with a guide color
    for guides in itertools.combinations(SLOTS, 3):
        rest = [s for s in SLOTS if s not in guides]
        for fill in guides:
            m = dict(base); m.update({g: g for g in guides}); m[rest[0]] = fill
            yield ("4p", m)
    ident = dict(base); ident.update({s: s for s in SLOTS})
    yield ("design", ident)

MAPPINGS = list(mappings())

def build(seed, corner):
    rng = random.Random(seed)
    tree = spanning_tree(rng)
    corners = [(0, 0), (0, N - 1), (N - 1, N - 1), (N - 1, 0)]
    s = corners[corner]
    g = corners[(corner + 2) % 4]
    adj = adj_of(tree)
    path = tree_path(adj, s, g)
    if not (52 <= len(path) <= 64):
        return None
    path_edges = [ekey(path[i], path[i + 1]) for i in range(len(path) - 1)]

    n_path = rng.choice([10, 11, 12])
    # evenly spaced door indices (skip first/last 2 edges)
    lo, hi = 2, len(path_edges) - 3
    span = (hi - lo) / (n_path - 1)
    idxs = sorted(set(min(hi, max(lo, int(round(lo + i * span + rng.uniform(-span * 0.3, span * 0.3))))) for i in range(n_path)))
    if len(idxs) != n_path:
        return None

    # colors along path: slot doors never adjacent; K/W never repeat consecutively
    budget = dict(BUDGET)
    seq = []
    prev_kind = None
    prev_slot = None
    for i in range(n_path):
        opts = []
        if prev_kind != "slot" and any(budget[x] for x in SLOTS):
            opts += [x for x in SLOTS if budget[x] and x != prev_slot]
        for x in ("Black", "White"):
            if budget[x] and x != prev_kind:
                opts.append(x)
        if not opts:
            return None
        # prefer slots when allowed to spread colors
        slot_opts = [x for x in opts if x in SLOTS]
        pick = rng.choice(slot_opts) if slot_opts and rng.random() < 0.6 else rng.choice(opts)
        budget[pick] -= 1
        seq.append(pick)
        if pick in SLOTS:
            prev_kind, prev_slot = "slot", pick
        else:
            prev_kind = pick
    doors = {path_edges[idxs[k]]: seq[k] for k in range(n_path)}

    # regions of tree split by path doors
    region = {}
    region_adj = adj_of([e for e in tree if e not in doors])
    # label regions by flood fill ordered along path
    order = [s] + [path[i + 1] for i in idxs]
    for ri, root in enumerate(order):
        q = [root]
        region[root] = ri
        while q:
            u = q.pop()
            for v in region_adj[u]:
                if v not in region:
                    region[v] = ri
                    q.append(v)

    open_edges = set(tree)
    non_tree = [e for e in ALL_EDGES if e not in tree]
    rng.shuffle(non_tree)
    # intra-region loops (~10% of walls)
    extra = max(10, int(len(non_tree) * 0.10))
    added = 0
    for e in non_tree:
        if added >= extra:
            break
        if region[e[0]] == region[e[1]]:
            open_edges.add(e)
            added += 1

    # leftover doors: parallel routes between adjacent regions (same coarse class as the path door)
    left = [x for x, k in budget.items() for _ in range(k)]
    rng.shuffle(left)
    for e in non_tree:
        if not left:
            break
        if e in open_edges:
            continue
        a, b = sorted((region[e[0]], region[e[1]]))
        if b != a + 1:
            continue
        pdoor = seq[a]  # door between region a and a+1
        want_slot = pdoor in SLOTS
        cand = [x for x in left if (x in SLOTS) == want_slot and (not want_slot or True)]
        if want_slot:
            cand = [x for x in cand if x != pdoor] or cand
        else:
            cand = [x for x in cand if x == pdoor]
        if not cand:
            continue
        pick = cand[0]
        left.remove(pick)
        open_edges.add(e)
        doors[e] = pick
    # remaining: dead-end side branches (chaser blockers, not needed for solution)
    side = [e for e in tree if e not in doors and e not in path_edges]
    rng.shuffle(side)
    for e in side:
        if not left:
            break
        if any(ekey(e[0], n) in doors or ekey(e[1], n) in doors for n in list(nbrs(*e[0])) + list(nbrs(*e[1]))):
            continue
        doors[e] = left.pop()
    if left:
        return None

    # verify every mapping
    res = {}
    for label, cmap in MAPPINGS:
        sw = min_switches(open_edges, doors, cmap, s, g)
        if sw is None or not (MIN_SW <= sw <= MAX_SW):
            return None
        lo_, hi_ = res.get(label, (99, -1))
        res[label] = (min(lo_, sw), max(hi_, sw))
    # solo: all open
    if min_switches(open_edges, {}, {}, s, g) is None:
        return None

    adj2 = adj_of(open_edges)
    dead_ends = sum(1 for r in range(N) for c in range(N) if len(adj2[(r, c)]) == 1)

    # chaser spawns: 16 cells far (>= 30m) from start, spread
    cell = 100 / N
    def dist(a, b):
        return (((a[0] - b[0]) * cell) ** 2 + ((a[1] - b[1]) * cell) ** 2) ** 0.5
    far = [(r, c) for r in range(N) for c in range(N) if dist((r, c), s) >= 30 and (r, c) != g]
    spawns = []
    rng.shuffle(far)
    for x in far:
        if all(dist(x, y) >= 16 for y in spawns):
            spawns.append(x)
        if len(spawns) == 16:
            break
    if len(spawns) < 16:
        for x in far:
            if x not in spawns and all(dist(x, y) >= 10 for y in spawns):
                spawns.append(x)
            if len(spawns) == 16:
                break
    if len(spawns) < 16:
        return None

    # 2F pads: 6, scattered (not near stand point at center), min spacing
    pads = []
    cands = [(r, c) for r in range(1, N - 1) for c in range(1, N - 1) if dist((r, c), (5.5, 5.5)) >= 12]
    rng.shuffle(cands)
    for x in cands:
        if all(dist(x, y) >= 28 for y in pads):
            pads.append(x)
        if len(pads) == 6:
            break
    if len(pads) < 6:
        return None
    pad_colors = ["Blue", "Purple", "Green", "Yellow", "Black", "White"]
    rng.shuffle(pad_colors)

    walls = [e for e in ALL_EDGES if e not in open_edges and e not in doors]
    return {
        "seed": seed,
        "start": s, "goal": g,
        "walls": sorted(walls),
        "doors": [{"a": e[0], "b": e[1], "color": c, "onPath": e in path_edges} for e, c in sorted(doors.items())],
        "pads": [{"cell": p, "color": pc} for p, pc in zip(pads, pad_colors)],
        "spawns": spawns,
        "stats": {"pathLen": len(path), "pathDoors": n_path, "deadEnds": dead_ends, "loops": added,
                  "switches": res},
    }

def main():
    maps = []
    seed = 5000
    for i in range(7):
        while True:
            seed += 1
            m = build(seed, i % 4)
            if m:
                maps.append(m)
                break
    for i, m in enumerate(maps):
        st = m["stats"]
        print(f"Map_{i+1:02d} seed={m['seed']} start={m['start']} goal={m['goal']} path={st['pathLen']} "
              f"pathDoors={st['pathDoors']} doors={len(m['doors'])} deadEnds={st['deadEnds']} loops={st['loops']} sw={st['switches']}")
    json.dump({"maps": maps}, open(sys.argv[1], "w"))

if __name__ == "__main__":
    main()
