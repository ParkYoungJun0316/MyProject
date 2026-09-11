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

### H.5 다음 에이전트 — 여기부터 (2026-09-09)

**닫힌 트랙:** M 팀 응원 되돌림 (입 닫힘·침·혀). 코드+에디터+플레이 확인 **됨** (2026-09-04). 입/침/혀 머신 다시 열지 말 것. **M.Boss P4 입 닫힘** 코드+에디터 **됨** (2026-09-09) — `MouthBossJawSmash` 다시 열지 말 것. **연출 재검토(2026-09-09):** Breaking 구간이 이미 최대 암전이라 "이빨 프롭" 시각 연출은 안 보임을 확인 → `toothProps` 제거, 파괴음(`SFXId.Breakable_Destroy`)만으로 임팩트 전달. 부서진 자리는 빈 구멍.

**다음 트랙:** M 인게임 판을 잠금에 맞추기. T1 조임은 이 트랙 뒤.

1. **읽기:** 이 절 → §2 Barrier · §3 ColorTile · §7 P4(참고만). Grid 잠금은 §8 (이 트랙에서 코드로 열지 않음). 공유 [`CoopStageAudit.md`](CoopStageAudit.md) §H.5. T는 아직 구현하지 않음.
2. **할 일:** ColorTile 점수제 §3 — 코드 됨. 타이머·발동 스케줄·실패 패널티 **폐기**. **에디터:** M.Stage3 `uniqueQuota`=6, 흑·백 배열 1인 4/4 · 2인 6/6 · 3인 7/7 · 4인 8/8, `tilePrefabs`에 Black·White, `ColorTileRoundObjective.targetTime`=180. Barrier §2.1 슬롯 코드 됨(2026-09-05) — M.Stage1 이동은 에디터 남음. **Barrier incoming = §2.2 감독 — `ArrowIncomingDirector` + `ArrowTrap.FireOnce()` 코드 됨(2026-09-05). 에디터(사용자) 남음: §2.2 표 참고.** Sequence 손대지 않음. **Grid 룰 = §8 (2026-09-11 잠금, 코드 됨 — H.4 참고). 에디터 배선 남음.**
3. **하지 말 것:** ColorTile에 넉백·문 내림·광장화. 입/침/혀 재설계. P4 입 닫힘 머신 재설계. 팀 힐·120초. 새 RPC. Tutorial 팀 외침(마지막). T 조임·안개. T5. **Incoming 감독을 Barrier 색 배정·리빌·입 닫힘 창과 연동(§2.2 재확인).**

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
입 Recover = **Open**. 침 Recover = 수면 페이드아웃. 혀 4.1 Recover = **Retract**(Hold 포즈에서 시작, Rise 역재생 아님). 혀 4.2 Recover = 꺼진 1×1 복구 + Idle (**Retract 없음**. Hold 대기 없음 — Attack 후 다음 사이클 반대쪽). 자동 Open 없음. 닫힘 대가 = 암전만(HP 없음). 침 대가 = 미끄럼(HP 없음). 혀 대가 = 꺼진 1×1 낙사(HP 없음).  
**Attack 중 즉시 중단(2026-09-06, Saliva/식도만 적용):** `SalivaHazard.CoverRoutine`은 이제 Cover 진행 중 외침이 성공하면 즉시 그 알파값에서 멈추고 바로 Recover로 전환한다(예전엔 Cover가 끝까지 찬 뒤에야 Recover 시작). `EsophagusSqueeze`/`EsophagusFog`(T)도 동일 수정. **`Close`/`Rise`/`Attack_L`/`R`(입·혀, Animator 클립 기반)은 여전히 시작되면 끊지 않음** — 클립 중간 인터럽트는 별도 검토 필요, 미착수.  
**혀만 추가 — "응원 성공 후 칸이 깨진 채 남는" 버그 수정(2026-09-07):** 애니메이션 자체는 여전히 끝까지 재생되지만(위 항목과 별개), `TongueController.Revert()`가 `Attacking`/`Holding` 중 호출되면 `_recoverQueued`를 세우는 즉시 `RestoreAll()`도 같이 호출하고, `SweepBreak()`/`BreakRemaining()`도 `_recoverQueued`면 그 즉시 무시하도록 게이트를 걸었다. 이전엔 가운데 패턴(4.1 Rise / MixedSweep 가운데)에서 외침 성공 시점에 칸이 이미 깨진 상태였다면 `RecoverRoutine`이 `RetractIfCenter`(Retract 클립, 약 1.67초)를 다 기다린 뒤에야 복구해서 그 몇 초간 실제로 칸이 깨진 채 남는 버그가 있었음 — 이제 응원 성공 시점에 즉시 복구되어 그런 구간이 없다. 좌/우 스윕(4.2)은 원래도 클립 끝의 일괄 `BreakRemaining` → 같은 프레임 내 `RestoreAll`이라 실질적 위험은 없었지만 동일 게이트로 구조적으로도 보장.

**코드 (있음):**
- `CheerService` — 투표 RPC 유지. `ApplyTeamBuff` = Heal/120 없음 → `BuildRevertOrder`로 세대·재개 시각을 정해 `BroadcastTeamBuffActivatedClientRpc(generation, resumeAt)`. `ValidateTeamCheer`는 `_revert.IsAvailable` + 창 소비 latch. `RegisterRevert`(중복 등록 경고) / `NotifyHazardWindow`(창이 닫히면 표 리셋).
- `ITeamCheerRevert` — 씬당 하나. `IsAvailable` / `BuildRevertOrder` / `Revert(generation, resumeAt)`.
- `MouthController.teamCheerHazard` — true면 위 머신. false면 옛 Close→Hold초→Open.
- `SalivaHazard` — M2 revert. Warning→Cover→Hold→Recover. `SalivaVolume`이 발판 위일 때만 `Player` 얼음 미끄럼.
- `TongueController` — M4 revert. 4.1 RiseHold / 4.2 AttackSweep. `SweepBreak` 이벤트 안 씀(클립 끝 `BreakRemaining`).
- `MouthBossJawSmash` — M.Boss P4 전용. Close는 무조건, 응원은 Open 뒤 사후 복구만. 새 RPC 없음. Mouth/Tongue의 `_skipNextWindow`(창 건너뛰기) **쓰지 않음**.
- `TeamCheerWarningUI` — `OnHazardWindowChanged`. `TeamCheerCleared` — `OnTeamBuffActivated`. 팀 쿨 HUD는 **삭제됨**(스크립트·NV·세션 저장까지). 이 UI는 창이 열린 동안 같은 스프라이트를 띄움 — P4에서는 "막아라"가 아니라 "복구하라".

