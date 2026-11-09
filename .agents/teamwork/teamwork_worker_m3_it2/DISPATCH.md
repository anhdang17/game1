# Task Assignment: Milestone 3 Iteration 2 Remediation Worker

## Objective
Apply the exact drop-in remediations designed by Explorers 1, 2, and 3 to resolve the two Challenger 1 & 2 defects:
1. `Source/Core/Puzzles/PhoCookingEngine.cs`: reset `IsSolved = false;` in `ValidateAssembly` so post-solve mutation / validation failures do not leave stale `IsSolved = true`.
2. `Source/Stages/StallPuzzleScene.cs`: inject optional `IInputService? inputService = null`, forward confirm/cancel pulses to `_letterModal.Update()`, synchronize `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true`, publish `CutsceneEndedEvent("GrayShadow_Letter")` and `LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám")`, and set `RequestNextScene = true`.
3. `Source/Stages/HanoiExplorationScene.cs`: pass `InputService` to `StallPuzzleScene` constructor, and forward `InputService` pulses to `_grayShadowLetterModal.Update()`.
4. `Tests/Milestone3Challenger1StressTests.cs` and `Tests/ChallengerM3ProgressionStressTests.cs`: update the empirical defect tests to verify the corrected behaviors (asserting `IsSolved == false`, `RequestNextScene == true`, `GrayShadow_LetterRead == true`, `RequestWorldMap == true`).
5. Verify build (`dotnet build GameProject.csproj`) with 0 warnings and 0 errors, and run all unit/regression tests (`dotnet test Tests/GameProject.Tests.csproj`) ensuring 100% pass (391+ tests).

## Source of Truth & Explorer Reports
Read the following reports thoroughly before editing:
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (Verbatim user request)
- `/Users/mac/Projects/game1/PROJECT.md` (Milestone 3 Architecture & Contracts)
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_1/handoff.md` (Cutscene synchronization & gate transition)
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_2/handoff.md` (Headless input forwarding)
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_3/handoff.md` (PhoCookingEngine invariant reset & stress test updates)

## Integrity & Style Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.
Adhere strictly to the "lazy senior dev mode" (AGENTS.md): smallest complete diff, no unnecessary abstraction.

## Output Requirements
Write your detailed handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m3_it2/handoff.md`.
Document every file modified, build output, test execution output, and verification commands.
Send a message back to the orchestrator upon completion.


## 2026-11-09T01:31:59Z
You are the Milestone 3 Iteration 2 Remediation Worker.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m3_it2/

Read your dispatch instructions and explorer handoffs before editing:
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m3_it2/DISPATCH.md
- /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
- /Users/mac/Projects/game1/PROJECT.md
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_1/handoff.md
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_2/handoff.md
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_3/handoff.md

Tasks:
1. Apply the 1-line invariant reset in `Source/Core/Puzzles/PhoCookingEngine.cs:66-70` (`IsSolved = false;`).
2. Update `Source/Stages/StallPuzzleScene.cs`: inject optional `IInputService? inputService = null`, forward confirm/cancel to `_letterModal.Update()`, set `_saveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;`, publish `CutsceneEndedEvent` and `LetterOpenedEvent`, and set `RequestNextScene = true;`.
3. Update `Source/Stages/HanoiExplorationScene.cs`: pass `InputService` to `new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService, InputService);` and forward `InputService` pulses to `_grayShadowLetterModal.Update()`.
4. Update tests in `Tests/Milestone3Challenger1StressTests.cs` and `Tests/ChallengerM3ProgressionStressTests.cs` as specified in the explorer reports.
5. Build project: `dotnet build GameProject.csproj` (0 warnings, 0 errors).
6. Run all tests: `dotnet test Tests/GameProject.Tests.csproj` (all 391+ tests must pass).
