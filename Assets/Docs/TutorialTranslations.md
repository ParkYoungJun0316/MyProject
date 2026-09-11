# TutorialTranslations — Tutorial 씬 `TutorialInfoBoards` + `CheerNamePanel` 13개 언어 번역본

> 대상: `Tutorial.unity` → ①`TutorialInfoBoards` 하위 안내판 6개(`Board_Controls`, `Board_SelfCheer`, `Board_TeamCheer`, `Board_CheerName`, `Board_Test`, `Board_GotoStartZone`)의 정적 TMP 텍스트 15개 필드, ②`CheerNamePanel`(응원 이름/팀 키워드 입력 패널) 정적 텍스트 + 동적 피드백 문구 19개 필드. 총 34개 필드, 같은 String Table Collection **`Tutorial`** 하나로 통합 관리(2026-09-07, 사용자 결정 — 별도 테이블 안 만듦). `Interlude.unity` 전용 `Board_NameChange`(2필드)와 Tutorial·Interlude 공용 `Prompt.*`(E-키 안내 2필드)도 같은 테이블에 추가됨(2026-09-08).
> 원문 소스: 각 TMP `TextMeshProUGUI.m_text` / `TutorialCheerNameUI.cs`의 하드코딩 한국어 폴백 문자열 (에디터에서 직접 확인, 이 문서 작성 시점 기준).
> `TutorialInfoBoards` 15개 필드의 String Table Collection **`Tutorial`** + `LocalizeStringEvent` 연결은 MCP로 적용됨 (2026-09-07). `CheerNamePanel` 19개 키도 같은 `Tutorial` 테이블에 입력하고, 정적 텍스트 8곳(`LocalizeStringEvent`) + `TutorialCheerNameUI` `LocalizedString` 12필드를 MCP로 연결함 (2026-09-07).
>
> **CheerNamePanel 관련 결정 (2026-09-07):**
> - CheerName/TeamCheerWord 형식: 영문 **소문자만**(a-z) 허용 — 숫자·밑줄(_) 제외. Vosk 음성 인식이 숫자/기호를 발음으로 인식 못 해 실제 응원 매칭이 안 되는 문제 실측 확인.
> - 금칙어에 `sex` 추가(기존 목록에 성적 단어 카테고리는 있었으나 이 단어 자체가 누락돼 있었음).
> - 화면 표시(개인 이름, 팀 키워드)는 항상 **대문자** — 저장/매칭용 내부 값은 그대로 소문자 유지, 표시 시점에만 변환(PlayerHPUI.selfNameLabel과 동일 패턴).
> - 실패 피드백 문구는 카테고리별로 세분화하지 않고 지금처럼 4종(형식/예약어/금칙어/중복) + 팀워드용 `not_server`로 뭉뚱그림 유지 — 어뷰징 유저에게 어떤 금칙어 카테고리에 걸렸는지 정확히 알려주면 우회가 쉬워지므로 의도적으로 모호하게 둠.
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
| `Tutorial.Board_Controls.Row_BuffNote` | `.../Row_Buff/Note` (신규 TMP — Q 행 바로 아래) |
| `Tutorial.Board_Controls.Row_Voice` | `.../Row_Voice/Label` (신규 행 — 아이콘 `Assets/Figma/Tutorial/Speek.png`) |
| `Tutorial.Board_SelfCheer.Title` / `.Body` | `TutorialInfoBoards/Board_SelfCheer/Face/Title`, `/Body` |
| `Tutorial.Board_TeamCheer.Title` / `.Body` | `TutorialInfoBoards/Board_TeamCheer/Face/Title`, `/Body` |
| `Tutorial.Board_CheerName.Title` / `.Body` | `TutorialInfoBoards/Board_CheerName/Face/Title`, `/Body` |
| `Tutorial.Board_Test.Title` / `.Body` | `TutorialInfoBoards/Board_Test/Face/Title`, `/Body` |
| `Tutorial.Board_GotoStartZone.Title` / `.Body` | `TutorialInfoBoards/Board_GotoStartZone/Face/Title`, `/Body` |

`CheerNamePanel` 키는 씬 경로 대신 UI 요소별 역할명 사용 (패널이 `TutorialInfoBoards`처럼 Face 구조가 아니라 평면 UI라서):

