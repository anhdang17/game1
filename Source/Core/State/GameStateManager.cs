using System;
using System.Collections.Generic;
using GameProject.Core.Events;

namespace GameProject.Core.State;

/// <summary>
/// Represents an entry in the game state stack.
/// </summary>
public record GameStateEntry(GameState State, object? Context, DateTime EnteredAt);

/// <summary>
/// Manages the game state lifecycle and stack-based transitions (modals and base scenes).
/// Pure C# domain logic with zero graphics dependencies.
/// </summary>
public class GameStateManager
{
    private readonly Stack<GameStateEntry> _stateStack = new();
    private readonly IEventBus _eventBus;
    private readonly List<GameStateEntry> _stateHistory = new();

    public GameStateManager(IEventBus eventBus, GameState initialBaseState = GameState.Title, object? initialContext = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        ChangeState(initialBaseState, initialContext);
    }

    /// <summary>
    /// Gets the currently active top-level game state.
    /// </summary>
    public GameState CurrentState => _stateStack.Count > 0 ? _stateStack.Peek().State : GameState.Title;

    /// <summary>
    /// Gets the context object associated with the current top state, if any.
    /// </summary>
    public object? CurrentContext => _stateStack.Count > 0 ? _stateStack.Peek().Context : null;

    /// <summary>
    /// Gets the number of states currently on the stack.
    /// </summary>
    public int StackDepth => _stateStack.Count;

    /// <summary>
    /// True if an overlay or modal state is pushed on top of a base state.
    /// </summary>
    public bool IsOverlay => _stateStack.Count > 1;

    /// <summary>
    /// Returns the underlying base state (at the bottom of the stack).
    /// </summary>
    public GameState BaseState
    {
        get
        {
            if (_stateStack.Count == 0) return GameState.Title;
            var array = _stateStack.ToArray();
            return array[^1].State;
        }
    }

    /// <summary>
    /// Checks whether the Album can currently be opened.
    /// Blocked during active MiniGames, Dialogues, Cutscenes, or when Album/Pause is already open.
    /// </summary>
    public bool CanOpenAlbum
    {
        get
        {
            var current = CurrentState;
            return current != GameState.Album &&
                   current != GameState.Pause &&
                   current != GameState.MiniGamePho &&
                   current != GameState.MiniGameStalls &&
                   current != GameState.Dialogue &&
                   current != GameState.Cutscene;
        }
    }

    /// <summary>
    /// Checks whether the game can currently be paused.
    /// </summary>
    public bool CanPause => CurrentState != GameState.Title && CurrentState != GameState.Pause;

    /// <summary>
    /// Completely resets the stack and sets a new base game state.
    /// Used for major transitions: Title -> WorldMap -> Exploration.
    /// </summary>
    public void ChangeState(GameState newState, object? context = null)
    {
        var oldState = _stateStack.Count > 0 ? CurrentState : newState;
        _stateStack.Clear();

        var entry = new GameStateEntry(newState, context, DateTime.UtcNow);
        _stateStack.Push(entry);
        _stateHistory.Add(entry);

        _eventBus.Publish(new GameStateChangedEvent(oldState, newState, context, IsOverlay: false));
    }

    /// <summary>
    /// Pushes a modal or overlay state on top of the current state.
    /// Preserves the underlying state for resumption upon PopState().
    /// </summary>
    public bool PushState(GameState overlayState, object? context = null)
    {
        // Enforce modal stacking rules
        if (overlayState == GameState.Album && !CanOpenAlbum)
        {
            return false;
        }

        if (overlayState == GameState.Pause && !CanPause)
        {
            return false;
        }

        if (CurrentState == overlayState)
        {
            return false; // Prevent pushing identical state on itself
        }

        var oldState = CurrentState;
        var entry = new GameStateEntry(overlayState, context, DateTime.UtcNow);
        _stateStack.Push(entry);
        _stateHistory.Add(entry);

        _eventBus.Publish(new GameStateChangedEvent(oldState, overlayState, context, IsOverlay: true));
        return true;
    }

    /// <summary>
    /// Pops the active overlay state and restores the previous state.
    /// </summary>
    public bool PopState(out GameState restoredState)
    {
        if (_stateStack.Count <= 1)
        {
            // Cannot pop the base state
            restoredState = CurrentState;
            return false;
        }

        var poppedEntry = _stateStack.Pop();
        var currentEntry = _stateStack.Peek();
        restoredState = currentEntry.State;

        _eventBus.Publish(new GameStateChangedEvent(poppedEntry.State, currentEntry.State, currentEntry.Context, IsOverlay: _stateStack.Count > 1));
        return true;
    }

    /// <summary>
    /// Pops the active overlay state without an out parameter.
    /// </summary>
    public bool PopState() => PopState(out _);

    /// <summary>
    /// Toggles the in-game Pause menu overlay.
    /// </summary>
    public bool TogglePause()
    {
        if (CurrentState == GameState.Pause)
        {
            return PopState(out _);
        }
        else if (CanPause)
        {
            return PushState(GameState.Pause);
        }
        return false;
    }

    /// <summary>
    /// Toggles the Collection Album overlay.
    /// </summary>
    public bool ToggleAlbum()
    {
        if (CurrentState == GameState.Album)
        {
            return PopState(out _);
        }
        else if (CanOpenAlbum)
        {
            return PushState(GameState.Album);
        }
        return false;
    }

    /// <summary>
    /// Returns the audit history of state transitions.
    /// </summary>
    public IReadOnlyList<GameStateEntry> GetHistory() => _stateHistory.AsReadOnly();
}
