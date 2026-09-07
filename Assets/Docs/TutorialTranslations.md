# TutorialTranslations — Tutorial 씬 `TutorialInfoBoards` 13개 언어 번역본

> 대상: `Tutorial.unity` → `TutorialInfoBoards` 하위 안내판 6개(`Board_Controls`, `Board_SelfCheer`, `Board_TeamCheer`, `Board_CheerName`, `Board_Test`, `Board_GotoStartZone`)의 정적 TMP 텍스트, 총 15개 필드.
> 원문 소스: 각 TMP `TextMeshProUGUI.m_text` (에디터에서 직접 확인, 이 문서 작성 시점 기준 — 사용자가 3번 문항 `Board_TeamCheer`/`Board_Test` 본문을 직접 수정한 최신 값 반영함).
> String Table Collection **`Tutorial`** + `Tutorial.unity`의 15개 TMP `LocalizeStringEvent` 연결은 MCP로 적용됨 (2026-09-07).
>
> **번역 원칙:** ①각 언어 문법에 맞게. ②단순 직역이 아니라 그 언어 화자가 게임 튜토리얼에서 실제로 쓸 법한 자연스러운 말투로 다듬음 — 예를 들어 영어는 캐주얼한 명령형, 일본어는 です/ます체, 독일어/프랑스어/러시아어/폴란드어는 비격식 2인칭(du/tu/ты/ty), 스페인어는 스페인(pulsa)과 중남미(presiona) 어휘 차이, 포르투갈은 포르투갈(carrega em)과 브라질(aperte) 어휘 차이를 반영함.
> **용어 통일:** "host"는 한국어 원문도 번역하지 않고 그대로 쓰므로, 각 언어에서 그 지역 게이머들이 실제로 쓰는 표현을 채택함 — en/de/ru/pl `host`(차용어 그대로), fr `l'hôte`, ja `ホスト`, zh `房主`, es `el host`, pt `anfitrião`, pt-BR `host`.
> **"팀 응원 단어"**는 모든 언어에서 `TeamCheer`/`Test` 섹션에 동일한 표현으로 통일(예: en `your team's cheer word`, ja `チームの合言葉`, de `das Team-Wort`).

## 키 네이밍

새 String Table Collection **`Tutorial`**. 키는 씬 하이어라키 경로를 그대로 반영:

| 키 | 씬 경로 |
|---|---|
| `Tutorial.Board_Controls.Row_Move` | `TutorialInfoBoards/Board_Controls/Face/Row_Move/Label` |
| `Tutorial.Board_Controls.Row_Push` | `.../Row_Push/Label` |
| `Tutorial.Board_Controls.Row_Color` | `.../Row_Color/Label` |
| `Tutorial.Board_Controls.Row_Color2` | `.../Row_Color2/Label` |
| `Tutorial.Board_Controls.Row_Buff` | `.../Row_Buff/Label` |
| `Tutorial.Board_SelfCheer.Title` / `.Body` | `TutorialInfoBoards/Board_SelfCheer/Face/Title`, `/Body` |
| `Tutorial.Board_TeamCheer.Title` / `.Body` | `TutorialInfoBoards/Board_TeamCheer/Face/Title`, `/Body` |
| `Tutorial.Board_CheerName.Title` / `.Body` | `TutorialInfoBoards/Board_CheerName/Face/Title`, `/Body` |
| `Tutorial.Board_Test.Title` / `.Body` | `TutorialInfoBoards/Board_Test/Face/Title`, `/Body` |
| `Tutorial.Board_GotoStartZone.Title` / `.Body` | `TutorialInfoBoards/Board_GotoStartZone/Face/Title`, `/Body` |

언어 순서(13개, `Assets/Localization/Locales/` 전체와 동일): `ko, en, ja, zh-Hans, zh-Hant, es, es-419, fr, de, pt, pt-BR, ru, pl`

---

## Board_Controls (조작 안내 — 라벨 5개, Title 없음)

### `Tutorial.Board_Controls.Row_Move`

- ko: 이동
- en: Move
- ja: 移動
- zh-Hans: 移动
- zh-Hant: 移動
- es: Movimiento
- es-419: Movimiento
- fr: Déplacement
- de: Bewegen
- pt: Mover
- pt-BR: Mover
- ru: Движение
- pl: Ruch

### `Tutorial.Board_Controls.Row_Push`

