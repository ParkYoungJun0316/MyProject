# OXQuizTranslations — OX퀴즈(OXQuizManager) 전체 스테이지 12개 언어 번역본

> `OXQuizManager`를 재사용하는 스테이지가 **M.Stage2**, **T.Stage4** 2곳 — 둘 다 이 문서에 포함.
> 원문 소스: 각 씬(`M.Stage2.unity`, `T.Stage4.unity`)의 `OXQuizManager` 컴포넌트 `questions[]` (영어로 이미 작성돼 있던 인스펙터 데이터, 이 문서 작성 시점 기준).
> 코드 변경: `OXQuestion.questionText` / `explanationText`를 `string` → `LocalizedString`으로 변경함 (`Assets/Scripts/Stage/OXQuizManager.cs`, DialogueUI 파일럿과 동일한 Unity Localization 패턴, 두 씬 모두 같은 클래스라 한 번의 코드 변경으로 공통 적용됨). 이 타입 변경으로 두 씬 인스펙터에 있던 기존 영어 텍스트(M.Stage2 12문항×2, T.Stage4 13문항×2)는 **초기화됨** — 이 문서 값으로 String Table을 채운 뒤 아래 체크리스트대로 각 씬에서 다시 연결해야 함.
>
> 에이전트는 String Table `.asset`이나 씬을 직접 쓰지 않음 (`unity-mcp-readonly.mdc`). 아래 내용은 사람이 String Table Editor로 입력.

## 키 네이밍 (제안)

새 String Table Collection **`OXQuiz`** (기존 `Dialogue`와 분리 — 대사 서사 vs 퀴즈 사실 정보로 도메인이 달라서 분리 추천, 굳이 합쳐도 기능상 문제는 없음). `Dialogue` 테이블과 동일하게 **스테이지명을 키 앞에 그대로 붙임**:

- `M.Stage2.Q1.Question` / `M.Stage2.Q1.Explanation` ... `M.Stage2.Q12.Question` / `M.Stage2.Q12.Explanation`
- `T.Stage4.Q1.Question` / `T.Stage4.Q1.Explanation` ... `T.Stage4.Q13.Question` / `T.Stage4.Q13.Explanation`

정답(O/X, `correctAnswerIsO`)은 `LocalizedString`이 아니라 그냥 `bool` 필드라 번역 대상 아님 — 그대로 유지됨.

---

## M.Stage2 (12문항)

### Q1 (정답: O)

**Question**
- ko: 성인은 하루에 1리터 이상의 침을 분비한다.
- en: Adults produce over 1 liter of saliva every day.
- ja: 大人は毎日1リットル以上の唾液を分泌する。
- zh-Hans: 成年人每天分泌超过1升唾液。
- zh-Hant: 成年人每天分泌超過1公升唾液。
- es: Los adultos producen más de 1 litro de saliva al día.
- es-419: Los adultos producen más de 1 litro de saliva al día.
- fr: Les adultes produisent plus d'un litre de salive par jour.
- de: Erwachsene produzieren täglich über 1 Liter Speichel.
- pt-BR: Adultos produzem mais de 1 litro de saliva por dia.
- ru: Взрослые вырабатывают больше 1 литра слюны каждый день.
- pl: Dorośli wytwarzają ponad 1 litr śliny każdego dnia.

**Explanation**
- ko: 성인은 하루에 약 1~1.5리터의 침을 분비한다.
- en: Adults produce about 1 to 1.5 liters of saliva each day.
- ja: 大人は1日に約1〜1.5リットルの唾液を分泌する。
- zh-Hans: 成年人每天大约分泌1到1.5升唾液。
- zh-Hant: 成年人每天大約分泌1到1.5公升唾液。
- es: Los adultos producen entre 1 y 1,5 litros de saliva al día.
- es-419: Los adultos producen entre 1 y 1.5 litros de saliva al día.
- fr: Les adultes produisent environ 1 à 1,5 litre de salive par jour.
- de: Erwachsene produzieren täglich etwa 1 bis 1,5 Liter Speichel.
- pt-BR: Adultos produzem entre 1 e 1,5 litro de saliva por dia.
- ru: Взрослые вырабатывают примерно от 1 до 1,5 литра слюны в день.
- pl: Dorośli wytwarzają dziennie około 1–1,5 litra śliny.

### Q2 (정답: X)

**Question**
- ko: 혀의 부위마다 특정한 맛만 느낄 수 있다.
- en: Different parts of the tongue can only taste certain flavors.
- ja: 舌の部位ごとに感じられる味は決まっている。
- zh-Hans: 舌头的不同部位只能尝出特定的味道。
- zh-Hant: 舌頭的不同部位只能嚐出特定的味道。
- es: Las distintas partes de la lengua solo pueden percibir ciertos sabores.
- es-419: Las distintas partes de la lengua solo pueden percibir ciertos sabores.
- fr: Différentes parties de la langue ne peuvent percevoir que certaines saveurs.
- de: Verschiedene Bereiche der Zunge können nur bestimmte Geschmäcker wahrnehmen.
- pt-BR: Partes diferentes da língua só conseguem sentir certos sabores.
- ru: Разные участки языка чувствуют только определённые вкусы.
- pl: Różne części języka mogą wyczuwać tylko określone smaki.

**Explanation**
- ko: 혀의 모든 부위는 기본적인 맛을 전부 감지할 수 있다.
- en: All parts of the tongue can detect all basic tastes.
- ja: 舌のどの部位でも基本的な味はすべて感知できる。
- zh-Hans: 舌头的每个部位都能感知所有基本味道。
- zh-Hant: 舌頭的每個部位都能感知所有基本味道。
- es: Todas las partes de la lengua pueden detectar todos los sabores básicos.
- es-419: Todas las partes de la lengua pueden detectar todos los sabores básicos.
- fr: Toutes les parties de la langue peuvent détecter toutes les saveurs de base.
- de: Jeder Bereich der Zunge kann alle Grundgeschmäcker erkennen.
- pt-BR: Todas as partes da língua conseguem detectar todos os sabores básicos.
- ru: Любой участок языка способен распознавать все основные вкусы.
- pl: Każda część języka może wykrywać wszystkie podstawowe smaki.

### Q3 (정답: O)

