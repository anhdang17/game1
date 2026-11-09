# Dispatch: Milestone 2 Worker — Hanoi Exploration & Quest 1

## Objective
Implement all features for Milestone 2:
- Bé Mây 2D movement, inertia, foot collision box, and smooth wall sliding (Feature 5, 27).
- Dynamic Y-sorting with stable tie-breaking (Feature 6).
- 2D Camera follow with lerp and viewport clamping.
- Visual presentation in Raylib-cs: HanoiPalette, FontManager (Vietnamese TTF with 762 codepoints), VectorRenderer (procedural Bé Mây, Bút Chì, bicycle, cyclo, stalls, tables), and ColorBloomEffect (GLSL 330 ripple bloom shader + CPU fallback) (Feature 27).
- Interaction Prompt Bubble `[E] Tương tác` with 48px detection radius, priority selection, and sine bobbing (Feature 7).
- Market merchants and Quest 1 gathering loop (Bà An + 5 ingredient stalls: Bánh Phở, Thịt Bò, Nước Dùng, Hành Lá, Thảo Mộc) with idempotent inventory addition and quest progression (Feature 8).
- UI HUD: `InventoryBar.cs` hotbar with vector ingredient icons and pulse animation, `QuestHUD.cs` objective tracker, and turn-in alert (Feature 9).
- `HanoiExplorationScene.cs` integrating all gameplay mechanics, rendering, and interaction.
- `RaylibAudioService.cs` hardware implementation of `IAudioService`.
- Update `Program.cs` to launch `HanoiExplorationScene` with Raylib window initialization (1280x720, 60 FPS).
- Automated tests in `Tests/MovementCollisionCameraTests.cs` and `Tests/InteractionAndQuest1Tests.cs` verifying kinematics, collision sliding, camera, Y-sorting, interaction range, merchant logic, and Quest 1 gathering headlessly via xUnit.

## Input Documents
- Scope & Contracts: `/Users/mac/Projects/game1/PROJECT.md`
- Original Request: `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- Movement & Physics Handoff: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_1/handoff.md`
- Visual Design & Graphics Handoff: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_2/handoff.md`
- Interaction & Quest 1 Handoff: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_3/handoff.md`

## Write Ownership
You exclusively own:
- `Source/Core/Spatial/` (`AABB.cs`, `CollisionWorld.cs`)
- `Source/Core/Movement/` (`CharacterKinematics.cs`, `Camera2DTracker.cs`, `YSorting.cs`)
- `Source/Core/Interactions/` (`IInteractable.cs`, `InteractionManager.cs`, `HanoiInteractables.cs`)
- `Source/Graphics/` (`HanoiPalette.cs`, `FontManager.cs`, `VectorRenderer.cs`, `ColorBloomEffect.cs`)
- `Source/UI/` (`InventoryBar.cs`, `QuestHUD.cs`, `InteractionBubbleUI.cs`)
- `Source/Stages/` (`HanoiExplorationScene.cs`, `SceneManager.cs`)
- `Source/Audio/` (`RaylibAudioService.cs`)
- `Program.cs`
- `Tests/MovementCollisionCameraTests.cs`
- `Tests/InteractionAndQuest1Tests.cs`

Do NOT touch existing M1 domain code in `Source/Core/Models/`, `Source/Core/Quests/`, `Source/Core/Puzzles/`, `Source/Core/Events/`, `Source/Core/Ports/` unless fixing a genuine compile blocker.

## Verification Requirements
You MUST run:
1. `dotnet build GameProject.csproj` -> 0 warnings, 0 errors.
2. `dotnet test Tests/GameProject.Tests.csproj` -> all tests pass (existing 192 M1 tests + new M2 tests).
3. Document verification output in `.agents/teamwork/teamwork_worker_m2_1/handoff.md`.

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

