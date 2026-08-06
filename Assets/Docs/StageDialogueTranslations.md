# StageDialogueTranslations — M/T 스테이지 대사 11개 언어 번역본

> SSOT 원문: [`StageDialogueLines.md`](StageDialogueLines.md) (한국어 확정본).
> 이 문서는 그 원문을 `en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl` 11개 언어로 번역한 결과다
> (`SteamworksIntegrationDesign.md` 트랙4 §10 코어 12개 언어 중 ko 제외).
>
> **에이전트는 `Assets/Localization/StringTables/*.asset`(String Table)이나 씬(.unity)을 직접 쓰지 않는다** (`unity-mcp-readonly.mdc` — 에셋/씬 파일은 워크스페이스 파일 도구로도 쓰지 않음).
> 아래 내용을 사람이 String Table Editor(또는 CSV Import)로 직접 입력해야 한다. 적용 방법은 맨 아래 "적용 체크리스트" 참고.

## ⚠️ 먼저 확인할 것 — 기존 파일럿 키와 충돌

`SteamworksIntegrationDesign.md` 트랙4 파일럿에서 `M.Stage1` 씬 `Dialogue_Panel`에 이미 4줄을 연결해뒀음:

- 키: `M.Stage1.Intro.Line1~4`
- 내용: Cheer 시스템/Stealth 설명 (영어만 채워짐) — 이건 **기술 검증용 placeholder**였고, `StageDialogueLines.md` 확정본 기준으론 그 설명은 `2.Tutorial` 씬 담당이라 `M.Stage1`에 있으면 안 되는 내용임.
- `StageDialogueLines.md`의 실제 `M.Stage1` 대사는 **1줄**("우리, 먹혔어...")뿐이라 4줄 구조와도 안 맞음.

**제안:** 아래 새 키 체계(`M.Stage1.Line1` 등)로 교체하고, 기존 `M.Stage1.Intro.Line1~4` 4줄(과 연결된 TMP 오브젝트)은 삭제하거나 `2.Tutorial` 씬으로 옮길 것. 이 판단·실행은 사용자가 에디터에서.

## 키 네이밍 규칙 (제안)

`StageDialogueLines.md` 섹션 구조를 그대로 키로 사용:

- `M.Stage1.Line1`
- `M.Stage3.Line1~3`
- `M.Stage4.Stage1.Line1~3` (Stage2는 대사 없음 — 원문 참고)
- `M.Stage5.Line1`
- `M.Boss.Intro.Line1~2`, `M.Boss.Bossdown.Line1~4`
- `T.Stage1.Line1~3`
- `T.Stage2.Stage1.Line1`, `T.Stage2.Stage2.Line1~2`, `T.Stage2.Stage3.Line1`
- `T.Stage5.Stage1.Line1`, `T.Stage5.Stage3.Line1`
- `T.Boss.Intro.Line1~3`, `T.Boss.Bossdown.Line1~3`

번역 대상에서 제외 (원문 미정 — `StageDialogueLines.md` "열려 있는 항목" 참고):

- `M.Stage2` (OX퀴즈) — 한국어 원문 자체가 없음
- `M.Stage5` BW 모드용 별도 문구 — 없음 (Color 모드와 공용, `M.Stage5.Line1` 그대로 사용)

---

## M.Stage1

### M.Stage1.Line1

- ko: 우리, 먹혔어. 이제부터 살아나가는 것만 생각해.
- en: We've been eaten. From now on, just focus on surviving.
- ja: 私たち、食べられた。これからは生き延びることだけ考えて。
- zh-Hans: 我们被吃掉了。从现在起，只想着怎么活下去。
- zh-Hant: 我們被吃掉了。從現在起，只想著怎麼活下去。
- es: Nos han comido. A partir de ahora, piensa solo en sobrevivir.
- es-419: Nos comieron. De ahora en adelante, piensa solo en sobrevivir.
- fr: On a été mangés. À partir de maintenant, pense juste à survivre.
- de: Wir wurden gefressen. Von jetzt an denk nur ans Überleben.
- pt-BR: Fomos comidos. A partir de agora, pense só em sobreviver.
- ru: Нас съели. Теперь думай только о том, как выжить.
- pl: Zostaliśmy zjedzeni. Od teraz myśl tylko o tym, jak przetrwać.

---

