# Dispatch: M2 Remediation Explorer 2 (InteractionManager Distance Hysteresis & Audio Debounce)

## Task
Investigate and design the exact code remediation for the MEDIUM audio chattering issue reported by Challenger 2:
`Source/Core/Interactions/InteractionManager.cs` uses identical entry and exit detection threshold ($48\text{ px}$), causing rapid toggling and 30Hz audio SFX chimes when oscillating across the boundary.

## References to Read
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m2_2/handoff.md` (Observation 3)
- `Source/Core/Interactions/InteractionManager.cs`

## Output Requirements
Write your remediation design to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_2/handoff.md` with:
- Exact file paths and line numbers
- Dual-threshold distance hysteresis (enter at $48\text{ px}$, exit at $54\text{ px}$) and audio prompt debounce cooldown ($\ge 0.25\text{ s}$)
- Drop-in replacement C# code blocks
- Verification test plan.
Do NOT modify source files directly.


## 2026-11-08T16:56:34Z
You are Milestone 2 Remediation Explorer 2 (InteractionManager Distance Hysteresis & Audio Debounce).

State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_2/
Read /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_2/DISPATCH.md and follow all instructions.
Investigate audio chattering and boundary flutter in Source/Core/Interactions/InteractionManager.cs.
Design the dual-threshold distance hysteresis (enter at 48px, exit at 54px) and audio cooldown.
Write your report to /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_it2_2/handoff.md with drop-in C# code.
Send a completion message when done. Do NOT modify source files.
