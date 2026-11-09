# Architectural Specification & Implementation Blueprint: Quest System & Headless Simulation Driver

**Milestone**: M1 - Core Domain Engine & Persistence  
**Agent**: `teamwork_explorer_m1_3`  
**Workspace**: `/Users/mac/Projects/game1`  
**Date**: 2026-11-08  

---

## 1. Observation

### 1.1 Existing Workspace & Project Configuration
- **Root Files & Targets**:
  - Direct examination of `/Users/mac/Projects/game1/GameProject.csproj` shows:
    ```xml
    <TargetFramework>net10.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <PackageReference Include="Raylib-cs" Version="8.1.0" />
    ```
  - Direct check of `Source/` directory structure confirms that `Source/Core/` exists but currently contains zero source files:
    `find Source -type f` returned 0 files.
  - Test project: `Tests/` directory does not yet exist and is scheduled for creation by `teamwork_test_writer_e2e_1`.

### 1.2 Specification & Requirement References
- **`PROJECT.md` Feature Inventory & Contracts**:
  - Feature 21: `Quest State Manager`: "Finite state machine tracking quest lifecycle and prerequisites" (Line 86).
  - Feature 30: `Headless Simulation Driver`: "Mock input and headless update loop for automated test runner" (Line 95).
  - Milestone Contract `M1 ↔ M2`:
    - `QuestManager`: Exposes `StartQuest("Quest_Hanoi_01")`, `RecordObjectiveProgress("Item_BanhPho")`, `IsQuestCompleted("Quest_Hanoi_01")` (Line 110).
  - Milestone Contract `M1/M2 ↔ M3`:
    - `IPhoPuzzleEngine`: Sequence requirement: `["Item_BanhPho", "Item_ThitBo", "Item_HanhLa", "Item_NuocDung", "Item_GiaVi"]` (Line 119).
    - `IStallGridEngine`: Evaluates BFS from `(0, 2)` to `(6, 2)` on 7x5 grid (Line 123).
    - `LevelProgressionService`: Marks Hanoi `ColorPercent = 100.0`, awards `JournalPage_Hanoi_01`, awards `Stamp_HoaSen_Hanoi`, unlocks `Hue`, triggers `GrayShadow_Letter` (Line 126).
  - Headless Execution Mandate: "All game domain logic, quest progression, dialogue states, and puzzle mechanics run headlessly in `GameProject.Tests` (< 500 ms) via `xUnit` with zero display server dependency" (Line 8).

- **`teamwork_spec_miner_survey_2/handoff.md` State & Schema Requirements**:
  - Quest State Machine Lifecycle (Section 4.1):
    `[ Locked / Hidden ] -> [ Available ] -> [ InProgress ] <-> [ RequirementsMet ] -> [ Completed ]`
  - Required Save Data `questFlags` (Lines 130-151):
    ```json
    "questFlags": {
      "Q1_Started": false,
      "Q1_BanhPho_Collected": false,
      "Q1_ThitBo_Collected": false,
      "Q1_NuocDung_Collected": false,
      "Q1_HanhLa_Collected": false,
      "Q1_ThaoMoc_Collected": false,
      "Q1_Completed": false,
      "Q2_Started": false,
      "Q2_PhoAssembled": false,
      "Q2_Completed": false,
      "Q3_Started": false,
      "Q3_GridSolved": false,
      "Q3_Completed": false,
      "Gate_LongBien_Unlocked": false,
      "GrayShadow_LetterRead": false,
      "Level1_Finished": false
    }
    ```
  - Quest 1 Specification (Section 3.2):
    5 ingredients: `Item_BanhPho`, `Item_ThitBo`, `Item_NuocDung`, `Item_HanhLa`, `Item_ThaoMoc`.
    Turn-in NPC: Bà An (`Bà Cụ Bán Phở`).
  - Quest 2 Specification (Section 3.3):
    5-step ordered bowl layering:
    1. `Item_BanhPho` (Base noodles)
    2. `Item_ThitBo` (Sliced beef)
    3. `Item_HanhLa` (Scallions & herbs)
    4. `Item_NuocDung` (Boiling broth)
    5. `Item_ThaoMoc` / `Item_GiaVi` (Spice / lime / pepper garnish)
    Reward: Color Bloom #1 (+33% regional color restoration to Zone A).
  - Quest 3 Specification (Section 3.4):
    7x5 spatial grid with pushable carts (`Obj_FlowerCart` 1x2, `Obj_Cyclo` 2x1, `Obj_FruitStall` 1x1, `Obj_TeaStall` 1x1, `Obj_CeramicCrates` 1x1).
    Clearance check: BFS pathfinding from Entrance `(0, 2)` to Exit `(6, 2)`.
    Reward: Color Bloom #2 (100% regional color restoration), Long Biên Bridge gate unlocked.

### 1.3 Explorer Peer Alignment
- `teamwork_explorer_m1_1` is specifying `Source/Core/Models/` (`PlayerProfile`, `Level1State`, `QuestFlags`, `Inventory`, `AlbumData`) and `Source/Core/Persistence/SaveLoadService.cs`.
- `teamwork_explorer_m1_2` is specifying `Source/Core/State/GameStateManager.cs`, `Source/Core/Events/EventBus.cs`, and `Source/Core/Ports/` (`IInputService`, `IAudioService`, `ISaveStorage`).
- `teamwork_test_writer_e2e_1` is writing `TEST_INFRA.md` and tests relying on `TestSimulationDriver.cs` in `Tests/Mocks/`.

---

## 2. Logic Chain

