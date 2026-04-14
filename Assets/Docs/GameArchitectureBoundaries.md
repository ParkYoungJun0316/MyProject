# Game Architecture Boundaries

## Scope
- Vertical slice target: `Stage1 -> SavePoint -> Stage2 -> Transition`
- Multiplayer implementation is postponed, but interfaces and ownership boundaries are prepared now.

## Domain Ownership
- `Player`: input, movement, stamina, dodge, respawn lifecycle, own state events.
- `Enemy`: detection/chase/attack and local combat state only.
- `StageObjective` + `StageManager`: stage-local win/fail conditions.
- `ColorSavePoint` + `ColorSaveZone`: checkpoint activation from zone occupancy.
- `StageFlowManager` (new): cross-stage progression, scene loading, global stage state, save/load orchestration.
- `StageSaveService` (new): persistent save I/O (JSON), no scene logic.
- `StageCheckpoint` (new): binds in-scene checkpoint trigger to stage-flow save API.
- `UI`: subscribes to player/stage events and displays state only.
- `TopDownCamera`/Cinemachine rigs: camera follow/view only, no gameplay ownership.

## Dependency Rules
- Stage-local systems do not directly load scenes.
- Save I/O is isolated from gameplay logic.
- UI does not own gameplay state and should not poll scene objects by name.
- Checkpoint activation reports to stage flow through a dedicated API.
- Hardcoded stage progress flags are forbidden; use enum state and data objects.

## Stage Progress State
- `Locked`: cannot be entered from flow.
- `Unlocked`: selectable/enterable.
- `Cleared`: objective completed once.

## Persistence Contract
- Save payload stores:
  - current stage id
  - last checkpoint id
  - stage clear states
  - total play time
- Save writes occur on:
  - checkpoint activation
  - stage clear
- Save restore occurs at boot and before first playable scene starts.

## Multiplayer Preparation (Post-MVP hooks)
- Damage contract is interface-ready (`IDamageReceiver`).
- Player identity/context is interface-ready (`IPlayerContext`).
- Stage flow does not assume exactly one human player in save schema.
