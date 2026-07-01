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
| 7 | **Steamworks 연동** | Transport·Lobby·빌드·Depot (§0.3) |
| 8 | **컷씬** | **2주 일정에 넣지 않음** → 출시 후 |
| 9 | **출시 QA** | 빌드·Steam·2~4인 시나리오 체크리스트 |

### 0.2 네트워크 테스트 단계 (개발자 환경)

**LAN 실기 테스트는 현재 불가.** 아래 순서로만 검증한다.

```
① ParrelSync (에디터 Host + Clone Client)
   → Host 화면 포커스 기준 빠른 반복. 버그 발견·수정용.
   ※ ParrelSync만으로 “완료” 판정 금지. Clone/에디터 한계로 오탐·미탐 많음.

② Development Build (Host EXE + Client EXE, localhost 또는 같은 PC)
   → 양쪽 화면 포커스하며 실제 플레이. **데모 출시 전 필수 통과.**

③ Steam P2P (정식 출시 직전)
   → Transport 교체 + Steam Lobby. 친구/외부 1회 이상 실연.
```

| 단계 | 목적 | 통과 기준 (최소) |
|------|------|------------------|
| ParrelSync | 구현·버그 수정 속도 | Title→Lobby→M→T→End 2인 클리어 1회 |
| 빌드 | 실제 출시 품질 | 2인·4인 각 1회 클리어, 사망 리로드 1회, 스테이지 전환 OK |
| Steam P2P | 배포 환경 | ②와 동일 시나리오 Steam에서 1회 |

**Transport:** 코드상 LAN(`UnityTransport`) 유지 가능. **검증은 ② 빌드부터** 신뢰한다.

### 0.3 데모 vs 정식 출시 — 어디까지 끝내나

#### 데모 출시 (Must Have)

- **플레이 경로:** Title → Lobby → `M.Stage1` → `T.Stage1` → `End.Demo` (멀티 2~4인)
- **솔로:** Title 오프라인 패널 → 동일 스테이지 (NGO 없음)
- **네트워크:** §9 **핵심 동기화만** — 플레이어, HP/사망, StageStartGate, Phase, 함정 발사·피격, T.Stage 패드·문·Boulder, 클리어
- **네트워크 검증:** ② **빌드 Host/Client** 통과 (ParrelSync만으로 출시 금지)
- **UI:** 타이틀·로비·HP·카운트다운·End.Demo (옵션 패널 **없어도 됨**)
- **사운드:** BGM 1~2 + 핵심 SFX (발사, 피격, Break, UI 클릭)
- **파티클:** 피격·Break만 (없어도 데모 가능, 있으면 이 두 개만)
- **난이도:** “클리어 가능” 수준만. **본격 밸런싱은 데모 후**

#### 데모에서 **의도적으로 빼는 것** (버그·일정 방어)

| 항목 | 이유 |
|------|------|
| §12 재접속·Kick·60초 유예·스냅샷 복원 | 복잡도 최상. 데모: **호스트 이탈 = 종료**, 중간 이탈 = **즉시 리로드** 수준으로 단순화 |
| LAN UDP discovery 실기 검증 | 개발 환경 불가. **빌드 localhost + IP 직접 입력**으로 대체 |
| Steam P2P | 데모는 **빌드 LAN/localhost**로 배포 가능. Steam은 정식 직전 |
| sit / dance 등 이모트 애니 | 정식 이후 |
| 컷씬 | 정식 이후 |
| 옵션·설정 UI 전체 | 정식에서 (데모: OS 볼륨으로 대체) |

#### 정식 출시 (데모 후 2주 — Must Have)

- ③ **Steam P2P** + Steam Lobby (Invite는 있으면 좋음, 없으면 코드 Join)
- **난이도 밸런싱** (데모 피드백 반영)
- **UI:** 옵션(마스터·BGM·SFX), 해상도/전체화면 최소
- **출시 QA** 체크리스트 전 항목
- **Steamworks:** App ID, Depot, 빌드 업로드, SteamPipe

#### 정식 2주 안에 **넣지 않는 것** (Post-Launch)

- 컷씬
- sit / dance / 이모트 애니메이션
- §12 재접속 풀 스펙 (스냅샷 무리로드 없이 재개 등) — 여유 있으면 v1.1
- Late Join, 호스트 마이그레이션
- 파티클 대량 추가

