# Coop Stage Audit — M (입)

입 스테이지·미니게임·M.Boss 감사 SSOT.  
공유 규칙(인원·2인 테스트·버킷·금지·세션 길이): [`CoopStageAudit.md`](CoopStageAudit.md).  
식도: [`CoopStageAudit.T.md`](CoopStageAudit.T.md).

관련: [`CheerSystemDesign.md`](CheerSystemDesign.md) (RPC·투표·그래머 — 팀 **효과**는 여기 §4). [`MinigameDesign.md`](MinigameDesign.md)와 충돌하면 **이 문서가 이김**.

**확정:** 2026-09-03. M은 **개념 잠금**. 다시 묻지 말 것. 초·데미지는 해당 스테이지 때.

**범례:** [확정] = 승인 완료. 수치는 해당 스테이지 때.

---

## H. 핸드오프 **[확정]**

`.cs` / Docs만 에이전트. 씬·MCP는 사용자 “구현해” / “MCP로 수정해줘” 전까지 금지.

M1–5·M.Boss를 다시 묻지 말 것. T5·T.Boss는 보류.

### H.5 다음 에이전트 — 여기부터 (2026-09-04)

**닫힌 트랙:** M 팀 응원 되돌림 (입 닫힘·침·혀). 코드+에디터+플레이 확인 **됨** (2026-09-04). 입/침/혀 머신 다시 열지 말 것.

**다음 트랙:** M 인게임 판을 잠금에 맞추기. T1 조임은 이 트랙 뒤.

1. **읽기:** 이 절 → §2 Barrier · §3 ColorTile. 공유 [`CoopStageAudit.md`](CoopStageAudit.md) §H.5. T는 아직 구현하지 않음.
2. **할 일:** ColorTile 점수제 §3 — 코드 됨. 타이머·발동 스케줄·실패 패널티 **폐기**. **에디터:** M.Stage3 `uniqueQuota`=6, 흑·백 배열 1인 4/4 · 2인 6/6 · 3인 7/7 · 4인 8/8, `tilePrefabs`에 Black·White, `ColorTileRoundObjective.targetTime`=180. Barrier §2.1 슬롯 코드 됨(2026-09-05) — M.Stage1 이동은 에디터 남음. **Barrier incoming = §2.2 감독 — `ArrowIncomingDirector` + `ArrowTrap.FireOnce()` 코드 됨(2026-09-05). 에디터(사용자) 남음: §2.2 표 참고.** Sequence / Grid 손대지 않음.
3. **하지 말 것:** ColorTile에 넉백·문 내림·광장화. 입/침/혀 재설계. 팀 힐·120초. 새 RPC. Tutorial 팀 외침(마지막). T 조임·안개. T5. **Incoming 감독을 Barrier 색 배정·리빌·입 닫힘 창과 연동(§2.2 재확인).**

**혀 반영 (2026-09-04) [확정]**

| 항목 | 내용 |
|------|------|
| 코드 | `TongueController`. 새 RPC 없음. 4.1 `RiseHold` / 설계 4.2 `AttackSweep` |
| 에디터 | `Tongue.controller` 트리거. `TongueAttack.fbx` 경기장 혀. `MouthBG` 혀 안 씀 |
| 4.1 | 가운데 **1칸** (`MiddleRingTile`). 3×3 9칸 **폐기**. 사용자 선택 |
| 4.2 | 씬 GO 이름 `Stage4.3`. 왼 10 + 오른 10 `FloorTile`. 가운데 1×5는 배열에 없음 |
| 스윕 | `SweepBreak` 이벤트 **안 씀**. 클립 끝나면 `BreakRemaining()`이 해당 배열을 끔 |
| 플레이 | 입 닫힘(M.Stage1)·침(M2)·혀(M4) 확인됨 (2026-09-04). 숫자(`warnDuration`)는 나중에 |

**되돌림 머신 (입 기준, 혀·침·조임 동일):**  
Idle(응원 무시) → Warning(UI, 응원 켜짐) → 외침이면 Attack 안 넣음 / 없으면 Attack 끝까지 → Hold(유지, 암전·침·혀 나온 채) → 외침이면 Recover 클립 → Idle.  
입 Recover = **Open**. 침 Recover = 수면 페이드아웃. 혀 4.1 Recover = **Retract**(Hold 포즈에서 시작, Rise 역재생 아님). 혀 4.2 Recover = 꺼진 1×1 복구 + Idle (**Retract 없음**. Hold 대기 없음 — Attack 후 다음 사이클 반대쪽). Close/Cover/Rise/Attack_L/R이 시작되면 끊지 않음. 자동 Open 없음. 닫힘 대가 = 암전만(HP 없음). 침 대가 = 미끄럼(HP 없음). 혀 대가 = 꺼진 1×1 낙사(HP 없음).

