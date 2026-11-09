using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Core.State;
using GameProject.Core.WorldMap;
using GameProject.Stages;
using GameProject.UI;
using Xunit;
using MockSaveStorage = GameProject.Tests.Mocks.MockSaveStorage;
using NullAudioService = GameProject.Tests.Mocks.NullAudioService;
using MockInputService = GameProject.Tests.Mocks.MockInputService;
using TestSimulationDriver = GameProject.Tests.Mocks.TestSimulationDriver;

namespace GameProject.Tests;

/// <summary>
/// Milestone 5 Adversarial End-to-End, State Corruption, and Edge-Case Stress Test Suite.
/// Empirically challenges:
/// 1. Save/Load corruption, syntax errors, missing fields, invalid region IDs, negative/NaN color percentages, unexpected types.
/// 2. Out-of-order quest objective tampering, prerequisite bypass attempts, invalid pho combinations, empty inventory submissions.
/// 3. Dialogue & UI state spamming, typewriter fast-forwarding during pending choices, invalid branch index selections, rapid key cycling.
/// 4. Boundary and spatial edge cases, 7x5 stall grid boundary collisions, impossible stall puzzle layouts, out-of-bounds player positions.
/// 5. Continuous multi-cycle playthrough persistence, locked region travel attempts, and multi-cycle album preservation.
/// </summary>
public sealed class ChallengerM5E2EAndStateStressTests
{
    // =========================================================================
    // SECTION 1: SAVE/LOAD CORRUPTION & TAMPERING STRESS TESTS
    // =========================================================================

