# StageTipTranslations — Tip HUD 11개 언어

> SSOT 원문: [`StageTipLines.md`](StageTipLines.md) (한국어 확정본).
> `en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl` (`SteamworksIntegrationDesign.md` 트랙4 §10, ko 제외).
>
> String Table / 씬은 에이전트가 쓰지 않는다. 사람이 Localization Tables에 넣는다.
> 한 키 = Tip TMP 한 칸. `\n` = 줄바꿈, `\n\n` = T.Boss 사탕 줄 앞 빈 줄.
> 표시 시 비어 있지 않은 줄 앞에 `•` 불릿. 빈 줄에는 점 없음. `Tools/Setup StageTip Localization`이 테이블에 같이 넣는다.

## 번역 규칙

1. 그 언어 문법에 맞출 것.
2. 한국어를 직역하지 말 것. 그 언어권 HUD·코옵에서 실제로 쓰는 말로.
3. 소리 내어 읽히게. Tip은 합니다체 원문 → 각 언어의 **짧은 지시문**(en 명령형, ja です/ます, de/fr/ru/pl 비격식 2인칭, es 스페인/중남미 어휘 분리, pt-BR).

**용어 [2026-09-17 통일]:** 경고 때 외치는 단어 = Tutorial 설정 UI와 같은 말 — ko `팀 키워드`, en `team word`, ja `チームの合言葉`, zh `团队关键词`/`團隊關鍵詞`, es `palabra de equipo`, fr `mot d'équipe`, de `Team-Wort`, pt-BR `palavra da equipe`, ru `командное слово`, pl `hasło drużyny`. (예전 `팀 응원 이름` / `team cheer` / `Teamruf` / `cri d'équipe` 등은 폐기. 스토리 대사의 `팀 구호`는 캐릭터 말투라 유지.)

**고정:** `Ctrl` / `Space` / `"TEAMCHEER"` 는 모든 언어에서 그대로. 키 강조 태그는 아직 안 씀.

**용어 (Dialogue / Tutorial과 맞춤):**

| ko | en | 비고 |
|---|---|---|
| 팀 키워드 | team word / チームの合言葉 / 团队关键词 / palabra de equipo / mot d'équipe / Team-Wort / palavra da equipe / командное слово / hasło drużyny | HUD 표기는 `"TEAMCHEER"`. Tutorial 설정 UI와 같은 말 (2026-09-17, 구 `팀 응원 이름`) |
| 고유색 | unique color / 固有色 / 专属色 | |
| 흑백 | black and white | |
| 조준 | lock-on | |
| 버프 | buff | 게이머 차용어 유지 (zh만 增益) |
| 부종 | swellings | 알레르기 붓기. 의학 용어 남발 금지 |
| 적혈구 | red blood cells | 세계관 단어 유지 |
| 사탕 | candy / キャンディー / 糖果 / caramelo / bonbon / Bonbon / doce / конфета / cukierek | |

---

## Tip.M.Stage1

- ko: 한 번에 한 색의 입만 올라옵니다.\n흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.\n상단에 "TEAMCHEER" 경고가 뜨면 팀 키워드를 외치세요.
- en: Only one color of mouth comes up at a time.\nAnyone can match their color and step on black-and-white pads.\nWhen "TEAMCHEER" pops up at the top, shout your team word.
- ja: 一度に上がる口は一色だけです。\n白黒の足場は、色を合わせて誰でも踏めます。\n上に「TEAMCHEER」が出たら、チームの合言葉を叫んでください。
- zh-Hans: 一次只会升起一种颜色的嘴。\n黑白踏板谁都可以对好颜色再踩。\n顶部出现 “TEAMCHEER” 时，喊出团队关键词。
- zh-Hant: 一次只會升起一種顏色的嘴。\n黑白踏板誰都可以對好顏色再踩。\n頂部出現「TEAMCHEER」時，喊出團隊關鍵詞。
- es: Solo sube una boca de un color cada vez.\nCualquiera puede igualar su color y pisar las plataformas en blanco y negro.\nCuando salga "TEAMCHEER" arriba, gritad la palabra de equipo.
- es-419: Solo sube una boca de un color a la vez.\nCualquiera puede igualar su color y pisar las plataformas en blanco y negro.\nCuando aparezca "TEAMCHEER" arriba, griten la palabra de equipo.
- fr: Une seule couleur de bouche se lève à la fois.\nTout le monde peut matcher sa couleur et marcher sur les dalles noir et blanc.\nQuand "TEAMCHEER" s'affiche en haut, crie le mot d'équipe.
- de: Es kommt immer nur ein Mund in einer Farbe hoch.\nSchwarz-weiße Trittflächen kann jeder betreten, wenn er die Farbe anpasst.\nWenn oben "TEAMCHEER" kommt, ruf das Team-Wort.
- pt-BR: Só sobe uma boca de cada cor por vez.\nQualquer um pode acertar a cor e pisar nas plataformas preto e branco.\nQuando o "TEAMCHEER" aparecer em cima, grite a palavra da equipe.
- ru: За раз поднимается рот только одного цвета.\nНа чёрно-белые платформы может встать любой, подстроив цвет.\nКогда сверху появится «TEAMCHEER», выкрикни командное слово.
- pl: Za razem unosi się usta tylko jednego koloru.\nNa czarno-białe platformy może wejść każdy, byle dopasował kolor.\nGdy u góry pojawi się „TEAMCHEER”, wykrzycz hasło drużyny.

