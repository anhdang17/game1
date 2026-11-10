# Project: Du Ký Bé Mây Visual & Gameplay Revamp

## Architecture
- Presentation Layer: Pure Raylib procedural vector graphics (`VectorRenderer.cs`, `HanoiPalette.cs`, `ParticleEmitter.cs`). Zero external sprite/image dependencies.
- Movement & Physics: `CharacterKinematics.cs` with smooth deceleration, Cloud Glide, and Dash activation via Space/Shift keys.
- Exploration Stage: `HanoiExplorationScene.cs` with organic cobblestone pavement, moss, puddles, lanterns with warm radial glow, Long Bien bridge, and Bà An steaming pho stall (grid lines removed).
- Minigames Stage: `PhoAssemblyScene.cs` (bouncy drop animation, broth steam, confetti celebration) and `StallPuzzleScene.cs` (smooth lerp sliding, golden BFS path ribbon). Core puzzle engines (`PhoCookingEngine.cs`, `SpatialGridEngine.cs`) are 100% decoupled and preserved.
- World Progression: `WorldProgression.cs` & `WorldMapService.cs` with dual-contract architecture (retaining 4 legacy nodes for backward-compatible `GetRegionNodes()` and 7 Wonder nodes for `GetWorldWonderNodes()`). `WorldMapScene.cs` with vintage parchment sea chart diorama.
- Storybook UI: `DialogueBox.cs` (handmade dó paper with gold trim), `QuestHUD.cs` & `InventoryBar.cs` (rustic bamboo & rattan styling).

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Bé Mây Cloud Redesign | 5-puff fluffy magical cloud, rosy cheeks, golden crest, mini tilted nón lá, leather pouch, expressive sparkly eyes | M1 | ORIGINAL_REQUEST §R1 |
| 2 | Bút Chì Companion Redesign | 3-facet hexagonal wooden body, wood grain, pink eraser, silver ferrule, eye tracking, cyan magic tip with star dust | M1 | ORIGINAL_REQUEST §R1 |
| 3 | Bóng Xám Visual Helper | Dreamy slate-violet storm cloud with soulful expressive eyes and falling mist particles (`DrawBongXam`) | M1 | Plan §Component 2 |
| 4 | Procedural Particle Emitter | Lightweight procedural particle system supporting CloudPuff, Steam, Sparkles, Confetti with headless test safety | M2 | ORIGINAL_REQUEST §R2 |
| 5 | Cloud Glide & Dash Kinematics | Smooth deceleration, Space/Shift Dash mechanics with cloud puffs, preserving `GetVerticalBobbingOffset() == 0f` when idle | M2 | ORIGINAL_REQUEST §R3 |
| 6 | Old Quarter Grid Line Removal | Remove Cartesian checkerboard grid lines in `HanoiExplorationScene.cs` (lines 708-715) | M3 | ORIGINAL_REQUEST §R2 |
| 7 | Cobblestone Street & Environment Art | Procedural cobblestones, moss patches, rain puddles with sky reflections, fallen petals/leaves, weathered colonial walls, green shutters, warm red lanterns | M3 | ORIGINAL_REQUEST §R2 |
| 8 | Bà An Steaming Pho Stall | Bà An NPC with silver hair chignon and ladle, porcelain bowls, and steaming broth pot with glowing coals | M3 | Plan §Component 3 |
| 9 | Pho Assembly Minigame Polish | Bouncy ingredient drop animation, steam puffs on broth addition, confetti explosion celebration on recipe completion | M4 | ORIGINAL_REQUEST §R3 |
| 10 | Stall Puzzle Lerp Sliding | Smooth visual lerp interpolation for sliding stalls/carts and glowing multi-layer golden BFS path ribbon | M4 | ORIGINAL_REQUEST §R3 |
| 11 | 7 Wonders World Map Expansion | Support 7 World Wonders in `WorldProgression.cs` & `WorldMapService.cs` with dual-contract preserving 4 legacy nodes for test compatibility | M5 | ORIGINAL_REQUEST §R4 |
| 12 | Storybook Parchment Sea Chart Map | 2D vintage parchment sea chart with continental flight arcs, wonder diorama emblems, and progress states in `WorldMapScene.cs` | M5 | ORIGINAL_REQUEST §R4 |
| 13 | Handmade Dó Paper Dialogue Box | Dó paper texture, deckled edges, and double-line gold trim corner flourishes in `DialogueBox.cs` | M5 | ORIGINAL_REQUEST §R4 |
| 14 | Rustic Bamboo QuestHUD & Inventory | Bamboo slat framing, joint notches, and woven rattan backgrounds in `QuestHUD.cs` and `InventoryBar.cs` | M5 | ORIGINAL_REQUEST §R4 |
| 15 | Test Suite Preservation (567/567) | Complete test suite preservation under Release configuration (`dotnet test Tests/GameProject.Tests.csproj -c Release`) | M6 | ORIGINAL_REQUEST §R5 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Character Presentation Revamp | `VectorRenderer.cs`, `HanoiPalette.cs` | none | DONE |
| M2 | Particle System & Kinematics Dash | `ParticleEmitter.cs`, `CharacterKinematics.cs`, Input ports | none | DONE |
| M3 | Old Quarter Environment Art | `HanoiExplorationScene.cs` | M1, M2 | DONE |
| M4 | Minigames Polish | `PhoAssemblyScene.cs`, `StallPuzzleScene.cs` | M2 | DONE |
| M5 | 7 Wonders World Map & Storybook UI | `WorldProgression.cs`, `WorldMapService.cs`, `WorldMapScene.cs`, `DialogueBox.cs`, `QuestHUD.cs`, `InventoryBar.cs` | none | DONE |
| M6 | Final Acceptance & Regression Verification | Full test execution (567/567 passed), adversarial coverage audit | M1, M2, M3, M4, M5 | DONE |

