# Trap Network Board

> **역할:** `Assets/Scripts/Traps/*.cs` 전체를 "네트워크 필요 vs 로컬"로 분류하고, 아직 결론 안 난/애매한 항목을 여기서 계속 논의한다.
> 확정된 결론은 [`NetworkDesign.md`](NetworkDesign.md) §9.1(패턴 B — 함정·피격)로 승급한다. (§9.1.3 표에도 이미 M/T 스코프 구분이 있음 — 이 보드는 그 표의 하위 작업 로그.)
> **분류 기준(확정):** RPC 송수신 / NetworkVariable 사용 / Spawn·Despawn으로 존재·소멸 동기화 — 이 중 **하나라도 필요하면 네트워크**(`NetworkObject`+`NetworkBehaviour`), **전부 아니면 로컬**(`MonoBehaviour`).

---

## 1. 분류표 (`Assets/Scripts/Traps/*.cs`)

| 파일 | 베이스 | 네트워크 필요? | 비고 |
|------|--------|----------------|------|
| `TrapBase.cs` | MonoBehaviour | 로컬 | 하위 클래스가 `TrapLoop()` 오버라이드 안 하면 로컬 프레임 타이머(`WaitForSeconds`) 그대로 씀 — ServerTime 앵커 없음 |
| `ArrowTrap.cs` | `TrapBase` | 로컬 | `TrapLoop` 오버라이드해서 `StageStartServerTime` 앵커드 — 발사체(`arrowPrefab`)만 `NetworkObject` 필요 |
| `DropTrap.cs` | `TrapBase` | 로컬 | 동일하게 `StageStartServerTime` 앵커드 — `dropPrefab`만 `NetworkObject` 필요 |
| `WindTrap.cs` | `TrapBase` | 로컬 | `StageStartServerTime` 앵커드, 데미지 없음(포스만) — 문제 없음 |
| `SpikeTrap.cs` | `TrapBase` | 로컬 | 데미지는 `nm.IsServer` 가드로 보호(OK). **2026-07-21 수정 완료** — `TrapLoop()`를 `StageStartServerTime` 앵커 방식으로 오버라이드 (§2 참조) |
| `ContactDamage.cs` | MonoBehaviour | 로컬 | 데미지는 `nm.IsServer` 가드로 보호(OK) |
| `SpikeLaneField.cs` | `TrapBase` | 로컬 | **2026-07-21 수정 완료** — `TrapLoop()` 앵커링 + `NetworkSessionData.Seed` 기반 `Random.InitState`로 레인 선택 동기화 (§2 참조). **실기 테스트 아직 안 함** |
| `SpikeLane.cs` | MonoBehaviour | 로컬 | 단순 Activate/Deactivate 릴레이 |
| `CeilingTrap.cs` | MonoBehaviour | 로컬 | `TrapBase` 아님, `Update()` 감지형. 데미지는 별도 컴포넌트(`ContactDamage` 등)가 처리할 것으로 **추정 — 미확인, §3 논의 필요** |
| `TrapPlayerTracker.cs` | MonoBehaviour | 로컬 | — |
| `TrapProjectile.cs` | `NetworkBehaviour` | **네트워크 필요** | 데미지 + Despawn 복제 필수 |
| `MouthTrapAnimator.cs` | MonoBehaviour | 로컬 | — |
| `MouthTrapAnimatorAnim.cs` | MonoBehaviour (2026-07-21 NetworkBehaviour→전환 완료 / 2026-07-22 Close 고정 타이머화) | 로컬 | Host→Client RPC relay 3개 제거, 각 피어가 `TrapBase` 이벤트를 로컬로 직접 트리거. Close는 `holdDuration` 고정 로컬 타이머 — `MouthExitTrigger`(발사체 탈출 이벤트) 의존 제거, Client Hold 과다 지연 버그 수정 |
| `MouthExitTrigger.cs` | — | — | **삭제됨 (2026-07-22)** — Close를 발사체 탈출 이벤트에 묶으면 Host만 Spawn하는 B안 구조상 Client Hold가 Spawn/RPC RTT만큼 길어지는 문제 → `MouthTrapAnimatorAnim`의 고정 타이머로 대체 |
| `MouthWindAnimator.cs` | MonoBehaviour | 로컬 | — |
| `TrapSpeedPhase.cs` (`SpeedPhase`) | 데이터 구조체 | 해당없음 | `ArrowTrap`/`DropTrap`/`WindTrap` 공용 파라미터 |

