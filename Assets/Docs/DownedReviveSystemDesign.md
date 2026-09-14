# Downed & Revive System Design

다운/부활 시스템 SSOT. 도메인 소유권은 `GameArchitectureBoundaries.md`를 따르고, 네트워크 권위·동기화 상세는 9절. `NetworkDesign.md`의 권위 매트릭스(§9.0)에는 요약 1행만 편입 — 상세는 이 문서가 1차 SSOT. Cheer 상호작용은 `CheerAndTutorialDesign.md` 범위와 겹치는 부분만 5절에서 다룬다.

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
| 상호작용 | 다운된 대상 근접 + **E만** 누른 채 **2초** 유지 |
| 구현(2026-09-14) | `PlayerReviveInteract`(Player 도메인) — 기존 `Interact`(E) 액션 press 시점에 근접한 다운 팀원을 찾아 `RequestStartRevive` 1회 전송. 2초 완료 판정은 Host(§9.2)가 유일 — 클라이언트는 누른 순간의 근접 여부만 로컬로 거르는 얇은 어댑터이고, Host가 거리·다운 여부를 다시 검증한다. `InputSystem_Actions` Interact의 Hold 인터랙션은 제거됨(2026-09-14) — E 누름 즉시 시전 요청. |
| 동시 시전 | **1:1만.** 여러 명이 한 대상을 동시에 시전해도 단축 없음. 한 시전자가 두 대상을 동시에 시전하는 것도 불가 |
| 셀프 리바이브 | **없음** |
| 시전 중 이동·외부 영향 | **잠그지 않는다.** 시전자는 평소처럼 움직이고 넉백·바람 등 외부 힘도 그대로 받는다 — 대신 아래 캔슬 조건에 걸린다(2026-09-14, 구 "이동 불가" 규칙 대체) |
| 캔슬 조건 | 아래 중 하나라도 발생하면 진행도 **0으로 리셋**, 방치 타이머 재개 (2026-09-14 확정) |
| └ 위치 밀림 (Host 판정) | 시전 시작 위치에서 **허용 거리(기본 0.3m) 이상** 벗어남 — 걷기·넉백·바람·침 미끄러짐·움직이는 발판 등 원인 불문. 사거리 이탈도 포함 |
| └ HP 감소 (Host 판정) | 시전자가 **실제 HP 감소**를 겪음. 쉴드 등으로 HP 불변이면 캔슬 아님 — "피격"이 아니라 "HP 감소 발생"에 건다. "환경 티끌이라도 부딪히면 캔슬"은 충돌 자체가 아니라 **위치 밀림 또는 HP 감소**로 정의(바닥·다운된 대상·옆 팀원과의 상시 접촉 때문에 충돌 자체는 판정 불가) |
| └ 입력 (Owner 신고) | **E 해제**, 또는 E 외의 **키보드 키·마우스 클릭·패드 버튼** 입력(채팅·ESC·마이크 단축키 포함). **마우스 이동(카메라 회전)은 허용.** Host는 키 입력을 볼 수 없으므로 Owner가 `RequestCancelReviveServerRpc`로 신고 |
| 부활 직후 HP | **고정 3칸** (`maxHeart`는 `Player.cs` 단일 값으로 전 스테이지 공통 — 최대 5칸 기준 -2칸 상태) |
| 그레이스 피리어드 | 부활 직후 **1초 무적** |

## 5. 다운 중 상태

