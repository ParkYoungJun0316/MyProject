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

**개발 중 멀티 검증은 아래 순서로만 한다** (원격 IP Join·discovery 없음).

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
| **보장 안 됨 → 4인 전용 버그** | — | `ActivePlayerCount`·집계, 4색 Gate, 4명 보이스 혼잡, 이탈 시 §12 전원 타이틀 수렴 |
| 판정 | **데모 출시 최소 게이트** (2PC 한정) | **홍보 신뢰도** — 친구/플레이테스트 1회 **강력 권장** |

**2인 통과 = 4인 100% 보장 아님.** 다만 NGO·Steam P2P·응원 **연결·규칙 골격**은 2인에서 대부분 검증 가능.  
**4인만 터지는 버그**는 §0.2.1 표 우측 항목 — 데모 직전 **4인 1회**로 잡는다.

### 0.3 데모 vs 정식 출시 — 어디까지 끝내나

#### 데모 출시 (Must Have) — Steam 홍보 데모

- **플레이 경로:** Title → Lobby → `M.Stage1` → `T.Stage1` → `End.Demo` (멀티 **2~4인**)
- **솔로:** Title → Lobby → 동일 스테이지 (**NGO Host 1인**, `partySize=1`)
- **네트워크:** §9 Must 동기화 + **§0.2 ④ Steam P2P + Steam Lobby**
- **응원·보이스:** 인게임 **Dissonance 4인 보이스** + **Vosk 응원** + `/cheer` (→ `CheerAndTutorialDesign.md`)
- **배포:** **Steam** (Depot 업로드). 원격 멀티 = **Steam P2P 필수** (localhost/IP Join 데모 아님)
- **네트워크 검증:** ② Dev Build (중간) → ④ **Steam P2P 2인 Must** + **4인 1회 권장** (§0.2.1)
- **UI:** 타이틀·로비·HP·카운트다운·End.Demo·응원 HUD·채팅 `/cheer`
- **사운드:** BGM 1~2 + 핵심 SFX
- **파티클:** 피격·Break만 (선택)
- **난이도:** “클리어 가능” 수준. 본격 밸런싱은 데모 후
- **피드백:** End.Demo **Discord 피드백 버튼** (수치 텔레메트리는 데모 범위 밖 → 정식 §0.5.1)

#### 데모에서 **의도적으로 빼는 것** (버그·일정 방어)

| 항목 | 이유 |
|------|------|
| §12 재접속·유예·스냅샷·호스트 마이그레이션 | **미지원.** 인게임 이탈 = **방 종료**(전원 타이틀). 재접속 일절 없음 |
| 원격 IP Join / UDP discovery | **미사용.** 개발=ParrelSync·localhost 빌드, 데모=**Steam** |
| **텔레메트리** (§0.5.1) | **정식 Must.** 데모는 Discord 피드백·플레이테스트로 대체 (네트워크·데모 일정 방어) |
| Tutorial 씬(조작 연습) | 정식 (CheerAndTutorialDesign.md §9) |
| *(참고)* CheerName 로비 커스텀 + 불러보기 | **데모 Must** — `CheerAndTutorialDesign.md` §3.2 |
| 관전(Spectator) 모드 | 내부 QA용. Discord 화면공유로 대체 |
| sit / dance 등 이모트 애니 | 정식 이후 |
| 컷씬 | 정식 이후 |
| 옵션·설정 UI 전체 | 정식 (데모: OS 볼륨) |

#### 정식 출시 (데모 후 2주 — Must Have)

- Steam P2P·Lobby **유지·안정화** (데모에서 이미 구현 — Invite UX polish)
- **난이도 밸런싱** (데모 피드백)
- **Tutorial** (연습·말해보기) + CheerName **발음 유사/G2P polish** (`CheerAndTutorialDesign.md` §3 — 로비 커스텀은 데모에 포함)
- **UI:** 옵션(마스터·BGM·SFX), 해상도/전체화면
- **텔레메트리:** §0.5.1 — Steam **정식** 빌드, Google Sheets upsert (이탈·체류·사망·응원 거부 합계)
- **출시 QA** 체크리스트
- (선택) Dissonance **Steam P2P** 음성 transport 분리

