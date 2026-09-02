# Game Architecture Boundaries

> Domain ownership only. Network / session / authority SSOT: `NetworkDesign.md`.  
> Cheer / voice SSOT: `CheerAndTutorialDesign.md`.  
> Co-op stage/minigame content SSOT: [`CoopStageAudit.md`](CoopStageAudit.md) (공유) · [`CoopStageAudit.M.md`](CoopStageAudit.M.md) · [`CoopStageAudit.T.md`](CoopStageAudit.T.md).

## Scope (full release)

- Play path **현재:** Title → Lobby → Tutorial → `M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`. **5스테이지 채우기 의무 없음** — 컷은 [`CoopStageAudit.md`](CoopStageAudit.md) §1.5. 시퀀스는 컷 확정 후에만 줄임. 판별 감사는 M/T 문서.
- Solo = **NGO Host 1인** (`partySize=1`) — same path as multi. No offline mode.
- **목표:** **2026-09-01 Steam 정식 출시.** 데모 / Playtest / Open·Release 이원화 **없음** (`ReleaseRoadmap.md`).
- Multiplayer: NGO 2.9 Listen-Server
- No persistent cross-run checkpoint save in MVP (`NetworkDesign` §13) — respawn via **`ColoredStartZone`**

## Domain Ownership

- **Player:** input, movement, stamina, dodge, respawn lifecycle, own state events. **No** Vosk / mic / cheer-submit.
- **Enemy:** detection / chase / attack and local combat state only.
- **Stage:** `StageObjective` + `StageManager` — stage-local win/fail. 축 SSOT: `NetworkDesign.md` §11A.
- **Spawn / respawn (MVP):** `ColoredStartZone` + `spawnPoint` (not ColorSavePoint / StageCheckpoint save pipeline).
- **Flow:** `SceneFlowManager` — scene progression (M stages → M.Boss → T stages → T.Boss → `End.Demo`); stage-local systems do not load scenes directly.
- **Damage:** `NetworkDamageUtil` — single networked damage entry (Host).
- **Cheer/Voice:** `CheerKeywordEngine` (Owner detect) + `CheerService` (Host apply). See Cheer doc.
- **Session leave (in-game):** `DisconnectManager` → `TitleReturnFlow` → `NetworkManagerSetup.Shutdown`.
- **UI:** subscribe / display only — no gameplay ownership, no mic/Vosk ownership.
- **Camera:** `ThirdPersonCamera` follow/view only — not Cinemachine-as-SSOT.
- **Telemetry:** `TelemetryService` (Host) — 출시 후 OK; see `TelemetryDesign.md`.

## Dependency Rules

- Stage-local systems do not directly load scenes (`SceneFlowManager` owns transitions).
- UI does not own gameplay state and should not poll scene objects by name.
- Do not invent parallel damage, cheer, or leave/shutdown paths.
- Do not add reconnect / late-join / host-migration designs (`NetworkDesign` §12).
- Do not add spectator (Post-Launch 후보) or cutscenes (permanently out of scope).
- Stage/minigame content changes follow [`CoopStageAudit.md`](CoopStageAudit.md) (2p floor, unique+black+white, no pad-to-5, no boss debuts) and the M/T audit docs.

## Out of scope here

- Authority matrix, projectile B안, Steam/telemetry → `NetworkDesign.md`
- Vosk worker / Dissonance mic → `CheerAndTutorialDesign.md`
- Co-op feel / player-count floor → `CoopStageAudit.md`. M stages → `CoopStageAudit.M.md`. T stages → `CoopStageAudit.T.md`
