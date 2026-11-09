# Handoff Report: Milestone 4 Explorer 3 (Collection Album & 2D World Map)

**Target**: Milestone 4 Implementation Team & Orchestrator  
**Author**: Explorer 3 (Collection Album & 2D World Map)  
**State Folder**: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_3/`  
**Timestamp**: 2026-11-09T01:52:00Z  

---

## 1. Observation

### 1.1 Existing Files and Architecture
Direct examination of the codebase reveals the following existing files, interfaces, and baseline tests:
- `Source/Stages/WorldMapScene.cs` (305 lines) — Visual Raylib scene with regional pins for Hanoi, Hue, HoiAn, SaiGon.
- `Source/Stages/AlbumViewerScene.cs` (403 lines) — Visual Raylib scene with 3 tabs: Trang Sổ, Tem Du Lịch, Thẻ Văn Hóa.
- `Source/Core/WorldMap/WorldMapService.cs` (114 lines) — Domain service exposing `GetRegionNodes(WorldProgression)` and `TrySelectRegion(string regionId, WorldProgression, out string failureReason)`.
- `Source/Core/Album/AlbumService.cs` (208 lines) — Domain service exposing `GetUnlockedJournalPages`, `GetUnlockedTravelStamps`, `GetUnlockedFactCards`, and award/unlock methods.
- `Source/Core/Models/CollectionAlbum.cs` (119 lines) — Data model with `CollectedJournalPages`, `CollectedStamps`, and `UnlockedFactCards`.
- `Source/Core/Models/WorldProgression.cs` (162 lines) — Data model tracking `UnlockedRegions`, `CompletedRegions`, and `RegionColorRestoration` dictionary.
- `Source/Stages/SceneManager.cs` (51 lines) — Active scene coordinator interface `IScene` (Enter, Update, Draw, Exit, Dispose).
- `Source/Graphics/FontManager.cs` (175 lines) — Vietnamese TTF font loader and text outline renderer.
- `Tests/AlbumTests.cs` (94 lines) & `Tests/WorldMapTests.cs` (105 lines) — Baseline unit tests passing 100% (410 tests passing across test suite).

### 1.2 Verbatim Observations & Discrepancies
1. **Font Codepoint Limitation & Broken Emojis**:
   - In `Source/Graphics/FontManager.cs:40-57`, `GetVietnameseCodepoints()` explicitly registers only 762 codepoints:
     - 0x0020 to 0x024F (Basic Latin & Latin Extended-A/B)
     - 0x1EA0 to 0x1EF9 (Vietnamese compound diacritics)
     - 0x2000 to 0x206E (Punctuation, dashes, quotes)
     - 0x20AB (₫)
   - In `Source/Stages/AlbumViewerScene.cs:28`, `TabNames = ["📖 Trang Sổ", "🏷️ Tem Du Lịch", "🃏 Thẻ Văn Hóa"]`.
   - In `Source/Stages/AlbumViewerScene.cs:336`, `string[] emojis = ["🏮", "🌸", "🏛️", "🌊", "🎵", "⛄"]`.
   - In `Source/Stages/WorldMapScene.cs:220`, `string lockStr = "🔒"`.
   - **Result**: These Unicode emojis are outside the 762 loaded codepoints, causing Raylib to render missing-glyph boxes ('?') or blank spaces. Pure vector drawings / badges must replace Unicode emojis.

2. **FactCard Identifier Mismatch**:
   - In `Source/Core/Models/CollectionAlbum.cs:14-17`:
     ```csharp
     public const string FactCardPho = "FactCard_Hanoi_Pho";
     public const string FactCardLongBien = "FactCard_Hanoi_LongBien";
     public const string FactCardSenTayHo = "FactCard_Hanoi_SenTayHo";
     public const string FactCardPhoCo = "FactCard_Hanoi_PhoCo";
     ```
   - In `Source/Core/Album/AlbumService.cs:102-140`, dictionary keys are:
     `"FactCard_PhoHaNoi"`, `"FactCard_CauLongBien"`, `"FactCard_HoaSenTayHo"`, `"FactCard_36PhoPhuong"`.
   - In `Tests/AdversarialIteration2GateTests.cs:376`, test calls `AddFactCard(AlbumIds.FactCardPho)`.
   - In `Tests/AlbumTests.cs:81`, test calls `UnlockFactCard(album, "FactCard_PhoHaNoi")`.
   - **Result**: `AlbumService` must alias both key formats so both tests and quest unlock callers resolve to the same underlying FactCard data.

3. **Cuốn Sổ Ký Ức (Journal Pages) Missing Page 2 Layout**:
   - In `Source/Core/Album/AlbumService.cs:83-89`, `AllJournalPages` contains only `AlbumIds.HanoiJournalPage` ("JournalPage_Hanoi_01").
   - `AlbumViewerScene.cs:81` sets `GetCurrentTabCount() = _albumService.GetUnlockedJournalPages().Count`. When Page 1 is unlocked, count is 1. The player cannot see or navigate to Page 2 (Huế).
   - Dispatch requirement states: *"Displays Page 1 (Hanoi memory restore story), with layout for Page 2 (Huế)."*

4. **Tem Du Lịch (Travel Stamps) Missing Collection Slots**:
   - In `Source/Stages/AlbumViewerScene.cs:243-249`, `DrawStampsTab` only draws unlocked stamps. If no stamps are unlocked, an empty state is shown; if 1 stamp is unlocked, only 1 stamp is shown without placeholder slots.
   - Dispatch requirement states: *"Displays 'Tem Hoa Sen Hà Nội' and stamp collection slot."*

5. **Navigation Input Gaps in AlbumViewerScene**:
   - `Source/Stages/AlbumViewerScene.cs:48-65` checks `1`, `2`, `3` for tabs, `A`/`D` or `Left`/`Right` for pages, and `Tab`/`Esc` to close.
   - Key inputs `[Q]` and `[E]` (standard tab cycle) are NOT implemented.

6. **Navigation & Preview Card Gaps in WorldMapScene**:
   - `Source/Stages/WorldMapScene.cs:63-85` only checks `GetPinAtMouse(mouse)` on mouse click. Arrow keys (`[Up]`, `[Down]`, `[Left]`, `[Right]`) are not implemented.
   - There is no dedicated regional preview card (only a small text line `_feedbackMsg` at the bottom).
   - Dispatch requirement states: *"Node selection logic: arrow keys / mouse selection, shows region preview, restored color %, and travel prompt ([Enter] / Click to enter)."*

7. **Scene Coordinator Flow in Program.cs**:
   - In `Program.cs:22-40`, `HanoiExplorationScene` is created directly; the game loop only calls `sceneManager.Update(dt)` and `sceneManager.Draw()`.
   - It never checks `explorationScene.RequestWorldMap`, `worldMapScene.RequestTravelTo`, `worldMapScene.RequestAlbum`, or `albumViewerScene.RequestClose`.
   - Therefore, transitions between Exploration <-> World Map <-> Album are not hooked up in the main application loop.

---

## 2. Logic Chain

1. **Premise 1 (R1 & Dispatch)**: World Map must display Vietnam regions (Hanoi unlocked with 0-100% color, Hue unlocked upon Level 1 completion, future regions locked in gray silhouette), support arrow keys and mouse selection, show a rich region preview card, prevent locked region selection, and transition smoothly.
   - *Supporting Evidence*: § 1.1, 1.2 item 6.
   - *Inference*: `WorldMapScene` must manage an active selection index, allow cycling via `Up/Down` arrow keys, render a dedicated preview card on the right side (showing region name, status, color %, description, travel prompt), and set `RequestTravelTo` upon `[Enter]` or clicking the travel button.

2. **Premise 2 (R4 & Dispatch)**: Collection Album must feature 3 distinct tabs (Cuốn Sổ Ký Ức, Tem Du Lịch, Thẻ Văn Hóa) with scrapbook aesthetics, unlocked vs locked badges, Page 2 (Huế) layout, stamp collection slots, and Q/E + Tab navigation.
   - *Supporting Evidence*: § 1.1, 1.2 items 3, 4, 5.
   - *Inference*: `AlbumService` must register Page 2 (Huế) and registered stamp slots across Vietnam. `AlbumViewerScene` must present fixed slot layouts (Page 1 & 2 in Journal; 4 stamp slots in Stamps; 4 cultural cards in Facts) displaying unlocked details vs locked silhouette badges and mystery backs. `[Q]` and `[E]` must cycle tabs.

3. **Premise 3 (Visual Reliability & Clean Architecture)**: FontManager cannot render Unicode emojis. All badges and icons must be vector-rendered. Scenes must accept `IInputService?` so all state changes and selection rules can be verified headlessly in `xUnit`.
   - *Supporting Evidence*: § 1.2 item 1; `PROJECT.md` headless testability requirement.
   - *Inference*: Replace emoji strings with vector badges (`VectorRenderer.DrawRoundedRect`, `DrawCartoonCircle`, `DrawPadlock`, `DrawStar`). Inject `IInputService` into `WorldMapScene` and `AlbumViewerScene`.

4. **Premise 4 (ID Compatibility)**: FactCard IDs have two conventions (`AlbumIds.FactCardPho` = `"FactCard_Hanoi_Pho"` vs `"FactCard_PhoHaNoi"`).
   - *Supporting Evidence*: § 1.2 item 2.
   - *Inference*: `AlbumService.AllFactCards` must index both keys to the identical `FactCard` instances, ensuring existing tests and new callers never fail.

5. **Premise 5 (Smooth Transitions)**: Exploration -> World Map -> Album -> World Map -> Exploration must be coordinated.
   - *Supporting Evidence*: § 1.2 item 7.
   - *Inference*: `Program.cs` needs a scene coordinator loop, and `HanoiExplorationScene` should support `[Tab]` to open `AlbumViewer` as a modal overlay, matching `_phoAssemblyModal` and `_stallPuzzleModal`.

---

## 3. Caveats

1. **Raylib Headless Constraints**:
   - Raylib graphics functions (`DrawRectangle`, `DrawTextEx`, etc.) crash or no-op if called without an active display server or initialized window (`Raylib.IsWindowReady() == false`).
   - All xUnit tests must verify domain logic, state models, input interpretation, navigation state, and node selection through domain services (`WorldMapService`, `AlbumService`, `GameStateManager`) or headless scene state without calling `Draw()`.
2. **Vietnam Boundary Stylization**:
   - The map of Vietnam is a stylized cartoon representation designed at 1280x720. It uses rounded vector shapes for North, Central, and South regions, along with Biển Đông and Hoằng Sa / Trường Sa symbols, rather than precise geographic GIS shapefiles.
3. **Vertical Slice Scope (Hà Nội & Huế)**:
   - For the Vertical Slice, Hà Nội is fully playable. Huế is unlocked upon completing Hà Nội Level 1 (climax). Future regions (Đà Nẵng/Hội An, Sài Gòn) remain locked silhouettes. When selecting Huế, the game displays a preview of Cố Đô Huế and sets `RequestTravelTo = "Hue"`.

---

## 4. Conclusion & Drop-In Code Specification

To achieve complete Milestone 4 compliance for the Collection Album and 2D World Map, implementers must apply the following drop-in modifications:

### 4.1 `Source/Core/Album/AlbumService.cs` (Enhanced with Page 2, Stamp Slots, and FactCard Aliases)

```csharp
// Enhancements in AlbumService.cs:
// 1. Add HuePage2 to AllJournalPages
// 2. Add Stamp catalog for Hanoi, Hue, HoiAn, SaiGon
// 3. Register dual-key aliases for all 4 FactCards:
//    - "FactCard_PhoHaNoi" & "FactCard_Hanoi_Pho"
//    - "FactCard_CauLongBien" & "FactCard_Hanoi_LongBien"
//    - "FactCard_HoaSenTayHo" & "FactCard_Hanoi_SenTayHo"
//    - "FactCard_36PhoPhuong" & "FactCard_Hanoi_PhoCo"
// 4. Add helper query methods:
//    - GetAllJournalPages() -> IReadOnlyList<JournalPage> (Page 1 & 2)
//    - GetAllRegisteredStamps() -> IReadOnlyList<TravelStamp> (4 slots)
//    - GetAllRegisteredFactCards() -> IReadOnlyList<FactCard> (4 unique cards)
//    - IsJournalPageUnlocked(album, pageId)
//    - IsStampUnlocked(album, stampId)
//    - IsFactCardUnlocked(album, cardId)
```

Exact metadata for Hue Page 2:
```csharp
public static readonly JournalPage HuePage2 = new()
{
    Id = "JournalPage_Hue_02",
    Title = "Dòng Sông Hương & Bóng Cung Thành Cổ",
    Subtitle = "Trang Sổ 2: Ký ức Cố Đô Huế",
    Region = RegionIds.Hue,
    StoryText = "Cố Đô Huế - Dòng sông Hương lững lờ trôi qua những lăng tẩm trầm mặc và Hoàng thành rêu phong. Sau khi phục hồi sắc màu cho Hà Nội, Bé Mây và Bút Chì hướng về phương Nam...",
    IllustrationKey = "Art_Journal_Hue_02"
};
```

Exact stamp catalog:
```csharp
public static readonly TravelStamp HueStamp = new()
{
    Id = "Stamp_Hue_ThienMu",
    Name = "Tem Cố Đô Huế",
    Subtitle = "Ngàn Năm Di Sản Cố Đô",
    Region = RegionIds.Hue,
    DateEarned = "",
    PostmarkText = "CỐ ĐÔ HUẾ - DI SẢN THẾ GIỚI",
    IconKey = "Icon_Stamp_Hue"
};

