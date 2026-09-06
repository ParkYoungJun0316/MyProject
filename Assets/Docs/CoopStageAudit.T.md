# Coop Stage Audit — T (식도)

식도 스테이지·T.Boss 감사 SSOT.  
공유 규칙(인원·2인 테스트·버킷·금지·세션 길이): [`CoopStageAudit.md`](CoopStageAudit.md).  
입: [`CoopStageAudit.M.md`](CoopStageAudit.M.md). ColorTile 점수제는 M §3 — T.Boss도 그 룰.

관련: [`CheerSystemDesign.md`](CheerSystemDesign.md) (RPC — 팀 **효과**는 여기 §3·§5).

**확정:** 2026-09-03. **다음 M 트랙:** 인게임 판 맞추기 (Barrier M1 · ColorTile) — [`CoopStageAudit.M.md`](CoopStageAudit.M.md) §H.5. 입·침·혀 됨. T1–T4 설계는 잠금, 구현은 그 M 트랙 다음. T5·T.Boss 설계는 보류.

**조임·안개 개념 확정: 2026-09-06.** 조임은 좌우/위아래 축 폐기 → 식도 원통(BG) 반경 축소로 통일. 안개는 바닥 한정 폐기 → 거리 기반 Render Fog로 전환. **모델은 같은 날 두 번 바뀜:** 처음엔 "창 없음, 씬 시작부터 지속 진행" 지속형으로 갔다가, 팀 응원을 계속 유도해야 한다는 이유로 **SalivaHazard와 동일한 랜덤 주기 창 모델(Idle→Warning→Attack→Hold→Recover)로 최종 확정**(코드까지 반영됨). 상세 §3·§5.

**범례:** [확정] = 승인 완료. 수치는 해당 스테이지 때.

---

## H. 핸드오프 **[확정]**

설계 감사만. `.cs` / Docs만 에이전트. 씬·MCP는 사용자 “구현해” / “MCP로 수정해줘” 전까지 금지.

M·T1–T4를 다시 묻지 말 것. **T5·T.Boss 응원·페이즈는 보류** — 조임·안개를 플레이로 느끼기 전에 종이로 잠그지 않음. Runner/Chaser 재설계·T5 안개 재사용·세 번째 동사 지금 금지.

읽을 순서: **§H.6 → §H.2 T1–T4.** T5 행은 참조만.

### H.2 T 잠금 (다시 묻지 말 것)

| 항목 | 잠금 |
|------|------|
| T1 | 고유색 패드·리맵 **유지**. Door_3. Door_1은 사용자가 버리거나 단순화. 볼더 유지. **조임 초출** = 식도 원통 반경 축소(전방향, 랜덤 주기 공격) + ColorWall 고유(좌우, 별도 압박). 팀 외침 = Warning 중이면 공격 취소, Hold 중이면 **원상 복구**. §3 |
| T2 | 미니게임 **유지.** 팀 외침 = **거리 기반 안개 공격 초출** §5. 퍼즐 판정 안 고침 |
| T3 | 조임 **복습**(같은 원통 반경 메카닉). 2인 장면 = **전원 외침 원상 복구**만. ColorWall **흑백** 초출 = 색 일(잠깐 멈춤), 2인 게이트 아님. 패드 Door_3 커먼. Door_1형 4색 겹침 삭제. Spike·볼더·Thron·점액·넉백은 압력 |
| T4 | **ContactKnockback + 뚫린 바닥 낙사**가 판. 각자 생존, 2인 게이트 아님. **패드 밟으면 Door가 올라와 구멍 위 길이 됨** (Door_3 1:1·커먼). 그 길도 2인 게이트 아님. SideSplit **삭제 예정**. MovingCorridor는 압력. 2인 장면 = **안개 걷힘**. 조임 원상 복구 없음 |
| T5 | Floor + Runner/Chaser **유지**. 응원·2인 장면 **보류**. 지금 재설계·안개 재사용 없음 |
| T.Boss | ColorTile = M. 초출 시드 §4. **응원 보류.** 페이즈 초안 §6 **[미잠금]** (P3 미정). 조임 쓰면 T1·T3와 같은 원통 반경 메카닉(축 선택 불필요) |
| 조임 원상 복구 | **T1·T3만** |
| 안개 | **T2 초출, T4 복습.** 거리 기반(Render Fog), 씬 전역 — 구간 분리 없음. 미리보기 정답 다시 보여주기 금지 |

