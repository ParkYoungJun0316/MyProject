# Network Design (MVP)

네트워크 · 출시 계획 문서.  
**데모 없음.** Steam **Playtest** → **정식 출시**만.  
스테이지 범위: **`M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`**.  
(`End.Demo` = 클리어 UI 씬명 레거시. 리네임은 별도 작업.)

**앵커:** D0 = Steam Direct App Fee 결제일. **D14** = Coming Soon + Playtest 동시 오픈 목표.

---

## 0. 출시 로드맵 · 작업 우선순위

### 0.1 달력 · 3트랙 (확정)

| 구간 | 초점 |
|------|------|
| **D0–D13** | 오픈 준비 — Steam P2P, **M 풀코스+보스**, **텔레메트리**, 스토어 리뷰, 법인 착수 |
| **D14** | **Coming Soon + Playtest 동시** / Steam 2인 스모크 |
| **D14–D21** | **Playtest M주** — `M.Stage1`→`M.Stage5`→`M.Boss` |
| **D21–D28** | **Playtest T주** — `T.Stage1`→`T.Stage5`→`T.Boss` |
| **D28–D30+** | 출시 직전 — Tutorial·옵션·밸런스·QA · **법인 계정**에서 정식 출시 |

**출시 가능 윈도우:** 앱피 30일(≈D30) ∩ Coming Soon 14일(오픈 D14 → ≈D28 충족) → **대략 D30 전후**.

| 트랙 | 역할 | 합류 |
|------|------|------|
| **게임/빌드** | P2P → M주 → T주 → 출시 Must | D14 / M주 / T주 / 출시 |
| **스토어** | 페이지 → 리뷰 → Coming Soon(+Playtest) | **D14 = 게임 빌드와 동시** |
| **법인** | 설립 → 법인 Steam → 앱 이전 | **출시 버튼만** 법인. Coming Soon·Playtest는 개인 가능 |

**폴백:** Coming Soon만 먼저 → **+1주** Playtest. 기본안은 **동시**.

### 0.1.1 전체 작업 목록 (확정 순서)

| 순위 | 작업 | 비고 |
|------|------|------|
| 1 | **네트워크** | ParrelSync → 빌드 → **Steam P2P** (§0.2) — Playtest·정식 필수 |
| 2 | **텔레메트리** | §0.5.1 — **Open Must** (관전 대신 상황 파악) |
| 3 | **사운드 마무리** | 최소한만. 과하면 방해되므로 억제 |
| 4 | **파티클** | 시작 안 함. **피격·Break** 등 핵심만 |
| 5 | **난이도 밸런싱** | Playtest M주·T주 피드백 기반 → 출시 전 흡수 |
| 6 | **UI 마무리** | 오픈: 최소 HUD / **정식:** 옵션·볼륨 등 |
| 7 | **Steamworks 연동** | Transport·Lobby·Depot·Playtest (§0.3) |
| 8 | **출시 QA** | 빌드·Steam·2~4인 시나리오 체크리스트 |
| — | **컷씬** | **영구 제외** (출시 후에도 안 넣음) |
| — | **관전** | 출시 전 제외 → **Post-Launch 후보** |
| — | sit / dance 이모트 | Post-Launch |

### 0.2 네트워크 테스트 단계 (개발자 환경)

**개발 중 멀티 검증은 아래 순서로만 한다** (원격 IP Join·discovery 없음).

**오픈·Playtest 핵심:** Steam에서 **원격 협동 + 인게임 보이스 + 응원** + **텔레메트리**.  
**개발자 장비:** 테스트 PC **최대 2대** — Steam P2P 일상 검증은 **2인** 기준 (§0.2.1).

```
① ParrelSync (에디터 Host + Clone Client)
   → 빠른 반복·버그 수정. ※ 출시 판정용 아님.

② Development Build (Host EXE + Client EXE, localhost / 같은 PC)
   → exe·NGO·마이크 등 **빌드 전용 버그** 중간 게이트. ※ 원격 4인 검증 아님.

③ 응원 시스템 (CheerService + Dissonance + Vosk + /cheer)
   → ②에서 1차, ④ Steam에서 최종 검증. (상세: CheerAndTutorialDesign.md §11)

④ Steam P2P + Steam Lobby + Depot
   → Transport 교체. **Playtest / 정식 오픈 게이트** — 2인 필수, 4인 권장 (§0.2.1).
```

| 단계 | 목적 | 통과 기준 (최소) |
|------|------|------------------|
| ① ParrelSync | 구현·버그 수정 속도 | Title→Lobby→M 경로 **2인** 진행 1회 |
| ② Dev Build | 빌드 품질·localhost NGO | **2인** 클리어/사망 리로드 1회, 스테이지 전환 OK |
| ③ 응원 | 협동+보이스+응원 | CheerAndTutorialDesign §12 시나리오 |
| ④ **Steam P2P** | **Playtest·정식 오픈** | **2인** Steam 원격: 초대·보이스·응원. **4인 1회 권장** |

**Transport:** ①② 개발 중 `UnityTransport`(localhost). **Playtest·정식 배포 = Steam Networking transport 필수.**

#### 0.2.1 개발자 2PC · 2인 테스트 vs 4인

| | **2인 Steam P2P** (일상) | **4인 Steam P2P** (오픈·Playtest 권장) |
|--|--------------------------|----------------------------------|
| 검증됨 | Transport, Lobby, Invite/Join, NGO 동기화, Dissonance, Vosk, 응원(필요 1표), 스테이지 진행 | 위 + **4슬롯·4스폰·응원 3표·4음성** |
| **보장 안 됨 → 4인 전용 버그** | — | `ActivePlayerCount`·집계, 4색 Gate, 4명 보이스 혼잡, 이탈 시 §12 전원 타이틀 수렴 |
| 판정 | **Playtest 오픈 최소 게이트** (2PC 한정) | **외부 신뢰도** — 친구 Playtest **1회 강력 권장** |

**2인 통과 = 4인 100% 보장 아님.** 다만 NGO·Steam P2P·응원 **연결·규칙 골격**은 2인에서 대부분 검증 가능.  
**4인만 터지는 버그**는 §0.2.1 표 우측 항목 — 오픈·M주 중 **4인 1회**로 잡는다.

### 0.3 범위 — Open / Playtest / Release

#### Open Must (D14 Coming Soon + Playtest)

- **플레이 경로 (오픈 빌드):** Title → Lobby → `M.Stage1`…`M.Stage5` → `M.Boss` (멀티 **2~4인**)
- **솔로:** 동일 경로 (**NGO Host 1인**, `partySize=1`)
- **T 풀코스:** 오픈일에 완벽할 필요 없음. **T주 시작 전**(`D21`)까지 `T.Stage1`…`T.Boss` 완성
- **네트워크:** §9 Must 동기화 + **§0.2 ④ Steam P2P + Steam Lobby** (친구 Playtest 다운·초대)
- **응원·보이스:** 인게임 **Dissonance** + **Vosk 응원** + `/cheer` (`CheerAndTutorialDesign.md`)
- **텔레메트리:** §0.5.1 — Steam **Playtest·정식** Depot에서 전송 ON
- **배포:** Steam Playtest Depot. localhost/IP Join로 외부 테스트 **안 함**
- **UI:** 타이틀·로비·HP·카운트다운·응원 HUD·채팅 `/cheer` · 클리어 시 `End.Demo`(풀런 시)
- **사운드:** BGM 1~2 + 핵심 SFX
- **파티클:** 피격·Break만 (선택)
- **난이도:** “클리어 가능” 수준. 본격 밸런싱은 M주·T주 피드백 후

#### Playtest M주 (D14–D21)

- 코스: **`M.Stage1` → … → `M.Stage5` → `M.Boss`**
- 텔레메트리로 이탈·사망·클리어·응원 거부 확인
- 핫픽스 주 1–2회 · **4인 1회** 권장
- 병행: T 풀코스 마무리

