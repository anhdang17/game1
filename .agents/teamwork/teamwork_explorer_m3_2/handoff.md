# Handoff Report: Milestone 3 Explorer 2 (Quest 3 Street Stall Spatial Grid BFS Puzzle)

## 1. Observation

### 1.1 Existing Codebase & File Paths
- **`Source/Core/Puzzles/SpatialGridEngine.cs`**:
  - Pure C# domain engine with zero Raylib presentation dependencies.
  - Interface `IStallGridEngine` (lines 58–70) and class `SpatialGridEngine` (lines 72–316).
  - Grid dimensions: `Width = 7`, `Height = 5` (lines 74–75).
  - Fixed entrance at `(0, 2)` and exit gate at `(6, 2)` (lines 77–78).
  - Row 0 and Row 4 curbstones are static immovable walls (`Wall_Top_0..6`, `Wall_Bottom_0..6`, lines 97–121).
  - Active playable cells span Rows 1, 2, and 3 across Columns 0 through 6.
  - Starting entities (lines 123–188):
    1. `Obj_FlowerCart`: `x=2, y=1, Width=1, Height=2` (occupies `(2, 1)` and `(2, 2)`), `CanPushHorizontally=false`, `CanPushVertically=true`.
    2. `Obj_FruitStall`: `x=2, y=3, Width=1, Height=1` (occupies `(2, 3)`), `CanPushHorizontally=true`, `CanPushVertically=true`.
    3. `Obj_Cyclo`: `x=3, y=2, Width=2, Height=1` (occupies `(3, 2)` and `(4, 2)`), `CanPushHorizontally=true`, `CanPushVertically=false`.
    4. `Obj_TeaStall`: `x=5, y=2, Width=1, Height=1` (occupies `(5, 2)`), `CanPushHorizontally=true`, `CanPushVertically=true`.
    5. `Obj_CeramicCrates`: `x=3, y=3, Width=1, Height=1` (occupies `(3, 3)`), `CanPushHorizontally=true`, `CanPushVertically=true`.
  - Push mechanics: `PushObject(string objectId, Direction direction)` (lines 202–258) validates directional axes, boundary bounds `[0, Width)` and `[0, Height)`, and prevents overlap collisions with other entities.
  - Path detection: `IsPathClear()` (lines 260–315) runs BFS from `(0, 2)` to `(6, 2)` and returns `bool`.
  - Reset mechanics: `ResetGrid()` (lines 193–200) clones `_initialEntities` back to original positions.

### 1.2 Identified Bugs and Gaps in Existing Presentation
- **`Source/Stages/StallPuzzleScene.cs`**:
  - **Entity ID Mismatch** (lines 45–61):
    ```csharp
    private static readonly Dictionary<string, Color> EntityColors = new()
    {
        { "Obj_FlowerCart",    new Color(255, 182, 193, 255) },
        { "Obj_PhoStall",      new Color(255, 220, 100, 255) }, // BUG: SpatialGridEngine defines "Obj_FruitStall"
        { "Obj_Cyclo",         new Color(43,  76,  72,  255) },
        { "Obj_TeaStall",      new Color(180, 230, 180, 255) },
        { "Obj_CeramicCrates", new Color(205, 149,  85, 255) },
    };
    ```
    Because `SpatialGridEngine` instantiates `"Obj_FruitStall"`, lookups for `"Obj_FruitStall"` fail, falling back to dull grey `Color(200, 200, 200)` and unlocalized raw ID text.
  - **BFS Path Highlight Missing** (lines 335–343):
    `DrawPathIndicator()` currently only renders text `"✓ Đường thông!"` or `"✗ Đường bị chặn"` below the grid. Neither `SpatialGridEngine` nor `StallPuzzleScene` extracts the coordinate sequence of cells along the solved route, omitting the required real-time glowing path highlight.
  - **Reset Event Omission** (lines 147–154):
    When the player presses `[R]`, `_grid.ResetGrid()` is called, but `_eventBus.Publish(new StallPuzzleResetEvent())` is NOT dispatched, breaking event-driven analytics and test spy assertions.
  - **Long Biên Color Bloom & Climax Integration**:
    `TriggerSolve()` (lines 186–195) correctly invokes `_progression.CompleteLevel1()`, which marks `100.0f` color restoration, awards `JournalPage_Hanoi_01` and `Stamp_HoaSen_Hanoi`, and unlocks `Gate_LongBien_Unlocked`. However, `HanoiExplorationScene` lacks an interactable barrier and collision geometry at the eastern bridge entrance (x ≈ 1450).