## Tip.M.Stage2.1

- ko: 지정된 색은 그 구역에 반드시 들어가야 합니다.\n상단에 "TEAMCHEER" 경고가 뜨면 팀 키워드를 외치세요.\n방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: The assigned color has to be in that zone.\nWhen "TEAMCHEER" pops up at the top, shout your team word.\nThe Defense buff also blocks round-fail damage.
- ja: 指定された色は、そのエリアに必ず入ってください。\n上に「TEAMCHEER」が出たら、チームの合言葉を叫んでください。\n防御バフはラウンド失敗のダメージも防いでくれます。
- zh-Hans: 指定颜色必须进入那个区域。\n顶部出现 “TEAMCHEER” 时，喊出团队关键词。\n防御增益也能挡住回合失败的伤害。
- zh-Hant: 指定顏色必須進入那個區域。\n頂部出現「TEAMCHEER」時，喊出團隊關鍵詞。\n防禦增益也能擋下回合失敗的傷害。
- es: El color asignado tiene que estar en esa zona.\nCuando salga "TEAMCHEER" arriba, gritad la palabra de equipo.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- es-419: El color asignado tiene que estar en esa zona.\nCuando aparezca "TEAMCHEER" arriba, griten la palabra de equipo.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- fr: La couleur désignée doit forcément être dans cette zone.\nQuand "TEAMCHEER" s'affiche en haut, crie le mot d'équipe.\nLe bonus de défense bloque aussi les dégâts d'un round raté.
- de: Die vorgesehene Farbe muss in diese Zone.\nWenn oben "TEAMCHEER" kommt, ruf das Team-Wort.\nDer Verteidigungs-Buff blockt auch Schaden bei verlorener Runde.
- pt-BR: A cor designada tem que estar naquela zona.\nQuando o "TEAMCHEER" aparecer em cima, grite a palavra da equipe.\nO buff de defesa também bloqueia o dano de rodada perdida.
- ru: Назначенный цвет обязан быть в этой зоне.\nКогда сверху появится «TEAMCHEER», выкрикни командное слово.\nБафф защиты также блокирует урон за проваленный раунд.
- pl: Wyznaczony kolor musi być w tej strefie.\nGdy u góry pojawi się „TEAMCHEER”, wykrzycz hasło drużyny.\nWzmocnienie obrony blokuje też obrażenia za przegraną rundę.

## Tip.M.Stage2.2

- ko: Ctrl로 흑/백 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
- en: Press Ctrl to match your character to the black or white floor and break the lock-on.
- ja: Ctrlで床の白黒にキャラの色を合わせて、ロックオンを外してください。
- zh-Hans: 按 Ctrl，把角色颜色对成地板的黑或白，甩掉锁定。
- zh-Hant: 按 Ctrl，把角色顏色對成地板的黑或白，甩掉鎖定。
- es: Pulsa Ctrl para igualar el color de tu personaje al suelo blanco o negro y quitar el marcaje.
- es-419: Presiona Ctrl para igualar el color de tu personaje al piso blanco o negro y quitar el lock-on.
- fr: Appuie sur Ctrl pour caler la couleur de ton perso sur le sol noir ou blanc et casser le lock.
- de: Drück Ctrl, pass die Farbe deines Charakters an den schwarz-weißen Boden an und löse das Lock-on.
- pt-BR: Aperte Ctrl para igualar a cor do personagem ao chão preto ou branco e sair da mira.
- ru: Нажми Ctrl, подгони цвет персонажа под чёрный или белый пол — и сбрось захват.
- pl: Wciśnij Ctrl, dopasuj kolor postaci do czarno-białej podłogi i zrzuć namierzenie.

## Tip.M.Stage3

- ko: 타일을 2초 동안 밟아야 점수가 올라갑니다.\n고유색 타일은 그 색만, 흑백 타일은 누구든 밟을 수 있습니다.
- en: Stand on a tile for 2 seconds to score.\nUnique-color tiles only count for that color. Anyone can step on black or white.
- ja: タイルの上に2秒立つとスコアが入ります。\n固有色のタイルはその色だけ、白黒は誰でも踏めます。
- zh-Hans: 在格子上站满 2 秒才加分。\n专属色格子只有该颜色能踩，黑白谁都可以踩。
- zh-Hant: 在格子上站滿 2 秒才加分。\n專屬色格子只有該顏色能踩，黑白誰都可以踩。
- es: Mantén una baldosa 2 segundos para puntuar.\nLas de color único solo cuentan para ese color. El blanco y el negro las puede pisar cualquiera.
- es-419: Quédate 2 segundos en una loseta para puntuar.\nLas de color único solo cuentan para ese color. El blanco y el negro las puede pisar cualquiera.
- fr: Reste 2 secondes sur une tuile pour marquer.\nLes tuiles de couleur unique ne comptent que pour cette couleur. Noir et blanc, tout le monde peut marcher dessus.
- de: Zwei Sekunden auf einer Kachel stehen, dann gibt's Punkte.\nEinzigfarbige Kacheln zählen nur für diese Farbe. Schwarz und Weiß kann jeder betreten.
- pt-BR: Fique 2 segundos no bloco para pontuar.\nBloco de cor única só vale pra essa cor. Preto e branco qualquer um pode pisar.
- ru: Стой на плитке 2 секунды — тогда идут очки.\nУникальные плитки считает только этот цвет. Чёрные и белые может наступать кто угодно.
- pl: Stań na kafelku 2 sekundy, żeby dostać punkty.\nKafelki unikalnego koloru liczą tylko dla tego koloru. Czarne i białe może stąpać każdy.

