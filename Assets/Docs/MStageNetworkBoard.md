# M Stage Network Board

> **역할:** 미확정 파이프라인을 여기서 잡고, **확정되면 [`NetworkDesign.md`](NetworkDesign.md) §9 / §9.1로 승급**한다.  
> (예: 발사체 **B안** — 보드·논의 → Docs 고정.)  
> **빈 체크리스트 전용이 아님.** 큰 틀을 정하기 위한 작업 md.

**현재 인게임 최우선:** M.Stage 네트워크 완료 (`NetworkDesign` §9.1).  
**현재 보드 포커스:** `GridBWTileChallenge` / `GridColorChallenge` / `SequenceRingMinigame` / `Floor` **전부 코드 반영 + ParrelSync 2인 검증까지 완료 (2026-07-25).** OX/ColorTile(2026-07-21·22)에 이어 나머지 챌린지 4개도 전부 통과 → [`NetworkDesign.md`](NetworkDesign.md) §11B.7(챌린지)·§11B.8(Floor)로 승급 완료. **이 보드가 추적하던 M.Stage 네트워크 축 작업은 여기서 종료 — 다음 보드 포커스는 `M.Boss` (2026-07-26 확정, "다음 세션 시작점" 참고).** 이 보드의 §1(축 골격)·§2(OX 개별 잠금 규칙)는 §11B가 SSOT이며, 아래 §1~§4는 **승급 완료 기록**으로만 남긴다.

---

## ✅ M.Stage 스폰 위치 버그 — 해결 (2026-08-17)

**증상 (2026-08-13, Steam 빌드 3~4인 테스트):** `M.Stage`(2~5, Boss 포함) Client 진입 시 스폰 직후 1프레임 만에 `(-167.18, *, -20.28)`(월드 원점과 무관한 임의 좌표)로 튀어 낙사.

**원인:** `Player1.prefab` 루트 `m_LocalPosition`이 `(-167.18423, 0, -20.28356)`으로 저장돼 있었음(과거 `2.Tutorial.unity`에 배치한 인스턴스를 Apply to Prefab 하면서 그 씬 좌표가 프리팹 원본에 박제됨). Host는 `Instantiate(prefab, e.SpawnPos)`로 위치를 직접 지정해 이 값을 거치지 않지만, Client는 프리팹 기본 포즈로 인스턴스화 후 스폰 메시지로 Transform만 보정한다 — 이때 `Rigidbody`(`isKinematic=false`, `Interpolate=on`)의 물리 포즈가 프리팹에 박힌 좌표에 남아있다가 다음 물리 틱에 Transform을 그 값으로 되돌리는 1프레임 워프가 발생했다. `NetworkPlayerSetup.EnablePhysics()`가 `velocity`만 리셋하고 `rb.position`을 스폰 위치에 맞추지 않았던 게 원인.

**수정:**
1. `Player1.prefab` 루트 좌표 `(0,0,0)`으로 원복 (사용자, 에디터).
2. `Assets/Scripts/Network/NetworkPlayerSetup.cs` `EnablePhysics()`에 `_rb.position = transform.position; _rb.rotation = transform.rotation;` 추가 — 스폰 메시지로 이미 확정된 Transform에 물리 바디를 맞춤(Writer는 여전히 `PlayerSpawnManager` 하나, 좌표 재계산 없음).
3. 조사용 `TEMP DIAG` 블록(`PlayerSpawnManager.SpawnNetworkPlayers()`의 진입횟수/레이캐스트 로그, `NetworkPlayerSetup`의 `DiagTrackSpawnPlacement()` 코루틴 전체) 제거 완료.

**postmortem:** `NetworkDesign.md` §11.8에 기록 (Player 스폰/물리 계층이라 M/T 공유 — 라운드 보드가 아닌 §11에 직접 기재).

---

## 현재 상태 (다음 세션 시작점 — 여기부터 읽을 것)

**요약:** 축 #4 골격 확정(§1) → OX 코드 구현 완료 → ParrelSync 2인 발테스트에서 문제 동기화 버그 1건 발견·수정 → **재테스트 통과 (2026-07-21) → `NetworkDesign.md` §11B로 승급 완료.** → `ColorTileChallenge` 동일 축 복제 코드 반영 (2026-07-22) → **ParrelSync 2인 재테스트 통과 (2026-07-22)**: 동일 스폰 위치/색, 성공·실패 동시 판정, 실패 시 벽 전진 동기화 전부 확인됨. → `GridBWTileChallenge`/`GridColorChallenge`/`SequenceRingMinigame` 동일 축 복제 코드 반영 (2026-07-22, 아래 상세) → `Floor` 마이그레이션(`NetworkBehaviour`→`MonoBehaviour`, `SyncTilesClientRpc(byte[])` 폐기 → 시드 전용 슬롯) 코드 반영 (2026-07-25, 아래 `### Floor 마이그레이션 반영 내용` 참고) → **이 4개 전부 ParrelSync 2인 검증 통과 (2026-07-25)** → `NetworkDesign.md` §11B.7(챌린지 3개)·§11B.8(Floor)로 승급 완료. **OX/ColorTile/GridBW/GridColor/SequenceRing/Floor 전부 완료 — 이 보드가 추적하던 축 작업은 종료.**

**다음 세션 시작점:**

**M.Stage 챌린지 축(OX/ColorTile/GridBW/GridColor/SequenceRing) + Floor 마이그레이션 — 전부 코드 반영 + ParrelSync 2인 검증 완료 (2026-07-25).** `NetworkDesign.md` §11B.7/§11B.8로 승급 완료. 이 보드가 잡고 있던 미확정 파이프라인은 전부 확정·승급됐으므로, **다음 보드 포커스는 `M.Boss` (2026-07-26 사용자 확정)** — Steam 페이지 개설 지연으로 Steamworks 대기, 그 공백에 오픈 코스(M1–5→M.Boss) 네트워크를 먼저 닫는다. T 라운드는 그다음.

### M.Boss 라운드 범위 (2026-07-26 확정)

`M.Boss`의 SequenceRing/GridBW는 챌린지 축에서 이미 검증 완료 — 재작업 금지. 남은 것:

