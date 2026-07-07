# Network Design (MVP)

네트워크 · 출시 계획 문서.  
스테이지 범위: **`M.Stage1` → `T.Stage1` → `End.Demo`**.  
목표: **데모 출시** → **2주 내 정식 출시**.

---

## 0. 출시 로드맵 · 작업 우선순위

### 0.1 전체 작업 목록 (확정 순서)

| 순위 | 작업 | 비고 |
|------|------|------|
| 1 | **네트워크** | ParrelSync → 빌드 → Steam P2P (§0.2) |
| 2 | **사운드 마무리** | 최소한만. 과하면 방해되므로 억제 |
| 3 | **파티클** | 시작 안 함. **피격·Break** 등 핵심만 |
| 4 | **난이도 밸런싱** | **데모 + 1~3 완료 후** 플레이테스트 기반 |
| 5 | **UI 마무리** | 옵션·사운드 볼륨 등. 데모용 최소 / 정식에서 확장 |
| 6 | **캐릭터 애니메이션** | sit, dance 등 — **정식 출시 이후 또는 v1.1** |
| 7 | **Steamworks 연동** | Transport·Lobby·Depot·**데모 Steam P2P** (§0.3) |
| 8 | **컷씬** | **2주 일정에 넣지 않음** → 출시 후 |
| 9 | **출시 QA** | 빌드·Steam·2~4인 시나리오 체크리스트 |

### 0.2 네트워크 테스트 단계 (개발자 환경)

**LAN 실기 테스트는 현재 불가.** 아래 순서로만 검증한다.

**데모 핵심:** Steam에서 **원격 협동 + 인게임 보이스 + 응원** (홍보용).  
**개발자 장비:** 테스트 PC **최대 2대** — Steam P2P 일상 검증은 **2인** 기준 (§0.2.1).

```
① ParrelSync (에디터 Host + Clone Client)
   → 빠른 반복·버그 수정. ※ 출시 판정용 아님.

② Development Build (Host EXE + Client EXE, localhost / 같은 PC)
   → exe·NGO·마이크 등 **빌드 전용 버그** 중간 게이트. ※ 원격 4인 검증 아님.

③ 응원 시스템 (CheerService + Dissonance + Vosk + /cheer)
   → ②에서 1차, ④ Steam에서 최종 검증. (상세: CheerAndTutorialDesign.md §11)

④ Steam P2P + Steam Lobby + Depot
   → Transport 교체. **Steam 데모 출시 게이트** — 2인 필수, 4인 권장 (§0.2.1).
```

| 단계 | 목적 | 통과 기준 (최소) |
|------|------|------------------|
| ① ParrelSync | 구현·버그 수정 속도 | Title→Lobby→M→T→End **2인** 클리어 1회 |
| ② Dev Build | 빌드 품질·localhost NGO | **2인** 클리어, 사망 리로드 1회, 스테이지 전환 OK |
| ③ 응원 | 협동+보이스+응원 | CheerAndTutorialDesign §12 시나리오 |
| ④ **Steam P2P** | **데모 출시 (홍보)** | **2인** Steam 원격: M→T + 보이스 + 응원. **4인 1회 권장** |

**Transport:** ①② 개발 중 `UnityTransport`(localhost). **데모 배포 = Steam Networking transport 필수.**

#### 0.2.1 개발자 2PC · 2인 테스트 vs 4인

| | **2인 Steam P2P** (일상) | **4인 Steam P2P** (데모 전 권장) |
|--|--------------------------|----------------------------------|
| 검증됨 | Transport, Lobby, Join, NGO 동기화, Dissonance, Vosk, 응원(필요 1표), M→T | 위 + **4슬롯·4스폰·응원 3표·4음성** |
| **보장 안 됨 → 4인 전용 버그** | — | `ActivePlayerCount`·집계, 4색 Gate, 4명 보이스 혼잡, 인원 이탈(§12.0) |
| 판정 | **데모 출시 최소 게이트** (2PC 한정) | **홍보 신뢰도** — 친구/플레이테스트 1회 **강력 권장** |

**2인 통과 = 4인 100% 보장 아님.** 다만 NGO·Steam P2P·응원 **연결·규칙 골격**은 2인에서 대부분 검증 가능.  
**4인만 터지는 버그**는 §0.2.1 표 우측 항목 — 데모 직전 **4인 1회**로 잡는다.

