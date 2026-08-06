# Steamworks 연동 · 다국어 — 확정 기준 (2026-08-04 전략적 결정 11개 확정 + 트랙 1~3 구현·스모크 테스트 완료, 2026-08-05 트랙 4 다국어 파일럿 완료, 2026-08-06/07 트랙 5 Depot 실사용 스모크 테스트 진행 중 — 버그 4건 발견·진단)

> **상태: 전략적 결정 11개(§1~§11) 확정 완료 + 코드 구현(트랙 1 부트스트랩 / 트랙 2 Transport / 트랙 3 Lobby) 및 실사용 스모크 테스트(Host↔Client 접속, 인게임 진입, Invite Overlay) 전부 통과 (2026-08-04).**
> **트랙 4(다국어) — `DialogueUI` 파일럿 완료 (2026-08-05) + 전체 대사/OX퀴즈 번역 완료 (2026-08-06):** String Table + `LocalizeStringEvent` 패턴 검증 완료, `Dialogue` 테이블 실번역 11개 언어 입력 완료(사용자), OX퀴즈용 `LocalizedString` 코드 전환 + 번역 완료(테이블 미생성). 자세한 내용은 아래 "트랙 4" 절 참고.
> **⭐ 트랙 5(실 App ID Depot 2인 스모크 테스트) — 진행 중, 다음 에이전트가 이어받을 것 (2026-08-06/07):** 실제 App ID(`5029890`)로 빌드 업로드·Set Live·테스터 계정 초대까지 완료, 실사용 2인 테스트 중 **버그 4건(A~D) 발견·진단 완료** — **이슈 A는 원인·수정안 확정, 사용자 OK만 받으면 바로 구현 가능.** 자세한 내용·재현 절차·Cause Site는 맨 아래 "트랙 5" 절 참고 — **다음 에이전트는 여기부터 시작.**
> §4(연결 API 시그니처)는 실제 구현된 `StartHostSteam(string roomCode = "")` / `StartClientSteam(SteamId)`로 이미 확정 사용 중 — 문서 §4 텍스트만 아직 "미정"으로 남아있어 정리 필요(급하지 않음).
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
| `SteamApps.GameLanguage` (정식 — 문서 초안엔 `GetCurrentGameLanguage()`로 잘못 적혀 있었으나, 실제 API 리플렉션 확인 결과 이 이름이 맞음. `GameLocalizationBootstrap.cs`에 이 이름으로 이미 구현됨) | ✅ 확정 |
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

1. ✅ (완료) ~~다국어(Localization) 구현~~ — 트랙 4에서 전체 대사/OX퀴즈 번역 완료 (2026-08-06).
2. ✅ (완료) ~~실제 Steam Depot 업로드 준비~~ — 실 App ID(`5029890`)로 업로드·Set Live·테스터 초대까지 완료 (2026-08-06/07). **다만 그 실사용 스모크 테스트에서 버그 4건 발견 — 아래 "트랙 5" 절 참고, 다음 에이전트가 이어서 진단·수정할 것.**
3. **§4 연결 API 세부 이름 문서 반영** — 코드 상 이미 `StartHostSteam(string roomCode = "")`/`StartClientSteam(SteamId)`로 확정되어 쓰이고 있음. 이 문서 §4 "미정" 상태를 실제 시그니처로 업데이트할 것(사소한 문서 정리, 급하지 않음).

> **코드 참고 파일 (트랙 1~3):** `Assets/Scripts/Network/SteamManager.cs`(Steam 부트스트랩), `Assets/Scripts/Network/SteamLobbyManager.cs`(Lobby 생성/참여/마스킹/§8 Owner 이전 무시/Invite Overlay), `Assets/Scripts/Network/NetworkManagerSetup.cs`(`StartHostSteam`/`StartClientSteam`/`IsSteamPath`/`Shutdown`/`UseLocalNetworkPath`), `Assets/Scripts/UI/TitleMenuController.cs`(`UseLocalNetworkPath` 참조로 로컬·Steam 경로 분기, 초대 수락 자동 참여), `Assets/Scripts/UI/LobbyMenuController.cs`(`OnClickSteamInvite`/`RefreshRoomCode` 마스킹 분기).

---

## 트랙 4: 다국어(Localization) 파일럿 — DialogueUI (2026-08-05)

**목표:** §9~11 확정 사항 기준으로 `com.unity.localization`(String Table + 키 기반 텍스트) 패턴을 `DialogueUI` 하나에 먼저 검증. 검증되면 나머지 Ship Must UI에 동일 패턴 복제.

