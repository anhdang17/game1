## 2026-11-08T16:03:07Z
You are teamwork_worker_m1_it2.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m1_it2/

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY READS BEFORE STARTING:
1. /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
2. /Users/mac/Projects/game1/PROJECT.md
3. /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_1/handoff.md (BUG-01 & BUG-02 remediation plan)
4. /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_2/handoff.md (BUILD-01 remediation plan)
5. /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_it2_3/handoff.md (INFRA-01 remediation plan)

Your write ownership:
- `Source/Core/` and all subdirectories
- `/Users/mac/Projects/game1/GameProject.csproj`
- `/Users/mac/Projects/game1/Tests/` and all subdirectories
- Your state folder: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m1_it2/`

Your mission:
Implement all 4 remediation fixes exactly as planned by the 3 Explorers:
1. BUG-01 (Arithmetic Overflow): Update `Source/Core/Quests/QuestObjective.cs` `IncrementProgress` using the overflow-free completion check:
   ```csharp
   public bool IncrementProgress(int amount = 1)
   {
       if (amount <= 0 || CurrentCount >= TargetCount)
           return false;

       if (amount >= TargetCount - CurrentCount)
           CurrentCount = TargetCount;
       else
           CurrentCount += amount;

       return true;
   }
   ```
2. BUG-02 (Nested Sanitization): Update `Source/Core/Models/SaveData.cs` and child models (`Level1State.cs`, `CollectionAlbum.cs`, `WorldProgression.cs`) to implement comprehensive null-coalescing and nested `Sanitize()` routines.
3. BUILD-01 (Root Project Globbing): Update `GameProject.csproj` to add `<DefaultItemExcludes>$(DefaultItemExcludes);Tests/**</DefaultItemExcludes>` and `<Compile Remove="Tests/**" />`.
4. INFRA-01 (Test De-Shadowing):
   - Move forward domain implementations from `Tests/Contracts/` (`PhoCookingEngine.cs`, `SpatialGridEngine.cs`, `DialogueService.cs`, `HintService.cs`, `AlbumService.cs`, `WorldMapService.cs`) into `Source/Core/` (`Source/Core/Puzzles/`, `Source/Core/Dialogue/`, `Source/Core/Hints/`, `Source/Core/Album/`, `Source/Core/WorldMap/`).
   - Remove redundant contract stubs from `Tests/Contracts/`.
   - In `Tests/GameProject.Tests.csproj`, remove `CS0436` from `<NoWarn>`.
   - Update tests to cleanly reference `GameProject.Core.*`.
5. Run verification commands:
   `dotnet build GameProject.csproj`
   `dotnet build Tests/GameProject.Tests.csproj`
   `dotnet test Tests/GameProject.Tests.csproj`
   Verify ALL builds succeed with 0 errors and all tests pass.
6. Write your handoff report to:
`/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m1_it2/handoff.md`
Follow Handoff Protocol. Update progress.md regularly. Send a message to parent when done.
