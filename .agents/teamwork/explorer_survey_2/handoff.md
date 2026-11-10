# Handoff Report: Environment Art, Particle System, and Movement Kinematics Architecture

## 1. Observation

Direct code inspection and grep traces revealed the following facts across the codebase:

### 1.1 Environment Rendering in `Source/Stages/HanoiExplorationScene.cs`
- **Lines 708-715 (Checkerboard Grid Lines)**:
  ```csharp
  // Ground stone cobblestones
  Raylib.ClearBackground(HanoiPalette.StoneStreet);

  // Grid lines for cobblestone pavement
  for (int x = 0; x < 1600; x += 64)
  {
      Raylib.DrawLine(x, 110, x, 850, HanoiPalette.StoneMortar);
  }
  for (int y = 110; y < 850; y += 48)
  {
      Raylib.DrawLine(0, y, 1600, y, HanoiPalette.StoneMortar);
  }
  ```
  These lines draw Cartesian graph paper lines across the playable area (X: 0..1600, Y: 110..850).
- **Lines 717-740 (Colonial Facade & Shutters)**:
  Renders a flat rectangle `facade` at `new Rectangle(0, 0, 1600, 110)` in `HanoiPalette.WallYellow`, a terracotta roof overhang at Y: 0..36, basic green shutters at intervals of 180px, and a bottom curb at Y: 850..900.
- **Lines 742-754 (Long Bien Bridge Trusses)**:
  Renders only 2 crude triangular steel truss segments between X=1350 and X=1600.
- **Lines 504-510 (Bà An & Stall Rendering)**:
  ```csharp
  // Bà An NPC & Stall
  yQueue.Add(new RenderableEntity
  {
      EntityId = BaAn.Id,
      FootY = BaAn.Position.Y,
      SubLayerPriority = 0,
      DrawAction = _ => VectorRenderer.DrawMarketStall(BaAn.Position, "Quán Phở Bà An", HanoiPalette.TerracottaRoof, HanoiPalette.WallYellow)
  });
  ```
  `VectorRenderer.DrawMarketStall` (lines 241-268 of `Source/Graphics/VectorRenderer.cs`) draws an empty wooden table with striped awning. It does **not** draw Bà An as a character, does **not** draw a broth pot, and has **no** steam effects.

### 1.2 Particle System Absence
- A filesystem search for `ParticleEmitter.cs` returned zero matches:
  `find Source -name "*Particle*"` returned exit code 0 with empty output.
  `grep -rn "Particle" Source/ Tests/` returned exit code 1 with no occurrences.
  No particle system exists currently in the repository.

### 1.3 Kinematics in `Source/Core/Movement/CharacterKinematics.cs`
- Lines 21-39:
  ```csharp
  public sealed class CharacterKinematics
  {
      public Vector2 Position { get; set; } = new(120.0f, 250.0f);
      public Vector2 Velocity { get; set; } = Vector2.Zero;
      public FacingDirection Facing { get; set; } = FacingDirection.Down;

      public float MaxSpeed { get; set; } = 200.0f; // px/s
      public float Acceleration { get; set; } = 1200.0f; // px/s²
      public float Friction { get; set; } = 1500.0f; // px/s²

      public float FootHalfWidth { get; set; } = 12.0f;
      public float FootHeight { get; set; } = 14.0f;

      public bool IsMoving => Velocity.LengthSquared() > 1.0f;
      public float WalkCycleTimer { get; private set; } = 0.0f;
      public float WalkAnimationFrequency { get; set; } = 4.0f; // cycles per second

      private float _previousWalkCycleTimer = 0.0f;
  ```
- Lines 117-121:
  ```csharp
  public float GetVerticalBobbingOffset()
  {
      if (!IsMoving) return 0f;
      return MathF.Sin(WalkCycleTimer * MathF.Tau) * 2.5f;
  }
  ```
- `CharacterKinematics` currently has no Dash mechanics (`DashTimer`, `DashCooldown`, `TryDash`), no cloud glide fluid drag, and returns strictly `0f` when `!IsMoving`.

### 1.4 Test Suite Invariants & Reflection Dependencies
- `Tests/MovementCollisionCameraTests.cs:113` asserts:
  `Assert.Equal(0f, kin.GetVerticalBobbingOffset());` when stationary. Changing `GetVerticalBobbingOffset()` to return non-zero when idle will break this unit test.
