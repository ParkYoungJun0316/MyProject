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

1. 왜 맛있는 과일들을 빼고 날 먹은 거야!!
2. 이제부터 생존만 생각해.
3. 주위에 있는 색깔 입들 위치를 잘 기억해둬.  
발판을 밟으면 그 색 입이 솟아올라서 이빨을 부숴줄 거야.
4. 한 번에 한 색만 올라와.  
다 같이 밟아봤자 소용없어.
5. 거대한 입이 닫히려고 하면 다 같이 팀 구호를 외쳐.  
안 그러면 앞이 어두워질 거야.

*(색 패드/응원 설명은 `2.Tutorial` 씬 담당 — M.Stage1에서 재설명하지 않음. **단, 은신(Stealth)은 예외** — Tutorial 구역 1이 생략 가능인 데다 실사용처가 `M.Stage2`(2.2)·`M.Stage4`(4.3)·`M.Boss`(1페이즈)뿐으로 희소해서, Tutorial 단독 교육을 신뢰하지 않고 **첫 실사용 스테이지(`M.Stage2` 2.2)에서 다시 짚어준다**(아래 참고). `CheerAndTutorialDesign.md` §4 참고.)*

*(3번 "색깔 입" = `DirectionalBarrier`/`MouthBarrier` 프리팹(`DoorController`). 5번 "거대한 입" = `MouthController`(방 전체 닫힘, 소리 초출) — 수식어(색깔/거대한)로 구분해 헷갈리지 않게 함. 2026-09-24 개정: 1번 꿀떡 명시·2번 속담 삭제·5번 "여기"→"거대한 입".)*

## M.Stage2 (`SideSplitChallenge` — 2.1 SideSplit+침 / 2.2 Drop+침)

**2.1 — SideSplit 첫 등장 + 침 초출**
1. 꿀떡 수만큼 들어가! 근데 자기 간판 꿀떡만 보이니까 서로 알려줘!
2. 날 먹기 전에 매운 걸 먹었나..?  
바닥에 침들이 자꾸 올라와.
3. 바닥에 침이 깔려 미끄러울 거야.  
다 같이 팀 구호를 외치면 없앨 수 있어.

**2.2 — 은신 첫 등장 (Drop+침 구간, `TrapPlayerTracker` 최초 사용)**
1. 이제는 위에서도 침이 떨어지잖아!  
매운 음식을 먹고 신 음식까지 먹은 게 분명해.
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
2. 발밑에서 뭔가 움직이고 있어... 조심해!  
혀 공격이 들어온다!
3. 가운데 발판을 부수고 혀가 길을 방해할 거야.  
다 같이 팀 구호를 외치면 다시 내려가고, 부서진 길도 원상복구될 거야.

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

## M.Stage5 (GridChallenge 혼합판)

1. 운동하고 있나 봐! 숨이 갑자기 거칠어졌어.
2. 바닥에 색 칸이 켜질 거야.  
네 색이 나오면 그 칸에만 서.  
안 나왔으면 검은색이나 흰색, 네 흑백에 맞춰서 버텨!

*(한 보드·한 라운드 줄. Color/BW 페이즈 분리 없음. 네 색이 나온 라운드는 그 칸만, 없으면 흑백. 캐릭터 색도 맞출 것. 2026-09-24: 3번(캐릭터 색 맞추기) 대사 삭제 — Tip 3번이 담당. 바닥 붕괴는 보면 바로 알 수 있어 대사·Tip 안내 없음.)*

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
> **번역 기준 = 영어 (2026-09-24):** 한국어 원문 → 영어 확정 → 나머지 10개 언어는 **영어에서** 옮긴다. 진행은 씬마다 ko·en만 확정하고, 나머지 언어는 전 씬이 끝난 뒤 한 번에 번역한다 — 그 전까지 ko·en 외 언어는 옛 대사가 남아 있을 수 있음. → **2026-09-24 전 씬 ko·en 확정 후 10개 언어 일괄 재번역 완료**(M·T 번역 섹션 전체 교체, String Table `Dialogue_*` 반영). 용어: 팀 구호 = ja チームの掛け声 / zh 团队口号·團隊口號 / es·es-419 grito de equipo / fr cri d'équipe / de Teamruf / pt-BR grito da equipe / ru командный клич / pl okrzyk drużyny. 사탕 = アメ / 糖果 / caramelo(es)·dulce(es-419) / bonbon / Bonbon / bala / леденец / cukierek. 입 주인 = 3인칭 남성(he/他/él/il/er/ele/он/on), ja는 주어 생략. 말하는 사람(꿀떡) 1인칭 ja = わたし.
>
> ~~속담(M.Stage1-2) 직역 규칙~~ — 2026-09-24 M.Stage1-2에서 속담 삭제로 폐기.
>
> ⚠️ 11개 언어 전체를 1차로 초안 작성한 것으로, 특히 JA/ZH/RU/PL은 원어민 검수 권장.

### EN

**M.Stage1**
1. Why'd you skip all the tasty fruit and eat me instead?!
2. From now on, just focus on staying alive.
3. Remember where the colored mouths around you are.  
Step on the pad, and that color's mouth will rise up and smash the teeth.
4. Only one color rises at a time.  
Stepping on it together won't help.
5. When the giant mouth starts to close, everyone shout the team cheer!  
Otherwise everything will go dark.

**M.Stage2 — 2.1**
1. Match each zone's KKUL-TTEOK count! You only see your own sign's, so call them out!
2. Did he eat something spicy before he ate me..?  
Drool keeps bubbling up from the floor.
3. All this drool's gonna make the floor slippery.  
Shout the team cheer together and we can clear it!

**M.Stage2 — 2.2**
1. Now it's dripping from above too!  
He must've eaten something spicy and sour.
2. Some of the falling drool is targeting me!  
Match my color to the floor with <color=#FFD24D><b>Ctrl</b></color> and it'll lose track of me.

**M.Stage3**
1. Colored tiles are gonna pop up on the floor.  
Stand on one for 2 seconds to score a point.
2. Anyone can use white or black tiles, but a colored tile only counts for that color.  
Stay out of each other's way and move fast!

**M.Stage4 — 4.1**
1. When your color shows up on the floor, hit <color=#FFD24D><b>Space</b></color>.  
Anyone can hit white, but don't touch black!  
Black passes to the next turn on its own after 1 second.
2. Something's moving under our feet... watch out!  
Here comes the tongue!
3. The tongue's gonna smash the middle platform and block the way.  
Shout the team cheer together and it'll go back down — the broken path will be restored, too.

**M.Stage4 — 4.2**
1. From now on, you'll only see one tile ahead.  
Make sure you remember it!

**M.Stage4 — 4.3**
1. Something's targeting me here too!  
Match my color to the floor with <color=#FFD24D><b>Ctrl</b></color> and it'll lose track of me.
2. This time, the tongue's gonna smash the whole left or right side of the floor.  
Shout the team cheer to stop it — if you don't, it's gonna break.
3. The floor only comes back if you shout the team cheer while the warning's up.  
Once it's broken, you'll just have to hang on until the next warning.

**M.Stage5**
1. Is he working out or something? His breathing suddenly got rough!
2. Colored tiles are gonna light up on the floor.  
If your color shows up, stand on that tile only.  
If it doesn't, hang on to black or white — whichever matches you!

**M.Boss — Intro**
1. Haah... haah... just a little more and we might actually make it out alive...

**M.Boss — Bossdown**
1. ...It's gone quiet in here.  
Does that mean we can finally escape...?
2. .....(CRUNCH!)
3. Oh no, we're being swallowed...!!

### JA

**M.Stage1**
1. おいしい果物がたくさんあるのに、なんでわたしを食べたの！？
2. ここからは、生き残ることだけ考えて。
3. 周りにある色付きの口の場所、よく覚えておいて。  
足場を踏むと、その色の口がせり上がって歯を砕いてくれるよ。
4. 一度に上がるのは一色だけ。  
みんなで踏んでも意味ないよ。
5. 巨大な口が閉じそうになったら、みんなでチームの掛け声を叫んで！  
じゃないと、目の前が真っ暗になっちゃうよ。

**M.Stage2 — 2.1**
1. KKUL-TTEOKの数だけゾーンに入って！でも見えるのは自分の看板のKKUL-TTEOKだけだから、声をかけ合って！
2. わたしを食べる前に、辛いものでも食べたのかな…？  
床からよだれがどんどん湧いてくる。
3. よだれで床がツルツルになっちゃう。  
みんなでチームの掛け声を叫べば、消せるよ！

**M.Stage2 — 2.2**
1. 今度は上からも垂れてきた！  
辛いものと酸っぱいものを食べたに違いないよ。
2. 落ちてくるよだれの中に、わたしを狙ってるのがある！  
<color=#FFD24D><b>Ctrl</b></color>でわたしの色を床に合わせれば、見失うはず。

**M.Stage3**
1. 床に色付きのタイルが出てくるよ。  
2秒乗っていれば1点！
2. 白と黒のタイルは誰でもOK。でも色付きのタイルは、その色の人しか点にならないよ。  
お互い邪魔しないように、素早く動いて！

