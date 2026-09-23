# T.Stage3 구간 데드라인 (구간 시계) 계획서

**상태(2026-09-23):** 설계 ✅ · **코드 ✅**(`SegmentTimer` / `SegmentTimerSign` / `StageNetworkState` 슬롯) ·
**씬 배치 ✅**(구역 5개 · 판정 볼륨 5개(구간 전체) · 위액 수면 5개 · 간판 5개 · 정리 배선) ·
**남은 것: 실플레이 검증 · `wallIntervalMin/Max` 적용(§6)**

**이 문서가 T.Stage3 구간 데드라인 SSOT.** 기존 T3 설계(`CoopStageAudit.T.md` §H.2·§3)는 **그대로 유효**하다 —
조임(`EsophagusSqueeze`) 복습, 전원 외침 원상 복구, ColorWall 흑백 초출은 건드리지 않았다.
이 문서는 거기에 **시계 축만 추가**한다.

**연관 SSOT:** 부활·사망은 `ReviveSystemDesign.md` · 함정 네트워크 이력은 `TrapNetworkBoard.md` ·
감사 보드는 `CoopStageAudit.T.md`

---

## 0. 한 줄 요약

1300m 통로를 **5구간으로 나누고 구간마다 제한 시간**을 준다.
시간 안에 못 나가면 **그 구간 전체에 위액이 차올라** 남아 있는 동안 피를 흘린다.
쫓아오는 물체는 없다 — **쫓기는 감각은 시계가 만든다.**

---

## 1. 왜 넣는가 (2026-09-23 진단)

"달리기만 해서 밋밋하다"의 원인은 함정 부족이 아니었다. 씬 값을 재보니 **위협이 전부 "왔다가 되돌아가는" 형태**고
**시계가 아예 없었다.**

| 항목 | 측정값 | 결과 |
|---|---|---|
| 통로 길이 / 기본 속도 | 1290m / 10m/s (`Kkultteok.prefab` speed 10 × runMultiplier 1) | 순수 달리기 130초 |
| 조임 `EsophagusSqueeze` | `randomIntervalMin/Max = 46~58초`, 링 1개(z 813 고정) | 한 판에 창이 2번 뜰까 말까 |
| 옆벽 `WallLineRandomizer` | `wallRetreatRatio: 1` = **순전진 0**, 10~15초마다 30m 전진 → 1초에 완전 복귀 | 10번 넘게 움직여도 **영구히 좁아지는 건 0m** |
| ColorWall | `pauseDuration: 0.1` (스크립트 기본값 2f) | 색을 맞춰도 0.1초 멈춤 = 인과를 배울 수 없음 |
| 볼더 | 4레인, 2~5초 간격, 30m/s, z 1360→0 **정면** | 직선 + 안개 없음 → 수 초 전부터 보임 |
| 목표 | `ReachZoneObjective` 하나. `SurviveTimeObjective` 없음 | **제한 시간 0** — 천천히 가도 손해가 없다 |
| 부활 | 1초 뒤 가장 가까운 생존자 위치(`ReviveSystemDesign.md` §92) | 뒤처져 죽으면 오히려 앞으로 이동 |

→ **되돌릴 수 없는 것이 하나도 없고, 서두를 이유가 없었다.**

## 2. 안 택한 대안과 그 이유

**이 표는 판단 근거 기록이지 잠금이 아니다.** "다시 묻지 말 것" 수준의 잠금은 `CoopStageAudit.T.md` §H.2가 SSOT다 —
여기 항목을 그쪽 잠금으로 올리려면 사용자 확인을 받을 것.

| 안 | 기각 사유 |
|---|---|
| **뒤에서 쫓아오는 볼더(상시)** | 볼더는 이미 정면 4레인으로 있음 = 방향만 바꾼 중복. 멈춰서 처리하는 T3의 두 수업(ColorWall 색 멈춤 · 조임 Hold 중 외침)을 무력화. 체이서 초출은 T5(`TStage5RunnerRedesign.md`) |
| **`MovingCorridor` 뒤 벽** | 기술적으로는 최적(틱 결정론 동기화 완료). 그러나 T4 정체성이라 T3에 넣으면 T4가 복습이 됨 |
| **`BreakTile` 낙사** | ① T4·T.Boss P1 카드 ② 즉사 → 생존자 위치 부활이라 압박이 오히려 사라짐 ③ `Ground`가 통짜 2장이라 타일화 비용이 큼 |
| **함정을 뒤에서부터 순차 상승** | 오브젝트 개수는 동시 발동과 같다(제어 방식 차이일 뿐). 1300m를 수백 개로 까는 것과 다를 바 없어 폐기 — **구간마다 한 번에 덮는다** |
| **`SpikeTrap` 사용** | 첫 배치는 `SpikeTrapAcid` 20개였으나 **연출이 기존 Spike와 겹쳐** 폐기. `ContactDamage` + 가스로 교체(2026-09-23) |
| **8구간 유지** | 2·3·4구간이 10~13초라 간판을 읽을 틈이 없음. 5구간으로 묶음(§3) |
| **누적형 시계(StageStartGate 기준 절대 시각)** | 전파가 공짜(기존 `StageStartServerTime`)라는 장점이 있었으나, 구간 독립형이 긴장감이 더 크고 비용 차이는 스크립트 1개 수준이라 독립형 채택 |

