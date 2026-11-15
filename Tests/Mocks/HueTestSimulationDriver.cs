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
/// Domain puzzle engine for authentic 5-step Bún Bò Huế cooking assembly.
/// </summary>
public sealed class BunBoHueCookingEngine : IPhoPuzzleEngine
{
    public const int TotalSlots = 5;

    public static readonly string[] CorrectSequence = new[]
    {
        ItemIds.BunSoiTo,
        ItemIds.BapBo,
        ItemIds.ChaCua,
        ItemIds.RauSongHue,
        ItemIds.NuocDungHue
    };

    private readonly string?[] _slots = new string?[TotalSlots];
    public IReadOnlyList<string?> CurrentSlots => _slots;
    public bool IsSolved { get; private set; } = false;

    public bool PlaceIngredient(int slotIndex, string ingredientId)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return false;
        if (string.IsNullOrWhiteSpace(ingredientId)) return false;

        _slots[slotIndex] = ingredientId;
        return true;
    }

    public bool RemoveIngredient(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= TotalSlots) return false;
        _slots[slotIndex] = null;
        return true;
    }

    public bool ValidateAssembly(out string errorMessage)
    {
        errorMessage = string.Empty;
        IsSolved = false;

        // 1. Check all slots filled
        for (int i = 0; i < TotalSlots; i++)
        {
            if (string.IsNullOrWhiteSpace(_slots[i]))
            {
                errorMessage = "Bát bún bò vẫn còn thiếu nguyên liệu, hãy hoàn thiện đủ cả 5 bước nhé!";
                return false;
            }
        }

        // 2. Distinct ingredients
        if (_slots.Distinct().Count() < TotalSlots)
        {
            errorMessage = "Mỗi bước nấu bún bò chỉ dùng một nguyên liệu riêng biệt!";
            return false;
        }

        // 3. Recognized Hue ingredient check
        foreach (var slot in _slots)
        {
            if (!ItemIds.AllBunBoIngredients.Contains(slot!))
            {
                errorMessage = "Nguyên liệu không hợp lệ cho món Bún Bò Huế!";
                return false;
            }
        }

        // 4. Broth placed first rule (authentic cultural guidance)
        if (_slots[0] == ItemIds.NuocDungHue)
        {
            errorMessage = "Nước dùng không thể chan trước khi xếp bún và thịt! Làm như vậy bún sẽ bị nát và nguội.";
            return false;
        }

        // 5. Sequence match
        if (!_slots.SequenceEqual(CorrectSequence))
        {
            errorMessage = "Thứ tự các bước nấu bún bò chưa chuẩn vị Huế! Hãy xếp bún trước, sau đó là bắp bò, chả cua, rau sống và chan nước dùng sau cùng.";
            return false;
        }

        IsSolved = true;
        return true;
    }

    public void ResetPuzzle()
    {
        Array.Clear(_slots, 0, _slots.Length);
        IsSolved = false;
    }
}

/// <summary>
/// Spatial grid obstacle engine for Thả Đèn Hoa Đăng trên Sông Hương.
/// </summary>
public sealed class HoaDangRiverEngine : IStallGridEngine
{
    public int Width { get; private set; } = 7;
    public int Height { get; private set; } = 5;
    public (int x, int y) Entrance => (0, 2);
    public (int x, int y) ExitGate => (Width - 1, 2);

    private readonly List<GridEntity> _entities = new();
    private readonly List<GridEntity> _initialEntities = new();
    public IReadOnlyList<GridEntity> Entities => _entities;

    public HoaDangRiverEngine()
    {
        InitializeGrid();
    }