- ko: 밀치기(데미지 0)
- en: Push (0 damage)
- ja: 突き飛ばし（ダメージ0）
- zh-Hans: 推开（伤害为0）
- zh-Hant: 推開（傷害為0）
- es: Empujón (0 de daño)
- es-419: Empujón (0 de daño)
- fr: Poussée (0 dégâts)
- de: Stoßen (0 Schaden)
- pt: Empurrão (0 de dano)
- pt-BR: Empurrão (0 de dano)
- ru: Толчок (0 урона)
- pl: Pchnięcie (0 obrażeń)

### `Tutorial.Board_Controls.Row_Color`

- ko: 흑/백
- en: Black & White
- ja: 白黒
- zh-Hans: 黑白
- zh-Hant: 黑白
- es: Blanco y negro
- es-419: Blanco y negro
- fr: Noir et blanc
- de: Schwarz-Weiß
- pt: Preto e branco
- pt-BR: Preto e branco
- ru: Чёрно-белый режим
- pl: Czerń i biel

### `Tutorial.Board_Controls.Row_Color2`

- ko: 고유색
- en: Your Color
- ja: 自分の色
- zh-Hans: 专属颜色
- zh-Hant: 專屬顏色
- es: Tu color
- es-419: Tu color
- fr: Ta couleur
- de: Deine Farbe
- pt: A tua cor
- pt-BR: Sua cor
- ru: Свой цвет
- pl: Twój kolor

### `Tutorial.Board_Controls.Row_Buff`

- ko: 버프 교체
- en: Swap Buff
- ja: バフ切り替え
- zh-Hans: 切换增益
- zh-Hant: 切換增益
- es: Cambiar de mejora
- es-419: Cambiar de mejora
- fr: Changer de bonus
- de: Buff wechseln
- pt: Trocar de bónus
- pt-BR: Trocar de buff
- ru: Смена баффа
- pl: Zmiana buffa

---

## Board_SelfCheer (개인 응원)

### `Tutorial.Board_SelfCheer.Title`

- ko: 개인 응원
- en: Solo Cheer
- ja: 個人応援
- zh-Hans: 个人应援
- zh-Hant: 個人應援
- es: Ánimo individual
- es-419: Ánimo individual
- fr: Encouragement perso
- de: Solo-Anfeuerung
- pt: Incentivo a solo
- pt-BR: Torcida individual
- ru: Личная поддержка
- pl: Doping solo

### `Tutorial.Board_SelfCheer.Body`

- ko: 자신의 이름을 외치면 버프가 켜집니다.\nE를 눌러 개인 닉네임을 설정하세요.\n테스트를 꼭 해보세요!
- en: Shout your own name to activate your buff.\nPress E to set your nickname.\nMake sure to test it out!
- ja: 自分の名前を叫ぶとバフが発動します。\nEキーでニックネームを設定しましょう。\n忘れずにテストしてみてください！
- zh-Hans: 喊出自己的名字即可触发增益效果。\n按E键设置你的昵称。\n一定要试一试哦！
- zh-Hant: 喊出自己的名字就能觸發增益效果。\n按E鍵設定你的暱稱。\n記得一定要測試看看！
- es: Grita tu propio nombre para activar tu mejora.\nPulsa E para configurar tu apodo.\n¡No olvides probarlo!
- es-419: Grita tu propio nombre para activar tu mejora.\nPresiona E para configurar tu apodo.\n¡No olvides probarlo!
- fr: Crie ton propre nom pour activer ton bonus.\nAppuie sur E pour définir ton pseudo.\nPense bien à le tester !
- de: Rufe deinen eigenen Namen, um deinen Buff zu aktivieren.\nDrücke E, um deinen Spitznamen festzulegen.\nProbier's unbedingt einmal aus!
- pt: Grita o teu próprio nome para ativares o teu bónus.\nCarrega em E para definires a tua alcunha.\nNão te esqueças de testar!
- pt-BR: Grite seu próprio nome para ativar seu buff.\nAperte E para definir seu apelido.\nNão esqueça de testar!
- ru: Прокричи своё имя, чтобы активировать бафф.\nНажми E, чтобы задать никнейм.\nОбязательно проверь, как это работает!
- pl: Krzyknij swoje imię, żeby aktywować swojego buffa.\nNaciśnij E, aby ustawić swój pseudonim.\nKoniecznie to przetestuj!

---

## Board_TeamCheer (팀 응원)

### `Tutorial.Board_TeamCheer.Title`

