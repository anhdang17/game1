using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Xunit;

using GameProject.Core.Album;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Puzzles;
using GameProject.Core.Quests;
using GameProject.Core.Simulation;
using GameProject.Core.State;
using GameProject.Core.WorldMap;

namespace GameProject.Tests;

/// <summary>
/// Adversarial Iteration 2 Gate Verification Suite.
/// Empirically challenges:
/// 1. QuestObjective overflow robustness across all boundary conditions.
/// 2. SaveData nested sanitization and persistence round-trip with corrupted/extreme inputs.
/// 3. GameStateManager and SimulationDriver for state leaks, underflows, and unhandled exceptions.
/// 4. Production assembly resolution and absence of type shadowing.
/// </summary>
public class AdversarialIteration2GateTests
{
    // =========================================================================
    // 1. QUEST OBJECTIVE OVERFLOW ROBUSTNESS ACROSS ALL BOUNDARY CONDITIONS
    // =========================================================================

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(-9999, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void QuestObjective_TargetCount_BoundaryInitialization_ClampedSafely(int inputTarget, int expectedTarget)
    {
        // Constructor path
        var obj1 = new QuestObjective("obj1", "desc1", inputTarget);
        Assert.Equal(expectedTarget, obj1.TargetCount);
        Assert.Equal(0, obj1.CurrentCount);
        Assert.False(obj1.IsCompleted);

        // Object initializer path
        var obj2 = new QuestObjective { TargetCount = inputTarget };
        Assert.Equal(expectedTarget, obj2.TargetCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    [InlineData(int.MinValue)]
    public void QuestObjective_IncrementProgress_NonPositiveAmounts_RejectedWithoutMutation(int invalidAmount)
    {
        var obj = new QuestObjective("obj", "desc", targetCount: 5);
        bool changed = obj.IncrementProgress(invalidAmount);

        Assert.False(changed);
        Assert.Equal(0, obj.CurrentCount);
        Assert.False(obj.IsCompleted);
    }

    [Fact]
    public void QuestObjective_IncrementProgress_IntMaxValue_FromZero_ClampsToTargetCount()
    {
        var obj = new QuestObjective("obj", "desc", targetCount: 5);
        bool changed = obj.IncrementProgress(int.MaxValue);

        Assert.True(changed);
        Assert.Equal(5, obj.CurrentCount);
        Assert.True(obj.IsCompleted);

        // Subsequent increment returns false (already completed)
        Assert.False(obj.IncrementProgress(1));
        Assert.Equal(5, obj.CurrentCount);
    }

    [Fact]
    public void QuestObjective_IncrementProgress_IntMaxValue_FromNearTarget_ClampsWithoutOverflow()
    {
        var obj = new QuestObjective("obj", "desc", targetCount: 10);
        obj.IncrementProgress(9);
        Assert.Equal(9, obj.CurrentCount);

        // Exploit attempt: 9 + int.MaxValue would wrap to negative if unchecked
        bool changed = obj.IncrementProgress(int.MaxValue);

        Assert.True(changed);
        Assert.Equal(10, obj.CurrentCount);
        Assert.True(obj.IsCompleted);
    }

    [Fact]
    public void QuestObjective_IncrementProgress_WhenTargetCountIsIntMaxValue_HandlesBoundaries()
    {
        var obj = new QuestObjective("massive", "massive target", targetCount: int.MaxValue);
        Assert.Equal(int.MaxValue, obj.TargetCount);

        // Increment near max: int.MaxValue - 2
        bool step1 = obj.IncrementProgress(int.MaxValue - 2);
        Assert.True(step1);
        Assert.Equal(int.MaxValue - 2, obj.CurrentCount);
        Assert.False(obj.IsCompleted);

        // Increment by 1
        bool step2 = obj.IncrementProgress(1);
        Assert.True(step2);
        Assert.Equal(int.MaxValue - 1, obj.CurrentCount);
        Assert.False(obj.IsCompleted);

        // Increment by 5: exceeds remaining needed (1), must clamp to int.MaxValue
        bool step3 = obj.IncrementProgress(5);
        Assert.True(step3);
        Assert.Equal(int.MaxValue, obj.CurrentCount);
        Assert.True(obj.IsCompleted);

        // Further increments rejected
        Assert.False(obj.IncrementProgress(1));
        Assert.Equal(int.MaxValue, obj.CurrentCount);
    }

    [Theory]
    [InlineData(int.MinValue, 0)]
    [InlineData(-100, 0)]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    [InlineData(int.MaxValue, 5)]
    public void QuestObjective_SetProgress_ClampedToValidRange(int setCount, int expectedCount)
    {
        var obj = new QuestObjective("obj", "desc", targetCount: 5);
        obj.SetProgress(setCount);

        Assert.Equal(expectedCount, obj.CurrentCount);
        Assert.Equal(expectedCount == 5, obj.IsCompleted);
    }

    [Fact]
    public void QuestObjective_Reset_RestoresCleanZeroState()
    {
        var obj = new QuestObjective("obj", "desc", targetCount: 5);
        obj.IncrementProgress(5);
        Assert.True(obj.IsCompleted);

        obj.Reset();
        Assert.Equal(0, obj.CurrentCount);
        Assert.False(obj.IsCompleted);

        // Can progress again after reset
        Assert.True(obj.IncrementProgress(2));
        Assert.Equal(2, obj.CurrentCount);
    }

    [Fact]
    public void QuestObjective_RandomFuzzing_50000Operations_NeverViolatesInvariants()
    {
        var random = new Random(1337);
        var obj = new QuestObjective("fuzz", "fuzz target", targetCount: 100);

        for (int i = 0; i < 50_000; i++)
        {
            int op = random.Next(4);
            switch (op)
            {
                case 0:
                    // Random increment (can be negative, 0, huge, or small)
                    int amount = random.Next(-1000, 1000);
                    if (random.Next(10) == 0) amount = int.MaxValue;
                    if (random.Next(10) == 0) amount = int.MinValue;
                    obj.IncrementProgress(amount);
                    break;
                case 1:
                    // Random SetProgress
                    int setCount = random.Next(-100, 200);
                    obj.SetProgress(setCount);
                    break;
                case 2:
                    if (random.Next(50) == 0)
                        obj.Reset();
                    break;
                case 3:
                    // Standard small increment
                    obj.IncrementProgress(1);
                    break;
            }

            // Invariant check after EVERY single operation
            Assert.True(obj.CurrentCount >= 0, $"CurrentCount underflowed to {obj.CurrentCount} at iteration {i}");
            Assert.True(obj.CurrentCount <= obj.TargetCount, $"CurrentCount exceeded TargetCount ({obj.CurrentCount} > {obj.TargetCount}) at iteration {i}");
            Assert.Equal(obj.CurrentCount >= obj.TargetCount, obj.IsCompleted);
        }
    }

    [Fact]
    public void QuestManager_RecordObjectiveProgress_OverflowInput_ClampsWithoutCorruptingQuest()
    {
        var bus = new EventBus();
        var qm = new QuestManager(bus);
        HanoiQuests.RegisterAll(qm);

        qm.StartQuest(HanoiQuests.Quest1Id);

        // Advance BanhPho with int.MaxValue
        bool progressed = qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.BanhPho, int.MaxValue);
        Assert.True(progressed);

        var q1 = qm.GetQuest(HanoiQuests.Quest1Id);
        Assert.NotNull(q1);
        var obj = q1.GetObjective(ItemIds.BanhPho);
        Assert.NotNull(obj);
        Assert.Equal(1, obj.CurrentCount);
        Assert.True(obj.IsCompleted);

        // Negative increment via QuestManager
        bool negative = qm.RecordObjectiveProgress(HanoiQuests.Quest1Id, ItemIds.ThitBo, -99);
        Assert.False(negative);
        var thitBo = q1.GetObjective(ItemIds.ThitBo);
        Assert.NotNull(thitBo);
        Assert.Equal(0, thitBo.CurrentCount);
    }

    // =========================================================================
    // 2. SAVEDATA NESTED SANITIZATION & PERSISTENCE ROUND-TRIP
    // =========================================================================

    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    [InlineData("not json at all")]
    [InlineData("{\"truncated\":")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"just a string\"")]
    [InlineData("123456789")]
    [InlineData("null")]
    [InlineData("{\"saveVersion\": 12345}")]
    public void SaveLoadService_ExtremeCorruptedJson_RecoversCleanDefault_AndCreatesCorruptBackup(string corruptJson)
    {
        var storage = new InMemorySaveStorage();
        var bus = new EventBus();
        GameLoadedEvent? loadedEvent = null;
        bus.Subscribe<GameLoadedEvent>(e => loadedEvent = e);

        string savePath = "Data/savegame.json";
        storage.WriteAllText(savePath, corruptJson);

        var service = new SaveLoadService(savePath, storage, bus);
        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal(SaveData.CurrentSaveVersion, loaded.SaveVersion);
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        Assert.NotNull(loaded.QuestFlags);
        Assert.NotNull(loaded.Level1State.InventoryItems);
        Assert.NotNull(loaded.CollectionAlbum.CollectedStamps);
        Assert.NotNull(loadedEvent);
        Assert.True(loadedEvent.WasCorruptedAndRecovered);

        // Verify .corrupt file was created
        Assert.True(storage.FileExists(savePath + SaveLoadService.CorruptSuffix));

        // Verify auto-repair persisted valid JSON back to savePath
        string repairedJson = storage.ReadAllText(savePath);
        Assert.False(string.IsNullOrWhiteSpace(repairedJson));
        Assert.Contains(SaveData.CurrentSaveVersion, repairedJson);
    }

    [Fact]
    public void SaveData_ExhaustiveNullSubProperties_RepairedBySanitize_WithZeroExceptions()
    {
        string exhaustiveNullsJson = """
        {
            "saveVersion": null,
            "lastSavedTimestamp": "2026-11-08T00:00:00Z",
            "playerProfile": {
                "playerName": null,
                "currentRegionId": null,
                "totalPlayTimeSeconds": -99999.0
            },
            "worldProgression": {
                "unlockedRegions": null,
                "completedRegions": null,
                "regionColorRestoration": null
            },
            "level1State": {
                "playerSpawnX": 9999.0,
                "playerSpawnY": -8888.0,
                "activeQuestId": null,
                "questFlags": null,
                "inventoryItems": null,
                "puzzle3ObjectPositions": null
            },
            "collectionAlbum": {
                "collectedJournalPages": null,
                "collectedStamps": null,
                "unlockedFactCards": null
            },
            "audioSettings": {
                "masterVolume": 999.0,
                "musicVolume": -50.0,
                "sfxVolume": 25.0
            }
        }
        """;

        var storage = new InMemorySaveStorage();
        storage.WriteAllText("Data/nulls.json", exhaustiveNullsJson);
        var service = new SaveLoadService("Data/nulls.json", storage);

        var loaded = service.Load();
        Assert.NotNull(loaded);

        // 1. Root & SaveVersion
        Assert.Equal(SaveData.CurrentSaveVersion, loaded.SaveVersion);

        // 2. PlayerProfile
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        Assert.Equal("Hanoi", loaded.PlayerProfile.CurrentRegionId);
        Assert.Equal(0.0, loaded.PlayerProfile.TotalPlayTimeSeconds);
        loaded.PlayerProfile.AddPlayTime(15.5);
        Assert.Equal(15.5, loaded.PlayerProfile.TotalPlayTimeSeconds);

        // 3. WorldProgression
        Assert.NotNull(loaded.WorldProgression.UnlockedRegions);
        Assert.Contains(RegionIds.Hanoi, loaded.WorldProgression.UnlockedRegions);
        Assert.NotNull(loaded.WorldProgression.CompletedRegions);
        Assert.NotNull(loaded.WorldProgression.RegionColorRestoration);
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(RegionIds.Hanoi));
        Assert.False(loaded.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.Equal(0.0f, loaded.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        // Mutate WorldProgression without NRE
        Assert.True(loaded.WorldProgression.UnlockRegion(RegionIds.Hue));
        Assert.True(loaded.WorldProgression.CompleteRegion(RegionIds.Hanoi));
        Assert.Equal(100.0f, loaded.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        // 4. Level1State
        Assert.Equal("Quest_Hanoi_01", loaded.Level1State.ActiveQuestId);
        Assert.Equal(9999.0, loaded.Level1State.PlayerSpawnX);
        Assert.Equal(-8888.0, loaded.Level1State.PlayerSpawnY);
        Assert.NotNull(loaded.Level1State.QuestFlags);
        Assert.NotNull(loaded.QuestFlags);
        Assert.False(loaded.QuestFlags.Q1_Started);

        // Mutate Level1State without NRE
        loaded.QuestFlags.Q1_Started = true;
        Assert.True(loaded.Level1State.QuestFlags.Q1_Started);

        Assert.NotNull(loaded.Level1State.InventoryItems);
        loaded.Level1State.InventoryItems.Add(ItemIds.BanhPho);
        Assert.Single(loaded.Level1State.InventoryItems);

        Assert.NotNull(loaded.Level1State.Puzzle3ObjectPositions);
        loaded.Level1State.Puzzle3ObjectPositions.Add(new Puzzle3ObjectPosition("cart", 1, 2));
        Assert.Single(loaded.Level1State.Puzzle3ObjectPositions);

        // 5. CollectionAlbum
        Assert.NotNull(loaded.CollectionAlbum.CollectedJournalPages);
        Assert.NotNull(loaded.CollectionAlbum.CollectedStamps);
        Assert.NotNull(loaded.CollectionAlbum.UnlockedFactCards);

        // Mutate CollectionAlbum without NRE
        Assert.True(loaded.CollectionAlbum.AddJournalPage(AlbumIds.HanoiJournalPage));
        Assert.True(loaded.CollectionAlbum.AddStamp(AlbumIds.HanoiLotusStamp));
        Assert.True(loaded.CollectionAlbum.AddFactCard(AlbumIds.FactCardPho));

        Assert.True(loaded.CollectionAlbum.HasJournalPage(AlbumIds.HanoiJournalPage));
        Assert.True(loaded.CollectionAlbum.HasStamp(AlbumIds.HanoiLotusStamp));
        Assert.True(loaded.CollectionAlbum.HasFactCard(AlbumIds.FactCardPho));

        // 6. AudioSettings
        Assert.Equal(1.0, loaded.AudioSettings.MasterVolume);
        Assert.Equal(0.0, loaded.AudioSettings.MusicVolume);
        Assert.Equal(1.0, loaded.AudioSettings.SfxVolume);
    }

    [Fact]
    public void SaveData_Level1State_PlayerSpawnCoordinates_NaNAndInfinity_ResetToDefaults()
    {
        var level1 = new Level1State
        {
            PlayerSpawnX = double.NaN,
            PlayerSpawnY = double.PositiveInfinity
        };

        level1.Sanitize();

        Assert.Equal(120.0, level1.PlayerSpawnX);
        Assert.Equal(250.0, level1.PlayerSpawnY);
    }

    [Fact]
    public void SaveData_VietnameseUtf8DiacriticsAndEmojis_RoundTripFidelity()
    {
        var storage = new InMemorySaveStorage();
        var service = new SaveLoadService("Data/utf8_save.json", storage);

        var save = SaveData.CreateDefault();
        string richVietnamese = "Bé Mây & Bút Chì: Hành trình khôi phục sắc màu tại Thủ đô Hà Nội ngàn năm văn hiến! 🇻🇳 🍜 🌸 🚲";
        save.PlayerProfile.PlayerName = richVietnamese;
        save.Level1State.InventoryItems.Add("Bánh Phở Tươi 🍜");
        save.Level1State.InventoryItems.Add("Nước Dùng Bò Hầm Quế Hồi 🍲");
        save.CollectionAlbum.AddJournalPage("Trang Sổ Ký Ức #1 - Phố Cổ Rực Rỡ");

        bool saved = service.Save(save);
        Assert.True(saved);

        var loaded = service.Load();
        Assert.NotNull(loaded);
        Assert.Equal(richVietnamese, loaded.PlayerProfile.PlayerName);
        Assert.Equal("Bánh Phở Tươi 🍜", loaded.Level1State.InventoryItems[0]);
        Assert.Equal("Nước Dùng Bò Hầm Quế Hồi 🍲", loaded.Level1State.InventoryItems[1]);
        Assert.True(loaded.CollectionAlbum.HasJournalPage("Trang Sổ Ký Ức #1 - Phố Cổ Rực Rỡ"));
    }

    [Fact]
    public void SaveData_WorldProgression_RegionColorRestoration_SanitizationOfMalformedEntries()
    {
        var wp = new WorldProgression();
        wp.RegionColorRestoration[""] = 50.0f;
        wp.RegionColorRestoration["   "] = 50.0f;
        wp.RegionColorRestoration["ValidRegion"] = 250.0f;
        wp.RegionColorRestoration["NegativeRegion"] = -100.0f;
        wp.RegionColorRestoration["NaNRegion"] = float.NaN;
        wp.RegionColorRestoration["InfRegion"] = float.PositiveInfinity;

        wp.Sanitize();

        Assert.False(wp.RegionColorRestoration.ContainsKey(""));
        Assert.False(wp.RegionColorRestoration.ContainsKey("   "));
        Assert.Equal(100.0f, wp.RegionColorRestoration["ValidRegion"]);
        Assert.Equal(0.0f, wp.RegionColorRestoration["NegativeRegion"]);
        Assert.Equal(0.0f, wp.RegionColorRestoration["NaNRegion"]);
        Assert.Equal(0.0f, wp.RegionColorRestoration["InfRegion"]);
        Assert.Equal(0.0f, wp.RegionColorRestoration[RegionIds.Hanoi]);
    }

    // =========================================================================
    // 3. GAMESTATEMANAGER & SIMULATIONDRIVER ROBUSTNESS & STATE LEAKS
    // =========================================================================

    [Fact]
    public void GameStateManager_StackUnderflow_RepeatedPopAtBase_NeverThrowsOrUnderflows()
    {
        var bus = new EventBus();
        var sm = new GameStateManager(bus, GameState.Title);

        Assert.Equal(1, sm.StackDepth);
        Assert.Equal(GameState.Title, sm.CurrentState);

        for (int i = 0; i < 1000; i++)
        {
            bool popped = sm.PopState(out var restored);
            Assert.False(popped);
            Assert.Equal(GameState.Title, restored);
            Assert.Equal(GameState.Title, sm.CurrentState);
            Assert.Equal(1, sm.StackDepth);
            Assert.False(sm.IsOverlay);
            Assert.Equal(GameState.Title, sm.BaseState);
        }
    }

    [Fact]
    public void GameStateManager_IllegalModalTransitions_AllEnforced()
    {
        var bus = new EventBus();
        var sm = new GameStateManager(bus, GameState.Title);

        // In Title: Pause blocked
        Assert.False(sm.CanPause);
        Assert.False(sm.PushState(GameState.Pause));

        // Switch to Exploration
        sm.ChangeState(GameState.Exploration);
        Assert.True(sm.CanPause);
        Assert.True(sm.CanOpenAlbum);

        // Push MiniGamePho: Album blocked
        sm.PushState(GameState.MiniGamePho);
        Assert.False(sm.CanOpenAlbum);
        Assert.False(sm.PushState(GameState.Album));

        // Push Dialogue: Album blocked
        sm.PopState();
        sm.PushState(GameState.Dialogue);
        Assert.False(sm.CanOpenAlbum);
        Assert.False(sm.PushState(GameState.Album));

        // Push Cutscene: Album blocked
        sm.PopState();
        sm.PushState(GameState.Cutscene);
        Assert.False(sm.CanOpenAlbum);
        Assert.False(sm.PushState(GameState.Album));

        // Cannot push identical state on itself
        Assert.False(sm.PushState(GameState.Cutscene));
    }

    [Fact]
    public void GameStateManager_ReentrantStateChange_DoesNotDeadlockOrCorrupt()
    {
        var bus = new EventBus();
        GameStateManager? sm = null;
        var transitionLog = new List<GameState>();

        // Re-entrant listener: when Exploration is entered, push Dialogue
        bus.Subscribe<GameStateChangedEvent>(e =>
        {
            transitionLog.Add(e.NewState);
            if (e.NewState == GameState.Exploration && sm != null && sm.CurrentState == GameState.Exploration)
            {
                sm.PushState(GameState.Dialogue);
            }
        });

        sm = new GameStateManager(bus, GameState.Title);
        sm.ChangeState(GameState.Exploration);

        Assert.Equal(GameState.Dialogue, sm.CurrentState);
        Assert.Equal(2, sm.StackDepth);
        Assert.Contains(GameState.Title, transitionLog);
        Assert.Contains(GameState.Exploration, transitionLog);
        Assert.Contains(GameState.Dialogue, transitionLog);
    }

    [Fact]
    public void SimulationDriver_DiscreteTimeSimulation_NegativeOrZeroDeltaTime_HandledSafely()
    {
        var driver = new SimulationDriver();

        // Step with 0 deltaTime
        driver.Step(0.0f);
        Assert.Equal(1, driver.TotalTicksSimulated);
        Assert.Equal(0.0f, driver.CurrentSimulationTime);

        // AdvanceTicks with 0 ticks
        driver.AdvanceTicks(0);
        Assert.Equal(1, driver.TotalTicksSimulated);

        // AdvanceTicks with negative ticks
        driver.AdvanceTicks(-5);
        Assert.Equal(1, driver.TotalTicksSimulated);

        // AdvanceTime with 0 duration
        driver.AdvanceTime(0.0f);
        Assert.Equal(1, driver.TotalTicksSimulated);

        // AdvanceTime with negative duration
        driver.AdvanceTime(-10.0f);
        Assert.Equal(1, driver.TotalTicksSimulated);
    }

    [Fact]
    public void SimulationDriver_AdvanceUntil_TimeoutCondition_SafelyExits()
    {
        var driver = new SimulationDriver();

        // AdvanceUntil with condition that is never met
        bool reached = driver.AdvanceUntil(() => false, maxWaitSeconds: 0.05f, dtPerTick: 0.016667f);

        Assert.False(reached);
        Assert.True(driver.TotalTicksSimulated >= 3);
        Assert.True(driver.CurrentSimulationTime >= 0.05f);
    }

    [Fact]
    public void SimulationDriver_Initialize_ZeroStateLeaks_CompletePurge()
    {
        var driver = new SimulationDriver();

        // 1. Run full level 1 playthrough
        driver.SimulateCompleteFullLevel1();
        Assert.True(driver.TotalTicksSimulated > 0);
        Assert.True(driver.CurrentSimulationTime > 0.0f);
        Assert.True(driver.FiredEvents.Count > 0);
        Assert.True(driver.SaveData.Level1State.QuestFlags.Level1_Finished);
        Assert.Equal(100.0f, driver.SaveData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));

        // Change state and push modals
        driver.StateManager.ChangeState(GameState.Exploration);
        driver.StateManager.PushState(GameState.Dialogue);

        // Inject input & audio
        driver.InputService.PulseInteract();
        driver.AudioService.PlaySfx(SfxType.ItemCollected);

        // Save to storage
        driver.SaveLoadService.Save(driver.SaveData);
        Assert.True(driver.Storage.FileExists(SaveLoadService.DefaultSavePath));

        // 2. Perform Complete Reset via Initialize()
        driver.Initialize();

        // 3. Verify ZERO state leaks
        Assert.Equal(0, driver.TotalTicksSimulated);
        Assert.Equal(0.0f, driver.CurrentSimulationTime);
        Assert.Empty(driver.FiredEvents);

        // StateManager clean
        Assert.Equal(1, driver.StateManager.StackDepth);
        Assert.Equal(GameState.Title, driver.StateManager.CurrentState);

        // SaveData clean
        Assert.Equal(SaveData.CurrentSaveVersion, driver.SaveData.SaveVersion);
        Assert.Equal("Bé Mây", driver.SaveData.PlayerProfile.PlayerName);
        Assert.Equal(0.0f, driver.SaveData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.False(driver.SaveData.Level1State.QuestFlags.Level1_Finished);
        Assert.False(driver.SaveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.Empty(driver.SaveData.Level1State.InventoryItems);
        Assert.Empty(driver.SaveData.CollectionAlbum.CollectedJournalPages);
        Assert.Empty(driver.SaveData.CollectionAlbum.CollectedStamps);

        // Storage clean
        Assert.False(driver.Storage.FileExists(SaveLoadService.DefaultSavePath));

        // Audio & Input clean
        Assert.Empty(driver.AudioService.PlayedSfx);
        Assert.Equal(Vector2.Zero, driver.InputService.MovementDirection);
        Assert.False(driver.InputService.IsInteractJustPressed);

        // Quests reset
        Assert.Equal(QuestState.NotStarted, driver.QuestManager.GetQuestState(HanoiQuests.Quest1Id));
        Assert.Equal(QuestState.NotStarted, driver.QuestManager.GetQuestState(HanoiQuests.Quest2Id));
        Assert.Equal(QuestState.NotStarted, driver.QuestManager.GetQuestState(HanoiQuests.Quest3Id));
    }

    // =========================================================================
    // 4. PRODUCTION ASSEMBLY RESOLUTION & ZERO SHADOWING EMPIRICAL VERIFICATION
    // =========================================================================

    [Fact]
    public void AssemblyVerification_TypesInstantiatedFromProductionGameProjectAssembly()
    {
        // Obtain assembly of core domain types
        var questObjAssembly = typeof(QuestObjective).Assembly;
        var saveDataAssembly = typeof(SaveData).Assembly;
        var stateManagerAssembly = typeof(GameStateManager).Assembly;
        var simDriverAssembly = typeof(SimulationDriver).Assembly;

        // Verify that all core types originate from "GameProject" assembly (not GameProject.Tests)
        Assert.Equal("GameProject", questObjAssembly.GetName().Name);
        Assert.Equal("GameProject", saveDataAssembly.GetName().Name);
        Assert.Equal("GameProject", stateManagerAssembly.GetName().Name);
        Assert.Equal("GameProject", simDriverAssembly.GetName().Name);

        // Verify test assembly is distinct
        var testAssembly = GetType().Assembly;
        Assert.Equal("GameProject.Tests", testAssembly.GetName().Name);
        Assert.NotEqual(questObjAssembly, testAssembly);
    }
}
