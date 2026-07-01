# Cheer System & Tutorial Design

음성·채팅 **응원 시스템**, **인게임 보이스챗**, **정식 Tutorial** 설계 문서.  
관련: [`NetworkDesign.md`](NetworkDesign.md) (네트워크 검증 단계·Host 권한).

**범례**

| 태그 | 의미 |
|------|------|
| **[데모 Must]** | Steam 데모 출시 전 필수 |
| **[데모]** | 데모에 포함, Must는 아님 |
| **[정식]** | 데모 이후 ~ 정식 출시 |
| **[Post-Launch]** | 정식 이후 |

---

## 0. 데모 vs 정식 — 범위 요약

| 항목 | **[데모 Must]** | **[정식]** |
|------|-----------------|------------|
| 씬 흐름 | Title → Lobby → M.Stage1 → T.Stage1 → End.Demo | Title → Lobby → **Tutorial** → M.Stage1 → … |
| Tutorial 씬 | **없음** | **필수 경로** (연습 구간은 경험자 생략 가능) |
| **인게임 보이스챗** | **Dissonance + NGO** (4인 Global, Voice Activation) | 동일. Steam 직전 **Dissonance Steam P2P** transport 검토 |
| CheerName | **고정** berry / guma / ssuk / danho (소문자) | Tutorial에서 설정, 미입력 = 색상 기본값 |
| 이름 커스텀 | **없음** | Tutorial UI + `PlayerPrefs` + Network 동기화 |
| **키워드 인식** | **Vosk grammar** (고정 4종 lexicon) | Vosk + **CheerLexiconBuilder** (커스텀 G2P) |
| 채팅 응원 | `/cheer {name}` **필수 폴백** | 동일 |
| 스테이지 버프 | M.Stage1 = **Shield** (`Invincibility`), T.Stage1 = **SpeedUp** | 동일 |
| 인게임 설명 | **DialogueUI** (M/T 구역별) | Tutorial(핵심 메카) + DialogueUI |
| **멀티 연결** | **Steam P2P + Lobby** (§NetworkDesign ④) | 유지·Invite polish |
| **데모 목표** | Steam **원격 협동 + 보이스 + 응원** (홍보) | Tutorial·커스텀·밸런싱 |
| **개발자 테스트** | PC **2대** → Steam **2인** Must; **4인 1회** 권장 | — |
| 음성 인식 정확도 | 100% 불필요. 플레이테스트로 튜닝 | 커스텀 이름 + **말해보기 테스트 UI** |

> **데모 = Steam 홍보.** LAN/IP 멀티 배포 아님. `NetworkDesign.md` §0.2·§0.2.1 참고.

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
| 필요 응원 수 | `ActivePlayerCount - 1` (솔로 제외, §2.6) |
| 응원자 동시 타겟 | **1명만**. 타겟 변경 시 **이전 타겟 집계 -1** |
| 동시 수혜 | **가능**. 수혜자별 독립 |
| 갱신 | **없음**. 버프 중 시간·수치 연장 불가 |

**인원별 필요 응원 수**

| 접속 인원 | 수혜자 1명당 필요 응원 |
|-----------|------------------------|
| 1 (솔로) | §2.6 |
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

### 2.6 솔로 (오프라인)

- NGO 미사용.
- **음성:** 자기 CheerName 감지 → 1회 발동 (데모: `/cheer berry`와 동일 규칙).
- **채팅:** `/cheer {자기 CheerName}`.
- **인게임 보이스:** 솔로 시 **비활성** (팀원 없음).

---

## 3. CheerName (호출명)

### 3.1 기본값 (색상별)

| PlayerColorType | 기본 CheerName |
|-----------------|----------------|
| Blue | berry |
| Purple | guma |
| Green | ssuk |
| Yellow | danho |

저장·비교 시 **소문자 통일**.

### 3.2 **[데모 Must]**

- **커스텀 불가.** 위 4종 고정.
- 채팅: `/cheer berry`, `/cheer guma`, `/cheer ssuk`, `/cheer danho` (대소문자 무시).

### 3.3 **[정식]**

- **Tutorial**에서 1회 설정. 미입력 = 색상 기본값.
- **저장:** `PlayerPrefs` (로컬) + 세션 **NetworkVariable / LobbyPlayerState** 동기화.
- **변경 시점 (MVP):** Tutorial 연습 구간만. 로비·인게임 변경 = Post-Launch.
- **재접속 (경험자):** `PlayerPrefs` → Tutorial 이름 UI **생략** (§9.3).

