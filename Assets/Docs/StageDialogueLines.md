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
흰색은 아무나 눌러도 되고, 검은색은 누르면 안 돼!  
검은색은 1초 뒤에 자동으로 다음 차례로 넘어갈 거야.
2. 발 밑에서 뭔가 움직이고 있어..조심해!  
혀 공격이 들어온다!
3. 가운데 발판을 부수고 혀가 길을 방해할 거야.  
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

1. 운동하고 있나 봐! 숨이 갑자기 거칠어졌어.  
바람이 불어온다! 맞는 바닥 위에 올라가서 버텨!

*(Color/BW 모드 공용 — BW 모드용 별도 문구 없음.)*

## M.Boss (BossFightObjective — 몬스터 없음, 스테이지 함정 자체가 보스)

**Intro**
1. 후아... 후아... 조금만 더 버티면 살아서 나갈 수 있을 것 같아...

**Bossdown**
1. ...입 안이 조용해졌다.  
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
Anyone can press for white, but don't press for black!  
Black will move to the next turn automatically after 1 second.
2. Something's moving under our feet..watch out!  
It's a tongue attack!
3. The tongue will smash the platform in the center and block the way.  
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
1. Feels like it's working out! Its breathing suddenly got rough.  
Here comes the wind! Get on the matching tile and hold your ground!

**M.Boss — Intro**
1. Haah... haah... just a little more and we might actually make it out alive...

**M.Boss — Bossdown**
1. ...It's gone quiet in here.  
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
白は誰が押してもいいけど、黒は押しちゃダメ!  
黒は1秒後に自動で次の番に進むよ。
2. 足元で何かが動いてる…気をつけて!  
舌の攻撃が来る!
3. 舌が中央の足場を壊して道を妨げるよ。  
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
1. 運動でもしてるのかな!急に息が荒くなった。  
風が吹いてくる!合ってるタイルに乗って耐えて!

**M.Boss — Intro**
1. はあ…はあ…もう少し耐えれば、生きて出られそうだ…

**M.Boss — Bossdown**
1. ……口の中が静かになった。  
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
白色谁按都行,黑色不能按!  
黑色会在1秒后自动轮到下一个人。
2. 脚下好像有什么在动……小心!  
舌头攻击来了!
3. 舌头会砸碎中间的踏板,挡住去路。  
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
1. 好像在运动!呼吸突然变粗重了。  
风吹过来了!站到对应的地板上撑住!

**M.Boss — Intro**
1. 呼……呼……再撑一下,好像就能活着出去了……

**M.Boss — Bossdown**
1. ……嘴里安静下来了。  
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
白色誰按都行,黑色不能按!  
黑色會在1秒後自動輪到下一個人。
2. 腳下好像有什麼在動……小心!  
舌頭攻擊來了!
3. 舌頭會砸碎中間的踏板,擋住去路。  
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
1. 好像在運動!呼吸突然變粗重了。  
風吹過來了!站到對應的地板上撐住!

**M.Boss — Intro**
1. 呼……呼……再撐一下,好像就能活著出去了……

**M.Boss — Bossdown**
1. ……嘴裡安靜下來了。  
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
Al blanco puede darle cualquiera, ¡pero al negro no le deis!  
El negro pasará automáticamente al siguiente turno después de 1 segundo.
2. Algo se mueve bajo nuestros pies... ¡cuidado!  
¡Viene un ataque de lengua!
3. La lengua destrozará la plataforma central y bloqueará el camino.  
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
1. ¡Parece que está haciendo ejercicio! De repente respira con más fuerza.  
¡Viene viento! ¡Subid a la baldosa correcta y aguantad!

**M.Boss — Intro**
1. Fiu... fiu... con un poco más de aguante, parece que podremos salir con vida...

**M.Boss — Bossdown**
1. ...La boca se ha quedado callada.  
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
Al blanco le puede dar cualquiera, ¡pero al negro no le den!  
El negro va a pasar automáticamente al siguiente turno después de 1 segundo.
2. Algo se mueve bajo nuestros pies... ¡cuidado!  
¡Viene un ataque de lengua!
3. La lengua va a destrozar la plataforma del centro y bloquear el camino.  
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
1. ¡Parece que está haciendo ejercicio! De repente empezó a respirar más fuerte.  
¡Viene viento! ¡Súbanse a la baldosa correcta y aguanten!

**M.Boss — Intro**
1. Fiu... fiu... con un poco más de aguante, parece que vamos a poder salir con vida...

**M.Boss — Bossdown**
1. ...La boca se quedó callada.  
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
N'importe qui peut appuyer pour le blanc, mais pas pour le noir !  
Pour le noir, ça passe automatiquement au tour suivant après 1 seconde.
2. Quelque chose bouge sous nos pieds... attention !  
Une attaque de langue arrive !
3. La langue va détruire la plateforme centrale et bloquer le chemin.  
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
1. On dirait qu'il fait de l'exercice ! Sa respiration est devenue soudainement plus forte.  
Le vent arrive ! Montez sur la bonne dalle et tenez bon !

**M.Boss — Intro**
1. Hah... hah... encore un peu, et on devrait pouvoir sortir vivants...

**M.Boss — Bossdown**
1. ...C'est devenu calme à l'intérieur.  
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
Bei Weiß darf jeder drücken, aber bei Schwarz nicht!  
Bei Schwarz geht es nach 1 Sekunde automatisch zum nächsten Zug über.
2. Unter unseren Füßen bewegt sich etwas... Vorsicht!  
Ein Zungenangriff kommt!
3. Die Zunge zerstört die Plattform in der Mitte und blockiert den Weg.  
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
1. Sieht aus, als würde es trainieren! Der Atem ist plötzlich schwerer geworden.  
Der Wind kommt! Stellt euch auf die passende Kachel und haltet durch!

**M.Boss — Intro**
1. Haah... haah... noch ein bisschen durchhalten, dann schaffen wir es vielleicht lebend raus...

**M.Boss — Bossdown**
1. ...Es ist ruhig geworden hier drin.  
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
No branco qualquer um pode apertar, mas no preto não aperte!  
No preto, passa automático pro próximo turno depois de 1 segundo.
2. Tem algo se mexendo debaixo dos nossos pés... cuidado!  
Vem um ataque de língua!
3. A língua vai destruir a plataforma do centro e bloquear o caminho.  
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
1. Parece que tá se exercitando! A respiração ficou pesada do nada.  
O vento tá vindo! Suba no bloco certo e aguenta firme!