### H.3 T에서 버린 제안

- 고유색 패드·리맵 폐기 (문제는 Door_1 중첩)
- T2 Memory/Pioneer를 개인전/협동으로 다시 짜기
- 조임 원상 복구를 T4·T5·T.Boss에 지금 붙이기
- 안개로 Memory 미리보기 정답을 다시 보여주기
- 계속 고함으로 벽·안개를 붙잡아 두기
- T1에 고유+흑백을 한 번에 초출
- ColorWall을 “동시에 고유색 두 벽이 길을 막아 멈춤이 필수”로 바꿔 T3 2인 게이트 만들기
- Spike·Thron·점액·넉백·볼더로 협동 만들기
- T4 넉백·낙사를 2인 게이트로 만들기
- T4 패드→Door 길을 “한 명이 밟고 한 명이 건넌다”로 2인 게이트 만들기

### H.4 코드

`CheerService` = Heal·120초 폐기, `ITeamCheerRevert` 있음. **조임(`EsophagusSqueeze`)·안개(`EsophagusFog`) 코드 됨(2026-09-06, SalivaHazard와 동일한 윈도우 모델로 최종 확정).** `AdvancingWall`은 스케줄 후퇴만. 새 RPC 없음.

**조임/안개 코드 (2026-09-06 최종 — 랜덤 주기 공격 모델):**
- 둘 다 `MouthController`/`SalivaHazard`와 **완전히 동일한 상태 머신**을 쓴다: `Idle(응원 무시) → Warning(UI, 응원 유효) → [외침: Attack 취소, Idle로] / [무응답: Attack(공격, attackDuration에 걸쳐 진행) → Hold(그 상태 유지, 외침 전까지 무한 대기) → 외침 시 Recover(recoverDuration에 걸쳐 원상) → Idle]`. Idle마다 `randomIntervalMin~Max` 랜덤 간격(`NetworkSessionData.Seed` 기반 결정론적 시드, `PickSeededInterval`) 대기 후 다음 Warning. `PhaseStartServerTime` 앵커로 첫 창 동기화(`ResolveFirstWindow`), 세대 관리(`_syncGeneration`)·창 스킵(`_skipNextWindow`)까지 SalivaHazard 그대로 재사용. **Attack 도중 즉시 중단(2026-09-06 추가):** Attack(Squeezing/Thickening/Covering) 진행 중 외침이 성공하면 `_recoverQueued`를 즉시 체크해 그 시점(현재 반경/밀도/알파)에서 루프를 멈추고 바로 Recover로 전환한다 — 예전엔 Attack이 목표치까지 다 찬 뒤에야 Recover를 시작했다. `SalivaHazard`도 동일하게 수정됨. `MouthController`/`TongueController`는 Animator 클립 기반이라 이 수정 대상에서 제외(별도 검토 필요).
- `EsophagusSqueeze`(`Assets/Scripts/Cheer/EsophagusSqueeze.cs`) — **MeshCollider 스케일 폐기, Box 조각 링(조리개) 방식으로 최종 교체(2026-09-06).** 원통을 스케일하는 대신 이 컴포넌트를 원통 중심에 두고 자식 `segments[]`(평평한 Box 판자 8개, 카메라 조리개처럼 방사형 배치)를 건다. 각 판자는 회전 없이 자기 반경 방향으로만 `Rigidbody(kinematic).MovePosition`으로 평행 이동 — 스케일이 아니라 위치 이동이라 재굽기 자체가 없어 Attack/Recover뿐 아니라 매 프레임 완전히 매끄럽게 움직여도 비용이 없다. Attack 중 원래 반경 → **`squeezeTargetRadius`(압박 강도, 인스펙터)**로 Lerp, Hold 중엔 고정. 판자 폭을 원래 반경(rest)에서 이웃과 맞물리게 잡으면 조여들수록 인접 판자 간격이 더 줄어 틈이 안 생김(계산상 보장). kinematic Rigidbody가 실제로 밀고 들어와 Player(dynamic Rigidbody)가 물리적으로 밀려나므로 MeshCollider 스케일 때의 끼임 위험도 사라짐(AdvancingWall과 동일 원리).
- `EsophagusFog`(`Assets/Scripts/Cheer/EsophagusFog.cs`) — `RenderSettings.fogDensity`를 Attack 중 0 → **`maxDensity`(압박 강도, 인스펙터)**로 Lerp, Hold 중 고정. 콜라이더 비용은 원래 없어서 이 구조 자체는 성능 사유가 아니라 "랜덤 주기로 팀 응원을 계속 유도" 사유로 채택.
- `ColorWall`은 조임과 별개(좌우 압박, 되돌림 대상 아님) — 변경 없음.
- **에디터(사용자, 남음):** T1/T3 씬에 `EsophagusSqueeze` 배치 — 이 오브젝트를 식도 원통 중심에 두고, 자식으로 판자 8개를 45°씩 등간격 방사형 배치(각 판자 = Box 오브젝트 + `Rigidbody(Is Kinematic=true, Interpolate)` + `BoxCollider`, 폭은 원래 반경에서 이웃과 맞물리는 값). `segments[]`에 그 8개 Transform 연결, `squeezeTargetRadius`/`attackDuration`/`recoverDuration`/`randomIntervalMin~Max`/`warnDuration` 튜닝(선택 시 Gizmo로 원래→목표 반경 이동 경로 확인 가능). T2/T4 씬에 `EsophagusFog` 배치(`maxDensity`/`fogColor`/같은 랜덤·클립 필드 튜닝). 안개는 URP **빌트인 Fog**(Lighting > Environment)를 코드가 켜므로 별도 Volume 오버라이드 없음 — URP Lit 계열만 반영, 커스텀/Unlit 셰이더(Boulder 등)는 범위 밖. `TeamCheerWarningUI`는 M과 동일하게 `CheerService.OnHazardWindowChanged` 구독 — 씬에 해당 UI prefab 배치 여부만 확인. 둘 다 씬당 **하나만** 둘 것(`CheerService.RegisterRevert`가 중복 등록 경고).

