# StageTipTranslations — Tip HUD 11개 언어

> SSOT 원문: [`StageTipLines.md`](StageTipLines.md) (한국어 확정본).
> `en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl` (`SteamworksIntegrationDesign.md` 트랙4 §10, ko 제외).
>
> String Table / 씬은 에이전트가 쓰지 않는다. 사람이 Localization Tables에 넣는다.
> 한 키 = Tip TMP 한 칸. `\n` = 줄바꿈, `\n\n` = T.Boss 사탕 줄 앞 빈 줄.
> 표시 시 비어 있지 않은 줄 앞에 `•` 불릿. 빈 줄에는 점 없음. `Tools/Setup StageTip Localization`이 테이블에 같이 넣는다.

> **2026-09-24 전면 재번역:** ko·en 확정 후 **영어 기준**으로 10개 언어 재작성(대사 개정 작업과 함께). `Tip.M.Stage4.1`·`Tip.T.Stage2.1`·`Tip.M.Boss.4`는 영어가 안 바뀌어 기존 번역 유지. String Table에도 반영 완료(`pt`는 `pt-BR`과 동일).

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
- en: Only one color's mouth rises at a time.
Anyone can use black and white pads by matching their color.
When "TEAMCHEER" pops up at the top, shout your team word.
- ja: 一度に上がる口は一色だけです。\n白黒の足場は、色を合わせれば誰でも使えます。\n上に「TEAMCHEER」が出たら、チームの合言葉を叫んでください。
- zh-Hans: 一次只会升起一种颜色的嘴。\n黑白踏板谁都能用，对好颜色就行。\n顶部出现“TEAMCHEER”时，喊出团队关键词。
- zh-Hant: 一次只會升起一種顏色的嘴。\n黑白踏板誰都能用，對好顏色就行。\n頂部出現「TEAMCHEER」時，喊出團隊關鍵詞。
- es: Solo sube la boca de un color cada vez.\nCualquiera puede usar las placas blancas y negras si iguala su color.\nCuando salga "TEAMCHEER" arriba, gritad la palabra de equipo.
- es-419: Solo sube la boca de un color a la vez.\nCualquiera puede usar las placas blancas y negras si iguala su color.\nCuando aparezca "TEAMCHEER" arriba, griten la palabra de equipo.
- fr: Une seule bouche de couleur se lève à la fois.\nTout le monde peut utiliser les dalles noires et blanches en accordant sa couleur.\nQuand "TEAMCHEER" s'affiche en haut, crie le mot d'équipe.
- de: Es fährt immer nur ein Mund einer Farbe hoch.\nSchwarze und weiße Platten kann jeder nutzen, der seine Farbe anpasst.\nWenn oben "TEAMCHEER" erscheint, ruf das Team-Wort.
- pt-BR: Só sobe a boca de uma cor por vez.\nQualquer um pode usar as placas pretas e brancas ajustando a cor.\nQuando "TEAMCHEER" aparecer no topo, grite a palavra da equipe.
- ru: За раз поднимается рот только одного цвета.\nЧёрные и белые плиты может использовать любой, подстроив цвет.\nКогда сверху появится «TEAMCHEER», выкрикни командное слово.
- pl: Naraz unoszą się usta tylko jednego koloru.\nZ czarnych i białych płytek może korzystać każdy, kto dopasuje kolor.\nGdy u góry pojawi się „TEAMCHEER”, wykrzycz hasło drużyny.

## Tip.M.Stage2.1

