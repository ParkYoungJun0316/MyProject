# Game Architecture Boundaries

> Domain ownership only. Network / session / authority SSOT: `NetworkDesign.md`.  
> Cheer / voice SSOT: `CheerAndTutorialDesign.md`.

## Scope (current demo)

- Play path: **Title → Lobby → `M.Stage1` → `T.Stage1` → `End.Demo`** (solo skips Lobby NGO)
- Multiplayer is **in progress** (NGO 2.9) — not “postponed”
- No persistent cross-run checkpoint save in MVP (`NetworkDesign` §13) — respawn via **`ColoredStartZone`**

## Domain Ownership

- **Player:** input, movement, stamina, dodge, respawn lifecycle, own state events. **No** Vosk / mic / cheer-submit.
- **Enemy:** detection / chase / attack and local combat state only.
- **Stage:** `StageObjective` + `StageManager` — stage-local win/fail.
- **Spawn / respawn (MVP):** `ColoredStartZone` + `spawnPoint` (not ColorSavePoint / StageCheckpoint save pipeline).
- **Flow:** `SceneFlowManager` — scene progression (`M` → `T` → `End.Demo`); stage-local systems do not load scenes directly.
- **Damage:** `NetworkDamageUtil` — single networked damage entry (Host).
- **Cheer/Voice:** `CheerKeywordEngine` (Owner detect) + `CheerService` (Host apply). See Cheer doc.
- **Session leave (in-game):** `DisconnectManager` → `TitleReturnFlow` → `NetworkManagerSetup.Shutdown`.
- **UI:** subscribe / display only — no gameplay ownership, no mic/Vosk ownership.
- **Camera:** `TopDownCamera` follow/view only — not Cinemachine-as-SSOT.

## Dependency Rules

- Stage-local systems do not directly load scenes (`SceneFlowManager` owns transitions).
- UI does not own gameplay state and should not poll scene objects by name.
- Do not invent parallel damage, cheer, or leave/shutdown paths.
- Do not add reconnect / late-join / host-migration designs (`NetworkDesign` §12).

## Out of scope here

- Authority matrix, projectile B안, Steam/telemetry → `NetworkDesign.md`
- Vosk worker / Dissonance mic → `CheerAndTutorialDesign.md`
