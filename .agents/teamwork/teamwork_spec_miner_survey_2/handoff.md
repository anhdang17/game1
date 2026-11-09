# Comprehensive Game Design & Technical Specification: "Du Ký Bé Mây" (Vertical Slice)

## Executive Summary
This document provides the authoritative, exhaustive specification mining report for the 2D Vertical Slice of **"Du Ký Bé Mây"** (Bé Mây's Journey). It details the lore, visual art direction, core gameplay loops, data models, state machines, quest mechanics, puzzle logic, UI/UX flows, and headless automated testability requirements.

---

# 1. Lore, Art Style, and Protagonists

### 1.1 Narrative Premise & World Lore
- **The World of Vietnam**: A vibrant, culturally rich tapestry of traditions, architecture, culinary arts, and natural beauty.
- **The Calamity of the Gray Shadow (Bóng Xám)**: An enigmatic entity known as *Bóng Xám* has swept through the land, draining the warmth, colors, and memories from the landscape. Cities and villages are left in a desaturated, melancholic, monochromatic state, where residents lose their passion and forget their ancestral crafts.
- **The Enchanted Memory Journal (Cuốn Sổ Ký Ức)**: A magical heirloom sketchbook that holds the power to restore the stolen colors of the world. Each time Bé Mây helps the local people, solves cultural challenges, and revives communal traditions, a lost page of the journal is rekindled, bursting vibrant colors back into the region.
- **Narrative Arc of Level 1 (Hà Nội)**:
  - Mây and Bút Chì arrive in the Old Quarter of Hanoi, greeted by a silent, gray streetscape.
  - Through rekindling the communal warmth of a traditional Phở stall and unblocking the historic path to the Long Biên Bridge, Mây awakens Hanoi's memory.
  - Upon completion, the Gray Shadow leaves a mysterious letter hinting that the ancient royal citadel of Huế is suffering an even deeper oblivion.

### 1.2 Protagonists & Characters
1. **Bé Mây (Protagonist)**:
   - *Role*: Player-controlled explorer.
   - *Visual Design*: A cheerful, kind-hearted Vietnamese child traveler (~8-10 years old). Wears a bright yellow nón lá (or explorer beret), colorful scarf, turquoise/white tunic/shirt, brown shorts, and a sturdy leather satchel holding the Memory Journal.
   - *Personality*: Curious, empathetic, determined, respectful of elders and cultural heritage.
   - *Gameplay Ability*: Walk/run, inspect objects, speak with villagers, pick up/deliver items, push street stalls.
2. **Bút Chì (Companion & Guide)**:
   - *Role*: Magical floating pencil companion and journal keeper.
   - *Visual Design*: An anthropomorphic, chubby wooden colored pencil with expressive eyes, wooden shavings collar, and a glowing graphite tip that radiates color sparks.
   - *Personality*: Witty, observant, scholarly, encouraging. Offers playful remarks and cultural trivia.
   - *Gameplay Functionality*: Serves as the player's UI guide, in-game hint dispenser, and animator of journal pages when colors are restored.
3. **Bóng Xám (The Gray Shadow - Antagonist / Mystery)**:
   - *Role*: Elusive force of desaturation and forgotten memories.
   - *Visual Presence in Vertical Slice*: Appears as a swirling mist / shadowy silhouette during the opening and ending cutscenes, leaving behind wax-sealed narrative letters.
4. **Local Hanoi NPCs**:
   - *Bà An (Bà Cụ Bán Phở)*: Traditional phở artisan in Hàng Buồm / Đồng Xuân, initially disheartened because her ingredients and broth recipe are scattered.
   - *Bác Nam (Bác Xích Lô)*: Elderly cyclo driver with conical hat, waiting near the crossroads.
   - *Cô Lan (Quầy Gia Vị Hàng Buồm / Lãn Ông)*: Traditional herbalist and spice vendor.
   - *Bác Tư (Quầy Thực Phẩm & Nước Dùng)*: Butcher and broth master.

### 1.3 Art Style & Visual Tone
- **Art Direction**: 2D Flat Cartoon Illustration with hand-crafted storybook warmth.
- **Visual Aesthetic**:
  - Soft, rounded shapes (no harsh sharp geometry) giving a friendly, accessible feel.
  - Vibrant, rich regional color palettes: Ochre yellow walls (vàng phố cổ), deep moss green shutters (xanh rêu), terracotta roof tiles (đỏ ngói), lotus pink (hồng cánh sen), jade green broth garnish (hành hoa), and rusted iron orange (sắt cầu Long Biên).
- **The Color Restoration Dynamic**:
  - *Unexplored / Unresolved State*: Desaturated, muted, sepia-gray tones (monochromatic shader or desaturated sprites).
  - *Restoration Transition*: When quests/puzzles are solved, an animated color bloom / ripple wave radiates outward from the puzzle focal point, snapping the environment into glorious full color with sparkling particle confetti and cheerful audio chimes.
- **Iconic Hanoi Cultural Elements**:
  - *Phố Cổ (Hanoi 36 Guild Streets)*: Colonial facades, hanging lanterns, woven bamboo blinds, street signs ("Phở Gia Truyền", "Thuốc Bắc Lãn Ông").
  - *Cầu Long Biên*: Cantilever iron truss bridge crossing the Red River, bathed in sunset glow.
  - *Vehicles*: Vintage bicycles with flower baskets loaded with lilies and lotuses, traditional pedal-powered xích lô (cyclo).
  - *Flora*: Tây Hồ lotus flowers (Hoa sen hồng) in ceramic water bowls.
  - *Street Gastronomy*: Steaming phở pot with curling aromatic vapors, wooden low stools and blue plastic tables.

---

# 2. World Map & Progression System (R1)

### 2.1 2D World Map Architecture
- **Visual Presentation**:
  - An illustrated parchment map of Vietnam depicting regional nodes along the S-curve.
  - Nodes:
    1. **Hà Nội (Node 1)**: Available / Unlocked at start of slice.
    2. **Huế (Node 2)**: Locked with padlock badge; unlocks upon Hanoi completion.
    3. **Hội An (Node 3)**: Locked silhouette (future expansion).
    4. **TP. Hồ Chí Minh & Miền Tây (Node 4)**: Locked silhouette (future expansion).
- **Node State Machine**:
  ```
  [ Locked ] ──────────> [ Unlocked (Desaturated) ] ──────────> [ Completed (100% Restored) ]
       │                                                                  │
  (Hidden/Padlock)             (0% - 99% Color Restored)             (Golden Lotus & Full Color)
  ```
  - `Locked`: Grayscale silhouette, padlock icon, displays tooltip: *"Vùng đất còn bị bao phủ trong bóng xám"*.
  - `Unlocked_Uncolored`: Selectable; shows region name, current restoration percentage (e.g., 0% or 33%), and button *"Khám phá ngay"*.
  - `Completed_Restored`: Full vibrant color thumbnail, golden lotus badge, 100% restoration metric, allows replay/free exploration.
- **World Map Navigation**:
  - Keyboard (A/D or Left/Right arrow keys) to cycle between nodes.
  - Enter / Spacebar or Mouse Click to inspect and enter selected node.
  - Shortcut key `[J]` to open Collection Album, `[Esc]` for Pause/Settings.

### 2.2 Persistent Save/Load System
- **File Location**: `savegame.json` in local persistent app data folder (`%APPDATA%/DuKyBeMay/` or `./Data/savegame.json`).
- **Persistence Guarantees**:
  - Atomic write pattern (write to `savegame.json.tmp`, flush, atomic replace).
  - Schema versioning (`saveVersion: "1.0.0"`) with backward-compatibility fallbacks.
  - Corrupt file detection: If JSON deserialization fails, isolate corrupt file to `savegame.json.bak` and instantiate fresh default state without crashing.
- **Complete Save/Load JSON Schema**:
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DuKyBeMay_SaveData",
  "type": "object",
  "properties": {
    "saveVersion": { "type": "string", "default": "1.0.0" },
    "lastSavedTimestamp": { "type": "string", "format": "date-time" },
    "playerProfile": {
      "type": "object",
      "properties": {
        "playerName": { "type": "string", "default": "Bé Mây" },
        "currentRegionId": { "type": "string", "default": "Hanoi" },
        "totalPlayTimeSeconds": { "type": "number", "default": 0 }
      },
      "required": ["playerName", "currentRegionId"]
    },
    "worldProgression": {
      "type": "object",
      "properties": {
        "unlockedRegions": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["Hanoi"]
        },
        "completedRegions": {
          "type": "array",
          "items": { "type": "string" },
          "default": []
        },
        "regionColorRestoration": {
          "type": "object",
          "additionalProperties": { "type": "number", "minimum": 0, "maximum": 100 }
        }
      },
      "required": ["unlockedRegions", "completedRegions", "regionColorRestoration"]
    },
    "level1State": {
      "type": "object",
      "properties": {
        "playerSpawnX": { "type": "number", "default": 120.0 },
        "playerSpawnY": { "type": "number", "default": 250.0 },
        "activeQuestId": { "type": "string", "default": "Quest_Hanoi_01" },
        "questFlags": {
          "type": "object",
          "properties": {
            "Q1_Started": { "type": "boolean", "default": false },
            "Q1_BanhPho_Collected": { "type": "boolean", "default": false },
            "Q1_ThitBo_Collected": { "type": "boolean", "default": false },
            "Q1_NuocDung_Collected": { "type": "boolean", "default": false },
            "Q1_HanhLa_Collected": { "type": "boolean", "default": false },
            "Q1_ThaoMoc_Collected": { "type": "boolean", "default": false },
            "Q1_Completed": { "type": "boolean", "default": false },
            "Q2_Started": { "type": "boolean", "default": false },
            "Q2_PhoAssembled": { "type": "boolean", "default": false },
            "Q2_Completed": { "type": "boolean", "default": false },
            "Q3_Started": { "type": "boolean", "default": false },
            "Q3_GridSolved": { "type": "boolean", "default": false },
            "Q3_Completed": { "type": "boolean", "default": false },
            "Gate_LongBien_Unlocked": { "type": "boolean", "default": false },
            "GrayShadow_LetterRead": { "type": "boolean", "default": false },
            "Level1_Finished": { "type": "boolean", "default": false }
          },
          "required": ["Q1_Completed", "Q2_Completed", "Q3_Completed", "Gate_LongBien_Unlocked"]
        },
        "inventoryItems": {
          "type": "array",
          "items": { "type": "string" },
          "default": []
        },
        "puzzle3ObjectPositions": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "objectId": { "type": "string" },
              "gridX": { "type": "integer" },
              "gridY": { "type": "integer" }
            },
            "required": ["objectId", "gridX", "gridY"]
          }
        }
      },
      "required": ["activeQuestId", "questFlags", "inventoryItems"]
    },
    "collectionAlbum": {
      "type": "object",
      "properties": {
        "collectedJournalPages": {
          "type": "array",
          "items": { "type": "string" },
          "default": []
        },
        "collectedStamps": {
          "type": "array",
          "items": { "type": "string" },
          "default": []
        },
        "unlockedFactCards": {
          "type": "array",
          "items": { "type": "string" },
          "default": []
        }
      },
      "required": ["collectedJournalPages", "collectedStamps", "unlockedFactCards"]
    },
    "audioSettings": {
      "type": "object",
      "properties": {
        "masterVolume": { "type": "number", "minimum": 0.0, "maximum": 1.0, "default": 0.8 },
        "sfxVolume": { "type": "number", "minimum": 0.0, "maximum": 1.0, "default": 0.9 },
        "musicVolume": { "type": "number", "minimum": 0.0, "maximum": 1.0, "default": 0.7 }
      }
    }
  },
  "required": ["saveVersion", "playerProfile", "worldProgression", "level1State", "collectionAlbum"]
}
```

---

# 3. Level 1 Exploration & 3 Hanoi Quests (R2)

### 3.1 Exploration Mechanics & World Layout
- **Camera & Viewport**: Top-down 2D orthographic adventure perspective (aspect ratio 16:9, reference resolution 1280x720 or 1920x1080). Smooth camera follow with soft bounding clamps.
- **Character Controller (Bé Mây)**:
  - Input: WASD or Arrow Keys.
  - Physics/Movement: Smooth velocity interpolation with acceleration (speed: 180-220 px/sec).
  - Depth Rendering: Dynamic Y-sorting so Bé Mây correctly renders in front of or behind buildings, lampposts, stalls, and NPCs based on foot coordinate (`Y`).
  - Interaction Trigger: Proximity-based interaction bubble (`radius: 40-50 px`). When near an interactive entity, an animated floating prompt appears: `[E] Tương tác`.
- **Hanoi Map Layout Sections**:
  - *Zone A (Phố Phở Hàng Buồm / Đinh Liệt)*: Traditional stalls, dining tables, Bà Cụ Bán Phở stall, spice merchant.
  - *Zone B (Ngõ Phố Cổ & Chợ Hàng Rau)*: Tight alleys with flower bicycles, vegetable crates, fresh noodles stall, meat stall.
  - *Zone C (Cửa Ngõ Cầu Long Biên - The Bottleneck)*: Crowded passage obstructed by stalls and cyclo carts (Puzzle 3).
  - *Zone D (Chân Cầu Long Biên & Khải Hoàn Môn)*: The grand gateway leading onto the Long Biên iron bridge spans.

### 3.2 Quest 1: Thu thập nguyên liệu nấu Phở (Ingredient Gathering)
- **Quest Trigger**: Talking to Bà Cụ Bán Phở (Bà An) at her desaturated stall in Zone A.
- **Story Context**: Bà An wants to brew her ancestral phở to bring smiles and color back, but her signature ingredients are scattered among fellow merchants across the market stalls.
- **The 5 Required Ingredients**:
  1. `Item_BanhPho`: **Bánh Phở tươi** (Fresh hand-cut rice noodles) — Found at *Quầy Bánh Phở & Mì Sợi*.
  2. `Item_ThitBo`: **Thịt Bò tươi thái mỏng** (Fresh tender beef slices) — Found at *Quầy Bác Tư Hàng Thịt*.
  3. `Item_NuocDung`: **Nước dùng hầm xương gia truyền** (Simmering aromatic bone broth) — Found at *Bếp Nấu Nước Dùng Nóng*.
  4. `Item_HanhLa`: **Hành lá & Rau mùi tươi** (Fresh scallions, culantro & herbs) — Found at *Gánh Rau Cô Lan*.
  5. `Item_ThaoMoc`: **Túi Thảo mộc hồi quế** (Star anise, cinnamon stick, cardamom) — Found at *Hiệu Thuốc Nam & Gia Vị Lãn Ông*.
- **Collection Mechanics**:
  - Player approaches each designated stall/NPC.
  - Press `[E]` to interact.
  - Brief culturally authentic dialogue (e.g., herbalist explaining how star anise gives Hanoi phở its distinctive aroma).
  - Item is acquired: Sound chime plays, item icon pops up above Mây's head, item is added to `inventoryItems`.
  - HUD Quest Tracker updates: `Thu thập nguyên liệu phở: (n/5)`.
- **Completion Check**:
  - Once `n == 5`, quest status transitions to `RequirementsMet`.
  - Prompt updates: *"Đã đủ nguyên liệu! Hãy quay lại gặp Bà Cụ Bán Phở!"*.
  - Talking to Bà An completes Quest 1, unlocks Quest 2, and initiates the cooking station mini-game.

### 3.3 Quest 2: Ghép đúng Bát Phở hoàn chỉnh (Culinary Assembly Puzzle)
- **UI Mini-Game Layout**:
  - Full-screen or modal cooking interface.
  - Center: Large traditional Vietnamese porcelain bowl with blue fish/flower motif (Bát phở rỗng).
  - Left/Bottom: 5 Ingredient Cards/Tokens with clear visual icons and Vietnamese labels:
    - `[Bánh Phở]`, `[Thịt Bò]`, `[Hành Rau]`, `[Nước Dùng]`, `[Gia Vị Chanh Tiêu]`.
  - Right: 5 ordered preparation slots (`Slot 1` to `Slot 5`).
- **Authentic Culinary Sequence (The Correct Solution)**:
  1. **Slot 1 (Đáy bát)**: `Bánh Phở tươi chần kỹ` (Base layer of blanched rice noodles).
  2. **Slot 2 (Lớp thịt)**: `Thịt Bò thái mỏng xếp đều` (Thinly sliced beef arranged over noodles).
  3. **Slot 3 (Hương vị xanh)**: `Hành hoa chẻ & Rau mùi thái nhỏ` (Scallions & fresh fragrant herbs on top).
  4. **Slot 4 (Chan nước)**: `Nước dùng hầm xương sôi sùng sục` (Boiling hot bone broth ladled over to cook meat and melt aromas).
  5. **Slot 5 (Gia vị hoàn thiện)**: `Chanh ớt & Hạt tiêu Bắc` (Fresh lime, chili slices & pepper garnish).
- **Interaction & Drag-and-Drop / Slot Selector**:
  - Mouse: Click and drag ingredient card to slot, or click ingredient then click target slot.
  - Keyboard: Arrow keys navigate between items and slots, `Space`/`Enter` to place/remove.
  - `[Đặt lại / Hoàn tác]` (Reset/Undo button) available at all times.
- **Validation & Feedback**:
  - Player clicks `[Hoàn Tất Nấu Phở]` (or auto-validates on 5th slot filled).
  - *If Incorrect Order*:
    - Gentle error chime.
    - Bút Chì provides informative feedback:
      - E.g., if broth was poured before noodles: *"Nước dùng mà chan trước khi cho bánh phở thì bánh sẽ bị nát và nguội mất, Mây ơi! Hãy đặt bánh phở xuống đáy bát trước nhé!"*
      - E.g., if herbs placed after broth without meat: *"Thịt bò cần được xếp lên bánh phở trước khi chan nước dùng sôi để thịt chín mềm thơm ngon!"*
    - The invalid items return to the tray; no game over or penalty.
  - *If Correct Order*:
    - **Victory Fanfare & Visual Spectacle**: A radiant bowl of steaming Phở appears with golden vapor curls.
    - **Color Bloom #1**: Vibrant color wave ripples across Zone A! The phở stall, wooden tables, and storefronts burst from gray into rich yellow and moss green (+33% regional color restored).
    - Bà An smiles warmly: *"Tuyệt vời quá! Hương thơm này đã đánh thức cả con phố! Nhưng Mây ơi, đường ra Cầu Long Biên đang bị tắc nghẽn, các sạp hàng hoa và xe xích lô bị xô lệch chắn hết lối đi rồi!"*
    - Quest 2 marked `Completed`; Quest 3 unlocked.

### 3.4 Quest 3: Sắp xếp gian hàng mở lối ra Cầu Long Biên (Spatial Grid Puzzle)
- **Puzzle Design & Context**:
  - The entrance archway leading out of the market to Long Biên Bridge (Zone C) is completely blocked by displaced street vendor stalls, fruit crates, flower carts, and a vintage cyclo.
  - Genre: 2D Top-Down Spatial Sliding / Sokoban-style path clearance puzzle.
- **Grid Specifications**:
  - Grid Size: 7 columns (X: 0 to 6) by 5 rows (Y: 0 to 4).
  - Entrance: Tile (0, 2) (West side - player enters).
  - Exit Gate: Tile (6, 2) (East side - leading onto Long Biên Bridge).
  - Fixed Boundary Walls: Street curbstones and colonial yellow walls enclose the grid.
- **Movable & Fixed Entities**:
  1. `Obj_FlowerCart` (Xe Hoa Hà Nội): Size 1x2 (occupies 1 column, 2 vertical tiles). Can be pushed vertically.
  2. `Obj_Cyclo` (Xe Xích Lô): Size 2x1 (occupies 2 horizontal tiles, 1 row). Can be pushed horizontally.
  3. `Obj_FruitStall` (Sạp Hoa Quả): Size 1x1. Can be pushed in all 4 cardinal directions.
  4. `Obj_TeaStall` (Quầy Nước Chè Vỉa Hè): Size 1x1. Can be pushed in all 4 cardinal directions.
  5. `Obj_CeramicCrates` (Thùng Gốm Bát Tràng): Size 1x1. Movable obstacle.
- **Pushing Mechanics & Physics**:
  - Player walks up to a movable stall and presses `[E] + Direction` (or walks firmly against it for > 0.15s).
  - The object slides into the adjacent grid cell if and only if:
    1. The target cell(s) are empty (no walls, no other objects).
    2. Path is not obstructed.
  - Pushing sound effect: Wooden scrape / squeaky wheel sound.
- **Path Clearance Detection Algorithm**:
  - The win condition is evaluated whenever an object stops moving.
  - Algorithm: **Breadth-First Search (BFS) / Flood Fill** from Entrance `(0, 2)` to Exit Gate `(6, 2)` through walkable grid tiles.
  - When an unobstructed continuous pathway exists from entrance to exit, `IsPathClear == true`.
- **Quality-of-Life & Reset**:
  - If a player pushes an object into a corner or gets stuck, pressing `[R]` or speaking to Bút Chì triggers *"Sắp xếp lại từ đầu"* (Reset board state to initial layout).
- **Completion Feedback & Color Bloom #2**:
  - As the path opens, the barricades part neatly.
  - Audio: Bicycle bells ringing (*leng keng leng keng*), birds chirping, gentle river breeze.
  - Color bloom expands over Zone C and up onto the majestic red-rust trusses of Long Biên Bridge! (Regional color restoration reaches **100%**).
  - Bridge gate locks dissolve into golden sparkles.

### 3.5 Level Completion & Narrative Climax
- **Bridge Gate Unlock**:
  - The gate to Long Biên Bridge opens. An interactive exit portal appears at the bridge head.
  - Bé Mây and Bút Chì step onto the bridge overlook, looking out across the Red River bathed in golden twilight.
- **Rewards Delivered**:
  1. **Trang Sổ Ký Ức #1 (Memory Journal Page 1)**:
     - Title: *"Hương Vị Phố Cổ & Nhịp Cầu Thời Gian"*
     - Visual: Full-page illustrated storybook art showing Bé Mây and Bút Chì slurping phở by the blooming Long Biên bridge.
     - Narrative Text: Documents the warmth of Hanoi, the secret of the broth, and how unity restores color to the ancient streets.
  2. **Tem Du Lịch #1 (Travel Stamp 1)**:
     - Name: *"Tem Hoa Sen Hà Nội"* (Golden embossed lotus stamp with postal date seal).
- **Narrative Cutscene / The Gray Shadow Letter (Bóng Xám)**:
  - As the bridge gate glows, a gust of cool wind swirls a desaturated gray vortex onto the bridge deck.
  - The vortex dissipates, leaving a dark gray envelope with a wax stamp on the stone path.
  - Bé Mây picks up the letter; an illustrated letter cutscene opens on screen:
    > *"Gửi Bé Mây và ngòi bút nhỏ...*
    > *Ngươi nghĩ đã mang lại sắc màu cho phố cổ này sao? Những bát phở bốc khói và nhịp cầu này chỉ là một mảnh ký ức nhỏ nhoi.*
    > *Cố đô Huế - nơi lưu giữ linh hồn và sự trang nghiêm của ngàn năm văn hiến - đang chìm sâu trong sự lãng quên mà ngươi không thể tưởng tượng nổi.*
    > *Liệu ngòi bút của ngươi có đủ sắc sảo để đánh thức điệu hò trên dòng sông Hương và những mái cung điện rêu phong?*
    > *Ta chờ ngươi nơi miền Hương Ngự...*
    > *— Ký tên: Bóng Xám."*
  - Bút Chì reacts: *"Bóng Xám đang thách thức chúng ta kìa Mây! Vùng đất Cố đô Huế đang cần chúng ta! Hãy mở bản đồ và xuất phát thôi!"*
- **Post-Completion State**:
  - Hanoi marked as `100% Restored` in `worldProgression`.
  - Next Region `Huế` unlocked on 2D World Map.
  - Save game automatically triggered.
  - Player returned to World Map with celebratory banner.

---

# 4. Dialogue, Quest & Hint Systems (R3)

### 4.1 Quest Manager State Machine & Event Dispatching
- **Quest State Lifecycle**:
  ```
  [ Locked / Hidden ]
          │ (Prerequisite completed)
          ▼
     [ Available ]
          │ (Triggered by NPC dialog or trigger volume)
          ▼
     [ InProgress ] ◄──────┐ (Partial objective progress)
          │                │
          ▼                │
  [ RequirementsMet ] ─────┘
          │ (Player reports back to turn-in NPC)
          ▼
     [ Completed ]
  ```
- **Event Bus Contract**:
  - The game architecture must use a decoupled Event Bus (`EventManager` / C# events) ensuring domain logic is testable headless:
    * `Event_QuestStarted(string questId)`
    * `Event_QuestProgressUpdated(string questId, string objectiveId, int currentCount, int targetCount)`
    * `Event_QuestCompleted(string questId)`
    * `Event_ItemAddedToInventory(string itemId)`
    * `Event_PuzzleSolved(string puzzleId)`
    * `Event_RegionColorUpdated(string regionId, float colorPercent)`
    * `Event_GateUnlocked(string gateId)`
    * `Event_CutsceneTriggered(string cutsceneId)`
    * `Event_JournalPageAwarded(string pageId)`
    * `Event_StampAwarded(string stampId)`

### 4.2 Dialogue System
- **Dialogue Script JSON Schema**:
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "DuKyBeMay_DialogueGraph",
  "type": "object",
  "properties": {
    "dialogueId": { "type": "string" },
    "startNodeId": { "type": "string" },
    "nodes": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "id": { "type": "string" },
          "speaker": {
            "type": "string",
            "enum": ["Bé Mây", "Bút Chì", "Bà Cụ Bán Phở", "Bác Xích Lô", "Cô Bán Rau", "Chủ Quầy Gia Vị", "Bóng Xám"]
          },
          "speakerAvatar": { "type": "string" },
          "avatarPosition": { "type": "string", "enum": ["Left", "Right"] },
          "text": { "type": "string" },
          "sfxVoiceBlip": { "type": "string" },
          "branches": {
            "type": "array",
            "items": {
              "type": "object",
              "properties": {
                "choiceText": { "type": "string" },
                "nextNodeId": { "type": "string" },
                "requiredQuestFlag": { "type": "string" }
              },
              "required": ["choiceText", "nextNodeId"]
            }
          },
          "nextNodeId": { "type": ["string", "null"] },
          "actionTrigger": {
            "type": ["string", "null"],
            "enum": [
              null,
              "Start_Quest_1",
              "Open_Pho_Puzzle",
              "Complete_Quest_2",
              "Trigger_GrayShadow_Cutscene"
            ]
          }
        },
        "required": ["id", "speaker", "speakerAvatar", "text"]
      }
    }
  },
  "required": ["dialogueId", "startNodeId", "nodes"]
}
```
- **UI & Presentation**:
  - High-readability pixel-clean text box with typewriter effect (configurable speed, instant skip on space/click).
  - Character portrait on Left (Mây / Bút Chì) or Right (NPCs) with blinking/speaking animation frames.
  - Choice selector buttons with hover states when branching dialogues occur.

