# M Stage Network Board

> **역할:** 미확정 파이프라인을 여기서 잡고, **확정되면 [`NetworkDesign.md`](NetworkDesign.md) §9 / §9.1로 승급**한다.  
> (예: 발사체 **B안** — 보드·논의 → Docs 고정.)  
> **빈 체크리스트 전용이 아님.** 큰 틀을 정하기 위한 작업 md.

**현재 인게임 최우선:** M.Stage 네트워크 완료 (`NetworkDesign` §9.1).  
**현재 보드 포커스:** `M.Stage3` · **C 패턴 · ColorTile** — **OX Quiz는 검증 통과 후 [`NetworkDesign.md`](NetworkDesign.md) §11B(챌린지 축 SSOT)로 승급 완료.** 이 보드의 §1(축 골격)·§2(OX 개별 잠금 규칙)는 이제 §11B가 SSOT이며, 아래 §1~§4는 **승급 완료 기록**으로만 남긴다 — 앞으로 축 골격 자체를 바꿀 일이 있으면 여기 말고 §11B를 고칠 것.

---

## 현재 상태 (다음 세션 시작점 — 여기부터 읽을 것)

**요약:** 축 #4 골격 확정(§1) → OX 코드 구현 완료 → ParrelSync 2인 발테스트에서 문제 동기화 버그 1건 발견·수정 → **재테스트 통과 (2026-07-21) → `NetworkDesign.md` §11B로 승급 완료.**

**다음 세션 시작점:** `M.Stage3` **ColorTile**을 §11B.3 매핑표대로 동일 축(①Trigger→②RoundStart→③Generate→④Judge→⑤Resolve)에 복제. ColorTile은 ①Trigger가 "스케줄(시간 기반)"이라 Host `Update()` 자체가 이미 단일 소스여야 한다는 점만 OX와 다르다 — §11B.3 참고.

### 지금까지 실제로 한 일 (코드, 전부 완료)

1. `Assets/Scripts/Network/StageNetworkState.cs` — 축 #4 공통 API 추가
   - `ChallengeStepState` 구조체(`seed`/`stepIndex`/`stepStartServerTime`) + `NetworkVariable<ChallengeStepState> _challengeStep` **1개**로 통합 관리
   - `NetworkVariable<bool> _challengeCleared`
   - Host 전용 메서드: `ChallengeStart(seed)` / `ChallengeStepBegin(stepIndex)` / `ChallengeCleared(bool)`
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
