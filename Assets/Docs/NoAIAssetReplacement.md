# NoAI 에셋 교체 (Meshy AI → 비-AI) — 작업 현황

> 2026-08-26 재검증 후 갱신 (이전 2026-08-19 버전 대체). 코드/씬 직접 열어서 guid 단위로 재확인함 — 아래 "완료" 항목은 전부 실제 파일 확인 완료.

## 목적 (반드시 유지)

**스팀(Steam)의 AI 생성 콘텐츠 공개(AI Disclosure) 의무를 피하기 위해**, 기존에 Meshy AI로 생성했던 모델·텍스처를 전부 비-AI 소스(Blender 직접 제작 + 구매/무료 에셋팩)로 교체하는 작업이다. 데모용 임시 조치가 아니라 **정식 출시본 전체에 적용**되어야 한다(`AGENTS.md` 범위 잠금과 동일 원칙).

## 2026-08-26 재검증 결론

**활성 씬/프리팹 기준으로 AI(Meshy/Gemini) 생성 콘텐츠 없음.** 아래 항목 전부 guid 추적으로 실제 파일 확인:

| 대상 | 확인 결과 |
|---|---|
| **Player** | `Title.unity`의 `PlayerSpawnManager.playerPrefab` → `Assets/Prefab/Kkultteok.prefab` (NoAI). 실제 온라인 스폰 경로(`InitializeOnline`→`SpawnWithOwnership`)가 이걸 사용 |
| **Esophagus (식도 본관)** | `T.Stage1/2/3`, `T.Boss`에 `NoAI/Esophagus/Esophagus.fbx` 프리팹 인스턴스로 배치 완료, 재질 오버라이드도 적용됨 |
| **BossBox / RealMouth** | 활성 씬(M/T 전 스테이지)에 없음. `Scenes/Backup/1.Mouth.Backup.unity`(안 쓰는 백업 씬)에만 흔적 — 실질적으로 게임에서 제거됨 |
| **UI (Figma)** | `Figma/Materials/Gemini_Generated_Image_*` 파일 자체가 더 이상 없음. 현재 `Logo.mat`/`Marker.mat`/`Start.mat`은 `Assets/Figma/**/*.png`(Figma 디자인 export)를 참조 — AI 이미지 아님 |
| **Ground/Wall/Esophagus 계열 mat** (`Stage1Ground`, `SplitGround`, `T.Stage1~5`, `T.BossGround`, `T.BossBG`, `WallMover`, `FloorTile.*`, `M.Boss1`, `M.Phase1~5` 등) | 텍스처 guid 추적 결과 전부 `Bark0xx_2K-JPG`, `Sponge00x_2K-JPG`, `Ice002_2K-JPG`, `Foam003_2K-JPG`, `Leather024_2K-JPG`, `Rubber004_2K-JPG`, `Snow010A_2K-JPG`, `Rope001_2K-JPG`, `box_profile_metal_sheet` 등 **PBR 텍스처팩(ambientCG류) 파일** — AI 생성 이미지 아님 |
| **Boulder / `123124124123.mat`** | `Sponge001_2K-JPG_Displacement.jpg` — 텍스처팩, AI 아님 |
| **Tooth / FrontTooth / BackTooth / BackTooth_Stage2 / FrontTooth_Stage5** | `Tooth1.mat`/`Tooth2.mat` — 이미지 텍스처 없음(단색 Base Color만) |
| **FixedBox** | `Player/UnCommon.mat` — 단색, 텍스처 없음 |
| **Door.\*** / **PressurePad.\*** / **ColorStartZone.\*** | `Player/Berry.mat` — 단색, 텍스처 없음 |
| **ColorTile.B/G/Y/P/White/Black/C** | 전부 단색(Base Color만), 텍스처 없음 |
| **Ring.F / Ring.B** (`T.Stage4`) | 메시가 Unity 빌트인 **Capsule**(fileID 10208)로 교체 완료. 재질도 텍스처팩(`Bark015_2K-JPG`) |
| **DropTrap3 / BossDrop5.1** | `IceBall 1.mat` / `BossPhase5.1.mat` — 단색, 텍스처 없음 |
| **Memorypath** | 재질이 Unity URP **기본(Default) 머티리얼** — AI 아니지만 아직 고유 재질 없음(placeholder, 필요시 나중에 손보면 됨) |

**그리지 않음 (확정, 기존 유지):** Thron(사용자 제작), SpikeTrap·초록함정(파티클화), Bowl(에셋), Box 메시(사용자 제작), Framework(안 씀).

---

## 아직 남은 것 (AI 콘텐츠 문제 아님 — 별개 이슈)

| 항목 | 내용 | 비고 |
|---|---|---|
| **Chaser / Runner (T.Stage5)** | `Stage5ChaserSpawner.chaserPrefab` 필드가 어떤 프리팹도 참조하지 않는 빈 상태(guid가 프로젝트에 존재하지 않음) — 애니메이션 작업 대기 중이라 아직 안 꽂음 | `NoAI/Chaser/Chaser1.fbx`, `Chaser2.fbx`, `NoAI/Runner/Runner.fbx` 메시는 이미 있음. 빈 슬롯 자체는 AI 콘텐츠가 아니라 "미완성" 문제 — disclosure 판단과 무관 |
| **`NetworkManager.prefab`의 `NetworkConfig.PlayerPrefab`** | guid `22a34ecf8fee91f4496e7ace0639125c`가 프로젝트에 존재하지 않는 죽은 참조. 실제 스폰은 `PlayerSpawnManager`가 담당해서 게임플레이엔 영향 없어 보이지만, 정리 안 된 필드로 남아있음 | 인스펙터에서 `Kkultteok.prefab`으로 다시 채우거나 비워서 정리 권장 (AI 문제 아님, 기술 정리 항목) |

---

## 결론

문서 작성 시점(2026-08-26) 기준, **실제 출시 빌드에 남아있는 AI 생성 콘텐츠는 확인되지 않음.** Chaser/Runner 미배선은 콘텐츠 완성도 이슈이고, Player/Esophagus/UI/Ground/Tooth 등 나머지는 전부 실사용 파일 레벨로 확인 완료.

**단, 아래는 이번 재검증 범위 밖 — 필요시 추가 확인 권장:**
- 프로젝트 전체 Mat/텍스처를 100% 전수 조사한 건 아님(주요 게임플레이 오브젝트 위주로 확인). 새로 추가되는 에셋은 그때그때 출처 확인 필요.
- Steam 페이지 스크린샷/트레일러 자체의 시각적 갱신 여부는 별도 트랙(스토어 작업, `ReleaseRoadmap.md` 참고).
