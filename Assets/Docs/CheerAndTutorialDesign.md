# Tutorial Design

정식 **Tutorial** 씬 설계 문서 (구 Lobby 흡수 — 사전 게이트 구간 + 조작 연습 + CheerName/TeamCheerWord 설정).

> **2026-09 문서 분리.** 응원(Cheer) 시스템의 규칙·네트워크·UI 상세는 **[`CheerSystemDesign.md`](CheerSystemDesign.md)**로 이동했다. 이 문서는 **Tutorial 씬의 콘텐츠·구역 배치·게이트 흐름**만 다룬다 — 응원 버프 규칙은 이 문서에서 다루지 않음.

관련: [`NetworkDesign.md`](NetworkDesign.md) §6B (네트워크·수명주기 관점 SSOT — 이탈 정책, Kick 폐지, Invite HUD, 게이트 동작. 이 문서는 콘텐츠 관점만).

**범례**

| 태그 | 의미 |
|------|------|
| **[Ship Must]** | **2026-09-01 정식 출시** 전 필수 |
| **[Post-Launch]** | 정식 이후 |

---

## 0. 정식 출시 — 범위 요약 (Tutorial 관점)

| 항목 | **[Ship Must]** |
|------|-----------------|
| 씬 흐름 | Title → **Tutorial** → M1…5→M.Boss → T1…5→T.Boss → End.Demo (`1.Lobby` 폐지, 2026-08-17) |
| Tutorial 씬 | **필수 경로** (연습 구간은 경험자 생략 가능). **구 Lobby 역할(색 배정·Invite·Start) 흡수** — §2 |
| CheerName/TeamCheerWord | **Tutorial 씬에서 설정** — 개인 CheerName은 각자, TeamCheerWord는 Host. 규칙 상세는 `CheerSystemDesign.md` §3 |
| **말해보기** | Tutorial에서 확정↔재변경 반복 가능. 상세는 `CheerSystemDesign.md` §5 |
| 인게임 설명 | Tutorial(핵심 메카) + `DialogueUI`(M/T 구역별) |
| **멀티 연결** | Steam P2P + Lobby(`NetworkDesign.md` ④) |
| **목표** | 2026-09-01 원격 협동 + 보이스 + 응원 + Tutorial |
| **개발자 테스트** | PC 2대 → Steam 2인 Must; 4인 1회 권장 |

> **데모/Playtest 없음.** 원격 IP Join/UDP discovery 미사용. 개발=ParrelSync·localhost, 배포=Steam(`ReleaseRoadmap.md` §3).

응원 버프 규칙(개인/팀 버프, CheerName/TeamCheerWord 검증 규칙, Vosk 인식, 네트워크 RPC, 버프 UI)은 전부 **`CheerSystemDesign.md`** SSOT.

---

## 1. 씬 흐름

```
Title → Tutorial → M.Stage1…5 → M.Boss → T.Stage1…5 → T.Boss → End.Demo
```

정식 경로에 Tutorial **포함**. `1.Lobby`는 더 이상 존재하지 않는다 — 접속 즉시 `Tutorial`에 캐릭터가 스폰된다(색 자동배정, `NetworkDesign.md` §6B.2). 연습 콘텐츠(Stealth/응원 등)는 경험자가 생략 가능하지만(§4), **사전 게이트 구간 자체(스폰·게이트 통과)는 누구도 생략 불가**.

---

## 2. Tutorial 구역 (4구역)

Tutorial은 **자유 이동 구간**이다 — 아래 구역을 순서 상관없이 자유롭게 오가다, 마지막에 `TutorialGatherZone`에 모이면 `M.Stage1`로 넘어간다.

| # | 구역 | 내용 | 신규 | 경험자 |
|---|------|------|------|--------|
| (사전) | 접속/스폰 | 접속 즉시 스폰 + 색 자동배정(중복없음) + Invite HUD(구 로비 흡수, `NetworkDesign.md` §6B) | 필수 | 필수 (생략 불가) |
| 1 | 스텔스 체험 | 은신 플레이 감 잡기 | 있음 | **생략 가능** |
| 2 | CheerName/TeamCheerWord 설정 | 상호작용 표지판(`TutorialCheerNameSignboard`) → 개인 CheerName 입력·확정·말해보기(자유 반복) + **Host 전용 TeamCheerWord 입력 필드**(신규, §3) | 표지판 상호작용으로 개폐 — `PlayerPrefs` 스킵 없음 | **생략 불가** (매 판 재입력) |
| 3 | 응원 1회 체험 | 자기 CheerName 발화 → 개인 버프 발동 감 잡기 + (인원 2+ 시) TeamCheerWord 다같이 외쳐서 팀 버프 체험 | 있음 — **개편 필요**(구 cross-target 체험 → self+team 체험으로 교체) | **생략 가능** |
| 4 | `TutorialGatherZone` | 전원이 존에 모이면 카운트다운 → `M.Stage1` (§5) | **필수** | **필수** |

