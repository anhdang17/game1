# Test Infrastructure & Automated Testing Architecture: "Du Ký Bé Mây" (Vertical Slice)

## 1. Executive Summary
This document establishes the comprehensive automated testing infrastructure for the 2D Vertical Slice of **"Du Ký Bé Mây"**.
In accordance with Clean Architecture and headless testability principles, all game domain logic, quest progression states, puzzle rules, dialogue traversal, and persistence mechanisms are decoupled from visual display servers and audio hardware.
The test suite executes via `xUnit` on .NET 10.0 (< 500 ms total execution time) and provides 100% deterministic verification across four rigorous test tiers.

---

## 2. The 4-Tier Testing Methodology

```
┌────────────────────────────────────────────────────────┐
│  Tier 4: Real-World E2E Scenarios (Playthrough Flows)   │
├────────────────────────────────────────────────────────┤
│  Tier 3: Cross-Feature & Subsystem Integrations        │
├────────────────────────────────────────────────────────┤
│  Tier 2: Boundary, Robustness & Negative Edge Cases    │
├────────────────────────────────────────────────────────┤
│  Tier 1: Isolated Feature & Domain Logic Coverage      │
└────────────────────────────────────────────────────────┘
```

### Tier 1: Feature Coverage (Isolated Domain Verification)
Tests each domain feature in isolation to verify fundamental happy-path contracts:
- **Save/Load Persistence (R1)**: Serialization/deserialization of `SaveData` schema v1.0.0, default profile initialization, round-trip fidelity.
- **World Map Progression (R1)**: Regional nodes initialization (`Hanoi`, `Hue`), lock/unlock state transitions, regional color percentage calculation (0% to 100%).
- **Hanoi Quest 1 — Ingredient Gathering (R2)**: 5 authentic phở ingredients pickup (`Item_BanhPho`, `Item_ThitBo`, `Item_NuocDung`, `Item_HanhLa`, `Item_ThaoMoc`), inventory count tracking, quest transition from `InProgress` to `RequirementsMet`.
- **Hanoi Quest 2 — Phở Culinary Assembly (R2)**: 5-slot layering order verification (`Noodles` -> `Beef` -> `Herbs` -> `Broth` -> `Garnish`), victory celebration state, first color bloom (+33%).
- **Hanoi Quest 3 — Long Biên Spatial Grid (R2)**: 7x5 Sokoban-style grid initialization, push mechanics for carts (`1x2`), cyclos (`2x1`), and crates (`1x1`), BFS path clearance detection from `(0, 2)` to `(6, 2)`, puzzle reset logic.
- **Hanoi Level Climax (R2)**: Long Biên Bridge gate unlocking, Memory Journal Page 1 award (`Trang Sổ Ký Ức #1`), Golden Lotus Stamp award (`Tem Hoa Sen Hà Nội`), Gray Shadow narrative letter delivery, 100% color restoration wave.
- **Dialogue Graph Engine (R3)**: JSON schema parsing, node traversal, avatar positioning, branch choice evaluation, and action triggers (`Start_Quest_1`, `Open_Pho_Puzzle`, `Trigger_GrayShadow_Cutscene`).
- **Hint Engine (Bút Chì Companion) (R3)**: Contextual on-demand hint resolution (`[H]`) based on active quest state, dynamic idle prompt activation (> 45s without progress).
- **Collection Album (R4)**: Three-tab album state tracking: Journal Pages, Travel Stamps, and Cultural Fact Cards (4 authentic Hanoi trivia cards).
- **UTF-8 Vietnamese Diacritics**: String fidelity test verifying preservation of Vietnamese tone marks and special characters in dialogue and journal entries.

