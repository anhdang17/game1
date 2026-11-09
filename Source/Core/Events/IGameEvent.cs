using System;

namespace GameProject.Core.Events;

/// <summary>
/// Marker interface for all domain events dispatched across the game engine.
/// </summary>
public interface IGameEvent
{
    DateTime TimestampUtc { get; }
}

/// <summary>
/// Base record providing automatic UTC timestamping for all game events.
/// </summary>
public abstract record GameEventBase : IGameEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
