# Milestone 3 Explorer 3 Handoff Report: Level Progression Climax, Rewards & Cutscene

**Folder**: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_3/`  
**Mission**: Investigate and design the implementation for Feature 17 (Bridge Gate Portal Transition), Feature 18 (Memory Journal Page 1 Award), Feature 19 (Lotus Stamp Award), and Feature 20 (Gray Shadow Letter Cutscene & Auto-Save).  
**Constraint**: Read-only exploration. No source code modifications.

---

## 1. Observation

### 1.1 Existing Codebase Components
Direct inspection of the repository reveals the current structural and behavioral foundations:

1. **LevelProgressionService (`Source/Core/Quests/LevelProgressionService.cs:35-71`)**:
   ```csharp
   public void CompleteLevel1()
   {
       // 1. 100% Color Bloom
       float oldColor = _saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHanoi);
       _saveData.WorldProgression.SetColorRestoration(WorldProgression.RegionHanoi, 100.0f);
       _saveData.WorldProgression.MarkRegionCompleted(WorldProgression.RegionHanoi);

       _eventBus?.Publish(new RegionColorUpdatedEvent(WorldProgression.RegionHanoi, 100.0f));
       _eventBus?.Publish(new ColorRestoredEvent(WorldProgression.RegionHanoi, oldColor, 100.0f, HanoiQuests.Quest3Id));
       _eventBus?.Publish(new RegionCompletedEvent(WorldProgression.RegionHanoi));

       // 2. Gate Unlock
       _saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
       _saveData.Level1State.QuestFlags.Q3_Completed = true;
       _eventBus?.Publish(new GateUnlockedEvent("Gate_LongBien", WorldProgression.RegionHanoi));

       // 3. Award Memory Journal Page #1
       _saveData.CollectionAlbum.AddJournalPage(AlbumIds.HanoiJournalPage);
       _eventBus?.Publish(new JournalPageAwardedEvent(AlbumIds.HanoiJournalPage, "Hương Vị Phố Cổ & Nhịp Cầu Thời Gian", WorldProgression.RegionHanoi));

       // 4. Award Lotus Stamp #1
       _saveData.CollectionAlbum.AddStamp(AlbumIds.HanoiLotusStamp);
       _eventBus?.Publish(new StampAwardedEvent(AlbumIds.HanoiLotusStamp, "Tem Hoa Sen Hà Nội", WorldProgression.RegionHanoi));

       // 5. Unlock Hue Region
       _saveData.WorldProgression.UnlockRegion(WorldProgression.RegionHue);
       _eventBus?.Publish(new RegionUnlockedEvent(WorldProgression.RegionHue));

       // 6. Trigger Gray Shadow letter narrative
       _saveData.Level1State.QuestFlags.Level1_Finished = true;
       _eventBus?.Publish(new CutsceneTriggeredEvent("GrayShadow_Letter"));
   }
   ```
   *Observed Gap*: `CompleteLevel1()` executes steps 1–6, but does **not** invoke `SaveLoadService.Save(_saveData)` directly. Auto-save is currently delegated to external callers (e.g. `TestSimulationDriver.SimulateLevelClimax()` in `Tests/Mocks/TestSimulationDriver.cs:231`).

2. **CollectionAlbum Model (`Source/Core/Models/CollectionAlbum.cs:23-58`)**:
   ```csharp
   public sealed class CollectionAlbum
   {
       private List<string> _collectedJournalPages = new();
       private List<string> _collectedStamps = new();
       private List<string> _unlockedFactCards = new();

       public List<string> CollectedJournalPages { ... }
       public List<string> CollectedStamps { ... }
       public List<string> UnlockedFactCards { ... }
   ```
   *Observed Gap*: Properties are named `CollectedJournalPages` and `CollectedStamps`. However, dispatch specifications reference `CollectionAlbum.JournalPages` and `CollectionAlbum.TravelStamps`. Without `[JsonIgnore]` alias properties, accessing those names causes compilation failure.

3. **Album Rich Metadata Service (`Source/Core/Album/AlbumService.cs:8-35, 62-90`)**:
   `AlbumService` maps IDs from `CollectionAlbum` to rich `JournalPage` and `TravelStamp` objects:
   - `JournalPage` has `Id`, `Title`, `Subtitle`, `Region`, `StoryText`, `IllustrationKey`.
   - `TravelStamp` has `Id`, `Name`, `Subtitle`, `Region`, `DateEarned`, `PostmarkText`, `IconKey`.
   - Pre-populated Hanoi items:
     - Page: `AlbumIds.HanoiJournalPage` (`"JournalPage_Hanoi_01"`), `"Hương Vị Phố Cổ & Nhịp Cầu Thời Gian"`, `IllustrationKey = "Art_Journal_Hanoi_01"`.
     - Stamp: `AlbumIds.HanoiLotusStamp` (`"Stamp_HoaSen_Hanoi"`), `"Tem Hoa Sen Hà Nội"`, `PostmarkText = "HÀ NỘI - 1000 NĂM VĂN HIẾN"`.

4. **Long Biên Gate in Exploration Scene (`Source/Stages/HanoiExplorationScene.cs:430-442`)**:
   ```csharp
   // Distant Long Biên Bridge steel trusses silhouette on right edge
   Vector2[] girderPoints =
   [
       new(1350, 110), new(1420, 40), new(1490, 110),
       new(1490, 110), new(1560, 40), new(1600, 110)
   ];
   ```
   *Observed Gap*: The Long Biên bridge is rendered only as background silhouette at the top-right. There is **no interactable portal entity** (`IInteractable`) registered at the right street boundary (`x = 1480f, y = 480f`) to provide player feedback or trigger the transition into `WorldMapScene`.

5. **StallPuzzleScene Climax Trigger (`Source/Stages/StallPuzzleScene.cs:186-195`)**:
   ```csharp
   private void TriggerSolve()
   {
       _solved = true;
       _solveTimer = 0f;
       _saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
       _questManager.RecordObjectiveProgress(HanoiQuests.Quest3Id, "clear_long_bien_path");
       _questManager.CompleteQuest(HanoiQuests.Quest3Id);
       _progression.CompleteLevel1();
       _eventBus.Publish(new StallPuzzleSolvedEvent(_moveCount));
   }
   ```
   When solved, `StallPuzzleScene` draws a temporary solve banner and lotus circle/stamp icons, but does not display the full Gray Shadow narrative letter modal with typewriter animation before transitioning.

6. **Durable Persistence Pipeline (`Source/Core/Persistence/SaveLoadService.cs:50-86`)**:
   - `SaveLoadService.SaveGame(SaveData data)` guarantees atomic persistence via temporary file write (`.tmp`) followed by atomic swap (`ReplaceFile`) with `.bak` backup retention.
   - Preserves UTF-8 Vietnamese strings via `SaveGameJsonContext.CreateOptions()` (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping`).