| 키 | 연결 대상 | 방식 |
|---|---|---|
| `Tutorial.CheerNamePanel.Title` | `CheerNamePanel/TitleText` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.Examples` | `CheerNamePanel/ExamplesText` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.NameInputPlaceholder` | `CheerNamePanel/NameInputField/Text Area/Placeholder` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.TeamWordInputPlaceholder` | `.../HostTeamWordSection/TeamWordInputField/Text Area/Placeholder` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.ConfirmButton` | `ConfirmButton/Text (TMP)` 및 `TeamWordConfirmButton/Text (TMP)` (동일 키 공용) | `LocalizeStringEvent` ×2 |
| `Tutorial.CheerNamePanel.CloseButton` | `CloseButton/Text (TMP)` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.HostHint` | `.../HostTeamWordSection/HostHintText` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.TeamKeywordPrefix` | `TutorialCheerNameUI.teamKeywordPrefix` (코드, `{0}` 포맷) | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Format` | `TutorialCheerNameUI.feedbackFormat` (CheerName/TeamWord 공용) | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Reserved_Name` | `TutorialCheerNameUI.feedbackReservedName` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Reserved_Team` | `TutorialCheerNameUI.feedbackReservedTeam` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Blocked` | `TutorialCheerNameUI.feedbackBlocked` (CheerName/TeamWord 공용) | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Taken_Name` | `TutorialCheerNameUI.feedbackTakenName` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Taken_Team` | `TutorialCheerNameUI.feedbackTakenTeam` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Generic_Name` | `TutorialCheerNameUI.feedbackGenericName` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Generic_Team` | `TutorialCheerNameUI.feedbackGenericTeam` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_NotServer` | `TutorialCheerNameUI.feedbackNotServer` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Submitting` | `TutorialCheerNameUI.feedbackSubmitting` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Timeout` | `TutorialCheerNameUI.feedbackTimeout` | `LocalizedString` 필드 |

언어 순서(13개, `Assets/Localization/Locales/` 전체와 동일): `ko, en, ja, zh-Hans, zh-Hant, es, es-419, fr, de, pt, pt-BR, ru, pl`

---

## Board_Controls (조작 안내)

> **2026-09-11 개편 (카피 확정):** 라벨 5개 유지 + Q 행 아래 Note 1줄 + Speek 행 1개. 화살표는 TMP에서 `↔`. Q 아이콘은 버프 교체 행에 유지. 음성 행은 키보드 아이콘 쓰지 않고 `Assets/Figma/Tutorial/Speek.png`.

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

- ko: 밀치기 (데미지 없음)
- en: Push (no damage)
- ja: 突き飛ばし（ダメージなし）
- zh-Hans: 推开（无伤害）
- zh-Hant: 推開（無傷害）
- es: Empujón (sin daño)
- es-419: Empujón (sin daño)
- fr: Poussée (sans dégâts)
- de: Stoßen (kein Schaden)
- pt: Empurrão (sem dano)
- pt-BR: Empurrão (sem dano)
- ru: Толчок (без урона)
- pl: Pchnięcie (bez obrażeń)

### `Tutorial.Board_Controls.Row_Color`

- ko: 색 변경 흑/백
- en: Color change B/W
- ja: 色変更 白黒
- zh-Hans: 颜色切换 黑白
- zh-Hant: 顏色切換 黑白
- es: Cambiar color b/n
- es-419: Cambiar color b/n
- fr: Couleur N/B
- de: Farbe S/W
- pt: Mudar cor P/B
- pt-BR: Mudar cor P/B
- ru: Смена цвета ч/б
- pl: Zmiana koloru c/b

### `Tutorial.Board_Controls.Row_Color2`

- ko: 색 변경 고유색
- en: Color change Unique
- ja: 色変更 自分の色
- zh-Hans: 颜色切换 专属色
- zh-Hant: 顏色切換 專屬色
- es: Cambiar color propio
- es-419: Cambiar color propio
- fr: Couleur perso
- de: Farbe Eigenfarbe
- pt: Mudar cor própria
- pt-BR: Mudar cor própria
- ru: Смена цвета свой
- pl: Zmiana koloru własny

### `Tutorial.Board_Controls.Row_Buff`

- ko: 버프 교체(방어↔이속)
- en: Swap buff (Guard ↔ Speed)
- ja: バフ切替（防御↔速度）
- zh-Hans: 切换增益（防御↔加速）
- zh-Hant: 切換增益（防禦↔加速）
- es: Cambiar mejora (defensa ↔ velocidad)
- es-419: Cambiar mejora (defensa ↔ velocidad)
- fr: Changer de bonus (défense ↔ vitesse)
- de: Buff wechseln (Abwehr ↔ Tempo)
- pt: Trocar bónus (defesa ↔ velocidade)
- pt-BR: Trocar buff (defesa ↔ velocidade)
- ru: Смена баффа (защита ↔ скорость)
- pl: Zmiana buffa (obrona ↔ prędkość)

### `Tutorial.Board_Controls.Row_BuffNote`

- ko: 방어는 라운드 데미지도 막습니다.
- en: Guard also blocks round damage.
- ja: 防御はラウンドダメージも防ぎます。
- zh-Hans: 防御也能挡住回合伤害。
- zh-Hant: 防禦也能擋下回合傷害。
- es: La defensa también bloquea el daño de ronda.
- es-419: La defensa también bloquea el daño de ronda.
- fr: La défense bloque aussi les dégâts de round.
- de: Abwehr blockt auch Rundenschaden.
- pt: A defesa também bloqueia o dano da ronda.
- pt-BR: A defesa também bloqueia o dano da rodada.
- ru: Защита блокирует и урон за раунд.
- pl: Obrona blokuje też obrażenia rundy.

### `Tutorial.Board_Controls.Row_Voice`

- ko: 자신의 이름을 말하면 버프가 켜집니다.
- en: Say your own name to turn on your buff.
- ja: 自分の名前を言うとバフが発動します。
- zh-Hans: 喊出自己的名字就会开启增益。
- zh-Hant: 喊出自己的名字就會開啟增益。
- es: Di tu propio nombre para activar tu mejora.
- es-419: Di tu propio nombre para activar tu mejora.
- fr: Dis ton propre nom pour activer ton bonus.
- de: Sag deinen eigenen Namen, um deinen Buff zu aktivieren.
- pt: Diz o teu próprio nome para activares o teu bónus.
- pt-BR: Diga seu próprio nome para ativar seu buff.
- ru: Скажи своё имя — бафф включится.
- pl: Powiedz swoje imię, żeby włączyć buffa.

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

## CheerNamePanel (응원 이름 / 팀 키워드 입력 패널)

### `Tutorial.CheerNamePanel.Title`

- ko: 응원 이름을 정해주세요
- en: Choose your cheer name
- ja: 応援ネームを決めてください
- zh-Hans: 请设置你的应援名
- zh-Hant: 請設定你的應援名
- es: Elige tu nombre de ánimo
- es-419: Elige tu nombre de ánimo
- fr: Choisis ton nom d'encouragement
- de: Leg deinen Anfeuerungsnamen fest
- pt: Escolhe o teu nome de incentivo
- pt-BR: Escolha seu nome de torcida
- ru: Выбери своё имя для поддержки
- pl: Wybierz swoje imię dopingowe

### `Tutorial.CheerNamePanel.Examples`

- ko: 이렇게는 안 돼요\n· 욕설/금칙어 (예: fuck)\n· 예약어 (admin, host, cheer)\n· 숫자·밑줄(_)·한글·이모지·1글자\n· 2~12자, 영문 소문자만 가능\n\n확정 후에는 실제로 인식되는지 확인해보세요!
- en: These won't work\n· Profanity/blocked words (e.g. fuck)\n· Reserved words (admin, host, cheer)\n· Numbers, underscores (_), Korean, emojis, single letters\n· 2–12 characters, lowercase English letters only\n\nAfter confirming, make sure to test if it's actually recognized!
- ja: これはNGです\n・卑猥な言葉/禁止ワード（例：fuck）\n・予約語（admin、host、cheer）\n・数字・アンダーバー（_）・ハングル・絵文字・1文字\n・2〜12文字、半角英小文字のみ\n\n確定したら実際に認識されるか確認してみましょう！
- zh-Hans: 以下情况不行\n· 脏话/违禁词（例：fuck）\n· 保留字（admin、host、cheer）\n· 数字、下划线（_）、韩文、表情符号、1个字符\n· 2~12个字符，仅限英文小写字母\n\n确定后请一定测试一下能不能被正确识别！
- zh-Hant: 以下情況不行\n· 髒話/違禁詞（例：fuck）\n· 保留字（admin、host、cheer）\n· 數字、底線（_）、韓文、表情符號、1個字元\n· 2~12個字元，僅限英文小寫字母\n\n確定後請一定要測試看看能不能被正確辨識！
- es: Esto no vale\n· Palabrotas/términos prohibidos (ej: fuck)\n· Palabras reservadas (admin, host, cheer)\n· Números, guion bajo (_), coreano, emojis, una sola letra\n· 2-12 caracteres, solo letras minúsculas en inglés\n\nDespués de confirmar, ¡asegúrate de probar si se reconoce de verdad!
- es-419: Esto no funciona\n· Groserías/palabras prohibidas (ej: fuck)\n· Palabras reservadas (admin, host, cheer)\n· Números, guion bajo (_), coreano, emojis, una sola letra\n· 2 a 12 caracteres, solo letras minúsculas en inglés\n\nDespués de confirmar, ¡no olvides probar si se reconoce de verdad!
- fr: Ça, ça ne marche pas\n· Insultes/mots interdits (ex. : fuck)\n· Mots réservés (admin, host, cheer)\n· Chiffres, tiret bas (_), coréen, émojis, une seule lettre\n· 2 à 12 caractères, lettres minuscules (alphabet latin) uniquement\n\nUne fois confirmé, pense à tester si c'est bien reconnu !
- de: Das geht nicht\n· Beleidigungen/gesperrte Wörter (z. B. fuck)\n· Reservierte Wörter (admin, host, cheer)\n· Zahlen, Unterstrich (_), Koreanisch, Emojis, ein einzelner Buchstabe\n· 2–12 Zeichen, nur englische Kleinbuchstaben\n\nTeste nach dem Bestätigen unbedingt, ob es wirklich erkannt wird!
- pt: Isto não pode\n· Palavrões/palavras proibidas (ex.: fuck)\n· Palavras reservadas (admin, host, cheer)\n· Números, sublinhado (_), coreano, emojis, uma única letra\n· 2 a 12 carateres, apenas letras minúsculas em inglês\n\nDepois de confirmares, testa se é mesmo reconhecido!
- pt-BR: Isso não pode\n· Palavrões/palavras proibidas (ex.: fuck)\n· Palavras reservadas (admin, host, cheer)\n· Números, sublinhado (_), coreano, emojis, uma única letra\n· 2 a 12 caracteres, apenas letras minúsculas em inglês\n\nDepois de confirmar, teste se realmente é reconhecido!
- ru: Так нельзя\n· Ругательства/запрещённые слова (напр.: fuck)\n· Зарезервированные слова (admin, host, cheer)\n· Цифры, знак подчёркивания (_), корейские буквы, эмодзи, одна буква\n· 2–12 символов, только строчные латинские буквы\n\nПосле подтверждения обязательно проверь, распознаётся ли имя на самом деле!
- pl: Tak nie może być\n· Wulgaryzmy/zakazane słowa (np. fuck)\n· Zastrzeżone słowa (admin, host, cheer)\n· Cyfry, podkreślenie (_), koreański, emotikony, jedna litera\n· 2–12 znaków, tylko małe litery angielskiego alfabetu\n\nPo zatwierdzeniu koniecznie sprawdź, czy naprawdę jest rozpoznawane!

### `Tutorial.CheerNamePanel.NameInputPlaceholder`

- ko: 예) happy
- en: ex) happy
- ja: 例）happy
- zh-Hans: 例：happy
- zh-Hant: 例：happy
- es: ej: happy
- es-419: ej: happy
- fr: ex. : happy
- de: z. B. happy
- pt: ex.: happy
- pt-BR: ex.: happy
- ru: напр.: happy
- pl: np. happy

### `Tutorial.CheerNamePanel.TeamWordInputPlaceholder`

- ko: 예) fighting
- en: ex) fighting
- ja: 例）fighting
- zh-Hans: 例：fighting
- zh-Hant: 例：fighting
- es: ej: fighting
- es-419: ej: fighting
- fr: ex. : fighting
- de: z. B. fighting
- pt: ex.: fighting
- pt-BR: ex.: fighting
- ru: напр.: fighting
- pl: np. fighting

### `Tutorial.CheerNamePanel.ConfirmButton`

- ko: 확정
- en: Confirm
- ja: 決定
- zh-Hans: 确定
- zh-Hant: 確定
- es: Confirmar
- es-419: Confirmar
- fr: Confirmer
- de: Bestätigen
- pt: Confirmar
- pt-BR: Confirmar
- ru: Подтвердить
- pl: Potwierdź

### `Tutorial.CheerNamePanel.CloseButton`

- ko: 닫기
- en: Close
- ja: 閉じる
- zh-Hans: 关闭
- zh-Hant: 關閉
- es: Cerrar
- es-419: Cerrar
- fr: Fermer
- de: Schließen
- pt: Fechar
- pt-BR: Fechar
- ru: Закрыть
- pl: Zamknij

### `Tutorial.CheerNamePanel.HostHint`

- ko: 팀 전체가 함께 외칠 단어를 정해주세요 (기본값: FIGHTING)
- en: Set the word your whole team will shout together (default: FIGHTING)
- ja: チーム全員で叫ぶ合言葉を決めてください（初期値：FIGHTING）
- zh-Hans: 请设置全队一起喊的应援词（默认：FIGHTING）
- zh-Hant: 請設定全隊一起喊的應援詞（預設：FIGHTING）
- es: Elige la palabra que gritará todo el equipo junto (por defecto: FIGHTING)
- es-419: Elige la palabra que va a gritar todo el equipo junto (por defecto: FIGHTING)
- fr: Choisis le mot que toute l'équipe criera ensemble (par défaut : FIGHTING)
- de: Legt das Wort fest, das das ganze Team gemeinsam ruft (Standard: FIGHTING)
- pt: Define a palavra que toda a equipa vai gritar em conjunto (predefinição: FIGHTING)
- pt-BR: Defina a palavra que todo o time vai gritar junto (padrão: FIGHTING)
- ru: Задайте слово, которое вся команда будет кричать вместе (по умолчанию: FIGHTING)
- pl: Ustal słowo, które cała drużyna będzie razem krzyczeć (domyślnie: FIGHTING)

### `Tutorial.CheerNamePanel.TeamKeywordPrefix` (`{0}` 포맷 — 팀 키워드 대문자가 채워짐)

- ko: 팀 키워드: {0}
- en: Team keyword: {0}
- ja: チームの合言葉：{0}
- zh-Hans: 团队关键词：{0}
- zh-Hant: 團隊關鍵詞：{0}
- es: Palabra de equipo: {0}
- es-419: Palabra de equipo: {0}
- fr: Mot d'équipe : {0}
- de: Team-Wort: {0}
- pt: Palavra de equipa: {0}
- pt-BR: Palavra da equipe: {0}
- ru: Командное слово: {0}
- pl: Hasło drużyny: {0}

### `Tutorial.CheerNamePanel.Feedback_Format`

- ko: 2~12자, 영문 소문자만 사용할 수 있어요.
- en: Use 2–12 lowercase English letters only.
- ja: 2〜12文字の半角英小文字のみ使用できます。
- zh-Hans: 只能使用2~12个英文小写字母。
- zh-Hant: 只能使用2~12個英文小寫字母。
- es: Usa entre 2 y 12 letras minúsculas en inglés.
- es-419: Usa entre 2 y 12 letras minúsculas en inglés.
- fr: Utilise entre 2 et 12 lettres minuscules (alphabet latin) uniquement.
- de: Verwende nur 2–12 englische Kleinbuchstaben.
- pt: Usa apenas entre 2 e 12 letras minúsculas em inglês.
- pt-BR: Use apenas entre 2 e 12 letras minúsculas em inglês.
- ru: Используй от 2 до 12 строчных латинских букв.
- pl: Użyj od 2 do 12 małych liter angielskiego alfabetu.

### `Tutorial.CheerNamePanel.Feedback_Reserved_Name`

- ko: 시스템 예약어라 사용할 수 없는 이름이에요.
- en: That name is a reserved system word, so you can't use it.
- ja: システムの予約語なので、その名前は使用できません。
- zh-Hans: 这是系统保留字，无法用作名字。
- zh-Hant: 這是系統保留字，無法用作名字。
- es: Ese nombre es una palabra reservada del sistema, así que no puedes usarlo.
- es-419: Ese nombre es una palabra reservada del sistema, así que no lo puedes usar.
- fr: Ce nom est un mot réservé par le système, tu ne peux pas l'utiliser.
- de: Das ist ein reserviertes Systemwort und kann nicht als Name verwendet werden.
- pt: Esse nome é uma palavra reservada do sistema, por isso não podes usá-lo.
- pt-BR: Esse nome é uma palavra reservada do sistema, então você não pode usar.
- ru: Это имя — зарезервированное системное слово, его нельзя использовать.
- pl: To zastrzeżone słowo systemowe, nie możesz go użyć jako imienia.

### `Tutorial.CheerNamePanel.Feedback_Reserved_Team`

- ko: 시스템 예약어라 사용할 수 없는 단어예요.
- en: That word is a reserved system word, so you can't use it.
- ja: システムの予約語なので、その単語は使用できません。
- zh-Hans: 这是系统保留字，无法用作关键词。
- zh-Hant: 這是系統保留字，無法用作關鍵詞。
- es: Esa palabra es una palabra reservada del sistema, así que no puedes usarla.
- es-419: Esa palabra es una palabra reservada del sistema, así que no la puedes usar.
- fr: Ce mot est réservé par le système, tu ne peux pas l'utiliser.
- de: Das ist ein reserviertes Systemwort und kann nicht verwendet werden.
- pt: Essa palavra é uma palavra reservada do sistema, por isso não podes usá-la.
- pt-BR: Essa palavra é uma palavra reservada do sistema, então você não pode usar.
- ru: Это слово — зарезервированное системное слово, его нельзя использовать.
- pl: To zastrzeżone słowo systemowe, nie możesz go użyć.

### `Tutorial.CheerNamePanel.Feedback_Blocked`

- ko: 사용할 수 없는 단어가 포함되어 있어요.
- en: This contains a word that can't be used.
- ja: 使用できない単語が含まれています。
- zh-Hans: 包含了无法使用的词语。
- zh-Hant: 包含了無法使用的詞語。
- es: Contiene una palabra que no se puede usar.
- es-419: Contiene una palabra que no se puede usar.
- fr: Cela contient un mot qui ne peut pas être utilisé.
- de: Das enthält ein Wort, das nicht verwendet werden darf.
- pt: Contém uma palavra que não pode ser usada.
- pt-BR: Contém uma palavra que não pode ser usada.
- ru: Здесь есть слово, которое нельзя использовать.
- pl: Zawiera słowo, którego nie można użyć.

### `Tutorial.CheerNamePanel.Feedback_Taken_Name`

- ko: 이미 다른 팀원이 사용 중인 이름이에요.
- en: Another teammate is already using that name.
- ja: 他のチームメイトがすでに使っている名前です。
- zh-Hans: 已经有其他队友在使用这个名字了。
- zh-Hant: 已經有其他隊友在使用這個名字了。
- es: Otro compañero de equipo ya está usando ese nombre.
- es-419: Otro compañero de equipo ya está usando ese nombre.
- fr: Un autre coéquipier utilise déjà ce nom.
- de: Dieser Name wird bereits von einem Teammitglied verwendet.
- pt: Esse nome já está a ser usado por outro colega de equipa.
- pt-BR: Esse nome já está sendo usado por outro colega de equipe.
- ru: Это имя уже использует другой участник команды.
- pl: Ta nazwa jest już używana przez innego członka drużyny.

### `Tutorial.CheerNamePanel.Feedback_Taken_Team`

- ko: 이미 팀원이 응원 이름으로 쓰고 있어요.
- en: A teammate is already using that as their cheer name.
- ja: その単語はすでにチームメイトが応援ネームとして使っています。
- zh-Hans: 已经有队友把这个词用作应援名了。
- zh-Hant: 已經有隊友把這個詞用作應援名了。
- es: Un compañero de equipo ya lo está usando como nombre de ánimo.
- es-419: Un compañero de equipo ya lo está usando como nombre de ánimo.
- fr: Un coéquipier l'utilise déjà comme nom d'encouragement.
- de: Ein Teammitglied verwendet das bereits als Anfeuerungsname.
- pt: Um colega de equipa já está a usar isso como nome de incentivo.
- pt-BR: Um colega de equipe já está usando isso como nome de torcida.
- ru: Кто-то из команды уже использует это как имя для поддержки.
- pl: Członek drużyny używa już tego jako imienia dopingowego.

### `Tutorial.CheerNamePanel.Feedback_Generic_Name`

- ko: 이름을 확정할 수 없어요.
- en: Couldn't confirm that name.
- ja: その名前は確定できません。
- zh-Hans: 无法确定该名字。
- zh-Hant: 無法確定該名字。
- es: No se ha podido confirmar el nombre.
- es-419: No se pudo confirmar el nombre.
- fr: Impossible de confirmer ce nom.
- de: Der Name konnte nicht bestätigt werden.
- pt: Não foi possível confirmar o nome.
- pt-BR: Não foi possível confirmar o nome.
- ru: Не удалось подтвердить имя.
- pl: Nie udało się zatwierdzić nazwy.

### `Tutorial.CheerNamePanel.Feedback_Generic_Team`

- ko: 팀 키워드를 확정할 수 없어요.
- en: Couldn't confirm the team keyword.
- ja: チームの合言葉を確定できません。
- zh-Hans: 无法确定团队关键词。
- zh-Hant: 無法確定團隊關鍵詞。
- es: No se ha podido confirmar la palabra de equipo.
- es-419: No se pudo confirmar la palabra de equipo.
- fr: Impossible de confirmer le mot d'équipe.
- de: Das Team-Wort konnte nicht bestätigt werden.
- pt: Não foi possível confirmar a palavra de equipa.
- pt-BR: Não foi possível confirmar a palavra da equipe.
- ru: Не удалось подтвердить командное слово.
- pl: Nie udało się zatwierdzić hasła drużyny.

### `Tutorial.CheerNamePanel.Feedback_NotServer`

- ko: 호스트만 팀 키워드를 정할 수 있어요.
- en: Only the host can set the team keyword.
- ja: チームの合言葉を決められるのはホストだけです。
- zh-Hans: 只有房主才能设置团队关键词。
- zh-Hant: 只有房主才能設定團隊關鍵詞。
- es: Solo el host puede fijar la palabra de equipo.
- es-419: Solo el host puede definir la palabra de equipo.
- fr: Seul l'hôte peut définir le mot d'équipe.
- de: Nur der Host kann das Team-Wort festlegen.
- pt: Só o anfitrião pode definir a palavra de equipa.
- pt-BR: Só o host pode definir a palavra da equipe.
- ru: Только хост может задать командное слово.
- pl: Tylko host może ustalić hasło drużyny.

### `Tutorial.CheerNamePanel.Feedback_Submitting`

- ko: 확인 중...
- en: Checking…
- ja: 確認中…
- zh-Hans: 确认中…
- zh-Hant: 確認中…
- es: Comprobando…
- es-419: Verificando…
- fr: Vérification…
- de: Wird geprüft …
- pt: A verificar…
- pt-BR: Verificando…
- ru: Проверка…
- pl: Sprawdzanie…

### `Tutorial.CheerNamePanel.Feedback_Timeout`

- ko: 응답이 없어요. 다시 시도해 주세요.
- en: No response. Please try again.
- ja: 応答がありません。もう一度お試しください。
- zh-Hans: 没有收到响应，请再试一次。
- zh-Hant: 沒有收到回應，請再試一次。
- es: No hay respuesta. Inténtalo de nuevo.
- es-419: No hay respuesta. Inténtalo de nuevo.
- fr: Pas de réponse. Réessaie.
- de: Keine Antwort erhalten. Bitte versuch's noch einmal.
- pt: Sem resposta. Tenta novamente.
- pt-BR: Sem resposta. Tente novamente.
- ru: Нет ответа. Попробуйте ещё раз.
- pl: Brak odpowiedzi. Spróbuj ponownie.

---

## 적용 상태

### TutorialInfoBoards

1. [x] String Table Collection `Tutorial` 생성 (13개 로케일, `Assets/Localization/StringTables/Tutorial*.asset`)
2. [x] 15개 키 + 번역 채움
3. [x] `TutorialInfoBoards` 하위 15개 TMP에 `LocalizeStringEvent` 부착 (`OnUpdateString` → `TMP_Text.text`)
4. [ ] Play 모드에서 Locale 몇 개 바꿔가며 6개 보드가 바뀌는지 스모크 테스트 (사용자)

### Board_Controls 개편 (2026-09-11, MCP 적용)

1. [x] `TutorialTranslations.md` Controls 7키 한국어+13로케일 확정 (`Row_Move` 유지, Push/Color/Color2/Buff 문구 변경, `Row_BuffNote`·`Row_Voice` 신규)
2. [x] `Tutorial` String Table에 기존 4키 번역 갱신 + `Row_BuffNote`/`Row_Voice` 2키 추가 (13로케일)
3. [x] 씬 `Board_Controls/Face`: Q 행 아래 `Note` TMP → `Row_BuffNote`. 새 `Row_Voice`(아이콘 `Speek.png`) → `Row_Voice`. Q 아이콘은 `Row_Buff`에 유지.
4. [ ] Play 모드에서 Controls 보드 6행+Note 스모크 (사용자)

### CheerNamePanel

1. [x] `CheerNameValidator.cs` — 형식 a-z만 허용(숫자/밑줄 제외), 금칙어에 `sex` 추가
2. [x] `TutorialCheerNameUI.cs` — 피드백/표시 문구를 하드코딩 한국어 대신 `LocalizedString` 필드(+한국어 폴백)로 리팩터, 팀 키워드 표시 대문자화
3. [x] 번역 19키 확정 (위 §CheerNamePanel)
4. [x] String Table Collection `Tutorial`에 19개 키 + 13로케일 번역 입력 (MCP, 2026-09-07)
5. [x] 정적 텍스트 8곳(Title/Examples/두 Placeholder/두 ConfirmButton/CloseButton/HostHint)에 `LocalizeStringEvent` 부착 (`OnUpdateString` → `TMP_Text.text`)
6. [x] `TutorialCheerNameUI` 컴포넌트의 `LocalizedString` 필드 12개 연결
7. [ ] Play 모드에서 Locale 바꿔가며 패널 문구·피드백이 바뀌는지 스모크 테스트 (사용자)

## Interlude.Board_NameChange (Interlude 씬 전용 안내판)

> `Interlude.unity`의 `Board_Test` GameObject(Tutorial의 `TutorialInfoBoards/Board_Test`와 오브젝트 이름만 같고 내용은 다름 — CheerName/TeamCheerWord 2차 변경 안내). 같은 `Tutorial` String Table Collection을 재사용하되, **키는 반드시 `Tutorial.Board_Test.*`와 구분**할 것 — 이름이 같다고 그 키를 재사용하면 Interlude 안내판이 Tutorial의 "팀 응원 연습" 문구로 덮어써진다(2026-09-08 실제 발견·수정: Interlude `Board_Test`가 씬 복제 과정에서 `Tutorial.Board_Test.*` 키를 그대로 물고 있었음).

### `Interlude.Board_NameChange.Title`

- ko: 이름 바꾸기
- en: Change Your Name
- ja: 名前の変更
- zh-Hans: 改名
- zh-Hant: 改名
- es: Cambiar nombre
- es-419: Cambiar nombre
- fr: Changer de nom
- de: Namen ändern
- pt: Mudar de nome
- pt-BR: Mudar de nome
- ru: Смена имени
- pl: Zmiana imienia

### `Interlude.Board_NameChange.Body`

- ko: 개인 이름과 팀 키워드, 마지막으로 한 번 더 바꿀 수 있어요.\n여기서 놓치면 게임이 끝날 때까지 못 바꾸니,\n원하시는 분은 발판에서 지금 다시 정하세요.
- en: This is your last chance to change your name and team keyword.\nMiss it here and you're locked in until the game ends —\nif you want to change anything, do it at the stand now.
- ja: 個人の名前とチームの合い言葉を、最後にもう一度だけ変更できます。\nここを逃すとゲームが終わるまで変更できなくなるので、\n変更したい方は今すぐ看板で決め直してください。
- zh-Hans: 个人名字和团队关键词，这是最后一次修改机会。\n错过这里，就要等到本局游戏结束才能再改，\n想改的话，现在就去牌子那边重新设置吧。
- zh-Hant: 個人名字和團隊關鍵詞，這是最後一次修改機會。\n錯過這裡的話，要等到本局遊戲結束才能再改，\n想改的話，現在就到牌子那邊重新設定吧。
- es: Puedes cambiar tu nombre y la palabra de equipo una última vez aquí.\nSi te lo saltas, no podrás cambiarlos hasta que acabe la partida,\nasí que si quieres cambiarlos, hazlo ahora en el letrero.
- es-419: Puedes cambiar tu nombre y la palabra de equipo una última vez aquí.\nSi te lo pierdes, no vas a poder cambiarlos hasta que termine la partida,\nasí que si quieres cambiarlos, hazlo ahora en el letrero.
- fr: Tu peux changer ton nom et le mot d'équipe une dernière fois ici.\nSi tu rates ça, tu ne pourras plus les changer avant la fin de la partie,\nalors si tu veux les changer, fais-le maintenant sur le panneau.
- de: Hier kannst du deinen Namen und das Team-Wort ein letztes Mal ändern.\nVerpasst du das, kannst du sie bis zum Ende der Partie nicht mehr ändern,\nalso ändere sie jetzt am Schild, wenn du willst.
- pt: Aqui podes mudar o teu nome e a palavra de equipa uma última vez.\nSe perderes esta oportunidade, só podes voltar a mudá-los quando a partida acabar,\npor isso, se quiseres mudar, faz já no letreiro.
- pt-BR: Aqui você pode mudar seu nome e a palavra da equipe uma última vez.\nSe perder essa chance, só vai poder mudar de novo quando a partida acabar,\nentão, se quiser mudar, faça agora na placa.
- ru: Здесь можно в последний раз изменить своё имя и командное слово.\nЕсли упустишь этот момент, изменить их будет нельзя до конца партии,\nтак что если хочешь что-то поменять — сделай это сейчас у таблички.
- pl: Tutaj możesz ostatni raz zmienić swoje imię i hasło drużyny.\nJeśli to przegapisz, nie zmienisz ich już do końca rozgrywki,\nwięc jeśli chcesz coś zmienić, zrób to teraz przy tablicy.

**적용 상태 (2026-09-08, MCP):**

- [x] `Tutorial` 테이블에 `Interlude.Board_NameChange.Title`/`.Body` 13로케일 입력
- [x] `Interlude.unity`의 `Board_Test/Title`·`Board_Test/Body`에 `LocalizeStringEvent` 부착, 위 새 키로 연결(기존에 `Tutorial.Board_Test.*`를 잘못 물고 있던 것 수정)
- [ ] Play 모드에서 Locale 바꿔가며 문구가 바뀌는지 스모크 테스트 (사용자)

## Prompt (E-키 상호작용 안내, Tutorial·Interlude 공용)

> `CheerNameSignboard`(응원 이름 패널 오픈)와 `TutorialCheerNameTest`(미니 입 — 팀 응원 인식 테스트)의 월드스페이스 `PromptRoot/PromptText`. 두 씬(`Tutorial.unity`, `Interlude.unity`)에 동일 기능이 각각 존재하고 문구도 동일하므로, `CheerNamePanel.*`과 같은 방식으로 **키를 공유**함(씬별로 따로 안 만듦).
> `TutorialCheerNameTest`의 프롬프트는 원래 `[E] Team Cheer Test`로 **영어가 하드코딩**되어 있었음(로컬라이즈 미적용 상태로 방치됨) — 2026-09-08에 발견, 한국어 베이스 텍스트로 교정 후 로컬라이즈함.

| 키 | 연결 대상 | 원문(교정 전) |
|---|---|---|
| `Tutorial.Prompt.CheerName` | `CheerNameSignboard/PromptRoot/PromptText` (Tutorial·Interlude 공용) | `[E] 이름 설정` |
| `Tutorial.Prompt.TeamCheerTest` | `TutorialCheerNameTest/PromptRoot/PromptText` (Tutorial·Interlude 공용) | `[E] Team Cheer Test` → `[E] 팀 응원 테스트`로 교정 |

**명칭 결정 (2026-09-08):** "이름 설정"은 패널 제목("응원 이름을 정해주세요")과 결이 맞아 그대로 유지. "Team Cheer Test"는 프로젝트 전체가 한국어 베이스 + 로컬라이즈 오버레이 구조인데 이 프롬프트만 영어였던 것이라 한국어 "팀 응원 테스트"로 교정 — `Board_Test` 타이틀("팀 응원 연습")은 구역 설명, 이 프롬프트는 행동 안내라 "테스트"로 구분해서 사용.

### `Tutorial.Prompt.CheerName`

- ko: [E] 이름 설정
- en: [E] Set Name
- ja: [E] 名前設定
- zh-Hans: [E] 设置名字
- zh-Hant: [E] 設定名字
- es: [E] Configurar nombre
- es-419: [E] Configurar nombre
- fr: [E] Définir le nom
- de: [E] Namen festlegen
- pt: [E] Definir nome
- pt-BR: [E] Definir nome
- ru: [E] Задать имя
- pl: [E] Ustaw imię

### `Tutorial.Prompt.TeamCheerTest`

- ko: [E] 팀 응원 테스트
- en: [E] Team Cheer Test
- ja: [E] チーム応援テスト
- zh-Hans: [E] 团队应援测试
- zh-Hant: [E] 團隊應援測試
- es: [E] Prueba de ánimo de equipo
- es-419: [E] Prueba de ánimo de equipo
- fr: [E] Test d'encouragement d'équipe
- de: [E] Team-Anfeuerung testen
- pt: [E] Teste de incentivo de equipa
- pt-BR: [E] Teste de torcida em equipe
- ru: [E] Тест командной поддержки
- pl: [E] Test dopingu drużynowego

**적용 상태 (2026-09-08, MCP):**

- [x] `Tutorial` 테이블에 `Tutorial.Prompt.CheerName`/`.TeamCheerTest` 13로케일 입력
- [x] `Tutorial.unity` — `CheerNameSignboard`·`TutorialCheerNameTest`의 `PromptText`에 `LocalizeStringEvent` 부착 + 연결
- [x] `Interlude.unity` — 동일 구조 2곳에 `LocalizeStringEvent` 부착 + 연결, `TutorialCheerNameTest` 쪽 영어 잔존 텍스트를 한국어로 교정
- [ ] Play 모드에서 Locale 바꿔가며 두 프롬프트가 바뀌는지 스모크 테스트 (사용자)

## 참고

- `Board_TeamCheer`/`Board_Test` 본문의 "입·식도의 위협" 표현은 사용자가 기존 "함정이 원상복구됩니다" 문구를 교체한 최신본을 반영함.
- 이 문서는 기존 `Assets/Docs/OXQuizTranslations.md`(OX퀴즈 번역, 별도 작업 완료되어 삭제됨)와 별개의 String Table Collection(`Tutorial` vs `OXQuiz`)을 사용함.
