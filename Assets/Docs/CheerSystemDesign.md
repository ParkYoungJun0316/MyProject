# Cheer System Design

음성·숫자키 **응원 시스템** 설계 문서 — 개인 버프(자기 응원) + 팀 버프(팀 공용 키워드) 이원 구조.
관련: [`NetworkDesign.md`](NetworkDesign.md) (네트워크 검증 단계·Host 권한·출시 달력), [`CheerAndTutorialDesign.md`](CheerAndTutorialDesign.md) (Tutorial 구역·게이트 흐름 — CheerName/TeamCheerWord 설정 UI는 Tutorial 씬에 있음).

**범례**

| 태그 | 의미 |
|------|------|
| **[Ship Must]** | **2026-09-01 정식 출시** 전 필수 |
| **[Post-Launch]** | 정식 이후 |

> **2026-09-01 전면 개편.** 구 "팀원이 나를 응원해야 버프" 방식(전원 투표로 타인 타겟에게 버프)은 **폐기**. 신규: ①자기 응원 → 즉시 개인 버프, ②팀 전원 공용 키워드 → 팀 전체 버프. 남을 지목해서 응원하는 기능(cross-targeting)은 완전히 삭제됐다.
>
> **구현 현황:** Phase A·B·C·D1·D2 코드 완료 (인수인계 **§10.1**~**§10.4**). D3 안내 문구는 코드에서 제외(사용자 씬 텍스트). D0는 Tutorial 씬에 `CheerService` 배치됨. 남은 건 D4(구역 3) + Phase E 에디터.

---

## 1. 응원 시스템 개요

### 1.1 왜 바꿨나

기존 방식("나를 제외한 전원이 나를 응원해야 발동")은 마이크·인식 실패·타이밍이 하나만 어긋나도 버프가 안 뜨는 구조라 실전에서 답답함이 컸다. **개인 버프(확정적, 항상 내 힘으로 가능)** + **팀 버프(협동 하이라이트, 확실히 보상)** 조합으로 대체한다.

구 2안("A가 B를 응원하면 B는 버프, A는 쿨타임 손해")은 **드랍**. 개인 버프로 누구나 자력으로 버프를 받을 수 있는 상황에서, 남을 도우면 손해 보는 구조를 얹으면 오히려 협동을 방해하는 역설적 인센티브가 되고, 구현 비용(응원자별 쿨타임 신규 state)도 크다.

### 1.2 규칙 한 줄 요약

| | 개인 버프 | 팀 버프 |
|---|---|---|
| 트리거 | **자기** CheerName 외치기 (또는 숫자키 `1`, 기본 비활성) | **전원**(예외 없이 자기 자신 포함)이 **팀 공용 키워드(TeamCheerWord)** 외치기 (또는 숫자키 `2`, 기본 비활성) |
| 발동 방식 | **즉시** — 필요 인원 1명(자기 자신)이라 투표 집계 자체가 불필요 | 투표 집계 + 타임아웃(첫 인식 후 N초 내 전원 미달 시 초기화) |
| 효과 | 본인이 Q키로 고른 `Shield`/`SpeedUp` (기존 유지) | 전체 체력회복 **+2** (즉발, 지속시간 없음) |
| 쿨다운 | 개인별 (`cheerCooldownSeconds`) | 팀 공용 (`teamCheerCooldownSeconds`, 개인과 별개 — 값은 Inspector에서 추후 설정) |
| 대상 | 항상 자기 자신 (솔로/멀티 구분 없음 — 구 "솔로만 self 허용" 예외 폐기, 이제 기본 규칙) | `GameSession.ActivePlayerCount` 전원 |

### 1.3 두 개의 독립 시스템 (유지)

```
┌─ [① Dissonance] ─────────────────────────────────────┐
│  팀원 4인 ↔ 자유 대화 (Opus, NGO transport)           │
└──────────────────────────────────────────────────────┘

┌─ [② Vosk + CheerKeywordEngine] ────────────────────┐
│  각 Client: 자기 마이크만 분석                        │
│  → 로컬 grammar = [내 CheerName, TeamCheerWord] 2개  │
│  → 감지 시 SubmitSelfCheerServerRpc / SubmitTeamCheerServerRpc │
└──────────────────────────────────────────────────────┘

┌─ [③ CheerService (Host)] ────────────────────────────┐
│  개인: 즉시 발동/쿨. 팀: 투표·타임아웃·쿨·Heal 적용    │
└──────────────────────────────────────────────────────┘
```

