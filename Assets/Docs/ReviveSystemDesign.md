# Revive System Design (부활 시스템)

**상태(2026-09-19):** **다운 시스템 전면 폐기 — 사망 + 자동 부활 구조로 교체.**
**설계 확정 ✅ · UI 확정 ✅(§11) · 코드 구현 완료 ✅ · 에디터 작업 완료 ✅(§10.1, 2026-09-21 전수 확인)**

> **읽을 순서:** §2(판정) → §3(부활) → §9.3(텔레포트 — `NetworkDesign.md` §11.9 규칙 필수) →
> §10(변경 지점) → §11(UI). T5 예외는 §7.1 + `TStage5RunnerRedesign.md` §1.6.

부활 시스템 SSOT. 도메인 소유권은 `GameArchitectureBoundaries.md`를 따르고, 네트워크 권위·동기화 상세는 §9.
`NetworkDesign.md`의 권위 매트릭스(§9.0)에는 요약 1행만 편입 — 상세는 이 문서가 1차 SSOT.
Cheer 상호작용은 `CheerAndTutorialDesign.md` 범위와 겹치는 부분만 §5에서 다룬다.

---

## 0. 한 줄 요약

HP가 0이면 **죽는다**. 1초 뒤 **가장 가까운 생존자 위치에서 자동으로 살아난다**.
살아날 때마다 **팀 공유 목숨 1개**를 쓰고, 목숨이 없으면 그 죽음이 곧 스테이지 실패다.

---

## 1. 배경 / 목적

### 1.1 원래 문제 (유지)

팀원 1명이 죽으면 스테이지 전체가 처음부터 리셋됐다. 특히 **낙사 = 즉시 스테이지 실패**가 너무 가혹했다 —
구멍에 한 번 빠진 것이 팀 전체의 진행을 날렸다. 캐주얼·즐기기 목적 플레이어층에서 체감 난이도가 최상급으로 떴다.
**이 문제를 푸는 것이 이번 리팩토링의 가장 큰 이유다**(2026-09-19 사용자 확정).

### 1.2 다운 시스템을 폐기한 이유 (2026-09-19)

구 설계는 "다운(쓰러짐) 상태 + 팀원이 **E를 2초 홀드해서 살리기**"였다. 폐기 이유는 기능이 아니라 **유지비**다:

- 시전 캔슬 판정이 3경로(Host 위치 밀림 판정 + HP 감소 훅 + Owner 입력 감시)로 흩어져 있었다
- CNT 보간 떨림 때문에 `reviveMoveTolerance` 0.3m·`reviveMoveGraceDuration` 0.2초 같은 보정값이 필요했다
- 시전자/대상 양측 1:1 장부(`_activeReviveByReviver`)와 시작·캔슬 RPC 공유 번호열(dedup)이 필요했다
- 상태가 `IsDead`와 `IsDowned` 둘로 갈려 8개 파일에 `IsDead || IsDowned` 이중 가드가 퍼졌다

자동 부활로 바꾸면 위가 **전부 사라진다**. 협동 요소(살리러 가기)를 잃는 대가로 단순함을 산 것이며,
팀 긴장은 **팀 공유 목숨**(§4)이 계속 담당한다.

### 1.3 기각한 대안

| 대안 | 기각 이유 |
|---|---|
| 목숨 N회 소모 후 리셋 (다운 없음) | SurviveObjective류의 "3명 다운 + 1명 캐리" 협동 설계 의도 붕괴 |
| 다운 + 팀원 살리기 (구 설계) | §1.2 — 유지비. 2026-09-19 폐기 |
| 3초 부활 유예 | 1초로 축소. 3초는 죽은 뒤 대기가 길어 "가볍게"가 안 나온다 |
| 클릭으로 부활 스킵 | 결과가 같아 순수 빨리감기였다. 의미가 없으면 입력 처리만 늘어난다 |
| 부활 위치 = 마지막 안전 위치 | 낙사는 완벽히 풀리지만 **협동 합류가 안 된다** — 뒤처져 죽으면 여전히 혼자 |
| 부활 위치 = 생존자 옆 1m 오프셋 | 방향이 벽·구멍으로 나올 수 있어 지면 검증이 필요해진다 |
| 부활 위치 = 생존자 발자국 버퍼 | 유효하지만, 생존자 위치를 그대로 쓰면 애초에 검증이 필요 없다 |

---

## 2. 판정 / 트리거

| 상황 | 결과 |
|---|---|
| HP 0 도달 | **사망**. 아래 분기 |
| └ 생존자 있음 + 팀 목숨 ≥ 1 | 목숨 1 소모 → **1초 뒤 자동 부활**(§3) |
| └ 팀 목숨 0 | **즉시 스테이지 실패** → 씬 리로드 |
| └ 생존자 0 (전원 사망) | **즉시 스테이지 실패** → 씬 리로드 |
| 즉사 소스 (낙사 포함) | **일반 사망과 동일 취급.** 별도 분기 없음 |
| 스테이지 실패 (objective) | 전원 즉사를 거치지 않고 **직접 실패 통보**(§6) |
| 개인 사망 횟수 | 제한 없음 — 팀 목숨이 유일한 상한 |

**"생존자"의 정의:** 지금 살아 있는(`IsDead == false`) 다른 플레이어. **부활 대기 중인 사람은 생존자가 아니다**
(2026-09-19 사용자 확정). 그래서 전원이 1초 유예 안에 죽으면 생존자 0으로 판정돼 즉시 실패한다.

**솔로:** 목숨 = 파티 인원−1 = **0**이므로 죽으면 즉시 실패다. 별도 솔로 분기가 없다.

**동시 사망:** Host 단일 레인이라 순서가 정해진다. 목숨 1개에 두 명이 같은 프레임에 죽으면
먼저 처리된 1명이 소모하고 부활, 나머지는 목숨 0이므로 즉시 실패다(2026-09-19 사용자 확정).

**낙사:** `NetworkPlayerSetup.ApplyFallDeathFromServer` 경로 그대로. 이제 **부활 대상**이며,
부활 위치가 생존자 위치라 구멍 밖으로 복귀한다. §1.1의 문제가 여기서 해소된다.

