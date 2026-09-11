# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project identity

Solo-dev **4-player co-op survival/action game**, Steam release target **2026-09-16** (no separate demo/playtest builds — same codebase).

- **Unity 6.3 LTS (6000.3.9f1)**, C#, URP, Input System, uGUI
- Network: **Netcode for GameObjects 2.9.2**, Listen-Server (Host + Clients)
- Camera: **ThirdPersonCamera** is the camera SSOT — Cinemachine (present as a package, v3.1.5) is not treated as authority
- Voice: Dissonance (voice chat) + Vosk (offline speech-to-text, `Assets/ThirdParty/Vosk`, `Assets/StreamingAssets/vosk-model-*`) power the "Cheer" system
- `com.community.netcode.transport.facepunch` is present in `Packages/manifest.json` but **Steamworks is not integrated yet** — don't assume it's wired up; ask before proposing Facepunch/Steamworks.NET/Mirror/Photon/FishNet/Relay as the current transport

Play path: Title → Lobby → Tutorial → `M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`. Solo play is **NGO Host with `partySize=1`**, same path as multiplayer — there is no offline mode.

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
