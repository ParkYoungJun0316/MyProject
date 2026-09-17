# T.Stage5 러너/안내자 재설계 계획서

**상태(2026-09-18):** 설계 확정 · **M단계 완료** · **C단계 코드 C1~C9 완료** · **맵/게이트/체이서 배선 완료** ·
UI 배선(§3-W W7) 미완 → **V단계 검증이 다음**
**작업 순서:** ~~M(맵 제작)~~ → ~~C(코드)~~ → **V(ParrelSync 검증)** → C10(정리) → D(문서 갱신)
**이 문서가 T5 재설계 SSOT.** `CoopStageAudit.T.md`의 "T5 보류·Runner/Chaser 재설계 금지"는 이 결정으로 해제 예정(D단계에서 반영 — 사용자 확인 후).

---

## 0. 한 줄 요약

1층 미로에서 **러너 1명**이 체이서를 피해 Goal까지 달리고, **나머지는 투명한 2층 바닥에서 내려다보며** 길을 안내하고 **색 패드를 밟아 문을 연다**. 맵 7개 중 2개를 랜덤으로 뽑아 2라운드, 라운드마다 러너가 바뀐다.

## 1. 확정 결정

### 1.1 역할 / 라운드
| 항목 | 결정 |
|---|---|
| 라운드 수 | **2** (맵 7개 중 랜덤 2개, 중복 없음) |
| 러너 선정 | 솔로 = 본인 2회 / 2인 = 각 1회 / 3~4인 = 서로 다른 2명 랜덤 |
| 리로드 시 | 새 시드로 **맵 2개·러너 2명 전부 재추첨** (이전 러너 유지 없음) |
| 라운드 흐름 | 러너가 1층 Goal 도달 → 전원 다음 맵으로 텔레포트 → 다음 러너 1층, 나머지 2층 → 문 전부 닫힘 · 체이서 리셋 → 3초 카운트다운 + "OO님이 러너" 알림 → 시작 |
| 카메라 인트로 | **없음** (T.Boss P1 교훈 — 리트라이마다 기다리는 연출 금지) |
| 제한 시간 | **라운드마다 120초** |
| 실패 | 러너 사망 **또는** 시간 초과 → 기존 리로드 경로(`StageNetworkState`) |
| 클리어 | 2라운드 Goal 도달 → 기존 StageManager 클리어 흐름 |
| 하트 | 라운드 간 이월 개념 없음(러너가 다른 사람이므로) |
| 다운/부활 | **이번 범위 아님.** 별도 리팩토링(다운 제거·부활만 유지, 낙사 가혹함 완화) — 맵 완성 후 논의 |

### 1.2 문 / 패드 (배타적 색 게이트)
| 항목 | 결정 |
|---|---|
| 규칙 | 패드 1개를 밟으면 **그 색 문만 Open, 나머지 전부 Close**. 흑·백도 동일 규칙의 한 색 (DirectionalBarrier·기존 흑백 토글과 같은 방식, 색 폭만 6으로 확장) |
| 시작 상태 | 라운드 시작 시 **전부 닫힘** |
| 맵당 문 | **15개** = 고유색 설계슬롯 4색(Blue/Purple/Green/Yellow)×2 = 8 + 흑 4 + 백 3 |
| 패드 | 2층에 색당 1개 = **6개**, 2층 곳곳에 **흩어 배치** |
| 패드 권한 | 고유색 패드 = 그 색 플레이어만 / 흑·백 패드 = 누구나 |
| 색 매핑 | 라운드마다 **2층 인원 색**으로 재매핑. 러너 색·빈 색 슬롯은 2층 인원 색으로 시드 랜덤 채움 (DirectionalBarrierRound처럼). 2인이면 고유 4슬롯 전부 안내자 색 |
| 솔로 | 문 **전부 Open 고정**, 2층 패드 숨김 |
| 문 방식 | SlideUp, 닫힐 때 기존 `DoorController` 닫힘 넉백 그대로 |

### 1.3 체이서
| 항목 | 결정 |
|---|---|
| 모델 | 기존 `Stage5ChaserAI`/`Stage5ChaserSpawner` 재사용 (Host 전권 시뮬 + NetworkTransform, `TStageNetworkBoard.md` §3.2) |
| 타겟 | **러너만** (2층 인원 무시) |
| 피격 | 데미지 1 후 **소멸**(Host Despawn) |
| 수 | 살아있는 수 유지: **2~4인 8마리 / 솔로 6마리** |
| 리스폰 | 소멸 후 **6초** 뒤, 러너에서 **30m 이상** 떨어진 스폰 지점 |
| 등장 | 라운드 시작 **3초 유예** 후 전부 등장 |
| 속도 | **8 m/s** (러너 10 m/s — 현재 `Chaser.prefab` moveSpeed 27, NavMeshAgent speed 25는 변경 필요) |
| 문 | 닫힌 문은 **체이서도 막음** (문에 `NavMeshObstacle` carve) |