#### Playtest T주 (D21–D28)

- 코스: **`T.Stage1` → … → `T.Stage5` → `T.Boss`**
- 동일하게 텔레메트리 + 핫픽스
- 병행: Tutorial · 옵션 · 밸런스 흡수 · 법인 Steam·앱 이전

#### Release Must (D28–D30+ · 정식 출시)

- Steam P2P·Lobby **유지·안정화** (Invite UX polish)
- **난이도 밸런싱** (Playtest 피드백)
- **Tutorial** (연습·말해보기) + CheerName **발음 유사/G2P polish**
- **UI:** 옵션(마스터·BGM·SFX), 해상도/전체화면
- **출시 QA** · **법인 계정**에서 빌드 리뷰·정식 출시
- (선택) Dissonance **Steam P2P** 음성 transport 분리

#### 출시 전 · 영구 제외

| 항목 | 처리 |
|------|------|
| Steam **데모** 빌드/페이지 | **없음.** Playtest로 대체 |
| §12 재접속·유예·스냅샷·호스트 마이그레이션 | **미지원.** 인게임 이탈 = **방 종료** |
| 원격 IP Join / UDP discovery | **미사용.** 개발=ParrelSync·localhost / 배포=**Steam** |
| 관전(Spectator) | 출시 전 **제외** → Post-Launch 후보 |
| **컷씬** | **영구 제외** (출시 후에도 안 넣) |
| sit / dance 이모트 | Post-Launch |
| 파티클 대량 추가 | Post-Launch |

### 0.4 권장 작업 순서 (요약)

**상세 실행 순서·체크 항목은 §0.5 참고.**

```
[D0–D13 오픈 준비]
0. 테스트 전 블로커 (Vosk, CheerName, AudioListener)
1. 폴리시 (오디오, 카메라, DialogueUI, End.Demo, 빌드 메타)
2. 로컬 테스트 (1인 → 2인 Dev Build → 스크린샷 1차)
3. Steamworks (App ID · Transport · Lobby · Depot) + 텔레메트리 MVP (§0.5.1)
4. 스토어 페이지 · 리뷰 · M 풀코스+보스 Steam 2인

[D14] Coming Soon + Playtest 동시
[D14–D21] Playtest M주 (보스 포함) + T 병행
[D21–D28] Playtest T주 (보스 포함) + Tutorial·옵션·법인 이전
[D28–D30+] 출시 QA → 법인 계정 정식 출시
```

### 0.5 오픈·Playtest·출시 체크리스트 (실행 순서)

> 음성 시스템(CheerService + Dissonance + Vosk) 구축 완료 이후 기준.  
> 각 테스트 단계 직후 **버그 수정 구간**을 둔다.

#### Phase 0 — 테스트 전 블로커

| # | 작업 | 비고 |
|---|------|------|
| 0-1 | Vosk zip 정합 | `VoskModelLoader` 기대 zip ↔ `StreamingAssets` 실제 파일 일치 |
| 0-2 | CheerName 최종화 | `berry` / `guma` / `sook` / `hobak` — `CheerLexiconBuilder`·`CheerService`·`/cheer` 통일 |
| 0-3 | AudioListener | `LocalPlayerCamera` 프리팹에 1개. 씬 Main Camera 비활성 → 클라이언트당 1개 보장 |

#### Phase 1 — 폴리시

| # | 작업 | 비고 |
|---|------|------|
| 1 | 오디오 | SFX/BGM 볼륨, `SFXManager.masterVolume`, Listener 배치 |
| 2 | 카메라 | **C안 확정** — `LocalPlayerCamera` DDOL 프리팹. Owner 첫 스폰 시 1회 생성, 씬 Main Camera 비활성 |
| 3 | DialogueUI | M/T 구역별 규칙·응원 설명 (`DialogueUI.cs`) |
| 4 | End.Demo | 클리어 UI, 타이틀 복귀 (Discord 피드백 버튼 선택) |
| 5 | 빌드 메타 | `Player Settings`: Product Name, Default Icon, `bundleVersion` (예: `0.1.0-playtest`) |

#### Phase 2 — 로컬 테스트

| # | 작업 | 통과 기준 (최소) |
|---|------|------------------|
| 6 | 1인 E2E | Title → Lobby → M 경로. `/cheer`·음성 응원 1회 |
| 7 | 버그 수정 | Phase 2 이슈 정리 |
| 8 | 2인 Dev Build E2E | localhost, 보이스 양방향, 응원, 사망 리로드 1회 (§0.2 ②) |
| 9 | 버그 수정 | Phase 2 이슈 정리 |
| 10 | 스크린샷 1차 | Steam 스토어 초안용 (§0.5.2) |

#### Phase 3 — Steamworks · 텔레메트리 · 스토어

| # | 작업 | 비고 |
|---|------|------|
| 11 | Steam App ID + Steamworks | Transport → Steam Networking, Lobby, Depot 파이프라인 |
| 12 | **텔레메트리 MVP** | §0.5.1 — **Open Must.** Playtest Depot에서 전송 ON |
| 13 | 스토어 페이지 · 리뷰 신청 | 스크린샷·설명 §0.5.2. D14 Coming Soon 목표 |
| 14 | M 풀코스+보스 Steam 2인 | 오픈 직전 최소 게이트 |

#### Phase 4 — D14 오픈 → M주 → T주 → 정식

| # | 작업 | 비고 |
|---|------|------|
| 15 | **Coming Soon + Playtest 동시** | D14. 친구 초대 스모크 |
| 16 | Playtest M주 | M1–5+Boss · 핫픽스 · 텔레메트리 · 4인 1회 권장 |
| 17 | Playtest T주 | T1–5+Boss · Tutorial·옵션·밸런스 · 법인 이전 |
| 18 | 스크린샷 최종 + 스토어 마무리 | 실플레이·안정 빌드 (§0.5.2) |
| 19 | 출시 QA · **법인 계정** 빌드 리뷰 · **정식 출시** | 앱피 30일·Coming Soon 14일 충족 후 |

#### 0.5.1 텔레메트리 MVP (Open Must)

> **범위:** **Open Must** — Coming Soon/Playtest 오픈 전에 전송 경로 ON. 관전 시스템 대신 이탈·체류·사망·응원 거부로 상황 파악.  
> **구현 에이전트:** 이 절만 읽고 구현 가능. 착수 = **D14 오픈 전**.  
> 순서: ① Google Sheet + Apps Script upsert → ② `TelemetryService` + 게임 연동.

##### 목적 · 시점

- **목적:** Steam **Playtest·정식** 플레이 1판당 **Google Sheets 1행(upsert)** — 이탈 구간, 바이옴(M/T) 체류·사망, 응원 거부·채팅 **합계**.
- **시점:** **D14 오픈 전** 구현·전송 ON. Playtest 데이터를 잃지 않음.
- **구현 순서:** ① Google Sheet + Apps Script upsert → ② `TelemetryService` + 게임 연동.

##### 아키텍처

| 항목 | 규칙 |
|------|------|
| **진입점** | `TelemetryService` — `0.Title` 배치, **DontDestroyOnLoad** |
| **전송 대상** | Google Sheets (Apps Script **Web App** URL) |
| **행 모델** | **세션 1행** — `sessionId` 기준 **upsert**(갱신). append-only 금지(중간 스냅샷 쓰레기 행 방지). |
| **보내는 쪽** | **Host PC 1행만** (솔로 = 그 PC가 Host 역할). Client는 Host에 **+1만 RPC 보고**, Sheets 직접 전송 **금지**. |
| **Sink 분리** | `ITelemetrySink` (MVP: `GoogleSheetsSink`) — URL·HTTP만 담당. 집계는 `TelemetryService`. |

##### 전송 ON/OFF (Must)

