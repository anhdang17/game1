using System;
using System.Collections.Generic;
using System.Linq;

namespace GameProject.Core.Puzzles;

public enum Direction
{
    Up,
    Down,
    Left,
    Right
}

public sealed class GridEntity
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
    public bool CanPushHorizontally { get; set; } = true;
    public bool CanPushVertically { get; set; } = true;
    public bool IsStaticWall { get; set; } = false;

    public IEnumerable<(int x, int y)> OccupiedCells
    {
        get
        {
            for (int dx = 0; dx < Width; dx++)
            {
                for (int dy = 0; dy < Height; dy++)
                {
                    yield return (X + dx, Y + dy);
                }
            }
        }
    }

    public GridEntity Clone()
    {
        return new GridEntity
        {
            Id = Id,
            Name = Name,
            X = X,
            Y = Y,
            Width = Width,
            Height = Height,
            CanPushHorizontally = CanPushHorizontally,
            CanPushVertically = CanPushVertically,
            IsStaticWall = IsStaticWall
        };
    }
}

public interface IStallGridEngine
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

public sealed class SpatialGridEngine : IStallGridEngine
{
    public int Width { get; private set; } = 7;
    public int Height { get; private set; } = 5;

    public (int x, int y) Entrance => (0, 2);
    public (int x, int y) ExitGate => (Width - 1, 2);

    private readonly List<GridEntity> _initialEntities = new();
    private readonly List<GridEntity> _entities = new();

    public IReadOnlyList<GridEntity> Entities => _entities;

    public SpatialGridEngine()
    {
        InitializeGrid();
    }

    public void InitializeGrid(int width = 7, int height = 5)
    {
        Width = width;
        Height = height;

        _initialEntities.Clear();

        // 1. Fixed boundary curbstones on Row 0 and Row 4
        for (int x = 0; x < Width; x++)
        {
            _initialEntities.Add(new GridEntity
            {
                Id = $"Wall_Top_{x}",
                Name = "Gờ Vỉa Hè Phía Bắc",
                X = x,
                Y = 0,
                IsStaticWall = true,
                CanPushHorizontally = false,
                CanPushVertically = false
            });

            _initialEntities.Add(new GridEntity
            {
                Id = $"Wall_Bottom_{x}",
                Name = "Gờ Vỉa Hè Phía Nam",
                X = x,
                Y = Height - 1,
                IsStaticWall = true,
                CanPushHorizontally = false,
                CanPushVertically = false
            });
        }

        // 2. Movable Entities:
        // Flower Cart (1x2 at x=2, y=1) -> occupies (2, 1) and (2, 2)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_FlowerCart",
            Name = "Xe Hoa Hà Nội",
            X = 2,
            Y = 1,
            Width = 1,
            Height = 2,
            CanPushHorizontally = false,
            CanPushVertically = true
        });

        // Fruit Stall (1x1 at x=2, y=3) -> occupies (2, 3)
        // Together with Flower Cart, Column 2 is 100% blocked from row 0 to 4!
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_FruitStall",
            Name = "Sạp Hoa Quả Tươi",
            X = 2,
            Y = 3,
            Width = 1,
            Height = 1,
            CanPushHorizontally = true,
            CanPushVertically = true
        });

        // Cyclo (2x1 at x=3, y=2) -> occupies (3, 2) and (4, 2)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_Cyclo",
            Name = "Xe Xích Lô Phố Cổ",
            X = 3,
            Y = 2,
            Width = 2,
            Height = 1,
            CanPushHorizontally = true,
            CanPushVertically = false
        });

        // Tea Stall (1x1 at x=5, y=2) -> occupies (5, 2)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_TeaStall",
            Name = "Quầy Nước Chè Vỉa Hè",
            X = 5,
            Y = 2,
            Width = 1,
            Height = 1,
            CanPushHorizontally = true,
            CanPushVertically = true
        });

        // Ceramic Crates (1x1 at x=3, y=3)
        _initialEntities.Add(new GridEntity
        {
            Id = "Obj_CeramicCrates",
            Name = "Thùng Gốm Bát Tràng",
            X = 3,
            Y = 3,
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
        foreach (var e in _initialEntities)
        {
            _entities.Add(e.Clone());
        }
    }

    public bool PushObject(string objectId, Direction direction)
    {
        var entity = _entities.FirstOrDefault(e => e.Id == objectId);
        if (entity == null || entity.IsStaticWall) return false;

        // Verify direction constraint
        if ((direction == Direction.Left || direction == Direction.Right) && !entity.CanPushHorizontally)
            return false;
        if ((direction == Direction.Up || direction == Direction.Down) && !entity.CanPushVertically)
            return false;

        int dx = 0;
        int dy = 0;
        switch (direction)
        {
            case Direction.Up: dy = -1; break;
            case Direction.Down: dy = 1; break;
            case Direction.Left: dx = -1; break;
            case Direction.Right: dx = 1; break;
        }

        int targetX = entity.X + dx;
        int targetY = entity.Y + dy;

        // Check grid bounds for all cells of the entity
        if (targetX < 0 || targetX + entity.Width > Width) return false;
        if (targetY < 0 || targetY + entity.Height > Height) return false;

        // Check collision with other entities
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
                    // Target cell is occupied
                    return false;
                }
            }
        }

        // Move allowed
        entity.X = targetX;
        entity.Y = targetY;
        return true;
    }

    public bool IsPathClear() => FindPath() != null;

    public List<(int x, int y)>? FindPath()
    {
        // Build occupancy map
        var occupied = new HashSet<(int x, int y)>();
        foreach (var entity in _entities)
        {
            foreach (var cell in entity.OccupiedCells)
            {
                occupied.Add(cell);
            }
        }

        // If entrance or exit itself is occupied, no path
        if (occupied.Contains(Entrance) || occupied.Contains(ExitGate))
        {
            return null;
        }

        // BFS pathfinding from Entrance (0, 2) to ExitGate (6, 2)
        var queue = new Queue<(int x, int y)>();
        var visited = new HashSet<(int x, int y)>();
        var parent = new Dictionary<(int x, int y), (int x, int y)>();

        queue.Enqueue(Entrance);
        visited.Add(Entrance);

        int[] dX = { 0, 0, -1, 1 };
        int[] dY = { -1, 1, 0, 0 };

        bool found = false;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == ExitGate)
            {
                found = true;
                break;
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = current.x + dX[i];
                int ny = current.y + dY[i];

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
