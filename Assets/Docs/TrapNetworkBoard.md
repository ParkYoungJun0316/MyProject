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
| `WindTrap.cs` | `TrapBase` | 로컬 | **2026-07-23 수정 완료** — 스케줄 앵커를 로컬 `Activate()` 시각 → `PhaseStartServerTime`으로 교체(ArrowTrap/DropTrap과 동일 패턴), Random 모드는 `NetworkSessionData.Seed ^ salt ^ fireCount` 결정적 시드로 동기화(RPC 없음), `AddForce`는 `IsLocalOwnerRigidbody()` 필터로 자기 Owner 캐릭터에만 적용. **실기 검증 완료(ParrelSync 2인)** |
| `SpikeTrap.cs` | `TrapBase` | 로컬 | 데미지는 `nm.IsServer` 가드로 보호(OK). **앵커 결함 수정 완료 (2026-07-24, 커밋 `2706cf4`)** — `TrapLoop()`가 `WindTrap`과 동일하게 `PhaseStartServerTime` 앵커로 교체됨(코드 재확인, 2026-08). §5 참조 — **ParrelSync 2인 실기 검증 통과 완료 (2026-08)**. **별도 NRE 수정 완료 (2026-08)** — §6 참조 |
| `ContactDamage.cs` | MonoBehaviour | 로컬 | 데미지는 `nm.IsServer` 가드로 보호(OK) |
| `SpikeLaneField.cs` | `TrapBase` | 로컬 | 레인 선택 시드(`NetworkSessionData.Seed ^ salt ^ fireCount`)는 정상. **앵커 결함 수정 완료 (2026-07-24, 커밋 `2706cf4`)** — `TrapLoop()`가 `PhaseStartServerTime` 앵커로 교체됨(코드 재확인, 2026-08). §5 참조. **ParrelSync 2인 실기 검증 통과 완료 (2026-08)** |
| `SpikeLane.cs` | MonoBehaviour | 로컬 | 단순 Activate/Deactivate 릴레이 |
| `CeilingTrap.cs` | MonoBehaviour | 로컬 | `TrapBase` 아님, `Update()` 감지형. 데미지는 별도 컴포넌트(`ContactDamage` 등)가 처리할 것으로 **추정 — 미확인, §3 논의 필요** |
| `TrapPlayerTracker.cs` | MonoBehaviour | 로컬 | — |
| `TrapProjectile.cs` | `NetworkBehaviour` | **네트워크 필요** | 데미지 + Despawn 복제 필수 |
| `MouthTrapAnimator.cs` | MonoBehaviour | 로컬 | **2026-07-27 수정** — `PlayChargeById`/`PlayFireById`가 `MouthTrapAnimatorAnim`만 찾아 BlendShape 버전(Boss `ArrowTrap.prefab`)에서 RPC가 no-op이었음(티켓 A). `MouthTrapAnimatorAnim`/`MouthWindAnimator`와 동일 패턴으로 통일: Client 로컬 구독 제거(`IsServer` 가드) + `PlayOpenFromNetwork`/`PlayHoldFromNetwork` RPC 진입점 추가, `ArrowTrap`이 두 컴포넌트를 모두 시도하도록 수정 |
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

- [ ] §5 코드 반영은 완료(2026-07-24, 커밋 `2706cf4`) — **T3 라운드 실기 검증(ParrelSync 2인)만 남음** — §2의 옛 항목을 대체
- [ ] `CeilingTrap.cs` 데미지 처리 컴포넌트가 실제로 뭔지 확인 (현재 "추정"만 있음)
- [ ] `Assets/Scripts/Traps/` 밖의 다른 함정류(`WallMover`/`WallMoverSequencer`/`BoulderSpawner`/`Breakable` 등, `NetworkDesign.md` §9.1.3 그룹 2/미분류)도 같은 3가지 기준으로 분류할지 — 스코프 확대 여부는 사용자 확인 필요
- [ ] (계속 여기에 추가)

