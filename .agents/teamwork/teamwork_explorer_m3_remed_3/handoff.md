# Milestone 3 Remediation Report: PhoCookingEngine Invariant Reset & Stress Test Updates

**Author**: Milestone 3 Remediation Explorer 3 (`teamwork_explorer_m3_remed_3`)  
**Scope**: Invariant correction for `PhoCookingEngine.ValidateAssembly` and empirical assertion updates for `Milestone3Challenger1StressTests.cs` and `ChallengerM3ProgressionStressTests.cs`  
**Verdict**: Remediation Design Ready for Worker Implementation  

---

## 1. Observation

### 1.1 `PhoCookingEngine.cs` Invariant Defect
- **File**: `Source/Core/Puzzles/PhoCookingEngine.cs`, lines 66–140.
- **Code observed**:
  ```csharp
  66:     public bool ValidateAssembly(out string errorMessage)
  67:     {
  68:         errorMessage = string.Empty;
  69: 
  70:         // Check if any slot is empty
  71:         for (int i = 0; i < TotalSlots; i++)
  72:         {
  73:             if (string.IsNullOrEmpty(_slots[i]))
  74:             {
  75:                 errorMessage = "Bát phở vẫn còn thiếu nguyên liệu, hãy hoàn thiện đủ cả 5 bước nhé!";
  76:                 return false;
  77:             }
  78:         }
  ...
  138:         IsSolved = true;
  139:         return true;
  140:     }
  ```
- **Direct observation**: `IsSolved` defaults to `false` (line 48) and is set to `true` at line 138 upon validation success. None of the seven `return false;` branches within `ValidateAssembly` assign `IsSolved = false;`. If an engine instance previously validated successfully (`IsSolved == true`), modifying slots (e.g., calling `RemoveIngredient(0)`) followed by a subsequent `ValidateAssembly(out _)` correctly returns `false`, but leaves `IsSolved == true`.

### 1.2 `Tests/Milestone3Challenger1StressTests.cs` Defect Reproduction Assertions
- **File**: `Tests/Milestone3Challenger1StressTests.cs`
- **Method 1 (`PhoAssembly_PostSolveMutation_ValidateFailureLeavesIsSolvedStale`, lines 212–234)**:
  ```csharp
  225:         // Mutate by removing an ingredient
  226:         engine.RemoveIngredient(0);
  227:         bool valid = engine.ValidateAssembly(out string error);
  228:         Assert.False(valid);
  229:         Assert.NotEmpty(error);
  230: 
  231:         // EMPIRICAL DEFECT: PhoCookingEngine.ValidateAssembly does not reset IsSolved = false on failure,
  232:         // leaving IsSolved = true despite the assembly being currently invalid:
  233:         Assert.True(engine.IsSolved, "Empirical defect: IsSolved remains true after invalid assembly validation");
  ```
  - **Observation**: Line 233 was specifically written to assert `Assert.True(engine.IsSolved)` to prove the bug existed. Once `PhoCookingEngine.ValidateAssembly` is remediated to reset `IsSolved = false;`, line 233 fails.
- **Method 2 (`StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding`, lines 580–621)**:
  ```csharp
  615:         Assert.False(scene.RequestNextScene,
  616:             "Empirical Defect: StallPuzzleScene is trapped in letter modal and cannot transition headlessly");
  617: 
  618:         // EMPIRICAL DEFECT 2: GrayShadow_LetterRead was never set by StallPuzzleScene
  619:         Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
  620:             "Empirical Defect: GrayShadow_LetterRead remains false, causing cutscene duplication in ExplorationScene");
  ```
  - **Observation**: Asserts `Assert.False` on `scene.RequestNextScene` and `saveData.Level1State.QuestFlags.GrayShadow_LetterRead` to document the headless stall deadlock and missing flag synchronization. Once Remediation 1 & 2 are applied, this test must supply mock input and assert positive transition and flag synchronization.