## M.Stage3

### M.Stage3.Line1

- ko: 이제부터 바닥에 네 색이랑 똑같은 타일이 무작위로 뜰 거야.
- en: From now on, a tile matching your color will randomly light up on the floor.
- ja: これから、床に自分の色と同じタイルがランダムに出るよ。
- zh-Hans: 从现在开始，地板上会随机出现和你颜色一样的方块。
- zh-Hant: 從現在開始，地板上會隨機出現跟你顏色一樣的方塊。
- es: A partir de ahora, aparecerá aleatoriamente en el suelo una baldosa igual a tu color.
- es-419: De ahora en adelante, va a aparecer al azar en el piso una baldosa igual a tu color.
- fr: À partir de maintenant, une tuile de ta couleur apparaîtra aléatoirement sur le sol.
- de: Von jetzt an erscheint zufällig eine Kachel in deiner Farbe auf dem Boden.
- pt-BR: A partir de agora, vai aparecer aleatoriamente no chão um bloco da sua cor.
- ru: Теперь на полу будет случайно появляться плитка твоего цвета.
- pl: Od teraz na podłodze będzie losowo pojawiać się płytka w twoim kolorze.

### M.Stage3.Line2

- ko: 제한 시간 안에 그 위로 올라서야 해.
- en: You have to step onto it before time runs out.
- ja: 時間内にその上に乗らなきゃ。
- zh-Hans: 你得在限时内站上去。
- zh-Hant: 你得在限時內站上去。
- es: Tienes que subirte a ella antes de que se acabe el tiempo.
- es-419: Tienes que subirte antes de que se acabe el tiempo.
- fr: Tu dois monter sur elle avant la fin du temps limite.
- de: Du musst rechtzeitig darauf steigen.
- pt-BR: Você precisa subir nele antes que o tempo acabe.
- ru: Тебе нужно успеть встать на неё до конца отведённого времени.
- pl: Musisz na nią wejść, zanim skończy się czas.

### M.Stage3.Line3

- ko: 못 올라서면... 천장의 이빨이 떨어지기 시작할 거야.
- en: If you don't make it... the teeth on the ceiling will start falling.
- ja: 乗れなかったら…天井の歯が落ちてくる。
- zh-Hans: 如果站不上去……天花板上的牙齿就会开始掉下来。
- zh-Hant: 如果站不上去……天花板上的牙齒就會開始掉下來。
- es: Si no lo logras... los dientes del techo empezarán a caer.
- es-419: Si no lo logras... los dientes del techo van a empezar a caer.
- fr: Si tu n'y arrives pas... les dents du plafond commenceront à tomber.
- de: Wenn du es nicht schaffst... fangen die Zähne von der Decke an zu fallen.
- pt-BR: Se não conseguir... os dentes do teto vão começar a cair.
- ru: Если не успеешь... зубы с потолка начнут падать.
- pl: Jeśli nie zdążysz... zęby z sufitu zaczną spadać.

---

## M.Stage4 (Stage1만 — Stage2는 대사 없음)

### M.Stage4.Stage1.Line1

> **강조 표기:** 키워드를 `<b><color=#FFC400>...</color></b>`로 감싸서 강조 (골드/노란 계열 — 키 입력 안내에 세계적으로 가장 흔히 쓰이는 색. 빨강은 이 프로젝트에서 이미 `OXQuizUI.cs`의 오답 색이라 배제, 순수 Yellow(`#DCA524`)는 플레이어 팀 색과 겹쳐서 그보다 밝은 골드 톤으로 구분).

- ko: 네 색이 바닥에 뜨면 <b><color=#FFC400>스페이스</color></b>를 눌러.
- en: Press <b><color=#FFC400>Space</color></b> when your color appears on the floor.
- ja: 自分の色が床に出たら<b><color=#FFC400>スペース</color></b>を押して。
- zh-Hans: 你的颜色出现在地板上时按<b><color=#FFC400>空格键</color></b>。
- zh-Hant: 你的顏色出現在地板上時按<b><color=#FFC400>空格鍵</color></b>。
- es: Pulsa <b><color=#FFC400>Espacio</color></b> cuando aparezca tu color en el suelo.
- es-419: Presiona <b><color=#FFC400>Espacio</color></b> cuando aparezca tu color en el piso.
- fr: Appuie sur <b><color=#FFC400>Espace</color></b> quand ta couleur apparaît sur le sol.
- de: Drück <b><color=#FFC400>Leertaste</color></b>, wenn deine Farbe auf dem Boden erscheint.
- pt-BR: Aperte <b><color=#FFC400>Espaço</color></b> quando sua cor aparecer no chão.
- ru: Нажимай <b><color=#FFC400>пробел</color></b>, когда на полу появится твой цвет.
- pl: Wciśnij <b><color=#FFC400>spację</color></b>, gdy twój kolor pojawi się na podłodze.