### 1.4 맵
| 항목 | 결정 |
|---|---|
| 개수 | **7** |
| 크기 | **100×100**, 격자 간격 100/12 = **8.333 → 12×12칸** (통로 폭 8 유지) |
| 벽 | 높이 **15**, 두께 0.5, 기존 규격 그대로 (layer 27, tag Floor, Cube) |
| 문 규격 | 기존 `Door_B_01` 규격 (scale 8.233×15×0.6, SlideUp) |
| 2층 | **y≈25**, MeshRenderer 없는 BoxCollider 바닥(투명), **구멍 없음**, 가장자리 투명 난간 |
| 1층↔2층 연결 | **없음** (점프대·Shaft·NorthDrop 폐기 — 층 이동은 텔레포트만) |
| 시작/Goal | 둘 다 1층, **대각선 반대 모서리** |
| 남북 경계 | 없음 |
| 난이도 | 7개 **비슷하게** (최소 색 전환 8~12회) |

### 1.5 UI / 가독성
- 2층 인원에게만 보이는 **러너 머리 위 마커**
- `ObjectiveUI` Timer로 라운드(1/2) + 남은 시간
- 러너 알림("OO님이 러너")
- `TipUI` `Tip.T.Stage5.1` 문구 교체
- 러너 카메라: 현재 T5 씬 `ThirdPersonCamera` distance 12 / targetOffset.y 5 / maxPitch 50 → 최고 높이 ≈ 5 + 12·sin50° ≈ 14.2 (벽 15와 거의 같음). **maxPitch 40 이하로 낮춰** 벽 위로 미로가 보이지 않게 함 (M단계에서 MCP로 씬 값 수정 후 실측).

---

## 2. M단계 — 맵 제작 (MCP로 에이전트가 직접, 사용자 확인)

> 사용자 요청(2026-09-18): 이 계획서 작성 → `/clear` → 에이전트가 MCP로 맵 제작.

### M1. 레이아웃 생성 + BFS 검증 (씬 수정 전, 스크래치 스크립트)
1. 12×12 격자에서 스패닝 트리(완전 미로) 생성 후 벽 약 10%를 추가 제거해 루프 생성 (체이서 가두기·우회로 여지).
2. Start = 한 모서리 칸, Goal = 대각선 반대 모서리 칸.
3. 정답 경로 위 문 **10~12개**, 연속한 두 문은 **서로 다른 색** → 색 전환 강제. 나머지 문은 곁가지·루프 위(체이서 차단용).
4. 색 배분: 설계슬롯 Blue/Purple/Green/Yellow 각 2, Black 4, White 3 = 15.
5. **BFS 검증** — 상태 = (칸, 열린 색 ∈ {없음, 6색}). 안내자는 언제든 한 색으로 전환 가능. 인원별 매핑 후 색 집합으로 각각 검증:
   - 2인(고유 4슬롯 = 같은 색 → 실질 3색), 3인(2색 + 흑백), 4인(3색 + 흑백; 러너 색 슬롯은 안내자 색으로 채움 — 채움 조합 전부)
   - 조건: Goal 도달 가능 / 최소 색 전환 8~12 / 루프로 정답 경로의 문을 우회할 수 없음(우회 시 최소 전환 수가 기준 미달이면 실패)
   - 솔로: 전부 열림 상태에서 도달 가능(자명)
6. 7개 맵 각각 통과 결과(최소 전환 수, 경로 길이, 막다른 길 수)를 표로 남긴 뒤 M2로.

### M2. 씬 배치 (MCP)
- 배치: 맵 원점은 시작 홀(현재 `StageStartGate`/`ColoredStartZone`·`PlayerSpawnManager` 고정 스폰 주변)과 **겹치지 않게** 떨어뜨린다. 착수 시 시작 홀 좌표를 먼저 읽고 원점 결정 (예: 맵 간 간격 130m, 2줄 배치). `Ground` 콜라이더가 전 맵을 덮는지 확인·확장.
- 계층:
  ```
  StageManager5/T5_Mazes
    Map_01 … Map_07
      Walls/            (Cube, 기존 벽 규격)
      Doors/Blue|Purple|Green|Yellow|Black|White/   (기존 문 복제 + NavMeshObstacle carve)
      Floor2F/          (BoxCollider만, y≈25)
      Rails2F/          (투명 BoxCollider 난간)
      Pads2F/           (색당 1, 6개 — C단계 컴포넌트 부착 전까지 자리만)
      Start1F / Goal1F / Stand2F   (Transform 마커 — 텔레포트 목적지)
      ChaserSpawns/     (1층, 맵당 16개)
  ```
- 기존 `StageManager5/T5_Maze`는 **비활성화만** (삭제는 V단계 통과 후 사용자 승인).
- T5 씬 `ThirdPersonCamera.maxPitch` 조정.
- NavMesh 재베이크 (T5 씬이 쓰는 방식 — NavMeshSurface(`com.unity.ai.navigation` 2.0.10) 여부 착수 시 확인).

### M3. 확인
- MCP 스크린샷(맵별 탑뷰) + 계층 수량 검사(맵당 문 15, 패드 6, 스폰 16).
- 씬에서 읽은 벽·문 좌표로 BFS 재실행 → M1 결과와 일치 확인.
- 콘솔 에러 없음.

---

## 2-R. M단계 결과 (2026-09-18 완료)

생성기·검증 스크립트는 **`Tools/T5MazeGen/`** (Unity 밖, 빌드 비포함). `t5maps.json`이 현재 씬 배치의 원본 데이터다.

### 맵 7개