**Question**
- ko: 혀의 무늬는 사람마다 다르다.
- en: Tongue patterns are different for each person.
- ja: 舌の模様は人によって違う。
- zh-Hans: 每个人的舌纹都不一样。
- zh-Hant: 每個人的舌紋都不一樣。
- es: Los patrones de la lengua son diferentes en cada persona.
- es-419: Los patrones de la lengua son diferentes en cada persona.
- fr: Les motifs de la langue sont différents pour chaque personne.
- de: Die Zungenmuster sind bei jedem Menschen unterschiedlich.
- pt-BR: Os padrões da língua são diferentes para cada pessoa.
- ru: Узор языка у каждого человека уникален.
- pl: Wzory na języku są inne u każdej osoby.

**Explanation**
- ko: 혀 표면의 무늬는 생체 인식 기술로 연구될 만큼 사람마다 고유하다.
- en: The surface patterns of the tongue are unique enough to be studied for biometric technology.
- ja: 舌の表面の模様は生体認証技術の研究対象になるほど、人によって固有のものだ。
- zh-Hans: 舌头表面的纹路因人而异，独特到可以用于生物识别技术研究。
- zh-Hant: 舌頭表面的紋路因人而異，獨特到可以用於生物識別技術研究。
- es: Los patrones de la superficie de la lengua son tan únicos que se estudian para tecnología biométrica.
- es-419: Los patrones de la superficie de la lengua son tan únicos que se estudian para tecnología biométrica.
- fr: Les motifs à la surface de la langue sont suffisamment uniques pour être étudiés en biométrie.
- de: Die Oberflächenmuster der Zunge sind so einzigartig, dass sie für Biometrie-Technologie erforscht werden.
- pt-BR: Os padrões da superfície da língua são únicos o suficiente para serem estudados em tecnologia biométrica.
- ru: Узор поверхности языка настолько уникален, что его изучают для биометрических технологий.
- pl: Wzory na powierzchni języka są tak unikalne, że są badane w technologii biometrycznej.

### Q4 (정답: O)

**Question**
- ko: 입술에는 일반 피부와 달리 땀샘이 없다.
- en: Lips do not have sweat glands like normal skin.
- ja: 唇には普通の皮膚のような汗腺がない。
- zh-Hans: 嘴唇不像普通皮肤那样有汗腺。
- zh-Hant: 嘴唇不像一般皮膚那樣有汗腺。
- es: Los labios no tienen glándulas sudoríparas como la piel normal.
- es-419: Los labios no tienen glándulas sudoríparas como la piel normal.
- fr: Les lèvres n'ont pas de glandes sudoripares comme la peau normale.
- de: Lippen haben keine Schweißdrüsen wie normale Haut.
- pt-BR: Os lábios não têm glândulas sudoríparas como a pele normal.
- ru: На губах, в отличие от обычной кожи, нет потовых желёз.
- pl: Wargi, w przeciwieństwie do zwykłej skóry, nie mają gruczołów potowych.

**Explanation**
- ko: 입술은 땀샘과 기름샘이 없어서 쉽게 건조해진다.
- en: Lips dry out easily because they do not have sweat or oil glands.
- ja: 唇は汗腺や皮脂腺がないため、すぐに乾燥する。
- zh-Hans: 嘴唇因为没有汗腺和油脂腺，很容易干燥。
- zh-Hant: 嘴唇因為沒有汗腺和油脂腺，很容易乾燥。
- es: Los labios se secan fácilmente porque no tienen glándulas sudoríparas ni sebáceas.
- es-419: Los labios se secan fácilmente porque no tienen glándulas sudoríparas ni sebáceas.
- fr: Les lèvres sèchent facilement car elles n'ont ni glandes sudoripares ni glandes sébacées.
- de: Lippen trocknen leicht aus, weil sie keine Schweiß- oder Fettdrüsen haben.
- pt-BR: Os lábios secam facilmente porque não têm glândulas sudoríparas nem sebáceas.
- ru: Губы легко пересыхают, потому что у них нет потовых и сальных желёз.
- pl: Wargi łatwo wysychają, bo nie mają gruczołów potowych ani łojowych.

### Q5 (정답: O)

**Question**
- ko: 나이가 들수록 쓴 음식을 더 잘 먹게 되는 경우가 많다.
- en: People often become better at eating bitter foods as they get older.
- ja: 年を取るほど苦い食べ物が食べられるようになることが多い。
- zh-Hans: 人年纪越大，往往越能吃苦味食物。
- zh-Hant: 人年紀越大，往往越能吃苦味食物。
- es: Con la edad, muchas personas empiezan a tolerar mejor los alimentos amargos.
- es-419: Con la edad, muchas personas empiezan a tolerar mejor los alimentos amargos.
- fr: Avec l'âge, on tolère souvent mieux les aliments amers.
- de: Mit dem Alter vertragen viele Menschen bittere Speisen besser.
- pt-BR: Com a idade, muitas pessoas passam a tolerar melhor comidas amargas.
- ru: С возрастом люди часто начинают лучше переносить горькую еду.
- pl: Z wiekiem ludzie często zaczynają lepiej tolerować gorzkie jedzenie.

**Explanation**
- ko: 미각의 변화와 반복된 음식 경험 때문에 나타나는 현상이다.
- en: This happens because of changes in taste and repeated food experiences.
- ja: 味覚の変化と食経験の積み重ねによって起こる現象だ。
- zh-Hans: 这是因为味觉发生变化，再加上反复的饮食经验。
- zh-Hant: 這是因為味覺發生變化，再加上反覆的飲食經驗。
- es: Esto ocurre por cambios en el gusto y la experiencia repetida con la comida.
- es-419: Esto pasa por cambios en el gusto y la experiencia repetida con la comida.
- fr: Cela s'explique par des changements du goût et l'expérience répétée des aliments.
- de: Das liegt an Veränderungen des Geschmacks und wiederholter Erfahrung mit Nahrung.
- pt-BR: Isso acontece por causa de mudanças no paladar e da experiência repetida com alimentos.
- ru: Это происходит из-за изменений вкусовых ощущений и накопленного опыта питания.
- pl: Dzieje się to z powodu zmian w odczuwaniu smaku i powtarzanych doświadczeń z jedzeniem.

### Q6 (정답: X)

**Question**
- ko: 기네스 세계 기록에 오른 가장 긴 인간의 치아는 4cm가 넘었다.
- en: The longest human tooth recorded in Guinness World Records was over 4 cm long.
- ja: ギネス世界記録に残る最も長い人間の歯は4cmを超えていた。
- zh-Hans: 吉尼斯世界纪录中最长的人类牙齿超过4厘米。
- zh-Hant: 金氏世界紀錄中最長的人類牙齒超過4公分。
- es: El diente humano más largo registrado en el Libro Guinness medía más de 4 cm.
- es-419: El diente humano más largo registrado en el Libro Guinness medía más de 4 cm.
- fr: La dent humaine la plus longue enregistrée dans le Livre Guinness mesurait plus de 4 cm.
- de: Der längste im Guinness-Buch der Rekorde verzeichnete menschliche Zahn war über 4 cm lang.
- pt-BR: O dente humano mais longo registrado no Guinness Book media mais de 4 cm.
- ru: Самый длинный человеческий зуб, зафиксированный в Книге рекордов Гиннесса, был длиннее 4 см.
- pl: Najdłuższy ludzki ząb odnotowany w Księdze Rekordów Guinnessa miał ponad 4 cm.

