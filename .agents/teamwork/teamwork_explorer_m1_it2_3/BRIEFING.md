# BRIEFING — 2026-11-08T15:58:00Z

## Mission
Investigate Tests/Contracts/ and Tests/GameProject.Tests.csproj to formulate an exact step-by-step remediation plan for eliminating type shadowing (INFRA-01) without implementing code changes directly.

## 🔒 My Identity
- Archetype: explorer
- Roles: investigation, synthesis
- Working directory: /Users/mac/Projects/game1
- State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_3/
- Original parent: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Milestone: Milestone 1 Iteration 2

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Analyze Tests/Contracts/ and Tests/GameProject.Tests.csproj
- Eliminate type shadowing so tests compile against Source/Core/ (GameProject.dll)
- Remove CS0436 suppression
- Propose deletion or migration of useful tests/contracts
- Produce step-by-step remediation plan for Worker

## Current Parent
- Conversation ID: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Updated: not yet

## Investigation State
- **Explored paths**:
  - `ORIGINAL_REQUEST.md`, `PROJECT.md`
  - Reviewer handoff (`teamwork_reviewer_m1_1/handoff.md`)
  - Challenger handoff (`teamwork_challenger_m1_2/handoff.md`)
  - `GameProject.csproj`, `Tests/GameProject.Tests.csproj`
  - `Tests/Contracts/` (all 11 files)
  - `Tests/Mocks/` (`MockSaveStorage.cs`, `MockInputService.cs`, `NullAudioService.cs`, `TestSimulationDriver.cs`)
  - `Source/Core/` (all 32 files)
  - Standard test suites (`SaveLoadTests.cs`, `QuestFlowTests.cs`, `WorldMapTests.cs`, `PhoPuzzleTests.cs`, `StallPuzzleTests.cs`, `DialogueTests.cs`, `HintTests.cs`, `AlbumTests.cs`, `Utf8EncodingTests.cs`, `E2EPlaythroughTests.cs`)
  - Adversarial test suites (`AdversarialCoreTests.cs`, `AdversarialCoreEngineStressTests.cs`)
- **Key findings**:
  - 11 files in `Tests/Contracts/` divide into:
    * 5 shadowed files: `SaveLoadService.cs`, `QuestManager.cs`, `Models.cs`, `Events.cs`, `Ports.cs` (duplicate `Source/Core/`).
    * 6 forward milestone domain implementations: `PhoCookingEngine.cs`, `SpatialGridEngine.cs`, `DialogueService.cs`, `HintService.cs`, `AlbumService.cs`, `WorldMapService.cs`.
  - Type shadowing mechanism: C# prioritizes local compilation (`Tests.dll`) over project reference (`GameProject.dll`), suppressed by CS0436 in csproj line 9.
  - Globbing defect: `GameProject.csproj` implicitly compiles `Tests/**/*.cs`, triggering 444 compiler errors on standalone `dotnet build GameProject.csproj`.
  - Subtle contract differences:
    * `ISaveStorage`: missing `ReplaceFile`, `GetLastWriteTimeUtc`, etc. in `MockSaveStorage`.
    * `IInputService`: missing `Is*JustPressed` properties in `MockInputService`.
    * `SaveLoadService`: missing auto-repair on load corruption.
    * `QuestObjective`: integer overflow vulnerability on unchecked addition.
    * Events: `PuzzleSolvedEvent` missing in `Source/Core/Events/GameEvents.cs`.
    * Objective ID mismatch: `"assemble_pho"` vs `"Objective_AssemblePho"`.
- **Unexplored areas**: None within scope.

## Key Decisions Made
- Recommend Complete De-Shadowing & Domain Clean Architecture Migration (Option 1): Delete the 5 shadowed files and migrate the 6 domain files to `Source/Core/` per `PROJECT.md`.
- Provide complete step-by-step remediation plan with exact file diffs and command verifications for the Worker.

## Artifact Index
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_3/DISPATCH.md` — Incoming dispatches
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_3/progress.md` — Liveness heartbeat
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_3/BRIEFING.md` — Working memory
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_3/handoff.md` — Final handoff report
