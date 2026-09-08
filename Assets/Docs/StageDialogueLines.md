# StageDialogueLines — M/T 스테이지 한국어 대사 SSOT

> 이 문서는 M/T 스테이지 `PhaseDialogueGate` / `DialogueUI`에 들어갈 한국어 대사 최종본이다.
> 다음 단계: 이 표를 기준으로 `Assets/Localization/StringTables/Dialogue_*.asset` (en/ja/zh-Hans/zh-Hant/es/es-419/fr/de/pt-BR/ru/pl)에 번역해 넣는다.
> 대사 자체는 게임 씬(.unity) `DialogueUI`의 `dialogueLines` TMP 텍스트에 직접 들어가는 내용이라, 여기 문서를 고쳐도 씬에는 자동 반영되지 않는다 — 씬 작업은 사용자가 에디터에서 직접 입력.

## 표기 규칙

- **키보드 키**: Ctrl/Space 등 조작 키는 영어로 통일하고 `<color=#FFD24D><b>Space</b></color>` 형식 TMP Rich Text 태그로 강조한다(`DialogueUI.cs` 상단 주석에 이미 동일 방식 사용 중 — `<color=#3B82F6><b>색</b></color>`). 아이콘(`Assets/UnityAssets/GameInputControllerIconsFree/keyboard/*`) 대체안도 가능하지만 TMP Sprite Asset 등록이 필요한 별도 에디터 작업이라, 필요 시 따로 요청.
- **줄바꿈**: 대사 한 항목(= TMP 화면 한 줄)에 문장이 2개 이상이면 문장 경계마다 실제 줄바꿈을 넣는다. 문장 수·내용은 그대로 두고 표시 위치만 나눔 — 씬 TMP 텍스트 입력 시에도 같은 지점에서 Enter로 나눠 넣을 것.

## 진행 순서 (스토리 흐름)

입(M) → 식도(T) → 위(시즌2, 미제작). M.Boss 클리어 시 "삼켜진다" → T.Stage로 연결. T.Boss 클리어는 위(胃)를 확정하지 않고 클리프행어로 마무리(시즌2 여지 보존).

---

## M.Stage1 (`DirectionalBarrier` 이동 + 소리 초출)

1. 왜 맛있는 과일들이랑 군것질들을 빼고 꿀떡을 먹은 거야!!!
2. 호랑이 굴에 들어가도 정신만 차리면 살 수 있다고 했어..!  
이제부터 생존만 생각해.
3. 주위에 있는 색깔 입들 위치를 잘 기억해둬.  
발판을 밟으면 그 색 입이 솟아올라서 이빨을 부숴줄 거야.
4. 한 번에 한 색만 올라와.  
다 같이 밟아봤자 소용없어.
5. 여기가 닫히려고 하면... 다 같이 팀 구호를 외쳐!  
안 그러면 앞이 안 보이게 될 거야.

*(색 패드/응원 설명은 `2.Tutorial` 씬 담당 — M.Stage1에서 재설명하지 않음. **단, 은신(Stealth)은 예외** — Tutorial 구역 1이 생략 가능인 데다 실사용처가 `M.Stage2`(2.2)·`M.Stage4`(4.3)·`M.Boss`(1페이즈)뿐으로 희소해서, Tutorial 단독 교육을 신뢰하지 않고 **첫 실사용 스테이지(`M.Stage2` 2.2)에서 다시 짚어준다**(아래 참고). `CheerAndTutorialDesign.md` §4 참고.)*

*(3번 "색깔 입" = `DirectionalBarrier`/`MouthBarrier` 프리팹(`DoorController`). 5번 "여기" = `MouthController`(방 전체 닫힘, 소리 초출) — 서로 다른 단어로 분리해 헷갈리지 않게 함.)*

## M.Stage2 (`SideSplitChallenge` — 2.1 SideSplit+침 / 2.2 Drop+침)

**2.1 — SideSplit 첫 등장 + 침 초출**
1. 화면에 표시된 대로 안전 구역에 들어가면 돼!
2. 날 먹기 전에 매운 걸 먹었나..?  
바닥에 침들이 자꾸 올라와.
3. 바닥에 침이 깔려 미끄러울 거야.  
다 같이 팀 구호를 외치면 없앨 수 있어.

**2.2 — 은신 첫 등장 (Drop+침 구간, `TrapPlayerTracker` 최초 사용)**
1. 이제는 위에서도 침이 떨어지잖아!  
매운 음식을 먹고 신 음식까지 먹은 게 분명해.  
꿀떡으로 중화하려는 거야, 분명!
2. 떨어지는 침 중에 날 조준하는 게 있는데, <color=#FFD24D><b>Ctrl</b></color>로 바닥 색이랑 내 색을 맞추면 조준이 풀릴 거야.

## M.Stage3 (`ColorTileChallenge` — 점수제)

1. 바닥에 색깔 타일이 뜰 거야.  
그 위에 2초간 서 있으면 점수가 올라가.
2. 흰색이나 검은색 타일은 누구든 밟아도 되지만, 고유색 타일은 그 색만 밟아야 점수가 올라가.  
길 막지 말고 빨리 움직여!

## M.Stage4 (`SequenceRingMinigame` — 4.1 턴제+혀 초출 / 4.2 난이도업 / 4.3 은신+혀 복습)

**4.1 — SequenceRing 최초 룰 설명 + 혀 초출**
1. 네 색이 바닥에 뜨면 <color=#FFD24D><b>Space</b></color>를 눌러.  
흰색은 아무나 눌러도 되고, 검은색은 절대 누르면 안 돼!  
1초 뒤에 자동으로 다음 차례로 넘어가.
2. 발 밑에서 뭔가 움직이고 있어..조심해!  
혀 공격이 들어온다!
3. 가운데서 혀가 솟아올라서 반대편이 안 보이게 가리고 길을 막을 거야.  
다 같이 팀 구호를 외치면 다시 내려가고, 부서진 길도 원상복구 될 거야.

**4.2 — 난이도업**
1. 이제부터는 한 칸 앞 바닥만 보여줄 거야.  
잘 기억해둬!

**4.3 — 은신 복습 + 혀 복습(바닥 파괴)**
1. 여기도 날 조준하는 게 있어.  
<color=#FFD24D><b>Ctrl</b></color>로 바닥 색이랑 내 색을 맞추면 조준이 풀려.
2. 이번엔 혀가 왼쪽이나 오른쪽 바닥을 통째로 부술 거야.  
팀 구호를 외치면 막고, 못 외치면 그대로 부서져.
3. 경고가 떠 있을 때 팀 구호를 외쳐야만 원상복구가 돼.  
바닥이 부서지고 나면, 다음 경고가 뜰 때까지는 버티는 수밖에 없어.

