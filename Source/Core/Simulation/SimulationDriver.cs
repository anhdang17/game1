using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.State;

namespace GameProject.Core.Simulation;

/// <summary>
/// Headless simulation driver enabling sub-second automated test runs without Raylib window or display server.
/// Assembles domain state machines, event bus, mock ports, and scenario playthrough helpers.
/// </summary>
public class SimulationDriver
{
    private readonly List<IGameEvent> _firedEvents = new();

    public SaveData SaveData { get; set; }

    /// <summary>
    /// Dual naming compatibility: allows tests to access active SaveData via GameState property.
    /// </summary>
    public SaveData GameState
    {
        get => SaveData;
        set => SaveData = value;
    }

    public GameStateManager StateManager { get; private set; }
    public EventBus EventBus { get; private set; }
    public QuestManager QuestManager { get; private set; }
    public LevelProgressionService ProgressionService { get; private set; }
    public SaveLoadService SaveLoadService { get; private set; }

    public MockInputService InputService { get; private set; }
    public RecordingAudioService AudioService { get; private set; }
    public InMemorySaveStorage Storage { get; private set; }

    public float CurrentSimulationTime { get; private set; } = 0.0f;
    public int TotalTicksSimulated { get; private set; } = 0;
    public IReadOnlyList<IGameEvent> FiredEvents => _firedEvents.AsReadOnly();

    public SimulationDriver()
    {
        EventBus = new EventBus();
        Storage = new InMemorySaveStorage();
        AudioService = new RecordingAudioService();
        InputService = new MockInputService();

        SaveData = SaveData.CreateDefault();
        StateManager = new GameStateManager(EventBus, Core.State.GameState.Title);
        QuestManager = new QuestManager(EventBus);
        HanoiQuests.RegisterAll(QuestManager);

        ProgressionService = new LevelProgressionService(SaveData, EventBus);
        SaveLoadService = new SaveLoadService(SaveLoadService.DefaultSavePath, Storage, EventBus);

        HookEventTracking();
    }

    public void Initialize()
    {
        SaveData = SaveData.CreateDefault();
        ProgressionService = new LevelProgressionService(SaveData, EventBus);
        StateManager.ChangeState(Core.State.GameState.Title);
        QuestManager.ResetAllQuests();
        HanoiQuests.RegisterAll(QuestManager);
        Storage.Clear();
        AudioService.Clear();
        InputService.ResetAll();
        _firedEvents.Clear();
        CurrentSimulationTime = 0.0f;
        TotalTicksSimulated = 0;
    }