- ko: 팀 응원
- en: Team Cheer
- ja: チーム応援
- zh-Hans: 团队应援
- zh-Hant: 團隊應援
- es: Ánimo de equipo
- es-419: Ánimo de equipo
- fr: Encouragement d'équipe
- de: Team-Anfeuerung
- pt: Incentivo de equipa
- pt-BR: Torcida em equipe
- ru: Командная поддержка
- pl: Doping drużynowy

### `Tutorial.Board_TeamCheer.Body`

- ko: 경고 아이콘이 뜨면 팀 응원 단어를 다같이 외치세요.\n입·식도의 위협이 원래대로 돌아갑니다.\n팀 응원 이름은 host만 정할 수 있습니다. 팀원과 미리 상의하세요.
- en: When the warning icon appears, shout your team's cheer word together.\nIt undoes whatever threat the mouth or esophagus just made.\nOnly the host can set the team cheer word — talk it over with your team first.
- ja: 警告アイコンが出たら、チームの合言葉をみんなで叫びましょう。\n口や食道の脅威が元の状態に戻ります。\nチームの合言葉を決められるのはホストだけです。事前にチームで話し合っておきましょう。
- zh-Hans: 警告图标出现时，全队一起喊出团队应援词。\n嘴巴或食道造成的威胁就会被解除。\n团队应援词只能由房主设置，记得提前和队友商量好。
- zh-Hant: 警告圖示出現時，全隊一起喊出團隊應援詞。\n嘴巴或食道造成的威脅就會被解除。\n團隊應援詞只有房主能設定，記得先跟隊友商量好。
- es: Cuando aparezca el icono de aviso, gritad todos juntos la palabra de equipo.\nAsí se anula la amenaza que la boca o el esófago os acaban de hacer.\nSolo el host puede fijar la palabra de equipo: poneos de acuerdo con antelación.
- es-419: Cuando aparezca el ícono de advertencia, griten todos juntos la palabra de equipo.\nAsí se anula la amenaza que la boca o el esófago les acaban de hacer.\nSolo el host puede definir la palabra de equipo: pónganse de acuerdo con anticipación.
- fr: Quand l'icône d'alerte apparaît, criez tous ensemble le mot d'équipe.\nCela annule la menace que la bouche ou l'œsophage vient de vous infliger.\nSeul l'hôte peut définir le mot d'équipe : mettez-vous d'accord à l'avance.
- de: Wenn das Warnsymbol erscheint, ruft gemeinsam das Team-Wort.\nDadurch wird die Bedrohung durch Mund oder Speiseröhre rückgängig gemacht.\nNur der Host kann das Team-Wort festlegen — sprecht euch vorher im Team ab.
- pt: Quando aparecer o ícone de aviso, gritem todos juntos a palavra de equipa.\nIsso anula a ameaça que a boca ou o esófago acabaram de fazer.\nSó o anfitrião pode definir a palavra de equipa — combinem isso antes com a equipa.
- pt-BR: Quando o ícone de aviso aparecer, gritem juntos a palavra da equipe.\nIsso desfaz a ameaça que a boca ou o esôfago acabaram de fazer.\nSó o host pode definir a palavra da equipe — combinem isso com antecedência.
- ru: Когда появится значок предупреждения, прокричите командное слово все вместе.\nЭто отменяет угрозу, которую только что создали рот или пищевод.\nКомандное слово может задать только хост — договоритесь заранее всей командой.
- pl: Gdy pojawi się ikona ostrzeżenia, krzyknijcie razem drużynowe hasło.\nTo cofa zagrożenie, które właśnie stworzyły usta albo przełyk.\nHasło drużynowe może ustawić tylko host — ustalcie je wcześniej w drużynie.

---

## Board_CheerName (응원 이름 설정)

### `Tutorial.Board_CheerName.Title`

- ko: 응원 이름
- en: Cheer Name
- ja: 応援ネーム
- zh-Hans: 应援名
- zh-Hant: 應援名
- es: Nombre de ánimo
- es-419: Nombre de ánimo
- fr: Nom d'encouragement
- de: Anfeuerungsname
- pt: Nome de incentivo
- pt-BR: Nome de torcida
- ru: Имя для поддержки
- pl: Imię dopingowe

### `Tutorial.Board_CheerName.Body`