## M.Stage5 (GridColorChallenge / GridBWTileChallenge)

1. 운동하고 있나 봐!  
숨이 갑자기 거칠어졌어.  
바람이 불어온다!  
맞는 바닥 위에 올라가서 버텨!

*(Color/BW 모드 공용 — BW 모드용 별도 문구 없음.)*

## M.Boss (BossFightObjective — 몬스터 없음, 스테이지 함정 자체가 보스)

**Intro**
1. 후아... 후아... 조금만 더 버티면 살아서 나갈 수 있을 것 같아...

**Bossdown**
1. 이 정적... 입 안이 조용해졌다.  
드디어 탈출할 수 있는 건가...?
2. .....(콰직!)
3. 이런, 삼켜진다...!!

*(2번 대사 직후 바닥이 부서지는 연출 — 3번 대사와 동시에 T.Stage로 전환)*

---

# M-Stage 번역 (Localization Draft)

> 번역 규칙: **1)** 언어별 문법에 맞게, **2)** 그 언어권에서 실제 쓰는 말투로, **3)** "꿀떡"은 전 언어 공통으로 로마자 표기 `KKUL-TTEOK` 그대로 사용(한국 고유 음식이라 의미 번역 대신 음차 유지 — 필요하면 별도로 "한국식 꿀떡(honey rice cake)" 주석을 붙일 것). 키 강조 태그(`<color=#FFD24D><b>Space</b></color>` 등)와 줄바꿈 규칙은 모든 언어 동일 적용.
>
> **속담(M.Stage1-2)은 언어별 관용구로 교체하지 않고, "호랑이 굴에 들어가도 정신만 차리면 산다"를 전 언어 공통으로 직역**한다 — 한국 속담 자체를 보여주는 것이 목적.
>
> ⚠️ 11개 언어 전체를 1차로 초안 작성한 것으로, 특히 JA/ZH/RU/PL은 원어민 검수 권장.

### EN

**M.Stage1**
1. Why'd you skip all the tasty fruit and snacks and eat a KKUL-TTEOK instead?!
2. They say you can survive even a tiger's den if you keep your wits about you..!  
From now on, just focus on staying alive.
3. Remember where the colored mouths around you are.  
Step on the pad, and that color's mouth will rise up and smash the teeth.
4. Only one color rises at a time.  
Stepping on it together won't help.
5. If this place starts to close... everyone shout the team cheer!  
Otherwise you won't be able to see a thing.

**M.Stage2 — 2.1**
1. Get into the safe zone shown on screen!
2. Did it eat something spicy before swallowing me..?  
Drool keeps rising up from the floor.
3. The floor's covered in slippery drool.  
Everyone shout the team cheer together to clear it.

**M.Stage2 — 2.2**
1. Now it's dripping from above too!  
It must've eaten something spicy, then something sour on top of that.  
It's definitely trying to neutralize it with a KKUL-TTEOK!
2. Some of the falling drops are targeting me — press <color=#FFD24D><b>Ctrl</b></color> to match the floor color to your own and break the lock-on.

**M.Stage3**
1. Colored tiles will appear on the floor.  
Stand on one for 2 seconds and your score goes up.
2. Anyone can step on white or black tiles, but only your own color counts on unique-color tiles.  
Don't block the way — move fast!

**M.Stage4 — 4.1**
1. Press <color=#FFD24D><b>Space</b></color> when your color lights up on the floor.  
Anyone can press for white, but never press for black!  
After 1 second, it moves to the next turn automatically.
2. Something's moving under our feet..watch out!  
It's a tongue attack!
3. A tongue will shoot up from the center, blocking your view of the other side and the path.  
Everyone shout the team cheer to bring it back down and repair the broken path.

**M.Stage4 — 4.2**
1. From now on, it'll only show you one tile ahead.  
Remember it well!

**M.Stage4 — 4.3**
1. There's something targeting me here too.  
Press <color=#FFD24D><b>Ctrl</b></color> to match the floor color to your own and break the lock-on.
2. This time the tongue will smash the entire left or right floor.  
Shout the team cheer to block it — if you don't, it'll just break.
3. It only repairs if you shout the team cheer while the warning is up.  
Once the floor breaks, you'll just have to hold out until the next warning.

**M.Stage5**
1. Feels like it's working out!  
Its breathing suddenly got rough.  
Here comes the wind!  
Get on the matching tile and hold your ground!

**M.Boss — Intro**
1. Haah... haah... just a little more and we might actually make it out alive...

**M.Boss — Bossdown**
1. This silence... it's gone quiet in here.  
Does that mean we can finally escape...?
2. .....(CRUNCH!)
3. Oh no, we're being swallowed...!!

### JA

**M.Stage1**
1. 美味しい果物やお菓子を差し置いて、なんでKKUL-TTEOKを食べちゃったんだよ!!!
2. 虎穴に入るとも、冷静でいれば生き延びられるって言うだろ..!  
これからは生き残ることだけ考えよう。
3. 周りにある色付きの口の位置、よく覚えておいて。  
足場を踏むと、その色の口がせり上がって歯を砕いてくれるよ。
4. 一度に上がるのは一色だけ。  
みんなで踏んでも意味ないよ。
5. ここが閉じそうになったら…みんなでチームの掛け声を叫んで!  
じゃないと、前が見えなくなっちゃうよ。

**M.Stage2 — 2.1**
1. 画面に表示された通り、安全エリアに入って!
2. 食べる前に辛いものでも食べたのかな..?  
床からよだれがどんどん湧いてくる。
3. 床によだれが広がって滑りやすくなってる。  
みんなでチームの掛け声を叫べば消せるよ。

**M.Stage2 — 2.2**
1. 今度は上からもよだれが落ちてくる!  
辛いもの食べて、酸っぱいものまで食べたに違いない。  
KKUL-TTEOKで中和しようとしてるんだ、絶対!
2. 落ちてくるよだれの中には私を狙ってくるものもあって、<color=#FFD24D><b>Ctrl</b></color>で床の色と自分の色を合わせればロックオンが外れるよ。

**M.Stage3**
1. 床に色付きのタイルが出現するよ。  
その上に2秒立っていればスコアが上がる。
2. 白や黒のタイルは誰が踏んでもいいけど、固有色のタイルはその色じゃないとスコアが上がらないよ。  
道をふさがずに素早く動いて!

