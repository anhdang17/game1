# Milestone 3 Challenger 1 Handoff Report: Puzzle Engines & BFS Adversarial Stress Testing

**Verdict**: `REQUEST_CHANGES`

---

## 1. Observation

### Verification Commands & Results
- Command: `dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Milestone3Challenger1StressTests"`
  - Output: `Passed! - Failed: 0, Passed: 43, Skipped: 0, Total: 43, Duration: 52 ms`
- Command: `dotnet build GameProject.csproj`
  - Output: `Build succeeded. 0 Warning(s), 0 Error(s)`
- Command: `dotnet test Tests/GameProject.Tests.csproj`
  - Output: 43 new adversarial stress tests executed in `Tests/Milestone3Challenger1StressTests.cs`.

### Empirical Findings Observed in Code

1. **Headless Cutscene Modal Deadlock in `StallPuzzleScene.cs`**:
   - Location: `Source/Stages/StallPuzzleScene.cs:97-105`
   - Code:
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
   - In `Source/UI/GrayShadowLetterModal.cs:48-67`, `confirm` input is evaluated as:
     ```csharp
     bool confirm = isConfirmPressed;
     if (Raylib.IsWindowReady())
     {
         confirm = confirm || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
         ...
     }
     ...
     else if (confirm)
     {
         Dismiss();
     }
     ```
   - In headless execution, `Raylib.IsWindowReady()` is `false`.
   - `StallPuzzleScene` does not accept `IInputService` nor does it provide any programmatic hook or forward `confirm` into `_letterModal.Update(dt)`.
   - Therefore, `confirm` remains permanently `false`, `_letterModal.Dismiss()` is never called, `_letterModal.RequestClose` is never set, and `RequestNextScene` never becomes `true`. Headless test runners are permanently deadlocked in `_letterModal.IsActive == true`. Verified in `Tests/Milestone3Challenger1StressTests.cs:StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding`.

2. **Missing `GrayShadow_LetterRead` Sync & Duplicate Cutscene Trigger**:
   - Location: `Source/Stages/StallPuzzleScene.cs:112` and `Source/Stages/HanoiExplorationScene.cs:288-297`
   - In `StallPuzzleScene.cs`, after solving, `_letterModal.Show()` is displayed at `_solveTimer >= 3.0f`. However, `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead` is **never set to `true`**.
   - Furthermore, in `StallPuzzleScene.cs:112`:
     ```csharp
     if (_solveTimer >= 3.0f && !_letterModal.IsActive && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)
     {
         _letterModal.Show();
     }
     ```
     Because `GrayShadow_LetterRead` remains `false`, if `_letterModal.Dismiss()` is invoked, `IsActive` becomes `false` and line 112 immediately calls `_letterModal.Show()` again on the next update, triggering an infinite modal loop unless immediately popped on that exact tick.
   - When control returns to `HanoiExplorationScene.cs:288-297`:
     ```csharp
     else if (QuestFlags.Gate_LongBien_Unlocked)
     {
         if (!QuestFlags.GrayShadow_LetterRead)
         {
             _grayShadowLetterModal.Show();
             StateManager.PushState(GameState.Cutscene);
         }
         else
         {
             RequestWorldMap = true;
         }
     }
     ```
     Because `GrayShadow_LetterRead` was never set by `StallPuzzleScene`, interacting with the gate in Hanoi forces the player to view and dismiss the exact same Bóng Xám letter cutscene a second time before `RequestWorldMap` is granted.

3. **Stale `PhoCookingEngine.IsSolved` Invariant on Post-Solve Slot Invalidation**:
   - Location: `Source/Core/Puzzles/PhoCookingEngine.cs:66-140`
   - In `ValidateAssembly(out string errorMessage)`, `IsSolved = true;` is only assigned at line 138 upon reaching the end of the method.
   - When an ingredient is removed or altered after solving (e.g. `engine.RemoveIngredient(0)`), a subsequent call to `ValidateAssembly` correctly returns `false`, but does not reset `IsSolved = false;`.
   - Consequently, `engine.IsSolved` continues to report `true` on an invalid/empty bowl until `ResetPuzzle()` is called. Verified in `Tests/Milestone3Challenger1StressTests.cs:PhoAssembly_PostSolveMutation_ValidateFailureLeavesIsSolvedStale`.

---

## 2. Logic Chain

1. **Premise**: PROJECT.md Section 8 and Section 106 stipulate that all game domain logic, quest progression, cutscene states, and puzzle mechanics must run headlessly (< 500 ms) in `GameProject.Tests` with zero display server dependency, and that `CompleteLevel1()` coordinates the single occurrence of the Gray Shadow narrative cutscene.
2. **Analysis of StallPuzzleScene**:
   - `StallPuzzleScene` hosts an internal instance of `GrayShadowLetterModal`.
   - Because `StallPuzzleScene` does not take `IInputService` or pass input flags to `_letterModal.Update(dt)`, headless tests executing `Update()` after puzzle solve cannot dismiss the modal, violating the headless testability requirement.
   - Because `StallPuzzleScene` does not set `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true` upon dismissal, `HanoiExplorationScene` re-triggers `_grayShadowLetterModal` upon gate interaction, creating duplicate narrative presentation.
3. **Analysis of PhoCookingEngine**:
   - Invariants require `engine.IsSolved` to be equivalent to "the current bowl configuration is valid".
   - Because `ValidateAssembly` does not reset `IsSolved = false` on failure paths, modifying slots post-solve breaks this invariant.
4. **Conclusion**:
   - The puzzle algorithms (Pho permutations and BFS 4-connected grid pathfinding) are mathematically sound and robust against adversarial attacks.
   - However, the modal lifecycle and flag synchronization defects require remediation before Milestone 3 can be approved.

---

## 3. Caveats

- Tests were run headlessly via .NET 10 xUnit test runner. Real-time audio rendering and GPU raylib shaders were tested via headless mocks and contract verification rather than physical display hardware.
- No other core domain models were found to have invariant corruption.

---

## 4. Conclusion

**Verdict**: `REQUEST_CHANGES`

### Required Actions for Worker:
1. **Fix `StallPuzzleScene` input decoupling and headless testability**:
   - Inject `IInputService? inputService = null` in `StallPuzzleScene` constructor.
   - In `StallPuzzleScene.Update(float dt)`, pass input pulses to `_letterModal.Update(dt, isConfirmPressed: _inputService?.IsConfirmJustPressed ?? false, isSkipPressed: _inputService?.IsCancelJustPressed ?? false)`.
2. **Synchronize `GrayShadow_LetterRead` in `StallPuzzleScene`**:
   - When `_letterModal.RequestClose` is detected in `StallPuzzleScene.Update()`, set:
     ```csharp
     _saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
     _eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
     _eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
     RequestNextScene = true;
     ```
3. **Reset `IsSolved` on validation failure in `PhoCookingEngine`**:
   - In `PhoCookingEngine.ValidateAssembly(out string errorMessage)`, add `IsSolved = false;` at the beginning of the method before the validation checks.

---

## 5. Verification Method

To independently reproduce and verify these findings, run:
```bash
# 1. Run Challenger 1 adversarial suite (all 43 tests pass, exposing empirical defect assertions):
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Milestone3Challenger1StressTests"

# 2. Specifically inspect the empirical defect tests:
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding"
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~PhoAssembly_PostSolveMutation_ValidateFailureLeavesIsSolvedStale"
```