- `Tests/MovementCollisionCameraTests.cs:62-81` asserts:
  Velocity cancels strictly to zero when `kin.Update(Vector2.Zero, world, 0.01f)` is called for 20 frames with `Friction = 1500f`.
- `Tests/M2PhysicsCollisionStressTests.cs:43, 61, 86` asserts:
  `Assert.True(kin.Velocity.Length() <= 200.01f)` under all normal input and extreme dt without dash.
- `Tests/Milestone3Iteration2ProgressionEmpiricalTests.cs:246, 525`, `Tests/ChallengerM3ProgressionStressTests.cs:576` use reflection:
  `typeof(HanoiExplorationScene).GetField("_stallPuzzleModal", BindingFlags.NonPublic | BindingFlags.Instance)`
  Private field names `_stallPuzzleModal`, `_phoAssemblyModal`, `_albumViewerModal` must remain intact.
- `Tests/ChallengerM5PackagingAndPerformanceTests.cs:58-64` exercises `HanoiExplorationScene` headless where `Raylib.IsWindowReady() == false`.

---

## 2. Logic Chain

1. **Environment Replacement (Observation 1.1)**:
   - Lines 708-715 of `HanoiExplorationScene.cs` draw a Cartesian grid.
   - Removing lines 708-715 removes the grid.
   - To replace the grid with organic cobblestone pavement, weathered yellow walls, green shutters with flower boxes, warm glowing lanterns, puddles, fallen petals, and Long Bien bridge silhouette:
     - All rendering must be done procedurally via Raylib 2D vector primitives (`Raylib.DrawRectangleRounded`, `Raylib.DrawCircleV`, `Raylib.DrawEllipse`, `Raylib.DrawLineEx`, `Raylib.DrawTriangle`).
     - Rendering occurs in world-space coordinates within `Raylib.BeginMode2D(rayCamera)`.
     - Static features (cobblestones, puddles, fallen petals) must use deterministic positions or pre-computed layout arrays to avoid per-frame random jitter.
     - Red lanterns need multi-tier concentric radial alpha halos (`Warm Glow`) and subtle pendular sway (`MathF.Sin(totalTime * 2.5f)`).

2. **Bà An & Broth Pot (Observation 1.1)**:
   - Bà An's stall currently only invokes `VectorRenderer.DrawMarketStall`.
   - Adding a dedicated `DrawBaAnPhoStall` or expanding `VectorRenderer`:
     - Wooden counter with Bat Trang bowls, bamboo chopstick holder, lime/chili condiments.
     - Gleaming copper/brass broth pot with glowing embers underneath.
     - Bà An NPC character with silver hair bun, black velvet headband (`khăn vấn`), and ladle.
     - Steaming broth pot connected to `ParticleEmitter.EmitSteam`.

3. **Particle System Architecture (Observation 1.2)**:
   - Because `Source/Graphics/ParticleEmitter.cs` does not exist, creating it requires implementing the specification from `plan_visual_gameplay_revamp.md`:
     - Struct `Particle`: Position, Velocity, Color, Size, Lifetime, MaxLifetime.
     - Methods: `EmitCloudPuff(Vector2 pos, int count = 3)`, `EmitSteam(Vector2 potPos, float dt)`, `EmitSparkles(Vector2 pos, Color color, int count = 5)`, `EmitConfetti(Vector2 center, int count = 30)`, `UpdateAndDraw(float dt)`, plus `Update(float dt)`, `Draw()`, `Clear()`, `ParticleCount`.
   - Headless safety: All Raylib drawing calls in `ParticleEmitter.Draw()` must guard with `if (!Raylib.IsWindowReady()) return;` so headless unit tests do not crash.
   - Integration points:
     - `HanoiExplorationScene`: cloud puffs on dash/move, pho steam at Bà An's pot, sparkles at Bút Chì's tip, confetti on gate unlocked.
     - `PhoAssemblyScene`: steam over bowl, sparkles on ingredient drop, confetti on recipe solved.
     - `StallPuzzleScene`: cloud puff on stall push, confetti on puzzle solved.