**M.Stage4 — 4.1**
1. 自分の色が床に出たら<color=#FFD24D><b>Space</b></color>を押して。  
白は誰が押してもいいけど、黒は絶対に押しちゃダメ!  
1秒後には自動で次の番に進むよ。
2. 足元で何かが動いてる…気をつけて!  
舌の攻撃が来る!
3. 中央から舌がせり上がって、向こう側が見えなくなって道もふさがれる。  
みんなでチームの掛け声を叫べば元に戻って、壊れた道も直るよ。

**M.Stage4 — 4.2**
1. これからは1マス先しか見えなくなるよ。  
しっかり覚えておいて!

**M.Stage4 — 4.3**
1. ここにも私を狙ってくるものがある。  
<color=#FFD24D><b>Ctrl</b></color>で床の色と自分の色を合わせればロックオンが外れるよ。
2. 今度は舌が左か右の床を丸ごと壊すよ。  
チームの掛け声で防げるけど、防げないとそのまま壊れる。
3. 警告が出ている間にチームの掛け声を叫ばないと元に戻らないよ。  
床が壊れたら、次の警告が出るまで持ちこたえるしかない。

**M.Stage5**
1. 運動でもしてるのかな!  
急に息が荒くなった。  
風が吹いてくる!  
合ってるタイルに乗って耐えて!

**M.Boss — Intro**
1. はあ…はあ…もう少し耐えれば、生きて出られそうだ…

**M.Boss — Bossdown**
1. この静けさ…口の中が静かになった。  
やっと脱出できるってこと…?
2. …………(ガキッ!)
3. まずい、飲み込まれる…!!

### ZH-Hans

**M.Stage1**
1. 你怎么放着好吃的水果和零食不吃,偏偏吃了个KKUL-TTEOK!!!
2. 常言道,就算误入虎穴,只要保持镇定就能活下来..!  
从现在起,只想着怎么活下去。
3. 记好周围那些彩色嘴巴的位置。  
踩上踏板,那个颜色的嘴巴就会升起来,把牙齿砸碎。
4. 一次只会升起一种颜色。  
大家一起踩也没用。
5. 这里要是快关上了……大家一起喊团队口号!  
不然眼前就会变得一片漆黑。

**M.Stage2 — 2.1**
1. 按照画面提示进入安全区!
2. 吃我之前是不是吃了辣的..?  
地上的口水不停地往上冒。
3. 地上全是口水,很滑。  
大家一起喊团队口号就能清掉。

**M.Stage2 — 2.2**
1. 现在上面也开始滴口水了!  
肯定是吃了辣的又吃了酸的。  
一定是想用KKUL-TTEOK来中和!
2. 掉下来的口水里有的会瞄准我,用<color=#FFD24D><b>Ctrl</b></color>把地板颜色和自己的颜色对上就能解除锁定。

**M.Stage3**
1. 地上会出现彩色瓷砖。  
在上面站2秒分数就会增加。
2. 白色和黑色瓷砖谁踩都行,但专属颜色瓷砖只有对应颜色才能加分。  
别挡路,动作快点!

**M.Stage4 — 4.1**
1. 你的颜色出现在地上时按<color=#FFD24D><b>Space</b></color>。  
白色谁按都行,黑色绝对不能按!  
1秒后会自动轮到下一个人。
2. 脚下好像有什么在动……小心!  
舌头攻击来了!
3. 舌头会从中间升起来,挡住对面的视线,也会堵住路。  
大家一起喊团队口号就能让它降下去,损坏的路也会恢复原状。

**M.Stage4 — 4.2**
1. 从现在起只会显示前面一格。  
要好好记住!

**M.Stage4 — 4.3**
1. 这里也有瞄准我的东西。  
用<color=#FFD24D><b>Ctrl</b></color>把地板颜色和自己的颜色对上就能解除锁定。
2. 这次舌头会把左边或右边的地板整片砸碎。  
喊团队口号就能挡住,喊不出来就会直接碎掉。
3. 只有在警告出现的时候喊团队口号才能恢复原状。  
地板碎了之后,只能撑到下一次警告出现。

**M.Stage5**
1. 好像在运动!  
呼吸突然变粗重了。  
风吹过来了!  
站到对应的地板上撑住!

**M.Boss — Intro**
1. 呼……呼……再撑一下,好像就能活着出去了……

**M.Boss — Bossdown**
1. 这寂静……嘴里安静下来了。  
难道终于能逃出去了……?
2. .....(咔嚓!)
3. 糟了,要被吞下去了……!!

### ZH-Hant

**M.Stage1**
1. 你怎麼放着好吃的水果和零食不吃,偏偏吃了個KKUL-TTEOK啊!!!
2. 常言道,就算誤入虎穴,只要保持鎮定就能活下來..!  
從現在起,只想著怎麼活下去。
3. 記好周圍那些彩色嘴巴的位置。  
踩上踏板,那個顏色的嘴巴就會升起來,把牙齒砸碎。
4. 一次只會升起一種顏色。  
大家一起踩也沒用。
5. 這裡要是快關上了……大家一起喊團隊口號!  
不然眼前就會變得一片漆黑。

**M.Stage2 — 2.1**
1. 照畫面提示進入安全區!
2. 吃我之前是不是吃了辣的..?  
地上的口水一直冒出來。
3. 地上都是口水,很滑。  
大家一起喊團隊口號就能清掉。

**M.Stage2 — 2.2**
1. 現在上面也開始滴口水了!  
肯定是吃了辣的又吃了酸的。  
一定是想用KKUL-TTEOK來中和!
2. 掉下來的口水裡有的會瞄準我,用<color=#FFD24D><b>Ctrl</b></color>把地板顏色和自己的顏色對上就能解除鎖定。

**M.Stage3**
1. 地上會出現彩色磁磚。  
站在上面2秒分數就會增加。
2. 白色和黑色磁磚誰踩都行,但專屬顏色磁磚只有對應顏色才能加分。  
別擋路,動作快點!

**M.Stage4 — 4.1**
1. 你的顏色出現在地上時按<color=#FFD24D><b>Space</b></color>。  
白色誰按都行,黑色絕對不能按!  
1秒後會自動輪到下一個人。
2. 腳下好像有什麼在動……小心!  
舌頭攻擊來了!
3. 舌頭會從中間升起來,擋住對面的視線,也會堵住路。  
大家一起喊團隊口號就能讓它降下去,壞掉的路也會恢復原狀。

**M.Stage4 — 4.2**
1. 從現在起只會顯示前面一格。  
要好好記住!

**M.Stage4 — 4.3**
1. 這裡也有瞄準我的東西。  
用<color=#FFD24D><b>Ctrl</b></color>把地板顏色和自己的顏色對上就能解除鎖定。
2. 這次舌頭會把左邊或右邊的地板整片砸碎。  
喊團隊口號就能擋住,喊不出來就會直接碎掉。
3. 只有在警告出現的時候喊團隊口號才能恢復原狀。  
地板碎了之後,只能撐到下一次警告出現。

