using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Core.State;
using GameProject.Stages;
using GameProject.UI;
using Xunit;
using MockInput = GameProject.Core.Ports.MockInputService;
using NullAudio = GameProject.Core.Ports.NullAudioService;

namespace GameProject.Tests;

/// <summary>
/// Milestone 3 Adversarial Challenge & Stress Test Suite:
/// - Phở Assembly Engine permutations, boundary inputs, repeated slots, state purity.
/// - SpatialGridEngine boundary stalls, push collisions, oscillation, and BFS path monotonicity.
/// - Modal state overlays, rapid transitions, and cutscene state lifecycle.
/// </summary>
public sealed class Milestone3Challenger1StressTests
{
    // =========================================================================
    // SECTION 1: PHỞ ASSEMBLY ENGINE ADVERSARIAL CHALLENGES
    // =========================================================================

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(-100)]
    public void PhoAssembly_OutOfBoundsSlotIndex_ReturnsFalse_AndDoesNotMutate(int invalidSlot)
    {
        var engine = new PhoCookingEngine();
        bool placed = engine.PlaceIngredient(invalidSlot, ItemIds.BanhPho);
        Assert.False(placed);

        bool removed = engine.RemoveIngredient(invalidSlot);
        Assert.False(removed);

        for (int i = 0; i < PhoCookingEngine.TotalSlots; i++)
        {
            Assert.Null(engine.CurrentSlots[i]);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t\n")]
    public void PhoAssembly_NullOrWhitespaceIngredient_ReturnsFalse(string? invalidIngredient)
    {
        var engine = new PhoCookingEngine();
        bool placed = engine.PlaceIngredient(0, invalidIngredient!);
        Assert.False(placed);
        Assert.Null(engine.CurrentSlots[0]);
    }

    [Fact]
    public void PhoAssembly_RepeatedSlotPlacement_OverwritesPreviousIngredient()
    {
        var engine = new PhoCookingEngine();

        // Overwrite slot 0 multiple times
        Assert.True(engine.PlaceIngredient(0, ItemIds.ThitBo));
        Assert.Equal(ItemIds.ThitBo, engine.CurrentSlots[0]);

        Assert.True(engine.PlaceIngredient(0, ItemIds.NuocDung));
        Assert.Equal(ItemIds.NuocDung, engine.CurrentSlots[0]);

        Assert.True(engine.PlaceIngredient(0, ItemIds.BanhPho));
        Assert.Equal(ItemIds.BanhPho, engine.CurrentSlots[0]);
    }

    [Fact]
    public void PhoAssembly_DuplicateIngredientAcrossAllSlots_RejectsWithDistinctErrorMessage()
    {
        var engine = new PhoCookingEngine();

        // Fill all 5 slots with BanhPho
        for (int i = 0; i < PhoCookingEngine.TotalSlots; i++)
        {
            engine.PlaceIngredient(i, ItemIds.BanhPho);
        }

        bool success = engine.ValidateAssembly(out string error);
        Assert.False(success);
        Assert.Contains("riêng biệt", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void PhoAssembly_DuplicateIngredientInTwoSlots_RejectsWithDistinctErrorMessage()
    {
        var engine = new PhoCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.BanhPho); // Duplicate of slot 0

        bool success = engine.ValidateAssembly(out string error);
        Assert.False(success);
        Assert.Contains("riêng biệt", error);
        Assert.False(engine.IsSolved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PhoAssembly_PrematureValidation_MissingSlots_FailsGracefully(int filledCount)
    {
        var engine = new PhoCookingEngine();
        string[] validSequence = [ItemIds.BanhPho, ItemIds.ThitBo, ItemIds.HanhLa, ItemIds.NuocDung, ItemIds.ThaoMoc];

        for (int i = 0; i < filledCount; i++)
        {
            engine.PlaceIngredient(i, validSequence[i]);
        }

        bool success = engine.ValidateAssembly(out string error);
        Assert.False(success);
        Assert.Contains("thiếu nguyên liệu", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void PhoAssembly_InvalidGarnish_WithDistinctItems_RejectsAtFinalStep()
    {
        var engine = new PhoCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, "Item_UnknownGarnish"); // Unknown garnish

        bool success = engine.ValidateAssembly(out string error);
        Assert.False(success);
        Assert.Contains("hạt tiêu Bắc hoặc hương thảo mộc", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void PhoAssembly_CyclicalAndReversedPermutations_AllReject()
    {
        var engine = new PhoCookingEngine();
        string[] canonical = [ItemIds.BanhPho, ItemIds.ThitBo, ItemIds.HanhLa, ItemIds.NuocDung, ItemIds.ThaoMoc];

        // 1. Reversed sequence: [ThaoMoc, NuocDung, HanhLa, ThitBo, BanhPho]
        var reversed = canonical.Reverse().ToArray();
        for (int i = 0; i < 5; i++) engine.PlaceIngredient(i, reversed[i]);
        Assert.False(engine.ValidateAssembly(out _));

        // 2. Shift by 1: [ThitBo, HanhLa, NuocDung, ThaoMoc, BanhPho]
        engine.ResetPuzzle();
        for (int i = 0; i < 5; i++) engine.PlaceIngredient(i, canonical[(i + 1) % 5]);
        Assert.False(engine.ValidateAssembly(out _));

        // 3. Shift by 2: [HanhLa, NuocDung, ThaoMoc, BanhPho, ThitBo]
        engine.ResetPuzzle();
        for (int i = 0; i < 5; i++) engine.PlaceIngredient(i, canonical[(i + 2) % 5]);
        Assert.False(engine.ValidateAssembly(out _));
    }

    [Fact]
    public void PhoAssembly_ResetPurity_AllowsFreshSolveAfterFailureOrSuccess()
    {
        var engine = new PhoCookingEngine();

        // First attempt: complete canonical solve
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);
        Assert.True(engine.ValidateAssembly(out _));
        Assert.True(engine.IsSolved);

        // Reset
        engine.ResetPuzzle();
        Assert.False(engine.IsSolved);
        for (int i = 0; i < PhoCookingEngine.TotalSlots; i++)
        {
            Assert.Null(engine.CurrentSlots[i]);
        }

        // Second attempt: solve with alternative garnish
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.GiaVi);
        Assert.True(engine.ValidateAssembly(out _));
        Assert.True(engine.IsSolved);
    }

    [Fact]
    public void PhoAssemblyScene_InvalidCardPlacements_IgnoredGracefully()
    {
        var eventBus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(questManager);

        var scene = new PhoAssemblyScene(saveData, questManager, eventBus);
        scene.Enter();

        // Place card with out-of-range slot or card index
        scene.PlaceCard(-1, 0);
        scene.PlaceCard(5, 0);
        scene.PlaceCard(0, -1);
        scene.PlaceCard(0, 99);

        // Slots should remain untouched
        for (int i = 0; i < 5; i++)
        {
            Assert.Null(scene.CurrentSlots[i]);
        }

        // Validate on empty scene
        bool valid = scene.Validate();
        Assert.False(valid);
        Assert.False(scene.IsSolved);
        Assert.NotNull(scene.FeedbackMessage);
    }

    // =========================================================================
    // SECTION 2: SPATIAL GRID BFS & PUSH MECHANICS ADVERSARIAL CHALLENGES
    // =========================================================================

    [Theory]
    [InlineData("Wall_Top_0", Direction.Up)]
    [InlineData("Wall_Top_0", Direction.Down)]
    [InlineData("Wall_Top_0", Direction.Left)]
    [InlineData("Wall_Top_0", Direction.Right)]
    [InlineData("Wall_Bottom_6", Direction.Up)]
    [InlineData("Wall_Bottom_6", Direction.Down)]
    [InlineData("Wall_Bottom_6", Direction.Left)]
    [InlineData("Wall_Bottom_6", Direction.Right)]
    public void SpatialGrid_PushStaticWall_AlwaysReturnsFalse(string wallId, Direction dir)
    {
        var grid = new SpatialGridEngine();
        bool pushed = grid.PushObject(wallId, dir);
        Assert.False(pushed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Obj_NonExistent")]
    public void SpatialGrid_PushNonExistentEntity_ReturnsFalse(string? invalidId)
    {
        var grid = new SpatialGridEngine();
        bool pushed = grid.PushObject(invalidId!, Direction.Up);
        Assert.False(pushed);
    }

    [Fact]
    public void SpatialGrid_DirectionalLockConstraints_AreStrictlyEnforced()
    {
        var grid = new SpatialGridEngine();

        // Xe hoa (Obj_FlowerCart) is vertical only
        Assert.False(grid.PushObject("Obj_FlowerCart", Direction.Left));
        Assert.False(grid.PushObject("Obj_FlowerCart", Direction.Right));

        // Xich lo (Obj_Cyclo) is horizontal only
        Assert.False(grid.PushObject("Obj_Cyclo", Direction.Up));
        Assert.False(grid.PushObject("Obj_Cyclo", Direction.Down));
    }

    [Fact]
    public void SpatialGrid_PushOntoOccupiedCells_IsPrevented()
    {
        var grid = new SpatialGridEngine();

        // Obj_FlowerCart is at (2, 1)-(2, 2). Obj_FruitStall is at (2, 3).
        // Pushing FlowerCart DOWN into FruitStall must fail.
        Assert.False(grid.PushObject("Obj_FlowerCart", Direction.Down));

        // Pushing Cyclo (3, 2)-(4, 2) LEFT into FlowerCart (2, 2) must fail.
        Assert.False(grid.PushObject("Obj_Cyclo", Direction.Left));

        // Pushing Cyclo (3, 2)-(4, 2) RIGHT into TeaStall (5, 2) must fail.
        Assert.False(grid.PushObject("Obj_Cyclo", Direction.Right));

        // Pushing FruitStall (2, 3) UP into FlowerCart (2, 2) must fail.
        Assert.False(grid.PushObject("Obj_FruitStall", Direction.Up));

        // Pushing FruitStall (2, 3) RIGHT into CeramicCrates (3, 3) must fail.
        Assert.False(grid.PushObject("Obj_FruitStall", Direction.Right));
    }

    [Fact]
    public void SpatialGrid_BoundaryStalls_CannotPushOffGridOrIntoCurbs()
    {
        var grid = new SpatialGridEngine();

        // Fruit stall at (2, 3). Push LEFT to (1, 3).
        Assert.True(grid.PushObject("Obj_FruitStall", Direction.Left));
        // Push LEFT to (0, 3).
        Assert.True(grid.PushObject("Obj_FruitStall", Direction.Left));

        // Now at (0, 3):
        // Pushing LEFT hits x < 0 boundary
        Assert.False(grid.PushObject("Obj_FruitStall", Direction.Left));

        // Pushing DOWN hits Wall_Bottom_0 at y = 4
        Assert.False(grid.PushObject("Obj_FruitStall", Direction.Down));

        var fruit = grid.Entities.First(e => e.Id == "Obj_FruitStall");
        Assert.Equal(0, fruit.X);
        Assert.Equal(3, fruit.Y);
    }

    [Fact]
    public void SpatialGrid_MoveOscillationStress_MaintainsCoordinateSync()
    {
        var grid = new SpatialGridEngine();

        // Fruit stall starts at (2, 3). Move LEFT to (1, 3), then oscillate 500 times.
        for (int i = 0; i < 500; i++)
        {
            bool left = grid.PushObject("Obj_FruitStall", Direction.Left);
            Assert.True(left);
            var curr = grid.Entities.First(e => e.Id == "Obj_FruitStall");
            Assert.Equal(1, curr.X);
            Assert.Equal(3, curr.Y);

            bool right = grid.PushObject("Obj_FruitStall", Direction.Right);
            Assert.True(right);
            curr = grid.Entities.First(e => e.Id == "Obj_FruitStall");
            Assert.Equal(2, curr.X);
            Assert.Equal(3, curr.Y);
        }

        // Final position must match starting position exactly
        var finalFruit = grid.Entities.First(e => e.Id == "Obj_FruitStall");
        Assert.Equal(2, finalFruit.X);
        Assert.Equal(3, finalFruit.Y);
        Assert.False(grid.IsPathClear());
    }

    [Fact]
    public void SpatialGrid_BFSPath_StrictMonotonicConnectedness_NoDiagonalJumps()
    {
        var grid = new SpatialGridEngine();

        // Solve puzzle: FruitStall LEFT, FlowerCart DOWN
        Assert.True(grid.PushObject("Obj_FruitStall", Direction.Left));
        Assert.True(grid.PushObject("Obj_FlowerCart", Direction.Down));
        Assert.True(grid.IsPathClear());

        var path = grid.FindPath();
        Assert.NotNull(path);
        Assert.NotEmpty(path);

        // 1. Path must begin at Entrance (0, 2) and end at ExitGate (6, 2)
        Assert.Equal(grid.Entrance, path[0]);
        Assert.Equal(grid.ExitGate, path[^1]);

        // 2. Strict 4-connectedness check: every step must have Manhattan distance == 1
        for (int i = 0; i < path.Count - 1; i++)
        {
            var p1 = path[i];
            var p2 = path[i + 1];
            int manhattanDist = Math.Abs(p2.x - p1.x) + Math.Abs(p2.y - p1.y);
            Assert.True(manhattanDist == 1,
                $"Step from ({p1.x}, {p1.y}) to ({p2.x}, {p2.y}) is non-monotonic or diagonal (distance: {manhattanDist})");
        }

        // 3. No cycles/duplicates in shortest path
        Assert.Equal(path.Count, path.Distinct().Count());

        // 4. All nodes must be strictly within grid bounds and NOT hit occupied cells
        var occupied = new HashSet<(int x, int y)>();
        foreach (var entity in grid.Entities)
        {
            foreach (var c in entity.OccupiedCells)
            {
                occupied.Add(c);
            }
        }

        foreach (var step in path)
        {
            Assert.InRange(step.x, 0, grid.Width - 1);
            Assert.InRange(step.y, 0, grid.Height - 1);
            Assert.DoesNotContain(step, occupied);
        }
    }

    [Fact]
    public void SpatialGrid_BlockedExitGate_FindPathReturnsNull()
    {
        var grid = new SpatialGridEngine();

        // First solve the puzzle
        grid.PushObject("Obj_FruitStall", Direction.Left);
        grid.PushObject("Obj_FlowerCart", Direction.Down);
        Assert.True(grid.IsPathClear());

        // Now push Obj_TeaStall from (5, 2) RIGHT to (6, 2) (directly blocking the ExitGate)
        bool pushedExit = grid.PushObject("Obj_TeaStall", Direction.Right);
        Assert.True(pushedExit);

        var teaStall = grid.Entities.First(e => e.Id == "Obj_TeaStall");
        Assert.Equal(grid.ExitGate, (teaStall.X, teaStall.Y));

        // When ExitGate is occupied, FindPath must return null and IsPathClear must be false
        Assert.Null(grid.FindPath());
        Assert.False(grid.IsPathClear());
    }

    [Fact]
    public void SpatialGrid_ResetGrid_RestoresEntitiesCleanlyAfterMultipleMoves()
    {
        var grid = new SpatialGridEngine();

        // Execute several valid moves
        grid.PushObject("Obj_FruitStall", Direction.Left);
        grid.PushObject("Obj_FlowerCart", Direction.Down);
        grid.PushObject("Obj_FruitStall", Direction.Left);

        // Reset
        grid.ResetGrid();

        // Verify all entities match initial state
        var flower = grid.Entities.First(e => e.Id == "Obj_FlowerCart");
        Assert.Equal(2, flower.X);
        Assert.Equal(1, flower.Y);

        var fruit = grid.Entities.First(e => e.Id == "Obj_FruitStall");
        Assert.Equal(2, fruit.X);
        Assert.Equal(3, fruit.Y);

        var cyclo = grid.Entities.First(e => e.Id == "Obj_Cyclo");
        Assert.Equal(3, cyclo.X);
        Assert.Equal(2, cyclo.Y);

        Assert.False(grid.IsPathClear());
        Assert.Null(grid.FindPath());
    }

    // =========================================================================
    // SECTION 3: MODAL STATE OVERLAYS & LEVEL CLIMAX LIFECYCLE
    // =========================================================================

    [Fact]
    public void GameStateManager_RapidModalTransitions_MaintainsStackPurity()
    {
        var bus = new EventBus();
        var sm = new GameStateManager(bus, GameState.Exploration);

        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // Rapid push & pop sequence
        sm.PushState(GameState.MiniGamePho);
        Assert.Equal(GameState.MiniGamePho, sm.CurrentState);

        sm.PopState();
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        sm.PushState(GameState.MiniGameStalls);
        Assert.Equal(GameState.MiniGameStalls, sm.CurrentState);

        sm.PopState();
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        sm.PushState(GameState.Cutscene);
        Assert.Equal(GameState.Cutscene, sm.CurrentState);

        sm.PopState();
        Assert.Equal(GameState.Exploration, sm.CurrentState);
    }

    [Fact]
    public void HanoiExplorationScene_MiniGamePhoOverlay_PauseAndResumeKinematics()
    {
        var saveData = SaveData.CreateDefault();
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var input = new MockInput();
        var sm = new GameStateManager(bus, GameState.Exploration);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            stateManager: sm,
            inputService: input,
            audioService: new NullAudio(),
            eventBus: bus
        );

        scene.Enter();
        var initialPos = scene.Kinematics.Position;

        // Push Pho mini-game state
        sm.PushState(GameState.MiniGamePho);
        Assert.Equal(GameState.MiniGamePho, sm.CurrentState);

        // Send movement input while in mini-game
        input.MovementDirection = new Vector2(1, 0);
        scene.Update(0.1f);

        // Position must NOT change because movement is paused during modal overlay
        Assert.Equal(initialPos, scene.Kinematics.Position);

        // Pop state back to exploration
        sm.PopState();
        Assert.Equal(GameState.Exploration, sm.CurrentState);

        // Now movement input should take effect
        scene.Update(0.1f);
        Assert.True(scene.Kinematics.Position.X > initialPos.X);
    }

    [Fact]
    public void StallPuzzleScene_DirectSolve_ProgressionAndLetterModalLifecycle()
    {
        var eventBus = new EventBus();
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(questManager);

        var scene = new StallPuzzleScene(saveData, questManager, eventBus);
        scene.Enter();

        Assert.False(scene.IsSolved);
        Assert.Equal(0, scene.MoveCount);

        // Push 1: Fruit Stall Left
        Assert.True(scene.Push("Obj_FruitStall", Direction.Left));
        Assert.Equal(1, scene.MoveCount);
        Assert.False(scene.IsSolved);

        // Push 2: Flower Cart Down -> Triggers solve
        Assert.True(scene.Push("Obj_FlowerCart", Direction.Down));
        Assert.Equal(2, scene.MoveCount);
        Assert.True(scene.IsSolved);

        // Verify domain progression side-effects
        Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(saveData.Level1State.QuestFlags.Q3_Completed);
        Assert.Equal(100.0f, saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHanoi));
        Assert.True(saveData.CollectionAlbum.HasJournalPage(AlbumIds.HanoiJournalPage));
        Assert.True(saveData.CollectionAlbum.HasStamp(AlbumIds.HanoiLotusStamp));
        Assert.True(saveData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHue));

        // Advance timer through solve animation (3.0s activates letter modal)
        scene.Update(3.1f);

        // Advance through letter typing and dismiss
        scene.Update(1.0f);
        Assert.False(scene.RequestNextScene);

        // Fast forward 15s to complete typewriter and confirm
        for (int i = 0; i < 30; i++)
        {
            scene.Update(0.5f);
        }
    }

    [Fact]
    public void ExplorationScene_LongBienGate_CollisionRemovedWhenUnlocked()
    {
        var saveData = SaveData.CreateDefault();
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        var scene = new HanoiExplorationScene(
            saveData: saveData,
            questManager: qm,
            audioService: new NullAudio(),
            eventBus: bus
        );

        scene.Enter();

        // 1. Initially locked: gate collider is registered at x=1480, y=400
        Assert.False(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        var gateColliderBefore = scene.CollisionWorld.Obstacles
            .FirstOrDefault(o => o.Overlaps(new AABB(1460, 300, 1500, 500)));
        Assert.NotEqual(default, gateColliderBefore);

        // 2. Unlock gate via progression
        var progression = new LevelProgressionService(saveData, bus);
        progression.CompleteLevel1();
        Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);

        // Re-enter or update obstacles
        scene.Enter();
        var gateColliderAfter = scene.CollisionWorld.Obstacles
            .FirstOrDefault(o => o.Overlaps(new AABB(1460, 300, 1500, 500)));
        Assert.Equal(default, gateColliderAfter);
    }
}
