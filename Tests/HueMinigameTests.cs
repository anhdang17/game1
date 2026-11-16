using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Stages;
using Xunit;

namespace GameProject.Tests;

public class HueMinigameTests
{
    #region BunBoHueCookingEngine Tests

    [Fact]
    public void CookingEngine_CorrectAuthenticSequence_ValidatesSuccessfully()
    {
        var engine = new BunBoHueCookingEngine();

        // 1. Thick rice noodles
        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        // 2. Beef shank
        engine.PlaceIngredient(1, ItemIds.BapBo);
        // 3. Crab cakes
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        // 4. Fresh herbs & banana blossom
        engine.PlaceIngredient(3, ItemIds.RauSongHue);
        // 5. Scalding broth
        engine.PlaceIngredient(4, ItemIds.NuocDungHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.True(success);
        Assert.Empty(error);
        Assert.True(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_IncompleteBowl_ReturnsMissingIngredientsError()
    {
        var engine = new BunBoHueCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.BapBo);
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        // Slots 3 and 4 empty

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("thiếu nguyên liệu", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_BrothPlacedFirst_ReturnsContextualAdviceFromButChi()
    {
        var engine = new BunBoHueCookingEngine();

        // Broth poured in slot 0 before noodles
        engine.PlaceIngredient(0, ItemIds.NuocDungHue);
        engine.PlaceIngredient(1, ItemIds.BunSoiTo);
        engine.PlaceIngredient(2, ItemIds.BapBo);
        engine.PlaceIngredient(3, ItemIds.ChaCua);
        engine.PlaceIngredient(4, ItemIds.RauSongHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("Nước dùng mà chan trước khi cho bún", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_NoodlesMissingFromBase_ReturnsBaseGuidance()
    {
        var engine = new BunBoHueCookingEngine();

        // Beef placed at slot 0
        engine.PlaceIngredient(0, ItemIds.BapBo);
        engine.PlaceIngredient(1, ItemIds.BunSoiTo);
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        engine.PlaceIngredient(3, ItemIds.RauSongHue);
        engine.PlaceIngredient(4, ItemIds.NuocDungHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("Đặt bún sợi to làng Vân Cù", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_CrabPlacedBeforeBeef_ReturnsOrderingAdvice()
    {
        var engine = new BunBoHueCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.ChaCua); // Crab placed before beef
        engine.PlaceIngredient(2, ItemIds.BapBo);
        engine.PlaceIngredient(3, ItemIds.RauSongHue);
        engine.PlaceIngredient(4, ItemIds.NuocDungHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("xếp từng lát bắp bò hoa thơm mềm lên trên lớp bún trước", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_Step3NotCrab_ReturnsCrabGuidance()
    {
        var engine = new BunBoHueCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.BapBo);
        engine.PlaceIngredient(2, ItemIds.RauSongHue); // Herbs placed before crab
        engine.PlaceIngredient(3, ItemIds.ChaCua);
        engine.PlaceIngredient(4, ItemIds.NuocDungHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("chả cua", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_Step4NotHerbs_ReturnsHerbsGuidance()
    {
        var engine = new BunBoHueCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.BapBo);
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        engine.PlaceIngredient(3, ItemIds.NuocDungHue); // Broth before herbs
        engine.PlaceIngredient(4, ItemIds.RauSongHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("rau sống", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_DuplicateIngredients_FailsWithUniquenessMessage()
    {
        var engine = new BunBoHueCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.BunSoiTo); // Duplicate noodles
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        engine.PlaceIngredient(3, ItemIds.RauSongHue);
        engine.PlaceIngredient(4, ItemIds.NuocDungHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("riêng biệt", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_InputBoundariesAndRemoval()
    {
        var engine = new BunBoHueCookingEngine();

        Assert.False(engine.PlaceIngredient(-1, ItemIds.BunSoiTo));
        Assert.False(engine.PlaceIngredient(5, ItemIds.BunSoiTo));
        Assert.False(engine.PlaceIngredient(0, ""));
        Assert.False(engine.PlaceIngredient(0, "   "));

        Assert.True(engine.PlaceIngredient(0, ItemIds.BunSoiTo));
        Assert.Equal(ItemIds.BunSoiTo, engine.CurrentSlots[0]);

        Assert.True(engine.RemoveIngredient(0));
        Assert.Null(engine.CurrentSlots[0]);
        Assert.False(engine.RemoveIngredient(-1));
        Assert.False(engine.RemoveIngredient(5));
    }

    [Fact]
    public void CookingEngine_Step2NotBeef_ReturnsFallbackGuidance()
    {
        var engine = new BunBoHueCookingEngine();

        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.RauSongHue); // Neither beef shank nor crab cake
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        engine.PlaceIngredient(3, ItemIds.BapBo);
        engine.PlaceIngredient(4, ItemIds.NuocDungHue);

        bool success = engine.ValidateAssembly(out string error);

        Assert.False(success);
        Assert.Contains("bắp bò hoa tươi ngon", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void CookingEngine_CurrentSlots_IsEncapsulatedReadOnlyCollection()
    {
        var engine = new BunBoHueCookingEngine();
        engine.PlaceIngredient(0, ItemIds.BunSoiTo);

        // Verification 1: CurrentSlots is encapsulated, not the raw mutable array
        Assert.False(engine.CurrentSlots is string?[]);

        // Verification 2: Attempting to mutate via IList interface throws NotSupportedException
        var list = engine.CurrentSlots as IList<string?>;
        Assert.NotNull(list);
        Assert.Throws<NotSupportedException>(() => list[0] = "Malicious_Override");

        // Verification 3: Internal engine slot remains untouched
        Assert.Equal(ItemIds.BunSoiTo, engine.CurrentSlots[0]);
    }

    [Fact]
    public void CookingEngine_ResetPuzzle_ClearsAllSlotsAndSolvedFlag()
    {
        var engine = new BunBoHueCookingEngine();

        for (int i = 0; i < 5; i++)
        {
            engine.PlaceIngredient(i, BunBoHueCookingEngine.CorrectSequence[i]);
        }
        Assert.True(engine.ValidateAssembly(out _));
        Assert.True(engine.IsSolved);

        engine.ResetPuzzle();

        Assert.False(engine.IsSolved);
        Assert.All(engine.CurrentSlots, Assert.Null);
    }

    #endregion

    #region HoaDangRiverEngine Tests

    [Fact]
    public void RiverEngine_InitialState_PathIsBlocked()
    {
        var grid = new HoaDangRiverEngine();

        Assert.False(grid.IsPathClear());
        Assert.Null(grid.FindPath());
        Assert.Equal((0, 2), grid.Entrance);
        Assert.Equal((6, 2), grid.ExitGate);
        Assert.True(grid.Entities.Count >= 7);
    }

    [Fact]
    public void RiverEngine_DragonBoat_CanOnlyMoveVertically()
    {
        var grid = new HoaDangRiverEngine();

        // Royal Dragon Boat is at x=2, y=1 (size 1x2, occupies (2, 1) and (2, 2))
        // CanPushHorizontally = false, CanPushVertically = true
        // Horizontal moves must fail
        Assert.False(grid.PushObject("Obj_DragonBoat_1", Direction.Left));
        Assert.False(grid.PushObject("Obj_DragonBoat_1", Direction.Right));
        Assert.False(grid.PushObject("Obj_DragonBoat", Direction.Left));
        Assert.False(grid.PushObject("Obj_DragonBoat", Direction.Right));

        // Pushing DOWN fails while Fruit Boat / Fisher Sampan occupies (2, 3)
        Assert.False(grid.PushObject("Obj_DragonBoat_1", Direction.Down));

        // Move Fruit Boat Left out of cell (2, 3)
        Assert.True(grid.PushObject("Obj_FruitBoat_1", Direction.Left));

        // Now vertical push DOWN into free cell (y=2) succeeds
        Assert.True(grid.PushObject("Obj_DragonBoat_1", Direction.Down));
        var boat = grid.Entities.First(e => HoaDangRiverEngine.MatchesId(e.Id, "Obj_DragonBoat"));
        Assert.Equal(2, boat.Y);
    }

    [Fact]
    public void RiverEngine_FlowerRaft_CanOnlyMoveHorizontally()
    {
        var grid = new HoaDangRiverEngine();

        // Flower Raft is at x=3, y=2 (size 2x1, occupies (3, 2) and (4, 2))
        // CanPushHorizontally = true, CanPushVertically = false
        // Vertical moves must fail
        Assert.False(grid.PushObject("Obj_FlowerRaft_1", Direction.Up));
        Assert.False(grid.PushObject("Obj_FlowerRaft_1", Direction.Down));
        Assert.False(grid.PushObject("Obj_FlowerRaft", Direction.Up));
        Assert.False(grid.PushObject("Obj_FlowerRaft", Direction.Down));
    }

    [Fact]
    public void RiverEngine_BoundaryCollisions_CannotPushIntoPiersOrOutside()
    {
        var grid = new HoaDangRiverEngine();

        // Top stone pier embankment is at y=0. Pushing Dragon Boat UP into pier must fail
        Assert.False(grid.PushObject("Obj_DragonBoat_1", Direction.Up));

        // Bottom stone pier embankment is at y=4. Pushing Fisher Sampan DOWN must fail
        Assert.False(grid.PushObject("Obj_FruitBoat_1", Direction.Down));
    }

    [Fact]
    public void RiverEngine_SolvingGrid_ClearsContinuousBFSPathFromCitadelPierToSacredWaters()
    {
        var grid = new HoaDangRiverEngine();
        Assert.False(grid.IsPathClear());

        // Step 1: Push Fruit Boat / Fisher Sampan (1x1 at x=2, y=3) LEFT to (1, 3)
        bool m1 = grid.PushObject("Obj_FruitBoat_1", Direction.Left);
        Assert.True(m1);

        // Step 2: Push Dragon Boat (1x2 at x=2, y=1) DOWN to y=2
        bool m2 = grid.PushObject("Obj_DragonBoat_1", Direction.Down);
        Assert.True(m2);

        // Continuous path from Citadel Pier (0, 2) to Sacred Waters (6, 2) is now open!
        Assert.True(grid.IsPathClear());
        var path = grid.FindPath();
        Assert.NotNull(path);
        Assert.Equal((0, 2), path.First());
        Assert.Equal((6, 2), path.Last());
    }

    [Fact]
    public void RiverEngine_InvalidTargetOrStaticWall_ReturnsFalse()
    {
        var grid = new HoaDangRiverEngine();

        Assert.False(grid.PushObject("NonExistentId", Direction.Up));
        Assert.False(grid.PushObject("Pier_North_0", Direction.Down));
        Assert.False(grid.PushObject("Pier_South_3", Direction.Up));
    }

    [Fact]
    public void RiverEngine_NullAndEmptyInputs_ReturnFalseGracefully()
    {
        var grid = new HoaDangRiverEngine();

        // PushObject with null, empty, or whitespace objectId returns false without throwing
        Assert.False(grid.PushObject(null!, Direction.Up));
        Assert.False(grid.PushObject("", Direction.Up));
        Assert.False(grid.PushObject("   ", Direction.Down));

        // MatchesId with null, empty, or whitespace inputs returns false without throwing
        Assert.False(HoaDangRiverEngine.MatchesId(null!, "Obj_DragonBoat"));
        Assert.False(HoaDangRiverEngine.MatchesId("Obj_DragonBoat", null!));
        Assert.False(HoaDangRiverEngine.MatchesId(null!, null!));
        Assert.False(HoaDangRiverEngine.MatchesId("", "Obj_DragonBoat"));
        Assert.False(HoaDangRiverEngine.MatchesId("Obj_DragonBoat", ""));
        Assert.False(HoaDangRiverEngine.MatchesId("", ""));
        Assert.False(HoaDangRiverEngine.MatchesId("   ", "   "));
    }

    [Fact]
    public void RiverEngine_UntangledAliases_MatchCorrectEntitiesStrictly()
    {
        Assert.True(HoaDangRiverEngine.MatchesId("Obj_DragonBoat_1", "Obj_DragonBoat"));
        Assert.True(HoaDangRiverEngine.MatchesId("Obj_DragonBoat", "Obj_DragonBoat_1"));
        Assert.True(HoaDangRiverEngine.MatchesId("Obj_FlowerRaft_1", "Obj_FlowerRaft"));
        Assert.True(HoaDangRiverEngine.MatchesId("Obj_FlowerRaft", "Obj_FlowerRaft_1"));
        Assert.True(HoaDangRiverEngine.MatchesId("Obj_FruitBoat_1", "Obj_FisherSampan"));
        Assert.True(HoaDangRiverEngine.MatchesId("Obj_LotusCluster_1", "Obj_WaterLilyPatch"));

        // Cross-aliasing strictly rejected
        Assert.False(HoaDangRiverEngine.MatchesId("Obj_DragonBoat_1", "Obj_FlowerRaft"));
        Assert.False(HoaDangRiverEngine.MatchesId("Obj_FlowerRaft_1", "Obj_DragonBoat"));
        Assert.False(HoaDangRiverEngine.MatchesId("Obj_FruitBoat_1", "Obj_DragonBoat"));
        Assert.False(HoaDangRiverEngine.MatchesId("Obj_LotusCluster_1", "Obj_FlowerRaft"));
    }

    [Fact]
    public void RiverEngine_ResetGrid_RestoresStartingCoordinates()
    {
        var grid = new HoaDangRiverEngine();

        grid.PushObject("Obj_FruitBoat_1", Direction.Left);
        grid.PushObject("Obj_DragonBoat_1", Direction.Down);
        Assert.True(grid.IsPathClear());

        grid.ResetGrid();

        Assert.False(grid.IsPathClear());
        var sampan = grid.Entities.First(e => HoaDangRiverEngine.MatchesId(e.Id, "Obj_FruitBoat_1"));
        Assert.Equal(2, sampan.X);
        Assert.Equal(3, sampan.Y);

        var boat = grid.Entities.First(e => HoaDangRiverEngine.MatchesId(e.Id, "Obj_DragonBoat_1"));
        Assert.Equal(2, boat.X);
        Assert.Equal(1, boat.Y);
    }

    #endregion

    #region Scene Lifecycle Tests

    private static void CompleteQuest1(QuestManager questManager)
    {
        questManager.StartQuest(HueQuests.Quest1Id);
        questManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.BunSoiTo, 1);
        questManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.BapBo, 1);
        questManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.ChaCua, 1);
        questManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.NuocDungHue, 1);
        questManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.RauSongHue, 1);
        questManager.CompleteQuest(HueQuests.Quest1Id);
    }

    private static void CompleteQuest2(QuestManager questManager)
    {
        CompleteQuest1(questManager);
        questManager.StartQuest(HueQuests.Quest2Id);
        questManager.RecordObjectiveProgress(HueQuests.Quest2Id, "cook_bun_bo", 1);
        questManager.CompleteQuest(HueQuests.Quest2Id);
    }

    [Fact]
    public void BunBoHueAssemblyScene_Lifecycle_EnterSolveAndCompleteQuest()
    {
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager();
        HueQuests.RegisterAll(questManager);
        var eventBus = new EventBus();

        // Satisfy prerequisite Quest 1
        CompleteQuest1(questManager);

        var scene = new BunBoHueAssemblyScene(saveData, questManager, eventBus);

        // 1. Enter
        scene.Enter();
        Assert.False(scene.IsSolved);
        Assert.False(scene.RequestExit);
        Assert.False(scene.RequestNextScene);
        Assert.NotNull(scene.FeedbackMessage);
        Assert.Contains("Bút Chì", scene.FeedbackMessage);
        Assert.Equal(QuestState.InProgress, questManager.GetQuest(HueQuests.Quest2Id)?.State);

        // 2. Place 5 ingredients in authentic sequence
        for (int i = 0; i < 5; i++)
        {
            scene.PlaceCard(i, i);
        }

        // 3. Validate
        bool solved = scene.Validate();
        Assert.True(solved);
        Assert.True(scene.IsSolved);
        Assert.True(saveData.QuestFlags.Hue_Q2_BunBoCooked);
        Assert.True(saveData.QuestFlags.Hue_Q2_Completed);
        Assert.Equal(QuestState.Completed, questManager.GetQuest(HueQuests.Quest2Id)?.State);
        Assert.Equal(33.3f, saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue), 1);

        // 4. Update transitions
        scene.Update(1.5f);
        Assert.False(scene.RequestExit);
        scene.Update(1.5f);
        Assert.True(scene.RequestExit);
        Assert.True(scene.RequestNextScene);

        // 5. Draw headless safety
        scene.Draw();
    }

    [Fact]
    public void BunBoHueAssemblyScene_Reset_ClearsSlotsAndFeedback()
    {
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager();
        HueQuests.RegisterAll(questManager);

        var scene = new BunBoHueAssemblyScene(saveData, questManager);
        scene.Enter();

        scene.PlaceCard(0, 0);
        Assert.Equal(ItemIds.BunSoiTo, scene.CurrentSlots[0]);

        scene.Reset();
        Assert.Null(scene.CurrentSlots[0]);
        Assert.Contains("Bắt đầu lại", scene.FeedbackMessage);
    }

    [Fact]
    public void HoaDangRiverScene_Lifecycle_EnterSolveAndCompleteHueClimax()
    {
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager();
        HueQuests.RegisterAll(questManager);
        var eventBus = new EventBus();

        // Satisfy prerequisite Quests 1 and 2
        CompleteQuest2(questManager);

        var scene = new HoaDangRiverScene(saveData, questManager, eventBus);

        // 1. Enter
        scene.Enter();
        Assert.False(scene.IsSolved);
        Assert.False(scene.RequestExit);
        Assert.False(scene.RequestNextScene);
        Assert.NotNull(scene.HintMessage);
        Assert.Equal(QuestState.InProgress, questManager.GetQuest(HueQuests.Quest3Id)?.State);

        // 2. Push obstacles to clear river channel
        bool m1 = scene.Push("Obj_FisherSampan", Direction.Left);
        Assert.True(m1);
        Assert.Equal(1, scene.MoveCount);
        Assert.False(scene.IsSolved);

        bool m2 = scene.Push("Obj_DragonBoat_1", Direction.Down);
        Assert.True(m2);
        Assert.Equal(2, scene.MoveCount);

        // 3. Puzzle automatically solves and completes Hue level
        Assert.True(scene.IsSolved);
        Assert.True(saveData.QuestFlags.Hue_Q3_PuzzleSolved);
        Assert.True(saveData.QuestFlags.Hue_Q3_Completed);
        Assert.True(saveData.QuestFlags.Gate_HoiAn_Unlocked);
        Assert.True(saveData.QuestFlags.Hue_Finished);
        Assert.Equal(QuestState.Completed, questManager.GetQuest(HueQuests.Quest3Id)?.State);

        // 4. Progression & Rewards
        Assert.True(saveData.WorldProgression.IsRegionCompleted(WorldProgression.RegionHue));
        Assert.True(saveData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));
        Assert.Equal(100.0f, saveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));
        Assert.Contains(AlbumService.HuePage2.Id, saveData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumService.HueStamp.Id, saveData.CollectionAlbum.CollectedStamps);

        // 5. Update transitions
        scene.Update(2.0f);
        Assert.False(scene.RequestNextScene);
        scene.Update(2.0f);
        Assert.True(scene.RequestNextScene);
        Assert.True(scene.RequestExit);

        // 6. Draw headless safety
        scene.Draw();
    }

    [Fact]
    public void HoaDangRiverScene_ResetGrid_RestoresInitialState()
    {
        var saveData = SaveData.CreateDefault();
        var questManager = new QuestManager();
        HueQuests.RegisterAll(questManager);

        var scene = new HoaDangRiverScene(saveData, questManager);
        scene.Enter();

        scene.Push("Obj_FisherSampan", Direction.Left);
        Assert.Equal(1, scene.MoveCount);

        scene.ResetGrid();
        Assert.Equal(0, scene.MoveCount);
        Assert.False(scene.IsSolved);
    }

    #endregion
}
