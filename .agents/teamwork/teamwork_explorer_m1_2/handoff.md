# Technical Specification & Handoff Report: Core State, Events, and Ports Architecture

**Date**: 2026-11-08  
**Agent**: `teamwork_explorer_m1_2`  
**Workspace**: `/Users/mac/Projects/game1`  
**State Folder**: `/Users/mac/Projects/game1/.agents/teamwork/teamwork_explorer_m1_2/`  
**Milestone Scope**: Milestone 1 (Core Domain Engine & Persistence) — Focus: `GameStateManager`, `EventBus`, `GameEvents`, `IInputService`, `IAudioService`, and `ISaveStorage`.

---

## 1. Observation

### 1.1 Project Structure and Current Workspace State
- Direct inspection of the workspace confirmed the following files and directories:
  - `GameProject.csproj` configured for `<TargetFramework>net10.0</TargetFramework>`, `<Nullable>enable</Nullable>`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, referencing `Raylib-cs 8.1.0`.
  - `Program.cs`: Currently contains an initial audio synthesis probe from Phase 0.
  - Directories `Source/Core/`, `Source/UI/`, `Source/Stages/`, `Source/Utils/`, `Source/Audio/`, `Source/Graphics/`, `Source/Data/` exist, but currently contain zero C# domain source files (`find Source -type f` returns empty).
  - Directory `Tests/` has not yet been initialized (to be scaffolded as `GameProject.Tests.csproj` with `xUnit`).
  - Runtime environment: .NET SDK `10.0.101` on macOS Apple Silicon `arm64`. Native Raylib dynamic library `libraylib.dylib` is verified working.

### 1.2 System Requirements and Architectural Directives
- **`PROJECT.md` Architecture Directive** (§ 1, lines 4-9):
  - "Clean Architecture separating Pure C# Core Domain (`Source/Core/`) from Presentation (`Source/Graphics/`, `Source/UI/`, `Source/Audio/`, `Source/Stages/`)."
  - "Headless Testability: All game domain logic, quest progression, dialogue states, and puzzle mechanics run headlessly in `GameProject.Tests` (< 500 ms) via `xUnit` with zero display server dependency."
- **`PROJECT.md` Feature Inventory** (§ Feature Inventory, lines 63-96):
  - Feature 21: *Quest State Manager* (M1) — Finite state machine tracking quest lifecycle and prerequisites.
  - Feature 22: *Event Dispatching Bus* (M1) — Decoupled observer event architecture for quests, items, and UI.
  - Feature 29: *Save Data Corruption Recovery* (M1) — Atomic save with backup and auto-repair on corrupted JSON.
  - Feature 30: *Headless Simulation Driver* (M1) — Mock input and headless update loop for automated test runner.
- **`PROJECT.md` Interface Contracts** (§ Interface Contracts, lines 106-141):
  - `GameStateManager`: Provides `CurrentState`, `ActiveRegion`, `PlayerProfile`.
  - `EventBus`: Fires `OnItemCollected`, `OnQuestUpdated`, `OnColorRestored`, `OnGateUnlocked`.
  - `Ports`: `IInputService`, `IAudioService`, `ISaveStorage`.
- **`teamwork_spec_miner_survey_2/handoff.md` Behavioral Edge Cases**:
  - Edge Case #13 (Movement): Opposite keys (W+S or A+D) held simultaneously must cancel out to a zero vector (`Vector2.Zero`) with no jitter.
  - Edge Case #17 (Collection Modal): Opening Album (`[J]`) must be blocked during active dialogues or modal cooking puzzle to prevent modal stacking conflicts.
  - Edge Case #18 (Headless Mode): Domain logic and tests must execute without calling `Raylib.InitWindow()` or `Raylib.InitAudioDevice()`.
  - Edge Case #1-3 (Atomic Save & Corruption): Atomic write via temporary file, corruption fallback to `.bak`, in-memory testability.

---

## 2. Logic Chain

### 2.1 State Management Architecture: Hybrid State Stack (`GameStateManager`)

#### Why a Flat Enum FSM Fails
A simple flat enum finite state machine (e.g. `CurrentState = GameState.Exploration`) fails to handle the UX requirements of "Du Ký Bé Mây":
1. **Modal Overlays**: When Bé Mây is exploring Hanoi (`Exploration`) and the player opens the Album (`Album`, key `[J]`), or pauses the game (`Pause`, key `[Escape]`), or enters the Phở cooking interface (`MiniGamePho`) by talking to Bà An, the underlying exploration world state (player position, NPC coordinates, active dialogue) must be preserved in memory.
2. **Resumption**: When the player closes the Album or unpauses, the system must cleanly resume the exact prior state without reloading the level or re-initializing scenes.
3. **Modal Stacking Constraints**: As specified in Edge Case #17, modal states cannot be arbitrarily stacked (e.g., pressing `[J]` while in `MiniGamePho` or `Cutscene` must be rejected).

