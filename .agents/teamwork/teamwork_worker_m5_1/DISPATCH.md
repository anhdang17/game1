# DISPATCH: Milestone 5 Final Integration & E2E Verification Worker

## Mission
Execute Milestone 5 Phase 1: Full E2E Playthrough Verification, UI Polish, and Self-Contained Release Packaging.

## Scope of Work

### 1. `PROJECT.md` Milestone Status Update
- Update lines 102-104 in `PROJECT.md`:
  - Milestone 3: `DONE`
  - Milestone 4: `DONE`
  - Milestone 5: `IN_PROGRESS`

### 2. Full E2E Playthrough Integration Verification
- Examine `Tests/E2EPlaythroughTests.cs` and verify that the full vertical slice narrative and gameplay flow executes seamlessly from start to finish:
  1. **R1**: Title / World Map (Hà Nội available, color restoration 0%, Huế locked).
  2. **R2 - Q1**: Hanoi exploration, controls (WASD/arrows + Interact/Confirm/Cancel), collect 5 phở ingredients across market stalls (`BanhPho`, `ThitBo`, `NuocDung`, `HanhLa`, `ThaoMoc`).
  3. **R2 - Q2**: Return to Bà An, assemble authentic bowl of phở via 5-step drag-and-drop ordering puzzle, trigger 33.3% color bloom.
  4. **R2 - Q3**: Street stall sliding grid puzzle (7x5, BFS path clearance to Long Biên bridge), trigger 100% color bloom.
  5. **R2 - Climax**: Long Biên gate unlocks, receive Memory Journal Page 1 ("Ký ức Phố Cổ"), Lotus Travel Stamp ("Tem Hoa Sen"), trigger Bóng Xám mysterious letter cutscene pointing to Huế.
  6. **R1 - Progression**: World Map reflects Hà Nội 100% color restored, Huế node unlocked for travel.
  7. **R3**: Dialogue trees in `Data/dialogues_hanoi.json` read correctly with Form C Vietnamese diacritics; Bút Chì provides on-demand hints (`[H]`) and idle hints (>45s).
  8. **R4**: Collection Album displays 3 tabs (Cuốn Sổ Ký Ức, Tem Du Lịch, Thẻ Văn Hóa) with unlocked page 1, locked silhouette page 2 (Huế), lotus stamp, and cultural trivia cards.
  9. **R1 - Persistence**: SaveData round-trips to JSON storage, completely preserving unlocked nodes, journal pages, stamps, and color states across simulated restart.
- Add an explicit, all-encompassing E2E test in `Tests/E2EPlaythroughTests.cs` (or `Tests/FullVerticalSliceE2ETests.cs`) if any portion of the full end-to-end chain is not already tested in a single continuous simulation.

### 3. UI Font & Glyphs Polish
- Reviewer 2 in M4 noted that any Unicode symbols (`✕`, `✓`, `★`, `▲`, `▼`, `◄`, `►`, `←`, `→`) should be either drawn with vector shapes or mapped to clean ASCII equivalents so they never render as '?' glyph boxes under `FontManager`.
- Ensure all UI scenes (`WorldMapScene`, `AlbumViewerScene`, `HanoiExplorationScene`, `PhoAssemblyScene`, `StallPuzzleScene`, `DialogueBox`, `ButChiGuideUI`, `GrayShadowLetterModal`) render cleanly.

### 4. Production Packaging
- Build the self-contained Release binary:
  `dotnet publish GameProject.csproj -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true`
- Confirm publish succeeded and check the generated binary path and file size.
- Run the full test suite:
  `dotnet test Tests/GameProject.Tests.csproj -c Release`
  Confirm 100% pass rate (>520 tests), 0 warnings, 0 errors.

## MANDATORY INTEGRITY WARNING
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

## Mandatory Inputs to Read
- `/Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md`
- `/Users/mac/Projects/game1/PROJECT.md`
- `/Users/mac/Projects/game1/TEST_READY.md`
- `/Users/mac/Projects/game1/TEST_INFRA.md`
- `/Users/mac/Projects/game1/Tests/E2EPlaythroughTests.cs`

## Output Requirements
Write your detailed report to:
`/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m5_1/handoff.md`
Then use `send_message` with your summary, build output, test counts, and binary artifact location.


## 2026-11-09T09:10:58Z
Execute Milestone 5 Phase 1 tasks:
1. Update `PROJECT.md` line 102 (M3 -> DONE), line 103 (M4 -> DONE), line 104 (M5 -> IN_PROGRESS).
2. Verify full continuous E2E playthrough test in `Tests/E2EPlaythroughTests.cs` (or create `Tests/FullVerticalSliceE2ETests.cs` if needed) exercising all requirements R1-R4 continuously.
3. Check UI symbols in `FontManager.cs` / vector rendering to ensure 0 missing glyph boxes ('?').
4. Execute `dotnet publish GameProject.csproj -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true`.
5. Run full test suite `dotnet test Tests/GameProject.Tests.csproj -c Release` (must pass 100%, 0 warnings, 0 errors).
Write your handoff report to `/Users/mac/Projects/game1/.agents/teamwork/teamwork_worker_m5_1/handoff.md` and send_message when done.