4. **Movement Kinematics & Gameplay Feel (Observations 1.3 & 1.4)**:
   - To satisfy the Cloud Dash requirement without breaking `M2PhysicsCollisionStressTests`:
     - Add `IsDashing => DashTimer > 0f;`, `DashTimer`, `DashCooldown`, `DashDuration = 0.2f`, `DashSpeed = 420f`, `DashCooldownDuration = 0.4f`.
     - Add `public bool TryDash(Vector2 inputDirection)` to `CharacterKinematics`. When triggered, it locks velocity to `DashDirection * DashSpeed` for `DashDuration`.
     - Keep the original signature `public void Update(Vector2 inputDirection, CollisionWorld collisionWorld, float deltaTime)` intact. Existing callers pass no dash parameters and operate under normal `MaxSpeed = 200f` capping.
     - For Dash activation: In `IInputService`, add `bool IsDashJustPressed => false;`. In `RaylibInputService`, bind to `LeftShift`, `RightShift`, and `Space` (when no interactable prompt is focused). In `MockInputService`, add `public bool IsDashJustPressed { get; set; }`.
   - To satisfy Sinusoidal Hovering:
     - In `CharacterKinematics`, keep `GetVerticalBobbingOffset() == 0f` when `!IsMoving` to preserve `MovementCollisionCameraTests.cs:113`.
     - In `VectorRenderer.DrawBeMay`, enhance the visual floating offset:
       `float hover = MathF.Sin(animTime * 2.8f) * 4.5f * scale;` when idle, and scale/fade the ground shadow accordingly.
   - To satisfy Cloud Glide:
     - In `HanoiExplorationScene`, set `Kinematics.Friction = 1200f` (or 1100f) during scene initialization, allowing smooth gliding stops in Hanoi without altering the default 1500f in `CharacterKinematics` constructor that unit tests rely on.

---

## 3. Caveats

- **Caveat 1 (Headless Test Context)**: Raylib is uninitialized (`IsWindowReady() == false`) during `dotnet test`. Any code in `ParticleEmitter`, `VectorRenderer`, or `HanoiExplorationScene.Draw()` must never assume an active OpenGL context or non-null Raylib window.
- **Caveat 2 (Frame Rate Jitter in Vector Drawing)**: Any procedural street details (cobblestones, moss, puddles, fallen petals) must NOT call `new Random()` or unseeded pseudo-random generators inside `Draw()` on every frame. They must be rendered from deterministic calculations or pre-computed coordinates.
- **Caveat 3 (Reflection Invariants)**: Certain automated stress tests inspect private fields in `HanoiExplorationScene` via reflection (`_stallPuzzleModal`). Renaming or moving these private fields will fail existing tests.

---

## 4. Conclusion

1. **Grid Line Removal & Street Art Revamp**:
   - Safely remove lines 708-715 in `HanoiExplorationScene.cs`.
   - Implement deterministic cobblestone pavement (staggered paver slabs with mortar joints), organic moss clusters, 4 rain puddles with sky reflections, fallen lotus petals and red almond leaves.
   - Upgrade colonial yellow walls (`HanoiPalette.WallYellow`) with weathering, green louvered shutters with bougainvillea planters, swaying warm red lanterns with 3-tier ambient glow halos, and Long Bien bridge cantilever steel trusses with warm sunset backlighting.
   - Upgrade Bà An's stall with full NPC character visuals, porcelain bowls, condiment jars, and steaming copper broth pot with glowing coals underneath.

2. **Particle System (`ParticleEmitter.cs`)**:
   - Create `Source/Graphics/ParticleEmitter.cs` in `GameProject.Graphics` namespace.
   - Implement `Particle` struct and methods: `EmitCloudPuff`, `EmitSteam`, `EmitSparkles`, `EmitConfetti`, `Update`, `Draw`, and `UpdateAndDraw`.
   - Integrate into `HanoiExplorationScene`, `PhoAssemblyScene`, and `StallPuzzleScene`.

3. **Movement Kinematics & Dash**:
   - Upgrade `CharacterKinematics.cs` with `TryDash`, `IsDashing`, `DashTimer`, `DashCooldown`.
   - Add `IsDashJustPressed` to `IInputService`, `RaylibInputService` (LeftShift/RightShift/Space), and `MockInputService`.
   - Hook dash activation in `HanoiExplorationScene.Update` to emit cloud puff bursts and play sfx.
   - Preserve 100% of existing behavior and test contracts (567/567 tests pass).