### M.Stage4.Stage1.Line2

- ko: 흰색은 아무나 눌러도 되고, 검은색은 절대 누르면 안 돼!
- en: Anyone can press for white, but never press for black!
- ja: 白色は誰が押してもいいけど、黒色は絶対押しちゃダメ!
- zh-Hans: 白色谁按都行，但黑色绝对不能按!
- zh-Hant: 白色誰按都行，但黑色絕對不能按!
- es: Cualquiera puede pulsar para el blanco, ¡pero nunca pulses para el negro!
- es-419: Cualquiera puede presionar para el blanco, ¡pero nunca presiones para el negro!
- fr: N'importe qui peut appuyer pour le blanc, mais ne jamais appuyer pour le noir!
- de: Bei Weiß darf jeder drücken, aber bei Schwarz niemals drücken!
- pt-BR: Qualquer um pode apertar para o branco, mas nunca aperte para o preto!
- ru: На белый может нажать кто угодно, а на чёрный — никогда не нажимай!
- pl: Na biały może wcisnąć każdy, ale na czarny nigdy nie wciskaj!

### M.Stage4.Stage1.Line3

- ko: 잘못 누르면 시간이 줄어드니까 조심해!
- en: A wrong press costs you time, so be careful!
- ja: 間違って押すと時間が減るから気をつけて!
- zh-Hans: 按错了时间会减少，小心点!
- zh-Hant: 按錯了時間會減少，小心點!
- es: ¡Si pulsas mal, pierdes tiempo, así que ten cuidado!
- es-419: ¡Si presionas mal, pierdes tiempo, así que cuidado!
- fr: Une mauvaise pression te fait perdre du temps, alors fais attention!
- de: Ein falscher Tastendruck kostet Zeit, also pass auf!
- pt-BR: Se apertar errado, você perde tempo, então cuidado!
- ru: Ошибёшься — потеряешь время, так что осторожно!
- pl: Błędne wciśnięcie kosztuje czas, więc uważaj!

---

## M.Stage5 (Color/BW 모드 공용 — BW 모드용 별도 문구 없음)

### M.Stage5.Line1

- ko: 네 색 타일 위에 올라가서 버텨!
- en: Get onto your color's tile and hold your ground!
- ja: 自分の色のタイルに乗って持ちこたえて!
- zh-Hans: 站到你颜色的方块上，撑住!
- zh-Hant: 站到你顏色的方塊上，撐住!
- es: ¡Sube a la baldosa de tu color y resiste!
- es-419: ¡Súbete a la baldosa de tu color y resiste!
- fr: Monte sur la tuile de ta couleur et tiens bon!
- de: Stell dich auf die Kachel deiner Farbe und halt durch!
- pt-BR: Suba no bloco da sua cor e aguente firme!
- ru: Встань на плитку своего цвета и держись!
- pl: Wejdź na płytkę swojego koloru i trzymaj się!

---

## M.Boss

### M.Boss.Intro.Line1

- ko: 여기가 입에서 마지막이야.
- en: This is the last stretch of the mouth.
- ja: ここが口の中、最後の区間だ。
- zh-Hans: 这里是嘴里的最后一段了。
- zh-Hant: 這裡是嘴裡的最後一段了。
- es: Este es el tramo final de la boca.
- es-419: Este es el tramo final dentro de la boca.
- fr: C'est ici la dernière partie de la bouche.
- de: Das hier ist das letzte Stück im Mund.
- pt-BR: Este é o último trecho dentro da boca.
- ru: Это последний участок во рту.
- pl: To już ostatni odcinek w ustach.

### M.Boss.Intro.Line2

