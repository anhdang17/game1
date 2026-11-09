# Handoff Report: Milestone 4 Explorer 2 (Bút Chì Hint Engine & Companion Guide)

## 1. Observation
Direct codebase investigation of Milestone 4 hint and companion guide systems revealed the following exact facts:

1. **Core Domain Hint Service (`Source/Core/Hints/HintService.cs`)**:
   - `IHintService` defines `string GetCurrentHint(string activeQuestId, Level1State state)` and `bool CheckIdlePrompt(float idleSeconds, int failedAttemptsCount, out string idlePrompt)`.
   - `HintManager` implements `IdleThresholdSeconds = 45.0f`.
   - In Quest 1 (`QuestIds.Hanoi01`), `HintManager` only specifically branches on `!Q1_ThaoMoc_Collected` (points to "Lãn Ông") and `!Q1_HanhLa_Collected` (points to "gánh rau"). It lacks specific branches for `Q1_BanhPho_Collected` (Hàng Buồm), `Q1_ThitBo_Collected` (Bác Tư), `Q1_NuocDung_Collected` (Bếp Nước Dùng), and unstarted/completed quest states.
   - `E2EPlaythroughTests.cs` (lines 171-173) relies on `driver.HintManager.GetCurrentHint(QuestIds.Hanoi01, state)` returning a string containing `"Lãn Ông"` when no flags are collected. Any refactor must preserve this priority order.
   - In Quest 2 (`QuestIds.Hanoi02`), `HintManager` returns a static string: `"Hãy nhớ quy tắc nấu phở: Bánh phở ở đáy, thịt bò trải đều, rắc hành hoa, chan nước sôi rồi mới thêm gia vị!"`. It lacks step-by-step guidance for individual empty slots.
   - In Quest 3 (`QuestIds.Hanoi03`), `HintManager` returns a static hint mentioning `[R]` and `sắp xếp lại`, but lacks decoupled spatial orientation to Cầu Long Biên.

2. **Input Infrastructure (`Source/Core/Ports/IInputService.cs` & `Source/Graphics/RaylibInputService.cs`)**:
   - `IInputService` already specifies `bool IsHintJustPressed { get; }` (lines 22-23).
   - `RaylibInputService.cs` maps `IsHintJustPressed = Raylib.IsKeyPressed(KeyboardKey.H)` (line 45).
   - `MockInputService.cs` implements `PulseHint()` and `IsHintJustPressed`.

3. **Companion Character Visuals (`Source/Graphics/VectorRenderer.cs` lines 111-155)**:
   - `VectorRenderer.DrawButChi(Vector2 floatingPos, float animTime, float scale = 1.0f)` is fully implemented. It features:
     - Sinusoidal hover bobbing: `MathF.Sin(animTime * 3.5f) * 6f * scale`
     - Dynamic ground shadow scaling inversely with altitude
     - Sunny yellow hexagonal pencil body (`HanoiPalette.ButChiYellow`)
     - Metal ferrule collar, pink eraser, carved wood collar, and graphite tip
     - Glowing magical cyan sparkle tip with pulse animation
     - Blinking cartoon eyes (with open pupils and blinking slit state).

4. **Companion Guide UI Gap (`Source/UI/`)**:
   - `PROJECT.md` line 38 lists `Source/UI/ButChiGuideUI.cs` ("Companion portrait & hint bubble").
   - `Source/UI/` currently contains `DialogueBox.cs`, `GrayShadowLetterModal.cs`, `InteractionBubbleUI.cs`, `InventoryBar.cs`, `QuestHUD.cs`, but **`ButChiGuideUI.cs` has not yet been implemented**.
   - Neither `HanoiExplorationScene.cs` nor `PhoAssemblyScene.cs` integrates on-demand hint triggers `[H]` or tracks the 45-second idle timer.
   - `StallPuzzleScene.cs` had a hardcoded `KeyboardKey.H` message that was not wired to `IHintService`.

5. **Test Coverage (`Tests/HintTests.cs` lines 1-100)**:
   - 6 existing tests pass: `OnDemandHint_Quest1_MissingThaoMoc`, `OnDemandHint_Quest1_AllIngredientsCollected`, `OnDemandHint_Quest2_ExplainsPhoLayeringRule`, `OnDemandHint_Quest3_MentionsResetKeyWhenStuck`, `IdlePrompt_ActivatesAfter45SecondsOfInactivity`, and `IdlePrompt_ActivatesAfter3FailedPuzzleAttempts`.
   - Missing tests for individual missing ingredients (`BanhPho`, `ThitBo`, `NuocDung`, `HanhLa`), unstarted quest, completed quest, boundary conditions (44.9s vs 45.0s), and headless `ButChiGuideUI` liveness.