**T.Stage2 완화(2026-09-14, 유지):** `MemoryPathTile`/`ColoredMemoryPathTile`/`PioneerPathTile`의 트랩·오답 타일은
즉사가 아니라 **데미지**(기본 6, 매니저 Inspector로 조정)다. 연속 피격은 `NetworkPlayerSetup`의 서버 피격 무적
타이머가 막는다. `MemoryPath`/`ColoredMemoryPath`의 `PathState.Failed`/`OnFailed`는 제거됨(소프트락 방지).

---

## 3. 부활

| 항목 | 규칙 |
|---|---|
| 트리거 | **자동.** 입력·상호작용 없음. 살리기(구 E 홀드) 전면 폐기 |
| 유예 | 사망 후 **1초** |
| 유예 중 연출 | **카메라는 죽은 자리에 그대로.** 시체가 그 자리에 보인다. 별도 카메라 코드 없음 — 플레이어가 안 움직이니 `ThirdPersonCamera`가 따라오지 않는다 |
| 부활 위치 | **가장 가까운 생존자의 위치 그대로**(+0.1m 위). 오프셋·방향 선택·지면 레이캐스트 **전부 없음** |
| 위치가 안전한 이유 | 사람이 지금 서 있는 자리라 **구멍일 수도 벽 안일 수도 없다.** 검증이 구조적으로 불필요하다 |
| 겹침 | 캡슐끼리 겹치면 물리가 밀어낸다. 별도 분산 처리 없음 |
| 이동 방식 | Host가 좌표 계산 → **ClientRpc** → **Owner 머신에서 `NetworkTransform.Teleport()`**. `transform.position` 대입 **금지** — `NetworkDesign.md` §11.9가 SSOT(§9.3) |
| 카메라 | 텔레포트 순간 **스냅**(보간하면 맵을 가로질러 날아간다) |
| 부활 직후 HP | **3칸** (`maxHeart`는 `Kkultteok.prefab` 기준 **5**. 풀피가 아니다) |
| 그레이스 피리어드 | **1초 무적** |
| 위험 지역 부활 | **허용한다.** 생존자가 보울더 앞이면 같이 죽는다 — 의도된 결과(2026-09-19 사용자 확정) |
| 되돌리는 것 | 색(흑백·고유색) **유지**. 버프(Shield/SpeedUp)·색 전환 쿨다운 **초기화** |
| 점유 판정 | 부활 후 **1초 동안 점유 판정에서 제외**(§3.1) |

### 3.1 부활 직후 점유 판정 제외

**문제:** T.Stage4 `CapacityTile`은 "1명만 밟을 수 있는 타일"이 있는데, 생존자 옆에 부활하면 점유가 갑자기
2가 되어 타일이 깨지고 둘 다 떨어진다. `PressurePad`·`ColorGatePad`·`ColorTile`도 같은 구조다.

**기각한 안 — 부활 후 1초간 콜라이더 끄기:** 콜라이더가 꺼진 채 `IsDead == false`가 되면
`Player.Update`의 y 고정(`fixedY`)이 풀려서 **중력으로 바닥을 뚫고 떨어진다.**

**채택안:** `Player`에 **부활 무적 중 플래그**를 두고, 지금 `IsDead`를 보는 점유 판정들이 같이 본다.
콜라이더는 켜져 있어 바닥은 정상적으로 밟고, 1초 동안만 안 세이므로 그 사이에 비켜나면 된다.
`CapacityTile`은 이미 `IsDead` 필터를 갖고 있다(`CapacityTile.cs:239, 246`) — 거기에 한 조건을 더하는 형태다.

---

## 4. 팀 공유 목숨

- **팀 전체가 공유하는 목숨 풀.** 목숨이 남아 있는 동안 사망은 부활로 이어진다.
- **목숨 개수 = 파티 인원 − 1** (4인=3, 3인=2, 2인=1, 1인=0).
- **소모 시점 = 사망 순간** (2026-09-19 변경). 구 설계는 "부활 완료 시" 소모였는데, 자동 부활이면
  완료 시점에 소모할 이유가 없고 **동시 부활 완료 경쟁(`FailAllOtherDowned`)이 통째로 사라진다.**
  규칙이 "죽는 순간 목숨 0이면 실패, 아니면 1 소모 후 1초 뒤 부활" 한 줄이 된다.
- **범위 = 씬 단위.** `PlayerSpawnCoordinator.OnPlayersReady`에서 1회 확정, 회복 없음, 씬 리로드로만 초기화.
  한 씬 안의 서브 스테이지끼리는 이어진다.
- **목숨 0에서 사망:** 즉시 스테이지 실패. 목숨이 0이 되는 것 자체로는 아무도 죽지 않는다.
- **적용 범위:** `StageNetworkState`가 있는 M/T 스테이지 **12개 씬만**. 튜토리얼은 `StageNetworkState`가 없다(§8).
- **구현:** `StageNetworkState`가 목숨 NV(`TeamLivesRemaining`, Host 쓰기) 소유 — 팀 전체 상태라
  개별 플레이어가 아니라 스테이지 싱글턴 슬롯.
- **UI:** 상시 `TeamLivesUI`(아이콘 + 숫자). **모든 스테이지에서 표시한다** — 목숨을 쓰지 않는
  미니게임 스테이지(`SequenceRing`·`ColorTileChallenge`·T5)에서도 숨기지 않는다(2026-09-19 사용자 확정).
  `TeamLivesUI.ReadVisibleLives()`로 매 프레임 폴링 — Client는 NV 초기값이 스폰 데이터로 오면
  `OnValueChanged`가 불리지 않는다.

---

## 5. 사망 중 상태 (1초)

- **콜라이더 제거.** 기존 `Player.Die()` 그대로 — 시체를 통과할 수 있다(2026-09-19 사용자 확정).
  구 다운 설계의 "시체가 통로를 막는 긴장"은 폐기.