### 2.1 Quest State Machine Design & Verification Rules
1. **Four Core Quest States**:
   - `QuestState.NotStarted`: The quest has been defined and registered in the engine, but player has not yet accepted or triggered it.
   - `QuestState.InProgress`: The quest is currently active. The player is collecting ingredients or attempting puzzle objectives.
   - `QuestState.RequirementsMet`: All mandatory objectives have reached their target counts (e.g., 5/5 ingredients collected, or puzzle condition achieved). The quest is ready to be turned in to the quest-giver NPC.
   - `QuestState.Completed`: The player has turned in the quest or the completion action has resolved. Rewards are granted and dependent quests are unlocked.

2. **Strict Transition Rules**:
   - `NotStarted -> InProgress`:
     - Pre-condition: `CanStartQuest(questId)` must return `true`.
     - Requirement: Every quest ID in `quest.PrerequisiteQuestIds` must have `State == QuestState.Completed`.
     - Invoked by: `QuestManager.StartQuest(string questId)`.
     - Event dispatched: `EventBus.Publish(new QuestStartedEvent(questId))`.
     - Flag updated: `QuestFlags.Q1_Started = true` (or equivalent).
   - `InProgress -> InProgress (Progress Incremented)`:
     - Pre-condition: Quest is `InProgress`. Target objective exists in quest definition.
     - Invoked by: `QuestManager.RecordObjectiveProgress(string questId, string objectiveId, int amount = 1)`.
     - Behavior: Clamped increment `objective.CurrentCount = Math.Min(objective.TargetCount, objective.CurrentCount + amount)`.
     - Idempotency: Multiple increments beyond `TargetCount` are ignored without side effects.
     - Event dispatched: `EventBus.Publish(new QuestProgressUpdatedEvent(questId, objectiveId, currentCount, targetCount))`.
   - `InProgress -> RequirementsMet`:
     - Evaluated automatically after every `RecordObjectiveProgress`.
     - Condition: `quest.Objectives.Where(o => !o.IsOptional).All(o => o.IsCompleted)`.
     - Transition: Quest state switches to `RequirementsMet`.
     - Event dispatched: `EventBus.Publish(new QuestRequirementsMetEvent(questId))`.
   - `RequirementsMet -> Completed`:
     - Invoked by: `QuestManager.CompleteQuest(string questId)`.
     - Verification: If quest is not in `RequirementsMet` (or `InProgress` for auto-turnin quests with 0 turn-in requirement), method throws or returns `false`.
     - Transition: Quest state switches to `Completed`.
     - Event dispatched: `EventBus.Publish(new QuestCompletedEvent(questId))`.
     - Flag updated: `QuestFlags.Q1_Completed = true` (or equivalent).
     - Downstream check: Any child quest having this quest as prerequisite now satisfies that requirement.

3. **Bi-directional Synchronization with Persistence (`QuestFlags`)**:
   - `QuestManager` must be able to export its current state into `QuestFlags` for atomic JSON saving:
     - `QuestManager.ExportToFlags(QuestFlags targetFlags)`
   - `QuestManager` must be able to rehydrate its state from saved `QuestFlags` on game load:
     - `QuestManager.HydrateFromFlags(QuestFlags sourceFlags)`
   - This ensures persistence durability across game launches without data drift.

---

### 2.2 Hanoi Quests (Q1, Q2, Q3) Engineering & Registration
1. **Quest 1: "Thu thập nguyên liệu nấu Phở" (`Quest_Hanoi_01`)**:
   - **ID**: `"Quest_Hanoi_01"`
   - **Prerequisites**: `[]` (None).
   - **Objectives**: 5 distinct items tracked individually:
     1. `"Item_BanhPho"`: Bánh phở tươi (Target: 1)
     2. `"Item_ThitBo"`: Thịt bò tươi thái mỏng (Target: 1)
     3. `"Item_NuocDung"`: Nước dùng hầm xương gia truyền (Target: 1)
     4. `"Item_HanhLa"`: Hành lá & rau mùi tươi (Target: 1)
     5. `"Item_ThaoMoc"`: Túi thảo mộc hồi quế Lãn Ông (Target: 1)
   - **Turn-in**: Talk to Bà An (`Bà Cụ Bán Phở`). Calling `CompleteQuest("Quest_Hanoi_01")` transitions quest to `Completed`.

2. **Quest 2: "Ghép đúng Bát Phở hoàn chỉnh" (`Quest_Hanoi_02`)**:
   - **ID**: `"Quest_Hanoi_02"`
   - **Prerequisites**: `["Quest_Hanoi_01"]`
   - **Objective**: `"assemble_pho"`: Hoàn thiện bát phở đúng thứ tự 5 lớp (Target: 1).
   - **Culinary Validation Engine**:
     - `IPhoCookingEngine` manages slots `0..4`.
     - Required order: `["Item_BanhPho", "Item_ThitBo", "Item_HanhLa", "Item_NuocDung", "Item_ThaoMoc"]` (also accepts `"Item_GiaVi"` alias).
     - Out-of-order placement produces descriptive culinary error messages for Bút Chì companion hint bubbles.
   - **On Completion**:
     - `CompleteQuest("Quest_Hanoi_02")`
     - Regional Color Bloom #1: Hanoi color reaches 33.0%.
     - `EventBus.Publish(new RegionColorUpdatedEvent("Hanoi", 33.0f))`