### 완료된 작업

1. **공용 경로 분기 정리** — `NetworkManagerSetup`에 `public static bool UseLocalNetworkPath => Application.isEditor || Debug.isDebugBuild;` 신설. `TitleMenuController`의 중복 private 프로퍼티 제거하고 이걸 참조하도록 변경. (Steam/로컬 분기 로직을 한 곳으로 통합 — 로컬라이제이션 부트스트랩도 이 플래그로 Steam API 사용 여부를 결정함.)
2. **`GameLocalizationBootstrap.cs` 신규 작성** (`Assets/Scripts/Localization/`) — 씬 최초 진입 시 1회, `LocalizationSettings.InitializationOperation` 대기 후:
   - 로컬 경로(`UseLocalNetworkPath == true`): `Application.systemLanguage` → Locale 코드 매핑 후 적용.
   - 정식 경로: `SteamApps.GameLanguage`(§9 참고 — API명 정정됨) → Locale 코드 매핑, 실패 시 `systemLanguage`로 폴백.
   - 매핑 실패/Locale 없음 시 최종적으로 `en`으로 폴백.
   - **미검증 항목**: 지금까지는 에디터(`systemLanguage` 경로)에서만 테스트함. 실제 Steam 초기화된 빌드에서 `SteamApps.GameLanguage` 분기가 정상 동작하는지는 아직 스모크 테스트 안 됨 — 다음 에이전트가 Steam 빌드로 한번 확인할 것.
3. **Editor 설정 (사용자 작업)** — Project Settings > Localization에 로케일 13개(en 포함, §10 코어 12개 + en) 전부 등록 완료. `es-419`(Spanish Latin America)는 표준 피커에 안 떠서 "Add Custom Locale"로 직접 추가.
4. **String Table Collection `Dialogue` 생성 + 파일럿 4줄 연결** — `M.Stage1` 씬 `Dialogue_Panel` 하위 `Text (TMP) (1)~(4)`(사용자가 미리 만들어 둔 키 `M.Stage1.Intro.Line1~4`, `en` 값은 이미 채워져 있었음) 각각에 대해:
   - `LocalizeStringEvent.SetTable("Dialogue")` / `SetEntry(key)`로 String Reference 연결.
   - `OnUpdateString(string)` 이벤트 → 해당 오브젝트 자신의 `TextMeshProUGUI.text` 세터를 Dynamic String 방식으로 바인딩.
   - (에디터 UI에서 "TMP_Text → text"가 안 보이는 문제로 막혀서, 사용자 명시적 요청으로 이 1건은 MCP `execute_code`로 직접 배선함 — 리플렉션으로 `PropertyInfo.GetSetMethod()` + `Delegate.CreateDelegate` + `UnityEventTools.AddPersistentListener`. 실수로 중복 키(`M.Stage1.Dialogue.Line1~4`)를 새로 만들었다가, 기존 `Intro.Line1~4` 키에 이미 en 텍스트가 있는 걸 뒤늦게 발견하고 정정 + 중복 키 삭제함.)
5. **Locale Fallback 설정 (사용자 작업)** — `en`을 제외한 12개 Locale 각각에 `Fallback Locale` 메타데이터 = `en` 추가.
6. **⚠️ 함정 발견 — "Use Fallback" 체크박스가 2개 있음** — Project Settings > Localization 화면에 `Asset Database`용 `Use Fallback`과 `String Database`용 `Use Fallback`이 서로 다른 섹션에 각각 존재. 텍스트(String Table) 폴백에는 **`String Database` 쪽**을 켜야 하는데, 모양이 똑같아서 처음엔 `Asset Database` 쪽만 켜고 한참 헤맴(`LocalizationSettings.StringDatabase.UseFallback`이 계속 `false`로 남아있었음). 프로젝트의 `LocalizationSettings` 에셋 실제 파일명은 `Assets/StringTableCollection.asset`(이름이 헷갈리니 참고).
7. **스모크 테스트 통과 (2026-08-05, 사용자 확인)** — 시스템 언어 `ko`(한국어) 환경에서 Play 모드 진입 → `ko` 테이블엔 아직 값이 없지만 `en`으로 폴백되어 대사 4줄이 정상 텍스트로 표시됨 (이전엔 `"No translation found for '...' in Dialogue"` 메시지가 그대로 UI에 노출되던 상태였음).

### 후속 진행 (2026-08-06) — 전체 스테이지 대사 + OX퀴즈 번역