- `PlayerDead` 레이어 전환 — 적 AI의 감지·타겟팅에서 제외.
- 애니메이션은 기존 `doDie` 모션.
- 입력 차단: 이동·색전환·공격·이모트·상호작용 전부. `IsDead` 가드가 이미 전부 처리한다.
- 위치: `Player.Update`가 y를 `fixedY`로 고정 — 낙사해도 계속 떨어지지 않는다.
- **Cheer는 계속 된다.** Cheer 도메인에 `IsDead` 가드가 없어(예외: `SalivaVolume`) 마이크 인식·투표가
  죽은 상태에서 그대로 통과한다. **죽어 있어도 TeamCheer를 의무적으로 하게 만드는 것이 의도다**(2026-09-19 확정).
- 머리 위 느낌표(`PlayerCheerHeartsUI`)는 `CheerExclamation`을 **플레이어 transform의 자식으로** 붙이므로
  (`PlayerCheerHeartsUI.cs:183`), 렌더러를 자식까지 일괄로 끄면 같이 꺼진다.
- Dissonance 음성 채팅은 제한 없음.
- **관전 모드 아님** — 카메라는 죽은 자리에 1초 머물 뿐, 시점 이동·타 플레이어 시점 전환이 없다
  (`GameArchitectureBoundaries.md` 스펙터 모드 out-of-scope 원칙 준수).

---

## 6. 스테이지 실패 — 전원 즉사 폐기 (2026-09-19)

**구 구조:** objective 실패 → `StageManager.KillAllPlayersOnFail()`이 **전원을 즉사**시켜
"사망 → 씬 리로드" 결선에 얹었다.

**왜 성립하지 않는가:** 자동 부활이면 첫 번째로 죽은 사람이 **즉시 살아난다**. 지금은 목숨이 정확히
(인원−1)이라 우연히 수렴하지만, 목숨을 늘리는 순간 전원 즉사시켰는데 전원이 살아나고
**스테이지 실패가 조용히 무시된다.**

**채택안:** objective 실패는 `StageNetworkState`에 **직접 통보**한다. 원래 구 설계 §9.2가 의도했다가
미구현 TODO로 남아 있던 흐름이다.

| 파일 | 변경 |
|---|---|
| `StageManager` | `KillAllPlayersOnFail()` → `NotifyStageFailed(reason)` → `StageNetworkState.FailStageFromServer`. **호출부가 2곳이었다**(objective 실패 판정 + `Debug_Fail` ContextMenu) — 설계 시점엔 1곳으로 적혀 있었다 |
| `SequenceRingObjective` | `Fail()` 전에 자체 `KillAllPlayers()`를 또 부르는 **중복 경로 — 삭제** |
| `BossSpherePhaseDriver` | 페이즈 타임아웃 즉사 → `FailStageFromServer("T.Boss 페이즈 시간 초과")` |

`StageObjective.Fail()` → `StageManager`가 중앙에서 받으므로, ColorTileChallenge·T5·SequenceRing 등
모든 objective가 자동으로 새 경로를 탄다.

**`StageResetOnPlayerDeath` 폐기.** `OnDied`를 구독해 `NotifyPlayerDeathServerRpc()`를 보내던
오케스트레이터인데, 사망이 더 이상 리로드를 뜻하지 않으므로 삭제한다. 실패 판정(목숨 0 사망 / 생존자 0)은
**Host가 `StageNetworkState`에서 직접** 내린다.

**`NotifyPlayerDeathServerRpc` → `NotifyStageResetServerRpc` 개명 완료.** 이제 "사망 문"이 아니라
"실패·리셋 문"이다. 남은 호출부는 `EscMenuController.OnClickReset()` 하나뿐이다(ESC 리셋 — 유지).

실제 실패 판정(objective 실패 / 팀 목숨 소진 / 생존자 0)은 RPC를 거치지 않고 Host가
**`FailStageFromServer(reason)`** 로 직접 들어온다. 둘 다 `BeginStageResetOnServer(reason)` 한 본문으로
모이므로 `_resetPending` 멱등 가드와 STAGE FAILED 배너가 원인과 무관하게 항상 한 번만 동작한다.
`reason`은 로그로 남는다 — 어느 경로로 판이 끝났는지 콘솔에서 바로 구분된다.

---

## 7. 스테이지별 예외

### 7.1 T.Stage5 — 러너 사망 = 즉시 스테이지 실패 (2026-09-19 확정)

T5는 **부활 예외 스테이지**다. 상세는 `TStage5RunnerRedesign.md` §1.6이 SSOT.

| 항목 | 규칙 |
|---|---|
| **T5는 부활이 없다** | **누가 죽든 즉시 `Fail()`** — 러너·안내자 구분 없음(2026-09-19 확정) |
| 부활 차단 | 씬의 `StageNetworkState`에 **`Disable Revive` 체크박스**(Inspector). 켜면 `PlayerReviveState`가 사망 시 **목숨도 안 쓰고 예약도 안 한다** |
| 감지 주체 | **`T5RunnerObjective.Tick()`이 매 Tick 경계에서 `AnyPlayerDead()`를 본다** → `Fail()`. 구 설계는 `StageResetOnPlayerDeath`에 맡겼는데 그게 폐기됐다 |
| 팀 목숨 | T5에서는 **한 번도 소모되지 않는다.** HUD에는 그대로 표시(§4) |

**왜 차단 플래그와 감지가 따로인가(구현 결정).** "`Fail()`이 1초 안에 나므로 §9.4가 알아서 예약을
취소한다"에 기대면 **목숨은 이미 깎인 뒤**다(소모 시점 = 사망 순간, §4). 차단 플래그가 있어야
"T5에서는 목숨이 한 번도 소모되지 않는다"가 성립한다. 그리고 실패 확정은 Stage 도메인이 내려야 하므로
(`GameArchitectureBoundaries.md`) 플래그는 부활만 막고 `Fail()`은 Objective가 낸다.
시간 관계에 기대는 부분이 없다.

**감지를 구독이 아니라 매 Tick 검사로 한 이유**는 이 클래스가 이미 Goal 판정에 쓰는 방식 그대로이기
때문이다 — 구독/해제 수명 관리가 없고 Enter/Exit 짝이 어긋날 여지도 없다. 인원이 최대 4명이라 비용은
무시할 수 있다.

