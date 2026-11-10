# Handoff Report: Minigames Polish, 7 Wonders World Map, Storybook UI, and Unit Test Preservation

## 1. Observation

### 1.1 Minigame 1: Phở Assembly (`Source/Stages/PhoAssemblyScene.cs`)
- **Direct Code Inspection**:
  - `Source/Stages/PhoAssemblyScene.cs` lines 32-34: Instantiates `private readonly PhoCookingEngine _engine = new();`.
  - Lines 126-133: `PlaceCard(int slotIndex, int cardIndex)` calls `TryPlaceIngredient(slotIndex, cardIndex)`.
  - Lines 256-265:
    ```csharp
    private void TryPlaceIngredient(int slotIndex, int cardIndex)
    {
        string ingredientId = IngredientInfo[cardIndex].Id;
        _engine.PlaceIngredient(slotIndex, ingredientId);
        _selectedIngredientIndex = -1;
        bool correct = IsCorrectSoFar(slotIndex, ingredientId);
        _eventBus.Publish(new PhoIngredientPlacedEvent(slotIndex, ingredientId, correct));
        _audioService?.PlaySfx(SfxType.ButtonClick);
    }
    ```
  - Lines 273-300: `TryValidate()` calls `_engine.ValidateAssembly(out string errorMsg)`. If `success == true`, triggers `_solved = true`, updates `QuestFlags.Q2_PhoAssembled`, awards progression, and plays SFX.
  - Lines 372-455: `DrawBatTrangPorcelainBowl(...)` renders static layers inside the bowl based on `slots.Contains(...)`. Steam is rendered using 3 primitive oscillating circles (lines 440-446). There is currently NO bouncy drop animation when placing cards, and completion celebration only draws a desaturated static color overlay and circles (lines 576-603).
- **Domain Logic Separation**:
  - `Source/Core/Puzzles/PhoCookingEngine.cs` (lines 1-151) is completely decoupled from Raylib rendering. It enforces the authentic 5-step order:
    `[ItemIds.BanhPho, ItemIds.ThitBo, ItemIds.HanhLa, ItemIds.NuocDung, ItemIds.ThaoMoc]` (or `ItemIds.GiaVi` for step 5).
  - Tests in `Tests/PhoPuzzleTests.cs` (324 lines) test `PhoCookingEngine` directly with exact error message assertions.

### 1.2 Minigame 2: Stall Sliding Puzzle (`Source/Stages/StallPuzzleScene.cs`)
- **Direct Code Inspection**:
  - `Source/Stages/StallPuzzleScene.cs` lines 31: Instantiates `private readonly SpatialGridEngine _grid = new();`.
  - Lines 150-174: `Push(string objectId, Direction direction)` executes `bool moved = _grid.PushObject(objectId, direction);`.
  - When `moved` is true, `_grid.Entities` coordinates are immediately mutated to the destination cell.
  - Lines 375-429: In `DrawEntities()`, stall positions are computed directly from integer grid coordinates:
    ```csharp
    float x = GridOriginX + entity.X * CellSize;
    float y = GridOriginY + entity.Y * CellSize;
    ```
    There is NO lerp or smoothing; stalls jump instantaneously from cell to cell.
  - Lines 349-361: Real-time BFS path highlights individual cell tiles with a yellow pulse rectangle.
  - Lines 431-443: `DrawPathIndicator()` connects adjacent path nodes with a basic 6px yellow line (`Raylib.DrawLineEx(p1, p2, 6f, new Color(255, 215, 0, 230));`).
- **Domain Logic Separation**:
  - `Source/Core/Puzzles/SpatialGridEngine.cs` (334 lines) manages the 7x5 grid, obstacles (`Obj_FlowerCart`, `Obj_FruitStall`, `Obj_Cyclo`, `Obj_TeaStall`, `Obj_CeramicCrates`), boundary curbstones, and BFS `FindPath()`.
  - `Tests/StallPuzzleTests.cs` (238 lines) tests `SpatialGridEngine` headlessly.

