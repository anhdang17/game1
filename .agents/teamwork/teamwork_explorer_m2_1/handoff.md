# Handoff Report: Milestone 2 — Bé Mây Movement, Collision Sliding, Dynamic Y-Sorting & Camera Follow

**Agent**: `teamwork_explorer_m2_1`  
**Milestone**: Milestone 2 (Hanoi Exploration & Quest 1 — Movement, Collision, Y-Sorting & Camera)  
**Date**: 2026-11-08T16:30:00Z  
**Target Audience**: `teamwork_worker_m2`, `teamwork_reviewer_m2`, `teamwork_orchestrator_1`  

---

## 1. Observation

1. **Architecture & Project Layout (`PROJECT.md:4-9, 15-49, 63-95`)**:
   - `PROJECT.md:4`: "Clean Architecture separating Pure C# Core Domain (`Source/Core/`) from Presentation (`Source/Graphics/`, `Source/UI/`, `Source/Audio/`, `Source/Stages/`)."
   - `PROJECT.md:8`: "Headless Testability: All game domain logic, quest progression, dialogue states, and puzzle mechanics run headlessly in `GameProject.Tests` (< 500 ms) via `xUnit` with zero display server dependency."
   - Feature 5: "Bé Mây 2D Movement — Smooth 4-way character movement with inertia and collision (M2)".
   - Feature 6: "Dynamic Y-Sorting — Renders sprites in proper visual depth order based on Y-axis (M2)".
   - Feature 27: "Hanoi Street Collision Bounds — Static collision geometry preventing walking through walls/curbs (M2)".

2. **Existing Input Port Abstraction (`Source/Core/Ports/IInputService.cs:9-62`)**:
   - `IInputService` already specifies:
     - `Vector2 MovementDirection { get; }` (normalized 2D movement vector with opposing inputs cancelling out to zero, line 15).
     - `bool IsInteractJustPressed { get; }` (line 18).
     - `bool IsHintJustPressed { get; }`, `IsResetJustPressed`, `IsAlbumJustPressed`, `IsPauseJustPressed`.
     - `Vector2 PointerPosition { get; }` (line 46).
     - `void Update(float deltaTime);` (line 58).
   - In `Source/Core/Ports/MockInputService.cs:40-60`, helper methods `SimulateMove(float x, float y)` and `SimulateOpposingMove(bool up, bool down, bool left, bool right)` already exist and normalize vectors or cancel opposing keys.

3. **Existing Player Spawn & State (`Source/Core/Models/Level1State.cs:35-36`)**:
   - `PlayerSpawnX = 120.0`, `PlayerSpawnY = 250.0`. Default active quest is `"Quest_Hanoi_01"`.

4. **Directory Structure Status**:
   - Pure C# domain in `Source/Core/` is complete for M1 (192 tests pass in 90ms via `dotnet test Tests/GameProject.Tests.csproj`).
   - `Source/Stages/`, `Source/Graphics/`, `Source/UI/`, and `Source/Audio/` exist as empty folders ready for M2 presentation implementations.
   - `GameProject.csproj` has `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` and references `Raylib-cs 8.1.0`.

---

## 2. Logic Chain

1. **Headless Testability for Spatial & Kinematics Engines**:
   - Following Observation 1 (`PROJECT.md` rule: headless testability with zero display server dependency), all mathematical representations—2D AABB bounding boxes, independent-axis collision slide resolution, kinematics acceleration/friction, camera tracking lerp, and Y-sort ordering—must reside in `Source/Core/` (e.g. `Source/Core/Spatial/` and `Source/Core/Movement/`) using standard `System.Numerics.Vector2`.
   - This ensures 100% of the movement, collision slide, Y-sorting, and camera clamp algorithms can be unit-tested in `GameProject.Tests` under sub-second execution times without launching a Raylib window.

