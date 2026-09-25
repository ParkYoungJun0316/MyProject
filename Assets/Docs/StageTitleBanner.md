# StageTitleBanner — 씬 진입 제목 배너

> **상태(2026-09-24):** 규칙·제목·번역 확정. 코드 완료(컴파일 통과). UI.prefab 배치 완료. 테이블 생성·플레이 검증 남음.
> 출처: 플레이테스트 의견 — "장소가 바뀌면 이름이 뜨는 배너(예: 불타오르는 용암지대)가 있으면 좋겠다".

## 1. 규칙

| 항목 | 결정 |
|---|---|
| 내용 | **제목 한 줄만.** 부제·설명 없음 (정신없어서 폐기) |
| 대상 씬 | `M.Stage1`~`M.Stage5`, `M.Boss`, `T.Stage1`~`T.Stage5`, `T.Boss` (12개) |
| 제외 | `Tutorial`, `End.Demo` |
| 하위 스테이지 | M.Stage2(2.1/2.2)·M.Stage4(4.1~4.3)도 **씬 첫 진입 때 1번만**. 하위 스테이지 전환 시 표시 없음 |
| 타이밍 | 씬 준비 커튼(`SceneReadyAndColorMapping.md`)이 **걷힌 직후**. 스테이지 시작 대사보다 **먼저** |
| 재도전 | **안 띄움** — DialogueUI 대사와 같은 기준 |
| 위치 | 화면 **상단 1/3** |
| 표기 | 문장부호 없음 |

## 2. 로컬라이제이션

- 한국어 원문 + 영어 확정본을 먼저 정하고, 나머지 언어는 **영어를 기준으로** 번역한다.
- 언어: `en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl` (+ ko), `StageTipTranslations.md`와 동일.
- 용어는 `StageTipTranslations.md` 용어표를 따른다 (사탕 = candy / キャンディー / 糖果 / caramelo / bonbon / Bonbon / doce / конфета / cukierek).
- 키 (제안): `Title.<씬 이름>` — 예: `Title.M.Stage1`, `Title.T.Boss`.
- String Table / 씬은 에이전트가 쓰지 않는다. 사람이 Localization Tables에 넣는다.

> **2026-09-24 최종본:** ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl 제목을 최종 카피로 교체해 String Table `StageTitle_*`에 반영(`pt`는 `pt-BR`과 동일). ko·en은 그대로.

## 3. 제목 (ko / en)

| 키 | ko | en |
|---|---|---|
| `Title.M.Stage1` | 쉬지 않는 입 | The Restless Mouth |
| `Title.M.Stage2` | 넘쳐나는 침 | Overflowing Saliva |
| `Title.M.Stage3` | 비좁은 통로 | The Narrow Passage |
| `Title.M.Stage4` | 지하의 혀 | The Tongue Below |
| `Title.M.Stage5` | 끝없는 붕괴 | Endless Collapse |
| `Title.M.Boss` | 마지막 탈출 기회 | The Last Chance to Escape |
| `Title.T.Stage1` | 굴러오는 사탕 | The Rolling Candy |
| `Title.T.Stage2` | 보이지 않는 길 | The Unseen Path |
| `Title.T.Stage3` | 역류하는 위액 | Acid Reflux |
| `Title.T.Stage4` | 정원 초과 | Over Capacity |
| `Title.T.Stage5` | 문 열어 | Open the Door |
| `Title.T.Boss` | 사탕이 닿기 전에 | Before the Candy Lands |

작명 메모:
- `M.Boss` "마지막 탈출 기회"는 탈출 실패 → 삼켜져 T로 넘어가는 전개의 복선.
- `T.Stage3` "역류"는 위액 수면이 아래에서 차오르는 규칙(`TStage3SegmentDeadline.md`)과 맞춤.
- `T.Stage4` "무게 초과"는 전 타일 용량 1(`TStage4TrapRandomization.md` §1.7). 영어 `Overweight`는 사람 과체중으로 읽혀서 `Over Capacity`로 확정. 다른 언어도 엘리베이터 "정원/하중 초과" 뜻으로 번역.
- `T.Boss` "닿기 전에"는 사탕 시계 규칙("사탕이 땅에 닿기 전에 이 구간을 끝내세요")에서 옴.

