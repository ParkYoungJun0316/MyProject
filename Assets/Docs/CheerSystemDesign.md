# Cheer System Design

**응원 시스템** 설계 문서 — 개인 버프(키 입력, 2026-09-14부터) + 팀 버프(음성/`T`키, 팀 공용 키워드) 이원 구조.
관련: [`NetworkDesign.md`](NetworkDesign.md) (네트워크 검증 단계·Host 권한·출시 달력), [`CheerAndTutorialDesign.md`](CheerAndTutorialDesign.md) (Tutorial 구역·게이트 흐름 — CheerName/TeamCheerWord 설정 UI는 Tutorial 씬에 있음).

**범례**

| 태그 | 의미 |
|------|------|
| **[Ship Must]** | **2026-09-16 정식 출시** 전 필수 |
| **[Post-Launch]** | 정식 이후 |

> **콘텐츠 오버라이드 (2026-09-03):** 팀 응원 **효과·쿨**의 콘텐츠 SSOT는 [`CoopStageAudit.md`](CoopStageAudit.md) §9 맵 + [`CoopStageAudit.M.md`](CoopStageAudit.M.md) / [`CoopStageAudit.T.md`](CoopStageAudit.T.md). **+2힐·120초는 코드에서 폐기.** `CheerService`는 `ITeamCheerRevert` — 입 닫힘·침·혀 **됨** (2026-09-04 플레이 확인). T 조임·안개는 아직. RPC·투표·그래머·개인 버프 경로는 유지. 다음 작업 = M §H.5 (Barrier M1 · ColorTile 점수제).
>
> **팀 버프 코드 리뷰 반영 (2026-09-05):** 되돌림이 **Host 권한 명령**이 됐다 — `BroadcastTeamBuffActivatedClientRpc(generation, resumeAt)`. 예전엔 각 머신이 "지금 로컬 창을 닫아라"만 받아서, 창이 아직 안 열린 머신은 명령을 조용히 버리고 혼자 암전/미끄럼이 유지됐다. 함정 첫 창도 `PhaseStartServerTime`에 앵커링(WindTrap과 같은 패턴). 팀 쿨 잔재(`_teamCooldownEndNv`·`OnTeamCooldownClockChanged`·`GameSession` 쿨 저장·`TeamBuffCooldownUI`)는 **전부 삭제.** 아래 표 중 옛 Heal/쿨 서술은 이 항목이 우선한다.
>
> **2026-09-01 전면 개편.** 구 "팀원이 나를 응원해야 버프" 방식(전원 투표로 타인 타겟에게 버프)는 **폐기**. 신규: ①자기 응원 → 즉시 개인 버프, ②팀 전원 공용 키워드 → 팀 전체 버프. 남을 지목해서 응원하는 기능(cross-targeting)은 완전히 삭제됐다.
>
> **구현 현황:** Phase A·B·C·D1·D2 코드 완료 (인수인계 **§10.1**~**§10.4**). D3 안내 문구는 코드에서 제외(사용자 씬 텍스트). D0는 Tutorial 씬에 `CheerService` 배치됨. 남은 건 D4(구역 3) + Phase E 에디터.
>
> **2026-09-14 개인 버프 입력 전면 개편 [설계 확정, 코드 완료].** 음성 인식의 구조적 한계(지연·오인식 — 원하는 타이밍에 즉시 발동이 안 됨)로 인한 플레이어 불만 때문에 **개인 버프 트리거를 음성/숫자키에서 완전히 키 입력으로 전환**한다: `Ctrl`=색변환(기존 유지, 손 안 댐) → `Q`=버프 종류 전환(기존 `RequestToggleBuffTypeServerRpc` 그대로 유지) → `Space`=버프 발동(신규). **자기 CheerName을 외쳐서 개인 버프를 발동하는 음성 경로, 개인 버프 숫자키는 전부 삭제.** CheerName 자체는 없어지지 않지만 `TeamStatusUI` 표시용 닉네임 기능만 남고, Vosk grammar 등록·말해보기 테스트·Tutorial 구역3 개인 응원 체험에서는 제외된다(사용자 결정, 2026-09-14). **추가로 같은 날 확정:** `Q` 버프 종류 전환은 **발동 중·쿨타임 중에도 항상 허용**(기존 `IsBuffActive` 잠금 제거) — 전환은 다음 `Space` 발동부터 적용, 이미 나간 효과에는 영향 없음. 상세는 §1.2·§2.1·§3.1·§3.4·§6, 신규 작업은 **Phase F(§10.5)**.
>
> **2026-09-14 (같은 날 2차 변경) — 팀 응원 `T`키로 재배정 + 이모트 시스템 숫자키 전면 개편 [설계 확정, 코드 완료].** 이모트를 숫자키 `1`~`8` 직접 트리거로 바꾸면서(§6.4, 구 `T`홀드 마우스 휠 폐지) 팀 응원의 숫자키 대체 입력이 **`T`키로 재배정**됐다(그 직전엔 `1`, 그 전엔 `2`였음 — 두 번 재배정). 상세는 §6.3·§6.4. 아래 본문 중 "개인 버프 = 음성/숫자키1", "팀 응원 숫자키 = 1 또는 2", "Q는 발동 중 잠김", "이모트는 T 홀드 휠"이라는 서술은 전부 이 항목들이 우선한다.
>
> **2026-09-14 (같은 날 3차 변경) — 팀 응원 1회 통과, 10초 표 리셋 폐기 [설계 확정, 코드 완료].** 구: 첫 인식 후 `teamCheerTimeoutSeconds`(10초) 안에 전원 미달이면 표 전부 초기화 → 이미 외친 사람도 다시 외침. 신: 창이 열린 동안 **1회 인식 = 그 사람 통과**, 재외침 없음, 느낌표 소거. 성공 조건은 그대로 **전원 각자 1회**. 창은 전원이 통과할 때까지 유지(실패로 닫히지 않음) — **예외 2종: 혀 휩쓸기(`RiseHold` 외 패턴)·보스 턱(`MouthBossJawSmash`)은 함정 발동 후 창(=팀 응원 배너)이 닫혀 뒤늦게 외쳐도 원상복구 안 됨**(사용자 결정 2026-09-14, `CheerAndTutorialDesign.md` §2.1). 플레이어 규칙 잠금은 [`CheerAndTutorialDesign.md`](CheerAndTutorialDesign.md) §2.1. 아래 본문 중 "첫 인식 후 N초 타임아웃으로 표 리셋", "회색=대기 / 초록=인식"은 이 항목이 우선한다.
>
> **2026-09-14 (같은 날 4차 변경, 최종) — 개인 CheerName 커스텀화 완전 삭제 [코드 완료].** 이름은 `PlayerColorUtil.DefaultCheerNames`(berry/guma/sook/dan) **고정값**이다. 입력 UI·`PlayerCheerNameSync`·세션 CheerName 스냅샷(`GameSession.SetSessionCheerNames` 등)·우선순위 역전·CheerName↔TeamCheerWord 충돌 검사·`CheerLexiconBuilder.VariantMap`/`ResolveVariant`는 **전부 삭제**. `CheerService.GetCheerName`/`GetColorIndex`는 고정 배열을 직접 읽는다. grammar 재빌드 헬퍼는 `CheerKeywordEngine.RebuildOwnerLocalGrammar()`로 이전. 머리 위 이름표 `PlayerNameTagUI`는 흑/백 팔레트 구분 문제로 **재도입**(팀 응원 느낌표가 떠 있는 동안은 숨김, §10.3). Tutorial/Interlude 패널은 TeamCheerWord 전용. **아래 본문(§3·§5.2·§10.1~§10.4 인수인계·§11 체크리스트)에서 `PlayerCheerNameSync`·커스텀 CheerName·세션 이름 스냅샷을 다루는 서술은 전부 이 항목이 우선한다(이력으로만 보존).**
>
> **2026-10-07 — 팀 응원 소리 매칭(TeamCheerSound) 설계 초안 [코드 미착수].** TeamCheerWord(영어 단어, Vosk)를 **Host 녹음 소리 1개 + 각자 자기 목소리 등록(틀 검사)** 방식으로 교체한다. Vosk·모델·사전 검사 전부 삭제 예정. 상세 **§14**. 아래 본문의 Vosk·grammar·단어 전제 서술은 §14가 확정되면 전부 이력이 된다.
>
> **2026-09-15 — 음성 인식 반응속도·인식률 개편 [코드 완료].** ①**창 게이팅:** `CheerKeywordEngine`은 팀 응원 창(`CheerService.IsHazardWindowActive`)이 열려 있고 이번 창에서 내가 아직 통과하지 않았을 때만 Vosk에 음성을 넣는다. ②**partial 부활(2연속 확인):** 2026-09-10 "partial 금지"를 **해제** — 매 100ms partial에서 TeamCheerWord가 **연속 2번** 들리면 즉시 제출, final은 보험으로 유지(사용자 결정). ③**리샘플러 교체:** 필터 없는 선형 보간 → 저역통과 FIR + 청크 간 위상 유지. ④**모델 선로드:** 호출부가 사라졌던 `LoadSync`를 부팅 시 백그라운드 로드(`BeginLoad`)로 교체. ⑤**사전 미등재 팀워드 거절:** `TrySetTeamCheerWord` 실패 사유에 `"unknown"` 추가(구 "경고만, 강제 아님" 대체). 상세 §4.7·§5.2. 아래 본문의 "final만 사용", "partial 부활 금지", "사전 미등재는 경고만"은 이 항목이 우선한다.
>

---

## 1. 응원 시스템 개요

### 1.1 왜 바꿨나

기존 방식("나를 제외한 전원이 나를 응원해야 발동")은 마이크·인식 실패·타이밍이 하나만 어긋나도 버프가 안 뜨는 구조라 실전에서 답답함이 컸다. **개인 버프(확정적, 항상 내 힘으로 가능)** + **팀 버프(협동 하이라이트, 확실히 보상)** 조합으로 대체한다.

구 2안("A가 B를 응원하면 B는 버프, A는 쿨타임 손해")은 **드랍**. 개인 버프로 누구나 자력으로 버프를 받을 수 있는 상황에서, 남을 도우면 손해 보는 구조를 얹으면 오히려 협동을 방해하는 역설적 인센티브가 되고, 구현 비용(응원자별 쿨타임 신규 state)도 크다.

### 1.2 규칙 한 줄 요약

| | 개인 버프 | 팀 버프 |
|---|---|---|
| 트리거 | **[2026-09-14 변경] `Space`키 (키 입력 전용)** — 음성/숫자키 완전 삭제 | **전원**(예외 없이 자기 자신 포함)이 **팀 공용 키워드(TeamCheerWord)** 외치기 (또는 **`T`키**[2026-09-14 재배정, 구 숫자키 `1`→그 전엔 `2`], 기본 비활성) |
| 발동 방식 | **즉시** — 필요 인원 1명(자기 자신)이라 투표 집계 자체가 불필요, 쿨/버프 중이 아니면 Space 입력 순간 발동 | **1회 통과 누적** — 창 안에서 각자 1회 인식되면 그 사람 통과, 전원 통과 시 발동. **10초 표 리셋 없음** (`CheerAndTutorialDesign.md` §2.1) |
| 효과 | 본인이 Q키로 고른 `Shield`/`SpeedUp` (기존 유지) | 그 씬 함정이 한 일을 **되돌림**(`ITeamCheerRevert`) — 힐 아님(§0 오버라이드) |
| 쿨다운 | 개인별 (`cheerCooldownSeconds`) | **없음** — 함정이 창(Warning~되돌림)을 열었을 때만 유효하므로 쿨이 곧 함정 주기 |
| 대상 | 항상 자기 자신 (솔로/멀티 구분 없음 — 구 "솔로만 self 허용" 예외 폐기, 이제 기본 규칙) | `GameSession.ActivePlayerCount` 전원 |

### 1.3 두 개의 독립 시스템 (유지)

```
┌─ [① Dissonance] ─────────────────────────────────────┐
│  팀원 4인 ↔ 자유 대화 (Opus, NGO transport)           │
└──────────────────────────────────────────────────────┘

┌─ [② Vosk + CheerKeywordEngine] ────────────────────┐
│  각 Client: 자기 마이크만 분석                        │
│  → 로컬 grammar = [TeamCheerWord] 1개 [2026-09-14]  │
│  → 팀 응원 창 + 내 미통과일 때만 청취 [2026-09-15]   │
│  → partial 2연속/final 감지 시 SubmitTeamCheerServerRpc │
└──────────────────────────────────────────────────────┘

┌─ [③ Space 키 입력] [2026-09-14 신규] ─────────────────┐
│  개인 버프는 음성이 아니라 키 입력 — Q(버프 전환)/     │
│  Space(발동) → RequestSelfBuffServerRpc류             │
└──────────────────────────────────────────────────────┘

┌─ [④ CheerService (Host)] ────────────────────────────┐
│  개인: 즉시 발동/쿨. 팀: 1회 통과 누적 → 전원 시 되돌림   │
└──────────────────────────────────────────────────────┘
```

**그래머 크기 [2026-09-14 변경]:** 개인 버프가 키 입력으로 바뀌면서 클라이언트 로컬 grammar에서 **내 CheerName이 빠지고 TeamCheerWord 1단어만 남는다.** 구 방식(4명 전부 이름) → 2단어(내 이름+팀워드) → **1단어**로 두 번 축소된 것 — 인식 후보가 줄어들수록 인식률은 계속 좋아진다.

---

## 2. 코어 규칙

### 2.1 개인 버프 (키 입력) **[2026-09-14 트리거 변경]**

| 규칙 | 내용 |
|------|------|
| 수혜자 | 항상 자기 자신 |
| 트리거 | **`Space`키 입력** (음성/숫자키 `1` 삭제) — 로컬에서 곧바로 `RequestSelfBuffServerRpc`류 호출, 인식 지연 없음 |
| 필요 인원 | 1 (자기 자신) — **투표 없음, 입력 즉시 발동** |
| 버프 중 재트리거(Space 재입력) | 무시 |
| 쿨타임 중 재트리거(Space 재입력) | 무시 |
| 사망 | ⚠️ **2026-09-19 변경** — 사망이 더 이상 씬 리로드가 아니다(자동 부활, `ReviveSystemDesign.md`). 리로드는 목숨 0 사망·전원 사망·objective 실패에서만. `StageResetOnPlayerDeath`는 삭제됨. **부활 시 Cheer 상태를 초기화할지 확인 필요** |

**쿨타임:** 버프 종료(`remainingTime == 0`) 순간부터 `cheerCooldownSeconds`(기존 15초 유지) 시작. 트리거 수단만 바뀌었을 뿐 쿨타임 규칙 자체는 변경 없음.

**버프 종류:** 기존 §1.4 버프 선택제 **그대로 유지** — `NetworkPlayerSetup.SelectedBuffType` + **`Q`키 토글**(기존 `RequestToggleBuffTypeServerRpc`) + **`Space`로 발동(신규)**. Shield/SpeedUp 모두 M/T 전 스테이지(Boss 포함)에서 자유 선택 가능. `Ctrl`(색변환)은 이번 변경과 무관 — 별도 3단 순환 리팩터링 없이 기존 2키(Ctrl=흑백토글, Alt=고유색복귀) 구조 그대로 유지, `Alt`/`Space` 둘 다 색변환과 무관해짐(Space는 자유였고, Alt는 원래 계획했던 후보에서 제외).

**[2026-09-14 변경] Q 토글 잠금 해제:** 기존엔 `CheerService.Instance.IsBuffActive(_colorIndex.Value)`가 true면(버프 발동 중) `RequestToggleBuffTypeServerRpc`가 무조건 거부됐다(쿨타임 중에도 마찬가지로 막혔던 건 아니고, "발동 중"만 막던 잠금). **이 잠금을 없앤다** — 버프 발동 중이든 쿨타임 중이든 언제나 `Q`로 `SelectedBuffType`을 바꿀 수 있다. 단, **이미 적용된 효과는 전환의 영향을 받지 않는다**: `ApplyCheerBuff(type)`가 호출된 시점에 그 버프의 duration/value가 이미 스냅샷됐으므로, 발동 중에 `Q`를 눌러 선택을 바꿔도 지금 돌고 있는 버프는 원래 종류·지속시간 그대로 끝까지 간다. 바뀐 선택은 **다음 번 `Space` 발동부터** 적용된다.
예: SpeedUp을 Space로 발동한 직후 Q로 Shield로 전환 → 지금 진행 중인 SpeedUp은 그대로 유지, 다음 Space 발동 시 Shield가 나간다.

### 2.2 팀 버프 (팀 공용 키워드)

| 규칙 | 내용 |
|------|------|
| 수혜자 | 팀 전원 (그 순간 스폰돼 있는 활성 플레이어 전부) |
| 필요 인원 | `GameSession.ActivePlayerCount` (제외 없음 — 자기 자신도 포함해서 셈) |
| 1회 통과 | 창 안에서 TeamCheerWord **1회** 인식 = 그 사람 통과. 같은 창에서 표 유지. 재외침 불필요(중복 Submit은 기존처럼 무시) |
| 타임아웃 | **없음.** `teamCheerTimeoutSeconds` / `CheckTeamTimeout` / 미달 시 표 전부 리셋 **폐기**. 구 규칙(첫 인식 후 10초)은 `CheerAndTutorialDesign.md` §2.1 |
| 효과 | 씬에 등록된 `ITeamCheerRevert` 되돌림 — 입 Open / 침 수면 페이드아웃 / 혀 복구. **Heal 아님**(§0 오버라이드) |
| 유효 구간 | 함정이 창을 연 동안(Warning~되돌림)만. Idle 중 외침은 무시되고 표도 안 쌓임 |
| 창 종료 | 전원이 통과해 되돌림이 성공한 뒤에만 닫힘. **실패로 창이 닫히지 않음.** 다음 사이클 창이 열리면 전원 미통과로 다시 시작. **예외(2026-09-14):** 혀 휩쓸기(`RiseHold` 외 패턴)는 공격이 끝나면, 보스 턱(`MouthBossJawSmash`)은 응원 창 제한 시각이 지나면 창(=팀 응원 배너)이 닫힌다 — 그 뒤 외침은 무시, 원상복구 없음, 표는 창과 함께 리셋 (`CheerAndTutorialDesign.md` §2.1) |
| 인식 조건 | 팀 응원 배너(`TeamCheerWarningUI`, `OnHazardWindowChanged` 구독)가 떠 있는 동안만. Host 판정은 등록된 revert의 `IsAvailable`(창 열림) + 이번 창 미성공(`_teamWindowConsumed == false`) — `ValidateTeamCheer`. **[2026-09-15] 클라이언트도 같은 조건으로 게이팅** — 창 밖이거나 이미 통과했으면 Vosk에 음성을 넣지 않는다(§4.7). Host 검증은 그대로 최종 권한 |
| 쿨다운 | **없음.** 팀 쿨 state·NV·세션 저장 전부 삭제(2026-09-05). 성공으로 창이 닫히면 표도 리셋(다음 창과 섞이지 않게) |
| 발동 피드백 | 전원 화면에 짧게 배너 표시 (`TeamCheerCleared`, §8.2) |
| 솔로(1인) | `ActivePlayerCount==1`이면 자기 혼자 TeamCheerWord 1회로 발동 — 자연스럽게 축소, 별도 예외 코드 불필요 |
| 머리 위 UI | 미통과 = 빨간 느낌표, 통과 = 그 사람 표시 **소거**. 회색/초록 유지 **폐기** (`PlayerCheerHeartsUI`) |