**Explanation**
- ko: 기록상 가장 긴 인간의 송곳니는 약 3.67cm였다.
- en: The longest recorded human canine tooth was about 3.67 cm long.
- ja: 記録された最も長い人間の犬歯は約3.67cmだった。
- zh-Hans: 有记录的最长人类犬齿大约是3.67厘米。
- zh-Hant: 有紀錄的最長人類犬齒大約是3.67公分。
- es: El canino humano más largo registrado medía unos 3,67 cm.
- es-419: El canino humano más largo registrado medía unos 3.67 cm.
- fr: La plus longue canine humaine enregistrée mesurait environ 3,67 cm.
- de: Der längste verzeichnete menschliche Eckzahn war etwa 3,67 cm lang.
- pt-BR: O maior canino humano registrado media cerca de 3,67 cm.
- ru: Самый длинный зафиксированный человеческий клык был около 3,67 см.
- pl: Najdłuższy odnotowany ludzki kieł miał około 3,67 cm.

### Q7 (정답: O)

**Question**
- ko: 입 안에는 수십억 개의 세균이 살고 있다.
- en: There are billions of bacteria living inside the mouth.
- ja: 口の中には数十億もの細菌が住んでいる。
- zh-Hans: 口腔里生活着数十亿细菌。
- zh-Hant: 口腔裡生活著數十億細菌。
- es: Hay miles de millones de bacterias viviendo dentro de la boca.
- es-419: Hay miles de millones de bacterias viviendo dentro de la boca.
- fr: Des milliards de bactéries vivent dans la bouche.
- de: Im Mund leben Milliarden von Bakterien.
- pt-BR: Existem bilhões de bactérias vivendo dentro da boca.
- ru: Во рту живут миллиарды бактерий.
- pl: W ustach żyją miliardy bakterii.

**Explanation**
- ko: 양치질을 해도 입 안에는 여전히 많은 세균이 남아 있다.
- en: Even after brushing, many bacteria still remain inside the mouth.
- ja: 歯を磨いた後でも、口の中には多くの細菌が残っている。
- zh-Hans: 即使刷过牙，口腔里仍然会留有大量细菌。
- zh-Hant: 即使刷過牙，口腔裡仍然會留有大量細菌。
- es: Incluso después de cepillarse, muchas bacterias siguen dentro de la boca.
- es-419: Incluso después de cepillarse los dientes, muchas bacterias siguen dentro de la boca.
- fr: Même après le brossage, de nombreuses bactéries restent dans la bouche.
- de: Selbst nach dem Zähneputzen bleiben viele Bakterien im Mund zurück.
- pt-BR: Mesmo depois de escovar os dentes, muitas bactérias continuam na boca.
- ru: Даже после чистки зубов во рту остаётся много бактерий.
- pl: Nawet po myciu zębów wiele bakterii wciąż pozostaje w ustach.

### Q8 (정답: X)

**Question**
- ko: 사람은 잠을 자는 동안 침을 훨씬 더 많이 분비한다.
- en: People produce much more saliva while sleeping.
- ja: 人は眠っている間、唾液をもっと多く分泌する。
- zh-Hans: 人在睡觉时会分泌更多唾液。
- zh-Hant: 人在睡覺時會分泌更多唾液。
- es: Las personas producen mucha más saliva mientras duermen.
- es-419: Las personas producen mucha más saliva mientras duermen.
- fr: On produit beaucoup plus de salive pendant le sommeil.
- de: Menschen produzieren im Schlaf viel mehr Speichel.
- pt-BR: As pessoas produzem muito mais saliva enquanto dormem.
- ru: Во время сна люди вырабатывают намного больше слюны.
- pl: Podczas snu ludzie wytwarzają znacznie więcej śliny.

**Explanation**
- ko: 잠자는 동안 침 분비는 거의 멈추는데, 이게 아침 입 냄새의 원인이 될 수 있다.
- en: Saliva production almost stops during sleep, which can cause bad breath in the morning.
- ja: 睡眠中は唾液の分泌がほぼ止まり、それが朝の口臭の原因になることがある。
- zh-Hans: 睡觉时唾液分泌几乎停止，这可能是早晨口臭的原因。
- zh-Hant: 睡覺時唾液分泌幾乎停止，這可能是早晨口臭的原因。
- es: La producción de saliva casi se detiene durante el sueño, lo que puede causar mal aliento por la mañana.
- es-419: La producción de saliva casi se detiene durante el sueño, lo que puede causar mal aliento por la mañana.
- fr: La production de salive s'arrête presque pendant le sommeil, ce qui peut causer une mauvaise haleine au réveil.
- de: Die Speichelproduktion kommt im Schlaf fast zum Stillstand, was morgens Mundgeruch verursachen kann.
- pt-BR: A produção de saliva quase para durante o sono, o que pode causar mau hálito de manhã.
- ru: Во время сна выработка слюны почти прекращается, из-за чего утром может появляться неприятный запах.
- pl: Podczas snu wytwarzanie śliny prawie zanika, co może powodować nieświeży oddech rano.

### Q9 (정답: O)

**Question**
- ko: 혀도 피로해질 수 있다.
- en: The tongue can get tired.
- ja: 舌も疲れることがある。
- zh-Hans: 舌头也会感到疲劳。
- zh-Hant: 舌頭也會感到疲勞。
- es: La lengua puede cansarse.
- es-419: La lengua puede cansarse.
- fr: La langue peut se fatiguer.
- de: Auch die Zunge kann müde werden.
- pt-BR: A língua também pode ficar cansada.
- ru: Язык тоже может уставать.
- pl: Język też może się zmęczyć.

