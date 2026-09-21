# Playtest Log

플레이 테스트에서 나온 이슈와 수정 방향 기록. 상태: `open` / `wip` / `fixed` / `verify`(수정 후 플레이 확인 대기).

## 2026-09-21 — 1인 테스트 (Tutorial ~ T.Stage5, T.Boss 미도달)

T.Stage5 StageStartGate 불량으로 T.Boss 이후는 미테스트.

### 묶음 1 — 블로커 / 빠른 버그

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 11 | T.Stage5 | 배선 누락: StageStartGate 미작동, Chaser 미등장 | 원인: `092c643`에서 `StageFlow`의 PhaseManager 삭제 → `PhaseDialogueGate.Begin` 호출자 소멸 → `StageStartGate.Arm` 안 됨(armOnStart=0). 체이서는 별개로 `OnCountdownComplete → T5RunnerDirector.BeginStage`가 원래 미배선. 클리어 후 다음 씬 이동·Tip도 같이 끊김. 조치: `StageFlow`(PhaseManager 1-phase) 복구 + `OnStageClear→AdvancePhase` + `OnCountdownComplete→BeginStage` 배선(MCP) | verify |
| 1 | M.Stage1 | Banana trap projectile이 안 부서짐 | 원인: 비볼록 MeshCollider + 동적 Rigidbody 조합은 Unity가 충돌을 처리하지 않아 `Breakable`에 이벤트가 안 옴. Trigger+Convex로 바꿔 다른 음식 프리팹(Apple/Watermelon 등)과 설정 통일 | fixed |

### 묶음 2 — M.Boss

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 5 | M.Boss P2 | 중앙 FixedBox에서 **카메라**가 벽을 뚫음(플레이어 아님) | 원인: `Box.fbx` 메시 4.3 vs BoxCollider 4 → ×5 스케일에서 보이는 면 ±10.75, 충돌 면 ±10. 카메라 SphereCast가 충돌 면 앞(x=10.45)에 멈춰 **보이는 메시 안 0.3m**에 들어감(플레이 모드 실측). 조치: 스케일 5→4.649, 콜라이더 4→4.302 → 보이는 면=충돌 면=±10(게임플레이 동선 그대로). 재측정 결과 카메라가 메시 밖 | verify |
| 10 | M.Boss P2 | 제한 시간이 감으로 잡혀 있어 긴장감 부족 | 계산: 1인 최악 ≈4.8s(8s면 쉬움) / 4인 통상 ≈6.5–7s·통로 교행 충돌 시 ≈8.5–9s. 조치: `SideSplitChallenge.roundTimeLimitByPlayerCount` 추가, M.Boss = [5.5, 6.5, 7.5, 9]. resolveDelay 5 유지 | verify |
| 12a | M.Boss P4 | 패턴 간 텀이 김 | interCycleGap 5→2, cheerWindowSeconds 6→5 (MCP 적용) | verify |
| 12b | M.Boss P4 | 마지막 1칸 남으면 Dialogue 후 종료여야 하는데, TeamCheer 발동 + 바닥 복구 후 Dialogue가 나옴 | 원인: 6회차에도 응원 창이 열려 성공 시 25칸 복구 후 클리어. 조치: `MouthBossJawSmash` 마지막 회차는 창 없이 Open 직후 클리어 | verify |

### 묶음 3 — 이동 속도 밸런스

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 8 | 공통 | SpeedUp 버프 10초 너무 김 | 배율·지속시간 재산정(#7과 함께 계산) | open |
| 7 | T.Stage1 | Boulder가 너무 느림, 수동 조절 어려움 | 플레이어 기본/버프 속도 기준으로 Boulder 속도 공식화(버프 사용 시 여유, 미사용 시 빠듯) | open |

### 묶음 4 — M 스테이지 난이도

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 2 | M.Stage4 | SequenceRing 너무 쉬움 | 난이도 변수(길이·시간·속도) 상향 | open |
| 2b | M.Stage2 | SideSplit 인원별 라운드 시간(`roundTimeLimitByPlayerCount`) 미설정 — M.Boss와 달리 중앙 박스가 없어 동선이 다 뚫려 있어 체감이 다름 | M.Stage2 동선 기준으로 따로 계산 후 값 입력(비워 두면 기존 `roundTimeLimit` 그대로) | open |
| 3 | M.Stage5 | 너무 쉬움 | 방해 요소 추가 또는 Wind 강도를 이동 속도 기반으로 계산 | open |
| 4 | M.Stage5 | WindTrap 파티클이 트랩 주기보다 ~0.5초 김 | 파티클 재생 길이를 트랩 active 구간에 코드로 동기화 | open |

### 묶음 5 — T.Stage4 난이도

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 9 | T.Stage4 | 너무 쉬움 | `TStage4TrapRandomization.md` 기준 파괴 용량·주기 상향 | open |

### 묶음 6 — 바닥 타일 파괴 복구 연출

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 13 | M.Boss 등 | TongueAttack / Jaw Smash 타일 파괴 연출은 좋은데 복구가 아쉬움 | 복구 연출 방식 설계 논의 후 적용 | open |

### 묶음 7 — T 스테이지 벽 콜라이더

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 6 | T.Stage1/3 | Wall.L/R BoxCollider가 메시와 안 맞아 이질감. WallLineRandomizer 때문에 MeshCollider 불가 | WallLineRandomizer 구조 확인 후 방안 결정(세그먼트별 콜라이더 / 합성 메시 / 단순화 등) | open |
