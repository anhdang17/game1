# Handoff Report: Milestone 2 Remediation 1 (QuestManager / SaveData Desync & Soft-Lock)

**Author**: `teamwork_explorer_m2_remed_1`  
**Milestone**: Milestone 2 Remediation (Iteration 2)  
**Role**: Remediation Explorer (QuestManager / SaveData Desync & Soft-Lock)  
**Target Files**:
- `Source/Stages/HanoiExplorationScene.cs`
- `Source/Core/Interactions/HanoiInteractables.cs`
- `Tests/Challenger2EmpiricalTests.cs`  
**Date**: 2026-11-09T00:40:00Z  
**Verdict**: **REMEDIATION_READY**

---

## 1. Observation

1. **Scene Constructor Omission of Hydration (`Source/Stages/HanoiExplorationScene.cs` lines 73-77)**:
   ```csharp
   QuestManager = questManager ?? new QuestManager(EventBus);
   if (questManager == null)
   {
       HanoiQuests.RegisterAll(QuestManager);
   }
   ```
   `HanoiExplorationScene` initializes `QuestManager` and registers `HanoiQuests` when `questManager` is null, but **never invokes `QuestManager.HydrateFromFlags(QuestFlags)`**.
   When loaded with an existing `SaveData` where `QuestFlags.Q1_Started = true` or where ingredients were gathered (`QuestFlags.Q1_BanhPho_Collected = true`, etc.), `QuestManager` starts with all quests in `QuestState.NotStarted` and all objective counters at 0.

2. **Unchecked Completion & Unconditional Flag Mutation in Bà An (`Source/Core/Interactions/HanoiInteractables.cs` lines 58-69)**:
   ```csharp
   // State 2: Requirements Met -> Complete Quest 1
   if (context.QuestFlags.AreAllQ1IngredientsCollected() && !context.QuestFlags.Q1_Completed)
   {
       context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id);
       context.QuestFlags.Q1_Completed = true;
       context.AudioService.PlaySfx(SfxType.PhoCookSuccess);

       return new InteractionResult(
           Success: true,
           DialogueSpeaker: DisplayName,
           DialogueText: "Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu hảo hạng rồi: Bánh phở dẻo mềm, thịt bò tươi, nước dùng thơm ngát, hành hoa và thảo mộc Lãn Ông. Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh nhé!",
           QuestCompleted: true
       );
   }
   ```
   `BaAnNpc.Interact` checks only `context.QuestFlags.AreAllQ1IngredientsCollected()`. It invokes `context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id)` without inspecting the boolean return value.
   In `Source/Core/Quests/QuestManager.cs` lines 141-142:
   ```csharp
   if (quest.State != QuestState.RequirementsMet && !quest.AreAllMandatoryObjectivesComplete)
       return false;
   ```
   If `QuestManager` was not previously hydrated or progressed (e.g., loaded save data or flags set directly), `CompleteQuest` returns `false` and leaves `quest.State` at `NotStarted` or `InProgress`.
   However, `BaAnNpc` unconditionally assigns `context.QuestFlags.Q1_Completed = true;` and transitions permanently to State 3 (lines 73-80):
   ```csharp
   // State 3: Quest 1 Completed
   if (context.QuestFlags.Q1_Completed)
   {
       return new InteractionResult(
           Success: true,
           DialogueSpeaker: DisplayName,
           DialogueText: "Bà cháu mình cùng vào bàn ghép bát phở chuẩn vị Hà Nội nào cháu yêu!"
       );
   }
   ```

3. **Merchant Progression Dependency on `QuestState.InProgress` (`Source/Core/Interactions/HanoiInteractables.cs` line 166 & `Source/Core/Quests/QuestManager.cs` lines 92-93)**:
   In `PhoIngredientMerchant.Interact`:
   ```csharp
   context.QuestManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemId, 1);
   ```
   In `QuestManager.RecordObjectiveProgress`:
   ```csharp
   if (quest.State != QuestState.InProgress)
       return false;
   ```
   If a save game is loaded where `flags.Q1_Started = true` but `QuestManager` was not hydrated into `QuestState.InProgress`, every merchant interaction silently returns `false` from `RecordObjectiveProgress`, granting items to `Inventory` and setting flags in `QuestFlags` while leaving `QuestManager`'s internal objective progress permanently at 0.