1. **`BossFightObjective` (D — 보스 진행 축):** 현재 일반 MonoBehaviour + 로컬 카운터(`_phasesCleared`)·로컬 `phaseManager.AdvancePhase()` 호출 — Host 판정 + 상태 복제로 전환 필요. `BossHealthBarUI`는 표시 전용(이벤트 구독만)이라 UI 쪽 네트워크 코드는 불필요 (§11A 계약 그대로).
2. **`DirectionalBarrier` / `PhaseSurviveChallenge`:** C 축(§11B) 복제인지 새 계약인지 판별 후 기존 파이프에 연결.
3. **Drop / Wind:** 기존 M 패턴 확정분 — 보스 인스턴스 스모크만.
4. **M 풀코스 ParrelSync 2인:** Stage1→…→Boss 연속 1회.

### `ChallengeOwner` 소유자 가드 — A-B-C-A 순환 회귀 수정 (코드, 2026-07-28)

**증상:** 챌린지 하나(A)를 고치면 다른 챌린지(B)가 새로 깨지고, B를 고치면 C가 깨지는 식으로 순환하는 회귀. `NetworkDesign.md` §11B.9에 상세 원인·수정 내용 SSOT로 기록.

- **원인**: `_challengeStep`(§11B.2 공유 슬롯)과 `_currentPhase`(`PhaseManager`, 오브젝트 on/off)가 별도 NV라 Client 도착 순서가 보장되지 않음 — 아직 안 꺼진 이전 챌린지가 새 챌린지의 `stepIndex`를 자기 것으로 오인해 반응하는 레이스였다.
- **수정**: `Assets/Scripts/Network/StageNetworkState.cs`에 `ChallengeOwnerType` enum(`None`/`OX`/`ColorTile`/`GridColor`/`GridBW`/`SequenceRing`/`DirectionalBarrier`) 신설, `ChallengeStepState`에 `owner` 필드 추가, `ChallengeStart(seed)` → `ChallengeStart(seed, owner)`로 시그니처 변경(`ChallengeStepBegin`/`ResetChallengeStep`은 기존 owner 값 유지), `ChallengeOwner` 읽기 프로퍼티 추가.
- 6개 챌린지 매니저(`OXQuizManager`/`ColorTileChallenge`/`GridColorChallenge`/`GridBWTileChallenge`/`SequenceRingMinigame`/`DirectionalBarrierRound`) 전부: `ChallengeStart` 호출부에 자기 타입 전달 + `HandleChallengeStepChanged`/`HandleChallengeClearedChanged`/`HandleChallengeOutcome`(구독 중인 것만) 맨 앞에 `ChallengeOwner` 불일치 시 즉시 반환하는 가드 추가.
- 린트 확인 완료(에러 없음). **ParrelSync 재검증 통과 (2026-07-28)**.

### `GameSession` Ready 늦은 구독 누락 — ColorTile 조용한 미생성 버그 수정 (코드+검증 완료, 2026-07-28)

**증상:** `M.Stage3` `ColorTileChallenge`가 Host는 정상인데 Client 화면엔 타일이 하나도 안 뜸. 에러/경고 없음. `NetworkDesign.md` §11.7에 상세 원인·수정 SSOT로 기록.

- **원인**: `NetworkDesign.md` §11.3이 명시한 표준 Consumer 패턴(`PlayerSpawnCoordinator.OnPlayersReady += Handler; if (IsReady) Handler();`)을 `GameSession.OnSceneLoaded()`만 지키지 않고 `+=`만 걸어놨다. `OnPlayersReady`가 그 구독보다 먼저 도착한 판에는 `_activePlayers`가 씬 내내 빈 채로 남고, `ColorTileChallenge.HandleChallengeStepChanged`가 `GameSession.GetActivePlayers()`로 얻은 빈 목록 때문에 `colors.Count == 0`으로 조용히 return — 콘솔에 흔적이 안 남아 원인 특정이 오래 걸렸다.
- **수정**: `Assets/Scripts/GameSession.cs` `OnSceneLoaded()`에 `if (PlayerSpawnCoordinator.IsReady) RefreshPlayersOnReady();` 한 줄 추가 — §11.3 표준 패턴대로 통일.
- **부수 효과**: `GridColorChallenge`/`GridBWTileChallenge`도 동일 의존이라 같은 레이스의 잠재 피해자였음 — 이번 수정으로 함께 해소.
- 린트 확인 완료(에러 없음). **ParrelSync 2인 재검증 통과 (2026-07-28)** — 콘솔 `[GameSession] N인 모드 적용` 매 스테이지 진입 시 확인, `M.Stage3` Client 화면 타일 정상 생성 확인.

### BossHealthBarUI / ObjectiveUI 통합 + 세그먼트 BG (2026-07-27 확정 — M.Boss·T.Boss 동시 적용)

`BossHealthBarUI`/`ObjectiveUI`/`BossFightObjective`는 M.Boss·T.Boss가 **같은 스크립트를 공유**하므로, 아래 UI 개편은 두 씬에 **동일하게** 반영한다 (T.Boss를 별도 이월 항목으로 미루지 않음):

- `ObjectiveUI`는 그대로 유지(스테이지 진행도 슬롯 + `ShowSceneClear` 문구 표시 역할 그대로). 개별로 떠 있던 Mouth Boss(체력바+이름)를 `Objective_Panel` 쪽으로 편입.
- `ObjectiveUI.BuildSlots()`가 매 `Refresh()`마다 자기 자식을 전부 `Destroy`하므로, 보스 체력바(고정 UI)를 그 슬롯 생성 로직과 분리해야 파괴되지 않음 — 코드 수정 필요.
- 세그먼트는 기존 color tint(활성/클리어 색)를 그대로 유지 — 스프라이트 스왑 등 추가 연출 불필요, 뒤에 **BG 1장(전체 세그먼트 바 배경)** 만 추가.
- 씬 작업(계층 이동, BG 스프라이트 배치)은 M.Boss에서 먼저 검증 후, **T.Boss에도 동일하게 반영** — 코드 계약은 공유라 자동 커버되지만 씬 인스턴스는 각각 확인 필요.

### T 라운드 이월 체크리스트 (까먹지 말 것 — T 라운드 시작 시 여기부터)

