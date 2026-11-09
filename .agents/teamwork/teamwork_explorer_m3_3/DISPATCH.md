# Dispatch: Milestone 3 Explorer 3 (Level Progression Climax, Rewards & Cutscene)

## Task
Investigate and design the implementation for Feature 17 (Bridge Gate Portal Transition), Feature 18 (Memory Journal Page 1 Award), Feature 19 (Lotus Stamp Award), and Feature 20 (Gray Shadow Letter Cutscene & Auto-Save):
- Design `Source/Core/Services/LevelProgressionService.cs` (or level climax coordinator in `Source/Stages/`).
- Condition: When all 3 Hanoi quests are completed:
  1. Award Memory Journal Page 1 ("Trang Sổ 1: Ký ức Phố Cổ Hà Nội") with story text and artwork metadata into `CollectionAlbum.JournalPages`.
  2. Award Lotus Travel Stamp ("Tem Hoa Sen Hà Nội") with acquisition timestamp into `CollectionAlbum.TravelStamps`.
  3. Unlock Long Biên portal gate with glowing particle/ripple visuals.
  4. Display the Gray Shadow (*Bóng Xám*) letter narrative modal:
     - Typewriter text displaying the mysterious letter left behind by Bóng Xám, pointing Bé Mây and Bút Chì towards their next destination (Huế - Cố đô rêu phong).
     - Continue button transitions or returns to exploration / world map portal.
  5. Trigger atomic persistent auto-save to disk via `ISaveStorage` / `SaveLoadService`, ensuring progress is durable.

## Context Files
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (R1, R2, R4)
- `/Users/mac/Projects/game1/PROJECT.md` (Features 17, 18, 19, 20)
- `Source/Core/Models/CollectionAlbum.cs`
- `Source/Core/Models/SaveData.cs`
- `Source/Core/Services/SaveLoadService.cs`
- `Source/Core/Quests/HanoiQuests.cs`

## Output Requirements
Write your exploration handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_3/handoff.md` with:
- Concrete reward data structures, cutscene UI layout, narrative text, save triggers, and gate portal interactions.
- Automated test plan for headless xUnit verification.
Do NOT modify source files directly.


## 2026-11-09T00:53:40Z
You are Milestone 3 Explorer 3 (Level Progression Climax, Rewards & Cutscene).

State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_3/
Read /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_3/DISPATCH.md and follow all instructions.
Investigate level progression climax upon completing all 3 quests: Journal Page 1 award, Lotus Stamp award, Long Biên portal gate unlock, Bóng Xám letter narrative cutscene, and durable auto-save.
Write your report to /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m3_3/handoff.md.
Send a completion message when done. Do NOT modify source files.
