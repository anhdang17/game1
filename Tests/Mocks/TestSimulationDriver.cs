using System;
using System.Collections.Generic;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.WorldMap;
using GameProject.Core.Ports;

namespace GameProject.Tests.Mocks;

public sealed class TestSimulationDriver
{
    public EventBus EventBus { get; }
    public MockInputService InputService { get; }
    public MockSaveStorage Storage { get; }
    public NullAudioService AudioService { get; }

    public SaveData CurrentData { get; set; }
    public SaveLoadService SaveLoadService { get; }
    public QuestManager QuestManager { get; }
    public PhoCookingEngine PhoEngine { get; }
    public SpatialGridEngine GridEngine { get; }
    public DialogueService DialogueService { get; }
    public HintManager HintManager { get; }
    public AlbumService AlbumService { get; }
    public WorldMapService WorldMapService { get; }

    public float CurrentTime { get; private set; } = 0.0f;
    public int TotalTicksSimulated { get; private set; } = 0;

    public List<IGameEvent> ReceivedEvents { get; } = new();

    public TestSimulationDriver(MockSaveStorage? storage = null)
    {
        EventBus = new EventBus();
        InputService = new MockInputService();
        Storage = storage ?? new MockSaveStorage();
        AudioService = new NullAudioService();

        CurrentData = SaveData.CreateDefault();
        SaveLoadService = new SaveLoadService(Storage);
        QuestManager = new QuestManager(EventBus);
        HanoiQuests.RegisterAll(QuestManager);

        PhoEngine = new PhoCookingEngine();
        GridEngine = new SpatialGridEngine();
        DialogueService = new DialogueService();
        HintManager = new HintManager();
        AlbumService = new AlbumService();
        WorldMapService = new WorldMapService();

        // Subscribe to all core events for simulation tracking
        EventBus.Subscribe<ItemCollectedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<QuestStartedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<QuestProgressUpdatedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<QuestCompletedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<PuzzleSolvedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<RegionColorUpdatedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<GateUnlockedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<CutsceneTriggeredEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<JournalPageAwardedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<StampAwardedEvent>(e => ReceivedEvents.Add(e));
    }

    public void Step(float deltaTime = 0.016667f)
    {
        CurrentTime += deltaTime;
        TotalTicksSimulated++;
        InputService.Update(deltaTime);
        AudioService.Update(deltaTime);
    }

    public void AdvanceTicks(int ticks, float dt = 0.016667f)
    {
        for (int i = 0; i < ticks; i++)
        {
            Step(dt);
        }
    }

    public void AdvanceTime(float seconds, float dt = 0.016667f)
    {
        int count = (int)Math.Ceiling(seconds / dt);
        AdvanceTicks(count, dt);
    }

    public bool AdvanceUntil(Func<bool> condition, float maxWaitSeconds = 5.0f, float dt = 0.016667f)
    {
        float elapsed = 0.0f;
        while (elapsed < maxWaitSeconds)
        {
            if (condition()) return true;
            Step(dt);
            elapsed += dt;
        }
        return condition();
    }

    // --- High-level simulation actions ---

    public void SimulateCollectIngredient(string itemId)
    {
        if (!CurrentData.Level1State.InventoryItems.Contains(itemId))
        {
            CurrentData.Level1State.InventoryItems.Add(itemId);
        }

        switch (itemId)
        {
            case ItemIds.BanhPho: CurrentData.Level1State.QuestFlags.Q1_BanhPho_Collected = true; break;
            case ItemIds.ThitBo: CurrentData.Level1State.QuestFlags.Q1_ThitBo_Collected = true; break;
            case ItemIds.NuocDung: CurrentData.Level1State.QuestFlags.Q1_NuocDung_Collected = true; break;
            case ItemIds.HanhLa: CurrentData.Level1State.QuestFlags.Q1_HanhLa_Collected = true; break;
            case ItemIds.ThaoMoc: CurrentData.Level1State.QuestFlags.Q1_ThaoMoc_Collected = true; break;
        }

        EventBus.Publish(new ItemCollectedEvent(itemId));
        QuestManager.RecordObjectiveProgress(QuestIds.Hanoi01, itemId, 1);
        AudioService.PlaySfx(SfxType.ItemCollected);
        Step();
    }

    public void SimulateGatherAllPhoIngredients()
    {
        QuestManager.StartQuest(QuestIds.Hanoi01);
        CurrentData.Level1State.QuestFlags.Q1_Started = true;

        SimulateCollectIngredient(ItemIds.BanhPho);
        SimulateCollectIngredient(ItemIds.ThitBo);
        SimulateCollectIngredient(ItemIds.NuocDung);
        SimulateCollectIngredient(ItemIds.HanhLa);
        SimulateCollectIngredient(ItemIds.ThaoMoc);

        QuestManager.CompleteQuest(QuestIds.Hanoi01);
        CurrentData.Level1State.QuestFlags.Q1_Completed = true;
    }