7. **Test Suite Baseline**:
   - Running `dotnet test Tests/GameProject.Tests.csproj` executed **303 passing tests in 226 ms** with zero errors and zero warnings.

---

## 2. Logic Chain

From the observations above, the design for Features 17, 18, 19, and 20 proceeds in a strict 5-stage progression:

### Stage 1: Trigger Condition & Quest Climax Verification
- **Prerequisite**: All 3 quests in Hanoi must be verified complete:
  - `QuestFlags.Q1_Completed == true` (Ingredients gathered & returned to Bà An)
  - `QuestFlags.Q2_Completed == true` (Authentic 5-layer phở bowl assembled)
  - `QuestFlags.Q3_Completed == true` (Spatial grid stalls cleared, path to Long Biên opened)
- **Coordinator**: `LevelProgressionService.CompleteLevel1()` is the centralized domain method coordinating all climax milestones atomically.

### Stage 2: Feature 18 — Memory Journal Page 1 Award
- **Target**: Award `"Trang Sổ 1: Ký ức Phố Cổ Hà Nội"` into `CollectionAlbum`.
- **Model Additions**:
  In `Source/Core/Models/CollectionAlbum.cs`, add convenience property aliases:
  ```csharp
  [JsonIgnore]
  public List<string> JournalPages => CollectedJournalPages;
  ```
