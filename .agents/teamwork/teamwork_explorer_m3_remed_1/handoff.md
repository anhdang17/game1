# Milestone 3 Remediation Explorer 1 Handoff Report: StallPuzzleScene Cutscene Synchronization & Gate Transition

**Author**: `teamwork_explorer_m3_remed_1`  
**Milestone**: Milestone 3 Remediation  
**Target Areas**: `Source/Stages/StallPuzzleScene.cs`, `Source/Stages/HanoiExplorationScene.cs`, `Tests/ChallengerM3ProgressionStressTests.cs`  
**Date**: 2026-11-09T01:31:00Z  

---

## 1. Observation

### 1.1 Existing Code in `Source/Stages/StallPuzzleScene.cs` (Lines 97–121)

```csharp
97:         if (_letterModal.IsActive)
98:         {
99:             _letterModal.Update(dt);
100:            if (_letterModal.RequestClose)
101:            {
102:                RequestNextScene = true;
103:            }
104:            return;
105:        }
106:
107:        if (_solved)
108:        {
109:            _solveTimer += dt;
110:            _bloomProgress = Math.Clamp(_solveTimer / 2.5f, 0f, 1f);
111:
112:            if (_solveTimer >= 3.0f && !_letterModal.IsActive && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)
113:            {
114:                _letterModal.Show();
115:            }
116:            else if (_solveTimer >= 4.0f && !RequestNextScene)
117:            {
118:                RequestNextScene = true;
119:            }
120:            return;
121:        }
```

### 1.2 Existing Code in `Source/Stages/HanoiExplorationScene.cs` (Lines 238–249 and 287–299)

In `HanoiExplorationScene.cs` lines 238–249:
```csharp
238:        if (StateManager.CurrentState == GameState.Cutscene)
239:        {
240:            _grayShadowLetterModal.Update(deltaTime);
241:            if (_grayShadowLetterModal.RequestClose)
242:            {
243:                SaveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
244:                EventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
245:                EventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
246:                StateManager.PopState();
247:            }
248:            return;
249:        }
```

In `HanoiExplorationScene.cs` lines 287–299:
```csharp
287:                    else if (QuestFlags.Gate_LongBien_Unlocked)
288:                    {
289:                        if (!QuestFlags.GrayShadow_LetterRead)
290:                        {
291:                            _grayShadowLetterModal.Show();
292:                            StateManager.PushState(GameState.Cutscene);
293:                        }
294:                        else
295:                        {
296:                            RequestWorldMap = true;
297:                        }
298:                    }
```

### 1.3 Game Event Records in `Source/Core/Events/GameEvents.cs` (Lines 44–45)

```csharp
44: public record CutsceneEndedEvent(string CutsceneId) : GameEventBase;
45: public record LetterOpenedEvent(string LetterId, string Sender = "Bóng Xám") : GameEventBase;
```

### 1.4 Empirical Defect Confirmation in `Tests/ChallengerM3ProgressionStressTests.cs` (Lines 623–641)

```csharp
623:        // Bug 1 Confirmation: Even after letter modal was shown and dismissed inside StallPuzzleScene,
624:        // GrayShadow_LetterRead was NEVER set to true!
625:        Assert.False(saveData.Level1State.QuestFlags.GrayShadow_LetterRead,
626:            "DEFECT CONFIRMED: StallPuzzleScene does not set GrayShadow_LetterRead flag upon modal dismissal");
627:
628:        // Step 5: Player interacts with Long Bien gate again expecting to enter World Map
629:        input.IsInteractJustPressed = true;
630:        scene.Update(0.016f);
631:        input.IsInteractJustPressed = false;
632:
633:        input.IsConfirmJustPressed = true;
634:        scene.Update(0.016f);
635:        input.IsConfirmJustPressed = false;
636:
637:        // DEFECT CONFIRMED: Player does NOT transition to World Map!
638:        // Instead, the player is trapped in GameState.Cutscene receiving a DUPLICATE letter modal!
639:        Assert.False(scene.RequestWorldMap, "DEFECT: RequestWorldMap is false because GrayShadow_LetterRead was not set");
640:        Assert.Equal(GameState.Cutscene, sm.CurrentState);
```

