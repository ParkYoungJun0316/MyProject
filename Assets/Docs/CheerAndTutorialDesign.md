# Cheer System & Tutorial Design

음성·채팅 **응원 시스템**, **인게임 보이스챗**, **정식 Tutorial** 설계 문서.  
관련: [`NetworkDesign.md`](NetworkDesign.md) (네트워크 검증 단계·Host 권한·출시 달력).

**범례**

| 태그 | 의미 |
|------|------|
| **[Ship Must]** | **2026-09-01 정식 출시** 전 필수 |
| **[Post-Launch]** | 정식 이후 (관전 후보 등). **컷씬은 영구 제외** |

> 구 Open Must / Release Must / Playtest 이원화는 **폐기** (`ReleaseRoadmap.md`).

---

## 0. 정식 출시 — 범위 요약

| 항목 | **[Ship Must]** |
|------|-----------------|
| 씬 흐름 | Title → **Tutorial** → M1…5→M.Boss → T1…5→T.Boss → End.Demo (`1.Lobby` 폐지, 2026-08-17. SSOT: `NetworkDesign.md` §6B) |
| Tutorial 씬 | **필수 경로** (연습 구간은 경험자 생략 가능). **구 Lobby 역할(색 배정·Invite·Start) 흡수** — §9 |
| **인게임 보이스챗** | **Dissonance + NGO** (4인 Global, Voice Activation). Steam 시 **Dissonance Steam P2P** transport 검토 |
| CheerName | **Tutorial 씬 커스텀** (§3). 빈칸 = 색 기본값 |
| 이름 커스텀 | **Tutorial 자유 입력·자유 재변경** (Player별 `NetworkVariable`) + Host 검증·확정. 잠금 없음, `PlayerPrefs` 기억 없음(매 판 재입력) |
| **말해보기** | **Must** — Tutorial에서 확정↔재변경 반복 가능, 매 확정마다 Vosk grammar 재빌드 → 즉시 재테스트 (§5.5) |
| **키워드 인식** | **Vosk grammar** + **CheerLexiconBuilder** (사전 검증 + 발음 변형 대체 단어, §5) |
| 채팅 응원 | `/cheer {name}` **필수 폴백** |
| 스테이지 버프 | M = **Shield** (`Invincibility`), T = **SpeedUp** + **응원 확장 2종** (출시 범위) |
| 인게임 설명 | Tutorial(핵심 메카) + **DialogueUI** (M/T 구역별) |
| **멀티 연결** | **Steam P2P + Lobby** (`NetworkDesign` ④) |
| **목표** | **2026-09-01** 원격 협동 + 보이스 + 응원 + Tutorial (텔레메트리는 출시 후 OK) |
| **개발자 테스트** | PC **2대** → Steam **2인** Must; **4인 1회** 권장 |
| 음성 인식 정확도 | 100% 불필요. **Tutorial 말해보기(확정↔재변경 자유 반복, 인식률 검증)**로 확인 |

> **데모 / Playtest 없음.** 원격 IP Join / UDP discovery **미사용**. 개발=ParrelSync·localhost, 배포=Steam (`ReleaseRoadmap.md` §3).

---

## 1. 응원 시스템 — 개요

### 1.1 플레이 UX (확정)

플레이 중 **팀원과 자유롭게 대화** (인게임 보이스). 버프가 필요할 때 대화 속에서 팀원의 **CheerName**을 외치면 응원 1표.

예: `give me buff!` → `berry go go!!` → `thank you friends~`

- **조용히 단어만** 말하는 UX **아님**.
- **외부 Discord 앱**에 의존하지 **않음** (인게임 보이스 Must).

### 1.2 응원 규칙 (한 줄)

플레이어가 **팀원의 CheerName**을 **음성 또는 채팅**으로 외치면 응원 1표.  
**나를 제외한 전원**이 같은 수혜자를 응원하면 **팀 버프** 발동.

| 스테이지 | 버프 | `PlayerBuffSystem` |
|----------|------|---------------------|
| M.Stage1 | Shield | `Invincibility` |
| T.Stage1 | SpeedUp | `SpeedUp` |

### 1.3 두 개의 독립 시스템

```
┌─ [① Dissonance] ─────────────────────────────────────┐
│  팀원 4인 ↔ 자유 대화 (Opus, NGO transport)           │
│  → 다른 플레이어에게 "들리게"                         │
└──────────────────────────────────────────────────────┘

┌─ [② Vosk + CheerKeywordEngine] ────────────────────┐
│  각 Client: 자기 마이크만 분석                        │
│  → 세션 CheerName 3~4개 grammar + lexicon            │
│  → 감지 시 SubmitCheerServerRpc (음성은 서버 미전송)  │
└──────────────────────────────────────────────────────┘

┌─ [③ CheerService (Host)] ────────────────────────────┐
│  집계 · 타임아웃 · 쿨 · 버프 · UI 동기화               │
└──────────────────────────────────────────────────────┘
```

**규모 오해 방지:** 전 세계 50세션 × 4명 = 200개 이름이 **한 PC에서 200개를 동시 인식**하는 구조가 **아님**.  
각 클라이언트는 **현재 세션(Tutorial에서 확정된) CheerName 3~4개만** grammar/lexicon에 넣는다.

---

## 2. 응원 코어 규칙

### 2.1 발동 조건

| 규칙 | 내용 |
|------|------|
| 수혜자 | **자기 자신**. 자기 자신에게 응원 불가 |
| 필요 응원 수 | `max(1, ActivePlayerCount - 1)`. 1인(`ActivePlayerCount==1`)이면 자기 자신 응원 허용 |
| 응원자 동시 타겟 | **1명만**. 타겟 변경 시 **이전 타겟 집계 -1** |
| 동시 수혜 | **가능**. 수혜자별 독립 |
| 갱신 | **없음**. 버프 중 시간·수치 연장 불가 |

**인원별 필요 응원 수**

| 접속 인원 | 수혜자 1명당 필요 응원 |
|-----------|------------------------|
| 1 (솔로) | 1 (자기 자신 응원 허용 — `ValidateCheer` 예외) |
| 2 | 1 |
| 3 | 2 |
| 4 | 3 |

### 2.2 수혜자 상태별 처리

| 상태 | 응원 표 | 버프 발동 |
|------|---------|-----------|
| **정상** | 쌓임 | 조건 충족 시 발동 |
| **버프 중** | **쌓지 않음** (입력 차단) | 불가 |
| **쿨타임 중** (버프 종료 후) | 쌓임 | 쿨 종료 전까지 **발동만** 불가 |
| **사망** | **진행 초기화** | 불가 |

- **응원자 사망:** `StageResetOnPlayerDeath` → 씬 리로드 → 응원 상태 **자동 초기화**.

### 2.3 쿨타임

- **대상:** 수혜자 **개인**
- **시작:** 버프 `remainingTime` = **0** 순간
- **길이:** Inspector `cheerCooldownSeconds` (예: 15초)

### 2.4 타임아웃 (부분 응원)

- **조건:** 수혜자에게 **첫 표** 시점부터 **N초** (기본 **10초**)
- **동작:** 필요 수 미달 → 해당 수혜자 응원 표 **전부 초기화**

### 2.5 응원자 쿨타임

- **없음.** 타겟 1명 + 타임아웃 + (채팅) rate limit으로 충분.
- 채팅 **rate limit:** 0.5~1초, Host (스팸 방지).

### 2.6 솔로 (1인 Host, `ActivePlayerCount == 1`)

- **NGO Host 1인.** 멀티와 동일 코드 경로.
- **응원:** `CheerService.ValidateCheer` — `ActivePlayerCount==1`이면 self-cheer 허용. `GetRequiredVotes()=1` → 즉시 발동.
- **음성:** `CheerKeywordEngine` → `SubmitCheerServerRpc` (멀티와 동일).
- **채팅:** `/cheer {자기 CheerName}` → `InGameChatUI` → `SubmitCheerServerRpc`.
- **인게임 보이스:** 솔로 시 **비활성** 권장 (팀원 없음). Dissonance mute는 별도 구현.