---

## 2. Logic Chain

From the observations above, the design follows four logical steps:

1. **Enhancing Core Domain Hint Rules (`Source/Core/Hints/HintService.cs`)**:
   - *Premise*: Domain logic must remain in `Source/Core/Hints/` with zero Raylib dependency.
   - *Quest 1 Coverage*: Added contextual branches for all 5 merchants (`Stall_BanhPho` on Hàng Buồm, `Stall_ThitBo` Bác Tư, `Stall_NuocDung`, `Stall_HanhLa` Cô Lan, `Stall_ThaoMoc` Lãn Ông).
   - *Backward Compatibility Invariant*: By checking `!Q1_ThaoMoc_Collected` first, initial state queries continue to return `"Lãn Ông"`, maintaining 100% compatibility with `E2EPlaythroughTests.cs` and `HintTests.cs`.
   - *Quest 2 Step-by-Step Guidance*: Added `GetPhoAssemblyHint(int emptySlotIndex, IReadOnlyList<string?> currentSlots)` returning specific culinary instructions for steps 1 through 5.
   - *Quest 3 Spatial Guidance*: Added `GetStallPuzzleHint(bool isPathClear, int moveCount)` providing sliding instructions (sạp hoa quả sang trái, xe hoa xuống dưới) pointing toward Cầu Long Biên, with `[R]` recovery reminder.

2. **Presentation Companion Guide (`Source/UI/ButChiGuideUI.cs`)**:
   - *Premise*: Clean Architecture requires visual rendering, animations, and input polling to reside in `Source/UI/`.
   - *Visual Layout*: Anchored at screen bottom-right `(1210, 650)`.
   - *Portrait & Bobbing*: Invokes `VectorRenderer.DrawButChi` with time-based bobbing and sparkle pulse.
   - *Keycap Badge*: Displays rounded `[H]` gold keycap tag on companion circular badge; responds to both `KeyboardKey.H`, `IInputService.IsHintJustPressed`, and pointer click.
   - *Idle Exclamation Alert*: When `idleSeconds >= 45.0f` or `failedAttemptsCount >= 3`, pulses an exclamation bubble `(!)` above Bút Chì and plays `SfxType.InteractPrompt`.
   - *Animated Speech Bubble*: Opens to the left of Bút Chì with smooth alpha fade, golden header tag ("Bút Chì Gợi Ý"), speech callout notch, and automatic word-wrapping via `FontManager.Instance.MeasureText`.
   - *Audio Feedback*: Plays `SfxType.DialogueBlip` on hint trigger and `SfxType.InteractPrompt` on idle nudge.
   - *Headless Safety*: All Raylib rendering and window queries are guarded by `Raylib.IsWindowReady()`, allowing full unit test instantiation and headless updates in xUnit tests.

3. **Progression Liveness & Anti-Spam (Debounce & Reset)**:
   - *Debounce*: 0.35s debounce cooldown on hint trigger prevents audio stutter and visual flickering when keys are held or spammed.
   - *Reset on Progress*: `NotifyProgress()` resets `_idleSeconds = 0f`, clears alerts, and dismisses exclamation when items are collected or objectives advance.
   - *Failure Accumulation*: `NotifyFailure()` increments failure counter, triggering prompt after 3 unsuccessful attempts.

4. **Integration into Exploration & Puzzle Stages**:
   - `HanoiExplorationScene.cs`: Instantiates `ButChiGuideUI`, subscribes `ItemCollectedEvent` to `NotifyProgress()`, resets idle timer during active character movement, and renders `ButChiGuide.Draw()` in screen-space HUD layer.
   - `PhoAssemblyScene.cs`: Binds `[H]` key to `GetPhoAssemblyHint`, notifying failure on invalid culinary assembly.
   - `StallPuzzleScene.cs`: Binds `[H]` key to `GetStallPuzzleHint`, providing dynamic moves toward Long Biên gate.

---

## 3. Caveats

1. **Initial Hint Invariant**:
   - `E2EPlaythroughTests.cs` (line 171) specifically tests:
     `Assert.Contains("Lãn Ông", clue);`
   - Therefore, `!state.QuestFlags.Q1_ThaoMoc_Collected` MUST be evaluated before other missing ingredients when all flags are false. Reordering this check would break existing regression tests.