- **Reward Registration in `AlbumService.cs`**:
  ```csharp
  public static readonly JournalPage HanoiPage1 = new()
  {
      Id = AlbumIds.HanoiJournalPage,
      Title = "Trang Sổ 1: Ký ức Phố Cổ Hà Nội",
      Subtitle = "Hương Vị Phố Cổ & Nhịp Cầu Thời Gian",
      Region = RegionIds.Hanoi,
      StoryText = "Hà Nội - Những ngày thu thơm mùi hoa sữa và ấm nồng nước phở. Khi sắc màu trở lại, những nụ cười trên phố cổ lại bừng sáng dưới nhịp cầu Long Biên trăm năm lịch sử. Bé Mây và Bút Chì đã giúp cả khu phố tìm lại những ký ức ngọt ngào nhất.",
      IllustrationKey = "Art_Journal_Hanoi_01"
  };
  ```
- **Award Action**:
  - `_saveData.CollectionAlbum.AddJournalPage(AlbumIds.HanoiJournalPage);`
  - Publish `JournalPageAwardedEvent(AlbumIds.HanoiJournalPage, HanoiPage1.Title, RegionIds.Hanoi);`

### Stage 3: Feature 19 — Lotus Stamp Award
- **Target**: Award `"Tem Hoa Sen Hà Nội"` into `CollectionAlbum` with timestamp.
- **Model Additions**:
  In `Source/Core/Models/CollectionAlbum.cs`:
  ```csharp
  [JsonIgnore]
  public List<string> TravelStamps => CollectedStamps;
  ```
- **Reward Registration in `AlbumService.cs`**:
  ```csharp
  public static readonly TravelStamp HanoiLotusStamp = new()
  {
      Id = AlbumIds.HanoiLotusStamp,
      Name = "Tem Hoa Sen Hà Nội",
      Subtitle = "Biểu Tượng Tinh Hoa Đất Thăng Long",
      Region = RegionIds.Hanoi,
      DateEarned = DateTime.UtcNow.ToString("dd/MM/yyyy"),
      PostmarkText = "HÀ NỘI - 1000 NĂM VĂN HIẾN",
      IconKey = "Icon_Stamp_HoaSen"
  };
  ```
- **Award Action**:
  - `_saveData.CollectionAlbum.AddStamp(AlbumIds.HanoiLotusStamp);`
  - Publish `StampAwardedEvent(AlbumIds.HanoiLotusStamp, HanoiLotusStamp.Name, RegionIds.Hanoi);`

### Stage 4: Feature 17 — Long Biên Bridge Gate Portal Transition
- **World Position**: `Vector2(1480f, 480f)` on Hanoi Exploration map (bounds: 1600x900).
- **Interactable Entity**: `LongBienGateInteractable : IInteractable, IYSortable`:
  - `Position`: `(1480f, 480f)`
  - `InteractionRadius`: `64f`
  - **Locked State** (`!Gate_LongBien_Unlocked`):
    - Visual: Wooden sawhorse barriers + caution tape + padlock icon.
    - Prompt text: `"[E] Cổng Cầu Long Biên (Bị chặn bởi sạp hàng)"`
    - Dialogue text: `"Lối lên Cầu Long Biên hiện đang bị các sạp hàng và xe cộ chặn kín. Hãy giúp các tiểu thương sắp xếp lại đường đi!"`
  - **Unlocked State** (`Gate_LongBien_Unlocked == true`):
    - Visual: Barrier removed; archway illuminated with cyan/gold portal aura (`#64DCFF` to `#FFE664`); animated expanding ripple rings (`MathF.Sin(_totalTime * 3.5f)`) and rising spark particles.
    - Prompt text: `"[E] Đi qua Cầu Long Biên (Đến Bản Đồ Thế Giới)"`
    - Interaction: Triggers `RequestWorldMap = true` on scene, completing level exploration.

