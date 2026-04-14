# Stage1_2 Playable Pass Checklist

## Stage1
- NavMesh bake completed and agent pathing validated for all enemy spawn areas.
- At least one checkpoint path from stage start to clear objective is always reachable.
- Trap lanes have deterministic safe windows and no unavoidable instant-kill chain.
- Stage objective clear event is connected to stage-flow clear notification.

## Stage2
- Intro sequence can freeze and unfreeze player movement safely.
- Main objective can be completed after death/respawn without deadlock.
- Transition/cutscene trigger returns input control to players.
- Stage clear unlocks next stage state in stage flow.

## Enemy and Trap Balance
- Enemy attack ranges and timings are configured from Inspector, not hardcoded.
- `WindTrap` base force, duration, schedule and phase multipliers are configured per area.
- Contact damage cooldown and damage values are configured per enemy/trap archetype.

## Runtime Validation
- New game start -> Stage1 clear -> Stage2 entry works without manual scene intervention.
- Save point activation updates respawn and survives app restart.
- Respawn returns to last checkpoint in current stage.