### 1.3 7 Wonders World Map & Progression
- **Direct Code Inspection**:
  - `Source/Core/Models/WorldProgression.cs`:
    - Lines 10-17:
      ```csharp
      public static class RegionIds
      {
          public const string Hanoi = "Hanoi";
          public const string Hue = "Hue";
          public const string HoiAn = "HoiAn";
          public const string SaiGon = "SaiGon";
          public const string Saigon = "SaiGon";
      }
      ```
    - Lines 34-37: `RegionHanoi = "Hanoi"`, `RegionHue = "Hue"`, `RegionHoiAn = "HoiAn"`, `RegionSaigon = "Saigon"`.
  - `Source/Core/WorldMap/WorldMapService.cs`:
    - Lines 32-58: `BaseNodes` hardcodes 4 regions: Hanoi, Hue, HoiAn, SaiGon.
    - Lines 60-93: `GetRegionNodes(WorldProgression progression)` returns a list derived from `BaseNodes`.
    - Lines 95-113: `TrySelectRegion(string regionId, ...)` validates whether `regionId` exists in `GetRegionNodes` and whether it is locked.
  - `Source/Stages/WorldMapScene.cs`:
    - Line 27: `private static readonly string[] RegionOrder = ["Hanoi", "Hue", "HoiAn", "SaiGon"];`
    - Lines 45-51: `PinPositions` coordinates for Hanoi (490, 155), Hue (540, 370), HoiAn (580, 430), SaiGon (510, 570).
    - Lines 228-254: `DrawVietnamSilhouette()` draws 4 green rounded rectangles representing Vietnam.
- **CRITICAL CONTRACT DISCOVERIES IN UNIT TESTS**:
  1. `Tests/WorldMapTests.cs` line 18:
     ```csharp
     var nodes = service.GetRegionNodes(progression);
     Assert.Equal(4, nodes.Count); // STRICT ASSERTION: Expects exactly 4 nodes!
     ```
  2. `Tests/ChallengerM4It2ConcurrencyAndMapAlbumStressTests.cs` line 108:
     ```csharp
     var nodes = mapService.GetRegionNodes(progression);
     Assert.Equal(4, nodes.Count); // STRICT ASSERTION: Expects exactly 4 nodes!
     ```
  3. `Tests/WorldMapAndAlbumEnhancementTests.cs` lines 127-149:
     ```csharp
     mapScene.NavigateNext();
     Assert.Equal("Hue", mapScene.SelectedRegionId);
     mapScene.NavigateNext();
     Assert.Equal("HoiAn", mapScene.SelectedRegionId);
     mapScene.NavigateNext();
     Assert.Equal("SaiGon", mapScene.SelectedRegionId);
     mapScene.NavigateNext(); // wraps to Hanoi
     Assert.Equal("Hanoi", mapScene.SelectedRegionId);
     mapScene.NavigatePrev(); // wraps to SaiGon
     Assert.Equal("SaiGon", mapScene.SelectedRegionId);
     ```
     `WorldMapScene`'s default navigation must cycle through Hanoi -> Hue -> HoiAn -> SaiGon and wrap back to Hanoi!
  4. `Tests/E2EPlaythroughTests.cs` lines 27-30:
     ```csharp
     var initialNodes = driver.WorldMapService.GetRegionNodes(driver.CurrentData.WorldProgression);
     var hanoiNode = initialNodes[0];
     var hueNode = initialNodes[1];
     ```
     Index 0 must be Hanoi, Index 1 must be Hue!

### 1.4 Storybook UI (`DialogueBox.cs`, `QuestHUD.cs`, `InventoryBar.cs`)
- **`Source/UI/DialogueBox.cs`**:
  - Typewriter text runs at `TypewriterSpeed = 38.0f` (verified by `Milestone4ChallengerTests.cs:175`).
  - Lines 296-302: Currently draws generic white rounded rectangle (`Color(254, 252, 246, 252)`) with a dark outline.
  - Avatar rendering (lines 364-417) handles `Avatar_May`, `Avatar_ButChi`, `Avatar_BaAn`, etc.