**M.Boss — Intro**
1. Haah... haah... com mais um pouco de esforço, parece que a gente sai vivo daqui...

**M.Boss — Bossdown**
1. ...Ficou tudo quieto aqui dentro.  
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
На белый может нажать кто угодно, а на чёрный — не нажимай!  
На чёрном ход автоматически перейдёт к следующему через 1 секунду.
2. Что-то шевелится у нас под ногами... осторожно!  
Сейчас будет атака языком!
3. Язык разрушит платформу в центре и перекроет путь.  
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
1. Похоже, оно тренируется! Дыхание вдруг стало тяжёлым.  
Начинается ветер! Встаньте на нужную плитку и держитесь!

**M.Boss — Intro**
1. Ха... ха... ещё немного продержаться, и мы, кажется, сможем выбраться живыми...

**M.Boss — Bossdown**
1. ...Внутри стало тихо.  
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
Biały może wcisnąć każdy, ale czarnego nie wciskajcie!  
Przy czarnym po 1 sekundzie automatycznie przechodzi do następnej tury.
2. Coś się rusza pod naszymi stopami... uważajcie!  
Nadchodzi atak językiem!
3. Język zniszczy platformę na środku i zablokuje drogę.  
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
1. Chyba właśnie ćwiczy! Oddech nagle zrobił się ciężki.  
Nadchodzi wiatr! Wejdźcie na właściwą płytkę i trzymajcie się!

**M.Boss — Intro**
1. Hał... hał... jeszcze trochę wytrzymać i chyba uda nam się wyjść stąd żywi...

**M.Boss — Bossdown**
1. ...W środku zrobiło się cicho.  
Czyżbyśmy wreszcie mogli uciec...?
2. .....(CHRUP!)
3. O nie, zostajemy połknięci...!!

---

## T.Stage1 (BoulderSpawnManager + ReachZoneObjective — 조임 초출 + ColorWall 고유)

1. 삼켜져서 식도로 넘어왔어... 이렇게 된 이상, 아래로 내려가서 탈출하는 수밖에 없어.
2. 식도가 주기적으로 조여올 거야. 경고가 뜨면, 다 같이 팀 구호를 외쳐서 막아!
3. 양옆에서 색벽이 밀려올 거야. 내 색이 뜬 벽엔 직접 부딪혀 — 그러면 벽이 뒤로 물러날 거야.
4. ...데구르르르르? 지금 굴러오고 있는 게 설마 사탕이야?! 깔리기 싫으면 달려!!

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

1. 바닥을 조심해. 역류성 식도염이 있나 봐. 산성 물질이 올라오잖아!
2. 안 보이는 곳에서 가시 공격이 올 거야. 항상 유의해.
3. 양옆의 벽이 흑백으로 바뀔 거야. 색에 맞춰 벽에 부딪혀서, 압박하는 벽을 밀어내!

*(1번 = `GreenMucusTrap`(`AcidPool`/`AcidHazardVFX` + `ContactDamage`) — "점액"(압력) 카테고리. 2번 = `SpikeTrap`(바닥에서 올라오는 가시, `ContactDamage`류 데미지) — T.Stage3.unity에 다수 배치. 3번 = `ColorWall` 흑백 초출 — 접촉 시 색 일치면 `AdvancingWall.PauseTemporarily()`로 원점 후퇴+정지(2인 게이트 아님). 조임 복습(`EsophagusSqueeze`)은 T1과 동일 메카닉이라 대사엔 안 넣음 — 2인 장면은 전원 외침 원상 복구.)*

## T.Stage4 (MovingCorridor + ContactKnockback + 구멍 바닥 + 패드→Door 길 — 안개 복습)

1. 알레르기 반응이 왔나 봐. 식도에 부종들이 생겼어. 이것들에 부딪히면 튕겨져 나갈 거야.

*("부종" = `ContactKnockback`(순수 넉백, HP 무관) + 구멍 바닥(각자 생존, 2인 게이트 아님), T3에 잠깐 나온 걸 T4에서 메인으로. 패드→`DoorController` 길, 안개 복습(`EsophagusFog`)은 T1·T2에서 이미 가르쳐서 대사 없이 감.)*

## T.Stage5 (Stage5TargetRunner / Stage5ChaserAI)

**Stage1 — Runner 최초 등장** (Stage2도 재사용, 재설명 없음)
1. 돌아다니는 적혈구를 잡아. 이 몸한테 타격을 주자. 당하기만 할 순 없어!

**Stage3 — Chaser 최초 등장** (Stage4도 재사용, 재설명 없음)
1. 적혈구들을 잡았더니 백혈구들이 우리를 적으로 인식했어! 도망쳐서 살아남아!
2. 벽에 부딪히면 튕겨져 나갈 거야. 이걸 잘 이용하면 도망칠 수 있겠는데?

*(Stage1 = `Stage5TargetRunner`, 색·고유색 조건 없이 접촉하면 포획. Stage3 1번 = `Stage5ChaserAI` 초출(비은신 최근접 1명 추격, 은신 전환 시 타겟 교체 — Ctrl 은신이 실제 회피 수단). 2번 = `T.Stage5.unity`에 배치된 `ContactKnockback` 벽 다수 확인됨, 튕김을 이용한 회피 팁.)*

## T.Boss (BossFightObjective — 시간 구간 기반 연속 생존)

**Intro**
1. 식도의 마지막 부분이야... 이 아래는 분명 위액으로 가득 차있을 거야. 준비 없이 내려가면 위험해.
2. 천장에 아까의 사탕이 점점 내려오고 있어. 땅에 닿기 전에 바닥을 만들어서 막아야 해 — 안 그러면 우리도 같이 휩쓸려 떨어질 거야.
3. 각 구간을 해결하면 바닥이 생겨. 얼른 만들어서 저걸 막아야 해!

**Bossdown**
1. 휴우~~ 가까스로 막았네... 이제 생각해보자. 어떻게 안전하게 내려갈지.
2. 꿀꺽... 꿀꺽... 이게 무슨 소리지...?
3. 목이 막혀서 물을 마시기 시작했어! 사탕이 바닥을 부순다!!
4. 으아아아아아아아아...!!