- ko: 간판 아래 꿀떡 수만큼 그 구역에 들어가세요.
비대칭 정보를 각자 가지고 있습니다. 서로 공유하세요.
상단에 "TEAMCHEER" 경고가 뜨면 팀 키워드를 외치세요.
방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: Each zone needs as many players as there are KKUL-TTEOK under its sign — no more, no less.
Everyone sees something different. Share what you see!
When "TEAMCHEER" pops up at the top, shout your team word.
Defense buff also blocks round-fail damage.
- ja: 各ゾーンには、看板の下のKKUL-TTEOKと同じ人数が必要です。多くても少なくてもダメです。\n見えているものは人によって違います。見えたものを伝え合ってください。\n上に「TEAMCHEER」が出たら、チームの合言葉を叫んでください。\n防御バフはラウンド失敗のダメージも防ぎます。
- zh-Hans: 每个区域的人数要和牌子下的 KKUL-TTEOK 数量一样，不多不少。\n每个人看到的都不一样，把你看到的说出来！\n顶部出现“TEAMCHEER”时，喊出团队关键词。\n防御增益也能挡住回合失败的伤害。
- zh-Hant: 每個區域的人數要和牌子下的 KKUL-TTEOK 數量一樣，不多不少。\n每個人看到的都不一樣，把你看到的說出來！\n頂部出現「TEAMCHEER」時，喊出團隊關鍵詞。\n防禦增益也能擋下回合失敗的傷害。
- es: Cada zona necesita tantos jugadores como KKUL-TTEOK haya bajo su cartel: ni más ni menos.\nCada uno ve algo distinto. ¡Compartid lo que veis!\nCuando salga "TEAMCHEER" arriba, gritad la palabra de equipo.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- es-419: Cada zona necesita tantos jugadores como KKUL-TTEOK haya bajo su letrero: ni más ni menos.\nCada uno ve algo distinto. ¡Compartan lo que ven!\nCuando aparezca "TEAMCHEER" arriba, griten la palabra de equipo.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- fr: Chaque zone doit avoir autant de joueurs que de KKUL-TTEOK sous son panneau — ni plus, ni moins.\nChacun voit quelque chose de différent. Partagez ce que vous voyez !\nQuand "TEAMCHEER" s'affiche en haut, crie le mot d'équipe.\nLe bonus de défense bloque aussi les dégâts d'un round raté.
- de: Jede Zone braucht genau so viele Spieler, wie KKUL-TTEOK unter ihrem Schild sind – nicht mehr, nicht weniger.\nJeder sieht etwas anderes. Sagt an, was ihr seht!\nWenn oben "TEAMCHEER" erscheint, ruf das Team-Wort.\nDer Verteidigungs-Buff blockt auch Schaden bei verlorener Runde.
- pt-BR: Cada área precisa de tantos jogadores quanto os KKUL-TTEOK embaixo da placa — nem mais, nem menos.\nCada um vê uma coisa diferente. Contem o que estão vendo!\nQuando "TEAMCHEER" aparecer no topo, grite a palavra da equipe.\nO buff de defesa também bloqueia o dano de rodada perdida.
- ru: В каждой зоне должно быть столько игроков, сколько KKUL-TTEOK под её табличкой, — ни больше ни меньше.\nКаждый видит своё. Рассказывайте, что видите!\nКогда сверху появится «TEAMCHEER», выкрикни командное слово.\nБафф защиты также блокирует урон за проваленный раунд.
- pl: W każdej strefie musi być tylu graczy, ile KKUL-TTEOK jest pod jej tabliczką — ani więcej, ani mniej.\nKażdy widzi coś innego. Mówcie, co widzicie!\nGdy u góry pojawi się „TEAMCHEER”, wykrzycz hasło drużyny.\nWzmocnienie obrony blokuje też obrażenia za przegraną rundę.

## Tip.M.Stage2.2

- ko: Ctrl로 흑/백 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
- en: Press Ctrl to match your color to the black or white floor and avoid being targeted.
- ja: Ctrlで自分の色を白黒の床に合わせて、狙われないようにしてください。
- zh-Hans: 按 Ctrl 把你的颜色换成黑白地板的颜色，避免被瞄准。
- zh-Hant: 按 Ctrl 把你的顏色換成黑白地板的顏色，避免被瞄準。
- es: Pulsa Ctrl para igualar tu color con el suelo blanco o negro y que no te apunten.
- es-419: Presiona Ctrl para igualar tu color con el piso blanco o negro y que no te apunten.
- fr: Appuie sur Ctrl pour accorder ta couleur au sol noir ou blanc et ne plus être ciblé.
- de: Drück Ctrl, um deine Farbe an den schwarzen oder weißen Boden anzupassen, damit du nicht anvisiert wirst.
- pt-BR: Aperte Ctrl pra igualar sua cor ao chão preto ou branco e não virar alvo.
- ru: Нажми Ctrl, чтобы подогнать свой цвет под чёрный или белый пол и не попасть под прицел.
- pl: Wciśnij Ctrl, żeby dopasować swój kolor do czarnej lub białej podłogi i nie być na celowniku.

## Tip.M.Stage3

