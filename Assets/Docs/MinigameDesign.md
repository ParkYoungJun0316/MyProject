# Minigame Design

스테이지에 배치되는 **미니게임(챌린지)** 설계 SSOT. OX퀴즈 삭제 이후 신설 — 미니게임이 1개 더 추가될 수 있어, 특정 미니게임 하나에 종속되지 않는 별도 문서로 분리한다.

관련: [`NetworkDesign.md`](NetworkDesign.md) §11B (챌린지 축·`ChallengeOwnerType`·시드 동기화 SSOT), [`GameArchitectureBoundaries.md`](GameArchitectureBoundaries.md), [`CoopStageAudit.md`](CoopStageAudit.md) (2인 테스트·버킷) · [`CoopStageAudit.M.md`](CoopStageAudit.M.md) / [`CoopStageAudit.T.md`](CoopStageAudit.T.md) (챌린지 **콘텐츠** 변경의 기준).

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

통로가 **왼쪽/오른쪽**으로 갈라지고, 각 방향 끝에 도달 판정 지점이 있다. 라운드마다 3·2·1 카운트다운 후 방향 간판 아래 하트로 "몇 명이 어느 방향으로 가야 하는지"(+ 특정 색 포함 여부)가 공개되고, 플레이어들은 제한시간 안에 정확한 인원·색으로 재배치해야 통과한다. (공지 방식은 2026-09-22 화면 문장 UI → 월드 표시로 교체 — §1.8)

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

### 1.5 UI

- ~~`SideSplitUI` — Unity Localization `LocalizedString.Arguments`(Smart Format) 기반 문장형 템플릿 3종: 색 조건 없음/왼쪽에 색 조건/오른쪽에 색 조건~~
- ~~색상명(`Blue`/`Purple`/`Green`/`Yellow`)도 각각 `LocalizedString`으로 Inspector에서 String Table 연결~~
- **2026-09-22 폐기 → 월드 표시(`SideSplitWorldDisplay`)로 교체, §1.8.** 이유: 화면 중앙 검정 박스 + 흰 문장(`앞 2명 / 뒤 1명 / …`)은 문장을 처음부터 읽어야 방향별 인원이 나오고, 판을 봐야 할 순간에 화면 가운데를 가렸다. 흰 카운트다운 숫자도 밝은 배경에서 안 읽혔다.
- 진행도("n/m 라운드")는 기존 `ObjectiveUI`/`RoundProgressObjective` 패턴 그대로 재사용 (`SideSplitObjective`) — 변경 없음

### 1.6 남은 작업 (에디터 — 사용자)

- [ ] String Table에 `SideSplitUI`의 `LocalizedString` 필드 10개(안내 문구 3종 + 결과 텍스트 3종 + 색상명 4종) 엔트리 생성·연결
- [ ] `M.Stage2`/`T.Stage4`(또는 신규 배치 스테이지)에 `SideSplitChallenge`/`SideSplitZone`(좌/우 2개)/`SideSplitObjective`/`SideSplitUI` GameObject 배치 + Inspector 필드 연결
- [ ] `StageStartGate.OnCountdownComplete` → `SideSplitChallenge.StartChallenge()` 연결(기존 `OXQuizManager.StartQuiz` 연결 대체)
- [ ] 몇 개 스테이지(오버월드)에 배치할지 결정 (OX퀴즈처럼 `M.Stage2` + `T.Stage4` 1곳씩만? 여러 곳?)

### 1.7 4방향 확장 (T.Stage4 전용) **[확정]**

`M.Stage2`는 좌/우 2방향 그대로 유지. `T.Stage4`는 좌/우/앞/뒤 4방향으로 확장 — 이미 있는 앞/뒤 분기 통로 지형을 그대로 사용(레벨 블록아웃 추가 불필요).

**확정 규칙**

