# Remediation Analysis & Drop-In Implementation Plan: StallPuzzleScene Headless Input Forwarding

## 1. Observation

### Codebase Observations
1. **Missing Input Service Injection in `StallPuzzleScene.cs`**:
   - Location: `Source/Stages/StallPuzzleScene.cs:22-31` & `71-82`:
     ```csharp
     public sealed class StallPuzzleScene : IScene
     {
         private readonly SaveData _saveData;
         private readonly QuestManager _questManager;
         private readonly IEventBus _eventBus;
         private readonly IAudioService? _audioService;
         private readonly LevelProgressionService _progression;
         private readonly SpatialGridEngine _grid = new();
         private readonly GrayShadowLetterModal _letterModal = new();
         ...
         public StallPuzzleScene(
             SaveData saveData,
             QuestManager questManager,
             IEventBus eventBus,
             IAudioService? audioService = null)
         {
             _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
             _questManager = questManager ?? throw new ArgumentNullException(nameof(questManager));
             _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
             _audioService = audioService;
             _progression = new LevelProgressionService(_saveData, _eventBus);
         }
     ```
   - Direct observation: `StallPuzzleScene` holds no `IInputService` field and the constructor does not accept any `IInputService` instance.

2. **Headless Modal Deadlock in `StallPuzzleScene.cs`**:
   - Location: `Source/Stages/StallPuzzleScene.cs:97-105`:
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
   - Location: `Source/UI/GrayShadowLetterModal.cs:39-67`:
     ```csharp
     public void Update(float dt, bool isConfirmPressed = false, bool isSkipPressed = false)
     {
         if (!IsActive) return;

         _timer += dt;

         bool confirm = isConfirmPressed;
         bool skip = isSkipPressed;

         if (Raylib.IsWindowReady())
         {
             confirm = confirm || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
             skip = skip || Raylib.IsKeyPressed(KeyboardKey.Space) || Raylib.IsKeyPressed(KeyboardKey.Enter) ||
                    (Raylib.IsMouseButtonPressed(MouseButton.Left) && !IsTypingComplete);
         }

         if (!IsTypingComplete)
         {
             TypewriterProgress += dt;
             if (skip || confirm)
             {
                 TypewriterProgress = LetterText.Length / 35.0f; // Instant fast-forward
             }
         }
         else if (confirm)
         {
             Dismiss();
         }
     }
     ```
   - Direct observation: In headless unit test environments, `Raylib.IsWindowReady()` evaluates to `false`. Because `StallPuzzleScene.Update(dt)` invokes `_letterModal.Update(dt)` without arguments, `isConfirmPressed` defaults to `false`. Consequently, `confirm` is permanently `false`, `Dismiss()` is unreachable via `Update`, `_letterModal.RequestClose` is never set to `true`, and headless executions are deadlocked indefinitely.

3. **Missing `GrayShadow_LetterRead` Sync & Event Emission**:
   - Location: `Source/Stages/StallPuzzleScene.cs:100-104` vs `Source/Stages/HanoiExplorationScene.cs:241-247`:
     In `HanoiExplorationScene.cs`:
     ```csharp
     if (_grayShadowLetterModal.RequestClose)
     {
         SaveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
         EventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
         EventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
         StateManager.PopState();
     }
     ```
     In `StallPuzzleScene.cs`:
     ```csharp
     if (_letterModal.RequestClose)
     {
         RequestNextScene = true;
     }
     ```
   - Direct observation: When `_letterModal` closes in `StallPuzzleScene`, `GrayShadow_LetterRead` is never set to `true`, and neither `CutsceneEndedEvent` nor `LetterOpenedEvent` is published. When returning to `HanoiExplorationScene`, interacting with `LongBienGate` triggers a duplicate cutscene rather than setting `RequestWorldMap = true`.
   - Furthermore, in `StallPuzzleScene.cs:112-115`:
     ```csharp
     if (_solveTimer >= 3.0f && !_letterModal.IsActive && !_saveData.Level1State.QuestFlags.GrayShadow_LetterRead)
     {
         _letterModal.Show();
     }
     ```
     Because `GrayShadow_LetterRead` remained `false`, dismissing `_letterModal` caused `_letterModal.Show()` to be re-triggered on subsequent frames if the scene was not immediately popped.