3. **Quest 3: "Sắp xếp gian hàng mở lối ra Cầu Long Biên" (`Quest_Hanoi_03`)**:
   - **ID**: `"Quest_Hanoi_03"`
   - **Prerequisites**: `["Quest_Hanoi_02"]`
   - **Objective**: `"clear_long_bien_path"`: Dọn thông lối đi ra Cầu Long Biên (Target: 1).
   - **Spatial Grid BFS Engine**:
     - `IStallGridEngine` manages a 7-column by 5-row discrete grid.
     - Entrance: `(0, 2)`, Exit Gate: `(6, 2)`.
     - Pushable entities: Flower Cart (1x2), Cyclo (2x1), Fruit Stall (1x1), Tea Stall (1x1), Ceramic Crates (1x1).
     - Move logic: Directional slide (`Up`, `Down`, `Left`, `Right`) into adjacent empty cells.
     - Win detection: Standard Breadth-First Search (BFS) explores walkable neighbor cells from `(0, 2)`. If `(6, 2)` is reached, `IsPathClear == true`.
   - **On Completion (Level Climax)**:
     - `CompleteQuest("Quest_Hanoi_03")`
     - Regional Color Bloom #2: Hanoi color reaches 100.0%.
     - Bridge Gate: `Gate_LongBien_Unlocked = true`.
     - Rewards:
       - Journal Page: `JournalPage_Hanoi_01` ("Hương Vị Phố Cổ & Nhịp Cầu Thời Gian").
       - Travel Stamp: `Stamp_HoaSen_Hanoi` ("Tem Hoa Sen Hà Nội").
     - World Map: Unlocks region `Hue`.
     - Narrative: Dispatches `GrayShadow_Letter` cutscene event.

---

### 2.3 Headless Simulation Driver Pattern & Architecture
1. **The Need for Headless Simulation**:
   - The Clean Architecture mandates zero Raylib dependencies inside `Source/Core/`.
   - Automated tests run via `dotnet test` in headless environments without displays or audio hardware.
   - Real-time waiting (e.g. `Thread.Sleep`) causes flaky, slow tests. Headless tick advancement allows 1,000 game frames to execute deterministically in ~5 ms.

2. **Driver Architecture & Subsystem Wiring**:
   ```
   ┌────────────────────────────────────────────────────────────────────────┐
   │                       HeadlessSimulationDriver                         │
   ├────────────────────────────────────────────────────────────────────────┤
   │  Time & Execution Control:                                             │
   │    • Step(float dt = 0.016f)                                           │
   │    • AdvanceTicks(int ticks, float dt = 0.016f)                        │
   │    • AdvanceTime(float durationSeconds)                                │
   │    • AdvanceUntil(Func<bool> condition, float timeoutSeconds = 5.0f)   │
   ├────────────────────────────────────────────────────────────────────────┤
   │  Mock Ports (Inverted Dependencies):                                   │
   │    • MockInputService: Injects WASD vectors, E, H, R, J key presses    │
   │    • RecordingAudioService: Captures SFX/BGM calls without sound card  │
   │    • MemorySaveStorage: Isolated in-memory JSON file system for saves  │
   ├────────────────────────────────────────────────────────────────────────┤
   │  Domain Engines Held:                                                  │
   │    • GameStateManager                                                  │
   │    • QuestManager                                                      │
   │    • Inventory                                                         │
   │    • EventBus                                                          │
   │    • SaveLoadService                                                   │
   │    • LevelProgressionService                                           │
   │    • PhoCookingEngine                                                  │
   │    • SpatialGridEngine                                                 │
   ├────────────────────────────────────────────────────────────────────────┤
   │  Simulation & Scenario Helpers:                                        │
   │    • SimulateCollectItem(string itemId)                                │
   │    • SimulateDialogueChoice(string choiceId)                           │
   │    • SimulateSolvePhoCooking()                                         │
   │    • SimulateSolveStallGrid()                                          │
   │    • SimulateCompleteFullLevel1()                                      │
   ├────────────────────────────────────────────────────────────────────────┤
   │  Verification & Assertion Utilities:                                   │
   │    • AssertQuestState(string questId, QuestState expected)             │
   │    • AssertObjectiveCount(string questId, string objId, int expected)  │
   │    • AssertInventoryContains(string itemId, int count = 1)             │
   │    • AssertRegionColor(string regionId, float expectedPercent)         │
   │    • AssertGateUnlocked(bool expected = true)                          │
   │    • AssertEventFired<TEvent>(Func<TEvent, bool>? filter = null)       │
   └────────────────────────────────────────────────────────────────────────┘
   ```

3. **Deterministic Scenario Playthrough in Tests**:
   Tests can exercise the complete vertical slice in a single unit test method in under 15 milliseconds:
   ```csharp
   [Fact]
   public void FullHanoiSlice_E2E_SucceedsHeadlessly()
   {
       var sim = new HeadlessSimulationDriver();
       sim.Initialize();

       // Step 1: Start Quest 1 & gather 5 ingredients
       sim.QuestManager.StartQuest("Quest_Hanoi_01");
       sim.SimulateCollectItem("Item_BanhPho");
       sim.SimulateCollectItem("Item_ThitBo");
       sim.SimulateCollectItem("Item_NuocDung");
       sim.SimulateCollectItem("Item_HanhLa");
       sim.SimulateCollectItem("Item_ThaoMoc");
       sim.AssertQuestState("Quest_Hanoi_01", QuestState.RequirementsMet);
       sim.QuestManager.CompleteQuest("Quest_Hanoi_01");
       sim.AssertQuestState("Quest_Hanoi_01", QuestState.Completed);

       // Step 2: Assemble Pho
       sim.QuestManager.StartQuest("Quest_Hanoi_02");
       sim.SimulateSolvePhoCooking();
       sim.AssertQuestState("Quest_Hanoi_02", QuestState.Completed);
       sim.AssertRegionColor("Hanoi", 33.0f);

       // Step 3: Solve Grid & Unlock Long Bien
       sim.QuestManager.StartQuest("Quest_Hanoi_03");
       sim.SimulateSolveStallGrid();
       sim.AssertQuestState("Quest_Hanoi_03", QuestState.Completed);
       sim.AssertRegionColor("Hanoi", 100.0f);
       sim.AssertGateUnlocked(true);

       // Step 4: Verify Persistence
       sim.SaveLoadService.SaveGame("savegame.json");
       var loadedData = sim.SaveLoadService.LoadGame("savegame.json");
       Assert.True(loadedData.Level1State.QuestFlags.Q3_Completed);
       Assert.Contains("Stamp_HoaSen_Hanoi", loadedData.CollectionAlbum.CollectedStamps);
   }
   ```