*(2번 = T.Stage1 "사탕을 삼켰잖아...!"의 그 사탕이 식도를 타고 밀려 내려오며 커진 것 — 조임(연동운동)에 밀려 내려오는 설정, `CoopStageAudit.T.md` §6 "시계"(Sphere). 3번 = 구간(P1–P4)마다 Pioneer/Door/ColorWall+튕김 등으로 바닥을 넓혀 막음. Bossdown 3·4번 대사와 동시에 바닥이 부서지며 추락 — 위(胃)는 확정하지 않고 클리프행어로 마무리(시즌2 여지 보존).)*

---

# T-Stage 번역 (Localization Draft)

> 번역 규칙: `M-Stage 번역 (Localization Draft)` 섹션과 동일 — **1)** 언어별 문법에 맞게, **2)** 그 언어권에서 실제 쓰는 말투로 자연스럽게. 용어는 M 번역에서 이미 확정된 표현을 그대로 재사용해 전체 문서 용어를 통일한다: 팀 구호 = team cheer / チームの掛け声 / 团队口号 / 團隊口號 / grito de equipo / cri d'équipe / Teamruf / grito do time / командный клич / okrzyk drużyny. 개인 행동(예: "네 색에 맞춰")은 2인칭 단수(tú/du/tu 등), 전원 행동(팀 구호·도주·경계)은 2인칭 복수(vosotros·ustedes·vous·ihr 등)로 M과 동일하게 구분.
>
> ⚠️ 11개 언어 전체를 1차로 초안 작성한 것으로, 특히 JA/ZH/RU/PL은 원어민 검수 권장.

### EN

**T.Stage1**
1. We got swallowed and ended up in the esophagus... At this point, there's nothing to do but head down and escape.
2. The esophagus is going to squeeze in on us at random intervals. When the warning pops up, everyone shout the team cheer to stop it!
3. Colored walls will push in from both sides. If a wall shows your color, slam right into it — it'll get knocked back.
4. ...rumble, rumble? Don't tell me that's the candy rolling toward us?! Run if you don't want to get flattened!!

**T.Stage2 — Stage1**
1. Remember only the glowing tiles. Step on the wrong one and it's an instant kill.
2. A burp got stuck in the esophagus... gas is starting to build up. If it gets too thick to see through, shout the team cheer to clear it out.

**T.Stage2 — Stage2**
1. This time it'll show tiles by color. You have to match your own color! Even if the color's right, you'll still die if it's in black-and-white mode.

**T.Stage2 — Stage3**
1. Each zone has an assigned color. The path only becomes safe once that color goes through first! Stick to the order!

**T.Stage3**
1. Watch your step. Feels like acid reflux — that's stomach acid coming up!
2. Spike attacks will come from places you can't see. Stay alert at all times.
3. The walls on both sides will turn black-and-white. Match the color and slam into the wall to push back the pressure!

**T.Stage4**
1. Must be an allergic reaction — swelling's popped up all through the esophagus. Bump into one of these and you'll get knocked flying.

**T.Stage5 — Stage1**
1. Go catch those red blood cells running around — let's land some damage on this body. We can't just keep taking hits!

**T.Stage5 — Stage3**
1. Catching those red blood cells made the white blood cells mark us as enemies! Run and survive!
2. Bump into a wall and you'll get knocked back. Might be able to use that to get away, huh?

**T.Boss — Intro**
1. This is the last stretch of the esophagus... below here has to be full of stomach acid. Going down unprepared is way too risky.
2. That candy from before is coming down from the ceiling, and it's bigger now. We need to build a floor before it hits bottom — or we'll get swept down with it.
3. Clearing each section builds part of the floor. Hurry and build it to block that thing!

**T.Boss — Bossdown**
1. Phew... we barely held it back... now let's think about how to get down safely from here.
2. Gulp... gulp... what is that sound...?
3. Its throat's clogged and it's starting to drink water! The candy's breaking through the floor!!
4. AAAAAAAAAH...!!

### JA

**T.Stage1**
1. 飲み込まれて食道まで来ちゃった…こうなったら、下まで降りて逃げるしかない。
2. 食道が周期的に締まってくるよ。警告が出たら、みんなでチームの掛け声を叫んで止めよう!
3. 両側から色の壁が迫ってくるよ。自分の色が出た壁には直接ぶつかって — そうすれば壁が後ろに下がるはず。
4. …ゴロゴロゴロ…?今転がってきてるの、まさかアメ玉じゃないよね?!踏まれたくないなら走れ!!

**T.Stage2 — Stage1**
1. 光ってるマスだけしっかり覚えて。間違えて踏んだら即死だよ。
2. ゲップが出かけて食道でつっかえちゃった…ガスが溜まり始めてる。濃くなって前が見えなくなったら、みんなでチームの掛け声を叫んで払おう。

**T.Stage2 — Stage2**
1. 今度は色ごとに見せるよ。必ず自分の色に合わせて!色が合ってても白黒状態なら死ぬから気をつけて。

**T.Stage2 — Stage3**
1. 区域ごとに担当の色があるよ。担当が先に通らないと道は安全にならない!順番は絶対守って!

**T.Stage3**
1. 足元に気をつけて。逆流性食道炎かも…酸性の液体が上がってきてる!
2. 見えない場所から棘の攻撃が来るよ。常に気を抜かないで。
3. 両側の壁が白黒に切り替わるよ。色を合わせて壁にぶつかって、迫ってくる壁を押し返せ!

**T.Stage4**
1. アレルギー反応が出たみたい。食道にむくみができてる。ぶつかると弾き飛ばされるよ。

**T.Stage5 — Stage1**
1. 動き回る赤血球を捕まえて。この体にダメージを与えよう。やられっぱなしじゃいられない!

**T.Stage5 — Stage3**
1. 赤血球を捕まえたら、白血球たちに敵として認識された!逃げて生き延びろ!
2. 壁にぶつかると弾き飛ばされるよ。これを上手く使えば逃げられそうだね?

**T.Boss — Intro**
1. 食道の最後の部分だ…この下は絶対胃液でいっぱいのはず。準備なしで降りるのは危険すぎる。
2. さっきのアメ玉が天井からどんどん下がってきてる。地面に着く前に足場を作って止めないと — じゃないと私たちも一緒に飲み込まれて落ちちゃう。
3. 各区間を突破すれば足場ができるよ。早く作ってあれを止めよう!