| 항목 | 규칙 |
|---|---|
| 활성 방향 결정 | `SideSplitChallenge.frontZone`/`backZone`을 **둘 다** Inspector에 연결하면 4방향 모드로 자동 전환. 코드 분기 없음 — `leftZone`/`rightZone`만 연결된 인스턴스(`M.Stage2`)는 그대로 2방향 |
| 인원 분배 | 활성 방향(2개 또는 4개) 전원이 반드시 다 나뉨 — 2방향과 동일 원칙의 N방향 일반화. 고정 순서(좌→우→앞→뒤)로 순차 소진하며 배정, 일부 방향이 0명이 되는 것도 정상(그 자체가 유효한 조건) |
| 판정 | 기존과 동일 — 활성 방향 전부 정확히 일치해야 통과. 2개 이상 zone 동시 점유·어느 zone에도 없는 생존자가 있으면 그 자체로 실패 |
| 색상 조건 | 인원이 0이 아닌 활성 방향들 중에서만 배정(기존 좌/우 로직의 직접 확장) |
| UI 문구 | 2방향 문장형 템플릿(`promptColorLeft`/`Right`)은 그대로 유지. 4방향은 별도 템플릿 5종(`promptNoColor4` + 방향별 색상 템플릿 4종) 신설 — 인자 순서 항상 앞/뒤/좌/우 고정 |

**구현 완료 (코드)**

- `SideSplitChallenge` — `frontZone`/`backZone` 필드 추가, `SideSplitRound`/`RoundInfo`에 `frontCount`/`backCount` 추가, `colorOnLeft(bool)` → `colorDirection(SideSplitDirection enum)`으로 확장. `RegenerateRoundPlan()`/`Judge()`는 활성 zone 리스트 기반으로 일반화(2방향일 때 결과는 기존과 완전히 동일)
- `SideSplitZone` — Gizmo 색 구분용 `isLeftSide(bool)` → `gizmoDirection(GizmoDirection enum: Left/Right/Front/Back)`으로 교체(판정 로직과 무관, 순수 표시용)
- `SideSplitUI` — 4방향 전용 템플릿 필드 5개 추가, `challenge.IsFourDirection`으로 2방향/4방향 문구 분기

**남은 작업 (에디터 — 사용자)**

- [ ] `T.Stage4`에 `SideSplitZone` 2개(앞/뒤) 배치, `SideSplitChallenge.frontZone`/`backZone`에 연결
- [ ] 기존 `SideSplitZone_Left`/`Right`(및 신규 앞/뒤 2개)의 Gizmo 표시 필드가 `isLeftSide`→`gizmoDirection`으로 바뀜 — 필요하면 Inspector에서 방향 재선택(순수 표시용, 판정에는 영향 없음)
- [ ] `T.Stage4`의 `SideSplitUI`에 4방향 템플릿 필드 5개(`promptNoColor4`/`promptColorFront4`/`Back4`/`Left4`/`Right4`) 연결
- [ ] String Table에 4방향 템플릿 엔트리 5개 신규 생성("앞 {0}명 / 뒤 {1}명 / 좌 {2}명 / 우 {3}명" 형식 + 색상 버전 4종)
- [ ] `T.Stage4`의 `totalRounds`/`minColorRounds`/`maxColorRounds`는 인원 수 대비 4방향 분배 체감 난이도를 실제 플레이해보고 튜닝(코드 변경 불필요, Inspector 값만 조정)
- [ ] ParrelSync 2인 검증(4방향 분배·판정·UI 동기화)

### 1.8 카운트다운 · 재배치 · 월드 표시 (2026-09-22) **[확정]**

> **2026-09-23 §1.9 보충:** 이 절에서 바뀌는 것은 둘뿐 — 아래의 "방향 글씨"가 **모양 4종**이 되고, 하트 패널이 **자기 담당 모양 하나만** 보인다. 그 외(카운트다운·재배치·타이머·결과·SFX·색 조건)는 이 절 그대로.