**그래머 크기:** 클라이언트당 2단어(내 이름 + 팀워드)만 인식 대상. 구 방식(4명 전부 이름을 넣던 것)보다 오히려 후보가 줄어 인식률이 더 좋아진다 — cross-targeting이 없어져서 남의 이름을 들을 필요가 없기 때문.

---

## 2. 코어 규칙

### 2.1 개인 버프 (자기 응원)

| 규칙 | 내용 |
|------|------|
| 수혜자 | 항상 자기 자신 |
| 필요 인원 | 1 (자기 자신) — **투표 없음, 첫 인식 즉시 발동** |
| 버프 중 재트리거 | 무시 (표 자체가 없으므로 "쌓임" 개념 없음, 그냥 무시) |
| 쿨타임 중 재트리거 | 무시 |
| 사망 | 씬 리로드로 자동 초기화(`StageResetOnPlayerDeath`, 기존 동일) |

**쿨타임:** 버프 종료(`remainingTime == 0`) 순간부터 `cheerCooldownSeconds`(기존 15초 유지) 시작.

**버프 종류:** 기존 §1.4 버프 선택제 **그대로 유지** — `NetworkPlayerSetup.SelectedBuffType` + Q키 토글 + `RequestToggleBuffTypeServerRpc`. Shield/SpeedUp 모두 M/T 전 스테이지(Boss 포함)에서 자유 선택 가능. 이 문서에서 재변경 없음.

### 2.2 팀 버프 (팀 공용 키워드)

| 규칙 | 내용 |
|------|------|
| 수혜자 | 팀 전원 (그 순간 스폰돼 있는 활성 플레이어 전부) |
| 필요 인원 | `GameSession.ActivePlayerCount` (제외 없음 — 자기 자신도 포함해서 셈) |
| 타임아웃 | 첫 인식 후 `teamCheerTimeoutSeconds`(기본값, Inspector 노출) 내 전원 미달 시 표 전부 초기화 |
| 효과 | 전체 체력회복 **+2** — `NetworkDamageUtil.ApplyHeal` 경유, 즉발(지속시간 없음) |
| 쿨다운 | 팀 공용 1개, 개인 쿨다운과 완전히 별개 상태. `teamCheerCooldownSeconds`(Inspector 노출, **값은 사용자가 나중에 직접 설정** — placeholder만) |
| 발동 피드백 | 전원 화면에 짧게 **"Team Buff!"** 텍스트 배너 표시 (§8.2) |
| 솔로(1인) | `ActivePlayerCount==1`이면 자기 혼자 TeamCheerWord 1회로 발동 — 자연스럽게 축소, 별도 예외 코드 불필요 |

**개인 버프와 팀 버프는 서로 독립.** 같은 순간에 개인 버프 쿨타임 중이어도 팀 버프 투표/발동에는 영향 없음(그 반대도 마찬가지).

### 2.3 삭제된 것 (구 시스템 대비)

- **cross-targeting 전체** — 남의 이름을 외쳐서 그 사람에게 버프를 주는 기능 없음.
- 타겟 전환(`HandleTargetSwitch`), 응원자→타겟 매핑(`_cheererTarget`) — 개념 자체가 없어짐.
- "나를 제외한 전원" 공식(`max(1, ActivePlayerCount-1)`) — 개인 버프는 항상 1(자기 자신), 팀 버프는 `ActivePlayerCount`(제외 없음)로 대체.
- 숫자키 `3`, `4` — 더 이상 컬러 인덱스를 지목할 대상이 없으므로 제거. `1`=자기 응원, `2`=팀 응원만 남음.

---

## 3. CheerName & TeamCheerWord

### 3.1 CheerName (개인 호출명) — 기존 유지

| PlayerColorType | 기본 CheerName |
|-----------------|----------------|
| Blue | berry |
| Purple | guma |
| Green | sook |
| Yellow | dan |

- Tutorial 씬에서 각자 자유 입력·확정·재변경 (`PlayerCheerNameSync`, 잠금 없음, 세션 중 반복 가능).
- 형식/금칙어 검증: `CheerNameValidator` (길이 2~12, `a-z`/`0-9`/`_`, 예약어·블록리스트).
- **[신규]** 중복 검사 풀에 **TeamCheerWord도 포함** — 개인 이름이 현재 TeamCheerWord와 겹치면 거절 (§3.3).