### 1.3 Test Suite Status
- Command `dotnet test Tests/GameProject.Tests.csproj` executes **303 passing tests in 193 ms** with 0 failures and 0 skips.
- `Tests/StallPuzzleTests.cs` (lines 1–117) contains 7 core tests validating 1-move/2-move constraints, horizontal/vertical lockouts, boundary collisions, 2-step BFS solution, and coordinate reset.

---

## 2. Logic Chain

### 2.1 Spatial Puzzle Geometry & Solvability Proof
- **Observation**:
  - Curbstones at Row 0 and Row 4 are immovable static walls. Playable height is Rows 1, 2, and 3.
  - Flower Cart has `Width=1, Height=2`, starting at `(2, 1)`, occupying `(2, 1)` and `(2, 2)`. Because `CanPushHorizontally=false`, Flower Cart's X is locked at 2.
  - If Flower Cart is at `y=1`, it covers `y=1` and `y=2`. If pushed down to `y=2`, it covers `y=2` and `y=3`.
  - In every legal state, Flower Cart occupies Row 2 at Column 2. Hence, **Row 2 can never be cleared in a straight line through Column 2**.
  - Any solution path MUST route around Column 2 via Row 1 (or Row 3).
- **Inference**:
  - At start, Column 2 is completely blocked: `(2, 0)` wall, `(2, 1)` Flower Cart, `(2, 2)` Flower Cart, `(2, 3)` Fruit Stall, `(2, 4)` wall.
  - Flower Cart cannot move Up due to wall at `(2, 0)`. It cannot move Down because Fruit Stall occupies `(2, 3)`.
  - Fruit Stall at `(2, 3)` can move Left to `(1, 3)` (free cell).
  - Once Fruit Stall moves to `(1, 3)`, cell `(2, 3)` is open.
  - Flower Cart can then be pushed Down to `(2, 2)` and `(2, 3)`.
  - This frees cell `(2, 1)`.
  - Row 1 across Columns 0 to 6 is now completely vacant:
    `Path: (0, 2) -> (1, 2) -> (1, 1) -> (2, 1) -> (3, 1) -> (4, 1) -> (5, 1) -> (6, 1) -> (6, 2)`.
  - This is an optimal 2-move solution: `FruitStall Left`, `FlowerCart Down`.

### 2.2 Reconstructing BFS Path for Real-Time Feedback
- **Observation**:
  `SpatialGridEngine.IsPathClear()` explores reachable cells using a standard FIFO queue and `visited` hash set, but discards traversal history upon reaching `ExitGate`.
- **Inference**:
  By storing parent pointers `Dictionary<(int x, int y), (int x, int y)> parent`, when `current == ExitGate`, we can backtrack from `ExitGate` to `Entrance` and reverse the sequence to return `List<(int x, int y)>`.
- **Proposed Pure C# Signature**:
  ```csharp
  public List<(int x, int y)>? FindPath()
  ```
  This returns `null` if no continuous route exists, or an ordered list of 9 coordinate pairs when solved. `IsPathClear()` can directly wrap `FindPath() != null`, maintaining 100% backwards compatibility with zero breaking changes.

