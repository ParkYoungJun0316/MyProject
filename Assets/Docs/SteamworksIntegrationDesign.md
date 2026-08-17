# Steamworks 연동 · 다국어 — 확정 기준 (2026-08-04 전략적 결정 11개 확정 + 트랙 1~3 구현·스모크 테스트 완료, 2026-08-05 트랙 4 다국어 파일럿 완료, 2026-08-06/07 트랙 5 Depot 실사용 스모크 테스트 — 버그 4건 발견, 2026-08-07 2차 세션 이슈 A 수정 + 3차 세션 이슈 D 근본원인 수정·이슈 B 절반 수정 + 4차 세션 이슈 D 실제 수정(virtual port)·이슈 E 신규 발견·수정·이슈 F 신규 발견 + 5차 세션 이슈 F 진짜 원인 확정·유령슬롯 근본수정·이슈 B 온기동 프로세스 재시작 우회 구현 + 6차 세션 이슈 B 온기동 진짜 근본원인 확정·수정 완료·사용자 검증 통과 — 트랙 5 전 이슈 종료 + **2026-08-12 트랙 6 재개 — 4인 Depot 실사용 테스트에서 크래시 + 온기동 재발 보고, 6차 세션 컨텍스트 소진으로 인계 → 7차 세션에서 크래시 원인 확정(D3D12 디바이스 제거, 크래시 덤프 8건으로 증명·"D3D11도 실패" 보고는 구빌드 오데이터로 정정) + 온기동 근본원인 확정·수정 완료(로비 씬에서 초대 이벤트 소멸) — 둘 다 친구/2인 재검증 대기** + **2026-08-13 8차 세션 — 크래시 진짜 원인 확정: 친구 크래시는 D3D12가 아니라 비ASCII(한글) 사용자명 경로에서 libvosk가 모델을 열지 못한 것. 재현 성공(companyName 한글화) + 수정 완료(모델을 StreamingAssets에 풀린 상태로 배포, 런타임 압축 해제 제거, 네이티브 핸들 NULL 검증, 실패 시 음성 인식만 비활성화). 7차 세션의 "D3D12 단독 원인" 결론 정정 — 크래시는 두 종류였다. 한글 사용자명 친구 재검증 대기** + **2026-08-13 밤 9차 세션 — 4인 실사용 재발 보고(온기동 여전히 실패) 후 "공개 방 목록" 아이디어 논의 → 코드 직접입력도 초대와 동일하게 실패한다는 사실 확인, 방 목록은 같은 파이프라인을 공유해 우회책이 안 됨을 근거로 제시하고 보류 → 대신 접속 실패 지점을 특정하기 위한 진단 로그/정체 감지 워치독을 3개 파일에 계측만 추가(코드 수정 없음), 2계정(본인+부계정 B) 재현 대기**)

> ## ⭐⭐⭐ 다음 에이전트(새 채팅) 시작 지침 — 여기부터 읽을 것
>
> **트랙 6 10차 세션이 최신 — 맨 아래 "트랙 6 — 10차 세션" 절부터 읽을 것.**
> 0. 10차 세션에서 온기동/코드입력 공통 실패의 근본 원인을 확정하고 수정 완료했다("호스트 종료 후 같은 프로세스에서 첫 클라이언트 접속" 시 NGO 씬 핸들 충돌 — `Server Scene Handle already exist!`). 다음 세션은 재현 검증 결과부터 받을 것.
> 1. 트랙 5(6차 세션까지)는 전부 해결 확인된 히스토리 — 재조사 불필요.
> 2. **크래시는 두 종류였다 (8차 세션에서 정정 — 7차 세션의 "D3D12 단독 원인" 결론은 절반만 옳았다):**
>    - ① 호스트 PC **"실행 직후"** = D3D12 디바이스 제거(덤프 8건으로 증명). **D3D11 고정으로 조치 — 유지한다.**
>    - ② 친구 PC **"로비 진입 후"** = **비ASCII(한글) 사용자명 경로에서 libvosk가 모델을 못 열고, 실패가 감지되지 않은 채 네이티브 호출로 프로세스 즉사.** 8차 세션에서 **재현 성공 + 수정 완료**(모델을 StreamingAssets에 풀린 상태로 배포).
>    - 7차 세션의 "친구는 D3D11 빌드를 테스트한 적 없다"는 타임라인 추론은 **사용자 정정으로 폐기** — 친구는 D3D11 빌드에서도 크래시했다.
>    - **교훈: 크래시 타이밍이 다르면 다른 버그로 취급할 것.** 호스트 덤프로 친구 크래시를 설명하려 한 것이 2개 세션을 낭비시켰다.
> 3. **온기동 재발도 원인 확정·수정 완료** — 트랙 5 6차 세션 수정의 회귀가 아니라, 그때 다루지 않은 별개 케이스(로비 씬에서 초대 수신)였음.
> 4. Bug Hunter 규칙(`.cursor/rules/bug-hunter.mdc`) 적용 — 새 버그가 나오면 로그로 확정 안 되는 부분은 진단만 하고 사용자 OK 전엔 코드 수정하지 말 것.
> 5. **네이티브 크래시는 `Player.log`가 아니라 크래시 덤프 폴더를 볼 것:** `%LOCALAPPDATA%\Temp\DefaultCompany\Kkul-tteok!\Crashes\Crash_<날짜>\`에 `crash.dmp` + 그 세션의 `Player.log`가 크래시별로 **보존**된다. `Player.log`는 재실행 시 덮어써지지만 이 폴더는 남으므로, 테스트가 끝난 뒤에도 증거를 받을 수 있다.
>
> ---
>
> **상태: 전략적 결정 11개(§1~§11) 확정 완료 + 코드 구현(트랙 1 부트스트랩 / 트랙 2 Transport / 트랙 3 Lobby) 및 실사용 스모크 테스트(Host↔Client 접속, 인게임 진입, Invite Overlay) 전부 통과 (2026-08-04).**
> **트랙 4(다국어) — `DialogueUI` 파일럿 완료 (2026-08-05) + 전체 대사/OX퀴즈 번역 완료 (2026-08-06):** String Table + `LocalizeStringEvent` 패턴 검증 완료, `Dialogue` 테이블 실번역 11개 언어 입력 완료(사용자), OX퀴즈용 `LocalizedString` 코드 전환 + 번역 완료(테이블 미생성). 자세한 내용은 아래 "트랙 4" 절 참고.
> **✅ 트랙 5(실 App ID Depot 2인 스모크 테스트) — 종료. 버그 A~F 전부 해결 확인 완료 (2026-08-07 6차 세션):** 실제 App ID(`5029890`)로 빌드 업로드·Set Live·테스터 계정 초대까지 완료, 발견된 버그 6건(A/B/D/E/F + 유령슬롯 재발) 전부 수정·검증 통과. C(Steam 자동 언어감지)는 사용자 결정으로 우선순위 하향(수동 전환 경로는 이미 동작). 자세한 내용은 맨 아래 "트랙 5" 절 참고.
> §4(연결 API 시그니처)는 실제 구현된 `StartHostSteam(string roomCode = "")` / `StartClientSteam(SteamId)`로 이미 확정 사용 중 — 문서 §4 텍스트만 아직 "미정"으로 남아있어 정리 필요(급하지 않음).
> 확정된 내용은 `NetworkDesign.md` §4.2(Steam P2P + Lobby) / 신규 로컬라이제이션 절로 승격 예정(아직 미승격).
>
> 배경: `ReleaseRoadmap.md` §4 순위 1 "Steamworks (전부)" — 출시 하드 블로커.
> 다국어(스팀 언어 감지 포함)는 같은 페이즈로 묶어 진행하기로 합의됨. **단, 2026-08-07 사용자 결정: Steam 자동 언어감지는 우선순위 하향 — 나중에 만들 설정 화면에서 수동 언어 변경만 확실히 되면 충분(이미 코드상 가능, 블로커 없음). 자세한 내용은 아래 이슈 C 절 참고.**

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

### 인수인계 요약 (2026-08-06/07, 최초 진단 시점 — 아래 "2026-08-07 2차 세션"에서 갱신됨)

1. ~~이슈 A 수정 승인 요청~~ → 2차 세션에서 처리 완료(아래 참고).
2. 이슈 B/C/D는 사용자에게 위 "확인 필요" 질문들 먼저 던지고, 재현 정보(로그/재테스트 결과) 받은 뒤 진단 마무리 → 수정안 제시 → 승인 후 구현.
3. 이슈 A~D 전부 해결 후, 트랙4 남은 "미검증 항목"(Steam 빌드에서 `SteamApps.GameLanguage` 분기 확인)까지 이슈 C 재테스트로 같이 충족되면 트랙 4/5 모두 종료 처리하고 `ReleaseRoadmap.md` §4 순서대로 응원 시스템 확장 테스트로 넘어갈 것.

---

## 트랙 5 — 2026-08-07 2차 세션: 이슈 A 근본 원인 재진단·수정 완료 + B/C/D 추가 진단 (다음 에이전트 인수인계)

> **이 절이 최신 상태.** 위의 "이슈 A~D" 최초 진단(2026-08-06/07)은 출발점으로만 참고하고, 실제 조치는 이 절 기준으로 이어갈 것.

### 이슈 A — 수정 완료 (2단계, 재검증 대기)

**1단계 수정 (승인받고 즉시 적용, 2026-08-07):** `LobbySlotUI.cs` `SetEmpty()`의 조기 `return` 제거 — 최초 진단대로. 하지만 이것만으론 **부족했음** (아래 2단계가 진짜 원인).

**2단계 — 진짜 근본 원인 발견 (재현 로그로 확정):** 1단계 수정을 포함한 빌드로 재테스트했는데도 유령 슬롯이 그대로 재현됨. `Player.log`에서 결정적 증거 발견:

```
[LobbyMenuController] VoskModelLoader 로드 실패 — libvosk assembly:<unknown assembly> type:<unknown type> member:(null)
DllNotFoundException: libvosk ...
  at Vosk.Vosk.SetLogLevel (System.Int32 level) ...
  at VoskModelLoader.GetSharedModel () ...
  at CheerLexiconBuilder.IsKnownWord (System.String word) ...
  at LobbySlotUI.Refresh (...) ...
  at LobbyMenuController.RefreshAllSlots () ...
  at LobbyMenuController.SubscribeAll () ...
  at LobbyMenuController.Start () ...
