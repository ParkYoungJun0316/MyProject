# T Stage Network Board

> **역할:** 미확정 파이프라인을 여기서 잡고, **확정되면 [`NetworkDesign.md`](NetworkDesign.md) §9 / §9.1로 승급**한다.  
> (예: 발사체 **B안** — 보드·논의 → Docs 고정. M.Stage는 [`MStageNetworkBoard.md`](MStageNetworkBoard.md) 참고.)  
> **빈 체크리스트 전용이 아님.** 큰 틀을 정하기 위한 작업 md.

**현재 인게임 진행:** `M.Stage1`…`M.Stage5` → `M.Boss` 네트워크 축은 종료됨 — 다음 라운드는 `T.Stage1`…`T.Stage5` → `T.Boss` (`AGENTS.md` / `MStageNetworkBoard.md` "T 라운드 이월 체크리스트" 참고).  
**이 보드가 다루는 것:** T 전용 씬 인벤토리 정리, 관측성(구조화 로그, `NetworkDesign.md` §9B) 신설. Door 네트워크 통합(§3.1)은 구현+검증 완료. `MovingCorridor`(`startActive` 경로)는 T4 착수로 코드 반영 완료. **T.Boss ParrelSync 실기 테스트 중 `AdvancingWall`/`ColorWall`/`WallLineRandomizer` 착수 — 코드 반영 + ParrelSync 2인 검증 통과 (2026-08, §2/§3.3 참고). 이어서 `ColorTileChallenge` 인스턴스 5개 owner 슬롯 공유 교차 오염 버그(페널티 방향 Host/Client 불일치) 발견 → `instanceId` 가드 코드 반영 + ParrelSync 2인 검증 통과 완료 (2026-08, §3.3 참고).**

---

## 현재 상태 (다음 세션 시작점 — 여기부터 읽을 것)

**요약 (2026-08):** T 라운드 착수 전 준비 단계 완료 — (1) `NetLog.Transition()` 구조화 로그 유틸 신설(`Assets/Scripts/Network/NetLog.cs`, `NetworkDesign.md` §9B), (2) 이 보드(`TStageNetworkBoard.md`) 신설 — 배경 조사로 정정한 씬 인벤토리·결정 항목·작업 순서를 아래에 확정. (3) **코딩 전 축 결정 3건 완료 (2026-08, 사용자 확정)** — 아래 "축 결정 (2026-08 확정)" 참고. (4) **Door(§3.1) 코드 구현 + ParrelSync 2인 검증 통과 완료 (2026-08)** — `DoorNetworkSync` 폐기, `StageNetworkState` 공유 슬롯 통합. (5) **패드·볼더 ParrelSync 2인 검증 통과 (2026-08)** — 새 코드 작업 없이 기존 구현 그대로 정상 동작 확인. **`T.Stage1` Must(패드/문/볼더) 전부 완료.** T5 AI(§3.2)는 아직 미착수. `MovingCorridor`(startActive 경로)는 T4에서 코드 반영 완료(ParrelSync 검증 대기). **`AdvancingWall`/`ColorWall`/`WallLineRandomizer`(§3 결정 1·3)은 T.Boss ParrelSync 실기 테스트 중 미동기화 버그 발견 → 진단·코드 반영 + ParrelSync 2인 검증 통과 완료 (2026-08)** — 아래 "다음 세션 시작점" 및 §2/§3.3 참고.

**공유 컴포넌트 버그 (2026-08):** `StageStartGate`/`StageNetworkState` 다중 게이트 stale 재점화 버그 발견·수정 — 공유 컴포넌트라 `NetworkDesign.md` §11A.7에 기록. 영향 씬: `T.Stage2`(3게이트)/`T.Stage4`(2게이트)/`T.Stage5`(4게이트). 코드 반영 완료, **Inspector `gateId` 배정 + ParrelSync 검증 대기**(§11A.7 참고) — 이 세 씬 착수 전 반드시 처리.

**다음 세션 시작점:** **`T.Stage1` Must 항목(패드·문·볼더) 전부 검증 통과 (2026-08)** — 문(Door)은 §3.1 신규 구현+검증, 패드·볼더는 기존 구현 그대로 검증 통과(상세는 §2 T1 행). **패턴 E/B/A 전체 재분류표(§3.3) 확정 완료 (2026-08)** — `MovingCorridor`/`AdvancingWall`/`WallLineRandomizer`를 기존 검증된 두 표준(`WallMover` 자유런 / `WallMoverSequencer` 트리거+브로드캐스트)에 편입, `Breakable`/`RingBlendShapePulse`는 별개 축으로 확인. **`T3` 완료 (2026-08)** — SpikeLane 앵커 결함(`TrapNetworkBoard.md` §5) ParrelSync 2인 실기 검증 통과, `WallWaveController`(`playOnStart`, ①자유런) `Time.time`→`ServerTime` 치환 코드 반영 + ParrelSync 2인 검증 통과. **`MovingCorridor`(`startActive` 경로) `ServerTime`/시드 치환 코드 반영 완료 (2026-08)** — §3.3 표 참고, ParrelSync 2인 검증 대기. **T.Boss ParrelSync 실기 테스트 중 `ColorWall` 색 미동기화 발견 → Bug Hunter 진단 + 사용자 승인 후 코드 반영 완료 (2026-08, §4 순서보다 앞당겨 처리)**: ① `GameSessionWallColorRemap.RemapSchedule()`이 `GameSessionColorDistribution.Distribute()`를 rng 없이 호출해 플레이어 색 슬롯 배정이 머신마다 갈라지던 버그 — `ColorWall.StartSchedule()`에서 `NetworkSessionData.Seed ^ salt` 기반 `System.Random` 생성해 전달하도록 수정(벽별 다양성은 신규 Inspector `colorSeedSalt`로 노출). ② `ColorWall`/`AdvancingWall` `ScheduleRoutine()`의 `Time.time` → `nm.ServerTime.Time` 치환(§3.3 표 참고). 이어서 **`WallLineRandomizer`(8개 인스턴스)도 같은 축으로 판단해 코드 반영 완료 (2026-08)** — 당초 제안했던 "`NetworkBehaviour`+`ClientRpc`" 대신, `WindTrap`(Random 모드)/`MouthController`가 이미 쓰던 "`NetworkSessionData.Seed ^ salt ^ (사이클 카운터)`로 매 사이클 RNG 재시드" 방식을 재사용 — `NetworkObject` 부착 자체가 불필요해짐(§3.3 표 참고). `WallLineRandomizer`는 1차 구현에서 `cycleSeedSalt` 씬 미반영으로 인스턴스 전부 같은 색·순서가 나오는 버그가 재발견돼, `WindTrap._registry`/`GetHierarchyPath`와 동일한 계층 경로 정렬로 `_netIndex`를 자동 배정하도록 재수정. **세 항목 모두 ParrelSync 2인 검증 통과 완료 (2026-08).** 이어서 실기 재현으로 **`ColorTileChallenge` 페널티 방향 Host/Client 불일치 버그**(`T.Boss` 인스턴스 5개 — `ColorChallenge_Wall_F/B/L/R`+`ColorChallenge_Ceiling` — 가 `ChallengeOwnerType.ColorTile` 슬롯을 공유해, `NotifyChallengeOutcomeClientRpc`가 Host 자신에겐 스킵되고 Client에서만 실제 발동 → Client에서만 형제 인스턴스 전부가 반응해 페널티 중복 적용, Host 2방향 vs Client 4방향)를 Bug Hunter로 진단해 `ChallengeStepState.instanceId` + `NotifyChallengeOutcomeClientRpc(bool, int)` 확장으로 수정, Inspector `challengeInstanceId` 배정(`Wall_F/B/L/R`=0~3, `Ceiling`=0 — 별도 Phase 컨테이너라 `PhaseManager.EnterPhase()` 동기 disable→enable로 겹치는 프레임 없음) 후 **ParrelSync 2인 검증 통과 완료 (2026-08, §3.3 참고).** 이어서 **T2 `MemoryPath`/`ColoredMemoryPath` 조사 완료 (2026-08)** — 네트워크 작업 불필요 확정. 미리보기 시작 타이밍은 기존 `MemoryPathIntroController`(카메라 인트로 오케스트레이터, `StageStartGate.OnCountdownComplete` → `BeginIntro()`)가 이미 처리 중이라 `startOnAwake` 기본값을 `false`로 맞추는 코드만 반영(§3.4 참고, ParrelSync 2인 검증 대기). 아래 §4 작업 순서대로 **`T4`(OX 스모크 + `MovingCorridor` ParrelSync 검증)**로 계속 진행.

