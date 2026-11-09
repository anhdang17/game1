# Handoff Report: Milestone 3 Explorer 1 (Quest 2 Phở Assembly Mini-game UI & Visuals)

## 1. Observation

### 1.1 Existing Engine & Domain Logic
- **`Source/Core/Puzzles/PhoCookingEngine.cs` (lines 18–150)**:
  - Implements `IPhoPuzzleEngine` with `TotalSlots = 5`.
  - Canonical culinary sequence: `ItemIds.BanhPho` (0) → `ItemIds.ThitBo` (1) → `ItemIds.HanhLa` (2) → `ItemIds.NuocDung` (3) → `ItemIds.ThaoMoc` or `ItemIds.GiaVi` (4).
  - Validation: `ValidateAssembly(out string errorMessage)` checks emptiness, duplicates (`distinctCount != 5`), and step-by-step order, returning contextual Vietnamese guidance from Bút Chì when out of sequence (e.g. line 94: `"Nước dùng mà chan trước khi cho bánh phở thì bánh sẽ bị nát và nguội mất, Mây ơi! Đặt bánh phở tươi xuống đáy bát trước nhé!"`).
  - `PlaceIngredient(slotIndex, ingredientId)` and `RemoveIngredient(slotIndex)` mutate slot array; `ResetPuzzle()` clears all 5 slots.
  - Tests in `Tests/PhoPuzzleTests.cs` (lines 12–195) fully verify correct sequences, alternative garnish (`ItemIds.GiaVi`), duplicate rejection, and adversarial permutation rejection (120 permutations: 1 valid, 119 rejected).

### 1.2 Existing Stage Implementation & Missing Integrations
- **`Source/Stages/PhoAssemblyScene.cs` (lines 20–447)**:
  - Already implemented as an `IScene`, but is currently an isolated orphan:
    - Never instantiated or referenced in `Program.cs` or `HanoiExplorationScene.cs`.
    - Line 22–27: Missing `IAudioService` dependency. Does not play any sound effects (`PhoIngredientPlaced`, `PhoIngredientRemoved`, `PhoCookSuccess`, `PhoCookFailed`, `PuzzleReset`, `ColorBloomWave`) or background music (`PhoPuzzleTheme`).
    - Line 108–157: Directly queries hardware via `Raylib.IsKeyPressed()` and `Raylib.GetMousePosition()`, breaking headless test execution and bypassing the existing `IInputService` abstraction.
    - Line 18–19: Contains outdated comment `"When solved, fires PhoAssemblyCompletedEvent and signals readiness to transition → StallPuzzleScene."`, whereas the specification requires returning to Hanoi street exploration with color bloom wave and Bà An dialogue unlocked.
    - Line 266–290: Bowls and slots are rendered using basic Raylib shapes (`Raylib.DrawEllipse`, `Raylib.DrawRing`) without the iconic Bát Tràng blue-and-white porcelain motifs or dynamic food layering inside the bowl.

### 1.3 Exploration Scene & Dialogue States
- **`Source/Stages/HanoiExplorationScene.cs` (lines 194–230)**:
  - Interacting with Bà An (`BaAnNpc`) completes Quest 1 (`Q1_Completed = true`), but subsequent interactions with Bà An return dialogue `"Bà cháu mình cùng vào bàn ghép bát phở chuẩn vị Hà Nội nào cháu yêu!"` without transitioning game state or launching `PhoAssemblyScene`.
  - Has `StateManager` (`GameStateManager`) initialized to `GameState.Exploration`.
  - `GameState.MiniGamePho` already exists in `Source/Core/State/GameState.cs` (line 18) and is supported by `GameStateManager.PushState/PopState`, but is currently uncalled.
  - `ColorBloomEffect.cs` (lines 99–128): Supports `TriggerWave(Vector2 epicenter, float targetPercent, float duration = 1.5f)` using a GLSL 330 desaturation ripple shader with CPU fallback.
  - Currently in `HanoiExplorationScene.cs` line 225, `BloomEffect.TriggerWave` is prematurely fired upon Quest 1 turn-in, whereas PROJECT.md Feature 12 assigns the +33.3% color bloom to Quest 2 Phở Assembly resolution.

### 1.4 Quest & Progression Contracts
- **`Source/Core/Quests/HanoiQuests.cs` (lines 33–44)**:
  - `Quest2Id = "Quest_Hanoi_02"`, Title: `"Ghép đúng Bát Phở hoàn chỉnh"`, Objective: `new("assemble_pho", "Xếp các lớp phở đúng thứ tự 5 bước", targetCount: 1)`.
  - Prerequisite: `Quest_Hanoi_01`.
