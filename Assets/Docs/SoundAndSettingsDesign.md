# 사운드(BGM/SFX) · 옵션(설정) 메뉴 — 설계·구현 현황

`ReleaseRoadmap.md` §4 순위 5~7("BGM 추가" / "옵션·설정 메뉴" / "SFX 마무리 + BGM 음량 조절")의 상세 구현 문서.
**코드 구현은 완료, 에디터 배치·UI 프리팹 제작·실제 테스트는 아직 진행 전** (2026-08-07 세션 기준).

> **다음 에이전트/세션 시작 지침:** 이 문서 "남은 작업(에디터·콘텐츠)" 절부터 확인. 코드 쪽에 추가 변경이
> 필요한 상황이 아니면 이 문서의 아키텍처를 재설계하지 말 것 — 사용자와 합의된 구조임(AudioMixer 미사용
> 등, 아래 §1 참고).

---

## 0. 배경 · 왜 이 구조인가

- `ReleaseRoadmap.md`에 "사운드: BGM + 핵심 SFX (**과투자 금지**)"라고 명시돼 있음 — 지금 필요한 건
  마스터/BGM/SFX 볼륨 슬라이더 3개 수준이라 Unity `AudioMixer` 에셋(그룹·덕킹 등 고급 오디오 라우팅)은
  오버엔지니어링으로 판단, **쓰지 않기로 확정**.
- 옵션 메뉴는 **타이틀 화면 + 인게임 ESC 메뉴 양쪽에서 열 수 있어야 함**(사용자 확정) — 하나의
  `OptionsMenuController` 컴포넌트를 양쪽에 재사용하는 구조로 설계.
- 화면 모드는 전체화면/창모드/테두리없는창모드 3종 다 지원, 해상도 선택도 지원(전체화면에서도) — 흔한
  인디게임 패턴.

---

## 1. 아키텍처 — 볼륨 시스템 (AudioMixer 미사용, "pull" 방식)

**공식:** `최종 소리 크기 = 원본 파일(고정) × 클립별 보정 배율(SFXLibrary, 0~2, 기본 1) × 마스터 볼륨(0~1) × BGM 또는 SFX 볼륨(0~1)`

- 플레이어용 마스터/BGM/SFX 슬라이더는 **0~1로 clamp** — 원본보다 더 크게 부스트하지 않음(클리핑 방지,
  일반적인 게임 볼륨 슬라이더 관례).
- `SFXLibrary`의 클립별 보정 배율(§4 참고)만 0~2 범위로 부스트 가능 — 이건 "플레이어 설정"이 아니라
  "개발자가 여러 음원 간 상대 밸런스를 맞추는 보정값"이라 별도 축.
- **"Push" 대신 "Pull" 패턴 채택**: `GameSettingsManager`가 값이 바뀔 때마다 `SFXManager`/`BGMManager`에
  값을 밀어주는(push) 방식이 아니라, `SFXManager`/`BGMManager`가 재생 시점(또는 매 프레임)에
  `GameSettingsManager.Instance`를 직접 읽어서(pull) 계산함.
  **이유:** Unity는 서로 다른 GameObject의 `Awake()` 실행 순서를 보장하지 않음(공식 문서 명시) — push
  방식이면 어느 쪽이 먼저 Awake되냐에 따라 초기값이 안 먹는 타이밍 버그가 날 수 있음. 실제로 이 프로젝트
  `SteamworksIntegrationDesign.md` 트랙5 6차 세션에서 `TitleMenuController`/`SteamLobbyManager` 사이에
  이런 Awake 순서 버그가 실제로 발생했던 전례가 있어서, 같은 함정을 피하려고 의도적으로 pull 방식을 택함.

### PlayerPrefs 키 (영구 저장)

| 키 | 의미 | 소유 |
|---|---|---|
| `Settings.MasterVolume` / `Settings.BgmVolume` / `Settings.SfxVolume` | 볼륨 0~1 | `GameSettingsManager` |
| `Settings.DisplayMode` / `Settings.ResWidth` / `Settings.ResHeight` | 화면모드·해상도 | `GameSettingsManager` |
| `Settings.LocaleCode` (`GameLocalizationBootstrap.ManualLocaleOverrideKey`) | 수동 선택 언어 코드 | 양쪽이 공유(아래 §3) |

---