**개인 버프와 팀 버프는 서로 독립.** 같은 순간에 개인 버프 쿨타임 중이어도 팀 버프 투표/발동에는 영향 없음(그 반대도 마찬가지).

### 2.3 삭제된 것 (구 시스템 대비)

- **cross-targeting 전체** — 남의 이름을 외쳐서 그 사람에게 버프를 주는 기능 없음.
- 타겟 전환(`HandleTargetSwitch`), 응원자→타겟 매핑(`_cheererTarget`) — 개념 자체가 없어짐.
- "나를 제외한 전원" 공식(`max(1, ActivePlayerCount-1)`) — 개인 버프는 항상 1(자기 자신), 팀 버프는 `ActivePlayerCount`(제외 없음)로 대체.
- 숫자키 `3`, `4` — 더 이상 컬러 인덱스를 지목할 대상이 없으므로 제거. `1`=자기 응원, `2`=팀 응원만 남음.
- **[2026-09-14 신규 삭제]** 개인 버프의 음성 트리거(자기 CheerName 외치기) 전체, 숫자키 `1`(self) — `Space`키로 대체. `2`(team)는 유지.

---

## 3. CheerName & TeamCheerWord

### 3.1 CheerName (개인 호출명) — **[2026-09-14 표시 전용으로 축소]**

> **역할 변경:** 개인 버프가 `Space`키로 발동되면서 CheerName은 더 이상 음성 인식 대상이 아니다. 이제 남는 역할은 `TeamStatusUI` 코너 패널에 표시되는 **닉네임**뿐이다. Vosk grammar 등록, "말해보기" 개인 버프 테스트, Tutorial 구역3 개인 응원 체험은 전부 제거 대상(§10.5 Phase F).

| PlayerColorType | 기본 CheerName |
|-----------------|----------------|
| Blue | berry |
| Purple | guma |
| Green | sook |
| Yellow | dan |

- **[2026-09-14 최종] 커스텀 입력 없음 — 위 표의 고정값이 곧 이름이다.** 표시처: `TeamStatusUI` 코너 패널, `PlayerHPUI`("YOU · BERRY"), 머리 위 `PlayerNameTagUI`(자기 것은 숨김, 팀 응원 느낌표가 떠 있으면 숨김), `DeathOverlayUI`. 전부 `CheerService.GetCheerName(colorIndex)` 하나로 읽는다.
- ~~Tutorial 씬 자유 입력(`PlayerCheerNameSync`)·형식 검증·TeamCheerWord 충돌 검사~~ — 삭제(이력).
- **[2026-09-14 삭제]** Vosk grammar 등록 대상에서 제외(§3.4), "말해보기"로 자기 CheerName을 발화해 개인 버프를 테스트하는 흐름 삭제.

### 3.2 TeamCheerWord (팀 공용 키워드) **[신규]**

| 항목 | 규칙 |
|---|---|
| 설정 주체 | **Host만.** 팀원 개별 설정 아님 — 팀 전체가 공유하는 단 하나의 값 |
| 기본값 | `"fighting"` (Host가 안 건드리면 이 값 그대로 사용, 기존 4개 CheerName과 발음상 안 겹침) |
| 설정 위치 | Tutorial CheerName 설정 구역(§9.2 zone 2, `CheerAndTutorialDesign.md`)에 Host 전용 입력 필드 추가. 비-Host 클라이언트는 현재 값을 **읽기 전용**으로 표시(뭘 외쳐야 하는지 알아야 하므로) |
| 검증 | `CheerNameValidator`(형식/금칙어) 그대로 재사용 + **[2026-09-15] Vosk 모델 사전 미등재 단어 거절**(`CheerLexiconBuilder.IsKnownWord`, 사유 `"unknown"`, §5.2) |
| 충돌 검사 | **[2026-09-14 삭제]** 개인 CheerName이 고정값·비인식 대상이 되어 겹칠 대상이 없음(§3.3) |
| 구현 | `CheerService`에 `NetworkVariable<FixedString32Bytes> _teamCheerWord`(Server write, Everyone read) + Host-only setter. Host 프로세스는 곧 서버이므로 **RPC 불필요** — Host 클라이언트 UI가 `IsServer` 가드 걸린 public 메서드를 직접 호출. **단, 인스턴스 메서드라 그 씬에 `CheerService`가 실제로 배치돼 있어야 호출 가능** — Tutorial에서 Host가 설정하려면 Tutorial 씬에도 `CheerService`가 필요(§10 Phase D0) |
| 세션 지속 | `GameSession.SetSessionTeamCheerWord`/`GetSessionTeamCheerWord` (기존 `SetSessionCheerNames`와 동일 패턴) — `TutorialNetworkManager`의 게이트 완료 지점(기존 `SetSessionCheerNames` 호출부 2곳)에서 나란히 호출 |

### 3.3 양방향 충돌 검증

> **[2026-09-14 삭제]** 개인 CheerName이 고정값이 되고 음성 인식 대상에서도 빠져, 양방향 충돌 검사는 **코드에서 제거**됐다. `CheerService.TrySetTeamCheerWord`의 실패 사유는 `format`/`reserved`/`blocked`/`unknown`(2026-09-15)/`not_server`뿐(`taken` 없음). 아래는 이력.

### 3.4 Vosk 그래머 슬림화 **[2026-09-14 재축소 — 1단어]**

- **[2026-09-14 변경]** 각 클라이언트 로컬 grammar = **[TeamCheerWord, `[unk]`]** — 딱 1단어. 개인 버프가 키 입력으로 바뀌면서 내 CheerName은 grammar에서 완전히 빠진다.
- 재빌드 트리거: **TeamCheerWord 변경** 시에만 (내 CheerName 변경은 더 이상 재빌드 트리거가 아님 — 음성 인식과 무관해졌으므로).
- `CheerService._teamCheerWord.OnValueChanged` → "현재 팀워드로 로컬 grammar 재적용"이 유일한 재빌드 경로. 호출 헬퍼는 `CheerKeywordEngine.RebuildOwnerLocalGrammar()`(2026-09-14, 구 `PlayerCheerNameSync`에서 이전).
- Tutorial/Interlude "말해보기"는 **TeamCheerWord 테스트만** 남는다 — 개인 CheerName 말해보기는 삭제(§10.5).

> (기존 이력, 참고용) 구 방식(cross-targeting): 4명 전부 이름 → 2026-09-01: [내 이름, TeamCheerWord] 2단어 → 2026-09-14: [TeamCheerWord] 1단어. 인식 후보가 줄어들수록 오인식 확률도 계속 낮아졌다.

---

## 4. 음성 스택 — Dissonance + Vosk (기존 인프라 유지)

인게임 보이스챗(Dissonance)과 키워드 인식(Vosk)의 하드웨어/스레드 공유 구조는 **변경 없음**. 상세는 아래 유지.

### 4.1 인게임 보이스챗 — Dissonance **[Ship Must]**

| 항목 | 선택 |
|------|------|
| 패키지 | Dissonance Voice Chat + Dissonance for Netcode for GameObjects |
| 역할 | 4인 자유 대화 |
| 설정 | Global room, Voice Activation |
| NGO | 게임 상태와 병행. 음성은 Dissonance transport, 규칙은 NGO Host |

### 4.2 키워드 인식 — Vosk **[Ship Must]**

| 항목 | 내용 |
|------|------|
| 종류 | 오픈소스 STT (Apache 2.0) |
| 모드 | **grammar** — TeamCheerWord + `[unk]`만 후보 (§3.4, 2026-09-14 개인 CheerName 제외) |
| 비용 | $0, 클라이언트 로컬 처리, 서버 저장 없음 |

### 4.3 마이크 공유 **[Ship Must · 코드 확정]**

Dissonance와 Vosk가 동일 마이크를 쓰되, OS `Microphone.Start` **이중 오픈 금지**.

| 모드 | 캡처 경로 |
|------|-----------|
| **멀티 (NGO)** | Dissonance만 마이크 소유 → `CheerKeywordEngine`이 `SubscribeToRecordedAudio`로 PCM tap |
| **솔로** | Dissonance가 오디오를 안 줄 때만 `Microphone.Start` fallback |

**과거 사고:** 멀티에서 Dissonance + 직접 `Microphone.Start` 동시 오픈 → 메인 스톨(0.3~0.4s) → NGO 스폰 Deferred/유실. **재발 금지.**

### 4.4 스레드 구조 **[Ship Must · 코드 확정]**

```
[메인]  Dissonance(또는 솔로 마이크) PCM 캡처 → (창 밖이면 버림) → 저역통과+16kHz 리샘플 → float→short → _pcmQueue
[워커]  VoskWorker: AcceptWaveform → Result(final) 또는 PartialResult → (세대 태그) → _resultQueue
[메인]  결과 drain(partial 2연속 또는 final) → TeamCheerWord 매칭 → SubmitTeamCheerServerRpc(isVoice: true)
        (개인 버프는 음성 경로 없음 — Space → RequestSelfBuffServerRpc, §6.1)
```

`AcceptWaveform`은 반드시 백그라운드 워커. 메인에서 돌리면 프레임 히치.

### 4.5 모델 배포·로드 — 비ASCII 경로 크래시 주의 **[코드 확정]**