### 3.4 검증 규칙 **[정식]**

| 항목 | 값 |
|------|-----|
| 길이 | **2 ~ 12자** |
| 허용 문자 | `a-z`, `0-9`, `_` |
| 대소문자 | 구분 없음 (저장 소문자) |
| 금칙어 | blocklist + 예약어 (`cheer`, `admin`, `host` 등) |
| 중복 | **같은 로비 세션 내 불가** |
| 발음 유사 **[정식·권장]** | 같은 로비에 `bac` / `bek` 동시 존재 **경고 또는 차단** |
| 검증 | 클라 1차 → **Host 최종** |

### 3.5 발음·인식 정책 (확정)

- **100% 정확 발음 강제 아님.** `back` / `bac` / `bek` / `bec` 등 **비슷한 소리**면 같은 CheerName으로 잡혀도 OK.
- **정확한 `bec` 발음만** 허용하는 구조 **아님** — grammar 4택1 + lexicon **발음 변형 여러 개**.
- 한국어 STT = Post-Launch. MVP = **로마字 CheerName + 영어 Vosk 모델**.
- **음성으로 lexicon “학습·저장”하지 않음.** UI 텍스트 → G2P → 런타임 lexicon. Tutorial **말해보기** = **검증**만.

---

## 4. 음성 스택 — Dissonance + Vosk

### 4.1 인게임 보이스챗 — Dissonance **[데모 Must]**

| 항목 | 선택 |
|------|------|
| 패키지 | **Dissonance Voice Chat** + **Dissonance for Netcode for GameObjects** |
| 비용 | Asset Store 일회 (~$55 세일 시) |
| 역할 | 4인 **자유 대화** |
| 설정 | **Global** room, **Voice Activation** (말할 때 전송) |
| 배치 | Lobby→Stage DDoL 또는 M/T 씬 `DissonanceSetup` + `NfgoCommsNetwork` |
| NGO | 게임 상태와 **병행**. 음성은 Dissonance transport, 규칙은 NGO Host |

**[정식] Steam 직전:** Dissonance **Steamworks P2P** 음성 transport 분리 검토 (데모는 NGO transport로 충분).

### 4.2 키워드 인식 — Vosk **[데모 Must]**

| 항목 | 내용 |
|------|------|
| 종류 | **오픈소스** STT (Apache 2.0). Asset Store 유료 아님 |
| 연동 | GitHub `alphacep/vosk-unity-asr` + 영어 소형 모델 (~50MB, `StreamingAssets`) |
| 모드 | **grammar** — 세션 CheerName + `[unk]` 만 후보 |
| 비용 | **$0**, MAU 무제한 (클라이언트 로컬 처리) |
| 서버 | 음성·lexicon **서버 저장 없음** |

**Porcupine / Azure:** 상용·과금·커스텀 파이프라인 부담 → **본 프로젝트 기본 선택 아님**. Post-Launch 검토만.

### 4.3 마이크 공유 **[데모 Must]**

Dissonance와 Vosk가 **동일 마이크** 사용. `Microphone.Start` 이중 오픈 **금지**.

| 방안 | 설명 |
|------|------|
| A (권장) | 한 경로 캡처 → 보이스 인코드 + Vosk feed **fork** |
| B | Dissonance C# 소스 tap 지점에서 PCM 분기 |

구현 Phase 3에서 확정.

---

## 5. Lexicon / G2P 파이프라인

### 5.1 데이터 역할 분리

| 데이터 | 저장 | 용도 |
|--------|------|------|
| **CheerName** (텍스트) | `PlayerPrefs` **[정식]**, Network 동기화 | UI, `/cheer`, grammar 토큰 |
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

**[데모 Must]:** 고정 4종 + 사전-defined lexicon 변형 (에디터 또는 코드 테이블).

**[정식]:** 로비 확정 시 Host → 전 Client에 CheerName 브로드캐스트 → 각 Client **동일 G2P 규칙**으로 lexicon 재생성.

### 5.3 네트워크 — lexicon 동기화

```
Host: CheerName 4개 확정 (검증·중복)
  → ClientRpc / NetworkList
Each Client: CheerLexiconBuilder.Build(names) → Vosk Apply (로컬)
Each Client: 자기 마이크 → 감지 → SubmitCheerServerRpc
```