| 맵 | 시드 | 시작→골(행,열) | 정답 경로 | 경로 위 문 | 최소 색 전환 | 막다른 길 | 루프 | NavMesh 경로 |
|---|---|---|---|---|---|---|---|---|
| Map_01 | 5002 | (0,0)→(11,11) | 53칸 | 12 | 12 | 14 | 12 | 243m |
| Map_02 | 5009 | (0,11)→(11,0) | 63칸 | 11 | 11 | 11 | 12 | 277m |
| Map_03 | 5011 | (11,11)→(0,0) | 59칸 | 11 | 11 | 13 | 12 | 278m |
| Map_04 | 5014 | (11,0)→(0,11) | 55칸 | 12 | 12 | 12 | 12 | 300m |
| Map_05 | 5020 | (0,0)→(11,11) | 61칸 | 10 | 10 | 10 | 12 | 263m |
| Map_06 | 5033 | (0,11)→(11,0) | 55칸 | 11 | 11 | 10 | 12 | 235m |
| Map_07 | 5034 | (11,11)→(0,0) | 63칸 | 11 | 11 | 14 | 12 | 343m |

"최소 색 전환"은 2인·3인(24가지)·4인(12가지)·설계색 매핑 **전부에서 동일한 값**이 나왔다(§1.2 기준 8~12 충족). 솔로는 전부 열림으로 도달 가능.

### 씬 배치

- 루트 `StageManager5/T5_Mazes`, 맵 원점(시작 홀과 분리):
  윗줄 `Map_01~04` = x 200/330/460/590, z **-65** · 아랫줄 `Map_05~07` = x 200/330/460, z **+65**
- 맵당: `Floor1F` · `Walls` · `Doors/{Blue,Purple,Green,Yellow,Black,White}` · `Floor2F` · `Rails2F` · `Pads2F` · `Start1F` · `Goal1F` · `Stand2F` · `ChaserSpawns`
- 총계: 벽 454, 문 105(맵당 15), 패드 42(맵당 6), 스폰 112(맵당 16)
- 고유색 문·패드에 `ColoredDoorVisual` / `ColoredPadVisual` 부착 + 재질 매핑 완료(런타임 색 재매핑용)
- 문 전부에 `NavMeshObstacle`(Box, carving, `carveOnlyStationary=false`)
- 기존 `T5_Maze`는 **비활성화만** (삭제는 V단계 통과 후)
- `ThirdPersonCamera` maxPitch 50→**40**, initialPitch 55→**40**
- NavMeshSurface(`T5_Mazes`, Children/RenderMeshes) + `Assets/Scenes/T.Stage5/NavMesh-T5_Mazes.asset`.
  `Doors`·`Pads2F`에 `NavMeshModifier(ignoreFromBuild)`를 달았으나 **베이크 시 무시되지 않아**, 두 폴더를 일시 비활성화한 상태로 구웠다. **재베이크 시 같은 방법을 써야 한다.**

### 계획서와 다르게 간 부분

- **1층 바닥**: `Ground`를 확장하지 않고 맵마다 `Floor1F`를 따로 깔았다(맵이 원점에서 멀어 확장이 비효율).
- **카메라**: maxPitch만이 아니라 initialPitch도 40으로 낮췄다. 55로 두면 시작 프레임에서 카메라가 벽(15) 위로 올라간다.
- **마커 가시화**: `Start1F`/`Goal1F`에 콜라이더 없는 얇은 판(`Visual`)을 붙여 에디터에서 보이게 했다. Goal 판은 기존 Goal 재질(노랑)이라 **노랑 문과 혼동 여지 있음 — 교체 검토**.
- **2층 난간**: 높이 6m(넘어가기 방지).
- Map 번호는 생성 순서일 뿐 난이도 순서가 아니다.

### M단계에서 확인하지 못한 것 → V단계로

- **열린 문(SlideUp, y+5)이 carve를 해제하는지.** 에디터 idle 상태에서는 carving이 갱신되지 않아 판정 불가. 닫힌 문이 NavMesh를 막는 것 자체는 확인됨. 해제가 안 되면 `DoorController.OnOpened`에서 `NavMeshObstacle.carving`을 끄는 처리가 C단계에 추가로 필요하다.

---

## 3. C단계 — 코드 변경 목록