### H.5 다음 (우선)

1. **구현 코드 됨 (2026-09-06, 최종 = 랜덤 주기 공격 모델):** T1 조임 **원상 복구** → T3 복습 → T2 안개 초출 → T4 안개 복습. `EsophagusSqueeze`/`EsophagusFog`가 `ITeamCheerRevert`에 꽂힘. 새 RPC 없음. **에디터 배선(사용자, 남음)** — §H.4 참고.
2. T5·T.Boss는 그 뒤에 다시 감사. 지금 잠그지 않음.
3. 에디터(사용자, 구현과 맞춰): T1 Door_1→Door_3. T3 Door_1형 4색 겹침 삭제·Door_3 커먼. T4 패드→Door 길·SideSplit 삭제. T3 `ThronSeq.*`에 `NetworkObject`. + §H.4의 `EsophagusSqueeze`/`EsophagusFog` 배치.
4. 초·안개 창·조임 속도는 해당 스테이지 때(인스펙터 `squeezeTargetRadius`/`maxDensity`/`attackDuration`/`recoverDuration`/`randomIntervalMin~Max`/`warnDuration`로 노출됨 — 플레이하며 튜닝).

### H.6 다음 에이전트 — T5 보류 **[미잠금]**

T1–T4 설계는 끝. T5를 지금 감사하지 말 것.

**보류 이유:** 되돌릴 식도의 일이 안 떠오름. 조임·안개는 T1–T4. 세 번째 동사·안개 재사용·Runner/Chaser 재설계는 조임·안개를 플레이한 뒤에.

