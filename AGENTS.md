# AGENTS.md — Project identity (AI entry)

> Slim SSOT for “what this repo is”, first-pass bans, and **approved locks**.  
> Full design: `Assets/Docs/` (esp. `NetworkDesign.md`, `CheerAndTutorialDesign.md`).  
> Setup roadmap: `Assets/Docs/CursorVibeSetupBrief.md`

## Identity

- Solo-dev **4-player co-op** survival/action game, **Steam 정식 출시 목표 2026-09-01** (데모/Playtest 없음)
- **Unity 6.3 LTS (6000.3.9f1)**, C#, URP, Input System, **uGUI**
- Network: **Netcode for GameObjects 2.9**, Listen-Server (Host + Clients)
- Camera: **ThirdPersonCamera** (do not treat Cinemachine as camera source of truth)

## Approved locks (Docs 확정 — Rules에도 동일)

- **Movement:** Owner + `ClientNetworkTransform` (Host-move / client prediction **폐기**)
- **Projectiles:** **B안** — Host spawn + initial velocity → Client local flight → Client hit report ServerRpc → Host damage
- **Session:** No reconnect / late-join / host migration. Anyone leaves in-game → **room ends** (all to title)
- **Test:** **ParrelSync** and/or **Build + Editor/Build** on one PC — **no LAN / discovery** testing
- **Co-op content:** 1–4 variable, design floor = **2p**, audit stages (not reconceive / not FFA) — `Assets/Docs/CoopStageAudit.md` (공유) · `CoopStageAudit.M.md` · `CoopStageAudit.T.md`

## Test environment

- **LAN discovery / real LAN does not work** right now
- Default on **one PC**: **ParrelSync** and/or **Build + Editor/Build**
- Do not assume LAN multiplayer testing

## First-pass bans (ask before proposing)

- Do not treat as current stack: Facepunch.Steamworks, Steamworks.NET, Mirror, Photon, FishNet, Unity Relay  
  (Steamworks not integrated; Steam Networking transport is a later plan only)
- Do not invent missing classes/APIs — search the repo or ask
- Do **not** auto-apply unapproved `Assets/Docs/*` into `.cursor/rules` or skills — **user approval required**
- Do not recycle deleted old project skills/rules — rebuild from approved imports / new drafts only
- Do not revive projectile **A안** or Host-only movement

## Cursor setup status

| Item | Status |
|------|--------|
| Docs (Network / Cheer) | Approved locks above |
| Old project skills/rules | Deleted (rebuild) |
| `.cursor/rules` | **7 rules present** (4 always / 3 on-demand) — Unity MCP read-only lock included; `refactor-phase` removed |
| `.cursor/skills` | **ngo-debug**, **lifecycle-fix**, **flow-map** |
| Import filter | `.cursor/docs/import-filter.md` |
| Customization spec | Phase **3-A** when customizing imports |

## Where details go

| Topic | Location |
|-------|----------|
| Authority, projectiles, session, traps, lobby, 관측성(구조화 로그) | `Assets/Docs/NetworkDesign.md` |
| 출시 일정·범위·QA (9/1 정식만) | `Assets/Docs/ReleaseRoadmap.md` |
| 텔레메트리 MVP 스펙 | `Assets/Docs/TelemetryDesign.md` |
| Cheer / voice / Vosk / tutorial | `Assets/Docs/CheerAndTutorialDesign.md` |
| 협동 체감·인원·2인 테스트 | `Assets/Docs/CoopStageAudit.md` |
| 입 스테이지 감사 (M) | `Assets/Docs/CoopStageAudit.M.md` — **다음 핸드오프 = §H.5** (입·침·혀 됨. 다음 = Barrier M1 · ColorTile 점수제) |
| 식도 스테이지 감사 (T) | `Assets/Docs/CoopStageAudit.T.md` |
| Domain boundaries | `Assets/Docs/GameArchitectureBoundaries.md` |
| Cursor AI rebuild process | `Assets/Docs/CursorVibeSetupBrief.md` |
