# Downed & Revive System Design

다운/부활 시스템 SSOT. 도메인 소유권은 `GameArchitectureBoundaries.md`를 따르고, 네트워크 권위·동기화 상세는 9절. `NetworkDesign.md`의 권위 매트릭스(§9.0)에는 아직 별도 행으로 편입 전 — 이 문서가 1차 SSOT. Cheer 상호작용은 `CheerAndTutorialDesign.md` 범위와 겹치는 부분만 5절에서 다룬다.

---

## 1. 배경 / 목적

- **문제:** 팀원 1명이 죽으면 스테이지 전체가 처음부터 리셋됨. 특히 한 씬에 stage가 여러 개 있는 경우(예: 일부 M/T 씬) 스트레스가 큼 — 캐주얼/즐기기 목적 플레이어층에서 체감 난이도가 최상급으로 뜀.
- **기각한 대안:** "목숨 N회 소모 후 리셋" 방식 — SurviveObjective류에서 "3명 다운 + 1명 캐리로 클리어" 같은 협동 설계 의도 붕괴 우려, 그리고 전진형(ReachZone) 스테이지의 리스폰 위치를 스테이지별로 새로 설계해야 하는 콘텐츠 비용 문제로 제외.
- **채택안:** 다운(down) 상태 + 팀원 부활(revive) 구조. 죽음 즉시 리셋이 아니라, 일정 시간 내 부활 가능한 유예를 준다. 단 협동 난이도의 핵심 긴장감(팀 전체 생존)은 유지한다.

## 2. 판정 / 트리거

| 상황 | 결과 |
|------|------|
| HP 0 도달 | **부활 가능자 판정** 후 분기 (아래 두 행) |
| └ 부활 가능자 있음 (나 외에 다운·사망이 아닌 플레이어 1명 이상) | **다운** 상태 진입, 10초 카운트다운 |
| └ 부활 가능자 없음 (솔로, 또는 마지막 생존자의 다운) | 다운 **스킵** → 즉시 완전사망 → 씬 리로드 |
| 즉사 판정 (보울더, 낙사, 스테이지 실패 등 인스턴트킬 소스) | 다운 **스킵** → 즉시 스테이지 실패 → 씬 리로드. **이미 다운 중인 인원에게도 적용**(진행 중 부활은 정리) |
| 개인 완전사망 (10초 방치 만료, §3) | 즉시 스테이지 실패 |
| 스테이지 내 개인 다운 횟수 | **무제한** |

즉사 소스가 이미 많아 다운 시스템으로 완화되는 체감 난이도가 과하지 않을 것으로 판단(2026-09 논의).

**부활 가능자 판정(2026-09-14 확정):** 예전 "4인 전원 동시 다운은 별도 판정 없이 첫 다운자의 10초 만료로 귀결" 규칙을 대체한다. 살릴 사람이 없는데 10초를 기다리게 하는 건 의미 없는 대기라서, 다운 진입 시점에 Host가 판정해 즉시 리로드한다. 부활 가능자가 사라지는 경로는 사실상 "다른 사람의 다운"뿐이다(즉사는 어차피 리로드, 이탈은 방 종료). 그래서 다운 진입 시점 1회 판정으로 충분하고, 마지막 생존자가 다운되는 순간 그 판정에서 리로드로 귀결된다. 시전 중이던 시전자가 피격으로 다운되는 경우도 캔슬이 먼저 처리된 뒤 이 판정을 탄다.

**T.Stage2 완화(2026-09-14):** `MemoryPathTile`/`ColoredMemoryPathTile`/`PioneerPathTile`의 트랩·오답 타일은 기존 `ApplyInstantKill` → **데미지**(`NetworkDamageUtil.ApplyDamage`, 기본 6 — 매니저 Inspector `MemoryPath.trapDamage` / `ColoredMemoryPath.wrongTileDamage` / `PioneerPathManager.trapDamage`로 조정)로 전환. 더 이상 즉사 소스가 아니며 위 표의 일반 HP 데미지 경로(다운 진입 가능)를 그대로 탄다. 타일은 1회성이 아니라 닿을 때마다 데미지를 주고, 연속 피격은 `NetworkPlayerSetup`의 서버 피격 무적 타이머가 막는다. `MemoryPath`/`ColoredMemoryPath`가 트랩 판정 시 자체적으로 갖고 있던 `PathState.Failed`/`OnFailed`(모든 씬에서 미배선 확인 후 제거)도 함께 삭제 — 안 그러면 데미지만 받고 죽지 않아도 해당 구간 미니게임이 영구 Failed 상태로 굳어 세이프 타일 진행이 막히는 소프트락이 생김.