| # | 대상 | 변경 | 도메인 |
|---|---|---|---|
| ~~C1~~ | ~~`BlackWhiteDoorToggle`~~ → **`ColorGateController`** ✅ | 흑/백 2상태 → "열린 색 1개 또는 없음". 색별 문 묶음, 솔로 전부 Open. 병렬 컴포넌트 새로 만들지 않음 | Stage |
| ~~C2~~ | ~~`BlackWhiteTogglePad`~~ → **`ColorGatePad`** ✅ | 색 필드 추가, 고유색은 해당 색 플레이어만(`PressurePad` 판정 방식 참고), 흑·백은 누구나. Host 판정 유지 | Stage |
| ~~C3~~ | `StageNetworkState` ✅ | `_blackDoorOpen`(bool) → `_openGateColor`(int, -1=전부 닫힘) + `T5RoundState` NV(맵 2·러너 2·현재 라운드·구간 시각) | Network |
| ~~C4~~ | **`T5RunnerRoundDirector`(신규)** ✅ (C5 자리 비움) | Host: 시드로 맵·러너 추첨 → NV 기록 → 라운드 시작/종료·타임아웃·Goal 판정(러너만) → 색 재매핑(`GameSessionColorDistribution.Distribute(list, slots, rng)` 재사용) → 게이트·체이서 리셋 → 2라운드 후 클리어 | Stage |
| ~~C5~~ | 플레이어 텔레포트 ✅ | Host → 전원 ClientRpc → 각자 Owner가 `NetworkTransform.Teleport()` + `rb.position` 동기화. `LoadingCurtain` 암전으로 가리고 카운트다운 동안 이동 잠금. `NetworkDesign.md` **§11.9 신설** | Network/Player |
| ~~C6~~ | `Stage5ChaserAI` ✅ | 타겟을 러너 1명으로 한정, 피격 시 정지 → Host Despawn | Stage |
| ~~C7~~ | `Stage5ChaserSpawner` / `Stage5DifficultyConfig` ✅ | 살아있는 수 유지(8/솔로 6), 6초 리스폰, 러너 30m 이상 스폰 지점, 3초 유예, 라운드 종료 시 `StopAndClear` | Stage |
| ~~C8~~ | `T5RunnerMarkerUI`(신규) ✅ | 2층 인원에게만 표시, NV 구독만 | UI |
| ~~C9~~ | `ObjectiveUI` / `TipUI` / `T5RunnerAnnounceUI`(신규) ✅ | 라운드·타이머 표시, 문구 교체 (**번역 10개 언어 미완** — 아래) | UI |
| C10 | 정리 | 폐기된 T5 흑백 전용 코드·Tip 정리, 옛 `T5_Maze` 삭제(승인 후) | — |

### C1~C9 구현 결과 (2026-09-18)

- `StageNetworkState`: `T5RoundState` 구조체 + `_openGateColor`(int) / `_t5Round` NV.
  읽기 `OpenGateColor` · `IsGateColorOpen(color)` · `T5CurrentRound/MapIndex/RunnerClientId` ·
  `IsT5Runner(id)` · `IsLocalPlayerT5Runner`, Host 쓰기 `SetOpenGateColor` · `CloseAllGates` ·
  `SetT5Draw` · `BeginT5Round`, 이벤트 `OnOpenGateColorChanged` · `OnT5RoundChanged`.
  라운드 값을 NV 하나로 묶은 이유는 `PhaseStartSignal`과 동일(도착 순서 미보장).
- `BlackWhiteDoorToggle` → **`ColorGateController`** (파일·클래스 rename, `.meta` guid 유지).
  맵 루트마다 1개. `doorGroups` 6색 + 하위 패드 자동 수집.
  `ApplySlotMapping(slotColors)` — 고유색 설계슬롯 4개(`DesignSlots` 순서 Blue/Purple/Green/Yellow)를
  안내자 실제 색으로 재매핑하고 `ColoredDoorVisual`·패드까지 갱신. Black/White는 고정.
  `RequestOpen(실제색)` / `CloseAll()` / `SnapAllClosed()`(라운드 리셋 — 연출 없이 닫힘 위치로 텔레포트).
  솔로면 전부 Open + 패드 숨김.
- `BlackWhiteTogglePad` → **`ColorGatePad`** (rename, guid 유지). `designColor` + 컨트롤러에서
  당겨오는 `EffectiveColor`. 고유색은 `isUniqueColor && playerColorType` 일치만, 흑·백은 누구나.
  매핑 SSOT는 컨트롤러 하나 — 패드는 당겨오기만 한다(푸시 순서 버그 방지).

- **`T5RunnerRoundDirector`**(신규, `StageObjective` 상속). `StageObjective`로 만든 이유는
  클리어·실패 경로를 새로 파지 않기 위해서다 — `Complete()`는 StageManager 클리어 판정에,
  `Fail()`(타임아웃)은 `KillAllPlayersOnFail()` → §11 사망 문 → 기존 리로드에 얹힌다.
  **러너 사망은 이 클래스가 안 잡는다** — `StageResetOnPlayerDeath`가 이미 모든 사망을 그 문으로
  보내고 있어 중복이다.
  추첨은 시드 salt 3종(맵/러너/색)으로 분리. 러너는 "clientId 정렬 → 시드 셔플 → 앞 2개" 한 규칙으로
  1인(같은 사람 2회)·2인(각 1회)·3~4인(서로 다른 2명)이 전부 커버된다.
  연출(맵 활성화·색 재매핑·문 스냅)은 NV + 시드에서만 나오므로 Host/Client가 같은 결과를 만든다.
  맵은 `mazesRoot` 하위 `Map_*`를 이름 순으로 자동 수집하고 각 맵의
  `ColorGateController`/`Start1F`/`Goal1F`/`Stand2F`/`Stage5ChaserSpawner`를 이름으로 자동 해석 —
  **인스펙터 배선은 `mazesRoot` 하나뿐**이다.
  Goal 판정은 트리거 컴포넌트를 새로 만들지 않고 `Goal1F` 마커와의 거리(`goalRadius`, 기본 4m)로 한다.