- ko: 응원 이름은 영어로만 적을 수 있어요.\n실제로 쓰는 단어일수록 인식률이 좋아요!\n마이크가 없거나 인식이 안될 경우 ESC 메뉴에서 숫자키 응원을 켜보세요.\n(1은 개인 응원 2는 팀 응원 — 목소리 응원 방식을 추천합니다!)
- en: Cheer names can only be typed in English.\nReal words you'd actually say out loud work best!\nNo mic, or voice recognition not working? Turn on number-key cheering from the ESC menu.\n(1 = solo cheer, 2 = team cheer — but voice is way more fun!)
- ja: 応援ネームは英語でしか入力できません。\n実際によく使う単語ほど認識されやすいですよ！\nマイクがない、または認識されない場合はESCメニューから数字キー応援をオンにしましょう。\n（1が個人応援、2がチーム応援です。でも声で応援する方が断然楽しいですよ！）
- zh-Hans: 应援名只能用英文输入。\n越是常用的词汇，识别率就越高！\n没有麦克风或识别不了？可以在ESC菜单里打开数字键应援。\n（1是个人应援，2是团队应援——不过用语音应援会更有趣！）
- zh-Hant: 應援名只能用英文輸入。\n越常用的單字，辨識率就越高！\n沒有麥克風或辨識不出來？可以到ESC選單開啟數字鍵應援。\n（1是個人應援，2是團隊應援——不過用語音應援會更有趣喔！）
- es: El nombre de ánimo solo se puede escribir en inglés.\nCuanto más natural sea la palabra al pronunciarla, mejor se reconoce.\nSi no tienes micrófono o no te reconoce, activa el ánimo con teclas numéricas desde el menú ESC.\n(1 = ánimo individual, 2 = ánimo de equipo, ¡aunque animar con la voz es mucho más divertido!)
- es-419: El nombre de ánimo solo se puede escribir en inglés.\n¡Mientras más natural sea la palabra al decirla, mejor se reconoce!\n¿No tienes micrófono o no te reconoce? Activa el ánimo con teclas numéricas desde el menú ESC.\n(1 = ánimo individual, 2 = ánimo de equipo, ¡aunque animar con la voz es mucho más divertido!)
- fr: Le nom d'encouragement ne peut être écrit qu'en anglais.\nPlus le mot est naturel à prononcer, mieux il est reconnu !\nPas de micro, ou la reconnaissance qui bugue ? Active les encouragements au clavier numérique dans le menu Échap.\n(1 = encouragement perso, 2 = encouragement d'équipe — mais crier, c'est quand même plus fun !)
- de: Der Anfeuerungsname kann nur auf Englisch eingegeben werden.\nJe natürlicher das Wort beim Aussprechen klingt, desto besser wird es erkannt!\nKein Mikro oder die Erkennung klappt nicht? Aktiviere die Zahlentasten-Anfeuerung im ESC-Menü.\n(1 = Solo-Anfeuerung, 2 = Team-Anfeuerung — mit der Stimme macht es aber viel mehr Spaß!)
- pt: O nome de incentivo só pode ser escrito em inglês.\nQuanto mais natural for a palavra ao dizê-la, melhor é reconhecida!\nSem microfone ou o reconhecimento não funciona? Ativa o incentivo por teclas numéricas no menu ESC.\n(1 é incentivo a solo, 2 é incentivo de equipa — mas incentivar com a voz é muito mais divertido!)
- pt-BR: O nome de torcida só pode ser digitado em inglês.\nQuanto mais natural for a palavra na hora de falar, melhor o reconhecimento!\nSem microfone ou o reconhecimento não está funcionando? Ative a torcida por teclas numéricas no menu ESC.\n(1 é torcida individual, 2 é torcida em equipe — mas torcer com a voz é muito mais divertido!)
- ru: Имя для поддержки можно ввести только на английском.\nЧем естественнее звучит слово, когда его произносишь, тем лучше оно распознаётся!\nНет микрофона или распознавание не работает? Включи поддержку цифровыми клавишами в меню ESC.\n(1 — личная поддержка, 2 — командная, но кричать голосом гораздо веселее!)
- pl: Imię dopingowe można wpisać tylko po angielsku.\nIm bardziej naturalnie brzmi słowo, gdy je wypowiadasz, tym lepiej jest rozpoznawane!\nNie masz mikrofonu albo rozpoznawanie nie działa? Włącz doping klawiszami numerycznymi w menu ESC.\n(1 to doping solo, 2 to doping drużynowy — ale dopingowanie głosem jest o wiele fajniejsze!)

---

## Board_Test (팀 응원 연습)

### `Tutorial.Board_Test.Title`