**되돌림 동기화 (2026-09-05 리뷰 반영) — 함정 3종 공통:**
- 명령은 **Host 권한**. 세대 번호가 낮거나 같으면 무시하므로 같은 창이 두 번 발동해도 되돌림은 1회.
- 명령을 받은 시점에 창이 아직 안 열린 머신은 그 창을 **열지 않고 건너뛴다**(`_skipNextWindow`). 혀 4.2는 건너뛸 때도 복구·공격 방향 소비를 같이 해 Host와 좌/우가 어긋나지 않게 한다. **예외 — `MouthBossJawSmash`:** 응원이 공격을 막는 게 아니라 사후 복구라서 `_skipNextWindow`를 **쓰지 않는다**. Revert는 창 안/밖 어디서 받아도 바닥 전체 복구만 하고, 6회차는 성공/실패 무관하게 전부 돈다. 건너뛰면 Host만 다음 회차를 진행하고 그 클라이언트 바닥이 멀쩡한 채로 남아 낙사가 갈린다.
- 첫 창은 `StageNetworkState.PhaseStartServerTime` 앵커(WindTrap/ArrowTrap과 같은 패턴). 앵커가 없는 씬은 예전대로 로컬 폴백. 앵커를 읽기 전에 **한 프레임 양보**한다 — `PhaseManager.EnterPhase`가 `objectsToEnable.SetActive(true)`를 `MarkAndSyncPhase`보다 먼저 하므로, Phase가 켜주는 함정(혀 4.1↔4.2)이 곧바로 읽으면 Host만 직전 Phase의 낡은 앵커를 잡는다(`SafeZoneWarnSign`과 같은 이유).
- 간격 추첨은 `Random.state`를 저장·복원한다. `InitState`가 전역 RNG를 갈아엎어 같은 씬의 다른 시스템이 이 시드 스트림을 물려받는 걸 막는다(결정성은 그대로).
- 간격 시드는 **축을 분리**한다(`ScheduleAxis` / `RevertAxis`). `_cycleCount`와 되돌림 세대가 둘 다 1,2,3…이라 축을 안 섞으면 같은 간격이 반복된다.
- 재개 시각 대기는 매 프레임 필드를 다시 읽는다 — 대기 중에 새 명령이 오면 그 예약을 따라간다.

**입 고유 (2026-09-05 리뷰 반영):**
- `MouthController.OnDisable`이 `screenFader.FadeIn(0f)`으로 암전을 걷는다. 페이드는 ScreenFader 자기 코루틴이라 `StopAllCoroutines`로 안 멈춘다 — 없으면 Hold 중 입이 꺼질 때 화면이 까만 채로 굳는다. `StopCycle()`에도 같은 복구가 있지만 **그쪽은 코드에서 호출되지 않는다(ContextMenu 전용)**.
- 연출 전용 입(`teamCheerHazard=false`, `AutoCycle`)도 같은 `ResolveFirstWindow` 앵커를 쓴다. 이 입들은 `screenFader`가 비어 있어 암전은 없지만 배경 연출 위상을 맞춘다.
- Hold는 **외침 전까지 무한 대기**가 설계다(자동 Open 없음). Closing·Holding 내내 `_available`이 true라 Hold 중 외침이 먹힌다.

**에디터 (됨):** `M.Stage1` / `M.Stage3` / `M.Boss`의 GO 이름 `MouthController`만 `teamCheerHazard=true`. `TransitionPhase*`·M2 입·M4·M5는 false. `UI.prefab`에 `TeamCheerWarning` + `Assets/Figma/Lobby/Warning.png`, Fadeout보다 위(마지막 형제). M.Boss P4(`Boss 270-360`)에는 `MouthController`를 **붙이지 않음** — `MouthBossJawSmash`가 이 페이즈의 입 애니메이터(MouthBG Close/Open)를 직접 구동. P2 `MouthController`는 그대로(닫힘 되돌림).

**침 구현 (2026-09-03) [확정]**

로직 됨. 수면 아트는 다른 에이전트. 수치(`salivaAccelTime` / `salivaDecelTime`)는 플레이로 깎음.

| 항목 | 내용 |
|------|------|
| 범위 | **M.Stage2 전체.** 2.1 SideSplit + 2.2 Drop. (옛 잠금 “2.1 침 없음” 폐기) |
| revert | 씬당 하나 = `SalivaHazard`. 입 `teamCheerHazard`는 M2에서 **끔**. 새 RPC 없음. 머신 = 입과 동일 (Warning→Cover→Hold→Recover) |
| 미끄럼 | PhysicMaterial **안 씀.** `Player.Move()` 얼음: 입력 중엔 가속만 더함(출발이 느리고 밀림). 손 떼면 감속만 약하게(관성으로 쭉). 방향 전환도 얼음(반대 스틱해도 예전 속도가 남음). 목표속도로 끌어당기면 물속 저항이 되므로 폐기 |
| 수치 | `Player.salivaAccelTime` 기본 1.2초(정지→풀속도). `Player.salivaDecelTime` 기본 3.5초(풀속도→정지). **Decel > Accel.** 플레이어 프리팹 인스펙터 |
| 씬 GO | 루트 `SalivaHazard`. `SalivaVolume_2_1`(Stage2.1, Ground 25×15 위). `SalivaVolume_2_2`(Stage2.2 자식). LEFT/RIGHT 기둥에는 안 깔음 |
| 비주얼 | M.Stage2는 `coverRoots` 2 / `coverRenderers` 2 연결됨 (`coverAlpha` 0.9). **드롭 완전 제거(2026-09-05):** 발판 위로 낙하하는 침방울 연출(`coverDropPrefab`/`SalivaCoverDrop`, 중앙 1회든 주변 반복이든)이 공격처럼 보인다는 피드백으로 폐기 — 코드·스크립트(`SalivaCoverDrop.cs`) 삭제됨. 이제 비주얼은 수면 알파 페이드만. `Assets/Prefab/입/Drop.prefab`은 참조하는 스크립트가 없어졌으니 **에디터에서 정리(삭제 또는 컴포넌트 제거) 남음.** |