**M.Stage4 — 4.1**
1. 床に自分の色が出たら<color=#FFD24D><b>Space</b></color>！  
白は誰が押してもいいけど、黒は押しちゃダメ！  
黒は1秒たつと勝手に次の番に進むよ。
2. 足の下で何か動いてる…気をつけて！  
舌が来るよ！
3. 舌が真ん中の足場を壊して、道をふさいじゃう。  
みんなでチームの掛け声を叫べば引っ込んで、壊れた道も元に戻るよ。

**M.Stage4 — 4.2**
1. ここからは、1マス先の床しか見えないよ。  
しっかり覚えてね！

**M.Stage4 — 4.3**
1. ここにもわたしを狙ってるのがいる！  
<color=#FFD24D><b>Ctrl</b></color>でわたしの色を床に合わせれば、見失うはず。
2. 今度は舌が、床の左か右を丸ごと壊しにくるよ。  
チームの掛け声を叫べば止められる。叫べなかったら、そのまま壊れちゃう。
3. 床が元に戻るのは、警告が出ている間にチームの掛け声を叫んだときだけ。  
壊れちゃったら、次の警告まで耐えるしかないよ。

**M.Stage5**
1. 運動でもしてるのかな？急に息が荒くなった！
2. 床に色付きのマスが光るよ。  
自分の色が出たら、そのマスにだけ乗って。  
出なかったら、黒か白、自分に合うほうで耐えて！

**M.Boss — Intro**
1. はぁ…はぁ…もう少し耐えれば、生きて出られそう…

**M.Boss — Bossdown**
1. …口の中が静かになった。  
やっと脱出できるのかな…？
2. …………（ガリッ！）
3. まずい、飲み込まれる…！！

### ZH-Hans

**M.Stage1**
1. 那么多好吃的水果不吃，干嘛偏偏吃我啊！？
2. 从现在起，只管想着怎么活下去。
3. 记住周围那些彩色嘴巴的位置。  
踩下踏板，那个颜色的嘴就会升起来，帮我们把牙齿砸碎。
4. 一次只会升起一种颜色。  
大家一起踩也没用。
5. 巨大的嘴要合上的时候，大家一起喊团队口号！  
不然眼前就会一片漆黑。

**M.Stage2 — 2.1**
1. 按 KKUL-TTEOK 的数量进区域！不过每个人只看得到自己牌子上的，互相报一下！
2. 他吃我之前是不是吃了辣的……？  
地上一直在冒口水。
3. 这么多口水，地板会变得很滑。  
大家一起喊团队口号就能清掉！

**M.Stage2 — 2.2**
1. 现在连上面都开始滴了！  
他肯定吃了又辣又酸的东西。
2. 掉下来的口水里有瞄准我的！  
按 <color=#FFD24D><b>Ctrl</b></color> 把我的颜色换成地板的颜色，它就找不到我了。

**M.Stage3**
1. 地上会冒出彩色方块。  
在上面站 2 秒就能得 1 分。
2. 白色和黑色方块谁都能用，但彩色方块只算那个颜色的人的分。  
别挡着彼此，动作快！

**M.Stage4 — 4.1**
1. 地上出现你的颜色时按 <color=#FFD24D><b>Space</b></color>。  
白色谁都能按，黑色千万别碰！  
黑色过 1 秒会自动轮到下一个。
2. 脚底下有什么在动……小心！  
舌头来了！
3. 舌头会砸碎中间的踏板，把路堵住。  
大家一起喊团队口号，它就会缩回去，碎掉的路也会恢复。

**M.Stage4 — 4.2**
1. 从现在起，只能看到前面一格。  
一定要记住！

**M.Stage4 — 4.3**
1. 这里也有东西在瞄准我！  
按 <color=#FFD24D><b>Ctrl</b></color> 把我的颜色换成地板的颜色，它就找不到我了。
2. 这次舌头会把左边或右边的地板整片砸掉。  
喊团队口号就能拦住，喊不出来就直接碎了。
3. 只有在警告出现时喊团队口号，地板才会恢复。  
一旦碎了，就只能撑到下一次警告。

**M.Stage5**
1. 他是在运动吗？呼吸突然变得好粗！
2. 地上会亮起彩色格子。  
出现你的颜色就只站那一格。  
没出现的话，就站在黑白里跟你对得上的那种撑住！

**M.Boss — Intro**
1. 呼……呼……再撑一下，好像就能活着出去了……

**M.Boss — Bossdown**
1. ……嘴里安静下来了。  
终于能逃出去了吗……？
2. ……（咔嚓！）
3. 糟了，要被吞下去了……！！

### ZH-Hant

**M.Stage1**
1. 那麼多好吃的水果不吃，幹嘛偏偏吃我啊！？
2. 從現在起，只管想著怎麼活下去。
3. 記住周圍那些彩色嘴巴的位置。  
踩下踏板，那個顏色的嘴就會升起來，幫我們把牙齒砸碎。
4. 一次只會升起一種顏色。  
大家一起踩也沒用。
5. 巨大的嘴要合上的時候，大家一起喊團隊口號！  
不然眼前就會一片漆黑。

**M.Stage2 — 2.1**
1. 按 KKUL-TTEOK 的數量進區域！不過每個人只看得到自己牌子上的，互相報一下！
2. 他吃我之前是不是吃了辣的……？  
地上一直在冒口水。
3. 這麼多口水，地板會變得很滑。  
大家一起喊團隊口號就能清掉！

**M.Stage2 — 2.2**
1. 現在連上面都開始滴了！  
他肯定吃了又辣又酸的東西。
2. 掉下來的口水裡有瞄準我的！  
按 <color=#FFD24D><b>Ctrl</b></color> 把我的顏色換成地板的顏色，它就找不到我了。

**M.Stage3**
1. 地上會冒出彩色方塊。  
在上面站 2 秒就能得 1 分。
2. 白色和黑色方塊誰都能用，但彩色方塊只算那個顏色的人的分。  
別擋著彼此，動作快！

**M.Stage4 — 4.1**
1. 地上出現你的顏色時按 <color=#FFD24D><b>Space</b></color>。  
白色誰都能按，黑色千萬別碰！  
黑色過 1 秒會自動輪到下一個。
2. 腳底下有什麼在動……小心！  
舌頭來了！
3. 舌頭會砸碎中間的踏板，把路堵住。  
大家一起喊團隊口號，它就會縮回去，碎掉的路也會恢復。

**M.Stage4 — 4.2**
1. 從現在起，只能看到前面一格。  
一定要記住！

**M.Stage4 — 4.3**
1. 這裡也有東西在瞄準我！  
按 <color=#FFD24D><b>Ctrl</b></color> 把我的顏色換成地板的顏色，它就找不到我了。
2. 這次舌頭會把左邊或右邊的地板整片砸掉。  
喊團隊口號就能攔住，喊不出來就直接碎了。
3. 只有在警告出現時喊團隊口號，地板才會恢復。  
一旦碎了，就只能撐到下一次警告。

**M.Stage5**
1. 他是在運動嗎？呼吸突然變得好粗！
2. 地上會亮起彩色格子。  
出現你的顏色就只站那一格。  
沒出現的話，就站在黑白裡跟你對得上的那種撐住！

**M.Boss — Intro**
1. 呼……呼……再撐一下，好像就能活著出去了……

**M.Boss — Bossdown**
1. ……嘴裡安靜下來了。  
終於能逃出去了嗎……？
2. ……（咔嚓！）
3. 糟了，要被吞下去了……！！

### ES

**M.Stage1**
1. ¡¿Con toda la fruta rica que había, por qué me has comido a mí?!
2. A partir de ahora, pensad solo en sobrevivir.
3. Fijaos bien en dónde están las bocas de colores.  
Si pisáis la placa, la boca de ese color se alzará y hará pedazos los dientes.
4. Solo sube un color cada vez.  
No sirve de nada que piséis todos a la vez.
5. Cuando la boca gigante empiece a cerrarse, ¡gritad todos el grito de equipo!  
Si no, todo se quedará a oscuras.

**M.Stage2 — 2.1**
1. ¡En cada zona tiene que haber tantos como KKUL-TTEOK! Cada uno solo ve los de su cartel, ¡así que avisaos!
2. ¿Habrá comido algo picante antes de comerme…?  
No para de salir baba del suelo.
3. Con tanta baba, el suelo va a resbalar.  
¡Si gritamos juntos el grito de equipo, podemos quitarla!

**M.Stage2 — 2.2**
1. ¡Ahora también gotea desde arriba!  
Seguro que ha comido algo picante y ácido.
2. ¡Algunas gotas me están apuntando!  
Iguala mi color con el del suelo con <color=#FFD24D><b>Ctrl</b></color> y me perderán de vista.

**M.Stage3**
1. Van a aparecer baldosas de colores en el suelo.  
Quédate 2 segundos encima de una para sumar un punto.
2. Las baldosas blancas y negras valen para cualquiera, pero las de color solo cuentan para ese color.  
¡No os estorbéis y moveos rápido!

**M.Stage4 — 4.1**
1. Cuando salga tu color en el suelo, pulsa <color=#FFD24D><b>Space</b></color>.  
El blanco lo puede pulsar cualquiera, ¡pero el negro ni tocarlo!  
El negro pasa solo al siguiente turno al cabo de 1 segundo.
2. Algo se mueve bajo nuestros pies… ¡cuidado!  
¡Ahí viene la lengua!
3. La lengua va a romper la plataforma del centro y cortar el paso.  
Si gritamos juntos el grito de equipo, bajará y el camino roto volverá a estar como antes.

