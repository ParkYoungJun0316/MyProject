# StageTipTranslations — Tip HUD 11개 언어

> SSOT 원문: [`StageTipLines.md`](StageTipLines.md) (한국어 확정본).
> `en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl` (`SteamworksIntegrationDesign.md` 트랙4 §10, ko 제외).
>
> String Table / 씬은 에이전트가 쓰지 않는다. 사람이 Localization Tables에 넣는다.
> 한 키 = Tip TMP 한 칸. `\n` = 줄바꿈, `\n\n` = T.Boss 사탕 줄 앞 빈 줄.
> 표시 시 비어 있지 않은 줄 앞에 `•` 불릿. 빈 줄에는 점 없음. `Tools/Setup StageTip Localization`이 테이블에 같이 넣는다.

> **2026-09-24 전면 재번역:** ko·en 확정 후 **영어 기준**으로 10개 언어 재작성(대사 개정 작업과 함께). `Tip.M.Stage4.1`·`Tip.T.Stage2.1`·`Tip.M.Boss.4`는 영어가 안 바뀌어 기존 번역 유지. String Table에도 반영 완료(`pt`는 `pt-BR`과 동일).
>
> **2026-09-24 진짜 최종본:** 위 10개 언어 문장을 다시 교체해 String Table `StageTip_*`에 반영(`pt`는 `pt-BR`과 동일). ko·en도 팀 구호 / team cheer로 맞춤. 팀 구호 용어는 대사와 같이 ja チームの掛け声 / es·es-419 grito de equipo / fr cri d'équipe / de Teamruf / pt-BR grito da equipe / ru командный клич / pl okrzyk drużyny.

## 번역 규칙

1. 그 언어 문법에 맞출 것.
2. 한국어를 직역하지 말 것. 그 언어권 HUD·코옵에서 실제로 쓰는 말로.
3. 소리 내어 읽히게. Tip은 합니다체 원문 → 각 언어의 **짧은 지시문**(en 명령형, ja です/ます, de/fr/ru/pl 비격식 2인칭, es 스페인/중남미 어휘 분리, pt-BR).

**용어:** 팁 HUD에서 외치는 말은 대사와 같다 — ja `チームの掛け声`, zh `团队口令`/`團隊口令`, es·es-419 `grito de equipo`, fr `cri d'équipe`, de `Teamruf`, pt-BR `grito da equipe`, ru `командный клич`, pl `okrzyk drużyny`. ko `팀 구호`, en `team cheer`.

**고정:** `Ctrl` / `Space` / `"TEAMCHEER"` 는 모든 언어에서 그대로. 키 강조 태그는 아직 안 씀.

**용어 (Dialogue / Tutorial과 맞춤):**

| ko | en | 비고 |
|---|---|---|
| 팀 구호 | team cheer / チームの合言葉 / 团队关键词 / palabra de equipo / mot d'équipe / Team-Wort / palavra da equipe / командное слово / hasło drużyny | HUD 표기는 `"TEAMCHEER"`. Tutorial 설정 UI와 같은 말 (2026-09-17, 구 `팀 응원 이름`) |
| 고유색 | unique color / 固有色 / 专属色 | |
| 흑백 | black and white | |
| 조준 | lock-on | |
| 버프 | buff | 게이머 차용어 유지 (zh만 增益) |
| 부종 | swellings | 알레르기 붓기. 의학 용어 남발 금지 |
| 적혈구 | red blood cells | 세계관 단어 유지 |
| 사탕 | candy / キャンディー / 糖果 / caramelo / bonbon / Bonbon / doce / конфета / cukierek | |

---

## Tip.M.Stage1

- ko: 한 번에 한 색의 입만 올라옵니다.\n흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.\n상단에 "TEAMCHEER" 경고가 뜨면 팀 구호를 외치세요.
- en: Only one color's mouth rises at a time.
Anyone can use black and white pads by matching their color.
When "TEAMCHEER" pops up at the top, shout the team cheer.
- ja: 一度に1色の口だけがせり上がります。\n自分の色を合わせれば、誰でも黒と白のパッドを使えます。\n上に「TEAMCHEER」が出たら、チームの掛け声を叫んでください。
- zh-Hans: 每次只会升起一种颜色的嘴巴。\n只要将自己的颜色与黑色或白色的踏板匹配，任何人都可以使用。\n当顶部出现“TEAMCHEER”时，大声喊出你的团队口令。
- zh-Hant: 每次只會升起一種顏色的嘴巴。\n只要將自己的顏色與黑色或白色的踏板匹配，任何人都可以使用。\n當頂部出現「TEAMCHEER」時，大聲喊出你的團隊口令。
- es: Solo se eleva la boca de un color a la vez.\nCualquiera puede usar las plataformas negras y blancas haciendo coincidir su color.\nCuando aparezca «TEAMCHEER» en la parte superior, grita el grito de equipo.
- es-419: Solo se eleva la boca de un color a la vez.\nCualquiera puede usar las plataformas negras y blancas al hacer coincidir su color.\nCuando aparezca «TEAMCHEER» en la parte superior, grita el grito de equipo.
- fr: Une seule bouche d'une couleur se soulève à la fois.\nN'importe qui peut utiliser les plateformes noires et blanches en faisant correspondre sa couleur.\nLorsque « TEAMCHEER » apparaît en haut de l'écran, criez le cri d'équipe.
- de: Immer nur der Mund einer Farbe fährt nach oben.\nJeder kann die schwarzen und weißen Flächen benutzen, indem er seine Farbe anpasst.\nWenn oben „TEAMCHEER“ erscheint, ruft laut euren Teamruf.
- pt-BR: Apenas a boca de uma cor sobe por vez.\nQualquer jogador pode usar as placas pretas e brancas ao combinar sua cor.\nQuando “TEAMCHEER” aparecer no topo, grite o grito da equipe.
- ru: За раз поднимается только рот одного цвета.\nЛюбой игрок может использовать чёрные и белые платформы, если совпадёт с ними по цвету.\nКогда вверху появится «TEAMCHEER», выкрикните командный клич.
- pl: Naraz unoszą się usta tylko jednego koloru.\nKażdy może korzystać z czarnych i białych platform, dopasowując do nich swój kolor.\nGdy u góry pojawi się „TEAMCHEER”, wykrzyczcie okrzyk drużyny.