| 환경 | Sheets 기록 |
|------|-------------|
| Unity **에디터** Play | ❌ |
| **ParrelSync** (에디터 클론) | ❌ |
| **Development Build** localhost | ❌ |
| **Steam Depot Playtest**, Steam 클라이언트로 실행 | ✅ |
| **Steam Depot 정식**, Steam 클라이언트로 실행 | ✅ |

**권장 게이트 (둘 다 만족 시 전송):**

1. `#if !UNITY_EDITOR`
2. Steamworks 초기화 성공 (`SteamAPI` 등). Playtest·정식 빌드 파이프라인에서 Scripting Define `TELEMETRY_RELEASE` (또는 동등한 배포 게이트).

Inspector `enabled` 토글은 **로컬 디버그용**. 위 게이트가 **배포 판정** 기준.

##### 세션 생명주기

| | 시점 | 동작 |
|--|------|------|
| **세션 시작** | **`M.Stage1` 첫 로드** | 새 `sessionId`(UUID), 카운터·dwell 타이머 초기화. 멀티=로비 Start 후 / 솔로=로비 Start 후 동일. |
| **세션 진행** | M·T 전 씬(보스 포함) 플레이 중 | 누적 카운터 갱신 + 주기 upsert (§전송 타이밍). |
| **`run_complete`** | Host(또는 솔로 PC)가 **`End.Demo` 씬 로드** | `run_complete = true` (타이틀 복귀 **전**). |
| **세션 끝** | Host(또는 솔로 PC) **`TitleReturnFlow.ExecuteReturn()`** | **마지막 upsert** + `quitAt` 기록 + `sessionId` 폐기. |

**세션 끝 = Host `TitleReturnFlow` 1회.** 아래 경로는 전부 동일 훅:

- `End.Demo` 타이틀 복귀 (`TitleReturnReason.EndDemo`)
- Host Quit (`HostQuitRoom`)
- Client 이탈 → Host `DisconnectManager` (`ClientDisconnected`)
- Client가 End에서 먼저 나가도 Host도 타이틀 복귀 → Host `TitleReturnFlow`

**예외 (세션 끝 upsert 없음):** Host **Alt+F4** / 작업 관리자 강제 종료 → `TitleReturnFlow` 미실행. **마지막 중간 upsert**(30초·씬 전환·리로드)까지만 유지. **감수.**

로비만 있고 Start 안 누른 구간 = **세션 아님** (기록 없음).

##### Google Sheets — 컬럼 (헤더 1행 고정)

**복붙용 순서:**

```
timestamp | sessionId | buildVersion | playMode | partySize | run_complete | quitAt | M_dwell_sec | M_death_count | M_buff_count | T_dwell_sec | T_death_count | T_buff_count | reject_self_cheer | reject_target_buffed | reject_timeout | reject_chat_rate_limit | reject_voice_no_match | chat_used_count
```

| 컬럼 | 타입 | 설명 |
|------|------|------|
| `timestamp` | datetime | **upsert 전송 시각** (UTC 또는 KST 중 하나로 통일) |
| `sessionId` | string | 익명 UUID. **upsert 키.** |
| `buildVersion` | string | `Application.version` (예: `0.1.0-playtest` / `1.0.0`) |
| `playMode` | string | `Solo` / `Multi` (멀티 1인도 `Multi`) |
| `partySize` | int | 1~4 |
| `run_complete` | bool | `End.Demo` **씬 진입** 여부 |
| `quitAt` | string | 세션 끝 upsert 시 Host 위치: `M` / `T` / `End` |
| `M_dwell_sec` | float | **M 바이옴 합산** 체류(초) — `M.Stage*` + `M.Boss` |
| `M_death_count` | int | **M 바이옴** 씬 로드(리로드 포함) 횟수 합산 |
| `M_buff_count` | int | **M 바이옴** 버프 **적용** 횟수 합산 |
| `T_dwell_sec` | float | **T 바이옴 합산** 체류(초) — `T.Stage*` + `T.Boss` |
| `T_death_count` | int | **T 바이옴** 씬 로드 횟수 합산 |
| `T_buff_count` | int | **T 바이옴** 버프 적용 횟수 합산 |
| `reject_self_cheer` | int | 자기 응원 거부 **합계** (전원) |
| `reject_target_buffed` | int | 대상 버프 중 거부 **합계** |
| `reject_timeout` | int | 표 부족 타임아웃 **합계** |
| `reject_chat_rate_limit` | int | 채팅 rate limit **합계** |
| `reject_voice_no_match` | int | Vosk 미인식 **합계** |
| `chat_used_count` | int | `/cheer` 사용 **총 횟수** (합계) |

**수집 금지:** 마이크 원음, 채팅/대화 **전문**, SteamID, IP, 닉네임 등 **개인 식별 정보**.

##### 측정 규칙 (Must)

| 항목 | 규칙 |
|------|------|
| **바이옴** | 씬 이름이 `M.` 접두 → M 컬럼, `T.` 접두 → T 컬럼. Stage·Boss 구분 없이 **합산**. |
| **dwell** | 해당 씬 **로드 직후** ~ **다른 씬으로 나가기 직전**까지 Host `Time.time`(또는 `unscaledTime`)을 해당 바이옴에 누적. |
| **death_count** | 해당 바이옴 **씬 Load마다 +1** (첫 진입 포함). 사망=전원 리로드이므로 **리로드 1회 = death 1**. 동시 다수 사망도 **+1**. |
| **buff_count** | 버프가 **플레이어 1명에게 적용될 때마다 +1**. 4인 전원 버프 = **+4**. 적용 시점 씬의 바이옴에 가산. |
| **reject / chat** | **4인 합계** — Client 발생 시 Host RPC로 +1 보고 후 Host가 누적. |
| **partySize** | 세션 시작 시 `GameSession` / `NetworkManager.ConnectedClientsIds.Count` 등 Host 기준 스냅샷. |

##### reject / chat — 코드 매핑

| 컬럼 | +1 조건 (구현 참고) |
|------|---------------------|
| `reject_self_cheer` | `CheerService.ValidateCheer` — 자기 색 응원 (`myIdx == targetColorIndex`) |
| `reject_target_buffed` | `ValidateCheer` — `_buffEnd`에 target 존재 |
| `reject_timeout` | `CheerService.CheckTimeouts` → `ResetVotes` (표 부족). **별도 timeout 이벤트 없음 — 여기만.** |
| `reject_chat_rate_limit` | `ValidateCheer` — `!isVoice` && rate limit (`_chatRateEnd`) |
| `reject_voice_no_match` | `CheerKeywordEngine` — Vosk 미매칭 시 **Client → Host RPC** |
| `chat_used_count` | `InGameChatUI` — `/cheer` 파싱 성공 1회마다 +1 (Host 집계) |

**제외:** `reject_invalid_target` — **수집하지 않음**.

Host `ValidateCheer` false 반환 시 **reason enum**으로 위 컬럼 중 하나만 +1.

**`TelemetryRejectReason` enum (구현용 — 컬럼명과 1:1):**

```csharp
public enum TelemetryRejectReason
{
    SelfCheer,        // → reject_self_cheer
    TargetBuffed,     // → reject_target_buffed
    Timeout,          // → reject_timeout
    ChatRateLimit,    // → reject_chat_rate_limit
    VoiceNoMatch,     // → reject_voice_no_match (Client → Host RPC)
}
```

##### 전송 타이밍 · upsert

매 전송은 **그 시점까지의 누적 스냅샷** 전체를 보냄 (예: death 5 → 10이면 **같은 `sessionId` 행**만 10으로 갱신).

| 트리거 | upsert |
|--------|--------|
| **30초마다** | ✅ (Inspector `flushIntervalSec`, 기본 30) |
| **씬 전환** (스테이지·보스·M→T·T→End) | ✅ |
| **타이틀 복귀** 씬 전환 | ✅ |
| **스테이지 씬 리로드**(사망) | ✅ |
| **세션 끝** (`TitleReturnFlow`) | ✅ **마지막 flush** |
| `Application.quitting` | ✅ 가능한 범위 동기 전송 (보조) |

