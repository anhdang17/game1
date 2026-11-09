# Dispatch: M2 Remediation Explorer 1 (QuestManager / SaveData Desync & Soft-Lock Fix)

## Task
Investigate and design the exact code remediation for the CRITICAL soft-lock reported by Challenger 2:
1. `Source/Stages/HanoiExplorationScene.cs`: Scene constructor fails to hydrate `QuestManager` from loaded `SaveData` / `QuestFlags`.
2. `Source/Core/Interactions/HanoiInteractables.cs`: `BaAnNpc.Interact` calls `CompleteQuest(HanoiQuests.Quest1Id)` without ensuring internal objective synchronization. If `CompleteQuest` fails, it still sets `QuestFlags.Q1_Completed = true`, causing an unrecoverable soft-lock where Quest 2 can never start (`CanStartQuest` returns false).

## References to Read
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m2_2/handoff.md` (Observation 2)
- `Source/Stages/HanoiExplorationScene.cs`
- `Source/Core/Interactions/HanoiInteractables.cs`
- `Source/Core/Quests/QuestManager.cs`

## Output Requirements
Write your remediation design to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_1/handoff.md` with:
- Exact file paths and line numbers
- Drop-in replacement C# code blocks
- Test verification plan to ensure existing tests and `Challenger2EmpiricalTests.cs` pass without soft-locks.
Do NOT modify source files directly.


## 2026-11-08T16:56:34Z
Sender: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
Content: You are Milestone 2 Remediation Explorer 1 (QuestManager / SaveData Desync & Soft-Lock Fix).

State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_1/
Read /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_1/DISPATCH.md and follow all instructions.
Investigate the soft-lock in HanoiExplorationScene constructor and BaAnNpc.Interact.
Design the exact drop-in fix with line numbers and C# replacements.
Write your report to /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_1/handoff.md.
Send a completion message when done. Do NOT modify source files.
