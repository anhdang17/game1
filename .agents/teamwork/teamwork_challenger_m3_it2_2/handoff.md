# Milestone 3 Iteration 2 Challenger 2 Empirical Report (Progression & Gate Challenger)

**Author**: Milestone 3 Iteration 2 Challenger 2 (`teamwork_challenger_m3_it2_2`)  
**Verdict**: **APPROVE**  
**Date**: 2026-11-09T01:44:30Z  
**Target Verification**:
- `Source/Stages/StallPuzzleScene.cs`
- `Source/Stages/HanoiExplorationScene.cs`
- `Source/Core/Puzzles/PhoCookingEngine.cs`
- `Tests/Milestone3Iteration2ProgressionEmpiricalTests.cs` (New test suite, 9 tests)
- `Tests/ChallengerM3ProgressionStressTests.cs`

---

## 1. Observation

### 1.1 StallPuzzleScene Letter Modal Lifecycle & Flag Synchronization
- In `Source/Stages/StallPuzzleScene.cs:76-85`, `IInputService? inputService = null` was added as an optional constructor parameter, stored in `_inputService`.
- In `Source/Stages/StallPuzzleScene.cs:100-121`:
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
- Directly observed: Upon modal dismissal, `GrayShadow_LetterRead` is set to `true`, `CutsceneEndedEvent("GrayShadow_Letter")` and `LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám")` are dispatched, and `RequestNextScene` is set to `true`.
- In `Source/Stages/HanoiExplorationScene.cs:284`, `_stallPuzzleModal ??= new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService, InputService);` correctly passes `InputService`.
- In `Source/Stages/HanoiExplorationScene.cs:240-249`, `_grayShadowLetterModal.Update()` also receives `isConfirmPressed` and `isSkipPressed` pulses, and synchronizes `GrayShadow_LetterRead = true`, dispatches both events, and pops `GameState.Cutscene`.

### 1.2 Long Biên Gate Interaction & Transition to World Map
- In `Source/Stages/HanoiExplorationScene.cs:289-301`:
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
- Because `GrayShadow_LetterRead` was set to `true` during `StallPuzzleScene`'s modal dismissal, subsequent interaction with the gate evaluates `if (!QuestFlags.GrayShadow_LetterRead)` as `false` and executes `RequestWorldMap = true;`.
- Directly observed: Player is NOT trapped in `GameState.Cutscene` and no duplicate letter modal is spawned.

### 1.3 PhoCookingEngine Post-Solve Mutation Invariant
- In `Source/Core/Puzzles/PhoCookingEngine.cs:69`:
  ```csharp
  public bool ValidateAssembly(out string errorMessage)
  {
      errorMessage = string.Empty;
      IsSolved = false;
      ...
  ```
- Directly observed: Mutating a previously solved bowl (removing an ingredient, adding invalid ingredients, or resetting) and calling `ValidateAssembly` correctly resets `IsSolved = false;`.

### 1.4 Automated Test Suite Execution
- Running `dotnet test Tests/GameProject.Tests.csproj` executes 410 automated unit and integration tests.
- Results: `Passed! - Failed: 0, Passed: 410, Skipped: 0, Total: 410, Duration: 226 ms`.

---

## 2. Logic Chain

1. **StallPuzzleScene Modal Synchronization**:
   - `StallPuzzleScene` now forwards `_inputService?.IsConfirmJustPressed` and `_inputService?.IsCancelJustPressed` to `_letterModal.Update()`.
   - In both headless test environments and graphical environments, when the modal receives confirm (Enter/Space) or is programmatically dismissed, `RequestClose` triggers.
   - Lines 107-109 and 116-118 unconditionally set `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true`, and publish `CutsceneEndedEvent` and `LetterOpenedEvent`.
   - Therefore, after solving `StallPuzzleScene` and dismissing the modal, `GrayShadow_LetterRead` is guaranteed to be `true`.

2. **Absence of Duplicate Cutscene Trap at Long Biên Gate**:
   - When the player returns from `StallPuzzleScene` to `HanoiExplorationScene` (via `StateManager.PopState()` on `_stallPuzzleModal.RequestNextScene`), `QuestFlags.Gate_LongBien_Unlocked == true` and `QuestFlags.GrayShadow_LetterRead == true`.
   - Interacting with `LongBienGateInteractable` and dismissing the confirmation dialogue executes line 298: `RequestWorldMap = true;`.
   - The branch `if (!QuestFlags.GrayShadow_LetterRead)` that previously spawned a duplicate letter modal is bypassed.
   - The state machine remains in `GameState.Exploration`, cleanly exposing `RequestWorldMap == true` for transition to the World Map.

3. **Event Dispatch Integrity & Idempotency**:
   - Empirical test `StallPuzzleScene_DismissalPublishesCutsceneEndedAndLetterOpenedEvents` verified that `CutsceneEndedEvent` and `LetterOpenedEvent` are published with exact payloads (`"GrayShadow_Letter"`, `"Letter_GrayShadow_Hanoi"`, `"Bóng Xám"`).
   - Calling `StallPuzzleScene.Update()` across 20 subsequent frames confirmed no duplicate events are fired.

4. **Persistence & Fallback Safety**:
   - Cross-session persistence test `Persistence_SolvedStateAndLetterRead_SurvivesSerializationAndTransfersToWorldMap` proved that `GrayShadow_LetterRead` is preserved across save/load JSON serialization and enables direct gate passage in a rehydrated session.
   - Test `LongBienGate_FallbackCutsceneFlow_WhenLetterReadWasInitiallyFalse_RecoversAndEnablesTransition` verified that even if a corrupted or legacy save lacked the `GrayShadow_LetterRead` flag, interacting with the gate presents the modal, allows dismissal, synchronizes the flag, and enables `RequestWorldMap = true` on the second interaction.

5. **PhoCookingEngine Invariant Integrity**:
   - Empirical test `PhoCookingEngine_PostSolveMutations_StrictlyResetIsSolved` confirmed that post-solve bowl mutations reset `IsSolved = false`.

---

## 3. Caveats

- **Raylib Headless vs GPU Rendering**: Automated tests run in headless mode using `MockInputService` and `NullAudioService`. Raylib window rendering and GPU draw calls were verified through headless mock state reflection and code inspection, which is consistent with the project's zero-display-server headless testing contract in `PROJECT.md`.

---

## 4. Conclusion & Verdict

**Verdict**: **APPROVE**

All four empirical criteria specified in the dispatch have been rigorously challenged and verified:
1. After `StallPuzzleScene` is solved and modal is dismissed, `GrayShadow_LetterRead` is `true`.
2. Interacting with Long Biên Gate immediately sets `RequestWorldMap = true` without duplicate cutscene trap.
3. Event publication (`CutsceneEndedEvent`, `LetterOpenedEvent`) is verified with exact arguments and zero spurious re-firings.
4. All 410 automated tests in the test suite pass cleanly (0 errors, 0 warnings, 226 ms).

---

## 5. Verification Method

To independently reproduce the empirical verification results:

```bash
# 1. Compile the solution (0 warnings, 0 errors)
dotnet build GameProject.csproj
dotnet build Tests/GameProject.Tests.csproj

# 2. Run the newly authored Challenger 2 progression empirical tests (9 tests)
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Milestone3Iteration2ProgressionEmpiricalTests"

# 3. Run the complete test suite (410 tests)
dotnet test Tests/GameProject.Tests.csproj
```

**Empirical Test Results**:
- `Milestone3Iteration2ProgressionEmpiricalTests`: 9 passed, 0 failed, duration 58 ms.
- Full test suite: 410 passed, 0 failed, 0 skipped, duration 226 ms.
