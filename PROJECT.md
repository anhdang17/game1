# Project: Du Ký Bé Mây (Vertical Slice)

## Architecture
- **Paradigm**: Clean Architecture separating Pure C# Core Domain (`Source/Core/`) from Presentation (`Source/Graphics/`, `Source/UI/`, `Source/Audio/`, `Source/Stages/`).
- **Platform**: .NET 10.0 (C# 13) + Raylib-cs 8.1.0 on macOS arm64 (Apple Silicon).
- **Presentation Engine**: Raylib-cs for 2D vector shapes, rounded rectangles, TTF font rendering with Vietnamese UTF-8 glyphs, camera management, and procedural audio synthesis.
- **Persistence**: Durable atomic JSON file persistence (`savegame.json`) with versioning and backup fallback.
- **Headless Testability**: All game domain logic, quest progression, dialogue states, and puzzle mechanics run headlessly in `GameProject.Tests` (< 500 ms) via `xUnit` with zero display server dependency.

## Code Layout
```
/Users/mac/Projects/game1/
├── GameProject.csproj                    # Main application project (.NET 10, Raylib-cs 8.1.0)
├── Program.cs                            # Application entry point & game loop
├── Source/
│   ├── Core/                            # PURE C# DOMAIN (Zero Raylib dependencies)
│   │   ├── Models/                      # Player, Region, Quest, Inventory, Album models
│   │   ├── State/                       # GameStateManager, RegionState, SaveData
│   │   ├── Quests/                      # QuestManager, HanoiQuests (Q1, Q2, Q3)
│   │   ├── Puzzles/                     # PhoCookingEngine, SpatialGridEngine (BFS)
│   │   ├── Dialogue/                    # DialogueGraph, DialogueNode, Speaker
│   │   ├── Hints/                       # HintManager, ButChiHintEngine
│   │   ├── Persistence/                 # SaveLoadService, SaveGameJsonContext
│   │   ├── Events/                      # EventBus, GameEvents
│   │   └── Ports/                       # IInputService, IAudioService, ISaveStorage
│   ├── Graphics/                        # Raylib presentation rendering
│   │   ├── VectorRenderer.cs            # Rounded flat cartoon shape renderer
│   │   ├── FontManager.cs               # Vietnamese UTF-8 TTF font loader
│   │   └── ColorBloomEffect.cs          # Color restoration ripple effect
│   ├── Audio/                           # Audio presentation & synthesis
│   │   ├── AudioService.cs              # Raylib audio player implementation
│   │   └── ProceduralSynth.cs           # Chimes, victory fanfare, footstep SFX
│   ├── UI/                              # User Interface components
│   │   ├── DialogueBox.cs               # Typewriter dialogue box & choices
│   │   ├── QuestHUD.cs                  # Objective tracker & notifications
│   │   ├── InventoryBar.cs              # Ingredient icons hotbar
│   │   ├── AlbumViewer.cs               # Scrapbook UI (Pages, Stamps, Trivia)
│   │   └── ButChiGuideUI.cs             # Companion portrait & hint bubble
│   ├── Stages/                          # Scenes & stage controllers
│   │   ├── SceneManager.cs              # Scene state manager
│   │   ├── WorldMapScene.cs             # Vietnam 2D regional map
│   │   ├── HanoiExplorationScene.cs     # Hanoi Old Quarter street exploration
│   │   ├── PhoAssemblyScene.cs          # Quest 2 culinary mini-game
│   │   └── StallPuzzleScene.cs          # Quest 3 Long Bien clearance puzzle
│   ├── Data/                            # JSON content data
│   │   ├── dialogues_hanoi.json         # Dialogue scripts for Hanoi characters
│   │   ├── quests_hanoi.json            # Quest definitions and objectives
│   │   └── trivia_hanoi.json            # Cultural cards & album content
│   └── Utils/                           # Shared mathematical and helper utilities
├── Tests/                               # Headless Automated Test Project
│   ├── GameProject.Tests.csproj         # xUnit test project (.NET 10)
│   ├── Mocks/                           # MockInputService, MockSaveStorage, NullAudio
│   ├── QuestFlowTests.cs                # Hanoi Q1, Q2, Q3 progression tests
│   ├── PhoPuzzleTests.cs                # Culinary assembly sequence validation
│   ├── StallPuzzleTests.cs              # Spatial grid sliding & BFS clearance tests
│   ├── DialogueTests.cs                 # JSON parsing & choice branching tests
│   ├── HintTests.cs                     # On-demand & idle hint trigger tests
│   ├── SaveLoadTests.cs                 # Persistence durability & corruption recovery
│   └── Utf8EncodingTests.cs             # Vietnamese diacritic string integrity
└── .agents/                             # Agent teamwork coordination state
```

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | 2D World Map Navigation | Regional map of Vietnam with selectable nodes (Hanoi, Huế) | M4 | ORIGINAL_REQUEST § R1 |
| 2 | Region Color Restoration State | Visual reflection of color recovery (0% to 100%) | M4 | ORIGINAL_REQUEST § R1 |
| 3 | Save Game to JSON | Serialize progression, inventory, quest flags, album to JSON | M1 | ORIGINAL_REQUEST § R1 |
| 4 | Load Game from JSON | Deserialize saved state on startup with fallback resilience | M1 | ORIGINAL_REQUEST § R1 |
| 5 | Bé Mây 2D Movement | Smooth 4-way character movement with inertia and collision | M2 | ORIGINAL_REQUEST § R2 |
| 6 | Dynamic Y-Sorting | Renders sprites in proper visual depth order based on Y-axis | M2 | ORIGINAL_REQUEST § R2 |
| 7 | Interaction Prompt Bubble | Floating `[E] Tương tác` prompt when near interactables | M2 | ORIGINAL_REQUEST § R2 |
| 8 | Ingredient Gathering Loop | Collect 5 authentic phở ingredients from market merchants | M2 | ORIGINAL_REQUEST § R2 |
| 9 | Inventory HUD Display | Upper screen inventory tracker showing collected ingredient icons | M2 | ORIGINAL_REQUEST § R2 |
| 10 | Phở Assembly Mini-game UI | Modal interface with porcelain bowl, ingredient cards, slots | M3 | ORIGINAL_REQUEST § R2 |
| 11 | Culinary Ordering Logic | Validates authentic 5-step layering order (Noodles->Beef->Herbs->Broth->Garnish) | M3 | ORIGINAL_REQUEST § R2 |
| 12 | Phở Stall Color Bloom | First regional color wave restoring +33% vibrant color | M3 | ORIGINAL_REQUEST § R2 |
| 13 | Street Stall Spatial Grid | 7x5 top-down grid puzzle with pushable carts and flower stalls | M3 | ORIGINAL_REQUEST § R2 |
| 14 | Path Clearance Detection (BFS) | Real-time BFS flood fill checking path from street to Long Bien gate | M3 | ORIGINAL_REQUEST § R2 |
| 15 | Puzzle Reset Function | Resets movable stalls to starting positions on `[R]` or hint | M3 | ORIGINAL_REQUEST § R2 |
| 16 | Long Biên Full Color Wave | Restores 100% color to Hanoi level and Long Biên Bridge | M3 | ORIGINAL_REQUEST § R2 |
| 17 | Bridge Gate Transition | Unlocks portal to finish level and transition to world map | M3 | ORIGINAL_REQUEST § R2 |
| 18 | Memory Journal Page Award | Delivers illustrated Hanoi storybook page into album | M3 | ORIGINAL_REQUEST § R2 |
| 19 | Lotus Stamp Award | Delivers "Tem Hoa Sen Hà Nội" into passport stamp collection | M3 | ORIGINAL_REQUEST § R2 |
| 20 | Gray Shadow Letter & Cutscene | Narrative cutscene revealing Bóng Xám letter pointing to Huế | M3 | ORIGINAL_REQUEST § R2 |
| 21 | Quest State Manager | Finite state machine tracking quest lifecycle and prerequisites | M1 | ORIGINAL_REQUEST § R3 |
| 22 | Event Dispatching Bus | Decoupled observer event architecture for quests, items, and UI | M1 | ORIGINAL_REQUEST § R3 |
| 23 | JSON Dialogue Graph Parser | Loads and parses dialogue trees from structured JSON files | M4 | ORIGINAL_REQUEST § R3 |
| 24 | Typewriter Dialogue UI | Animated character dialogue box with avatar portraits and choices | M4 | ORIGINAL_REQUEST § R3 |
| 25 | Bút Chì On-Demand Hints | Companion advice triggered via `[H]` based on quest state | M4 | ORIGINAL_REQUEST § R3 |
| 26 | Idle Contextual Hints | Companion exclamation bubble if player is stalled > 45s | M4 | ORIGINAL_REQUEST § R3 |
| 27 | Hanoi Street Collision Bounds | Static collision geometry preventing walking through walls/curbs | M2 | ORIGINAL_REQUEST § R2 |
| 28 | Collection Album UI | 3-tab scrapbook for Journal Pages, Travel Stamps, Fact Cards | M4 | ORIGINAL_REQUEST § R4 |
| 29 | Save Data Corruption Recovery | Atomic save with backup and auto-repair on corrupted JSON | M1 | ORIGINAL_REQUEST § R1 |
| 30 | Headless Simulation Driver | Mock input and headless update loop for automated test runner | M1 | ORIGINAL_REQUEST § Acceptance |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Core Domain Engine & Persistence | Models, GameStateManager, QuestEngine, SaveLoadService, EventBus, MockPorts | none | DONE |
| M2 | Hanoi Exploration & Quest 1 | Hanoi street map, Bé Mây movement, Y-sorting, stalls, Q1 ingredient gathering | M1 | DONE |
| M3 | Hanoi Quests 2 & 3 & Progression Climax | Q2 Phở assembly, Q3 spatial grid BFS, Long Biên gate unlock, Journal Page, Lotus Stamp, Gray Shadow letter | M1, M2 | IN_PROGRESS |
| M4 | Dialogue, Hint, Album Systems & World Map | JSON Dialogue parser & UI, Bút Chì hint engine, Collection Album (3 tabs), 2D World Map | M1, M3 | PLANNED |
| M5 | Final Integration & E2E Verification | Phase 1: Pass 100% E2E tests (Tiers 1-4); Phase 2: Adversarial coverage hardening (Tier 5); Production build packaging | M1, M2, M3, M4 | PLANNED |

## Interface Contracts

### M1 ↔ M2 (Core Domain ↔ Hanoi Exploration)
- `GameStateManager`: Provides `CurrentState`, `ActiveRegion`, `PlayerProfile`.
- `QuestManager`: Exposes `StartQuest("Quest_Hanoi_01")`, `RecordObjectiveProgress("Item_BanhPho")`, `IsQuestCompleted("Quest_Hanoi_01")`.
- `Inventory`: Exposes `AddItem(string itemId)`, `HasItem(string itemId)`, `GetItems()`.
- `EventBus`: Fires `OnItemCollected`, `OnQuestUpdated`.

### M1/M2 ↔ M3 (Exploration ↔ Puzzles & Level Climax)
- `IPhoPuzzleEngine`:
  - `bool PlaceIngredient(int slotIndex, string ingredientId)`
  - `bool ValidateAssembly(out string errorMessage)`
  - `void ResetPuzzle()`
  - Sequence requirement: `["Item_BanhPho", "Item_ThitBo", "Item_HanhLa", "Item_NuocDung", "Item_GiaVi"]`
- `IStallGridEngine`:
  - `void InitializeGrid(int width = 7, int height = 5)`
  - `bool PushObject(string objectId, Direction dir)`
  - `bool IsPathClear()` (evaluates BFS from `(0, 2)` to `(6, 2)`)
  - `void ResetGrid()`
- `LevelProgressionService`:
  - `CompleteLevel1()`: marks Hanoi `ColorPercent = 100.0`, awards `JournalPage_Hanoi_01`, awards `Stamp_HoaSen_Hanoi`, unlocks `Hue` region, triggers `GrayShadow_Letter`.

### M1/M3 ↔ M4 (Core & Progression ↔ Dialogue, Hints, Album, World Map)
- `IDialogueService`:
  - `void LoadDialogueFile(string path)`
  - `DialogueNode StartDialogue(string dialogueId)`
  - `DialogueNode SelectChoice(string choiceId)`
- `IHintService`:
  - `string GetCurrentHint(string activeQuestId, GameContext context)`
- `IAlbumService`:
  - `IReadOnlyList<JournalPage> GetJournalPages()`
  - `IReadOnlyList<TravelStamp> GetTravelStamps()`
  - `IReadOnlyList<FactCard> GetUnlockedFactCards()`
- `IWorldMapService`:
  - `IReadOnlyList<RegionNode> GetRegionNodes()`
  - `bool TrySelectRegion(string regionId, out string failureReason)`