### 0.4 권장 작업 순서 (실행용)

```
[데모 전]
1. 네트워크 핵심 동기화 (§9 Must) + Spawner Spawn 패턴
2. ParrelSync로 버그 수정 (①)
3. Development Build ② 통과 — 여기서 데모 출시 가능
4. 사운드·파티클(피격/Break) — 병행 가능, 네트워크 ② 전에는 최소만

[데모 후 ~ 정식 2주]
주 1: 데모 피드백 → 난이도 → Steam Transport/Lobby → UI 옵션
주 2: Steamworks 빌드·업로드 → QA → 출시
(컷씬·이모트·재접속 풀스펙은 출시 후)
```

---

## 1. 기술 스택

| 항목 | MVP | 이후 |
|------|-----|------|
| 네트워크 | **Netcode for GameObjects (NGO)** | 동일 |
| 연결 | **LAN** (`UnityTransport`, 포트 **7777**) | **Steam P2P** |
| 권한 | **호스트 권한** | 동일 |
| 최대 인원 | **4인** (호스트 포함) | 동일 |

- Transport는 교체 가능하게 분리 (LAN `UnityTransport` ↔ Steam Networking).
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

### 4.1 LAN / localhost (코드·데모 배포)

| 항목 | 값 |
|------|-----|
| 포트 | **7777** (고정) |
| 방식 | **개발:** localhost 또는 IP 직접 입력 (빌드 ②) |
| Discovery | UDP 브로드캐스트 — **실기 LAN 검증 불가**. 코드 유지, 데모는 IP/localhost 우선 |
| Join UX | **6자리 숫자 룸코드 입력** (또는 Host IP 직접 입력 폴백) |
| Host | 랜덤 6자리 코드 생성 (+ broadcast는 Steam/LAN 실기 환경에서만 의미) |
| 실패 | **「방을 찾을 수 없음」** (타임아웃 후) |

**룸코드 UI (LAN)**

- 표시: 앞 2자리 + `**` + 뒤 2자리 (예: `12**56`)
- 복사: **전체 6자리** 클립보드 (`125634` 등)

**개발자 테스트:** ParrelSync(①) → **Development Build Host+Client(②)**. 같은 PC에서 Client는 `127.0.0.1:7777` Join.

### 4.2 Steam (정식 출시 직전, ③)

- 실제 연결은 Steamworks P2P.
- UI 마스킹 예: 포트/식별자 `7**1` 형태 (중간 비표시).
- 룸코드: **랜덤 생성** (Steam Lobby ID 등과 매핑).
- Join UX는 LAN과 동일하게 **코드 입력** 유지.

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

### 16.1 네트워크 (데모 전)

1. NGO + `UnityTransport` + Title `NetworkManager`
2. 로비 Ready / 캐릭터 / Start 동기화
3. Player Network Prefab + 존 스폰 + Owner 입력·카메라
4. **Must 동기화** (§9 표): StageStartGate, Phase, Trap Spawn, T.Stage 패드·문·Boulder, Breakable, 클리어, HP/사망
5. **ParrelSync ①** — Must 항목 버그 수정
6. **Development Build ②** — 데모 출시 게이트
7. `End.Demo` + 타이틀 복귀 + 멀티 Shutdown
8. Title **오프라인** 패널 + 솔로 경로
9. Should 항목 (Mouth, Wind, Buff 등) — ② 통과 후 또는 데모 직전 여유분

### 16.2 데모 후 ~ 정식 (2주)

1. Steam Transport + Lobby (③)
2. 난이도 밸런싱 (데모 피드백)
3. UI 옵션 (볼륨·해상도)
4. Steamworks Depot·업로드
5. 출시 QA
6. (선택) §12 재접속·Kick — **2주에 못 넣으면 v1.1**

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
A. **아니오.** Development Build Host+Client(§0.2 ②) 통과가 데모 게이트.

**Q. LAN discovery 없이 테스트하나?**  
A. **localhost / IP 직접 Join**으로 빌드 테스트. discovery는 Steam·실제 LAN 환경에서 검증.

**Q. 2주 안에 컷씬·이모트·재접속 풀스펙을 넣어야 하나?**  
A. **아니오.** 정식 Must는 Steam P2P, 밸런싱, 옵션 UI, QA. 나머지는 Post-Launch.