    private void HookEventTracking()
    {
        EventBus.Subscribe<GameStateChangedEvent>(_firedEvents.Add);
        EventBus.Subscribe<QuestStartedEvent>(_firedEvents.Add);
        EventBus.Subscribe<QuestProgressUpdatedEvent>(_firedEvents.Add);
        EventBus.Subscribe<QuestRequirementsMetEvent>(_firedEvents.Add);
        EventBus.Subscribe<QuestCompletedEvent>(_firedEvents.Add);
        EventBus.Subscribe<ItemCollectedEvent>(_firedEvents.Add);
        EventBus.Subscribe<ItemRemovedEvent>(_firedEvents.Add);
        EventBus.Subscribe<PhoIngredientPlacedEvent>(_firedEvents.Add);
        EventBus.Subscribe<PhoAssemblyAttemptedEvent>(_firedEvents.Add);
        EventBus.Subscribe<PhoAssemblyCompletedEvent>(_firedEvents.Add);
        EventBus.Subscribe<PhoPuzzleResetEvent>(_firedEvents.Add);
        EventBus.Subscribe<StallPushedEvent>(_firedEvents.Add);
        EventBus.Subscribe<StallPuzzleResetEvent>(_firedEvents.Add);
        EventBus.Subscribe<StallPuzzleSolvedEvent>(_firedEvents.Add);
        EventBus.Subscribe<GateUnlockedEvent>(_firedEvents.Add);
        EventBus.Subscribe<ColorRestoredEvent>(_firedEvents.Add);
        EventBus.Subscribe<RegionColorUpdatedEvent>(_firedEvents.Add);
        EventBus.Subscribe<RegionCompletedEvent>(_firedEvents.Add);
        EventBus.Subscribe<RegionUnlockedEvent>(_firedEvents.Add);
        EventBus.Subscribe<DialogueStartedEvent>(_firedEvents.Add);
        EventBus.Subscribe<DialogueLineAdvancedEvent>(_firedEvents.Add);
        EventBus.Subscribe<DialogueChoiceSelectedEvent>(_firedEvents.Add);
        EventBus.Subscribe<DialogueEndedEvent>(_firedEvents.Add);
        EventBus.Subscribe<CutsceneTriggeredEvent>(_firedEvents.Add);
        EventBus.Subscribe<CutsceneEndedEvent>(_firedEvents.Add);
        EventBus.Subscribe<LetterOpenedEvent>(_firedEvents.Add);
        EventBus.Subscribe<HintRequestedEvent>(_firedEvents.Add);
        EventBus.Subscribe<JournalPageAwardedEvent>(_firedEvents.Add);
        EventBus.Subscribe<StampAwardedEvent>(_firedEvents.Add);
        EventBus.Subscribe<FactCardUnlockedEvent>(_firedEvents.Add);
        EventBus.Subscribe<GameSavedEvent>(_firedEvents.Add);
        EventBus.Subscribe<GameLoadedEvent>(_firedEvents.Add);
    }

    /// <summary>Advances the headless game loop by a discrete delta time.</summary>
    public void Step(float deltaTime = 0.016667f)
    {
        CurrentSimulationTime += deltaTime;
        TotalTicksSimulated++;

        InputService.Tick(deltaTime);
        AudioService.Update(deltaTime);
        EventBus.ProcessPending();
    }

    /// <summary>Simulates N frames/ticks headlessly.</summary>
    public void AdvanceTicks(int ticks, float dtPerTick = 0.016667f)
    {
        for (int i = 0; i < ticks; i++)
        {
            Step(dtPerTick);
        }
    }

    /// <summary>Simulates a duration in seconds headlessly.</summary>
    public void AdvanceTime(float durationSeconds, float dtPerTick = 0.016667f)
    {
        int count = (int)Math.Ceiling(durationSeconds / dtPerTick);
        AdvanceTicks(count, dtPerTick);
    }

    /// <summary>Advances time until condition returns true or maxWaitSeconds is reached.</summary>
    public bool AdvanceUntil(Func<bool> condition, float maxWaitSeconds = 5.0f, float dtPerTick = 0.016667f)
    {
        float elapsed = 0.0f;
        while (elapsed < maxWaitSeconds)
        {
            if (condition()) return true;
            Step(dtPerTick);
            elapsed += dtPerTick;
        }
        return condition();
    }

    // --- Scenario & Playthrough Simulation Helpers ---

    public void SimulateCollectItem(string itemId)
    {
        SaveData.Level1State.InventoryItems.Add(itemId);
        EventBus.Publish(new ItemCollectedEvent(itemId));
        QuestManager.RecordObjectiveProgress(HanoiQuests.Quest1Id, itemId, 1);
        AudioService.PlaySfx(SfxType.ItemCollected);
        Step();
    }

    public void SimulateSolvePhoCooking()
    {
        QuestManager.RecordObjectiveProgress(HanoiQuests.Quest2Id, "assemble_pho", 1);
        QuestManager.CompleteQuest(HanoiQuests.Quest2Id);
        ProgressionService.OnQuest2Completed();
        AudioService.PlaySfx(SfxType.PhoCookSuccess);
        AudioService.PlaySfx(SfxType.ColorBloomWave);
        Step();
    }