### 4.3 Hint System (Bút Chì Companion)
- **Design Philosophy**: Whimsical, supportive, culturally educational, non-intrusive.
- **Two Trigger Modes**:
  1. **On-Demand Hint**: Player presses `[H]` or clicks the floating Bút Chì icon on the HUD. Bút Chì bounces forward with an eraser-tip flip and offers immediate advice based on current player location and active quest.
  2. **Dynamic / Idle Hint**: If the player makes no progress (idle, wandering without interacting for > 45 seconds, or failing puzzle checks 3 times), Bút Chì displays a gentle bouncing exclamation mark: `[!] Mây ơi, cần Bút Chì giúp một tay không?`.
- **Level 1 Contextual Hint Table**:
  | Active State | Scenario | Bút Chì Hint Message |
  |---|---|---|
  | Quest 1 (0-4 items) | Player near spice store | *"Mùi hoa hồi và quế chi thơm lừng từ hiệu thuốc Lãn Ông kia kìa Mây! Vào hỏi cô bán hàng thử đi!"* |
  | Quest 1 (0-4 items) | Player near vegetable cart | *"Phở ngon không thể thiếu hành hoa và rau mùi tươi đâu! Thử lại gần gánh rau kia xem sao!"* |
  | Quest 1 (5/5 items) | All items gathered | *"Đủ cả 5 nguyên liệu rồi! Mau mang về cho Bà Cụ Bán Phở kẻo nước hầm nguội mất Mây ơi!"* |
  | Quest 2 (Cooking) | Broth placed too early | *"Nước dùng mà chan trước khi cho bánh phở thì bánh sẽ bị nát và nguội mất, Mây ơi! Đặt bánh phở tươi xuống đáy bát trước nhé!"* |
  | Quest 2 (Cooking) | Herbs placed before meat | *"Thịt bò thái mỏng phải trải đều trên bánh phở trước, rồi mới rắc hành hoa lên trên để giữ trọn vị ngọt!"* |
  | Quest 3 (Stall Puzzle)| Stalled for > 30s | *"Chiếc xe xích lô dài quá chắn ngang đường rồi. Thử đẩy sạp hoa sang bên trái xem có chỗ cho xích lô tiến lên không nhé!"* |
  | Quest 3 (Stall Puzzle)| Dead-end blocked | *"Đừng lo Mây ơi, nếu bị kẹt hàng thì bấm phím [R] để sắp xếp lại từ đầu nhé!"* |
  | Bridge Gate Open | Gate glowing | *"Lối đi ra Cầu Long Biên đã mở rồi! Mau tới xem vẻ đẹp của cây cầu lịch sử thôi!"* |

