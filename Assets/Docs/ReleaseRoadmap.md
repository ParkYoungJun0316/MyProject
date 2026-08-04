# Release Roadmap

출시 일정·범위·체크리스트 SSOT.  
텔레메트리 스펙: [`TelemetryDesign.md`](TelemetryDesign.md) · 네트워크: [`NetworkDesign.md`](NetworkDesign.md) · 응원/튜토리얼: [`CheerAndTutorialDesign.md`](CheerAndTutorialDesign.md).

**확정 (2026-08):** 데모 / Steam Playtest Depot / Open·Release 이원화 **전부 폐기**.  
**목표 하나:** **2026-09-01 Steam 정식 출시.**

---

## 1. 앵커 · 달력

| 항목 | 값 |
|------|-----|
| **출시일** | **2026-09-01** (정식) |
| **배포** | Steam 정식 Depot만. 데모 페이지·**Steam Playtest Depot 없음** |
| **스토어 상태 (현재)** | **상점 페이지 검수 중** |

### 1.1 Steam 스토어 파이프라인 (확정)

```
페이지 검수 (현재)
  → 빌드 검수 (약 2–5일; 실패 시 +약 7일·주말까지 버퍼)
  → Coming Soon 약 2주 공개 노출
       ※ 이 2주에 친구/지인 play test (난이도·버그 피드백)
       ※ Steam Playtest 제품/Depot이 아님 — 정식 빌드·초대 플레이
  → 2026-09-01 정식 출시
```

| 단계 | 의미 |
|------|------|
| **페이지 검수** | 스토어 페이지 Valve 리뷰 |
| **빌드 검수** | Depot 업로드 빌드 Valve 리뷰 (약 2–5일) |
| **2주 노출** | Coming Soon 공개. 트레일러·캡처 노출 + **비공식 play test**로 난이도·버그 수집 |
| **9/1** | 정식 출시 버튼 |

### 1.2 주간 초점 (러프)

| 구간 | 초점 |
|------|------|
| **지금** | M/T **Dev Build(②)** 잔여 → **Steamworks(전부)** 착수 |
| **페이지·빌드 검수 병행** | Steam 네트워크·로비·Depot·알파 빌드 + **애니**(트레일러) |
| **중반** | SFX → 응원 확장 → Tutorial |
| **Coming Soon 2주** | **난이도** (play test 피드백) · 버그 핫픽스 · QA 준비 |
| **출시 직전** | **출시 QA** → 9/1 |
| **출시 후 OK** | **텔레메트리** (9/1 블로커 아님) |

| 트랙 | 역할 |
|------|------|
| **게임/빌드** | §4 작업 순서 |
| **스토어** | §1.1 파이프라인 · 트레일러·캡처 |
| **법인** | 가능하면 출시 전 이전. 막히면 출시 후 허용 (출시일 고정) |

---

## 2. 출시 Must / 제외

### 2.1 Ship Must (9/1에 들어가야 함)

- **플레이 경로:** Title → Lobby → **Tutorial** → `M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`
- **솔로:** 동일 경로 (**NGO Host 1인**, `partySize=1`). 오프라인 모드 없음
- **Steamworks (전부):** Steam Networking transport + Steam Lobby + Depot/알파 빌드 + Invite — §3 ④ · §4 순위 1. **로컬 NGO만으로 출시 불가**
- **응원·보이스:** Dissonance + Vosk + `/cheer` + **응원 확장 2종**
- **Tutorial:** 조작·말해보기 (경험자 생략 가능 UX OK)
- **난이도:** Coming Soon 2주 play test 피드백 반영 후 출시선 확정
- **사운드:** BGM + 핵심 SFX (과투자 금지)
- **플레이어 애니메이션:** 트레일러·인게임 체감 품질
- **UI:** 타이틀·로비·HP·카운트다운·응원 HUD·채팅 · 옵션(마스터·BGM·SFX, 해상도/전체화면)
- **배포:** Steam 정식만. localhost/IP Join로 외부 배포 **안 함**

### 2.2 출시 후 OK / 제외

