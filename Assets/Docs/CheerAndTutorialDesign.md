# Cheer System & Tutorial Design

음성·채팅 **응원 시스템**, **인게임 보이스챗**, **정식 Tutorial** 설계 문서.  
관련: [`NetworkDesign.md`](NetworkDesign.md) (네트워크 검증 단계·Host 권한·출시 달력).

**범례**

| 태그 | 의미 |
|------|------|
| **[Open Must]** | Coming Soon + Playtest 오픈(D14) 전 필수 |
| **[Open]** | 오픈·Playtest에 포함, Must는 아님 |
| **[Release Must]** | 정식 출시 전 필수 |
| **[Post-Launch]** | 정식 이후 (관전 후보 등). **컷씬은 영구 제외** |

---

## 0. Open / Playtest / Release — 범위 요약

| 항목 | **[Open Must]** | **[Release Must]** |
|------|-----------------|-------------------|
| 씬 흐름 | Title → Lobby → **M.Stage1…5 → M.Boss** (T는 T주까지) | Title → Lobby → **Tutorial** → M1…5→M.Boss → T1…5→T.Boss → End.Demo |
| Tutorial 씬 | **없음** (Playtest 오픈) | **필수 경로** (연습 구간은 경험자 생략 가능) |
| **인게임 보이스챗** | **Dissonance + NGO** (4인 Global, Voice Activation) | 동일. Steam 직전 **Dissonance Steam P2P** transport 검토 |
| CheerName | **로비 커스텀** (§3). 빈칸 = 색 기본값 | 동일 + Tutorial 연습 |
| 이름 커스텀 | **Lobby 슬롯 인라인** + Host 확정 + `LobbyPlayerState` | Lobby 유지. `PlayerPrefs` 기억 |
| **로비 불러보기** | **Must** — 이름 확정 후 Vosk ✓/다시 (Start 강제 아님, §3.2) | Tutorial 말해보기로 확장 가능 |
| **키워드 인식** | **Vosk grammar** (세션 3~4 CheerName lexicon) | Vosk + **CheerLexiconBuilder** (커스텀 G2P) |
| 채팅 응원 | `/cheer {name}` **필수 폴백** | 동일 |
| 스테이지 버프 | M = **Shield** (`Invincibility`), T = **SpeedUp** | 동일 |
| 인게임 설명 | **DialogueUI** (M/T 구역별) | Tutorial(핵심 메카) + DialogueUI |
| **멀티 연결** | **Steam P2P + Lobby** (§NetworkDesign ④) | 유지·Invite polish |
| **목표** | Steam **Playtest** — 원격 협동 + 보이스 + 응원 + **텔레메트리** | Tutorial·밸런싱·옵션·정식 출시 |
| **개발자 테스트** | PC **2대** → Steam **2인** Must; **4인 1회** 권장 | — |
| 음성 인식 정확도 | 100% 불필요. **로비 불러보기로** 사전 확인 | Tutorial 말해보기 polish |

> **데모 빌드/페이지 없음.** Playtest + Coming Soon → 정식. 원격 IP Join / UDP discovery **미사용**. 개발=ParrelSync·localhost, 배포=Steam (`ReleaseRoadmap.md` §0.2).

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

**규모 오해 방지:** 전 세계 50로비 × 4명 = 200개 이름이 **한 PC에서 200개를 동시 인식**하는 구조가 **아님**.  
각 클라이언트는 **현재 로비의 CheerName 3~4개만** grammar/lexicon에 넣는다.

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
`LobbyPlayerState.CheerName`이 **빈 문자열**이면 저장값으로 기본명을 넣지 않는다.  
표시·`/cheer`·Vosk grammar·버프 대상은 **현재 `ColorIndex`의 기본 CheerName**으로 해석한다.

### 3.2 어디에 설정하나 **[Open Must]**

- **씬:** Tutorial 신설 없이 **`1.Lobby` 슬롯 UI**.
- **UI:** 기존 슬롯 `nameText`(BERRY 등) 자리 — **로컬 슬롯만** `TMP_InputField`(또는 클릭 시 편집). 타인 슬롯은 읽기 전용.
- **채팅 UI로 닉네임 설정하지 않음.** 인게임 `/cheer` 폴백만 채팅.
- **타이틀에서 입력하지 않음.** (로컬 기억용 `PlayerPrefs`는 정식에서 검토)