---

## 2. Logic Chain

1. **Premise 1**: When the street stall puzzle is solved in `StallPuzzleScene`, `TriggerSolve()` sets `_solved = true`, unlocks the gate, awards stamp/journal page, and completes Level 1.
2. **Premise 2**: At `_solveTimer >= 3.0f`, `_letterModal.Show()` displays the Bóng Xám parchment letter cutscene.
3. **Observation Reference 1.1**: When the player dismisses `_letterModal`, `_letterModal.Dismiss()` marks `IsActive = false` and `RequestClose = true`.
4. **Observation Reference 1.1**: Lines 100–103 in `StallPuzzleScene.cs` evaluate `if (_letterModal.RequestClose)` and set `RequestNextScene = true`, but omit updating `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true` and omit publishing `CutsceneEndedEvent("GrayShadow_Letter")` and `LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám")`.
5. **Deduction 1 (Internal Scene Loop Trap)**: If `StallPuzzleScene.Update(dt)` executes on any subsequent tick while `RequestNextScene == true`:
   - Line 97 `if (_letterModal.IsActive)` is `false`.
   - Line 107 `if (_solved)` is `true`.
   - Line 112 evaluates `if (_solveTimer >= 3.0f && !_letterModal.IsActive && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)`.
   - Because `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead` was never set to `true`, this condition evaluates to `true` on the very next frame!
   - Line 114 executes `_letterModal.Show()`, reviving the modal (`IsActive = true`, `RequestClose = false`), entrapping the scene in an infinite modal loop unless the parent popped the state on that exact single frame.
6. **Observation Reference 1.2 & 1.4**: When control returns to `HanoiExplorationScene`, interacting with the unlocked Long Biên Gate checks `if (!QuestFlags.GrayShadow_LetterRead)`. Because `GrayShadow_LetterRead` was left `false`, `_grayShadowLetterModal.Show()` is called and `GameState.Cutscene` is pushed. The player is forced to read and dismiss the identical letter a second time before `RequestWorldMap = true` can be reached.
7. **Deduction 2 (Remediation Mechanism)**:
   - Synchronizing `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;` inside `StallPuzzleScene` upon `_letterModal.RequestClose` guarantees:
     1. Line 112 in `StallPuzzleScene` evaluates `!_saveData.Level1State.QuestFlags.GrayShadow_LetterRead` as `false`, preventing re-triggering of `_letterModal.Show()`.
     2. Event stream parity with `HanoiExplorationScene.cs:244–245` (`CutsceneEndedEvent` and `LetterOpenedEvent`).
     3. In `HanoiExplorationScene.cs:289`, `if (!QuestFlags.GrayShadow_LetterRead)` evaluates to `false`, executing `else { RequestWorldMap = true; }` immediately on gate interaction.

---

## 3. Caveats

- **No Caveats on synchronization mechanics**: Both challenger reports (Challenger 1 and Challenger 2) independently isolated and confirmed this exact defect.
- **Scope Boundary with Explorer 2**: Explorer 2 is responsible for constructor input injection (`IInputService? inputService = null`) and passing input flags to `_letterModal.Update(dt, ...)`. The remediation designed here handles both normal in-update dismissal and external programmatic dismissal (`_letterModal.Dismiss()`).
- **Scope Boundary with Explorer 3**: Explorer 3 is responsible for `PhoCookingEngine.IsSolved` invariant reset. Test assertion updates in `ChallengerM3ProgressionStressTests.cs` and `Milestone3Challenger1StressTests.cs` are documented below so the implementing worker has the exact specification.

---

## 4. Conclusion & Drop-In Remediation Design

### 4.1 Drop-in Code Fix for `Source/Stages/StallPuzzleScene.cs` (Lines 97–105)