### Tier 2: Boundary & Corner Cases (Adversarial Robustness)
Probes edge cases, invalid inputs, resource constraints, and fault recovery:
- **Persistence Corruption Recovery**: Simulated truncated JSON, syntax errors, and missing critical fields trigger automatic backup to `.corrupt` and instantiation of clean fallback state without application crash.
- **Atomic File Write Fault Tolerance**: Verification that writing to a temporary file (`.tmp`) prior to atomic replacement prevents partial/corrupted saves upon abrupt termination.
- **Duplicate Ingredient Collection**: Interacting multiple times with a merchant is idempotent; does not duplicate inventory or overflow counters.
- **Invalid Culinary Layering Order**: All 119 incorrect permutations of phở assembly are rejected with specific educational feedback from Bút Chì.
- **Partial Assembly Submissions**: Submitting an incomplete bowl (< 5 ingredients placed) is gracefully rejected without penalty.
- **Spatial Grid Boundary Collisions**: Movable stalls cannot be pushed into walls, outside grid bounds `[0..6, 0..4]`, or into occupied cells.
- **Multi-Cell Obstacle Clearance**: Ensuring 2x1 cyclos and 1x2 flower carts correctly occupy multiple grid cells and block BFS path traversal until fully cleared.
- **Dead-End Recovery**: Triggering `[R]` (reset) cleanly restores original stall coordinates without corrupting grid or quest state.
- **Dialogue Malformed Input**: Broken links (`nextNodeId` targeting non-existent nodes) and cyclic references gracefully terminate dialogue without crashing.
- **Input Spamming**: Rapid consecutive interactions and simultaneous opposite directional inputs (W+S or A+D) resolve cleanly with zero-vector velocity.

### Tier 3: Cross-Feature Interactions (Subsystem Integration)
Validates that disparate modules communicate correctly through decoupled contracts:
- **Quest 1 Completion -> Quest 2 Unlocking -> Mini-Game Transition**: Dialogue interaction with Bà An upon collecting 5/5 ingredients triggers quest completion event, unlocks Quest 2, and initializes the cooking station.
- **Quest 2 Completion -> Color Bloom (+33%) -> Quest 3 Activation**: Culinary validation triggers event bus notification, increasing Hanoi color restoration to 33.3%, updating world map state, and enabling Quest 3.
- **Quest 3 Clearance -> Level Climax Dispatcher**: BFS path completion fires `GateUnlocked` event, triggering Long Biên full color restoration (100%), awarding Journal Page #1 and Lotus Stamp #1 to the Collection Album, and queueing the Gray Shadow letter cutscene.
- **Mid-Game Persistence & State Restoration**: Saving game state at intermediate checkpoints (e.g. after Quest 1, with 3 items gathered, or after Quest 2) and restoring from storage restores exact inventory, quest state, and world progress.
- **Dialogue Action Triggers & Quest Synchronizer**: Selecting specific dialogue options triggers quest starts and mini-game states, updating HUD and quest tracker simultaneously.
- **Album Disabling During Active Gameplay/Puzzles**: Verification that opening the album during active modal mini-games is blocked to prevent modal stacking bugs.

### Tier 4: Real-World Scenarios (End-to-End User Playthrough)
Simulates end-to-end player journeys from initial launch to level completion:
- **Full Hanoi Level 1 Golden Playthrough**:
  1. Boot game from clean state (`NewGame`).
  2. Inspect World Map: Hanoi is unlocked at 0% color; Huế is locked.
  3. Enter Hanoi Old Quarter. Bé Mây spawns at `(120, 250)`.
  4. Walk to Bà An, initiate dialogue, receive Quest 1 (`Quest_Hanoi_01`).
  5. Traverse market: collect Bánh Phở, Thịt Bò, Nước Dùng, Hành Lá, Thảo Mộc from respective stalls.
  6. Return to Bà An with 5/5 ingredients; complete Quest 1.
  7. Enter Quest 2 Phở Assembly: arrange ingredients in correct 5-step order (`Bánh Phở` -> `Thịt Bò` -> `Hành Lá` -> `Nước Dùng` -> `Gia Vị`).
  8. Submit bowl: verify victory fanfare, color bloom (+33%), unlock Quest 3 (`Quest_Hanoi_03`).
  9. Move to Zone C bottleneck: push flower cart and cyclo to clear passage to Long Biên gate.
  10. BFS evaluates clear path: gate unlocks, full 100% color wave ripples over Hanoi and Long Biên Bridge.
  11. Climax rewards triggered: Journal Page 1 added to Album, Lotus Stamp added to Passport, Gray Shadow letter cutscene displayed.
  12. Complete Level 1: Huế region unlocked on World Map.
  13. Trigger Save: persist state to storage.
  14. Terminate game and reboot: reload save file and verify 100% state integrity (Huế unlocked, Hanoi completed, album items present).
