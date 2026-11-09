# BRIEFING — 2026-11-09T02:15:00Z

## Mission
Implement all Milestone 4 components (Dialogue System, Bút Chì Hint Engine, Collection Album, and 2D World Map) with high-fidelity UI, zero-warning builds, and comprehensive headless test coverage.

## 🔒 My Identity
- Archetype: worker
- Roles: implementer, qa, specialist
- Working directory: /Users/mac/Projects/game1
- State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m4_1/
- Original parent: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Milestone: Milestone 4 (Dialogue, Hint, Album & World Map)

## 🔒 Key Constraints
- DO NOT CHEAT. All implementations must be genuine. No hardcoded test results, no dummy facade implementations.
- Adhere strictly to "lazy senior dev mode" (AGENTS.md): minimal code that works, no unnecessary abstractions, preserve error handling and validation.
- Zero Raylib GUI crash in headless tests: guard all drawing and input device queries with `Raylib.IsWindowReady()`.
- Vietnamese Unicode: Form C normalization for all diacritics. Avoid Unicode emojis outside loaded TTF codepoint range (use vector badges instead).
- Retain 100% backward compatibility with all 410 existing tests.
- Target: 0 build warnings, 0 errors, all tests pass.

## Current Parent
- Conversation ID: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Updated: 2026-11-09T02:15:00Z

## Task Summary
- **What to build**:
  1. Dialogue System: `Data/dialogues_hanoi.json`, `Source/Data/dialogues_hanoi.json`, `Source/Core/Dialogue/DialogueService.cs`, `Source/UI/DialogueBox.cs`, `Tests/DialogueTests.cs`.
  2. Bút Chì Hint Engine: `Source/Core/Hints/HintService.cs`, `Source/UI/ButChiGuideUI.cs`, `Tests/HintTests.cs`.
  3. Collection Album & World Map: `Source/Core/Album/AlbumService.cs`, `Source/Stages/AlbumViewerScene.cs`, `Source/Stages/WorldMapScene.cs`, `Tests/WorldMapAndAlbumEnhancementTests.cs`.
  4. Integration & Game Flow: `Source/Stages/HanoiExplorationScene.cs`, `Program.cs`, update `PROJECT.md` line 103 status to `IN_PROGRESS`.
- **Success criteria**: Clean compilation (0 warnings, 0 errors), all 442 unit tests pass headlessly in < 350ms.
- **Interface contracts**: PROJECT.md § Interface Contracts
- **Code layout**: PROJECT.md § Code Layout

## Key Decisions Made
- `DialogueService`: Multi-graph JSON parsing (array or envelope), path probing across 4 parent directories, conditional branch filtering by quest flag, event publishing (`DialogueStartedEvent`, `DialogueLineAdvancedEvent`, `DialogueChoiceSelectedEvent`, `DialogueEndedEvent`).
- `DialogueBox`: 38 chars/sec typewriter, Form C Unicode normalization, procedural vector avatars, fast-forward debouncing.
- `HintService`: Preserved initial hint priority `!Q1_ThaoMoc_Collected` ("Lãn Ông") for full backward compatibility, added step-by-step guidance for culinary pho slots and spatial cart sliding.
- `ButChiGuideUI`: Animated companion bobbing, exclamation alerts after 45s idle, speech bubble with word-wrap, headless safety with `Raylib.IsWindowReady()`.
- `AlbumService`: Registered Page 2 (Huế) with rich lore, 4-stamp regional catalog, and bidirectional alias mapping for FactCards (`FactCard_PhoHaNoi` <-> `FactCard_Hanoi_Pho`).
- `AlbumViewerScene` & `WorldMapScene`: Replaced Unicode emojis with custom vector badges to avoid missing-glyph rendering glitches.
- Decoupled scenes via optional `IInputService?` parameter.

## Artifact Index
- `.agents/teamwork/teamwork_worker_m4_1/DISPATCH.md` — Assignment prompt
- `.agents/teamwork/teamwork_worker_m4_1/BRIEFING.md` — Persistent state and working memory
- `.agents/teamwork/teamwork_worker_m4_1/progress.md` — Heartbeat and step progress
- `.agents/teamwork/teamwork_worker_m4_1/handoff.md` — Self-contained completion report

## Change Tracker
- **Files modified/created**:
  - `Data/dialogues_hanoi.json` (created): Complete Hanoi dialogue trees.
  - `Source/Data/dialogues_hanoi.json` (created): Co-located duplicate of dialogues JSON.
  - `Source/Core/Dialogue/DialogueService.cs` (updated): Multi-graph JSON, events, branches, file probing.
  - `Source/UI/DialogueBox.cs` (created): Typewriter UI, procedural avatars, choices, headless safe.
  - `Tests/DialogueTests.cs` (updated): 14 unit tests for dialogue system.
  - `Source/Core/Hints/HintService.cs` (updated): 45s idle prompt, culinary/spatial hints, Q1 priority.
  - `Source/UI/ButChiGuideUI.cs` (created): Companion portrait, speech bubble, idle exclamation alert.
  - `Tests/HintTests.cs` (updated): 20 unit tests for hints and companion UI.
  - `Source/Core/Album/AlbumService.cs` (updated): Hue Page 2, 4-stamp catalog, dual-key aliases.
  - `Source/Stages/AlbumViewerScene.cs` (updated): 3 tabs, vector badges, Q/E cycling, headless safe.
  - `Source/Stages/WorldMapScene.cs` (updated): 4 regional pins, arrow navigation, preview panel, travel validation.
  - `Tests/WorldMapAndAlbumEnhancementTests.cs` (created): 10 tests for album & map features.
  - `Source/Stages/HanoiExplorationScene.cs` (updated): DialogueBox, ButChiGuide, [Tab] Album integration.
  - `Source/Stages/PhoAssemblyScene.cs` (updated): [H] hint key trigger wired to HintService.
  - `Source/Stages/StallPuzzleScene.cs` (updated): [H] hint key trigger wired to HintService.
  - `Program.cs` (updated): Scene coordinator between Exploration, WorldMap, and Album.
  - `GameProject.csproj` (updated): PreserveNewest copy for Data and Source/Data.
  - `PROJECT.md` (updated): Milestone 4 status updated to IN_PROGRESS.
- **Build status**: PASS (0 warnings, 0 errors)
- **Pending issues**: None

## Quality Status
- **Build/test result**: PASS (442/442 tests pass, 0 errors, duration: 319 ms)
- **Lint status**: 0 violations
- **Tests added/modified**: +32 new tests (14 DialogueTests, 20 HintTests, 10 WorldMapAndAlbumEnhancementTests, total suite 442)

## Loaded Skills
- None