- **C5 텔레포트** (`NetworkDesign.md` §11.9 신설 — 이게 SSOT).
  진입점은 `StageNetworkState.BeginT5Transition()` 하나. Host가 목적지만 계산해 ClientRpc로 뿌리고,
  **각 플레이어의 Owner 머신이 자기 것만** `NetworkTransform.Teleport()`로 옮긴다(CNT는 Owner 권한).
  `transform.position` 단순 대입을 쓰지 않은 이유: CNT가 `Interpolate ✅`라 원격 화면에서 맵 사이
  수백 m를 미끄러지고, Host 비오너 레인의 `rb.MovePosition()`이 그 거리를 물리 이동으로 쓸어
  벽에 끼거나 터널링한다.
  연출은 `LoadingCurtain` 암전 → (덮인 상태에서) 텔레포트 → 페이드인. **라운드 시작 3초 카운트다운
  안에 들어가 추가 대기가 0초**다(T.Boss P1 교훈 준수).
  목적지: 러너 = `Start1F`, 안내자 = `Stand2F` + **시작 홀 색별 XZ 오프셋**((0,0,5)/(5,0,0)/(-5,0,0)/(0,0,-5)),
  둘 다 바닥에서 `dropHeight`(기본 3m) 위 — 색마다 자리가 달라 겹치지 않는다(사용자 확정).
  1라운드 진입(시작 홀 → Map A)도 **같은 코드**를 탄다.
- **이동 잠금**: `Player.SetMovementLocked(bool)` 신규. `IsDead`/`IsDowned`(사망 축 상태)와도,
  `isOwnerControlled`("이 복사본이 오너인가")와도 의미가 달라 별도 플래그로 뒀다. 수평 이동만 막고
  중력은 살려둬서 텔레포트 후 착지가 된다. 해제는 NV에서 로컬 계산 — **해제용 RPC 없음**.

> **순서 주의:** `AdvanceRound`는 `BeginRound` → `TeleportForRound` 순이다. NV를 먼저 써야
> 전 머신이 목적지 맵을 활성화하고, 0.35초 뒤 착지할 때 바닥이 이미 있다. 뒤집으면 비활성 맵으로
> 떨어져 바닥을 통과한다.

- **C6 `Stage5ChaserAI`**: 타겟을 러너 1명으로 한정. 러너 clientId는 스폰 시 받아두지 않고
  **매 retarget마다 NV(`T5CurrentRunnerClientId`)에서 다시 읽는다** — 라운드가 넘어가면 옛 러너를
  쫓게 되는데, 스포너의 정리 순서에 기대는 것보다 "타겟의 진실은 NV 하나"가 안전하다.
  러너가 죽었거나 은신(레이어 다름)이면 **대체 타겟 없이 정지**(구 동작인 "가장 가까운 생존자"는 폐기 —
  2층 인원이 체이서를 끌어당기면 미로가 성립하지 않는다).
  피격 시 `postHitStopDuration` 정지 후 **Host Despawn**. 정지 동안 `CanApplyDamage`가 막혀
  사라지기 전에 한 번 더 때리지 않는다.
- **C7 `Stage5ChaserSpawner`**: 최초 스폰 후 **살아있는 수를 유지**한다(Host 레인 `Update`).
  소멸분을 매 프레임 세어 **1마리당 예약 1개**를 큐(`_respawnDueAt`)에 넣는다 — 타이머 하나를
  돌려쓰면 같은 프레임에 2마리가 죽었을 때 두 번째가 `respawnDelay`의 2배만큼 늦게 나온다.
  리스폰 지점은 러너에서 `minRunnerDistance`(30m) 밖 후보 중 랜덤. 조건에 맞는 자리가 하나도
  없으면(러너가 맵 한가운데) 가장 먼 자리로 타협하고, 러너를 못 찾으면 거리 조건을 버린다 —
  어느 쪽이든 리스폰이 영영 멈추지는 않게.

- **C8 `T5RunnerMarkerUI`**(신규, UI). 씬에 **1개**면 된다 — 러너는 라운드마다 한 명이고
  마커 내용도 사람마다 다르지 않으므로, 프리팹에 붙일 이유가 없다(`PlayerNameTagUI`와 다른 점).
  표시 규칙은 "라운드 진행 중 && **내가 러너가 아님**" — 자기 머리 위 화살표는 시야만 가리고,
  이 규칙 하나로 솔로도 자동으로 안 보인다. 러너 Transform은 clientId가 바뀔 때만 다시 찾는다.
- **C9 UI 3종**
  - `ObjectiveUI`: `T5RunnerRoundDirector` 분기 추가 → `FormatCountClock`으로 "1/2 · 1:58".
    디렉터는 표시값(라운드 또는 **올림 초**)이 실제로 바뀔 때만 `OnProgressChanged`를 쏜다 —
    매 프레임 쏘면 같은 글자를 60번 다시 쓴다.
  - `T5RunnerAnnounceUI`(신규): "OO님이 러너" + 3·2·1. 값을 전부 NV에서 읽어 **알림용 RPC가 없다**.
    내가 러너면 이름 대신 "당신이 러너" 문구(자기 이름을 3인칭으로 읽는 건 어색하다).
    이름은 CheerName 고정값(`CheerService.GetCheerName`) — DisplayName 아님.
  - `TipUI`: `Tip.T.Stage5.1` 코드 폴백 교체 + `StageTipLines.md` 갱신.

> **찌꺼기 버그 1건 수정:** `ObjectiveUI.DisconnectPreviousSlots`가 `ColorTileRoundObjective`의
> `roundListener`를 해제하지 않고 있었다(`RoundProgressObjective`만 처리). Refresh가 반복되면
> 리스너가 계속 쌓인다. T5 분기를 넣으면서 같이 고쳤다.