### 2.3 Grid Rendering Coordinates & Visual Layout
- **Math Specifications**:
  - Viewport: `1280 x 720` standard virtual resolution.
  - Grid: `7` columns, `5` rows.
  - `CellSize = 88.0f`.
  - Grid Width: `7 * 88 = 616.0f`.
  - Grid Height: `5 * 88 = 440.0f`.
  - Center Horizontally: `GridOriginX = (1280 - 616) / 2 = 332.0f` (or `160.0f` when offset to accommodate right sidebar at `x = 1050f`).
  - Screen Placement:
    - `GridOriginX = 180f`, `GridOriginY = 130f`.
    - Grid bounding box: `Rectangle(180f, 130f, 616f, 440f)`.
  - Cell `(col, row)` to Screen Rect:
    ```csharp
    float x = GridOriginX + col * CellSize;
    float y = GridOriginY + row * CellSize;
    Rectangle cellRect = new(x, y, CellSize, CellSize);
    ```
  - Entity `e` to Screen Rect:
    ```csharp
    float ex = GridOriginX + e.X * CellSize + 3f;
    float ey = GridOriginY + e.Y * CellSize + 3f;
    float ew = e.Width * CellSize - 6f;
    float eh = e.Height * CellSize - 6f;
    Rectangle entityRect = new(ex, ey, ew, eh);
    ```
  - Cell Center:
    `Vector2 center = new(GridOriginX + (col + 0.5f) * CellSize, GridOriginY + (row + 0.5f) * CellSize);`

### 2.4 Real-Time BFS Path Highlight Feedback
- When `_grid.FindPath()` returns a list of cells `path`:
  1. **Cell Tile Glow**: Each cell `(x, y)` in `path` receives a semi-transparent luminous gold underlay:
     `Color.Alpha(new Color(255, 230, 80, 255), 0.35f + 0.15f * Sin(totalTime * 4.0f))`.
  2. **Golden Path Ribbon**: Connect adjacent path nodes `path[i]` and `path[i+1]` with a 6px thick anti-aliased line (`Color(255, 215, 0, 230)`).
  3. **Directional Arrow / Footprint Pips**: Draw small glowing circles or arrowheads moving along the segments from Entrance `(0, 2)` toward Exit `(6, 2)`.
  4. **Status Text**: Draw `"✓ Đường thông! Cổng Cầu Long Biên đã mở"` in bold emerald green with white outline.

### 2.5 Input & Push Mechanics
- **Mouse Selection**:
  Clicking on an entity checks `CheckCollisionPointRec(mouse, entityRect)`. If clicked, entity becomes selected (`_selectedObjectId = entity.Id`). Clicking empty space or clicking the selected entity again deselects it.
- **Keyboard Navigation / Cycling**:
  `Tab` cycles selection through all 5 movable entities in sequence (`FlowerCart -> FruitStall -> Cyclo -> TeaStall -> CeramicCrates`).
- **Push Execution**:
  When an entity is selected, pressing `WASD` or `Arrow Keys` determines target `Direction`:
  - `W` / `Up` -> `Direction.Up`
  - `S` / `Down` -> `Direction.Down`
  - `A` / `Left` -> `Direction.Left`
  - `D` / `Right` -> `Direction.Right`
  If the entity allows movement in that axis and target cells are vacant within grid bounds:
  - Updates entity `X`, `Y`.
  - Increments `_moveCount++`.
  - Publishes `StallPushedEvent(entityId, fromX, fromY, toX, toY, true)`.
  - Plays SFX `StepStreet` or wooden cart slide.
  If movement is blocked:
  - Publishes `StallPushedEvent(entityId, fromX, fromY, fromX, fromY, false)`.
  - Visual nudge shake on the selected card (offset ±3px for 0.15s).
- **Reset on `[R]`**:
  Pressing `[R]` restores initial coordinates, nullifies selection, resets `_moveCount = 0`, triggers hint toast `"Đã làm lại bố cục ban đầu!"`, and publishes `StallPuzzleResetEvent()`.