4. **Empirical Challenger Proof Tests Characterize Failure (`Tests/Challenger2EmpiricalTests.cs` lines 627-670)**:
   - Line 627 (`Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock`):
     Asserts that when flags are set directly, `qm.IsQuestCompleted(HanoiQuests.Quest1Id)` is `false` and `qm.CanStartQuest(HanoiQuests.Quest2Id)` is `false`, demonstrating the soft-lock.
   - Line 657 (`Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData`):
     Asserts that after `new HanoiExplorationScene(saveData)`, `scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id) == QuestState.NotStarted` and objective count is 0.
   Both tests were written to confirm the vulnerability. Once remediated, these tests must assert the corrected, synchronized behavior.

---

## 2. Logic Chain

1. **Root Cause Analysis — Save/Load Desync**:
   - `SaveData.Level1State.QuestFlags` stores the authoritative serializable quest state across sessions.
   - Domain class `QuestManager` maintains runtime state machine logic (`QuestState.NotStarted` -> `InProgress` -> `RequirementsMet` -> `Completed`) and prerequisite gating (`CanStartQuest`).
   - `QuestManager` already provides `HydrateFromFlags(QuestFlags flags)` (lines 186-233) specifically designed to restore objectives and state from `QuestFlags`.
   - By omitting `QuestManager.HydrateFromFlags(QuestFlags)` in `HanoiExplorationScene`'s constructor, `QuestManager` was decoupled from loaded save state.

2. **Root Cause Analysis — Turn-in Soft-lock**:
   - When all ingredients are gathered, the player returns to Bà An.
   - If `QuestManager` was not in sync (from an unhydrated load or directly modified flags), `quest.State` is not `RequirementsMet` and its internal counters are 0.
   - `QuestManager.CompleteQuest(...)` rejects the transition and returns `false`.
   - `BaAnNpc.Interact` ignores this rejection and forcefully overwrites `context.QuestFlags.Q1_Completed = true`.
   - Subsequent calls bypass State 2 because `QuestFlags.Q1_Completed` is now `true`, permanently locking out re-attempts.
   - `QuestManager.CanStartQuest("Quest_Hanoi_02")` checks `IsQuestCompleted("Quest_Hanoi_01")`, which evaluates `quest.State == QuestState.Completed` (false).
   - As a result, Quest 2 can never start. The vertical slice progression is irrevocably soft-locked.

3. **Remediation Strategy (Clean Architecture & Ponytail "Least Code" Principle)**:
   - **Step 1 (`HanoiExplorationScene.cs`)**: In the scene constructor, after ensuring `QuestManager` has quests registered, invoke `QuestManager.HydrateFromFlags(QuestFlags)`. (Cost: 2 lines).
   - **Step 2 (`HanoiInteractables.cs` — `BaAnNpc.Interact`)**:
     1. Guarantee `HanoiQuests.RegisterAll(context.QuestManager)` if missing.
     2. In State 2, before completing, call `context.QuestManager.HydrateFromFlags(context.QuestFlags);` to guarantee that all objective counts and `QuestState.RequirementsMet` are synchronized from flags.
     3. Wrap flag mutation: only set `context.QuestFlags.Q1_Completed = true` if `context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id)` returns `true`. (Cost: 6 lines).
   - **Step 3 (`HanoiInteractables.cs` — `PhoIngredientMerchant.Interact`)**:
     Ensure `context.QuestManager.HydrateFromFlags(context.QuestFlags)` if `QuestState` is still `NotStarted` despite `Q1_Started = true`. This prevents merchant interaction objective progress from being dropped. (Cost: 4 lines).
   - **Step 4 (`Tests/Challenger2EmpiricalTests.cs`)**:
     Update assertions in `Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock` and `Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData` to assert the resolved, synchronized state (`Assert.True(qm.IsQuestCompleted(...))` and `Assert.Equal(QuestState.InProgress, ...)`).