- ko: 팀 응원 연습
- en: Team Cheer Practice
- ja: チーム応援の練習
- zh-Hans: 团队应援练习
- zh-Hant: 團隊應援練習
- es: Práctica de ánimo de equipo
- es-419: Práctica de ánimo de equipo
- fr: Entraînement à l'encouragement d'équipe
- de: Team-Anfeuerung üben
- pt: Treino de incentivo de equipa
- pt-BR: Treino de torcida em equipe
- ru: Тренировка командной поддержки
- pl: Trening dopingu drużynowego

### `Tutorial.Board_Test.Body`

- ko: [E]를 누르면 입·식도의 위협 경고가 뜨고 \n위에 있는 입이 닫힙니다.\n팀 응원 단어를 다같이 외치면 다시 열립니다.\n외친 사람 머리 위에 하트가 뜹니다.
- en: Press [E] to trigger a mock threat warning —\nthe mouth above will close.\nShout your team's cheer word together to open it back up.\nA heart appears over the head of whoever shouted.
- ja: [E]を押すと実際の脅威と同じ警告が表示され、\n上にある口が閉じます。\nチームの合言葉をみんなで叫べば再び開きます。\n叫んだ人の頭上にハートが表示されます。
- zh-Hans: 按[E]会像真正遇到威胁一样弹出警告，\n上方的嘴巴也会随之关闭。\n全队一起喊出团队应援词就能重新打开。\n喊出应援词的人头上会出现爱心。
- zh-Hant: 按下[E]會像遇到真正的威脅一樣跳出警告，\n上方的嘴巴也會跟著關閉。\n全隊一起喊出團隊應援詞就能重新打開。\n喊出應援詞的人頭上會出現愛心。
- es: Al pulsar [E] aparece un aviso como el de una amenaza real\ny la boca de arriba se cierra.\nGritad todos juntos la palabra de equipo para volver a abrirla.\nSobre la cabeza de quien grite aparecerá un corazón.
- es-419: Al presionar [E] aparece una advertencia como la de una amenaza real\ny la boca de arriba se cierra.\nGriten todos juntos la palabra de equipo para volver a abrirla.\nSobre la cabeza de quien grite va a aparecer un corazón.
- fr: Appuie sur [E] pour déclencher une fausse alerte de menace :\nla bouche du dessus se ferme.\nCriez tous ensemble le mot d'équipe pour la rouvrir.\nUn cœur apparaît au-dessus de la tête de celui qui a crié.
- de: Drückst du [E], erscheint eine Warnung wie bei einer echten Bedrohung,\nund der Mund oben schließt sich.\nRuft gemeinsam das Team-Wort, um ihn wieder zu öffnen.\nÜber dem Kopf der rufenden Person erscheint ein Herz.
- pt: Ao carregares em [E] aparece um aviso igual ao de uma ameaça real\ne a boca ali em cima fecha-se.\nGritem todos juntos a palavra de equipa para a voltar a abrir.\nAparece um coração por cima da cabeça de quem gritou.
- pt-BR: Ao apertar [E], aparece um aviso igual ao de uma ameaça de verdade\ne a boca lá em cima se fecha.\nGritem juntos a palavra da equipe para abri-la de novo.\nUm coração aparece acima da cabeça de quem gritou.
- ru: Нажми [E] — появится предупреждение, как от настоящей угрозы,\nи рот наверху закроется.\nПрокричите командное слово все вместе, чтобы снова его открыть.\nНад головой того, кто кричал, появится сердечко.
- pl: Naciśnięcie [E] wywołuje ostrzeżenie takie jak przy prawdziwym zagrożeniu\ni usta na górze się zamykają.\nKrzyknijcie razem drużynowe hasło, żeby je znowu otworzyć.\nNad głową osoby, która krzyknęła, pojawia się serduszko.

---

## Board_GotoStartZone (시작 존 안내)

### `Tutorial.Board_GotoStartZone.Title`

- ko: 게임 시작하러 가세요
- en: Time to Start the Game
- ja: そろそろゲームを始めましょう
- zh-Hans: 该去开始游戏啦
- zh-Hant: 該去開始遊戲囉
- es: Hora de empezar la partida
- es-419: Hora de empezar la partida
- fr: C'est parti pour la partie
- de: Auf geht's ins Spiel
- pt: Vamos começar o jogo
- pt-BR: Hora de começar o jogo
- ru: Пора начинать игру
- pl: Czas zacząć grę

### `Tutorial.Board_GotoStartZone.Body`