## Interface Contracts
### `VectorRenderer` ↔ Caller Scenes
- `DrawBeMay(Vector2 feetPos, FacingDirection facing, float animTime, bool isMoving, float scale = 1.0f, bool withShadow = true)`
- `DrawButChi(Vector2 floatingPos, float animTime, float scale = 1.0f)`
- `DrawBongXam(Vector2 centerPos, float animTime, float scale = 1.0f)`
- `DrawBaAnPhoStall(Vector2 pos, string stallName, float animTime)`

### `ParticleEmitter` ↔ Scenes
- `EmitCloudPuff(Vector2 pos, int count = 3)`
- `EmitSteam(Vector2 potPos, float dt)`
- `EmitSparkles(Vector2 pos, Color color, int count = 5)`
- `EmitConfetti(Vector2 center, int count = 30)`
- `Update(float dt)`, `Draw()`, `UpdateAndDraw(float dt)`, `Clear()`, `ParticleCount`
- Safe for headless tests: `if (!Raylib.IsWindowReady()) return;` inside `Draw()`.

### `CharacterKinematics` ↔ Input & Scenes
- `bool TryDash(Vector2 inputDirection)`
- `bool IsDashing`, `float DashTimer`, `float DashCooldown`
- `GetVerticalBobbingOffset()` returns strictly `0f` when `!IsMoving`.
- `IInputService.IsDashJustPressed` bound to `LeftShift`, `RightShift`, `Space`.

### `WorldMapService` ↔ Tests & Scenes
- `GetRegionNodes(WorldProgression progression)` returns exactly 4 legacy nodes (Hanoi, Hue, HoiAn, SaiGon) to satisfy `Tests/WorldMapTests.cs:18`.
- `GetWorldWonderNodes(WorldProgression progression)` returns 7 World Wonders for `WorldMapScene.cs`.
- `TrySelectRegion(string regionId, WorldProgression progression, out string failureReason)` resolves against both wonder and legacy nodes.

## Code Layout
- `Source/Graphics/HanoiPalette.cs` — Owned by M1
- `Source/Graphics/VectorRenderer.cs` — Owned by M1
- `Source/Graphics/ParticleEmitter.cs` — Owned by M2 (NEW)
- `Source/Core/Movement/CharacterKinematics.cs` — Owned by M2
- `Source/Core/Ports/IInputService.cs` — Owned by M2
- `Source/Graphics/RaylibInputService.cs` — Owned by M2
- `Source/Core/Ports/MockInputService.cs` — Owned by M2
- `Source/Stages/HanoiExplorationScene.cs` — Owned by M3
- `Source/Stages/PhoAssemblyScene.cs` — Owned by M4
- `Source/Stages/StallPuzzleScene.cs` — Owned by M4
- `Source/Core/Models/WorldProgression.cs` — Owned by M5
- `Source/Core/WorldMap/WorldMapService.cs` — Owned by M5
- `Source/Stages/WorldMapScene.cs` — Owned by M5
- `Source/UI/DialogueBox.cs` — Owned by M5
- `Source/UI/QuestHUD.cs` — Owned by M5
- `Source/UI/InventoryBar.cs` — Owned by M5
- Core Domain Engines (READ-ONLY, UNTOUCHED):
  - `Source/Core/Puzzles/PhoCookingEngine.cs`
  - `Source/Core/Puzzles/SpatialGridEngine.cs`
  - `Source/Core/Models/SaveData.cs`
  - `Source/Core/Quests/QuestManager.cs`