1. **`StageDialogueLines.md`(SSOT) 기준 M/T 전체 스테이지 대사 11개 언어 번역 완료** → `Assets/Docs/StageDialogueTranslations.md` 작성(총 29줄, ko 제외 11개 언어 + 제안 키 네이밍 `M.StageX.LineY` / `T.StageX.LineY`). **사용자가 `Dialogue` String Table에 값 입력까지 완료.**
2. **`M.Stage4.Stage1.Line1`의 "Space" 키 강조 확정** — 이미지 삽입 대신 TMP Rich Text로 처리. 최초 파란색(`#3B82F6`) → 사용자 피드백으로 어색하다는 의견 → 빨강(오답/경고 색과 충돌) 대신 **`<b><color=#FFC400>...</color></b>`(골드)**로 최종 확정. *(주의: 이 강조가 실제로 보이려면 해당 씬의 TMP 오브젝트가 `LocalizeStringEvent`로 String Table에 연결되어 있어야 함 — 현재 `M.Stage4` 씬은 아직 하드코딩된 영어 텍스트라 미연결 상태, 아래 "남은 작업" 참고.)*
3. **`OXQuizManager.cs` 코드 변경 완료** — `OXQuestion.questionText`/`explanationText`를 `string` → `LocalizedString`으로 전환(`[TextArea]` 제거), `OnQuestionReady`/`OnAnswerRevealed`에서 `.GetLocalizedString()` 호출로 변경. `DialogueUI` 파일럿과 동일한 String Table 참조 패턴.
4. **OX퀴즈 문항/해설 11개 언어 번역 완료** → `Assets/Docs/OXQuizTranslations.md` 작성 — `M.Stage2`(12문항) + `T.Stage4`(13문항), 키 네이밍 `OXQuiz.StageX.QY.Question` / `...Explanation`. **`OXQuiz` String Table Collection 자체는 아직 미생성 — 번역 텍스트만 준비된 상태, 테이블 생성·입력은 남은 작업.**

### 남은 작업 (다음 에이전트 / 다음 작업)

- **`OXQuiz` String Table Collection 신규 생성 + 50키 값 입력** (사용자 작업) — `OXQuizTranslations.md` 체크리스트 참고. 생성 후 `M.Stage2`/`T.Stage4`의 `OXQuizManager` 컴포넌트 Inspector에서 각 `LocalizedString` 필드를 Table+Entry로 재연결해야 함(문자열 직접 입력 아님).
- **⭐ `LocalizeStringEvent` 연결 — 다음 작업, MCP로 진행 예정** — 사용자가 Cursor 무료 grok 에이전트로 MCP를 통해 직접 연결할 계획. **`M.Stage4`부터 먼저 연결해 정상 동작(골드 강조 포함) 확인 후 나머지 스테이지로 확장.** 연결 방식은 위 트랙4 항목4의 `M.Stage1` 파일럿과 동일: `SetTable("Dialogue")`/`SetEntry(key)`로 String Reference 연결 + `OnUpdateString(string)` → 해당 오브젝트 `TextMeshProUGUI.text` Dynamic String 바인딩. (Inspector에서 "TMP_Text → text"가 안 보이는 문제가 있었던 전례가 있음 — 그때는 MCP `execute_code`로 리플렉션 배선(`PropertyInfo.GetSetMethod()` + `Delegate.CreateDelegate` + `UnityEventTools.AddPersistentListener`)으로 해결함. 이번에도 같은 방식이 필요할 수 있음.)
- **Bossdown 대화 배선 (M.Boss / T.Boss)** — 현재 `PhaseManager.onAllPhasesComplete`가 `SceneFlowRelay.LoadNextScene`에 직결되어 대화 없이 바로 씬 전환됨. 순서: 모든 phase clear → (신규) `Dialogue_Panel` + 2번째 `PhaseDialogueGate`의 `Begin()` 호출 → 전원 스페이스로 완료 시 `OnAllReady` → `SceneFlowRelay.LoadNextScene`. 미구현.
- **`M.Stage1` 구 파일럿 키(`Intro.Line1~4`) 정리** — 실제 스테이지 대사 키(`M.Stage1.LineY`)와 이름이 겹치지 않는지 확인하고, 기술 검증용으로만 쓰인 거면 정리/삭제.
- **미사용 Portuguese(pt) Locale + 빈 테이블 정리** — 현재는 매핑되지 않아 런타임 문제는 없으나, 정리 권장.
- **Steam 빌드에서 `SteamApps.GameLanguage` 분기 스모크 테스트** — 트랙4 항목2 "미검증 항목" 그대로 유지, 아직 확인 안 됨.