- ko: 타일을 2초 동안 밟아야 점수가 올라갑니다.
고유색 타일은 해당 색만, 흑백 타일은 누구나 점수를 올릴 수 있습니다.
- en: Stand on a tile for 2 seconds to score.
Colored tiles only count for that color. Black and white tiles count for anyone.
- ja: タイルに2秒乗ると得点です。\n色付きのタイルはその色の人だけ、白黒のタイルは誰でも得点になります。
- zh-Hans: 在方块上站 2 秒得分。\n彩色方块只算对应颜色的人，黑白方块谁都算。
- zh-Hant: 在方塊上站 2 秒得分。\n彩色方塊只算對應顏色的人，黑白方塊誰都算。
- es: Quédate 2 segundos en una baldosa para puntuar.\nLas de color solo cuentan para ese color; las blancas y negras, para cualquiera.
- es-419: Quédate 2 segundos en una baldosa para puntuar.\nLas de color solo cuentan para ese color; las blancas y negras, para cualquiera.
- fr: Reste 2 secondes sur une dalle pour marquer.\nLes dalles colorées ne comptent que pour leur couleur ; les noires et blanches, pour tout le monde.
- de: Bleib 2 Sekunden auf einer Kachel, um zu punkten.\nBunte Kacheln zählen nur für ihre Farbe, schwarze und weiße für alle.
- pt-BR: Fique 2 segundos num bloco pra marcar ponto.\nBlocos coloridos só contam pra sua cor; pretos e brancos contam pra qualquer um.
- ru: Постой на плитке 2 секунды, чтобы получить очко.\nЦветные плитки засчитываются только своему цвету, чёрные и белые — всем.
- pl: Postój 2 sekundy na kafelku, żeby zdobyć punkt.\nKolorowe kafelki liczą się tylko dla swojego koloru, czarne i białe — dla każdego.

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
- en: You only see one tile ahead.
Memorize which tile to press.
- ja: 1マス先の床しか見えません。\n押すマスを覚えておいてください。
- zh-Hans: 只能看到前面一格。\n记住要按哪一格。
- zh-Hant: 只能看到前面一格。\n記住要按哪一格。
- es: Solo ves la siguiente baldosa.\nMemoriza cuál tienes que pulsar.
- es-419: Solo ves la siguiente baldosa.\nMemoriza cuál tienes que presionar.
- fr: Tu ne vois que la case suivante.\nRetiens sur laquelle appuyer.
- de: Du siehst nur ein Feld voraus.\nMerk dir, welches du drücken musst.
- pt-BR: Você só vê um bloco à frente.\nGrave qual bloco apertar.
- ru: Видна только одна плитка вперёд.\nЗапоминай, на какую нажимать.
- pl: Widzisz tylko jeden kafelek do przodu.\nZapamiętaj, który trzeba wcisnąć.

## Tip.M.Stage4.3

- ko: Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.\n"TEAMCHEER" 경고가 뜰 때 팀 키워드를 외치면 바닥이 복구됩니다.\n이미 부서진 뒤에는 다음 경고까지 버티세요.
- en: Press Ctrl to match your color to the floor and avoid being targeted.
Shout your team word while "TEAMCHEER" is up to repair the floor.
If it's already broken, hold out until the next warning.
- ja: Ctrlで自分の色を床に合わせて、狙われないようにしてください。\n「TEAMCHEER」が出ている間にチームの合言葉を叫ぶと、床が直ります。\nもう壊れてしまったら、次の警告まで耐えてください。
- zh-Hans: 按 Ctrl 把你的颜色换成地板的颜色，避免被瞄准。\n“TEAMCHEER”出现时喊出团队关键词，就能修好地板。\n如果已经碎了，就撑到下一次警告。
- zh-Hant: 按 Ctrl 把你的顏色換成地板的顏色，避免被瞄準。\n「TEAMCHEER」出現時喊出團隊關鍵詞，就能修好地板。\n如果已經碎了，就撐到下一次警告。
- es: Pulsa Ctrl para igualar tu color con el suelo y que no te apunten.\nGritad la palabra de equipo mientras aparezca "TEAMCHEER" para reparar el suelo.\nSi ya se ha roto, aguanta hasta el siguiente aviso.
- es-419: Presiona Ctrl para igualar tu color con el piso y que no te apunten.\nGriten la palabra de equipo mientras aparezca "TEAMCHEER" para reparar el piso.\nSi ya se rompió, aguanta hasta la siguiente advertencia.
- fr: Appuie sur Ctrl pour accorder ta couleur au sol et ne plus être ciblé.\nCrie le mot d'équipe pendant que "TEAMCHEER" est affiché pour réparer le sol.\nS'il est déjà cassé, tiens jusqu'à la prochaine alerte.
- de: Drück Ctrl, um deine Farbe an den Boden anzupassen, damit du nicht anvisiert wirst.\nRuf das Team-Wort, solange "TEAMCHEER" angezeigt wird, um den Boden zu reparieren.\nIst er schon kaputt, halt bis zur nächsten Warnung durch.
- pt-BR: Aperte Ctrl pra igualar sua cor à do chão e não virar alvo.\nGrite a palavra da equipe enquanto "TEAMCHEER" estiver na tela pra consertar o chão.\nSe já quebrou, aguente até o próximo aviso.
- ru: Нажми Ctrl, чтобы подогнать свой цвет под пол и не попасть под прицел.\nВыкрикни командное слово, пока горит «TEAMCHEER», — и пол восстановится.\nЕсли пол уже сломан, продержись до следующего предупреждения.
- pl: Wciśnij Ctrl, żeby dopasować swój kolor do podłogi i nie być na celowniku.\nWykrzycz hasło drużyny, póki widać „TEAMCHEER”, żeby naprawić podłogę.\nJeśli już pękła, wytrzymaj do następnego ostrzeżenia.