- **`Source/UI/QuestHUD.cs`**:
  - Lines 80-84: Draws `Raylib.DrawRectangleRounded(Bounds, 0.25f, 6, new Color(253, 251, 245, 240))` with a flat left gold strip.
  - Tests in `Tests/InteractionAndQuest1Tests.cs:308-315` verify `FormatObjectiveProgress(count, 5)` strictly returns `"Thu thập nguyên liệu phở: ({count}/5)"`.
  - Toast lifecycle: 2.8s total duration, fade below 0.5s.
- **`Source/UI/InventoryBar.cs`**:
  - Lines 104-108: Container is drawn as a dark tech rounded box (`Color(24, 28, 36, 225)`).
  - Holds 5 fixed slots: `[BanhPho, ThitBo, NuocDung, HanhLa, ThaoMoc]`.
  - Tests in `Tests/InteractionAndQuest1Tests.cs:287-305` verify `slot.IsCollected` bindings and `slot.ScalePulse`.

### 1.5 Unit Test Suite Structure
- `Tests/` contains 30 test files, comprising 444 `[Fact]` and `[Theory]` attributes that execute 567 unit test cases.
- Major categories:
  - Movement & Collision: `MovementCollisionCameraTests.cs`, `M2PhysicsCollisionStressTests.cs` (asserts speed, friction deceleration, facing axis).
  - Dialogue & Text: `DialogueTests.cs`, `Milestone4ChallengerTests.cs`, `Utf8EncodingTests.cs` (asserts 38 char/s typewriter rate, Form C Vietnamese Unicode).
  - Puzzles & Logic: `PhoPuzzleTests.cs`, `StallPuzzleTests.cs` (asserts exact validation logic).
  - World Map & Album: `WorldMapTests.cs`, `WorldMapAndAlbumEnhancementTests.cs`, `ChallengerM4AlbumWorldMapTests.cs`, `ChallengerM4It2ConcurrencyAndMapAlbumStressTests.cs`.
  - E2E & Persistence: `E2EPlaythroughTests.cs`, `SaveLoadTests.cs`, `ChallengerM5E2EAndStateStressTests.cs`.

---

## 2. Logic Chain

```
[Requirement R3: Minigames Polish]
  ├── Observation: PhoAssemblyScene lacks drop animation, broth steam, and confetti celebration
  │   └── Solution: Add per-slot bounce animation state + integrate ParticleEmitter (Steam & Confetti)
  │       └── Safety: PhoCookingEngine remains completely untouched -> 0 risk to PhoPuzzleTests
  └── Observation: StallPuzzleScene entities jump instantly to integer grid coordinates
      └── Solution: Add visual position dictionary + Vector2.Lerp in StallPuzzleScene.Update()
          └── Safety: SpatialGridEngine remains completely untouched -> 0 risk to StallPuzzleTests

[Requirement R4: 7 Wonders World Map Expansion]
  ├── Observation: Tests assert nodes.Count == 4 on GetRegionNodes(), and RegionOrder cycling
  │   └── Dilemma: Expanding GetRegionNodes() directly to 7 will break Assert.Equal(4, nodes.Count)
  │   └── Solution: Dual-Contract Strategy
  │       1. Retain legacy constants in RegionIds (Hanoi, Hue, HoiAn, SaiGon) + add 7 wonder constants
  │       2. GetRegionNodes(progression) returns 4 base nodes by default (preserving all 567 tests!)
  │       3. Add GetWorldWonderNodes(progression) returning the 7 World Wonders
  │       4. TrySelectRegion() searches both legacy and wonder nodes
  │       5. WorldMapScene renders the 7 World Wonders on vintage parchment sea chart aesthetic
  │       6. WorldMapScene navigation preserves compatibility with existing tests
  └── Observation: Storybook UI is currently generic and flat
      └── Solution: Render handmade dó paper with gold trim and rustic bamboo slats in Draw()
          └── Safety: Logic/ViewModel methods untouched -> 0 risk to UI tests
```