## 3. 방치 / 완전사망

- 다운 후 **10초** 방치 시 완전사망.
- 부활 시전이 시작되면 이 10초 타이머는 **정지**한다 (시전 완료 전 타이머가 먼저 끝나는 경쟁 상태 방지).
- 완전사망 시: `STAGE FAILED` 배너 단발 표시(2초, 기존 `STAGE CLEAR` 연출과 대칭) → 씬 리로드.

## 4. 부활

| 항목 | 규칙 |
|------|------|
| 상호작용 | 다운된 대상 근접 + 버튼 홀드 **2초** |
| 구현(2026-09-14) | `PlayerReviveInteract`(Player 도메인) — 기존 `Interact`(E) 액션 버튼 press 시점에 근접한 다운 팀원을 찾아 `RequestStartRevive` 1회 전송. "홀드 2초"는 클라이언트가 재는 게 아니라 Host의 2초 내부 타이머(§9.2)가 유일한 판정 — 클라이언트는 누른 순간의 근접 여부만 로컬로 거르는 얇은 어댑터이고, Host가 거리·다운 여부를 다시 검증한다. `Interact` 액션 자체에 Input System Hold 인터랙션이 걸려 있어 버튼을 실제로 누르고 있어야 하는 최소 시간(기본 ~0.4초)이 있다 — 원치 않으면 Input Actions 에디터에서 제거(에디터 작업, 미적용) |
| 동시 시전 | **1:1만.** 여러 명이 한 대상을 동시에 시전해도 단축 없음 |
| 셀프 리바이브 | **없음** |
| 시전 중 이동 | 불가 |
| 캔슬 조건 | 시전자가 **실제 HP 감소**를 겪으면 진행도 **0으로 리셋**. 쉴드 등으로 데미지가 완전히 무효화(HP 불변)되면 캔슬되지 않음 — 캔슬 판정은 "피격"이 아니라 "HP 감소 발생" 이벤트에 건다 |
| 부활 직후 HP | 최대 5칸 기준 **3칸**(= -2칸 상태)으로 복귀 |
| 그레이스 피리어드 | 부활 직후 **1초 무적** |

## 5. 다운 중 상태

- **피격 무적.** 별도 물리/논리 레이어(예: `PlayerDown`)로 분리해 적 AI의 감지·타겟팅 대상에서 제외.
- 적 AI는 다운된 플레이어를 감지/추적/공격하지 않는다 (Enemy 도메인 규칙 갱신 필요, 7절 참고).
- 애니메이션은 기존 die 모션 재사용. 레이어 분리로 처리(신규 애니메이션 불필요).
- **콜라이더는 유지** — 물리적으로 다른 플레이어/적의 이동을 막는 오브젝트로 남는다. 좁은 구간에서 구조 이동을 방해하는 것은 의도된 긴장 요소.
- 입력 차단: 이동, 색전환, 공격, 이모트, 오브젝트 상호작용 **전부 차단**.
- 카메라: `ThirdPersonCamera`(SSOT) 그대로, 제자리에서 회전(주변 둘러보기)만 허용. 줌 없음. **관전 모드 아님** — 캐릭터 위치 고정, 시점 이동/타 플레이어 시점 전환 없음 (`GameArchitectureBoundaries.md` 스펙터 모드 out-of-scope 원칙 준수).
- Cheer: 다운 중에도 **TeamCheer만** 유효 발동. 개인 버프류 Cheer는 다운 중 미적용.
- Dissonance 음성 채팅은 계속 유지(별도 시스템, 제한 없음).

