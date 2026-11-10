## 2026-11-10T04:47:40Z
You are Worker M5 on the Du Ký Bé Mây Visual & Gameplay Revamp team.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY INPUTS:
- ORIGINAL_REQUEST.md path: /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
- Revamp Plan Reference: /Users/mac/.gemini/antigravity/brain/0475d55f-53f9-40a7-ae77-970f01c0dbf1/plan_visual_gameplay_revamp.md
- PROJECT.md path: /Users/mac/Projects/game1/PROJECT.md
- Explorer 3 Report & Implementation Blueprint: /Users/mac/Projects/game1/.agents/teamwork/explorer_survey_3/handoff.md
- Working directory: /Users/mac/Projects/game1
- Your state folder: /Users/mac/Projects/game1/.agents/teamwork/worker_m5

EXCLUSIVE FILE WRITE OWNERSHIP:
You own and may ONLY edit:
1. `Source/Core/Models/WorldProgression.cs`
2. `Source/Core/WorldMap/WorldMapService.cs`
3. `Source/Stages/WorldMapScene.cs`
4. `Source/UI/DialogueBox.cs`
5. `Source/UI/QuestHUD.cs`
6. `Source/UI/InventoryBar.cs`
Do NOT touch or modify any other files.

TASK OBJECTIVE:
Implement Milestone M5 (7 Wonders World Map Expansion & Storybook UI):
1. In `Source/Core/Models/WorldProgression.cs`:
   - Keep all legacy constants (`Hanoi`, `Hue`, `HoiAn`, `SaiGon`, `Saigon`, `RegionHanoi`, etc.) intact.
   - Add constants for the 7 World Wonders: `Kyoto`, `Cairo`, `Paris`, `Rio`, `BacCuc` (or `Arctic`), `BauTroiKyUc` (or `SkyOfMemories`).
2. In `Source/Core/WorldMap/WorldMapService.cs`:
   - CRITICAL DUAL-CONTRACT INVARIANT: `GetRegionNodes(progression)` MUST return 4 legacy nodes by default so `Tests/WorldMapTests.cs:18` (`Assert.Equal(4, nodes.Count)`) passes 100%!
   - Implement `GetWorldWonderNodes(progression)` returning the 7 World Wonders for `WorldMapScene`.
   - Update `TrySelectRegion(string regionId, ...)` to resolve against both wonder nodes and legacy nodes.
3. In `Source/Stages/WorldMapScene.cs`:
   - Render the 7 World Wonders (Hà Nội, Kyoto, Cairo, Paris, Rio, Bắc Cực, Bầu Trời Ký Ức) with vintage parchment sea chart aesthetic (`#F5ECD7`), compass rose, navigational rhumb lines, and sweeping golden flight arcs.
   - Preserve default navigation compatibility (`NavigateNext`, `NavigatePrev`) for `Tests/WorldMapAndAlbumEnhancementTests.cs`.
4. In Storybook UI:
   - `Source/UI/DialogueBox.cs`: Upgrade dialog frame to handmade dó paper with deckled edges and double-stroke gold trim corner flourishes (`#D4AF37`), vermilion/gold name badge, and ornate frame for character avatars.
   - `Source/UI/QuestHUD.cs`: Upgrade frame to rustic bamboo pole styling with segmented notch joints and woven bamboo background, preserving exact `FormatObjectiveProgress` behavior.
   - `Source/UI/InventoryBar.cs`: Upgrade container from dark grey to a handcrafted bamboo tray with caramel-honey bamboo slats (`#C29B38`) and dark rattan bindings at corners, preserving all slot logic and tests.
   - Headless safety: Ensure all Raylib drawing guards with `if (!Raylib.IsWindowReady()) return;`.

VERIFICATION:
- Run `dotnet test Tests/GameProject.Tests.csproj -c Release` and ensure all 567 unit tests pass with 0 failures.

OUTPUT REQUIREMENTS:
- Write your completion report to `/Users/mac/Projects/game1/.agents/teamwork/worker_m5/handoff.md`.
- Include exact build/test commands executed, test results, files modified, and verification evidence.
- Send a completion message to the orchestrator when finished.