---

## 3. CheerName (호출명)

### 3.1 기본값 (색상별)

| PlayerColorType | 기본 CheerName |
|-----------------|----------------|
| Blue | berry |
| Purple | guma |
| Green | sook |
| Yellow | hobak |

저장·비교 시 **소문자 통일**.

**빈칸 = 기본값 취급 (확정):**  
Tutorial에서 확정한 CheerName(Player별 `NetworkVariable`)이 **빈 문자열**이면 저장값으로 기본명을 넣지 않는다.  
표시·`/cheer`·Vosk grammar·버프 대상은 **현재 `ColorIndex`의 기본 CheerName**으로 해석한다.

### 3.2 어디에 설정하나 **[Ship Must — 2026-08-17 갱신: Lobby → Tutorial]**

> **변경 배경 (2026-08-17 오전):** 기존엔 Lobby 슬롯 인라인 편집으로 확정하고 Tutorial은 반복 인식 검증(말해보기)만 담당했으나, **입력·확정·말해보기 전부를 Tutorial 씬 하나로 통합**했다. 당시엔 Lobby가 색/캐릭터 선택 + Ready + Kick만 담당하는 것으로 정리.
>
> **추가 변경 (2026-08-17 저녁, 더 큰 결정) — `1.Lobby` 씬 자체를 폐지.** 색 선택·Ready·Kick·Steam Invite까지 전부 `Tutorial` 앞부분(사전 게이트 구간)으로 흡수됐다. 즉 이제 "Lobby가 없다"가 아니라 **"Lobby가 Tutorial의 일부가 됐다"**. SSOT는 `NetworkDesign.md` §6B, 이 문서 §9.

- **씬:** `Tutorial` 하나. 별도 로비 씬 없음.
- **대상:** Tutorial 진입(=접속) 즉시 스폰되어 있는 **Player별로 독립된 이름** — "슬롯" 개념 없음. 각자 자기 화면에서 자기 캐릭터의 이름만 입력.
- **UI:** Tutorial 전용 이름 입력 UI(신규, §8.3) — 로컬 입력창 1개. 확정 시 자기 캐릭터 머리 위 이름표(`PlayerNameTagUI`)·팀원 화면에 **즉시** 반영.
- **채팅 UI로 닉네임 설정하지 않음.** 인게임 `/cheer` 폴백만 채팅.
- **타이틀에서 입력하지 않음.**
- **`PlayerPrefs` 기억 없음 (2026-08-17 확정, 폐기).** 매 판 Tutorial 진입 시 항상 새로 입력. "경험자 스킵" 없음 — 과잉 설계로 판단, 드롭.

#### 확정 → 말해보기 → 재변경 (자유 반복) **[Ship Must]**

로비의 "최초 1회 등록 확인"과 Tutorial의 "반복 검증" 이원화는 **폐기**. Tutorial 하나에서 다음 루프를 몇 번이든 반복할 수 있다.

```
입력 → Enter(확정 제출) → Host 검증 → 통과 시 전원에 즉시 반영 + Vosk grammar 재빌드
  → 말해보기(육안·청각 확인) → 마음에 안 들면 다시 입력 → …(반복)
```

| 항목 | 규칙 |
|------|------|
| 잠금 | **없음.** Ready 같은 상태가 Tutorial엔 없으므로 언제든 재확정 가능 |
| "최종 확정" | 별도 단계 없음 — **`TutorialGatherZone` 통과 시점의 값이 곧 최종값** (§9.4) |
| 강제 | 말해보기 실패해도 진행 가능. 안내만 |
| 저장 | 녹음·lexicon 학습 없음. 검증 UI만 |
| 숫자 테스트 | `b_4nana` 등 §3.5 잠정 숫자 — 실패율 높으면 §3.5에서 `0-9` 제거로 갱신 |

> **2026-08-07 Dev Build 2인 테스트에서 발견된 "로비 최초 1회만 인식, 재시도 시 인식 안 됨" 증상**은 이 구조 변경으로 **자연 해소**된다(로비에 이름 관련 로직이 없어지므로). 재조사 불필요.

### 3.3 소유·색 변경 (확정)

- CheerName은 **색/캐릭터가 아니라 플레이어(슬롯)** 에 붙는다.
- 색을 Blue → Purple로 바꿔도 **커스텀 문자열은 플레이어를 따라간다.**
- Blue 슬롯이 “berry를 회수”하지 않는다. Blue인 **다른** 플레이어가 빈칸이면 그때만 `berry`.
- 커스텀을 지우고 확정(또는 빈칸 유지) → 다시 **현재 색 기본값** 취급.

### 3.4 확정·동시성 — 자유 재변경 (2026-08-17 갱신)

> Tutorial엔 Ready/Start 같은 잠금식 게이트가 없다. 이름은 **`TutorialGatherZone` 통과 전까지 몇 번이든 재확정 가능** — "Ready 중 잠금" 개념 자체가 없다.

| 단계 | 규칙 |
|------|------|
| 타이핑 중 | **로컬만.** 남에게 중간 글자 동기화 안 함. 확정 전까지 내 화면에만 보임 |
| Enter / 확정 | Client → **ServerRpc** → **Host 최종 검증** 후 통과 시 해당 Player의 `NetworkVariable`에 반영 → 전원 즉시 표시 갱신 |
| 동시 확정 | **먼저 처리된 Rpc 승.** 나중 동일/위반 이름은 **거절**. UI: 치던 글자 유지 + 에러(테두리/짧은 문구) |
| 재확정 | **언제든 가능.** 확정 후에도 다시 입력 → 다시 Enter로 재제출 가능(잠금 없음) |
| 빈칸 | **자동으로 `berry` 등을 문자열로 넣지 않음.** 빈칸 = §3.1 기본값 취급 |
| 최종값 | 별도 "확정 완료" 단계 없음 — **`TutorialGatherZone` 통과 시점**의 각자 최신값이 그대로 세션 최종값 |

해석 후 유일: 빈칸 플레이어의 유효 이름은 `ColorIndex` 기본값.  
예: A가 Blue+빈칸(`berry`), B가 `berry` 커스텀 확정 시도 → Host 거절.

### 3.5 검증·차단 리스트

클라 1차 → **Host 최종**. 실패 시 슬롯값 변경 없음.

#### 형식 **[Ship Must]**

| # | 규칙 | 값 / 비고 |
|---|------|-----------|
| 1 | 길이 | **2 ~ 12** (확정 시에만). 빈칸은 길이 검사 생략 → 기본값 취급 |
| 2 | 대소문자 | 구분 없음. **저장·비교는 소문자** |
| 3 | 허용 문자 | **`a-z`, `0-9`, `_`** — 한글·공백·이모지·기타 기호 **불가** |
| 4 | 공백 | trim 후 빈칸이면 커스텀 없음(기본값 취급) |

**숫자 (`0-9`) — 잠정 허용:**  
`b_4nana` 등이 Vosk grammar/G2P에서 인식되는지 **플레이테스트 후** 숫자 금지로  Tight할 수 있다.  
테스트 전제: 숫자 포함 이름 2~3종을 Dev Build 2인에서 외쳐 보기 → 실패율 높으면 §3.5에서 `0-9` 제거로 Docs 갱신.

#### 세션·시스템 **[Ship Must]**

