# Steamworks 연동 · 다국어 — 확정 기준 (2026-08-04 전략적 결정 11개 확정 + 트랙 1~3 구현·스모크 테스트 완료)

> **상태: 전략적 결정 11개(§1~§11) 확정 완료 + 코드 구현(트랙 1 부트스트랩 / 트랙 2 Transport / 트랙 3 Lobby) 및 실사용 스모크 테스트(Host↔Client 접속, 인게임 진입, Invite Overlay) 전부 통과 (2026-08-04).**
> §4(연결 API 시그니처)는 실제 구현된 `StartHostSteam(string roomCode = "")` / `StartClientSteam(SteamId)`로 이미 확정 사용 중 — 문서 §4 텍스트만 아직 "미정"으로 남아있어 정리 필요(급하지 않음).
> **다음 작업은 맨 아래 "트랙 1~3 전체 완료" 절의 후속 작업 목록(다국어 구현 / 실제 Steam Depot 업로드) 참고.**
> 확정된 내용은 `NetworkDesign.md` §4.2(Steam P2P + Lobby) / 신규 로컬라이제이션 절로 승격 예정(아직 미승격).
>
> 배경: `ReleaseRoadmap.md` §4 순위 1 "Steamworks (전부)" — 출시 하드 블로커.
> 다국어(스팀 언어 감지 포함)는 같은 페이즈로 묶어 진행하기로 합의됨.

---

## 1. Steamworks SDK 라이브러리

| 옵션 | 설명 | 추천 |
|------|------|------|
| **Facepunch.Steamworks** | 최신 .NET 친화적 API. NGO용 커뮤니티 트랜스포트(`com.community.netcode.transport.facepunch`)가 이 라이브러리 기반 | ✅ 추천 |
| Steamworks.NET | 전통적으로 많이 쓰인 P/Invoke 래퍼 | — |

**추천 근거:** NGO + Steam 조합에서 커뮤니티 표준 트랜스포트가 Facepunch 기반이라 마찰이 적음. 동장르(co-op 서바이벌) Rust가 실전 사용 중.

**상태:** ✅ **확정 (2026-08-04) — Facepunch.Steamworks**

---

## 2. NGO 트랜스포트 패키지