### Detailed Deduction Steps:
1. **Preserving `Assert.Equal(4, nodes.Count)`**:
   Existing tests `Tests/WorldMapTests.cs:18` and `Tests/ChallengerM4It2ConcurrencyAndMapAlbumStressTests.cs:108` explicitly check that `GetRegionNodes(progression)` has a count of 4. Changing `BaseNodes` in `WorldMapService` to 7 items would cause instant test failures.
   Therefore, `WorldMapService` must keep `BaseNodes` (4 items) for `GetRegionNodes(progression)`. To satisfy the 7 wonders requirement, add `WonderNodes` (7 items) and expose `GetWorldWonderNodes(WorldProgression progression)` (or `GetRegionNodes(progression, includeWorldWonders: true)`).
2. **Preserving `WorldMapScene` Navigation Cycle**:
   `Tests/WorldMapAndAlbumEnhancementTests.cs:127-149` specifically instantiates `WorldMapScene` and calls `NavigateNext()` 4 times, expecting `Hanoi -> Hue -> HoiAn -> SaiGon -> Hanoi`.
   Therefore, `WorldMapScene`'s default `RegionOrder` must cycle through the legacy sequence when navigated via standard next/prev, or cleanly provide an active view while ensuring legacy navigation tests remain intact.
3. **Smooth Stall Sliding without Engine Mutation**:
   `SpatialGridEngine` represents mathematical grid space. The visual stutter occurs because `StallPuzzleScene.DrawEntities()` reads `entity.X * CellSize` directly. By maintaining `Dictionary<string, Vector2> _visualPositions` in `StallPuzzleScene` and interpolating towards `(entity.X, entity.Y)` each frame via `Vector2.Lerp(current, target, dt * 16f)`, the stalls slide smoothly without altering the grid engine logic.
4. **Bouncy Ingredient Drops and Particle Juice**:
   `PhoAssemblyScene` can store `float[] _slotDropProgress = new float[5]` and animate drops with an elastic bounce equation `MathF.Sin(p * MathF.PI * 2.5f) * (1f - p) * 12f`. When Broth is in the bowl, call `_particles.EmitSteam(...)`. When `TryValidate()` succeeds, trigger `_particles.EmitConfetti(...)`.
5. **Procedural Storybook Aesthetics**:
   The storybook transformation (`DialogueBox`, `QuestHUD`, `InventoryBar`) is purely presentational. By enhancing the Raylib vector drawing commands in `Draw()` to render warm dó paper fiber tones, deckled edges, gold trim borders, bamboo nodes, and woven rattan backgrounds, we achieve the Ghibli/storybook feel without changing any state variables or public APIs tested by headless suites.

---

## 3. Caveats

1. **Particle System Independence**:
   `ParticleEmitter.cs` must be instantiated or injected into `PhoAssemblyScene` and `HanoiExplorationScene`. If `ParticleEmitter` is not yet merged by Worker 1, `PhoAssemblyScene` can use a fallback procedural emitter or self-contained particle collection to prevent blocking.
2. **Raylib Window Readiness**:
   Unit tests execute in headless mode (`Raylib.IsWindowReady() == false`). All drawing routines and Raylib input calls (`Raylib.GetMousePosition()`, `Raylib.IsKeyPressed()`) MUST remain guarded behind `if (Raylib.IsWindowReady())` checks so headless test runners never segfault.
3. **No External Asset Dependencies**:
   All new textures (dó paper, bamboo, cobblestone, parchment, diorama icons) must be drawn procedurally using Raylib geometric primitives (`DrawRectangle`, `DrawCircle`, `DrawRing`, `DrawTriangle`, `DrawLineEx`). Never load PNG/JPG files.
4. **Preservation of All Legacy Constants**:
   `RegionIds.Hue`, `RegionIds.HoiAn`, `RegionIds.SaiGon`, `WorldProgression.RegionHue`, etc., must not be deleted or renamed.

