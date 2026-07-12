# AGENTS.md — Project identity (AI entry)

> Slim SSOT for “what this repo is” and first-pass bans.  
> Design details (authority, projectiles, session, etc.) live in `Assets/Docs/` and are locked into Rules only after **Phase 0.5** user approval.  
> Setup roadmap: `Assets/Docs/CursorVibeSetupBrief.md`

## Identity

- Solo-dev **4-player co-op** survival/action game, **Steam** release goal
- **Unity 6.3 LTS (6000.3.9f1)**, C#, URP, Input System, **uGUI**
- Network: **Netcode for GameObjects 2.9**, Listen-Server (Host + Clients)
- Camera: **TopDownCamera** (do not treat Cinemachine as camera source of truth)

## Test environment

- **LAN discovery / real LAN does not work** right now
- Default on **one PC**: **ParrelSync** and/or **Build + Editor/Build**
- Do not assume LAN multiplayer testing

## First-pass bans (ask before proposing)

- Do not treat as current stack: Facepunch.Steamworks, Steamworks.NET, Mirror, Photon, FishNet, Unity Relay  
  (Steamworks not integrated; Steam Networking transport is a later plan only)
- Do not invent missing classes/APIs — search the repo or ask
- Do **not** auto-apply `Assets/Docs/*` into `.cursor/rules` or skills — **user approval required** (Phase 0.5+)
- Do not recycle deleted old project skills/rules — rebuild from approved imports only

## Cursor setup status

| Item | Status |
|------|--------|
| Old project skills/rules | Deleted (rebuild) |
| `.cursor/rules` / `.cursor/skills` | Not created yet (Phase 3-B) |
| Import filter | `.cursor/docs/import-filter.md` |
| Docs review | Phase **0.5** → `.cursor/docs/docs-review.md` |
| Customization spec | Phase **3-A** → `.cursor/docs/customization-spec.md` |

## Where details go

| Topic | Location |
|-------|----------|
| Authority, projectiles, session, reconnect, traps, lobby, … | `Assets/Docs/` (esp. `NetworkDesign.md` and related) + Phase **0.5** review |
| How we rebuild Cursor AI config | `Assets/Docs/CursorVibeSetupBrief.md` |
| What we may import from outside | `.cursor/docs/import-filter.md` |

## Next

**Phase 0.5 — Docs review:** compare Docs vs code vs brief; write `docs-review.md`; user approves sentences before any Multiplayer Rule.
