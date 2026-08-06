# StageDialogueLines — M/T 스테이지 한국어 대사 SSOT

> 이 문서는 M/T 스테이지 `PhaseDialogueGate` / `DialogueUI`에 들어갈 한국어 대사 최종본이다.
> 다음 단계: 이 표를 기준으로 `Assets/Localization/StringTables/Dialogue_*.asset` (en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl)에 번역해 넣는다.
> 대사 자체는 게임 씬(.unity) `DialogueUI`의 `dialogueLines` TMP 텍스트에 직접 들어가는 내용이라, 여기 문서를 고쳐도 씬에는 자동 반영되지 않는다 — 씬 작업은 사용자가 에디터에서 직접 입력.

## 진행 순서 (스토리 흐름)

입(M) → 식도(T) → 위(시즌2, 미제작). M.Boss 클리어 시 "삼켜진다" → T.Stage로 연결. T.Boss 클리어는 위(胃)를 확정하지 않고 클리프행어로 마무리(시즌2 여지 보존).

---

## M.Stage1

1. 우리, 먹혔어. 이제부터 살아나가는 것만 생각해.

*(연습 구역인 Stealth/색 패드/응원 설명은 `2.Tutorial` 씬 담당 — M.Stage1에서 재설명하지 않음. `CheerAndTutorialDesign.md` §9.3 참고.)*

## M.Stage2 (OXQuizManager)

> 미정 — 아직 번역 원문 없음. 필요 시 `OXQuizManager`/`OXQuizUI`(문제·타이머·정답 UI가 이미 룰을 설명하므로 대사는 선택 사항, 이전 대화 참고).

## M.Stage3 (ColorTileChallenge)

1. 이제부터 바닥에 네 색이랑 똑같은 타일이 무작위로 뜰 거야.
2. 제한 시간 안에 그 위로 올라서야 해.
3. 못 올라서면... 천장의 이빨이 떨어지기 시작할 거야.

## M.Stage4 (SequenceRingMinigame)

**Stage1** (최초 룰 설명)
1. 네 색이 바닥에 뜨면 스페이스를 눌러.
2. 흰색은 아무나 눌러도 되고, 검은색은 절대 누르면 안 돼!
3. 잘못 누르면 시간이 줄어드니까 조심해!

**Stage2** — 대사 삭제 (난이도만 상승, 대사 없이 진행)

## M.Stage5 (GridColorChallenge / GridBWTileChallenge)

1. 네 색 타일 위에 올라가서 버텨!

*(Color 모드 기준. BW 모드용 별도 문구는 아직 미정 — 필요 시 추가.)*

## M.Boss (BossFightObjective — 몬스터 없음, 스테이지 함정 자체가 보스)

**Intro**
1. 여기가 입에서 마지막이야.
2. 조금만 버티면 나갈 수 있을 것 같아...

**Bossdown**
1. 입 안이 조용해졌다... 다 멈춘 건가.
2. 이제 탈출할 수 있는 거지...?
3. ...
4. 이런, 삼켜진다...!!

---

## T.Stage1 (BoulderSpawnManager + ReachZoneObjective)

1. 식도로 넘어온 건가...
2. 뒤에서 뭔가 굴러오는 것 같은데...
3. 달려!!!

## T.Stage2 (MemoryPath / ColoredMemoryPath / PioneerPathManager)

**Stage1 — MemoryPath**
1. 잘못 밟으면 그대로 즉사야. 빛나는 칸만 외워둬.

**Stage2 — ColoredMemoryPath**
1. 색깔별로 보여줄 거야. 반드시 네 색에 맞춰!
2. 색이 맞아도 흑백이면 죽을 거야.

**Stage3 — PioneerPathManager**
1. 구역마다 담당 색이 있어. 담당이 먼저 지나가야 길이 안전해져.

## T.Stage5 (Stage5TargetRunner / Stage5ChaserAI)

**Stage1 — Runner 최초 등장** (Stage2도 재사용, 재설명 없음)
1. 도망치는 적혈구들을 잡아!

**Stage3 — Chaser 최초 등장** (Stage4도 재사용, 재설명 없음)
1. 항체로부터 도망쳐서 살아남아!

## T.Boss (BossFightObjective — 시간 구간 기반 연속 생존)

**Intro**
1. 식도 끝부분까지 왔어.
2. 마지막이라서 그런가... 압박감이 다르네.
3. 멈추지 말고 움직여!

**Bossdown**
1. 조임이 멈췄다. 후... 결국 살았네.
2. 근데 바닥이 이상해...
3. 무너지기 시작한다!

---

## 열려 있는 항목 (다음 작업)

- [ ] M.Stage2 (OX퀴즈) 대사 미정
- [ ] M.Stage5 GridBW 모드용 별도 대사 미정
- [ ] 위 전체를 `Dialogue_en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl` 로컬라이제이션 테이블에 번역해 반영
- [ ] 씬(.unity)의 `DialogueUI.dialogueLines` TMP 텍스트에 실제 한국어 원문 입력 (에디터 작업, 에이전트는 읽기만 — `unity-mcp-readonly.mdc`)