## 2. 구현 완료 파일

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Settings/GameSettingsManager.cs` (신규) | 볼륨·화면·언어 SSOT. 싱글턴, DontDestroyOnLoad. PlayerPrefs 저장/로드. |
| `Assets/Scripts/Audio/BGMManager.cs` (신규) | 씬 이름 접두사 기반 BGM 재생 + 크로스페이드(1.5초). `PlayClip(AudioClip)`으로 같은 씬 안 Phase별 강제 전환도 지원(§5). |
| `Assets/Scripts/Audio/SFXManager.cs` (수정) | `EffectiveVolume`이 `GameSettingsManager` 우선 사용(없으면 기존 Inspector 필드 폴백). `SFXLibrary.GetVolumeMultiplier(id)` 반영. |
| `Assets/Scripts/Audio/SFXLibrary.cs` (수정) | `VolumeOverride[]` 배열 추가 — 클립별 0~2배 보정(§4). |
| `Assets/Scripts/Audio/PlayerAudio.cs` (수정) | 달리기 루프 사운드가 SFX 마스터 볼륨을 매 프레임 반영하도록 수정(기존엔 전혀 반영 안 되던 버그성 gap). |
| `Assets/Scripts/Localization/GameLocalizationBootstrap.cs` (수정) | 옵션에서 저장한 수동 언어(`ManualLocaleOverrideKey`)가 있으면 Steam/systemLanguage 자동감지보다 최우선 적용. |
| `Assets/Scripts/UI/OptionsMenuController.cs` (신규) | 슬라이더 3개 + 언어/해상도/화면모드 드롭다운 ↔ `GameSettingsManager` 연결. 타이틀·ESC 메뉴 양쪽 재사용 가능. |
| `Assets/Scripts/UI/EscMenuController.cs` (수정) | 미구현이던 Setting 버튼에 `OnClickSettings()`/`OnClickCloseSettings()` 추가(`TitleMenuController`와 동일 패턴). |

---

## 3. 언어(로컬라이제이션) 연동

`GameLocalizationBootstrap`(기존, 트랙4)의 자동감지(Steam/systemLanguage) 위에 **수동 우선순위**를 추가함:

1. 옵션 메뉴에서 유저가 직접 고른 언어(`GameSettingsManager.SetLocale()` → PlayerPrefs 저장) — **최우선**
2. 릴리스 경로면 Steam 클라이언트 언어
3. 로컬 경로거나 위 실패 시 `Application.systemLanguage`
4. 전부 실패 시 `en`

`OptionsMenuController`는 `LocalizationSettings.AvailableLocales.Locales`를 그대로 드롭다운에 채움 — 코어
12개 언어가 Project Settings에 등록돼 있으면 자동으로 다 나옴(별도 코드 수정 불필요).

---

## 4. SFX 클립별 볼륨 보정 (`SFXLibrary.VolumeOverride`)

여러 곳에서 받은 음원끼리 소리 크기가 안 맞을 때, Audacity로 파일을 다시 만지지 않고도 `SFXLibrary` 에셋
Inspector에서 특정 SFX만 보정 가능:

- `SFXLibrary` 에셋 선택 → `Volume Overrides` 배열에 항목 추가 → `Id`에 해당 `SFXId`, `Volume`에 배율(0~2,
  기본 1) 입력.
- 목록에 없는 SFXId는 보정 없음(1배)으로 자동 처리.
- **주의:** 1보다 많이 키우면(부스트) 노이즈/클리핑 위험 — 큰 차이가 나면 원본을 Audacity에서 정규화하는
  쪽이 먼저고, 이건 미세 보정용.
- 사용자 테스트 결과에 따라 실제 값 채우는 작업은 아직 안 함(§7 참고).

---

## 5. BGM — 구역별(씬 접두사) + 구간별(Phase) 전환

`BGMManager.zoneClips[]`에 `scenePrefix`(예: `"M."`, `"T."`) ↔ `clips[]`(재생목록) 매핑을 등록하면, 씬
이름이 그 접두사로 시작할 때 자동 재생·크로스페이드됨. **더 긴(구체적인) 접두사가 우선** — 예를 들어
`"M."`(구역 공통)과 `"M.Boss"`(보스 전용) 둘 다 등록해두면 보스 씬에서만 `"M.Boss"` 쪽이 이김.

**한 구역에 곡을 2개 이상 등록하면 순서대로 순환 재생됨** (1번 곡 끝 → 크로스페이드로 2번 곡 → 끝나면
다시 1번 곡…). 곡이 1개면 그냥 그 곡을 계속 loop. 구역 전환(씬 이동)이 일어나면 재생목록은 항상 처음
(index 0)부터 다시 시작.

**같은 씬 안에서 구간이 바뀌는 경우** (예: `M.Stage2` 안에 OX퀴즈 구간 → 화살함정 구간처럼 `PhaseManager`
의 `PhaseData`가 여러 개인 경우) — 씬 전환이 없어서 자동 매칭으로는 못 잡음. 이때는
`PhaseData.onPhaseEnter`(UnityEvent, 이미 존재)에 `BGMManager.PlayClip(AudioClip)`을 연결하면 그 시점에
원하는 곡으로 크로스페이드되고, 구역 재생목록 순환은 중단됨(수동 지정 곡을 계속 loop). 씬 자동 매칭과
독립적으로 동작해서 충돌 없음.

현재 `Assets/Audio/BGM/`에 임시로 들어있는 파일 2개(`말랑말랑한_발걸음.mp3` 28초, `Gums and Gullet.wav`
36초) — 둘 다 짧은 편이지만 `AudioSource.loop`는 길이 무관하게 매끄럽게 반복되므로, 편집(이어붙이기) 없이
일단 그대로 넣어보고 실제 플레이에서 "너무 금방 반복된다" 싶을 때만 나중에 늘리는 작업을 하기로 함(미리
작업 안 함). 참고로 같은 구역에 이 2곡을 둘 다 등록하면 위 재생목록 순환 기능으로 번갈아 재생됨.

---

## 6. 남은 작업 (에디터·콘텐츠 — 다음 세션에서 진행)

1. **`0.Title` 씬의 `NetworkManager` GameObject**(SteamManager/GameLocalizationBootstrap과 같은 자리)에
   `GameSettingsManager`, `BGMManager` 컴포넌트 추가.
2. `BGMManager.Zone Clips`에 `"M."`/`"T."` 접두사 + 곡 연결(§5 참고, 사용자가 어느 쪽에 어느 곡 쓸지 아직
   미정 — 결정 필요).
3. **옵션 패널 UI 프리팹 신규 제작**: 슬라이더 3개(마스터/BGM/SFX) + 언어 드롭다운 + 해상도 드롭다운 +
   화면모드 드롭다운. `OptionsMenuController` 부착 후 각 필드 연결.
4. 이 패널을 **타이틀 씬**(`TitleMenuController.settingsPanel`)과 **각 스테이지의 ESC 메뉴**
   (`EscMenuController.settingsPanel`, `UI.prefab` 쪽으로 추정)에 배치. 가능하면 같은 프리팹 재사용.
5. 각 패널의 "닫기" 버튼에 `OnClickCloseSettings()` 연결.
6. (선택) `M.Stage2`처럼 씬 안에 Phase가 여러 개인 스테이지에서 구간별 BGM이 필요하면, 해당
   `PhaseData.onPhaseEnter`에 `BGMManager.PlayClip()` 연결(§5).
7. `SFXLibrary.Volume Overrides`는 실제 플레이해보면서 소리 크기 안 맞는 것부터 채워나가는 방식으로 진행
   (§4).

## 7. 테스트 체크리스트 (위 배치 완료 후)

- [ ] 마스터/BGM/SFX 슬라이더가 실시간으로 소리 크기에 반영되는지 (BGM은 매 프레임 반영, SFX는 다음 재생부터)
- [ ] 슬라이더/화면/언어 값이 재실행 후에도 유지되는지 (PlayerPrefs)
- [ ] 타이틀 옵션 패널, 인게임 ESC 옵션 패널 둘 다 정상 동작하는지 (같은 GameSettingsManager 값 공유)
- [ ] 화면모드 전체화면/창모드/테두리없는창 전환 정상 동작, 테두리없는창일 때 해상도 드롭다운 비활성화되는지
- [ ] 해상도 변경이 실제 적용되는지
- [ ] 언어 드롭다운 선택 시 즉시 텍스트가 바뀌는지(연결된 로컬라이즈 텍스트 한정) + 재실행 후에도 그 언어 유지되는지
- [ ] BGM이 M구역/T구역 이동 시 자연스럽게 크로스페이드되는지, 같은 구역 내 스테이지 이동 시 안 끊기는지
- [ ] (해당 시) `M.Stage2` OX퀴즈→화살함정 구간 전환에서 BGM이 바뀌는지

---

## 8. 참고 — 나중에 옵션 메뉴에 추가될 수 있는 항목 (지금 범위 아님)

사용자가 언급했지만 이번 작업 범위에서 제외, 나중에 필요 시 지금 구조 위에 자연스럽게 얹을 수 있음:

- 밝기 조절
- 입력장치/출력장치 선택 (드롭다운)
- 마이크 볼륨 조절 (+ 소음 차단 기능 여부는 별도 검토)
- 마이크 음소거 on/off
- 팀원 보이스 크기 조절(수신 음량)

`CheerAndTutorialDesign.md` §8.2에 "마이크 mute, 수신 볼륨 — 최소 구현 OK. 옵션 패널(마스터·BGM·SFX)은
Ship Must"라고 이미 명시돼 있어 우선순위상 지금 범위(마스터/BGM/SFX/화면/언어)가 먼저.