**Apps Script upsert:** POST body JSON → `token` 검증 → `sessionId` 검색 → 있으면 **Update**, 없으면 **Append**.  
Web App URL은 **Steam Playtest·정식 빌드** 설정에만 (에디터·localhost Inspector 기본 empty).

실패 시 **1~2회 재시도** 후 포기 (영구 로컬 큐는 MVP 범위 밖).

##### 멀티 · 솔로

| | |
|--|--|
| **멀티** | **Host만** `TelemetryService` 전송. Client → Host `TelemetryReportServerRpc(reason)` 등으로 reject/chat +1만. |
| **솔로** | NGO Host 1인. `playMode=Solo`, `partySize=1`. 멀티와 동일 코드 경로. |
| **멀티 1인** | `playMode=Multi`, `partySize=1`. |

##### 게임 연동 훅 (구현 체크리스트)

| # | 훅 | 동작 |
|---|-----|------|
| 1 | **M.*/T.* `SceneManager.sceneLoaded`** (Host·솔로) | 세션 시작(첫 `M.Stage1`만), 해당 바이옴 `death_count +1`, dwell 타이머 시작 |
| 2 | **씬 unload / 다음 씬 로드 직전** | 떠나는 씬 dwell을 해당 바이옴에 확정 |
| 3 | **`End.Demo` sceneLoaded** (Host·솔로) | `run_complete = true` |
| 4 | **`TitleReturnFlow.ExecuteReturn()`** (Host·솔로) | `quitAt` = 현재 바이옴/`End`, **세션 끝 upsert**, 세션 상태 리셋 |
| 5 | **`CheerService`** (Host·솔로) | reject reason별 +1, `ApplyBuff` 시 해당 바이옴 `buff_count +1`, timeout +1 |
| 6 | **`CheerKeywordEngine`** (Client) | 미인식 → Host RPC |
| 7 | **`InGameChatUI`** | `/cheer` 성공 시 `chat_used_count +1` |
| 8 | **`TelemetryService.Update`** | 30초 주기 flush |

`TitleReturnFlow`에 직접 삽입 또는 `ISessionResettable` / 전용 콜백 등록 — **게임 코어에 Sheets URL 흩뿌리지 말 것**.

##### Google Sheets · Apps Script (구현 전 선행)

1. Sheet 생성 → **헤더 1행** §컬럼 표와 **동일**하게 입력.
2. **Apps Script** `doPost(e)`: JSON 파싱 → `token` 검증 → `sessionId` upsert.
3. **Deploy → Web app** → URL 확보.
4. Unity: `GoogleSheetsSink`에 URL + token (**Steam Playtest·정식 빌드** ScriptableObject 또는 `Resources` — 에디터·localhost 기본 empty).

##### MVP 완료 판정

- [ ] Steam **Playtest** 빌드 1판: Sheet에 **행 1개**, `sessionId` upsert 동작 (30초·리로드·종료 시 값 갱신).
- [ ] 멀티 Host: Client reject/chat 합산 **1행**에 반영.
- [ ] 에디터 Play / Dev Build localhost: **행 추가 없음**.
- [ ] payload에 **금지 필드** 없음.

#### 0.5.2 스크린샷

| 시점 | 목적 |
|------|------|
| §0.5 #10 (2인 Dev Build 후) | 스토어 **초안** — 플레이 가능 확인용 |
| §0.5 #18 (Steam Playtest 후) | **최종** — capsule·헤더·실플레이 품질 |

---

## 1. 기술 스택

| 항목 | 개발 ①② | **Playtest·정식 배포 ④** | 정식 |
|------|---------|-----------------|------|
| 네트워크 | **NGO** | **NGO** | 동일 |
| 연결 | `UnityTransport` **localhost** (**7777**) | **Steam P2P + Lobby** | 동일·안정화 |
| 권한 | §9.0 매트릭스 (**이동=Owner+CNT**, 판정=Host, 발사체 비행=Client B안) | 동일 | 동일 |
| 최대 인원 | 4인 | 4인 | 동일 |

- Transport **교체 가능**하게 분리 (`UnityTransport` ↔ Steam Networking). **Playtest·정식 = Steam transport 필수.**
- 중간 참가(Late Join) **없음**. 재접속 **미지원**. 호스트 마이그레이션 **없음**.
- **이탈 정책:** Host 또는 Client **누구든** 나가면 **즉시 방 종료** → 전원 타이틀. 남은 인원으로 계속·재입장 **없음**.

---

## 2. 씬 흐름

### 2.1 멀티플레이

```
0.Title → 1.Lobby → [Release: Tutorial] → M.Stage1…5 → M.Boss → T.Stage1…5 → T.Boss → End.Demo → 0.Title
```

| 씬 | 역할 |
|----|------|
| `0.Title` | `NetworkManager`, `GameSession`, `SceneFlowManager` (DDoL), Host/Join |
| `1.Lobby` | Steam Lobby, Ready, 캐릭터 선택(선착순), Host Start |
| Tutorial | **Release Must** — 조작·말해보기 (Playtest 오픈 빌드에서는 생략 가능) |
| `M.Stage1`…`M.Stage5` / `M.Boss` | M 바이옴 + 보스 |
| `T.Stage1`…`T.Stage5` / `T.Boss` | T 바이옴 + 보스 |
| `End.Demo` | 클리어 UI → 타이틀 복귀 (씬명 레거시) |

`SceneFlowManager.sceneSequence` 권장 순서:  
`M.Stage1`…`M.Stage5`, `M.Boss`, `T.Stage1`…`T.Stage5`, `T.Boss`, `End.Demo`.

### 2.2 솔로 (1인 Host)

```
0.Title → 1.Lobby (Host 1인, 즉시 CanStart) → (동일 스테이지 시퀀스) → End.Demo → 0.Title
```

- **NGO 사용.** `LobbyMode.OnlineHost` + `partySize=1`. 멀티와 동일 코드 경로.
- 로비에서 Start 즉시 가능 (`LobbyNetworkManager.CanStart()` — Host 1인이면 즉시 true).
- 1인 전용 규칙: `GameSession.ActivePlayerCount == 1`
  - `CheerService.ValidateCheer`: self-cheer 허용
  - `GetRequiredVotes()`: `max(1, 0) = 1` → 1표로 버프 발동

---

## 3. DontDestroyOnLoad (Title부터)

`0.Title`에 배치 후 세션 종료까지 유지:

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
**※ Steam Playtest·정식 배포·플레이어 멀티에는 사용하지 않음** → §4.2.

**개발자 테스트:** ParrelSync(①) → Dev Build ② (같은 PC 2 exe). **실제 테스트 가능한 방법은 이 2가지뿐** — 상세: §6A.3.

> **코드 참고:** `LanDiscovery`(UDP 47777, 룸코드→IP 해석)가 존재하나, 이는 같은 PC/세션 내 편의 기능일 뿐 **물리적으로 분리된 2PC 간 실사용 LAN 연결 테스트 수단이 아니다** (미지원/미검증). Steamworks 연동 전까지 개발자 검증은 ①②로만 한다.

### 4.2 Steam P2P + Lobby (**Open Must**, §0.2 ④)

- **Steamworks** 초기화 + **Steam Networking** transport + **Steam Lobby**.
- Join: Lobby 코드 / 친구 초대 (Playtest 다운 후 Invite).
- UI 마스킹 예: 식별자 `7**1` 형태.
- **Depot 업로드** 후 Steam 클라이언트에서 실행 — **원격 2~4인** 협동·응원·보이스 검증 환경.
- **개발자 2PC:** 일상 QA = **2인** Steam Join. 오픈·Playtest 중 **4인 1회** 권장 (§0.2.1).

---

## 5. 타이틀 UI

| 버튼 | 동작 |
|------|------|
| 게임 만들기 | Host → `1.Lobby` |
| 게임 참여 | Client, 룸코드 입력 → `1.Lobby` |
| **게임 만들기 (솔로 포함)** | Host 시작 → `1.Lobby` (NGO OnlineHost) |

