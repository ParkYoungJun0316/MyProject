# QA Release Checklist

## Progression
- New game boot loads first stage defined by stage flow config.
- Stage clear marks stage as `Cleared` and unlocks next stage.
- Restarting game resumes from saved stage state.

## Save and Respawn
- Checkpoint trigger saves checkpoint id.
- Player respawn point updates when checkpoint is activated.
- Re-entering stage restores spawn from last saved checkpoint id.

## Combat and Traps
- Enemy A/B/C attack timing and range feel correct after inspector tuning.
- Enemy contact damage respects cooldown.
- Wind trap schedule, force, and duration align with design intent.

## UI
- Objective panel updates on each objective event.
- Dodge cooldown icon and timer are synchronized to player action cooldown.
- Buff panel shows active buffs and countdown values correctly.
- Stage flow status text reacts to stage load/clear/checkpoint save events.

## Performance Smoke
- No avoidable per-frame object search in newly added systems.
- Scene transition and respawn occur without frame spikes or stuck agents.
