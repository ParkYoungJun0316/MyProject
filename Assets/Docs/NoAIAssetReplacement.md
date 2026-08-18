# NoAI 에셋 교체 (Meshy AI → 비-AI) — 작업 현황

> 2026-08-19 기준. 루트의 `meshy_folders.txt`(임시 체크리스트)를 이 문서로 정리·대체. 앞으로 진행 상황은 이 문서에 갱신할 것.

## 목적 (반드시 유지)

**스팀(Steam)의 AI 생성 콘텐츠 공개(AI Disclosure) 의무를 피하기 위해**, 기존에 Meshy AI로 생성했던 모델·텍스처를 전부 비-AI 소스(Blender 직접 제작 + 구매/무료 에셋팩)로 교체하는 작업이다. 데모용 임시 조치가 아니라 **정식 출시본 전체에 적용**되어야 한다(`AGENTS.md` 범위 잠금과 동일 원칙).

## 신규 모델 제작 방식 (Blender MCP)

- Blender MCP로 연결해서 직접 제작.
- **의도적으로 초보자가 만든 것처럼**: 기초 도형(박스/구/원통 등) 조합 위주, **vertex 수도 적게** 유지.
- 목적: Meshy AI 특유의 고디테일·유기적 형태와 명확히 구분되는, 단순하고 손으로 만든 티가 나는 형태 유지.

## 세계관 (오브젝트·재질 작업 시 필수)

**꿀떡이 먹혀서 입과 식도에서 생존하는 게임이다.** Blender MCP로 메시/재질을 만들 때 이 세계관을 반드시 고려할 것.

- 스테이지·함정·환경은 **입 안 / 식도 안**에 있어야 자연스럽다 (이빨, 침, 식도벽, 혹, 음식물 등).
- 재질 색·질감도 같은 맥락: 살색/점막, 침, 음식, 이빨 등. 입·식도와 무관한 재질을 넣지 말 것.
- Food 카테고리처럼 에셋팩으로 교체하는 경우도, 배치·스케일은 "먹혀서 그 안에 있다"는 전제를 깨지 않게 맞출 것.

## 재질(Material) 전략 (확정)

저-vertex 큐브/스피어 메시라도 재질이 단색으로만 제한되는 건 아니다. 메시 형태와 재질/텍스처는 서로 독립적인 문제 — 아래 방식들을 상황에 맞게 조합해서 쓴다. **핵심 원칙: "AI가 생성한 이미지 텍스처"만 아니면 된다.**

- **기본: 단색(Base Color)** — 이미지 텍스처 없이 색만 지정. 가장 단순하고 세계관(살색/점막/침/이빨색)에도 맞음.
- **버텍스 컬러(Vertex Color)** — Blender에서 버텍스별로 색을 찍어 그레이디언트/얼룩 표현. 이미지 파일 없이 메시 데이터에 색이 들어가는 방식.
- **URP Shader Graph 절차적(procedural) 노이즈** — Noise/Voronoi/Gradient 노드로 무늬 생성. 이미지 파일이 아니라 사람이 짠 노드 그래프라 명확히 비-AI.
- **직접 손으로 그린 간단한 텍스처**도 허용 — 그림판/포토샵 등으로 직접 그린 이미지(체크무늬, 얼룩 등). AI 생성 이미지가 아니면 OK.

단색만 강제하지는 않음 — 필요한 오브젝트는 위 4가지 중 어울리는 방식을 골라 쓴다.

## 작업 패턴 (중요 — 다음 작업자가 알아야 함)

- 씬의 GameObject는 그대로 `Assets/Prefab/**` 하위 기존 래퍼 프리팹을 참조 유지.
- 그 프리팹의 `PrefabInstance` 오버라이드(`m_Mesh`, `m_Materials`)만 `Assets/NoAI/**`의 신규 모델로 갈아끼우는 방식 — 프리팹 이름/씬 배치는 그대로 두고 내부 참조만 교체.
- **⚠️ 메시 교체 ≠ 재질(텍스처) 교체.** 메시만 새 걸로 바꾸고 재질을 안 바꾸면, 렌더링에는 옛 Meshy 텍스처가 그대로 입혀진 상태로 남는다(아래 표의 5건 전부 이 상태로 확인됨). **둘 다 바꿔야 AI 콘텐츠 제거로 인정된다.**

## 완료 기준 (2026-08-19 재확정)

에셋팩으로 메시+재질을 통째로 갈아낀 것만 완료.  
우리가 그리거나 큐브/스피어로 바꾼 것은 **재질이 남았다.** Thron은 사용자 직접 제작 → 건드리지 않음. SpikeTrap·초록함정은 파티클화 → 건드리지 않음.

플레이어/UI/카메라(`Player*`, `UI`, `Setting_Panel`, `LocalPlayerCamera`)는 범위 밖.

---

## 1. 안 그린 오브젝트