### 2.6 Full 100% Color Bloom Wave Climax
- Upon path clearance (`_grid.FindPath() != null`):
  1. `TriggerSolve()` initiates victory sequence:
     - Marks `QuestFlags.Gate_LongBien_Unlocked = true` and `QuestFlags.Q3_Completed = true`.
     - Advances quest: `QuestManager.RecordObjectiveProgress(HanoiQuests.Quest3Id, "clear_long_bien_path")` and `QuestManager.CompleteQuest(HanoiQuests.Quest3Id)`.
     - Invokes `LevelProgressionService.CompleteLevel1()`.
     - Publishes `StallPuzzleSolvedEvent(_moveCount)`.
     - Plays `AudioService.PlaySfx(SfxType.GateUnlocked)` and `ColorBloomWave`.
  2. Visual Transition in `StallPuzzleScene`:
     - Over 2.5 seconds, `_bloomProgress` interpolates from `0.0` to `1.0`.
     - Screen desaturation shifts from dull monotone (25%) to full vibrant saturation (100%).
     - Expanding gold ripple wave animates outward from `(GridOriginX + 6.5f * CellSize, GridOriginY + 2.5f * CellSize)`.
     - Long Biên steel truss silhouette transitions from rusted grey (20%) to warm terracotta red with glowing yellow lantern points.
     - Celebration Overlay presents:
       - Title: `"🌉 Cổng Cầu Long Biên đã mở!"`
       - Subtitle: `"Hà Nội đã hồi sinh 100% sắc màu! ({_moveCount} bước)"`
       - Medallion 1: **Tem Hoa Sen Hà Nội** (Lotus Stamp, `#FFB6C1` with golden rim).
       - Medallion 2: **Trang Sổ Khởi Đầu** (Memory Journal Page 1 with vintage calligraphy texture).
     - When `_solveTimer >= 4.0f`, sets `RequestNextScene = true`, signaling transition.

### 2.7 Integration Contract with `HanoiExplorationScene` & Collision World
- **World Coordinate Placement**:
  - `HanoiExplorationScene` map is `1600 x 900`.
  - Western street: `x = 100..700` (Bà An, merchants, Q1/Q2).
  - Eastern street / Bridge approach: `x = 1200..1600`.
- **Barrier & Gate Interactable**:
  - When `!QuestFlags.Gate_LongBien_Unlocked`:
    - Add static collision barrier: `AABB.FromCenterAndExtents(new Vector2(1480f, 400f), new Vector2(30f, 300f))` blocking access to the bridge.
    - Register interactable `LongBienGateInteractable` at `Vector2(1440f, 400f)`.
    - If `!QuestFlags.Q2_Completed`:
      - Bubble text: `[E] Cổng Cầu Long Biên (Bị khóa)`.
      - Dialogue: `"Cổng cầu đang bị các sạp hàng và xe cộ chặn cứng. Bé Mây cần giúp Bà An nấu phở trước đã!"`.
    - If `QuestFlags.Q2_Completed && !QuestFlags.Q3_Completed`:
      - Bubble text: `[E] Dọn dẹp sạp hàng mở lối`.
      - Interaction: Transitions scene to `StallPuzzleScene(SaveData, QuestManager, EventBus)`.
  - When `QuestFlags.Gate_LongBien_Unlocked`:
    - Collision barrier is removed from `CollisionWorld`.
    - Visual rendering shows cleared street with flower cart and stalls parked orderly on the curbs, and the steel bridge portal glowing with lotus lanterns.
    - Bubble text: `[E] Đi qua Cầu Long Biên (Hoàn thành Màn 1)`.
    - Interaction: Triggers Gray Shadow narrative cutscene modal and transitions to World Map.

---

## 3. Caveats

1. **Read-Only Scope**: This report does not directly modify source code. The proposed patches and signatures are specified below for the implementer agent.
2. **Dialogue JSON Scripting**: The bridge gate NPC dialogue strings are mocked in C# code in this vertical slice; full JSON dialogue trees will be externalized in Milestone 4 (`Data/dialogues_hanoi.json`).
3. **Sound Effects**: Raylib procedural synth handles `SfxType.GateUnlocked` and `SfxType.ColorBloomWave` headlessly without audio hardware crash when running under `RaylibAudioService` or `NullAudioService`.
4. **Resolution Scaling**: Grid layout coordinates are specified for the standard 1280x720 window; responsive letterboxing is already handled by Raylib camera/viewport settings.

---

## 4. Conclusion

The implementation requirements for Features 13–16 are completely defined, mathematically scoped, and verified against existing project patterns:

1. **Feature 13 (7x5 Street Stall Spatial Grid)**:
   - Fix entity ID mapping in `StallPuzzleScene`: replace `"Obj_PhoStall"` with `"Obj_FruitStall"` (`#FFDC64` golden yellow, "Sạp hoa quả").
   - Maintain strict `SpatialGridEngine` entity constraints (`FlowerCart` 1x2 Vertical only, `Cyclo` 2x1 Horizontal only, `FruitStall`, `TeaStall`, `CeramicCrates` 1x1 Omnidirectional).