| # | 규칙 |
|---|------|
| 5 | 같은 게임 세션(Tutorial에 스폰된 현재 플레이어들) 안에서 **해석 후 CheerName 중복 불가** |
| 6 | ~~Ready 중 이름 변경 불가~~ — **폐기 (2026-08-17).** Tutorial엔 Ready 잠금이 없음 (§3.4) |
| 7 | ~~`CanStart`에 이름 유일 포함~~ — **폐기 (2026-08-17).** 대신 매 확정 시마다 현재 활성 플레이어 기준 중복 검사 (§3.4) |
| 8 | 예약어 불가: `cheer`, `admin`, `host`, `server`, `system`, `bot`, `null` 등 |

> **구현 갭 노트 갱신 (2026-08-17):** 2026-08-05에 기록된 "미점유 슬롯의 기본 이름은 중복 검사 대상 아님" 갭은 **Lobby 구현(`LobbyNetworkManager.SetCheerNameServerRpc`, `_slots` 순회) 전제**였음. Lobby에서 CheerName 로직이 제거되므로 이 갭은 자동 소멸. Tutorial 신규 구현 시 동일한 함정(미접속/미확정 색의 기본 이름을 중복 검사에서 빠뜨리는 것)을 다시 만들지 않도록 확인만 하고, 별도 "버그"로 재조사하지 말 것.

#### 금칙어 **[Ship Must]**

| # | 규칙 | 범위 |
|---|------|------|
| 9 | 욕설 | 영문 공통 비속어 blocklist |
| 10 | 성·체 관련 | sexual / body slur |
| 11 | 혐오·차별 | 최소 목록 |
| 12 | 초간단 우회 | `f4ck`, `a$$` 등 — 완벽 필터 불필요 |

공개 영문 blocklist 파일 + Host 재검증. AI 필터 없음.

#### 정식에서 강화 **[Ship Must]**

| # | 규칙 |
|---|------|
| 13 | 발음 유사 (`bac` / `bek`) 경고 또는 차단 |
| 14 | Tutorial 연습 맵 + 말해보기 — 입력·확정·**반복 인식·인식률 최종 검증**을 Tutorial 하나로 통합 (2026-08-17, §5.5) |
| 15 | ~~`PlayerPrefs`로 로컬 이름 기억 · 경험자 Tutorial 이름 UI 생략~~ — **폐기 (2026-08-17).** 매 판 새로 입력 |

### 3.6 **[Ship Must]** Tutorial과의 관계

- **⭐ 결정 (2026-08-17, Lobby 방식 완전 대체):** CheerName **입력 + 확정 + 재변경 + 말해보기 전부**를 `Tutorial` 씬 하나로 통합. **`1.Lobby` 씬 자체가 이후 완전히 폐지**됐으므로(§9, `NetworkDesign.md` §6B) 이 문장은 이제 자명함 — CheerName UI·네트워크 로직은 원래부터 로비라는 곳이 없음.
- `Tutorial`은 **사전 게이트 구간(구 Lobby 흡수) + 조작 연습 + CheerName 입력·확정·말해보기(자유 반복)** 중심.
- 인게임(M/T 스테이지 진입 후) 이름 변경 = **Post-Launch** (Tutorial 안에서의 자유 재변경과는 별개 — Tutorial 종료 후엔 그대로 잠김).
- `PlayerPrefs` 기반 경험자 스킵은 **완전 폐기 (2026-08-17)**. `TutorialCompleted` 플래그(연습 구간 Stealth/색 패드 스킵용)는 CheerName과 무관하게 그대로 유지 — 세션 정책은 `NetworkDesign.md` §12.

### 3.7 발음·인식 정책 (확정)

- **100% 정확 발음 강제 아님.** `back` / `bac` / `bek` / `bec` 등 **비슷한 소리**면 같은 CheerName으로 잡혀도 OK.
- **정확한 철자 발음만** 허용하는 구조 **아님** — grammar에 **사전 검증된 발음 변형 대체 단어 여러 개**(§5.2 B) 포함.
- 한국어 STT = Post-Launch. MVP = **로마字 CheerName + 영어 Vosk 모델**.
- **음성으로 뭔가 “학습·저장”하지 않음.** grammar는 코드 테이블(§5.2)에서 즉시 생성. Tutorial **말해보기** = **검증**만.

---

## 4. 음성 스택 — Dissonance + Vosk

### 4.1 인게임 보이스챗 — Dissonance **[Ship Must]**

| 항목 | 선택 |
|------|------|
| 패키지 | **Dissonance Voice Chat** + **Dissonance for Netcode for GameObjects** |
| 비용 | Asset Store 일회 (~$55 세일 시) |
| 역할 | 4인 **자유 대화** |
| 설정 | **Global** room, **Voice Activation** (말할 때 전송) |
| 배치 | Lobby→Stage DDoL 또는 M/T 씬 `DissonanceSetup` + `NfgoCommsNetwork` |
| NGO | 게임 상태와 **병행**. 음성은 Dissonance transport, 규칙은 NGO Host |

**[Ship Must] Steam 직전:** Dissonance **Steamworks P2P** 음성 transport 분리 검토 (개발기는 NGO transport로 충분).

### 4.2 키워드 인식 — Vosk **[Ship Must]**

| 항목 | 내용 |
|------|------|
| 종류 | **오픈소스** STT (Apache 2.0). Asset Store 유료 아님 |
| 연동 | GitHub `alphacep/vosk-unity-asr` + 영어 모델 `vosk-model-en-us-0.22-lgraph` (**204MB / 17파일, 압축 해제된 폴더 그대로 `StreamingAssets`에 포함**) |
| 모드 | **grammar** — 세션 CheerName + `[unk]` 만 후보 |
| 비용 | **$0**, MAU 무제한 (클라이언트 로컬 처리) |
| 서버 | 음성·lexicon **서버 저장 없음** |

**Porcupine / Azure:** 상용·과금·커스텀 파이프라인 부담 → **본 프로젝트 기본 선택 아님**. Post-Launch 검토만.

### 4.3 마이크 공유 **[Ship Must · 코드 확정]**

Dissonance와 Vosk가 **동일 마이크**를 쓰되, OS `Microphone.Start` **이중 오픈 금지**.

| 모드 | 캡처 경로 |
|------|-----------|
| **멀티 (NGO)** | Dissonance만 마이크 소유 → `CheerKeywordEngine`이 `SubscribeToRecordedAudio` / 구독자로 **PCM tap** |
| **솔로** | Dissonance가 오디오를 안 줄 때만 `Microphone.Start` **fallback** |

**과거 사고:** 멀티에서 Dissonance + 직접 `Microphone.Start` 동시 → 버퍼 오버런·오디오 스레드 경합이 메인 스톨(0.3~0.4s)로 번짐 → NGO 스폰 Deferred/유실. **재발 금지.**

### 4.4 스레드 구조 **[Ship Must · 코드 확정]**

꿀떡은 “보이스 채팅 + 로컬 STT로 버프 트리거”라 일반 보이스 전용 게임과 다름. 인식 부하는 메인에서 빼야 함.

```
[메인]  Dissonance(또는 솔로 마이크) PCM 캡처
        → float→short → _pcmQueue
[워커]  VoskWorker: AcceptWaveform → JSON → _resultQueue
[메인]  결과 drain → CheerName 매칭 → SubmitCheerServerRpc / Unity·NGO API
```

| 항목 | 위치 | 비고 |
|------|------|------|
| `AcceptWaveform` (Vosk 인식) | **백그라운드 워커** | 메인에서 돌리면 프레임 히치 |
| `VoskModelLoader.LoadSync` | **메인 동기** (Tutorial 진입 1회) | Tutorial 진입 순간 히치 **가능** — 감수 또는 추후 비동기 |
| Cheer 제출 / UI | **메인만** | |

### 4.5 Dissonance 버퍼 경고 — 원인 · 결론 · 해결 방향

콘솔에 보이는 예:

- `BasicMicrophoneCapture: Insufficient buffer space … (dropping N samples)`
- `BasePreprocessingPipeline: Lost … samples … (buffer full), injecting silence`