**있는 판 (나중에 쓸 사실):** Stage5.1–5.4. 5.1–5.2 Runner 접촉 포획(색 없음). 5.3–5.4 Chaser 근접 추적. 재설계하지 말고 유지.

**안 함:** T5 지금 잠그기, T.Boss 페이즈를 T5와 한 번에, 구현은 “구현해” 전까지.

---

## 0. 식도 동사 **[확정]**

T = 긴 통로. 떨어져서 역할 나누기. 이동이 곧 시간. 통로를 입으로 다시 구상하지 않음.

| 동사 | 한 줄 |
|------|------|
| 조임 | **채택 §3.** 랜덤 주기로 식도 원통이 공격처럼 좁혀와 그대로 유지(Hold). 응원 예고 중 외치면 취소, Hold 중 외치면 **원상 복구**. 초출 T1, 복습 T3. ColorWall은 별도 좌우 압박(되돌림 대상 아님). **T1·T3만** |
| 안개 | **채택 §5.** 랜덤 주기로 거리 기반 Render Fog가 짙어져 유지(전역). 예고 중 외치면 취소, Hold 중 외치면 걷힘. 초출 T2, 복습 T4. 미리보기 정답 아님 |

ColorWall 일치 = 그 벽만 잠깐 멈춤 (색 일). 팀 외침 조임 = **전부 원상 복구**. 같은 일을 두 번 하지 않음.

---

## 1. 감사 보드

| 씬 | 현재 컨텐츠 | 버킷 | 남길 장면 | 바꿀 판정 | 빼도 되는 함정 |
|----|-------------|------|-----------|-----------|----------------|
| T.Stage1 | 패드·문·볼더 + 조임 초출 | **A [확정]** §3 | Door_3. 원통 반경 조임(전방향, 랜덤 주기 공격). 원상 복구 | ColorWall **고유**(좌우 압박, 별도) + 원통 반경. Door_1은 사용자가 Door_3으로 | 볼더 **유지** |
| T.Stage2 | Memory / ColoredMemory / Pioneer | **A [확정]** | 안개 걷힘 | 퍼즐 안 고침. 안개는 정답 미리보기 아님. 거리 기반 Render Fog | |
| T.Stage3 | Wall·볼더·Spike·패드 + 조임 복습 | **A [확정]** §3 | 같은 원통 반경 조임. **전원 외침 원상 복구** | ColorWall은 2인 게이트 아님. **흑백** 초출 = 색 일. Door_1형 4색 겹침 삭제. Door_3 커먼 | Spike·볼더·Thron·점액·넉백은 압력 |
| T.Stage4 | MovingCorridor + ContactKnockback + 구멍 바닥 + 패드→Door 길 | **A [확정]** §5 | **안개 걷힘** (2인 장면) | 패드 밟으면 Door가 올라와 길이 됨 (Door_3). 넉백→구멍 낙사·그 길은 각자 생존. SideSplit **삭제 예정**. 조임 원상 복구 없음 | 넉백은 압력. 협동으로 안 바꿈 |
| T.Stage5 | Floor + Runner/Chaser | 보류 | 판 유지 | 응원·재설계 지금 없음 | |
| T.Boss | ColorTile + SurviveTime + AdvancingWall/ColorWall | 보류 | ColorTile = M. 초출 시드 §4. 페이즈 초안 §6 | 응원·P3 미정. 확정 아님. 조임 쓰면 T1·T3와 같은 원통 반경(축 선택 불필요) | |

**T.Stage1.** 리맵 유지. Door_1 중첩 4색이 뒤죽박죽. Door_3 = 패드가 바로 앞 문과 1:1.

**T.Stage2.** 제일 쉬운 판. 퍼즐 유지. 안개만 추가.

**T.Stage3.** 2인 장면 = **조임 + 전원 외침 원상 복구.** 한 명이 안 외치면 벽이 안 돌아감. ColorWall 흑백은 색 수업(잠깐 멈춤)이지 2인 게이트가 아님. 고유 두 벽을 동시에 멈춰야 살게 만들지 않음. Door_3 커먼 유지. 4색 겹침 문은 삭제. 함정은 압력.

