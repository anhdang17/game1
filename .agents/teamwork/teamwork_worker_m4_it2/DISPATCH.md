# DISPATCH: Milestone 4 Iteration 2 Implementation Worker

## Mission
Implement the remediation for Milestone 4 to achieve 100% test pass (>495 tests), 0 warnings, 0 errors, and pass Gate It2.

## Background & Problem Summary
Baseline tests currently pass 493/495 tests, with exactly 2 failing tests authored by Challenger 1 in `Tests/Milestone4ChallengerTests.cs`:
1. `BugCheck_ExplorationScene_DoesNotUpdateDialogueBoxTypewriter`: `HanoiExplorationScene.cs` does not call `DialogueBox.Update(deltaTime, InputService)` when dialogue is open, leaving `TypewriterProgress = 0` and rendering dialogue text completely blank on screen.
2. `BugCheck_DialogueBox_ChooseOption_WithFilteredBranches_SelectsWrongBranchIndex`: `DialogueBox.cs:ChooseOption(int index)` passes the UI filtered branch index to `_dialogueService.SelectChoice(index)`, which indexes into the unfiltered list `_currentNode.Branches`, causing players to select locked branches that require unmet quest flags.
3. Concurrency safety: `HanoiExplorationScene.cs` default constructor parameter creates `new RaylibAudioService()` which initializes hardware audio devices in headless test runners, causing `ma_device_init` miniaudio assertion crashes during parallel xUnit execution.

## Specific Implementation Requirements

### 1. `Source/Stages/HanoiExplorationScene.cs`
- In `Update(float deltaTime)`:
  When `DialogueBox.IsActive` or `_activeDialogue != null`:
  - Call `DialogueBox.Update(deltaTime, InputService)`.
  - Handle dialogue advancement:
    - If user presses Interact, Confirm, or Cancel:
      - If `!DialogueBox.IsTypingComplete`, call `DialogueBox.CompleteTypewriter()` to immediately display full text.
      - Else if `DialogueBox.IsTypingComplete` and not waiting on choices, dismiss the dialogue:
        `DialogueBox.Dismiss();`
        `_activeDialogue = null;`
        Handle any pending post-dialogue action or transition (e.g., transition to `PhoCookingScene` if Bà An requested cooking, or transition to `StallPuzzleScene` if merchant puzzle, or gate transition).
  - Ensure compatibility with existing tests (`E2EPlaythroughTests`, `Milestone3Challenger1StressTests`, `Milestone3Iteration2ProgressionEmpiricalTests`, `BugCheck_ExplorationScene_DoesNotUpdateDialogueBoxTypewriter`).
- In `TryInteract` or interaction handler:
  - Wire `GetDialogueGraphIdForTarget(target)`: if a dialogue graph ID exists for the interactable, start dialogue via `DialogueService.StartDialogue(graphId)` and show it via `DialogueBox.Show(...)`.
  - If a dialogue graph has action triggers (`Start_Quest_1`, `Open_World_Map`, etc.), ensure `DialogueService.OnActionTriggered` or appropriate handlers handle them.
  - Preserve backward compatibility for interactables that provide immediate interaction results.
- In constructor default parameter for `IAudioService`:
  - Change default from unconditional `new RaylibAudioService()` to:
    `audioService ?? (Raylib.IsWindowReady() ? new RaylibAudioService() : new NullAudioService())`
  - This ensures headless tests and parallel test suites never crash miniaudio.

### 2. `Source/UI/DialogueBox.cs`
- In `ChooseOption(int index)`:
  - Currently:
    ```csharp
    if (index < 0 || index >= _currentBranches.Count) return;
    ...
    var nextNode = _dialogueService.SelectChoice(index);
    ```
  - Fix: Map the selected branch in `_currentBranches[index]` back to the actual branch or index in `_dialogueService`:
    ```csharp
    var selectedBranch = _currentBranches[index];
    // Find the true index in _currentNode.Branches or use ChoiceId/NextNodeId
    var currentNode = _dialogueService.CurrentNode;
    int rawIndex = currentNode?.Branches?.IndexOf(selectedBranch) ?? index;
    if (rawIndex < 0 && currentNode?.Branches != null)
    {
        // Fallback matching by ChoiceId or NextNodeId
        rawIndex = currentNode.Branches.FindIndex(b => b.ChoiceId == selectedBranch.ChoiceId && b.NextNodeId == selectedBranch.NextNodeId);
    }
    var nextNode = _dialogueService.SelectChoice(rawIndex >= 0 ? rawIndex : index);
    ```
  - Also ensure `DialogueService.SelectChoice` has an overload or method if cleaner, e.g. `SelectChoice(DialogueBranch branch)` or `SelectChoiceByChoiceId(string choiceId)`.

### 3. Build & Test Verification
- Run `dotnet build GameProject.csproj` (must be 0 warnings, 0 errors).
- Run `dotnet build Tests/GameProject.Tests.csproj` (must be 0 warnings, 0 errors).
- Run `dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Milestone4ChallengerTests"`: both failing tests must PASS!
- Run the entire test suite `dotnet test Tests/GameProject.Tests.csproj`: ALL tests (>495 tests) must PASS, 0 failures, 0 crashes.

## MANDATORY INTEGRITY WARNING
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Mandatory Inputs to Read
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_reviewer_m4_1/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m4_1/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_auditor_m4_1/handoff.md`
- `/Users/mac/Projects/game1/Tests/Milestone4ChallengerTests.cs`

## Output Requirements
Write your detailed report of changes, build output, and test results to:
`/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m4_it2/handoff.md`
Then use `send_message` to notify the orchestrator when completed.


## 2026-11-09T08:46:13Z
Read your instructions at:
/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m4_it2/DISPATCH.md
Also read ORIGINAL_REQUEST.md at:
/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md

State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m4_it2/
Working directory: /Users/mac/Projects/game1

Implement the required fixes in `Source/Stages/HanoiExplorationScene.cs`, `Source/UI/DialogueBox.cs`, and `Source/Core/Dialogue/DialogueService.cs` as specified in DISPATCH.md.
Run builds and test runs to verify that all tests pass (0 failures, 0 warnings, 0 errors).
Write a comprehensive report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m4_it2/handoff.md` and send_message when done.
