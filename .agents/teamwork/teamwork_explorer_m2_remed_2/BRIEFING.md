# BRIEFING — 2026-11-08T23:55:00Z

## Mission
Investigate interaction boundary flutter and audio chattering in InteractionManager.cs and design dual-threshold distance hysteresis (48px enter, 54px exit) with audio debounce cooldown (>= 0.25s).

## 🔒 My Identity
- Archetype: explorer
- Roles: investigator, synthesizer
- Working directory: /Users/mac/Projects/game1
- State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_2
- Original parent: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Milestone: Milestone 2 Remediation (Interaction Distance Hysteresis & Audio Debounce)

## 🔒 Key Constraints
- Read-only investigation — do NOT implement
- Do NOT modify source files directly
- Write report to /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_2/handoff.md
- Adhere to Ponytail lazy senior dev principles (smallest complete change, no unneeded abstractions)

## Current Parent
- Conversation ID: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
- Updated: 2026-11-08T23:43:52Z

## Investigation State
- **Explored paths**:
  - `Source/Core/Interactions/InteractionManager.cs`
  - `Source/Core/Interactions/IInteractable.cs`
  - `Source/Core/Interactions/HanoiInteractables.cs`
  - `Source/Stages/HanoiExplorationScene.cs`
  - `Tests/Challenger2EmpiricalTests.cs` (lines 230-350)
  - `Tests/InteractionAndQuest1Tests.cs` (lines 60-165)
  - `.agents/teamwork/teamwork_challenger_m2_2/handoff.md`
- **Key findings**:
  - `InteractionManager` currently uses single symmetric threshold ($r = 48.0\text{ px}$) in `FindNearestInteractable`.
  - Boundary jitter at 48.0px causes `CurrentTarget` to toggle every frame between target and null.
  - In `Update()`, `prevTarget != CurrentTarget` unconditionally triggers `AudioService.PlaySfx(SfxType.InteractPrompt)`, causing 30Hz machine-gun audio spam.
  - Switching between equidistant entities (e.g. Stall_Left and Stall_Right) also toggles targets at frame rate without audio throttling.
  - Adding dual-threshold distance hysteresis ($48\text{ px}$ entry, $54\text{ px}$ exit with $6\text{ px}$ buffer) stabilizes target selection in $[48\text{ px}, 54\text{ px}]$.
  - Adding `AudioPromptCooldown = 0.25f` debounces SFX triggering so prompts play at most once every $0.25\text{ s}$ ($\le 4\text{ Hz}$), even under extreme multi-entity or boundary oscillation.
  - Existing tests in `Tests/InteractionAndQuest1Tests.cs` and `Tests/Challenger2EmpiricalTests.cs` call `FindNearestInteractable(pos, ctx)` directly. Preserving the 2-parameter overload calling `FindNearestInteractable(pos, ctx, null)` ensures 100% backward compatibility.
- **Unexplored areas**: Milestone 3 scene compilation errors (`WorldMapScene.cs`, `AlbumViewerScene.cs`) outside Milestone 2 scope.

## Key Decisions Made
- Expose constants `DefaultRadius = 48.0f`, `DefaultEnterRadius = 48.0f`, `DefaultExitRadius = 54.0f`, `HysteresisBuffer = 6.0f`, `AudioPromptCooldown = 0.25f`.
- Provide backward-compatible `FindNearestInteractable(Vector2, InteractionContext)` delegating to `FindNearestInteractable(Vector2, InteractionContext, IInteractable? currentTarget)`.
- In `Update`, decrement `_audioPromptCooldownTimer` by `deltaTime` and only play SFX when `prevTarget != CurrentTarget && _audioPromptCooldownTimer <= 0f`.
- Document update for `Tests/Challenger2EmpiricalTests.cs` line 263 from expecting 30 chimes to expecting 1 chime, plus 2 new unit tests.

## Artifact Index
- DISPATCH.md — Task dispatch instructions
- BRIEFING.md — Persistent working memory
- progress.md — Liveness heartbeat and progress tracking
- handoff.md — Final remediation handoff report with drop-in C# code
