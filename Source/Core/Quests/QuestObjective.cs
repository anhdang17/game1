using System;

namespace GameProject.Core.Quests;

/// <summary>
/// Tracks individual objective criteria and progress within a quest.
/// </summary>
public sealed class QuestObjective
{
    private readonly int _targetCount = 1;

    public string Id { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int TargetCount
    {
        get => _targetCount;
        init => _targetCount = Math.Max(1, value);
    }
    public int CurrentCount { get; private set; } = 0;
    public bool IsOptional { get; init; } = false;

    public bool IsCompleted => CurrentCount >= TargetCount;

    public QuestObjective() { }

    public QuestObjective(string id, string description, int targetCount = 1, bool isOptional = false)
    {
        Id = id;
        Description = description;
        _targetCount = Math.Max(1, targetCount);
        IsOptional = isOptional;
        CurrentCount = 0;
    }

    /// <summary>
    /// Increments objective progress by the specified amount, safely clamped to TargetCount without integer overflow.
    /// Returns true if progress changed; false if already completed or amount is non-positive.
    /// </summary>
    public bool IncrementProgress(int amount = 1)
    {
        if (amount <= 0 || CurrentCount >= TargetCount)
            return false;

        if (amount >= TargetCount - CurrentCount)
            CurrentCount = TargetCount;
        else
            CurrentCount += amount;

        return true;
    }

    /// <summary>
    /// Directly sets progress count, clamped between 0 and TargetCount.
    /// </summary>
    public void SetProgress(int count)
    {
        CurrentCount = Math.Clamp(count, 0, TargetCount);
    }

    /// <summary>
    /// Resets progress back to zero.
    /// </summary>
    public void Reset()
    {
        CurrentCount = 0;
    }
}