**Explanation**
- ko: 혀는 근육으로 이루어져 있어서 많이 쓰면 피로해질 수 있다.
- en: The tongue is made of muscles, so it can become fatigued after heavy use.
- ja: 舌は筋肉でできているため、使いすぎると疲労することがある。
- zh-Hans: 舌头由肌肉构成，使用过度也会感到疲劳。
- zh-Hant: 舌頭由肌肉構成，使用過度也會感到疲勞。
- es: La lengua está hecha de músculos, así que puede fatigarse tras un uso intenso.
- es-419: La lengua está hecha de músculos, así que se puede fatigar tras un uso intenso.
- fr: La langue est faite de muscles, elle peut donc se fatiguer après un usage intense.
- de: Die Zunge besteht aus Muskeln und kann daher bei starker Belastung ermüden.
- pt-BR: A língua é feita de músculos, então pode ficar cansada depois de muito uso.
- ru: Язык состоит из мышц, поэтому может уставать после сильной нагрузки.
- pl: Język jest zbudowany z mięśni, więc może się zmęczyć po intensywnym użyciu.

### Q10 (정답: O)

**Question**
- ko: 치아는 뼈와 구조가 다르다.
- en: Teeth have a different structure from bones.
- ja: 歯は骨と構造が違う。
- zh-Hans: 牙齿的结构和骨骼不同。
- zh-Hant: 牙齒的結構和骨骼不同。
- es: Los dientes tienen una estructura distinta a la de los huesos.
- es-419: Los dientes tienen una estructura distinta a la de los huesos.
- fr: Les dents ont une structure différente de celle des os.
- de: Zähne haben eine andere Struktur als Knochen.
- pt-BR: Os dentes têm uma estrutura diferente da dos ossos.
- ru: У зубов иное строение, чем у костей.
- pl: Zęby mają inną budowę niż kości.

**Explanation**
- ko: 뼈와 달리 치아는 스스로 완전히 재생되지 않는다.
- en: Unlike bones, teeth cannot fully repair themselves.
- ja: 骨と違い、歯は自分で完全に修復できない。
- zh-Hans: 与骨骼不同，牙齿无法完全自我修复。
- zh-Hant: 與骨骼不同，牙齒無法完全自我修復。
- es: A diferencia de los huesos, los dientes no pueden repararse por completo.
- es-419: A diferencia de los huesos, los dientes no pueden repararse por completo.
- fr: Contrairement aux os, les dents ne peuvent pas se réparer complètement.
- de: Anders als Knochen können sich Zähne nicht vollständig selbst reparieren.
- pt-BR: Diferente dos ossos, os dentes não conseguem se reparar completamente.
- ru: В отличие от костей, зубы не могут полностью восстанавливаться сами.
- pl: W przeciwieństwie do kości, zęby nie mogą się w pełni samodzielnie naprawić.

### Q11 (정답: X)

**Question**
- ko: 사람의 입술이 붉은 건 붉은 색소가 많아서다.
- en: Human lips look red because they contain a lot of red pigment.
- ja: 人の唇が赤いのは赤い色素が多いからだ。
- zh-Hans: 人的嘴唇看起来红是因为含有大量红色色素。
- zh-Hant: 人的嘴唇看起來紅是因為含有大量紅色色素。
- es: Los labios humanos se ven rojos porque contienen mucho pigmento rojo.
- es-419: Los labios humanos se ven rojos porque tienen mucho pigmento rojo.
- fr: Les lèvres humaines paraissent rouges parce qu'elles contiennent beaucoup de pigment rouge.
- de: Menschliche Lippen wirken rot, weil sie viel rotes Pigment enthalten.
- pt-BR: Os lábios humanos parecem vermelhos porque têm muito pigmento vermelho.
- ru: Губы человека кажутся красными из-за большого количества красного пигмента.
- pl: Ludzkie wargi wydają się czerwone, bo zawierają dużo czerwonego pigmentu.

**Explanation**
- ko: 입술은 피부가 얇아서 그 아래 혈관의 색이 비쳐 보이는 것이다.
- en: Lips are thin, so the color of blood vessels underneath can be seen through the skin.
- ja: 唇は皮膚が薄いため、下にある血管の色が透けて見えるのだ。
- zh-Hans: 嘴唇皮肤很薄，所以能透出下面血管的颜色。
- zh-Hant: 嘴唇皮膚很薄，所以能透出下面血管的顏色。
- es: Los labios son finos, por lo que se ve el color de los vasos sanguíneos debajo de la piel.
- es-419: Los labios son delgados, así que se ve el color de los vasos sanguíneos debajo de la piel.
- fr: Les lèvres sont fines, ce qui laisse voir la couleur des vaisseaux sanguins en dessous.
- de: Lippen sind dünn, sodass die Farbe der darunterliegenden Blutgefäße durchscheint.
- pt-BR: Os lábios são finos, então dá para ver a cor dos vasos sanguíneos por baixo da pele.
- ru: Губы тонкие, поэтому сквозь них просвечивает цвет сосудов под кожей.
- pl: Wargi są cienkie, dlatego widać przez nie kolor naczyń krwionośnych pod skórą.

### Q12 (정답: O)

**Question**
- ko: 혀는 신체에서 가장 빨리 회복되는 부위 중 하나다.
- en: The tongue is one of the fastest-healing parts of the body.
- ja: 舌は体の中でも最も早く治る部位の一つだ。
- zh-Hans: 舌头是身体上愈合最快的部位之一。
- zh-Hant: 舌頭是身體上癒合最快的部位之一。
- es: La lengua es una de las partes del cuerpo que cicatriza más rápido.
- es-419: La lengua es una de las partes del cuerpo que sana más rápido.
- fr: La langue est l'une des parties du corps qui guérit le plus vite.
- de: Die Zunge ist einer der am schnellsten heilenden Teile des Körpers.
- pt-BR: A língua é uma das partes do corpo que cicatriza mais rápido.
- ru: Язык — одна из самых быстро заживающих частей тела.
- pl: Język jest jedną z najszybciej gojących się części ciała.

**Explanation**
- ko: 혀는 혈류가 풍부하고 세포 재생이 빨라서 빠르게 회복된다.
- en: The tongue heals quickly because it has strong blood flow and fast cell repair.
- ja: 舌は血流が豊富で細胞の修復が速いため、早く治る。
- zh-Hans: 舌头血流丰富、细胞修复快，所以愈合得很快。
- zh-Hant: 舌頭血流豐富、細胞修復快，所以癒合得很快。
- es: La lengua cicatriza rápido porque tiene un flujo sanguíneo fuerte y una reparación celular veloz.
- es-419: La lengua sana rápido porque tiene un flujo sanguíneo fuerte y una reparación celular veloz.
- fr: La langue guérit vite grâce à une forte circulation sanguine et une réparation cellulaire rapide.
- de: Die Zunge heilt schnell, weil sie stark durchblutet ist und Zellen sich schnell erneuern.
- pt-BR: A língua cicatriza rápido porque tem bastante fluxo sanguíneo e renovação celular rápida.
- ru: Язык заживает быстро благодаря сильному кровотоку и быстрому обновлению клеток.
- pl: Język goi się szybko, bo ma silny przepływ krwi i szybką odnowę komórek.