## Tip.M.Stage5

- ko: 고유색 칸이 나오면 그 칸 위에 서야 합니다.\n고유색이 없으면 흑백 칸 위에서 버티세요.\n바닥 색에 맞춰 캐릭터 색도 바꾸세요.\n방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: If a tile in your color appears, stand on it.
If not, hold out on a black or white tile.
Match your character's color to the floor, too.
Defense buff also blocks round-fail damage.
- ja: 自分の色のマスが出たら、その上に乗ってください。\n出なければ、黒か白のマスで耐えてください。\nキャラの色も床に合わせてください。\n防御バフはラウンド失敗のダメージも防ぎます。
- zh-Hans: 出现你颜色的格子就站上去。\n没有的话，就站在黑色或白色格子上撑住。\n角色颜色也要对上地板。\n防御增益也能挡住回合失败的伤害。
- zh-Hant: 出現你顏色的格子就站上去。\n沒有的話，就站在黑色或白色格子上撐住。\n角色顏色也要對上地板。\n防禦增益也能擋下回合失敗的傷害。
- es: Si sale una casilla de tu color, ponte encima.\nSi no, aguanta en una casilla negra o blanca.\nIguala también el color de tu personaje con el del suelo.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- es-419: Si sale una casilla de tu color, ponte encima.\nSi no, aguanta en una casilla negra o blanca.\nIguala también el color de tu personaje con el del piso.\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- fr: Si une case de ta couleur apparaît, mets-toi dessus.\nSinon, tiens bon sur une case noire ou blanche.\nAccorde aussi la couleur de ton perso au sol.\nLe bonus de défense bloque aussi les dégâts d'un round raté.
- de: Kommt ein Feld in deiner Farbe, stell dich drauf.\nWenn nicht, halt dich auf einem schwarzen oder weißen Feld.\nPass auch die Farbe deines Charakters an den Boden an.\nDer Verteidigungs-Buff blockt auch Schaden bei verlorener Runde.
- pt-BR: Se aparecer um bloco da sua cor, fique em cima dele.\nSe não, aguente num bloco preto ou branco.\nAjuste também a cor do personagem à do chão.\nO buff de defesa também bloqueia o dano de rodada perdida.
- ru: Если появилась клетка твоего цвета — встань на неё.\nЕсли нет — держись на чёрной или белой клетке.\nЦвет персонажа тоже подгони под пол.\nБафф защиты также блокирует урон за проваленный раунд.
- pl: Jeśli pojawi się pole w twoim kolorze, stań na nim.\nJeśli nie — wytrzymaj na czarnym albo białym polu.\nDopasuj też kolor postaci do podłogi.\nWzmocnienie obrony blokuje też obrażenia za przegraną rundę.

## Tip.M.Boss.1

> M.Boss P1 (Barrier + 화살 + 침). `Tip.M.Stage1` 앞 두 줄 — TEAMCHEER 경고 줄은 뺌.

- ko: 한 번에 한 색의 입만 올라옵니다.\n흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.
- en: Only one color's mouth rises at a time.
Anyone can use black and white pads by matching their color.
- ja: 一度に上がる口は一色だけです。\n白黒の足場は、色を合わせれば誰でも使えます。
- zh-Hans: 一次只会升起一种颜色的嘴。\n黑白踏板谁都能用，对好颜色就行。
- zh-Hant: 一次只會升起一種顏色的嘴。\n黑白踏板誰都能用，對好顏色就行。
- es: Solo sube la boca de un color cada vez.\nCualquiera puede usar las placas blancas y negras si iguala su color.
- es-419: Solo sube la boca de un color a la vez.\nCualquiera puede usar las placas blancas y negras si iguala su color.
- fr: Une seule bouche de couleur se lève à la fois.\nTout le monde peut utiliser les dalles noires et blanches en accordant sa couleur.
- de: Es fährt immer nur ein Mund einer Farbe hoch.\nSchwarze und weiße Platten kann jeder nutzen, der seine Farbe anpasst.
- pt-BR: Só sobe a boca de uma cor por vez.\nQualquer um pode usar as placas pretas e brancas ajustando a cor.
- ru: За раз поднимается рот только одного цвета.\nЧёрные и белые плиты может использовать любой, подстроив цвет.
- pl: Naraz unoszą się usta tylko jednego koloru.\nZ czarnych i białych płytek może korzystać każdy, kto dopasuje kolor.

## Tip.M.Boss.2

> M.Boss P2 (SideSplit 판정 + 입 닫힘).