**M.Stage5**
1. 好像在運動!  
呼吸突然變粗重了。  
風吹過來了!  
站到對應的地板上撐住!

**M.Boss — Intro**
1. 呼……呼……再撐一下,好像就能活著出去了……

**M.Boss — Bossdown**
1. 這寂靜……嘴裡安靜下來了。  
難道終於能逃出去了……?
2. .....(喀嚓!)
3. 糟了,要被吞下去了……!!

### ES

**M.Stage1**
1. ¡¿Por qué comiste un KKUL-TTEOK en vez de toda la fruta y las chuches tan ricas que había?!
2. Dicen que hasta en la cueva del tigre se sobrevive si mantienes la cabeza fría..!  
A partir de ahora, pensad solo en sobrevivir.
3. Acordaos bien de dónde están las bocas de colores de alrededor.  
Si pisáis la palanca, la boca de ese color se alza y os rompe los dientes.
4. Solo sube un color cada vez.  
No sirve de nada pisarla todos a la vez.
5. Si esto empieza a cerrarse... ¡gritad todos el grito de equipo!  
Si no, os quedaréis sin ver nada.

**M.Stage2 — 2.1**
1. ¡Entrad en la zona segura que marca la pantalla!
2. ¿Habrá comido algo picante antes de tragarme..?  
No paran de salir babas del suelo.
3. El suelo está lleno de babas resbaladizas.  
Gritad todos el grito de equipo para quitarlas.

**M.Stage2 — 2.2**
1. ¡Ahora también caen babas desde arriba!  
Seguro que ha comido algo picante y encima algo ácido.  
¡Está claro que quiere neutralizarlo con un KKUL-TTEOK!
2. Algunas de las babas que caen me apuntan a mí: pulsad <color=#FFD24D><b>Ctrl</b></color> para igualar el color del suelo con el vuestro y que se desactive el bloqueo.

**M.Stage3**
1. Aparecerán baldosas de colores en el suelo.  
Quedaos 2 segundos encima de una y subirá la puntuación.
2. Las baldosas blancas o negras las puede pisar cualquiera, pero las de color único solo suman puntos si las pisa ese color.  
¡No bloqueéis el paso, moveos rápido!

**M.Stage4 — 4.1**
1. Cuando aparezca tu color en el suelo, pulsa <color=#FFD24D><b>Space</b></color>.  
Al blanco puede darle cualquiera, ¡pero al negro no le déis nunca!  
Después de 1 segundo pasará automáticamente al siguiente turno.
2. Algo se mueve bajo nuestros pies... ¡cuidado!  
¡Viene un ataque de lengua!
3. Del centro se alzará una lengua que tapará la vista del otro lado y bloqueará el camino.  
Gritad todos el grito de equipo para que baje de nuevo y se repare el camino roto.

**M.Stage4 — 4.2**
1. A partir de ahora solo se verá una baldosa por delante.  
¡Memorizadla bien!

**M.Stage4 — 4.3**
1. Aquí también hay algo que me apunta.  
Pulsad <color=#FFD24D><b>Ctrl</b></color> para igualar el color del suelo con el vuestro y desactivar el bloqueo.
2. Esta vez la lengua destrozará todo el suelo de la izquierda o de la derecha.  
Gritad el grito de equipo para bloquearla; si no lo hacéis, se romperá sin más.
3. Solo se repara si gritáis el grito de equipo mientras esté activo el aviso.  
Una vez roto el suelo, no queda más remedio que aguantar hasta el próximo aviso.

**M.Stage5**
1. ¡Parece que está haciendo ejercicio!  
De repente respira con más fuerza.  
¡Viene viento!  
¡Subid a la baldosa correcta y aguantad!

**M.Boss — Intro**
1. Fiu... fiu... con un poco más de aguante, parece que podremos salir con vida...

**M.Boss — Bossdown**
1. Este silencio... la boca se ha quedado callada.  
¿Será que por fin podemos escapar...?
2. .....(¡CRAC!)
3. No puede ser, ¡nos está tragando...!!

### ES-419

**M.Stage1**
1. ¡¿Por qué comiste un KKUL-TTEOK en lugar de toda la fruta y los dulces tan ricos que había?!
2. Dicen que hasta en la cueva del tigre se puede sobrevivir si mantienes la cabeza fría..!  
De ahora en adelante, piensen solo en sobrevivir.
3. Recuerden bien dónde están las bocas de colores que hay alrededor.  
Si pisan la palanca, la boca de ese color se levanta y les rompe los dientes.
4. Solo se levanta un color a la vez.  
No sirve de nada pisarla todos juntos.
5. Si esto empieza a cerrarse... ¡griten todos el grito de equipo!  
Si no, se van a quedar sin ver nada.

**M.Stage2 — 2.1**
1. ¡Entren a la zona segura que marca la pantalla!
2. ¿Habrá comido algo picante antes de tragarme..?  
No paran de salir babas del piso.
3. El piso está lleno de babas resbalosas.  
Griten todos el grito de equipo para quitarlas.

**M.Stage2 — 2.2**
1. ¡Ahora también caen babas desde arriba!  
Seguro que comió algo picante y encima algo ácido.  
¡Está clarísimo que quiere neutralizarlo con un KKUL-TTEOK!
2. Algunas de las babas que caen me apuntan a mí: presionen <color=#FFD24D><b>Ctrl</b></color> para igualar el color del piso con el suyo y desactivar el bloqueo.

**M.Stage3**
1. Van a aparecer baldosas de colores en el piso.  
Quédense 2 segundos encima de una y sube el puntaje.
2. Las baldosas blancas o negras las puede pisar cualquiera, pero las de color único solo suman puntos si las pisa ese color.  
¡No bloqueen el paso, muévanse rápido!

**M.Stage4 — 4.1**
1. Cuando aparezca tu color en el piso, presiona <color=#FFD24D><b>Space</b></color>.  
Al blanco le puede dar cualquiera, ¡pero al negro no le den nunca!  
Después de 1 segundo pasa automáticamente al siguiente turno.
2. Algo se mueve bajo nuestros pies... ¡cuidado!  
¡Viene un ataque de lengua!
3. Del centro va a salir una lengua que tapa la vista del otro lado y bloquea el camino.  
Griten todos el grito de equipo para que baje de nuevo y se repare el camino roto.

**M.Stage4 — 4.2**
1. De ahora en adelante solo se va a ver una baldosa más adelante.  
¡Memorícenla bien!

