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

### H.5 다음 에이전트 — 여기부터 (2026-09-03)

**트랙:** M 팀 응원 되돌림. T1 조임은 이 트랙 뒤.

1. **읽기:** 이 절 → §4·§5·§6. 공유 [`CoopStageAudit.md`](CoopStageAudit.md) §9. T는 아직 구현하지 않음.
2. **코드 (됨):** **M2 침** — `SalivaHazard` + `SalivaVolume` + `Player.Move()` 얼음 가속/코스트. PhysicMaterial 아님. 새 RPC 없음. **M.Stage2 전체(2.1 SideSplit + 2.2 Drop).**
3. **에디터 (됨):** `M.Stage2` `SalivaHazard` + `SalivaVolume` 2.1/2.2. 수면·드립·머티리얼은 **삭제**. 비주얼 슬롯만 비움. `TransitionPhase2` 입 `teamCheerHazard`는 끄기 유지.
4. **혀:** 코드 됨 (`TongueController`). 에디터 남음 — `Tongue.controller` 트리거, M.Stage4 프롭·1×1 배열, 클립 Animation Event `SweepBreak(int)`. `MouthBG` 혀 금지.
5. **입 닫힘:** 코드+에디터 반영됨. M.Stage1 2인 플레이로 확인할 것. 숫자(`warnDuration` 기본 2)는 나중에.
6. **하지 말 것:** 팀 힐·120초 쿨 부활. 새 RPC. Tutorial 팀 외침(마지막). Barrier 슬롯·ColorTile 점수제·T 조임은 침/혀와 별 트랙.

**되돌림 머신 (입 기준, 혀·침·조임 동일):**  
Idle(응원 무시) → Warning(UI, 응원 켜짐) → 외침이면 Attack 안 넣음 / 없으면 Attack 끝까지 → Hold(유지, 암전·침·혀 나온 채) → 외침이면 Recover 클립 → Idle.  
입 Recover = **Open**. 침 Recover = 수면 페이드아웃. 혀 4.1 Recover = **Retract**(Hold 포즈에서 시작, Rise 역재생 아님). 혀 4.2 Recover = 꺼진 1×1 복구 + Idle (**Retract 없음**. Hold 대기 없음 — Attack 후 다음 사이클 반대쪽). Close/Cover/Rise/Attack_L/R이 시작되면 끊지 않음. 자동 Open 없음. 닫힘 대가 = 암전만(HP 없음). 침 대가 = 미끄럼(HP 없음). 혀 대가 = 꺼진 1×1 낙사(HP 없음).

**코드 (있음):**
- `CheerService` — 투표 RPC 유지. `ApplyTeamBuff` = Heal/120 없음 → `_revert.Revert()`. `ValidateTeamCheer`는 `_revert.IsAvailable`만. `RegisterRevert` / `NotifyHazardWindow`.
- `ITeamCheerRevert` — 씬당 하나.
- `MouthController.teamCheerHazard` — true면 위 머신. false면 옛 Close→Hold초→Open.
- `SalivaHazard` — M2 revert. Warning→Cover→Hold→Recover. `SalivaVolume`이 발판 위일 때만 `Player` 얼음 미끄럼.
- `TongueController` — M4 revert. 4.1 RiseHold / 4.2 AttackSweep. `SweepBreak(int)`.
- `TeamCheerWarningUI` — `OnHazardWindowChanged`. `TeamBuffCooldownUI`는 숨김.

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
| 비주얼 | `coverRoots` / `coverRenderers` / `coverParticles` **슬롯만 유지, 비움.** `SalivaCover_*`·드립·`Assets/Art/Mouth/Saliva/` **삭제됨.** 볼륨은 coverRoots 자식으로 넣지 말 것 |

**에디터 (침):** 위 표. 수면 연결은 나중에 빈 슬롯에.

### H.2 M 잠금 (다시 묻지 말 것)