- **Stuck Player Assistance Flow**:
  1. Player idles in market or attempts incorrect stall moves.
  2. Dynamic idle timer (> 45s) fires; Bút Chì prompts with contextual advice.
  3. Player requests on-demand hint (`[H]`); Bút Chì provides precise clue.
  4. Player presses `[R]` to reset puzzle and proceeds to victory.

---

## 3. Test Harness & Mock Architecture

### Mock Interfaces (`Tests/Mocks/`)
1. **`MockInputService`**:
   - Implements `IInputService`.
   - Programmatically simulates directional input (`Vector2`), button presses (`Interact`, `Hint`, `Reset`, `Album`, `Pause`), and mouse clicks.
2. **`MockSaveStorage`**:
   - Implements `ISaveStorage`.
   - In-memory dictionary storage simulating file system IO.
   - Provides fault injection methods: `InjectCorruption()`, `SimulateIoException()`, `SimulateTruncation()`.
3. **`NullAudioService`**:
   - Implements `IAudioService`.
   - Silent execution for headless test environments without sound card or ALSA/CoreAudio dependencies.
   - Records played SFX and BGM IDs for assertion (`LastPlayedSfx`, `PlayedSfxCount`).
4. **`TestSimulationDriver`**:
   - Headless runner that drives game loop ticks (`Update(float deltaTime)`).
   - Allows deterministic step-by-step or fast-forward simulation of entire gameplay sessions.

---

## 4. Test Project Configuration

- **Target Framework**: `net10.0`
- **Testing Framework**: `xunit` (v2.9.2)
- **Test Runner**: `xunit.runner.visualstudio` (v2.8.2)
- **SDK**: `Microsoft.NET.Test.Sdk` (v17.12.0)
- **Reference**: `../GameProject.csproj`
- **Execution Command**: `dotnet test --logger "console;verbosity=normal"`

---

## 5. Traceability Matrix

| Requirement | Test Class | Tiers Covered |
|---|---|---|
| R1: Save/Load Durability & Fallback | `SaveLoadTests.cs` | Tier 1, Tier 2, Tier 3 |
| R1: World Map & Color Restoration | `WorldMapTests.cs` | Tier 1, Tier 3, Tier 4 |
| R2: Hanoi Q1 Ingredient Gathering | `QuestFlowTests.cs` | Tier 1, Tier 2, Tier 3 |
| R2: Hanoi Q2 Phở Assembly Puzzle | `PhoPuzzleTests.cs` | Tier 1, Tier 2, Tier 3 |
| R2: Hanoi Q3 Spatial Grid & BFS | `StallPuzzleTests.cs` | Tier 1, Tier 2, Tier 3 |
| R2: Level Climax & Narrative Rewards | `QuestFlowTests.cs` | Tier 1, Tier 3, Tier 4 |
| R3: Dialogue Graph Engine | `DialogueTests.cs` | Tier 1, Tier 2, Tier 3 |
| R3: Bút Chì Hint System | `HintTests.cs` | Tier 1, Tier 2, Tier 4 |
| R4: Collection Album (3 Tabs) | `AlbumTests.cs` | Tier 1, Tier 3, Tier 4 |
| Non-Functional: UTF-8 Diacritics | `Utf8EncodingTests.cs` | Tier 1, Tier 2 |
| Non-Functional: End-to-End Playthrough | `E2EPlaythroughTests.cs` | Tier 4 |
