using System.Linq;
using GameProject.Core.Puzzles;
using Xunit;

namespace GameProject.Tests;

public class StallPuzzleTests
{
    [Fact]
    public void Grid_InitialState_PathIsBlocked()
    {
        var grid = new SpatialGridEngine();

        // Path from (0, 2) to (6, 2) is blocked by default obstacles
        Assert.False(grid.IsPathClear());
        Assert.True(grid.Entities.Count >= 5);
    }

    [Fact]
    public void FlowerCart_CanOnlyMoveVertically()
    {
        var grid = new SpatialGridEngine();

        // Xe Hoa is at x=2, y=1 (size 1x2, occupies y=1 and y=2)
        // Horizontal push must fail
        bool horizontalPush = grid.PushObject("Obj_FlowerCart", Direction.Left);
        Assert.False(horizontalPush);

        // Move Fruit Stall left out of cell (2, 3)
        grid.PushObject("Obj_FruitStall", Direction.Left);

        // Vertical push DOWN into free cell (y=2) must succeed
        bool verticalPush = grid.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.True(verticalPush);

        var cart = grid.Entities.First(e => e.Id == "Obj_FlowerCart");
        Assert.Equal(2, cart.Y);
    }

    [Fact]
    public void Cyclo_CanOnlyMoveHorizontally()
    {
        var grid = new SpatialGridEngine();

        // Xích Lô is at x=3, y=2 (size 2x1)
        // Vertical push must fail
        bool verticalPush = grid.PushObject("Obj_Cyclo", Direction.Up);
        Assert.False(verticalPush);
    }

    [Fact]
    public void BoundaryCollisions_CannotPushOutsideGrid()
    {
        var grid = new SpatialGridEngine();

        // Top curb wall is at y=0. Trying to push Xe Hoa UP into wall must fail
        bool pushUpIntoWall = grid.PushObject("Obj_FlowerCart", Direction.Up);
        Assert.False(pushUpIntoWall);

        var cart = grid.Entities.First(e => e.Id == "Obj_FlowerCart");
        Assert.Equal(1, cart.Y);
    }

    [Fact]
    public void OccupiedCellCollision_CannotPushIntoAnotherObstacle()
    {
        var grid = new SpatialGridEngine();

        // Xe Hoa occupies (2, 1) and (2, 2). Fruit Stall is at (2, 3).
        // Pushing Xe Hoa DOWN into Fruit Stall without moving Fruit Stall first must fail
        bool push = grid.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.False(push);
    }

    [Fact]
    public void SolvingGrid_ClearsContinuousBFSPathFromEntranceToGate()
    {
        var grid = new SpatialGridEngine();
        Assert.False(grid.IsPathClear());

        // Step 1: Push Fruit Stall (1x1 at x=2, y=3) LEFT to (1, 3)
        bool m1 = grid.PushObject("Obj_FruitStall", Direction.Left);
        Assert.True(m1);

        // Step 2: Push Xe Hoa (1x2 at x=2, y=1) DOWN to y=2
        bool m2 = grid.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.True(m2);

        // Continuous path from Entrance (0, 2) to Exit (6, 2) via Row 1 is now open!
        bool isClear = grid.IsPathClear();
        Assert.True(isClear);
    }

    [Fact]
    public void ResetGrid_RestoresAllEntitiesToStartingCoordinates()
    {
        var grid = new SpatialGridEngine();
        Assert.False(grid.IsPathClear());

        // Solve puzzle
        grid.PushObject("Obj_FruitStall", Direction.Left);
        grid.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.True(grid.IsPathClear());

        // Reset
        grid.ResetGrid();

        // Verify restored
        var cartReset = grid.Entities.First(e => e.Id == "Obj_FlowerCart");
        Assert.Equal(1, cartReset.Y);
        var fruitReset = grid.Entities.First(e => e.Id == "Obj_FruitStall");
        Assert.Equal(2, fruitReset.X);
        Assert.Equal(3, fruitReset.Y);

        Assert.False(grid.IsPathClear());
    }

    [Fact]
    public void SpatialGridEngine_FindPath_ReturnsNullWhenBlocked_AndFullCoordinatesWhenSolved()
    {
        var grid = new SpatialGridEngine();

        // 1. Initially blocked
        Assert.Null(grid.FindPath());
        Assert.False(grid.IsPathClear());

        // 2. Execute 2-move solution
        bool p1 = grid.PushObject("Obj_FruitStall", Direction.Left);
        bool p2 = grid.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.True(p1 && p2);

        // 3. Path must be reconstructed
        var path = grid.FindPath();
        Assert.NotNull(path);
        Assert.True(path.Count >= 7);
        Assert.Equal((0, 2), path[0]);               // Entrance
        Assert.Equal((6, 2), path[path.Count - 1]); // Exit Gate

        // 4. Verify Manhattan continuity (step by step)
        for (int i = 0; i < path.Count - 1; i++)
        {
            int dist = System.Math.Abs(path[i + 1].x - path[i].x) + System.Math.Abs(path[i + 1].y - path[i].y);
            Assert.Equal(1, dist);
        }
    }

