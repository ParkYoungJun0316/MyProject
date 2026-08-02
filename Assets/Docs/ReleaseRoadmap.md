# Release Roadmap

`NetworkDesign.md` §0에서 분리된 문서 (2026-08) — 출시 일정·범위·체크리스트 전용. 텔레메트리 스펙은 [`TelemetryDesign.md`](TelemetryDesign.md), 네트워크 동기화 아키텍처는 [`NetworkDesign.md`](NetworkDesign.md) 참고.

**앵커:** D0 = Steam Direct App Fee 결제일. **D14** = Coming Soon + Playtest 동시 오픈 목표.

---

## 0.1 달력 · 3트랙 (확정)

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
| 2 | **텔레메트리** | [`TelemetryDesign.md`](TelemetryDesign.md) — **Open Must** (관전 대신 상황 파악) |
| 3 | **사운드 마무리** | 최소한만. 과하면 방해되므로 억제 |
| 4 | **파티클** | 시작 안 함. **피격·Break** 등 핵심만 |
| 5 | **난이도 밸런싱** | Playtest M주·T주 피드백 기반 → 출시 전 흡수 |
| 6 | **UI 마무리** | 오픈: 최소 HUD / **정식:** 옵션·볼륨 등 |
| 7 | **Steamworks 연동** | Transport·Lobby·Depot·Playtest (§0.3) |
| 8 | **출시 QA** | 빌드·Steam·2~4인 시나리오 체크리스트 |
| — | **컷씬** | **영구 제외** (출시 후에도 안 넣음) |
| — | **관전** | 출시 전 제외 → **Post-Launch 후보** |
| — | sit / dance 이모트 | Post-Launch |

## 0.2 네트워크 테스트 단계 (개발자 환경)

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

### 0.2.1 개발자 2PC · 2인 테스트 vs 4인

| | **2인 Steam P2P** (일상) | **4인 Steam P2P** (오픈·Playtest 권장) |
|--|--------------------------|----------------------------------|
| 검증됨 | Transport, Lobby, Invite/Join, NGO 동기화, Dissonance, Vosk, 응원(필요 1표), 스테이지 진행 | 위 + **4슬롯·4스폰·응원 3표·4음성** |
| **보장 안 됨 → 4인 전용 버그** | — | `ActivePlayerCount`·집계, 4색 Gate, 4명 보이스 혼잡, 이탈 시 §12(`NetworkDesign.md`) 전원 타이틀 수렴 |
| 판정 | **Playtest 오픈 최소 게이트** (2PC 한정) | **외부 신뢰도** — 친구 Playtest **1회 강력 권장** |

**2인 통과 = 4인 100% 보장 아님.** 다만 NGO·Steam P2P·응원 **연결·규칙 골격**은 2인에서 대부분 검증 가능.  
**4인만 터지는 버그**는 §0.2.1 표 우측 항목 — 오픈·M주 중 **4인 1회**로 잡는다.

## 0.3 범위 — Open / Playtest / Release

#### Open Must (D14 Coming Soon + Playtest)

- **플레이 경로 (오픈 빌드):** Title → Lobby → `M.Stage1`…`M.Stage5` → `M.Boss` (멀티 **2~4인**)
- **솔로:** 동일 경로 (**NGO Host 1인**, `partySize=1`)
- **T 풀코스:** 오픈일에 완벽할 필요 없음. **T주 시작 전**(`D21`)까지 `T.Stage1`…`T.Boss` 완성
- **네트워크:** `NetworkDesign.md` §9 Must 동기화 + **§0.2 ④ Steam P2P + Steam Lobby** (친구 Playtest 다운·초대)
- **응원·보이스:** 인게임 **Dissonance** + **Vosk 응원** + `/cheer` (`CheerAndTutorialDesign.md`)
- **텔레메트리:** [`TelemetryDesign.md`](TelemetryDesign.md) — Steam **Playtest·정식** Depot에서 전송 ON
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
| §12(`NetworkDesign.md`) 재접속·유예·스냅샷·호스트 마이그레이션 | **미지원.** 인게임 이탈 = **방 종료** |
| 원격 IP Join / UDP discovery | **미사용.** 개발=ParrelSync·localhost / 배포=**Steam** |
| 관전(Spectator) | 출시 전 **제외** → Post-Launch 후보 |
| **컷씬** | **영구 제외** (출시 후에도 안 넣) |
| sit / dance 이모트 | Post-Launch |
| 파티클 대량 추가 | Post-Launch |

## 0.4 권장 작업 순서 (요약)

**상세 실행 순서·체크 항목은 §0.5 참고.**

```
[D0–D13 오픈 준비]
0. 테스트 전 블로커 (Vosk, CheerName, AudioListener)
1. 폴리시 (오디오, 카메라, DialogueUI, End.Demo, 빌드 메타)
2. 로컬 테스트 (1인 → 2인 Dev Build → 스크린샷 1차)
3. Steamworks (App ID · Transport · Lobby · Depot) + 텔레메트리 MVP (TelemetryDesign.md)
4. 스토어 페이지 · 리뷰 · M 풀코스+보스 Steam 2인

[D14] Coming Soon + Playtest 동시
[D14–D21] Playtest M주 (보스 포함) + T 병행
[D21–D28] Playtest T주 (보스 포함) + Tutorial·옵션·법인 이전
[D28–D30+] 출시 QA → 법인 계정 정식 출시
```

## 0.5 오픈·Playtest·출시 체크리스트 (실행 순서)

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
| 12 | **텔레메트리 MVP** | [`TelemetryDesign.md`](TelemetryDesign.md) — **Open Must.** Playtest Depot에서 전송 ON |
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

#### 0.5.2 스크린샷

| 시점 | 목적 |
|------|------|
| §0.5 #10 (2인 Dev Build 후) | 스토어 **초안** — 플레이 가능 확인용 |
| §0.5 #18 (Steam Playtest 후) | **최종** — capsule·헤더·실플레이 품질 |
