# Cheer System & Tutorial Design

음성·채팅 **응원 시스템**과 **정식 Tutorial** 설계 문서.  
관련: [`NetworkDesign.md`](NetworkDesign.md) (네트워크 검증 단계·Host 권한).

---

## 0. 데모 vs 정식 — 범위 요약

| 항목 | 데모 | 정식 |
|------|------|------|
| 씬 흐름 | Title → Lobby → M.Stage1 → … | Title → Lobby → **Tutorial** → M.Stage1 → … |
| Tutorial 씬 | **없음** | **필수 경로** (연습 구간은 경험자 생략 가능) |
| CheerName | **고정** BERRY / GUMA / SSUK / DANHO | Tutorial에서 설정, 미입력 = 색상 기본값 |
| 이름 커스텀 | **없음** | Tutorial (PlayerPrefs 저장) |
| 스테이지 버프 | M.Stage1 = **Shield**, T.Stage1 = **SpeedUp** | 동일 |
| 인게임 설명 | **DialogueUI** (M/T 구역별) | Tutorial(핵심 메카) + DialogueUI |

---

## 1. 응원 시스템 — 개요

플레이어가 **팀원의 호출명(CheerName)** 을 음성 또는 채팅으로 외치면 응원 1표.  
**나를 제외한 전원**이 같은 수혜자를 응원하면 **팀 버프** 발동.

- **M.Stage1 (Mouth):** Shield (`Invincibility` 또는 `DefenseUp` — 구현 시 `PlayerBuffSystem`에 추가)
- **T.Stage1 (Throat):** SpeedUp

---

## 2. 응원 코어 규칙

### 2.1 발동 조건

| 규칙 | 내용 |
|------|------|
| 수혜자 | **자기 자신**. 자기 자신에게 응원 불가 |
| 필요 응원 수 | `ActivePlayerCount - 1` (솔로 제외, §2.6) |
| 응원자 동시 타겟 | **1명만**. 타겟 변경 시 **이전 타겟 집계 -1** |
| 동시 수혜 | **가능**. 파랑 버프 중 보라도 별도로 버프 획득 가능 (수혜자별 독립) |
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
| **버프 중** | **쌓지 않음** (입력 자체 차단) | 불가 |
| **쿨타임 중** (버프 종료 후) | 쌓임 | 쿨 종료 전까지 **발동만** 불가 |
| **사망** | **진행 초기화** | 불가 |

- **응원자 사망:** 멀티는 `StageResetOnPlayerDeath` → 씬 리로드로 응원 상태 **자동 초기화**.

### 2.3 쿨타임

- **대상:** 수혜자 **개인**
- **시작 시점:** 버프 `remainingTime`이 **0이 된 순간**
- **길이:** Inspector (`cheerCooldownSeconds`, 예: 15초)
- **갱신:** 없음 (§2.1)

### 2.4 타임아웃 (부분 응원)

- **조건:** 특정 수혜자에게 **첫 표가 들어온 시점**부터 **N초** (Inspector, 기본 **10초**)
- **동작:** 필요 수에 못 미치면 해당 수혜자에 대한 **응원 표 전부 초기화**
- **2인·4인 공통** 단일 값 (Inspector 조정)

### 2.5 응원자 쿨타임

- **MVP 없음.** 타겟 1명 규칙 + 키워드 인식 + 타임아웃으로 충분.
- 채팅만 **rate limit** (0.5~1초, 서버) — 스팸 방지.

### 2.6 솔로 (오프라인)

- NGO 미사용. 팀원 없음.
- **음성:** 자기 `CheerName` 키워드 인식 → 1회 발동
- **채팅:** `/cheer berry` (자기 이름, §4.2)
- 쿨타임·갱신 없음 규칙은 멀티와 동일.

---

## 3. CheerName (호출명)

### 3.1 기본값 (색상별)

| PlayerColorType | 기본 CheerName |
|-----------------|----------------|
| Blue | BERRY |
| Purple | GUMA |
| Green | SSUK |
| Yellow | DANHO |

### 3.2 데모

- **커스텀 불가.** 위 4종 고정.
- 채팅 화이트리스트: `/cheer berry`, `/cheer guma`, `/cheer ssuk`, `/cheer danho` (대소문자 무시).

### 3.3 정식

- **Tutorial**에서 1회 설정. 미변경 시 **색상 기본값** 유지.
- **저장:** `PlayerPrefs` (로컬) + 세션 중 **NetworkVariable / LobbyPlayerState** 동기화.
- **변경 시점 (MVP):** Tutorial 연습 구간 진입 시만. 인게임·로비 변경은 Post-Launch.
- **재접속 (경험자):** PlayerPrefs 로드 → Tutorial 이름 UI **생략** (§6.3).

### 3.4 검증 규칙

