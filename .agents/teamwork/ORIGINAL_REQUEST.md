# Original User Request

## Initial Request — 2026-11-10T06:01:33Z

This is a single self-contained fix; keep it small and focused. Integrate authentic pixel art sprite assets for the 3 market stalls into the game so their in-game appearance matches the user's reference images exactly, replacing the low-fidelity procedural vector rendering.

Working directory: /Users/mac/Projects/game1
Integrity mode: development

## Requirements

### R1. Pixel Art Sprite Asset Generation
Process and crop the 3 user-provided reference images into clean, transparent PNG texture sprite assets and place them in the project's asset directory (`Data/` or equivalent):
- Quán Phở Bà An (`media_1794288642657_056fa298.jpg`): grandmother cooking broth, canopy, signboards, bowls, condiment trays, stool, A-frame chalkboard.
- Quầy Thịt Bò Bác Tư (`media_1794289263118_647ac935.jpg`): butcher chopping beef on wooden block, hanging ribs and shanks, meat trays, Nhơn Hòa scale, A-frame menu.
- Gánh Rau Củ Quả Cô Lan (`media_1794289672573_d8d071c7.jpg`): vegetable vendor waving, hanging garlic/shallot braids and chilies, tiered produce crates on counter and ground, watering can, A-frame menu.

### R2. In-Game Rendering Integration
Update the game rendering system (replacing the procedural vector drawings in `Source/Graphics/VectorRenderer.cs` for the stalls) to load and render the transparent PNG sprite textures via Raylib `DrawTexturePro` or equivalent:
- Stalls render at their appropriate scale, position, and layer depth within all active gameplay scenes: `HanoiExplorationScene` (street exploration), `StallPuzzleScene` (stall sliding minigame), and related mini-games.
- Maintain existing collision boxes, interaction trigger zones, and dialogue/puzzle interactions without breaking scene logic.

### R3. Non-Regression & Build Verification
Ensure the solution builds cleanly in Release configuration and all existing automated tests pass without regressions (`dotnet test Tests/GameProject.Tests.csproj -c Release`).

## Verification Resources
- Reference Image 1 (Quán Phở Bà An): /Users/mac/.gemini/antigravity/brain/097595dc-a0b9-4e11-89f6-3770124724f3/.user_uploaded/media_1794288642657_056fa298.jpg
- Reference Image 2 (Quầy Thịt Bò Bác Tư): /Users/mac/.gemini/antigravity/brain/097595dc-a0b9-4e11-89f6-3770124724f3/.user_uploaded/media_1794289263118_647ac935.jpg
- Reference Image 3 (Gánh Rau Củ Quả Cô Lan): /Users/mac/.gemini/antigravity/brain/097595dc-a0b9-4e11-89f6-3770124724f3/.user_uploaded/media_1794289672573_d8d071c7.jpg
- Existing Unit Tests: Tests/GameProject.Tests.csproj

## Acceptance Criteria

### Visual Fidelity & Asset Integration
- [ ] Processed transparent PNG sprite textures exist in the game assets directory for all 3 stalls: Quán Phở Bà An, Quầy Thịt Bò Bác Tư, and Gánh Rau Củ Quả Cô Lan.
- [ ] The stalls in Hanoi exploration street scene and stall puzzle scene render using the sprite textures matching the reference images rather than primitive vector geometry.
- [ ] Visual verification: In-game screenshots or frame captures of the 3 stalls confirm visual 1:1 fidelity with the reference pixel art.

### Gameplay & Scene Integration
- [ ] Player can interact with all 3 stalls in `HanoiExplorationScene` and `StallPuzzleScene` with proper interaction bounds and collision boxes intact.
- [ ] Game runs without Raylib texture load failures or memory leaks during scene transitions.

### Non-Regression
- [ ] `dotnet test Tests/GameProject.Tests.csproj -c Release` executes with all tests passing (0 failures, 0 errors).