**M.Stage4 — 4.3**
1. Aquí también hay algo que me apunta.  
Presionen <color=#FFD24D><b>Ctrl</b></color> para igualar el color del piso con el suyo y desactivar el bloqueo.
2. Esta vez la lengua va a destrozar todo el piso de la izquierda o de la derecha.  
Griten el grito de equipo para bloquearla; si no lo hacen, se rompe nomás.
3. Solo se repara si gritan el grito de equipo mientras esté activa la advertencia.  
Una vez que se rompe el piso, no queda otra que aguantar hasta la próxima advertencia.

**M.Stage5**
1. ¡Parece que está haciendo ejercicio!  
De repente empezó a respirar más fuerte.  
¡Viene viento!  
¡Súbanse a la baldosa correcta y aguanten!

**M.Boss — Intro**
1. Fiu... fiu... con un poco más de aguante, parece que vamos a poder salir con vida...

**M.Boss — Bossdown**
1. Este silencio... la boca se quedó callada.  
¿Será que por fin podemos escapar...?
2. .....(¡CRAC!)
3. No puede ser, ¡nos está tragando...!!

### FR

**M.Stage1**
1. Pourquoi tu as mangé un KKUL-TTEOK au lieu de tous ces bons fruits et bonbons !!!
2. On dit qu'on peut survivre même dans la tanière du tigre si on garde la tête froide..!  
À partir de maintenant, pensons juste à survivre.
3. Souvenez-vous bien de l'emplacement des bouches colorées autour de vous.  
Marchez sur la dalle, et la bouche de cette couleur se soulèvera pour briser les dents.
4. Une seule couleur monte à la fois.  
Ça ne sert à rien de marcher dessus tous ensemble.
5. Si ça commence à se refermer... criez tous le cri d'équipe !  
Sinon, vous ne verrez plus rien devant vous.

**M.Stage2 — 2.1**
1. Entrez dans la zone sûre indiquée à l'écran !
2. Il a mangé épicé avant de m'avaler, ou quoi..?  
La bave n'arrête pas de monter du sol.
3. Le sol est couvert de bave glissante.  
Criez tous le cri d'équipe pour l'effacer.

**M.Stage2 — 2.2**
1. Maintenant ça dégouline même d'en haut !  
Il a sûrement mangé épicé, et en plus quelque chose d'acide.  
Il essaie clairement de neutraliser ça avec un KKUL-TTEOK !
2. Certaines gouttes qui tombent me visent : appuyez sur <color=#FFD24D><b>Ctrl</b></color> pour assortir la couleur du sol à la vôtre et débloquer le ciblage.

**M.Stage3**
1. Des dalles colorées vont apparaître au sol.  
Restez dessus 2 secondes et le score augmente.
2. Les dalles blanches ou noires, n'importe qui peut les prendre, mais les dalles de couleur unique ne comptent que pour cette couleur.  
Ne bloquez pas le passage, bougez vite !

**M.Stage4 — 4.1**
1. Quand ta couleur apparaît au sol, appuie sur <color=#FFD24D><b>Space</b></color>.  
N'importe qui peut appuyer pour le blanc, mais jamais pour le noir !  
Après 1 seconde, ça passe automatiquement au tour suivant.
2. Quelque chose bouge sous nos pieds... attention !  
Une attaque de langue arrive !
3. Une langue va surgir du centre, bloquant la vue de l'autre côté et le passage.  
Criez tous le cri d'équipe pour la faire redescendre et réparer le chemin détruit.

**M.Stage4 — 4.2**
1. À partir de maintenant, une seule dalle en avance sera visible.  
Mémorisez-la bien !

**M.Stage4 — 4.3**
1. Ici aussi, quelque chose me vise.  
Appuyez sur <color=#FFD24D><b>Ctrl</b></color> pour assortir la couleur du sol à la vôtre et débloquer le ciblage.
2. Cette fois, la langue va détruire tout le sol de gauche ou de droite.  
Criez le cri d'équipe pour la bloquer ; sinon, ça se casse tout simplement.
3. Ça ne se répare que si vous criez le cri d'équipe pendant que l'alerte est active.  
Une fois le sol détruit, il faudra tenir jusqu'à la prochaine alerte.

**M.Stage5**
1. On dirait qu'il fait de l'exercice !  
Sa respiration est devenue soudainement plus forte.  
Le vent arrive !  
Montez sur la bonne dalle et tenez bon !

**M.Boss — Intro**
1. Hah... hah... encore un peu, et on devrait pouvoir sortir vivants...

**M.Boss — Bossdown**
1. Ce silence... c'est devenu calme à l'intérieur.  
Est-ce qu'on va enfin pouvoir s'échapper... ?
2. .....(CRAC !)
3. Non, on se fait avaler... !!

### DE

**M.Stage1**
1. Warum hast du all die leckeren Früchte und Snacks liegen lassen und stattdessen ein KKUL-TTEOK gegessen?!
2. Man sagt, selbst in der Höhle des Tigers überlebt man, wenn man einen kühlen Kopf bewahrt..!  
Von jetzt an denken wir nur ans Überleben.
3. Merkt euch gut, wo die bunten Mäuler um euch herum sind.  
Tretet auf das Pedal, dann fährt das Maul dieser Farbe hoch und zertrümmert die Zähne.
4. Es fährt immer nur eine Farbe gleichzeitig hoch.  
Es bringt nichts, wenn ihr alle zusammen draufsteht.
5. Wenn sich das hier zu schließen beginnt... ruft alle zusammen den Teamruf!  
Sonst seht ihr bald nichts mehr.

**M.Stage2 — 2.1**
1. Geht in die Sicherheitszone, die auf dem Bildschirm angezeigt wird!
2. Hat es vor dem Verschlucken irgendwas Scharfes gegessen..?  
Vom Boden steigt ständig Speichel auf.
3. Der Boden ist voller rutschigem Speichel.  
Ruft alle zusammen den Teamruf, um ihn zu beseitigen.

**M.Stage2 — 2.2**
1. Jetzt tropft es sogar von oben!  
Es hat bestimmt Scharfes und dazu noch Saures gegessen.  
Es will das eindeutig mit einem KKUL-TTEOK neutralisieren!
2. Manche der herabfallenden Tropfen zielen auf mich – drückt <color=#FFD24D><b>Ctrl</b></color>, um die Bodenfarbe an eure eigene anzupassen und die Zielerfassung zu lösen.