---

## 5. Implementation Specification for Worker

### Step 1: Create `Source/Graphics/ParticleEmitter.cs`
- Define `Particle` struct:
  ```csharp
  public struct Particle
  {
      public Vector2 Position;
      public Vector2 Velocity;
      public Color Color;
      public float Size;
      public float Lifetime;
      public float MaxLifetime;
      public ParticleType Type;
      public float Rotation;
      public float RotationSpeed;
  }
  ```
- Implement:
  - `EmitCloudPuff(Vector2 pos, int count = 3)`: pastel white/light blue puffs, expanding radius, decelerating, fading alpha.
  - `EmitSteam(Vector2 potPos, float dt)`: continuous upward drifting ivory puffs with gentle horizontal sine drift.
  - `EmitSparkles(Vector2 pos, Color color, int count = 5)`: radial bursts with 4-point sparkle cross.
  - `EmitConfetti(Vector2 center, int count = 30)`: 7 vibrant colors, parabolic trajectory with gravity and rotational flutter.
  - `Update(float dt)`: advances lifetimes, updates physics per particle type, removes dead particles.
  - `Draw()`: guards `if (!Raylib.IsWindowReady()) return;`, draws circle/rectangle primitives.
  - `UpdateAndDraw(float dt)`: calls `Update(dt)` and `Draw()`.
  - `Clear()` and `ParticleCount`.

### Step 2: Upgrade `Source/Core/Movement/CharacterKinematics.cs`
- Add Dash properties:
  ```csharp
  public bool IsDashing => DashTimer > 0f;
  public float DashTimer { get; private set; } = 0f;
  public float DashCooldown { get; private set; } = 0f;
  public float DashDuration { get; set; } = 0.2f;
  public float DashSpeed { get; set; } = 420.0f;
  public float DashCooldownDuration { get; set; } = 0.4f;
  public Vector2 DashDirection { get; private set; } = Vector2.Zero;
  ```
- Add `TryDash(Vector2 inputDirection)`:
  - Check `DashCooldown <= 0f && !IsDashing`.
  - Calculate direction (from input or fallback to `Facing`).
  - Set `DashTimer = DashDuration`, `DashCooldown = DashCooldownDuration`, `Velocity = DashDirection * DashSpeed`.
- In `Update(Vector2 inputDirection, CollisionWorld collisionWorld, float deltaTime)`:
  - Decrement `DashCooldown -= deltaTime`.
  - If `IsDashing`: decrement `DashTimer -= deltaTime`, retain dash speed/direction, slide along walls. If timer expires, drop speed to `DashDirection * MaxSpeed`.
  - If not dashing: run existing acceleration/friction logic unmodified.
  - Leave `GetVerticalBobbingOffset()` unchanged (`0f` when `!IsMoving`).

### Step 3: Upgrade Input Ports for Dash
- In `Source/Core/Ports/IInputService.cs`:
  ```csharp
  bool IsDashJustPressed => false;
  bool IsDashPressed() => IsDashJustPressed;
  ```
- In `Source/Graphics/RaylibInputService.cs`:
  ```csharp
  public bool IsDashJustPressed { get; private set; }
  ...
  IsDashJustPressed = Raylib.IsKeyPressed(KeyboardKey.LeftShift) || 
                      Raylib.IsKeyPressed(KeyboardKey.RightShift) || 
                      (Raylib.IsKeyPressed(KeyboardKey.Space) && !IsInteractJustPressed);
  ```
- In `Source/Core/Ports/MockInputService.cs`:
  ```csharp
  public bool IsDashJustPressed { get; set; }
  public bool IsDashPressed() => IsDashJustPressed;
  public void PulseDash() => IsDashJustPressed = true;
  ```

### Step 4: Upgrade `Source/Graphics/VectorRenderer.cs`
- In `DrawBeMay`:
  - Enhance idle hovering: `float hover = MathF.Sin(animTime * 2.8f) * 4.5f * scale;`
  - Scale shadow inversely with altitude: `float shadowScale = 1.0f - (hover / 35f);`
- Add `DrawBaAnPhoStall(Vector2 pos, string stallName, float animTime)`:
  - Stacks of white Bat Trang bowls with blue rings.
  - Bamboo container with chopsticks, lime wedges, chili jar.
  - Large brass broth pot with rounded handles and lid tilted open, glowing orange embers beneath.
  - Bà An NPC: silver hair in chignon, black velvet headband (`khăn vấn`), kind smiling face, holding soup ladle.