---

# 5. Collection Album System (R4)

### 5.1 Album UI Architecture & Tabs
- Accessible at any time via `[J]` or HUD album icon.
- Visual metaphor: A luxurious handcrafted leather-bound scrapbook with gold foil embossing, bookmark ribbons, and textured Vietnamese Do paper (giấy Dó).
- **Tab 1: Trang Sổ Ký Ức (Memory Journal Pages)**:
  - Grid / Carousel of discovered full-page watercolor art spreads.
  - Page 1 (Unlocked by Hanoi completion):
    - *Illustration*: Mây and Bút Chì sitting at the street stall eating a steaming bowl of phở with the Long Biên Bridge in full sunset color behind them.
    - *Narrative Entry*: *"Hà Nội - Những ngày thu thơm mùi hoa sữa và ấm nồng nước phở. Khi sắc màu trở lại, những nụ cười trên phố cổ lại bừng sáng..."*
- **Tab 2: Tem Du Lịch (Travel Stamps Collection)**:
  - Passport-style grid with perforated postage stamp outlines.
  - Stamp 1 (Hanoi):
    - *Artwork*: Golden West Lake Lotus Flower (Hoa Sen Tây Hồ) with postmark: `HÀ NỘI - 1000 NĂM VĂN HIẾN`.
    - *Status*: Golden embossed stamp when collected; faint silhouette with lock when unearned.