**M.Stage3**
1. Auf dem Boden erscheinen bunte Kacheln.  
Steht 2 Sekunden darauf, dann steigt der Punktestand.
2. Weiße oder schwarze Kacheln darf jeder betreten, aber bei Kacheln mit einer eigenen Farbe zählt nur diese Farbe.  
Blockiert den Weg nicht, bewegt euch schnell!

**M.Stage4 — 4.1**
1. Wenn deine Farbe auf dem Boden aufleuchtet, drück <color=#FFD24D><b>Space</b></color>.  
Bei Weiß darf jeder drücken, aber bei Schwarz auf keinen Fall!  
Nach 1 Sekunde geht es automatisch zum nächsten Zug über.
2. Unter unseren Füßen bewegt sich etwas... Vorsicht!  
Ein Zungenangriff kommt!
3. In der Mitte schießt eine Zunge hoch, die die Sicht auf die andere Seite versperrt und den Weg blockiert.  
Ruft alle zusammen den Teamruf, damit sie wieder runtergeht und der zerstörte Weg repariert wird.

**M.Stage4 — 4.2**
1. Von jetzt an wird nur noch eine Kachel im Voraus angezeigt.  
Merkt sie euch gut!

**M.Stage4 — 4.3**
1. Auch hier zielt etwas auf mich.  
Drückt <color=#FFD24D><b>Ctrl</b></color>, um die Bodenfarbe an eure eigene anzupassen und die Zielerfassung zu lösen.
2. Diesmal zerstört die Zunge den ganzen Boden links oder rechts.  
Ruft den Teamruf, um sie zu blockieren – wenn nicht, bricht er einfach zusammen.
3. Er wird nur repariert, wenn ihr den Teamruf ruft, während die Warnung aktiv ist.  
Ist der Boden erst zerstört, müsst ihr bis zur nächsten Warnung durchhalten.

**M.Stage5**
1. Sieht aus, als würde es trainieren!  
Der Atem ist plötzlich schwerer geworden.  
Der Wind kommt!  
Stellt euch auf die passende Kachel und haltet durch!

**M.Boss — Intro**
1. Haah... haah... noch ein bisschen durchhalten, dann schaffen wir es vielleicht lebend raus...

**M.Boss — Bossdown**
1. Diese Stille... es ist ruhig geworden hier drin.  
Heißt das, wir können endlich entkommen...?
2. .....(KRACH!)
3. Oh nein, wir werden verschluckt...!!

### PT-BR

**M.Stage1**
1. Por que você comeu um KKUL-TTEOK e deixou passar todas aquelas frutas e guloseimas gostosas?!
2. Dizem que até no covil do tigre dá pra sobreviver se você mantiver a cabeça fria..!  
A partir de agora, só pense em sobreviver.
3. Decore bem onde estão as bocas coloridas ao redor.  
Se pisar no pedal, a boca daquela cor sobe e quebra os dentes.
4. Só sobe uma cor por vez.  
Não adianta todo mundo pisar junto.
5. Se isso aqui começar a fechar... gritem todos juntos o grito do time!  
Senão, vocês vão ficar sem enxergar nada.

**M.Stage2 — 2.1**
1. Entrem na zona segura marcada na tela!
2. Será que comeu algo apimentado antes de me engolir..?  
Não para de sair baba do chão.
3. O chão está cheio de baba escorregadia.  
Gritem todos juntos o grito do time pra sumir com ela.

**M.Stage2 — 2.2**
1. Agora tá pingando baba de cima também!  
Com certeza comeu algo apimentado e ainda por cima algo ácido.  
Só pode estar tentando neutralizar com um KKUL-TTEOK!
2. Algumas das gotas que caem estão mirando em mim: aperta <color=#FFD24D><b>Ctrl</b></color> pra combinar a cor do chão com a sua e travar a mira.

**M.Stage3**
1. Vão aparecer blocos coloridos no chão.  
Fique 2 segundos em cima e a pontuação sobe.
2. Blocos brancos ou pretos qualquer um pode pisar, mas os de cor única só somam ponto se for daquela cor.  
Não bloqueia o caminho, se mexe rápido!

**M.Stage4 — 4.1**
1. Quando sua cor aparecer no chão, aperta <color=#FFD24D><b>Space</b></color>.  
No branco qualquer um pode apertar, mas no preto nunca aperte!  
Depois de 1 segundo passa automático pro próximo turno.
2. Tem algo se mexendo debaixo dos nossos pés... cuidado!  
Vem um ataque de língua!
3. Uma língua vai subir do centro, tampando a visão do outro lado e bloqueando o caminho.  
Gritem todos juntos o grito do time pra ela descer de novo e o caminho quebrado se consertar.

**M.Stage4 — 4.2**
1. A partir de agora só vai mostrar um bloco à frente.  
Decorem bem!

**M.Stage4 — 4.3**
1. Aqui também tem algo mirando em mim.  
Aperta <color=#FFD24D><b>Ctrl</b></color> pra combinar a cor do chão com a sua e travar a mira.
2. Dessa vez a língua vai destruir o chão inteiro da esquerda ou da direita.  
Gritem o grito do time pra bloquear; se não conseguirem, ela quebra do mesmo jeito.
3. Só conserta se vocês gritarem o grito do time enquanto o aviso estiver ativo.  
Depois que o chão quebra, só resta aguentar até o próximo aviso.

**M.Stage5**
1. Parece que tá se exercitando!  
A respiração ficou pesada do nada.  
O vento tá vindo!  
Suba no bloco certo e aguenta firme!

**M.Boss — Intro**
1. Haah... haah... com mais um pouco de esforço, parece que a gente sai vivo daqui...

**M.Boss — Bossdown**
1. Esse silêncio... ficou tudo quieto aqui dentro.  
Será que finalmente dá pra escapar...?
2. .....(CRAC!)
3. Não, estamos sendo engolidos...!!

### RU

**M.Stage1**
1. Почему ты съел KKUL-TTEOK, а не всю эту вкусную фрукту и сладости?!
2. Говорят, даже в логове тигра можно выжить, если не терять голову..!  
Теперь думаем только о том, как выжить.
3. Хорошенько запомните, где находятся цветные рты вокруг.  
Наступишь на плиту — рот того цвета поднимется и раздробит зубы.
4. За раз поднимается только один цвет.  
Наступать всем вместе бесполезно.
5. Если это начнёт закрываться... кричите все вместе командный клич!  
Иначе вы больше ничего не увидите.

**M.Stage2 — 2.1**
1. Заходите в безопасную зону, как показано на экране!
2. Оно что, съело что-то острое перед тем, как проглотить меня..?  
С пола постоянно поднимается слюна.
3. Пол залит скользкой слюной.  
Кричите все вместе командный клич, чтобы её убрать.