#### 정식 2주 안에 **넣지 않는 것** (Post-Launch)

- 컷씬
- sit / dance / 이모트 애니메이션
- 재접속·호스트 마이그레이션·Late Join (**미지원 확정** — 구현하지 않음)
- 파티클 대량 추가

### 0.4 권장 작업 순서 (요약)

**상세 실행 순서·체크 항목은 §0.5 참고.**

```
[데모 — Steam 홍보]
0. 테스트 전 블로커 (Vosk, CheerName, AudioListener)
1. 폴리시 (오디오, 카메라, DialogueUI, End.Demo, 빌드 메타)
2. 로컬 테스트 (1인 → 2인 Dev Build → 스크린샷 1차)
3. Steamworks (App ID · Transport · Lobby · Depot)
4. Steam 테스트 (2인 Must → 4인 권장) → 스토어 → 데모 출시

[데모 후 — 정식]
→ 텔레메트리 MVP (§0.5.1) + Tutorial · 밸런싱 · 옵션 UI 등 (§0.3 정식)
```

### 0.5 데모 출시 전 체크리스트 (실행 순서)

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

#### Phase 3 — Steamworks

| # | 작업 | 비고 |
|---|------|------|
| 11 | Steam App ID + Steamworks | Transport → Steam Networking, Lobby, Depot 파이프라인 |
| 12 | 스토어 페이지 초안 | App ID 필요. 스크린샷·설명은 §0.5.2 참고 |
| — | *(제외)* 텔레메트리 | **정식 Must** — §0.5.1. 데모 Phase에서 구현·전송 **안 함** |

#### Phase 4 — Steam 테스트 → 출시

| # | 작업 | 비고 |
|---|------|------|
| 14 | Steam 솔로/2인 원격 | **데모 출시 최소 게이트** (§0.2 ④) |
| 15 | 버그 수정 | |
| 16 | 친구 4인 테스트 | 3표 응원·4보이스·4Gate — **1회 권장** (§0.2.1) |
| 17 | 버그 수정 | |
| 18 | 스크린샷 최종 + 스토어 마무리 | 실플레이·안정 빌드 기준 (§0.5.2) |
| 19 | Steam 데모 출시 | Depot 업로드 |

#### 0.5.1 텔레메트리 MVP (정식 Must · 데모 제외)

> **범위:** **정식 출시 Must.** 데모에서는 구현·전송하지 않음 (Discord 피드백·플레이테스트로 대체).  
> **구현 에이전트:** 이 절만 읽고 구현 가능. 착수 시점 = **데모 출시 이후·정식 작업**.  
> 순서: ① Google Sheet + Apps Script upsert → ② `TelemetryService` + 게임 연동 (Steamworks 데모 연동과 **독립** — 정식 Steam 빌드에서 전송).

##### 목적 · 시점

- **목적:** Steam **정식** 플레이 1판당 **Google Sheets 1행(upsert)** — 이탈 구간, 스테이지 체류·사망, 응원 거부·채팅 **합계**.
- **시점:** **데모 출시 후** 정식 구간에서 구현. 정식 Steam 빌드·원격 QA **전**에 전송 경로를 켜 초반 정식 유저 데이터를 잃지 않음.
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
| **Steam Depot 데모** | ❌ (데모 범위 밖) |
| **Steam Depot 정식**, Steam 클라이언트로 실행 | ✅ |

**권장 게이트 (둘 다 만족 시 전송):**

1. `#if !UNITY_EDITOR`
2. Steamworks 초기화 성공 (`SteamAPI` 등). 정식 빌드 파이프라인에서만 Scripting Define `TELEMETRY_RELEASE` (또는 동등한 출시 게이트).

Inspector `enabled` 토글은 **로컬 디버그용**. 위 게이트가 **출시 판정** 기준.

