# Mouth 에셋 파이프라인 (Unity Asset Store → MouthTrap / RealMouth)

> 2026-08-19 확정. Meshy Mouth/MouthTrap/ArrowTrap 겉모습 교체용.  
> 현황 표는 `NoAIAssetReplacement.md` 섹션 4. 이 문서는 **한 입 에셋을 끝까지 만드는 순서**만 다룬다.

## 역할

| 단계 | 담당 |
|---|---|
| 유니티 에셋 가져오기 (Unpack 전용 프로젝트) | **사용자** |
| 입만 분리. 셰이프키·본·웨이트·잔여 메시 전부 삭제. 오브젝트 1개만 남김 | **에이전트** (사용자가 미리 떼도 됨) |
| 뒷벽 + 입술 끝 연결 fill. **입 안은 열어둠** | **에이전트** |
| 오브젝트를 `(0,0,0)`으로 옮기기 (오리진=지오메트리) | **에이전트** |
| 버텍스 페인트 | **사용자** |
| 버텍스 컬러 → 알베도 PNG만 | **에이전트** |
| 유니티 URP Lit mat (Base Map에 그 PNG) | **사용자** |
| 아마추어·Open/Hold/Close 애니 → FBX 내보내기 | **사용자** |

입 **하나**를 이 순서 끝까지 보낸 다음, 다음 에셋으로 간다. 메시만 여러 개 쌓아 두고 칠·애니를 몰아서 하지 않는다.

## 입 범위 (남길 것)

한 오브젝트 안에 있어야 할 것:

- 이빨
- 혀
- 잇몸 (이빨과 같은 덩어리로 둬도 됨)
- **입천장**
- **입바닥**

이빨 / 혀 / 잇몸을 **오브젝트로 쪼개지 않는다.** 나중에 윗턱·아래턱 자동 웨이트가 깨진다. 색만 버텍스 페인트로 나눈다.

## 하지 말 것

- 출시용 `My project`에 캐릭터 풀팩을 넣지 말 것. **AssetUnpack** 같은 빈 프로젝트에서만 임포트.
- 핸드메이드 프롭 파일(`MouthProps.blend` / `FrontTooth` 등)에 후보 캐릭터를 넣지 말 것. 추출은 `NoAI/Mouth/` 쪽 전용 blend.
- ArrowTrap **스크립트**를 지우거나 바꾸지 말 것. 교체는 `ArrowTrap.prefab` / MouthTrap 겉모습만.
- Open 클립을 10초로 만들고 인스펙터 초로 잘라 쓸 생각 하지 말 것. 클립 길이 = `MouthTrapAnimatorAnim`의 `openClipLength` / `closeClipLength`.
- **입 안(이빨 사이 구멍)을 그 자리에서 `F`로 채우지 말 것.** 그건 입을 막는 것이다. 닫기는 아래 섹션 3만 해당한다.
- Boolean Union으로 타원/캡을 합치지 말 것. 원본 입이 잘린다. Join + 루프 연결 fill만.
- 에이전트가 유니티 `.mat`을 만들지 말 것. PNG만 두고 mat은 사용자가 유니티에서 만든다.

---

## 1. 사용자 — 에셋 가져오기

