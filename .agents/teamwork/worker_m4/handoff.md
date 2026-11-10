# Milestone M4 (Minigames Polish) Handoff Report

## 1. Observation
- **Target Files**:
  - `Source/Stages/PhoAssemblyScene.cs`: Previously lacked dynamic drop bounce animations, steam puff emissions from broth, placement sparkles, and confetti celebration effects.
  - `Source/Stages/StallPuzzleScene.cs`: Previously jumped stalls instantaneously to integer coordinates `entity.X * CellSize`, used a plain 6px line for BFS path indicator, and lacked tactile stall push cloud puffs and victory confetti.
- **Constraints & Contracts**:
  - `PhoCookingEngine.cs` and `SpatialGridEngine.cs` were strictly left untouched to preserve domain logic integrity.
  - Unit tests execute in headless mode (`Raylib.IsWindowReady() == false`). All drawing routines and Raylib queries must remain safely guarded.
- **Verification Results**:
  - `dotnet build -c Release`: Built with 0 errors and 0 warnings in modified files.
  - `dotnet test Tests/GameProject.Tests.csproj -c Release`: Passed! Failed: 0, Passed: 567, Skipped: 0, Total: 567, Duration: 10 s.

## 2. Logic Chain
- **Step 1 (Pho Bouncy Drop Animation & Particle Juice)**:
  - Added `_slotDropTimers = new float[5];` and initialized slot drop timer to `1.0f` in `TryPlaceIngredient(slotIndex, cardIndex)`.
  - In `Update(dt)`, timers decay smoothly via `MathF.Max(0f, _slotDropTimers[i] - dt * 3.5f)`.
  - In `DrawBatTrangPorcelainBowl`, applied bounce offset `bounceY = -MathF.Sin((1f - _slotDropTimers[s]) * MathF.PI) * _slotDropTimers[s] * 20f;` to ingredient layer geometry (noodles, beef, herbs, broth, and star anise spices) and slot icons.
  - Integrated `ParticleEmitter _particles = new();`.
  - In `Update(dt)`, emitted curling steam puffs when broth is in the bowl (`_engine.CurrentSlots.Contains(ItemIds.NuocDung)`).
  - Emitted sparkles at `GetSlotPosition(slotIndex)` whenever ingredients are placed.
  - On `TryValidate()` completion success, emitted 60 confetti particles and festive sparkles (`HanoiPalette.LotusPink`, `HanoiPalette.WallYellow`, `Color(80, 200, 120)`).
  - In `Draw()`, rendered active particles guarded by `if (!Raylib.IsWindowReady()) return;`.
- **Step 2 (Stall Puzzle Smooth Lerp Sliding & Golden BFS Ribbon)**:
  - Added `Dictionary<string, Vector2> _visualPositions = new();` initialized with each entity's coordinate.
  - In `Update(dt)`, interpolated visual positions towards grid coordinates with `Vector2.Lerp(current, target, Math.Clamp(dt * 14f, 0f, 1f));`.
  - In `DrawEntities()`, rendered stalls using `_visualPositions[entity.Id].X * CellSize` and `_visualPositions[entity.Id].Y * CellSize`.
  - In `Push()`, emitted 4 cloud puff particles at the pushed stall's origin.
  - In `TriggerSolve()`, emitted confetti bursts and victory sparkles at the Long Bien bridge gate and puzzle board center.
  - In `DrawPathIndicator()`, rendered a multi-layered golden ribbon:
    1. Outer pulsing aura ribbon (width 10px, glowing golden pulse `new Color(255, 215, 0, pulse)`).
    2. Inner bright ribbon (width 4px, `Color(255, 235, 120)`).
    3. Flowing dash sparkles moving along the path segments towards Long Bien gate.
  - Guarded all drawing routines behind `if (!Raylib.IsWindowReady()) return;`.

## 3. Caveats
- No caveats. All 567 unit tests pass without regression, and rendering enhancements operate exclusively under valid window contexts.

## 4. Conclusion
Milestone M4 (Minigames Polish) is fully implemented across `PhoAssemblyScene.cs` and `StallPuzzleScene.cs` adhering to the minimal change principle, ponytail senior dev guidelines, and zero headless crashes.

## 5. Verification Method
- Build command:
  ```bash
  dotnet build -c Release
  ```
- Test command:
  ```bash
  dotnet test Tests/GameProject.Tests.csproj -c Release
  ```
- Invalidation Conditions:
  - If any of the 567 unit tests fail.
  - If `PhoCookingEngine.cs` or `SpatialGridEngine.cs` are modified.

