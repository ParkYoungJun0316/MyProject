# StageTipLines — Tip HUD 한국어 SSOT

> 인게임 `Tip_Panel`(`UI.prefab`, Chat 아래 300×300)에 넣을 **규칙 안내** 원문.
> DialogueUI 대사(`StageDialogueLines.md` / `StageDialogueTranslations.md`)와 **별개**다 — 대사 줄 재사용이 아니라, 플레이 중 상시 볼 짧은 규칙만 둔다.
> 번역본: [`StageTipTranslations.md`](StageTipTranslations.md) (`en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl`).

## 표시 규칙

- 한 페이즈 = Tip TMP **한 칸**. 아래 번호는 줄바꿈 단위다. `(빈 줄)`은 실제 빈 줄이다.
- 비어 있지 않은 줄 앞에는 `•` 불릿을 붙인다 (문장 수 = 점 수). 빈 줄에는 점을 넣지 않는다.
- 키보드 키는 영어 (`Ctrl`, `Space`). HUD 경고 표기는 `"TEAMCHEER"` 그대로.
- 스토리·꿀떡 대사는 넣지 않는다.
- **M.Boss:** Tip 없음 (끔). **T.Boss:** 켬. 둘 다 Bossdown은 끔.

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

M.Boss는 키 없음.

---

## M.Stage1

1. 한 번에 한 색의 입만 올라옵니다.
2. 흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.
3. 상단에 "TEAMCHEER" 경고가 뜨면 팀 응원 이름을 외치세요.

## M.Stage2 — 2.1

1. 지정된 색은 그 구역에 반드시 들어가야 합니다.
2. 상단에 "TEAMCHEER" 경고가 뜨면 팀 응원 이름을 외치세요.

## M.Stage2 — 2.2

1. Ctrl로 흑/백 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.

## M.Stage3

1. 타일을 2초 동안 밟아야 점수가 올라갑니다.
2. 고유색 타일은 그 색만, 흑백 타일은 누구든 밟을 수 있습니다.

## M.Stage4 — 4.1

1. 자기 색이 뜨면 Space를 누르세요.
2. 흰색은 아무나 눌러도 되고, 검은색은 1초 뒤 자동으로 넘어갑니다.
3. 미니게임 중에는 Space 버프를 쓸 수 없습니다.

## M.Stage4 — 4.2

1. 한 칸 앞의 바닥만 보여 줍니다.
2. 누를 칸을 미리 외워 두세요.

## M.Stage4 — 4.3

1. Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
2. "TEAMCHEER" 경고가 뜰 때 팀 응원 이름을 외치면 바닥이 복구됩니다.
3. 이미 부서진 뒤에는 다음 경고까지 버티세요.

## M.Stage5

1. 고유색 칸이 나오면 그 칸 위에 서야 합니다.
2. 고유색이 없으면 흑백 칸 위에서 버티세요.
3. 바닥 색에 맞춰 캐릭터 색도 바꾸세요.

## M.Boss

Tip 없음.

## T.Stage1

1. 내 색이 뜬 양옆 벽에 부딪히면 벽이 뒤로 물러납니다.

## T.Stage2 — 2.1

1. 길을 외워 두세요.

## T.Stage2 — 2.2

1. 자기 색 칸만 밟으세요.
2. 칸 색이 맞아도 캐릭터가 흑백이면 안 됩니다.

## T.Stage2 — 2.3

1. 담당 색이 먼저 지나가야 다른 팀원도 그 바닥을 밟을 수 있습니다.

## T.Stage3

1. 양옆 벽에 색을 맞춰 부딪히면 벽이 뒤로 물러납니다.

## T.Stage4

1. 앞뒤 벽과 부종에 닿으면 튕겨 나갑니다.

## T.Stage5

1. 어디서 패드를 밟든, 문을 열고 닫을 수 있습니다.
2. 흑색 문이 열리면 백색 문이 닫히고, 백색 문이 열리면 흑색 문이 닫힙니다.
3. 2층으로 가는 길을 먼저 찾으세요.

## T.Boss

사탕 시계 문장(`사탕이 땅에 닿기 전에 이 구간을 끝내세요.`)은 **마지막 줄**. 그 앞에 빈 줄을 둔다. P4는 SurviveTime이라 이 문장을 넣지 않는다.

### P1

1. 지정된 색이 길을 연 뒤 목표까지 도달하세요.
2. (빈 줄)
3. 사탕이 땅에 닿기 전에 이 구간을 끝내세요.

### P2

1. 발판을 눌러 길을 만들고 목표까지 도달하세요.
2. (빈 줄)
3. 사탕이 땅에 닿기 전에 이 구간을 끝내세요.

### P3

1. 가운데 발판을 밟아 튕겨 올라가 사탕과 색을 맞춰 부딪히세요.
2. (빈 줄)
3. 사탕이 땅에 닿기 전에 이 구간을 끝내세요.

### P4

1. 색을 맞춰 벽에 부딪혀 밀어내세요.