1. Hub에서 Unpack 전용 유니티 프로젝트를 연다 (`My project` 아님).
2. Package Manager → My Assets → Download → Import.
3. 그 프로젝트 `Assets`에서 **`.fbx` + `Textures/*.png`만** 복사한다. 유니티 `.mat`은 블렌더가 안 읽는다.
4. 작업 폴더 예: `C:\Users\u\Desktop\Unity\NoAI\Mouth\Source<이름>\`
   - FBX와 `Textures\`를 같이 둔다. FBX만 열면 회색·분홍(missing)이다.

블렌더 File → Import → FBX에는 **텍스처 가져오기 버튼이 없다.** 메시만 가져온다. png는 재질 Base Color에 직접 연결하거나, 구강 추출 후에는 버려도 된다(색은 버텍스 페인트로 다시 칠함).

## 2. 에이전트 — 입만 남기기

- 이빨·혀·잇몸·입천장·입바닥만 남긴 **오브젝트 1개**.
- 삭제: 몸통, 눈, 옷, 아마추어 오브젝트, 셰이프키, 버텍스 그룹, 안 쓰는 메시/이미지/재질.
- 씬에 보이는 것도, 데이터블록도 입 메시만.

셰이프키(`OpenMouth` 등)를 남겨 두면 에디트 모드에서 입이 혼자 여닫힌다. 남기지 않는다.

## 3. 에이전트 — 뒷벽 + 입술 끝 fill (2026-08-19 확정)

이게 “닫기”의 전부다. **입 안을 막는 게 아니다.** 앞에서 보면 이빨·구강이 열려 있고, 뒤에서 보면 막혀 있어야 한다.

완료 정의: **뒷벽 테두리 버텍스 ↔ 입술 끝(자른 루프) 버텍스를 짝지어 fill한 상태.**

원본 입 오브젝트는 한 번도 지우거나 Boolean하지 않는다. 뒷벽만 붙이고, 둘의 끝 루프를 연결한다.

### 하지 말 것 (이 단계)

- 입 안쪽 구멍 / 이빨 사이를 `F`로 채우기
- 바깥 테두리를 **그 자리**에서 채우기 (입구에 북이 막힌다)
- Boolean Union, 타원을 원본에 파고 들어가게 합치기
- 원본 입 버텍스를 삭제·용접·리메쉬

### 순서

1. **입 원본은 그대로 둔다.** 사용자가 입만 떼 둔 상태여도 된다.
2. 입술 끝의 **바깥 테두리 루프**를 찾는다 (메인 섬의 가장 큰 boundary. 이빨 구멍 아님).
3. 그 루프만 **복제**해서 새 오브젝트로 둔다. 원본 루프는 열린 채로 남긴다.
4. 복제 루프를 이빨 반대쪽(**머리 뒤**)으로 옮긴다. 이빨·입 안을 지나치면 안 된다. 입 뒷면에 닿을 정도로만.
5. 복제 루프만 `F`로 막아 **뒷벽**을 만든다. 큰 n-gon은 **삼각화**.
6. 뒷벽을 입에 **Join**한다. 입이 active. Boolean 아님. 원본 버텍스 좌표는 그대로여야 한다.
7. **fill (이게 닫기의 본작업):** 뒷벽 테두리 버텍스와 입술 끝 루프 버텍스를 개수·진행 방향을 맞춰 짝짓는다. 인접한 두 쌍마다 쿼드 한 장. (Bridge Edge Loops와 동일.)
8. 앞에서 이빨이 보이는지, 옆에서 입술 끝→뒷벽이 면으로 이어졌는지 확인.

이빨 뿌리 구멍은 이 단계 범위가 아니다. 입 안을 열어둠과 별개다.

## 4. 에이전트 — `(0,0,0)`

스케일 적용 → Origin to Geometry → location `(0,0,0)`.  
캐릭터 키 좌표(Y≈150)에 두면 뷰·페인트가 점프한다.

## 5. 사용자 — 버텍스 페인트

- 오브젝트는 1개 유지. 이빨 흰색, 잇몸·혀 선분홍 등.
- 면만 칠하려면: **에디트 모드에서 면을 고른 다음** 버텍스 페인트로 오고, 헤더 **면 마스크**만 켠다. 페인트 헤더의 면 아이콘은 면 선택이 아니라 “고른 면에만 칠하기”다. 선택 0개면 안 칠해진다.
- Paint Hard에 빈 텍스처가 붙어 있으면 스트로크가 0이 된다. Texture 슬롯 **X로 끊기**.
- 이미 칠한 곳 제외: 에디트에서 그 면을 고르고 **H**로 숨기거나 **Ctrl+I**로 반전.

머터리얼 보기(기본 Principled)는 버텍스 컬러를 안 보여 준다. 페인트 직후 안 보이면 고장난 게 아니다. 다음 단계에서 PNG로 굽는다.

## 6. 에이전트 — 알베도 PNG (베이크)

색은 블렌더에서 확정한다. Shader Graph(버텍스 컬러)는 쓰지 않는다. **유니티 `.mat`은 만들지 않는다.**

1. (필요 시) Smart UV Project로 입에 UV를 다시 펼친다. 버텍스 색은 점에 남아 있다.
2. Color Attribute(`속성`) → Emission → Cycles **Bake Type: Emit**.
3. 선택된 Image Texture 노드에 구움 (1024면 충분).
4. PNG만 저장. 예: `Assets/NoAI/Mouth/<이름>_Albedo.png`.
5. 블렌더 Principled Base Color에 그 PNG (뷰포트 확인용).

유니티 Lit mat은 **사용자가** 만들고 Base Map에 이 PNG를 넣는다. 색을 다시 칠하면 베이크·PNG 저장만 다시 한다.

## 7. 사용자 — 본·애니·내보내기

뼈는 캐릭터에서 가져오지 않는다. **윗턱 / 아래턱 2개**를 이 메시에 새로 넣고 Automatic Weights.

클립 3개 + Idle (인스펙터 초와 길이 일치):

| 상태 | 클립 내용 | 인스펙터 |
|---|---|---|
| Open | 닫힘 → 다 열림 동작만. Hold 프레임 넣지 않음 | `openClipLength` |
| Hold | 완전히 열린 포즈. Loop Time | `holdDuration` (유지 시간. 클립과 달라도 됨) |
| Close | 열림 → 닫힘 동작만 | `closeClipLength` |

`MouthTrapAnimatorAnim`은 인스펙터 초가 지나면 클립을 잘라서 끝까지 재생하지 않고 **즉시 다음 트리거**로 끊는다. Open 10초 클립 + `openClipLength` 0.2초면 입이 거의 안 벌어진다.

FBX: 메시+아마추어만, Selected Objects, Add Leaf Bones 끄기, Bake Animation 켜기. PNG를 같은 `Assets/NoAI/Mouth/`에 둔다. 기존 MouthTrap 컨트롤러 트리거(`doOpen` / `doHold` / `doClose`)에 클립만 끼운다.

---

## 배선 완료 (2026-08-19)

MCP로 아래 표대로 전부 갈아끼움. 씬 인스턴스는 래퍼 프리팹 참조 그대로 유지(프리팹 자체 asset guid 불변, 루트 fileID 보존). 스크립트(`ArrowTrap`, `MouthTrapAnimatorAnim`)는 그대로 두고 메시/재질/스케일/Animator만 교체.

**방식:** `PrefabUtility.LoadPrefabContents` → 기존 메시/본 자식 전부 삭제(+wrapper는 기존 Animator도 삭제) → 새 FBX를 같은 임시 씬에 `InstantiatePrefab(fbx, root.scene)`으로 인스턴스화 → `UnpackPrefabInstance(Completely)`로 완전 언팩 → 자식들을 루트 밑으로 `SetParent(root.transform, false)` 재부모 지정 → 재질/스케일/Animator 재배선 → `SaveAsPrefabAsset`. (주의: `InstantiatePrefab`을 임시 씬이 아닌 활성 씬에 하면 `SetParent`가 조용히 무시된다 — 반드시 같은 씬 + Unpack 필요.)

**컨트롤러:** 기존 `MouthTrap1~4.controller`는 그대로 재사용, 4개 State(Open/Hold/Close/Idle)의 Motion만 새 FBX의 동일 이름 클립으로 교체. ArrowTrap용은 `MouthTrap2.controller`를 복제해 `Assets/Animator/MouthTrapArrow2.controller` 신규 생성 후 Mouth2 클립으로 교체.

**스케일:** 코드에서 실측 계산(기존 SMR localBounds × 기존 scale = 월드 크기, 그 값 ÷ 새 SMR localBounds = 새 scale). 아래는 결과값.

### 매핑 (완료)

| 대상 프리팹 | 새 메시 | 재질 | 결과 스케일 |
|---|---|---|---|
| `Prefab/입/MouthTrap1` | `NoAI/Mouth/Mouth3` | `Mouth3.mat` | (0.59, 0.79, 0.53) |
| `Prefab/입/MouthTrap4` | `NoAI/Mouth/Mouth3` | `Mouth3.mat` | (0.59, 0.82, 0.51) |
| `Prefab/입/MouthTrap2` | `NoAI/Mouth/Mouth1` | `Mouth1.mat` | (2.57, 3.15, 1.95) |
| `Prefab/입/MouthTrap3` | `NoAI/Mouth/Mouth0` | `Mouth0.mat` | (0.26, 0.83, 0.51) |
| `Prefab/ArrowTrap` | `NoAI/Mouth/Mouth2` | `Mouth2.mat` | (0.66, 0.43, 0.53) |
| `Prefab/입/MouthBarrier.B/G/Y/P` | `NoAI/Mouth/Mouth3` | **기존 캐릭터색 mat 유지** (`MouthTrap4_B/G/Y/P`) | (0.98, 1.37, 0.77) |

`RealMouth`는 이번 라운드 밖 (미배선).

ArrowTrap: 기존 `MouthTrapAnimator`(BlendShape) + `MeshFilter` + `SkinnedMeshRenderer`(루트 직속) 제거 → `FirePoint` 자식은 유지 → Mouth2 자식(메시+아마추어) 추가 → 루트에 `Animator`(`MouthTrapArrow2.controller`) + `MouthTrapAnimatorAnim` 신규 부착. `ArrowTrap.cs`는 그대로.

Hold 클립은 FBX에서 이미 Loop Time = true로 익스포트됨. `openClipLength`/`closeClipLength`는 각 FBX의 Open/Close 클립 실제 길이로 자동 반영(Mouth0/1/3 = 0.583s, Mouth2 = 0.467s). `holdDuration`은 기존 값(0.2s) 유지.

---

## 한 줄 체크 (에이전트 완료 조건)

- [ ] 오브젝트 1개 (천장·이빨·혀·바닥 포함)
- [ ] 셰이프키·본·웨이트·잔여 데이터블록 없음
- [ ] 뒷벽 있음. 입술 끝 ↔ 뒷벽 테두리가 쿼드로 연결됨. **입 안은 열려 있음**
- [ ] 뒷벽 n-gon 삼각화. Boolean 없음. 원본 입 버텍스 유지
- [ ] origin/location `(0,0,0)`
- [ ] 사용자 페인트 후 알베도 PNG만 (`Assets/NoAI/Mouth/`). 유니티 mat은 사용자가 만듦
- [ ] 사용자는 2본 + Open/Hold/Close 맞춰 보낸 뒤 프리팹 겉모습만 교체
