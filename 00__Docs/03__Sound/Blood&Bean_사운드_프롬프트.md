# BLOOD & BEAN — 사운드 생성 프롬프트 (VARCO Sound)

> 효과음 목록과 번호는 `Blood&Bean_효과음_리스트.md`를 따른다. 이 문서는 **뽑는 법**만 다룬다.
>
> 파일 이름은 `SFX_S{번호}_{이름}.wav` — 44.1kHz · 모노 · 16bit (`SFX/CLAUDE.md`)

---

## 0. 쓰는 법

1. **공통 톤 문장 + 개별 프롬프트**를 이어 붙여 넣는다. 공통 문장을 빼면 소리마다 다른 게임처럼 들린다
2. 프롬프트는 영어로 적었다. 결과가 어긋나면 VARCO의 **텍스트 피드백**으로 고친다 — 「더 짧게」 「더 만화처럼」 「더 가볍게」 「잔향 없이」
3. **한 세트는 한 번에 뽑는다** — S-06 ~ S-08 판정, S-32 등급 3종, S-28 · S-70 스킬 4종. 같은 악기 · 같은 질감이어야 서로 구분이 산다
4. 자주 울리는 소리(S-01 · S-29 · S-54)는 **Variation**으로 2~3개 더 뽑아 번갈아 쓴다
5. 루프(S-15 · S-30 · S-41 · S-64 · 앰비언스 · BGM)는 **Loop** 도구로 이음매를 맞춘다
6. 생성한 소리는 앞뒤 무음을 잘라 길이를 표에 맞춘다

### 공통 톤 — 효과음 앞에 붙인다

**낮 · UI**
```
Stylized cartoon sound effect for a cute-spooky 3D party game where little monsters run a café. Playful, punchy, exaggerated like Fall Guys or Party Animals. Clean and dry, no music, no reverb tail, starts instantly, mono.
```

**밤** — 안개 숲. 낮과 같은 게임이되 조금 어둡게
```
Stylized cartoon sound effect for a cute-spooky 3D party game, night scene in a foggy haunted forest. Mischievous and slightly dark but still comedic, never scary or painful. Clean and dry, no music, starts instantly, mono.
```

---

### 먼저 뽑을 것 — 지금 들어 있는 임시본

**`SFX/` 폴더의 wav 20개는 코드로 합성한 임시본이다** (`_src/make_sfx.py`). 소리가 허접한 것은 이 때문이고, 게임에 이미 붙어 있으니 **이것부터 바꾼다.** 파일 이름을 그대로 두고 덮어쓰면 코드는 손댈 필요가 없다.

|순서|파일|이유|
|---|---|---|
|1|S-06 · S-07 · S-08 판정 3종 · S-10 판매 · S-11 코인|가장 자주 듣는 보상음|
|2|S-01a · b · c 재료 담기 · S-62 「안 됨」 · S-16 건네기|F를 누를 때마다 울린다|
|3|S-04 게이지 등장 · S-05 게이지 틱 · S-14 쓰레기 · S-27 부딪힘 · S-37 대시|낮 한 바퀴|
|4|S-33 블러드 빈 · S-34 보석|밤의 큰 사건|
|5|S-12 오배송 (`_TEMP`)|목소리라 합성 임시본이 가장 어색하다. 새로 뽑으면 이름에서 `_TEMP`를 뗀다|
|—|S-26 보석 만료 · S-58 블러드 빈 담기|**직접 뽑지 않는다.** S-34를 거꾸로, S-33의 첫 박동만 잘라 다시 굽는다|

---

## 1. 효과음

**길이 기준** — 잦은 것 0.15초 · 행동 0.2~0.4초 · 사건 0.4~0.6초 · 드문 것 1초 이상. `톤`은 앞에 붙일 공통 문장이다.

### 1.1 낮 코어 루프