**성격:** **Warn(경고)**. 크래시 아님. 마이크 샘플을 제때 못 빼서 **일부를 버리고 무음으로 메움**.

#### 원인 정리 (확정)

| | 무엇이 넘침 | 직접 원인 |
|--|-------------|-----------|
| **위 Dissonance 로그** | Dissonance **마이크 캡처 버퍼** | **메인 히치** — `Update`/`DrainMicSamples`가 밀리는 동안 OS 마이크만 쌓임 → 한 번에 너무 많이 빼려다 clamp/drop |
| **Vosk 쪽 (별개)** | `CheerKeywordEngine` `_pcmQueue` | 워커가 **큰 청크 통째** `AcceptWaveform` → 큐 적체 → 가득 차면 Enqueue drop → **인식률** 하락 |

워커가 느려도 Dissonance를 **직접 블로킹하지는 않음** (`ConcurrentQueue`).  
다만 메인 `ProcessAudio`에서 큰 덩어리 리샘플·할당이 히치에 **기여**할 수 있고, 그때 Dissonance Warn과 Vosk 큐 밀림이 **같이** 보일 수 있음.

**메인 히치:** 메인 스레드가 수십~수백 ms 동안 다른 일(동기 모델 로드, 씬/스폰, GC, 에디터+빌드 부하 등)에 묶여 프레임/`Update`가 안 도는 것.

**결론:**  
- **지금 Dissonance 경고의 1순위 원인 = 메인 히치**  
- **청크 크기 = 2순위 보강** (특히 멀티 경로가 프레임 통째 Enqueue + 워커 통째 Accept일 때 Vosk·간접 메인 부하)

#### 해결 방향 (문서 합의 — 코드는 후속)

| 순위 | 방향 | 목적 |
|------|------|------|
| **1** | 메인 히치 줄이기 | Dissonance Warn 직접 완화. 예: `LoadSync` 타이밍/비동기, 스파이크 구간 프로파일 |
| **2** | Dissonance→큐 **작은 청크**로 넣기 | 메인 `ProcessAudio`·워커 일감 크기 감소 |
| **3** | 워커 `AcceptWaveform`도 **작은 단위**로 | Vosk 큐 적체·이름 인식 중간 끊김 완화 |
| **유지** | 마이크 이중 오픈 금지, Vosk는 워커 | 이미 확정 |

**인식률:** 샘플 drop 시 `"berry"`가 `"ber"`처럼 잘릴 수 있음. 완전 불능 수준은 아님. 연발 Warn이면 히치부터 조사.

**비교 관점:** Among Us/VRChat 등은 보통 **채팅용 캡처만**. 꿀떡은 **채팅 + 로컬 STT**라 특수. 비교 기준은 “보이스 캡처가 메인을 막지 않게”이지, 타 게임 STT 파이프라인 복제가 아님.

---

## 5. 인식률 개선 파이프라인 (2026-08-04 재확정 — 커스텀 Lexicon 방식 폐기)

