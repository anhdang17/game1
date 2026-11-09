# Task Assignment: Milestone 4 Implementation Worker

## Objective
Implement all features of Milestone 4 (Dialogue System, Bút Chì Hint Engine, Collection Album, and 2D World Map) following the drop-in specifications designed by Explorers 1, 2, and 3:

1. **Dialogue System (Explorer 1)**:
   - Create `Data/dialogues_hanoi.json` and `Source/Data/dialogues_hanoi.json` with the complete dialogue trees for Bà An, merchants, Long Biên, and points of interest.
   - Update `Source/Core/Dialogue/DialogueService.cs`: multi-graph JSON parsing, `LoadDialogueFile(string path)` with file path probing, `SelectChoice(string choiceId)`, `RequiredQuestFlag` evaluation, and publishing `DialogueStartedEvent`, `DialogueLineAdvancedEvent`, `DialogueChoiceSelectedEvent`, `DialogueEndedEvent` to `IEventBus`.
   - Create `Source/UI/DialogueBox.cs`: typewriter text (38 chars/sec), Form C diacritic normalization, audio blips (`SfxType.DialogueBlip`), procedural character avatars, choice buttons with arrow key/number selection, and headless testability.
   - Update `Tests/DialogueTests.cs` to verify multi-graph loading, events, choices, and typewriter progression.

2. **Bút Chì Hint Engine (Explorer 2)**:
   - Update `Source/Core/Hints/HintService.cs` (`IHintService` & `HintManager`): implement `GetPhoAssemblyHint`, `GetStallPuzzleHint`, `CheckIdlePrompt(float idleSeconds, int failedAttemptsCount, out string idlePrompt)` with 45s threshold, preserving `"Lãn Ông"` initial hint priority for Quest 1.
   - Create `Source/UI/ButChiGuideUI.cs`: animated Bút Chì portrait with bobbing, speech bubble anchored to Bút Chì, `[H]` key / click trigger with 0.35s debounce, idle exclamation alert (`!`), and sound cues. Headless safe (guarded by `Raylib.IsWindowReady()`).
   - Update `Tests/HintTests.cs` to verify on-demand hints, quest states, and idle timers.

3. **Collection Album & 2D World Map (Explorer 3)**:
   - Update `Source/Core/Album/AlbumService.cs`: register Page 2 (Huế) with rich metadata, full 4-stamp passport catalog (Hanoi, Hue, HoiAn, SaiGon), and dual-key alias resolution for FactCards (`FactCard_PhoHaNoi` <-> `FactCard_Hanoi_Pho`).
   - Replace/Update `Source/Stages/AlbumViewerScene.cs`: 3 tabs ("Cuốn Sổ Ký Ức", "Tem Du Lịch", "Thẻ Văn Hóa"), Q/E tab cycling, vector badges replacing broken font emojis, Page 2 locked silhouette & lore, 4-slot passport stamp grid, and `IInputService` decoupling.
   - Replace/Update `Source/Stages/WorldMapScene.cs`: regional pins (Hanoi, Hue, HoiAn, SaiGon), Arrow-key and mouse navigation, right-hand preview panel (region name, status, lore, color progress bar, travel button), travel validation preventing locked regions, Vietnam vector map with Biển Đông and islands, and `IInputService` decoupling.
   - Create `Tests/WorldMapAndAlbumEnhancementTests.cs` to thoroughly test album tabs, Page 2 metadata, stamp slots, fact card aliases, arrow key navigation, preview card, and travel validation.

4. **Integration & Game Flow**:
   - In `Source/Stages/HanoiExplorationScene.cs`: wire `DialogueBox` and `ButChiGuideUI`, supporting `[H]` hints, idle timer tracking, `[Tab]` to open Album viewer overlay, and NPC dialogue triggers.
   - In `Program.cs`: coordinate scene switching between `HanoiExplorationScene`, `WorldMapScene`, and `AlbumViewerScene`.
   - Update `PROJECT.md` line 103 status to `IN_PROGRESS`.

5. **Build & Test Verification**:
   - Run `dotnet build GameProject.csproj` -> 0 warnings, 0 errors.
   - Run `dotnet test Tests/GameProject.Tests.csproj` -> all tests pass (410+ existing tests + new M4 tests).

## Source of Truth & Explorer Reports
Read the comprehensive explorer handoff reports:
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (Verbatim user request)
- `/Users/mac/Projects/game1/PROJECT.md` (Milestone 4 Architecture & Contracts)
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_1/handoff.md` (Dialogue System & Typewriter UI)
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/handoff.md` (Hint Engine & Bút Chì Guide UI)
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_3/handoff.md` (Album & World Map Scenes)
- Pre-generated draft files in `.agents/teamwork/teamwork_explorer_m4_2/`: `proposed_HintService.cs`, `proposed_ButChiGuideUI.cs`, `proposed_HintTests.cs`.

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.
Adhere strictly to "lazy senior dev mode" (AGENTS.md): clean, concise code, zero unnecessary abstraction, maintain clean architecture.

## Deliverables
Write your handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m4_1/handoff.md` with:
- Files modified/created
- Build and test results
- Commands used
Send a message when done.