### 0.3 데모 vs 정식 출시 — 어디까지 끝내나

#### 데모 출시 (Must Have) — Steam 홍보 데모

- **플레이 경로:** Title → Lobby → `M.Stage1` → `T.Stage1` → `End.Demo` (멀티 **2~4인**)
- **솔로:** Title 오프라인 패널 → 동일 스테이지 (NGO 없음)
- **네트워크:** §9 Must 동기화 + **§0.2 ④ Steam P2P + Steam Lobby**
- **응원·보이스:** 인게임 **Dissonance 4인 보이스** + **Vosk 응원** + `/cheer` (→ `CheerAndTutorialDesign.md`)
- **배포:** **Steam** (Depot 업로드). 원격 멀티 = **Steam P2P 필수** (LAN/IP 데모 아님)
- **네트워크 검증:** ② Dev Build (중간) → ④ **Steam P2P 2인 Must** + **4인 1회 권장** (§0.2.1)
- **UI:** 타이틀·로비·HP·카운트다운·End.Demo·응원 HUD·채팅 `/cheer`
- **사운드:** BGM 1~2 + 핵심 SFX
- **파티클:** 피격·Break만 (선택)
- **난이도:** “클리어 가능” 수준. 본격 밸런싱은 데모 후
- **텔레메트리:** §0.5.1 — 공개 데모 유저 데이터 수집 (이탈 구간·스테이지 실패율·음성 응원 실패)

#### 데모에서 **의도적으로 빼는 것** (버그·일정 방어)

| 항목 | 이유 |
|------|------|
| §12 재접속·Kick·60초 유예·스냅샷 복원 | 복잡도 최상. 데모: **호스트 이탈 = 종료**, 중간 이탈 = **즉시 리로드** |
| LAN UDP discovery 실기 검증 | 개발 환경 불가. ②는 localhost, ④는 **Steam** |
| Tutorial·CheerName 커스텀 | 정식 (CheerAndTutorialDesign.md) |
| 관전(Spectator) 모드 | 내부 QA용. Discord 화면공유로 대체 |
| sit / dance 등 이모트 애니 | 정식 이후 |
| 컷씬 | 정식 이후 |
| 옵션·설정 UI 전체 | 정식 (데모: OS 볼륨) |

#### 정식 출시 (데모 후 2주 — Must Have)

- Steam P2P·Lobby **유지·안정화** (데모에서 이미 구현 — Invite UX polish)
- **난이도 밸런싱** (데모 피드백)
- **Tutorial** + CheerName 커스텀 + lexicon G2P
- **UI:** 옵션(마스터·BGM·SFX), 해상도/전체화면
- **출시 QA** 체크리스트
- (선택) Dissonance **Steam P2P** 음성 transport 분리

#### 정식 2주 안에 **넣지 않는 것** (Post-Launch)

- 컷씬
- sit / dance / 이모트 애니메이션
- §12 재접속 풀 스펙 (스냅샷 무리로드 없이 재개 등) — 여유 있으면 v1.1
- Late Join, 호스트 마이그레이션
- 파티클 대량 추가

### 0.4 권장 작업 순서 (요약)

**상세 실행 순서·체크 항목은 §0.5 참고.**

```
[데모 — Steam 홍보]
0. 테스트 전 블로커 (Vosk, CheerName, AudioListener)
1. 폴리시 (오디오, 카메라, DialogueUI, End.Demo, 빌드 메타)
2. 로컬 테스트 (1인 → 2인 Dev Build → 스크린샷 1차)
3. Steamworks + 텔레메트리 MVP (§0.5.1)
4. Steam 테스트 (2인 Must → 4인 권장) → 스토어 → 데모 출시

[데모 후]
→ 별도 논의 (정식 출시 범위)
```

### 0.5 데모 출시 전 체크리스트 (실행 순서)

> 음성 시스템(CheerService + Dissonance + Vosk) 구축 완료 이후 기준.  
> 각 테스트 단계 직후 **버그 수정 구간**을 둔다.

#### Phase 0 — 테스트 전 블로커