**코드 (있음):**
- `CheerService` — 투표 RPC 유지. `ApplyTeamBuff` = Heal/120 없음 → `BuildRevertOrder`로 세대·재개 시각을 정해 `BroadcastTeamBuffActivatedClientRpc(generation, resumeAt)`. `ValidateTeamCheer`는 `_revert.IsAvailable` + 창 소비 latch. `RegisterRevert`(중복 등록 경고) / `NotifyHazardWindow`(창이 닫히면 표 리셋).
- `ITeamCheerRevert` — 씬당 하나. `IsAvailable` / `BuildRevertOrder` / `Revert(generation, resumeAt)`.
- `MouthController.teamCheerHazard` — true면 위 머신. false면 옛 Close→Hold초→Open.
- `SalivaHazard` — M2 revert. Warning→Cover→Hold→Recover. `SalivaVolume`이 발판 위일 때만 `Player` 얼음 미끄럼.
- `TongueController` — M4 revert. 4.1 RiseHold / 4.2 AttackSweep. `SweepBreak` 이벤트 안 씀(클립 끝 `BreakRemaining`).
- `TeamCheerWarningUI` — `OnHazardWindowChanged`. `TeamCheerCleared` — `OnTeamBuffActivated`. 팀 쿨 HUD는 **삭제됨**(스크립트·NV·세션 저장까지).

**되돌림 동기화 (2026-09-05 리뷰 반영) — 함정 3종 공통:**
- 명령은 **Host 권한**. 세대 번호가 낮거나 같으면 무시하므로 같은 창이 두 번 발동해도 되돌림은 1회.
- 명령을 받은 시점에 창이 아직 안 열린 머신은 그 창을 **열지 않고 건너뛴다**(`_skipNextWindow`). 혀 4.2는 건너뛸 때도 복구·공격 방향 소비를 같이 해 Host와 좌/우가 어긋나지 않게 한다.
- 첫 창은 `StageNetworkState.PhaseStartServerTime` 앵커(WindTrap/ArrowTrap과 같은 패턴). 앵커가 없는 씬은 예전대로 로컬 폴백. 앵커를 읽기 전에 **한 프레임 양보**한다 — `PhaseManager.EnterPhase`가 `objectsToEnable.SetActive(true)`를 `MarkAndSyncPhase`보다 먼저 하므로, Phase가 켜주는 함정(혀 4.1↔4.2)이 곧바로 읽으면 Host만 직전 Phase의 낡은 앵커를 잡는다(`SafeZoneWarnSign`과 같은 이유).
- 간격 추첨은 `Random.state`를 저장·복원한다. `InitState`가 전역 RNG를 갈아엎어 같은 씬의 다른 시스템이 이 시드 스트림을 물려받는 걸 막는다(결정성은 그대로).
- 간격 시드는 **축을 분리**한다(`ScheduleAxis` / `RevertAxis`). `_cycleCount`와 되돌림 세대가 둘 다 1,2,3…이라 축을 안 섞으면 같은 간격이 반복된다.
- 재개 시각 대기는 매 프레임 필드를 다시 읽는다 — 대기 중에 새 명령이 오면 그 예약을 따라간다.

**입 고유 (2026-09-05 리뷰 반영):**
- `MouthController.OnDisable`이 `screenFader.FadeIn(0f)`으로 암전을 걷는다. 페이드는 ScreenFader 자기 코루틴이라 `StopAllCoroutines`로 안 멈춘다 — 없으면 Hold 중 입이 꺼질 때 화면이 까만 채로 굳는다. `StopCycle()`에도 같은 복구가 있지만 **그쪽은 코드에서 호출되지 않는다(ContextMenu 전용)**.
- 연출 전용 입(`teamCheerHazard=false`, `AutoCycle`)도 같은 `ResolveFirstWindow` 앵커를 쓴다. 이 입들은 `screenFader`가 비어 있어 암전은 없지만 배경 연출 위상을 맞춘다.
- Hold는 **외침 전까지 무한 대기**가 설계다(자동 Open 없음). Closing·Holding 내내 `_available`이 true라 Hold 중 외침이 먹힌다.

**에디터 (됨):** `M.Stage1` / `M.Stage3` / `M.Boss`의 GO 이름 `MouthController`만 `teamCheerHazard=true`. `TransitionPhase*`·M2 입·M4·M5는 false. `UI.prefab`에 `TeamCheerWarning` + `Assets/Figma/Lobby/Warning.png`, Fadeout보다 위(마지막 형제).

**침 구현 (2026-09-03) [확정]**

로직 됨. 수면 아트는 다른 에이전트. 수치(`salivaAccelTime` / `salivaDecelTime`)는 플레이로 깎음.