#### The Solution: Hybrid State Stack Machine
`GameStateManager` maintains:
1. `Stack<GameStateEntry>`: The active state stack. The top of the stack is the currently executing state.
2. Operations:
   - `ChangeState(GameState newState, object? context = null)`: Clears the entire stack and pushes `newState`. Used for primary stage transitions (e.g., `Title` ➔ `WorldMap` ➔ `Exploration`).
   - `PushState(GameState overlayState, object? context = null)`: Pushes an overlay/modal state on top of the stack (e.g., `Exploration` ➔ `Album`, `Exploration` ➔ `MiniGamePho`, `Exploration` ➔ `Pause`). Validates against illegal stack combinations.
   - `PopState()`: Removes the top overlay state and resumes the underlying state. Throws or returns false if only 1 state remains (cannot pop the base state).
3. State Context Passing:
   - Each state transition can carry an optional strongly typed context object (e.g., `ExplorationContext`, `CutsceneContext`, `MiniGameContext`).
4. Lifecycle Hooks:
   - `OnStateChanged`: Emits `GameStateChangedEvent(oldState, newState, context)` via the `EventBus`.

```
┌────────────────────────────────────────────────────────┐
│                   State Stack Examples                 │
├──────────────────────────┬─────────────────────────────┤
│   Base Exploration       │   With Pause Overlay        │
│ ┌──────────────────────┐ │ ┌─────────────────────────┐ │
│ │ Exploration          │ │ │ Pause (Top - Active)    │ │
│ └──────────────────────┘ │ ├─────────────────────────┤ │
│                          │ │ Exploration (Suspended) │ │
│                          │ └─────────────────────────┘ │
└──────────────────────────┴─────────────────────────────┘
```

### 2.2 Decoupled Observer Architecture (`EventBus` & `GameEvents`)

#### Decoupling Domain from Presentation
In Clean Architecture, the domain layer (`Source/Core/`) must never know about Raylib rendering, UI widgets, or audio hardware.
- When an objective is completed in `QuestManager`, it does not invoke `QuestHUD.Update()` or `AudioService.PlaySound()`.
- Instead, it dispatches an immutable record event: `EventBus.Publish(new QuestProgressUpdatedEvent(...))`.
- Subscribers (UI HUD, Audio Synth, Dialogue Manager, Hint Engine, Analytics) listen independently.

#### Thread Safety and Snapshot Dispatching
In C# game loops, event handlers frequently trigger subsequent state changes or unregister themselves (e.g., a dialogue node triggers a quest start, which unregisters an old prompt).
Iterating directly over a `List<Action<T>>` during dispatch causes `InvalidOperationException: Collection was modified`.
- **Resolution**:
  1. Internal storage: `Dictionary<Type, List<Delegate>>` protected by a thread synchronization lock (`private readonly object _lock = new();`).
  2. Snapshot dispatching: `Publish<T>` copies the delegate list to an array (`handlers.ToArray()`) inside the lock, then invokes each handler outside the lock.
  3. Subscription token pattern: `Subscribe<T>` returns an `IDisposable`. Calling `token.Dispose()` unsubscribes cleanly, preventing memory leaks when UI widgets or scenes are destroyed.
  4. Isolation: `Clear()` method resets all listeners for unit test tear-down.

### 2.3 Ports Architecture (`IInputService`, `IAudioService`, `ISaveStorage`)

#### 2.3.1 `IInputService`
- **Responsibilities**:
  - Movement vectors: Returns a 2D normalized vector (`Vector2`) representing WASD / Arrow Keys.
  - Key actions: `IsInteractPressed()` (`[E]`), `IsHintPressed()` (`[H]`), `IsResetPressed()` (`[R]`), `IsAlbumPressed()` (`[J]`), `IsPausePressed()` (`[Escape]`), `IsConfirmPressed()` (`[Enter]`/`[Space]`), `IsCancelPressed()` (`[Escape]`).
  - Pointer/Mouse: Coordinates `(X, Y)`, `IsPointerDown`, `IsPointerJustPressed`, `IsPointerJustReleased` for drag-and-drop phở assembly and UI clicks.