### 1.3 `Tests/ChallengerM3ProgressionStressTests.cs` Defect Reproduction Assertions
- **File**: `Tests/ChallengerM3ProgressionStressTests.cs`
- **Method (`StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap`, lines 541–641)**:
  ```csharp
  625:         Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
  626:             "DEFECT CONFIRMED: StallPuzzleScene does not set GrayShadow_LetterRead flag upon modal dismissal");
  ...
  639:         Assert.False(scene.RequestWorldMap, "DEFECT: RequestWorldMap is false because GrayShadow_LetterRead was not set");
  640:         Assert.Equal(GameState.Cutscene, sm.CurrentState);
  ```
  - **Observation**: Lines 625, 639, and 640 assert the presence of the duplicate cutscene trap (`GrayShadow_LetterRead == false`, `RequestWorldMap == false`, `sm.CurrentState == GameState.Cutscene`). Once `StallPuzzleScene` sets `GrayShadow_LetterRead = true`, gate interaction triggers the `else` branch in `HanoiExplorationScene.cs:294-296` (`RequestWorldMap = true`), remaining in `GameState.Exploration`. Thus lines 625, 639, and 640 fail unless updated to assert correct post-remediation behavior.

---

## 2. Logic Chain

1. **`PhoCookingEngine` Invariant Contract**:
   - `IPhoPuzzleEngine.IsSolved` reflects whether the puzzle is currently validated as solved.
   - If an engine has reached a solved state (`IsSolved = true`), and the player mutates or empties any slot, running `ValidateAssembly` evaluates the current state.
   - If validation fails, the configuration is invalid. Leaving `IsSolved == true` directly violates the invariant that `IsSolved` reflects valid assembly state.
   - By inserting `IsSolved = false;` at the entry of `ValidateAssembly(out string errorMessage)` immediately after `errorMessage = string.Empty;`, every validation run starts in an unvalidated/unsolved state. Only if all seven checks pass does execution reach line 138 (`IsSolved = true;`).
   - This 1-line change guarantees that all failure exit points return `false` with `IsSolved == false`.

2. **Challenger Stress Test Inversion**:
   - Challengers 1 and 2 implemented adversarial tests designed to demonstrate empirical defects under existing production code. They asserted `Assert.True(engine.IsSolved)` on invalid bowls and `Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead)` on cutscenes.
   - When the worker applies the remediations, tests asserting buggy behavior will fail during regression testing (`dotnet test`).
   - Updating these assertions from defect confirmations to invariant/remediation verifications aligns the entire test suite with the corrected domain contracts.

---

## 3. Caveats

1. **Scope of Invalidation**:
   - `ValidateAssembly` is the sole public validation method in `PhoCookingEngine`.
   - Modifying slots via `PlaceIngredient` or `RemoveIngredient` without calling `ValidateAssembly` will leave `IsSolved` unchanged until `ValidateAssembly()` or `ResetPuzzle()` is called. In game flow (`PhoAssemblyScene.cs`), `scene.Validate()` is always invoked after placing/modifying ingredients before querying `IsSolved`. Setting `IsSolved = false;` inside `ValidateAssembly` provides the exact minimal diff needed.
2. **Defensive Consideration for `StallPuzzleScene`**:
   - In `StallPuzzleScene.Update(float dt)`, checking `if (_letterModal.IsActive || _letterModal.RequestClose)` ensures that even if an automated test calls `letterModal.Dismiss()` directly via reflection (setting `IsActive = false; RequestClose = true;`), `StallPuzzleScene.Update` will still catch `_letterModal.RequestClose == true`, synchronize `GrayShadow_LetterRead = true`, publish events, and set `RequestNextScene = true`.

---

## 4. Conclusion & Drop-In Code Remediations

### 4.1 Drop-In Fix for `Source/Core/Puzzles/PhoCookingEngine.cs`

**Target File**: `Source/Core/Puzzles/PhoCookingEngine.cs`  
**Location**: Lines 66–70

```csharp
<<<< CURRENT (Lines 66-70)
    public bool ValidateAssembly(out string errorMessage)
    {
        errorMessage = string.Empty;

        // Check if any slot is empty
==== REPLACEMENT
    public bool ValidateAssembly(out string errorMessage)
    {
        errorMessage = string.Empty;
        IsSolved = false;

        // Check if any slot is empty
>>>>
```

---

### 4.2 Drop-In Updates for `Tests/Milestone3Challenger1StressTests.cs`

**Target File**: `Tests/Milestone3Challenger1StressTests.cs`  

#### Change 1: Update `PhoAssembly_PostSolveMutation_ValidateFailureLeavesIsSolvedStale` (Lines 212–234)

