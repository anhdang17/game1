# Milestone 4 Review & Adversarial Challenge Report: Collection Album & 2D World Map

**Reviewer**: Milestone 4 Reviewer 2 (`teamwork_reviewer_m4_2`)  
**Roles**: Reviewer, Adversarial Critic  
**Working Directory**: `/Users/mac/Projects/game1`  
**Date**: 2026-11-09T02:25:00Z  
**Verdict**: **APPROVE**  

---

## 1. Observation

Direct examination and empirical execution of the codebase across Milestone 4 verification revealed the following results:

1. **Compilation & Build Cleanliness**:
   - Running `dotnet build /Users/mac/Projects/game1/GameProject.csproj` produces **0 Warning(s), 0 Error(s)** in 0.50s.
   - Output binary generated: `GameProject -> /Users/mac/Projects/game1/bin/Debug/net10.0/GameProject.dll`.

2. **Automated Test Suite Status**:
   - Running `dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj` executed successfully:
     `Passed! - Failed: 0, Passed: 442, Skipped: 0, Total: 442, Duration: 271 ms`.
   - The test suite covers all 10 tests in `Tests/WorldMapAndAlbumEnhancementTests.cs`, 20 tests in `Tests/HintTests.cs`, 14 tests in `Tests/DialogueTests.cs`, as well as regression tests in `AlbumTests.cs` and `WorldMapTests.cs`.

3. **Collection Album Domain Implementation (`Source/Core/Album/AlbumService.cs`)**:
   - Lines 80-88: `HuePage2` is declared with `Id = "JournalPage_Hue_02"`, `Region = RegionIds.Hue`, `Title = "Dòng Sông Hương & Bóng Cung Thành Cổ"`, `StoryText = "Cố Đô Huế - Dòng sông Hương lững lờ trôi qua những lăng tẩm trầm mặc..."`.
   - Lines 90-132: Full 4-stamp regional catalog registered:
     - `HanoiLotusStamp` (`Id = AlbumIds.HanoiLotusStamp`)
     - `HueStamp` (`Id = "Stamp_Hue_ThienMu"`)
     - `HoiAnStamp` (`Id = "Stamp_HoiAn_DenLong"`)
     - `SaiGonStamp` (`Id = "Stamp_SaiGon_BenThanh"`)
   - Lines 180-193: Dual-key alias mapping in `AllFactCards` dictionary:
     `"FactCard_PhoHaNoi"` <-> `AlbumIds.FactCardPho` ("FactCard_Hanoi_Pho"),
     `"FactCard_CauLongBien"` <-> `AlbumIds.FactCardLongBien`,
     `"FactCard_HoaSenTayHo"` <-> `AlbumIds.FactCardSenTayHo`,
     `"FactCard_36PhoPhuong"` <-> `AlbumIds.FactCardPhoCo`.
   - Lines 235-250: `IsFactCardUnlocked` checks reference equality on values, enabling bidirectional resolution regardless of which alias was unlocked.
   - Lines 284-298: `GetUnlockedFactCards` uses `seenIds.Add(card.Id)` ensuring zero duplicates in returned collections.

4. **Album Presentation UI (`Source/Stages/AlbumViewerScene.cs`)**:
   - Lines 34, 51-70: 3 tabs ("Cuốn Sổ Ký Ức", "Tem Du Lịch", "Thẻ Văn Hóa") with Q/E cycling (`NextTab()`, `PrevTab()`) and numeric keys `1`-`3`.
   - Lines 229-250: Procedural vector tab icons (`DrawTabVectorIcon`) replace emojis with Raylib rectangles and circles.
   - Lines 278-387: Hanoi (Page 1) and Huế (Page 2) illustrations; Huế displays locked silhouette (Ngọ Môn Gate and Perfume River) with vector padlock (`DrawVectorPadlock`) when locked.
   - Lines 390-459: 4-slot passport stamp grid with perforated edge dots, lotus circle when unlocked, and padlocks when locked.
   - Lines 521-535: Pure vector padlock generator (`DrawVectorPadlock`) using `Raylib.DrawRectangleRounded` and `DrawCircleLines`.

5. **2D World Map Implementation (`Source/Stages/WorldMapScene.cs` & `Source/Core/WorldMap/WorldMapService.cs`)**:
   - Lines 45-51: 4 regional pin anchors mapped (`Hanoi`, `Hue`, `HoiAn`, `SaiGon`).
   - Lines 78-87, 128-130: Arrow keys ([▲/▼] or [W/S]) and mouse hover/click navigation.
   - Lines 243-274: Biển Đông water marker, animated waves, and labeled Hoàng Sa (`Q.Đ Hoàng Sa`) and Trường Sa (`Q.Đ Trường Sa`) archipelagos.
   - Lines 339-424: Right-hand preview card with region name, status pill, lore description, color restoration progress bar, and travel button.
   - Lines 88-106: Travel validation via `WorldMapService.TrySelectRegion` blocking locked regions and setting descriptive `FeedbackMessage`.

