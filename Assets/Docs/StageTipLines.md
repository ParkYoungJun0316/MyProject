# StageTipLines — Tip HUD 한국어 SSOT

> 인게임 `Tip_Panel`(`UI.prefab`, Chat 위 360×320, 본문 자동 크기 16~20 · 줄 간격 +8 · 제목 아래 한 줄 띄움)에 넣을 **규칙 안내** 원문.
> DialogueUI 대사(`StageDialogueLines.md` / `StageDialogueTranslations.md`)와 **별개**다 — 대사 줄 재사용이 아니라, 플레이 중 필요할 때 꺼내 볼 짧은 규칙만 둔다.
> 번역본: [`StageTipTranslations.md`](StageTipTranslations.md) (`en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl`).

## 노출 방식 (2026-09-21)

- 헤더 `[Tab 아이콘] Tip`은 보여줄 키가 있으면 **항상** 보인다 (아이콘: `GameInputControllerIconsFree/keyboard/keyboard-outlined/tab.png`, `TipUI.tabIcon`).
- 본문 + 배경은 **기본 숨김**, **Tab을 누르고 있는 동안만** 보인다 (토글 아님). 채팅·치어네임 입력·ESC 메뉴 중엔 Tab 무시.
- 옵션 `ESC → 설정 → 일반 → Tip 항상 표시`(`GameSettingsManager.TipAlwaysShow`, 기본 OFF)를 켜면 Tab과 무관하게 본문이 항상 보인다 (Tab 무반응).
- 페이즈 시작 시 자동으로 펼치는 카드/타이머는 **두지 않는다** — 대화창을 읽는 동안 시간이 지나가고, 보스 P2 이후엔 대화창이 없어 기준점이 없기 때문.

## 표시 규칙

- 한 페이즈 = Tip TMP **한 칸**. 아래 번호는 줄바꿈 단위다. `(빈 줄)`은 실제 빈 줄이다.
- 비어 있지 않은 줄 앞에는 `•` 불릿을 붙인다 (문장 수 = 점 수). 빈 줄에는 점을 넣지 않는다.
- 키보드 키는 영어 (`Ctrl`, `Space`). HUD 경고 표기는 `"TEAMCHEER"` 그대로.
- 스토리·꿀떡 대사는 넣지 않는다.
- **M.Boss / T.Boss:** 켬 (M.Boss는 2026-09-17부터). 둘 다 Bossdown은 끔 (`TipUI.hideOnAllPhasesComplete`).

## 키 (제안)

String Table은 나중에 별도 Collection (`StageTip` 가칭). 키는 페이즈당 하나:

| 키 | 구간 |
|---|---|
| `Tip.M.Stage1` | M.Stage1 |
| `Tip.M.Stage2.1` | M.Stage2 2.1 |
| `Tip.M.Stage2.2` | M.Stage2 2.2 |
| `Tip.M.Stage3` | M.Stage3 |
| `Tip.M.Stage4.1` | M.Stage4 4.1 |
| `Tip.M.Stage4.2` | M.Stage4 4.2 |
| `Tip.M.Stage4.3` | M.Stage4 4.3 |
| `Tip.M.Stage5` | M.Stage5 |
| `Tip.M.Boss.1` | M.Boss P1 |
| `Tip.M.Boss.2` | M.Boss P2 |
| `Tip.M.Boss.3` | M.Boss P3 |
| `Tip.M.Boss.4` | M.Boss P4 |
| `Tip.T.Stage1` | T.Stage1 |
| `Tip.T.Stage2.1` | T.Stage2 2.1 |
| `Tip.T.Stage2.2` | T.Stage2 2.2 |
| `Tip.T.Stage2.3` | T.Stage2 2.3 |
| `Tip.T.Stage3` | T.Stage3 |
| `Tip.T.Stage4` | T.Stage4 |
| `Tip.T.Stage5.1` | T.Stage5 (미로) |
| `Tip.T.Boss.1` | T.Boss P1 |
| `Tip.T.Boss.2` | T.Boss P2 |
| `Tip.T.Boss.3` | T.Boss P3 |
| `Tip.T.Boss.4` | T.Boss P4 |


---

## M.Stage1

1. 한 번에 한 색의 입만 올라옵니다.
2. 흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.
3. 상단에 "TEAMCHEER" 경고가 뜨면 팀 키워드를 외치세요.

## M.Stage2 — 2.1

