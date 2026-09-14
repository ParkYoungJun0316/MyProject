# TutorialTranslations — Tutorial 씬 `TutorialInfoBoards` + `CheerNamePanel` 13개 언어 번역본

> 대상: `Tutorial.unity` → ①`TutorialInfoBoards` 하위 안내판 4개(`Board_Controls`, `Board_TeamCheer`, `Board_CheerName`, `Board_GotoStartZone`)의 정적 TMP 텍스트, ②`CheerNamePanel`(TeamCheerWord 입력 패널) 정적 텍스트 + 동적 피드백 문구. 같은 String Table Collection **`Tutorial`** 하나로 통합 관리(2026-09-07, 사용자 결정 — 별도 테이블 안 만듦). `Interlude.unity` 전용 `Board_NameChange`와 Tutorial·Interlude 공용 `Prompt.*`(E-키 안내)도 같은 테이블에 추가됨(2026-09-08).
>
> **[2026-09-14 확정, 최종] 개인 CheerName 커스텀화 완전 삭제.** 이름은 이제 `PlayerColorUtil.DefaultCheerNames`(berry/guma/sook/dan) 고정값 — 입력/검증/재제출 UI 없음. `Board_SelfCheer`(개인 응원 안내)와 `Board_Test`(팀 응원 연습)는 `Board_TeamCheer` 하나로 합쳐졌고, `Board_CheerName`/`CheerNamePanel`은 TeamCheerWord 전용으로 축소됐다. `Board_Controls`의 `Row_Voice`(개인 이름 음성 안내, 이미 오류였음)는 삭제하고 `Row_Buff`에 Space 발동 안내를 합쳤으며, 새로 `Row_Revive`(부활 조작 안내)를 추가했다.
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
| `Tutorial.Board_Controls.Row_BuffNote` | `.../Row_Buff/Note` |
| `Tutorial.Board_Controls.Row_Revive` | `.../Row_Revive/Label` (신규, 2026-09-14 — 구 `Row_Voice` 자리 재사용) |
| `Tutorial.Board_TeamCheer.Title` / `.Body` | `TutorialInfoBoards/Board_TeamCheer/Face/Title`, `/Body` (2026-09-14 — 구 `Board_Test` 내용 흡수) |
| `Tutorial.Board_CheerName.Title` / `.Body` | `TutorialInfoBoards/Board_CheerName/Face/Title`, `/Body` (2026-09-14 — TeamCheerWord 전용으로 축소) |
| `Tutorial.Board_GotoStartZone.Title` / `.Body` | `TutorialInfoBoards/Board_GotoStartZone/Face/Title`, `/Body` |

`CheerNamePanel` 키는 씬 경로 대신 UI 요소별 역할명 사용 (패널이 `TutorialInfoBoards`처럼 Face 구조가 아니라 평면 UI라서):

| 키 | 연결 대상 | 방식 |
|---|---|---|
| `Tutorial.CheerNamePanel.Title` | `CheerNamePanel/TitleText` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.TeamWordInputPlaceholder` | `.../HostTeamWordSection/TeamWordInputField/Text Area/Placeholder` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.ConfirmButton` | `HostTeamWordSection/TeamWordConfirmButton/Text (TMP)` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.CloseButton` | `CloseButton/Text (TMP)` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.HostHint` | `.../HostTeamWordSection/HostHintText` | `LocalizeStringEvent` |
| `Tutorial.CheerNamePanel.TeamKeywordPrefix` | `TutorialCheerNameUI.teamKeywordPrefix` (코드, `{0}` 포맷) | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Format` | `TutorialCheerNameUI.feedbackFormat` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Reserved_Team` | `TutorialCheerNameUI.feedbackReservedTeam` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Blocked` | `TutorialCheerNameUI.feedbackBlocked` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_Generic_Team` | `TutorialCheerNameUI.feedbackGenericTeam` | `LocalizedString` 필드 |
| `Tutorial.CheerNamePanel.Feedback_NotServer` | `TutorialCheerNameUI.feedbackNotServer` | `LocalizedString` 필드 |