- [ ] **`T.Boss` 보스 objective UI 재확인:** `BossFightObjective`/`BossHealthBarUI`는 M.Boss와 **같은 클래스 공유** — M.Boss 라운드에서 코드 계약을 잠그면 코드 작업은 자동 커버. T.Boss에 남는 것은 **씬 인스턴스 확인만**: 인스펙터 이벤트 연결(`OnPhaseCleared`→`BossHealthBarUI`, 각 챌린지 `OnChallengeComplete`→`NotifyPhaseCleared`), `totalPhases`↔`PhaseManager.phases` 수 일치, ParrelSync 2인 검증.
- [ ] `SpikeTrap`/`SpikeLaneField` 앵커 수정 후 실기 검증 (`TrapNetworkBoard.md` §5 — `T.Stage3`/`T.Boss`)
- [ ] T 전용 E 패턴 (`WallMover`/`BoulderSpawner` 등 — `NetworkDesign.md` §9.1.3 그룹 2)
- [ ] `T.Stage1` Must (패드·문·볼더 — `NetworkDesign.md` §9 표)

> **이관 완료:** 위 체크리스트의 상세 씬 인벤토리·결정 항목·작업 순서는 [`TStageNetworkBoard.md`](TStageNetworkBoard.md)로 옮겨졌다 — T 라운드 작업은 여기가 아니라 그 문서를 참고할 것.

### Floor 마이그레이션 상세 설계 (2026-07-24 확정 — **코드 반영 완료 2026-07-25**, 아래 `### Floor 마이그레이션 반영 내용` 참고)

**요약**: `Floormanager.cs`를 `NetworkBehaviour`(자체 `NetworkObject`) → 일반 `MonoBehaviour` + `StageNetworkState`의 새 전용 NV 슬롯 구독으로 전환. 기존 `SyncTilesClientRpc(byte[] states)`(타일 상태 배열 전체를 매번 전송)를 폐기하고, **시드 하나만 전송해 전 머신이 로컬로 동일 결과를 재생성**하는 §11B Generate 패턴을 재사용한다 (Floor는 성공/실패 판정이 없는 "무한 반복 Generate"라 ④Judge/⑤Resolve는 필요 없음 — OX/GridBW보다 단순).

**왜 별도 슬롯인가**: `_challengeStep`(챌린지 공유 슬롯)과 `_stageStartServerTime`(StageStartGate 전용, 다른 시스템과 공유 금지 — 2026-07-21 오공유 버그 참고)에 이미 있는 "슬롯 배타성" 원칙과 동일하게, Floor도 **자기 전용 NV 슬롯**을 새로 만든다. 챌린지와 Floor가 씬에서 동시에 도는 경우는 없음이 확인됐지만(2026-07-22), 의미가 다른 시스템이라 슬롯을 공유하면 나중에 또 오공유 버그가 재발할 수 있음.

**1. `StageNetworkState.cs`에 추가**
```csharp
public struct FloorRollState : INetworkSerializable, IEquatable<FloorRollState>
{
    public int   seed;
    public float keepBWRatio; // Host가 그 순간의 Phase 값을 같이 실어보냄 — Client는 Phase를 독자 계산하지 않음
    // NetworkSerialize/Equals는 ChallengeStepState 구현 그대로 복제
}

private readonly NetworkVariable<FloorRollState> _floorRoll = new(
    default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

public event Action<FloorRollState> OnFloorRollChanged;

// OnNetworkSpawn에 구독 추가: _floorRoll.OnValueChanged += (_, next) => OnFloorRollChanged?.Invoke(next);
// OnNetworkDespawn에 구독 해제 추가

/// <summary>Host 전용: 새 타일 롤 배포. keepBWRatio를 실어보내 Client가 Phase를 독자 계산할 필요가 없다.</summary>
public void FloorRoll(int seed, float keepBWRatio)
{
    if (!IsServer) return;
    _floorRoll.Value = new FloorRollState { seed = seed, keepBWRatio = keepBWRatio };
}
```
- `keepBWRatio`를 NV에 같이 실어보내는 이유: Client가 Phase 진행(`triggerTime`/`changeInterval`)을 독자적으로 계산하게 만들면 OX처럼 ServerTime 역산이 필요해져 복잡해짐 — Host가 이미 계산한 값을 그대로 실어보내는 쪽이 훨씬 단순하고 안전(SequenceRing의 시간 동기화에서 얻은 교훈과 같은 결론).
- `stepIndex`/`stepStartServerTime` 같은 필드는 Floor에 불필요 — 롤마다 `seed` 값 자체가 바뀌므로 `OnValueChanged`만으로 "새 롤이 왔다"는 신호가 충분하다(중간 롤을 하나 놓쳐도 최종 상태로 스냅되니 무해 — 챌린지처럼 스텝을 건너뛰면 안 되는 판정형이 아니라서 인덱스 불필요).

**2. `Floormanager.cs` 전면 개조**
- `NetworkBehaviour` → `MonoBehaviour`로 변경, 클래스명은 유지(`FloorManager`) — `StageManager.RegisterFloor(FloorManager)`가 참조하는 타입이라 이름 바꾸면 안 됨
- `IsServer`(NetworkBehaviour 상속 멤버) 대신 다른 전환 파일들과 동일한 로컬 `IsClientOnly()` 헬퍼 추가(`NetworkManager.Singleton` 기반)
- `Start()`에서 `StageNetworkState.Instance` 캐시 + `OnFloorRollChanged` 구독, `OnDestroy()`에서 해제 (OX/GridBW/SequenceRing과 동일 전제 — `StageNetworkState.Awake()`가 먼저 실행됨)
- `StartFloor()`: Host 가드(`IsClientOnly()`) 추가만, 나머지(`_isRunning`/`_elapsedTime`/`nextTime`/`currentPhaseIndex` 리셋)는 그대로
- `Update()`: `if (!_isRunning) return;` 다음에 `if (IsClientOnly()) return;` 추가 — Client는 타이머 진행을 전혀 하지 않음(§11A 이중 계산 금지, SequenceRing에서 이미 겪은 것과 동일 원칙). `CheckPhase()`는 그대로 Host에서만 실행
- `RandomizeTiles()`를 `RollTiles()`로 교체: 로컬로 `tiles[i].SetType()` 직접 호출하는 대신 새 시드 하나 뽑아서 `_netState.FloorRoll(seed, keepBWRatio)` 호출만 하고 끝 (`byte[] states` 배열·`SyncTilesClientRpc` 전부 삭제)
- 신규 `HandleFloorRollChanged(FloorRollState state)` — Host/Client 공통 코드로 실제 타일 색 계산: `var rng = new System.Random(state.seed);` 로 `tiles[]`를 순회하며 원래 `RandomizeTiles()`에 있던 `Random.value < keepBWRatio` 로직을 `rng.NextDouble() < state.keepBWRatio`로 바꿔 그대로 적용 (전역 `UnityEngine.Random` 오염 없음 — OX `RegenerateQuestionOrder`와 동일 원칙)
- `OnEnable()`/`OnDisable()`/`CheckPhase()`/`FloorPhase` 구조체는 변경 없음