| # | 작업 | 비고 |
|---|------|------|
| 0-1 | Vosk zip 정합 | `VoskModelLoader` 기대 zip ↔ `StreamingAssets` 실제 파일 일치 |
| 0-2 | CheerName 최종화 | `berry` / `guma` / `ssuk` / `danho` — `CheerLexiconBuilder`·`CheerService`·`/cheer` 통일 |
| 0-3 | AudioListener 중복 제거 | 스테이지·타이틀 씬당 **1개** (보통 `TopDownCamera` 자식) |

#### Phase 1 — 폴리시

| # | 작업 | 비고 |
|---|------|------|
| 1 | 오디오 | SFX/BGM 볼륨, `SFXManager.masterVolume`, Listener 배치 |
| 2 | 카메라 | **프리팹 X** — 씬 `TopDownCamera` 유지, Inspector 수치 튜닝 |
| 3 | DialogueUI | `M.Stage1` / `T.Stage1` 구역별 규칙·응원 설명 (`DialogueUI.cs`) |
| 4 | End.Demo 꾸미기 | 클리어 연출, 타이틀 복귀, **Discord 피드백 버튼** |
| 5 | 빌드 메타 | `Player Settings`: Product Name, Default Icon, `bundleVersion` (예: `0.1.0-demo`) |

#### Phase 2 — 로컬 테스트

| # | 작업 | 통과 기준 (최소) |
|---|------|------------------|
| 6 | 1인 E2E | Title 오프라인 → M → T → End. `/cheer`·음성 응원 1회 |
| 7 | 버그 수정 | Phase 2 이슈 정리 |
| 8 | 2인 Dev Build E2E | localhost, 보이스 양방향, 응원, 사망 리로드 1회 (§0.2 ②) |
| 9 | 버그 수정 | Phase 2 이슈 정리 |
| 10 | 스크린샷 1차 | Steam 스토어 초안용 (§0.5.2) |

#### Phase 3 — Steam + 텔레메트리

| # | 작업 | 비고 |
|---|------|------|
| 11 | Steam App ID + Steamworks | Transport → Steam Networking, Lobby, Depot 파이프라인 |
| 12 | 텔레메트리 MVP | §0.5.1 — **Steam 원격 테스트 전** 구축·전송 확인 |
| 13 | 스토어 페이지 초안 | App ID 필요. 스크린샷·설명은 §0.5.2 참고 |

#### Phase 4 — Steam 테스트 → 출시

| # | 작업 | 비고 |
|---|------|------|
| 14 | Steam 솔로/2인 원격 | **데모 출시 최소 게이트** (§0.2 ④) |
| 15 | 버그 수정 | |
| 16 | 친구 4인 테스트 | 3표 응원·4보이스·4Gate — **1회 권장** (§0.2.1) |
| 17 | 버그 수정 | |
| 18 | 스크린샷 최종 + 스토어 마무리 | 실플레이·안정 빌드 기준 (§0.5.2) |
| 19 | Steam 데모 출시 | Depot 업로드 |

#### 0.5.1 텔레메트리 MVP (데모 Must)

**목적:** 공개 데모 유저의 **이탈 구간(Quit Point)**, **스테이지 실패율·클리어 타임**, **음성 응원 실패 원인** 수집.  
**시점:** Steam 원격 테스트(§0.5 Phase 4) **전**에 켜야 초반 유저 데이터를 잃지 않음.

**구현:** `TelemetryService` (DDoL, `0.Title`) — `Track(eventName, properties)` 단일 진입점, 익명 `sessionId` + `buildVersion`, 배치 전송·Quit 시 flush.

**수집 이벤트**

| 이벤트 | 용도 |
|--------|------|
| `session_start` | 빌드 버전, 솔로/멀티, 인원 |
| `scene_enter` / `scene_exit` | 이탈 구간·체류 시간 |
| `quit` | Title / Lobby / 스테이지 / End 이탈 지점 |
| `stage_death` | 씬별 사망 → **실패율** |
| `stage_clear` | 씬별 클리어 시각 → **클리어 타임** |
| `run_complete` | End.Demo 도달 여부 |
| `cheer_voice_detected` | Vosk 키워드 감지 성공 |
| `cheer_voice_rejected` | 제출 거부 (자기응원, 쿨, 버프중 등 — reason enum) |
| `cheer_buff_activated` | 버프 발동 성공 |
| `cheer_buff_timeout` | 부분 응원 타임아웃 (표 부족) |
| `cheer_chat_used` | `/cheer` 폴백 — 음성 UX 문제 신호 |

