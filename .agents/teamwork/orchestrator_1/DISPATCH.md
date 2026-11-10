## 2026-11-10T04:37:53Z

Implement the approved Visual and Gameplay Revamp Plan for Du Ký Bé Mây (vertical slice production) across character visuals, old quarter environment art, particle system, gameplay feel, 7 wonders world map, and storybook UI using Raylib procedural vector graphics.

Working directory: /Users/mac/Projects/game1
Integrity mode: development

Reference: file:///Users/mac/.gemini/antigravity/brain/0475d55f-53f9-40a7-ae77-970f01c0dbf1/plan_visual_gameplay_revamp.md

## Requirements

### R1. Character Presentation Revamp
Redesign Bé Mây into a fluffy magical cloud with expressive eyes, rosy cheeks, and floating motion, replacing the previous humanoid bà ba and nón lá drawing in VectorRenderer.DrawBeMay. Redesign companion Bút Chì with a warm wood body, expressive eyes, and a glowing cyan tip in VectorRenderer.DrawButChi. Render all character visuals purely via procedural Raylib vector graphics without external sprite textures.

### R2. Old Quarter Environment Art & Particle System
Remove the checkerboard grid lines in HanoiExplorationScene.cs (lines 708-715) and replace them with organic cobblestone ground with moss, warm glowing red lanterns, and steaming pho. Implement Source/Graphics/ParticleEmitter.cs as a procedural particle system supporting cloud puffs, pho steam, confetti bursts, and magic sparkles.

### R3. Gameplay Feel & Minigame Polish
Enhance character kinematics with smooth Cloud Glide deceleration and Dash (Space/Shift keys). Upgrade PhoAssemblyScene with bouncy ingredient drops, steam puffs, and confetti completion celebration. Upgrade StallPuzzleScene with smooth lerp sliding animations instead of instant grid jumping.

### R4. 7 Wonders World Map Expansion & Storybook UI
Expand WorldMapScene and WorldMapService to feature 7 world wonders (Hà Nội, Kyoto, Cairo, Paris, Rio, Bắc Cực, Bầu Trời Ký Ức) while retaining all legacy constants to maintain backward compatibility. Upgrade DialogueBox to handmade dó paper with gold trim, and QuestHUD / InventoryBar to a rustic bamboo style.

### R5. Non-Regression & Test Suite Preservation
Ensure all 567 existing unit tests pass with zero failures under Release configuration.

## Acceptance Criteria

### Automated Tests
- [ ] `dotnet test Tests/GameProject.Tests.csproj -c Release` executes with 567/567 passed, 0 failed, 0 errors.

### Character & Environment Visuals
- [ ] Bé Mây renders as a floating fluffy magical cloud with rosy cheeks and expressive eyes without humanoid clothing or legs.
- [ ] Bút Chì renders with warm wood tones, expressive eyes, and cyan magical tip glow.
- [ ] Hanoi exploration street scene renders cobblestone pavement, warm red lanterns, and pho steam, with debug grid lines removed.
- [ ] Visual elements use procedural Raylib vector rendering with no external image asset dependencies.

### Particle System & Gameplay Dynamics
- [ ] Source/Graphics/ParticleEmitter.cs compiles and emits cloud puffs, pho steam, sparkles, and confetti bursts.
- [ ] Character controls support smooth deceleration and dash activation via Space/Shift.
- [ ] PhoAssemblyScene triggers bouncy ingredient drop animations and confetti completion effects.
- [ ] StallPuzzleScene animates pushed stalls smoothly between tiles using lerp interpolation.

### World Map & Storybook UI
- [ ] WorldMapScene displays the 7 wonder locations with progression status.
- [ ] DialogueBox, QuestHUD, and InventoryBar reflect the storybook / bamboo aesthetic.