|#|파일|길이|톤|프롬프트|
|---|---|---|---|---|
|S-01a|SFX_S01a_Ingredient_Liquid|0.15초|낮|`quick small liquid pour plop into a cup, milk splash, tiny and soft`|
|S-01b|SFX_S01b_Ingredient_Hard|0.15초|낮|`quick small hard pieces dropping into a cup, coffee beans and ice clink, tiny`|
|S-01c|SFX_S01c_Ingredient_Soft|0.15초|낮|`quick soft squishy plop, whipped cream or bread landing, tiny and fluffy`|
|S-02|SFX_S02_Insert|0.25초|낮|`cartoon cup slotted into a coffee machine, mechanical clack-lock, satisfying`|
|S-04|SFX_S04_GaugeDing|0.4초|낮|`bright attention ding, small bell, "it's ready" notification, cheerful`|
|S-05|SFX_S05_GaugeTick|0.05초|낮|`single tiny wooden tick, clock tick, very short, will be repeated rapidly`|
|S-06|SFX_S06_Perfect|0.5초|낮|`judgement set 1 of 3: best result, sparkling two-note rising chime, xylophone and bells, very rewarding`|
|S-07|SFX_S07_Good|0.4초|낮|`judgement set 2 of 3: neutral okay result, single plain xylophone note, same instrument as the best result`|
|S-08|SFX_S08_Miss|0.45초|낮|`judgement set 3 of 3: small mistake, two-note falling xylophone, mildly disappointed not harsh, same instrument`|
|S-09|SFX_S09_Burnt|0.7초|낮|`food burning, cartoon sizzle hiss then a low dull bell, smoky failure`|
|S-10|SFX_S10_Sale|0.5초|낮|`happy sale completed, cheerful rising chime with a cash register ka-ching flavor`|
|S-11|SFX_S11_Coin|0.3초|낮|`handful of coins jingling into a pile, bright and light`|
|S-14|SFX_S14_Spoiled|0.35초|낮|`cartoon deflating pfft, gross bubble pop, short and comic`|
|S-15|SFX_S15_Wash|3초 루프 + 0.3초|낮|`cartoon dish washing loop, running water and bubbles, ends with a clean squeaky click`|
|S-16|SFX_S16_HandOff|0.3초|낮|`quick two-note toss and catch, item passed from hand to hand, friendly`|
|S-18|SFX_S18_StationFree|0.4초|낮|`distinct bright ping meaning "machine is free now", different from a bell ding, audible from far`|

### 1.2 손님과 경고

|#|파일|길이|톤|프롬프트|
|---|---|---|---|---|
|S-12|SFX_S12_Misdelivery|0.8초|낮|`cute ghost customer gets angry, short huffy grumble voice plus a low buzzer, small crowd murmur behind`|
|S-13|SFX_S13_CustomerLeave|0.8초|낮|`cute ghost sighs sadly and floats away, ending with a soft door close`|
|S-19|SFX_S19_CustomerEnter|0.35초|낮|`cute little ghost pops in, small boop with a tiny breathy "ooh" voice, soft`|
|S-20|SFX_S20_PatienceAlert|0.5초|낮|`urgent but cute alert, two quick high beeps like a café bell, noticeable from a distance`|
|S-24|SFX_S24_Dump|0.4초|낮|`liquid splashing into a trash can, swish then a hollow thud at the bottom`|
|S-27|SFX_S27_Bump|0.2초|낮|`two soft cartoon bodies bump, rubbery boing thud, funny not painful`|
|S-29|SFX_S29_Footstep|0.1초|낮|`single tiny cartoon footstep, soft padded tap, very quiet` (Variation 3개)|

### 1.3 스킬 — 4종씩 한 세트로

|#|파일|길이|톤|프롬프트|
|---|---|---|---|---|
|S-28a|SFX_S28a_Coffin|0.5초|낮|`skill set 1 of 4: heavy wooden coffin drops and slams on stone floor, cartoon thump with dust puff, comedic`|
|S-28b|SFX_S28b_Bite|0.6초|낮|`skill set 2 of 4: cartoon zombie chomp bite, then a short goofy groan "uuurgh"`|
|S-28c|SFX_S28c_Howl|1.2초|낮|`skill set 3 of 4: short cartoon werewolf howl "awooo", playful and cute, not scary`|
|S-28d|SFX_S28d_FrogCurse|0.6초|낮|`skill set 4 of 4: magic poof puff of smoke, then a single cute frog croak "ribbit"`|
|S-70a|SFX_S70a_Bats|0.8초|밤|`skill set 1 of 4: swarm of little bats flapping past, flutter whoosh with tiny squeaks`|
|S-70b|SFX_S70b_ArmThrow|0.6초|밤|`skill set 2 of 4: cartoon zombie arm thrown, whoosh then a wet slap grab`|
|S-70c|SFX_S70c_Sniff|0.5초|밤|`skill set 3 of 4: two quick cartoon dog sniffs "sniff sniff", clear and audible`|
|S-70d|SFX_S70d_SlipPotion|0.6초|밤|`skill set 4 of 4: small glass potion bottle shatters, then a sticky gooey splat`|
|S-80|SFX_S80_SlipFall|0.8초|밤|`cartoon slip on goo, squeaky slide then a butt-thump landing, little stars twinkling`|
|S-81|SFX_S81_CoffinVanish|0.5초|낮|`creaky wooden coffin lid swings open, then a soft poof vanish`|

