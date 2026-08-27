# Release Roadmap

출시 일정·범위·체크리스트 SSOT.  
텔레메트리 스펙: [`TelemetryDesign.md`](TelemetryDesign.md) · 네트워크: [`NetworkDesign.md`](NetworkDesign.md) · 응원/튜토리얼: [`CheerAndTutorialDesign.md`](CheerAndTutorialDesign.md) · 사운드/옵션 메뉴: [`SoundAndSettingsDesign.md`](SoundAndSettingsDesign.md).

**확정 (2026-08):** 데모 / Steam Playtest Depot / Open·Release 이원화 **전부 폐기**.  
**목표 하나:** **2026-09-08 Steam 정식 출시.** (원래 9/1 → 콘텐츠 마무리 버퍼 확보 위해 9/8로 변경, 아래 참고)

> **🔶 출시일 변경 (2026-08-23 결정):** 9/1 → **9/8(화)**로 1주 연장. 남은 콘텐츠 작업(§4 순위 5~11) 여유 확보 목적. 요일은 원래(화)와 동일 유지 — Tue~Thu 권장 요일 룰 유지, 금(9/4)·월(9/7)은 배제.
> **⚠️ Steamworks 조치 필요:** 출시일이 특정일 기준 14일 이내로 들어오면 Steamworks에서 직접 수정 불가(잠김). **Steam 파트너 지원팀에 티켓 제출해 9/8로 변경 요청 필요** — 아직 미제출이면 최우선 처리.
> **순서 갱신 (2026-08-06):** Steamworks 코드 구현(트랙 1~3: 부트스트랩/Transport/Lobby)·다국어(Localization) 전체 번역 완료. 다음은 **Depot 실사용 2인 스모크 → 빌드 검수 즉시 제출(콘텐츠 완성 기다리지 않음, §1.1a) → 응원 확장 테스트 → BGM → 옵션/설정 메뉴 → SFX+볼륨조절 → 애니메이션 → Tutorial → 난이도 → 출시 QA** 순으로 확정. 상세는 §4 참고.
> **✅ Depot 2인 스모크 완료 (2026-08-06~07, §4 순위 2 종료):** 실 App ID 빌드 업로드·Set Live·테스터 초대 완료 후 실사용 테스트에서 버그 6건 발견, 전부 진단·수정·검증 통과. 상세 히스토리는 [`SteamworksIntegrationDesign.md`](SteamworksIntegrationDesign.md) "트랙 5" 절(6차 세션이 최종) 참고.
> **✅ 빌드 검수 제출 완료 (2026-08-07, §4 순위 3 종료):** 빌드 메타 정리 후 Steam 빌드 검수 제출함. **✅ 통과 완료.**
> **🔶 §4 순위 5~7(BGM/옵션·설정 메뉴/SFX 볼륨) — 코드 구현 완료, 에디터 배치·UI 프리팹·테스트는 아직 (2026-08-07 세션):** `GameSettingsManager`(볼륨·화면·언어 SSOT)·`BGMManager`(구역별 BGM+크로스페이드)·`OptionsMenuController`(옵션 UI)·`SFXLibrary` 클립별 볼륨 보정까지 스크립트 전부 작성됨. **다음 세션은 [`SoundAndSettingsDesign.md`](SoundAndSettingsDesign.md) "남은 작업(에디터·콘텐츠)" 절부터 이어서 진행** — 씬에 컴포넌트 배치, 옵션 패널 UI 프리팹 제작·연결, BGM 곡 배정, 실사용 테스트가 남음. 응원 시스템 확장 2개 테스트(§4 순위 4)는 다른 세션에서 병행 진행 중.

---

## 1. 앵커 · 달력

| 항목 | 값 |
|------|-----|
| **출시일** | **2026-09-08** (정식, 2026-08-23 결정 — 원래 9/1) |
| **배포** | Steam 정식 Depot만. 데모 페이지·**Steam Playtest Depot 없음** |
| **스토어 상태 (현재)** | **페이지·빌드 검수 통과, 출시일 9/8로 재조정 (Steam 지원팀 변경 요청 필요)** |

### 1.1 Steam 스토어 파이프라인 (확정)

```
페이지 검수 (완료)
  → 빌드 검수 (완료, 통과)
  → Coming Soon 약 2주 공개 노출
       ※ 이 2주에 친구/지인 play test (난이도·버그 피드백)
       ※ Steam Playtest 제품/Depot이 아님 — 정식 빌드·초대 플레이
  → 2026-09-08 정식 출시 (2026-08-23 결정, 원래 9/1)
```

| 단계 | 의미 |
|------|------|
| **페이지 검수** | 스토어 페이지 Valve 리뷰 — ✅ 완료 |
| **빌드 검수** | Depot 업로드 빌드 Valve 리뷰 — ✅ 완료·통과 |
| **2주 노출** | Coming Soon 공개. 트레일러·캡처 노출 + **비공식 play test**로 난이도·버그 수집 |
| **9/8** | 정식 출시 버튼 (원래 9/1 → 1주 연장) |