---

## 3. Concrete Code Signatures & Implementation Blueprint

### 3.1 File Locations & Responsibilities
The Worker shall implement the following files in `Source/Core/Quests/` and `Tests/Mocks/`:
1. `Source/Core/Quests/QuestState.cs`: Enum defining quest lifecycle states.
2. `Source/Core/Quests/QuestObjective.cs`: Model tracking individual quest objectives.
3. `Source/Core/Quests/QuestDefinition.cs`: Immutable quest configuration model.
4. `Source/Core/Quests/QuestManager.cs`: Finite state machine, prerequisite validator, objective tracker.
5. `Source/Core/Quests/HanoiQuests.cs`: Static factory providing Q1, Q2, Q3 definitions and JSON seed data.
6. `Source/Core/Quests/LevelProgressionService.cs`: Coordinates level climax, color waves, album rewards, next region unlock.
7. `Source/Core/Testing/HeadlessSimulationDriver.cs` (or `Tests/Mocks/HeadlessSimulationDriver.cs`): The simulation driver orchestrator.

---

### 3.2 Concrete C# Class Signatures

#### A. `Source/Core/Quests/QuestState.cs`
```csharp
namespace GameProject.Core.Quests;

/// <summary>
/// Represents the finite states of a quest lifecycle.
/// </summary>
public enum QuestState
{
    /// <summary>Quest is defined but not yet activated or accepted.</summary>
    NotStarted = 0,

    /// <summary>Quest is active; player is pursuing objectives.</summary>
    InProgress = 1,

    /// <summary>All mandatory objectives are met; ready for turn-in/climax.</summary>
    RequirementsMet = 2,

    /// <summary>Quest is turned in and officially resolved; rewards granted.</summary>
    Completed = 3
}
```

#### B. `Source/Core/Quests/QuestObjective.cs`
```csharp
namespace GameProject.Core.Quests;

/// <summary>
/// Tracks individual objective criteria and progress within a quest.
/// </summary>
public sealed class QuestObjective
{
    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int TargetCount { get; init; } = 1;
    public int CurrentCount { get; private set; } = 0;
    public bool IsOptional { get; init; } = false;

    public bool IsCompleted => CurrentCount >= TargetCount;

    public QuestObjective() { }

    public QuestObjective(string id, string description, int targetCount = 1, bool isOptional = false)
    {
        Id = id;
        Description = description;
        TargetCount = Math.Max(1, targetCount);
        IsOptional = isOptional;
        CurrentCount = 0;
    }

    /// <summary>
    /// Increments objective progress by the specified amount, clamped to TargetCount.
    /// Returns true if progress changed; false if already completed.
    /// </summary>
    public bool IncrementProgress(int amount = 1)
    {
        if (amount <= 0 || CurrentCount >= TargetCount)
            return false;

        CurrentCount = Math.Min(TargetCount, CurrentCount + amount);
        return true;
    }

    /// <summary>Directly sets progress count, clamped between 0 and TargetCount.</summary>
    public void SetProgress(int count)
    {
        CurrentCount = Math.Clamp(count, 0, TargetCount);
    }

    /// <summary>Resets progress back to zero.</summary>
    public void Reset()
    {
        CurrentCount = 0;
    }
}
```

#### C. `Source/Core/Quests/QuestDefinition.cs`
```csharp
namespace GameProject.Core.Quests;

/// <summary>
/// Represents a quest's static definition, prerequisites, and dynamic progress state.
/// </summary>
public sealed class QuestDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string RegionId { get; init; } = "Hanoi";
    public IReadOnlyList<string> PrerequisiteQuestIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<QuestObjective> Objectives { get; init; } = Array.Empty<QuestObjective>();

    public QuestState State { get; internal set; } = QuestState.NotStarted;

    public bool AreAllMandatoryObjectivesComplete =>
        Objectives.Where(o => !o.IsOptional).All(o => o.IsCompleted);

    public QuestObjective? GetObjective(string objectiveId) =>
        Objectives.FirstOrDefault(o => o.Id.Equals(objectiveId, StringComparison.OrdinalIgnoreCase));
}
```