## Tip.M.Stage2.1

- ko: 간판 아래 꿀떡 수만큼 그 구역에 들어가세요.
비대칭 정보를 각자 가지고 있습니다. 서로 공유하세요.
상단에 "TEAMCHEER" 경고가 뜨면 팀 구호를 외치세요.
방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: Each zone needs as many players as there are KKUL-TTEOK under its sign — no more, no less.
Everyone sees something different. Share what you see!
When "TEAMCHEER" pops up at the top, shout the team cheer.
Defense buff also blocks round-fail damage.
- ja: 各ゾーンには、看板の下にあるKKUL-TTEOKの数と同じ人数を入れる必要があります。多すぎても少なすぎてもダメです。\n全員が違うものを見ます。見えたものを共有しましょう！\n上に「TEAMCHEER」が出たら、チームの掛け声を叫んでください。\n防御バフはラウンド失敗時のダメージも防ぎます。
- zh-Hans: 每个区域的人数必须与标志下方的KKUL-TTEOK数量完全一致，不能多，也不能少。\n每个人看到的东西都不一样。分享你看到的内容！\n当顶部出现“TEAMCHEER”时，大声喊出你的团队口令。\n防御增益还可以抵挡本回合失败时受到的伤害。
- zh-Hant: 每個區域的人數必須與標誌下方的KKUL-TTEOK數量完全一致，不能多，也不能少。\n每個人看到的東西都不一樣。分享你看到的內容！\n當頂部出現「TEAMCHEER」時，大聲喊出你的團隊口令。\n防禦增益也可以抵擋本回合失敗時受到的傷害。
- es: Cada zona necesita exactamente tantos jugadores como KKUL-TTEOK haya bajo su señal. Ni más ni menos.\nCada jugador ve algo diferente. ¡Comparte lo que ves!\nCuando aparezca «TEAMCHEER» en la parte superior, grita el grito de equipo.\nEl potenciador de defensa también bloquea el daño por fallar la ronda.
- es-419: Cada zona necesita exactamente tantos jugadores como KKUL-TTEOK haya debajo de su señal. Ni uno más ni uno menos.\nCada jugador ve algo diferente. ¡Comparte lo que ves!\nCuando aparezca «TEAMCHEER» en la parte superior, grita el grito de equipo.\nEl potenciador de defensa también bloquea el daño por fallar la ronda.
- fr: Chaque zone doit contenir exactement autant de joueurs qu'il y a de KKUL-TTEOK sous son panneau. Ni plus ni moins.\nTout le monde voit quelque chose de différent. Partagez ce que vous voyez !\nLorsque « TEAMCHEER » apparaît en haut de l'écran, criez le cri d'équipe.\nLe bonus de défense bloque également les dégâts causés par l'échec de la manche.
- de: In jeder Zone müssen genau so viele Spieler stehen, wie KKUL-TTEOK unter dem Schild angezeigt werden. Nicht mehr und nicht weniger.\nJeder sieht etwas anderes. Teilt, was ihr seht!\nWenn oben „TEAMCHEER“ erscheint, ruft laut euren Teamruf.\nDer Verteidigungs-Buff schützt auch vor Schaden beim Scheitern der Runde.
- pt-BR: Cada zona precisa de exatamente a mesma quantidade de jogadores que houver de KKUL-TTEOK abaixo da placa. Nem mais, nem menos.\nCada jogador vê algo diferente. Compartilhe o que você vê!\nQuando “TEAMCHEER” aparecer no topo, grite o grito da equipe.\nO bônus de defesa também bloqueia o dano causado por falhar na rodada.
- ru: В каждой зоне должно быть ровно столько игроков, сколько KKUL-TTEOK находится под её табличкой. Ни больше ни меньше.\nКаждый видит что-то своё. Делитесь тем, что видите!\nКогда вверху появится «TEAMCHEER», выкрикните командный клич.\nЗащитный бафф также блокирует урон при провале раунда.
- pl: W każdej strefie musi być dokładnie tylu graczy, ile KKUL-TTEOK znajduje się pod jej znakiem. Ani więcej, ani mniej.\nKażdy widzi coś innego. Podzielcie się tym, co widzicie!\nGdy u góry pojawi się „TEAMCHEER”, wykrzyczcie okrzyk drużyny.\nWzmocnienie obrony chroni również przed obrażeniami za nieudane ukończenie rundy.

## Tip.M.Stage2.2

- ko: Ctrl로 흑/백 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
- en: Press Ctrl to match your color to the black or white floor and avoid being targeted.
- ja: Ctrlを押して自分の色を黒または白の床に合わせ、狙われるのを避けましょう。
- zh-Hans: 按下Ctrl，让自己的颜色与黑色或白色地板匹配，避免成为攻击目标。
- zh-Hant: 按下Ctrl，讓自己的顏色與黑色或白色地板匹配，避免成為攻擊目標。
- es: Pulsa Ctrl para hacer coincidir tu color con el suelo negro o blanco y evitar que te conviertan en objetivo.
- es-419: Presiona Ctrl para hacer coincidir tu color con el piso negro o blanco y evitar ser el objetivo.
- fr: Appuyez sur Ctrl pour faire correspondre votre couleur au sol noir ou blanc et éviter d'être pris pour cible.
- de: Drücke Ctrl, um deine Farbe an den schwarzen oder weißen Boden anzupassen und nicht ins Visier genommen zu werden.
- pt-BR: Pressione Ctrl para combinar sua cor com o piso preto ou branco e evitar ser o alvo.
- ru: Нажмите Ctrl, чтобы подобрать цвет под чёрный или белый пол и избежать атаки.
- pl: Naciśnij Ctrl, aby dopasować swój kolor do czarnej lub białej podłogi i uniknąć namierzenia.

## Tip.M.Stage3