    public void InitializeGrid(int width = 7, int height = 5)
    {
        Width = width;
        Height = height;
        _initialEntities.Clear();

        // 1. Boundary banks on Row 0 and Row 4
        for (int x = 0; x < Width; x++)
        {
            _initialEntities.Add(new GridEntity
            {
                Id = $"RiverBank_North_{x}",
                Name = "Bờ Bắc Sông Hương",
                X = x,
                Y = 0,
                IsStaticWall = true,
                CanPushHorizontally = false,
                CanPushVertically = false
            });

            _initialEntities.Add(new GridEntity
            {
                Id = $"RiverBank_South_{x}",
                Name = "Bờ Nam Sông Hương",
                X = x,
                Y = Height - 1,
                IsStaticWall = true,
                CanPushHorizontally = false,
                CanPushVertically = false
            });
        }

        // 2. Movable Hue river obstacles
        // Dragon Boat (1x2 at x=2, y=1) -> occupies (2, 1) and (2, 2)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_DragonBoat_1",
            Name = "Thuyền Rồng Hoàng Gia",
            X = 2,
            Y = 1,
            Width = 1,
            Height = 2,
            CanPushHorizontally = false,
            CanPushVertically = true
        });

        // Fruit Boat (1x1 at x=2, y=3)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_FruitBoat_1",
            Name = "Thuyền Hoa Quả Đông Ba",
            X = 2,
            Y = 3,
            Width = 1,
            Height = 1,
            CanPushHorizontally = true,
            CanPushVertically = true
        });

        // Flower Raft (2x1 at x=3, y=2) -> occupies (3, 2) and (4, 2)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_FlowerRaft_1",
            Name = "Bè Hoa Đăng",
            X = 3,
            Y = 2,
            Width = 2,
            Height = 1,
            CanPushHorizontally = true,
            CanPushVertically = false
        });

        // Lotus Cluster (1x1 at x=5, y=2) -> occupies (5, 2)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_LotusCluster_1",
            Name = "Cụm Sen Sông Hương",
            X = 5,
            Y = 2,
            Width = 1,
            Height = 1,
            CanPushHorizontally = true,
            CanPushVertically = true
        });

        ResetGrid();
    }

    public bool PushObject(string objectId, Direction direction)
    {
        var entity = _entities.FirstOrDefault(e => e.Id == objectId);
        if (entity == null || entity.IsStaticWall) return false;

        // Verify movement axis constraints
        if ((direction == Direction.Left || direction == Direction.Right) && !entity.CanPushHorizontally)
            return false;
        if ((direction == Direction.Up || direction == Direction.Down) && !entity.CanPushVertically)
            return false;

        int dx = direction switch
        {
            Direction.Left => -1,
            Direction.Right => 1,
            _ => 0
        };

        int dy = direction switch
        {
            Direction.Up => -1,
            Direction.Down => 1,
            _ => 0
        };

        int targetX = entity.X + dx;
        int targetY = entity.Y + dy;

        // Check grid boundary bounds
        if (targetX < 0 || targetX + entity.Width > Width ||
            targetY < 0 || targetY + entity.Height > Height)
        {
            return false;
        }

        // Check collisions with other entities
        var targetCells = new HashSet<(int x, int y)>();
        for (int x = 0; x < entity.Width; x++)
        {
            for (int y = 0; y < entity.Height; y++)
            {
                targetCells.Add((targetX + x, targetY + y));
            }
        }

        foreach (var other in _entities)
        {
            if (other.Id == entity.Id) continue;
            foreach (var cell in other.OccupiedCells)
            {
                if (targetCells.Contains(cell))
                {
                    return false; // Collision detected
                }
            }
        }

        entity.X = targetX;
        entity.Y = targetY;
        return true;
    }

    public bool IsPathClear()
    {
        return FindPath() != null;
    }

    public List<(int x, int y)>? FindPath()
    {
        var occupied = new HashSet<(int x, int y)>();
        foreach (var entity in _entities)
        {
            foreach (var cell in entity.OccupiedCells)
            {
                occupied.Add(cell);
            }
        }

        if (occupied.Contains(Entrance) || occupied.Contains(ExitGate))
        {
            return null;
        }

        var queue = new Queue<(int x, int y)>();
        var visited = new HashSet<(int x, int y)>();
        var parent = new Dictionary<(int x, int y), (int x, int y)>();

        queue.Enqueue(Entrance);
        visited.Add(Entrance);

        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == ExitGate)
            {
                var path = new List<(int x, int y)>();
                var step = ExitGate;
                while (step != Entrance)
                {
                    path.Add(step);
                    step = parent[step];
                }
                path.Add(Entrance);
                path.Reverse();
                return path;
            }

            foreach (var (dx, dy) in dirs)
            {
                int nx = cur.x + dx;
                int ny = cur.y + dy;

                if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
                {
                    var next = (nx, ny);
                    if (!visited.Contains(next) && !occupied.Contains(next))
                    {
                        visited.Add(next);
                        parent[next] = cur;
                        queue.Enqueue(next);
                    }
                }
            }
        }

        return null;
    }

    public void ResetGrid()
    {
        _entities.Clear();
        foreach (var ent in _initialEntities)
        {
            _entities.Add(ent.Clone());
        }
    }
}

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

        // Push Lotus Cluster (1x1 at x=5, y=2) UP to y=1
        RiverEngine.PushObject("Obj_LotusCluster_1", Direction.Up);

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