| 항목 | 내용 |
|------|------|
| 범위 | **M.Stage2 전체.** 2.1 SideSplit + 2.2 Drop. (옛 잠금 “2.1 침 없음” 폐기) |
| revert | 씬당 하나 = `SalivaHazard`. 입 `teamCheerHazard`는 M2에서 **끔**. 새 RPC 없음. 머신 = 입과 동일 (Warning→Cover→Hold→Recover) |
| 미끄럼 | PhysicMaterial **안 씀.** `Player.Move()` 얼음: 입력 중엔 가속만 더함(출발이 느리고 밀림). 손 떼면 감속만 약하게(관성으로 쭉). 방향 전환도 얼음(반대 스틱해도 예전 속도가 남음). 목표속도로 끌어당기면 물속 저항이 되므로 폐기 |
| 수치 | `Player.salivaAccelTime` 기본 1.2초(정지→풀속도). `Player.salivaDecelTime` 기본 3.5초(풀속도→정지). **Decel > Accel.** 플레이어 프리팹 인스펙터 |
| 씬 GO | 루트 `SalivaHazard`. `SalivaVolume_2_1`(Stage2.1, Ground 25×15 위). `SalivaVolume_2_2`(Stage2.2 자식). LEFT/RIGHT 기둥에는 안 깔음 |
| 비주얼 | M.Stage2는 `coverRoots` 2 / `coverRenderers` 2 / `coverDropPrefab` **연결됨** (`coverAlpha` 0.9, 스폰 높이 50, 낙하 50). `coverParticles` 슬롯은 코드에 없음. 방울 착지 Y는 볼륨 `bounds.max.y` — 하드코딩 0 아님. 볼륨은 coverRoots 자식으로 넣지 말 것(Awake 경고) |

**에디터 (침):** 위 표. 수면 연결은 나중에 빈 슬롯에.

### H.2 M 잠금 (다시 묻지 말 것)

| 항목 | 잠금 |
|------|------|
| M1 | `DirectionalBarrier`를 보스에서 앞으로. 패드→문 상승→incoming 함정 파괴. 뮤텍스 = 한 색만 업. **소리 초출.** 통과 퍼즐 아님. 슬롯 = §2. `Distribute` 1인=전원동색 / 2인=2+2 **쓰지 않음**. Incoming = §2.2 감독(계단 텀, Barrier와 무연동). 기존 스케줄 값은 유지 — 공존 방식 미정 |
| M2 | **한 씬, 두 구간.** 2.1 SideSplit + **침**. 2.2 Drop + **침**. 암전 안 씀. 2.1 위에 Drop 안 얹음. 라운드로 시간 안 벌음 |
| M3 ColorTile | **컷 취소.** 점수제 §3. 각자 칸 서기 폐기. **입 시계.** 3분(2–5) |
| M4 | **한 씬, 두 구간.** 4.1 SequenceRing 턴제 + **혀 초출.** 4.2 ArrowTrap + **혀 복습**. 링 위에 화살 없음. M6·M7 없음. 리듬·16칸 암기·검정만 늘리기 **폐기** |
| M5 | Grid Color+BW **유지.** 2인 장면 = BW 후반. Color/1인 쉬움 수용. **WindTrap 유지**, 강도만 사용자. 바람에서 협동 찾지 않음. **입 열기 없음** |
| ColorTile 점수 | 2초(기본) 또는 3초 점유 → 뽕 → 그 색 +1 → **다른 칸에 재스폰**. 고유는 주인만, 흑백은 아무나. 통과 = 고유+흑+백 의무. 흑백 의무 0 금지. 통로 좁게. **압력 = 할당량 + 입 창.** 함정으로 협동 안 만듦. 넉백·문 내림·광장화 안 씀 |
| 소리 초출 | **M1.** 외침으로 닫힘 막기. 닫힘의 맛 = **암흑 시야 정도는 가져감.** 데미지·둘 다는 나중에. M3·보스 복습 |
| 침 초출 | **M2 (2.1부터).** 2.2·보스 복습. PhysicMaterial 아님 — `Player.Move()` 얼음 가속/코스트 (`salivaAccelTime` / `salivaDecelTime`). §6 |
| 혀 초출 | **M4.1.** 보스·M4.2 복습. M6·M7 없음. 4.1 가운데 1칸. 클립 끝에 칸 끔 (`SweepBreak` 안 씀). 꺼진 칸 낙사→방 리셋. §5 |
| 입 창 리듬 | **개념만.** M1·M3·보스. M2·M5 없음. 초·횟수·데미지는 나중에 |
| M.Boss | §7. 1 Barrier+침, 2 Drop+화살+혀, 3 Sequence+닫힘, 4 ColorTile+침+닫힘, 5 혀가 바닥을 부수고 삼켜 T. 시드=Host ChallengeStart |

### H.3 M에서 버린 제안