**에디터 (침):** 위 표. 수면 연결은 나중에 빈 슬롯에.

### H.2 M 잠금 (다시 묻지 말 것)

| 항목 | 잠금 |
|------|------|
| M1 | `DirectionalBarrier`를 보스에서 앞으로. 패드→문 상승→incoming 함정 파괴. 뮤텍스 = 한 색만 업. **소리 초출.** 통과 퍼즐 아님. 슬롯 = §2. `Distribute` 1인=전원동색 / 2인=2+2 **쓰지 않음**. Incoming = §2.2 감독(계단 텀, Barrier와 무연동). 기존 스케줄 값은 유지 — 공존 방식 미정 |
| M2 | **한 씬, 두 구간.** 2.1 SideSplit + **침**. 2.2 Drop + **침**. 암전 안 씀. 2.1 위에 Drop 안 얹음. 라운드로 시간 안 벌음 |
| M3 ColorTile | **컷 취소.** 점수제 §3. 각자 칸 서기 폐기. **입 시계.** 3분(2–5) |
| M4 | **한 씬, 두 구간.** 4.1 SequenceRing 턴제 + **혀 초출.** 4.2 ArrowTrap + **혀 복습**. 링 위에 화살 없음. M6·M7 없음. 리듬·16칸 암기·검정만 늘리기 **폐기** |
| Sequence 인원별 난이도 (2026-09-11 예외) | 룰(판정·미리보기·시드 재생성)은 그대로 — **손대지 않음** 원칙 유지. `targetStepCount`·`timeLimit`만 ColorTile의 `blackQuotaByPlayerCount`와 동일 패턴(`targetStepCountByPlayerCount`/`timeLimitByPlayerCount`, 인덱스 0=1인…3=4인)으로 배열화. 1인은 색 풀이 1개뿐이라 단조로워지므로 step 수·시간을 함께 줄여 짧게 넘김. 흑·백 스폰 비율(`commonSpawnChance`/`dangerSpawnChance`)은 인원별 분기 없음 — 체감 효과 낮다고 판단해 보류 |
| 챌린지 인원수·색 SSOT | **`GameSession`만 본다.** 챌린지가 `PlayerSpawnCoordinator`를 직접 조회하지 않음 — GameSession이 씬 로드 시점에 PSC(NetworkList)를 흡수하고(`OnSceneLoaded` → `SetActiveColors`), 늦게 오면 `OnPlayersReady`로 재적용한다. `ActivePlayerCount`(=`_activePlayers.Count`)는 Player 스폰에 의존해 씬 로드 직후 0일 수 있으나 `GetActiveColors()`는 그와 무관하게 채워지므로(`Apply` ①), **인원수는 `GetActiveColors().Count`로 파생**시킨다. 색과 인원수를 각각 다른 체인에서 풀면 폴백이 어긋난다(2026-09-11 실제 버그: 색 4색 폴백 + 인원수 1인 폴백). 폴백(GameSession 없음)은 4색 + **1회 경고** — `GridChallenge.PickRandomTiles`와 동일 규약 |
| M5 | Grid **혼합판 §8.** 한 보드·한 라운드 줄. 고유+흑+백을 같이 깐다. 내 고유색 칸이 나왔으면 그 칸만. 2인 장면 = 후반 1칸 모이기. **WindTrap 유지**, 강도만 사용자. 바람에서 협동 찾지 않음. **입 열기 없음** |
| ColorTile 점수 | 2초(기본) 또는 3초 점유 → 뽕 → 그 색 +1 → **다른 칸에 재스폰**. 고유는 주인만, 흑백은 아무나. 통과 = 고유+흑+백 의무. 흑백 의무 0 금지. 통로 좁게. **압력 = 할당량 + 입 창.** 함정으로 협동 안 만듦. 넉백·문 내림·광장화 안 씀 |
| 소리 초출 | **M1.** 외침으로 닫힘 막기. 닫힘의 맛 = **암흑 시야 정도는 가져감.** 데미지·둘 다는 나중에. M3·보스 복습 |
| 침 초출 | **M2 (2.1부터).** 2.2·보스 복습. PhysicMaterial 아님 — `Player.Move()` 얼음 가속/코스트 (`salivaAccelTime` / `salivaDecelTime`). §6 |
| 혀 초출 | **M4.1.** 보스·M4.2 복습. M6·M7 없음. 4.1 가운데 1칸. 클립 끝에 칸 끔 (`SweepBreak` 안 씀). 꺼진 칸 낙사→방 리셋. §5 |
| 입 창 리듬 | **개념만.** M1·M3·보스. M2·M5 없음. 초·횟수·데미지는 나중에 |
| M.Boss | §7 (2026-09-08 재확정, 5→**4페이즈**). 1 Barrier+침, 2 SafeZoneWarnSign+닫힘, 3 Drop+화살+혀, **4 입 닫힘(무조건)+타일 파괴(파괴음), 6회 누적 4N개, Open 후 응원 창 → 마지막 1칸 남고 삼켜 T**(2026-09-08, 혀 MixedSweep 폐기). Grid·Sequence·ColorTile·WindTrap·SideSplit 안 씀 — 페이즈당 되돌림 대상 하나(침/닫힘/혀)만. 시드=Host ChallengeStart |

### H.3 M에서 버린 제안