**3. 씬 작업 (에이전트는 씬 파일 쓰기 금지 — `unity-mcp-readonly.mdc`, 사용자가 에디터에서 직접)**
- `FloorManager`가 있는 씬: `M.Stage1`~`M.Stage5`, `T.Stage5` (2026-07-24 grep 확인 — `T.Stage2`/`T.Stage3`/`T.Stage4`/`T.Boss`/`M.Boss`에는 없음, 코드 수정 전 실제 씬에서 재확인 권장)
- 위 각 씬의 Floor GameObject에서 `NetworkObject` 컴포넌트 제거
- `NetworkManager`의 Network Prefabs 리스트에 Floor 프리팹이 등록되어 있다면(씬 배치形이라 등록 안 되어 있을 가능성이 높음, 확인 필요) 같이 제거

**4. 검증**: ParrelSync 2인으로 Host/Client 화면에서 타일 롤 패턴(Black/White/Reveal 배치)이 동일한지, Phase 전환(간격·비율 변화)이 양쪽에서 같은 타이밍에 반영되는지 확인.

### Floor 마이그레이션 반영 내용 (코드, 2026-07-25)

- `Assets/Scripts/Network/StageNetworkState.cs` — `FloorRollState` 구조체(`seed`+`keepBWRatio`, `ChallengeStepState`와 동일한 `INetworkSerializable`/`IEquatable` 패턴 복제) + `_floorRoll` NV 신설(`_challengeStep`과 **별도 슬롯** — 슬롯 배타성 원칙 유지) + `OnFloorRollChanged` 이벤트(`OnNetworkSpawn`/`OnNetworkDespawn` 구독·해제 포함) + Host 전용 `FloorRoll(int seed, float keepBWRatio)` 메서드 추가
- `Assets/Scripts/Floormanager.cs` — `NetworkBehaviour` → `MonoBehaviour`로 전면 개조(클래스명 `FloorManager` 유지, `StageManager.RegisterFloor(FloorManager)` 참조 무영향). 다른 축 매니저와 동일한 `IsClientOnly()` 헬퍼 추가, `StartFloor()`/`Update()`에 Host 가드(Client는 타이머 진행 자체를 안 함). `RandomizeTiles()` → `RollTiles()`로 교체: 로컬 적용 없이 새 시드만 뽑아 `StageNetworkState.FloorRoll()` 호출. 신규 `HandleFloorRollChanged(FloorRollState)`가 Host/Client 공통으로 `System.Random(state.seed)` 기반 재생성(전역 `UnityEngine.Random` 오염 없음) — `SyncTilesClientRpc(byte[] states)`·`IsMultiplayer()` 전부 삭제
- `Assets/Scripts/Stage/StageManager.cs` — 타입 참조만 있어 무수정 확인
- Judge/Resolve 단계 없음(설계대로) — Floor는 성공/실패 판정이 없는 "무한 반복 Generate"라 시드 배포만으로 끝
- 린트 확인 완료(에러 없음), Unity 콘솔 확인 결과 컴파일 에러 없음(`MouthBarrier`/`MouthTrap` 프리팹 경고는 기존 이슈로 무관)
- **ParrelSync 2인 검증 통과 (2026-07-25):** 씬 작업(Floor GameObject `NetworkObject` 제거) 완료 후 Host/Client 화면에서 타일 롤 패턴(Black/White/Reveal 배치)·Phase 전환 타이밍 동일 확인. `NetworkDesign.md` §11B.8로 승급 완료

### `SequenceRingMinigame` 반영 내용 (코드, 2026-07-22)

- `Activate()` 격의 `StartMinigame()`에 Host 가드, `SequenceRingMinigame.Instance` 싱글턴 신설(`StageNetworkState`의 새 ServerRpc가 Host에서 참조)
- 기존에는 Host/Client가 각자 로컬로 `TickTimer`/`TickDangerStep`/`PollSimInput→TrySubmit` 전부를 독자 실행 — **§11A "이중 계산" 위반이 실제로 존재했음** (Client도 자기 화면에서 독자적으로 시간 초과·Danger 자동 통과를 판정하고 있었음). `Update()`를 Host 전용 판정 블록(`TickTimer`/`TickDangerStep`)과 전 머신 공통 입력 감지(`PollSimInput`)로 분리해 수정
- `PollSimInput()`이 로컬 `TrySubmit`/`TrySubmitAnyKey()`를 직접 호출하던 것을, Client는 신설된 `StageNetworkState.SubmitStepServerRpc(color)`/`SubmitAnyKeyStepServerRpc()`로 요청만 보내고 Host만 실제 판정하도록 교체 (§11B.1). Host에서는 여전히 로컬 직접 호출(자기 자신에게 RPC 왕복할 필요 없음)
- `GenerateSteps()`가 매 프레임 새 `System.Random()`(비결정적)을 쓰던 것을 `StageNetworkState.ChallengeSeed` 기반으로 교체 — `HandleChallengeStepChanged`에서 매 스텝마다 재계산하지만 시드가 같으므로 항상 같은 결과(OX `RegenerateQuestionOrder`와 동일 원칙)
- `AdvanceStep()`이 로컬로 직접 `_currentStepIndex++`+타일 갱신을 하던 것을, `StageNetworkState.ChallengeStepBegin(nextIndex)` NV 쓰기로 바꾸고 화면 반영(`OnEnterStep`/`RefreshTileColors`)은 `HandleChallengeStepChanged`(전 머신 공통)로 이동
- 성공은 `ChallengeCleared(true)` → `OnChallengeClearedChanged` 공통 발동(OX `OnAllCleared`와 동일), 실패는 `NotifyChallengeOutcomeClientRpc(false)`로 전파
- **신규 추가**: 남은 시간이 오답 페널티 등 이벤트 기반으로 변하기 때문에(OX처럼 `ChallengeStepStartServerTime` 역산 불가) `StageNetworkState`에 `SyncChallengeTimeClientRpc(float)` + `OnChallengeTimeSync` 이벤트를 신설, Host가 0.1초 주기로 브로드캐스트(`SurviveTimeObjective.SyncSurvivalRemainingClientRpc`와 동일 패턴 재사용 — 이 사실을 명시적으로 알려드립니다, 보드에 사전 기재되지 않았던 항목)
- `SequenceRingObjective.HandleSuccess()`/`HandleFail()`에 Host 가드 추가 — `Complete()`/`Fail()` 확정은 Host 레인에서만 (`KillAllPlayers()`의 `NetworkDamageUtil.ApplyInstantKill`은 이미 내부적으로 Server 가드가 있어 그대로 유지)
- **범위 밖으로 남긴 것**: 오답(`OnWrongInput`) 시각 효과 자체는 아직 Client에 전파되지 않음(누른 사람 화면에만 즉시 보임, 다른 머신은 다음 상태 갱신 때까지 반영 안 됨) — 폴리싱 항목으로 남김, 필요하면 추후 별도 요청
- **ParrelSync 2인 검증 통과 (2026-07-25)**: 어느 플레이어가 눌러도 다른 클라이언트에서 동일하게 스텝 진행, 오답 페널티 포함 남은 시간 표시가 양쪽 화면에 거의 동시 반영 확인. `NetworkDesign.md` §11B.7로 승급 완료