- **`Source/Core/Quests/LevelProgressionService.cs` (lines 24–34)**:
  - `OnQuest2Completed()`: Sets Hanoi regional color restoration to `33.3f`, sets `_saveData.Level1State.QuestFlags.Q2_Completed = true`, and publishes `RegionColorUpdatedEvent` and `ColorRestoredEvent`.
- **`Source/Core/Models/QuestFlags.cs` (lines 33–40)**:
  - Flags: `Q2_Started`, `Q2_PhoAssembled`, `Q2_Completed`.
- **`Source/Core/Events/GameEvents.cs` (lines 20–23)**:
  - Events defined: `PhoIngredientPlacedEvent`, `PhoAssemblyAttemptedEvent`, `PhoAssemblyCompletedEvent`, `PhoPuzzleResetEvent`.

---

## 2. Logic Chain

1. **State Preservation**: In `Program.cs`, `HanoiExplorationScene` holds player position, collision world, camera tracker, and street state. Switching scenes via `SceneManager.SwitchScene` destroys and disposes `HanoiExplorationScene`. Therefore, the Phở mini-game must be presented as a **modal overlay** on top of `HanoiExplorationScene` using `GameStateManager.PushState(GameState.MiniGamePho)` and `PopState()`. This directly satisfies PROJECT.md Feature 10 ("Modal interface with porcelain bowl, ingredient cards, slots").
2. **Transition Trigger**: When Quest 1 is completed (`Q1_Completed = true`) and Quest 2 is pending (`!Q2_Completed`), talking to Bà An must display prompt `"[E] Ghép Bát Phở"`. Dismissing this dialogue pushes `GameState.MiniGamePho`, initiating the mini-game. If dismissed without solving, player returns to exploration; talking to Bà An re-opens the modal.
3. **Audio & Input Decoupling**: Injecting `IAudioService` and `IInputService` into `PhoAssemblyScene` (or an overlay controller) ensures:
   - Full SFX playback (`PhoIngredientPlaced`, `PhoIngredientRemoved`, `PhoCookSuccess`, `PhoCookFailed`, `PuzzleReset`, `ColorBloomWave`) and BGM (`PhoPuzzleTheme`).
   - 100% headless testability in xUnit without requiring native Raylib window handles.
4. **Authentic Visual Presentation**: The porcelain bowl represents Bát Tràng blue-and-white ceramics (gốm men lam). Progressive vector rendering inside the bowl cavity gives immediate visual satisfaction:
   - Placing noodles renders ivory noodle folds at the base.
   - Placing beef renders thin pink-marbled slices.
   - Placing scallions renders green scallion rings.
   - Placing broth renders a warm translucent golden broth glaze with steam curls.
   - Placing herbs/spices renders the star anise flower at center.
5. **Reward & Climax Resolution**: Upon successful validation (`ValidateAssembly` returns true):
   - `Q2_PhoAssembled = true`, `Q2_Completed = true`.
   - `QuestManager.RecordObjectiveProgress("assemble_pho", 1)` and `QuestManager.CompleteQuest("Quest_Hanoi_02")`.
   - `LevelProgressionService.OnQuest2Completed()` updates Hanoi color restoration to +33.3%.
   - `PhoAssemblyCompletedEvent` is published.
   - Celebration banner with lotus stamp pops up for ~2.5s.
   - Modal closes (`PopState()`), returning to street exploration.
   - `BloomEffect.TriggerWave(BaAn.Position, 33.3f, 2.0f)` sweeps vibrant colors across Hanoi Old Quarter.
   - Bà An's dialogue unlocks to State 4, directing Mây toward the Long Biên bridge stalls (Quest 3).

---

## 3. Class Designs, State Machine & UI Specifications

### 3.1 State Machine & Scene Flow

```
[ Exploration (Street) ]
        │
        ▼ (Q1 turned in: Q1_Completed = true, Q2_Completed = false)
[ Bà An Interaction: "[E] Ghép Bát Phở" ]
        │
        ▼ (Dialogue dismissed)
[ PushState(GameState.MiniGamePho) ] ───────────┐
        │                                        │
        ▼                                        │ (Press [Esc] to exit)
[ Pho Assembly Modal Active ]                    │
   ├── Select Card (1-5 or Click)                │
   ├── Place in Slot (Slot 1-5 or Click)         │
   ├── Remove from Slot (Right-click or Click)   │
   ├── Reset ([R] or Reset Button)               │
   └── Validate ([Enter] or Validate Button)     │
        │                                        │
        ├── [Invalid] -> Shake + Sfx + Hint      │
        │                                        │
        └── [Valid]                              │
              ├── Sfx.PhoCookSuccess + Sfx.ColorBloomWave
              ├── QuestManager.CompleteQuest(Quest2Id)
              ├── Progression.OnQuest2Completed() (+33.3%)
              ├── Celebration Lotus Stamp Banner (2.5s)
              ▼
[ PopState() -> Return to Exploration ] <────────┘
        │
        ▼
[ BloomEffect.TriggerWave(BaAn.Position, 33.3f, 2.0f) ]
        │
        ▼
[ Bà An State 4 Dialogue Unlocked -> Direct to Quest 3 Long Biên ]
```