### 축 결정 (2026-08 확정 — 코딩 착수 전 사용자 확인 완료)

| # | 항목 | 결정 |
|---|------|------|
| 1 | **문(Door) 네트워크 구조** | `DoorNetworkSync`(문마다 개별 `NetworkObject`+`NetworkBehaviour`) **폐기**. `StageNetworkState`에 공유 `NetworkList<bool>` 슬롯 하나를 추가해 Floor(§11B.8)와 동일한 "슬롯 재사용" 원칙 적용 — `DoorController`는 `NetworkObject` 없는 순수 `MonoBehaviour`로 유지. 이유: `DoorNetworkSync`는 현재 실제 씬에 배치된 적이 없어(M.Stage2 배리어는 null이라 무산) 지금이 구조를 바꿀 수 있는 유일한 타이밍이었고, "챌린지·Floor마다 새 `NetworkBehaviour` 발명 금지"(`architecture.mdc`) 원칙과 문마다 개별 `NetworkObject`를 붙이는 기존 설계가 어긋났음. 설계 상세는 §3.1 |
| 2 | **T5 AI(체이서·타겟) 네트워크 모델** | **Host 전권 시뮬 + NetworkTransform 복제**로 확정. `Stage5ChaserSpawner`/`Stage5ChaserAI`/`Stage5TargetRunner`는 현재 **완전 로컬**(`NetworkObject` 없음, 시드 없는 `Random.Range`, 각 머신 독립 `NavMeshAgent` 추적) — 조사 결과 재사용할 기존 선례(Docs가 가리키는 `Enemy.cs`/`EnemyHitbox.cs`)가 코드베이스에 존재하지 않아 새로 정한 축. **GUID로 씬 검색해 `T.Stage5.unity` 전용임을 확인(2026-08) — M.Stage5엔 없음, 공유 컴포넌트 아님** — 상세는 §3.2 |
| 3 | **`MovingCorridor`/`AdvancingWall` E 표준 미적용 수정 시점** | 별도 세션으로 분리하지 않고, **해당 씬 라운드(T3/T4/T.Boss) 착수 시 그 작업에 포함**해서 바로 고친다 |

---

## 0. Board → Docs 승급 규칙

| Board (여기) | NetworkDesign |
|--------------|---------------|
| 후보·논의·미결 | 확정 lock |
| 파이프라인 초안 | §9 / §9.1 / 권한 표에 한 줄로 고정 |
| 구현 중 변경 | 승급 후에만 Docs 수정 |

승급 조건: 해당 씬 ParrelSync **2인** 검증 통과 + 아래 §2 결정 항목에 미결 없음.

---

## 1. 배경 (조사로 정정된 사실)

다른 agent가 공유한 분석 중 일부가 실제 코드와 어긋나 있었다 — 아래는 코드·Docs 직접 확인으로 정정한 내용이다.