    public bool SimulateSolvePhoCooking()
    {
        QuestManager.StartQuest(QuestIds.Hanoi02);
        CurrentData.Level1State.QuestFlags.Q2_Started = true;

        PhoEngine.PlaceIngredient(0, ItemIds.BanhPho);
        PhoEngine.PlaceIngredient(1, ItemIds.ThitBo);
        PhoEngine.PlaceIngredient(2, ItemIds.HanhLa);
        PhoEngine.PlaceIngredient(3, ItemIds.NuocDung);
        PhoEngine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool valid = PhoEngine.ValidateAssembly(out _);
        if (valid)
        {
            QuestManager.RecordObjectiveProgress(QuestIds.Hanoi02, "Objective_AssemblePho", 1);
            QuestManager.CompleteQuest(QuestIds.Hanoi02);
            CurrentData.Level1State.QuestFlags.Q2_PhoAssembled = true;
            CurrentData.Level1State.QuestFlags.Q2_Completed = true;

            // First Color Bloom (+33.3%)
            CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 33.3f);
            EventBus.Publish(new RegionColorUpdatedEvent(RegionIds.Hanoi, 33.3f));
            EventBus.Publish(new PuzzleSolvedEvent("Puzzle_PhoCooking"));
            AudioService.PlaySfx(SfxType.PhoCookSuccess);
            AudioService.PlaySfx(SfxType.ColorBloomWave);
        }

        Step();
        return valid;
    }

    public bool SimulateClearStallPath()
    {
        QuestManager.StartQuest(QuestIds.Hanoi03);
        CurrentData.Level1State.QuestFlags.Q3_Started = true;

        // Push Fruit Stall (1x1 at x=2, y=3) LEFT to (1, 3)
        GridEngine.PushObject("Obj_FruitStall", Direction.Left);

        // Push Xe Hoa (1x2 at x=2, y=1) DOWN to y=2 (occupies 2,2 and 2,3)
        GridEngine.PushObject("Obj_FlowerCart", Direction.Down);

        // Path is now clear from (0, 2) to (6, 2) via Row 1!
        bool clear = GridEngine.IsPathClear();
        if (clear)
        {
            QuestManager.RecordObjectiveProgress(QuestIds.Hanoi03, "Objective_ClearStallPath", 1);
            QuestManager.CompleteQuest(QuestIds.Hanoi03);
            CurrentData.Level1State.QuestFlags.Q3_GridSolved = true;
            CurrentData.Level1State.QuestFlags.Q3_Completed = true;

            // Full color wave (100%)
            CurrentData.WorldProgression.SetColorRestoration(RegionIds.Hanoi, 100.0f);
            EventBus.Publish(new RegionColorUpdatedEvent(RegionIds.Hanoi, 100.0f));
            EventBus.Publish(new PuzzleSolvedEvent("Puzzle_StallGrid"));
            AudioService.PlaySfx(SfxType.ColorBloomWave);
        }

        Step();
        return clear;
    }

    public void SimulateLevelClimax()
    {
        // 1. Gate Unlock
        CurrentData.Level1State.QuestFlags.Gate_LongBien_Unlocked = true;
        EventBus.Publish(new GateUnlockedEvent("Gate_LongBien"));
        AudioService.PlaySfx(SfxType.GateUnlocked);

        // 2. Journal Page 1 award
        AlbumService.AwardJournalPage(CurrentData.CollectionAlbum, "JournalPage_Hanoi_01");
        EventBus.Publish(new JournalPageAwardedEvent("JournalPage_Hanoi_01"));

        // 3. Lotus Stamp award
        AlbumService.AwardTravelStamp(CurrentData.CollectionAlbum, "Stamp_HoaSen_Hanoi");
        EventBus.Publish(new StampAwardedEvent("Stamp_HoaSen_Hanoi"));
        AudioService.PlaySfx(SfxType.StampAwarded);

        // 4. Gray Shadow Letter cutscene
        CurrentData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
        EventBus.Publish(new CutsceneTriggeredEvent("Cutscene_GrayShadow_Letter"));

        // 5. Complete Level 1 & Unlock Hue
        CurrentData.Level1State.QuestFlags.Level1_Finished = true;
        CurrentData.WorldProgression.CompleteRegion(RegionIds.Hanoi);
        CurrentData.WorldProgression.UnlockRegion(RegionIds.Hue);

        // Save progress
        SaveLoadService.Save(CurrentData);
        Step();
    }
}
