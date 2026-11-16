# 카드 아트 Gemini 프롬프트 가이드

컨셉 기획서 v2의 6장(카드 아트 방향)을 실제 생성용 프롬프트로 옮긴 문서입니다.

**방향**: 3D 렌더풍 슬라임(캐릭터 레이어) + 2D 페인팅 배경(배경 레이어) + 플랫 프레임(Unity에서 제작). 입체감은 엔진의 패럴랙스 셰이더와 카드 두께로 만듭니다.

---

## 0. 작업 순서

1. **스타일 앵커 만들기**: `F-N1 불씨 슬라임`을 먼저 5~10번 뽑아 가장 마음에 드는 1장을 고릅니다. 이 이미지가 50장 전체의 기준(앵커)입니다.
2. **세션 설정**: Gemini 새 대화를 열고 1장의 "세션 설정 프롬프트"를 보낸 뒤, 앵커 이미지를 첨부합니다.
3. **일괄 생산**: 이후에는 카드별 "주제 줄"만 보냅니다. 10장마다 또는 스타일이 흔들리면 앵커 이미지를 다시 첨부합니다.
4. **누끼 따기**: 캐릭터 레이어의 단색 배경을 제거합니다 (포토샵, remove.bg 등). Gemini에게 "배경을 투명하게"라고 해도 투명 PNG는 잘 나오지 않습니다.
5. **이름 규칙에 맞춰 저장**: 아래 6장의 파일 이름 규칙을 따릅니다.

> 대화가 길어지면 스타일이 흐려집니다. 속성 하나(8장)마다 새 대화를 열고 2번부터 다시 하는 것을 권장합니다.

### 이미지 비율

| 레이어 | 비율 | 용도 |
| --- | --- | --- |
| 캐릭터 | 1:1 | 누끼를 딴 뒤 카드 위에 배치 |
| 창 배경 (노말 · 레어 · 수퍼레어) | 4:3 | 카드 상단의 일러스트 창 |
| 풀아트 배경 (유니크 · 레전더리 · 필드) | 2:3 | 카드 전체를 채움 (5:7로 크롭) |
| 카드 뒷면 | 2:3 | 5:7로 크롭 |

---

## 1. 공통 스타일 블록

### 1-1. 세션 설정 프롬프트 (대화 시작 때 1번)

```text
I'm making 50 original trading card illustrations for a VR game about cute slime creatures.
For every image I request in this chat, follow these fixed rules unless I say otherwise.

CHARACTER images:
- 3D rendered style like a glossy collectible vinyl toy, clean and cute
- Semi-translucent jelly body, mostly opaque, with soft subsurface scattering and a slightly brighter inner core
- Soft specular highlights on the top-left of the body
- Big round glossy eyes with two white highlights, small simple mouth, rosy cheeks optional
- Lighting: soft key light from the top-left, soft rim light from the back-right
- A small soft contact shadow directly below the creature
- Centered, full body visible, generous margin around it, square 1:1 image
- Plain solid flat background in the color I specify, no gradient, no floor, no scenery
- No text, no letters, no numbers, no border, no card frame, no watermark

BACKGROUND images:
- Soft painterly 2D digital painting, gentle gradients, low detail
- Slightly blurred, shallow depth of field so a character can stand in front of it
- Lighting from the top-left, matching the character images
- No characters, no creatures, no animals, no text, no border

Keep the rendering style, eye style, and lighting identical to the attached reference image.
Avoid: classic pointed-tip teardrop slime shape, existing game or anime characters, realistic gore, horror.
```

### 1-2. 단독 사용용 블록 (세션 설정 없이 쓸 때 주제 줄 앞에 붙이기)

**캐릭터**
```text
3D rendered glossy vinyl toy style, cute original slime creature, semi-translucent mostly opaque jelly body with subsurface scattering, soft specular highlights top-left, big round glossy eyes with two highlights, soft key light from top-left, soft rim light, small contact shadow below, centered full body with margin, square 1:1, plain solid flat background, no text, no border, no frame.
```

**배경**
```text
Soft painterly 2D digital painting background, gentle gradients, low detail, slightly blurred depth of field, light from top-left, no characters, no creatures, no text, no border.
```

### 1-3. 등급 사다리 (주제 줄에 이미 들어 있음)