> **폐기 배경:** 기존 §5는 Vosk `vosk_recognizer_set_grm_with_lexicon`(커스텀 발음 lexicon 런타임 주입)을 전제로 했으나,
> 조사 결과 이 API는 [alphacep/vosk-api PR #1362](https://github.com/alphacep/vosk-api/pull/1362)로 **2023-05 제안된 뒤 아직 미병합·충돌(conflict) 상태로 정체**돼 있어
> **어떤 공식 Vosk 배포본에도 포함돼 있지 않다** — 이 프로젝트가 쓰는 `libvosk.dll`(`Assets/ThirdParty/Vosk`)도 마찬가지.
> 즉 "G2P → 커스텀 lexicon 적용"은 모델 그래프를 오프라인 재컴파일하지 않는 한 **구현 불가능** — 실제 존재하지 않는 API를 전제한 설계였으므로 폐기한다.
>
> **커스텀 빌드(PR #1362 패치)는 Post-Launch 후보로만 남긴다** — Windows/Android/OSX 네이티브 바이너리 전부 재빌드·자체 유지보수 필요, PR 자체도 미완성(SIL 하드코딩, grammar fst 미지원) 명시 상태라 9/1 일정 리스크가 큼.

### 5.1 데이터 역할 분리

| 데이터 | 저장 | 용도 |
|--------|------|------|
| **CheerName** (텍스트) | 로컬 저장 없음 (`PlayerPrefs` 기억 폐기, 2026-08-17) — Player별 `NetworkVariable`만, 매 판 재입력 | UI, `/cheer`, grammar 토큰 |
| **대체 발음 후보** (텍스트 목록, 사전 검증됨) | **저장 안 함** — 코드 테이블 | grammar JSON에 원래 이름과 함께 포함 |
| **Vosk 모델** | `StreamingAssets/vosk-model-en-us-0.22-lgraph/` (폴더 204MB) | 빌드에 포함 — 런타임 압축 해제·다운로드 **없음** |

> **[2026-08-13 확정 — 모델 배포·로드 규칙]** 모델은 **zip이 아니라 압축 해제된 폴더**로 `StreamingAssets`에 넣는다. `persistentDataPath`로 풀어 쓰던 기존 방식은 **Windows 사용자명이 한글인 플레이어에서 100% 크래시**했다 — libvosk 내부 Kaldi가 `std::ifstream`으로 파일을 열어 비ASCII 경로를 읽지 못하고([vosk-api#1072](https://github.com/alphacep/vosk-api/issues/1072)), `new Model()`이 예외 대신 NULL 핸들을 돌려주는 탓에 실패가 감지되지 않은 채 다음 네이티브 호출에서 프로세스가 즉사했다.
> 지켜야 할 것: ① 모델 경로를 사용자 폴더로 옮기지 말 것(비ASCII 유입) ② `VoskModelLoader.GetSharedModel()`의 **null 반환을 반드시 존중**할 것 — 네이티브 핸들 검증은 로더에서 1회만 수행한다 ③ **모델 로드 실패는 음성 인식만 비활성화하고 게임 진행을 막지 않는다.**
> 전체 진단 기록: `SteamworksIntegrationDesign.md` 트랙 6 — 8차 세션.

플레이어 UI 입력 = **영문 텍스트만**. Vosk 입력 = **grammar JSON(사전 등재 단어만)**. 커스텀 phoneme lexicon 없음.

### 5.2 확정 방향 — A(사전 검증) + B(발음 변형 대체 단어) **[Ship Must]**

Vosk grammar(`vosk_recognizer_new_grm`/`set_grm`)는 **모델 사전(words.txt)에 이미 있는 단어만** 인식 가능. 조어(`guma`/`sook`/`hobak` 등)는 모델에 없으면 원리상 인식이 잘 안 됨. 이를 아래 두 방법으로 보완한다.

**A. 사전 검증 (`Model.vosk_model_find_word`)**

```
입력: CheerName 후보 (Tutorial 커스텀 입력 또는 고정 4종)
  ↓
Model.vosk_model_find_word(word) → -1 이면 모델 사전에 없음
  ↓
사전에 없으면: Tutorial "말해보기"(§3.2/§5.5) UI에서 경고 표시 (강제 변경은 아님)
```

`vosk_model_find_word`는 `Assets/ThirdParty/Vosk/Model.cs`에 이미 바인딩돼 있음 — 새 네이티브 API 불필요, `CheerLexiconBuilder`/Tutorial 검증 플로우에서 호출만 추가하면 됨.

**B. 발음 변형 대체 단어 (grammar 배열 확장)**

```
입력: 고정 CheerName 중 사전 미등재 이름
  ↓
모델 words.txt 실측 + vosk_model_find_word로 검증된, 비슷한 소리의 실제 사전 단어를 후보로 추가
  ↓
grammar JSON: [원래 이름, 대체 단어..., "[unk]"]
  ↓
인식 결과가 대체 단어여도 CheerLexiconBuilder.ResolveVariant()로 원래 CheerName으로 매핑 → SubmitCheerServerRpc
```

**[Ship Must — 구현 완료, 2026-08]:** 고정 4종(`berry`/`guma`/`sook`/`hobak`)을 모델 `words.txt`에서 실측한 결과:

| CheerName | 사전 등재 | 대체 단어 |
|---|---|---|
| `berry` | ✅ 있음 | 불필요 |
| `guma`  | ✅ 있음 *(과거 "미포함" 기록은 오기 — 실측으로 정정)* | 불필요 |
| `sook`  | ✅ 있음 *(과거 "미포함" 기록은 오기 — 실측으로 정정)* | 불필요 |
| `hobak` | ❌ 없음 | `dan` (실발화 원형 "단호박"(danhobak)의 앞 음절, 사전 등재 확인 — 2026-08-05, `hobo` 근사 대체) |

`CheerLexiconBuilder.VariantMap`(코드 테이블)에 반영 완료. `ResolveVariant()`로 인식된 대체 단어를 원래 CheerName으로 되돌림.

> **2026-08-05 변경 배경:** 기존 `hobo`는 "hobak"을 순수 영어 철자 G2P로 근사(HH OW B 공유)한 것이라 실제 발화와 무관했음. `hobak`(호박)의 실제 구어 원형은 한국어 "단호박"(danhobak, 단맛 호박)이고, 그 앞 음절 "dan"이 모델 `words.txt`에 실제 등재된 단어로 확인됨 → 실발화 기반 대체 단어로 교체. **트레이드오프:** `dan`은 영어에서 매우 흔한 인명·단어라 인게임 자유 대화(Dissonance) 중 우연히 언급되면 오탐(false positive) 응원 소지가 `hobo`보다 높을 수 있음 — 플레이테스트로 재확인 필요.

**[Ship Must]:** Tutorial에서 (재)확정 시마다 Host → 전 Client에 최신 CheerName 배열 브로드캐스트 → 각 Client **동일 매핑 테이블**로 grammar 재생성(§5.3 그대로).

**C. 커스텀 이름 발음 변형 — 혼합 방식 [설계 확정 2026-08-05, 구현 미착수]**

B는 고정 4종에만 적용됨(사람이 직접 `words.txt`를 찾아서 표에 넣는 수동 방식이라 자유 입력 이름엔 자동 적용 불가). 커스텀 CheerName(`sahur` 등)까지 대체 발음을 지원하려면 아래 **혼합 방식**으로 확장하기로 확정:

```
1. 플레이어가 CheerName 확정 시도 (예: "sahur")
2. IsKnownWord(사전 검증, §5.2 A) → 사전에 없으면:
   a. 발음 근사 알고리즘(Metaphone/Soundex 계열 — 텍스트 스펠링 기반, Vosk G2P 아님)으로
      모델 words.txt 전체에서 발음이 비슷한 실제 사전 단어 N개 후보 산출
   b. Tutorial UI에 후보 리스트 표시 → 플레이어가 그중 선택
   c. 플레이어가 후보 밖 단어를 직접 입력해도 허용 — 단 그 단어도 IsKnownWord 통과해야 함
      (Vosk grammar는 사전에 없는 단어를 원리상 출력 불가 — 어떤 방식이든 이 제약은 못 피함)
3. 최종 선택된 "인식 대상 단어" → CheerName과 별도로 저장 (커스텀 VariantMap 엔트리로 취급)
4. 인식되면 §5.2 B의 ResolveVariant()와 동일한 방식으로 원래 CheerName에 매핑
```

**미착수 이유:** phonetic 유사도 인덱싱(30만+ 단어) + 후보 선택 UI가 필요해 B(표 하나 추가)보다 작업량이 큼. 다음 세션에서 이어서 설계·구현.

**미착수 시 폴백:** 커스텀 이름은 A(경고)만 적용 — 대체 발음 없이 원래 이름 그대로 grammar에 들어감(사전에 있으면 정상 인식, 없으면 인식 어려움 안내만).

**실측 참고 (2026-08-05, `graph/words.txt` 직접 확인):**
| 예시 이름 | 사전 등재 |
|---|---|
| `berry`/`guma`/`sook`/`jun`/`jack`/`saha`/`sahar`/`sahara` | ✅ 있음 |
| `hobak`/`sahur` | ❌ 없음 |

→ 흔한 영어 단어·이름은 대부분 이미 사전에 있음(모델이 30만+ 단어). 문제는 조어·외래어 계열만.

### 5.3 네트워크 — grammar 동기화 (2026-08-17 갱신)

```
[Tutorial] Client: 이름 입력 → Enter → SubmitCheerNameServerRpc(candidate)
[Host]     검증(형식·중복·금칙어) → 통과 시 해당 Player NetworkVariable 갱신
             → 전 Client에 최신 세션 이름 배열 전파 (GameSession 갱신)
[Each Client] 받은 이름 배열로 CheerLexiconBuilder.BuildGrammarJson(names)
             → CheerKeywordEngine.ApplySessionGrammar(names)로 로컬 Vosk 즉시 재적용
[Each Client] 자기 마이크 → 감지 → SubmitCheerServerRpc (응원 제출, 별개 RPC)
```

**확정 1회성이 아니라 매 확정마다 반복** — 누군가 이름을 바꿀 때마다 이 사이클이 다시 돈다. Lobby 시절의 "Start 시 1회 배포"는 폐기.

grammar/매핑 테이블 **파일을 서버 DB에 모을 필요 없음**.

### 5.4 (폐기 — §5.5로 통합) **[2026-08-17]**

> 기존 "로비 불러보기"(최초 1회 등록 확인)는 Lobby에 CheerName 로직이 없어지면서 **폐기**. 입력·확정·반복 검증을 전부 Tutorial 하나(§5.5)에서 담당한다.

### 5.5 Tutorial 말해보기 — 확정·검증 통합 지점 **[Ship Must — 2026-08-17 최종 확정]**

CheerName **입력·확정·재변경·반복 인식 검증**의 유일한 무대. 확정마다 §5.3 사이클이 돌아 grammar가 즉시 갱신되므로, 다음 루프를 몇 번이든 반복할 수 있다.

```
입력 → 확정 → grammar 갱신 → 말해보기(테스트) → 불만족 시 다시 입력 → …
```

실패 시 철자 변경 안내·대체 단어(§5.2 B) 추가(개발 튜닝). 강제 아님 — 실패해도 `TutorialGatherZone` 진행 가능.

---

## 6. 입력 — 음성 · 채팅

### 6.1 음성 흐름

```
[Client Owner]
  마이크 (Dissonance/Vosk 공유)
  → Vosk continuous + grammar/lexicon
  → CheerName 토큰 감지 (대화 중 문장 속 포함 OK)
  → SubmitCheerServerRpc(targetColorIndex, Voice)
```

| Inspector | 용도 |
|-----------|------|
| `keywordConfidence` | Vosk 결과 필터 (필요 시) |
| `minVolume` / `maxVolume` | (선택) VAD 보조 |

### 6.2 채팅 **[Ship Must]**

- **문법:** `/cheer berry` (`/cheer {CheerName}`, 공백 1개)
- 대소문자 무시, trim.
- 자기 이름 응원 불가. 버프 중 타겟 불가.
- **음성 OR 채팅** = 1표 (동일 ServerRpc).
- **마이크 없음 / Vosk 실패** → 채팅 **필수** 지원.

### 6.3 응원 주체

- **응원하는 사람**이 CheerName을 **말함** (팀원 이름 외치기).
- **각 Client**는 **자기 마이크**만 분석 → 다른 사람 목소리에서 이름 찾을 필요 **없음**.

---

## 7. 네트워크 권한

### 7.1 아키텍처 (NGO + Host)

```
[각 Client]
  Dissonance: 팀 보이스 송수신
  Vosk: 로컬 키워드
  채팅: /cheer 파싱
  → SubmitCheerServerRpc(targetColorIndex, source)

[Host]
  CheerService:
    cheererClientId → target (1:1)
    target별 집계 · 타임아웃 · 쿨
    → ApplyBuff (PlayerBuffSystem)
    → NetworkVariable / ClientRpc (UI, 버프 미러링)
```

- **응원 판정용 음성**은 서버로 **스트리밍하지 않음**.
- **팀 대화 음성**은 **Dissonance** P2P (게임 NGO와 별 transport).
- 게임 규칙 = **Host** (`NetworkDesign.md` §9).

### 7.2 타겟 ID

- **`PlayerColorType`** (또는 `NetworkPlayerSetup.colorIndex`) — `NetworkSessionData.ClientColors`와 일치.

### 7.3 버프 동기화 **[Ship Must]**

`PlayerBuffSystem`은 로컬 MonoBehaviour → **`NetworkPlayerSetup`에 버프 NetworkVariable** 미러링 (Host Apply 후 갱신, Client HUD 일치).

### 7.4 치팅 방어 (Open 수준)

- 동일 타겟 이미 응원 중 → 중복 RPC 무시.
- 채팅 rate limit.
- Host: 버프 중·사망·쿨·무효 target·자기 응원 거부.

---

## 8. UI

### 8.1 응원 HUD **[Ship Must]**

- 수혜자별 **`2/3`** 또는 **`●●○`**
- **내가 응원 중인 타겟** 하이라이트
- 수혜자 **버프 중 / 쿨 중**
- (선택) 타임아웃 잔여

**연동:** `TeamStatusUI` (버프 아이콘), `CheerProgressUI` (신규).

### 8.2 보이스 UI (여유)

- (선택) 마이크 mute, 수신 볼륨 — 최소 구현 OK. 옵션 패널(마스터·BGM·SFX)은 Ship Must.

### 8.3 Tutorial UI **[Ship Must]**

- CheerName **입력·확정(자유 재변경) + 말해보기 테스트** — 신규 컴포넌트(§3.4/5.3, Player 프리팹에 부착)
- Gate 카운트다운 — `TimerUI` / `OnCountdownTick` 재사용

### 8.4 채팅 입력 **[Ship Must]**

- M/T 스테이지 HUD에 `/cheer`용 **최소 입력창** (TMP_InputField 등).

---

## 9. Tutorial (**[Ship Must]**)

> **⭐ 2026-08-17 확정 — `1.Lobby` 씬 폐지, 역할 전부 Tutorial로 흡수.** 네트워크·수명주기 관점 SSOT는 `NetworkDesign.md` §6B (이탈 정책, Kick 폐지, Invite HUD, 게이트 동작). 이 절은 **Tutorial 콘텐츠·구역 배치** 관점만 다룬다 — 중복 서술 금지.

### 9.1 씬 흐름

```
Title → Tutorial → M.Stage1…5 → M.Boss → T.Stage1…5 → T.Boss → End.Demo
```

정식 경로에 Tutorial **포함**. `1.Lobby`는 더 이상 존재하지 않는다 — 접속 즉시 `Tutorial`에 캐릭터가 스폰된다(색 자동배정, `NetworkDesign.md` §6B.2). 연습 콘텐츠(Stealth/응원 등)는 경험자가 생략 가능하지만(§9.3), **사전 게이트 구간 자체(스폰·게이트 통과)는 누구도 생략 불가**.

### 9.2 Tutorial 구역 (4구역, 2026-08-17 확정)

Tutorial은 **자유 이동 구간**이다 — 아래 구역을 순서 상관없이 자유롭게 오가다, 마지막에 `TutorialGatherZone`에 모이면 `M.Stage1`로 넘어간다.

| # | 구역 | 내용 | 신규 | 경험자 |
|---|------|------|------|--------|
| (사전) | 접속/스폰 | 접속 즉시 스폰 + 색 자동배정(중복없음) + Invite HUD(구 로비 흡수, `NetworkDesign.md` §6B) | 필수 | 필수 (생략 불가) |
| 1 | 스텔스 체험 | 은신 플레이 감 잡기 | 있음 | **생략 가능** |
| 2 | CheerName 설정 | 이름 입력·확정·말해보기(자유 반복, §3.2·§5.5) | CheerName UI **항상 표시** — `PlayerPrefs` 스킵 없음 (2026-08-17 폐기) | **생략 불가** (매 판 재입력) |
| 3 | 응원 1회 체험 | 실제 응원 키워드 발화 → 버프 발동 감 잡기 | 있음 | **생략 가능** |
| 4 | `TutorialGatherZone` | 전원이 존에 모이면 카운트다운 → `M.Stage1` (§9.4) | **필수** | **필수** |

> **색 패드 연습(4번째 후보로 검토했던 것):** 2026-08-17 **보류**. 필요성이 재확인되면 별도 구역으로 추가 논의.

### 9.3 경험자 판정

| 방식 | 설명 |
|------|------|
| `PlayerPrefs TutorialCompleted = 1` | `TutorialGatherZone` 통과 후 저장 — Stealth/응원 1회 구역 스킵 판단용 (CheerName·게이트는 이 값과 무관하게 항상 수행) |
| (선택) 「연습 건너뛰기」 | 첫 판 숙련자 |

### 9.4 `TutorialGatherZone` · Dialogue

- **`TutorialGatherZone`**: 색 구분 없는 **단일** 트리거 존. 존 안 인원 == 접속 중인 전체 인원이면 카운트다운 → 통과 시 인원 동결 → `M.Stage1` 로드. **동적 인원(중간 합류/이탈)에도 헤드카운트 비교라 별도 로직 불필요.** 네트워크 세부(이탈 정책, Writer, 솔로 케이스)는 `NetworkDesign.md` §6B.3~4가 SSOT.
- 구 `StageStartGate`/`ColoredStartZone`(색별 지정 구역) 방식은 Tutorial에서 **`TutorialGatherZone`으로 대체**됐다. **M/T 스테이지의 색별 게이트는 영향 없음** — 그쪽은 계속 `StageStartGate`/`ColoredStartZone`/`StageNetworkState` 유지.
- DialogueUI: Tutorial = 손 연습, M/T = 구역별 필수.

---

## 10. Inspector 파라미터

| 파라미터 | 설명 | 초안값 |
|----------|------|--------|
| `buffDuration` | Shield / SpeedUp 지속 | 5초 |
| `cheerCooldownSeconds` | 수혜자 쿨 | 15초 |
| `cheerTimeoutSeconds` | 부분 응원 타임아웃 | 10초 |
| `chatRateLimitSeconds` | 채팅 응원 간격 | 0.5~1초 |
| `keywordConfidence` | Vosk 필터 | 플레이테스트 |

---

## 11. 구현 순서 (Phase)

> 전체 네트워크 단계: `ReleaseRoadmap.md` §3. **출시 게이트 = Steam P2P ④ + 응원·보이스 + Ship Must 콘텐츠.**

### Phase 0 — 설계 (완료)

- 본 문서 + NetworkDesign 확정.

### Phase 1 — 응원 코어 (채팅만) **[Ship Must]**

| 작업 | 내용 |
|------|------|
| `CheerService` | Host, M/T 씬. 집계·타임아웃·쿨·버프 |
| `SubmitCheerServerRpc` | `/cheer berry` 등 |
| 버프 | M=`Invincibility`, T=`SpeedUp`, `NetworkPlayerSetup` 미러링 |
| UI | `CheerProgressUI`, `TeamStatusUI`, 채팅 입력 |

**테스트:** ParrelSync **2인** — 채팅만 버프·쿨·타임아웃.

### Phase 2 — Dissonance **[Ship Must]**

| 작업 | 내용 |
|------|------|
| Asset | Dissonance + NGO integration |
| 설정 | Global, Voice Activation |

**테스트:** Dev Build ② localhost **2인** — 보이스 들림 (응원 전).

### Phase 3 — Vosk **[Ship Must]**

| 작업 | 내용 |
|------|------|
| Vosk | 패키지 + 모델 + `CheerLexiconBuilder` (고정 4종) |
| `CheerKeywordEngine` | Dissonance 마이크 공유 → ServerRpc |

**테스트:** Dev Build ② **2인** — `"berry go go"` + `/cheer` 중복 방지.

### Phase 4 — Development Build 중간 게이트

- localhost **2인**: NGO Must + 보이스 + 응원 **1회** 클리어, 사망 리로드.
- 출시 판정 아님 — Steam 전 빌드 버그 제거용.

### Phase 5 — Steam P2P + Lobby + Depot **[Ship Must]**

| 작업 | 내용 |
|------|------|
| Steamworks | Transport → Steam Networking, Lobby, 업로드 |
| NGO | Host/Client Steam Join |
| Dissonance | Steam 세션 위 보이스 |

**테스트 (2PC):** Steam **2인** 원격 — Title→Tutorial→M→T + 보이스 + 응원. **출시 최소 게이트.**

### Phase 6 — Steam 4인 검증 (출시 전 권장)

- 친구 **4인 1회** — 3표 응원·4보이스·4Gate.
- **2인 OK ≠ 4인 보장** (`ReleaseRoadmap.md` §3.1).

### Phase 7 — Tutorial · 커스텀 **[Ship Must]**

- CheerName UI, §5.2 사전 검증/대체 단어, 말해보기, Tutorial 씬.

---

## 12. 테스트

| Phase | 환경 | 인원 | 확인 |
|-------|------|------|------|
| 1 | ParrelSync | 2 | `/cheer` 규칙 |
| 2~3 | Dev Build ② | **2** | 보이스 + Vosk |
| 4 | Dev Build ② | **2** | NGO+응원 중간 게이트 |
| 5 | **Steam P2P ④** | **2 (Must)** | **출시 게이트** — 원격+보이스+응원 |
| 6 | Steam P2P | **4 (권장)** | 3표·4보이스·신뢰도 |
| 7 | Dev/Steam | — | Tutorial · 커스텀 이름 **[Ship Must]** |

### Ship Must 시나리오

**Steam 2인 (2PC — 출시 최소 게이트):**

- [ ] Steam Lobby Join → Tutorial(사전 게이트 구간 통과) → M 풀코스(+Boss) → T 풀코스(+Boss)
- [ ] Dissonance 보이스 양방향
- [ ] 대화 중 `"berry go go"` → Shield/SpeedUp (2인: **1표**면 발동)
- [ ] `/cheer berry` 폴백
- [ ] 사망 리로드 1회

**Steam 4인 (1회 권장 — 2PC만으로는 불가, 친구 필요):**

- [ ] 3표 응원 발동, 부분 응원 10초 초기화
- [ ] 4인 보이스
- [ ] 버프 중 / 쿨 중 규칙

**Dev Build / 솔로:**

- [ ] localhost 2인 NGO (Phase 4)
- [ ] 솔로 `/cheer berry`

---

## 13. 구현 체크리스트

### **[Ship Must]**

- [ ] `CheerService` + `SubmitCheerServerRpc`
- [ ] `/cheer {세션 CheerName}` (빈칸→색 기본값)
- [ ] ~~Lobby CheerName 인라인 편집 + Host `SetCheerNameServerRpc`~~ — **폐기 (2026-08-17), Tutorial로 이동**
- [ ] ~~`LobbyPlayerState.CheerName` + 슬롯 UI 동기화~~ — **폐기.** `LobbyPlayerState`에서 `CheerName` 필드 제거
- [ ] **[필수] Title/Tutorial UI 로컬라이제이션** — `TitleMenuController`/Tutorial 상시 HUD 상태 메시지 등 하드코딩된 한국어 문자열(예: "찾는 중...", "방을 찾을 수 없습니다.", "6자리 숫자를 입력해주세요." 등)을 String Table 방식으로 전환. DialogueUI/OXQuiz와 동일한 패턴 적용
- [ ] **[신규, 2026-08-17] `1.Lobby` 씬 삭제** + `0.Title`→`Title`, `2.Tutorial`→`Tutorial` 리네임 (에디터 작업, 사용자 담당)
- [ ] **[신규] `TutorialNetworkManager`(가칭)** — 구 `LobbyNetworkManager` 역할 이전: 접속 즉시 스폰, 색 자동배정, 사전구간 이탈 처리, 게이트 통과 시 Start 로직. SSOT: `NetworkDesign.md` §6B
- [ ] **[신규] `TutorialGatherZone`** — 색 무관 단일 헤드카운트 게이트 (§9.4, `NetworkDesign.md` §6B.3)
- [ ] **[신규] Tutorial 상시 HUD** — 룸코드 표시 + Steam Invite 버튼 (구 로비 UI 역할, §6B.5). 게이트 통과 후 숨김
- [ ] Kick UI/API **완전 제거** (`LobbyMenuController`/`LobbyNetworkManager`의 `KickPlayerServerRpc` 등) — 2026-08-17 폐지 확정
- [ ] **신규:** Tutorial CheerName 컴포넌트 (Player 프리팹 부착) — `NetworkVariable<FixedString32Bytes>` + `SubmitCheerNameServerRpc` + Host 검증(§3.5 로직 재사용) + 자유 재변경
- [ ] CheerName 검증 (§3.5, Tutorial 활성 플레이어 기준 중복 검사로 변경)
- [ ] ~~로비 불러보기~~ — **폐기 (2026-08-17).** §5.5 Tutorial 말해보기로 완전 통합
- [ ] **Dissonance + NGO** (4인 Global 보이스)
- [ ] **Vosk** grammar (세션 3~4명) + `CheerKeywordEngine`
- [x] `CheerLexiconBuilder` — §5.2 A(사전 검증) 구현 완료, 동작 확인됨 (2026-08-05)
- [x] `CheerLexiconBuilder` — §5.2 B(고정 4종 발음 변형 대체 단어 매핑) 구현 완료 — 실제 인식 플레이테스트는 아직
- [ ] `CheerLexiconBuilder` — §5.2 C(커스텀 이름 혼합 방식 대체 발음) — 설계만 확정, 구현 안 함
- [ ] Dissonance ↔ Vosk 마이크 공유
- [ ] M=Invincibility, T=SpeedUp + NetworkPlayerSetup 버프 미러링 + **응원 확장 2종**
- [ ] `CheerProgressUI` + `TeamStatusUI`
- [ ] 채팅 입력 UI
- [ ] 솔로 `/cheer` + 로컬 CheerService
- [ ] **숫자 포함 이름** — Tutorial 말해보기(§5.5)로 확인 → 필요 시 `0-9` 금지로 §3.5 갱신
- [ ] `Tutorial` — 조작 연습 + CheerName **입력·확정·자유 재변경·말해보기(반복 검증)**; **경험자도 스킵 없음** (2026-08-17). **[Ship Must, 2026-08-17 최종 확정]**
- [ ] ~~CheerName `PlayerPrefs` 기억 + TutorialCompleted 스킵~~ — **폐기.** `TutorialCompleted`(연습 구간 스킵용)만 유지, CheerName엔 미적용
- [ ] 연습 구역 (Stealth / 응원 1회) — **색 패드는 보류** (§9.2, 2026-08-17)
- [ ] Dev Build ② **2인** (중간) — Tutorial 이름+말해보기+인게임 응원
- [ ] **Steam P2P ④ 2인** (2PC — **출시 게이트**)
- [ ] Steam **4인 1회** (권장)
- [ ] (선택) Dissonance Steam P2P 음성 transport

---

## 14. 관련 코드 · 에셋 · 문서

| 항목 | 경로 / 비고 |
|------|-------------|
| 게이트 (M/T 스테이지) | `Assets/Scripts/Stage/StageStartGate.cs` |
| 발판 (M/T 스테이지) | `Assets/Scripts/Stage/ColoredStartZone.cs` |
| `TutorialGatherZone` | **신규, 미구현** — 색 무관 단일 게이트 (§9.4) |
| 버프 | `Assets/Scripts/PlayerBuffSystem.cs` |
| 네트워크 플레이어 | `Assets/Scripts/Network/NetworkPlayerSetup.cs` |
| 스테이지 네트워크 | `Assets/Scripts/Network/StageNetworkState.cs` |
| 팀 UI | `Assets/Scripts/UI/TeamStatusUI.cs` |
| 대화 | `Assets/Scripts/UI/DialogueUI.cs` |
| 네트워크 설계 | `Assets/Docs/NetworkDesign.md` (§6B = Lobby 흡수 SSOT) |
| `1.Lobby` 씬 | **삭제 대상** (2026-08-17 확정) |
| `LobbyNetworkManager.cs` / `LobbyMenuController.cs` | **역할 이전 대상** → `TutorialNetworkManager`(가칭)/Tutorial 상시 HUD 컨트롤러(가칭). Ready/색선택/Kick 로직은 이전하지 않고 삭제 |
| Tutorial 씬 | `Assets/Scenes/Tutorial.unity` (리네임 예정, 현재는 `2.Tutorial.unity`) |
| **Dissonance** | Asset Store + NGO integration |
| **Vosk** | GitHub `alphacep/vosk-unity-asr`, 모델 alphacephei.com |
| 응원 구현 | `CheerService`, `CheerKeywordEngine`, `CheerLexiconBuilder`, `CheerProgressUI`, `VoskModelLoader` |

---

## 15. FAQ

**Q. Dissonance `Insufficient buffer space` 경고는 버그?**  
A. **Warn.** 메인 히치로 마이크를 제때 못 비울 때. §4.5 — 1순위 히치, 2순위 청크. 코드 수정은 Docs 정리 후.

**Q. Discord로 팀 대화하면 되지 않나?**  
A. **아니오.** Ship Must = **인게임 보이스 (Dissonance)**. Discord 링크는 커뮤니티용만.

**Q. 음성을 서버로 보내서 인식하나?**  
A. **아니오.** 키워드는 **각 Client 로컬 Vosk**. 서버는 RPC·집계만. 팀 대화는 **Dissonance P2P**.

**Q. 50개 세션이면 grammar 200단어?**  
A. **아니오.** Client당 **현재 세션 3~4 CheerName**(+대체 단어)만.

**Q. `bec`를 back처럼 발음해도 되나?**  
A. **OK.** grammar 4택1 + §5.2 B 발음 변형 대체 단어. 100% 불필요.

**Q. 커스텀 lexicon(발음표)을 Vosk에 직접 주입할 수 있나?**  
A. **아니오 (2026-08-04 확정).** `vosk_recognizer_set_grm_with_lexicon`은 [PR #1362](https://github.com/alphacep/vosk-api/pull/1362)로 미병합·정체 상태 — 공식 Vosk 배포본에 없음. 대신 §5.2 A(사전 검증)+B(대체 단어)로 대응.

**Q. Porcupine은?**  
A. 상용·커스텀 파이프라인 부담. **Vosk grammar + §5.2 사전 검증/대체 단어**가 기본.

**Q. `jun`, `jack`처럼 흔한 이름도 경고 뜨나?**  
A. **아니오.** 모델(`vosk-model-en-us-0.22-lgraph`)이 30만+ 단어라 흔한 영어 이름·단어는 대부분 이미 사전에 있음. `hobak`, `sahur`처럼 조어·외래어만 경고 대상.

**Q. 사전 검증(A)만 빼고 대체 발음(B/C)만 쓰면 사전에 없는 단어도 인식되나?**  
A. **아니오.** Vosk grammar 모드는 모델 사전에 없는 단어를 원리상 출력 불가 — 이건 UI 경고를 끄고 끌 수 있는 옵션이 아니라 기술적 제약. 대체 발음도 결국 "사전에 있는 다른 단어"로 바꿔치기하는 것일 뿐, 원래 이름 자체가 인식되는 게 아님.

**Q. 커스텀 이름(`sahur` 등)도 §5.2 B처럼 자동으로 대체 발음이 붙나?**  
A. **아니오, 아직.** B는 고정 4종만(수동 표). 커스텀 이름까지 다루려면 §5.2 C(혼합 방식 — 자동 후보 제안 + 플레이어 선택/직접입력)로 확장하기로 설계만 확정, 구현은 다음 세션.

**Q. ParrelSync / Dev Build만으로 정식 출시?**  
A. **아니오.** **Ship Must** = Steam P2P 2인 + 보이스 + 응원 + Tutorial 등. Dev Build ②는 **중간** 게이트.

**Q. Steam P2P 테스트 2인만 가능한데?**  
A. **2PC면 Steam 2인**이 일상 QA·**출시 최소 게이트**. **4인 1회**는 친구 **권장** (`ReleaseRoadmap.md` §3.1).

**Q. 2인 OK면 4인도 OK?**  
A. **Transport·연결·1표 응원**은 2인에서 검증. **3표 집계·4보이스·4Gate**는 4인 전용 — **100% 보장 아님**.

**Q. Steam P2P 전에 응원 넣나?**  
A. Dev Build ② NGO **후** Phase 1~3 응원 → Dev Build **2인** → Steam P2P **2인** → (권장) Steam 4인 → **9/1 정식**.

**Q. 솔로 `/cheer`?**  
A. `/cheer {자기 CheerName}`. 빈칸이면 색 기본값 (`berry` 등).

**Q. CheerName은 로비에서? Tutorial에서?**  
A. **Tutorial.** 입력·확정·재변경·말해보기 전부 Tutorial 씬에서 처리(2026-08-17 확정). 로비 씬 자체가 없어졌으니 애초에 다른 선택지가 없다. §3.2·§3.6·§5.5.

**Q. `1.Lobby` 씬을 없애면 색 선택·Ready·Kick·초대는 어떻게 하나?**  
A. 전부 `Tutorial`로 흡수됐다(2026-08-17 확정, SSOT `NetworkDesign.md` §6B). 색은 접속 시 **자동 배정**(선택 UI 없음), Ready/Start는 `TutorialGatherZone`(§9.4)이 대신하고, Kick은 **완전 폐지**, 초대는 Tutorial 상시 HUD로 이동.

**Q. Tutorial에서 사람이 계속 늘거나 줄면 게이트가 이상해지지 않나?**  
A. 안 그런다. `TutorialGatherZone`은 색별 지정이 아니라 **"존 안 인원 == 현재 접속 인원"** 헤드카운트 비교라 인원 변동에 자동으로 맞춰진다(§9.4, `NetworkDesign.md` §6B.3). 별도 동적 로직이 필요 없다.

**Q. 말해보기 실패하면 진행 못 막나?**  
A. **아니오.** 강제 아님. 이름 수정 안내만, 몇 번이든 다시 시도 가능. §3.2.

**Q. 빈 이름에 확정하면 berry가 저장되나?**  
A. **아니오.** 빈칸 유지·기본값 **취급**만. §3.1·§3.4.

**Q. 숫자가 들어간 이름?**  
A. **잠정 허용.** Vosk 테스트 후 막을 수 있음. §3.5.

**Q. 버프 중 응원?**  
A. **표 안 쌓임**, 발동 불가.

**Q. Tutorial 매 판 5~8분?**  
A. **아니오.** `TutorialCompleted` 시 Stealth/응원 1회 구역은 스킵하고 CheerName 입력 + `TutorialGatherZone` 직행.