| 대상 | 비고 |
|---|---|
| 이빨 변형 | `Tooth`, `Tooth_Stage2`, `Tooth1_2.5`, `tooth2_0.5` |
| BossBox | `식도/BossBox` |
| Framework | `식도/Framework` |
| Stage5Wall | `식도/Stage5Wall` |
| Fiber | `fiber.Side` / `fiber.Top` — 씬·프리팹 참조 없음. **그래도 만들어야 함** (사용자 확정) |
| ArrowTrap | `TrapFire1.fbx` (Meshy) 사용 중 |
| Food 채소 잔여 | 아래 17개와 별개. 아직 `Blender/Food/Food.fbx`(Meshy)를 씀: Strawberry, Tomato, Pumpkin, Cucumber, Potato, Corn, AppleMango, Drop4_Cabbage, Broccoli (+ Unused/Mangoi, Unused/Radish) |

**벽 초창기 확인 결과:** `Assets/Blender/Box&Wall/벽/벽 초창기/Wall_1.fbx` + `벽면1.mat`. 씬·프리팹에서 **한 번도 안 씀.** 초기 벽 실험 잔재. 안 그려도 됨. 파일 삭제만 결정하면 됨.

**그리지 않음 (확정):** Thron(사용자 제작), SpikeTrap(파티클), 초록함정(파티클), Bowl(에셋), Box 메시(사용자 제작).

---

## 2. Mat 작업

과일·ithappy·Thron·파티클 빼고, 우리가 손댄 게임플레이 오브젝트는 mat가 남음.

| 그룹 | 대상 |
|---|---|
| NoAI 메시 끼움, 재질은 옛 Meshy | Boulder, BreakableBoulder, Lump, Nodular, FrontTooth, FrontTooth_Stage5, BackTooth, BackTooth_Stage2 |
| 블렌더에서 그림, 미익스포트 | StageStartGate(도넛+바닥), StageStartPad, PressurePad |
| 프리미티브로 교체 | FloorTile, Ground, Ground 1, 입/Tile.*, Drop_*, DropTrap3, BossDrop5.*, BossPhase2 |
| 게이트/타일로 쓸 예정 | ColorTile_B/G/Y/P, ColorStartZone.* (메시 교체 후 mat) |
| Door | Door, Door.B/G/Y/P/C — 스피어 교체 후 mat |
| Box | FixedBox — 메시는 사용자 제작, **mat는 확인/작업** |
| Ring | 캡슐/스피어 교체 후 mat |
| MemoryPath | MemoryPath_Safe, MemoryPath_Trap, ColoredFloor, Memorypath |
| 앞으로 그릴 것 | 이빨 변형, BossBox, Framework, Stage5Wall, Fiber, ArrowTrap — 그린 뒤 mat |
| Vessel / Muscle | `NoAI/Ground` 배선 후 mat |

---

## 3. 에디터 작업 (메시 교체·배선, 새로 안 그림)

| 작업 | 대상 |
|---|---|
| Ring → capsule 또는 sphere | `식도/Ring.F`, `Ring.B` |
| ColorTile / ColorStartZone → StageStartGate(도넛) | ColorTile_B/G/Y/P, ColorStartZone.* |
| PressurePad / StageStartGate 익스포트 후 프리팹 메시 교체 | 프리팹은 아직 큐브/옛 빵 |
| Door → sphere | Door.* (프리팹에 Box 메시가 남아 있으면 저장 확인) |
| 채소 프리팹 → 에셋팩 메시 | Strawberry, Tomato, Pumpkin, Cucumber, Potato, Corn, AppleMango, Drop4_Cabbage, Broccoli |
| Ground 배선 | Vessel, Vessel2, Muscle, Ground, Ground 1 → `NoAI/Ground` (또는 현재 큐브 유지 + mat만) |

---

## 4. 애니메이션 때문에 뒤로 미룬 것

모델이 있어도 애니 없으면 배선하지 않음.

| 대상 | 지금 |
|---|---|
| Chaser | `NoAI/Chaser/Chaser1.fbx`, `Chaser2.fbx` 있음, 프리팹 미배선 |
| Runner | `NoAI/Runner/Runner.fbx` 있음, 프리팹 미배선 |
| Mouth / MouthTrap | `NoAI/Mouth/Mouth.fbx` 있음. 프리팹 `MouthTrap1`~`4`, `MouthBarrier.*`, `RealMouth`는 아직 Meshy |
| 식도 본관 | `NoAI/Esophagus/Esophagus.fbx` 있음, 미배선 (연동운동/블렌드셰이프 가능) |

---

## 5. 오브젝트 + Mat 끝난 것

| 대상 | 출처 |
|---|---|
| Food 17개 (원래 목록) | PolyOne 과일: Apple, Banana, Cherry, Grape, Pineapple, Watermelon, Drop4_Avocado. ithappy: Burger, Chips, Cookie, Croissant, Donut, IceCream, Sandwich, Mushroom, Pepper, Drop4_Onion |
| Bowl | 에셋팩 |
| Thron | 사용자 직접 제작 — 유지 |
| SpikeTrap | 파티클화 — 유지 |
| 초록함정 | 파티클화 — 유지 |

## 🗑️ 삭제됨

- `Boulder2.prefab`, `DamageWall.prefab`, `SpeedBuff.prefab`

## 다음 작업 순서

1. 안 그린 메시 (이빨 변형, BossBox, Framework, Stage5Wall, Fiber).
2. 에디터 교체 (Ring, ColorTile/게이트, Door, 채소 잔여).
3. Mat — 섹션 2 전부.
4. 애니 끝난 뒤 Chaser/Runner/Mouth/식도 배선.