**[2026-09-14 삭제]** 개인 CheerName 커스텀화 삭제로 아래 8개 키는 더 이상 쓰이지 않음(입력 UI 자체가 없어짐): `Examples`, `NameInputPlaceholder`, `Feedback_Reserved_Name`, `Feedback_Taken_Name`, `Feedback_Taken_Team`(팀워드가 겹칠 대상 자체가 없어져 도달 불가), `Feedback_Generic_Name`, `Feedback_Submitting`, `Feedback_Timeout`(팀워드 설정은 RPC 왕복 없는 동기 호출이라 대기 상태가 없음).

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

> **[2026-09-14 개편]** Space 발동 안내를 라벨에 합침 — 구 `Row_Voice`(개인 이름 음성 안내, 오류였음)가 삭제되면서 Space 정보가 갈 곳이 없어졌기 때문.

- ko: 개인 버프: Q 전환 · Space 발동 (방어↔이속)
- en: Personal buff: Q to swap · Space to activate (Guard ↔ Speed)
- ja: 個人バフ：Qで切替・Spaceで発動（防御↔速度）
- zh-Hans: 个人增益：Q切换 · Space发动（防御↔加速）
- zh-Hant: 個人增益：Q切換 · Space發動（防禦↔加速）
- es: Mejora personal: Q para cambiar · Espacio para activar (defensa ↔ velocidad)
- es-419: Mejora personal: Q para cambiar · Espacio para activar (defensa ↔ velocidad)
- fr: Bonus perso : Q pour changer · Espace pour activer (défense ↔ vitesse)
- de: Persönlicher Buff: Q zum Wechseln · Leertaste zum Aktivieren (Abwehr ↔ Tempo)
- pt: Bónus pessoal: Q para trocar · Espaço para ativar (defesa ↔ velocidade)
- pt-BR: Buff pessoal: Q para trocar · Espaço para ativar (defesa ↔ velocidade)
- ru: Личный бафф: Q — смена · Пробел — активация (защита ↔ скорость)
- pl: Osobisty buff: Q — zmiana · Spacja — aktywacja (obrona ↔ prędkość)

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

### `Tutorial.Board_Controls.Row_Revive` **[2026-09-14 신규 — 구 `Row_Voice` 자리 재사용]**