- **피격 무적.** `PlayerDead` 레이어로 전환해 적 AI의 감지·타겟팅 대상에서 제외. HP는 0으로 고정돼 일반 데미지는 `ApplyDamageFromServer`의 HP 0 가드에 막힌다(즉사는 §2대로 적용).
- 적 AI는 다운된 플레이어를 감지/추적/공격하지 않는다 — 구현은 §10 Enemy 항목.
- 애니메이션은 기존 die 모션 재사용. 레이어 분리로 처리(신규 애니메이션 불필요).
- **콜라이더는 유지** — 물리적으로 다른 플레이어/적의 이동을 막는 오브젝트로 남는다. 좁은 구간에서 구조 이동을 방해하는 것은 의도된 긴장 요소.
- 입력 차단: 이동, 색전환, 공격, 이모트, 오브젝트 상호작용 **전부 차단**.
- 카메라: `ThirdPersonCamera`(SSOT) 그대로, 제자리에서 회전(주변 둘러보기)만 허용. 줌 없음. **관전 모드 아님** — 캐릭터 위치 고정, 시점 이동/타 플레이어 시점 전환 없음 (`GameArchitectureBoundaries.md` 스펙터 모드 out-of-scope 원칙 준수).
- Cheer: 다운 중에도 **TeamCheer만** 유효 발동. 개인 버프류 Cheer는 다운 중 미적용.
- Dissonance 음성 채팅은 계속 유지(별도 시스템, 제한 없음).

## 6. UI / 연출

| 이벤트 | 연출 |
|--------|------|
| 다운 진입 (팀원) | 화면 중앙 배너 **없음**(2026-09-14 변경 — 한때 `DeathOverlayUI`가 `OnDowned`를 구독해 "{CheerName} 다운" 배너를 띄웠으나 제거. `DeathOverlayUI`는 완전사망 `OnDied` 전용). 팀원 다운은 아래 "팀 상태" 행의 `TeamStatusUI`로만 표시 |
| 다운 진입 (본인) | `LocalDownOverlayUI`(2026-09-14) — 로컬 Owner의 `OnDowned`/`OnRevived`/`OnDied` 구독, 네트워크 쓰기 없음. ① **회색 막**: 전용 `ScreenFader` 인스턴스(MouthController용과 공유 금지)를 `SetProgress(1 − RemainingDownTime / DownTimeoutDuration)`로 매 프레임 구동 — 고정 코루틴이 아니어야 부활 시전 중 정지·캔슬 복원과 어긋나지 않음. 완전한 흑백(포스트프로세싱)이 아니라 회색 반투명 막이며, 사망 연출(고유색)과 구분하려고 색을 쓰지 않음. ② **정수 초 타이머** ③ **안내 문구**(흰색): `DeathUI/Down.Guide` "쓰러졌습니다! 팀원이 곁에서 [E]를 누르면 부활합니다." — `[E]`는 키보드 표기라 전 언어 영문 고정. 부활 시전 중엔 `DeathUI/Down.Reviving` "부활 중..."으로 교체. 부활 시 막·문구 페이드아웃, 완전사망 시 문구만 즉시 숨기고 막은 리로드까지 유지. `TeamStatusUI`는 자기 슬롯을 숨기므로 본인 표시를 대신하지 않는다. **문구 추가·수정 후 `Tools/Font/Noto Static 베이킹 - 실행` 필수**(Static 폰트는 테이블에 없던 글자를 렌더링 못 함) |
| 완전사망(스테이지 실패) | `StageFailedBannerUI`(2026-09-14, `StageClearBannerUI`와 동일 골격) — `StageNetworkState.OnAnyStageFailedPulse` 구독, 단발 배너 2초. `NotifyPlayerDeathServerRpc`(사망→리로드 유일 진입점) 안에서 `NotifyStageFailed()`를 호출하므로 즉사·다운 방치 만료 등 원인과 무관하게 항상 뜬다. "OO 사망" `DeathOverlayUI`와 동시에 뜬다(제거 여부는 미정) |
| 부활 진행 중 | 게이지 대신 **파티클 효과**로 표시 (시전 2초로 짧아 숫자 게이지 불필요 판단). 캔슬 시 파티클이 즉시 끊기는 등 실패를 구분할 수 있는 피드백 필요(세부 미정, 8절). 별도 RPC 없이 `IsBeingRevived` true→false 중 `IsDowned`가 여전히 true면 캔슬, `IsDowned`까지 false면 완료로 구분 가능 |
| HP UI | 다운 진입(HP→0)은 `OnDamaged`, 부활(0→3)은 `OnHealed`로 `PlayerHPUI`/`TeamStatusUI`가 갱신된다(`NetworkPlayerSetup.OnHpChanged`) |
| 팀 상태 | `TeamStatusUI` 확장(2026-09-14 구현) — 슬롯별 `downIndicator`(체력 칸 옆 HELP 이미지) + `downTimerText`(정수 초 카운트다운). `PlayerEvents.OnDowned`/`OnRevived`로 on/off, 숫자는 `PlayerDownState.RemainingDownTime`(부활 시전 중엔 정지값)을 `Update`에서 값이 바뀔 때만 갱신. 완전사망(`OnDied`) 시 즉시 숨김 — 사망 후에도 `IsDowned`가 true로 남는 §9 설계 때문. **체력이 낮을 때의 경고 연출(점멸 등)은 없음** — 강조 연출은 다운 상태 하나뿐 |