    public void SimulateSolveStallGrid()
    {
        QuestManager.RecordObjectiveProgress(HanoiQuests.Quest3Id, "clear_long_bien_path", 1);
        QuestManager.CompleteQuest(HanoiQuests.Quest3Id);
        ProgressionService.CompleteLevel1();
        AudioService.PlaySfx(SfxType.GateUnlocked);
        AudioService.PlaySfx(SfxType.ColorBloomWave);
        Step();
    }

    public void SimulateCompleteFullLevel1()
    {
        // 1. Start Quest 1 & gather 5 ingredients
        QuestManager.StartQuest(HanoiQuests.Quest1Id);
        foreach (var item in ItemIds.AllPhoIngredients)
        {
            SimulateCollectItem(item);
        }
        QuestManager.CompleteQuest(HanoiQuests.Quest1Id);

        // 2. Start & solve Pho assembly
        QuestManager.StartQuest(HanoiQuests.Quest2Id);
        SimulateSolvePhoCooking();

        // 3. Start & solve Stall grid
        QuestManager.StartQuest(HanoiQuests.Quest3Id);
        SimulateSolveStallGrid();
    }

    // --- Assertion Helpers ---

    public void AssertQuestState(string questId, QuestState expectedState)
    {
        var quest = QuestManager.GetQuest(questId);
        if (quest == null)
            throw new InvalidOperationException($"Quest '{questId}' is not registered.");
        if (quest.State != expectedState)
            throw new Exception($"Quest '{questId}' state mismatch: expected {expectedState}, got {quest.State}.");
    }

    public void AssertObjectiveCount(string questId, string objectiveId, int expectedCount)
    {
        var quest = QuestManager.GetQuest(questId);
        if (quest == null)
            throw new InvalidOperationException($"Quest '{questId}' is not registered.");
        var obj = quest.GetObjective(objectiveId);
        if (obj == null)
            throw new InvalidOperationException($"Objective '{objectiveId}' not found in quest '{questId}'.");
        if (obj.CurrentCount != expectedCount)
            throw new Exception($"Objective '{objectiveId}' count mismatch: expected {expectedCount}, got {obj.CurrentCount}.");
    }

    public void AssertInventoryContains(string itemId, bool expected = true)
    {
        bool actual = SaveData.Level1State.InventoryItems.Contains(itemId, StringComparer.OrdinalIgnoreCase);
        if (actual != expected)
            throw new Exception($"Inventory presence mismatch for '{itemId}': expected {expected}, got {actual}.");
    }

    public void AssertRegionColor(string regionId, float expectedPercent)
    {
        float actual = SaveData.WorldProgression.GetColorRestoration(regionId);
        if (Math.Abs(actual - expectedPercent) > 0.1f)
            throw new Exception($"Region '{regionId}' color mismatch: expected {expectedPercent}%, got {actual}%.");
    }

    public void AssertGateUnlocked(bool expected = true)
    {
        bool actual = SaveData.Level1State.QuestFlags.Gate_LongBien_Unlocked;
        if (actual != expected)
            throw new Exception($"Long Bien gate unlock mismatch: expected {expected}, got {actual}.");
    }

    public bool HasEventFired<TEvent>(Func<TEvent, bool>? filter = null) where TEvent : class, IGameEvent
    {
        return _firedEvents.OfType<TEvent>().Any(e => filter == null || filter(e));
    }

    public void AssertEventFired<TEvent>(Func<TEvent, bool>? filter = null) where TEvent : class, IGameEvent
    {
        if (!HasEventFired(filter))
            throw new Exception($"Expected event '{typeof(TEvent).Name}' was not fired.");
    }
}

/// <summary>
/// Alias for SimulationDriver.
/// </summary>
public class HeadlessSimulationDriver : SimulationDriver { }

/// <summary>
/// Alias for SimulationDriver.
/// </summary>
public class TestSimulationDriver : SimulationDriver { }