- ColorTile 컷 (취소됨)
- M2를 암전·보이스 차단·이심전심으로 길게 (보스 의식 후보만. M2 본체 아님)
- SequenceRing 메트로놈/리듬, 16칸 한꺼번에 외우기, 검정만 늘리기
- Grid / Wind 컷, Wind로 협동
- Grid Color 7 + BW 7 구간 분리. 고유색은 위치만, 흑백은 토글만
- 고유색 칸이 나왔는데 그 색이 흑/백 칸으로 통과
- Barrier를 통과·알코브 퍼즐, 1인 4문 동일색
- 흑백 할당량 0인 ColorTile
- M6·M7, 링 위에 ArrowTrap, 2.1 위에 Drop
- 혀 맞음을 약한 밀침으로, 침을 PhysicMaterial로
- 팀 응원 +2힐·120초를 M 시계로, 창 중 재외침으로 연장, 계속 고함
- ColorTile에 ContactKnockback·흑백 문 내림·고유색 벽·광장화로 난이도 (압력은 할당량+입 창)
- Incoming 레인 선택을 Barrier 색 슬롯·라운드 상태와 연동("겹치는 incoming으로 2인 장면" 폐기) — 레인은 방향일 뿐, §2.2
- Incoming 감독을 리빌 구간·입 닫힘 창(소리 초출)과 연동해 정지시키기 — 불필요, §2.2에서 뺌
- Incoming에 Tracker 부착, 화살 속도 단계(`speedPhases`)로 난이도 이중화 — 텀 하나만
- M.Boss를 5페이즈로: ColorTile(구 P4)에 침+닫힘을 같이 얹기 — `ITeamCheerRevert`는 씬(페이즈)당 하나뿐이라 동시 등록 충돌. §7 참고
- M.Boss 1페이즈를 Barrier+WindTrap으로 (2026-09-08 검토 후 폐기) — WindTrap은 `ITeamCheerRevert` 미구현이라 그 페이즈에 응원 창이 통째로 사라짐. 침으로 원복
- M.Boss에 Sequence+닫힘, Grid+닫힘 둘 다 (2026-09-08 검토 후 폐기) — Sequence는 억지로 끼워 넣은 느낌, Grid+닫힘은 대안으로 검토했으나 SafeZoneWarnSign+닫힘이 더 낫다고 판단. Grid+**침**은 별도로 폐기 — 침의 얼음 관성(회피용으로 튠)이 Grid의 정밀 스테핑과 만나면 실력이 아니라 운으로 미끄러져 불공정

### H.4 코드