- ko: 타일을 2초 동안 밟아야 점수가 올라갑니다.
고유색 타일은 해당 색만, 흑백 타일은 누구나 점수를 올릴 수 있습니다.
- en: Stand on a tile for 2 seconds to score.
Colored tiles only count for that color. Black and white tiles count for anyone.
- ja: タイルの上に2秒間立つとスコアを獲得します。\n色付きタイルは、その色のプレイヤーだけがカウントされます。黒と白のタイルは誰でもカウントされます。
- zh-Hans: 在一个方块上站满2秒即可得分。\n彩色方块只有对应颜色的玩家可以计分。黑色和白色方块任何人都可以计分。
- zh-Hant: 在一個方塊上站滿2秒即可得分。\n彩色方塊只有對應顏色的玩家可以計分。黑色和白色方塊任何人都可以計分。
- es: Ponte sobre una baldosa durante 2 segundos para conseguir puntos.\nLas baldosas de colores solo cuentan para los jugadores de ese color. Las baldosas negras y blancas cuentan para cualquiera.
- es-419: Quédate sobre una casilla durante 2 segundos para ganar puntos.\nLas casillas de colores solo cuentan para los jugadores de ese color. Las casillas negras y blancas cuentan para cualquiera.
- fr: Restez sur une dalle pendant 2 secondes pour marquer des points.\nLes dalles colorées ne comptent que pour les joueurs de cette couleur. Les dalles noires et blanches comptent pour tout le monde.
- de: Bleibe 2 Sekunden auf einem Feld stehen, um Punkte zu erhalten.\nFarbige Felder zählen nur für Spieler dieser Farbe. Schwarze und weiße Felder zählen für alle.
- pt-BR: Fique em uma plataforma por 2 segundos para marcar pontos.\nAs plataformas coloridas só contam para jogadores daquela cor. As plataformas pretas e brancas contam para qualquer jogador.
- ru: Стойте на плитке 2 секунды, чтобы заработать очки.\nЦветные плитки засчитываются только для игроков соответствующего цвета. Чёрные и белые плитки подходят всем.
- pl: Stań na kafelku przez 2 sekundy, aby zdobyć punkty.\nKolorowe kafelki liczą się tylko dla graczy w danym kolorze. Czarne i białe kafelki liczą się dla każdego.

## Tip.M.Stage4.1

- ko: 자기 색이 뜨면 Space를 누르세요.\n흰색은 아무나 눌러도 되고, 검은색은 1초 뒤 자동으로 넘어갑니다.\n미니게임 중에는 Space 버프를 쓸 수 없습니다.
- en: Press Space when your color lights up.\nAnyone can hit white. Black skips to the next turn after 1 second.\nYou can't use your Space buff during this minigame.
- ja: 自分の色が光ったらSpaceを押しましょう。\n白は誰でも叩けます。黒は1秒後に次のターンへ進みます。\nこのミニゲーム中はSpaceバフを使用できません。
- zh-Hans: 当你的颜色亮起时按下Space。\n白色任何人都可以击打。黑色在1秒后会自动进入下一回合。\n在这个小游戏中无法使用Space增益。
- zh-Hant: 當你的顏色亮起時按下Space。\n白色任何人都可以擊打。黑色在1秒後會自動進入下一回合。\n在這個小遊戲中無法使用Space增益。
- es: Pulsa Space cuando se ilumine tu color.\nCualquiera puede golpear la blanca. La negra pasa al siguiente turno después de 1 segundo.\nNo puedes usar tu potenciador de Space durante este minijuego.
- es-419: Presiona Space cuando se ilumine tu color.\nCualquiera puede golpear la casilla blanca. La negra pasa al siguiente turno después de 1 segundo.\nNo puedes usar tu potenciador de Space durante este minijuego.
- fr: Appuyez sur Space lorsque votre couleur s'allume.\nTout le monde peut frapper la case blanche. La noire passe au tour suivant après 1 seconde.\nVous ne pouvez pas utiliser votre bonus de Space pendant ce mini-jeu.
- de: Drücke Space, wenn deine Farbe aufleuchtet.\nAuf Weiß kann jeder schlagen. Schwarz wechselt nach 1 Sekunde zum nächsten Zug.\nWährend dieses Minispiels kannst du deinen Space-Buff nicht benutzen.
- pt-BR: Pressione Space quando a sua cor acender.\nQualquer jogador pode bater na branca. A preta passa para o próximo turno após 1 segundo.\nVocê não pode usar seu bônus de Space durante este minijogo.
- ru: Нажмите Space, когда загорится ваш цвет.\nБелую может нажать кто угодно. Чёрная через 1 секунду переходит к следующему ходу.\nВо время мини-игры бафф на Space не работает.
- pl: Naciśnij Space, gdy zaświeci się twój kolor.\nBiały może nacisnąć każdy. Czarny po 1 sekundzie sam przechodzi dalej.\nPodczas tej minigry nie możesz używać wzmocnienia Space.

## Tip.M.Stage4.2

- ko: 한 칸 앞의 바닥만 보여 줍니다.\n누를 칸을 미리 외워 두세요.
- en: You only see one tile ahead.
Memorize which tile to press.
- ja: 1つ先のタイルしか見えません。\nどのタイルを押すか覚えておきましょう。
- zh-Hans: 你只能看到前方一个方块。\n记住要按下哪个方块。
- zh-Hant: 你只能看到前方一個方塊。\n記住要按下哪個方塊。
- es: Solo puedes ver la baldosa que tienes justo delante.\nMemoriza qué baldosa debes pulsar.
- es-419: Solo puedes ver la casilla que está justo adelante.\nMemoriza qué casilla debes presionar.
- fr: Vous ne voyez qu'une seule case devant vous.\nMémorisez la case sur laquelle appuyer.
- de: Du siehst nur ein Feld vor dir.\nMerke dir, auf welches Feld du drücken musst.
- pt-BR: Você só consegue ver a plataforma logo à frente.\nMemorize qual plataforma deve pressionar.
- ru: Вы видите только одну плитку впереди.\nЗапомните, на какую плитку нужно нажать.
- pl: Widzisz tylko jeden kafelek przed sobą.\nZapamiętaj, który kafelek nacisnąć.

## Tip.M.Stage4.3