- **Tab 3: Thẻ Văn Hóa & Sự Thật Thú Vị (Cultural Fact Cards / Trivia)**:
  - Hanoi Slice features 4 collectible trivia cards unlocked during exploration and quest resolution:
    1. **Thẻ 01: Nét Tinh Túy Phở Hà Nội**: Explains the history of Hanoi Pho, the importance of simmering beef bones for 10-12 hours with ginger and grilled shallots, and why star anise and cinnamon are essential.
    2. **Thẻ 02: Cầu Long Biên - Dải Lụa Thép Qua Sông Hồng**: Built between 1898 and 1902, surviving historical wars, standing as a living witness to Hanoi's resilience and soul.
    3. **Thẻ 03: Hoa Sen Tây Hồ & Văn Hóa Trà Sen**: The bách diệp lotus flower of West Lake and the ancient art of scenting green tea inside lotus blossoms overnight.
    4. **Thẻ 04: Chuyện 36 Phố Phường**: How each street in the Old Quarter took the name of the specialty craft produced there (Hàng Bạc - silver, Hàng Buồm - sails, Hàng Gai - silk).

---

# 6. Comprehensive Feature Inventory

## Features Discovered
| # | Category | Feature | Description | Inputs | Outputs | Error Behavior | Discovered Via |
|---|----------|---------|-------------|--------|---------|----------------|----------------|
| 1 | Progression | 2D World Map Navigation | Display interactive regional map of Vietnam with selectable nodes | Arrow keys / WASD / Mouse click | Highlighted node, camera pan, level preview panel | Sound click on locked node; displays "Chưa mở khóa" | ORIGINAL_REQUEST.md § R1 |
| 2 | Progression | Region Color Restoration State | Visual reflection of color recovery (0% desaturated to 100% full vibrant color) | Region progress percentage (0-100%) | Dynamic color saturation shader / sprite swap | Clamps to [0, 100]; fallback to grayscale if data missing | ORIGINAL_REQUEST.md § R1 |
| 3 | Persistence | Save Game to JSON | Serialize current game progression, inventory, quest flags, album to persistent storage | Save event trigger (level exit, checkpoint) | Atomic write to `savegame.json` | Fallback to `.tmp`, keeps previous backup on write failure | ORIGINAL_REQUEST.md § R1 |
| 4 | Persistence | Load Game from JSON | Deserialize saved state into memory on game launch | Application start / Load menu | Restored player coordinates, quests, stamps, map state | If file corrupt or missing, generates clean default state without crashing | ORIGINAL_REQUEST.md § R1 |
| 5 | Exploration | Bé Mây 2D Movement | Smooth 4-way character movement with inertia and collision detection | WASD / Arrow Keys | Vector2 displacement, walking animation frames | Collides with solid colliders without tunneling or clipping | ORIGINAL_REQUEST.md § R2 |
| 6 | Exploration | Dynamic Y-Sorting | Renders sprites in proper visual depth order based on Y-axis foot line | Entity Y coordinates | Correct front-to-back render layering | Default to sorting layer order if Y equal | Requirement analysis § R2 |
| 7 | Exploration | Contextual Interaction Prompt | Displays floating `[E] Tương tác` bubble when player is near interactables | Distance to interactable entity <= interaction radius | Floating UI prompt on screen | Prompt fades smoothly when player walks away | ORIGINAL_REQUEST.md § R2 |
| 8 | Quest 1 | Ingredient Gathering Loop | Collect 5 authentic phở ingredients from market merchants | Interact key `[E]` at designated vendor stalls | Item added to inventory, counter increments (x/5), sound chime | Vendor repeats reminder dialogue if already collected or wrong quest | ORIGINAL_REQUEST.md § R2 |
| 9 | Quest 1 | Inventory HUD Display | Upper screen inventory tracker showing collected ingredient icons | Inventory state change event | Updated icon display (colored when held, grayed out when missing) | Graceful empty slots display | Requirement analysis § R2 |
| 10 | Quest 2 | Phở Assembly Mini-game UI | Modal interface with porcelain bowl, ingredient cards, and ordered slots | Quest 1 complete -> Talk to Bà An | Interactive cooking station screen | ESC closes mini-game safely without state corruption | ORIGINAL_REQUEST.md § R2 |
| 11 | Quest 2 | Culinary Ordering Logic | Validates authentic 5-step layering order (Noodles -> Beef -> Herbs -> Broth -> Seasoning) | Placement of 5 ingredients into sequential slots | Success fanfare or informative hint feedback | Out-of-order placement triggers Bút Chì hint and resets invalid card | ORIGINAL_REQUEST.md § R2 |
| 12 | Quest 2 | Phở Stall Color Bloom | First regional color wave restoring vibrant tones to Phở stall and surrounding plaza | Quest 2 completion event | Color ripple animation, +33% regional color restoration | Fallback to instant color switch if animation skipped | ORIGINAL_REQUEST.md § R2 |
| 13 | Quest 3 | Street Stall Spatial Grid | 7x5 top-down grid puzzle with pushable carts and flower stalls blocking bridge | Directional push `[E] + Walk` against movable object | Object moves 1 grid unit into free cell | Blocked if target cell occupied by wall or another stall | ORIGINAL_REQUEST.md § R2 |
| 14 | Quest 3 | Path Clearance Detection (BFS) | Real-time pathfinding to check if continuous clear path exists from street to Long Biên gate | Object move completion event | Unlocks Bridge Gate when path is open (`IsPathClear == true`) | Keeps gate locked if any grid cell along shortest path blocked | ORIGINAL_REQUEST.md § R2 |
| 15 | Quest 3 | Puzzle Reset Function | Resets all movable stalls to starting grid positions if player gets stuck | Key `[R]` or dialogue option with Bút Chì | Smooth slide reset of all puzzle entities | Safe to call anytime; cannot corrupt grid state | Requirement analysis § R2 |
| 16 | Quest 3 | Long Biên Full Color Wave | Restores 100% color to entire Hanoi level including Long Biên Bridge | Quest 3 completion event | Full color screen wipe, street audio ambiance, joyful bells | Idempotent: does not re-trigger if already restored | ORIGINAL_REQUEST.md § R2 |
| 17 | Level Exit | Bridge Gate Transition | Unlocks portal to finish level and transition back to world map | Walk into unlocked Long Biên bridge portal | Triggers reward sequence and cutscene | Blocked with message "Cổng chưa mở" if Q1-Q3 not done | ORIGINAL_REQUEST.md § R2 |
| 18 | Rewards | Memory Journal Page Award | Delivers illustrated Hanoi storybook page into player's album | Level 1 completion event | Modal page pop-up with illustration and narrative text | Safely marked in save data; prevents duplicate grants | ORIGINAL_REQUEST.md § R2 |
| 19 | Rewards | Lotus Stamp Award | Delivers "Tem Hoa Sen Hà Nội" into passport stamp collection | Level 1 completion event | Golden stamp stamping animation and sound effect | Prevent duplicate awards | ORIGINAL_REQUEST.md § R2 |
| 20 | Story | Gray Shadow Cutscene & Letter | Narrative cutscene revealing Bóng Xám letter pointing to Huế | Gate unlock / Level completion | Fullscreen letter modal with voice/sound effects | ESC or Space advances/skips to next page | ORIGINAL_REQUEST.md § R2 |
| 21 | Quests | Quest State Manager | Finite state machine tracking quest lifecycle and prerequisites | In-game actions, dialogue triggers | State transitions (`NotStarted` -> `InProgress` -> `Completed`) | Rejects invalid transitions; maintains consistency | ORIGINAL_REQUEST.md § R3 |
| 22 | Quests | Event Dispatching Bus | Decoupled observer event architecture for quests, items, and UI | State triggers, player interactions | Dispatches events to subscribers (Audio, UI, Progression) | Unhandled events logged without crashing | ORIGINAL_REQUEST.md § R3 |
| 23 | Dialogue | JSON Dialogue Graph Parser | Loads and parses dialogue trees from structured JSON files | Dialogue file path / resource stream | In-memory DialogueNode graph with speaker, avatar, choices | Logs error and returns empty node if JSON malformed | ORIGINAL_REQUEST.md § R3 |
| 24 | Dialogue | Typewriter Dialogue UI | Animated character dialogue box with avatar portraits and choices | DialogueNode data | Scrolling text characters, audio blips, choice buttons | Space/Enter instantly finishes typing or advances | ORIGINAL_REQUEST.md § R3 |
| 25 | Hint | Bút Chì On-Demand Hint | Keypress triggers Bút Chì with contextual advice for current task | Key `[H]` or click on Bút Chì icon | Floating Bút Chì dialogue speech bubble | If no active quest, gives warm general encouraging remark | ORIGINAL_REQUEST.md § R3 |
| 26 | Hint | Bút Chì Dynamic Idle Prompt | Automatically prompts player with clue after prolonged inactivity or repeated failures | Inactivity timer (> 45s) or 3 failed puzzle attempts | Animated `[!]` alert bubble above Bút Chì | Non-intrusive; auto-dismisses if player makes progress | ORIGINAL_REQUEST.md § R3 |
| 27 | Collection | Album UI Book Viewer | Interactive book interface with page-turning animation and 3 tabs | Key `[J]` or HUD icon | Opens Scrapbook modal with Journal, Stamps, Fact Cards | Pauses background gameplay while open | ORIGINAL_REQUEST.md § R4 |
| 28 | Collection | Hanoi Cultural Fact Cards | 4 detailed trivia cards about Phở, Long Biên, Sen Tây Hồ, 36 Phố Phường | Exploration interactions / quest completion | Inspectable cards with high-res illustrations and cultural facts | Displays card details on click | ORIGINAL_REQUEST.md § R4 |
| 29 | Audio | Dynamic Audio & SFX | Plays Vietnamese cultural ambient tracks, bicycle bells, sizzle of phở broth | Scene load, interactions, puzzle actions | Sound effects and looping background music | Silent fallback if audio device unavailable (headless safe) | Technical survey & R2 |
| 30 | Architecture | Headless Test Harness | Allows full game loop and quest simulation without Raylib window/audio hardware | CLI command / test runner | Assertions on quest states, inventory, save/load schemas | Exit code 0 on test pass, non-zero on failure | ORIGINAL_REQUEST.md § Acceptance Criteria |

