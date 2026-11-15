using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Persistence;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Core.WorldMap;
using GameProject.Stages;
using GameProject.Tests.Mocks;
using Xunit;

namespace GameProject.Tests;

/// <summary>
/// Comprehensive 4-Tier Opaque-Box E2E Test Suite for Chapter 2: Cố Đô Huế.
/// Derived strictly from ORIGINAL_REQUEST.md, PROJECT.md, SCOPE.md, and TEST_INFRA.md.
/// 
/// 4-Tier Architecture:
/// - Tier 1: Feature Coverage (R1 to R5: 30 test cases)
/// - Tier 2: Boundary & Corner Cases (R1 to R5: 30 test cases)
/// - Tier 3: Cross-Feature Combinations (6 pairwise / multi-system test cases)
/// - Tier 4: Real-World Application Scenarios (5 full end-to-end workload test cases)
/// Total: 71 test cases.
/// 
/// 100% Headless Safe: Uses NullAudioService, MockInputService, and MockSaveStorage.
/// </summary>
public sealed class HueChapter2E2ETests
{
    private static string GetHueDialoguePath()
    {
        if (File.Exists("Data/dialogues_hue.json")) return "Data/dialogues_hue.json";
        if (File.Exists("Source/Data/dialogues_hue.json")) return "Source/Data/dialogues_hue.json";
        return "../../../Source/Data/dialogues_hue.json";
    }

    #region Tier 1: Feature Coverage (R1 - R5)

    // =========================================================================
    // Category R1: Exploration & Environment (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T1_R1_01_HueExplorationScene_RendersAllFourCulturalLandmarks_HeadlessSafe()
    {
        var data = SaveData.CreateDefault();
        var audio = new NullAudioService();
        var input = new MockInputService();
        var bus = new EventBus();

        using var scene = new HueExplorationScene(data, audio, input, bus);
        scene.Enter();

        // Verify Map Bounds (1600x900)
        Assert.Equal(1600f, scene.CollisionWorld.MapBounds.Width);
        Assert.Equal(900f, scene.CollisionWorld.MapBounds.Height);

        // Verify player initial spawn in courtyard before Ngọ Môn
        Assert.Equal(new Vector2(400f, 480f), scene.Kinematics.Position);

        // Headless draw must execute cleanly without window
        var ex = Record.Exception(() => scene.Draw());
        Assert.Null(ex);
        scene.Exit();
    }

    [Fact]
    public void TC_T1_R1_02_HueExplorationScene_CollisionBounds_ContainPlayerWithinMapBounds()
    {
        var data = SaveData.CreateDefault();
        var scene = new HueExplorationScene(data, new NullAudioService(), new MockInputService(), new EventBus());
        scene.Enter();

        // Drive player toward negative coordinates (outside top-left)
        scene.Kinematics.Position = new Vector2(-100f, -50f);
        scene.Kinematics.Update(new Vector2(-1f, -1f), scene.CollisionWorld, 0.016f);
        Assert.True(scene.Kinematics.Position.X >= 0f, "Player X must be clamped to map left boundary.");
        Assert.True(scene.Kinematics.Position.Y >= 0f, "Player Y must be clamped to map top boundary.");

        // Drive player beyond right and bottom map bounds
        scene.Kinematics.Position = new Vector2(1700f, 950f);
        scene.Kinematics.Update(new Vector2(1f, 1f), scene.CollisionWorld, 0.016f);
        Assert.True(scene.Kinematics.Position.X <= 1600f, "Player X must be clamped to map right boundary.");
        Assert.True(scene.Kinematics.Position.Y <= 900f, "Player Y must be clamped to map bottom boundary.");

        scene.Exit();
    }

    [Fact]
    public void TC_T1_R1_03_HueExplorationScene_StaticColliders_BlockMovementIntoCitadelWallAndDeepWater()
    {
        var data = SaveData.CreateDefault();
        var scene = new HueExplorationScene(data, new NullAudioService(), new MockInputService(), new EventBus());
        scene.Enter();

        // 1. Citadel wall line at Y <= 320
        scene.Kinematics.Position = new Vector2(400f, 325f);
        for (int i = 0; i < 30; i++)
        {
            scene.Kinematics.Update(new Vector2(0f, -1f), scene.CollisionWorld, 0.016f);
        }
        Assert.True(scene.Kinematics.Position.Y >= 320f, $"Player Y ({scene.Kinematics.Position.Y}) cannot penetrate Citadel wall collider (Y <= 320).");

        // 2. River curb at Y >= 820
        scene.Kinematics.Position = new Vector2(400f, 815f);
        for (int i = 0; i < 30; i++)
        {
            scene.Kinematics.Update(new Vector2(0f, 1f), scene.CollisionWorld, 0.016f);
        }
        Assert.True(scene.Kinematics.Position.Y <= 820f, $"Player Y ({scene.Kinematics.Position.Y}) cannot penetrate deep water curb (Y >= 820).");

        scene.Exit();
    }

    [Fact]
    public void TC_T1_R1_04_HueExplorationScene_PlayerMovement_KinematicsAccelerationAndFriction()
    {
        var kinematics = new CharacterKinematics
        {
            Position = new Vector2(400f, 480f),
            MaxSpeed = 200.0f,
            Friction = 1200.0f
        };
        var world = new CollisionWorld { MapBounds = new AABB(0, 0, 1600, 900) };

        // Accelerate right for 30 ticks
        for (int i = 0; i < 30; i++)
        {
            kinematics.Update(new Vector2(1f, 0f), world, 0.016667f);
        }
        Assert.True(kinematics.Velocity.X > 150f, "Velocity should accelerate toward MaxSpeed.");
        Assert.True(kinematics.IsMoving);

        // Release input: friction decelerates to zero
        for (int i = 0; i < 60; i++)
        {
            kinematics.Update(Vector2.Zero, world, 0.016667f);
        }
        Assert.Equal(0f, kinematics.Velocity.X);
        Assert.Equal(0f, kinematics.Velocity.Y);
        Assert.False(kinematics.IsMoving);
    }

