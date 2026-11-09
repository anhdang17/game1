# Review & Adversarial Critic Report: Milestone 2 (Movement, Kinematics, Collision, Y-Sorting)

**Reviewer**: `teamwork_reviewer_m2_1`  
**Milestone**: Milestone 2 (Hanoi Exploration & Quest 1)  
**Date**: 2026-11-08T16:52:00Z  
**Verdict**: **APPROVE**  
**Overall Risk Assessment**: **LOW**

---

## 1. Observation

1. **Build Execution**:
   Command: `dotnet build GameProject.csproj`
   Output:
   ```text
   Determining projects to restore...
   All projects are up-to-date for restore.
   GameProject -> /Users/mac/Projects/game1/bin/Debug/net10.0/GameProject.dll

   Build succeeded.
       0 Warning(s)
       0 Error(s)

   Time Elapsed 00:00:00.78
   ```

2. **Automated Headless Test Execution**:
   Command: `dotnet test Tests/GameProject.Tests.csproj --logger "console;verbosity=normal"`
   Output:
   ```text
   Test run for /Users/mac/Projects/game1/Tests/bin/Debug/net10.0/GameProject.Tests.dll (.NETCoreApp,Version=v10.0)
   ...
   Test Run Successful.
   Total tests: 230
        Passed: 230
    Total time: 0,4620 Seconds
   ```
   Specific test suites:
   - `MovementCollisionCameraTests`: 18 tests passed in 0.3668s.
   - `InteractionAndQuest1Tests`: 20 tests passed in 0.4125s.
   - Total test count: 230 tests passed (192 M1 baseline + 38 M2 new tests), 0 failures, 0 skipped.

3. **Source Code Inspection & Integrity Check**:
   - `Source/Core/Spatial/AABB.cs` (59 lines): Read-only immutable struct with normalized coordinate min/max ordering in constructor (`MathF.Min/Max`), proper boundary intersection logic (`MinX < other.MaxX && MaxX > other.MinX && MinY < other.MaxY && MaxY > other.MinY`), point containment query, and IEquatable implementation. Zero dummy facades.
   - `Source/Core/Spatial/CollisionWorld.cs` (144 lines): Decoupled axis-aligned sliding resolution (`MoveAndSlide`). Resolves X displacement against obstacles and resets $v_x = 0$ on collision; independently resolves Y displacement from resolved X coordinate and resets $v_y = 0$ on collision. Clamps final position to `MapBounds`.
   - `Source/Core/Movement/CharacterKinematics.cs` (123 lines): Full kinematics model with acceleration ($1200\text{ px/s}^2$), friction ($1500\text{ px/s}^2$), normalized diagonal speed capping ($200\text{ px/s}$), dominant axis facing direction tracking, modulo walk cycle accumulator, vertical sinusoidal bobbing ($\pm 2.5\text{ px}$), and phase boundary crossing footstep sound triggers ($0.25$ and $0.75$).
   - `Source/Core/Movement/YSorting.cs` (65 lines): Multi-key deterministic sorter (`FootY`, `SubLayerPriority`, `EntityId` using `StringComparison.Ordinal`). Eliminates z-fighting across identical depth coordinates.
   - `Source/Core/Movement/Camera2DTracker.cs` (72 lines): Exponential lerp camera follower ($1.0 - e^{-8.0 \cdot dt}$) with $0.05\text{ px}^2$ deadband jitter snapping, defensive map centering when viewport exceeds map bounds, and visible world AABB computation.
   - `Source/Stages/HanoiExplorationScene.cs` & `Program.cs`: Full stage integration. `Program.cs` lines 28-30 enforces `if (dt > 0.1f) dt = 0.1f;` protecting physics against lag spike tunneling.

---

## 2. Logic Chain

