# Telemetry Design (MVP)

텔레메트리 스펙 전용. 일정/체크리스트는 [`ReleaseRoadmap.md`](ReleaseRoadmap.md), 네트워크는 [`NetworkDesign.md`](NetworkDesign.md) 참고.

> **범위:** **출시 후 OK** — 9/1 블로커 아님 (`ReleaseRoadmap.md` §4 순위 8). 관전 대신 이탈·체류·사망·응원 거부로 상황 파악.
> **구현 에이전트:** 이 문서만 읽고 구현 가능. 착수 = Steamworks(전부) 이후·출시 전후 여유 시.
> 순서: ① Google Sheet + Apps Script upsert → ② `TelemetryService` + 게임 연동.

---

## 목적 · 시점

- **목적:** Steam **정식** 플레이 1판당 **Google Sheets 1행(upsert)** — 이탈 구간, 바이옴(M/T) 체류·사망, 응원 거부·채팅 **합계**.
- **시점:** **출시 이후 착수 OK.** 여유 있으면 출시 직전에도 가능.
- **구현 순서:** ① Google Sheet + Apps Script upsert → ② `TelemetryService` + 게임 연동.

## 아키텍처

| 항목 | 규칙 |
|------|------|
| **진입점** | `TelemetryService` — `0.Title` 배치, **DontDestroyOnLoad** |
| **전송 대상** | Google Sheets (Apps Script **Web App** URL) |
| **행 모델** | **세션 1행** — `sessionId` 기준 **upsert**(갱신). append-only 금지(중간 스냅샷 쓰레기 행 방지). |
| **보내는 쪽** | **Host PC 1행만** (솔로 = 그 PC가 Host 역할). Client는 Host에 **+1만 RPC 보고**, Sheets 직접 전송 **금지**. |
| **Sink 분리** | `ITelemetrySink` (MVP: `GoogleSheetsSink`) — URL·HTTP만 담당. 집계는 `TelemetryService`. |

## 전송 ON/OFF (Must)

| 환경 | Sheets 기록 |
|------|-------------|
| Unity **에디터** Play | ❌ |
| **ParrelSync** (에디터 클론) | ❌ |
| **Development Build** localhost | ❌ |
| **Steam Depot 정식**, Steam 클라이언트로 실행 | ✅ |

**권장 게이트 (둘 다 만족 시 전송):**

1. `#if !UNITY_EDITOR`
2. Steamworks 초기화 성공 (`SteamAPI` 등). 정식 빌드 파이프라인에서 Scripting Define `TELEMETRY_RELEASE` (또는 동등한 배포 게이트).

Inspector `enabled` 토글은 **로컬 디버그용**. 위 게이트가 **배포 판정** 기준.

## 세션 생명주기

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

## Google Sheets — 컬럼 (헤더 1행 고정)

**복붙용 순서:**

```
timestamp | sessionId | buildVersion | playMode | partySize | run_complete | quitAt | M_dwell_sec | M_death_count | M_buff_count | T_dwell_sec | T_death_count | T_buff_count | reject_self_cheer | reject_target_buffed | reject_timeout | reject_chat_rate_limit | reject_voice_no_match | chat_used_count
```

| 컬럼 | 타입 | 설명 |
|------|------|------|
| `timestamp` | datetime | **upsert 전송 시각** (UTC 또는 KST 중 하나로 통일) |
| `sessionId` | string | 익명 UUID. **upsert 키.** |
| `buildVersion` | string | `Application.version` (예: `1.0.0`) |
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

## 측정 규칙 (Must)

| 항목 | 규칙 |
|------|------|
| **바이옴** | 씬 이름이 `M.` 접두 → M 컬럼, `T.` 접두 → T 컬럼. Stage·Boss 구분 없이 **합산**. |
| **dwell** | 해당 씬 **로드 직후** ~ **다른 씬으로 나가기 직전**까지 Host `Time.time`(또는 `unscaledTime`)을 해당 바이옴에 누적. |
| **death_count** | 해당 바이옴 **씬 Load마다 +1** (첫 진입 포함). 사망=전원 리로드이므로 **리로드 1회 = death 1**. 동시 다수 사망도 **+1**. |
| **buff_count** | 버프가 **플레이어 1명에게 적용될 때마다 +1**. 4인 전원 버프 = **+4**. 적용 시점 씬의 바이옴에 가산. |
| **reject / chat** | **4인 합계** — Client 발생 시 Host RPC로 +1 보고 후 Host가 누적. |
| **partySize** | 세션 시작 시 `GameSession` / `NetworkManager.ConnectedClientsIds.Count` 등 Host 기준 스냅샷. |

## reject / chat — 코드 매핑

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

## 전송 타이밍 · upsert

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
Web App URL은 **Steam 정식 빌드** 설정에만 (에디터·localhost Inspector 기본 empty).

실패 시 **1~2회 재시도** 후 포기 (영구 로컬 큐는 MVP 범위 밖).

## 멀티 · 솔로

| | |
|--|--|
| **멀티** | **Host만** `TelemetryService` 전송. Client → Host `TelemetryReportServerRpc(reason)` 등으로 reject/chat +1만. |
| **솔로** | NGO Host 1인. `playMode=Solo`, `partySize=1`. 멀티와 동일 코드 경로. |
| **멀티 1인** | `playMode=Multi`, `partySize=1`. |

## 게임 연동 훅 (구현 체크리스트)

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

## Google Sheets · Apps Script (구현 전 선행)

1. Sheet 생성 → **헤더 1행** §컬럼 표와 **동일**하게 입력.
2. **Apps Script** `doPost(e)`: JSON 파싱 → `token` 검증 → `sessionId` upsert.
3. **Deploy → Web app** → URL 확보.
4. Unity: `GoogleSheetsSink`에 URL + token (**Steam 정식 빌드** ScriptableObject 또는 `Resources` — 에디터·localhost 기본 empty).

## MVP 완료 판정

- [ ] Steam **정식** 빌드 1판: Sheet에 **행 1개**, `sessionId` upsert 동작 (30초·리로드·종료 시 값 갱신).
- [ ] 멀티 Host: Client reject/chat 합산 **1행**에 반영.
- [ ] 에디터 Play / Dev Build localhost: **행 추가 없음**.
- [ ] payload에 **금지 필드** 없음.