#### 로비 불러보기 (Say Test) **[Open Must]**

인게임 첫 실패 고통을 줄이기 위해, **로비에서 CheerName 인식이 되는지** 확인한다.

| 항목 | 규칙 |
|------|------|
| 시점 | CheerName **Host 확정 후** (빈칸이면 현재 색 기본값으로 테스트) |
| UI | 로컬 슬롯 옆 **TEST** / “불러보세요” — Heard ✓ / Try again |
| 인식 | 로컬 Vosk + 유효 CheerName grammar. 가능하면 **팀원 슬롯 이름**도 같은 엔진으로 불러보기 |
| 강제 | **Ready/Start 강제 통과 아님.** 실패해도 진행 가능. 안내만 (“안 되면 이름 고쳐라”) |
| 저장 | **녹음·lexicon 학습 없음.** 검증 UI만 |
| 숫자 테스트 | `b_4nana` 등 §3.5 잠정 숫자 — 이 불러보기로 인식률 판단 |

### 3.3 소유·색 변경 (확정)

- CheerName은 **색/캐릭터가 아니라 플레이어(슬롯)** 에 붙는다.
- 색을 Blue → Purple로 바꿔도 **커스텀 문자열은 플레이어를 따라간다.**
- Blue 슬롯이 “berry를 회수”하지 않는다. Blue인 **다른** 플레이어가 빈칸이면 그때만 `berry`.
- 커스텀을 지우고 확정(또는 빈칸 유지) → 다시 **현재 색 기본값** 취급.

### 3.4 확정·동시성 · Ready · Start (확정)

| 단계 | 규칙 |
|------|------|
| 타이핑 중 | **로컬만.** 남에게 중간 글자 동기화 안 함. 슬롯에는 **직전 Host 확정값**(또는 빈칸→기본값 표시) |
| Enter / 확정 | Client → **ServerRpc** → **Host 최종 검증** 후 `LobbyPlayerState` 반영 |
| 동시 확정 | **먼저 처리된 Rpc 승.** 나중 동일/위반 이름은 **거절**. UI: 치던 글자 유지 + 에러(테두리/짧은 문구) |
| Ready 중 | **색·이름 변경 불가** (기존 색 Ready 잠금과 동일) |
| Ready + 빈칸 | **자동으로 `berry` 등을 문자열로 넣지 않음.** 빈칸 = §3.1 기본값 취급 |
| `CanStart` | 색 유일 **AND** (해석 후) CheerName 유일 **AND** 클라이언트 Ready. Host Start만 |

해석 후 유일: 빈칸 플레이어의 유효 이름은 `ColorIndex` 기본값.  
예: A가 Blue+빈칸(`berry`), B가 `berry` 커스텀 확정 시도 → Host 거절.

### 3.5 검증·차단 리스트

클라 1차 → **Host 최종**. 실패 시 슬롯값 변경 없음.

#### 형식 **[Open Must]**

| # | 규칙 | 값 / 비고 |
|---|------|-----------|
| 1 | 길이 | **2 ~ 12** (확정 시에만). 빈칸은 길이 검사 생략 → 기본값 취급 |
| 2 | 대소문자 | 구분 없음. **저장·비교는 소문자** |
| 3 | 허용 문자 | **`a-z`, `0-9`, `_`** — 한글·공백·이모지·기타 기호 **불가** |
| 4 | 공백 | trim 후 빈칸이면 커스텀 없음(기본값 취급) |

**숫자 (`0-9`) — 잠정 허용:**  
`b_4nana` 등이 Vosk grammar/G2P에서 인식되는지 **플레이테스트 후** 숫자 금지로  Tight할 수 있다.  
테스트 전제: 숫자 포함 이름 2~3종을 Dev Build 2인에서 외쳐 보기 → 실패율 높으면 §3.5에서 `0-9` 제거로 Docs 갱신.

#### 세션·시스템 **[Open Must]**