> **코드 참고 파일 (트랙 4):** `Assets/Scripts/Localization/GameLocalizationBootstrap.cs`(부트스트랩), `Assets/Scripts/Network/NetworkManagerSetup.cs`의 `UseLocalNetworkPath`, `Assets/StringTableCollection.asset`(프로젝트 Localization Settings), `Dialogue` String Table Collection(`M.Stage1.Intro.Line1~4` 키), `Assets/Scripts/Stage/OXQuizManager.cs`(`OXQuestion.questionText`/`explanationText` = `LocalizedString`), `Assets/Docs/StageDialogueTranslations.md`, `Assets/Docs/OXQuizTranslations.md`.

---

## 트랙 5: 실 App ID Depot 2인 스모크 테스트 — 버그 4건 발견 (2026-08-06/07, 다음 에이전트 인수인계)

> **⚠️ 이 절은 이전 대화의 컨텍스트가 꽉 차서 다음 에이전트가 이어받도록 작성됨. 아직 코드 수정 없음 — 이슈 A는 사용자 OK만 받으면 즉시 구현 가능한 상태, B/C/D는 사용자에게 재현 정보를 더 받아야 확정 진단 가능.**

### 완료된 셋업 (재현 불필요, 참고만)

- **실 App ID(`5029890`)/Depot(`5029891`)로 최초 업로드 성공** — `steamcmd` + ContentBuilder VDF(`app_build_5029890.vdf`, `depot_build_5029891.vdf`). `BuildOutput`을 존재하지 않는 `D:\` 경로에서 `..\output\`(SDK 내부, 상대경로)로 수정해서 해결. **BuildID `24596262`**로 성공, `default` 브랜치에 Set Live 완료.
  - 경고 `WARNING! File steam_appid.txt shouldn't be included in Steam depots.` — 무해, 다음 업로드부터 Depot 파일 매핑에서 제외 권장(급하지 않음).
  - Set Live 직후 설치 시 일시적 "No internet connection" 에러 발생 → CDN 전파 지연으로 진단, 몇 분 후 재시도로 해결 추정.
- **테스터 계정 초대 완료** — App Admin > Manage Users에서 권한 체크박스 **전부 해제**(Manage Users 등 Partner-wide 권한 없음)로 초대. "Everyone" 그룹 autogrant(Developer Comp)로 라이브러리 접근만 확보 — Depot 관리 권한은 안 줌. `Everyone Group Rights: View Rights Only`, `Organization Rights: None`으로 최종 확인됨.
- **테스트 시점 빌드 조건:** Development Build 체크 해제한 Release 빌드로 뽑음 → `NetworkManagerSetup.UseLocalNetworkPath`가 `false`가 되어 Steam 경로(`StartHostSteam`/`StartClientSteam`) 및 Steam 로케일 분기(`GameLocalizationBootstrap`)를 정상적으로 타는 상태에서 테스트됨.

### 사용자 원본 재현 보고 (2026-08-07, 2인 실사용 테스트, 원문 그대로)

1. 로비 들어가면 host 화면이 위 이미지처럼 되어 있음(스크린샷: 빈 슬롯 3개가 이미 이름·별 아이콘·체크(Ready)·색상으로 채워진 것처럼 보임)
2. invite overlay 잘 열리고 잘 초대됨
3. 계정 B 초대 수락 눌렀는데 로비에는 못들어감
4. `109775241266342676`(LobbyId/코드 번호) 입력하고 들어가면 들어가짐
5. 계정 B가 코드번호 입력하고 들어왔는데도 위 이미지처럼(유령 슬롯 상태) 그대로임
6. 계정 B는 정상적으로 2명의 플레이어가 보임
7. 계정 B ready하고 호스트가 (유령 슬롯 상태에서) 그냥 start 누르면 인게임 들어가짐
8. 방폭파(방 나가기/종료)는 잘됨
9. 방폭파하고 다시 방 만들고 테스트하려고 하는데 방이 **2계정 다** 안만들어짐
10. 언어 바꾸고 테스트 재실행. 언어 안바뀜 — Steam 클라이언트 언어 바꿔서 재실행했는데도 그대로

### 이슈 A (#1, #5) — Host 화면 "Young" 유령 슬롯 3개 + 색상 중복 경고 — **원인 확정, 수정 대기 중**