**T.Boss — Bossdown**
1. ふぅ…なんとか止めたね…さあ考えよう。どうやって安全に降りるか。
2. …ゴクッ…ゴクッ…この音は何…?
3. のどが詰まって水を飲み始めた!アメ玉が足場を壊す!!
4. うわああああああああ…!!

### ZH-Hans

**T.Stage1**
1. 被吞下去掉进食道里了……到这一步,只能往下走想办法逃出去了。
2. 食道会周期性地收缩。警告一出现,大家一起喊团队口号把它顶回去!
3. 两侧的彩色墙会挤过来。哪面墙是你的颜色,就直接撞上去——墙会被撞退回去。
4. ……咕噜咕噜咕噜?现在滚过来的这个,难道是糖果吗?!不想被压扁就快跑!!

**T.Stage2 — Stage1**
1. 只记住发光的格子就行。踩错格子会立刻死。
2. 打嗝打到一半被食道堵住了……气体开始积起来了。变浓看不清路的时候,大家一起喊团队口号把它清掉。

**T.Stage2 — Stage2**
1. 这次会按颜色分别显示。一定要对上你自己的颜色!颜色对了也没用,黑白状态照样会死。

**T.Stage2 — Stage3**
1. 每个区域都有对应的负责颜色。负责的颜色先走一遍,路才会变安全!一定要按顺序来!

**T.Stage3**
1. 小心脚下。是不是有反流性食道炎啊,胃酸都涌上来了!
2. 看不见的地方会有尖刺攻击过来。时刻保持警惕。
3. 两侧的墙会变成黑白色。对上颜色撞上去,把压过来的墙推回去!

**T.Stage4**
1. 好像是过敏反应,食道里长出了一堆浮肿。撞到这些东西会被弹飞出去。

**T.Stage5 — Stage1**
1. 去抓住到处跑的红细胞,咱们也给这身体来点伤害。不能一直只挨打!

**T.Stage5 — Stage3**
1. 抓了红细胞之后,白细胞把我们当成敌人了!快跑,活下来!
2. 撞到墙会被弹开。好好利用这个说不定能逃掉?

**T.Boss — Intro**
1. 这是食道的最后一段了……下面肯定全是胃液。毫无准备就下去太危险了。
2. 天花板上那块糖果正在慢慢降下来,而且越来越大。得赶紧搭出地面挡住它——不然我们也会一起被冲下去。
3. 每解决一个区间就会生成一块地面。赶紧搭好挡住那东西!

**T.Boss — Bossdown**
1. 呼……总算是挡住了……好,想想接下来要怎么安全地下去。
2. 咕咚……咕咚……这是什么声音……?
3. 它嗓子堵住了,开始咕嘟咕嘟灌水!糖果把地面砸破了!!
4. 啊啊啊啊啊啊啊啊啊……!!

### ZH-Hant

**T.Stage1**
1. 被吞下去掉進食道裡了……到這一步,只能往下走想辦法逃出去了。
2. 食道會週期性地收縮。警告一出現,大家一起喊團隊口號把它頂回去!
3. 兩側的彩色牆會擠過來。哪面牆是你的顏色,就直接撞上去——牆會被撞退回去。
4. ……咕嚕咕嚕咕嚕?現在滾過來的這個,難道是糖果嗎?!不想被壓扁就快跑!!

**T.Stage2 — Stage1**
1. 只要記住發光的格子就好。踩錯格子會立刻死掉。
2. 打嗝打到一半被食道卡住了……氣體開始積起來了。變濃看不清路的時候,大家一起喊團隊口號把它清掉。

**T.Stage2 — Stage2**
1. 這次會按顏色分別顯示。一定要對上你自己的顏色!顏色對了也沒用,黑白狀態照樣會死。

**T.Stage2 — Stage3**
1. 每個區域都有對應的負責顏色。負責的顏色要先走一遍,路才會變安全!一定要按順序來!

**T.Stage3**
1. 小心腳下。是不是有胃食道逆流啊,胃酸都湧上來了!
2. 看不見的地方會有尖刺攻擊過來。要隨時保持警覺。
3. 兩側的牆會變成黑白色。對上顏色撞上去,把壓過來的牆推回去!

**T.Stage4**
1. 好像是過敏反應,食道裡長出了一堆浮腫。撞到這些東西會被彈飛出去。

**T.Stage5 — Stage1**
1. 去抓住到處亂跑的紅血球,大家一起給這身體來點傷害。不能一直只挨打!

**T.Stage5 — Stage3**
1. 抓了紅血球之後,白血球把我們當成敵人了!快逃,活下來!
2. 撞到牆會被彈開。好好利用這個,說不定能逃掉?

**T.Boss — Intro**
1. 這是食道的最後一段了……下面肯定全是胃液。毫無準備就下去太危險了。
2. 天花板上那顆糖果正在慢慢降下來,而且越變越大。得趕緊搭出地板擋住它——不然我們也會一起被沖下去。
3. 每解決一個區段就會生成一塊地板。趕緊搭好擋住那東西!

**T.Boss — Bossdown**
1. 呼……總算是擋住了……好,想想接下來要怎麼安全下去。
2. 咕嘟……咕嘟……這是什麼聲音……?
3. 牠喉頭卡住了,開始咕嘟咕嘟灌水!糖果正在把地板砸破!!
4. 啊啊啊啊啊啊啊啊啊……!!

### ES

**T.Stage1**
1. Nos tragó y acabamos en el esófago... Así las cosas, no queda otra que bajar y escapar.
2. El esófago se va a contraer a intervalos aleatorios. En cuanto salga el aviso, ¡gritad todos el grito de equipo para detenerlo!
3. Unas paredes de colores avanzarán desde los lados. Si una pared muestra tu color, chócate contra ella sin miedo — retrocederá.
4. ...¿ese ruido de algo rodando? ¡No me digáis que es el caramelo de antes!? ¡Corred si no queréis quedar aplastados!!

**T.Stage2 — Stage1**
1. Memorizad solo las baldosas que brillan. Si pisáis la equivocada, es muerte instantánea.
2. Se le atascó un eructo en el esófago... está empezando a acumularse el gas. Si se pone tan espeso que no se ve nada, gritad el grito de equipo para despejarlo.

**T.Stage2 — Stage2**
1. Esta vez las va a mostrar por colores. ¡Tenéis que acertar con vuestro propio color! Aunque el color sea el correcto, si está en blanco y negro, morís igual.