- **패드(T1 Must)는 이미 구현됨** — `PressurePad.Evaluate()`는 `!nm.IsServer`면 이벤트를 안 쏘고, `StagePressurePadSetup.ApplySeedAndColors()`는 이미 `NetworkSessionData.Seed` + `PlayerSpawnCoordinator.OnPlayersReady` 표준 패턴을 따름 — **새 설계 불필요. ParrelSync 2인 검증 통과(2026-08)로 완료.**
- **문(Door)은 새 코드 작업 필요로 정정됨 (2026-08 축 결정)** — `DoorNetworkSync`(문마다 개별 `NetworkObject`+`NetworkBehaviour`)는 실제 씬에 배치된 적이 없고, "새 `NetworkBehaviour` 발명 금지" 원칙과 어긋나 **폐기 확정**. `StageNetworkState` 공유 `NetworkList<bool>`로 통합하는 코드 작업이 T1에서 새로 필요 — §3.1 설계 참고.
- **Boulder는 이미 B안** (`BoulderSpawner.SpawnOne()` Host만 Instantiate+`NetworkObject.Spawn`, `TrapProjectile.PrepareWaypoints`로 Deferred OnSpawn 레이스 방지까지 반영됨). 레인 셔플(`BoulderSpawnManager.Shuffle`)은 Host 전용 루프 안에서만 도니 시드 불필요. **ParrelSync 2인 검증 통과(2026-08)로 완료.** ⚠ **단, 2026-08 추가 수정 있음** — `T.Stage3`에서 `BoulderSpawnManager`의 GameObject가 씬에 활성 상태로 배치돼 있으면 `OnEnable()`의 자동 시작이 NGO 스폰(IsServer 세팅)보다 먼저 실행돼 스폰이 영구적으로 안 되는 레이스가 발견됨 — `OnNetworkSpawn()`으로 이동해 수정 완료(`TrapNetworkBoard.md` §7 참고). 위 "검증 통과"는 T1 기준이었고, T3는 이 수정 후 **ParrelSync 2인 재검증 통과(2026-08)** — **Build(Editor+Build 조합) 검증은 아직 남음.**
- **패턴 E는 이미 두 갈래 기존 패턴이 있음:**
  - [`WallMover.ScheduleRoutine()`](../Scripts/Stage/WallMover.cs) — 트리거 없는 free-running 스케줄. 각 머신이 `nm.ServerTime.Time` 폴링만으로 이미 결정론적(RPC 불필요).
  - [`WallMoverSequencer.Activate()`](../Scripts/Stage/WallMoverSequencer.cs) — Host가 트리거 감지(`IsServer` 가드) → `ServerTime` 앵커를 `ClientRpc`로 브로드캐스트 → 전 머신 로컬 코루틴 재생. 이미 네트워크 세이프.
  - **문제는 이 표준을 안 따르는 나머지:** [`MovingCorridor`](../Scripts/Stage/MovingCorridor.cs)는 `OnTriggerEnter`에 Host 가드가 아예 없고 `FixedUpdate`가 `Time.time` 기반 + `Environment.TickCount` 랜덤 시드(머신마다 다름). [`AdvancingWall`](../Scripts/Stage/AdvancingWall.cs)의 `ScheduleRoutine()`도 `float startTime = Time.time;` — 순수 로컬 타이머, `ServerTime` 미사용. `MStageNetworkBoard.md`는 "M.Stage3에서 검증해 T도 커버"라 적었지만 코드에 반영된 흔적이 없다 — **정정.**
  - **`Nodular`/`Lump`(`Breakable`) / `RingBlendShapePulse` / `WallLineRandomizer`까지 포함한 전체 매핑은 §3.3 재분류표로 확정** — E(월드모션) 두 표준에 나머지를 편입시키고, B(`Breakable`)·A(`RingBlendShapePulse`)는 별개 축임을 명시.
- **T.Boss `BossFightObjective`는 이미 Host 가드 + `StageNetworkState.SetBossPhasesCleared` NV로 구현되어 있고, M.Boss와 클래스를 공유**한다(`MStageNetworkBoard.md` "T 라운드 이월 체크리스트"에 이미 명시). "BossFightObjective 없음"이라는 분석은 부정확 — **새 설계가 아니라 씬 인스턴스 확인(이벤트 연결·`totalPhases` 일치)만 남은 항목.**
- **T5 Stage5 AI 스폰 권한은 2026-08 축 결정으로 확정됨** — `Stage5ChaserSpawner`/`Stage5ChaserAI`/`Stage5TargetRunner`는 조사 결과 `NetworkObject` 없이 완전 로컬(각 머신 독립 시뮬레이션)이었고, Docs(`NetworkDesign.md` §9A.5.1)가 언급하는 선례 `Enemy.cs`/`EnemyHitbox.cs`는 코드베이스에 존재하지 않음(grep 0건) — 재사용할 선례가 없어 **새 축으로 확정**(Host 전권 시뮬 + NetworkTransform 복제). **GUID 검색으로 `T.Stage5.unity` 전용 확인(2026-08, M.Stage5엔 없음) — T 단독 범위, M/T 공유 아님** — §3.2 참고.
- **T2 MemoryPath/ColoredMemoryPath는 조사 완료 — 네트워크 작업 불필요로 결론 (2026-08)**. 한때 네트워크 동기화 코드를 추가했다가 되돌렸는데, 정정 조사 결과 되돌린 판단이 맞았다 — 상세는 §3.4.

---

## 2. 씬 인벤토리 (정정 반영)

| 씬 | 컨텐츠 | 상태(정정됨) |
|---|---|---|
| T1 | 패드·문·볼더·Breakable·WallMover | **Must 전부 완료 (2026-08).** 패드·볼더는 기존 구현 그대로 ParrelSync 2인 검증 통과(새 코드 작업 없음). 문(Door)은 §3.1 신규 구현 + ParrelSync 2인 검증 통과. Breakable은 M 그룹1(B)에서 처리된 클래스 재사용 스모크만(별도 확인 필요 시) |
| T2 | MemoryPath + ColoredMemoryPath + 문/패드 | **조사 완료 (2026-08) — 네트워크 작업 불필요.** Trap 즉사는 `NetworkDamageUtil.ApplyInstantKill`(서버 전용 가드 내장)로 이미 안전, Safe 경로는 런타임 랜덤 없이 고정이라 시드 이슈 없음, 미리보기 시작 타이밍은 기존 `MemoryPathIntroController`(카메라 인트로 오케스트레이터)가 이미 `StageStartGate.OnCountdownComplete` 기반으로 처리 중이라 `startOnAwake=false` 확인만 필요(§3.4). 문은 T1에서 구현 완료된 §3.1 구조를 그대로 재사용(새 설계 없음) |
| T3 | WallMover/Sequencer/Wave·볼더·SpikeLane·패드퍼즐 | **완료 (2026-08).** WallMover/Sequencer는 기존 패턴 재사용. SpikeLane 앵커 — ParrelSync 2인 실기 검증 통과(코드는 2026-07-24 커밋 `2706cf4`에서 이미 반영, `TrapNetworkBoard.md` §5). `WallWaveController`(Wave) — ①자유런 코드 반영 + ParrelSync 2인 검증 통과(§3.3) |
| T4 | OX(§11B 재사용) + MovingCorridor + `Nodular`(Breakable) 다수 + `RingBlendShapePulse` | OX는 이미 검증된 축 재사용(새 설계 없음). **MovingCorridor(startActive 경로) `ServerTime`/시드 치환 코드 반영 완료 (2026-08, §3.3 표)** — ParrelSync 2인 검증 대기. 이 씬 인스턴스는 `activateOnPlayerTrigger` 미사용이라 그 경로는 범위 밖. `Nodular`(Breakable)는 패턴 B 축 재사용, `RingBlendShapePulse`는 패턴 A(로컬 유지) — §3.3 참고 |
| T5 | Floor(§11B.8 재사용) + Stage5 AI 스폰 + `Lump`(Breakable) 다수 | Floor는 이미 완료된 마이그레이션 재사용 스모크만. **AI 스폰 권한 확정됨(§3.2)** — Host 전권 시뮬 + NetworkTransform 복제 코드 작업 신규 필요. `T.Stage5.unity` 전용(M.Stage5엔 없음, GUID 검색 확인). `Lump`(Breakable)는 패턴 B 축 재사용 — §3.3 참고 |
| T.Boss | ColorTile + SurviveTime + AdvancingWall/ColorWall/WallLineRandomizer 다수 | `BossFightObjective`/`BossHealthBarUI`는 **이미 M.Boss와 공유·구현 완료** — 씬 인스턴스 확인만. `ColorTileChallenge`/`SurviveTimeObjective`는 조사 결과 이미 Host 가드 + NV/RPC로 구현 완료(추가 작업 불필요) — **정정 (2026-08): `ColorTileChallenge`는 인스턴스 5개(`ColorChallenge_Wall_F/B/L/R`+`ColorChallenge_Ceiling`)가 owner 슬롯을 공유해 Client에서 형제 인스턴스가 서로의 페널티에 교차 반응하는 실기 버그 발견(Host 2방향 vs Client 4방향 페널티) — `instanceId` 가드 코드 반영 + Inspector 배정(§3.3 표 참고) 완료, **ParrelSync 2인 검증 통과 (2026-08)**. `AdvancingWall`/`ColorWall`(46개 인스턴스, T.Boss 전용) — **`Time.time`→`ServerTime` 치환 코드 반영 완료 (2026-08)**, `ColorWall`은 추가로 `GameSessionWallColorRemap.RemapSchedule` 시드 결정론화도 반영. `WallLineRandomizer`(8개 인스턴스) — **WindTrap/MouthController 시드 미러링 방식으로 코드 반영 완료 (2026-08)**, `NetworkObject` 불필요(§3.3 참고). **`AdvancingWall`/`ColorWall`/`WallLineRandomizer`/`ColorTileChallenge` 전부 ParrelSync 2인 검증 통과 (2026-08)** — `BossFightObjective`/`BossHealthBarUI` 씬 인스턴스 확인만 남음 |

