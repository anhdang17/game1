# Dispatch: M2 Remediation Explorer 1 (QuestManager / SaveData Desync & Soft-Lock)

## Task
Investigate and design the exact code remediation for the CRITICAL soft-lock reported by Challenger 2:
1. `Source/Stages/HanoiExplorationScene.cs`: Scene constructor fails to hydrate `QuestManager` from loaded `SaveData` / `QuestFlags`.
2. `Source/Core/Interactions/HanoiInteractables.cs`: `BaAnNpc.Interact` calls `CompleteQuest(HanoiQuests.Quest1Id)` without ensuring internal objective synchronization. If `CompleteQuest` fails, it still sets `QuestFlags.Q1_Completed = true`, causing an unrecoverable soft-lock where Quest 2 can never start (`CanStartQuest` returns false).

## References to Read
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m2_2/handoff.md` (Observation 2 & Conclusion 1-2)
- `Source/Stages/HanoiExplorationScene.cs`
- `Source/Core/Interactions/HanoiInteractables.cs`
- `Source/Core/Quests/QuestManager.cs`

## Output Requirements
Write your remediation report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_1/handoff.md` with exact line-by-line replacements and unit tests to ensure `Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock` and `Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData` in `Tests/Challenger2EmpiricalTests.cs` pass.
Do NOT modify source files directly.


## 2026-11-08T23:43:52Z
You are Milestone 2 Remediation Explorer 1 (QuestManager / SaveData Desync & Soft-Lock).

State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_1/
Read /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_1/DISPATCH.md and follow all instructions.
Investigate the soft-lock in Source/Stages/HanoiExplorationScene.cs constructor and Source/Core/Interactions/HanoiInteractables.cs (BaAnNpc.Interact).
Design the exact drop-in fix with line numbers and C# replacements.
Write your report to /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_1/handoff.md.
Send a completion message when done. Do NOT modify source files.