### 3.2 TeamCheerWord (팀 공용 키워드) **[신규]**

| 항목 | 규칙 |
|---|---|
| 설정 주체 | **Host만.** 팀원 개별 설정 아님 — 팀 전체가 공유하는 단 하나의 값 |
| 기본값 | `"fighting"` (Host가 안 건드리면 이 값 그대로 사용, 기존 4개 CheerName과 발음상 안 겹침) |
| 설정 위치 | Tutorial CheerName 설정 구역(§9.2 zone 2, `CheerAndTutorialDesign.md`)에 Host 전용 입력 필드 추가. 비-Host 클라이언트는 현재 값을 **읽기 전용**으로 표시(뭘 외쳐야 하는지 알아야 하므로) |
| 검증 | `CheerNameValidator`(형식/금칙어) 그대로 재사용 |
| 충돌 검사 (양방향) | 설정 시 현재 확정된 CheerName들과 겹치면 거절 / CheerName을 나중에 그 값으로 바꾸면 마찬가지로 거절 (§3.3) |
| 구현 | `CheerService`에 `NetworkVariable<FixedString32Bytes> _teamCheerWord`(Server write, Everyone read) + Host-only setter. Host 프로세스는 곧 서버이므로 **RPC 불필요** — Host 클라이언트 UI가 `IsServer` 가드 걸린 public 메서드를 직접 호출. **단, 인스턴스 메서드라 그 씬에 `CheerService`가 실제로 배치돼 있어야 호출 가능** — Tutorial에서 Host가 설정하려면 Tutorial 씬에도 `CheerService`가 필요(§10 Phase D0) |
| 세션 지속 | `GameSession.SetSessionTeamCheerWord`/`GetSessionTeamCheerWord` (기존 `SetSessionCheerNames`와 동일 패턴) — `TutorialNetworkManager`의 게이트 완료 지점(기존 `SetSessionCheerNames` 호출부 2곳)에서 나란히 호출 |

### 3.3 양방향 충돌 검증

```
Host가 TeamCheerWord 설정 시도
  → PlayerCheerNameSync.GetAllEffectiveNames()로 현재 확정된 CheerName들과 비교
  → 겹치면 거절

플레이어가 CheerName (재)확정 시도
  → CheerService.Instance의 TeamCheerWord 값과 비교 (기존 IsTakenByOther 검사 풀에 추가)
  → 겹치면 거절
```

두 검증 다 Host 프로세스 내부에서 인스턴스 참조만으로 처리 — 새로운 RPC 경로 불필요.

### 3.4 Vosk 그래머 슬림화 **[변경]**

- 각 클라이언트 로컬 grammar = **[내 CheerName, TeamCheerWord, `[unk]`]** — 딱 2단어. 남의 CheerName은 더 이상 넣지 않음(구 방식은 cross-targeting 때문에 4명 전부 넣었음).
- 재빌드 트리거: **내 CheerName 변경** 또는 **TeamCheerWord 변경** 시에만 (구 "누구든 이름이 바뀌면 전체 재빌드"에서 축소).
- `PlayerCheerNameSync.RebuildOwnerLocalGrammar()`와 `CheerService._teamCheerWord.OnValueChanged` 양쪽 다 결국 "내 이름 + 현재 팀워드로 로컬 grammar 재적용"이라는 동일 동작이므로 작은 공용 헬퍼로 묶어서 양쪽에서 호출.
- Tutorial "말해보기" 테스트 grammar도 인게임과 동일하게 **[내 유효 CheerName, TeamCheerWord]** (`ApplyOwnerLocalGrammar`). 확정 전 입력 후보는 grammar에 넣지 않음(말해보기 전용 UI는 폐기됨).

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
| 모드 | **grammar** — 세션 CheerName(내 것) + TeamCheerWord + `[unk]`만 후보 (§3.4) |
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
[메인]  결과 drain → CheerName/TeamCheerWord 매칭 → SubmitSelfCheerServerRpc / SubmitTeamCheerServerRpc
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
  → 고정 4종(berry/guma/sook/dan)은 이미 전부 사전 등재 확인됨 (VariantMap 현재 빈 테이블)
  → 커스텀 이름/TeamCheerWord가 사전에 없으면 경고만, 대체 발음은 미지원(§5.3 C 참고)
