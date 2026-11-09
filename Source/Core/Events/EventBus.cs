using System;
using System.Collections.Generic;

namespace GameProject.Core.Events;

/// <summary>
/// Contract for decoupled domain event dispatching.
/// </summary>
public interface IEventBus
{
    void Publish<T>(T gameEvent) where T : class, IGameEvent;
    IDisposable Subscribe<T>(Action<T> handler) where T : class, IGameEvent;
    bool Unsubscribe<T>(Action<T> handler) where T : class, IGameEvent;
    void Clear();
    int SubscriberCount<T>() where T : class, IGameEvent;
    void ProcessPending();
}

/// <summary>
/// High-performance, strongly typed, thread-safe EventBus implementation.
/// Implements snapshot iteration to prevent collection modification exceptions upon re-entrant subscriptions.
/// </summary>
public class EventBus : IEventBus
{
    private static readonly Lazy<EventBus> _defaultInstance = new(() => new EventBus());
    public static EventBus Default => _defaultInstance.Value;

    private readonly object _lock = new();
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    /// <summary>
    /// Dispatches a game event to all registered subscribers using snapshot iteration.
    /// </summary>
    public void Publish<T>(T gameEvent) where T : class, IGameEvent
    {
        ArgumentNullException.ThrowIfNull(gameEvent);

        Delegate[] handlersSnapshot;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var list) || list.Count == 0)
            {
                return;
            }
            handlersSnapshot = list.ToArray();
        }

        foreach (var handler in handlersSnapshot)
        {
            if (handler is Action<T> typedHandler)
            {
                try
                {
                    typedHandler(gameEvent);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[EventBus] Error executing handler for {typeof(T).Name}: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Registers a subscriber and returns an IDisposable unsubscription token.
    /// </summary>
    public IDisposable Subscribe<T>(Action<T> handler) where T : class, IGameEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _subscribers[typeof(T)] = list;
            }
            list.Add(handler);
        }

        return new SubscriptionToken(() => Unsubscribe(handler));
    }

    /// <summary>
    /// Explicitly unregisters an event handler.
    /// </summary>
    public bool Unsubscribe<T>(Action<T> handler) where T : class, IGameEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_lock)
        {
            if (_subscribers.TryGetValue(typeof(T), out var list))
            {
                var removed = list.Remove(handler);
                if (list.Count == 0)
                {
                    _subscribers.Remove(typeof(T));
                }
                return removed;
            }
            return false;
        }
    }

    /// <summary>
    /// Removes all subscribers for all event types.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _subscribers.Clear();
        }
    }

    /// <summary>
    /// Returns the number of registered handlers for a specific event type.
    /// </summary>
    public int SubscriberCount<T>() where T : class, IGameEvent
    {
        lock (_lock)
        {
            return _subscribers.TryGetValue(typeof(T), out var list) ? list.Count : 0;
        }
    }

    /// <summary>
    /// Synchronously processes any pending asynchronous events (no-op in synchronous dispatch mode).
    /// </summary>
    public void ProcessPending()
    {
        // Snapshot dispatching is synchronous; hook provided for game loop compatibility.
    }

    private sealed class SubscriptionToken : IDisposable
    {
        private Action? _unsubscribe;

        public SubscriptionToken(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            var action = System.Threading.Interlocked.Exchange(ref _unsubscribe, null);
            action?.Invoke();
        }
    }
}