> **주의:** rename으로 옛 `T5_Maze`(비활성)에 붙어 있던 두 컴포넌트의 인스펙터 배선
> (`blackDoorsRoot`/`whiteDoorsRoot`)은 필드 구조가 달라져 유실된다. `T5_Maze`는 어차피
> V단계 후 삭제 대상이라 그대로 뒀다.

**인스펙터/프리팹:**
- ~~문 NavMeshObstacle(carve)~~ — M단계 완료
- ~~`Stage5DifficultyConfig` 행(1인 6 / 2~4인 8)~~ — 스크립트 기본 테이블을 그 값으로 바꿨다.
  다만 **씬에 오브젝트 자체가 아직 없어 배치는 필요**하다(배치하면 기본값이 곧 이 값).
- **`Chaser.prefab` moveSpeed 27→8 + NavMeshAgent speed 25→8 — 여전히 미처리.**
  M단계에 이어 C단계에서도 MCP 공유 에셋 수정 권한이 거부됐다. 사용자가 인스펙터에서
  직접 하거나, MCP 툴 permission 규칙을 추가해야 한다.
- 맵별 `ColorGateController` 배치 + `doorGroups` 6색 루트 배선, `Pads2F` 패드 6개에
  `ColorGatePad` 부착 + `designColor` 설정 — C1·C2가 끝났으므로 이제 가능(아래 §3-W).
- `T5RunnerRoundDirector` 배치 + `mazesRoot` 배선, `StageManager.objectives` 등록 (아래 §3-W).

## 3-W. 배선 체크리스트 (에디터 — 사용자 작업)

> **W1~W5는 2026-09-18 완료.** MCP 읽기로 실물 검증했다(결과는 §7). 아래 내용은 재작업·복구용 기록.
> **남은 것은 W7(UI) 하나뿐이다.**

### ~~W1~~ ✅ 맵마다 `ColorGateController` (7회)
1. `StageManager5/T5_Mazes/Map_XX` **루트**에 `ColorGateController` 추가.
2. `doorGroups` 크기 6, 각 원소의 `designColor` / `root`:
   | # | designColor | root |
   |---|---|---|
   | 0 | Blue | `Map_XX/Doors/Blue` |
   | 1 | Purple | `Map_XX/Doors/Purple` |
   | 2 | Green | `Map_XX/Doors/Green` |
   | 3 | Yellow | `Map_XX/Doors/Yellow` |
   | 4 | Black | `Map_XX/Doors/Black` |
   | 5 | White | `Map_XX/Doors/White` |
3. `pads`는 **비워둘 것** — Awake에서 하위 `ColorGatePad`를 자동 수집한다.

### ~~W2~~ ✅ 맵마다 색 패드 6개 (7회)
`Map_XX/Pads2F`의 패드 6개 각각:
1. `ColorGatePad` 추가 (`Collider`는 `isTrigger`가 Awake에서 강제되므로 체크 여부 무관, 단 **Collider는 있어야 함**).
2. `designColor`를 그 패드의 색으로 (Blue/Purple/Green/Yellow/Black/White 각 1개).
3. `controller`는 **비워둘 것** — 부모에서 자동 탐색한다.
4. 고유색 4개에는 M단계에서 붙인 `ColoredPadVisual`이 그대로 있어야 한다(런타임 색 재매핑용).

### ~~W3~~ ✅ 문 설정 확인 (맵마다 15개)
- `DoorController.openMode = SlideUp`
- **`latchOnOpen = false`** ← 래치가 켜져 있으면 한 번 열린 문이 다시 닫히지 않아 색 게이트가 깨진다. 가장 놓치기 쉬운 항목.
- `requiredPads`는 **비어 있어야 한다** (이 문들은 압력 발판이 아니라 게이트가 직접 연다).

### ~~W4~~ ✅ 라운드 디렉터 (1회)
1. `StageManager5` 하위에 빈 GameObject `T5RoundDirector` 생성 → `T5RunnerRoundDirector` 추가.
2. `mazesRoot` ← `StageManager5/T5_Mazes` **(배선은 이것 하나뿐)**.
3. 타이밍 기본값 확인: `introSeconds=3` / `roundSeconds=120` / `chaserGraceSeconds=3` / `goalRadius=4`.
4. `StageManager5`의 `StageManager.objectives`에 이 컴포넌트를 등록.
   (비워두면 `GetComponentsInChildren`로 자동 수집되지만, 기존 T5 Objective가 남아 있으면
   그것들도 같이 클리어 조건에 들어가므로 **옛 목표는 제거**할 것.)

### ~~W5~~ ✅ 체이서
- 맵마다 `Map_XX` 하위에 `Stage5ChaserSpawner`를 두고 `spawnPoints`에 `ChaserSpawns` 16개를 넣으면
  디렉터가 자동으로 찾아 쓴다. 없으면 체이서 없이 라운드만 돈다(에러 아님).
- `Stage5DifficultyConfig`는 씬 루트에 빈 GameObject로 1개 배치(기본 테이블이 이미 1인 6 / 2~4인 8).

### W7. UI — **미완, 다음 작업**
1. 빈 GameObject에 **`T5RunnerMarkerUI`** 1개. 배선 없음(텍스트는 코드가 만든다).
   `offset` 기본 3.4 — `PlayerNameTagUI`(2.2) 위로 뜬다.