**수집 금지:** 마이크 원음, 대화 내용 전문, 개인 식별 정보.

**연동 후보:** `SceneFlowManager`, `StageResetOnPlayerDeath`, `StageManager.OnStageClear`, `CheerKeywordEngine`, `CheerService`, Title/Lobby Quit.

#### 0.5.2 스크린샷

| 시점 | 목적 |
|------|------|
| §0.5 #10 (2인 Dev Build 후) | 스토어 **초안** — 플레이 가능 확인용 |
| §0.5 #18 (Steam 테스트 후) | **최종** — capsule·헤더·실플레이 품질 |

---

## 1. 기술 스택

| 항목 | 개발 ①② | **데모 배포 ④** | 정식 |
|------|---------|-----------------|------|
| 네트워크 | **NGO** | **NGO** | 동일 |
| 연결 | `UnityTransport` localhost (**7777**) | **Steam P2P + Lobby** | 동일·안정화 |
| 권한 | Host | Host | 동일 |
| 최대 인원 | 4인 | 4인 | 동일 |

- Transport **교체 가능**하게 분리 (`UnityTransport` ↔ Steam Networking). **Steam 데모 = Steam transport 필수.**
- 중간 참가(Late Join) **없음**. 재접속 **지원**.

---

## 2. 씬 흐름

### 2.1 멀티플레이

```
0.Title  →  1.Lobby  →  M.Stage1  →  T.Stage1  →  End.Demo  →  0.Title
```

| 씬 | 역할 |
|----|------|
| `0.Title` | `NetworkManager`, `GameSession`, `SceneFlowManager` (DDoL), Host/Join, **오프라인 패널** |
| `1.Lobby` | 룸코드, Ready, 캐릭터 선택(선착순), Host Start |
| `M.Stage1` | Phase / Mouth / Trap |
| `T.Stage1` | 패드·문·Boulder·함정 퍼즐 |
| `End.Demo` | 검은 화면 + 종료 UI → 타이틀 복귀 |

### 2.2 솔로 (오프라인)

```
0.Title (오프라인 패널에서 색 1개 선택)  →  M.Stage1  →  T.Stage1  →  End.Demo  →  0.Title
```

- **NGO 사용 안 함.** `1.Lobby` **거치지 않음**.
- **별도 솔로 로비 씬 없음.** Title에 **오프라인/Solo 패널**만 추가.
  - 구성: 캐릭터 **드롭다운 1개** + 시작 버튼
  - 선택 색 → `GameSession.SetActiveColors(1색)` → `M.Stage1` 직행

---

## 3. DontDestroyOnLoad (Title부터)

`0.Title`에 배치 후 세션 종료까지 유지:

| 오브젝트 | 비고 |
|----------|------|
| `NetworkManager` | 멀티만 활성. 솔로는 미사용 |
| `GameSession` | 인원·활성 색. 에디터에서 Title로 이동 (수동) |
| `SceneFlowManager` | 씬 시퀀스. 에디터에서 Title로 이동 (수동) |

---

## 4. 연결 · 룸코드

### 4.1 LAN / localhost (개발 ①② 전용)

| 항목 | 값 |
|------|-----|
| 포트 | **7777** (고정) |
| 용도 | ParrelSync 보조, **Dev Build ②** localhost만 |
| Join | `127.0.0.1:7777` 또는 6자리 룸코드 UI (개발용) |

**※ Steam 데모 배포·플레이어 멀티에는 사용하지 않음.**

**개발자 테스트:** ParrelSync(①) → Dev Build ② (같은 PC 2 exe).

### 4.2 Steam P2P + Lobby (**데모 Must**, §0.2 ④)

- **Steamworks** 초기화 + **Steam Networking** transport + **Steam Lobby**.
- Join: Lobby 코드 / 친구 초대 (Invite 있으면 좋음, 없으면 코드 Join).
- UI 마스킹 예: 식별자 `7**1` 형태.
- **Depot 업로드** 후 Steam 클라이언트에서 실행 — **원격 2~4인** 협동·응원·보이스 검증 환경.
- **개발자 2PC:** 일상 QA = **2인** Steam Join. 데모 직전 **4인 1회** 권장 (§0.2.1).

---

## 5. 타이틀 UI

