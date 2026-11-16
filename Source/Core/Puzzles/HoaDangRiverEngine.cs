using System;
using System.Collections.Generic;
using System.Linq;

namespace GameProject.Core.Puzzles;

/// <summary>
/// Domain contract for Perfume River lantern sliding grid puzzle.
/// </summary>
public interface IRiverGridEngine : IPuzzleEngine
{
    int Width { get; }
    int Height { get; }
    (int x, int y) Entrance { get; }
    (int x, int y) ExitGate { get; }

    IReadOnlyList<GridEntity> Entities { get; }
    void InitializeGrid(int width = 7, int height = 5);
    bool PushObject(string objectId, Direction direction);
    bool IsPathClear();
    List<(int x, int y)>? FindPath();
    void ResetGrid();
}

/// <summary>
/// Spatial river grid engine for Quest 3: "Thả Đèn Hoa Đăng trên Sông Hương".
/// Recreates the river surface between Imperial Citadel pier (0, 2) and sacred waters of Thiên Mụ (Width - 1, 2).
/// Features movable obstacles:
/// - Dragon Boat (1x2 at x=2, y=1, vertical only)
/// - Fruit Boat / Sampan (1x1 at x=2, y=3, omnidirectional)
/// - Flower Raft (2x1 at x=3, y=2, horizontal only)
/// - Lotus Cluster / Lily Patch (1x1 at x=5, y=2, omnidirectional)
/// - Fixed stone embankments/banks on Row 0 (North) and Row Height - 1 (South)
/// Executes pure domain BFS pathfinding headlessly with zero graphics or audio dependencies.
/// </summary>
public sealed class HoaDangRiverEngine : IRiverGridEngine, IStallGridEngine
{
    public const int DefaultWidth = 7;
    public const int DefaultHeight = 5;

    public int Width { get; private set; } = DefaultWidth;
    public int Height { get; private set; } = DefaultHeight;

    public (int x, int y) Entrance => (0, 2);
    public (int x, int y) ExitGate => (Width - 1, 2);

    private readonly List<GridEntity> _initialEntities = new();
    private readonly List<GridEntity> _entities = new();

    public IReadOnlyList<GridEntity> Entities => _entities;
    public bool IsSolved => IsPathClear();

    public HoaDangRiverEngine(int width = DefaultWidth, int height = DefaultHeight)
    {
        InitializeGrid(width, height);
    }