**왜 안내자도 즉시 실패인가:** 2층에 `SpikeTrap`/`ContactDamage`를 넣기로 하면서 안내자도 죽을 수
있게 됐다. 기본 규칙대로면 **마지막 안내자가 죽었을 때 생존자가 러너뿐**이라 러너 옆(1층)에 부활하는데,
`TStage5RunnerRedesign.md` §1.3 때문에 2층으로 못 올라간다 → 2층 Goal 불가 → 90초를 멍하니 기다린다.

"누가 죽든 실패"로 통일하면 이 구멍이 사라지고, **부활 지점 override 훅도 필요 없다.** 감지 조건이
"러너 사망"에서 "아무나 사망"으로 **넓어지므로 코드는 오히려 줄어든다.** `SequenceRing`·
`ColorTileChallenge`의 "실수 1회 = 판 다시"와도 같은 형태다.
| 왜 부활을 안 넣나 | ① 실패 → 리로드 → **러너 재추첨**이라 실패 자체가 러너 교대 기회다. 부활을 넣으면 같은 사람이 계속 러너다 ② T5 재설계의 이유가 "다른 스테이지와 형태가 같아야 한다"였다 ③ 부활 지점 override 훅이 통째로 불필요해진다 |

**부활을 허용해도 난이도가 안 무너지긴 한다** — 체이서 4마리가 소모품이라 러너는 구조적으로 최대 1번만
죽는다(HP 5, 데미지 2 → 3방 사망 = 체이서 3마리 소진 → 남은 1마리로는 부활 HP 3을 못 뚫는다).
그럼에도 위 3가지 이유로 부활을 안 넣는다.

---

## 8. 미결정 / 다음 단계

### 8.1 튜토리얼 — **처리하지 않는다 (2026-09-19 확정)**

튜토리얼에는 `StageNetworkState`도 `StageResetOnPlayerDeath`도 없어 목숨도 실패 경로도 없다.
그런데 **애초에 죽을 수가 없다**:

- 씬 검사 결과 **데미지 소스 0개** — `ContactDamage`·`SpikeTrap`·`TrapProjectile`·`SalivaHazard`·
  `ArrowTrap`·`DropTrap` 전부 0. `ContactKnockback` 11개가 전부인데 이건 넉백만 준다
- **낙사할 구멍도 없다**(2026-09-19 사용자 확인)

따라서 **별도 처리를 넣지 않는다.** "없는 상황을 위해 코드를 늘리지 않는다"(T4 §1.4 / T5 §1.2와 같은 판단).

> ⚠️ **튜토리얼에 데미지 소스나 낙하 지형을 추가하면 이 판단이 깨진다.** 그때는 생존자가 없을 수 있으므로
> (순차 합류라 첫 입장은 혼자) 부활 위치를 **자기 `ColoredStartZone`**으로 두는 폴백이 필요하다.

### 8.2 남은 미결정

**설계는 전부 닫혔고 코드도 반영됐다 (2026-09-19).** 남은 것은 **에디터 작업 §10.1**뿐이다.

**`Interact`(E) 액션은 존치 확정 (2026-09-19 사용자 결정).** `PlayerReviveInteract` 삭제로 이 *액션*의
바인딩을 읽는 코드는 없어졌지만, **E 키 자체는 현역이다** — 튜토리얼 팀 이름 표지판
(`TutorialCheerNameSignboard.cs:90`)이 `TutorialCheerNameUI.Toggle()`을 E로 연다. 다만 그쪽은
`Interact` 액션을 경유하지 않고 `Keyboard.current.eKey`를 직접 폴링한다(`CheerDigitInput`·
`MicMuteHotkeyUI`와 같은 계열). 그래서 "액션 사용처 0"과 "E 키 사용 중"이 동시에 참이다 —
액션을 지우면 같은 키를 나중에 다시 정의하게 되므로 남긴다.

사망 SFX·화면 플래시는 **이번 범위 밖**으로 확정(§11.3). 필요해지면 그때 별도 항목으로 연다.

**폐기 — T5 2층 체이서 (2026-09-19):** 안내자 리스크 0 문제를 체이서 1마리로 풀려 했으나
비용이 커서 접었다 — ① 2층을 NavMesh에 구워야 하는데 `TStage5RunnerRedesign.md` §2가
"2층에 렌더 메시를 붙이지 말 것"으로 막아둔 구조적 보장을 깨야 한다 ② `Stage5ChaserAI`가
러너 clientId만 타겟하므로 타겟 로직 분기가 필요하다 ③ 2인에서 안내자 1명이 도망치며
패드 6개를 다 밟아야 해 인원별 격차가 커진다. **대신 기존 `SpikeTrap`/`ContactDamage`로 압박을
만든다**(2026-09-19 사용자 결정) — 이미 있는 비용만 쓴다.

---

## 9. 네트워크 동기화

**전제:** 새 동기화 패턴을 도입하지 않는다. 기존 "Host 단일 레인 판정 + NV 지속 상태 + 1회성은 ClientRpc" 그대로.

### 9.1 상태

구 설계의 NV 4개(`IsDowned`·`DownDeadlineServerTime`·`IsBeingRevived`·`ReviverClientId`)에서
**NV 1개로 줄었다**(구현 결과 — 검토 대상이던 `IsDead` NV는 **불필요로 확정**).

| 필드 | 타입 | 쓰기 | 용도 |
|---|---|---|---|
| `_reviveAtServerTime` | `NetworkVariable<double>` | Host | 부활 예정 서버 시각(절대). 음수 = 예약 없음 |

**`IsDead` NV를 안 만든 이유:** `Player.IsDead`는 이미 `ForceKillClientRpc`가 전 머신에 퍼뜨린다
(Owner는 `ForceKill()`, 비오너는 `SyncDeadFlag()`). NV를 하나 더 두면 같은 사실의 출처가 둘이 된다.
대신 **Host 판정용으로는 `PlayerReviveState._isDeadServer`(순수 로컬 필드)를 쓴다** — "지금 누가
생존자인가"를 Host가 RPC 왕복에 기대지 않고 자기 필드만으로 답하기 위해서다(§9 Host 단일 레인).