### `GridBWTileChallenge` / `GridColorChallenge` 반영 내용 (코드, 2026-07-22)

- 공통: `Activate()`/`Cancel()`에 Host 가드(`IsClientOnly()`) 추가. `StartRound(round)` 신설 — 라운드마다 새 시드를 생성해 `ChallengeStart(seed)` 직후 같은 프레임에 `ChallengeStepBegin(round)` 호출(원자적 2단 쓰기, `Activate()`의 최초 진입과 동일 패턴이라 Client는 항상 최종 커밋값만 관찰)
- 기존 `ChallengeRoutine()` 반복 코루틴(라운드 루프 전체를 로컬로 순회)을 폐기하고, 라운드별 로직을 `HandleChallengeStepChanged(int stepIndex)`(전 머신 공통, `StageNetworkState.OnChallengeStepChanged` 구독)로 이동. **GridBW/GridColor 둘 다 `stepIndex`를 라운드 번호로 사용** — OX/ColorTile(1샷)과 달리 매 라운드 새 시드가 배포되므로 `System.Random(ChallengeSeed)`를 매 라운드 새로 생성해도 라운드마다 다른 결과가 나온다
- `PickRandomSafeTiles()`(GridBW) / `PickRandomColorTiles()`(GridColor) — `UnityEngine.Random` 대신 `System.Random(ChallengeSeed)`를 인자로 받도록 시그니처 변경 (안전 칸 위치 + GridBW의 Black/White 색까지 전부 시드로 결정)
- 판정은 `HandleChallengeStepChanged` 끝에서 Host만 `JudgeRoutine(round)` 코루틴 시작(§11B ④Judge) — `roundDuration` 대기 후 `EvaluateRound()`+개인 데미지 적용, `HandleRoundOutcome()`(로컬, `OnRoundSettled` 발동 + 타일 Default화) 직접 호출 + `NotifyChallengeOutcomeClientRpc`로 Client에 동일 연출 전파
- 라운드 종료 후 진행 결정도 `JudgeRoutine` 안에서 Host만: 다음 라운드가 있으면 쿨다운 대기 후 `StartRound(round+1)`, 마지막 라운드면 `StageNetworkState.ChallengeCleared(true)` 기록 → `OnChallengeClearedChanged` 구독(`HandleChallengeClearedChanged`)이 전 머신 공통으로 `OnChallengeComplete` 1회 발동 (OX의 `OnAllCleared`와 동일 패턴, Host 이중 발동 없음)
- **버그 수정**: `GridBWTileChallenge.ApplyIndividualDamage()`의 `p.ReceiveDamage()` 직접 호출 → `NetworkDamageUtil.ApplyDamage()`로 교체 (GridColor는 2026-07-19에 이미 고쳐져 있었음, 이번에 GridBW도 동일하게 맞춤)
- `GridRoundObjective.HandleChallengeComplete()`에 Host 가드 추가 — `OnChallengeComplete`가 이제 전 머신 공통으로 발동되므로 `Complete()` 확정은 Host 레인에서만 (`ColorTileRoundObjective.HandleSuccess`와 동일 패턴, §11A.2 계약 위반이었던 것을 이번에 같이 수정)
- **ParrelSync 2인 검증 통과 (2026-07-25, 둘 다)**: 동일 라운드 배치(안전 칸·색 타일), 동일 성공·실패 판정, 개인 데미지 동기화, 라운드 반복 진행 확인. `NetworkDesign.md` §11B.7로 승급 완료

### `ColorTileChallenge` 반영 내용 (코드, 2026-07-22)

- `Activate()`/`StartSchedule()`/`Cancel()`에 Host 가드 추가 — Client는 스케줄 코루틴 자체를 실행하지 않음(스케줄=시간 기반 Trigger라 Host가 단일 소스)
- 타일 생성 로직을 `ChallengeRoutine()`(로컬 전용 코루틴)에서 `HandleChallengeStepChanged(int)`(StageNetworkState NV 구독, 전 머신 공통)로 이동 — `System.Random(ChallengeSeed)`로 스폰 포인트 셔플(전역 `UnityEngine.Random` 오염 없음)
- 판정(`JudgeRoutine`)은 `HandleChallengeStepChanged` 끝에서 Host만 시작 — `ResolveRound()`가 Host 로컬 반영 + `NotifyChallengeOutcomeClientRpc`로 Client 전파
- 실패 패널티(`AdvancingWall.PermanentAdvance`)는 네트워크 동기화가 없는 로컬 컴포넌트라, Host/Client가 `HandleChallengeOutcome`을 통해 동일한 고정값으로 각자 호출해 위치를 맞춤(별도 NV 불필요)
- `AdvancingWall` 패널티 이동 무음(2026-09-01): 공유 컴포넌트라 `NetworkDesign.md` §9.1.3에 기록. `PenaltyRoutine`에 `StartMoveLoop`/`StopMoveLoop` 추가.
- `ColorTileRoundObjective.HandleSuccess()`에 Host 가드 추가 — `Complete()` 확정은 Host 레인에서만(`OXQuizObjective.HandleAllCleared`와 동일 패턴)