**T.Stage2 — Stage3**
1. Cada zona tiene un color asignado. ¡El camino solo se vuelve seguro después de que pase primero ese color! ¡Respetad el orden!

**T.Stage3**
1. Cuidado con el suelo. Debe tener reflujo — ¡le está subiendo ácido del estómago!
2. Vendrán ataques de púas desde donde no se ve. Estad siempre alerta.
3. Las paredes de los lados se van a poner en blanco y negro. ¡Igualad el color y chocad contra la pared para hacer retroceder la presión!

**T.Stage4**
1. Debe ser una reacción alérgica — le han salido bultos por todo el esófago. Si chocáis con uno, saldréis despedidos.

**T.Stage5 — Stage1**
1. Id a atrapar a los glóbulos rojos que andan sueltos, a ver si le hacemos daño a este cuerpo. ¡No podemos quedarnos solo aguantando golpes!

**T.Stage5 — Stage3**
1. ¡Al atrapar a los glóbulos rojos, los glóbulos blancos nos marcaron como enemigos! ¡Corred y sobrevivid!
2. Si chocáis contra una pared, saldréis despedidos. Podríamos usar eso para escapar, ¿no?

**T.Boss — Intro**
1. Este es el último tramo del esófago... ahí abajo tiene que estar lleno de ácido del estómago. Bajar sin prepararnos es demasiado peligroso.
2. El caramelo de antes está bajando por el techo, y cada vez es más grande. Tenemos que construir suelo antes de que toque fondo — si no, nos arrastrará con él.
3. Al superar cada tramo se genera parte del suelo. ¡Rápido, construidlo para detener a eso!

**T.Boss — Bossdown**
1. Fiu... por poco lo detenemos... ahora pensemos. Cómo bajar de forma segura desde aquí.
2. Glup... glup... ¿qué es ese sonido...?
3. ¡Se le atascó la garganta y se puso a beber agua! ¡El caramelo está rompiendo el suelo!!
4. ¡AAAAAAAAAH...!!

### ES-419

**T.Stage1**
1. Nos tragó y terminamos en el esófago... Así las cosas, no queda de otra más que bajar y escapar.
2. El esófago se va a contraer a intervalos aleatorios. En cuanto aparezca la advertencia, ¡griten todos el grito de equipo para detenerlo!
3. Van a avanzar paredes de colores desde los costados. Si una pared muestra tu color, chócate contra ella sin miedo — va a retroceder.
4. ...¿ese ruido de algo rodando? ¡No me digan que es el caramelo de antes!? ¡Corran si no quieren terminar aplastados!!

**T.Stage2 — Stage1**
1. Memoricen solo las baldosas que brillan. Si pisan la equivocada, es muerte instantánea.
2. Se le atoró un eructo en el esófago... está empezando a acumularse el gas. Si se pone tan espeso que no se ve nada, griten el grito de equipo para despejarlo.

**T.Stage2 — Stage2**
1. Esta vez las va a mostrar por colores. ¡Tienen que acertar con su propio color! Aunque el color sea el correcto, si está en blanco y negro, mueren igual.

**T.Stage2 — Stage3**
1. Cada zona tiene un color asignado. ¡El camino solo se vuelve seguro después de que pase primero ese color! ¡Respeten el orden!

**T.Stage3**
1. Cuidado con el piso. Debe tener reflujo — ¡le está subiendo ácido del estómago!
2. Van a venir ataques de púas desde donde no se ve. Estén siempre alerta.
3. Las paredes de los costados se van a poner en blanco y negro. ¡Igualen el color y choquen contra la pared para hacer retroceder la presión!

**T.Stage4**
1. Debe ser una reacción alérgica — le salieron bultos por todo el esófago. Si chocan con uno, van a salir volando.

**T.Stage5 — Stage1**
1. Vayan a atrapar a los glóbulos rojos que andan sueltos, a ver si le hacemos daño a este cuerpo. ¡No podemos quedarnos solo aguantando golpes!

**T.Stage5 — Stage3**
1. ¡Al atrapar a los glóbulos rojos, los glóbulos blancos nos marcaron como enemigos! ¡Corran y sobrevivan!
2. Si chocan contra una pared, van a salir despedidos. Podríamos usar eso para escapar, ¿no?

**T.Boss — Intro**
1. Este es el último tramo del esófago... ahí abajo debe estar lleno de ácido del estómago. Bajar sin prepararnos es demasiado peligroso.
2. El caramelo de antes está bajando por el techo, y cada vez está más grande. Tenemos que construir piso antes de que toque fondo — si no, nos va a arrastrar con él.
3. Al superar cada tramo se genera parte del piso. ¡Rápido, constrúyanlo para detener a eso!

**T.Boss — Bossdown**
1. Fiu... por poco lo detenemos... ahora pensemos. Cómo bajar de forma segura desde aquí.
2. Glup... glup... ¿qué es ese sonido...?
3. ¡Se le atoró la garganta y empezó a beber agua! ¡El caramelo está rompiendo el piso!!
4. ¡AAAAAAAAAH...!!

### FR

**T.Stage1**
1. On a été avalés et on s'est retrouvés dans l'œsophage... Vu la situation, il ne reste plus qu'à descendre pour s'échapper.
2. L'œsophage va se resserrer à intervalles aléatoires. Dès que l'alerte apparaît, criez tous le cri d'équipe pour l'arrêter !
3. Des murs colorés vont avancer des deux côtés. Si un mur montre ta couleur, jette-toi dessus sans hésiter — il reculera.
4. ...ce bruit de truc qui roule ? Ne me dites pas que c'est le bonbon d'avant !? Courez si vous ne voulez pas finir écrasés !!

**T.Stage2 — Stage1**
1. Mémorisez juste les dalles qui brillent. Marchez sur la mauvaise et c'est la mort instantanée.
2. Un rot est resté coincé dans l'œsophage... le gaz commence à s'accumuler. S'il devient trop épais pour voir, criez le cri d'équipe pour le dissiper.

**T.Stage2 — Stage2**
1. Cette fois, ça va s'afficher par couleur. Tu dois absolument faire correspondre ta propre couleur ! Même si la couleur est correcte, tu meurs quand même si c'est en noir et blanc.

**T.Stage2 — Stage3**
1. Chaque zone a une couleur assignée. Le chemin ne devient sûr qu'une fois que cette couleur est passée en premier ! Respectez bien l'ordre !

