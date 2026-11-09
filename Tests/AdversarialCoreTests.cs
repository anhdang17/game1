extern alias CoreApp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using CoreSaveData = CoreApp::GameProject.Core.Models.SaveData;
using CorePlayerProfile = CoreApp::GameProject.Core.Models.PlayerProfile;
using CoreWorldProgression = CoreApp::GameProject.Core.Models.WorldProgression;
using CoreLevel1State = CoreApp::GameProject.Core.Models.Level1State;
using CoreQuestFlags = CoreApp::GameProject.Core.Models.QuestFlags;
using CoreCollectionAlbum = CoreApp::GameProject.Core.Models.CollectionAlbum;
using CoreAudioSettings = CoreApp::GameProject.Core.Models.AudioSettings;
using CorePuzzle3ObjectPosition = CoreApp::GameProject.Core.Models.Puzzle3ObjectPosition;
using CoreItemIds = CoreApp::GameProject.Core.Models.ItemIds;
using CoreRegionIds = CoreApp::GameProject.Core.Models.RegionIds;
using CoreQuestIds = CoreApp::GameProject.Core.Models.QuestIds;

using CoreSaveLoadService = CoreApp::GameProject.Core.Persistence.SaveLoadService;
using CoreSaveGameJsonContext = CoreApp::GameProject.Core.Persistence.SaveGameJsonContext;

using CoreEventBus = CoreApp::GameProject.Core.Events.EventBus;
using CoreIEventBus = CoreApp::GameProject.Core.Events.IEventBus;
using CoreIGameEvent = CoreApp::GameProject.Core.Events.IGameEvent;
using CoreGameSavedEvent = CoreApp::GameProject.Core.Events.GameSavedEvent;
using CoreGameLoadedEvent = CoreApp::GameProject.Core.Events.GameLoadedEvent;
using CoreGameStateChangedEvent = CoreApp::GameProject.Core.Events.GameStateChangedEvent;
using CoreQuestStartedEvent = CoreApp::GameProject.Core.Events.QuestStartedEvent;
using CoreQuestProgressUpdatedEvent = CoreApp::GameProject.Core.Events.QuestProgressUpdatedEvent;
using CoreQuestRequirementsMetEvent = CoreApp::GameProject.Core.Events.QuestRequirementsMetEvent;
using CoreQuestCompletedEvent = CoreApp::GameProject.Core.Events.QuestCompletedEvent;
using CoreItemCollectedEvent = CoreApp::GameProject.Core.Events.ItemCollectedEvent;

using CoreISaveStorage = CoreApp::GameProject.Core.Ports.ISaveStorage;
using CoreInMemorySaveStorage = CoreApp::GameProject.Core.Ports.InMemorySaveStorage;
using CoreFileSaveStorage = CoreApp::GameProject.Core.Ports.FileSaveStorage;

using CoreGameStateManager = CoreApp::GameProject.Core.State.GameStateManager;
using CoreGameState = CoreApp::GameProject.Core.State.GameState;

using CoreQuestManager = CoreApp::GameProject.Core.Quests.QuestManager;
using CoreQuestDefinition = CoreApp::GameProject.Core.Quests.QuestDefinition;
using CoreQuestObjective = CoreApp::GameProject.Core.Quests.QuestObjective;
using CoreQuestState = CoreApp::GameProject.Core.Quests.QuestState;
using CoreHanoiQuests = CoreApp::GameProject.Core.Quests.HanoiQuests;
using CoreLevelProgressionService = CoreApp::GameProject.Core.Quests.LevelProgressionService;

namespace GameProject.Tests;

public class AdversarialCoreTests
{
    // =========================================================================
    // CATEGORY 1: EXTREME INPUTS & ROBUSTNESS UNDER ADVERSARIAL PAYLOADS
    // =========================================================================