2. HUD에 **`T5RunnerAnnounceUI`** 1개. `messageText` / `countdownText`를 물리고,
   **`introSeconds`를 디렉터와 같은 3으로** 맞출 것(두 값이 어긋나면 알림이 일찍 사라지거나 남는다).
   `runnerMessage`(`{0}` = 러너 이름) / `youAreRunnerMessage`는 로컬라이즈 키를 만든 뒤 물린다 —
   **비워두면 한국어 폴백**으로 동작하므로 지금 당장 Play 하는 데는 지장 없다.
3. `ObjectiveUI`는 `StageManager.objectives`를 그대로 읽으므로 **추가 배선 없음**.

### W8. 로컬라이제이션
- `Tip.T.Stage5.1`: **13개 로케일 전부 새 문구로 교체 완료**(2026-09-18).
  `StageTip_*.asset` 실제 에셋 + `TipUI` 코드 폴백 + `StageTipTranslations.md` 전부 일치.
  ko/en 외 11개는 **기계번역이라 원어민 검수 전** — Steam AI 표기 검토 대상.
  > 주의: `TipUI.ResolveText()`는 **String Table을 먼저 읽고** 테이블이 없을 때만 코드 폴백을 쓴다.
  > 즉 화면에 뜨는 건 `.asset` 쪽이다 — 코드 폴백만 고치면 아무것도 바뀌지 않는다.
- **러너 알림 2문구는 아직 테이블 키가 없다** (`{0}님이 러너입니다` / `당신이 러너입니다`).
  `T5RunnerAnnounceUI`의 LocalizedString 2개를 비워두면 한국어 폴백으로 동작하므로 급하지 않다.

### W9. 아직 하지 말 것 (구 W6)
- 옛 `T5_Maze` 삭제 — **V단계 통과 후** 사용자 승인. 지금은 비활성 상태로 둔다.
  (rename 여파로 거기 붙은 `ColorGateController`는 `doorGroups` root가 전부 NULL이다 — 정상, 어차피 폐기 대상)

## 4. V단계 — 검증 (ParrelSync / 로컬 빌드 2개)

**인원별**
- 1인: 문 전부 열림, 2층 패드 숨김, 체이서 6, 2라운드 모두 본인, 러너 마커 안 보임
- 2인: 러너 교대, 고유 4슬롯 = 안내자 색(패드 1개가 고유색 문 8개를 함께 엶 — **정상**), 패드 권한, 문 개폐가 Host/Client 동일

**라운드 전환 (C4·C5 — 가장 새롭고 가장 위험한 구간)**
- 시작 홀 → Map A 진입도 2라운드 전환과 **같은 경로**를 타는지
- 암전이 텔레포트 순간을 실제로 가리는지 (커튼 전에 순간이동이 보이면 `coverFadeSeconds` 부족)
- 러너 = Start1F, 안내자 = Stand2F에 **색별로 안 겹치게** 착지 (dropHeight 3m)
- Client 러너가 튕김·워프·벽 끼임 없이 이동 (`NetworkTransform.Teleport` 동작 확인)
- 카운트다운 3초 동안 이동 잠금 → 끝나는 순간 Host/Client **동시에** 풀림
- **목적지 맵이 착지 전에 활성화돼 있는지** (바닥 통과 = `BeginRound`/`TeleportForRound` 순서 깨짐)

**체이서 (C6·C7)**
- 러너만 추격, 2층 안내자 완전 무시
- 피격 후 소멸 → **6초 뒤** 러너에서 30m 밖 리스폰, 살아있는 수 유지
- 같은 프레임에 2마리가 죽어도 **둘 다 6초 뒤** 나오는지(큐 동작)
- 닫힌 문에 막힘 / **열린 문은 통과** ← §2-R 미확인 항목(carve 해제). 안 되면 `DoorController.OnOpened`에서 `NavMeshObstacle.carving`을 끄는 처리 추가
- Client 위치 일치

**실패·클리어**
- 타임아웃 120초 → 리로드 → 맵·러너 **재추첨**(같은 조합이 반복되지 않는지)
- 러너 사망 → 리로드 (`StageResetOnPlayerDeath` 경로)
- 2라운드 Goal → StageManager 클리어 → 다음 씬

**UI (C8·C9)**
- `ObjectiveUI` "1/2 · 1:58" 갱신, 라운드 넘어갈 때 2/2
- 러너 마커가 **안내자에게만** 보이고 러너 본인에겐 안 보임
- 러너 알림이 3초간, 내가 러너일 땐 "당신이 러너"
- Tip 문구가 새 러너 문구로 나오는지 (ko 외 1개 언어도 확인)

**카메라**
- 러너 카메라가 벽(높이 15) 위로 넘어가지 않음 (maxPitch/initialPitch 40)

## 5. D단계 — 문서 갱신 (사용자 확인 후)
- `CoopStageAudit.T.md`: T5 "보류·재설계 금지" 해제 → 이 문서 링크 — **미완**
- `TStageNetworkBoard.md` §3.2: 러너 한정 타겟·소멸·리스폰 반영 — **미완**
- ~~`NetworkDesign.md` §11: 라운드 중 텔레포트 칸(C5)~~ → **§11.9로 신설 완료(2026-09-18)**
- ~~`StageTipLines.md` / `StageTipTranslations.md`~~ → **갱신 완료(2026-09-18)**
- 에이전트 메모리 `project_t5_maze_rules` → 이 문서를 SSOT로 가리키고 있어 그대로 유효