---

## 5. SpikeLaneField / SpikeTrap — WindTrap과 동일한 앵커 결함 (**코드 반영 + ParrelSync 2인 실기 검증 통과 완료, 2026-08**)

**상태 (2026-08 재확인 + 검증):** 아래 배경에서 지적한 결함은 **커밋 `2706cf4`(2026-07-24, "stage3,4마무리 단계 + stage5진입")에서 이미 수정 완료됨.** 당시 코드만 고치고 이 보드의 "테스트 상태"를 갱신하지 않아 문서가 stale했던 것 — `SpikeTrap.TrapLoop()`/`SpikeLaneField.TrapLoop()` 둘 다 아래 §5 처방(`WindTrap` 앵커 블록)과 동일한 코드가 이미 들어가 있음을 실제 소스로 재확인(2026-08). **`T3` ParrelSync 2인 실기 검증 통과 완료 (2026-08)** — 추가 코드 작업 불필요, 이 항목은 종료.

**배경 (당시 문제, 참고용):** §2에서 "`StageStartServerTime` 앵커링 완료"라고 적었지만, 실제 코드(`SpikeLaneField.TrapLoop`, `SpikeTrap.TrapLoop`)는 `StageStartServerTime`도 `PhaseStartServerTime`도 안 쓰고 **이 트랩이 로컬로 `Activate()`된 순간의 `ServerTime.Time`**만 스냅샷으로 잡았다. `WindTrap`이 최근까지 똑같은 방식이었고(2026-07-23 수정 완료 · 실기 검증 통과), `ArrowTrap`/`DropTrap`과 같은 `PhaseStartServerTime` 앵커로 교체했다.

**왜 문제인가:** Client의 `Activate()` 호출은 Phase 진입 NetworkVariable 전파 지연만큼 Host보다 늦게 일어날 수 있다. 각자 자기 `Activate()` 시각을 앵커로 잡으면, `scheduleStartTime`이 머신마다 달라져서 발동 "시각"이 서서히 어긋난다. `SpikeLaneField`의 레인 선택 시드(`NetworkSessionData.Seed ^ salt ^ fireCount`)는 값 자체는 맞지만, `fireCount`가 증가하는 **시점**이 머신마다 갈라지면 결국 같은 시드가 다른 시각에 적용될 수 있다.

**증거 (수정 전 코드, 참고용 — 현재 코드 아님):**

```text
    protected override IEnumerator TrapLoop()
    {
        var nm = NetworkManager.Singleton;
        float scheduleStartTime;

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
        scheduleStartTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
```

`SpikeTrap.TrapLoop()`도 완전히 동일한 패턴이었음. **현재는 둘 다 아래 처방 그대로 `PhaseStartServerTime` 앵커로 교체돼 있음** (`SpikeTrap.cs` 66~100줄, `SpikeLaneField.cs` 75~109줄, 2026-08 재확인).

**해야 할 일 (Cause-Site Only — 이 두 `TrapLoop()`만 교체) — 완료, 아래는 처방 기록용:**

`WindTrap.TrapLoop()`(수정 완료본, `Assets/Scripts/Traps/WindTrap.cs`)의 앵커 결정 블록을 그대로 이식:

```csharp
if (StageNetworkState.Instance != null && StageNetworkState.Instance.PhaseStartServerTime > 0)
{
    scheduleStartTime = (float)StageNetworkState.Instance.PhaseStartServerTime + initialDelay;
    while (nm != null && (float)nm.ServerTime.Time < scheduleStartTime)
        yield return null;
}
else
{
    if (initialDelay > 0f)
        yield return new WaitForSeconds(initialDelay);
    scheduleStartTime = nm != null ? (float)nm.ServerTime.Time : Time.time;
}
```