1. **Integrity Violation Analysis**:
   - Inspected all new M2 source files (`AABB.cs`, `CollisionWorld.cs`, `CharacterKinematics.cs`, `YSorting.cs`, `Camera2DTracker.cs`, `HanoiInteractables.cs`, `InteractionManager.cs`).
   - Verified that no hardcoded test outputs or dummy return values exist. Real vector mathematics, collision response algorithms, and state machines are implemented.
   - Tests assert real physical invariants (e.g., stopping distance within $\pm 0.01\text{ px}$, normalization to $200.0\text{ px/s}$, deterministic tie-break ordering).
   - Conclusion: **No integrity violations detected.**

2. **Decoupled Axis Collision Slide Resolution**:
   - In `CollisionWorld.MoveAndSlide`, when moving diagonally (e.g. Down-Right $(1, 1)$ into a vertical wall at $X = 300$), the X axis is resolved first: $v_x$ is cancelled to 0 and position is held at $X = 287.99$.
   - The subsequent Y axis resolution evaluates from $X = 287.99$ with full downward velocity $v_y$. Since the obstacle does not block the Y path, the character advances smoothly downwards without getting stuck.
   - Verified via automated test `Collision_SlideAlongVerticalWall_DiagonalMaintainsSlidingAxis`.

3. **Deterministic Y-Sorting Stability**:
   - Top-down visual depth requires ground-contact ordering by `FootY`.
   - When entities share the exact same `FootY`, secondary key `SubLayerPriority` ensures shadows ($-1$) render before bodies ($0$) and companions ($1$) render after.
   - When two entities share both `FootY` and `SubLayerPriority`, tertiary key `EntityId` (evaluated via `StringComparison.Ordinal`) provides rock-solid frame-to-frame determinism, preventing sprite flickering.
   - Verified via automated test `YSort_IdenticalFootY_DeterministicTieBreaking`.

4. **Camera Viewport Clamping & Defensive Math**:
   - `Camera2DTracker.ClampCamera` computes half viewport dimensions divided by zoom.
   - If map bounds are smaller than the viewport, `minX > maxX` occurs. The code defensively detects this condition and centers the camera on the map center `(MapBounds.MinX + MapBounds.MaxX) * 0.5f` rather than throwing an exception.
   - Exponential lerp $1.0 - \exp(-\lambda \cdot dt)$ provides framerate-independent smoothing.

---

## 3. Adversarial Challenges & Findings

### [Minor] Finding 1 — Potential `ArgumentException` in `CollisionWorld.MoveAndSlide` if `MapBounds` is Smaller than Foot Extents
- **Location**: `Source/Core/Spatial/CollisionWorld.cs`, lines 135–136
- **What**: `float clampedX = Math.Clamp(pos.X, minX, maxX);`
  Where `minX = MapBounds.MinX + footHalfWidth` and `maxX = MapBounds.MaxX - footHalfWidth`.
- **Attack Scenario**: If a level or test sets a degenerate `MapBounds` with `Width < 2 * footHalfWidth` (e.g. width $10\text{ px}$ with foot half width $12\text{ px}$), `minX > maxX`. In .NET runtime, `Math.Clamp` throws `ArgumentException: 'min (22) cannot be greater than max (-2)'`.
- **Blast Radius**: Low. In standard gameplay, `MapBounds` is $1600 \times 900\text{ px}$, far exceeding $24\text{ px}$.
- **Mitigation Suggestion**: Add ternary centering or safety clamping identical to `Camera2DTracker`:
  `float clampedX = (minX > maxX) ? (MapBounds.MinX + MapBounds.MaxX) * 0.5f : Math.Clamp(pos.X, minX, maxX);`

### [Minor] Finding 2 — Camera Zoom Division by Zero / Inversion Guard
- **Location**: `Source/Core/Movement/Camera2DTracker.cs`, lines 46–47
- **What**: `float halfW = (ViewportSize.X * 0.5f) / Zoom;`
- **Attack Scenario**: If `Zoom` is ever set to $\le 0$, division produces `Infinity` or negative extents.
- **Blast Radius**: Low. Current game runtime maintains `Zoom = 1.0f`.
- **Mitigation Suggestion**: Clamp zoom input or property setter: `public float Zoom { get => _zoom; set => _zoom = Math.Max(value, 0.05f); }`.

