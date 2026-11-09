using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Persistence;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.State;
using GameProject.Core.WorldMap;
using GameProject.Stages;
using GameProject.Tests.Mocks;
using Xunit;
using NullAudioService = GameProject.Core.Ports.NullAudioService;
using MockInputService = GameProject.Tests.Mocks.MockInputService;

namespace GameProject.Tests;

/// <summary>
/// Empirical Adversarial Stress Test Suite for Milestone 4 Iteration 2:
/// - Suite Concurrency & Multi-threaded Scene / Audio Isolation
/// - World Map: Rapid Cycling, Clamping, Travel Validation, Persistence Synchronization
/// - Collection Album: Rapid Tab Switching, Index Clamping, Silhouette Inspection, Multi-Save Consistency
/// </summary>
public class ChallengerM4It2ConcurrencyAndMapAlbumStressTests
{
    // =========================================================================
    // 1. CONCURRENCY & MULTI-THREADED ISOLATION
    // =========================================================================

    [Fact]
    public async Task Concurrency_MultiThreadedSceneInstantiationAndDisposal_NoAudioCrashes()
    {
        // Stress-test rapid concurrent creation and destruction of scenes that previously collided on miniaudio hardware
        const int threadCount = 8;
        const int iterationsPerThread = 25;

        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    var saveData = SaveData.CreateDefault();
                    var eventBus = new EventBus();
                    var mockInput = new MockInputService();

                    // Exploration Scene (which had the miniaudio bug)
                    var expScene = new HanoiExplorationScene(saveData, eventBus: eventBus, inputService: mockInput);
                    expScene.Enter();
                    expScene.Update(0.016f);
                    Assert.NotNull(expScene.AudioService);
                    Assert.IsType<NullAudioService>(expScene.AudioService);
                    expScene.Exit();
                    expScene.Dispose();

                    // World Map Scene
                    var mapScene = new WorldMapScene(saveData, eventBus: eventBus, inputService: mockInput);
                    mapScene.Enter();
                    mapScene.Update(0.016f);
                    mapScene.NavigateNext();
                    mapScene.Exit();
                    mapScene.Dispose();

                    // Album Viewer Scene
                    var albumScene = new AlbumViewerScene(saveData, eventBus: eventBus, inputService: mockInput);
                    albumScene.Enter();
                    albumScene.Update(0.016f);
                    albumScene.NextTab();
                    albumScene.Exit();
                    albumScene.Dispose();
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Concurrency_WorldMapAndAlbumServices_ThreadSafeQuerying()
    {
        var mapService = new WorldMapService();
        var albumService = new AlbumService();
        var progression = WorldProgression.CreateDefault();
        var album = CollectionAlbum.CreateDefault();

        album.AddJournalPage(AlbumIds.HanoiJournalPage);
        album.AddStamp(AlbumIds.HanoiLotusStamp);
        album.AddFactCard(AlbumIds.FactCardPho);

        const int workerCount = 10;
        const int queriesPerWorker = 100;
        var tasks = new Task[workerCount];

        for (int w = 0; w < workerCount; w++)
        {
            tasks[w] = Task.Run(() =>
            {
                for (int i = 0; i < queriesPerWorker; i++)
                {
                    // WorldMap queries
                    var nodes = mapService.GetRegionNodes(progression);
                    Assert.Equal(4, nodes.Count);
                    bool selectHanoi = mapService.TrySelectRegion(RegionIds.Hanoi, progression, out _);
                    Assert.True(selectHanoi);
                    bool selectHue = mapService.TrySelectRegion(RegionIds.Hue, progression, out string failReason);
                    Assert.False(selectHue);
                    Assert.NotEmpty(failReason);

                    // Album queries
                    var pages = albumService.GetAllJournalPages();
                    Assert.Equal(2, pages.Count);
                    var stamps = albumService.GetAllRegisteredStamps();
                    Assert.Equal(4, stamps.Count);
                    var facts = albumService.GetAllRegisteredFactCards();
                    Assert.Equal(4, facts.Count);

                    Assert.True(albumService.IsJournalPageUnlocked(album, AlbumIds.HanoiJournalPage));
                    Assert.True(albumService.IsStampUnlocked(album, AlbumIds.HanoiLotusStamp));
                    Assert.True(albumService.IsFactCardUnlocked(album, AlbumIds.FactCardPho));
                    Assert.False(albumService.IsJournalPageUnlocked(album, "JournalPage_Hue_02"));
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    // =========================================================================
    // 2. WORLD MAP STRESS & PERSISTENCE SYNCHRONIZATION
    // =========================================================================

    [Fact]
    public void WorldMap_RapidCyclesStress_TenThousandIterations_NeverEntersInvalidState()
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        var allowedRegions = new HashSet<string> { "Hanoi", "Hue", "HoiAn", "SaiGon" };
        var rng = new Random(1337);

        for (int i = 0; i < 10000; i++)
        {
            int action = rng.Next(3);
            switch (action)
            {
                case 0:
                    mapScene.NavigateNext();
                    break;
                case 1:
                    mapScene.NavigatePrev();
                    break;
                case 2:
                    string randomPick = allowedRegions.ElementAt(rng.Next(allowedRegions.Count));
                    mapScene.SelectRegion(randomPick);
                    break;
            }

            Assert.Contains(mapScene.SelectedRegionId, allowedRegions);
        }
    }

    [Theory]
    [InlineData("NonExistentRegion")]
    [InlineData("Tokyo")]
    [InlineData("Paris")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData(null)]
    public void WorldMap_SelectRegion_AdversarialInputs_PreservesCurrentSelection(string? invalidInput)
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        mapScene.SelectRegion("SaiGon");
        Assert.Equal("SaiGon", mapScene.SelectedRegionId);

        // Attempting to select invalid/null region must not throw and must keep current valid selection
        mapScene.SelectRegion(invalidInput!);
        Assert.Equal("SaiGon", mapScene.SelectedRegionId);
    }

    [Fact]
    public void WorldMap_TravelConfirmation_LockedRegionsRefused_ThenUnlockedRegionSucceeds()
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        // 1. Try travel to locked regions: Hue, HoiAn, SaiGon
        string[] lockedRegions = ["Hue", "HoiAn", "SaiGon"];
        foreach (var reg in lockedRegions)
        {
            mapScene.SelectRegion(reg);
            bool allowed = mapScene.ConfirmTravel();
            Assert.False(allowed);
            Assert.Null(mapScene.RequestTravelTo);
            Assert.NotNull(mapScene.FeedbackMessage);
            Assert.Contains("bóng xám", mapScene.FeedbackMessage);
        }

        // 2. Select Hanoi (unlocked default region)
        mapScene.SelectRegion("Hanoi");
        bool hanoiAllowed = mapScene.ConfirmTravel();
        Assert.True(hanoiAllowed);
        Assert.Equal("Hanoi", mapScene.RequestTravelTo);
        Assert.NotNull(mapScene.FeedbackMessage);
        Assert.Contains("Thủ Đô Hà Nội", mapScene.FeedbackMessage);
    }

    [Fact]
    public void WorldMap_PersistenceSynchronization_AcrossMultipleSaveLoadCycles()
    {
        var storage = new InMemorySaveStorage();
        var saveLoad = new SaveLoadService("savegame.json", storage);

        // 1. Initial State: Only Hanoi unlocked
        var initialSave = SaveData.CreateDefault();
        saveLoad.SaveGame(initialSave);

        var loaded1 = saveLoad.LoadGame();
        var mapScene1 = new WorldMapScene(loaded1);
        mapScene1.Enter();

        mapScene1.SelectRegion("Hue");
        Assert.False(mapScene1.ConfirmTravel());

        // 2. Player advances through Hanoi quests and finishes Level 1 climax
        var eventBus = new EventBus();
        var levelProgression = new LevelProgressionService(loaded1, eventBus);
        levelProgression.CompleteLevel1();

        // Save progressed state
        saveLoad.SaveGame(loaded1);

        // 3. Load state in a brand-new session
        var loaded2 = saveLoad.LoadGame();
        var mapScene2 = new WorldMapScene(loaded2);
        mapScene2.Enter();

        // Verify Hanoi is marked 100% completed
        var mapService = new WorldMapService();
        var nodes = mapService.GetRegionNodes(loaded2.WorldProgression);
        var hanoiNode = nodes.First(n => n.RegionId == "Hanoi");
        var hueNode = nodes.First(n => n.RegionId == "Hue");

        Assert.Equal(RegionStatus.Completed, hanoiNode.Status);
        Assert.Equal(100f, hanoiNode.ColorRestorationPercent);
        Assert.Equal(RegionStatus.Unlocked, hueNode.Status);

        // Verify travel to Hue is now allowed
        mapScene2.SelectRegion("Hue");
        bool hueAllowed = mapScene2.ConfirmTravel();
        Assert.True(hueAllowed);
        Assert.Equal("Hue", mapScene2.RequestTravelTo);
        Assert.Contains("Cố Đô Huế", mapScene2.FeedbackMessage);

        // Hoi An and Sai Gon remain locked
        mapScene2.SelectRegion("HoiAn");
        Assert.False(mapScene2.ConfirmTravel());
        mapScene2.SelectRegion("SaiGon");
        Assert.False(mapScene2.ConfirmTravel());
    }

    // =========================================================================
    // 3. COLLECTION ALBUM STRESS & MULTI-SAVE CONSISTENCY
    // =========================================================================

    [Fact]
    public void CollectionAlbum_RapidTabSwitching_IndexAlwaysClampedSafely()
    {
        var saveData = SaveData.CreateDefault();
        var viewer = new AlbumViewerScene(saveData);
        viewer.Enter();

        var rng = new Random(2026);

        // Stress 2,000 mixed tab switches and page navigations
        for (int i = 0; i < 2000; i++)
        {
            int action = rng.Next(6);
            switch (action)
            {
                case 0: viewer.NextTab(); break;
                case 1: viewer.PrevTab(); break;
                case 2: viewer.SelectTab(rng.Next(-5, 8)); break;
                case 3: viewer.NextPage(); break;
                case 4: viewer.PrevPage(); break;
                case 5: viewer.Enter(); break;
            }

            int currentTab = viewer.ActiveTab;
            Assert.InRange(currentTab, 0, 2);

            int tabCount = viewer.GetCurrentTabCount();
            Assert.True(tabCount > 0, $"Tab {currentTab} count must be positive");
            Assert.InRange(viewer.PageIndex, 0, tabCount - 1);
        }
    }

    [Fact]
    public void CollectionAlbum_TabCountAlignment_Journal2_Stamps4_Facts4()
    {
        var saveData = SaveData.CreateDefault();
        var viewer = new AlbumViewerScene(saveData);
        viewer.Enter();

        // Tab 0: Journal -> 2 pages
        viewer.SelectTab(0);
        Assert.Equal(2, viewer.GetCurrentTabCount());

        // Tab 1: Stamps -> 4 stamps
        viewer.SelectTab(1);
        Assert.Equal(4, viewer.GetCurrentTabCount());

        // Tab 2: Facts -> 4 fact cards
        viewer.SelectTab(2);
        Assert.Equal(4, viewer.GetCurrentTabCount());
    }

    [Fact]
    public void CollectionAlbum_LockedSilhouettes_HuePageAndRegionalStamps()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        // 1. Initial State: all items locked
        Assert.False(albumService.IsJournalPageUnlocked(album, AlbumService.HanoiPage1.Id));
        Assert.False(albumService.IsJournalPageUnlocked(album, AlbumService.HuePage2.Id));
        Assert.False(albumService.IsStampUnlocked(album, AlbumService.HanoiLotusStamp.Id));
        Assert.False(albumService.IsStampUnlocked(album, AlbumService.HueStamp.Id));
        Assert.False(albumService.IsStampUnlocked(album, AlbumService.HoiAnStamp.Id));
        Assert.False(albumService.IsStampUnlocked(album, AlbumService.SaiGonStamp.Id));

        // 2. Award Level 1 Hanoi awards
        albumService.AwardJournalPage(album, AlbumService.HanoiPage1.Id);
        albumService.AwardTravelStamp(album, AlbumService.HanoiLotusStamp.Id);

        // Hanoi is unlocked
        Assert.True(albumService.IsJournalPageUnlocked(album, AlbumService.HanoiPage1.Id));
        Assert.True(albumService.IsStampUnlocked(album, AlbumService.HanoiLotusStamp.Id));

        // Hue Page 2 and other stamps remain locked
        Assert.False(albumService.IsJournalPageUnlocked(album, AlbumService.HuePage2.Id));
        Assert.False(albumService.IsStampUnlocked(album, AlbumService.HueStamp.Id));
        Assert.False(albumService.IsStampUnlocked(album, AlbumService.HoiAnStamp.Id));
        Assert.False(albumService.IsStampUnlocked(album, AlbumService.SaiGonStamp.Id));

        // 3. Inspect locked Hue Page metadata
        var huePage = AlbumService.HuePage2;
        Assert.Equal("JournalPage_Hue_02", huePage.Id);
        Assert.Equal(RegionIds.Hue, huePage.Region);
        Assert.Contains("Sông Hương", huePage.Title);
        Assert.Contains("Ký ức Cố Đô Huế", huePage.Subtitle);
    }

    [Fact]
    public void CollectionAlbum_StampUnlockingConsistency_AcrossMultipleSaveLoadCycles()
    {
        var storage = new InMemorySaveStorage();
        var saveLoad = new SaveLoadService("savegame.json", storage);
        var albumService = new AlbumService();

        var saveData = SaveData.CreateDefault();

        // Cycle 1: Fresh save -> 0 stamps
        saveLoad.SaveGame(saveData);
        var loaded = saveLoad.LoadGame();
        Assert.Empty(loaded.CollectionAlbum.CollectedStamps);
        Assert.Empty(albumService.GetUnlockedTravelStamps(loaded.CollectionAlbum));

        // Cycle 2: Award Hanoi Stamp -> Save -> Load -> 1 stamp
        bool a1 = albumService.AwardTravelStamp(loaded.CollectionAlbum, AlbumIds.HanoiLotusStamp);
        Assert.True(a1);
        saveLoad.SaveGame(loaded);
        loaded = saveLoad.LoadGame();
        Assert.Single(loaded.CollectionAlbum.CollectedStamps);
        Assert.True(albumService.IsStampUnlocked(loaded.CollectionAlbum, AlbumIds.HanoiLotusStamp));

        // Cycle 3: Award Hue Stamp -> Save -> Load -> 2 stamps
        bool a2 = albumService.AwardTravelStamp(loaded.CollectionAlbum, "Stamp_Hue_ThienMu");
        Assert.True(a2);
        saveLoad.SaveGame(loaded);
        loaded = saveLoad.LoadGame();
        Assert.Equal(2, loaded.CollectionAlbum.CollectedStamps.Count);
        Assert.True(albumService.IsStampUnlocked(loaded.CollectionAlbum, "Stamp_Hue_ThienMu"));

        // Cycle 4: Attempt duplicate Hanoi stamp -> idempotent, still 2 stamps
        bool aDup = albumService.AwardTravelStamp(loaded.CollectionAlbum, AlbumIds.HanoiLotusStamp);
        Assert.False(aDup);
        saveLoad.SaveGame(loaded);
        loaded = saveLoad.LoadGame();
        Assert.Equal(2, loaded.CollectionAlbum.CollectedStamps.Count);

        // Cycle 5: Award Hoi An and Sai Gon stamps with different casing
        albumService.AwardTravelStamp(loaded.CollectionAlbum, "stamp_hoian_denlong");
        albumService.AwardTravelStamp(loaded.CollectionAlbum, "STAMP_SAIGON_BENTHANH");
        saveLoad.SaveGame(loaded);
        loaded = saveLoad.LoadGame();

        Assert.Equal(4, loaded.CollectionAlbum.CollectedStamps.Count);
        Assert.True(albumService.IsStampUnlocked(loaded.CollectionAlbum, "Stamp_HoiAn_DenLong"));
        Assert.True(albumService.IsStampUnlocked(loaded.CollectionAlbum, "Stamp_SaiGon_BenThanh"));
        Assert.Equal(4, albumService.GetUnlockedTravelStamps(loaded.CollectionAlbum).Count);

        // Cycle 6: Introduce whitespace/null entries into album JSON and verify auto-sanitization on Load
        loaded.CollectionAlbum.CollectedStamps.Add("   ");
        loaded.CollectionAlbum.CollectedStamps.Add("");
        saveLoad.SaveGame(loaded);
        loaded = saveLoad.LoadGame();

        Assert.Equal(4, loaded.CollectionAlbum.CollectedStamps.Count);
        Assert.DoesNotContain("", loaded.CollectionAlbum.CollectedStamps);
        Assert.DoesNotContain("   ", loaded.CollectionAlbum.CollectedStamps);
    }
}