- ko: Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.\n"TEAMCHEER" 경고가 뜰 때 팀 구호를 외치면 바닥이 복구됩니다.\n이미 부서진 뒤에는 다음 경고까지 버티세요.
- en: Press Ctrl to match your color to the floor and avoid being targeted.
Shout the team cheer while "TEAMCHEER" is up to repair the floor.
If it's already broken, hold out until the next warning.
- ja: Ctrlを押して自分の色を床に合わせ、狙われるのを避けましょう。\n「TEAMCHEER」が出ている間にチームの掛け声を叫ぶと、床が直ります。\nすでに壊れている場合は、次の警告が出るまで耐え抜きましょう。
- zh-Hans: 按下Ctrl，让自己的颜色与地板匹配，避免成为攻击目标。\n“TEAMCHEER”出现时，喊出你的团队口令来修复地板。\n如果地板已经损坏，就坚持到下一次警告出现。
- zh-Hant: 按下Ctrl，讓自己的顏色與地板匹配，避免成為攻擊目標。\n「TEAMCHEER」出現時，喊出你的團隊口令來修復地板。\n如果地板已經損壞，就撐到下一次警告出現。
- es: Pulsa Ctrl para hacer coincidir tu color con el suelo y evitar que te conviertan en objetivo.\nCuando aparezca «TEAMCHEER», grita el grito de equipo para reparar el suelo.\nSi ya está roto, aguanta hasta la siguiente advertencia.
- es-419: Presiona Ctrl para hacer coincidir tu color con el piso y evitar ser el objetivo.\nCuando aparezca «TEAMCHEER», grita el grito de equipo para reparar el piso.\nSi ya está roto, resiste hasta la siguiente advertencia.
- fr: Appuyez sur Ctrl pour faire correspondre votre couleur au sol et éviter d'être pris pour cible.\nLorsque « TEAMCHEER » est affiché, criez le cri d'équipe pour réparer le sol.\nS'il est déjà cassé, tenez bon jusqu'au prochain avertissement.
- de: Drücke Ctrl, um deine Farbe an den Boden anzupassen und nicht ins Visier genommen zu werden.\nWenn „TEAMCHEER“ erscheint, rufe deinen Teamruf, um den Boden zu reparieren.\nWenn er bereits kaputt ist, halte bis zur nächsten Warnung durch.
- pt-BR: Pressione Ctrl para combinar sua cor com o piso e evitar ser o alvo.\nQuando “TEAMCHEER” aparecer, grite o grito da equipe para reparar o piso.\nSe ele já estiver quebrado, aguente até o próximo aviso.
- ru: Нажмите Ctrl, чтобы подобрать цвет под цвет пола и избежать атаки.\nКогда появляется «TEAMCHEER», выкрикните командный клич, чтобы восстановить пол.\nЕсли пол уже сломан, продержитесь до следующего предупреждения.
- pl: Naciśnij Ctrl, aby dopasować swój kolor do podłogi i uniknąć namierzenia.\nGdy pojawi się „TEAMCHEER”, wykrzycz okrzyk drużyny, aby naprawić podłogę.\nJeśli podłoga jest już zniszczona, wytrzymaj do następnego ostrzeżenia.

## Tip.M.Stage5

- ko: 고유색 칸이 나오면 그 칸 위에 서야 합니다.\n고유색이 없으면 흑백 칸 위에서 버티세요.\n바닥 색에 맞춰 캐릭터 색도 바꾸세요.\n방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: If a tile in your color appears, stand on it.
If not, hold out on a black or white tile.
Match your character's color to the floor, too.
Defense buff also blocks round-fail damage.
- ja: 自分の色のタイルが出現したら、その上に立ちましょう。\nなければ、黒または白のタイルで耐え抜きましょう。\nキャラクターの色も床の色に合わせましょう。\n防御バフはラウンド失敗時のダメージも防ぎます。
- zh-Hans: 如果出现与你颜色相同的方块，就站上去。\n如果没有，就站在黑色或白色方块上坚持下去。\n角色的颜色也要与地板匹配。\n防御增益还可以抵挡本回合失败时受到的伤害。
- zh-Hant: 如果出現與你顏色相同的方塊，就站上去。\n如果沒有，就站在黑色或白色方塊上撐下去。\n角色的顏色也要與地板匹配。\n防禦增益也可以抵擋本回合失敗時受到的傷害。
- es: Si aparece una baldosa de tu color, ponte sobre ella.\nSi no, aguanta sobre una baldosa negra o blanca.\nHaz coincidir también el color de tu personaje con el del suelo.\nEl potenciador de defensa también bloquea el daño por fallar la ronda.
- es-419: Si aparece una casilla de tu color, párate sobre ella.\nSi no, resiste sobre una casilla negra o blanca.\nHaz coincidir también el color de tu personaje con el del piso.\nEl potenciador de defensa también bloquea el daño por fallar la ronda.
- fr: Si une dalle de votre couleur apparaît, placez-vous dessus.\nSinon, tenez bon sur une dalle noire ou blanche.\nFaites également correspondre la couleur de votre personnage à celle du sol.\nLe bonus de défense bloque également les dégâts causés par l'échec de la manche.
- de: Wenn ein Feld in deiner Farbe erscheint, stelle dich darauf.\nWenn nicht, halte dich auf einem schwarzen oder weißen Feld.\nPasse auch die Farbe deiner Figur an die Farbe des Bodens an.\nDer Verteidigungs-Buff schützt auch vor Schaden beim Scheitern der Runde.
- pt-BR: Se aparecer uma plataforma da sua cor, fique sobre ela.\nCaso contrário, aguente em uma plataforma preta ou branca.\nCombine também a cor do seu personagem com a cor do piso.\nO bônus de defesa também bloqueia o dano causado por falhar na rodada.
- ru: Если появилась плитка вашего цвета, встаньте на неё.\nЕсли её нет, держитесь на чёрной или белой плитке.\nЦвет персонажа тоже должен совпадать с цветом пола.\nЗащитный бафф также блокирует урон при провале раунда.
- pl: Jeśli pojawi się kafelek w twoim kolorze, stań na nim.\nJeśli go nie ma, wytrzymaj na czarnym lub białym kafelku.\nDopasuj również kolor swojej postaci do koloru podłogi.\nWzmocnienie obrony chroni również przed obrażeniami za nieudane ukończenie rundy.

## Tip.M.Boss.1

> M.Boss P1 (Barrier + 화살 + 침). `Tip.M.Stage1` 앞 두 줄 — TEAMCHEER 경고 줄은 뺌.

