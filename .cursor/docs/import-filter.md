# Import Filter — Phase 1 gate

> Only items that pass this filter may be copied into `_cursor_import/`.  
> Do **not** merge into `.cursor/rules` or `.cursor/skills` until Phases 2 → 3-A approval.  
> Source of truth for process: `Assets/Docs/CursorVibeSetupBrief.md`

## Required (must eventually exist in our setup)

| Area | Need | Notes |
|------|------|--------|
| Rules pattern | Unity C# / NGO-friendly rule **structure** (`.mdc` or equivalent) | Content must be rewritten to our stack after 0.5 |
| Architecture | “No guessing / no inventing APIs” style rules | Keep pattern, not foreign class names |
| Workflow | Plan-first, diff-only, bugfix impact-check patterns | Generic OK |
| Skills pattern | Task skills with concrete checklists/templates | Prefer NGO + Unity 6 examples if any |
| Multiplayer | NGO (Netcode for GameObjects) guidance only | Reject Mirror/Photon-first kits |
| Test | Editor multi-instance / ParrelSync-friendly notes | **No LAN-required** workflows |

## Deferred (do not import now)

| Item | Why |
|------|-----|
| Steamworks / Steam Networking skills | Steam not integrated yet |
| Steam / store release checklists | Post Steam kickoff |
| Host migration / reconnect / late-join kits | Session policy under Docs review (0.5) |
| Unity Editor MCP / GitHub MCP install bundles | Phase 4 |
| Hooks | Too early; noise for solo setup |
| README/Changelog generators (optional) | Nice-to-have after core Rules/Skills |

## Forbidden (reject on sight)

| Item | Why |
|------|-----|
| Mirror / Photon / FishNet / custom relay as default stack | We use **NGO 2.9** |
| Facepunch / Steamworks.NET as “current” dependency | Not chosen; ask user |
| ECS / DOTS-first, UI Toolkit-first kits | Not our stack (uGUI) |
| Web / React / Next / Node vibe kits | Irrelevant |
| Cinemachine-as-camera-SSOT rules | We use **TopDownCamera** |
| “LAN multiplayer test required” skills | LAN broken; ParrelSync/build only |
| Giant “18 Unity skills” dump without filter | Token waste + conflicts |
| Anything that auto-writes Rules from random blog Docs | Violates Docs→Rules approval gate |
| Recycling this repo’s **deleted** old skills | Rebuild from external/new only |

## Import procedure

1. Propose candidate repos/kits in a **comparison table** (fit to NGO, Unity 6, last update, license).
2. Wait for **user approval**.
3. Copy **only** passing files into `_cursor_import/` (recommend gitignoring this folder).
4. Write `import-inventory.md` (path + 1-line summary + Required/Deferred/Forbidden verdict).
5. Stop. No merge into live `.cursor` until Phase 2.

## Pass/fail checklist (per file)

- [ ] Mentions or assumes Mirror/Photon/FishNet as primary? → **Fail**
- [ ] Requires working LAN? → **Fail**
- [ ] Forces Cinemachine as camera authority? → **Fail** or strip that section
- [ ] Unity 6 / NGO compatible or easily rewritten? → else **Fail**
- [ ] Web/frontend? → **Fail**
- [ ] Concrete template (not vague “write good code”)? → prefer **Pass** for skills