### 1.1a 빌드 검수 타이밍 (2026-08-06 결정) / 출시일 변경 (2026-08-23 결정)

- 빌드 검수·페이지 검수 **모두 통과 완료**.
- **9/1 → 9/8 변경 (2026-08-23):** 남은 콘텐츠 작업(§4 순위 5~11: BGM/옵션/SFX/애니/Tutorial/난이도/QA) 버퍼 확보 목적으로 1주 연장. 요일은 원래와 동일한 **화요일** 유지(스팀 권장 요일 Tue~Thu 룰 준수).
- **Steamworks 조치:** 출시일 14일 이내 진입 시 Steamworks 랜딩 페이지에서 직접 수정 불가 → **Steam 파트너 지원팀에 티켓 제출해 9/8로 변경 요청** 필요 (미완료 시 최우선 처리).
- Coming Soon 2주 노출 조건: 9/8 기준으로는 늦어도 **8/25**에 Coming Soon이 열려 있어야 함. 실제 오픈일 확인 필요.

### 1.2 주간 초점 (러프)

| 구간 | 초점 |
|------|------|
| **지금 (8/23)** | 빌드 검수 통과 완료, 출시일 9/1→9/8 변경 결정 → **콘텐츠 마무리** (응원 확장 테스트 → BGM → 옵션/설정 메뉴 → SFX+볼륨조절 → 애니) |
| **중반** | Tutorial |
| **Coming Soon 2주** | **난이도** (play test 피드백) · 버그 핫픽스 · QA 준비 |
| **출시 직전** | **출시 QA** → 9/8 |
| **출시 후 OK** | **텔레메트리** (9/8 블로커 아님) |

| 트랙 | 역할 |
|------|------|
| **게임/빌드** | §4 작업 순서 |
| **스토어** | §1.1 파이프라인 · 트레일러·캡처 |
| **법인** | 가능하면 출시 전 이전. 막히면 출시 후 허용 (출시일 고정) |

---

## 2. 출시 Must / 제외

### 2.1 Ship Must (9/8에 들어가야 함)

- **플레이 경로:** Title → Lobby → **Tutorial** → `M.Stage1`…`M.Stage5` → `M.Boss` → `T.Stage1`…`T.Stage5` → `T.Boss` → `End.Demo`
- **솔로:** 동일 경로 (**NGO Host 1인**, `partySize=1`). 오프라인 모드 없음
- **Steamworks (전부):** Steam Networking transport + Steam Lobby + Depot/알파 빌드 + Invite — §3 ④ · §4 순위 1~3(전부 완료: 코드·Depot 실사용 테스트·빌드 검수 통과). **로컬 NGO만으로 출시 불가**
- **응원·보이스:** Dissonance + Vosk + 숫자키(1~4) + **응원 확장 2종**
- **Tutorial:** 조작·말해보기 (경험자 생략 가능 UX OK)
- **난이도:** Coming Soon 2주 play test 피드백 반영 후 출시선 확정
- **사운드:** BGM + 핵심 SFX (과투자 금지)
- **플레이어 애니메이션:** 트레일러·인게임 체감 품질
- **UI:** 타이틀·로비·HP·카운트다운·응원 HUD·채팅 · 옵션(마스터·BGM·SFX, 해상도/전체화면)
- **배포:** Steam 정식만. localhost/IP Join로 외부 배포 **안 함**

### 2.2 출시 후 OK / 제외

| 항목 | 처리 |
|------|------|
| **텔레메트리** | **출시 이후 착수 OK** (9/8 블로커 아님). 스펙: [`TelemetryDesign.md`](TelemetryDesign.md) |
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

③ 응원 (CheerService + Dissonance + Vosk + 숫자키 1~4)
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
| 0 | ~~M/T Dev Build(②)~~ | ✅ 완료 |
| 1 | ~~Steamworks 코드 구현 (트랙1~3) + 다국어~~ | ✅ 완료 (부트스트랩/Transport/Lobby, 전체 번역) |
| 2 | ~~Depot 실사용 2인 스모크 테스트~~ | ✅ 완료 — 버그 6건 발견·전부 수정·검증 통과 (`SteamworksIntegrationDesign.md` 트랙5) |
| 3 | ~~빌드 메타 정리 + 빌드 검수 즉시 제출~~ | ✅ 완료 — 검수 제출(2026-08-07) 후 **통과 완료** (§1.1a) |
| **4** | **응원 시스템 확장 2개 테스트** | 구현 완료, 테스트만 남음 — 검수 대기 중 병행 |
| **5** | **BGM 추가** | 🔶 코드 완료(`BGMManager`), 곡 배정·씬 배치·테스트 남음 — [`SoundAndSettingsDesign.md`](SoundAndSettingsDesign.md) |
| **6** | **옵션/설정 메뉴** | 🔶 코드 완료(`GameSettingsManager`/`OptionsMenuController`), UI 프리팹 제작·연결 남음. AudioMixer 미사용(스크립트 기반, 과투자 방지) — [`SoundAndSettingsDesign.md`](SoundAndSettingsDesign.md) |
| **7** | **SFX 마무리 + BGM 음량 조절** | 🔶 코드 완료(`SFXLibrary` 클립별 볼륨 보정), 실사용 밸런스 조정 남음 |
| **8** | **플레이어 애니메이션** | 트레일러·인게임. 트레일러 일정과 맞춤 |
| **9** | **Tutorial 씬** | 조작·말해보기 |
| **10** | **난이도** | Coming Soon 2주 play test 피드백 흡수 |
| **11** | **출시 QA** | §6 — 출시 직전 E2E 통과 체크 |
| **12** | **텔레메트리** | **출시 이후 OK** ([`TelemetryDesign.md`](TelemetryDesign.md)) |
| — | 파티클(피격·Break) · 이모트 | 여유 시 / Post-Launch |
| — | 컷씬 / 관전 | 영구 제외 / Post-Launch |