- ko: 한 번에 한 색의 입만 올라옵니다.\n흑백 발판은 누구나 색을 맞춰 밟을 수 있습니다.
- en: Only one color's mouth rises at a time.
Anyone can use black and white pads by matching their color.
- ja: 一度に1色の口だけがせり上がります。\n自分の色を合わせれば、誰でも黒と白のパッドを使えます。
- zh-Hans: 每次只会升起一种颜色的嘴巴。\n只要匹配自己的颜色，任何人都可以使用黑色和白色踏板。
- zh-Hant: 每次只會升起一種顏色的嘴巴。\n只要匹配自己的顏色，任何人都可以使用黑色和白色踏板。
- es: Solo se eleva la boca de un color a la vez.\nCualquiera puede usar las plataformas negras y blancas haciendo coincidir su color.
- es-419: Solo se eleva la boca de un color a la vez.\nCualquiera puede usar las plataformas negras y blancas al hacer coincidir su color.
- fr: Une seule bouche d'une couleur se soulève à la fois.\nN'importe qui peut utiliser les plateformes noires et blanches en faisant correspondre sa couleur.
- de: Immer nur der Mund einer Farbe fährt nach oben.\nJeder kann die schwarzen und weißen Flächen benutzen, indem er seine Farbe anpasst.
- pt-BR: Apenas a boca de uma cor sobe por vez.\nQualquer jogador pode usar as placas pretas e brancas ao combinar sua cor.
- ru: За раз поднимается только рот одного цвета.\nЛюбой игрок может использовать чёрные и белые платформы, если совпадёт с ними по цвету.
- pl: Naraz unoszą się usta tylko jednego koloru.\nKażdy może korzystać z czarnych i białych platform, dopasowując do nich swój kolor.

## Tip.M.Boss.2

> M.Boss P2 (SideSplit 판정 + 입 닫힘).

- ko: 비대칭 정보를 각자 가지고 있습니다. 서로 공유하세요.
방어 버프는 라운드 실패 데미지도 막아 줍니다.
- en: Everyone sees something different. Share what you see!
Defense buff also blocks round-fail damage.
- ja: 全員が違うものを見ます。見えたものを共有しましょう！\n防御バフはラウンド失敗時のダメージも防ぎます。
- zh-Hans: 每个人看到的东西都不一样。分享你看到的内容！\n防御增益还可以抵挡本回合失败时受到的伤害。
- zh-Hant: 每個人看到的東西都不一樣。分享你看到的內容！\n防禦增益也可以抵擋本回合失敗時受到的傷害。
- es: Cada jugador ve algo diferente. ¡Comparte lo que ves!\nEl potenciador de defensa también bloquea el daño por fallar la ronda.
- es-419: Cada jugador ve algo diferente. ¡Comparte lo que ves!\nEl potenciador de defensa también bloquea el daño por fallar la ronda.
- fr: Tout le monde voit quelque chose de différent. Partagez ce que vous voyez !\nLe bonus de défense bloque également les dégâts causés par l'échec de la manche.
- de: Jeder sieht etwas anderes. Teilt, was ihr seht!\nDer Verteidigungs-Buff schützt auch vor Schaden beim Scheitern der Runde.
- pt-BR: Cada jogador vê algo diferente. Compartilhe o que você vê!\nO bônus de defesa também bloqueia o dano causado por falhar na rodada.
- ru: Каждый видит что-то своё. Делитесь тем, что видите!\nЗащитный бафф также блокирует урон при провале раунда.
- pl: Każdy widzi coś innego. Podzielcie się tym, co widzicie!\nWzmocnienie obrony chroni również przed obrażeniami za nieudane ukończenie rundy.

## Tip.M.Boss.3

> M.Boss P3 (Drop + 화살 + 혀). `Tip.M.Stage4.3` 첫 줄.

- ko: Ctrl로 바닥 색과 캐릭터 색을 맞춰 조준을 피하세요.
- en: Press Ctrl to match your color to the floor and avoid being targeted.
- ja: Ctrlを押して自分の色を床に合わせ、狙われるのを避けましょう。
- zh-Hans: 按下Ctrl，让自己的颜色与地板匹配，避免成为攻击目标。
- zh-Hant: 按下Ctrl，讓自己的顏色與地板匹配，避免成為攻擊目標。
- es: Pulsa Ctrl para hacer coincidir tu color con el suelo y evitar que te conviertan en objetivo.
- es-419: Presiona Ctrl para hacer coincidir tu color con el piso y evitar ser el objetivo.
- fr: Appuyez sur Ctrl pour faire correspondre votre couleur au sol et éviter d'être pris pour cible.
- de: Drücke Ctrl, um deine Farbe an den Boden anzupassen und nicht ins Visier genommen zu werden.
- pt-BR: Pressione Ctrl para combinar sua cor com o piso e evitar ser o alvo.
- ru: Нажмите Ctrl, чтобы подобрать цвет под цвет пола и избежать атаки.
- pl: Naciśnij Ctrl, aby dopasować swój kolor do podłogi i uniknąć namierzenia.

## Tip.M.Boss.4

> M.Boss P4 (`MouthBossJawSmash` — 닫힘 → 바닥 파괴 → 열린 뒤 응원으로 복구).

- ko: 입이 열리면 팀 구호를 외쳐 부서진 바닥을 복구하세요.
- en: When the mouth opens, shout the team cheer to repair the broken floor.
- ja: 口が開いたら、チームの掛け声を叫んで壊れた床を直してください。
- zh-Hans: 嘴巴张开时，大声喊出你的团队口令来修复损坏的地板。
- zh-Hant: 嘴巴張開時，大聲喊出你的團隊口令來修復損壞的地板。
- es: Cuando la boca se abra, grita el grito de equipo para reparar el suelo roto.
- es-419: Cuando la boca se abra, grita el grito de equipo para reparar el piso roto.
- fr: Lorsque la bouche s'ouvre, criez le cri d'équipe pour réparer le sol cassé.
- de: Wenn sich der Mund öffnet, rufe deinen Teamruf, um den kaputten Boden zu reparieren.
- pt-BR: Quando a boca abrir, grite o grito da equipe para reparar o piso quebrado.
- ru: Когда рот открывается, выкрикните командный клич, чтобы восстановить сломанный пол.
- pl: Gdy usta się otworzą, wykrzycz okrzyk drużyny, aby naprawić zniszczoną podłogę.