4. **Instantiation Site in `HanoiExplorationScene.cs`**:
   - Location: `Source/Stages/HanoiExplorationScene.cs:283`:
     ```csharp
     _stallPuzzleModal ??= new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService);
     ```
   - Direct observation: `HanoiExplorationScene` already maintains `public IInputService InputService { get; }`, but did not pass it to `StallPuzzleScene`.

5. **Existing Verification Command Results**:
   - `dotnet build GameProject.csproj` -> Build succeeded. 0 Warning(s), 0 Error(s).
   - `dotnet test Tests/GameProject.Tests.csproj` -> Passed: 391, Failed: 0, Total: 391, Duration: 234 ms.
   - `dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding"` -> Passed: 1 (verifies existing defect test in Challenger 1).
   - `dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap"` -> Passed: 1 (verifies existing defect test in Challenger Progression).

---

## 2. Logic Chain

1. **Premise**: PROJECT.md Section 8 and Section 106 require all game simulation logic, narrative cutscenes, and stage transitions to execute deterministically and headlessly in test runners without physical display hardware.
2. **Identification of Root Cause**:
   - `GrayShadowLetterModal.Update` was designed with parameters `bool isConfirmPressed = false, bool isSkipPressed = false` specifically to support headless testing and decoupled input.
   - However, `StallPuzzleScene` never took an `IInputService` and passed no parameters to `_letterModal.Update(dt)`.
   - Because `Raylib.IsWindowReady()` is `false` headlessly, `confirm` remains `false`. The modal can never advance or close via programmatic updates.
   - Furthermore, the cutscene lifecycle in `StallPuzzleScene` lacked the flag assignment `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true` and event publication (`CutsceneEndedEvent`, `LetterOpenedEvent`).
3. **Shortest Complete Solution (Lazy Senior Dev Mode)**:
   - In `StallPuzzleScene.cs`:
     - Add field `private readonly IInputService? _inputService;`.
     - Update constructor to accept optional `IInputService? inputService = null` as the 5th parameter (guaranteeing zero breaking changes to existing 3-parameter or 4-parameter call sites).
     - In `StallPuzzleScene.Update(float dt)`, forward `isConfirmPressed: _inputService?.IsConfirmJustPressed ?? false` and `isSkipPressed: _inputService?.IsCancelJustPressed ?? false`.
     - When `_letterModal.RequestClose` is true, assign `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;` and publish `CutsceneEndedEvent("GrayShadow_Letter")` and `LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám")`.
   - In `HanoiExplorationScene.cs`:
     - Pass `InputService` to `new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService, InputService);`.
     - In `HanoiExplorationScene.Update()`, forward `InputService.IsConfirmJustPressed` and `InputService.IsCancelJustPressed` to `_grayShadowLetterModal.Update(deltaTime, ...)`.
   - In Test Suite:
     - Update `StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding` to provide `MockInputService`, pulse confirm to dismiss modal, and assert `RequestNextScene == true` and `GrayShadow_LetterRead == true`.
     - Update `StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap` to assert that `RequestWorldMap == true` and `GameState.Exploration` is maintained without entering duplicate `GameState.Cutscene`.

---

## 3. Caveats

- **No Caveats**: The call sites for `StallPuzzleScene` are strictly limited to `HanoiExplorationScene.cs` and two unit test files (`Tests/StallPuzzleTests.cs`, `Tests/Milestone3Challenger1StressTests.cs`). Adding `IInputService? inputService = null` with a default `null` parameter introduces zero API breaking changes.
- Interactive gameplay with physical keyboard/mouse remains 100% functional because `RaylibInputService` maps `Enter`/`Space` to `IsConfirmJustPressed` and `Escape`/`Backspace` to `IsCancelJustPressed`, and `GrayShadowLetterModal` additionally falls back to direct Raylib key polling when `Raylib.IsWindowReady()` is true.

---

## 4. Conclusion & Drop-In Code Specification

### A. Changes to `Source/Stages/StallPuzzleScene.cs`

#### 1. Add private field:
```csharp
    private readonly IAudioService? _audioService;
    private readonly IInputService? _inputService;
    private readonly LevelProgressionService _progression;
```