---

## T.Stage4 (13문항 — 식도 주제)

### Q1 (정답: X)

**Question**
- ko: 음식이 식도로 내려가는 건 중력 때문이다.
- en: Food goes down the esophagus because of gravity.
- ja: 食べ物が食道を下るのは重力のおかげだ。
- zh-Hans: 食物顺着食道往下走是因为重力。
- zh-Hant: 食物順著食道往下走是因為重力。
- es: La comida baja por el esófago por la gravedad.
- es-419: La comida baja por el esófago por la gravedad.
- fr: La nourriture descend dans l'œsophage grâce à la gravité.
- de: Nahrung rutscht durch die Speiseröhre wegen der Schwerkraft nach unten.
- pt-BR: A comida desce pelo esôfago por causa da gravidade.
- ru: Пища опускается по пищеводу благодаря силе тяжести.
- pl: Jedzenie przechodzi przez przełyk dzięki grawitacji.

**Explanation**
- ko: 식도는 근육 운동으로 음식을 밀어내리기 때문에, 몸이 뒤집혀 있어도 삼킬 수 있다.
- en: The esophagus pushes food down with muscle movements, so you can even swallow upside down.
- ja: 食道は筋肉の動きで食べ物を押し下げるので、逆さまでも飲み込むことができる。
- zh-Hans: 食道靠肌肉运动把食物往下推，所以即使倒立也能吞咽。
- zh-Hant: 食道靠肌肉運動把食物往下推，所以即使倒立也能吞嚥。
- es: El esófago empuja la comida hacia abajo con movimientos musculares, por eso incluso se puede tragar boca abajo.
- es-419: El esófago empuja la comida hacia abajo con movimientos musculares, así que incluso puedes tragar de cabeza.
- fr: L'œsophage pousse la nourriture vers le bas grâce à des mouvements musculaires, donc on peut même avaler la tête en bas.
- de: Die Speiseröhre schiebt Nahrung durch Muskelbewegungen nach unten, sodass man sogar kopfüber schlucken kann.
- pt-BR: O esôfago empurra a comida para baixo com movimentos musculares, então é possível engolir até de cabeça para baixo.
- ru: Пищевод продвигает еду вниз мышечными движениями, поэтому проглотить можно даже вверх ногами.
- pl: Przełyk przesuwa jedzenie ruchami mięśni, więc można przełykać nawet do góry nogami.

### Q2 (정답: X)

**Question**
- ko: 소화는 식도에서 일어난다.
- en: Digestion happens in the esophagus.
- ja: 消化は食道で行われる。
- zh-Hans: 消化是在食道里进行的。
- zh-Hant: 消化是在食道裡進行的。
- es: La digestión ocurre en el esófago.
- es-419: La digestión ocurre en el esófago.
- fr: La digestion se produit dans l'œsophage.
- de: Die Verdauung findet in der Speiseröhre statt.
- pt-BR: A digestão acontece no esôfago.
- ru: Пищеварение происходит в пищеводе.
- pl: Trawienie zachodzi w przełyku.

**Explanation**
- ko: 식도는 주로 음식을 위로 이동시키는 역할을 한다.
- en: The esophagus mainly moves food to the stomach.
- ja: 食道は主に食べ物を胃へ運ぶ役割をする。
- zh-Hans: 食道主要负责把食物运送到胃里。
- zh-Hant: 食道主要負責把食物運送到胃裡。
- es: El esófago se encarga principalmente de llevar la comida al estómago.
- es-419: El esófago se encarga principalmente de llevar la comida al estómago.
- fr: L'œsophage se contente surtout de transporter la nourriture vers l'estomac.
- de: Die Speiseröhre transportiert Nahrung hauptsächlich zum Magen.
- pt-BR: O esôfago serve principalmente para levar a comida até o estômago.
- ru: Пищевод в основном просто перемещает еду в желудок.
- pl: Przełyk głównie przenosi jedzenie do żołądka.

### Q3 (정답: X)

**Question**
- ko: 식도는 항상 열려 있다.
- en: The esophagus is always open.
- ja: 食道は常に開いている。
- zh-Hans: 食道一直是打开的。
- zh-Hant: 食道一直是打開的。
- es: El esófago siempre está abierto.
- es-419: El esófago siempre está abierto.
- fr: L'œsophage est toujours ouvert.
- de: Die Speiseröhre ist immer geöffnet.
- pt-BR: O esôfago está sempre aberto.
- ru: Пищевод всегда открыт.
- pl: Przełyk jest zawsze otwarty.

**Explanation**
- ko: 평소에는 닫혀 있고, 음식이 지나갈 때만 열린다.
- en: It normally stays closed and only opens when food passes through.
- ja: 普段は閉じていて、食べ物が通るときだけ開く。
- zh-Hans: 平时是关闭的，只有食物经过时才会打开。
- zh-Hant: 平時是關閉的，只有食物經過時才會打開。
- es: Normalmente está cerrado y solo se abre cuando pasa comida.
- es-419: Normalmente está cerrado y solo se abre cuando pasa comida.
- fr: Il reste normalement fermé et ne s'ouvre que quand la nourriture y passe.
- de: Normalerweise ist sie geschlossen und öffnet sich nur, wenn Nahrung durchgeht.
- pt-BR: Normalmente ele fica fechado e só abre quando a comida passa.
- ru: Обычно он закрыт и открывается только тогда, когда через него проходит еда.
- pl: Zwykle jest zamknięty i otwiera się tylko, gdy przechodzi przez niego jedzenie.

### Q4 (정답: X)

**Question**
- ko: 식도는 음식을 아래쪽으로만 이동시킨다.
- en: The esophagus only moves food downward.
- ja: 食道は食べ物を下方向にだけ動かす。
- zh-Hans: 食道只会把食物往下移动。
- zh-Hant: 食道只會把食物往下移動。
- es: El esófago solo mueve la comida hacia abajo.
- es-419: El esófago solo mueve la comida hacia abajo.
- fr: L'œsophage ne déplace la nourriture que vers le bas.
- de: Die Speiseröhre bewegt Nahrung nur nach unten.
- pt-BR: O esôfago só move a comida para baixo.
- ru: Пищевод перемещает еду только вниз.
- pl: Przełyk przesuwa jedzenie tylko w dół.