플레이 피드백: ① 안내 UI가 안 읽힘(§1.5) ② 라운드가 언제 시작하는지 모르고 갑자기 타이머만 돎 ③ 라운드 사이 스핀 중엔 방향을 못 읽음.

**라운드 한 번의 흐름 (전 머신 동일 — 전부 `ChallengeStepStartServerTime` 기준)**

```
ChallengeStepBegin(i) ─ 재배치 스냅 ─ [카운트다운 preRoundCountdown=3초]
                                      간판 글씨 숨김, 네 간판 모두 큰 숫자 3·2·1
                     ─ OnRoundReady ─ [읽는 시간 revealReadHold=1초] 방향 글씨 + 하트 공개, 채움 꽉 찬 채 정지
                                    ─ [제한시간 roundTimeLimit(ByPlayerCount)] 네 간판 채움이 줄어듦 + 진행 틱
                     ─ 판정(Host) ─ [resolveDelay=3초] 간판 채움이 성공/실패 색으로 꽉 참
                     ─ ChallengeStepBegin(i+1) …
```

| 항목 | 규칙 |
|---|---|
| 카운트다운 | **첫 라운드 포함** 매 라운드 3초. 이 동안 타이머 안 돎. `SideSplitChallenge.preRoundCountdown`, 이벤트 `OnCountdownTick(남은 초)`. 제한시간·판정 시각은 스텝 시작 + 카운트다운 이후로 밀림 |
| 라운드 간격 | 결과 표시 `resolveDelay` **3초** + 카운트다운 **3초** + 읽는 시간 `revealReadHold` **1초** (M.Stage2·M.Boss). 처음엔 결과 2 + 카운트다운 3(= 옛 5초)이었으나 "쉬는 시간이 짧다"는 피드백으로 같은 날 결과 +1초, 읽는 시간 +1초 추가. 카운트다운을 늘리지 않은 이유: 카운트다운 중엔 볼 정보가 없어 빈 대기가 됨 — 늘린 시간은 결과 소화와 하트 읽기에 씀 |
| 재배치 (구 "회전") | **스핀 폐기.** `rotationStartRound` 라운드부터 카운트다운 시작 순간 `zoneRig`를 90° 단위로 스냅(`rotationSteps`, 시드 결정 — 변경 없음). 카운트다운 동안 네 간판이 똑같이 숫자만 보여서, 공개 순간 방향이 "다른 자리에서 나타난다". 판 4개가 중심 대칭이라 서는 자리는 그대로. 삭제 필드: `rotationSpinSpeedBase`/`rotationSpinSpeedStep`/`lockBeforeRoundStart` |
| 인원 표시 | 방향 간판 아래 하트 패널. **하트 개수 = 필요 인원**, **색 하트(HP 아이콘) = 그 색 플레이어 필수**, **빈 하트 = 아무나**. 0명 방향은 **하트만 없다** — 간판·패널·타이머는 네 방향 모두 똑같이 보이고 판단은 하트로만 한다(흐리게·강조 없음, 2026-09-22 확정). 글자 없음 → 문구 로컬라이제이션 불필요 |
| 남은 시간 | **네 간판 모두** 글씨 뒤 배경이 꽉 찬 색에서 한쪽으로 줄어듦(숫자 없음). 마지막 1초는 빨강(깜빡임 없이 유지). 바닥 패드엔 타이머 없음(2026-09-22 삭제) |
| 읽는 시간 | 공개 직후 `revealReadHold`초 동안 채움이 꽉 찬 채 멈춰 있다가 줄기 시작. 판정 시각도 그만큼 밀림(제한시간 자체는 그대로) |
| 소리 | 3·2·1 숫자가 바뀔 때마다 `SFXId.Minigame_CountdownTick` 1회. 채움이 **줄기 시작하는 순간**(읽는 시간 끝)부터 `SFXId.Minigame_TimerTick` 루프 → 시간 종료·판정 시 정지 |
| 화면 UI | 없음 — `SideSplitUI`·`UI.prefab`의 `SideSplit_Panel`·String Table `SideSplit` 삭제(2026-09-22) |