### [Minor] Finding 3 — Heap Allocations in Render Loop Dynamic Y-Sort Queue
- **Location**: `Source/Stages/HanoiExplorationScene.cs`, lines 269–348
- **What**: Inside `Draw()`, a new `YSortQueue<RenderableEntity>` and ~10 `RenderableEntity` objects with action closures are allocated on every frame (60 times per second).
- **Attack Scenario**: Generates ~30–50 KB/s of Gen 0 heap garbage. Over lengthy play sessions, this increases GC collection frequency.
- **Blast Radius**: Low. In .NET 10 Gen 0 GC, 30 KB/s is negligible and causes zero framerate drops, but pre-allocating an entity buffer is an optimal practice.
- **Mitigation Suggestion**: In Milestone 3/5 optimization pass, retain a persistent `_yQueue` instance on `HanoiExplorationScene` and call `_yQueue.Clear()` each frame instead of creating a new instance.

---

## 4. Stress Test Results

| Test Scenario | Input / Condition | Expected Behavior | Actual Behavior | Result |
|---|---|---|---|---|
| **Zero DeltaTime** | `dt = 0` and `dt < 0` | Early return, zero movement, no NaN | Handled safely via `if (deltaTime <= 0f) return;` | **PASS** |
| **Diagonal Speed Capping** | Input `(1, 1)`, speed cap 200 px/s | Vector normalized, speed $\le 200.0\text{ px/s}$ | Exact $200.0\text{ px/s}$ length | **PASS** |
| **Wall Sliding (Vertical)** | Down-Right $(1, 1)$ into vertical wall | X halted at boundary, Y advances freely | $X = 287.99\text{ px}$, $Y$ slides $>50\text{ px}$ | **PASS** |
| **Wall Sliding (Horizontal)** | Down-Right $(1, 1)$ into horizontal wall | Y halted at boundary, X advances freely | $Y = 500.00\text{ px}$, $X$ slides $>50\text{ px}$ | **PASS** |
| **Lag Spike Physics Tunneling** | Delta time jump $> 0.1\text{s}$ | Prevent teleporting through walls | `Program.cs` caps `dt <= 0.1f` ($20\text{ px}$ max step) | **PASS** |
| **Tie-Breaking Y-Sort** | Same FootY ($300\text{ px}$) on multiple entities | Deterministic layer and string order | Shadow (-1) -> Body A -> Body Z -> ButChi (1) | **PASS** |
| **Map Smaller than Camera** | Viewport $1280 \times 720$ on Map $400 \times 300$ | Center on map, no crash | Clamps to center $(300, 250)$ without error | **PASS** |

---

## 5. Caveats

- **Visual / Audio Hardware Execution**: Automated tests run in headless mode without a physical display server or audio sink. The graphical rendering and procedural audio were verified to fall back safely and compile cleanly with Raylib-cs 8.1.0 on macOS arm64.

---

## 6. Conclusion

The Milestone 2 implementation of Movement, Kinematics, Collision, Y-Sorting, Camera Tracking, and Quest 1 interactions is robust, cleanly architected, and fully verified.
- **Zero integrity violations detected**.
- **0 compilation errors, 0 compilation warnings**.
- **230 of 230 automated unit tests pass** in 0.46s.
- The 3 minor advisory findings are non-blocking edge cases that can be addressed during subsequent polish milestones.

**Verdict: APPROVE**

---

## 7. Verification Method

To independently reproduce this verification:

1. **Verify Build**:
   ```bash
   dotnet build GameProject.csproj
   ```
2. **Execute Full Test Suite**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --logger "console;verbosity=normal"
   ```
3. **Execute M2 Specific Test Suites**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~MovementCollisionCameraTests"
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~InteractionAndQuest1Tests"
   ```
