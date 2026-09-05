# 사운드(BGM/SFX) · 옵션(설정) 메뉴 — 설계·구현 현황

`ReleaseRoadmap.md` §4 순위 5~7("BGM 추가" / "옵션·설정 메뉴" / "SFX 마무리 + BGM 음량 조절")의 상세 구현 문서.
**옵션 패널 UI는 타이틀 씬에 실제로 배치·연결 완료(로컬라이제이션 포함). BGM 곡 배정은 §5에 "완료"로
기록돼 있었으나 2026-08-28에 확인해보니 실제로는 비어있음(stale, §10.1 참고) — 재배정 필요.
2026-08-10 세션에서 발견된 버그 8개 중 7개 수정 완료, 1개는 의도적 보류(§9.2/§9.4 참고). 2026-08-11 세션에서
마이크 음소거·입력장치 선택 구현 완료, 밝기·출력장치는 스코프 제외 확정(§8/§9.5), 팀원별 수신 볼륨
(Dissonance 연동)도 코드 구현 완료(§9.6). 2026-08-28 세션에서 BGM 16곡 정규화(AudacityMCP) 완료,
SFX 정규화 방침 확정, SFX 1차 22개 피크 정규화 완료(`SFX2`→`SFX3`), 압축은 최종 단계로 보류(§10),
사운드 청음 도구(`SoundAuditionTool`) 신규(§10.3). 2026-08-29 세션에서 3D SFX 감쇠가 맵 스케일에
안 맞는 문제 발견, 2D/3D 재분류 6곳 전부 완료(DropTrap×2/SpikeLaneField/SpikeTrap/WindTrap 임펄스 →
2D 전환, `ArrowTrap` 화살 발사음은 논의 후 완전 제거, §11).

> **다음 에이전트/세션 시작 지침 (최신, 2026-08-29):** §11의 SFX 2D/3D 재분류는 전부 완료됨. 이제
> **§10 "다음 세션 체크리스트"** 순서로 진행(BGM `zoneClips` 재배정 등). 그 이전(§9.2~9.6)에 남은 옵션
> 메뉴/마이크 작업들은 §6 "남은 작업" 참고 — 우선순위는 §10 → §6 순. 코드 쪽에 추가 변경이 필요한
> 상황이 아니면 이 문서의 아키텍처를 재설계하지 말 것 — 사용자와 합의된 구조임(AudioMixer 미사용 등,
> 아래 §1 참고).
>
> **Unity MCP 쓰기 권한:** 워크스페이스 `.cs` 파일 수정은 항상 허용. 씬/프리팹 배치 등 에디터 MCP
> **쓰기**는 사용자가 "MCP로 해줘"라고 명시할 때만 — `.cursor/rules/unity-mcp-readonly.mdc` 참고.
> **Audacity MCP**(`user-audacity` 네임스페이스, `~/.cursor/mcp.json`에 `audacity` 항목으로 이미 등록됨)는
> 이 규칙의 적용 대상이 아님(Unity 에디터가 아니라 Audacity 제어용) — 오디오 정규화 작업엔 자유롭게 사용.
> 단, Audacity를 미리 켜두고 `mod-script-pipe`가 연결돼 있어야 하며, `project_new`/`project_close`를
> 연달아 호출하면 크래시하니 트랙 삭제(`track_select`+`track_remove`)로 대체할 것(§10.1 참고).

### 다음 세션 체크리스트 (2026-08-28 세션 종료 시점, 컨텍스트 소진으로 핸드오프) — 최우선

**이어서 할 순서:**