### 1.4 밤

|#|파일|길이|톤|프롬프트|
|---|---|---|---|---|
|S-30|SFX_S30_BoxOpen|1.5초 루프 + 0.3초 + 0.2초|밤|`treasure chest rattling while being pried open (loop), then lid pops open; separate take: lid slams shut`|
|S-31|SFX_S31_SlotReveal|1초|밤|`magical rising shimmer, filling up anticipation, ends right before a reveal`|
|S-32|SFX_S32_Grade1 · 2 · 3|0.4 · 0.5 · 0.7초|밤|`rarity reveal set of 3, same instrument: grade 1 plain wooden knock chime; grade 2 silvery metallic chime; grade 3 rich golden-purple magical chime with sparkle`|
|S-33|SFX_S33_BloodBean|1.2초|밤|`legendary find, deep booming low hit with a heartbeat thump-thump, blood-red magical glow, the biggest sound in the game`|
|S-34|SFX_S34_Gem|0.6초|밤|`precious gem found, golden bell sparkle, smaller than a legendary`|
|S-37|SFX_S37_Dash|0.25초|낮|`quick cartoon dash whoosh, air swipe`|
|S-38|SFX_S38_Hit|0.3초|밤|`cartoon body slam hit, soft poof thump with a squeak, funny not painful`|
|S-39|SFX_S39_ItemsSpill|0.6초|밤|`many small items tumbling out of a backpack onto the ground, rattle clatter, count-able pieces`|
|S-40|SFX_S40_Heavy|0.6초|밤|`little monster breathing heavily under a heavy load, cute huff and puff`|
|S-41|SFX_S41_Dig|1.5초 루프 + 0.3초|밤|`digging dirt with hands loop, soft earth scoops, ends with a pat-pat`|
|S-42|SFX_S42_FogClear|0.6초|밤|`very soft airy whoosh, fog parting, subtle`|
|S-43a|SFX_S43a_ReturnOK|1초|밤|`magic light beam lifting up, rising whoosh sparkle, relief`|
|S-43b|SFX_S43b_ReturnFail|0.7초|밤|`backpack bursts open, cartoon pop and items scattering away`|
|S-63|SFX_S63_HoldCancel|0.3초|낮|`cartoon deflate shrink sound, quick descending wobble`|
|S-64|SFX_S64_Burn|3초 루프|밤|`fire catching on a buried bag, crackling flames, with a mischievous cartoon giggle`|
|S-66|SFX_S66_BagRespawn|0.2초|밤|`light pop, backpack appears on back, bouncy`|
|S-67|SFX_S67_NightBell|1.2초|밤|`distant church-like bell, two tolls, "time to go home" warning`|

### 1.5 조작 반응

|#|파일|길이|톤|프롬프트|
|---|---|---|---|---|
|S-54|SFX_S54_DishPick|0.15초|낮|`light ceramic cup clink, picked up` (Variation 2개)|
|S-56|SFX_S56_DirtyDish|0.25초|낮|`ceramic clink with a sticky gross squelch layer, comic`|
|S-57|SFX_S57_MenuReady|0.25초|낮|`single short bright rising sparkle note, recipe completed`|
|S-61|SFX_S61_ZoneBlock|0.25초|낮|`springy elastic bounce off an invisible wall, boing, clearly different from a solid wall thud`|
|S-62|SFX_S62_NotAllowed|0.15초|낮|`short low dull "nope" thunk, descending pitch, not annoying`|

### 1.6 메타 · UI