2. **Kinematics & Inertia Model for Bé Mây (Speed 180–220 px/s)**:
   - When input direction $\vec{u} \neq \vec{0}$, the target velocity is $\vec{v}_{target} = \text{Normalize}(\vec{u}) \times V_{max}$, where $V_{max} = 200.0\text{ px/s}$.
   - To achieve snappy yet smooth arcade responsiveness without ice-skating floatiness:
     - Linear acceleration $a_{accel} = 1200.0\text{ px/s}^2$ ($0.16$s to full speed).
     - Deceleration/friction $a_{friction} = 1500.0\text{ px/s}^2$ ($0.13$s to complete stop).
     - At every tick $\Delta t$:
       $$\Delta \vec{v} = \vec{v}_{target} - \vec{v}$$
       $$\vec{v}_{next} = \vec{v} + \text{ClampLength}(\Delta \vec{v}, a \cdot \Delta t)$$
     - Velocity deadzone: If $\|\vec{v}\| < 1.0\text{ px/s}$ and $\vec{v}_{target} == \vec{0}$, snap $\vec{v} = \vec{0}$.
   - Normalizing $\vec{u}$ guarantees diagonal travel speed is strictly capped at $200.0\text{ px/s}$ rather than $200 \times \sqrt{2} \approx 282.8\text{ px/s}$.

3. **Facing Direction & Animation Bobbing**:
   - Facing directions: `Direction.Down = 0`, `Direction.Up = 1`, `Direction.Left = 2`, `Direction.Right = 3`.
   - Update rule: When moving ($\|\vec{v}\| > 1.0\text{ px/s}$):
     - If $|v_x| \ge |v_y|$: `Right` if $v_x > 0$ else `Left`.
     - Else: `Down` if $v_y > 0$ else `Up`.
     - When stopped ($\|\vec{v}\| \le 1.0\text{ px/s}$): retain previous facing direction.
   - Walk cycle animation:
     - Timer accumulator: `WalkTimer = (WalkTimer + dt * 4.0f) % 1.0f`.
     - Vertical bobbing offset: $\Delta y_{bob} = \sin(2\pi \cdot WalkTimer) \times 2.5\text{ px}$.
     - Angular tilt wobble: $\theta_{tilt} = \sin(2\pi \cdot WalkTimer) \times 2.0^\circ$.
     - Footstep SFX triggers: when `WalkTimer` passes $0.25$ and $0.75$, trigger footstep audio.

4. **Footprint Collision Geometry vs Full Visual Sprite**:
   - Top-down 2D perspective requires that Bé Mây's collision box is NOT her full body sprite.
   - If Bé Mây is anchored at her foot center $(X, Y)$, her visual height is $\approx 52\text{ px}$ and width $\approx 36\text{ px}$.
   - Her foot collision box is only `Width = 24`, `Height = 14`, spanning from $X - 12$ to $X + 12$ and $Y - 14$ to $Y$.
   - This allows Bé Mây's head and torso to visually overlap wall edges, market stall canopies, and counters when walking behind them, creating natural 2.5D visual depth.

5. **Smooth Slide-Along-Wall Collision Response (Independent Axis Resolution)**:
   - Simultaneous 2D displacement testing causes players to snag on walls during diagonal movement because a collision in one axis cancels movement on both axes.
   - To guarantee smooth sliding along walls, curbs, and stalls:
     - **Axis X Step**: Test candidate position $(X + v_x \Delta t, Y)$. If overlapping any obstacle AABB, resolve $X$ by clamping to the obstacle boundary ($obstacle.MinX - halfWidth - \epsilon$ if moving right, $obstacle.MaxX + halfWidth + \epsilon$ if moving left) and zero out $v_x$.
     - **Axis Y Step**: Test candidate position $(resolvedX, Y + v_y \Delta t)$. If overlapping any obstacle AABB, resolve $Y$ by clamping to the obstacle boundary ($obstacle.MinY - \epsilon$ if moving down, $obstacle.MaxY + height + \epsilon$ if moving up) and zero out $v_y$.
     - **Map Clamping**: Clamp final position to map boundaries: $[MapBounds.MinX + halfWidth, MapBounds.MaxX - halfWidth]$ and $[MapBounds.MinY + height, MapBounds.MaxY]$.
   - This mathematical decoupling ensures that pressing Down+Right into a vertical wall produces zero movement on X, but 100% full speed on Y, creating a silky slide.