- ko: 노란색 바닥에 시작 존이 있습니다.\n팀 전원이 모이면 카운트다운 후 게임이 시작됩니다.\n카운트다운이 끝나면 그 순간의 응원 이름으로 확정되니, \n미리 다 정했는지 확인하세요.
- en: The start zone is on the yellow floor.\nOnce the whole team gathers there, a countdown begins and the game starts.\nYour cheer name locks in the moment the countdown ends,\nso make sure everyone's done choosing.
- ja: 黄色い床にスタートゾーンがあります。\nチーム全員が集まるとカウントダウン後にゲームが始まります。\nカウントダウンが終わった瞬間の応援ネームで確定するので、\n事前に決め終えているか確認しておきましょう。
- zh-Hans: 黄色地板上就是出发区。\n全队集合后会倒计时，倒计时结束游戏就会开始。\n倒计时一结束，应援名就会立刻定下来，\n记得提前和大家商量好。
- zh-Hant: 黃色地板上就是起始區。\n全隊集合後會倒數計時，倒數結束遊戲就會開始。\n倒數一結束，應援名就會立刻定案，\n記得先跟大家都討論好。
- es: La zona de inicio está en el suelo amarillo.\nCuando todo el equipo se reúna allí, empezará una cuenta atrás y comenzará la partida.\nVuestro nombre de ánimo queda fijado en el instante en que termine la cuenta atrás,\nasí que aseguraos de tenerlo decidido antes.
- es-419: La zona de inicio está en el piso amarillo.\nCuando todo el equipo se reúna ahí, empieza una cuenta regresiva y arranca la partida.\nSu nombre de ánimo queda definido apenas termine la cuenta regresiva,\nasí que asegúrense de tenerlo decidido antes.
- fr: La zone de départ se trouve sur le sol jaune.\nDès que toute l'équipe s'y retrouve, un compte à rebours démarre puis la partie commence.\nTon nom d'encouragement est figé au moment où le compte à rebours se termine,\nalors assurez-vous d'avoir tout choisi avant.
- de: Die Startzone liegt auf dem gelben Boden.\nSobald sich das ganze Team dort versammelt, läuft ein Countdown, und das Spiel beginnt.\nEuer Anfeuerungsname wird genau in dem Moment festgelegt, in dem der Countdown endet,\nalso stellt vorher sicher, dass alle fertig gewählt haben.
- pt: A zona de início fica no chão amarelo.\nQuando toda a equipa se juntar ali, começa uma contagem decrescente e o jogo arranca.\nO vosso nome de incentivo fica fixado no instante em que a contagem termina,\npor isso confirmem que já está tudo decidido antes disso.
- pt-BR: A zona de início fica no chão amarelo.\nQuando todo o time se reunir lá, começa uma contagem regressiva e o jogo começa.\nO nome de torcida de vocês é definido no exato momento em que a contagem termina,\nentão confiram se já decidiram tudo antes disso.
- ru: Стартовая зона находится на жёлтом полу.\nКак только вся команда соберётся там, начнётся отсчёт, и игра запустится.\nИмя для поддержки фиксируется в тот момент, когда закончится отсчёт,\nтак что убедитесь, что все уже всё выбрали.
- pl: Strefa startowa znajduje się na żółtej podłodze.\nGdy zbierze się tam cała drużyna, ruszy odliczanie, a potem zacznie się gra.\nWasze imię dopingowe zostaje ustalone dokładnie w momencie zakończenia odliczania,\nwięc upewnijcie się wcześniej, że wszyscy już wybrali.

---

## 적용 상태

1. [x] String Table Collection `Tutorial` 생성 (13개 로케일, `Assets/Localization/StringTables/Tutorial*.asset`)
2. [x] 15개 키 + 번역 채움
3. [x] `TutorialInfoBoards` 하위 15개 TMP에 `LocalizeStringEvent` 부착 (`OnUpdateString` → `TMP_Text.text`)
4. [ ] Play 모드에서 Locale 몇 개 바꿔가며 6개 보드가 바뀌는지 스모크 테스트 (사용자)

## 참고

- `Board_TeamCheer`/`Board_Test` 본문의 "입·식도의 위협" 표현은 사용자가 기존 "함정이 원상복구됩니다" 문구를 교체한 최신본을 반영함.
- 이 문서는 기존 `Assets/Docs/OXQuizTranslations.md`(OX퀴즈 번역, 별도 작업 완료되어 삭제됨)와 별개의 String Table Collection(`Tutorial` vs `OXQuiz`)을 사용함.
