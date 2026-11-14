# Project: Du Ký Bé Mây - Realm Progression, UI Polish & Map Sizing

## Architecture
- Scene Coordination & Progression: `Program.cs`, `WorldProgression.cs`, `SaveData.cs`, `HueExplorationScene.cs`. Handles progression unlock after Hanoi completion, safe desync reconciliation, and scene switching to Hue / Central Realm.
- World Map Navigation & Visuals: `WorldMapScene.cs`. Vintage parchment diorama with non-overlapping pin layout (>=64px pairwise spacing), composite hitboxes (pin circle + label pill), responsive layout, Esc/Back navigation, and safe status/feedback positioning.
- Exploration Stage & Camera: `HanoiExplorationScene.cs`. 0.85f camera zoom (+38.4% broader view, zero outer void), spatial gate exit trigger at X >= 1520.
- Storybook UI: `DialogueBox.cs`, `QuestHUD.cs`, `InventoryBar.cs`, `StallPuzzleScene.cs`. Responsive anchoring, anti-collision text formatting, dynamic active quest display (Q1/Q2/Q3), symmetric inventory tray margins, and pixel-aligned button hitboxes.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Hue Exploration Scene & Transition | Create `HueExplorationScene.cs` and wire `Program.cs` scene coordination for Hue travel and return | M1 | ORIGINAL_REQUEST §R1 |
| 2 | Progression Unlock & Desync Safety | Auto-unlock Hue when Hanoi is completed in `WorldProgression.cs` & `SaveData.Sanitize()` | M1 | ORIGINAL_REQUEST §R1 |
| 3 | Expanded World Map Hitboxes & Click Handling | Hitbox checks both pin circle and label pill in `WorldMapScene.cs`; single-click flow preserved | M1 | ORIGINAL_REQUEST §R1 |
| 4 | Long Bien Gate Spatial Exit | Add automatic spatial transition trigger when walking east through opened gate (X >= 1520) | M1 | ORIGINAL_REQUEST §R1 |
| 5 | World Map Pin Non-Overlapping Layout | Redesign pin coordinates with >=64px pairwise separation; shift Rio NW, BacCuc down, S-curve for Vietnam | M1 | ORIGINAL_REQUEST §R3 |
| 6 | Hanoi Exploration Camera Zoom | Set `CameraTracker.Zoom = 0.85f` in `HanoiExplorationScene.cs` for broader view with zero void exposure | M1 | ORIGINAL_REQUEST §R3 |
| 7 | World Map UI Collision Cleanup | Fix BacCuc/info text collision, Album button/feedback banner overlap, preview banner status pill margin, and add Esc/Back button | M1 | ORIGINAL_REQUEST §R2 |
| 8 | Storybook UI Polish & Alignment | Fix DialogueBox text/prompt collision and choice step; dynamic QuestHUD Q1/Q2/Q3 progression; symmetric InventoryBar padding; StallPuzzle hitbox alignment | M1 | ORIGINAL_REQUEST §R2 |
| 9 | E2E Testing Suite & Regression Verification | Comprehensive opaque-box test suite for R1, R2, R3 (Tiers 1-4) with 100% pass and adversarial hardening | M2 | ORIGINAL_REQUEST §Verification |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Core Implementation (Progression, UI Polish, Camera & Map Zoom) | `HueExplorationScene.cs`, `Program.cs`, `WorldProgression.cs`, `SaveData.cs`, `HanoiExplorationScene.cs`, `WorldMapScene.cs`, `DialogueBox.cs`, `QuestHUD.cs`, `InventoryBar.cs`, `StallPuzzleScene.cs` | none | DONE |
| M2 | Final E2E Test Suite & Acceptance Gate | E2E test suite (Tiers 1-4, `TEST_READY.md`), full test pass (631/631 passed), Tier 5 adversarial audit | M1 | DONE |

## Interface Contracts
### `HueExplorationScene` ↔ `Program.cs` / `SceneManager`
- Implements `IScene`.
- Constructor: `HueExplorationScene(SaveData saveData, IAudioService? audioService = null, IInputService? inputService = null, IEventBus? eventBus = null)`
- Properties: `bool RequestWorldMap { get; set; }`
- `Program.cs` transitions:
  - `wm.RequestTravelTo == "Hue"` -> `sceneManager.SwitchScene(new HueExplorationScene(...))`
  - `sceneManager.CurrentScene is HueExplorationScene hue && hue.RequestWorldMap` -> switch to `WorldMapScene(...)`

### `WorldMapScene` ↔ `WorldProgression` & `Program.cs`
- `GetPinAtMouse(Vector2 mouse)` checks pin circle ($r = 28f$) and label pill rectangle (`nameBg`).
- Pin coordinates maintain $\ge 64f$ Euclidean distance for all distinct pairs.
- `RequestTravelTo`: string ID representing target region (`"Hanoi"`, `"Hue"`, etc.).
- Back navigation: `[Esc]` key or back button sets `RequestTravelTo = "Hanoi"`.

### `HanoiExplorationScene` ↔ `Camera2DTracker`
- `CameraTracker.Zoom = 0.85f`.
- Visible world dimensions $1505.9 \times 847.1$ inside map bounds $[0, 0, 1600, 900]$.

## Code Layout
- `Source/Stages/HueExplorationScene.cs` — Owned by M1
- `Program.cs` — Owned by M1
- `Source/Core/Models/WorldProgression.cs` — Owned by M1
- `Source/Core/Models/SaveData.cs` — Owned by M1
- `Source/Stages/HanoiExplorationScene.cs` — Owned by M1
- `Source/Stages/WorldMapScene.cs` — Owned by M1
- `Source/UI/DialogueBox.cs` — Owned by M1
- `Source/UI/QuestHUD.cs` — Owned by M1
- `Source/UI/InventoryBar.cs` — Owned by M1
- `Source/Stages/StallPuzzleScene.cs` — Owned by M1
- `Tests/E2EProgressionAndLayoutTests.cs` & `Tests/MapZoomAndLayoutTests.cs` & `Tests/ResponsiveUILayoutTests.cs` — Owned by E2E Testing Track / M2