모델은 zip이 아니라 **압축 해제된 폴더**로 `StreamingAssets`에 포함(`persistentDataPath`로 풀면 Windows 한글 사용자명 경로에서 100% 크래시, libvosk/Kaldi가 `std::ifstream`으로 비ASCII 경로를 못 읽음 — [vosk-api#1072](https://github.com/alphacep/vosk-api/issues/1072)). `VoskModelLoader.GetSharedModel()`의 null 반환은 반드시 존중. 모델 로드 실패는 **음성 인식만 비활성화하고 게임 진행은 막지 않음**.

**로드 시점 [2026-09-15]:** 게임 부팅 직후 `VoskModelLoader.BeginLoad()`(`RuntimeInitializeOnLoadMethod`)가 **백그라운드 스레드**에서 1회 로드한다. `CheerKeywordEngine`은 `IsLoading`이 끝날 때까지 기다린 뒤 `GetSharedModel()`을 읽는다. 구 `LoadSync`(로비 Start에서 호출)는 로비 삭제로 호출부가 사라져, Tutorial 첫 스폰 때 메인 스레드 동기 로드로 멈추던 문제가 있었다 — 메인 스레드 동기 로드로 되돌리지 말 것.

### 4.6 Dissonance 버퍼 경고

`Insufficient buffer space` 류 경고는 **Warn**, 크래시 아님. 1순위 원인은 메인 히치, 2순위는 청크 크기. 재발 시 프로파일 우선.

### 4.7 청취 구간·판정 규칙 **[2026-09-15 · 코드 완료]**

**목표:** 외치는 순간 반응 + 창 밖 잡음으로 인한 오인식 제거.

| 항목 | 규칙 |
|---|---|
| 청취 조건 | `CheerService.IsHazardWindowActive` **그리고** 마지막 `OnTeamVoteChanged` 명단에 내 colorIndex 없음. 둘 중 하나라도 아니면 Vosk에 음성을 넣지 않는다 |
| 창 밖 오디오 | 버린다. 단 Dissonance `base.Update`는 매 프레임 계속 돌려 전달 버퍼를 비운다. **Dissonance 구독은 창마다 끊지 않는다**(§4.3 이중 오픈 사고와 같은 계열 — 구독/해제 반복 금지) |
| 창 열림 순간 | PCM/결과 큐·누적 버퍼·리샘플러·게인 스무딩을 비우고 `_listenGeneration`을 올린다 → 워커가 `VoskRecognizer.Reset()`으로 발화 상태를 비움. 세대가 다른 결과는 메인이 버린다 |
| partial 판정 | 청크 100ms마다 `PartialResult`. TeamCheerWord가 **연속 `PartialConfirmHits`(2)번** 들리면 제출. 한 번 튀었다 정정되는 추측은 여기서 걸러진다 |
| final 판정 | 침묵으로 확정된 결과에 TeamCheerWord가 있으면 즉시 제출(partial을 놓친 경우의 보험) |
| 제출 후 | `_listenGeneration`을 올려 같은 발화로 재제출 방지. Host가 통과 명단을 보내면 청취 중지. 명단이 안 오면(창 열림 시점 경합으로 Host 거절) `SubmitRetrySec`(1.5초) 뒤 다시 제출 가능 |
| 리샘플 | 입력(보통 48kHz) → 63탭 윈도우드 싱크 저역통과(7kHz) → 선형 보간 16kHz. 필터 이력·보간 위상은 청크 사이에 이어간다(경로별 인스턴스) |

> **2026-09-10 "partial 금지"와의 관계:** 당시 오탐은 grammar가 여러 단어이고 게임 내내 청취하던 구조에서 "잠깐 튄 추측"을 그대로 제출해서 났다. 1단어 grammar + 창 게이팅 + 연속 확인으로 원인을 막고 partial을 되살렸다(사용자 결정 2026-09-15). **연속 확인 없이 partial 1회로 제출하는 형태로 되돌리지 말 것.**
>
> **튜닝 포인트:** 오탐이 보이면 `PartialConfirmHits`를 3으로, 반응이 느리면 final 쪽 침묵 대기(`StreamingAssets/vosk-model-*/conf/model.conf`의 `endpoint.rule2/3.min-trailing-silence`)를 실측 후 조정. 둘 다 현재 기본값 유지.

---

## 5. 인식률 개선 파이프라인 (기존 유지)

### 5.1 커스텀 lexicon 주입 — 불가능 (재확인)

`vosk_recognizer_set_grm_with_lexicon`은 [PR #1362](https://github.com/alphacep/vosk-api/pull/1362)로 미병합·정체 상태 — 공식 배포본에 없음. 대신 아래 A+B로 대응.

### 5.2 A. 사전 검증 + B. 발음 변형 대체 단어

```
TeamCheerWord 후보
  → Model.vosk_model_find_word(word) → -1이면 모델 사전에 없음
  → [2026-09-15] TrySetTeamCheerWord가 "unknown"으로 거절 → TutorialCheerNameUI 피드백 표시
     (모델 로드 전/실패면 검사 불가 → 통과)
  → [2026-09-14] 이름은 인식 대상이 아니므로 대체 단어(B) 경로 자체를 삭제
```

**[2026-09-15] 경고 → 거절로 변경:** 사전에 없는 단어는 grammar에 넣어도 Vosk가 무시해 **절대 인식되지 않는다.** 구 설계(Tutorial UI 경고만)는 코드에 연결된 적이 없어, Host가 모르는 단어를 정하면 팀 응원 음성이 조용히 전부 실패했다.

**[2026-09-14]** `VariantMap`/`ResolveVariant` 삭제 — grammar는 TeamCheerWord 1단어라 인식 단어를 이름으로 되돌리는 변환이 남아 있으면 변형 단어가 팀워드와 겹칠 때 매칭이 영원히 실패한다. `BuildGrammarJson`은 전달받은 단어 + `[unk]`만 넣는다.

### 5.3 C. 커스텀 이름 자동 대체 발음 — 설계만 확정, 미착수

Metaphone/Soundex류 발음 근사로 후보 제안하는 방식은 작업량이 커서 아직 미착수. 폴백: 사전 검증(A)만 적용, 없으면 경고만 표시.

---

## 6. 입력 — 개인(Space) · 팀(음성·T) · 이모트(숫자키)

> **[2026-09-14 구조 변경, 같은 날 2차 조정]** 개인 버프·팀 버프·이모트 셋 다 입력 수단이 서로 다르다. 개인 = `Space` 키 입력 전용(음성 없음). 팀 = 기존대로 음성 우선 + `T`키 대체(§6.3, 구 숫자키). 이모트 = 숫자키 `1`~`8` 직접 트리거(§6.4, 구 `T`홀드 마우스 휠).

### 6.1 개인 버프 — 키 입력 **[2026-09-14 신규]**

| 키 | 동작 | 비고 |
|---|---|---|
| `Ctrl` | 색변환(흑백 토글 + 고유색 해제) | 기존 그대로, 이번 변경과 무관 |
| `Alt` | 고유색 복귀 | 기존 그대로, 이번 변경과 무관 |
| `Q` | 버프 종류 전환 (Shield ↔ SpeedUp) | 기존 `RequestToggleBuffTypeServerRpc` 재사용, **[2026-09-14] 발동 중/쿨타임 중에도 항상 전환 가능**하도록 잠금 제거(§2.1) |
| `Space` | 선택된 버프 발동 | **신규.** 로컬 입력 즉시 Host에 요청, 인식 지연 없음 |

```
[Client Owner] Space 입력
  → (Host) 버프/쿨 중 아니면 즉시 개인 버프 발동
```

**[2026-09-14 확정] 입력 소유권:**

| 상황 | Space | 마우스 왼쪽 클릭 |
|---|---|---|
| 평소 | 개인 버프 발동 | 펀치 |
| 대화창(`DialogueUI`, `handleInputLocally=true`) 열림 | 개인 버프 발동(대화와 무관) | **대사 넘기기 전용 — 펀치 안 나감** (`DialogueUI.BlocksPrimaryClick`, 닫힌 프레임 포함). 열린 직후 0.25초는 넘기기도 무시(펀치 연타가 첫 줄을 넘기지 않게) |
| SequenceRing 진행 중(`Playing`) | **링 입력 전용 — 개인 버프 안 나감** (`SequenceRingMinigame.BlocksSelfBuffSpace`, 링이 Space를 소비한 프레임 포함) | 펀치 |
| 채팅·치어네임·ESC 메뉴 열림 | 무시 | 대사 넘기기 무시 |

구 규칙("대화 중 Space = 넘기기, 버프 무시")은 폐기 — 대화 넘기기가 좌클릭으로 옮겨졌다.

### 6.2 팀 버프 — 음성 흐름 (기존 유지)

```
[Client Owner] 마이크 (Dissonance/Vosk 공유)
  → Vosk grammar = [TeamCheerWord] (2026-09-14, §3.4)
  → TeamCheerWord 인식 → SubmitTeamCheerServerRpc(isVoice: true)
```

### 6.3 팀 버프 — `T`키 대체 입력, 기본 비활성 **[2026-09-14 숫자키 → T 재배정]**

> **왜 T인가:** 같은 날 이모트 시스템이 숫자키 `1`~`8`을 직접 트리거로 전부 가져가면서(§6.4), 팀 응원용 숫자키 자리가 없어졌다. 마침 `T`는 구 이모트 휠을 여는 키였는데 그 휠 자체가 폐지되면서(§6.4) 비었으므로, 팀 응원 대체 입력을 `T`로 옮긴다.

| 항목 | 규칙 |
|---|---|
| 매핑 | **[2026-09-14 재배정] `T` = 팀 응원(team)** (구 숫자키 `1`, 그 전엔 `2`). 개인 버프는 `Space`(§6.1), 이모트는 숫자키 `1`~`8`(§6.4) — 이제 세 입력 수단이 서로 겹치지 않는다 |
| 기본 상태 | **비활성(OFF)** — 음성이 기본 응원 수단이므로 |
| 활성화 | 옵션(Options) 메뉴에서 토글 (`GameSettingsManager.DigitCheerEnabled` → 이름은 더 이상 "숫자키"가 아니므로 `KeyCheerEnabled`류로 개명 검토, PlayerPrefs 저장, 마이크 mute 토글과 동일 패턴) |
| 안내 | Tutorial CheerName/응원 체험 구역에서 "팀 응원 인식이 잘 안 되거나 마이크가 없으면 설정에서 T키 응원을 켜세요" 문구 안내 (개인 버프는 이제 항상 Space라 이 안내 대상이 아님) |
| 구현 | `CheerDigitInput`(개명 검토: `CheerKeyInput`) `Update()` 최상단에 `if (GameSettingsManager.Instance?.DigitCheerEnabled != true) return;` 가드는 유지, 감지 키만 숫자 `1` → `Keyboard.current.tKey`로 교체 |
| 서버 검증 | 음성과 동일 RPC 경로(`SubmitTeamCheerServerRpc`, `isVoice=false`)를 그대로 재사용 |

### 6.4 이모트 — 숫자키 `1`~`8` 직접 트리거 **[2026-09-14 전면 개편 — 휠 UI 폐지]**

> 이 절은 Cheer 시스템이 아니라 **별도의 이모트 시스템**(`PlayerEmoteMenuUI.cs`) 관련이지만, 오늘 Cheer 쪽 키 재배치와 같은 세션에서 숫자키 소유권이 함께 정리됐으므로 충돌 방지 기록 차원에서 여기 남긴다.

**변경 전:** `T` 홀드 → 도넛형 이모트 휠 열림 → 마우스로 8조각 중 하나를 겨냥한 채 `T`를 떼서 확정. 각도 판정·호버 하이라이트·휠 패널 UI 필요.

**변경 후:** 휠 UI 완전 폐지. 숫자키를 누르면 그 즉시 해당 이모트 애니메이션 발동 — 마우스 조작·홀드·판정 로직 불필요.

| 키 | 이모트 | 재생 방식 |
|---|---|---|
| `1` | Yes | 루프(Bool `isYes`) |
| `2` | No | 루프(Bool `isNo`) |
| `3` | Thanks | 원샷(Trigger `doThanks`) |
| `4` | Hide | 루프(Bool `isHide`) |
| `5` | Point | 루프(Bool `isPoint`) |
| `6` | Shame | 원샷(Trigger `doShame`) |
| `7` | Fly | 원샷(Trigger `doFly`) |
| `8` | Surprise | 원샷(Trigger `doSurprise`) |

- 매핑은 기존 휠의 12시 오른쪽부터 시계방향 순서(Yes→No→Thanks→Hide→Point→Shame→Fly→Surprise)를 그대로 숫자 순서에 대입한 것 — 순서 변경 없음.
- 루프 이모트(Yes/No/Hide/Point) 중지: **별도 토글 불필요** — 기존처럼 이동 입력이 들어오면 자동 취소(`Player.moveInput` 체크), 또는 다른 이모트 숫자키를 누르면 그걸로 교체. 사용자 확인 완료(2026-09-14): "다른 이모트 들어오면 그게 발동되니 문제없다."
- 원샷 이모트는 `NetworkAnimator.SetTrigger`로 전송(기존 `PlayByIndex`/`PlayOneShotEmote` 로직과 동일 원칙 유지, 트리거 경로만 마우스 판정 대신 숫자키 직접 매핑으로 교체).
- **코드 영향:** `PlayerEmoteMenuUI.cs`의 각도 판정(`ResolveHoveredIndex`)·호버 하이라이트(`ApplyAllSlotVisuals`)·휠 패널 열기/닫기(`OpenMenu`/`CloseMenu`)·`T`키 홀드-릴리스 로직이 전부 불필요해지고, `CheerDigitInput`과 유사한 "숫자키 → 즉시 Animator 파라미터 세팅" 단순 컴포넌트로 대체된다. **씬의 `Emote_Panel`(도넛 UI 프리팹)도 더 이상 쓰이지 않음 — 삭제는 사용자 에디터 작업.**
- Dialogue UI 등 기존 UI-오픈 가드(`InGameChatUI.IsChatOpen`, `TutorialCheerNameUI.IsOpen`)는 그대로 유지 — 그 구간에는 숫자키 이모트 입력도 무시.

---

## 7. 네트워크 권한

### 7.1 아키텍처

```
[각 Client]
  Dissonance: 팀 보이스 송수신
  Vosk: 로컬 키워드(팀워드만, 2026-09-14)
  CheerDigitInput(개명검토 CheerKeyInput): T키 (팀, 설정 ON일 때만. 2026-09-14 재배정, self·숫자키 매핑은 삭제)
  Space: 개인 버프 발동 (2026-09-14 신규, 키 입력 — 음성 아님)
  → RequestSelfBuffServerRpc() / SubmitTeamCheerServerRpc(isVoice)

[Host]
  CheerService:
    개인: sender 색 조회 → 즉시 버프 발동/쿨 체크
    팀: 가상 풀에 1회 통과 누적 → 전원 시 되돌림
    → NetworkVariable / ClientRpc (UI, 버프·팀워드 미러링)
```

- 응원 판정용 음성은 서버로 스트리밍하지 않음. 팀 대화 음성은 Dissonance P2P.
- 게임 규칙(집계·버프·Heal) = **Host**.

### 7.2 RPC 구조 **[2026-09-14 개인 버프 RPC 시그니처 변경]**

| RPC | 방향 | 처리 |
|---|---|---|
| `RequestSelfBuffServerRpc()` (구 `SubmitSelfCheerServerRpc(bool isVoice)`) | Client→Host | **[2026-09-14]** `isVoice` 파라미터 삭제 — 트리거가 항상 Space 키 입력이라 음성 여부 구분이 무의미해짐. 서버가 `PlayerSpawnCoordinator.TryGetColor(senderId)`로 자기 색 조회 → 버프 중/쿨 중 아니면 즉시 `ApplyBuff` |
| `SubmitTeamCheerServerRpc(bool isVoice)` | Client→Host | 변경 없음. 가상 "team" 풀에 표 추가 → `ActivePlayerCount` 충족 시 `ApplyTeamBuff`(= 등록된 `ITeamCheerRevert` 되돌림. **Heal 아님** — §0 오버라이드) |
| `RequestToggleBuffTypeServerRpc` | Owner→Host | Shield/SpeedUp 선택(`Q`키). **[2026-09-14]** `IsBuffActive` 체크로 발동 중 전환을 막던 잠금 제거 — 언제나 `SelectedBuffType`만 바꿈, 이미 적용된 효과엔 영향 없음(§2.1) |
| `ApplyCheerBuffClientRpc` | Host→All | 기존 유지 |
| `BroadcastTeamBuffActivatedClientRpc(int generation, double resumeAt)` | Host→All | 되돌림 명령(세대 + 다음 창 재개 ServerTime) + 배너(`OnTeamBuffActivated`). 팀 쿨 없음 |
| `BroadcastTeamVoteChangedClientRpc` | Host→All | 팀 진행도 UI (누가 이미 외쳤는지) |

**삭제:** `SubmitCheerServerRpc(targetColorIndex, isVoice)`, `HandleTargetSwitch`, `_cheererTarget`, `GetCheererColorIndices`(개인 타겟용) — cross-targeting 제거로 불필요. **[2026-09-14 추가 삭제 대상]** `SubmitSelfCheerServerRpc`의 `isVoice` 파라미터, `CheerKeywordEngine`의 self-cheer 음성 인식 분기, `CheerDigitInput`의 `1`(self) 분기.

### 7.3 Heal 파이프라인 **[현재 팀 응원은 안 씀]**

> **2026-09-03 오버라이드:** 팀 응원 효과가 Heal → `ITeamCheerRevert` 되돌림으로 바뀌면서 **`CheerService`는 `ApplyHeal`을 호출하지 않는다.** 아래 API(`NetworkDamageUtil.ApplyHeal` / `NetworkPlayerSetup.ApplyHealFromServer` / `PlayerEvents.OnHealed`)는 회복이 필요한 다음 기능을 위한 정식 진입점으로 **남겨 둔 것** — 새 회복 경로를 만들지 말고 이걸 쓸 것.

데미지 파이프라인과 동일하게 `NetworkDamageUtil`이 단일 진입점 — 우회 없음.

```csharp
// NetworkDamageUtil — Host 전용 판정, 기존 ApplyDamage/ApplyInstantKill/ApplyKnockback과 동일 패턴
public static void ApplyHeal(Player p, int amount)
{
    if (p == null || amount <= 0) return;
    var nm = NetworkManager.Singleton;
    if (nm == null || !nm.IsListening || !nm.IsServer) return;
    p.GetComponent<NetworkPlayerSetup>()?.ApplyHealFromServer(amount);
}
```

```csharp
// NetworkPlayerSetup — heart 단위, maxHeart로 클램프
public void ApplyHealFromServer(int amount)
{
    if (_player == null || _player.IsDead) return;
    _hp.Value = Mathf.Min(_player.maxHeart, _hp.Value + amount);
}
```

### 7.4 치팅 방어 (Open 수준)

- 개인: 버프 중/쿨 중 재요청 무시. 연타 제한은 쓰지 않음(2026-09-14 — 팀 응원 T키와 버킷을 공유해 Space가 조용히 거절되던 문제 제거, 중복 RPC도 버프 중 체크로 거절).
- 팀: 동일 클라이언트 중복 투표 무시(Set 기반), rate limit(`T`키 입력만, 음성은 제외).
- Host가 모든 최종 판정.

---

## 8. UI

### 8.1 개인 버프 — `CheerProgressUI` (구조 유지, 트리거만 변경)

Idle(선택 아이콘) / BuffActive(fill) / Cooldown(숫자) 3상태 구조는 **변경 없음.** `Q`키(버프 종류 전환)도 기존 그대로. **[2026-09-14]** Idle 상태에서 발동을 기다리는 입력이 음성 인식이 아니라 `Space`키 대기로 바뀜 — UI 상 "지금 눌러야 하는 키" 안내가 있다면 `Space`로 갱신 필요.

### 8.2 팀 버프 UI **[신규]**

| 컴포넌트 | 역할 |
|---|---|
| **`TeamCheerCleared`** (구 이름 `TeamBuffBannerUI`) | 팀 응원 성공 시 화면 가운데 스탬프. `CheerService.OnTeamBuffActivated` 구독. `UI.prefab`에 배치됨 |
| **`TeamCheerWarningUI`** | 팀 응원 가능 구간(Warning~되돌림) 경고. `CheerService.OnHazardWindowChanged` 구독. `UI.prefab`에 배치됨 |
| ~~`TeamBuffCooldownUI`~~ | **삭제됨** — 팀 쿨(120초) 폐기와 함께 스크립트·NV·이벤트 전부 제거(2026-09-05) |
| `PlayerCheerHeartsUI` (역할 재정의) | 구: "나를 응원 중인 남들" → 팀워드 라운드 머리 위 표시. **[2026-09-14]** 창 동안 미통과=빨간 느낌표, 1회 통과=그 사람 표시 소거. 회색/초록 구 **폐기** (`CheerAndTutorialDesign.md` §2.1) |
| `TeamStatusUI` (숫자키 아이콘 자리 교체) | 구: 팀원별 숫자키(1~4) 아이콘 → **신: 팀워드 진행도**(그 팀원이 이미 외쳤는지 체크마크) |
| `PlayerNameTagUI` | 본인 "지금 응원 중인 대상" 표시 제거 (더 이상 타겟 개념 없음) |

### 8.3 TeamCheerWord 설정 UI **[신규]**

Tutorial CheerName 설정 구역(`TutorialCheerNameUI`, `CheerAndTutorialDesign.md` §2 zone 2)에 병합:

- Host: 입력 필드 + 확정 버튼 (개인 CheerName과 같은 패널에 별도 섹션)
- 비-Host: 현재 TeamCheerWord 값을 읽기 전용으로 표시

### 8.4 숫자키 옵션 토글 **[신규]**

`OptionsMenuController`에 마이크 mute 토글과 동일한 방식으로 "숫자키로 응원하기" 체크박스 추가. **[2026-09-14]** 키가 `T`로 재배정돼 라벨은 "T키로 응원하기"가 맞다(코드 설정명 `DigitCheerEnabled`는 유지, 라벨만 에디터에서 변경).

---

## 9. Inspector 파라미터

| 파라미터 | 위치 | 설명 | 값 |
|---|---|---|---|
| `PlayerBuffSystem.buffSettings[type].duration` | `PlayerBuffSystem` | Shield/SpeedUp 지속 | Shield 5초 / SpeedUp **5초·+6**(기본 10→16m/s, 1.6배) — 2026-09-21 변경(구 10초·+15는 버프 해제 시 이질감 + 평균 속도 1.6배로 T.Stage1 볼더 무력화). 버프 필수 구간 T.Stage1 `WallMover_Seq_2`는 이 값에 맞춰 재계산(PlaytestLog #8) |
| `cheerCooldownSeconds` | `CheerService` | 개인 버프 종료 후 쿨 | 15초 (기존 유지) |
| ~~`teamCheerTimeoutSeconds`~~ | — | **삭제됨 (2026-09-14).** 첫 인식 후 10초 표 리셋 폐기. 1회 통과는 창이 끝날 때까지 유지 | — |
| `chatRateLimitSeconds` | `CheerService` | 숫자키 응원 간격 | 0.5~1초 (기존 유지) |
| 함정 `randomIntervalMin/Max` + `warnDuration` | M/T 각 함정 | Idle / 경고 | 스테이지별 인스펙터 (경고 **4초**) |
| ~~`teamCheerCooldownSeconds`~~ / ~~`teamHealAmount`~~ | — | **삭제됨** — 팀 쿨 120초·+2힐 폐기(§0 오버라이드). 창 주기는 함정 인스펙터 | — |

---

## 10. 구현 순서 (Phase A~E)

의존성 순. **아래 단계를 건너뛰면 다음이 막힌다.** 에이전트는 `.cs`만. 씬/프리팹/인스펙터는 사용자.

> **다음 에이전트:** Phase A~D2는 끝났다. 코드 착수점은 없음 — 남은 건 D4(사용자 에디터)와 Phase E. 코어는 **§10.1**, grammar는 **§10.2**, 인게임 UI는 **§10.3**, Tutorial 연동은 **§10.4**.

### 0. 이미 있는 것 (손대지 않음)

- Tutorial CheerName 입력 UI / 표지판
- 개인 버프 UI (`CheerProgressUI`) · Q키 Shield/SpeedUp
- Dissonance + Vosk 마이크/스레드 구조
- `CheerNameValidator` 형식·금칙어

### Phase A — 기반 API + CheerService 코어 **[완료 2026-09-01]**

CheerService가 호출할 것들부터 만든 뒤, 코어를 새 RPC 계약으로 재작성한다.

| # | 작업 | 파일 |
|---|---|---|
| A1 | `ApplyHeal` | `NetworkDamageUtil` |
| A2 | `ApplyHealFromServer` | `NetworkPlayerSetup` |
| A3 | `Set/GetSessionTeamCheerWord` | `GameSession` |
| A4 | `DigitCheerEnabled` (기본 OFF, PlayerPrefs) | `GameSettingsManager` |
| A5 | 구 RPC/상태 삭제: `SubmitCheerServerRpc`, `_cheererTarget`, `HandleTargetSwitch`, 개인 투표 집계 | `CheerService` |
| A6 | `SubmitSelfCheerServerRpc` — 즉시 개인 버프/쿨 | 동일 |
| A7 | `_teamCheerWord` NV + Host-only setter + CheerName 양방향 충돌 | 동일 |
| A8 | 팀 투표·타임아웃·팀 쿨 + `ApplyTeamBuff`(전원 Heal) | 동일 |
| A9 | `BroadcastTeamBuffActivatedClientRpc` / `BroadcastTeamVoteChangedClientRpc` + 이벤트 | 동일 |
| A10 | Inspector: `teamCheerCooldownSeconds`, `teamCheerTimeoutSeconds`, `teamHealAmount` | 동일 (값은 사용자가 나중에) |

같은 계약의 최소 소비자 (미갱신 시 컴파일 불가 / 제출 경로 단절):

- `CheerDigitInput` — `1`=self, `2`=team, `DigitCheerEnabled` 가드
- `CheerKeywordEngine` — 내 이름→Self RPC, 팀워드→Team RPC. grammar는 Phase B에서 [내 이름, TeamCheerWord] 2단어.
- `PlayerCheerNameSync` — TeamCheerWord 충돌 검사
- Heal 시 하트 UI 갱신 — `PlayerEvents.OnHealed` + `PlayerHPUI` / `TeamStatusUI`

### Phase B — 입력·인식 **[완료 2026-09-01]**

| # | 작업 | 파일 |
|---|---|---|
| B1 | 로컬 grammar = [내 이름, TeamCheerWord] 2개. 재빌드 트리거 축소 | `PlayerCheerNameSync` + `CheerKeywordEngine` |
| B2 | Tutorial 말해보기 grammar도 동일하게 2단어 | `CheerKeywordEngine` |
| B3 | Options에 "숫자키로 응원하기" 토글 | `OptionsMenuController` |

### Phase C — 인게임 UI **[완료 2026-09-01]**

| # | 작업 | 파일 |
|---|---|---|
| C1 | 머리 위 표시 = 이번 창 미통과 여부. **[2026-09-14]** 빨간 느낌표 / 통과 시 소거 (구 회색·초록 구는 폐기) | `PlayerCheerHeartsUI` |
| C2 | 죽은 숫자키 아이콘 슬롯 제거(교체 아이콘 없음 — 사용자 결정 2026-09-01, 팀워드 진행도는 C1 머리 위 구로만) | `TeamStatusUI` |
| C3 | "지금 응원 중인 대상" 표시 제거 | `PlayerNameTagUI` |
| C4 | **"Team Buff!"** 배너 (2~3초) | 신규 컴포넌트 (오브젝트 배치는 사용자) |

개인 버프 HUD(`CheerProgressUI`)는 유지.

### Phase D — Tutorial 연동 **[D1·D2 코드 완료 2026-09-01]**

> **D0**: Tutorial 씬에 `CheerService`(NetworkObject) **이미 배치됨** (2026-09-01 확인).
> **D3 제외** (사용자 결정 2026-09-01): "숫자키 켜세요" 안내는 Tutorial 씬에서 1회 설명. 코드 문자열 넣지 않음.

| # | 작업 | 파일 / 담당 | 상태 |
|---|---|---|---|
| D0 | Tutorial 씬에 `CheerService` 배치 | **사용자 에디터** | **완료** (씬에 NetworkObject+CheerService) |
| D1 | Host 전용 TeamCheerWord 입력 + 비-Host 읽기 전용 | `TutorialCheerNameUI` (코드) + 패널 배치는 사용자 | **코드 완료** — GO 연결은 사용자 |
| D2 | 게이트 완료 2곳에서 그 시점 `CheerService.TeamCheerWord` 값을 `SetSessionTeamCheerWord`로 옮김 | `TutorialNetworkManager` | **코드 완료** |
| D3 | ~~안내 문구 코드~~ | — | **제외** — 사용자 씬 텍스트 |
| D4 | 구역 3: 구 cross-target 체험 → 자기 응원 + 팀 응원 | **사용자 에디터** | 미착수 |

### Phase E — 에디터 마감 + 플레이테스트

사용자 에디터:

- ~~Tutorial 씬에 `CheerService` 배치 (D0)~~ **완료**
- `CheerService` Inspector: 개인 쿨 15초 유지. ~~팀 쿨 / 타임아웃 10초 / Heal 2~~ 폐기. **[2026-09-14]** `teamCheerTimeoutSeconds` 필드 제거 예정
- Options 체크박스, Team Buff 배너 연결
- Tutorial CheerName 패널: Host 팀워드 입력 필드·확정 버튼·현재값 텍스트 연결 (D1)
- Tutorial 구역 3 체험 재배치 (D4)
- 숫자키 안내 문구 배치 (D3 대체 — 씬 텍스트 1회)

테스트:

- ParrelSync 2인: Host 팀워드 설정, 각자 1회 통과 누적, 전원 시 되돌림. **10초 표 리셋이 없어야 함**
- Steam 2인·4인은 Tutorial 문서 출시 게이트 — Phase D 이후

---

## 10.5 Phase F — 개인 버프 키 입력 전환 (2026-09-14 설계 확정, **코드 완료 2026-09-14**)

> **왜:** 음성 인식은 지연·오인식이 구조적 한계라 "원하는 타이밍에 정확히 발동"이 필요한 개인 버프와 안 맞았다(§0 오버라이드 참고). 팀 버프(TeamCheerWord)는 타이밍 정밀도가 중요하지 않아 그대로 음성 유지, 개인 버프만 키 입력으로 전환한다.
>
> **추가 확정(2026-09-14, 코드 반영 완료 — 같은 날 재변경):** 대화 넘기기는 Space가 아니라 **마우스 왼쪽 클릭**(대화창이 떠 있는 동안 펀치 안 나감). **SequenceRing 진행 중 Space는 링 입력 전용 — 개인 버프 안 나감.** 대화 중 Space 버프 차단은 폐기. 입력 소유권 표는 §6.1.

| # | 작업 | 파일 | 상태 |
|---|---|---|---|
| F1 | `RequestSelfBuffServerRpc()` — `isVoice` 파라미터 제거, `SubmitSelfCheerServerRpc` 대체 | `CheerService.cs` | **완료** |
| F2 | `Player.cs`의 `GetInput()`에 `Space` 읽기 추가, SequenceRing 진행 중·ESC 등 커서 UI 열림 중엔 무시(위 추가 확정), 그 외엔 F1 호출(서버가 버프/쿨 최종 판정). 대화 넘기기는 좌클릭(`DialogueUI`) + 대화 중 펀치 차단(`PlayerPunch`) | `Player.cs`, `DialogueUI.cs`, `PlayerPunch.cs`, `SequenceRingMinigame.cs` | **완료** |
| F3 | `CheerKeywordEngine`에서 self-cheer 음성 인식 분기 삭제, grammar를 `[TeamCheerWord, [unk]]` 1단어로 축소(§3.4) | `CheerKeywordEngine.cs` | **완료** |
| F4 | `PlayerCheerNameSync`의 grammar 관련 로직에서 CheerName 관여 제거 — CheerName 변경이 더 이상 grammar 재빌드를 트리거하지 않음 | `PlayerCheerNameSync.cs` | **완료** |
| F5 | `CheerDigitInput`에서 self(숫자 `1`) 분기 삭제, 팀 응원 감지 키를 `Keyboard.current.tKey`로 교체(§6.3) | `CheerDigitInput.cs` | **완료**(클래스명 `CheerKeyInput` 개명은 보류) |
| F6 | Tutorial 구역3 "개인 응원 체험" → Space 키 안내로 교체(또는 팀 응원 체험만 남기고 개인은 구역2/HUD 안내로 대체) — 콘텐츠 결정은 `CheerAndTutorialDesign.md` §2 | `CheerAndTutorialDesign.md`, 사용자 에디터 | **미착수** |
| F7 | "말해보기" 테스트 범위를 TeamCheerWord로만 한정 — 개인 CheerName 말해보기 UI 문구/흐름 제거 | `TutorialCheerNameUI.cs` 관련 문구 | **미착수**(코드상 self 테스트 경로는 F3에서 이미 제거됨 — 남은 건 씬 텍스트/흐름) |
| F8 | `RequestToggleBuffTypeServerRpc`의 `IsBuffActive` 잠금 제거 — 발동 중/쿨타임 중에도 항상 `SelectedBuffType` 전환 가능하게. `CheerProgressUI`의 로컬 선(先)차단도 함께 제거 | `NetworkPlayerSetup.cs`, `CheerProgressUI.cs` | **완료** |
| F9 | **[이모트, Cheer와 별개 시스템이지만 같은 세션에 결정됨, §6.4]** `PlayerEmoteMenuUI`를 휠 UI 대신 숫자키 `1`~`8` 직접 트리거 컴포넌트로 재작성. `PlayerEmoteMenuUI.IsOpen`/`ConsumedEscThisFrame` 삭제에 맞춰 `EscMenuController`의 참조도 함께 정리 | `PlayerEmoteMenuUI.cs`, `EscMenuController.cs` | **코드 완료** — 씬의 `Emote_Panel` 삭제는 사용자 에디터 |

**변경 없음(재작성 금지):** `Ctrl`/`Alt` 색변환 로직, TeamCheerWord 음성 경로, `CheerProgressUI`의 3상태 구조.

**남은 사용자 에디터 작업(§10.6 참고):** Inspector 필드 정리, 씬의 `Emote_Panel` 삭제, Tutorial 구역3/말해보기 콘텐츠(F6·F7).

## 10.6 Phase F 사용자 에디터 체크리스트 (2026-09-14)

Phase F 코드 반영 후 씬/프리팹/Inspector에서 사용자가 정리해야 할 것. 에이전트는 `.cs`/Docs만 건드렸고 아래는 전부 미반영 상태.

**삭제**

- [ ] `UI.prefab`의 `Emote_Panel`(도넛 이모트 휠 루트, `Btn.Yes`~`Btn.Surprise` 8슬롯 포함) — `PlayerEmoteMenuUI`가 더 이상 참조하지 않음
- [ ] `PlayerEmoteMenuUI` 컴포넌트의 구 Inspector 필드 값(비워도 무방, 필드 자체는 코드에서 이미 제거됨): `emoteMenuPanel`, `slotImages`, `innerRadius`/`outerRadius`, `slotHighlightColor`, `highlightScale`, `lockCursorOnClose` — 재작성된 스크립트에 해당 필드가 없으므로 재부착 시 자동으로 사라짐. 씬에 남은 컴포넌트가 있다면 Missing Script/필드 경고가 뜨는지 확인
- [ ] Tutorial 구역3의 구 "개인 응원(자기 CheerName 외치기)" 체험 오브젝트/안내판 — cross-target 폐기 이후로도 남아있던 개인 음성 체험이라면 제거 대상(F6)
- [ ] Tutorial "말해보기" UI에서 개인 CheerName 테스트 관련 문구/버튼 — 팀워드 테스트만 남김(F7)

**변경**

- [x] `EmoteHintUI` 삭제 (2026-09-15) — HUD 숫자키 안내는 쓰지 않음. 스크립트 제거됨. `UI.prefab`의 `Emot`에는 `PlayerEmoteMenuUI`만 유지
- [ ] 모든 `Dialogue_Panel`의 `skipHint`(스킵 안내 이미지/문구): "Space" → "좌클릭"으로 교체 (대화 넘기기 입력 변경)
- [x] SequenceRing(M.Stage4) 안내에 "링 진행 중 Space는 버프 대신 링 입력" 설명 추가
- [ ] Player 프리팹 `PlayerCheerHeartsUI.exclamationPrefab`에 `Assets/Prefab/CheerExclamation.prefab` 연결 (구 `sphereMaterial`/색 필드는 삭제됨)
- [ ] Tutorial 팀 응원 안내 문구: "숫자키 응원을 켜세요" → "T키 응원을 켜세요"로 수정(§6.3) — Options 토글 이름 자체(`DigitCheerEnabled`)는 코드상 안 바꿨으니 UI 라벨만 맞추면 됨
- [ ] `CheerProgressUI`/HUD 쪽에 "지금 눌러야 하는 키" 텍스트가 있다면 Space로 갱신(§8.1)
- [ ] Options 메뉴 "숫자키로 응원하기" 체크박스 라벨을 "T키로 응원하기"류로 검토(선택)

**확인(값 변경 불필요, 동작 점검용)**

- [ ] `CheerService` Inspector의 `cheerCooldownSeconds`(15초)·`chatRateLimitSeconds`(0.5~1초). ~~`teamCheerTimeoutSeconds`(10초)~~ **폐기 (2026-09-14)** — ParrelSync로 Space 발동/쿨/Q 전환 + 팀 1회 통과 누적 플레이테스트
- [ ] Player 프리팹에 `Keyboard.current.spaceKey` 입력이 다른 UI(예: 커스텀 InputAction)와 충돌하지 않는지 — 이 프로젝트는 Player.cs가 `Keyboard.current`를 직접 읽는 방식이라 Input Action 에셋 수정은 불필요

---

## 10.1 Phase A 인수인계 (2026-09-01 완료)

다음 에이전트는 **Phase D 코드 완료(§10.4)**. Phase A 코어·RPC·Heal과 Phase B grammar/Options, Phase C 인게임 UI를 다시 짜지 말 것.
에이전트는 `.cs` / Docs만. 씬·프리팹·인스펙터는 사용자.

### 상태

| 항목 | 상태 |
|---|---|
| Phase A (기반 API + CheerService 코어 + 최소 소비자) | **코드 완료** |
| 코드 리뷰 | 완료. 결정 반영됨 (아래 "리뷰 결정") |
| Phase B | **코드 완료** (§10.2) |
| Phase C (인게임 UI) | **코드 완료** (§10.3) |
| Phase D D1·D2 (Tutorial UI + 세션 저장) | **코드 완료** (§10.4) |
| 플레이테스트 | 아직 없음 (Phase E) |

### 한 줄 계약 (Phase A 시점 기록 — **2026-09-14 Phase F로 대체됨, §10.5 참고**)

```
숫자키 1 / 내 CheerName 인식 → SubmitSelfCheerServerRpc → Host 즉시 개인 버프/쿨
숫자키 2 / TeamCheerWord 인식 → SubmitTeamCheerServerRpc → Host 팀 투표·타임아웃·전원 Heal
```

> **현재는 다르다:** 개인 버프는 `Space`키(§6.1), 팀 응원 대체 입력은 `T`키(§6.3), 이모트는 숫자키 `1`~`8`(§6.4), 팀 표는 1회 통과 누적·10초 리셋 없음(§2.2). 이 블록은 Phase A 완료 시점 기록으로만 보존.

구 `SubmitCheerServerRpc(targetColorIndex)` / `_cheererTarget` / `HandleTargetSwitch` **삭제됨. 부활 금지.**

### 파일별 구현 (Phase A가 남긴 실제 API)

> **[2026-09-14]** 이 표의 `PlayerCheerNameSync.cs` 행과 CheerName 우선순위 서술은 이력 — 파일 삭제됨(상단 4차 변경 항목).

| 파일 | 무엇을 넣었나 | 다음 에이전트가 알 것 |
|---|---|---|
| `NetworkDamageUtil.cs` | `ApplyHeal(Player, int)` — Host 전용, 클라이언트 즉시 return. `ApplyDamage`와 동일 가드 | Heal 우회 금지. 이 진입점만 쓸 것 |
| `NetworkPlayerSetup.cs` | `ApplyHealFromServer` — 사망/`_hp<=0`/음수 무시, `maxHeart` 클램프. `OnHpChanged`에서 `next>prev && prev>0`이면 `RaiseHealed` (0→양수는 기존대로 리스폰, Owner 제외) | 풀피면 NV 불변 → `OnHealed` 안 뜸. 정상 |
| `PlayerEvent.cs` | `OnHealed` / `RaiseHealed()` | 기존 `OnDamaged`와 별개. 피격 SFX 경로에 넣지 말 것 |
| `PlayerHPUI.cs` | `OnHealed` 구독 + **리뷰 후** 델리게이트 필드로 `OnDestroy` 해제 (`TeamStatusUI`와 동일 패턴) | 익명 람다 구독으로 되돌리지 말 것 |
| `TeamStatusUI.cs` | 슬롯 `onHealed` 필드 구독/해제 | Heal 구독은 유지. 숫자키 아이콘은 C2에서 팀워드 체크로 교체됨 (§10.3) |
| `GameSession.cs` | `DefaultTeamCheerWord = "fighting"`, `Set/GetSessionTeamCheerWord`, `HasSessionTeamCheerWord`, `ResetSession`에서 null | 게이트 전 `Has==false`, `Get`은 그래도 `"fighting"` 폴백. D2에서 `Set` 호출됨 (§10.4) |
| `GameSettingsManager.cs` | `DigitCheerEnabled` (PlayerPrefs `Settings.DigitCheerEnabled`, 기본 0=OFF), `SetDigitCheerEnabled` | Options 토글은 B3. `ResetToDefaults()`가 `SetDigitCheerEnabled(false)` 호출 |
| `CheerService.cs` | 아래 "CheerService 계약" | Tutorial 씬에 **배치됨** (D0). `_teamCheerWord.OnValueChanged` → `RebuildOwnerLocalGrammar` (B1) |
| `CheerDigitInput.cs` | `DigitCheerEnabled` 가드. `1`=Self RPC, `2`=Team RPC. 3/4 제거 | 매핑 유지 |
| `CheerKeywordEngine.cs` | 인식 후 self→Self RPC, team→Team RPC. `ApplyOwnerLocalGrammar` = [내 이름, TeamCheerWord] | `ApplySessionGrammar` / `WithTeamCheerWord` / 4이름 grammar **삭제됨. 부활 금지.** |
| `PlayerCheerNameSync.cs` | `ConflictsWithTeamCheerWord` — `IsTakenByOther`와 OR. CheerService → 세션 Has → `"fighting"` 순. `EffectiveCheerName`. `RebuildOwnerLocalGrammar`는 Owner 이름만 | Tutorial `CheerService` 배치됨(D0). 인스턴스 없을 때만 `"fighting"` 폴백 |

### CheerService 계약 (재작성 금지, 확장만)

공개/RPC:

- `RequestSelfBuffServerRpc()` (구 `SubmitSelfCheerServerRpc(bool isVoice)`, 2026-09-14) — sender 색 → `ValidateSelfCheer`(버프 중/쿨 중만, 연타 제한 없음) → `ApplyBuff`
- `SubmitTeamCheerServerRpc(bool isVoice)` — `ValidateTeamCheer` → `_teamVotes` HashSet(1회 통과, 타임아웃 없음) → 충족 시 `ApplyTeamBuff`(되돌림 브로드캐스트 → 표 리셋 순서)
- `TrySetTeamCheerWord(string, out reason)` — Host(`IsServer`)만. 실패: `"format"` / `"reserved"` / `"blocked"` / `"unknown"`(모델 사전 미등재, 2026-09-15) / `"not_server"`. ~~`"taken"`~~ 삭제(2026-09-14). RPC 없음
- `TeamCheerWord` 프로퍼티 / `static ResolveTeamCheerWord()` — 팀워드 조회 SSOT(Instance → 세션값 → `"fighting"`, 항상 non-empty). UI·`CheerKeywordEngine`은 이것만 쓴다. ~~`MatchesTeamCheerWord`~~ **삭제 (2026-09-14, 호출자 없음 — 판정은 Owner-side)**
- `_teamCheerWord` NV: Server write, Everyone read, 기본 `"fighting"`
- `OnNetworkSpawn`: Host만 `HasSessionTeamCheerWord`면 세션값을 NV에 복사(구독 **전** — 콜백 안 뜸) → 전원 `_teamCheerWord.OnValueChanged` 구독 → `RebuildOwnerLocalGrammar` + `OnTeamCheerWordChanged` 1회. 스폰 시 초기값은 NGO가 `OnValueChanged`를 띄우지 않으므로 이 수동 호출이 없으면 HUD가 기본값에 고착된다 (2026-09-14 수정)
- `RegisterRevert` / `UnregisterRevert` / `NotifyHazardWindow(bool)` — 씬당 `ITeamCheerRevert` 하나. 중복 등록은 경고 로그
- Inspector: `cheerCooldownSeconds`(15), `chatRateLimitSeconds`(0.5). ~~`teamCheerTimeoutSeconds`(10)~~ **폐기 (2026-09-14)**

Host 내부:

- 개인: `_buffEnd` / `_cooldownEnd` (colorIndex), `_chatRateEnd` (clientId, 숫자키만, 음성은 rate skip)
- 팀: `_teamVotes`, `_teamWindowConsumed`(이번 창 이미 되돌림 — 창이 새로 열릴 때 해제). **팀 쿨 없음. `_teamTimeoutStart` / `CheckTeamTimeout` 폐기 (2026-09-14, 코드 미착수).**
- `GetRequiredTeamVotes()` = `max(1, ActivePlayerCount)` (폴백: `ConnectedClientsIds.Count`)
- `ApplyTeamBuff`: `_revert.BuildRevertOrder`로 세대·재개 시각을 Host가 정하고 `BroadcastTeamBuffActivatedClientRpc`로 전 머신에 그대로 전달 (전원 Heal **아님**)
- `ResetTeamVotes`: Host 전용 + 이미 비어 있으면 no-op + `IsSpawned`일 때만 ClientRpc. **성공으로 창이 닫힐 때**(`NotifyHazardWindow(false)`) 호출해 표가 다음 창으로 넘어가지 않음. 미달 N초 리셋으로는 **호출하지 않음** (2026-09-14)

UI 이벤트:

| 이벤트 | 발행 | 구독 현황 |
|---|---|---|
| `OnBuffActivated` / `OnCooldownStart` | ClientRpc → 로컬 이벤트. 개인 버프 HUD | `CheerProgressUI` (유지, 손대지 않음) |
| `OnTeamBuffActivated` | 되돌림 ClientRpc 직후 | `TeamCheerCleared` (§10.3). **GO 미배치면 배너만 없음** |
| `OnHazardWindowChanged(bool)` | 함정이 `NotifyHazardWindow` 호출 (머신 로컬) | `TeamCheerWarningUI` |
| `OnTeamVoteChanged(current, required, voterColorIndices)` | 표 추가 때 ClientRpc (미달 N초 리셋 **없음**) | `PlayerCheerHeartsUI` (§10.3). `TeamStatusUI` 구독 **금지** |

### 리뷰 결정 (이미 반영)

1. **Tutorial TeamCheerWord 경로** = CheerService를 Tutorial 씬에도 배치 (신규 RPC 안 만듦). **사용자 에디터 D0.** 코드는 배치만 되면 `TrySetTeamCheerWord` + 게이트 `SetSessionTeamCheerWord` + 스테이지 `OnNetworkSpawn` 복원으로 닫힘.
2. **PlayerHPUI 구독 해제** = 지금 고침 (델리게이트 필드). 완료.
3. **죽은 이벤트** = Phase C에서 삭제 완료. `OnVoteChanged` / `OnVoteReset` / `OnCheerersChanged` **부활 금지.**

### 알려진 한계 (버그로 착각하지 말 것)

- Tutorial에 `CheerService` 없음 → Host가 팀워드를 못 바꿈, CheerName 충돌은 `"fighting"`만 검사. **D0 전 정상.** *(D0 완료 — Tutorial 씬에 배치됨)*
- ~~`CheerKeywordEngine.ParseAndSubmit`의 `_lastDetected` 중복~~ — 2026-09-15 판정 로직 교체(`DrainResultQueue`/`TrySubmit`, §4.7)로 해당 코드 없음.
- `CheerKeywordEngine`은 창이 닫혀 있으면 마이크 레벨 로그(`LogMicLevel`, 솔로 경로)도 찍지 않는다. "로그가 안 나온다" = 창 밖이라 청취 중이 아닌 것. `[CheerKeywordEngine] 청취 시작/중지` 로그로 확인.
- `ApplyBuff`/`ApplyTeamBuff`의 `FindObjectsByType<NetworkPlayerSetup>`는 구 패턴 유지. 인원 최대 4. 새로 바꾸지 말 것.
- Options `digitCheerToggle` 미연결이면 숫자키 설정 UI가 안 보임. API·가드는 동작(기본 OFF). **사용자 에디터.**
- `TeamCheerCleared` GO 미배치면 배너만 없음 (되돌림은 적용). `UI.prefab`에는 배치돼 있음.
- `TeamStatusUI`에는 이름/HP 하트 외 아이콘 없음(사용자 결정). 팀워드 진행도가 보고 싶으면 캐릭터 머리 위(`PlayerCheerHeartsUI`)를 본다.
- 팀 정체성 색(`PlayerColorType`/`colorIndex`, Blue/Purple/Green/Yellow)은 스폰 시 1회만 정해지고 게임 중 재변경 경로 없음 — `isBlack`/`isUniqueColor`(흑백↔고유색 토글, `ChangeColorCooldownUI`)와 다른 시스템이니 혼동하지 말 것.

### Phase B 착수점 — **완료.** 다음은 §10.2 / 그 다음 Phase C는 §10.3에서 완료.

Phase B에서 하지 말 것은 유지: CheerService RPC 재작성, Heal 파이프라인, Phase C UI 재정의, Tutorial Host 입력 UI (D1).

### 에이전트 제약

- `.cs` / Docs만 수정. MCP·에디터로 씬/프리팹/인스펙터 쓰지 말 것 (사용자가 "MCP로 수정해줘"라고 하기 전).
- NGO: NV는 Host만 write. Client는 ServerRpc. Heal은 `NetworkDamageUtil`만.
- 오프라인 모드 / 구 `SubmitCheerServerRpc` / cross-targeting 부활 금지.

---

## 10.2 Phase B 인수인계 (2026-09-01 완료)

다음 에이전트는 **Phase D 코드 완료(§10.4)**. grammar 슬림·Options 토글·인게임 UI를 다시 짜지 말 것.
에이전트는 `.cs` / Docs만. 씬·프리팹·인스펙터는 사용자.

### 한 줄

로컬 Vosk grammar = **[내 유효 CheerName, TeamCheerWord]**. 재빌드 = 내 이름 변경 또는 TeamCheerWord NV 변경만. *(2026-09-01 시점 기록 — 현재는 [TeamCheerWord] 1단어, `PlayerCheerNameSync` 삭제됨, §3.4)*

### 파일별

| 파일 | 무엇을 넣었나 | 다음 에이전트가 알 것 |
|---|---|---|
| `CheerKeywordEngine.cs` | `ApplyOwnerLocalGrammar` / `OwnerGrammarWords`. Init도 같은 2단어. `ApplySessionGrammar`·`BuildInGameGrammarJson`·`BuildTutorialTestGrammarJson` 삭제 | 남의 CheerName을 grammar에 넣지 말 것. `_grammarJson`은 재적용 시 같이 갱신됨 (`ResetAudioStream`이 구 grammar로 되돌리지 않게) |
| `PlayerCheerNameSync.cs` | `EffectiveCheerName`. `RebuildOwnerLocalGrammar`는 Owner 엔진만. `OnCheerNameChanged`도 Owner만 grammar 재적용 | 남의 이름 변경은 `OnAnyCheerNameChanged`(이름표)만. CheerService가 팀워드 NV에서 이 헬퍼를 호출 |
| `CheerService.cs` | `_teamCheerWord.OnValueChanged` 전원 구독 → `RebuildOwnerLocalGrammar`. Host 세션 복원은 그대로 | RPC 재작성 금지. 구독 해제는 `OnNetworkDespawn` |
| `OptionsMenuController.cs` | `digitCheerToggle` — `micMuteToggle`과 같은 Toggle 패턴 (`OnEnable` 반영, listener, `RefreshDigitCheerToggle`) | **체크박스 오브젝트 배치는 사용자.** 미연결이면 null no-op |
| `GameSettingsManager.cs` | `ResetToDefaults()` → `SetDigitCheerEnabled(false)` | 기본 OFF 유지 |

### Phase C 착수점 — **완료.** 다음은 §10.3 / Phase D.

Phase C에서 하지 말 것은 유지됐다: CheerService RPC 재작성, grammar 되돌리기, Tutorial Host 입력 UI (D1).

### Phase B 코드 리뷰 (2026-09-01, 반영됨)

런타임 버그 없음. 문서 drift 2건만 고침. 아래는 **버그로 착각해서 지우지 말 것.**

| # | 내용 | 상태 |
|---|---|---|
| 1-1 | `NetworkDesign.md` §6B.7 P6가 구 `ApplySessionGrammar`(전원 이름)를 현재 동작처럼 서술 | **고침** — 2026-08-18 기록은 보존, 옆에 Phase B `ApplyOwnerLocalGrammar` 각주. 현재 SSOT는 이 문서 §10.2 |
| 1-2 | `CheerKeywordEngine` 클래스 doc 초기화 순서가 `BuildDemoGrammarJson` / Dissonance-먼저 | **고침** — 실제 순서: Model → `OwnerGrammarWords` → Dissonance 대기 → Subscribe |
| 2-1 | Host `OnNetworkSpawn`에서 세션 NV write 시 `OnValueChanged` + 명시적 `RebuildOwnerLocalGrammar`가 겹침 | **의도. 지우지 말 것.** 명시적 호출은 세션값 없는 스폰(Tutorial D0 전 등)을 커버. 겹칠 때는 `ApplyOwnerLocalGrammar`가 JSON 같으면 no-op |
| 2-2 | `ResolveOwnerCheerName`의 `PlayerCheerNameSync` 이후 GameSession/기본값 폴백 | **의도. 지우지 말 것.** 프리팹에 Sync가 빠진 경우의 안전망. `GetColorIndex` 3단 폴백과 동일 패턴 |

---

## 10.3 Phase C 인수인계 (2026-09-01 완료)

다음 에이전트는 **Phase D 코드 완료(§10.4)**. 인게임 UI를 다시 짜지 말 것. D4(구역 3)는 **사용자 에디터**.
에이전트는 `.cs` / Docs만. 씬·프리팹·인스펙터는 사용자.

### 한 줄

팀 응원 창 동안 → 미통과만 머리 위 빨간 느낌표, 1회 통과 시 그 사람 표시 소거(코너 패널엔 없음). 전원 통과 → 되돌림 + 배너(`TeamCheerCleared`). 10초 표 리셋 없음 (`CheerAndTutorialDesign.md` §2.1).

### 파일별

| 파일 | 무엇을 넣었나 | 다음 에이전트가 알 것 |
|---|---|---|
| `PlayerCheerHeartsUI.cs` | `OnHazardWindowChanged` → 표시 ON/OFF, `OnTeamVoteChanged` — 자기 colorIndex가 voter에 있으면 **소거**, 없으면 빨간 느낌표 | 구 하트 여러 개 **부활 금지**. 구 회색/초록 유지 **폐기**. 미달 N초로 표를 비우지 않음. 팀워드 진행도를 보여주는 **유일한** UI(코너 패널엔 없음) |
| `TeamStatusUI.cs` | `keyIconSprites`(죽은 숫자키 3/4 아이콘) 삭제, 대체 아이콘 없음. 이름+HP 하트만 | **사용자 결정(2026-09-01): 코너 패널에 팀워드 체크 아이콘 추가하지 않음.** `CheerService.OnTeamVoteChanged` 구독 **부활 금지** — 팀워드 진행도는 오직 `PlayerCheerHeartsUI`(머리 위)로만 표시 |
| `PlayerNameTagUI.cs` | 로컬 오너 "응원 대상" 분기 삭제. 타인 CheerName만 | `hideForLocalOwner` 유지. 타겟 텍스트 슬롯 **부활 금지** |
| `TeamCheerCleared.cs` (구 `TeamBuffBannerUI.cs`) | `OnTeamBuffActivated` → 페이드 배너 (`StageClearBannerUI` 패턴) | `UI.prefab`에 배치 완료 |
| ~~`TeamBuffCooldownUI.cs`~~ | **삭제됨** (2026-09-05) — 팀 쿨 폐기. `OnTeamCooldownClockChanged` / `_teamCooldownEndNv` / `GameSession` 쿨 저장도 함께 제거 | **부활 금지** |
| `CheerService.cs` | `OnVoteChanged` / `OnVoteReset` / `OnCheerersChanged` 삭제 | 구 이벤트 **부활 금지.** RPC 재작성 금지 |

> **2026-09-13 수정:** `PlayerCheerHeartsUI`의 표시 방식을 재정의. World Space 2D 하트 스프라이트(`colorHeartMap`) → 코드 생성 3D 구로 교체. 인스펙터 `sphereMaterial`에 흰색 URP Unlit 머티리얼 1개를 연결하고, 색은 `_BaseColor`만 MaterialPropertyBlock으로 회색=대기/초록=인식으로 바꾼다(빌드에서 `Shader.Find`가 셰이더 스트리핑으로 실패하므로 코드 생성 머티리얼 사용 안 함). 창 열림(피어 로컬)과 표 명단(Host RPC)의 도착 순서가 달라도 마지막 표 상태를 기억해 창 열림 시 그 값으로 칠한다.
> 뜨는 시점도 `OnTeamVoteChanged`(발동/타임아웃)뿐 아니라 `OnHazardWindowChanged(true)`(경고 창 시작)로 확장 — 창이 열리면 전원 회색 구가 먼저 뜨고, 투표가 들어오면 그 사람만 초록, 타임아웃으로 리셋되면(창 유지) 다시 회색, 창이 닫히면 구 자체가 꺼진다.
> 색은 플레이어 고유색이 아니라 회색/초록 2색 고정(전원 동일). "팀워드 진행도를 보여주는 유일한 UI(코너 패널엔 없음)" 원칙은 그대로 유지.
>
> **2026-09-14 재정의:** 위 회색/초록 구·타임아웃 시 다시 회색은 **폐기**. 메시는 3D 느낌표(`CheerExclamation`). 미통과=빨강, 1회 통과=그 사람 표시 소거. 10초 표 리셋 없음. `CheerAndTutorialDesign.md` §2.1.
>
> **같은 날 추가 수정 (2026-09-14, 이후 재번복됨):** `PlayerNameTagUI.cs` 삭제(사용자 결정) — 개인 버프가 자기 자신에게만 적용되는 구조라 캐릭터 머리 위에 남의 CheerName을 띄울 이유가 없다(과거 "서로 응원" 구조의 잔재). 그 정보(게임 닉네임)는 `TeamStatusUI`로 옮겨, 기존 Steam 닉네임 옆에 `"BERRY (영준)"` 형식으로 같이 표시한다(`TeamStatusUI.GetSlotNameLabel`). `PlayerCheerNameSync.OnAnyCheerNameChanged` 구독을 추가해 CheerName이 바뀌면 코너 패널도 즉시 갱신한다. 머리 위에는 이제 `PlayerCheerHeartsUI` 구만 남는다.
>
> **2026-09-14 재도입 [최종]:** 개인 CheerName 커스텀화를 완전히 삭제(§3 재작성 예정 — 이름은 이제 `PlayerColorUtil.DefaultCheerNames`(berry/guma/sook/dan) 고정값)하면서, 흑/백 팔레트로 색을 바꾸면 팀원을 구분할 수 없다는 문제가 다시 불거져 `PlayerNameTagUI.cs`를 **부활**시켰다. 커스텀 이름이 없으니 CheerName 변경 이벤트 구독은 불필요 — `PlayerSpawnCoordinator.OnPlayersReady`만 구독해 색 매핑 준비 시 1회 갱신한다. `PlayerCheerHeartsUI`의 느낌표와 같은 머리 위 자리를 다투므로, `PlayerCheerHeartsUI.IsMarkVisible`을 매 프레임 폴링해 느낌표가 떠 있으면 이름표를 숨기고(대신 느낌표만 보임), 느낌표가 꺼지면(통과했거나 창이 닫히면) 이름표를 다시 보여준다. `hideForLocalOwner=true`는 그대로 유지 — 자기 이름표는 안 보임(`PlayerHPUI`가 "YOU · BERRY" 표시).

### Phase D 착수점 — **D1·D2 완료.** 상세는 §10.4.

Phase D에서 하지 말 것은 유지됐다: CheerService RPC 재작성, grammar 되돌리기, Phase C UI 재정의, 신규 TeamCheerWord 설정 RPC (D0 배치 + D1 직접 호출로 닫힘). D3 안내 문구 코드 **넣지 말 것**.

---

## 10.4 Phase D 인수인계 (2026-09-01 완료 — D1·D2 코드)

다음 에이전트는 **코드 착수점 없음**. 남은 건 D4(구역 3)와 Phase E 에디터. D1·D2를 다시 짜지 말 것.
에이전트는 `.cs` / Docs만. 씬·프리팹·인스펙터는 사용자.

### 한 줄

Tutorial CheerName 패널에서 Host가 TeamCheerWord를 정함(`TrySetTeamCheerWord`, RPC 없음). 게이트 통과 시 Host+Client `GameSession.SetSessionTeamCheerWord`. 다음 스테이지 `CheerService.OnNetworkSpawn`이 그 값을 NV에 복원.

### 파일별

| 파일 | 무엇을 넣었나 | 다음 에이전트가 알 것 |
|---|---|---|
| `TutorialCheerNameUI.cs` | Host 입력 필드+확정 / 비-Host 섹션 숨김 / `currentTeamWordText`. 확정은 `CheerService.TrySetTeamCheerWord` 직접 호출. 성공해도 패널을 닫지 않음 | **인스펙터 연결은 사용자.** 미연결이면 팀워드 UI만 없음. CheerName Enter/Esc/커서 계약 유지. `ConsumedEnterThisFrame`은 팀워드 Enter도 소비 |
| `TutorialNetworkManager.cs` | `CompleteGate`에서 CheerName 직후 `SetSessionTeamCheerWord` + `BroadcastSessionTeamCheerWordClientRpc` | CheerName과 같은 2곳(Host 로컬 + ClientRpc). 신규 설정 RPC 만들지 말 것. `CheerService` 없으면 `"fighting"` 폴백으로라도 `Set`해서 `HasSession`이 true가 됨 |

### 사용자 에디터 잔여

- D1 패널: `hostTeamWordSection` / `teamWordInputField` / `teamWordConfirmButton` / `currentTeamWordText` (`clientTeamWordSection`은 선택). **`currentTeamWordText`는 Host/Client 공통으로 보이게 두 섹션 바깥에**
- D3: Tutorial 씬 텍스트로 숫자키 안내 (코드 없음)
- D4: 구역 3 자기 응원 + 팀 응원 체험
- Options `digitCheerToggle` (Phase B/C 잔여). 배너·경고 GO는 `UI.prefab`에 배치 완료

### 결정

- **D3 제외** (사용자 2026-09-01): 숫자키 안내는 Tutorial 씬에서 1회 설명. `TutorialCheerNameUI`에 안내 문자열 넣지 말 것.
- **D0 완료**: Tutorial 씬에 `CheerService`+`NetworkObject` 이미 있음.

---

## 11. 구현 체크리스트

> **[2026-09-14]** 아래 `PlayerCheerNameSync` 관련 항목(완료·미완료 모두)은 파일 삭제로 종결 — 미완료 `grammar 관련 CheerName 관여 제거`도 삭제로 해소됨.

### **[Ship Must]**

**Phase A**

- [x] `NetworkDamageUtil.ApplyHeal` 신규
- [x] `NetworkPlayerSetup.ApplyHealFromServer` 신규
- [x] `GameSession` — `SetSessionTeamCheerWord`/`GetSessionTeamCheerWord` 추가
- [x] `GameSettingsManager` — `DigitCheerEnabled` 설정(PlayerPrefs, 기본 OFF) 추가
- [x] `CheerService` 재작성 — `SubmitSelfCheerServerRpc`/`SubmitTeamCheerServerRpc`, `_cheererTarget`/`HandleTargetSwitch` 제거
- [x] `CheerService` — `_teamCheerWord` NetworkVariable + Host-only setter + 양방향 충돌 검증
- [x] `CheerService` — 팀 투표/타임아웃/쿨다운 state + `ApplyTeamBuff`(전원 Heal) + `OnTeamBuffActivated` 이벤트
- [x] `CheerDigitInput` / `CheerKeywordEngine` / `PlayerCheerNameSync` — 새 RPC·충돌 검사 최소 연결
- [x] `PlayerEvents.OnHealed` + `PlayerHPUI` / `TeamStatusUI` Heal UI 갱신
- [x] `PlayerHPUI` PlayerEvents 구독 해제 (리뷰 후, 델리게이트 필드 패턴)
- [x] 코드 리뷰 + 인수인계 기록 (`CheerSystemDesign.md` §10.1)

**Phase B**

- [x] `CheerKeywordEngine` — grammar를 [내 이름, TeamCheerWord] 2개로 슬림화, 재빌드 트리거 정리
- [x] `PlayerCheerNameSync` — `RebuildOwnerLocalGrammar` 슬림화
- [x] `OptionsMenuController` — "숫자키로 응원하기" 토글 UI 추가
- [x] `CheerService` — `_teamCheerWord.OnValueChanged` → 로컬 grammar 재적용
- [x] `GameSettingsManager.ResetToDefaults` — `DigitCheerEnabled` OFF
- [x] 인수인계 기록 (`CheerSystemDesign.md` §10.2)

**Phase C**

- [x] `PlayerCheerHeartsUI` — "팀워드 이미 외쳤는지" 표시로 재정의
- [x] `TeamStatusUI` — 죽은 숫자키 아이콘 삭제(대체 아이콘 없음, 이름+HP 하트만)
- [x] `PlayerNameTagUI` — 본인 "응원 대상 표시" 제거
- [x] Team Buff! 배너 UI 신규 컴포넌트
- [x] `CheerService` 구 투표 이벤트 (`OnVoteChanged` / `OnVoteReset` / `OnCheerersChanged`) 삭제
- [x] 인수인계 기록 (`CheerSystemDesign.md` §10.3)

**Phase D**

- [x] Tutorial 씬에 `CheerService` 배치 (D0, 사용자 에디터 — 2026-09-01 씬 확인)
- [x] `TutorialNetworkManager` — 게이트 완료 지점 2곳(기존 `SetSessionCheerNames` 호출부)에 TeamCheerWord 세션 저장 추가
- [x] TeamCheerWord 설정 UI — `TutorialCheerNameUI` Host 전용 필드 (에디터 배치는 사용자 작업)
- [x] ~~Tutorial 안내 문구 코드~~ — **제외** (사용자 씬 텍스트, 2026-09-01)
- [ ] 구역 3 재설계 — 구 cross-target 체험 → 자기 응원 + 팀 응원 (에디터)
- [x] 인수인계 기록 (`CheerSystemDesign.md` §10.4)

**Phase F (2026-09-14 신규 — 개인 버프 키 입력 전환)**

- [ ] `CheerService.RequestSelfBuffServerRpc()` — `isVoice` 제거, 기존 `SubmitSelfCheerServerRpc` 대체
- [ ] `Player.cs` — `Space` 입력 읽기 + 버프 발동 요청, Dialogue UI 열림 가드
- [ ] `CheerKeywordEngine` — self-cheer 음성 분기 삭제, grammar `[TeamCheerWord]` 1단어로 축소
- [ ] `PlayerCheerNameSync` — grammar 관련 CheerName 관여 제거
- [ ] `CheerDigitInput` — self 분기 삭제, 팀 응원 감지 키를 `T`로 교체(숫자키 아님)
- [ ] Tutorial 구역3 콘텐츠 교체 (에디터)
- [ ] "말해보기" 문구/흐름을 TeamCheerWord 전용으로 축소
- [ ] `NetworkPlayerSetup.RequestToggleBuffTypeServerRpc` — `IsBuffActive` 잠금 제거(발동/쿨타임 중에도 Q 전환 허용)
- [ ] `PlayerEmoteMenuUI` — 휠 UI 폐지, 숫자키 `1`~`8` 직접 트리거로 재작성 (§6.4, Cheer와 별개 시스템)
- [ ] 인수인계 기록 (`CheerSystemDesign.md` §10.5)

---

## 12. 관련 코드

| 항목 | 경로 |
|------|------|
| 응원 코어 | `Assets/Scripts/Cheer/CheerService.cs` |
| 키워드 인식 | `Assets/Scripts/Cheer/CheerKeywordEngine.cs` |
| Grammar 빌더 | `Assets/Scripts/Cheer/CheerLexiconBuilder.cs` |
| 이름 검증 | `Assets/Scripts/Cheer/CheerNameValidator.cs` |
| 머리 위 이름표 (고정 이름) | `Assets/Scripts/UI/PlayerNameTagUI.cs` |
| 숫자키 입력 | `Assets/Scripts/Cheer/CheerDigitInput.cs` |
| 데미지/Heal 유틸 | `Assets/Scripts/Network/NetworkDamageUtil.cs` |
| 네트워크 플레이어 | `Assets/Scripts/Network/NetworkPlayerSetup.cs` |
| 플레이어 이벤트 | `Assets/Scripts/PlayerEvent.cs` (`OnHealed`) |
| 버프 | `Assets/Scripts/PlayerBuffSystem.cs` |
| 세션 | `Assets/Scripts/GameSession.cs` |
| 설정 | `Assets/Scripts/Settings/GameSettingsManager.cs` |
| 옵션 UI | `Assets/Scripts/UI/OptionsMenuController.cs` |
| 개인 버프 UI | `Assets/Scripts/UI/CheerProgressUI.cs` |
| 로컬 HP UI | `Assets/Scripts/UI/PlayerHPUI.cs` |
| 팀 UI | `Assets/Scripts/UI/TeamStatusUI.cs` |
| 진행도 UI | `Assets/Scripts/UI/PlayerCheerHeartsUI.cs` |
| 팀 응원 성공 배너 | `Assets/Scripts/UI/TeamCheerCleared.cs` |
| 팀 응원 경고 | `Assets/Scripts/UI/TeamCheerWarningUI.cs` |
| 되돌림 계약 | `Assets/Scripts/Cheer/ITeamCheerRevert.cs` |
| 되돌림 대상 | `Assets/Scripts/MouthController.cs` · `Assets/Scripts/Cheer/SalivaHazard.cs` · `Assets/Scripts/Cheer/TongueController.cs` |
| Tutorial 이름 설정 UI | `Assets/Scripts/UI/TutorialCheerNameUI.cs` |
| Tutorial 네트워크 | `Assets/Scripts/Network/TutorialNetworkManager.cs` |
| 이모트 (별도 시스템, §6.4 참고) | `Assets/Scripts/UI/PlayerEmoteMenuUI.cs` |

---

## 13. FAQ

**Q. 팀원을 지목해서 응원할 수 있나?**
A. **아니오.** cross-targeting은 완전히 삭제됐다. 항상 자기 자신(개인 버프) 또는 팀 전체(팀 버프)만 대상이다.

**Q. 개인 버프는 왜 투표가 없나?**
A. 필요 인원이 항상 1명(자기 자신)이라 "투표를 모은다"는 개념 자체가 무의미하다 — 인식되는 순간 바로 발동.

**Q. TeamCheerWord는 누가 정하나?**
A. **Host만.** 팀원 각자가 정하는 개인 CheerName과 다르다. 기본값은 `"fighting"`.

**Q. TeamCheerWord와 CheerName이 겹치면?**
A. 어느 쪽이든 나중에 확정하려는 값이 거절된다(§3.3, 양방향 검사).

**Q. 팀 버프 효과는 왜 Heal인가, 왜 즉발인가?**
A. 사용자 결정 — 전체 체력회복 +2, 지속시간 있는 버프(무적/스피드업 등)는 이번 범위에서 드랍.

**Q. 숫자키는 기본으로 켜져 있나?**
A. **아니오.** 팀 응원(`T`키, 2026-09-14 재배정)은 음성이 기본이라 기본 꺼짐, 옵션에서 켜야 동작. **개인 버프는 숫자키가 아니라 항상 `Space`.** 숫자키 `1`~`8`은 이제 이모트 전용(§6.4)이라 Cheer 시스템과는 무관.

**Q. 그래머 단어 수가 줄어드는 게 맞나?**
A. 맞다. 구 방식(cross-targeting, 4명 전부) → 2026-09-01(내 이름+팀워드 2개) → **2026-09-14(팀워드 1개)** 순으로 계속 줄었다. 개인 버프가 키 입력으로 완전히 바뀌면서 내 CheerName도 grammar에서 빠졌기 때문 — 인식 후보가 줄어들수록 인식률은 계속 좋아진다.

**Q. 개인 버프는 왜 음성에서 키 입력으로 바꿨나?**
A. 음성 인식은 지연·오인식이 구조적 한계라 "원하는 타이밍에 정확히 발동"이 필요한 개인 버프와 안 맞았다(2026-09-14 결정). 팀 버프는 타이밍 정밀도가 중요하지 않아 그대로 음성 유지.

**Q. CheerName은 이제 왜 필요한가?**
A. `TeamStatusUI`에 표시되는 닉네임 용도로만 남는다. 음성 인식·버프 발동과는 더 이상 관련 없다.

**Q. 버프 발동 중에 Q로 종류를 바꾸면 지금 버프가 취소되나?**
A. **아니오.** 지금 진행 중인 효과는 발동 시점에 이미 확정된 값(`ApplyCheerBuff`)이라 그대로 끝까지 간다. Q로 바꾼 선택은 다음 `Space` 발동부터 적용된다(2026-09-14, §2.1).

**Q. 팀 응원 대체 입력은 왜 T키인가?**
A. 2026-09-14 안에서만 두 번 바뀌었다: 개인 self 숫자키 삭제로 잠깐 `1`이 됐다가, 같은 날 이모트 시스템이 숫자키 `1`~`8`을 전부 직접 트리거로 가져가면서(§6.4) 팀 응원이 마침 폐지된 이모트 휠의 `T`키로 다시 옮겨갔다.

**Q. 팀 응원은 10초 안에 전원이 외쳐야 하나?**
A. **아니오 (2026-09-14).** 창이 열린 동안 한 번 인식되면 그 사람은 통과고, 다시 외칠 필요 없다. 성공은 여전히 전원 각자 1회. 구 10초 표 리셋은 폐기. `CheerAndTutorialDesign.md` §2.1.

**Q. 이모트는 왜 휠 UI가 없어졌나?**
A. 마우스로 8조각 중 하나를 겨냥하다 놓치는 경우가 있어, 숫자키 `1`~`8` 직접 트리거로 단순화했다(2026-09-14, §6.4). 매핑 순서는 기존 휠 순서(Yes→No→Thanks→Hide→Point→Shame→Fly→Surprise) 그대로.

---

## 14. 팀 응원 소리 매칭 — TeamCheerSound **[2026-10-07 설계 초안 · 코드 미착수]**

> **한 줄:** TeamCheerWord(영어 단어, Vosk)를 **Host가 직접 녹음한 소리 1개**로 바꾼다. 언어·단어 제한 없음("우와와와아아아", "헬로우~", 중국어·일본어 전부 가능). 규칙(§2.2 전원 각자 1회, 창 게이팅 §4.7, T키 대체 §6.3)은 **그대로** — 바뀌는 건 "무엇을 어떻게 알아듣느냐"뿐.
>
> **배경:** 출시 첫 달 구매자가 대부분 중국 플레이어였고 영어 단어 외치기가 장벽으로 판단됨(실구매 ~7 중 환불 5, 접근성 사유 1건). Vosk 영어 모델(205MB)은 영어 단어를 받아 적는 도구라 이 방향에 맞지 않아 **삭제**한다.
>
> **핵심 결정 (2026-10-07 사용자):** ① 목록에서 고르는 방식 아님 — Host가 자유 녹음. ② **각 플레이어가 자기 목소리로 1회 등록**하고, 인게임 판정은 자기 등록본 기준(같은 사람·같은 마이크 → 정확도 확보). ③ 단, 등록본은 **Host 소리와 같은 틀**이어야 한다 — Host가 "우와와와아아아"면 "우가우가"로 등록하면 거절. ④ 오인식(특히 외쳤는데 못 알아듣는 쪽) 최소화가 조건.

### 14.1 용어

| 용어 | 뜻 |
|---|---|
| **기준 소리** (Host clip) | Host가 녹음한 원본 PCM. 전원에게 배포돼 "뭘 외칠지" 들려주는 용도 + 등록 틀 검사의 기준 |
| **등록본** (my template) | 각 플레이어가 자기 PC에서 자기 목소리로 녹음한 것의 특징 묶음. **로컬에만 있음, 네트워크로 안 보냄** |
| **틀 검사** (shape check) | 등록본이 기준 소리와 같은 소리인지 — 길이·끊김 횟수·높낮이 곡선·음색을 **사람이 달라도 통과하도록 느슨하게** 비교 |
| **판정** (detect) | 인게임 창 동안 내 마이크가 내 등록본과 맞는지 — **같은 사람 기준이라 빡빡하게** 비교 |

### 14.2 흐름 — Tutorial / Interlude **[2026-10-07 3차 개정 — 패널에서 전부, 코드 완료]**

> **등록본 규칙 (2026-10-07 확정, 사용자):** **게임에서 쓰는 등록본은 전부 Host 틀 검사(§14.4)를 통과한 것뿐이다.** 플레이어에게는 한 줄 — "Host 소리를 듣고 똑같이 두 번 녹음(나 1·나 2) → 연습 표지판에서 외쳐 통과 → 게임에서도 그 소리". 실시간 판정(§14.6)은 Host를 직접 보지 않지만, Host 기준을 통과한 등록본 안에서만 돈다.
>
> **[3차 개정 — 사용자 결정]** 나 2는 연습 창에서 뽑지 않고 **같은 패널에서 두 번째 녹음**으로 받는다(= "두 번 인식해 달라"). 구역 3 연습 표지판은 등록본으로 **실시간 판정만** 한다.
>
> **[4차 개정 2026-10-07 — 기준은 Host 하나, 사용자 결정]** ① 1번·2번 **모두 Host 틀 검사만**(1↔2 비교 삭제 — 같은 사람이 똑같이 해도 거리 7~9라 2번이 거의 안 넘어갔다). ② 게임 중 판정은 1·2번 중 가까운 쪽(2번이 있는 이유 = 인식률). ③ **R키 = 내 1번 녹음**(판정 기준과 같은 소리), 등록 못 한 사람은 Host 소리. ④ 녹음 **1~3초 강제**(1초 전 정지 불가, 3초 자동 정지), **소리 0.5초 이상**(안내 문구에 명시). ⑤ 끊김 횟수는 최대 △. ⑥ 마이크는 Dissonance **가공 전 원본**(`DissonanceComms.MicrophoneCapture.Subscribe`)으로 받는다. ⑦ 마이크 없이 T키로 시작해 도중에 마이크를 꽂은 사람은 다음 등록 기회(Interlude)까지 T키만 — Host 소리 직접 비교로 따로 판정하지 않는다(기준 하나 유지).
>
> | 단계 | 어디서 | 하는 일 | 통과 조건 | 거절되면 |
> |---|---|---|---|---|
> | ⓪ Host 녹음 | 구역 2 패널 (Host만) | [녹음/정지] → 자동 재생 → [확정] | 녹음 자체 검사(§14.4 #0) | 이유 표시, 다시 녹음 |
> | ① 나 1 | 구역 2 패널 (전원) | [호스트 소리 듣기] → [나 1 녹음/정지] | Host 틀 검사 통과 | 이유 표시, **될 때까지 다시** |
> | ② 나 2 | 구역 2 패널 (전원) | [2번 녹음/정지] | **Host 틀 검사만**(4차 개정 — 1번과 비교 삭제) | 이유 표시("나 1과 다르게 들려요" / 틀 검사 이유), 다시 |
> | ③ 연습 (필수 관문) | 구역 3 표지판 | 진짜 함정 창에서 외침 | 나 1·나 2 중 하나라도 실시간 기준(전원 공통 고정 §14.6) 통과 | 창 유지, 다시 외침. **7회 연속 실패 → 그 세션 T키 자동 ON** |
> | ④ 게임 중 | 함정 창 | 외침 | ③과 같음 | 계속 들음, 창 안 3회 연속 실패 시 T키 힌트(설정 위치 명시) |
>
> - Host 자신의 나 1 = 확정한 기준 소리(별도 녹음 없음). Host도 나 2는 녹음한다.
> - 나 1을 다시 녹음하면 나 2는 무효(옛 나 1 기준). Host가 다시 녹음하면(버전 +1) 전원 나 1·나 2 무효 + 연습 기록 초기화.
> - Lab(`Tools/Cheer Sound Lab`)도 같은 규칙으로 돈다 — 거절된 녹음은 실시간 판정에 쓰지 않는다.

```
[구역 2 — 패널 (TutorialCheerNameUI, 한 패널에서 전부)]
  Host:   [녹음] 누름 → 바로 녹음 → 같은 버튼([정지]) → 정지(4초 상한) → 발화 구간 자동 재생 → [확정]
          확정 → CheerService.TrySetTeamCheerSound → 버전 +1 → 전원 배포(§14.5) + Host 나 1 = 이 녹음
  전원:   [호스트 소리 듣기] → [나 1 녹음] → 틀 검사(§14.4) → 저장 / 거절 힌트
          [나 2 녹음] → 나 1 기준 실시간 판정(CheerSoundMatcher.OfflineCheck) + 틀 검사 → 저장 / 거절 힌트
          상태 줄: "① 호스트 소리를 듣고 똑같이 '나 1'을 녹음하세요" → "② 한 번 더 똑같이 '나 2'를…" → "등록 완료 ✓"
          녹음은 CheerKeywordEngine.Local.BeginCapture/EndCapture — 마이크를 따로 열지 않음(§4.3)

[구역 3 — 연습 표지판 (TutorialTeamCheerTestSignboard) — 필수 관문, 경험자도 생략 불가]
  E → Host RequestStartRpc → CheerService.MarkNextWindowAsPractice() → 전원 mouth0 창 열림(_practiceWindow NV = true)
  각자 외침 → 실시간 판정(§14.6) → SubmitTeamCheerServerRpc(isVoice:true) → Host가 _practicePassed에 기록 → _practicePassedCount NV
  등록본이 없어 T키 자동 ON인 사람은 T키로 통과해도 연습 통과로 친다
  등록본은 있는데 7회 연속 실패(CheerSoundLocalState.PracticeFailStreak) → 그 세션 T키 자동 ON + 힌트

[게이트 (TutorialNetworkManager.UpdateGate — 매 프레임 EvaluateSoundGate)]
  Host 기준 소리 없음(HasTeamCheerSound=false)   → _gateBlock = HostSoundMissing   → 간판: "호스트가 팀 구호를 녹음해야 시작할 수 있어요"
  접속자 중 연습 미통과자 있음(AllPracticePassed) → _gateBlock = PracticeIncomplete → 간판: "전원이 팀 구호 연습을 통과해야… (N/M)"
  둘 다 아님 + 전원 존 안 → 카운트다운. 세션 스냅샷 없음 — 소리는 CheerSoundLocalState(static)가 씬을 넘어 유지
  등록본 없는 팀원은 게이트를 막지 않음 → 첫 인게임 창에서 T키 자동 ON(§14.8)

[Interlude — 2차 변경]
  패널·표지판 그대로. Host가 다시 녹음하면 버전 +1 → 전원 등록본 무효(재등록) + 연습 기록 초기화.
  연습 기록은 씬 단위(CheerService 인스턴스) — Interlude 표지판 1회 통과는 재녹음 여부와 무관하게 항상 필요(사용자 확인 — Tutorial과 같은 규칙).
```

### 14.3 특징 추출 — 등록본·판정 공통

입력은 기존 경로 그대로: Dissonance 탭(멀티)/솔로 마이크 → 저역통과 → **16kHz mono float** (§4.4·§4.7의 리샘플러 재사용. **마이크 이중 오픈 금지 §4.3 유지** — 녹음도 `CheerKeywordEngine`의 같은 PCM 스트림에서 "캡처 모드"로 받는다).

| 단계 | 내용 |
|---|---|
| 전처리 | DC 제거 → 앞뒤 침묵 자르기(프레임 RMS가 소음 바닥 +6dB 미만, 30ms 미만 튐 무시) → 프레임마다 프리엠퍼시스 0.97. 소음 바닥 = min(하위 10% 프레임, 피크 −25dB) |
| 프레임 | 25ms 창 / 10ms 간격, Hamming, FFT 512 |
| **음색** MFCC | 멜 필터 26개(0~8kHz) → log → DCT → 13계수(c0 제외 — 음량 무관) + Δ 13 = **26차원/프레임**. **정규화 없음** — 아래 [정규화 결정] |
| **리듬** | 프레임 에너지(dB) 곡선 → 봉우리 수 = **끊김 횟수(burstCount)**(봉우리 간격 최소 120ms, 골이 봉우리보다 8dB 이상 낮아야 별개) + **길이(durationMs)** |
| **높낮이** | 정규화 자기상관(YIN 간이형) 60~800Hz(여성·아이 고음 "미야옹"), 유성 프레임만 → 반음 단위 → 중앙값 빼기 → 시간축 50점으로 리샘플 = **pitchContour[50]** + 유성 비율 |
| 등록본 | `{ mfcc[frames][26], burstCount, durationMs, pitchContour[50], voicedRatio, hostClipVersion }` |

MFCC/DTW/자기상관은 전부 C#으로 직접 구현(외부 패키지 없음, FFT 포함 300~500줄). 전부 **워커 스레드**(기존 VoskWorker 자리). 이 산출물은 결정론적 신호처리라 Steam AI 표기 대상이 아니다.

> **[정규화 결정 — 2026-10-07 합성 시험]** 초안의 발화 단위 CMVN은 **길게 끄는 모음의 음색을 지운다** — 정적인 소리는 평균을 빼면 거의 0이 되고, 분산 나누기는 작은 흔들림을 키운다. 합성 시험에서 "오오오" 등록본 기준 내 소리 d 5.2 vs 소음 5.9로 구분이 안 됐다. 평균만 빼도(CMN) "오오오"↔"에에에"가 1.4~2.0으로 붙었다. 판정은 **같은 사람·같은 마이크** 비교라 채널 보정이 필요 없어 정규화를 뺐다 → 소음 거리 12~15로 벌어짐. 다른 사람끼리 비교하는 틀 검사 음색은 원래 느슨한 상한이라 영향 적음.

### 14.4 틀 검사 — 등록본 vs 기준 소리 (사람이 달라도 통과)

목적은 "같은 소리인가"지 "같은 목소리인가"가 아니다. 그래서 **사람 차이에 무딘 특징(길이·끊김·상대 높낮이)**을 주로 보고 음색은 느슨하게 본다.

**[2026-10-07 Lab 실측 후 개정] 3단계 판정.** 초안(한 항목이라도 넘으면 거절)은 실제 목소리에서 같은 소리를 살짝 다르게 따라 해도 자주 거절됐다. 항목마다 **통과 ✓ / 애매 △ / 확실히 다름 ✗**으로 나누고 **✗ 1개 또는 △ 3개 이상이면 거절**한다(△ 2개까지 통과 — 2026-10-07 Lab 3차 후 사용자 결정; 애매 2개로 거절된 건 전부 같은 소리였고 다른 소리는 전부 음색 ✗로 걸렸다). 살짝 다른 따라하기는 보통 한 항목만 경계에 걸리고, 다른 소리는 여러 항목이 같이 틀리거나 한 항목이 크게 틀린다. 거절 이유는 ✗ 항목(없으면 첫 △ 항목).

| 순서 | 항목 | ✓ 통과 | △ 애매 | ✗ 확실히 다름 | 거절 힌트 |
|---|---|---|---|---|---|
| 0 | 녹음 자체 | 피크 ≥ −45dBFS, SNR ≥ 12dB, 0.3~3초, 찢어짐 ≤1% | — | 하나라도 아니면 바로 거절 | 너무 작아요 / 짧아요 / 길어요 / 커요 |
| 1 | 길이(기준 대비) | 0.6~1.7배 | 0.5~0.6 / 1.7~2.0배 | 그 밖 | 너무 짧아요 / 길어요 |
| 2 | 끊김 횟수 | 기준 1~5번: 같음 · 기준 6번+: ±1 | 그보다 1 더 | 그 이상 | 끊는 횟수가 달라요 (기준 N번, 나 M번) |
| 3 | 높낮이 곡선 | 상관 ≥0.4 또는 평균 차 ≤2.0반음 | 상관 ≥0.1 또는 ≤3.0반음 | 그 밖 | 높낮이가 달라요 |
| 4 | 음색 (MFCC **앞 8계수**+Δ, 전체 DTW) | ≤ 8.0 | ≤ 9.0 | > 9.0 | 소리가 달라요 |

> **[2026-10-07 Lab 2차 후 개정 — 사용자 결정]** 높낮이 0.5/1.5·0.2/2.5 → 0.4/2.0·0.1/3.0(살짝 완화), 음색 6/8 → **8/9**. 근거(8계수 음색): 같은 목소리 같은 소리 5.61·6.33 / 목소리 바꿔 같은 소리 7.73·8.12 / 다른 소리 9.09·10.25. 길이·끊김은 빡빡하게 유지(사용자 결정) — 끊김 ±1이 자주 △가 되므로 음색 통과선을 8까지 올려야 따라하기가 통과한다. **다른 소리 실측이 2개뿐이고 9.09가 선에 가까움.** 합성 시험에서는 다른 소리 잘못 통과가 2/30 → 9/30으로 늘었다(대부분 길게 이어지는 소리끼리 — 우와아아·헬로우·오오오·에에에).

- 높낮이는 두 쪽 다 **유성 비율 ≥ 40%**일 때만 본다(아니면 – 생략). 유성 비율 = **발화 프레임 중** 유성 프레임 — 초안은 끊김 사이 틈까지 분모에 넣어 "우가 우가"류가 14~27%로 나와 높낮이 검사가 항상 생략됐다(Lab 10/7).
- **높낮이 측정 오류 수정(10/7):** Lab에서 한 번 외침의 "반음 폭"이 25~30(2옥타브+)으로 나왔다. 원인 ① 고음 "아"처럼 3배음이 센 소리에서 YIN이 2/3 주기의 얕은 골을 먼저 잡아 **5도 높게** 읽음(합성 330→470Hz). → 유성 판단(가장 깊은 골 < 0.35)과 주기 고르기(max(0.1, 가장 깊은 골 + 0.05)보다 낮은 첫 골)를 분리. ② 남은 옥타브 튐은 중앙값에서 9반음 넘게 벗어나면 12반음씩 접고, 유성 프레임 5개 중앙값 필터. 합성 확인: 음높이 ×1.0/1.8/2.4 모두 실제 폭과 일치(우가 4.4·우와 6.8·미야옹 8.2·평평 0).
- 끊김: 빠른 음절(6번+)은 사람마다 한 번쯤 붙거나 갈라진다(Lab 같은 소리 9 vs 8).
- 음색을 **앞 8계수만** 보는 이유: 뒤쪽 계수는 음높이 배음이 섞여 남녀·고음에서 크게 갈린다(합성: 13계수면 여성 고음 "미야옹"이 같은 소리인데 11.1로 거절, 8계수면 7.5). 같은 사람 판정(§14.6)은 13계수 그대로.
- 수치는 Lab 실측(같은 사람) + 합성 시험 1차값. **다른 사람 실측 후 다시 맞춘다.**

> 남녀·마이크 차이: 높낮이는 중앙값을 빼서 **상대 곡선**만 보고, 음색은 CMVN으로 평균을 빼므로 절대 음높이·음색 차이는 통과한다. "우와와와아아아"(1~2번 끊김, 길게, 끝이 내려감) vs "우가우가우가"(3번 끊김, 짧게) 는 3번에서 갈린다. "헬로우" vs "할로"는 2·3·4가 같아서 5(음색)만 남는데, 이 둘은 **허용해도 게임상 문제 없다**(어차피 판정은 각자 자기 등록본 기준). 틀 검사의 목표는 "전혀 다른 소리를 등록하는 것"을 막는 수준이다.

### 14.5 기준 소리 배포·보관

| 항목 | 규칙 |
|---|---|
| 형식 | 16kHz mono, 앞뒤 침묵 제거, **최대 3초**. 전송은 8kHz μ-law 8bit로 압축(3초 = 24KB) — 들려주기 용도라 충분. 틀 검사의 기준 특징은 Host가 원본 16kHz로 뽑아 **특징(§14.3 등록본 형식, 0.8초 8.5KB ~ 3초 약 31KB)을 같이 보낸다** → 팀원은 압축본을 재생만 하고, 틀 검사는 Host가 뽑은 특징과 비교(압축 열화가 검사에 안 섞임) |
| 전송 | `CheerService` Host → ClientRpc **4KB 청크 + 버전 번호**. 접속 중 Tutorial에 들어온 클라이언트(Tutorial = 로비, 접속 후 녹음이 먼저일 수 있음)에게는 접속 시 Host가 현재 기준 소리를 그 클라이언트에만 재전송. 버전이 다른 청크는 버림 |
| NV | `_teamCheerWord`(FixedString32) → `_teamCheerSoundVersion`(int, Server write). 0 = 아직 없음(기본 소리, 아래). 팀원은 버전 변경을 보고 "다시 들어야/다시 등록해야 함" 표시 |
| 세션 | `GameSession.SetSessionTeamCheerWord` → `SetSessionTeamCheerSound(bytes, features, version)`. M/T 씬 진입 시 `CheerService.OnNetworkSpawn`이 세션값으로 재시드(기존 패턴 그대로). 재전송 필요 없음 — 전원이 Tutorial/Interlude에서 이미 받았고 late-join 없음 |
| 기본 소리 | **없음 (2026-10-07 확정).** `DefaultTeamCheerWord("fighting")` 폴백 삭제. Host 녹음(버전 ≥1)이 Tutorial 게이트 필수 조건 — `TutorialNetworkManager`의 전원 입장 판정에 `CheerService.HasTeamCheerSound` 조건 추가. Interlude는 Tutorial 것이 세션에 있으므로 재녹음 선택 |
| 검증 | 0.3초 미만·3초 초과·피크 부족·클리핑 과다 → Host에게 거절 힌트. 금칙어·형식·사전 검사(`CheerNameValidator`·`CheerLexiconBuilder`)는 **삭제** |

### 14.6 인게임 판정 — 내 마이크 vs 내 등록본 (같은 사람 기준)

§4.7 청취 조건(창 열림 + 내가 미통과)·세대 번호·제출 후 재제출 방지는 그대로. Vosk 워커 자리에 **매처 워커**가 들어간다.

| 항목 | 규칙 |
|---|---|
| 버퍼 | 최근 **4초** 특징 프레임 링버퍼(400프레임). 창 열림 순간 비움(기존과 동일) |
| 사전 게이트 (싸게) | ① 최근 발화 에너지가 소음 바닥 +12dB 이상 ② 발화 길이가 등록본의 0.5배 이상 ③ 발화가 끝났다면 길이 ≤ 2.0배 & 끊김 횟수 ±1. 하나라도 틀리면 DTW 안 돌림 → 잡담·숨소리·게임 소리로는 안 뚫림 |
| 본 판정 | **서브시퀀스 DTW**(시작·끝 자유) 등록본 ↔ 버퍼, MFCC 26차원 유클리드, 길이로 정규화한 거리 `d`. 등록본이 여러 개면 **하나라도 통과하면 통과**. **[10/7 기울기 제한]** 세로·가로 이동을 두 번 연달아 못 하게(Itakura식) → 맞춰지는 구간이 등록본의 0.5~2배로 묶인다. 없을 때 "오오오"처럼 처음부터 끝까지 같은 소리는 등록본 전체가 0.5초 구간에 몰려(Lab 길이비 0.28) 리듬 검사에서 떨어졌다. 합성: 내 다른 소리 잘못 통과 2/30 → 0/30 |
| partial | 100ms마다 평가. `d ≤ T_self`가 **연속 2회**면 제출(§4.7 `PartialConfirmHits` 유지 — "한 번 튄 값"을 거름). 말 끝나기 전에 통과 가능 |
| final 보험 | 발화 종료(침묵 300ms) 시점에 `d ≤ T_self × 1.15`면 제출 |
| 비용 | 400 × 300 프레임 × 26차원 ≈ 3M 연산/100ms → 무시 수준 |
| **T_self** | **전 플레이어 공통 고정 6.0**(말 끝난 뒤 보험 ×1.15 = 6.9). **[10/7 사용자 결정]** 사람마다 바뀌는 보정(두 등록본 거리 × 2.5)은 삭제 — 실제 목소리에선 두 등록본 거리가 3.5~7.1이라 항상 상한 6.0에 붙어 작동하지 않았고, "기준이 없다"는 혼란만 만들었다. Lab 실측 통과 d 4.7~5.8, 다른 소리 6.4+. 등록본은 나 1 + 나 2 최대 2개, 인게임 통과분은 **추가하지 않음**(오염 방지) |
| 로그 | 창마다 `d` 최솟값·통과 여부·사전 게이트 탈락 사유를 구조화 로그(`NetworkDesign.md` 로깅 규약)로 남겨 임계값 튜닝 근거로 씀 |

**오인식 2종과 설계 방향:** 못 알아들음(false reject)이 게임을 막으므로 `T_self`는 **넉넉한 쪽**으로 잡고, 잘못 통과(false accept)는 사전 게이트(크기·길이·끊김)로 막는다. 잘못 통과의 피해는 "함정이 조금 쉬워짐"뿐.

**정확도 기대치(추정, 실측 아님):** 같은 사람·같은 마이크 기준 90%+ 첫 외침 통과. 떨어뜨리는 요인: 등록 땐 차분히 말하고 게임에선 소리 지름(→ 녹음 안내 문구 "게임에서 외치듯이"), 스피커 게임 소리 유입, 주변 소음. 구역 3 연습 성공률을 실측 지표로 쓴다.

### 14.7 UI 변경

| 컴포넌트 | 변경 |
|---|---|
| `TutorialCheerNameUI` | **코드 완료.** Host 섹션 [녹음/정지][다시 듣기][확정] + 상태 / 전원 섹션 [호스트 소리 듣기][나 1 녹음/정지][나 2 녹음/정지] + 상태 줄 + 거절 힌트. 텍스트 입력 필드 삭제(인스펙터 재배선 필요 — §14.11 ② 체크리스트) |
| `TeamCheerWordUI` (HUD) | **코드 완료.** 라벨은 프리팹 정적 텍스트 "[R] 팀 구호 듣기"로 바꿀 것(코드가 단어를 안 씀). R키/버튼 → `CheerSoundPlayback.PlayHostClip`. 기준 소리 없으면 라벨 숨김 |
| `TeamCheerWarningUI` | **코드 완료.** 자동 재생 없음. `tKeyHintRoot`(창 안 3회 연속 실패 시 켜는 안내 오브젝트) 필드 추가 — 문구 "설정 → 'T키로 응원하기'를 켜면 T키로도 응원할 수 있어요" |
| `TutorialTeamCheerTestSignboard` | **코드 완료.** `RequestStartRpc`에서 `CheerService.MarkNextWindowAsPractice()` 한 줄 추가 |
| `TutorialGatherDisplay` | **코드 완료.** `gateBlockText`(Start 간판 아래 TMP 3D 한 줄) + 로컬라이즈 2키 — 막힘 사유 "(N/M)" 표시 |
| 구역 2 보드·힌트 문구 | "영어 소문자 2~12자" → "아무 소리나 녹음(최대 3초)" + "팀원은 똑같은 소리로 등록" (13개 언어 재번역) |
| Options | "T키로 응원하기" 토글 유지. 세션 자동 ON(§14.8)은 토글 값을 건드리지 않음 |

### 14.8 폴백 — 마이크 없음·등록 실패

| 상황 | 동작 |
|---|---|
| 마이크 없음 / Dissonance 오디오 5초 미수신 / 등록본 없이 게이트 통과 | **이번 세션만** 그 플레이어 T키 응원 자동 ON(`PlayerPrefs` 안 건드림) + HUD에 "T키로 응원" 안내 |
| 등록본 버전 ≠ 기준 소리 버전 (Interlude 재녹음 뒤 미재등록) | 위와 동일 |
| 창 동안 판정 3회 연속 실패(사전 게이트 통과했는데 `d` 초과) | 그 창 한정 힌트 **"설정(Options) → 'T키로 응원하기'를 켜면 T키로도 응원할 수 있어요"** — 설정 위치까지 명시(2026-10-07 확정). 자동 ON은 하지 않음(이미 등록본이 있는 사람이므로) |
| 마이크 음소거(M키/옵션) 상태 | Dissonance가 구독자에게 PCM을 계속 주는지 **코드 확인 필요**. 안 주면 녹음·판정 모두 불가 → 패널에 "마이크가 꺼져 있어요" 표시 |

### 14.9 삭제·추가 파일

| 삭제 | 추가/변경 |
|---|---|
| `Assets/ThirdParty/Vosk/*`, `StreamingAssets/vosk-model-en-us-0.22-lgraph` (205MB), `VoskModelLoader`, `CheerLexiconBuilder`, `CheerNameValidator`, `TrySetTeamCheerWord` 사유 `format/reserved/blocked/unknown` | `CheerSoundFeatures`(MFCC·피치·끊김·DTW, 순수 함수) · `CheerSoundTemplate`(데이터+직렬화) · `CheerSoundMatcher`(워커, §14.6) · `CheerSoundRecorder`(캡처 모드, §14.2) · `CheerService`: `TrySetTeamCheerSound` + 청크 RPC + 버전 NV · `GameSession`: 세션 기준 소리 + 로컬 등록본 · UI 3종(§14.7) · `TutorialNetworkManager`/`InterludeNetworkManager` 게이트 스냅샷 교체 |

§3.2·§3.4·§4.2·§4.5·§5·§6.2·§8.3 중 Vosk·단어·사전을 전제한 서술은 이 절이 우선한다(이력으로 보존).

**[2026-10-07 ② 적용 결과]** 삭제: `ThirdParty/Vosk/*`(libvosk + C# 바인딩), `StreamingAssets/vosk-model-en-us-0.22-lgraph`(205MB), `VoskModelLoader`·`CheerLexiconBuilder`·`CheerNameValidator`, `GameSession` TeamCheerWord 세션 API(`DefaultTeamCheerWord`·`Set/GetSessionTeamCheerWord`·`HasSessionTeamCheerWord`), 두 NetworkManager의 `BroadcastSessionTeamCheerWordClientRpc`. 추가: `CheerSoundLocalState`(static, 씬 넘어 유지), `CheerSoundPlayback`(2D 재생 DDOL). `CheerKeywordEngine`·`TutorialCheerNameUI`·`TeamCheerWordUI`는 이름 유지한 채 내용 교체(프리팹·씬 연결 보존).

### 14.10 미정 — 사용자 확인 필요

1. ~~기본 소리~~ → **확정: 기본 소리 없음, Host 녹음이 게이트 필수**(§14.2·§14.5).
2. ~~재생~~ → **확정: 자동 재생 없음, 키로만 재생**(§14.7). 키 `R`은 초안 — 다른 키 원하면 변경.
3. ~~녹음 UX~~ → **확정: 버튼 누르면 바로 녹음, 다시 누르면 정지(한 버튼 토글, 3초 상한은 안전장치)**(§14.2).
4. ~~2번째 등록본~~ → **확정: 연습 통과분을 2번째 등록본으로 사용**(§14.6). 추가 확정: **구역 3 연습은 필수 관문**(경험자 포함, §14.2).
5. ~~T키 힌트~~ → **확정: 넣는다, 설정 위치까지 안내**(§14.8).
6. 초깃값 — Lab 1~3차(같은 사람) 실측으로 1차 확정: 음색 8/9, 높낮이 0.4/2.0·0.1/3.0, △ 2개까지, T_self 6.0. **다른 사람 목소리 표본은 아직 없음** — 받으면 재확인.
7. ~~빈틈~~ → **확정(2026-10-07 사용자): 등록본은 있는데 구역 3 연습 창에서 7회 연속 실패하면 그 세션 T키 자동 ON**(§14.8 미등록자와 같은 처리) + 힌트. 실패 1회 = 사전 게이트를 통과한 외침이 기준 초과로 거절된 것(§14.8 "3회 연속 실패 시 힌트"와 같은 셈법, 힌트는 3회·자동 ON은 7회).
8. **[신규 — 빈틈]** 게임 소리가 스피커로 마이크에 들어가면 소음 바닥이 올라가 d가 커진다(Lab은 조용한 환경). 고정 6.0이 게임 중에도 맞는지는 ②에서 실제 함정 창으로 확인. 창마다 d 로그(§14.6)로 추적.

### 14.11 구현 진행

#### ① 신호처리 클래스 — **코드 완료 (2026-10-07)**, 게임 미연결

| 파일 | 역할 |
|---|---|
| `Cheer/CheerSoundParams.cs` | 수치 SSOT (전부 초안 — Lab 실측으로 확정) |
| `Cheer/CheerSoundDsp.cs` | 프레임 dB · MFCC · YIN 피치 · 끊김 횟수 · DTW(전체/부분 구간). Unity API 없음 → 워커 스레드 가능 |
| `Cheer/CheerSoundTemplate.cs` | 녹음 → 앞뒤 자르기 → 특징(등록본). 녹음 문제(`CheerClipIssue`) 판정. 직렬화 |
| `Cheer/CheerSoundShapeCheck.cs` | 틀 검사(§14.4). 첫 실패 항목 + 전 항목 수치 |
| `Cheer/CheerSoundMatcher.cs` | 인게임 판정(§14.6). 4초 링버퍼, 100ms 평가, partial 2연속 / final ×1.15 |
| `Cheer/CheerSoundRecorder.cs` | 버튼 토글 녹음 버퍼. 앞뒤 0.1초(클릭 소리) 버림, 4초 자동 정지. **마이크 직접 안 엶** |
| `Cheer/CheerSoundCodec.cs` | Host 클립 8kHz μ-law 압축/복원 |
| `Editor/CheerSoundLab.cs` | **Tools/Cheer Sound Lab** — Host/나1/나2 녹음·WAV 저장/열기·틀 검사 수치·T_self·실시간 판정·흘려보기. 임계값 실측 도구 |

**검증:** Unity 밖에서 런타임 7개 파일 + Lab을 Unity 6.3 DLL로 컴파일 — 오류·경고 0. 사용자 Lab 실측(10/7): 판정은 잘 됨, 틀 검사가 같은 소리도 자주 거절 → §14.4 3단계 판정으로 개정. 합성 소리(모음 포먼트+음높이 곡선, 남/여 f0 ×1.8·포먼트 ×1.15·속도 ±15%·소음) 6종(우가우가우가/우와아아/헬로우/미야옹/오오오/에에에)으로:

| 시험 | 결과 |
|---|---|
| 틀 검사 — 다른 사람(여)이 같은 소리 등록 | 초판 **5/6**(고음 "미야옹" 음색 11.1로 거절 — 처음 보고에서 6/6으로 잘못 적었음, 10/7 정정) → 3단계 판정 + 음색 8계수 개정 후 **6/6 통과** |
| 틀 검사 — 다른 사람이 다른 소리 등록 | 개정 후 30개 중 28개 거절, 2개 통과(우와아아 기준에 헬로우·오오오 — 길게 이어지고 끊김 1번으로 같음) |
| 판정 — 내 등록본 vs 내 실시간(높이 +5%·속도 0.9·소음 3배) | **6/6 통과** |
| 판정 — 내 등록본 vs 내 다른 소리 | 30개 중 2개 잘못 통과(우와아아←우가우가, 오오오←헬로우) |
| 판정 — 소음만 | **0/6** (d 12~15, 기준 4~6) |
| 계산량 | 평가 1회 약 3ms(1.3초 등록본 2개) — 워커 스레드 충분 |

> 합성 소리는 실제 목소리 대용일 뿐이다. **임계값은 Lab으로 실제 목소리(여러 사람·마이크) 측정 후 확정**(§14.10 #6).

**Lab 실측 1~3차 (2026-10-07, 사용자 본인 목소리):** 판정은 매번 잘 됨. 틀 검사는 ① 한 항목 초과 거절 → 3단계(△ 2개 거절) → △ 3개 거절로 두 번 완화, ② 유성 비율 분모 버그(높낮이 항상 생략), ③ 높낮이 5도·옥타브 오류(반음 폭 25~30), ④ 일정한 소리의 실시간 몰림(길이비 0.28), ⑤ Lab이 거절된 녹음도 판정에 쓰던 문제(기준 없음) — 전부 수정. T_self 보정은 삭제하고 고정 6.0. 세부는 §14.4·§14.6·§14.2 등록본 규칙.

**②로 넘기는 주의점:**
- `CheerKeywordEngine`의 자동 게인(`NormalizeBuffer`·`_smoothedGain`)은 청크마다 음량을 바꿔 **끊김 곡선을 뭉갠다** → 녹음기·판정기에는 **게인 적용 전** 16kHz 신호를 넣을 것(리샘플러는 재사용).
- 판정기는 워커 스레드에서 돌리고(Vosk 워커 자리), 결과(통과/Eval)만 메인으로 넘긴다.
- 스피커로 나오는 팀원 목소리가 내 마이크로 들어가 내 판정을 통과시킬 수 있음(헤드셋 미사용 시). Vosk 때도 같은 구조 — 피해는 "함정이 조금 쉬워짐"뿐이라 별도 대응 안 함.

#### ② 게임 연결 + Vosk 삭제 — **코드·에디터 완료 (2026-10-07)**, 플레이 검증 남음

| 파일 | 변경 |
|---|---|
| `Cheer/CheerSoundLocalState.cs` (신규) | 내 PC 전용 상태: Host 기준 소리(버전·압축 클립·특징)·나 1/나 2·세션 T키 자동 ON·실패 횟수. `GameSession.ResetSession`에서 비움 |
| `Cheer/CheerSoundPlayback.cs` (신규) | Host 소리/내 녹음 2D 재생(SFX 볼륨 따름) |
| `Cheer/CheerService.cs` | `_teamCheerWord` → `_teamCheerSoundVersion`(int)·`_practicePassedCount`·`_practiceWindow` NV. `TrySetTeamCheerSound` + 4KB 청크 ClientRpc 배포 + `RequestTeamCheerSoundServerRpc`(pull). 연습 창 통과 집합·`AllPracticePassed`·`ForgetClient`. 다음 씬 스폰 시 `CheerSoundLocalState`에서 버전 되살림 |
| `Cheer/CheerKeywordEngine.cs` | Vosk 워커 → `CheerSoundMatcher` 워커. 자동 게인 삭제. `BeginCapture/EndCapture`(패널 녹음). `Local` 접근자. 통과/실패 이벤트 → `CheerSoundLocalState` 집계·`OnVoiceAttemptFailed`. 등록본 없이 인게임 창 → 세션 T키 자동 ON(연습 씬 제외) |
| `Cheer/CheerSoundMatcher.cs` | `OfflineCheck`(녹음 하나를 실시간처럼 판정 — 나 2 등록·Lab 공용) |
| `Cheer/CheerDigitInput.cs` | `IsTKeyEnabled` = 옵션 OR 세션 자동 ON |
| `UI/TutorialCheerNameUI.cs` | §14.2 패널 전면 교체(텍스트 입력 삭제) |
| `UI/TeamCheerWordUI.cs` | R키/버튼 재생, 단어 표시 삭제 |
| `UI/TeamCheerWarningUI.cs` | T키 힌트 오브젝트(창 안 3회 연속 실패) |
| `UI/TutorialGatherDisplay.cs` | 게이트 막힘 사유 한 줄 |
| `Stage/TutorialTeamCheerTestSignboard.cs` | 연습 창 표시 1줄 |
| `Network/TutorialNetworkManager.cs`·`InterludeNetworkManager.cs` | `GateBlock` NV + `EvaluateSoundGate`(Host 소리 + 전원 연습 통과) 카운트다운 조건. 세션 TeamCheerWord 확정·배포 삭제. Tutorial 이탈 시 `CheerService.ForgetClient` |
| `GameSession.cs` | TeamCheerWord 세션 API 삭제, `ResetSession` → `CheerSoundLocalState.ResetSession()` |

**검증:** Unity 밖에서 `Assets/**/*.cs`(Assembly-CSharp 전체 + Lab)를 Unity 6.3 DLL·`Library/ScriptAssemblies`(NGO·Dissonance·TMP·Localization·Facepunch)로 컴파일 — **오류 0**. Unity 에디터 컴파일·플레이는 아직.

**인스펙터·씬 재배선 체크리스트 — 2026-10-07 MCP로 전부 적용(아래 '에디터 적용 결과'):**
1. **Tutorial·Interlude `CheerNamePanel`(TutorialCheerNameUI):** 구 `hostTeamWordSection/teamWordInputField/teamWordConfirmButton/currentTeamWordText` 연결이 사라짐 → 새 필드: `hostSection`(Host 전용 루트) 안에 `hostRecordButton`+`hostRecordButtonLabel`·`hostPreviewButton`·`hostConfirmButton`·`hostStatusText`; 전원용 `listenHostButton`·`hostSoundStatusText`·`record1Button`+`record1ButtonLabel`·`record2Button`+`record2ButtonLabel`·`enrollStatusText`; `feedbackText`·`closeButton`은 그대로. 로컬라이즈 필드 28개는 비워 두면 한국어 폴백(키 목록은 `TutorialTranslations.md`에 추가 예정).
2. **`UI.prefab` → `TeamCheerWordUI`:** `wordLabel` 정적 텍스트를 "[R] 팀 구호 듣기"로. 선택: `playButton`, `visualRoot`.
3. **`UI.prefab` → `TeamCheerWarningUI`:** `tKeyHintRoot`에 안내 텍스트 오브젝트(기본 비활성) 연결. 문구 "설정 → 'T키로 응원하기'를 켜면 T키로도 응원할 수 있어요".
4. **Tutorial·Interlude `TutorialGatherDisplay`:** `gateBlockText`에 Start 간판 아래 TMP 3D 텍스트(기본 비활성) 연결.
5. **Tutorial 보드 문구:** `Board_CheerName.Body` "영어 단어로 정해요" → "호스트가 아무 소리나 녹음해요(최대 3초). 팀원은 똑같은 소리로 두 번 녹음해요 — 녹음한 그대로 외치세요". `Board_TeamCheer.Body`에 "연습을 통과해야 시작" 한 줄. 13개 언어 재번역은 별건.
6. **Options:** "T키로 응원하기" 토글 그대로.
7. Player 프리팹 `CheerKeywordEngine`: 옛 `soloMicGain/autoNormalizeMic/normalizeTargetPeak` 필드가 사라져 인스펙터에 "missing" 없이 조용히 제거됨 — 할 일 없음.

**플레이 검증 순서(2인 권장):** ⓪ Host 녹음·확정 → 팀원 패널에 "팀 구호 v1" 뜨고 [듣기] 재생 ① 나 1 거절 사유 표시·통과 ② 나 2 둘 다 검사 ③ 연습 표지판: 간판 "(0/2)"→"(2/2)"→카운트다운 ④ M1 입 함정에서 음성 통과·HUD R키 ⑤ 등록 안 한 팀원이 게이트 넘으면 첫 창에서 T키 자동 ON ⑥ Interlude 재녹음 → 재등록·재연습 ⑦ 타이틀 복귀 후 새 방에서 전부 초기화.

**아직 확인 못 한 것:** 게임 소리 유입 시 고정 6.0(§14.10 #8) — ④에서 창마다 `VoiceDetected/VoiceAttemptFailed` 로그의 d로 본다. NGO `byte[]` RPC 인자·4KB 청크 전송은 NGO 2.x 표준 지원 범위지만 실제 2인 전송은 미확인.

**에디터 적용 결과 (2026-10-07, MCP):**
- 에디터 컴파일 오류 0(새 타입 로드 확인).
- **Tutorial·Interlude `CheerNamePanel`** 재구성: `HostSection`(HostHintText · [녹음][다시 듣기][확정] · HostStatusText) / `HostSoundStatusText` / [호스트 소리 듣기][1번 녹음][2번 녹음] / `EnrollStatusText` / `FeedbackText` / [닫기]. 텍스트 입력 필드 삭제. 스크립트 필드 15개 + `LocalizedString` 23개 연결(누락 0). 화면: `Assets/Screenshots/CheerSound_Panel_Tutorial.png`.
- 비-Host는 `HostSection`이 숨겨져 제목 아래가 비어 보인다(위치 고정 레이아웃) — 보기 싫으면 레이아웃 그룹으로 바꿀 것.
- **Tutorial·Interlude `GatherSign`**: 자식 `GateBlockText`(TMP 3D, 진한 자주색 굵게) + 뒤판 `Plate`(간판 배경 복제) — 기본 꺼짐, 막힘 사유 있을 때만 켜짐. 화면: `CheerSound_GateBlockText_Tutorial.png`.
- **`UI.prefab`**: `TeamCheerWord/Caption` "TEAM CHEAR"(오타) → "TEAM CHEER", `Word` → "[R] 팀 구호 듣기"(LocalizeStringEvent `Tutorial.HUD.TeamCheerListen`, 자동 크기). `TeamCheerWarning/TKeyHint`(기본 꺼짐, `Tutorial.HUD.TeamCheerTKeyHint`) + `tKeyHintRoot` 연결. 다른 씬에 이 라벨을 덮어쓴 오버라이드 없음 확인.
- **번역:** `Tutorial` 테이블 신규 28키·수정 6키(제목·HostHint·보드 2종·Prompt 2종) × 13로케일(pt 포함). 보드의 "영어 단어로 정해요" 문구 삭제. **원어민 검수 남음.**
- **폰트:** 한·일·중 정적 아틀라스에 새 글자가 없어 `Tools/Font/Noto Static 베이킹 - 실행` 재실행 → 13로케일 새 문구 누락 글자 0.
- **씬 저장 잡음:** Tutorial 저장 시 프리팹 인스턴스 RectTransform 레이아웃 값 149개가 섞임 → 오버라이드 항목 단위로 HEAD 값 복원(스크립트, 줄 번호 기반 되돌리기는 엉뚱한 값을 바꿔서 폐기). 두 씬 모두 오버라이드 항목 집합이 HEAD와 동일(Tutorial 669·Interlude 378). Interlude diff가 큰 건 PrefabInstance 블록 순서가 바뀐 것뿐.

#### ③ 실플레이 2차 반영 — **코드·에디터 완료 (2026-10-07)**, 재실측 남음

**실플레이 로그(사용자, 1인 Host = 팀원 역할 겸함):** 같은 사람·같은 마이크·같은 말("what's going on")인데 1번 녹음이 Host와 끊김 3 vs 5, 음색 8~12로 거절 연속. 연습 실패 거리 7.4~8.5(Lab 같은 사람 4.7~5.8). 실패 로그에 같은 d가 3~4번 연달아 찍힘.

**원인(소스 확인):** 판정기가 받던 `SubscribeToRecordedAudio`는 `BasePreprocessingPipeline.SendSamplesToSubscribers` — rnnoise + WebRTC(잡음 억제·에코 제거) **처리 후** 소리. 처리가 음절 사이 틈·스펙트럼을 매번 다르게 바꿔 끊김·음색이 흔들렸다. Lab은 마이크 직접이라 처리가 없었다.

| 변경 | 파일 |
|---|---|
| 원본 마이크 탭 `comms.MicrophoneCapture.Subscribe(this)` (없으면 처리된 스트림, 로그 `MicTap source=raw/processed`) | `CheerKeywordEngine` |
| 2번 녹음 1번 비교 삭제, 1번 소리(발화 구간) 보관 | `TutorialCheerNameUI`, `CheerSoundLocalState.SetTemplate1(t, pcm)`, `CheerService` |
| R키 = 내 1번 녹음(미등록 = Host) | `CheerSoundLocalState.GetListenClip`, `CheerSoundPlayback.PlayListenClip`, `TeamCheerWordUI` |
| 녹음 1~3초(`RecordMinSec`·`RecordMaxSec`), 소리 0.5초(`MinDurationSec`) | `CheerSoundParams`, `CheerSoundRecorder`, 패널 버튼 잠금 |
| 끊김 최대 △ | `CheerSoundShapeCheck.BandBursts` |
| final(말 끝남) 평가는 큰 소리가 새로 난 뒤 한 번만 — 실패 중복 집계 버그 | `CheerSoundMatcher` |
| 등록 전용 기준 9.0(`EnrollPairThreshold`) 삭제 — 1↔2 비교가 없어짐 | `CheerSoundParams`, `CheerSoundMatcher.UseFixedThreshold`는 Lab 흘려보기용으로 남김 |
| Lab: 나 2도 Host 틀 검사만 | `CheerSoundLab` |
| 번역 3키×13로케일(HostHint·Feedback_TooShort·Status_EnrollNeed1 — 0.5초 안내), 폰트 재베이킹(누락 0) | `Tutorial` 테이블, Noto Static |
| 패널 20% 확대(`localScale 1.2`, 820×590 → 화면상 984×708) | Tutorial·Interlude 씬 |

**재실측 필요:** 원본 마이크로 바뀌어 거리 분포가 Lab 쪽(같은 사람 4.7~5.8)으로 내려올 것으로 예상하지만 미확인. 게임 중 기준 6.0·틀 검사 8/9를 실측으로 다시 확인. 스피커로 게임 소리를 크게 틀 때(에코 제거 없음) 영향도 같이 확인. 도중에 마이크를 꽂았을 때 Dissonance가 새 장치를 잡는지 미확인.

#### ④ 준비 상태·마이크 없음·연습 거절 — **코드·에디터 완료 (2026-10-07)**, 2인 Steam 검증 남음

**사용자 결정(10/7):**
| 주제 | 규칙 |
|---|---|
| 준비 완료 | 1·2번 등록 완료, 또는 개인 "마이크가 없어요" 토글, 또는 Host "마이크 없음 — 팀 전체 T키" 토글 |
| 연습 요청 | 접속자 중 한 명이라도 준비 안 됐으면 **연습 자체를 거절**(연습 창은 전원이 통과할 때까지 열려 있어 끝나지 않으므로). 표지판엔 "아직 준비 안 된 사람이 있어요. Start 간판을 확인하세요", Start 간판은 준비 안 된 줄이 빨갛게 깜빡임 |
| Host 마이크 없음 | Host 패널 토글 → 이번 판 음성 응원 끔, 전원 T키, 게이트는 녹음 조건 면제(연습은 T키로) |
| Client 마이크 없음 | 패널 토글 → 그 사람만 T키, 준비 완료로 침, 간판에 "T키" 표시 |
| Start 간판 | 막힘 사유 한 줄 + **사람별 줄**(초록 준비 완료 / 흰 연습 필요 / 빨강 녹음 필요, 앞에 "T키 ·") — 간판 **위**로 옮김(시작 존 파티클에 가려지던 문제) |
| Interlude | Tutorial과 같은 시스템. 단 Host가 이 씬에서 녹음(또는 팀 전체 마이크 없음)을 **안 바꿨으면 연습 면제**(개인 재녹음·연습은 자유). 바꿨으면 전원 재등록·연습 |
| 게임 중 미등록자 T키 자동 | 유지(각자 PC의 bool 하나, 네트워크 없음 — 가벼움) |

**구현:** `CheerService` — `_teamNoMic` NV, `NetworkList<CheerReadyEntry>`(색·상태 비트·연습 통과), `ReportStatusServerRpc`(각자 상태 비트 1바이트, 바뀔 때만), Host가 0.25초마다 비교 후 바뀐 경우에만 목록 갱신, `AllReadyForPractice`, `PracticeRequired`(Tutorial 항상 / Interlude 변경 시), `BroadcastPracticeRejected`. `CheerSoundLocalState` — `PersonalNoMic`·`TeamNoMic`·`LocalStatusFlags`·`IsReadyFlags`. `CheerDigitInput.IsTKeyEnabled`에 두 토글 추가. `TutorialTeamCheerTestSignboard` — Host가 준비 확인 후 거절, `rejectRoot`. `TutorialGatherDisplay` — 사람별 목록·거절 깜빡임. `TutorialCheerNameUI` — 두 토글(Host엔 개인 토글 숨김), 팀 전체 T키면 녹음 버튼 잠금. 두 게이트 매니저 `EvaluateSoundGate` 갱신.

**에디터(MCP):** Tutorial·Interlude 패널에 `HostSection/TeamNoMicRow`·`PersonalNoMicRow`(설정 창 토글 복제, 리스너 제거) + 레이아웃 820×660(×1.2), Start 간판 상태판 위로(20×8.4, 판 22×9.4, 밝은 글자색), 연습 표지판 `RejectRoot`(프롬프트 복제, 빨간 배경). 번역 12키×13로케일 + 폰트 재베이킹(누락 0). 씬 오버라이드 항목 HEAD와 동일 확인. 화면: `Assets/Screenshots/CheerSound_Panel_Tutorial.png`, `CheerSound_StartSignStatus_Tutorial.png`.

**2인 Steam에서 볼 것:** ① 팀원 미등록 상태에서 연습 E → 거절·간판 빨간 줄 ② 팀원 "마이크가 없어요" → 간판 "T키 · 연습 필요" → 연습 T키 통과 ③ Host 팀 전체 토글 → 녹음 버튼 잠김·게이트가 연습만 봄 ④ 늦게 들어온 팀원에게 목록이 보이는지 ⑤ Interlude 무변경 → 바로 진행 / Host 재녹음 → 전원 재등록·연습.