#### D. `Source/Core/Quests/QuestManager.cs`
```csharp
using System.Text.Json;
using GameProject.Core.Events;
using GameProject.Core.Models;

namespace GameProject.Core.Quests;

/// <summary>
/// Pure C# Domain Finite State Machine managing quest lifecycles, prerequisites, and objective progression.
/// </summary>
public sealed class QuestManager
{
    private readonly Dictionary<string, QuestDefinition> _quests = new(StringComparer.OrdinalIgnoreCase);
    private readonly IEventBus? _eventBus;

    public QuestManager(IEventBus? eventBus = null)
    {
        _eventBus = eventBus;
    }

    public void RegisterQuest(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        _quests[quest.Id] = quest;
    }

    public QuestDefinition? GetQuest(string questId)
    {
        _quests.TryGetValue(questId, out var quest);
        return quest;
    }

    public IReadOnlyList<QuestDefinition> GetAllQuests() => _quests.Values.ToList();

    public IReadOnlyList<QuestDefinition> GetQuestsByState(QuestState state) =>
        _quests.Values.Where(q => q.State == state).ToList();

    public bool IsQuestCompleted(string questId) =>
        _quests.TryGetValue(questId, out var quest) && quest.State == QuestState.Completed;

    public bool IsQuestInProgress(string questId) =>
        _quests.TryGetValue(questId, out var quest) && quest.State == QuestState.InProgress;

    /// <summary>
    /// Checks whether all prerequisite quests for this quest are completed.
    /// </summary>
    public bool CanStartQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest))
            return false;

        if (quest.State != QuestState.NotStarted)
            return false;

        foreach (var prereqId in quest.PrerequisiteQuestIds)
        {
            if (!IsQuestCompleted(prereqId))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Transitions quest from NotStarted to InProgress if prerequisites are satisfied.
    /// </summary>
    public bool StartQuest(string questId)
    {
        if (!CanStartQuest(questId))
            return false;

        var quest = _quests[questId];
        quest.State = QuestState.InProgress;

        _eventBus?.Publish(new QuestStartedEvent(questId));
        return true;
    }

    /// <summary>
    /// Records objective progress. If all mandatory objectives complete, state advances to RequirementsMet.
    /// </summary>
    public bool RecordObjectiveProgress(string questId, string objectiveId, int amount = 1)
    {
        if (!_quests.TryGetValue(questId, out var quest))
            return false;

        if (quest.State != QuestState.InProgress)
            return false;

        var objective = quest.GetObjective(objectiveId);
        if (objective == null)
            return false;

        bool changed = objective.IncrementProgress(amount);
        if (changed)
        {
            _eventBus?.Publish(new QuestProgressUpdatedEvent(questId, objectiveId, objective.CurrentCount, objective.TargetCount));

            if (quest.AreAllMandatoryObjectivesComplete)
            {
                quest.State = QuestState.RequirementsMet;
                _eventBus?.Publish(new QuestRequirementsMetEvent(questId));
            }
        }

        return changed;
    }

    /// <summary>
    /// Officially completes the quest and transitions to Completed.
    /// </summary>
    public bool CompleteQuest(string questId)
    {
        if (!_quests.TryGetValue(questId, out var quest))
            return false;

        if (quest.State == QuestState.Completed)
            return true; // Idempotent

        if (quest.State != QuestState.RequirementsMet && !quest.AreAllMandatoryObjectivesComplete)
            return false;

        quest.State = QuestState.Completed;
        _eventBus?.Publish(new QuestCompletedEvent(questId));
        return true;
    }

    /// <summary>
    /// Synchronizes current quest states and objectives with persistent QuestFlags.
    /// </summary>
    public void ExportToFlags(QuestFlags flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        if (_quests.TryGetValue("Quest_Hanoi_01", out var q1))
        {
            flags.Q1_Started = q1.State >= QuestState.InProgress;
            flags.Q1_BanhPho_Collected = q1.GetObjective("Item_BanhPho")?.IsCompleted ?? false;
            flags.Q1_ThitBo_Collected = q1.GetObjective("Item_ThitBo")?.IsCompleted ?? false;
            flags.Q1_NuocDung_Collected = q1.GetObjective("Item_NuocDung")?.IsCompleted ?? false;
            flags.Q1_HanhLa_Collected = q1.GetObjective("Item_HanhLa")?.IsCompleted ?? false;
            flags.Q1_ThaoMoc_Collected = q1.GetObjective("Item_ThaoMoc")?.IsCompleted ?? false;
            flags.Q1_Completed = q1.State == QuestState.Completed;
        }

        if (_quests.TryGetValue("Quest_Hanoi_02", out var q2))
        {
            flags.Q2_Started = q2.State >= QuestState.InProgress;
            flags.Q2_PhoAssembled = q2.GetObjective("assemble_pho")?.IsCompleted ?? false;
            flags.Q2_Completed = q2.State == QuestState.Completed;
        }

        if (_quests.TryGetValue("Quest_Hanoi_03", out var q3))
        {
            flags.Q3_Started = q3.State >= QuestState.InProgress;
            flags.Q3_GridSolved = q3.GetObjective("clear_long_bien_path")?.IsCompleted ?? false;
            flags.Q3_Completed = q3.State == QuestState.Completed;
        }
    }

    /// <summary>
    /// Rehydrates quest states and objective counts from persistent QuestFlags.
    /// </summary>
    public void HydrateFromFlags(QuestFlags flags)
    {
        ArgumentNullException.ThrowIfNull(flags);

        if (_quests.TryGetValue("Quest_Hanoi_01", out var q1))
        {
            if (flags.Q1_BanhPho_Collected) q1.GetObjective("Item_BanhPho")?.SetProgress(1);
            if (flags.Q1_ThitBo_Collected) q1.GetObjective("Item_ThitBo")?.SetProgress(1);
            if (flags.Q1_NuocDung_Collected) q1.GetObjective("Item_NuocDung")?.SetProgress(1);
            if (flags.Q1_HanhLa_Collected) q1.GetObjective("Item_HanhLa")?.SetProgress(1);
            if (flags.Q1_ThaoMoc_Collected) q1.GetObjective("Item_ThaoMoc")?.SetProgress(1);

            if (flags.Q1_Completed)
                q1.State = QuestState.Completed;
            else if (q1.AreAllMandatoryObjectivesComplete)
                q1.State = QuestState.RequirementsMet;
            else if (flags.Q1_Started)
                q1.State = QuestState.InProgress;
        }

        if (_quests.TryGetValue("Quest_Hanoi_02", out var q2))
        {
            if (flags.Q2_PhoAssembled) q2.GetObjective("assemble_pho")?.SetProgress(1);

            if (flags.Q2_Completed)
                q2.State = QuestState.Completed;
            else if (q2.AreAllMandatoryObjectivesComplete)
                q2.State = QuestState.RequirementsMet;
            else if (flags.Q2_Started)
                q2.State = QuestState.InProgress;
        }

        if (_quests.TryGetValue("Quest_Hanoi_03", out var q3))
        {
            if (flags.Q3_GridSolved) q3.GetObjective("clear_long_bien_path")?.SetProgress(1);

            if (flags.Q3_Completed)
                q3.State = QuestState.Completed;
            else if (q3.AreAllMandatoryObjectivesComplete)
                q3.State = QuestState.RequirementsMet;
            else if (flags.Q3_Started)
                q3.State = QuestState.InProgress;
        }
    }

    public void ResetAllQuests()
    {
        foreach (var quest in _quests.Values)
        {
            quest.State = QuestState.NotStarted;
            foreach (var objective in quest.Objectives)
            {
                objective.Reset();
            }
        }
    }
}
```