| 항목 | 값 |
|------|-----|
| 패키지 | `com.community.netcode.transport.facepunch` (Unity 공식 `multiplayer-community-contributions` 저장소) |
| 설치 방식 | UPM git URL (`Packages/manifest.json`) |
| 내장 Facepunch.Steamworks 버전 | 2.3.2 (오래됐지만 P2P/Lobby 기본 기능엔 문제 없음) |
| 알려진 이슈 | Win64/Posix DLL 중복 타입으로 `CS0433` 컴파일 에러 가능 — Windows 전용 빌드이므로 Inspector에서 비-Win64 플랫폼 DLL 비활성화로 해결 (표준 워크어라운드) |
| 알려진 이슈 2 (실제 발견, 2026-08-04) | `Runtime/FacepunchTransport.cs` 파일 끝에 중복 `#endregion` 한 줄 — 업스트림(`Unity-Technologies/multiplayer-community-contributions`) main 브랜치 자체의 미해결 버그. 고치는 PR이 이미 2개 올라가 있으나 아직 미머지([#270](https://github.com/Unity-Technologies/multiplayer-community-contributions/pull/270), [#273](https://github.com/Unity-Technologies/multiplayer-community-contributions/pull/273)). `CS1028 Unexpected preprocessor directive`로 그 어셈블리 전체가 컴파일 안 되고, 그 결과 `FacepunchTransport` 컴포넌트가 Inspector "Add Component" 목록에 아예 안 뜬다. |

**확정 필요:** 이 패키지로 갈지, 아니면 최신 Facepunch.Steamworks(2.4.1/2.5.0)를 수동으로 넣고 트랜스포트를 직접 짤지.

**상태:** ✅ **확정 (2026-08-04) — 커뮤니티 패키지(`com.community.netcode.transport.facepunch`) 그대로 사용.** §1에서 Facepunch 선택에 따른 자동 귀결. DLL 충돌은 "결정 사항"이 아니라 구현 중 워크어라운드(Windows 전용 DLL만 남기기)로 처리 — 문제 생기면 그때 수동 업그레이드 재검토.

**실제 설치 완료 (2026-08-04):**
- Package Manager에 git URL(`https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.facepunch`)로 설치 완료.
- **위 "알려진 이슈 2" 워크어라운드:** `Library/PackageCache/com.community.netcode.transport.facepunch@<hash>/Runtime/FacepunchTransport.cs` 맨 끝의 중복 `#endregion` 라인 삭제(정상 코드는 `#region`/`#endregion` 3쌍, 버그 있는 배포본은 4번째 `#endregion`이 여분으로 붙어 있음) → 컴파일 정상화 확인(콘솔 `CS1028` 사라짐).
- **주의 — 임시 위치:** 위 수정은 `Library/PackageCache`(Unity가 관리하는 불변 캐시)에 직접 적용한 것이라, 패키지가 재-resolve되면(예: `Library` 폴더 삭제, 매니페스트 버전 변경) 버그가 다시 나타날 수 있음. **영구화하려면** Package Manager에서 이 패키지를 **"Embed"**(→ `Packages/com.community.netcode.transport.facepunch/`로 로컬 복사, git 추적 대상이 됨) 후 그 복사본에 동일한 2줄 삭제를 다시 적용할 것.
- `0.Title` 씬 `NetworkManager` GameObject에 `FacepunchTransport` 컴포넌트 부착 완료.

**스모크 테스트 — Steam Host 성공 (2026-08-04):** `NetworkManagerSetup.StartHostSteam()` 실행 → `SteamManager.EnsureInitialized()` 성공(SteamId `76561198678350233`) → `NetworkManager.StartHost()` 성공. 이 과정에서 콘솔에 `[FacepunchTransport] - Caught an exeption ... Calling SteamClient.Init but is already initialized` 에러 로그가 함께 뜨는데, **이건 실패가 아니라 무해한 노이즈**다 — `FacepunchTransport.Initialize()`가 자체적으로도 `SteamClient.Init`을 한 번 더 시도하다 "이미 초기화됨"에 막혀 내부 `catch`에서 `Debug.LogError`만 찍는 것(`FacepunchTransport.cs` 자체 코드 — 우리 코드 문제 아님). `SteamManager`가 먼저 초기화해두는 순서(§5 취지: Steam 경로에서만, 한 곳에서 초기화)와 겹쳐서 나는 구조적 로그이며, Host 시작 자체는 정상 완료됨. **조치 불필요** — 신경 쓰지 않아도 됨(제거하려면 `FacepunchTransport` 컴포넌트 자신의 `Steam App Id` Inspector 필드도 `5029890`으로 맞춰주면 로그 문구가 좀 더 일관되지만, 에러 자체는 어차피 뜬다 — 패키지 쪽 구조라 완전히 없앨 수 없음).

---

## 3. Steam Lobby 정책 (타입 · 코드 · 초대)

| 결정 항목 | 옵션 | 추천 |
|-----------|------|------|
| **Lobby 타입** | Public(공개 매칭) / FriendsOnly / **Private**(코드/초대로만 입장) / Invisible | **Private** — 공개 매칭 기능 요구사항 없음, 기존 "룸코드" 철학과 일치 |
| **입장 방식** | (a) 코드 공유 → `SteamMatchmaking.JoinLobbyAsync(lobbyId)` (b) Steam 친구 초대 오버레이 (c) 둘 다 | **둘 다** — 기존 `NetworkDesign.md` §4.2 "Join: Lobby 코드 / 친구 초대" 그대로 |
| **룸코드 형태** | Steam LobbyId(64bit)를 그대로 노출 vs 마스킹(`7**1` 형태, 이미 문서에 명시) | 마스킹 유지 |
| **초대 메커니즘** | Lobby Invite Overlay(`ActivateGameOverlayInviteDialog`) vs Rich Presence "Join Game" 버튼 | **Lobby Invite Overlay만** (MVP) — Rich Presence Join은 별도 콜백 배선 필요, 범위 확대 |

**상태:** ✅ **확정 (2026-08-04)**
- Lobby 타입: **Private**
- 초대: **Lobby Invite Overlay만** (Rich Presence는 Post-Launch 후보)
- 룸코드: **LobbyId를 마스킹해서 그대로 표시** (`7**1` 형태, 별도 6자리 코드 매핑 테이블 만들지 않음 — 구현 단순화)

**주의(§8과 연결):** Facepunch Lobby는 Owner 이탈 시 다음 멤버에게 Owner를 자동 이전하려는 동작이 있음 — 이는 Lobby 타입과 무관하게 항상 발생. `NetworkDesign.md` §12(호스트 나가면 방 폭파, 마이그레이션 없음)와 충돌하므로 §8에서 "Steam Lobby 자동 Owner 이전 기능 무시, 기존 `DisconnectManager` 문 하나만 유지"로 확정 예정.

---

## 4. 연결 시그니처 — SteamId 기반 API

- 기존 `NetworkManagerSetup.StartClient(string address)`는 **IP 문자열** 기반 (①② 로컬 전용, 유지).
- Steam 경로는 **SteamId(64bit)** 기반으로 연결 대상을 지정해야 함 — 새 오버로드/분기가 필요.
- **확정 필요:** 새 메서드명을 뭐로 할지(`StartClientSteam(SteamId)` 등), 기존 `StartClient`와 이름 충돌 안 나게 구조 어떻게 나눌지.

**상태:** 미정 (트랙 2 구현 시 같이 확정 예정).

---

## 5. 로컬 개발 환경 — Steam 클라이언트 필요 여부

| 상황 | 확정 필요 사항 |
|------|----------------|
| ParrelSync (①) | Steam 클라이언트 안 켜져 있어도 되는지 — `steam_appid.txt`로 `SteamClient.Init` 우회 가능한지, 아니면 로컬 모드는 Steam 초기화 자체를 스킵할지 |
| Dev Build (②) | 위와 동일 |

**추천:** 로컬 모드(①②)는 `SteamManager` 초기화 자체를 건너뛰는 플래그로 처리 — Steam 클라이언트 유무와 무관하게 항상 되게. (주의: 이건 "오프라인 모드"가 아니라 "개발자 로컬 테스트 편의" — `architecture.mdc`의 온라인 전용 락과 별개 개념. 문서에 이 구분을 명시해야 향후 혼동 없음.)

**상태:** ✅ **확정 (2026-08-04) — A+B 조합.**
- 로컬(①②): `SteamManager` 초기화 자체를 스킵 (Steam 클라이언트 유무 무관하게 항상 동작)
- `steam_appid.txt`는 프로젝트 루트에 안전망으로 상시 배치 (혹시 로컬 빌드에서 Steamworks API를 만질 필요가 생겨도 대비됨)
- **오프라인 모드 아님** — `architecture.mdc`의 온라인 전용 락(플레이 경로)과는 별개 층위(개발자 로컬 테스트 편의)임을 명시

---

## 6. App ID / Depot / Branch 구조

| 항목 | 확정 필요 |
|------|-----------|
| App ID | 이미 발급됨 (사용자 확인) — **`5029890`**. `steam_appid.txt`(프로젝트 루트) + `NetworkManager`(`SteamManager`) Inspector에 입력 완료 (2026-08-04) |
| Depot 개수 | 단일(Windows만) vs 복수 — 데모 없음 확정이므로 단일 권장 |
| Branch | `default`(public) 외에 QA용 `beta` 브랜치를 Coming Soon 기간에 쓸지 |
| 언어별 Depot 분리 | 텍스트만 다국어면 불필요(추천), 음성/폰트 에셋이 언어별로 무거워지면 재검토 |

**상태:** ✅ **확정 (2026-08-04) — 단일 Depot(Windows) + `default` 브랜치만.** 필요해지면(핫픽스 검증 등) `beta` 브랜치는 그때 추가.

---

## 7. Steam 부가 기능 범위 (이번 페이즈에 넣을지)

`ReleaseRoadmap.md`엔 "Steamworks 전부 = P2P·Lobby·Depot/알파·Invite"라고만 명시돼 있음. 아래는 로드맵에 없는 항목이라 **명시적으로 범위 밖으로 배제할지 확인 필요**:

| 항목 | 추천 |
|------|------|
| Achievements | 이번 범위 밖 (Post-Launch 후보) |
| Steam Cloud Save | 이번 범위 밖 |
| Rich Presence | 이번 범위 밖 (§3의 Invite Overlay만 사용) |
| Steam Stats/Leaderboard | 이번 범위 밖 |

**상태:** ✅ **확정 (2026-08-04) — Achievements/Cloud Save/Rich Presence/Stats 4개 전부 이번 범위 밖.** Post-Launch 후보로 이관.

---

## 8. Steam Lobby ↔ 기존 §12(이탈·세션 종료) 정합성

- `NetworkDesign.md` §12: 누구든 이탈 → 즉시 방 종료, 재접속/Late Join/호스트 마이그레이션 없음 (**변경 없음, 그대로 유지**).
- **확정 필요:** Steam Lobby 자체의 Owner 이탈 시 자동 동작(Facepunch가 Lobby를 자동 파괴/Owner 이전하려 할 수 있음)을 **끄거나 무시**하고, 기존 `DisconnectManager`/`TitleReturnFlow` 문(§6A.1)만 단일 경로로 유지할지 확인.

**상태:** ✅ **확정 (2026-08-04) — Steam Lobby 자동 Owner 이전 기능 무시, 기존 `DisconnectManager`→`TitleReturnFlow` 문(§6A.1) 하나만 유지.** Lobby 객체는 Host 이탈 시 파괴, 재사용 안 함. `NetworkDesign.md` §12·`multiplayer-ngo.mdc` 재접속/마이그레이션 미지원 정책과 일치.

---

## 9. 다국어 — 언어 소스

(이전 대화 요약 — 재확인용)

| 방법 | 상태 |
|------|------|
| `SteamApps.GetCurrentGameLanguage()` (정식) | ✅ 확정 |
| `Application.systemLanguage` (로컬 폴백) | ✅ 확정 |
| Steam 런치 옵션 `-language` (파트너 백엔드 설정) | 사용 안 함 (API 방식으로 대체) |

**상태:** ✅ **확정 (2026-08-04) — §5(로컬 Steam 초기화 스킵) 확정의 자연스러운 귀결.** 정식 빌드(Steam 초기화 성공)는 API, 로컬 빌드는 `systemLanguage` 자동 폴백.

---

## 10. 다국어 — 지원 언어 목록

| 구분 | 언어 |
|------|------|
| 코어 12개 (제안) | 영어, 한국어, 일본어, 간체 중국어, 번체 중국어, 러시아어, 독일어, 프랑스어, 스페인어(스페인), 스페인어(라틴아메리카), 포르투갈어(브라질), 폴란드어 |
| 확장 후보 +4 (보류) | 튀르키예어, 이탈리아어, 태국어, 우크라이나어 |

**상태:** ✅ **확정 (2026-08-04) — 코어 12개로 시작.** 기술 구조(String Table)는 언어 개수에 무관하게 동일하므로, 번역 리소스가 확보되면 +4(또는 그 이상)를 언제든 추가 가능. 지금 12개로 묶는 건 번역 작업량·9/1 일정 리스크 관리 목적.

---

## 11. 다국어 — 구현 방식

- **확정 완료:** Unity 공식 Localization 패키지(`com.unity.localization`, 이미 프로젝트에 설치돼 있음) 사용.
- **상태:** ✅ **확정 (2026-08-04) — 최종 목표 범위 = 전체 Ship Must UI**(타이틀·로비·HP·카운트다운·응원 HUD·채팅·옵션·`DialogueUI`, `ReleaseRoadmap.md` §2.1 목록). **구현 순서는 `DialogueUI`부터** — 기술 패턴(String Table + 키 기반 텍스트) 검증 후 나머지 UI에 동일 패턴 복제. §11B "OX에서 먼저 잠그고 복제" 원칙과 동일.

---

## 요약 — 확정 결과 (2026-08-04)

- [x] §1 SDK: **Facepunch.Steamworks**
- [x] §2 트랜스포트: **`com.community.netcode.transport.facepunch`** (Windows 전용 DLL 워크어라운드 적용)
- [x] §3 Lobby: **Private** · 초대 **Overlay만** · 룸코드 **LobbyId 마스킹**
- [~] §4 연결 API: 방향 확정(로컬 IP / Steam SteamId 별도 오버로드), 세부 이름은 구현 중 확정
- [x] §5 로컬 개발: **SteamManager 초기화 스킵 + `steam_appid.txt` 안전망**
- [x] §6 Depot/Branch: **단일 Depot + `default` 브랜치만**
- [x] §7 부가 기능: **Achievements/Cloud/Rich Presence/Stats 전부 범위 밖**
- [x] §8 Lobby↔이탈 정합성: **Steam Lobby 자동 Owner 이전 무시, 기존 `DisconnectManager`→`TitleReturnFlow` 문만 유지**
- [x] §9 언어 소스: **Steam API(정식) + `systemLanguage`(로컬 폴백)**
- [x] §10 언어 목록: **코어 12개**로 시작 (영/한/일/중간체/중번체/러/독/불/스페인·스페인/스페인·라틴/포르투갈·브라질/폴란드)
- [x] §11 로컬라이제이션 범위: **최종 목표 = 전체 Ship Must UI**, 구현은 `DialogueUI` 파일럿부터

**다음 단계:**
- ✅ **트랙 1(Steam 부트스트랩) + 트랙 2(Transport 이중화) 코드·설치 완료 (2026-08-04)** — `SteamManager.cs`, `NetworkManagerSetup.StartHostSteam/StartClientSteam`, 패키지 설치, App ID(`5029890`) 적용, `FacepunchTransport` 부착, 위 §2 워크어라운드 적용까지 반영됨.
- ✅ **Steam Host 스모크 테스트 통과 (2026-08-04)** — `NetworkManagerSetup.StartHostSteam()` 실행 → `SteamManager` 초기화 성공(SteamId `76561198678350233`) → `NetworkManager.StartHost()` 성공 확인. (`FacepunchTransport`의 중복 `SteamClient.Init` 에러 로그는 무해 — 위 §2 참고.)
- ✅ **트랙 3: Steam Lobby 구현 완료 (2026-08-04)** — `SteamLobbyManager.cs`(신규) + `NetworkManagerSetup`/`TitleMenuController`/`LobbyMenuController` 연동. Private Lobby 생성/참여, LobbyId 마스킹 표시(`LobbyNetworkManager.SharedRoomCode` 기존 인프라 재사용), Invite Overlay 버튼 연결, §8(Owner 자동 이전 콜백 무시 + `Shutdown()`에 `Lobby.Leave()` 연결) 전부 반영.
- ✅ **Host → Client 접속 실사용 스모크 테스트 통과 (2026-08-04, Build)** — 임시로 App Id를 `480`(Spacewar, 테스트용 공용 앱)으로 전환해 실행. `SteamManager` 초기화 → `SteamLobbyManager.CreateLobbyAsync()` → `StartHostSteam()` → 2번째 계정이 LobbyId 코드 입력으로 `JoinLobbyAsync` → `StartClientSteam()` 접속 → 인게임(`T.Stage4`) 스폰까지 전 구간 확인. Host 종료 시 `SteamLobbyManager.LeaveCurrentLobby()` → `NetworkManagerSetup.Shutdown()`까지 로그로 확인(`Player.log`).
  - **주의: 테스트용으로 App Id `480`을 세 곳(steam_appid.txt, `SteamManager.appId`, `FacepunchTransport.Steam App Id`)에 임시로 넣어둔 상태.** 실사용 빌드 전 반드시 `5029890`으로 원복할 것 (§6 App ID 참고).
- ✅ **Invite Overlay 동작 확인 (2026-08-04, 사용자 확인)** — 최초 시도에서 오버레이가 안 열렸던 원인은 탐색기에서 `.exe`를 직접 실행했기 때문(§ 위 설명: Steam은 그래픽 디바이스 초기화 전에 프로세스에 오버레이를 주입해야 하는데, 직접 실행 시 그 타이밍을 못 잡음). 사용자가 재시도하여 정상 동작 확인 완료 — Non-Steam 게임 등록 같은 별도 절차 없이 통과.
- ✅ **App Id 원복 완료 (2026-08-04)** — 테스트용으로 임시 사용했던 `480`(Spacewar)을 실사용 값 `5029890`으로 3곳 모두 원복: `steam_appid.txt`(에이전트 작업), `SteamManager.appId` Inspector(사용자 작업), `FacepunchTransport.Steam App Id` Inspector(사용자 작업).

## 트랙 1~3 전체 완료 (2026-08-04)

Steam 부트스트랩(트랙 1) · Transport 이중화(트랙 2) · Steam Lobby(트랙 3) 코드 구현과 실사용 스모크 테스트(Host 생성 → Client 접속 → 인게임 진입 → Host 종료 → Invite Overlay)가 전부 통과했다. §1~§11 확정 사항 중 구현이 필요했던 항목은 모두 반영 완료 상태.

**다음 에이전트가 시작할 수 있는 후속 작업 (우선순위 순):**

1. **다국어(Localization) 구현** — §9~11 확정 사항 기준. `com.unity.localization` 패키지(이미 설치돼 있음)로 String Table + 키 기반 텍스트 패턴 구축. **`DialogueUI`부터 파일럿** 진행 후 나머지 Ship Must UI(타이틀·로비·HP·카운트다운·응원 HUD·채팅·옵션)에 동일 패턴 복제. 코어 12개 언어(§10) 대상.
2. **실제 Steam Depot 업로드 준비** — 지금까지는 로컬 P2P/Lobby 코드 검증만 했고, 실제 Steam 백엔드(SteamPipe/ContentBuilder)로 빌드를 업로드해본 적은 없음. App Admin에서 단일 Depot(Windows, §6 확정) 설정 + `steamcmd`/ContentBuilder 스크립트(VDF) 작성 + 실제 업로드 1회 테스트가 필요. 이건 Steamworks 파트너 사이트 관리자 권한이 필요한 작업이라 사용자와 함께 진행해야 함.
3. **§4 연결 API 세부 이름 문서 반영** — 코드 상 이미 `StartHostSteam(string roomCode = "")`/`StartClientSteam(SteamId)`로 확정되어 쓰이고 있음. 이 문서 §4 "미정" 상태를 실제 시그니처로 업데이트할 것(사소한 문서 정리, 급하지 않음).

> **코드 참고 파일:** `Assets/Scripts/Network/SteamManager.cs`(Steam 부트스트랩), `Assets/Scripts/Network/SteamLobbyManager.cs`(Lobby 생성/참여/마스킹/§8 Owner 이전 무시/Invite Overlay), `Assets/Scripts/Network/NetworkManagerSetup.cs`(`StartHostSteam`/`StartClientSteam`/`IsSteamPath`/`Shutdown`), `Assets/Scripts/UI/TitleMenuController.cs`(`UseLocalNetworkPath` 로컬·Steam 경로 분기, 초대 수락 자동 참여), `Assets/Scripts/UI/LobbyMenuController.cs`(`OnClickSteamInvite`/`RefreshRoomCode` 마스킹 분기).