## 7. Objective 타입별 영향

- 별도 임계값 없음. 트리거는 2절의 "개인 완전사망" 하나로 통일.
- SurviveObjective 등 특정 objective 타입에 대한 예외 규칙 불필요 — "누군가 완전사망하면 그 즉시 실패"가 모든 objective에 동일 적용되므로 "N명 다운까지 허용" 같은 별도 로직을 얹지 않는다.
- ReachZone류(전진형)는 구조상 전원 도달이 필요해 다운 시 자연히 부활을 강제하게 됨 — 별도 처리 불필요.

## 8. 미결정 / 다음 단계

- ~~Cheer 개인 버프 삭제 여부~~ → **확정(2026-09-14): 개인 버프 삭제, TeamCheer만 유지.** Cheer/Voice 도메인 문서 반영은 `CheerAndTutorialDesign.md` 쪽에서.
- ~~다운 상태 물리 레이어~~ → **확정(2026-09-14): 기존 `PlayerDead` 레이어 재사용.** 적 타겟팅 제외는 구현 완료(§10 Enemy).
- 부활 파티클/캔슬 SFX 등 세부 피드백 디자인.

## 9. 네트워크 동기화

**전제:** 기존 `TutorialGatherZone` 게이트 카운트다운(`NetworkDesign.md` §6B.3)과 동일한 패턴 재사용 — "절대 시각 NV + Host 단일 레인 판정 + 클라이언트 로컬 계산 + 1회성 이벤트는 ClientRpc". 새 동기화 패턴 도입 아님.

**연출은 NV 구동 (2026-09-14):** 다운 여부는 지속 상태라 `IsDowned.OnValueChanged`에서 `Player.EnterDownState`/`ExitDownState`를 호출한다. 처음엔 `PlayerDownedClientRpc`/`ReviveCompletedClientRpc`로 했으나 제거 — ① 전송 계층 RPC 중복 수신(`RpcSubmitDedup` 참고)으로 부활 뒤 늦게 도착한 다운 RPC가 서버와 무관하게 로컬만 다시 다운시킬 수 있고 ② RPC가 HP NV보다 먼저 도착해 연출 시점의 `heart`가 이전 값이었다. `IsDowned`는 **부활로만 false**가 되며, 완전사망은 서버 전용 `_deathFinalized`로 표시하고 씬 리로드로 정리한다(false로 내리면 사망 직전 `ExitDownState`가 돌아 레이어·애니가 잠깐 살아난 상태로 되돌아간다). BERRY DOWN 배너 같은 1회성 연출이 필요해지면 그때 ClientRpc를 추가한다.

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
| 다운 진입 | `NetworkDamageUtil` → `NetworkPlayerSetup.ApplyDamageFromServer`에서 HP 0 판정(즉사 아님) | 부활 가능자 판정(§2). 없으면 즉시 완전사망. 있으면 `IsDowned=true`, `DownDeadlineServerTime = now+10` | NV 전파 → `OnValueChanged`에서 다운 연출 |
| 부활 요청 | 시전자 Owner → `RequestStartReviveServerRpc(downedPlayerId)` | 검증(§9.4) 통과 시 `IsBeingRevived=true`, `ReviverClientId=요청자`, 남은 시간·시전자 시작 위치 내부 보관, 완료 시각 기록 | NV 전파 |
| 캔슬 | Host `Update()`: 시전자 위치 밀림·사거리 이탈·사망/다운 / 시전자 실제 HP 감소 / Owner `RequestCancelReviveServerRpc`(E 해제·다른 입력) | `IsBeingRevived=false`, 보관해둔 남은 시간으로 `DownDeadlineServerTime` 복원 | NV 전파 (클라이언트는 `IsBeingRevived` 변화로 캔슬 인지, §6) |
| 완료 | Host `Update()`: 캔슬 없이 2초 경과 | HP 3칸 적용·1초 무적 부여 **후** `IsDowned=false` (Host에서 콜백이 동기 발동하므로 HP 먼저) | NV 전파 → `OnValueChanged`에서 다운 해제 연출 |
| 완전사망 | Host `Update()`에서 매 프레임 `ServerTime >= DownDeadlineServerTime && IsDowned && !IsBeingRevived` 체크 (게이트 `UpdateGate()`와 동일한 Host 단일 레인 방식) | `StageManager`에 실패 직접 통보(같은 Host 프로세스, RPC 불필요) | `StageFailedClientRpc` (배너 2초 → 씬 리로드) |