|#|파일|길이|톤|프롬프트|
|---|---|---|---|---|
|S-44|SFX_S44_DayChange|1.2초|낮|`transition sting, day turning to night, whimsical spooky harp and bell flourish`|
|S-45|SFX_S45_RentPaid|1초|낮|`coin count-up ticks then a golden success chime`|
|S-46|SFX_S46_RentUnpaid|0.6초|낮|`heavy rubber stamp thump, low and disappointing`|
|S-47|SFX_S47_Forecast|0.5초|낮|`paper unfolding with a small magical chime, news reveal`|
|S-48|SFX_S48_RankChange|0.15초|낮|`tiny soft UI tick, subtle`|
|S-49a|SFX_S49a_Win|2초|낮|`short victory jingle, playful spooky orchestra, celebratory`|
|S-49b|SFX_S49b_Lose|2초|낮|`short losing jingle, sad trombone wah-wah, comedic`|
|S-50|SFX_S50_Recipe|0.3초|낮|`paper page flip`|
|S-51|SFX_S51_Button|0.08초|낮|`soft UI button click, wooden and cute`|
|S-68|SFX_S68_CooldownReady|0.25초|낮|`short bright ready ping, ability recharged`|
|S-71|SFX_S71_RentWarning|0.6초|낮|`warning sting about money, low brass two-note, different from a customer alert`|
|S-73|SFX_S73_CountTick|0.1초|낮|`clean countdown tick, woodblock`|
|S-74|SFX_S74_Stamp|0.8초|낮|`big closing stamp thump with a low bell`|
|S-76|SFX_S76_Start|0.5초|낮|`bright two-note rising start signal, "go!"`|

### 1.7 앰비언스 — 30~60초를 뽑아 Loop로 잇는다

|#|파일|길이|프롬프트|
|---|---|---|---|
|AMB-01|AMB_01_Plaza|60초 루프|`ambience loop, busy cartoon town square with small monsters working at coffee stalls, gentle chatter murmur without words, distant clinks, cheerful`|
|AMB-02|AMB_02_Cafe|60초 루프|`ambience loop, small cozy café interior, quiet warm room tone, soft cup clinks, calm`|
|AMB-03|AMB_03_Forest|60초 루프|`ambience loop, foggy haunted forest at night, crickets, soft wind, rustling leaves, mysterious but not scary`|
|AMB-04|AMB_04_ForestCore|60초 루프|`ambience loop, deep center of a haunted forest, almost silent, low distant rumble, faint eerie wind, tension`|
|AMB-05|AMB_05_Lobby|60초 루프|`ambience loop, cozy monster lobby, soft fireplace crackle, faint friendly chatter`|

---

## 2. BGM

**지금 들어 있는 트랙(`BGM/`)은 그대로 두고, 같은 자리에 새로 뽑아 비교할 때 쓴다.** 낮 · 밤은 2분 페이즈라 **60초를 뽑아 Loop로 이어** 쓴다.

### 공통 톤 — BGM 앞에 붙인다
```
Background music for a cute-spooky 3D party game where little monsters (vampire, zombie, werewolf, witch) run a café by day and scavenge a foggy haunted forest by night. Whimsical Halloween-comedy orchestration: pizzicato strings, harpsichord, bassoon, tuba, celesta, theremin accents, light percussion. Playful, catchy, never truly scary. Instrumental, no vocals.
```

|자리|파일|길이|프롬프트|
|---|---|---|---|
|**타이틀**|BGM_Title|60초 루프|`title screen theme, memorable main melody, mysterious opening that turns cheerful, medium tempo, inviting`|
|**메인 메뉴**|BGM_Main|60초 루프|`main menu loop, relaxed and charming, light waltz feel, celesta and pizzicato, unobtrusive`|
|**로비 · 캐릭터 선택**|BGM_Lobby|60초 루프|`lobby waiting loop, upbeat bouncy groove, friendly anticipation before a match, swing feel`|
|**낮 — 카페 영업**|BGM_Day|60초 루프|`daytime café rush, fast upbeat swing jazz with spooky instruments, busy and fun, 130 BPM, keeps energy for two minutes without fatigue, leaves space for sound effects`|
|**밤 — 안개 숲**|BGM_Night|60초 루프|`nighttime sneaking in a foggy forest, mischievous pizzicato and bass clarinet, low tension, comedic stealth, 100 BPM, sparse so footsteps and sound cues stay audible`|

**마지막 30초 버전 (선택)** — 낮 마감 · 밤 경보(S-72 · S-67) 뒤에 바꿔 틀 버전. 같은 곡으로 이어지도록 위 결과를 레퍼런스 오디오로 넣고 뽑는다.
```
same theme as the reference, faster and more urgent, added ticking percussion and rising strings, final 30 seconds of a timed round
```

- **효과음 자리를 비워 둔다** — 낮은 판정 · 판매 · 경고 소리가 계속 울리고, 밤은 소리가 정보다 (리스트 0.1). 프롬프트의 `leaves space` · `sparse`를 빼지 않는다
- 공포 쪽으로 기울면 `never truly scary`를 앞으로 옮기거나 피드백으로 「더 코믹하게」를 준다