`StageNetworkState` 소유(팀 전체 슬롯):

| 필드 | 타입 | 쓰기 | 용도 |
|---|---|---|---|
| `TeamLivesRemaining` | `NetworkVariable<int>` | Host | 팀 공유 목숨. -1=미초기화 → (인원−1) → 사망마다 -1. 씬 단위 |

### 9.2 흐름

| 단계 | 트리거 | Host 처리 | 전파 |
|---|---|---|---|
| 사망 | `NetworkPlayerSetup.ApplyDamageFromServer` HP 0 / 낙사 / 즉사 | 생존자·목숨 판정(§2). 실패면 `StageNetworkState` 실패 통보. 아니면 목숨 −1, `ReviveAtServerTime = now + 1` | `ForceKillClientRpc`(기존) |
| 부활 | Host `Update()`: `ServerTime >= ReviveAtServerTime` | 가장 가까운 생존자 좌표 계산 → HP 3 + 무적 1초 부여 | `ReviveClientRpc(pos)` → Owner가 자기 위치 설정 + 카메라 스냅 |
| 스테이지 실패 | objective `Fail()` / 목숨 0 사망 / 생존자 0 | `StageNetworkState`가 실패 확정 | `StageFailedClientRpc`(배너 2초 → 씬 리로드) |

### 9.3 위치 이동 — `NetworkDesign.md` §11.9 재사용

⚠️ **새 규칙을 만들지 않는다.** "⑤ Play 중 텔레포트"의 SSOT가 이미 `NetworkDesign.md` §11.9에 있다
(2026-09-18 신설, T5 라운드 폐기로 사용처가 비어 있었다). **부활이 그 첫 사용처가 된다.**

| 항목 | 규칙 (§11.9 그대로) |
|---|---|
| 목적지 계산 | **Host** — 가장 가까운 생존자 위치 |
| 실제 좌표 쓰기 | **각 플레이어의 Owner 머신.** ClientRpc를 전원이 받고 `clientId == LocalClientId`인 사람만 자기 것을 옮긴다 — CNT는 Owner 권한이라 남의 좌표를 쓸 수 없다 |
| 쓰는 API | **`NetworkTransform.Teleport(pos, rot, scale)` + `rb.position` 동기화.** `transform.position` 단순 대입 **금지** |
| 진입점 | Host만 호출하는 진입점 **하나**를 둔다(구 `BeginT5Transition` 자리) |

**왜 `Teleport()`여야 하나** — 그냥 대입하면 ① CNT가 `Interpolate ✅`라 원격 화면에서 **맵을 가로질러
미끄러지고** ② Host 비오너 레인이 `rb.MovePosition()`으로 적용해 그 거리를 **물리로 쓸어 벽에 끼거나
터널링**한다. 부활은 죽은 자리에서 생존자까지 거리가 길 수 있어 두 사고가 다 현실적이다.
`Teleport()`는 보간을 리셋하며, **권한(Owner) 인스턴스가 아닌 곳에서 호출하면 예외를 던진다.**

⚠️ **`Player.fixedY`를 같이 갱신해야 한다.** `Player.Update`가 `IsDead` 중 y를 `fixedY`로 매 프레임
고정하므로(`Player.cs:167-173`), 갱신하지 않으면 텔레포트 후 옛 높이로 끌려간다.

⚠️ **카메라 스냅 — 전용 API 신설(2026-09-19 구현).** `ThirdPersonCamera`가 `positionDamping`으로
보간하므로 텔레포트하면 맵을 가로질러 날아간다. 설계 시점엔 "`ThirdPersonCamera.cs:181-183`에 스냅
분기가 이미 있다"고 적었는데 **그건 `positionDamping <= 0`일 때 타는 내부 분기지 외부 API가 아니었다.**
`ThirdPersonCamera.SnapToTarget()`을 새로 만들었다.

| 항목 | 규칙 |
|---|---|
| 방식 | **1프레임 플래그.** 호출은 부활 ClientRpc(메시지 처리 레인)라 위치를 직접 써도 같은 프레임의 `LateUpdate`가 `SmoothDamp`로 덮어쓴다 — 카메라 위치의 진실은 `LateUpdate` 하나이므로 거기서 소비한다 |
| `target` | **안 건드린다.** 부활은 플레이어 transform을 옮기는 것이고 카메라는 그 transform을 따라가므로 자동으로 맞는다 |
| 회전 | **안 건드린다.** `rotationDamping`은 각도 보간이고 텔레포트로 각도는 안 바뀐다. yaw/pitch 유지 — 부활 순간 시점 방향까지 바뀌면 방향감이 끊긴다 |
| 프리뷰 중 | **무동작.** 프리뷰/블렌드 중엔 `target`이 플레이어가 아니라 pivot이라 스냅하면 탑다운 프레이밍이 튄다. 인트로 도중 사망은 실재하는 경로다(`ForceGameplayViewImmediate` 주석의 2026-09-14 사고) |

⚠️ **낙사 폴백을 `IsOwner`로 좁혔다(2026-09-19 구현).** `NetworkPlayerSetup.Update()`의 Host 레인 Y
판정이 원격 플레이어에게도 돌고 있었는데, **부활 텔레포트는 Owner 머신에서 일어나므로 Host의 CNT
프록시 좌표가 아직 구멍 바닥인 몇 프레임 동안 `IsDead`가 이미 false다.** 그 창에서 이 폴백이 돌면
살아난 그 프레임에 다시 낙사 확정되어 **목숨이 한 번에 2개 나간다**(4인 3목숨이 낙사 두 번에 소진).

타이머로 막지 않고 `IsOwner` 게이트로 막은 이유는 그게 원래 의도이기 때문이다 — 같은 메서드의 주석이
이미 *"Host-as-Owner 등 Host 실좌표가 신뢰될 때의 폴백. Client Owner void 낙사의 주경로는
`ReportFallDeathServerRpc`"* 라고 적고 있었다. 원격 프록시 Y는 **원래부터 신뢰 대상이 아니었고**
(void 낙사를 놓치기 때문에 Owner 신고 RPC가 존재한다) 안전망 역할도 못 하고 있었다. 상태 추가 없이
코드가 줄어드는 방향이다.