**Explanation**
- ko: 구토할 때는 반대로 음식이 위쪽으로 이동할 수 있다.
- en: During vomiting, food can move upward instead.
- ja: 嘔吐するときは反対に食べ物が上に動くことがある。
- zh-Hans: 呕吐时食物反而会往上移动。
- zh-Hant: 嘔吐時食物反而會往上移動。
- es: Durante los vómitos, la comida puede moverse hacia arriba.
- es-419: Durante los vómitos, la comida puede moverse hacia arriba.
- fr: Pendant les vomissements, la nourriture peut au contraire remonter.
- de: Beim Erbrechen kann sich Nahrung stattdessen nach oben bewegen.
- pt-BR: Durante o vômito, a comida pode se mover para cima.
- ru: Во время рвоты еда, наоборот, может двигаться вверх.
- pl: Podczas wymiotów jedzenie może przemieszczać się do góry.

### Q5 (정답: O)

**Question**
- ko: 식도와 위 사이에는 위산 역류를 막아주는 근육이 있다.
- en: There is a muscle between the esophagus and stomach that helps stop acid reflux.
- ja: 食道と胃の間には、逆流を防ぐ筋肉がある。
- zh-Hans: 食道和胃之间有一块能防止胃酸反流的肌肉。
- zh-Hant: 食道和胃之間有一塊能防止胃酸逆流的肌肉。
- es: Entre el esófago y el estómago hay un músculo que ayuda a evitar el reflujo ácido.
- es-419: Entre el esófago y el estómago hay un músculo que ayuda a evitar el reflujo ácido.
- fr: Il y a un muscle entre l'œsophage et l'estomac qui aide à empêcher les reflux acides.
- de: Zwischen Speiseröhre und Magen gibt es einen Muskel, der Reflux verhindert.
- pt-BR: Entre o esôfago e o estômago existe um músculo que ajuda a evitar o refluxo ácido.
- ru: Между пищеводом и желудком есть мышца, которая помогает предотвращать кислотный рефлюкс.
- pl: Między przełykiem a żołądkiem jest mięsień, który pomaga zapobiegać refluksowi.

**Explanation**
- ko: 하부 식도 조임근이 위산이 다시 올라오는 것을 막아준다.
- en: The lower esophageal sphincter helps keep stomach acid from coming back up.
- ja: 下部食道括約筋が胃酸の逆流を防いでくれる。
- zh-Hans: 食道下段的括约肌能防止胃酸倒流上来。
- zh-Hant: 食道下段的括約肌能防止胃酸倒流上來。
- es: El esfínter esofágico inferior ayuda a evitar que el ácido del estómago suba de nuevo.
- es-419: El esfínter esofágico inferior ayuda a evitar que el ácido del estómago suba de nuevo.
- fr: Le sphincter œsophagien inférieur empêche l'acide gastrique de remonter.
- de: Der untere Ösophagussphinkter verhindert, dass Magensäure wieder hochsteigt.
- pt-BR: O esfíncter esofágico inferior ajuda a impedir que o ácido do estômago volte a subir.
- ru: Нижний пищеводный сфинктер не даёт желудочной кислоте подниматься обратно.
- pl: Dolny zwieracz przełyku pomaga powstrzymać kwas żołądkowy przed powrotem do góry.

### Q6 (정답: X)

**Question**
- ko: 식도암은 초기에 발견하기 쉽다.
- en: Esophageal cancer is easy to detect early.
- ja: 食道がんは早期に発見しやすい。
- zh-Hans: 食道癌很容易早期发现。
- zh-Hant: 食道癌很容易早期發現。
- es: El cáncer de esófago es fácil de detectar en etapas tempranas.
- es-419: El cáncer de esófago es fácil de detectar en etapas tempranas.
- fr: Le cancer de l'œsophage est facile à détecter tôt.
- de: Speiseröhrenkrebs lässt sich früh leicht erkennen.
- pt-BR: O câncer de esôfago é fácil de detectar no início.
- ru: Рак пищевода легко обнаружить на ранней стадии.
- pl: Rak przełyku łatwo wykryć na wczesnym etapie.

**Explanation**
- ko: 초기에는 증상이 거의 없어서 늦게 발견되는 경우가 많다.
- en: It often has few symptoms at first, so it may be found late.
- ja: 初期には症状がほとんどないため、発見が遅れることが多い。
- zh-Hans: 早期症状很少，所以往往会被发现得比较晚。
- zh-Hant: 早期症狀很少，所以往往會被發現得比較晚。
- es: Al principio suele tener pocos síntomas, por lo que puede detectarse tarde.
- es-419: Al principio suele tener pocos síntomas, así que puede detectarse tarde.
- fr: Il présente souvent peu de symptômes au début, donc il peut être découvert tard.
- de: Anfangs gibt es oft nur wenige Symptome, weshalb er häufig spät erkannt wird.
- pt-BR: No início costuma ter poucos sintomas, então pode ser descoberto tarde.
- ru: На ранней стадии симптомов почти нет, поэтому его часто обнаруживают поздно.
- pl: Na początku ma zwykle niewiele objawów, więc bywa wykrywany późno.

### Q7 (정답: X)

**Question**
- ko: 딸꾹질은 식도 때문에 생긴다.
- en: Hiccups are caused by the esophagus.
- ja: しゃっくりは食道が原因で起こる。
- zh-Hans: 打嗝是食道引起的。
- zh-Hant: 打嗝是食道引起的。
- es: El hipo es causado por el esófago.
- es-419: El hipo es causado por el esófago.
- fr: Le hoquet est causé par l'œsophage.
- de: Schluckauf wird durch die Speiseröhre verursacht.
- pt-BR: O soluço é causado pelo esôfago.
- ru: Икота вызывается пищеводом.
- pl: Czkawka jest spowodowana przez przełyk.

**Explanation**
- ko: 딸꾹질은 횡격막이 갑자기 수축할 때 일어난다.
- en: Hiccups happen when the diaphragm suddenly contracts.
- ja: しゃっくりは横隔膜が突然収縮するときに起こる。
- zh-Hans: 打嗝是横膈膜突然收缩时发生的。
- zh-Hant: 打嗝是橫膈膜突然收縮時發生的。
- es: El hipo ocurre cuando el diafragma se contrae de repente.
- es-419: El hipo ocurre cuando el diafragma se contrae de repente.
- fr: Le hoquet survient quand le diaphragme se contracte soudainement.
- de: Schluckauf entsteht, wenn sich das Zwerchfell plötzlich zusammenzieht.
- pt-BR: O soluço acontece quando o diafragma se contrai de repente.
- ru: Икота возникает при внезапном сокращении диафрагмы.
- pl: Czkawka pojawia się, gdy przepona nagle się skurczy.