**M.Stage4 — 4.2**
1. A partir de ahora solo verás la siguiente baldosa.  
¡Memorízala bien!

**M.Stage4 — 4.3**
1. ¡Aquí también hay algo apuntándome!  
Iguala mi color con el del suelo con <color=#FFD24D><b>Ctrl</b></color> y me perderá de vista.
2. Esta vez la lengua va a destrozar todo el lado izquierdo o derecho del suelo.  
Gritad el grito de equipo para pararla; si no, se romperá.
3. El suelo solo vuelve si gritáis el grito de equipo mientras está el aviso.  
Si ya se ha roto, tocará aguantar hasta el siguiente aviso.

**M.Stage5**
1. ¿Estará haciendo ejercicio o qué? ¡De repente respira con fuerza!
2. Se van a iluminar casillas de colores en el suelo.  
Si sale tu color, ponte solo en esa casilla.  
Si no, aguanta en la negra o la blanca, ¡la que coincida contigo!

**M.Boss — Intro**
1. Uf… uf… un poco más y a lo mejor salimos vivos de aquí…

**M.Boss — Bossdown**
1. …Aquí dentro se ha quedado todo en silencio.  
¿Será que por fin podemos escapar…?
2. …(¡CRAC!)
3. ¡Oh, no, nos está tragando…!!

### ES-419

**M.Stage1**
1. ¡¿Con toda la fruta rica que había, por qué me comiste a mí?!
2. De ahora en adelante, piensen solo en sobrevivir.
3. Fíjense bien dónde están las bocas de colores.  
Si pisan la placa, la boca de ese color se va a levantar y hará pedazos los dientes.
4. Solo sube un color a la vez.  
No sirve de nada que pisen todos juntos.
5. Cuando la boca gigante empiece a cerrarse, ¡griten todos el grito de equipo!  
Si no, todo se va a quedar a oscuras.

**M.Stage2 — 2.1**
1. ¡En cada zona tiene que haber tantos como KKUL-TTEOK! Cada uno solo ve los de su letrero, ¡así que avísense!
2. ¿Habrá comido algo picante antes de comerme…?  
No para de salir baba del piso.
3. Con tanta baba, el piso va a quedar resbaloso.  
¡Si gritamos juntos el grito de equipo, podemos quitarla!

**M.Stage2 — 2.2**
1. ¡Ahora también gotea desde arriba!  
Seguro que comió algo picante y ácido.
2. ¡Algunas gotas me están apuntando!  
Iguala mi color con el del piso con <color=#FFD24D><b>Ctrl</b></color> y me van a perder de vista.

**M.Stage3**
1. Van a aparecer baldosas de colores en el piso.  
Quédate 2 segundos encima de una para sumar un punto.
2. Las baldosas blancas y negras sirven para cualquiera, pero las de color solo cuentan para ese color.  
¡No se estorben y muévanse rápido!

**M.Stage4 — 4.1**
1. Cuando salga tu color en el piso, presiona <color=#FFD24D><b>Space</b></color>.  
El blanco lo puede presionar cualquiera, ¡pero el negro ni lo toquen!  
El negro pasa solo al siguiente turno después de 1 segundo.
2. Algo se mueve bajo nuestros pies… ¡cuidado!  
¡Ahí viene la lengua!
3. La lengua va a romper la plataforma del centro y cortar el paso.  
Si gritamos juntos el grito de equipo, va a bajar y el camino roto va a volver a estar como antes.

**M.Stage4 — 4.2**
1. De ahora en adelante solo vas a ver la siguiente baldosa.  
¡Memorízala bien!

**M.Stage4 — 4.3**
1. ¡Aquí también hay algo apuntándome!  
Iguala mi color con el del piso con <color=#FFD24D><b>Ctrl</b></color> y me va a perder de vista.
2. Esta vez la lengua va a destrozar todo el lado izquierdo o derecho del piso.  
Griten el grito de equipo para detenerla; si no, se va a romper.
3. El piso solo vuelve si gritan el grito de equipo mientras está la advertencia.  
Si ya se rompió, les va a tocar aguantar hasta la siguiente advertencia.

**M.Stage5**
1. ¿Estará haciendo ejercicio o qué? ¡De repente está respirando fuerte!
2. Se van a iluminar casillas de colores en el piso.  
Si sale tu color, ponte solo en esa casilla.  
Si no, aguanta en la negra o la blanca, ¡la que coincida contigo!

**M.Boss — Intro**
1. Uf… uf… un poco más y tal vez salgamos vivos de aquí…

**M.Boss — Bossdown**
1. …Aquí adentro se quedó todo en silencio.  
¿Será que por fin podemos escapar…?
2. …(¡CRAC!)
3. ¡Oh, no, nos está tragando…!!

### FR

**M.Stage1**
1. Il y avait plein de bons fruits… pourquoi c'est moi que tu as mangé ?!
2. À partir de maintenant, pensez juste à survivre.
3. Retenez bien où sont les bouches colorées autour de vous.  
Marchez sur la dalle, et la bouche de cette couleur surgira pour briser les dents.
4. Une seule couleur monte à la fois.  
Ça ne sert à rien de marcher dessus tous ensemble.
5. Quand la bouche géante commence à se fermer, criez tous le cri d'équipe !  
Sinon, tout va devenir noir.

**M.Stage2 — 2.1**
1. Autant de joueurs par zone que de KKUL-TTEOK ! Chacun ne voit que ceux de son panneau, alors annoncez-les !
2. Il a mangé un truc épicé avant de me manger… ?  
La bave n'arrête pas de remonter du sol.
3. Avec toute cette bave, le sol va glisser.  
Crions le cri d'équipe ensemble et on pourra la faire disparaître !

**M.Stage2 — 2.2**
1. Maintenant, ça coule aussi d'en haut !  
Il a dû manger un truc épicé et acide.
2. Certaines gouttes me visent !  
Accorde ma couleur au sol avec <color=#FFD24D><b>Ctrl</b></color> et elles me perdront de vue.

**M.Stage3**
1. Des dalles colorées vont apparaître au sol.  
Reste 2 secondes dessus pour marquer un point.
2. Les dalles blanches et noires, c'est pour tout le monde, mais une dalle colorée ne compte que pour sa couleur.  
Ne vous gênez pas et bougez vite !

**M.Stage4 — 4.1**
1. Quand ta couleur apparaît au sol, appuie sur <color=#FFD24D><b>Space</b></color>.  
Le blanc, tout le monde peut appuyer, mais surtout pas le noir !  
Le noir passe tout seul au tour suivant au bout d'1 seconde.
2. Quelque chose bouge sous nos pieds… attention !  
Voilà la langue !
3. La langue va briser la plateforme du milieu et bloquer le passage.  
Criez le cri d'équipe ensemble et elle redescendra — le chemin cassé reviendra aussi.

**M.Stage4 — 4.2**
1. À partir de maintenant, tu ne verras que la case suivante.  
Retiens-la bien !

**M.Stage4 — 4.3**
1. Ici aussi, quelque chose me vise !  
Accorde ma couleur au sol avec <color=#FFD24D><b>Ctrl</b></color> et ça me perdra de vue.
2. Cette fois, la langue va détruire tout le côté gauche ou droit du sol.  
Criez le cri d'équipe pour l'arrêter — sinon, il va se briser.
3. Le sol ne revient que si vous criez le cri d'équipe pendant l'alerte.  
Une fois cassé, il faudra tenir jusqu'à la prochaine alerte.

**M.Stage5**
1. Il fait du sport ou quoi ? Sa respiration s'est emballée d'un coup !
2. Des cases colorées vont s'allumer au sol.  
Si ta couleur apparaît, mets-toi uniquement sur cette case.  
Sinon, tiens bon sur le noir ou le blanc — celui qui te correspond !

**M.Boss — Intro**
1. Hah… hah… encore un petit effort et on pourrait vraiment s'en sortir vivants…

**M.Boss — Bossdown**
1. …C'est devenu calme ici.  
On va enfin pouvoir s'échapper… ?
2. …(CRAC !)
3. Oh non, on se fait avaler… !!

### DE

**M.Stage1**
1. Da gibt's so viel leckeres Obst, und du isst ausgerechnet mich?!
2. Ab jetzt zählt nur noch eins: überleben.
3. Merkt euch, wo die bunten Mäuler um euch herum sind.  
Tretet auf die Platte, dann schießt das Maul in der Farbe hoch und zertrümmert die Zähne.
4. Es fährt immer nur eine Farbe hoch.  
Zusammen draufzutreten bringt nichts.
5. Wenn sich das riesige Maul schließt, ruft alle den Teamruf!  
Sonst wird alles dunkel.

**M.Stage2 — 2.1**
1. Pro Zone so viele Leute, wie KKUL-TTEOK da sind! Jeder sieht nur die auf seinem Schild, also sagt sie an!
2. Hat er was Scharfes gegessen, bevor er mich gegessen hat…?  
Aus dem Boden quillt ständig Sabber hoch.
3. Bei so viel Sabber wird der Boden rutschig.  
Wenn wir zusammen den Teamruf rufen, kriegen wir ihn weg!

