using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Quests;
using GameProject.Core.State;
using GameProject.Core.WorldMap;
using GameProject.Stages;
using GameProject.Tests.Mocks;
using Xunit;

namespace GameProject.Tests;

/// <summary>
/// Adversarial challenger empirical stress tests for Milestone 4:
/// - 3 Album Tabs, navigation wrapping, and index bounds clamping
/// - Page 2 (Huế) locked/unlocked rich metadata and idempotence
/// - 4-stamp regional catalog validation and case insensitivity
/// - Dual-key FactCard alias completeness, bidirectional resolution, and deduplication
/// - Arrow-key navigation on World Map, wrapping, and random stress cycle
/// - Region preview data binding across all 4 regions and color states
/// - Locked region travel prevention and user feedback
/// - Post-climax LevelProgressionService Huế unlocking and travel confirmation
/// - Scene transitions coordinator simulation and modal overlay lifecycles
/// - Headless InputService integration
/// </summary>
public class ChallengerM4AlbumWorldMapTests
{
    // =========================================================================
    // 1. ALBUM TABS & NAVIGATION WRAPPING
    // =========================================================================

    [Fact]
    public void Album_ThreeTabs_NamesAndInitialIndices_AreValid()
    {
        var saveData = SaveData.CreateDefault();
        var viewer = new AlbumViewerScene(saveData);
        viewer.Enter();

        Assert.Equal(3, AlbumViewerScene.TabNames.Length);
        Assert.Equal("Cuốn Sổ Ký Ức", AlbumViewerScene.TabNames[0]);
        Assert.Equal("Tem Du Lịch", AlbumViewerScene.TabNames[1]);
        Assert.Equal("Thẻ Văn Hóa", AlbumViewerScene.TabNames[2]);
        Assert.Equal(0, viewer.ActiveTab);
        Assert.Equal(0, viewer.PageIndex);
    }

    [Fact]
    public void Album_TabCycling_ForwardAndBackward_WrapsCorrectly()
    {
        var saveData = SaveData.CreateDefault();
        var viewer = new AlbumViewerScene(saveData);
        viewer.Enter();

        // Forward wrapping: 0 -> 1 -> 2 -> 0 -> 1 -> 2 -> 0
        int[] expectedForward = [1, 2, 0, 1, 2, 0, 1];
        foreach (int expected in expectedForward)
        {
            viewer.NextTab();
            Assert.Equal(expected, viewer.ActiveTab);
            Assert.Equal(0, viewer.PageIndex);
        }

        // Backward wrapping: 1 -> 0 -> 2 -> 1 -> 0 -> 2
        int[] expectedBackward = [0, 2, 1, 0, 2];
        foreach (int expected in expectedBackward)
        {
            viewer.PrevTab();
            Assert.Equal(expected, viewer.ActiveTab);
            Assert.Equal(0, viewer.PageIndex);
        }
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(999)]
    public void Album_SelectTab_OutOfBoundsIndex_IsSafelyIgnored(int invalidIndex)
    {
        var saveData = SaveData.CreateDefault();
        var viewer = new AlbumViewerScene(saveData);
        viewer.Enter();

        viewer.SelectTab(1); // Set to valid tab 1
        Assert.Equal(1, viewer.ActiveTab);

        viewer.SelectTab(invalidIndex);
        // Must remain at tab 1, not crash or enter corrupt state
        Assert.Equal(1, viewer.ActiveTab);
    }

    [Fact]
    public void Album_PageIndex_ClampedAcrossAllTabs_AndResetsOnTabChange()
    {
        var saveData = SaveData.CreateDefault();
        var viewer = new AlbumViewerScene(saveData);
        viewer.Enter();

        // Tab 0: Journal (2 pages max: indices 0 and 1)
        Assert.Equal(2, viewer.GetCurrentTabCount());
        viewer.NextPage();
        Assert.Equal(1, viewer.PageIndex);
        viewer.NextPage(); // should clamp at 1
        Assert.Equal(1, viewer.PageIndex);
        viewer.PrevPage();
        Assert.Equal(0, viewer.PageIndex);
        viewer.PrevPage(); // should clamp at 0
        Assert.Equal(0, viewer.PageIndex);

        // Switch to Tab 1: Stamps (4 stamps: indices 0..3)
        viewer.SelectTab(1);
        Assert.Equal(0, viewer.PageIndex);
        Assert.Equal(4, viewer.GetCurrentTabCount());

        for (int i = 0; i < 10; i++) viewer.NextPage();
        Assert.Equal(3, viewer.PageIndex); // clamped at 3

        // Switch to Tab 0: PageIndex MUST reset to 0 to prevent out-of-range index 3 on a 2-page tab!
        viewer.SelectTab(0);
        Assert.Equal(0, viewer.PageIndex);
    }