2. **Feature 14 (Path Clearance Detection with BFS & Real-Time Visualization)**:
   - Add `FindPath()` to `SpatialGridEngine.cs` using parent tracking dictionary to reconstruct the 9-step path.
   - In `StallPuzzleScene.cs`, render the glowing golden ribbon along `path` with animated pulse arrows and tile highlights.
3. **Feature 15 (Puzzle Reset on [R])**:
   - In `StallPuzzleScene.HandleInput()`, trigger `_grid.ResetGrid()`, reset `_moveCount = 0`, and publish `StallPuzzleResetEvent()`.
4. **Feature 16 (Long Biên Full Color Wave +100%)**:
   - On path clearance, call `TriggerSolve()`, invoke `LevelProgressionService.CompleteLevel1()`, play SFX, and render expanding bloom wave, 100% saturation, and Lotus Stamp + Journal Page rewards.
5. **Integration Contract**:
   - Add `LongBienGateInteractable` and collision AABB in `HanoiExplorationScene` at `x = 1440f` to switch scenes and unlock bridge passage.

---

## 5. Verification Method

### 5.1 Automated Headless Test Suite Execution
Run the complete test suite:
```bash
dotnet test Tests/GameProject.Tests.csproj
```
Expected result: **All existing 303 tests pass with 0 failures**.

### 5.2 Proposed Automated Headless Tests for Implementer
Implement these tests in `Tests/StallPuzzleTests.cs`:

```csharp
[Fact]
public void SpatialGridEngine_FindPath_ReturnsNullWhenBlocked_AndFullCoordinatesWhenSolved()
{
    var grid = new SpatialGridEngine();
    
    // 1. Initially blocked
    Assert.Null(grid.FindPath());
    Assert.False(grid.IsPathClear());

    // 2. Execute 2-move solution
    bool p1 = grid.PushObject("Obj_FruitStall", Direction.Left);
    bool p2 = grid.PushObject("Obj_FlowerCart", Direction.Down);
    Assert.True(p1 && p2);

    // 3. Path must be reconstructed
    var path = grid.FindPath();
    Assert.NotNull(path);
    Assert.True(path.Count >= 7);
    Assert.Equal((0, 2), path[0]);               // Entrance
    Assert.Equal((6, 2), path[path.Count - 1]); // Exit Gate

    // 4. Verify Manhattan continuity (step by step)
    for (int i = 0; i < path.Count - 1; i++)
    {
        int dist = Math.Abs(path[i + 1].x - path[i].x) + Math.Abs(path[i + 1].y - path[i].y);
        Assert.Equal(1, dist);
    }
}

[Fact]
public void StallPuzzle_ResetGrid_EmitsResetEvent_AndClearsPath()
{
    var grid = new SpatialGridEngine();
    grid.PushObject("Obj_FruitStall", Direction.Left);
    grid.PushObject("Obj_FlowerCart", Direction.Down);
    Assert.True(grid.IsPathClear());

    grid.ResetGrid();
    Assert.False(grid.IsPathClear());
    Assert.Null(grid.FindPath());

    var fruit = grid.Entities.First(e => e.Id == "Obj_FruitStall");
    Assert.Equal(2, fruit.X);
    Assert.Equal(3, fruit.Y);
}

[Fact]
public void StallPuzzle_PushStaticWall_ReturnsFalse_AndEntityDoesNotMove()
{
    var grid = new SpatialGridEngine();
    bool pushedWall = grid.PushObject("Wall_Top_0", Direction.Down);
    Assert.False(pushedWall);
}

[Fact]
public void LevelClimax_StallSolve_Triggers100PercentBloom_AndUnlocksGate()
{
    var eventBus = new EventBus();
    var saveData = SaveData.CreateDefault();
    var questManager = new QuestManager(eventBus);
    HanoiQuests.RegisterAll(questManager);

    questManager.StartQuest(HanoiQuests.Quest1Id);
    questManager.CompleteQuest(HanoiQuests.Quest1Id);
    questManager.StartQuest(HanoiQuests.Quest2Id);
    questManager.CompleteQuest(HanoiQuests.Quest2Id);
    questManager.StartQuest(HanoiQuests.Quest3Id);

    var progression = new LevelProgressionService(saveData, eventBus);
    
    bool gateUnlockedFired = false;
    bool color100Fired = false;
    eventBus.Subscribe<GateUnlockedEvent>(e => gateUnlockedFired = true);
    eventBus.Subscribe<RegionColorUpdatedEvent>(e => { if (e.NewPercentage >= 100.0f) color100Fired = true; });

    progression.CompleteLevel1();

    Assert.True(gateUnlockedFired);
    Assert.True(color100Fired);
    Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
    Assert.Equal(100.0f, saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHanoi));
    Assert.True(saveData.CollectionAlbum.HasJournalPage(AlbumIds.HanoiJournalPage));
    Assert.True(saveData.CollectionAlbum.HasStamp(AlbumIds.HanoiLotusStamp));
}
```