    [Fact]
    public void Stress_SaveLoad_MassivePlayerName_PreservesDataWithoutTruncation()
    {
        // 100,000 character player name containing authentic Vietnamese Unicode
        string massiveName = string.Concat(Enumerable.Repeat("Bé Mây phiêu lưu cùng Bút Chì tại Hà Nội 🇻🇳 ", 2000));
        var storage = new CoreInMemorySaveStorage();
        var service = new CoreSaveLoadService(storage, "Data/stress_save.json");

        var save = CoreSaveData.CreateDefault();
        save.PlayerProfile.PlayerName = massiveName;

        bool saved = service.Save(save);
        Assert.True(saved, "Save must succeed for massive string input");

        var loaded = service.Load();
        Assert.NotNull(loaded);
        Assert.Equal(massiveName, loaded.PlayerProfile.PlayerName);
    }

    [Fact]
    public void Adversarial_SaveLoad_JsonInjectionCharacters_EscapesSafely()
    {
        // Player name attempting JSON injection attacks & control characters
        string injectionName = "Bé Mây \", \"isAdmin\": true, \"injected\": [1,2,3], \r\n\t\0 \\ \b \f";
        var storage = new CoreInMemorySaveStorage();
        var service = new CoreSaveLoadService(storage, "Data/injection_save.json");

        var save = CoreSaveData.CreateDefault();
        save.PlayerProfile.PlayerName = injectionName;

        bool saved = service.Save(save);
        Assert.True(saved);

        var loaded = service.Load();
        Assert.NotNull(loaded);
        Assert.Equal(injectionName, loaded.PlayerProfile.PlayerName);
    }

    [Fact]
    public void Adversarial_Load_AllNullFieldsJson_RestoresCleanDefaultState()
    {
        // JSON where every primary object property is explicitly null
        string nullFieldsJson = """
        {
            "saveVersion": null,
            "lastSavedTimestamp": null,
            "playerProfile": null,
            "worldProgression": null,
            "level1State": null,
            "collectionAlbum": null,
            "audioSettings": null
        }
        """;

        var storage = new CoreInMemorySaveStorage();
        storage.WriteAllText("Data/null_save.json", nullFieldsJson);
        var service = new CoreSaveLoadService(storage, "Data/null_save.json");

        var loaded = service.Load();
        Assert.NotNull(loaded);
        // Sanitize() must restore all null fields so accessing properties does not throw NullReferenceException
        Assert.NotNull(loaded.PlayerProfile);
        Assert.NotNull(loaded.WorldProgression);
        Assert.NotNull(loaded.Level1State);
        Assert.NotNull(loaded.CollectionAlbum);
        Assert.NotNull(loaded.AudioSettings);
        Assert.NotNull(loaded.QuestFlags);
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        Assert.True(loaded.WorldProgression.IsRegionUnlocked(CoreRegionIds.Hanoi));
    }

    [Fact]
    public void Adversarial_Load_DeeplyNestedOrEmptyObjects_RecoversGracefully()
    {
        var storage = new CoreInMemorySaveStorage();
        var service = new CoreSaveLoadService(storage, "Data/nested_save.json");

        // Fuzz 1: Empty object
        storage.WriteAllText("Data/nested_save.json", "{}");
        var loaded1 = service.Load();
        Assert.NotNull(loaded1);
        Assert.NotNull(loaded1.PlayerProfile);

        // Fuzz 2: Deeply nested unknown payload
        string nestedJson = "{\"extra\": {\"depth1\": {\"depth2\": {\"depth3\": {\"array\": [1, 2, 3]}}}}, \"saveVersion\": \"1.0.0\"}";
        storage.WriteAllText("Data/nested_save.json", nestedJson);
        var loaded2 = service.Load();
        Assert.NotNull(loaded2);
        Assert.Equal("1.0.0", loaded2.SaveVersion);
    }