**T.Stage3**
1. Attention au sol. On dirait un reflux — c'est de l'acide gastrique qui remonte !
2. Des attaques de pics vont venir d'endroits invisibles. Restez toujours sur vos gardes.
3. Les murs des deux côtés vont passer en noir et blanc. Fais correspondre la couleur et jette-toi sur le mur pour repousser la pression !

**T.Stage4**
1. On dirait une réaction allergique — des gonflements sont apparus dans tout l'œsophage. Si vous en touchez un, vous serez projetés en arrière.

**T.Stage5 — Stage1**
1. Allez attraper les globules rouges qui traînent, et infligeons des dégâts à ce corps. On ne peut pas se contenter d'encaisser !

**T.Stage5 — Stage3**
1. En attrapant les globules rouges, les globules blancs nous ont pris pour des ennemis ! Courez et survivez !
2. Si vous touchez un mur, vous serez repoussés. On pourrait s'en servir pour s'échapper, non ?

**T.Boss — Intro**
1. C'est la dernière partie de l'œsophage... en bas, ça doit être plein d'acide gastrique. Descendre sans préparation est bien trop dangereux.
2. Le bonbon d'avant descend du plafond, et il grossit de plus en plus. On doit construire un sol avant qu'il touche le fond — sinon on sera emportés avec lui.
3. Chaque section terminée fait apparaître une partie du sol. Vite, construisez-le pour bloquer ce truc !

**T.Boss — Bossdown**
1. Ouf... on a réussi à le bloquer de justesse... bon, réfléchissons. Comment descendre en sécurité à partir d'ici.
2. Glou... glou... c'est quoi ce bruit...?
3. Sa gorge s'est bouchée et il commence à boire de l'eau ! Le bonbon défonce le sol !!
4. AAAAAAAAAH...!!

### DE

**T.Stage1**
1. Wir wurden verschluckt und sind in der Speiseröhre gelandet... So wie die Dinge stehen, bleibt uns nur, nach unten zu gehen und zu entkommen.
2. Die Speiseröhre wird sich in zufälligen Abständen zusammenziehen. Sobald die Warnung erscheint, ruft alle zusammen den Teamruf, um sie zu stoppen!
3. Von beiden Seiten drängen bunte Wände heran. Zeigt eine Wand deine Farbe, ramm einfach direkt rein — dann wird sie zurückgestoßen.
4. ...dieses Rumpeln? Sag bloß, das ist das Bonbon von vorhin, das da angerollt kommt?! Rennt, wenn ihr nicht zerquetscht werden wollt!!

**T.Stage2 — Stage1**
1. Merkt euch nur die aufleuchtenden Felder. Tretet ihr aufs falsche, ist es sofort vorbei.
2. Ein Rülpser blieb in der Speiseröhre stecken... es sammelt sich langsam Gas an. Wird es zu dicht, um noch etwas zu sehen, ruft den Teamruf, um es zu vertreiben.

**T.Stage2 — Stage2**
1. Diesmal wird es nach Farben angezeigt. Du musst unbedingt deine eigene Farbe treffen! Selbst wenn die Farbe stimmt, stirbst du trotzdem, wenn es schwarz-weiß ist.

**T.Stage2 — Stage3**
1. Jede Zone hat eine zuständige Farbe. Der Weg wird erst sicher, wenn diese Farbe zuerst durchgeht! Haltet unbedingt die Reihenfolge ein!

**T.Stage3**
1. Passt auf den Boden auf. Das sieht nach Reflux aus — da steigt Magensäure hoch!
2. Aus unsichtbaren Stellen kommen Stachelangriffe. Bleibt immer wachsam.
3. Die Wände an beiden Seiten werden schwarz-weiß. Pass die Farbe an und ramm gegen die Wand, um den Druck zurückzudrängen!

**T.Stage4**
1. Sieht nach einer allergischen Reaktion aus — überall in der Speiseröhre sind Schwellungen entstanden. Berührt ihr eine davon, werdet ihr zurückgeschleudert.

**T.Stage5 — Stage1**
1. Fangt die roten Blutkörperchen, die hier herumlaufen — lasst uns diesem Körper Schaden zufügen. Wir können nicht einfach nur einstecken!

**T.Stage5 — Stage3**
1. Weil wir die roten Blutkörperchen gefangen haben, halten uns die weißen Blutkörperchen jetzt für Feinde! Rennt und überlebt!
2. Berührt ihr eine Wand, werdet ihr zurückgeschleudert. Vielleicht können wir das nutzen, um zu entkommen, was?

**T.Boss — Intro**
1. Das ist der letzte Abschnitt der Speiseröhre... da unten muss es voller Magensäure sein. Unvorbereitet runterzugehen ist viel zu riskant.
2. Das Bonbon von vorhin kommt von der Decke herab und wird immer größer. Wir müssen einen Boden bauen, bevor es unten aufschlägt — sonst werden wir mit runtergerissen.
3. Schafft man jeden Abschnitt, entsteht ein Stück Boden. Schnell, baut ihn, um das Ding zu stoppen!

**T.Boss — Bossdown**
1. Puh... gerade noch geschafft, es zu stoppen... jetzt lasst uns überlegen, wie wir hier sicher runterkommen.
2. Schluck... schluck... was ist das für ein Geräusch...?
3. Sein Hals ist verstopft, und es fängt an, Wasser zu trinken! Das Bonbon zerschmettert den Boden!!
4. AAAAAAAAAH...!!

### PT-BR

**T.Stage1**
1. A gente foi engolido e acabou no esôfago... Já que é assim, só resta descer e escapar.
2. O esôfago vai se contrair em intervalos aleatórios. Quando aparecer o aviso, gritem todos juntos o grito do time pra parar isso!
3. Paredes coloridas vão avançar dos dois lados. Se uma parede mostrar sua cor, bate direto nela — ela vai recuar.
4. ...esse barulho de coisa rolando? Não me diga que é aquela bala rolando de novo?! Corram se não quiserem ser esmagados!!

**T.Stage2 — Stage1**
1. Decorem só os blocos que brilham. Se pisarem no errado, é morte instantânea.
2. Um arroto ficou entalado no esôfago... o gás está começando a se acumular. Se ficar tão denso que não dá pra ver nada, gritem o grito do time pra limpar isso.