### 9.3 이동 잠금 / 시전 캔슬 원칙

**다운 중 이동 불가**는 Host가 위치를 묶지 않는다. 이동 권한은 Owner+`ClientNetworkTransform`(No Host-move 원칙, `NetworkDesign.md` §9.0)이므로 각 클라이언트가 자신의 `IsDowned`로 **자기 입력을 스스로 차단**한다.

**부활 시전자는 잠그지 않는다(2026-09-14).** 처음엔 "내가 시전 중"을 NV로 판단해 시전자의 수평 속도를 0으로 묶었으나 폐기 — ① 잠금이 서버 수락 NV 도착(RTT)만큼 늦게 걸리고 ② 매 물리 프레임 속도를 덮어써 넉백 등 외부 힘까지 지워졌다. 대신 **움직이면(밀리면) 캔슬**로 바꿔 두 문제가 함께 사라졌다. 캔슬 판정은 두 경로로 나뉜다.

| 경로 | 판정 | 이유 |
|------|------|------|
| Host (`PlayerDownState.Update`) | 시전자 위치가 시작 위치에서 `reviveMoveTolerance`(기본 0.3m) 초과 / 대상과의 거리가 `reviveRange` 초과 / 시전자 사망·다운 / 시전자 HP 감소(`NetworkPlayerSetup.ApplyDamageFromServer` → `CancelIfReviving`) | 서버 권한으로 볼 수 있는 것은 위치(CNT 복제)와 HP뿐. 원인(걷기·넉백·바람 등)과 무관하게 한 규칙으로 처리되고 새 RPC가 필요 없다. 허용 거리는 Host가 보는 원격 시전자 위치가 CNT 보간값이라 떨림 흡수용 |
| Owner (`PlayerReviveInteract`) | E 해제, E 외 물리 버튼(키보드 키·마우스 클릭·패드 버튼, `synthetic`/`noisy` 제외) 입력 → `RequestCancelReviveServerRpc` 1회 | Host는 키 입력을 볼 수 없다. 자기 의사 표시일 뿐이라 클라이언트를 신뢰해도 악용 여지가 없다. 마우스 이동·스틱의 합성 방향 버튼은 `synthetic`이라 입력 캔슬에서 제외(스틱 이동은 Host 위치 판정이 잡는다) |