##### 세션 생명주기

| | 시점 | 동작 |
|--|------|------|
| **세션 시작** | **`M.Stage1` 첫 로드** | 새 `sessionId`(UUID), 카운터·dwell 타이머 초기화. 멀티=로비 Start 후 / 솔로=오프라인 로비 Start 후 동일. |
| **세션 진행** | M·T 플레이 중 | 누적 카운터 갱신 + 주기 upsert (§전송 타이밍). |
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
| `buildVersion` | string | `Application.version` (예: `0.1.0-demo`) |
| `playMode` | string | `Solo` / `Multi` (멀티 1인도 `Multi`) |
| `partySize` | int | 1~4 |
| `run_complete` | bool | `End.Demo` **씬 진입** 여부 |
| `quitAt` | string | 세션 끝 upsert 시 Host 위치: `M` / `T` / `End` |
| `M_dwell_sec` | float | M.Stage1 체류(초) — §측정 규칙 |
| `M_death_count` | int | M.Stage1 씬 로드(리로드 포함) 횟수 |
| `M_buff_count` | int | M.Stage1 버프 **적용** 횟수 |
| `T_dwell_sec` | float | T.Stage1 체류(초) |
| `T_death_count` | int | T.Stage1 씬 로드 횟수 |
| `T_buff_count` | int | T.Stage1 버프 적용 횟수 |
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
| **dwell** | 해당 스테이지 **씬 로드 직후** ~ **다른 씬으로 나가기 직전**까지 Host `Time.time`(또는 `unscaledTime`) 누적. M→T 전환 시 `M_dwell_sec` 확정, T에서 T dwell 계속. |
| **death_count** | 해당 스테이지 **씬 Load마다 +1** (첫 진입 포함). 사망=전원 리로드이므로 **리로드 1회 = death 1**. 동시 다수 사망도 **+1**. 분석 시 첫 로드 제외하려면 시트에서 -1 (구현은 +1 유지). |
| **buff_count** | 버프가 **플레이어 1명에게 적용될 때마다 +1**. 4인 전원 버프 = **+4**. |
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
| **M → T** 씬 전환 | ✅ |
| **T → End** 또는 **타이틀 복귀** 씬 전환 | ✅ |
| **스테이지 씬 리로드**(사망) | ✅ |
| **세션 끝** (`TitleReturnFlow`) | ✅ **마지막 flush** |
| `Application.quitting` | ✅ 가능한 범위 동기 전송 (보조) |

**Apps Script upsert:** POST body JSON → `token` 검증 → `sessionId` 검색 → 있으면 **Update**, 없으면 **Append**.  
Web App URL은 **Steam 정식 빌드 설정**에만 (에디터·데모 빌드 Inspector 기본 empty).

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
| 1 | **`M.Stage1` / `T.Stage1` `SceneManager.sceneLoaded`** (Host·솔로) | 세션 시작(첫 M만), 해당 스테이지 `death_count +1`, dwell 타이머 시작 |
| 2 | **씬 unload / 다음 씬 로드 직전** | 떠나는 스테이지 dwell 확정 |
| 3 | **`End.Demo` sceneLoaded** (Host·솔로) | `run_complete = true` |
| 4 | **`TitleReturnFlow.ExecuteReturn()`** (Host·솔로) | `quitAt` = 현재 씬(`M`/`T`/`End`), **세션 끝 upsert**, 세션 상태 리셋 |
| 5 | **`CheerService`** (Host·솔로) | reject reason별 +1, `ApplyBuff` 시 해당 스테이지 `buff_count +1`, timeout +1 |
| 6 | **`CheerKeywordEngine`** (Client) | 미인식 → Host RPC |
| 7 | **`InGameChatUI`** | `/cheer` 성공 시 `chat_used_count +1` |
| 8 | **`TelemetryService.Update`** | 30초 주기 flush |

`TitleReturnFlow`에 직접 삽입 또는 `ISessionResettable` / 전용 콜백 등록 — **게임 코어에 Sheets URL 흩뿌리지 말 것**.

