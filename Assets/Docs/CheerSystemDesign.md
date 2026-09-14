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
│  → 감지 시 SubmitTeamCheerServerRpc                  │
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
| 사망 | 씬 리로드로 자동 초기화(`StageResetOnPlayerDeath`, 기존 동일) |

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
| 인식 조건 | 팀 응원 배너(`TeamCheerWarningUI`, `OnHazardWindowChanged` 구독)가 떠 있는 동안만. Host 판정은 등록된 revert의 `IsAvailable`(창 열림) + 이번 창 미성공(`_teamWindowConsumed == false`) — `ValidateTeamCheer` |
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
| 검증 | `CheerNameValidator`(형식/금칙어) 그대로 재사용 |
| 충돌 검사 | **[2026-09-14 삭제]** 개인 CheerName이 고정값·비인식 대상이 되어 겹칠 대상이 없음(§3.3) |
| 구현 | `CheerService`에 `NetworkVariable<FixedString32Bytes> _teamCheerWord`(Server write, Everyone read) + Host-only setter. Host 프로세스는 곧 서버이므로 **RPC 불필요** — Host 클라이언트 UI가 `IsServer` 가드 걸린 public 메서드를 직접 호출. **단, 인스턴스 메서드라 그 씬에 `CheerService`가 실제로 배치돼 있어야 호출 가능** — Tutorial에서 Host가 설정하려면 Tutorial 씬에도 `CheerService`가 필요(§10 Phase D0) |
| 세션 지속 | `GameSession.SetSessionTeamCheerWord`/`GetSessionTeamCheerWord` (기존 `SetSessionCheerNames`와 동일 패턴) — `TutorialNetworkManager`의 게이트 완료 지점(기존 `SetSessionCheerNames` 호출부 2곳)에서 나란히 호출 |

### 3.3 양방향 충돌 검증

> **[2026-09-14 삭제]** 개인 CheerName이 고정값이 되고 음성 인식 대상에서도 빠져, 양방향 충돌 검사는 **코드에서 제거**됐다. `CheerService.TrySetTeamCheerWord`의 실패 사유는 `format`/`reserved`/`blocked`/`not_server`뿐(`taken` 없음). 아래는 이력.

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
[메인]  Dissonance(또는 솔로 마이크) PCM 캡처 → float→short → _pcmQueue
[워커]  VoskWorker: AcceptWaveform → JSON → _resultQueue
[메인]  결과 drain(final만) → TeamCheerWord 매칭 → SubmitTeamCheerServerRpc(isVoice: true)
        (개인 버프는 음성 경로 없음 — Space → RequestSelfBuffServerRpc, §6.1)
```

`AcceptWaveform`은 반드시 백그라운드 워커. 메인에서 돌리면 프레임 히치.

### 4.5 모델 배포·로드 — 비ASCII 경로 크래시 주의 **[코드 확정]**

모델은 zip이 아니라 **압축 해제된 폴더**로 `StreamingAssets`에 포함(`persistentDataPath`로 풀면 Windows 한글 사용자명 경로에서 100% 크래시, libvosk/Kaldi가 `std::ifstream`으로 비ASCII 경로를 못 읽음 — [vosk-api#1072](https://github.com/alphacep/vosk-api/issues/1072)). `VoskModelLoader.GetSharedModel()`의 null 반환은 반드시 존중. 모델 로드 실패는 **음성 인식만 비활성화하고 게임 진행은 막지 않음**.

### 4.6 Dissonance 버퍼 경고

`Insufficient buffer space` 류 경고는 **Warn**, 크래시 아님. 1순위 원인은 메인 히치, 2순위는 청크 크기. 재발 시 프로파일 우선.

---

## 5. 인식률 개선 파이프라인 (기존 유지)

### 5.1 커스텀 lexicon 주입 — 불가능 (재확인)

`vosk_recognizer_set_grm_with_lexicon`은 [PR #1362](https://github.com/alphacep/vosk-api/pull/1362)로 미병합·정체 상태 — 공식 배포본에 없음. 대신 아래 A+B로 대응.

### 5.2 A. 사전 검증 + B. 발음 변형 대체 단어

```
CheerName/TeamCheerWord 후보
  → Model.vosk_model_find_word(word) → -1이면 모델 사전에 없음 → Tutorial UI 경고(강제 아님)
  → [2026-09-14] 이름은 인식 대상이 아니므로 대체 단어(B) 경로 자체를 삭제
  → 커스텀 이름/TeamCheerWord가 사전에 없으면 경고만, 대체 발음은 미지원(§5.3 C 참고)
```

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
| `PlayerBuffSystem.buffSettings[type].duration` | `PlayerBuffSystem` | Shield/SpeedUp 지속 | Shield 5초 / SpeedUp 10초 (기존 유지) |
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

- [ ] `EmoteHintUI`의 `hintLabel` 텍스트: "T: 이모트" → "1~8: 이모트" 류로 문구 수정 (T는 이제 팀 응원 키)
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
- `TrySetTeamCheerWord(string, out reason)` — Host(`IsServer`)만. 실패: `"format"` / `"reserved"` / `"blocked"` / `"taken"` / `"not_server"`. RPC 없음
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
- `CheerKeywordEngine.ParseAndSubmit`에 `_lastDetected[word] = Time.time`이 두 분기에 있음 (실행은 상호배타). 스타일만, 동작 버그 아님.
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