| 항목 | 잠금 |
|------|------|
| M1 | `DirectionalBarrier`를 보스에서 앞으로. 패드→문 상승→incoming 함정 파괴. 뮤텍스 = 한 색만 업. **소리 초출.** 통과 퍼즐 아님. 슬롯 = §2. `Distribute` 1인=전원동색 / 2인=2+2 **쓰지 않음** |
| M2 | **한 씬, 두 구간.** 2.1 SideSplit + **침**. 2.2 Drop + **침**. 암전 안 씀. 2.1 위에 Drop 안 얹음. 라운드로 시간 안 벌음 |
| M3 ColorTile | **컷 취소.** 점수제 §3. 각자 칸 서기 폐기. **입 시계.** 10–12분 |
| M4 | **한 씬, 두 구간.** 4.1 SequenceRing 턴제 + **혀 초출.** 4.2 ArrowTrap + **혀 복습**. 링 위에 화살 없음. M6·M7 없음. 리듬·16칸 암기·검정만 늘리기 **폐기** |
| M5 | Grid Color+BW **유지.** 2인 장면 = BW 후반. Color/1인 쉬움 수용. **WindTrap 유지**, 강도만 사용자. 바람에서 협동 찾지 않음. **입 열기 없음** |
| ColorTile 점수 | 2초(기본) 또는 3초 점유 → 뽕 → 그 색 +1 → **다른 칸에 재스폰**. 고유는 주인만, 흑백은 아무나. 통과 = 고유+흑+백 의무. 흑백 의무 0 금지. 통로 좁게. 함정으로 협동 안 만듦 |
| 소리 초출 | **M1.** 외침으로 닫힘 막기. 닫힘의 맛 = **암흑 시야 정도는 가져감.** 데미지·둘 다는 나중에. M3·보스 복습 |
| 침 초출 | **M2 (2.1부터).** 2.2·보스 복습. PhysicMaterial 아님 — `Player.Move()` 얼음 가속/코스트 (`salivaAccelTime` / `salivaDecelTime`). §6 |
| 혀 초출 | **M4.1.** 보스·M4.2 복습. M6·M7 없음. 1×1 스윕 파괴. 꺼진 칸 낙사→방 리셋. §5 |
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

### H.4 코드

| 대상 | 상태 |
|------|------|
| CheerService 팀 | **됨.** Heal·120초 폐기. Warning~Revert만 유효. 새 RPC 없음. 입 닫힘·침·혀 연결 |
| MouthController | **됨.** hazard 씬만 Close→Hold(외침까지)→Open. 자동 재오픈 없음 |
| 침 | **됨.** `SalivaHazard` / `SalivaVolume` / `Player` 얼음. 수면 비주얼은 슬롯만 비움 |
| 혀 | **됨.** `TongueController`. 에디터(컨트롤러·프롭·타일·이벤트) 남음 |
| ColorTile | 구 클리어(전원 자기 색 칸). §3 미반영 |
| Barrier | 아직 보스. §2 슬롯/M1 이동 미반영 |
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

협동은 순서와 타이밍. 2인+는 **겹치는 incoming**이 있어야 장면.

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

2인: 문이 한 색만 올라가는데 incoming이 두 색에서 겹치면 순서를 맞추거나 한쪽을 맞는다.

---

## 3. ColorTile 점수제 **[확정]**

컷 취소. M.Stage3 유지. 입 시계 안에서 점수. 구현 아직. 통로 좁게. 10–12분 = 할당량 + 창. 라운드 수로 안 벌음.

점유: 연속 2초 또는 3초(기본 2) → 뽕 → 그 색 +1 → **다른 칸에 재스폰**. 발 떼면 리셋.

| 타일 | 누가 |
|------|------|
| 고유색 | 그 색만. 다른 색은 점수 없음 |
| 백 / 흑 | 아무나 |

통과: 고유 의무 **그리고** 백 의무 + 흑 의무. 덤 합산(의무보다 큰 총점) 없음. 흑백 의무 0 **금지**. 1인은 혼자 순환. 2인+는 몸이 고유+흑백보다 적으니 담당을 나눔.

2인: 흑·백을 아무도 안 채우면 실패. 자기 색만 밟으면 실패.

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
| 1 초출 | 4.1 SequenceRing | Rise→Hold→Retract. 가운데 1×1 ×9. 가림막. 링 위 화살 없음 |
| 2 복습 | 4.2 ArrowTrap | Attack 한 번에 L **또는** R 하나. 왼쪽/오른쪽 1×1 ×10 (2×5). Hold·Retract 클립 없음. 화살은 압력 |
| 보스 | M.Boss | 복습만. P2는 4.2쪽 |

