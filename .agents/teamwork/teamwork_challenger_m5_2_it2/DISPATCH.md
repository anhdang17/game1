# Task Assignment — M5 Concurrency, Memory & Audio Safety Challenger

## Objective
Empirically challenge system concurrency, headless execution safety, resource lifecycle, and audio stability across repeated execution loops.

## Mandatory Reading
1. `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (Verbatim user specifications)
2. `/Users/mac/Projects/game1/PROJECT.md` (Global architecture & Milestones)
3. `/Users/mac/Projects/game1/TEST_READY.md` & `/Users/mac/Projects/game1/TEST_INFRA.md`
4. `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m5_1/handoff.md` (Worker handoff report)

## Challenge Scope & Stress Scenarios
Author adversarial concurrency and stability tests in `Tests/ChallengerM5PackagingAndPerformanceTests.cs`:
1. Headless Audio Safety Invariant: Verify that across all scenes (`HanoiExplorationScene`, `PhoAssemblyScene`, `StallPuzzleScene`, `WorldMapScene`, `AlbumViewerScene`), instantiating scenes headlessly (when `!Raylib.IsWindowReady()`) never invokes miniaudio hardware initialization and safely uses `NullAudioService`.
2. Multi-Threaded Stress: Run 20 concurrent threads executing game update loops, event bus publishing, dialogue advancement, and save/load operations simultaneously. Verify 0 race conditions, 0 deadlock, 0 exceptions.
3. Memory & Resource Stability: Simulate 2,000 rapid scene transitions between Title, WorldMap, Hanoi, Pho, Stall, and Album to verify no runaway memory allocation or unbounded event subscriber accumulation.
4. UTF-8 & Form C Normalization Under Stress: Test `FontManager.NormalizeText()` and dialogue parser with extreme Vietnamese diacritic combinations, emojis, control codes, and malformed UTF-8 sequences.
5. Full Test Suite Stress: Execute 5 consecutive runs of the entire test suite in Release mode (`dotnet test Tests/GameProject.Tests.csproj -c Release`) to guarantee 0 flaky tests.

## Verification
- Run `dotnet test Tests/GameProject.Tests.csproj -c Release` to verify all tests pass.

## Output
Write `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_2/handoff.md` with:
- Observation (tests authored, concurrency metrics, test execution results)
- Logic Chain
- Caveats
- Conclusion with explicit verdict: **APPROVE** (no bugs found) or **REQUEST_CHANGES** (bugs exposed)
Send completion message to orchestrator (`e7f3d2fb-3839-4a66-a5bc-d1756013c7ba`).

## 2026-11-09T09:37:04Z
[Message from e7f3d2fb-3839-4a66-a5bc-d1756013c7ba]
You are the M5 Concurrency, Memory & Audio Safety Challenger for the "Du Ký Bé Mây" 2D Vertical Slice.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_2/


## 2026-11-09T11:02:28Z
[Message from e7f3d2fb-3839-4a66-a5bc-d1756013c7ba]
You are Challenger 2 for Milestone 5 (Concurrency, Memory & Audio Safety Challenger) for the "Du Ký Bé Mây" 2D Vertical Slice.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_2_it2/

Read your task assignment in `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_2_it2/DISPATCH.md`.
Read `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`, `/Users/mac/Projects/game1/PROJECT.md`, `/Users/mac/Projects/game1/TEST_READY.md`, and `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m5_1/handoff.md`.
Author adversarial concurrency and stability tests in `Tests/ChallengerM5PackagingAndPerformanceTests.cs` covering headless audio safety invariants across all scenes, multi-threaded stress (20 concurrent threads), 2,000 rapid scene transitions for memory stability, and UTF-8 Form C normalization under stress.
Run `dotnet test Tests/GameProject.Tests.csproj -c Release` 5 consecutive times to verify 0 flaky tests.
Write your complete handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_2_it2/handoff.md` with explicit verdict APPROVE or REQUEST_CHANGES.
Send completion message to orchestrator (`e7f3d2fb-3839-4a66-a5bc-d1756013c7ba`).