- ko: 조금만 버티면 나갈 수 있을 것 같아...
- en: If we just hold on a little longer, we might get out...
- ja: あと少し耐えれば出られそう…
- zh-Hans: 再撑一会儿好像就能出去了……
- zh-Hant: 再撐一下好像就能出去了……
- es: Si aguantamos un poco más, parece que podremos salir...
- es-419: Si resistimos un poco más, parece que vamos a poder salir...
- fr: Si on tient encore un peu, on devrait pouvoir sortir...
- de: Wenn wir noch etwas durchhalten, kommen wir vielleicht raus...
- pt-BR: Se aguentarmos só um pouco mais, parece que vamos conseguir sair...
- ru: Если ещё немного продержаться, кажется, можно будет выбраться...
- pl: Jeśli jeszcze trochę wytrzymamy, chyba zdołamy się wydostać...

### M.Boss.Bossdown.Line1

- ko: 입 안이 조용해졌다... 다 멈춘 건가.
- en: It's gone quiet in here... did it all stop?
- ja: 口の中が静かになった…全部止まったのか。
- zh-Hans: 嘴里安静下来了……难道全都停了?
- zh-Hant: 嘴裡安靜下來了……難道全都停了?
- es: Se ha quedado en silencio dentro de la boca... ¿se detuvo todo?
- es-419: Todo quedó en silencio adentro... ¿se detuvo todo?
- fr: Le silence est revenu dans la bouche... tout s'est arrêté?
- de: Es ist still geworden im Mund... hat alles aufgehört?
- pt-BR: Ficou tudo quieto aqui dentro... será que parou tudo?
- ru: Во рту стало тихо... неужели всё остановилось?
- pl: W ustach zapadła cisza... czy to znaczy, że wszystko się zatrzymało?

### M.Boss.Bossdown.Line2

- ko: 이제 탈출할 수 있는 거지...?
- en: Does that mean we can escape now...?
- ja: これで脱出できるんだよね…?
- zh-Hans: 现在能逃出去了吧……?
- zh-Hant: 現在能逃出去了吧……?
- es: ¿Eso significa que ya podemos escapar...?
- es-419: ¿Eso quiere decir que ya podemos escapar...?
- fr: Ça veut dire qu'on peut s'échapper maintenant...?
- de: Heißt das, wir können jetzt entkommen...?
- pt-BR: Isso quer dizer que já podemos escapar...?
- ru: Значит, теперь мы можем сбежать...?
- pl: To znaczy, że możemy już uciec...?

### M.Boss.Bossdown.Line3

- ko: ...
- en: ...
- ja: …
- zh-Hans: ……
- zh-Hant: ……
- es: ...
- es-419: ...
- fr: ...
- de: ...
- pt-BR: ...
- ru: ...
- pl: ...

### M.Boss.Bossdown.Line4

- ko: 이런, 삼켜진다...!!
- en: Oh no, we're being swallowed...!!
- ja: まずい、飲み込まれる…!!
- zh-Hans: 糟了，又被吞下去了……!!
- zh-Hant: 糟了，又被吞下去了……!!
- es: ¡No, nos están tragando otra vez...!!
- es-419: ¡No, nos están tragando de nuevo...!!
- fr: Non, on est encore avalés...!!
- de: Oh nein, wir werden wieder verschluckt...!!
- pt-BR: Não, estamos sendo engolidos de novo...!!
- ru: О нет, нас снова глотают...!!
- pl: O nie, znowu nas przełykają...!!

---

## T.Stage1

### T.Stage1.Line1

- ko: 식도로 넘어온 건가...
- en: Did we just pass into the esophagus...?
- ja: 食道に入ったのか…
- zh-Hans: 是进到食道里了吗……
- zh-Hant: 是進到食道裡了嗎……
- es: ¿Hemos pasado al esófago...?
- es-419: ¿Pasamos al esófago...?
- fr: On serait passés dans l'œsophage...?
- de: Sind wir in die Speiseröhre gelangt...?
- pt-BR: A gente passou para o esôfago...?
- ru: Мы попали в пищевод...?
- pl: Czy przeszliśmy do przełyku...?

### T.Stage1.Line2