| # | 규칙 |
|---|------|
| 5 | 같은 로비 세션에서 **해석 후 CheerName 중복 불가** |
| 6 | Ready 중 이름 변경 불가 |
| 7 | `CanStart`에 이름 유일 포함 (§3.4) |
| 8 | 예약어 불가: `cheer`, `admin`, `host`, `server`, `system`, `bot`, `null` 등 |

#### 금칙어 **[Open Must]**

| # | 규칙 | 범위 |
|---|------|------|
| 9 | 욕설 | 영문 공통 비속어 blocklist |
| 10 | 성·체 관련 | sexual / body slur |
| 11 | 혐오·차별 | 최소 목록 |
| 12 | 초간단 우회 | `f4ck`, `a$$` 등 — 완벽 필터 불필요 |

공개 영문 blocklist 파일 + Host 재검증. AI 필터 없음.

#### 정식에서 강화 **[Release Must]**

| # | 규칙 |
|---|------|
| 13 | 발음 유사 (`bac` / `bek`) 경고 또는 차단 |
| 14 | Tutorial 연습 맵 + 말해보기 polish (로비 불러보기는 Open에 이미 있음) |
| 15 | `PlayerPrefs`로 로컬 이름 기억 · 경험자 Tutorial 이름 UI 생략 (§9.3) |

### 3.6 **[Release Must]** Tutorial과의 관계

- Open: Lobby에서 커스텀 완료. Tutorial 씬 **불필요**.
- 정식: `2.Tutorial`은 **조작 연습 + 말해보기** 중심. CheerName 입력은 Lobby 유지 또는 Tutorial 병행(구현 시 택1, Docs 갱신).
- 인게임 이름 변경 = **Post-Launch**.
- ⚠️ `PlayerPrefs` / TutorialCompleted는 **네트워크 재접속이 아님.** 세션 정책은 `NetworkDesign.md` §12.

### 3.7 발음·인식 정책 (확정)

- **100% 정확 발음 강제 아님.** `back` / `bac` / `bek` / `bec` 등 **비슷한 소리**면 같은 CheerName으로 잡혀도 OK.
- **정확한 철자 발음만** 허용하는 구조 **아님** — grammar + lexicon **발음 변형 여러 개**.
- 한국어 STT = Post-Launch. MVP = **로마字 CheerName + 영어 Vosk 모델**.
- **음성으로 lexicon “학습·저장”하지 않음.** UI 텍스트 → G2P → 런타임 lexicon. Tutorial **말해보기** = **검증**만.

---

## 4. 음성 스택 — Dissonance + Vosk

### 4.1 인게임 보이스챗 — Dissonance **[Open Must]**

| 항목 | 선택 |
|------|------|
| 패키지 | **Dissonance Voice Chat** + **Dissonance for Netcode for GameObjects** |
| 비용 | Asset Store 일회 (~$55 세일 시) |
| 역할 | 4인 **자유 대화** |
| 설정 | **Global** room, **Voice Activation** (말할 때 전송) |
| 배치 | Lobby→Stage DDoL 또는 M/T 씬 `DissonanceSetup` + `NfgoCommsNetwork` |
| NGO | 게임 상태와 **병행**. 음성은 Dissonance transport, 규칙은 NGO Host |

**[Release Must] Steam 직전:** Dissonance **Steamworks P2P** 음성 transport 분리 검토 (Open은 NGO transport로 충분).

### 4.2 키워드 인식 — Vosk **[Open Must]**

| 항목 | 내용 |
|------|------|
| 종류 | **오픈소스** STT (Apache 2.0). Asset Store 유료 아님 |
| 연동 | GitHub `alphacep/vosk-unity-asr` + 영어 소형 모델 (~50MB, `StreamingAssets`) |
| 모드 | **grammar** — 세션 CheerName + `[unk]` 만 후보 |
| 비용 | **$0**, MAU 무제한 (클라이언트 로컬 처리) |
| 서버 | 음성·lexicon **서버 저장 없음** |

**Porcupine / Azure:** 상용·과금·커스텀 파이프라인 부담 → **본 프로젝트 기본 선택 아님**. Post-Launch 검토만.