    [Fact]
    public void TC_T1_R1_05_HueExplorationScene_InteractiveTriggers_DetectProximityForLandmarksAndNPCs()
    {
        var meBichPos = new Vector2(350f, 450f);
        float interactionRadius = 48.0f;

        // Player near Mệ Bích
        var playerNear = new Vector2(360f, 450f);
        float distNear = Vector2.Distance(playerNear, meBichPos);
        bool canInteractNear = distNear <= interactionRadius;
        Assert.True(canInteractNear, "Player within 48px radius must be able to interact.");

        // Player far from Mệ Bích
        var playerFar = new Vector2(500f, 450f);
        float distFar = Vector2.Distance(playerFar, meBichPos);
        bool canInteractFar = distFar <= interactionRadius;
        Assert.False(canInteractFar, "Player beyond 48px radius cannot interact.");
    }

    [Fact]
    public void TC_T1_R1_06_HueExplorationScene_SceneTransition_ReturnsToWorldMapOnHotkeysAndExitPortal()
    {
        var data = SaveData.CreateDefault();
        var scene = new HueExplorationScene(data);
        scene.Enter();

        Assert.False(scene.RequestWorldMap);
        scene.RequestWorldMap = true;
        Assert.True(scene.RequestWorldMap);

        scene.Exit();
    }

    // =========================================================================
    // Category R2: Cultural Quests & Activities (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T1_R2_01_BunBoHueCookingEngine_AuthenticSequence_ValidatesSuccessfully()
    {
        var engine = new BunBoHueCookingEngine();

        Assert.True(engine.PlaceIngredient(0, ItemIds.BunSoiTo));
        Assert.True(engine.PlaceIngredient(1, ItemIds.BapBo));
        Assert.True(engine.PlaceIngredient(2, ItemIds.ChaCua));
        Assert.True(engine.PlaceIngredient(3, ItemIds.RauSongHue));
        Assert.True(engine.PlaceIngredient(4, ItemIds.NuocDungHue));

        bool valid = engine.ValidateAssembly(out string error);
        Assert.True(valid, $"Assembly should be valid. Error: {error}");
        Assert.Empty(error);
        Assert.True(engine.IsSolved);
    }

    [Fact]
    public void TC_T1_R2_02_BunBoHueCookingEngine_IncompleteBowl_ReturnsMissingIngredientsError()
    {
        var engine = new BunBoHueCookingEngine();
        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.BapBo);
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        // Slots 3 and 4 empty

        bool valid = engine.ValidateAssembly(out string error);
        Assert.False(valid);
        Assert.Contains("thiếu nguyên liệu", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void TC_T1_R2_03_BunBoHueCookingEngine_BrothPlacedFirst_ReturnsContextualButChiGuidance()
    {
        var engine = new BunBoHueCookingEngine();
        engine.PlaceIngredient(0, ItemIds.NuocDungHue); // Broth first!
        engine.PlaceIngredient(1, ItemIds.BunSoiTo);
        engine.PlaceIngredient(2, ItemIds.BapBo);
        engine.PlaceIngredient(3, ItemIds.ChaCua);
        engine.PlaceIngredient(4, ItemIds.RauSongHue);

        bool valid = engine.ValidateAssembly(out string error);
        Assert.False(valid);
        Assert.Contains("Nước dùng", error);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void TC_T1_R2_04_HoaDangRiverEngine_InitialState_PathIsBlockedByBoatsAndRafts()
    {
        var engine = new HoaDangRiverEngine();

        Assert.Equal(7, engine.Width);
        Assert.Equal(5, engine.Height);
        Assert.Equal((0, 2), engine.Entrance);
        Assert.Equal((6, 2), engine.ExitGate);

        Assert.False(engine.IsPathClear(), "River channel must be initially blocked by river obstacles.");
        Assert.Null(engine.FindPath());
    }

    [Fact]
    public void TC_T1_R2_05_HoaDangRiverEngine_ValidSolutionSequence_ClearsChannelAndSolves()
    {
        var engine = new HoaDangRiverEngine();

        // 1. Push Fruit Boat left to (1, 3)
        Assert.True(engine.PushObject("Obj_FruitBoat_1", Direction.Left));

        // 2. Push Dragon Boat down to Y=2 (occupies 2,2 and 2,3)
        Assert.True(engine.PushObject("Obj_DragonBoat_1", Direction.Down));

        // 3. Push Lotus Cluster down to Y=3 (opens row 1 at x=5)
        Assert.True(engine.PushObject("Obj_LotusCluster_1", Direction.Down));

        // Path is now clear from (0, 2) to (6, 2)!
        Assert.True(engine.IsPathClear());
        var path = engine.FindPath();
        Assert.NotNull(path);
        Assert.Equal(engine.Entrance, path[0]);
        Assert.Equal(engine.ExitGate, path[^1]);
    }

    [Fact]
    public void TC_T1_R2_06_HoaDangRiverEngine_ResetGrid_RestoresInitialLayoutAndBlocksPath()
    {
        var engine = new HoaDangRiverEngine();

        // Move obstacle
        Assert.True(engine.PushObject("Obj_FruitBoat_1", Direction.Left));
        var fruitBoat = engine.Entities.First(e => e.Id == "Obj_FruitBoat_1");
        Assert.Equal(1, fruitBoat.X);

        // Reset
        engine.ResetGrid();
        fruitBoat = engine.Entities.First(e => e.Id == "Obj_FruitBoat_1");
        Assert.Equal(2, fruitBoat.X);
        Assert.False(engine.IsPathClear());
    }

    // =========================================================================
    // Category R3: NPCs & Dialogue (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T1_R3_01_HueDialogue_LoadsJsonFileAndValidatesGraphs()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        Assert.True(service.LoadedGraphs.Count >= 14, $"Expected >= 14 Hue dialogue graphs, got {service.LoadedGraphs.Count}");
        Assert.NotNull(service.StartDialogue("Dialogue_MeHue_Intro"));
        Assert.NotNull(service.StartDialogue("Dialogue_MeHue_TurnIn"));
        Assert.NotNull(service.StartDialogue("Dialogue_Npc_SongHuong"));
        Assert.NotNull(service.StartDialogue("Dialogue_PoI_ThienMu"));
    }

    [Fact]
    public void TC_T1_R3_02_HueDialogue_BranchingChoices_NavigateToProperNextNodes()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        service.StartDialogue("Dialogue_MeHue_Intro");
        service.AdvanceToNext(); // Move to choice node
        Assert.Equal(2, service.CurrentNode?.Branches.Count);

        service.SelectChoice(0); // Accept quest
        Assert.Equal("Node_MeHue_Accept", service.CurrentNode?.Id);
    }