## 6. 보류 / 범위 밖
- 다운 제거·부활 유지 리팩토링 (맵 완성 후)
- T.Boss P1 Pioneer 교체, T.Stage4 함정 랜덤화 (T5 다음 — 순서: T5 → T.Boss P1 → T4)
- T5 응원(Cheer) 연동

## 7. 다음 세션 시작점 (2026-09-18 C단계 완료 시점 기준)

### 지금까지 끝난 것

- **M단계** — 맵 7개 생성·씬 배치 (§2-R)
- **C단계 코드 C1~C9 전부** (§3 표 + "C1~C9 구현 결과")
- **배선 W1~W5** — 맵/게이트/패드/문/디렉터/체이서. **MCP 읽기로 실물 검증했다:**
  맵 7개 각각 게이트 6묶음(문 2/2/2/2/4/3=15)·패드 6(Collider·Visual 정상)·스폰 16,
  문 105개 전부 `latchOnOpen=false`·`requiredPads` 비어 있음·`SlideUp`,
  디렉터 `mazesRoot=T5_Mazes`이며 `StageManager5.objectives`의 **유일한** 항목,
  `Stage5DifficultyConfig` 배치(1→6/2~4→8), `Chaser.prefab` moveSpeed=8·NavMeshAgent speed=8,
  옛 `T5_Maze` 비활성.
- **로컬라이제이션** — `Tip.T.Stage5.1` **13개 로케일 전부** 새 문구 (§3-W W8)

### 다음에 할 것 (순서대로)

1. **W7 — UI 배선 2개** (§3-W W7). 마커 1개(배선 없음) + 알림 1개(텍스트 2개 연결,
   `introSeconds`를 디렉터와 같은 3으로). 로컬라이즈 필드는 비워도 한국어 폴백으로 동작한다.
2. **V단계 검증** (§4). ParrelSync 2인 또는 로컬 빌드 2개.
   → 라운드 전환(C4·C5)이 가장 새롭고 위험한 구간이니 거기부터 본다.
3. **C10 정리** — 옛 `T5_Maze` 삭제(승인 후), 폐기된 흑백 전용 잔재 확인
4. **D단계 문서** (§5)

### 발 헛디디기 쉬운 곳 (전부 실제로 밟았거나 밟을 뻔한 것)

- **`TipUI`는 String Table을 먼저 읽는다.** 코드 폴백(`TipUI.cs` `KoreanFallback`)만 고치면
  화면은 하나도 안 바뀐다. 실제로 이 함정에 빠져 "ko 갱신했다"고 잘못 보고한 적 있다.
- **`AdvanceRound`는 `BeginRound` → `TeleportForRound` 순서여야 한다.** 뒤집으면 아직 비활성인
  맵으로 떨어져 바닥을 통과한다.
- **`ColorGateController.SnapAllClosed()` → `ApplySlotMapping()` 순서.** 스냅이 "적용 완료" 표식을
  세우므로 재매핑이 뒤에 와야 다음 프레임에 새 매핑으로 수렴한다.
- **2인일 때 패드 1개가 고유색 문 8개를 동시에 엶 = 정상.** §1.2 "4슬롯 전부 안내자 색"이고
  M단계 BFS도 "2인 = 실질 3색" 전제로 통과시켰다. 버그로 오해하지 말 것.
- **`NetworkTransform.Teleport()`는 권한(Owner) 인스턴스에서만** — 아니면 예외를 던진다.
  `transform.position` 단순 대입은 금지(§`NetworkDesign.md` 11.9에 이유 전부 기록).
- **NavMesh 재베이크 시** `Doors`·`Pads2F`를 일시 비활성화해야 한다 (§2-R — `NavMeshModifier`가 안 먹힘).

### 남은 미확인·미완

- **열린 문이 NavMeshObstacle carve를 해제하는지** — M단계에서 판정 불가, V단계로 넘김.
  해제가 안 되면 `DoorController.OnOpened`에서 `carving`을 끄는 처리가 필요하다.
- **`Chaser.prefab` `postHitStopDuration`이 2초** — 예전엔 "맞고 멈췄다 재추격"이라 말이 됐지만
  지금은 **소멸까지의 연출 길이**다. 0.4~0.6 권장(무해하지만 길게 느껴짐).
- **러너 알림 2문구 테이블 키 없음** (§3-W W8). 폴백으로 동작하므로 급하지 않다.
- ko/en 외 11개 Tip 번역은 **기계번역, 원어민 검수 전** — Steam AI 표기 검토 대상.
- `Goal1F` 판이 기존 Goal 재질(노랑)이라 **노랑 문과 혼동 여지** (§2-R).

### 작업 방식 메모

- 에디터 상태는 사용자에게 넘기지 말고 **MCP 읽기로 직접 확인**해 보고한다
  (`execute_code`로 `SerializedObject`를 덤프하는 방식이 이번에 잘 통했다).
- 공유 에셋(프리팹·씬) **쓰기**는 auto mode 분류기에 막힐 수 있다. 막히면 우회하지 말고
  정확한 수정 지점을 사용자에게 넘긴다.
6. 맵을 다시 뽑아야 하면 `Tools/T5MazeGen/` 사용. NavMesh 재베이크 시 `Doors`·`Pads2F` 일시 비활성화 필요(§2-R).