| 항목 | 값 |
|------|-----|
| 길이 | **2 ~ 12자** |
| 허용 문자 | `a-z`, `0-9`, `_` (로마자·숫자) |
| 대소문자 | 구분 없음 (저장은 소문자 통일 권장) |
| 금칙어 | blocklist + 예약어 (`cheer`, `admin`, `host` 등) |
| 중복 | **같은 로비 세션 내 중복 불가** |
| 검증 주체 | 클라 1차 → **서버 최종** |

### 3.5 음성 인식

- **키워드 = 각 플레이어의 CheerName** (동기화된 목록).
- 커스텀 이름(`youngjun` 등)은 **로마자 발음** 기준 MVP.
- 한국어 STT는 Post-Launch 검토.

---

## 4. 입력 — 음성 · 채팅

### 4.1 음성 (A3, 기본)

```
1. 마이크 RMS 측정 (짧은 윈도우)
2. minVolume ≤ RMS ≤ maxVolume → "발화" 인정
3. STT/키워드 매칭 → 타겟 CheerName 감지
4. SubmitCheerServerRpc(targetPlayerId, Voice)
```

| Inspector | 용도 |
|-----------|------|
| `minVolume` | 너무 작은 소리 무시 |
| `maxVolume` | (선택) 클리핑·과도한 소리 무시 |
| `keywordConfidence` | STT 신뢰도 (사용 시) |

- **민감도 과함** → 볼륨 게이트 제거, **키워드만(A1)** 으로 후퇴.
- **피치(F0)는 MVP 미사용.** 「작게 말하기」= **볼륨(RMS)** 조절.

### 4.2 채팅

- **문법:** `/cheer berry` (공백 1개, `/cheer {CheerName}`)
- 대소문자 무시, 앞뒤 trim.
- 자기 이름 응원 불가. 버프 중인 타겟 불가.
- **음성 OR 채팅** 중 하나 성공 = **1표** (동일 ServerRpc).

### 4.3 마이크 없는 플레이어

- 채팅 `/cheer {name}` 필수 지원.
- 집계·발동 규칙은 음성과 **동일**.

---

## 5. 네트워크 권한

### 5.1 아키텍처 (NGO + Host 권한)

```
[각 Client]
  마이크 / 채팅 입력
  → 로컬 키워드 매칭 (동기화된 CheerName 목록)
  → SubmitCheerServerRpc(targetPlayerId, source)

[Host / Server]
  CheerService: cheererClientId → targetPlayerId (1:1)
  target별 집계
  → 조건 충족 & 수혜자 발동 가능 시 ApplyBuff
  → NetworkVariable / ClientRpc로 UI·버프 동기화
```

- **마이크는 클라이언트 로컬만** 분석 (스트리밍 없음).
- 게임 규칙·버프 적용은 **Host** (`NetworkDesign.md` §9와 동일).
- Transport(LAN / Steam P2P)와 **무관** — Development Build에서 검증 후 Steam은 회귀만.

### 5.2 구현 시점 (권장)

```
① ParrelSync — §9 Must 동기화 안정화
② Development Build — 응원 없이 게임플레이 통과
③ 응원 시스템 구현 + ParrelSync 디버그
④ Development Build — 응원 포함 재검증
⑤ Steam P2P — 동일 시나리오 1회
```

### 5.3 치팅 방어 (데모 수준)

- 이미 해당 타겟 응원 중이면 중복 RPC 무시.
- 채팅 rate limit.
- 서버: 버프 중·사망·쿨·유효하지 않은 targetId 거부.

---

## 6. Tutorial (정식)

### 6.1 씬 흐름

```
Title → Lobby → Tutorial → M.Stage1 → …
```

- **데모:** Tutorial 없음. Lobby → M.Stage1 직행.

### 6.2 Tutorial 씬 역할

| 구간 | 신규 플레이어 | 경험자 |
|------|---------------|--------|
| 이름 설정 | CheerName UI (미입력 = 기본값) | PlayerPrefs 있으면 **생략** |
| 연습 | Stealth, 색 패드, 응원 1회 | **생략** (Gate 직행) |
| StageStartGate | **필수** (공통) | **필수** (공통) |

- Tutorial 씬은 **항상 로드**되나, **5~8분 연습은 신규만**.
- **경험자:** 연습 구역 비활성 또는 Gate 구역 스폰 → 발판만 밟고 M.Stage1.

### 6.3 경험자 판정

| 방식 | 설명 |
|------|------|
| `PlayerPrefs TutorialCompleted = 1` | 1회 Tutorial Gate 통과 후 자동 스킵 |
| (선택) 입구 「연습 건너뛰기」 | 첫 판 숙련자 수동 스킵 |

Gate 통과 시 `TutorialCompleted` 저장.

### 6.4 Tutorial 씬 레이아웃 (개념)

```
[입구]
  ├─ Skip / TutorialCompleted → Gate 구역 (경험자)
  └─ NameStation → PracticeZone → Gate 구역 (신규)

[Gate 구역] — 공통 종착
  StageStartGate + ColoredStartZone ×4
  전원 발판 점유 + 카운트다운 → M.Stage1
```

**연습 구역 (신규, Dialogue 병행 가능)**