| 항목 | 처리 |
|------|------|
| **텔레메트리** | **출시 이후 착수 OK** (9/1 블로커 아님). 스펙: [`TelemetryDesign.md`](TelemetryDesign.md) |
| Steam **데모** / **Playtest Depot** | **없음** |
| Open Must / Release Must 이원화 | **폐기** — Ship Must 하나 |
| §12 재접속·호스트 마이그레이션 | **미지원.** 인게임 이탈 = **방 종료** |
| 원격 IP Join / UDP discovery | **미사용** |
| 관전 / sit·dance / 파티클 대량 | Post-Launch |
| **컷씬** | **영구 제외** |

---

## 3. 네트워크 테스트 단계 (개발자 환경)

원격 IP Join·discovery 없음. 개발자 PC **최대 2대** — Steam 일상 검증은 **2인**.

```
① ParrelSync (에디터 Host + Clone Client)
   → 빠른 반복. ※ 출시 판정용 아님.

② Development Build (같은 PC localhost)
   → 빌드 전용 버그. ※ Steamworks 전 잔여 게이트.

③ 응원 (CheerService + Dissonance + Vosk + /cheer)
   → ② 1차, Steamworks 후 Steam에서 최종.

④ Steamworks = Steam 관련 전부
   → Transport · Lobby · Depot/알파 · Invite.
   → 정식 출시 게이트 — 2인 필수, 4인 권장 (§3.1).
```

| 단계 | 목적 | 통과 기준 (최소) |
|------|------|------------------|
| ① ParrelSync | 구현 속도 | Title→Lobby→스테이지 **2인** 1회 |
| ② Dev Build | 빌드 품질 | **2인** 클리어/사망 리로드·스테이지 전환 |
| ③ 응원 | 보이스+응원 | Cheer 시나리오 |
| ④ **Steamworks** | **출시 게이트** | **2인** Steam 원격: 초대·보이스·응원. **4인 1회 권장** |

**Transport:** ①② = `UnityTransport`(localhost). **정식 = Steam Networking.**

### 3.1 2인 vs 4인

| | **2인 Steam** (일상) | **4인 Steam** (출시 전 권장) |
|--|----------------------|------------------------------|
| 검증 | Transport, Lobby, Invite, NGO, 보이스, 응원, 스테이지 | 위 + 4슬롯·응원 3표·4음성 |
| 판정 | 출시 최소 게이트 | 친구 **1회 강력 권장** |

---

## 4. 전체 작업 순서 (확정)

> **Steamworks** = Steam 관련 **전부** (네트워크 P2P · Lobby · Depot/알파 빌드 · Invite · 빌드 검수용 업로드).  
> 별도 “SDK만” 단계가 아님.

| 순위 | 작업 | 비고 |
|------|------|------|
| 0 | **M/T Dev Build(②)** | 현재 잔여. Steamworks 전 로컬 게이트 |
| 1 | **Steamworks (전부)** | P2P · Lobby · Depot/알파 · 빌드 검수 대응 — **출시 하드 블로커** |
| 2 | **플레이어 애니메이션** | 트레일러·인게임. 트레일러 일정과 맞춤 |
| 3 | **SFX 마무리** | 인게임 폴리시. 트레일러 음성은 별도 믹스 OK |
| 4 | **응원 시스템 확장 2개** | Ship Must |
| 5 | **Tutorial 씬** | 조작·말해보기 |
| 6 | **난이도** | Coming Soon 2주 play test 피드백 흡수 |
| 7 | **출시 QA** | §6 — 출시 직전 E2E 통과 체크 |
| 8 | **텔레메트리** | **출시 이후 OK** ([`TelemetryDesign.md`](TelemetryDesign.md)) |
| — | 파티클(피격·Break) · UI 옵션 마감 | 여유 시 / QA 전 |
| — | 컷씬 / 관전 / 이모트 | 제외 또는 Post-Launch |

```
[지금]           ② Dev Build → Steamworks(전부) + 애니(트레일러 병행 가능)
[중반]           SFX → 응원 확장 → Tutorial
[Coming Soon 2주] 난이도·버그 (친구 play test) → 핫픽스
[출시 직전]      출시 QA → 9/1
[출시 후]        텔레메트리
```