---

# 7. Edge Cases & Robustness Analysis

## Edge Cases
| # | Feature | Input / Condition | Observed / Expected Behavior |
|---|---------|-------------------|------------------------------|
| 1 | Save/Load | Corrupted `savegame.json` (invalid JSON syntax or truncated file) | Game logs warning, backs up corrupt file to `savegame.json.corrupt`, instantiates clean default game state, launches successfully without crash. |
| 2 | Save/Load | Missing `savegame.json` on brand-new installation | Creates initial game state with Hanoi unlocked (0% color), empty album, empty quest flags, and writes new file cleanly. |
| 3 | Save/Load | Player kills game process (SIGKILL / power loss) during save write | Atomic write pattern (write to `.tmp` then rename) guarantees file is never left in half-written state. |
| 4 | Quest 1 | Player interacts with spice merchant multiple times | First interaction grants `Item_ThaoMoc`; subsequent interactions trigger friendly merchant reminder: *"Mây đã có đủ hoa hồi quế rồi đấy, mau mang về cho Bà An nhé!"*. Item count remains 1 (idempotent). |
| 5 | Quest 1 | Player visits Bà An before gathering all 5 ingredients | Bà An reminds Mây what ingredients are still missing: *"Bà vẫn đang đợi bánh phở và chút hoa hồi thơm Mây ơi!"*. Quest 2 does NOT open. |
| 6 | Quest 2 | Player submits Phở with correct 5 ingredients but wrong sequence (e.g. Broth first) | Mini-game rejects submission; Bút Chì gives gentle specific advice explaining why broth belongs after noodles and meat; cards return to tray without penalty. |
| 7 | Quest 2 | Player clicks Finish when only 3 out of 5 slots are filled | Error chime; tooltip indicates *"Bát phở vẫn còn thiếu nguyên liệu, hãy hoàn thiện đủ các bước nhé!"*. |
| 8 | Quest 2 | Player presses `[Esc]` or close button mid-assembly | Mini-game closes gracefully; collected ingredients remain safely in inventory; reopening mini-game restores clean or previous state. |
| 9 | Quest 3 | Player pushes movable stall into a corner (dead-end trap) | Object stops at boundary; player cannot push through walls; player can press `[R]` or speak to Bút Chì to reset puzzle layout cleanly to initial state. |
| 10 | Quest 3 | Player tries to push an object when adjacent target cell is occupied | Push is blocked; player continues moving or walks around object; no jitter or coordinate snapping glitch. |
| 11 | Quest 3 | Player walks directly to Long Biên Bridge gate before solving grid | Gate is visually barred by stalls/gatekeeper; interaction triggers message: *"Lối ra Cầu Long Biên đang bị các sạp hàng chắn kín!"*. |
| 12 | Quest 3 | Rapid multiple pushes on consecutive frames | Physics and grid coordinate updates are locked during the move animation slide (~0.2s); concurrent pushes are queued or ignored until entity reaches target tile. |
| 13 | Movement | Player presses opposing directions simultaneously (W+S or A+D) | Direction vector sums to zero; character stops walking and retains last facing idle direction without flickering. |
| 14 | Dialogue | Rapidly pressing `[Space]` or clicking during dialogue | First click completes typewriter text instantly for current line; second click advances to next dialogue node. |
| 15 | Dialogue | Dialogue node points to non-existent `nextNodeId` | Dialogue parser logs error, gracefully ends dialogue, returns control to player without null reference exception. |
| 16 | Hint System | Player spams `[H]` repeatedly | Bút Chì dialogue speech bubble resets timer; does not stack multiple overlapping audio or visual bubbles. |
| 17 | Collection | Opening Album while in middle of dialogue or puzzle | Album key `[J]` is disabled/suppressed during active dialogues or modal cooking puzzle to prevent modal stacking conflicts. |
| 18 | Headless Mode | Running game logic or automated tests on server without graphics/audio display | Engine detects headless flag or runs pure domain logic tests without initializing Raylib window or audio device; returns exit code 0. |