### 4.3 마이크 공유 **[Open Must · 코드 확정]**

Dissonance와 Vosk가 **동일 마이크**를 쓰되, OS `Microphone.Start` **이중 오픈 금지**.

| 모드 | 캡처 경로 |
|------|-----------|
| **멀티 (NGO)** | Dissonance만 마이크 소유 → `CheerKeywordEngine`이 `SubscribeToRecordedAudio` / 구독자로 **PCM tap** |
| **솔로** | Dissonance가 오디오를 안 줄 때만 `Microphone.Start` **fallback** |

**과거 사고:** 멀티에서 Dissonance + 직접 `Microphone.Start` 동시 → 버퍼 오버런·오디오 스레드 경합이 메인 스톨(0.3~0.4s)로 번짐 → NGO 스폰 Deferred/유실. **재발 금지.**

### 4.4 스레드 구조 **[Open Must · 코드 확정]**

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
| `VoskModelLoader.LoadSync` | **메인 동기** (로비 1회) | 로비 진입 순간 히치 **가능** — 감수 또는 추후 비동기 |
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

## 5. Lexicon / G2P 파이프라인

### 5.1 데이터 역할 분리

| 데이터 | 저장 | 용도 |
|--------|------|------|
| **CheerName** (텍스트) | `PlayerPrefs` **[Release Must]**, Network 동기화 | UI, `/cheer`, grammar 토큰 |
| **Lexicon** (발음표) | **저장 안 함** — 런타임 생성 | Vosk `SetGrammar` / `set_grm_with_lexicon` |
| **Vosk 모델** | `StreamingAssets` | 빌드에 포함 |

플레이어 UI 입력 = **영문 텍스트만**. Vosk 입력 = **grammar JSON + phoneme lexicon**.

### 5.2 `CheerLexiconBuilder` (우리 구현)

```
입력: 세션 CheerName[] (최대 4, 자기 이름 제외 시 감지 목록 3)
  ↓
1. grammar JSON: ["berry","guma","bec","[unk]"]
2. 각 이름:
   - 모델 사전에 있으면 기본 발음 사용 (berry 등)
   - 없으면 G2P (grapheme → phoneme)
   - 짧은 이름 / bac·back류: 발음 변형 2~3개 추가 (B EH K, B AE K …)
  ↓
출력: in-memory lexicon → Vosk 적용
```

**[Open Must]:** 고정 4종 + 사전-defined lexicon 변형 (에디터 또는 코드 테이블).

**[Release Must]:** 로비 확정 시 Host → 전 Client에 CheerName 브로드캐스트 → 각 Client **동일 G2P 규칙**으로 lexicon 재생성.

### 5.3 네트워크 — lexicon 동기화

```
Host: CheerName 4개 확정 (검증·중복)
  → ClientRpc / NetworkList
Each Client: CheerLexiconBuilder.Build(names) → Vosk Apply (로컬)
Each Client: 자기 마이크 → 감지 → SubmitCheerServerRpc
```

lexicon **파일을 서버 DB에 모을 필요 없음**.

### 5.4 로비 불러보기 **[Open Must]**

§3.2 동일. 로비에서 이름 확정 → `[TEST]` → Vosk 토큰 ✓/다시.  
**녹음 파일을 lexicon에 저장하지 않음.**

### 5.5 Tutorial 말해보기 **[Release Must]**

Tutorial 연습 구간에 말해보기 UX를 넣거나, 로비 불러보기만으로 충분하면 생략 가능(구현 시 Docs 갱신).  
실패 시 철자 변경 안내·G2P 변형 추가(개발 튜닝).

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

### 6.2 채팅 **[Open Must]**

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

### 7.3 버프 동기화 **[Open Must]**

`PlayerBuffSystem`은 로컬 MonoBehaviour → **`NetworkPlayerSetup`에 버프 NetworkVariable** 미러링 (Host Apply 후 갱신, Client HUD 일치).

### 7.4 치팅 방어 (Open 수준)

- 동일 타겟 이미 응원 중 → 중복 RPC 무시.
- 채팅 rate limit.
- Host: 버프 중·사망·쿨·무효 target·자기 응원 거부.

---

## 8. UI