---

## 6. 로비 규칙 (`1.Lobby`)

| 규칙 | 내용 |
|------|------|
| 인원 | **1~4인 가변** (빈 슬롯 허용. 2인이면 2슬롯만 사용 가능) |
| Ready | **클라이언트 전원** Ready. **호스트는 Ready 불필요** (Start 조건에서 제외) |
| Ready 취소 | 가능 |
| Start | **호스트만**. 클라이언트 전원 Ready + **4색 중복 없음** + **CheerName 중복 없음** |
| 캐릭터 | 자유 선택 (중복 허용). **Start 시 색·CheerName 중복 없어야 활성화**. **Ready 후 변경 불가** |
| 빈 슬롯 UI | `Empty` |
| Kick (**로비 전용**) | **호스트만**, Ready 전/후 모두. **즉시 해당 슬롯만 비움** — **방은 유지**, 남은 인원 계속 Ready |
| (참고) 인게임 이탈 | 로비 Start **이후** 누구든 끊기면 §12 — **방 전체 종료**. 로비 Kick과 다름 |
| 호스트 | Host는 readyRoot 숨김. 드롭다운으로 캐릭터·색 자유 선택 |

---

## 6A. 룸·세션 수명주기 축 (SSOT)

> **한 줄:** 진입(Connect) → 로비(Lobby) → 시작 게이트(Start Gate) → 인게임(InGame — 내부에서 §11 플레이어 축이 씬마다 반복 재진입) → 종료(Leave/SessionEnd) → ①로 재진입.
> 이탈·클리어·Host Quit **전부 같은 종료 문(⑤)**으로 들어간다. 평행 종료 경로 없음.
> §4·§5·§6·§12는 각 칸의 **세부 규칙**이고, 이 절은 그 위의 **축 골격**이다. 세부는 해당 절 참조, 중복 서술하지 않음.

### 6A.0 축 (5칸 · 일방통행)

```
① Connect → ② Lobby → ③ Start Gate → ④ InGame → ⑤ Leave/SessionEnd
                                                        │
                          (이탈·Host Quit·클리어 전부 여기로 재진입) ── ①로 (0.Title)
```

| 칸 | 불변식 (칸이 끝나면 참) | Writer (여기만 진실) | 상세 |
|----|------------------------|----------------------|------|
| ① Connect | Host 시작 또는 Client 룸코드 접속 완료 | `NetworkManagerSetup` (`StartHost`/`StartClient`) — `TitleMenuController` 경유만 | §4, §5 |
| ② Lobby | 슬롯 배정, Ready/색/CheerName 반영. 로비 이탈=슬롯만 비움(**방 유지**) | `LobbyNetworkManager` 유일 (`OnClientJoined`/`OnClientLeft`/`SetReadyServerRpc`/`SetColorServerRpc`/`SetCheerNameServerRpc`/`KickPlayerServerRpc`) | §6 |
| ③ Start Gate | Host만 발동. `CanStart`(클라 전원 Ready + 색·CheerName 중복 없음, 1인이면 즉시 true) 통과해야 진행. 통과 시점부터 인원/색/CheerName **동결** | `LobbyNetworkManager.StartGameServerRpc` 유일 | §6 |
| ④ InGame | 세션 진행 중(M/T 스테이지+보스). 룸 구성(인원/색) **불변** — 이 구간엔 kick/late join/재접속 없음 | 없음(룸 레벨) — 씬 단위 진실은 §11 플레이어 축이 담당 | §11 |
| ⑤ Leave/SessionEnd | 이탈(Host/Client 누구든)·Host Quit·클리어 전부 같은 문. `Shutdown` + 세션 리셋 후 ①로 재진입 | `TitleReturnFlow.Request` 유일 (`ExecuteReturn`은 내부) | §12 |

### 6A.1 ⑤로 들어오는 문 — 전부 `TitleReturnFlow.Request` 경유

| 문 | 경로 | Reason |
|----|------|--------|
| 클리어 | `EndDemoController` → `End.Demo` 복귀 버튼 | `EndDemo` (`FullRunReset`) |
| Client 이탈(본인이 끊김을 감지) | `DisconnectManager.OnClientLeft` | `ClientDisconnected` |
| Host 이탈/Quit | `DisconnectManager.OnClickLeaveRoom` → 타 Client에 `NotifyAllReturnClientRpc` 통지 | `HostQuitRoom` |
| 로비 Quit | `LobbyMenuController.OnClickQuit` | `LobbyQuit` |
| 로비 중 연결 끊김 | `LobbyMenuController.OnNetworkDisconnected` | `ClientDisconnected` |

이 문들 **외**에 `NetworkManager.Shutdown()` 직접 호출 금지 — 전부 `NetworkManagerSetup.Shutdown()`을 거치고, 그 호출은 `TitleReturnFlow.ExecuteReturn` 내부 1곳뿐이어야 한다.

### 6A.2 로비 Kick vs 인게임 이탈 — 구분 (혼동 금지)

| | 로비 Kick (②) | 인게임 이탈 (⑤) |
|--|--|--|
| 트리거 | **Host가 대상을 지정**해 강퇴 | 누구든 연결 끊김/Quit (본인 의사와 무관해도 발생) |
| 결과 | 슬롯만 비움, **방 유지**, 남은 인원 계속 Ready | **방 전체 종료** → 전원 타이틀 |
| API | `LobbyNetworkManager.KickPlayerServerRpc` | `DisconnectManager` → `TitleReturnFlow` |
| 인게임에 존재하는가 | **아니오** — 로비 전용 | 이게 인게임에서 "누가 빠지는" **유일한** 경로 |

**인게임 Kick(강퇴/Ban)은 존재하지 않고, 앞으로도 추가하지 않는다.** Host가 인게임 중 특정 Client를 강제로 내보내는 기능은 로비에만 있다. 인게임에서 누군가 빠지면(자의든 타의든, 인터넷 문제·개인 사정 등) 이는 **"이탈"**이며, §12 규칙대로 **방 전체가 종료**된다 — "Kick"이라는 별도 기능이 아니다.

### 6A.3 개발 환경 연결 방식 — 실제 가능한 것만 (§4.1 보강)

현재 실제로 검증 가능한 개발자 테스트 방법은 **2가지뿐**이다 (§0.2와 동일):

| 방법 | 실제 동작 |
|------|----------|
| ① ParrelSync | 에디터 Host + Clone Client, **같은 PC** |
| ② Dev Build | Host EXE + Client EXE, **같은 PC** localhost:7777 |
| 물리적으로 분리된 2PC 간 LAN 연결 | **테스트 안 됨 / 미지원** |
| ④ Steam P2P | 아직 미구현 |

`LanDiscovery`(UDP 47777, 룸코드→IP 해석)가 코드에 있지만, 이는 같은 PC/세션 안에서의 편의 기능이고 **실사용 LAN 2PC 연결 테스트 수단이 아니다.** Steamworks(§0.2 ④)가 붙기 전까지 개발자 검증은 **①②만** 사용한다.

### 6A.4 금지 (평행 경로 — 발견 즉시 삭제)

| 항목 | 이유 |
|------|------|
| 인게임 Kick(강퇴) 기능 추가 | 로비 전용 (§6A.2). 인게임에 만들지 않음 |
| Late Join / 재접속 / 호스트 마이그레이션 | §12 미지원 정책. 코드에도 없음(확인됨) — 추가 금지 |
| `NetworkManager.Shutdown()` 직접 호출 | ⑤ Writer(`TitleReturnFlow`) 우회 — 금지 |
| `LobbyNetworkManager`의 로비 이탈/Kick 경로를 인게임 이탈에 재사용 | ②/⑤ 별도 유지, 섞지 말 것 |
| 인게임(④) 중 인원/색/CheerName 변경 | ③ Start Gate 통과 후 동결 |
| 실사용 LAN 2PC 연결을 정식 테스트/배포 수단으로 취급 | §6A.3 — 미지원. ①②만 |