- ko: 비대칭 정보를 각자 가지고 있습니다. 서로 공유하세요.
방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: Everyone sees something different. Share what you see!
Defense buff also blocks round-fail damage.
- ja: 見えているものは人によって違います。見えたものを伝え合ってください。\n防御バフはラウンド失敗のダメージも防ぎます。
- zh-Hans: 每个人看到的都不一样，把你看到的说出来！\n防御增益也能挡住回合失败的伤害。
- zh-Hant: 每個人看到的都不一樣，把你看到的說出來！\n防禦增益也能擋下回合失敗的傷害。
- es: Cada uno ve algo distinto. ¡Compartid lo que veis!\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- es-419: Cada uno ve algo distinto. ¡Compartan lo que ven!\nLa mejora de defensa también bloquea el daño por fallar la ronda.
- fr: Chacun voit quelque chose de différent. Partagez ce que vous voyez !\nLe bonus de défense bloque aussi les dégâts d'un round raté.
- de: Jeder sieht etwas anderes. Sagt an, was ihr seht!\nDer Verteidigungs-Buff blockt auch Schaden bei verlorener Runde.
- pt-BR: Cada um vê uma coisa diferente. Contem o que estão vendo!\nO buff de defesa também bloqueia o dano de rodada perdida.
- ru: Каждый видит своё. Рассказывайте, что видите!\nБафф защиты также блокирует урон за проваленный раунд.
- pl: Każdy widzi coś innego. Mówcie, co widzicie!\nWzmocnienie obrony blokuje też obrażenia za przegraną rundę.

## Tip.M.Boss.3

> M.Boss P3 (Drop + 화살 + 혀). `Tip.M.Stage4.3` 첫 줄.

- ko: Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
- en: Press Ctrl to match your color to the floor and avoid being targeted.
- ja: Ctrlで自分の色を床に合わせて、狙われないようにしてください。
- zh-Hans: 按 Ctrl 把你的颜色换成地板的颜色，避免被瞄准。
- zh-Hant: 按 Ctrl 把你的顏色換成地板的顏色，避免被瞄準。
- es: Pulsa Ctrl para igualar tu color con el suelo y que no te apunten.
- es-419: Presiona Ctrl para igualar tu color con el piso y que no te apunten.
- fr: Appuie sur Ctrl pour accorder ta couleur au sol et ne plus être ciblé.
- de: Drück Ctrl, um deine Farbe an den Boden anzupassen, damit du nicht anvisiert wirst.
- pt-BR: Aperte Ctrl pra igualar sua cor à do chão e não virar alvo.
- ru: Нажми Ctrl, чтобы подогнать свой цвет под пол и не попасть под прицел.
- pl: Wciśnij Ctrl, żeby dopasować swój kolor do podłogi i nie być na celowniku.

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

- ko: 벽 색에 맞춰 부딪히세요.
- en: Ram the walls that match your color.
- ja: 自分の色と同じ壁に体当たりしてください。
- zh-Hans: 撞向和你颜色相同的墙。
- zh-Hant: 撞向和你顏色相同的牆。
- es: Embiste las paredes de tu color.
- es-419: Embiste las paredes de tu color.
- fr: Fonce dans les murs de ta couleur.
- de: Renn gegen die Wände in deiner Farbe.
- pt-BR: Trombe nas paredes da sua cor.
- ru: Врезайся в стены своего цвета.
- pl: Taranuj ściany w swoim kolorze.

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

- ko: 자기 색 칸만 밟으세요.
칸과 캐릭터 색을 맞추세요.
- en: Only step on tiles in your color.
Match your character's color to the tiles.
- ja: 自分の色のマスだけを踏んでください。\nキャラの色をマスに合わせてください。
- zh-Hans: 只踩你颜色的格子。\n角色颜色要和格子对上。
- zh-Hant: 只踩你顏色的格子。\n角色顏色要和格子對上。
- es: Pisa solo las baldosas de tu color.\nIguala el color de tu personaje con el de las baldosas.
- es-419: Pisa solo las baldosas de tu color.\nIguala el color de tu personaje con el de las baldosas.
- fr: Ne marche que sur les cases de ta couleur.\nAccorde la couleur de ton perso à celle des cases.
- de: Tritt nur auf Felder in deiner Farbe.\nPass die Farbe deines Charakters an die Felder an.
- pt-BR: Pise só nos blocos da sua cor.\nAjuste a cor do personagem à dos blocos.
- ru: Наступай только на клетки своего цвета.\nПодгони цвет персонажа под клетки.
- pl: Stawaj tylko na polach w swoim kolorze.\nDopasuj kolor postaci do pól.

## Tip.T.Stage2.3