| 버튼 | 동작 |
|------|------|
| 게임 만들기 | Host → `1.Lobby` |
| 게임 참여 | Client, 룸코드 입력 → `1.Lobby` |
| **오프라인 / Solo** | 패널 열기 → 드롭다운 1개 → `M.Stage1` (NGO 없음) |

---

## 6. 로비 규칙 (`1.Lobby`)

| 규칙 | 내용 |
|------|------|
| 인원 | **1~4인 가변** (빈 슬롯 허용. 2인이면 2슬롯만 사용 가능) |
| Ready | **접속한 인원 전원** Ready |
| Ready 취소 | 가능 |
| Start | **호스트만**, 전원 Ready + **4색 중복 없음** |
| 캐릭터 | **선착순** 점유, **Ready 후 변경 불가** |
| 빈 슬롯 UI | `Empty` |
| Kick | **호스트만**, Ready 전/후 모두. **즉시 슬롯 비움** |
| 호스트 | 드롭다운으로 캐릭터 선택 |

---

## 7. 플레이어 · 스폰

### 7.1 Prefab

- 씬에 Player 4개 배치 **제거** (`M.Stage1` 등).
- **NetworkObject Player Prefab 1개** + 스폰 시 `Configure(color, playerId, stats)`.
- **활성 슬롯(선택된 색)만** 스폰.

### 7.2 스폰 위치

- **`ColoredStartZone.spawnPoint`** (존 **위**)에 배치.
- 스폰 직후 `ForceSetSpawnPoint` 동일 좌표 설정.
- 존 트리거 진입 → 점유 → `StageStartGate` 카운트다운 (전원 점유 시 진행).
- **씬 지형(T.Stage) 이동 불필요.** 존·spawnPoint는 씬마다 에디터 배치.

### 7.3 입력 · 카메라

| 항목 | 규칙 |
|------|------|
| 이동 | **Owner 권한** — 각 클라이언트가 **자기 캐릭터만** 조종 |
| 입력 | Owner만 Input 활성 |
| 카메라 | `TopDownCamera` — **로컬 플레이어만** follow |
| 마우스 시점 | **본인만** |
| 클라이언트 예측 | **사용 안 함** (`NetworkTransform` 보간만) |

---

## 8. 씬 로드 · 진행

- Host가 `NetworkSceneManager.LoadScene` (로비→스테이지, 스테이지 전환, 리로드).
- `SceneFlowManager.LoadNextScene`: `M.Stage1` → `T.Stage1` (Host 트리거).
- `T.Stage1` 클리어 → **`End.Demo`**.
- `End.Demo`: 검은 화면 + UI → **타이틀 복귀**.

### 타이틀 복귀 시

| 모드 | 동작 |
|------|------|
| **멀티** | `NetworkManager.Shutdown()` + 세션·로비 상태 리셋 |
| **솔로** | NGO 없음, `GameSession` 등 런타임 상태 리셋 |

---

## 9. 호스트 판정 · 동기화

- **게임 규칙**(패드, 문, 함정, Phase, 데미지, 클리어)은 **Host에서만** 판정.
- 결과는 **`StageNetworkState` (중앙 매니저)** 를 통해 `NetworkVariable` / RPC로 **전원에 공유**.
- Client도 **동일한 연출·상태**를 봄 (Host만 보이는 것이 아님).
- 플레이어 위치는 Owner + `NetworkTransform`.

### MVP 동기화 대상

**우선순위:** `Must (데모)` → `Should (데모 여유)` → `Post (정식 이후)`

**M.Stage1**

| 대상 | 우선순위 |
|------|----------|
| `StageStartGate` / 카운트다운 | Must |
| `PhaseManager` | Must |
| `ArrowTrap` / `DropTrap` 발사·피격 (Host Spawn) | Must |
| `TrapProjectile` 데미지·파괴 | Must |
| `MouthController` / Mouth Animator 연동 | Should |
| `WindTrap` (Owner 힘 전달) | Should |
| `TrapPlayerTracker` 및 Mouth 계열 함정 | Should |

**T.Stage1**

| 대상 | 우선순위 |
|------|----------|
| `StagePressurePadSetup` 결과 (Host 시드) | Must |
| `PressurePad` / `DoorController` / `DoorPuzzleGroup` | Must |
| `BoulderSpawnManager` / `BoulderSpawner` | Must |
| `Breakable` 파괴 **시각** 동기화 | Must |
| `ReachZoneObjective` / `StageManager` 클리어 | Must |
| `BuffPickup` Despawn | Should |
| `WallMover` | Should |
| 기타 `TrapBase` 파생 | Should |