---

## 5. 실행 체크리스트

> 음성(CheerService + Dissonance + Vosk) 구축 완료 이후 기준.

### Phase 0 — 테스트 전 블로커

| # | 작업 | 비고 |
|---|------|------|
| 0-1 | Vosk zip 정합 | `VoskModelLoader` ↔ `StreamingAssets` |
| 0-2 | CheerName 최종화 | `berry` / `guma` / `sook` / `hobak` |
| 0-3 | AudioListener | `LocalPlayerCamera` 1개 |

### Phase 1 — 로컬 빌드 게이트

| # | 작업 | 통과 기준 |
|---|------|-----------|
| 1 | M/T 1인 E2E | Title→Lobby→M/T. `/cheer`·음성 1회 |
| 2 | M/T **2인 Dev Build** | localhost, 보이스, 응원, 사망 리로드 |
| 3 | 빌드 버그 수정 | |
| 4 | 스크린샷·트레일러 소재 1차 | 스토어/Coming Soon용 |

### Phase 2 — Steamworks (전부) · 스토어 검수

| # | 작업 | 비고 |
|---|------|------|
| 5 | **Steamworks 전부** | Steam Networking · Lobby · Depot/알파 · Invite |
| 6 | Steam **2인** 스모크 | 원격 초대·보이스·응원 |
| 7 | **빌드 검수** 대응 | 페이지 검수 통과 후 · 실패 시 +~7일 버퍼 |
| 8 | 빌드 메타 | Product Name, Icon, `bundleVersion` (예: `1.0.0`) |
| 9 | Coming Soon 게시 | 트레일러·캡처. **약 2주 노출** |

### Phase 3 — 콘텐츠 · Coming Soon play test

| # | 작업 | 비고 |
|---|------|------|
| 10 | 플레이어 애니메이션 | 트레일러 우선 → 인게임 |
| 11 | SFX / BGM | 과투자 금지 |
| 12 | 응원 확장 2개 | Cheer 문서 |
| 13 | Tutorial 씬 | 연습·말해보기 |
| 14 | **난이도** | **2주 노출 중** 친구 play test 피드백 |
| 15 | UI 옵션 · DialogueUI / End.Demo | |
| 16 | 핫픽스 | Coming Soon 기간 버그 |

### Phase 4 — QA · 출시

| # | 작업 | 비고 |
|---|------|------|
| 17 | Steam **4인 1회** | 권장 |
| 18 | **출시 QA** | §6 |
| 19 | 스크린샷·스토어 최종 | |
| 20 | **2026-09-01 정식 출시** | |
| 21 | 텔레메트리 MVP | **출시 후 OK** |

### 5.1 스크린샷 · 트레일러

| 시점 | 목적 |
|------|------|
| Phase 1~2 | Coming Soon **초안** (애니 컷 포함) |
| Phase 4 | **최종** capsule·헤더 |

---

## 6. 출시 QA (최소)

출시 직전 **“이 빌드로 9/1에 내도 되나?”** E2E 통과 체크. (별도 QA팀 프로세스 아님.)

- [ ] Title → Lobby → Tutorial → M 풀코스+보스 → T 풀코스+보스 → End → Title (1인 Host)
- [ ] 동일 경로 **Steam 2인** (초대·보이스·응원·사망 리로드)
- [ ] Steam **4인 1회** (가능 시)
- [ ] 인게임 이탈 시 전원 타이틀 (`NetworkDesign` §12)
- [ ] 옵션 볼륨/해상도 저장·적용
- [ ] 스토어 페이지·빌드 Depot·버전 문자열 일치
- [ ] (텔레메트리는 출시 후 — QA 블로커 아님)

---

## 7. Post-Launch (출시 후)

- **텔레메트리** MVP ([`TelemetryDesign.md`](TelemetryDesign.md))
- 관전(Spectator) 후보
- sit / dance 이모트
- 파티클 확장
- (재접속·Late Join·호스트 마이그레이션 **미지원 유지**)
- **컷씬: 영구 제외**
