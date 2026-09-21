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
| 13 | M.Stage2 2.1 · M.Boss P2 | SideSplit 안내 UI(검정 박스+흰 문장)가 안 읽힘, 라운드 시작 시점을 몰라 갑자기 타이머만 돎, 스핀 중 방향을 못 읽음 (2026-09-22) | 화면 UI 폐기 → 월드 표시: 매 라운드 3·2·1 카운트다운(첫 라운드 포함) → 간판 글씨+하트 공개, 간판 채움 타이머(네 방향 동일, 마지막 1초 빨강 고정 — 바닥 타이머·깜빡임·0명 흐림은 같은 날 제거), 틱 2종(카운트다운/진행). 스핀 폐기 → 카운트다운 중 90° 스냅. 간격: 결과 resolveDelay 3초 + 카운트다운 3초 + 공개 후 읽는 시간 revealReadHold 1초(처음 2+3으로 했다가 "쉬는 시간 짧음" 피드백으로 같은 날 조정). [`MinigameDesign.md`](MinigameDesign.md) §1.8 | verify |
| 12a | M.Boss P4 | 패턴 간 텀이 김 | interCycleGap 5→2, cheerWindowSeconds 6→5 (MCP 적용) | verify |
| 12b | M.Boss P4 | 마지막 1칸 남으면 Dialogue 후 종료여야 하는데, TeamCheer 발동 + 바닥 복구 후 Dialogue가 나옴 | 원인: 6회차에도 응원 창이 열려 성공 시 25칸 복구 후 클리어. 조치: `MouthBossJawSmash` 마지막 회차는 창 없이 Open 직후 클리어 | verify |

### 묶음 3 — 이동 속도 밸런스

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 8 | 공통 | SpeedUp 버프 10초 너무 김, 해제 시 속도 이질감 | SpeedUp **5초 · +6**(16m/s, 1.6배). T5 체이서는 4m/s라 영향 없음, T3 볼더는 회피용이라 유지. **seq2 재계산**(벽 이동 선형 1s, 인접 벽 겹침 2m 구간 11곳의 막힘 시각 시뮬): delays `2.82/0/1.13/0.24/1.01/0.24/1.01/0.83/1.10/0.96/1.04/0.96` → 버프 여유 +1.6s(1초 일찍 켜도 +1.0s), 버프 없이 −1.4s(통과 불가). 구 설계 = 옛 버프 여유 +1.6s·기본 −5.7s | verify |
| 7 | T.Stage1 | Boulder가 너무 느림 | 원인 대부분은 옛 버프(평균 16m/s). 8.5 시험 → 1인 체감 "조금 빠듯"(2026-09-22) → **8로 되돌림**, 사용자가 4인으로 8 테스트 후 결정. 참고식: 속도 = 1455 ÷ (클리어 시간 + 20) | 4인 테스트 대기 |
| 14 | 공통 | Shield 쿨타임이 UI에선 끝났는데 Space가 안 먹힘 | 원인: Host 쿨은 원래 duration 종료 후 시작(`CheerService._buffEnd`), UI는 charge 소모 순간부터 15초 카운트 → 남은 duration만큼 UI가 먼저 풀림. 조치: `CheerProgressUI.HandleBuffRemoved`에서 남은 duration을 쿨에 더함(규칙 불변, 표시만 Host에 맞춤) | verify |