- ko: 뒤에서 뭔가 굴러오는 것 같은데...
- en: Something feels like it's rolling in from behind...
- ja: 後ろから何か転がってくる気がする…
- zh-Hans: 感觉后面有什么东西滚过来……
- zh-Hant: 感覺後面有什麼東西滾過來……
- es: Parece que algo viene rodando desde atrás...
- es-419: Siento que algo viene rodando desde atrás...
- fr: On dirait que quelque chose roule derrière nous...
- de: Es fühlt sich an, als würde etwas von hinten heranrollen...
- pt-BR: Parece que tem algo rolando vindo de trás...
- ru: Кажется, что-то катится сзади...
- pl: Wydaje mi się, że coś nadjeżdża z tyłu...

### T.Stage1.Line3

- ko: 달려!!!
- en: Run!!!
- ja: 走れ!!!
- zh-Hans: 快跑!!!
- zh-Hant: 快跑!!!
- es: ¡Corre!!!
- es-419: ¡Corre!!!
- fr: Cours!!!
- de: Lauf!!!
- pt-BR: Corre!!!
- ru: Бегом!!!
- pl: Biegnij!!!

---

## T.Stage2

### T.Stage2.Stage1.Line1

- ko: 잘못 밟으면 그대로 즉사야. 빛나는 칸만 외워둬.
- en: One wrong step and it's instant death. Just memorize the glowing tiles.
- ja: 間違って踏んだら即死だ。光るマスだけ覚えておけ。
- zh-Hans: 踩错一步就当场死亡。只要记住发光的格子。
- zh-Hant: 踩錯一步就當場死亡。只要記住發光的格子。
- es: Un paso en falso y es muerte instantánea. Memoriza solo las casillas que brillan.
- es-419: Un paso en falso es muerte instantánea. Memoriza solo las casillas que brillan.
- fr: Un faux pas et c'est la mort instantanée. Retiens juste les cases qui brillent.
- de: Ein falscher Schritt und es ist sofort vorbei. Merk dir nur die leuchtenden Felder.
- pt-BR: Um passo errado e é morte instantânea. Só memorize os quadrados que brilham.
- ru: Один неверный шаг — и мгновенная смерть. Запоминай только светящиеся клетки.
- pl: Jeden błędny krok i to natychmiastowa śmierć. Zapamiętaj tylko świecące pola.

### T.Stage2.Stage2.Line1

- ko: 색깔별로 보여줄 거야. 반드시 네 색에 맞춰!
- en: It'll show by color this time. Make sure you match your own color!
- ja: 今度は色ごとに見せるよ。必ず自分の色に合わせて!
- zh-Hans: 这次会按颜色显示。一定要对应你自己的颜色!
- zh-Hant: 這次會按顏色顯示。一定要對應你自己的顏色!
- es: Esta vez se mostrará por colores. ¡Asegúrate de igualar tu propio color!
- es-419: Esta vez se va a mostrar por colores. ¡Asegúrate de igualar tu propio color!
- fr: Cette fois, ça s'affichera par couleur. Fais bien correspondre ta propre couleur!
- de: Diesmal wird's nach Farben angezeigt. Achte unbedingt auf deine eigene Farbe!
- pt-BR: Dessa vez vai mostrar por cor. Não esqueça de combinar com a sua própria cor!
- ru: Теперь будет показано по цветам. Обязательно соответствуй своему цвету!
- pl: Tym razem pokaże się według kolorów. Koniecznie dopasuj do własnego koloru!

### T.Stage2.Stage2.Line2

- ko: 색이 맞아도 흑백이면 죽을 거야.
- en: Even if the color's right, black-and-white will kill you.
- ja: 色が合っていても、白黒だったら死ぬよ。
- zh-Hans: 颜色对了，但如果是黑白的话也会死。
- zh-Hant: 顏色對了，但如果是黑白的話也會死。
- es: Aunque el color sea correcto, si está en blanco y negro morirás.
- es-419: Aunque el color sea correcto, si está en blanco y negro vas a morir.
- fr: Même si la couleur est bonne, le noir et blanc te tuera.
- de: Selbst wenn die Farbe stimmt, bringt Schwarz-Weiß dich um.
- pt-BR: Mesmo com a cor certa, se estiver em preto e branco você morre.
- ru: Даже если цвет верный, чёрно-белое убьёт тебя.
- pl: Nawet jeśli kolor się zgadza, czarno-białe cię zabije.

### T.Stage2.Stage3.Line1

