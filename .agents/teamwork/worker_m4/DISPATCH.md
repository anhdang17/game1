## 2026-11-10T04:58:14Z
You are Worker M4 on the Du Ký Bé Mây Visual & Gameplay Revamp team.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY INPUTS:
- ORIGINAL_REQUEST.md path: /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
- Revamp Plan Reference: /Users/mac/.gemini/antigravity/brain/0475d55f-53f9-40a7-ae77-970f01c0dbf1/plan_visual_gameplay_revamp.md
- PROJECT.md path: /Users/mac/Projects/game1/PROJECT.md
- Explorer 3 Report & Implementation Blueprint: /Users/mac/Projects/game1/.agents/teamwork/explorer_survey_3/handoff.md
- Working directory: /Users/mac/Projects/game1
- Your state folder: /Users/mac/Projects/game1/.agents/teamwork/worker_m4

EXCLUSIVE FILE WRITE OWNERSHIP:
You own and may ONLY edit:
1. `Source/Stages/PhoAssemblyScene.cs`
2. `Source/Stages/StallPuzzleScene.cs`
Do NOT touch or modify any other files. DO NOT modify `Source/Core/Puzzles/PhoCookingEngine.cs` or `Source/Core/Puzzles/SpatialGridEngine.cs` under any circumstances!

TASK OBJECTIVE:
Implement Milestone M4 (Minigames Polish):
1. In `Source/Stages/PhoAssemblyScene.cs`:
   - Bouncy drop animation: Add `_slotDropTimers = new float[5]`. In `TryPlaceIngredient`, set `_slotDropTimers[slotIndex] = 1.0f;`. In `Update(dt)`, decay timer. In `DrawBatTrangPorcelainBowl`, apply dynamic bounce offset `bounceY = -MathF.Sin((1f - _slotDropTimers[s]) * MathF.PI) * _slotDropTimers[s] * 20f;`.
   - Particle Juice: Instantiate `ParticleEmitter _particles = new();`.
   - When broth is placed in bowl (`slots.Contains(ItemIds.NuocDung)`), emit steam puffs curling upwards from the bowl.
   - When ingredients drop, emit sparkles at the drop point.
   - On completion (`TryValidate()` success == true): emit 60 confetti particles from bowl center with festive colors (`HanoiPalette.LotusPink`, `HanoiPalette.WallYellow`, `Color(80, 200, 120)`).
   - In `Draw()`, update and draw particles with headless safety (`if (!Raylib.IsWindowReady()) return;`).
2. In `Source/Stages/StallPuzzleScene.cs`:
   - Smooth Lerp Sliding: Add `Dictionary<string, Vector2> _visualPositions = new();`. In `Update(dt)`, smoothly interpolate visual positions towards grid coordinates `Vector2.Lerp(current, target, Math.Clamp(dt * 14f, 0f, 1f));`.
   - In `DrawEntities()`, render stalls using `_visualPositions[entity.Id].X * CellSize` instead of instantaneous jumps.
   - Golden BFS Ribbon: In `DrawPathIndicator()`, render a multi-layered golden ribbon with outer pulse aura (width 10px, glowing gold), inner bright ribbon (width 4px, `Color(255, 235, 120)`), and flowing dash sparkles along the path towards Long Bien gate.
   - Add particle effects: cloud puff when stalls are pushed, confetti celebration when puzzle is solved.
   - Headless safety: Ensure all drawing routines guard with `if (!Raylib.IsWindowReady()) return;`.

VERIFICATION:
- Run `dotnet test Tests/GameProject.Tests.csproj -c Release` and ensure all 567 unit tests pass with 0 failures.

OUTPUT REQUIREMENTS:
- Write your completion report to `/Users/mac/Projects/game1/.agents/teamwork/worker_m4/handoff.md`.
- Include exact build/test commands executed, test results, files modified, and verification evidence.
- Send a completion message to the orchestrator when finished.