### Q8 (정답: O)

**Question**
- ko: 식도는 안에 음식이 없어도 움직일 수 있다.
- en: The esophagus can move even when there is no food inside.
- ja: 食道は中に食べ物がなくても動くことがある。
- zh-Hans: 即使里面没有食物，食道也能蠕动。
- zh-Hant: 即使裡面沒有食物，食道也能蠕動。
- es: El esófago puede moverse incluso cuando no hay comida dentro.
- es-419: El esófago puede moverse incluso cuando no hay comida dentro.
- fr: L'œsophage peut bouger même quand il n'y a pas de nourriture à l'intérieur.
- de: Die Speiseröhre kann sich auch bewegen, wenn keine Nahrung drin ist.
- pt-BR: O esôfago pode se movimentar mesmo sem comida dentro dele.
- ru: Пищевод может двигаться, даже когда внутри нет еды.
- pl: Przełyk może się poruszać, nawet gdy nie ma w nim jedzenia.

**Explanation**
- ko: 식도에서는 작은 근육 운동이 계속 일어날 수 있다.
- en: Small muscle movements can still happen in the esophagus.
- ja: 食道では小さな筋肉の動きが続くことがある。
- zh-Hans: 食道里仍然会有细微的肌肉蠕动。
- zh-Hant: 食道裡仍然會有細微的肌肉蠕動。
- es: En el esófago pueden seguir ocurriendo pequeños movimientos musculares.
- es-419: En el esófago pueden seguir ocurriendo pequeños movimientos musculares.
- fr: De petits mouvements musculaires peuvent tout de même se produire dans l'œsophage.
- de: In der Speiseröhre können weiterhin kleine Muskelbewegungen stattfinden.
- pt-BR: Pequenos movimentos musculares ainda podem ocorrer no esôfago.
- ru: В пищеводе всё равно могут происходить небольшие мышечные движения.
- pl: W przełyku wciąż mogą zachodzić niewielkie ruchy mięśni.

### Q9 (정답: O)

**Question**
- ko: 식도는 공기도 이동시킬 수 있다.
- en: The esophagus can also move air.
- ja: 食道は空気も動かすことができる。
- zh-Hans: 食道也能输送空气。
- zh-Hant: 食道也能輸送空氣。
- es: El esófago también puede mover aire.
- es-419: El esófago también puede mover aire.
- fr: L'œsophage peut aussi déplacer de l'air.
- de: Die Speiseröhre kann auch Luft transportieren.
- pt-BR: O esôfago também consegue mover ar.
- ru: Пищевод может перемещать и воздух.
- pl: Przełyk może również przenosić powietrze.

**Explanation**
- ko: 트림을 할 때 공기가 식도를 통해 이동한다.
- en: Air moves through the esophagus when you burp.
- ja: げっぷをするとき、空気は食道を通って移動する。
- zh-Hans: 打嗝时空气会通过食道移动。
- zh-Hant: 打嗝時空氣會通過食道移動。
- es: El aire pasa por el esófago cuando eructas.
- es-419: El aire pasa por el esófago cuando eructas.
- fr: L'air passe par l'œsophage quand on rote.
- de: Beim Aufstoßen bewegt sich Luft durch die Speiseröhre.
- pt-BR: O ar passa pelo esôfago quando você arrota.
- ru: Когда ты отрыгиваешь, воздух проходит через пищевод.
- pl: Podczas odbijania powietrze przechodzi przez przełyk.

### Q10 (정답: O)

**Question**
- ko: 물은 식도에 잠깐 머물러 있을 수 있다.
- en: Water can stay in the esophagus for a short time.
- ja: 水は食道に少しの間留まることがある。
- zh-Hans: 水可以在食道里短暂停留。
- zh-Hant: 水可以在食道裡短暫停留。
- es: El agua puede quedarse un momento en el esófago.
- es-419: El agua puede quedarse un momento en el esófago.
- fr: L'eau peut rester un court instant dans l'œsophage.
- de: Wasser kann kurz in der Speiseröhre bleiben.
- pt-BR: A água pode ficar um pouco no esôfago.
- ru: Вода может ненадолго задерживаться в пищеводе.
- pl: Woda może na chwilę zatrzymać się w przełyku.

**Explanation**
- ko: 물도 근육 운동을 통해 아래로 이동한다.
- en: Water also moves down through muscle movements.
- ja: 水も筋肉の動きによって下へ移動する。
- zh-Hans: 水也是靠肌肉运动往下移动的。
- zh-Hant: 水也是靠肌肉運動往下移動的。
- es: El agua también baja gracias a los movimientos musculares.
- es-419: El agua también baja gracias a los movimientos musculares.
- fr: L'eau descend aussi grâce aux mouvements musculaires.
- de: Auch Wasser bewegt sich durch Muskelbewegungen nach unten.
- pt-BR: A água também desce por meio de movimentos musculares.
- ru: Вода тоже опускается благодаря мышечным движениям.
- pl: Woda również przesuwa się w dół dzięki ruchom mięśni.

### Q11 (정답: O)

**Question**
- ko: 식도는 스스로 언제 열고 닫을지를 조절한다.
- en: The esophagus controls when to open and close by itself.
- ja: 食道は自分でいつ開いて閉じるかを調整する。
- zh-Hans: 食道会自行控制何时开合。
- zh-Hant: 食道會自行控制何時開合。
- es: El esófago controla por sí mismo cuándo abrirse y cerrarse.
- es-419: El esófago controla por sí mismo cuándo abrirse y cerrarse.
- fr: L'œsophage contrôle lui-même le moment où il s'ouvre et se ferme.
- de: Die Speiseröhre steuert selbst, wann sie sich öffnet und schließt.
- pt-BR: O esôfago controla por conta própria quando abrir e fechar.
- ru: Пищевод сам управляет тем, когда открываться и закрываться.
- pl: Przełyk sam kontroluje, kiedy się otwierać i zamykać.

