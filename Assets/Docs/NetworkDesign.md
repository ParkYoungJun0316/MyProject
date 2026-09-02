# Network Design (MVP)

네트워크 동기화 아키텍처 문서 — 권한(Authority)·룸/세션·플레이어·스테이지 진행·챌린지 축의 SSOT.  
**출시 일정·범위·QA 체크리스트는 [`ReleaseRoadmap.md`](ReleaseRoadmap.md), 텔레메트리 스펙은 [`TelemetryDesign.md`](TelemetryDesign.md) 참고.**  
**데모 / Playtest 없음.** 목표 = **2026-09-01 Steam 정식 출시**만.  
스테이지 범위: **`M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`**.  
(`End.Demo` = 클리어 UI 씬명 레거시. 리네임은 별도 작업.)

---

## 1. 기술 스택

| 항목 | 개발 ①② | **정식 배포 ④** |
|------|---------|-----------------|
| 네트워크 | **NGO** | **NGO** |
| 연결 | `UnityTransport` **localhost** (**7777**) | **Steam P2P + Lobby** |
| 권한 | §9.0 매트릭스 (**이동=Owner+CNT**, 판정=Host, 발사체 비행=Client B안) | 동일 |
| 최대 인원 | 4인 | 4인 |

- Transport **교체 가능**하게 분리 (`UnityTransport` ↔ Steam Networking). **정식 = Steam transport 필수.**
- 중간 참가(Late Join) **없음**. 재접속 **미지원**. 호스트 마이그레이션 **없음**.
- **이탈 정책:** Host 또는 Client **누구든** 나가면 **즉시 방 종료** → 전원 타이틀. 남은 인원으로 계속·재입장 **없음**.

---

## 2. 씬 흐름

### 2.1 멀티플레이

> **⭐ 2026-08-17 확정 — `1.Lobby` 씬 폐지.** 로비가 하던 일(Kick·색 선택·Ready·Start·Steam Invite UI)은 전부 `Tutorial` 씬 앞부분(사전 게이트 구간)으로 흡수됐다. **상세 SSOT는 §6B** — 이 절 표는 씬 시퀀스 개요만. 씬 파일명도 숫자 prefix 없이 `Title`/`Tutorial`로 정리(실제 리네임은 에디터 작업, 사용자 담당).

```
Title → Tutorial → M.Stage1…5 → M.Boss → T.Stage1…5 → T.Boss → End.Demo → Title
```

| 씬 | 역할 |
|----|------|
| `Title` | `NetworkManager`, `GameSession`, `SceneFlowManager` (DDoL), Host/Join, Steam Invite 수락(아직 방 안 만든 상태에서만) |
| `Tutorial` | **Ship Must** — **사전 게이트 구간(구 Lobby 역할 흡수, §6B)** + 조작·CheerName·응원 체험 + `TutorialGatherZone` |
| `M.Stage1`…`M.Stage5` / `M.Boss` | M 바이옴 + 보스 |
| `T.Stage1`…`T.Stage5` / `T.Boss` | T 바이옴 + 보스 |
| `End.Demo` | 클리어 UI → 타이틀 복귀 (씬명 레거시) |

`SceneFlowManager.sceneSequence` 권장 순서:  
`Tutorial`, `M.Stage1`…`M.Stage5`, `M.Boss`, `T.Stage1`…`T.Stage5`, `T.Boss`, `End.Demo`.

### 2.2 솔로 (1인 Host)

```
Title → Tutorial (Host 1인, TutorialGatherZone 즉시 통과) → (동일 스테이지 시퀀스) → End.Demo → Title
```

- **NGO 사용.** `LobbyMode.OnlineHost` + `partySize=1`. 멀티와 동일 코드 경로.
- `TutorialGatherZone`은 헤드카운트 비교 방식(§6B.3)이라 Host 1인이면 "존 안 인원 1 == 접속 인원 1"이 즉시 성립 → 카운트다운 즉시 시작.
- 1인 전용 규칙: `GameSession.ActivePlayerCount == 1`
  - `CheerService.ValidateCheer`: self-cheer 허용
  - `GetRequiredVotes()`: `max(1, 0) = 1` → 1표로 버프 발동

---

## 3. DontDestroyOnLoad (Title부터)

`Title`에 배치 후 세션 종료까지 유지:

| 오브젝트 | 비고 |
|----------|------|
| `NetworkManager` | 멀티·솔로 공통 활성 (솔로 = Host 1인) |
| `GameSession` | 인원·활성 색. 에디터에서 Title로 이동 (수동) |
| `SceneFlowManager` | 씬 시퀀스. 에디터에서 Title로 이동 (수동) |

---

## 4. 연결 · 룸코드

### 4.1 localhost (개발 ①② 전용 — ParrelSync / 같은 PC 빌드)

| 항목 | 값 |
|------|-----|
| 포트 | **7777** (고정) |
| 용도 | **ParrelSync ①**, **Dev Build ②** (같은 PC Host/Client EXE) |
| Join | `127.0.0.1:7777` 또는 6자리 룸코드 UI (개발용) |

**※ 원격 IP Join·실사용 LAN(물리적으로 분리된 2대 PC) 테스트는 하지 않는다.**  
**※ Steam 정식 배포·플레이어 멀티에는 사용하지 않음** → §4.2.

**개발자 테스트:** ParrelSync(①) → Dev Build ② (같은 PC 2 exe). **실제 테스트 가능한 방법은 이 2가지뿐** — 상세: §6A.3.

> **코드 참고:** `LanDiscovery`(UDP 47777, 룸코드→IP 해석)가 존재하나, 이는 같은 PC/세션 내 편의 기능일 뿐 **물리적으로 분리된 2PC 간 실사용 LAN 연결 테스트 수단이 아니다** (미지원/미검증). Steamworks 연동 전까지 개발자 검증은 ①②로만 한다.

### 4.2 Steam P2P + Lobby (**Ship Must**, `ReleaseRoadmap.md` §3 ④)

- **Steamworks** 초기화 + **Steam Networking** transport + **Steam Lobby**.
- **Join: Steam 초대 전용, 룸코드 없음 (2026-08-17 확정).** 친구 목록에서 "Join Game" 또는 게임 내 Invite 버튼(`ActivateGameOverlayInviteDialog`류) → 오버레이 초대 수락. 비친구는 오버레이 "Copy Link"로 얻은 `steam://joinlobby/...` 링크 공유로 대체(Discord 등) — 코드 타이핑 UI 자체가 없음.
- **근거:** 이 게임은 사적 파티 초대 전용(공개 매칭 없음) — Deep Rock Galactic(Friends Only)/Risk of Rain 2/Overcooked 2 등 동일 조건의 Steam 코업 게임들도 전부 오버레이 초대만 쓰고 코드 UI가 없다. 이 프로젝트의 `TitleMenuController.OnSteamInviteAccepted`(온기동)/`TryAutoJoinFromLaunchArgs`(`+connect_lobby`, 냉기동)가 이미 코드 없는 조인 경로를 완성해뒀다 — 룸코드 입력(`ConfirmJoinSteam`, 64비트 Lobby Id 직접 타이핑)은 그 위에 얹힌 중복 백업 경로였을 뿐.
- **Depot 업로드** 후 Steam 클라이언트에서 실행 — **원격 2~4인** 협동·응원·보이스 검증 환경.
- **개발자 2PC:** 일상 QA = **2인** Steam Join. 출시 전 **4인 1회** 권장 (`ReleaseRoadmap.md` §3.1).

---

## 5. 타이틀 UI

> **2026-08-17 갱신 — `1.Lobby` 폐지로 목적지가 `Tutorial`로 변경.** Host/Client 구분 없이 접속하면 즉시 `Tutorial`에 캐릭터가 스폰되고 자유 이동 가능(§6B). Room code·Steam Invite 버튼은 더 이상 로비 UI가 아니라 `Tutorial` 내 상시 HUD로 이동. 아직 방을 만들기 **전**(Title 화면 대기 중) Invite 수락 흐름은 기존과 동일하게 `TitleMenuController` 경유.

| 버튼 | 동작 |
|------|------|
| 게임 만들기 | Host → `Tutorial` (즉시 캐릭터 스폰, NGO `OnlineHost`) |
| 게임 참여 (로컬 ①②만) | Client, 6자리 룸코드 입력 → `Tutorial` (즉시 캐릭터 스폰). **Steam(④) 경로에는 이 버튼이 없다** — §4.2 |

> **솔로 전용 버튼 없음 (2026-08-17 정리).** 이전 버전에 "게임 만들기 (솔로 포함)"이 별도 행으로 있었으나 "게임 만들기"와 동작이 동일해 중복이었다 — 삭제. 솔로/멀티는 버튼으로 나뉘지 않고 `TutorialGatherZone` 통과 시점의 접속 인원수로 자동 결정된다(§2.2, §6B.3) — Host 혼자면 솔로, 누가 더 합류하면 멀티.
>
> **Steam 경로 "게임 참여" 버튼·룸코드 입력 UI 완전 제거 (2026-08-17 확정, §4.2).** `TitleMenuController`의 `joinPanel`/`roomCodeInputField`/`ConfirmJoinSteam`은 Steam(④) 빌드에서 아예 쓰지 않는다 — 조인은 오버레이 초대 수락(`OnSteamInviteAccepted`) 또는 `+connect_lobby` 냉기동(`TryAutoJoinFromLaunchArgs`)으로만 발생, 둘 다 이미 구현되어 있어 기능 손실 없음. **로컬 ①②(ParrelSync/Dev Build) 경로는 그대로 유지** — Steamworks가 없는 순수 개발 테스트 인프라라 이번 결정과 무관.

---

## 6. 로비 규칙 — **폐지 (2026-08-17, `1.Lobby` 씬 삭제)**

> `1.Lobby` 씬 자체가 없어졌으므로 이 절의 Ready/Kick/캐릭터 선택 UI 규칙은 전부 폐기. **후속 SSOT는 §6B(Tutorial 사전 게이트 구간)** — 신규 작업·진단은 §6B를 본다. 이 절 번호는 상위 문서 상호참조 보존을 위해 자리만 유지.

---

## 6A. 룸·세션 수명주기 축 (SSOT)

> **한 줄:** 진입(Connect) → 로비(Lobby) → 시작 게이트(Start Gate) → 인게임(InGame — 내부에서 §11 플레이어 축이 씬마다 반복 재진입) → 종료(Leave/SessionEnd) → ①로 재진입.
> 이탈·클리어·Host Quit **전부 같은 종료 문(⑤)**으로 들어간다. 평행 종료 경로 없음.
> §4·§5·§6(폐지)·§6B·§12는 각 칸의 **세부 규칙**이고, 이 절은 그 위의 **축 골격**이다. 세부는 해당 절 참조, 중복 서술하지 않음.
>
> **⭐ 2026-08-17 확정 — `1.Lobby` 씬 폐지.** 축 이름(①②③④⑤)은 그대로 유지되지만, **②Lobby 칸의 물리적 위치가 `Tutorial` 씬 안으로 이동**했다. 즉 `Tutorial` 씬에 들어가서 `TutorialGatherZone`을 통과하기 **전까지**는 전부 ②Lobby 칸(자유이동 + 연습 3존 포함)이고, 그 게이트를 통과하는 순간이 ③Start Gate이며 그 뒤(M.Stage1~)부터 ④InGame이다. **Tutorial 자체를 ④InGame으로 착각하지 말 것** — 상세는 §6B.

### 6A.0 축 (5칸 · 일방통행)

```
① Connect → ② Lobby(= Tutorial 사전 게이트 구간, §6B) → ③ Start Gate(= TutorialGatherZone) → ④ InGame(M.Stage1~) → ⑤ Leave/SessionEnd
                                                        │
                          (이탈·Host Quit·클리어 전부 여기로 재진입) ── ①로 (Title)
```

| 칸 | 불변식 (칸이 끝나면 참) | Writer (여기만 진실) | 상세 |
|----|------------------------|----------------------|------|
| ① Connect | Host 시작 또는 Client 접속 완료(로컬: 룸코드, Steam: 초대 수락/`+connect_lobby`) | `NetworkManagerSetup` (`StartHost`/`StartClient`류) — `TitleMenuController` 경유만 | §4, §5 |
| ② Lobby | `Tutorial` 씬 진입 즉시 캐릭터 스폰, 색 자동배정(중복없음), 자유이동 + 연습 3존. 이 구간 이탈=슬롯(캐릭터)만 제거(**방 유지**) | `TutorialNetworkManager`(가칭, 구 `LobbyNetworkManager` 역할 이전) 유일 | §6B |
| ③ Start Gate | 접속 중인 전원이 `TutorialGatherZone`에 동시 체류 → 카운트다운 완료. 통과 시점부터 인원 **동결** | `TutorialNetworkManager`(게이트 컴포넌트) 유일 | §6B.3 |
| ④ InGame | 세션 진행 중(M/T 스테이지+보스). 룸 구성(인원) **불변** — 이 구간엔 kick/late join/재접속 없음 | 없음(룸 레벨) — 씬 단위 진실은 §11 플레이어 축이 담당 | §11 |
| ⑤ Leave/SessionEnd | 이탈(Host/Client 누구든)·Host Quit·클리어 전부 같은 문. `Shutdown` + 세션 리셋 후 ①로 재진입 | `TitleReturnFlow.Request` 유일 (`ExecuteReturn`은 내부) | §12 |

### 6A.1 ⑤로 들어오는 문 — 전부 `TitleReturnFlow.Request` 경유

| 문 | 경로 | Reason |
|----|------|--------|
| 클리어 | `EndDemoController` → `End.Demo` 복귀 버튼 | `EndDemo` (`FullRunReset`) |
| Client 이탈(본인이 끊김을 감지) | `DisconnectManager.OnClientLeft` | `ClientDisconnected` |
| Host 이탈/Quit | `DisconnectManager.OnClickLeaveRoom` → 타 Client에 `NotifyAllReturnClientRpc` 통지 | `HostQuitRoom` |
| Tutorial 사전 게이트 구간 Quit | Tutorial 상시 HUD의 나가기 버튼 (구 `LobbyMenuController.OnClickQuit` 역할 이전, 클래스명은 구현 시 확정) | `LobbyQuit` |
| Tutorial 사전 게이트 구간 중 연결 끊김 | 위와 동일 컴포넌트의 `OnNetworkDisconnected` 역할 | `ClientDisconnected` |

이 문들 **외**에 `NetworkManager.Shutdown()` 직접 호출 금지 — 전부 `NetworkManagerSetup.Shutdown()`을 거치고, 그 호출은 `TitleReturnFlow.ExecuteReturn` 내부 1곳뿐이어야 한다.

### 6A.2 Tutorial 사전 게이트 구간 이탈 vs 인게임 이탈 — 구분 (혼동 금지)

> **2026-08-17 확정 — Kick 기능 자체를 완전히 폐지한다.** 구 로비의 "Host가 대상을 지정해 강퇴" 기능은 Tutorial 사전 게이트 구간에도 이식하지 않는다. Host는 특정 Client를 강제로 내보낼 수 없다 — 있는 것은 **자연 이탈**(연결 끊김/본인 Quit)뿐이다.

| | Tutorial 사전 게이트 구간 이탈 (②) | 인게임 이탈 (⑤) |
|--|--|--|
| 트리거 | 누구든 연결 끊김/Quit (Host가 강제로 내보낼 수 없음) | 누구든 연결 끊김/Quit |
| 결과 | 캐릭터(슬롯)만 제거, **방 유지**, 남은 인원은 계속 자유이동/게이트 대기 | **방 전체 종료** → 전원 타이틀 |
| API | `TutorialNetworkManager`의 `OnClientLeft`류 (구 `LobbyNetworkManager.OnClientLeft` 역할 이전) | `DisconnectManager` → `TitleReturnFlow` |
| Host 이탈 시 | **방 전체 종료** (Host는 ②에서도 예외 없음 — 6A.2-표와 별개로 6B.4 참조) | **방 전체 종료** |
| 인게임에 존재하는가 | **아니오** — Tutorial 사전 구간 전용 | 이게 인게임에서 "누가 빠지는" **유일한** 경로 |

**Kick(강퇴/Ban)은 존재하지 않고, 앞으로도 추가하지 않는다** — 로비였을 때도, 지금 Tutorial 사전 구간이 되어서도 마찬가지다. 인게임에서 누군가 빠지면(자의든 타의든, 인터넷 문제·개인 사정 등) 이는 **"이탈"**이며, §12 규칙대로 **방 전체가 종료**된다 — "Kick"이라는 별도 기능이 아니다.

### 6A.3 개발 환경 연결 방식 — 실제 가능한 것만 (§4.1 보강)

현재 실제로 검증 가능한 개발자 테스트 방법은 **2가지뿐**이다 (`ReleaseRoadmap.md` §3와 동일):

| 방법 | 실제 동작 |
|------|----------|
| ① ParrelSync | 에디터 Host + Clone Client, **같은 PC** |
| ② Dev Build | Host EXE + Client EXE, **같은 PC** localhost:7777 |
| 물리적으로 분리된 2PC 간 LAN 연결 | **테스트 안 됨 / 미지원** |
| ④ Steam P2P | 아직 미구현 |

`LanDiscovery`(UDP 47777, 룸코드→IP 해석)가 코드에 있지만, 이는 같은 PC/세션 안에서의 편의 기능이고 **실사용 LAN 2PC 연결 테스트 수단이 아니다.** Steamworks(`ReleaseRoadmap.md` §3 ④)가 붙기 전까지 개발자 검증은 **①②만** 사용한다.

### 6A.4 금지 (평행 경로 — 발견 즉시 삭제)

| 항목 | 이유 |
|------|------|
| Kick(강퇴) 기능 추가 (로비였을 때도, Tutorial 사전 구간에도) | 완전 폐지 (§6A.2, §6B.4). 어디에도 만들지 않음 |
| Late Join / 재접속 / 호스트 마이그레이션 | §12 미지원 정책. 코드에도 없음(확인됨) — 추가 금지 |
| **Tutorial 사전 게이트 구간의 동적 합류를 "Late Join"으로 착각** | 이는 ②Lobby 칸의 정상 동작(§6B) — Late Join은 ④InGame(M/T 스테이지) 중 재접속을 뜻함. 서로 다른 개념, 혼동 금지 |
| `NetworkManager.Shutdown()` 직접 호출 | ⑤ Writer(`TitleReturnFlow`) 우회 — 금지 |
| `TutorialNetworkManager`의 사전 구간 이탈 경로를 인게임 이탈에 재사용 | ②/⑤ 별도 유지, 섞지 말 것 |
| 인게임(④) 중 인원/색 변경 | ③ Start Gate 통과 후 동결 |
| 실사용 LAN 2PC 연결을 정식 테스트/배포 수단으로 취급 | §6A.3 — 미지원. ①②만 |

### 6A.5 증상 → 볼 칸 (진단 사다리)

| 증상 | 먼저 볼 칸 |
|------|-----------|
| `TutorialGatherZone`에 다 모였는데 카운트다운 안 시작 | ③ 헤드카운트 비교 로직 (접속 인원 수 vs 존 안 인원 수 불일치, §6B.3) |
| Tutorial에서 나갔는데 방이 통째로 터짐 | ②/⑤ 혼동 — 지금 게이트 통과 전인지 후인지, 호출된 게 `TutorialNetworkManager`의 사전구간 이탈 경로인지 `DisconnectManager`인지 확인 |
| 인게임 중 한 명 나갔는데 계속 진행됨 | ⑤ `DisconnectManager.OnClientLeft` 콜백 등록 여부 |
| 타이틀로 안 돌아가고 멈춤 | ⑤ `TitleReturnFlow.Request` 호출 여부 / `Shutdown` 완료 여부 |
| "인게임에서 Kick하고 싶다"는 요청 | 의도된 미지원(§6A.2) — 버그 아님, 구현하지 않음 |
| 재접속/Late Join 요청 (M/T 스테이지 중) | 의도된 미지원(§12) — 버그 아님, 구현하지 않음. **Tutorial 사전 구간의 동적 합류와 다른 개념** |
| 개발 중 다른 PC로 접속이 안 됨 | §6A.3 — 의도된 제약. ①②만 사용, LAN 실사용 기대하지 말 것 |

### 6A.6 검증 (ParrelSync 2인)

1. Title → Host 생성 → Client 룸코드 접속 → `Tutorial` 진입 즉시 캐릭터 스폰 확인, 색 중복 없음 확인 (①→②)
2. 전원 `TutorialGatherZone`에 진입 → 카운트다운 → M.Stage1 전환 확인 (③)
3. 인게임 중 Client 강제 종료(연결 끊기) → Host 포함 전원 타이틀 복귀 확인 (⑤)
4. 인게임 중 Host 종료 → Client `NotifyAllReturnClientRpc` 수신 후 타이틀 복귀 확인 (⑤)
5. 클리어(`End.Demo`) → 타이틀 복귀 버튼 → `GameSession`/`SceneFlowManager` 리셋 확인 (⑤→①)
6. `grep`: 게임 코드 내 `NetworkManager.Shutdown()` 직접 호출 — `NetworkManagerSetup` 내부 1곳 제외 **0건**

---

## 6B. Tutorial 사전 게이트 구간 (구 Lobby 흡수 — 2026-08-17 확정)

> **SSOT.** `1.Lobby` 씬이 하던 일(Kick·색 선택·Ready·Start·Steam Invite UI)의 후속 규칙은 전부 이 절에 모은다. Tutorial 세부 콘텐츠(연습 존 배치·CheerName/TeamCheerWord UX 등)는 `CheerAndTutorialDesign.md` §2~§3이 SSOT이고, 이 절은 **네트워크·수명주기 관점**만 다룬다 — 중복 서술 금지. 응원 버프 규칙 자체는 `CheerSystemDesign.md` SSOT (2026-09 문서 분리).

### 6B.1 배경 · 왜 없앴나

- 로비가 실질적으로 하던 일은 "인원 모이기 + 색 배정 + Start 대기"뿐이었고, 이 3가지 모두 Tutorial 진입 시점부터 자연스럽게 해결 가능(§6B.2~3).
- Kick은 파티가 사적·초대 전용이라 실사용 가치가 낮다고 판단해 **기능 자체를 폐지**(§6A.2).
- Steam Invite UI(룸코드 없음, §4.2·§6B.5)만 별도 씬 없이도 상시 HUD로 대체 가능.
- 씬 하나(로드/언로드, 전용 NetworkManager, 별도 UI 세트)를 통째로 없애 흐름을 단순화: `Title → Tutorial → M.Stage1…`.

### 6B.2 진입 · 스폰 · 색 배정

> **구현 참고 (2026-08-17 확인) — 이 절의 스폰 메커니즘은 신규 설계다.** 기존 `LobbyNetworkManager`는 실제 Player `NetworkObject`를 스폰한 적이 없다 — 로비는 `NetworkList<LobbyPlayerState>`(색·Ready·이름 데이터)만 관리했고, 실제 캐릭터는 `StartGameServerRpc` 시점에 `PlayerSpawnManager.InitializeOnline()` + 스테이지 씬 진입 시 `SpawnNetworkPlayers()`(4색 고정 좌표 배치 스폰)로만 생성됐다(`NetworkManagerSetup.ApproveConnection`의 `CreatePlayerObject = false` 주석이 이를 증명). 따라서 **"구 로비 로직 이전"은 색 배정(`GetNextFreeColorIndex()`류)에만 해당** — 접속 즉시 캐릭터 스폰 자체는 아래 표대로 새로 구현한다.