---

## 3. Caveats

1. **Unrelated Compiler Warnings / Errors in Future Milestone Staged Files**:
   - `Source/Stages/WorldMapScene.cs(126,48)` has an ambiguous call `new Color(r, g, b, 255)` (`(byte, byte, byte, byte)` vs `(int, int, int, int)`). Fix: cast `(byte)255` or cast `(int)r, (int)g, (int)b, 255`.
   - `Source/Stages/AlbumViewerScene.cs(219, 223)` has inverted argument positions in `Raylib.DrawRectangleRounded(rect, 0.1f, color, segments)` where Raylib-cs 8.1.0 expects `(Rectangle rec, float roundness, int segments, Color color)`.
   - These files belong to Milestone 4 / future milestones and were not modified during this investigation due to read-only constraints. The implementing worker should apply one-line parameter fixes to these files when restoring the full build.

2. **No Model Schema Changes**:
   - `SaveData`, `Level1State`, `QuestFlags`, and `QuestDefinition` schemas remain 100% unchanged. No JSON serialization migrations or version bumps are necessary.

---

## 4. Conclusion & Exact Code Replacements

### Exact Drop-In Replacement 1: `Source/Stages/HanoiExplorationScene.cs`
**Location**: `Source/Stages/HanoiExplorationScene.cs`, lines 73-77

#### Current Code:
```csharp
        QuestManager = questManager ?? new QuestManager(EventBus);
        if (questManager == null)
        {
            HanoiQuests.RegisterAll(QuestManager);
        }
```

#### Proposed Replacement:
```csharp
        QuestManager = questManager ?? new QuestManager(EventBus);
        if (questManager == null || QuestManager.GetQuest(HanoiQuests.Quest1Id) == null)
        {
            HanoiQuests.RegisterAll(QuestManager);
        }
        QuestManager.HydrateFromFlags(QuestFlags);
```

---

### Exact Drop-In Replacement 2: `Source/Core/Interactions/HanoiInteractables.cs` (`BaAnNpc.Interact`)
**Location**: `Source/Core/Interactions/HanoiInteractables.cs`, lines 40-70

#### Current Code:
```csharp
    public InteractionResult Interact(InteractionContext context)
    {
        // State 1: Quest 1 Not Started
        if (!context.QuestFlags.Q1_Started)
        {
            context.QuestManager.StartQuest(HanoiQuests.Quest1Id);
            context.QuestFlags.Q1_Started = true;
            context.AudioService.PlaySfx(SfxType.ButtonClick);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Chào bé Mây! Hôm nay bà chuẩn bị nấu nồi phở gia truyền nhưng thiếu mất mấy nguyên liệu ngon nhất. Cháu giúp bà ghé các quầy hàng trong phố cổ xin đủ 5 món: Bánh phở tươi, Thịt bò mềm, Nước dùng ninh xương, Hành lá rau mùi và Gói thảo mộc thơm lừng nhé!",
                QuestStarted: true
            );
        }

        // State 2: Requirements Met -> Complete Quest 1
        if (context.QuestFlags.AreAllQ1IngredientsCollected() && !context.QuestFlags.Q1_Completed)
        {
            context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id);
            context.QuestFlags.Q1_Completed = true;
            context.AudioService.PlaySfx(SfxType.PhoCookSuccess);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu hảo hạng rồi: Bánh phở dẻo mềm, thịt bò tươi, nước dùng thơm ngát, hành hoa và thảo mộc Lãn Ông. Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh nhé!",
                QuestCompleted: true
            );
        }
```