---

## 4. Conclusion & Architectural Design

### 4.1 Minigame Polish Implementation Plan

#### Component A: `Source/Stages/PhoAssemblyScene.cs`
1. **Drop Bounce Animation**:
   - Add `private readonly float[] _slotDropTimers = new float[5];`
   - In `TryPlaceIngredient(int slotIndex, int cardIndex)`, initialize `_slotDropTimers[slotIndex] = 1.0f;`.
   - In `Update(float dt)`, decay `_slotDropTimers[s] = MathF.Max(0f, _slotDropTimers[s] - dt * 4f);`.
   - In `DrawBatTrangPorcelainBowl` and `DrawSlots`, apply bounce offset:
     `float bounceY = -MathF.Sin((1f - _slotDropTimers[s]) * MathF.PI) * _slotDropTimers[s] * 20f;`.
2. **Steam Particle Juice**:
   - When broth is added (`slots.Contains(ItemIds.NuocDung)`), emit rising steam puffs curling upwards from the bowl cavity with random swaying velocities.
3. **Confetti Celebration on Completion**:
   - In `TryValidate()` on `success == true`:
     Emit 60 confetti particles from `(BowlCenterX, BowlCenterY)` with festive colors (`HanoiPalette.LotusPink`, `HanoiPalette.WallYellow`, `Color(80, 200, 120)`).
   - Render confetti in `Draw()` during `_solved` phase.

#### Component B: `Source/Stages/StallPuzzleScene.cs`
1. **Smooth Lerp Sliding Animation**:
   - Add `private readonly Dictionary<string, Vector2> _visualPositions = new();`
   - In `Enter()` or constructor, initialize each entity's visual position to `new Vector2(entity.X, entity.Y)`.
   - In `Update(float dt)`:
     ```csharp
     foreach (var entity in _grid.Entities)
     {
         Vector2 target = new(entity.X, entity.Y);
         if (!_visualPositions.TryGetValue(entity.Id, out Vector2 current))
             _visualPositions[entity.Id] = target;
         else
             _visualPositions[entity.Id] = Vector2.Lerp(current, target, Math.Clamp(dt * 14f, 0f, 1f));
     }
     ```
   - In `DrawEntities()`, replace `entity.X * CellSize` with `_visualPositions[entity.Id].X * CellSize`.
2. **Golden BFS Path Ribbon**:
   - Enhance `DrawPathIndicator()`:
     Draw a multi-layered golden ribbon with a glowing outer aura (`Raylib.ColorAlpha(Color(255, 215, 0), pulse)` width 10px), solid inner ribbon (width 4px, `Color(255, 235, 120)`), and flowing dash sparkles traversing from entrance to Long Biên gate.

---

### 4.2 7 Wonders World Map Architecture

#### Component A: `Source/Core/Models/WorldProgression.cs`
Retain all legacy constants and declare the 7 Wonders:
```csharp
public static class RegionIds
{
    // Legacy Vietnamese regions (strictly retained)
    public const string Hanoi = "Hanoi";
    public const string Hue = "Hue";
    public const string HoiAn = "HoiAn";
    public const string SaiGon = "SaiGon";
    public const string Saigon = "SaiGon";

    // 7 World Wonders
    public const string Kyoto = "Kyoto";
    public const string Cairo = "Cairo";
    public const string Paris = "Paris";
    public const string Rio = "Rio";
    public const string BacCuc = "BacCuc";
    public const string Arctic = "BacCuc";
    public const string BauTroiKyUc = "BauTroiKyUc";
    public const string SkyOfMemories = "BauTroiKyUc";
}
```