---

# 8. Acceptance Criteria Mapping & Testability Requirements

### 8.1 Acceptance Criteria Traceability Matrix
| Request Acceptance Criteria | Specification Implementation | Verification Test Strategy |
|---|---|---|
| **AC-1**: Game compiles successfully and launches without errors. | Clean .NET 10 project structure, Raylib-cs bindings, headless runner capability. | `dotnet build` succeeds with 0 warnings/errors; smoke test initializes and exits cleanly. |
| **AC-2**: World Map displays locations, reflections of unlock and color states. | `WorldMapManager` with node states (`Locked`, `Unlocked`, `Completed`), color percentage shader/tint. | Unit tests verify node state transitions; UI test verifies node click triggers Hanoi level. |
| **AC-3**: Player controls Bé Mây smoothly with WASD/arrows & interacts with NPCs/objects. | `PlayerController` with velocity damping, AABB collision, `InteractionDetector` trigger radius. | Automated simulation test feeds WASD input, asserts coordinate delta, triggers `[E]` within radius. |
| **AC-4**: All 3 Hanoi Quests solvable with precise logic and visual feedback. | `QuestManager` state machine: Q1 5-item inventory check, Q2 ordering validator, Q3 BFS path solver. | Unit tests test all permutations of Q1 pickups, Q2 slot orders, Q3 grid moves & BFS path clear. |
| **AC-5**: Completing 3 quests unlocks gate, awards 1 journal page, 1 lotus stamp, shows Gray Shadow letter. | `LevelCompletionHandler` listens to Q3 complete event, unlocks gate, triggers cutscene modal and album rewards. | Integration test completes Q1->Q2->Q3, asserts gate unlocked, page #1 in album, stamp #1 in album. |
| **AC-6**: Dialogue System parses JSON; Bút Chì hint system operates as expected. | `DialogueParser` validates against schema; `HintManager` on-demand and idle timers. | Unit test validates all dialog JSON files; tests on-demand `[H]` returns expected context string. |
| **AC-7**: Collection Album displays journal pages, stamps, and cultural fact cards. | `AlbumModel` stores unlocked pages, stamps, and trivia cards with inspection view. | Unit test checks album data model populates correctly upon quest completion events. |
| **AC-8**: Progress data persistently saved and loaded across game restarts. | `SaveManager` with atomic file write and schema validation. | Test saves progress, resets memory, loads file, verifies exact equality of quest flags & items. |
| **AC-9**: Automated test suite validates quest logic, game states, and Save/Load. | xUnit / NUnit test project with automated domain tests and headless game harness. | `dotnet test` executes comprehensive test suite with 100% pass rate. |

