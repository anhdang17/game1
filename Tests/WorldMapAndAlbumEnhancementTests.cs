using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.WorldMap;
using GameProject.Stages;
using Xunit;

namespace GameProject.Tests;

public class WorldMapAndAlbumEnhancementTests
{
    [Fact]
    public void Album_ThreeTabs_QueryAndDisplayCountCorrectly()
    {
        var albumService = new AlbumService();
        var allPages = albumService.GetAllJournalPages();
        var allStamps = albumService.GetAllRegisteredStamps();
        var allFacts = albumService.GetAllRegisteredFactCards();

        Assert.Equal(2, allPages.Count);
        Assert.Equal(4, allStamps.Count);
        Assert.Equal(4, allFacts.Count);
    }

    [Fact]
    public void Album_Page2_Hue_ExistsWithRichMetadata_AndLockedState()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        var huePage = albumService.GetAllJournalPages().FirstOrDefault(p => p.Id == "JournalPage_Hue_02");
        Assert.NotNull(huePage);
        Assert.Equal(RegionIds.Hue, huePage.Region);
        Assert.Contains("Sông Hương", huePage.Title);
        Assert.Contains("Huế", huePage.Subtitle);
        Assert.Contains("sông Hương", huePage.StoryText);

        // Initially locked
        Assert.False(albumService.IsJournalPageUnlocked(album, huePage.Id));

