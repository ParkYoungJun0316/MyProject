# 머리 소품(코스튬) 설계

> 2026-10-04 확정·구현. 플레이어가 Tutorial·Interlude에서 모자·머리장식·선글라스를 골라 쓰고, 같은 방 전원이 그 모습을 본다.

## 1. 규칙 (사용자 확정)

| 항목 | 결정 |
|---|---|
| 칸 | **머리 칸**(없음 / 모자 5 / 머리장식 5 중 하나) + **선글라스 칸**(없음 / 10). 모자와 머리장식은 같은 칸이라 같이 못 쓴다 |
| 바꾸는 곳 | Tutorial·Interlude의 간판에서 [E] → 선택 창. 스테이지·서바이벌에서는 고른 그대로 유지 |
| 해금 | 전부 처음부터 열림. 여러 명이 같은 소품을 써도 됨 |
| 저장 | **없음.** 접속하면 전원 기본 상태(아무것도 안 씀)로 시작 |
| 색 | 소품은 고유색/흑/백 전환·피격 번쩍·스텔스 투명의 대상이 아님(자기 재질 색 유지) |
| 판정 | 없음. 콜라이더 없이 보이기만 한다 — 플레이어 판정 크기 그대로 |
| 선택 창 | 아이콘만(글자 없음). 누르면 즉시 착용. 창을 닫고 카메라를 돌려 확인(창이 열려도 카메라는 그대로) |
| 제외 | 타이틀 화면 캐릭터·기존 마케팅 스샷은 그대로 |

## 2. 구조

```
[E] CostumeSignboard ─ 열기 ─▶ CostumePanelUI ─ 버튼 ─▶ PlayerCostume.Local.RequestSet(head, glasses)
                                                              │ Rpc(SendTo.Server, Owner만)
                                                              ▼
                                        Host: 범위 검사 → PlayerSpawnCoordinator.SetCostume(clientId, …)
                                                              │ NetworkList<ClientCostumeEntry>
                                                              ▼
                              전 머신: OnCostumeChanged → 각 PlayerCostume가 자기 소품 오브젝트 켜고 끔
```

- **값이 사는 곳 = `PlayerSpawnCoordinator`** (clientId → 머리 칸 번호, 선글라스 칸 번호. 0 = 없음).
  플레이어는 씬마다 새로 스폰되므로(`NetworkDesign.md` §11) 플레이어 오브젝트에 두면 씬이 바뀔 때 사라진다. Coordinator는 세션 동안 유지되고 방을 나가면 Despawn되므로, "스테이지 넘어가도 유지 + 접속하면 기본 상태"가 추가 코드 없이 성립한다.
- **`PlayerCostume`** (플레이어 프리팹 루트, NetworkBehaviour): 목록을 읽어 `headItems`/`glassesItems`의 오브젝트를 켜고 끈다. 변경 요청 RPC도 여기 — Owner만 호출, 요청 번호(`RpcSubmitDedup`)로 늦게 온 옛 요청을 버리고, Host가 배열 길이로 범위 검사.
- **`CostumePanelUI`** (씬 HUD Canvas 자식, 기본 비활성, 프리팹 `Assets/Prefab/CostumePanel.prefab`): 칸을 직접 들고 있지 않다 — 처음 열릴 때 `PlayerCostume.Local`의 목록을 읽어 `CellTemplate`(`CostumeCell`)을 복제한다.
- **`CostumeSignboard`** (씬 오브젝트): 근접 트리거 + [E]. `TutorialCheerNameSignboard`와 같은 패턴.

### 창이 열려 있는 동안 (팀 구호 창과 같은 규칙 — 사용자 확정 2026-10-04)

이동·Ctrl/Alt/Space, 채팅(Enter), 숫자 응원, 버프 교체(Q), 마이크(M), 이모트, 다른 간판의 [E]가 전부 막힌다. 채팅하려면 창을 닫아야 한다. Esc는 창이 먼저 닫히고 ESC 메뉴는 같은 프레임에 열리지 않는다.
이동 키를 누른 채 창(꾸미기·팀 구호·채팅)을 열어도 멈춘다 — `Player.GetInput`이 매 프레임 `moveInput`을 0으로 만든다. 닫은 뒤엔 키를 다시 눌러야 움직인다.

## 3. 소품 목록·번호 — SSOT는 플레이어 프리팹의 `PlayerCostume`

어떤 소품이 있는지, 순서(= 네트워크 번호, 1부터. 0 = 없음), 아이콘은 **`Kkultteok.prefab` → `PlayerCostume.headItems` / `glassesItems` 한 곳**에만 있다(항목 = 소품 오브젝트 + 아이콘). 창의 칸과 Host 범위 검사가 전부 이 배열에서 나오므로, 이 문서에는 목록을 따로 적지 않는다 — 프리팹 인스펙터가 정답이다.

## 4. 에셋