```

`CheerLexiconBuilder.BuildGrammarJson(names)`가 원래 이름 + 등록된 대체 단어를 함께 grammar에 넣고, 인식 시 `ResolveVariant()`로 원래 이름으로 되돌린다.

### 5.3 C. 커스텀 이름 자동 대체 발음 — 설계만 확정, 미착수

Metaphone/Soundex류 발음 근사로 후보 제안하는 방식은 작업량이 커서 아직 미착수. 폴백: 사전 검증(A)만 적용, 없으면 경고만 표시.

---

## 6. 입력 — 음성 · 숫자키

### 6.1 음성 흐름

```
[Client Owner] 마이크 (Dissonance/Vosk 공유)
  → Vosk grammar = [내 CheerName, TeamCheerWord]
  → 내 CheerName 인식 → SubmitSelfCheerServerRpc(isVoice: true)
  → TeamCheerWord 인식 → SubmitTeamCheerServerRpc(isVoice: true)
```

### 6.2 숫자키 — 기본 비활성, 설정에서 켜기 **[변경]**

| 항목 | 규칙 |
|---|---|
| 매핑 | `1` = 자기 응원(self), `2` = 팀 응원(team). `3`/`4` 제거 |
| 기본 상태 | **비활성(OFF)** — 음성이 기본 응원 수단이므로 |
| 활성화 | 옵션(Options) 메뉴에서 토글 (`GameSettingsManager.DigitCheerEnabled`, PlayerPrefs 저장, 마이크 mute 토글과 동일 패턴) |
| 안내 | Tutorial CheerName/응원 체험 구역에서 "인식이 잘 안 되거나 마이크가 없으면 설정에서 숫자키 응원을 켜세요" 문구 안내 (자동 감지 팝업은 범위 밖 — 텍스트 안내만) |
| 구현 | `CheerDigitInput.Update()` 최상단에 `if (GameSettingsManager.Instance?.DigitCheerEnabled != true) return;` 가드. Rate limit(`chatRateLimitSeconds`)은 기존 그대로 |
| 서버 검증 | 음성과 동일 RPC 경로(`SubmitSelfCheerServerRpc`/`SubmitTeamCheerServerRpc`, `isVoice=false`)를 그대로 재사용 |

### 6.3 응원 주체

- 자기 CheerName은 **자기가 말함**. 다른 사람 목소리에서 이름을 찾을 필요 없음 (구조 동일, cross-targeting이 없어져서 오히려 더 확실해짐).

---

## 7. 네트워크 권한

### 7.1 아키텍처

```
[각 Client]
  Dissonance: 팀 보이스 송수신
  Vosk: 로컬 키워드(내 이름/팀워드)
  CheerDigitInput: 숫자키 1/2 (설정 ON일 때만)
  → SubmitSelfCheerServerRpc(isVoice) / SubmitTeamCheerServerRpc(isVoice)

[Host]
  CheerService:
    개인: sender 색 조회 → 즉시 버프 발동/쿨 체크
    팀: 가상 풀에 표 누적 → 타임아웃/쿨 체크 → 발동 시 전원 Heal
    → NetworkVariable / ClientRpc (UI, 버프·팀워드 미러링)