    [Fact]
    public void Boundary_Sanitize_ExtremeVolumeAndColorValues_ClampedProperly()
    {
        var save = CoreSaveData.CreateDefault();
        save.AudioSettings.MasterVolume = 999.9;
        save.AudioSettings.MusicVolume = -50.0;
        save.AudioSettings.SfxVolume = 1.0001;

        save.WorldProgression.SetColorRestoration(CoreRegionIds.Hanoi, 500.0f);
        save.WorldProgression.SetColorRestoration("CustomRegion", -100.0f);

        save.Sanitize();

        Assert.Equal(1.0, save.AudioSettings.MasterVolume);
        Assert.Equal(0.0, save.AudioSettings.MusicVolume);
        Assert.Equal(1.0, save.AudioSettings.SfxVolume);
        Assert.Equal(100.0f, save.WorldProgression.GetColorRestoration(CoreRegionIds.Hanoi));
        Assert.Equal(0.0f, save.WorldProgression.GetColorRestoration("CustomRegion"));
    }

    // =========================================================================
    // CATEGORY 2: FAULT INJECTION ON SaveLoadService
    // =========================================================================

    [Fact]
    public void FaultInjection_Load_TruncatedJson_CreatesCorruptBackup_AndReturnsCleanDefault()
    {
        var storage = new CoreInMemorySaveStorage();
        var bus = new CoreEventBus();
        CoreGameLoadedEvent? loadEvent = null;
        bus.Subscribe<CoreGameLoadedEvent>(evt => loadEvent = evt);

        var service = new CoreSaveLoadService("Data/truncated_save.json", storage, bus);

        // Truncate in the middle of a JSON dictionary
        storage.SimulateTruncation("Data/truncated_save.json");

        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal("1.0.0", loaded.SaveVersion);
        Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        Assert.NotNull(loadEvent);
        Assert.True(loadEvent.WasCorruptedAndRecovered, "Event must flag that recovery occurred");

        // Verify corrupt file was created
        Assert.True(storage.FileExists("Data/truncated_save.json.corrupt"));
    }

    [Fact]
    public void FaultInjection_Save_SimulatedIoException_ReturnsFalse_AndPublishesFailureEvent()
    {
        var storage = new CoreInMemorySaveStorage();
        var bus = new CoreEventBus();
        CoreGameSavedEvent? saveEvent = null;
        bus.Subscribe<CoreGameSavedEvent>(evt => saveEvent = evt);

        var service = new CoreSaveLoadService("Data/fail_save.json", storage, bus);
        var save = CoreSaveData.CreateDefault();

        // Inject I/O failure on next file operation
        storage.SimulateIoException();

        bool success = service.Save(save);

        Assert.False(success, "SaveGame must return false on I/O exception");
        Assert.NotNull(saveEvent);
        Assert.False(saveEvent.Success, "GameSavedEvent must report failure");
        Assert.NotNull(saveEvent.ErrorMessage);
    }

    [Fact]
    public void FaultInjection_Load_SimulatedIoException_ReturnsCleanDefault_WithoutCrashing()
    {
        var storage = new CoreInMemorySaveStorage();
        var bus = new CoreEventBus();
        var service = new CoreSaveLoadService("Data/load_fail.json", storage, bus);

        // Put a valid file first
        storage.WriteAllText("Data/load_fail.json", "{\"saveVersion\": \"1.0.0\"}");

        // Now simulate I/O exception on read
        storage.SimulateIoException();

        var loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal("1.0.0", loaded.SaveVersion);
    }