### 지금까지 실제로 한 일 (코드, 전부 완료)

1. `Assets/Scripts/Network/StageNetworkState.cs` — 축 #4 공통 API 추가
   - `ChallengeStepState` 구조체(`seed`/`stepIndex`/`stepStartServerTime`/`owner`, `owner`는 2026-07-28 추가) + `NetworkVariable<ChallengeStepState> _challengeStep` **1개**로 통합 관리
   - `NetworkVariable<bool> _challengeCleared`
   - Host 전용 메서드: `ChallengeStart(seed, owner)` / `ChallengeStepBegin(stepIndex)` / `ChallengeCleared(bool)`
   - `[ClientRpc] NotifyChallengeOutcomeClientRpc(bool success)`
   - 이벤트: `OnChallengeStepChanged(int)` / `OnChallengeClearedChanged(bool)` / `OnChallengeOutcome(bool)`
2. `Assets/Scripts/Stage/OXQuizManager.cs` — Q1~Q6, Q8 반영
   - `StartQuiz()`/`ResetQuiz()`에 Host 가드(`IsClientOnly()`)
   - 셔플을 `RegenerateQuestionOrder()`로 교체 — `StageNetworkState.ChallengeSeed` 기반 `System.Random` (전역 `UnityEngine.Random` 오염 없음)
   - `TimerRoutine()`을 ServerTime 기반 공통 루틴으로 교체 — 전 머신 동시 타임업, Host만 이어서 `JudgeByPosition()`
   - 정답 공개는 문제 데이터에서 로컬 도출(RPC 불필요), 정답/오답 연출과 클리어만 NV/RPC로 전파
   - 레거시 `PlayerEvents.OnRespawned → ResetQuiz` 구독 제거(죽은 코드)
3. `Assets/Scripts/Stage/OXQuizObjective.cs` — `HandleAllCleared()`의 `Complete()` 호출에 Host 가드 (§11A.2 계약)
4. `Assets/Scripts/Stage/GridColorChallenge.cs` — `ApplyIndividualDamage()`가 `Player.ReceiveDamage()`를 직접 불러 온라인에서 no-op이던 버그 수정 → `NetworkDamageUtil.ApplyDamage`로 교체 (ColorTile/SequenceRing은 아직 손대지 않음 — OX 잠근 뒤 §1.2 매핑표대로 복제할 차례)

### 발견·수정한 실기 버그 1건

**증상 (사용자 리포트, 2026-07-20 새벽):** ParrelSync 2인 테스트에서 Host와 Client가 **다른 문제**를 봄.

**원인:** 시드(`seed`)와 스텝 인덱스(`stepIndex`)를 별개의 NetworkVariable 2개로 나눠서 관리 → Client에 두 값이 도착하는 순서가 보장되지 않아, "인덱스는 새 값, 시드는 아직 이전 값"인 순간에 `RegenerateQuestionOrder()`가 잘못된 시드로 셔플을 계산.

**수정:** 위 1번 항목의 `ChallengeStepState` 구조체로 통합 — 시드+인덱스+시작시간이 항상 한 번에 원자적으로 도착하도록 변경. `OXQuizManager.cs`는 프로퍼티 이름(`ChallengeSeed`/`ChallengeStepIndex`/`ChallengeStepStartServerTime`)이 그대로라 손대지 않음.

**→ 재테스트 결과 (2026-07-21): 통과.** Host/Client 동일 문제 순서·동일 판정 확인됨. 이 수정으로 버그 해소 확정.

### 씬 조사로 확정된 것 (재확인 불필요)

- `M.Stage2`의 `OXQuizManager.barrierDoor`는 **`{fileID: 0}`(null)** — 이 씬엔 배리어 자체가 없음. **`DoorNetworkSync`/문 관련 `NetworkObject` 작업은 필요 없음.**
- `M.Stage2`엔 조사 시점(2026-07-19)엔 **`NetworkObject`가 하나도 없었음** (grep 0건, `M.Stage1`엔 있음) — **이후 배치 완료 확인됨** (2026-07-21 재테스트가 통과했다는 것 자체가 `StageNetworkState` NetworkObject가 정상 배치돼 있다는 증거).

### 다음 액션 (순서대로)

1. ~~재테스트~~ — **완료 (2026-07-21, 통과).**
2. ~~§1 골격 문구를 `NetworkDesign.md`로 승급~~ — **완료.** [`NetworkDesign.md`](NetworkDesign.md) §11B에 승급됨 (아래 §1~§4는 이제 승급 완료 기록).
3. **다음:** 보드 포커스를 `M.Stage3` **ColorTile**로 이동, §11B.3 매핑표대로 동일 축 복제 (그다음 GridColor, SequenceRing — SequenceRing은 §11B.1의 ServerRpc 입력 제출 추가 필요).
4. 향후 다른 챌린지에서 또 동기화 증상이 나오면: 이번처럼 "어느 NV/RPC가 언제 도착하는가" 관점으로 먼저 의심할 것 — Host 쪽 코드 로직 자체보다 **동기화 타이밍 레이스**가 실제 원인이었던 사례가 이미 1건 있음 (§11B.4 금지 목록에도 반영됨).

---

## 0. Board → Docs 승급 규칙

| Board (여기) | NetworkDesign |
|--------------|---------------|
| 후보·논의·미결 | 확정 lock |
| 파이프라인 초안 | §9.1 / 권한 표에 한 줄로 고정 |
| 구현 중 변경 | 승급 후에만 Docs 수정 |

승급 조건: ParrelSync **2인**으로 OX 1회 클리어(시작→판정→데미지→AllCleared) + 아래 **§2 계약**에 미결 없음.

---

## 1. C 패턴 — 축 #4(챌린지) 공통 골격 (확정 → **승급 완료, 기록용**)