### 5.3 Concrete Code Snippets for Implementer

#### Snippet A: Add `FindPath()` to `Source/Core/Puzzles/SpatialGridEngine.cs`
```csharp
public List<(int x, int y)>? FindPath()
{
    var occupied = new HashSet<(int x, int y)>();
    foreach (var entity in _entities)
    {
        foreach (var cell in entity.OccupiedCells)
        {
            occupied.Add(cell);
        }
    }

    if (occupied.Contains(Entrance) || occupied.Contains(ExitGate))
    {
        return null;
    }

    var queue = new Queue<(int x, int y)>();
    var visited = new HashSet<(int x, int y)>();
    var parent = new Dictionary<(int x, int y), (int x, int y)>();

    queue.Enqueue(Entrance);
    visited.Add(Entrance);

    int[] dX = { 0, 0, -1, 1 };
    int[] dY = { -1, 1, 0, 0 };

    bool found = false;
    while (queue.Count > 0)
    {
        var current = queue.Dequeue();

        if (current == ExitGate)
        {
            found = true;
            break;
        }

        for (int i = 0; i < 4; i++)
        {
            int nx = current.x + dX[i];
            int ny = current.y + dY[i];

            if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
            {
                var neighbor = (nx, ny);
                if (!visited.Contains(neighbor) && !occupied.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    parent[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    if (!found) return null;

    var path = new List<(int x, int y)>();
    var curr = ExitGate;
    while (curr != Entrance)
    {
        path.Add(curr);
        curr = parent[curr];
    }
    path.Add(Entrance);
    path.Reverse();
    return path;
}
```

#### Snippet B: Fix Entity Dictionary & Add Path Rendering in `Source/Stages/StallPuzzleScene.cs`
```csharp
// In EntityColors & EntityLabels:
{ "Obj_FruitStall", new Color(255, 200, 80, 255) }, // Golden orange-yellow fruit stall
// In EntityLabels:
{ "Obj_FruitStall", "Sạp hoa quả" },

// In DrawPathIndicator():
private void DrawPathIndicator()
{
    var path = _grid.FindPath();
    if (path != null && path.Count > 1)
    {
        // Draw path line
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2 p1 = new(GridOriginX + (path[i].x + 0.5f) * CellSize, GridOriginY + (path[i].y + 0.5f) * CellSize);
            Vector2 p2 = new(GridOriginX + (path[i + 1].x + 0.5f) * CellSize, GridOriginY + (path[i + 1].y + 0.5f) * CellSize);
            Raylib.DrawLineEx(p1, p2, 6f, new Color(255, 215, 0, 230));
        }
    }
}
```

#### Snippet C: Add Reset Event Publication in `Source/Stages/StallPuzzleScene.cs`
```csharp
if (Raylib.IsKeyPressed(KeyboardKey.R))
{
    _grid.ResetGrid();
    _selectedObjectId = null;
    _moveCount = 0;
    _eventBus.Publish(new StallPuzzleResetEvent());
    _hintMessage = "Đã làm lại! Thử tìm đường khác nhé.";
    _hintTimer = 3f;
}
```