### Stage 5: Feature 20 — Gray Shadow Letter Cutscene & Durable Auto-Save
- **Narrative Content (Verbatim Vietnamese Text)**:
  ```
  Hỡi Bé Mây và cây Bút Chì nhỏ bé,

  Các ngươi đã mang lại tiếng cười và sắc màu ấm áp cho những mái ngói rêu phong của Phố Cổ Hà Nội...
  Nhưng đừng vội mừng! Màu sắc chỉ là một mảnh ký ức mong manh trôi theo dòng thời gian.

  Nếu muốn tìm lại trọn vẹn Cuốn Sổ Ký Ức, hãy vượt Cầu Long Biên, xuôi về phương Nam tới Cố Đô Huế.
  Nơi dòng Hương Giang lững lờ và những lăng tẩm hoàng cung trầm mặc đang chìm trong màn sương xám lạnh...

  Ta đợi các ngươi ở đó!
  — Bóng Xám
  ```
- **Modal Component (`Source/UI/GrayShadowLetterModal.cs`)**:
  - **Dimensions**: `840 x 520 px`, centered on screen `(X: 220, Y: 100)`.
  - **Visual Styling**:
    - Fullscreen backdrop dim: `ColorAlpha(Black, 0.70f)`.
    - Modal body: Rounded rect (`radius 0.08f`), aged parchment fill `Color(252, 248, 235, 255)`, double border (inner `TerracottaRoof`, outer `CartoonOutline`).
    - Top header: Wax seal emblem with stylized shadow crest and title `"Bức Thư Bí Ẩn Của Bóng Xám"`.
  - **Typewriter Mechanics**:
    - Speed: 35 characters per second.
    - Sound: Low-volume pencil sketch/blip SFX on punctuation/intervals.
    - Skip/Fast-Forward: Pressing `[Space]`, `[Enter]`, or clicking mouse immediately fills all text.
  - **Continue Button**:
    - Appears when text finishes typing.
    - Dimensions: `260 x 44 px` at bottom-center of modal.
    - Pulsing border: `MathF.Sin(_timer * 4f)`.
    - Label: `"[Enter / Space] Tiếp tục hành trình ➔"`.
    - Dismissal Action:
      1. `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;`
      2. `_eventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));`
      3. `_eventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));`
      4. Closes modal, enables Long Biên bridge portal transition to World Map.
- **Durable Auto-Save Execution**:
  - Add optional `SaveLoadService? saveLoadService = null` to `LevelProgressionService` constructor:
    ```csharp
    public LevelProgressionService(SaveData saveData, IEventBus? eventBus = null, SaveLoadService? saveLoadService = null)
    ```
  - In `CompleteLevel1()`:
    ```csharp
    // 7. Atomic Persistent Auto-Save
    _saveLoadService?.Save(_saveData);
    ```
  - Writing snapshot to disk immediately upon level completion guarantees that even if the game process terminates, all progress, album items, unlocked regions, and quest flags are durably preserved.

---

## 3. Caveats

1. **Milestone 4 Boundary**:
   The interactive dialogue graph parser and full World Map navigation system belong to Milestone 4. Explorer 3 defines the level climax transition point (gate unlock, cutscene modal, and signaling), but actual travel into Huế exploration remains gated behind Milestone 4/5.
2. **Read-Only Investigation Compliance**:
   No source code or test files were modified during this investigation. All proposed classes and modifications are documented in this report for implementers to execute cleanly.
3. **No Breaking Changes**:
   Adding convenience alias properties `JournalPages` and `TravelStamps` to `CollectionAlbum` and an optional parameter `saveLoadService = null` to `LevelProgressionService` ensures 100% backwards compatibility with all 303 existing tests.

---

## 4. Conclusion

The implementation design for Features 17, 18, 19, and 20 is structured as follows:

### 4.1 Target File Modifications and Additions