- `SpikeLaneField`/`SpikeTrap` 둘 다 `scheduleStartTime`이 지역 변수(필드 아님)이므로 그대로 대입만 바꾸면 됨 — `WindTrap`처럼 필드로 승격할 필요 없음(다른 곳에서 재사용 안 함).
- **손대지 말 것:** `OnTrapTrigger()`의 시드 로직(`fireCount`/`NetworkSessionData.Seed`)은 이미 정상 — 건드리지 않는다. `TrapBase.cs`, `SpikeLane.cs`도 건드리지 않는다.
- 위 표(§1)의 "실제 구현은 아님" 메모는 2026-08 갱신 완료.

**테스트 (아직 미완료 — T3 착수 시 실행):** ParrelSync 2인, `T.Stage3`/`T.Boss`에서 여러 사이클 반복 — 매 발동마다 Host/Client 동시에 같은 레인이 올라오는지, 시간이 지나도 드리프트 없는지 확인 (§3 체크박스와 동일).

---

## 6. SpikeTrap — Client 씬 로드 중 NullReferenceException (수정 완료, 2026-08)

**증상:** T.Stage3에서 Client 콘솔에 `NullReferenceException: SpikeTrap+<RaiseCycle>d__15.MoveNext () (spikeTrigger.enabled = true)`. Host는 재현 안 됨.

**원인:** Unity 비동기 씬 로드에서 `AsyncOperation.completed` 콜백은 씬의 `Awake()`/`OnEnable()` 이후지만 **`Start()`보다 먼저** 실행될 수 있다. NGO의 `NetworkSceneManager.OnClientLoadedScene()`이 이 콜백 안에서 씬 배치 `NetworkObject`(`StageNetworkState`)를 역직렬화하며 `_currentPhase` NetworkVariable의 `OnValueChanged`(`OnPhaseChanged`)를 동기 호출 → `PhaseManager.EnterPhaseOnClient()` → `onPhaseEnter` → `StageManager.StartStage()` → `TrapBase.Activate()` → `SpikeTrap.TrapLoop()`까지 전부 같은 콜스택에서 동기 실행됨. `PhaseStartServerTime`이 이미 과거 시각이면(Host가 이 Phase를 먼저 진행 중) 코루틴이 한 번도 yield 안 하고 곧장 `RaiseCycle()`까지 진행 — 이 메서드 첫 줄이 `Start()`에서만 세팅되는 `spikeTrigger`를 참조해 Client의 `Start()`가 아직 안 돈 시점엔 `null`.

`ArrowTrap`/`DropTrap`/`WindTrap`은 트리거 직후 첫 동작이 Inspector 직렬화 필드나 `NetworkManager.Singleton`만 참조해 이 레이스에 노출되지 않음 — `SpikeTrap`만 `Start()` 캐시 필드를 코루틴 첫 줄에서 바로 사용해 문제가 됨.

**수정 내용:** `Start()`의 초기화(`spikeTrigger`/`spikeTriggerBox`/`loweredLocalPos`/`baseColliderCenter` 세팅)를 `EnsureInitialized()`로 분리, `Start()`와 `RaiseCycle()` 시작부 양쪽에서 멱등하게 호출 — `Start()` 타이밍과 무관하게 안전.

**손댄 파일:** `Assets/Scripts/Traps/SpikeTrap.cs` (Cause-Site Only). `SpikeLaneField` 자식으로 배치된 `SpikeTrap`도 같은 파일 수정으로 함께 커버됨.

