---
name: ngo-debug
description: >-
  NGO multiplayer debug for this project — reproduce templates, Host/Client log
  checklist, authority/RPC/projectile B안 checks. Use when debugging Netcode bugs,
  ParrelSync/Build desync, or user asks for multiplayer reproduce/log format.
---

# NGO Debug

Project locks: Owner+CNT movement, projectile **B안**, in-game leave = room end, test = **ParrelSync / Build** (no LAN).  
Pair with Rule **Bug Hunter** for fix order; this skill is the **reproduce + log template**.

## When to use

- Host vs Client mismatch, RPC/NV wrong, projectile/trap desync, cheer/voice hitch with NGO
- User asks to “재현 정리” / “로그 포맷” / multiplayer debug

## Reproduce template (fill before fixing)

```
Env: [ParrelSync | Dev Build Host+Client EXE]  (not LAN)
Roles: Host = [ ]  Client = [ ]
Build/clone note: [same project? cache cleared?]
Steps:
1. …
2. …
Expected:
Actual (Host):
Actual (Client):
Frequency: [always | intermittent]
```

## Log checklist

Prefix logs with role: `[Host]` / `[Client]` / `[Owner]`.

| Check | What to look for |
|-------|------------------|
| Authority | Who runs the code path (Host / Owner / Client local) |
| RPC direction | ServerRpc vs ClientRpc; who calls |
| NV writes | Client must not write gameplay NV — Host after validate |
| Damage | Through `NetworkDamageUtil` only? |
| Projectile B안 | Prefab: no follow-NT for flight; Client flight + hit ServerRpc → Host damage |
| Session | Accidental reconnect/late-join “fix”? Forbidden |
| Leave path | `DisconnectManager` → `TitleReturnFlow` → `NetworkManagerSetup.Shutdown` |
| Solo | No NGO — DisconnectManager N/A |

## Cheer / audio (if relevant)

- Vosk: worker vs main (`AcceptWaveform` not on main)
- Mic: no double `Microphone.Start` (multi = Dissonance tap)
- Buffer: Dissonance warn vs Vosk `_pcmQueue` — main hitch vs chunk size

## Output (after investigate)

Use Bug Hunter report format:

**Reproduce:** …  
**Root cause:** …  
**Fix:** …  
**Verify:** ParrelSync and/or Build — Host + Client steps  
**Impact:** …