**M.Stage2 — 2.2**
1. Jetzt tropft's auch noch von oben!  
Er muss was Scharfes und Saures gegessen haben.
2. Ein paar Tropfen zielen auf mich!  
Pass meine Farbe mit <color=#FFD24D><b>Ctrl</b></color> an den Boden an, dann verlieren sie mich.

**M.Stage3**
1. Gleich tauchen bunte Kacheln auf dem Boden auf.  
Bleib 2 Sekunden drauf, dann gibt's einen Punkt.
2. Weiße und schwarze Kacheln gehen für alle, bunte zählen nur für ihre Farbe.  
Steht euch nicht im Weg und macht schnell!

**M.Stage4 — 4.1**
1. Wenn deine Farbe am Boden auftaucht, drück <color=#FFD24D><b>Space</b></color>.  
Weiß darf jeder drücken, aber Finger weg von Schwarz!  
Schwarz geht nach 1 Sekunde von selbst weiter.
2. Unter unseren Füßen bewegt sich was… pass auf!  
Da kommt die Zunge!
3. Die Zunge zertrümmert die mittlere Plattform und versperrt den Weg.  
Ruft zusammen den Teamruf, dann zieht sie sich zurück – und der kaputte Weg kommt auch wieder.

**M.Stage4 — 4.2**
1. Ab jetzt siehst du nur noch ein Feld voraus.  
Merk es dir gut!

**M.Stage4 — 4.3**
1. Auch hier zielt was auf mich!  
Pass meine Farbe mit <color=#FFD24D><b>Ctrl</b></color> an den Boden an, dann verliert es mich.
2. Diesmal zertrümmert die Zunge die komplette linke oder rechte Bodenhälfte.  
Ruft den Teamruf, um sie aufzuhalten – sonst bricht alles weg.
3. Der Boden kommt nur zurück, wenn ihr den Teamruf ruft, solange die Warnung da ist.  
Ist er einmal kaputt, müsst ihr bis zur nächsten Warnung durchhalten.

**M.Stage5**
1. Macht der gerade Sport oder was? Er atmet plötzlich total schwer!
2. Gleich leuchten bunte Felder am Boden auf.  
Kommt deine Farbe, stell dich nur auf dieses Feld.  
Wenn nicht, halt dich auf Schwarz oder Weiß – je nachdem, was zu dir passt!

**M.Boss — Intro**
1. Hah… hah… nur noch ein bisschen, dann kommen wir vielleicht lebend hier raus…

**M.Boss — Bossdown**
1. …Hier drin ist es still geworden.  
Können wir endlich raus…?
2. …(KNACK!)
3. Oh nein, wir werden verschluckt…!!

### PT-BR

**M.Stage1**
1. Com tanta fruta gostosa, por que você foi me comer?!
2. De agora em diante, só pensem em sobreviver.
3. Guardem bem onde ficam as bocas coloridas em volta.  
Pisou na placa, a boca daquela cor sobe e quebra os dentes.
4. Só sobe uma cor por vez.  
Não adianta todo mundo pisar junto.
5. Quando a boca gigante começar a fechar, todo mundo grita o grito da equipe!  
Senão, vai ficar tudo escuro.

**M.Stage2 — 2.1**
1. Cada área precisa ter a mesma quantidade de gente que de KKUL-TTEOK! Cada um só vê os da própria placa, então avisem!
2. Será que ele comeu algo apimentado antes de me comer…?  
Não para de brotar baba do chão.
3. Com tanta baba, o chão vai ficar escorregadio.  
Se a gente gritar o grito da equipe junto, dá pra limpar!

**M.Stage2 — 2.2**
1. Agora tá pingando de cima também!  
Ele deve ter comido algo apimentado e azedo.
2. Algumas gotas estão mirando em mim!  
Iguale minha cor à do chão com <color=#FFD24D><b>Ctrl</b></color> e elas me perdem de vista.

**M.Stage3**
1. Vão aparecer blocos coloridos no chão.  
Fique 2 segundos em cima de um pra marcar ponto.
2. Os blocos brancos e pretos valem pra qualquer um, mas os coloridos só contam pra aquela cor.  
Não atrapalhem uns aos outros e sejam rápidos!

**M.Stage4 — 4.1**
1. Quando a sua cor aparecer no chão, aperte <color=#FFD24D><b>Space</b></color>.  
Branco qualquer um pode apertar, mas preto nem pensar!  
O preto passa sozinho pro próximo turno depois de 1 segundo.
2. Tem alguma coisa se mexendo debaixo da gente… cuidado!  
Lá vem a língua!
3. A língua vai quebrar a plataforma do meio e bloquear o caminho.  
Gritem o grito da equipe juntos que ela desce de novo — e o caminho quebrado volta também.

**M.Stage4 — 4.2**
1. De agora em diante, você só vai ver um bloco à frente.  
Grave bem!

**M.Stage4 — 4.3**
1. Aqui também tem algo mirando em mim!  
Iguale minha cor à do chão com <color=#FFD24D><b>Ctrl</b></color> e ele me perde de vista.
2. Dessa vez a língua vai destruir o lado esquerdo ou direito inteiro do chão.  
Gritem o grito da equipe pra impedir — senão, vai quebrar.
3. O chão só volta se vocês gritarem o grito da equipe enquanto o aviso estiver na tela.  
Se já quebrou, vai ter que aguentar até o próximo aviso.

**M.Stage5**
1. Ele tá malhando ou o quê? A respiração dele ficou pesada do nada!
2. Vão acender blocos coloridos no chão.  
Se a sua cor aparecer, fique só naquele bloco.  
Se não aparecer, aguente no preto ou no branco — o que combinar com você!

**M.Boss — Intro**
1. Ufa… ufa… só mais um pouco e talvez a gente saia vivo daqui…

**M.Boss — Bossdown**
1. …Ficou tudo quieto aqui dentro.  
Será que finalmente dá pra escapar…?
2. …(CRAC!)
3. Ah, não, a gente tá sendo engolido…!!

### RU

**M.Stage1**
1. Столько вкусных фруктов, а ты съел именно меня?!
2. С этого момента думайте только о том, как выжить.
3. Запомните, где вокруг стоят цветные рты.  
Наступите на плиту — рот этого цвета поднимется и раскрошит зубы.
4. За раз поднимается только один цвет.  
Наступать всем вместе бесполезно.
5. Когда огромный рот начнёт закрываться, кричите все командный клич!  
Иначе всё погрузится во тьму.

**M.Stage2 — 2.1**
1. В каждой зоне — столько игроков, сколько KKUL-TTEOK! Каждый видит только свою табличку, так что говорите вслух!
2. Он что, съел что-то острое перед тем, как съесть меня…?  
Из пола всё время сочится слюна.
3. От всей этой слюны пол станет скользким.  
Крикнем вместе командный клич — и уберём её!

**M.Stage2 — 2.2**
1. Теперь капает ещё и сверху!  
Он наверняка съел что-то острое и кислое.
2. Некоторые капли целятся в меня!  
Подгони мой цвет под пол через <color=#FFD24D><b>Ctrl</b></color> — и они меня потеряют.

**M.Stage3**
1. На полу будут появляться цветные плитки.  
Постой на одной 2 секунды — получишь очко.
2. Белые и чёрные плитки подходят всем, а цветная засчитывается только своему цвету.  
Не мешайте друг другу и двигайтесь быстрее!

**M.Stage4 — 4.1**
1. Когда на полу появится твой цвет, жми <color=#FFD24D><b>Space</b></color>.  
На белый может нажать кто угодно, а чёрный не трогай!  
Чёрный сам перейдёт к следующему ходу через 1 секунду.
2. Под ногами что-то шевелится… осторожно!  
Вот и язык!
3. Язык разобьёт центральную плиту и перекроет путь.  
Крикните вместе командный клич — он уйдёт вниз, а разрушенный путь восстановится.

**M.Stage4 — 4.2**
1. Теперь ты будешь видеть только одну плитку вперёд.  
Запоминай хорошенько!

**M.Stage4 — 4.3**
1. И здесь что-то целится в меня!  
Подгони мой цвет под пол через <color=#FFD24D><b>Ctrl</b></color> — и оно меня потеряет.
2. На этот раз язык разнесёт всю левую или правую половину пола.  
Крикните командный клич, чтобы остановить его, — иначе пол развалится.
3. Пол вернётся, только если крикнуть командный клич, пока висит предупреждение.  
Если он уже сломан — придётся продержаться до следующего.

**M.Stage5**
1. Он что, спортом занялся? Дыхание вдруг стало тяжёлым!
2. На полу будут загораться цветные клетки.  
Появился твой цвет — стой только на этой клетке.  
Если нет, держись на чёрной или белой — той, что совпадает с тобой!

**M.Boss — Intro**
1. Фух… фух… ещё чуть-чуть — и, может, выберемся живыми…

**M.Boss — Bossdown**
1. …Во рту стало тихо.  
Неужели мы наконец выберемся…?
2. …(ХРУСТЬ!)
3. О нет, нас глотают…!!

### PL

**M.Stage1**
1. Tyle pysznych owoców, a ty zjadłeś akurat mnie?!
2. Od teraz myślcie tylko o tym, żeby przeżyć.
3. Zapamiętajcie, gdzie wokół są kolorowe usta.  
Stańcie na płytce, a usta w tym kolorze wyskoczą i pokruszą zęby.
4. Naraz podnosi się tylko jeden kolor.  
Wspólne deptanie nic nie da.
5. Kiedy wielkie usta zaczną się zamykać, krzyczcie wszyscy okrzyk drużyny!  
Inaczej zrobi się zupełnie ciemno.