> **승급 완료:** 이 절의 골격은 [`NetworkDesign.md` §11B](NetworkDesign.md)로 그대로 승급됐다. 앞으로 축 골격 자체를 바꿀 필요가 있으면 여기가 아니라 §11B를 고칠 것 — 이 절은 "왜 이렇게 정했는가"의 기록으로만 남긴다.
>
> 코드 조사 결과: `OXQuizManager` / `ColorTileChallenge` / `GridColorChallenge` / `SequenceRingMinigame` **넷 다 순수 로컬 `MonoBehaviour`**, Host 가드 없음, 시드 없는 로컬 `Random`으로 각 머신이 독립 계산 — §11A가 금지하는 "Host 1벌 + Client 1벌 이중 계산" 상태였음. 아래 골격은 **새로 발명하지 않고, 이미 이 프로젝트에 잠긴 패턴만 재사용**해서 이 문제를 없앤다.

```
① Trigger        → ② RoundStart(Seed)  → ③ Generate        → ④ Judge          → ⑤ Resolve
(Host만 감지)        (Host가 시드 NV 배포)    (Host+Client 각자      (Host 레인만          (Complete/Fail →
                                              동일 시드로 로컬        확정, 결과만            §11A Progress로
                                              재생성 — 기존           ClientRpc 연출)         반환)
                                              코드 그대로)
```

| 칸 | 불변식 | Writer | 재사용하는 기존 패턴 (발명 아님) |
|----|--------|--------|--------------------------------|
| ① Trigger | Host만 시작 판정. Client 트리거는 표시용일 뿐 시작 권한 없음 | Host의 로컬 인스턴스 (Host도 리모트 플레이어 Rigidbody를 직접 시뮬레이션하므로 자기 화면에서 트리거 감지 가능 — §9A) | `StageStartGate.Update()` — `if (!nm.IsServer) { ClientDisplay(); return; }` 분기 |
| ② RoundStart | 라운드/문제 시작마다 Host가 새 `int` 시드를 생성해 NV로 배포. 세션 시드(`NetworkSessionData.Seed`) 재사용 아님 — 라운드마다 독립적이라 타이밍 레이스 없음 | Host만 씀 (`StageNetworkState` 확장 또는 챌린지별 NV 홀더) | `StageNetworkState._stageStartServerTime` / `_currentPhase` NV 패턴 |
| ③ Generate | Host/Client 전부 `Random.InitState(roundSeed)` 호출 후 **기존 로컬 생성 코드를 그대로** 실행 → 네트워크로 전체 결과를 실어보낼 필요 없음 | 각 머신 로컬 (읽기는 전부, 쓰기는 없음 — 시드만 진실) | `StagePressurePadSetup.ApplySeedAndColors()` (`Random.InitState(seed ^ salt)`) |
| ④ Judge | 타이머 종료·정답 판정·성공/실패 확정은 **Host 레인에서만**. Client는 결과를 관찰만 (연출용 ClientRpc) | Host만 | §11A ③ Progress "Host 레인 하나만" 규칙 |
| ⑤ Resolve | 데미지는 **`NetworkDamageUtil`만** 경유. 클리어/실패는 `StageObjective.Complete()/Fail()`로 §11A Progress에 반환 — 새 리로드/전환 경로 금지 | Host만 | `NetworkDamageUtil.ApplyDamage`, §11A ④→⑤ |

### 1.1 Client → Host 입력 제출이 필요한 챌린지 (예: SequenceRing)

포지션 판정형(OX/GridColor/ColorTile)은 Host가 리모트 플레이어 위치를 직접 갖고 있어 별도 제출이 필요 없다. 반면 **키 입력형(SequenceRing)**은 어느 플레이어가 어떤 키를 눌렀는지 자체가 Host에 없는 정보이므로 별도 제출 경로가 필요하다:

```
Client: 자기 키 입력 감지 → SubmitStepServerRpc(stepColor)
Host  : TrySubmit() 판정 (④ Judge, Host 레인) → 결과 ClientRpc 연출
```

이건 새 메커니즘이 아니라 기존 **"Client → Host 한 방향 요청: ServerRpc, Host 검증"** 규칙(`multiplayer-ngo.mdc` Sync 절, Cheer 제출·발사체 히트 리포트와 동일 패턴)의 재적용이다.

### 1.2 4개 챌린지 → 이 골격 매핑

| 챌린지 | ①Trigger | ②RoundStart 시드로 대체할 것 | ④Judge | 비고 |
|--------|----------|------------------------------|--------|------|
| OX Quiz | 배리어 진입 트리거 | `ShuffleQuestions()` | `JudgeByPosition()` (물리 오버랩) | 이번 라운드 잠금 대상 |
| ColorTile | 스케줄(시간 기반, 트리거 아님) | 스폰 포인트 셔플 | 타일 완료 체크 | ①은 타이머 스케줄이라 Host `Update()` 자체가 이미 단일 소스여야 함 |
| GridColor | Activate() 호출 시점 | `PickRandomColorTiles()` | `EvaluateRound()` | **버그 수정 포함**: `ApplyIndividualDamage()`가 `Player.ReceiveDamage()` 직접 호출 → 온라인에서 no-op. `NetworkDamageUtil.ApplyDamage`로 교체 |
| SequenceRing | `StartMinigame()` 호출 시점 | `GenerateSteps()` | `TrySubmit()` | §1.1 ServerRpc 제출 추가 필요 (현재는 로컬 키 시뮬레이션만 존재) |

---

## 2. OX — 잠가야 할 규칙 (확정)

### 2.1 권한

| # | 항목 | 확정 |
|---|------|------|
| Q1 | 퀴즈 상태머신 (`OXQuizManager`) | **Host 레인만 실제 진행.** Client는 NV 관찰 + 로컬 연출만 (①Trigger/④Judge 모두 Host 가드 추가) |
| Q2 | Trigger로 퀴즈 시작 | **Host 자기 감지** — Client Trigger→ServerRpc 요청 경로 없음. Host도 리모트 플레이어 Rigidbody를 직접 시뮬레이션하므로 자기 화면에서 트리거가 그대로 발생 (`StageStartGate`와 동일 원리) |
| Q3 | 문제 셔플 / `questionsPerRun` | **라운드 시드 NV 방식.** 문제 시작마다 Host가 새 `int` 시드 생성 → NV 배포 → 전 머신 `Random.InitState(seed)` 후 `ShuffleQuestions()` 그대로 실행 |
| Q4 | 타이머 종료·오버랩 판정 | **Host만.** Client는 판정 호출 자체를 하지 않음(로컬 타이머 UI 표시만 유지) |
| Q5 | 오답·무응답 데미지 | **Host** `NetworkDamageUtil.ApplyDamage` (기존 경로 유지, 이미 맞음) |
| Q6 | `OnAllCleared` → Objective | Host 판정 확정 후에만 `Complete()` — Client 독자 호출 금지 |
| Q7 | 배리어 `DoorController` | **기존 `DoorNetworkSync` 사이드카 재사용.** 새 스크립트 불필요 — OX 배리어 GameObject에 `NetworkObject` + `DoorNetworkSync` 부착 |
| Q8 | 문제/해설/진행 UI | **γ (NV + ClientRpc 혼합)** — 진행 인덱스·시드는 NV, 정답 공개(`OnAnswerRevealed`) 등 1회성 연출은 ClientRpc. (`multiplayer-ngo.mdc` Sync 규칙과 이미 동일한 결론이라 별도 선택 사항 아님) |