---

## 3. 결정 항목 (이전 라운드 확정 + 2026-08 축 결정 반영)

1. **패턴 E 표준 확정**: `WallMover`(free-running ServerTime 폴링) / `WallMoverSequencer`(Host 트리거+ClientRpc 앵커) 두 기존 패턴을 그대로 표준으로 승격. `MovingCorridor`/`AdvancingWall`은 이 표준 미준수 상태임을 명시 — **해당 씬 라운드(T3/T4/T.Boss) 착수 시 바로 수정**(2026-08 확정, 별도 세션으로 안 미룸) — "머신별 `Time.time` 각자 진행 금지" 원칙을 여기 못박는다. **전체 대상(Breakable/RingBlendShapePulse 포함) 편입 매핑은 §3.3 확정.**
2. **Door**: `DoorNetworkSync`(문마다 개별 `NetworkObject`) **폐기 확정 및 구현+검증 완료 (2026-08)**. `StageNetworkState` 공유 `NetworkList<bool>`로 통합 — §3.1 설계·구현 내용 참고. 컬러 Door(`ColoredDoorVisual` 부착분)도 동일 구조로 통일(비주얼 컴포넌트만 별개, 개폐 네트워크 경로는 하나).
3. **Boulder**: 시드 불필요 확정(Host 단일 스폰 루프라 Client 계산 자체가 없음) — **ParrelSync 2인 검증 통과(2026-08)로 완료.**
4. **T5 AI 스폰·틱 권한**: **Host 전권 시뮬 + NetworkTransform 복제로 확정 (2026-08)** — §3.2 설계 참고. `T.Stage5.unity` 전용(M.Stage5엔 없음 — GUID 검색 확인, 사용자 확정 2026-08).
5. **T2 MemoryPath/ColoredMemoryPath**: 조사 완료 — 네트워크 작업 불필요 확정 (2026-08). §3.4 참고.
6. **T.Boss 범위**: M.Boss와 공유 컴포넌트이므로 별도 objective 설계 불필요 — 정정 기록.
7. **BuffPickup**: 스크립트/배치 자체가 없는 Should 고아 항목 — 이번 범위에서 제외 확정.

### 3.1 Door 네트워크 통합 설계 (2026-08 확정 — **구현 완료 + 검증 통과**)

**배경**: `DoorController.CheckPadState()`(문을 실제로 움직이는 `AnimateDoor` 코루틴 트리거)는 `PressurePad.OnFulfilled`/`OnUnfulfilled` 이벤트가 와야 호출되는데, `PressurePad.Evaluate()`가 그 이벤트를 **Host에서만** 쏘도록 가드돼 있다(`if (nm.IsListening && !nm.IsServer) return;`). 즉 별도 복제 없이는 Client 화면에서 문이 전혀 안 열린다 — `DoorNetworkSync`는 이 구멍을 메우려던 것이었으나, 문마다 개별 `NetworkObject`+`NetworkBehaviour`를 부착해야 해서 "새 `NetworkBehaviour` 발명 금지"(`architecture.mdc`) 원칙 및 Floor 마이그레이션(§11B.8) 방향과 어긋난다. 현재 어떤 씬에도 배치된 적이 없어 지금이 구조를 정할 수 있는 시점.

**설계 (Floor §11B.8과 동일한 슬롯 재사용 원칙)**:

- `StageNetworkState`에 `NetworkList<bool> _doorOpenStates` 신설 (Server write). `OnDoorStateChanged(int index, bool isOpen)` 이벤트 + Host 전용 `SetDoorOpen(int index, bool isOpen)` 메서드.
- `DoorController`/`DoorNetworkSync` 어디에도 `NetworkObject` 부착 불필요 — `DoorController`는 순수 `MonoBehaviour` 유지.
- **Index 배정**: `StagePressurePadSetup`이 씬의 `DoorController[]`를 **이름순 정렬**(기존 `coloredPads.Sort(...)`와 동일 관례 — Host/Client 동일 순서 보장) 후 순서대로 index 부여. `OnPlayersReady` 이후 시점에 `_doorOpenStates`를 문 개수만큼 초기화(`Clear()` → `Add(false)` 반복).
- Host: 각 문의 `OnOpened`/`OnClosed`(이미 Host에서만 발동)를 `StageNetworkState.SetDoorOpen(index, true/false)`에 연결.
- Client: `OnDoorStateChanged` 구독 → 해당 index의 `DoorController.Open()/Close()` 직접 호출(연출만, 판정 아님).
- `DoorNetworkSync.cs`는 삭제 대상.