## Tip.M.Stage4.1

- ko: 자기 색이 뜨면 Space를 누르세요.\n흰색은 아무나 눌러도 되고, 검은색은 1초 뒤 자동으로 넘어갑니다.\n미니게임 중에는 Space 버프를 쓸 수 없습니다.
- en: Press Space when your color lights up.\nAnyone can hit white. Black skips to the next turn after 1 second.\nYou can't use your Space buff during this minigame.
- ja: 自分の色が点灯したらSpaceを押してください。\n白は誰でも押せます。黒は1秒後に自動で次に進みます。\nミニゲーム中はSpaceのバフは使えません。
- zh-Hans: 亮起自己的颜色时按 Space。\n白色谁都可以按。黑色 1 秒后会自动进入下一轮。\n小游戏进行中不能用 Space 增益。
- zh-Hant: 亮起自己的顏色時按 Space。\n白色誰都可以按。黑色 1 秒後會自動進入下一輪。\n小遊戲進行中不能用 Space 增益。
- es: Pulsa Space cuando salga tu color.\nEl blanco lo puede pulsar cualquiera. El negro pasa al siguiente turno al cabo de 1 segundo.\nDurante el minijuego no puedes usar el buff de Space.
- es-419: Presiona Space cuando salga tu color.\nEl blanco lo puede presionar cualquiera. El negro pasa al siguiente turno después de 1 segundo.\nDurante el minijuego no puedes usar el buff de Space.
- fr: Appuie sur Space quand ta couleur s'allume.\nLe blanc, n'importe qui peut appuyer. Le noir passe au tour suivant au bout d'1 seconde.\nPendant le mini-jeu, tu ne peux pas utiliser le buff Space.
- de: Drück Space, wenn deine Farbe aufleuchtet.\nWeiß darf jeder drücken. Schwarz geht nach 1 Sekunde automatisch weiter.\nWährend des Minispiels geht der Space-Buff nicht.
- pt-BR: Aperte Space quando a sua cor acender.\nBranco qualquer um pode apertar. Preto passa pro próximo turno depois de 1 segundo.\nDurante o minigame você não pode usar o buff do Space.
- ru: Нажми Space, когда загорится твой цвет.\nБелую может нажать кто угодно. Чёрная через 1 секунду сама перейдёт дальше.\nВо время мини-игры бафф на Space не работает.
- pl: Wciśnij Space, gdy zapali się twój kolor.\nBiały może nacisnąć każdy. Czarny po 1 sekundzie sam przechodzi dalej.\nW minigrze nie użyjesz buffa na Space.

## Tip.M.Stage4.2

- ko: 한 칸 앞의 바닥만 보여 줍니다.\n누를 칸을 미리 외워 두세요.
- en: You only see one tile ahead.\nMemorize which ones to press.
- ja: 1マス先の床しか見えません。\n押すマスを覚えておいてください。
- zh-Hans: 只能看到前面一格。\n要按的格子请先记住。
- zh-Hant: 只能看到前面一格。\n要按的格子請先記住。
- es: Solo se ve una casilla por delante.\nMemoriza las que hay que pulsar.
- es-419: Solo se ve una casilla por delante.\nMemoriza las que hay que presionar.
- fr: Tu ne vois qu'une case en avant.\nRetiens celles qu'il faut presser.
- de: Du siehst nur eine Kachel voraus.\nMerk dir, welche du drücken musst.
- pt-BR: Só aparece um bloco à frente.\nDecore quais você tem que apertar.
- ru: Видно только одну клетку вперёд.\nЗапомни, какие жать.
- pl: Widać tylko jeden kafelek do przodu.\nZapamiętaj, które wciskać.

## Tip.M.Stage4.3