    [Fact]
    public void FaultInjection_RealFileSystem_InvalidUtf8Bytes_RecoversCleanState()
    {
        // Use real FileSaveStorage with invalid UTF-8 byte sequences on disk
        string tempDir = Path.Combine(Path.GetTempPath(), "game1_test_" + Guid.NewGuid().ToString("N"));
        string testFile = Path.Combine(tempDir, "invalid_utf8.json");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Write invalid UTF-8 byte sequences (lone high surrogate & invalid byte markers)
            byte[] invalidUtf8Bytes = [0xFF, 0xFE, 0xFD, 0x80, 0x81, 0xC0, 0xAF];
            File.WriteAllBytes(testFile, invalidUtf8Bytes);

            var storage = new CoreFileSaveStorage();
            var service = new CoreSaveLoadService(testFile, storage, null);

            var loaded = service.Load();

            Assert.NotNull(loaded);
            Assert.Equal("1.0.0", loaded.SaveVersion);
            Assert.Equal("Bé Mây", loaded.PlayerProfile.PlayerName);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void FaultInjection_LeftoverTmpFile_DoesNotBlockSubsequentSaves()
    {
        var storage = new CoreInMemorySaveStorage();
        var service = new CoreSaveLoadService("Data/leftover.json", storage, null);

        // Pre-create leftover stale .tmp file
        storage.WriteAllText("Data/leftover.json.tmp", "stale garbage from interrupted write");

        var save = CoreSaveData.CreateDefault();
        save.PlayerProfile.PlayerName = "Bé Mây Đã Cứu";

        bool saved = service.Save(save);
        Assert.True(saved);

        var loaded = service.Load();
        Assert.NotNull(loaded);
        Assert.Equal("Bé Mây Đã Cứu", loaded.PlayerProfile.PlayerName);
    }

    // =========================================================================
    // CATEGORY 3: EventBus STRESS & CONCURRENCY TESTING
    // =========================================================================

    [Fact]
    public void Stress_EventBus_HighFrequencyPublish_ReceivesAllEventsInOrder()
    {
        var bus = new CoreEventBus();
        const int iterations = 10_000;
        int receivedCount = 0;
        int lastReceivedIndex = -1;
        bool inOrder = true;

        bus.Subscribe<CoreItemCollectedEvent>(evt =>
        {
            int index = int.Parse(evt.ItemId);
            if (index != lastReceivedIndex + 1)
            {
                inOrder = false;
            }
            lastReceivedIndex = index;
            Interlocked.Increment(ref receivedCount);
        });

        for (int i = 0; i < iterations; i++)
        {
            bus.Publish(new CoreItemCollectedEvent(i.ToString(), "TestItem", 1));
        }

        Assert.Equal(iterations, receivedCount);
        Assert.True(inOrder, "Events must be processed strictly in publishing order");
    }

    [Fact]
    public void Stress_EventBus_NestedPublishing_DoesNotDeadlockOrCorrupt()
    {
        var bus = new CoreEventBus();
        var callLog = new List<string>();

        // Event A triggers Event B
        bus.Subscribe<CoreQuestStartedEvent>(evt =>
        {
            callLog.Add($"Started:{evt.QuestId}");
            bus.Publish(new CoreQuestProgressUpdatedEvent(evt.QuestId, "Obj1", 1, 1, "Desc"));
        });

        // Event B triggers Event C
        bus.Subscribe<CoreQuestProgressUpdatedEvent>(evt =>
        {
            callLog.Add($"Progress:{evt.QuestId}");
            bus.Publish(new CoreQuestRequirementsMetEvent(evt.QuestId));
        });

        // Event C triggers dynamic new subscription
        bus.Subscribe<CoreQuestRequirementsMetEvent>(evt =>
        {
            callLog.Add($"Met:{evt.QuestId}");
            bus.Subscribe<CoreQuestCompletedEvent>(_ => callLog.Add("DynamicSubscriberTriggered"));
        });

        // Act: Trigger chain reaction
        bus.Publish(new CoreQuestStartedEvent("Q_Chain", "Title", "Desc"));
        bus.Publish(new CoreQuestCompletedEvent("Q_Chain", "Title"));

        // Assert
        Assert.Equal(4, callLog.Count);
        Assert.Equal("Started:Q_Chain", callLog[0]);
        Assert.Equal("Progress:Q_Chain", callLog[1]);
        Assert.Equal("Met:Q_Chain", callLog[2]);
        Assert.Equal("DynamicSubscriberTriggered", callLog[3]);
    }

    [Fact]
    public void Stress_EventBus_SubscriberUnsubscribesDuringOwnCallback()
    {
        var bus = new CoreEventBus();
        int callCount = 0;
        IDisposable? token = null;

        token = bus.Subscribe<CoreItemCollectedEvent>(_ =>
        {
            callCount++;
            token?.Dispose();
        });

        bus.Publish(new CoreItemCollectedEvent("Item1", "Item1", 1));
        bus.Publish(new CoreItemCollectedEvent("Item2", "Item2", 1));

        Assert.Equal(1, callCount);
        Assert.Equal(0, bus.SubscriberCount<CoreItemCollectedEvent>());
    }

    [Fact]
    public void Stress_EventBus_FailingSubscriber_DoesNotHaltOtherSubscribers()
    {
        var bus = new CoreEventBus();
        bool subscriber2Executed = false;

        bus.Subscribe<CoreItemCollectedEvent>(_ =>
        {
            throw new InvalidOperationException("Simulated subscriber explosion");
        });

        bus.Subscribe<CoreItemCollectedEvent>(_ =>
        {
            subscriber2Executed = true;
        });

        // Act: Must not throw out of Publish
        bus.Publish(new CoreItemCollectedEvent("Item_Safe", "Item_Safe", 1));

        Assert.True(subscriber2Executed, "Subscriber 2 must execute even if Subscriber 1 threw");
    }

    [Fact]
    public async Task Stress_EventBus_MultiThreadedConcurrentPublishAndSubscribe()
    {
        var bus = new CoreEventBus();
        const int threadCount = 8;
        const int eventsPerThread = 1_000;
        int totalReceived = 0;

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        bus.Subscribe<CoreItemCollectedEvent>(_ =>
        {
            Interlocked.Increment(ref totalReceived);
        });

        // Launch concurrent publishers
        var publishTasks = Enumerable.Range(0, threadCount).Select(_ => Task.Run(() =>
        {
            for (int i = 0; i < eventsPerThread; i++)
            {
                bus.Publish(new CoreItemCollectedEvent("concurrent_item", "Item", 1));
            }
        })).ToArray();

        // Launch concurrent subscribe/unsubscribe churner
        var churnTask = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                var unsub = bus.Subscribe<CoreItemCollectedEvent>(_ => { });
                unsub.Dispose();
            }
        });

        await Task.WhenAll(publishTasks);
        cts.Cancel();

        Assert.True(totalReceived >= threadCount * eventsPerThread,
            $"Expected at least {threadCount * eventsPerThread} events received, got {totalReceived}");
    }

    // =========================================================================
    // CATEGORY 4: GameStateManager & QuestManager STATE MACHINE ADVERSARIAL TESTS
    // =========================================================================

    [Fact]
    public void StateMachine_GameStateManager_ModalStackingStress()
    {
        var bus = new CoreEventBus();
        var manager = new CoreGameStateManager(bus, CoreGameState.Exploration);

        Assert.Equal(CoreGameState.Exploration, manager.CurrentState);
        Assert.Equal(1, manager.StackDepth);

        // Cannot pop base state
        Assert.False(manager.PopState());
        Assert.Equal(1, manager.StackDepth);

        // Cannot push duplicate state
        Assert.False(manager.PushState(CoreGameState.Exploration));

        // Block Album during active mini-game
        manager.PushState(CoreGameState.MiniGamePho);
        Assert.False(manager.CanOpenAlbum);
        Assert.False(manager.PushState(CoreGameState.Album));

        // Pop back to Exploration
        Assert.True(manager.PopState(out var restored));
        Assert.Equal(CoreGameState.Exploration, restored);

        // Rapid 1000 modal pushes and pops
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(manager.PushState(CoreGameState.Pause));
            Assert.True(manager.PopState());
        }

        Assert.Equal(CoreGameState.Exploration, manager.CurrentState);
        Assert.Equal(1, manager.StackDepth);
    }

    [Fact]
    public void StateMachine_QuestManager_PrerequisiteEnforcement()
    {
        var bus = new CoreEventBus();
        var qm = new CoreQuestManager(bus);
        CoreHanoiQuests.RegisterAll(qm);

        // Hanoi Q2 depends on Q1, Q3 depends on Q2
        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest2Id), "Q2 cannot start before Q1 completes");
        Assert.False(qm.StartQuest(CoreHanoiQuests.Quest2Id));

        Assert.False(qm.CanStartQuest(CoreHanoiQuests.Quest3Id), "Q3 cannot start before Q2 completes");
        Assert.False(qm.StartQuest(CoreHanoiQuests.Quest3Id));

        // Start Q1
        Assert.True(qm.StartQuest(CoreHanoiQuests.Quest1Id));
        Assert.True(qm.IsQuestInProgress(CoreHanoiQuests.Quest1Id));

        // Try completing Q1 before collecting 5 ingredients -> must fail
        Assert.False(qm.CompleteQuest(CoreHanoiQuests.Quest1Id));

        // Advance all 5 ingredients
        qm.RecordObjectiveProgress(CoreItemIds.BanhPho);
        qm.RecordObjectiveProgress(CoreItemIds.ThitBo);
        qm.RecordObjectiveProgress(CoreItemIds.NuocDung);
        qm.RecordObjectiveProgress(CoreItemIds.HanhLa);
        qm.RecordObjectiveProgress(CoreItemIds.ThaoMoc);

        var q1 = qm.GetQuest(CoreHanoiQuests.Quest1Id);
        Assert.NotNull(q1);
        Assert.Equal(CoreQuestState.RequirementsMet, q1.State);

        // Now completion succeeds
        Assert.True(qm.CompleteQuest(CoreHanoiQuests.Quest1Id));
        Assert.True(qm.IsQuestCompleted(CoreHanoiQuests.Quest1Id));

        // Q2 is now eligible
        Assert.True(qm.CanStartQuest(CoreHanoiQuests.Quest2Id));
        Assert.True(qm.StartQuest(CoreHanoiQuests.Quest2Id));
    }

    [Fact]
    public void StateMachine_QuestManager_FlagsRoundtripSynchronization()
    {
        var bus = new CoreEventBus();
        var qm1 = new CoreQuestManager(bus);
        CoreHanoiQuests.RegisterAll(qm1);

        qm1.StartQuest(CoreHanoiQuests.Quest1Id);
        qm1.RecordObjectiveProgress(CoreItemIds.BanhPho);
        qm1.RecordObjectiveProgress(CoreItemIds.ThitBo);

        var flags = CoreQuestFlags.CreateDefault();
        qm1.ExportToFlags(flags);

        Assert.True(flags.Q1_Started);
        Assert.True(flags.Q1_BanhPho_Collected);
        Assert.True(flags.Q1_ThitBo_Collected);
        Assert.False(flags.Q1_NuocDung_Collected);
        Assert.False(flags.Q1_Completed);

        // Hydrate fresh QuestManager from flags
        var qm2 = new CoreQuestManager(bus);
        CoreHanoiQuests.RegisterAll(qm2);
        qm2.HydrateFromFlags(flags);

        var hydratedQ1 = qm2.GetQuest(CoreHanoiQuests.Quest1Id);
        Assert.NotNull(hydratedQ1);
        Assert.Equal(1, hydratedQ1.GetObjective(CoreItemIds.BanhPho)?.CurrentCount);
        Assert.Equal(1, hydratedQ1.GetObjective(CoreItemIds.ThitBo)?.CurrentCount);
        Assert.Equal(0, hydratedQ1.GetObjective(CoreItemIds.NuocDung)?.CurrentCount);
    }

    [Fact]
    public void Simulation_FullPlaythrough_And_DurablePersistence_RoundTrip()
    {
        var driver = new CoreApp::GameProject.Core.Simulation.SimulationDriver();

        // Run full Hanoi level 1 completion headlessly
        driver.SimulateCompleteFullLevel1();

        // Verify progression state post-playthrough
        Assert.Equal(100.0f, driver.SaveData.WorldProgression.GetColorRestoration(CoreRegionIds.Hanoi));
        Assert.True(driver.SaveData.WorldProgression.IsRegionCompleted(CoreRegionIds.Hanoi));
        Assert.True(driver.SaveData.WorldProgression.IsRegionUnlocked(CoreRegionIds.Hue));
        Assert.True(driver.SaveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(driver.SaveData.Level1State.QuestFlags.Level1_Finished);
        Assert.True(driver.SaveData.CollectionAlbum.HasJournalPage(CoreApp::GameProject.Core.Models.AlbumIds.HanoiJournalPage));
        Assert.True(driver.SaveData.CollectionAlbum.HasStamp(CoreApp::GameProject.Core.Models.AlbumIds.HanoiLotusStamp));

        // Save state to in-memory persistence
        bool saved = driver.SaveLoadService.Save(driver.SaveData);
        Assert.True(saved);

        // Create a completely new SimulationDriver with empty initial state
        var newDriver = new CoreApp::GameProject.Core.Simulation.SimulationDriver();
        var reloadedData = driver.SaveLoadService.Load();

        Assert.NotNull(reloadedData);
        Assert.Equal(100.0f, reloadedData.WorldProgression.GetColorRestoration(CoreRegionIds.Hanoi));
        Assert.True(reloadedData.WorldProgression.IsRegionCompleted(CoreRegionIds.Hanoi));
        Assert.True(reloadedData.WorldProgression.IsRegionUnlocked(CoreRegionIds.Hue));
        Assert.True(reloadedData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(reloadedData.Level1State.QuestFlags.Level1_Finished);
        Assert.True(reloadedData.CollectionAlbum.HasJournalPage(CoreApp::GameProject.Core.Models.AlbumIds.HanoiJournalPage));
        Assert.True(reloadedData.CollectionAlbum.HasStamp(CoreApp::GameProject.Core.Models.AlbumIds.HanoiLotusStamp));
    }

    [Fact]
    public void Progression_CompleteLevel1_IsStrictlyIdempotent()
    {
        var saveData = CoreSaveData.CreateDefault();
        var bus = new CoreEventBus();
        int gateUnlockEvents = 0;
        bus.Subscribe<CoreApp::GameProject.Core.Events.GateUnlockedEvent>(_ => gateUnlockEvents++);

        var progression = new CoreLevelProgressionService(saveData, bus);

        // Call CompleteLevel1 multiple times
        progression.CompleteLevel1();
        progression.CompleteLevel1();
        progression.CompleteLevel1();

        Assert.Equal(100.0f, saveData.WorldProgression.GetColorRestoration(CoreRegionIds.Hanoi));
        Assert.Single(saveData.CollectionAlbum.CollectedJournalPages);
        Assert.Single(saveData.CollectionAlbum.CollectedStamps);
        Assert.Equal(3, gateUnlockEvents);
    }

    [Fact]
    public void QuestObjective_NegativeAndExcessiveProgress_ClampedCleanly()
    {
        var obj = new CoreQuestObjective("test_obj", "Test Description", targetCount: 5);

        // Negative increments rejected
        Assert.False(obj.IncrementProgress(-3));
        Assert.False(obj.IncrementProgress(0));
        Assert.Equal(0, obj.CurrentCount);

        // Valid progress
        Assert.True(obj.IncrementProgress(3));
        Assert.Equal(3, obj.CurrentCount);
        Assert.False(obj.IsCompleted);

        // Excessive increment clamped to TargetCount
        Assert.True(obj.IncrementProgress(100));
        Assert.Equal(5, obj.CurrentCount);
        Assert.True(obj.IsCompleted);

        // Further increments rejected once completed
        Assert.False(obj.IncrementProgress(1));
        Assert.Equal(5, obj.CurrentCount);
    }

    [Fact]
    public void CollectionAlbum_DuplicateEntries_IgnoredCaseInsensitively()
    {
        var album = CoreCollectionAlbum.CreateDefault();

        Assert.True(album.AddJournalPage("Page_01"));
        Assert.False(album.AddJournalPage("page_01"));
        Assert.False(album.AddJournalPage("PAGE_01"));
        Assert.False(album.AddJournalPage(""));
        Assert.False(album.AddJournalPage("   "));
        Assert.Single(album.CollectedJournalPages);

        Assert.True(album.AddStamp("Stamp_01"));
        Assert.False(album.AddStamp("STAMP_01"));
        Assert.Single(album.CollectedStamps);

        Assert.True(album.AddFactCard("Card_01"));
        Assert.False(album.AddFactCard("card_01"));
        Assert.Single(album.UnlockedFactCards);
    }

    [Fact]
    public void SaveData_ExtremeCoordinatesAndSpatialPositions_PreservedRoundTrip()
    {
        var storage = new CoreInMemorySaveStorage();
        var service = new CoreSaveLoadService(storage, "Data/spatial_save.json");

        var save = CoreSaveData.CreateDefault();
        save.Level1State.PlayerSpawnX = -999999.999;
        save.Level1State.PlayerSpawnY = 123456789.123;
        save.Level1State.Puzzle3ObjectPositions.Add(new CorePuzzle3ObjectPosition("stall_cart_1", int.MinValue, int.MaxValue));
        save.Level1State.Puzzle3ObjectPositions.Add(new CorePuzzle3ObjectPosition("stall_flower_2", -500, 500));

        bool saved = service.Save(save);
        Assert.True(saved);

        var loaded = service.Load();
        Assert.NotNull(loaded);
        Assert.Equal(-999999.999, loaded.Level1State.PlayerSpawnX);
        Assert.Equal(123456789.123, loaded.Level1State.PlayerSpawnY);
        Assert.Equal(2, loaded.Level1State.Puzzle3ObjectPositions.Count);
        Assert.Equal(int.MinValue, loaded.Level1State.Puzzle3ObjectPositions[0].GridX);
        Assert.Equal(int.MaxValue, loaded.Level1State.Puzzle3ObjectPositions[0].GridY);
    }

    [Fact]
    public void Remediation_QuestObjective_IntegerOverflow_ClampedSafelyToTargetCount()
    {
        var obj = new CoreQuestObjective("Item_BanhPho", "Thu thập bánh phở", targetCount: 5);
        obj.IncrementProgress(1);
        Assert.Equal(1, obj.CurrentCount);

        // Exploit attempt: Pass int.MaxValue
        bool changed = obj.IncrementProgress(int.MaxValue);

        // Verification: The count is safely clamped to TargetCount without overflow or negative numbers
        Assert.True(changed);
        Assert.Equal(5, obj.CurrentCount);
        Assert.True(obj.IsCompleted);

        // Subsequent increment returns false (already completed)
        Assert.False(obj.IncrementProgress(1));
        Assert.Equal(5, obj.CurrentCount);
    }

    [Fact]
    public void Remediation_SaveData_Sanitize_RepairsNullNestedProperties_PreventingExceptions()
    {
        string jsonWithNullNestedProperties = """
        {
            "saveVersion": "1.0.0",
            "level1State": {
                "questFlags": null,
                "inventoryItems": null,
                "puzzle3ObjectPositions": null
            },
            "collectionAlbum": {
                "collectedJournalPages": null,
                "collectedStamps": null,
                "unlockedFactCards": null
            }
        }
        """;

        var storage = new CoreInMemorySaveStorage();
        storage.WriteAllText("Data/nested_null.json", jsonWithNullNestedProperties);
        var service = new CoreSaveLoadService(storage, "Data/nested_null.json");

        var loaded = service.Load();
        Assert.NotNull(loaded);

        // Verification: Level1State.QuestFlags and root QuestFlags are restored and non-null
        Assert.NotNull(loaded.Level1State.QuestFlags);
        Assert.NotNull(loaded.QuestFlags);
        Assert.False(loaded.QuestFlags.Q1_Started);

        // Verification: InventoryItems is restored, and operations succeed without throwing
        Assert.NotNull(loaded.Level1State.InventoryItems);
        loaded.Level1State.InventoryItems.Add(CoreItemIds.BanhPho);
        Assert.Single(loaded.Level1State.InventoryItems);

        // Verification: CollectionAlbum collections are restored, and operations succeed without throwing
        Assert.NotNull(loaded.CollectionAlbum.CollectedStamps);
        bool stampAdded = loaded.CollectionAlbum.AddStamp("Stamp_1");
        Assert.True(stampAdded);
        Assert.True(loaded.CollectionAlbum.HasStamp("Stamp_1"));

        Assert.NotNull(loaded.CollectionAlbum.CollectedJournalPages);
        Assert.NotNull(loaded.CollectionAlbum.UnlockedFactCards);
        Assert.NotNull(loaded.Level1State.Puzzle3ObjectPositions);
    }
}
