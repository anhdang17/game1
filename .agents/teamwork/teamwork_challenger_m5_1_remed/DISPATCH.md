# Task Assignment — M5 Adversarial E2E & State Corruption Challenger

## Objective
Empirically challenge the end-to-end game logic, state machine transitions, and persistence resilience through adversarial test cases.

## Mandatory Reading
1. `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (Verbatim user specifications)
2. `/Users/mac/Projects/game1/PROJECT.md` (Global architecture & Milestones)
3. `/Users/mac/Projects/game1/TEST_READY.md` & `/Users/mac/Projects/game1/TEST_INFRA.md`
4. `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m5_1/handoff.md` (Worker handoff report)

## Challenge Scope & Stress Scenarios
Author adversarial stress tests in `Tests/ChallengerM5E2EAndStateStressTests.cs`:
1. Save/Load Corruption & Tampering: Corrupted JSON syntax, missing fields, invalid region IDs, negative color percentages, unexpected types — verify graceful fallback without crashes or data corruption.
2. Out-of-Order Quest Objective Tampering: Attempting to complete Quest 2 (Pho Assembly) or Quest 3 (Stall Puzzle) before Quest 1 is active, attempting to submit invalid ingredient combinations, submitting ingredients when inventory is empty.
3. Dialogue & UI State Spamming: Rapid key pressing, fast-forwarding while choices are pending, selecting invalid branch indices, dialogue triggers while another dialogue or modal is already active.
4. Boundary and Spatial Edge Cases: 7x5 stall grid boundary collisions, impossible stall puzzle layouts, out-of-bounds player positions in Hanoi street map.
5. Continuous Multi-Cycle Playthrough: Complete Hanoi, save, reload, travel to Hue, attempt invalid travel to locked regions, verify album data integrity across multiple save/reload cycles.

## Verification
- Run `dotnet test Tests/GameProject.Tests.csproj -c Release` to verify all tests pass.

## Output
Write `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_1/handoff.md` with:
- Observation (tests authored, test results, stress test metrics)
- Logic Chain
- Caveats
- Conclusion with explicit verdict: **APPROVE** (no bugs found) or **REQUEST_CHANGES** (bugs exposed)
Send completion message to orchestrator (`e7f3d2fb-3839-4a66-a5bc-d1756013c7ba`).


## 2026-11-09T09:36:56Z
You are the M5 Adversarial E2E & State Corruption Challenger for the "Du Ký Bé Mây" 2D Vertical Slice.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_1/

Read your task assignment in `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_1/DISPATCH.md`.
Read `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`, `/Users/mac/Projects/game1/PROJECT.md`, `/Users/mac/Projects/game1/TEST_READY.md`, and `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m5_1/handoff.md`.
Author adversarial stress tests in `Tests/ChallengerM5E2EAndStateStressTests.cs` covering save corruption/tampering, out-of-order quest objective completion attempts, dialogue fast-forward/spamming, boundary spatial collisions, and multi-cycle playthrough persistence.
Run `dotnet test Tests/GameProject.Tests.csproj -c Release` and verify results.
Write your complete handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_1/handoff.md` with explicit verdict APPROVE or REQUEST_CHANGES.
Send completion message to orchestrator (`e7f3d2fb-3839-4a66-a5bc-d1756013c7ba`).


## 2026-11-09T10:55:17Z
You are the replacement M5 Adversarial E2E & State Corruption Challenger for the "Du Ký Bé Mây" 2D Vertical Slice.
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_1_remed/

Read your task assignment in `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_1_remed/DISPATCH.md`.
Read `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`, `/Users/mac/Projects/game1/PROJECT.md`, `/Users/mac/Projects/game1/TEST_READY.md`, and `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m5_1/handoff.md`.
Author adversarial stress tests in `Tests/ChallengerM5E2EAndStateStressTests.cs` covering save corruption/tampering, out-of-order quest objective completion attempts, dialogue fast-forward/spamming, boundary spatial collisions, and multi-cycle playthrough persistence.
Run `dotnet test Tests/GameProject.Tests.csproj -c Release` and verify results.
Write your complete handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m5_1_remed/handoff.md` with explicit verdict APPROVE or REQUEST_CHANGES.
Send completion message to orchestrator (`e7f3d2fb-3839-4a66-a5bc-d1756013c7ba`).