**M.Stage2 — 2.2**
1. Теперь слюна капает ещё и сверху!  
Точно съело что-то острое, а потом ещё и кислое.  
Явно пытается нейтрализовать это KKUL-TTEOK!
2. Некоторые из падающих капель целятся в меня — нажми <color=#FFD24D><b>Ctrl</b></color>, чтобы совпасть цветом пола со своим и снять захват цели.

**M.Stage3**
1. На полу появятся цветные плитки.  
Постой на ней 2 секунды — очки вырастут.
2. На белые и чёрные плитки может вставать кто угодно, а на плитки уникального цвета — только соответствующий цвет.  
Не загораживайте путь, двигайтесь быстрее!

**M.Stage4 — 4.1**
1. Когда твой цвет появится на полу, нажми <color=#FFD24D><b>Space</b></color>.  
На белый может нажать кто угодно, а на чёрный — ни в коем случае!  
Через 1 секунду ход автоматически перейдёт к следующему.
2. Что-то шевелится у нас под ногами... осторожно!  
Сейчас будет атака языком!
3. Из центра поднимется язык, закроет обзор на другую сторону и перекроет путь.  
Кричите все вместе командный клич, чтобы он опустился, а разрушенный путь восстановился.

**M.Stage4 — 4.2**
1. Теперь будет видна только одна плитка вперёд.  
Хорошенько запоминайте!

**M.Stage4 — 4.3**
1. Здесь тоже что-то целится в меня.  
Нажми <color=#FFD24D><b>Ctrl</b></color>, чтобы совпасть цветом пола со своим и снять захват цели.
2. На этот раз язык разрушит весь пол слева или справа.  
Кричите командный клич, чтобы заблокировать это — если не успеете, пол просто разрушится.
3. Восстановится только если вы прокричите командный клич, пока идёт предупреждение.  
После того как пол разрушен, остаётся только продержаться до следующего предупреждения.

**M.Stage5**
1. Похоже, оно тренируется!  
Дыхание вдруг стало тяжёлым.  
Начинается ветер!  
Встаньте на нужную плитку и держитесь!

**M.Boss — Intro**
1. Ха... ха... ещё немного продержаться, и мы, кажется, сможем выбраться живыми...

**M.Boss — Bossdown**
1. Эта тишина... внутри стало тихо.  
Неужели мы наконец сможем сбежать...?
2. .....(ХРУСТЬ!)
3. Нет, нас проглатывают...!!

### PL

**M.Stage1**
1. Dlaczego zjadłeś KKUL-TTEOK zamiast tych wszystkich pysznych owoców i słodyczy?!
2. Podobno nawet w jaskini tygrysa można przeżyć, jeśli zachowa się zimną krew..!  
Od teraz myślimy tylko o przetrwaniu.
3. Zapamiętajcie dobrze, gdzie są kolorowe pyski wokół nas.  
Jak nadepniesz na płytkę, pysk tego koloru wyskoczy i zmiażdży zęby.
4. Naraz podnosi się tylko jeden kolor.  
Nie ma sensu deptać razem wszystkim.
5. Jeśli to zacznie się zamykać... krzyczcie wszyscy okrzyk drużyny!  
Inaczej nic nie będziecie widzieć.

**M.Stage2 — 2.1**
1. Wejdźcie do bezpiecznej strefy pokazanej na ekranie!
2. Chyba zjadło coś ostrego, zanim mnie połknęło..?  
Z podłogi ciągle wypływa ślina.
3. Podłoga jest śliska od śliny.  
Krzyczcie wszyscy okrzyk drużyny, żeby ją usunąć.

**M.Stage2 — 2.2**
1. Teraz ślina kapie też z góry!  
Na pewno zjadło coś ostrego, a do tego coś kwaśnego.  
Na pewno próbuje to zneutralizować KKUL-TTEOKIEM!
2. Niektóre z opadających kropel celują we mnie – wciśnij <color=#FFD24D><b>Ctrl</b></color>, żeby dopasować kolor podłogi do swojego i zdjąć namierzanie.

**M.Stage3**
1. Na podłodze pojawią się kolorowe płytki.  
Postój na niej 2 sekundy, a wynik wzrośnie.
2. Białe i czarne płytki może zdeptać każdy, ale te w unikalnym kolorze liczą punkty tylko dla tego koloru.  
Nie blokujcie drogi, ruszajcie się szybko!

**M.Stage4 — 4.1**
1. Gdy twój kolor pojawi się na podłodze, wciśnij <color=#FFD24D><b>Space</b></color>.  
Biały może wcisnąć każdy, ale czarnego nie wciskajcie nigdy!  
Po 1 sekundzie automatycznie przechodzi do następnej tury.
2. Coś się rusza pod naszymi stopami... uważajcie!  
Nadchodzi atak językiem!
3. Ze środka wystrzeli język, który zasłoni widok na drugą stronę i zablokuje drogę.  
Krzyczcie wszyscy okrzyk drużyny, żeby opadł z powrotem, a zniszczona droga się naprawiła.

**M.Stage4 — 4.2**
1. Od teraz będzie widać tylko jedną płytkę do przodu.  
Zapamiętajcie ją dobrze!

**M.Stage4 — 4.3**
1. Tutaj też coś we mnie celuje.  
Wciśnij <color=#FFD24D><b>Ctrl</b></color>, żeby dopasować kolor podłogi do swojego i zdjąć namierzanie.
2. Tym razem język zniszczy całą podłogę po lewej albo po prawej.  
Krzyczcie okrzyk drużyny, żeby to zablokować – jeśli się nie uda, po prostu się zniszczy.
3. Naprawi się tylko wtedy, gdy krzykniecie okrzyk drużyny w trakcie ostrzeżenia.  
Gdy podłoga już się zniszczy, trzeba wytrzymać do następnego ostrzeżenia.

**M.Stage5**
1. Chyba właśnie ćwiczy!  
Oddech nagle zrobił się ciężki.  
Nadchodzi wiatr!  
Wejdźcie na właściwą płytkę i trzymajcie się!

**M.Boss — Intro**
1. Hał... hał... jeszcze trochę wytrzymać i chyba uda nam się wyjść stąd żywi...

**M.Boss — Bossdown**
1. Ta cisza... w środku zrobiło się cicho.  
Czyżbyśmy wreszcie mogli uciec...?
2. .....(CHRUP!)
3. O nie, zostajemy połknięci...!!

---

## T.Stage1 (BoulderSpawnManager + ReachZoneObjective — 조임 초출 + ColorWall 고유)