| 항목 | 규칙 |
|------|------|
| 캐릭터 스폰 시점 | Host/Client가 `Tutorial`에 접속(=Connect 완료)하는 **즉시** — 대기 없음 |
| 스폰 Writer/트리거 | **Host** `NetworkManager.OnClientConnectedCallback`(기존 `LobbyNetworkManager.OnClientJoined`와 동일 지점 재사용 — `EnableSceneManagement` 하에서 이 콜백은 해당 클라이언트의 씬 동기화 완료 후 발생하므로 안전) → `Instantiate` + `SpawnWithOwnership(clientId, destroyWithScene:true)` |
| `destroyWithScene` | **`true`** (2026-08-17 확정) — §11(M/T 스테이지)과 동일 원칙, §11.4 "DDOL 플레이어 가정 폐기"와 일치. `TutorialGatherZone` 통과 후 `M.Stage1` 로드 시 씬 언로드로 자동 Despawn되어 §11 ②Spawn(배치 스폰)과 충돌 없음 |
| 색 배정 | **자동, 접속 순서대로 겹치지 않는 색 1개** 할당 (기존 `LobbyNetworkManager.GetNextFreeColorIndex()`류 로직을 이전) |
| 색 해제 시점 | 이탈(`OnClientDisconnectCallback`) **즉시** — 기존 `OnClientLeft`가 슬롯을 즉시 제거하던 패턴과 동일, 유예 없음 |
| 색 재선택 UI | **없음** — 색 선택 화면 자체가 사라짐 (구 로비 캐릭터 선택 드롭다운 폐지) |
| 이동 | Owner + `ClientNetworkTransform`, §7.3과 동일. 스폰 즉시 자유이동 가능 |
| 이후 인원 추가 합류 | `TutorialGatherZone` 통과 **전까지** 언제든 가능 (Steam Invite 수락 시, §6B.5) — 이는 "Late Join"이 아니라 ②Lobby 칸의 정상 동작(§6A.4) |
| 최대 인원(4인) 시행 위치 | **이미 해결됨.** `NetworkManagerSetup.ApproveConnection()`이 `ConnectionApprovalCallback`에서 `current < maxConnections(4)`를 체크해 5번째 접속을 거부 — 씬과 무관하게 항상 적용되므로 Tutorial 전용 추가 로직 불필요 |

### 6B.3 `TutorialGatherZone` — 단일 색-무관 게이트

기존 §9.4/§11A의 `StageStartGate`+`ColoredStartZone`(색별 지정 구역) 방식이 아니라, **색 구분 없는 단일 트리거 존**으로 단순화한다.

| 항목 | 규칙 |
|------|------|
| 존 개수 | **1개** (색별 4구역 아님) |
| 트리거 조건 | 존 안에 있는 플레이어 수 == **현재 접속 중인 플레이어 수** (헤드카운트 비교, 색 무관) |
| 통과 시 | 카운트다운(수 초) 시작 → 카운트다운 중 누가 이탈/신규 합류하면 **카운트다운 리셋** → 완료되면 인원 **동결**, `M.Stage1` 로드 |
| 동적 인원 대응 | 헤드카운트 비교라 인원이 늘거나 줄어도 별도 로직 불필요 — "존 안 인원 == 접속 인원"만 보면 됨 |
| Writer | `TutorialNetworkManager`(가칭) 내 게이트 컴포넌트. NGO 스타트 로직(시드 생성, `SceneFlowManager.LoadNextScene` 호출)도 여기서 수행 — 구 `LobbyNetworkManager.StartGameServerRpc` 역할 이전 |
| 솔로 | 1인이면 "존 안 1 == 접속 1"이 즉시 성립 → 카운트다운 즉시 시작 (§2.2) |

### 6B.4 이탈 정책 (게이트 통과 전)

| 상황 | 결과 |
|------|------|
| Client가 연결 끊김/Quit | 캐릭터(슬롯)만 제거, **방 유지** — 남은 인원은 계속 자유이동/게이트 대기 (구 로비 정책과 동일) |
| **Host**가 연결 끊김/Quit | **방 전체 종료** (호스트 마이그레이션 없음 — §12와 동일 원칙, 예외 없음) |
| Kick | **없음** — §6A.2 |

게이트 통과 **후**(= ④InGame 진입 후)는 이 절이 아니라 §12(이탈 → 방 전체 종료)가 적용된다.

### 6B.5 Steam Invite UI (**룸코드 없음** — 2026-08-17 확정, §4.2)

| 항목 | 규칙 |
|------|------|
| 위치 | `Tutorial` 씬 내 **상시 HUD 패널** (구 로비 전용 화면이 아님 — 플레이 중에도 계속 보임) |
| Steam(④) 경로 표시 내용 | **Invite 버튼만.** 룸코드 표시 없음 — 클릭 시 오버레이 초대 다이얼로그(`ActivateGameOverlayInviteDialog`류) 오픈 |
| 로컬(①②) 경로 표시 내용 | 6자리 룸코드 텍스트만(개발 편의) — Steam Invite 버튼은 로컬 경로에 없음 |
| 표시 시점 | Tutorial 진입 ~ `TutorialGatherZone` 통과 전까지 |
| 게이트 통과 후 | HUD 숨김 (더 이상 인원 늘어날 수 없으므로) |
| 초대 수락 처리 | 게이트 통과 **전**까지 수락 가능 → 수락 시 §6B.2대로 즉시 스폰. 통과 **후** 수락은 무시(이미 인원 동결, §6B.3) |
| Title 화면에서 수락 | 기존과 동일 — `TitleMenuController.OnSteamInviteAccepted`/`TryAutoJoinFromLaunchArgs`가 처리 (Tutorial HUD와 무관, 이미 구현됨) |

### 6B.6 관련 코드 (이전 대상)

| 구 컴포넌트(로비) | 신 위치/이름 | 비고 |
|---|---|---|
| `LobbyNetworkManager` | `TutorialNetworkManager`(가칭, 리네임 확정) | `Tutorial` 씬에 배치. `OnClientJoined/Left`, 색 자동배정, 게이트 트리거, Start 로직 통합 |
| `LobbyMenuController` | Tutorial 상시 HUD 컨트롤러(가칭) | Steam 경로: Invite 버튼 + 나가기 버튼. 로컬 경로: 룸코드 표시 + 나가기 버튼 (§6B.5). Ready/색선택/Kick UI 전부 삭제 |
| `TitleMenuController`의 `joinPanel`/`roomCodeInputField`/`ConfirmJoinSteam` | **Steam 경로 삭제** | 로컬 경로(`ConfirmJoinLocal`)는 유지. Steam 조인은 오버레이 초대/`+connect_lobby`로만(§4.2) |
| `1.Lobby` 씬 | **삭제** | 사용자가 에디터에서 삭제 |
| `StageStartGate` / `ColoredStartZone` (Tutorial용) | `TutorialGatherZone` | 색 구분 없는 단일 존으로 대체. **M/T 스테이지의 색별 게이트는 영향 없음** — 그쪽은 계속 `StageStartGate`/`ColoredStartZone` 유지 |

**주의 (2026-08-17 확인):** 위 표의 "이전"은 `LobbyNetworkManager`가 실제로 갖고 있던 로직(색 자동배정 `GetNextFreeColorIndex()`, 접속/이탈 콜백 등록 패턴)에만 해당한다. **캐릭터 스폰 자체(`Instantiate`+`SpawnWithOwnership`)는 구 로비에 없던 신규 로직**이다 — §6B.2 참고.

### 6B.7 구현 순서 (실행용 체크리스트 — 2026-08-17 확정)

> **이 절의 목적:** §6B(+§4.2 Steam 룸코드 폐지, §5 Title UI) 전체를 실제로 구현할 때 따라갈 순서. 세부 규칙은 여기서 다시 설명하지 않고 각 항목이 해당 절을 참조한다 — 이 체크리스트는 "무엇을 언제 하는지"만 다룬다. **다음 세션(새 에이전트)이 컨텍스트 없이 이어서 작업할 수 있도록 이 절 하나로 자기완결적으로 순서를 파악할 수 있게 쓴다.**
>
> **전제:** §6B.2(스폰 트리거·위치·`destroyWithScene`·색 배정/해제), §6B.3(`TutorialGatherZone`), §6B.4(이탈 정책), §6B.5(Steam Invite UI, 룸코드 없음), §6B.6(코드 이전 매핑표), §4.2(Steam 룸코드 폐지 근거), §5(Title UI 버튼 정리)는 **이미 확정·문서화 완료** — 구현 중 헷갈리면 이 절들을 먼저 본다.

**P1 — 캐릭터 스폰 골격 (신규 로직, §6B.2)** — **코드 완료 + ParrelSync 2인(Host+Client) 검증 통과 (2026-08-17)**

- [x] `TutorialNetworkManager` 신설 — `Assets/Scripts/Network/TutorialNetworkManager.cs`. **Tutorial 씬 배치 완료(사용자, §15)** — `Tutorial.unity`에 `NetworkObject`+`TutorialNetworkManager` GameObject 존재, `coordinatorPrefab` 연결 확인됨
- [x] `NetworkManager.OnClientConnectedCallback` 구독 → 색 배정(`GetNextFreeColor()`, `LobbyNetworkManager.ColorOrder` 재사용 — 아직 이전 아님) → `Instantiate` + `SpawnWithOwnership(clientId, destroyWithScene:true)` at `PlayerSpawnManager.GetFixedSpawnPos(colorType)`
- [x] Host 자신도 `OnNetworkSpawn`에서 동일 경로로 스폰(`AssignColorAndSpawn(LocalClientId)`)
- [x] `OnClientDisconnectCallback` 구독 → 색 즉시 해제(`PlayerSpawnCoordinator.RemoveColorEntry`)
- [x] (부수 작업) `PlayerSpawnCoordinator`에 `AddColorEntry`/`RemoveColorEntry` 신설 — 기존 `PrepareColors`(배치 1회용)와 별개로 접속자 1명씩 증분 갱신. `PlayerSpawnManager`에 `PlayerPrefab` 읽기 전용 프로퍼티 추가(같은 프리팹 재사용)
- [x] **ParrelSync 1인(Host) 검증 통과 (2026-08-17):** Title → Host 생성 → `Tutorial` 진입 즉시 캐릭터 스폰 확인
- [x] **ParrelSync 2인(Host+Client) 검증 통과 (2026-08-17):** 아래 "P1/P2 2인 검증 통과" 참고 — 버그 2건 발견·수정 후 통과

**P2 — 이탈 정책 이식 (§6A.2, §6B.4)** — **코드 완료 + ParrelSync 2인 검증 통과 (2026-08-17)**

- [x] Client 이탈 → 캐릭터(슬롯) 제거만, **방 유지** (`TutorialNetworkManager.OnClientLeft`) — **ParrelSync 2인 검증 통과 (2026-08-17)**, 아래 버그 수정 내역 참고
- [x] Host 이탈 → `TitleReturnFlow.Request`(방 전체 종료) — 구 `OnClientDisconnectedSelf`/`NotifyHostQuit` 패턴 이전(`OnClientDisconnectedSelf`/`NotifyHostQuitClientRpc`)
- [x] Tutorial 상시 HUD "나가기" 버튼 → 위와 동일 종료 경로 호출 — `TutorialNetworkManager.OnClickLeaveRoom()` (Reason=`LobbyQuit`). **`DisconnectManager.OnClickLeaveRoom()`과 동일 시그니처로 맞춤 — ESC 메뉴 Quit 버튼 Inspector 연결을 이 메서드로 재배선 완료(사용자, §15)**
- [x] **ParrelSync 1인 검증 통과 (2026-08-17):** Host가 ESC 메뉴 나가기 클릭 → 방 종료(타이틀 복귀) 확인

#### P1/P2 2인 검증 통과 (2026-08-17) — 룸코드 표시 + 버그 2건 수정

**블로커 해소:** 2인 검증 전, Client를 초대할 방법 자체가 없었다 — `Tutorial`에 Host 자신의 룸코드를 보여줄 UI가 없어서(§6B.5 "Tutorial 상시 HUD"가 P7 체크리스트로 아직 미착수 상태) Client 쪽 입력창에 넣어줄 값이 없었다. **§6B.5/P7 전체를 앞당기지 않고, 룸코드는 어차피 Steam 정식 배포에서 폐지될 예정(§4.2)이라 최소 구현만 추가:** `Assets/Scripts/UI/TutorialRoomCodeDisplay.cs` 신설 — `NetworkManagerSetup.RoomCode`(Host가 `StartHost()` 시점에 이미 로컬로 들고 있는 값, NV 신설 없음)를 그대로 텍스트로 표시. Client는 `RoomCode`가 항상 빈 문자열이라 자동으로 숨겨짐. 개발 전용 표시라 `LanDiscovery.FormatDisplayCode`의 마스킹(`12**56`)은 쓰지 않고 6자리 원본 그대로 노출 — Client가 그대로 옮겨 적어야 하므로. 이걸로 P3 "룸코드" 항목은 완료 처리(아래 P3 참고).

**버그 1 — Client 카메라 안 잡힘:**
- **증상:** ParrelSync 2인에서 Host는 카메라가 정상 바인드되는데 Client는 자기 캐릭터에 카메라가 안 붙음. 콘솔 대조 결과 Host에는 `[NetworkPlayerSetup] 카메라 바인드 완료 (OnPlayersReady)` 로그가 있고 Client에는 이 로그 자체가 없음.
- **원인:** `PlayerSpawnCoordinator.NotifyPlayersReady()`는 `ClientRpc`로 구현되어 **호출 시점에 접속 중인 Client에게만** 전달된다. `TutorialNetworkManager.AssignColorAndSpawn`은 **Host 자신이 스폰되는 시점**(아직 아무 Client도 접속 전)에 `if (!PlayerSpawnCoordinator.IsReady) NotifyPlayersReady();`를 호출해버려, 그 뒤에 순차 합류하는 Client는 이 ClientRpc를 영원히 못 받는다 — `M.Stage1`(전원 접속 후 배치 스폰+1회 발행)과 달리 Tutorial은 "한 명씩 순차 합류" 구조라 이 가정이 깨진다.
- **수정:** `PlayerSpawnCoordinator`에 `CatchUpReadyFor(clientId)` + `CatchUpReadyClientRpc`(대상 지정 `ClientRpcParams`) 신설 — 이미 `IsReady`가 true로 확정된 뒤 합류하는 Client 1명에게만 개별 재전송. `TutorialNetworkManager.OnClientJoined`에서 `AssignColorAndSpawn(clientId)` 직후 호출. 기존 §11.3 늦은 구독 패턴(`if (PlayerSpawnCoordinator.IsReady) BindCameraOnPlayersReady();`)은 그대로 재사용 — 신호가 개별로 도착하기만 하면 나머지는 이미 있던 로직이 처리한다.

**버그 2 — Client가 나가면 방 전체 종료:**
- **증상:** §6B.4는 게이트 통과 전 Client 이탈 = 슬롯만 제거·방 유지라고 확정했는데, 실제로는 Client가 나가는 순간 Host를 포함해 전원이 타이틀로 튕김.
- **원인:** 코드 버그가 아니라 **씬 정리 누락** — `Tutorial.unity`에 `DisconnectManager`라는 이름의 GameObject가 `DisconnectManager` 컴포넌트(인게임 §12 "누구든 나가면 방 종료" 정책 전용, 원래 `M.Stage1`/`T.Stage1` 전용)를 붙인 채 활성 상태로 남아있었다. 이게 `TutorialNetworkManager.OnClientLeft`와 **같은** `NetworkManager.OnClientDisconnectCallback`에 동시 구독돼 있어서, Client 이탈 시 "슬롯만 제거"(정상)와 "전원 타이틀 복귀"(§12 오적용)가 동시에 발동했다.
- **수정:** 사용자가 `Tutorial.unity`에서 `DisconnectManager` GameObject를 삭제(에디터 작업, 코드 변경 없음). §6A.2/§6B.4가 원래 요구하던 "Tutorial 사전 구간 이탈≠인게임 이탈" 구분이 이제 실제로도 지켜짐.

**검증 결과 (2026-08-17, ParrelSync 2인):** 위 2건 수정 후 Client 카메라 정상 바인드 확인, Client 이탈 시 슬롯만 제거되고 방 유지·Host 계속 진행 확인.

**버그 3 — Tutorial TeamStatus 명단/표시 이름/CheerName (2026-09-01):**
- **증상:** Host TeamStatus가 비고, Client 슬롯은 있어도 Steam 이름이 `"Player"`. CheerName을 바꿔도 머리 위/`YOU ·`가 berry/guma/dan 고정. 게이트 통과 후 `M.Stage1`에 가서야 DisplayName이 보임.
- **원인:** `OnPlayersReady`는 Host 1인 스폰 때 1회뿐이고 `CatchUpReadyFor`는 신규 Client만 대상. TeamStatus는 `GameSession` 게이트 스냅샷만 읽고, CheerName UI는 NV 변경을 구독하지 않음.
- **수정:** `PlayerSpawnCoordinator.OnRosterChanged` — 각 머신 로컬 `NetworkPlayerSetup` 스폰/Despawn에서 발행 (`OnPlayersReady` 재발행 없음, §11.4). `TeamStatusUI`/`DeathOverlayUI`는 Ready+Roster를 **다음 프레임 1회 디바운스**로 재구성 — NGO는 `OnNetworkDespawn`을 `IsSpawned=false`·`Destroy`보다 먼저 호출하므로 즉시 `FindObjectsByType`하면 떠난 슬롯이 남고, M/T 배치 스폰은 N명 Roster + Ready로 N+1회 리빌드된다. DisplayName 우선순위는 `CheerService.GetCheerName`과 동일: **세션 확정값(`HasSessionDisplayNames`) → 없으면 `PlayerDisplayNameSync` 실시간 NV**. CheerName 즉시 반영은 `PlayerCheerNameSync.OnAnyCheerNameChanged`. **shared** (`UI.prefab`, Tutorial + 전 M/T).
- **스모크:** Tutorial ParrelSync 2인 Host/Client 슬롯·Steam(또는 로컬 OS) 이름·CheerName 즉시 반영. Client 이탈 시 슬롯 제거. 반대 라운드 `M.Stage1` TeamStatus (§9B.4).

**P3 — 세션 메타데이터**

- [x] 룸코드 표시 — **로컬(①②) 경로 전용, 최소 구현으로 완료** (위 "P1/P2 2인 검증 통과" 참고). 구 `_sharedRoomCode` NetworkVariable 패턴은 채택하지 않음 — `NetworkManagerSetup.RoomCode`(Host 로컬 프로퍼티)를 그대로 표시하는 것으로 충분(Client는 표시 대상 아님, §6B.5). 어차피 Steam 정식 배포에서 룸코드 자체가 폐지되므로(§4.2) 이 이상 정교화하지 않는다.
- [x] DisplayName 보고 (2026-08-22) — 구 `SubmitDisplayNameServerRpc`(슬롯 귀속)를 `PlayerDisplayNameSync`(Player 인스턴스 귀속, `PlayerCheerNameSync`와 동일 패턴)로 재구현. `OnNetworkSpawn`에서 Owner가 자기 표시 이름(Steam 경로: `SteamClient.Name`, 로컬 경로: OS 계정 이름)을 1회 자동 보고. HUD 읽기 우선순위는 CheerName과 동일: 게이트 후 `GameSession` 세션 스냅샷, 게이트 전(세션 미확정)에만 이 NV 실시간 표시 (2026-09-01, 위 버그 3). 게이트 통과 시 `GameSession.SetSessionDisplayNames()` + `BroadcastSessionDisplayNamesClientRpc`로 스테이지용 스냅샷만 복사.
- [x] VoiceId 보고 (2026-08-26) — 구 `SubmitVoiceIdServerRpc`(슬롯 귀속)를 별도 클래스 신설 없이 `PlayerDisplayNameSync`에 필드 추가로 재구현(DisplayName과 동일 "검증 없는 1회 자동 self-report" 뼈대라 한 컴포넌트로 합침 — CheerName은 입력·검증·재제출이 있는 별도 도메인이라 `PlayerCheerNameSync`에 그대로 분리 유지). Dissonance 초기화 지연 대비 `ReportVoiceIdRoutine` 코루틴이 `LocalPlayerName` 확정까지 최대 5회(1초 간격) 재시도 후 1회 보고 → `TutorialNetworkManager.CompleteGate()`에서 `GameSession.SetSessionVoiceIds()` + `BroadcastSessionVoiceIdsClientRpc`로 전원 배포(DisplayName과 동일 시점). **원인 회귀:** 2026-08-20 구 로비 삭제 때 `SubmitVoiceIdServerRpc`가 함께 삭제된 뒤 새 Tutorial 게이트 구조로 이식되지 않아 `OptionsTeamVoicePanel`의 팀 보이스 볼륨 슬라이더가 항상 비활성(100% 고정)이었음 — 이번 수정으로 해소. **ParrelSync/실제 멀티 검증 대기.**
- **공유 포스트모템 (2026-09-01, 마이크/팀 보이스 슬라이더):** `Row_MicVolume`은 placeholder라 `GameSettingsManager`/`OptionsMenuController`에 볼륨 필드가 없었고, 생성 기본값 70% + `interactable=false`로 고착. 팀 보이스는 `BindVolumeSlider`가 `SetValueWithoutNotify`만 써서 `%` 라벨이 70%에 남고, `FindPlayer` 실패 시 재시도가 없었다. 수정: `MicVolume` PlayerPrefs + `VoiceBroadcastTrigger.ActivationFader`(Cheer/Vosk 미변경), 슬라이더 런타임 탐색, `SliderValuePercentLabel.RefreshNow`, `OnPlayerJoinedSession` 재바인딩. 반대 라운드 스모크: `T.Stage1` Host+Client ESC 설정 (§9B.4).

**P4 — `TutorialGatherZone` (§6B.3, 신규)** — **코드 완료 + 씬 배치·검증 통과 (2026-08-18, 솔로+ParrelSync 2인)**

- [x] 색 무관 단일 트리거 존 — 헤드카운트 비교(존 안 인원 == 접속 인원), 카운트다운, 이탈/합류 시 리셋. `TutorialGatherZone.cs`(순수 로컬 트리거 감지, clientId 기준 점유 목록) + `TutorialNetworkManager.Update()`(Host 전용, 헤드카운트 비교·카운트다운·리셋). `ColoredStartZone`/`StageStartGate`와 동일 "트리거 감지 vs 판정" 역할 분리 원칙 재사용.

**P5 — 게이트 완료 처리 (구 `StartGameServerRpc` 책임 이전, §6B.3)** — **코드 완료 + 검증 통과 (2026-08-18, 솔로+ParrelSync 2인)**

- [x] `clientColorDict` 확정(그때까지 배정된 색 그대로) — `PlayerSpawnCoordinator.GetAllEntries()`로 읽음(Tutorial 접속마다 이미 누적돼 있던 값, 별도 확정 로직 불필요)
- [x] `PlayerSpawnCoordinator` 스폰(DDOL, 기존 그대로) — Tutorial 접속 시점(`EnsureCoordinatorSpawned`)에 이미 스폰돼 있어 재스폰 불필요(§6B.2와 동일 인스턴스)
- [x] `PlayerSpawnManager.InitializeOnline(clientColorDict)` 호출
- [x] 세션 시드 생성+배포(`BroadcastSeedClientRpc`류), 세션 시작 서버시각 배포
- [x] `GameSession.SetActiveColors` 확정+배포
- [x] `SetSessionCheerNames`/`SetSessionDisplayNames`/`SetSessionVoiceIds` 확정+배포 완료 (2026-08-22 DisplayName, 2026-08-26 VoiceId — 위 P3/P8 항목 참고)
- [x] `SceneFlowManager.LoadNextScene()` 호출 → `M.Stage1`