    public void InitializeGrid(int width = DefaultWidth, int height = DefaultHeight)
    {
        Width = width;
        Height = height;

        _initialEntities.Clear();

        // 1. Boundary riverbanks on Row 0 and Row Height - 1
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

        // 2. Movable river obstacles:
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

        // Fruit Boat / Fisher Sampan (1x1 at x=2, y=3) -> occupies (2, 3)
        // Together with Dragon Boat, Column 2 is 100% blocked from row 0 to 4!
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

        // Lotus Cluster / Lily Patch (1x1 at x=5, y=2) -> occupies (5, 2)
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

    public void ResetGrid()
    {
        _entities.Clear();
        foreach (var entity in _initialEntities)
        {
            _entities.Add(entity.Clone());
        }
    }

    public void ResetPuzzle() => ResetGrid();

    public static bool MatchesId(string? entityId, string? queryId)
    {
        if (string.IsNullOrWhiteSpace(entityId) || string.IsNullOrWhiteSpace(queryId))
            return false;

        if (string.Equals(entityId, queryId, StringComparison.OrdinalIgnoreCase))
            return true;

        // Untangled 1-to-1 alias mappings (symmetrical)
        if ((string.Equals(entityId, "Obj_DragonBoat_1", StringComparison.OrdinalIgnoreCase) && string.Equals(queryId, "Obj_DragonBoat", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(queryId, "Obj_DragonBoat_1", StringComparison.OrdinalIgnoreCase) && string.Equals(entityId, "Obj_DragonBoat", StringComparison.OrdinalIgnoreCase)))
            return true;

        if ((string.Equals(entityId, "Obj_FlowerRaft_1", StringComparison.OrdinalIgnoreCase) && string.Equals(queryId, "Obj_FlowerRaft", StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(queryId, "Obj_FlowerRaft_1", StringComparison.OrdinalIgnoreCase) && string.Equals(entityId, "Obj_FlowerRaft", StringComparison.OrdinalIgnoreCase)))
            return true;

        if ((string.Equals(entityId, "Obj_FruitBoat_1", StringComparison.OrdinalIgnoreCase) &&
             (string.Equals(queryId, "Obj_FruitBoat", StringComparison.OrdinalIgnoreCase) || string.Equals(queryId, "Obj_FisherSampan", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(queryId, "Obj_FruitBoat_1", StringComparison.OrdinalIgnoreCase) &&
             (string.Equals(entityId, "Obj_FruitBoat", StringComparison.OrdinalIgnoreCase) || string.Equals(entityId, "Obj_FisherSampan", StringComparison.OrdinalIgnoreCase))))
            return true;

        if ((string.Equals(entityId, "Obj_LotusCluster_1", StringComparison.OrdinalIgnoreCase) &&
             (string.Equals(queryId, "Obj_LotusCluster", StringComparison.OrdinalIgnoreCase) || string.Equals(queryId, "Obj_WaterLilyPatch", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(queryId, "Obj_LotusCluster_1", StringComparison.OrdinalIgnoreCase) &&
             (string.Equals(entityId, "Obj_LotusCluster", StringComparison.OrdinalIgnoreCase) || string.Equals(entityId, "Obj_WaterLilyPatch", StringComparison.OrdinalIgnoreCase))))
            return true;

        // Static embankment bank/pier matching
        if (entityId.StartsWith("RiverBank_North_", StringComparison.OrdinalIgnoreCase) &&
            queryId.StartsWith("Pier_North_", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(entityId["RiverBank_North_".Length..], queryId["Pier_North_".Length..], StringComparison.OrdinalIgnoreCase);
        }
        if (queryId.StartsWith("RiverBank_North_", StringComparison.OrdinalIgnoreCase) &&
            entityId.StartsWith("Pier_North_", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(queryId["RiverBank_North_".Length..], entityId["Pier_North_".Length..], StringComparison.OrdinalIgnoreCase);
        }

        if (entityId.StartsWith("RiverBank_South_", StringComparison.OrdinalIgnoreCase) &&
            queryId.StartsWith("Pier_South_", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(entityId["RiverBank_South_".Length..], queryId["Pier_South_".Length..], StringComparison.OrdinalIgnoreCase);
        }
        if (queryId.StartsWith("RiverBank_South_", StringComparison.OrdinalIgnoreCase) &&
            entityId.StartsWith("Pier_South_", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(queryId["RiverBank_South_".Length..], entityId["Pier_South_".Length..], StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    public bool PushObject(string objectId, Direction direction)
    {
        if (string.IsNullOrWhiteSpace(objectId)) return false;

        var entity = _entities.FirstOrDefault(e => MatchesId(e.Id, objectId));
        if (entity == null || entity.IsStaticWall) return false;

        // Verify push directional constraints
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

        // Check grid boundary limits
        if (targetX < 0 || targetX + entity.Width > Width) return false;
        if (targetY < 0 || targetY + entity.Height > Height) return false;

        // Check collisions with all other entities
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
                    return false;
                }
            }
        }

        entity.X = targetX;
        entity.Y = targetY;
        return true;
    }

    public bool IsPathClear() => FindPath() != null;

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

        // BFS pathfinding from Entrance (0, 2) to ExitGate (Width - 1, 2)
        var queue = new Queue<(int x, int y)>();
        var visited = new HashSet<(int x, int y)>();
        var parent = new Dictionary<(int x, int y), (int x, int y)>();

        queue.Enqueue(Entrance);
        visited.Add(Entrance);

        (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        bool found = false;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == ExitGate)
            {
                found = true;
                break;
            }

            foreach (var (dx, dy) in dirs)
            {
                int nx = current.x + dx;
                int ny = current.y + dy;

                if (nx >= 0 && nx < Width && ny >= 0 && ny < Height)
                {
                    var neighbor = (nx, ny);
                    if (!visited.Contains(neighbor) && !occupied.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        parent[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        if (!found) return null;

        var path = new List<(int x, int y)>();
        var curr = ExitGate;
        while (curr != Entrance)
        {
            path.Add(curr);
            curr = parent[curr];
        }
        path.Add(Entrance);
        path.Reverse();
        return path;
    }
}