### 묶음 4 — M 스테이지 난이도

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 2 | M.Stage4 | SequenceRing 너무 쉬움 | 추정 필요시간(스텝당 연속 0.3s·교대 0.55s·흰 0.4s·검 1s 대기, 4.2 NextOnly는 0.45/0.7/0.5): 4.1 = 1인 14.8s·2인 36·3인 43.6·4인 50.4 → 현재 제한/필요 = **1인 2.36배**, 2~4인 ≈1.5배. 4.2 = 1인 19.8·2인 42.4·3인 47.7·4인 52.2 → 1인 1.77배, 2~4인 ≈1.45–1.53배. 즉 **1인만 튐** → 조치: `timeLimitByPlayerCount[0]` 4.1 35→**22**, 4.2 35→**28**(1인만 ≈1.5배). 2~4인 유지 | verify |
| 2b | M.Stage2 | SideSplit 인원별 라운드 시간 미설정(전 인원 4초) | 계산(뚫린 25×25, 존 ±11): 반대편 19.5m≈2s·옆 10.6m≈1s, 1인 필요 ≈3s / 4인 ≈4.5–5s → 4초는 4인에 과함. 조치: `roundTimeLimitByPlayerCount`=[4, 5, 5.5, 6] | verify |
| 3 | M.Stage5 | 너무 쉬움 | 원인: 바람 주기(8s)가 라운드 주기(11s)와 따로 돌아 이동 구간에 바람이 올지 운 + 이동 4s가 넉넉(중앙→최원 칸 ≈1s). 바람 세기 자체는 400=≈8m/s로 약하지 않음. 조치(인스펙터 + `WindTrap.anchorToStageStart` 토글 신설): 라운드 3.5s·쉬는 시간 4s(주기 7.5s), 바람 250(≈5m/s)·3.5s, fireAt 5.5·period 7.5, 카운트다운 완료 기준 → 입 차지 2s가 쉬는 시간 끝에 방향 예고, 힘은 라운드 1부터 매 라운드 이동 구간 전체(라운드 0은 바람 없음). 계산: 중앙→역풍 최원 칸 1+1.8=2.8s(여유 0.7) / 구석→반대 역풍 칸 4.65s(실패). **→ 2026-09-22 재테스트(1인)에도 쉬움.** 진짜 원인(사용자): 데미지원이 정산뿐 + 실패 여지 1~2라운드 + 바람 하나로는 방해가 사실상 없음 + 4인은 부활 3회로 라운드 무시 가능. 조치: **라운드 묶음 바닥 붕괴**(`GridTileCollapse`, 안전 칸 외 무작위 순차 파괴·정산 때 복구·낙사 즉사) + 데미지 1→2. 상세 `CoopStageAudit.M.md` §8 바닥 붕괴 | verify |
| 4 | M.Stage5 | WindTrap 파티클이 주기보다 ~0.5초 김 | 원인: 파티클 수명 0.6~1.1s, `Stop()`은 방출만 멈춰 기존 입자가 바람 종료 후 남음. 조치: `WindTrap`이 힘 종료 전 (최대 수명)만큼 먼저 방출 정지 → 마지막 입자가 힘 종료와 함께 소멸 | verify |

### 묶음 5 — T.Stage4 난이도

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 9 | T.Stage4 | 너무 쉬움 | 원인: ③ 파괴 타일 **0개**(판 4장 삭제 때 68개 전부 소실, 남은 판엔 없음) + ① 용량 타일은 솔로 무반응 + 벽 2 m/s(골까지 138s). 조치(사용자 승인, §1.6 원칙 해제): `BreakTile` 24개 재배치(한 줄 최대 1칸·앞뒤 줄 같은 열 금지 → 항상 통행 가능) + 벽 3 m/s(≈92s) | verify |

### 묶음 6 — 바닥 타일 파괴 복구 연출

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 13 | M.Boss 등 | TongueAttack / Jaw Smash 타일 파괴 연출은 좋은데 복구가 아쉬움 | 원인: 복구 = 타일 즉시 켜기뿐(팝 지속시간 0). 조치: 구 팝(`TileRestorePopGroup`) **삭제**(사용자 확정) → **파편 되감기** `TileRestoreRewindGroup` 신설 — 복구 순간 `FloorTileShards`를 새로 스폰, 조각 6개가 랜덤 위치(2~5m)에서 조립 위치로 ease-in 이동 후 진짜 타일 표시. 판정은 즉시(콜라이더 먼저 켜고 Renderer만 숨김), 새 RPC/NV 없음, 복구음 ReverseTime 복구 1회당 1번. Inspector `restoreRewind`: duration **0.8**(2026-09-22, 0.5→0.8 · 복구음 0.96s) · 거리 2~5 · origin Random/Below/Above · stagger 0.15 — **방향은 보고 확정** | verify |

### 묶음 7 — T 스테이지 벽 콜라이더

| # | 씬 | 내용 | 수정 방향 | 상태 |
|---|---|---|---|---|
| 6 | T.Stage1/3 | Wall.L/R BoxCollider가 메시와 안 맞아 이질감. MeshCollider 불가로 보였음 | 원인: `WallLineRandomizer` 무관 — 보이는 벽은 자식 13개(`ColorWallMuscle` SkinnedMeshRenderer, 블렌드셰이프 3개 전부 0·구동 코드 없음), 충돌은 부모의 12×12×1300 상자 하나. 조치(MCP): 자식 13개 × 2줄 × 2씬에 non-convex `MeshCollider`(자식 콜라이더 = 부모 Rigidbody의 복합 콜라이더라 `AdvancingWall`과 함께 이동, `ColorWall` 접촉도 부모로 전달) + 부모 `BoxCollider` 비활성(삭제 X) + 부모 Rigidbody `isKinematic` 씬 값도 true. 검증: 벽 방향 캐스트가 메시에 맞고 부모 rb 소속 확인, 구간 빈틈 없음. ⚠ T.Stage3는 옛 상자 안쪽면 |x|=17 → 메시 표면 ≈20으로 **통로가 양쪽 3m씩 넓어짐**. 빌드에서 벽 충돌 이상하면 `Wall.fbx` Read/Write 켜기 | verify |