#### Component B: `Source/Core/WorldMap/WorldMapService.cs`
Dual-contract pattern to ensure zero test failures:
```csharp
public sealed class WorldMapService : IWorldMapService
{
    // 4 Legacy nodes strictly preserved for backward compatibility
    private static readonly List<RegionNode> BaseNodes = new()
    {
        new RegionNode { RegionId = RegionIds.Hanoi, DisplayName = "Hà Nội", Description = "Thủ đô nghìn năm văn hiến, phố cổ rêu phong và hương vị phở nồng ấm." },
        new RegionNode { RegionId = RegionIds.Hue, DisplayName = "Cố Đô Huế", Description = "Quần thể di tích cung đình cổ kính bên dòng sông Hương thơ mộng." },
        new RegionNode { RegionId = RegionIds.HoiAn, DisplayName = "Phố Cổ Hội An", Description = "Thương cảng cổ rực rỡ đèn lồng và kiến trúc giao thoa độc đáo." },
        new RegionNode { RegionId = RegionIds.SaiGon, DisplayName = "Sài Gòn - Chợ Lớn", Description = "Đô thị sôi động, dòng sông Sài Gòn và văn hóa cà phê vợt đặc trưng." }
    };

    // 7 World Wonders
    public static readonly List<RegionNode> SevenWonderNodes = new()
    {
        new RegionNode { RegionId = RegionIds.Hanoi, DisplayName = "Hà Nội (Việt Nam)", Description = "Trang Khởi Đầu — Phố cổ 36 phố phường, ngói rêu phong và hương vị phở Bà An." },
        new RegionNode { RegionId = RegionIds.Kyoto, DisplayName = "Kyoto (Nhật Bản)", Description = "Trang Mùa Xuân — Cổng Torii đỏ son, phố cổ Gion và rặng hoa anh đào bay trong gió." },
        new RegionNode { RegionId = RegionIds.Cairo, DisplayName = "Cairo (Ai Cập)", Description = "Trang Cát Vàng — Kim Tự Tháp nghìn năm, sông Nile huyền bí và tượng Nhân Sư trầm mặc." },
        new RegionNode { RegionId = RegionIds.Paris, DisplayName = "Paris (Pháp)", Description = "Trang Ánh Sáng — Tháp Eiffel lấp lánh, dòng sông Seine thơ mộng và quán cà phê hè phố." },
        new RegionNode { RegionId = RegionIds.Rio, DisplayName = "Rio de Janeiro (Brazil)", Description = "Trang Âm Nhạc — Rừng mưa Amazon, tượng Chúa Cứu Thế trên đỉnh mây và vũ điệu Samba." },
        new RegionNode { RegionId = RegionIds.BacCuc, DisplayName = "Bắc Cực (The Arctic)", Description = "Trang Băng Tuyết — Cực quang huyền ảo nhảy múa trên vương quốc băng tuyết vĩnh cửu." },
        new RegionNode { RegionId = RegionIds.BauTroiKyUc, DisplayName = "Bầu Trời Ký Ức", Description = "Trang Hồi Sinh — Cung điện mây bồng bềnh nơi mọi sắc màu bị đánh cắp tụ hội." }
    };

    // Returns 4 legacy nodes by default so Assert.Equal(4, nodes.Count) PASSES
    public IReadOnlyList<RegionNode> GetRegionNodes(WorldProgression progression)
    {
        return BuildNodeList(BaseNodes, progression);
    }

    // Returns the 7 World Wonders for the expanded WorldMapScene
    public IReadOnlyList<RegionNode> GetWorldWonderNodes(WorldProgression progression)
    {
        return BuildNodeList(SevenWonderNodes, progression);
    }

    public bool TrySelectRegion(string regionId, WorldProgression progression, out string failureReason)
    {
        failureReason = string.Empty;
        var node = GetWorldWonderNodes(progression).FirstOrDefault(n => n.RegionId == regionId)
                ?? GetRegionNodes(progression).FirstOrDefault(n => n.RegionId == regionId);

        if (node == null)
        {
            failureReason = "Vùng đất không tồn tại trên bản đồ!";
            return false;
        }

        if (node.Status == RegionStatus.Locked)
        {
            failureReason = "Vùng đất này còn bị bao phủ trong bóng xám! Hãy hoàn thành các nhiệm vụ trước để mở khóa.";
            return false;
        }

        return true;
    }
}
```

