using System;
using System.Numerics;
using GameProject.Core.Spatial;

namespace GameProject.Core.Movement;

/// <summary>
/// Character facing direction for 4-way top-down sprites.
/// </summary>
public enum FacingDirection
{
    Down = 0,
    Up = 1,
    Left = 2,
    Right = 3
}

/// <summary>
/// Kinematics model for Bé Mây with acceleration, friction, velocity capping, and walk cycle animation.
/// </summary>
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

    public bool IsDashing => DashTimer > 0f;
    public float DashTimer { get; set; } = 0.0f;
    public float DashCooldown { get; set; } = 0.0f;
    public float DashDuration { get; set; } = 0.2f;
    public float DashSpeed { get; set; } = 420.0f;
    public float DashCooldownDuration { get; set; } = 0.4f;
    public Vector2 DashDirection { get; set; } = Vector2.Zero;

    private float _previousWalkCycleTimer = 0.0f;

    /// <summary>
    /// Attempts to trigger a high-speed cloud dash in the given direction (or facing direction if input is zero).
    /// </summary>
    /// <returns>True if dash was successfully initiated; false if on cooldown or already dashing.</returns>
    public bool TryDash(Vector2 inputDirection)
    {
        if (DashCooldown > 0f || IsDashing) return false;

        Vector2 dir;
        if (inputDirection != Vector2.Zero)
        {
            dir = Vector2.Normalize(inputDirection);
        }
        else
        {
            dir = Facing switch
            {
                FacingDirection.Up => new Vector2(0f, -1f),
                FacingDirection.Down => new Vector2(0f, 1f),
                FacingDirection.Left => new Vector2(-1f, 0f),
                FacingDirection.Right => new Vector2(1f, 0f),
                _ => new Vector2(0f, 1f)
            };
        }

        if (dir == Vector2.Zero) dir = new Vector2(0f, 1f);

        DashDirection = dir;
        DashTimer = DashDuration;
        DashCooldown = DashCooldownDuration;
        Velocity = DashDirection * DashSpeed;
        return true;
    }

    public void Update(Vector2 inputDirection, CollisionWorld collisionWorld, float deltaTime)
    {
        if (deltaTime <= 0f) return;

        // Manage Dash Cooldown
        if (DashCooldown > 0f)
        {
            DashCooldown = MathF.Max(0f, DashCooldown - deltaTime);
        }

        if (IsDashing)
        {
            DashTimer -= deltaTime;
            if (DashTimer <= 0f)
            {
                DashTimer = 0f;
                Velocity = DashDirection * MaxSpeed;
            }
            else
            {
                Velocity = DashDirection * DashSpeed;
            }
        }
        else
        {
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

            // Velocity snap to zero if very small and no input
            if (targetVelocity == Vector2.Zero && Velocity.LengthSquared() < 0.25f)
            {
                Velocity = Vector2.Zero;
            }
        }

        // 3. Update Facing Direction and Walk Cycle
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
        float prev = _previousWalkCycleTimer;
        float curr = WalkCycleTimer;

        bool crossedQuarter = (prev < 0.25f && curr >= 0.25f) || (prev > curr && (prev < 0.25f || curr >= 0.25f));
        bool crossedThreeQuarter = (prev < 0.75f && curr >= 0.75f) || (prev > curr && (prev < 0.75f || curr >= 0.75f));

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