    // =========================================================================
    // 2. PAGE 2 (HUẾ) METADATA & LOCK/UNLOCK BEHAVIOR
    // =========================================================================

    [Fact]
    public void Album_Page2_Hue_RichMetadata_And_CompatibilityProperties()
    {
        var page = AlbumService.HuePage2;

        Assert.Equal("JournalPage_Hue_02", page.Id);
        Assert.Equal(RegionIds.Hue, page.Region);
        Assert.Equal(page.Region, page.RegionId);
        Assert.Contains("Sông Hương", page.Title);
        Assert.Contains("Cung Thành Cổ", page.Title);
        Assert.Contains("Ký ức Cố Đô Huế", page.Subtitle);
        Assert.Contains("sông Hương", page.StoryText);
        Assert.Contains("Bé Mây", page.StoryText);
        Assert.Contains("Bút Chì", page.StoryText);
        Assert.Equal(page.StoryText, page.NarrativeText);
        Assert.Equal("Art_Journal_Hue_02", page.IllustrationKey);
        Assert.Equal(page.IllustrationKey, page.ImageKey);
    }

    [Fact]
    public void Album_Page2_Hue_LockUnlockLifecycle_And_CaseInsensitivity()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        // Null and empty checks
        Assert.False(albumService.IsJournalPageUnlocked(null!, "JournalPage_Hue_02"));
        Assert.False(albumService.IsJournalPageUnlocked(album, ""));
        Assert.False(albumService.IsJournalPageUnlocked(album, null!));

        // Initially locked
        Assert.False(albumService.IsJournalPageUnlocked(album, "JournalPage_Hue_02"));

        // Award Page 2
        bool firstAward = albumService.AwardJournalPage(album, "JournalPage_Hue_02");
        Assert.True(firstAward);

        // Idempotence: second award must return false
        bool secondAward = albumService.AwardJournalPage(album, "JournalPage_Hue_02");
        Assert.False(secondAward);

        // Case insensitivity
        Assert.True(albumService.IsJournalPageUnlocked(album, "JournalPage_Hue_02"));
        Assert.True(albumService.IsJournalPageUnlocked(album, "journalpage_hue_02"));
        Assert.True(albumService.IsJournalPageUnlocked(album, "JOURNALPAGE_HUE_02"));