    [Theory]
    [InlineData("{")]
    [InlineData("{\"saveVersion\": \"1.0.0\", \"worldProgression\": [BROKEN_JSON")]
    [InlineData("Not a JSON string at all!")]
    [InlineData("<html><head><title>502 Bad Gateway</title></head><body>Server error</body></html>")]
    [InlineData("{\"playerProfile\": {\"playerName\": \"Mây\"}} \0\0\0 Garbage trailing bytes")]
    [InlineData("[1, 2, 3, 4, 5]")]
    public void SaveLoad_CorruptedJsonSyntax_RecoversGracefullyWithoutCrashing(string corruptedPayload)
    {
        // ARRANGE: In-memory storage containing malformed syntax
        var storage = new MockSaveStorage();
        storage.InjectCorruption("Data/savegame.json", corruptedPayload);
        var service = new SaveLoadService(storage, "Data/savegame.json");

        // ACT: Attempt to load the corrupted save
        var recoveredData = service.Load();

        // ASSERT: Must not throw, must return clean default data, and must isolate corruption
        Assert.NotNull(recoveredData);
        Assert.Equal(SaveData.CurrentSaveVersion, recoveredData.SaveVersion);
        Assert.Equal("Bé Mây", recoveredData.PlayerProfile.PlayerName);
        Assert.True(recoveredData.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
        Assert.Equal(0.0f, recoveredData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        // Verify that storage was overwritten with fresh default data and backup created
        string rewrittenJson = storage.ReadAllText("Data/savegame.json");
        Assert.Contains("Bé Mây", rewrittenJson);
        Assert.True(storage.FileExists("Data/savegame.json.corrupt"));
    }

    [Fact]
    public void SaveLoad_MissingFieldsAndPartialJson_SanitizesAndFillsDefaults()
    {
        var storage = new MockSaveStorage();
        // Minimal valid JSON missing all subsystem sections
        storage.WriteAllText("Data/savegame.json", "{\"saveVersion\": \"1.0.0\"}");
        var service = new SaveLoadService(storage, "Data/savegame.json");

        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.NotNull(loaded.PlayerProfile);
        Assert.NotNull(loaded.WorldProgression);
        Assert.NotNull(loaded.Level1State);
        Assert.NotNull(loaded.CollectionAlbum);
        Assert.NotNull(loaded.AudioSettings);

        // Subsystems should default gracefully
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
        Assert.Equal("Quest_Hanoi_01", loaded.Level1State.ActiveQuestId);
        Assert.Empty(loaded.Level1State.InventoryItems);
        Assert.Empty(loaded.CollectionAlbum.CollectedJournalPages);
        Assert.Equal(0.8, loaded.AudioSettings.MasterVolume, 2);
    }

    [Fact]
    public void SaveLoad_TamperedInvalidRegionIds_CleansesUnknownAndInvalidEntries()
    {
        var storage = new MockSaveStorage();
        // Inject invalid/empty/whitespace/foreign region IDs into save data
        string tamperedJson = """
        {
            "saveVersion": "1.0.0",
            "worldProgression": {
                "unlockedRegions": ["", "   ", null, "Atlantis", "MoonBase", "Hanoi"],
                "completedRegions": ["", "Narnia"],
                "regionColorRestoration": {
                    "": 50.0,
                    "   ": 30.0,
                    "Atlantis": 80.0,
                    "Hanoi": 25.0
                }
            }
        }
        """;
        storage.WriteAllText("Data/savegame.json", tamperedJson);
        var service = new SaveLoadService(storage, "Data/savegame.json");

        var loaded = service.Load();

        // ASSERT: Hanoi must remain unlocked; empty and whitespace strings must be sanitized
        Assert.NotNull(loaded);
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
        Assert.DoesNotContain("", loaded.WorldProgression.UnlockedRegions);
        Assert.DoesNotContain("   ", loaded.WorldProgression.UnlockedRegions);
        Assert.DoesNotContain("", loaded.WorldProgression.CompletedRegions);
        Assert.False(loaded.WorldProgression.RegionColorRestoration.ContainsKey(""));
        Assert.Equal(25.0f, loaded.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
    }

    [Fact]
    public void SaveLoad_NegativeAndExtremeColorPercentages_ClampsToZeroAndHundred()
    {
        var storage = new MockSaveStorage();
        string tamperedJson = """
        {
            "saveVersion": "1.0.0",
            "worldProgression": {
                "unlockedRegions": ["Hanoi", "Hue"],
                "completedRegions": [],
                "regionColorRestoration": {
                    "Hanoi": -9999.0,
                    "Hue": 88888.5
                }
            }
        }
        """;
        storage.WriteAllText("Data/savegame.json", tamperedJson);
        var service = new SaveLoadService(storage, "Data/savegame.json");

        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal(0.0f, loaded.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.Equal(100.0f, loaded.WorldProgression.GetColorRestoration(RegionIds.Hue));
    }

    [Theory]
    [InlineData("{\"worldProgression\": \"NOT_AN_OBJECT\"}")]
    [InlineData("{\"playerProfile\": 99999}")]
    [InlineData("{\"level1State\": {\"playerSpawnX\": \"NOT_A_NUMBER\"}}")]
    [InlineData("{\"collectionAlbum\": true}")]
    public void SaveLoad_UnexpectedTypesAndSchemaMismatches_FallbacksSafely(string schemaMismatchJson)
    {
        var storage = new MockSaveStorage();
        storage.WriteAllText("Data/savegame.json", schemaMismatchJson);
        var service = new SaveLoadService(storage, "Data/savegame.json");

        var loaded = service.Load();

        // Must recover clean default state without crashing
        Assert.NotNull(loaded);
        Assert.Equal(SaveData.CurrentSaveVersion, loaded.SaveVersion);
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
    }

    [Fact]
    public void SaveLoad_NullOrCorruptedArrayElementsInState_SanitizesCleanly()
    {
        var data = SaveData.CreateDefault();
        data.Level1State.InventoryItems.Add(ItemIds.BanhPho);
        data.Level1State.InventoryItems.Add("");
        data.Level1State.InventoryItems.Add("   ");
        data.Level1State.Puzzle3ObjectPositions.Add(new Puzzle3ObjectPosition("", 0, 0));
        data.Level1State.Puzzle3ObjectPositions.Add(new Puzzle3ObjectPosition("Obj_Cart", 2, 3));
        data.CollectionAlbum.CollectedJournalPages.Add("");
        data.CollectionAlbum.CollectedJournalPages.Add("JournalPage_Hanoi_01");
        data.CollectionAlbum.CollectedStamps.Add("");

        data.Sanitize();

        Assert.Single(data.Level1State.InventoryItems);
        Assert.Equal(ItemIds.BanhPho, data.Level1State.InventoryItems[0]);

        Assert.Single(data.Level1State.Puzzle3ObjectPositions);
        Assert.Equal("Obj_Cart", data.Level1State.Puzzle3ObjectPositions[0].ObjectId);

        Assert.Single(data.CollectionAlbum.CollectedJournalPages);
        Assert.Equal("JournalPage_Hanoi_01", data.CollectionAlbum.CollectedJournalPages[0]);
        Assert.Empty(data.CollectionAlbum.CollectedStamps);
    }

    // =========================================================================
    // SECTION 2: OUT-OF-ORDER QUEST OBJECTIVE TAMPERING STRESS TESTS
    // =========================================================================

    [Fact]
    public void QuestOrder_AttemptStartQuest2_BeforeQuest1Complete_IsRejected()
    {
        var eventBus = new EventBus();
        var qm = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(qm);

        // Quest 1 is NotStarted
        Assert.False(qm.CanStartQuest(QuestIds.Hanoi02), "Quest 2 must not be startable when Quest 1 has not started");

        bool started = qm.StartQuest(QuestIds.Hanoi02);
        Assert.False(started, "StartQuest(QuestIds.Hanoi02) must return false");
        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(QuestIds.Hanoi02));

        // Direct complete call must fail
        bool completed = qm.CompleteQuest(QuestIds.Hanoi02);
        Assert.False(completed, "CompleteQuest(QuestIds.Hanoi02) must return false");
        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(QuestIds.Hanoi02));
    }

    [Fact]
    public void QuestOrder_AttemptStartQuest3_BeforeQuest2Complete_IsRejected()
    {
        var eventBus = new EventBus();
        var qm = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(qm);

        // Start and complete Quest 1
        qm.StartQuest(QuestIds.Hanoi01);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.BanhPho, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.ThitBo, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.NuocDung, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.HanhLa, 1);
        qm.RecordObjectiveProgress(QuestIds.Hanoi01, ItemIds.ThaoMoc, 1);
        qm.CompleteQuest(QuestIds.Hanoi01);
        Assert.Equal(QuestState.Completed, qm.GetQuestState(QuestIds.Hanoi01));

        // Quest 2 is NotStarted
        Assert.False(qm.CanStartQuest(QuestIds.Hanoi03), "Quest 3 must not be startable when Quest 2 has not completed");
        bool q3Started = qm.StartQuest(QuestIds.Hanoi03);
        Assert.False(q3Started);
        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(QuestIds.Hanoi03));

        // Start Quest 2 but do not complete it
        qm.StartQuest(QuestIds.Hanoi02);
        Assert.Equal(QuestState.InProgress, qm.GetQuestState(QuestIds.Hanoi02));

        Assert.False(qm.CanStartQuest(QuestIds.Hanoi03), "Quest 3 must not be startable while Quest 2 is still InProgress");
        bool q3StartedWhileQ2Active = qm.StartQuest(QuestIds.Hanoi03);
        Assert.False(q3StartedWhileQ2Active);
        Assert.Equal(QuestState.NotStarted, qm.GetQuestState(QuestIds.Hanoi03));
    }