1. 간판 아래 꿀떡 수만큼 그 구역에 들어가세요.
2. 비대칭 정보를 각자 가지고 있습니다. 서로 공유하세요.
3. 상단에 "TEAMCHEER" 경고가 뜨면 팀 키워드를 외치세요.
4. 방어 버프는 라운드 실패 데미지도 막아 줍니다.

## M.Stage2 — 2.2

1. Ctrl로 흑/백 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.

## M.Stage3

1. 타일을 2초 동안 밟아야 점수가 올라갑니다.
2. 고유색 타일은 해당 색만, 흑백 타일은 누구나 점수를 올릴 수 있습니다.

## M.Stage4 — 4.1

1. 자기 색이 뜨면 Space를 누르세요.
2. 흰색은 아무나 눌러도 되고, 검은색은 1초 뒤 자동으로 넘어갑니다.
3. 미니게임 중에는 Space 버프를 쓸 수 없습니다.

## M.Stage4 — 4.2

1. 한 칸 앞의 바닥만 보여 줍니다.
2. 누를 칸을 미리 외워 두세요.

## M.Stage4 — 4.3

1. Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
2. "TEAMCHEER" 경고가 뜰 때 팀 키워드를 외치면 바닥이 복구됩니다.
3. 이미 부서진 뒤에는 다음 경고까지 버티세요.

## M.Stage5

1. 고유색 칸이 나오면 그 칸 위에 서야 합니다.
2. 고유색이 없으면 흑백 칸 위에서 버티세요.
3. 바닥 색에 맞춰 캐릭터 색도 바꾸세요.
4. 방어 버프는 라운드 실패 데미지도 막아 줍니다.

## M.Boss

> **[2026-09-17]** Tip 켬. 방어 버프 줄은 Tutorial `Board_Controls`의 구 `Row_BuffNote`에서 옮겨 옴.

### P1 (Barrier + 화살 + 침)

1. 한 번에 한 색의 입만 올라옵니다.
2. 흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.

### P2 (SideSplit 판정 + 입 닫힘)

1. 비대칭 정보를 각자 가지고 있습니다. 서로 공유하세요.
2. 방어 버프는 라운드 실패 데미지도 막아 줍니다.

### P3 (Drop + 화살 + 혀)

1. Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.

### P4 (입 닫힘 → 바닥 파괴)

1. 입이 열리면 팀 키워드를 외쳐 부서진 바닥을 복구하세요.

## T.Stage1

1. 벽 색에 맞춰 부딪히세요.

## T.Stage2 — 2.1

1. 길을 외워 두세요.

## T.Stage2 — 2.2

1. 자기 색 칸만 밟으세요.
2. 칸과 캐릭터 색을 맞추세요.

## T.Stage2 — 2.3

1. 담당 색이 먼저 지나가야 다른 팀원도 그 바닥을 밟을 수 있습니다.

## T.Stage3

1. 간판 시간 안에 구간을 통과하세요. 늦으면 위액이 차오릅니다.
2. 양옆 벽에 색을 맞춰 부딪히면 벽이 뒤로 물러납니다.

## T.Stage4

1. 한 칸에 한 명만 서세요. 둘 이상 서면 칸이 가라앉습니다.
2. 깨지는 칸이 섞여 있습니다.
3. 앞뒤 벽과 부종에 닿으면 튕겨 나갑니다.

## T.Stage5

> 2026-09-18 러너 재설계로 문구 전면 교체 (`TStage5RunnerRedesign.md`). 옛 흑백 토글 문구는 폐기.

1. 러너 한 명이 미로를 달리고, 나머지는 2층에서 길을 안내합니다.
2. 패드를 밟으면 그 색 문만 열리고 나머지는 전부 닫힙니다.

## T.Boss

사탕 시계 문장(`사탕이 땅에 닿기 전에 이 구간을 끝내세요.`)은 **마지막 줄**. 그 앞에 빈 줄을 둔다. P4는 SurviveTime이라 이 문장을 넣지 않는다.

### P1

1. 목표까지 도달하세요.
2. (빈 줄)
3. 사탕이 땅에 닿기 전에 이 구간을 끝내세요.

### P2

1. 발판을 눌러 길을 만들고 목표까지 도달하세요.
2. (빈 줄)
3. 사탕이 땅에 닿기 전에 이 구간을 끝내세요.

### P3

1. 안전 칸에는 한 명만 서세요. 겹치면 데미지를 입습니다.
2. (빈 줄)
3. 사탕이 땅에 닿기 전에 이 구간을 끝내세요.

### P4

1. 색을 맞춰 벽에 부딪히세요.