## 3. 구간 확정 **[확정 2026-09-23]**

경계는 새로 만들지 않고 **지형 경계(PushWay 구간 · 점프 시작/끝)에서만** 골랐다.

| 구역 | z 범위 | 길이 | 실측(숙련자 1인) | 제한 | 배수 | 요구 평균속도 |
|---|---|---|---|---|---|---|
| A | 12 → 295 | 283m | 33초 | **50초** | ×1.5 | 4.0 m/s |
| B | 295 → 595 | 300m | 25초 | **50초** | ×2.0 | 6.0 m/s |
| C | 595 → 905 | 310m | 70초 | **160초** | ×2.3 | 1.9 m/s |
| D | 905 → 1068 | 163m | 30초 | **45초** | ×1.5 | 3.6 m/s |
| E | 1068 → 1180 | 112m | 20초 | **30초** | ×1.5 | 3.7 m/s |
| — | 1180 → 1318 | 138m | 15초 | **없음** | | 골 직전은 숨 돌리는 구간 |
| 합 | | 1306m | 193초 | **335초** | | |

**실측 조건:** 사용자(개발자·최숙련) 1인, 길을 전부 아는 상태, **속도 버프를 쿨마다 계속 사용.**
실효속도가 2구간 12.7 m/s · 4구간 13.2 m/s로 **기본 속도 10을 넘는다** → 이 시간은 버프 없이는 불가능한 바닥값이다.

**B만 배수가 다른 이유:** 300m를 버프 없이 직선 완주하면 30초다. ×1.5(38초)면 함정 처리 여유가 8초뿐인데
그 구간에 볼더 4레인·ColorWall 30m 전진·Spike·Thron·점액·넉백이 다 들어 있다. 회피는 옆으로 도는 거라
실주행 거리가 더 길어서 실제 여유는 3~6초로 줄어든다 — 초보 4인은 통과 불가로 판단해 A와 같은 50초로 맞췄다
(길이도 283m/300m로 거의 같다).

**C가 관대한 이유:** 점프 구간이라 실패 재시도 분산이 제일 크다. 못 넘으면 175초(×2.5)까지 열어둔다.

**8구간 → 5구간 묶음:** A=원래 1+2, B=3+4, C=5, D=6, E=7, 8구간은 버림.
원래 경계 z는 168 · 295 · 423 · 595 · 905 · 1068 · 1180 · 1318이었다.

## 4. 판정과 연출 **[확정]**

| 축 | 결정 |
|---|---|
| 판정 | **`ContactDamage`** (`SpikeTrap` 아님). damage 1, damageInterval 1 |
| 왜 즉사가 아닌가 | 지속 데미지면 **아프지만 뚫고 갈 수 있다** — 뒤처짐의 대가가 "즉사 후 리셋"이 아니라 "피 흘리며 따라붙기"가 된다. 낙사 즉사는 부활(생존자 위치)이 곧 전진이라 압박이 사라진다 |
| 덮는 범위 | **구간 전체**(§3 경계 z에 정확히), 통로 전폭. 판정 볼륨 **구간당 1개**(40 × 6 × 구간 길이). 2026-09-23 "끝 40m"에서 변경 — **구간 전체가 위험하다는 걸 보여야** 뒤처짐이 읽힌다 |
| 연출 | **위액 수면** `Assets/Art/Particle/GastricAcidPlane.prefab` **구간당 1개** (2026-09-23 확정) |
| 수면 원본 | `SalivaWaterPlane`의 **프리팹 Variant** + 복제 머티리얼 `GastricAcidPlane.mat`(탁한 황록, 알파 0.62, 약한 연두 Emission, 노멀맵·굴절 유지). **원본 머티리얼은 M.Stage2·M.Boss가 쓰므로 손대지 않는다** |
| 크기 | Plane(10m) × `(3.8, 1, 구간 길이/10)` = 판정 범위와 일치. 높이 y 0.65(바닥 윗면 0.5). **보이는 곳 = 아픈 곳** — 수면은 경계가 선명해 이 원칙을 지킨다 |
| 차오름 | `SegmentAcidRise`: 시간 초과 **3초 전**부터 바닥 1m 아래에서 차올라 **0초에 제자리** → 그 순간 `activateOnTimeout`이 `ContactDamage`를 켠다. 차오르는 3초 동안은 데미지 없음 |
| 가스 폐기 | 초기 `PoisonGas` 가스(구간당 1개)는 **폐기**(2026-09-23) — 잘 안 보이고, 잘 보이게 하려면 파티클 비용이 커서 |

