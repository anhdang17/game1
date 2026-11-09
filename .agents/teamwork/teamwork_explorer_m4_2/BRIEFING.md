# BRIEFING — 2026-11-09T01:54:30Z

## Mission
Investigate and design complete Hint Engine and Bút Chì Companion System for Milestone 4 (clean architecture, quest 1-3 hints, idle detection >45s, UI bubble & Bobbing animation, headless testable).

## 🔒 My Identity
- Archetype: explorer
- Roles: investigation, analysis, synthesis, report production
- Working directory: /Users/mac/Projects/game1
- State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/
- Original parent: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Milestone: Milestone 4 (Bút Chì Hint Engine & Companion Guide)

## 🔒 Key Constraints
- Read-only investigation — do NOT implement outside state folder
- Ponytail rule: lazy senior dev mode, smallest complete change, check edge cases
- Clean Architecture: Core has zero Raylib dependencies; UI in Source/UI/
- Automated test coverage for all hint states and idle timers

## Current Parent
- Conversation ID: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Updated: 2026-11-09T01:54:30Z

## Investigation State
- **Explored paths**:
  - `Source/Core/Hints/HintService.cs`
  - `Source/Graphics/VectorRenderer.cs` (DrawButChi, DrawInteractionPrompt)
  - `Source/Graphics/RaylibInputService.cs`
  - `Source/Core/Ports/IInputService.cs`
  - `Source/Core/Ports/IAudioService.cs`
  - `Source/UI/` (identified absence of `ButChiGuideUI.cs`)
  - `Source/Stages/HanoiExplorationScene.cs`, `PhoAssemblyScene.cs`, `StallPuzzleScene.cs`
  - `Tests/HintTests.cs`, `Tests/E2EPlaythroughTests.cs`
- **Key findings**:
  - `HintManager` lacked comprehensive Quest 1 stall coverage (only checked Thảo Mộc and Hành Lá).
  - Preserving `!Q1_ThaoMoc_Collected` priority is critical for `E2EPlaythroughTests.cs` (line 171).
  - `VectorRenderer.DrawButChi` is fully implemented with gentle bobbing, pencil body, blinking eyes, and magic cyan sparkle tip.
  - `ButChiGuideUI` designed as complete drop-in UI component with animated speech bubble, keycap tag [H], idle exclamation alert, word-wrapping, and headless testability.
- **Unexplored areas**:
  - None within Milestone 4 Explorer 2 scope.

## Key Decisions Made
- Designed `proposed_HintService.cs` with full Q1 (5 stalls), Q2 (5 steps), Q3 (Long Biên BFS), and idle prompt logic.
- Designed `proposed_ButChiGuideUI.cs` with Clean Architecture separation (Raylib guarded by `IsWindowReady()`).
- Produced comprehensive 20-test automated suite in `proposed_HintTests.cs`.
- Authored 5-component handoff report in `handoff.md`.

## Artifact Index
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/DISPATCH.md` — Dispatch instructions
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/progress.md` — Liveness heartbeat and task progress
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/BRIEFING.md` — Persistent working memory
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/proposed_HintService.cs` — Drop-in Core HintService replacement
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/proposed_ButChiGuideUI.cs` — Drop-in UI Companion guide component
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/proposed_HintTests.cs` — Comprehensive test suite
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_2/handoff.md` — 5-component handoff report
