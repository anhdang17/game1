using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.WorldMap;
using GameProject.Core.Ports;

namespace GameProject.Tests.Mocks;

/// <summary>
/// Headless test simulation harness for Chapter 2: Cố Đô Huế.
/// </summary>
public sealed class HueTestSimulationDriver
{
    public EventBus EventBus { get; }
    public MockInputService InputService { get; }
    public MockSaveStorage Storage { get; }
    public NullAudioService AudioService { get; }

    public SaveData CurrentData { get; set; }
    public Inventory Inventory { get; } = new();
    public SaveLoadService SaveLoadService { get; }
    public QuestManager QuestManager { get; }
    public BunBoHueCookingEngine BunBoEngine { get; }
    public HoaDangRiverEngine RiverEngine { get; }
    public DialogueService DialogueService { get; }
    public AlbumService AlbumService { get; }
    public WorldMapService WorldMapService { get; }
    public LevelProgressionService ProgressionService { get; }

    public float CurrentTime { get; private set; } = 0.0f;
    public int TotalTicksSimulated { get; private set; } = 0;

    public List<IGameEvent> ReceivedEvents { get; } = new();

    public HueTestSimulationDriver(MockSaveStorage? storage = null)
    {
        EventBus = new EventBus();
        InputService = new MockInputService();
        Storage = storage ?? new MockSaveStorage();
        AudioService = new NullAudioService();

        CurrentData = SaveData.CreateDefault();
        SaveLoadService = new SaveLoadService(Storage);
        QuestManager = new QuestManager(EventBus);
        HueQuests.RegisterAll(QuestManager);

        BunBoEngine = new BunBoHueCookingEngine();
        RiverEngine = new HoaDangRiverEngine();
        DialogueService = new DialogueService();
        AlbumService = new AlbumService();
        WorldMapService = new WorldMapService();
        ProgressionService = new LevelProgressionService(CurrentData, EventBus);

        // Load Hue dialogue graph
        string dialoguePath = File.Exists("Data/dialogues_hue.json")
            ? "Data/dialogues_hue.json"
            : (File.Exists("Source/Data/dialogues_hue.json") ? "Source/Data/dialogues_hue.json" : "../../../Source/Data/dialogues_hue.json");

        if (File.Exists(dialoguePath))
        {
            DialogueService.LoadDialogueFile(dialoguePath);
        }

        // Event tracking
        EventBus.Subscribe<ItemCollectedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<QuestStartedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<QuestProgressUpdatedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<QuestCompletedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<PuzzleSolvedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<RegionColorUpdatedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<ColorRestoredEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<RegionCompletedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<GateUnlockedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<JournalPageAwardedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<StampAwardedEvent>(e => ReceivedEvents.Add(e));
        EventBus.Subscribe<RegionUnlockedEvent>(e => ReceivedEvents.Add(e));
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

    // --- Simulation Actions ---

    public void SimulateCollectIngredient(string itemId)
    {
        Inventory.AddItem(itemId);
        if (!CurrentData.Level1State.InventoryItems.Contains(itemId))
        {
            CurrentData.Level1State.InventoryItems.Add(itemId);
        }

        switch (itemId)
        {
            case ItemIds.BunSoiTo: CurrentData.QuestFlags.Hue_Q1_BunSoiTo_Collected = true; break;
            case ItemIds.BapBo: CurrentData.QuestFlags.Hue_Q1_BapBo_Collected = true; break;
            case ItemIds.ChaCua: CurrentData.QuestFlags.Hue_Q1_ChaCua_Collected = true; break;
            case ItemIds.NuocDungHue: CurrentData.QuestFlags.Hue_Q1_NuocDungHue_Collected = true; break;
            case ItemIds.RauSongHue: CurrentData.QuestFlags.Hue_Q1_RauSongHue_Collected = true; break;
        }

        EventBus.Publish(new ItemCollectedEvent(itemId));
        QuestManager.RecordObjectiveProgress(HueQuests.Quest1Id, itemId, 1);
        AudioService.PlaySfx(SfxType.ItemCollected);
        Step();
    }

    public void SimulateGatherAllBunBoIngredients()
    {
        QuestManager.StartQuest(HueQuests.Quest1Id);
        CurrentData.QuestFlags.Hue_Q1_Started = true;

        SimulateCollectIngredient(ItemIds.BunSoiTo);
        SimulateCollectIngredient(ItemIds.BapBo);
        SimulateCollectIngredient(ItemIds.ChaCua);
        SimulateCollectIngredient(ItemIds.NuocDungHue);
        SimulateCollectIngredient(ItemIds.RauSongHue);

        QuestManager.CompleteQuest(HueQuests.Quest1Id);
        CurrentData.QuestFlags.Hue_Q1_Completed = true;
        Step();
    }

    public bool SimulateSolveBunBoCooking()
    {
        QuestManager.StartQuest(HueQuests.Quest2Id);
        CurrentData.QuestFlags.Hue_Q2_Started = true;

        BunBoEngine.PlaceIngredient(0, ItemIds.BunSoiTo);
        BunBoEngine.PlaceIngredient(1, ItemIds.BapBo);
        BunBoEngine.PlaceIngredient(2, ItemIds.ChaCua);
        BunBoEngine.PlaceIngredient(3, ItemIds.RauSongHue);
        BunBoEngine.PlaceIngredient(4, ItemIds.NuocDungHue);

        bool valid = BunBoEngine.ValidateAssembly(out _);
        if (valid)
        {
            QuestManager.RecordObjectiveProgress(HueQuests.Quest2Id, "cook_bun_bo", 1);
            QuestManager.CompleteQuest(HueQuests.Quest2Id);
            CurrentData.QuestFlags.Hue_Q2_BunBoCooked = true;
            CurrentData.QuestFlags.Hue_Q2_Completed = true;

            ProgressionService.OnHueQuest2Completed();
            EventBus.Publish(new PuzzleSolvedEvent("Puzzle_BunBoCooking"));
            AudioService.PlaySfx(SfxType.PhoCookSuccess);
            AudioService.PlaySfx(SfxType.ColorBloomWave);
        }

        Step();
        return valid;
    }

    public bool SimulateClearRiverPath()
    {
        QuestManager.StartQuest(HueQuests.Quest3Id);
        CurrentData.QuestFlags.Hue_Q3_Started = true;

        // Push Fruit Boat (1x1 at x=2, y=3) LEFT to (1, 3)
        RiverEngine.PushObject("Obj_FruitBoat_1", Direction.Left);

        // Push Dragon Boat (1x2 at x=2, y=1) DOWN to y=2 (occupies 2,2 and 2,3)
        RiverEngine.PushObject("Obj_DragonBoat_1", Direction.Down);

        // Push Lotus Cluster (1x1 at x=5, y=2) DOWN to y=3
        RiverEngine.PushObject("Obj_LotusCluster_1", Direction.Down);

        bool clear = RiverEngine.IsPathClear();
        if (clear)
        {
            QuestManager.RecordObjectiveProgress(HueQuests.Quest3Id, "release_hoa_dang", 1);
            QuestManager.CompleteQuest(HueQuests.Quest3Id);
            CurrentData.QuestFlags.Hue_Q3_PuzzleSolved = true;
            CurrentData.QuestFlags.Hue_Q3_Completed = true;

            EventBus.Publish(new PuzzleSolvedEvent("Puzzle_HoaDangRiver"));
            AudioService.PlaySfx(SfxType.ColorBloomWave);
        }

        Step();
        return clear;
    }

    public void SimulateHueClimax()
    {
        ProgressionService.CompleteHue();
        SaveLoadService.Save(CurrentData);
        Step();
    }
}