##### Google Sheets · Apps Script (구현 전 선행)

1. Sheet 생성 → **헤더 1행** §컬럼 표와 **동일**하게 입력.
2. **Apps Script** `doPost(e)`: JSON 파싱 → `token` 검증 → `sessionId` upsert.
3. **Deploy → Web app** → URL 확보.
4. Unity: `GoogleSheetsSink`에 URL + token (**Steam 정식 빌드** ScriptableObject 또는 `Resources` — 에디터·데모 빌드 기본 empty).

##### MVP 완료 판정

- [ ] Steam **정식** 빌드 1판: Sheet에 **행 1개**, `sessionId` upsert 동작 (30초·리로드·종료 시 값 갱신).
- [ ] 멀티 Host: Client reject/chat 합산 **1행**에 반영.
- [ ] 에디터 Play / Dev Build localhost / **데모 빌드**: **행 추가 없음**.
- [ ] payload에 **금지 필드** 없음.

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
| 연결 | `UnityTransport` **localhost** (**7777**) | **Steam P2P + Lobby** | 동일·안정화 |
| 권한 | §9.0 매트릭스 (**이동=Owner+CNT**, 판정=Host, 발사체 비행=Client B안) | 동일 | 동일 |
| 최대 인원 | 4인 | 4인 | 동일 |

- Transport **교체 가능**하게 분리 (`UnityTransport` ↔ Steam Networking). **Steam 데모 = Steam transport 필수.**
- 중간 참가(Late Join) **없음**. 재접속 **미지원**. 호스트 마이그레이션 **없음**.
- **이탈 정책:** Host 또는 Client **누구든** 나가면 **즉시 방 종료** → 전원 타이틀. 남은 인원으로 계속·재입장 **없음**.

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

### 2.2 솔로 (1인 Host)