#### 2. Update Constructor (Line 71):
```csharp
    public StallPuzzleScene(
        SaveData saveData,
        QuestManager questManager,
        IEventBus eventBus,
        IAudioService? audioService = null,
        IInputService? inputService = null)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _questManager = questManager ?? throw new ArgumentNullException(nameof(questManager));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _audioService = audioService;
        _inputService = inputService;
        _progression = new LevelProgressionService(_saveData, _eventBus);
    }
```

#### 3. Update `Update(float dt)` Modal Branch (Line 97-105):
```csharp
        if (_letterModal.IsActive)
        {
            bool isConfirm = _inputService?.IsConfirmJustPressed ?? false;
            bool isSkip = _inputService?.IsCancelJustPressed ?? false;
            _letterModal.Update(dt, isConfirmPressed: isConfirm, isSkipPressed: isSkip);

            if (_letterModal.RequestClose)
            {
                _saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
                _eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
                _eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
                RequestNextScene = true;
            }
            return;
        }
```

---

### B. Changes to `Source/Stages/HanoiExplorationScene.cs`

#### 1. Pass `InputService` to constructor at Line 283:
```csharp
_stallPuzzleModal ??= new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService, InputService);
```

#### 2. Forward `InputService` pulses to exploration cutscene modal at Line 240:
```csharp
        if (StateManager.CurrentState == GameState.Cutscene)
        {
            _grayShadowLetterModal.Update(
                deltaTime,
                isConfirmPressed: InputService.IsConfirmJustPressed,
                isSkipPressed: InputService.IsCancelJustPressed);
            if (_grayShadowLetterModal.RequestClose)
            {
                SaveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
                EventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
                EventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
                StateManager.PopState();
            }
            return;
        }
```

---

### C. Updates to Tests

#### 1. In `Tests/Milestone3Challenger1StressTests.cs:580-621`:
Update `StallPuzzleScene_HeadlessUpdate_TrappedInLetterModal_DueToMissingInputForwarding` to verify the remediated headless behavior:
```csharp
    [Fact]
    public void StallPuzzleScene_HeadlessUpdate_InputForwardingDismissesLetterModal()
    {
        var eventBus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(questManager);
        var mockInput = new GameProject.Core.Ports.MockInputService();

        var scene = new StallPuzzleScene(saveData, questManager, eventBus, audioService: null, inputService: mockInput);
        scene.Enter();

        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved);

        // Advance 3.5s -> triggers letter modal
        scene.Update(3.5f);

        var modalField = typeof(StallPuzzleScene).GetField("_letterModal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var letterModal = (GrayShadowLetterModal)modalField!.GetValue(scene)!;
        Assert.True(letterModal.IsActive);

        // Advance typewriter by pulsing confirm
        mockInput.PulseConfirm();
        scene.Update(0.1f);
        mockInput.ResetPulses();
        Assert.True(letterModal.IsTypingComplete);

        // Second confirm dismisses modal and completes transition
        mockInput.PulseConfirm();
        scene.Update(0.1f);
        mockInput.ResetPulses();

        Assert.False(letterModal.IsActive);
        Assert.True(scene.RequestNextScene);
        Assert.True(saveData.Level1State.QuestFlags.GrayShadow_LetterRead);
    }
```

#### 2. In `Tests/ChallengerM3ProgressionStressTests.cs:563-642`:
Update `StallPuzzle_To_GateTransition_RevealsDuplicateCutsceneTrap` (or update its assertions):
- When `stallModal` completes, `saveData.Level1State.QuestFlags.GrayShadow_LetterRead` is now `true`.
- In Step 5, interacting with `LongBienGate` asserts:
  ```csharp
  Assert.True(scene.RequestWorldMap);
  Assert.Equal(GameState.Exploration, sm.CurrentState);
  ```

---

## 5. Verification Method

1. **Build verification**:
   ```bash
   dotnet build GameProject.csproj
   dotnet build Tests/GameProject.Tests.csproj
   ```
2. **Execute Full Test Suite**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj
   ```
3. **Execute Targeted Stress Tests**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~StallPuzzleScene"
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~StallPuzzle_To_GateTransition"
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Milestone3Challenger1StressTests"
   ```
4. **Invalidation Conditions**:
   - `RequestNextScene` remaining `false` after modal typing completion and confirm input pulse in headless environment.
   - `GrayShadow_LetterRead` remaining `false` upon `_letterModal` dismissal.
   - `HanoiExplorationScene` re-entering `GameState.Cutscene` when interacting with Long Biên Gate after Stall Puzzle completion.