### 8.1 응원 HUD **[Open Must]**

- 수혜자별 **`2/3`** 또는 **`●●○`**
- **내가 응원 중인 타겟** 하이라이트
- 수혜자 **버프 중 / 쿨 중**
- (선택) 타임아웃 잔여

**연동:** `TeamStatusUI` (버프 아이콘), `CheerProgressUI` (신규).

### 8.2 보이스 UI **[Open]**

- (선택) 마이크 mute, 수신 볼륨 — 최소 구현 OK. 옵션 패널 전체 = 정식.

### 8.3 Tutorial UI **[Release Must]**

- CheerName 입력 + **말해보기 테스트**
- Gate 카운트다운 — `TimerUI` / `OnCountdownTick` 재사용

### 8.4 채팅 입력 **[Open Must]**

- M/T 스테이지 HUD에 `/cheer`용 **최소 입력창** (TMP_InputField 등).

---

## 9. Tutorial (**[Release Must]**)

### 9.1 씬 흐름

```
Title → Lobby → Tutorial → M.Stage1…5 → M.Boss → T.Stage1…5 → T.Boss → End.Demo
```

**[Open]:** Tutorial 없음. Lobby → M 풀코스(+Boss). T는 Playtest T주.

### 9.2 Tutorial 역할

| 구간 | 신규 | 경험자 |
|------|------|--------|
| 이름 설정 | CheerName UI | PlayerPrefs 있으면 **생략** |
| 연습 | Stealth, 색 패드, **응원 1회** | **생략** |
| StageStartGate | **필수** | **필수** |

### 9.3 경험자 판정

| 방식 | 설명 |
|------|------|
| `PlayerPrefs TutorialCompleted = 1` | Gate 통과 후 저장 |
| (선택) 「연습 건너뛰기」 | 첫 판 숙련자 |

### 9.4 레이아웃 · Gate · Dialogue

§6 Tutorial 레이아웃 — 기존 설계 유지 (`StageStartGate`, `ColoredStartZone`, `StageNetworkState`).  
DialogueUI: Tutorial = 손 연습, M/T = 구역별 필수.

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

> 전체 네트워크 단계: `ReleaseRoadmap.md` §0.2. **Playtest/오픈 게이트 = Steam P2P ④ + 응원·보이스.**

### Phase 0 — 설계 **[Open]**

- 본 문서 + NetworkDesign 확정.

### Phase 1 — 응원 코어 (채팅만) **[Open Must]**

| 작업 | 내용 |
|------|------|
| `CheerService` | Host, M/T 씬. 집계·타임아웃·쿨·버프 |
| `SubmitCheerServerRpc` | `/cheer berry` 등 |
| 버프 | M=`Invincibility`, T=`SpeedUp`, `NetworkPlayerSetup` 미러링 |
| UI | `CheerProgressUI`, `TeamStatusUI`, 채팅 입력 |

**테스트:** ParrelSync **2인** — 채팅만 버프·쿨·타임아웃.

### Phase 2 — Dissonance **[Open Must]**

| 작업 | 내용 |
|------|------|
| Asset | Dissonance + NGO integration |
| 설정 | Global, Voice Activation |

**테스트:** Dev Build ② localhost **2인** — 보이스 들림 (응원 전).

### Phase 3 — Vosk **[Open Must]**

| 작업 | 내용 |
|------|------|
| Vosk | 패키지 + 모델 + `CheerLexiconBuilder` (고정 4종) |
| `CheerKeywordEngine` | Dissonance 마이크 공유 → ServerRpc |

**테스트:** Dev Build ② **2인** — `"berry go go"` + `/cheer` 중복 방지.

### Phase 4 — Development Build 중간 게이트 **[Open]**

- localhost **2인**: NGO Must + 보이스 + 응원 **1회** 클리어, 사망 리로드.
- **Playtest 오픈 아님** — Steam 전 빌드 버그 제거용.

### Phase 5 — Steam P2P + Lobby + Depot **[Open Must]**

| 작업 | 내용 |
|------|------|
| Steamworks | Transport → Steam Networking, Lobby, 업로드 |
| NGO | Host/Client Steam Join |
| Dissonance | Steam 세션 위 보이스 |