- ko: 담당 색이 먼저 지나가야 다른 팀원도 그 바닥을 밟을 수 있습니다.
- en: The zone's color goes first, then everyone else can follow.
- ja: そのエリアの色の人が先に通れば、ほかの人も続けます。
- zh-Hans: 该区域的颜色先走，其他人再跟上。
- zh-Hant: 該區域的顏色先走，其他人再跟上。
- es: Primero pasa el color de la zona; después pueden seguirle los demás.
- es-419: Primero pasa el color de la zona; después pueden seguirle los demás.
- fr: La couleur de la zone passe en premier, puis les autres peuvent suivre.
- de: Zuerst geht die Farbe der Zone, dann können alle anderen folgen.
- pt-BR: A cor da área vai primeiro, depois os outros podem seguir.
- ru: Сначала идёт цвет зоны, потом за ним могут пройти остальные.
- pl: Najpierw idzie kolor strefy, potem reszta może za nim.

## Tip.T.Stage3

- ko: 간판 시간 안에 구간을 통과하세요. 늦으면 위액이 차오릅니다.
양옆 벽에 색을 맞춰 부딪히면 벽이 뒤로 물러납니다.
- en: Clear each section before the sign's timer runs out, or acid will flood it.
Match the side walls' color and ram them to push them back.
- ja: 看板のタイマーが切れる前に区間を抜けてください。遅れると胃液があふれます。\n両側の壁と色を合わせて体当たりすると、壁が押し戻されます。
- zh-Hans: 在牌子的计时结束前通过这一段，否则胃液会涌上来。\n对上两侧墙壁的颜色撞过去，就能把墙推回去。
- zh-Hant: 在牌子的計時結束前通過這一段，否則胃液會湧上來。\n對上兩側牆壁的顏色撞過去，就能把牆推回去。
- es: Pasa cada tramo antes de que se acabe el tiempo del cartel o se inundará de ácido.\nIguala el color de las paredes laterales y embístelas para hacerlas retroceder.
- es-419: Pasa cada tramo antes de que se acabe el tiempo del letrero o se va a inundar de ácido.\nIguala el color de las paredes laterales y embístelas para hacerlas retroceder.
- fr: Passe chaque section avant la fin du chrono du panneau, sinon l'acide l'inonde.\nAccorde ta couleur aux murs latéraux et fonce dedans pour les repousser.
- de: Schaff jeden Abschnitt, bevor der Timer am Schild abläuft, sonst flutet ihn die Säure.\nPass deine Farbe an die Seitenwände an und ramm sie, um sie zurückzudrängen.
- pt-BR: Passe cada trecho antes do tempo da placa acabar, senão o ácido inunda tudo.\nIguale a cor das paredes laterais e trombe nelas pra empurrá-las de volta.
- ru: Пройди участок, пока не истёк таймер на табличке, иначе его зальёт кислотой.\nПодгони цвет под боковые стены и врежься в них, чтобы оттолкнуть.
- pl: Przejdź odcinek, zanim skończy się czas na tabliczce, bo zaleje go kwas.\nDopasuj kolor do bocznych ścian i taranuj je, żeby je odepchnąć.

## Tip.T.Stage4

- ko: 한 칸에 한 명만 서세요. 둘 이상 서면 칸이 가라앉습니다.
깨지는 칸이 섞여 있습니다.
앞뒤 벽과 부종에 닿으면 튕겨 나갑니다.
- en: One player per tile. Any more and it'll sink.
Some of the tiles are breakable.
Touching the front and back walls or the swellings will knock you back.
- ja: 1マスに乗れるのは1人だけです。それ以上乗ると沈みます。\n割れるマスが混ざっています。\n前後の壁や腫れに触れると弾き飛ばされます。
- zh-Hans: 一格只能站一个人，多了就会下沉。\n有些格子会碎。\n碰到前后的墙或肿块会被弹开。
- zh-Hant: 一格只能站一個人，多了就會下沉。\n有些格子會碎。\n碰到前後的牆或腫塊會被彈開。
- es: Un jugador por casilla; si hay más, se hunde.\nAlgunas casillas se rompen.\nSi tocas las paredes delantera y trasera o las hinchazones, saldrás despedido.
- es-419: Un jugador por casilla; si hay más, se hunde.\nAlgunas casillas se rompen.\nSi tocas las paredes delantera y trasera o las hinchazones, vas a salir despedido.
- fr: Un joueur par case : au-delà, elle s'enfonce.\nCertaines cases peuvent se briser.\nToucher les murs avant et arrière ou les gonflements te repousse.
- de: Ein Spieler pro Feld – mehr, und es sinkt ab.\nManche Felder können brechen.\nWenn du die vordere und hintere Wand oder die Schwellungen berührst, wirst du zurückgeschleudert.
- pt-BR: Um jogador por bloco. Se tiver mais, ele afunda.\nAlguns blocos quebram.\nEncostar nas paredes da frente e de trás ou nos inchaços te joga pra trás.
- ru: Один игрок на клетку. Больше — и она проседает.\nНекоторые клетки ломаются.\nЕсли коснёшься передней и задней стен или отёков, тебя отбросит.
- pl: Jeden gracz na pole. Więcej — i pole się zapada.\nNiektóre pola pękają.\nDotknięcie przedniej i tylnej ściany albo opuchlizny cię odrzuci.