**M.Stage2 — 2.1**
1. W każdej strefie tylu graczy, ile jest KKUL-TTEOK! Każdy widzi tylko te na swojej tabliczce, więc mówcie na głos!
2. Zjadł coś ostrego, zanim zjadł mnie…?  
Z podłogi cały czas wypływa ślina.
3. Od tej śliny podłoga zrobi się śliska.  
Krzyknijmy razem okrzyk drużyny, to ją usuniemy!

**M.Stage2 — 2.2**
1. Teraz kapie też z góry!  
Musiał zjeść coś ostrego i kwaśnego.
2. Niektóre krople celują we mnie!  
Dopasuj mój kolor do podłogi przez <color=#FFD24D><b>Ctrl</b></color>, a stracą mnie z oczu.

**M.Stage3**
1. Na podłodze będą wyskakiwać kolorowe kafelki.  
Postój na jednym 2 sekundy, a zdobędziesz punkt.
2. Białe i czarne kafelki są dla każdego, ale kolorowy liczy się tylko dla swojego koloru.  
Nie wchodźcie sobie w drogę i ruszajcie się szybko!

**M.Stage4 — 4.1**
1. Gdy na podłodze pojawi się twój kolor, wciśnij <color=#FFD24D><b>Space</b></color>.  
Biały może wcisnąć każdy, ale czarnego nie ruszaj!  
Czarny sam przejdzie do następnej tury po 1 sekundzie.
2. Coś się rusza pod nami… uważaj!  
Nadchodzi język!
3. Język rozwali środkową platformę i zablokuje drogę.  
Krzyknijcie razem okrzyk drużyny, a schowa się z powrotem — zniszczona droga też wróci.

**M.Stage4 — 4.2**
1. Od teraz zobaczysz tylko jeden kafelek do przodu.  
Zapamiętaj go dobrze!

**M.Stage4 — 4.3**
1. Tu też coś we mnie celuje!  
Dopasuj mój kolor do podłogi przez <color=#FFD24D><b>Ctrl</b></color>, a straci mnie z oczu.
2. Tym razem język rozwali całą lewą albo prawą stronę podłogi.  
Krzyknijcie okrzyk drużyny, żeby go zatrzymać — inaczej wszystko się rozpadnie.
3. Podłoga wraca tylko wtedy, gdy krzykniecie okrzyk drużyny, póki widać ostrzeżenie.  
Jak już się rozpadnie, trzeba wytrzymać do następnego ostrzeżenia.

**M.Stage5**
1. Ćwiczy czy co? Nagle zaczął ciężko oddychać!
2. Na podłodze zaczną się świecić kolorowe pola.  
Jeśli pojawi się twój kolor, stań tylko na tym polu.  
Jeśli nie, wytrzymaj na czarnym albo białym — tym, który do ciebie pasuje!

**M.Boss — Intro**
1. Uff… uff… jeszcze trochę i może wyjdziemy stąd żywi…

**M.Boss — Bossdown**
1. …Zrobiło się tu cicho.  
Czyżbyśmy w końcu mogli uciec…?
2. …(CHRUP!)
3. O nie, połyka nas…!!

---

## T.Stage1 (BoulderSpawnManager + ReachZoneObjective — 조임 초출 + ColorWall 고유)

1. 삼켜져서 식도로 넘어왔어...  
이렇게 된 이상, 아래로 내려가서 탈출하는 수밖에 없어.
2. 식도가 조여올 거야.  
경고가 뜨면, 다 같이 팀 구호를 외쳐서 막아!
3. 양옆에서 색벽이 밀려올 거야.  
내 색이 뜬 벽엔 직접 부딪혀.  
그러면 벽이 뒤로 물러날 거야.
4. 지금 뒤에서 굴러오는 거, 사탕이야?! 깔리기 싫으면 달려!

*(1번 = 식도 진입(M→T 전환). 2번 "조여온다" = 식도 원통 반경 축소(`EsophagusSqueeze`, 전방향, 랜덤 주기 공격) — Warning 중 외치면 공격 취소, Hold 중 외치면 원상 복구. 3번 "색벽" = `ColorWall` 고유색(좌우 압박, 되돌림 대상 아님) — 접촉 시 색 일치면 `AdvancingWall.PauseTemporarily()`로 원점 후퇴 + 일시정지(`ColorWall.cs` `HandleContact`/`PauseRoutine`). **T1은 고유색, 흑백은 T3 초출**(`CoopStageAudit.T.md` §4 잠금). 4번 = `BoulderSpawnManager` 추격 시작(`ReachZoneObjective`).)*

## T.Stage2 (MemoryPath / ColoredMemoryPath / PioneerPathManager — 안개 초출)

**Stage1 — MemoryPath (+ 안개 초출)**
1. 식도에 가스가 차는 것 같은데...  
짙어져서 앞이 안 보이면 팀 구호를 외쳐서 걷어내.

**Stage2 — ColoredMemoryPath**
1. 내 색을 기억하고 칸과 색을 맞춰서 이동해!

**Stage3 — PioneerPathManager**
1. 담당 구역의 색이 먼저 지나가야 길이 안전해져. 순서를 꼭 지켜!

*(Stage1: 2026-09-24 옛 1번(외워둬·즉사) 삭제 — 즉사는 겪으면 바로 알고 Tip이 "길을 외워 두세요"를 담당. 키는 `T.Stage2.Stage1.Line1` 하나로 정리(옛 Line2 키 삭제). `MemoryPathTile` Trap은 `NetworkDamageUtil.ApplyInstantKill` 즉사. 1번 "가스" = 안개 초출 — 거리 기반 Render Fog(`EsophagusFog`), 씬 전역 적용·구간 분리 없음, 걷혀도 정답 하이라이트가 다시 뜨는 게 아니라 그 순간의 바닥만 보임. Stage2 = `ColoredMemoryPathTile.IsSafeFor`, 고유색 비활성(흑백) 상태면 색이 맞아도 즉사. Stage3 = `PioneerPathTile`, 미개방 타일을 pioneer 아닌 색이 밟거나 흑백 상태면 즉사, Trap 타일은 누가 밟든 항상 즉사.)*

## T.Stage3 (Wall·볼더·Spike·패드 — 조임 복습 + ColorWall 흑백 초출)

1. 역류성 식도염이 있나 봐. 위액이 올라오고 있어!  
간판 시간 안에 구간을 못 지나가면 위액에 잠길 거야!

*(2026-09-24 개정: 1번을 구간 시계·위액 설명으로 교체(`TStage3SegmentDeadline.md`), 옛 3번(흑백 색벽) 삭제 — 양옆 벽은 T1에서 학습, Tip 2번이 담당. 씬 `Text (TMP) (3)`·키 `T.Stage3.Line3` 제거. 옛 2번(가시) 삭제 — 함정을 하나하나 설명하지 않음. `Text (TMP) (2)`는 UI 프리팹 소속이라 배열에서만 빼고(size 1) 키 연결은 프리팹 기본값으로 되돌림, 키 `T.Stage3.Line2` 제거. 이하 옛 메모: 1번 = `GreenMucusTrap`(`AcidPool`/`AcidHazardVFX` + `ContactDamage`) — "점액"(압력) 카테고리. 2번 = `SpikeTrap`(바닥에서 올라오는 가시, `ContactDamage`류 데미지) — T.Stage3.unity에 다수 배치. 3번 = `ColorWall` 흑백 초출 — 접촉 시 색 일치면 `AdvancingWall.PauseTemporarily()`로 원점 후퇴+정지(2인 게이트 아님). 조임 복습(`EsophagusSqueeze`)은 T1과 동일 메카닉이라 대사엔 안 넣음 — 2인 장면은 전원 외침 원상 복구.)*

## T.Stage4 (MovingCorridor + ContactKnockback + 구멍 바닥 + 패드→Door 길 — 안개 복습)

1. 알레르기 반응이 왔나 봐. 식도에 부종이 생겼어.
2. 식도가 민감해져서 한 칸에 한 명만 설 수 있어.  
어떤 칸은 쉽게 깨지니까 빠르게 반응해야 해.

*(2026-09-24 개정: 넉백 설명은 Tip 3번으로 넘기고, 2번 신설 — 용량 타일(한 칸 1명)·파괴 타일(`TStage4TrapRandomization.md`). 패드→Door 길은 2026-09-19 제거됨. 2번은 UI 프리팹의 `Text (TMP) (2)`(이 씬에서 제거 오버라이드돼 있던 것)를 되살려 새 키 `T.Stage4.Line2`에 연결. 이하 옛 메모: "부종" = `ContactKnockback`(순수 넉백, HP 무관) + 구멍 바닥(각자 생존, 2인 게이트 아님), T3에 잠깐 나온 걸 T4에서 메인으로. 패드→`DoorController` 길, 안개 복습(`EsophagusFog`)은 T1·T2에서 이미 가르쳐서 대사 없이 감.)*

## T.Stage5 (흑/백 토글 미로 — ReachZone)