### 2.2 동기화 수단 — 확정: γ (혼합)

C 챌린지 **공통**으로 쓸 수단.

- **NetworkVariable**: `RoundSeed`(int), `RoundIndex`(int) — 지속 상태, 늦은 참가자도 복원 가능해야 함
- **ClientRpc**: 정답 공개, 오답 연출, 타이머 UI 틱 — 1회성 이벤트

기존 `StageNetworkState`를 확장해 위 NV/RPC를 얹는 것을 권장 (챌린지별 새 NetworkBehaviour를 매번 만들지 않음 — `architecture.mdc`: "Prefer extending existing systems over parallel new frameworks").

### 2.3 로컬 전제 (구현 시 확인)

- `OXQuizManager`를 `NetworkBehaviour`로 바꾸거나, `StageNetworkState` 확장 + `IsServer` 분기를 기존 메서드에 추가하는 두 방식 중 구현 단계에서 선택 (§11A `StageManager`처럼 자기 자신이 `IsServer` 분기를 갖는 쪽이 기존 스타일과 더 일치).
- `Random.Range` 셔플은 **NV 시드 수신 후에만** 실행되도록 트리거 지점을 옮길 것.
- 데미지 경로(`NetworkDamageUtil`)는 이미 맞음 — 손댈 필요 없음.

---

## 3. OX 파이프라인 (확정)

```
Player Trigger (영역 진입, Host 로컬 감지만 유효)
  → [Q2] Host: StartQuiz (IsServer 가드)
  → Host: barrier Open (DoorNetworkSync가 NV로 전파, Client는 자동 연출)
  → Host: 새 RoundSeed 생성 → NV 배포 [Q3]
  → loop:
       전 머신: Random.InitState(RoundSeed) → ShuffleQuestions() 동일 실행
       Host: push question + start timer  [Q8: NV index + ClientRpc 문구]
       Client: UI·타일 Pending 표시 (A, 로컬 연출)
       Host: timer end → overlap O/X 판정 [Q4, Host 전용]
       Host: wrong → NetworkDamageUtil.ApplyDamage [Q5]
       Host: reveal 결과 → ClientRpc로 전 클라 연출 동기화 [Q8]
  → Host: AllCleared → barrier Close(NV) + OXQuizObjective.Complete [Q6, Host만 호출]
```

사망 시: 기존 D(`StageResetOnPlayerDeath`) + Manager `ResetQuiz`가 Host에서 재구독·상태 리셋되는지 구현 시 확인 후 Docs에 한 줄.

---

## 4. 승급 체크 (OX 틀 → NetworkDesign)

**코드 작업 완료 (2026-07-19):**

- [x] `StageNetworkState` 확장 — `ChallengeSeed`/`ChallengeStepIndex`/`ChallengeStepStartServerTime`/`ChallengeCleared` NV + `ChallengeStart`/`ChallengeStepBegin`/`ChallengeCleared`/`NotifyChallengeOutcomeClientRpc` (Host 전용, 축 #4 공통 API)
- [x] `OXQuizManager` — Q1~Q6, Q8 코드 반영: `IsClientOnly()` 가드(Q1/Q2/Q4/Q6), 시드 기반 `RegenerateQuestionOrder()`(Q3, `System.Random` 사용 — `UnityEngine.Random` 전역 상태 오염 없음), `TimerRoutine()` ServerTime 기반 공통 타이머 + Host만 `JudgeByPosition()`, NV/RPC 구독 핸들러(`HandleChallengeStepChanged`/`HandleChallengeClearedChanged`/`HandleChallengeOutcome`)
- [x] `OXQuizObjective.HandleAllCleared()` — `Complete()` 호출에 Host 가드 추가 (§11A.2 계약)
- [x] `OXQuizManager`의 레거시 `PlayerEvents.OnRespawned → ResetQuiz` 구독 제거 (죽은 코드 — §11 사망=전체 씬 리로드가 이미 `OXQuizManager` 재생성을 보장)
- [x] `GridColorChallenge.ApplyIndividualDamage()` — `Player.ReceiveDamage()` 우회 버그 수정, `NetworkDamageUtil.ApplyDamage`로 교체

**남은 것 (유니티 에디터·씬 작업 — 코드 아님):**

- [x] `M.Stage2` 씬에 `StageNetworkState` NetworkObject 배치 확인 (`M.Stage1` 구성 그대로 복제) — 2026-07-21 재테스트 통과로 배치 확인됨
- [x] ~~OX 배리어 `NetworkObject`+`DoorNetworkSync`~~ — 불필요로 정정: `M.Stage2`의 `OXQuizManager.barrierDoor`가 null(`{fileID: 0}`)이라 이 씬엔 배리어가 없음

**검증 (완료, 2026-07-21):**

- [x] 2인: Trigger→클리어 1회, Client도 같은 문제·같은 판정 체감 (문제 순서까지 동일한지 확인) — 통과
- [x] Client만의 데미지/셔플/Complete 없음 (그레핑으로 `IsServer` 가드 누락 확인) — 이상 없음
- [x] C 공통 파이프라인(§1) 문구 Docs §11B(챌린지 축 신규 절)에 반영 — **승급 완료**
- [ ] 다음 보드 포커스 → `M.Stage3` ColorTile (동일 C 파이프 복제) — **진행 중 (현재 보드 포커스)**

---

## 5. 다음에 하지 말 것 (이 보드 범위 밖)

- WindTrap Host 힘 상세 (B Should — M5)
- T 스테이지 C
- Steam / 텔레메트리
- A–F 표 재작성 (이미 NetworkDesign §9.1)