| 대상 | 상태 |
|------|------|
| CheerService 팀 | **됨.** Heal·120초 폐기. Warning~Revert만 유효. 새 RPC 없음. 입 닫힘·침·혀 연결 |
| MouthController | **됨.** hazard 씬만 Close→Hold(외침까지)→Open. 자동 재오픈 없음 |
| 침 | **됨.** `SalivaHazard` / `SalivaVolume` / `Player` 얼음. 수면 비주얼은 슬롯만 비움 |
| 혀 | **됨(4.1·4.2만).** `TongueController` + M.Stage4 에디터. 4.1=1칸, `SweepBreak` 안 씀. 플레이 확인 (2026-09-04). 보스용 `MixedSweep` 패턴은 **코드는 있지만 보스에서 폐기(2026-09-08)** — M.Boss는 P3에서 `AttackSweep` 복습만 쓰고, P4는 `MouthBossJawSmash`(입 닫힘+파괴음) |
| M.Boss P4 | **됨(2026-09-09, 연출 재검토 반영).** `MouthBossJawSmash` (`Assets/Scripts/Cheer/MouthBossJawSmash.cs`). 새 RPC·NV 없음. 회차·구간 경계 = 절대 `PhaseStartServerTime`. Revert = 사후 복구만(회차 건너뛰기 없음). `SceneFlowManager.FreezeAllHazardsNow()`가 `StopCycle()` 순회. **연출:** Breaking이 이미 최대 암전이라 시각 연출(이빨 프롭) 대신 파괴음(`SFXId.Breakable_Destroy`, `breakSfxMinDistance`/`Max`/`RolloffMode`)만 재생 — `toothProps` 필드 제거. **에디터 됨:** M.Boss `Boss 270-360`/`StageManager_Boss5`에 GO `MouthBossJawSmash`. `floorTiles` 25(Ground, x→z 정렬) + `warnMarkers` 25(`SpikeLaneWarnMarker`, 혀/SpikeTrap과 동일). `mouthAnimator`=MouthBG, `screenFader`=Fadeout/Image, Close/Open 클립 길이 2.966667. `OnChallengeComplete`→`BossFightObjective.NotifyPhaseCleared`. `Bossdown` `OnAllReady` = `ForceBreakAllTilesForEnding` → `SceneFlowRelay.LoadNextScene`. P4 `onPhaseEnter`에 `StageManager_Boss5.StartStage`. `BossFightObjective.totalPhases`=4(이미). 낙사 = Player `enableFallDeath`(M4와 동일, 방 리셋). **에디터 남음:** 씬에 남은 예전 `Boss_Final` 이빨 프롭 복제 25개(정리 필요) |
| ColorTile | **점수제만.** unique 6 + 인원별 흑/백(4/4, 6/6, 7/7, 8/8). 목표 시간 = `targetTime`(기본 180, 권장 120–300). 타일 `Black`/`White`. M.Stage3 인스펙터 남음 |
| Barrier | §2.1 색 슬롯 표 **코드 됨(2026-09-05)** — `DirectionalBarrierRound.BuildBarrierSlots`가 균등 분배(`GameSessionColorDistribution.Distribute`) 대신 확정 표(1인=고유2+백+흑 / 2인=A+B+백+흑 / 3인=고유3+백1 / 4인=고유4)로 배정. 타일도 슬롯 중복 없이 색당 1개만 스폰(1인 고유 패드 1개 → 고유 문 2개). 시작 흐름 **Reveal/CloseAndSpawnTiles 2단계로 분리(2026-09-05)** — `Activate()`(단일 호출) 폐기. `Reveal()`은 배치+Open만 하고 자동으로 안 닫힘(다이얼로그 프리뷰용), `CloseAndSpawnTiles()`가 Close+타일 스폰(진짜 라운드 시작). Reveal 없이 CloseAndSpawnTiles만 호출해도 그 자리에서 스폰부터 자동 수행(무프리뷰). Incoming 감독(§2.2) — `ArrowIncomingDirector`·`ArrowTrap.FireOnce()` **코드 됨(2026-09-05)**. **에디터(사용자, 남음):** M.Boss→M.Stage1 이동, `barrierPrefabs`/`tilePrefabs`에 White·Black 문/타일 프리팹 추가, 화살 `Breakable` 부착, M.Stage1 Phase0 onPhaseEnter→`Reveal()` + `StageStartGate.OnCountdownComplete`→`CloseAndSpawnTiles()` 연결 |
| Sequence | 룰 유지. **코드 됨(2026-09-11 예외)** — `targetStepCount`/`timeLimit`을 `targetStepCountByPlayerCount`/`timeLimitByPlayerCount`(인덱스 0=1인…3=4인, ColorTile `blackQuotaByPlayerCount`와 동일 규약)로 배열화. 레거시 스칼라 값은 `MigrateLegacyDifficulty()`가 1회 4칸에 복제 후 비움. 판정·미리보기·시드 재생성 로직은 무변경. 인원수는 `GetUniqueColorPool().Length`로만 파생(위 "챌린지 인원수·색 SSOT" 행) — 별도 `PartySize()` 체인 없음. 같은 리뷰에서 고친 것: ① 제한 시간을 런당 1회 래칭(`_runTimeLimit`, 매 프레임 재해석 시 Host만 카운트다운하고 클라 타이머가 멈추는 desync) + `SyncChallengeTime` 수신 시 자가교정, ② 색 풀을 PSC `HashSet` 열거 순서로 쓰던 것을 GameSession(ColorIndex 정렬)로 교체 — 순서가 `rng.Next(pool.Length)`에 먹히므로 같은 시드에서도 Host/Client가 다른 색을 뽑을 수 있었음. **에디터 남음:** M.Stage4 `SequenceRingMinigame` 인스펙터에서 인원별 배열 값 확인/튜닝(마이그레이션 직후엔 4칸 모두 기존 스칼라 값과 동일) |
| Grid | **혼합판 §8. 코드 됨(2026-09-11).** `GridColorChallenge`+`GridBWTileChallenge` → `GridChallenge` 하나로 통합, `GridColorTile`+`GridBWTile` → `GridTile` 하나로 통합(둘 다 삭제됨, `ChallengeOwnerType.GridColor`/`GridBW`는 번호 재사용 금지로 자리만 유지, 신규 `ChallengeOwnerType.Grid` 사용). 풀 = 활성 고유색(GameSession 기준)+Black+White. 라운드 생성은 `GridSafePhase`(afterRound/tileCount/minBwCount) 커브 — 흑/백 최소 개수를 먼저 강제 추첨 후 나머지를 전체 풀에서 채우는 결정적 알고리즘(거부 샘플링 없음). 판정은 플레이어별 분기: 내 고유색이 이번 라운드 풀에 있으면 고유색 모드+내 색 칸, 없으면 흑백 모드+내 `isBlack` 일치 칸(공유 가능). **`minBwCount`는 "흑+백 합산 최소 개수"로 해석**(§8 표 마지막 행 `tileCount=1,minBwCount=1`이 "그 1칸이 흑이든 백이든 통과"가 되려면 이 해석만 성립 — "흑 각각 최소·백 각각 최소"였다면 타일 1개로는 불가능하므로 모순). 네트워크 골격(owner 가드·Host 레인 판정·시드 결정성·사망 처리)은 기존 두 챌린지와 동일 — §11B 챌린지 축 재사용, 새 RPC/NV 없음. **에디터 남음:** M.Stage5 씬에 `GridTile` 25개 배치(머티리얼 7종: Blue/Purple/Green/Yellow/Black/White/Default), `GridChallenge` GameObject에 tiles 연결, `safeTilePhases`(§8 표 수치) + `totalRounds`/`roundDuration`/`individualDamageOnFail` 인스펙터 설정, `GridRoundObjective.gridChallenge` 연결(구 `colorChallenge`/`bwChallenge` 필드는 제거됨) |

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
| Grid | 혼합판 §8 | **A** | 후반 1칸 모이기. 고유 없으면 흑백 | 고유 칸 있으면 그 칸만. Color/BW 7+7 폐기 | Wind 유지, 강도는 사용자 |
| M.Stage1 | Barrier + 소리 초출 | A. §2 · §4 | 부수기 + 뮤텍스. 닫힘 막기 | `Distribute` 2+2 / 1인 4면 동일색 안 씀 | 함정은 부술 대상 |
| M.Stage2 | 2.1 SideSplit+침. 2.2 Drop+침 | **A** §6 | 갈라서기. 침이 남아 미끄러짐 | 암전 안 씀. 라운드로 시간 안 벌음 | 2.1 위에 Drop 없음. 2.2 Drop은 침의 압력 |
| M.Stage3 | ColorTile + Drop + AdvancingWall | **B** §3 · §4 | 흑백 할당량 + 입 시계 | 점수제 | 실패 이빨은 남을 수 있음 |
| M.Stage4 | 4.1 링+혀. 4.2 화살+혀 | **A** §5 | 색 차례 + 혀. 혀가 바닥을 줄임 | 링 위에 화살 없음 | 4.1 Drop은 C. 4.2 화살은 혀의 압력 |
| M.Stage5 | Grid + Wind | **A** §8 | 후반 1칸 모이기 | **입 열기 없음.** Color/BW 구간 분리 폐기 | Wind **유지** |
| M.Boss | **4페이즈** §7 (2026-09-08) | 초출 금지 | 1–3 복습, **4 입 닫힘+타일 파괴 6회 누적 → 삼켜 T** | 시드=Host ChallengeStart | Grid·Sequence·ColorTile·WindTrap·SideSplit 없음. 바람만 페이즈 없음. 혀 MixedSweep도 보스에서 뺌 |