6. **Dynamic Y-Sorting Architecture**:
   - Visual depth in top-down 2D games is determined by the **Foot Y coordinate** (contact point with the ground).
   - Higher Foot Y = closer to the camera = rendered later (in front).
   - Lower Foot Y = farther back = rendered earlier (behind).
   - A tall lamppost (base $Y=300$, height 120) and Bé Mây (feet at $Y=295$) $\implies$ Bé Mây ($295$) is drawn before the lamppost ($300$). Bé Mây is correctly behind the lamppost.
   - When Bé Mây steps down to $Y=305 \implies$ Lamppost ($300$) is drawn before Bé Mây ($305$). Bé Mây is correctly in front of the lamppost.
   - **Deterministic Tie-Breaking**: .NET's `List<T>.Sort()` is not stable. If two entities have identical Foot Y, compare `(FootY, SubLayerPriority, EntityId)` to eliminate visual z-fighting/flickering.

7. **2D Camera Follow & Viewport Clamping**:
   - The camera targets Bé Mây's center: $\vec{C}_{target} = \vec{P}_{player} + (0, -20)$.
   - Frame-rate independent exponential lerp:
     $$\vec{C}_{next} = \vec{C} + (\vec{C}_{target} - \vec{C}) \times (1 - e^{-\lambda \Delta t})$$
     with $\lambda = 8.0\text{ s}^{-1}$ ($12.5\%$ step at 60 FPS, deadzone snap at $< 0.25\text{ px}$).
   - Viewport clamping bounds:
     $$C_x \in [MapMinX + \frac{V_w}{2 \cdot Zoom}, MapMaxX - \frac{V_w}{2 \cdot Zoom}]$$
     $$C_y \in [MapMinY + \frac{V_h}{2 \cdot Zoom}, MapMaxY - \frac{V_h}{2 \cdot Zoom}]$$
     If map dimension is smaller than viewport, center camera: $C = \frac{MapMin + MapMax}{2}$.

---

## 3. Concrete Specifications for Worker

### A. Core Spatial & Collision Math (`Source/Core/Spatial/`)

#### 1. `AABB.cs`
```csharp
namespace GameProject.Core.Spatial;

public readonly struct AABB : IEquatable<AABB>
{
    public float MinX { get; }
    public float MinY { get; }
    public float MaxX { get; }
    public float MaxY { get; }

    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;
    public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinY + MaxY) * 0.5f);

    public AABB(float minX, float minY, float maxX, float maxY)
    {
        MinX = MathF.Min(minX, maxX);
        MinY = MathF.Min(minY, maxY);
        MaxX = MathF.Max(minX, maxX);
        MaxY = MathF.Max(minY, maxY);
    }

    public static AABB FromPositionAndSize(Vector2 pos, Vector2 size)
        => new(pos.X, pos.Y, pos.X + size.X, pos.Y + size.Y);

    public static AABB FromCenterAndExtents(Vector2 center, Vector2 halfExtents)
        => new(center.X - halfExtents.X, center.Y - halfExtents.Y, center.X + halfExtents.X, center.Y + halfExtents.Y);

    public bool Overlaps(in AABB other)
    {
        return MinX < other.MaxX && MaxX > other.MinX &&
               MinY < other.MaxY && MaxY > other.MinY;
    }

    public bool Contains(Vector2 point)
    {
        return point.X >= MinX && point.X <= MaxX &&
               point.Y >= MinY && point.Y <= MaxY;
    }

    public bool Equals(AABB other) =>
        MinX.Equals(other.MinX) && MinY.Equals(other.MinY) &&
        MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);

    public override bool Equals(object? obj) => obj is AABB other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);
}
```