### 8.2 Headless Testability Architecture
To guarantee automated testability on headless CI/CD systems (macOS terminal, Linux servers) without requiring an active OpenGL window or audio device:
1. **Core Domain Separation (Clean Architecture)**:
   - `DuKyBeMay.Core`: Pure C# domain logic containing Game States, Quest State Machine, Inventory, Dialogue Graphs, Puzzle Solvers (Q2 Pho Validator, Q3 Grid BFS), Save/Load Serializer. **Zero dependency on Raylib-cs or graphics hardware**.
   - `DuKyBeMay.Presentation`: Raylib-cs rendering, input reading, audio synthesis, camera management.
2. **Automated Test Suite Targets**:
   - `SaveLoadTests.cs`: Roundtrip persistence, corruption handling, default creation.
   - `QuestProgressionTests.cs`: Full Level 1 flow from initial state to Long Biên gate unlock.
   - `PhoAssemblyPuzzleTests.cs`: Correct ordering test, 120 incorrect permutation tests, missing item tests.
   - `StallGridPuzzleTests.cs`: Grid boundary tests, collision tests, BFS reachability solver, reset functionality.
   - `DialogueSchemaTests.cs`: JSON linting, schema conformity, broken link detection for all dialog files.
   - `HintSystemTests.cs`: Contextual string generation for each active quest state.