- ColorTile 컷 (취소됨)
- M2를 암전·보이스 차단·이심전심으로 길게 (보스 의식 후보만. M2 본체 아님)
- SequenceRing 메트로놈/리듬, 16칸 한꺼번에 외우기, 검정만 늘리기
- Grid / Wind 컷, Wind로 협동
- Barrier를 통과·알코브 퍼즐, 1인 4문 동일색
- 흑백 할당량 0인 ColorTile
- M6·M7, 링 위에 ArrowTrap, 2.1 위에 Drop
- 혀 맞음을 약한 밀침으로, 침을 PhysicMaterial로
- 팀 응원 +2힐·120초를 M 시계로, 창 중 재외침으로 연장, 계속 고함
- ColorTile에 ContactKnockback·흑백 문 내림·고유색 벽·광장화로 난이도 (압력은 할당량+입 창)
- Incoming 레인 선택을 Barrier 색 슬롯·라운드 상태와 연동("겹치는 incoming으로 2인 장면" 폐기) — 레인은 방향일 뿐, §2.2
- Incoming 감독을 리빌 구간·입 닫힘 창(소리 초출)과 연동해 정지시키기 — 불필요, §2.2에서 뺌
- Incoming에 Tracker 부착, 화살 속도 단계(`speedPhases`)로 난이도 이중화 — 텀 하나만

### H.4 코드

| 대상 | 상태 |
|------|------|
| CheerService 팀 | **됨.** Heal·120초 폐기. Warning~Revert만 유효. 새 RPC 없음. 입 닫힘·침·혀 연결 |
| MouthController | **됨.** hazard 씬만 Close→Hold(외침까지)→Open. 자동 재오픈 없음 |
| 침 | **됨.** `SalivaHazard` / `SalivaVolume` / `Player` 얼음. 수면 비주얼은 슬롯만 비움 |
| 혀 | **됨.** `TongueController` + M.Stage4 에디터. 4.1=1칸, `SweepBreak` 안 씀. 플레이 확인 (2026-09-04). 보스용 `MixedSweep` 패턴 **코드 됨** (2026-09-05) — M.Boss 배선은 아직 |
| ColorTile | **점수제만.** unique 6 + 인원별 흑/백(4/4, 6/6, 7/7, 8/8). 목표 시간 = `targetTime`(기본 180, 권장 120–300). 타일 `Black`/`White`. M.Stage3 인스펙터 남음 |
| Barrier | §2.1 색 슬롯 표 **코드 됨(2026-09-05)** — `DirectionalBarrierRound.BuildBarrierSlots`가 균등 분배(`GameSessionColorDistribution.Distribute`) 대신 확정 표(1인=고유2+백+흑 / 2인=A+B+백+흑 / 3인=고유3+백1 / 4인=고유4)로 배정. 타일도 슬롯 중복 없이 색당 1개만 스폰(1인 고유 패드 1개 → 고유 문 2개). 시작 흐름 **Reveal/CloseAndSpawnTiles 2단계로 분리(2026-09-05)** — `Activate()`(단일 호출) 폐기. `Reveal()`은 배치+Open만 하고 자동으로 안 닫힘(다이얼로그 프리뷰용), `CloseAndSpawnTiles()`가 Close+타일 스폰(진짜 라운드 시작). Reveal 없이 CloseAndSpawnTiles만 호출해도 그 자리에서 스폰부터 자동 수행(무프리뷰). Incoming 감독(§2.2) — `ArrowIncomingDirector`·`ArrowTrap.FireOnce()` **코드 됨(2026-09-05)**. **에디터(사용자, 남음):** M.Boss→M.Stage1 이동, `barrierPrefabs`/`tilePrefabs`에 White·Black 문/타일 프리팹 추가, 화살 `Breakable` 부착, M.Stage1 Phase0 onPhaseEnter→`Reveal()` + `StageStartGate.OnCountdownComplete`→`CloseAndSpawnTiles()` 연결 |
| Sequence / Grid | 룰 유지. 손대지 않음 |

잔여 버킷 C: M3 Drop, 4.1 Drop. 수치는 해당 스테이지 때. Tutorial 팀 외침 = 마지막(지금은 빈 성공).

---

## 0. 입 동사 **[확정]**

M = 한정된 발판. 한 입에 붙어 있는 협동. 시계 = 입이 열린 창. 복도에서도 되면 M 전용 아님.

| 동사 | 한 줄 |
|------|------|
| 깨물림 | 닫히기 전 전원 같은 틈으로. (창 실패 맛은 암흑. 데미지는 나중에) |
| 벌리기 | **안 씀.** 소리와 겹침 |
| 소리 | **채택 §4.** 초출 M1. 닫힘 막기 |
| 침 | **채택 §6.** 초출 M2(2.1부터). 외치면 지움 |
| 혀 | **채택 §5.** 초출 M4.1, 복습 M4.2 |

---

## 1. 감사 보드 **[확정: 개념]**