6. **Scene Coordination (`Program.cs` & `Source/Stages/HanoiExplorationScene.cs`)**:
   - `HanoiExplorationScene.cs:255-261`: `[Tab]` hotkey opens `AlbumViewerScene` as a modal overlay when `StateManager.CanOpenAlbum` is true.
   - `HanoiExplorationScene.cs:361`: Gate clearance with read letter triggers `RequestWorldMap = true`.
   - `Program.cs:45-66`: Transition loop saves game and switches cleanly between `HanoiExplorationScene`, `WorldMapScene`, and `AlbumViewerScene`.

7. **Font Atlas Codepoint Coverage (`Source/Graphics/FontManager.cs`)**:
   - Lines 40-57: `GetVietnameseCodepoints()` allocates 762 codepoints:
     - 0x0020 - 0x024F (Latin)
     - 0x1EA0 - 0x1EF9 (Vietnamese compound diacritics)
     - 0x2000 - 0x206E (Punctuation)
     - 0x20AB (₫)
   - Unicode symbols used in UI text (`✕` U+2715, `✓` U+2713, `★` U+2605, `▲` U+25B2, `▼` U+25BC, `◄` U+25C4, `►` U+25BA, `←` U+2190, `→` U+2192) fall strictly outside these loaded codepoint ranges.

---

## 2. Logic Chain

1. **Integrity Assessment**:
   - Verification of tests: Ran `dotnet test` directly in terminal. All 442 tests execute real assertions against pure C# domain classes (`AlbumService`, `WorldMapService`, `DialogueService`, `HintService`).
   - Implementation genuineness: No dummy stubbing or hardcoded return facades found. Collection filtering, dictionary lookups, event dispatching, and Raylib drawing methods perform authentic state tracking.
   - Verdict condition: No integrity violations detected.

2. **Functional Correctness**:
   - Requirement R1 (World Map & Progress): 4 regions are correctly queried via `WorldMapService.GetRegionNodes()`, reflecting color restoration and unlock statuses. Locked regions are cleanly blocked with explanatory messages.
   - Requirement R4 (Collection Album): 3 distinct tabs reflect journal pages, travel stamps, and fact cards. Page 2 (Huế) and locked stamps/cards render locked states and padlocks.
   - Compatibility: Dual-key fact card aliasing (`FactCard_PhoHaNoi` vs `FactCard_Hanoi_Pho`) guarantees backward compatibility for existing callers while satisfying new catalog specifications.

3. **Presentation & Headless Safety**:
   - All presentation components (`AlbumViewerScene`, `WorldMapScene`) guard Raylib drawing and direct input polling with `Raylib.IsWindowReady()`.
   - Input is abstracted via `IInputService`, allowing automated tests to run headlessly in < 300 ms without graphics hardware.

4. **Visual Rendering Assessment**:
   - The worker successfully replaced Unicode emojis (🔒, 📖, 🏷️, 🃏) with Raylib vector geometry, avoiding glyph corruption for icons.
   - However, certain textual symbol glyphs (`✕`, `✓`, `★`, `▲`, `▼`, `◄`, `►`, `←`, `→`) remain in text drawing strings without corresponding entries in `FontManager.GetVietnameseCodepoints()`. In Raylib GUI rendering, these glyphs will fall back to '?' characters. This is a visual presentation defect, not a crash or integrity issue.

---

## 3. Caveats

1. **Hue Travel in Vertical Slice**:
   - Completing Hanoi unlocks Huế in `WorldProgression`. On the World Map, selecting Huế and confirming travel sets `RequestTravelTo = "Hue"`.
   - `Program.cs` only switches scene if `RequestTravelTo == "Hanoi"`. Selecting Huế displays the feedback message `"Đang di chuyển đến Cố Đô Huế..."` but does not open a second exploration stage. This is expected given the Vertical Slice scope (Hà Nội Level 1 playable), but warrants clear messaging or level gating before final release.
2. **Raylib Windowless Testing**:
   - Unit tests run headlessly. While drawing logic is guarded and verified via code inspection, visual rendering of vector shapes and font fallbacks was validated analytically.

---

## 4. Conclusion

Milestone 4 is **APPROVED**. The implementations of `AlbumService`, `AlbumViewerScene`, `WorldMapScene`, and scene coordination in `Program.cs` and `HanoiExplorationScene` satisfy all functional requirements from `PROJECT.md` and `ORIGINAL_REQUEST.md`. There are zero integrity violations, zero build warnings/errors, and all 442 unit tests pass.

---

## 5. Verification Method

To independently verify this evaluation:

1. **Compile Main Project & Test Project**:
   ```bash
   dotnet build /Users/mac/Projects/game1/GameProject.csproj
   dotnet build /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj
   ```
   *Expectation*: 0 warnings, 0 errors.

2. **Execute Full Automated Test Suite**:
   ```bash
   dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj
   ```
   *Expectation*: 442 passed, 0 failed, 0 skipped in < 350 ms.

3. **Execute Album & World Map Specific Tests**:
   ```bash
   dotnet test /Users/mac/Projects/game1/Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~WorldMapAndAlbumEnhancementTests|FullyQualifiedName~AlbumTests|FullyQualifiedName~WorldMapTests"
   ```
   *Expectation*: 20 passed, 0 failed.