#### 2. `CollisionWorld.cs`
```csharp
namespace GameProject.Core.Spatial;

public sealed class CollisionWorld
{
    private const float Epsilon = 0.01f;
    private readonly List<AABB> _obstacles = new();

    public AABB MapBounds { get; set; } = new(0, 0, 2000, 1200);
    public IReadOnlyList<AABB> Obstacles => _obstacles;

    public void AddObstacle(AABB obstacle) => _obstacles.Add(obstacle);
    public void AddObstacles(IEnumerable<AABB> obstacles) => _obstacles.AddRange(obstacles);
    public void ClearObstacles() => _obstacles.Clear();

    public bool CheckCollision(in AABB box, out AABB hitObstacle)
    {
        for (int i = 0; i < _obstacles.Count; i++)
        {
            if (box.Overlaps(_obstacles[i]))
            {
                hitObstacle = _obstacles[i];
                return true;
            }
        }
        hitObstacle = default;
        return false;
    }

    /// <summary>
    /// Smooth slide-along-wall resolution separating X and Y axes.
    /// </summary>
    public Vector2 MoveAndSlide(
        Vector2 currentFootPos,
        float footHalfWidth,
        float footHeight,
        ref Vector2 velocity,
        float dt)
    {
        Vector2 pos = currentFootPos;

        // --- 1. RESOLVE X AXIS ---
        float deltaX = velocity.X * dt;
        if (MathF.Abs(deltaX) > 0.0001f)
        {
            float targetX = pos.X + deltaX;
            AABB boxX = new(targetX - footHalfWidth, pos.Y - footHeight, targetX + footHalfWidth, pos.Y);

            bool hitX = false;
            float resolvedX = targetX;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (boxX.Overlaps(obs))
                {
                    hitX = true;
                    if (deltaX > 0)
                    {
                        float maxAllowedX = obs.MinX - footHalfWidth - Epsilon;
                        if (maxAllowedX < resolvedX) resolvedX = maxAllowedX;
                    }
                    else
                    {
                        float minAllowedX = obs.MaxX + footHalfWidth + Epsilon;
                        if (minAllowedX > resolvedX) resolvedX = minAllowedX;
                    }
                }
            }

            if (hitX)
            {
                pos.X = resolvedX;
                velocity.X = 0;
            }
            else
            {
                pos.X = targetX;
            }
        }

        // --- 2. RESOLVE Y AXIS ---
        float deltaY = velocity.Y * dt;
        if (MathF.Abs(deltaY) > 0.0001f)
        {
            float targetY = pos.Y + deltaY;
            AABB boxY = new(pos.X - footHalfWidth, targetY - footHeight, pos.X + footHalfWidth, targetY);

            bool hitY = false;
            float resolvedY = targetY;

            for (int i = 0; i < _obstacles.Count; i++)
            {
                var obs = _obstacles[i];
                if (boxY.Overlaps(obs))
                {
                    hitY = true;
                    if (deltaY > 0)
                    {
                        float maxAllowedY = obs.MinY - Epsilon;
                        if (maxAllowedY < resolvedY) resolvedY = maxAllowedY;
                    }
                    else
                    {
                        float minAllowedY = obs.MaxY + footHeight + Epsilon;
                        if (minAllowedY > resolvedY) resolvedY = minAllowedY;
                    }
                }
            }

            if (hitY)
            {
                pos.Y = resolvedY;
                velocity.Y = 0;
            }
            else
            {
                pos.Y = targetY;
            }
        }

        // --- 3. CLAMP TO MAP BOUNDS ---
        float clampedX = Math.Clamp(pos.X, MapBounds.MinX + footHalfWidth, MapBounds.MaxX - footHalfWidth);
        float clampedY = Math.Clamp(pos.Y, MapBounds.MinY + footHeight, MapBounds.MaxY);
        return new Vector2(clampedX, clampedY);
    }
}
```

---

### B. Character Kinematics & Player Controller (`Source/Core/Movement/`)