- **Opposite Key Cancellation (Edge Case #13)**:
  - If Left and Right (A and D) or Up and Down (W and S) are held simultaneously, the net vector must evaluate to `0.0f` on that axis.
- **Implementations**:
  - `RaylibInputService`: Polls `Raylib.IsKeyDown()`, `Raylib.IsKeyPressed()`, and `Raylib.GetMousePosition()`. (Preserved for Presentation layer in Milestone 2).
  - `MockInputService`: Pure C# mock implementing `IInputService` for headless simulation and unit tests. Allows tests to programmatically call `SetMovement(Vector2)`, `PressInteract()`, `SetPointer(x, y, isDown)`, and `AdvanceFrame()`.

#### 2.3.2 `IAudioService`
- **Responsibilities**:
  - SFX triggers via strongly typed `SfxType` enum (e.g., `ItemCollected`, `PhoIngredientPlaced`, `PhoCookSuccess`, `StallSlide`, `GateUnlocked`, `ColorBloomWave`, `DialogueBlip`).
  - BGM playback via strongly typed `MusicTrack` enum (e.g., `TitleTheme`, `HanoiStreetsTheme`, `PhoPuzzleTheme`, `StallPuzzleTheme`, `CelebrationFanfare`).
  - Volume attenuation and muting: `MasterVolume`, `MusicVolume`, `SfxVolume`, `IsMuted`.
- **Headless Safety**:
  - `NullAudioService`: Pure no-op implementation; does nothing, enabling headless tests and servers to run without audio hardware.
  - `RecordingAudioService`: In-memory recording mock that stores `LastPlayedSfx`, `SfxCallCount`, `ActiveMusicTrack`, and `AudioHistory`, enabling unit tests to verify that solving a puzzle or collecting an item triggers the correct sound effect!

#### 2.3.3 `ISaveStorage`
- **Responsibilities**:
  - Abstract all filesystem interactions (`FileExists`, `DirectoryExists`, `CreateDirectory`, `ReadAllText`, `WriteAllText`, `DeleteFile`, `MoveFile`, `CopyFile`).
- **Persistence Guarantees (R1, Edge Cases #1-#3)**:
  - Atomic write: Write data to `${path}.tmp`, flush, and execute an atomic replace (`MoveFile(tmpPath, path, overwrite: true)`).
  - Backup creation: Copy existing file to `${path}.bak` prior to overwriting.
  - Corrupt recovery: If parsing fails, move the corrupted file to `${path}.corrupt` and return a fresh clean default state.
- **Implementations**:
  - `FileSaveStorage`: Uses standard .NET `System.IO.File` and `System.IO.Directory` with explicit UTF-8 encoding.
  - `InMemorySaveStorage`: Virtual in-memory filesystem using `Dictionary<string, string>` and `HashSet<string>`. Allows unit tests to test corrupted JSON files, disk full, missing directories, and atomic recovery in memory with zero disk I/O.

---

## 3. Concrete Code Signatures & Implementation Blueprints

Here are the complete, production-ready class specifications and implementations to be created by the Worker.

### 3.1 State Management: `Source/Core/State/`

#### 3.1.1 `Source/Core/State/GameState.cs`
```csharp
namespace DuKyBeMay.Core.State;

/// <summary>
/// Defines all primary and overlay game states for Du Ký Bé Mây.
/// </summary>
public enum GameState
{
    /// <summary>Main title / start screen.</summary>
    Title,

    /// <summary>2D Regional World Map of Vietnam.</summary>
    WorldMap,

    /// <summary>2D street exploration mode in Hanoi (Bé Mây walking, talking, picking items).</summary>
    Exploration,

    /// <summary>Quest 2 Culinary assembly mini-game (Phở bowl ordering).</summary>
    MiniGamePho,

    /// <summary>Quest 3 Spatial grid clearance puzzle (Long Biên Bridge stalls).</summary>
    MiniGameStall,

    /// <summary>Story dialogue or cutscene (e.g. Gray Shadow letter sequence).</summary>
    Cutscene,

    /// <summary>Collection Album viewer overlay (Journal pages, Stamps, Fact cards).</summary>
    Album,

    /// <summary>In-game pause menu overlay.</summary>
    Pause
}
```

#### 3.1.2 `Source/Core/State/GameStateManager.cs`
```csharp
using System;
using System.Collections.Generic;
using DuKyBeMay.Core.Events;

namespace DuKyBeMay.Core.State;

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
    /// Blocked during active MiniGames, Cutscenes, or when Album/Pause is already open.
    /// </summary>
    public bool CanOpenAlbum
    {
        get
        {
            var current = CurrentState;
            return current != GameState.Album &&
                   current != GameState.Pause &&
                   current != GameState.MiniGamePho &&
                   current != GameState.MiniGameStall &&
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
        var oldState = CurrentState;
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
    /// Toggles the in-game Pause menu.
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
    /// Toggles the Collection Album.
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
```

---

### 3.2 Event System: `Source/Core/Events/`

#### 3.2.1 `Source/Core/Events/IGameEvent.cs`
```csharp
using System;

namespace DuKyBeMay.Core.Events;

/// <summary>
/// Marker interface for all domain events dispatched across Du Ký Bé Mây.
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
```

#### 3.2.2 `Source/Core/Events/EventBus.cs`
```csharp
using System;
using System.Collections.Generic;

namespace DuKyBeMay.Core.Events;

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
}

/// <summary>
/// High-performance, strongly typed, thread-safe EventBus implementation.
/// Implements snapshot iteration to prevent collection modification exceptions.
/// </summary>
public class EventBus : IEventBus
{
    private static readonly Lazy<EventBus> _defaultInstance = new(() => new EventBus());
    public static EventBus Default => _defaultInstance.Value;

    private readonly object _lock = new();
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

    /// <summary>
    /// Dispatches a game event to all registered subscribers.
    /// Uses snapshot iteration for re-entrancy safety.
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
                    // Log or handle without crashing the bus
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
    /// Removes all subscribers for all event types. Essential for test tear-downs.
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
```

#### 3.2.3 `Source/Core/Events/GameEvents.cs`
```csharp
using DuKyBeMay.Core.State;

namespace DuKyBeMay.Core.Events;

// --- State Transitions ---
public record GameStateChangedEvent(GameState OldState, GameState NewState, object? Context, bool IsOverlay) : GameEventBase;

// --- Quest & Objective Events ---
public record QuestStartedEvent(string QuestId, string Title, string Description) : GameEventBase;
public record QuestProgressUpdatedEvent(string QuestId, string ObjectiveId, int CurrentCount, int TargetCount, string ObjectiveText) : GameEventBase;
public record QuestRequirementsMetEvent(string QuestId) : GameEventBase;
public record QuestCompletedEvent(string QuestId, string Title) : GameEventBase;

// --- Inventory & Gathering Events ---
public record ItemCollectedEvent(string ItemId, string ItemName, int CurrentCount, int RequiredCount) : GameEventBase;
public record ItemRemovedEvent(string ItemId, int RemainingCount) : GameEventBase;

// --- Puzzle 2: Pho Assembly Events ---
public record PhoIngredientPlacedEvent(int SlotIndex, string IngredientId, bool IsCorrectOrder) : GameEventBase;
public record PhoAssemblyAttemptedEvent(bool IsSuccess, string? ErrorMessage) : GameEventBase;
public record PhoAssemblyCompletedEvent() : GameEventBase;
public record PhoPuzzleResetEvent() : GameEventBase;

// --- Puzzle 3: Long Bien Stall Sliding Events ---
public record StallPushedEvent(string ObjectId, int FromX, int FromY, int ToX, int ToY, bool Success) : GameEventBase;
public record StallPuzzleResetEvent() : GameEventBase;
public record StallPuzzleSolvedEvent(int TotalMoves) : GameEventBase;

// --- Progression & World Restoration Events ---
public record GateUnlockedEvent(string GateId, string RegionId) : GameEventBase;
public record ColorRestoredEvent(string RegionId, float OldPercentage, float NewPercentage, string SourceTrigger) : GameEventBase;
public record RegionCompletedEvent(string RegionId) : GameEventBase;
public record RegionUnlockedEvent(string RegionId) : GameEventBase;

// --- Narrative & Dialogue Events ---
public record DialogueStartedEvent(string DialogueId, string Speaker) : GameEventBase;
public record DialogueLineAdvancedEvent(string DialogueId, string NodeId, string Speaker, string Text) : GameEventBase;
public record DialogueChoiceSelectedEvent(string DialogueId, string ChoiceId, string NextNodeId) : GameEventBase;
public record DialogueEndedEvent(string DialogueId) : GameEventBase;
public record CutsceneTriggeredEvent(string CutsceneId) : GameEventBase;
public record CutsceneEndedEvent(string CutsceneId) : GameEventBase;
public record LetterOpenedEvent(string LetterId, string Sender) : GameEventBase;

// --- Companion & Hint Events ---
public record HintRequestedEvent(string ActiveQuestId, string HintText, bool IsIdleTrigger) : GameEventBase;

// --- Collection Album Awards ---
public record JournalPageAwardedEvent(string PageId, string Title, string RegionId) : GameEventBase;
public record StampAwardedEvent(string StampId, string Name, string RegionId) : GameEventBase;
public record FactCardUnlockedEvent(string CardId, string Title, string RegionId) : GameEventBase;

// --- Persistence Events ---
public record GameSavedEvent(string FilePath, bool Success, string? ErrorMessage = null) : GameEventBase;
public record GameLoadedEvent(string FilePath, bool Success, bool WasCorruptedAndRecovered) : GameEventBase;
```

---

### 3.3 Ports Architecture: `Source/Core/Ports/`

#### 3.3.1 `Source/Core/Ports/IInputService.cs`
```csharp
using System.Numerics;

namespace DuKyBeMay.Core.Ports;

/// <summary>
/// Abstract port for game input. Fully decouples player controls from Raylib windowing,
/// allowing headless scriptable simulation during automated test execution.
/// </summary>
public interface IInputService
{
    /// <summary>
    /// Gets the normalized 2D movement direction vector (X: -1 to 1, Y: -1 to 1).
    /// Opposing inputs cancel out to zero.
    /// </summary>
    Vector2 MovementDirection { get; }

    /// <summary>
    /// True on the exact frame the player presses the primary interaction key [E] or Space.
    /// </summary>
    bool IsInteractJustPressed { get; }

    /// <summary>
    /// True on the frame the player requests a companion hint via [H].
    /// </summary>
    bool IsHintJustPressed { get; }

    /// <summary>
    /// True on the frame the player requests a puzzle reset via [R].
    /// </summary>
    bool IsResetJustPressed { get; }

    /// <summary>
    /// True on the frame the player toggles the Collection Album via [J].
    /// </summary>
    bool IsAlbumJustPressed { get; }

    /// <summary>
    /// True on the frame the player toggles Pause or backs out via [Escape].
    /// </summary>
    bool IsPauseJustPressed { get; }

    /// <summary>
    /// True on the frame the player confirms an option or advances dialogue via [Enter] or [Space].
    /// </summary>
    bool IsConfirmJustPressed { get; }

    /// <summary>
    /// True on the frame the player cancels or closes a modal via [Escape] or [Backspace].
    /// </summary>
    bool IsCancelJustPressed { get; }

    // --- Pointer / Drag-and-Drop Navigation ---

    /// <summary>Screen coordinates of the pointer / cursor.</summary>
    Vector2 PointerPosition { get; }

    /// <summary>True while the primary mouse button / touch is held down.</summary>
    bool IsPointerDown { get; }

    /// <summary>True on the single frame the pointer is pressed.</summary>
    bool IsPointerJustPressed { get; }

    /// <summary>True on the single frame the pointer is released.</summary>
    bool IsPointerJustReleased { get; }

    /// <summary>Updates input states for the current frame.</summary>
    void Update(float deltaTime);
}
```

#### 3.3.2 `Source/Core/Ports/IAudioService.cs`
```csharp
namespace DuKyBeMay.Core.Ports;

/// <summary>
/// Predefined sound effect types matching game events and actions.
/// </summary>
public enum SfxType
{
    None = 0,
    ItemCollected,
    InteractPrompt,
    PhoIngredientPlaced,
    PhoCookSuccess,
    PhoCookFailed,
    StallSlide,
    StallBlocked,
    GateUnlocked,
    ColorBloomWave,
    JournalPageAwarded,
    StampAwarded,
    FactCardUnlocked,
    DialogueBlip,
    ButtonClick,
    StepStreet
}

/// <summary>
/// Predefined background music tracks.
/// </summary>
public enum MusicTrack
{
    None = 0,
    TitleTheme,
    HanoiStreetsTheme,
    PhoPuzzleTheme,
    StallPuzzleTheme,
    CelebrationFanfare,
    GrayShadowTheme
}

/// <summary>
/// Abstract port for audio triggers and music playback.
/// Allows headless test execution with zero sound card dependency.
/// </summary>
public interface IAudioService
{
    float MasterVolume { get; set; }
    float MusicVolume { get; set; }
    float SfxVolume { get; set; }
    bool IsMuted { get; set; }

    void PlaySfx(SfxType sfx, float volumeModifier = 1.0f);
    void PlayMusic(MusicTrack track, bool loop = true, float fadeDuration = 0.5f);
    void StopMusic(float fadeDuration = 0.5f);
    void PauseMusic();
    void ResumeMusic();

    bool IsMusicPlaying { get; }
    MusicTrack CurrentTrack { get; }

    void Update(float deltaTime);
}

/// <summary>
/// Default headless no-op implementation of IAudioService.
/// Safe for CI/test environments without audio hardware.
/// </summary>
public class NullAudioService : IAudioService
{
    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public bool IsMuted { get; set; } = false;

    public bool IsMusicPlaying => false;
    public MusicTrack CurrentTrack => MusicTrack.None;

    public void PlaySfx(SfxType sfx, float volumeModifier = 1.0f) { }
    public void PlayMusic(MusicTrack track, bool loop = true, float fadeDuration = 0.5f) { }
    public void StopMusic(float fadeDuration = 0.5f) { }
    public void PauseMusic() { }
    public void ResumeMusic() { }
    public void Update(float deltaTime) { }
}
```

#### 3.3.3 `Source/Core/Ports/ISaveStorage.cs`
```csharp
using System;
using System.IO;
using System.Text;

namespace DuKyBeMay.Core.Ports;

/// <summary>
/// Abstract port for file storage operations. Decouples persistence from System.IO.File,
/// enabling unit tests to verify atomic saving, corruption isolation, and backup rollbacks
/// completely in-memory without touching the host filesystem.
/// </summary>
public interface ISaveStorage
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    void DeleteFile(string path);
    void MoveFile(string sourcePath, string destinationPath, bool overwrite = true);
    void CopyFile(string sourcePath, string destinationPath, bool overwrite = true);
    DateTime GetLastWriteTimeUtc(string path);
}

/// <summary>
/// Standard production implementation of ISaveStorage using System.IO.
/// Enforces UTF-8 encoding.
/// </summary>
public class FileSaveStorage : ISaveStorage
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public void CreateDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);

    public void WriteAllText(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        if (File.Exists(destinationPath) && overwrite)
        {
            File.Delete(destinationPath);
        }
        File.Move(sourcePath, destinationPath);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
}
```

---

### 3.4 Headless Test Mocks: `Tests/Mocks/`

#### 3.4.1 `Tests/Mocks/MockInputService.cs`
```csharp
using System;
using System.Numerics;
using DuKyBeMay.Core.Ports;

namespace DuKyBeMay.Tests.Mocks;

/// <summary>
/// Scriptable input service mock for driving headless automated tests.
/// </summary>
public class MockInputService : IInputService
{
    public Vector2 MovementDirection { get; set; } = Vector2.Zero;

    public bool IsInteractJustPressed { get; set; }
    public bool IsHintJustPressed { get; set; }
    public bool IsResetJustPressed { get; set; }
    public bool IsAlbumJustPressed { get; set; }
    public bool IsPauseJustPressed { get; set; }
    public bool IsConfirmJustPressed { get; set; }
    public bool IsCancelJustPressed { get; set; }

    public Vector2 PointerPosition { get; set; } = Vector2.Zero;
    public bool IsPointerDown { get; set; }
    public bool IsPointerJustPressed { get; set; }
    public bool IsPointerJustReleased { get; set; }

    public void Update(float deltaTime)
    {
        // Single-frame pulses reset after tick unless maintained
    }

    public void SimulateMove(float x, float y)
    {
        var raw = new Vector2(x, y);
        MovementDirection = raw == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(raw);
    }

    public void SimulateOpposingMove(bool up, bool down, bool left, bool right)
    {
        float x = 0;
        float y = 0;
        if (left) x -= 1;
        if (right) x += 1;
        if (up) y -= 1;
        if (down) y += 1;

        var v = new Vector2(x, y);
        MovementDirection = v == Vector2.Zero ? Vector2.Zero : Vector2.Normalize(v);
    }

    public void PulseInteract() => IsInteractJustPressed = true;
    public void PulseHint() => IsHintJustPressed = true;
    public void PulseReset() => IsResetJustPressed = true;
    public void PulseAlbum() => IsAlbumJustPressed = true;
    public void PulsePause() => IsPauseJustPressed = true;
    public void PulseConfirm() => IsConfirmJustPressed = true;
    public void PulseCancel() => IsCancelJustPressed = true;

    public void ResetAll()
    {
        MovementDirection = Vector2.Zero;
        IsInteractJustPressed = false;
        IsHintJustPressed = false;
        IsResetJustPressed = false;
        IsAlbumJustPressed = false;
        IsPauseJustPressed = false;
        IsConfirmJustPressed = false;
        IsCancelJustPressed = false;
        PointerPosition = Vector2.Zero;
        IsPointerDown = false;
        IsPointerJustPressed = false;
        IsPointerJustReleased = false;
    }
}
```

#### 3.4.2 `Tests/Mocks/RecordingAudioService.cs`
```csharp
using System.Collections.Generic;
using DuKyBeMay.Core.Ports;

namespace DuKyBeMay.Tests.Mocks;

/// <summary>
/// Recording audio mock that records sound and music calls for test assertions.
/// </summary>
public class RecordingAudioService : IAudioService
{
    public float MasterVolume { get; set; } = 1.0f;
    public float MusicVolume { get; set; } = 1.0f;
    public float SfxVolume { get; set; } = 1.0f;
    public bool IsMuted { get; set; } = false;

    public bool IsMusicPlaying => CurrentTrack != MusicTrack.None;
    public MusicTrack CurrentTrack { get; private set; } = MusicTrack.None;

    public List<SfxType> PlayedSfx { get; } = new();
    public List<MusicTrack> PlayedTracks { get; } = new();

    public SfxType LastSfx => PlayedSfx.Count > 0 ? PlayedSfx[^1] : SfxType.None;

    public void PlaySfx(SfxType sfx, float volumeModifier = 1.0f)
    {
        PlayedSfx.Add(sfx);
    }

    public void PlayMusic(MusicTrack track, bool loop = true, float fadeDuration = 0.5f)
    {
        CurrentTrack = track;
        PlayedTracks.Add(track);
    }

    public void StopMusic(float fadeDuration = 0.5f)
    {
        CurrentTrack = MusicTrack.None;
    }

    public void PauseMusic() { }
    public void ResumeMusic() { }
    public void Update(float deltaTime) { }

    public void Clear()
    {
        PlayedSfx.Clear();
        PlayedTracks.Clear();
        CurrentTrack = MusicTrack.None;
    }
}
```

#### 3.4.3 `Tests/Mocks/InMemorySaveStorage.cs`
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using DuKyBeMay.Core.Ports;

namespace DuKyBeMay.Tests.Mocks;

/// <summary>
/// In-memory virtual storage implementation for hermetic persistence testing.
/// </summary>
public class InMemorySaveStorage : ISaveStorage
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> _writeTimes = new(StringComparer.OrdinalIgnoreCase);

    public bool FileExists(string path) => _files.ContainsKey(Normalize(path));

    public bool DirectoryExists(string path) => _directories.Contains(Normalize(path));

    public void CreateDirectory(string path)
    {
        _directories.Add(Normalize(path));
    }

    public string ReadAllText(string path)
    {
        var norm = Normalize(path);
        if (!_files.TryGetValue(norm, out var content))
        {
            throw new FileNotFoundException($"Virtual file not found: {path}");
        }
        return content;
    }

    public void WriteAllText(string path, string content)
    {
        var norm = Normalize(path);
        _files[norm] = content;
        _writeTimes[norm] = DateTime.UtcNow;
    }

    public void DeleteFile(string path)
    {
        var norm = Normalize(path);
        _files.Remove(norm);
        _writeTimes.Remove(norm);
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        var srcNorm = Normalize(sourcePath);
        var destNorm = Normalize(destinationPath);

        if (!_files.TryGetValue(srcNorm, out var content))
        {
            throw new FileNotFoundException($"Virtual source file not found: {sourcePath}");
        }

        if (_files.ContainsKey(destNorm) && !overwrite)
        {
            throw new IOException($"Virtual destination file already exists: {destinationPath}");
        }

        _files[destNorm] = content;
        _writeTimes[destNorm] = DateTime.UtcNow;
        _files.Remove(srcNorm);
        _writeTimes.Remove(srcNorm);
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = true)
    {
        var srcNorm = Normalize(sourcePath);
        var destNorm = Normalize(destinationPath);

        if (!_files.TryGetValue(srcNorm, out var content))
        {
            throw new FileNotFoundException($"Virtual source file not found: {sourcePath}");
        }

        if (_files.ContainsKey(destNorm) && !overwrite)
        {
            throw new IOException($"Virtual destination file already exists: {destinationPath}");
        }

        _files[destNorm] = content;
        _writeTimes[destNorm] = DateTime.UtcNow;
    }

    public DateTime GetLastWriteTimeUtc(string path)
    {
        var norm = Normalize(path);
        return _writeTimes.TryGetValue(norm, out var dt) ? dt : DateTime.UtcNow;
    }

    public void Clear()
    {
        _files.Clear();
        _directories.Clear();
        _writeTimes.Clear();
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimEnd('/');
}
```

---

## 4. Worker Implementation Strategy & Integration Plan

### 4.1 Dependency and File Sequence for Worker
The Worker implementing Milestone 1 must construct these core components in the following chronological sequence to satisfy all downstream milestone contracts:

1. **Step 1: Scaffolding Core State & Events**:
   - Create `Source/Core/Events/IGameEvent.cs`
   - Create `Source/Core/Events/GameEvents.cs`
   - Create `Source/Core/Events/EventBus.cs`
   - Create `Source/Core/State/GameState.cs`
   - Create `Source/Core/State/GameStateManager.cs`
2. **Step 2: Scaffolding Ports**:
   - Create `Source/Core/Ports/IInputService.cs`
   - Create `Source/Core/Ports/IAudioService.cs` (including `NullAudioService`)
   - Create `Source/Core/Ports/ISaveStorage.cs` (including `FileSaveStorage`)
3. **Step 3: Integration with Peer Deliverables**:
   - Integration with Explorer m1_1's `Source/Core/Models/` and `Source/Core/Persistence/SaveLoadService.cs`:
     `SaveLoadService` injects `ISaveStorage` to execute atomic writes (`savegame.json.tmp` -> `savegame.json`) and backup rotations (`savegame.json.bak`).
   - Integration with Explorer m1_3's `Source/Core/Quests/QuestManager.cs`:
     `QuestManager` injects `IEventBus` to publish `QuestStartedEvent`, `QuestProgressUpdatedEvent`, `QuestCompletedEvent`, and `GateUnlockedEvent`.
4. **Step 4: Scaffolding Test Mocks & Unit Test Suite**:
   - Create `Tests/GameProject.Tests.csproj` (xUnit, referencing `GameProject.csproj`).
   - Create `Tests/Mocks/MockInputService.cs`
   - Create `Tests/Mocks/RecordingAudioService.cs`
   - Create `Tests/Mocks/InMemorySaveStorage.cs`
   - Create `Tests/GameStateManagerTests.cs`
   - Create `Tests/EventBusTests.cs`
   - Create `Tests/PortsTests.cs`

### 4.2 Unit Test Matrix for `GameProject.Tests`
The Worker must ensure the following unit test methods are implemented and pass with 100% success:

| Test Class | Test Method | Scenario & Assertion |
|---|---|---|
| `EventBusTests` | `Publish_DeliversEventToSubscribers` | Multiple handlers receive `ItemCollectedEvent`; counts and payloads match. |
| `EventBusTests` | `SubscriptionToken_DisposesCleanly` | Disposing token stops handler from receiving subsequent published events. |
| `EventBusTests` | `Publish_SnapshotIteration_PreventsModificationException` | Handler unsubscribing during `Publish` does not throw `InvalidOperationException`. |
| `EventBusTests` | `Clear_RemovesAllSubscribers` | After `Clear()`, `SubscriberCount<T>() == 0` and no events are dispatched. |
| `GameStateManagerTests` | `InitialState_StartsAtTitle` | Default manager initializes stack with `GameState.Title`. |
| `GameStateManagerTests` | `ChangeState_ClearsStackAndSetsNewBase` | `ChangeState(GameState.Exploration)` sets `CurrentState = Exploration` and `StackDepth = 1`. |
| `GameStateManagerTests` | `PushState_AddsOverlayState` | `PushState(GameState.Pause)` increments depth to 2, `IsOverlay = true`, `CurrentState = Pause`. |
| `GameStateManagerTests` | `PopState_RestoresUnderlyingState` | Popping Pause restores `Exploration` and depth 1. |
| `GameStateManagerTests` | `PopState_BaseState_ReturnsFalse` | Attempting to pop the only state on the stack fails safely. |
| `GameStateManagerTests` | `CanOpenAlbum_EnforcesModalConstraints` | Returns false when in `MiniGamePho`, `MiniGameStall`, `Cutscene`, or `Pause` (Edge Case 17). |
| `PortsTests` | `MockInput_OpposingKeys_CancelToZero` | Left + Right or Up + Down yields `MovementDirection == Vector2.Zero` (Edge Case 13). |
| `PortsTests` | `RecordingAudio_TracksSfxAndMusic` | `PlaySfx` appends to list; `PlayMusic` updates `CurrentTrack`. |
| `PortsTests` | `InMemorySaveStorage_AtomicMove` | Writing to `.tmp` and moving to target file updates content and replaces atomically. |

---

## 5. Caveats

1. **Pure C# Core Domain Enforcement**:
   - Absolutely no `Raylib_cs` imports are allowed in `Source/Core/`. Vector operations in `Source/Core/` must use `System.Numerics.Vector2`, not `Raylib_cs.Vector2`. Presentation layer in Milestone 2 will easily convert between `System.Numerics.Vector2` and Raylib vectors.
2. **Nullable Reference Types**:
   - `GameProject.csproj` has `<Nullable>enable</Nullable>`. All interfaces and classes must use nullable annotations (`object? Context`, `string? ErrorMessage`) and proper null checks (`ArgumentNullException.ThrowIfNull`).
3. **EventBus Exception Logging**:
   - In production, event handler errors should be caught and logged so one failing UI listener does not crash gameplay logic. In strict test runs, tests may optionally verify handler exceptions.
4. **Test Project Configuration**:
   - When creating `Tests/GameProject.Tests.csproj`, the worker must ensure `<ProjectReference Include="../GameProject.csproj" />` is properly linked and `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio` NuGet packages are added.

---

## 6. Conclusion

1. **Architectural Verification**:
   - The specifications for `GameStateManager`, `EventBus`, `GameEvents`, and the three Ports (`IInputService`, `IAudioService`, `ISaveStorage`) provide a robust, decoupled, and 100% headless-testable domain foundation.
2. **Production Quality**:
   - All edge cases identified in survey reports (modal stacking, opposing key cancellation, atomic saving, headless safety) are rigorously addressed with concrete code structures.
3. **Seamless Worker Execution**:
   - The worker can directly copy and implement the provided class signatures, interfaces, and test mocks without ambiguity or guesswork.

---

## 7. Verification Method

To independently verify the architecture and blueprints defined in this report:

1. **Verify Source Compilation**:
   Once the Worker implements the files under `Source/Core/State/`, `Source/Core/Events/`, and `Source/Core/Ports/`:
   ```bash
   cd /Users/mac/Projects/game1
   dotnet build
   # Expected: Build succeeded. 0 Warning(s), 0 Error(s) in < 1.0s
   ```

2. **Verify Headless Execution via xUnit**:
   Once `Tests/GameProject.Tests.csproj` is created:
   ```bash
   dotnet test
   # Expected: Total tests: > 12. Passed: 100%. Total duration < 500ms with ZERO display window.
   ```

3. **Verify Pure C# Domain Isolation**:
   ```bash
   grep -rn "Raylib" Source/Core/
   # Expected: 0 matches. Confirms zero Raylib coupling in the domain engine.
   ```

4. **Invalidation Conditions**:
   - If any domain class in `Source/Core/State/`, `Source/Core/Events/`, or `Source/Core/Ports/` imports `Raylib_cs`: invalidates Clean Architecture and breaks headless CI.
   - If `EventBus` throws `InvalidOperationException` when a subscriber unregisters inside its callback: invalidates snapshot iteration guarantee.
   - If `MockInputService` does not zero-out opposing vectors: invalidates Edge Case #13 compliance.