| 씬 | 컨텐츠 | 버킷 | 남길 장면 | 바꿀 판정 | 빼도 되는 함정 |
|----|--------|------|-----------|-----------|----------------|
| ColorTile | 공유 룰 | **B** 점수제 | 흑·백 할당량. 좁은 길 | 각자 칸 서기 폐기. §3 | 함정으로 협동 안 만듦. Drop은 C |
| Grid | Color+BW | **A** 유지 | BW 후반 붙거나 흑백 분기 | Color 각자 칸·1인 쉬움 수용 | Wind 유지, 강도는 사용자 |
| M.Stage1 | Barrier + 소리 초출 | A. §2 · §4 | 부수기 + 뮤텍스. 닫힘 막기 | `Distribute` 2+2 / 1인 4면 동일색 안 씀 | 함정은 부술 대상 |
| M.Stage2 | 2.1 SideSplit+침. 2.2 Drop+침 | **A** §6 | 갈라서기. 침이 남아 미끄러짐 | 암전 안 씀. 라운드로 시간 안 벌음 | 2.1 위에 Drop 없음. 2.2 Drop은 침의 압력 |
| M.Stage3 | ColorTile + Drop + AdvancingWall | **B** §3 · §4 | 흑백 할당량 + 입 시계 | 점수제 | 실패 이빨은 남을 수 있음 |
| M.Stage4 | 4.1 링+혀. 4.2 화살+혀 | **A** §5 | 색 차례 + 혀. 혀가 바닥을 줄임 | 링 위에 화살 없음 | 4.1 Drop은 C. 4.2 화살은 혀의 압력 |
| M.Stage5 | Grid + Wind | **A** | BW 후반 | **입 열기 없음** | Wind **유지** |
| M.Boss | 5페이즈 §7 | 초출 금지 | 1–4 복습, 5 삼켜 T | 시드=Host ChallengeStart | 세이프존 입문·바람만 페이즈 없음 |

**M.Stage2.** 한 씬 두 구간. 이심전심 암전은 보스 후보만.

**M.Stage4.** M6·M7 없음. 흰은 아무나, 검은 누르면 안 됨(무입력 시 자동 통과).

**M.Stage5.** 입 열기 없음 — 바람과 안 맞음.

T.Boss ColorTile 인스턴스는 이 문서 §3과 같은 점수제.

---

## 2. DirectionalBarrier **[확정]**

M.Stage1로 옮긴다. 코드는 아직 안 바꿈. 통과·알코브 **안 씀**.

1. 패드를 밟으면 그 색 문이 올라온다.
2. 올라온 문이 incoming 함정을 부순다.
3. **한 번에 한 색만.** 1인 예외: 고유 패드 1개 → 고유 문 2개.

협동은 순서와 타이밍. Incoming 선택은 §2.2 감독 — **Barrier 색 배정과 무관, 연동 안 함.**

### 2.1 인원별 4슬롯 **[확정]**

| 인원 | 4슬롯 | 고유 패드 1개 |
|------|--------|----------------|
| **1** | 고유, 고유, 백, 흑 | 고유 문 **2개** 같이. 흑·백은 따로 |
| **2** | A, B, 백, 흑 | 그 사람 문 1개 |
| **3** | 고유 3 + **백 1** | 백은 공용 1개 |
| **4** | 고유 4 | 슬롯에 흑백 없음 |

고유색 패드 = 그 색만. 백/흑 = 아무나.

3인 4번째를 백으로 고정: 1·2인에 이미 흑+백이 있다. 3인은 슬롯이 하나뿐이라 공용 보험(백)을 남긴다.

1인 4면 전부 고유색 **금지**. 고유 패드 어느 쪽이든 밟으면 고유 문 두 개가 오른다. 흑·백은 따로. 고유를 밟는 동안 흑백 문은 내려가 있다.

클리어 = 웨이브 동안 버팀. 통과 존 없음. 입 시계: 열린 창 안에 막고, 닫힘 예고에 팀 외침.

### 2.2 Incoming 감독 **[확정, 코드 됨 2026-09-05]**

**Mouth1~4의 기존 `fireAtSeconds`/`loopSchedule`/`schedulePeriod` 값은 일단 유지 — 지우지 않음.** 감독과의 공존 방식(끄기/무시/교체)은 구현 착수 시 결정, 지금은 보류. Barrier 게임(색 슬롯·라운드·`ChallengeStart`)과는 **완전히 분리** — 감독은 어느 색 문이 열렸는지 모른다. 레인은 방향(자리)일 뿐, §2.1 슬롯 표와 코드로 안 엮는다. §2.1은 그대로 유지.

**대상:** `M.Stage1`의 실제 incoming `ArrowTrap` 4개(Mouth1~4). 추적형 등 그 외 `ArrowTrap`은 삭제됨 — 씬에 4개만 있어야 함.

**공존 방식 확정(2026-09-05):** Mouth1~4는 에디터에서 `startActive=false`로 자체 `TrapLoop()`을 꺼서 자동 발사를 막는다 — 이 감독의 `FireOnce()` 호출만이 유일한 트리거. `fireAtSeconds`/`loopSchedule`/`schedulePeriod` 값은 지우지 않고 남겨두되(§2.2 서두 원칙 그대로) 실제로는 안 쓰인다(`startActive=false`라 `Activate()`가 안 불림). "무시(둘 다 도는 것 감수)"·"교체(값 변환)" 두 대안은 폐기.

