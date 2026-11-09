# BRIEFING — 2026-11-09T00:35:00Z

## Mission
Investigate and design the exact code remediation for QuestManager / SaveData desync and soft-lock in HanoiExplorationScene constructor and BaAnNpc.Interact.

## 🔒 My Identity
- Archetype: explorer
- Roles: investigation, synthesis
- Working directory: /Users/mac/Projects/game1
- State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_1
- Original parent: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Milestone: Milestone 2 Remediation

## 🔒 Key Constraints
- Read-only investigation — do NOT implement / do NOT modify source files directly
- Design exact drop-in fix with line numbers and C# replacements
- Produce 5-component handoff.md in state folder

## Current Parent
- Conversation ID: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Updated: not yet

## Investigation State
- **Explored paths**:
  - `Source/Stages/HanoiExplorationScene.cs`: Constructor lines 60-85, quest state handling lines 163-172
  - `Source/Core/Interactions/HanoiInteractables.cs`: `BaAnNpc.Interact` lines 40-90, `PhoIngredientMerchant.Interact` lines 140-185
  - `Source/Core/Quests/QuestManager.cs`: `HydrateFromFlags` lines 186-233, `CompleteQuest` lines 133-147, `RecordObjectiveProgress` lines 87-128
  - `Tests/Challenger2EmpiricalTests.cs`: Lines 626-670 (`Bug_BaAnNpc_TurnIn_WhenQuestFlagsAreSetDirectly_CausesDesyncSoftLock`, `Bug_HanoiExplorationScene_Constructor_FailsToHydrateQuestManagerFromSaveData`)
- **Key findings**:
  1. `HanoiExplorationScene` constructor initializes `QuestManager` and registers quests, but never calls `QuestManager.HydrateFromFlags(QuestFlags)`.
  2. `BaAnNpc.Interact` State 2 checks `QuestFlags.AreAllQ1IngredientsCollected()`, calls `QuestManager.CompleteQuest`, and unconditionally sets `QuestFlags.Q1_Completed = true` regardless of `CompleteQuest` return value.
  3. When `QuestManager` is not in sync with `QuestFlags` (e.g. loaded save game or direct flag alteration), `CompleteQuest` returns `false`, but `QuestFlags.Q1_Completed` becomes `true`. Subsequent interactions hit State 3, permanently locking `Quest_Hanoi_02`.
  4. In `PhoIngredientMerchant.Interact`, `RecordObjectiveProgress` requires `QuestState.InProgress`. If flags have `Q1_Started = true` but `QuestManager` is not hydrated, it returns `false`.
  5. Challenger 2's empirical tests assert the buggy behavior (soft-lock demonstrated). Once remediated, the assertions in `Challenger2EmpiricalTests.cs` must be updated to assert the fixed behavior (`Assert.True(qm.IsQuestCompleted(HanoiQuests.Quest1Id))` and `Assert.Equal(QuestState.InProgress, ...)`).
- **Unexplored areas**: None for M2 Remediation 1 scope.

## Key Decisions Made
- Designed minimal drop-in replacements for `HanoiExplorationScene.cs`, `HanoiInteractables.cs` (`BaAnNpc` and `PhoIngredientMerchant`), and `Challenger2EmpiricalTests.cs`.
- Adhered strictly to Ponytail lazy senior dev rules: no new abstraction classes, reuse existing `QuestManager.HydrateFromFlags`, shortest working diffs.

## Artifact Index
- handoff.md — 5-component remediation report with exact diffs and line numbers
- progress.md — Liveness heartbeat and step progress
- DISPATCH.md — Task instructions and prompt history