- PlayerStealth (색 타일 위 은신)
- PressurePad — **자기 색만** 밟기
- 응원 1회 연습 (고정/설정된 CheerName)

**Gate 조건 (권장)**

- **전원 CheerName 확정** AND **활성 ColoredStartZone 전원 점유** → 카운트다운.
- 기존 `StageStartGate` + `GameSession.IsColorActive` — 2~3인 시 **해당 색 존만** 활성.

### 6.5 Tutorial → M.Stage1 전환

- **패턴:** M.Stage1과 동일 — `StageStartGate` + `ColoredStartZone`.
- **차이:** `CompleteCountdown()` 시 `StageManager.StartStage()` 대신  
  **Host `NetworkSceneManager.LoadScene("M.Stage1")`** (Tutorial 전용 완료 핸들러).
- Tutorial 씬에 `StageNetworkState` 배치 — 카운트다운 NGO 동기화.

### 6.6 DialogueUI와 역할 분담

| 내용 | Tutorial 씬 | M/T DialogueUI |
|------|-------------|----------------|
| Stealth, 색 패드, 응원 | **손으로 연습** | 리마인드만 |
| 함정·페이즈·OX 등 | — | **구역별 필수** |

---

## 7. UI

### 7.1 응원 HUD (필수)

- 수혜자별 진행: **`2/3`** 또는 **`●●○`**
- **내가 응원 중인 타겟** 하이라이트
- 수혜자 **버프 중 / 쿨 중** 표시
- (선택) 타임아웃 잔여

**연동 후보:** `TeamStatusUI` (버프 아이콘·이름 슬롯已有).

### 7.2 Tutorial UI

- CheerName 입력 (정식·신규)
- Gate 카운트다운 — 기존 `TimerUI` / `OnCountdownTick` 재사용

---

## 8. Inspector 기본 파라미터 (초안)

| 파라미터 | 설명 | 초안값 |
|----------|------|--------|
| `buffDuration` | Shield / SpeedUp 지속 | 5초 |
| `cheerCooldownSeconds` | 수혜자 쿨 (버프 종료 후) | 15초 |
| `cheerTimeoutSeconds` | 부분 응원 타임아웃 (첫 표 기준) | **10초** |
| `minVolume` / `maxVolume` | 음성 게이트 | 플레이테스트 |
| `chatRateLimitSeconds` | 채팅 응원 간격 | 0.5~1초 |

---

## 9. 구현 체크리스트 (요약)

### 데모

- [ ] `CheerService` (Host) + `SubmitCheerServerRpc`
- [ ] 음성 A3 + `/cheer {고정4종}`
- [ ] M=Shield, T=SpeedUp
- [ ] TeamStatusUI 응원 진행·버프·쿨
- [ ] Development Build 2~4인 검증
- [ ] **CheerName 커스텀·Tutorial 없음**

### 정식 (데모 후)

- [ ] `2.Tutorial` 씬 + Lobby → Tutorial 로드
- [ ] CheerName UI + PlayerPrefs + Network 동기화
- [ ] TutorialCompleted 스킵 + Gate → M.Stage1
- [ ] `StageStartGate` Tutorial 완료 분기
- [ ] 연습 구역 (Stealth / Pad / Cheer)

---

## 10. 관련 코드 · 문서

| 항목 | 경로 |
|------|------|
| 게이트 | `Assets/Scripts/Stage/StageStartGate.cs` |
| 발판 | `Assets/Scripts/Stage/ColoredStartZone.cs` |
| 버프 | `Assets/Scripts/PlayerBuffSystem.cs` |
| 팀 UI | `Assets/Scripts/UI/TeamStatusUI.cs` |
| 대화 | `Assets/Scripts/UI/DialogueUI.cs` |
| 로비 이름(현재 하드코딩) | `Assets/Scripts/UI/LobbySlotUI.cs` |
| 네트워크 설계 | `Assets/Docs/NetworkDesign.md` |
| Tutorial 씬 (에셋) | `Assets/Scenes/2.Tutorial.unity` |

---

## 11. FAQ

**Q. Steam P2P 전에 응원을 넣어야 하나?**  
A. **② Development Build**에서 게임플레이 Must 통과 **후**, Steam **전**에 넣고 ② 재검증.

**Q. 솔로에서 `/cheer`만 치면 되나?**  
A. `/cheer {자기 CheerName}`. 데모는 `/cheer berry` 등 4종.

**Q. 버프 중 팀원이 응원하면?**  
A. **표 자체를 쌓지 않음.** 발동도 불가.

**Q. 쿨 중 다른 캐릭 버프는?**  
A. **가능.** 수혜자별 독립. A가 쿨 중이어도 B 응원·버프 가능.

**Q. Tutorial 매 판 5~8분?**  
A. **아니오.** 씬은 매번 거치나, `TutorialCompleted` 시 **Gate 직행**.

**Q. ParrelSync만으로 응원 검증?**  
A. **아니오.** 마이크·권한은 **Development Build** 필수.