lexicon **파일을 서버 DB에 모을 필요 없음**.

### 5.4 Tutorial 말해보기 **[정식]**

1. CheerName 텍스트 입력 (`bec`)
2. `[테스트]` — 이름을 크게 불러보세요
3. Vosk가 해당 토큰으로 잡히면 ✅ 확정
4. 실패 → 철자 변경 안내 또는 G2P 변형 추가 (개발 튜닝)

**녹음 파일을 lexicon에 저장하지 않음.**

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

### 6.2 채팅 **[데모 Must]**

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

### 7.3 버프 동기화 **[데모 Must]**

`PlayerBuffSystem`은 로컬 MonoBehaviour → **`NetworkPlayerSetup`에 버프 NetworkVariable** 미러링 (Host Apply 후 갱신, Client HUD 일치).

### 7.4 치팅 방어 (데모 수준)

- 동일 타겟 이미 응원 중 → 중복 RPC 무시.
- 채팅 rate limit.
- Host: 버프 중·사망·쿨·무효 target·자기 응원 거부.

---

## 8. UI

### 8.1 응원 HUD **[데모 Must]**

- 수혜자별 **`2/3`** 또는 **`●●○`**
- **내가 응원 중인 타겟** 하이라이트
- 수혜자 **버프 중 / 쿨 중**
- (선택) 타임아웃 잔여

**연동:** `TeamStatusUI` (버프 아이콘), `CheerProgressUI` (신규).

### 8.2 보이스 UI **[데모]**

- (선택) 마이크 mute, 수신 볼륨 — 최소 구현 OK. 옵션 패널 전체 = 정식.

### 8.3 Tutorial UI **[정식]**

- CheerName 입력 + **말해보기 테스트**
- Gate 카운트다운 — `TimerUI` / `OnCountdownTick` 재사용

### 8.4 채팅 입력 **[데모 Must]**

- M/T 스테이지 HUD에 `/cheer`용 **최소 입력창** (TMP_InputField 등).

---

## 9. Tutorial (정식)

### 9.1 씬 흐름

```
Title → Lobby → Tutorial → M.Stage1 → …
```

**[데모]:** Tutorial 없음. Lobby → M.Stage1.

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

> 전체 네트워크 단계: `NetworkDesign.md` §0.2. **데모 출시 게이트 = Steam P2P ④ + 응원·보이스.**

### Phase 0 — 설계 **[데모]**

- 본 문서 + NetworkDesign 확정.

### Phase 1 — 응원 코어 (채팅만) **[데모 Must]**

| 작업 | 내용 |
|------|------|
| `CheerService` | Host, M/T 씬. 집계·타임아웃·쿨·버프 |
| `SubmitCheerServerRpc` | `/cheer berry` 등 |
| 버프 | M=`Invincibility`, T=`SpeedUp`, `NetworkPlayerSetup` 미러링 |
| UI | `CheerProgressUI`, `TeamStatusUI`, 채팅 입력 |

**테스트:** ParrelSync **2인** — 채팅만 버프·쿨·타임아웃.

### Phase 2 — Dissonance **[데모 Must]**

| 작업 | 내용 |
|------|------|
| Asset | Dissonance + NGO integration |
| 설정 | Global, Voice Activation |

**테스트:** Dev Build ② localhost **2인** — 보이스 들림 (응원 전).

### Phase 3 — Vosk **[데모 Must]**

| 작업 | 내용 |
|------|------|
| Vosk | 패키지 + 모델 + `CheerLexiconBuilder` (고정 4종) |
| `CheerKeywordEngine` | Dissonance 마이크 공유 → ServerRpc |

**테스트:** Dev Build ② **2인** — `"berry go go"` + `/cheer` 중복 방지.

### Phase 4 — Development Build 중간 게이트 **[데모]**

- localhost **2인**: NGO Must + 보이스 + 응원 **1회** 클리어, 사망 리로드.
- **데모 출시 아님** — Steam 전 빌드 버그 제거용.

### Phase 5 — Steam P2P + Lobby + Depot **[데모 Must]**

| 작업 | 내용 |
|------|------|
| Steamworks | Transport → Steam Networking, Lobby, 업로드 |
| NGO | Host/Client Steam Join |
| Dissonance | Steam 세션 위 보이스 |

**테스트 (2PC):** Steam **2인** 원격 — Title→Lobby→M→T + 보이스 + 응원. **데모 출시 최소 게이트.**