**구현 (2026-09-22)**

- `SideSplitChallenge` — `preRoundCountdown`, `revealReadHold`, `OnCountdownTick`, `CurrentRoundTimeLimit` 추가. `TimerRoutine`이 카운트다운 → `OnRoundReady` → 제한시간을 한 코루틴에서 진행. 스핀 코루틴·필드 삭제, 스냅은 `HandleChallengeStepChanged`에서만. 새 RPC·NV 없음
- `SideSplitWorldDisplay`(신규, `Scripts/UI`) — 챌린지 이벤트 구독·표시만. 방향별 `DirectionView`(간판 배경/글씨/카운트다운 TMP/하트 패널/하트 줄/채움 렌더러)
- `SideSplitZone` — 상태 색을 칠할 `visual` Renderer 지정(자식 간판을 잘못 잡지 않게)
- 채움 = 불투명 Unlit + Alpha Clip. 텍스처 알파가 위치 그라데이션이라 `_Cutoff = 1 - 남은비율`로 잘라 줄어들게 한다(커스텀 셰이더 없음)
- 에셋 `Assets/Mat/SplitZone/` — 바닥 패드, 간판 배경·채움·글씨(Figma `Ingame/SplitZone/*.png` 글자 모양), 하트 패널, 흰색 빈 하트(`Figma/Ingame/Heart/None.png` 색만 흰색). 색 하트는 `Figma/Ingame/Heart/*HP.png`
- 씬(M.Stage2·M.Boss) — 존 박스·간판 큐브는 렌더링만 끔(판정 볼륨·충돌 그대로). 표시 오브젝트는 전부 `SplitZoneRig` 자식이라 재배치를 따라감

**남은 작업**

- [ ] 플레이 검증(Host + ParrelSync 클라이언트): 카운트다운 동기, 공개 순간 재배치, 간판 채움, 틱 2종, 성공/실패 색
- [ ] `Minigame_CountdownTick` 클립 준비 → `SFXLibrary` 연결(비어 있으면 무음으로 건너뜀)
- [x] 정리(2026-09-22): `UI.prefab`의 `SideSplit_Panel`, `SideSplitUI.cs`, String Table `SideSplit`(컬렉션+공유 데이터+13개 언어, Addressables 항목 포함) 삭제. M.Stage2·M.Boss·T.Stage4의 UI 인스턴스에 남아 있던 패널 덮어쓰기 항목도 제거(`Scenes/Backup/`은 미변경)

### 1.9 모양 이름표 · 정보 비대칭 (2026-09-23) **[확정]**

플레이 피드백: §1.8로 **표시는 읽히게 됐지만 협상이 사라졌다.** 정보가 가야 할 자리에 그대로 붙어 있어서, 하트 붙은 간판으로 걸어가면 끝이다 — "너가 왼쪽! 파랑은 뒤로!" 같은 대화가 필요 없다. 눈만 있으면 무피해 클리어.

협동 게이트([`CoopStageAudit.md`](CoopStageAudit.md) §4 2인 테스트)는 살아 있다 — "별 1명 / 원 1명" 라운드는 한 사람 몸으로 못 한다. 죽은 것은 그 위의 **협상 층**뿐이라, 판을 갈지 않고 **딱 두 가지만** 바꾼다.

| # | 바뀌는 것 |
|---|---|
| 1 | 방향 글씨(FRONT/BACK/LEFT/RIGHT) → **모양 4종**(★ ● ▲ ■) |
| 2 | 하트를 전원이 같이 봄 → **각자 담당 모양의 하트 패널만** 봄 |