**T.Stage4.** 판 = **ContactKnockback + 뚫린 바닥.** 부딪히면 튕기고, 구멍으로 낙사. 각자 생존. 2인 게이트 아님. **패드(Door_3 커먼, 1:1)를 밟으면 Door가 올라와 구멍 위 길이 된다.** 길 만들기·건너기도 2인 게이트 아님(한 몸이 순서대로). Door_1형 4색 겹침 없음. 2인 장면 = **안개 걷힘** — 한 명이 안 외치면 구멍·길이 안 보임. SideSplit 삭제 예정. MovingCorridor는 압력. 조임 원상 복구 없음.

**T.Stage5 / T.Boss.** 설계 보류. Runner/Chaser 유지. 응원은 T1–T4를 플레이한 뒤에.

---

## 2. 팀 응원 (식도) **[확정]**

전원이 TeamCheerWord → 식도가 한 일을 되돌린다. +2힐 폐기. **T1·T3·T2·T4는 SalivaHazard와 동일한 윈도우 모델** — Idle에서 랜덤 주기로 Warning이 뜨고, 그때 외치면 이번 공격이 통째로 취소되며, 못 외치면 압박/안개가 걸려(Hold) 무한 대기하다 그때 외치면 원상 복구된다. 창을 놓쳐도 다음 창은 다음 랜덤 주기에 다시 온다.

| 판 | 효과 |
|----|------|
| T1·T3 | 조임 **원상 복구** |
| T2·T4 | **안개 걷힘**(거리 기반) |
| T5·T.Boss | 아직 없음 |

위치 조건 없음. 1인은 TeamCheerWord 1회.

2인 T1·T3: 한 명이 외치지 않으면 벽이 원상 복구되지 않음. **T3의 2인 장면은 이 외침이다.** ColorWall은 자기 색(또는 흑백) 벽만 잠깐 멈춤 — 색 일, 게이트 아님.  
2인 T2·T4: 한 명이 외치지 않으면 안개가 걷히지 않음. **T4의 2인 장면은 이 외침이다.** 넉백·낙사·패드→Door 길은 각자 생존.

---

## 3. 조임 **[확정: 개념 + 랜덤 주기 공격 모델 2026-09-06 최종]**

**원상 복구는 T1·T3만.** T.Boss는 페이즈 때.

**메카닉 교체:** 좌우/위아래 축 구분(AdvancingWall 평면 벽 두 짝)을 폐기하고, **식도 원통(BG) 자체의 반경을 축소**하는 것으로 통일.

**모델(2026-09-06, 하루에 두 번 확정 — 이게 최종):** 처음엔 "창 없음, 씬 시작부터 지속 축소"로 갔다가, "팀 응원을 계속 유도해야 한다"는 이유로 **SalivaHazard와 동일한 랜덤 주기 공격 모델**로 바꿈:

- **Idle** — 원래 반경, 응원 무시. 랜덤 간격(`randomIntervalMin~Max`, 인스펙터) 대기.
- **Warning** — UI 예고. 이 동안 외치면 이번 공격 자체가 취소되고 Idle로 돌아가 다음 랜덤 간격을 기다림.
- **Attack** — 예고를 넘기면 `attackDuration`(기본 1.5초)에 걸쳐 원래 반경 → **`squeezeTargetRadius`(압박 강도, 인스펙터로 fog `maxDensity`처럼 노출)**로 부드럽게 줄어듦. **도중에 외치면(2026-09-06 추가) 그 지점에서 즉시 멈추고 바로 Recover로 전환** — 목표 반경까지 다 조여질 때까지 기다리지 않는다.
- **Hold** — `squeezeTargetRadius`로 고정, 외침 전까지 **무한 대기**(자동 복구 없음).
- **Recover** — 외침 성공 시 `recoverDuration`(기본 1초)에 걸쳐 원래 반경으로 복귀 → Idle.