| File | Nature | Purpose |
|------|--------|---------|
| `Source/Core/Models/CollectionAlbum.cs` | Edit | Add `JournalPages => CollectedJournalPages` and `TravelStamps => CollectedStamps` convenience aliases. |
| `Source/Core/Album/AlbumService.cs` | Edit | Ensure `HanoiPage1` has title `"Trang Sổ 1: Ký ức Phố Cổ Hà Nội"` and `HanoiLotusStamp` has acquisition timestamp. |
| `Source/Core/Quests/LevelProgressionService.cs` | Edit | Inject optional `SaveLoadService` and trigger atomic `_saveLoadService?.Save(_saveData)` in `CompleteLevel1()`. |
| `Source/Core/Interactions/HanoiInteractables.cs` | Edit | Add `LongBienGateInteractable` with locked/unlocked state prompts and World Map trigger. |
| `Source/UI/GrayShadowLetterModal.cs` | **New File** | Typewriter letter modal, aged parchment vector styling, skip functionality, and continue button. |
| `Source/Stages/HanoiExplorationScene.cs` | Edit | Register `LongBienGateInteractable`, draw gate portal with ripple/glow shaders, and host `GrayShadowLetterModal`. |
| `Source/Stages/StallPuzzleScene.cs` | Edit | Trigger `GrayShadowLetterModal` upon BFS solve before switching scene. |
| `Tests/LevelProgressionClimaxTests.cs` | **New File** | 10+ headless xUnit tests validating all rewards, gate transitions, cutscene logic, and persistence durability. |

### 4.2 Class Specifications

#### 1. `CollectionAlbum.cs` Convenience Aliases
```csharp
[JsonIgnore]
public List<string> JournalPages => CollectedJournalPages;

[JsonIgnore]
public List<string> TravelStamps => CollectedStamps;
```

#### 2. `LongBienGateInteractable.cs`
```csharp
namespace GameProject.Core.Interactions;

public sealed class LongBienGateInteractable : IInteractable, IYSortable
{
    public string Id => "Gate_LongBien";
    public string DisplayName => "Cổng Cầu Long Biên";
    public Vector2 Position { get; set; } = new(1480f, 480f);
    public Vector2 InteractionCenter => Position + new Vector2(0, -20);
    public float InteractionRadius => 64.0f;
    public float VisualHeight => 60.0f;

    public string EntityId => Id;
    public float FootY => Position.Y;
    public int SubLayerPriority => 0;

    public bool CanInteract(InteractionContext context) => true;

    public string GetPromptText(InteractionContext context) =>
        context.QuestFlags.Gate_LongBien_Unlocked
            ? "[E] Đi qua Cầu Long Biên (Bản Đồ Thế Giới)"
            : "[E] Cổng Cầu Long Biên (Bị chặn bởi sạp hàng)";

    public InteractionResult Interact(InteractionContext context)
    {
        if (!context.QuestFlags.Gate_LongBien_Unlocked)
        {
            return new InteractionResult(
                Success: true,
                DialogueSpeaker: "Bút Chì",
                DialogueText: "Đường ra Cầu Long Biên đang bị các sạp hàng hoa và xe xích lô chặn kín rồi! Mây hãy giúp các bác tiểu thương dọn lối đi nhé!"
            );
        }

        return new InteractionResult(
            Success: true,
            DialogueSpeaker: "Bút Chì",
            DialogueText: "Lối đi đã thông suốt! Chúng ta cùng đi qua Cầu Long Biên để ngắm nhìn toàn cảnh sông Hồng và tiến vào Cố Đô Huế nào!",
            QuestCompleted: false
        );
    }
}
```