**공통**

| 대상 | 우선순위 |
|------|----------|
| 플레이어 HP·색·사망·리로드 | Must |
| `StageResetOnPlayerDeath` | Must |

---

## 10. Random · 시드

| 상황 | 시드 |
|------|------|
| **사망 리로드** | **매번 새 시드** (퍼즐 배치·랜덤 연출 변경) |
| **인원 변경 리로드** | **새 시드** |
| 로비 Start (첫 진입) | Host가 세션 시드 생성 |

대상: `StagePressurePadSetup`, `GameSessionColorDistribution`, `MouthController` 등 `Random` 사용처.  
Host 시드 기준 `InitState(seed + salt)` 통일.

---

## 11. 사망 · 리셋

- 멀티에서도 **`StageResetOnPlayerDeath`**: **1명 사망 = 전원 씬 리로드**.
- 리로드 후: 존 위 재스폰, `StageStartGate` 재진행, **새 시드**로 퍼즐 재배치.

---

## 12. 이탈 · 재접속 · Kick

> **데모 범위:** §12 **풀 스펙은 Post-Launch (v1.1)**. 데모는 아래 **§12.0 단순 규칙**만 적용.

### 12.0 데모 단순 규칙

| 상황 | 동작 |
|------|------|
| **호스트 이탈** | 세션 종료 → 전원 타이틀 |
| **클라이언트 이탈** | Host: **즉시 씬 리로드** (인원 변경 + 새 시드). 재접속 UI 없음 |
| **Kick** | 데모에서 **미구현 가능** (정식 또는 v1.1) |

### 12.1 공통 (정식 / v1.1 목표)

- 유예 시간: **60초**
- 일시정지: **`Time.timeScale = 0`** (전역 정지)
- 인증: LAN **`clientId`**, Steam **`SteamID`**
- **호스트 이탈 = 방 종료**

### 12.2 UI (호스트 결정)

- 이탈 시 UI 표시: 재접속 대기 vs **「계속 진행」**
- **「계속」** → **즉시** 슬롯 포기 → 인원 변경 → **씬 리로드** + `GameSession` 재적용

### 12.3 재접속 (전 구간)

- **같은 슬롯** (같은 색, 같은 `playerId`)으로 복귀.
- **60초 내** 재접속 + 스냅샷 있음:
  - **직전 위치·회전 복원** → **리로드 없이** 재개, `timeScale` 복구
  - 스냅샷: **위치, 회전, HP, 색 상태** (`isBlack`, `isUniqueColor` 등)
- 스냅샷 없음 / 유예 만료 / 호스트 「계속」 / Kick:
  - 인원 변경 처리 → **씬 리로드**
- **재접속 성공으로 인원이 복구**(3→4)되는 경우 포함 → **씬 리로드**

### 12.4 Kick

- **즉시 슬롯 비움** (유예 없음).
- 남은 인원에게 **동일 이탈 UI·리로드** 흐름.

### 12.5 중간 이탈 후 게임 지속

- **남은 인원으로 계속** (예: 4→3).
- 인원 변경 시: **씬 리로드** + `GameSession` 인원·색 재파악.

---

## 13. 체크포인트 · 스폰 (MVP)

- `M.Stage1` / `T.Stage1`에는 **별도 체크포인트 세이브 없음**.
- 스폰/리스폰 기준: **`ColoredStartZone.spawnPoint`**.
- `ColoredStartZone` 점유 시 해당 존이 **그 스테이지 내 리스폰 위치** (`ForceSetSpawnPoint`).
- 추후 체크포인트 추가 시: 있으면 체크포인트, 없으면 존 스폰.

---

## 14. End.Demo

- **새 씬 `End.Demo`** (검은 화면 + UI만).
- 멀티/솔로 공통.
- UI: 타이틀 복귀 버튼 → §8 타이틀 복귀 규칙.

---

## 15. 에디터 · 씬 작업 (확정)