**규칙:**
1. 동시 발사 없음. 한 번에 **1레인만.**
2. 직전에 쐈던 레인은 다음 추첨에서 제외(나머지 3개 중 랜덤). 가방 셔플 아님 — 매번 재추첨.
3. 텀(발사 간격) = **유일한 난이도 축.** 계단식. 예: 0~25초 7초 텀 → 25~45초 5초 텀 → 45초~ 3초 텀(문 `duration`=3초가 바닥). 숫자는 플레이로 조정.
4. 리빌·입 닫힘 창 등 Barrier/입 상태로 감독을 멈추지 않음 — **연동 안 함** (H.3 참고, 재검토 불필요).
5. 속도 단계(`speedPhases`)는 안 씀 — 난이도는 텀 하나로만.

**구현 (코드 됨, 2026-09-05):**
- `ArrowIncomingDirector`(신규 MonoBehaviour, `Assets/Scripts/Traps/ArrowIncomingDirector.cs`) — Host 전용 루프(`nm.IsServer` 가드, `OnEnable`에서 시작). `ArrowTrap[] lanes`(4개 연결 예정), `float[] termSteps` + `float[] stepAtSeconds`(`StageNetworkState.PhaseStartServerTime` 기준 경과 — `Time.time` 아님, 없는 씬은 로컬 폴백). 매 텀마다 직전 레인 제외하고 나머지 중 재추첨 → `lane.FireOnce()`.
- `ArrowTrap.FireOnce()`(신규 public, `Assets/Scripts/Traps/ArrowTrap.cs`) — 기존 protected `FireWithCharge()`를 감싸는 감독의 유일한 진입점. `startActive=false`(위 공존 방식) 상태에선 `isRunning`이 항상 false라 `FireWithCharge()` 내부의 "충전 중 Deactivate 취소" 가드가 항상 발사를 막아버리는 문제가 있어, `FireOnce()`가 이번 호출 한 번만 `isRunning`을 켜고 끈다. `TrapLoop()`은 여전히 시작 안 함. 이미 충전/발사 중이면 중복 호출 무시.
- 시드/NV **불필요.** Host만 루프를 돌리고 Client는 기존 `OnPreFireCharge`/`OnFiring` → `SyncArrowChargeClientRpc`/`SyncArrowFireClientRpc` 릴레이로만 본다(ArrowTrap 자체 스케줄이 돌던 방식과 동일 통로). 새 RPC 없음.
- `Breakable`은 코드 손 안 댐(기존 그대로 씀) — `arrowPrefab`에 부착은 에디터 작업.

**에디터(사용자, 남음):**
- Mouth1~4: `startActive=false`로 변경(공존 방식 확정). 기존 `fireAtSeconds`/`loopSchedule`/`schedulePeriod` 값은 **지우지 않음**(안 쓰이지만 참고용 보존). `speedPhases=[]`만 비움. `baseSpeed`는 유지.
- `ArrowIncomingDirector` GameObject 배치, `lanes`에 Mouth1~4 연결, `termSteps`/`stepAtSeconds` 값 입력.
- `arrowPrefab`에 `Breakable` 추가, `breakTriggerLayers`에 Barrier 문 레이어 지정.

---

## 3. ColorTile 점수제 **[확정]**

컷 취소. M.Stage3 유지. 입 시계 안에서 점수. 코드 됨, 에디터 할당·스폰 남음. 통로 좁게. **3분** = 할당량 + 입 창 (인스펙터 2–5분). 라운드 수로 안 벌음. 상한은 `ColorTileRoundObjective.targetTime`(기본 180초). 넘기면 Fail.

**압력 (2026-09-04 재확인):** 점수 할당을 무조건 채운다 + 입 창이 점수를 끊는다. 너무 쉽다고 함정·문을 붙이지 않음.

점유: 연속 2초 또는 3초(기본 2) → 뽕 → 그 색 +1 → **다른 칸에 재스폰**. 발 떼면 리셋.

| 타일 | 누가 |
|------|------|
| 고유색 | 그 색만. 다른 색은 점수 없음 |
| 백 / 흑 | 아무나 |

통과: 고유 의무 **그리고** 백 의무 + 흑 의무. 덤 합산(의무보다 큰 총점) 없음. 흑백 의무 0 **금지**. 1인은 혼자 순환. 2인+는 몸이 고유+흑백보다 적으니 담당을 나눔.

2인: 흑·백을 아무도 안 채우면 실패. 자기 색만 밟으면 실패.

안 함 (2026-09-04): ContactKnockback으로 난이도. 흑백 문 내려 통로 넓히기. 고유색 벽 오르내리기. 맵을 광장·평행 길로 넓히기. 라운드 수로 분 벌기.