**영향 파일 (구현 시)**: `Assets/Scripts/Network/StageNetworkState.cs`(슬롯 추가), `Assets/Scripts/Stage/StagePressurePadSetup.cs`(index 배정 + Host/Client 연결 배선), `Assets/Scripts/Network/DoorNetworkSync.cs`(삭제). `DoorController.cs`/`DoorPuzzleGroup.cs`는 무수정.

**구현 반영 내용 (2026-08, 코드+검증 완료)**:

- `StageNetworkState.cs`: `NetworkList<bool> _doorOpenStates` 슬롯 신설 + `OnDoorStateChanged(int,bool)` 이벤트 + Host 전용 `InitDoorSlots(int count)`/`SetDoorOpen(int index, bool isOpen)` + 늦은 구독 캐치업용 `DoorCount`/`IsDoorOpen(index)` 추가. `NetworkListEvent<bool>.EventType.Add`/`Value`에서 이벤트 발동(다른 슬롯과 동일한 `OnNetworkSpawn`/`OnNetworkDespawn` 구독·해제 패턴).
- `StagePressurePadSetup.cs`: `BuildDoorIndexMap()`(씬의 `DoorController[]`를 이름순 정렬해 index 배정, `Start()`에서 실행) + `SetupDoorNetworkSync()`(`ApplySeedAndColors()` 마지막 단계 — Host: `InitDoorSlots` + 각 문의 `OnOpened`/`OnClosed`를 `SetDoorOpen(index,·)`에 배선. 전 머신: `OnDoorStateChanged` 구독 → Client만 `DoorController.Open()/Close()` 반영, Host는 로컬 물리로 이미 처리했으므로 스킵 — 구 `DoorNetworkSync`와 동일한 `IsServer` 가드).
- `PressurePad.cs`/`OXQuizManager.cs`: `DoorNetworkSync` 언급 주석을 `StageNetworkState._doorOpenStates`로 정정(동작 무변경). `OXQuizManager.barrierDoor`도 일반 `DoorController`라 `BuildDoorIndexMap()`이 자동 수집·배선 — 별도 특수 처리 불필요.
- `DoorNetworkSync.cs`(+`.meta`) 삭제. `Door.Y/G/P/B/C.prefab`에서 `DoorNetworkSync`/`NetworkObject` 컴포넌트 제거(사용자 작업, 에디터).
- **ParrelSync 2인 검증 통과(2026-08)** — Host/Client 양쪽 문 개폐 연출 동일 확인.

### 3.2 T5 AI 네트워크 설계 (2026-08 확정 — 구현 전)

**배경**: `Stage5ChaserSpawner.StartSpawning()`이 로컬 `Instantiate()`(NetworkObject 없음) + 시드 없는 `Random.Range` 셔플로 스폰 위치를 고르고, `Stage5ChaserAI.Update()`/`Stage5TargetRunner.Update()`가 각 머신에서 독립적으로 `NavMeshAgent` 추적을 수행한다 — Host/Client가 서로 다른 위치에 서로 다른 수의 개체를 스폰하고, 위치도 서서히 어긋날 수 있는 상태. `NetworkDesign.md` §9A.5.1이 "이미 Host 경로(확인만)"로 분류한 `Enemy.cs`/`EnemyHitbox.cs`는 실제 코드베이스에 존재하지 않음(grep 0건) — 재사용할 선례가 없다. **`T.Stage5.unity`에서만 쓰인다(GUID 검색으로 확인, 2026-08 사용자 확정) — M.Stage5와 무관, T 단독 범위.**

**설계 방향 (확정)**: Host 전권 시뮬레이션 + `NetworkTransform` 복제 — §9.0 "함정 스폰 시점·스케줄 = Host" 원칙과 동일선상.

- `Stage5ChaserAI`/`Stage5TargetRunner` 프리팹에 `NetworkObject` + 서버 권한 `NetworkTransform`(Owner 없음, Host가 유일하게 움직이므로 Owner Authority 불필요) 추가.
- `Stage5ChaserSpawner.StartSpawning()`: Host 가드(`IsClientOnly()` 헬퍼) 추가, `Instantiate()` → `NetworkObject.Spawn()`으로 교체(Boulder `BoulderSpawner.SpawnOne()`과 동일 패턴). 스폰 위치 셔플은 `NetworkSessionData.Seed ^ salt` 결정적 시드로 교체(값 자체는 Host만 쓰므로 Client 재현 불필요 — 단, 시드를 그대로 두는 이유는 재현성·로그 목적).
- `Stage5ChaserAI.Update()`/`Stage5TargetRunner.Update()`: `NavMeshAgent` 추적·타겟팅 로직 자체는 Host 전용으로 가드(Client는 `Update()`에서 AI 판단 로직 실행 안 함, `NetworkTransform`이 위치만 복제). 애니메이션 파라미터(`isChase`/`isRun` 등)는 Host가 판단한 결과를 `NetworkVariable` 또는 `ClientRpc`로 전파(연출용, §9.0 "VFX/사운드=ClientRpc→All" 원칙).
- 포획/피격 판정(`Stage5ChaserHitbox`, `Stage5TargetRunner.OnTriggerEnter`)은 이미 있는 `NetworkDamageUtil`/Host 가드 원칙 그대로 유지 — 이 항목은 새로 안 건드림.

**영향 범위**: `T.Stage5.unity` 단독 — M.Stage5는 관련 없음(§9B.4 공유 컴포넌트 규칙 적용 대상 아님).

### 3.3 패턴 E/B/A 재분류표 (2026-08 확정 — 구현 전)

**배경**: §1에서 정리한 "패턴 E는 이미 두 갈래 표준이 있다"를 코딩 착수 전에 **T3/T4/T.Boss 전체 대상**(`MovingCorridor`/`AdvancingWall`/`WallLineRandomizer`/`Nodular`·`Lump`(`Breakable`)/`RingBlendShapePulse`)으로 확장 매핑했다. `WallMoverSequencer`가 이미 ParrelSync 검증까지 끝난 표준이므로, **같은 메커니즘을 쓰는 나머지를 그 라인에 편입**시키는 방식으로 분류 — 씬 라운드마다 새로 설계하지 않는다(§3 결정 1과 동일 원칙).

**표준 두 갈래 (재확인 — 변경 없음)**:

- **①자유런** — [`WallMover.ScheduleRoutine()`](../Scripts/Stage/WallMover.cs): 트리거 없이 각 머신이 `nm.ServerTime.Time` 폴링만으로 결정론적. RPC 불필요.
- **②트리거+브로드캐스트** — [`WallMoverSequencer.Activate()`](../Scripts/Stage/WallMoverSequencer.cs): Host가 시작 시점(트리거 진입 등)을 감지 → `ServerTime` 앵커를 `ClientRpc`로 1회 브로드캐스트 → 전 머신 로컬 코루틴 재생. **검증 완료.**

| 대상 | 편입 라인 | 근거 | 필요 작업 |
|---|---|---|---|
| `WallMover` | ①자유런 | 원본 그 자체 | 없음 (완료) |
| `WallMoverSequencer` | ②트리거+브로드캐스트 | 원본 그 자체 | 없음 (검증 완료) |
| [`AdvancingWall`](../Scripts/Stage/AdvancingWall.cs) `ScheduleRoutine()`(내장 schedule, `scheduleOnStart`) | ①자유런 | 트리거 없이 `atSeconds` 배열을 순서대로 도는 구조가 `WallMover.ScheduleRoutine()`과 동일 | `Time.time` → `nm.ServerTime.Time` 치환 — **코드 반영 + ParrelSync 2인 검증 통과 완료 (2026-08)** |
| [`ColorWall`](../Scripts/Stage/ColorWall.cs) `ScheduleRoutine()`(`colorSchedule`, `scheduleOnStart`) — **T.Boss 착수 시 조사로 재분류표에 신규 편입 (2026-08, 최초 조사 시 누락)** | ①자유런 + 별도 결정론화 이슈 | 트리거 없이 `colorSchedule`을 순서대로 도는 구조는 `AdvancingWall`과 동일(①자유런). **추가로** `StartSchedule()`이 부르는 `GameSessionWallColorRemap.RemapSchedule()`이 `GameSessionColorDistribution.Distribute()`를 rng 없이 호출해 플레이어 색 슬롯 배정 자체가 머신마다 갈라지는 별도 버그가 있었음(시간 문제가 아니라 "다른 색"이 나오는 즉시성 버그) | `Time.time`→`ServerTime.Time` 치환 + `RemapSchedule`에 `NetworkSessionData.Seed ^ salt` 기반 `System.Random` 전달(벽별 다양성은 Inspector `colorSeedSalt` 필드로 노출) — **코드 반영 + ParrelSync 2인 검증 통과 완료 (2026-08)** |
| [`MovingCorridor`](../Scripts/Stage/MovingCorridor.cs) — `startActive`(자동 시작) 경로 | ①자유런 | 트리거 없이 씬 시작부터 미는 구조 | `Time.time` → `ServerTime` 치환 + `backRandomSpeed`/`frontRandomSpeed`용 `Environment.TickCount` 시드를 `NetworkSessionData.Seed ^ salt`로 교체(머신별 랜덤 어긋남 제거) — **코드 반영 완료 (2026-08)**. ParrelSync 2인 실기 검증 대기 |
| `MovingCorridor` — `activateOnPlayerTrigger` 경로 | ②트리거+브로드캐스트 | `OnTriggerEnter`로 시작 시점이 결정되는 구조가 `WallMoverSequencer.OnTriggerEnter`와 동일 | Host 가드 추가 + `Activate()` 시 `ServerTime` 앵커를 `ClientRpc`로 브로드캐스트하도록 `WallMoverSequencer` 그대로 복제(같은 이유로 `NetworkObject` 부착도 기존 선례 그대로) |
| [`WallLineRandomizer`](../Scripts/Stage/WallLineRandomizer.cs)(+`AdvancingWall.RunOnce`) | 축 변경 — ②안(`NetworkBehaviour`+`ClientRpc`) 대신 **WindTrap/MouthController 시드 미러링 방식**으로 확정 (2026-08) | 매 사이클 "언제, 무슨 색"을 결정해야 하는 건 맞지만, `WindTrap.WindCycle()`(Random 모드)/`MouthController.AutoCycle()`이 이미 "`NetworkSessionData.Seed ^ salt ^ (카운터 * 0x2545F491)`로 매 사이클 RNG를 재시드"하는 방식으로 RPC 없이 전 머신 동일 결과를 내는 선례를 갖고 있음 — `NetworkObject`/`NetworkBehaviour` 신설(`architecture.mdc` 원칙과도 더 잘 맞음)보다 이 쪽이 더 단순하고 기존 패턴 재사용 | `WallLineRandomizer`에 `cycleSeedSalt`(Inspector, 수동 오버라이드용) + 사이클마다 로컬 `System.Random`(간격·색 두 값을 한 번에 뽑아야 해서 전역 `UnityEngine.Random` 대신 로컬 인스턴스 사용 — `GameSessionColorDistribution.Distribute`와 동일 이유) 생성 — **코드 반영 완료 (2026-08)**. `NetworkObject` 부착 불필요. **⚠ 1차 구현 버그 (2026-08, ParrelSync 실기 발견 후 수정)**: `cycleSeedSalt`가 씬에 미반영(전부 기본값 0)이라 인스턴스 여러 개가 전부 같은 색·같은 순서로 나옴 — `WindTrap._registry`/`GetHierarchyPath`와 동일하게 **계층 경로 정렬로 `_netIndex`를 자동 배정**해 시드에 섞도록 수정(씬 편집 불필요, 재발 방지). **ParrelSync 2인 재검증 통과 (2026-08)** |
| `AdvancingWall.PermanentAdvance`(챌린지 페널티) | 별도 취급 — **재조사로 정정 + 수정 완료 (2026-08)** | ~~Host/Client가 `HandleChallengeOutcome`으로 동일 고정값을 각자 호출~~ → **오판정이었음.** `T.Boss`에 `ColorTileChallenge` 인스턴스가 5개(`ColorChallenge_Wall_F/B/L/R` + `ColorChallenge_Ceiling`) 있고 전부 같은 `ChallengeOwnerType.ColorTile` 슬롯을 공유하는데, `NotifyChallengeOutcomeClientRpc`가 Host 자신에게는 `if (IsServer) return;`로 스킵되고(Host는 `ResolveRound`의 직접 호출로만 자기 자신 반영) Client에서만 실제로 `OnChallengeOutcome` 이벤트가 발동해 **형제 인스턴스 5개 전부**가 반응 — Client가 Host보다 훨씬 많은 방향에 페널티를 중복 적용하는 실기 버그(Host 2방향 vs Client 4방향) 발견 | `ChallengeStepState`에 `instanceId` 필드 추가 + `NotifyChallengeOutcomeClientRpc(bool, int instanceId=0)`로 확장, `ColorTileChallenge`에 Inspector `challengeInstanceId` 필드 신설해 owner 가드에 인스턴스 일치 조건 추가 — **코드 반영 완료 (2026-08).** Inspector 배정: `Wall_F/B/L/R`=0~3(같은 Phase, 동시 구독이라 서로 달라야 함), `Ceiling`=0(별도 Phase 컨테이너라 `PhaseManager.EnterPhase()`의 동기 disable→enable로 F/B/L/R과 겹치는 프레임이 없어 ID 재사용 가능, 2026-08 확인). **ParrelSync 2인 검증 통과 (2026-08)** |
| [`WallWaveController`](../Scripts/Stage/WallWaveController.cs) — `playOnStart`(트리거 없이 씬 시작 즉시 재생) | ①자유런 | 트리거 없이 매 프레임 사인파를 계산하는 연속 모션 — `AdvancingWall.ScheduleRoutine()`과 동일하게 각 머신이 같은 시간 소스만 폴링하면 결정론적 | `FixedUpdate()`의 `float t = Time.time;` → `nm.ServerTime.Time` 치환 완료 (2026-08). RPC/Host 가드 불필요 — **코드 반영 + ParrelSync 2인 실기 검증 통과 완료 (2026-08)** |
| `WallWaveController` — `activateOnPlayerTrigger` 경로 | ②트리거+브로드캐스트 (미착수) | `OnTriggerEnter`로 시작 시점이 결정되는 구조가 `WallMoverSequencer.OnTriggerEnter`와 동일 | 이번 라운드 대상 아님(현재 씬 인스턴스는 `playOnStart` 전용) — 향후 트리거형 인스턴스가 배치되면 `WallMoverSequencer` 패턴 그대로 적용 |
| `Nodular`/`Lump`([`Breakable`](../Scripts/Breakable.cs)) | 패턴 B — 별도 축 | E(월드모션)가 아니라 Host 판정 + 1회성 동기화(`SyncBreakClientRpc`) 축. 트리거 감지(`OnTriggerEnter`/`OnCollisionEnter`)는 이미 파이프에 포함돼 있어 E 라인과 안 섞임 | 파이프 그대로 재사용(M과 동일). fuse(`breakDelay`) 연출을 클라에도 방송할지만 미결(§6 참고) |
| [`RingBlendShapePulse`](../Scripts/Stage/RingBlendShapePulse.cs) | 패턴 A — 네트워크 진실 없음 | Collider 고정, BlendShape만 로컬 애니메이션. 판정에 전혀 관여 안 함 | 그대로 로컬 유지 |