- 원본: `C:\Users\u\Desktop\Unity\NoAIBlend\Player\Kkultteok.blend` — `Accessories` 컬렉션, 전부 `Head` 본 자식.
- 메시·재질: `Assets/NoAI/Accessories/Kkultteok_Accessories.fbx` (재질은 FBX 안에 묶여 있음).
- 아이콘: `Assets/NoAI/Accessories/Icons/Icon_<오브젝트 이름>.png` (블렌더 렌더 256px, 투명 배경) + `Icon_None.png`.
- 플레이어 프리팹 `Assets/Prefab/Kkultteok.prefab`: `아마튜어/Root/Core/Body/Head` 밑에 소품 20개(기본 비활성, FBX의 메시·재질 참조, local pos (0, -1.019, 0)·rot (270, 0, 0)). 변형 프리팹 5개는 상속.
- 색 변환 제외: `PlayerVisualController.fixedRenderers`에 소품 렌더러 20개 등록(눈·볼·입과 같은 방식 — 에디터 설정, 코드로 따로 빼지 않는다).

## 5. 소품을 추가할 때

1. 블렌더에서 `Head` 본 자식으로 만들고 FBX 다시 내보내기.
2. `Kkultteok.prefab`의 `Head` 밑에 오브젝트 추가(비활성) → `PlayerVisualController.fixedRenderers`에 추가.
3. 아이콘 렌더 → `PlayerCostume`의 해당 배열 **끝**에 항목(오브젝트 + 아이콘) 추가. 창은 손대지 않는다(칸이 자동으로 늘어남).
4. 배열 중간에 끼워 넣지 말 것 — 번호가 밀려 버전이 다른 클라이언트끼리 다른 소품이 보인다.

## 6. 씬 배치

| 씬 | 간판 위치 | 트리거 | 비고 |
|---|---|---|---|
| Tutorial | (13.16, 2, -4.79) rotY 110 | 8×8 | 팀 구호 간판에서 원을 따라 남동쪽. 다른 트리거와 겹침 없음 |
| Interlude | (5.25, 1, -3.5) rotY 90 | 3.5×3.5 | 동쪽 벽. 방이 좁아 트리거를 줄임. 다른 트리거와 겹침 없음 |

두 씬 모두 `UI/CostumePanel`(프리팹 인스턴스, 비활성)을 간판의 `costumePanel`에 연결. 프롬프트 문구는 `Tutorial.Prompt.Costume`(`TutorialTranslations.md`).

### 상인 NPC (`Assets/Prefab/CostumeNpc.prefab`, 2026-10-04)

간판만 있으면 어색해서 프롬프트 옆에 세워 둔 장식. 흰색 플레이어 + 밀짚모자(`Acc_Hat_Straw`) + 별 선글라스(`Acc_Sunglasses_Star`).
**스크립트·콜라이더·Animator 없음** — 렌더러만 있는 정적 오브젝트(자세는 Idle 0프레임을 에디터에서 구워 넣음, Tag Untagged·Layer Default). 플레이어가 통과한다.

| 씬 | 위치 | 방향 |
|---|---|---|
| Tutorial | (15.70, 0.5, -9.32) — 프롬프트 오른쪽 | rotY 290 (접시 중심 쪽) |
| Interlude | (6.2, 0.5, -1.7) — 동쪽 벽, 프롬프트 북쪽 | rotY 270 (방 안쪽) |

## 6A. 선글라스 크기 (2026-10-04 조정)

처음엔 10개가 거의 같은 크기에 볼·입을 많이 덮었다. 블렌더에서 메시를 직접 수정(각자 중심 기준):
전체 80% → 네모만 추가 90%(원래의 72%). 원형은 입이 렌즈에 박혀 앞으로 0.046, 네모는 0.037 띄움.
FBX는 같은 파일에 덮어써서(GUID·메시 ID 유지) 프리팹 수정 없이 반영됨. **아이콘은 다시 렌더하지 않음**(사용자 결정 — 옛 크기 그림 그대로).
내보내기: 아마튜어 + `Accessories`만, `apply_scale_options=FBX_SCALE_ALL`, leaf bone·애니메이션 없음.

## 7. 검증

- [x] 에디터 플레이(Host 1인, Tutorial): 요청 → 착용, 다른 소품으로 교체, 범위 밖 번호 거절, 창 닫으면 커서 다시 잠김, 소품이 몸 색(파랑)에 물들지 않음 (2026-10-04, MCP)
- [ ] 2인(ParrelSync 또는 빌드 2개): 상대 소품이 보이는지, 늦게 들어온 사람에게 먼저 고른 소품이 보이는지
- [ ] 씬 전환(Tutorial → M.Stage1, 실패 리로드, Interlude) 뒤에도 유지되는지
- [ ] 사망·부활, 흑백 전환, 피격, 스텔스 때 보임새
- [ ] 방을 나갔다 다시 만들면 기본 상태인지