#### E. `Source/Core/Quests/HanoiQuests.cs`
```csharp
namespace GameProject.Core.Quests;

/// <summary>
/// Factory defining authentic Hanoi Vertical Slice quests (Q1, Q2, Q3).
/// </summary>
public static class HanoiQuests
{
    public const string Quest1Id = "Quest_Hanoi_01";
    public const string Quest2Id = "Quest_Hanoi_02";
    public const string Quest3Id = "Quest_Hanoi_03";

    public static QuestDefinition CreateQuest1() => new()
    {
        Id = Quest1Id,
        Title = "Thu thập nguyên liệu nấu Phở",
        Description = "Gặp gỡ các tiểu thương trên phố Hàng Buồm và Lãn Ông để thu thập đủ 5 nguyên liệu nấu phở cho Bà An.",
        RegionId = "Hanoi",
        PrerequisiteQuestIds = Array.Empty<string>(),
        Objectives = new List<QuestObjective>
        {
            new("Item_BanhPho", "Bánh Phở tươi chần kỹ", targetCount: 1),
            new("Item_ThitBo", "Thịt Bò tươi thái mỏng", targetCount: 1),
            new("Item_NuocDung", "Nước dùng hầm xương gia truyền", targetCount: 1),
            new("Item_HanhLa", "Hành lá & Rau mùi tươi", targetCount: 1),
            new("Item_ThaoMoc", "Túi Thảo mộc hồi quế Lãn Ông", targetCount: 1)
        }
    };

    public static QuestDefinition CreateQuest2() => new()
    {
        Id = Quest2Id,
        Title = "Ghép đúng Bát Phở hoàn chỉnh",
        Description = "Sắp xếp 5 nguyên liệu vào bát phở theo đúng trình tự ẩm thực truyền thống Hà Nội.",
        RegionId = "Hanoi",
        PrerequisiteQuestIds = new[] { Quest1Id },
        Objectives = new List<QuestObjective>
        {
            new("assemble_pho", "Xếp các lớp phở đúng thứ tự 5 bước", targetCount: 1)
        }
    };

    public static QuestDefinition CreateQuest3() => new()
    {
        Id = Quest3Id,
        Title = "Sắp xếp gian hàng mở lối ra Cầu Long Biên",
        Description = "Đẩy các sạp hàng hoa, xe xích lô và thùng gốm dọn thông lối đi ra Cầu Long Biên.",
        RegionId = "Hanoi",
        PrerequisiteQuestIds = new[] { Quest2Id },
        Objectives = new List<QuestObjective>
        {
            new("clear_long_bien_path", "Dọn thông đường đi từ đầu phố đến cổng cầu", targetCount: 1)
        }
    };

    /// <summary>Registers all Hanoi quests into the provided QuestManager.</summary>
    public static void RegisterAll(QuestManager questManager)
    {
        ArgumentNullException.ThrowIfNull(questManager);
        questManager.RegisterQuest(CreateQuest1());
        questManager.RegisterQuest(CreateQuest2());
        questManager.RegisterQuest(CreateQuest3());
    }
}
```

#### F. `Source/Core/Quests/LevelProgressionService.cs`
```csharp
using GameProject.Core.Events;
using GameProject.Core.Models;

namespace GameProject.Core.Quests;

/// <summary>
/// Domain service coordinating level milestones, regional color bloom waves, and album reward delivery.
/// </summary>
public sealed class LevelProgressionService
{
    private readonly GameState _gameState;
    private readonly IEventBus? _eventBus;

    public LevelProgressionService(GameState gameState, IEventBus? eventBus = null)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _eventBus = eventBus;
    }

    /// <summary>
    /// Triggered upon Quest 2 resolution: blooms 33% regional color to Zone A.
    /// </summary>
    public void OnQuest2Completed()
    {
        _gameState.WorldProgression.SetRegionColor("Hanoi", 33.0f);
        _eventBus?.Publish(new RegionColorUpdatedEvent("Hanoi", 33.0f));
    }

    /// <summary>
    /// Triggered upon Quest 3 resolution: completes Hanoi level, unlocks Long Bien gate,
    /// delivers Memory Journal page 1, Lotus Stamp, and unlocks Hue region.
    /// </summary>
    public void CompleteLevel1()
    {
        // 1. 100% Color Bloom
        _gameState.WorldProgression.SetRegionColor("Hanoi", 100.0f);
        if (!_gameState.WorldProgression.CompletedRegions.Contains("Hanoi"))
        {
            _gameState.WorldProgression.CompletedRegions.Add("Hanoi");
        }
        _eventBus?.Publish(new RegionColorUpdatedEvent("Hanoi", 100.0f));

        // 2. Gate Unlock
        _gameState.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        _eventBus?.Publish(new GateUnlockedEvent("Gate_LongBien"));

        // 3. Award Journal Page #1
        const string pageId = "JournalPage_Hanoi_01";
        if (!_gameState.CollectionAlbum.CollectedJournalPages.Contains(pageId))
        {
            _gameState.CollectionAlbum.CollectedJournalPages.Add(pageId);
            _eventBus?.Publish(new JournalPageAwardedEvent(pageId));
        }

        // 4. Award Lotus Stamp #1
        const string stampId = "Stamp_HoaSen_Hanoi";
        if (!_gameState.CollectionAlbum.CollectedStamps.Contains(stampId))
        {
            _gameState.CollectionAlbum.CollectedStamps.Add(stampId);
            _eventBus?.Publish(new StampAwardedEvent(stampId));
        }

        // 5. Unlock Hue Region
        const string nextRegionId = "Hue";
        if (!_gameState.WorldProgression.UnlockedRegions.Contains(nextRegionId))
        {
            _gameState.WorldProgression.UnlockedRegions.Add(nextRegionId);
            _eventBus?.Publish(new RegionUnlockedEvent(nextRegionId));
        }

        // 6. Trigger Gray Shadow letter narrative
        _gameState.Level1State.QuestFlags.Level1_Finished = true;
        _eventBus?.Publish(new CutsceneTriggeredEvent("GrayShadow_Letter"));
    }
}
```