---

# 9. Handoff Protocol Report

### 1. Observation
- The workspace `/Users/mac/Projects/game1` has been set up with .NET 10.0 and Raylib-cs version 8.1.0 (`GameProject.csproj`).
- `Program.cs` currently contains a standalone audio synthesis test confirming sound synthesis capability in Raylib-cs.
- The directory skeleton includes `Source/UI`, `Source/Core`, `Source/Stages`, `Source/Utils`, `Source/Audio`, `Source/Graphics`, `Source/Data`, and `Data/`.
- `ORIGINAL_REQUEST.md` specifies the complete 2D Vertical Slice scope for "Du Ký Bé Mây" across 4 core requirement areas (R1: World Map & Save/Load, R2: Hanoi Exploration & 3 Quests, R3: Dialogue/Quest/Hint Systems, R4: Collection Album) plus 9 Acceptance Criteria.

### 2. Logic Chain
1. *Requirement R1* demands a 2D world map with region unlock states and persistent Save/Load. Based on clean architecture, the Save/Load schema must be represented in robust JSON with atomic write/replace to avoid data corruption, tracking world unlock states, level progression, quest flags, and album collections.
2. *Requirement R2* requires 3 interconnected Hanoi quests. The natural narrative and design progression flows sequentially:
   - Quest 1: Explore Old Quarter market stalls to gather 5 signature phở ingredients.
   - Quest 2: Assemble the authentic bowl of phở via drag-and-drop ordering (validating traditional Vietnamese culinary order: Noodles -> Beef -> Herbs -> Hot Broth -> Seasoning).
   - Quest 3: Sokoban-style spatial grid puzzle clearing movable flower carts, cyclos, and crates to open the path to Long Biên Bridge.
   - Completion: Unlocks the Bridge Gate, awards Memory Journal Page #1, Lotus Stamp #1, and triggers the Gray Shadow letter cutscene.
3. *Requirement R3* requires decoupled Quest Management and JSON-based dialogue graphs. A formal JSON schema ensures dialogue trees can be authored and validated headlessly, while Bút Chì provides both on-demand and idle hints.
4. *Requirement R4* specifies a Collection Album with 3 distinct categories: Journal Pages, Travel Stamps, and Cultural Fact Cards (featuring authentic Hanoi trivia).
5. *Acceptance Criteria* demand automated tests. By isolating pure domain models into a headless core, automated unit and E2E simulation tests can run in any environment without OpenGL/display hardware dependencies.

### 3. Caveats
- No graphical asset files (PNG/TTF) are pre-existing in `Data/`. The rendering engine must be designed to either load custom 2D sprite/font assets or procedurally render charming flat cartoon shapes and vector silhouettes via Raylib primitives until art assets are provided.
- Dialogue scripts must be packaged into JSON files in `Data/Dialogues/` and validated against the schema defined in Section 4.2.

### 4. Conclusion
The game design and technical requirements for "Du Ký Bé Mây" Vertical Slice are fully excavated, specified, and structured into a cohesive, production-grade blueprint. Every feature, quest step, puzzle rule, data model, and edge case is defined with explicit inputs, outputs, error behaviors, and testability criteria.

### 5. Verification Method
- **Specification Verification**:
  1. Inspect `handoff.md` and verify all 4 requirements (R1, R2, R3, R4) and 9 Acceptance Criteria are fully mapped.
  2. Verify JSON schemas for Save/Load and Dialogue systems are valid JSON Schema Draft-07 compliant.
  3. Verify the Pho recipe ordering and Sokoban grid BFS logic are mathematically deterministic and solvable.
- **Project Build & Test Verification**:
  - `dotnet build` builds without errors.
  - Test runner can execute domain test suites headlessly once implemented.