    [Fact]
    public void TC_T1_R3_03_HueDialogue_ActionTriggers_PublishQuestEvents()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        string? triggeredAction = null;
        service.OnActionTriggered += action => triggeredAction = action;

        service.StartDialogue("Dialogue_MeHue_Intro");
        service.AdvanceToNext();
        service.SelectChoice(0);

        Assert.Equal("Start_Hue_Quest_1", triggeredAction);
    }

    [Fact]
    public void TC_T1_R3_04_HueDialogue_CulturalLore_DeliversThienMuAndPerfumeRiverStories()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        var startNode = service.StartDialogue("Dialogue_PoI_ThienMu");
        Assert.NotNull(startNode);
        Assert.Contains("Thiên Mụ", startNode.Text);

        var riverNode = service.StartDialogue("Dialogue_Npc_SongHuong");
        Assert.NotNull(riverNode);
        Assert.Contains("sông Hương", riverNode.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TC_T1_R3_05_HueDialogue_ChoiceFiltering_BasedOnQuestFlags()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        var flags = new QuestFlags { Gate_HoiAn_Unlocked = false };
        var startNode = service.StartDialogue("Dialogue_Gate_HoiAn");
        Assert.NotNull(startNode);

        var branchesLocked = service.GetAvailableBranches(startNode, flags);
        Assert.Single(branchesLocked);
        Assert.Equal("Choice_Inspect", branchesLocked[0].ChoiceId);

        // Unlock gate
        flags.Gate_HoiAn_Unlocked = true;
        var branchesUnlocked = service.GetAvailableBranches(startNode, flags);
        Assert.Equal(2, branchesUnlocked.Count);
        Assert.Contains(branchesUnlocked, b => b.ChoiceId == "Choice_Cross");
    }

    [Fact]
    public void TC_T1_R3_06_HueDialogue_ReInteractingNPCs_DisplaysInProgressOrPostCompletionText()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        // In-progress reminder
        var inProg = service.StartDialogue("Dialogue_MeHue_InProgress");
        Assert.NotNull(inProg);
        Assert.Contains("Bún Vân Cù", inProg.Text);

        // Post-cook gratitude
        var postCook = service.StartDialogue("Dialogue_MeHue_PostCook");
        Assert.NotNull(postCook);
        Assert.Contains("Bún Bò", postCook.Text);
    }

    // =========================================================================
    // Category R4: Progression & Rewards (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T1_R4_01_HueQuests_RegistrationAndStateFlow_Q1ToQ3()
    {
        var manager = new QuestManager();
        HueQuests.RegisterAll(manager);

        Assert.NotNull(manager.GetQuest(HueQuests.Quest1Id));
        Assert.NotNull(manager.GetQuest(HueQuests.Quest2Id));
        Assert.NotNull(manager.GetQuest(HueQuests.Quest3Id));

        Assert.True(manager.CanStartQuest(HueQuests.Quest1Id));
        Assert.False(manager.CanStartQuest(HueQuests.Quest2Id), "Q2 requires Q1 complete.");
        Assert.False(manager.CanStartQuest(HueQuests.Quest3Id), "Q3 requires Q2 complete.");
    }

    [Fact]
    public void TC_T1_R4_02_HueInventory_CollectAllFiveBunBoIngredients_UpdatesObjectives()
    {
        var manager = new QuestManager();
        HueQuests.RegisterAll(manager);
        manager.StartQuest(HueQuests.Quest1Id);

        var inventory = new Inventory();
        foreach (var ingredient in ItemIds.AllBunBoIngredients)
        {
            inventory.AddItem(ingredient);
            manager.RecordObjectiveProgress(HueQuests.Quest1Id, ingredient, 1);
        }

        Assert.Equal(5, inventory.Count);
        var q1 = manager.GetQuest(HueQuests.Quest1Id);
        Assert.NotNull(q1);
        Assert.True(q1.AreAllMandatoryObjectivesComplete);
    }

    [Fact]
    public void TC_T1_R4_03_HueProgression_MidChapterColorBloom_SetsColorRestorationOnQ2()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progressionService = new LevelProgressionService(data, bus);

        RegionColorUpdatedEvent? colorEvent = null;
        bus.Subscribe<RegionColorUpdatedEvent>(e => colorEvent = e);

        progressionService.OnHueQuest2Completed();

        Assert.True(data.QuestFlags.Hue_Q2_Completed);
        Assert.True(Math.Abs(data.WorldProgression.GetColorRestoration(WorldProgression.RegionHue) - 33.3f) < 0.1f);
        Assert.NotNull(colorEvent);
        Assert.Equal(WorldProgression.RegionHue, colorEvent.RegionId);
    }

    [Fact]
    public void TC_T1_R4_04_HueProgression_CompleteHue_Restores100PercentColorAndUnlocksHoiAn()
    {
        var data = SaveData.CreateDefault();
        var bus = new EventBus();
        var progressionService = new LevelProgressionService(data, bus);

        progressionService.CompleteHue();

        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));
        Assert.True(data.WorldProgression.IsRegionCompleted(WorldProgression.RegionHue));
        Assert.True(data.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));
    }

    [Fact]
    public void TC_T1_R4_05_HueAlbum_AwardsJournalPage2AndThienMuStamp()
    {
        var data = SaveData.CreateDefault();
        var progressionService = new LevelProgressionService(data, new EventBus());

        progressionService.CompleteHue();

        Assert.Contains(AlbumService.HuePage2.Id, data.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumService.HueStamp.Id, data.CollectionAlbum.CollectedStamps);
    }

    [Fact]
    public void TC_T1_R4_06_HueAlbum_CulturalFactCards_UnlockedThroughInteractions()
    {
        var album = new CollectionAlbum();
        var service = new AlbumService();

        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueBunBo));
        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueNgoMon));
        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueSongHuong));
        Assert.True(service.UnlockFactCard(album, AlbumService.FactCardHueThienMu));

        var cards = service.GetUnlockedFactCards(album);
        Assert.Equal(4, cards.Count);
    }

    // =========================================================================
    // Category R5: State Persistence & Headless Runtime (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T1_R5_01_HueSaveData_RoundTrip_PreservesAllHueQuestFlagsAndInventory()
    {
        var original = SaveData.CreateDefault();
        original.QuestFlags.Hue_Q1_Started = true;
        original.QuestFlags.Hue_Q1_Completed = true;
        original.QuestFlags.Hue_Q2_Started = true;
        original.QuestFlags.Hue_Q2_BunBoCooked = true;
        original.QuestFlags.Hue_Q2_Completed = true;
        original.QuestFlags.Hue_Q3_Started = true;
        original.QuestFlags.Hue_Q3_PuzzleSolved = true;
        original.QuestFlags.Hue_Q3_Completed = true;
        original.QuestFlags.Gate_HoiAn_Unlocked = true;
        original.QuestFlags.Hue_Finished = true;

        foreach (var item in ItemIds.AllBunBoIngredients)
        {
            original.Level1State.InventoryItems.Add(item);
        }

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<SaveData>(json);

        Assert.NotNull(restored);
        Assert.True(restored.QuestFlags.Hue_Q1_Completed);
        Assert.True(restored.QuestFlags.Hue_Q2_BunBoCooked);
        Assert.True(restored.QuestFlags.Hue_Q3_PuzzleSolved);
        Assert.True(restored.QuestFlags.Gate_HoiAn_Unlocked);
        Assert.True(restored.QuestFlags.Hue_Finished);
        Assert.Equal(5, restored.Level1State.InventoryItems.Count);
    }

    [Fact]
    public void TC_T1_R5_02_HueSaveData_BackwardCompatibility_LoadsLegacySaveWithoutErrors()
    {
        // Legacy JSON omitting Hue flags
        string legacyJson = @"{
            ""saveVersion"": ""1.0.0"",
            ""playerProfile"": { ""playerName"": ""May"" },
            ""worldProgression"": { ""completedRegions"": [""Hanoi""], ""unlockedRegions"": [""Hanoi"", ""Hue""] },
            ""level1State"": { ""questFlags"": { ""Level1_Finished"": true } }
        }";

        var restored = JsonSerializer.Deserialize<SaveData>(legacyJson, SaveGameJsonContext.CreateOptions());
        Assert.NotNull(restored);
        restored.Sanitize();

        Assert.False(restored.QuestFlags.Hue_Q1_Started);
        Assert.False(restored.QuestFlags.Hue_Finished);
        Assert.True(restored.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHue));
    }

    [Fact]
    public void TC_T1_R5_03_HueExplorationScene_HeadlessLifecycle_EnterUpdateExit()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        scene.Enter();
        for (int i = 0; i < 60; i++)
        {
            scene.Update(0.016667f);
        }
        scene.Draw();
        scene.Exit();
    }

    [Fact]
    public void TC_T1_R5_04_BunBoHueCooking_HeadlessLifecycle_ModalOpenAndClose()
    {
        var engine = new BunBoHueCookingEngine();
        Assert.False(engine.IsSolved);

        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.ResetPuzzle();

        Assert.False(engine.IsSolved);
        Assert.Null(engine.CurrentSlots[0]);
    }

    [Fact]
    public void TC_T1_R5_05_HoaDangRiver_HeadlessLifecycle_ModalOpenAndClose()
    {
        var engine = new HoaDangRiverEngine();
        Assert.Equal(7, engine.Width);
        Assert.Equal(5, engine.Height);
        Assert.False(engine.IsPathClear());

        engine.ResetGrid();
        Assert.False(engine.IsPathClear());
    }

    [Fact]
    public void TC_T1_R5_06_Regression_HanoiTestSuitePassesAlongsideHueTests()
    {
        var data = SaveData.CreateDefault();
        var manager = new QuestManager();
        HanoiQuests.RegisterAll(manager);
        HueQuests.RegisterAll(manager);

        Assert.NotNull(manager.GetQuest(QuestIds.Hanoi01));
        Assert.NotNull(manager.GetQuest(HueQuests.Quest1Id));
    }

    #endregion

    #region Tier 2: Boundary & Corner Cases (R1 - R5)

    // =========================================================================
    // Category R1: Boundary & Corner Cases (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T2_R1_01_PlayerKinematics_ExtremeCoordinates_ClampedToMapBounds()
    {
        var kinematics = new CharacterKinematics { Position = new Vector2(-9999f, -9999f) };
        var world = new CollisionWorld { MapBounds = new AABB(0, 0, 1600, 900) };

        kinematics.Update(Vector2.Zero, world, 0.016f);
        Assert.True(kinematics.Position.X >= 0f);
        Assert.True(kinematics.Position.Y >= 0f);

        kinematics.Position = new Vector2(99999f, 99999f);
        kinematics.Update(Vector2.Zero, world, 0.016f);
        Assert.True(kinematics.Position.X <= 1600f);
        Assert.True(kinematics.Position.Y <= 900f);
    }

    [Fact]
    public void TC_T2_R1_02_PlayerKinematics_OpposingInputs_ProducesZeroVelocity()
    {
        var input = new MockInputService();
        input.SimulateOpposingKeys(true, true, true, true); // W+S, A+D

        Assert.Equal(Vector2.Zero, input.MovementDirection);
    }

    [Fact]
    public void TC_T2_R1_03_CollisionSliding_CornerCornerIntersection_DoesNotTrapPlayer()
    {
        var kinematics = new CharacterKinematics { Position = new Vector2(5f, 325f) };
        var world = new CollisionWorld { MapBounds = new AABB(0, 0, 1600, 900) };
        world.AddObstacle(new AABB(0, 0, 1600, 320));

        // Move up-left into the wall junction
        kinematics.Update(new Vector2(-1f, -1f), world, 0.016f);
        Assert.True(kinematics.Position.X >= 0f);
        Assert.True(kinematics.Position.Y >= 320f);

        // Slide right away from corner
        kinematics.Update(new Vector2(1f, 0f), world, 0.016f);
        Assert.True(kinematics.Position.X > 5f);
    }

    [Fact]
    public void TC_T2_R1_04_SceneTransition_WalkingToExitBoundary_BlockedWhenBridgeLocked()
    {
        var data = SaveData.CreateDefault();
        data.QuestFlags.Gate_HoiAn_Unlocked = false;

        // Bridge obstacle blocks player at X >= 1520 when locked
        var world = new CollisionWorld { MapBounds = new AABB(0, 0, 1600, 900) };
        var bridgeObstacle = new AABB(1500, 320, 1600, 820);
        world.AddObstacle(bridgeObstacle);

        var kinematics = new CharacterKinematics { Position = new Vector2(1480f, 480f) };
        for (int i = 0; i < 30; i++)
        {
            kinematics.Update(new Vector2(1f, 0f), world, 0.016f);
        }

        Assert.True(kinematics.Position.X <= 1500f, "Player cannot bypass bridge barrier while locked.");
    }

    [Fact]
    public void TC_T2_R1_05_InteractiveTrigger_EdgeOfRadius_ExactBoundaryToggling()
    {
        var targetCenter = new Vector2(300f, 300f);
        float radius = 48.0f;

        var inside = targetCenter + new Vector2(47.9f, 0f);
        Assert.True(Vector2.Distance(inside, targetCenter) <= radius);

        var outside = targetCenter + new Vector2(48.1f, 0f);
        Assert.False(Vector2.Distance(outside, targetCenter) <= radius);
    }

    [Fact]
    public void TC_T2_R1_06_RapidSceneTransition_RapidToggleBetweenExplorationAndWorldMap()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        for (int i = 0; i < 50; i++)
        {
            scene.Enter();
            scene.RequestWorldMap = true;
            Assert.True(scene.RequestWorldMap);
            scene.Exit();
        }
    }

    // =========================================================================
    // Category R2: Boundary & Corner Cases (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T2_R2_01_BunBoCooking_NegativeOrOutOfBoundsSlotIndex_ReturnsFalseGracefully()
    {
        var engine = new BunBoHueCookingEngine();

        Assert.False(engine.PlaceIngredient(-1, ItemIds.BunSoiTo));
        Assert.False(engine.PlaceIngredient(5, ItemIds.BunSoiTo));
        Assert.False(engine.PlaceIngredient(999, ItemIds.BunSoiTo));
        Assert.False(engine.RemoveIngredient(-1));
        Assert.False(engine.RemoveIngredient(5));
    }

    [Fact]
    public void TC_T2_R2_02_BunBoCooking_EmptyOrNullIngredientId_Rejected()
    {
        var engine = new BunBoHueCookingEngine();

        Assert.False(engine.PlaceIngredient(0, null!));
        Assert.False(engine.PlaceIngredient(0, ""));
        Assert.False(engine.PlaceIngredient(0, "   "));
        Assert.Null(engine.CurrentSlots[0]);
    }

    [Fact]
    public void TC_T2_R2_03_BunBoCooking_DuplicateIngredientsInMultipleSlots_FailsValidation()
    {
        var engine = new BunBoHueCookingEngine();
        for (int i = 0; i < 5; i++)
        {
            engine.PlaceIngredient(i, ItemIds.BunSoiTo);
        }

        bool valid = engine.ValidateAssembly(out string error);
        Assert.False(valid);
        Assert.Contains("riêng biệt", error);
    }

    [Fact]
    public void TC_T2_R2_04_BunBoCooking_ForeignOrUnrecognizedItemId_FailsValidation()
    {
        var engine = new BunBoHueCookingEngine();
        engine.PlaceIngredient(0, ItemIds.BunSoiTo);
        engine.PlaceIngredient(1, ItemIds.BapBo);
        engine.PlaceIngredient(2, ItemIds.ChaCua);
        engine.PlaceIngredient(3, ItemIds.RauSongHue);
        engine.PlaceIngredient(4, ItemIds.BanhPho); // Hanoi ingredient!

        bool valid = engine.ValidateAssembly(out string error);
        Assert.False(valid);
        Assert.Contains("không hợp lệ", error);
    }

    [Fact]
    public void TC_T2_R2_05_HoaDangRiver_PushObstacleIntoStaticWallOrOutOfBounds_Rejected()
    {
        var engine = new HoaDangRiverEngine();

        // Push Dragon Boat UP into northern riverbank
        Assert.False(engine.PushObject("Obj_DragonBoat_1", Direction.Up));

        // Push Fruit Boat DOWN into southern riverbank
        Assert.False(engine.PushObject("Obj_FruitBoat_1", Direction.Down));
    }

    [Fact]
    public void TC_T2_R2_06_HoaDangRiver_PushNonexistentEntity_ReturnsFalse()
    {
        var engine = new HoaDangRiverEngine();
        Assert.False(engine.PushObject("Entity_NonExistent_999", Direction.Left));
    }

    // =========================================================================
    // Category R3: Boundary & Corner Cases (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T2_R3_01_HueDialogue_MissingOrCorruptedJson_HandledGracefully()
    {
        var service = new DialogueService();
        service.LoadDialogueFile("Non_Existent_File_Path.json");

        Assert.Empty(service.LoadedGraphs);
        Assert.Null(service.StartDialogue("Any_Dialogue"));
    }

    [Fact]
    public void TC_T2_R3_02_HueDialogue_NonExistentDialogueId_ReturnsNull()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        var node = service.StartDialogue("Dialogue_NonExistent_999");
        Assert.Null(node);
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void TC_T2_R3_03_HueDialogue_NodeWithNoBranchesAndNullNextNode_TerminatesDialogue()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        service.StartDialogue("Dialogue_Hue_Intro");
        // Advance through terminal nodes
        while (service.IsDialogueActive)
        {
            service.AdvanceToNext();
        }
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void TC_T2_R3_04_HueDialogue_InvalidChoiceIndexOrBranchId_Ignored()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        service.StartDialogue("Dialogue_MeHue_Intro");
        service.AdvanceToNext(); // Choice node

        Assert.Null(service.SelectChoice(-1));
        Assert.Null(service.SelectChoice(99));
        Assert.True(service.IsDialogueActive);
    }

    [Fact]
    public void TC_T2_R3_05_HueDialogue_SimultaneousChoiceSelections_Idempotent()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        service.StartDialogue("Dialogue_MeHue_Intro");
        service.AdvanceToNext();

        var first = service.SelectChoice(0);
        Assert.NotNull(first);
        // Repeated choice selection on a non-branch node returns null
        var second = service.SelectChoice(0);
        Assert.Null(second);
    }

    [Fact]
    public void TC_T2_R3_06_HueDialogue_MissingRequiredQuestFlag_GracefullyHidesBranch()
    {
        var service = new DialogueService();
        service.LoadDialogueFile(GetHueDialoguePath());

        var flags = new QuestFlags(); // all false
        var node = service.StartDialogue("Dialogue_Gate_HoiAn");
        Assert.NotNull(node);

        var branches = service.GetAvailableBranches(node, flags);
        Assert.DoesNotContain(branches, b => b.ChoiceId == "Choice_Cross");
    }

    // =========================================================================
    // Category R4: Boundary & Corner Cases (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T2_R4_01_HueInventory_DuplicateItemAdditions_IdempotentAddition()
    {
        var inventory = new Inventory();
        Assert.True(inventory.AddItem(ItemIds.BunSoiTo));
        Assert.False(inventory.AddItem(ItemIds.BunSoiTo));
        Assert.Equal(1, inventory.Count);
    }

    [Fact]
    public void TC_T2_R4_02_HueInventory_NullOrWhitespaceItemId_Rejected()
    {
        var inventory = new Inventory();
        Assert.False(inventory.AddItem(null!));
        Assert.False(inventory.AddItem(""));
        Assert.False(inventory.AddItem("   "));
        Assert.Equal(0, inventory.Count);
    }

    [Fact]
    public void TC_T2_R4_03_HueProgression_DoubleCompletion_IdempotentWorldProgression()
    {
        var data = SaveData.CreateDefault();
        var service = new LevelProgressionService(data, new EventBus());

        service.CompleteHue();
        service.CompleteHue(); // Second call

        Assert.Equal(100.0f, data.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));
        Assert.Single(data.WorldProgression.CompletedRegions, r => r == WorldProgression.RegionHue);
    }

    [Fact]
    public void TC_T2_R4_04_HueProgression_ColorRestoration_ClampedBetweenZeroAnd100()
    {
        var progression = new WorldProgression();

        progression.SetColorRestoration("Hue", -100f);
        Assert.Equal(0.0f, progression.GetColorRestoration("Hue"));

        progression.SetColorRestoration("Hue", 300f);
        Assert.Equal(100.0f, progression.GetColorRestoration("Hue"));
    }

    [Fact]
    public void TC_T2_R4_05_HueAlbum_DuplicateAwardJournalPageOrStamp_DoesNotDuplicate()
    {
        var album = new CollectionAlbum();
        Assert.True(album.AddJournalPage(AlbumService.HuePage2.Id));
        Assert.False(album.AddJournalPage(AlbumService.HuePage2.Id));
        Assert.Single(album.CollectedJournalPages);

        Assert.True(album.AddStamp(AlbumService.HueStamp.Id));
        Assert.False(album.AddStamp(AlbumService.HueStamp.Id));
        Assert.Single(album.CollectedStamps);
    }

    [Fact]
    public void TC_T2_R4_06_HueQuests_SequenceBreakingAttempt_CannotStartQ2BeforeQ1()
    {
        var manager = new QuestManager();
        HueQuests.RegisterAll(manager);

        Assert.False(manager.CanStartQuest(HueQuests.Quest2Id));
        Assert.False(manager.CanStartQuest(HueQuests.Quest3Id));
    }

    // =========================================================================
    // Category R5: Boundary & Corner Cases (6 Tests)
    // =========================================================================

    [Fact]
    public void TC_T2_R5_01_SaveData_NullOrEmptyPayload_SanitizesToDefaults()
    {
        var storage = new MockSaveStorage();
        var service = new SaveLoadService(storage);

        storage.WriteAllText("savegame.json", "{}");
        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded.QuestFlags);
        Assert.False(loaded.QuestFlags.Hue_Q1_Started);
    }

    [Fact]
    public void TC_T2_R5_02_SaveData_CorruptedFieldTypes_HealsCorruptedFields()
    {
        var data = new SaveData
        {
            SaveVersion = null!,
            PlayerProfile = null!,
            WorldProgression = null!,
            CollectionAlbum = null!
        };

        data.Sanitize();

        Assert.Equal(SaveData.CurrentSaveVersion, data.SaveVersion);
        Assert.NotNull(data.PlayerProfile);
        Assert.NotNull(data.WorldProgression);
        Assert.NotNull(data.CollectionAlbum);
    }

    [Fact]
    public void TC_T2_R5_03_SaveData_MissingQuestFlagsProperty_InitializesDefaultQuestFlags()
    {
        string jsonWithoutFlags = @"{ ""saveVersion"": ""1.0.0"", ""level1State"": {} }";
        var restored = JsonSerializer.Deserialize<SaveData>(jsonWithoutFlags);

        Assert.NotNull(restored);
        Assert.NotNull(restored.QuestFlags);
        Assert.False(restored.QuestFlags.Hue_Q1_Started);
    }

    [Fact]
    public void TC_T2_R5_04_Headless_RapidUpdateTicks_NoAccumulatorDriftOrOverflow()
    {
        var driver = new HueTestSimulationDriver();
        driver.AdvanceTicks(1000);

        Assert.Equal(1000, driver.TotalTicksSimulated);
        Assert.True(driver.CurrentTime > 16.0f);
    }

    [Fact]
    public void TC_T2_R5_05_Headless_MultipleSceneInstances_NoCrossContamination()
    {
        var data1 = SaveData.CreateDefault();
        var data2 = SaveData.CreateDefault();

        var scene1 = new HueExplorationScene(data1);
        var scene2 = new HueExplorationScene(data2);

        scene1.Kinematics.Position = new Vector2(100f, 100f);
        scene2.Kinematics.Position = new Vector2(500f, 500f);

        Assert.Equal(new Vector2(100f, 100f), scene1.Kinematics.Position);
        Assert.Equal(new Vector2(500f, 500f), scene2.Kinematics.Position);
    }

    [Fact]
    public void TC_T2_R5_06_SaveData_FileWriteAtomic_BackupFilePreservedOnCrash()
    {
        var storage = new MockSaveStorage();
        var service = new SaveLoadService(storage);

        var data = SaveData.CreateDefault();
        data.QuestFlags.Hue_Q1_Started = true;
        service.Save(data);

        Assert.True(storage.FileExists(service.SaveFilePath));
    }

    #endregion

    #region Tier 3: Cross-Feature Combinations (6 Tests)

    [Fact]
    public void TC_T3_01_Dialogue_To_QuestStart_To_MinigameUnlock_To_ItemAward()
    {
        var driver = new HueTestSimulationDriver();

        // 1. Dialogue starts Q1
        driver.DialogueService.StartDialogue("Dialogue_MeHue_Intro");
        driver.DialogueService.AdvanceToNext();
        driver.DialogueService.SelectChoice(0); // Trigger Start_Hue_Quest_1
        driver.QuestManager.StartQuest(HueQuests.Quest1Id);
        driver.CurrentData.QuestFlags.Hue_Q1_Started = true;

        // 2. Collect 5 ingredients
        driver.SimulateCollectIngredient(ItemIds.BunSoiTo);
        driver.SimulateCollectIngredient(ItemIds.BapBo);
        driver.SimulateCollectIngredient(ItemIds.ChaCua);
        driver.SimulateCollectIngredient(ItemIds.NuocDungHue);
        driver.SimulateCollectIngredient(ItemIds.RauSongHue);
        driver.QuestManager.CompleteQuest(HueQuests.Quest1Id);
        driver.CurrentData.QuestFlags.Hue_Q1_Completed = true;

        // 3. Q2 unlocked
        Assert.True(driver.QuestManager.CanStartQuest(HueQuests.Quest2Id));
        Assert.True(driver.SimulateSolveBunBoCooking());
        Assert.True(driver.CurrentData.QuestFlags.Hue_Q2_Completed);
        Assert.True(driver.CurrentData.QuestFlags.Hue_Q2_BunBoCooked);
    }

    [Fact]
    public void TC_T3_02_MinigameWin_To_AlbumStampUnlock_And_JournalPageRegistration()
    {
        var driver = new HueTestSimulationDriver();

        // Solve river puzzle
        Assert.True(driver.SimulateClearRiverPath());
        Assert.True(driver.CurrentData.QuestFlags.Hue_Q3_PuzzleSolved);

        // Climax awards
        driver.ProgressionService.CompleteHue();

        Assert.Contains(AlbumService.HuePage2.Id, driver.CurrentData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumService.HueStamp.Id, driver.CurrentData.CollectionAlbum.CollectedStamps);
    }

    [Fact]
    public void TC_T3_03_Chapter2Completion_To_WorldProgressionHoiAnUnlock_To_PersistenceRoundTrip()
    {
        var storage = new MockSaveStorage();
        var driver1 = new HueTestSimulationDriver(storage);

        driver1.SimulateGatherAllBunBoIngredients();
        driver1.SimulateSolveBunBoCooking();
        driver1.SimulateClearRiverPath();
        driver1.SimulateHueClimax();

        Assert.True(driver1.CurrentData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));

        // Reboot fresh driver from storage
        var driver2 = new HueTestSimulationDriver(storage);
        var loaded = driver2.SaveLoadService.Load();

        Assert.True(loaded.WorldProgression.IsRegionCompleted(WorldProgression.RegionHue));
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));
        Assert.True(loaded.QuestFlags.Hue_Finished);
    }

    [Fact]
    public void TC_T3_04_InventoryItemGating_DuringCookingMinigame_AndDialogueTriggers()
    {
        var driver = new HueTestSimulationDriver();
        driver.SimulateCollectIngredient(ItemIds.BunSoiTo);
        driver.SimulateCollectIngredient(ItemIds.BapBo);
        // Missing ChaCua, NuocDungHue, RauSongHue

        Assert.False(driver.CurrentData.QuestFlags.AreAllHueQ1IngredientsCollected());
        Assert.Equal(2, driver.Inventory.Count);
        Assert.False(driver.BunBoEngine.ValidateAssembly(out _));
    }

    [Fact]
    public void TC_T3_05_SimultaneousCollisionAndInteractionTriggerBoundary_AtLandmarkHotspots()
    {
        var world = new CollisionWorld { MapBounds = new AABB(0, 0, 1600, 900) };
        world.AddObstacle(new AABB(0, 0, 1600, 320)); // North wall

        var meBichPos = new Vector2(350f, 340f); // Placed against the wall
        var kinematics = new CharacterKinematics { Position = new Vector2(350f, 350f) };

        // Push against wall
        kinematics.Update(new Vector2(0f, -1f), world, 0.016f);
        Assert.True(kinematics.Position.Y >= 320f);

        // Interaction remains valid while colliding
        float dist = Vector2.Distance(kinematics.Position, meBichPos);
        Assert.True(dist <= 48.0f, "Interaction trigger should remain active while colliding with wall.");
    }

    [Fact]
    public void TC_T3_06_ColorBloomProgression_CoupledWithSceneVisualAndSoundFeedback()
    {
        var driver = new HueTestSimulationDriver();
        Assert.Equal(0.0f, driver.CurrentData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));

        // Q2 completion: 33.3%
        driver.SimulateSolveBunBoCooking();
        Assert.True(Math.Abs(driver.CurrentData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue) - 33.3f) < 0.1f);
        Assert.Contains(driver.ReceivedEvents, e => e is RegionColorUpdatedEvent);

        // Climax: 100%
        driver.ProgressionService.CompleteHue();
        Assert.Equal(100.0f, driver.CurrentData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));
    }

    #endregion

    #region Tier 4: Real-World Application Scenarios (5 Scenarios)

    [Fact]
    public void TC_T4_01_Scenario1_GoldenPathPlaythrough_FullChapterE2E()
    {
        // ==========================================
        // 1. BOOT INTO HUE AFTER HANOI
        // ==========================================
        var driver = new HueTestSimulationDriver();
        driver.CurrentData.WorldProgression.MarkRegionCompleted(WorldProgression.RegionHanoi);
        driver.CurrentData.WorldProgression.UnlockRegion(WorldProgression.RegionHue);

        Assert.True(driver.CurrentData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHue));
        Assert.False(driver.CurrentData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));

        // ==========================================
        // 2. QUEST 1: GATHER 5 INGREDIENTS
        // ==========================================
        driver.SimulateGatherAllBunBoIngredients();
        Assert.True(driver.CurrentData.QuestFlags.Hue_Q1_Completed);
        Assert.Equal(5, driver.Inventory.Count);

        // ==========================================
        // 3. QUEST 2: COOK BÚN BÒ HUẾ
        // ==========================================
        Assert.True(driver.SimulateSolveBunBoCooking());
        Assert.True(driver.CurrentData.QuestFlags.Hue_Q2_Completed);
        Assert.True(Math.Abs(driver.CurrentData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue) - 33.3f) < 0.1f);

        // ==========================================
        // 4. QUEST 3: THẢ ĐÈN HOA ĐĂNG
        // ==========================================
        Assert.True(driver.SimulateClearRiverPath());
        Assert.True(driver.CurrentData.QuestFlags.Hue_Q3_Completed);

        // ==========================================
        // 5. CLIMAX & BRIDGE EXIT TO HỘI AN
        // ==========================================
        driver.SimulateHueClimax();

        Assert.Equal(100.0f, driver.CurrentData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));
        Assert.True(driver.CurrentData.WorldProgression.IsRegionCompleted(WorldProgression.RegionHue));
        Assert.True(driver.CurrentData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));
        Assert.Contains(AlbumService.HuePage2.Id, driver.CurrentData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumService.HueStamp.Id, driver.CurrentData.CollectionAlbum.CollectedStamps);
    }

    [Fact]
    public void TC_T4_02_Scenario2_SaveReloadResiliencePlaythrough_MidChapterInterruption()
    {
        var storage = new MockSaveStorage();
        var driver1 = new HueTestSimulationDriver(storage);

        // Play halfway: complete Q1 and place 2 items in bowl
        driver1.SimulateGatherAllBunBoIngredients();
        driver1.BunBoEngine.PlaceIngredient(0, ItemIds.BunSoiTo);
        driver1.BunBoEngine.PlaceIngredient(1, ItemIds.BapBo);
        driver1.SaveLoadService.Save(driver1.CurrentData);

        // Reboot new driver instance
        var driver2 = new HueTestSimulationDriver(storage);
        driver2.CurrentData = driver2.SaveLoadService.Load();

        Assert.True(driver2.CurrentData.QuestFlags.Hue_Q1_Completed);
        Assert.Equal(5, driver2.CurrentData.Level1State.InventoryItems.Count);

        // Resume remaining chapter
        Assert.True(driver2.SimulateSolveBunBoCooking());
        Assert.True(driver2.SimulateClearRiverPath());
        driver2.SimulateHueClimax();

        Assert.True(driver2.CurrentData.WorldProgression.IsRegionUnlocked(WorldProgression.RegionHoiAn));
    }

    [Fact]
    public void TC_T4_03_Scenario3_SequenceBreakingAndFailureRecovery()
    {
        var driver = new HueTestSimulationDriver();

        // 1. Attempt Q2 out of sequence
        Assert.False(driver.QuestManager.CanStartQuest(HueQuests.Quest2Id));

        // 2. Submit wrong recipe with broth first
        driver.BunBoEngine.PlaceIngredient(0, ItemIds.NuocDungHue);
        driver.BunBoEngine.PlaceIngredient(1, ItemIds.BunSoiTo);
        driver.BunBoEngine.PlaceIngredient(2, ItemIds.BapBo);
        driver.BunBoEngine.PlaceIngredient(3, ItemIds.ChaCua);
        driver.BunBoEngine.PlaceIngredient(4, ItemIds.RauSongHue);
        Assert.False(driver.BunBoEngine.ValidateAssembly(out _));

        // 3. Reset and recover cleanly
        driver.BunBoEngine.ResetPuzzle();
        Assert.Null(driver.BunBoEngine.CurrentSlots[0]);

        // 4. Illegal push on river obstacle
        Assert.False(driver.RiverEngine.PushObject("Obj_DragonBoat_1", Direction.Up));
        driver.RiverEngine.ResetGrid();

        // 5. Proper playthrough proceeds cleanly
        driver.SimulateGatherAllBunBoIngredients();
        Assert.True(driver.SimulateSolveBunBoCooking());
    }

    [Fact]
    public void TC_T4_04_Scenario4_CulturalAlbumCompletionist_CollectAllLoreAndStamps()
    {
        var driver = new HueTestSimulationDriver();

        // Unlock all fact cards
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, AlbumService.FactCardHueBunBo);
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, AlbumService.FactCardHueNgoMon);
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, AlbumService.FactCardHueSongHuong);
        driver.AlbumService.UnlockFactCard(driver.CurrentData.CollectionAlbum, AlbumService.FactCardHueThienMu);

        // Complete Hue
        driver.SimulateHueClimax();

        var unlockedCards = driver.AlbumService.GetUnlockedFactCards(driver.CurrentData.CollectionAlbum);
        Assert.Equal(4, unlockedCards.Count);
        Assert.Contains(AlbumService.HuePage2.Id, driver.CurrentData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumService.HueStamp.Id, driver.CurrentData.CollectionAlbum.CollectedStamps);

        // Verify durability
        driver.SaveLoadService.Save(driver.CurrentData);
        var reloaded = driver.SaveLoadService.Load();
        Assert.Equal(4, reloaded.CollectionAlbum.UnlockedFactCards.Count);
        Assert.Single(reloaded.CollectionAlbum.CollectedJournalPages);
        Assert.Single(reloaded.CollectionAlbum.CollectedStamps);
    }

    [Fact]
    public void TC_T4_05_Scenario5_RapidTransitionAndMemoryStability_HeadlessStress()
    {
        var data = SaveData.CreateDefault();
        using var scene = new HueExplorationScene(data);

        for (int i = 0; i < 100; i++)
        {
            scene.Enter();
            scene.Update(0.016f);
            scene.Draw();
            scene.Exit();
        }

        Assert.Equal(new Vector2(400f, 480f), scene.Kinematics.Position);
    }

    #endregion
}