#### 3. `GrayShadowLetterModal.cs`
```csharp
namespace GameProject.UI;

public sealed class GrayShadowLetterModal
{
    public const string LetterText =
        "Hỡi Bé Mây và cây Bút Chì nhỏ bé,\n\n" +
        "Các ngươi đã mang lại tiếng cười và sắc màu ấm áp cho những mái ngói rêu phong của Phố Cổ Hà Nội...\n" +
        "Nhưng đừng vội mừng! Màu sắc chỉ là một mảnh ký ức mong manh trôi theo dòng thời gian.\n\n" +
        "Nếu muốn tìm lại trọn vẹn Cuốn Sổ Ký Ức, hãy vượt Cầu Long Biên, xuôi về phương Nam tới Cố Đô Huế.\n" +
        "Nơi dòng Hương Giang lững lờ và những lăng tẩm hoàng cung trầm mặc đang chìm trong màn sương xám lạnh...\n\n" +
        "Ta đợi các ngươi ở đó!\n" +
        "— Bóng Xám";

    public bool IsActive { get; private set; }
    public float TypewriterProgress { get; private set; }
    public int VisibleCharCount => Math.Clamp((int)(TypewriterProgress * 35.0f), 0, LetterText.Length);
    public bool IsTypingComplete => VisibleCharCount >= LetterText.Length;
    public bool RequestClose { get; private set; }

    public void Show()
    {
        IsActive = true;
        TypewriterProgress = 0f;
        RequestClose = false;
    }

    public void Update(float dt, bool isConfirmPressed, bool isSkipPressed)
    {
        if (!IsActive) return;

        if (!IsTypingComplete)
        {
            TypewriterProgress += dt;
            if (isSkipPressed || isConfirmPressed)
            {
                TypewriterProgress = LetterText.Length / 35.0f; // Instant fast-forward
            }
        }
        else if (isConfirmPressed)
        {
            Dismiss();
        }
    }

    public void Dismiss()
    {
        IsActive = false;
        RequestClose = true;
    }
}
```

---

## 5. Verification Method

### 5.1 Automated Test Plan for Headless xUnit Verification
All tests are implemented headlessly in `Tests/LevelProgressionClimaxTests.cs` using `MockSaveStorage` and `EventBus`:

```csharp
using System;
using System.Collections.Generic;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Quests;
using GameProject.Tests.Mocks;
using GameProject.UI;
using Xunit;

namespace GameProject.Tests;

public class LevelProgressionClimaxTests
{
    [Fact]
    public void CompleteLevel1_AwardsJournalPage1_WithCorrectMetadata()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();

        // 1. Verify ID in both property accessors
        Assert.Contains(AlbumIds.HanoiJournalPage, data.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumIds.HanoiJournalPage, data.CollectionAlbum.JournalPages);

        // 2. Verify AlbumService rich metadata
        var albumService = new AlbumService();
        var pages = albumService.GetUnlockedJournalPages(data.CollectionAlbum);
        Assert.Single(pages);
        Assert.Equal(AlbumIds.HanoiJournalPage, pages[0].Id);
        Assert.Contains("Phố Cổ", pages[0].Title);
        Assert.Equal(RegionIds.Hanoi, pages[0].Region);
        Assert.NotEmpty(pages[0].StoryText);
        Assert.Equal("Art_Journal_Hanoi_01", pages[0].IllustrationKey);
    }

    [Fact]
    public void CompleteLevel1_AwardsLotusStamp_WithTimestamp()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();

        // 1. Verify Stamp ID in collection
        Assert.Contains(AlbumIds.HanoiLotusStamp, data.CollectionAlbum.CollectedStamps);
        Assert.Contains(AlbumIds.HanoiLotusStamp, data.CollectionAlbum.TravelStamps);

        // 2. Verify Stamp rich metadata
        var albumService = new AlbumService();
        var stamps = albumService.GetUnlockedTravelStamps(data.CollectionAlbum);
        Assert.Single(stamps);
        Assert.Equal(AlbumIds.HanoiLotusStamp, stamps[0].Id);
        Assert.Equal("Tem Hoa Sen Hà Nội", stamps[0].Name);
        Assert.NotEmpty(stamps[0].DateEarned);
        Assert.Contains("1000 NĂM VĂN HIẾN", stamps[0].PostmarkText);
    }

    [Fact]
    public void CompleteLevel1_UnlocksLongBienGate_AndHueRegion()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();

        Assert.True(data.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(data.Level1State.QuestFlags.Level1_Finished);
        Assert.True(data.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(data.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
    }

    [Fact]
    public void CompleteLevel1_TriggersDurableAtomicAutoSave()
    {
        var storage = new MockSaveStorage();
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var saveService = new SaveLoadService(SaveLoadService.DefaultSavePath, storage, bus);
        var progression = new LevelProgressionService(data, bus, saveService);

        progression.CompleteLevel1();

        // Verify savegame.json exists immediately in storage
        Assert.True(storage.FileExists(SaveLoadService.DefaultSavePath));

        // Reload data into fresh instance to verify durability
        var reloaded = saveService.Load();
        Assert.True(reloaded.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(reloaded.Level1State.QuestFlags.Level1_Finished);
        Assert.Contains(AlbumIds.HanoiJournalPage, reloaded.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumIds.HanoiLotusStamp, reloaded.CollectionAlbum.CollectedStamps);
        Assert.True(reloaded.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(reloaded.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
    }

    [Fact]
    public void GrayShadowLetterModal_TypewriterAndSkipBehavior()
    {
        var modal = new GrayShadowLetterModal();
        modal.Show();

        Assert.True(modal.IsActive);
        Assert.Equal(0, modal.VisibleCharCount);
        Assert.False(modal.IsTypingComplete);

        // Advance 1 second -> ~35 chars
        modal.Update(1.0f, isConfirmPressed: false, isSkipPressed: false);
        Assert.InRange(modal.VisibleCharCount, 30, 40);
        Assert.False(modal.IsTypingComplete);

        // Press skip -> Immediately reveal full letter
        modal.Update(0.016f, isConfirmPressed: false, isSkipPressed: true);
        Assert.True(modal.IsTypingComplete);
        Assert.Equal(GrayShadowLetterModal.LetterText.Length, modal.VisibleCharCount);

        // Press confirm -> Closes modal
        modal.Update(0.016f, isConfirmPressed: true, isSkipPressed: false);
        Assert.False(modal.IsActive);
        Assert.True(modal.RequestClose);
    }

    [Fact]
    public void LongBienGateInteractable_StatePromptsAndBehavior()
    {
        var gate = new LongBienGateInteractable();
        var data = SaveData.CreateDefault();
        var context = new InteractionContext(
            PlayerPosition: gate.Position,
            Inventory: new Inventory(),
            QuestManager: new QuestManager(),
            QuestFlags: data.Level1State.QuestFlags,
            EventBus: new EventBus(),
            AudioService: new NullAudioService()
        );

        // 1. Locked prompt
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = false;
        Assert.Contains("Bị chặn", gate.GetPromptText(context));

        // 2. Unlocked prompt
        data.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        Assert.Contains("Đi qua Cầu Long Biên", gate.GetPromptText(context));
    }

    [Fact]
    public void CompleteLevel1_IsIdempotent_DoesNotDuplicateAwards()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progression = new LevelProgressionService(data, bus);

        progression.CompleteLevel1();
        progression.CompleteLevel1();
        progression.CompleteLevel1();

        Assert.Single(data.CollectionAlbum.CollectedJournalPages);
        Assert.Single(data.CollectionAlbum.CollectedStamps);
        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
    }
}
```

### 5.2 Verification Commands
Execute the automated test suite to confirm zero regressions and sub-second execution:
```bash
dotnet test Tests/GameProject.Tests.csproj --no-restore
```

### 5.3 Invalidation Conditions
This investigation design would be invalidated if:
1. `CollectionAlbum` serialization requires a separate polymorphic metadata serializer rather than ID list mapping. (Refuted: `AlbumService` handles metadata lookup by ID).
2. `LevelProgressionService` must run exclusively in presentation layer. (Refuted: Clean Architecture keeps `LevelProgressionService` in `Core.Quests` headlessly testable with Raylib-free zero display server).
3. The save file format cannot serialize Vietnamese diacritics. (Refuted: `SaveGameJsonContext` uses `UnsafeRelaxedJsonEscaping`, verified in `Utf8EncodingTests.cs`).