### 3.2 Modal Screen Coordinates & Layout

| Component | Center / Rect Coordinates | Dimensions | Visual Style / Assets |
|---|---|---|---|
| **Modal Backdrop** | `(0, 0, 1280, 720)` | `1280 x 720` | Frosted dark overlay: `rgba(20, 16, 12, 190)` |
| **Title Header** | Center `(640, 36)` | Text size 28px | `HanoiPalette.WallYellow` with `CartoonOutline` (2.5px) |
| **Porcelain Bowl Body** | Center `(640, 290)` | Radius X: 155, Y: 95 | Bát Tràng white porcelain `#FFFDF5`, outer cobalt ring `#1A56A8` |
| **Bowl Inner Broth Well** | Center `(640, 295)` | Radius X: 135, Y: 75 | Base ivory; golden broth tint `#D69E2E` (alpha 0.45) when broth placed |
| **5 Pentagon Slots** | Radius 92px from `(640, 290)`: <br>• Slot 0: `(640, 198)`<br>• Slot 1: `(727, 261)`<br>• Slot 2: `(694, 364)`<br>• Slot 3: `(586, 364)`<br>• Slot 4: `(553, 261)` | Radius: 32px | Porcelain disc, cartoon outline (2px). Shows slot number `①..⑤`, role subtitle, ingredient sprite when placed |
| **Bút Chì Companion Avatar**| Center `(1060, 200)` | 40 x 60px | Floating animated pencil with bobbing motion, displaying hint bubble |
| **Feedback Toast Banner** | `(340, 435, 600, 42)` | `600 x 42` | Green `#38A169` on success; Terracotta red `#E53E3E` on error with shake |
| **Validate Button** | `(520, 490, 240, 46)` | `240 x 46` | Emerald `#38A169`, text `[Enter] Xác nhận bát phở` |
| **Reset Button** | `(390, 495, 110, 36)` | `110 x 36` | Light ochre `#ECC94B`, text `[R] Làm lại` |
| **Exit Button** | `(780, 495, 110, 36)` | `110 x 36` | Neutral gray `#A0AEC0`, text `[Esc] Thoát` |
| **Ingredient Card Tray** | `(120, 560, 1040, 130)`| `1040 x 130`| Bamboo mat `#D7B476` with rounded corners (0.3) |
| **5 Ingredient Cards** | Y = 620, Width 140, Height 90: <br>• Card 0: `X = 220`<br>• Card 1: `X = 390`<br>• Card 2: `X = 560`<br>• Card 3: `X = 730`<br>• Card 4: `X = 900` | `140 x 90` | Cream rounded box with key badge `[1]..[5]`, vector icon, Vietnamese name |

### 3.3 Enhanced Porcelain Phở Bowl Procedural Visuals