---

## 4. 팀 응원 = 입 **[확정]**

전원이 TeamCheerWord → 입이 한 일을 되돌린다. RPC는 `CheerSystemDesign`. +2힐·120초 폐기. 창 중 재외침 **무시**. 계속 고함 아님.

| 항목 | 잠금 |
|------|------|
| 닫힘 | M1·M3·M.Boss. 초출 M1 |
| 침 | M2(2.1·2.2)·보스 |
| 혀 | M4.1·M4.2·보스 |
| 없음 | M5 |

창 리듬(개념): 시작은 입 열린 채. 열린 창 = 색 일. 닫힘 예고에 전원 외침. 성공 = 다시 열림. 놓침 = 닫힘, 암흑 시야는 가져감. 초·데미지는 나중에. **M2는 입 닫힘 시계 없음** — 침 창만.

구현: `CheerService` + `MouthController.teamCheerHazard`(닫힘) / `SalivaHazard`(침) / `TongueController`(혀). 새 RPC 없음.

2인: 한 명이 외치지 않으면 갈라선 채로 깨문다.

---

## 5. 혀 **[확정: 개념]**

| 회 | 구간 | 역할 |
|----|------|------|
| 1 초출 | 4.1 SequenceRing | Rise→Hold→Retract. 가운데 **1칸**. 가림막. 링 위 화살 없음 |
| 2 복습 | 4.2 ArrowTrap | Attack 한 번에 L **또는** R 하나. 왼쪽/오른쪽 1×1 ×10 (2×5). Hold·Retract 클립 없음. 화살은 압력 |
| 보스 | M.Boss | 복습만. P2는 4.2쪽 |

제때 외침 = Attack 안 넣음. 늦게 외침 = 꺼진 1×1 복구 (이미 낙사면 방 리셋이 먼저).

**타일:** 인스펙터 배열. 배열 순서 = 스윕 순서(이벤트 쓸 때).
- 4.1 가운데: **1칸** (`MiddleRingTile`. 3×3 9칸 **폐기**, 2026-09-04)
- 4.2: 가로 5열 기준. 왼쪽 **10칸** (2×5) / 가운데 **5칸** (1×5) / 오른쪽 **10칸** (2×5). 가운데 1×5는 L·R 배열에 안 넣음. 3×5+3×5는 가운데 1×5가 겹쳐서 **폐기**. 씬 GO 이름 `Stage4.3`

**스윕:** `SweepBreak(int)` Animation Event **안 씀**. 클립 끝나면 `BreakRemaining()`이 해당 배열 남은 칸을 끔. 한 칸씩 따라가는 스윕 아님.

**4.1 머신:** Idle → Warning → 외침이면 Rise 안 넣음 / 없으면 Rise 끝까지 → Hold(가운데 칸 꺼진 채, 혀가 가림막 — 반대편 시퀀스는 돌아서 봄) → 외침이면 Retract + 칸 복구 → Idle.

**4.2 머신:** Idle → Warning → 외침이면 Attack 안 넣음 **그리고 꺼진 칸 전부 복구** / 없으면 **이번 방향 하나**만 끝까지 (L이면 왼 10칸, R이면 오른 10칸. 한 클립에 L+R 같이 안 함) → Hold 클립 없음. 혀 Idle.
- Attack 중 외침: 클립은 끊지 않음. 끝나면 꺼진 칸 전부 복구.
- 안 외치면 그 10칸은 꺼진 채 **다음 사이클이 반대쪽**. 그래서 L 다음 R을 놓치면 왼 10+오른 10이 꺼지고, **가운데 1×5는 두 번 다 맞아도 켜져 있음**.
- 방향: 한 번에 한쪽. 이번이 L이면 다음은 R. 첫 방향만 시드.

**보스 머신 (MixedSweep) [확정 2026-09-05]:** Idle → Warning → 외침이면 공격 안 넣음 **그리고 꺼진 칸 전부 복구** / 없으면 **이번 창의 영역 하나**만 끝까지 → Hold 없음 → 혀 Idle.
- 영역은 창마다 **가운데 3×3(9칸) / 왼 10칸 / 오른 10칸** 중 하나. 4.1처럼 Hold로 가림막을 세우지 않는다 — **가운데도 부수고 내려가는 공격**이다.
- 가운데는 Rise 클립으로 부수고 **Retract로 내려간다**(외침 여부와 무관). L/R 클립은 스스로 내려가므로 Retract 없음.
- 안 외치면 그 영역은 꺼진 채 남는다(4.2와 같은 대가). 외치면 **꺼진 칸 전부 복구**.
- 영역 추첨은 `NetworkSessionData.Seed` + **창 번호**(`_attackCount`). 로컬 `Random` 없음 — 머신마다 다른 영역을 부수면 한쪽만 낙사한다.
- 창 번호는 스킵·차단·완주 **세 경로 모두에서 1회씩** 소비된다(`AdvanceAttack`). 이게 깨지면 Host와 영역이 어긋난다.
- **이미 부서진 영역이 또 나오는 것은 허용** — 그 창은 헛방. 안 부서진 영역만 고르지 않는다.
- 4.1·4.2 본체에는 넣지 않음. 보스 전용.