- ko: Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.\n"TEAMCHEER" 경고가 뜰 때 팀 키워드를 외치면 바닥이 복구됩니다.\n이미 부서진 뒤에는 다음 경고까지 버티세요.
- en: Press Ctrl to match your character to the floor color and break the lock-on.\nShout the team word while "TEAMCHEER" is up to repair the floor.\nIf it's already broken, hold out until the next warning.
- ja: Ctrlで床の色にキャラの色を合わせて、ロックオンを外してください。\n「TEAMCHEER」の間にチームの合言葉を叫ぶと、床が直ります。\nすでに壊れたあとは、次の警告まで耐えてください。
- zh-Hans: 按 Ctrl，把角色颜色对成地板颜色，甩掉锁定。\n“TEAMCHEER” 出现时喊出团队关键词，地板就会修好。\n已经碎了的话，撑到下一次警告。
- zh-Hant: 按 Ctrl，把角色顏色對成地板顏色，甩掉鎖定。\n「TEAMCHEER」出現時喊出團隊關鍵詞，地板就會修好。\n已經碎了的話，撐到下一次警告。
- es: Pulsa Ctrl para igualar el color de tu personaje al del suelo y quitar el marcaje.\nGritad la palabra de equipo mientras esté "TEAMCHEER" para reparar el suelo.\nSi ya está roto, aguantad hasta el siguiente aviso.
- es-419: Presiona Ctrl para igualar el color de tu personaje al del piso y quitar el lock-on.\nGriten la palabra de equipo mientras esté "TEAMCHEER" para reparar el piso.\nSi ya está roto, aguanten hasta el siguiente aviso.
- fr: Appuie sur Ctrl pour caler la couleur de ton perso sur le sol et casser le lock.\nCrie le mot d'équipe pendant "TEAMCHEER" pour réparer le sol.\nS'il est déjà cassé, tiens jusqu'à la prochaine alerte.
- de: Drück Ctrl, pass die Farbe deines Charakters an den Boden an und löse das Lock-on.\nRuf das Team-Wort, solange "TEAMCHEER" da ist, dann repariert sich der Boden.\nIst er schon kaputt, halt durch bis zur nächsten Warnung.
- pt-BR: Aperte Ctrl para igualar a cor do personagem à do chão e sair da mira.\nGrite a palavra da equipe enquanto o "TEAMCHEER" estiver na tela pra consertar o chão.\nSe já quebrou, aguente até o próximo aviso.
- ru: Нажми Ctrl, подгони цвет персонажа под пол — и сбрось захват.\nВыкрикни командное слово, пока висит «TEAMCHEER» — пол починится.\nЕсли уже сломано, терпи до следующего предупреждения.
- pl: Wciśnij Ctrl, dopasuj kolor postaci do podłogi i zrzuć namierzenie.\nWykrzycz hasło drużyny, gdy wisi „TEAMCHEER” — podłoga się naprawi.\nJeśli już pękła, wytrzymaj do następnego ostrzeżenia.

## Tip.M.Stage5

- ko: 고유색 칸이 나오면 그 칸 위에 서야 합니다.\n고유색이 없으면 흑백 칸 위에서 버티세요.\n바닥 색에 맞춰 캐릭터 색도 바꾸세요.\n방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: If a unique-color tile appears, stand on it.\nIf it doesn't, hold out on black or white.\nMatch your character's color to the floor too.\nThe Defense buff also blocks round-fail damage.
- ja: 固有色のマスが出たら、その上に乗ってください。\n出なければ白黒のマスで耐えてください。\n床の色に合わせてキャラの色も変えてください。\n防御バフはラウンド失敗のダメージも防いでくれます。
- zh-Hans: 出现专属色格子时，必须站上去。\n没有专属色就站在黑白格子上撑住。\n角色颜色也要对上地板。\n防御增益也能挡住回合失败的伤害。
- zh-Hant: 出現專屬色格子時，必須站上去。\n沒有專屬色就站在黑白格子上撐住。\n角色顏色也要對上地板。\n防禦增益也能擋下回合失敗的傷害。
- es: Si sale una casilla de color único, ponte encima.\nSi no, aguanta en blanco o negro.\nCambia también el color de tu personaje al del suelo.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- es-419: Si sale una casilla de color único, ponte encima.\nSi no, aguanta en blanco o negro.\nCambia también el color de tu personaje al del piso.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- fr: Si une case de couleur unique apparaît, mets-toi dessus.\nSinon, tiens bon sur le noir ou le blanc.\nChange aussi la couleur de ton perso pour matcher le sol.\nLe bonus de défense bloque aussi les dégâts d'un round raté.
- de: Wenn eine einzigfarbige Kachel kommt, stell dich drauf.\nWenn nicht, halt dich auf Schwarz oder Weiß.\nPass auch die Farbe deines Charakters an den Boden an.\nDer Verteidigungs-Buff blockt auch Schaden bei verlorener Runde.
- pt-BR: Se aparecer um bloco de cor única, fique em cima.\nSe não aparecer, aguente no preto ou branco.\nTroque a cor do personagem pra combinar com o chão também.\nO buff de defesa também bloqueia o dano de rodada perdida.
- ru: Если появляется уникальная клетка — встань на неё.\nЕсли нет — держись на чёрной или белой.\nЦвет персонажа тоже подгони под пол.\nБафф защиты также блокирует урон за проваленный раунд.
- pl: Jeśli pojawi się kafelek unikalnego koloru, stań na nim.\nJeśli nie — wytrzymaj na czarnym albo białym.\nDopasuj też kolor postaci do podłogi.\nWzmocnienie obrony blokuje też obrażenia za przegraną rundę.

## Tip.M.Boss.1

> M.Boss P1 (Barrier + 화살 + 침). `Tip.M.Stage1` 앞 두 줄 — TEAMCHEER 경고 줄은 뺌.

- ko: 한 번에 한 색의 입만 올라옵니다.\n흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.
- en: Only one color of mouth comes up at a time.\nAnyone can match their color and step on black-and-white pads.
- ja: 一度に上がる口は一色だけです。\n白黒の足場は、色を合わせて誰でも踏めます。
- zh-Hans: 一次只会升起一种颜色的嘴。\n黑白踏板谁都可以对好颜色再踩。
- zh-Hant: 一次只會升起一種顏色的嘴。\n黑白踏板誰都可以對好顏色再踩。
- es: Solo sube una boca de un color cada vez.\nCualquiera puede igualar su color y pisar las plataformas en blanco y negro.
- es-419: Solo sube una boca de un color a la vez.\nCualquiera puede igualar su color y pisar las plataformas en blanco y negro.
- fr: Une seule couleur de bouche se lève à la fois.\nTout le monde peut matcher sa couleur et marcher sur les dalles noir et blanc.
- de: Es kommt immer nur ein Mund in einer Farbe hoch.\nSchwarz-weiße Trittflächen kann jeder betreten, wenn er die Farbe anpasst.
- pt-BR: Só sobe uma boca de cada cor por vez.\nQualquer um pode acertar a cor e pisar nas plataformas preto e branco.
- ru: За раз поднимается рот только одного цвета.\nНа чёрно-белые платформы может встать любой, подстроив цвет.
- pl: Za razem unosi się usta tylko jednego koloru.\nNa czarno-białe platformy może wejść każdy, byle dopasował kolor.

