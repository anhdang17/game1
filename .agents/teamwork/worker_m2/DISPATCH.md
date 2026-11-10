## 2026-11-10T04:47:23Z
You are Worker M2 on the Du Ký Bé Mây Visual & Gameplay Revamp team.

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY INPUTS:
- ORIGINAL_REQUEST.md path: /Users/mac/Projects/game1/.agents/teamwork/ORIGINAL_REQUEST.md
- Revamp Plan Reference: /Users/mac/.gemini/antigravity/brain/0475d55f-53f9-40a7-ae77-970f01c0dbf1/plan_visual_gameplay_revamp.md
- PROJECT.md path: /Users/mac/Projects/game1/PROJECT.md
- Explorer 2 Report & Implementation Blueprint: /Users/mac/Projects/game1/.agents/teamwork/explorer_survey_2/handoff.md
- Working directory: /Users/mac/Projects/game1
- Your state folder: /Users/mac/Projects/game1/.agents/teamwork/worker_m2

EXCLUSIVE FILE WRITE OWNERSHIP:
You own and may ONLY edit/create:
1. `Source/Graphics/ParticleEmitter.cs` (CREATE NEW)
2. `Source/Core/Movement/CharacterKinematics.cs`
3. `Source/Core/Ports/IInputService.cs`
4. `Source/Graphics/RaylibInputService.cs`
5. `Source/Core/Ports/MockInputService.cs`
Do NOT touch or modify any other files.

TASK OBJECTIVE:
Implement Milestone M2 (Particle System & Kinematics Dash):
1. In `Source/Graphics/ParticleEmitter.cs` (NEW FILE):
   - Implement `public sealed class ParticleEmitter` in namespace `GameProject.Graphics`.
   - Implement `public struct Particle` with Position, Velocity, Color, Size, Lifetime, MaxLifetime, Type, Rotation, RotationSpeed.
   - Implement methods:
     - `public void EmitCloudPuff(Vector2 pos, int count = 3)`
     - `public void EmitSteam(Vector2 potPos, float dt)`
     - `public void EmitSparkles(Vector2 pos, Color color, int count = 5)`
     - `public void EmitConfetti(Vector2 center, int count = 30)`
     - `public void Update(float dt)`
     - `public void Draw()` (MANDATORY: guard with `if (!Raylib.IsWindowReady()) return;` for headless test safety!)
     - `public void UpdateAndDraw(float dt)`
     - `public void Clear()`
     - `public int ParticleCount => ...`
2. In `Source/Core/Movement/CharacterKinematics.cs`:
   - Add Dash properties: `IsDashing => DashTimer > 0f;`, `DashTimer`, `DashCooldown`, `DashDuration = 0.2f`, `DashSpeed = 420f`, `DashCooldownDuration = 0.4f`, `DashDirection`.
   - Implement `public bool TryDash(Vector2 inputDirection)`: if `DashCooldown <= 0f && !IsDashing`, activate dash lock.
   - In `Update(Vector2 inputDirection, CollisionWorld collisionWorld, float deltaTime)`: manage `DashCooldown` and `DashTimer`. When dashing, move at `DashDirection * DashSpeed`. When not dashing, run existing acceleration/friction logic unmodified.
   - CRITICAL INVARIANT: Keep `GetVerticalBobbingOffset() == 0f` when `!IsMoving` (required by `Tests/MovementCollisionCameraTests.cs:113`).
   - Retain default constructor values `MaxSpeed = 200f`, `Acceleration = 1200f`, `Friction = 1500f`.
3. In Input Services:
   - In `Source/Core/Ports/IInputService.cs`: add `bool IsDashJustPressed => false;` and `bool IsDashPressed() => IsDashJustPressed;`.
   - In `Source/Graphics/RaylibInputService.cs`: bind `IsDashJustPressed` to `LeftShift`, `RightShift`, and `Space` (when `!IsInteractJustPressed`).
   - In `Source/Core/Ports/MockInputService.cs`: add `public bool IsDashJustPressed { get; set; }`, `public bool IsDashPressed() => IsDashJustPressed;`, `public void PulseDash() => IsDashJustPressed = true;`.

VERIFICATION:
- Run `dotnet test Tests/GameProject.Tests.csproj -c Release` and ensure all 567 unit tests pass with 0 failures.

OUTPUT REQUIREMENTS:
- Write your completion report to `/Users/mac/Projects/game1/.agents/teamwork/worker_m2/handoff.md`.
- Include exact build/test commands executed, test results, files created/modified, and verification evidence.
- Send a completion message to the orchestrator when finished.
