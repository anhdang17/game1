# Dispatch: Milestone 4 Explorer 3 (Collection Album & 2D World Map)

## Objective
Investigate and design the complete Collection Album and 2D World Map systems for Milestone 4:
1. **Collection Album (`AlbumViewerScene.cs` or equivalent)**:
   - 3 distinct tabs per user requirements:
     1. **Cuốn Sổ Ký Ức (Journal Pages)**: Displays Page 1 (Hanoi memory restore story), with layout for Page 2 (Huế).
     2. **Tem Du Lịch (Travel Stamps)**: Displays "Tem Hoa Sen Hà Nội" and stamp collection slot.
     3. **Thẻ Văn Hóa (Cultural Fact Cards)**: Displays unlocked trivia cards about Hanoi (Phở Hà Nội, Cầu Long Biên, Phố Cổ 36 Phố Phường, Hoa Sen).
   - Tab switching navigation (keyboard `[Tab]`, `[Q]`/`[E]`, arrows, mouse click).
   - Visual presentation: scrapbook theme, paper textures/vector drawing, unlocked vs locked badges.
2. **2D World Map (`WorldMapScene.cs`)**:
   - Visual map of Vietnam with regional nodes:
     - Hà Nội (unlocked by default, 100% color restoration after M3 climax).
     - Huế (unlocked upon completion of Hà Nội level 1).
     - Future regions (Đà Nẵng, Sài Gòn, Miền Tây) as locked nodes with subtle gray silhouette.
   - Node selection logic: arrow keys / mouse selection, shows region preview, restored color %, and travel prompt (`[Enter]` / Click to enter).
   - Travel validation: prevents selecting locked regions with appropriate feedback message.
   - Smooth scene transitions: Exploration <-> World Map <-> Album.
3. Clean Architecture separation & Headless testability (xUnit tests verifying state, node queries, selection guards, and album contents).

## Inputs
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md` (Verbatim user request, especially § R1 and § R4)
- `/Users/mac/Projects/game1/PROJECT.md` (Architecture, Feature Inventory items 1, 28, Interface Contracts)

## Output Requirements
Produce a comprehensive 5-component handoff report at `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_3/handoff.md`:
- Observation (existing codebase files, current AlbumViewerScene and WorldMapScene)
- Logic Chain (tab architecture, map node state, transition logic)
- Caveats (rendering limits, font glyphs, headless test inputs)
- Conclusion & Drop-In Code Specification
- Verification Method (commands to compile and test)
Send a message when done.


## 2026-11-09T01:46:09Z
Sender: e7f3d2fb-3839-4a66-a5bc-d1756013c7ba
Priority: MESSAGE_PRIORITY_HIGH
Content: You are Milestone 4 Explorer 3 (Collection Album & 2D World Map).
Working directory: /Users/mac/Projects/game1
State folder: /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_3/

Read:
- /Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_3/DISPATCH.md
- /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
- /Users/mac/Projects/game1/PROJECT.md

Investigate the Collection Album and World Map in the codebase:
- `Source/Stages/WorldMapScene.cs`
- `Source/Stages/AlbumViewerScene.cs` (or `Source/UI/AlbumViewerScene.cs`)
- 3 tabs: Cuốn Sổ Ký Ức, Tem Du Lịch, Thẻ Văn Hóa
- World Map node selection (Hà Nội, Huế, locked regions)
- Design complete drop-in enhancements or fixes needed for M4.

Write your 5-component handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m4_3/handoff.md` and send a message when done.