> **색 패드 연습(후보로 검토했던 것):** 보류. 필요성이 재확인되면 별도 구역으로 추가 논의.

**구역 2/3 안내 문구 갱신 필요:**
- 구역 2 패널에 "확정 후 팀원에게 이 이름을 외쳐달라 해서 인식되는지 확인해보세요" 안내는 유지.
- **[신규]** "음성 인식이 잘 안 되거나 마이크가 없으면 옵션(Options) → 숫자키로 응원하기를 켜세요" 안내 추가 (`CheerSystemDesign.md` §6.2).
- **[신규]** Host에게만 보이는 TeamCheerWord 입력 섹션에 "팀 전체가 함께 외칠 단어를 정해주세요(기본값: fighting)" 안내.

---

## 3. CheerName / TeamCheerWord 설정 UX (Tutorial 화면)

> 검증 규칙(형식·금칙어·중복·충돌)은 `CheerSystemDesign.md` §3 SSOT. 이 절은 **Tutorial 화면에서 어떻게 입력·확정하는가**만 다룬다.

### 3.1 어디서 설정하나

- **씬:** `Tutorial` 하나. 별도 로비 씬 없음.
- **개인 CheerName:** Tutorial 진입(=접속) 즉시 스폰돼 있는 **Player별로 독립된 이름** — "슬롯" 개념 없음. 각자 자기 화면에서 자기 캐릭터의 이름만 입력.
- **TeamCheerWord [신규]:** 같은 패널에 **Host에게만 보이는** 별도 입력 섹션. 비-Host는 현재 값을 읽기 전용으로 확인만.
- **UI:** `TutorialCheerNameUI`(로컬 입력창). 확정 시 자기 캐릭터 머리 위 이름표(`PlayerNameTagUI`)·팀원 화면에 즉시 반영.
- 채팅 UI로 설정하지 않음. 타이틀에서 설정하지 않음. `PlayerPrefs` 기억 없음 — 매 판 새로 입력.

### 3.2 확정 → 말해보기 → 재변경 (자유 반복)

```
입력 → Enter(확정 제출) → Host 검증 → 통과 시 전원에 즉시 반영 + Vosk grammar 재빌드
  → 말해보기(육안·청각 확인) → 마음에 안 들면 다시 입력 → …(반복)
```

| 항목 | 규칙 |
|------|------|
| 잠금 | **없음.** Ready 같은 상태가 Tutorial엔 없으므로 언제든 재확정 가능 |
| "최종 확정" | 별도 단계 없음 — **`TutorialGatherZone` 통과 시점의 값이 곧 최종값** (§5) |
| 강제 | 말해보기 실패해도 진행 가능. 안내만 |
| 개폐 | 상시 표시 아님 — 구역 2 표지판(`TutorialCheerNameSignboard`) 상호작용으로 게이트 통과 전까지 언제든 열어 재확정 |

### 3.3 소유·색 변경

- CheerName은 색/캐릭터가 아니라 **플레이어(슬롯)**에 붙는다. 색을 바꿔도 커스텀 문자열은 플레이어를 따라간다.
- TeamCheerWord는 플레이어에 붙지 않고 **세션(방) 전체**에 붙는다 — Host가 바뀌어도(없음, 이 프로젝트는 Host 고정) 값 자체는 세션 동안 유지.

---

## 4. 경험자 판정

| 방식 | 설명 |
|------|------|
| `PlayerPrefs TutorialCompleted = 1` | `TutorialGatherZone` 통과 후 저장 — Stealth/응원 1회 구역 스킵 판단용 (CheerName·TeamCheerWord·게이트는 이 값과 무관하게 항상 수행) |
| (선택) 「연습 건너뛰기」 | 첫 판 숙련자 |