---

## Tip.T.Stage1

- ko: 벽 색에 맞춰 부딪히세요.
- en: Ram the walls that match your color.
- ja: 自分の色と同じ壁に突進しましょう。
- zh-Hans: 撞向与你颜色相同的墙。
- zh-Hant: 撞向與你顏色相同的牆。
- es: Embiste las paredes que coincidan con tu color.
- es-419: Embiste las paredes que coincidan con tu color.
- fr: Foncez dans les murs correspondant à votre couleur.
- de: Ramme die Wände, die zu deiner Farbe passen.
- pt-BR: Invista contra as paredes que combinarem com a sua cor.
- ru: Тараньте стены, которые соответствуют вашему цвету.
- pl: Szarżuj w ściany pasujące do twojego koloru.

## Tip.T.Stage2.1

- ko: 길을 외워 두세요.
- en: Memorize the path.
- ja: 道順を覚えましょう。
- zh-Hans: 记住路线。
- zh-Hant: 記住路線。
- es: Memoriza el camino.
- es-419: Memoriza el camino.
- fr: Mémorisez le chemin.
- de: Merke dir den Weg.
- pt-BR: Memorize o caminho.
- ru: Запомните путь.
- pl: Zapamiętaj drogę.

## Tip.T.Stage2.2

- ko: 자기 색 칸만 밟으세요.
칸과 캐릭터 색을 맞추세요.
- en: Only step on tiles in your color.
Match your character's color to the tiles.
- ja: 自分の色のタイルだけを踏みましょう。\nキャラクターの色をタイルの色に合わせましょう。
- zh-Hans: 只能踩与你颜色相同的方块。\n让角色的颜色与方块匹配。
- zh-Hant: 只能踩與你顏色相同的方塊。\n讓角色的顏色與方塊匹配。
- es: Pisa solo las baldosas de tu color.\nHaz coincidir el color de tu personaje con el de las baldosas.
- es-419: Pisa solo las casillas de tu color.\nHaz coincidir el color de tu personaje con el de las casillas.
- fr: Marchez uniquement sur les dalles de votre couleur.\nFaites correspondre la couleur de votre personnage à celle des dalles.
- de: Betritt nur Felder deiner Farbe.\nPasse die Farbe deiner Figur an die Farbe der Felder an.
- pt-BR: Pise apenas nas plataformas da sua cor.\nCombine a cor do seu personagem com a cor das plataformas.
- ru: Наступайте только на плитки своего цвета.\nЦвет персонажа должен совпадать с цветом плиток.
- pl: Stąpaj tylko po kafelkach w swoim kolorze.\nDopasuj kolor swojej postaci do koloru kafelków.

## Tip.T.Stage2.3

- ko: 담당 색이 먼저 지나가야 다른 팀원도 그 바닥을 밟을 수 있습니다.
- en: The zone's color goes first, then everyone else can follow.
- ja: ゾーンと同じ色のプレイヤーが先に進み、その後みんなが続きます。
- zh-Hans: 与区域颜色相同的玩家先通过，然后其他人跟上。
- zh-Hant: 與區域顏色相同的玩家先通過，然後其他人跟上。
- es: El jugador del color de la zona va primero y los demás pueden seguirlo.
- es-419: El jugador del color de la zona va primero y los demás pueden seguirlo.
- fr: Le joueur de la couleur de la zone passe en premier, puis les autres peuvent suivre.
- de: Der Spieler in der Farbe der Zone geht zuerst, danach können alle anderen folgen.
- pt-BR: O jogador da cor da zona passa primeiro, e os demais podem seguir.
- ru: Сначала проходит игрок цвета зоны, затем остальные могут идти следом.
- pl: Gracz w kolorze strefy przechodzi pierwszy, a reszta może ruszyć za nim.

## Tip.T.Stage3

- ko: 간판 시간 안에 구간을 통과하세요. 늦으면 위액이 차오릅니다.
양옆 벽에 색을 맞춰 부딪히면 벽이 뒤로 물러납니다.
- en: Clear each section before the sign's timer runs out, or stomach acid will flood it.
Match the side walls' color and ram them to push them back.
- ja: 看板のタイマーが切れる前に各エリアをクリアしましょう。間に合わないと胃液が流れ込みます。\n両側の壁の色を合わせて突進し、壁を押し戻しましょう。
- zh-Hans: 在标志上的计时器结束前清除每个区域，否则胃酸会灌满该区域。\n匹配两侧墙壁的颜色并撞击它们，把墙推回去。
- zh-Hant: 在標誌上的計時器結束前清除每個區域，否則胃酸會灌滿該區域。\n匹配兩側牆壁的顏色並撞擊它們，把牆推回去。
- es: Limpia cada sección antes de que se acabe el temporizador de la señal o el ácido inundará la zona.\nHaz coincidir el color de las paredes laterales y embístelas para hacerlas retroceder.
- es-419: Limpia cada sección antes de que se acabe el temporizador de la señal o el ácido inundará la zona.\nHaz coincidir el color de las paredes laterales y embístelas para hacerlas retroceder.
- fr: Terminez chaque section avant la fin du minuteur du panneau, sinon la zone sera inondée d'acide.\nFaites correspondre la couleur des murs latéraux et foncez dedans pour les repousser.
- de: Schließe jeden Abschnitt ab, bevor der Timer des Schilds abläuft, sonst wird er mit Säure überflutet.\nPasse die Farbe der Seitenwände an und ramme sie, um sie zurückzudrücken.
- pt-BR: Conclua cada seção antes que o cronômetro da placa termine, ou ela será inundada por ácido.\nCombine a cor das paredes laterais e invista contra elas para empurrá-las de volta.
- ru: Очистите каждый участок до того, как закончится таймер на табличке, иначе его затопит кислотой.\nПодберите цвет боковых стен и тараном оттолкните их назад.
- pl: Ukończ każdą sekcję, zanim skończy się czas na znaku, inaczej zaleje ją kwas.\nDopasuj kolor bocznych ścian i szarżuj w nie, aby je odepchnąć.