#### 1. `CharacterKinematics.cs`
```csharp
namespace GameProject.Core.Movement;

public enum FacingDirection
{
    Down = 0,
    Up = 1,
    Left = 2,
    Right = 3
}

public sealed class CharacterKinematics
{
    public Vector2 Position { get; set; } = new(120.0f, 250.0f);
    public Vector2 Velocity { get; set; } = Vector2.Zero;
    public FacingDirection Facing { get; set; } = FacingDirection.Down;

    public float MaxSpeed { get; set; } = 200.0f; // px/s (range 180-220)
    public float Acceleration { get; set; } = 1200.0f; // px/s²
    public float Friction { get; set; } = 1500.0f; // px/s²

    public float FootHalfWidth { get; set; } = 12.0f;
    public float FootHeight { get; set; } = 14.0f;

    public bool IsMoving => Velocity.LengthSquared() > 1.0f;
    public float WalkCycleTimer { get; private set; } = 0.0f;
    public float WalkAnimationFrequency { get; set; } = 4.0f; // cycles per second

    private float _previousWalkCycleTimer = 0.0f;

    public void Update(Vector2 inputDirection, CollisionWorld collisionWorld, float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // 1. Calculate Target Velocity
        Vector2 targetVelocity = Vector2.Zero;
        if (inputDirection != Vector2.Zero)
        {
            Vector2 normInput = Vector2.Normalize(inputDirection);
            targetVelocity = normInput * MaxSpeed;
        }

        // 2. Accelerate or Decelerate
        Vector2 deltaV = targetVelocity - Velocity;
        float rate = (targetVelocity != Vector2.Zero) ? Acceleration : Friction;
        float maxStep = rate * deltaTime;

        if (deltaV.LengthSquared() <= maxStep * maxStep)
        {
            Velocity = targetVelocity;
        }
        else
        {
            Velocity += Vector2.Normalize(deltaV) * maxStep;
        }

        // Velocity snap to zero if very small
        if (targetVelocity == Vector2.Zero && Velocity.LengthSquared() < 0.25f)
        {
            Velocity = Vector2.Zero;
        }

        // 3. Update Facing Direction
        if (Velocity.LengthSquared() > 1.0f)
        {
            if (MathF.Abs(Velocity.X) >= MathF.Abs(Velocity.Y))
            {
                Facing = Velocity.X > 0 ? FacingDirection.Right : FacingDirection.Left;
            }
            else
            {
                Facing = Velocity.Y > 0 ? FacingDirection.Down : FacingDirection.Up;
            }

            _previousWalkCycleTimer = WalkCycleTimer;
            WalkCycleTimer = (WalkCycleTimer + deltaTime * WalkAnimationFrequency) % 1.0f;
        }
        else
        {
            WalkCycleTimer = 0.0f;
            _previousWalkCycleTimer = 0.0f;
        }

        // 4. Move and Slide with Collision
        Vector2 vel = Velocity;
        Position = collisionWorld.MoveAndSlide(Position, FootHalfWidth, FootHeight, ref vel, deltaTime);
        Velocity = vel;
    }

    /// <summary>
    /// Checks if a footstep sound should trigger on this frame (at phases 0.25 and 0.75).
    /// </summary>
    public bool CheckFootstepTrigger()
    {
        if (!IsMoving) return false;
        bool crossedQuarter = _previousWalkCycleTimer < 0.25f && WalkCycleTimer >= 0.25f;
        bool crossedThreeQuarter = _previousWalkCycleTimer < 0.75f && WalkCycleTimer >= 0.75f;
        return crossedQuarter || crossedThreeQuarter;
    }

    /// <summary>
    /// Calculates visual cartoon bobbing offset in pixels.
    /// </summary>
    public float GetVerticalBobbingOffset()
    {
        if (!IsMoving) return 0f;
        return MathF.Sin(WalkCycleTimer * MathF.Tau) * 2.5f;
    }
}
```

---

### C. Dynamic Y-Sorting Queue (`Source/Core/Spatial/YSortQueue.cs`)

```csharp
namespace GameProject.Core.Spatial;

public interface IYSortable
{
    string EntityId { get; }
    float FootY { get; }
    int SubLayerPriority { get; } // -1 = shadow, 0 = default entity, 1 = floating companion
}

public sealed class YSortQueue<T> where T : IYSortable
{
    private readonly List<T> _entities = new();

    public IReadOnlyList<T> Entities => _entities;
    public int Count => _entities.Count;

    public void Clear() => _entities.Clear();

    public void Add(T entity)
    {
        if (entity != null) _entities.Add(entity);
    }

    public void AddRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            if (entity != null) _entities.Add(entity);
        }
    }

    /// <summary>
    /// Sorts entities deterministically by:
    /// 1. FootY (ascending: lower on screen renders later/in front)
    /// 2. SubLayerPriority (ascending: shadows render before body)
    /// 3. EntityId (ordinal string comparison for rock-solid stability)
    /// </summary>
    public void Sort()
    {
        _entities.Sort((a, b) =>
        {
            int cmpY = a.FootY.CompareTo(b.FootY);
            if (cmpY != 0) return cmpY;

            int cmpSub = a.SubLayerPriority.CompareTo(b.SubLayerPriority);
            if (cmpSub != 0) return cmpSub;

            return string.Compare(a.EntityId, b.EntityId, StringComparison.Ordinal);
        });
    }
}
```

---

### D. 2D Camera Follow & Viewport Clamping (`Source/Core/Spatial/Camera2DTracker.cs`)