Owner는 요청 직후부터(서버 수락을 기다리지 않고) 입력을 감시한다. 시작·캔슬 RPC는 같은 신뢰 채널로 순서가 보장되므로 수락 전에 캔슬해도 "시작 → 캔슬" 순으로 처리되고, 거절됐거나 이미 끝난 시전에 대한 캔슬은 Host에서 no-op이다. 다른 입력을 누른 채로는 요청 자체를 보내지 않는다(시작 즉시 캔슬 방지).

**구현 현황(2026-09-14):** 완전사망은 아직 `StageManager` 직접 통보 + `StageFailedClientRpc` 배너가 아니다. 기존 사망 파이프라인(`NetworkPlayerSetup.FinalizeDownDeath` → `ForceKillClientRpc` → `RaiseDied` → `StageResetOnPlayerDeath`, 배너 없이 즉시 리로드)을 임시로 재사용 중이며, 위 표의 배너 흐름은 Stage/UI 작업 때 교체한다.

### 9.4 검증 항목 (Host-side, `RequestStartReviveServerRpc`)

- 요청자-대상 간 거리(근접 상호작용 범위 내)
- 대상이 실제로 `IsDowned == true`이고 완전사망 확정 전(`_deathFinalized`/`IsDead` 아님)
- 대상이 이미 `IsBeingRevived == true`(다른 시전자 존재)가 아님
- 요청자 본인이 다운·사망 상태가 아님(다운된 사람은 시전 불가)
- 요청자가 이미 다른 대상을 시전 중이 아님(시전자 측 1:1 — 이게 없으면 두 명을 동시에 시전해 캔슬 추적이 한쪽만 남는다)
- 중복 수신 방어: `RpcSubmitDedup` 적용. **시작·캔슬 RPC가 번호열 하나를 공유**한다 — 따로 두면 늦게 도착한 중복 캔슬이 그 뒤 새로 시작한 시전을 캔슬하거나, 중복 시작이 캔슬 이후 입력 없이 시전을 재시작할 수 있다

## 10. 관련 도메인 (변경 필요 지점)

- **Player:** 다운/부활 라이프사이클 소유 — `PlayerDownState`(상태·Host 판정), `PlayerReviveInteract`(입력), `Player.EnterDownState/ExitDownState`(로컬 연출). `GameArchitectureBoundaries.md` Player 항목에 반영됨.
- **Damage:** 진입점은 그대로 `NetworkDamageUtil` 단일(Host-applied). 실제 분기는 위임받는 `NetworkPlayerSetup` — HP 0이면 `EnterDown`, 즉사·낙사는 `CanApplyLethalFromServer`로 다운 중에도 통과.
- **Enemy (구현 완료, 2026-09-14):**
  - 추적 대상 선택: `Stage5ChaserAI.UpdateTarget`은 `Player` 레이어만 고르므로 `PlayerDead` 전환만으로 제외(코드 변경 없음). `TrapPlayerTracker.IsValidTarget`은 `playerVisibleLayer`가 0(미설정)이면 레이어 필터가 꺼져 씬 설정에 의존하게 되므로 `!IsDowned`를 명시.
  - 공격 반응: `Stage5ChaserHitbox.TryHit`에 `IsDowned` 제외 — 데미지 자체는 HP 0 가드로 무해하지만, 없으면 추격자가 다운된 대상을 때린 것처럼 정지·피격 애니·SFX를 낸다.
  - 도망 AI·포획: `Stage5TargetRunner`의 추적 대상 선택·포획 트리거에서 `IsDowned` 제외.
  - 제외하지 않음: `BossSpherePhaseDriver.HandlePhaseTimeout`(페이즈 실패 전원 즉사 — 다운 중에도 즉사 적용이 §2 규칙), `ContactDamage`/`SpikeTrap`(Traps 도메인, 피격 반응 콜백 없음 — HP 0 가드로 충분), `BoulderSpawnManager.AnyPlayerInTrigger`(용도 미확인, 보류).
- **Stage:** `StageObjective`/`StageManager`가 개인 완전사망 이벤트를 구독해 실패 처리.
- **UI:** `TeamStatusUI`, `PlayerHPUI` 확장.