**C구간만 높이가 다르다.** `Ground`가 z 595~905에서 끊기고 그 사이는 쿠키 튕김 발판(`ContactKnockback` VerticalUp, 힘 10,
윗면 전부 y 0.8)만 있다. 판정을 **y 0.3~1.5 얇은 띠**로 두어 **쿠키를 밟는 순간만 맞고, 튕겨 떠 있는 동안은 안 맞는다**
(튕김 최고점 ≈5m). 수면은 y 0.95 한 장 — 쿠키 사이 구멍 위도 덮여 "위험한 바닥"처럼 보이는데 **의도대로 확정**(2026-09-23).
(이전안: 착지 지점 z 905~945만 판정 → 구간 전체 덮기로 바뀌며 폐기.)

## 5. 정리(끄기) 규칙 **[확정]**

지나온 구간의 가스·판정을 계속 켜 둘 이유가 없다.

| 시점 | 끄는 것 |
|---|---|
| `Segment.C` 시계 시작 | `Hazard.A` + `AcidSurface.A` + **`SpikeLaneField`** |
| `Segment.D` 시계 시작 | `Hazard.B` + `AcidSurface.B` |
| `Segment.E` 시계 시작 | `Hazard.C` + `AcidSurface.C` |

- 배선은 `SegmentTimer.onTimerStarted` UnityEvent → 대상 `GameObject.SetActive(false)`. **코드 0줄.**
- `Hazard.X`와 **`AcidSurface.X`를 같이** 끈다(수면은 차오르는 3초를 보여줘야 해서 Hazard 밖에 있다).
- **`SpikeLaneField`는 D가 아니라 C에서 끈다** — 가시 390개가 전부 z 12.8~539.8에 있어 C 진입(595) 시점엔
  이미 전부 뒤다. 가시마다 `SpikeBurst` 파티클이 달려 있어 이 씬에서 제일 무거운 덩어리다.
- **알고 넘어갈 것:** A를 끄면 A에 남아 있던 사람의 데미지도 사라진다. 2구간 뒤라 이미 죽어서 생존자 옆으로
  부활했을 상황이라 실질 문제는 없다고 판단했으나, "뒤처진 사람 압박"이 거기서 끊긴다는 건 의도된 결과다.

## 6. 같이 합의된 값 (아직 씬에 미적용)

| 값 | 현재 | 합의 | 상태 |
|---|---|---|---|
| `WallLineRandomizer.wallIntervalMin/Max` | 10~15초 | **6~9초** | 사용자 동의(2026-09-23), **미적용** |
| `ColorWall.pauseDuration` | 0.1초 | 2초(스크립트 기본값) 제안 | **미정** — 결정 안 남 |
| `EsophagusSqueeze.randomIntervalMin/Max` | 46~58초 | **그대로 유지** | 확정 — 음성이라 자주 요구하면 안 됨 |