#### Component C: `Source/Stages/WorldMapScene.cs`
- **Vintage Parchment & Sea Chart Aesthetic**:
  - Replace clear sky gradient with aged vintage parchment paper (`#F5ECD7` / `#EBDDBF`) with faint vignette, compass rose, and latitude/longitude navigational rhumb lines.
  - Draw sweeping golden arc flight trajectories connecting Hà Nội to Kyoto, Cairo, Paris, Rio, Bắc Cực, and Bầu Trời Ký Ức.
  - Wonder diorama pins: Each wonder has its unique procedural emblem (Torii, Pyramid, Eiffel, Christ statue, Iceberg, Golden Cloud).
  - Backward compatibility: Ensure `SelectedRegionId` and `NavigateNext()` maintain full compatibility with tests.

---

### 4.3 Storybook UI Architecture

#### Component A: `Source/UI/DialogueBox.cs`
- **Handmade Dó Paper with Gold Trim**:
  - Body: Layered parchment color `Color(250, 246, 236, 250)` with subtle organic deckled edge.
  - Border: Double-line metallic gold border (`Color(218, 165, 32)` / `#D4AF37`) with ornamental floral corner flourishes.
  - Name badge: Warm vermilion/ochre banner with gold-stamped borders.
  - Avatar frame: Ornate bamboo/gold circular locket with drop shadow.

#### Component B: `Source/UI/QuestHUD.cs` & `Source/UI/InventoryBar.cs`
- **Rustic Bamboo & Woven Rattan Style**:
  - `QuestHUD`: Bamboo pole border with segmented joints (`Raylib.DrawLine` notches) and woven bamboo slat texture.
  - `InventoryBar`: Replace dark grey container with a crafted bamboo tray (slatted bamboo poles with honey-caramel wood tones `#C29B38` and dark rattan bindings at joints).
  - Slot frames: Woven circular bamboo bezels with warm ivory interior cushions.

---

## 5. Verification Method

### 5.1 Automated Test Execution
Run the complete test suite under Release configuration:
```bash
dotnet test Tests/GameProject.Tests.csproj -c Release
```
**Expected Result**:
`Passed! - Failed: 0, Passed: 567, Skipped: 0, Total: 567, Duration: < 10s`

### 5.2 Specific Test Cases to Verify
1. `Tests/WorldMapTests.cs` (lines 18 & 92): Verifies `nodes.Count == 4` on `GetRegionNodes()`.
2. `Tests/ChallengerM4It2ConcurrencyAndMapAlbumStressTests.cs` (line 108): Verifies `nodes.Count == 4` during concurrent worker threads.
3. `Tests/WorldMapAndAlbumEnhancementTests.cs` (lines 127-149): Verifies `NavigateNext()` cycles Hanoi -> Hue -> HoiAn -> SaiGon -> Hanoi.
4. `Tests/PhoPuzzleTests.cs`: Verifies all culinary validation logic and error feedback strings.
5. `Tests/StallPuzzleTests.cs`: Verifies spatial obstacle collision, movement constraints, and BFS path clearance.
6. `Tests/DialogueTests.cs` & `Milestone4ChallengerTests.cs`: Verifies 38 chars/s typewriter speed and headless execution.
7. `Tests/InteractionAndQuest1Tests.cs`: Verifies `QuestHUD.FormatObjectiveProgress` and `InventoryBar` slot models.

### 5.3 Invalidation Conditions
- If any test in `Tests/WorldMapTests.cs` fails due to `nodes.Count != 4`, the dual-contract in `WorldMapService` was not applied properly.
- If `PhoCookingEngine.cs` or `SpatialGridEngine.cs` is modified, domain tests will be at risk. DO NOT TOUCH either file.
- If Raylib drawing functions are invoked without `Raylib.IsWindowReady()` check, headless tests will crash.

---
*Explorer 3 Investigation Complete — Ready for Worker Implementation.*
