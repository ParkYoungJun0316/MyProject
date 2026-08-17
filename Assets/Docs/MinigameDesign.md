# Minigame Design

스테이지에 배치되는 **미니게임(챌린지)** 설계 SSOT. OX퀴즈 삭제 이후 신설 — 미니게임이 1개 더 추가될 수 있어, 특정 미니게임 하나에 종속되지 않는 별도 문서로 분리한다.

관련: [`NetworkDesign.md`](NetworkDesign.md) §11B (챌린지 축·`ChallengeOwnerType`·시드 동기화 SSOT), [`GameArchitectureBoundaries.md`](GameArchitectureBoundaries.md).

**범례**

| 태그 | 의미 |
|------|------|
| **[확정]** | 사용자 승인 완료 — 구현 시 그대로 따름 |
| **[설계 중]** | 방향은 잡혔으나 세부 미확정 |
| **[열린 질문]** | 구현 전 확인 필요 |

---

## 0. 배경

- **OX퀴즈 삭제 결정.** 플레이테스트 피드백: "공부하는 것도 아니고 머리 아프고 지루하다." → 삭제 후 다른 미니게임으로 교체.
- 영향받는 기존 자산: `OXQuizManager`/`OXQuizObjective`/`OXQuizTile`/`OXQuizUI` (사용 씬 `M.Stage2`, `T.Stage4`) — 제거 체크리스트는 §3 참고.
- `NetworkDesign.md` §9.1.4 매핑표("1 | `M.Stage2` | OXQuiz")도 이 미니게임 교체가 확정되면 갱신 필요 — 이 문서에서 먼저 설계를 잠그고 나중에 반영.

---

## 1. 미니게임 A — SideSplit (좌우 분기)

### 1.1 컨셉

통로가 **왼쪽/오른쪽**으로 갈라지고, 각 방향 끝에 도달 판정 지점이 있다. 라운드 시작 시 UI가 "몇 명이 어느 방향으로 가야 하는지"(+ 특정 색 포함 여부)를 공지하고, 플레이어들은 제한시간 안에 정확한 인원·색으로 재배치해야 통과한다.

예시:
- "왼쪽 3명, 오른쪽 1명"
- "왼쪽 1명(노란색), 오른쪽 3명"

### 1.2 확정 규칙 **[확정]**

| 항목 | 규칙 |
|---|---|
| 인원 분배 | **전체 인원이 반드시 다 나뉘어야 함** (좌+우 = 활성 플레이어 전원, 남는 인원 없음) |
| 인원 판정 | **정확히 일치**해야 통과 (지정 인원보다 많거나 적으면 실패) |
| 방향 | **좌/우 2분기만** (중앙·3분기 없음) |
| 색상 조건 | **라운드마다 선택적** — 색 없는 라운드도 존재. 초반 라운드는 색 조건 없이 시작, 뒤쪽 라운드부터 색 조건 등장 (초반 완화 → 후반 난이도 상승 커브) |
| 판정 시점 | **타이머 종료 시점 스냅샷 판정** (OX퀴즈와 동일 — 실시간 유지 판정 아님) |
| 라운드 반복 | OX퀴즈처럼 **여러 라운드 반복** (동일 스테이지 인스턴스 내 N라운드 클리어해야 최종 클리어) |
| 페널티 | 조건 불일치 시 **전원 데미지 적용 후 다음 라운드로 계속 진행** (OX퀴즈와 동일 — 재시도 루프 아님) |

> **확인 완료:** "총 5스테이지 중 대략 3~5스테이지는 색 조건"의 "스테이지"는 오버월드 스테이지가 아니라 **이 미니게임 인스턴스 내부 라운드 수**(OX퀴즈의 "1/5 문제"와 동일한 구조)로 확인됨 — 사용자 확인 완료(2026-08-17).

### 1.3 라운드 구조 (구현 완료)

- 스테이지 인스턴스당 라운드 수: `SideSplitChallenge.totalRounds` (Inspector, 기본 **5**)
- 색 조건 포함 라운드 수: `minColorRounds`~`maxColorRounds` (Inspector, 기본 **3~4** — `totalRounds`보다 작게 설정해 최소 1라운드는 항상 색 조건 없이 시작)
- 색 조건은 **뒤쪽 라운드부터** 배정 (`RegenerateRoundPlan()` — 앞쪽 라운드는 항상 색 조건 없음)
- 라운드별 랜덤 생성 순서(시드 기반, 전 머신 동일): ① 좌/우 인원 분배(전원 소진, `rng.Next(0, total+1)`) ② 이 라운드가 색 조건 라운드면 색 배정 쪽(좌/우 중 인원 0이 아닌 쪽 우선) + 활성 색 중 1개(`GameSessionColorDistribution.Distribute(1, rng)`) 결정