- ko: 구역마다 담당 색이 있어. 담당이 먼저 지나가야 길이 안전해져.
- en: Each zone has a color in charge. The path only becomes safe once that color goes through first.
- ja: 区域ごとに担当の色があるよ。担当が先に通らないと道は安全にならない。
- zh-Hans: 每个区域都有负责的颜色。负责的颜色先通过，路才会变安全。
- zh-Hant: 每個區域都有負責的顏色。負責的顏色先通過，路才會變安全。
- es: Cada zona tiene un color a cargo. El camino solo se vuelve seguro cuando ese color pasa primero.
- es-419: Cada zona tiene un color a cargo. El camino recién se vuelve seguro cuando ese color pasa primero.
- fr: Chaque zone a une couleur responsable. Le chemin ne devient sûr que quand cette couleur passe en premier.
- de: Jede Zone hat eine zuständige Farbe. Der Weg wird erst sicher, wenn diese Farbe zuerst durchgeht.
- pt-BR: Cada área tem uma cor responsável. O caminho só fica seguro depois que essa cor passar primeiro.
- ru: У каждой зоны свой ответственный цвет. Путь становится безопасным только после того, как этот цвет пройдёт первым.
- pl: Każda strefa ma odpowiedzialny kolor. Droga staje się bezpieczna tylko wtedy, gdy ten kolor przejdzie pierwszy.

---

## T.Stage5

### T.Stage5.Stage1.Line1

- ko: 도망치는 적혈구들을 잡아!
- en: Catch the fleeing red blood cells!
- ja: 逃げる赤血球を捕まえろ!
- zh-Hans: 抓住逃跑的红细胞!
- zh-Hant: 抓住逃跑的紅血球!
- es: ¡Atrapa a los glóbulos rojos que huyen!
- es-419: ¡Atrapa a los glóbulos rojos que escapan!
- fr: Attrape les globules rouges qui fuient!
- de: Fang die fliehenden roten Blutkörperchen!
- pt-BR: Pegue as hemácias que estão fugindo!
- ru: Лови убегающие эритроциты!
- pl: Złap uciekające czerwone krwinki!

### T.Stage5.Stage3.Line1

- ko: 항체로부터 도망쳐서 살아남아!
- en: Run from the antibodies and survive!
- ja: 抗体から逃げて生き残れ!
- zh-Hans: 躲开抗体活下去!
- zh-Hant: 躲開抗體活下去!
- es: ¡Huye de los anticuerpos y sobrevive!
- es-419: ¡Escapa de los anticuerpos y sobrevive!
- fr: Fuis les anticorps et survis!
- de: Flieh vor den Antikörpern und überlebe!
- pt-BR: Fuja dos anticorpos e sobreviva!
- ru: Беги от антител и выживи!
- pl: Uciekaj przed przeciwciałami i przetrwaj!

---

## T.Boss

### T.Boss.Intro.Line1

- ko: 식도 끝부분까지 왔어.
- en: We've reached the end of the esophagus.
- ja: 食道の終わりまで来た。
- zh-Hans: 已经到食道的尽头了。
- zh-Hant: 已經到食道的盡頭了。
- es: Hemos llegado al final del esófago.
- es-419: Llegamos al final del esófago.
- fr: On est arrivés au bout de l'œsophage.
- de: Wir sind am Ende der Speiseröhre angekommen.
- pt-BR: Chegamos ao fim do esôfago.
- ru: Мы дошли до конца пищевода.
- pl: Dotarliśmy do końca przełyku.

### T.Boss.Intro.Line2

- ko: 마지막이라서 그런가... 압박감이 다르네.
- en: Maybe because it's the last stretch... the pressure feels different.
- ja: 最後だからか…圧迫感が違うな。
- zh-Hans: 也许是因为是最后一段……这压迫感不一样。
- zh-Hant: 也許是因為是最後一段……這壓迫感不一樣。
- es: Quizá por ser el tramo final... la presión se siente distinta.
- es-419: Tal vez porque es el tramo final... la presión se siente distinta.
- fr: C'est peut-être parce que c'est la dernière ligne droite... la pression est différente.
- de: Vielleicht weil es das letzte Stück ist... der Druck fühlt sich anders an.
- pt-BR: Talvez por ser o último trecho... a pressão parece diferente.
- ru: Может, потому что это последний участок... давление ощущается иначе.
- pl: Może dlatego, że to ostatni odcinek... nacisk czuje się inaczej.