### 9.4 실패 확정 시 부활 예약 취소

실패가 확정되는 순간 **진행 중인 부활 예약을 전부 취소**한다. 안 하면
"실패 배너(2초) → 리로드" 사이에 1초 예약이 먼저 터져 죽은 사람이 살아난다. 지금은 배너가 2초라
우연히 안전하지만 시간 관계에 기대지 않는다.

**구현(2026-09-19):** `StageNetworkState.IsStageFailing`(= `_resetPending`)을
`PlayerReviveState.Update()`가 예약 만료 시점에 확인한다. 이벤트 구독이 아니라 **확인 방식**인
이유는 구독 수명 때문이다 — `PlayerReviveState`(플레이어 스폰)와 `StageNetworkState`(씬 오브젝트)는
스폰 순서가 보장되지 않아 `OnDeathReloadStarted` 구독/해제를 양방향으로 관리해야 하는데, 어차피
Host `Update()`가 매 프레임 도는 자리라 플래그 한 줄이면 순서 문제 없이 같은 보장을 얻는다.
`OnDeathFromServer()` 진입부에서도 같은 플래그를 봐서 **리로드 대기 중에 목숨이 더 깎이지 않게** 한다.

---

## 10. 관련 도메인 (변경 지점 — 2026-09-19 구현 완료)

- **Player:** `IsDowned` **완전 삭제** → 8개 파일의 `IsDead || IsDowned` 이중 가드가 `IsDead` 하나로 정리
  (`PlayerPunch`·`PlayerPunchHitbox`·`PlayerStealth`·`ContactKnockback`·`ColorGatePad`·`TrapPlayerTracker`·
  `Stage5ChaserHitbox`·`PlayerEmoteMenuUI`). `Die()`의 역함수 **`Revive(graceDuration)`** 신규 —
  콜라이더·레이어·애니 복구. `EnterDownState`/`ExitDownState` 삭제.
  `IsReviveGrace` / `CountsForOccupancy` 신규(§3.1). `PlayerEvents.OnDowned` 삭제(`OnRevived`는 유지).

  > ⚠️ **콜라이더 복구는 "전부 켜기"가 아니다.** `Die()`가 끄기 직전의 `enabled`를 `colsWereEnabled[]`에
  > 저장하고 `Revive()`가 그 값으로 되돌린다. 전부 켜면 원래 꺼져 있던 콜라이더(연출용·조건부 히트박스)까지
  > 살아난다 — 씬 리로드로만 죽던 시절엔 없던 문제다.

  > ⚠️ **`fixedY`는 갱신하지 않는다.** `Update()`의 y 고정 분기는 `IsDead` 중에만 돈다. `Revive()`가
  > `IsDead`를 먼저 내리고 같은 호출 스택에서 곧바로 텔레포트가 이어지므로 그 사이에 `Update`가 끼어들 수
  > 없고, 다시 죽으면 `Die()`가 `fixedY`를 새로 잡는다. (설계 §9.3의 경고는 검증 결과 해당 없음.)

  > ⚠️ **낙사 래치(`fallDeathReported`/`fallAnimTriggered`)는 `Die()`가 이미 내린다.** 다만 `Die()`는
  > Owner 머신에서만 돌아서(비오너는 `SyncDeadFlag`) `Revive()`에서도 명시적으로 내려 이중 방어한다.
- **Damage:** 진입점은 그대로 `NetworkDamageUtil` 단일(Host-applied). `CancelIfReviving` 삭제.
  **HP 0·낙사·즉사가 `NetworkPlayerSetup.ConfirmDeathFromServer()` 한 문으로 합쳐졌다** — 전파 후
  `PlayerReviveState.OnDeathFromServer()`로 판정을 넘긴다. `ReviveFromServer`는 유지(HP 3 + 무적).
- **Network:** `PlayerDownState` → **`PlayerReviveState`로 개명**, NV 4→1개(§9.1), 시전/캔슬/dedup 전부 삭제.
  `StageNetworkState`가 팀 목숨 + 실패 판정 소유(`TryConsumeTeamLife` / `FailStageFromServer` /
  `NotifyStageResetServerRpc` / `IsStageFailing` / `IsReviveDisabled`).
- **Stage:** `StageResetOnPlayerDeath` **삭제**. `StageManager`(2곳)·`SequenceRingObjective`·
  `BossSpherePhaseDriver`가 실패 직접 통보(§6). `T5RunnerObjective`가 사망 직접 감지(§7.1).
- **UI:** `ReviveInteractPromptUI`·`LocalDownOverlayUI`·`DeathOverlayUI` **파일째 삭제**,
  `TeamStatusUI`에서 다운 표시 + 사망 표시(`SetDead`) 제거. `TeamLivesUI`만 남는다 — 상세 **§11**.
- **입력:** `PlayerReviveInteract.cs` **전체 삭제**. `InputSystem_Actions`의 `Interact`(E) 액션은
  **그대로 둔다** — E 키는 튜토리얼 팀 이름 표지판이 계속 쓴다(§8.2).
- **VFX:** `ReviveHoneyVfx.cs` 삭제 + `Kkultteok.prefab`의 `ReviveHoneyMist` 자식 — §10.1.
- **Enemy:** `PlayerDead` 레이어 전환은 그대로라 추적 제외가 유지된다. `IsDowned` 참조만 `IsDead`로 정리.
- **Camera:** `ThirdPersonCamera.SnapToTarget()` 신규 — §9.3.

### 10.1 에디터 작업 — ✅ 완료 (2026-09-21 전수 확인)

코드는 전부 반영됐고 **컴파일도 통과했다**. 아래 표는 2026-09-19 시점의 남은 작업 목록이었고,
**2026-09-21 전수 확인 결과 플레이 경로에서는 전부 끝났다.**