### 6A.5 증상 → 볼 칸 (진단 사다리)

| 증상 | 먼저 볼 칸 |
|------|-----------|
| Ready 눌러도 Start 안 됨 | ③ `CanStart` (Ready 전원? 색·CheerName 중복?) |
| 로비에서 나갔는데 방이 통째로 터짐 | ②/⑤ 혼동 — 지금 로비인지 인게임인지, 호출된 게 `LobbyNetworkManager.OnClientLeft`인지 `DisconnectManager`인지 확인 |
| 인게임 중 한 명 나갔는데 계속 진행됨 | ⑤ `DisconnectManager.OnClientLeft` 콜백 등록 여부 |
| 타이틀로 안 돌아가고 멈춤 | ⑤ `TitleReturnFlow.Request` 호출 여부 / `Shutdown` 완료 여부 |
| "인게임에서 Kick하고 싶다"는 요청 | 의도된 미지원(§6A.2) — 버그 아님, 구현하지 않음 |
| 재접속/Late Join 요청 | 의도된 미지원(§12) — 버그 아님, 구현하지 않음 |
| 개발 중 다른 PC로 접속이 안 됨 | §6A.3 — 의도된 제약. ①②만 사용, LAN 실사용 기대하지 말 것 |

### 6A.6 검증 (ParrelSync 2인)

1. Title → Host 생성 → Client 룸코드 접속 → Lobby 슬롯 반영 (①→②)
2. 색·CheerName 중복 상태에서 Start 시도 → 막힘 확인 → 해소 후 Start (③)
3. 인게임 중 Client 강제 종료(연결 끊기) → Host 포함 전원 타이틀 복귀 확인 (⑤)
4. 인게임 중 Host 종료 → Client `NotifyAllReturnClientRpc` 수신 후 타이틀 복귀 확인 (⑤)
5. 클리어(`End.Demo`) → 타이틀 복귀 버튼 → `GameSession`/`SceneFlowManager` 리셋 확인 (⑤→①)
6. `grep`: 게임 코드 내 `NetworkManager.Shutdown()` 직접 호출 — `NetworkManagerSetup` 내부 1곳 제외 **0건**

---

## 7. 플레이어 · 스폰

### 7.1 Prefab

- 씬에 Player 4개 배치 **제거** (`M.Stage1` 등).
- **NetworkObject Player Prefab 1개** + 스폰 시 `Configure(color, playerId, stats)`.
- **활성 슬롯(선택된 색)만** 스폰.

### 7.2 스폰 위치

- **`ColoredStartZone.spawnPoint`** (존 **위**)에 배치.
- 리스폰 좌표는 `PlayerSpawnManager.fixedSpawnPositions`가 전담 (§11). Zone은 시작 게이트 판정만.
- 존 트리거 진입 → 점유 → `StageStartGate` 카운트다운 (전원 점유 시 진행).
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

---

## 8. 씬 로드 · 진행

- Host가 `NetworkSceneManager.LoadScene` (로비→스테이지, 스테이지 전환, 리로드).
- `SceneFlowManager.LoadNextScene`: `sceneSequence` 순서  
  (`M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`).
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

### MVP 동기화 대상

**우선순위:** `Must (Open/Playtest)` → `Should (여유)` → `Post (출시 이후)`

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

| 그룹 | 패턴 | 이번 라운드(M) 대상 | T 전용 — 별도 라운드로 미룸 |
|------|------|-------------------|---------------------------|
| **1** | B 함정·피격 | `ArrowTrap`, `DropTrap`, `TrapProjectile`, `WindTrap`, `ContactDamage`(M.Stage3에도 있음), `TrapBase`, `TrapPlayerTracker`, `Breakable`, `Stage5ChaserHitbox` (+ `CeilingTrap`/`TrapSpeedPhase`/`SpikeTrap`은 현재 씬 배치 미확인 — 작업 중 재확인) | `SpikeLane`/`SpikeLaneField` (`T.Stage3`, `T.Boss`만 확인됨) |
| **2** | E 월드 모션 | `AdvancingWall` (**`M.Stage3` 사용, `T.Boss`에서도 재사용되므로 여기서 검증해두면 T 쪽도 절반 커버됨**) | `WallMover`, `WallMoverSequencer`, `BoulderSpawner`, `BoulderSpawnManager`, `WaypointMover`(Boulder 프리팹에 내장), `WallWaveController`, `WallLineRandomizer`, `MovingCorridor`, `AdvancingWallTelegraph` — 전부 `T.Stage1`/`T.Stage3`/`T.Stage4`/`T.Boss`에서만 확인됨 |
| **3** | A 연출 껍데기 | `MouthTrapAnimator`(+`MouthTrapAnimatorAnim`), `MouthWindAnimator`, `MouthExitTrigger`, `ColoredDoorVisual`, `ColoredPadVisual`, `RingBlendShapePulse` 등 — M 인스턴스 위주로 확인 | (그룹 3은 네트워크 진실이 없다는 것만 확인하는 가벼운 감사라 M/T 구분 없이 봐도 무방) |

**그룹 1(B)은 M 트랩 인스턴스로 별도 에이전트가 진행 중.** 그룹 2(E)는 `AdvancingWall` 1개만 M이고 나머지 8개는 전부 T 전용이므로, **`AdvancingWall`은 그룹 1(B) 세션에 같이 묶고, 그룹 2(E) 세션은 T 전용 나머지만** 다루는 것을 권장 — 그러면 "패턴 E 세션 = 순수 T" 경계가 정확히 맞아떨어진다.

#### 9.1.4 M 씬 작업 순서 (확정)

F·D 공통은 **검증 완료** 전제. 이후 **씬 단위**로 C(·필요 시 B/E)만 붙인다.

| 순 | 씬 | C (챌린지) | B / E (같이 볼 것) |
|----|-----|------------|-------------------|
| 1 | `M.Stage2` | **OXQuiz** — **완료 (§11B로 승격)** | Drop |
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
Owner  = 이동(CNT) · 키 입력 · 카메라 · 애니/SFX 연출 · 로컬 마이크/응원
Client = 발사체 로컬 비행 (+ 트리거 감지 → ServerRpc 보고). VFX는 Rpc/로컬
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
| 일반 데미지 | `NetworkDamageUtil.ApplyDamage(player, amount, knockback)` |
| 즉사 (문 등) | `NetworkDamageUtil.ApplyInstantKill(player)` |
| 순수 넉백 (HP 미변경) | `NetworkDamageUtil.ApplyKnockback(player, direction, force)` — Breakable 범위 넉백, `PlayerPunch` PvP 넉백 등 (§7.4) |
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
| 4 | 문 즉사 | `DoorController.cs` — `ApplyInstantKill` 유지, 서버 충돌만 |
| 5 | 낙사 | `Player.cs` — Owner Y 1회 신고 `ReportFallDeathServerRpc` → Host `ApplyFallDeathFromServer` 확정. Host `Update` Y는 폴백 유지 (2026-07-16: Host 단독 Y 판정은 Client void 낙사를 놓쳐 폐기) |
| 6 | 깨진 경로 수정 | `Stage5ChaserHitbox.cs` → `ApplyDamage` + 피격 시 `_chaser.NotifyHitFromHitbox()` (서버에서) |
| 7 | Breakable | `Breakable.cs` — `ApplyDamageFromServer` 직접 호출 → util/Host 규칙 통일 |
| 8 | 이미 Host 경로 (확인만) | `Enemy.cs`, `EnemyHitbox.cs`, `OXQuizManager.cs`, `Player.OnTriggerEnter`(EnemyBullet) |
| 9 | EnemyBullet | 서버에서 플레이어 `Rigidbody` 동적 유지, Trigger 판정 **서버만** 유효한지 프리팹·레이어 점검 |
| 10 | `WindTrap` | Owner 힘 예외 제거 → **Host**가 힘/속도 적용 (Should) |
| 11 | 주석 | Owner+CNT · §9.0.1 B안과 맞게. CNT 삭제/Host 이동 문구 **넣지 말 것** |