---

## 5. `TutorialGatherZone` · Dialogue

- **`TutorialGatherZone`**: 색 구분 없는 **단일** 트리거 존. 존 안 인원 == 접속 중인 전체 인원이면 카운트다운 → 통과 시 인원 동결 → `M.Stage1` 로드. **동적 인원(중간 합류/이탈)에도 헤드카운트 비교라 별도 로직 불필요.** 네트워크 세부(이탈 정책, Writer, 솔로 케이스)는 `NetworkDesign.md` §6B.3~4가 SSOT.
- 구 `StageStartGate`/`ColoredStartZone`(색별 지정 구역) 방식은 Tutorial에서 **`TutorialGatherZone`으로 대체**됐다. **M/T 스테이지의 색별 게이트는 영향 없음** — 그쪽은 계속 `StageStartGate`/`ColoredStartZone`/`StageNetworkState` 유지.
- DialogueUI: Tutorial = 손 연습, M/T = 구역별 필수.

---

## 6. Tutorial UI 컴포넌트

- CheerName/TeamCheerWord **입력·확정(자유 재변경)** — `TutorialCheerNameUI`(§3.2, Tutorial 상시 HUD Canvas에 부착, Player 프리팹 아님) + 구역 2 상호작용 표지판(`TutorialCheerNameSignboard`)이 개폐.
- "말해보기"는 별도 테스트 기능이 아니라 패널의 안내 문구로 대체 — 실제 응원 제출이 곧 테스트(`CheerSystemDesign.md` §5).
- Gate 카운트다운 — `TimerUI`/`OnCountdownTick` 재사용.
- **[신규]** 마이크 없음/인식 실패 대비 "설정에서 숫자키 켜기" 안내 (§2 구역 2/3).

---

## 7. 구현 순서 (Phase — Tutorial 관점)

> 응원 코어 구현 순서(Phase A~E)는 `CheerSystemDesign.md` §10이 SSOT.
> **Phase A·B 완료. 다음 착수점은 Phase C** (`CheerSystemDesign.md` **§10.2**). 아래는 Tutorial 콘텐츠 관점만.

### Phase 7 — Tutorial · 커스텀 **[Ship Must]**

- CheerName/TeamCheerWord UI, 사전 검증/대체 단어(`CheerSystemDesign.md` §5), 말해보기, Tutorial 씬 4구역 배치.

**테스트:** Dev Build ② 2인 — Tutorial 이름+말해보기+인게임 응원(§8).

---

## 8. 테스트

| Phase | 환경 | 인원 | 확인 |
|-------|------|------|------|
| 7 | Dev Build ② | 2 | Tutorial 이름/TeamCheerWord 설정 + 말해보기 + 인게임 응원 |
| 7 | Steam P2P | 2 (Must) | Tutorial 전체 흐름 (사전 게이트 → 4구역 → GatherZone) |
| 7 | Steam P2P | 4 (권장) | 4인 GatherZone 헤드카운트, Host TeamCheerWord 설정 확인 |

### Ship Must 시나리오

**Steam 2인 (2PC — 출시 최소 게이트):**

- [ ] Steam Lobby Join → Tutorial(사전 게이트 구간 통과) → M 풀코스(+Boss) → T 풀코스(+Boss)
- [ ] 구역 2: 개인 CheerName 설정 + Host TeamCheerWord 설정
- [ ] 구역 3: 개인 응원 체험 + 팀 응원 체험
- [ ] `TutorialGatherZone` 통과 → `M.Stage1` 진입

---

## 9. 구현 체크리스트 (Tutorial 관점)

### **[Ship Must]**