public static readonly TravelStamp HoiAnStamp = new()
{
    Id = "Stamp_HoiAn_DenLong",
    Name = "Tem Phố Cổ Hội An",
    Subtitle = "Thương Cảng Cổ Lung Linh",
    Region = RegionIds.HoiAn,
    DateEarned = "",
    PostmarkText = "HỘI AN - PHỐ ĐÈN LỒNG",
    IconKey = "Icon_Stamp_HoiAn"
};

public static readonly TravelStamp SaiGonStamp = new()
{
    Id = "Stamp_SaiGon_BenThanh",
    Name = "Tem Nắng Phương Nam",
    Subtitle = "Sài Gòn Hoa Lệ & Năng Động",
    Region = RegionIds.SaiGon,
    DateEarned = "",
    PostmarkText = "SÀI GÒN - BẾN THÀNH",
    IconKey = "Icon_Stamp_SaiGon"
};
```

### 4.2 `Source/Stages/AlbumViewerScene.cs` (Complete Drop-In Replacement)
Key structural design:
- **3 Tab Headers**: `"Cuốn Sổ Ký Ức"`, `"Tem Du Lịch"`, `"Thẻ Văn Hóa"`.
- **Navigation Controls**:
  - `[Q]` / `[E]`: Cycle active tab left / right.
  - `[1]`, `[2]`, `[3]`: Direct jump to tabs 0, 1, 2.
  - `[A]` / `[D]`, `[Left]` / `[Right]`: Page / item navigation.
  - `[Tab]`, `[Esc]`, or clicking `[X]` button: Closes Album (`RequestClose = true`).
  - Mouse click on tab headers or item slots.
- **Tab 1 — Cuốn Sổ Ký Ức (Journal Pages)**:
  - Supports 2 pages: Page 1 (Hà Nội), Page 2 (Huế).
  - Page 1: If unlocked -> Displays full vector illustration (Long Biên bridge, Red River, lotus blossoms), title, story text, and "HÀ NỘI" postmark. If locked -> Displays locked parchment outline.
  - Page 2: Displays "Trang 2: Cố Đô Huế". If locked -> Displays grayscale silhouette of Ngọ Môn Gate, locked badge ("CHƯA MỞ KHÓA"), and preview lore explaining how to unlock it.
- **Tab 2 — Tem Du Lịch (Travel Stamps)**:
  - 4-slot passport stamp grid (Hà Nội, Huế, Hội An, Sài Gòn).
  - Hà Nội: Unlocked upon level completion, rendered with perforated circular edges, pink lotus blossom, and postmark "HÀ NỘI - 1000 NĂM VĂN HIẾN".
  - Huế, Hội An, Sài Gòn: Rendered as locked stamp mounts with dashed perforated borders, desaturated silhouettes, and padlock icons.
  - Click/Select any stamp to view philatelic detail card with inspection lore.
- **Tab 3 — Thẻ Văn Hóa (Cultural Fact Cards)**:
  - 4 Hanoi cultural trivia cards: Phở Hà Nội, Cầu Long Biên, Hoa Sen Tây Hồ, 36 Phố Phường.
  - Unlocked cards: Vibrant card with category pill badge ("Ẩm Thực", "Lịch Sử", "Văn Hóa", "Di Sản"), gold title, divider line, rich Vietnamese text, and "ĐÃ KHÁM PHÁ" badge.
  - Locked cards: Mystery card back with subtle geometric filigree, lock badge ("CHƯA MỞ KHÓA"), and unlock clue.
- **Glyph Safety**: Zero Unicode emojis. All locks, stars, checkmarks, and badges rendered via Raylib vector primitives (`VectorRenderer.DrawRoundedRect`, `DrawCartoonCircle`, `DrawRing`).
- **Input Decoupling**: Takes optional `IInputService? inputService = null` (defaults to `RaylibInputService` in GUI mode, or `MockInputService` in tests). Exposes `ActiveTab`, `PageIndex`, `RequestClose`, `NextTab()`, `PrevTab()`, `NextPage()`, `PrevPage()`.

### 4.3 `Source/Stages/WorldMapScene.cs` (Complete Drop-In Replacement)
Key structural design:
- **Regional Pins**:
  - Hà Nội: (590f, 155f) — Unlocked by default, 0% to 100% color restoration.
  - Huế: (640f, 380f) — Unlocked after Hà Nội Level 1 completed.
  - Hội An: (680f, 440f) — Locked silhouette.
  - Sài Gòn: (610f, 570f) — Locked silhouette.
- **Arrow Key & Mouse Navigation**:
  - `[Up]` / `[Down]`: Cycles through pins in geographic order (Hà Nội <-> Huế <-> Hội An <-> Sài Gòn).
  - Mouse hover & click: Directly selects hovered pin.
  - Double-click or clicking "Du Hành" button: Confirms travel.
  - `[Enter]`: Confirms travel to selected region.
  - `[Tab]` or clicking "[Tab] Mở Album": Sets `RequestAlbum = true`.
- **Region Preview Card**:
  - Positioned on right side of screen (X: 860f, Y: 140f, W: 380f, H: 480f).
  - Shows:
    - Region Name (e.g., "Thủ Đô Hà Nội", "Cố Đô Huế").
    - Status badge: "HOÀN THÀNH" (Gold star), "ĐANG KHÁM PHÁ" (Green circle), "BỊ BÓNG XÁM BAO PHỦ" (Gray lock).
    - Lore description.
    - Restored color progress bar (0% - 100%) with percentage label.
    - Travel button: Green `[Enter] Vào Màn Chơi` for unlocked playable regions; gray disabled button with reason text for locked regions.
- **Travel Validation**:
  - If locked: Blocks selection/travel, displays warning: *"Vùng đất này còn bị bao phủ trong bóng xám! Hãy hoàn thành các nhiệm vụ trước để mở khóa."*
  - If playable (Hà Nội): Sets `RequestTravelTo = "Hanoi"`.
  - If Huế (unlocked): Sets `RequestTravelTo = "Hue"` and provides notification.
- **Stylized Vietnam S-Curve**:
  - Vector rendered with regional color tint matching restoration percentage.
  - Subtle gray silhouette for locked southern regions.
  - Maritime label for Biển Đông and animated wave circles.
  - Hoang Sa & Truong Sa archipelago markers.
- **Input Decoupling**: Takes optional `IInputService? inputService = null`. Exposes `SelectedRegionId`, `HoveredRegionId`, `RequestTravelTo`, `RequestAlbum`, `SelectRegion(string id)`, `NavigateNext()`, `NavigatePrev()`, `ConfirmTravel()`.

### 4.4 Scene Transition Coordination (`Program.cs` & `HanoiExplorationScene.cs`)
In `HanoiExplorationScene.cs`:
- Support opening Album via `[Tab]` or `[J]`:
  ```csharp
  if (InputService.IsAlbumJustPressed && StateManager.CanOpenAlbum)
  {
      StateManager.PushState(GameState.Album);
  }
  ```
- Draw modal overlay when `StateManager.CurrentState == GameState.Album`:
  ```csharp
  if (StateManager.CurrentState == GameState.Album)
  {
      _albumViewerModal ??= new AlbumViewerScene(SaveData, EventBus, InputService);
      _albumViewerModal.Update(deltaTime);
      if (_albumViewerModal.RequestClose)
      {
          StateManager.PopState();
      }
  }
  ```

In `Program.cs`:
- Support smooth transitions between scenes:
  ```csharp
  if (sceneManager.CurrentScene is HanoiExplorationScene exp && exp.RequestWorldMap)
  {
      sceneManager.SwitchScene(new WorldMapScene(saveData, eventBus, inputService));
  }
  else if (sceneManager.CurrentScene is WorldMapScene wm)
  {
      if (wm.RequestTravelTo == "Hanoi")
      {
          sceneManager.SwitchScene(new HanoiExplorationScene(saveData, audioService, inputService));
      }
      else if (wm.RequestAlbum)
      {
          sceneManager.SwitchScene(new AlbumViewerScene(saveData, eventBus, inputService));
      }
  }
  else if (sceneManager.CurrentScene is AlbumViewerScene album && album.RequestClose)
  {
      sceneManager.SwitchScene(new WorldMapScene(saveData, eventBus, inputService));
  }
  ```

---

## 5. Verification Method

### 5.1 Automated Compilation & Test Execution
Execute the test command from project root:
```bash
dotnet test Tests/GameProject.Tests.csproj --logger "console;verbosity=normal"
```

### 5.2 Specific Test Cases to Add / Verify
Implement the following comprehensive tests in `Tests/WorldMapAndAlbumEnhancementTests.cs`:
1. `Album_ThreeTabs_QueryAndDisplayCountCorrectly`: Verifies Journal (2 pages), Stamps (4 slots), and Facts (4 cards).
2. `Album_Page2_Hue_ExistsWithRichMetadata_AndLockedState`: Verifies Page 2 has `RegionIds.Hue`, title, story, and reflects locked state when not yet awarded.
3. `Album_TravelStamps_FourRegionalSlots_HanoiLotusUnlockedAfterClimax`: Verifies slot 0 is unlocked after `AwardTravelStamp(AlbumIds.HanoiLotusStamp)`, while slots 1, 2, 3 remain locked.
4. `Album_FactCards_DualKeyAliasCompatibility`: Verifies unlocking via `AlbumIds.FactCardPho` ("FactCard_Hanoi_Pho") or `"FactCard_PhoHaNoi"` both resolve to the same card.
5. `Album_TabNavigation_QECyclingAndNumericJumps`: Verifies `NextTab()` and `PrevTab()` cycle 0 -> 1 -> 2 -> 0, and `SelectTab(int)` jumps directly.
6. `WorldMap_ArrowKeyNavigation_CyclesNorthToSouth`: Verifies `NavigateNext()` cycles Hanoi -> Hue -> HoiAn -> SaiGon -> Hanoi, and `NavigatePrev()` in reverse.
7. `WorldMap_RegionPreviewDataBinding`: Verifies `SelectedRegion` exposes accurate `DisplayName`, `Description`, `Status`, and `ColorRestorationPercent`.
8. `WorldMap_TravelValidation_PreventsLockedRegionTravel`: Verifies `ConfirmTravel()` on locked Hue/HoiAn fails with descriptive error and does not set `RequestTravelTo`.
9. `WorldMap_ClimaxUnlock_EnablesHueSelection`: Verifies completing Hanoi Level 1 unlocks Hue, and `TrySelectRegion(RegionIds.Hue)` succeeds.
10. `SceneTransitions_RequestFlagsCoordination`: Verifies `RequestWorldMap`, `RequestTravelTo`, `RequestAlbum`, and `RequestClose` flags signal transitions accurately.

### 5.3 Invalidation Conditions
- Any regression in the existing 410 unit tests.
- Non-zero failure count in `dotnet test`.
- Hardcoded emoji characters causing font glyph missing warnings in TTF rendering logs.
- Memory leak on repeated `SwitchScene` (verified via `IDisposable` pattern).
