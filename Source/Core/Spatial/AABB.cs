using System;
using System.Numerics;

namespace GameProject.Core.Spatial;

/// <summary>
/// Axis-Aligned Bounding Box (AABB) for 2D spatial queries and obstacle collisions.
/// </summary>
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

    public static bool operator ==(AABB left, AABB right) => left.Equals(right);
    public static bool operator !=(AABB left, AABB right) => !left.Equals(right);

    public override string ToString() => $"AABB({MinX:F1}, {MinY:F1} to {MaxX:F1}, {MaxY:F1})";
}