| 등급 | 외형 키워드 | 화면 비중 |
| --- | --- | --- |
| 노말 | small, simple round body, minimal features, calm pose | 캐릭터가 이미지의 약 55% |
| 레어 | one distinctive feature (horn, crest, accessory), a few element sparkles | 약 60% |
| 수퍼레어 | dynamic action pose, element effect swirling around the body | 약 70% |
| 유니크 | majestic, ornate armor or ornaments, glowing core, regal pose | 약 75% |
| 레전더리 | colossal mythic form, low camera angle looking up, epic glow | 약 85% (프레임 밖으로 튀어나오게 배치) |

### 1-4. 속성별 팔레트와 누끼용 배경색

| 속성 | 팔레트 | 몸 질감 | 누끼용 배경색 |
| --- | --- | --- | --- |
| 불 | orange, red, warm yellow | glowing ember jelly | plain solid white |
| 물 | aqua, teal, deep blue | clear jelly with tiny bubbles | plain solid white |
| 땅 | amber, olive green, warm brown | earthy jelly with moss and pebbles | plain solid white |
| 공기 | pale sky blue, white, soft lavender | fluffy cloud-like jelly | plain solid dark gray (#333333) |
| 무속성 | pastel pink, cream, soft peach | classic smooth jelly | plain solid white |

> 공기 속성은 몸이 흰색에 가까워서 흰 배경에서는 누끼가 어렵습니다. 그래서 어두운 회색 배경을 씁니다.

---

## 2. 슬라임 카드 40장: 캐릭터 레이어

각 줄을 그대로 복사해 보냅니다. (세션 설정을 안 했다면 1-2의 캐릭터 블록을 앞에 붙입니다.)

### 불 (Fire)

| ID | 등급 | 이름 | 주제 줄 |
| --- | --- | --- | --- |
| F-N1 | 노말 | 불씨 슬라임 | `Character: small round orange slime with a tiny candle-like flame flickering on top of its head, happy smile, calm pose. Normal tier. Background: plain solid white.` |
| F-N2 | 노말 | 숯불 슬라임 | `Character: small flat puddle-shaped dark red slime with glowing coal bits floating inside its body, sleepy half-closed eyes. Normal tier. Background: plain solid white.` |
| F-N3 | 노말 | 꼬마 불꽃 슬라임 | `Character: small bean-shaped warm yellow slime bouncing in mid-air, a little flame wisp trailing behind it like a tail, excited face. Normal tier. Background: plain solid white.` |
| F-R1 | 레어 | 화산뿔 슬라임 | `Character: round red-orange slime with two short shiny black obsidian horns and thin glowing lava cracks on its body, confident face, a few ember sparkles around. Rare tier. Background: plain solid white.` |
| F-R2 | 레어 | 갈기불 슬라임 | `Character: round orange slime with a fluffy mane made of soft flames around its face like a little lion, proud smile, a few ember sparkles around. Rare tier. Background: plain solid white.` |
| F-SR | 수퍼레어 | 화염 소용돌이 슬라임 | `Character: energetic orange-red slime leaping upward in a dynamic pose, a spiral ring of bright fire swirling around its body, determined eyes, embers flying. Super rare tier. Background: plain solid white.` |
| F-U | 유니크 | 용암 군주 슬라임 | `Character: majestic large red slime wearing a crown of molten gold and ornate black obsidian armor plates on its sides, glowing bright lava core visible inside, regal calm expression. Unique tier. Background: plain solid white.` |
| F-L | 레전더리 | 태양 거신 슬라임 | `Character: colossal mythic fire slime shaped like a living sun, a blazing corona of flames radiating around it, molten golden body with a white-hot core, wise powerful eyes, low camera angle looking up, epic scale. Legendary tier. Background: plain solid white.` |

### 물 (Water)

| ID | 등급 | 이름 | 주제 줄 |
| --- | --- | --- | --- |
| W-N1 | 노말 | 물방울 슬라임 | `Character: small round clear aqua slime with tiny bubbles inside, gentle smile, calm pose. Normal tier. Background: plain solid white.` |
| W-N2 | 노말 | 거품 슬라임 | `Character: small round light blue slime blowing a soap bubble from its mouth, many tiny bubbles inside its body, playful face. Normal tier. Background: plain solid white.` |
| W-N3 | 노말 | 이슬 슬라임 | `Character: tiny round teal slime sitting on a big green leaf, a single dewdrop balanced on its head, shy face. Normal tier. Background: plain solid white.` |
| W-R1 | 레어 | 산호뿔 슬라임 | `Character: round deep aqua slime with small pink coral antlers growing on its head, cheerful face, a few water sparkles around. Rare tier. Background: plain solid white.` |
| W-R2 | 레어 | 물보라 슬라임 | `Character: round blue slime with a fin-shaped crest on its head and little splashing water droplets around it, lively face. Rare tier. Background: plain solid white.` |
| W-SR | 수퍼레어 | 파도타기 슬라임 | `Character: energetic teal slime riding the crest of a curling wave in a dynamic pose, water spray swirling around its body, joyful eyes. Super rare tier. Background: plain solid white.` |
| W-U | 유니크 | 심해 여왕 슬라임 | `Character: majestic large deep blue slime wearing a crown of pearls and ornate seashell armor, soft bioluminescent dots glowing across its body, glowing cyan core, graceful regal expression. Unique tier. Background: plain solid white.` |
| W-L | 레전더리 | 대해신 슬라임 | `Character: colossal mythic water slime rising like a great sea leviathan, long flowing jelly fins, a giant whirlpool spiraling around its base, glowing deep blue body with a bright cyan core, ancient calm eyes, low camera angle looking up, epic scale. Legendary tier. Background: plain solid white.` |

### 땅 (Earth)

| ID | 등급 | 이름 | 주제 줄 |
| --- | --- | --- | --- |
| E-N1 | 노말 | 진흙 슬라임 | `Character: small round warm brown slime with a soft matte mud texture and a few drips, sleepy content face. Normal tier. Background: plain solid white.` |
| E-N2 | 노말 | 이끼 슬라임 | `Character: small round olive green slime with a soft patch of moss and a tiny sprout on top of its head, gentle smile. Normal tier. Background: plain solid white.` |
| E-N3 | 노말 | 조약돌 슬라임 | `Character: small round amber slime with several smooth colorful pebbles visible inside its body, curious face. Normal tier. Background: plain solid white.` |
| E-R1 | 레어 | 버섯갓 슬라임 | `Character: round amber slime wearing a big red-and-cream spotted mushroom cap like a hat, happy face, a few spore sparkles around. Rare tier. Background: plain solid white.` |
| E-R2 | 레어 | 수정 슬라임 | `Character: round olive-brown slime with small shiny amber crystals poking out of its back, proud face, a few glints around. Rare tier. Background: plain solid white.` |
| E-SR | 수퍼레어 | 대지 울림 슬라임 | `Character: energetic brown slime stomping down in a dynamic pose, chunks of rock and dust rising and swirling around it, determined eyes. Super rare tier. Background: plain solid white.` |
| E-U | 유니크 | 고목 수호자 슬라임 | `Character: majestic large green-brown slime wearing ornate armor of ancient tree bark and a crown of small branches with leaves, glowing amber core, wise regal expression. Unique tier. Background: plain solid white.` |
| E-L | 레전더리 | 산맥 거신 슬라임 | `Character: colossal mythic earth slime carrying a whole small mountain range with forests and a waterfall on its back, glowing amber core deep inside, ancient gentle eyes, low camera angle looking up, epic scale. Legendary tier. Background: plain solid white.` |

### 공기 (Air)

| ID | 등급 | 이름 | 주제 줄 |
| --- | --- | --- | --- |
| A-N1 | 노말 | 솜구름 슬라임 | `Character: small round fluffy white slime that looks like a soft cloud, pale blue tint, dreamy smile. Normal tier. Background: plain solid dark gray #333333.` |
| A-N2 | 노말 | 산들바람 슬라임 | `Character: small round pale sky blue slime with two little wind swirl wisps curling around it, relaxed face. Normal tier. Background: plain solid dark gray #333333.` |
| A-N3 | 노말 | 풍선 슬라임 | `Character: small puffed-up round lavender slime floating in the air like a balloon, cheeks puffed, surprised face. Normal tier. Background: plain solid dark gray #333333.` |
| A-R1 | 레어 | 깃털 슬라임 | `Character: round pale blue slime with two small white feather wings on its sides, hovering, cheerful face, a few sparkles around. Rare tier. Background: plain solid dark gray #333333.` |
| A-R2 | 레어 | 회오리꼬리 슬라임 | `Character: round lavender slime with a small spinning tornado tail beneath it, spinning playfully, a few sparkles around. Rare tier. Background: plain solid dark gray #333333.` |
| A-SR | 수퍼레어 | 폭풍 질주 슬라임 | `Character: energetic sky blue slime dashing through the air in a dynamic pose, strong gale streaks and swirling wind ribbons around its body, focused eyes. Super rare tier. Background: plain solid dark gray #333333.` |
| A-U | 유니크 | 천공 기사 슬라임 | `Character: majestic large pale blue slime wearing ornate silver wind armor and a crown of white feathers, large feathered wings, glowing soft white core, noble regal expression. Unique tier. Background: plain solid dark gray #333333.` |
| A-L | 레전더리 | 하늘고래 슬라임 | `Character: colossal mythic air slime shaped like a gentle sky whale made of cloud jelly, long flowing tail fins, faint aurora ribbons trailing from its body, glowing lavender core, serene ancient eyes, low camera angle looking up, epic scale. Legendary tier. Background: plain solid dark gray #333333.` |

### 무속성 (Neutral)

| ID | 등급 | 이름 | 주제 줄 |
| --- | --- | --- | --- |
| N-N1 | 노말 | 말랑 슬라임 | `Character: small round classic pastel pink slime, smooth glossy body, warm happy smile, calm pose. Normal tier. Background: plain solid white.` |
| N-N2 | 노말 | 졸린 슬라임 | `Character: small slightly squished cream-colored slime dozing off, closed eyes, tiny bubble from its nose. Normal tier. Background: plain solid white.` |
| N-N3 | 노말 | 아기 슬라임 삼형제 | `Character: three tiny round pastel slimes (pink, peach, cream) huddled together in a row, cute surprised faces. Normal tier. Background: plain solid white.` |
| N-R1 | 레어 | 리본 슬라임 | `Character: round pastel pink slime with a big glossy ribbon bow on top of its head, sweet smile, a few sparkles around. Rare tier. Background: plain solid white.` |
| N-R2 | 레어 | 무지갯빛 슬라임 | `Character: round cream slime with a soft iridescent rainbow sheen across its glossy body, delighted face, a few sparkles around. Rare tier. Background: plain solid white.` |
| N-SR | 수퍼레어 | 분열 슬라임 | `Character: energetic peach slime in the middle of splitting into two smaller copies of itself in a dynamic pose, stretchy jelly strands between them, playful mischievous faces. Super rare tier. Background: plain solid white.` |
| N-U | 유니크 | 프리즘 현자 슬라임 | `Character: majestic large pastel slime with a glowing prism crystal core inside, two floating ornate rune rings orbiting around it, small wise glasses, calm regal expression. Unique tier. Background: plain solid white.` |
| N-L | 레전더리 | 원초의 슬라임 | `Character: colossal mythic primordial slime whose body contains a swirling galaxy with tiny stars, a soft halo of light behind it, pastel cosmic colors, gentle all-knowing eyes, low camera angle looking up, epic scale. Legendary tier. Background: plain solid white.` |

---

## 3. 슬라임 카드 배경 레이어

노말·레어는 속성별 공용 배경 1장을 씁니다. 수퍼레어 이상은 카드마다 따로 만듭니다. (총 25장)

### 3-1. 공용 창 배경 (노말 · 레어, 4:3)

| ID | 속성 | 주제 줄 |
| --- | --- | --- |
| BG-F-W | 불 | `Background: a warm volcanic meadow with smooth reddish rocks, gentle distant volcano, warm orange and red sky, soft glowing embers in the air. Landscape 4:3.` |
| BG-W-W | 물 | `Background: a calm tide pool beach with clear turquoise water, smooth stones and shells, bright aqua sky. Landscape 4:3.` |
| BG-E-W | 땅 | `Background: a mossy forest floor with soft mushrooms, ferns and warm sunbeams through trees, amber and olive tones. Landscape 4:3.` |
| BG-A-W | 공기 | `Background: a soft pastel sky above fluffy clouds, gentle wind swirls, pale blue and lavender tones. Landscape 4:3.` |
| BG-N-W | 무속성 | `Background: a cozy sunny grass meadow with small flowers, soft pastel pink and cream sky. Landscape 4:3.` |

### 3-2. 수퍼레어 창 배경 (4:3)

| ID | 주제 줄 |
| --- | --- |
| F-SR-BG | `Background: inside a glowing volcano crater with rivers of lava and rising heat, dramatic orange and red light. Landscape 4:3.` |
| W-SR-BG | `Background: open ocean with big rolling waves under a bright sky, sparkling spray, aqua and deep blue tones. Landscape 4:3.` |
| E-SR-BG | `Background: a rocky canyon with cracked ground and floating dust in warm sunlight, amber and brown tones. Landscape 4:3.` |
| A-SR-BG | `Background: high in a windy sky among speeding clouds with streaks of wind, pale blue and white tones. Landscape 4:3.` |
| N-SR-BG | `Background: a whimsical pastel playground of soft jelly hills and bouncy shapes, pink and cream tones. Landscape 4:3.` |

### 3-3. 유니크 · 레전더리 풀아트 배경 (2:3)

풀아트는 카드 전체를 채우므로 **아래 1/3은 비교적 단순하게** 둡니다. 그 위에 이름과 정보 텍스트가 올라갑니다.

| ID | 주제 줄 |
| --- | --- |
| F-U-BG | `Background: a grand throne hall carved from black obsidian with lava waterfalls glowing behind, warm dramatic light, simpler darker lower third. Portrait 2:3.` |
| F-L-BG | `Background: an epic sky above an erupting volcano with a blazing sunset and glowing clouds, radiant golden light from the center, simpler lower third. Portrait 2:3.` |
| W-U-BG | `Background: a serene deep sea palace with coral pillars, glowing jellyfish and light rays from above, deep blue tones, simpler lower third. Portrait 2:3.` |
| W-L-BG | `Background: a vast stormy ocean with a gigantic whirlpool and a beam of light breaking through the clouds, epic scale, simpler lower third. Portrait 2:3.` |
| E-U-BG | `Background: an ancient sacred forest with a giant old tree, floating glowing pollen and soft god rays, simpler lower third. Portrait 2:3.` |
| E-L-BG | `Background: an epic wide valley with towering mountains, forests and waterfalls under a golden sky, epic scale, simpler lower third. Portrait 2:3.` |
| A-U-BG | `Background: a floating sky castle among white clouds with silver banners in the wind, bright soft light, simpler lower third. Portrait 2:3.` |
| A-L-BG | `Background: a vast night-to-dawn sky filled with aurora ribbons above a sea of clouds, epic scale, simpler lower third. Portrait 2:3.` |
| N-U-BG | `Background: a quiet mystical library tower with floating books and soft prism light beams, pastel tones, simpler lower third. Portrait 2:3.` |
| N-L-BG | `Background: deep space with a colorful pastel nebula, swirling galaxies and soft starlight, epic scale, simpler lower third. Portrait 2:3.` |

---

## 4. 환경 필드 카드 5장

필드 카드는 슬라임이 없는 풍경 카드입니다. 수퍼레어 이상은 **먼 배경 + 가까운 전경 소품**을 따로 뽑아 패럴랙스에 씁니다.

| ID | 등급 | 이름 | 레이어 | 주제 줄 |
| --- | --- | --- | --- | --- |
| FD-N | 노말 | 슬라임 들판 | 1장 | `Background: a peaceful green meadow with soft rolling hills, tiny colorful jelly droplets glistening on the grass, morning light. Portrait 2:3.` |
| FD-R | 레어 | 이슬 숲 | 1장 | `Background: a misty forest path with sparkling dewdrops on every leaf, soft green and teal tones, gentle light rays. Portrait 2:3.` |
| FD-SR | 수퍼레어 | 반딧불 버섯 동굴 | 먼 배경 | `Background: a dark cozy cave with glowing blue and purple mushrooms deep inside, soft magical glow. Portrait 2:3.` |
| | | | 전경 | `3D rendered glossy style: a cluster of large glowing blue and purple mushrooms in the foreground, plain solid white background, no text. Square 1:1.` |
| FD-U | 유니크 | 떠다니는 구름섬 | 먼 배경 | `Background: a wide pastel sky with distant floating islands and waterfalls pouring into the clouds. Portrait 2:3.` |
| | | | 전경 | `3D rendered glossy style: a small grassy floating island with a tiny waterfall and a little tree, plain solid white background, no text. Square 1:1.` |
| FD-L | 레전더리 | 슬라임 성지 | 먼 배경 | `Background: an ancient sacred sanctuary with tall stone arches, a huge glowing rainbow jelly crystal in the center, divine light rays, epic scale. Portrait 2:3.` |
| | | | 전경 | `3D rendered glossy style: broken ancient stone pillars with soft moss and small glowing jelly crystals, plain solid white background, no text. Square 1:1.` |

---

## 5. 간식(아이템) 카드 5장

간식은 캐릭터처럼 3D 렌더풍으로 뽑고, 배경은 무속성 공용 배경(`BG-N-W`)을 같이 씁니다.

| ID | 등급 | 이름 | 주제 줄 |
| --- | --- | --- | --- |
| SN-N | 노말 | 젤리빈 한 줌 | `Item: a small pile of glossy colorful jelly beans, 3D rendered glossy toy style, soft light from top-left, small contact shadow, centered. Normal tier. Background: plain solid white.` |
| SN-R | 레어 | 꿀방울 쿠키 | `Item: a round golden cookie with shiny honey drops on top, 3D rendered glossy toy style, soft light from top-left, small contact shadow, centered, a few sparkles. Rare tier. Background: plain solid white.` |
| SN-SR | 수퍼레어 | 무지개 푸딩 | `Item: a wobbly layered rainbow jelly pudding on a small plate with a cherry on top, 3D rendered glossy toy style, translucent layers, soft light from top-left, sparkles around. Super rare tier. Background: plain solid white.` |
| SN-U | 유니크 | 별가루 마카롱 탑 | `Item: an elegant tower of pastel macarons dusted with glittering star powder on an ornate golden stand, 3D rendered glossy toy style, soft glow, regal presentation. Unique tier. Background: plain solid white.` |
| SN-L | 레전더리 | 별의 꿀단지 | `Item: a legendary ornate golden honey pot overflowing with glowing cosmic honey that contains tiny stars, soft halo of light, 3D rendered glossy toy style, low camera angle, epic presentation. Legendary tier. Background: plain solid white.` |

---

## 6. 카드 뒷면 1장

기획서 6장: "중앙 심볼 + 대칭 패턴" 구조만 참고하고, 중앙에는 슬라임 얼굴을 둡니다.

```text
Flat 2D vector graphic design for the back of a trading card. Perfectly symmetrical layout.
In the center, a simple cute slime face emblem (round jelly shape with two big glossy eyes and a small smile) inside a circular badge.
Around it, a symmetrical pattern of soft radiating rays and small droplet shapes.
Color scheme: deep violet and teal gradient with a warm gold outline accent.
Clean crisp shapes, no text, no letters, no logo of any existing brand, no border. Portrait 2:3.
```

> 카드 뒷면은 패턴이 정확하게 대칭이어야 해서, Gemini 결과를 바탕으로 직접 다듬는 것이 더 빠를 수 있습니다.

---

## 7. 파일 이름 규칙

```text
Assets/<아트 폴더>/Cards/
  Char/   F_N1_char.png, F_L_char.png, SN_R_char.png, FD_SR_fg.png ...
  BG/     BG_F_W.png, F_SR_bg.png, F_U_bg.png, FD_L_bg.png ...
  Back/   card_back.png
```

- ID의 `-`를 `_`로 바꿔 파일 이름으로 씁니다. 이 ID는 카드 데이터 테이블(기획서 5-7)의 카드 ID와 똑같이 맞춥니다.
- 저장 해상도는 1024px 기준으로 하고, Quest 빌드에서 ASTC 압축을 씁니다.

---

## 8. 품질 체크리스트 (매 장마다)

- [ ] 조명이 왼쪽 위에서 오는가? (하이라이트 위치 확인)
- [ ] 눈의 스타일(하이라이트 2개)이 앵커 이미지와 같은가?
- [ ] 몸이 너무 투명해서 배경이 비쳐 보이지 않는가? (누끼가 깨짐)
- [ ] 이미지 안에 글자, 테두리, 카드 프레임이 들어가 있지 않은가?
- [ ] 캐릭터가 잘리지 않고 여백이 있는가? (레전더리는 프레임 밖으로 튀어나오게 배치하므로 여백이 특히 중요)
- [ ] 기존 IP와 닮지 않았는가? 특히 **끝이 뾰족한 물방울형 슬라임**, 파란 물방울형 주인공 슬라임, 왕관 쓴 거대 슬라임 같은 유명 디자인은 피합니다.
- [ ] 같은 속성 8장을 나란히 놓았을 때 등급 차이가 그림만 봐도 보이는가?

## 9. 생성 수량 요약

| 구분 | 장수 |
| --- | --- |
| 슬라임 캐릭터 | 40 |
| 슬라임 배경 (공용 5 + 수퍼레어 5 + 풀아트 10) | 20 |
| 필드 카드 (배경 5 + 전경 3) | 8 |
| 간식 캐릭터 | 5 |
| 카드 뒷면 | 1 |
| **합계** | **74** (재생성 제외) |