## 4. 번역

### 아시아권

| 키 | ja | zh-Hans | zh-Hant |
|---| --- | --- | --- |
| `Title.M.Stage1` | 落ち着かない口 | 不安分的嘴巴 | 不安分的嘴巴 |
| `Title.M.Stage2` | あふれるよだれ | 满溢的口水 | 滿溢的口水 |
| `Title.M.Stage3` | 狭い通路 | 狭窄的通道 | 狹窄的通道 |
| `Title.M.Stage4` | 下から迫る舌 | 下方的舌头 | 下方的舌頭 |
| `Title.M.Stage5` | 終わりなき崩壊 | 无尽的崩塌 | 無盡的崩塌 |
| `Title.M.Boss` | 最後の脱出チャンス | 最后的逃生机会 | 最後的逃生機會 |
| `Title.T.Stage1` | 転がるキャンディ | 滚动的糖果 | 滾動的糖果 |
| `Title.T.Stage2` | 見えない道 | 看不见的道路 | 看不見的道路 |
| `Title.T.Stage3` | 胃液の逆流 | 胃酸反流 | 胃酸逆流 |
| `Title.T.Stage4` | 定員オーバー | 超出承载人数 | 超出承載人數 |
| `Title.T.Stage5` | ドアを開けて | 打开大门 | 打開大門 |
| `Title.T.Boss` | キャンディが落ちる前に | 糖果落地之前 | 糖果落地之前 |

### 유럽·중남미권

| 키 | es | es-419 | fr | de | pt-BR | ru | pl |
|---| --- | --- | --- | --- | --- | --- | --- |
| `Title.M.Stage1` | La Boca Inquieta | La Boca Inquieta | La Bouche Agitée | Der Unruhige Mund | A Boca Inquieta | Беспокойный рот | Niespokojne Usta |
| `Title.M.Stage2` | Saliva Desbordada | Baba Desbordada | Salive Débordante | Überlaufender Speichel | Saliva Transbordando | Слюна через край | Przelewająca się Ślina |
| `Title.M.Stage3` | El Pasaje Estrecho | El Pasaje Estrecho | Le Passage Étroit | Der Enge Durchgang | A Passagem Estreita | Узкий проход | Wąskie Przejście |
| `Title.M.Stage4` | La Lengua de Abajo | La Lengua de Abajo | La Langue d'en Bas | Die Zunge in der Tiefe | A Língua Lá Embaixo | Язык снизу | Język z Dołu |
| `Title.M.Stage5` | Derrumbe Sin Fin | Derrumbe Sin Fin | Effondrement Sans Fin | Endloser Einsturz | Desmoronamento Sem Fim | Бесконечный обвал | Niekończące się Zawalenie |
| `Title.M.Boss` | La Última Oportunidad de Escapar | La Última Oportunidad de Escapar | La Dernière Chance de S'échapper | Die Letzte Chance zur Flucht | A Última Chance de Escapar | Последний шанс на побег | Ostatnia Szansa na Ucieczkę |
| `Title.T.Stage1` | El Caramelo Rodante | El Dulce Rodante | Le Bonbon Roulant | Das Rollende Bonbon | O Doce Rolante | Катящаяся конфета | Toczący się Cukierek |
| `Title.T.Stage2` | El Camino Invisible | El Camino Invisible | Le Chemin Invisible | Der Unsichtbare Weg | O Caminho Invisível | Невидимый путь | Niewidzialna Droga |
| `Title.T.Stage3` | Reflujo Ácido | Reflujo Ácido | Reflux Acide | Saurer Reflux | Refluxo Ácido | Кислотный рефлюкс | Refluks Kwasu |
| `Title.T.Stage4` | Exceso de Capacidad | Exceso de Capacidad | Capacité Dépassée | Überlastung | Capacidade Esgotada | Превышение вместимости | Przekroczona Pojemność |
| `Title.T.Stage5` | Abre la Puerta | Abre la Puerta | Ouvrez la Porte | Öffnet die Tür | Abra a Porta | Откройте дверь | Otwórz Drzwi |
| `Title.T.Boss` | Antes de que Caiga el Caramelo | Antes de que Caiga el Dulce | Avant que le Bonbon ne Tombe | Bevor das Bonbon Landet | Antes que o Doce Caia | До падения конфеты | Zanim Cukierek Spadnie |