1. **[사용자 작업]** SFX 원본 자르기 — **1차 22개 완료** (`C:\Users\u\Desktop\Unity\NoAISound\SFX2\`).
   부족한 클립은 사용자가 이어서 자른 뒤 같은 폴더(또는 별도 경로)를 알려줄 것.
2. ~~SFX 피크 정규화~~ — **✅ 1차 22개 완료(2026-08-28).** AudacityMCP `normalize`(-3dB, DC 제거,
   LUFS/리미터 안 씀) → `C:\Users\u\Desktop\Unity\NoAISound\SFX3\`에 같은 파일명 WAV 저장. 모노 원본
   3개(`AdvancingWall_Moving`, `BossPhasetrans_Mouth`, `Runner_Captured`)는 모노로, 나머지 19개는
   스테레오로 export. 추가분이 오면 같은 패턴으로 SFX3에 이어서 넣으면 됨.
3. ~~BGM 나머지 14곡을 `Assets/Audio/BGM/`으로 반입~~ — **✅ 완료 확인(2026-08-28, MD5 비교)**. 16곡
   전부(`Assets/Audio/BGM/*.wav`)가 `BGM2\`의 정규화 결과물과 MD5 100% 일치 — §10.1에 "2곡만 반입"으로
   적혀있던 건 stale, 실제로는 이미 전부 반입돼 있었음.
4. `Assets/Prefab/NetworkManager.prefab`의 `BGMManager.zoneClips`가 **비어있음**(§10.1에서 발견, §5 표는
   stale) — 사용자가 Inspector에서 직접 재배정 필요(MCP 쓰기 금지 원칙, §9.4-⑧ 당시엔 채워져 있었다고
   기록됐으나 지금은 아님 — 원인 불명, 재조사 불필요, 그냥 다시 채우면 됨).
5. BGM+SFX 다 넣고 실제 플레이로 같이 들어보면서 `SFXLibrary.VolumeOverride`(§4, §6-7) 밸런스 튜닝.
6. **가장 마지막**: 전체 밸런스 확정되면 Import Settings 일괄 압축(BGM: Vorbis+Streaming, SFX: 상황별,
   §10 참고) — 그 전에는 압축하지 말 것(음질 재작업 비효율 방지).

이 순서 끝나면 §6 "남은 작업"(ESC 메뉴, SFX Volume Override 튜닝은 5번과 겹침) 마무리로 복귀.

---

### 다음 세션 체크리스트 (2026-08-11 세션 종료 시점, 컨텍스트 소진으로 핸드오프)

**코드 쪽은 전부 완료·린트 통과 상태.** 아래는 순서대로.

1. **[사용자 확인 필요, 최우선]** 사용자가 아래 두 에디터 툴을 재실행했는지 확인부터 할 것 — 안 했으면
   먼저 안내:
   - `Tools > Setup Setting Panel (Title)` — 새 마이크 입력장치 드롭다운(`Row_InputDevice` 실동작화) +
     마이크 음소거 토글(`Row_MicMute`, `ToggleSpriteSwap` 부착) 반영, `Row_Brightness`/`Row_OutputDevice`
     제거 반영. **주의: `ContentRoot`/`Footer`를 통째로 지우고 재생성함** — 재실행 전 수동 레이아웃 조정이
     있었는지 사용자에게 먼저 물어볼 것.
   - `Tools > Setup Setting UI Localization` — 위 재생성된 UI에 `LocalizeStringEvent` 재부착 + 새
     String Table 엔트리 반영.
   - 재실행 후 `read_console`(MCP, 읽기 전용)로 컴파일 에러 없는지, `Setting_Panel` 하위에
     `Row_InputDevice`/`Row_MicMute`가 정상 생성됐는지 확인.
2. ~~[설계 결정 필요] §6-8 팀원별 수신 볼륨~~ — **사용자가 대안 A(스키마 추가) 채택, 구현 완료(§9.6).**
   실제 멀티플레이(2인 이상)에서 볼륨 슬라이더가 상대 목소리 크기를 실제로 바꾸는지 테스트 필요.
3. ~~§6의 3·4번 ESC/로비 설정 패널 배치~~ — **MCP로 완료**(2026-08-11). `Setting_Panel.prefab`
   공유 + Title/Lobby/`UI.prefab` 연결 검증됨.
4. ~~§6-10 `Btn_Reset` onClick~~ — Prefab에서 `OptionsMenuController.OnClickReset` 연결 완료.
   §6-9 `Default*Volume` 수치 튜닝은 아직(플레이 후 조절).
5. 그 외 §6 잔여 항목(7. SFX 볼륨 보정 실사용 튜닝, 6. Phase별 BGM 선택사항).

**이번 세션에 변경된 파일 전체 목록**(§2/§9.5/§9.6에 상세, 전부 린트 통과):
`GameSettingsManager.cs`(마이크 음소거/장치 추가), `OptionsMenuController.cs`(마이크 UI 연결),
`CheerKeywordEngine.cs`(솔로 폴백 장치 선택 반영), `SetupSettingPanel.cs`(Row_Brightness/OutputDevice 제거,
Row_InputDevice 실동작화, ToggleSpriteSwap 부착), `SetupSettingUILocalization.cs`(죽은 String Table 엔트리
제거), `ToggleSpriteSwap.cs`(신규), `LobbyPlayerState.cs`(VoiceId 필드 추가), `LobbyNetworkManager.cs`
(VoiceId self-report RPC·세션 배포), `GameSession.cs`(세션 VoiceId SSOT), `OptionsTeamVoicePanel.cs`
(Dissonance VoicePlayerState 실연동).

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
| `Assets/Scripts/Settings/GameSettingsManager.cs` (신규 / 수정 2026-08-11) | 볼륨·화면·언어·마이크(음소거/입력장치) SSOT. 싱글턴, DontDestroyOnLoad. PlayerPrefs 저장/로드. `ResetToDefaults()`(§9.4), `SetMicMuted()`/`SetMicDevice()`(§9.5, `DissonanceComms` 바인딩). |
| `Assets/Scripts/Audio/BGMManager.cs` (신규) | 씬 이름 접두사 기반 BGM 재생 + 크로스페이드(1.5초). `PlayClip(AudioClip)`으로 같은 씬 안 Phase별 강제 전환도 지원(§5). |
| `Assets/Scripts/Audio/SFXManager.cs` (수정) | `EffectiveVolume`이 `GameSettingsManager` 우선 사용(없으면 기존 Inspector 필드 폴백). `SFXLibrary.GetVolumeMultiplier(id)` 반영. |
| `Assets/Scripts/Audio/SFXLibrary.cs` (수정) | `VolumeOverride[]` 배열 추가 — 클립별 0~2배 보정(§4). |
| `Assets/Scripts/Audio/PlayerAudio.cs` (수정) | 달리기 루프 사운드가 SFX 마스터 볼륨을 매 프레임 반영하도록 수정(기존엔 전혀 반영 안 되던 버그성 gap). |
| `Assets/Scripts/Localization/GameLocalizationBootstrap.cs` (수정) | 옵션에서 저장한 수동 언어(`ManualLocaleOverrideKey`)가 있으면 Steam/systemLanguage 자동감지보다 최우선 적용. |
| `Assets/Scripts/UI/OptionsMenuController.cs` (신규 / 수정 2026-08-11) | 슬라이더 3개 + 언어/해상도/화면모드 드롭다운 + 마이크 음소거 토글·입력장치 드롭다운 ↔ `GameSettingsManager` 연결(§9.5). 타이틀·ESC 메뉴 양쪽 재사용 가능. 화면모드 드롭다운 라벨 3개도 `LocalizedString` 필드로 로컬라이즈(§9.1). |
| `Assets/Scripts/UI/EscMenuController.cs` (수정) | 미구현이던 Setting 버튼에 `OnClickSettings()`/`OnClickCloseSettings()` 추가(`TitleMenuController`와 동일 패턴). |
| `Assets/Scripts/UI/OptionsPanelTabs.cs` (신규, 2026-08-10 / 수정 2026-08-11) | 옵션 패널 좌측 탭(일반/사운드/팀보이스) 전환 — 탭 버튼 클릭 시 콘텐츠 패널 토글 + 선택/비선택 스프라이트 교체. 마지막으로 본 탭을 `static` 필드로 기억해 재오픈 시 유지(§9.2-⑤ 수정 완료). |
| `Assets/Scripts/UI/OptionsTeamVoicePanel.cs` (신규, 2026-08-10 / 수정 2026-08-11) | 팀 보이스 탭 — 고정 이름(GUMA/DANHO/SOOK) 대신 `GameSession`(인게임) 또는 `LobbyNetworkManager`(로비)에서 팀원 Steam 표시 이름을 읽어 슬롯에 표시. 수신 볼륨 슬라이더가 `VoiceId`로 `DissonanceComms.FindPlayer()`를 조회해 실제 `VoicePlayerState.Volume`에 연동됨(§9.6). |
| `Assets/Scripts/Network/LobbyPlayerState.cs` (수정 2026-08-11) | `VoiceId`(Dissonance `LocalPlayerName`) 필드 추가 — `DisplayName`과 동일한 self-report 패턴(§9.6). |
| `Assets/Scripts/Network/LobbyNetworkManager.cs` (수정 2026-08-11) | `SubmitVoiceIdServerRpc` + `ReportLocalVoiceIdRoutine`(Host/Client 공통, Dissonance 준비 대기 후 보고) + `StartGameServerRpc`에서 세션 VoiceId 확정·배포(`SyncVoiceIdsClientRpc`) 추가(§9.6). |
| `Assets/Scripts/GameSession.cs` (수정 2026-08-11) | `SetSessionVoiceIds`/`GetSessionVoiceId` 추가 — `DisplayName`과 동일한 세션 SSOT 패턴(§9.6). |
| `Assets/Scripts/UI/SliderValuePercentLabel.cs` (신규, 2026-08-10) | `Slider` 값을 옆 텍스트에 "70%" 형태로 표시하는 범용 컴포넌트. |
| `Assets/Scripts/UI/ToggleSpriteSwap.cs` (신규, 2026-08-11) | `Toggle` on/off 상태에 따라 `Image.sprite` 교체하는 범용 컴포넌트. 예전 에디터 툴의 비영속 `AddListener` 클로저 버그(§9.2-③) 대체. |
| `Assets/Scripts/Cheer/CheerKeywordEngine.cs` (수정 2026-08-11) | 솔로(1인) 마이크 폴백 경로(`StartSoloMic`/`PollSoloMic`/`Shutdown`)가 옵션에서 고른 마이크 장치(`GameSettingsManager.MicDeviceName`)를 쓰도록 수정(기존엔 `null`=시스템 기본 하드코딩). |
| `Assets/Editor/SetupSettingPanel.cs` (신규, 2026-08-10 / 수정 2026-08-11) | `TitleCanvas/Setting_Panel` 하위 구조(탭 3개, 콘텐츠 3종, Footer 버튼) 생성 + `OptionsPanelTabs`/`OptionsMenuController` 부착·필드 연결하는 1회성 에디터 툴(`Tools > Setup Setting Panel (Title)`). Footer는 이제 `Btn_Reset`만 생성(취소/적용 버튼 생성 코드 제거, §9.2-② 수정 완료). `CloseButton` 이름 불일치는 사용자가 씬에서 직접 통일(§9.2-④ 해결). `Row_Brightness`/`Row_OutputDevice` 생성 제거, `Row_InputDevice` 실동작화, `Row_MicMute`가 `ToggleSpriteSwap` 부착(§9.5). |
| `Assets/Editor/SetupSettingUILocalization.cs` (신규, 2026-08-10 / 수정 2026-08-11) | `SettingUI` String Table 생성·번역 채우기 + 패널의 `TextMeshProUGUI`에 `LocalizeStringEvent` 부착 + 팀보이스 슬롯 재구성까지 하는 1회성 에디터 툴(`Tools > Setup Setting UI Localization`). 죽은 분기(`TeamVoice.Slot`) 제거(§9.2-⑥ 수정 완료). `Settings.Brightness`/`Settings.OutputDevice` 엔트리 제거(§9.5). |
| `Assets/Localization/StringTables/SettingUI*.asset` (신규, 2026-08-10) | 옵션 패널 UI 텍스트 전용 String Table Collection. 12개 로케일(한국어 포함 en/ja/ko/zh-Hans/zh-Hant/de/es/es-419/fr/pl/pt-BR/ru) 번역 수록(§9.1). |

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

**배정 완료(2026-08-11 MCP 조회로 확인)** — `0.Title` 씬 `NetworkManager`의 `BGMManager.zoneClips`에 이미
아래처럼 채워져 있음:

| `scenePrefix` | 곡 |
|---|---|
| `M` (구역 공통) | M1~M5.wav (5곡 순환) |
| `M.Boss` | M.Boss1~3.wav (3곡 순환) |
| `T.Stage1` | T.1,3.wav |
| `T.Stage2` | T.2.wav |
| `T.Stage3` | T.1,3.wav |
| `T.Stage4` | T.4.wav |
| `T.Stage5` | T.5.wav |
| `T.Boss` | T.Boss1~2.wav (2곡 순환) |

위 §5 본문의 "말랑말랑한_발걸음.mp3" / "Gums and Gullet.wav" 임시 배치 설명은 이 실제 배정 이전 상태를
설명한 것 — 지금은 위 표가 실제 상태.

---

## 6. 남은 작업 (에디터·콘텐츠 — 다음 세션에서 진행)

**§9.2 버그 수정은 §9.4에서 대부분 완료.** 이제 아래 목록이 우선순위.

1. ~~`0.Title` 씬의 `NetworkManager` GameObject에 `GameSettingsManager`, `BGMManager` 컴포넌트 추가~~ —
   **완료 확인**(2026-08-11 MCP 조회, §9.4 참고). 둘 다 부착돼 있음.
2. ~~`BGMManager.Zone Clips`에 `"M."`/`"T."` 접두사 + 곡 연결~~ — **완료 확인**(2026-08-11 MCP 조회, §5
   표 참고). M/M.Boss/T.Stage1~5/T.Boss 전 구역 곡 배정 끝남.
3. ~~옵션 패널 UI~~ **타이틀·로비·인게임(ESC) 전부 완료**(2026-08-11). `Assets/Prefab/Setting_Panel.prefab`
   공유 프리팹 — Title `TitleCanvas` / Lobby `TitleCanvas` / `UI.prefab` 각각 인스턴스.
   - Title: `TitleMenuController.settingsPanel` + 설정 버튼(기존)
   - Lobby: `LobbyMenuController.settingsPanel` + `Option` → `OnClickSettings`
   - ESC: `EscMenuController.settingsPanel` + `Btn.Setting` → `OnClickSettings`
   닫기(X)/초기화는 패널 자체 `OptionsMenuController.OnClickClose`/`OnClickReset`에 연결(씬별 컨트롤러 불필요).
4. ~~ESC 메뉴 닫기 연결~~ — 위 3번과 함께 완료(`OptionsMenuController.OnClickClose`).
5. ~~각 패널의 "닫기" 버튼~~ — Prefab 공통으로 `OptionsMenuController.OnClickClose`에 통일 완료.
6. (선택) `M.Stage2`처럼 씬 안에 Phase가 여러 개인 스테이지에서 구간별 BGM이 필요하면, 해당
   `PhaseData.onPhaseEnter`에 `BGMManager.PlayClip()` 연결(§5).
7. `SFXLibrary.Volume Overrides`는 실제 플레이해보면서 소리 크기 안 맞는 것부터 채워나가는 방식으로 진행
   (§4).
8. ~~`OptionsTeamVoicePanel`의 수신 볼륨 슬라이더 → Dissonance 실제 볼륨 반영 연동~~ — **✅ 구현
   완료(§9.6).** `LobbyPlayerState.VoiceId` self-report 필드 추가(대안 A 채택, §9.6 참고) + 슬라이더를
   `DissonanceComms.FindPlayer(voiceId).Volume`에 직접 연결. **실제 멀티(2인 이상)에서 상대 목소리 크기가
   실제로 바뀌는지 테스트 필요**(§7 체크리스트에 추가).
9. `GameSettingsManager` Inspector의 `Default Master/Bgm/Sfx Volume` 필드를 실제 원하는 수치로 조정 —
   완료 여부 미확인, 다음 세션에서 확인.
10. `Setting_Panel`의 `Btn_Reset` → `OnClick()`에 `OptionsMenuController.OnClickReset` 연결 — 완료 여부
    미확인, 다음 세션에서 확인.
11. ~~`Row_MicVolume`(마이크 게인/노이즈 게이트)는 아직 placeholder 슬라이더~~ — **구현 완료 (2026-09-01).** `GameSettingsManager.MicVolume` + `VoiceBroadcastTrigger.ActivationFader`. Cheer/Vosk 캡처는 그대로.

## 7. 테스트 체크리스트 (위 배치 완료 후)

- [ ] 마스터/BGM/SFX 슬라이더가 실시간으로 소리 크기에 반영되는지 (BGM은 매 프레임 반영, SFX는 다음 재생부터)
- [ ] 슬라이더/화면/언어 값이 재실행 후에도 유지되는지 (PlayerPrefs)
- [ ] 타이틀 옵션 패널, 인게임 ESC 옵션 패널 둘 다 정상 동작하는지 (같은 GameSettingsManager 값 공유)
- [ ] 화면모드 전체화면/창모드/테두리없는창 전환 정상 동작, 테두리없는창일 때 해상도 드롭다운 비활성화되는지
- [ ] 해상도 변경이 실제 적용되는지
- [ ] 언어 드롭다운 선택 시 즉시 텍스트가 바뀌는지(연결된 로컬라이즈 텍스트 한정) + 재실행 후에도 그 언어 유지되는지
- [ ] BGM이 M구역/T구역 이동 시 자연스럽게 크로스페이드되는지, 같은 구역 내 스테이지 이동 시 안 끊기는지
- [ ] (해당 시) `M.Stage2` OX퀴즈→화살함정 구간 전환에서 BGM이 바뀌는지
- [ ] **(신규, §9.6)** 실제 멀티(2인 이상)에서 팀 보이스 탭 수신 볼륨 슬라이더를 움직이면 상대방 목소리
      크기가 실제로 바뀌는지 (로비/인게임 둘 다) — `VoiceId` 매칭이 안 되면 슬라이더가 비활성화됨(정상,
      Dissonance 보고가 아직 안 된 상태)

---

## 8. 참고 — 옵션 메뉴 확장 항목 스코프 결정 (2026-08-11, §9.5)

- **밝기 조절 — 스코프 제외 확정.** `ReleaseRoadmap.md` §2.1 Ship Must UI 항목("옵션: 마스터·BGM·SFX,
  해상도/전체화면")에 없음, 이 게임은
  어두운 톤의 장르가 아님, 구현하려면 각 스테이지 씬에 이미 있는 개별 `Global Volume Profile`과 충돌 안
  나게 별도 상위 우선순위 Volume 인프라를 새로 깔아야 해서 비용 대비 실익 낮음. 플레이테스트에서 특정
  구간 시야 불만이 나오면 그때 재검토(조명 자체를 손볼 문제일 수도 있음).
- **출력장치(헤드셋) 선택 — 기술적으로 불가능, 스코프 제외 확정.** Unity `AudioSource`/`AudioListener`는
  OS 기본 출력 장치로만 재생되고, 이걸 게임에서 바꾸는 표준 API가 없음(네이티브 플러그인 없이는 불가).
  Windows 사운드 설정에서 사용자가 직접 바꾸도록 안내.
- **마이크 입력장치 선택 — 구현 완료(§9.5).** `DissonanceComms.MicrophoneName` / `GetMicrophoneDevices()`
  기반. 멀티에서는 Dissonance가 마이크 소유자라 여기로 바로 반영, 솔로 폴백 경로(`CheerKeywordEngine`)도
  같은 선택값을 씀.
- **마이크 음소거 on/off — 구현 완료(§9.5).** `DissonanceComms.IsMuted` 바인딩. 네트워크 전송(인코더)만
  끊고 로컬 캡처는 유지되므로 응원 키워드 감지(Cheer)에는 영향 없음(코드 확인함).
- **마이크 볼륨(게인) 조절 — 구현 완료 (2026-09-01).** `GameSettingsManager.MicVolume`(PlayerPrefs `Settings.MicVolume`, 기본 1). 슬라이더는 `OptionsMenuController`가 `Row_MicVolume`을 찾아 연결(Inspector 미연결 폴백). 적용은 로컬 `VoiceBroadcastTrigger.ActivationFader.Volume`(상대가 듣는 송신 게인). Dissonance 로컬 `VoicePlayerState.Volume` setter는 미지원이라 쓰지 않음. CheerKeywordEngine/Vosk 캡처 레벨은 바꾸지 않음.
  - **후속 수정(같은 날, 스폰 타이밍 버그).** 최초 구현은 `ApplyMicTransmitVolume()`이
    `NetworkManager.LocalClient.PlayerObject`로 트리거를 다시 찾았는데, NGO
    `NetworkSpawnManager`가 이 필드를 `InvokeBehaviourNetworkSpawn()`(= `OnNetworkSpawn`, 즉
    `NetworkPlayerSetup.SetupOwner()`가 실행되는 그 안) **이후**에 채운다는 걸
    `Library/PackageCache` 소스 대조로 확인함 — 그래서 스폰 시점 호출은 매번 no-op되어
    저장된 마이크 볼륨이 재접속 직후 자동으로 반영 안 되고(기본 100%로 송신), 옵션 창을
    열어 슬라이더를 한 번 만져야만(그때는 PlayerObject가 이미 채워짐) 적용되는 문제가 있었음.
    `ApplyMicTransmitVolume(VoiceBroadcastTrigger)` 오버로드를 추가해 `SetupOwner()`가 이미
    캐시해둔 `_voiceBroadcast` 참조를 직접 넘기도록 수정 — 타이밍 문제 자체를 우회.
- **팀원 보이스 수신 볼륨 조절 — 구현 완료(§9.6).** Dissonance `VoicePlayerState.PlayerId`가 세션마다
  랜덤 GUID라 팀원 슬롯과 매칭이 안 되는 신원 문제가 있었는데, 사용자가 (필드 추가 없이 우회하는 대안보다)
  `LobbyPlayerState`에 self-report 필드(`VoiceId`)를 추가하는 쪽을 채택해 해결(§9.6). 개별 음소거(mute)
  버튼은 이번엔 추가 안 함 — 필요해지면 같은 `VoicePlayerState.IsLocallyMuted`로 후속 가능.

---

## 9. 2026-08-10 세션 결과 — Setting_Panel 실제 구현 + 로컬라이제이션 + 팀보이스 Steam화

이 세션에서 사용자가 `TitleCanvas` 아래 만들어둔 `Setting_Panel`을 기반으로 MCP(에디터 스크립트 경유)로
실제 UI 계층·컴포넌트를 구성했고, 로컬라이제이션과 팀 보이스 동적 표시까지 완료했다. **컨텍스트 소진으로
버그 수정 전에 세션 종료** — 다음 세션은 §9.2부터 처리할 것.

### 9.1 이번 세션에서 완료한 것

- **`Assets/Editor/SetupSettingPanel.cs`** (`Tools > Setup Setting Panel (Title)`)로 `Setting_Panel`
  하위에 다음을 생성·연결:
  - 기존에 있던 탭 버튼 3개를 `Tab_General`/`Tab_Sound`/`Tab_TeamVoice`로 식별해 재사용(단, 씬 안 실제
    이름은 스크립트가 기대하는 이름과 다를 수 있음 — §9.2-④ 참고).
  - `ContentRoot` 하위에 일반/사운드/팀보이스 콘텐츠 3종(슬라이더 행, 드롭다운 행, 토글 행 등).
  - `Footer`에 취소/적용/초기화 버튼, 우상단에 `Close.Btn`(X).
  - `OptionsPanelTabs`, `OptionsMenuController` 컴포넌트 부착 + Inspector serialized 필드 전부 코드로
    연결(슬라이더 3개, 드롭다운 3개, 탭 배열, Figma 배경 이미지 등).
- **`Assets/Editor/SetupSettingUILocalization.cs`** (`Tools > Setup Setting UI Localization`)로:
  - `Assets/Localization/StringTables/SettingUI*.asset` String Table Collection 신규 생성, 12개 로케일
    번역(예: "마스터 볼륨", "BGM 볼륨", "SFX 볼륨", "언어", "해상도", "화면 모드", "전체화면"/"창모드"/
    "테두리 없는 창모드", "취소"/"적용"/"초기화", "닫기" 등) 채움.
  - `Setting_Panel` 하위 모든 `TextMeshProUGUI`를 순회하며 `LocalizeStringEvent` 컴포넌트를 부착하고
    `SettingUI` 테이블의 해당 키에 연결(라벨↔키 매핑은 스크립트 내 `LabelBindings` 딕셔너리).
  - `OptionsMenuController`의 화면모드 드롭다운 라벨 3개(`LocalizedString` 필드)와 `OptionsTeamVoicePanel`
    필드도 같은 실행에서 함께 연결.
- **`Assets/Scripts/UI/OptionsTeamVoicePanel.cs`** 신규 작성 — 팀 보이스 탭이 더 이상 GUMA/DANHO/SOOK
  고정 이름을 쓰지 않고, 인게임이면 `GameSession`, 로비면 `LobbyNetworkManager`에서 팀원(본인 제외) Steam
  표시 이름을 최대 3명까지 읽어 슬롯에 채움. 팀원이 없으면(솔로) `emptyState` 표시.
- `OptionsMenuController`에 `LocalizationSettings.SelectedLocaleChanged` 구독 추가 — 언어 변경 시 화면모드
  드롭다운 라벨도 즉시 갱신되도록 처리.

### 9.2 리뷰에서 발견한 버그·이슈 (§9.4에서 처리 완료 — 아래는 발견 당시 기록)

읽기 전용 리뷰(씬/코드 조회만, 쓰기 없음)로 찾은 것 — 우선순위 높은 순. **처리 결과는 각 항목 끝의 상태
표시와 §9.4 참고.**

1. **`Btn_Reset`(초기화) 버튼이 완전히 비어있음** — `onClick` 리스너가 하나도 안 걸려있어 눌러도 아무 동작
   안 함. `GameSettingsManager`에 초기값 리셋 메서드가 있는지 확인하고, 없으면 추가해서 연결 필요.
   **→ ✅ 수정 완료(§9.4-①).**
2. **`Close.Btn`(X) / `Btn_Cancel`(취소) / `Btn_Apply`(적용) 세 버튼이 전부 동일하게
   `OnClickCloseSettings()`만 호출** — 지금 구조가 "값 변경 즉시 적용(pull 방식, §1)"이라 "취소"를 눌러도
   되돌릴 값이 없고 "적용"도 실질적으로 아무것도 안 하고 닫기만 함. 라벨과 실제 동작이 안 맞아 사용자가
   혼란스러울 수 있음. **결정 필요:** (a) 취소/적용 버튼을 없애고 닫기 버튼 하나로 통일, 또는 (b) 진짜
   "취소 시 되돌리기" 기능을 만들려면 패널 열 때 스냅샷을 떠야 함(설계 변경 필요 — 사용자와 상의 후 진행).
   **→ ✅ (a) 채택해 수정 완료(§9.4-②).**
3. **마이크 음소거 토글(`Row_MicMute`)의 스프라이트 교체 리스너가 비영속(non-persistent) C# 클로저로
   `AddListener`됨** — 씬 저장 시 직렬화되지 않으므로, 도메인 리로드 후나 빌드된 게임에서는 토글을 눌러도
   on/off 스프라이트가 안 바뀜(기능 자체는 동작하되 시각 피드백만 깨짐). `SetupSettingPanel.cs`에서 이
   부분을 영속 리스너(별도 컴포넌트 메서드로 빼서 `UnityEvent.AddListener`가 아니라 Inspector에서 연결
   하거나, 전용 `ToggleSpriteSwap` 같은 작은 컴포넌트를 새로 만들어 부착)로 바꿔야 함.
   **→ ⏸ 의도적 보류(§9.4-③) — 지금은 `interactable = false`인 비활성 placeholder라 실질 영향 없음. 마이크
   음소거를 실제로 구현하는 시점에 재검토.**
4. **닫기 버튼 이름 불일치** — `SetupSettingPanel.cs`는 `CloseButton`이라는 이름으로 GameObject를
   생성/탐색하는데, 실제 씬에는 `Close.Btn`이라는 이름으로 존재(사용자가 미리 만들어둔 것으로 추정).
   `SetupSettingUILocalization.cs`의 `LabelBindings`는 두 이름 다 대응해놨지만, `SetupSettingPanel.cs`의
   "기존 자식 파괴 후 재생성" 로직은 `CloseButton`만 찾기 때문에 **`Tools > Setup Setting Panel (Title)`을
   재실행하면 `Close.Btn`이 안 지워지고 새 `CloseButton`이 중복 생성될 위험**이 있음. 재실행 전에 반드시
   이 부분부터 고칠 것(이름 통일 또는 파괴 로직에 `Close.Btn`도 포함).
   **→ ✅ 해결(§9.4-④) — 사용자가 씬에서 직접 `CloseButton`으로 이름 통일.**
5. **옵션 패널이 열릴 때마다 항상 "일반" 탭으로 리셋됨** (`OptionsPanelTabs.OnEnable() → ShowTab(defaultTabIndex=0)`) —
   사용자가 "사운드" 탭에서 조정하다 닫고 다시 열면 또 "일반" 탭부터 시작. UX상 아쉬운 부분 — 마지막으로 본
   탭을 기억하게(static 필드 또는 `GameSettingsManager`에 저장) 개선하면 좋음(필수 아님, 사용자 판단).
   **→ ✅ 수정 완료(§9.4-⑤).**
6. **`SetupSettingUILocalization.cs`에 죽은 코드 존재** — `bindKey.StartsWith("Settings.TeamVoice.Slot")`
   분기가 있는데 실제로 이 접두사를 가진 키가 정의된 적이 없어 항상 false. 정리하거나, 팀보이스 슬롯 라벨도
   로컬라이즈 키 체계에 편입시키려면 이 분기를 실제로 쓰게 고쳐야 함.
   **→ ✅ 제거 완료(§9.4-⑥).**
7. **`OptionsMenuController.OnSelectedLocaleChanged`가 `_refreshing` 가드를 자체적으로 다시 구현** —
   기존 `RefreshAll()`이 이미 같은 패턴을 쓰고 있어서 코드가 중복됨. 기능상 버그는 아니고 정리 대상(사소).
   **→ ✅ `WithRefreshGuard` 헬퍼로 통합 완료(§9.4-⑦).**
8. **`GameSettingsManager`/`BGMManager`가 `0.Title` 씬의 `NetworkManager` GameObject에 실제로 부착됐는지
   미확인** — 이번 세션은 UI 작업만 진행, §6-1 항목을 확인 안 하고 넘어갔음. 다음 세션에서 씬 조회로 확인
   필요(없으면 부착부터).
   **→ ✅ 확인 완료(§9.4-⑧) — 둘 다 부착돼 있었고, BGM 곡 배정도 이미 끝나 있었음(§5 표).**

### 9.3 다음 세션 시작 순서 (2026-08-10 시점 기록 — §9.4에서 실행 완료)

1. §9.2의 1~4번(기능 결함) 먼저 수정 — 특히 4번(이름 불일치)은 `SetupSettingPanel.cs` 재실행 전 필수.
2. §9.2-2번은 사용자에게 (a)/(b) 중 선택받고 진행(설계 판단 필요, 임의 결정 금지).
3. §6의 남은 항목(ESC 메뉴 배치, BGM 곡 배정, SFX 볼륨 보정, Dissonance 연동) 순서대로 진행.
4. 전체 완료 후 §7 테스트 체크리스트로 검증.

### 9.4 2026-08-11 세션 결과 — §9.2 버그 수정

사용자와 버그별로 스펙을 확인해가며 하나씩 처리. 코드 변경분만 반영(씬/에디터 조작은 사용자가 직접 진행—
`unity-mcp-readonly.mdc` 원칙).

- **① 초기화 버튼:** `GameSettingsManager.ResetToDefaults()` 신규 — 볼륨은 새로 추가한 Inspector 필드
  `defaultMasterVolume`/`defaultBgmVolume`/`defaultSfxVolume`(0~1, 사용자가 나중에 원하는 수치로 튜닝 예정,
  지금은 1.0)로 복원, 화면은 현재 모니터 네이티브 해상도 + 전체화면(독점)으로, 언어는 수동 선택 해제 후
  Steam/systemLanguage 자동감지로 재적용. `GameLocalizationBootstrap`에 재적용용 `ReapplyAutoDetectedLocale()`
  public 메서드 신규 추가(에디터 전용 컨텍스트 메뉴도 이걸로 통일). `OptionsMenuController.OnClickReset()`
  신규 — 리셋 후 UI 새로고침까지 처리. **남은 사용자 작업:** Inspector 기본값 수치 조정(§6-9), `Btn_Reset`
  `OnClick()` 연결(§6-10).
- **② 취소/적용/닫기 불일치:** 사용자가 (a) 채택(취소/적용 버튼 제거, 닫기(X) 하나로 통일). `SetupSettingPanel.cs`
  Footer 생성부에서 `Btn_Cancel`/`Btn_Apply` 생성 및 wiring 코드 제거, `Btn_Reset`만 남김. 씬에 이미 있던
  `Btn_Cancel`/`Btn_Apply`는 사용자가 직접 Hierarchy에서 삭제.
- **③ 마이크 음소거 비영속 리스너:** ✅ **§9.5에서 실제로 수정 완료.** `ToggleSpriteSwap` 컴포넌트 신규 —
  에디터 툴이 `AddListener` 클로저로 직접 등록하던 걸 걷어내고, 필드 참조로 정상 직렬화되는 컴포넌트가
  자기 `OnEnable`에서 구독하는 방식으로 교체.
- **④ CloseButton/Close.Btn 이름 불일치:** 사용자가 씬에서 직접 이름을 `CloseButton`으로 통일해 해결.
- **⑤ 탭 항상 "일반"으로 리셋:** `OptionsPanelTabs`에 `static int s_lastTabIndex` 추가 — 마지막으로 본 탭을
  기억해 다음에 열 때도 유지(타이틀/ESC 패널 인스턴스 간에도 공유).
- **⑥ 죽은 코드:** `SetupSettingUILocalization.cs`에서 항상 false였던 `Settings.TeamVoice.Slot` 분기 제거.
- **⑦ `_refreshing` 가드 중복:** `OptionsMenuController`에 `WithRefreshGuard(Action)` 헬퍼 추가, `RefreshAll()`/
  `OnSelectedLocaleChanged()` 둘 다 이걸로 통일.
- **⑧ 컴포넌트 부착 미확인:** MCP로 `0.Title` 씬 `NetworkManager`를 조회해 `GameSettingsManager`/`BGMManager`
  둘 다 이미 부착돼 있음을 확인. 덤으로 `BGMManager.zoneClips`에 M/T 전 구역 곡이 이미 배정돼 있는 것도
  발견(§5 표에 반영, §6-2 완료 처리).

**다음 세션 시작 순서:** §6의 9·10번(Reset 기본값 수치 조정 + `Btn_Reset` 연결) 확인 후, 3·4번(ESC 메뉴
패널 배치)부터 진행.

### 9.5 2026-08-11 세션 결과 — 마이크 음소거·입력장치 선택 구현, 밝기·출력장치 스코프 확정

사용자가 "밝기/마이크 찾기/음소거/헤드셋 찾기는 어떻게 연동하냐"고 물어서, 각 항목의 실제 Unity/Dissonance
지원 여부를 코드로 확인한 뒤 항목별로 스코프를 확정하고 가능한 것부터 구현했다(§8 참고).

- **밝기:** 스코프 제외 확정(사용자 동의). §8 참고.
- **출력장치(헤드셋):** Unity 표준 API로 불가능함을 확인, 스코프 제외 확정(사용자 동의). §8 참고.
- **마이크 음소거 — 구현 완료.** `GameSettingsManager`에 `MicMuted`(PlayerPrefs `Settings.MicMuted`) +
  `SetMicMuted()` 추가, `DissonanceComms.IsMuted`에 바인딩. **중요 발견:** Dissonance의 `IsMuted`는
  `CapturePipelineManager.Update(muted, ...)`에서 인코더(네트워크 전송) 구독만 끄고 전처리기(로컬 캡처)는
  그대로 돌아가므로, 음소거를 켜도 `CheerKeywordEngine.SubscribeToRecordedAudio` 기반 응원 키워드 감지는
  영향 없음(코드 확인, `Assets/Plugins/Dissonance/Core/Audio/Capture/CapturePipelineManager.cs:165-225`).
  `OptionsMenuController.micMuteToggle` 필드 신규, `OnMicMuteChanged` 콜백 추가.
- **마이크 입력장치 선택 — 구현 완료.** `GameSettingsManager`에 `MicDeviceName`(PlayerPrefs
  `Settings.MicDevice`) + `SetMicDevice()` 추가, `DissonanceComms.MicrophoneName`(런타임 핫스왑 가능,
  `_started` 락 없음)에 바인딩. 드롭다운 목록은 `DissonanceComms.GetMicrophoneDevices()`(Dissonance 자체
  해석 우선, 실패 시 `Microphone.devices` 폴백)로 채움. **솔로(1인) 폴백 경로도 동일하게 처리** —
  `CheerKeywordEngine`이 Dissonance 오디오 미수신 시 직접 여는 `Microphone.Start(null, ...)`를
  `_soloMicDevice` 필드로 바꿔 옵션에서 고른 장치를 그대로 씀(`StartSoloMic`/`PollSoloMic`/`Shutdown` 전부
  일관되게 수정). `OptionsMenuController.micDeviceDropdown` 필드 신규, `OnMicDeviceChanged` 콜백 추가.
- **부수적으로 §9.2-③ 실제 수정:** 위 마이크 음소거를 실제 기능으로 만들면서, 예전에 "의도적 보류"였던
  `Row_MicMute` 토글의 비영속 리스너 버그도 같이 고쳤다. 새 `Assets/Scripts/UI/ToggleSpriteSwap.cs`
  컴포넌트가 on/off 스프라이트 교체를 전담(Inspector 필드 참조라 정상 직렬화, 매번 자기 `OnEnable`에서
  구독). `SetupSettingPanel.cs`의 `CreateToggleRow`가 이 컴포넌트를 부착하도록 교체, `interactable = false`
  및 "(미연동)" 라벨 제거.
- **`Row_OutputDevice`/`Row_Brightness` 생성 코드 제거**(`SetupSettingPanel.cs`), 관련 로컬라이제이션
  String Table 엔트리(`Settings.Brightness`/`Settings.OutputDevice`)도 `SetupSettingUILocalization.cs`에서
  제거. `Row_InputDevice` 드롭다운은 `stub: true` 해제해 실제 동작하도록 전환.
- **조사했지만 이번엔 구현 안 함 — 팀원별 음소거/수신 볼륨(§6-8):** `OptionsTeamVoicePanel`의 볼륨
  슬라이더를 Dissonance `VoicePlayerState.Volume`에 연결하면 될 것 같았으나, Dissonance
  `LocalPlayerName`(=`VoicePlayerState.PlayerId`)이 명시적으로 안 정하면 세션마다 랜덤 GUID로 자동
  생성됨(`DissonanceCommsImpl.cs` `Start()` 참고) — 팀원 슬롯(`LobbyPlayerState.ClientId`/`DisplayName`)과
  매칭할 방법이 없음. 해결하려면 `LobbyPlayerState`에 자기 자신의 Dissonance GUID를 self-report하는 필드를
  추가해야 함(기존 `DisplayName`/`CheerName` self-report 패턴과 동일 — `SubmitDisplayNameServerRpc` 참고).
  이건 NetworkList 동기화 스키마를 건드리는 변경이라 **사용자 확인 후 진행할 것**(§6-8에 상세 기록).

**다음 세션 시작 순서:** 사용자가 `Tools > Setup Setting Panel (Title)` → `Tools > Setup Setting UI
Localization` 재실행해서 새 마이크 드롭다운/토글을 씬에 반영한 뒤, §6-8(팀원별 음소거 스키마 변경 여부
확인) → §6 3·4번(ESC 메뉴 패널 배치) 순서로 진행.

### 9.6 2026-08-11 세션 결과 — 팀원별 수신 볼륨(Dissonance 연동) 구현

§9.5에서 조사만 하고 미룬 §6-8(팀원 수신 볼륨)을 이어서 진행. 두 가지 대안을 사용자에게 제시했다:

- **대안 A**: `LobbyPlayerState`에 필드 추가(스키마 변경) — self-report로 Dissonance ID를 그대로 보고.
- **대안 B**: 스키마 안 건드리고 `DissonanceComms.LocalPlayerName`을 접속 전에 우리가 이미 아는 값(Steam
  표시 이름)으로 선점 — 대신 `[DefaultExecutionOrder]`로 Awake/Start 순서를 강제해야 하는 새로운 타이밍
  위험이 생기고(§1에서 이미 겪은 문제와 같은 유형), Steam 닉네임 중복 이론상 가능성도 있음.

**사용자가 대안 A 채택.** 기존 `DisplayName`/`CheerName` self-report 패턴을 그대로 재사용:

- **`LobbyPlayerState.cs`**: `VoiceId`(`FixedString64Bytes`) 필드 추가, `NetworkSerialize`/`Equals`에 반영.
- **`LobbyNetworkManager.cs`**:
  - `SubmitVoiceIdServerRpc` 신규 — `SubmitDisplayNameServerRpc`와 동일 구조(슬롯 없으면 `_pendingVoiceIds`
    버퍼링).
  - `ReportLocalVoiceIdRoutine()` 코루틴 신규 — **Host/Client 구분 없이 `OnNetworkSpawn`에서 공통 호출**
    (DisplayName과 달리 Host도 슬롯 생성 시점에 즉시 값을 못 넣음 — Dissonance가 자기 `Start()`에서
    `LocalPlayerName`을 확정하는 시점이 늦을 수 있어서 폴링 필요). `DissonanceComms.GetSingleton()`과
    `LocalPlayerName`이 비어있지 않을 때까지 기다린 뒤 `SubmitVoiceIdServerRpc`를 최대 5회(1초 간격)
    재시도. Host가 자기 자신의 `[Rpc(SendTo.Server)]` 메서드를 직접 호출하는 것은 `SetReadyServerRpc`를
    `LobbyMenuController`가 Host/Client 구분 없이 호출하는 기존 패턴으로 이미 검증된 안전한 방식.
  - `StartGameServerRpc`에 세션 VoiceId 확정·배포 블록 추가(`DisplayName`과 동일 구조) —
    `SyncVoiceIdsClientRpc` 신규.
- **`GameSession.cs`**: `SetSessionVoiceIds`/`GetSessionVoiceId(colorIndex)` 추가 — `DisplayName`과 동일한
  세션 SSOT 패턴(인게임에서는 이 값을 읽음).
- **`OptionsTeamVoicePanel.cs`**: 팀원 목록 수집 시 이름과 함께 `VoiceId`도 같이 가져오도록 변경. 슬라이더
  `onValueChanged`를 `DissonanceComms.GetSingleton().FindPlayer(voiceId).Volume`에 직접 연결(신규
  `BindVolumeSlider` 헬퍼). `VoiceId` 매칭 실패(아직 미보고/미접속) 시 슬라이더 `interactable = false`로
  비활성화하고 100%로 표시 — 크래시 없이 조용히 저하.

**개별 음소거(mute) 버튼은 이번 범위에 포함 안 함** — 필요해지면 같은 `VoicePlayerState.IsLocallyMuted`로
후속 가능(구조는 이미 갖춰짐, `FindPlayer` 결과 재사용).

**다음 세션 시작 순서:** §7 체크리스트의 신규 항목(실제 멀티에서 수신 볼륨 슬라이더 동작 확인) → §6
3·4번(ESC 메뉴 패널 배치) → §6-9·10(Reset 기본값/`Btn_Reset` 연결 확인) 순서로 진행.

---

## 10. 오디오 에셋 파이프라인 — 정규화(BGM/SFX) + 압축 타이밍 정책 (2026-08-28)

**압축(Import Settings)은 지금 하지 않는다 — BGM·SFX 다 넣고 같이 들어본 다음, 최종적으로 한 번에 적용.**
잊지 않도록 여기 기록.

- **지금 단계:** BGM(16곡, WAV, 아래 처리 완료)과 앞으로 만들 SFX 전부 **비압축(WAV) 그대로** Unity에
  끌어다 넣고 테스트. 용량은 BGM만 해도 약 327MB인데, 지금 단계에선 문제 아님 — SFX까지 다 넣고 실제
  플레이하면서 다 같이 들어봐야 상대적 밸런스(발소리 vs 폭발음 등, §4 `VolumeOverride`로 미세 보정)를
  제대로 잡을 수 있기 때문에, 압축(음질 손실 있는 작업)을 지금 미리 하면 나중에 다시 들어보고 재작업할
  때 비효율적임.
- **최종 단계(빌드 직전):** 전체 사운드 다 들어보고 밸런스 확정되면, Import Settings 일괄 변경:
  - **BGM**: `Compression Format: Vorbis`, `Load Type: Streaming` (긴 트랙이라 메모리에 통째로 올리지
    않음).
  - **SFX**: 길이에 따라 다름 — 짧은 원샷은 `Decompress On Load` + `Compressed`(PCM 아님) 조합이 흔한
    선택, 루프/긴 SFX는 BGM처럼 `Streaming` 검토. 실제 적용 시점에 다시 상의.
  - Vorbis는 음악/효과음 음질 손실이 거의 안 들리면서 WAV 대비 10~15% 수준까지 용량 절감.

### 10.1 BGM 정규화 완료 (AudacityMCP 사용)

- **방법:** Audacity에 `AudacityMCP`(`mod-script-pipe` 경유 MCP 서버, `~/.cursor/mcp.json`에 `audacity`
  항목으로 등록됨) 연결해서, 각 원본 파일을 한 곡씩 임포트 → `select_all` → `loudness_normalize`(-16
  LUFS, `dual_mono: true`) → `limiter`(-1dB, `SoftLimit`) → `project_export_audio`(WAV) → 트랙 삭제 →
  다음 곡 반복. (주의: `project_new`/`project_close`를 연달아 호출하면 Audacity가
  `lib-track.dll`에서 크래시하는 버그 발견 — 트랙 삭제로 대체해서 회피.)
- **원본:** `C:\Users\u\Desktop\Unity\NoAISound\BGM1\` (16개 mp3) → **결과:**
  `C:\Users\u\Desktop\Unity\NoAISound\BGM2\`(정규화된 WAV, 파일명 동일) — 원본은 백업으로 남겨둠.
- **타겟 -16 LUFS를 고른 이유:** 도구(`loudness_normalize`)가 권장하는 3개 값(-16/-14/-11) 중, 팟캐스트·
  방송 기준인 -16이 SFX/보이스와 밸런스 잡기 좋다고 판단(스포티파이용 -14나 힙합/EDM용 -11보다 여유
  있음).
- **Unity 반입 상태 — ✅ 16곡 전부 완료 확인(2026-08-28, MD5 비교로 재확인).** `Assets/Audio/BGM/*.wav`
  16개 전부가 `BGM2\`의 정규화 결과물과 MD5 100% 일치. (직전 기록엔 "2곡만 확인, 14곡 미반입"이라
  적혀있었는데 재확인해보니 stale — 이미 전부 들어가 있었음. 언제 반입됐는지는 불명, 재조사 불필요.)
- **⚠️ §5 배정 표는 여전히 stale — `zoneClips` 비어있음 재확인(2026-08-28, prefab YAML 직접 확인,
  `zoneClips: []`).** §5에 "2026-08-11 MCP 조회로 확인 — zoneClips 배정 완료"라고 적혀있지만
  `Assets/Prefab/NetworkManager.prefab`의 `BGMManager.zoneClips`는 여전히 빈 배열. **16곡이 이미 다
  들어와 있으니 지금 바로 배정 가능한 상태** — 다음 세션(또는 사용자가 Inspector에서 직접)에서 §5 표
  대로 `scenePrefix`↔곡 매핑을 실제로 채우고 표를 실제 상태로 갱신할 것.

### 10.2 SFX 정규화 방침 (BGM과 다르게 처리)

BGM처럼 LUFS로 전부 통일하면 안 됨 — SFX는 종류별로 크기가 달라야 정상(발소리 < 폭발음). 결정된 방침:

- **LUFS/리미터 안 씀.** 대신 **피크(peak) 정규화**만 (`normalize` 툴, 목표 예: -3dB) — 서로 다른
  소스에서 받아온 SFX끼리 녹음 레벨이 들쭉날쭉한 것만 통일. 피크 정규화는 목표치를 안 넘으므로 리미터
  불필요.
- **"멀리서/가까이" 구분 불필요.** `SFXManager.Play(id, worldPosition)`가 `AudioSource.PlayClipAtPoint`를
  써서 Unity 엔진이 런타임에 3D 거리 감쇠를 자동 처리함(§0 AudioMixer 미사용 결정과 별개) — 파일 준비
  단계에서 "먼 소리는 작게, 가까운 소리는 크게" 미리 만들 필요 없음. 전부 같은 피크 기준으로 정규화.
- **종류별 상대 밸런스**(발소리 vs 폭발음 등)는 §4 `SFXLibrary.VolumeOverride`(0~2배, 줄이는 쪽은 무제한)
  로 사후 보정 — 실제 플레이해보면서 튜닝(§6-7, 아직 미착수).
- **작업 분담:** 자르기(원샷/루프 구분)는 사용자, 피크 정규화는 AudacityMCP.
  **1차 22개 완료(2026-08-28):** `SFX2\` → `normalize`(-3dB) → `SFX3\`. 추가 원본이 오면 같은 파이프라인
  재사용. Unity 반입·`SFXLibrary` 연결은 아직 아님(부족한 클립 채운 뒤).

### 10.3 사운드 청음/검증 도구 (2026-08-28, 신규)

`Assets/Editor/SoundAuditionTool.cs` — `Tools > Sound Audition Tool`. Play Mode에서 실제
`SFXManager`/`BGMManager` 파이프라인(옵션 볼륨·`VolumeOverride` 반영)으로 모든 SFX/BGM을 스테이지 이동 없이
바로 재생·확인하는 창. SFX는 `SFXId` 각각에 2D/3D(가상 거리 슬라이더) 버튼, BGM은 `Assets/Audio/BGM` 폴더
전체를 `zoneClips` 설정과 무관하게 즉시 크로스페이드 재생. **주의:** `SFXManager`/`BGMManager`는
`0.Title`에서만 생성되는 DontDestroyOnLoad 싱글턴이라, 3D 가상 거리 테스트는 **실제 스테이지 진입 후**
해야 의미 있음(타이틀 화면 카메라 기준으로 재면 스케일이 안 맞음).

---

## 11. SFX 2D/3D 재분류 — 맵 스케일에 안 맞는 3D 감쇠 (2026-08-29, 진행 중)

**배경:** 맵(스테이지) 자체가 작은데, 3D SFX 일부가 `AudioSource.PlayClipAtPoint`의 **Unity 기본 감쇠값
그대로**(코드에서 커스텀 안 함) 재생되고 있었음 — `minDistance=1m`, `maxDistance=500m`,
`rolloffMode=Logarithmic`(Unity 컴포넌트 기본값, 스크립트가 따로 안 정하면 이걸 씀). 이 커브는 500m 스케일
월드 기준이라, 대략 `gain ≈ minDistance / distance` 공식상 **5m만 떨어져도 이미 -14dB, 25m면 -28dB**로
꽤 급격히 죽음 — 작은 맵에서는 플레이어가 조금만 멀어져도 경고음이 거의 안 들리는 문제가 생길 수 있음
(Sound Audition Tool로 실측하다가 발견, 2026-08-29).

**코드 조사 결과 — 두 그룹으로 나뉨:**

1. **루프(지속) 사운드는 이미 Inspector에서 완전히 조절 가능** — 코드 수정 불필요. 각 컴포넌트가 자체
   `AudioSource`를 만들면서 `spatialBlend`/`rolloffMode`/`minDistance`/`maxDistance`를 전부 Inspector
   필드로 노출해둠(기본값만 1f/Logarithmic/1/500, 인스턴스별 override 가능):
   - `AdvancingWall.cs`(`moveSpatialBlend` 등), `AdvancingWallTelegraph.cs`(`warnSpatialBlend` 등)
   - `SpinRoller.cs`(`rollSpatialBlend` 등, `Boulder_Roll`)
   - `Stage5ChaserAI.cs`(`runSpatialBlend` 등, Chaser 달리기 루프)
   - `Stage5TargetRunner.cs`(`runSpatialBlend` 등, Runner 달리기 루프)
   - `WindTrap.cs`(`windSpatialBlend` 등, 바람 지속 루프)
   → **사용자가 요청한 "AdvancingWall/Boulder_Roll/Chaser/Runner는 3D 유지"가 정확히 이 그룹과 일치.**
   코드는 그대로 두고, 맵 스케일에 맞게 `minDistance`/`maxDistance`만 각 프리팹 Inspector에서 좁혀주면
   됨(예: min 1~2m, max 15~30m 등 — 사용자가 직접, MCP 쓰기 금지 원칙).

2. **단발(1회) 사운드 중 `SFXManager.Play(id, worldPosition)`로 재생하는 것들은 커스텀 불가능** —
   `AudioSource.PlayClipAtPoint`가 내부적으로 임시 `AudioSource`를 만들고 `spatialBlend=1`만 지정하지
   min/maxDistance는 그대로 Unity 기본값(1/500)이라 코드에서 못 바꿈. 대상:
   - `ArrowTrap.cs:282` — 화살 발사(`Trap_Arrow`)
   - `DropTrap.cs:300,346` — 발동(`Trap_Drop`) + 경고(`warnSfxId`)
   - `SpikeLaneField.cs:144` — 경고(`warnSfxId`)
   - `SpikeTrap.cs:132` — 상승(`raiseSfxId`)
   - `WindTrap.cs:345` — 바람 임펄스 순간음(`Wind_Push`/`Wind_Pull`, 위 지속 루프와는 별개 호출)
   - `Stage5ChaserAI.cs:240` — Chaser 공격(`Stage5_Chaser_Attack`)
   - `Stage5TargetRunner.cs:394` — Runner 포획(`Stage5_Runner_Captured`)

**사용자 결정(2026-08-29):** 작은 맵이니 위 2번 그룹을 전부 `Play(id)`(완전 2D, 거리 무관 항시 동일 볼륨)로
바꾸자는 방향 제시 — **단, `Stage5_Chaser_Attack`/`Stage5_Runner_Captured`는 3D 유지**(요청에 명시).

**✅ 완료 (2026-08-29) — 5곳 중 4곳 2D로 전환:**
- `DropTrap.cs:300` (`fireSfxId`) → `Play(fireSfxId)` **적용됨**
- `DropTrap.cs:346` (`warnSfxId`) → `Play(warnSfxId)` **적용됨**
- `SpikeLaneField.cs:144` (`warnSfxId`) → `Play(warnSfxId)` **적용됨**
- `SpikeTrap.cs:132` (`raiseSfxId`) → `Play(raiseSfxId)` **적용됨**
- `WindTrap.cs:345` (`windSfx`, `Wind_Push`/`Wind_Pull` 임펄스) → `Play(windSfx)` **적용됨** (지속 루프는
  그대로 3D, 이 임펄스 단발음만 2D — 사용자가 "우선 windtrap은 2d"로 확정)

**✅ 완료 (2026-08-29) — `ArrowTrap.cs` 화살 발사음(`Trap_Arrow`) 완전 제거.** 사용자와 논의: 이 맵에서
소리로 회피하는 함정은 `DropTrap`뿐이고, 화살은 시각(궤적)으로 피하는 쪽이라 발사음이 회피에 필요한
정보가 아님 + `ArrowTrap`이 스테이지에 여러 개 있어서 2D로 하면 겹쳐서 시끄럽고 3D로는 맵이 작아 잘 안
들리는 딜레마였음(제거/2D 작게/3D 볼륨업/3D 커스텀 감쇠 4가지 대안 제시 후 사용자가 **완전 제거** 선택).
`fireSfxId` 필드와 `OnTrapTrigger()`의 `SFXManager.Instance?.Play(fireSfxId, spawn.position)` 호출 삭제,
린트 통과 확인. 프리팹(`Trap_Arrow` 값이 들어있었을 `fireSfxId` serialized 데이터)은 필드 자체가 없어져
자동으로 무시됨 — 프리팹 직접 수정 불필요.

**변경 안 함(요청대로 3D 유지, 코드 수정 없음):**
- `AdvancingWall`/`SpinRoller`(Boulder)/`Stage5ChaserAI`(런/공격)/`Stage5TargetRunner`(런/포획) — 전부
  그대로, min/maxDistance만 사용자가 Inspector에서 맵 스케일에 맞게 나중에 좁히면 됨.

**NGO 영향 없음** — 이 변경은 각 클라이언트 로컬 SFX 재생 위치 파라미터만 빼는 것이라 authority/RPC 구조는
그대로(Chaser/Runner 쪽은 이미 `ClientRpc`로 각 클라이언트에서 재생 중, 그 구조 유지).

### 11.1 추가 조정 (2026-08-29, 같은 세션) — SpikeLaneField 제거 / SpikeTrap 3D / Telegraph·WindTrap 전부 2D

위 §11 1차 작업 이후 사용자가 실제로 들어보고 4곳을 추가로 조정 요청, 전부 반영·린트 통과:

- **`SpikeLaneField.cs` 경고음(`warnSfxId`) — 완전 제거.** §11 1차 때는 "3D→2D 전환"만 했었는데(제거가
  아니었음 — 착각하지 말 것, 이 문서에 그렇게 기록돼 있었음), 사용자가 재청취 후 완전 제거를 요청. 필드와
  `HandleWarnStart()`의 `Play(warnSfxId)` 호출 삭제(`ArrowTrap`과 동일 패턴).
- **`SpikeTrap.cs` 상승음(`raiseSfxId`) — 2D → 3D로 롤백.** `Play(raiseSfxId)` → `Play(raiseSfxId,
  transform.position)`.
- **`AdvancingWallTelegraph.cs` 경고 루프 — 3D(Inspector 조절형) → 2D 고정.** 원래 §11 1차 분류에서
  "이미 Inspector로 완전 조절 가능하니 코드 수정 불필요" 그룹(루프 사운드)에 속해 안 건드렸었는데, 사용자가
  이번에 명시적으로 2D 고정을 요청. `warnSpatialBlend`/`warnMinDistance`/`warnMaxDistance`/`warnRolloffMode`
  필드 전부 제거, `_warnLoopSource.spatialBlend = 0f` 하드코딩.
- **`WindTrap.cs` 바람 지속 루프 — 3D(Inspector 조절형) → 2D 고정, 임펄스와 통일.** 원래 임펄스(순간음,
  §11 1차에서 2D 전환 완료)와 지속 루프(그때는 "이미 문제 없음" 그룹이라 3D 유지)가 나뉘어 있었던 이유는
  "코드로 커스텀 안 되는 API(`PlayClipAtPoint`) 쓰는 것만 문제로 분류"했기 때문 — 취향(항상 2D)이 아니라
  기술적 결함 유무로 나눈 분류였음. 사용자가 "왜 나누냐, 다 2D로" 요청해서 통일. `windSpatialBlend`/
  `windMinDistance`/`windMaxDistance`/`windRolloffMode` 필드 전부 제거, `_windLoopSource.spatialBlend = 0f`
  하드코딩. 임펄스 쪽 코드는 원래부터 2D라 변경 없음.
- **`AdvancingWall.cs` 이동 루프 — 3D↔2D 왕복 후 최종 "패널티는 무음 + 3D 유지"로 확정 (2026-09-01).**
  1차: `M.Stage3` `Tooth`가 스케일 25·천장 ~32m·패널티 0.6초라 3D가 사실상 무음 → Telegraph/WindTrap과
  동일하게 2D 고정 시도. 2차: 2D로도 여전히 작게 들림 — 원인은 클립 자체가 아니라 (a) `SFXLibrary`의
  `VolumeOverride` 2배가 `AudioSource.volume`(0~1 클램프) 경로라 SFX 100에서도 못 먹고, (b) 패널티
  0.6초가 클립의 페이드인 구간과 겹침. **최종 결정: 패널티 이동엔 루프 사운드를 아예 안 씀** — 대신
  실패 시점에 `ColorTileChallenge.OnFail`(UnityEvent) → `SFXEventPlayer.Play()`(2D 단발, 이미 `M.Stage3`
  씬에 배치돼 있던 컴포넌트 재사용, 신규 코드 없음)로 연결. `AdvancingWall`의 이동 루프 자체는
  **3D로 원복**(`moveSpatialBlend`/`moveMinDistance`/`moveMaxDistance`/`moveRolloffMode` 필드 복원) —
  `T.Boss`가 스케줄 전진·후퇴(패널티 아님)에 이 루프를 그대로 쓰므로 2D로 두면 그쪽엔 과함. `PenaltyRoutine()`
  에서 `StartMoveLoop()`/`StopMoveLoop()` 호출 제거(패널티는 순간이동 취급, 사운드는 호출자 책임).

### 11.2 `Breakable_Destroy` 2D → 3D (2026-08-29, 같은 세션)

`Breakable.cs`에는 원래 사운드 재생 코드가 전혀 없었음 — `OnBreak`(UnityEvent)이 `M.Stage5.unity` 씬의
**공유 `SFXEventPlayer` GameObject 단 하나**(24개 벽 전부가 같은 오브젝트를 가리킴)의 `Play()`(2D)를 호출
하는 구조였음. 이 구조에서 단순히 리스너를 `Play3D()`로 바꾸면 24곳 벽이 전부 다른 위치에 있는데도 소리는
그 공유 오브젝트 하나의 고정 위치에서만 재생돼 완전히 틀린 결과가 나옴 — 그래서 프로젝트 전체에 `Play3D()`
사용처가 하나도 없었던 이유가 바로 이 구조적 한계였음.

**해결:** `Breakable.cs`의 `DoBreakVisuals()`(Host `ApplyFinalBreak()` / Client `ApplyBreakFromNetwork()`
양쪽에서 호출됨, 즉 각 머신 로컬에서 자기 위치 기준으로 재생)에 `SFXManager.Instance?.Play(SFXId
.Breakable_Destroy, transform.position)`을 직접 추가 — 각 벽이 자기 자신의 실제 위치에서 3D로 재생.
`SFXLibrary.asset`에 `Breakable_Destroy` 클립은 이미 배정돼 있었음(코드에서 호출하는 곳이 없었을 뿐).

**⚠️ 사용자 후속 작업 필요(씬 파일, MCP 쓰기 금지 원칙상 에이전트가 직접 못 고침) — `M.Stage5.unity`
한 곳만:** 이제 `Breakable_Destroy`가 코드에서 직접 재생되므로, 기존 `OnBreak → SFXEventPlayer.Play()`
리스너를 그대로 두면 2D+3D가 겹쳐 재생됨(중복). 씬에서 그 공유 `SFXEventPlayer` GameObject를 찾아
지우거나(24개 벽의 `OnBreak` 리스너가 전부 dangling 참조가 되어 조용히 무시됨), 각 벽의 `OnBreak`에서
리스너를 제거할 것. (다른 씬은 확인 결과 `OnBreak`가 비어있어 해당 없음.)

### 11.3 단발 3D SFX도 min/maxDistance Inspector 조절 가능하게 (2026-08-29, 같은 세션)

§11 조사 때 확인한 제약(`AudioSource.PlayClipAtPoint`는 min/maxDistance가 Unity 기본값 1m/500m로
고정, 코드로 커스텀 불가) 때문에 단발 3D SFX 4곳(`SpikeTrap` 상승음, `Stage5ChaserAI` 공격,
`Stage5TargetRunner` 포획, `Breakable_Destroy`)은 지금까지 맵 스케일에 안 맞는 기본 감쇠로 재생되고
있었음. 사용자가 이 4곳도 min/maxDistance를 조절해야 한다고 요청 — **`SFXManager`에 신규 메서드
`PlayAtPoint(id, worldPosition, minDistance, maxDistance, rolloffMode)` 추가**(`PlayClipAtPoint` 대신
임시 `AudioSource`를 직접 만들어 감쇠를 커스텀, 클립 길이만큼 지나면 자동 정리 — `PlayLoop()`와 같은
패턴, 1회성이라 자동 파괴만 다름).

4곳 전부 이 메서드로 전환 + 기존 루프 사운드들과 동일한 Inspector 필드 패턴(`Tooltip` + `float`
min/maxDistance + `AudioRolloffMode`) 추가:
- `SpikeTrap.cs`: `raiseMinDistance`/`raiseMaxDistance`/`raiseRolloffMode`
- `Stage5ChaserAI.cs`: `attackMinDistance`/`attackMaxDistance`/`attackRolloffMode`
- `Stage5TargetRunner.cs`: `capturedMinDistance`/`capturedMaxDistance`/`capturedRolloffMode`
- `Breakable.cs`: `destroyMinDistance`/`destroyMaxDistance`/`destroyRolloffMode`

전부 기본값 `min=1f`/`max=0f(→500 처리)`로, 기존 루프 사운드 필드들과 똑같이 동작 — **이제 3D인 7곳
전부(단발 4 + 루프 3) Inspector에서 min/maxDistance 조절 가능.** 실제 값 튜닝은 사용자가 각 컴포넌트를
씬/프리팹에서 선택해 Inspector에서 진행(맵 스케일에 맞게, 예: min 1~2m / max 10~20m 선에서 시작해서
실제 플레이하며 조정 권장 — 정확한 수치는 실측 필요).

**✅ 추가 (같은 세션) — `ArrowTrap` 화살 발사음 부활, 3D + 근접 감쇠로.** §11에서 `ArrowTrap`을 완전
제거하기로 했던 이유는 "여러 개 동시에 있어서 2D면 시끄럽고, (당시) 3D는 기본 감쇠라 잘 안 들림"이라는
딜레마였는데, 이번에 만든 `PlayAtPoint()`(근접 커스텀 감쇠)가 정확히 그 딜레마를 푸는 방식이라 사용자가
다시 추가 요청. `fireSfxId`/`fireMinDistance`/`fireMaxDistance`/`fireRolloffMode` 필드 재추가, `PlayAtPoint`
로 재생.

**⚠️ 발견(기존 구조, 이번에 새로 만든 문제 아님) — 이 발사음은 현재 Host에서만 재생됨.**
`ArrowTrap.OnTrapTrigger()`가 화살 스폰(B안: Host만 스폰) 때문에 `if (!nm.IsServer) return;`으로 일찍
리턴하는데, 발사음 재생 코드가 그 리턴 다음에 있어서 Client는 이 소리가 안 들림. 다른 단발 3D
트랩(`SpikeTrap` 등)은 `OnTrapTrigger`에 이런 얼리 리턴이 없어서 이 문제가 없음 — `ArrowTrap`만의
구조적 특성. 원래부터 이 위치였어서 그대로 유지, Client도 들리게 하려면 별도로 다뤄야 함(예: Host가
`ClientRpc`로 각 클라이언트에 재생 위치를 브로드캐스트하는 방식 — `WindTrap`의 Mouth 연출 동기화와
동일 패턴). 사용자 확인 후 진행할 것.

### 11.5 `Stage_TransitionEsophagus` 신규 추가 (2026-08-29, 같은 세션)

`Stage_TransitionMouth`(M.* 씬 진입 시 자동 재생)는 있는데 T.* 존(Esophagus)용 대응 SFX가
없다는 걸 사용자가 뒤늦게 발견 — git 히스토리 확인 결과 **원래부터 없었음**(삭제된 게 아니라
Mouth 쪽만 만들고 Esophagus는 안 만든 상태). 새로 추가:

- `SFXId.Stage_TransitionEsophagus = 31` (기존 마지막 번호 `Wind_Push = 30` 다음에 추가 — §0
  번호 고정 원칙대로 끝에 추가)
- `SFXLibrary.cs`에 `Stage_TransitionEsophagus` 필드 + `GetClip()` case 추가
- `SceneFlowManager.PlayStageTransitionSfx()`가 `"M."` 뿐 아니라 `"T."` 접두사도 처리하도록
  `else if` 추가 — `Stage_TransitionMouth`와 완전히 대칭 구조, 2D 재생.

**남은 작업(사용자):** `Assets/Audio/SFX/SFXLibrary.asset`(에셋 파일)에 실제 `AudioClip`을
`Stage_TransitionEsophagus` 필드에 연결해야 함 — 이건 에셋 Inspector 작업이라 에이전트가
대신 못 함.

### 11.4 min/maxDistance 기본 수치 확정 (2026-08-29, 같은 세션)

사용자가 각 3D 사운드를 개별 실측할 시간이 없어서, 튜닝 전 시작값을 지금 다 정함
(실측 후 필요하면 재조정). 원칙: **단발음(이미 일어난 이벤트 알림)은 좁게, 루프(접근
경고용)는 넓게** — 특히 `ArrowTrap`은 맵에 여러 개 동시 배치되므로 겹쳐서 시끄러워지지
않게 가장 좁게 잡음. `SpinRoller`는 기존 코드 툴팁에 있던 "위압감 있는 boulder는 min
15~25 권장" 가이드를 그대로 반영.

| 스크립트 | 필드 프리픽스 | 종류 | min | max |
|---|---|---|---|---|
| `ArrowTrap` | `fire` | 단발 | 1 | 8 |
| `SpikeTrap` | `raise` | 단발 | 1 | 10 |
| `Breakable` | `destroy` | 단발 | 1 | 10 |
| `Stage5ChaserAI` | `attack` | 단발 | 2 | 12 |
| `Stage5TargetRunner` | `captured` | 단발 | 2 | 12 |
| `AdvancingWall` | `move` | 루프 | 2 | 25 |
| `SpinRoller`(Boulder 등) | `roll` | 루프 | 18 | 40 |
| `Stage5ChaserAI`/`Stage5TargetRunner` | `run` | 루프 | 2 | 20 |

**✅ 재조정 (같은 세션, 사용자 지정) — 위 1차 제안값이 너무 좁다고 판단, 사용자가 직접
최종 수치를 지정.** 각 `.cs` 필드 기본값을 아래로 재갱신:

| 스크립트 | 필드 프리픽스 | 종류 | min | max |
|---|---|---|---|---|
| `ArrowTrap` | `fire` | 단발 | 25 | 50 |
| `SpikeTrap` | `raise` | 단발 | 5 | 25 |
| `Breakable` | `destroy` | 단발 | 5 | 50 |
| `Stage5ChaserAI` | `attack` | 단발 | 10 | 30 |
| `Stage5ChaserAI` | `run` | 루프 | 10 | 30 |
| `Stage5TargetRunner` | `captured` | 단발 | 15 | 30 |
| `Stage5TargetRunner` | `run` | 루프 | 10 | 30 |
| `AdvancingWall` | `move` | 루프 | 30 | 100 |
| `SpinRoller`(Boulder 등) | `roll` | 루프 | 40 | 200 |

**단, 8곳 전부 이미 씬/프리팹에
min=1/max=0(→500) 기본값이 명시적으로 저장돼 있는 상태**(`SpikeTrap.prefab`,
`Boulder.prefab`, `Chaser.prefab`, `Runner.prefab`, `M.Boss.unity`, `M.Stage5.unity`,
`M.Stage3.unity`, `T.Boss.unity` 등)라 **코드 기본값만 바꿔서는 기존 배치분에 반영 안 됨**
— MCP/에디터 쓰기는 에이전트 금지 원칙이라 사용자가 인스펙터에서 위 표 값을 직접
입력해야 함(§10 체크리스트에 추가).

**최종 상태 정리:**
- **2D:** `DropTrap`(발동/경고), `SpikeLaneField`(경고음 제거 — 무음), `WindTrap`(임펄스+지속 루프
  전부), `AdvancingWallTelegraph`(경고 루프), `ColorTileChallenge` 실패 사운드(`OnFail` →
  `SFXEventPlayer.Play()`, 2026-09-01 신설, §11 참고).
- **3D:** `ArrowTrap`(발사음), `SpikeTrap`(상승음), `Stage5ChaserAI`(공격), `Stage5TargetRunner`(포획),
  `Breakable_Destroy`(벽 파괴, §11.2) — 단발음 5곳, `PlayAtPoint()`로 min/maxDistance Inspector 조절
  가능. `SpinRoller`/Boulder_Roll(구르는 루프), `Stage5ChaserAI`/
  `Stage5TargetRunner`(달리기 루프), `AdvancingWall`(전진·후퇴 스케줄 루프, 패널티는 무음 — 2026-09-01
  최종) — 루프 3곳, Inspector `spatialBlend`/`minDistance`/`maxDistance` 조절 가능.

### 11.6 `PressurePad`/`ColorTile` 누름음(`Pad_Press`) 2D → 3D (2026-09-01, 별도 세션)

`PressurePad.PlayPressSfx()`/`ColorTile.PlayPressSfx()`는 §11 재분류 감사 때 대상에서 빠져 있던
두 곳 — 발판·타일 트리거는 각 머신이 CNT로 동기화된 위치를 로컬 물리로 감지해 트리거하므로,
원격 플레이어가 멀리 있는 발판을 밟아도 그 판정이 내 머신에서도 그대로 일어나 `Play(id)`(2D)가
거리 무관 풀볼륨으로 재생됐다 — 남이 밟았는데 내 귀 옆에서 나는 것처럼 들리는 문제
(`PlayerAudio` §9.1.3 포스트모템과 동일 계열 증상, 원인은 다름 — 여긴 로컬 트리거가 원격 대상에도
반응하는 문제, `PlayerAudio`는 오너 가드 누락이 원인).

**수정:** 둘 다 `Play(pressSfxId)` → `PlayAtPoint(pressSfxId, transform.position, pressMinDistance,
pressMaxDistance, pressRolloffMode)`로 전환, `SpikeTrap`과 동일한 Inspector 필드 패턴 추가
(`pressMinDistance`=5 / `pressMaxDistance`=20 / `pressRolloffMode`=Logarithmic 기본값). 네트워크
변경 없음 — 여전히 각 머신 로컬 재생, RPC 불필요(발판/타일은 고정 위치라 `transform.position` 그대로
사용 가능).

### 11.7 `SequenceRingMinigame` 정답/오답음이 Client에서 전혀 안 들림 (2026-09-01, 별도 세션)

`OnCorrectInput`/`OnWrongInput`(씬에서 `SFXEventPlayer.Play()` 2D 연결) 자체는 문제 없음 —
원인은 이 두 UnityEvent를 발동시키는 `AdvanceStep`/`ApplyWrongPenalty`가 `TrySubmit`/
`TrySubmitAnyKey`를 통해 **Host 판정 레인에서만** 호출된다는 것(§11B ④Judge, 네트워크
구조 문제라 여긴 2D/3D 선택과 무관 — SequenceRing은 화면 전체 UI성 미니게임이라 2D가 맞다는
판단은 유지). Client는 이 경로 자체를 안 타 정답/오답 사운드를 **아예** 못 들었다.

**수정:** 자세한 내용은 `NetworkDesign.md` §9.1 공유 포스트모템(`SequenceRingMinigame` 정답/오답
SFX, 2026-09-01) 참고 — `StageNetworkState.NotifyChallengeStepResult`(RPC 보장 전달)로 Host/Client 공통
채널을 새로 만들고, `SequenceRingMinigame.HandleChallengeStepResult`에서 `OnCorrectInput`/
`OnWrongInput`을 발동하도록 이동. SFX 자체는 2D 그대로 유지.

---

## 12. 마우스(카메라 회전) 감도 옵션 추가 (2026-09-06)

플레이테스트 피드백("마우스 감도를 만질 수 있는 UI가 있었으면 좋겠다") 반영. `ThirdPersonCamera`의
`sensitivityX`/`sensitivityY`(Inspector 고정값)에 배율(곱연산)을 곱하는 방식 — 기존 X/Y 비율은 그대로
유지되고 사용자는 "감도" 슬라이더 하나만 조절.

- **`GameSettingsManager.cs`**: `MouseSensitivity`(PlayerPrefs `Settings.MouseSensitivity`, 기본 1.0,
  범위 `MinMouseSensitivity~MaxMouseSensitivity` = 0.1~2) + `SetMouseSensitivity()`.
  `ResetToDefaults()`에도 포함.
- **`ThirdPersonCamera.cs`**: `LateUpdate`에서 게임플레이 중(`!_isInPreview`)일 때
  `GameSettingsManager.Instance.MouseSensitivity`를 **pull**해서
  `_activeSensX = sensitivityX * sensMul` / `_activeSensY = sensitivityY * sensMul`로 반영(§1 pull
  원칙과 동일 — push 이벤트 불필요). `Instance == null`이면 배율 1.0 폴백.
- **`OptionsMenuController.cs`**: `mouseSensitivitySlider` 필드, 일반 탭에 배치. 사용자 결정상 옆에
  숫자 라벨 없음(다른 슬라이더의 "70%"/정수 라벨과 다름).
- **`SetupSettingPanel.cs`**: 일반 탭 `Row_Language` 다음, `Row_ChatFontSize` **앞**에
  `Row_MouseSensitivity` 생성 코드 추가(사용자 요청 순서 — 채팅 글자 크기 자리를 밀어내고 배치).
  새 헬퍼 `ConvertSliderRowToPlainRange()` — `CreateSliderRow`가 기본으로 붙이는 0~1 전용
  `SliderValuePercentLabel`을 떼고, 값 텍스트를 비활성화한 채 임의 min/max 실수 슬라이더로 전환.
- **`SetupSettingUILocalization.cs`**: `Row_MouseSensitivity` → `Settings.MouseSensitivity` 키,
  12개 로케일 번역 추가.

**반영:** MCP로 `Setting_Panel.prefab`에 행 생성·슬라이더 연결·로컬라이즈까지 완료(2026-09-06).
`Tools > Setup Setting Panel (Title)` 재실행은 하지 말 것 — ContentRoot를 지워서 레이아웃이 날아감.
이후 패널을 처음부터 다시 만들 때만 에디터 툴을 쓰면 됨. `SetupSettingPanel`/`SetupSettingUILocalization`에는
`Row_DigitCheer` 생성·`Settings.DigitCheer` 키도 넣어 두었음.

**숫자키 응원 로컬라이즈:** `Row_DigitCheer` 라벨이 `Settings.MicMute`(마이크 음소거)에 잘못 묶여
이상한 글자가 나왔음. `Settings.DigitCheer` 키(12개 로케일)로 교체 완료.

**NGO 영향 없음** — Owner 로컬 카메라(`LocalPlayerCamera`/`ThirdPersonCamera`) 전용 설정, 네트워크
동기화 불필요.