#### 9A.5.2 Phase 1 완료 판정

- [ ] `grep ApplyDamageWithOwnerReport` / `ReportHitServerRpc` / `ReportInstantKillServerRpc` — **프로젝트 0건** (`ReportFallDeathServerRpc`는 낙사 한정 허용 — §9A.3)
- [ ] ParrelSync **2인**: 화살·가시·ContactDamage·문즉사·낙사·Enemy·Chaser 각 **1회 이상** — HP·리로드 정상
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
- [ ] ParrelSync 2인 → Dev Build localhost 2인 → **Steam 원격 2인** 순서 통과 (§0.2)
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

**즉사 (문):**

```
[Host Server]   Door OnCollision (IsServer) → ApplyInstantKill → _hp=0 → ForceInstantKillClientRpc
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

**Open/Playtest 진행 — §9A:**

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

**Q. Phase 2 없이 Playtest/정식?**  
A. **의도된 방침.** 이동은 Owner+CNT 유지. Phase 1+발사체 B만 Must.

**Q. 2인 OK면 4인도 OK?**  
A. §0.2.1과 동일.

---

## 10. Random · 시드

| 상황 | 시드 |
|------|------|
| **사망 리로드** | **매번 새 시드** (퍼즐 배치·랜덤 연출 변경) |
| 로비 Start (첫 진입) | Host가 세션 시드 생성 |

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

| 문 | 경로 | 비고 |
|----|------|------|
| 로비 Start | `LobbyNetworkManager.StartGameServerRpc` → `LoadScene("M.Stage1")` | Coordinator 스폰(DDOL) 포함 |
| **사망 · ESC Reset** | Owner `RaiseDied` → `StageResetOnPlayerDeath` → `StageNetworkState.NotifyPlayerDeathServerRpc` → Host `LoadScene(현재씬)` | **1명 사망 = 전원 리로드** + **새 시드** 배포. ESC Reset(`EscMenuController.OnClickReset`, Host 버튼)도 **같은 문** 사용 (2026-07-17 통일) |
| 클리어 | `StageManager.OnStageClear` / `PhaseManager.onAllPhasesComplete` → **`SceneFlowRelay.LoadNextScene`** → `SceneFlowManager` | **확정 배선: Relay 경유** (씬에서 SceneFlowManager 직결 금지 — DDOL이라 Inspector 연결 불가) |

이 3곳 **외의** 스테이지 `LoadScene` 호출 금지. Client가 씬 로드 금지.

### 11.2 사망 루프 상세 (잠금 유지 항목)

- **1명 사망 = 전원 씬 리로드** (`StageResetOnPlayerDeath`). 리로드 후: 존 위 재스폰, `StageStartGate` 재진행, **새 시드** 퍼즐 재배치 (§10).
- 낙사 확정: **Owner** Y 신고 (`ReportFallDeathServerRpc`) → **Host** HP 0 확정 (§9A.3). Host 단독 Y 판정은 Client void 낙사를 놓치므로 사용하지 않음.
- 리스폰 = **씬 리로드가 전부**. `destroyWithScene:true`로 옛 플레이어 자동 Despawn → ②에서 클린 스폰 → HP/포즈/색이 초기 상태. 별도 리셋 코드 불필요.
- `Player.IsDead`: 애니메이션·콜라이더·물리 정지는 `Die()`를 통해 **Owner 머신에서만** (Fix A, 의도된 설계). 단 Host는 원격 플레이어 Rigidbody도 직접 시뮬레이션(§9A)하므로, 비오너 머신에서도 `IsDead` 플래그만 별도 동기화(`Player.SyncDeadFlag()`, 2026-07-17) — 트랩·피격 판정이 사망 상태를 인지하도록. HP NetworkVariable이 실질 가드라 지금까지 증상은 없었음, 방어 차원.

### 11.3 ⑤ Play Consumers (Ready 구독만 — 나열은 목록, **실행 순서 아님**)

`NetworkPlayerSetup`(카메라 bind) · `GameSession` · `StageResetOnPlayerDeath` · `ColoredStartZone` · `StagePressurePadSetup` · `TrapPlayerTracker` · `PlayerHPUI` · `TeamStatusUI` · `CheerProgressUI` · `ChangeColorCooldownUI`

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

규칙: 한 칸씩 위로. 깨진 불변식이 설명되면 **정지**. 그 칸 Writer(+같은 가정 Consumer)만 고침. 칸에 복구 if 추가 금지. 코드 수정 전 Broken step/근거/Fix plan 제시.

### 11.6 검증 (Foundation — ParrelSync 2인)

1. Title → Lobby → M.Stage 진입: 이동·카메라·HP UI
2. Host 사망 1회 → 리로드 → 인원수 스폰 → Ready → 카메라/HP 정상
3. Client 사망 1회 → 동일
4. 클리어 1회 → 다음 씬 → 같은 축 재통과
5. `grep`: `Player.Respawn` / `ForceRespawn` / `ReloadCurrentScene` — 프로젝트 정의·호출 **0건** (삭제 완료 상태 유지)

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
- `StageStartGate` (Host `Update`): `AllZonesOccupied()` → `MarkCountdownStart()`(NV) → 타이머 → `CompleteCountdown()` → `stageManager.StartStage()` + Host가 `_stageStartServerTime` NV 기록.
- Client는 `_stageStartServerTime` NV 감지로 자기 화면에서도 `StartStage()`를 부른다 — **이건 ②Start 진입 트리거 전파일 뿐, ③Progress 판정에는 관여하지 않는다.** ②의 `objectives.Begin()`이 Client 로컬에서도 돌아가더라도, 그 이후 Complete/Fail 판정의 진실은 오직 Host.

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

규칙: 한 칸씩 위로. 깨진 불변식이 설명되면 **정지**. 그 칸 Writer만 고침. 칸에 복구 if 추가 금지.

### 11A.6 §11(플레이어 축)과의 관계

- 이 축은 §11 **"⑤ Play"** 구간의 스테이지 콘텐츠 세부 축이다. §11의 ①~④(Load/Spawn/Owner/Ready)는 그대로 선행 조건.
- **Fail Exit은 §11 사망 문을 그대로 재사용** — 새 사망/리로드 정의 금지.
- **Clear Exit은 §11 ①Load에 재진입**(다음 씬) — 새 씬 전환 정의 금지.
- 즉 §11A는 §11에 새 문을 추가하지 않는다. 기존 두 문(사망 문 / ①Load 문)에 스테이지 콘텐츠가 **어떻게 도달하는지**만 정의한다.

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
| `ChallengeStepState { seed, stepIndex, stepStartServerTime }` | `NetworkVariable` (Server write, 1개로 통합) | ②RoundStart 원자적 배포 |
| `ChallengeSeed` / `ChallengeStepIndex` / `ChallengeStepStartServerTime` | 읽기 프로퍼티 | ③Generate·④Judge 타이머 기준 |
| `IsChallengeCleared`(`_challengeCleared` NV) | `NetworkVariable<bool>` (Server write) | ⑤Resolve 클리어 연출 신호 |
| `ChallengeStart(seed)` / `ChallengeStepBegin(stepIndex)` / `ChallengeCleared(bool)` | Host 전용 메서드 | Writer |
| `OnChallengeStepChanged` / `OnChallengeClearedChanged` / `OnChallengeOutcome` | 이벤트 | 전 챌린지 매니저 공통 구독점 |
| `NotifyChallengeOutcomeClientRpc(bool success)` | `[ClientRpc]` | ④Judge 결과 1회성 연출(Client만 재생 — Host는 로컬에서 직접 처리하므로 스킵) |
| `SubmitStepServerRpc(PlayerColorType color)` / `SubmitAnyKeyStepServerRpc()` | `[Rpc(SendTo.Server)]` | §11B.1 — Client 입력 제출 → Host가 `SequenceRingMinigame.Instance.TrySubmit()`/`TrySubmitAnyKey()` 호출 |
| `OnChallengeTimeSync` / `SyncChallengeTimeClientRpc(float remaining)` | 이벤트 / `[ClientRpc]` | §11B.1 — 이벤트 기반 변동(페널티)이 있어 ServerTime 역산이 불가능한 연속 타이머 전용(SequenceRing). Host가 직접 tick + 주기 브로드캐스트 |

### 11B.3 4개 챌린지 → 이 축 매핑

| 챌린지 | ①Trigger | ②RoundStart 시드로 대체할 것 | ④Judge | 상태 |
|--------|----------|------------------------------|--------|------|
| **OX Quiz** | 배리어 진입 트리거 | `RegenerateQuestionOrder()`(`System.Random(seed)`) | `JudgeByPosition()`(물리 오버랩) | **잠김·검증 완료** (`OXQuizManager`, `M.Stage2`) |
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

---

## 12. 이탈 · 세션 종료

> **확정 정책:** 재접속·Late Join·호스트 마이그레이션 **전부 미지원**. 구현·제안하지 않음.

### 12.0 세션 이탈 규칙 (Playtest·정식 공통)

| 상황 | 동작 |
|------|------|
| **호스트 이탈** | **즉시 방 종료** → 전원 타이틀 |
| **클라이언트 이탈** | **즉시 방 종료** → 전원 타이틀 (Host와 동일) |
| **재접속** | **미지원** (유예·스냅샷·슬롯 복귀 없음) |
| **호스트 마이그레이션** | **없음** (Host 나가면 방 폭파) |
| **Late Join** | **없음** |
| **Kick (인게임)** | **기능 자체가 없음** (§6A.2). Host가 인게임 중 특정 Client를 강제로 내보내는 UI/API 없음 — 앞으로도 추가 안 함. 있는 건 **이탈**(연결 끊김/Quit)뿐이며 발생 시 §12 규칙대로 **방 종료** |
| **Kick (로비)** | §6 — **슬롯만 비움**, 방 유지. §12(인게임 이탈)와 **다름** |

### 12.1 구현 시 주의

- **로비 Kick(§6)** = 슬롯만 비움. **인게임 이탈/Kick(§12)** = 방 전체 종료. 섞지 말 것.
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
| `GameSession`, `SceneFlowManager` → `0.Title` | **수동 (기획/에디터)** |
| 스테이지 씬 내 Player 프리팹 인스턴스 제거 | 구현 시 |
| Network Player Prefab 생성 + NetworkManager 등록 | 구현 시 |
| `End.Demo` 씬 (Build Settings 등록) | 구현 시 |
| `sceneSequence`에 M1–5·M.Boss·T1–5·T.Boss·`End.Demo` | `SceneFlowManager` |

---

## 16. 구현 순서 (권장)

### 16.1 네트워크 · 응원 · Steam (Open / Playtest)

> **현재 실행 체크리스트:** §0.5.  
> **Authority:** §9.0 확정 (**이동=Owner+CNT**, 발사체=B안). Phase 2 이동 Host화 **폐기**.

1. NGO + `UnityTransport` + Title `NetworkManager`
2. 로비 Ready / 캐릭터 / Start 동기화
3. Player Network Prefab + 존 스폰 + Owner 입력·카메라·**Owner 이동(CNT)**
4. **§9A Phase 1** — 데미지·함정 Host 파이프라인 (ParrelSync / Dev Build 2인)
5. **§9.0.1 발사체 B안** — Host Spawn+velocity / Client 비행 / Client 보고→Host 피격
6. **Must 동기화** (§9 표) — WindTrap Host 힘 포함
7. **ParrelSync ①**
8. **Development Build ②** — localhost **2인** (중간 게이트)
9. **응원** — CheerService, Dissonance, Vosk (`CheerAndTutorialDesign.md`)
10. **Steamworks** — P2P transport, Lobby, Depot
11. **텔레메트리** — §0.5.1 (**Open Must**)
12. **Steam ④** — **2인 Must** + 4인 1회 권장 → **Coming Soon + Playtest (D14)**
13. M 풀코스+보스 / T 풀코스+보스 (`sceneSequence`) + `End.Demo`

### 16.2 Release (정식 · D28–D30+)

1. 난이도 밸런싱 (Playtest M주·T주 피드백)
2. Tutorial (연습·말해보기) + CheerName 발음/G2P polish
3. UI 옵션 (볼륨·해상도)
4. Steam Invite UX polish
5. 출시 QA · **법인 계정** 빌드 리뷰 · 정식 출시

### 16.3 Post-Launch

- 관전(Spectator) — **후보** (일정 여유 시)
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
A. **아니오.** Lobby Host 1인(`partySize=1`)과 동일 경로.

**Q. 솔로 색상은 어디서 고르나?**  
A. **Lobby** 캐릭터/슬롯 선택 (멀티와 동일).

**Q. 다른 플레이어를 내 PC에서 조종하나?**  
A. **아니오.** Owner는 자기 캐릭터 **이동·입력·카메라·연출**. **HP·함정·피격 최종**은 Host (§9.0).

**Q. Host Authority면 이동도 Host?**  
A. **아니오.** 이동=**Owner+CNT 확정**. Host는 HP·함정·피격 등 판정.

**Q. Host 판정이면 Client 화면에 안 보이나?**  
A. **보인다.** Host가 판정한 **결과**를 동기화해 전원이 같은 상태를 봄.

**Q. 타이틀 복귀 시 멀티 연결은?**  
A. **`NetworkManager.Shutdown()`** 으로 해제 (TitleReturnFlow / NetworkManagerSetup 경유).

**Q. ParrelSync만 통과하면 Playtest 오픈해도 되나?**  
A. **아니오.** **Steam P2P ④** (2인 Must + 4인 1회 권장) + 응원·보이스 + **텔레메트리**가 오픈 게이트.

**Q. Dev Build ②만 통과하면 Playtest 오픈?**  
A. **아니오.** ②는 **중간 게이트**. Playtest = **Steam 원격** + 협동 + 응원.

**Q. 개발 PC 2대뿐인데 4인 테스트?**  
A. 일상 = **Steam 2인** (§0.2.1). 오픈·M주 중 **4인 1회** — 친구 Playtest 권장. 2인 통과 ≠ 4인 100% 보장.

**Q. 2인 OK면 4인도 OK?**  
A. **연결·Transport·응원 골격**은 2인에서 대부분 검증. **4인 전용** (3표 집계, 4보이스, 4Gate)은 4인 1회 필요.

**Q. discovery / 원격 IP로 테스트하나?**  
A. **안 함.** 실제 검증 가능한 건 **ParrelSync · Dev Build(같은 PC) 뿐** (§6A.3). 물리적으로 분리된 2PC 간 LAN 연결은 미지원·미검증. Steamworks 붙으면 그때부터 ④ Steam P2P.

**Q. 로비 Kick이면 방이 터지나?**  
A. **아니오.** 로비 Kick=슬롯만 비움(§6), 방 유지. **인게임에는 Kick 기능 자체가 없다** (§6A.2) — 있는 건 이탈뿐이고, 발생 시 방 종료(§12).

**Q. 컷씬·관전·이모트를 출시 전에 넣나?**  
A. **컷씬: 안 넣음(영구).** 관전·이모트: **Post-Launch**. 재접속·호스트 마이그레이션·Late Join은 **미지원**(§12). 정식 Must는 Tutorial·밸런싱·옵션 UI·QA. Steam P2P·텔레메트리는 **Open Must**.

**Q. Steam 데모 페이지를 만드나?**  
A. **아니오.** 데모 없음. **Playtest + Coming Soon** → 정식 출시.