| 작업 | 담당 |
|------|------|
| `GameSession`, `SceneFlowManager` → `0.Title` | **수동 (기획/에디터)** |
| `M.Stage1` 씬 내 Player 프리팹 인스턴스 제거 | 구현 시 |
| Network Player Prefab 생성 + NetworkManager 등록 | 구현 시 |
| `0.Title` 오프라인 패널 (드롭다운 + 시작) | 구현 시 |
| `End.Demo` 씬 생성 (Build Settings 등록) | 구현 시 |
| `sceneSequence`에 `End.Demo` 추가 | `SceneFlowManager` |

---

## 16. 구현 순서 (권장)

### 16.1 네트워크 · 응원 · Steam (데모)

> **현재 실행 체크리스트:** §0.5 (음성 시스템 완료 이후 기준). 아래는 초기 구현 단계 요약.

1. NGO + `UnityTransport` + Title `NetworkManager`
2. 로비 Ready / 캐릭터 / Start 동기화
3. Player Network Prefab + 존 스폰 + Owner 입력·카메라
4. **Must 동기화** (§9 표)
5. **ParrelSync ①**
6. **Development Build ②** — localhost **2인** (중간 게이트)
7. **응원** — CheerService, Dissonance, Vosk (`CheerAndTutorialDesign.md`)
8. **Steamworks** — P2P transport, Lobby, Depot
9. **Steam ④** — **2인 Must** + 4인 1회 권장 → **Steam 데모 출시**
10. `End.Demo` + 솔로 경로 + Should 항목 (여유분)

### 16.2 데모 후

> 범위·일정은 데모 출시 후 별도 확정.

1. 난이도 밸런싱 (데모 피드백)
2. Tutorial + CheerName 커스텀
3. UI 옵션 (볼륨·해상도)
4. Steam Invite UX polish
5. 출시 QA
6. (선택) §12 재접속·Kick — v1.1

### 16.3 Post-Launch

- Steam Invite UX polish
- 컷씬, sit/dance 이모트
- §12 풀 스펙, Late Join
- 호스트 마이그레이션

---

## 17. Post-Launch (참고)

- §12 재접속·Kick·스냅샷 풀 스펙
- 컷씬, 캐릭터 이모트 (sit, dance)
- `PostMVP_Multiplayer_Backlog.md` (Late Join, 세이브 슬롯 등)
- 호스트 마이그레이션

---

## 18. FAQ (설계 중 합의)

**Q. 솔로 로비 씬이 따로 필요한가?**  
A. **아니오.** Title 오프라인 패널에서 색 선택 후 바로 `M.Stage1`.

**Q. 솔로 색상은 어디서 고르나?**  
A. **Title 오프라인 패널** 드롭다운 1개. 로비 안 거침.

**Q. 다른 플레이어를 내 PC에서 조종하나?**  
A. **아니오.** Owner는 자기 캐릭터만. 타인은 네트워크로 **보이기만** 함.

**Q. Host 판정이면 Client 화면에 안 보이나?**  
A. **보인다.** Host가 판정한 **결과**를 동기화해 전원이 같은 상태를 봄.

**Q. 타이틀 복귀 시 멀티 연결은?**  
A. **`NetworkManager.Shutdown()`** 으로 해제.

**Q. ParrelSync만 통과하면 데모 출시해도 되나?**  
A. **아니오.** **Steam P2P ④** (2인 Must + 4인 1회 권장) + 응원·보이스 통과가 데모 게이트.

**Q. Dev Build ②만 통과하면 데모 출시?**  
A. **아니오.** ②는 **중간 게이트**. 데모 = **Steam 원격** + 협동 + 응원.

**Q. 개발 PC 2대뿐인데 4인 테스트?**  
A. 일상 = **Steam 2인** (§0.2.1). 데모 직전 **4인 1회** — 친구/플레이테스트 권장. 2인 통과 ≠ 4인 100% 보장.

**Q. 2인 OK면 4인도 OK?**  
A. **연결·Transport·응원 골격**은 2인에서 대부분 검증. **4인 전용** (3표 집계, 4보이스, 4Gate)은 4인 1회 필요.

**Q. LAN discovery 없이 테스트하나?**  
A. ② localhost. **플레이어 멀티 = Steam P2P** (LAN 데모 아님).

**Q. 2주 안에 컷씬·이모트·재접속 풀스펙을 넣어야 하나?**  
A. **아니오.** 정식 Must는 Tutorial·밸런싱·옵션 UI·QA. Steam P2P는 **데모에서 이미 Must**.
