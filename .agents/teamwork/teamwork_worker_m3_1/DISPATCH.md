# Dispatch: Milestone 3 Worker — Hanoi Quests 2 & 3 & Progression Climax

## Objective
Implement all features for Milestone 3 based on the designs from Explorers 1, 2, and 3:
1. **Feature 10, 11, 12 (Quest 2 Phở Assembly Mini-game)**:
   - Connect `Source/Stages/PhoAssemblyScene.cs` as a modal overlay in `HanoiExplorationScene.cs` (or seamless modal transition).
   - Bát Tràng porcelain bowl vector visuals, 5 ingredient slots (Noodles, Beef, Herbs, Broth, Garnish), drag-and-drop or slot placement.
   - Validation via `PhoCookingEngine`, error feedback, success sound, +33.3% color bloom wave from Bà An's stall.
   - Quest 2 completion (`Quest_Hanoi_02`), unlocking Bà An's guidance towards Quest 3.
2. **Feature 13, 14, 15, 16 (Quest 3 Street Stall Spatial Grid BFS Puzzle)**:
   - Connect `Source/Stages/StallPuzzleScene.cs` or interactive 7x5 spatial grid puzzle zone at the entrance to Long Biên Bridge.
   - Add `FindPath()` parent-backtracking method in `Source/Core/Puzzles/SpatialGridEngine.cs` to supply real-time BFS path coordinates for glowing golden ribbon rendering.
   - Fix dictionary key `"Obj_FruitStall"` (replace `"Obj_PhoStall"`), dispatch `StallPuzzleResetEvent` on `[R]` key press.
   - Real-time path clearance detection via BFS flood-fill.
   - On clearance: complete Quest 3 (`Quest_Hanoi_03`), trigger 100% full color restoration wave across Hanoi and Long Biên bridge, and unlock the bridge gate!
3. **Feature 17, 18, 19, 20 (Progression Climax, Rewards & Cutscene)**:
   - Update `Source/Core/Quests/LevelProgressionService.cs` to coordinate climax atomically and invoke `SaveLoadService.Save(_saveData)` directly.
   - In `Source/Core/Models/CollectionAlbum.cs`, add `[JsonIgnore]` convenience aliases: `JournalPages => CollectedJournalPages` and `TravelStamps => CollectedStamps`.
   - Ensure `AlbumService.cs` defines `"Trang Sổ 1: Ký ức Phố Cổ Hà Nội"` and `"Tem Hoa Sen Hà Nội"` with acquisition timestamp.
   - Implement `LongBienGateInteractable` in `Source/Core/Interactions/HanoiInteractables.cs` with locked prompt (`"[E] Cổng Cầu Long Biên (Bị chặn bởi sạp hàng)"`) and unlocked prompt (`"[E] Đi qua Cầu Long Biên (Bản Đồ Thế Giới)"`) with glowing ripple visual aura.
   - Create `Source/UI/GrayShadowLetterModal.cs`: Typewriter cutscene revealing the mysterious letter left by Bóng Xám pointing to Huế, with skip (`[Space]`/`[Enter]`) and continue button.
4. **Automated Headless Tests**:
   - Create `Tests/LevelProgressionClimaxTests.cs` (validating rewards, gate transitions, cutscene logic, and atomic auto-save durability).
   - Ensure `Tests/PhoPuzzleTests.cs` and `Tests/StallPuzzleTests.cs` are fully wired.
   - Run `dotnet build GameProject.csproj` -> 0 warnings, 0 errors.
   - Run `dotnet test Tests/GameProject.Tests.csproj` -> all tests pass (303+ tests).

## References
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (R1, R2)
- `/Users/mac/Projects/game1/PROJECT.md` (Features 10–20, Interface Contracts)
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_1/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_2/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_3/handoff.md`

## Write Ownership
- `Source/Core/Puzzles/SpatialGridEngine.cs`
- `Source/Core/Quests/LevelProgressionService.cs`
- `Source/Core/Models/CollectionAlbum.cs`
- `Source/Core/Album/AlbumService.cs`
- `Source/Core/Interactions/HanoiInteractables.cs`
- `Source/UI/GrayShadowLetterModal.cs`
- `Source/Stages/PhoAssemblyScene.cs`
- `Source/Stages/StallPuzzleScene.cs`
- `Source/Stages/HanoiExplorationScene.cs`
- `Tests/LevelProgressionClimaxTests.cs`
- `Tests/PhoPuzzleTests.cs`
- `Tests/StallPuzzleTests.cs`

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## 2026-11-09T01:02:25Z
You are the Milestone 3 Worker (Hanoi Quests 2 & 3 & Progression Climax).
Implement all Milestone 3 features:
1. `Source/Stages/PhoAssemblyScene.cs` & `Source/Stages/HanoiExplorationScene.cs`: Modal Phở Assembly UI, porcelain bowl rendering, ingredient card slots, validation via `PhoCookingEngine`, error/success feedback, +33.3% color bloom wave, Quest 2 completion.
2. `Source/Core/Puzzles/SpatialGridEngine.cs`: Add `FindPath()` parent-backtracking method.
3. `Source/Stages/StallPuzzleScene.cs`: 7x5 pushable stalls, real-time BFS glowing path ribbon, fix `"Obj_FruitStall"`, dispatch `StallPuzzleResetEvent` on `[R]`, 100% full color wave, Quest 3 completion.
4. `Source/Core/Quests/LevelProgressionService.cs`: Atomic climax coordination, auto-save snapshot via `SaveLoadService.Save(_saveData)`.
5. `Source/Core/Models/CollectionAlbum.cs`: Add convenience alias properties `JournalPages` and `TravelStamps`.
6. `Source/Core/Album/AlbumService.cs`: Ensure rich metadata for Journal Page 1 ("Trang Sổ 1: Ký ức Phố Cổ Hà Nội") and Lotus Stamp ("Tem Hoa Sen Hà Nội").
7. `Source/Core/Interactions/HanoiInteractables.cs`: Add `LongBienGateInteractable` (locked/unlocked prompts, portal visual aura, world map transition trigger) and Bà An dialogue state updates.
8. `Source/UI/GrayShadowLetterModal.cs`: Typewriter cutscene revealing the mysterious letter left by Bóng Xám pointing to Huế, with skip (`[Space]`/`[Enter]`) and continue button.
9. `Tests/LevelProgressionClimaxTests.cs`, `Tests/PhoPuzzleTests.cs`, `Tests/StallPuzzleTests.cs`: Ensure comprehensive headless test coverage.

## 2026-11-09T01:12:51Z
**Context**: Milestone 3 Worker Check-in
**Content**: Checking in on Milestone 3 progress. How is implementation progressing for Phở Assembly Scene, Stall Puzzle Scene, Long Biên Gate, Level Progression Climax, and Automated Tests?
**Action**: Please update progress.md with your latest progress and continue implementation.