> **확인 방법:** `Assets` 전체의 `.unity`/`.prefab` **463개**에서 `m_Script`가 가리키는 GUID를
> `AssetDatabase.GUIDToAssetPath`로 역해석해 해결 안 되는 것(= Missing Script)을 전부 뽑고,
> 두 프리팹은 Unity에 실제 로드해 `GetComponents`에 `null`이 있는지 따로 봤다.
>
> | 항목 | 결과 |
> |---|---|
> | `Kkultteok.prefab` | Missing Script 0 · `ReviveHoneyMist` 오브젝트 없음 |
> | `UI.prefab` | Missing Script 0 · `LocalDownOverlay`/`DeathDelayOverlayUI`/`ReviveInteractPrompt` 전부 없음 |
> | 플레이 경로 14개 씬 | 미해결 스크립트 GUID 0 · `StageResetOnPlayerDeath` 0 |
> | `T.Stage5` | `disableRevive: 1` ✅ |
> | `TeamLivesUI` | `icon` = `Icon` 연결됨 (`content`/`countText`도) |
> | `DeathUI` String Table | 5개 키 없음 |
>
> **⬜ 남은 것 — 전부 플레이 경로 밖이라 일부러 안 건드렸다.**
> `StageResetOnPlayerDeath` GUID(`79b2fab…`)가 아직 있는 파일 16개:
> `Assets/Scenes/Backup/*` 14개 · `Assets/Scenes/식도/T.Stage5 1.unity` · `Assets/_Recovery/0.unity`.
> 백업에서 지우면 백업이 아니게 되므로 사용자 판단 대상.
>
> **문서에 없던 잔여물 1건:** `Assets/Resources/Crowdin/CrowdinTranslations.asset`에
> `Down.Guide`·`Down.Reviving`·`Revive.Prompt`·`Revive.Casting`·`Revive.Cancelled` 5개가 남아 있다.
> String Table에선 지워졌고 참조 코드도 없어 **죽은 데이터**다(동작 영향 없음). Crowdin 파이프라인을
> 어떻게 돌리느냐에 달려 있어 손대지 않았다.

| 대상 | 할 일 | 확인된 위치 |
|---|---|---|
| `Assets/Prefab/Kkultteok.prefab` | Missing Script **2개** 제거 (구 `PlayerReviveInteract`·`ReviveHoneyVfx`) + `ReviveHoneyMist` 자식 처리 | 루트 `Kkultteok` |
| `Assets/Prefab/UI.prefab` | Missing Script **3개** 제거 | `LocalDownOverlay` · `DeathDelayOverlayUI` · `ReviveInteractPrompt` |
| **M/T 12개 씬** | `StageResetOnPlayerDeath` 오브젝트 제거 | `M.Stage1~5` · `M.Boss` · `T.Stage1~5` · `T.Boss` (백업 씬 `Assets/Scenes/Backup/*` 14개 + `식도/T.Stage5 1` + `_Recovery/0`도 같은 GUID를 들고 있으나 플레이 경로 밖) |
| **`T.Stage5` 씬** | `StageNetworkState`의 **`Disable Revive` 체크** — 안 켜면 T5에서 부활이 일어나고 팀 목숨이 깎인다(§7.1) | T.Stage5 |
| `UI.prefab`의 `TeamLives` | **`TeamLivesUI.Icon` 필드 연결**(현재 비어 있음). 비워두면 숫자만 강조되고 아이콘 펄스가 안 나온다(§11.3) | `TeamLives` (`content`/`countText`는 이미 연결됨) |
| `DeathUI` String Table | `Down.Guide`·`Down.Reviving`·`Revive.Prompt`·`Revive.Casting`·`Revive.Cancelled` 5개 제거 | — |
| 폰트 | **불필요.** 새 UI는 전부 아이콘·색이라 추가된 문구가 0개다(§11.3) — 삭제만 하면 베이킹은 안 돌려도 된다 | — |

> **프리팹 참조는 안 끊겼다.** `PlayerDownState` → `PlayerReviveState` 개명은 `.cs`와 `.cs.meta`를
> **같이** 옮겨 GUID(`0d67fff…`)를 유지했다. 프리팹의 `m_Script`는 클래스 이름이 아니라 그 GUID를
> 가리키므로 재배선이 필요 없다(에디터에서 `Kkultteok.prefab`의 `PlayerReviveState` 정상 인식 확인).
> 클래스에 붙인 `[MovedFrom]`은 타입 이름으로 해석되는 경로(SerializeReference·API Updater)까지
> 덮는 보험이다.

---

## 11. UI (2026-09-19 확정)

### 11.1 전부 삭제

부활이 1초 만에 끝나므로 **사망을 알리는 UI가 전부 무의미해진다.** 특히 생존자 화면에 배너가
뜨는 것은 산만하기만 하다(2026-09-19 사용자 판단).

| 대상 | 내용 |
|---|---|
| `ReviveInteractPromptUI.cs` | 시전자 "[E] 부활" / "부활 중" / "부활 취소" — **파일 삭제** |
| `LocalDownOverlayUI.cs` | 본인 다운 화면 회색 막 + 초 카운트다운 + 안내 문구 — **파일 삭제** |
| `DeathOverlayUI.cs` | "OO 사망" 배너 — **파일 삭제.** 1초 뒤 살아나는데 남의 사망을 배너로 알릴 이유가 없다 |
| `ReviveHoneyVfx.cs` + `ReviveHoneyMist` 프리팹 | 시전 연출 — 시전 자체가 없다 |
| `TeamStatusUI`의 `downIndicator`(HELP 아이콘) | 다운 표시 |
| `TeamStatusUI`의 `downTimerText` | 초 카운트다운 |
| `TeamStatusUI`의 사망 표시(`SetDead`) | 1초짜리라 깜빡임만 된다 |
| `DeathUI` String Table 5개 | `Down.Guide`·`Down.Reviving`·`Revive.Prompt`·`Revive.Casting`·`Revive.Cancelled` |

**사망 피드백은 die 애니메이션 하나로 충분하다.**

### 11.2 남는 것