**P6 — CheerName Tutorial 통합** — **네트워크 동기화 코어 코드 완료 (2026-08-18), 자기응원 피드백 UI 코드 완료 (2026-08-19), 테스트는 이번 라운드 전체 보류 (사용자 지시 2026-08-19) → 아래 남은 작업 다 끝내고 한 번에 검증**

> **별도 SSOT** — `CheerAndTutorialDesign.md` §7 Phase 7 / §9 체크리스트를 따라 진행(응원 버프 규칙 자체는 `CheerSystemDesign.md`). 여기서 중복 서술하지 않음. 아래는 이번 라운드에서 실제로 반영한 범위만 기록.

- [x] `PlayerCheerNameSync`(`Assets/Scripts/Cheer/PlayerCheerNameSync.cs`) 신설 — Player 프리팹 부착 대상. `NetworkVariable<FixedString32Bytes>`(Server write) + `SubmitCheerNameServerRpc`(본인 소유 NetworkObject만 제출 가능 가드) + 형식·예약어·세션 내 중복 검증(§3.5) + 결과 통보 이벤트(`OnSubmitResult`)
- [x] `CheerNameValidator`(`Assets/Scripts/Cheer/CheerNameValidator.cs`) 신설 — 구 `LobbyNetworkManager.IsValidCheerNameFormat`/`ReservedNames`를 추출한 공용 검증 유틸. `LobbyNetworkManager`도 이제 이걸 재사용 — P8에서 구 로비 코드를 지워도 검증 규칙은 남는다
- [x] CheerName 변경 시 로컬 `CheerKeywordEngine.ApplySessionGrammar()` 재빌드 훅 연결 — 어느 Player의 이름이 바뀌었든 "내 로컬 인식기(Owner 인스턴스)" 하나만 매번 전체 이름 목록으로 재적용(§5.3) — **[2026-09-01 갱신] Phase B에서 `ApplySessionGrammar`(전원 이름) → `ApplyOwnerLocalGrammar`(내 이름 + TeamCheerWord만)로 교체됨. 현재 동작 SSOT는 `CheerSystemDesign.md` §10.2. 이 줄은 2026-08-18 시점 기록으로 보존.**
- [x] `TutorialNetworkManager.CompleteGate()`에 세션 CheerName 확정 추가 — 게이트 통과 시점 각자 최신값을 colorIndex 배열로 확정해 `GameSession.SetSessionCheerNames()` + `BroadcastSessionCheerNamesClientRpc`로 전원 배포(§6B.7 P3 두 번째 항목 중 CheerName 부분은 이걸로 완료 처리 — DisplayName은 2026-08-22, VoiceId는 2026-08-26에 각각 후속 완료, 위 P3/P8 항목 참고)
- [x] **입력 UI**(TMP_InputField 연결, 확정 버튼) — **코드+씬 배치 완료 (2026-08-19), ParrelSync 검증 대기.** 아래 "입력 UI 반영 내용" 참고
- [x] **"내가 지금 응원 중" 자기 확인 UI** (신규, 2026-08-19) — `PlayerNameTagUI`의 로컬 오너 전용 슬롯에 타겟 CheerName 표시. `OnVoteReset`/타겟 변경 시 자동 숨김. **ParrelSync 검증 대기** (아래 체크리스트 참고) — **[2026-09-01 갱신] Phase C에서 cross-targeting 삭제로 이 슬롯 자체 제거. 로컬 오너는 다시 이름표 숨김만. 현재 SSOT는 `CheerSystemDesign.md` §10.3. 이 줄은 2026-08-19 시점 기록으로 보존.**
- [x] `CheerKeywordEngine` Tutorial 연동 + 게이트 전 커스텀 이름 인식 갭 수정 — **구현 완료 (2026-08-19)**
  - `CheerKeywordEngine`의 `_lobbyTestMode`→`_sayTestMode` 리네임, `GetLobbyColorIndex`/`BuildLobbyGrammarJson`(둘 다 `LobbyNetworkManager.Instance` 슬롯 순회)을 `GetTutorialColorIndex`/`BuildTutorialTestGrammarJson`(`PlayerCheerNameSync.GetAllEffectiveNames()` + `PlayerSpawnCoordinator.TryGetColor` 기반)으로 교체 — 로비 의존 제거. `_sayTestMode=true`일 때는 여전히 `SubmitCheerServerRpc` 호출 안 함(로컬 인식 확인만). 영향 확인: `_lobbyTestMode=true`였던 곳은 삭제 대상 `1.Lobby.unity` 1곳뿐, 실사용 `Player1.prefab`은 `false`라 리네임으로 인한 동작 영향 없음
  - **Tutorial 게이트 통과 전 커스텀 이름 인식 갭 수정** — 원인은 `CheerService.GetColorIndex`/`GetCheerName`이 `GameSession._sessionCheerNames`(게이트 통과 시에만 설정)만 보고, 미설정 시 고정 4종 기본값으로만 폴백했던 것. `GameSession`에 `HasSessionCheerNames` 프로퍼티 신설(`GetSessionCheerName` 자체는 미확정 시에도 기본값으로 폴백해버려 "비어있으면 다음 우선순위" 방식으로는 구분 불가) + `CheerService.GetColorIndex`/`GetCheerName`에 우선순위 폴백 추가: ①`GameSession` 확정 세션값(게이트 후) → ②`PlayerCheerNameSync.GetAllEffectiveNames()` 실시간값(게이트 전) → ③정적 기본값. Vosk grammar 자체는 이미 `PlayerCheerNameSync.RebuildOwnerLocalGrammar()`가 실시간 반영해서 문제없었음 — 갭은 인식된 단어를 colorIndex로 바꾸는 이 매핑 단계였음
- [x] "말해보기" UX 설계 변경 (2026-08-19, 사용자 결정) — **상시 노출 → Tutorial 구역 2 상호작용 표지판으로 개폐.** 상시 패널은 화면을 계속 가리고, DialogueUI식 1회성 노출은 나중에 이름을 바꾸려 해도 타이밍을 놓칠 수 있다는 문제로, `TutorialCheerNameSignboard`(신규, `Assets/Scripts/Stage/`) 상호작용 표지판이 `TutorialCheerNameUI.Open()/Close()/Toggle()`을 호출해 게이트 통과 전까지 언제든 여닫는 방식으로 확정. 상세는 §9(체크리스트) 및 `CheerAndTutorialDesign.md` §6/§2(구역 2) 참고
  - `TutorialCheerNameUI`: 패널 GameObject 자체를 활성/비활성으로 토글(`Open`/`Close`/`Toggle`), `IsOpen` 정적 플래그 신설(`InGameChatUI.IsChatOpen`과 동일 패턴), 닫기 버튼 추가
  - `Player.cs`의 `OnMove`/`GetInput`에 `TutorialCheerNameUI.IsOpen` 이동 잠금 가드 추가(`InGameChatUI.IsChatOpen`과 나란히) — 타이핑 중 WASD가 이동으로 새는 문제 방지, 채팅 입력과 동일 해법 재사용
  - **1개(재방문 가능)로 배치, 표지판 3D 비주얼은 사용자가 나중에 교체 — 지금은 플레이스홀더로 진행. 상호작용 키는 프로젝트 기존 관례(`Keyboard.current` 직접 폴링)대로 E키, `InputSystem_Actions`의 미사용 `Interact` 액션(Hold 인터랙션 붙어있어 그대로 쓰기 부적합)은 손대지 않음**
  - [x] 사용자 에디터 작업 — **완료 (2026-08-19, MCP).** `CheerNamePanel` 기본 비활성 + `CloseButton`/`ExamplesText` 추가·연결, `CheerNameSignboard`(트리거+플레이스홀더 Visual+`[E] 이름 설정` 프롬프트) 구역 2 근처 `(10, 0, 8)`에 배치. `Tutorial.unity` 저장됨
- [x] "말해보기" 브로드캐스트 미결정 항목 — **모두 필요 없음으로 결론 (2026-08-19, 사용자 판단).** 두 차례 물었던 "팀원 화면에도 인식 결과를 보여줄지" 자체가 잘못된 질문이었음 — 실제 응원 제출 경로(`_sayTestMode=false`, 기본값)로 이름을 외치면 이미 **전원에게** 실시간으로 보인다:
  - `PlayerCheerHeartsUI` — 응원받는 사람 머리 위에 응원 중인 팀원 색 하트(네트워크 전체 브로드캐스트, 기존 구현) — **[2026-09-01] Phase C: 이번 팀워드 라운드에 그 플레이어가 이미 외쳤는지 하트 1개 온/오프. SSOT `CheerSystemDesign.md` §10.3**
  - `PlayerNameTagUI` — 응원하는 사람 본인 화면에 "지금 응원 중인 대상의 CheerName" 표시 → 내 발화가 인식돼 올바른 대상에 매칭됐는지 즉시 확인 가능 — **[2026-09-01] Phase C에서 삭제. 타겟 개념 없음**
  - `TeamStatusUI` — 나를 응원 중인 팀원에 "Cheering" 라벨 — **[2026-09-01] Phase C: 숫자키 아이콘 → 팀워드 진행도 체크**
  
  즉 "말해보기"는 **별도 모드가 아니라 진짜 응원 제출 그 자체**로 이미 충족된다. 별도 로컬 전용 인식 확인 UI(`TutorialCheerSayTestUI`)는 만들지 않기로 확정
- [x] ~~"말해보기" 테스트 UI 자체(`TutorialCheerSayTestUI`)~~ — **폐기 (2026-08-19).** 위 근거로 신규 컴포넌트 불필요. 대신 `CheerNamePanel`의 `ExamplesText`에 "확정 후에는 팀원에게 이 이름을 외쳐달라 해서 실제로 인식되는지 확인해보세요!" 안내 문구 추가(2026-08-19, MCP 반영·씬 저장 완료)로 대체. `CheerKeywordEngine`의 `_sayTestMode`/`GetTutorialColorIndex`/`BuildTutorialTestGrammarJson`/`OnKeywordDetected`는 로비 의존 제거 과정에서 이미 만들어둔 코드라 남겨두되(부작용 없음, 어떤 컴포넌트도 `_sayTestMode=true`로 설정 안 함), 이걸 소비하는 UI는 더 만들지 않음
  - **후속(지금 착수 안 함):** 구역 3(응원 1회 체험) 자체가 아직 미구현(§9.2, 체크리스트 미착수)이라 그 Dialogue에 같은 안내를 넣을 자리가 아직 씬에 없음. 구역 3 구현 시 Dialogue 문구에 "실제로 외쳐서 확인" 안내를 같이 넣을 것 — 그때 처리