**참고 — ColorWall을 못 멈추면 어디까지 오나:** 벽 안쪽면 |x|≈20(구 BoxCollider는 center ±23 / size 12 → 17,
`PlaytestLog.md` #6의 MeshCollider 교체로 ≈20). `wallAdvance 30`을 5초에 걸쳐 오므로(6m/s) 벽 면이
**중앙선을 10m 지나 x=−10까지** 온다. 반대편 벽 앞 10m 띠로 몰려야 산다. 복귀는 1초(30m/s).
두 벽은 `WallLineRandomizer`가 시드에 `_netIndex`를 섞어서 동시에 오지 않는다. 벽이 z 1300 전체를 덮는 판이라
**앞으로 달려서 벗어날 수 없다** — 옆으로 피하는 수밖에 없다.

## 7. 코드 **[구현 완료]**

### 7.1 `Assets/Scripts/Stage/SegmentTimer.cs` (신규)

구역당 1개. 이게 전부다.

| 인스펙터 | 뜻 |
|---|---|
| `segmentIndex` | `StageNetworkState` 슬롯 index. 0,1,2,3,4 |
| `duration` | 이 구역 제한 시간(초) |
| `activateOnTimeout` | 시간 초과 시 켤 오브젝트. 하위 `TrapBase`는 켠 뒤 `Activate()`까지 호출 |
| `onTimerStarted` / `onTimeout` | UnityEvent (정리 배선·연출용) |
| `Remaining01` / `RemainingSeconds` / `HasStarted` / `HasFired` | 간판이 읽는 값 |

- 이 오브젝트의 Is Trigger 콜라이더에 플레이어가 **처음** 들어오면 시계 시작. `StartTimer()`가 public이라
  다른 UnityEvent(StageStartGate 등)에 걸어도 된다.
- **한 번 시작한 시계는 통과 여부와 무관하게 계속 간다.** 앞선 사람이 다음 구역으로 넘어가도 이 구역에 남은
  사람에게는 시계가 그대로 흐른다 — 뒤처짐에 대한 압박이 이 설계의 핵심이다.
- 시작 시각은 **로컬에 캐시**한다. 캐시하지 않으면 다음 구역이 슬롯을 갱신할 때 앞 구역 시계가 흔들린다.

### 7.2 `Assets/Scripts/UI/SegmentTimerSign.cs` (신규)

`SegmentTimer`가 계산해 둔 남은 시간을 읽어 **그리기만** 한다. 판정·시각을 소유하지 않는다.
표시 방식은 `SideSplitWorldDisplay`와 동일 — 채움 Renderer의 머티리얼 `_Cutoff`를 올려 한쪽부터 잘라내고
`_BaseColor`로 색을 바꾼다(`MaterialPropertyBlock`). 마지막 `warnSeconds`(기본 5초)는 경고색,
시간 초과 순간 실패색으로 꽉 참. 숫자(TMP 3D)는 선택.

`SideSplitWorldDisplay` 클래스 자체는 `SideSplitChallenge`를 구독해서 재사용 불가 — **프리팹·머티리얼만 재사용**한다.

### 7.3 `Assets/Scripts/Network/StageNetworkState.cs` (슬롯 추가)

```
NetworkList<double> _segmentStartTimes   // index = 구역, 값 = 시계 시작 서버 시각(0 = 미시작)
public double GetSegmentStartTime(int index)
public void   MarkSegmentStart(int index)   // Host 전용. 이미 시작했으면 무시. 슬롯은 필요한 만큼 늘림
```

- **새 RPC 0개.** Host가 트리거를 판정해 시작 서버 시각을 찍으면 전 머신이 그 값을 읽어 **각자 로컬로** 센다 →
  카운트다운 숫자가 전원 동일하다.
- **왜 NV인가:** "이 구역 시계가 언제 시작했나"는 일회성 이벤트가 아니라 지속 상태다(`NetworkDesign.md` §9).
  ClientRpc로 보내면 수신 준비가 안 된 Client에게 값이 영구 유실돼 카운트다운이 아예 안 돈다.
- **왜 구역마다 슬롯이 따로인가:** 다음 구역이 시작해도 앞 구역 시각이 덮이면 안 된다(§7.1 마지막 줄).
- **왜 `SegmentTimer`가 `NetworkObject`가 아닌가:** 씬 배치 `NetworkObject`는 `OnEnable()`이 NGO 스폰 처리보다
  먼저 돌아 `IsServer` 가드에 걸리는 레이스가 있다 — **이 씬의 `BoulderSpawnManager`가 실제로 그 사고를 냈다**
  (`TrapNetworkBoard.md` §7). 상주 릴레이인 `StageNetworkState`에 슬롯을 두면 그 레이스가 없다.
- 데미지는 `ContactDamage`가 Host에서만 적용한다(`!IsServer` 리턴 → `NetworkDamageUtil.ApplyDamage`).
  클라이언트에서 가스가 켜지는 건 연출뿐이라 타이밍이 수십 ms 갈려도 판정이 갈라지지 않는다.

### 7.4 `ContactDamage` 쿨다운 — 플레이어별로 수정 (2026-09-23)

원래 `_nextDamageTime`이 **컴포넌트별**이라 볼륨 하나에 4명이 들어가면 1초에 한 명만 맞았다(그래서 한때 볼륨을 4개로 쪼갰다).
**`Dictionary<Player, float>` 플레이어별 쿨다운으로 고쳤다** — 공용 컴포넌트라 이 수정은 쓰는 곳 전부에 적용된다
(여럿이 동시에 닿으면 각자 간격마다 맞는다). 비활성화 시 기록을 비운다. 그래서 구간당 볼륨 1개로 합쳤다.

### 7.5 `Assets/Scripts/Stage/SegmentAcidRise.cs` (신규)

수면 연출 전용. `SegmentTimer.RemainingSeconds`만 읽어 `riseSeconds`(3) 전부터 `riseDepth`(1m) 아래에서 차오르고
시간 초과 순간 제자리. 판정은 소유하지 않는다.

## 8. 씬 구성 (T.Stage3, 배치 완료)

씬 루트 `SegmentDeadlines` 아래:

```
SegmentDeadlines
├─ Segment.A   trigger z=30    dur 50    → activateOnTimeout = [Hazard.A]
├─ Segment.B   trigger z=295   dur 50    → [Hazard.B]
├─ Segment.C   trigger z=595   dur 160   → [Hazard.C]   + onTimerStarted: Hazard.A·AcidSurface.A off, SpikeLaneField off
├─ Segment.D   trigger z=905   dur 45    → [Hazard.D]   + onTimerStarted: Hazard.B·AcidSurface.B off
├─ Segment.E   trigger z=1068  dur 30    → [Hazard.E]   + onTimerStarted: Hazard.C·AcidSurface.C off
├─ Hazard.A~E  (비활성 대기)
│    └─ Acid.X.0     BoxCollider(Is Trigger) 40×6×구간길이 + ContactDamage(1 / 1초)   ※ C만 y 0.3~1.5 띠
├─ AcidSurface.A~E  (활성 — Renderer만 숨김) GastricAcidPlane 인스턴스, y 0.65(C 0.95), scale (3.8,1,구간길이/10)
│                   SegmentAcidRise(timer = Segment.X, riseSeconds 3, riseDepth 1)
└─ Sign.A~E   z = 295 / 595 / 905 / 1068 / 1180, y=14, 2배(20×11.4)
     ├─ SignFace.X / SignFill.X / SignCount.X   (M.Stage2 SplitZoneRig에서 복사)
     └─ SegmentTimerSign (timer / timerFills=SignFill / secondsText=SignCount / warnSeconds 5)
```

- **트리거 박스:** 40(폭) × 12(높이) × 2(두께), 통로를 가로지른다.
- **간판은 구역 끝(다음 체크포인트)에 선다.** 구역 시작에 두면 지나가는 순간 등 뒤로 사라진다.
  Quad 노멀이 −z라 회전 0으로 달려오는 쪽을 본다. y=7·원본 크기로는 PushWay 블록에 가려서 y=14 / 2배로 올렸다.
- **판정 범위 = 구간 전체:** A z 12~295 · B 295~595 · C 595~905 · D 905~1068 · E 1068~1180.

**주의 — 씬의 연출 오브젝트는 반드시 프리팹 인스턴스로 둘 것**(당시 가스, 현재 수면). `Object.Instantiate`로 복제하면 프리팹 링크가 끊겨
파티클 데이터가 통째로 씬에 박힌다 — 실제로 그렇게 했다가 씬 파일이 **77,466줄** 늘어났고,
`PrefabUtility.InstantiatePrefab`으로 다시 만들어 5,121줄로 줄였다.

## 9. 남은 것

1. ~~가스 색 틴트~~ → 위액 수면으로 교체 완료(§4, 2026-09-23)
2. **실플레이 검증** — ParrelSync 2인 또는 빌드 2개. 특히 ① 카운트다운이 전원 동일한가 ② 시간 초과 순간이
   전 머신에서 같이 오는가 ③ (삭제 — ⑥으로 대체) ④ 수면이 3초 전부터 차올라 0초에 멈추고
   그때부터 데미지가 들어오는가 ⑤ PushWay·빵 장애물 밑에 수면이 비치는 곳이 어색하지 않은가
   ⑥ C: 쿠키를 밟을 때마다 맞고 떠 있는 동안은 안 맞는가 ⑦ 여럿이 같은 볼륨에 있을 때 각자 맞는가
3. **§6의 미적용 값** — `wallIntervalMin/Max` 6~9초 적용, `ColorWall.pauseDuration` 결정
4. 제한시간 재조정 — 4인 플레이 후. 제일 먼저 흔들릴 값은 **C(160초)**