**그대로 가는 것(§1.8):** 카운트다운 3·2·1, 재배치(카운트다운 중 90° 스냅), 채움 타이머, 결과 색, 읽는 시간, SFX 2종, **색 조건은 라운드당 1방향 1개**, "0명 방향은 하트만 없다", 간판·타이머는 네 방향 동일. 옛 화면 문장 UI 복귀도 아니다(§1.5에서 기각).

#### 1.9.1 모양 이름표

| 항목 | 규칙 |
|---|---|
| 소속 | `SplitZoneRig` 자식 **그대로** — 존과 같이 돈다. ★이 북쪽이었어도 다음 라운드엔 남쪽에 가 있을 수 있다. 이 랜덤성이 난이도의 축이므로 건드리지 않는다 |
| 왜 모양인가 | 방향 단어는 회전 뒤 **거짓이 된다** — `rotationStartRound`=2부터 `rotationSteps`가 항상 0이 아니라(`rng.Next(1,4)`), 2라운드부터 `FRONT` 간판이 물리적으로 왼쪽·뒤에 가 있다. 모양은 공간을 주장하지 않는 순수한 이름이라 어디로 움직여도 참이다. 덤으로, 네 명이 카메라를 각자 돌리고 있어 "앞"이 사람마다 다른 문제도 같이 사라진다 |
| 표시 | §1.8 그대로 — 카운트다운 중 숨김, 공개 때 **전원에게** 보임. "★이 이번엔 저기구나"를 공개 순간에 발견하는 것이 재배치의 재미 |
| 4종 | **별 · 웃는 얼굴 · 음표 · 하트**(`Assets/Figma/Ingame/M2/{Star,Face,Note,Heart}.png`). 인원 표시(하트 패널)의 아이콘은 하트가 아니라 **꿀떡**(`Figma/Ingame/Heart/*HP.png`)이라 모양 이름표와 그림이 겹치지 않는다 — 대사·Tip도 인원 표시를 "꿀떡"으로 부른다(2026-09-24 이미지 확인으로 정정: 이전 기술은 둘을 반대로 적었음) |
| 모양 색 | **흰색**(RGB 255, 형태는 알파가 정의). 담당자의 색을 칠하지 않는다 — 색은 이 게임에서 이미 "누가 가야 하는가"(하트 색 조건)라는 뜻이라, 같은 기호에 "누가 보는가"라는 두 번째 뜻을 얹으면 헷갈린다. **검정은 쓰지 않는다:** 간판 배경이 어두워서 타이머 채움이 줄어들수록 모양이 묻힌다(2026-09-23 게임뷰 확인). 폐기된 방향 글씨도 흰색이었다(평균 RGB 191) |
| 임포트 | `textureType` **Default** + **밉맵 켜기**. 간판을 최대 30유닛 거리에서 보므로 밉맵이 없으면 선이 반짝거린다. Figma에서 내보내면 Sprite 타입·밉맵 꺼짐으로 들어오니 매번 확인할 것 |
| 로컬라이제이션 | 불필요(모양) — §1.8 "글자 없음" 원칙 유지 |

#### 1.9.2 정보 비대칭 — 하트 패널

`SideSplitWorldDisplay`는 **표시 전용·클라이언트 로컬**이고 판정은 Host `Judge()`가 따로 한다 → 머신마다 다르게 보여줘도 규칙이 안 깨진다. **새 RPC·NetworkVariable 없음.**

