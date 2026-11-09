using System;
using System.Collections.Generic;
using System.Numerics;

namespace GameProject.Core.Spatial;

/// <summary>
/// 2D collision world managing static obstacles and decoupled axis slide-along-wall resolution.
/// </summary>
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
    /// Smooth slide-along-wall resolution separating X and Y axes independently.
    /// Prevents sticking on walls and curbs during diagonal movement.
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
                velocity.X = 0f;
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
                velocity.Y = 0f;
            }
            else
            {
                pos.Y = targetY;
            }
        }

        // --- 3. CLAMP TO MAP BOUNDS ---
        float minX = MapBounds.MinX + footHalfWidth;
        float maxX = MapBounds.MaxX - footHalfWidth;
        float minY = MapBounds.MinY + footHeight;
        float maxY = MapBounds.MaxY;

        float clampedX = Math.Clamp(pos.X, minX, maxX);
        float clampedY = Math.Clamp(pos.Y, minY, maxY);

        if (clampedX != pos.X) velocity.X = 0f;
        if (clampedY != pos.Y) velocity.Y = 0f;

        return new Vector2(clampedX, clampedY);
    }
}