---

## 2. T.Stage 관련 — 스코프 순서 앞질러 먼저 수정한 것 (2026-07-21)

**배경:** `NetworkDesign.md` §9.1.3 표에 `SpikeLane`/`SpikeLaneField`가 **"T 전용 — 별도 라운드로 미룸"** (`T.Stage3`, `T.Boss`만 확인됨)으로 명시돼 있었음. 이번 정리 라운드는 M 스코프였지만, 진단 중 체감 버그 가능성이 확인돼 스코프 순서와 무관하게 먼저 수정함.

**문제 1 — `SpikeLaneField` 레인 선택 시드 불일치:**
`PickRandomIndices()`가 공유 시드 없이 전역 `Random.Range()`를 호출 → Host/Client가 각자 다른 레인을 뽑을 수 있었음. 데미지 판정 자체는 `SpikeTrap.TryDamagePlayer()`의 `nm.IsServer` 가드로 안전하지만, Client 화면엔 안 올라온 가시인데 Host 판정으로 맞는 "안 보이는 가시에 맞는" 체감 버그 가능성.

**문제 2 — `SpikeTrap`/`SpikeLaneField`가 `TrapLoop()` 미오버라이드:**
base의 로컬 상대 타이머(`WaitForSeconds`)를 그대로 써서 Host/Client 발동 시각이 서서히 벌어질 수 있었음. `ArrowTrap`/`DropTrap`/`WindTrap`은 이미 `StageStartServerTime` 앵커링 패턴을 쓰고 있었는데 이 둘만 안 쓰고 있었음.

**수정 내용:**
1. `SpikeTrap.cs`, `SpikeLaneField.cs` 둘 다 `TrapLoop()`를 오버라이드해 `StageNetworkState.StageStartServerTime`(ServerTime 절대 기준)에 앵커링 — `ArrowTrap`/`DropTrap`/`WindTrap`과 동일 패턴, 사이클 인덱스 기반 절대 목표 시각 계산.
2. `SpikeLaneField.OnTrapTrigger()`에서 레인을 뽑기 직전에 `UnityEngine.Random.InitState(NetworkSessionData.Seed ^ salt ^ fireCount)`로 로컬 RNG 초기화 → `StagePressurePadSetup.ApplySeedAndColors()`와 동일한 "Seed ^ salt" 관례. `_fireCount`는 `OnDeactivated()`/`OnDisable()`에서 리셋.

**손댄 파일:** `Assets/Scripts/Traps/SpikeTrap.cs`, `Assets/Scripts/Traps/SpikeLaneField.cs` (Cause-Site Only — `TrapBase.cs`는 건드리지 않음).

**테스트 상태:** ⬜ 미완료. T 라운드 진입 시 ParrelSync 2인(또는 Build+Editor)으로 `T.Stage3`/`T.Boss`에서 매 발동마다 같은 레인이 동시에 올라오는지, 여러 사이클 반복해도 드리프트 없는지 확인 필요.

---

## 3. 남은 논의 항목 (다음에 계속)

- [ ] §2 수정사항 T 라운드 실기 검증 (ParrelSync 2인)
- [ ] `CeilingTrap.cs` 데미지 처리 컴포넌트가 실제로 뭔지 확인 (현재 "추정"만 있음)
- [ ] `Assets/Scripts/Traps/` 밖의 다른 함정류(`WallMover`/`WallMoverSequencer`/`BoulderSpawner`/`Breakable` 등, `NetworkDesign.md` §9.1.3 그룹 2/미분류)도 같은 3가지 기준으로 분류할지 — 스코프 확대 여부는 사용자 확인 필요
- [ ] (계속 여기에 추가)

---

## 4. Board → Docs 승급 규칙

| Board (여기) | NetworkDesign |
|--------------|---------------|
| 후보·논의·미결 | 확정 lock |
| 개별 파일 진단/수정 로그 | §9.1 분류표에 한 줄로 고정 |
| 구현 중 변경 | 승급 후에만 Docs(§9.1) 수정 |