```csharp
<<<< CURRENT (Lines 212-234)
    [Fact]
    public void PhoAssembly_PostSolveMutation_ValidateFailureLeavesIsSolvedStale()
    {
        var engine = new PhoCookingEngine();

        // Solve canonically
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);
        Assert.True(engine.ValidateAssembly(out _));
        Assert.True(engine.IsSolved);

        // Mutate by removing an ingredient
        engine.RemoveIngredient(0);
        bool valid = engine.ValidateAssembly(out string error);
        Assert.False(valid);
        Assert.NotEmpty(error);

        // EMPIRICAL DEFECT: PhoCookingEngine.ValidateAssembly does not reset IsSolved = false on failure,
        // leaving IsSolved = true despite the assembly being currently invalid:
        Assert.True(engine.IsSolved, "Empirical defect: IsSolved remains true after invalid assembly validation");
    }
==== REPLACEMENT
    [Fact]
    public void PhoAssembly_PostSolveMutation_ValidateFailureResetsIsSolved()
    {
        var engine = new PhoCookingEngine();

        // Solve canonically
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);
        Assert.True(engine.ValidateAssembly(out _));
        Assert.True(engine.IsSolved);

        // Mutate by removing an ingredient
        engine.RemoveIngredient(0);
        bool valid = engine.ValidateAssembly(out string error);
        Assert.False(valid);
        Assert.NotEmpty(error);

        // REMEDIATION VERIFIED: PhoCookingEngine.ValidateAssembly resets IsSolved = false on failure paths
        Assert.False(engine.IsSolved, "IsSolved must be false when ValidateAssembly fails on an invalid bowl post-mutation");
    }
>>>>
```

#### Change 2: Update `StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding` (Lines 580–621)

```csharp
<<<< CURRENT (Lines 580-621)
    [Fact]
    public void StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding()
    {
        var eventBus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(questManager);

        var scene = new StallPuzzleScene(saveData, questManager, eventBus);
        scene.Enter();

        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved);

        // Advance 3.5s -> triggers letter modal
        scene.Update(3.5f);

        // Access internal modal via reflection to observe state
        var modalField = typeof(StallPuzzleScene).GetField("_letterModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Advance 20 more seconds headlessly
        for (int i = 0; i < 40; i++)
        {
            scene.Update(0.5f);
        }

        // Even though typewriter is 100% complete:
        Assert.True(letterModal.IsTypingComplete);

        // EMPIRICAL DEFECT: StallPuzzleScene.Update(dt) does not forward input or inject IInputService,
        // and Raylib.IsWindowReady() is false headlessly, so confirm is never passed to _letterModal.
        // Therefore RequestNextScene is never set, and the scene is permanently trapped:
        Assert.False(scene.RequestNextScene,
            "Empirical Defect: StallPuzzleScene is trapped in letter modal and cannot transition headlessly");

        // EMPIRICAL DEFECT 2: GrayShadow_LetterRead was never set by StallPuzzleScene
        Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "Empirical Defect: GrayShadow_LetterRead remains false, causing cutscene duplication in ExplorationScene");
    }
==== REPLACEMENT
    [Fact]
    public void StallPuzzleScene_HeadlessUpdate_InputForwardingAllowsDismissalAndSetsLetterRead()
    {
        var eventBus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(questManager);
        var inputService = new GameProject.Core.Ports.MockInputService();

        var scene = new StallPuzzleScene(saveData, questManager, eventBus, inputService: inputService);
        scene.Enter();

        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved);

        // Advance 3.5s -> triggers letter modal
        scene.Update(3.5f);

        // Access internal modal via reflection to observe state
        var modalField = typeof(StallPuzzleScene).GetField("_letterModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Advance typewriter to completion
        for (int i = 0; i < 40; i++)
        {
            scene.Update(0.5f);
        }
        Assert.True(letterModal.IsTypingComplete);

        // Pulsing confirm input forwards to modal and triggers dismissal
        inputService.IsConfirmJustPressed = true;
        scene.Update(0.1f);
        inputService.IsConfirmJustPressed = false;

        // REMEDIATION VERIFIED: RequestNextScene is true and GrayShadow_LetterRead is synchronized
        Assert.True(scene.RequestNextScene,
            "Remediation Verified: StallPuzzleScene dismisses modal and requests next scene via forwarded input");
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "Remediation Verified: GrayShadow_LetterRead is set to true upon modal dismissal");
    }
>>>>
```

---

### 4.3 Drop-In Updates for `Tests/ChallengerM3ProgressionStressTests.cs`

**Target File**: `Tests/ChallengerM3ProgressionStressTests.cs`  
**Location**: Lines 593–642