```
[완료]            Depot 2인 스모크 → 빌드 메타+빌드 검수 → ✅ 통과, 출시일 9/1→9/8 변경 결정 (2026-08-23)
[지금]            응원 확장 테스트 → BGM → 옵션/설정 메뉴 → SFX+볼륨조절 → 애니(트레일러)
[중반]            Tutorial
[Coming Soon 2주] 난이도·버그 (친구 play test) → 핫픽스
[출시 직전]       출시 QA → 9/8
[출시 후]         텔레메트리
```

---

## 5. 실행 체크리스트

> 음성(CheerService + Dissonance + Vosk) 구축 완료 이후 기준.

### Phase 0 — 테스트 전 블로커

| # | 작업 | 비고 |
|---|------|------|
| 0-1 | Vosk zip 정합 | `VoskModelLoader` ↔ `StreamingAssets` |
| 0-2 | CheerName 최종화 | `berry` / `guma` / `sook` / `dan` |
| 0-3 | AudioListener | `LocalPlayerCamera` 1개 |

### Phase 1 — 로컬 빌드 게이트

| # | 작업 | 통과 기준 |
|---|------|-----------|
| 1 | M/T 1인 E2E | Title→Lobby→M/T. 숫자키·음성 1회 |
| 2 | M/T **2인 Dev Build** | localhost, 보이스, 응원, 사망 리로드 |
| 3 | 빌드 버그 수정 | |
| 4 | 스크린샷·트레일러 소재 1차 | 스토어/Coming Soon용 |

### Phase 2 — Steamworks (전부) · 스토어 검수

| # | 작업 | 비고 |
|---|------|------|
| 5 | ~~Steamworks 전부 (코드)~~ | ✅ 완료 — Steam Networking · Lobby · Depot/알파 · Invite |
| 6 | ~~Depot **2인** 스모크~~ | ✅ 완료 — 버그 6건 발견·전부 수정·검증 통과 |
| 7 | ~~빌드 메타~~ | ✅ 완료 |
| 8 | ~~**빌드 검수** 제출~~ | ✅ 완료 (2026-08-07) — **통과** (§1.1a) |
| 9 | Coming Soon 게시 | 트레일러·캡처. **약 2주 노출** |

### Phase 3 — 콘텐츠 · Coming Soon play test

| # | 작업 | 비고 |
|---|------|------|
| 10 | 응원 확장 2개 테스트 | 구현 완료, 테스트만 남음 — Cheer 문서. **검수 대기 중 병행** |
| 11 | BGM 추가 | 🔶 코드 완료, 곡 배정·씬 배치·테스트 남음 — `SoundAndSettingsDesign.md` |
| 12 | 옵션/설정 메뉴 (+ DialogueUI / End.Demo 마감) | 🔶 코드 완료, UI 프리팹 제작·연결 남음 — `SoundAndSettingsDesign.md` |
| 13 | SFX 마무리 + BGM 음량 조절 | 🔶 코드 완료(클립별 보정 배율), 실사용 밸런스 조정 남음 |
| 14 | 플레이어 애니메이션 | 트레일러 우선 → 인게임 |
| 15 | Tutorial 씬 | 연습·말해보기 |
| 16 | **난이도** | **2주 노출 중** 친구 play test 피드백 |
| 17 | 핫픽스 | Coming Soon 기간 버그 |

### Phase 4 — QA · 출시

| # | 작업 | 비고 |
|---|------|------|
| 18 | Steam **4인 1회** | 권장 |
| 19 | **출시 QA** | §6 |
| 20 | 스크린샷·스토어 최종 | |
| 21 | **2026-09-08 정식 출시** | 원래 9/1 → 2026-08-23 결정으로 1주 연장 |
| 22 | 텔레메트리 MVP | **출시 후 OK** |

### 5.1 스크린샷 · 트레일러

| 시점 | 목적 |
|------|------|
| Phase 1~2 | Coming Soon **초안** (애니 컷 포함) |
| Phase 4 | **최종** capsule·헤더 |

---

## 6. 출시 QA (최소)

출시 직전 **“이 빌드로 9/8에 내도 되나?”** E2E 통과 체크. (별도 QA팀 프로세스 아님.)

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