**테스트 (2PC):** Steam **2인** 원격 — Title→Lobby→M→T + 보이스 + 응원. **Playtest 오픈 최소 게이트.**

### Phase 6 — Steam 4인 검증 **[Open 권장]**

- 친구/플레이테스트 **4인 1회** — 3표 응원·4보이스·4Gate.
- **2인 OK ≠ 4인 보장** (`ReleaseRoadmap.md` §0.2.1).

### Phase 7 — Tutorial · 커스텀 **[Release Must]**

- CheerName UI, G2P lexicon, 말해보기, Tutorial 씬.

---

## 12. 테스트

| Phase | 환경 | 인원 | 확인 |
|-------|------|------|------|
| 1 | ParrelSync | 2 | `/cheer` 규칙 |
| 2~3 | Dev Build ② | **2** | 보이스 + Vosk |
| 4 | Dev Build ② | **2** | NGO+응원 중간 게이트 |
| 5 | **Steam P2P ④** | **2 (Must)** | **Playtest/오픈 게이트** — 원격+보이스+응원 |
| 6 | Steam P2P | **4 (권장)** | 3표·4보이스·홍보 신뢰도 |
| 7 | Dev/Steam | — | 커스텀 이름 **[Release Must]** |

### Open Must 시나리오

**Steam 2인 (2PC — Playtest 오픈 게이트):**

- [ ] Steam Lobby Join → Lobby → M 풀코스(+Boss) (T는 T주)
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

### **[Open Must]**

- [ ] `CheerService` + `SubmitCheerServerRpc`
- [ ] `/cheer {세션 CheerName}` (빈칸→색 기본값)
- [ ] **Lobby CheerName 인라인 편집** + Host `SetCheerNameServerRpc`
- [ ] `LobbyPlayerState.CheerName` (빈칸 = 기본값 취급) + 슬롯 UI 동기화
- [ ] CheerName 검증 (§3.5) + `CanStart` 이름 유일
- [ ] **로비 불러보기** (§3.2 / §5.4) — TEST → Vosk ✓/다시 (Ready/Start 강제 아님)
- [ ] **Dissonance + NGO** (4인 Global 보이스) — 로비에서도 마이크 공유 가능해야 불러보기 가능
- [ ] **Vosk** grammar (세션 3~4명) + `CheerKeywordEngine`
- [ ] `CheerLexiconBuilder` (세션 lexicon / G2P)
- [ ] Dissonance ↔ Vosk 마이크 공유
- [ ] M=Invincibility, T=SpeedUp + NetworkPlayerSetup 버프 미러링
- [ ] `CheerProgressUI` + `TeamStatusUI`
- [ ] 채팅 입력 UI
- [ ] 솔로 `/cheer` + 로컬 CheerService
- [ ] **숫자 포함 이름** — 로비 불러보기로 확인 → 필요 시 `0-9` 금지로 §3.5 갱신
- [ ] Dev Build ② **2인** (중간) — 로비 이름+불러보기+인게임 응원
- [ ] **Steam P2P ④ 2인** (2PC — **Playtest/오픈 게이트**)
- [ ] Steam **4인 1회** (권장)

### **[Release Must]**

- [ ] `2.Tutorial` + Lobby → Tutorial (조작 연습; 말해보기는 로비와 중복 시 간소화)
- [ ] CheerName `PlayerPrefs` 기억 + 경험자 이름 UI 생략
- [ ] `CheerLexiconBuilder` G2P polish + 발음 유사 검증
- [ ] TutorialCompleted 스킵 + Gate → M.Stage1
- [ ] 연습 구역 (Stealth / Pad / Cheer)
- [ ] (선택) Dissonance Steam P2P 음성 transport **[Release Must]**

---

## 14. 관련 코드 · 에셋 · 문서