**Explanation**
- ko: 식도의 움직임은 신경계에 의해 자동으로 조절된다.
- en: Its movements are automatically controlled by the nervous system.
- ja: 食道の動きは神経系によって自動的に調整される。
- zh-Hans: 它的运动是由神经系统自动控制的。
- zh-Hant: 它的運動是由神經系統自動控制的。
- es: Sus movimientos son controlados automáticamente por el sistema nervioso.
- es-419: Sus movimientos son controlados automáticamente por el sistema nervioso.
- fr: Ses mouvements sont contrôlés automatiquement par le système nerveux.
- de: Ihre Bewegungen werden automatisch vom Nervensystem gesteuert.
- pt-BR: Os movimentos dele são controlados automaticamente pelo sistema nervoso.
- ru: Его движения автоматически контролируются нервной системой.
- pl: Jego ruchy są automatycznie kontrolowane przez układ nerwowy.

### Q12 (정답: O)

**Question**
- ko: 식도는 음식 냄새만 맡아도 움직일 준비를 한다.
- en: The esophagus gets ready to move just by smelling food.
- ja: 食道は食べ物の匂いを感じるだけでも動く準備をする。
- zh-Hans: 光是闻到食物的味道，食道也会做好蠕动的准备。
- zh-Hant: 光是聞到食物的味道，食道也會做好蠕動的準備。
- es: El esófago se prepara para moverse solo con oler la comida.
- es-419: El esófago se prepara para moverse solo con oler la comida.
- fr: L'œsophage se prépare à bouger juste en sentant l'odeur de la nourriture.
- de: Die Speiseröhre bereitet sich schon vor, wenn sie nur den Geruch von Essen wahrnimmt.
- pt-BR: O esôfago já se prepara para se movimentar só com o cheiro da comida.
- ru: Пищевод начинает готовиться к движению, просто почувствовав запах еды.
- pl: Przełyk zaczyna się przygotowywać do ruchu już na sam zapach jedzenia.

**Explanation**
- ko: 뇌는 음식이 들어올 것을 예상하면 소화기관을 활성화한다.
- en: The brain activates the digestive system when it expects food.
- ja: 脳は食べ物が来ると予想すると消化器官を活性化させる。
- zh-Hans: 大脑一旦预期有食物进来，就会激活消化系统。
- zh-Hant: 大腦一旦預期有食物進來，就會激活消化系統。
- es: El cerebro activa el sistema digestivo cuando espera que llegue comida.
- es-419: El cerebro activa el sistema digestivo cuando espera que llegue comida.
- fr: Le cerveau active le système digestif quand il anticipe l'arrivée de nourriture.
- de: Das Gehirn aktiviert das Verdauungssystem, wenn es Nahrung erwartet.
- pt-BR: O cérebro ativa o sistema digestivo quando espera que a comida chegue.
- ru: Мозг активирует пищеварительную систему, когда ожидает поступление еды.
- pl: Mózg aktywuje układ pokarmowy, gdy oczekuje jedzenia.

### Q13 (정답: O)

**Question**
- ko: 약을 삼킨 뒤에도 목에 걸린 느낌이 들 수 있다.
- en: A pill can feel stuck in your throat even after it has gone down.
- ja: 薬を飲み込んだあとでも、喉に引っかかっている感じがすることがある。
- zh-Hans: 药丸吞下去之后，喉咙里也可能还有卡住的感觉。
- zh-Hant: 藥丸吞下去之後，喉嚨裡也可能還有卡住的感覺。
- es: Una pastilla puede sentirse atascada en la garganta incluso después de haberla tragado.
- es-419: Una pastilla puede sentirse atascada en la garganta incluso después de haberla tragado.
- fr: Un comprimé peut sembler coincé dans la gorge même après avoir été avalé.
- de: Eine Tablette kann sich noch im Hals festsitzend anfühlen, obwohl sie schon runtergeschluckt wurde.
- pt-BR: Um comprimido pode parecer que ficou preso na garganta mesmo depois de já ter descido.
- ru: Таблетка может ощущаться застрявшей в горле, даже когда она уже прошла дальше.
- pl: Tabletka może wydawać się utkwiona w gardle, nawet gdy już zeszła niżej.

**Explanation**
- ko: 식도의 자극 때문에 뇌가 여전히 뭔가 남아 있다고 느낄 수 있다.
- en: Irritation in the esophagus can make your brain still feel something there.
- ja: 食道の刺激によって、脳がまだ何か残っていると感じることがある。
- zh-Hans: 食道受到刺激，会让大脑仍然觉得那里还有东西。
- zh-Hant: 食道受到刺激，會讓大腦仍然覺得那裡還有東西。
- es: La irritación en el esófago puede hacer que tu cerebro siga sintiendo algo ahí.
- es-419: La irritación en el esófago puede hacer que tu cerebro siga sintiendo algo ahí.
- fr: Une irritation de l'œsophage peut faire croire au cerveau qu'il reste quelque chose là.
- de: Eine Reizung der Speiseröhre kann dazu führen, dass dein Gehirn dort weiterhin etwas spürt.
- pt-BR: A irritação no esôfago pode fazer seu cérebro continuar sentindo que há algo ali.
- ru: Раздражение в пищеводе может заставить мозг всё ещё чувствовать, что там что-то есть.
- pl: Podrażnienie przełyku może sprawić, że mózg wciąż czuje, że coś tam zostało.

---

## 적용 체크리스트 (사용자 — 에디터 작업)

1. `OXQuizManager.cs` 코드 변경 반영 확인 (이미 완료 — `questionText`/`explanationText`가 `LocalizedString`, M.Stage2·T.Stage4 둘 다 같은 클래스라 한 번에 적용됨).
2. `Window > Asset Management > Localization Tables`에서 새 컬렉션 `OXQuiz` 생성.
3. 위 키(`M.Stage2.Q1.Question` ~ `M.Stage2.Q12.Explanation`, `T.Stage4.Q1.Question` ~ `T.Stage4.Q13.Explanation`, 총 50개)를 만들고 각 언어 컬럼에 이 문서 값 채우기.
4. `M.Stage2` 씬의 `OXQuizManager` 인스펙터에서 `questions[0~11]`, `T.Stage4` 씬의 `OXQuizManager` 인스펙터에서 `questions[0~12]`의 `questionText`/`explanationText` 필드를 각각 `OXQuiz` 테이블의 해당 키로 연결 (`correctAnswerIsO`는 그대로 유지 — 이미 맞게 설정돼 있음).
5. Play 모드에서 Locale 몇 개 바꿔가며 두 스테이지 모두 문제/해설이 정상 표시되는지 스모크 테스트.

## 참고 — OXQuizUI.cs 하드코딩 텍스트 (이번 범위 아님)

`Assets/Scripts/UI/OXQuizUI.cs`의 `"TRUE"/"FALSE"/"Clear!"`는 아직 로컬라이제이션 대상이 아님 — 필요하면 이후 별도 요청으로 처리.