1. 갑자기 식도가 요동치더니 통로가 막혀버렸어.
2. 한 명은 아래에서 달리고, 나머지는 위에서 패드를 이용하여 문을 열어줘!
3. 한 번에 한 색 문만 열려. 뭔가 쫓아오니까 서둘러!

*(2026-09-24 개정: 러너 재설계(`TStage5RunnerRedesign.md`)에 맞춰 3줄 전면 교체 — 1번 진입, 2번 러너/안내자 역할, 3번 한 색만 열림+체이서. 이하 옛 메모(흑백 토글 미로, 폐기): 1번 = 미로 진입. 2번 "흑백 막" = `BlackWhiteDoorToggle`/`BlackWhiteTogglePad` — 패드를 밟을 때마다 흑/백 문이 전역으로 뒤바뀜. 3번 = 2층 점프대 안내(3곳 중 하나). 키는 `T.Stage5.Line1`~`Line3`(기존 `T.Stage5.Stage1.Line1`/`Stage3.Line1`/`Stage3.Line2` id 재사용, 이름만 정리). 패널 1개, `showOnceKey=Stage5`. 클리어 = `ReachZoneObjective` 전원 골.)*

## T.Boss (BossFightObjective — 시간 구간 기반 연속 생존)

**Intro**
1. 식도의 마지막이야...  
이 아래는 위액으로 가득 차 있을 거야. 준비 없이 내려가면 위험해.
2. 아까의 사탕이 천장에서 점점 떨어지고 있어.  
땅에 닿기 전에 저걸 멈춰야 해!

**Bossdown**
1. 후... 가까스로 막았네...  
이제 생각해보자. 어떻게 안전하게 내려갈지.
2. 꿀꺽... 꿀꺽...  
목이 막혀서 물을 마시기 시작했어! 사탕이 내려온다!

*(2026-09-24 개정: Intro 3→2줄, Bossdown 4→2줄. 씬 Intro `Text (TMP) (3)` 삭제, Bossdown `Text (TMP) (3)`·`(4)`는 UI 프리팹 소속이라 배열에서만 뺌(size 2)·키 연결 프리팹 기본값으로. 키 `T.Boss.Intro.Line3`·`Bossdown.Line3`·`Line4` 삭제. 대사 줄 번호에 걸린 연출 없음 — 전부 `OnSequenceComplete`/`OnAllReady`.)*

*(2번 = T.Stage1 "사탕을 삼켰잖아...!"의 그 사탕이 식도를 타고 밀려 내려오며 커진 것 — 조임(연동운동)에 밀려 내려오는 설정, `CoopStageAudit.T.md` §6 "시계"(Sphere). 3번 = 구간(P1–P4)마다 Pioneer/Door/ColorWall+튕김 등으로 바닥을 넓혀 막음. Bossdown 3·4번 대사와 동시에 바닥이 부서지며 추락 — 위(胃)는 확정하지 않고 클리프행어로 마무리(시즌2 여지 보존).)*

---

# T-Stage 번역 (Localization Draft)

> 번역 규칙: `M-Stage 번역 (Localization Draft)` 섹션과 동일 — **1)** 언어별 문법에 맞게, **2)** 그 언어권에서 실제 쓰는 말투로 자연스럽게. 용어는 M 번역에서 이미 확정된 표현을 그대로 재사용해 전체 문서 용어를 통일한다: 팀 구호 = team cheer / チームの掛け声 / 团队口号 / 團隊口號 / grito de equipo / cri d'équipe / Teamruf / grito do time / командный клич / okrzyk drużyny. 개인 행동(예: "네 색에 맞춰")은 2인칭 단수(tú/du/tu 등), 전원 행동(팀 구호·도주·경계)은 2인칭 복수(vosotros·ustedes·vous·ihr 등)로 M과 동일하게 구분.
>
> ⚠️ 11개 언어 전체를 1차로 초안 작성한 것으로, 특히 JA/ZH/RU/PL은 원어민 검수 권장.

### EN

**T.Stage1**
1. We got swallowed... now we're in the esophagus.  
No choice now — we've gotta head down and find a way out.
2. The esophagus is gonna squeeze in on us.  
When the warning pops up, shout the team cheer together to stop it!
3. Colored walls are gonna close in from both sides.  
If a wall shows my color, ram right into it — that'll push it back.
4. Is that candy rolling up behind us?! Run unless you wanna get flattened!

**T.Stage2 — Stage1**
1. I think gas is building up in the esophagus...  
If it gets too thick to see, shout the team cheer to clear it!

**T.Stage2 — Stage2**
1. Remember my color and move along the tiles that match it!

**T.Stage2 — Stage3**
1. The zone's color has to go first, then the path's gonna be safe. Don't cut in line!

**T.Stage3**
1. Feels like acid reflux... stomach acid's coming up!  
If we don't clear each section before the sign's timer runs out, we're gonna be swimming in it!

**T.Stage4**
1. Must be an allergic reaction... the esophagus is all swollen up.
2. It's gotten so sensitive that only one of us can stand on each tile.  
Some tiles break easily, so react fast!

**T.Stage5**
1. The esophagus suddenly convulsed and the passage got blocked off!
2. One of us runs down below. Everyone else, step on the color pads to open the doors!
3. Only one color of door opens at a time. Something's chasing us, so hurry!

**T.Boss — Intro**
1. This is the end of the esophagus...  
It's gotta be full of stomach acid down there. It's dangerous to go down without preparation.
2. That candy from before is falling from the ceiling.  
We've gotta stop it before it hits the ground!

**T.Boss — Bossdown**
1. Phew... we barely stopped it...  
Now let's figure out how to get down safely.
2. Gulp... gulp...  
He's choking, so he started drinking water! The candy's coming down!

### JA

**T.Stage1**
1. 飲み込まれて、食道まで来ちゃった…  
こうなったら、下へ降りて出口を探すしかない。
2. 食道が締めつけてくるよ。  
警告が出たら、みんなでチームの掛け声を叫んで止めて！
3. 両側から色の壁が迫ってくるよ。  
わたしの色が出た壁には、思いきり体当たりして。そうすれば押し返せる。
4. 後ろから転がってくるの…アメ！？ぺちゃんこになりたくなかったら走って！

**T.Stage2 — Stage1**
1. 食道にガスがたまってきてるみたい…  
濃くなって前が見えなくなったら、チームの掛け声で吹き飛ばして！

**T.Stage2 — Stage2**
1. わたしの色を覚えて、同じ色のマスを進んで！

**T.Stage2 — Stage3**
1. そのエリアの色の人が先に通れば、道が安全になるよ。割り込み禁止！

**T.Stage3**
1. 逆流性食道炎っぽい…胃液が上がってきてる！  
看板のタイマーが切れる前に区間を抜けないと、胃液に沈んじゃうよ！

**T.Stage4**
1. アレルギー反応かな…食道がパンパンに腫れてる。
2. 食道が敏感になってて、1マスに1人しか乗れないよ。  
割れやすいマスもあるから、素早く動いて！

**T.Stage5**
1. 急に食道がうねって、通路がふさがっちゃった！
2. 1人は下を走って、ほかのみんなは色のパネルを踏んで扉を開けて！
3. 一度に開くのは一色の扉だけ。何かが追ってくるから、急いで！

**T.Boss — Intro**
1. 食道の終わりだ…  
この下はきっと胃液でいっぱい。準備なしで降りるのは危ないよ。
2. さっきのアメが天井からどんどん落ちてきてる。  
地面に着く前に止めなきゃ！

**T.Boss — Bossdown**
1. ふう…なんとか止められた…  
さて、どうやって安全に降りるか考えよう。
2. ゴクッ…ゴクッ…  
喉が詰まって水を飲み始めた！アメが落ちてくる！

### ZH-Hans

**T.Stage1**
1. 被吞下去了……现在到了食道。  
没办法了，只能往下走找出口。
2. 食道会挤压过来。  
警告一出现，大家一起喊团队口号挡住它！
3. 两边会有彩色墙壁压过来。  
墙上出现我的颜色时就直接撞上去，这样就能把它推回去。
4. 后面滚过来的是……糖果吗？！不想被压扁就快跑！

**T.Stage2 — Stage1**
1. 食道里好像在积气……  
浓到看不清的时候，就喊团队口号把它驱散！

**T.Stage2 — Stage2**
1. 记住我的颜色，沿着同色的格子走！

**T.Stage2 — Stage3**
1. 得让这个区域的颜色先走，路才会安全。不准插队！

**T.Stage3**
1. 好像是胃食管反流……胃液涌上来了！  
要是没在牌子上的计时结束前通过这一段，就会泡在胃液里！

**T.Stage4**
1. 大概是过敏了……食道肿得厉害。
2. 食道变得很敏感，一格只能站一个人。  
有些格子很容易碎，反应要快！

**T.Stage5**
1. 食道突然一阵翻腾，通道被堵住了！
2. 一个人在下面跑，其他人踩彩色踏板开门！
3. 一次只会开一种颜色的门。有东西在追我们，快点！

**T.Boss — Intro**
1. 这是食道的尽头了……  
下面肯定全是胃液。没准备好就下去太危险了。
2. 刚才那颗糖果正从天花板上慢慢掉下来。  
必须在它落地前拦住它！

**T.Boss — Bossdown**
1. 呼……总算拦住了……  
现在想想怎么安全下去吧。
2. 咕嘟……咕嘟……  
他噎住了，开始喝水了！糖果要掉下来了！