## Tip.T.Stage5.1

> 2026-09-18 러너 재설계로 13개 로케일 전부 교체 (`TStage5RunnerRedesign.md`).
> 옛 흑백 토글 문구는 폐기. String Table 에셋에도 반영 완료(`StageTip_*.asset`).
> ko/en 외 11개는 **기계번역이므로 원어민 검수 전** — Steam AI 표기 검토 대상
> (`project_steam_ai_disclosure`).

- ko: 러너 한 명이 미로를 달리고, 나머지는 2층에서 길을 안내합니다.
패드를 밟으면 그 색 문만 열리고 나머지는 전부 닫힙니다.
- en: One runner races through the maze while the others guide from the second floor.
Stepping on a pad opens only that color's doors and closes all the rest.
- ja: ランナー1人が迷路を走り、ほかの人は2階から道を案内します。\nパネルを踏むと、その色の扉だけが開き、ほかはすべて閉まります。
- zh-Hans: 一名跑者在迷宫里跑，其他人在二楼指路。\n踩下踏板只会打开那个颜色的门，其他门全部关上。
- zh-Hant: 一名跑者在迷宮裡跑，其他人在二樓指路。\n踩下踏板只會打開那個顏色的門，其他門全部關上。
- es: Un corredor recorre el laberinto y los demás lo guían desde el segundo piso.\nAl pisar una placa se abren solo las puertas de ese color y se cierran todas las demás.
- es-419: Un corredor recorre el laberinto y los demás lo guían desde el segundo piso.\nAl pisar una placa se abren solo las puertas de ese color y se cierran todas las demás.
- fr: Un coureur traverse le labyrinthe pendant que les autres le guident depuis l'étage.\nMarcher sur une dalle ouvre seulement les portes de cette couleur et ferme toutes les autres.
- de: Ein Läufer rennt durchs Labyrinth, die anderen lotsen ihn vom oberen Stock aus.\nWer auf eine Platte tritt, öffnet nur die Türen dieser Farbe – alle anderen gehen zu.
- pt-BR: Um corredor atravessa o labirinto enquanto os outros guiam do andar de cima.\nPisar numa placa abre só as portas daquela cor e fecha todas as outras.
- pt: (pt-BR과 동일 — 기존 이 키의 관례를 따름)
- ru: Один бегун проходит лабиринт, остальные направляют его со второго этажа.\nПлита открывает только двери своего цвета и закрывает все остальные.
- pl: Jeden biegacz przemierza labirynt, a reszta prowadzi go z piętra.\nNadepnięcie płytki otwiera tylko drzwi w jej kolorze i zamyka wszystkie inne.

---

## Tip.T.Boss.1

- ko: 목표까지 도달하세요.

사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: Reach the goal.

Finish this section before the candy hits the ground.
- ja: ゴールまで到達してください。\n\nアメが地面に着く前に、この区間をクリアしてください。
- zh-Hans: 到达终点。\n\n在糖果落地前完成这一段。
- zh-Hant: 到達終點。\n\n在糖果落地前完成這一段。
- es: Llega a la meta.\n\nSupera esta fase antes de que el caramelo toque el suelo.
- es-419: Llega a la meta.\n\nSupera esta fase antes de que el dulce toque el piso.
- fr: Atteins l'arrivée.\n\nTermine cette section avant que le bonbon touche le sol.
- de: Erreiche das Ziel.\n\nSchaff diesen Abschnitt, bevor das Bonbon den Boden erreicht.
- pt-BR: Alcance o objetivo.\n\nTermine esta etapa antes que a bala chegue ao chão.
- ru: Доберись до цели.\n\nПройди этот этап, пока леденец не коснулся земли.
- pl: Dotrzyj do celu.\n\nUkończ ten etap, zanim cukierek uderzy w ziemię.

## Tip.T.Boss.2

- ko: 발판을 눌러 길을 만들고 목표까지 도달하세요.

사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: Step on the pads to make a path, then reach the goal.