**낙사:** 꺼진 칸에 서 있으면 낙사 → 방 리셋. 혀 히트박스 없음. 가운데 기둥 없음. `Breakable` 안 씀.

**프롭:** `TongueAttack.fbx` + `Assets/Animator/Tongue.controller`. `MouthBG` 혀 금지. 구간당 경기장 혀 1개. `ITeamCheerRevert` 씬당 하나 — 4.1/4.2 전환 시 활성 혀만 등록.

**시드:** 4.2 **첫** L/R만 `NetworkSessionData.Seed`. 이후는 교차. 전 머신 동일.

안 함: 4.1+4.2 한 바닥, 화살 전용 새 씬, 혀 무게로 기울이기, 혀 맞음=밀침, 4.2 3×5(가운데 겹침), 한 번에 L+R. 4.1 가운데 큰 판 1칸은 **허용**.

코드 `TongueController`. 에디터+플레이 확인 됨 (2026-09-04).

---

## 6. 침 **[확정: 개념]**

초출 **M2 전체**(2.1 SideSplit + 2.2 Drop). 보스 복습.  
외치면 지움. 안 외치면 미끄러운 채로. 피하기만으로는 클리어 아님.

**이동:** 얼음(극적 미끄럼). 물속 저항 아님. PhysicMaterial 아님.  
입력 중 = 가속만 더함(출발 느림·밀림). 손 뗌 = 약한 감속(관성으로 밀림). 반대로 꺾어도 한동안 예전 방향.  
인스펙터: `Player.salivaAccelTime`(기본 1.2), `Player.salivaDecelTime`(기본 3.5, Accel보다 크게).

**코드:** `SalivaHazard` + `SalivaVolume` + `Player.Move()`. 씬당 revert는 침 하나.  
**씬:** `SalivaHazard`, `SalivaVolume_2_1`(2.1 Ground), `SalivaVolume_2_2`(2.2 Floor).  
**비주얼:** M.Stage2는 coverRoots·coverRenderers·coverDropPrefab 연결됨(알파 페이드 + 볼륨당 방울 1회). 수면 아트만 나중에.

**미끄럼 권한:** 전 머신이 로컬로 `AddSalivaOverlap`을 걸지만 `Player.Move()`가 `isOwnerControlled` 게이트라 실제 효과는 오너 머신에서만 — Owner + CNT와 일치. 침 전용 RPC·NV 없음.

**배선 누락:** `volumes` 비었거나 `SalivaVolume.hazard` null이면 Awake에서 경고. 침이 깔려도 안 미끄러지면 콘솔부터 볼 것.

---

## 7. M.Boss 페이즈 **[확정: 개념]**

신기 초출 없음. Grid·SideSplit 없음. 외침은 하나 — 그 페이즈에서 입이 한 일을 되돌림.

| # | 입 + 일 |
|---|--------|
| 1 | Barrier + 침 — 패드가 미끄러움 |
| 2 | Drop + 화살 + 혀 — 혀가 장면 |
| 3 | Sequence + 닫힘 — 닫히면 안 보임 |
| 4 | ColorTile + 침 + 닫힘 |
| 5 | 혀가 바닥을 부숨 → 삼켜 T. 연출 |

페이즈 2는 혀가 본체. 드롭·화살이 동등한 숙제가 되면 다시 짠다. 랜덤 = Host `ChallengeStart(seed)`만. 클라이언트마다 `Random` 없음.

**P5 [확정 2026-09-05]:** 연출로 두지 않고 혀가 바닥을 부수는 걸 **피하는 판**. `TongueController.pattern = MixedSweep` — 창마다 가운데 3×3 / 왼 10 / 오른 10 중 하나를 시드로 뽑는다. **Hold 없음**: 가운데도 Rise로 부수고 Retract로 내려가는 공격이다(옛 후보의 "가운데는 RiseHold"는 폐기). 안 외치면 꺼진 채, 외치면 전부 복구. 4.1·4.2 본체에는 안 넣음. 머신 상세 = §5 "보스 머신".

**에디터 (보스 혀, 아직 안 됨):** M.Boss에 `TongueController`가 **없다.** P5에 혀 GO를 놓고 `pattern=MixedSweep`, `centerTiles` 9칸 + `leftTiles`/`rightTiles` 10칸씩, 클립 길이(rise/attack/retract), `seedSalt`를 입·침과 다르게 연결할 것.

빼는 것: 세이프존 입문, Barrier 초출, 깨물림 모이기, 바람만 페이즈.