| 항목 | 경로 / 비고 |
|------|-------------|
| 게이트 | `Assets/Scripts/Stage/StageStartGate.cs` |
| 발판 | `Assets/Scripts/Stage/ColoredStartZone.cs` |
| 버프 | `Assets/Scripts/PlayerBuffSystem.cs` |
| 네트워크 플레이어 | `Assets/Scripts/Network/NetworkPlayerSetup.cs` |
| 스테이지 네트워크 | `Assets/Scripts/Network/StageNetworkState.cs` |
| 팀 UI | `Assets/Scripts/UI/TeamStatusUI.cs` |
| 대화 | `Assets/Scripts/UI/DialogueUI.cs` |
| 네트워크 설계 | `Assets/Docs/NetworkDesign.md` |
| Tutorial 씬 | `Assets/Scenes/2.Tutorial.unity` |
| **Dissonance** | Asset Store + NGO integration |
| **Vosk** | GitHub `alphacep/vosk-unity-asr`, 모델 alphacephei.com |
| 응원 구현 | `CheerService`, `CheerKeywordEngine`, `CheerLexiconBuilder`, `CheerProgressUI`, `VoskModelLoader` |

---

## 15. FAQ

**Q. Dissonance `Insufficient buffer space` 경고는 버그?**  
A. **Warn.** 메인 히치로 마이크를 제때 못 비울 때. §4.5 — 1순위 히치, 2순위 청크. 코드 수정은 Docs 정리 후.

**Q. Discord로 팀 대화하면 되지 않나?**  
A. **아니오.** Open Must = **인게임 보이스 (Dissonance)**. Discord 링크는 커뮤니티용만.

**Q. 음성을 서버로 보내서 인식하나?**  
A. **아니오.** 키워드는 **각 Client 로컬 Vosk**. 서버는 RPC·집계만. 팀 대화는 **Dissonance P2P**.

**Q. 50로비면 lexicon 200단어?**  
A. **아니오.** Client당 **현재 로비 3~4 CheerName**만.

**Q. `bec`를 back처럼 발음해도 되나?**  
A. **OK.** grammar 4택1 + lexicon 발음 변형. 100% 불필요.

**Q. lexicon을 UI에서 녹음으로 저장?**  
A. **아니오.** 텍스트 → G2P → 런타임 lexicon. 말해보기 = **검증**만.

**Q. Porcupine은?**  
A. 상용·커스텀 파이프라인 부담. **Vosk grammar + G2P**가 기본.

**Q. ParrelSync / Dev Build만으로 Playtest 오픈?**  
A. **아니오.** **Open Must** = Steam P2P 2인 + 보이스 + 응원. Dev Build ②는 **중간** 게이트.

**Q. Steam P2P 테스트 2인만 가능한데?**  
A. **2PC면 Steam 2인**이 일상 QA·**Playtest 오픈 최소 게이트**. **4인 1회**는 친구 플레이테스트 **권장** (`ReleaseRoadmap.md` §0.2.1).

**Q. 2인 OK면 4인도 OK?**  
A. **Transport·연결·1표 응원**은 2인에서 검증. **3표 집계·4보이스·4Gate**는 4인 전용 — **100% 보장 아님**.

**Q. Steam P2P 전에 응원 넣나?**  
A. Dev Build ② NGO **후** Phase 1~3 응원 → Dev Build **2인** → Steam P2P **2인** → (권장) Steam 4인 → Playtest 오픈.

**Q. 솔로 `/cheer`?**  
A. `/cheer {자기 CheerName}`. 빈칸이면 색 기본값 (`berry` 등).

**Q. CheerName은 로비에서? Tutorial에서?**  
A. **Open Must = 로비 슬롯 인라인 + 불러보기.** Tutorial 조작 연습은 **Release Must**. §3.2·§3.6.

**Q. 로비 불러보기 실패하면 Start 막나?**  
A. **아니오.** 강제 아님. 이름 수정 안내만. §3.2.

**Q. 빈 이름에 Ready하면 berry가 저장되나?**  
A. **아니오.** 빈칸 유지·기본값 **취급**만. §3.1·§3.4.

**Q. 숫자가 들어간 이름?**  
A. **잠정 허용.** Vosk 테스트 후 막을 수 있음. §3.5.

**Q. 버프 중 응원?**  
A. **표 안 쌓임**, 발동 불가.

**Q. Tutorial 매 판 5~8분?**  
A. **아니오.** `TutorialCompleted` 시 Gate 직행.