| 항목 | 규칙 |
|---|---|
| 가리는 대상 | **하트 패널 통째로.** 간판(모양)·채움(타이머)·카운트다운 숫자는 전원 동일하게 보인다 |
| 담당 | **모양 ↔ 설계슬롯 4색 1:1 고정** — 별=Blue, 얼굴=Purple, 음표=Green, 하트=Yellow. 판 내내는 물론 **매 판 동일**하다. 시드를 쓰지 않는다. 씬 배선은 `DirectionView.ownerColor`(간판 배치: FRONT=별, RIGHT=얼굴, BACK=음표, LEFT=하트 — `M.Stage2`·`M.Boss` 동일) |
| 인원이 4명 미만일 때 | **주인 없는 모양(= 그 색이 이번 판에 없음)은 전원에게 공개.** [`SessionColorSlotMap`](../Scripts/SessionColorSlotMap.cs)의 "빠진 색 슬롯 → Common(누구나)" 규칙(2026-09-18 확정)을 그대로 쓴다 — **인원별 분기 코드가 없다** |
| 색을 못 얻을 때 | 담당 없음으로 처리 — Common 모양만 보인다. 표시가 멈추거나 예외가 나지 않는 쪽으로 자연 degrade |
| 2방향 인스턴스 | **적용 안 함** — 4방향(`M.Stage2` 2.1 · `M.Boss` P2)만 |

| 인원 | 담당 있는 모양 | Common(전원 공개) | 각자 보는 것 | 합집합 |
|---|---|---|---|---|
| 4인 | 4 | 0 | 내 것 1 | 4 |
| 3인 | 3 | 1 | 내 것 1 + 공개 1 = 2 | 4 |
| 2인 | 2 | 2 | 내 것 1 + 공개 2 = 3 | 4 |
| 1인 | 1 | 3 | 전부 | 4 |

**어느 인원에서도 각자 "나만 아는 패널 1개"를 갖고, 전원의 합집합은 항상 네 자리를 덮는다.** 2인에서도 소통이 죽지 않고, 1인은 자동으로 전부 보이므로 **솔로 특례가 필요 없다.**

**왜 랜덤 배정이 아닌가:** 랜덤은 시드가 필요하고, 시드는 사망 리로드 때 RPC로 온다 — 매핑 확정이 `OnPlayersReady`까지 밀려 **색이 정해지지 않은 창**이 생긴다. 이건 `SessionColorSlotMap`이 구 "활성 색 재배정" 규칙을 폐기하며 이미 겪은 문제라(그 파일 주석), 같은 함정을 다시 파지 않는다. 고정은 Tip으로 가르칠 것도 한 줄뿐이다 — "네 색 모양의 하트만 보인다".

**판정 방법:**

```
내 색 = PlayerSpawnCoordinator.TryGetColor(LocalClientId)

모양 s의 담당색 = slot[s]                           // ★=Blue … 상수 표
SessionColorSlotMap.Resolve(slot[s]) == Common  →  패널 공개 (주인 없음)
slot[s] == 내 색                                →  패널 공개 (내 담당)
그 외                                           →  패널 숨김
```

시드 없음 · 네트워크 없음 · 인원 분기 없음 · 새 상태 없음. `SessionColorSlotMap`은 `GameSession.Apply()`가 활성 색을 확정할 때 이미 세워져 있어 레이스도 없다.

플레이: 블루가 "★에 하트 4개! 다 모여!" → 인원 합이 정해져 있으니 나머지 셋이 0인 것도 따라온다. 반대로 블루의 ★이 비었으면 나머지 셋이 각자 말해야 그림이 나온다. **소통해야 하는 것은 "어디가 ★이냐"가 아니라 "각 자리에 몇 명이냐"** 다 — ★ 위치는 공개되면 다 보인다.

**위험:** 음성이 없는 팀에겐 벽이 된다. Dissonance가 기본이라 평시엔 괜찮지만, 이 절은 말이 오가는 것을 전제한다.

#### 1.9.3 작업

**코드 [완료 2026-09-23]** — `SideSplitWorldDisplay` **하나만**. `DirectionView.ownerColor` 필드, `IsPanelMine()`/`RefreshMyColor()` 추가. 공개 때 내 담당 패널만 켜고, 카운트다운·대기 중엔 네 패널 모두 끈다(빈 패널이 먼저 떴다가 셋이 사라지는 깜빡임 방지). `SideSplitChallenge`·판정·라운드 생성·네트워크는 **손대지 않았다.**