**별개 관찰 → 실제로 재현됨, 해결 완료 (2026-08):** 위에서 우려했던 대로 `T.Stage3`에서 `SpikeLaneField`가 `StageManager`의 직계 자식으로 배치돼 있었고, `TrapBase.Awake()`가 계층 깊이 무관하게 등록하는 탓에 `StageManager.StartStage()`가 `SpikeLaneField` 하위 모든 `SpikeTrap` 타일을 직접 `Activate()`해버려 "레인 선택 후 발동" 로직이 무력화되는 증상(스테이지 시작 시 전체 레인 동시 발동 후 영구 정지)이 실제로 확인됨. **해결:** `SpikeLaneField`를 `StageManager` 자식 계층에서 씬 루트로 이동(Editor 작업, 코드 변경 없음) — `TrapLoop()`가 이미 `PhaseStartServerTime` 앵커를 쓰므로 등록 여부와 무관하게 스케줄은 정상 동작. 별개로 `T.Stage3`의 `SpikeLaneField.activateInterval`이 `0`으로 설정돼 있어 1회 발동 후 반복이 안 되는 씬 설정 문제도 같이 발견 — Inspector에서 값 설정으로 해결(코드 변경 없음).

**테스트:** **ParrelSync 2인(Host+Client) 검증 통과(2026-08).** **Build(Editor+Build 조합) 검증은 아직 안 됨** — 남은 항목.

---

## 7. BoulderSpawnManager — OnEnable() 자동 시작이 NGO 스폰보다 먼저 실행되는 레이스 (수정 완료, 2026-08)

**증상:** `T.Stage3`에서 `StageStartGate`로 스테이지를 시작해도 Boulder가 전혀 스폰되지 않음(Host 포함).

**원인:** `BoulderSpawnManager.OnEnable()`이 `startSpawningOnEnable=true`일 때 곧바로 `BeginSpawning()`을 호출했는데, `BeginSpawning()`은 `NetworkBehaviour.IsServer`(NGO가 스폰 처리 시점에 세팅하는 `private set` 프로퍼티)로 Host 여부를 가드한다. `T.Stage3`의 `BoulderSpawnManager` GameObject는 씬에 `m_IsActive: 1`(처음부터 활성)로 배치돼 있어 `OnEnable()`이 씬 로드 도중 동기적으로 즉시 실행되는데, 이 시점은 NGO가 이 씬 배치 `NetworkObject`를 스폰 처리해 `IsServer`를 세팅하기 **이전**일 수 있다 — `SpikeTrap`의 `Start()` 레이스(§6)와 동일한 종류의 "Unity 라이프사이클이 NGO 스폰 파이프라인보다 먼저 도는" 문제. `IsServer`가 아직 기본값 `false`라 `BeginSpawning()`의 Host 가드에 걸려 스폰 루프가 시작조차 안 되고, 이후 아무도 재호출을 안 해 영구 정지. (`T.Stage1`의 동일 컴포넌트는 GameObject가 `m_IsActive: 0`으로 배치돼 있어 나중에 다른 트리거로 활성화되므로 이 레이스를 우연히 피해갔던 것 — 코드 자체는 T1/T3 동일.)

**수정 내용:** 자동 시작 훅을 `OnEnable()`에서 NGO 콜백 `OnNetworkSpawn()`(NGO가 스폰을 끝낸 뒤 호출을 보장)으로 이동. `OnEnable()`은 `_triggerFired` 리셋만 유지.

**손댄 파일:** `Assets/Scripts/Stage/BoulderSpawnManager.cs` (Cause-Site Only).

**영향 범위:** `BoulderSpawnManager`를 쓰는 다른 씬(`T.Stage1` 등)에도 코드가 공유되므로 동일하게 적용됨 — T1은 애초에 이 레이스를 안 밟는 배치라 회귀 위험 낮음.

**테스트:** **ParrelSync 2인(Host+Client) 검증 통과(2026-08).** **Build(Editor+Build 조합) 검증은 아직 안 됨** — 남은 항목.

---

## 4. Board → Docs 승급 규칙

| Board (여기) | NetworkDesign |
|--------------|---------------|
| 후보·논의·미결 | 확정 lock |
| 개별 파일 진단/수정 로그 | §9.1 분류표에 한 줄로 고정 |
| 구현 중 변경 | 승급 후에만 Docs(§9.1) 수정 |