## 6. UI / 연출

| 이벤트 | 연출 |
|--------|------|
| 다운 진입 | `BERRY DOWN` 배너 (기존 연출 재사용). 여러 명이 순차로 다운되면 각각 순서대로 표시 가능 |
| 완전사망(스테이지 실패) | `STAGE FAILED` 단발 배너 2초 → 씬 리로드 |
| 부활 진행 중 | 게이지 대신 **파티클 효과**로 표시 (시전 2초로 짧아 숫자 게이지 불필요 판단). 캔슬 시 파티클이 즉시 끊기는 등 실패를 구분할 수 있는 피드백 필요(세부 미정, 8절) |
| 팀 상태 | `TeamStatusUI` 확장 — 누가 다운 중인지 + 남은 방치 시간 표시 |

## 7. Objective 타입별 영향

- 별도 임계값 없음. 트리거는 2절의 "개인 완전사망" 하나로 통일.
- SurviveObjective 등 특정 objective 타입에 대한 예외 규칙 불필요 — "누군가 완전사망하면 그 즉시 실패"가 모든 objective에 동일 적용되므로 "N명 다운까지 허용" 같은 별도 로직을 얹지 않는다.
- ReachZone류(전진형)는 구조상 전원 도달이 필요해 다운 시 자연히 부활을 강제하게 됨 — 별도 처리 불필요.

## 8. 미결정 / 다음 단계

- ~~Cheer 개인 버프 삭제 여부~~ → **확정(2026-09-14): 개인 버프 삭제, TeamCheer만 유지.** Cheer/Voice 도메인 문서 반영은 `CheerAndTutorialDesign.md` 쪽에서.
- ~~다운 상태 물리 레이어~~ → **확정(2026-09-14): 기존 `PlayerDead` 레이어 재사용.** 적 타겟팅 제외 판정(Enemy 도메인)과 Collision Matrix 세부 검증은 후속 작업.
- 부활 파티클/캔슬 SFX 등 세부 피드백 디자인.

## 9. 네트워크 동기화

**전제:** 기존 `TutorialGatherZone` 게이트 카운트다운(`NetworkDesign.md` §6B.3)과 동일한 패턴 재사용 — "절대 시각 NV + Host 단일 레인 판정 + 클라이언트 로컬 계산 + 1회성 이벤트는 ClientRpc". 새 동기화 패턴 도입 아님.

### 9.1 상태 (각 플레이어 자신의 NetworkObject 소속, 가칭 `PlayerDownState` 컴포넌트 — Player 도메인)

| 필드 | 타입 | 쓰기 권한 | 용도 |
|------|------|-----------|------|
| `IsDowned` | `NetworkVariable<bool>` | Host | 다운 여부 (지속 상태) |
| `DownDeadlineServerTime` | `NetworkVariable<double>` | Host | 완전사망까지 남은 시간 계산용 절대 시각 |
| `IsBeingRevived` | `NetworkVariable<bool>` | Host | 부활 시전 중 여부 (방치 타이머 일시정지 판단) |
| `ReviverClientId` | `NetworkVariable<ulong>` | Host | 현재 시전자 (1:1 강제, 중복 시전 방지) |

### 9.2 흐름