2. **Vietnamese Glyph Encoding**:
   - All Vietnamese hint strings must use precomposed Unicode (Form C NFC) to render accurately with `FontManager.Instance`.
3. **Headless Execution Environment**:
   - In CI/CD headless test environments, Raylib window is NOT initialized. `ButChiGuideUI` must never invoke `Raylib.GetMousePosition()` or drawing commands unless `Raylib.IsWindowReady()` is true.
4. **Debounce vs Input Service**:
   - Both `_inputService.IsHintJustPressed` and direct `Raylib.IsKeyPressed(KeyboardKey.H)` are supported, with debounce timer `0.35s` to avoid double-triggers across scene modal boundaries.

---

## 4. Conclusion & Drop-In Code Specification

Three complete, drop-in specification files have been produced in `.agents/teamwork/teamwork_explorer_m4_2/`:
1. `proposed_HintService.cs` — Enhanced pure C# domain rules engine (`Source/Core/Hints/HintService.cs`).
2. `proposed_ButChiGuideUI.cs` — Presentation companion guide (`Source/UI/ButChiGuideUI.cs`).
3. `proposed_HintTests.cs` — Expanded 20-test automated verification suite (`Tests/HintTests.cs`).

### Drop-In Integration Code Snippets:

#### A. `Source/Stages/HanoiExplorationScene.cs`:
```csharp
// 1. Add fields:
public IHintService HintService { get; }
public ButChiGuideUI ButChiGuide { get; }

// 2. In constructor (add optional IHintService? hintService = null):
HintService = hintService ?? new HintManager();
ButChiGuide = new ButChiGuideUI(HintService, AudioService, InputService);
EventBus.Subscribe<ItemCollectedEvent>(_ => ButChiGuide.NotifyProgress());

// 3. In Enter():
ButChiGuide.ResetIdleTimer();

// 4. In Update(float deltaTime):
if (InputService.MovementDirection != Vector2.Zero)
{
    ButChiGuide.ResetIdleTimer();
}
ButChiGuide.Update(deltaTime, QuestManager.ActiveQuestId ?? QuestIds.Hanoi01, SaveData.Level1State);

// 5. In Draw() (screen-space HUD layer, after Raylib.EndMode2D()):
ButChiGuide.Draw(1280, 720);
```

#### B. `Source/Stages/PhoAssemblyScene.cs`:
```csharp
// In HandleInput():
if ((_inputService != null && _inputService.IsHintJustPressed) || Raylib.IsKeyPressed(KeyboardKey.H))
{
    int nextSlot = -1;
    for (int s = 0; s < PhoCookingEngine.TotalSlots; s++)
    {
        if (string.IsNullOrEmpty(_engine.CurrentSlots[s])) { nextSlot = s; break; }
    }
    _feedbackMessage = _hintService.GetPhoAssemblyHint(nextSlot, _engine.CurrentSlots);
    _feedbackTimer = 6f;
    _audioService?.PlaySfx(SfxType.DialogueBlip);
}
```

#### C. `Source/Stages/StallPuzzleScene.cs`:
```csharp
// In HandleInput():
if ((_inputService != null && _inputService.IsHintJustPressed) || Raylib.IsKeyPressed(KeyboardKey.H))
{
    _hintMessage = _hintService.GetStallPuzzleHint(_grid.IsPathClear(), _moveCount);
    _hintTimer = 6f;
    _audioService?.PlaySfx(SfxType.DialogueBlip);
}
```

---

## 5. Verification Method

### Test Commands:
```bash
# 1. Compile game and test projects
dotnet build /Users/mac/Projects/game1/GameProject.csproj
dotnet build /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj

# 2. Run dedicated hint unit tests
dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~HintTests"

# 3. Run complete test suite (regression check)
dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj
```

### Invalidation Conditions:
- If `OnDemandHint_Quest1_MissingThaoMoc_PointsToLanOngStreet` or `E2EPlaythroughTests.StuckPlayer_IdlePromptAndHintAssistanceFlow` fails, check that `!Q1_ThaoMoc_Collected` is evaluated before other uncollected items.
- If `ButChiGuideUI` crashes in headless test runner, check that all Raylib draw calls are guarded by `Raylib.IsWindowReady()`.
- If hint speech bubble text overflows screen boundaries, check that `DrawWrappedText` uses `FontManager.Instance.MeasureText` against `maxWidth`.