```csharp
// Vector rendering pseudo-code for Vietnamese Bát Tràng Porcelain Bowl:
public static void DrawBatTrangPorcelainBowl(Vector2 center, float radiusX, float radiusY, IReadOnlyList<string?> slots, float animTime)
{
    // 1. Soft contact shadow
    Raylib.DrawEllipse((int)center.X + 4, (int)center.Y + 12, radiusX + 10, radiusY * 0.6f + 8, Raylib.ColorAlpha(Color.Black, 0.22f));

    // 2. Bowl porcelain outer hull (Off-white ceramic)
    Raylib.DrawEllipse((int)center.X, (int)center.Y, radiusX, radiusY, new Color(255, 253, 245, 255));
    Raylib.DrawEllipseLines((int)center.X, (int)center.Y, radiusX, radiusY, HanoiPalette.CartoonOutline);

    // 3. Bát Tràng cobalt blue decorative band (Hoa lam)
    Raylib.DrawEllipseLines((int)center.X, (int)center.Y - 2, radiusX - 6, radiusY - 4, new Color(26, 86, 168, 200));
    // 16 decorative cobalt dot accents along rim
    for (int i = 0; i < 16; i++)
    {
        float angle = i * (MathF.PI * 2f / 16f);
        Vector2 pt = center + new Vector2(MathF.Cos(angle) * (radiusX - 10), MathF.Sin(angle) * (radiusY - 6));
        Raylib.DrawCircleV(pt, 2.5f, new Color(26, 86, 168, 220));
    }

    // 4. Inner cavity well
    Raylib.DrawEllipse((int)center.X, (int)center.Y + 6, radiusX - 22, radiusY - 16, new Color(248, 244, 230, 255));

    // 5. Dynamic Layering based on placed ingredients:
    bool hasNoodles = slots.Contains(ItemIds.BanhPho);
    bool hasMeat = slots.Contains(ItemIds.ThitBo);
    bool hasHerbs = slots.Contains(ItemIds.HanhLa);
    bool hasBroth = slots.Contains(ItemIds.NuocDung);
    bool hasSpices = slots.Contains(ItemIds.ThaoMoc) || slots.Contains(ItemIds.GiaVi);

    if (hasNoodles)
    {
        // Render ivory noodle ribbon folds at bowl bottom
        DrawNoodleBed(center + new Vector2(0, 10), radiusX - 35, radiusY - 26);
    }
    if (hasMeat)
    {
        // Render thin red-marbled beef slices
        DrawBeefSlices(center + new Vector2(0, 4));
    }
    if (hasHerbs)
    {
        // Render scattered green scallion rings and cilantro
        DrawScallionSprinkles(center + new Vector2(0, 2), animTime);
    }
    if (hasBroth)
    {
        // Translucent golden broth glaze covering interior + rising steam curls
        Raylib.DrawEllipse((int)center.X, (int)center.Y + 6, radiusX - 24, radiusY - 18, Raylib.ColorAlpha(new Color(214, 158, 46, 255), 0.40f));
        DrawSteamWisps(center, animTime);
    }
    if (hasSpices)
    {
        // Star anise flower garnish at center
        DrawStarAniseGarnish(center + new Vector2(0, -6));
    }
}
```

### 3.4 Integration Contract with `HanoiExplorationScene`

In `HanoiExplorationScene.cs`:
1. **Field Addition**:
   ```csharp
   private PhoAssemblyScene? _phoAssemblyModal;
   ```
2. **Interaction Trigger** (in dialogue dismissal logic):
   ```csharp
   if (_activeDialogue != null)
   {
       if (InputService.IsInteractJustPressed || InputService.IsConfirmJustPressed || InputService.IsCancelJustPressed)
       {
           AudioService.PlaySfx(SfxType.ButtonClick);
           bool launchPhoMiniGame = (_activeDialogue.DialogueSpeaker == BaAn.DisplayName &&
                                     QuestFlags.Q1_Completed && !QuestFlags.Q2_Completed);
           _activeDialogue = null;

           if (launchPhoMiniGame)
           {
               _phoAssemblyModal ??= new PhoAssemblyScene(SaveData, QuestManager, EventBus, AudioService, InputService);
               _phoAssemblyModal.Enter();
               StateManager.PushState(GameState.MiniGamePho);
           }
       }
   }
   ```
3. **Modal Update & Resolution** (in `Update`):
   ```csharp
   if (StateManager.CurrentState == GameState.MiniGamePho && _phoAssemblyModal != null)
   {
       _phoAssemblyModal.Update(deltaTime);

       if (_phoAssemblyModal.RequestExit)
       {
           if (_phoAssemblyModal.IsSolved)
           {
               // Trigger color bloom wave +33.3% centered on Ba An's stall
               BloomEffect.TriggerWave(BaAn.Position, 33.3f, 2.0f);
               AudioService.PlaySfx(SfxType.ColorBloomWave);
           }
           StateManager.PopState();
       }
       return; // Suspend kinematics & exploration movement during mini-game
   }
   ```
4. **Modal Rendering** (in `Draw`):
   ```csharp
   // Exploration background is rendered normally through bloom effect
   // Then Screen-Space UI is drawn:
   if (StateManager.CurrentState == GameState.MiniGamePho && _phoAssemblyModal != null)
   {
       _phoAssemblyModal.Draw();
   }
   ```
5. **Bà An Post-Q2 Dialogue** in `HanoiInteractables.cs`:
   ```csharp
   // State 4: Quest 2 Completed, Quest 3 Not Started
   if (context.QuestFlags.Q2_Completed)
   {
       return new InteractionResult(
           Success: true,
           DialogueSpeaker: DisplayName,
           DialogueText: "Bát phở thơm ngon chuẩn vị Hà Nội của bà cháu mình đây rồi! Cảm ơn bé Mây nhiều lắm! Giờ cháu hãy qua chỗ cổng ra Cầu Long Biên xem sao nhé, nghe nói mấy gian hàng đang che mất lối đi rồi đấy!"
       );
   }
   ```