### ZH-Hant

**T.Stage1**
1. 被吞下去了……現在到了食道。  
沒辦法了，只能往下走找出口。
2. 食道會擠壓過來。  
警告一出現，大家一起喊團隊口號擋住它！
3. 兩邊會有彩色牆壁壓過來。  
牆上出現我的顏色時就直接撞上去，這樣就能把它推回去。
4. 後面滾過來的是……糖果嗎？！不想被壓扁就快跑！

**T.Stage2 — Stage1**
1. 食道裡好像在積氣……  
濃到看不清的時候，就喊團隊口號把它驅散！

**T.Stage2 — Stage2**
1. 記住我的顏色，沿著同色的格子走！

**T.Stage2 — Stage3**
1. 得讓這個區域的顏色先走，路才會安全。不准插隊！

**T.Stage3**
1. 好像是胃食道逆流……胃液湧上來了！  
要是沒在牌子上的計時結束前通過這一段，就會泡在胃液裡！

**T.Stage4**
1. 大概是過敏了……食道腫得厲害。
2. 食道變得很敏感，一格只能站一個人。  
有些格子很容易碎，反應要快！

**T.Stage5**
1. 食道突然一陣翻騰，通道被堵住了！
2. 一個人在下面跑，其他人踩彩色踏板開門！
3. 一次只會開一種顏色的門。有東西在追我們，快點！

**T.Boss — Intro**
1. 這是食道的盡頭了……  
下面肯定全是胃液。沒準備好就下去太危險了。
2. 剛才那顆糖果正從天花板上慢慢掉下來。  
必須在它落地前攔住它！

**T.Boss — Bossdown**
1. 呼……總算攔住了……  
現在想想怎麼安全下去吧。
2. 咕嘟……咕嘟……  
他噎住了，開始喝水了！糖果要掉下來了！

### ES

**T.Stage1**
1. Nos ha tragado… ahora estamos en el esófago.  
No queda otra: hay que bajar y buscar una salida.
2. El esófago nos va a apretujar.  
Cuando salga el aviso, ¡gritad juntos el grito de equipo para pararlo!
3. Van a venir paredes de colores por los dos lados.  
Si una pared muestra mi color, embístela de lleno: así la harás retroceder.
4. ¡¿Eso que viene rodando por detrás es un caramelo?! ¡Corred si no queréis acabar aplastados!

**T.Stage2 — Stage1**
1. Creo que se está acumulando gas en el esófago…  
Si se pone tan denso que no se ve nada, ¡gritad el grito de equipo para despejarlo!

**T.Stage2 — Stage2**
1. ¡Recuerda mi color y avanza por las baldosas que coincidan!

**T.Stage2 — Stage3**
1. Primero tiene que pasar el color de la zona; después el camino será seguro. ¡No os coléis!

**T.Stage3**
1. Parece reflujo… ¡está subiendo ácido del estómago!  
Si no pasamos cada tramo antes de que se acabe el tiempo del cartel, ¡acabaremos nadando en él!

**T.Stage4**
1. Debe de ser una reacción alérgica… el esófago está todo hinchado.
2. Está tan sensible que solo cabe uno por casilla.  
Algunas casillas se rompen fácilmente, ¡reaccionad rápido!

**T.Stage5**
1. ¡El esófago se ha sacudido de golpe y ha bloqueado el paso!
2. Uno corre abajo. ¡Los demás, pisad las placas de color para abrir las puertas!
3. Solo se abren las puertas de un color a la vez. ¡Algo nos persigue, daos prisa!

**T.Boss — Intro**
1. Este es el final del esófago…  
Ahí abajo debe de estar lleno de ácido. Bajar sin prepararse es peligroso.
2. El caramelo de antes está cayendo del techo.  
¡Hay que pararlo antes de que toque el suelo!

**T.Boss — Bossdown**
1. Uf… por los pelos…  
Ahora pensemos cómo bajar sin peligro.
2. Glup… glup…  
¡Se ha atragantado y se ha puesto a beber agua! ¡El caramelo está bajando!

### ES-419

**T.Stage1**
1. Nos tragó… ahora estamos en el esófago.  
No queda de otra: hay que bajar y buscar una salida.
2. El esófago nos va a apretar.  
Cuando aparezca la advertencia, ¡griten juntos el grito de equipo para detenerlo!
3. Van a venir paredes de colores por los dos lados.  
Si una pared muestra mi color, embístela de lleno: así la vas a hacer retroceder.
4. ¡¿Eso que viene rodando atrás es un dulce?! ¡Corran si no quieren quedar aplastados!

**T.Stage2 — Stage1**
1. Creo que se está juntando gas en el esófago…  
Si se pone tan denso que no se ve nada, ¡griten el grito de equipo para despejarlo!

**T.Stage2 — Stage2**
1. ¡Recuerda mi color y avanza por las baldosas que coincidan!

**T.Stage2 — Stage3**
1. Primero tiene que pasar el color de la zona; después el camino va a ser seguro. ¡Nada de saltarse la fila!

**T.Stage3**
1. Parece reflujo… ¡está subiendo ácido del estómago!  
Si no pasamos cada tramo antes de que se acabe el tiempo del letrero, ¡vamos a terminar nadando en él!

**T.Stage4**
1. Debe ser una reacción alérgica… el esófago está todo hinchado.
2. Está tan sensible que solo cabe uno por casilla.  
Algunas casillas se rompen fácil, ¡reaccionen rápido!

**T.Stage5**
1. ¡El esófago se sacudió de golpe y bloqueó el paso!
2. Uno corre abajo. ¡Los demás, pisen las placas de color para abrir las puertas!
3. Solo se abren las puertas de un color a la vez. ¡Algo nos persigue, apúrense!

**T.Boss — Intro**
1. Este es el final del esófago…  
Ahí abajo debe estar lleno de ácido. Bajar sin prepararse es peligroso.
2. El dulce de antes está cayendo del techo.  
¡Hay que detenerlo antes de que toque el piso!

**T.Boss — Bossdown**
1. Uf… apenas lo logramos…  
Ahora pensemos cómo bajar sin peligro.
2. Glup… glup…  
¡Se atragantó y se puso a tomar agua! ¡El dulce está bajando!

### FR

**T.Stage1**
1. On s'est fait avaler… on est dans l'œsophage, maintenant.  
Pas le choix : il faut descendre et trouver une sortie.
2. L'œsophage va se resserrer sur nous.  
Dès que l'alerte apparaît, criez le cri d'équipe ensemble pour l'arrêter !
3. Des murs colorés vont se refermer des deux côtés.  
Si un mur affiche ma couleur, fonce dedans : ça le fera reculer.
4. C'est un bonbon qui roule derrière nous ?! Courez si vous ne voulez pas finir en crêpe !

**T.Stage2 — Stage1**
1. On dirait que du gaz s'accumule dans l'œsophage…  
S'il devient trop épais pour voir, criez le cri d'équipe pour le dissiper !

**T.Stage2 — Stage2**
1. Retiens ma couleur et avance sur les cases qui correspondent !

**T.Stage2 — Stage3**
1. La couleur de la zone doit passer en premier, ensuite le chemin sera sûr. On ne double pas !

**T.Stage3**
1. On dirait du reflux… l'acide gastrique remonte !  
Si on ne passe pas chaque section avant la fin du chrono du panneau, on va finir par nager dedans !

**T.Stage4**
1. Ça doit être une réaction allergique… l'œsophage est tout enflé.
2. Il est devenu si sensible qu'un seul d'entre nous peut tenir par case.  
Certaines cases se brisent facilement, alors réagissez vite !

**T.Stage5**
1. L'œsophage s'est contracté d'un coup et le passage est bloqué !
2. L'un de nous court en bas. Les autres, marchez sur les dalles de couleur pour ouvrir les portes !
3. Une seule couleur de porte s'ouvre à la fois. Quelque chose nous poursuit, dépêchez-vous !

**T.Boss — Intro**
1. C'est la fin de l'œsophage…  
En bas, ça doit être rempli d'acide gastrique. Descendre sans se préparer, c'est dangereux.
2. Le bonbon de tout à l'heure tombe du plafond.  
Il faut l'arrêter avant qu'il touche le sol !

**T.Boss — Bossdown**
1. Ouf… on l'a arrêté de justesse…  
Maintenant, réfléchissons à comment descendre sans risque.
2. Glou… glou…  
Il s'étouffe et s'est mis à boire de l'eau ! Le bonbon descend !

### DE

**T.Stage1**
1. Wir wurden verschluckt… jetzt sind wir in der Speiseröhre.  
Keine Wahl – wir müssen runter und einen Ausweg finden.
2. Die Speiseröhre wird sich um uns zusammenziehen.  
Sobald die Warnung kommt, ruft zusammen den Teamruf, um sie zu stoppen!
3. Von beiden Seiten rücken bunte Wände an.  
Zeigt eine Wand meine Farbe, renn voll rein – dann weicht sie zurück.
4. Rollt da etwa ein Bonbon hinter uns her?! Lauft, wenn ihr nicht platt gewalzt werden wollt!

**T.Stage2 — Stage1**
1. Ich glaub, in der Speiseröhre staut sich Gas…  
Wenn's so dicht wird, dass man nichts mehr sieht, vertreibt es mit dem Teamruf!