## Tip.M.Boss.2

> M.Boss P2 (SideSplit 판정 + 입 닫힘).

- ko: 방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: The Defense buff also blocks round-fail damage.
- ja: 防御バフはラウンド失敗のダメージも防いでくれます。
- zh-Hans: 防御增益也能挡住回合失败的伤害。
- zh-Hant: 防禦增益也能擋下回合失敗的傷害。
- es: La mejora de defensa también bloquea el daño por fallar la ronda.
- es-419: La mejora de defensa también bloquea el daño por fallar la ronda.
- fr: Le bonus de défense bloque aussi les dégâts d'un round raté.
- de: Der Verteidigungs-Buff blockt auch Schaden bei verlorener Runde.
- pt-BR: O buff de defesa também bloqueia o dano de rodada perdida.
- ru: Бафф защиты также блокирует урон за проваленный раунд.
- pl: Wzmocnienie obrony blokuje też obrażenia za przegraną rundę.

## Tip.M.Boss.3

> M.Boss P3 (Drop + 화살 + 혀). `Tip.M.Stage4.3` 첫 줄.

- ko: Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
- en: Press Ctrl to match your character to the floor color and break the lock-on.
- ja: Ctrlで床の色にキャラの色を合わせて、ロックオンを外してください。
- zh-Hans: 按 Ctrl，把角色颜色对成地板颜色，甩掉锁定。
- zh-Hant: 按 Ctrl，把角色顏色對成地板顏色，甩掉鎖定。
- es: Pulsa Ctrl para igualar el color de tu personaje al del suelo y quitar el marcaje.
- es-419: Presiona Ctrl para igualar el color de tu personaje al del piso y quitar el lock-on.
- fr: Appuie sur Ctrl pour caler la couleur de ton perso sur le sol et casser le lock.
- de: Drück Ctrl, pass die Farbe deines Charakters an den Boden an und löse das Lock-on.
- pt-BR: Aperte Ctrl para igualar a cor do personagem à do chão e sair da mira.
- ru: Нажми Ctrl, подгони цвет персонажа под пол — и сбрось захват.
- pl: Wciśnij Ctrl, dopasuj kolor postaci do podłogi i zrzuć namierzenie.

## Tip.M.Boss.4

> M.Boss P4 (`MouthBossJawSmash` — 닫힘 → 바닥 파괴 → 열린 뒤 응원으로 복구).

- ko: 입이 열리면 팀 키워드를 외쳐 부서진 바닥을 복구하세요.
- en: When the mouth opens, shout your team word to repair the broken floor.
- ja: 口が開いたら、チームの合言葉を叫んで壊れた床を直してください。
- zh-Hans: 嘴张开后，喊出团队关键词修好碎掉的地板。
- zh-Hant: 嘴張開後，喊出團隊關鍵詞修好碎掉的地板。
- es: Cuando se abra la boca, gritad la palabra de equipo para reparar el suelo roto.
- es-419: Cuando se abra la boca, griten la palabra de equipo para reparar el piso roto.
- fr: Quand la bouche s'ouvre, crie le mot d'équipe pour réparer le sol cassé.
- de: Wenn sich der Mund öffnet, ruf das Team-Wort, um den kaputten Boden zu reparieren.
- pt-BR: Quando a boca abrir, grite a palavra da equipe pra consertar o chão quebrado.
- ru: Когда рот откроется, выкрикни командное слово — сломанный пол починится.
- pl: Gdy usta się otworzą, wykrzycz hasło drużyny, żeby naprawić pękniętą podłogę.

---

## Tip.T.Stage1

- ko: 내 색이 뜬 양옆 벽에 부딪히면 벽이 뒤로 물러납니다.
- en: Ram the side walls when they show your color — they'll get knocked back.
- ja: 自分の色が出た左右の壁にぶつかると、壁が下がります。
- zh-Hans: 撞上亮着自己颜色的两侧墙壁，墙就会往后退。
- zh-Hant: 撞上亮著自己顏色的兩側牆壁，牆就會往後退。
- es: Si chocas contra las paredes laterales cuando muestran tu color, retroceden.
- es-419: Si chocas contra las paredes laterales cuando muestren tu color, retroceden.
- fr: Si tu percutes les murs latéraux quand ils affichent ta couleur, ils reculent.
- de: Wenn du gegen die Seitenwände knallst, die deine Farbe zeigen, weichen sie zurück.
- pt-BR: Se bater nas paredes laterais quando elas mostrarem a sua cor, elas recuam.
- ru: Если врезаться в боковые стены своего цвета, они отступят.
- pl: Jak walniesz w boczne ściany w swoim kolorze, cofną się.

## Tip.T.Stage2.1

