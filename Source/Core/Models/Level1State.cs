using System;
using System.Collections.Generic;

namespace GameProject.Core.Models;

/// <summary>
/// Serializable position coordinates for pushable obstacles in the Hanoi Quest 3 spatial grid puzzle.
/// </summary>
public sealed class Puzzle3ObjectPosition
{
    public string ObjectId { get; set; } = string.Empty;
    public int GridX { get; set; }
    public int GridY { get; set; }

    public Puzzle3ObjectPosition() { }

    public Puzzle3ObjectPosition(string objectId, int gridX, int gridY)
    {
        ObjectId = objectId;
        GridX = gridX;
        GridY = gridY;
    }
}

/// <summary>
/// State container for Hanoi Level 1 exploration, active quest, inventory, and puzzle object coordinates.
/// </summary>
public sealed class Level1State
{
    private string _activeQuestId = "Quest_Hanoi_01";
    private QuestFlags _questFlags = new();
    private List<string> _inventoryItems = new();
    private List<Puzzle3ObjectPosition> _puzzle3ObjectPositions = new();

    public double PlayerSpawnX { get; set; } = 120.0;
    public double PlayerSpawnY { get; set; } = 250.0;

    public string ActiveQuestId
    {
        get => _activeQuestId;
        set => _activeQuestId = string.IsNullOrWhiteSpace(value) ? "Quest_Hanoi_01" : value;
    }

    public QuestFlags QuestFlags
    {
        get => _questFlags ??= QuestFlags.CreateDefault();
        set => _questFlags = value ?? QuestFlags.CreateDefault();
    }

    public List<string> InventoryItems
    {
        get => _inventoryItems ??= new List<string>();
        set => _inventoryItems = value ?? new List<string>();
    }

    public List<Puzzle3ObjectPosition> Puzzle3ObjectPositions
    {
        get => _puzzle3ObjectPositions ??= new List<Puzzle3ObjectPosition>();
        set => _puzzle3ObjectPositions = value ?? new List<Puzzle3ObjectPosition>();
    }

    public static Level1State CreateDefault() => new()
    {
        PlayerSpawnX = 120.0,
        PlayerSpawnY = 250.0,
        ActiveQuestId = "Quest_Hanoi_01",
        QuestFlags = QuestFlags.CreateDefault(),
        InventoryItems = new List<string>(),
        Puzzle3ObjectPositions = new List<Puzzle3ObjectPosition>()
    };

    public void Sanitize()
    {
        if (string.IsNullOrWhiteSpace(ActiveQuestId))
        {
            ActiveQuestId = "Quest_Hanoi_01";
        }

        QuestFlags ??= QuestFlags.CreateDefault();
        InventoryItems ??= new List<string>();
        Puzzle3ObjectPositions ??= new List<Puzzle3ObjectPosition>();

        InventoryItems.RemoveAll(string.IsNullOrWhiteSpace);
        Puzzle3ObjectPositions.RemoveAll(p => p == null || string.IsNullOrWhiteSpace(p.ObjectId));

        if (double.IsNaN(PlayerSpawnX) || double.IsInfinity(PlayerSpawnX))
        {
            PlayerSpawnX = 120.0;
        }

        if (double.IsNaN(PlayerSpawnY) || double.IsInfinity(PlayerSpawnY))
        {
            PlayerSpawnY = 250.0;
        }
    }
}
