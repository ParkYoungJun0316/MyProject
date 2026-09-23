# StageTitleBanner — 씬 진입 제목 배너

> **상태(2026-09-23):** 규칙·제목·번역 확정. 코드·UI 미착수.
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
| `Title.T.Stage4` | 무게 초과 | Over Capacity |
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
|---|---|---|---|
| `Title.M.Stage1` | 休まない口 | 永不停歇的嘴 | 永不停歇的嘴 |
| `Title.M.Stage2` | あふれるよだれ | 满溢的口水 | 滿溢的口水 |
| `Title.M.Stage3` | 狭い通路 | 狭窄的通道 | 狹窄的通道 |
| `Title.M.Stage4` | 地下の舌 | 地下之舌 | 地下之舌 |
| `Title.M.Stage5` | 終わりなき崩壊 | 无尽的崩塌 | 無盡的崩塌 |
| `Title.M.Boss` | 最後の脱出チャンス | 最后的逃脱机会 | 最後的逃脫機會 |
| `Title.T.Stage1` | 転がるキャンディー | 滚来的糖果 | 滾來的糖果 |
| `Title.T.Stage2` | 見えない道 | 看不见的路 | 看不見的路 |
| `Title.T.Stage3` | 胃液の逆流 | 胃酸倒流 | 胃酸倒流 |
| `Title.T.Stage4` | 重量オーバー | 超重 | 超重 |
| `Title.T.Stage5` | ドアを開けて | 快开门 | 快開門 |
| `Title.T.Boss` | キャンディーが落ちる前に | 在糖果落地之前 | 在糖果落地之前 |

### 유럽·중남미권

| 키 | es | es-419 | fr | de | pt-BR | ru | pl |
|---|---|---|---|---|---|---|---|
| `Title.M.Stage1` | La boca incansable | La boca incansable | La bouche infatigable | Der unermüdliche Mund | A boca incansável | Неутомимый рот | Niestrudzone usta |
| `Title.M.Stage2` | Saliva desbordante | Saliva desbordante | Salive débordante | Überquellender Speichel | Saliva transbordante | Слюна через край | Wzbierająca ślina |
| `Title.M.Stage3` | El pasaje estrecho | El pasaje angosto | Le passage étroit | Der enge Durchgang | A passagem estreita | Узкий проход | Wąskie przejście |
| `Title.M.Stage4` | La lengua subterránea | La lengua subterránea | La langue souterraine | Die Zunge in der Tiefe | A língua subterrânea | Подземный язык | Podziemny język |
| `Title.M.Stage5` | Derrumbe sin fin | Derrumbe sin fin | Effondrement sans fin | Endloser Einsturz | Desmoronamento sem fim | Бесконечное обрушение | Niekończący się rozpad |
| `Title.M.Boss` | La última oportunidad de escapar | La última oportunidad de escapar | La dernière chance de s'échapper | Die letzte Chance zur Flucht | A última chance de escapar | Последний шанс на побег | Ostatnia szansa na ucieczkę |
| `Title.T.Stage1` | El caramelo rodante | El caramelo rodante | Le bonbon qui roule | Das rollende Bonbon | O doce rolante | Катящаяся конфета | Toczący się cukierek |
| `Title.T.Stage2` | El camino invisible | El camino invisible | Le chemin invisible | Der unsichtbare Weg | O caminho invisível | Невидимый путь | Niewidzialna ścieżka |
| `Title.T.Stage3` | Reflujo ácido | Reflujo ácido | Reflux acide | Säure-Reflux | Refluxo ácido | Кислотный рефлюкс | Kwaśny refluks |
| `Title.T.Stage4` | Exceso de peso | Exceso de peso | Surcharge | Überlast | Excesso de peso | Перегруз | Przeciążenie |
| `Title.T.Stage5` | Abre la puerta | Abre la puerta | Ouvre la porte | Mach die Tür auf | Abre a porta | Открой дверь | Otwórz drzwi |
| `Title.T.Boss` | Antes de que caiga el caramelo | Antes de que caiga el caramelo | Avant que le bonbon tombe | Bevor das Bonbon landet | Antes que o doce caia | Пока конфета не упала | Zanim cukierek spadnie |

## 5. 열려 있는 항목

- 구현: 트리거(커튼 걷힘 신호), 재도전 판정(DialogueUI와 같은 기준 재사용), 배너 UI 연출(페이드 시간·폰트·배경) — 미착수.
- 씬별 제목 데이터를 어디에 둘지(씬 컴포넌트 필드 vs 씬 이름 → 키 매핑) — 미정.