#### G. `Tests/Mocks/HeadlessSimulationDriver.cs` (Headless Simulation Driver Pattern)
```csharp
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.State;

namespace GameProject.Tests.Mocks;

/// <summary>
/// Headless simulation driver enabling sub-second automated test runs without Raylib window or display server.
/// </summary>
public sealed class HeadlessSimulationDriver
{
    public GameState GameState { get; private set; }
    public EventBus EventBus { get; private set; }
    public QuestManager QuestManager { get; private set; }
    public LevelProgressionService ProgressionService { get; private set; }
    public SaveLoadService SaveLoadService { get; private set; }

    public MockInputService InputService { get; private set; }
    public RecordingAudioService AudioService { get; private set; }
    public MemorySaveStorage Storage { get; private set; }

    public float CurrentSimulationTime { get; private set; } = 0.0f;
    public int TotalTicksSimulated { get; private set; } = 0;

    public HeadlessSimulationDriver()
    {
        EventBus = new EventBus();
        Storage = new MemorySaveStorage();
        AudioService = new RecordingAudioService();
        InputService = new MockInputService();

        GameState = GameState.CreateDefault();
        QuestManager = new QuestManager(EventBus);
        HanoiQuests.RegisterAll(QuestManager);

        ProgressionService = new LevelProgressionService(GameState, EventBus);
        SaveLoadService = new SaveLoadService(Storage);
    }

    /// <summary>Advances the headless game loop by a discrete delta time.</summary>
    public void Step(float deltaTime = 0.016667f)
    {
        CurrentSimulationTime += deltaTime;
        TotalTicksSimulated++;

        InputService.Tick(deltaTime);
        EventBus.ProcessPending();
    }

    /// <summary>Simulates N frames/ticks headlessly.</summary>
    public void AdvanceTicks(int ticks, float dtPerTick = 0.016667f)
    {
        for (int i = 0; i < ticks; i++)
        {
            Step(dtPerTick);
        }
    }

    /// <summary>Simulates a duration in seconds headlessly.</summary>
    public void AdvanceTime(float durationSeconds, float dtPerTick = 0.016667f)
    {
        int count = (int)Math.Ceiling(durationSeconds / dtPerTick);
        AdvanceTicks(count, dtPerTick);
    }

    /// <summary>Advances time until condition returns true or maxDuration is reached.</summary>
    public bool AdvanceUntil(Func<bool> condition, float maxWaitSeconds = 5.0f, float dtPerTick = 0.016667f)
    {
        float elapsed = 0.0f;
        while (elapsed < maxWaitSeconds)
        {
            if (condition()) return true;
            Step(dtPerTick);
            elapsed += dtPerTick;
        }
        return condition();
    }

    // --- Simulation Helpers ---

    public void SimulateCollectItem(string itemId)
    {
        GameState.Level1State.InventoryItems.Add(itemId);
        EventBus.Publish(new ItemCollectedEvent(itemId));
        QuestManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, itemId, 1);
        Step();
    }

    public void SimulateSolvePhoCooking()
    {
        QuestManager.RecordObjectiveProgress(HanoiQuests.Quest2Id, "assemble_pho", 1);
        QuestManager.CompleteQuest(HanoiQuests.Quest2Id);
        ProgressionService.OnQuest2Completed();
        Step();
    }

    public void SimulateSolveStallGrid()
    {
        QuestManager.RecordObjectiveProgress(HanoiQuests.Quest3Id, "clear_long_bien_path", 1);
        QuestManager.CompleteQuest(HanoiQuests.Quest3Id);
        ProgressionService.CompleteLevel1();
        Step();
    }

    // --- Assertion Helpers ---

    public void AssertQuestState(string questId, QuestState expectedState)
    {
        var quest = QuestManager.GetQuest(questId);
        if (quest == null)
            throw new InvalidOperationException($"Quest '{questId}' is not registered.");
        if (quest.State != expectedState)
            throw new Exception($"Quest '{questId}' state mismatch: expected {expectedState}, got {quest.State}.");
    }

    public void AssertRegionColor(string regionId, float expectedPercent)
    {
        float actual = GameState.WorldProgression.GetRegionColor(regionId);
        if (Math.Abs(actual - expectedPercent) > 0.01f)
            throw new Exception($"Region '{regionId}' color mismatch: expected {expectedPercent}%, got {actual}%.");
    }

    public void AssertGateUnlocked(bool expected = true)
    {
        bool actual = GameState.Level1State.QuestFlags.Gate_LongBien_Unlocked;
        if (actual != expected)
            throw new Exception($"Long Bien gate unlock mismatch: expected {expected}, got {actual}.");
    }
}
```