        // When awarded, becomes unlocked
        albumService.AwardJournalPage(album, huePage.Id);
        Assert.True(albumService.IsJournalPageUnlocked(album, huePage.Id));
    }

    [Fact]
    public void Album_TravelStamps_FourRegionalSlots_HanoiLotusUnlockedAfterClimax()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        var stamps = albumService.GetAllRegisteredStamps();
        Assert.Equal(4, stamps.Count);
        Assert.Equal(AlbumIds.HanoiLotusStamp, stamps[0].Id);
        Assert.Equal("Stamp_Hue_ThienMu", stamps[1].Id);
        Assert.Equal("Stamp_HoiAn_DenLong", stamps[2].Id);
        Assert.Equal("Stamp_SaiGon_BenThanh", stamps[3].Id);

        // Initially none unlocked
        Assert.False(albumService.IsStampUnlocked(album, stamps[0].Id));

        // Award Hanoi Lotus stamp
        albumService.AwardTravelStamp(album, AlbumIds.HanoiLotusStamp);
        Assert.True(albumService.IsStampUnlocked(album, stamps[0].Id));
        Assert.False(albumService.IsStampUnlocked(album, stamps[1].Id));
        Assert.False(albumService.IsStampUnlocked(album, stamps[2].Id));
        Assert.False(albumService.IsStampUnlocked(album, stamps[3].Id));
    }

    [Fact]
    public void Album_FactCards_DualKeyAliasCompatibility()
    {
        var albumService = new AlbumService();
        var album1 = CollectionAlbum.CreateDefault();
        var album2 = CollectionAlbum.CreateDefault();

        // Unlock using alias 1: "FactCard_PhoHaNoi"
        albumService.UnlockFactCard(album1, "FactCard_PhoHaNoi");
        Assert.True(albumService.IsFactCardUnlocked(album1, "FactCard_PhoHaNoi"));
        Assert.True(albumService.IsFactCardUnlocked(album1, AlbumIds.FactCardPho));

        var unlocked1 = albumService.GetUnlockedFactCards(album1);
        Assert.Single(unlocked1);
        Assert.Equal("Nét Tinh Túy Phở Hà Nội", unlocked1[0].Title);

        // Unlock using alias 2: AlbumIds.FactCardPho ("FactCard_Hanoi_Pho")
        albumService.UnlockFactCard(album2, AlbumIds.FactCardPho);
        Assert.True(albumService.IsFactCardUnlocked(album2, "FactCard_PhoHaNoi"));
        Assert.True(albumService.IsFactCardUnlocked(album2, AlbumIds.FactCardPho));

        var unlocked2 = albumService.GetUnlockedFactCards(album2);
        Assert.Single(unlocked2);
        Assert.Equal("Nét Tinh Túy Phở Hà Nội", unlocked2[0].Title);
    }

    [Fact]
    public void Album_TabNavigation_QECyclingAndNumericJumps()
    {
        var saveData = SaveData.CreateDefault();
        var viewer = new AlbumViewerScene(saveData);
        viewer.Enter();

        Assert.Equal(0, viewer.ActiveTab);

        viewer.NextTab();
        Assert.Equal(1, viewer.ActiveTab);

        viewer.NextTab();
        Assert.Equal(2, viewer.ActiveTab);

        viewer.NextTab(); // wraps to 0
        Assert.Equal(0, viewer.ActiveTab);

        viewer.PrevTab(); // wraps to 2
        Assert.Equal(2, viewer.ActiveTab);

        viewer.SelectTab(1);
        Assert.Equal(1, viewer.ActiveTab);

        viewer.SelectTab(0);
        Assert.Equal(0, viewer.ActiveTab);
    }

    [Fact]
    public void WorldMap_ArrowKeyNavigation_CyclesNorthToSouth()
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        Assert.Equal("Hanoi", mapScene.SelectedRegionId);

        mapScene.NavigateNext();
        Assert.Equal("Hue", mapScene.SelectedRegionId);

        mapScene.NavigateNext();
        Assert.Equal("HoiAn", mapScene.SelectedRegionId);

        mapScene.NavigateNext();
        Assert.Equal("SaiGon", mapScene.SelectedRegionId);

        mapScene.NavigateNext(); // wraps to Hanoi
        Assert.Equal("Hanoi", mapScene.SelectedRegionId);

        mapScene.NavigatePrev(); // wraps to SaiGon
        Assert.Equal("SaiGon", mapScene.SelectedRegionId);
    }

    [Fact]
    public void WorldMap_RegionPreviewDataBinding()
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        Assert.Equal("Thủ Đô Hà Nội", WorldMapScene.GetDisplayName(mapScene.SelectedRegionId));

        mapScene.NavigateNext(); // Hue
        Assert.Equal("Cố Đô Huế", WorldMapScene.GetDisplayName(mapScene.SelectedRegionId));
    }

    [Fact]
    public void WorldMap_TravelValidation_PreventsLockedRegionTravel()
    {
        var saveData = SaveData.CreateDefault();
        // Initially, Hue is locked
        saveData.WorldProgression.UnlockedRegions.Remove(RegionIds.Hue);

        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        mapScene.SelectRegion(RegionIds.Hue);
        bool confirmed = mapScene.ConfirmTravel();

        Assert.False(confirmed);
        Assert.Null(mapScene.RequestTravelTo);
        Assert.NotNull(mapScene.FeedbackMessage);
        Assert.Contains("bóng xám", mapScene.FeedbackMessage);
    }

    [Fact]
    public void WorldMap_ClimaxUnlock_EnablesHueSelection()
    {
        var saveData = SaveData.CreateDefault();
        // Simulate level 1 completion climax
        saveData.WorldProgression.UnlockRegion(RegionIds.Hue);

        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        mapScene.SelectRegion(RegionIds.Hue);
        bool confirmed = mapScene.ConfirmTravel();

        Assert.True(confirmed);
        Assert.Equal(RegionIds.Hue, mapScene.RequestTravelTo);
    }

    [Fact]
    public void SceneTransitions_RequestFlagsCoordination()
    {
        var saveData = SaveData.CreateDefault();
        var mapScene = new WorldMapScene(saveData);
        mapScene.Enter();

        Assert.False(mapScene.RequestAlbum);
        mapScene.OpenAlbum();
        Assert.True(mapScene.RequestAlbum);

        var albumScene = new AlbumViewerScene(saveData);
        albumScene.Enter();

        Assert.False(albumScene.RequestClose);
        albumScene.Close();
        Assert.True(albumScene.RequestClose);
    }
}