1. 삼켜져서 식도로 넘어왔어... 이렇게 된 이상, 아래로 내려가서 탈출하는 수밖에 없어.
2. 식도가 주기적으로 조여올 거야. 경고가 뜨면, 다 같이 팀 구호를 외쳐서 막아!
3. 양옆에서 색벽이 밀려올 거야. 내 색이 뜬 벽엔 직접 부딪혀 — 그러면 벽이 뒤로 물러날 거야.
4. 저 소리... 쿠르르릉? 설마 사탕이야?! 깔리기 싫으면 달려!!

*(1번 = 식도 진입(M→T 전환). 2번 "조여온다" = 식도 원통 반경 축소(`EsophagusSqueeze`, 전방향, 랜덤 주기 공격) — Warning 중 외치면 공격 취소, Hold 중 외치면 원상 복구. 3번 "색벽" = `ColorWall` 고유색(좌우 압박, 되돌림 대상 아님) — 접촉 시 색 일치면 `AdvancingWall.PauseTemporarily()`로 원점 후퇴 + 일시정지(`ColorWall.cs` `HandleContact`/`PauseRoutine`). **T1은 고유색, 흑백은 T3 초출**(`CoopStageAudit.T.md` §4 잠금). 4번 = `BoulderSpawnManager` 추격 시작(`ReachZoneObjective`).)*

## T.Stage2 (MemoryPath / ColoredMemoryPath / PioneerPathManager — 안개 초출)

**Stage1 — MemoryPath (+ 안개 초출)**
1. 빛나는 칸만 잘 외워둬. 잘못 밟으면 그대로 즉사야.
2. 트림이 나오다가 식도에 막혀버렸어... 가스가 차오르기 시작해. 짙어져서 앞이 안 보이면 팀 구호를 외쳐서 걷어내자.

**Stage2 — ColoredMemoryPath**
1. 이번엔 색깔별로 보여줄 거야. 반드시 네 색에 맞춰야 해! 색이 맞아도 흑백이면 죽을 거야.

**Stage3 — PioneerPathManager**
1. 구역마다 담당 색이 있어. 담당이 먼저 지나가야 그제야 길이 안전해져! 꼭 순서를 지켜!

*(Stage1 1번 = `MemoryPathTile`, Trap은 `NetworkDamageUtil.ApplyInstantKill` 즉사. 2번 "가스" = 안개 초출 — 거리 기반 Render Fog(`EsophagusFog`), 씬 전역 적용·구간 분리 없음, 걷혀도 정답 하이라이트가 다시 뜨는 게 아니라 그 순간의 바닥만 보임. Stage2 = `ColoredMemoryPathTile.IsSafeFor`, 고유색 비활성(흑백) 상태면 색이 맞아도 즉사. Stage3 = `PioneerPathTile`, 미개방 타일을 pioneer 아닌 색이 밟거나 흑백 상태면 즉사, Trap 타일은 누가 밟든 항상 즉사.)*

## T.Stage3 (Wall·볼더·Spike·패드 — 조임 복습 + ColorWall 흑백 초출)

1. 여기도 벽이 좁혀올 거야. 아까처럼 팀 구호로 버텨.
2. 이번엔 흑백 벽이야. 색 상관없이 아무나 맞추면 잠깐 멈출 거야.

*(1번 = T.Stage1과 같은 원통 반경 조임(`EsophagusSqueeze`) 복습 — 2인 장면은 전원 외침 원상 복구. 2번 = `ColorWall` 흑백 초출, 색 일치 시 잠깐 멈춤(2인 게이트 아님).)*

## T.Stage4 (MovingCorridor + ContactKnockback + 구멍 바닥 + 패드→Door 길 — 안개 복습)

1. 부딪히면 튕겨나가! 그 밑엔 구멍이 있으니 조심해.
2. 패드를 밟으면 문이 올라와서 구멍 위로 길이 생겨.
3. 안개가 심해지면 그 길도 안 보이게 될 거야. 팀 구호를 외쳐서 걷어내.

*(1번 = ContactKnockback + 구멍 바닥(각자 생존, 2인 게이트 아님). 2번 = 패드(Door_3, 커먼) 밟으면 Door가 올라와 길이 됨(길 만들기·건너기도 2인 게이트 아님). 3번 = 안개 복습(`EsophagusFog`) — 2인 장면은 전원 외침으로 안개 걷힘. 조임 원상 복구 없음.)*

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

- [x] M-Stage(1~5+Boss) 한국어 대사 확정
- [x] M-Stage 11개 언어 번역 초안 작성 (`M-Stage 번역 (Localization Draft)` 섹션) — JA/ZH/RU/PL 원어민 검수 권장
- [x] M-Stage 번역을 `Dialogue` String Table(ko 포함 12개 locale)에 반영
- [ ] T-Stage(1~5+Boss) 감사·대사 확정 (진행 예정)
- [ ] T-Stage 확정 후 동일 규칙으로 11개 언어 번역 + 테이블 반영
- [ ] 씬(.unity) `DialogueUI` / `LocalizeStringEvent`를 새 키·줄 수에 맞게 재연결 (에디터 작업)

### 에디터에서 할 일 (M-Stage 씬)

줄 수가 바뀌었으므로 기존 TMP 슬롯만으로는 부족하다. 각 M 씬 `Dialogue_Panel`에서:

| 구간 | 새 키 (순서) | 비고 |
|---|---|---|
| M.Stage1 | `M.Stage1.Line1`~`Line5` | 기존 1줄 → 5줄 |
| M.Stage2 2.1 | `M.Stage2.Stage1.Line1`~`Line3` | 신규 |
| M.Stage2 2.2 | `M.Stage2.Stage2.Line1`~`Line2` | 신규 |
| M.Stage3 | `M.Stage3.Line1`~`Line2` | `Line3`는 비워 둠(구 대사). 씬에서 빼기 |
| M.Stage4 4.1 | `M.Stage4.Stage1.Line1`~`Line3` | 내용만 갱신 |
| M.Stage4 4.2 | `M.Stage4.Stage2.Line1` | 신규 |
| M.Stage4 4.3 | `M.Stage4.Stage3.Line1`~`Line3` | 신규 |
| M.Stage5 | `M.Stage5.Line1` | 내용만 갱신 |
| M.Boss Intro | `M.Boss.Intro.Line1` | `Line2` 비움. 씬에서 빼기 |
| M.Boss Bossdown | `M.Boss.Bossdown.Line1`~`Line3` | `Line4` 비움. 씬에서 빼기 |

각 TMP에 `LocalizeStringEvent` → Table `Dialogue` → 위 키. TMP 본문에 `\n` 줄바꿈이 보이려면 `overflow`/`wrapping`이 켜져 있어야 한다.
