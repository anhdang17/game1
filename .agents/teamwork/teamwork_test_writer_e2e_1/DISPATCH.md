## 2026-11-08T15:18:17Z
You are teamwork_test_writer_e2e_1, leading the E2E Testing Track for "Du Ký Bé Mây".
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_test_writer_e2e_1/
MANDATORY: Read ORIGINAL_REQUEST.md at /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md and PROJECT.md at /Users/mac/Projects/game1/PROJECT.md before doing anything else.
Also consult the specification mining report at: /Users/mac/Projects/game1/.agents/teamwork/teamwork_spec_miner_survey_2/handoff.md

Your exclusive write ownership:
- `/Users/mac/Projects/game1/TEST_INFRA.md`
- `/Users/mac/Projects/game1/Tests/` (and all subdirectories/files therein, e.g. `GameProject.Tests.csproj`, mocks, tests)
- `/Users/mac/Projects/game1/TEST_READY.md` (upon completion)
- Your state folder: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_test_writer_e2e_1/`
DO NOT modify any files inside `Source/` or `GameProject.csproj`.

Your mission:
1. Create `TEST_INFRA.md` at project root following the required 4-tier methodology (Tier 1: Feature Coverage, Tier 2: Boundary & Corner Cases, Tier 3: Cross-Feature Interactions, Tier 4: Real-World Scenarios).
2. Create `Tests/GameProject.Tests.csproj` targeting `net10.0` with `xunit`, `xunit.runner.visualstudio`, and `Microsoft.NET.Test.Sdk`, referencing `../GameProject.csproj`.
3. Create test harness mocks in `Tests/Mocks/` (`MockInputService.cs`, `MockSaveStorage.cs`, `NullAudioService.cs`, `TestSimulationDriver.cs`).
4. Author comprehensive automated test suites covering:
   - Save/Load durability, schema validation, corruption recovery (R1)
   - World Map node unlocking & color restoration tracking (R1)
   - Hanoi Quest 1 (5 phở ingredient collection, inventory counts) (R2)
   - Hanoi Quest 2 (phở bowl layer ordering, invalid sequence error handling, color bloom) (R2)
   - Hanoi Quest 3 (7x5 street stall grid sliding, BFS path clearance detection to Long Bien gate, reset logic) (R2)
   - Hanoi Level Climax (Gate unlock, Memory Journal Page 1 award, Lotus Stamp award, Gray Shadow letter trigger) (R2)
   - Dialogue System (JSON loading, node traversal, branch choices, action triggers) (R3)
   - Hint System (Bút Chì on-demand hints, idle context prompts) (R3)
   - Collection Album (Journal pages, Travel stamps, Hanoi fact cards) (R4)
5. Run `dotnet test` to verify the test harness compiles and executes.
6. Publish `TEST_READY.md` at project root summarizing all test tiers and command runner.
7. Write your handoff report to:
`/Users/mac/Projects/game1/.agents/teamwork/teamwork_test_writer_e2e_1/handoff.md`
Follow Handoff Protocol. Update progress.md regularly. Notify parent when done.