### T.Boss.Intro.Line3

- ko: 멈추지 말고 움직여!
- en: Don't stop, keep moving!
- ja: 止まらず動け!
- zh-Hans: 别停下，快动起来!
- zh-Hant: 別停下，快動起來!
- es: ¡No te detengas, sigue moviéndote!
- es-419: ¡No te detengas, sigue moviéndote!
- fr: Ne t'arrête pas, continue de bouger!
- de: Bleib nicht stehen, beweg dich weiter!
- pt-BR: Não pare, continue se movendo!
- ru: Не останавливайся, двигайся!
- pl: Nie zatrzymuj się, ruszaj się dalej!

### T.Boss.Bossdown.Line1

- ko: 조임이 멈췄다. 후... 결국 살았네.
- en: The squeezing stopped. Phew... we actually made it.
- ja: 締め付けが止まった。はぁ…結局生き残ったな。
- zh-Hans: 收缩停了。呼……总算活下来了。
- zh-Hant: 收縮停了。呼……總算活下來了。
- es: La opresión se detuvo. Uf... al final sobrevivimos.
- es-419: La presión se detuvo. Uf... al final sobrevivimos.
- fr: La compression s'est arrêtée. Ouf... on a fini par survivre.
- de: Die Enge hat aufgehört. Puh... wir haben es am Ende doch überlebt.
- pt-BR: O aperto parou. Ufa... no fim, sobrevivemos.
- ru: Сжатие прекратилось. Уф... в итоге мы выжили.
- pl: Ucisk ustał. Uff... w końcu przetrwaliśmy.

### T.Boss.Bossdown.Line2

- ko: 근데 바닥이 이상해...
- en: But something's off with the floor...
- ja: でも、床の様子がおかしい…
- zh-Hans: 不过，地板好像不太对……
- zh-Hant: 不過，地板好像不太對……
- es: Pero el suelo se ve raro...
- es-419: Pero el piso se ve raro...
- fr: Mais le sol a l'air bizarre...
- de: Aber mit dem Boden stimmt etwas nicht...
- pt-BR: Mas o chão está estranho...
- ru: Но с полом что-то не так...
- pl: Ale z podłogą jest coś nie tak...

### T.Boss.Bossdown.Line3

- ko: 무너지기 시작한다!
- en: It's starting to collapse!
- ja: 崩れ始める!
- zh-Hans: 开始崩塌了!
- zh-Hant: 開始崩塌了!
- es: ¡Empieza a derrumbarse!
- es-419: ¡Empieza a derrumbarse!
- fr: Il commence à s'effondrer!
- de: Er fängt an einzustürzen!
- pt-BR: Está começando a desabar!
- ru: Он начинает рушиться!
- pl: Zaczyna się zawalać!

---

## 적용 체크리스트 (사용자 — 에디터 작업)

1. `Window > Asset Management > Localization Tables`에서 `Dialogue` 컬렉션 열기.
2. `M.Stage1.Intro.Line1~4` 기존 4개 키 처리: 삭제 또는 `2.Tutorial` 씬 대사용으로 재배치 (위 "먼저 확인할 것" 참고).
3. 위 키 네이밍(예: `M.Stage3.Line1`)으로 각 스테이지 씬에 필요한 만큼 새 키 추가.
4. 각 언어 컬럼(en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl)에 이 문서의 값을 그대로 복사해 채우기. `ko` 컬럼도 이 문서 값으로 채워두면 이후 한국어도 String Table 경로로 통일 가능(지금은 씬 TMP에 직접 하드코딩하는 방식과 병행 중).
5. 각 스테이지 씬의 `Dialogue_Panel` 하위 TMP 오브젝트에 `LocalizeStringEvent` 부착 → `SetTable("Dialogue")` + `SetEntry(키)` → `OnUpdateString` → 해당 TMP `text`에 바인딩 (`M.Stage1` 파일럿과 동일 패턴, `SteamworksIntegrationDesign.md` 트랙4 §4 참고).
6. `DialogueUI.dialogueLines` 배열에 순서대로 연결.
7. Locale 전환(테스트용 `Application.systemLanguage` 또는 `GameLocalizationBootstrap`의 `Debug_Reapply`)으로 몇 개 언어 스모크 테스트.