```

- 응원 판정용 음성은 서버로 스트리밍하지 않음. 팀 대화 음성은 Dissonance P2P.
- 게임 규칙(집계·버프·Heal) = **Host**.

### 7.2 RPC 구조 **[변경]**

| RPC | 방향 | 처리 |
|---|---|---|
| `SubmitSelfCheerServerRpc(bool isVoice)` | Client→Host | 서버가 `PlayerSpawnCoordinator.TryGetColor(senderId)`로 자기 색 조회 → 버프 중/쿨 중 아니면 즉시 `ApplyBuff` |
| `SubmitTeamCheerServerRpc(bool isVoice)` | Client→Host | 가상 "team" 풀에 표 추가 → `ActivePlayerCount` 충족 시 `ApplyTeamBuff`(전원 Heal) |
| `RequestToggleBuffTypeServerRpc` | Owner→Host | 기존 유지 (Shield/SpeedUp 선택) |
| `ApplyCheerBuffClientRpc` | Host→All | 기존 유지 |
| `BroadcastTeamBuffActivatedClientRpc` | Host→All | **신규** — "Team Buff!" 배너 트리거 |
| `BroadcastTeamVoteChangedClientRpc` | Host→All | **신규** — 팀 진행도 UI (누가 이미 외쳤는지) |

**삭제:** `SubmitCheerServerRpc(targetColorIndex, isVoice)`, `HandleTargetSwitch`, `_cheererTarget`, `GetCheererColorIndices`(개인 타겟용) — cross-targeting 제거로 불필요.

### 7.3 Heal 파이프라인 **[신규]**

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

- 개인: 버프 중/쿨 중 재요청 무시.
- 팀: 동일 클라이언트 중복 투표 무시(Set 기반), rate limit(숫자키).
- Host가 모든 최종 판정.

---

## 8. UI

### 8.1 개인 버프 — `CheerProgressUI` (기존 유지)

Idle(선택 아이콘) / BuffActive(fill) / Cooldown(숫자) 3상태, Q키 입력 통합. **변경 없음.**

### 8.2 팀 버프 UI **[신규]**

| 컴포넌트 | 역할 |
|---|---|
| **Team Buff! 배너** (신규) | 팀 버프 발동 시 화면에 2~3초 노출되는 텍스트. `CheerService.OnTeamBuffActivated` 구독 |
| `PlayerCheerHeartsUI` (역할 재정의) | 구: "나를 응원 중인 남들" 표시 → **신: "이 플레이어가 이번 팀워드 라운드에 이미 외쳤는지"**를 그 플레이어 자기 머리 위에 하트 1개 온/오프로 표시 |
| `TeamStatusUI` (숫자키 아이콘 자리 교체) | 구: 팀원별 숫자키(1~4) 아이콘 → **신: 팀워드 진행도**(그 팀원이 이미 외쳤는지 체크마크) |
| `PlayerNameTagUI` | 본인 "지금 응원 중인 대상" 표시 제거 (더 이상 타겟 개념 없음) |

### 8.3 TeamCheerWord 설정 UI **[신규]**

Tutorial CheerName 설정 구역(`TutorialCheerNameUI`, `CheerAndTutorialDesign.md` §2 zone 2)에 병합:

- Host: 입력 필드 + 확정 버튼 (개인 CheerName과 같은 패널에 별도 섹션)
- 비-Host: 현재 TeamCheerWord 값을 읽기 전용으로 표시

### 8.4 숫자키 옵션 토글 **[신규]**

`OptionsMenuController`에 마이크 mute 토글과 동일한 방식으로 "숫자키로 응원하기" 체크박스 추가.

---

## 9. Inspector 파라미터

| 파라미터 | 위치 | 설명 | 값 |
|---|---|---|---|
| `PlayerBuffSystem.buffSettings[type].duration` | `PlayerBuffSystem` | Shield/SpeedUp 지속 | Shield 5초 / SpeedUp 10초 (기존 유지) |
| `cheerCooldownSeconds` | `CheerService` | 개인 버프 종료 후 쿨 | 15초 (기존 유지) |
| `teamCheerCooldownSeconds` | `CheerService` | **[신규]** 팀 버프 종료 후 쿨 | placeholder — 사용자가 추후 설정 |
| `teamCheerTimeoutSeconds` | `CheerService` | **[신규]** 팀 첫 인식 후 전원 미달 타임아웃 | placeholder — 기본 10초 정도로 시작, 튜닝 여지 |
| `chatRateLimitSeconds` | `CheerService` | 숫자키 응원 간격 | 0.5~1초 (기존 유지) |
| `teamHealAmount` | `CheerService` | **[신규]** 팀 버프 체력회복량 | 2 (heart 단위) |

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
| C1 | 머리 위 하트 = "이번 팀워드 라운드에 이미 외쳤는지" | `PlayerCheerHeartsUI` |
| C2 | 죽은 숫자키 아이콘 슬롯 제거(교체 아이콘 없음 — 사용자 결정 2026-09-01, 팀워드 진행도는 C1 머리 위 하트로만) | `TeamStatusUI` |
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
- `CheerService` Inspector: 팀 쿨 / 타임아웃(시작 10초) / Heal 2
- Options 체크박스, Team Buff 배너 연결
- Tutorial CheerName 패널: Host 팀워드 입력 필드·확정 버튼·현재값 텍스트 연결 (D1)
- Tutorial 구역 3 체험 재배치 (D4)
- 숫자키 안내 문구 배치 (D3 대체 — 씬 텍스트 1회)

테스트:

- ParrelSync 2인: Host 팀워드 설정, 전원 외침, Heal, 쿨/타임아웃
- Steam 2인·4인은 Tutorial 문서 출시 게이트 — Phase D 이후

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

### 한 줄 계약 (이미 살아 있음)

```
숫자키 1 / 내 CheerName 인식 → SubmitSelfCheerServerRpc → Host 즉시 개인 버프/쿨
숫자키 2 / TeamCheerWord 인식 → SubmitTeamCheerServerRpc → Host 팀 투표·타임아웃·전원 Heal
```

구 `SubmitCheerServerRpc(targetColorIndex)` / `_cheererTarget` / `HandleTargetSwitch` **삭제됨. 부활 금지.**

### 파일별 구현 (Phase A가 남긴 실제 API)

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

- `SubmitSelfCheerServerRpc(bool isVoice)` — sender 색 → `ValidateSelfCheer` → `ApplyBuff`
- `SubmitTeamCheerServerRpc(bool isVoice)` — `ValidateTeamCheer` → `_teamVotes` HashSet → 충족 시 `ApplyTeamBuff`
- `TrySetTeamCheerWord(string, out reason)` — Host(`IsServer`)만. 실패: `"format"` / `"reserved"` / `"blocked"` / `"taken"` / `"not_server"`. RPC 없음
- `MatchesTeamCheerWord(string lower)` / `TeamCheerWord` 프로퍼티
- `_teamCheerWord` NV: Server write, Everyone read, 기본 `"fighting"`
- `OnNetworkSpawn`: 전원 `_teamCheerWord.OnValueChanged` 구독 + `RebuildOwnerLocalGrammar`. Host만 `HasSessionTeamCheerWord`면 세션값을 NV에 복사
- Inspector: `teamCheerCooldownSeconds`(15 placeholder), `teamCheerTimeoutSeconds`(10), `teamHealAmount`(2) — **값 튜닝은 사용자**

Host 내부:

- 개인: `_buffEnd` / `_cooldownEnd` (colorIndex), `_chatRateEnd` (clientId, 숫자키만, 음성은 rate skip)
- 팀: `_teamVotes`, `_teamTimeoutStart`, `_teamCooldownEnd` — 개인 쿨과 **독립**
- `GetRequiredTeamVotes()` = `max(1, ActivePlayerCount)` (폴백: `ConnectedClientsIds.Count`)
- `ApplyTeamBuff`: 세션 활성 색 `NetworkPlayerSetup`에 `NetworkDamageUtil.ApplyHeal`
- `ResetTeamVotes`: `IsSpawned`일 때만 ClientRpc (스폰 전 호출 가드)

UI 이벤트:

| 이벤트 | 발행 | 구독 현황 |
|---|---|---|
| `OnBuffActivated` / `OnCooldownStart` | ClientRpc → 로컬 이벤트. 개인 버프 HUD | `CheerProgressUI` (유지, 손대지 않음) |
| `OnTeamBuffActivated` | 팀 Heal 직후 ClientRpc | `TeamBuffBannerUI` (§10.3). **GO 미배치면 배너만 없음** |
| `OnTeamVoteChanged(current, required, voterColorIndices)` | 표 추가/리셋 때 ClientRpc | `PlayerCheerHeartsUI` / `TeamStatusUI` (§10.3) |

### 리뷰 결정 (이미 반영)

1. **Tutorial TeamCheerWord 경로** = CheerService를 Tutorial 씬에도 배치 (신규 RPC 안 만듦). **사용자 에디터 D0.** 코드는 배치만 되면 `TrySetTeamCheerWord` + 게이트 `SetSessionTeamCheerWord` + 스테이지 `OnNetworkSpawn` 복원으로 닫힘.
2. **PlayerHPUI 구독 해제** = 지금 고침 (델리게이트 필드). 완료.
3. **죽은 이벤트** = Phase C에서 삭제 완료. `OnVoteChanged` / `OnVoteReset` / `OnCheerersChanged` **부활 금지.**

### 알려진 한계 (버그로 착각하지 말 것)

- Tutorial에 `CheerService` 없음 → Host가 팀워드를 못 바꿈, CheerName 충돌은 `"fighting"`만 검사. **D0 전 정상.** *(D0 완료 — Tutorial 씬에 배치됨)*
- `CheerKeywordEngine.ParseAndSubmit`에 `_lastDetected[word] = Time.time`이 두 분기에 있음 (실행은 상호배타). 스타일만, 동작 버그 아님.
- `ApplyBuff`/`ApplyTeamBuff`의 `FindObjectsByType<NetworkPlayerSetup>`는 구 패턴 유지. 인원 최대 4. 새로 바꾸지 말 것.
- Options `digitCheerToggle` 미연결이면 숫자키 설정 UI가 안 보임. API·가드는 동작(기본 OFF). **사용자 에디터.**
- `TeamBuffBannerUI` GO 미배치면 배너만 없음 (Heal은 적용). **사용자 에디터.**
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

로컬 Vosk grammar = **[내 유효 CheerName, TeamCheerWord]**. 재빌드 = 내 이름 변경 또는 TeamCheerWord NV 변경만.

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

팀워드 투표 중 → 머리 위 하트 1개(코너 패널엔 표시 없음). 발동 → "Team Buff!" 배너. 타겟 개념/구 투표 이벤트 없음.

### 파일별

| 파일 | 무엇을 넣었나 | 다음 에이전트가 알 것 |
|---|---|---|
| `PlayerCheerHeartsUI.cs` | `OnTeamVoteChanged` — 자기 colorIndex가 voter에 있으면 자기 색 하트 1개 ON | 구 "누가 나를 응원 중" 하트 여러 개 **부활 금지**. 타임아웃/발동은 빈 voter 배열로 OFF. 팀워드 진행도를 보여주는 **유일한** UI(코너 패널엔 없음) |
| `TeamStatusUI.cs` | `keyIconSprites`(죽은 숫자키 3/4 아이콘) 삭제, 대체 아이콘 없음. 이름+HP 하트만 | **사용자 결정(2026-09-01): 코너 패널에 팀워드 체크 아이콘 추가하지 않음.** `CheerService.OnTeamVoteChanged` 구독 **부활 금지** — 팀워드 진행도는 오직 `PlayerCheerHeartsUI`(머리 위)로만 표시 |
| `PlayerNameTagUI.cs` | 로컬 오너 "응원 대상" 분기 삭제. 타인 CheerName만 | `hideForLocalOwner` 유지. 타겟 텍스트 슬롯 **부활 금지** |
| `TeamBuffBannerUI.cs` | 신규. `OnTeamBuffActivated` → 2.5초 페이드 배너 (`StageClearBannerUI` 패턴) | **Canvas 아래 빈 GO 배치는 사용자.** 미배치면 배너만 없음 |
| `CheerService.cs` | `OnVoteChanged` / `OnVoteReset` / `OnCheerersChanged` 삭제 | 구 이벤트 **부활 금지.** RPC 재작성 금지 |

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
- Options `digitCheerToggle`, `TeamBuffBannerUI` GO (Phase B/C 잔여)

### 결정

- **D3 제외** (사용자 2026-09-01): 숫자키 안내는 Tutorial 씬에서 1회 설명. `TutorialCheerNameUI`에 안내 문자열 넣지 말 것.
- **D0 완료**: Tutorial 씬에 `CheerService`+`NetworkObject` 이미 있음.

---

## 11. 구현 체크리스트

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

---

## 12. 관련 코드

| 항목 | 경로 |
|------|------|
| 응원 코어 | `Assets/Scripts/Cheer/CheerService.cs` |
| 키워드 인식 | `Assets/Scripts/Cheer/CheerKeywordEngine.cs` |
| Grammar 빌더 | `Assets/Scripts/Cheer/CheerLexiconBuilder.cs` |
| 이름 검증 | `Assets/Scripts/Cheer/CheerNameValidator.cs` |
| 개인 이름 동기화 | `Assets/Scripts/Cheer/PlayerCheerNameSync.cs` |
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
| 이름표 UI | `Assets/Scripts/UI/PlayerNameTagUI.cs` |
| 팀 버프 배너 | `Assets/Scripts/UI/TeamBuffBannerUI.cs` |
| Tutorial 이름 설정 UI | `Assets/Scripts/UI/TutorialCheerNameUI.cs` |
| Tutorial 네트워크 | `Assets/Scripts/Network/TutorialNetworkManager.cs` |

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
A. **아니오.** 음성이 기본이라 기본 꺼짐. 옵션에서 켜야 `1`(self)/`2`(team)가 동작.

**Q. 그래머 단어 수가 줄어드는 게 맞나?**
A. 맞다. 구 방식은 클라이언트마다 팀원 4명 이름을 전부 grammar에 넣었지만(cross-targeting 때문), 이제는 **내 이름 + 팀워드 2개뿐**이라 오히려 인식 후보가 줄어 인식률이 더 좋아진다.