### Step 5: Upgrade `Source/Stages/HanoiExplorationScene.cs`
- Remove lines 708-715 (Cartesian grid lines).
- In `DrawStreetEnvironment()`:
  - Render running bond cobblestones across street area (X: 0..1600, Y: 110..850).
  - Render moss patches along base of building walls and curb.
  - Render 4 rain puddles with sky reflections and subtle ripples.
  - Render scattered lotus petals and red almond leaves.
  - Render weathered yellow colonial walls with molded cornices.
  - Render green shutters with overflowing bougainvillea planters.
  - Render warm red lanterns swaying gently under eaves with 3-tier ambient glow halos.
  - Render Long Bien bridge cantilever steel trusses with sunset backlighting.
- In `Draw()`:
  - In `yQueue`, draw Bà An using `VectorRenderer.DrawBaAnPhoStall`.
  - Draw `Particles.Draw()` inside world camera mode.
- In `Update(deltaTime)`:
  - Check dash input: if triggered, invoke `Kinematics.TryDash(...)` and `Particles.EmitCloudPuff(..., count: 5)`.
  - When moving, emit trailing cloud puffs.
  - Emit steam at Bà An's broth pot: `Particles.EmitSteam(BaAn.Position + new Vector2(16f, -24f), deltaTime)`.
  - Emit sparkles at Bút Chì's tip periodically.
  - Update particles: `Particles.Update(deltaTime)`.

### Step 6: Hook Particles in Minigames
- In `Source/Stages/PhoAssemblyScene.cs`:
  - Instantiate `ParticleEmitter _particles = new();`.
  - Emit steam over bowl during assembly.
  - Emit sparkles when ingredients drop into bowl.
  - Emit confetti when recipe is completed (`_solved`).
  - Call `_particles.Update(dt)` and `_particles.Draw()`.
- In `Source/Stages/StallPuzzleScene.cs`:
  - Instantiate `ParticleEmitter _particles = new();`.
  - Emit cloud puff when stalls are pushed.
  - Emit confetti when puzzle is solved.

---

## 6. Verification Method

To independently verify the implementation:

1. **Automated Non-Regression Suite**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj -c Release
   ```
   **Pass Condition**: 567/567 passed, 0 failed, 0 warnings.
   Validates that:
   - All kinematics tests (`Kinematics_MoveRight_AcceleratesUpToMaxSpeed`, `Kinematics_OpposingInputs_CancelsToZeroWithFriction`, `Kinematics_FacingDirection_TracksDominantVelocityAxis`, `Kinematics_WalkCycle_BobbingAndFootstepTriggers`) pass without modification.
   - All stress tests (`M2PhysicsCollisionStressTests`, `Challenger1HysteresisStressTests`, `ChallengerM5PackagingAndPerformanceTests`) pass.
   - All reflection-based modal checks in `HanoiExplorationScene` succeed.

2. **Visual & Gameplay Manual Inspection**:
   ```bash
   dotnet run
   ```
   **Inspect**:
   - Ground: Ensure lines 708-715 (Cartesian grid lines) are completely gone; replaced by organic cobblestones, moss, puddles with sky reflections, and fallen leaves/petals.
   - Buildings & Props: Verify yellow colonial walls, green shutters with flower boxes, and red lanterns emitting warm radial halos.
   - Bà An's Pho Stall: Verify Bà An is present with her silver hair bun and ladle, beside a gleaming broth pot with continuous steam rising into the air.
   - Bé Mây Controls: Press `Space` or `Shift` to trigger Cloud Dash; verify high-speed dash burst with trailing cloud puff particles and smooth deceleration.
   - Minigames: Verify pho bowl steam, ingredient drop sparkles, and victory confetti.

3. **Invalidation Conditions**:
   - Any modification that causes `GetVerticalBobbingOffset()` to return non-zero when `!IsMoving` invalidates test compliance.
   - Any renaming or deletion of reflection fields (`_stallPuzzleModal`, `_phoAssemblyModal`) invalidates test compliance.
   - Unseeded random number calls inside `Draw()` causing screen flicker invalidates visual acceptance criteria.
