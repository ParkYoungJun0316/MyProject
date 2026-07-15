---
name: flow-map
description: >-
  Map and simplify event/data flow (UI, NGO sync, spawn, RPC, listeners).
  Diagrams only on Canvas (Before/After); prose in chat. Invoke with @flow-map.
  Independent of bug-hunter / ngo-debug.
disable-model-invocation: true
---

# Flow Map

Invoke with **@flow-map**. Do not auto-run.  
Independent — do **not** require bug-hunter or ngo-debug first.  
Focus: **structure clarity + simplification**. Unidirectional is a common
outcome — **not** required.

Project: Unity 6.3, NGO 2.9 Listen-Server. Sync locks: `Assets/Docs/NetworkDesign.md`.

## Delivery split (required)

| Surface | What goes there |
|---------|-----------------|
| **Canvas** | Before / After **diagrams only** (layer skeleton). Entry count, RPC ×N, **⚠**, ~~removed~~. Minimal title + legend. **No** long Callouts, complexity essays, or file tables on canvas. |
| **Chat (agent)** | Scope, checklist answers, Complexity list, After principle one-liner, Optional next. Link the canvas. |

Do **not** dump full ASCII flow trees in chat when a canvas exists — point at the canvas.

## When to use

User **@mentions** this skill for: dual/re-subscribe, race, Inspector+code collision,
“왜 X가 바뀌면 Y도 바뀌지?”, pending snapback, unclear write ownership (UI/spawn/NV).

## When NOT to use

- Not @mentioned
- Tiny typo / null / balance-only
- User @ngo-debug or @bug-hunter only

## Order

1. **Scope lock** — one action/signal.
2. **File gather** — real method names on that path.
3. **Canvas Before** — current (or pre-fix if comparing bug→fix).
4. **Chat Complexity** — evidence bullets (file + method).
5. **Canvas After** — simpler same-intent path; role branches OK.
6. **Chat Optional next** — Stop for OK if NV/RPC **signature**, **write authority**,
   or **same-write call-site ownership** changes. Wait unless user asked to implement.
   UI-only pending/silent + unchanged network contract OK when implement requested.
7. No reconnect / late-join / parallel-stack “fixes”.

## Diagram format (Canvas)

Skeleton (labels = examples — rename to code):

```
[User Input]
    ↓
[UI Handler]          (e.g. OnXxxChanged)
    ↓
[ServerRpc] ×N
    ↓
[Host Logic / NV write]
    ↓
[OnChanged callback]
    ↓
[UI Refresh]
```

Forks: side branches; mark leaks **⚠**.

**Before:** entry-point count (same intent), RPC ×N, **⚠** collisions.  
**After:** intended entries (1 per intent; Host/Client role-split OK), target RPC ×N,
~~removed~~ / dashed removed paths.

Same layer vocabulary every time; top→bottom column. SVG Before|After side-by-side
on a **new or topic-named** `.canvas.tsx` (e.g. `lobby-color.canvas.tsx`) —
not one eternal shared file, not chat ASCII walls. Canvas = diagrams only.

## Mapping checklist (chat)

| Question | Why |
|----------|-----|
| How many places write this state? | Multi-writer = race |
| How many listeners on same control/event? | Dual subscribe |
| Silent UI set still fire Inspector/persistent? | Unity pitfall |
| Refresh overwrite in-flight user choice? | Snapback |
| Host path vs Client path same? | Role asymmetry |

## Chat output format

1. **Scope** — one sentence + canvas link  
2. **Checklist** — short answers (table OK)  
3. **Complexity** — bullets with evidence  
4. **After principle** — one line  
5. **Optional next** — fix list / Stop for OK

## Simplification heuristics (not laws)

Prefer: one write path per field; UI does not re-emit on silent refresh; pending only
when UX needs it; role branches OK; **duplicate same-intent handlers** not OK.

Do **not**: force UDF as project rule; ServerRpc pure-local UI; Steam/telemetry;
refactor unrelated while mapping.

## Pairing

| Mention | Role |
|---------|------|
| @flow-map | Canvas diagrams + chat structure notes |
| @ngo-debug | Reproduce + logs |
| @bug-hunter | Fix order |

@flow-map alone is enough when only structure is needed.
