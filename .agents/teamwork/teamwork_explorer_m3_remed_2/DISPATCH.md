# Dispatch: M3 Remediation Explorer 2 (StallPuzzleScene Headless Input Forwarding)

## Task
Investigate and design the exact code remediation for the headless cutscene deadlock in `StallPuzzleScene`:
In `Source/Stages/StallPuzzleScene.cs`:
The scene does not accept `IInputService` nor does it forward confirm/skip inputs into `_letterModal.Update(dt)`. In headless unit test runners (`Raylib.IsWindowReady() == false`), the modal can never be dismissed via `Update()`.

## References
- `Source/Stages/StallPuzzleScene.cs`
- `Source/UI/GrayShadowLetterModal.cs`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m3_1/handoff.md`

## Output Requirements
Write your remediation design to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_2/handoff.md` with drop-in C# code. Do NOT modify source files directly.


## 2026-11-09T01:26:01Z
You are Milestone 3 Remediation Explorer 2 (StallPuzzleScene Headless Input Forwarding).

State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_2/
Read /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_2/DISPATCH.md and follow all instructions.
Design the drop-in fix for StallPuzzleScene constructor (inject IInputService? inputService = null) and forward confirm/skip inputs to _letterModal.Update() in headless test runs.
Write report to /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_remed_2/handoff.md.
Send completion message when done. Do NOT edit source files.