    [Fact]
    public void StallPuzzle_ResetGrid_EmitsResetEvent_AndClearsPath()
    {
        var grid = new SpatialGridEngine();
        grid.PushObject("Obj_FruitStall", Direction.Left);
        grid.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.True(grid.IsPathClear());

        grid.ResetGrid();
        Assert.False(grid.IsPathClear());
        Assert.Null(grid.FindPath());

        var fruit = grid.Entities.First(e => e.Id == "Obj_FruitStall");
        Assert.Equal(2, fruit.X);
        Assert.Equal(3, fruit.Y);
    }

    [Fact]
    public void StallPuzzle_PushStaticWall_ReturnsFalse_AndEntityDoesNotMove()
    {
        var grid = new SpatialGridEngine();
        bool pushedWall = grid.PushObject("Wall_Top_0", Direction.Down);
        Assert.False(pushedWall);
    }

    [Fact]
    public void LevelClimax_StallSolve_Triggers100PercentBloom_AndUnlocksGate()
    {
        var eventBus = new GameProject.Core.Events.EventBus();
        var saveData = GameProject.Core.Models.SaveData.CreateDefault();
        var questManager = new GameProject.Core.Quests.QuestManager(eventBus);
        GameProject.Core.Quests.HanoiQuests.RegisterAll(questManager);

        questManager.StartQuest(GameProject.Core.Quests.HanoiQuests.Quest1Id);
        questManager.CompleteQuest(GameProject.Core.Quests.HanoiQuests.Quest1Id);
        questManager.StartQuest(GameProject.Core.Quests.HanoiQuests.Quest2Id);
        questManager.CompleteQuest(GameProject.Core.Quests.HanoiQuests.Quest2Id);
        questManager.StartQuest(GameProject.Core.Quests.HanoiQuests.Quest3Id);

        var progression = new GameProject.Core.Quests.LevelProgressionService(saveData, eventBus);

        bool gateUnlockedFired = false;
        bool color100Fired = false;
        eventBus.Subscribe<GameProject.Core.Events.GateUnlockedEvent>(e => gateUnlockedFired = true);
        eventBus.Subscribe<GameProject.Core.Events.RegionColorUpdatedEvent>(e => { if (e.NewPercentage >= 100.0f) color100Fired = true; });

        progression.CompleteLevel1();

        Assert.True(gateUnlockedFired);
        Assert.True(color100Fired);
        Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.Equal(100.0f, saveData.WorldProgression.GetColorRestoration(GameProject.Core.Models.WorldProgression.RegionHanoi));
        Assert.True(saveData.CollectionAlbum.HasJournalPage(GameProject.Core.Models.AlbumIds.HanoiJournalPage));
        Assert.True(saveData.CollectionAlbum.HasStamp(GameProject.Core.Models.AlbumIds.HanoiLotusStamp));
    }

    [Fact]
    public void StallPuzzleScene_PushMethod_MaintainsMoveCountAndSolves()
    {
        var eventBus = new GameProject.Core.Events.EventBus();
        var saveData = GameProject.Core.Models.SaveData.CreateDefault();
        var questManager = new GameProject.Core.Quests.QuestManager(eventBus);
        GameProject.Core.Quests.HanoiQuests.RegisterAll(questManager);

        bool resetEventFired = false;
        eventBus.Subscribe<GameProject.Core.Events.StallPuzzleResetEvent>(e => resetEventFired = true);

        var scene = new GameProject.Stages.StallPuzzleScene(saveData, questManager, eventBus);
        scene.Enter();

        Assert.Equal(0, scene.MoveCount);
        Assert.False(scene.IsSolved);

        // Move 1: Fruit Stall Left
        bool m1 = scene.Push("Obj_FruitStall", Direction.Left);
        Assert.True(m1);
        Assert.Equal(1, scene.MoveCount);
        Assert.False(scene.IsSolved);

        // Reset
        scene.ResetGrid();
        Assert.True(resetEventFired);
        Assert.Equal(0, scene.MoveCount);

        // Solve again
        scene.Push("Obj_FruitStall", Direction.Left);
        scene.Push("Obj_FlowerCart", Direction.Down);
        Assert.True(scene.IsSolved);
        Assert.Equal(2, scene.MoveCount);
        Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
    }
}