        // Unlocked pages list
        var unlocked = albumService.GetUnlockedJournalPages(album);
        Assert.Single(unlocked);
        Assert.Equal("JournalPage_Hue_02", unlocked[0].Id);
    }

    // =========================================================================
    // 3. 4-STAMP REGIONAL CATALOG
    // =========================================================================

    [Fact]
    public void Album_Stamps_AllFourRegionsRepresented_WithAuthenticData()
    {
        var albumService = new AlbumService();
        var stamps = albumService.GetAllRegisteredStamps();

        Assert.Equal(4, stamps.Count);

        // Stamp 0: Hanoi
        Assert.Equal(AlbumIds.HanoiLotusStamp, stamps[0].Id);
        Assert.Equal("Tem Hoa Sen Hà Nội", stamps[0].Name);
        Assert.Equal("HÀ NỘI - 1000 NĂM VĂN HIẾN", stamps[0].PostmarkText);
        Assert.Equal(RegionIds.Hanoi, stamps[0].Region);

        // Stamp 1: Hue
        Assert.Equal("Stamp_Hue_ThienMu", stamps[1].Id);
        Assert.Equal("Tem Cố Đô Huế", stamps[1].Name);
        Assert.Equal("CỐ ĐÔ HUẾ - DI SẢN THẾ GIỚI", stamps[1].PostmarkText);
        Assert.Equal(RegionIds.Hue, stamps[1].Region);

        // Stamp 2: Hoi An
        Assert.Equal("Stamp_HoiAn_DenLong", stamps[2].Id);
        Assert.Equal("Tem Phố Cổ Hội An", stamps[2].Name);
        Assert.Equal("HỘI AN - PHỐ ĐÈN LỒNG", stamps[2].PostmarkText);
        Assert.Equal(RegionIds.HoiAn, stamps[2].Region);

        // Stamp 3: Sai Gon
        Assert.Equal("Stamp_SaiGon_BenThanh", stamps[3].Id);
        Assert.Equal("Tem Nắng Phương Nam", stamps[3].Name);
        Assert.Equal("SÀI GÒN - BẾN THÀNH", stamps[3].PostmarkText);
        Assert.Equal(RegionIds.SaiGon, stamps[3].Region);
    }

    [Fact]
    public void Album_Stamps_AwardingAndUnlockFiltering_NoDuplicates()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        // Award in non-sequential order
        albumService.AwardTravelStamp(album, "Stamp_SaiGon_BenThanh");
        albumService.AwardTravelStamp(album, AlbumIds.HanoiLotusStamp);

        Assert.True(albumService.IsStampUnlocked(album, "Stamp_SaiGon_BenThanh"));
        Assert.True(albumService.IsStampUnlocked(album, AlbumIds.HanoiLotusStamp));
        Assert.False(albumService.IsStampUnlocked(album, "Stamp_Hue_ThienMu"));
        Assert.False(albumService.IsStampUnlocked(album, "Stamp_HoiAn_DenLong"));

        var unlocked = albumService.GetUnlockedTravelStamps(album);
        Assert.Equal(2, unlocked.Count);
        Assert.Contains(unlocked, s => s.Id == "Stamp_SaiGon_BenThanh");
        Assert.Contains(unlocked, s => s.Id == AlbumIds.HanoiLotusStamp);
    }

    // =========================================================================
    // 4. DUAL-KEY FACTCARD ALIASES (ALL 4 PAIRS)
    // =========================================================================

    public static IEnumerable<object[]> FactCardAliasPairs =>
    [
        ["FactCard_PhoHaNoi", AlbumIds.FactCardPho, "Nét Tinh Túy Phở Hà Nội", "Ẩm Thực"],
        ["FactCard_CauLongBien", AlbumIds.FactCardLongBien, "Cầu Long Biên - Dải Lụa Thép Qua Sông Hồng", "Lịch Sử"],
        ["FactCard_HoaSenTayHo", AlbumIds.FactCardSenTayHo, "Hoa Sen Tây Hồ & Văn Hóa Trà Sen", "Văn Hóa"],
        ["FactCard_36PhoPhuong", AlbumIds.FactCardPhoCo, "Chuyện 36 Phố Phường", "Di Sản"]
    ];

    [Theory]
    [MemberData(nameof(FactCardAliasPairs))]
    public void Album_FactCards_BidirectionalAliasResolution(string aliasKey, string canonicalKey, string expectedTitle, string expectedCategory)
    {
        var albumService = new AlbumService();

        // 1. Unlock via aliasKey -> canonicalKey must also be reported unlocked
        var albumA = CollectionAlbum.CreateDefault();
        bool unlockedA = albumService.UnlockFactCard(albumA, aliasKey);
        Assert.True(unlockedA);
        Assert.True(albumService.IsFactCardUnlocked(albumA, aliasKey));
        Assert.True(albumService.IsFactCardUnlocked(albumA, canonicalKey));

        var cardsA = albumService.GetUnlockedFactCards(albumA);
        Assert.Single(cardsA);
        Assert.Equal(expectedTitle, cardsA[0].Title);
        Assert.Equal(expectedCategory, cardsA[0].Category);
        Assert.Equal(cardsA[0].Content, cardsA[0].FullContent);

        // 2. Unlock via canonicalKey -> aliasKey must also be reported unlocked
        var albumB = CollectionAlbum.CreateDefault();
        bool unlockedB = albumService.UnlockFactCard(albumB, canonicalKey);
        Assert.True(unlockedB);
        Assert.True(albumService.IsFactCardUnlocked(albumB, canonicalKey));
        Assert.True(albumService.IsFactCardUnlocked(albumB, aliasKey));

        var cardsB = albumService.GetUnlockedFactCards(albumB);
        Assert.Single(cardsB);
        Assert.Equal(expectedTitle, cardsB[0].Title);

        // 3. If BOTH keys exist in album -> GetUnlockedFactCards must deduplicate to exactly 1 card
        var albumC = CollectionAlbum.CreateDefault();
        albumC.UnlockedFactCards.Add(aliasKey);
        albumC.UnlockedFactCards.Add(canonicalKey);
        var cardsC = albumService.GetUnlockedFactCards(albumC);
        Assert.Single(cardsC);
        Assert.Equal(expectedTitle, cardsC[0].Title);
    }

    [Fact]
    public void Album_FactCards_CaseInsensitiveQuerying()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        albumService.UnlockFactCard(album, "factcard_phohanoi");
        Assert.True(albumService.IsFactCardUnlocked(album, "FACTCARD_PHOHANOI"));
        Assert.True(albumService.IsFactCardUnlocked(album, "FACTCARD_HANOI_PHO"));
    }

    // =========================================================================
    // 5. WORLD MAP NAVIGATION & CLAMPING STRESS
    // =========================================================================

    [Fact]
    public void WorldMap_Cycle_AllFourRegions_ForwardAndBackward()
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        Assert.Equal("Hanoi", mapScene.SelectedRegionId);

        // Forward sequence: Hanoi -> Hue -> HoiAn -> SaiGon -> Hanoi
        string[] forwardSequence = ["Hue", "HoiAn", "SaiGon", "Hanoi"];
        foreach (string expected in forwardSequence)
        {
            mapScene.NavigateNext();
            Assert.Equal(expected, mapScene.SelectedRegionId);
        }

        // Backward sequence: Hanoi -> SaiGon -> HoiAn -> Hue -> Hanoi
        string[] backwardSequence = ["SaiGon", "HoiAn", "Hue", "Hanoi"];
        foreach (string expected in backwardSequence)
        {
            mapScene.NavigatePrev();
            Assert.Equal(expected, mapScene.SelectedRegionId);
        }
    }

    [Fact]
    public void WorldMap_Navigation_StressRapidCycles_NeverExitsValidRegions()
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        var validRegions = new HashSet<string> { "Hanoi", "Hue", "HoiAn", "SaiGon" };
        var rng = new Random(42);

        for (int i = 0; i < 500; i++)
        {
            if (rng.Next(2) == 0)
                mapScene.NavigateNext();
            else
                mapScene.NavigatePrev();

            Assert.Contains(mapScene.SelectedRegionId, validRegions);
        }
    }

    [Theory]
    [InlineData("Danang")]
    [InlineData("PhuQuoc")]
    [InlineData("")]
    [InlineData(null)]
    public void WorldMap_SelectRegion_InvalidRegion_KeepsCurrentSelection(string? invalidRegion)
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        mapScene.SelectRegion("Hue");
        Assert.Equal("Hue", mapScene.SelectedRegionId);

        mapScene.SelectRegion(invalidRegion!);
        Assert.Equal("Hue", mapScene.SelectedRegionId);
    }

    // =========================================================================
    // 6. REGION PREVIEW DATA BINDING
    // =========================================================================

    [Fact]
    public void WorldMap_PreviewDataBinding_AllFourRegions()
    {
        Assert.Equal("Thủ Đô Hà Nội", WorldMapScene.GetDisplayName("Hanoi"));
        Assert.Equal("Cố Đô Huế", WorldMapScene.GetDisplayName("Hue"));
        Assert.Equal("Phố Cổ Hội An", WorldMapScene.GetDisplayName("HoiAn"));
        Assert.Equal("Sài Gòn - Bến Thành", WorldMapScene.GetDisplayName("SaiGon"));
    }

    [Fact]
    public void WorldMap_Service_NodesReflectProgressionColors()
    {
        var service = new WorldMapService();
        var progression = WorldProgression.CreateDefault();

        // Initially Hanoi at 0%
        var nodes = service.GetRegionNodes(progression);
        var hanoi = nodes.First(n => n.RegionId == "Hanoi");
        Assert.Equal(RegionStatus.Unlocked, hanoi.Status);
        Assert.Equal(0.0f, hanoi.ColorRestorationPercent);

        // Quest 2 bloom at 33.3%
        progression.SetColorRestoration("Hanoi", 33.3f);
        nodes = service.GetRegionNodes(progression);
        hanoi = nodes.First(n => n.RegionId == "Hanoi");
        Assert.Equal(33.3f, hanoi.ColorRestorationPercent);

        // Level 1 complete: 100%
        progression.CompleteRegion("Hanoi");
        nodes = service.GetRegionNodes(progression);
        hanoi = nodes.First(n => n.RegionId == "Hanoi");
        Assert.Equal(RegionStatus.Completed, hanoi.Status);
        Assert.Equal(100.0f, hanoi.ColorRestorationPercent);
    }

    // =========================================================================
    // 7. LOCKED REGION TRAVEL PREVENTION & FEEDBACK
    // =========================================================================

    [Theory]
    [InlineData("Hue")]
    [InlineData("HoiAn")]
    [InlineData("SaiGon")]
    public void WorldMap_TravelToLockedRegion_FailsWithFeedback(string lockedRegion)
    {
        var saveData = SaveData.CreateDefault();
        // Ensure region is locked
        saveData.WorldProgression.UnlockedRegions.Remove(lockedRegion);

        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        mapScene.SelectRegion(lockedRegion);
        bool allowed = mapScene.ConfirmTravel();

        Assert.False(allowed);
        Assert.NotEqual(lockedRegion, mapScene.RequestTravelTo);
        Assert.NotNull(mapScene.FeedbackMessage);
        Assert.Contains("bóng xám", mapScene.FeedbackMessage);
    }

    // =========================================================================
    // 8. POST-CLIMAX HUẾ UNLOCKING VIA LEVEL PROGRESSION SERVICE
    // =========================================================================

    [Fact]
    public void WorldMap_PostClimaxLevelProgression_UnlocksHue_AndEnablesTravelConfirmation()
    {
        var saveData = SaveData.CreateDefault();
        var eventBus = new EventBus();
        var levelProgression = new LevelProgressionService(saveData, eventBus);

        // Verify Hue is locked before climax
        Assert.False(saveData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));

        // Trigger Level 1 Climax (Quest 3 resolution)
        levelProgression.CompleteLevel1();

        // 1. Verify progression state updates
        Assert.True(saveData.WorldProgression.IsRegionUnlocked(RegionIds.Hue));
        Assert.True(saveData.WorldProgression.IsRegionCompleted(RegionIds.Hanoi));
        Assert.Equal(100.0f, saveData.WorldProgression.GetColorRestoration(RegionIds.Hanoi));
        Assert.True(saveData.Level1State.QuestFlags.Gate_LongBien_Unlocked);
        Assert.True(saveData.Level1State.QuestFlags.Level1_Finished);

        // 2. Verify album awards
        Assert.Contains(AlbumIds.HanoiJournalPage, saveData.CollectionAlbum.CollectedJournalPages);
        Assert.Contains(AlbumIds.HanoiLotusStamp, saveData.CollectionAlbum.CollectedStamps);

        // 3. Verify WorldMap allows travel to Hue now!
        var mapScene = new WorldMapScene(saveData, eventBus);
        mapScene.Enter();

        mapScene.SelectRegion(RegionIds.Hue);
        bool travelConfirmed = mapScene.ConfirmTravel();

        Assert.True(travelConfirmed);
        Assert.Equal(RegionIds.Hue, mapScene.RequestTravelTo);
        Assert.NotNull(mapScene.FeedbackMessage);
        Assert.Contains("Cố Đô Huế", mapScene.FeedbackMessage);
    }

    // =========================================================================
    // 9. SCENE TRANSITION COORDINATION & OVERLAY LIFECYCLES
    // =========================================================================

    [Fact]
    public void SceneTransitions_SimulateGameLoopCoordinator_NoSoftLocks()
    {
        var saveData = SaveData.CreateDefault();
        var eventBus = new EventBus();
        var mockInput = new MockInputService();
        using var sceneManager = new SceneManager();

        // Start in Exploration scene
        var expScene = new HanoiExplorationScene(saveData, inputService: mockInput, eventBus: eventBus);
        sceneManager.SwitchScene(expScene);
        Assert.Same(expScene, sceneManager.CurrentScene);

        // Simulate 20 alternating transitions: Exploration -> World Map -> Album -> World Map -> Exploration
        for (int cycle = 0; cycle < 10; cycle++)
        {
            // 1. Exploration requests World Map
            expScene.RequestWorldMap = true;
            if (sceneManager.CurrentScene is HanoiExplorationScene exp && exp.RequestWorldMap)
            {
                exp.RequestWorldMap = false;
                sceneManager.SwitchScene(new WorldMapScene(saveData, eventBus, mockInput));
            }
            Assert.IsType<WorldMapScene>(sceneManager.CurrentScene);

            // 2. World Map requests Album
            var wm = (WorldMapScene)sceneManager.CurrentScene;
            wm.OpenAlbum();
            if (wm.RequestAlbum)
            {
                sceneManager.SwitchScene(new AlbumViewerScene(saveData, eventBus, mockInput));
            }
            Assert.IsType<AlbumViewerScene>(sceneManager.CurrentScene);

            // 3. Album requests close -> back to World Map
            var album = (AlbumViewerScene)sceneManager.CurrentScene;
            album.Close();
            if (album.RequestClose)
            {
                sceneManager.SwitchScene(new WorldMapScene(saveData, eventBus, mockInput));
            }
            Assert.IsType<WorldMapScene>(sceneManager.CurrentScene);

            // 4. World Map travels back to Hanoi -> Exploration
            var wm2 = (WorldMapScene)sceneManager.CurrentScene;
            wm2.SelectRegion("Hanoi");
            wm2.ConfirmTravel();
            if (wm2.RequestTravelTo == "Hanoi")
            {
                expScene = new HanoiExplorationScene(saveData, inputService: mockInput, eventBus: eventBus);
                sceneManager.SwitchScene(expScene);
            }
            Assert.IsType<HanoiExplorationScene>(sceneManager.CurrentScene);
        }
    }

    [Fact]
    public void AlbumViewer_InExplorationOverlay_MultipleOpenCloseCycles()
    {
        var saveData = SaveData.CreateDefault();
        var eventBus = new EventBus();
        var mockInput = new MockInputService();
        var expScene = new HanoiExplorationScene(saveData, inputService: mockInput, eventBus: eventBus);
        expScene.Enter();

        for (int i = 0; i < 15; i++)
        {
            // Toggle Album Open via input service [J]
            mockInput.PressAlbum();
            expScene.Update(0.016f);
            mockInput.ReleaseAll();
            Assert.Equal(GameState.Album, expScene.StateManager.CurrentState);

            // Toggle Album Close via cancel [Escape]
            mockInput.PressCancel();
            expScene.Update(0.016f);
            mockInput.ReleaseAll();
            Assert.Equal(GameState.Exploration, expScene.StateManager.CurrentState);
        }
    }

    [Fact]
    public void AlbumViewer_InputService_CancelAndAlbumTriggersRequestClose()
    {
        var saveData = SaveData.CreateDefault();
        var mockInput = new MockInputService();
        var viewer = new AlbumViewerScene(saveData, inputService: mockInput);
        viewer.Enter();

        Assert.False(viewer.RequestClose);

        // 1. IsCancelJustPressed triggers close
        mockInput.PressCancel();
        viewer.Update(0.016f);
        Assert.True(viewer.RequestClose);

        // Reset via Enter()
        viewer.Enter();
        Assert.False(viewer.RequestClose);
        mockInput.ReleaseAll();

        // 2. IsAlbumJustPressed triggers close
        mockInput.PressAlbum();
        viewer.Update(0.016f);
        Assert.True(viewer.RequestClose);
    }
}