### 1.4 판정 · 페널티 (구현 완료)

- 타이머 종료 시점에 좌/우 각 판정 볼륨(`SideSplitZone`)에 있는 살아있는 플레이어를 물리 오버랩(OverlapBox)으로 스냅샷
- 판정 성공 조건: 좌측 인원 = 지정값 AND 우측 인원 = 지정값 AND (양쪽 동시 점유·미점유 인원 없음) AND (색 조건 있으면) 지정 색 플레이어(`isUniqueColor && playerColorType == requiredColor`)가 지정 방향에 존재
- 실패 시: **전원**에게 데미지(`NetworkDamageUtil.ApplyDamage`, Host만) → 생존자 있으면 다음 라운드 계속
- 결과 연출은 Host가 직접 재생 + `NotifyChallengeOutcomeClientRpc`로 Client 동기화 (OX퀴즈와 동일 원칙)
- **설계 가정:** 실패 시 "누가 잘못 섰는지" 개별로 가려내지 않고 전원 동일 데미지 처리(팀 전체 조건이라 개인 귀책이 애매함) — 추후 다르게 가고 싶으면 `SideSplitChallenge.Judge()`만 수정하면 됨

### 1.5 UI (구현 완료)

- `SideSplitUI` — Unity Localization `LocalizedString.Arguments`(Smart Format) 기반 문장형 템플릿 3종: 색 조건 없음/왼쪽에 색 조건/오른쪽에 색 조건
- 색상명(`Blue`/`Purple`/`Green`/`Yellow`)도 각각 `LocalizedString`으로 Inspector에서 String Table 연결(문자열 직접 입력 아님, `OXQuizManager.OXQuestion` 패턴과 동일)
- 진행도("n/m 라운드")는 기존 `ObjectiveUI`/`RoundProgressObjective` 패턴 그대로 재사용 (`SideSplitObjective`)

### 1.6 남은 작업 (에디터 — 사용자)

- [ ] String Table에 `SideSplitUI`의 `LocalizedString` 필드 10개(안내 문구 3종 + 결과 텍스트 3종 + 색상명 4종) 엔트리 생성·연결
- [ ] `M.Stage2`/`T.Stage4`(또는 신규 배치 스테이지)에 `SideSplitChallenge`/`SideSplitZone`(좌/우 2개)/`SideSplitObjective`/`SideSplitUI` GameObject 배치 + Inspector 필드 연결
- [ ] `StageStartGate.OnCountdownComplete` → `SideSplitChallenge.StartChallenge()` 연결(기존 `OXQuizManager.StartQuiz` 연결 대체)
- [ ] 몇 개 스테이지(오버월드)에 배치할지 결정 (OX퀴즈처럼 `M.Stage2` + `T.Stage4` 1곳씩만? 여러 곳?)

---

## 2. 구현 매핑 — 기존 챌린지 축 재사용 (구현 완료)

새 미니게임은 완전히 새로운 프레임워크가 아니라 **OX퀴즈와 동일한 §11B 챌린지 축**을 재사용했다 (`NetworkDesign.md` §11B "OX에서 먼저 잠그고 복제" 원칙).

| 필요 기능 | 재사용 소스 | 실제 구현 |
|---|---|---|
| 시드 동기화(Host→전체) | `StageNetworkState.ChallengeStart(seed, owner)` | `ChallengeOwnerType.SideSplit` 추가(`StageNetworkState.cs`), `SideSplitChallenge.StartChallenge()` |
| 라운드별 랜덤 생성 | `OXQuizManager.RegenerateQuestionOrder()` 패턴 (`System.Random(seed)`) | `SideSplitChallenge.RegenerateRoundPlan()` — 좌/우 인원 분배 + 색 조건, 색 선택은 `GameSessionColorDistribution.Distribute(1, rng)` 재사용(totalSlots=1로 호출하면 활성 색 중 1개만 시드 기반으로 뽑힘) |
| 좌/우 판정 볼륨 | `OXQuizTile.GetPlayersInVolume()` (OverlapBox) | `SideSplitZone.GetPlayersInVolume()` — 좌/우 2개 인스턴스, 판정은 `SideSplitChallenge.Judge()`(정확 인원수 + 필수색상 포함) |
| 라운드 진행도 목표 | `RoundProgressObjective` (`OXQuizObjective`와 동일 상속) | `SideSplitObjective` |
| 진행도 UI | `ObjectiveUI` | 그대로 재사용 (변경 없음) |
| 시작 트리거 흐름 | `StageStartGate → StartStage/StartQuiz` 연동 | `StageStartGate.OnCountdownComplete → SideSplitChallenge.StartChallenge()`로 교체 (씬 연결은 사용자 작업, §1.6) |
| 조건 불일치 데미지 | `NetworkDamageUtil` | 그대로 재사용 (변경 없음) |
| 문장형 안내 UI | (신규) | `SideSplitUI` — `LocalizedString.Arguments`(Smart Format) 기반 동적 문장 템플릿 |