```csharp
<<<< CURRENT (Lines 593-642)
        // Step 3: Trigger StallPuzzleScene's internal letter modal and simulate completion
        var letterModalField = typeof(StallPuzzleScene).GetField("_letterModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)letterModalField!.GetValue(stallModal)!;

        // Advance 3.1s so stall puzzle triggers letter modal
        for (int i = 0; i < 35; i++)
        {
            stallModal.Update(0.1f);
        }
        Assert.True(letterModal.IsActive);

        // Advance typewriter to completion
        for (int i = 0; i < 20; i++)
        {
            letterModal.Update(1.0f);
        }
        Assert.True(letterModal.IsTypingComplete);

        // Simulate modal dismissal and request next scene
        letterModal.Dismiss();
        var reqProp = typeof(StallPuzzleScene).GetProperty("RequestNextScene",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        reqProp!.SetValue(stallModal, true);
        Assert.True(stallModal.RequestNextScene);

        // Step 4: Exploration scene pops StallPuzzleScene
        scene.Update(0.016f);
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // Bug 1 Confirmation: Even after letter modal was shown and dismissed inside StallPuzzleScene,
        // GrayShadow_LetterRead was NEVER set to true!
        Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "DEFECT CONFIRMED: StallPuzzleScene does not set GrayShadow_LetterRead flag upon modal dismissal");

        // Step 5: Player interacts with Long Bien gate again expecting to enter World Map
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // DEFECT CONFIRMED: Player does NOT transition to World Map!
        // Instead, the player is trapped in GameState.Cutscene receiving a DUPLICATE letter modal!
        Assert.False(scene.RequestWorldMap, "DEFECT: RequestWorldMap is false because GrayShadow_LetterRead was not set");
        Assert.Equal(GameState.Cutscene, sm.CurrentState);
    }
==== REPLACEMENT
        // Step 3: Trigger StallPuzzleScene's internal letter modal and simulate completion
        var letterModalField = typeof(StallPuzzleScene).GetField("_letterModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)letterModalField!.GetValue(stallModal)!;

        // Advance 3.1s so stall puzzle triggers letter modal
        for (int i = 0; i < 35; i++)
        {
            stallModal.Update(0.1f);
        }
        Assert.True(letterModal.IsActive);

        // Advance typewriter to completion
        for (int i = 0; i < 20; i++)
        {
            letterModal.Update(1.0f);
        }
        Assert.True(letterModal.IsTypingComplete);

        // Forward confirm input into stallModal to dismiss letter modal
        input.IsConfirmJustPressed = true;
        stallModal.Update(0.1f);
        input.IsConfirmJustPressed = false;

        // Verify StallPuzzleScene closed modal and requested next scene
        Assert.True(stallModal.RequestNextScene);

        // Step 4: Exploration scene pops StallPuzzleScene
        scene.Update(0.016f);
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // REMEDIATION CONFIRMED: GrayShadow_LetterRead was synchronized upon modal dismissal
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
            "REMEDIATION CONFIRMED: StallPuzzleScene sets GrayShadow_LetterRead flag upon modal dismissal");

        // Step 5: Player interacts with Long Bien gate again to enter World Map
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // REMEDIATION CONFIRMED: Player transitions to World Map without duplicate cutscene trap
        Assert.True(scene.RequestWorldMap, "REMEDIATION CONFIRMED: RequestWorldMap is true because GrayShadow_LetterRead was set");
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }
>>>>
```

---

## 5. Verification Method

Once the worker implements the remediations, verify with the following commands:

```bash
# 1. Verify PhoCookingEngine invariant reset specifically:
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~PhoAssembly_PostSolveMutation_ValidateFailureResetsIsSolved"

# 2. Verify Challenger 1 stress test suite (43 tests):
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Milestone3Challenger1StressTests"

# 3. Verify Challenger 2 stress test suite (28 tests):
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~ChallengerM3ProgressionStressTests"

# 4. Verify full test suite across entire project (391+ tests):
dotnet test Tests/GameProject.Tests.csproj
```

**Invalidation Conditions**:
- If `PhoCookingEngine.ValidateAssembly` fails to reset `IsSolved = false` when called with an invalid slot combination.
- If `StallPuzzleScene` fails to dismiss via `_inputService` in headless mode.
- If interacting with the Long Biên Gate post-solve opens a duplicate letter modal or leaves `RequestWorldMap == false`.