**콜라이더(2026-09-06 최종 — MeshCollider 스케일 폐기 → Box 조각 링(조리개)):** 원통 하나를 스케일하면 non-convex `MeshCollider`가 매 프레임 재굽기(re-cook)돼야 해서, 대신 원통 중심에 `EsophagusSqueeze`를 두고 자식으로 평평한 Box 판자 **8개**를 카메라 조리개 블레이드처럼 방사형(45°씩) 배치한다. 각 판자는 회전 없이 자기 반경 방향으로만 `Rigidbody(kinematic).MovePosition`으로 평행 이동 — 스케일이 아니라 위치 이동이라 재굽기 자체가 없어 Attack/Recover뿐 아니라 매 프레임 완전히 매끄럽게 움직여도 비용이 전혀 없다. 판자 폭을 원래 반경(rest)에서 이웃과 맞물리게 잡으면(인접 판자 중심 간 거리가 반경에 비례하므로) 조여들수록 겹침이 늘어날 뿐 틈이 절대 안 생긴다 — 계산으로 보장됨. 부가 이득: kinematic Rigidbody가 실제로 밀고 들어와 Player(dynamic Rigidbody)가 물리적으로 밀려나므로, MeshCollider 스케일 때 있었던 "플레이어가 벽 안에 끼거나 반대편으로 관통" 위험도 없어짔다(AdvancingWall과 동일 원리).

- 전방향으로 균등하게 줄기 때문에 **T1·T3·T.Boss 모두 같은 메카닉**(축을 좌우/위아래 중 고를 필요 없음). T1↔T3의 차이는 이제 조임 자체가 아니라 **ColorWall 색 복잡도**(고유 → 흑백)에서만 남는다.
- **ColorWall = 별도 좌우 압박.** 원통 반경과는 독립된 옆벽 패널(순전진 0 `AdvancingWall` — 매 사이클 전진 후 제자리 복귀, 누적 없음)에 붙여서 색 일치=잠깐 멈춤만 준다. 되돌림(`ITeamCheerRevert`) 대상이 아님 — 볼더·Spike·Thron·점액·넉백과 같은 "압력" 취급.

패드(Door_3)와 조임은 다른 장면. T3 2인 장면은 **Hold 중 외침 원상 복구**만. ColorWall 멈춤은 색 일. 볼더·Spike·Thron·점액·넉백은 압력.

안 함: T4·T5·T.Boss에 지금 원상 복구, T1에 고유+흑백 한 번에, ColorWall을 동시 고유 두 벽 필수 멈춤으로 2인 게이트 만들기.

코드: `EsophagusSqueeze`, §H.4. 새 RPC 없음 — `ServerTime` 폴링 결정론적 랜덤(`NetworkSessionData.Seed` 기반) + `PhaseStartServerTime` 앵커로 첫 창 동기화.

---

## 4. T.Boss 초출 시드 **[확정]**

보스에서 메카닉 초출 금지. Telegraph는 연출. **응원·페이즈는 나중에.**

| 메카닉 | 가르칠 곳 | 안 함 |
|--------|-----------|--------|
| 원통 반경 조임 | **T1·T3** 조임 | 텔레그래프를 메카닉으로 세기 |
| ColorWall 고유색 | **T1** | 보스에서만 처음 |
| ColorWall 흑/백 | **T3** | T1에 고유와 동시 초출 |
| `AdvancingWallTelegraph`(ColorWall 좌우 압박용) | **건너뜀** | 초출 목록에 넣기 |

ColorWall 일치 = 그 벽만 잠깐 멈춤. 흑백은 아무나, 고유는 그 색만. **멈춤이 필수가 아니면 1인 통과.** T3 2인 장면은 이 멈춤이 아님.

---

## 5. 안개 **[확정: 개념 + 랜덤 주기 공격/Render Fog 모델 2026-09-06 최종]**

식도의 두 번째 일 = 시야를 가림. M 닫힘 암흑(거리 무관 화면 전체 알파)과는 다른 메커니즘 — **거리 기반 Render Fog**(`RenderSettings.fog`). 가까운 건 보이고 먼 것만 흐려짐. 파티클 바닥 안개는 밀도를 올리면 렉이 걸려서 **폐기** — Render Fog는 셰이더에 이미 있는 계산이라 훨씬 저렴함.