| 대상 | 역할 |
|---|---|
| **`TeamLivesUI`** | **팀 공유 목숨 — 이 시스템에서 유일하게 의미 있는 상시 UI**(§11.3) |
| `PlayerHPUI` | 내 하트. 부활 시 0→3이 `OnHealed`로 자동 갱신돼 **손댈 것 없음** |
| `TeamStatusUI` | 팀원 이름 + HP만. HP 0→3도 자동 |
| `StageFailedBannerUI` | STAGE FAILED 2초. 실패는 여전히 알려야 하는 사건이라 유지 |

### 11.3 ⚠️ 목숨 UI에 "소모 강조"가 없으면 시스템이 안 보인다

**문제:** 목숨은 이 게임의 유일한 실패 자원인데, 소모되는 장면을 **아무도 못 볼 가능성이 크다.**

- **본인**은 죽고 1초 만에 맵 반대편으로 옮겨진다 — HUD 구석 숫자를 볼 여유가 없다
- **팀원**은 저 멀리서 일어난 일이라 화면에 아무 변화가 없다. 숫자만 조용히 3→2가 된다

그러면 "언제 왜 목숨이 줄었는지" 아무도 모른 채 어느 순간 0이 되고 스테이지가 끝난다.
§1.2에서 협동 요소(살리러 가기)를 포기하면서 **팀 긴장을 목숨 하나에 몰아줬는데**, 그게 안 보이면
긴장 장치가 통째로 작동하지 않는다.

**확정 (2026-09-19) — 신규 UI 0개, `TeamLivesUI` 하나에만 얹는다:**

| # | 내용 |
|---|---|
| **1** | **소모 순간 강조** — 목숨이 줄어드는 프레임에 아이콘을 **1초간 펄스 + 색 플래시**. **전원 화면에서 동일하게.** 이거 하나면 "누군가 죽었고 목숨이 깎였다"가 전달된다 |
| **2** | **0 경고** — 목숨이 0이면 **상시 빨강** 유지. "다음에 죽으면 끝"이 이 게임의 실질 긴장 지점이다 |

> **이 둘 외에는 추가하지 않는다**(2026-09-19 사용자 확정). 사망 SFX·화면 플래시도 이번 범위 아님.

**구현(2026-09-19 완료):** `TeamLivesUI`는 `ReadVisibleLives()`를 매 프레임 폴링해 값이 바뀔 때만
텍스트를 갱신한다(`_shown` 비교). **그 비교 지점이 곧 "소모 순간"이라** 거기서 펄스를 시작한다 —
Client는 NV 초기값이 스폰 데이터로 오면 `OnValueChanged`가 안 불리므로 폴링 구조를 유지했다.
펄스 조건은 `_shown >= 0 && lives < _shown`, 즉 **감소일 때만** — 최초 확정(-1 → 인원−1)과 스테이지
진입 시 값 세팅에서는 울리지 않는다. 튜닝 값(`pulseDuration`/`pulseScale`/`pulseColor`/`zeroColor`)은
전부 Inspector 노출. ⬜ `icon` 필드 연결은 에디터 작업으로 남아 있다(§10.1).

**솔로는 계속 숨긴다(2026-09-19 확정).** `ReadVisibleLives()`가 `EntryCount <= 1`이면 -1을 반환하는
기존 동작 유지. 솔로는 목숨이 항상 0이라 ②(0 경고)를 그대로 적용하면 **처음부터 끝까지 빨간 0**이
떠 있게 되는데, 그건 긴장이 아니라 배경이 된다. §4의 "모든 스테이지에서 표시한다"는 미니게임
스테이지를 두고 한 말이고 솔로 예외와 충돌하지 않는다.

> **문구가 하나도 없다** — 전부 아이콘·색이라 로컬라이제이션도, Noto Static 베이킹도 필요 없다.
> "누가 죽었는지"는 일부러 안 알린다. 어차피 즉시 부활하므로 **행동에 영향을 주지 않는 정보**다.

---

## 12. 폐기 이력

<details>
<summary>구 설계 — 다운 + 팀원 살리기 (2026-09-14 ~ 2026-09-19)</summary>

HP 0에서 **다운** 상태에 들어가 10초 카운트다운을 돌고, 팀원이 근접해 **E를 2초 홀드**하면 부활했다.
콜라이더를 유지해 시체가 통로를 막았고, 다운 중에도 TeamCheer만 유효했다.

시전 캔슬 조건이 세 갈래였다 — Host가 시전자 위치 밀림(`reviveMoveTolerance` 0.3m, 시작 유예 0.2초)과
사거리 이탈을 매 프레임 판정, HP 감소는 `CancelIfReviving` 훅, E 해제·타 버튼 입력은 Owner가
`RequestCancelReviveServerRpc`로 신고. 시작·캔슬 RPC가 dedup 번호열 하나를 공유해야 했다.

부활 직후 HP 2칸 + 1초 무적. 팀 목숨은 **부활 완료 시** 소모했고, 소모로 0이 되면 다운 중인
나머지 전원을 즉시 완전사망시키는 `FailAllOtherDowned` 규칙이 있었다.

**폐기 이유는 §1.2.** 같이 폐기된 것: `PlayerReviveInteract` · `ReviveInteractPromptUI` · `ReviveHoneyVfx` ·
`LocalDownOverlayUI` · `Player.IsDowned` · `EnterDownState`/`ExitDownState` · `TeamStatusUI` 다운 표시 ·
`StageResetOnPlayerDeath` · 전원 즉사 실패 경로.

</details>

<details>
<summary>구 설계 — 3초 유예 + 카메라 연출 (2026-09-19 당일 폐기)</summary>

사망 후 3초 유예를 두고 그동안 카메라가 **부활 위치(생존자)를 따라다니며** 비추다가, 3초 뒤 시체 렌더러를
끄고 부활 위치에 나타나는 안. 부활 위치는 생존자의 **발자국 링버퍼**(0.25초 간격, 최근 3초)에서
현재 위치로부터 1~2m 떨어진 최근 지점을 골라 "사람이 실제로 서 있던 자리"라 검증이 불필요하다는 설계였다.

**1초 + 생존자 위치 그대로**로 교체 — 발자국 버퍼·8방향 탐색·레이캐스트·카메라 추적이 전부 사라졌다.
생존자 위치를 그대로 쓰면 유효성 보장은 동일하면서 계산이 0이다.

</details>
