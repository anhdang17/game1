## 2026-11-10T04:58:01Z
You are Worker M3 on the Du Ký Bé Mây Visual & Gameplay Revamp team.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY INPUTS:
- ORIGINAL_REQUEST.md path: /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
- Revamp Plan Reference: /Users/mac/.gemini/antigravity/brain/0475d55f-53f9-40a7-ae77-970f01c0dbf1/plan_visual_gameplay_revamp.md
- PROJECT.md path: /Users/mac/Projects/game1/PROJECT.md
- Explorer 2 Report & Implementation Blueprint: /Users/mac/Projects/game1/.agents/teamwork/explorer_survey_2/handoff.md
- Working directory: /Users/mac/Projects/game1
- Your state folder: /Users/mac/Projects/game1/.agents/teamwork/worker_m3

EXCLUSIVE FILE WRITE OWNERSHIP:
You own and may ONLY edit:
1. `Source/Stages/HanoiExplorationScene.cs`
Do NOT touch or modify any other files.

TASK OBJECTIVE:
Implement Milestone M3 (Old Quarter Environment Art & Particle Integration):
1. Remove Cartesian Grid Lines:
   - Permanently remove the checkerboard grid lines (lines 708-715).
2. Environment Art Procedural Overhaul:
   - Replace with organic cobblestone ground: deterministic staggered cobblestone paver slabs with soft mortar joints across the street area (X: 0..1600, Y: 110..850).
   - Render moss patches along the base of colonial walls and curbs.
   - Render 4 deterministic rain puddles reflecting sky color with gentle ripple arcs.
   - Render scattered pink lotus petals and red almond leaves.
   - Upgrade colonial facades: warm yellow walls (`HanoiPalette.WallYellow`), decorative cornices, green louvered shutters with bougainvillea flower boxes.
   - Render warm red lanterns swaying gently in the breeze with 3-tier radial ambient glow halos (`Warm Glow`).
   - Upgrade Long Bien bridge cantilever steel trusses with warm sunset backlighting.
3. Bà An Pho Stall & Steam Integration:
   - In `yQueue`, replace `VectorRenderer.DrawMarketStall` with `VectorRenderer.DrawBaAnPhoStall(BaAn.Position, "Quán Phở Bà An", _totalTime)` so Bà An NPC and broth pot are visible.
   - Hook broth pot steam: instantiate `ParticleEmitter` and continuously emit steam at Bà An's pot: `_particles.EmitSteam(BaAn.Position + new Vector2(16f, -24f), dt)`.
4. Cloud Glide & Dash Integration:
   - In `Update(dt)`, check dash input: `if (_inputService.IsDashJustPressed && Kinematics.TryDash(_inputService.GetMovementInput()))`.
   - When dashing or moving, emit trailing cloud puffs: `_particles.EmitCloudPuff(playerPos, count: 4)`.
   - Update `_particles.Update(dt)` and draw `_particles.Draw()` inside world camera mode.
5. Critical Test Invariants & Reflection Fields:
   - DO NOT rename or remove private reflection fields: `_stallPuzzleModal`, `_phoAssemblyModal`, `_albumViewerModal` (tested by `Tests/Milestone3Iteration2ProgressionEmpiricalTests.cs:246` and `Tests/ChallengerM3ProgressionStressTests.cs:576`).
   - Headless safety: Ensure all drawing routines guard with `if (!Raylib.IsWindowReady()) return;`.

VERIFICATION:
- Run `dotnet test Tests/GameProject.Tests.csproj -c Release` and ensure all 567 unit tests pass with 0 failures.

OUTPUT REQUIREMENTS:
- Write your completion report to `/Users/mac/Projects/game1/.agents/teamwork/worker_m3/handoff.md`.
- Include exact build/test commands executed, test results, files modified, and verification evidence.
- Send a completion message to the orchestrator when finished.