**에디터 [완료 2026-09-23, MCP로 적용]** — 텍스처 임포트 설정(Default+밉맵) → `Assets/Mat/SplitZone/SignLabel_*.mat` 4개의 `_BaseMap`/`_MainTex` 교체 → 두 씬의 `ownerColor` 배선. `SignLabel_START.mat`은 다른 용도라 건드리지 않음. **오브젝트 이동·재배치 없음** — 씬 diff는 각 4줄 추가뿐.

> 씬 저장 시 RectTransform 프리팹 오버라이드 잡음(`value:` 줄 130여 개)이 섞인다. 저장 직후 `git diff`로 확인하고 `value:` 줄은 되돌릴 것.

**남은 작업**

- [x] 모양 4종 확정 + 에셋 (2026-09-23, 흰색으로 변환)
- [x] 코드 구현 + 에디터 배선 (2026-09-23)
- [x] Tip 한 줄(2026-09-24 — `Tip.M.Stage2.1` 1·2번 + 2.1 대사 1번) — `M.Stage2`가 SideSplit 초출이라 비대칭은 가르쳐야 함 ([`StageTipLines.md`](StageTipLines.md)). 문구는 "네 색 모양의 하트만 보인다" 한 줄이면 족함
- [ ] 플레이 검증(ParrelSync 2인 이상): 머신마다 다른 하트 패널, 담당 고정, 회전은 종전 그대로

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
| ~~문장형 안내 UI~~ | ~~(신규)~~ | ~~`SideSplitUI`~~ → **월드 표시 `SideSplitWorldDisplay`** (2026-09-22, §1.8) |

**파일 목록**
- `Assets/Scripts/Stage/SideSplitChallenge.cs` — 매니저 (+ `SideSplitRound`/`SideSplitRoundInfo`/`SideSplitRoundEvent`/`SideSplitFloatEvent`)
- `Assets/Scripts/Stage/SideSplitZone.cs` — 좌/우 판정 볼륨
- `Assets/Scripts/Stage/SideSplitObjective.cs` — 스테이지 목표 연동
- ~~`Assets/Scripts/UI/SideSplitUI.cs` — 문장형 안내 UI~~ (2026-09-22 삭제)
- `Assets/Scripts/UI/SideSplitWorldDisplay.cs` — 월드 표시(카운트다운·하트·채움·틱)
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
- [x] `SFXId.Minigame_OX_Correct` / `Minigame_OX_TimerTick` + `SFXLibrary` 슬롯 삭제 (2026-08-20). 번호 9·10은 재사용 금지. `M.Stage2`/`T.Stage4`의 `SFXEventManager`(sfxId 9/10)는 씬에서 직접 제거

**문서**
- [x] `NetworkDesign.md` §9.1.4 매핑표 갱신 (`M.Stage2` 행 — OX 취소선 + SideSplit 교체 표기)
- [x] `NetworkDesign.md` §11B.3 표에 SideSplit 행 추가, OX Quiz 행 취소선 처리(제거 기록 보존)

---

## 4. 미니게임 목록 (확장 슬롯)

이 문서는 미니게임 1개에 종속되지 않는다. 추가 미니게임 논의 시 아래 표에 행을 추가하고 §1과 동일한 형식의 섹션을 이어서 작성한다.

| 이름 | 상태 | 배치 스테이지 | 비고 |
|---|---|---|---|
| ~~OX퀴즈~~ | **삭제 완료** | ~~`M.Stage2`, `T.Stage4`~~ | §0, §3 참고 |
| SideSplit (4방향) | **월드 표시·카운트다운 적용(2026-09-22) — 플레이 검증 대기** | `M.Stage2`, `M.Boss` P2 (T.Stage4는 삭제 예정 — `CoopStageAudit.T.md`) | §1/§2/§1.7/§1.8 참고 |
| 미니게임 B | **미정** | — | 사용자 언급: "미니게임 1개 더 추가될 수 있음" — 아이디어 확정 시 §5로 추가 |