## Tip.T.Stage4

- ko: 한 칸에 한 명만 서세요. 둘 이상 서면 칸이 가라앉습니다.
깨지는 칸이 섞여 있습니다.
앞뒤 벽과 부종에 닿으면 튕겨 나갑니다.
- en: One player per tile. Any more and it'll sink.
Some of the tiles are breakable.
Touching the front and back walls or the swellings will knock you back.
- ja: 1枚のタイルにつき1人までです。2人以上乗ると沈みます。\n壊れるタイルもあります。\n前後の壁や腫れた部分に触れると、後ろに弾き飛ばされます。
- zh-Hans: 每个方块只能站一名玩家。人数超过就会下沉。\n有些方块可以被破坏。\n碰到前后墙或肿起的部位会被弹开。
- zh-Hant: 每個方塊只能站一名玩家。人數超過就會下沉。\n有些方塊可以被破壞。\n碰到前後牆或腫起的部位會被彈開。
- es: Solo puede haber un jugador por baldosa. Si hay más, se hundirá.\nAlgunas baldosas pueden romperse.\nTocar las paredes delantera y trasera o las protuberancias te hará retroceder.
- es-419: Solo puede haber un jugador por casilla. Si hay más, se hundirá.\nAlgunas casillas pueden romperse.\nTocar las paredes del frente y de atrás o las protuberancias hará que retrocedas.
- fr: Un seul joueur par dalle. S'il y en a plus, elle s'enfonce.\nCertaines dalles peuvent se casser.\nToucher les murs avant ou arrière ou les renflements vous fera reculer.
- de: Pro Feld darf nur ein Spieler stehen. Bei mehreren Spielern sinkt es ein.\nEinige Felder können zerbrechen.\nWenn du die vordere oder hintere Wand oder die Wölbungen berührst, wirst du zurückgestoßen.
- pt-BR: Apenas um jogador por plataforma. Se houver mais, ela afundará.\nAlgumas plataformas podem quebrar.\nTocar nas paredes da frente, de trás ou nas saliências fará você recuar.
- ru: На каждой плитке может стоять только один игрок. Если игроков больше, плитка провалится.\nНекоторые плитки могут ломаться.\nПрикосновение к передней или задней стене либо к выпуклостям отбросит вас назад.
- pl: Na każdym kafelku może stać tylko jeden gracz. Jeśli będzie ich więcej, kafelek się zapadnie.\nNiektóre kafelki można zniszczyć.\nDotknięcie przedniej lub tylnej ściany albo wybrzuszeń odrzuci cię do tyłu.

## Tip.T.Stage5.1

> 2026-09-18 러너 재설계로 13개 로케일 전부 교체 (`TStage5RunnerRedesign.md`).
> 옛 흑백 토글 문구는 폐기. String Table 에셋에도 반영 완료(`StageTip_*.asset`).
> ko/en 외 10개 언어는 **2026-09-24 최종본**으로 교체됨 (`StageTip_*` 반영).

- ko: 러너 한 명이 미로를 달리고, 나머지는 2층에서 길을 안내합니다.
패드를 밟으면 그 색 문만 열리고 나머지는 전부 닫힙니다.
- en: One runner races through the maze while the others guide from the second floor.
Stepping on a pad opens only that color's doors and closes all the rest.
- ja: 1人が迷路を駆け抜け、他のプレイヤーは2階から道を案内します。\nパッドを踏むと、その色のドアだけが開き、それ以外のドアはすべて閉まります。
- zh-Hans: 一名玩家穿过迷宫，其他玩家从二楼负责指引路线。\n踩下踏板后，只有对应颜色的门会打开，其余所有门都会关闭。
- zh-Hant: 一名玩家穿過迷宮，其他玩家從二樓負責指引路線。\n踩下踏板後，只有對應顏色的門會打開，其餘所有門都會關閉。
- es: Un jugador atraviesa el laberinto mientras los demás le guían desde el segundo piso.\nAl pisar una plataforma, solo se abren las puertas de ese color y todas las demás se cierran.
- es-419: Un jugador atraviesa el laberinto mientras los demás lo guían desde el segundo piso.\nAl pisar una plataforma, solo se abren las puertas de ese color y todas las demás se cierran.
- fr: Un joueur traverse le labyrinthe pendant que les autres le guident depuis le deuxième étage.\nMarcher sur une plateforme n'ouvre que les portes de cette couleur et ferme toutes les autres.
- de: Ein Spieler läuft durch das Labyrinth, während die anderen ihn vom zweiten Stock aus führen.\nWenn du auf eine Fläche trittst, öffnen sich nur die Türen dieser Farbe und alle anderen schließen sich.
- pt-BR: Um jogador atravessa o labirinto enquanto os outros o guiam do segundo andar.\nAo pisar em uma placa, apenas as portas daquela cor se abrem e todas as outras se fecham.
- pt: (pt-BR과 동일 — 기존 이 키의 관례를 따름)
- ru: Один игрок проходит лабиринт, а остальные направляют его со второго этажа.\nНаступив на платформу, вы открываете только двери соответствующего цвета, а все остальные закрываются.
- pl: Jeden gracz biegnie przez labirynt, a pozostali kierują nim z drugiego piętra.\nNadepnięcie na platformę otwiera tylko drzwi w tym samym kolorze i zamyka wszystkie pozostałe.

---

## Tip.T.Boss.1

- ko: 목표까지 도달하세요.

사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: Reach the goal.

Finish this section before the candy hits the ground.
- ja: ゴールを目指しましょう。\n\nキャンディが地面に落ちる前に、このエリアをクリアしましょう。
- zh-Hans: 到达终点。\n\n在糖果落地前完成这一部分。
- zh-Hant: 到達終點。\n\n在糖果落地前完成這一部分。
- es: Llega a la meta.\n\nCompleta esta sección antes de que el caramelo toque el suelo.
- es-419: Llega a la meta.\n\nCompleta esta sección antes de que el dulce toque el suelo.
- fr: Atteignez l'arrivée.\n\nTerminez cette section avant que le bonbon ne touche le sol.
- de: Erreiche das Ziel.\n\nSchließe diesen Abschnitt ab, bevor das Bonbon den Boden berührt.
- pt-BR: Chegue ao objetivo.\n\nConclua esta seção antes que o doce toque o chão.
- ru: Доберитесь до цели.\n\nЗавершите этот участок до того, как конфета упадёт на землю.
- pl: Dotrzyj do celu.\n\nUkończ tę sekcję, zanim cukierek spadnie na ziemię.

