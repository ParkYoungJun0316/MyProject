# Post-MVP Multiplayer Backlog

## Implement First
- Replace direct `Player` damage entry with network-ready command path.
- Move authority checks into `IPlayerContext` and ownership service.
- Keep `IDamageReceiver` as common contract for player and enemy.

## Data and Sync
- Convert stage flow save payload to support multiple player progress slots.
- Add deterministic checkpoint ownership and team-wide checkpoint policy.
- Separate local-only UI events from replicated gameplay events.

## Runtime
- Introduce player spawn service that supports host/client join order.
- Split local input reading from character simulation execution.
- Add replay-safe stage clear and checkpoint events for late join handling.