### Phase 6 — Steam 4인 검증 **[데모 권장]**

- 친구/플레이테스트 **4인 1회** — 3표 응원·4보이스·4Gate.
- **2인 OK ≠ 4인 보장** (`NetworkDesign.md` §0.2.1).

### Phase 7 — Tutorial · 커스텀 **[정식]**

- CheerName UI, G2P lexicon, 말해보기, Tutorial 씬.

---

## 12. 테스트

| Phase | 환경 | 인원 | 확인 |
|-------|------|------|------|
| 1 | ParrelSync | 2 | `/cheer` 규칙 |
| 2~3 | Dev Build ② | **2** | 보이스 + Vosk |
| 4 | Dev Build ② | **2** | NGO+응원 중간 게이트 |
| 5 | **Steam P2P ④** | **2 (Must)** | **데모 출시 게이트** — 원격+보이스+응원 |
| 6 | Steam P2P | **4 (권장)** | 3표·4보이스·홍보 신뢰도 |
| 7 | Dev/Steam | — | 커스텀 이름 **[정식]** |

### 데모 Must 시나리오

**Steam 2인 (2PC — 출시 게이트):**

- [ ] Steam Lobby Join → Lobby → M → T → End
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

### **[데모 Must]**

- [ ] `CheerService` + `SubmitCheerServerRpc`
- [ ] `/cheer {고정4종}`
- [ ] **Dissonance + NGO** (4인 Global 보이스)
- [ ] **Vosk** grammar (고정 4종) + `CheerKeywordEngine`
- [ ] `CheerLexiconBuilder` (데모 고정 lexicon)
- [ ] Dissonance ↔ Vosk 마이크 공유
- [ ] M=Invincibility, T=SpeedUp + NetworkPlayerSetup 버프 미러링
- [ ] `CheerProgressUI` + `TeamStatusUI`
- [ ] 채팅 입력 UI
- [ ] 솔로 `/cheer` + 로컬 CheerService
- [ ] Dev Build ② **2인** (중간)
- [ ] **Steam P2P ④ 2인** (2PC — **데모 출시 게이트**)
- [ ] Steam **4인 1회** (권장)
- [ ] CheerName 커스텀·Tutorial **없음**

### **[정식]**

- [ ] `2.Tutorial` + Lobby → Tutorial
- [ ] CheerName UI + PlayerPrefs + Network
- [ ] `CheerLexiconBuilder` G2P + 발음 변형 + 말해보기
- [ ] 발음 유사 이름 검증
- [ ] TutorialCompleted 스킵 + Gate → M.Stage1
- [ ] 연습 구역 (Stealth / Pad / Cheer)
- [ ] (선택) Dissonance Steam P2P 음성 transport **[정식]**

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
| **(구현 예정)** | `CheerService`, `CheerKeywordEngine`, `CheerLexiconBuilder`, `CheerProgressUI` |

---

## 15. FAQ

**Q. Discord로 팀 대화하면 되지 않나?**  
A. **아니오.** 데모 Must = **인게임 보이스 (Dissonance)**. Discord 링크는 커뮤니티용만.

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

**Q. ParrelSync / Dev Build만으로 데모 출시?**  
A. **아니오.** 데모 = **Steam P2P 2인 Must** + 보이스 + 응원. Dev Build ②는 **중간** 게이트.

**Q. Steam P2P 테스트 2인만 가능한데?**  
A. **2PC면 Steam 2인**이 일상 QA·**데모 출시 최소 게이트**. **4인 1회**는 친구 플레이테스트 **권장** (§0.2.1).

**Q. 2인 OK면 4인도 OK?**  
A. **Transport·연결·1표 응원**은 2인에서 검증. **3표 집계·4보이스·4Gate**는 4인 전용 — **100% 보장 아님**.

**Q. Steam P2P 전에 응원 넣나?**  
A. Dev Build ② NGO **후** Phase 1~3 응원 → Dev Build **2인** → Steam P2P **2인** → (권장) Steam 4인 → 데모 출시.

**Q. 솔로 `/cheer`?**  
A. `/cheer {자기 CheerName}`. 데모: `/cheer berry` 등.

**Q. 버프 중 응원?**  
A. **표 안 쌓임**, 발동 불가.

**Q. Tutorial 매 판 5~8분?**  
A. **아니오.** `TutorialCompleted` 시 Gate 직행.