제때 외침 = Attack 안 넣음. 늦게 외침 = 꺼진 1×1 복구 (이미 낙사면 방 리셋이 먼저).

**타일:** 전부 **1×1**. 큰 판 아님. 인스펙터 배열. 배열 순서 = 혀가 지나가는 순서.
- 4.1 가운데: **9칸** (3×3)
- 4.2: 가로 5열 기준. 왼쪽 **10칸** (2×5) / 가운데 **5칸** (1×5) / 오른쪽 **10칸** (2×5). 가운데 1×5는 L·R 배열에 안 넣음. 3×5+3×5는 가운데 1×5가 겹쳐서 **폐기**

**스윕:** Rise·Attack_L·Attack_R 모두 **혀가 그 칸을 지나는 프레임에 그 1×1만** 끔 (콜라이더+메시). 클립 시작에 전부 끄지 않음. 클립 Animation Event `SweepBreak(int)` (칸 인덱스). 클립 끝나면 남은 칸은 코드가 끔.

**4.1 머신:** Idle → Warning → 외침이면 Rise 안 넣음 / 없으면 Rise 끝까지 → Hold(9칸 꺼진 채, 혀가 가림막 — 반대편 시퀀스는 돌아서 봄) → 외침이면 Retract + 9칸 복구 → Idle.

**4.2 머신:** Idle → Warning → 외침이면 Attack 안 넣음 **그리고 꺼진 칸 전부 복구** / 없으면 **이번 방향 하나**만 끝까지 (L이면 왼 10칸, R이면 오른 10칸. 한 클립에 L+R 같이 안 함) → Hold 클립 없음. 혀 Idle.
- Attack 중 외침: 클립은 끊지 않음. 끝나면 꺼진 칸 전부 복구.
- 안 외치면 그 10칸은 꺼진 채 **다음 사이클이 반대쪽**. 그래서 L 다음 R을 놓치면 왼 10+오른 10이 꺼지고, **가운데 1×5는 두 번 다 맞아도 켜져 있음**.
- 방향: 한 번에 한쪽. 이번이 L이면 다음은 R. 첫 방향만 시드.

**낙사:** 꺼진 칸에 서 있으면 낙사 → 방 리셋. 혀 히트박스 없음. 가운데 기둥 없음. `Breakable` 안 씀.

**프롭:** `TongueAttack.fbx` + `Assets/Animator/Tongue.controller`. `MouthBG` 혀 금지. 구간당 경기장 혀 1개. `ITeamCheerRevert` 씬당 하나 — 4.1/4.2 전환 시 활성 혀만 등록.

**시드:** 4.2 **첫** L/R만 `NetworkSessionData.Seed`. 이후는 교차. 전 머신 동일.

안 함: 4.1+4.2 한 바닥, 화살 전용 새 씬, 혀 무게로 기울이기, 혀 맞음=밀침, 큰 판 하나, 4.2 3×5(가운데 겹침), 한 번에 L+R.

코드 `TongueController`. 에디터(컨트롤러·프롭·타일 배열·이벤트) 남음.

---

## 6. 침 **[확정: 개념]**

초출 **M2 전체**(2.1 SideSplit + 2.2 Drop). 보스 복습.  
외치면 지움. 안 외치면 미끄러운 채로. 피하기만으로는 클리어 아님.

**이동:** 얼음(극적 미끄럼). 물속 저항 아님. PhysicMaterial 아님.  
입력 중 = 가속만 더함(출발 느림·밀림). 손 뗌 = 약한 감속(관성으로 밀림). 반대로 꺾어도 한동안 예전 방향.  
인스펙터: `Player.salivaAccelTime`(기본 1.2), `Player.salivaDecelTime`(기본 3.5, Accel보다 크게).

**코드:** `SalivaHazard` + `SalivaVolume` + `Player.Move()`. 씬당 revert는 침 하나.  
**씬:** `SalivaHazard`, `SalivaVolume_2_1`(2.1 Ground), `SalivaVolume_2_2`(2.2 Floor).  
**비주얼:** 슬롯만 비움. 수면·드립은 나중에 슬롯에 연결.

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

빼는 것: 세이프존 입문, Barrier 초출, 깨물림 모이기, 바람만 페이즈.