**파일 목록**
- `Assets/Scripts/Stage/SideSplitChallenge.cs` — 매니저 (+ `SideSplitRound`/`SideSplitRoundInfo`/`SideSplitRoundEvent`/`SideSplitFloatEvent`)
- `Assets/Scripts/Stage/SideSplitZone.cs` — 좌/우 판정 볼륨
- `Assets/Scripts/Stage/SideSplitObjective.cs` — 스테이지 목표 연동
- `Assets/Scripts/UI/SideSplitUI.cs` — 문장형 안내 UI
- `Assets/Scripts/Network/StageNetworkState.cs` — `ChallengeOwnerType.SideSplit` 추가

**결론:** 코드 재사용률이 높아 난이도는 예상대로 **낮음~중간**이었다. 신규 설계는 "좌/우 인원+색상 랜덤 생성기", "좌/우 판정 볼륨", "문장형 안내 UI" 3가지로 국한됐다.

---

## 3. OX퀴즈 제거 체크리스트

> 에이전트는 워크스페이스 파일(.cs, Docs)만 삭제/수정 가능. 씬(`.unity`)·프리팹(`.prefab`) 내 참조 제거는 **사용자가 에디터에서 직접** 수행 (Unity MCP 읽기 전용 규칙).

**스크립트 (에이전트 삭제 완료 — 2026-08-17)**
- [x] `Assets/Scripts/Stage/OXQuizManager.cs`
- [x] `Assets/Scripts/Stage/OXQuizObjective.cs`
- [x] `Assets/Scripts/Stage/OXQuizTile.cs`
- [x] `Assets/Scripts/UI/OXQuizUI.cs`
- [ ] `Assets/Docs/OXQuizTranslations.md` — 삭제하지 않고 보존(과거 번역 작업 기록). 필요 없어지면 별도 요청 시 정리

**씬/프리팹 (사용자가 에디터에서 직접 — 체크리스트만 제공)**
- [x] `M.Stage2.unity` — SideSplit 교체 완료 (사용자 에디터 작업)
- [x] `T.Stage4.unity` **Stage4.1만** — OXQuiz missing script 제거 후 `SideSplitChallenge`/`SideSplitZone`×2/`SideSplitObjective` 배치, `StageStartGate.OnCountdownComplete` → `StartChallenge()` 연결 (2026-08-17). Stage4.2는 미변경
- [ ] `Assets/Prefab/UI.prefab` — `OXQuiz_Panel`(`OXQuizUI` 부착) 제거, `SideSplitUI` 부착 패널로 교체 + `mainText`/`timerText`/`LocalizedString` 필드 연결
- [x] String Table `M.Stage.Quiz` / `T.Stage.Quiz` — 컬렉션·로케일 테이블 28개 삭제 + Addressables 엔트리 26개 제거 (2026-08-17)
- [ ] `Assets/Audio/SFX/OXQuiz_Timer.wav`, `OXQuiz_Correct.wav` — 신규 미니게임에서 재사용할지 결정 (현재 `SideSplitChallenge`/`SideSplitUI`에는 SFX 연동 코드 없음 — 필요 시 `OnTimerTick`/`OnRoundSuccess`/`OnRoundFailed` UnityEvent에 연결)

**문서**
- [x] `NetworkDesign.md` §9.1.4 매핑표 갱신 (`M.Stage2` 행 — OX 취소선 + SideSplit 교체 표기)
- [x] `NetworkDesign.md` §11B.3 표에 SideSplit 행 추가, OX Quiz 행 취소선 처리(제거 기록 보존)

---

## 4. 미니게임 목록 (확장 슬롯)

이 문서는 미니게임 1개에 종속되지 않는다. 추가 미니게임 논의 시 아래 표에 행을 추가하고 §1과 동일한 형식의 섹션을 이어서 작성한다.

| 이름 | 상태 | 배치 스테이지 | 비고 |
|---|---|---|---|
| ~~OX퀴즈~~ | **삭제 완료** | ~~`M.Stage2`, `T.Stage4`~~ | §0, §3 참고 |
| SideSplit (좌우 분기) | **코드 작성 완료 — 씬 배치·검증 대기** | 미확정 (§1.6) | §1/§2 참고 |
| 미니게임 B | **미정** | — | 사용자 언급: "미니게임 1개 더 추가될 수 있음" — 아이디어 확정 시 §5로 추가 |