## Tip.T.Boss.2

- ko: 발판을 눌러 길을 만들고 목표까지 도달하세요.

사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: Step on the pads to make a path, then reach the goal.

Finish this section before the candy hits the ground.
- ja: パッドを踏んで道を作り、ゴールを目指しましょう。\n\nキャンディが地面に落ちる前に、このエリアをクリアしましょう。
- zh-Hans: 踩下踏板制造道路，然后到达终点。\n\n在糖果落地前完成这一部分。
- zh-Hant: 踩下踏板製造道路，然後到達終點。\n\n在糖果落地前完成這一部分。
- es: Pisa las plataformas para crear un camino y después llega a la meta.\n\nCompleta esta sección antes de que el caramelo toque el suelo.
- es-419: Pisa las plataformas para crear un camino y después llega a la meta.\n\nCompleta esta sección antes de que el dulce toque el suelo.
- fr: Marchez sur les plateformes pour créer un chemin, puis atteignez l'arrivée.\n\nTerminez cette section avant que le bonbon ne touche le sol.
- de: Betritt die Flächen, um einen Weg zu bilden, und erreiche dann das Ziel.\n\nSchließe diesen Abschnitt ab, bevor das Bonbon den Boden berührt.
- pt-BR: Pise nas placas para criar um caminho e depois chegue ao objetivo.\n\nConclua esta seção antes que o doce toque o chão.
- ru: Наступайте на платформы, чтобы создать путь, затем доберитесь до цели.\n\nЗавершите этот участок до того, как конфета упадёт на землю.
- pl: Stawaj na platformach, aby utworzyć drogę, a następnie dotrzyj do celu.\n\nUkończ tę sekcję, zanim cukierek spadnie na ziemię.

## Tip.T.Boss.3

- ko: 안전 칸에는 한 명만 서세요. 겹치면 데미지를 입습니다.

사탕이 땅에 닿기 전에 이 구간을 끝내세요.
- en: One player per safe tile. Doubling up hurts.

Finish this section before the candy hits the ground.
- ja: 安全なタイル1枚につき1人までです。2人で乗るとダメージを受けます。\n\nキャンディが地面に落ちる前に、このエリアをクリアしましょう。
- zh-Hans: 每个安全方块只能站一名玩家。两人同时站上去会受到伤害。\n\n在糖果落地前完成这一部分。
- zh-Hant: 每個安全方塊只能站一名玩家。兩人同時站上去會受到傷害。\n\n在糖果落地前完成這一部分。
- es: Solo puede haber un jugador por baldosa segura. Si se juntan dos, recibirán daño.\n\nCompleta esta sección antes de que el caramelo toque el suelo.
- es-419: Solo puede haber un jugador por casilla segura. Si dos se juntan, recibirán daño.\n\nCompleta esta sección antes de que el dulce toque el suelo.
- fr: Un seul joueur par dalle sûre. Si deux joueurs se retrouvent dessus, ils subissent des dégâts.\n\nTerminez cette section avant que le bonbon ne touche le sol.
- de: Pro sicherem Feld darf nur ein Spieler stehen. Wenn zwei darauf stehen, erleiden sie Schaden.\n\nSchließe diesen Abschnitt ab, bevor das Bonbon den Boden berührt.
- pt-BR: Apenas um jogador por plataforma segura. Se dois ficarem juntos, eles sofrerão dano.\n\nConclua esta seção antes que o doce toque o chão.
- ru: На каждой безопасной плитке может стоять только один игрок. Если встанут двое, они получат урон.\n\nЗавершите этот участок до того, как конфета упадёт на землю.
- pl: Na każdym bezpiecznym kafelku może stać tylko jeden gracz. Jeśli staną na nim dwie osoby, otrzymają obrażenia.\n\nUkończ tę sekcję, zanim cukierek spadnie na ziemię.

## Tip.T.Boss.4

- ko: 색을 맞춰 벽에 부딪히세요.
- en: Match the wall's color and ram it.
- ja: 壁の色を合わせて突進しましょう。
- zh-Hans: 匹配墙壁的颜色并撞击它。
- zh-Hant: 匹配牆壁的顏色並撞擊它。
- es: Haz coincidir tu color con el de la pared y embístela.
- es-419: Haz coincidir tu color con el de la pared y embístela.
- fr: Faites correspondre votre couleur à celle du mur et foncez dedans.
- de: Passe deine Farbe an die Wandfarbe an und ramme sie.
- pt-BR: Combine sua cor com a da parede e invista contra ela.
- ru: Подберите цвет под цвет стены и тараном ударьте по ней.
- pl: Dopasuj swój kolor do koloru ściany i szarżuj w nią.

---

## 적용 체크리스트 (사용자 — 에디터)

1. String Table Collection `StageTip`(가칭) 생성, 키는 위 `Tip.*` 그대로.
2. 각 로케일 칸에 이 문서 값을 넣는다. `\n` / `\n\n` 은 TMP 실제 줄바꿈.
3. `Tip_Panel/Txt.Tip`에 `LocalizeStringEvent` — 페이즈가 바뀌면 키만 갈아끼운다 (코드 단계에서).
4. **[2026-09-17]** M.Boss도 Tip 켬 — `Tip.M.Boss.1`~`4`를 `BossFlow` PhaseManager 각 페이즈 `onPhaseEnter` → `TipUI.ShowTip`에 연결, `Tip_Panel.hideOnAllPhasesComplete` ON(Bossdown 숨김). MCP 적용 완료. T.Boss P4는 사탕 줄 없음.
5. **[2026-09-17]** `Tip.M.Stage2.1` / `Tip.M.Stage5` 끝에 방어 버프 줄 추가(구 `Tutorial.Board_Controls.Row_BuffNote` 이전). 13로케일 렌더 결과 넘침 없음 — `Tip.M.Stage5`만 ja 18 · ru 19로 축소, 나머지 20.