**T.Stage2 — Stage2**
1. Merk dir meine Farbe und lauf über die Felder, die dazu passen!

**T.Stage2 — Stage3**
1. Die Farbe der Zone muss zuerst durch, dann ist der Weg sicher. Nicht vordrängeln!

**T.Stage3**
1. Fühlt sich nach Sodbrennen an… da kommt Magensäure hoch!  
Wenn wir nicht jeden Abschnitt schaffen, bevor der Timer am Schild abläuft, schwimmen wir gleich drin!

**T.Stage4**
1. Muss eine allergische Reaktion sein… die Speiseröhre ist total geschwollen.
2. Sie ist so empfindlich, dass nur einer von uns auf jedem Feld stehen kann.  
Manche Felder brechen leicht, also reagiert schnell!

**T.Stage5**
1. Die Speiseröhre hat plötzlich gezuckt und der Durchgang ist versperrt!
2. Einer von uns rennt unten. Alle anderen treten auf die Farbplatten, um die Türen zu öffnen!
3. Es öffnen sich immer nur die Türen einer Farbe. Irgendwas verfolgt uns, beeilt euch!

**T.Boss — Intro**
1. Das ist das Ende der Speiseröhre…  
Da unten ist bestimmt alles voller Magensäure. Ohne Vorbereitung runterzugehen ist gefährlich.
2. Das Bonbon von vorhin fällt von der Decke.  
Wir müssen es aufhalten, bevor es unten aufschlägt!

**T.Boss — Bossdown**
1. Puh… gerade noch aufgehalten…  
Jetzt überlegen wir, wie wir sicher runterkommen.
2. Gluck… gluck…  
Er hat sich verschluckt und fängt an, Wasser zu trinken! Das Bonbon kommt runter!

### PT-BR

**T.Stage1**
1. A gente foi engolido… agora estamos no esôfago.  
Não tem jeito — temos que descer e achar uma saída.
2. O esôfago vai apertar a gente.  
Quando o aviso aparecer, gritem juntos o grito da equipe pra impedir!
3. Paredes coloridas vão vir dos dois lados.  
Se uma parede mostrar a minha cor, pode trombar com tudo — isso empurra ela pra trás.
4. Isso rolando atrás da gente é uma bala?! Corram se não quiserem virar panqueca!

**T.Stage2 — Stage1**
1. Acho que tá acumulando gás no esôfago…  
Se ficar tão denso que não dá pra ver, gritem o grito da equipe pra dissipar!

**T.Stage2 — Stage2**
1. Lembre a minha cor e vá pelos blocos que combinam com ela!

**T.Stage2 — Stage3**
1. A cor da área tem que ir primeiro, aí o caminho fica seguro. Nada de furar fila!

**T.Stage3**
1. Parece refluxo… o suco gástrico tá subindo!  
Se a gente não passar cada trecho antes do tempo da placa acabar, vamos acabar nadando nele!

**T.Stage4**
1. Deve ser uma reação alérgica… o esôfago tá todo inchado.
2. Ficou tão sensível que só cabe um de nós por bloco.  
Alguns blocos quebram fácil, então reajam rápido!

**T.Stage5**
1. O esôfago se contorceu do nada e bloqueou a passagem!
2. Um de nós corre lá embaixo. O resto, pise nas placas coloridas pra abrir as portas!
3. Só abrem as portas de uma cor por vez. Tem alguma coisa atrás da gente, corram!

**T.Boss — Intro**
1. Aqui é o fim do esôfago…  
Lá embaixo deve estar cheio de suco gástrico. Descer sem se preparar é perigoso.
2. Aquela bala de antes tá caindo do teto.  
A gente tem que parar ela antes de chegar no chão!

**T.Boss — Bossdown**
1. Ufa… foi por pouco…  
Agora vamos pensar em como descer com segurança.
2. Glup… glup…  
Ele engasgou e começou a beber água! A bala tá descendo!

### RU

**T.Stage1**
1. Нас проглотили… теперь мы в пищеводе.  
Выбора нет — надо спускаться и искать выход.
2. Пищевод будет сжиматься вокруг нас.  
Как появится предупреждение — кричите вместе командный клич, чтобы остановить это!
3. С обеих сторон будут надвигаться цветные стены.  
Если на стене мой цвет — врежься в неё с разгону, и она отступит.
4. Это что, за нами катится леденец?! Бегите, если не хотите превратиться в лепёшку!

**T.Stage2 — Stage1**
1. Кажется, в пищеводе скапливается газ…  
Если станет так густо, что ничего не видно, — кричите командный клич, чтобы его разогнать!

**T.Stage2 — Stage2**
1. Запомни мой цвет и иди по клеткам того же цвета!

**T.Stage2 — Stage3**
1. Сначала должен пройти цвет этой зоны — потом путь будет безопасным. Не лезьте вперёд!

**T.Stage3**
1. Похоже на рефлюкс… поднимается желудочный сок!  
Если не пройдём участок, пока не истёк таймер на табличке, будем в нём плавать!

**T.Stage4**
1. Похоже на аллергию… пищевод весь распух.
2. Он стал таким чувствительным, что на одной клетке может стоять только один.  
Некоторые клетки легко ломаются, так что реагируйте быстро!

**T.Stage5**
1. Пищевод вдруг содрогнулся, и проход перекрыло!
2. Один бежит внизу. Остальные — наступайте на цветные плиты, чтобы открывать двери!
3. За раз открываются двери только одного цвета. За нами что-то гонится, быстрее!

**T.Boss — Intro**
1. Это конец пищевода…  
Внизу наверняка полно желудочного сока. Спускаться без подготовки опасно.
2. Тот самый леденец падает с потолка.  
Надо остановить его, пока он не коснулся земли!

**T.Boss — Bossdown**
1. Фух… еле остановили…  
Теперь давайте придумаем, как безопасно спуститься.
2. Глык… глык…  
Он подавился и начал пить воду! Леденец падает!

### PL

**T.Stage1**
1. Połknął nas… jesteśmy teraz w przełyku.  
Nie ma wyboru — trzeba zejść na dół i znaleźć wyjście.
2. Przełyk będzie się na nas zaciskał.  
Gdy pojawi się ostrzeżenie, krzyknijcie razem okrzyk drużyny, żeby go powstrzymać!
3. Z obu stron nadjadą kolorowe ściany.  
Jeśli ściana ma mój kolor, wbij się w nią z całej siły — wtedy się cofnie.
4. Czy to cukierek toczy się za nami?! Biegiem, jeśli nie chcecie zostać naleśnikiem!

**T.Stage2 — Stage1**
1. Chyba w przełyku zbiera się gaz…  
Jak zrobi się tak gęsto, że nic nie widać, krzyknijcie okrzyk drużyny, żeby go rozwiać!

**T.Stage2 — Stage2**
1. Zapamiętaj mój kolor i idź po polach, które do niego pasują!

**T.Stage2 — Stage3**
1. Najpierw musi przejść kolor strefy, dopiero wtedy droga będzie bezpieczna. Nie wpychajcie się!

**T.Stage3**
1. Chyba refluks… podchodzi kwas żołądkowy!  
Jeśli nie przejdziemy odcinka, zanim skończy się czas na tabliczce, będziemy w nim pływać!

**T.Stage4**
1. To chyba reakcja alergiczna… przełyk cały spuchł.
2. Zrobił się tak wrażliwy, że na jednym polu zmieści się tylko jedno z nas.  
Niektóre pola łatwo pękają, więc reagujcie szybko!

**T.Stage5**
1. Przełyk nagle się wzdrygnął i przejście się zablokowało!
2. Jedno z nas biegnie na dole. Reszta — stawajcie na kolorowych płytkach, żeby otwierać drzwi!
3. Naraz otwierają się drzwi tylko jednego koloru. Coś nas goni, pośpieszcie się!

**T.Boss — Intro**
1. To już koniec przełyku…  
Na dole na pewno jest pełno kwasu żołądkowego. Schodzenie bez przygotowania jest niebezpieczne.
2. Ten cukierek z wcześniej spada z sufitu.  
Musimy go zatrzymać, zanim uderzy w ziemię!

**T.Boss — Bossdown**
1. Uff… ledwo go zatrzymaliśmy…  
To teraz pomyślmy, jak bezpiecznie zejść.
2. Glup… glup…  
Zakrztusił się i zaczął pić wodę! Cukierek spada!

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
| M.Stage5 | `Stage5.1` | `Dialogue_Panel` | `M.Stage5.Line1`~`Line3` |
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
| T.Stage5 | `Stage5` | `Dialogue_Panel` | `T.Stage5.Line1`~`Line3` |
| T.Boss | `Boss.Intro` / `Bossdown` | `Dialogue_Panel` / `(1)` | `Intro.Line1`~`3` / `Bossdown.Line1`~`4` |

T.Stage3·T.Stage4: 대화 끝날 때까지 시작 게이트가 안 켜지도록 `armOnStart=false`, 대화 완료 후 `Arm()`. Phase enter는 `StartStage`/`Arm` 대신 `PhaseDialogueGate.Begin`.
T.Boss: 기존 게이트에 `dialogueUI`가 비어 있어서 Intro=`Dialogue_Panel`, Bossdown=`Dialogue_Panel (1)`로 연결.
