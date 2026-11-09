using System;
using System.Collections.Generic;

namespace GameProject.Core.Movement;

/// <summary>
/// Interface for entities participating in dynamic Y-sorting for top-down depth rendering.
/// </summary>
public interface IYSortable
{
    string EntityId { get; }
    float FootY { get; }
    int SubLayerPriority { get; } // -1 = shadow, 0 = default entity, 1 = floating companion
}

/// <summary>
/// Deterministic Y-sorting queue eliminating z-fighting via multi-key ordering:
/// 1. FootY (ascending: entities higher on screen draw first / behind)
/// 2. SubLayerPriority (ascending: shadows draw before body)
/// 3. EntityId (ordinal string comparison for rock-solid stability)
/// </summary>
public class YSortQueue<T> where T : IYSortable
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

/// <summary>
/// Non-generic Y-sort queue for mixed IYSortable entities.
/// </summary>
public sealed class YSortQueue : YSortQueue<IYSortable>
{
}