#### Proposed Replacement:
```csharp
    public InteractionResult Interact(InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Ensure Hanoi quests are registered
        if (context.QuestManager.GetQuest(HanoiQuests.Quest1Id) == null)
        {
            HanoiQuests.RegisterAll(context.QuestManager);
        }

        // State 1: Quest 1 Not Started
        if (!context.QuestFlags.Q1_Started)
        {
            context.QuestManager.StartQuest(HanoiQuests.Quest1Id);
            context.QuestFlags.Q1_Started = true;
            context.AudioService.PlaySfx(SfxType.ButtonClick);

            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: "Chào bé Mây! Hôm nay bà chuẩn bị nấu nồi phở gia truyền nhưng thiếu mất mấy nguyên liệu ngon nhất. Cháu giúp bà ghé các quầy hàng trong phố cổ xin đủ 5 món: Bánh phở tươi, Thịt bò mềm, Nước dùng ninh xương, Hành lá rau mùi và Gói thảo mộc thơm lừng nhé!",
                QuestStarted: true
            );
        }

        // State 2: Requirements Met -> Complete Quest 1
        if (context.QuestFlags.AreAllQ1IngredientsCollected() && !context.QuestFlags.Q1_Completed)
        {
            // Rehydrate QuestManager from QuestFlags to guarantee internal synchronization
            context.QuestManager.HydrateFromFlags(context.QuestFlags);

            if (context.QuestManager.CompleteQuest(HanoiQuests.Quest1Id))
            {
                context.QuestFlags.Q1_Completed = true;
                context.AudioService.PlaySfx(SfxType.PhoCookSuccess);

                return new InteractionResult(
                    Success: true,
                    DialogueSpeaker: DisplayName,
                    DialogueText: "Tuyệt vời quá bé Mây ơi! Đủ cả 5 nguyên liệu hảo hạng rồi: Bánh phở dẻo mềm, thịt bò tươi, nước dùng thơm ngát, hành hoa và thảo mộc Lãn Ông. Giờ bà cháu mình cùng bắt tay vào xếp bát phở hoàn chỉnh nhé!",
                    QuestCompleted: true
                );
            }
        }
```

---

### Exact Drop-In Replacement 3: `Source/Core/Interactions/HanoiInteractables.cs` (`PhoIngredientMerchant.Interact`)
**Location**: `Source/Core/Interactions/HanoiInteractables.cs`, lines 140-155

#### Current Code:
```csharp
    public InteractionResult Interact(InteractionContext context)
    {
        // 1. Not started yet
        if (!context.QuestFlags.Q1_Started)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _preQuestDialogue
            );
        }
```

#### Proposed Replacement:
```csharp
    public InteractionResult Interact(InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Not started yet
        if (!context.QuestFlags.Q1_Started)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: DisplayName,
                DialogueText: _preQuestDialogue
            );
        }

        // Ensure QuestManager is hydrated into InProgress before recording progress
        if (context.QuestManager.GetQuest(HanoiQuests.Quest1Id) == null)
        {
            HanoiQuests.RegisterAll(context.QuestManager);
        }
        if (context.QuestManager.GetQuestState(HanoiQuests.Quest1Id) == QuestState.NotStarted)
        {
            context.QuestManager.HydrateFromFlags(context.QuestFlags);
        }
```

---

### Exact Drop-In Replacement 4: `Tests/Challenger2EmpiricalTests.cs`
**Location**: `Tests/Challenger2EmpiricalTests.cs`, lines 626-670

