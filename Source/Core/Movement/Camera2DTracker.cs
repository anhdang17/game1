using System;
using System.Numerics;
using GameProject.Core.Spatial;

namespace GameProject.Core.Movement;

/// <summary>
/// 2D Camera tracking Bé Mây with smooth exponential lerp and viewport clamping within map bounds.
/// </summary>
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