    [Fact]
    public void QuestOrder_PhoAssembly_AttemptComplete_WithEmptyOrMissingIngredients_IsRejected()
    {
        var engine = new PhoCookingEngine();

        // 0 ingredients placed
        bool validEmpty = engine.ValidateAssembly(out string errEmpty);
        Assert.False(validEmpty);
        Assert.Contains("thiếu nguyên liệu", errEmpty);
        Assert.False(engine.IsSolved);

        // 4 ingredients placed, 1 missing
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.ThitBo);
        engine.PlaceIngredient(2, ItemIds.HanhLa);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        // slot 4 left empty

        bool validMissingOne = engine.ValidateAssembly(out string errMissingOne);
        Assert.False(validMissingOne);
        Assert.Contains("thiếu nguyên liệu", errMissingOne);
        Assert.False(engine.IsSolved);
    }

    [Fact]
    public void QuestOrder_PhoAssembly_DuplicateAndInvalidIngredients_AreRejected()
    {
        var engine = new PhoCookingEngine();

        // Duplicate ingredients: All 5 slots filled with Bánh Phở
        for (int i = 0; i < 5; i++)
        {
            engine.PlaceIngredient(i, ItemIds.BanhPho);
        }
        bool validDuplicate = engine.ValidateAssembly(out string errDuplicate);
        Assert.False(validDuplicate);
        Assert.Contains("riêng biệt", errDuplicate);
        Assert.False(engine.IsSolved);

        // Invalid fake ingredients
        engine.ResetPuzzle();
        engine.PlaceIngredient(0, "Item_FakePizza");
        engine.PlaceIngredient(1, "Item_FakeCheese");
        engine.PlaceIngredient(2, "Item_FakeTomato");
        engine.PlaceIngredient(3, "Item_FakeSauce");
        engine.PlaceIngredient(4, "Item_FakeSpice");

        bool validFake = engine.ValidateAssembly(out string errFake);
        Assert.False(validFake);
        Assert.False(engine.IsSolved);

        // Out-of-bounds slot index handling
        Assert.False(engine.PlaceIngredient(-1, ItemIds.BanhPho));
        Assert.False(engine.PlaceIngredient(5, ItemIds.BanhPho));
        Assert.False(engine.PlaceIngredient(99, ItemIds.BanhPho));
        Assert.False(engine.PlaceIngredient(0, ""));
        Assert.False(engine.PlaceIngredient(0, "   "));
        Assert.False(engine.RemoveIngredient(-1));
        Assert.False(engine.RemoveIngredient(10));
    }

    [Fact]
    public void QuestOrder_PhoAssembly_AuthenticCulinaryInversion_ProvidesContextualCorrections()
    {
        var engine = new PhoCookingEngine();

        // Error 1: Broth placed at bottom (slot 0)
        engine.PlaceIngredient(0, ItemIds.NuocDung);
        engine.PlaceIngredient(1, ItemIds.BanhPho);
        engine.PlaceIngredient(2, ItemIds.ThitBo);
        engine.PlaceIngredient(3, ItemIds.HanhLa);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool validBrothBottom = engine.ValidateAssembly(out string errBrothBottom);
        Assert.False(validBrothBottom);
        Assert.Contains("Nước dùng mà chan trước", errBrothBottom);

        // Error 2: Scallions placed before meat (slot 1)
        engine.ResetPuzzle();
        engine.PlaceIngredient(0, ItemIds.BanhPho);
        engine.PlaceIngredient(1, ItemIds.HanhLa);
        engine.PlaceIngredient(2, ItemIds.ThitBo);
        engine.PlaceIngredient(3, ItemIds.NuocDung);
        engine.PlaceIngredient(4, ItemIds.ThaoMoc);

        bool validScallionBeforeMeat = engine.ValidateAssembly(out string errScallion);
        Assert.False(validScallionBeforeMeat);
        Assert.Contains("Thịt bò thái mỏng phải trải đều trên bánh phở trước", errScallion);
    }

    [Fact]
    public void QuestOrder_RecordObjectiveProgress_OnInactiveOrNonExistentQuest_ReturnsFalse()
    {
        var eventBus = new EventBus();
        var qm = new QuestManager(eventBus);
        HanoiQuests.RegisterAll(qm);

        // Attempting to advance inactive quest directly
        bool advancedInactive = qm.RecordObjectiveProgress(QuestIds.Hanoi02, "Objective_AssemblePho", 1);
        Assert.False(advancedInactive);

        // Attempting to advance non-existent quest
        bool advancedNonExistent = qm.RecordObjectiveProgress("Quest_NonExistent_99", "AnyObjective", 1);
        Assert.False(advancedNonExistent);

        // Attempting to advance non-existent objective on active quest
        qm.StartQuest(QuestIds.Hanoi01);
        bool advancedBadObj = qm.RecordObjectiveProgress(QuestIds.Hanoi01, "Objective_NonExistent", 1);
        Assert.False(advancedBadObj);
    }

    [Fact]
    public void QuestOrder_GateInteraction_BeforeQuestsCompleted_DoesNotUnlockGateOrCutscene()
    {
        var gate = new LongBienGateInteractable();
        var flags = QuestFlags.CreateDefault();
        var context = new InteractionContext(
            PlayerPosition: gate.Position,
            Inventory: new Inventory(),
            QuestManager: new QuestManager(),
            QuestFlags: flags,
            EventBus: new EventBus(),
            AudioService: new NullAudioService()
        );

        // State: Q1 not done, Q2 not done
        var resultQ1 = gate.Interact(context);
        Assert.Contains("Mây hãy giúp Bà An nấu phở trước đã", resultQ1.DialogueText);
        Assert.False(flags.Gate_LongBien_Unlocked);
        Assert.False(flags.GrayShadow_LetterRead);

        // State: Q1 done, Q2 done, but Q3 not done
        flags.Q1_Completed = true;
        flags.Q2_Completed = true;
        var resultQ2 = gate.Interact(context);
        Assert.Contains("sắp xếp lại đường đi", resultQ2.DialogueText);
        Assert.False(flags.Gate_LongBien_Unlocked);
        Assert.False(flags.GrayShadow_LetterRead);
    }

    // =========================================================================
    // SECTION 3: DIALOGUE & UI STATE SPAMMING STRESS TESTS
    // =========================================================================

    [Fact]
    public void Dialogue_RapidKeySpamming_MaintainsConsistentState()
    {
        var audio = new NullAudioService();
        var box = new DialogueBox(audio);
        var input = new MockInputService();

        box.Show("Bà An", "Chào Bé Mây, hôm nay phố cổ thật đẹp!", "Avatar_BaAn");

        // Simulate 500 ticks of chaotic user inputs: rapid confirms, cancels, clicks
        var rand = new Random(42);
        for (int i = 0; i < 500; i++)
        {
            input.ConfirmPressed = (rand.Next(3) == 0);
            input.CancelPressed = (rand.Next(10) == 0);
            input.InteractPressed = (rand.Next(5) == 0);
            input.IsPointerJustPressed = (rand.Next(4) == 0);

            float dt = (float)(rand.NextDouble() * 0.05 + 0.005);
            box.Update(dt, input);
        }

        // Dialogue must either have dismissed cleanly or completed typing without exceptions
        Assert.True(!box.IsActive || box.IsTypingComplete);
        Assert.True(box.VisibleCharCount <= box.CurrentText.Length);
    }

    [Fact]
    public void Dialogue_FastForward_WhileChoicesPending_DoesNotBypassOrCorruptChoices()
    {
        var service = new DialogueService();
        var node = new DialogueNode
        {
            Id = "Node_Choice",
            Speaker = "Bé Mây",
            Text = "Chúng ta nên làm gì trước đây?",
            Branches = new List<DialogueBranch>
            {
                new() { ChoiceId = "Choice_1", ChoiceText = "Đi nhặt bánh phở", NextNodeId = "Node_Pho" },
                new() { ChoiceId = "Choice_2", ChoiceText = "Hỏi thăm Bút Chì", NextNodeId = "Node_ButChi" }
            }
        };

        var box = new DialogueBox(new NullAudioService());
        box.Show(node, service);

        // Fast forward typewriter while choices are pending
        box.FastForward();
        Assert.True(box.IsTypingComplete);
        Assert.Equal(2, box.CurrentBranches.Count);
        Assert.Equal(0, box.SelectedChoiceIndex);

        // Rapidly call FastForward again
        for (int i = 0; i < 20; i++)
        {
            box.FastForward();
        }

        // Choices must remain fully intact and active
        Assert.True(box.IsActive);
        Assert.Equal(2, box.CurrentBranches.Count);
        Assert.Equal("Choice_1", box.CurrentBranches[0].ChoiceId);
        Assert.Equal("Choice_2", box.CurrentBranches[1].ChoiceId);
    }

    [Fact]
    public void Dialogue_SelectInvalidBranchIndicesAndIds_ReturnsNullGracefully()
    {
        var service = new DialogueService();
        string json = """
        {
            "dialogues": [
                {
                    "dialogueId": "Test_Graph",
                    "startNodeId": "Node_Start",
                    "nodes": [
                        {
                            "id": "Node_Start",
                            "speaker": "Mây",
                            "text": "Câu hỏi chọn lựa",
                            "branches": [
                                { "choiceId": "Opt_A", "choiceText": "Lựa chọn A", "nextNodeId": "Node_EndA" }
                            ]
                        },
                        {
                            "id": "Node_EndA",
                            "speaker": "Bút Chì",
                            "text": "Đã chọn A"
                        }
                    ]
                }
            ]
        }
        """;
        service.LoadDialogueJson(json);
        service.StartDialogue("Test_Graph");

        // Selecting invalid choice index: negative, out of range
        Assert.Null(service.SelectChoice(-1));
        Assert.Null(service.SelectChoice(1));
        Assert.Null(service.SelectChoice(999));

        // Selecting invalid choice IDs
        Assert.Null(service.SelectChoice("NonExistent_Choice"));
        Assert.Null(service.SelectChoice(""));
        Assert.Null(service.SelectChoice((string)null!));

        // State remains intact on Node_Start
        Assert.NotNull(service.CurrentNode);
        Assert.Equal("Node_Start", service.CurrentNode.Id);

        // Valid selection works
        var next = service.SelectChoice("Opt_A");
        Assert.NotNull(next);
        Assert.Equal("Node_EndA", next.Id);
    }

    [Fact]
    public void Dialogue_ConcurrentOrOverlappingTriggers_HandlesSwitchingSafely()
    {
        var service = new DialogueService();
        string json = """
        {
            "dialogues": [
                {
                    "dialogueId": "Graph_A",
                    "startNodeId": "Node_A1",
                    "nodes": [
                        { "id": "Node_A1", "speaker": "A", "text": "Text A", "nextNodeId": "Node_A2" },
                        { "id": "Node_A2", "speaker": "A", "text": "End A" }
                    ]
                },
                {
                    "dialogueId": "Graph_B",
                    "startNodeId": "Node_B1",
                    "nodes": [
                        { "id": "Node_B1", "speaker": "B", "text": "Text B" }
                    ]
                }
            ]
        }
        """;
        service.LoadDialogueJson(json);

        // Start Graph A
        service.StartDialogue("Graph_A");
        Assert.Equal("Node_A1", service.CurrentNode?.Id);

        // Switch to Graph B mid-dialogue without ending Graph A
        service.StartDialogue("Graph_B");
        Assert.Equal("Node_B1", service.CurrentNode?.Id);
        Assert.Equal("Graph_B", service.ActiveGraph?.DialogueId);

        // Attempt starting non-existent graph
        service.StartDialogue("NonExistentGraph");
        Assert.Null(service.CurrentNode);
        Assert.Null(service.ActiveGraph);
        Assert.False(service.IsDialogueActive);
    }

    [Fact]
    public void Dialogue_Typewriter_BoundaryFractionsAndTimeDeltas_DoesNotOverflowOrUnderflow()
    {
        var box = new DialogueBox(new NullAudioService());
        box.Show("Mây", "Hà Nội mùa thu", "Avatar_May");

        // Edge case deltas: 0, negative, huge
        box.Update(0f);
        Assert.Equal(0, box.VisibleCharCount);

        box.Update(-5.0f);
        Assert.Equal(0, box.VisibleCharCount);

        box.Update(0.000001f);
        Assert.True(box.VisibleCharCount >= 0);

        box.Update(100000.0f);
        Assert.Equal(box.CurrentText.Length, box.VisibleCharCount);
        Assert.True(box.IsTypingComplete);
    }

    // =========================================================================
    // SECTION 4: BOUNDARY AND SPATIAL EDGE CASES
    // =========================================================================

    [Fact]
    public void SpatialGrid_BoundaryPushAttempts_CannotPushEntitiesOutsideGrid()
    {
        var grid = new SpatialGridEngine();

        // 1. Static boundary wall at (0, 0) cannot be moved in any direction
        Assert.False(grid.PushObject("Wall_Top_0", Direction.Up));
        Assert.False(grid.PushObject("Wall_Top_0", Direction.Down));
        Assert.False(grid.PushObject("Wall_Top_0", Direction.Left));
        Assert.False(grid.PushObject("Wall_Top_0", Direction.Right));

        // 2. Fruit Stall (1x1 at x=2, y=3)
        // Push Fruit Stall Left to (1, 3) -> valid
        Assert.True(grid.PushObject("Obj_FruitStall", Direction.Left));
        // Push Fruit Stall Left to (0, 3) -> valid
        Assert.True(grid.PushObject("Obj_FruitStall", Direction.Left));
        // Attempt pushing Fruit Stall Left beyond x=0 -> must fail
        Assert.False(grid.PushObject("Obj_FruitStall", Direction.Left));

        // Attempt pushing Fruit Stall Down into row 4 (occupied by Wall_Bottom_0) -> must fail
        Assert.False(grid.PushObject("Obj_FruitStall", Direction.Down));

        // Attempt pushing unknown object ID
        Assert.False(grid.PushObject("Ghost_Cart_999", Direction.Left));
    }

    [Fact]
    public void SpatialGrid_DirectionalPushConstraints_CannotPushAgainstConstraints()
    {
        var grid = new SpatialGridEngine();

        // Flower Cart (1x2 at x=2, y=1) has CanPushHorizontally = false
        Assert.False(grid.PushObject("Obj_FlowerCart", Direction.Left));
        Assert.False(grid.PushObject("Obj_FlowerCart", Direction.Right));

        // Cyclo (2x1 at x=3, y=2) has CanPushVertically = false
        Assert.False(grid.PushObject("Obj_Cyclo", Direction.Up));
        Assert.False(grid.PushObject("Obj_Cyclo", Direction.Down));
    }

    [Fact]
    public void SpatialGrid_EntityCollisionOverlaps_CannotPushIntoOccupiedCells()
    {
        var grid = new SpatialGridEngine();

        // In default layout, Fruit Stall is at (2, 3). Flower Cart occupies (2, 1) and (2, 2).
        // Pushing Fruit Stall UP towards (2, 2) must collide with Flower Cart and fail.
        bool pushedUpIntoFlowerCart = grid.PushObject("Obj_FruitStall", Direction.Up);
        Assert.False(pushedUpIntoFlowerCart, "Pushing Fruit Stall into Flower Cart must be blocked by collision");

        // Ceramic Crates is at (3, 3). Pushing Fruit Stall RIGHT towards (3, 3) must collide and fail.
        bool pushedRightIntoCrates = grid.PushObject("Obj_FruitStall", Direction.Right);
        Assert.False(pushedRightIntoCrates, "Pushing Fruit Stall into Ceramic Crates must be blocked by collision");
    }

    [Fact]
    public void SpatialGrid_ImpossibleLayouts_PathfindingFailsGracefullyWithoutLoop()
    {
        var grid = new SpatialGridEngine();

        // Default layout is blocked (requires solving)
        Assert.False(grid.IsPathClear());
        Assert.Null(grid.FindPath());

        // Now deliberately block the exit gate (6, 2)
        grid.Entities.First(e => e.Id == "Obj_TeaStall").X = 6;
        grid.Entities.First(e => e.Id == "Obj_TeaStall").Y = 2;

        Assert.False(grid.IsPathClear());
        Assert.Null(grid.FindPath());

        // Block entrance (0, 2)
        grid.Entities.First(e => e.Id == "Obj_TeaStall").X = 0;
        grid.Entities.First(e => e.Id == "Obj_TeaStall").Y = 2;

        Assert.False(grid.IsPathClear());
        Assert.Null(grid.FindPath());

        // Reset grid restores initial positions cleanly
        grid.ResetGrid();
        Assert.False(grid.IsPathClear());
    }

    [Fact]
    public void HanoiMap_PlayerOutOfBoundsPositions_ClampedSafelyByCollisionWorld()
    {
        var world = new CollisionWorld
        {
            MapBounds = new AABB(0, 0, 1600, 900)
        };

        float halfW = 12f;
        float height = 14f;
        Vector2 vel = new(500f, 500f);

        // Case 1: Player positioned far beyond top-left (-500, -500)
        Vector2 extremeNegative = new(-500f, -500f);
        Vector2 clampedNeg = world.MoveAndSlide(extremeNegative, halfW, height, ref vel, 0.016f);
        Assert.Equal(12f, clampedNeg.X);
        Assert.Equal(14f, clampedNeg.Y);
        Assert.Equal(0f, vel.X);
        Assert.Equal(0f, vel.Y);

        // Case 2: Player positioned far beyond bottom-right (99999, 99999)
        vel = new(1000f, 1000f);
        Vector2 extremePositive = new(99999f, 99999f);
        Vector2 clampedPos = world.MoveAndSlide(extremePositive, halfW, height, ref vel, 0.016f);
        Assert.Equal(1600f - halfW, clampedPos.X);
        Assert.Equal(900f, clampedPos.Y);
        Assert.Equal(0f, vel.X);
        Assert.Equal(0f, vel.Y);

        // Case 3: PlayerSpawn in Level1State with NaN or Infinity resets safely during Sanitize()
        var level1 = Level1State.CreateDefault();
        level1.PlayerSpawnX = double.NaN;
        level1.PlayerSpawnY = double.PositiveInfinity;
        level1.Sanitize();
        Assert.Equal(120.0, level1.PlayerSpawnX);
        Assert.Equal(250.0, level1.PlayerSpawnY);
    }

    // =========================================================================
    // SECTION 5: CONTINUOUS MULTI-CYCLE PLAYTHROUGH PERSISTENCE
    // =========================================================================

    [Fact]
    public void MultiCycle_CompleteHanoi_SaveReload_TravelToHue_RejectsLockedRegions()
    {
        var driver = new TestSimulationDriver();

        // 1. Complete Hanoi full slice
        driver.SimulateGatherAllPhoIngredients();
        Assert.True(driver.SimulateSolvePhoCooking());
        Assert.True(driver.SimulateClearStallPath());
        driver.SimulateLevelClimax();

        // Verify climax state
        Assert.Equal(100.0f, driver.CurrentData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(driver.CurrentData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.True(driver.CurrentData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));

        // 2. Save progress
        driver.SaveLoadService.Save(driver.CurrentData);

        // 3. Reboot into fresh driver
        var rebootDriver = new TestSimulationDriver(driver.Storage);
        rebootDriver.CurrentData = rebootDriver.SaveLoadService.Load();

        // 4. Test World Map Region Selection
        // Hanoi is completed
        bool canSelectHanoi = rebootDriver.WorldMapService.TrySelectRegion(RegionIds.Hanoi, rebootDriver.CurrentData.WorldProgression, out _);
        Assert.True(canSelectHanoi);

        // Huế is unlocked and selectable
        bool canSelectHue = rebootDriver.WorldMapService.TrySelectRegion(RegionIds.Hue, rebootDriver.CurrentData.WorldProgression, out string hueErr);
        Assert.True(canSelectHue);
        Assert.Empty(hueErr);

        // Locked regions: Hội An, Sài Gòn, invalid regions
        bool canSelectHoiAn = rebootDriver.WorldMapService.TrySelectRegion(RegionIds.HoiAn, rebootDriver.CurrentData.WorldProgression, out string hoianErr);
        Assert.False(canSelectHoiAn);
        Assert.NotEmpty(hoianErr);

        bool canSelectSaigon = rebootDriver.WorldMapService.TrySelectRegion(RegionIds.Saigon, rebootDriver.CurrentData.WorldProgression, out string saigonErr);
        Assert.False(canSelectSaigon);
        Assert.NotEmpty(saigonErr);

        bool canSelectMars = rebootDriver.WorldMapService.TrySelectRegion("MarsColony", rebootDriver.CurrentData.WorldProgression, out string marsErr);
        Assert.False(canSelectMars);
        Assert.NotEmpty(marsErr);

        bool canSelectEmpty = rebootDriver.WorldMapService.TrySelectRegion("", rebootDriver.CurrentData.WorldProgression, out _);
        Assert.False(canSelectEmpty);
    }

    [Fact]
    public void MultiCycle_SaveReloadLoop_MaintainsAlbumAndProgressionIntegrityAcrossCycles()
    {
        var storage = new MockSaveStorage();
        var saveService = new SaveLoadService(storage, "Data/savegame.json");
        var albumService = new AlbumService();

        // CYCLE 0: Initial Playthrough Completion
        var initialDriver = new TestSimulationDriver(storage);
        initialDriver.SimulateGatherAllPhoIngredients();
        initialDriver.SimulateSolvePhoCooking();
        initialDriver.SimulateClearStallPath();
        initialDriver.SimulateLevelClimax();
        saveService.Save(initialDriver.CurrentData);

        // 5 Sequential modification & reboot cycles
        for (int cycle = 1; cycle <= 5; cycle++)
        {
            // Reboot from storage
            var reloadedData = saveService.Load();

            // Verify core invariants hold in every cycle
            Assert.Equal("1.0.0", reloadedData.SaveVersion);
            Assert.Equal(100.0f, reloadedData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
            Assert.True(reloadedData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
            Assert.True(reloadedData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
            Assert.True(reloadedData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
            Assert.True(reloadedData.Level1State.QuestFlags.Level1_Finished);
            Assert.Equal(5, reloadedData.Level1State.InventoryItems.Count);

            // Verify Album items
            Assert.True(albumService.IsJournalPageUnlocked(reloadedData.CollectionAlbum, "JournalPage_Hanoi_01"));
            Assert.False(albumService.IsJournalPageUnlocked(reloadedData.CollectionAlbum, "JournalPage_Hue_02"));
            Assert.True(albumService.IsStampUnlocked(reloadedData.CollectionAlbum, "Stamp_HoaSen_Hanoi"));

            // Perform cycle-specific additions
            if (cycle == 1)
            {
                albumService.UnlockFactCard(reloadedData.CollectionAlbum, AlbumIds.FactCardPho);
                albumService.UnlockFactCard(reloadedData.CollectionAlbum, AlbumIds.FactCardLongBien);
            }
            else if (cycle == 2)
            {
                albumService.UnlockFactCard(reloadedData.CollectionAlbum, AlbumIds.FactCardSenTayHo);
                albumService.UnlockFactCard(reloadedData.CollectionAlbum, AlbumIds.FactCardPhoCo);
            }
            else if (cycle == 3)
            {
                // Attempt duplicate awards - album service must deduplicate
                albumService.AwardJournalPage(reloadedData.CollectionAlbum, "JournalPage_Hanoi_01");
                albumService.AwardTravelStamp(reloadedData.CollectionAlbum, "Stamp_HoaSen_Hanoi");
                albumService.UnlockFactCard(reloadedData.CollectionAlbum, AlbumIds.FactCardPho);
            }
            else if (cycle == 4)
            {
                reloadedData.AudioSettings.MasterVolume = 0.65;
                reloadedData.AudioSettings.MusicVolume = 0.45;
                reloadedData.Level1State.PlayerSpawnX = 980.0;
                reloadedData.Level1State.PlayerSpawnY = 420.0;
            }

            // Persist at end of cycle
            saveService.Save(reloadedData);
        }

        // Final verification after 5 full reload/save cycles
        var finalData = saveService.Load();
        Assert.Single(finalData.CollectionAlbum.CollectedJournalPages);
        Assert.Single(finalData.CollectionAlbum.CollectedStamps);
        Assert.Equal(4, finalData.CollectionAlbum.UnlockedFactCards.Count);
        Assert.Equal(0.65, finalData.AudioSettings.MasterVolume, 2);
        Assert.Equal(0.45, finalData.AudioSettings.MusicVolume, 2);
        Assert.Equal(980.0, finalData.Level1State.PlayerSpawnX);
        Assert.Equal(420.0, finalData.Level1State.PlayerSpawnY);
    }
}