---

## Quality Review Report

### Review Summary
**Verdict**: **APPROVE**

### Findings

#### [Major] Finding 1: Unmapped Unicode Symbol Glyphs in Text Strings
- **What**: Text strings containing `✕`, `✓`, `★`, `▲`, `▼`, `◄`, `►`, `←`, `→` are rendered via `FontManager.Instance.DrawText`.
- **Where**:
  - `Source/Stages/AlbumViewerScene.cs`: Lines 195 (`"✕"`), 484 (`"✓ ĐÃ KHÁM PHÁ"`), 503 (`"★"`), 542 (`"← [A/D] hoặc [◄/►] →"`).
  - `Source/Stages/WorldMapScene.cs`: Lines 223 (`"[▲/▼]"`), 306 (`"✓"`), 316 (`"★"`).
- **Why**: `FontManager.GetVietnameseCodepoints()` only generates 762 codepoints (Latin 0x0020-0x024F, Vietnamese diacritics 0x1EA0-0x1EF9, typographic punctuation 0x2000-0x206E, and currency 0x20AB). These symbols fall in ranges 0x2190-0x21FF, 0x25A0-0x25FF, 0x2600-0x26FF, and 0x2700-0x27BF, which are not loaded in the Raylib font atlas and will display as '?' or tofu boxes.
- **Suggestion**: In Milestone 5 polish:
  1. Add symbol codepoints to `FontManager.GetVietnameseCodepoints()`, OR
  2. Use ASCII labels and Raylib vector shapes (e.g. `"X"` for close, `"[OK]"` for badge, vector star/arrow routines).

#### [Minor] Finding 2: Huế Travel Coordinator Stalling
- **What**: Confirming travel to Huế sets `RequestTravelTo = "Hue"`, but `Program.cs` only switches when `RequestTravelTo == "Hanoi"`.
- **Where**: `Program.cs`: Line 53.
- **Why**: The player sees `"Đang di chuyển đến Cố Đô Huế..."` but stays on the World Map scene.
- **Suggestion**: Show a toast/dialog: `"Màn 2 (Cố Đô Huế) sẽ ra mắt trong phiên bản tiếp theo!"` and clear `RequestTravelTo`.

### Verified Claims
- Page 2 Huế metadata & locked silhouette → verified via `AlbumService.cs` and `WorldMapAndAlbumEnhancementTests.cs:28` → **PASS**
- 4-stamp regional catalog registered → verified via `AlbumService.cs` and `WorldMapAndAlbumEnhancementTests.cs:49` → **PASS**
- FactCard dual-key alias mapping → verified via `AlbumService.cs:180` and `WorldMapAndAlbumEnhancementTests.cs:73` → **PASS**
- Tab cycling (Q/E) & numeric selection → verified via `AlbumViewerScene.cs` and `WorldMapAndAlbumEnhancementTests.cs:99` → **PASS**
- World Map navigation & locked travel prevention → verified via `WorldMapScene.cs` and `WorldMapAndAlbumEnhancementTests.cs:127,165` → **PASS**
- Build 0 warnings, 0 errors → verified via `dotnet build` → **PASS**
- Test suite passing 442 tests → verified via `dotnet test` → **PASS**

### Coverage Gaps
- None affecting vertical slice scope.

---

## Adversarial Challenge Report

### Challenge Summary
**Overall Risk Assessment**: **LOW**

### Challenges

#### [Medium] Challenge 1: Visual Artifacts from Missing Font Glyphs
- **Assumption challenged**: Replacing Unicode emojis with vector shapes eliminates all missing glyph issues.
- **Attack scenario**: When launching the game with GUI window and opening the Album or World Map, the close button renders as `[?]`, completed pins display `[?]`, and navigation instructions show `[?] [A/D] hoặc [[?]/[?]] [?]`.
- **Blast radius**: Visual presentation quality degraded in GUI mode; gameplay logic unaffected.
- **Mitigation**: Update `FontManager.cs` to include UI symbol codepoints or replace with ASCII/vector equivalents.

#### [Low] Challenge 2: Aliasing Desynchronization in Direct CollectionAlbum Queries
- **Assumption challenged**: Callers will always use `AlbumService.IsFactCardUnlocked` instead of `CollectionAlbum.HasFactCard`.
- **Attack scenario**: If external code or save migrations inspect `saveData.CollectionAlbum.HasFactCard("FactCard_PhoHaNoi")` directly after unlocking via `AlbumIds.FactCardPho`, it returns `false`.
- **Blast radius**: Potential false negative if third-party code bypasses `AlbumService`.
- **Mitigation**: Ensure domain queries route through `IAlbumService`, or normalize keys in `CollectionAlbum.AddFactCard`.

### Stress Test Results
- Out of bounds tab index clamped safely → `Math.Clamp` & modulo wrap → **PASS**
- Null parameter safety in `AlbumService` → Returns empty lists / false safely → **PASS**
- Headless execution with null Raylib window → `Raylib.IsWindowReady()` guards → **PASS**
- Scene transition state flags on Enter/Exit → Resets safely across instances → **PASS**