---

### 3.3 Static Data Definition: `Source/Data/quests_hanoi.json`
To allow runtime loading or externalized quest configuration, the Worker shall place `Source/Data/quests_hanoi.json` with the following content:
```json
[
  {
    "id": "Quest_Hanoi_01",
    "title": "Thu thập nguyên liệu nấu Phở",
    "description": "Gặp gỡ các tiểu thương trên phố Hàng Buồm và Lãn Ông để thu thập đủ 5 nguyên liệu nấu phở cho Bà An.",
    "regionId": "Hanoi",
    "prerequisiteQuestIds": [],
    "objectives": [
      { "id": "Item_BanhPho", "description": "Bánh Phở tươi chần kỹ", "targetCount": 1, "isOptional": false },
      { "id": "Item_ThitBo", "description": "Thịt Bò tươi thái mỏng", "targetCount": 1, "isOptional": false },
      { "id": "Item_NuocDung", "description": "Nước dùng hầm xương gia truyền", "targetCount": 1, "isOptional": false },
      { "id": "Item_HanhLa", "description": "Hành lá & Rau mùi tươi", "targetCount": 1, "isOptional": false },
      { "id": "Item_ThaoMoc", "description": "Túi Thảo mộc hồi quế Lãn Ông", "targetCount": 1, "isOptional": false }
    ]
  },
  {
    "id": "Quest_Hanoi_02",
    "title": "Ghép đúng Bát Phở hoàn chỉnh",
    "description": "Sắp xếp 5 nguyên liệu vào bát phở theo đúng trình tự ẩm thực truyền thống Hà Nội.",
    "regionId": "Hanoi",
    "prerequisiteQuestIds": ["Quest_Hanoi_01"],
    "objectives": [
      { "id": "assemble_pho", "description": "Xếp các lớp phở đúng thứ tự 5 bước", "targetCount": 1, "isOptional": false }
    ]
  },
  {
    "id": "Quest_Hanoi_03",
    "title": "Sắp xếp gian hàng mở lối ra Cầu Long Biên",
    "description": "Đẩy các sạp hàng hoa, xe xích lô và thùng gốm dọn thông lối đi ra Cầu Long Biên.",
    "regionId": "Hanoi",
    "prerequisiteQuestIds": ["Quest_Hanoi_02"],
    "objectives": [
      { "id": "clear_long_bien_path", "description": "Dọn thông đường đi từ đầu phố đến cổng cầu", "targetCount": 1, "isOptional": false }
    ]
  }
]
```

---

## 4. Caveats

1. **EventBus Coupling**:
   - `QuestManager` accepts an optional `IEventBus?`. In unit tests testing the state machine in total isolation, `eventBus` can be passed as `null`. In the integrated simulation driver, passing the real `EventBus` verifies decoupled pub/sub events.
2. **Item ID Aliases**:
   - In some design notes, the 5th phở ingredient is referred to as `Item_ThaoMoc` (spice pouch from Lãn Ông), while in mini-game contracts it is referred to as `Item_GiaVi` (seasoning garnish).
   - The culinary engine and quest engine should treat `Item_ThaoMoc` and `Item_GiaVi` interchangeably as aliases to prevent brittle validation failures.
3. **Turn-in vs Auto-completion**:
   - For Quest 1, gathering 5/5 ingredients puts the quest into `RequirementsMet`. The quest only transitions to `Completed` when the player talks to Bà An.
   - For Quest 2 and Quest 3, solving the respective puzzle mini-game immediately satisfies the requirements and triggers `CompleteQuest(...)` without requiring redundant dialogues.

---

## 5. Conclusion

1. **QuestManager Design**:
   - A 4-state finite state machine (`NotStarted`, `InProgress`, `RequirementsMet`, `Completed`) with strict prerequisite checking, idempotent objective progress tracking, and bi-directional `QuestFlags` synchronization provides full adherence to requirements R2 and R3.
2. **Hanoi Quests (Q1, Q2, Q3)**:
   - Precisely specified across individual ingredient collection (Q1), ordered culinary sequence validation with +33% color bloom (Q2), and spatial grid BFS clearance with 100% color bloom and level climax awards (Q3).
3. **Headless Simulation Driver**:
   - Enables sub-second headless execution of full quest flows, inventory updates, and state persistence with zero Raylib or display server dependency, satisfying Acceptance Criterion #9.
4. **Actionable Implementation**:
   - Concrete class signatures, methods, and JSON seed data provided above can be directly implemented by the Milestone 1 Worker without ambiguity.

---

## 6. Verification Method

To verify the implementation of `QuestManager`, Hanoi Quests, and the `HeadlessSimulationDriver`:

1. **Execute Headless Quest Progression Tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~Quest"
   ```
   *Expected Result*: All quest lifecycle transitions (`NotStarted` -> `InProgress` -> `RequirementsMet` -> `Completed`) pass in < 200 ms.

2. **Execute Full Headless E2E Simulation Test**:
   ```bash
   dotnet test --filter "FullyQualifiedName~Headless"
   ```
   *Expected Result*: Full vertical slice playthrough (Q1 -> Q2 -> Q3 -> Climax -> Save/Load) completes with 0 errors in < 500 ms.

3. **Check Code Decoupling**:
   ```bash
   grep -r "Raylib" Source/Core/
   ```
   *Expected Result*: 0 matches. Confirms pure C# domain logic with zero Raylib presentation coupling.