**요약**: E 패턴은 결국 ①자유런/②트리거+브로드캐스트 둘 중 하나로만 갈리고, `MovingCorridor`·`WallLineRandomizer`는 컴포넌트 하나가 상황(자유런 경로 vs 트리거 경로)에 따라 두 라인에 걸칠 수 있다는 점만 유의. `Breakable`(B)·`RingBlendShapePulse`(A)는 애초에 E 축이 아니므로 섞지 않고 각자 축 유지.

**영향 파일 (구현 시)**: `Assets/Scripts/Stage/AdvancingWall.cs`(수정 완료), `Assets/Scripts/Stage/ColorWall.cs`(수정 완료, `using Unity.Netcode;` 추가 + `colorSeedSalt` Inspector 필드 신설), `Assets/Scripts/GameSessionWallColorRemap.cs`(`RemapSchedule`에 `System.Random rng = null` 인자 추가, 유일한 호출자인 `ColorWall.cs`만 영향), `Assets/Scripts/Stage/WallLineRandomizer.cs`(수정 완료 — `NetworkBehaviour` 전환 없이 `cycleSeedSalt` + 사이클별 로컬 `System.Random`으로 결정론화), `Assets/Scripts/Stage/MovingCorridor.cs`(salt 목록 주석에 `0x434F4C57`/`0x574C525A` 추가). `WallMover.cs`/`WallMoverSequencer.cs`/`Breakable.cs`/`RingBlendShapePulse.cs`는 무수정(재사용/유지). `WallWaveController.cs`는 `playOnStart` 경로 **수정+검증 완료**(2026-08) — `activateOnPlayerTrigger` 경로는 미착수(현재 씬에 해당 인스턴스 없음). `MovingCorridor.cs` 본체는 `startActive` 경로 **수정 완료(2026-08, ParrelSync 검증 대기)** — `activateOnPlayerTrigger` 경로는 미착수(현재 씬에 해당 인스턴스 없음).

**상태**: 축 확정. `WallWaveController`(`playOnStart`, ①자유런) **완료**(코드+ParrelSync 2인 검증). `MovingCorridor`(`startActive` 경로) **코드 반영 완료 (2026-08)** — ParrelSync 2인 검증 대기. **`AdvancingWall`/`ColorWall`/`WallLineRandomizer`(T.Boss 착수, 2026-08) 코드 반영 + ParrelSync 2인 검증 통과 완료.** 나머지(`MovingCorridor`(`activateOnPlayerTrigger` 경로, 이 씬 미사용))는 구현 전 — 실사용 인스턴스 배치 시 반영(§3 결정 1).

### 3.4 T2 MemoryPath/ColoredMemoryPath 조사 결론 (2026-08 확정 — 네트워크 작업 불필요)

**배경**: 한때 `MemoryPath`/`MemoryPathTile`에 네트워크 동기화 코드(`NetworkObject`/`NetworkVariable` 등)를 추가했다가 되돌렸다. 이유는 (1) Safe 경로가 고정이라 시드 문제가 없고, (2) Trap을 밟으면 즉사라는 판정 하나뿐이라는 것. 재조사로 이 판단이 맞았음을 확인했고, `ColoredMemoryPath`(색 버전 — `GameSession.IsColorActive()`로 활성 색만 필터링하는 것 외엔 구조 동일)도 같은 결론.

**결론 근거**:

- **Trap 즉사**: `MemoryPathTile.OnCollisionEnter()`가 `NetworkObject`/`IsServer` 가드 없이 로컬로 `NetworkDamageUtil.ApplyInstantKill(player)`를 호출하지만, 이 유틸 자체가 `if (!nm.IsServer) return;`으로 서버 전용 판정을 내장하고 있어(`Assets/Scripts/Network/NetworkDamageUtil.cs`) 트랩 오브젝트 쪽에 별도 가드가 필요 없다. **새 패턴이 아니라 `ColoredMemoryPathTile`/`PioneerPathTile`/`DoorController`가 이미 쓰는, `072e0eb`(네트워크 데모 수준 완료) 커밋에 포함된 기존 검증 패턴**과 동일 — 챌린지마다 새 `NetworkBehaviour` 발명 금지(`architecture.mdc`) 원칙에도 맞다.
- **Safe 경로 고정**: `MemoryPathTile.role`(Safe/Trap)이 Inspector 고정값이고 런타임 랜덤이 없어, `ColorWall`/`WallLineRandomizer`가 겪은 "머신마다 다른 시드" 문제 자체가 발생할 수 없는 구조.
- **판정 수렴 원리**: Trap/Safe 판정은 로컬 물리(`OnCollisionEnter`)로 이뤄지지만, 플레이어 위치가 `ClientNetworkTransform`으로 전 머신에 복제되므로 "누가 어느 발판을 밟았는지"는 모든 머신이 거의 동일한 시점에 로컬로 감지한다 — RPC/NV 없이도 `_safeStepped` 카운트와 `OnFailed` 발동이 전 머신에서 사실상 동시에 수렴한다.
- **미리보기 시작 타이밍**: 시간차 자체(수십~수백 ms)는 문제가 안 되지만(1회성 타이머라 드리프트 누적 없음), `startOnAwake=true`로 씬 로드 즉시 시작하면 스폰이 늦은 클라이언트가 미리보기를 통째로 놓칠 수 있는 문제가 있었다 — 단, **이건 이미 `MemoryPathIntroController`(카메라 인트로 오케스트레이터, 기존 구현체)가 해결하고 있었음**. `StageStartGate.OnCountdownComplete`(Host/Client 동일 타이밍에 발동) → `MemoryPathIntroController.BeginIntro()` → 카메라 탑다운 전환 + 리드인 대기 → **이 구역의 모든 경로(`MemoryPath`/`ColoredMemoryPath`/`PioneerPathManager`) `StartPreview()`를 한 번에 호출** → 전체 Challenge 진입 대기 → 베리어 Open + `stageManager.StartStage()`까지 이미 한 파이프로 구성돼 있다. `StartPreview()`를 `OnCountdownComplete`에 직접 연결하면 이 카메라 인트로 시퀀스가 통째로 생략되므로 **하지 말 것** — `startOnAwake=false`만 지키면 나머지는 `MemoryPathIntroController`가 전담.
- **라운드 진행 UI**: `MemoryRoundObjective`(`RoundProgressObjective` 상속, `StageObjective` 구현)가 Stage2의 구역 3개(`StageManager2.1/2.2/2.3`) 진행을 별도로 추적 — 각 구역 `StageStartGate.OnCountdownComplete` → `BeginSectionN()`도 같이 연결(진행 UI 갱신 전용, `MemoryPathIntroController`와는 목적이 다른 별개 리스너).

**코드 반영 (2026-08)**: `MemoryPath.cs`/`ColoredMemoryPath.cs` 둘 다 `startOnAwake` 기본값 `true`→`false`로 갱신(주석은 `MemoryPathIntroController`가 호출 주체임을 명시). `StartPreview()`/`MemoryPathIntroController.BeginIntro()` 둘 다 기존에 이미 구현돼 있어 로직 변경은 없음 — 이번 조사로 "새로 만들 것 없음"이 재확인된 셈.

**사용자 Inspector 작업 (필수)**: T.Stage2의 `MemoryPath`/`ColoredMemoryPath` 오브젝트 — `Start On Awake` 체크 해제(기존 씬 값은 코드 기본값 변경으로 자동 갱신 안 됨). 각 구역 `StageStartGate`는 `stageManager` 필드를 **비워두고**(`MemoryPathIntroController`가 대신 호출하므로), `On Countdown Complete()`에 `MemoryPathIntroController.BeginIntro()` + `MemoryRoundObjective.BeginSectionN()`을 연결. **ParrelSync 2인 검증 대기.**

---

## 4. 작업 순서 (확정 — 실제 착수는 다음 라운드부터)

```
T1 Must — 전부 완료(패드/볼더/문 ParrelSync 2인 검증 통과, 2026-08)
  → T3 — 완료(SpikeLane 앵커·WallWaveController 자유런 전부 ParrelSync 2인 검증 통과, 2026-08)
  → T4(OX 스모크 + MovingCorridor ParrelSync 2인 검증 — 코드는 반영 완료)   ← 다음 착수
  → T2(Memory 조사 완료 — §3.4, ParrelSync 2인 검증만 남음 + Door §3.1 재사용)
  → T5(Floor 스모크 + AI §3.2 신규 구현, T.Stage5 단독)
  → T.Boss(씬 인스턴스 확인 남음 + AdvancingWall/ColorWall/WallLineRandomizer §3 결정1 코드 반영 + ParrelSync 2인 검증 통과 완료)
```

각 씬 착수 시 §9B 구조화 로그(`NetLog.Transition`)를 신규 코드 전환점에 적용한다 — 기존 M 코드 소급 없음(`NetworkDesign.md` §9B 확정).

---

## 5. 상호 참조

- 관측성 규칙 SSOT: [`NetworkDesign.md`](NetworkDesign.md) §9B.
- 그룹 1(B) 트랩 세부: [`TrapNetworkBoard.md`](TrapNetworkBoard.md).
- M.Stage 축 완료 기록 + "T 라운드 이월 체크리스트": [`MStageNetworkBoard.md`](MStageNetworkBoard.md).

---

## 6. 이번 라운드에서 하지 않은 것 (범위 밖)

- `MovingCorridor`(`activateOnPlayerTrigger` 경로, 이 씬 미사용) 실제 게임 코드 수정 (E 표준 미적용 — 실사용 인스턴스 배치 시 처리, §3 결정 1). **Door는 §3.1로 완료됨, `MovingCorridor`(`startActive` 경로)는 T4에서 코드 반영 완료(ParrelSync 검증만 남음), `AdvancingWall`/`ColorWall`/`WallLineRandomizer`는 T.Boss 착수로 코드 반영 + ParrelSync 2인 검증 통과 완료 — 이 목록에서 제외.**
- `Nodular`/`Lump`(`Breakable`) fuse(`breakDelay`) 연출을 클라에 사전 방송할지 여부 결정 — §3.3에 미결로 등록만, 이번 라운드 결론 없음
- M.Stage 기존 코드에 구조화 로그 소급 적용 (사용자 확정: T 신규만)
- 씬/프리팹 파일 직접 수정 (`unity-mcp-readonly.mdc` — 체크리스트만 제공. Door 프리팹의 `DoorNetworkSync`/`NetworkObject` 컴포넌트 제거는 사용자가 직접 수행)