```
0.Title → 1.Lobby (Host 1인, 즉시 CanStart) → M.Stage1 → T.Stage1 → End.Demo → 0.Title
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

**※ 원격 IP Join·UDP discovery는 하지 않는다.**  
**※ Steam 데모 배포·플레이어 멀티에는 사용하지 않음** → §4.2.

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

- **게임 규칙**(패드, 문, 함정 **스케줄/상태**, Phase, 데미지, 클리어)은 **Host에서만** 최종 판정.
- 결과는 **`StageNetworkState` (중앙 매니저)** 등 `NetworkVariable` / RPC로 **전원에 공유**.
- Client도 **동일한 연출·상태**를 봄.
- 플레이어 위치는 **Owner + CNT** (§7.3 확정).
- **데미지·HP:** `NetworkDamageUtil` 단일 파이프라인 (§9A.3). 발사체만 §9.0.1 Client 보고 → Host 적용.

### MVP 동기화 대상

**우선순위:** `Must (데모)` → `Should (데모 여유)` → `Post (정식 이후)`

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

**데모 진행 — §9A:**

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

**Q. Phase 2 없이 데모?**  
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

## 11. 사망 · 리셋

- 멀티에서도 **`StageResetOnPlayerDeath`**: **1명 사망 = 전원 씬 리로드**.
- 리로드 후: 존 위 재스폰, `StageStartGate` 재진행, **새 시드**로 퍼즐 재배치.
- 낙사 확정: **Owner** Y 신고 (`ReportFallDeathServerRpc`) → **Host** HP 0 확정 (§9A.3). Host 단독 Y 판정은 Client void 낙사를 놓치므로 사용하지 않음.
- 리로드 리셋: `ResetForNewStage` → `ResetStageClientRpc` — 색 NV write는 **Owner만**, `Respawn`(위치·Idle·`RaiseRespawned`)은 **전원 로컬 적용**. 플레이어는 `destroyWithScene: false`(DDOL)라 비오너도 로컬 Respawn 없이는 doDie 포즈가 리로드를 넘어 잔존 (2026-07-16 수정).

---

## 12. 이탈 · 세션 종료

> **확정 정책:** 재접속·Late Join·호스트 마이그레이션 **전부 미지원**. 구현·제안하지 않음.

### 12.0 세션 이탈 규칙 (데모·정식 공통)

| 상황 | 동작 |
|------|------|
| **호스트 이탈** | **즉시 방 종료** → 전원 타이틀 |
| **클라이언트 이탈** | **즉시 방 종료** → 전원 타이틀 (Host와 동일) |
| **재접속** | **미지원** (유예·스냅샷·슬롯 복귀 없음) |
| **호스트 마이그레이션** | **없음** (Host 나가면 방 폭파) |
| **Late Join** | **없음** |
| **Kick (인게임)** | 로비 Start **이후**에 Host가 대상을 끊으면 → **방 종료** (전원 타이틀). 별도 Kick UI는 후순위 |
| **Kick (로비)** | §6 — **슬롯만 비움**, 방 유지. §12와 **다름** |

### 12.1 구현 시 주의

- **로비 Kick(§6)** = 슬롯만 비움. **인게임 이탈/Kick(§12)** = 방 전체 종료. 섞지 말 것.
- 인게임 이탈 후 **남은 인원으로 스테이지 계속**·인원만 줄이는 리로드·재입장 UI **금지**.
- Host/`DisconnectManager` (인게임) → **세션 정리 → 전원 타이틀**.
- “60초 유예 / 스냅샷 / 3인 계속” 구 스펙 **되살리지 말 것**.

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
11. **Steam ④** — **2인 Must** + 4인 1회 권장 → **Steam 데모 출시**
12. `End.Demo` + 솔로 경로 + Should 항목 (여유분)

### 16.2 데모 후 (정식)

> 범위·일정은 데모 출시 후 별도 확정.

1. **텔레메트리** — §0.5.1 (Sheet → `TelemetryService`, 정식 Steam 빌드 전송)
2. 난이도 밸런싱 (데모 피드백)
3. Tutorial (연습·말해보기) + CheerName 발음/G2P polish (로비 커스텀은 데모 §Cheer §3)
4. UI 옵션 (볼륨·해상도)
5. Steam Invite UX polish
6. 출시 QA

### 16.3 Post-Launch

- Steam Invite UX polish
- 컷씬, sit/dance 이모트
- (재접속·Late Join·호스트 마이그레이션은 **미지원 유지** — 로드맵에 넣지 않음)

---

## 17. Post-Launch (참고)

- 컷씬, 캐릭터 이모트 (sit, dance)
- 재접속·호스트 마이그레이션·Late Join: **미지원** (§12)

---

## 18. FAQ (설계 중 합의)

**Q. 솔로 로비 씬이 따로 필요한가?**  
A. **아니오.** Title 오프라인 패널에서 색 선택 후 바로 `M.Stage1`.

**Q. 솔로 색상은 어디서 고르나?**  
A. **Title 오프라인 패널** 드롭다운 1개. 로비 안 거침.

**Q. 다른 플레이어를 내 PC에서 조종하나?**  
A. **아니오.** Owner는 자기 캐릭터 **이동·입력·카메라·연출**. **HP·함정·피격 최종**은 Host (§9.0).

**Q. Host Authority면 이동도 Host?**  
A. **아니오.** 이동=**Owner+CNT 확정**. Host는 HP·함정·피격 등 판정.

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

**Q. discovery / 원격 IP로 테스트하나?**  
A. **안 함.** ParrelSync · localhost 빌드 · (데모) Steam P2P만.

**Q. 로비 Kick이면 방이 터지나?**  
A. **아니오.** 로비 Kick=슬롯만 비움(§6). **인게임** 이탈/Kick=방 종료(§12).

**Q. 2주 안에 컷씬·이모트·재접속 풀스펙을 넣어야 하나?**
A. **아니오.** 재접속·호스트 마이그레이션·Late Join은 **미지원 확정**(§12). 정식 Must는 Tutorial·밸런싱·옵션 UI·QA. Steam P2P는 **데모에서 이미 Must**.