```csharp
namespace GameProject.Core.Spatial;

public sealed class Camera2DTracker
{
    public Vector2 CurrentPosition { get; set; } = Vector2.Zero;
    public Vector2 TargetPosition { get; set; } = Vector2.Zero;
    public Vector2 ViewportSize { get; set; } = new(1280, 720);
    public AABB MapBounds { get; set; } = new(0, 0, 2000, 1200);

    public float FollowSmoothRate { get; set; } = 8.0f; // 1/s exponential lerp rate
    public float Zoom { get; set; } = 1.0f;

    public void SnapToTarget(Vector2 target)
    {
        TargetPosition = target;
        CurrentPosition = ClampCamera(target);
    }

    public void Update(Vector2 target, float deltaTime)
    {
        TargetPosition = target;
        if (deltaTime <= 0f) return;

        // Exponential decay lerp
        float t = 1.0f - MathF.Exp(-FollowSmoothRate * deltaTime);
        Vector2 smoothed = Vector2.Lerp(CurrentPosition, TargetPosition, t);

        // Snap threshold to prevent endless sub-pixel jitter
        if (Vector2.DistanceSquared(smoothed, TargetPosition) < 0.05f)
        {
            smoothed = TargetPosition;
        }

        CurrentPosition = ClampCamera(smoothed);
    }

    public Vector2 ClampCamera(Vector2 pos)
    {
        float halfW = (ViewportSize.X * 0.5f) / Zoom;
        float halfH = (ViewportSize.Y * 0.5f) / Zoom;

        float minX = MapBounds.MinX + halfW;
        float maxX = MapBounds.MaxX - halfW;
        float minY = MapBounds.MinY + halfH;
        float maxY = MapBounds.MaxY - halfH;

        float cx = (minX > maxX) ? (MapBounds.MinX + MapBounds.MaxX) * 0.5f : Math.Clamp(pos.X, minX, maxX);
        float cy = (minY > maxY) ? (MapBounds.MinY + MapBounds.MaxY) * 0.5f : Math.Clamp(pos.Y, minY, maxY);

        return new Vector2(cx, cy);
    }

    public AABB GetVisibleWorldBounds()
    {
        float halfW = (ViewportSize.X * 0.5f) / Zoom;
        float halfH = (ViewportSize.Y * 0.5f) / Zoom;
        return new AABB(
            CurrentPosition.X - halfW,
            CurrentPosition.Y - halfH,
            CurrentPosition.X + halfW,
            CurrentPosition.Y + halfH
        );
    }
}
```

---

### E. Raylib Input Adapter (`Source/Graphics/RaylibInputService.cs`)

```csharp
using System.Numerics;
using GameProject.Core.Ports;
using Raylib_cs;

namespace GameProject.Graphics;

public sealed class RaylibInputService : IInputService
{
    public Vector2 MovementDirection { get; private set; } = Vector2.Zero;

    public bool IsInteractJustPressed { get; private set; }
    public bool IsHintJustPressed { get; private set; }
    public bool IsResetJustPressed { get; private set; }
    public bool IsAlbumJustPressed { get; private set; }
    public bool IsPauseJustPressed { get; private set; }
    public bool IsConfirmJustPressed { get; private set; }
    public bool IsCancelJustPressed { get; private set; }

    public Vector2 PointerPosition { get; private set; } = Vector2.Zero;
    public bool IsPointerDown { get; private set; }
    public bool IsPointerJustPressed { get; private set; }
    public bool IsPointerJustReleased { get; private set; }

    public void Update(float deltaTime)
    {
        // 1. Movement axes with opposing input cancellation
        float x = 0f;
        float y = 0f;

        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) x -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) x += 1f;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) y -= 1f;
        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) y += 1f;

        Vector2 raw = new(x, y);
        MovementDirection = raw == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(raw);

        // 2. Action keys
        IsInteractJustPressed = Raylib.IsKeyPressed(KeyboardKey.E) || Raylib.IsKeyPressed(KeyboardKey.Space);
        IsHintJustPressed = Raylib.IsKeyPressed(KeyboardKey.H);
        IsResetJustPressed = Raylib.IsKeyPressed(KeyboardKey.R);
        IsAlbumJustPressed = Raylib.IsKeyPressed(KeyboardKey.J);
        IsPauseJustPressed = Raylib.IsKeyPressed(KeyboardKey.Escape);
        IsConfirmJustPressed = Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space);
        IsCancelJustPressed = Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Backspace);

        // 3. Pointer / Mouse
        PointerPosition = Raylib.GetMousePosition();
        IsPointerDown = Raylib.IsMouseButtonDown(MouseButton.Left);
        IsPointerJustPressed = Raylib.IsMouseButtonPressed(MouseButton.Left);
        IsPointerJustReleased = Raylib.IsMouseButtonReleased(MouseButton.Left);
    }
}
```

