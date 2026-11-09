# Dispatch: Milestone 2 Remediation Worker (Iteration 2)

## Objective
Apply the exact drop-in remediations designed by the 3 Explorers and Challenger 2 to resolve all findings:
1. Fix QuestManager / SaveData Desync & Soft-Lock:
   - `Source/Stages/HanoiExplorationScene.cs`: Hydrate `QuestManager` from `QuestFlags` in constructor.
   - `Source/Core/Interactions/HanoiInteractables.cs`: In `BaAnNpc.Interact` and `PhoIngredientMerchant.Interact`, call `HydrateFromFlags`, and only set `QuestFlags.Q1_Completed = true` if `CompleteQuest` succeeds.
2. Fix Interaction Proximity Hysteresis & Audio Debounce:
   - `Source/Core/Interactions/InteractionManager.cs`: Implement dual-threshold hysteresis (enter at 48px, exit at 54px) and minimum 0.25s audio prompt cooldown timer.
3. Fix Inventory Alias Symmetry:
   - `Source/Core/Models/Inventory.cs`: Symmetrically handle `Item_ThaoMoc` and `Item_GiaVi` across `AddItem`, `RemoveItem`, and `HasItem`.
4. Fix FontManager Unicode Form C Normalization:
   - `Source/Graphics/FontManager.cs`: Normalize text to Form C in `DrawText`, `DrawTextOutline`, and `MeasureText`, while keeping `GetVietnameseCodepoints()` at exactly 762 codepoints.
5. Fix Pre-existing compiler issues:
   - `Source/Stages/WorldMapScene.cs` line 126 (`new Color((int)r, (int)g, (int)b, 255)`).
   - `Source/Stages/AlbumViewerScene.cs` lines 219 and 223 (`Raylib.DrawRectangleRounded(..., 0.1f, 4, color)`).
6. Update and run test suite:
   - Update `Tests/Challenger2EmpiricalTests.cs` to assert the resolved behaviors as specified in the explorer reports.
   - Run `dotnet build GameProject.csproj` -> 0 warnings, 0 errors.
   - Run `dotnet test Tests/GameProject.Tests.csproj` -> all tests pass (278+ tests).

## References
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_challenger_m2_2/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_1/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_2/handoff.md`
- `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_3/handoff.md`

## Write Ownership
You own:
- `Source/Core/Interactions/HanoiInteractables.cs`
- `Source/Core/Interactions/InteractionManager.cs`
- `Source/Core/Models/Inventory.cs`
- `Source/Graphics/FontManager.cs`
- `Source/Stages/HanoiExplorationScene.cs`
- `Source/Stages/WorldMapScene.cs`
- `Source/Stages/AlbumViewerScene.cs`
- `Tests/Challenger2EmpiricalTests.cs`

## Mandatory Integrity Warning
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## 2026-11-09T00:36:10Z
[Message from e7f3d2fb-3839-4a66-a5bc-d1756013c7ba]
You are the Milestone 2 Remediation Worker.
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m2_it2/
Read /Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m2_it2/DISPATCH.md and follow all instructions.
Read the 3 remediation handoff reports:
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_1/handoff.md
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_2/handoff.md
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m2_remed_3/handoff.md
Apply the exact drop-in code fixes:
1. `Source/Stages/HanoiExplorationScene.cs`: Hydrate `QuestManager` in constructor from `QuestFlags`.
2. `Source/Core/Interactions/HanoiInteractables.cs`: Hydrate before `CompleteQuest` in `BaAnNpc.Interact`, and only mark `Q1_Completed = true` if `CompleteQuest` succeeds. Also hydrate in `PhoIngredientMerchant.Interact`.
3. `Source/Core/Interactions/InteractionManager.cs`: Apply dual-threshold hysteresis (48px enter, 54px exit) and audio cooldown (0.25s).
4. `Source/Core/Models/Inventory.cs`: Apply symmetric alias handling for `Item_ThaoMoc` and `Item_GiaVi`.
5. `Source/Graphics/FontManager.cs`: Add `NormalizeText` with Form C in `DrawText`, `DrawTextOutline`, and `MeasureText`, while keeping 762 codepoints.
6. `Source/Stages/WorldMapScene.cs` (line 126) and `Source/Stages/AlbumViewerScene.cs` (lines 219, 223): Fix minor parameter order typos.
7. `Tests/Challenger2EmpiricalTests.cs`: Update assertions to reflect the remediated behaviors as specified in the reports.