**T.Stage2 — Stage2**
1. Agora vai mostrar por cor. Você tem que acertar a sua própria cor! Mesmo se a cor bater, se estiver em preto e branco, você morre do mesmo jeito.

**T.Stage2 — Stage3**
1. Cada área tem uma cor responsável. O caminho só fica seguro depois que essa cor passar primeiro! Respeitem bem a ordem!

**T.Stage3**
1. Cuidado com o chão. Deve ser refluxo — tá subindo ácido do estômago!
2. Vão vir ataques de espinho de lugares que a gente não vê. Fiquem sempre atentos.
3. As paredes dos dois lados vão virar preto e branco. Acerte a cor e bata na parede pra empurrar a pressão de volta!

**T.Stage4**
1. Deve ser uma reação alérgica — surgiram inchaços por todo o esôfago. Se baterem em um desses, vão ser lançados pra trás.

**T.Stage5 — Stage1**
1. Vão atrás dos glóbulos vermelhos que estão correndo por aí, vamos dar um dano nesse corpo. A gente não pode só ficar levando pancada!

**T.Stage5 — Stage3**
1. Depois que capturamos os glóbulos vermelhos, os glóbulos brancos passaram a nos marcar como inimigos! Corram e sobrevivam!
2. Se baterem numa parede, vão ser lançados pra trás. Dá pra usar isso pra fugir, né?

**T.Boss — Intro**
1. Esse é o último trecho do esôfago... aí embaixo deve estar cheio de ácido do estômago. Descer sem se preparar é arriscado demais.
2. Aquela bala de antes tá descendo do teto, e cada vez maior. A gente precisa construir um chão antes que ela toque o fundo — senão vamos ser arrastados junto.
3. Cada trecho que a gente vence gera uma parte do chão. Rápido, construam isso pra bloquear aquilo!

**T.Boss — Bossdown**
1. Ufa... quase não deu, mas paramos... agora vamos pensar. Como descer daqui em segurança.
2. Glup... glup... que som é esse...?
3. A garganta dele entupiu e ele começou a beber água! A bala tá quebrando o chão!!
4. AAAAAAAAAH...!!

### RU

**T.Stage1**
1. Нас проглотили, и мы оказались в пищеводе... В такой ситуации остаётся только спускаться вниз и искать выход.
2. Пищевод будет периодически сжиматься. Как только появится предупреждение, кричите все вместе командный клич, чтобы остановить это!
3. С обеих сторон на нас будут надвигаться цветные стены. Если стена показывает твой цвет — врезайся в неё прямо, она отступит назад.
4. ...это что, что-то катится? Только не говорите, что это та самая конфета?! Бегите, если не хотите быть раздавленными!!

**T.Stage2 — Stage1**
1. Запоминайте только светящиеся плитки. Наступишь на неправильную — мгновенная смерть.
2. Отрыжка застряла в пищеводе... начал скапливаться газ. Если станет так густо, что ничего не видно, кричите командный клич, чтобы его развеять.

**T.Stage2 — Stage2**
1. На этот раз плитки будут показаны по цветам. Обязательно совпади со своим цветом! Даже если цвет верный, при чёрно-белом режиме всё равно умрёшь.

**T.Stage2 — Stage3**
1. У каждой зоны есть свой цвет-ответственный. Путь станет безопасным только после того, как этот цвет пройдёт первым! Обязательно соблюдайте порядок!

**T.Stage3**
1. Осторожно с полом. Похоже на рефлюкс — это желудочный сок поднимается!
2. Атаки шипами будут прилетать из невидимых мест. Будьте всегда начеку.
3. Стены с обеих сторон станут чёрно-белыми. Совпади цветом и врежься в стену, чтобы оттолкнуть напирающее давление!

**T.Stage4**
1. Похоже на аллергическую реакцию — по всему пищеводу появились отёки. Столкнёшься с одним из них — тебя отбросит.

**T.Stage5 — Stage1**
1. Ловите эритроциты, которые бегают повсюду — давайте нанесём урон этому телу. Мы не можем всё время только получать!

**T.Stage5 — Stage3**
1. После того как мы поймали эритроциты, лейкоциты приняли нас за врагов! Бегите и выживайте!
2. Врежешься в стену — тебя отбросит назад. Можно этим воспользоваться, чтобы сбежать, да?

**T.Boss — Intro**
1. Это последний участок пищевода... там внизу наверняка всё заполнено желудочным соком. Спускаться без подготовки слишком опасно.
2. Та самая конфета опускается с потолка сверху и становится всё больше. Нужно построить пол, пока она не коснулась дна — иначе нас снесёт вместе с ней.
3. За каждый пройденный участок появляется часть пола. Быстрее строим его, чтобы остановить эту штуку!

**T.Boss — Bossdown**
1. Уф... еле остановили... теперь подумаем. Как безопасно спуститься отсюда.
2. Буль... буль... что это за звук...?
3. У него забило горло, и оно начало пить воду! Конфета проламывает пол!!
4. А-А-А-А-А-А-А-А-А...!!

### PL

**T.Stage1**
1. Zostaliśmy połknięci i wylądowaliśmy w przełyku... W takiej sytuacji nie ma innego wyjścia, jak zejść niżej i uciec.
2. Przełyk będzie się ściskał w losowych odstępach. Jak tylko pojawi się ostrzeżenie, krzyczcie wszyscy okrzyk drużyny, żeby to zatrzymać!
3. Z obu stron będą napierać kolorowe ściany. Jeśli ściana pokazuje twój kolor, walnij w nią prosto — odskoczy do tyłu.
4. ...ten dźwięk czegoś, co się kotłuje? Tylko nie mówcie, że to ten sam cukierek?! Biegnijcie, jeśli nie chcecie zostać rozgnieceni!!

**T.Stage2 — Stage1**
1. Zapamiętajcie tylko świecące płytki. Nadepniecie na złą — to natychmiastowa śmierć.
2. Odbicie zatrzymało się w przełyku... gaz zaczyna się gromadzić. Jak zrobi się tak gęsto, że nic nie widać, krzyczcie okrzyk drużyny, żeby go rozgonić.

**T.Stage2 — Stage2**
1. Tym razem będzie pokazywać po kolorach. Musisz koniecznie trafić w swój kolor! Nawet jeśli kolor się zgadza, i tak zginiesz, jeśli będzie czarno-białe.