- ko: 길을 외워 두세요.
- en: Memorize the path.
- ja: 道を覚えておいてください。
- zh-Hans: 把路记下来。
- zh-Hant: 把路記下來。
- es: Memoriza el camino.
- es-419: Memoriza el camino.
- fr: Retiens le chemin.
- de: Merk dir den Weg.
- pt-BR: Decore o caminho.
- ru: Запомни дорогу.
- pl: Zapamiętaj drogę.

## Tip.T.Stage2.2

- ko: 자기 색 칸만 밟으세요.\n칸 색이 맞아도 캐릭터가 흑백이면 안 됩니다.
- en: Only step on tiles of your color.\nEven if the tile matches, don't do it while you're black or white.
- ja: 自分の色のマスだけ踏んでください。\nマスの色が合っていても、キャラが白黒のときはダメです。
- zh-Hans: 只踩自己颜色的格子。\n格子颜色对了，角色却是黑白也不行。
- zh-Hant: 只踩自己顏色的格子。\n格子顏色對了，角色卻是黑白也不行。
- es: Pisa solo las casillas de tu color.\nAunque el color coincida, no vale si tu personaje está en blanco y negro.
- es-419: Pisa solo las casillas de tu color.\nAunque el color coincida, no vale si tu personaje está en blanco y negro.
- fr: Marche uniquement sur tes cases de couleur.\nMême si la case match, ça ne marche pas si ton perso est en noir et blanc.
- de: Tritt nur auf Kacheln deiner Farbe.\nAuch wenn die Kachel stimmt: Als Schwarz oder Weiß geht das nicht.
- pt-BR: Pise só nos blocos da sua cor.\nMesmo se a cor do bloco bater, não vale se o personagem estiver preto ou branco.
- ru: Ступай только на клетки своего цвета.\nДаже если цвет клетки совпал — в чёрно-белом нельзя.
- pl: Stąpaj tylko po kafelkach swojego koloru.\nNawet gdy kolor kafelka się zgadza — na czarno-białym nie wolno.

## Tip.T.Stage2.3

- ko: 담당 색이 먼저 지나가야 다른 팀원도 그 바닥을 밟을 수 있습니다.
- en: The assigned color has to go first before anyone else can step on that floor.
- ja: 担当色が先に通らないと、他のメンバーはその床を踏めません。
- zh-Hans: 负责的颜色先过去，其他队员才能踩那块地板。
- zh-Hant: 負責的顏色先過去，其他隊員才能踩那塊地板。
- es: El color encargado tiene que pasar primero para que el resto pueda pisar ese suelo.
- es-419: El color encargado tiene que pasar primero para que el resto pueda pisar ese piso.
- fr: La couleur assignée doit passer en premier, sinon les autres ne peuvent pas marcher sur ce sol.
- de: Die zuständige Farbe muss zuerst rüber, sonst dürfen die anderen den Boden nicht betreten.
- pt-BR: A cor responsável tem que passar primeiro pra o resto do time poder pisar nesse chão.
- ru: Сначала должен пройти свой цвет, иначе остальные не могут наступать на этот пол.
- pl: Najpierw musi przejść przypisany kolor, inaczej reszta nie może stąpać po tej podłodze.

## Tip.T.Stage3

- ko: 양옆 벽에 색을 맞춰 부딪히면 벽이 뒤로 물러납니다.
- en: Match the side walls' color and ram them — they'll get knocked back.
- ja: 左右の壁に色を合わせてぶつかると、壁が下がります。
- zh-Hans: 对好两侧墙壁的颜色再撞上去，墙就会往后退。
- zh-Hant: 對好兩側牆壁的顏色再撞上去，牆就會往後退。
- es: Iguala el color de las paredes laterales y choca: retroceden.
- es-419: Iguala el color de las paredes laterales y choca: retroceden.
- fr: Aligne la couleur des murs latéraux et percute-les : ils reculent.
- de: Pass die Farbe der Seitenwände an und knall dagegen — sie weichen zurück.
- pt-BR: Acerte a cor das paredes laterais e bata nelas — elas recuam.
- ru: Подгони цвет боковых стен и врежься — они отступят.
- pl: Dopasuj kolor bocznych ścian i walnij — cofną się.

## Tip.T.Stage4

- ko: 앞뒤 벽과 부종에 닿으면 튕겨 나갑니다.
- en: Touching the front or back walls, or the swellings, will bounce you.
- ja: 前後の壁と腫れに触れると弾かれます。
- zh-Hans: 碰到前后的墙或肿块会被弹开。
- zh-Hant: 碰到前後的牆或腫塊會被彈開。
- es: Si tocas las paredes de delante o detrás, o los bultos, te lanzan.
- es-419: Si tocas las paredes de adelante o atrás, o los bultos, te aventan.
- fr: Toucher les murs avant/arrière ou les gonflements te projette.
- de: Vordere und hintere Wände und Schwellungen schleudern dich weg.
- pt-BR: Encostar nas paredes da frente e de trás ou nos inchaços te arremessa.
- ru: Касание передних и задних стен или отёков отбрасывает.
- pl: Kontakt z przednimi i tylnymi ścianami albo obrzękami cię odbija.

## Tip.T.Stage5.1

> 2026-09-18 러너 재설계로 13개 로케일 전부 교체 (`TStage5RunnerRedesign.md`).
> 옛 흑백 토글 문구는 폐기. String Table 에셋에도 반영 완료(`StageTip_*.asset`).
> ko/en 외 11개는 **기계번역이므로 원어민 검수 전** — Steam AI 표기 검토 대상
> (`project_steam_ai_disclosure`).