## 5. 구현

| 파일 | 역할 |
|---|---|
| `Scripts/UI/StageTitleBannerUI.cs` | 키 = `Title.` + 활성 씬 이름(씬별 설정 없음). 한국어 폴백 목록에 없는 씬은 무시. 로딩 커튼 `IsCovered`가 false가 되면 재생 — 이벤트가 아니라 상태를 봐서 구독 전 걷힘·타임아웃 포기도 똑같이 잡는다. 본 키 `StageTitle/<씬>`을 `GameSession` 인트로 본 목록에 **실제로 띄울 때** 기록 → 사망/준비 실패 리로드 때 안 뜸. CanvasGroup 알파: 대기 0.2 → 인 0.5 → 유지 2 → 아웃 0.7초(unscaled, 인스펙터 조정) |
| `Scripts/UI/DialogueUI.cs` | `StartSequence()`가 배너 진행 중이면 끝난 뒤 이 피어에서 연다(`StageTitleBannerUI.WhenClear`). 줄 넘김이 원래 로컬이라 네트워크 변경 없음 |
| `Editor/SetupStageTitleLocalization.cs` | `Tools / Setup StageTitle Localization` — 이 문서 §3·§4 표를 읽어 `StageTitle` String Table 채움 |

배너 진행 여부(`IsHolding`)는 Awake에서 세운다 — PhaseManager.Start → Phase 0 인트로 대화가 같은 Start 패스에서 열릴 수 있어서.

### 에디터 작업 (사용자)

1. ~~UI.prefab 배치~~ **완료(2026-09-24, MCP)** — `UI/StageTitle`(앵커 0.5,0.75 · 1400×120 · 검정 50% 띠 · CanvasGroup · 활성) + 자식 `Txt.StageTitle`(Fredoka-Bold 흰색 Bold, 자동 크기 36~64, 줄바꿈 없음). StageClear 바로 앞 형제. 폰트는 런타임에 로케일 폰트로 교체된다.
   - **2026-09-26 스타일 변경(MCP):** 앵커 y 0.7 · 1400×200. 배경 = `Figma/Ingame/StageTitleBand.png`(양 끝·위아래가 투명하게 사라지는 띠) 검정 60%. 글자 뒤에 `Txt.StageTitle.Shadow`(같은 TMP 복제, 파랑 `#2080C0` = Stage Clear·Team Cheer 그림자 색, (6,−6) 오프셋)를 깔고, 글자 자동 크기 최대 64→**72**(두 TMP 같이 — 72까지는 가장 긴 제목(es "La Última Oportunidad de Escapar" ≈1279px)도 안 줄어 전 제목이 같은 크기) `shadowText`에 연결 — 로케일 폰트 교체 때 머티리얼이 바뀌어 TMP Underlay를 못 쓰므로 TMP 두 장으로 만든다. 배경 폭은 고정 — 띠 양 끝이 사라져서 언어별 글자 길이 차이가 박스로 드러나지 않는다.
2. `Tools / Setup StageTitle Localization` 실행.
3. 플레이 검증: M.Stage1 진입 시 커튼 → 배너 → 대사 순서 / 사망 리로드 시 배너·대사 안 뜸 / 언어 변경 시 폰트.

## 6. 같이 고친 것 — 대사 본 키 M/T 충돌

`PhaseDialogueGate.showOnceKey`가 M과 T에서 같은 값(`Stage1`, `Stage2.1`, `Stage3.1`, `Stage4.1`, `Boss.Intro`, `Bossdown` …)이라,
한 판을 이어 하면 T 쪽 인트로/보스다운 대사가 "이미 봄"으로 스킵돼야 했다(코드 추론, 플레이 미확인).
2026-09-24부터 기록 키를 코드가 `<씬 이름>/<showOnceKey>`로 만든다 — 인스펙터 값은 씬 안에서만 고유하면 된다.