#### Exact Before & After Snippet:

**Before:**
```csharp
        if (_letterModal.IsActive)
        {
            _letterModal.Update(dt);
            if (_letterModal.RequestClose)
            {
                RequestNextScene = true;
            }
            return;
        }
```

**After (Robust & Decoupled drop-in):**
```csharp
        if (_letterModal.IsActive)
        {
            _letterModal.Update(dt,
                isConfirmPressed: _inputService?.IsConfirmJustPressed ?? false,
                isSkipPressed: _inputService?.IsCancelJustPressed ?? false);
            if (_letterModal.RequestClose)
            {
                _saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
                _eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
                _eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
                RequestNextScene = true;
            }
            return;
        }
        else if (_letterModal.RequestClose && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)
        {
            _saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
            _eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
            _eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
            RequestNextScene = true;
            return;
        }
```

*Note*: The `else if (_letterModal.RequestClose && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)` branch provides resilience against scenarios where `_letterModal.Dismiss()` is invoked directly (such as in headless unit tests that call `letterModal.Dismiss()` via reflection or helper without advancing frames).

### 4.2 Supporting Update for `Source/Stages/HanoiExplorationScene.cs`

In `Source/Stages/HanoiExplorationScene.cs`:

1. **Line 240** (Cutscene update forwarding):
```csharp
<<<<
            _grayShadowLetterModal.Update(deltaTime);
====
            _grayShadowLetterModal.Update(deltaTime,
                isConfirmPressed: InputService.IsConfirmJustPressed,
                isSkipPressed: InputService.IsCancelJustPressed);
>>>>
```

2. **Line 283** (InputService pass-through to StallPuzzleScene):
```csharp
<<<<
                        _stallPuzzleModal ??= new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService);
====
                        _stallPuzzleModal ??= new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService, InputService);
>>>>
```

### 4.3 Required Test Assertion Update in `Tests/ChallengerM3ProgressionStressTests.cs`

In `Tests/ChallengerM3ProgressionStressTests.cs` lines 612–641:
Change the empirical defect assertions to the verified correct assertions:

```csharp
<<<<
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
====
        // Simulate modal dismissal and update stall modal
        letterModal.Dismiss();
        stallModal.Update(0.016f);
        Assert.True(stallModal.RequestNextScene);

        // Step 4: Exploration scene pops StallPuzzleScene
        scene.Update(0.016f);
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // Verified: GrayShadow_LetterRead is cleanly set to true upon StallPuzzleScene modal dismissal
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead);

        // Step 5: Player interacts with Long Bien gate again expecting to enter World Map
        input.IsInteractJustPressed = true;
        scene.Update(0.016f);
        input.IsInteractJustPressed = false;

        input.IsConfirmJustPressed = true;
        scene.Update(0.016f);
        input.IsConfirmJustPressed = false;

        // Verified: Player transitions directly to World Map without duplicate cutscene trap
        Assert.True(scene.RequestWorldMap);
        Assert.Equal(GameState.Exploration, sm.CurrentState);
>>>>
```

---

## 5. Verification Method

To independently verify this remediation:

```bash
# 1. Build solution to verify zero compiler errors or warnings
dotnet build GameProject.csproj

# 2. Run the progression stress tests
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~ChallengerM3ProgressionStressTests"

# 3. Specifically verify the transition test
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap"

# 4. Run entire suite to guarantee zero regressions
dotnet test Tests/GameProject.Tests.csproj
```

### Invalidation Conditions:
- If `saveData.Level1State.QuestFlags.GrayShadow_LetterRead` remains `false` after `_letterModal` dismissal in `StallPuzzleScene`.
- If `HanoiExplorationScene` pushes `GameState.Cutscene` when interacting with Long Biên Gate after Quest 3 completion.
- If `RequestWorldMap` fails to become `true` on the initial gate interaction post-Quest 3.
- If `_letterModal.Show()` is re-invoked in `StallPuzzleScene` after having already been dismissed.
