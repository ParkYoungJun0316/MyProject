# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project identity

Solo-dev **4-player co-op survival/action game**, Steam release target **2026-09-16** (no separate demo/playtest builds — same codebase).

- **Unity 6.3 LTS (6000.3.9f1)**, C#, URP, Input System, uGUI
- Network: **Netcode for GameObjects 2.9.2**, Listen-Server (Host + Clients)
- Camera: **ThirdPersonCamera** is the camera SSOT — Cinemachine (present as a package, v3.1.5) is not treated as authority
- Voice: Dissonance (voice chat) + Vosk (offline speech-to-text, `Assets/ThirdParty/Vosk`, `Assets/StreamingAssets/vosk-model-*`) power the "Cheer" system
- `com.community.netcode.transport.facepunch` is present in `Packages/manifest.json` but **Steamworks is not integrated yet** — don't assume it's wired up; ask before proposing Facepunch/Steamworks.NET/Mirror/Photon/FishNet/Relay as the current transport

Play path: Title → Tutorial → `M.Stage1`…`M.Stage5` → `M.Boss` → `Interlude` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End` → Title (the old `Lobby` scene was folded into Tutorial). `End` is the clear-credits scene: no players are spawned there and the transition curtain does not wait for them. Solo play is **NGO Host with `partySize=1`**, same path as multiplayer — there is no offline mode.

## Commands

This is a pure Unity Editor project — there is no CLI build/lint/test pipeline, no `package.json`/`Makefile`, and no project-owned test assembly (all `*Tests*.asmdef` under `Assets/` belong to third-party packages, e.g. Dissonance/AureDevGames water shader). Build, Play Mode, and any Test Runner usage happen from inside the Unity Editor.

- Multiplayer testing is **ParrelSync (editor clone) and/or two local builds on one PC** — real LAN discovery does not currently work, so never assume LAN-based testing.
- Unity MCP tools exist in this project but are **read-only for the agent** by convention (see Editor/MCP rules below) — use them to inspect Hierarchy/console/assets when code context alone is insufficient, not to mutate scenes/prefabs/inspector state.

## Architecture

### Domain ownership (`Assets/Docs/GameArchitectureBoundaries.md` is SSOT)

Each domain owns its state; other systems only read/subscribe:

- **Player** (`Assets/Scripts/...`): input, movement, stamina, dodge, respawn lifecycle — never Vosk/mic/cheer-submit logic.
- **Enemy**: detection/chase/attack and local combat state only.
- **Stage** (`Assets/Scripts/Stage`, 59 files — largest domain): `StageObjective` + `StageManager` own stage-local win/fail.
- **Flow** (`Assets/Scripts/Flow`): `SceneFlowManager` owns all scene transitions; stage-local systems never load scenes directly.
- **Damage**: `NetworkDamageUtil` is the single networked damage entry point (Host-applied) — never build a parallel damage path.
- **Cheer/Voice** (`Assets/Scripts/Cheer`, 16 files): `CheerKeywordEngine` (Owner-side detection) + `CheerService` (Host-side apply). See `Assets/Docs/CheerAndTutorialDesign.md`.
- **Session leave (in-game)**: `DisconnectManager` → `TitleReturnFlow` → `NetworkManagerSetup.Shutdown()`. Never call `NetworkManager.Shutdown()` directly or build a parallel leave path.
- **UI** (`Assets/Scripts/UI`, 40 files): subscribe/display only — never owns gameplay state, never polls scene objects by name.
- **Spawn/respawn (MVP)**: `ColoredStartZone` + `spawnPoint`. No persistent cross-run checkpoint save yet.

### Network authority model (`Assets/Docs/NetworkDesign.md` is SSOT)

| Area | Authority |
|------|-----------|
| Movement | **Owner + `ClientNetworkTransform`** — no Host-move, no client prediction |
| HP / damage / trap schedule / game rules | **Host** |
| Projectile flight | **Client-local** ("B안": Host spawns + sets initial velocity → client self-flies it locally — this exists specifically because Host-driven flight looked choppy on clients) |
| Projectile hit → damage | Client reports via ServerRpc → Host applies via `NetworkDamageUtil` |

Sync conventions: continuous state (HP, floor color, ready) via `NetworkVariable`; one-shot events (VFX, chat, game-over) via `ClientRpc`; client→Host requests via `ServerRpc` with Host-side validation. Clients never write gameplay `NetworkVariable`s directly.

Session model is intentionally minimal: **no reconnect, no late-join, no host migration.** Anyone leaving mid-game ends the room for everyone (all players return to title). Do not design around reconnect/late-join scenarios.

### Scripts layout

`Assets/Scripts/{Audio, Cheer, Core, Flow, Lobby, Localization, Network, Settings, Stage, Tools, Traps, UI}` — Network (21 files) and Traps (22 files) are the other large domains alongside Stage/UI/Cheer above.

### Docs as SSOT

Design intent lives in `Assets/Docs/`, not in code comments. The key files, in rough order of how often they matter:

- `NetworkDesign.md` — authority matrix, projectile model, session/leave, player lifecycle axis (§11), structured logging
- `CheerAndTutorialDesign.md` — Cheer/voice/Vosk/tutorial system
- `GameArchitectureBoundaries.md` — domain ownership (summarized above)
- `CoopStageAudit.md` (+ `.M.md` / `.T.md`) — co-op feel/player-count floor and per-stage audit status; stage/minigame content changes must follow this, not reinvent genre/FFA rules
- `ReleaseRoadmap.md`, `TelemetryDesign.md`, `SteamworksIntegrationDesign.md` — later-phase scope, not yet active

## Working rules

These carry the weight of hard constraints in this repo (mirrored in `.cursor/rules/*.mdc` for Cursor; apply the same here):

- **Don't invent missing classes/APIs.** Search the repo first; ask if still unclear.
- **Offline mode does not exist.** Solo play is `OnlineHost` with `partySize=1`. Delete (don't dormant-guard) any "offline"/`isOnline == false` branch you find; `LobbyContext.IsOnline` is always `true`.
- **Scope is full release, not demo.** Don't scope-limit fixes to demo-only paths; demo and release share one codebase.
- **Don't revive rejected designs**: Host-only movement, projectile "A안" (Host-driven flight), reconnect/late-join/host-migration, spectator mode, cutscenes.
- **Scene/prefab/inspector edits are the user's**, not the agent's. Edit `.cs` files and Docs; for anything requiring Inspector wiring, scene placement, or prefab changes, describe the checklist and let the user apply it in-editor — don't patch `.unity`/`.prefab` YAML directly.
- **Don't promote Docs content into new binding rules on your own judgment** — if something in `Assets/Docs/` seems like it should become a hard rule, flag it and let the user confirm, mirroring how `.cursor/rules` are gated.

### Absolute rules (imported from FortDefense, 2026-10-08)

1. **No edits without approval** — `.cs`, Docs, and MCP alike. Report analysis/plan and stop.
2. **Approval words are explicit instructions only**: "수정해 / 고쳐 / 진행해 / 만들어 / 적용해". **"좋아 / 그래 / 알겠어 / ㅇㅇ" are not approval** — ask again.
3. **One path (SSOT).** Before a new script or branch, read `GameArchitectureBoundaries.md`, grep all of `Assets/`, list **every** related script, and report reuse/extend/new and why before writing. → `design-check` skill. For Survival work, weigh shipped-story-code impact per `SurvivalDesign.md` §4.3.
4. **No duplicate calls.** Never "call it once more in case it didn't run" — trace the call path to find the break. Guards (safe if called twice) are fine.
5. **No bug confirmed by inference.** Hypotheses OK. If code can't confirm it, add `[DBG]` logs, confirm via console, explain, get approval, then fix. → `bug-fix` skill

### Enforced by hooks (`.claude/hooks`)

- Editing scenes/prefabs/assets/ProjectSettings/Packages (Edit/Write or shell writes) is blocked — give an "object → component → value" checklist instead. MCP edits only when the user says so in that conversation (`mcp-edit` skill).
- Hard-to-undo git (push, hard reset, restore, `checkout --`, clean, rebase, amend, rm/mv) is blocked — the user runs those. Commit only when the user says "커밋해"; after each fix, recommend a commit (one fix = one commit).
- Scene-save noise (UI.prefab RectTransform overrides) is checked by Claude and removed by the user: before recommending a commit with `.unity` changes, follow the `scene-noise` skill (`Tools/SceneNoise/strip-scene-noise.js`).

### Conversation

- Korean. **Plain 4 lines first** (what / how serious / what changes / how to verify), details after. Explain as flow (who → whom → order), not code detail.
- Side issues found along the way: one line, record only. Don't widen scope.
- Tuning questions: rough multiplier estimate first; simulations only after approval. → `rough-estimate` skill
- Unity MCP is **read-only** by default; report editor state as works/doesn't. → `editor-verify` skill
- No compiling edits while the user is in Play Mode. "Works in editor" ≠ "works" — recommend ParrelSync/two-build and Steam build checks early.
