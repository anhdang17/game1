# DISPATCH: Milestone 4 Iteration 2 Explorer 2 - JSON Dialogue Graph Integration & Branch Choice Indexing

## Mission
Investigate JSON dialogue integration in `HanoiExplorationScene.cs`, `DialogueService.cs`, and `DialogueBox.cs`:
1. Resolve the filtered choice index mismatch bug:
   - In `DialogueBox.cs:219`: `ChooseOption(int index)` passes the UI filtered index to `_dialogueService.SelectChoice(index)`, which indexes into the unfiltered `_currentNode.Branches` list, selecting locked/wrong branches when branches are filtered by quest flags.
   - Design clean resolution: e.g. selecting by `ChoiceId`, mapping filtered index to raw index, or updating `DialogueService.SelectChoice` to handle filtered branches or branch objects.
2. Resolve gameplay JSON dialogue triggering:
   - `HanoiExplorationScene.cs` loads `Data/dialogues_hanoi.json` and has `GetDialogueGraphIdForTarget(...)`, but never calls `DialogueService.StartDialogue(...)` during gameplay, serving legacy hardcoded strings from M2 instead.
   - Investigate how NPC interactions (Bà An intro/turn-in, stalls, gate) can trigger their authentic JSON dialogue graphs while preserving interaction callbacks and action triggers (`Start_Quest_1`, `Open_World_Map`, etc.) seamlessly.
   - Ensure backward compatibility with existing tests expecting interaction dialogue or direct quest flow.

## Mandatory Inputs to Read
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_reviewer_m4_1/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m4_1/handoff.md`
- `/Users/mac/Projects/game1/Source/Core/Dialogue/DialogueService.cs`
- `/Users/mac/Projects/game1/Source/UI/DialogueBox.cs`
- `/Users/mac/Projects/game1/Source/Stages/HanoiExplorationScene.cs`
- `/Users/mac/Projects/game1/Data/dialogues_hanoi.json`
- `/Users/mac/Projects/game1/Tests/Milestone4ChallengerTests.cs`

## Output Requirements
Write your findings and concrete remediation strategy to:
`/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_it2_2/handoff.md`
Do NOT implement code directly. Present code diff proposals, exact line numbers, and impact analysis.


## 2026-11-09T02:27:50Z
Read your task instructions at:
/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_it2_2/DISPATCH.md
Also read ORIGINAL_REQUEST.md at:
/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md

State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_it2_2/
Working directory: /Users/mac/Projects/game1

Investigate `DialogueBox.ChooseOption` filtered choice index mismatch vs `DialogueService.SelectChoice`, and how gameplay interactions in `HanoiExplorationScene.cs` can trigger JSON dialogue trees from `Data/dialogues_hanoi.json`. Produce a comprehensive handoff report at `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_it2_2/handoff.md` and send_message when done. Do NOT edit source files.