- [x] 금칙어 blocklist(`CheerNameValidator.cs`, §3.5 #9~12) — **구현 완료 (2026-08-19).** `CheerNameValidator.Blocklist`(부분 문자열 매칭) + `ContainsBlockedWord()` 추가, `PlayerCheerNameSync.SubmitCheerNameServerRpc`에서 형식·예약어 통과 후 호출 → 거절 시 `"blocked"` 사유 반환, `TutorialCheerNameUI.ResolveErrorMessage`에 메시지 매핑 추가. 스코프는 아래 표 그대로("완벽 필터 아님, 대놓고 심한 단어만"):

  | # | 카테고리 | 포함 | 제외 |
  |---|---|---|---|
  | 9 | 욕설 | 흔한 영문 비속어 (`fuck`/`shit`/`bitch`/`ass`/`damn`/`bastard`/`whore`/`slut` 등) | 지역/은어 방언 전부 |
  | 10 | 성/신체 | 성기·성행위 직접 지칭 소수 | 의학 용어·은유 표현 |
  | 11 | 혐오·차별 | 대표적 슬러 몇 개만 | 전 세계 모든 혐오 표현 |
  | 12 | 숫자 치환 우회 | `f4ck`/`fuk`/`sh1t`/`a55`/`b1tch` 등 가장 뻔한 패턴만 | 정교한 leetspeak 전체, `$` 등 기호(형식검증이 이미 막음) |

  코드 테이블 방식이라 플레이테스트 후 걸리는 단어는 `Blocklist` 배열에 추가하면 됨(AI 필터 없음, §3.5 기존 방침 그대로 유지). **ParrelSync 검증 대기** (아래 통합 검증 체크리스트 그룹 A).
- [ ] ParrelSync 2인 검증 — **이번 라운드 전체 보류 (2026-08-19, 사용자 지시).** 위 미착수 항목들 다 끝낸 뒤 아래 "통합 검증 체크리스트"로 한 번에 진행

> **⚠️ 크리티컬 블로커 — 정정 (2026-08-19 재조사) — 실제로는 이미 해결된 상태로 보임.**
> 이전 기록은 `Player.Network.prefab`을 기준으로 "미부착"이라 판단했으나, 이는 잘못된 프리팹을 지목한 것이었다. `Tutorial`이 실제로 스폰에 쓰는 프리팹은 `PlayerSpawnManager.playerPrefab` 필드(`Title.unity`에서 직렬화됨)가 가리키는 **`Player1.prefab`**이고(`TutorialNetworkManager.AssignColorAndSpawn` → `PlayerSpawnManager.Instance.PlayerPrefab` 경로로 확인), `Player.Network.prefab`은 `NetworkManager.NetworkConfig.PlayerPrefab`에만 연결돼 있을 뿐 `ApproveConnection`의 `CreatePlayerObject=false`로 인해 실제로는 쓰이지 않는다(§6B.2 구현 참고 각주와 동일 사실). 확인해보니 `Player1.prefab`에는 이미 `PlayerCheerNameSync` 컴포넌트가 부착돼 있다(현재 미커밋 변경사항).
> **남은 것: 실기 확인뿐.** ParrelSync로 Tutorial에 들어가 CheerNamePanel이 정상 동작하는지(대상을 찾는지) 1회 확인 필요 — 통합 검증 체크리스트 그룹 A에서 같이 확인하면 됨. 별도 에디터 작업은 필요 없어 보임.

#### 다음 에이전트 시작점 (2026-08-19 갱신 — 블록리스트 + CheerKeywordEngine Tutorial 연동 + 말해보기 UX 설계 완료 후)

1. ~~금칙어 blocklist 구현~~ — **완료**
2. ~~`CheerKeywordEngine` Tutorial 연동 + 게이트 전 커스텀 이름 인식 갭 수정~~ — **완료 (2026-08-19)**
3. **크리티컬 블로커 실기 확인** — 위 정정 내용대로 `Player1.prefab`의 `PlayerCheerNameSync`가 ParrelSync에서 실제로 동작하는지 1회 확인(아래 통합 검증 체크리스트 그룹 A와 겸해서 진행 가능)
4. ~~에디터 작업~~ — **완료 (2026-08-19, MCP).** `CheerNamePanel` 기본 비활성 + Close/Examples(+실제 테스트 안내 문구) + `CheerNameSignboard` 배치·연결, `Tutorial.unity` 저장
5. ~~"말해보기" 테스트 UI~~ — **폐기 (2026-08-19).** 실제 응원 제출 자체가 곧 테스트(위 §6B.7 P6 참고), 신규 컴포넌트 불필요
6. 남은 건 위 3번(크리티컬 블로커 실기 확인)뿐 — 이제 아래 "통합 검증 체크리스트"(입력 UI + 자기응원 UI + 블록리스트)를 ParrelSync 2인으로 한 번에 실행 가능

#### 입력 UI 반영 내용 (2026-08-19, 상시 표시 → 상호작용 표지판 개폐로 재변경)

**코드:** `Assets/Scripts/UI/TutorialCheerNameUI.cs` + 신규 `Assets/Scripts/Stage/TutorialCheerNameSignboard.cs`. `PlayerCheerNameSync`(§6B.2 이미 완료)를 그대로 사용 — 새 네트워크 로직 없음, 로컬 입력 UI + 로컬 트리거만.

- 씬의 모든 `PlayerCheerNameSync`를 훑어 `NetworkObject.IsOwner`로 내 캐릭터만 탐색(`RebuildOwnerLocalGrammar()`와 동일 패턴) — 찾을 때까지만 `Update()`에서 재시도, 찾으면 폴링 중단
- Enter(`TMP_InputField.onSubmit`) 또는 확정 버튼 → `SubmitCheerNameServerRpc` 호출 → 응답 오기 전까지 입력창/버튼 비활성화 → `OnSubmitResult`로 성공/실패(`format`/`reserved`/`taken`/`blocked`) 메시지 표시
- `PlayerCheerNameSync.GetAllEffectiveNames()`(기존 public static)로 내 clientId의 "실제 적용중인 이름"(커스텀 없으면 색 기본값)을 그대로 읽어 표시 — 별도 기본값 계산 로직 없음
- 타이핑 중엔 로컬만(§3.4) — 확정 전까지 ServerRpc 호출 자체가 없어 이 구조로 자동 충족
- **개폐 방식 (2026-08-19 변경):** `CheerNamePanel`(패널 루트 GameObject) 자체를 `Open()`/`Close()`/`Toggle()`로 활성/비활성 토글. `IsOpen` 정적 플래그(`InGameChatUI.IsChatOpen`과 동일 패턴)로 `Player.cs`가 이동 잠금. 게이트 통과 전까지 몇 번이든 다시 열어 재확정 가능(§3.4)
- `TutorialCheerNameSignboard`: Tutorial 구역 2의 상호작용 표지판 — 로컬 플레이어가 트리거 범위에 들어오면 프롬프트 표시, E키로 `cheerNameUI.Toggle()` 호출. 순수 로컬(네트워크 판정 없음, 각자 자기 화면 UI만 여닫으므로 충돌 없음)

**씬 배치 (2026-08-19 MCP 반영 완료):** `Assets/Scenes/Tutorial.unity`

```
UI (Canvas)
└─ CheerNamePanel          ← TutorialCheerNameUI 부착, 기본 비활성(SetActive false)으로 변경
   ├─ TitleText             "응원 이름을 정해보세요"
   ├─ NameInputField        TMP_InputField, Single Line, characterLimit=12, placeholder="예: dan"
   ├─ ConfirmButton          "확정"
   ├─ CloseButton            "닫기" (신규, closeButton 필드 연결)
   ├─ CurrentNameText        "현재 이름: ..."
   ├─ FeedbackText           성공/실패 메시지 (초기 비활성)
   └─ ExamplesText           (신규, 순수 텍스트) "이렇게는 안 돼요: fuck, admin, ab, 한글이름 🎉" 등 예시 — 코드 없이 고정 텍스트로 작성

(Tutorial 구역 2, 월드) SignboardObject   ← TutorialCheerNameSignboard 부착 + Collider(Is Trigger)
└─ PromptRoot                              "[E] 이름 설정" 안내, 기본 비활성
```

Inspector 필드 연결: `TutorialCheerNameUI`의 `closeButton` 신규 연결 필요(나머지는 완료), `TutorialCheerNameSignboard`의 `cheerNameUI`(CheerNamePanel 참조) + `promptRoot` 연결 필요. `CheerNamePanel` 자체는 씬에서 기본 비활성으로 바꿔야 함(현재 활성 상태로 남아있으면 상시 노출되던 예전 동작 그대로 유지됨).

#### 통합 검증 체크리스트 (ParrelSync 2인, 전체 미실시 — 2026-08-19 사용자 지시로 한 번에 모아서 진행)

> 아래 두 그룹 전부 **위 "다음 에이전트 시작점" 1~4번 작업이 끝난 뒤** 한 번에 실행할 것. 순서상 이 파일에서 관련 항목이 전부 `[x]`가 된 다음이 적절함. (구 그룹 C "말해보기" 테스트 UI는 폐기 — 위 §6B.7 P6 참고, 실제 응원 제출 자체로 검증됨. 그 시나리오는 아래 그룹 A 마지막 항목에 통합)

**A. CheerName 입력 UI**

- [ ] Host + Client 접속 → `Tutorial` 진입 → `CheerNamePanel`이 기본적으로 안 보이는지(구 상시 표시 아님)
- [ ] 표지판(구역 2) 근처에 가면 "[E] 이름 설정" 프롬프트가 뜨고, E키로 `CheerNamePanel`이 열리는지
- [ ] 패널이 열려있는 동안 WASD를 눌러도 캐릭터가 이동하지 않는지(`TutorialCheerNameUI.IsOpen` 이동 잠금)
- [ ] 닫기 버튼 또는 표지판 재상호작용(E) → 패널이 닫히고 다시 이동 가능해지는지
- [ ] 게이트 통과 전이면 몇 번이든 다시 열어 이름을 재확정할 수 있는지(§3.4)
- [ ] 이름 입력 후 확정 → "이름이 확정되었습니다" + `현재 이름:` 갱신 확인
- [ ] 팀원 화면에도 그 이름이 반영되는지(같은 Player의 `PlayerCheerNameSync.CustomCheerName`을 다른 클라이언트가 읽을 때)
- [ ] 같은 이름으로 둘 다 확정 시도 → 나중 시도한 쪽이 "이미 다른 팀원이 사용 중" 거절되는지
- [ ] 한글/공백/이모지 입력 시도 → 타이핑 자체가 막히는지(`onValidateInput` 필터)
- [ ] 13자 이상 입력 시도 → `characterLimit=12`로 막히는지
- [ ] 예약어(`cheer`, `admin`, `host`, `server`, `system`, `bot`, `null`) 입력 → "시스템 예약어" 거절 확인
- [ ] 빈칸으로 확정 → 색 기본값으로 돌아가는지(`현재 이름:`에 기본 이름 표시)
- [ ] 같은 사람이 여러 번 재확정(잠금 없음, §3.4) → 매번 반영되는지
- [ ] 색 변경 후에도 커스텀 이름이 유지되는지(§3.3, CheerName은 색이 아니라 플레이어에 귀속)
- [ ] 금칙어(`fuck`/`fuk`/`sh1t` 등 `CheerNameValidator.Blocklist`) 입력 → "사용할 수 없는 단어가 포함되어 있어요" 거절 확인
- [ ] 금칙어를 포함한 긴 단어(예: `fuckboy`) → 부분 문자열 매칭으로도 거절되는지
- [ ] **게이트 통과 전** 커스텀 이름 확정 후 zone 3(응원 1회 체험)에서 그 이름을 외쳐도 정상 인식되는지(`CheerService.GetColorIndex` 게이트 전 폴백 검증)
- [ ] "확정 후 실제로 외쳐서 확인해보세요" 안내 문구(`ExamplesText`)가 잘 보이는지 — 이게 "말해보기"의 실제 구현체(별도 UI 없음, 2026-08-19 결정)이므로 팀원에게 이름을 외쳐달라고 부탁 → 아래 그룹 B로 인식 확인이 이어지는 흐름이 매끄러운지

**B. "내가 지금 응원 중" 자기 확인 UI (`PlayerNameTagUI`, 신규)**

- [ ] A가 B를 응원 시작 → **A 자신의 화면**에서 A 캐릭터 머리 위에 B의 CheerName이 B의 색으로 뜨는지
- [ ] 그 텍스트가 다른 팀원(C) 화면에서도 A 머리 위에 동일하게 보이는지(월드 스페이스, 전원 공통)
- [ ] A가 타겟을 B→C로 바꾸면 즉시 텍스트가 C 이름/색으로 바뀌는지(깜빡임 없이)
- [ ] 버프 발동(표 충족) 또는 타임아웃으로 표 초기화 → A 머리 위 텍스트가 사라지는지
- [ ] 응원을 아예 안 하고 있을 때 A 머리 위엔 아무것도 안 뜨는지(기존처럼 빈 상태)
- [ ] 자기 자신 응원(솔로 1인 규칙, `ActivePlayerCount==1`) 상황에서도 자연스럽게 동작하는지(또는 의도적으로 숨김 처리할지 확인)

**P7 — UI**

- [ ] Tutorial 상시 HUD 컨트롤러(가칭) — Steam: Invite 버튼만 / 로컬: 룸코드 표시만, 공통: 나가기 버튼. 게이트 통과 후 숨김 (§6B.5)
- [ ] Gate 카운트다운 UI(`TimerUI`/`OnCountdownTick` 재사용)
- [ ] Title: Steam 빌드에서 `OnClickJoinGame`/`joinPanel`/`roomCodeInputField`/`ConfirmJoinSteam` 경로 숨김/미사용 처리 (§5). **로컬 경로(`ConfirmJoinLocal`)는 그대로 유지**

**P8 — Kick 제거 + 구 코드 삭제**

- [x] `KickPlayerServerRpc` + Kick UI 완전 삭제 (2026-08-19) — `LobbyNetworkManager.KickPlayerServerRpc`, `LobbySlotUI.OnClickKick()`/`kickButtonRoot`, `LobbyMenuController`의 `canKick` 계산 전부 삭제. 참조가 `LobbySlotUI` 1곳뿐이라 다른 파일 영향 없음
- [x] `LobbyNetworkManager.cs` / `LobbyMenuController.cs` / `LobbyPlayerState.cs` / `LobbySlotUI.cs` / `LobbyCharacterPreview.cs` 삭제 (2026-08-20 완료) — 공용 상수(`ColorOrder`/`DefaultCheerNames`/`ColorTypeToIndex`)를 먼저 `PlayerColorUtil.cs`(기존 색 유틸)로 이전한 뒤, 이 상수를 참조하던 20여개 파일(`GameSession`/`CheerService`/`CheerKeywordEngine`/`PlayerCheerNameSync`/`TutorialNetworkManager`/`PlayerSpawnManager`/`NetworkPlayerSetup`/`TeamStatusUI`/`PlayerHPUI`/`PlayerNameTagUI`/`PlayerCheerHeartsUI`/`DeathOverlayUI`/`CheerProgressUI`/`InGameChatUI`/`ColorTileChallenge`/`OptionsTeamVoicePanel` 등)의 참조를 `PlayerColorUtil.XXX`로 교체 후 삭제. `OptionsTeamVoicePanel`의 죽은 로비 슬롯 폴백 분기(`TryCollectFromLobby`)도 함께 제거 — 이제 `GameSession` 세션 데이터 하나만 본다(단, Tutorial 게이트 통과 전에는 DisplayName/VoiceId 세션 확정이 아직 미구현이라 팀원 목록이 비어 보일 수 있음 — P3 두 번째 항목 참고, 별개 이슈). `GetEffectiveCheerName`은 로비 UI 전용이라 이전 없이 함께 삭제(다른 참조 없음 확인).
- [x] `TitleMenuController`의 Steam 룸코드 입력 경로(`ConfirmJoinSteam`) 삭제 (2026-08-19, §4.2, §5) — `OnClickConfirmJoin()`이 로컬(①②) 경로만 수행, Steam 빌드에서는 경고 로그만 남기고 무시
- [ ] **사용자 에디터 작업 (남음):** `Assets/Scenes/1.Lobby.unity` 삭제 + Build Settings에서 제거 (§15) — 코드 쪽 의존성은 위에서 전부 제거 완료, 씬 파일 자체만 남음

**P9 — 에디터 작업 (사용자 담당, §15)** — **부분 진행 (2026-08-17)**

- [x] `Title`/`Tutorial` 씬 신설 완료(내용 있음, 실제 사용 중). `0.Title.unity`/`2.Tutorial.unity`는 이미 정리됨 — **`Assets/Scenes/1.Lobby.unity`만 남음.** 코드 쪽 대체(P8)는 2026-08-20 완료 — 남은 건 씬 파일 삭제 + Build Settings 제거뿐(사용자 에디터 작업)
- [x] `Tutorial.unity`에 `TutorialNetworkManager` 배치 + `coordinatorPrefab` 연결 완료
- [x] Tutorial ESC 메뉴 Quit 버튼 → `TutorialNetworkManager.OnClickLeaveRoom()` 재배선 완료
- [x] `TitleMenuController.lobbySceneName` → `"Tutorial"`로 갱신 완료 (`Title.unity` 확인됨)
- [x] `Tutorial.unity` 상시 HUD에 `TutorialRoomCodeDisplay` 텍스트 오브젝트 추가 완료 (2026-08-17, 위 "P1/P2 2인 검증 통과" 참고)
- [x] `Tutorial.unity`의 leftover `DisconnectManager` GameObject 삭제 완료 (2026-08-17, 버그 2 수정 — 위 참고)
- [x] `SceneFlowManager.sceneSequence` 갱신 — **확인 완료 (2026-08-18)**. `Title.unity`의 직렬화 값이 `Title, Tutorial, M.Stage1~5, M.Boss, T.Stage1~5, T.Boss` 순으로 이미 정상 배치돼 있음(1.Lobby 없음)
- [x] `Tutorial.unity`에 `TutorialGatherZone` GameObject 신규 배치 완료 — **검증 통과 (2026-08-18)**

**P10 — 검증 (ParrelSync 2인 기준)** — **P1/P2/P4/P5 관련 검증 통과, P6은 별도 SSOT 대기**

- [x] Title → Host 생성 → `Tutorial` 진입 즉시 캐릭터 스폰 확인 (1인)
- [x] Host가 ESC 나가기 → 방 종료(타이틀 복귀) 확인 (1인)
- [x] Client 룸코드 접속 → 색 중복 없이 합류 스폰 확인 — **ParrelSync 2인 통과 (2026-08-17)**
- [x] 접속 → 색별 고정좌표 스폰 → 게이트 통과 → `M.Stage1` 정상 진입(캐릭터 정상 스폰) — **솔로 + ParrelSync 2인 모두 통과 (2026-08-18).** CheerName 확정/재변경/말해보기 부분만 P6(CheerName Tutorial 통합) 미구현이라 아직 검증 범위 밖
- [x] Client 이탈(게이트 전) → 슬롯만 제거, 방 유지 확인 — **ParrelSync 2인 통과 (2026-08-17)**, 버그 2 수정 후
- [x] Host 이탈(게이트 전) → 방 전체 종료 확인 — **1인 기준 통과.** 2인 상태 재확인은 아직 별도 실시 안 함(다음 라운드 권장)
- [x] 솔로(Host 1인) → 게이트 즉시 통과 확인 (§2.2) — **통과 (2026-08-18)**
- [ ] Steam Invite 수락(온기동 `OnSteamInviteAccepted` / 냉기동 `TryAutoJoinFromLaunchArgs`) — 룸코드 UI 없이도 정상 조인되는지 확인 (§4.2)
- [ ] 위 항목 통과 후 §6A.6/§11.6 기존 검증 체크리스트와 통합 실행

---

## 7. 플레이어 · 스폰

### 7.1 Prefab

- 씬에 Player 4개 배치 **제거** (`M.Stage1` 등).
- **NetworkObject Player Prefab 1개** + 스폰 시 `Configure(color, playerId, stats)`.
- **활성 슬롯(선택된 색)만** 스폰.

### 7.2 스폰 위치

- **`ColoredStartZone.spawnPoint`** (존 **위**)에 배치. — **M/T 스테이지 전용.**
- 리스폰 좌표는 `PlayerSpawnManager.fixedSpawnPositions`가 전담 (§11). Zone은 시작 게이트 판정만.
- **`Tutorial`도 동일한 `PlayerSpawnManager.fixedSpawnPositions`(색별 4개 고정 좌표, `GetFixedSpawnPos(colorType)`)를 재사용한다 (2026-08-17 확정).** 별도의 Tutorial 전용 스폰 포인트를 새로 만들지 않음 — 색 배정 즉시 그 색의 고정 좌표에 스폰되므로 4명이 자동으로 흩어져 시작하고, 겹침 방지용 별도 로직이 필요 없다. (Tutorial 씬도 다른 스테이지 씬과 동일하게 원점(0,0,0) 기준으로 그 주변이 이동 가능한 지형인지 배치 확인 필요 — 에디터 작업.)
- 존 트리거 진입 → 점유 → `StageStartGate` 카운트다운 (전원 점유 시 진행). — Tutorial의 게이트는 `TutorialGatherZone`(§6B.3, 색 무관 단일 존)으로 별도 로직.
- **씬 지형(T.Stage) 이동 불필요.** 존·spawnPoint는 씬마다 에디터 배치.

### 7.3 입력 · 카메라 · 이동 (**확정** = Owner + CNT)

> **확정 (2026-07-13):** 플레이어 이동 = **Owner + `ClientNetworkTransform`**. Host 이동화·Client Prediction으로 **바꾸지 않음**. 이 시스템을 계속 유지한다.

| 항목 | 규칙 (**확정**) |
|------|----------------|
| **이동** | **Owner** — `ClientNetworkTransform` (Owner Authority). 로컬 `Rigidbody` 이동 |
| **키 입력** | **Owner** — 로컬 `PlayerInput`만 활성 |
| **카메라** | **Owner** — `LocalPlayerCamera`(DDOL 프리팹) 1대. Owner 스폰 시 생성, target = Owner Transform. 씬 Main Camera 비활성 |
| **애니메이션** | **Owner 연출** — 달리기 등 로컬. **맞음/사망 확정은 Host** (`ClientRpc`) |
| **마우스 시점** | **본인만** |
| **원격 플레이어 표시** | Owner 위치 복제 + 보간 |
| **클라이언트 예측** | 이동은 Owner 로컬 → **별도 예측 불필요** |

**Owner 전용:** 키 입력, 카메라, 애니 연출, 로컬 마이크·Vosk 응원 (`CheerKeywordEngine`, `VoiceBroadcastTrigger`).

**데미지 Owner 신고 RPC (`ReportHitServerRpc` 등) / `ApplyDamageWithOwnerReport`:** Phase 1에서 제거 대상 — **플레이어 본체 “내가 맞았다” 신고**와, 발사체 B안 Client 트리거→ServerRpc(§9.0.1)는 구분한다.

### 7.4 Punch (PvP 넉백)

- **Owner** Attack 입력 → `PunchServerRpc`(Host: 쿨다운·생존만 체크) → **Host 로컬 히트박스**(`PlayerPunchHitbox`, Host가 전 플레이어 Rigidbody를 시뮬레이션하므로 유효) 판정 → `NetworkDamageUtil.ApplyKnockback`(§9A.3) → `ClientRpc`로 피격자 Owner에만 `AddForce`.
- HP 데미지는 0(순수 넉백)이지만 **네트워크 진실(Host 판정 + 데미지 파이프라인 API)이 있는 기능** — §9.1의 Pattern **B(함정·피격)와 동일 권한 구조**. `PlayerPunch`/`PlayerPunchHitbox`는 §9.1의 "스테이지 컨텐츠" 표에는 넣지 않는다 (레벨디자이너가 배치하는 컨텐츠가 아니라 플레이어 대 플레이어 기본 동작이므로) — 여기 §7.4와 §9A.3에만 기재.
- **쿨다운 레이스 완화 (2026-09-01):** Owner 로컬 쿨다운(`_nextLocalPunchTime`)과 Host 쿨다운(`_nextPunchTime`)은 같은 `cooldown` 값이지만 시작 시점이 다르다 — Owner는 입력 즉시, Host는 `PunchServerRpc` 수신 시점(네트워크 지연만큼 늦음)에 시작. 이 차이 때문에 Owner 로컬 쿨다운이 끝나자마자 보낸 다음 펀치를 Host가 "아직 쿨다운 중"으로 거부하면, 애니(NetworkAnimator로 무조건 동기화)는 나가는데 사운드(`PlayPunchSfxClientRpc`)·넉백 판정만 빠지는 desync가 생김. **완화:** `localCooldownBuffer`(기본 0.15초)를 Owner 로컬 쿨다운에만 더해, Owner의 다음 허용 시점이 Host보다 항상 늦게 풀리게 함 — 완전 제거는 아니고 레이스 발생 빈도를 낮추는 실용적 절충(근본 해결은 "Host 승인 후 재생"으로 순서를 뒤집는 구조 변경, 반응성 트레이드오프 있어 보류).

---

## 8. 씬 로드 · 진행

- Host가 `NetworkSceneManager.LoadScene` (Tutorial 게이트 통과→`M.Stage1`, 스테이지 전환, 리로드).
- `SceneFlowManager.LoadNextScene`: `sceneSequence` 순서  
  (`Tutorial` → `M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`).
- `T.Boss` 클리어 → **`End.Demo`**.
- `End.Demo`: 클리어 UI → **타이틀 복귀**.

### 타이틀 복귀 시

| 모드 | 동작 |
|------|------|
| **멀티** | `NetworkManager.Shutdown()` + 세션·로비 상태 리셋 |
| **솔로** | NGO Shutdown + `GameSession` 등 런타임 상태 리셋 (멀티와 동일 경로) |

---

## 9. 호스트 판정 · 동기화

### 9.0 권한 매트릭스 (**현재 확정**)

| 카테고리 | 권한 | 이유 |
|----------|------|------|
| 플레이어 이동 | **Owner + CNT** | 입력 레이턴시 없음. **이 모델 유지 (Host 이동화 안 함)** |
| 플레이어 HP / 데미지 | **Host** | 치트 방지·판정 신뢰 |
| 함정 (ArrowTrap 등 발사자) | **Host** | 스폰 시점·스케줄을 전원 동일하게 |
| 발사체 **비행** | **Client (로컬 시뮬)** | Host 물리 복제 끊김 방지·시각 부드러움 |
| 발사체 **피격 판정** | **Host** (B안: Client 보고 → Host 확정) | §9.0.1 |
| 문·패드 등 규칙 오브젝트 | **Host** | 게임 규칙과 연동 |
| VFX / 사운드 | **ClientRpc → All** (또는 로컬) | 판정과 무관한 연출 |

**이동 = Owner + CNT 확정.** Host Authority + Client Prediction / Phase 2 이동 Host화는 **채택하지 않음**.

### 9.0.1 발사체 (ArrowTrap / TrapProjectile 등) — **B안 확정**

> **확정 (2026-07-13):** 비행 = Client 로컬 / 피격 최종 = Host.  
> **A안**(Host가 안 보이는 화살로 충돌 계산)은 **채택하지 않음**.

**한 줄:** Host가 화살을 “따라가게” 밀어 넣지 않는다. Client가 알아서 날리고, “맞았는지→HP”만 Host가 확정한다.

```
[Host]      Spawn + 초기 velocity 확정
    ↓
[Host]      ClientRpc(또는 NV)로 velocity(및 필요 상태) 브로드캐스트
    ↓
[각 Client] 위치 네트워크 따라가기(NetworkTransform 등) 없이
            받은 속도로 로컬 비행
    ↓
[Client]    화면 기준 OnTrigger/Collision → ServerRpc 피격 보고
    ↓
[Host]      최소 검증 후 NetworkDamageUtil → HP NV / 연출 Rpc / Despawn
```

**손대지 않는 것:** 함정 타이머(Host), Spawn=Host, `NetworkDamageUtil`=Host 데미지 진입점.

**구현 체크 (코드):**

1. 발사체 **위치 네트워크 따라가기** 제거/비활성 (끊김 원인 제거)
2. Spawn 직후 **속도 전달** + Client **자가 비행**
3. Client 충돌 → ServerRpc → Host 데미지 (발사체 전용; 구 플레이어 `ReportHitServerRpc`와 혼동 금지)
4. Host Despawn/파괴 동기화

#### 9.0.1-a 알려진 테스트 아티팩트 — ParrelSync 포커스 스로틀 (2026-07-21, 재조사 불필요)

**증상:** ParrelSync에서 **Client 창에 포커스**하면(= Host 창이 언포커스) `ArrowTrap` 등 함정 발사 스케줄이
Host 포커스 때보다 크게 어긋남 (예: 일자로 나가야 할 화살 5발이 흩어짐). Host 포커스 시엔 정확.

**원인:** `ArrowTrap`/`DropTrap`/`WindTrap`은 `targetTime`/`now` 계산엔 `NetworkManager.ServerTime`을
쓰지만, 실제 대기는 `yield return new WaitForSeconds(waitTime)` — **Unity 프레임 타이머** 기반.
Unity **에디터**는 포커스를 잃은 Play 창의 Update 틱 레이트를 낮춘다 (빌드 전용 설정인
`runInBackground`와 무관, 에디터 고유 동작). Host 창이 언포커스되면 이 코루틴 대기만 늘어지고,
그 사이에도 `ServerTime`은 그대로 흐르므로 다음 Spawn 타이밍이 흔들린다.

**결론 — 코드 수정 대상 아님:**

- ParrelSync는 같은 PC에서 에디터 두 창을 띄우는 **테스트 도구 특유 구조**. 실제 별도 프로세스(빌드
  끼리, 또는 Editor+Build) 테스트에서는 포커스와 무관하게 정상 프레임으로 돌아 재현되지 않을 것으로
  판단.
- B안(Host Spawn+velocity, Client 로컬 비행) 자체의 결함이 아니다. 스폰 권한을 Client 로컬로
  바꿔도 해결되지 않음 — 오히려 각 머신이 서로 다른 화살을 스폰해 새로운 불일치가 생길 수 있음.
- `WallMover`(§ Stage 이동 오브젝트)는 이미 `while (ServerTime < target) yield return null;` 형태의
  ServerTime 폴링을 쓰고 있어 이 스로틀에 강함. `ArrowTrap`/`DropTrap`/`WindTrap`은 `WaitForSeconds`
  기반이라 상대적으로 약하지만, **ParrelSync 테스트 아티팩트로 재확인된 이상 지금 고칠 필요 없음.**
  실기기 빌드에서 Host가 실제로 큰 프레임드랍(랙스파이크 등)을 겪을 때만 재검토 대상.

**AI 주의:** 이후 대화에서 "Client 포커스 시 함정 발사 스케줄이 어긋난다"는 보고가 다시 나오면,
재조사하지 말고 이 절을 먼저 인용해 "ParrelSync 에디터 포커스 스로틀 — 알려진 테스트 아티팩트,
코드 정상"이라고 답할 것. 실제 빌드/2PC에서도 재현된다는 새 증거가 있을 때만 재오픈.

- **게임 규칙**(패드, 문, 함정 **스케줄/상태**, Phase, 데미지, 클리어)은 **Host에서만** 최종 판정.
- 결과는 **`StageNetworkState` (중앙 매니저)** 등 `NetworkVariable` / RPC로 **전원에 공유**.
- Client도 **동일한 연출·상태**를 봄.
- 플레이어 위치는 **Owner + CNT** (§7.3 확정).
- **데미지·HP:** `NetworkDamageUtil` 단일 파이프라인 (§9A.3). 발사체만 §9.0.1 Client 보고 → Host 적용.

#### 9.0.1-b Deferred OnSpawn / PurgeTrigger 경고 — 축 분류표 (Axis A/B)

**증상:** 콘솔에 `[Deferred OnSpawn] Messages were received for a trigger of type
XxxMessage associated with id (N), but the NetworkObject was not received within
the timeout period 10 second(s).` → `Unity.Netcode.DeferredMessageManager.PurgeTrigger`.

**원칙:** 이 로그는 NGO의 `DeferredMessageManager`가 **메시지 종류(Rpc/ClientRpc/
NetworkTransformMessage 등)와 무관하게** "발신 시점에 로컬에 그 NetworkObjectId가 없음"을
찍는 **공용** 경고다. **로그 문구가 같다고 원인도 같다고 가정하지 말 것** — 아래 축으로
먼저 분류한 뒤 해당 축의 절만 본다.

| 축 | 대상 | 원인 메커니즘 | 상태 |
|----|------|--------------|------|
| **Axis A** | 짧은 수명 함정 발사체 (`TrapProjectile` 공유 — Arrow/Drop/Boulder) | 별도 Rpc가 Spawn 메시지와 다른 경로로 전송되어, 이미 Despawn된 id 또는 아직 CreateObject가 도착 안 한 id를 대상으로 함 | **확정 해결 (2026-07-27~28)** — 아래 3개 fix로 수렴 |
| **Axis B** | 지속 동기화 오브젝트 (Player, `ClientNetworkTransform`) — §11 사망/씬전환 | Owner가 매 틱 자동 전송하는 `NetworkTransformMessage`가 `destroyWithScene:true` 씬 리로드/전환으로 그 NetworkObject가 Despawn되는 시점과 겹침 | **미확정** — §11.5 참고, 재현 로그 확정 전 임의 수정 금지 |

**Axis A 확정 fix 3종 (재사용 가능한 패턴 — 새 함정 발사체 추가 시 그대로 적용):**

1. Spawn "전" `PrepareVelocity`/`PrepareWaypoints`로 예약 → NV에 기록 → Spawn 메시지 자체에
   실려 전파 (`TrapProjectile.cs`, `ArrowTrap.cs`, `DropTrap.cs`, `BoulderSpawner.cs`)
2. Rpc 타겟을 발사체 자기 자신이 아니라 상주 `StageNetworkState`로 이동
   (`ReportTrapHitServerRpc` / `RequestTrapDestroyServerRpc`)
3. Wall/Floor 중복 파괴 판정 자체를 제거 (`ArrowTrap.cs`, 재충돌마다 중복 요청 발생 원천 차단)

**Axis B에는 Axis A 패턴을 그대로 적용할 수 없다:** `ClientNetworkTransform`은 Rpc가 아니라
NGO가 매 틱 자동 전송하는 위치 델타라 재타겟도, Spawn 페이로드 번들도 불가능하다. 이 경고
하나만으로 Owner + CNT 이동 권한(§7.3 확정)을 바꾸는 방향은 채택하지 않는다.

**AI 주의:** 새 Deferred OnSpawn/PurgeTrigger 경고가 보고되면 먼저 이 표로 Axis A/B(또는
신규 축)인지 분류한다. Axis A 재발이면 위 3개 패턴 중 어긋난 지점만 찾는다. Axis B(또는
신규 축)면 재현 로그로 원인을 확정하기 전에는 코드를 고치지 않는다.

### MVP 동기화 대상

**우선순위:** `Must (Ship / 9/1)` → `Should (여유)` → `Post (출시 이후)`

**M.Stage1**

| 대상 | 우선순위 |
|------|----------|
| `StageStartGate` / 카운트다운 | Must |
| `PhaseManager` | Must |
| `ArrowTrap` / `DropTrap` — Host Spawn+초기속도, Client 비행, Host 피격 (§9.0.1) | Must |
| `TrapProjectile` 데미지·파괴 (Host 최종) | Must |
| `MouthController` / Mouth Animator 연동 | Should |
| `WindTrap` (Host 힘 적용) | Should |
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

### 9.1 스테이지 컨텐츠 패턴 (A–F) · M 우선

> **30일 로드맵 — 인게임 최우선:** **`M.Stage1`…`M.Stage5` → `M.Boss` 네트워크 완료.**  
> T·Cheer 확장·Steam·텔레메트리는 M 골격 이후.  
> 작업 보드(미확정 파이프라인): [`MStageNetworkBoard.md`](MStageNetworkBoard.md) → 확정 시 본 절·§9로 **승급**(발사체 B안과 동일).

#### 9.1.1 패턴 A–F (네트워크 진실 기준)

게임 느낌(“함정 같아 보임”)이 아니라 **무엇을 동기화해야 하는가**로 분류한다.

| 패턴 | 이름 | 네트워크 진실 | Host / Client | 정리 상태 |
|------|------|---------------|---------------|-----------|
| **A** | 연출·타일 껍데기 | 없음 (표시만) | C/D 결과를 따라 그림. 타일마다 RPC 금지 | 정리 대기 — §9.1.3 그룹 3 |
| **B** | 함정·피격 | 스폰·스케줄·데미지 | Host 판정 + `NetworkDamageUtil` (발사체=§9.0.1 B안) | 정리 대기 — §9.1.3 그룹 1 |
| **C** | 챌린지·라운드 | 시드·라운드·정답·타이머 | **Host 상태머신** → 상태만 복제. 오답 데미지는 B API 호출 | **완료 — §11B(챌린지 축)로 승격됨.** 이 표는 정의만, 세부는 §11B 참조 |
| **D** | 목표·게이트·클리어 | 시작·클리어·리로드 | Host만 Gate / Complete / 씬 리로드 (`StageNetworkState` 등) | **완료 — §11A(스테이지 진행 축)로 승격됨.** 이 표는 정의만, 세부는 §11A 참조 |
| **E** | 월드 모션 | 위치·이동 스케줄 | Host 타임라인(또는 ServerTime) + 위상/위치 동기화 | 정리 대기 — §9.1.3 그룹 2 |
| **F** | 플로우 인프라 | 씬 시퀀스 | `SceneFlowManager` / Relay — 컨텐츠별 설계 금지 | **완료 — §6A(룸·세션 축)/§11(플레이어 축)이 커버.** 이 표는 정의만 |

**판별:** 데미지 코드?→B · 정답/라운드?→C · 클리어/게이트?→D · Transform 스케줄?→E · 표시만?→A · 씬 파이프?→F.

> **PvP 동작(Punch 등):** 스테이지가 배치하는 컨텐츠가 아니므로 이 표에 넣지 않음. 권한 구조는 B와 동일 — §7.4 / §9A.3 참조.

#### 9.1.3 정리 작업 순서 (그룹 분할 · 미완료 패턴 A/B/E)

> D·F는 §11A/§6A로 이미 승격 완료. C는 축 #4(챌린지)로 완전히 이관 — 이 절에서 다루지 않음.
> 남은 A/B/E를 파일 규모 순으로 그룹화. **그룹 1(B) → 그룹 2(E) → 그룹 3(A)** 순서 권장 (B가 가장 사용 빈도 높고 급함, A는 네트워크 진실이 없다는 것만 확인하면 되는 가벼운 감사).

> **스코프 원칙 (확정):** 이번 정리 라운드는 **`M.Stage1`…`M.Stage5` → `M.Boss` 인스턴스로 한정**한다. T 전용 클래스(아래 표에 표시)는 **별도 T 라운드**로 미룬다. 이는 "데모 스코프 축소"가 아니라 — 같은 클래스를 M에서 먼저 검증/고정하고 T 인스턴스에 그대로 재사용하는 순서일 뿐, T도 결국 동일 라운드 방식으로 전부 다룬다(§9.1.4 "C 파이프라인은 OX에서 먼저 잠그고 복제" 원칙과 동일).
> 각 파일이 실제로 어느 씬에서 쓰이는지는 스크립트 `.meta` guid를 씬 파일에서 직접 대조해 확인한 것 — 추정 아님.

> 그룹 1(B) 파일별 네트워크/로컬 분류·진단 로그: [`TrapNetworkBoard.md`](TrapNetworkBoard.md) (`SpikeLane`/`SpikeLaneField`는 T 전용이지만 체감 버그로 스코프 순서보다 먼저 수정 완료 — 2026-07-21, 보드 §2 참조).
> T 전용(별도 라운드) 씬 인벤토리·결정 항목·작업 순서: [`TStageNetworkBoard.md`](TStageNetworkBoard.md).

| 그룹 | 패턴 | 이번 라운드(M) 대상 | T 전용 — 별도 라운드로 미룸 |
|------|------|-------------------|---------------------------|
| **1** | B 함정·피격 | `ArrowTrap`, `DropTrap`, `TrapProjectile`, `WindTrap`, `ContactDamage`(M.Stage3에도 있음), `TrapBase`, `TrapPlayerTracker`, `Breakable`, `Stage5ChaserHitbox` (+ `CeilingTrap`/`TrapSpeedPhase`/`SpikeTrap`은 현재 씬 배치 미확인 — 작업 중 재확인) | `SpikeLane`/`SpikeLaneField` (`T.Stage3`, `T.Boss`만 확인됨) |
| **2** | E 월드 모션 | `AdvancingWall` (**`M.Stage3` 사용, `T.Boss`에서도 재사용되므로 여기서 검증해두면 T 쪽도 절반 커버됨**) | `WallMover`, `WallMoverSequencer`, `BoulderSpawner`, `BoulderSpawnManager`, `WaypointMover`(Boulder 프리팹에 내장), `WallWaveController`, `WallLineRandomizer`, `MovingCorridor`, `AdvancingWallTelegraph` — 전부 `T.Stage1`/`T.Stage3`/`T.Stage4`/`T.Boss`에서만 확인됨 |
| **3** | A 연출 껍데기 | `MouthTrapAnimator`(+`MouthTrapAnimatorAnim`), `MouthWindAnimator`, `MouthExitTrigger`, `ColoredDoorVisual`, `ColoredPadVisual`, `RingBlendShapePulse`, `SafeZoneWarnSign` (`M.Boss` only — PhaseStartServerTime 로컬 스케줄, RPC 없음) 등 — M 인스턴스 위주로 확인 | (그룹 3은 네트워크 진실이 없다는 것만 확인하는 가벼운 감사라 M/T 구분 없이 봐도 무방) |
| **UI** | shared | `DeathOverlayUI` — `UI.prefab`, M/T 전 스테이지. 사망 문구 `{0}` = CheerName(`CheerService.GetCheerName`). Steam/OS DisplayName 아님 (2026-08-29: 로컬 경로에서 `u died`로 보이던 원인). | 동일 — T 대표 씬(`T.Stage1`)에서도 `UI.prefab` 인스턴스 |
| **UI** | shared | `OptionsTeamVoicePanel` / `OptionsMenuController` / `GameSettingsManager` — `Setting_Panel.prefab` (`Title` + `UI.prefab`, 전 M/T). 마이크 송신 볼륨·팀 보이스 수신 볼륨. 사후기록: §6B.7 P3 VoiceId 항목. | 동일 — T 대표 씬(`T.Stage1`) ESC 설정 |
| **SFX** | shared | `PlayerAudio` / `PlayerPunch` — `Kkultteok.prefab`, 전 M/T. 개인 SFX(ColorChange/Buff/Hit/Death/Run)는 Owner 2D. Punch/PunchHit는 전 클라 3D. 사후기록: 아래 포스트모템. | 동일 — T 대표 씬(`T.Stage1`) |

**그룹 1(B)은 M 트랩 인스턴스로 별도 에이전트가 진행 중.** 그룹 2(E)는 `AdvancingWall` 1개만 M이고 나머지 8개는 전부 T 전용이므로, **`AdvancingWall`은 그룹 1(B) 세션에 같이 묶고, 그룹 2(E) 세션은 T 전용 나머지만** 다루는 것을 권장 — 그러면 "패턴 E 세션 = 순수 T" 경계가 정확히 맞아떨어진다.

**공유 포스트모템 (2026-09-01, `PlayerAudio`):** 색 NV/`ApplyCheerBuffClientRpc`가 비주얼용으로 전 머신에 이벤트를 올리는데, `PlayerAudio`가 오너 가드 없이 `SFXManager.Play()`(2D)를 호출해 Client가 원격 플레이어의 ColorChange/Buff/Hit/Death를 거리 무관 풀볼륨으로 들었다. 개인 SFX는 Owner만 2D. Punch는 Owner 즉시 3D + 비오너 `PlayPunchSfxClientRpc`, PunchHit는 `NotifyPunchHitClientRpc`에서 전 클라 3D(애니는 Owner만). 스모크: `M.Stage1` Host+Client 색/버프/펀치, 반대 라운드 `T.Stage1` (§9B.4).

**공유 포스트모템 (2026-09-01, `SequenceRingMinigame` 정답/오답 SFX):** `AdvanceStep`/`ApplyWrongPenalty`가 `OnCorrectInput`/`OnWrongInput` UnityEvent를 직접 발동시켰는데, 이 두 메서드는 `TrySubmit`/`TrySubmitAnyKey`를 통해 **Host 판정 레인에서만** 호출된다(§11B ④Judge) — Client는 이 이벤트가 전혀 발동하지 않아 정답/오답 SFX(`SFXEventPlayer.Play()`, 2D)를 못 들었다. 스텝 진행(정답)은 `ChallengeStepBegin`(NV)로 Client에도 전파되지만 "오답"은 스텝 인덱스가 그대로라 NV만으로는 신호를 못 보냄. 수정: `StageNetworkState`에 `OnChallengeStepResult` 이벤트 + `NotifyChallengeStepResult(bool)`/`NotifyChallengeStepResultClientRpc` 추가(`ChallengeCleared`/`NotifyChallengeClearedClientRpc`와 동일한 "Host 로컬 즉시 발동 + RPC 보장 전달" 패턴). `SequenceRingMinigame`은 이제 `HandleChallengeStepResult`(owner 가드 포함, Host/Client 공통 구독)에서 `OnCorrectInput`/`OnWrongInput`을 발동. 스모크: `M.Stage4`/`M.Boss` Host+Client 정답/오답 시 양쪽 다 SFX 확인.

**공유 포스트모템 (2026-09-01, `AdvancingWall`):** `PermanentAdvance`/`PenaltyRoutine`이 `RunEntry`와 달리 이동 루프 SFX를 안 켜서, ColorTile 실패 패널티로만 움직이는 벽(`M.Stage3` `Tooth`, `T.Boss` 동일 경로)이 무음이었다. 1차 수정: `LerpTo` 전후에 `StartMoveLoop`/`StopMoveLoop`를 맞추고, 3D→2D(`spatialBlend = 0`)로 전환. 2차(같은 날): 2D로도 여전히 안 들림 — 원인은 `VolumeOverride` 2배가 `AudioSource.volume`(0~1 클램프) 경로라 못 먹는 것 + 패널티 0.6초가 클립 페이드인과 겹치는 것이었음. **최종: 패널티 이동은 무음으로 되돌리고(`PenaltyRoutine`에서 `StartMoveLoop`/`StopMoveLoop` 제거), 이동 루프 자체는 3D로 원복**(`moveSpatialBlend`/`moveMinDistance`/`moveMaxDistance`/`moveRolloffMode` 필드 복원 — `T.Boss` 스케줄 전진·후퇴가 이 루프를 그대로 쓰므로 2D는 과함). 실패 사운드는 `ColorTileChallenge.OnFail` → `SFXEventPlayer.Play()`(2D 단발, `M.Stage3`에 이미 배치된 컴포넌트 재사용)로 챌린지 쪽에서 별도 처리. 네트워크/위치 동기화는 변경 없음(Host/Client 각자 로컬 재생). 스모크: `M.Stage3` 챌린지 실패 시 단발 SFX 1회 + 반대 라운드 `T.Boss` 스케줄 전진·후퇴 이동음(3D, 패널티 시 무음) 확인.

#### 9.1.4 M 씬 작업 순서 (확정)

F·D 공통은 **검증 완료** 전제. 이후 **씬 단위**로 C(·필요 시 B/E)만 붙인다.

| 순 | 씬 | C (챌린지) | B / E (같이 볼 것) |
|----|-----|------------|-------------------|
| 1 | `M.Stage2` | ~~OXQuiz~~ → **SideSplit** — 코드 작성 완료, 씬 배치·검증 대기 (§11B.3) | Drop |
| 2 | `M.Stage3` | ColorTile | Drop, Contact, **E: AdvancingWall** |
| 3 | `M.Stage4` | SequenceRing | Drop |
| 4 | `M.Stage5` | GridColor + GridBW | Drop, Wind(Should), Mouth |
| 5 | `M.Boss` | SequenceRing, GridBW, DirectionalBarrier, PhaseSurvive | Drop, Wind · **D: BossFight** |
| — | `M.Stage1` | (챌린지 없음) | F·D·Survive — 공통 검증용 |

**C 파이프라인**은 OX에서 먼저 잠그고, ColorTile·Grid·Ring에 **동일 계약**을 복제한다. 챌린지마다 새 RPC 체계를 만들지 않는다.

---

## 9A. Authority 상세 · 마이그레이션

> **현재 확정 총표:** §9.0 / §9.0.1 (**이동=Owner+CNT 유지**, HP·함정·피격=Host, 발사체 비행=Client B안).  
> **Phase 1 (데미지 파이프라인 + 발사체 B):** 계속 진행.  
> **Phase 2 (이동 Host화):** **폐기.** CNT 제거·Host 이동·Client Prediction **구현하지 않음**.  
> **합의 갱신:** 2026-07-13 (이동 Owner+CNT 확정 · 발사체 B안).

### 9A.1 한 줄 요약 (**확정**)

```
Host   = HP · 피격 최종 판정 · 함정 스폰/스케줄 · 문/패드 등 규칙 · (발사체) 초기 velocity + 데미지 확정
Owner  = 이동(CNT) · 키 입력 · 카메라 · 개인 SFX(색/버프/피격/사망/런) · 로컬 마이크/응원
Client = 발사체 로컬 비행 (+ 트리거 감지 → ServerRpc 보고). VFX는 Rpc/로컬
Punch/PunchHit SFX = 전 클라 3D (월드). 개인 SFX와 분리 — §9.1.3 `PlayerAudio` 포스트모템.
```

**이동 Host화는 하지 않는다.** Owner + `ClientNetworkTransform`을 계속 가져간다.

### 9A.2 왜 Phase 1을 하나 (레거시 데미지 문제)

| 레거시 | 문제 |
|--------|------|
| `ApplyDamage` vs `ApplyDamageWithOwnerReport` | 함정마다 데미지 경로가 달라 신규 함정 추가 시 실수 |
| `ReportHitServerRpc` / `ReportInstantKillServerRpc` / `ReportFallDeathServerRpc` | 플레이어 **본체** “내가 맞았다” 신고 — Host와 이중 세계 |
| `Player.TakeDamage()` 온라인 차단 | `TryTakeDamage` 직접 호출 시 데미지 0 (`Stage5ChaserHitbox` 등) |
| `_hp` (Host) + `heart` (로컬) | 정식 경로 밖 HP 수정 시 UI·권한 불일치 |
| Host가 발사체 **비행까지** 시뮬 | Client에서 화살 끊김·튐 (§9.0.1로 해소) |

**참고:** 발사체 Client `OnTrigger`→ServerRpc(§9.0.1)는 **허용된 보고 경로**. 구 `ReportHitServerRpc`(플레이어 본체)와 **동일시하지 말 것**.

### 9A.3 허용 API (게임·함정 스크립트에서 사용)

**멀티 (NGO listening):**

| 용도 | API |
|------|-----|
| 일반 데미지 | `NetworkDamageUtil.ApplyDamage(player, amount)` |
| 즉사 (함정 타일·스테이지 Fail 등) | `NetworkDamageUtil.ApplyInstantKill(player)` |
| 순수 넉백 (HP 미변경) | `NetworkDamageUtil.ApplyKnockback(player, direction, force)` — Breakable 범위 넉백, `PlayerPunch` PvP, 문 닫힘, `ContactKnockback` 등 (§7.4) |
| 충돌 감지 (함정 본체·문 등) | `OnTriggerEnter` / `OnCollisionEnter` — **첫 줄 `if (!IsServer) return;`** |
| 발사체 비행 중 피격 | **Client** `OnTrigger` → **ServerRpc** → Host 검증 → 위 `ApplyDamage` (§9.0.1). Host-only Trigger **필수 아님** |
| 낙사 (void 추락) | **Owner** `y < fallDeathY` 1회 → `NetworkPlayerSetup.ReportFallDeathServerRpc` → Host `ApplyFallDeathFromServer` 확정. Host `Update` Y 체크는 Host-as-Owner 폴백 (2026-07-16 확정) |

**솔로 (NGO Host 1인):**

- 멀티와 동일 진입점. `NetworkDamageUtil.ApplyDamage` / `ApplyInstantKill` 사용. 별도 경로 없음.

**서버 내부 전용 (함정에서 직접 호출 금지):**

- `NetworkPlayerSetup.ApplyDamageFromServer` — `NetworkDamageUtil` 또는 Host 전용 시스템만 호출.
- `NetworkPlayerSetup.ApplyInstantKillFromServer` — `NetworkDamageUtil.ApplyInstantKill` 경유만.

**Owner 연출 (Host → ClientRpc로만 트리거):**

- `Player.TakeDamageVisualOnly`, `Player.ForceKill`, `Player.KillInstantly` — Host 확정 후 Owner에게만.

### 9A.4 제거·금지 (Phase 1에서 삭제)

| 항목 | 조치 |
|------|------|
| `NetworkDamageUtil.ApplyDamageWithOwnerReport` | 삭제. 호출처 → `ApplyDamage` |
| `NetworkPlayerSetup.ReportHitServerRpc` | 삭제 |
| `NetworkPlayerSetup.ReportInstantKillServerRpc` | 삭제. `ApplyInstantKill` → 서버 직접만 |
| `NetworkPlayerSetup.ReportFallDeathServerRpc` | ~~삭제~~ → **낙사 한정 복원 (2026-07-16).** Owner+CNT에서 Host 비오너 프록시가 바닥 콜라이더에 걸려 void 낙하를 못 보는 문제 → Owner 실좌표 Y 신고 → Host `ApplyFallDeathFromServer` 확정. **HP write는 여전히 Host만.** 피격·즉사 신고 RPC 복원은 계속 금지 |
| `ClientNetworkTransform` | **유지 (확정).** Owner 이동. 제거·Host NT 교체 **금지** |
| 함정/스크립트의 `Player.TakeDamage` / `TryTakeDamage` (온라인) | `NetworkDamageUtil` 로 교체 |
| `Breakable`의 `ApplyDamageFromServer` 직접 호출 | util 경유 또는 Host 전용 래퍼로 통일 |
| Host-only로 발사체 **비행** 강제 | §9.0.1 위반 — Client 로컬 비행으로 |

### 9A.5 Phase 1 — 데미지 · 함정 · HP (**이동은 Owner+CNT 확정 유지**)

**목표:** 데미지·피격·즉사·낙사를 Host 최종 파이프라인으로 통일. 플레이어 본체 Owner 신고 RPC 제거.  
**발사체:** §9.0.1 B안.

**이동:** Owner + `ClientNetworkTransform` **확정 유지**. Host가 보는 좌표와 Owner 좌표가 어긋날 수 있음 → 원격 함정 체감은 **감수** (이동 모델 바꾸지 않음).

#### 9A.5.1 작업 순서 (에이전트 실행용)

| # | 작업 | 파일·대상 |
|---|------|-----------|
| 1 | `NetworkDamageUtil` 정리 | `ApplyDamageWithOwnerReport` 삭제. `ApplyInstantKill` 서버 직접만 (`ReportInstantKill` 분기 제거) |
| 2 | Owner RPC 삭제 | `NetworkPlayerSetup`: `ReportHitServerRpc`, `ReportInstantKillServerRpc`, `ReportFallDeathServerRpc` |
| 3 | 함정 → `ApplyDamage` + 서버 가드 | `ContactDamage`, `SpikeTrap` 등 **본체** 함정. `TrapProjectile`은 §9.0.1 |
| 3b | 발사체 §9.0.1 | `ArrowTrap` / `TrapProjectile` — velocity Rpc, Client 비행, 피격 ServerRpc, Host 검증·데미지·Despawn |
| 4 | 문 닫힘 넉백 | `DoorController.cs` — `ApplyKnockback` (수평 Y=0), 서버 충돌만. 즉사 아님 |
| 5 | 낙사 | `Player.cs` — Owner Y 1회 신고 `ReportFallDeathServerRpc` → Host `ApplyFallDeathFromServer` 확정. Host `Update` Y는 폴백 유지 (2026-07-16: Host 단독 Y 판정은 Client void 낙사를 놓쳐 폐기) |
| 6 | 깨진 경로 수정 | `Stage5ChaserHitbox.cs` → `ApplyDamage` + 피격 시 `_chaser.NotifyHitFromHitbox()` (서버에서) |
| 7 | Breakable | `Breakable.cs` — `ApplyDamageFromServer` 직접 호출 → util/Host 규칙 통일 |
| 8 | 이미 Host 경로 (확인만) | `Enemy.cs`, `EnemyHitbox.cs`, `OXQuizManager.cs`, `Player.OnTriggerEnter`(EnemyBullet) |
| 9 | EnemyBullet | 서버에서 플레이어 `Rigidbody` 동적 유지, Trigger 판정 **서버만** 유효한지 프리팹·레이어 점검 |
| 10 | `WindTrap` | Owner 힘 예외 제거 → **Host**가 힘/속도 적용 (Should) |
| 11 | 주석 | Owner+CNT · §9.0.1 B안과 맞게. CNT 삭제/Host 이동 문구 **넣지 말 것** |

#### 9A.5.2 Phase 1 완료 판정

- [ ] `grep ApplyDamageWithOwnerReport` / `ReportHitServerRpc` / `ReportInstantKillServerRpc` — **프로젝트 0건** (`ReportFallDeathServerRpc`는 낙사 한정 허용 — §9A.3)
- [ ] ParrelSync **2인**: 화살·가시·ContactDamage·문 닫힘 넉백·낙사·Enemy·Chaser 각 **1회 이상** — HP·리로드 정상
- [ ] Host·Client **동일 HP** (`heart` UI = `_hp`). 이중 데미지 없음

### 9A.6 Phase 2 — 이동 Host화 (**폐기**)

> **채택하지 않음.** Owner + CNT를 **계속 유지**.  
> Host Authority 이동 · Client Prediction · CNT 제거 관련 설계/구현 **하지 말 것**.  
> 아래 절은 역사적 참고용으로만 남기며, 에이전트는 **실행하지 않는다**.

~~(구 목표: ClientNetworkTransform 제거, Host 단일 좌표계)~~ — **무효.**

#### 9A.6.1 Prefab · 컴포넌트

| 변경 | 내용 |
|------|------|
| 제거 | `ClientNetworkTransform` |
| 추가 | 서버 권한 `NetworkTransform` (기본 `OnIsServerAuthoritative() => true`) |
| 유지 | `NetworkObject`, `NetworkPlayerSetup`, `Player`, `Rigidbody`, `PlayerInput` |

#### 9A.6.2 `NetworkPlayerSetup` 동작

| 역할 | Owner | 비오너 Client | Host (Server) |
|------|-------|---------------|---------------|
| `PlayerInput` | ✅ 활성 | ❌ 비활성 | Owner인 경우만 ✅ |
| `Rigidbody` | Phase 2: **kinematic** (클라) | kinematic | **동적** — 전 플레이어 시뮬 |
| `Move()` / `FixedUpdate` | ❌ (입력만 NV 기록) | ❌ | ✅ 서버가 Owner NV 읽고 이동 |
| 카메라·음성·Vosk | Owner만 | ❌ | Owner만 |

**입력 동기화 (합의):**

- Owner가 `moveInput` (`Vector2`) 등을 **NetworkVariable** 에 기록 (Owner write).
- 서버 `FixedUpdate`에서 해당 플레이어 NV를 읽고 `Player.Move()` 실행.
- NGO NV 권한: 구현 시 **Server-only read** 가능 여부 확인. 불가 시 Owner write / Everyone read — **이동 처리는 서버만** (클라이언트는 NV 읽어도 `Move()` 호출 금지).

**색 전환 (`_isBlack`, `_isUniqueColor`):** 기존 Owner write NV 유지 가능 (표시·입력에 가깝고 Host HP와 분리).

#### 9A.6.3 `Player.cs` 변경 요약

- `FixedUpdate`의 `Move()` — **서버 전용** (`IsServer` 또는 `NetworkPlayerSetup` 플래그).
- Owner: `Update`에서 입력 → NV 쓰기만 (기존 `GetInput` 로직 유지, 물리 적용 제거).
- 낙사: 서버가 **전 플레이어** `transform.position.y` 판정 (Phase 1에서 RPC 제거 완료 전제).
- `isOwnerControlled` 의미: “로컬 입력·카메라 대상” (물리 주도 아님).

#### 9A.6.4 Phase 2 완료 판정

- [ ] `ClientNetworkTransform` / `ClientNetworkTransform.cs` 참조 **0건** (또는 클래스 Deprecated)
- [ ] ParrelSync 2인 → Dev Build localhost 2인 → **Steam 원격 2인** 순서 통과 (`ReleaseRoadmap.md` §3)
- [ ] 함정·즉사·낙사: Host / Client **체감 규칙 동일** (같은 좌표계)
- [ ] 이동 체감: **플레이 느낌**으로 예측 필요 여부 결정 (§9A.7)

### 9A.7 클라이언트 예측 (조건부, Phase 2 이후)

| 단계 | 규칙 |
|------|------|
| 기본 | **예측 없음** — `NetworkTransform` 보간만 |
| 테스트 | ParrelSync 2인 → Dev Build 2인 → Steam 2인, **각 30분+** 플레이 |
| 도입 기준 | **숫자(ms) 고정 없음** — “이동이 답답하다” 체감 시 **입력 예측만** 검토 |
| 범위 | MVP: 로컬에서 입력 반영 선행 → 서버 위치 도착 시 **짧은 보정**. 풀 FPS reconciliation은 Post |

### 9A.8 데이터 흐름 (목표 상태)

**이동:**

```
[Owner Client]  키 입력 → moveInput NV (Owner write)
       ↓
[Host Server]   NV read → Move() → Rigidbody → NetworkTransform sync
       ↓
[All Clients]   보간된 위치 표시 · Owner 애니 isRun 등
```

**데미지 (일반 함정·문 — Host Trigger):**

```
[Host Server]   OnTrigger (IsServer) → NetworkDamageUtil.ApplyDamage
                → ApplyDamageFromServer → _hp NV
                → NotifyHitClientRpc → Owner TakeDamageVisualOnly
```

**데미지 (발사체 — §9.0.1):**

```
[Clients]  로컬 비행 · OnTrigger → ServerRpc(피격자, 발사체 id 등)
[Host]     검증 → ApplyDamage → NV / ClientRpc 연출 · 발사체 Despawn
```

**문 닫힘 넉백:**

```
[Host Server]   Door OnCollision (IsServer, _isClosing) → ApplyKnockback (dir.y=0) → Owner AddForce
```

### 9A.9 NetworkVariable · Transform · Animator (기대 컴포넌트)

| 컴포넌트 | 역할 |
|----------|------|
| `NetworkObject` | 스폰·소유권 |
| `NetworkTransform` / CNT | **`ClientNetworkTransform` (Owner) 확정 유지** |
| `NetworkVariable` | `_hp`, `_shield`, `_colorIndex` (Server write), `_isBlack` / `_isUniqueColor` (Owner write), **`_moveInput`** (Owner write, Phase 2) |
| `Animator` | **별도 NetworkAnimator 필수 아님** — Owner 로컬 트리거 + Host `ClientRpc` 로 피격·사망 연출 |

복잡도는 NGO 컴포넌트 수가 아니라 **진실을 Host 한 곳에 모았는지**에 달림.

### 9A.10 솔로 (1인 Host)

- 멀티와 완전히 동일한 코드 경로. **`NetworkDamageUtil.ApplyDamage` / `ApplyInstantKill`** 동일 진입점.
- `Player.TakeDamage` 직접 호출은 온라인 시 no-op (early-return). 반드시 `NetworkDamageUtil` 경유.

### 9A.11 §16 구현 순서에 끼워 넣을 위치

**정식 출시 진행 — §9A:**

```
A. Phase 1 — 데미지 Host + 발사체 §9.0.1 B안   ← ParrelSync / Dev Build 2인 검증  (Must)
B. Phase 2 — 이동 Host화                         ← **폐기** (Owner+CNT 유지)
```

Phase 1 + 발사체 B가 Must. 이동 모델은 바꾸지 않음.

### 9A.12 FAQ (Authority)

**Q. 키 입력도 Host인가?**  
A. **아니오.** Owner PC.

**Q. 이동은?**  
A. **Owner + CNT 확정.** Host 이동화·Client Prediction **안 함**.

**Q. 화살이 Client에서 끊기면?**  
A. §9.0.1 B안 — Host는 Spawn+초기속도, **비행은 Client**, 피격 보고→Host 데미지.

**Q. Phase 2 없이 정식?**  
A. **의도된 방침.** 이동은 Owner+CNT 유지. Phase 1+발사체 B만 Must.

**Q. 2인 OK면 4인도 OK?**  
A. `ReleaseRoadmap.md` §3.1과 동일.

---

## 9B. 관측성 · 구조화 로그

> **배경:** M.Stage 라운드에서 "A버그 고치면 B버그, B 고치면 C, C 고치면 다시 A"가 반복된 핵심 원인은 Host/Client 중 어느 쪽이 무엇을 언제 봤는지 로그로 알 수 없었다는 것. 이 절은 공통 포맷 1개로 그 공백만 메운다 — 로그를 "많이" 찍는 절이 아니다.
> **적용 범위 (확정):** **T.Stage 신규 코드부터만 적용.** 기존 M.Stage 코드에는 소급 적용하지 않는다(사용자 확정, 2026-08). M 코드를 이 절 이유로 건드리지 말 것.

### 9B.1 유틸 — `Assets/Scripts/Network/NetLog.cs`

```csharp
public static class NetLog
{
    public static void Transition(string system, string evt, string details = null) { ... }
}
```

- 출력 포맷: `[Host]|[Client]|[Local] {system} {evt} {details}` — 예: `[Host] WallMoverSequencer SequenceStart seed=1234 startTime=12.34`
- Role 태그는 `NetworkManager.Singleton` 기준 자동 판정(`IsListening` 없으면 `[Local]`, 있으면 `IsServer`로 `[Host]`/`[Client]`) — 호출부가 문자열로 Host/Client를 직접 넣지 않는다.
- `details`는 key=value 나열(시드·인덱스·시각 등) 권장, 문장형 설명 금지.

### 9B.2 사용처 (전환점만 — 확정)

| 허용 | 금지 |
|------|------|
| Trigger 감지 (①) | `Update()`/`FixedUpdate()` 내부 매 프레임 호출 |
| RoundStart·시드 배포 (②) | 매 틱 폴링 로그 |
| Judge/Resolve (④⑤) | 값이 안 바뀌었는데도 반복 출력 |
| Scene Load/Ready | 순수 연출(A 패턴 타일 등) 갱신 로그 |
| 소유권 가드 실패 (교차 오염 감지 등) | — |

**원칙:** state transition(상태 전이) 1회 = 로그 1줄. 틱마다 찍으면 노이즈가 되어 오히려 관측성을 해친다.

### 9B.3 적용 대상

T.Stage 신규 코드(예: T 라운드에서 새로 작성/수정하는 스크립트)에서만 사용한다. 기존 M.Stage 코드는 이미 검증·승급 완료 상태라 로그 리팩터로 건드리지 않는다 — 필요하면 별도 요청 시에만.

작업 보드: [`TStageNetworkBoard.md`](TStageNetworkBoard.md).

### 9B.4 M/T 보드 간 공유 컴포넌트 버그 라우팅 (확정)

> **배경:** M/T 라운드가 별도 보드([`MStageNetworkBoard.md`](MStageNetworkBoard.md) / [`TStageNetworkBoard.md`](TStageNetworkBoard.md))로 나뉘어 있어, "지금 활성 라운드가 아닌 쪽"에서도 쓰는 공유 클래스(예: `StageStartGate`, `StageNetworkState`, `NetworkDamageUtil`, `PlayerSpawnCoordinator`)의 버그가 활성 보드에만 적혀서 반대쪽 라운드 작업자가 놓치는 사고를 막기 위한 규칙 (2026-08 확정).

- **판단 기준:** 버그가 M/T 어느 한쪽 전용 코드가 아니라 **양쪽이 같이 쓰는 클래스**(본 문서에 이미 계약이 적혀 있는 파일)에서 발견됐다면 "공유 버그"로 분류한다.
- **기록 위치 (발견 라운드 무관):** 공유 버그는 발견 즉시 그 컴포넌트가 이미 살고 있는 이 문서의 섹션(예: `StageStartGate`→§11A, `ChallengeOwner`→§11B.9)에 원인·수정 내용을 기록한다. "보드에 먼저 적고 나중에 승급"이 아니라 **바로 SSOT 직행** — 수정 시점엔 이미 원인·해결이 확정돼 있으므로 승급 대기 상태로 둘 이유가 없다.
- **보드엔 포인터만:** 발견 당시 활성 보드(M 또는 T)에는 "OO 버그, 공유 컴포넌트라 §X.Y에 기록"이라는 1~2줄 포인터만 남긴다. 내용을 보드에 중복 서술하지 않는다.
- **반대쪽 라운드 스모크 검증:** 공유 파일(본 문서에 계약이 명시된 파일)을 고쳤다면, 지금 작업 중인 라운드 씬뿐 아니라 **반대쪽 라운드 대표 씬 1개도 같이 ParrelSync 스모크 검증**한다 — 회귀가 반대쪽에서 조용히 발생하는 것을 막기 위함.

---

## 10. Random · 시드

| 상황 | 시드 |
|------|------|
| **사망 리로드** | **매번 새 시드** (퍼즐 배치·랜덤 연출 변경) |
| Tutorial 게이트 통과 (첫 진입) | Host가 세션 시드 생성 |

> **참고:** 인원 변경 후 리로드는 **없음**. 플레이어 이탈 = §12 방 종료.

대상: `StagePressurePadSetup`, `GameSessionColorDistribution`, `MouthController` 등 `Random` 사용처.  
Host 시드 기준 `InitState(seed + salt)` 통일.

---

## 11. 플레이어 수명주기 축 (A안 · SSOT)

> **한 줄:** 씬 로드 → 씬 스폰(`destroyWithScene:true`) → Owner 입력 → Ready 1회 → Play 소비.
> 사망·클리어·리셋 전부 **같은 축으로 재진입**한다. 평행 리스폰 경로 없음. 분기 없음.
> 버그 = 어느 칸의 불변식이 깨졌는가. 칸을 한 칸씩 거슬러 올라가 확인하고, **그 칸의 Writer만** 고친다.

### 11.0 축 (5칸 · 일방통행)

```
① Load → ② Spawn → ③ Owner → ④ Ready → ⑤ Play
                                            │
        사망/클리어/Reset ── Host LoadScene ─┘ (①로 재진입)
```

| 칸 | 불변식 (칸이 끝나면 참) | Writer (여기만 진실) |
|----|------------------------|----------------------|
| ① Load | Host/Client 동일 씬 로드 완료 | NGO SceneEvent (Host만 LoadScene — 아래 문 4개) |
| ② Spawn | 인원수만큼 `SpawnWithOwnership(destroyWithScene:true)`. 이전 씬 플레이어 없음. Host가 HP 등 서버 NV 초기화 | `PlayerSpawnManager.SpawnNetworkPlayers` **유일** |
| ③ Owner | Owner만 `PlayerInput` 활성. 비오너 Input off + kinematic. **위치 재조정 금지** — ②가 이미 `e.SpawnPos`로 Instantiate, Spawn 메시지가 그대로 복제됨. Owner는 검증 로그만(`VerifySpawnPosition`, 불일치 시 Warning만) | `NetworkPlayerSetup.OnNetworkSpawn` |
| ④ Ready | `OnPlayersReady` **씬당 1회**. 이후 `FindObjects`로 전원 조회 가능 | `PlayerSpawnCoordinator.NotifyPlayersReady` **유일** |
| ⑤ Play | Consumer는 Ready 이후에만 초기화. Consumer끼리 순서·의존 없음 | 없음 (아래 Consumer 목록) |

### 11.1 ① Load 로 들어오는 문 — 3개 (전부 Host만)

> 이 축(§11)은 **M/T 스테이지 씬**의 배치 스폰(씬 로드 시 인원수만큼 한 번에 `SpawnWithOwnership`)을 다룬다. **`Tutorial` 씬 내부의 개별 접속자 스폰(§6B.2)은 이 축과 다르다** — 배치가 아니라 접속 시마다 1명씩 즉시 스폰되고, `OnPlayersReady`류의 "씬당 1회 배치 완료 신호" 개념이 없다. Tutorial→M.Stage1 전환 시점(`TutorialGatherZone` 통과)부터는 아래 문 목록대로 이 축이 정상 적용된다.

| 문 | 경로 | 비고 |
|----|------|------|
| Tutorial 게이트 통과 | `TutorialNetworkManager`(가칭) → `LoadScene("M.Stage1")` | Coordinator 스폰(DDOL) 포함. 구 `LobbyNetworkManager.StartGameServerRpc` 역할 이전 |
| **사망 · ESC Reset** | Owner `RaiseDied` → `StageResetOnPlayerDeath` → `StageNetworkState.NotifyPlayerDeathServerRpc` → Host `LoadScene(현재씬)` | **1명 사망 = 전원 리로드** + **새 시드** 배포. ESC Reset(`EscMenuController.OnClickReset`, Host 버튼)도 **같은 문** 사용 (2026-07-17 통일). `DeathOverlayUI` 문구는 CheerName (`CheerService.GetCheerName`) — Steam/OS DisplayName이 아님. |
| 클리어 | `StageManager.OnStageClear` / `PhaseManager.onAllPhasesComplete` → **`SceneFlowRelay.LoadNextScene`** → `SceneFlowManager` | **확정 배선: Relay 경유** (씬에서 SceneFlowManager 직결 금지 — DDOL이라 Inspector 연결 불가) |

이 3곳 **외의** 스테이지 `LoadScene` 호출 금지. Client가 씬 로드 금지.

### 11.2 사망 루프 상세 (잠금 유지 항목)

- **1명 사망 = 전원 씬 리로드** (`StageResetOnPlayerDeath`). 리로드 후: 존 위 재스폰, `StageStartGate` 재진행, **새 시드** 퍼즐 재배치 (§10).
- 낙사 확정: **Owner** Y 신고 (`ReportFallDeathServerRpc`) → **Host** HP 0 확정 (§9A.3). Host 단독 Y 판정은 Client void 낙사를 놓치므로 사용하지 않음.
- 리스폰 = **씬 리로드가 전부**. `destroyWithScene:true`로 옛 플레이어 자동 Despawn → ②에서 클린 스폰 → HP/포즈/색이 초기 상태. 별도 리셋 코드 불필요.
- `Player.IsDead`: 애니메이션·콜라이더·물리 정지는 `Die()`를 통해 **Owner 머신에서만** (Fix A, 의도된 설계). 단 Host는 원격 플레이어 Rigidbody도 직접 시뮬레이션(§9A)하므로, 비오너 머신에서도 `IsDead` 플래그만 별도 동기화(`Player.SyncDeadFlag()`, 2026-07-17) — 트랩·피격 판정이 사망 상태를 인지하도록. HP NetworkVariable이 실질 가드라 지금까지 증상은 없었음, 방어 차원.

### 11.3 ⑤ Play Consumers (Ready 구독만 — 나열은 목록, **실행 순서 아님**)

`NetworkPlayerSetup`(카메라 bind) · `GameSession` · `StageResetOnPlayerDeath` · `ColoredStartZone` · `StagePressurePadSetup` · `TrapPlayerTracker` · `PlayerHPUI` · `TeamStatusUI` · `CheerProgressUI` · `ChangeColorCooldownUI`

`TeamStatusUI` — **shared** (`UI.prefab`, Tutorial + 전 M.* / T.* 씬). Tutorial 순차 합류는 Ready만으로는 명단이 안 늘어나 `OnRosterChanged`(로컬 스폰/Despawn)로 재구성. Ready+Roster 구독은 다음 프레임 1회 디바운스(`RequestRebuild`) — 즉시 리빌드하면 Despawn 중인 오브젝트가 슬롯에 남고, M/T 배치 스폰은 N+1회 중복. DisplayName은 `CheerService.GetCheerName`과 동일 우선순위(세션 확정값 → 게이트 전 실시간 NV). CheerName 즉시 반영은 `PlayerCheerNameSync.OnAnyCheerNameChanged`. 사후기록: §6B.7 버그 3. 반대 라운드 스모크: `M.Stage1` Host+Client TeamStatus (§9B.4). **DisplayName 글자 (2026-09-02):** 8/28 잘림 대응 오토사이즈(8–13pt)가 닉네임을 과도 축소 → 오토사이즈 제거, NoWrap+Ellipsis, `nameFontSize` 인스펙터. 이름/하트는 슬롯 높이 자동 확장으로 간격 확보(VLG `slotSpacing` 유지).

공통 패턴 (전 Consumer 동일):

```csharp
PlayerSpawnCoordinator.OnPlayersReady += Handler;
if (PlayerSpawnCoordinator.IsReady) Handler();   // 늦은 구독 대비
// OnDestroy: -= Handler
```

- Consumer ↔ Consumer 대기·순서 강제 금지.
- Ready 전 실패(플레이어 못 찾음 등)를 복구 분기로 메우지 말 것 → ①~④ 불변식부터 확인.

### 11.4 금지 (평행 축 — 발견 즉시 삭제)

| 항목 | 이유 |
|------|------|
| `Player.Respawn()` / `ForceRespawn()` | 리스폰 = 씬 리로드. **삭제 완료 (2026-07-17)** — 부활 금지 |
| `PlayerSpawnManager` 외 플레이어 Spawn / 응급 `Instantiate` | ② Writer 유일 |
| `SceneFlowManager.ReloadCurrentScene` / `SceneFlowRelay.ReloadCurrentScene` | 리로드 Writer = `StageNetworkState` 유일. **둘 다 삭제 완료 (2026-07-17)** — 부활 금지 |
| 다른 클래스에서 `OnPlayersReady` 수동 Invoke | ④ Writer 유일 |
| “카메라 없으면 다시 찾기” 류 복구 if | 칸 불변식 위반을 우회 — 원인 칸을 고칠 것 |
| DDOL 플레이어 가정 (구 §11 `destroyWithScene:false` + `ResetForNewStage`/`ResetStageClientRpc`) | **폐기.** 플레이어는 씬 스폰. DDOL은 Coordinator·매니저·`LocalPlayerCamera`만 |

### 11.5 증상 → 볼 칸 (진단 사다리)

| 증상 | 먼저 볼 칸 | 그다음 |
|------|-----------|--------|
| 플레이어 실종 | ② 스폰 수 | ① 씬 로드 여부 |
| HP 안 참 / 옛 상태 잔존 | ② Host NV 초기화 | 레거시 `Respawn` 잔재 |
| 입력 안 됨 / 이중 입력 | ③ Owner 분기 | Instantiate 직후 Input 선점 옆길 |
| 카메라 안 붙음 | ④ Ready 발행 | ⑤ bind 구독 타이밍 |
| UI/패드 일부만 깨짐 | ⑤ 해당 Consumer만 | ④ OK 확인 |
| 죽어도 리로드 없음 | 사망 문 (Owner 가드 → ServerRpc → Host) | — |
| `Deferred OnSpawn`/`PurgeTrigger` 경고 (Player NetworkObjectId 관련 의심 시) | §9.0.1-b 축 분류표로 Axis A/B 먼저 분류 | Axis B면 사망 문 리로드 딜레이(`StageNetworkState.deathReloadDelay`, 초 단위 — 2026-08 UX 개선으로 1프레임 상수에서 전환, 필요 최소치보다 훨씬 큼) 또는 ② Spawn `destroyWithScene:true` 타이밍 재현 |
| 챌린지 콘텐츠(타일 등)가 한쪽 머신에서만 조용히 하나도 안 생김(에러/경고 無) | §11.7 — 해당 Consumer가 §11.3 표준 구독 패턴(`IsReady` 늦은 구독 가드) 지키는지 | ④ Ready 발행 자체는 정상인지(다른 Consumer는 정상 동작하는지) |

규칙: 한 칸씩 위로. 깨진 불변식이 설명되면 **정지**. 그 칸 Writer(+같은 가정 Consumer)만 고침. 칸에 복구 if 추가 금지. 코드 수정 전 Broken step/근거/Fix plan 제시.

### 11.6 검증 (Foundation — ParrelSync 2인)

1. Title → Tutorial → M.Stage 진입: 이동·카메라·HP UI
2. Host 사망 1회 → 리로드 → 인원수 스폰 → Ready → 카메라/HP 정상
3. Client 사망 1회 → 동일
4. 클리어 1회 → 다음 씬 → 같은 축 재통과
5. `grep`: `Player.Respawn` / `ForceRespawn` / `ReloadCurrentScene` — 프로젝트 정의·호출 **0건** (삭제 완료 상태 유지)

### 11.7 `GameSession` Ready 늦은 구독 누락 — 콘텐츠 조용한 실패 버그 수정 (2026-07-28)

**증상:** `M.Stage3` `ColorTileChallenge`가 Host 화면엔 타일이 정상 생성되는데 Client 화면에선 아무 에러·경고 없이 타일이 하나도 안 생김. ParrelSync 재현 시 매판 되거나 안 되거나 하는 **간헐적** 증상.

**원인:** §11.3 표준 Consumer 패턴(`OnPlayersReady += Handler; if (IsReady) Handler();`)을 `GameSession`만 지키지 않고 있었다(`OnSceneLoaded()`에서 `+=`만 걸고 `IsReady` 늦은 구독 체크 누락). `OnPlayersReady`는 네트워크로 오는 신호라 도착 시점이 매판 미세하게 다른데, 이 신호가 `GameSession`의 구독보다 먼저 도착해버리면 그 씬 내내 `_activePlayers`가 빈 채로 굳는다. `ColorTileChallenge.HandleChallengeStepChanged`는 `GameSession.GetActivePlayers()`로 생존 색을 구하고, 결과가 비면 `colors.Count == 0`으로 **로그 한 줄 없이** 조용히 return하기 때문에 콘솔에 아무 흔적도 안 남았다.

**수정:** `Assets/Scripts/GameSession.cs` `OnSceneLoaded()`의 `PlayerSpawnCoordinator.OnPlayersReady += RefreshPlayersOnReady;` 바로 뒤에 `if (PlayerSpawnCoordinator.IsReady) RefreshPlayersOnReady();`를 추가 — §11.3 표준 패턴대로 통일. 다른 신규 로직 없음, 순수 catch-up 1줄.

- `GridColorChallenge`/`GridBWTileChallenge`도 동일하게 `GameSession.GetActivePlayers()`/`GetActiveColors()`에 의존하므로 같은 레이스의 잠재 피해자였다 — 이번 수정으로 함께 해소됨.
- **ParrelSync 2인 재검증 통과 (2026-07-28)**: 콘솔 `[GameSession] N인 모드 적용` 로그가 스테이지 진입마다 찍히고, `M.Stage3` Client 화면에 타일 정상 생성 확인.
- 상세 반영 내용: [`MStageNetworkBoard.md`](MStageNetworkBoard.md).

### 11.8 Client 스폰 1프레임 워프 — 프리팹 저장 좌표 + Rigidbody 미동기화 (2026-08-17)

**증상:** `M.Stage`(2~5, Boss 포함) Client 스폰 직후 1프레임 만에 스폰 좌표와 무관한 임의 좌표로 튀어 낙사. Host는 재현 안 됨.

**원인:** `Player1.prefab` 루트 `m_LocalPosition`이 `(-167.18423, 0, -20.28356)`으로 저장돼 있었다(과거 `2.Tutorial.unity` 인스턴스를 Apply to Prefab하며 그 씬 좌표가 프리팹 원본에 박제됨). Host는 `Instantiate(prefab, e.SpawnPos)`로 위치를 직접 지정해 이 값을 거치지 않지만, Client는 프리팹 기본 포즈로 인스턴스화 후 스폰 메시지로 **Transform만** 보정한다 — `Rigidbody`(`isKinematic=false`, `Interpolate=on`)의 물리 포즈는 프리팹에 박힌 좌표에 남아있다가 다음 물리 틱에 Transform을 그 값으로 되돌렸다. `NetworkPlayerSetup.EnablePhysics()`가 velocity만 리셋하고 `rb.position`을 스폰 Transform에 맞추지 않은 게 직접 원인.

**수정:**
1. `Player1.prefab` 루트 좌표 `(0,0,0)` 원복(사용자, 에디터 — 프리팹 파일은 에이전트가 직접 쓰지 않음).
2. `NetworkPlayerSetup.EnablePhysics()`에 `_rb.position = transform.position; _rb.rotation = transform.rotation;` 추가 — ②Spawn이 이미 확정한 `e.SpawnPos`에 물리 바디를 맞춤. **Writer는 여전히 `PlayerSpawnManager` 하나** — 좌표 재계산·워프 분기 없음, `VerifySpawnPosition`은 계속 검증만.

**영향 범위:** Player 스폰/물리 계층 — M/T 공유. 다른 프리팹(예: 챌린지 소품)도 씬에서 Apply to Prefab 하면 같은 방식으로 재발 가능 — 프리팹 원본은 원점 기준으로만 저장할 것.
- 상세: [`MStageNetworkBoard.md`](MStageNetworkBoard.md) "M.Stage 스폰 위치 버그" 절.

---

## 11A. 스테이지 진행 축 (SSOT)

> **한 줄:** 존 점유 → 카운트다운 → `StartStage` → **Host 레인 하나**만의 판정(Tick/Complete/Fail) → Resolve(Clear 또는 Fail) → **Clear는 §11 ①Load(다음 씬)로, Fail은 §11 사망 문으로** 재진입한다.
> 이 축은 §11(플레이어 수명주기)의 **"⑤ Play" 구간** 안에서 스테이지 콘텐츠가 실제로 어떻게 진행·종료되는지를 다룬다. 챌린지(OX퀴즈 등) 내부 판정 로직은 별도 축(§9.1 패턴 C) — 여기선 `StageObjective.Begin/Tick/Complete/Fail` **계약 경계까지만** 다룬다.
> 버그 = 어느 칸의 불변식이 깨졌는가. §11과 동일하게, 칸을 한 칸씩 거슬러 올라가 확인하고 **그 칸의 Writer만** 고친다.

### 11A.0 축 (5칸 · 일방통행)

```
① Gate → ② Start → ③ Progress → ④ Resolve → ⑤ Exit
                                                 ├─ Clear → §11 ①Load 재진입 (다음 씬)
                                                 └─ Fail  → §11 사망 문 재진입 (전원 리로드, 같은 씬)
```

| 칸 | 불변식 (칸이 끝나면 참) | Writer (여기만 진실) |
|----|------------------------|----------------------|
| ① Gate | 활성 색 전원 `ColoredStartZone` 점유해야 카운트다운 시작. Host `ServerTime` 기준 전원 동일 타이머 | `StageStartGate`(Host `Update`) → `StageNetworkState`(`countdownStartServerTime`/`isCountdownActive` NV) |
| ② Start | `StageManager.StartStage()`는 **Host 레인에서만** 의미를 가진다 — objectives `Begin()`, traps `Activate()`, floor `Start()` | `StageManager.StartStage()` — Host 레인 |
| ③ Progress | `Tick()`/`Complete()`/`Fail()` 판정은 **Host 레인 하나만** 존재한다. Client는 이 판정 루프의 "결과"를 진실로 취급하지 않는다 (필요한 표시는 Host 브로드캐스트로만 관찰) | `StageManager.Update()` — Host 레인 |
| ④ Resolve | Cleared/Failed는 Host가 **한 번만** 확정 | `StageManager` (`_isCleared`/`_isFailed`) — Host 레인 |
| ⑤ Exit | 확정된 결과에 따라 **기존 문을 재사용** — 새 리로드/전환 경로 금지 | Clear: `SceneFlowRelay → SceneFlowManager.LoadNextScene`(Host만 `LoadScene`). Fail: `NetworkDamageUtil.ApplyInstantKill`(전원, Host) → §11 사망 문 |

**"Host 레인"이 의미하는 것:** 가드(`IsServer` 등)는 나중에 Client가 끼어들어서 막는 패치가 아니다. **StageManager 판정 자체가 원래 Host 하나에서만 존재하는 개념**이고, 코드에서 Client 인스턴스가 같은 계산을 중복 수행하지 않게 만드는 것은 그 진실을 코드로 그대로 옮기는 것뿐이다. Host 1벌 + Client 1벌, 이중 계산이 남아있다면 그 자체가 이 축 위반.

### 11A.1 ① Gate 상세

- `ColoredStartZone.OnTriggerEnter/Stay` — 로컬 점유 판정(색 매칭 + 생존). 네트워크 가드 없음 — **판정 자체는 표시/조회용**, 카운트다운 시작 여부는 Host만 사용.
- `StageStartGate` (Host `Update`): `AllZonesOccupied()` → `MarkCountdownStart()`(NV) → 타이머 → `CompleteCountdown()` → `stageManager.StartStage()` + Host가 `MarkStageStart(gateId)`로 `StageStartSignal{serverTime, gateId}` NV 기록.
- Client는 `StageStartSignal` NV 감지로 자기 화면에서도 `StartStage()`를 부른다 — **이건 ②Start 진입 트리거 전파일 뿐, ③Progress 판정에는 관여하지 않는다.** ②의 `objectives.Begin()`이 Client 로컬에서도 돌아가더라도, 그 이후 Complete/Fail 판정의 진실은 오직 Host.
- **씬에 `StageStartGate`가 여러 개면(T.Stage2/4/5) 각 게이트가 자기 `gateId`를 신호에 실어 보내고, Client는 자기 `gateId`가 찍힌 신호만 자기 것으로 인정한다** — 2026-08 버그 수정, §11A.7 참고. 게이트가 1개뿐인 씬(M 전체, T.Stage1/3/Boss)은 기본값(-1)이 항상 자기 자신과만 일치하므로 영향 없음.

### 11A.2 ③ Progress — Host 레인과 챌린지 축(#4)의 경계

- `StageObjective` 계약(`Begin/Tick/Complete/Fail/ResetObjective`)은 **Stage 축이 소유**한다.
- 계약 "안"에서 "무엇을 판정 기준으로 삼는가"(정답, 시퀀스, 그리드, 타이밍 등)는 **챌린지 축**(§11B, §9.1 패턴 C) 소관 — 이 문서 §11A는 그 내부 규칙을 정의하지 않는다.
- Stage 축이 요구하는 것은 딱 하나: **`Complete()`/`Fail()` 호출은 Host 레인에서만 일어난다.** 챌린지 구현이 Client 트리거로 직접 `Complete()`/`Fail()`을 호출하면 챌린지 축이 아니라 **이 §11A 위반**.

### 11A.3 ④→⑤ Resolve → Exit

**Clear:**
```
[Host] 전 objective Completed
  → StageManager: _isCleared = true, traps/projectile 정리
  → OnStageClear (Host 레인에서만 유효한 신호)
  → SceneFlowRelay.LoadNextScene → SceneFlowManager.LoadNextScene
  → Host만 NetworkSceneManager.LoadScene (§11 ①Load 재진입, 다음 씬)
```
Client에도 씬 로드가 그대로 전파되므로(NGO SceneEvent) **별도 "Cleared" NetworkVariable을 새로 만들지 않는다** — 씬 전환 자체가 이미 브로드캐스트. 새 동기화 프리미티브 발명 금지(§9 Sync 규칙과 동일 원칙).

**Fail:**
```
[Host] objective.IsFailed 감지
  → StageManager: _isFailed = true
  → OnStageFailed (Host 레인) → NetworkDamageUtil.ApplyInstantKill(전원)
  → 각 플레이어 HP NV → 0 → PlayerEvents.OnDied
  → §11 사망 문 (StageResetOnPlayerDeath → StageNetworkState.NotifyPlayerDeathServerRpc → 전원 씬 리로드)
```
**Fail은 사망과 다른 리로드 경로를 만들지 않는다.** `StageManager.ResetStage()`(부분 리셋)는 이 축에서 쓰지 않음 — §11A.4 금지 목록.

### 11A.4 금지 (평행 축 — 발견 즉시 삭제 대상)

| 항목 | 이유 |
|---|---|
| `StageManager.ResetStage()` | 호출처 0. "부분 리셋" 평행 경로 — 리셋 = 씬 리로드(§11)가 유일 |
| `PhaseManager.RestartCurrentPhase()` | 코드에 존재하지 않음, 주석에서만 참조되는 유령 메서드 — 만들지 않음 |
| `StageManager.OnStageFailed`를 사망 문 이외 경로(부분 리셋 등)에 연결 | §11A.3 Fail 규칙 위반 |
| Client가 ③ Progress 판정(Tick/Complete/Fail) 결과를 독자적으로 신뢰 | Host 레인 단일 진실 위반 |
| `BroadcastStartStageClientRpc` + `_stageStartServerTime` NV — ②Start 이중 시작 신호 | 트리거 전파는 **하나의 메커니즘**만 (현재 NV 감지가 실질 경로 — no-op RPC는 정리 대상) |
| `StageStartGate`/`StageResetOnPlayerDeath`/`ReachZoneObjective`/`BoulderSpawner`의 "오프라인" 분기·주석 | 프로젝트 온라인 전용 확정(`architecture.mdc`) — 삭제 대상 |

### 11A.5 증상 → 볼 칸 (진단 사다리)

| 증상 | 먼저 볼 칸 | 그다음 |
|------|-----------|--------|
| 게이트 카운트다운 안 뜨거나 씹힘 | ① Gate (Host `AllZonesOccupied`/NV) | `ColoredStartZone` 점유 판정 |
| 트랩 안 움직임 / 스테이지가 시작이 안 됨 | ② Start (`StartStage` 호출 여부) | ① Gate 완료 여부 |
| Client 화면에서만 먼저 클리어/실패로 보임 | ③ Progress Host 레인 위반 (Client 로컬 계산 여부) | ④ Resolve 중복 판정 |
| 클리어했는데 다음 씬으로 안 넘어감 | ⑤ Exit-Clear (Host `LoadScene` 가드) | ④ Resolve `_isCleared` 확정 여부 |
| 실패 판정 났는데 아무 일도 안 일어남 | ⑤ Exit-Fail (`ApplyInstantKill` 연결 여부) | ④ Resolve `_isFailed` 확정 여부 |
| 리로드 후에도 이전 스테이지 상태 잔존(트랩 계속 날아다님 등) | §11 ①Load 재진입 정합성 | ② Start 재초기화 |
| Client 화면만 존 점유·카운트다운 없이 스테이지가 저절로 시작(Host는 정상 대기) — 씬에 게이트가 여러 개 | ① Gate `gateId` 배정 여부/중복 여부 (§11A.7) | 해당 게이트 Inspector `gateId` 값 확인 |

규칙: 한 칸씩 위로. 깨진 불변식이 설명되면 **정지**. 그 칸 Writer만 고침. 칸에 복구 if 추가 금지.

### 11A.6 §11(플레이어 축)과의 관계

- 이 축은 §11 **"⑤ Play"** 구간의 스테이지 콘텐츠 세부 축이다. §11의 ①~④(Load/Spawn/Owner/Ready)는 그대로 선행 조건.
- **Fail Exit은 §11 사망 문을 그대로 재사용** — 새 사망/리로드 정의 금지.
- **Clear Exit은 §11 ①Load에 재진입**(다음 씬) — 새 씬 전환 정의 금지.
- 즉 §11A는 §11에 새 문을 추가하지 않는다. 기존 두 문(사망 문 / ①Load 문)에 스테이지 콘텐츠가 **어떻게 도달하는지**만 정의한다.

### 11A.7 다중 게이트 씬 `_stageStartServerTime` stale 재점화 버그 수정 (2026-08)

**증상:** `T.Stage5`에서 Stage5.1 게이트 클리어 후 Stage5.2로 넘어갈 때, Client 화면의 타이머가 존 점유·카운트다운 없이 저절로 흐름. Host는 자기 `AllZonesOccupied()`를 실제로 기다리므로 정상 대기 — Host/Client가 서로 다른 시작 타이밍을 봄.

**원인:** `StageStartGate` 신호(당시 `_stageStartServerTime` 단독 `double` NV)는 씬 하나에 게이트가 여럿이어도 슬롯 하나를 전부 공유했다. 이 값을 되돌리는 코드가 없어서, 앞 게이트(Stage5.1)가 완료 시 찍은 타임스탬프가 그대로 남아있는 상태에서 뒤 게이트(Stage5.2)가 `PhaseManager.EnterPhase()`의 `onPhaseEnter`로 `Arm()`되는 순간, `UpdateCountdownOnClient()`가 `StageStartServerTime > 0 && _isArmed`만 보고 "이미 시작됨"으로 오인해 즉시 `StartStage()`를 호출했다(`StageStartGate.cs` `UpdateCountdownOnClient()`). 씬 전체를 세어보니 T.Stage5(게이트 4개)뿐 아니라 **T.Stage2(3개)/T.Stage4(2개)도 동일 구조**라 같은 버그를 안고 있었음 — M 라운드는 씬당 게이트가 1개뿐이라 미해당.

**수정:** `_stageStartServerTime`(단독 `double`)을 `StageStartSignal{serverTime, gateId}` 구조체 NV(`_stageStartSignal`)로 교체 — `ChallengeStepState`(§11B.2)와 동일하게 "연관 데이터는 하나의 NV로 원자적으로" 원칙 적용. `StageStartGate`에 `[SerializeField] int gateId`를 추가해 게이트마다 서로 다른 값을 갖게 하고, `MarkStageStart(gateId)`가 시간과 gateId를 같이 기록, `UpdateCountdownOnClient()`는 `StageStartGateId == gateId`까지 확인해서 다른 게이트의 낡은 신호를 걸러낸다. (baseline 스냅샷 방식도 검토했으나, `_currentPhase` NV와 `_stageStartSignal` NV가 서로 다른 NV라 도착 순서에 의존하게 되는 구조적 레이스가 남아 — 특히 `surviveDuration=0` 패스스루 Phase가 게이트 Arm 직전에 낄 경우 — gateId 방식으로 그 의존성 자체를 없앴다.) 게이트가 1개뿐인 씬은 기본값(-1)이 자기 자신과만 비교되므로 영향 없음 — Inspector 작업 불필요.

**영향 파일:** `Assets/Scripts/Network/StageNetworkState.cs`(`StageStartSignal` 구조체 신설, `_stageStartServerTime`→`_stageStartSignal`, `MarkStageStart()`→`MarkStageStart(int gateId)`, `StageStartGateId` 프로퍼티 추가), `Assets/Scripts/Stage/StageStartGate.cs`(`gateId` 필드 + 중복/미설정 시 `Debug.LogError` 가드, `CompleteCountdown()`/`UpdateCountdownOnClient()` 갱신).

**사용자 Inspector 작업 (필수 — 코드만으론 끝나지 않음):** 게이트가 여러 개인 씬만 대상 — `T.Stage5`(Stage5.1~5.4 게이트 4개 → `gateId` 0/1/2/3), `T.Stage2`(게이트 3개 → 0/1/2), `T.Stage4`(게이트 2개 → 0/1). 게이트가 1개뿐인 씬(M 전체, `T.Stage1`/`T.Stage3`/`T.Boss`)은 손댈 필요 없음. 잘못 설정하면(미설정 또는 중복) `Awake()`에서 콘솔에 에러 로그가 뜨므로 ParrelSync 검증 전에 콘솔로 먼저 확인할 것.

**검증 상태:** 코드 반영 완료, **ParrelSync 2인 검증 미실시** — 위 Inspector 작업 완료 후 `T.Stage5`(Stage5.1→5.2 전환)에서 Client가 존 점유·카운트다운 없이 시작되지 않는지 확인 필요. §9B.4 규칙에 따라 이번 라운드(T) 씬뿐 아니라 **반대쪽 라운드(M) 대표 씬 1개**(예: `M.Stage1`, 게이트 1개)도 회귀 스모크 필요 — 단일 게이트 씬은 기본값(-1) 그대로 정상 동작해야 함.

---

## 11B. 챌린지 축 (C 패턴 — SSOT)

> **한 줄:** Trigger(Host 감지) → RoundStart(Host가 시드 NV 배포) → Generate(전 머신 로컬 재생성) → Judge(Host 레인만) → Resolve(Complete/Fail → §11A ③Progress로 반환).
> 이 축은 §11A(스테이지 진행 축) **"③ Progress"** 안에서, C 패턴(챌린지: OX/ColorTile/GridColor/SequenceRing 등)이 시드·라운드를 실제로 어떻게 동기화하는지 정의한다. §9.1 패턴 C("네트워크 진실 = 시드·라운드·정답·타이머")의 구현 계약.
> **최초 잠금·검증 완료:** `OXQuizManager`(`M.Stage2`) — ParrelSync 2인 재테스트 통과. 이후 챌린지(ColorTile/GridBW/GridColor/SequenceRing)는 새 설계를 발명하지 않고 이 축을 그대로 재사용한다(§9.1.3 "OX에서 먼저 잠그고 복제" 원칙 실행 완료). **5개 챌린지 전부 ParrelSync 2인 검증 완료(2026-07-25)** — §11B.7 참고. 보드 원문: [`MStageNetworkBoard.md`](MStageNetworkBoard.md).

### 11B.0 축 (5칸 · 일방통행)

```
① Trigger → ② RoundStart(Seed) → ③ Generate → ④ Judge → ⑤ Resolve
                                                              │
                                          Complete → §11A ④Resolve(Clear)로 반환
                                          Fail     → §11A ④Resolve(Fail)로 반환
```

| 칸 | 불변식 (칸이 끝나면 참) | Writer (여기만 진실) | 재사용하는 기존 패턴 (발명 아님) |
|----|------------------------|----------------------|--------------------------------|
| ① Trigger | Host만 시작 판정 확정. Client의 로컬 트리거 호출은 무시된다(Host도 리모트 플레이어 Rigidbody를 직접 시뮬레이션하므로 자기 화면에서 트리거가 그대로 발생 — §9A) | 각 챌린지 매니저의 Host 가드(`IsClientOnly()` 등) | `StageStartGate.Update()` — `if (!IsServer) { Display(); return; }` |
| ② RoundStart | 라운드 시작마다 Host가 새 `int` 시드를 생성해 NV로 배포. 세션 시드(`NetworkSessionData.Seed`)와 별개 — 라운드 전용이라 타이밍 레이스 없음. **시드+스텝 인덱스+시작시간은 하나의 NV로 원자적 전달** (2026-07-20 버그 수정 — 별도 NV 2개로 나누면 도착 순서 레이스로 Host/Client가 다른 문제를 보는 증상 발생) | `StageNetworkState.ChallengeStart(seed)` / `ChallengeStepBegin(stepIndex)` — `_challengeStep : NetworkVariable<ChallengeStepState>` 하나 |
| ③ Generate | Host/Client 전부 동일 시드로 로컬 생성 코드(셔플·배치 등)를 재실행 → 결과 자체는 네트워크로 보내지 않음 | 각 챌린지 매니저 로컬 (읽기 전부, 쓰기 없음 — 시드만 진실) | `StagePressurePadSetup.ApplySeedAndColors()`(`Random.InitState(seed^salt)`) 패턴. OX는 `System.Random(seed)`로 `UnityEngine.Random` 전역 상태 오염 방지 |
| ④ Judge | 타이머 종료·정답/조건 판정은 **Host 레인에서만**. Client는 결과를 관찰만(연출용 ClientRpc) | 각 챌린지 매니저의 Host 전용 판정 메서드(예: `OXQuizManager.JudgeByPosition()`) | §11A ③ Progress "Host 레인 하나만" 규칙 그대로 |
| ⑤ Resolve | 데미지는 `NetworkDamageUtil`만 경유. 챌린지 전체 클리어/실패는 `StageObjective.Complete()/Fail()`로 §11A Progress에 반환 — 새 리로드/전환 경로 금지 | `NetworkDamageUtil.ApplyDamage`, 각 `*Objective.Complete()`(Host 가드) | `NetworkDamageUtil.ApplyDamage`, §11A ④→⑤ |

### 11B.1 Client → Host 입력 제출이 필요한 챌린지 (예: SequenceRing)

포지션 판정형(OX/GridColor/ColorTile)은 Host가 리모트 플레이어 위치를 직접 갖고 있어 별도 제출이 필요 없다. 반면 **키 입력형(SequenceRing)**은 어느 플레이어가 어떤 키를 눌렀는지 자체가 Host에 없는 정보이므로 별도 제출 경로가 필요하다:

```
Client: 자기 키 입력 감지 → SubmitStepServerRpc(color) / SubmitAnyKeyStepServerRpc()
Host  : TrySubmit()/TrySubmitAnyKey() 판정 (④ Judge, Host 레인) → 결과 ClientRpc 연출
```

새 메커니즘이 아니라 기존 **"Client → Host 한 방향 요청: ServerRpc, Host 검증"** 규칙(`multiplayer-ngo.mdc` Sync 절, Cheer 제출·발사체 히트 리포트와 동일 패턴)의 재적용이다. **코드 반영 완료(2026-07-22)** — `StageNetworkState`에 두 ServerRpc 신설, Host에서 `SequenceRingMinigame.Instance`를 통해 판정 메서드 호출.

**남은 시간 동기화 (SequenceRing 전용 추가):** SequenceRing의 남은 시간은 오답 페널티 등 이벤트 기반 변동이 있어 OX처럼 `ChallengeStepStartServerTime` 역산만으로는 재현할 수 없다. `SurviveTimeObjective`가 이미 쓰는 "Host가 직접 tick + 주기 ClientRpc 브로드캐스트" 패턴(`SyncSurvivalRemainingClientRpc`)을 그대로 재사용해 `StageNetworkState.SyncChallengeTimeClientRpc(float remaining)` + `OnChallengeTimeSync` 이벤트를 추가했다(0.1초 주기, §11B.2에 반영).

### 11B.2 공통 API (`StageNetworkState` 확장 — 챌린지 공용 슬롯)

씬당 챌린지는 한 번에 하나만 진행되므로, 아래 필드를 **모든 C 패턴 챌린지가 공유 슬롯으로 재사용**한다. 챌린지마다 새 `NetworkBehaviour`를 만들지 않는다(`architecture.mdc`: "Prefer extending existing systems over parallel new frameworks").

| 멤버 | 종류 | 역할 |
|------|------|------|
| `ChallengeStepState { seed, stepIndex, stepStartServerTime, owner }` | `NetworkVariable` (Server write, 1개로 통합) | ②RoundStart 원자적 배포. `owner`는 2026-07-28 추가 — §11B.9 참고 |
| `ChallengeSeed` / `ChallengeStepIndex` / `ChallengeStepStartServerTime` / `ChallengeOwner` | 읽기 프로퍼티 | ③Generate·④Judge 타이머 기준. `ChallengeOwner`는 §11B.9 소유자 가드 전용 |
| `IsChallengeCleared`(`_challengeCleared` NV) | `NetworkVariable<bool>` (Server write) | ⑤Resolve 클리어 연출 신호 |
| `ChallengeStart(seed, owner)` / `ChallengeStepBegin(stepIndex)` / `ChallengeCleared(bool)` | Host 전용 메서드 | Writer. `owner`는 호출한 챌린지 자신의 `ChallengeOwnerType` — 반드시 자기 타입을 넘겨야 함 |
| `OnChallengeStepChanged` / `OnChallengeClearedChanged` / `OnChallengeOutcome` | 이벤트 | 전 챌린지 매니저 공통 구독점(공유 슬롯이므로 핸들러 내부에서 `ChallengeOwner` 가드 필수 — §11B.9) |
| `NotifyChallengeOutcomeClientRpc(bool success)` | `[ClientRpc]` | ④Judge 결과 1회성 연출(Client만 재생 — Host는 로컬에서 직접 처리하므로 스킵) |
| `SubmitStepServerRpc(PlayerColorType color)` / `SubmitAnyKeyStepServerRpc()` | `[Rpc(SendTo.Server)]` | §11B.1 — Client 입력 제출 → Host가 `SequenceRingMinigame.Instance.TrySubmit()`/`TrySubmitAnyKey()` 호출 |
| `OnChallengeTimeSync` / `SyncChallengeTimeClientRpc(float remaining)` | 이벤트 / `[ClientRpc]` | §11B.1 — 이벤트 기반 변동(페널티)이 있어 ServerTime 역산이 불가능한 연속 타이머 전용(SequenceRing). Host가 직접 tick + 주기 브로드캐스트 |

### 11B.3 4개 챌린지 → 이 축 매핑

| 챌린지 | ①Trigger | ②RoundStart 시드로 대체할 것 | ④Judge | 상태 |
|--------|----------|------------------------------|--------|------|
| ~~OX Quiz~~ | ~~배리어 진입 트리거~~ | ~~`RegenerateQuestionOrder()`~~ | ~~`JudgeByPosition()`~~ | **제거됨 (2026-08)** — 플레이테스트 피드백("지루함")으로 삭제, `SideSplit`로 교체. 상세: [`MinigameDesign.md`](MinigameDesign.md) §0/§3 |
| **SideSplit** (좌/우 분기, `T.Stage4`는 좌/우/앞/뒤 4방향) | 배리어 진입 트리거 | `RegenerateRoundPlan()`(`System.Random(seed)`, 활성 방향(2 또는 4) 인원+색상 조건 — N방향 일반화) | `Judge()`(활성 zone 전체 물리 오버랩, 정확 인원+색상 일치) | **코드 작성 완료 — ParrelSync 2인 검증 대기.** `SideSplitChallenge`/`SideSplitZone`/`SideSplitObjective`/`SideSplitUI`. OX 축을 그대로 복제(설계: [`MinigameDesign.md`](MinigameDesign.md) §1/§2/§1.7) |
| ColorTile | 스케줄(시간 기반, 트리거 아님 — Host `Update()` 자체가 이미 단일 소스여야 함) | 스폰 포인트 셔플 | 타일 완료 체크 | **완료 — ParrelSync 2인 검증 통과(2026-07-22, `M.Stage3`)**. 동일 위치/색, 성공·실패 동시, 실패 시 벽 전진 동기화 확인 |
| GridBW | `Activate()` 호출 시점 | `PickRandomSafeTiles()`(라운드마다 새 시드) | `EvaluateRound()` | **완료 — ParrelSync 2인 검증 통과(2026-07-25, `M.Boss`/`M.Stage5`)**. stepIndex=라운드 번호, 데미지 버그(`ReceiveDamage` 직접 호출) 수정 포함. 동일 라운드 배치·판정·데미지 동기화, 라운드 반복 진행 확인 |
| GridColor | `Activate()` 호출 시점 | `PickRandomColorTiles()`(라운드마다 새 시드) | `EvaluateRound()` | **완료 — ParrelSync 2인 검증 통과(2026-07-25, `M.Stage5`)**. 데미지 경로는 이미 `NetworkDamageUtil.ApplyDamage`로 수정 완료(2026-07-19). 동일 라운드 배치·판정·데미지 동기화 확인 |
| SequenceRing | `StartMinigame()` 호출 시점 | `GenerateSteps()` | `TrySubmit()`/`TrySubmitAnyKey()` | **완료 — ParrelSync 2인 검증 통과(2026-07-25, `M.Stage4`/`M.Boss`)**. §11B.1 `SubmitStepServerRpc`/`SubmitAnyKeyStepServerRpc` 포함 — 어느 플레이어가 눌러도 양쪽에 동일 반영, 남은 시간(오답 페널티 포함) 동기화 확인 |

### 11B.4 금지 (평행 축 — 발견 즉시 삭제)

| 항목 | 이유 |
|---|---|
| 챌린지별로 새 `NetworkBehaviour`/새 NV 세트 발명 | `StageNetworkState._challengeStep` 공유 슬롯 재사용 — 씬당 챌린지 1개 동시 진행 전제 |
| 시드·스텝 인덱스·시작시간을 별도 NV로 분리 | 도착 순서 레이스 재발(2026-07-20에 이미 겪은 버그) — 반드시 한 NV(구조체)로 원자적 전달 |
| Client가 ④ Judge 판정을 독자 수행 | §11A "Host 레인 하나만" 위반. Client는 Host 판정 결과만 관찰 |
| 챌린지 데미지가 `Player.TakeDamage`/`ReceiveDamage` 직접 호출 | 온라인 no-op. `NetworkDamageUtil.ApplyDamage` 경유 필수 (GridColor에서 이미 1건 발견·수정) |
| 챌린지 Objective의 `Complete()`를 Host 가드 없이 호출 | §11A.2 계약 위반 — Client 독자 클리어 확정 금지 |

### 11B.5 증상 → 볼 칸 (진단 사다리)

| 증상 | 먼저 볼 칸 | 그다음 |
|------|-----------|--------|
| Host/Client가 다른 문제·다른 배치를 봄 | ② RoundStart (시드/인덱스 원자적 도착 여부) | ③ Generate 시드 소스 확인 |
| Client 화면에서만 먼저 클리어/오답으로 보임 | ④ Judge Host 레인 위반 | ⑤ Resolve 중복 판정 |
| 오답인데 데미지 없음(온라인) | ⑤ Resolve — `NetworkDamageUtil` 경유 여부 | ④ Judge 호출 여부 |
| 전원 클리어했는데 다음 Phase로 안 넘어감 | ⑤ Resolve `Complete()` Host 가드 | §11A ③Progress 연결 여부 |
| SequenceRing류에서 다른 사람이 누른 입력이 반영 안 됨 | §11B.1 ServerRpc 제출 누락 | ④ Judge `TrySubmit()` |
| 챌린지 A를 고치면 B가 깨지는 회귀(A→B→C→A 순환) | §11B.9 `ChallengeOwner` 가드 누락 여부 | ② RoundStart의 `ChallengeStart(seed, owner)` 호출이 자기 타입을 넘기는지 |

규칙: 한 칸씩 위로. 깨진 불변식이 설명되면 **정지**. 그 칸 Writer만 고침. 칸에 복구 if 추가 금지.

### 11B.6 검증 완료 (OX — ParrelSync 2인)

1. Trigger→클리어 1회: Host/Client 동일 문제 순서·동일 판정 확인
2. Client만의 데미지/셔플/`Complete()` 없음(`IsServer` 가드 누락 그레핑으로 확인)
3. 결과: **통과** — 이 절(§11B)로 승급. `MStageNetworkBoard.md` 포커스는 `M.Stage3` ColorTile로 이동(§11B.3 매핑표대로 동일 축 복제)

### 11B.7 검증 완료 — GridBW/GridColor/SequenceRing (ParrelSync 2인, 2026-07-25)

§11B.3 매핑표대로 OX 축을 그대로 복제한 나머지 3개 챌린지(ColorTile은 2026-07-22에 이미 통과)까지 ParrelSync 2인 검증이 전부 통과했다. **이로써 §9.1 패턴 C(챌린지 축) 대상 5개 챌린지(OX/ColorTile/GridBW/GridColor/SequenceRing) 전부 검증 완료** — §11B는 이 5개 실제 구현으로 뒷받침되는 SSOT로 확정된다.

- GridBW/GridColor: 동일 라운드 배치(안전 칸·색 타일), 동일 성공/실패 판정, 개인 데미지 동기화, 라운드 반복 진행(다음 라운드로 정상 이행) 확인
- SequenceRing: 위 항목 + (a) 어느 플레이어가 눌러도 다른 클라이언트에서 동일하게 스텝 진행, (b) 오답 페널티 포함 남은 시간 표시가 양쪽 화면에 거의 동시 반영(§11B.1/§11B.2 `SyncChallengeTimeClientRpc`) 확인
- 남은 이슈 없음 — §11B.4 금지 목록 위반 없음 확인됨

### 11B.8 Floor — Generate-only 변형 (§11B 축 재사용, 코드+검증 완료 2026-07-25)

`FloorManager`(`M.Stage1`~`M.Stage5`, `T.Stage5`)는 성공/실패 판정이 없는 "무한 반복 Generate"라 §11B.0의 ④Judge/⑤Resolve 없이 **①Trigger 없음 + ②RoundStart(Seed) + ③Generate만 반복**하는 축 #4의 축소 변형이다. 새 설계가 아니라 §11B.0 ②③칸만 재사용한 것 — 별도 항목 번호(축 #5 등)를 붙이지 않는다.

- `NetworkBehaviour`(자체 `NetworkObject`) → `MonoBehaviour` + `StageNetworkState`의 전용 NV 슬롯(`_floorRoll : NetworkVariable<FloorRollState>`, `_challengeStep`과 별도 슬롯)으로 전환. 기존 `SyncTilesClientRpc(byte[] states)`(타일 상태 배열 전체 매 전송) 폐기 → 시드 하나만 배포해 전 머신이 로컬로 동일 결과 재생성(§11B.0 ③Generate와 동일 원칙)
- `keepBWRatio`를 시드와 함께 NV에 실어보내 Client가 Phase 진행(`triggerTime`/`changeInterval`)을 독자 계산하지 않게 함(SequenceRing 시간 동기화와 같은 결론)
- **ParrelSync 2인 검증 통과(2026-07-25)**: Host/Client 화면에서 타일 롤 패턴(Black/White/Reveal 배치)과 Phase 전환(간격·비율 변화) 타이밍 동일 확인
- 상세 반영 내용: [`MStageNetworkBoard.md`](MStageNetworkBoard.md) "Floor 마이그레이션 반영 내용"

### 11B.9 `ChallengeOwner` 소유자 가드 — 공유 슬롯 교차 오염 버그 수정 (2026-07-28)

**증상:** 챌린지 하나(A)를 고치면 다른 챌린지(B)가 새로 깨지고, B를 고치면 C가 깨지는 식으로 계속 순환하는 회귀가 있었다.

**원인:** §11B.2의 `_challengeStep`은 씬당 여러 챌린지 종류가 공유하는 슬롯인데, "이 컴포넌트를 꺼라"(`_currentPhase` NV, `PhaseManager`)와 "이번 라운드 데이터"(`_challengeStep` NV)가 서로 다른 NetworkVariable이라 Client 도착 순서가 NGO에서 보장되지 않는다. `PhaseManager.EnterPhase()`가 `onPhaseEnter`(새 챌린지의 `Activate()`/`StartQuiz()` 호출 → `_challengeStep` 갱신)를 먼저, `SyncPhase()`(오브젝트 on/off용 `_currentPhase` 갱신)를 나중에 쓰기 때문에, Client에서 두 NV의 `OnValueChanged` 콜백이 역전되면 아직 `SetActive(false)`되지 않은 이전 챌린지가 새 챌린지의 `stepIndex`를 자기 것으로 오인해 반응할 수 있었다. 이게 A를 고쳐 활성화 타이밍이 미묘하게 바뀔 때마다 다른 챌린지가 새로 이 레이스에 걸리는 회귀의 실제 원인이었다.

**수정:** `ChallengeStepState`에 `owner : ChallengeOwnerType`(`None`/`OX`/`ColorTile`/`GridColor`/`GridBW`/`SequenceRing`/`DirectionalBarrier`) 필드를 추가. `ChallengeStart(seed, owner)` 호출 시 자기 자신의 타입을 실어보내고, `ChallengeStepBegin`/`ResetChallengeStep`은 기존 `owner` 값을 그대로 유지한다. 각 챌린지 매니저의 `HandleChallengeStepChanged`/`HandleChallengeClearedChanged`/`HandleChallengeOutcome` 핸들러 맨 앞에 `if (_netState.ChallengeOwner != <자기 타입>) return;` 가드를 추가해, 활성화 타이밍이 완벽히 맞지 않아도 "내 것이 아닌 이벤트"를 항상 안전하게 무시하게 만들었다.

- `_currentPhase`/`_challengeStep`을 하나로 합치는 안(§11B.4 "별도 NV로 분리 금지" 원칙과 유사한 방향)도 검토했으나, `_currentPhase`는 트랩 스케줄링(`PhaseStartServerTime` 등)과도 얽혀 있어 블라스트 반경이 크고, 슬롯 배타성 원칙(§11B.8 "왜 별도 슬롯인가")과도 상충 — owner 태그 추가가 가장 국소적이고 기존 원칙과 일치하는 수정.
- 적용 대상: OX/ColorTile/GridColor/GridBW/SequenceRing 5개 챌린지 + `DirectionalBarrierRound`(§11B.3에는 없으나 동일 공유 슬롯 사용 확인되어 함께 수정).
- 상세 반영 내용: [`MStageNetworkBoard.md`](MStageNetworkBoard.md).

---

## 12. 이탈 · 세션 종료

> **확정 정책:** 재접속·Late Join·호스트 마이그레이션 **전부 미지원**. 구현·제안하지 않음.

### 12.0 세션 이탈 규칙 (정식)

| 상황 | 동작 |
|------|------|
| **호스트 이탈** | **즉시 방 종료** → 전원 타이틀 |
| **클라이언트 이탈** | **즉시 방 종료** → 전원 타이틀 (Host와 동일) |
| **재접속** | **미지원** (유예·스냅샷·슬롯 복귀 없음) |
| **호스트 마이그레이션** | **없음** (Host 나가면 방 폭파) |
| **Late Join** | **없음** |
| **Kick (인게임)** | **기능 자체가 없음** (§6A.2). Host가 인게임 중 특정 Client를 강제로 내보내는 UI/API 없음 — 앞으로도 추가 안 함. 있는 건 **이탈**(연결 끊김/Quit)뿐이며 발생 시 §12 규칙대로 **방 종료** |
| **Kick (Tutorial 사전 게이트 구간)** | **기능 자체가 없음** (§6A.2, §6B.4 — 2026-08-17 확정, 구 로비 Kick도 함께 폐지). 이 구간의 이탈은 슬롯만 제거, 방 유지 |

### 12.1 구현 시 주의

- **Tutorial 사전 게이트 구간 이탈(§6B.4)** = 슬롯만 비움, 방 유지. **인게임 이탈/Kick(§12)** = 방 전체 종료. 섞지 말 것.
- 인게임 이탈 후 **남은 인원으로 스테이지 계속**·인원만 줄이는 리로드·재입장 UI **금지**.
- Host/`DisconnectManager` (인게임) → **세션 정리 → 전원 타이틀**.
- “60초 유예 / 스냅샷 / 3인 계속” 구 스펙 **되살리지 말 것**.

---

## 13. 체크포인트 · 스폰 (MVP)

- 각 스테이지/보스 씬에는 **별도 체크포인트 세이브 없음** (MVP).
- 스폰/리스폰 좌표 = **`PlayerSpawnManager.fixedSpawnPositions`** 고정 좌표 (§11 ② Spawn Writer 유일).
- `ColoredStartZone`은 리스폰 위치를 갖지 않는다 — `StageStartGate` 점유 판정 전용.
- 체크포인트는 아직 없음. 추가 시에도 리스폰 Writer는 `PlayerSpawnManager` 하나로 유지 (좌표 소스만 교체).

---

## 14. End.Demo

- 클리어 UI 씬 **`End.Demo`** (씬명 레거시 — 리네임 별도).
- `T.Boss` 클리어 후 진입. 멀티/솔로 공통.
- UI: 타이틀 복귀 버튼 → §8 타이틀 복귀 규칙.

---

## 15. 에디터 · 씬 작업 (확정)

| 작업 | 담당 |
|------|------|
| `GameSession`, `SceneFlowManager` → `Title` | **수동 (기획/에디터)** |
| `1.Lobby` 씬 삭제, `0.Title`→`Title`/`2.Tutorial`→`Tutorial` 리네임 | **수동 (기획/에디터, 2026-08-17 확정)** |
| `TutorialGatherZone` 구현 (§6B.3) | 구현 시 |
| 스테이지 씬 내 Player 프리팹 인스턴스 제거 | 구현 시 |
| Network Player Prefab 생성 + NetworkManager 등록 | 구현 시 |
| `End.Demo` 씬 (Build Settings 등록) | 구현 시 |
| `sceneSequence`에 `Tutorial`·M1–5·M.Boss·T1–5·T.Boss·`End.Demo` | `SceneFlowManager` |

---

## 16. 구현 순서 (권장)

### 16.1 네트워크 · 응원 · Steam → 정식 출시

> **현재 실행 체크리스트:** `ReleaseRoadmap.md` §5.  
> **Authority:** §9.0 확정 (**이동=Owner+CNT**, 발사체=B안). Phase 2 이동 Host화 **폐기**.  
> **목표:** **2026-09-01** 정식 (`ReleaseRoadmap.md`).

1. NGO + `UnityTransport` + Title `NetworkManager`
2. Tutorial 사전 게이트 구간 — 접속 스폰/색 자동배정/`TutorialGatherZone` 동기화 (§6B, 구 "로비 Ready/캐릭터/Start 동기화") — **실행용 체크리스트는 §6B.7**
3. Player Network Prefab + 존 스폰 + Owner 입력·카메라·**Owner 이동(CNT)**
4. **§9A Phase 1** — 데미지·함정 Host 파이프라인 (ParrelSync / Dev Build 2인)
5. **§9.0.1 발사체 B안** — Host Spawn+velocity / Client 비행 / Client 보고→Host 피격
6. **Must 동기화** (§9 표) — WindTrap Host 힘 포함
7. **ParrelSync ①**
8. **Development Build ②** — localhost **2인** (현재 잔여 게이트)
9. **응원** — CheerService, Dissonance, Vosk (`CheerAndTutorialDesign.md`)
10. **Steamworks (전부)** — P2P · Lobby · Depot/알파 · Invite (**출시 하드 블로커**, `ReleaseRoadmap.md` §4)
11. 애니 → SFX → 응원 확장 → Tutorial → 난이도(Coming Soon play test) → 출시 QA
12. **2026-09-01 정식 출시**
13. **텔레메트리** — [`TelemetryDesign.md`](TelemetryDesign.md) (**출시 후 OK**)
14. M/T 풀코스+보스 (`sceneSequence`) + `End.Demo` + UI 옵션

### 16.2 Post-Launch

- 관전(Spectator) — **후보**
- sit/dance 이모트
- (재접속·Late Join·호스트 마이그레이션은 **미지원 유지**)
- **컷씬: 안 넣음** (로드맵에 넣지 않음)

---

## 17. Post-Launch (참고)

- 관전(Spectator) 후보, 캐릭터 이모트 (sit, dance)
- **컷씬: 영구 제외**
- 재접속·호스트 마이그레이션·Late Join: **미지원** (§12)

---

## 18. FAQ (설계 중 합의)

**Q. 솔로 로비 씬이 따로 필요한가?**  
A. **아니오.** 로비 씬 자체가 없다(§6B). `Tutorial` Host 1인(`partySize=1`)과 동일 경로 — 접속 즉시 스폰, 게이트 즉시 통과.

**Q. 솔로 색상은 어디서 고르나?**  
A. 선택 UI 없음. `Tutorial` 진입 즉시 **자동 배정**(§6B.2) — 멀티와 동일 로직.

**Q. 다른 플레이어를 내 PC에서 조종하나?**  
A. **아니오.** Owner는 자기 캐릭터 **이동·입력·카메라·연출**. **HP·함정·피격 최종**은 Host (§9.0).

**Q. Host Authority면 이동도 Host?**  
A. **아니오.** 이동=**Owner+CNT 확정**. Host는 HP·함정·피격 등 판정.

**Q. Host 판정이면 Client 화면에 안 보이나?**  
A. **보인다.** Host가 판정한 **결과**를 동기화해 전원이 같은 상태를 봄.

**Q. 타이틀 복귀 시 멀티 연결은?**  
A. **`NetworkManager.Shutdown()`** 으로 해제 (TitleReturnFlow / NetworkManagerSetup 경유).

**Q. ParrelSync만 통과하면 정식 출시해도 되나?**  
A. **아니오.** **Steam P2P ④** (2인 Must + 4인 1회 권장) + 응원·보이스 + Ship Must 콘텐츠가 출시 게이트.

**Q. Dev Build ②만 통과하면 출시?**  
A. **아니오.** ②는 **중간 게이트**. 정식 = **Steam 원격** + 협동 + 응원 + `ReleaseRoadmap.md` Ship Must.

**Q. 개발 PC 2대뿐인데 4인 테스트?**  
A. 일상 = **Steam 2인** (`ReleaseRoadmap.md` §3.1). 출시 전 **4인 1회** — 친구 권장. 2인 통과 ≠ 4인 100% 보장.

**Q. 2인 OK면 4인도 OK?**  
A. **연결·Transport·응원 골격**은 2인에서 대부분 검증. **4인 전용** (3표 집계, 4보이스, 4Gate)은 4인 1회 필요.

**Q. discovery / 원격 IP로 테스트하나?**  
A. **안 함.** 실제 검증 가능한 건 **ParrelSync · Dev Build(같은 PC) 뿐** (§6A.3). 물리적으로 분리된 2PC 간 LAN 연결은 미지원·미검증. Steamworks 붙으면 그때부터 ④ Steam P2P.

**Q. Kick 기능은 어디에 있나?**  
A. **어디에도 없다.** 로비였을 때도 Tutorial 사전 게이트 구간이 된 지금도 Kick은 완전히 폐지됐다(§6A.2, §6B.4, 2026-08-17). 있는 건 자연 이탈뿐 — Tutorial 사전 구간 이탈은 슬롯만 제거(방 유지), 인게임 이탈은 방 종료(§12).

**Q. `1.Lobby` 씬 없이 Steam 초대는 어떻게 받나?**  
A. `Tutorial` 씬 내 **상시 HUD**(Invite 버튼, §6B.5 — **룸코드 없음**, §4.2)로 받는다. `TutorialGatherZone` 통과 전까지 수락 가능, 통과 후엔 무시된다. 아직 방을 안 만든 Title 화면에서의 수락은 기존과 동일하게 `TitleMenuController.OnSteamInviteAccepted`/`TryAutoJoinFromLaunchArgs`가 처리.

**Q. Steam에서도 룸코드로 참여하나?**  
A. **아니오 (2026-08-17 확정).** Steam(④) 경로는 오버레이 초대 수락 또는 초대 링크(`steam://joinlobby/...`)로만 조인한다 — 코드 입력 UI 자체가 없다. Deep Rock Galactic/Risk of Rain 2/Overcooked 2 등 같은 조건(사적 파티 전용, 공개 매칭 없음)의 Steam 코업 게임들도 전부 이 방식이다. 룸코드는 **로컬 개발(①②) 전용**으로만 남는다(§4.1).

**Q. 컷씬·관전·이모트를 출시 전에 넣나?**  
A. **컷씬: 안 넣음(영구).** 관전·이모트: **Post-Launch**. 재접속·호스트 마이그레이션·Late Join은 **미지원**(§12). Ship Must·순서는 `ReleaseRoadmap.md` §4 (텔레메트리는 출시 후 OK).

**Q. Steam 데모 / Playtest 페이지를 만드나?**  
A. **아니오.** 데모·Playtest 없음. **2026-09-01 정식 출시**만.