```

**Root cause:** 빌드에 libvosk 네이티브 DLL이 없어서(별도 이슈 — 음성 인식 관련, 이번 범위 밖) `VoskModelLoader.GetSharedModel()`의 `Vosk.Vosk.SetLogLevel(0)`이 `DllNotFoundException`을 던짐. 이 예외가 `LobbySlotUI.Refresh()`(동기 호출, 182행 `CheerLexiconBuilder.IsKnownWord()`)까지 안 잡히고 올라가서, **`LobbyMenuController.RefreshAllSlots()`의 for문 자체가 로컬(호스트) 슬롯을 처리하다가 그 자리에서 중단됨.** 그 뒤에 와야 할 `allSlotUIs[1..3].SetEmpty()` 호출이 통째로 스킵되어, 빈 슬롯들이 씬 기본값("Young" 이름 + Ready 체크 + 별 아이콘)으로 그대로 남음. (`CheerKeywordEngine`의 같은 호출은 코루틴 안이라 Unity가 개별적으로 예외를 잡아줘서 이 문제가 없었음 — `LobbySlotUI.Refresh()`만 동기 호출이라 안전망이 없었음.)

**Cause site:** `Assets/Scripts/Cheer/VoskModelLoader.cs` → `GetSharedModel()` (당시 95~106행), `Vosk.Vosk.SetLogLevel(0)` 호출부.

**Fix 적용 완료:** `GetSharedModel()`의 모델 로드 부분을 try/catch로 감싸서, 예외 발생 시 로그만 남기고 `null` 반환(이미 있던 "모델 없음→null" 패턴과 동일하게 취급). 이러면 `CheerLexiconBuilder.IsKnownWord()`의 기존 `if (model == null) return true;` 처리로 자연스럽게 흡수되고, `LobbySlotUI.Refresh()`/`RefreshAllSlots()` 루프가 끝까지 정상 실행됨. `LobbyMenuController.cs:98`(`VoskModelLoader.LoadSync()`)와 `CheerKeywordEngine.cs:144`(`GetSharedModel()`)도 같은 수정으로 자동 보호됨 — 코드베이스 전체에서 이 두 API의 호출부는 이 3곳뿐임을 확인함(grep).

**Files changed:** `Assets/Scripts/UI/LobbySlotUI.cs`(1단계, `return` 제거), `Assets/Scripts/Cheer/VoskModelLoader.cs`(2단계, try/catch).

**Verify (다음 에이전트/사용자):** 재빌드 + Depot 재업로드 후, 호스트 혼자 로비 진입 시 빈 슬롯 3개가 완전히 빈 발판으로 보이는지, 2인 접속 시 정확히 그 슬롯만 실데이터로 채워지는지 확인. libvosk DLL 누락 자체(음성 인식 기능이 실제로 동작하는지)는 별도 이슈로 취급 — 이번 수정은 "그로 인한 로비 UI 크래시 전파"만 막는 것.

### 이슈 B — 여전히 미확정, 재현 조건 갱신 필요

- 2026-08-07 재현 시도에서 사용자가 **룸코드 직접 입력**으로 들어감(Invite Overlay 수락 경로 아님) → 이번 로그는 이슈 B 증거로 못 씀.
- **진단용 로그 계측 완료(2026-08-07):** `TitleMenuController.OnClickCreateGame/ConfirmJoinSteam/CreateGameSteamAsync/JoinGameSteamAsync`에 진입 로그 + try/catch 추가, `SteamLobbyManager.CreateLobbyAsync/JoinLobbyAsync/LeaveCurrentLobby`에 진입 로그 + 소요시간(ms) + try/catch 추가, `NetworkManagerSetup.Shutdown()`에 진입 로그 추가. 순수 로그 추가라 동작 변경 없음.
- **⚠️ 로그 필터 명령 실수 정정:** 이전에 안내한 `Select-String` 필터 패턴에 `\[TitleMenuController\]`가 빠져 있어서, 정작 "버튼이 눌렸는지/초대냐 수동입력이냐" 구분에 필요한 로그가 잘려나갔었음. **다음엔 이 패턴 사용:**
  ```powershell
  Select-String -Path "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\Kkul-tteok!\Player.log" -Pattern "\[NetworkManagerSetup\]|\[SteamLobbyManager\]|\[GameLocalizationBootstrap\]|\[SteamManager\]|\[TitleReturnFlow\]|\[TitleMenuController\]" | Select-Object -ExpandProperty Line
  ```
- **다음 에이전트 액션:** 게임이 이미 타이틀 화면에 떠 있는 상태에서 Invite Overlay로 수락하는 케이스를 정확히 재현해서(커맨드라인 실행 케이스 아님) 위 필터로 로그 받을 것. `[SteamLobbyManager] 초대 수락 감지` 로그 유무로 이벤트 체인이 끊긴 지점을 좁힐 것 (§ 위 "이슈 B" 최초 진단 참고).

### 이슈 C — 원인 확정(Steam 파트너 사이트 언어 선언 누락), 그러나 사용자 결정으로 우선순위 하향

- **1차 확인:** Steamworks 파트너 사이트 "Managing Base Languages"에 English만 체크돼 있었음(스크린샷 확인) → 12개 코어 언어 체크 + Save 완료.
- **2차 확인:** Base Languages 등록 후에도 `Player.log`에 `[GameLocalizationBootstrap] Locale 적용 — en (소스: Steam)`이 계속 찍힘. 웹 검색으로 확인한 Steamworks 공식 동작: `GetCurrentGameLanguage()`는 Steam 클라이언트 전체 언어가 아니라 **Steam 라이브러리에서 그 게임에 대해 개별로 설정하는 "Language" 값**(Library > 게임 우클릭 > 속성 > 일반 > Language 드롭다운)을 먼저 보고, 이게 기본으로 English로 고정되는 게 알려진 Steam 클라이언트 동작(참고: [rlabrecque/Steamworks.NET#539](https://github.com/rlabrecque/Steamworks.NET/issues/539), [ceifa/steamworks.js#141](https://github.com/ceifa/steamworks.js/issues/141)).
- **3차 확인(반전):** 사용자가 실제로 Steam 라이브러리 속성 화면을 확인했는데, **일반 탭에 Language 드롭다운 자체가 안 보임**(스크린샷 확인 — "게임 내 Steam 오버레이 사용" 토글과 "실행 옵션"만 있고 언어 섹션 없음). 원인 미확인 상태(Steam 클라이언트 버전/캐시/Depot 언어 메타데이터 전파 지연 등 후보 있으나 미검증).
- **⭐ 사용자 결정 (2026-08-07): 이 이슈는 우선순위 하향.** Steam 자동 언어감지가 되면 좋지만 필수는 아니고, **나중에 만들 설정(Settings) 화면에서 수동으로 언어를 바꾸는 기능만 확실히 동작하면 충분**하다고 확정함. 수동 전환은 `LocalizationSettings.SelectedLocale = <Locale>` 한 줄로 Steam API와 완전히 무관하게 이미 동작 가능한 경로이므로(en/ko 폴백이 이미 검증된 것과 같은 메커니즘), **블로커 없음.**
- **다음 에이전트 액션 없음(당장) — Settings 화면 구현 시점에 언어 선택 UI에서 `LocalizationSettings.SelectedLocale` 연결만 하면 됨.** Steam 자동감지 미스터리는 사용자가 다시 요청할 때 재조사.

### 이슈 D — 이번 세션 미재현, 재현 자체를 안 해봄

- 사용자가 방 폭파 후 재생성을 시도하지 않고 바로 앱을 종료함(`quit`) — 이번 세션은 증거 없음, 최초 진단(두 가지 후보)만 유효.
- 진단용 로그 계측(이슈 B와 동일 커밋)이 이미 적용돼 있으니, 다음 재현 시 위 이슈 B의 정정된 필터로 `Shutdown 완료` 이후 로그가 이어지는지만 보면 됨 — 로그가 없으면 버튼 클릭 자체가 안 된 것, `이미 실행 중입니다` 경고가 있으면 §후보1(NGO Shutdown 비동기 타이밍), `CreateLobbyAsync`/`JoinLobbyAsync` 예외 로그가 있으면 §후보2(Steam Lobby 정리 타이밍).

### Depot 업로드 절차 (정리, 2026-08-07)

SDK 위치: `C:\Users\u\Desktop\Steam\sdk\tools\ContentBuilder\` (Unity 프로젝트 밖, Desktop). 스크립트: `scripts\app_build_5029890.vdf` + `scripts\depot_build_5029891.vdf`.

1. Unity Build (Windows, **Development Build 체크 해제** — Steam 경로를 타야 함).
2. 빌드 결과물을 `content\KKUL-TTEOK!_Build\`에 통째로 복사(덮어쓰기). **`steam_appid.txt`는 이 폴더에 넣지 말 것** — Unity 프로젝트 루트(`steam_appid.txt`)는 에디터/로컬 테스트 전용(§5)이고 Depot엔 불필요, 넣으면 `WARNING! File steam_appid.txt shouldn't be included in Steam depots.` 경고만 남음(무해하지만 지저분함).
3. `cd C:\Users\u\Desktop\Steam\sdk\tools\ContentBuilder` → `.\builder\steamcmd.exe +login <계정> +run_app_build ..\scripts\app_build_5029890.vdf +quit`
4. Steamworks 파트너 사이트 App Admin > SteamPipe > Builds에서 새 BuildID를 `default` 브랜치에 Set Live.
5. `app_build_5029890.vdf`의 `Desc`는 버전 문자열(현재 `"Version 1.0.3 Beta"`, 2026-08-07 갱신 — 이전엔 `1.0.1 Beta`로 2번 빌드됨) — 다음 업로드 전 필요시 갱신.

### 인수인계 요약 (2026-08-07 2차 세션 기준, 최신)

1. **이슈 A — 수정 완료.** 재빌드 + Depot 재업로드 후 재검증만 남음.
2. **이슈 B — Invite Overlay 수락(게임이 타이틀에 떠 있는 상태)으로 정확히 재현 + 정정된 필터(`[TitleMenuController]` 포함)로 로그 캡처 필요.**
3. **이슈 C — 보류.** Settings 화면 만들 때 `LocalizationSettings.SelectedLocale` 연결만 하면 됨. Steam 자동감지 문제(Library 속성에 Language 드롭다운 자체가 안 보이는 것)는 사용자가 다시 요청할 때만 재조사.
4. **이슈 D — 실제로 방 폭파 후 재생성을 시도하는 재현이 아직 한 번도 안 됨.** 다음 테스트에서 반드시 시도하고 정정된 필터로 로그 캡처.
5. B/D 둘 다 진단용 로그 계측은 이미 배포된 빌드에 포함돼 있음 — 추가 코드 변경 없이 재현 로그만 받으면 진단 마무리 가능.

---

## 트랙 5 — 2026-08-07 3차 세션: 이슈 D 근본 원인 재확정·수정 완료 + 이슈 B 절반 수정(냉기동) + 이슈 B 재확인(온기동, 미해결) (다음 세션 인수인계)

> **이 절이 최신 상태.** 2차 세션 인수인계 요약(바로 위)의 "후보 두 가지" 추측은 이 절에서 확정된 실제 원인으로 대체됨.

### 이슈 D — 진짜 근본 원인 확정 + 수정 완료 (재빌드 후 검증 대기)

**사용자가 이번 세션에 직접 재현 + Player.log(계정 A "Young") 전체 캡처 제공.** 로그 분석 결과 2차 세션의 두 후보(①`IsListening` 레이스 ②Lobby 정리 타이밍) 둘 다 아니었음 — 실제 원인은 완전히 다른 곳.

**Root cause:** `FacepunchTransport.Shutdown()`(NGO 패키지 코드)이 소켓만 닫는 게 아니라 **`SteamClient.Shutdown()`을 호출해서 Steam 클라이언트 전체를 죽여버림.** 반면 `SteamManager.IsInitialized` 플래그는 절대 리셋이 안 돼서(자기 스스로 Shutdown된 적 없다고 착각), 다음 `StartHostSteam()`에서 재초기화를 스킵함. 그 상태로 `NetworkManager.StartHost()` → `FacepunchTransport.StartServer()` → `SteamNetworkingSockets.CreateRelaySocket()`가 `ArgumentException: Invalid Socket`으로 매번 실패(로그에서 4연속 재현 확인). **한 번이라도 Host를 했다가 방을 폭파하면, 같은 프로세스에서는 재시작 전까지 다시 Host가 안 되는 버그**였음 — 계정 B가 성공했던 건 B가 그 프로세스에서 Host(서버 릴레이 소켓)를 만든 적이 한 번도 없어서(Client로만 있었음) 우연히 안 걸린 것.

**#6(계정 A 화면 0명)도 동일 원인의 연쇄 증상으로 확인:** A가 Host 재시도를 4번 실패하는 동안 NGO `NetworkSceneManager` 내부 상태가 오염돼서, 그 뒤 A가 B 방에 Client로 들어갔을 때 `Exception: Server Scene Handle already exist! ... scene load of 1.Lobby`가 6번 연속 발생 → 씬 동기화 실패 → 스폰 반영 안 됨(0명) → Ready 클릭 시 RPC `NullReferenceException` → 연결 끊김. 이슈 D가 고쳐지면 재호스트가 정상적으로 성공하므로 이 연쇄도 같이 사라질 것으로 예상(별도 검증 필요).

**Fix 적용 완료:**
- `Library/PackageCache/com.community.netcode.transport.facepunch@27d3e825ecdd/Runtime/FacepunchTransport.cs` → `Shutdown()`에서 `SteamClient.Shutdown();` 줄 삭제, `connectionManager`/`socketManager`를 `null`로 정리하는 코드로 교체. **주의(§2와 동일한 성격의 임시 위치 이슈): `Library/PackageCache`는 Unity가 관리하는 캐시라 패키지 재-resolve(Library 삭제, 매니페스트 버전 변경 등) 시 이 수정이 사라질 수 있음.** 영구화하려면 이 패키지를 Package Manager에서 "Embed" 후 그 복사본(`Packages/com.community.netcode.transport.facepunch/`)에 동일 수정을 다시 적용할 것 — §2의 "중복 #endregion" 워크어라운드와 같은 성격의 임시 위치 이슈이니 그때 같이 영구화 권장.
- `Assets/Scripts/Network/NetworkManagerSetup.cs` → `Shutdown()` 로그에 `SteamManager.IsInitialized` 상태 같이 출력하도록 보강(검증용, 동작 변경 없음).