| 단계 | 트리거 | Host 처리 | 전파 |
|------|--------|-----------|------|
| 다운 진입 | `NetworkDamageUtil` → `NetworkPlayerSetup.ApplyDamageFromServer`에서 HP 0 판정(즉사 아님) | 부활 가능자 판정(§2). 없으면 즉시 완전사망. 있으면 `IsDowned=true`, `DownDeadlineServerTime = now+10` | NV 자동 전파 + `PlayerDownedClientRpc` (BERRY DOWN 배너 트리거) |
| 부활 요청 | 시전자 클라이언트 → `RequestStartReviveServerRpc(downedPlayerId)` | 검증(거리 / 대상이 실제 다운 상태인지 / 이미 다른 시전자가 있는지 / 요청자 본인이 다운 상태가 아닌지) 통과 시 `IsBeingRevived=true`, `ReviverClientId=요청자`, 남은 시간 내부 보관 후 2초 내부 타이머 시작 | NV 자동 전파 |
| 캔슬 | 시전자가 실제 HP 감소 이벤트 발생 | `IsBeingRevived=false`, 보관해둔 남은 시간으로 `DownDeadlineServerTime` 복원 | NV 전파 + `ReviveCancelledClientRpc` (파티클 중단 연출) |
| 완료 | 캔슬 없이 2초 경과 | `IsDowned=false`, HP 3칸 적용, 1초 무적 부여 | NV 전파 + `ReviveCompletedClientRpc` |
| 완전사망 | Host `Update()`에서 매 프레임 `ServerTime >= DownDeadlineServerTime && IsDowned && !IsBeingRevived` 체크 (게이트 `UpdateGate()`와 동일한 Host 단일 레인 방식) | `StageManager`에 실패 직접 통보(같은 Host 프로세스, RPC 불필요) | `StageFailedClientRpc` (배너 2초 → 씬 리로드) |

### 9.3 이동 잠금 원칙

다운 중 이동 불가 / 부활 시전 중 이동 불가는 **Host가 강제로 위치를 묶지 않는다.** 이동 권한은 Owner+`ClientNetworkTransform`(No Host-move 원칙, `NetworkDesign.md` §9.0 매트릭스)이므로, 각 클라이언트가 자신의 `IsDowned` 또는 "내가 현재 리바이버로 시전 중"이라는 로컬 판단으로 **자기 입력을 스스로 차단**한다. 다이얼로그 등 기존 입력 락 패턴과 동일 원칙.

"내가 현재 리바이버로 시전 중"은 별도 NV 없이 판단한다. 스폰된 모든 `PlayerDownState` 중 `IsBeingRevived && ReviverClientId == 내 OwnerClientId`인 대상이 있는지를 전 머신에서 동일하게 계산한다(`PlayerDownState.IsRevivingOther`). 시전 중에는 수평 속도만 0으로 두고 중력은 유지하며, 입력값은 계속 받아 시전이 끝나면 누르던 방향으로 바로 이어진다.

**구현 현황(2026-09-14):** 완전사망은 아직 `StageManager` 직접 통보 + `StageFailedClientRpc` 배너가 아니다. 기존 사망 파이프라인(`NetworkPlayerSetup.FinalizeDownDeath` → `ForceKillClientRpc` → `RaiseDied` → `StageResetOnPlayerDeath`, 배너 없이 즉시 리로드)을 임시로 재사용 중이며, 위 표의 배너 흐름은 Stage/UI 작업 때 교체한다.

### 9.4 검증 항목 (Host-side, `RequestStartReviveServerRpc`)

- 요청자-대상 간 거리(근접 상호작용 범위 내)
- 대상이 실제로 `IsDowned == true`
- 대상이 이미 `IsBeingRevived == true`(다른 시전자 존재)가 아님
- 요청자 본인이 `IsDowned == true`가 아님(다운된 사람은 시전 불가)
- 요청자가 이미 다른 대상을 시전 중이 아님(시전자 측 1:1 — 이게 없으면 두 명을 동시에 시전해 캔슬 추적이 한쪽만 남는다)
- 중복 수신 방어: `RpcSubmitDedup` 적용(캔슬 이후 다른 틱에 도착한 중복 요청이 입력 없이 시전을 재시작하는 것을 막음)

## 10. 관련 도메인 (변경 필요 지점)

- **Player:** 다운 라이프사이클 소유 후보 (`GameArchitectureBoundaries.md`의 "respawn lifecycle" 범위 확장).
- **Damage:** `NetworkDamageUtil`에 다운 상태 전환 로직 통합 (Host-applied 원칙 유지).
- **Enemy:** 다운 상태 플레이어를 감지/타겟팅 대상에서 제외하는 판정 추가.
- **Stage:** `StageObjective`/`StageManager`가 개인 완전사망 이벤트를 구독해 실패 처리.
- **UI:** `TeamStatusUI`, `PlayerHPUI` 확장.