초출 **T2**. 복습 **T4**. T5·T.Boss는 지금 안 붙임.

**모델(§3 조임과 동일한 이유로 같은 날 확정 → 최종):** 처음엔 "창 없음, 지속 증가"였다가, **조임과 같은 SalivaHazard 랜덤 주기 공격 모델**로 최종 확정. Idle(밀도 0, 응원 무시) → 랜덤 간격 대기 → Warning(예고, 외치면 취소) → Attack(`attackDuration`에 걸쳐 밀도 0 → **`maxDensity`(압박 강도, 인스펙터)**, **도중에 외치면(2026-09-06 추가) 그 지점에서 즉시 멈추고 바로 Recover로 전환**) → Hold(그 밀도로 무한 대기) → 외침 성공 시 Recover(`recoverDuration`에 걸쳐 0으로) → Idle.

- **씬 전역 적용, 구간 분리 없음.** `RenderSettings.fog`는 씬 단위 설정이라 같은 씬 안에서 안개 구간/비안개 구간을 나누지 않음(T.Stage는 대부분 페이즈 1개라 문제 없음).
- **팀원 시야도 동일하게 영향받음** — 거리 기반이라 멀리 있는 팀원도 잘 안 보이게 됨. Host/Client 차이 없이 각 머신이 같은 `ServerTime` 기준으로 로컬 계산.
- Boulder 등 **커스텀 셰이더를 쓰는 요소는 이 작업 범위 밖** — 필요하면 그쪽에서 별도로 Fog 반영.

T2 = 타일이 곧 판. 거리 기반이라 "가까이 가야 읽힌다"는 느낌이 자연스럽게 나옴 — 원래 취지("이속만으로는 타일·복도를 못 읽는다")에 오히려 더 맞음. T4 = **구멍 난 바닥**과 **올라온 Door 길**을 보고 달려야 함 (넉백에 튕겨 구멍으로 낙사). T5는 캐릭터 추적이라 기본 안 씀.

Memory 미리보기 정답을 다시 켜지 않음(안개가 걷혀도 정답 하이라이트 아님, 그 순간 챌린지 상태의 바닥만).

2인: Hold 중(공격을 못 막았을 때) 한 명이 외치지 않으면 안개가 걷히지 않는다.

안 함: 미리보기 Safe/Trap·색 경로를 다시 보여주기, T5·T.Boss에 지금 붙이기.

코드: `EsophagusFog`, §H.4. 새 RPC 없음 — `ServerTime` 폴링 결정론적 랜덤(`NetworkSessionData.Seed` 기반) + `PhaseStartServerTime` 앵커로 첫 창 동기화.

---

## 6. T.Boss 페이즈 초안 **[미잠금, 2026-09-04]**

확정 아님. T1–T4 잠금·§4 초출 시드는 유지. T5는 이 초안에 넣지 않음. ColorTile은 이 초안에 없음.

**공간:** 지금 T.Boss 환경. 전체 발판 100×100. **25씩 네 번** 올라온다. 처음은 통로 폭, 마지막은 한정된 방. 수치(100·25)는 예시, 나중에.

**시계:** 식도 끝 Sphere가 내려온다. 땅에 닿으면 전멸. 각 페이즈를 깨고, 닿기 전에 **전부** 깨면 승리.

| # | 발판 | 판 | 식도 동사 |
|---|------|-----|-----------|
| 1 | 25×100 | Pioneer로 긴 쪽을 뚫음 | 조임 |
| 2 | 50×100 | Door가 올라와 길, 반대편 도착. SpikeTrap = 폭탄 피하기(압력) | 안개 |
| 3 | 75×100 | **미정** | **미정** |
| 4 | 100×100 | ColorWall **고유+흑백**(좌우 압박), 원통 반경 조임 | 안개 |

P3은 비움. 응원(조임 원상 / 안개 걷힘)을 페이즈에 어떻게 붙일지는 아직.