**M.Stage2.** 한 씬 두 구간. 이심전심 암전은 보스 후보만.

**M.Stage4.** M6·M7 없음. 흰은 아무나, 검은 누르면 안 됨(무입력 시 자동 통과).

**M.Stage5.** 입 열기 없음 — 바람과 안 맞음. Grid = 혼합판 §8.

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
| 보스 | M.Boss | **P3만.** 복습만, 4.2쪽(AttackSweep). 옛 P4/P5 자리의 `MixedSweep`은 2026-09-08에 보스에서 완전히 빠짐 — §7 참고 |

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

**보스 머신 (MixedSweep) [확정 2026-09-05, 2026-09-08 보스 사용 폐기]:** 아래 스펙 자체는 참고용으로 남기지만 **보스에는 더 이상 안 쓴다** — 마지막 페이즈가 혀 대신 입 닫힘 기반으로 전면 교체됐다(§7 P4). 4.1·4.2 본체에도 넣지 않는다는 원칙은 그대로.
- (구) 영역은 창마다 **가운데 3×3(9칸) / 왼 10칸 / 오른 10칸** 중 하나. 4.1처럼 Hold로 가림막을 세우지 않는다 — 가운데도 부수고 내려가는 공격.
- (구) 가운데는 Rise 클립으로 부수고 Retract로 내려간다(외침 여부와 무관). L/R 클립은 스스로 내려가므로 Retract 없음.
- (구) 영역 추첨은 `NetworkSessionData.Seed` + 창 번호(`_attackCount`). 로컬 `Random` 없음.
- (구) 창 번호는 스킵·차단·완주 세 경로 모두에서 1회씩 소비(`AdvanceAttack`).

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
**비주얼:** M.Stage2는 coverRoots·coverRenderers 연결됨(알파 페이드만). 낙하 드롭 연출은 완전 폐기(§H.5 참고). 수면 아트는 나중에.

**미끄럼 권한:** 전 머신이 로컬로 `AddSalivaOverlap`을 걸지만 `Player.Move()`가 `isOwnerControlled` 게이트라 실제 효과는 오너 머신에서만 — Owner + CNT와 일치. 침 전용 RPC·NV 없음.

**배선 누락:** `volumes` 비었거나 `SalivaVolume.hazard` null이면 Awake에서 경고. 침이 깔려도 안 미끄러지면 콘솔부터 볼 것.

---

## 7. M.Boss 페이즈 **[확정: 개념, 2026-09-08 재확정 — 5→4페이즈]**

신기 초출 없음. Grid·SideSplit·Sequence·ColorTile·WindTrap **안 씀**(2026-09-08). 외침은 하나 — 그 페이즈에서 입이 한 일을 되돌림. **페이즈당 되돌림 대상은 하나** (`ITeamCheerRevert` 씬당 1개 계약 — 한 페이즈에 침·닫힘·혀를 동시에 등록하지 않음).

| # | 입 + 일 | 되돌림 |
|---|--------|--------|
| 1 | Barrier + 침 — 패드가 미끄러움 | 침 |
| 2 | SafeZoneWarnSign + 닫힘 — 크로스파이어, 암전 중엔 안전지대도 안 보임 | 닫힘 |
| 3 | Drop + 화살 + 혀 — 혀가 장면 | 혀 |
| 4 | 입 닫힘(무조건) + 타일 파괴(파괴음), 6회 누적 → 삼켜 T | 닫힘(보스 전용 새 변형) |

페이즈 3은 혀가 본체. 드롭·화살이 동등한 숙제가 되면 다시 짠다. 랜덤 = Host `ChallengeStart(seed)`만. 클라이언트마다 `Random` 없음.

**P2 [확정 2026-09-08]:** `SafeZoneWarnSign`은 순수 연출·스케줄이라 `ITeamCheerRevert`가 아니다 — 되돌림 대상은 같은 페이즈의 `MouthController`(닫힘)뿐, SafeZoneWarnSign 자체는 등록하지 않는다. 암전(Closing/Holding) 중엔 안전지대 마커도 안 보여서 "빛이 있어야 화살을 피한다"는 압박이 자연스럽게 생긴다. 세이프존 입문은 이전에 "빼기"로 잠갔던 걸 여기서 되살린 것 — `SafeZoneWarnSign` 자체(크로스파이어 안전지대 표시)는 보스 전용이라 새 메카닉 초출이 아니고, 짝인 화살(`ArrowTrap`)은 이미 M1·M4.2에서 가르침.

**P4 [확정 2026-09-08 — 입 닫힘 기반으로 전면 교체, 혀 `MixedSweep`은 보스에서 폐기. 코드+에디터 2026-09-09]:** 혀가 아니라 입 닫힘을 마지막 공격 수단으로 쓴다. **인과관계가 지금까지의 닫힘(M1·M3·P2)과 반대다** — 응원이 Close를 막는 게 아니라, Close→Open이 끝난 뒤에만 응원 창이 열려 사후 복구만 한다. 기존 `MouthController.teamCheerHazard`로는 못 표현 → **`MouthBossJawSmash`** (P4 전용 클래스, MouthBG Close/Open을 직접 구동). 같은 아레나에 `MouthController`를 붙이지 않는다(씬당 `ITeamCheerRevert` 하나).

머신 (6회 반복):
1. **경고** — 바닥 25칸 중 이번 회차에 부술 대상에 마커 표시(시드 기반, `TongueController.MixSeed`/`PickSeededRegion`과 같은 패턴을 K개 픽으로 확장). 이 단계엔 응원 없음, 예고만.
2. **Close(무조건 발동, 응원으로 못 막음)** — 암전.
3. 암전 중 경고 뜬 칸들이 파괴음과 함께 깨진다. **연출 재검토(2026-09-09):** 이 구간은 화면이 이미 최대 암전(`ScreenFader`)이라 "이빨 프롭이 내려와 부순다" 같은 시각 연출을 넣어도 플레이어가 못 본다 — 그래서 시각 연출 없이 **사운드로만** 임팩트 전달(`SFXId.Breakable_Destroy`, `Breakable`/`TongueController`와 동일 3D 재생 패턴 재사용, 새 SFXId 없음). 부서진 자리는 그냥 빈 구멍(`floorTiles[i].SetActive(false)`) — 원래 있던 `toothProps`(`Boss_Final` 비주얼 복제) 필드는 제거함.
4. **Open** — 암전 걷힘. **이 시점부터 응원 창이 열린다.** 성공하면 바닥 전체 원상복구. 실패하면 깨진 채로 다음 회차로.
5. 다음 회차 경고로 이어짐. 총 **6회**.