**T.Stage2 — Stage3**
1. Każda strefa ma przypisany kolor. Droga staje się bezpieczna tylko wtedy, gdy ten kolor przejdzie pierwszy! Koniecznie trzymajcie się kolejności!

**T.Stage3**
1. Uważajcie na podłogę. Musi mieć refluks — podchodzi kwas żołądkowy!
2. Ataki kolcami będą nadchodzić z miejsc, których nie widać. Bądźcie stale czujni.
3. Ściany po obu stronach zmienią się na czarno-białe. Dopasuj kolor i uderz w ścianę, żeby odeprzeć napór!

**T.Stage4**
1. To pewnie reakcja alergiczna — w całym przełyku powstały obrzęki. Uderzycie w jeden z nich, i zostaniecie odrzuceni.

**T.Stage5 — Stage1**
1. Łapcie krwinki czerwone, które biegają wokół — zadajmy temu ciału jakieś obrażenia. Nie możemy tylko ciągle oberwać!

**T.Stage5 — Stage3**
1. Skoro złapaliśmy krwinki czerwone, krwinki białe uznały nas za wrogów! Biegnijcie i przetrwajcie!
2. Uderzysz w ścianę i zostaniesz odrzucony. Może da się to wykorzystać, żeby uciec, co?

**T.Boss — Intro**
1. To ostatni odcinek przełyku... tam w dole musi być pełno kwasu żołądkowego. Zejście bez przygotowania jest zbyt ryzykowne.
2. Ten cukierek z wcześniej opada z sufitu i robi się coraz większy. Musimy zbudować podłogę, zanim dotknie dna — inaczej zniesie nas razem z nim.
3. Za każdy pokonany odcinek powstaje kawałek podłogi. Szybko, budujcie ją, żeby to zablokować!

**T.Boss — Bossdown**
1. Uff... jakoś udało się to zatrzymać... teraz pomyślmy. Jak bezpiecznie zejść stąd.
2. Chlup... chlup... co to za dźwięk...?
3. Gardło mu się zatkało i zaczęło pić wodę! Cukierek rozwala podłogę!!
4. AAAAAAAAAH...!!

---

## 열려 있는 항목 (다음 작업)

- [x] M-Stage(1~5+Boss) 한국어 대사 확정
- [x] M-Stage 11개 언어 번역 초안 작성 (`M-Stage 번역 (Localization Draft)` 섹션) — JA/ZH/RU/PL 원어민 검수 권장
- [x] M-Stage 번역을 `Dialogue` String Table(ko 포함 12개 locale)에 반영
- [x] M-Stage 씬 `DialogueUI` / `LocalizeStringEvent` MCP 연결 (2026-09-08)
- [x] T-Stage(1~5+Boss) 한국어 대사 확정
- [x] T-Stage 11개 언어 번역 초안 작성 (`T-Stage 번역 (Localization Draft)` 섹션) — JA/ZH/RU/PL 원어민 검수 권장
- [x] T-Stage 번역을 `Dialogue` String Table에 반영
- [x] T-Stage 씬 `DialogueUI` / `LocalizeStringEvent` MCP 연결 (2026-09-08)

### M-Stage 씬 연결 결과 (MCP, 2026-09-08)

| 씬 | 게이트 `showOnceKey` | 패널 | 키 |
|---|---|---|---|
| M.Stage1 | `Stage1` | `Dialogue_Panel` | `M.Stage1.Line1`~`Line5` |
| M.Stage2 | `Stage2.1` / `Stage2.2` (신규 게이트) | `Dialogue_Panel` / `Dialogue_Panel (1)` | `M.Stage2.Stage1.Line1`~`3` / `M.Stage2.Stage2.Line1`~`2` |
| M.Stage3 | `Stage3.1` | `Dialogue_Panel` | `M.Stage3.Line1`~`Line2` |
| M.Stage4 | `Stage4.1` / `Stage4.2` / `Stage4.3` | `Dialogue_Panel` / `(2)` / `(1)` | `Stage1.Line1`~`3` / `Stage2.Line1` / `Stage3.Line1`~`3` |
| M.Stage5 | `Stage5.1` | `Dialogue_Panel` | `M.Stage5.Line1` |
| M.Boss | `Boss.Intro` / `Bossdown` | `Dialogue_Panel` / `(1)` | `Intro.Line1` / `Bossdown.Line1`~`3` |

M.Stage2: `StageStartGate2.armOnStart`를 false로 바꾸고, 2.1 대화 완료 후 `Arm()`. 2.2 대화 완료 후 `StageManager2.2.StartStage()`.
M.Stage4: 4.2/4.3 대화 완료 후 각각 `StageManger4.2`/`4.3`.StartStage(). 4.2의 `Start.SetActive`는 phase enter에 유지.

### T-Stage 씬 연결 결과 (MCP, 2026-09-08)

| 씬 | 게이트 `showOnceKey` | 패널 | 키 |
|---|---|---|---|
| T.Stage1 | `Stage1` | `Dialogue_Panel` | `T.Stage1.Line1`~`Line4` |
| T.Stage2 | `Stage2.1` / `Stage2.2` / `Stage2.3` | `Dialogue_Panel` / `(1)` / `(2)` | `Stage1.Line1`~`2` / `Stage2.Line1` / `Stage3.Line1` |
| T.Stage3 | `Stage3.1` (신규 게이트) | `Dialogue_Panel` | `T.Stage3.Line1`~`Line3` |
| T.Stage4 | `Stage4.1` (신규 게이트) | `Dialogue_Panel` | `T.Stage4.Line1` |
| T.Stage5 | `Stage5.1` / `Stage5.3` | `Dialogue_Panel` / `(1)` | `Stage1.Line1` / `Stage3.Line1`~`2` |
| T.Boss | `Boss.Intro` / `Bossdown` | `Dialogue_Panel` / `(1)` | `Intro.Line1`~`3` / `Bossdown.Line1`~`4` |

T.Stage3·T.Stage4: 대화 끝날 때까지 시작 게이트가 안 켜지도록 `armOnStart=false`, 대화 완료 후 `Arm()`. Phase enter는 `StartStage`/`Arm` 대신 `PhaseDialogueGate.Begin`.
T.Boss: 기존 게이트에 `dialogueUI`가 비어 있어서 Intro=`Dialogue_Panel`, Bossdown=`Dialogue_Panel (1)`로 연결.