- ko: 러너 한 명이 미로를 달리고, 나머지는 2층에서 길을 안내합니다.\n패드를 밟으면 그 색 문만 열리고 나머지는 전부 닫힙니다.\n고유색 패드는 그 색 플레이어만, 흑·백 패드는 누구나 밟을 수 있습니다.
- en: One runner races through the maze while the others guide from the second floor.\nStepping on a pad opens only that color's doors and closes all the rest.\nColored pads work only for that color's player; black and white pads work for anyone.
- ja: ランナー1人が迷路を走り、残りは2階から道を案内します。\nパッドを踏むと、その色の扉だけが開き、ほかはすべて閉じます。\n固有色のパッドはその色のプレイヤーだけ、黒と白のパッドは誰でも踏めます。
- zh-Hans: 一名奔跑者在迷宫中奔跑，其他人在二楼指路。\n踩下踏板后，只有该颜色的门会打开，其余全部关闭。\n专属颜色的踏板只有该颜色的玩家能踩，黑白踏板任何人都能踩。
- zh-Hant: 一名奔跑者在迷宮中奔跑，其他人在二樓指路。\n踩下踏板後，只有該顏色的門會打開，其餘全部關閉。\n專屬顏色的踏板只有該顏色的玩家能踩，黑白踏板任何人都能踩。
- es: Un corredor recorre el laberinto mientras los demás guían desde la segunda planta.\nPisar un pad abre solo las puertas de ese color y cierra todas las demás.\nLos pads de color solo valen para el jugador de ese color; los negros y blancos valen para cualquiera.
- es-419: Un corredor recorre el laberinto mientras los demás guían desde el segundo piso.\nPisar un pad abre solo las puertas de ese color y cierra todas las demás.\nLos pads de color solo valen para el jugador de ese color; los negros y blancos valen para cualquiera.
- fr: Un coureur traverse le labyrinthe pendant que les autres le guident depuis le deuxième étage.\nMarcher sur un pad ouvre uniquement les portes de cette couleur et ferme toutes les autres.\nLes pads de couleur ne marchent que pour le joueur de cette couleur ; les pads noirs et blancs marchent pour tout le monde.
- de: Ein Läufer rennt durch das Labyrinth, während die anderen vom zweiten Stock aus lotsen.\nEin Pad zu betreten öffnet nur die Türen dieser Farbe und schließt alle anderen.\nFarbige Pads gelten nur für den Spieler dieser Farbe; schwarze und weiße Pads gelten für alle.
- pt-BR: Um corredor atravessa o labirinto enquanto os outros guiam do segundo andar.\nPisar em um pad abre só as portas daquela cor e fecha todas as outras.\nPads coloridos valem só para o jogador daquela cor; pads pretos e brancos valem para qualquer um.
- pt: (pt-BR과 동일 — 기존 이 키의 관례를 따름)
- ru: Один бегун мчится по лабиринту, остальные направляют его со второго этажа.\nНаступив на площадку, вы откроете двери только этого цвета, а все остальные закроются.\nЦветные площадки работают только для игрока того же цвета, чёрные и белые — для всех.
- pl: Jeden biegacz pędzi przez labirynt, a reszta naprowadza go z drugiego piętra.\nNadepnięcie na płytkę otwiera tylko drzwi w tym kolorze, a wszystkie pozostałe zamyka.\nKolorowe płytki działają tylko dla gracza w tym kolorze, czarne i białe — dla każdego.

---

## Tip.T.Boss.1

- ko: 지정된 색이 길을 연 뒤 목표까지 도달하세요.\n\n사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: Have the assigned color open the path, then reach the goal.\n\nFinish this section before the candy hits the ground.
- ja: 指定された色が道を開いてから、ゴールまで到達してください。\n\nキャンディーが地面に着く前に、この区間を終わらせてください。
- zh-Hans: 指定颜色开路后，到达终点。\n\n在糖果落到地上之前，清掉这一段。
- zh-Hant: 指定顏色開路後，到達終點。\n\n在糖果落到地上之前，清掉這一段。
- es: El color asignado abre el camino; luego llegad a la meta.\n\nTerminad este tramo antes de que el caramelo toque el suelo.
- es-419: El color asignado abre el camino; luego lleguen a la meta.\n\nTerminen este tramo antes de que el caramelo toque el piso.
- fr: La couleur désignée ouvre le chemin, puis atteins l'objectif.\n\nFinis ce passage avant que le bonbon touche le sol.
- de: Die vorgesehene Farbe macht den Weg frei, dann erreicht das Ziel.\n\nBeendet diesen Abschnitt, bevor das Bonbon den Boden berührt.
- pt-BR: A cor designada abre o caminho, depois chegue no objetivo.\n\nTermine esse trecho antes do doce encostar no chão.
- ru: Назначенный цвет открывает путь — потом доберись до цели.\n\nЗакончи этот отрезок, пока конфета не коснётся земли.
- pl: Wyznaczony kolor otwiera drogę, potem dotrzyj do celu.\n\nDokończ ten odcinek, zanim cukierek uderzy o ziemię.

## Tip.T.Boss.2