**크기 누적(회차 번호 기준, 응원 성공 여부와 무관):** 회차 N의 목표 파괴 수 = **4×N**(1=4, 2=8, 3=12, 4=16, 5=20, 6=24). 직전 회차가 복구됐으면 이번에 4N개를 새로 뽑고, 복구 안 됐으면 이미 깨진 4(N-1)개에 새 4개만 추가로 뽑아 4N을 채운다(응원은 항상 전체 복구라 회차 사이 깨진 수는 0 또는 4(N-1)만 나옴 — 산수가 갈라지지 않음). **총 타일 수 = 25**(4×6+1) — 수치를 나중에 바꾸더라도 "총 타일 = 4×회차수 + 1" 관계는 유지해야 "마지막 1칸" 결말이 보장됨. 연속으로 여러 회차를 놓쳐서 남은 칸이 목표보다 적으면 남은 칸 전부를 깨는 걸로 캡. `ValidateWiring()`이 이 불변식과 배열 길이 불일치를 Awake에서 경고.

**클리어:** 6회차가 끝나면(성공/실패 무관, 팀이 살아있으면) 클리어. 시간 기반 `PhaseSurviveChallenge`가 아니라 **회차 카운트 기반** — `MouthBossJawSmash`가 6회 완료 시 `BossFightObjective.NotifyPhaseCleared()`를 직접 호출(Host 레인 가드, 기존 챌린지들과 동일 연결 방식).

**낙사:** 꺼진 칸 = M4 혀와 동일. `Player.enableFallDeath` / `fallDeathY`(Owner 신고 → Host 적용 → 사망 리로드 = 방 리셋). P4 컴포넌트에 사망 코드 없음.

**리듬 (2026-09-08 리뷰 확정):** 회차 길이는 고정. 응원을 창 초반에 성공해도 다음 회차가 앞당겨지지 않는다 — 복구는 즉시, 남은 창 시간은 숨 돌리기. 로컬 `WaitForSeconds` 누적은 Host/Client 창 종료·타일 추첨을 갈라놓으므로 **쓰지 않음**. 회차 N 시작 = `PhaseStartServerTime + interCycleGap + (N-1)×CycleDuration`(절대 ServerTime).

**되돌림:** 새 RPC 없음. `ITeamCheerRevert` 기존 채널. Mouth/Tongue의 `_skipNextWindow` **금지**(위 H.5 예외). `StartCycle()`은 `_brokenIndices`만 비우지 않고 `RestoreAllTiles()`로 집합과 `activeSelf`를 같이 맞춘다.

**정지/엔딩:** `SceneFlowManager.FreezeAllHazardsNow()`가 `MouthBossJawSmash.StopCycle()`을 순회한다. `ForceBreakAllTilesForEnding()`은 루프·창을 먼저 끊고 `_endingBroken`을 세워, 이후 `StopCycle`/`OnDisable`이 바닥을 되살리지 않게 한다. Bossdown `OnAllReady` 순서: `ForceBreakAllTilesForEnding` → `SceneFlowRelay.LoadNextScene`.

**엔딩:** 클리어 → 대화(Bossdown) → 마지막 남은 1칸까지 파괴음과 함께 부서지는 연출 → T로 전환. 원래 잠긴 "혀가 바닥을 부숨 → 삼켜 T"는 폐기 — 주체가 혀에서 입(파괴음 기반)으로 바뀜.

**초출 판단:** 새 메카닉 아님. "경고→파괴→응원 복구"라는 핵심 상호작용은 M4가 이미 가르쳤다. 다만 "닫힘→바닥 파괴"라는 조합과 "응원이 사후 복구만 한다"는 인과관계는 보스에서 처음 나오는 변형 — 플레이어가 배울 새 스킬은 아니라 초출 금지 위반은 아니라고 판단.

**P2와 연출 중복 (미해결, 나중에):** P2도 닫힘(암전)을 쓰므로 보스 4페이즈 중 2개가 "화면이 까매짐" 연출을 공유한다. 지금은 괜찮다고 보고 넘어감 — 나중에 재검토 여지 있음.

**에디터 (됨 2026-09-09):** `Boss 270-360` / `StageManager_Boss5` 아래 `MouthBossJawSmash`. Ground 25칸 → `floorTiles`(x→z). 경고 = `SpikeLaneWarnMarker` 25(혀/SpikeTrap과 동일, White URP Lit, 노랑→빨강 `PlayWarning`). MouthBG + Fadeout/Image. `BossFightObjective.totalPhases`=4. P4 `onPhaseEnter` → `StartStage`. **에디터 남음(2026-09-09 연출 재검토로 추가):** `toothProps` 필드가 삭제되어 씬에 남아있는 `Boss_Final` 비주얼 복제 25개(예전 이빨 프롭)는 더 이상 코드가 참조하지 않음 — 정리(삭제 또는 비활성 유지)는 사용자가 에디터에서. `breakSfxMinDistance`/`Max`/`RolloffMode`는 기본값(5/50/Logarithmic)이라 별도 배선 불필요, `SFXManager`가 씬에 있으면 그대로 동작. **에디터(보스 세이프존, 아직 안 됨):** P2에 `SafeZoneWarnSign` GO 배치, `cycles[]`에 해당 페이즈 `ArrowTrap` 발사 시각과 안전 타일 연결. Barrier(P1)는 M.Boss→M.Stage1 이동(§2.2 에디터 항목)과 별개로 보스용 인스턴스를 따로 유지.