#### Current Code:
```csharp
    [Fact]
    public void Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock()
    {
        // Forensic Proof of Desynchronization Bug:
        // If flags are set (e.g. from save state or dialogue branch) without QuestManager.RecordObjectiveProgress,
        // BaAnNpc.Interact calls CompleteQuest, which silently fails (returns false) because QuestManager objectives aren't met.
        // However, BaAnNpc blindly sets QuestFlags.Q1_Completed = true.
        // As a result: QuestFlags says Q1 is completed, but QuestManager says Q1 is NOT completed!
        // Quest 2 is permanently locked out (soft-lock).
        var (ctx, _, qm, _, flags, _) = CreateHarnessContext();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();

        flags.Q1_Started = true;
        flags.Q1_BanhPho_Collected = true;
        flags.Q1_ThitBo_Collected = true;
        flags.Q1_NuocDung_Collected = true;
        flags.Q1_HanhLa_Collected = true;
        flags.Q1_ThaoMoc_Collected = true;

        var result = baAn.Interact(ctx);

        Assert.True(result.Success);
        Assert.True(result.QuestCompleted);
        Assert.True(flags.Q1_Completed);

        // Soft-lock demonstrated: QuestManager remains incomplete and Quest 2 cannot start!
        Assert.False(qm.IsQuestCompleted(HanoiQuests.Quest1Id));
        Assert.False(qm.CanStartQuest(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData()
    {
        // Forensic Proof: HanoiExplorationScene accepts SaveData but fails to call
        // QuestManager.HydrateFromFlags(SaveData.Level1State.QuestFlags).
        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Started = true;
        saveData.Level1State.QuestFlags.Q1_BanhPho_Collected = true;

        var scene = new GameProject.Stages.HanoiExplorationScene(saveData);

        // QuestManager was NOT hydrated:
        Assert.Equal(QuestState.NotStarted, scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id));
        Assert.Equal(0, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.BanhPho)!.CurrentCount);
    }
```

#### Proposed Replacement:
```csharp
    [Fact]
    public void Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock()
    {
        // Verified Fix for Desynchronization Bug:
        // When flags are set directly (e.g. from save state or dialogue branch) without prior QuestManager.RecordObjectiveProgress,
        // BaAnNpc.Interact synchronizes QuestManager via HydrateFromFlags before CompleteQuest.
        // Both QuestFlags and QuestManager transition to Completed, and Quest 2 is unlocked without soft-lock.
        var (ctx, _, qm, _, flags, _) = CreateHarnessContext();
        var (baAn, _) = HanoiInteractableFactory.CreateHanoiInteractables();

        flags.Q1_Started = true;
        flags.Q1_BanhPho_Collected = true;
        flags.Q1_ThitBo_Collected = true;
        flags.Q1_NuocDung_Collected = true;
        flags.Q1_HanhLa_Collected = true;
        flags.Q1_ThaoMoc_Collected = true;

        var result = baAn.Interact(ctx);

        Assert.True(result.Success);
        Assert.True(result.QuestCompleted);
        Assert.True(flags.Q1_Completed);

        // Desync resolved: QuestManager is completed and Quest 2 can now start!
        Assert.True(qm.IsQuestCompleted(HanoiQuests.Quest1Id));
        Assert.True(qm.CanStartQuest(HanoiQuests.Quest2Id));
    }

    [Fact]
    public void Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData()
    {
        // Verified Fix: HanoiExplorationScene constructor hydrates QuestManager
        // from SaveData.Level1State.QuestFlags upon initialization.
        var saveData = SaveData.CreateDefault();
        saveData.Level1State.QuestFlags.Q1_Started = true;
        saveData.Level1State.QuestFlags.Q1_BanhPho_Collected = true;

        var scene = new GameProject.Stages.HanoiExplorationScene(saveData);

        // QuestManager is properly hydrated:
        Assert.Equal(QuestState.InProgress, scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id));
        Assert.Equal(1, scene.QuestManager.GetQuest(HanoiQuests.Quest1Id)!.GetObjective(ItemIds.BanhPho)!.CurrentCount);
    }
```

---

## 5. Verification Method

1. **Verify Target Unit Tests**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock|FullyQualifiedName~Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData"
   ```
   *Expected Result*: 2 Passed, 0 Failed.

2. **Verify Full Challenger 2 Harness**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Challenger2EmpiricalTests"
   ```
   *Expected Result*: All 31 tests pass.

3. **Verify Full Test Suite**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj
   ```
   *Expected Result*: 278+ tests pass with 0 failures.

4. **Invalidation Conditions**:
   - If `scene.QuestManager.GetQuestState(HanoiQuests.Quest1Id)` returns `NotStarted` after loading `SaveData` with `Q1_Started = true`, hydration failed.
   - If `qm.CanStartQuest(HanoiQuests.Quest2Id)` returns `false` after `baAn.Interact` completes Quest 1, desynchronization soft-lock persists.