---

## 4. Caveats

1. **Map Dimensions**: Assumed default Hanoi street map size is $2000 \times 1200\text{ px}$. If the level design worker alters these bounds, `CollisionWorld.MapBounds` and `Camera2DTracker.MapBounds` update automatically via property assignment.
2. **Pushable Obstacles (Quest 3)**: In Milestone 3, pushable carts will be integrated into `CollisionWorld`. In Milestone 2, static market stall obstacles and walls are registered.
3. **Raylib Window Context**: The presentation scene `HanoiExplorationScene` requires an active Raylib window, while all domain classes (`CharacterKinematics`, `CollisionWorld`, `AABB`, `YSortQueue`, `Camera2DTracker`) are 100% headless and testable in `GameProject.Tests`.

---

## 5. Conclusion

1. **Clean Architecture Adherence**: All mathematical foundations (kinematics, AABB collision, independent axis slide response, Y-sorting queue, camera tracking lerp & clamping) are purely decoupled from Raylib and placed in `Source/Core/Spatial/` and `Source/Core/Movement/`.
2. **Smooth Slide-Along-Wall**: Resolving X and Y axis displacements independently prevents player sticking against walls and curbs during diagonal movement.
3. **Dynamic Y-Sorting**: Sorting on the `FootY` ground-contact coordinate with `(FootY, SubLayerPriority, EntityId)` multi-key ordering ensures natural depth layering (walking in front of/behind stalls, NPCs, bicycles) and eliminates z-fighting.
4. **Camera Tracking**: Frame-rate independent exponential lerp with map bounding clamp provides smooth tracking of Bé Mây without ever exposing black space beyond level edges.
5. **Concrete Implementation Ready**: Complete, production-grade C# code signatures and headless test specifications are provided for direct execution by the Worker.

---

## 6. Verification Method

To independently verify the implementation:

1. **Build & Test Command**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --logger "console;verbosity=normal"
   ```
2. **Automated Unit Tests to Add in `Tests/MovementCollisionCameraTests.cs`**:
   - `Kinematics_MoveRight_AcceleratesUpToMaxSpeed`: Verifies velocity reaches $200\text{ px/s}$ with $a = 1200$.
   - `Kinematics_DiagonalMove_NormalizedToMaxSpeed`: Verifies $\|\vec{v}\| \le 200.01\text{ px/s}$ when moving $(1, 1)$.
   - `Kinematics_OpposingInputs_CancelsToZero`: Verifies $(1, 0) + (-1, 0) \implies \vec{v} \to 0$.
   - `Collision_DirectMoveIntoWall_StopsAtBoundary`: Verifies foot position stops at $obs.MinX - halfWidth$.
   - `Collision_SlideAlongWall_DiagonalMaintainsSlidingAxis`: Moving $(1, 1)$ against a vertical wall stops $X$ at wall boundary while $Y$ continues advancing at full speed!
   - `YSort_EntitiesSortedByFootY_Ascending`: Entity with $FootY = 250$ sorts before entity with $FootY = 300$.
   - `YSort_IdenticalFootY_StableDeterministicTieBreaking`: Multiple entities with same FootY maintain deterministic order based on SubLayer and EntityId.
   - `Camera_ClampsToMapBounds_NeverExposesOuterVoid`: Camera clamped to $[minX + halfW, maxX - halfW]$ across all extreme target coordinates.
3. **Invalidation Conditions**:
   - Diagonal movement speed exceeds $200.5\text{ px/s}$.
   - Diagonal movement into a straight wall completely stops the player instead of sliding.
   - Player upper body cannot walk behind market stalls due to whole-sprite collision boxes.
   - Camera viewport renders black outside map boundaries.
   - Y-sorting flickers when two entities stand at the same Y coordinate.