- ko: 발판을 눌러 길을 만들고 목표까지 도달하세요.\n\n사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: Step on the pads to make a path, then reach the goal.\n\nFinish this section before the candy hits the ground.
- ja: 足場を踏んで道を作り、ゴールまで到達してください。\n\nキャンディーが地面に着く前に、この区間を終わらせてください。
- zh-Hans: 踩踏板开路，到达终点。\n\n在糖果落到地上之前，清掉这一段。
- zh-Hant: 踩踏板開路，到達終點。\n\n在糖果落到地上之前，清掉這一段。
- es: Pisad las plataformas para hacer un camino y llegad a la meta.\n\nTerminad este tramo antes de que el caramelo toque el suelo.
- es-419: Pisen las plataformas para hacer un camino y lleguen a la meta.\n\nTerminen este tramo antes de que el caramelo toque el piso.
- fr: Marche sur les dalles pour faire un chemin, puis atteins l'objectif.\n\nFinis ce passage avant que le bonbon touche le sol.
- de: Tretet auf die Platten, baut einen Weg, erreicht das Ziel.\n\nBeendet diesen Abschnitt, bevor das Bonbon den Boden berührt.
- pt-BR: Pise nas plataformas pra fazer um caminho e chegue no objetivo.\n\nTermine esse trecho antes do doce encostar no chão.
- ru: Наступай на платформы, проложи путь и доберись до цели.\n\nЗакончи этот отрезок, пока конфета не коснётся земли.
- pl: Wdepń w płyty, zrób drogę i dotrzyj do celu.\n\nDokończ ten odcinek, zanim cukierek uderzy o ziemię.

## Tip.T.Boss.3

- ko: 가운데 발판을 밟아 튕겨 올라가 사탕과 색을 맞춰 부딪히세요.\n\n사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: Step on the center pad, bounce up, and ram the candy in matching color.\n\nFinish this section before the candy hits the ground.
- ja: 中央の足場を踏んで跳ね上がり、キャンディーに色を合わせてぶつかってください。\n\nキャンディーが地面に着く前に、この区間を終わらせてください。
- zh-Hans: 踩中间的踏板弹上去，对好颜色撞糖果。\n\n在糖果落到地上之前，清掉这一段。
- zh-Hant: 踩中間的踏板彈上去，對好顏色撞糖果。\n\n在糖果落到地上之前，清掉這一段。
- es: Pisa la plataforma del centro, salta y choca contra el caramelo del color que toque.\n\nTermina este tramo antes de que el caramelo toque el suelo.
- es-419: Pisa la plataforma del centro, salta y choca contra el caramelo del color que toque.\n\nTermina este tramo antes de que el caramelo toque el piso.
- fr: Marche sur la dalle du centre, rebondis, et percute le bonbon de la bonne couleur.\n\nFinis ce passage avant que le bonbon touche le sol.
- de: Tritt auf die mittlere Platte, spring hoch und knall farblich passend gegen das Bonbon.\n\nBeende diesen Abschnitt, bevor das Bonbon den Boden berührt.
- pt-BR: Pise na plataforma do meio, pule e bata no doce na cor certa.\n\nTermine esse trecho antes do doce encostar no chão.
- ru: Наступи на центральную платформу, подпрыгни и врежься в конфету нужным цветом.\n\nЗакончи этот отрезок, пока конфета не коснётся земли.
- pl: Wdepń w środkową płytę, odbij się w górę i walnij w cukierek w pasującym kolorze.\n\nDokończ ten odcinek, zanim cukierek uderzy o ziemię.

## Tip.T.Boss.4

- ko: 색을 맞춰 벽에 부딪혀 밀어내세요.
- en: Match the wall's color and ram it to push it back.
- ja: 壁に色を合わせてぶつかり、押し返してください。
- zh-Hans: 对好墙壁颜色撞上去，把墙推回去。
- zh-Hant: 對好牆壁顏色撞上去，把牆推回去。
- es: Iguala el color de la pared y choca para empujarla.
- es-419: Iguala el color de la pared y choca para empujarla.
- fr: Aligne la couleur du mur et percute-le pour le repousser.
- de: Pass die Wandfarbe an und knall dagegen, um sie zurückzudrücken.
- pt-BR: Acerte a cor da parede e bata nela pra empurrar.
- ru: Подгони цвет стены и врежься, чтобы оттолкнуть её.
- pl: Dopasuj kolor ściany i walnij, żeby ją odepchnąć.

---

## 적용 체크리스트 (사용자 — 에디터)

1. String Table Collection `StageTip`(가칭) 생성, 키는 위 `Tip.*` 그대로.
2. 각 로케일 칸에 이 문서 값을 넣는다. `\n` / `\n\n` 은 TMP 실제 줄바꿈.
3. `Tip_Panel/Txt.Tip`에 `LocalizeStringEvent` — 페이즈가 바뀌면 키만 갈아끼운다 (코드 단계에서).
4. **[2026-09-17]** M.Boss도 Tip 켬 — `Tip.M.Boss.1`~`4`를 `BossFlow` PhaseManager 각 페이즈 `onPhaseEnter` → `TipUI.ShowTip`에 연결, `Tip_Panel.hideOnAllPhasesComplete` ON(Bossdown 숨김). MCP 적용 완료. T.Boss P4는 사탕 줄 없음.
5. **[2026-09-17]** `Tip.M.Stage2.1` / `Tip.M.Stage5` 끝에 방어 버프 줄 추가(구 `Tutorial.Board_Controls.Row_BuffNote` 이전). 13로케일 렌더 결과 넘침 없음 — `Tip.M.Stage5`만 ja 18 · ru 19로 축소, 나머지 20.