Finish this section before the candy hits the ground.
- ja: 足場を踏んで道を作り、ゴールまで到達してください。\n\nアメが地面に着く前に、この区間をクリアしてください。
- zh-Hans: 踩踏板铺出道路，然后到达终点。\n\n在糖果落地前完成这一段。
- zh-Hant: 踩踏板鋪出道路，然後到達終點。\n\n在糖果落地前完成這一段。
- es: Pisa las placas para abrir camino y luego llega a la meta.\n\nSupera esta fase antes de que el caramelo toque el suelo.
- es-419: Pisa las placas para abrir camino y luego llega a la meta.\n\nSupera esta fase antes de que el dulce toque el piso.
- fr: Marche sur les dalles pour créer un chemin, puis atteins l'arrivée.\n\nTermine cette section avant que le bonbon touche le sol.
- de: Tritt auf die Platten, um einen Weg zu bauen, und erreiche dann das Ziel.\n\nSchaff diesen Abschnitt, bevor das Bonbon den Boden erreicht.
- pt-BR: Pise nas placas pra formar um caminho e depois alcance o objetivo.\n\nTermine esta etapa antes que a bala chegue ao chão.
- ru: Наступай на плиты, чтобы проложить путь, и доберись до цели.\n\nПройди этот этап, пока леденец не коснулся земли.
- pl: Stawaj na płytkach, żeby utworzyć drogę, a potem dotrzyj do celu.\n\nUkończ ten etap, zanim cukierek uderzy w ziemię.

## Tip.T.Boss.3

- ko: 안전 칸에는 한 명만 서세요. 겹치면 데미지를 입습니다.

사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: One player per safe tile. Doubling up hurts.

Finish this section before the candy hits the ground.
- ja: 安全なマスには1人ずつ乗ってください。重なるとダメージを受けます。\n\nアメが地面に着く前に、この区間をクリアしてください。
- zh-Hans: 每个安全格只能站一个人，挤在一起会受伤。\n\n在糖果落地前完成这一段。
- zh-Hant: 每個安全格只能站一個人，擠在一起會受傷。\n\n在糖果落地前完成這一段。
- es: Un jugador por casilla segura; compartirla hace daño.\n\nSupera esta fase antes de que el caramelo toque el suelo.
- es-419: Un jugador por casilla segura; compartirla hace daño.\n\nSupera esta fase antes de que el dulce toque el piso.
- fr: Un joueur par case sûre. À plusieurs, on prend des dégâts.\n\nTermine cette section avant que le bonbon touche le sol.
- de: Ein Spieler pro sicherem Feld. Zu zweit gibt's Schaden.\n\nSchaff diesen Abschnitt, bevor das Bonbon den Boden erreicht.
- pt-BR: Um jogador por bloco seguro. Dividir o bloco causa dano.\n\nTermine esta etapa antes que a bala chegue ao chão.
- ru: Один игрок на безопасную клетку. Вдвоём — получите урон.\n\nПройди этот этап, пока леденец не коснулся земли.
- pl: Jeden gracz na bezpieczne pole. Tłok oznacza obrażenia.\n\nUkończ ten etap, zanim cukierek uderzy w ziemię.

## Tip.T.Boss.4

- ko: 색을 맞춰 벽에 부딪히세요.
- en: Match the wall's color and ram it.
- ja: 壁と色を合わせて体当たりしてください。
- zh-Hans: 对上墙的颜色撞过去。
- zh-Hant: 對上牆的顏色撞過去。
- es: Iguala el color de la pared y embístela.
- es-419: Iguala el color de la pared y embístela.
- fr: Accorde ta couleur au mur et fonce dedans.
- de: Pass deine Farbe an die Wand an und ramm sie.
- pt-BR: Iguale a cor da parede e trombe nela.
- ru: Подгони цвет под стену и врежься в неё.
- pl: Dopasuj kolor do ściany i taranuj ją.

---

## 적용 체크리스트 (사용자 — 에디터)

1. String Table Collection `StageTip`(가칭) 생성, 키는 위 `Tip.*` 그대로.
2. 각 로케일 칸에 이 문서 값을 넣는다. `\n` / `\n\n` 은 TMP 실제 줄바꿈.
3. `Tip_Panel/Txt.Tip`에 `LocalizeStringEvent` — 페이즈가 바뀌면 키만 갈아끼운다 (코드 단계에서).
4. **[2026-09-17]** M.Boss도 Tip 켬 — `Tip.M.Boss.1`~`4`를 `BossFlow` PhaseManager 각 페이즈 `onPhaseEnter` → `TipUI.ShowTip`에 연결, `Tip_Panel.hideOnAllPhasesComplete` ON(Bossdown 숨김). MCP 적용 완료. T.Boss P4는 사탕 줄 없음.
5. **[2026-09-17]** `Tip.M.Stage2.1` / `Tip.M.Stage5` 끝에 방어 버프 줄 추가(구 `Tutorial.Board_Controls.Row_BuffNote` 이전). 13로케일 렌더 결과 넘침 없음 — `Tip.M.Stage5`만 ja 18 · ru 19로 축소, 나머지 20.