빼는 것 (2026-09-08 재확정): ColorTile, Sequence, Grid, SideSplit, WindTrap, 혀 `MixedSweep`(P4 대체로 폐기, P3 `AttackSweep`만 남음) — 보스 페이즈 구성·복습 파트너 어느 쪽으로도 안 씀. 깨물림 모이기, 바람만 페이즈는 그대로 빠짐.

---

## 8. Grid 혼합판 **[확정: 개념, 2026-09-11]**

M.Stage5. 코드 아직. 새 미니게임 아님 — Color 7 + BW 7을 **한 보드·한 라운드 줄**로 합친다. WindTrap 유지. **입 열기 없음.** 네트워크는 지금 Grid와 같은 챌린지 축(시드 Generate → Host 판정). 새 RPC 없음.

**한 줄:** 매 라운드 고유·흑·백을 섞어 깐다. **내 고유색 칸이 나왔으면 그 칸만** 성공. 없으면 흑백.

### 보드 · 진행

챌린지 하나, 5×5 하나. Stage5.1 Color / Stage5.2 BW 페이즈 폐기. `totalRounds`는 인스펙터 (커브가 11라운드부터 1칸이면 12 이상이 자연스러움. 값은 에디터).

실패 = 지금처럼 **개인 데미지**. Objective는 라운드 실패로 Fail하지 않음. HP 0이면 기존 방 리셋.

### 색 풀 (Generate 후보)

활성 고유색 + 흑 + 백. 없는 플레이어 고유색은 안 깔음 (가짜 칸 없음).

- 4인: 6색 (고유4 + 흑 + 백)
- 2인: 4색 (고유2 + 흑 + 백)

### 깔기

라운드마다 안전 칸 = **색당 1칸**, 위치는 시드 랜덤. 나머지 Default.

단계 표는 인스펙터 배열 (`afterRound`는 0부터, 옛 `SafeCountPhase`와 같음):

| 필드 | 의미 |
|------|------|
| `afterRound` | 이 라운드 인덱스부터 적용 |
| `tileCount` | 이번 단계 안전 칸 수 (= 나오는 색 수) |
| `minBwCount` | 그중 흑·백 **최소** 개수 (흑과 백은 따로 셈) |

풀에서 `tileCount`개를 겹치지 않게 뽑되, 흑/백이 `minBwCount` 미만이면 안 됨. `tileCount`가 풀보다 크면 풀 크기로 클램프.

**4인 기본 커브** (라운드 번호 1-based. 인스펙터 `afterRound`는 0-based):

| 라운드 | afterRound | tileCount | minBwCount |
|--------|------------|-----------|------------|
| 1–6 | 0 | 4 | 0 |
| 7–8 | 6 | 3 | 1 |
| 9–10 | 8 | 2 | 1 |
| 11+ | 10 | 1 | 1 |

1·2인도 **같은 표·같은 알고리즘**. 풀이 작아서 초반 4칸이면 고유가 항상 나올 수 있다 — 버그 아님. 숫자 조이는 건 인스펙터.

### 정산 (Judge) — 난이도

정산 시 생존자 각자:

1. **내 고유색이 이번 라운드 안전 칸에 있다** → 고유색 모드 + **자기 색 칸**. 흑/백 칸에 서면 **데미지**. 다른 사람 고유 칸도 데미지.
2. **내 고유색이 없다** → 고유색 끄고 흑/백 모드 + 나온 흑 또는 백 칸과 `isBlack` 일치. 흑백 칸은 **공유 가능**.
3. Default·칸 밖·모드 틀림 → 데미지.

예: 흑·백·노랑·파랑이 나옴. 파랑은 파란 칸. 초록·보라는 흑/백. 파랑이 흰칸에 서면 데미지.

**겹침 판정 = 관대 [확정 2026-09-11]:** 칸 경계에 걸쳐 서면 캡슐 콜라이더가 트리거 두 개에 동시에 닿아 두 칸 모두에 점유 등록된다. 이때 **밟은 칸 중 내 정답 칸이 하나라도 있으면 통과**로 본다(대표 칸 하나를 골라 검사하면 배열 순서에 따라 내 칸을 밟고도 옆 칸 기준으로 실패하는 억울한 판정이 남 — `GridChallenge.PlayerPassed` 주석). 잘못된 칸만 밟고 있으면 정답 칸이 없으니 그대로 실패라 "내 색이 나왔는데 흑백에 서면 데미지" 규칙은 그대로. 엄격 판정(잘못된 칸에 닿기만 해도 실패)은 폐기 — 5×5 칸에 캡슐 콜라이더라 경계 걸침이 잦고 억울함이 누적됨.

**모드 불일치 = 칸과 무관하게 실패 [확정 2026-09-11]:** 내 고유색이 나왔는데 흑백 모드이거나, 안 나왔는데 고유색 모드면 어느 칸에 서 있든 실패. 옛 `GridBWTileChallenge`는 `isUniqueColor`를 아예 안 봤지만 혼합판은 "고유색 끄고 흑백 모드"(§8 정산 2)가 규칙의 일부라 검사한다. `isBlack`/`isUniqueColor`는 `NetworkPlayerSetup`의 NV로 Owner→Host 복제되므로 Host 단독 판정이 성립.

후반 1칸(흑 또는 백) = 전원 그 칸에 모여 같은 흑/백 모드. 옛 BW 후반 2인 장면.

### 안 함

- 고유색 구간 / 흑백 구간을 다시 나누기
- 고유 칸이 있는데 흑백으로 통과
- 흑백 칸을 “아무 모드나 가능”
- 비활성 고유색 decoy
- 겹침 엄격 판정(잘못된 칸에 닿기만 해도 실패) — 관대 판정으로 확정(2026-09-11)
- 라운드 수로 시간만 벌기 (칸 수·흑백 최소가 난이도)
- Grid를 새 미니게임으로 교체. 이 룰로 한 챌린지로 합치는 것만
- Wind로 협동. 입 열기