---

## 4. Caveats

1. **Dual Scene Usage**: While the primary gameplay runs `PhoAssemblyScene` as an overlay modal inside `HanoiExplorationScene`, `PhoAssemblyScene` retains full `IScene` compliance so it can also be launched standalone via `SceneManager.SwitchScene` for developer test rigs.
2. **Audio Mock Fallback**: In headless test environments (`NullAudioService`), sound effect triggers are safely no-ops, but for verified integration tests, `RecordingAudioService` logs all emitted SFX.
3. **Input Hardware Independence**: Number keys `[1]..[5]` are queried through `RaylibInputService` during live execution, but `PhoAssemblyScene` must accept direct programmatic slot placement methods (`PlaceCard(slotIndex, cardIndex)`) so unit and integration tests run headlessly in under 10 ms.
4. **Font Normalization**: All Vietnamese text displayed in the mini-game must pass through `FontManager.NormalizeText()` to ensure NFD decomposed strings do not cause missing glyph rendering artifacts.

---

## 5. Conclusion

1. **Complete Alignment**: The design fully integrates `PhoCookingEngine`, `PhoAssemblyScene`, `HanoiExplorationScene`, `QuestManager`, and `ColorBloomEffect`.
2. **Lazy Senior Architecture**:
   - Reuses existing `PhoCookingEngine` and `InventoryBar.DrawIngredientIcon` without creating duplicate models or redundant math classes.
   - Reuses `GameStateManager.PushState/PopState` for clean modal lifecycle.
   - Eliminates scene teardown/rebuild overhead by overlaying the modal on the live street scene.
3. **Exact Climax Progression**: Solving the assembly activates Quest 2 completion, triggers the +33.3% color bloom ripple wave across Hanoi Old Quarter, updates save flags, and unlocks Bà An's dialogue pointing to Quest 3 Long Biên Bridge stalls.

---

## 6. Verification Method

### 6.1 Automated Headless Test Suite (`xUnit`)
Execute:
```bash
dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Pho"
```

### 6.2 Proposed Test Cases (to add in `Tests/PhoAssemblySceneTests.cs`)
1. **`PhoAssembly_CompleteSequence_SetsQ2CompletedAnd33PercentColor`**:
   - Sets up `SaveData`, `QuestManager`, `EventBus`, `SimulationDriver`.
   - Starts Quest 2, places ingredients 0..4 in order (`BanhPho` → `ThitBo` → `HanhLa` → `NuocDung` → `ThaoMoc`).
   - Validates assembly: asserts `IsSolved == true`, `Q2_Completed == true`, `Q2_PhoAssembled == true`.
   - Asserts `WorldProgression.GetColorRestoration("Hanoi") == 33.3f`.
   - Asserts `PhoAssemblyCompletedEvent` published.
2. **`PhoAssembly_BrothBeforeNoodles_RejectsWithButChiWarning`**:
   - Places `NuocDung` in slot 0, `BanhPho` in slot 1.
   - Validates: asserts `IsSolved == false`, `errorMsg` contains `"Nước dùng mà chan trước khi cho bánh phở"`.
   - Asserts `Q2_Completed == false`.
3. **`PhoAssembly_AudioFeedback_EmitsSuccessAndBloomSfx`**:
   - Uses `RecordingAudioService`.
   - Completes puzzle: asserts `RecordingAudioService.PlayedSfx` contains `SfxType.PhoCookSuccess` and `SfxType.ColorBloomWave`.
4. **`PhoAssembly_Reset_ClearsEngineAndUI`**:
   - Places 3 ingredients, calls reset: asserts all slots null, `selectedCard == -1`.
5. **`PhoAssembly_SaveLoad_HydratesQ2FlagsProperly`**:
   - Saves state with `Q2_Completed = true`, reloads from JSON: asserts `QuestFlags.Q2_Completed == true` and `QuestManager.GetQuestState("Quest_Hanoi_02") == QuestState.Completed`.

### 6.3 Invalidation Conditions
- If `dotnet test` takes `> 500 ms` or fails on headless execution (e.g., throwing null references on display server or sound hardware).
- If color restoration percent fails to update to `33.3f` upon Quest 2 solve.
- If Bà An's prompt or dialogue fails to update to State 4 after Quest 2 completion.