- [ ] ~~`1.Lobby` 씬~~ — **삭제 완료.** `0.Title`→`Title`, `2.Tutorial`→`Tutorial` 리네임 (에디터 작업, 사용자 담당)
- [ ] `TutorialNetworkManager` — 접속 즉시 스폰, 색 자동배정, 사전구간 이탈 처리, 게이트 통과 시 Start 로직 (SSOT: `NetworkDesign.md` §6B)
- [ ] `TutorialGatherZone` — 색 무관 단일 헤드카운트 게이트 (§5, `NetworkDesign.md` §6B.3)
- [ ] Tutorial 상시 HUD — 룸코드 표시 + Steam Invite 버튼 (구 로비 UI 역할). 게이트 통과 후 숨김
- [ ] `TutorialCheerNameUI` — 개인 CheerName 입력 (완료) + **[신규] Host 전용 TeamCheerWord 입력 섹션 추가**
- [ ] `TutorialCheerNameSignboard` — 구역 2 상호작용 표지판 (완료)
- [ ] **[필수] Title/Tutorial UI 로컬라이제이션** — 하드코딩된 한국어 문자열을 String Table 방식으로 전환
- [ ] 구역 3 재설계 — 구 cross-target 응원 체험 → **자기 응원 + 팀 응원(TeamCheerWord)** 체험으로 교체 (미구현)
- [ ] 구역 2 안내 문구에 "숫자키 응원 설정" 안내 추가
- [ ] 연습 구역(Stealth/응원 1회) — 색 패드는 보류
- [ ] Dev Build ② 2인 (중간) — Tutorial 이름+TeamCheerWord+말해보기+인게임 응원
- [ ] Steam P2P ④ 2인 (2PC — 출시 게이트)
- [ ] Steam 4인 1회 (권장)

---

## 10. 관련 코드 · 문서

| 항목 | 경로 / 비고 |
|------|-------------|
| 응원 시스템 전체 | **`CheerSystemDesign.md`** |
| 게이트(M/T 스테이지) | `Assets/Scripts/Stage/StageStartGate.cs` |
| 발판(M/T 스테이지) | `Assets/Scripts/Stage/ColoredStartZone.cs` |
| `TutorialGatherZone` | 색 무관 단일 게이트 (§5) |
| 네트워크 플레이어 | `Assets/Scripts/Network/NetworkPlayerSetup.cs` |
| 스테이지 네트워크 | `Assets/Scripts/Network/StageNetworkState.cs` |
| 대화 | `Assets/Scripts/UI/DialogueUI.cs` |
| 네트워크 설계 | `Assets/Docs/NetworkDesign.md` (§6B = Lobby 흡수 SSOT) |
| Tutorial 씬 | `Assets/Scenes/Tutorial.unity` |
| Tutorial 네트워크 | `Assets/Scripts/Network/TutorialNetworkManager.cs` |
| Tutorial CheerName UI | `Assets/Scripts/UI/TutorialCheerNameUI.cs` |
| Tutorial CheerName 표지판 | `Assets/Scripts/Stage/TutorialCheerNameSignboard.cs` |

---

## 11. FAQ

**Q. 응원 버프 규칙(개인/팀, 쿨타임, 인식 등)은 어디 있나?**
A. **`CheerSystemDesign.md`.** 이 문서에는 없음.

**Q. CheerName은 로비에서? Tutorial에서?**
A. **Tutorial.** 입력·확정·재변경·말해보기 전부 Tutorial 씬에서 처리. 로비 씬 자체가 없어졌으니 애초에 다른 선택지가 없다.

**Q. TeamCheerWord도 Tutorial에서 정하나?**
A. **맞다.** 같은 CheerName 설정 패널(구역 2)에 Host 전용 입력 섹션이 추가된다. 상세 규칙은 `CheerSystemDesign.md` §3.

**Q. `1.Lobby` 씬을 없애면 색 선택·Ready·Kick·초대는 어떻게 하나?**
A. 전부 `Tutorial`로 흡수됐다(`NetworkDesign.md` §6B). 색은 접속 시 자동 배정(선택 UI 없음), Ready/Start는 `TutorialGatherZone`(§5)이 대신하고, Kick은 완전 폐지, 초대는 Tutorial 상시 HUD로 이동.

**Q. Tutorial에서 사람이 계속 늘거나 줄면 게이트가 이상해지지 않나?**
A. 안 그런다. `TutorialGatherZone`은 색별 지정이 아니라 "존 안 인원 == 현재 접속 인원" 헤드카운트 비교라 인원 변동에 자동으로 맞춰진다(§5, `NetworkDesign.md` §6B.3).

**Q. 말해보기 실패하면 진행 못 막나?**
A. **아니오.** 강제 아님. 이름 수정 안내만, 몇 번이든 다시 시도 가능.

**Q. Tutorial 매 판 5~8분?**
A. **아니오.** `TutorialCompleted` 시 Stealth/응원 1회 구역은 스킵하고 CheerName/TeamCheerWord 입력 + `TutorialGatherZone` 직행.