- ko: 부활 (다운된 팀원 옆 [E] 홀드)
- en: Revive (hold [E] next to a downed teammate)
- ja: 蘇生（ダウンした仲間のそばで[E]長押し）
- zh-Hans: 复活（在倒地队友旁按住[E]）
- zh-Hant: 復活（在倒地隊友旁按住[E]）
- es: Reanimar (mantén [E] junto a un compañero caído)
- es-419: Reanimar (mantén presionado [E] junto a un compañero caído)
- fr: Réanimer (maintiens [E] près d'un coéquipier à terre)
- de: Wiederbeleben (halte [E] neben einem niedergestreckten Teammitglied)
- pt: Reanimar (mantém [E] junto a um colega caído)
- pt-BR: Reanimar (segure [E] perto de um colega caído)
- ru: Оживление (держи [E] рядом с упавшим товарищем)
- pl: Ożywianie (przytrzymaj [E] obok powalonego towarzysza)

---

## ~~Board_SelfCheer (개인 응원)~~ **[2026-09-14 삭제]**

개인 CheerName 커스텀화가 완전히 삭제되면서 이 보드 자체가 씬에서 사라졌다(이미 그 전에 제거된 상태였고, 로컬라이제이션 키만 고아로 남아있던 것을 여기서 정리). 개인 버프는 `Board_Controls.Row_Buff`가 다루는 Q/Space 키 조작으로 대체됐다.

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

### `Tutorial.Board_TeamCheer.Body` **[2026-09-14 개편 — 구 `Board_Test` 내용 흡수]**

> 경고 표시(빨간 느낌표, 1회 통과 규칙)는 텍스트로 설명하지 않는다 — [E]로 바로 연습해보면 직관적으로 보이므로 굳이 규칙을 나열하지 않기로 함(사용자 결정, 2026-09-14).

- ko: 경고 아이콘이 뜨면 팀워드를 다같이 외쳐서 위협을 되돌리세요.\n여기서 [E]를 누르면 바로 연습할 수 있어요.
- en: When the warning icon appears, shout your team's word together to undo the threat.\nPress [E] here to practice right now.
- ja: 警告アイコンが出たら、チームの合言葉をみんなで叫んで脅威を元に戻しましょう。\nここで[E]を押せば、すぐに練習できます。
- zh-Hans: 警告图标出现时，全队一起喊出团队关键词，把威胁解除。\n在这里按[E]即可马上练习。
- zh-Hant: 警告圖示出現時，全隊一起喊出團隊關鍵詞，把威脅解除。\n在這裡按[E]即可馬上練習。
- es: Cuando aparezca el icono de aviso, gritad todos juntos la palabra de equipo para anular la amenaza.\nPulsa [E] aquí para practicar ahora mismo.
- es-419: Cuando aparezca el ícono de advertencia, griten todos juntos la palabra de equipo para anular la amenaza.\nPresiona [E] aquí para practicar ahora mismo.
- fr: Quand l'icône d'alerte apparaît, criez tous ensemble le mot d'équipe pour annuler la menace.\nAppuie sur [E] ici pour t'entraîner tout de suite.
- de: Wenn das Warnsymbol erscheint, ruft gemeinsam das Team-Wort, um die Bedrohung rückgängig zu machen.\nDrücke hier [E], um sofort zu üben.
- pt: Quando aparecer o ícone de aviso, gritem todos juntos a palavra de equipa para anular a ameaça.\nCarrega em [E] aqui para praticares já.
- pt-BR: Quando o ícone de aviso aparecer, gritem juntos a palavra da equipe para desfazer a ameaça.\nAperte [E] aqui para praticar agora mesmo.
- ru: Когда появится значок предупреждения, прокричите командное слово все вместе, чтобы отменить угрозу.\nНажми [E] здесь, чтобы потренироваться прямо сейчас.
- pl: Gdy pojawi się ikona ostrzeżenia, krzyknijcie razem drużynowe hasło, żeby cofnąć zagrożenie.\nNaciśnij tu [E], żeby od razu poćwiczyć.

---

## Board_CheerName (팀 키워드 설정) **[2026-09-14 개편 — 개인 응원 이름 삭제, TeamCheerWord 전용으로 축소]**

### `Tutorial.Board_CheerName.Title`

- ko: 팀 키워드
- en: Team Word
- ja: チームの合言葉
- zh-Hans: 团队关键词
- zh-Hant: 團隊關鍵詞
- es: Palabra de equipo
- es-419: Palabra de equipo
- fr: Mot d'équipe
- de: Team-Wort
- pt: Palavra de equipa
- pt-BR: Palavra da equipe
- ru: Командное слово
- pl: Hasło drużyny

### `Tutorial.Board_CheerName.Body`

- ko: TeamCheerWord는 host만 설정할 수 있어요.\n실제로 자주 쓰는 영어 단어일수록 인식이 잘 돼요. (2~12자, 영문 소문자만)\n마이크가 없거나 인식이 안 되면 ESC → 일반 탭 → "T키로 응원하기"를 켜고, 경고가 뜨면 T키를 누르세요.
- en: Only the host can set the TeamCheerWord.\nReal words you'd actually say out loud are recognized best. (2–12 letters, lowercase English only)\nNo mic, or not being recognized? Turn on T-key cheering in Options → General, then press T when the warning appears.
- ja: TeamCheerWordはホストだけが設定できます。\n実際によく使う単語ほど認識されやすいです。（2〜12文字、半角英小文字のみ）\nマイクがない、または認識されない場合はオプション→一般タブでTキー応援をオンにし、警告が出たらTキーを押しましょう。
- zh-Hans: 只有房主可以设置TeamCheerWord。\n越是常用的英文单词，识别率越高。（2~12个字符，仅限英文小写字母）\n没有麦克风或识别不了？在选项→常规标签打开T键应援，警告出现时按T键。
- zh-Hant: 只有房主可以設定TeamCheerWord。\n越常用的英文單字，辨識率越高。（2~12個字元，僅限英文小寫字母）\n沒有麥克風或辨識不出來？在選項→一般分頁開啟T鍵應援，警告出現時按T鍵。
- es: Solo el host puede fijar la TeamCheerWord.\nCuanto más natural sea la palabra al pronunciarla, mejor se reconoce. (2-12 letras, solo minúsculas en inglés)\n¿Sin micrófono o no te reconoce? Activa el ánimo con la tecla T en Opciones → General, y pulsa T cuando aparezca el aviso.
- es-419: Solo el host puede definir la TeamCheerWord.\nMientras más natural sea la palabra al decirla, mejor se reconoce. (2 a 12 letras, solo minúsculas en inglés)\n¿No tienes micrófono o no te reconoce? Activa el ánimo con la tecla T en Opciones → General, y presiona T cuando aparezca la advertencia.
- fr: Seul l'hôte peut définir le TeamCheerWord.\nPlus le mot est naturel à prononcer, mieux il est reconnu. (2 à 12 lettres, minuscules latines uniquement)\nPas de micro, ou la reconnaissance qui bugue ? Active les encouragements à la touche T dans Options → Général, puis appuie sur T quand l'alerte apparaît.
- de: Nur der Host kann das TeamCheerWord festlegen.\nJe natürlicher das Wort beim Aussprechen klingt, desto besser wird es erkannt. (2–12 Zeichen, nur englische Kleinbuchstaben)\nKein Mikro oder die Erkennung klappt nicht? Aktiviere die T-Taste-Anfeuerung unter Optionen → Allgemein und drücke T, wenn die Warnung erscheint.
- pt: Só o anfitrião pode definir a TeamCheerWord.\nQuanto mais natural for a palavra ao dizê-la, melhor é reconhecida. (2 a 12 letras, apenas minúsculas em inglês)\nSem microfone ou o reconhecimento não funciona? Ativa o incentivo pela tecla T em Opções → Geral e carrega em T quando aparecer o aviso.
- pt-BR: Só o host pode definir a TeamCheerWord.\nQuanto mais natural for a palavra na hora de falar, melhor o reconhecimento. (2 a 12 letras, apenas minúsculas em inglês)\nSem microfone ou o reconhecimento não está funcionando? Ative a torcida pela tecla T em Opções → Geral e aperte T quando o aviso aparecer.
- ru: Только хост может задать TeamCheerWord.\nЧем естественнее звучит слово, когда его произносишь, тем лучше оно распознаётся. (2–12 символов, только строчные латинские буквы)\nНет микрофона или распознавание не работает? Включи поддержку клавишей T в Опциях → Общие и нажимай T, когда появится предупреждение.
- pl: Tylko host może ustawić TeamCheerWord.\nIm bardziej naturalnie brzmi słowo, gdy je wypowiadasz, tym lepiej jest rozpoznawane. (2–12 znaków, tylko małe litery angielskiego alfabetu)\nNie masz mikrofonu albo rozpoznawanie nie działa? Włącz doping klawiszem T w Opcje → Ogólne i naciskaj T, gdy pojawi się ostrzeżenie.

---

## ~~Board_Test (팀 응원 연습)~~ **[2026-09-14 삭제 — `Board_TeamCheer`로 흡수]**

내용이 `Board_TeamCheer`와 거의 중복이라 2보드 → 1보드로 통합. 물리적으로 어느 GameObject를 남길지(구 `Board_Test` 자리 vs 구 `Board_TeamCheer` 자리)는 씬 배치 문제라 사용자가 정함 — 살아남는 오브젝트가 `Tutorial.Board_TeamCheer.*` 키를 쓰도록 `LocalizeStringEvent` 연결.

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

> **[2026-09-14]** 개인 CheerName 확정 안내 → 게이트에서 잠기는 **팀 키워드**(host 설정) 안내로 교체.

- ko: 노란색 바닥에 시작 존이 있습니다.\n팀 전원이 모이면 카운트다운 후 게임이 시작됩니다.\n카운트다운이 끝나면 그 순간의 팀 키워드로 확정되니,\nhost가 미리 정해 두었는지 확인하세요.
- en: The start zone is on the yellow floor.\nOnce the whole team gathers there, a countdown begins and the game starts.\nYour team word locks in the moment the countdown ends,\nso make sure the host has set it.
- ja: 黄色い床にスタートゾーンがあります。\nチーム全員が集まるとカウントダウン後にゲームが始まります。\nカウントダウンが終わった瞬間の合言葉で確定するので、\nhostが決めてあるか確認しておきましょう。
- zh-Hans: 黄色地板上就是出发区。\n全队集合后会倒计时，倒计时结束游戏就会开始。\n倒计时一结束，团队关键词就会立刻定下来，\n记得确认host已经设置好。
- zh-Hant: 黃色地板上就是起始區。\n全隊集合後會倒數計時，倒數結束遊戲就會開始。\n倒數一結束，團隊關鍵詞就會立刻定案，\n記得確認host已經設定好。
- es: La zona de inicio está en el suelo amarillo.\nCuando todo el equipo se reúna allí, empezará una cuenta atrás y comenzará la partida.\nLa palabra de equipo queda fijada en el instante en que termine la cuenta atrás,\nasí que aseguraos de que el host la haya definido.
- es-419: La zona de inicio está en el piso amarillo.\nCuando todo el equipo se reúna ahí, empieza una cuenta regresiva y arranca la partida.\nLa palabra de equipo queda definida apenas termine la cuenta regresiva,\nasí que asegúrense de que el host la haya definido.
- fr: La zone de départ se trouve sur le sol jaune.\nDès que toute l'équipe s'y retrouve, un compte à rebours démarre puis la partie commence.\nLe mot d'équipe est figé au moment où le compte à rebours se termine,\nalors assurez-vous que l'hôte l'a bien défini.
- de: Die Startzone liegt auf dem gelben Boden.\nSobald sich das ganze Team dort versammelt, läuft ein Countdown, und das Spiel beginnt.\nDas Team-Wort wird genau in dem Moment festgelegt, in dem der Countdown endet,\nalso stellt vorher sicher, dass der Host es gesetzt hat.
- pt: A zona de início fica no chão amarelo.\nQuando toda a equipa se juntar ali, começa uma contagem decrescente e o jogo arranca.\nA palavra de equipa fica fixada no instante em que a contagem termina,\npor isso confirmem que o anfitrião já a definiu.
- pt-BR: A zona de início fica no chão amarelo.\nQuando todo o time se reunir lá, começa uma contagem regressiva e o jogo começa.\nA palavra da equipe é definida no exato momento em que a contagem termina,\nentão confiram se o host já definiu.
- ru: Стартовая зона находится на жёлтом полу.\nКак только вся команда соберётся там, начнётся отсчёт, и игра запустится.\nКомандное слово фиксируется в тот момент, когда закончится отсчёт,\nтак что убедитесь, что хост его уже задал.
- pl: Strefa startowa znajduje się na żółtej podłodze.\nGdy zbierze się tam cała drużyna, ruszy odliczanie, a potem zacznie się gra.\nHasło drużyny zostaje ustalone dokładnie w momencie zakończenia odliczania,\nwięc upewnijcie się wcześniej, że host je ustawił.

---

## CheerNamePanel (팀 키워드 입력 패널)

### `Tutorial.CheerNamePanel.Title`

> **[2026-09-14]** 개인 CheerName 제목 → TeamCheerWord 전용으로 교체.

- ko: 팀 키워드를 정해주세요
- en: Choose your team word
- ja: チームの合言葉を決めてください
- zh-Hans: 请设置团队关键词
- zh-Hant: 請設定團隊關鍵詞
- es: Elige la palabra de equipo
- es-419: Elige la palabra de equipo
- fr: Choisis le mot d'équipe
- de: Leg das Team-Wort fest
- pt: Escolhe a palavra de equipa
- pt-BR: Escolha a palavra da equipe
- ru: Выбери командное слово
- pl: Wybierz hasło drużyny

> **[2026-09-14 삭제]** `Examples`/`NameInputPlaceholder`는 개인 CheerName 입력 필드와 함께 사라짐(아래 §키 네이밍 참고).

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

> **[2026-09-14 삭제]** `Feedback_Reserved_Name`/`Feedback_Taken_Name`/`Feedback_Taken_Team`/`Feedback_Generic_Name`/`Feedback_Submitting`/`Feedback_Timeout` — 개인 이름 입력 자체가 없어졌고, 팀워드 설정은 RPC 왕복 없는 동기 호출이라 제출 대기 상태도 없다(§키 네이밍 삭제 목록 참고).

---

## 적용 상태

### TutorialInfoBoards

1. [x] String Table Collection `Tutorial` 생성 (13개 로케일, `Assets/Localization/StringTables/Tutorial*.asset`)
2. [x] 키 + 번역 채움
3. [x] `TutorialInfoBoards` 하위 TMP에 `LocalizeStringEvent` 부착 (`OnUpdateString` → `TMP_Text.text`)
4. [ ] Play 모드에서 Locale 몇 개 바꿔가며 4개 보드(`Board_Controls`/`Board_TeamCheer`/`Board_CheerName`/`Board_GotoStartZone`)가 바뀌는지 스모크 테스트 (사용자)

### Board_Controls 개편 (2026-09-11, MCP 적용 / 2026-09-14 재개편)

1. [x] `TutorialTranslations.md` Controls 7키 한국어+13로케일 확정 (`Row_Move` 유지, Push/Color/Color2/Buff 문구 변경, `Row_BuffNote`·`Row_Voice` 신규)
2. [x] `Tutorial` String Table에 기존 4키 번역 갱신 + `Row_BuffNote`/`Row_Voice` 2키 추가 (13로케일)
3. [x] 씬 `Board_Controls/Face`: Q 행 아래 `Note` TMP → `Row_BuffNote`. 새 `Row_Voice`(아이콘 `Speek.png`) → `Row_Voice`. Q 아이콘은 `Row_Buff`에 유지.
4. **[2026-09-14 최종]** `Row_Voice` 삭제(개인 이름 음성 안내, 이미 오류였음), `Row_Buff`에 Space 발동 안내 병합, 신규 `Row_Revive`(부활 조작 안내) 추가 — 13로케일 `.asset` 반영 완료
5. [ ] Play 모드에서 Controls 보드 6행+Note 스모크 (사용자, `Row_Voice` 아이콘 오브젝트도 씬에서 제거할 것)

### CheerNamePanel **[2026-09-14 개편 — TeamCheerWord 전용]**

1. [x] `CheerNameValidator.cs` — 형식 a-z만 허용(숫자/밑줄 제외), 금칙어에 `sex` 추가 (TeamCheerWord 전용 검증기로 축소, 독스트링 갱신)
2. [x] `TutorialCheerNameUI.cs` — 개인 이름 입력 필드·검증·RPC 왕복 로직 전부 삭제, TeamCheerWord 섹션만 남김
3. [x] 번역 키 확정 (위 §CheerNamePanel — Examples/NameInputPlaceholder/Feedback_Reserved_Name/Taken_Name/Taken_Team/Generic_Name/Submitting/Timeout 8키 삭제)
4. [x] String Table Collection `Tutorial`에 반영 (13로케일)
5. [x] 씬에서 `CheerNamePanel`의 개인 이름 입력 필드(`NameInputField`)·`ExamplesText`(및 고아 `ConfirmButton`) UI 오브젝트 제거 (MCP, 2026-09-14)
6. [ ] Play 모드에서 Locale 바꿔가며 패널 문구·피드백이 바뀌는지 스모크 테스트 (사용자)

## Interlude.Board_NameChange (Interlude 씬 전용 안내판) **[2026-09-14 개편 — TeamCheerWord 전용으로 축소]**

> `Interlude.unity`의 `Board_Test` GameObject(Tutorial의 `TutorialInfoBoards/Board_Test`와 오브젝트 이름만 같고 내용은 다름 — TeamCheerWord 2차 변경 안내). 같은 `Tutorial` String Table Collection을 재사용하되, **키는 반드시 `Tutorial.Board_Test.*`와 구분**할 것 — 이름이 같다고 그 키를 재사용하면 Interlude 안내판이 Tutorial의 "팀 응원 연습" 문구로 덮어써진다(2026-09-08 실제 발견·수정: Interlude `Board_Test`가 씬 복제 과정에서 `Tutorial.Board_Test.*` 키를 그대로 물고 있었음).
> **[2026-09-14]** 개인 CheerName 커스텀화 완전 삭제로 2차 변경 대상은 이제 TeamCheerWord뿐 — Title/Body 모두 "이름"에서 "팀 키워드"로 교체.

### `Interlude.Board_NameChange.Title`

- ko: 팀 키워드 바꾸기
- en: Change Team Word
- ja: チームの合言葉を変える
- zh-Hans: 更改团队关键词
- zh-Hant: 更改團隊關鍵詞
- es: Cambiar la palabra de equipo
- es-419: Cambiar la palabra de equipo
- fr: Changer le mot d'équipe
- de: Team-Wort ändern
- pt: Mudar a palavra de equipa
- pt-BR: Mudar a palavra da equipe
- ru: Изменить командное слово
- pl: Zmień hasło drużyny

### `Interlude.Board_NameChange.Body`

- ko: 팀 키워드는 마지막으로 한 번 더 바꿀 수 있어요.\n여기서 놓치면 게임이 끝날 때까지 못 바꾸니,\n원하시는 분은 발판에서 지금 다시 정하세요.
- en: You can change your team word one last time here.\nMiss this chance and it's locked for the rest of the game,\nso reset it at the panel now if you want to.
- ja: チームの合言葉は、ここが最後の変更チャンスです。\n今を逃すとゲームが終わるまで変更できないので、\n変えたい方はパネルで今すぐ設定し直してください。
- zh-Hans: 团队关键词在这里可以做最后一次更改。\n错过这次就要到游戏结束都无法再改，\n想改的人请现在到面板重新设置。
- zh-Hant: 團隊關鍵詞在這裡可以做最後一次更改。\n錯過這次就要到遊戲結束都無法再改，\n想改的人請現在到面板重新設定。
- es: Aquí podéis cambiar la palabra de equipo por última vez.\nSi dejáis pasar esta oportunidad, quedará fija hasta el final de la partida,\nasí que quien quiera cambiarla que lo haga ahora en el panel.
- es-419: Aquí pueden cambiar la palabra de equipo por última vez.\nSi dejan pasar esta oportunidad, queda fija hasta el final de la partida,\nasí que quien quiera cambiarla que lo haga ahora en el panel.
- fr: Vous pouvez changer le mot d'équipe une dernière fois ici.\nSi vous laissez passer cette chance, il restera figé jusqu'à la fin de la partie,\nalors si tu veux le changer, fais-le maintenant sur le panneau.
- de: Hier könnt ihr das Team-Wort ein letztes Mal ändern.\nVerpasst ihr diese Chance, bleibt es für den Rest der Partie fest,\nalso legt es jetzt am Panel neu fest, wenn ihr wollt.
- pt: Aqui podem mudar a palavra de equipa pela última vez.\nSe perderem esta oportunidade, fica fixa até ao fim do jogo,\npor isso quem quiser mudá-la, faça-o agora no painel.
- pt-BR: Aqui vocês podem mudar a palavra da equipe pela última vez.\nSe perderem essa chance, ela fica travada até o fim do jogo,\nentão quem quiser mudar, faça isso agora no painel.
- ru: Здесь можно изменить командное слово в последний раз.\nЕсли упустите этот момент, оно останется неизменным до конца игры,\nтак что, если хотите его поменять, сделайте это сейчас на панели.
- pl: Tutaj możecie ostatni raz zmienić hasło drużyny.\nJeśli przegapicie tę okazję, zostanie ono zablokowane do końca gry,\nwięc jeśli ktoś chce je zmienić, niech zrobi to teraz na panelu.

**적용 상태 (2026-09-08, MCP):**

- [x] `Tutorial` 테이블에 `Interlude.Board_NameChange.Title`/`.Body` 13로케일 입력
- [x] `Interlude.unity`의 `Board_Test/Title`·`Board_Test/Body`에 `LocalizeStringEvent` 부착, 위 새 키로 연결(기존에 `Tutorial.Board_Test.*`를 잘못 물고 있던 것 수정)
- [ ] Play 모드에서 Locale 바꿔가며 문구가 바뀌는지 스모크 테스트 (사용자)

## Prompt (E-키 상호작용 안내, Tutorial·Interlude 공용)

> `CheerNameSignboard`(응원 이름 패널 오픈)와 `TutorialCheerNameTest`(미니 입 — 팀 응원 인식 테스트)의 월드스페이스 `PromptRoot/PromptText`. 두 씬(`Tutorial.unity`, `Interlude.unity`)에 동일 기능이 각각 존재하고 문구도 동일하므로, `CheerNamePanel.*`과 같은 방식으로 **키를 공유**함(씬별로 따로 안 만듦).
> `TutorialCheerNameTest`의 프롬프트는 원래 `[E] Team Cheer Test`로 **영어가 하드코딩**되어 있었음(로컬라이즈 미적용 상태로 방치됨) — 2026-09-08에 발견, 한국어 베이스 텍스트로 교정 후 로컬라이즈함.

| 키 | 연결 대상 | 원문(교정 전) |
|---|---|---|
| `Tutorial.Prompt.CheerName` | `CheerNameSignboard/PromptRoot/PromptText` (Tutorial·Interlude 공용) | `[E] 이름 설정` → `[E] 팀 키워드 설정`로 재교정(2026-09-14) |
| `Tutorial.Prompt.TeamCheerTest` | `TutorialCheerNameTest/PromptRoot/PromptText` (Tutorial·Interlude 공용) | `[E] Team Cheer Test` → `[E] 팀 응원 테스트`로 교정 |

**명칭 결정 (2026-09-08, 2026-09-14 갱신):** 원래 "이름 설정"은 패널 제목("응원 이름을 정해주세요")과 결을 맞춘 것이었으나, 개인 CheerName 커스텀화가 완전히 삭제되면서 이 표지판이 여는 패널이 TeamCheerWord 전용이 됐다 — 문구도 "팀 키워드 설정"으로 갱신. "Team Cheer Test"는 프로젝트 전체가 한국어 베이스 + 로컬라이즈 오버레이 구조인데 이 프롬프트만 영어였던 것이라 한국어 "팀 응원 테스트"로 교정.

### `Tutorial.Prompt.CheerName`

- ko: [E] 팀 키워드 설정
- en: [E] Set team word
- ja: [E] チームの合言葉を設定
- zh-Hans: [E] 设置团队关键词
- zh-Hant: [E] 設定團隊關鍵詞
- es: [E] Definir palabra de equipo
- es-419: [E] Definir palabra de equipo
- fr: [E] Définir le mot d'équipe
- de: [E] Team-Wort festlegen
- pt: [E] Definir palavra de equipa
- pt-BR: [E] Definir palavra da equipe
- ru: [E] Задать командное слово
- pl: [E] Ustaw hasło drużyny

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

- 이 문서는 기존 `Assets/Docs/OXQuizTranslations.md`(OX퀴즈 번역, 별도 작업 완료되어 삭제됨)와 별개의 String Table Collection(`Tutorial` vs `OXQuiz`)을 사용함.
- **[2026-09-14, 최종]** 개인 CheerName 커스텀화 완전 삭제 요약: `Board_SelfCheer` 삭제, `Board_Test`→`Board_TeamCheer` 흡수, `Board_CheerName`/`CheerNamePanel` TeamCheerWord 전용 축소, `Board_Controls.Row_Voice` 삭제(→`Row_Buff`에 병합)+`Row_Revive` 신규, `Interlude.Board_NameChange`/`Tutorial.Prompt.CheerName` 팀 키워드 전용 문구로 교체. 코드 쪽은 `PlayerCheerNameSync.cs` 삭제, `CheerService`/`GameSession`/`TutorialNetworkManager`/`InterludeNetworkManager`/`TutorialCheerNameUI` 단순화, 머리 위 이름표(`PlayerNameTagUI`, 고정값 표시)는 흑/백 팔레트 구분 문제로 재도입(`CheerSystemDesign.md` §10.3).