**Verify (다음 세션 — 사용자가 내일 재빌드 후 확인):** 재빌드 + Depot 재업로드 후, Host → 방 폭파 → 다시 Host 생성을 연속 3회 이상 시도해서 전부 성공하는지, 그 뒤 Client 접속 시 씬 핸들 충돌 예외 없이 정상 스폰(#6 증상 재현 안 되는지)까지 함께 확인. Player.log에서 `StartHostSteam() 실패`/`Invalid Socket` 문자열이 더 이상 안 나오는지로 1차 확인 가능.

**Files read (이번 세션 추가):** 계정 A `Player.log`(직접 읽음), `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/Network/SteamLobbyManager.cs`, `Assets/Scripts/Network/SteamManager.cs`, `Library/PackageCache/com.community.netcode.transport.facepunch@27d3e825ecdd/Runtime/FacepunchTransport.cs`.

### 이슈 B — 냉기동(게임 꺼진 상태) 케이스 구현 완료 + 온기동(게임 켜진 상태) 케이스는 여전히 미해결

사용자가 **두 케이스 다** 재현: (a) 게임이 켜진 채 타이틀에 있는데 초대를 받아도 계정 B가 그 자리에 그대로 머묦, (b) 게임이 꺼진 상태에서 초대 수락 → 새로 실행되지만 타이틀까지만 가고 로비로 안 들어감.

**(b) 냉기동 케이스 — 구현 완료 (재빌드 후 검증 대기):** Steam은 게임이 안 켜진 상태에서 Lobby Invite를 수락하면 `+connect_lobby <64bit lobbyId>`를 커맨드라인 인자로 넘겨서 게임을 새로 실행시킨다(Steamworks 공식 문서, [partner.steamgames.com/doc/features/multiplayer/matchmaking](https://partner.steamgames.com/doc/features/multiplayer/matchmaking) 확인). 이 인자를 안 읽고 있던 게 원인. `TitleMenuController.Start()`에서 `Environment.GetCommandLineArgs()`로 `+connect_lobby` 토큰을 찾아 파싱 후, 기존 Invite Overlay 수락 경로(`OnSteamInviteAccepted`)와 동일한 `JoinGameSteamAsync()`로 합류시킴. 앱 프로세스 수명당 1회만 처리(`static bool s_launchLobbyArgsHandled`) — 타이틀 복귀로 씬이 다시 로드돼도 재시도 안 함.

**(a) 온기동 케이스 — 원인 미확정, 재현 로그 필요.** 기존 경로(`SteamFriends.OnGameLobbyJoinRequested` → `SteamLobbyManager.OnInviteAccepted` → `TitleMenuController.OnSteamInviteAccepted` → `JoinGameSteamAsync`)는 코드상 이미 완성돼 있고 오늘 건드리지 않았음. 이 경로가 왜 안 되는지는 코드만 봐서 확정 불가 — 아래 두 후보 중 뭔지는 로그로만 구분 가능:
1. Steam이 콜백(`OnGameLobbyJoinRequested`) 자체를 안 준 것 → `[SteamLobbyManager] 초대 수락 감지` 로그가 전혀 안 찍힘.
2. 콜백은 왔는데 그 다음(`JoinGameSteamAsync`/`StartClientSteam`)이 실패 → 그 로그는 찍히는데 그 이후 로그가 없거나 예외가 남음.
   - 참고로 이번 세션에 확인한 유명한 후보 원인 하나(Facepunch.Steamworks `InstallEvents()` 미호출로 `OnGameLobbyJoinRequested`가 전혀 안 불리는 버그, [Facepunch.Steamworks#379](https://github.com/Facepunch/Facepunch.Steamworks/issues/379))는 **2020년에 이미 고쳐진 버그라 우리가 쓰는 2.3.2 버전엔 해당 안 됨 — 배제함.**

**다음 세션 액션:** 계정 B를 미리 타이틀 화면에 켜둔 상태에서 A가 Invite Overlay로 초대 → B가 Accept → 아래 필터로 B의 `Player.log` 캡처:
```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\Kkul-tteok!\Player.log" -Pattern "\[NetworkManagerSetup\]|\[SteamLobbyManager\]|\[TitleMenuController\]|\[SteamManager\]" | Select-Object -ExpandProperty Line
```
`[SteamLobbyManager] 초대 수락 감지` 유무로 위 후보 1/2를 바로 가른다.

**Files changed (이번 세션):** `Assets/Scripts/UI/TitleMenuController.cs`(냉기동 커맨드라인 파싱 추가), `Assets/Scripts/Network/NetworkManagerSetup.cs`(로그 보강), `Library/PackageCache/com.community.netcode.transport.facepunch@27d3e825ecdd/Runtime/FacepunchTransport.cs`(이슈 D 수정).

### 인수인계 요약 (2026-08-07 3차 세션 기준, 최신)

1. **이슈 A — 사용자 확인 완료(해결).**
2. **이슈 B — 냉기동 케이스 수정 완료(검증 대기). 온기동 케이스는 위 필터로 B의 Player.log 캡처 필요.**
3. **이슈 C — 보류(사용자 결정 유지). 이번 세션 재확인(#4, 인게임에서도 en) — 새 정보 아니고 기존 미스터리 그대로.**
4. **이슈 D — 근본 원인 확정·수정 완료(검증 대기). 재빌드 + Depot 재업로드 후 Host 재생성 연속 테스트 + Player.log 확인.**
5. **2026-08-07 오전 재빌드 + 재테스트 완료 — 결과는 사용자가 다음 채팅(컨텍스트 초과로 이 채팅 이어가기 불가)에서 전달할 예정. 다음 에이전트는 그 결과(Player.log 등)를 받는 즉시 이슈 D/B(온기동) 확정 진단부터 시작할 것.**

## 트랙 5 — 2026-08-07 4차 세션: 이슈 D "진짜" 근본원인 확정(virtual port 방식으로 수정) + 이슈 E 신규 발견·수정 + 이슈 F 신규 발견(미수정) (다음 세션 인수인계)

> **이 절이 최신 상태.** 3차 세션에서 "근본 원인 확정"이라고 적었던 `SteamClient.Shutdown()` 제거 수정은 **필요하지만 불충분한 수정**이었음 — 재빌드 후에도 사용자가 같은 프로세스에서 재호스트를 시도하면 `Invalid Socket`이 그대로 재발했다.

### 이슈 D — 재발. 진짜 원인은 "릴레이 소켓 virtualport=0 재사용 자체가 구조적으로 불가"

**재현 로그(계정 A):** `SteamClient.Shutdown()` 제거 수정이 반영된 빌드에서도, Host → 방 폭파 → 재-Host 시도 시 여전히 `SteamNetworkingSockets.CreateRelaySocket()`에서 `ArgumentException: Invalid Socket` 발생. `SteamManager.IsInitialized`는 이번엔 정상적으로 `True`를 유지하고 있었음(3차 세션 수정은 유효) — 그런데도 실패. 즉 Steam 클라이언트 자체는 살아있는데, **같은 프로세스에서 `virtualport=0`으로 릴레이 리슨 소켓을 한 번 닫았다가 다시 여는 것 자체가 SteamNetworkingSockets 레이어의 구조적 제약으로 막혀 있음**(재시도 딜레이를 줘도 동일하게 실패 — 사용자가 직접 확인). 타이밍/레이스 문제가 아니라 "같은 소켓 번호 재사용 불가"라는 API 레벨 제약.

**Fix (구현 완료, 재빌드 후 검증 대기):** 소켓을 닫았다 다시 여는 대신, **Host 세션마다 다른 virtual port 번호를 발급**해서 재사용 자체를 피하는 방식으로 전환.
- `Library/PackageCache/com.community.netcode.transport.facepunch@27d3e825ecdd/Runtime/FacepunchTransport.cs`: `virtualPort` 필드 추가, `StartServer()`의 `CreateRelaySocket()`과 `StartClient()`의 `ConnectRelay()`에 전달하도록 수정. (§2/이슈 D 3차 세션 수정과 같은 성격의 임시 위치 이슈 — Package Manager "Embed" 시 함께 영구화 필요.)
- `Assets/Scripts/Network/NetworkManagerSetup.cs`: `StartHostSteam()`이 프로세스 전역 증가 카운터(`s_nextVirtualPort`, 1부터 시작)로 매번 새 포트를 발급해 `steamTransport.virtualPort`에 설정, 발급값은 `LastHostVirtualPort`로 공개. `StartClientSteam(SteamId hostId, int virtualPort = 0)`으로 시그니처 확장 — Client는 Host가 실제로 쓴 포트 번호를 받아서 그대로 연결해야 함(안 그러면 기본 포트 0에 접속 시도하다 실패).
- `Assets/Scripts/UI/TitleMenuController.cs`: `CreateGameSteamAsync()`에서 Host 성공 후 `lobby.Value.SetData("vport", ...)`로 Lobby 메타데이터에 포트 번호를 실어 공유. `JoinGameSteamAsync()`에서 `lobby.Value.GetData("vport")`로 읽어 `StartClientSteam(ownerId, vport)`에 전달(값 없거나 파싱 실패 시 0으로 폴백 — 구버전 호환).

**Verify (다음 세션):** 재빌드 후 Host → 방 폭파 → 재-Host를 프로세스 재시작 없이 5회 이상 연속 성공하는지, 그리고 Client가 매번 정상적으로 그 Host에 접속되는지(포트 불일치로 연결 실패하지 않는지) 확인.

### 이슈 E (신규) — 로비에서 Host가 "나가기" 눌러도 Client가 타이틀로 안 돌아옴 — 원인 확정·수정 완료

**재현:** 계정 A(Host)가 로비 화면에서 "나가기" 버튼 클릭 → 계정 A 본인은 정상적으로 타이틀 복귀됨(로그 확인) → 그러나 계정 B(Client)는 로비 화면에 그대로 멈춰 있음 (Steam 쪽에서도 방이 살아있는 것처럼 보임).

**Root cause:** 인게임(`M.Stage1`/`T.Stage1`)에는 `DisconnectManager`가 있어서 Host 이탈 시 Client 전원에게 `NotifyAllReturnClientRpc()`로 즉시 알리고, 만약 그마저 놓쳐도 `NetworkManager.OnClientDisconnectCallback`으로 자기 자신의 연결 끊김을 감지해 타이틀로 복귀한다. **`1.Lobby` 씬에는 이 두 안전장치가 전혀 없었음** — `LobbyMenuController.OnClickQuit()`은 그냥 로컬 `TitleReturnFlow.Request()`만 호출하고 끝(Client에 알리는 RPC 없음), `LobbyNetworkManager`는 Host 쪽에서만 `OnClientDisconnectCallback`을 구독하고(슬롯 정리용) Client 쪽은 아무것도 구독하지 않았음. 그 결과 Host가 나가서 서버가 죽어도, Client는 자기 연결이 끊긴 걸 알아챌 코드 자체가 없어서 로비 화면에 그대로 방치됨.

**Fix 적용 완료:**
- `Assets/Scripts/Network/LobbyNetworkManager.cs`: Client 쪽 `OnNetworkSpawn()/OnNetworkDespawn()`에 `NetworkManager.OnClientDisconnectCallback`을 새로 구독(`OnClientDisconnectedSelf`) — 인게임 `DisconnectManager`와 동일한 isSelf 판정으로 자기 연결이 끊기면 `TitleReturnFlow.Request(ClientDisconnected)` 호출. 그리고 Host 전용 공개 메서드 `NotifyHostQuit()` + `[ClientRpc] NotifyHostQuitClientRpc()` 추가 — Host가 나갈 때 Client 전원에게 즉시 `TitleReturnFlow.Request(HostQuitRoom)`을 브로드캐스트.
- `Assets/Scripts/UI/LobbyMenuController.cs`: `OnClickQuit()`이 `TitleReturnFlow.Request()` 호출 전에 `LobbyNetworkManager.Instance.IsHost`면 먼저 `NotifyHostQuit()`을 호출하도록 수정 (RPC가 먼저 도착 → Client 즉시 복귀, 혹시 못 받아도 위 `OnClientDisconnectedSelf`가 안전망 역할).
- `TitleReturnFlow.Request()`의 기존 `_isReturning` 가드 덕분에 RPC 경로와 disconnect 콜백 경로가 둘 다 걸려도 중복 처리되지 않음(추가 플래그 불필요).

**Verify (다음 세션):** Host가 로비에서 "나가기" 클릭 시 Client가 즉시(또는 최소 지연으로) 타이틀로 복귀하는지, Steam 쪽에서도 Lobby가 정상적으로 정리되는지 확인.

**설계 결정 확정(사용자 확인, 2026-08-07 4차 세션):** 인게임용 기존 `DisconnectManager` 컴포넌트를 로비 씬에 그대로 붙이는 방식은 **채택하지 않음** — 사용자에게 직접 확인함. 이유: `DisconnectManager`의 Host측 로직(`OnClientLeft`)은 "누구든(자기 자신 아니어도) 한 명이라도 이탈하면 무조건 전원 타이틀 복귀"라서, 로비의 기존 확정 정책("로비 kick/leave = 슬롯만 초기화, 인게임만 방 종료" — `multiplayer-ngo.mdc`)과 정면 충돌한다. 그대로 붙이면 로비에서 클라이언트 1명만 나가거나 네트워크가 잠깐 끊겨도 호스트 포함 전원이 타이틀로 튕기는 회귀가 생김. 사용자가 "기존 그대로 — 그 슬롯만 비우고 나머지는 로비에 남음" 정책을 명시적으로 재확인했으므로, 위 `LobbyNetworkManager`/`LobbyMenuController` 전용 구현(호스트 명시적 이탈 시에만 전원 알림)을 유지 — 향후 에이전트는 이 부분을 `DisconnectManager` 재사용으로 되돌리지 말 것.

### 이슈 F (신규, **미수정** — 진단만 완료) — Client로 씬 진입 시 `Server Scene Handle already exist` 재현, 이슈 D와 무관한 별개 원인

**재현 로그(계정 A, 이번 세션 캡처):** 한 프로세스 안에서 ①Host로 "1.Lobby" 정상 진입 → ②로비에서 "나가기"로 정상 Shutdown+타이틀 복귀(실패한 재호스트 시도 없음, 이슈 D 연쇄 아님) → ③이후 **Client**로 다른 사람이 새로 만든 방("1.Lobby")에 참여 → `Exception: Server Scene Handle (...) already exist! Happened during scene load of 1.Lobby with Client Handle (-678)` 즉시 발생, 스폰 실패.

이게 중요한 이유: 이전 3차 세션에서는 이 예외를 "이슈 D(재호스트 실패)의 연쇄 증상"으로 추정했었는데, **이번엔 재호스트 실패가 단 한 번도 없었는데도 똑같은 예외가 재현됨** — 즉 이슈 D와는 별개의 독립적인 버그다. 사용자가 물어본 "3번 현상(A 0명/B 3명)이 로비 폭파 문제(이슈 E)와 같은 원인이냐"에 대한 답: **아니다, 이슈 E와도 다른 제3의 원인.**

**가설(미검증, NGO 소스 분석 기반):** NGO `NetworkSceneManager`는 Host→Client 세션 전환 때마다 `ServerSceneHandleToClientSceneHandle` 딕셔너리를 새로 만들지만(`NetworkManager.ShutdownInternal()`이 `SceneManager.Dispose()` 후 `null` 처리 — 코드 확인함), 새 `NetworkSceneManager`가 초기화되며 **그 시점에 로컬에 이미 로드돼 있는 Unity 씬들("0.Title" 등)의 `Scene.handle` 값을 자기 자신에게 매핑해 미리 등록**한다(`InitializeScenesLoaded()`). `Scene.handle`은 Unity가 프로세스 내에서 재사용하는 작은 정수라서, 마침 클라이언트가 현재 있는 "0.Title" 씬의 handle 번호와 **원격 Host가 보내온 "1.Lobby" 씬의 서버측 handle 번호가 우연히 같은 값으로 겹치면** `UpdateServerClientSceneHandle()`이 "이미 존재"로 판단해 예외를 던진다(`ClientLoadedSynchronization`, NGO 2.9.2 소스 `NetworkSceneManager.cs` 1800줄대 확인). 즉 Host였던 적이 있는지와 무관하게, **같은 프로세스에서 씬 로드를 반복하며 Unity의 handle 번호가 우연히 겹치기만 해도** 발생할 수 있는 구조적 이슈로 추정.

**아직 코드 수정 안 함 — 이유:** (1) 위 가설은 NGO 소스 코드 분석 기반 추론이며 실측 handle 번호로 직접 검증하지 못함, (2) 이슈 D/E 수정으로 씬 로드 시퀀스/타이밍이 달라지면 이 우연한 충돌 자체가 사라지거나 재현 조건이 바뀔 수 있음, (3) 섣부른 수정은 리스크 대비 확신도가 낮음(Bug Hunter 원칙).

**다음 세션 액션(우선순위):**
1. 이슈 D/E 수정 반영 후 재테스트 — 이슈 F가 여전히 재현되는지부터 확인. (씬 로드 흐름이 바뀌어서 우연히 사라질 가능성 있음, 그래도 근본 해결은 아니므로 계속 관찰 필요.)
2. 재현되면, 예외 발생 직전/직후 `Debug.Log($"[Diag] localScene({sceneName}).handle={UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName).handle}")` 같은 임시 로그를 추가해서 실제 handle 값 충돌을 실측 확인.
3. 확인되면 후보 수정안(택1, 리스크·효과 재검토 필요): (a) Client가 "0.Title"에서 대기 중일 때 씬 로드를 시작하지 않고 먼저 언로드하도록 흐름 변경, (b) 이 특정 예외를 캐치해서 완전 실패 대신 재시도/복구하는 래퍼 추가, (c) Unity/NGO 패치 버전 확인(2.9.2 이후 수정된 known issue인지 체인지로그 확인).

**Files changed (이번 세션):** `Library/PackageCache/com.community.netcode.transport.facepunch@27d3e825ecdd/Runtime/FacepunchTransport.cs`(virtual port 필드/적용), `Assets/Scripts/Network/NetworkManagerSetup.cs`(virtual port 발급/전달), `Assets/Scripts/UI/TitleMenuController.cs`(Lobby 데이터로 vport 공유/조회), `Assets/Scripts/Network/LobbyNetworkManager.cs`(Client 이탈 감지 + Host 이탈 알림 RPC 추가), `Assets/Scripts/UI/LobbyMenuController.cs`(OnClickQuit에서 NotifyHostQuit 호출 추가).

**Files read (이번 세션):** 계정 A `Player.log`(직접 읽음, 최신), `Assets/Scripts/Flow/TitleReturnFlow.cs`, `Assets/Scripts/Network/DisconnectManager.cs`, `Assets/Scripts/Network/LobbyNetworkManager.cs`, `Assets/Scripts/Network/SteamLobbyManager.cs`, `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/UI/TitleMenuController.cs`, NGO 패키지 소스(`NetworkSceneManager.cs`, `NetworkManager.cs`, `com.unity.netcode.gameobjects@02e4aaa4170c`).

### 인수인계 요약 (2026-08-07 4차 세션 기준, 최신)

1. **이슈 A — 해결 확인 완료.**
2. **이슈 B — 냉기동 해결 확인 완료(사용자 재확인). 온기동은 여전히 원인 미확정 — 위 3차 세션 절의 로그 캡처 방법 그대로 필요.**
3. **이슈 C — 보류(사용자 결정 유지, 우선순위 하향).**
4. **이슈 D — 3차 세션 수정은 불충분했음(재발 확인). 4차 세션에서 virtual port 방식으로 재수정 완료 — 재빌드 후 연속 재호스트 테스트 필요.**
5. **이슈 E(신규) — 원인 확정·수정 완료. 재빌드 후 Host 로비 이탈 시 Client 즉시 복귀하는지 검증 필요.**
6. **이슈 F(신규) — 진단만 완료, 미수정. 이슈 D/E 재테스트 후에도 재현되면 위 "다음 세션 액션" 순서대로 진행.**

---

## 트랙 5 — 2026-08-07 5차 세션: 유령 슬롯 근본수정 + 이슈 F 진짜 원인 확정(4차 세션 가설 기각) + 이슈 B(온기동)·F 공통 우회 구현 (다음 세션 인수인계)

> **이 절이 최신 상태.** 4차 세션의 이슈 F 가설("Unity Scene.handle 재사용 충돌")은 이번 세션 재현 로그로 **기각됨** — 진짜 원인은 완전히 다름.

### 발견한 버그 1 — 유령 슬롯 재발의 진짜 원인: `LobbyNetworkManager.OnClientJoined`에 중복 방지 가드 없음 — **수정 완료**

**증상:** 클라이언트 1명이 접속했는데 호스트 화면에 같은 사람이 여러 슬롯에 중복으로 나타남(계정A 4명/계정B 0명 등). 클라이언트가 나가도 유령 슬롯이 남음.

**Root cause:** Host·Client 양쪽 `Player.log`를 대조한 결과, `NetworkManager.OnClientConnectedCallback`이 **같은 clientId에 대해 여러 번(실측 5회) 호출**됐는데, `LobbyNetworkManager.OnClientJoined()`는 호출될 때마다 무조건 `_slots.Add()`만 해서 매번 중복 슬롯이 쌓였음. `OnClientLeft()`도 첫 매치 하나만 지우고 `return`해서, 나갈 때도 유령이 대부분 남았음.

**Fix 적용 완료 (`Assets/Scripts/Network/LobbyNetworkManager.cs`):**
- `OnClientJoined`: 슬롯 추가 전 해당 `clientId`가 이미 있는지 검사 — 있으면 무시.
- `OnClientLeft`: 첫 매치만 지우던 것을 **해당 clientId 슬롯 전부 제거**로 변경.

**검증 결과(사용자 재테스트, 이번 세션):** 이 수정 이후 호스트 쪽 슬롯 수는 정확히 1개씩만 증가 — 유령 슬롯 문제 자체는 해결 확인됨. (단, 아래 "발견한 버그 2"는 별개로 남아있었음.)

### 발견한 버그 2 — 이슈 F 진짜 원인: `ConnectionApprovedMessage` 트랜스포트 레벨 중복 전달, 온기동(이슈 B)과 동일 원인 — **프로세스 재시작으로 우회 구현**

**4차 세션 가설 기각:** "Unity Scene.handle 재사용 우연 충돌" 가설은 근거 부족이었음. 이번 세션에 유령 슬롯 수정을 반영한 빌드로도 `Server Scene Handle already exist`가 **그대로 재현**돼서 가설이 틀렸음이 확인됨.

**진짜 Root cause (로그로 확정):** 클라이언트 쪽 `Player.log`에 결정적 증거:
```
[Netcode] [Client-2] Connection approved! Synchronizing...
[Netcode] [Client-2] Connection approved! Synchronizing...   ← 같은 접속에 대해 중복
```
바로 다음 재접속 시도에서는 3번 중복으로 더 늘어남. **`ConnectionApprovedMessage`가 클라이언트에 중복 전달**되고, 클라이언트는 이 메시지를 받을 때마다 현재 씬("1.Lobby")을 다시 로드하려 시도 → 첫 처리는 성공해서 Server↔Client Scene Handle 매핑을 등록하지만, 중복 처리가 같은 매핑을 또 등록하려다 `Server Scene Handle already exist!`로 충돌·크래시. 이후 NGO 내부 상태가 깨져서 `Ready` 클릭 시 RPC `NullReferenceException`까지 이어짐.

이건 우리 C# 코드 버그가 아니라 **Facepunch.Steamworks/SteamNetworkingSockets 릴레이 트랜스포트가 메시지를 중복 전달하는 SDK 레벨 문제**로 확인됨 — Unity NGO 공식 GitHub에 동일 증상 이슈 있음([`#2704`](https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/issues/2704), NGO 팀 답변: "Steam Networking Transport가 중복 메시지를 전달하는 문제로 보임").

**왜 냉기동은 되고 온기동만 안 되는지 — 이슈 B와 완전히 같은 원인으로 통합됨:**
- **냉기동**(프로세스 새로 실행): `SteamClient.Init()`이 진짜 최초 1회 → 릴레이 세션 상태 깨끗 → 항상 성공.
- **온기동**(같은 프로세스에서 재접속): 이슈 D(재호스트 `Invalid Socket`) 우회를 위해 `SteamClient.Shutdown()`을 절대 호출하지 않도록 만들어 놨음(3차 세션) — 그 결과 같은 프로세스 안에서 클라이언트 접속을 반복할수록 Steam 릴레이 세션/디스패치 상태가 절대 리셋되지 않고 누적됨. 재접속 시도 횟수가 늘수록 중복 메시지 개수도 1→2→3으로 계속 늘어나는 것을 실측 확인.
- 호스트 쪽 재호스팅(virtual port 우회, 이슈 D)은 같은 프로세스에서 반복해도 문제 없었음 — 이 버그는 **클라이언트 쪽에만** 있음.

**Fix 적용 완료 — 이슈 B(온기동)와 이슈 F를 한 번에 해결하는 공통 우회, "재시작해서 항상 냉기동처럼 접속":**
- `Assets/Scripts/Network/NetworkManagerSetup.cs`: 프로세스 전역 플래그 `HasConnectedAsClientSteamThisProcess` 추가 — `StartClientSteam()` 성공 시 `true`로 세팅. "이 프로세스에서 Client로 접속한 게 몇 번째 시도인지" 추적용.
- `Assets/Scripts/UI/TitleMenuController.cs`: 모든 클라이언트 접속 진입점(웜 인바이트 수락 + 수동 룸코드 입장)이 공통으로 지나가는 `JoinGameSteamAsync()` 맨 앞에 `TryRestartForWarmReconnect(lobbyId)` 체크 추가. `HasConnectedAsClientSteamThisProcess`가 이미 `true`면(=2번째 이상 시도) 인프로세스 접속 대신 현재 실행 파일을 `+connect_lobby <lobbyId>` 인자로 재실행하고 `Application.Quit()`. 새 프로세스는 기존에 이미 있던 냉기동 경로(`TryAutoJoinFromLaunchArgs`)를 그대로 타서 정상 접속.
- 호스트 쪽은 건드리지 않음 — 재호스팅은 이 버그의 영향을 받지 않는 것으로 확인됨(위 참고).

**Verify (다음 세션):**
1. 방장이 방 폭파 → 재생성 후, 클라이언트가 **두 번째로** 초대 수락 시 화면이 잠깐 꺼지고 재시작되며 자동으로 그 로비에 들어가는지.
2. 재시작 후 접속한 클라이언트가 정상적으로 슬롯에 보이는지, `Server Scene Handle already exist` 없이 깨끗하게 되는지.
3. 재시작 방식(`Process.Start` + `Application.Quit`)이 Steam 오버레이/AppID 인식과 충돌 없이 자연스러운지 — 문제 있으면 `steam://run/<appid>//+connect_lobby <id>` URI 방식으로 교체 검토.

**Files changed (이번 세션):** `Assets/Scripts/Network/LobbyNetworkManager.cs`(유령 슬롯 근본 수정), `Assets/Scripts/Network/NetworkManagerSetup.cs`(`HasConnectedAsClientSteamThisProcess` 플래그), `Assets/Scripts/UI/TitleMenuController.cs`(`TryRestartForWarmReconnect` 재시작 우회).

**Files read (이번 세션):** 계정 A/B `Player.log`(여러 라운드, 직접 읽음), `Assets/Scripts/Network/LobbyNetworkManager.cs`, `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/Network/SteamLobbyManager.cs`, `Assets/Scripts/Network/SteamManager.cs`, `Assets/Scripts/UI/TitleMenuController.cs`, `Library/PackageCache/com.community.netcode.transport.facepunch@27d3e825ecdd/Runtime/FacepunchTransport.cs`, NGO 패키지 소스(`NetworkConnectionManager.cs`, `NetworkSceneManager.cs`, `SceneEventData.cs`, `NetworkManagerHooks.cs` — `com.unity.netcode.gameobjects@02e4aaa4170c`), 웹 검색(NGO GitHub issue #2704/#239, Facepunch.Steamworks issue #217).

### 인수인계 요약 (2026-08-07 5차 세션 기준, 최신)

1. **이슈 A — 해결 확인 완료(유지).**
2. **이슈 B(온기동) — 프로세스 재시작 우회 구현 완료, 검증 대기.** 이슈 F와 동일 원인으로 통합됨.
3. **이슈 C — 보류(사용자 결정 유지).**
4. **이슈 D — 해결 확인 완료(유지, virtual port 방식).**
5. **이슈 E — 해결 확인 완료(유지).**
6. **이슈 F — 진짜 원인 확정(4차 세션 가설 기각). 이슈 B와 같은 프로세스 재시작 우회로 해결 시도 — 검증 대기.**
7. **유령 슬롯(구 이슈 A 재발) — `LobbyNetworkManager.OnClientJoined` 중복 방지 가드로 근본 수정 완료, 사용자 재테스트로 해결 확인됨.**
8. **다음 세션 액션:** 위 "Verify" 3개 항목 확인. 재시작 방식이 어색하거나 문제 있으면 `steam://run/` URI 방식 재검토.

---

## 트랙 5 — 2026-08-07 6차 세션: 이슈 B(온기동) 진짜 근본원인 확정·수정 완료·검증 통과 — **트랙 5 전 이슈 종료**

> **이 절이 최신 상태.** 5차 세션의 "프로세스 재시작 우회"(트랜스포트 중복 메시지 회피)는 유지되지만, 온기동 최초 진입 실패의 **진짜 원인은 그것과 무관한 별개 버그**였음이 이번 세션에 확정됨.

### 재현 (사용자 원본 보고)

1. 계정 B를 **새로 실행**해 타이틀 화면에 대기(최초 진입) → 계정 A가 Invite Overlay로 초대 → 알림은 뜨고 B가 Accept 눌렀는데 화면이 그대로 타이틀에 머묦(로비로 안 들어감).
2. B가 룸코드 직접 입력으로 방에 들어감(성공) → 방 폭파 → A가 방 재생성 후 다시 초대 → **이번엔 정상 합류됨.**

두 시도의 유일한 차이는 "B가 타이틀 씬에 몇 번째로 진입했는가"였음(둘 다 같은 프로세스, 같은 Steam 초기화 상태).

### Root cause — Unity Awake/OnEnable 실행 순서 비결정성

`TitleMenuController.OnEnable()`이 `SteamLobbyManager.Instance`를 참조해 `OnInviteAccepted` 이벤트를 구독하는데, `SteamLobbyManager.Instance`는 그 자신의 `Awake()`에서 세팅됨. Unity 공식 문서(Execution order of event functions, Unity 6000.3 Manual)가 명시하는 대로 **다른 GameObject 간의 `Awake`/`OnEnable` 순서는 비결정적**이라, `0.Title` 씬 최초 로드 시 `TitleMenuController.OnEnable()`이 `SteamLobbyManager.Awake()`보다 먼저 실행되면 `Instance == null`이라 구독이 **로그 한 줄 없이** 스킵됨. 이후 Steam이 정상적으로 `OnInviteAccepted`를 발행해도 구독자가 없어 이벤트가 소멸.

두 번째 이후 타이틀 진입(로비→타이틀 복귀)에서는 `SteamLobbyManager`가 이미 DDOL로 살아있어 `Instance`가 항상 non-null이므로 구독이 항상 성공 — 그래서 "최초 진입만 실패, 그 이후는 항상 성공"하는 패턴이 나왔던 것.

**Cause site:** `Assets/Scripts/UI/TitleMenuController.cs` → `OnEnable()` (구 99~103행), `SteamLobbyManager.Instance != null` 조건이 최초 씬 로드 타이밍에 false가 되는 경로.

### Fix 적용 완료

- `Assets/Scripts/UI/TitleMenuController.cs`: `_inviteSubscribed` 멱등 플래그 + `TrySubscribeInviteAccepted()` 헬퍼 신설. `OnEnable()`과 `Start()` 양쪽에서 호출 — `Start()`는 씬 내 모든 `Awake`/`OnEnable`이 끝난 뒤 보장되므로 `OnEnable()`에서 놓쳤어도 `Start()`에서 반드시 재시도·성공한다. 구독 성공/보류 시 각각 로그 추가(향후 같은 계열 문제 발생 시 즉시 판별 가능).
- `Assets/Scripts/Network/SteamLobbyManager.cs`: `HandleGameLobbyJoinRequested()`에 `OnInviteAccepted == null`(구독자 없음) 케이스 경고 로그 추가 — 이벤트가 소멸되는 상황을 더 이상 조용히 넘기지 않음.

### 검증 결과 — 사용자 확인 완료 (2026-08-07)

계정 B 최초 타이틀 진입 상태에서 A의 초대를 Accept → 정상 로비 합류 확인. **이슈 B(온기동) 해결.**

### 인수인계 요약 (2026-08-07 6차 세션 기준, 최종)

1. **이슈 A — 해결 확인 완료.**
2. **이슈 B — 냉기동·온기동 둘 다 해결 확인 완료.** (온기동 최초 진입 실패는 트랜스포트 문제가 아니라 이벤트 구독 타이밍 버그였음 — 5차 세션의 재시작 우회는 별개로 여전히 유효(2번째 이상 접속 시 트랜스포트 중복 메시지 회피용).)
3. **이슈 C — 보류(사용자 결정 유지).**
4. **이슈 D — 해결 확인 완료(virtual port 방식).**
5. **이슈 E — 해결 확인 완료.**
6. **이슈 F — 5차 세션 수정(프로세스 재시작 우회)으로 해결 시도 중이었고, 이번 세션 재현 시 별도 재발 보고 없음.**
7. **유령 슬롯(구 이슈 A 재발) — 해결 확인 완료.**
8. **⭐ 트랙 5 종료. 다음 작업은 `ReleaseRoadmap.md` §4 순위 3 "빌드 메타 정리 + 빌드 검수 즉시 제출"로 진행.**

**Files changed (이번 세션):** `Assets/Scripts/UI/TitleMenuController.cs`(`_inviteSubscribed`/`TrySubscribeInviteAccepted` 추가), `Assets/Scripts/Network/SteamLobbyManager.cs`(구독자 없음 경고 로그 추가).

**Files read (이번 세션):** `Assets/Scripts/UI/TitleMenuController.cs`, `Assets/Scripts/Network/SteamLobbyManager.cs`, `Assets/Scripts/Network/SteamManager.cs`, `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/Localization/GameLocalizationBootstrap.cs`, `Assets/Scenes/0.Title.unity`(스크립트 배치 순서), `ProjectSettings/MonoManager.asset`, Unity 6000.3 Execution order 공식 문서(웹).

---

## 트랙 6: 2026-08-12 — 4인 Depot 실사용 테스트에서 크래시 재발 + 온기동 재발 (다음 세션 인수인계, **컨텍스트 소진으로 미완료 상태에서 인계**)

> **⚠️ 이 절이 최신 상태. 아직 원인 미확정 — 코드 수정은 그래픽 API 변경(§ 아래) 하나만 적용됐고, 그것도 효과가 있는지 검증 실패 상태.** 다음 에이전트는 반드시 크래시 당사자(친구) PC의 실제 `Player.log`를 먼저 확보하고 진행할 것 — 추측 기반 재수정 금지(Bug Hunter 원칙).

### 재현 보고 (사용자 원본, 시간순)

1. 정식(Release, Development Build 체크 해제) 빌드를 Steam에 업로드 후 친구 4명과 실사용 테스트 진행. **Invite Overlay로 초대**, **로비 진입 후(after_lobby) 크래시**, **재현성: 고정(매번 동일)**.
2. **4명 중 2명이 크래시, 본인 + 친구 1명은 정상.**
3. 친구들이 마이크를 뽑고 재접속 테스트 — **크래시 동일하게 재현** (~~Vosk/마이크 네이티브 크래시 가설 배제~~ — **⚠️ 8차 세션에서 이 배제가 오류로 판명. 모델 로드·`vosk_model_find_word`·`VoskRecognizer` 생성은 전부 마이크 유무와 무관하게 로비 진입만으로 실행된다. 실제 원인이 바로 Vosk였다.**).
4. 본인(호스트) PC의 `Player.log`에서 크래시 직전 로그 확보 — 아래 "1차 진단" 참고.
5. 그래픽 API를 Direct3D 11로 전환 후 재빌드/재업로드, 친구가 재테스트 — **"안돼. 그렇게 설정했는데도 똑같대."(사용자 원문) — 여전히 크래시 재현 보고.**
6. 별개로, **Steam 친구 초대 "온기동"이 다시 제대로 안 된다는 보고** — 트랙 5 6차 세션에서 "해결 확인 완료"로 종료 처리했던 바로 그 이슈(§ 위 "트랙 5 — 6차 세션" 참고)의 재발 가능성.

### 크래시 — 1차 진단: D3D12 디바이스 제거 크래시 (호스트 PC에서 발견, **친구 PC 로그로는 아직 미확인**)

**증거 (호스트 본인 `Player.log`, 이번 세션에 직접 읽음):**
```
[D3D12 Device Filter] Vendor Name: NVIDIA
[D3D12 Device Filter] Device Name: NVIDIA GeForce RTX 3060
...
d3d12: swapchain present failed (887a0001)
Unrecoverable D3D12 device error!
```
`ProjectSettings/ProjectSettings.asset`의 Windows Graphics API 설정이 당시 **Auto**(자동 선택)였어서 Unity 6이 D3D12를 우선 선택했고, 로비 진입 후 D3D12 디바이스가 제거(`DXGI_ERROR_INVALID_CALL`, device removed)되며 크래시하는 것으로 추정됨. 이 패턴은 Unity 6 D3D12 백엔드가 Steam Overlay 또는 특정 GPU 드라이버 조합과 충돌하는 알려진 문제군과 일치.

**적용한 조치 (완료):** Player Settings > Windows Graphics API를 **Direct3D 11 단일 항목으로 명시 고정**(Auto 해제). 실제 설정 파일로 확인됨:
```550:552:ProjectSettings/ProjectSettings.asset
  - m_BuildTarget: WindowsStandaloneSupport
    m_APIs: 02000000
    m_Automatic: 0
```
(`m_APIs: 02000000` = `GraphicsDeviceType.Direct3D11`(값 2) 단일 항목, `m_Automatic: 0`으로 자동 선택 꺼짐 — 코드 수정 아니라 Player Settings에서 사용자가 직접 변경한 것.)

**재빌드 후 호스트 본인 PC 검증(이번 세션 확인):** `Player.log` 최신본과 `Player-prev.log` 둘 다 `Direct3D 11.0 [level 11.1]` 사용 확인, 두 세션 모두 크래시 없이 `HostQuitRoom`/`LobbyQuit` 경로로 **정상 종료**(로그 마지막까지 정상). **즉, D3D11 전환 이후 호스트 자신의 PC에서는 크래시가 재현되지 않았음.**

**그런데 사용자는 "친구가 재테스트해도 여전히 팅긴다"고 보고 — 이 지점에서 컨텍스트 소진.** 다음 두 가능성이 미구분 상태:
1. **진짜 원인이 D3D12가 아니었다** (다른 크래시 원인이 있고 D3D12는 호스트 PC에만 우연히 해당).
2. **친구가 테스트한 게 실제로는 D3D11 반영 전 구빌드였다** (Depot 재업로드/Set Live가 친구 쪽에 아직 전파 안 됨, 또는 재업로드 자체를 아직 안 함) — 이 경우 "D3D11도 안 됨"이라는 결론 자체가 틀린 데이터일 수 있음.

**다음 에이전트 최우선 액션 — 확정 없이 넘어가지 말 것:**
1. **친구가 테스트한 빌드가 실제로 D3D11 반영본인지 먼저 확인** (Depot BuildID / Set Live 시각 vs 친구가 마지막으로 다운로드/업데이트한 시각 대조. Steam은 클라이언트가 자동 업데이트 받게 되어 있지만 확인 필요).
2. **크래시하는 친구 본인 PC의 `Player.log`를 반드시 확보할 것** — 지금까지 확보한 로그는 전부 호스트(크래시 안 나는 쪽) 것뿐이라 실제 크래시 스택/에러 코드를 한 번도 못 봤음. 경로: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Kkul-tteok!\Player.log`(친구 PC에서 직접 열어서 크래시 직전 20~30줄 + 파일 마지막 줄 캡처 요청). `Player-prev.log`도 같이(현재 세션이 아니라 크래시났던 그 세션 로그일 수 있음).
3. 위 로그 없이는 그래픽 API 외 다른 원인(GPU 드라이버 상이, 특정 하드웨어 스펙 부족, 다른 네이티브 플러그인 등)을 추측하지 말 것 — 로그로 확정 후 진행.

### 온기동 재발 보고 — 회귀 여부 미확인, 재현 정보 필요

트랙 5 6차 세션에서 "이슈 B(온기동) — `TitleMenuController.OnEnable()`의 `SteamLobbyManager.Instance` null 참조로 구독 스킵" 문제를 `_inviteSubscribed`/`TrySubscribeInviteAccepted()`(`OnEnable` + `Start()` 이중 시도)로 근본 수정 완료, 사용자 검증까지 통과했던 이슈. 이번 4인 테스트에서 사용자가 "온기동도 제대로 안 됨"이라고 재차 보고했으나, **이번 세션엔 재현 조건(누가·몇 번째 진입에서·냉기동/온기동 중 어느 쪽인지)을 아직 못 받음.**

**다음 에이전트 액션:**
1. 정확히 어떤 증상인지부터 재확인 필요 — 트랙 5에서 구분했던 두 케이스 그대로 물어볼 것: (a) 게임이 이미 타이틀에 떠 있는 상태에서 초대 Accept해도 로비로 안 들어가는지(온기동), (b) 게임이 꺼진 상태에서 초대 Accept로 새로 켜졌는데 로비로 안 들어가는지(냉기동). 트랙 5 6차 세션 수정은 (a) 전용이었고, 5차 세션 재시작 우회는 "2번째 이상 접속 시" 트랜스포트 중복 메시지 회피용이었음 — 어느 경로가 깨졌는지 증상만 봐도 좁혀짐.
2. 재현되면 동일한 로그 필터로 캡처 요청:
   ```powershell
   Select-String -Path "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\Kkul-tteok!\Player.log" -Pattern "\[NetworkManagerSetup\]|\[SteamLobbyManager\]|\[TitleMenuController\]|\[SteamManager\]" | Select-Object -ExpandProperty Line
   ```
   `[SteamLobbyManager] 초대 수락 감지` 로그 유무, `[TitleMenuController]` 구독 성공/보류 로그(6차 세션에서 추가한 것) 유무로 판별.
3. **가능성 있는 새 변수:** 이번엔 4인 테스트(트랙 5는 2인)라서, 트랙 5에서 검증 안 된 "3번째 이상 인원이 초대받는 경로"나 "이미 2명 있는 방에 3/4번째가 온기동으로 합류" 같은 트랙 5 미검증 조합일 수 있음 — 재현 시 몇 번째 인원인지도 같이 확인.

### 인수인계 요약 (2026-08-12, 트랙 6 — 다음 세션 필수 확인)

1. **크래시 — 원인 미확정.** D3D12 디바이스 제거 크래시는 호스트 PC 로그에서만 확인됨, D3D11 전환은 호스트 PC에서는 효과 확인(크래시 재현 안 됨)했으나 친구 PC 재테스트에서 "여전히 크래시" 보고 — 친구가 실제로 D3D11 빌드를 테스트했는지 미확인 상태. **크래시하는 친구 PC의 `Player.log` 확보가 최우선.**
2. **온기동 — 회귀 여부 미확인.** 트랙 5 6차 세션에서 해결 확인까지 했던 이슈라 재현 조건(냉기동/온기동 구분, 몇 번째 인원)부터 다시 받아야 함.
3. 이번 세션에 코드 변경 없음 — 그래픽 API 변경은 Player Settings(에디터)에서 사용자가 직접 적용한 것, 에이전트의 파일 수정 없음.
4. 컨텍스트 소진으로 이 세션 종료 — 다음 에이전트는 위 "다음 에이전트 최우선 액션" 두 항목(친구 PC 로그, 온기동 재현 조건)부터 확보한 뒤 진단을 이어갈 것.

**Files read (이번 세션):** 호스트 본인 `Player.log`/`Player-prev.log`(2회, D3D12 크래시 버전 + D3D11 전환 후 버전), `ProjectSettings/ProjectSettings.asset`(Graphics API 설정), `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/Network/SteamManager.cs`, `Assets/Scripts/Network/SteamLobbyManager.cs`, `Assets/Scripts/UI/TitleMenuController.cs`, `Assets/Scripts/Network/PlayerSpawnManager.cs`, `Assets/Scripts/GameSession.cs`, `Assets/Scripts/Cheer/CheerKeywordEngine.cs`, `Assets/Scripts/Cheer/VoskModelLoader.cs`, `Assets/Scripts/UI/LobbyMenuController.cs`, `Assets/Docs/NetworkDesign.md`.

---

## 트랙 6 — 7차 세션 (2026-08-12 밤): 크래시 원인 확정(D3D12) + 온기동 재발 근본원인 확정·수정 완료

> **⚠️ 이 절의 크래시 결론은 8차 세션(맨 아래)에서 정정됨 — 절반만 옳았다.** 아래 "D3D12 원인 확정"은 **호스트 PC의 "실행 직후" 크래시**에만 해당한다. 친구의 **"로비 진입 후" 크래시는 별개 원인**(비ASCII 경로 + libvosk 모델 로드 실패)이었고, 아래 "친구가 구빌드를 테스트했다"는 타임라인 추론은 **사용자 정정으로 폐기됐다** — 친구는 D3D11 빌드에서도 크래시했다. 온기동 수정 내용은 유효하다.

### 결정적 방법론 — 네이티브 크래시는 `Player.log`가 아니라 크래시 덤프 폴더

6차 세션까지 "친구 `Player.log`를 못 받아서 확정 불가"로 막혀 있었으나, 사용자가 보낸 스크린샷의 창이 **`UnityCrashHandler64`의 크래시 리포터**임을 식별한 것이 돌파점이었다. 네이티브 크래시는 아래 경로에 크래시별 폴더로 **보존**된다.

```
%LOCALAPPDATA%\Temp\DefaultCompany\Kkul-tteok!\Crashes\Crash_<yyyy-MM-dd_HHmmssfff>\
  ├─ crash.dmp
  └─ Player.log   ← 그 세션 전용 스냅샷 (덮어써지지 않음)
```

호스트 본인 PC에서 이 폴더를 열자 **덤프 8건**이 그대로 남아 있었다. `Player.log`는 재실행 시 덮어써지지만 이 폴더는 남으므로, **테스트가 끝난 뒤에도 증거 확보가 가능하다.** 앞으로 크래시 보고를 받으면 이 폴더를 먼저 요청할 것.

### 크래시 — 원인 확정: D3D12 디바이스 제거 (`DXGI_ERROR_INVALID_CALL`)

**증거:** 크래시 덤프 8건(21:13~22:09) 전부 동일 패턴. `Direct3D 12 [level 12.2]`로 초기화된 뒤:

```
d3d12: swapchain present failed (887a0001).
d3d12: Device failed error (887a0001).
Unrecoverable D3D12 device error!
Crash!!!
→ 스택 전부 UnityPlayer 렌더 스레드
```

`887A0001` = `DXGI_ERROR_INVALID_CALL`. 8건 모두 매니지드 로그가 **한 줄도 없다** — 즉 첫 씬 로드 이전, 실행 직후 첫 present에서 죽었다(6차 세션에 적힌 "로비 진입 후"와 다름 — 친구 쪽 크래시 타이밍은 별개일 수 있으나 원인 계열은 동일). 로드된 모듈에 Steam 오버레이(`gameoverlayrenderer64.dll`) + NVIDIA 캡처 훅(`nvspcap64.dll`)이 함께 주입되어 있어, D3D12 + 오버레이 훅 조합에서 디바이스가 제거되는 알려진 패턴과 일치. 4명 중 2명만 크래시한 것도 GPU/드라이버/오버레이 조합 차이로 설명된다.

**⭐ "D3D11에서도 팅긴다"는 6차 세션 보고는 잘못된 데이터 — 타임라인으로 증명:**

| 시각 (2026-08-12) | 사건 |
|---|---|
| 21:13 ~ 22:09 | 크래시 8건 — 전부 **D3D12** (Steam 설치 경로에서 실행) |
| 22:15:18 | `ProjectSettings.asset` 수정 = D3D11 고정이 실제로 적용된 시각 |
| 22:15:39 | ContentBuilder `content\KKUL-TTEOK!_Build\`에 새 빌드 복사 |
| 22:19:04 | Steam 설치본 갱신 (buildid **`24693223`**) |
| 22:22 / 22:24 | D3D11로 두 세션 정상 — **크래시 없음** |

**D3D11이 포함된 빌드가 처음 존재한 시각이 22:15:39**이고, 친구 4인 테스트는 그 이전에 이미 종료됐다. 즉 친구는 D3D11 빌드를 받아본 적이 없다. 6차 세션 문서가 남겨둔 두 갈래 중 **"가능성 2(친구가 구빌드 테스트)"가 정답**이었다.

**Cause site:** 코드 아님 — `ProjectSettings/ProjectSettings.asset` 550~552행(`m_APIs: 02000000` = Direct3D11 단일, `m_Automatic: 0`). 현재 올바르게 적용되어 있음.

**조치:** 코드 변경 없음. **남은 것은 친구 재검증뿐.** 확인 순서: ① 친구가 실행 후 `Player.log` 상단에 `Direct3D 11.0`이 찍히는지(D3D12면 아직 구빌드 = 결과 무효) ② 그래도 크래시하면 위 `Crashes` 폴더를 압축해서 받을 것.

### 온기동 재발 — 근본원인 확정·수정 완료 (트랙 5 6차 세션 수정의 회귀 아님)

**Reproduce (로그 확정):** 본인 PC `Player-prev.log`에 그 순간이 그대로 남아 있었다.

```
[LobbyMenuController] 룸코드 복사됨: 109775241869821632
[SteamLobbyManager] 초대 수락 감지 — Lobby 109775241869822409, 초대자 76561198662605787
                    — 그러나 OnInviteAccepted 구독자 없음(이벤트 소멸됨).
```

계정 A가 **자기 방을 만들어 로비 씬에 있는 상태**에서 초대를 수락 → Steam 콜백은 정상 도착했으나 구독자가 없어 버려짐. 이후 A가 방을 나가고 앱을 완전히 종료한 뒤 같은 lobbyId로 초대를 재수락하니 `+connect_lobby` 냉기동 경로로 정상 입장(최신 `Player.log`).

**Root cause:** 초대를 처리하는 주체가 `TitleMenuController`, 즉 **타이틀 씬에만 존재하는 UI 컴포넌트**였다. 이벤트 발행자 `SteamLobbyManager`는 DDOL이라 어디서든 콜백을 받지만, 로비 씬으로 넘어가면 `TitleMenuController`가 파괴되며 `OnDisable()`에서 구독을 해제한다. 결과: **타이틀 화면에 있을 때만 초대가 동작하고, 방을 만들어 로비에 있거나 인게임 중이면 100% 무시.** 트랙 5 6차 세션에서 고친 "Awake/OnEnable 순서로 최초 진입 구독 누락"은 회귀하지 않았고(`Start()` 구독 유지, 정상 동작 중), 이번 건은 그때 다루지 않은 별개 케이스다. 4인 테스트에서 잘 재현된 이유는 인원이 늘면 "각자 방을 만들어 놓고 서로 초대를 주고받는" 상황이 훨씬 자주 생기기 때문.

**Cause site:** `Assets/Scripts/UI/TitleMenuController.cs` `Start()`(구독) / `OnDisable()`(해제), `Assets/Scripts/Network/SteamLobbyManager.cs` `HandleGameLobbyJoinRequested()`(구독자 없으면 경고만 남기고 return).

**확정 정책 (사용자 결정, 2026-08-12 7차 세션):**
- **로비에 있을 때만** 자동 이동(현재 방 정리 → 초대받은 방으로). **인게임 중에는 무시.**
- **Host여도 확인 팝업 없이 바로 허용** — 방 폭파는 기존 §12 정책 그대로.

**Fix 적용 완료 (3파일):**
- `Assets/Scripts/Network/NetworkManagerSetup.cs`: `public static bool RestartWithConnectLobby(SteamId)` 신설 — `TitleMenuController`의 private static 안에 갇혀 있던 `+connect_lobby` 재실행 로직을 공용으로 승격.
- `Assets/Scripts/UI/TitleMenuController.cs`: `TryRestartForWarmReconnect`가 위 공용 메서드를 호출하도록 변경(동작 동일, 중복 제거).
- `Assets/Scripts/UI/LobbyMenuController.cs`: `SteamLobbyManager.OnInviteAccepted`를 기존 `SubscribeAll()`/`UnsubscribeAll()` 짝에 추가(`_inviteSubscribed`, Steam 경로에서만). 핸들러 `OnSteamInviteAccepted` → 코루틴 `MoveToInvitedLobby`: Host면 `NotifyHostQuit()`으로 남은 인원에게 먼저 알리고 → 한 프레임 양보(ClientRpc 전송 보장) → `NetworkManagerSetup.Shutdown()`(Steam Lobby leave 포함) → `RestartWithConnectLobby()`. 재실행 실패 시 `TitleReturnFlow`로 타이틀 복귀 폴백. `_invitePending`으로 중복 수락 차단.

**이동 수단으로 프로세스 재실행을 택한 이유:** 5차 세션에서 실사용 검증된 유일한 경로이고, 세션 정리와 Steam 릴레이 상태 초기화가 동시에 해결된다. 인프로세스로 방을 옮기면 이슈 F(`Server Scene Handle already exist`) 조건에 그대로 들어간다.

**정책이 코드 구조로 보장되는 지점:** 구독자가 로비 씬에만 존재하므로, 인게임에서는 기존과 동일하게 `OnInviteAccepted` 구독자 없음 경고만 남고 무시된다 — 별도 씬 판정 분기가 필요 없다.

**Verify (다음 세션 / 사용자):** 재빌드 + Depot 업로드 후 계정 B(노트북)로 2인 재현 — ① A가 방을 만들어 로비에 있는 상태에서 B가 초대 → A가 초대받은 방으로 이동하는지 ② 회귀 확인: 타이틀에서 초대 수락(온기동) ③ 회귀 확인: 게임 꺼진 상태에서 초대 수락(냉기동).

### 부수 발견 (미수정, 조건만 남아 있음) — `TryRestartForWarmReconnect` 가드 범위

`TryRestartForWarmReconnect`는 `HasConnectedAsClientSteamThisProcess`, 즉 "이 프로세스에서 **Client로** 접속한 적 있는지"만 본다. 따라서 **Host를 했다가 방을 폭파하고 같은 프로세스에서 Client로 처음 접속하는 경로는 우회가 발동하지 않는다** — 4차 세션에서 이슈 F(`Server Scene Handle already exist`)를 재현했던 조건과 정확히 같다. 4인 플레이의 "방장이 방을 닫고 다른 사람이 방장을 맡아 재모집" 패턴에서 걸릴 수 있다. 이번 테스트에서 해당 증상 보고가 없었으므로 **미확정 리스크로만 기록하고 수정하지 않음.** 재현되면 가드를 "이 프로세스에서 Steam 세션(Host든 Client든)을 한 번이라도 시작한 적 있는지"로 넓히는 것이 1순위 후보.

### 인수인계 요약 (2026-08-12 7차 세션 기준, 최신)

1. **크래시 — 원인 확정(D3D12 디바이스 제거). 코드 변경 없음.** 현재 Depot의 `24693223`(D3D11) 빌드로 **친구 재검증만 남음.** 검증 시 `Direct3D 11.0` 로그 확인이 선행 조건 — 확인 없이 결과를 신뢰하지 말 것(6차 세션이 그래서 틀렸음).
2. **온기동 — 원인 확정·수정 완료(3파일). 재빌드 후 위 Verify 3항목 검증 필요.**
3. **`TryRestartForWarmReconnect` 가드 범위 — 미수정 리스크로 기록.** 재현 시에만 착수.
4. 크래시 보고를 받을 때는 항상 `Crashes` 폴더(덤프+세션 로그)를 먼저 요청할 것 — `Player.log`만으로는 네이티브 크래시를 못 본다.

**Files changed (이번 세션):** `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/UI/TitleMenuController.cs`, `Assets/Scripts/UI/LobbyMenuController.cs`, `Assets/Docs/SteamworksIntegrationDesign.md`.

**Files read (이번 세션):** 크래시 덤프 8건의 `Player.log`(`%LOCALAPPDATA%\Temp\DefaultCompany\Kkul-tteok!\Crashes\`), `Player.log` / `Player-prev.log`(최신), `ProjectSettings/ProjectSettings.asset`(타임스탬프), `appmanifest_5029890.acf`(buildid/LastUpdated), `Assets/Scripts/UI/TitleMenuController.cs`, `Assets/Scripts/Network/SteamLobbyManager.cs`, `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/UI/LobbyMenuController.cs`, `Assets/Docs/NetworkDesign.md`(M/T 분류 grep — `TitleMenuController`/`SteamLobbyManager`는 `0.Title` 전용이라 M/T 라운드 분류 대상 아님).

---

## 트랙 6 — 8차 세션 (2026-08-13): 크래시 진짜 원인 확정 — 비ASCII(한글) 경로 + libvosk 모델 로드 실패 (**재현 성공 · 수정 완료**)

> **이 절이 최신 상태.** 7차 세션의 "크래시 원인 확정 = D3D12" 결론은 **절반만 옳았다.** D3D12는 **호스트 PC의 "실행 직후" 크래시**만 설명하고, 친구의 **"로비 진입 후" 크래시는 완전히 다른 원인**이었다.

### 7차 세션 오진 정정 — 서로 다른 두 크래시를 하나로 묶어 봤다

7차 세션은 타임라인 분석으로 "친구는 D3D11 빌드를 받아본 적이 없다"고 결론했으나, **사용자가 직접 정정했다: 친구는 D3D11 전환 후에도 테스트했고 동일하게 크래시했다.** 이 정정을 받아들이면 크래시가 두 종류였음이 드러난다.

| | 호스트(본인) PC | 친구 PC |
|---|---|---|
| 타이밍 | **실행 직후** (매니지드 로그 0줄) | **로비 진입 후** |
| 원인 | D3D12 디바이스 제거 (`DXGI_ERROR_INVALID_CALL`) | **libvosk 모델 로드 실패 → 네이티브 즉사** |
| 조치 | D3D11 고정 (유효 — 유지) | 이 절의 수정 |

**교훈 1 — 크래시 타이밍이 다르면 다른 버그로 취급할 것.** 확보한 증거(덤프 8건)는 전부 호스트 PC 것이었는데 그걸로 친구 크래시까지 설명하려 했다. 7차 세션은 덤프의 "실행 직후"가 6차 세션 기록의 "로비 진입 후"와 어긋나는 것을 **인지하고 문장으로 남겨두기까지 했으면서도** 같은 원인으로 묶었다.

**교훈 2 — 6차 세션의 "마이크를 뽑아도 재현되니 Vosk/마이크 네이티브 크래시 가설 배제"는 잘못된 추론이었다.** 모델 압축 해제 · `new Model()` · `vosk_model_find_word` · `VoskRecognizer` 생성은 **전부 마이크 유무와 무관하게 로비 진입만으로 실행된다.** 마이크 제거로는 Vosk 가설을 배제할 수 없다. 이 오배제가 6→7차 세션 내내 정답을 후보에서 지워놨다.

### 진짜 원인 — 비ASCII 경로에서 libvosk가 모델을 열지 못한다

**기존 구조:** 130MB `vosk-model-en-us-0.22-lgraph.zip`을 StreamingAssets에 넣고, 로비 `Start()`에서 `Application.persistentDataPath`(= `C:\Users\<사용자명>\AppData\LocalLow\DefaultCompany\Kkul-tteok!`)로 압축 해제(해제 후 204MB)한 뒤 그 경로로 `new Model()`.

**결함 1 — 비ASCII 경로를 못 읽는다.** libvosk 내부 Kaldi는 `std::ifstream`으로 모델 파일을 여는데, Windows에서 이는 비ASCII 경로를 열지 못한다. C#은 경로를 UTF-8 바이트로 넘기고 CRT는 시스템 코드페이지(한국어 = CP949)로 해석하므로 바이트가 어긋나 "존재하지 않는 경로"가 된다. **Windows 사용자명이 한글인 플레이어는 모델 로드가 100% 실패한다.** 업스트림 미해결: [alphacep/vosk-api#1072](https://github.com/alphacep/vosk-api/issues/1072) — "Kaldi를 고쳐야 한다" 상태로 정체, 어떤 마샬링을 써도 크래시한다는 보고까지 달려 있다. **우리가 우회하는 수밖에 없다.**

**결함 2 — 실패가 감지되지 않는다.** `VoskPINVOKE.new_Model`은 실패 시 예외 없이 `IntPtr.Zero`를 반환하고(SWIG 예외 체크 코드가 없다), 래퍼 `new Model(path)`는 그 NULL을 그대로 감싼 **C# 객체**를 돌려준다. 따라서 `model != null` 체크는 무의미하다. 그 객체로 네이티브를 호출하는 순간 프로세스가 즉사한다(access violation). 호출 지점 두 곳:

- `CheerLexiconBuilder.IsKnownWord` → `model.vosk_model_find_word(...)` — **로비 슬롯 갱신 경로에서 동기 호출** = 로비 진입 직후 첫 사망 지점
- `CheerKeywordEngine.WorkerLoop` → `new VoskRecognizer(m, ...)` — 워커 스레드

동일 사례: [alphacep/vosk-api#955](https://github.com/alphacep/vosk-api/issues/955) — `new VoskRecognizer` 라인에서 `0xC0000005`, 메인테이너 답변도 "모델이 제대로 초기화되지 않았다".

**결함 3 — 손상된 모델이 영구 고착된다.** `EnsureModel()`이 `Directory.Exists`만 확인했다. 압축 해제가 중간에 실패하면 폴더는 남고 내용은 불완전한데, 다음 실행부터 "모델 이미 존재"로 통과한다. `ExtractExistingFileAction.DoNotOverwrite`라 자가 복구도 안 된다 → **매번 같은 지점에서 죽는다**(보고된 "재현성: 고정"과 일치).

**모든 관측치가 설명된다:** 로비 진입 후 타이밍(= 모델 최초 해제·로드 시점) / 4명 중 2명(= 한글 사용자명 여부) / 마이크 무관 / 재현성 고정(= 사용자명은 안 바뀐다) / 호스트 본인은 무사(사용자명 `u` = ASCII).

### 재현 성공 (2026-08-13, 에디터)

사용자 계정을 건드리지 않고 **`companyName`만 한글로 바꿔** `persistentDataPath`에 한글을 주입하는 방법으로 재현했다. 압축 해제 실패 변수를 배제하려고 정상 모델 폴더(17파일 / 204.1MB)를 한글 경로에 미리 복사한 뒤 테스트.

1. Player Settings → Company Name: `DefaultCompany` → `한글테스트`
2. 정상 모델을 `%USERPROFILE%\AppData\LocalLow\한글테스트\Kkul-tteok!\vosk-model-en-us-0.22-lgraph`에 복사
3. 에디터 Play → 로비 진입 → **에디터 프로세스 즉사** = 원인 확정 (사용자 확인: "맞다 찾았다. 이거다. 정확해.")

> **재현 도구로 재사용 가능:** 비ASCII 경로 관련 네이티브 이슈는 `companyName`을 한글로 바꾸면 계정을 건드리지 않고 재현할 수 있다. 테스트 후 원복 필수(`PlayerPrefs` 경로가 함께 바뀌어 설정이 초기화된 것처럼 보인다).

### 수정 — 모델을 StreamingAssets에 풀린 상태로 배포 (런타임 압축 해제 제거)

**배포 변경 (사용자 작업 완료·검증됨):** `Assets/StreamingAssets/vosk-model-en-us-0.22-lgraph/` 폴더로 모델을 그대로 포함하고 `vosk-model-en-us-0.22-lgraph.zip`은 삭제. 실측 확인: 17파일 204.1MB, 필수 파일 10개 크기 일치, 폴더 중첩 없음.

- **모델 경로가 Steam 설치 경로가 되어 사용자명과 무관해진다** → 근본 원인 소멸 (`...\steamapps\common\KKUL-TTEOK!\Kkul-tteok!_Data\StreamingAssets\...`)
- 런타임 압축 해제 소멸 → 로비 진입 시 수십 초 메인 스레드 정지 제거 + 해제 실패 경로(결함 3) 제거
- 플레이어 디스크 사용량 334MB(zip 130 + 해제본 204) → **204MB**. 설치 용량은 순증 74MB (Steam 전송은 압축되므로 다운로드 증가는 더 작다)
- zip이 애초에 있었던 이유는 Vosk 공식 유니티 예제가 **Android**(StreamingAssets가 APK 내부라 파일 경로 접근 불가)를 전제로 하기 때문 — **PC 전용인 이 프로젝트에는 해당되지 않는다.** 예제를 그대로 따라온 것이 원인의 뿌리였다
- `.gitignore`에 `Assets/StreamingAssets/vosk-model-en-us-0.22-lgraph/` 추가 (204MB 커밋 방지). 폴더 `.meta`만 추적된다

**코드 변경 — `Assets/Scripts/Cheer/VoskModelLoader.cs` (재작성):**

| 항목 | 내용 |
|---|---|
| 압축 해제 | **제거** (`Ionic.Zip` 의존 제거, `EnsureModel()` 삭제 — 외부 호출처 없었음) |
| 무결성 검증 | 필수 파일 10개의 **존재 + 최소 크기** 확인(`ValidateModel`). 폴더 존재만으로 판단하지 않음 |
| 비ASCII 방어 | 경로에 비ASCII가 섞이면 `C:\Users\Public\Documents\KkulTteok`(항상 ASCII·관리자 권한 불필요)로 복사 후 사용. 폴백도 비ASCII면 포기 |
| **네이티브 실패 감지** | `Model.getCPtr(model).Handle == IntPtr.Zero` 확인 → NULL이면 `_sharedModel` 미설정. Vosk에 `.asmdef`가 없어 `internal getCPtr`을 같은 어셈블리에서 접근 가능 |
| 실패 시 동작 | **음성 인식만 비활성화, 게임 진행은 정상.** `_loadAttempted`로 재시도 차단(로그 스팸·반복 크래시 방지) |

`CheerKeywordEngine`은 헤더 주석의 초기화 순서만 갱신(`EnsureModel` → `GetSharedModel`). `CheerLexiconBuilder`는 **변경 없음** — `model == null`이면 `true` 반환(경고 미표시)으로 이미 안전하고, 이제 유효한 모델만 전달된다.

**핵심 원칙 (신규):** 응원(음성 인식)은 부가 기능이다. **모델 로드 실패가 게임을 죽여서는 안 된다.** 이번 수정으로 실패는 `LogError` 한 줄 + 기능 비활성화로 끝난다.

M/T 분류: Cheer(음성 인식) 계열은 로비 + 전 스테이지 공통 서브시스템이라 M/T 라운드 분류 대상이 아니다.

### Verify (다음 세션 / 사용자)

1. 에디터 로비 진입 → `[VoskModelLoader] 공유 Model 로드 완료: .../StreamingAssets/vosk-model-en-us-0.22-lgraph` 확인, 압축 해제 로그가 사라졌는지
2. Company Name 원복(`DefaultCompany`) — 테스트 잔여물 `%USERPROFILE%\AppData\LocalLow\한글테스트`(204MB)와 구 해제본 `...\DefaultCompany\Kkul-tteok!\vosk-model-en-us-0.22-lgraph`(204MB) 삭제 가능
3. 재빌드 + Depot 업로드 → **한글 사용자명 친구**가 로비 진입 시 크래시 없는지 (**최종 확정 조건**)
4. 음성 인식이 실제로 동작하는지도 함께 확인 (경로 변경 회귀 여부)
5. 또 크래시하면 `Crashes` 폴더 + `Player.log`의 `[VoskModelLoader]` 라인 확인 — 이제 모델 실패는 크래시가 아니라 `LogError`로 남으므로, 크래시가 나면 **다른 원인**이다

### 남은 리스크 / 미검증

- **Steam 라이브러리 폴더를 한글 경로에 만든 플레이어**(예: `D:\게임\Steam`): 폴백 복사 경로로 처리되지만 실환경 미검증. `[VoskModelLoader] 설치 경로에 비ASCII 문자 포함` 경고가 그 신호다. **이 경우에도 크래시는 없다** — 복사 성공 시 음성 인식 정상, 복사 실패 시 음성 인식만 비활성화

#### 비ASCII 경로 대응 — 검토했으나 채택하지 않은 방안 (2026-08-13 결정: **현재 폴백 복사 유지**)

| 방안 | 결과 |
|---|---|
| **8.3 짧은 경로**(`GetShortPathName`) | **불가 — 실측으로 폐기.** `한글테스트` → `한글테~1`로 비ASCII가 그대로 남는다(Windows가 8.3 이름에도 코드페이지 표현 가능 문자를 유지). 추가로 `disable8dot3`이 볼륨별 설정(기본값 2)이라 8.3 생성 자체가 꺼져 있을 수도 있어 신뢰 불가 |
| **디렉터리 junction** | 가능. 204MB 복사 대신 ASCII 이름의 링크만 생성(디스크 0). 다만 여전히 "ASCII 경로를 매개"하는 우회이고, 해당 환경을 만들어 검증하기 어려워 **미채택** |
| **UTF-8 ACP 매니페스트** | **유일한 근본 해결.** Windows 1903+에서 exe 매니페스트에 `activeCodePage=UTF-8`을 선언하면 프로세스 코드페이지가 UTF-8이 되어 C#이 넘긴 경로와 CRT 해석이 일치한다. 그러나 Unity는 exe를 사전 컴파일해 배포하므로 `Create Visual Studio Solution` 빌드 → `WindowsPlayer.manifest` 수정 → **Visual Studio로 exe 재컴파일 → 매 빌드마다 exe 교체**가 필요하다. 교체를 잊으면 조용히 원상복귀된다. 또한 프로세스 전역 코드페이지 변경이라 Steam SDK/Dissonance 등 다른 네이티브 문자열 처리에 영향, MinGW 빌드인 libvosk가 프로세스 ACP를 따르는지도 실측 필요 → **9/1 일정 리스크가 이득보다 크다고 판단, 미채택** |

**판단 근거:** 발동 조건이 "Steam 라이브러리 폴더명을 직접 비ASCII로 지은 플레이어"로 극히 좁고, 발동해도 최악이 **크래시가 아니라 음성 인식 기능 하나 비활성화**다. 매 빌드에 수동 단계를 추가하는 리스크가 더 크다. 재고할 트리거: 실제로 이 경고 로그를 남긴 플레이어 사례가 보고되면 junction → UTF-8 매니페스트 순으로 검토.
- **libvosk 호출부를 새로 추가할 때**는 반드시 `GetSharedModel()`의 null 반환을 존중할 것. 핸들 검증은 로더에서 1회만 수행한다
- **D3D11 고정은 유지한다.** 호스트 PC의 실행 직후 D3D12 크래시는 실재했고 덤프 8건으로 증명됐다 — 이번 발견이 그 결론을 뒤집지는 않는다
- 7차 세션에서 수정한 **온기동(로비에서 초대 수락) 검증은 여전히 미완료** — 위 7차 절의 Verify 3항목 그대로 남아 있다

**Files changed (이번 세션):** `Assets/Scripts/Cheer/VoskModelLoader.cs`(재작성), `Assets/Scripts/Cheer/CheerKeywordEngine.cs`(헤더 주석), `.gitignore`, `Assets/Docs/SteamworksIntegrationDesign.md`.

**Files read (이번 세션):** `Assets/Scripts/Cheer/VoskModelLoader.cs`, `Assets/Scripts/Cheer/CheerKeywordEngine.cs`, `Assets/Scripts/Cheer/CheerLexiconBuilder.cs`, `Assets/ThirdParty/Vosk/Model.cs`, `Assets/ThirdParty/Vosk/VoskPINVOKE.cs`, `Assets/Scripts/UI/LobbyMenuController.cs`, 크래시 덤프 `Player.log`(스택·로드된 모듈 목록), 빌드 산출물 `Plugins/x86_64` + `StreamingAssets` 실측, `ProjectSettings/ProjectSettings.asset`(company/product name), `Assets/Docs/CheerAndTutorialDesign.md`(모델 배포 방식 grep).

---

## 트랙 6 — 9차 세션 (2026-08-13 밤): "공개 방 목록" 방향 논의 → 보류 + 접속 실패 진단 계측 추가 (코드 수정 없음, 다음 세션 로그 대기)

> **이 절이 최신 상태.** 이번 세션은 **버그를 고치지 않았다** — 확정 진단에 필요한 로그가 아직 없어서 Bug Hunter 원칙상 추정 수정을 하지 않고, 대신 다음 재현에서 확정 진단이 가능하도록 진단 로그/워치독만 추가했다.

### 재현 보고 (사용자 원본)

4명과 실사용 테스트 진행 중(3-4인은 이제 불가, 본인 계정 + 부계정 B 2인으로 재테스트 예정):
1. **온기동(타이틀 화면에 대기 중인 상태에서 Invite Overlay로 초대) — 여전히 실패.** 트랙 5 6차 세션·트랙 6 7차 세션에서 각각 다른 원인으로 "해결 확인"까지 했던 바로 그 증상이 재발.
2. 친구들 사이에서 "Steam 친구만 초대할 수 있는 구조가 불편하다 — 방을 아예 목록으로 보여주고 아무나(친구 아니어도) 골라서 코드 입력 후 입장하게 해달라"는 요청 → **"공개 방 목록(Public Lobby List)" 신규 기능 아이디어**로 논의 시작.
3. 논의 중 사용자가 결정적 정보 제공: **"코드를 직접 입력해서 들어가는 것도 초대랑 똑같이 로비로 안 들어가고 타이틀에 그대로 머문다."**

### 핵심 발견 — "공개 방 목록"은 이 버그의 우회책이 될 수 없다

코드 추적 결과, 아래 3개 진입 경로가 전부 **정확히 같은 함수**로 합류한다:

```
Invite Overlay 수락   → TitleMenuController.OnSteamInviteAccepted(lobbyId)
코드 직접 입력        → TitleMenuController.ConfirmJoinSteam(code)
(구상 중이던) 방 목록 클릭 → 결국 같은 lobbyId를 얻어 동일 지점으로 합류
                     ↓ (셋 다 동일)
              TitleMenuController.JoinGameSteamAsync(lobbyId)
                     ↓
              SteamLobbyManager.JoinLobbyAsync → NetworkManagerSetup.StartClientSteam
                     ↓
              NGO NetworkSceneManager가 "1.Lobby"로 클라이언트 씬 동기화
```

사용자가 확인해준 "코드 입력도 똑같이 실패"는, 실패 지점이 **초대 콜백 구독(트랙5/7에서 다뤘던 부분)이 아니라 `JoinGameSteamAsync` 이후 공유 구간**이라는 뜻이다. 방 목록 UI를 새로 만들어도 마지막엔 같은 `JoinGameSteamAsync`를 타므로, **겉모습만 다른 입구 3개를 만들어도 안쪽 복도가 막혀있으면 셋 다 똑같이 막힌다.** 이 근거로 방 목록 작업은 **보류**하고, 공유 구간의 확정 진단을 먼저 하기로 사용자와 합의함.

**용의자로 지목된 지점(미확정, 다음 로그로 검증 필요):** `TitleMenuController.TryRestartForWarmReconnect()` — "이 프로세스에서 이미 한 번이라도 Client로 접속에 성공한 적 있으면(2번째 이상 시도), 트랜스포트 중복 메시지 버그를 피하려고 프로세스를 통째로 재시작(`Process.Start`+`Application.Quit`)"하는 트랙5 우회 로직. 이 재시작 자체가 실패/불안정할 수 있고, 초대·코드입력 가리지 않고 모든 2번째 이상 시도에 걸리는 공용 지점이라 유력한 후보다. **다만 이번 재현이 "완전 최초 시도"였는지 "2번째 이상 시도"였는지 구분이 안 된 채로 보고돼서, 이번 세션엔 이걸 특정할 수 없었다** — 아래 진단 계측이 바로 이 구분을 위한 것.

### "공개 방 목록" 아이디어 자체는 기각이 아니라 보류 — 설계 초안(참고용, 미확정)

친구가 아닌 사람과도 하고 싶다는 요구 자체는 타당하다고 판단함. `SteamMatchmaking.LobbyList.WithMaxResults(n).RequestAsync()`(Facepunch API 존재 확인됨, 웹 검색)로 기술적 실현 가능성도 확인함. 접속 실패가 확정 진단·수정된 **이후** 재개할 것 — 논의된 초안만 기록:

- Lobby 타입 `Private` → `Public` 전환 필요(§3 재검토 대상 — "공개 매칭 요구사항 없음"으로 확정했던 결정을 뒤집는 것).
- 호스트가 방 제목(커스텀 텍스트) 입력 → `lobby.SetData("roomName", title)`으로 목록에 노출.
- 목록 화면: `[방 제목] [인원 X/4]` 행 나열, Steam 로비 목록은 실시간 push가 아니라 스냅샷이라 수동 새로고침 필요.
- 입장 방식은 3안 중 미정 — (A) 목록 클릭 시 원클릭 입장 / (B) 클릭 시 코드가 입력창에 자동으로 채워지고 한 번 더 확인 버튼(기존 코드입력 파이프라인 재사용, 사용자 발언 "코드를 입력한 다음 입장"과 가장 가까움) / (C) 목록엔 제목만, 코드는 안 보여주고 방장이 별도 공유.
- 비공개방 옵션 유지 여부, 꽉 찬 방/게임 시작된 방 표시 여부, 방 제목 자유입력 여부도 미정.

### 이번 세션에 추가한 것 — 진단 로그 + 정체 감지 워치독 (동작 변경 없음, 로그만 추가)

**`Assets/Scripts/UI/TitleMenuController.cs`:**
- `JoinGameSteamAsync(SteamId lobbyId)` → `JoinGameSteamAsync(SteamId lobbyId, string source = "미상")`로 시그니처 확장. 호출부 3곳이 각각 `"온기동초대"`(`OnSteamInviteAccepted`) / `"코드입력"`(`ConfirmJoinSteam`) / `"냉기동"`(`TryAutoJoinFromLaunchArgs`) 태그를 넘기도록 수정 — **이제 로그만 보고도 어느 경로였는지 100% 구분 가능**(지금까지는 이게 안 돼서 "코드입력도 똑같다"는 걸 사용자 구두 확인에만 의존했음).
- 각 단계(진입 / `TryRestartForWarmReconnect` 결과 / `JoinLobbyAsync` 호출 전후 / `StartClientSteam` 반환)에 `System.Diagnostics.Stopwatch` 경과시간을 실어 로그.
- `TryRestartForWarmReconnect()` — 트리거될 때만 로그하던 것을 **진입 시 항상**(트리거 여부와 무관하게) `HasConnectedAsClientSteamThisProcess` 값과 함께 로그하도록 변경. **이 로그 한 줄이 위 "용의자" 가설을 바로 검증한다** — `true`면 이번이 그 프로세스의 2번째 이상 시도라는 뜻.
- **`JoinWatchdog` 코루틴 신설** — 접속 시도(`JoinGameSteamAsync`) 시작 시 같이 기동, 10초 안에 "1.Lobby" 씬으로 전환되지 않으면 `[WATCHDOG]` 태그로 NGO/Steam 전체 상태(ActiveScene, IsListening, IsConnectedClient, LocalClientId, ConnectedClients.Count, SteamLobby.IsInLobby, HasConnectedAsClientSteamThisProcess)를 한 번에 덤프. 접속이 성공하면 "0.Title" 씬이 언로드되며 이 컴포넌트 자체가 파괴돼 코루틴이 조용히 멈춘다(정상 — 성공 시 별도 로그 없음, WATCHDOG 로그가 없으면 성공했다는 뜻).

**`Assets/Scripts/Network/NetworkManagerSetup.cs`:**
- **`NetworkManager.SceneManager.OnSceneEvent` 신규 구독** (`SubscribeSceneDiag`/`DiagOnSceneEvent`) — `StartHostSteam`/`StartClientSteam` 성공 직후 구독. Load/LoadComplete/Synchronize/SynchronizeComplete 등 NGO 씬 동기화 이벤트 전부를 `[DIAG][SceneEvent] Type=... SceneName=... ClientId=...`로 로그. **지금까지 "Steam Client 접속은 됐는데 그 다음 씬 전환이 어디서 막히는지"를 볼 방법이 전혀 없었던 구멍을 메우는 게 이번 계측의 핵심.** `NetworkSceneManager`는 세션마다 새로 생성되므로 매 `StartHostSteam`/`StartClientSteam` 호출 후 재구독(직전 구독 인스턴스를 기억해뒀다가 중복 방지).
- `ApproveConnection`에 **승인 성공 로그**도 추가(기존엔 거부 시에만 로그 있었음) — `clientId`, 승인 전후 인원수 포함.

**`Assets/Scripts/UI/LobbyMenuController.cs`:**
- `Start()` 최상단에 `[LobbyMenuController][DIAG] Start() 진입 — 로비 씬 로드 완료` 로그 추가. **클라이언트가 실제로 로비 씬에 도달했는지 확인하는 최종 판정 지점**(이 로그가 있으면 성공, 없으면 그 앞 어딘가에서 막힌 것).

### 다음 세션 액션 — 재현 가이드 (본인 + 부계정 B, 2인 전용)

**반드시 아래 두 케이스를 구분해서 각각 재현하고, 어느 쪽인지 명시해서 로그와 함께 전달할 것:**

1. **케이스 1(최초 시도)** — B가 게임을 새로 켜고 처음으로 접속 시도(초대든 코드든 상관없음). `HasConnectedAsClientSteamThisProcess=False`로 찍혀야 정상.
2. **케이스 2(재시도)** — 케이스 1 시도 직후(성공/실패 무관), **같은 프로세스에서 종료하지 않고** 다시 한번 접속 시도. `HasConnectedAsClientSteamThisProcess=True`로 찍히고 `TryRestartForWarmReconnect`가 트리거되어야 정상 — 이게 안 되거나 재시작 후에도 실패하면 위 "용의자"가 확정됨.

각 케이스마다 아래 필터로 B(접속 시도한 쪽)의 `Player.log`를 캡처:

```powershell
Select-String -Path "$env:USERPROFILE\AppData\LocalLow\DefaultCompany\Kkul-tteok!\Player.log" -Pattern "\[DIAG\]|\[NetworkManagerSetup\]|\[SteamLobbyManager\]|\[TitleMenuController\]|\[SteamManager\]|\[LobbyMenuController\]" | Select-Object -ExpandProperty Line
```

**판정 방법:**
- `[LobbyMenuController][DIAG] Start() 진입` 로그가 있으면 → **성공**(그 케이스는 정상 동작, 다른 케이스를 의심).
- 없고 `[WATCHDOG]` 경고가 있으면 → 그 로그 한 줄의 상태 덤프로 실패 지점이 좁혀짐:
  - `NGO.IsListening=False` → `StartClientSteam` 자체가 실패했거나 `JoinLobbyAsync`에서 이미 멈춘 것 (그 위의 `[DIAG]` 로그로 어디까지 갔는지 확인).
  - `NGO.IsListening=True`인데 `IsConnectedClient=False` → 트랜스포트 레벨 연결이 안 됨(Steam 릴레이/virtual port 불일치 의심).
  - `IsConnectedClient=True`인데 씬 전환이 안 됨 → `[NetworkManagerSetup][DIAG][SceneEvent]` 로그가 있는지 확인. 아예 없으면 Host가 씬 로드 자체를 안 보낸 것, `Load`는 있는데 `LoadComplete`가 없으면 클라이언트 쪽 씬 로드 도중 예외(과거 이슈 F류 — "Server Scene Handle already exist" 등) 의심.
- 이 판정 트리 그대로 따라가면 다음 세션은 추측 없이 원인을 하나로 좁힐 수 있다.

### 인수인계 요약 (2026-08-13 밤 9차 세션 기준, 최신)

1. **온기동/코드입력 공통 실패 — 원인 미확정. 코드 수정 없음, 진단 계측만 추가.** 다음 세션은 위 케이스 1/2 재현 로그부터 받고 시작.
2. **공개 방 목록 — 설계 논의만 하고 보류.** 재개 조건: 위 접속 실패가 확정 진단·수정된 이후. 논의된 초안은 위 절 참고.
3. 크래시(D3D12)/한글 사용자명 libvosk 이슈(8차 세션) — 이번 세션과 무관, 상태 변경 없음.

**Files changed (이번 세션):** `Assets/Scripts/UI/TitleMenuController.cs`(source 태그·Stopwatch·`JoinWatchdog` 추가), `Assets/Scripts/Network/NetworkManagerSetup.cs`(`SceneManager.OnSceneEvent` 구독·승인 로그 추가), `Assets/Scripts/UI/LobbyMenuController.cs`(`Start()` 진입 로그), `Assets/Docs/SteamworksIntegrationDesign.md`.

**Files read (이번 세션):** `Assets/Scripts/UI/TitleMenuController.cs`, `Assets/Scripts/Network/SteamLobbyManager.cs`, `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/UI/LobbyMenuController.cs`, `Assets/Scenes/0.Title.unity`(버튼 배선 확인), `.cursor/rules/bug-hunter.mdc`, `.cursor/rules/plan-first.mdc`, `.cursor/rules/diff-only.mdc`, 웹 검색(Steamworks `ELobbyType` 공식 문서, Facepunch.Steamworks lobbyId 우회 이슈 #648, `SteamMatchmaking.LobbyList` API, NGO `NetworkSceneManager.OnSceneEvent`/`SceneEvent`/`ConnectionApprovalRequest.ClientNetworkId` 공식 문서).

## 트랙 6 — 10차 세션 (2026-08-17): 온기동/코드입력 공통 실패 — "Server Scene Handle already exist" 확정 진단·수정 완료

> **이 절이 최신 상태.** 9차 세션에서 추가한 진단 계측(`source` 태그, `[DIAG][SceneEvent]`, `JoinWatchdog`, `LobbyMenuController.Start()` 진입 로그)이 실제로 원인을 특정하는 데 성공했다.

### 재현 로그 확보 경위

사용자가 계정 B(부계정, 다른 PC)와 계정 A(본인 데스크탑, "Young") 둘로 반복 테스트하며 `Player.log`/`Player-prev.log`를 여러 차례 캡처해서 제공:

1. **B가 냉기동(`+connect_lobby`)으로 A의 방에 접속** — 정상 성공(로비 씬 진입, 슬롯 정상). 이후 A가 `NotifyHostQuit`으로 방을 나가서 B가 타이틀로 복귀 — **이건 버그 아님, 정상 동작.**
2. **A가 호스트로 방을 열었다 종료(`NotifyHostQuit`) → 그 직후 같은 프로세스에서 B의 초대(`source=온기동초대`)를 받아 클라이언트로 접속 시도** — 여기서 진짜 문제 재현. `HasConnectedAsClientSteamThisProcess=False`로 찍혀서(직전엔 호스트였지 클라이언트였던 적은 없으므로) `TryRestartForWarmReconnect`가 **트리거되지 않고** 인프로세스로 접속을 시도했고, 그 결과:
   ```
   [NetworkManagerSetup][DIAG][SceneEvent] Type=Synchronize, ClientId=1
   [NetworkManagerSetup][DIAG][SceneEvent] Type=Load, SceneName=1.Lobby, ClientId=1
   [NetworkManagerSetup][DIAG][SceneEvent] Type=Synchronize, ClientId=1     ← 중복 발생
   [NetworkManagerSetup][DIAG][SceneEvent] Type=Load, SceneName=1.Lobby, ClientId=1   ← 중복 발생
   ...
   Exception: Server Scene Handle (Unity.Netcode.NetworkSceneHandle) already exist!
   Happened during scene load of 1.Lobby with Client Handle (-1754)
   NullReferenceException (연쇄 3회)
   [LobbyMenuController][DIAG] RefreshAllSlots 실행 — SlotCount=0, hostId=18446744073709551615(없음)
   ```
   호스트 슬롯까지 화면에서 사라지고("완전히 0명") 이후 화면이 멈춤 — 사용자가 "타이틀 씬에서 아예 꼼짝도 안 한다"고 보고한 증상의 정체.

### 근본 원인 (확정)

`TryRestartForWarmReconnect()`의 재시작 가드가 `NetworkManagerSetup.HasConnectedAsClientSteamThisProcess`(= "이 프로세스에서 **Client**로 접속한 적 있는지")만 체크했다. 이 프로세스가 **호스트**로 한 번 방을 열었다 `Shutdown()`한 경우는 이 플래그가 여전히 `False`라서 감지되지 않았다. 하지만 실측 결과 이전 세션(호스트든 클라이언트든)의 NGO `NetworkSceneManager` 씬 핸들이 `Shutdown()` 이후에도 완전히 정리되지 않고 남아있다가, 같은 프로세스로 다시 네트워킹(이번엔 클라이언트)을 시작하면 "1.Lobby" 씬 동기화 시 씬 핸들이 충돌해 `Server Scene Handle already exist!` 예외 → 연쇄 `NullReferenceException` → 슬롯 데이터 붕괴로 이어진다. 즉 트랙5에서 이미 알려진 "2번째 이상 Client 접속" 충돌과 **완전히 동일한 메커니즘**이지만, 트리거 조건이 "Client 재접속"뿐 아니라 **"Host였다가 Client로 최초 전환"** 케이스에도 적용된다는 게 이번에 새로 확인된 사실.

### 수정 완료

- `Assets/Scripts/Network/NetworkManagerSetup.cs`: `s_hasConnectedAsClientSteamThisProcess`(`HasConnectedAsClientSteamThisProcess`) → `s_hasStartedSteamNetworkingThisProcess`(`HasStartedSteamNetworkingThisProcess`)로 개명·확장. `StartClientSteam()` 성공 시뿐 아니라 **`StartHostSteam()` 성공 시에도** 이 플래그를 `true`로 세팅하도록 추가.
- `Assets/Scripts/UI/TitleMenuController.cs`: `TryRestartForWarmReconnect()` / `JoinGameSteamAsync()` / `JoinWatchdog()` 세 곳의 참조를 새 플래그명으로 교체 — 호스트였다가 클라이언트로 전환하는 시도도 이제 프로세스 재시작(`+connect_lobby`) 경로를 타도록 함.

**Verify (다음 세션/사용자 재현):** 정확히 이번에 재현했던 순서(A가 호스트로 방 생성 → 누군가 접속·이탈 반복 → `NotifyHostQuit`으로 방 종료 → 그 직후 같은 프로세스로 초대/코드입력 접속 시도)를 다시 재현해서 `TryRestartForWarmReconnect 진입 — HasStartedSteamNetworkingThisProcess=True`가 찍히고 프로세스 재시작 후 정상적으로 로비에 입장되는지 확인. 기존에 정상이었던 "완전 최초 실행 → 접속"(케이스 1), "Client로 이미 접속했다가 재접속"(케이스 2, 트랙5부터 정상)도 회귀 없는지 같이 확인.

**Impact:** 재시작 워크어라운드가 걸리는 시나리오가 늘어남(호스트 종료 후 첫 접속도 이제 재시작됨) — 재시작 자체는 트랙5부터 검증된 안전한 경로라 새로운 리스크는 아니고, 해당 유저는 접속 시 창이 한 번 재실행되는 지연(수백 ms~1초)이 추가로 생김.

**Files changed (이번 세션):** `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Scripts/UI/TitleMenuController.cs`, `Assets/Docs/SteamworksIntegrationDesign.md`.

**Files read (이번 세션):** `Assets/Scripts/Network/LobbyNetworkManager.cs`, `Assets/Scripts/UI/LobbyMenuController.cs`, `Assets/Scripts/UI/TitleMenuController.cs`, `Assets/Scripts/Network/NetworkManagerSetup.cs`, `Assets/Docs/NetworkDesign.md`(로비 관련 항목), 사용자 제공 `Player.log`/`Player-prev.log`(계정 A "Young", 계정 B "youngjun0316") 직접 읽음.

### 인수인계 요약 (2026-08-17 10차 세션 기준, 최신)

1. **온기동/코드입력 공통 실패 — 원인 확정·수정 완료.** "호스트 종료 후 같은 프로세스에서 첫 클라이언트 접속" 케이스가 재시작 가드에서 빠져있던 게 원인. 다음 세션은 위 Verify 시나리오 재현 결과부터 받을 것.
2. **공개 방 목록 — 여전히 보류.** 이번 접속 버그 수정이 검증된 뒤 재개.
3. 크래시(D3D12)/한글 사용자명 libvosk 이슈(8차 세션) — 이번 세션과 무관, 상태 변경 없음.