**재현:** Host가 방 만들고 로비 들어가면(실제 인원 1명뿐인데도) 빈 슬롯 3개가 이미 "Young"이라는 이름 + 별 아이콘 + 체크(Ready) + 색(BERRY)로 채워진 것처럼 보임. Client B가 실제 코드로 들어온 뒤에도 Host 화면이 그대로임.

**Root cause:** `LobbySlotUI.SetEmpty()`에 조기 `return`이 있어서, `slotContentRoot`를 꺼주는 것 말고는 개별 필드(이름/Ready/Host별/드롭다운 등)를 정리하는 아래쪽 코드가 전혀 실행되지 않음:

```204:231:Assets/Scripts/UI/LobbySlotUI.cs
    public void SetEmpty()
    {
        _assignedClientId = ulong.MaxValue;
        UnsubscribeDropdown();
        UnsubscribeInput();

        HideAllHeardDots();

        if (emptyVisualRoot != null) emptyVisualRoot.SetActive(true);

        if (slotContentRoot != null)
        {
            slotContentRoot.SetActive(false);
            return; // ← 이 return 때문에 아래 개별 필드 정리가 전부 스킵됨
        }

        // slotContentRoot 미연결 시 개별 처리 (return 때문에 도달 못 함)
        if (portrait           != null) portrait.gameObject.SetActive(false);
        if (nameText           != null) nameText.text = "";
        // ...
        if (readyIndicator     != null) readyIndicator.SetActive(false);
        if (hostIndicator      != null) hostIndicator.SetActive(false);
        // ...
    }
```

실제 씬 파일(`Assets/Scenes/1.Lobby.unity`) 확인 결과, "ID"라는 이름의 `TextMeshProUGUI` 오브젝트(= `nameText` 필드)가 `m_text: Young`으로 하드코딩돼 있고, **`slotContentRoot`의 자식이 아니라 `Slot0`의 직속 자식(형제 관계)**으로 배치돼 있음. 즉 `slotContentRoot.SetActive(false)`로는 이 텍스트가 절대 안 꺼짐. Slot0(호스트, 실제 점유)에서는 `Refresh()`가 `nameText.gameObject.SetActive(false)`를 명시적으로 호출해서 가려지지만, Slot1~3(빈 슬롯)은 위 `return` 때문에 그 처리가 스킵되어 씬 기본값("Young" 텍스트 활성 상태)이 그대로 노출됨. UI 목업 작업 중 임시로 넣어둔 이름("Young" — 파트너 사이트 테스터 계정 표시명과 동일)이 그대로 남은 것으로 추정.

**Files read:** `Assets/Scripts/UI/LobbySlotUI.cs`, `Assets/Scripts/UI/LobbyMenuController.cs`, `Assets/Scripts/Network/LobbyNetworkManager.cs`, `Assets/Scenes/1.Lobby.unity`(grep "Young" + GameObject 계층 추적)

**Cause site:** `Assets/Scripts/UI/LobbySlotUI.cs` → `SetEmpty()` 205~231행, 특히 218행의 `return;`

**Fix proposal:** `return`을 제거해서 `slotContentRoot` 토글 여부와 무관하게 아래 개별 필드 정리 코드가 항상 실행되게 수정. (씬 쪽 "ID" 텍스트를 `slotContentRoot` 자식으로 재배치하는 게 근본적으로 더 깔끔하지만, 그건 씬 편집 영역 — Unity MCP 읽기 전용 규칙상 에이전트가 직접 못 건드림. 코드만 고쳐도 기능적으로는 충분히 해결됨. 씬 재배치는 사용자가 직접 하거나 "MCP로 수정해줘"라고 명시적으로 요청 시에만 가능.)

**Verify:** Build에서 1인만 로비 들어갔을 때 빈 슬롯 3개가 완전히 빈 발판으로 보이는지, 2인 접속 후 정확히 그 슬롯만 실제 데이터로 채워지는지 확인.

**Impact:** 순수 UI 표시 로직 — 네트워크/게임플레이 로직엔 영향 없음. Slot1~3 관련 화면 전체에 동일 효과.

**다음 에이전트 액션:** 사용자에게 이 진단 내용 그대로 보여주고 OK 받으면 `Assets/Scripts/UI/LobbySlotUI.cs` 218행 `return;` 제거하고 컴파일 확인.

### 이슈 B (#3) — Invite 수락해도 로비 자동 참여 안 됨 — **재현 조건 확인 필요**

기존 코드에 이미 범위가 명시돼 있음:

```35:36:Assets/Scripts/Network/SteamLobbyManager.cs
/// Invite Overlay 수락 시 발행 (SteamFriends.OnGameLobbyJoinRequested 중계).
/// 게임이 이미 실행 중인 상태(타이틀 화면)에서만 의미 있음 — 게임 미실행 중 초대 수락(커맨드라인 실행)은 범위 밖.
```

**다음 에이전트가 사용자에게 확인해야 할 것:** 계정 B가 Invite를 수락할 때, **게임이 이미 타이틀 화면에 켜져 있던 상태**였는지, 아니면 **게임이 꺼져 있다가 Accept 누르니까 새로 실행**됐는지.

- 후자라면 → 버그 아님, 원래 범위 밖으로 명시된 케이스(런치 인자 파싱 미구현). 고치려면 별도 작업(커맨드라인 `+connect_lobby` 파싱)이 필요 — 사용자와 우선순위 논의 필요.
- 전자(이미 켜져 있었는데도 안 됨)라면 → 진짜 버그. Client B 쪽 `Player.log`에서 `[SteamLobbyManager] 초대 수락 감지` 로그가 찍혔는지 확인 필요.

### 이슈 C (#10) — 언어 바꿔도 텍스트 안 바뀜 — **테스트 대상 화면이 잘못됐을 가능성 높음, 재테스트 필요**

로비 화면 텍스트("Waiting for all players...", "Warning! Have Same Color Player!!", "START"/"QUIT" 등)는 **로컬라이제이션에 아예 연결이 안 된 하드코딩 영어 텍스트**임 — 트랙4 기준 지금까지 연결된 건 `M.Stage1` 대사 4줄 파일럿 + OX퀴즈 코드뿐이고, 로비 UI는 아직 손 안 댄 상태.

**다음 에이전트 액션:** 로비가 아니라 **`M.Stage1`에 진입해서 대사 4줄이 언어별로 바뀌는지**로 재테스트 요청. (아직 ko 등 다른 언어 값이 채워진 게 en뿐이라, en 폴백이면 그것도 "정상 동작"으로 봐야 함 — 텍스트 자체가 바뀌는지가 아니라 에러 없이 en으로라도 뜨는지가 포인트. 이건 트랙4 "미검증 항목"이었던 `SteamApps.GameLanguage` 분기 스모크 테스트와 동일 건.)

### 이슈 D (#9) — 방 폭파 후 재생성 실패 (양쪽 계정 다) — **로그 필요, 확정 진단 불가**

코드만으로 확신 있게 원인을 못 짚음. 후보 두 가지:

1. `NetworkManagerSetup.StartHostSteam`/`StartClientSteam`에 `_net.IsListening`이 아직 `true`면 조용히 무시하고 `true`만 반환하는 가드가 있음(179~188행 근처) — NGO `Shutdown()`이 비동기라 완전히 정리되기 전에 재시도하면 이 가드에 걸릴 수 있음.
2. `SteamLobbyManager.LeaveCurrentLobby()`가 Steam 쪽 `Leave()` 완료를 기다리지 않고 로컬 상태만 바로 지우는데, Steam 서버 쪽 정리가 늦으면 곧바로 `CreateLobbyAsync()`가 실패할 수 있음.

**다음 에이전트 액션:** 재현할 때 `Player.log`에서 `[NetworkManagerSetup]`, `[SteamLobbyManager]` 태그 로그를 그대로 캡처해달라고 요청 — "이미 실행 중입니다" 경고가 찍혔는지, `CreateLobbyAsync가 null 반환` 에러가 찍혔는지 보면 둘 중 뭔지 바로 나옴.

### 인수인계 요약 — 다음 에이전트가 할 일 (우선순위 순)

1. **이슈 A 수정 승인 요청 → 승인되면 즉시 구현.** 위 진단 그대로 사용자에게 제시하고 OK만 받으면 `LobbySlotUI.cs` 218행 `return;` 제거 (1줄 수정, 리스크 낮음).
2. **이슈 B/C/D는 사용자에게 위 "확인 필요" 질문들 먼저 던지고, 재현 정보(로그/재테스트 결과) 받은 뒤 진단 마무리 → 수정안 제시 → 승인 후 구현.**
3. 이슈 A~D 전부 해결 후, 트랙4 남은 "미검증 항목"(Steam 빌드에서 `SteamApps.GameLanguage` 분기 확인)까지 이슈 C 재테스트로 같이 충족되면 트랙 4/5 모두 종료 처리하고 `ReleaseRoadmap.md` §4 순서대로 응원 시스템 확장 테스트로 넘어갈 것.
