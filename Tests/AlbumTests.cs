using GameProject.Core.Album;
using GameProject.Core.Models;
using Xunit;

namespace GameProject.Tests;

public class AlbumTests
{
    [Fact]
    public void Album_InitialState_AllCollectionsAreEmpty()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        var pages = albumService.GetUnlockedJournalPages(album);
        var stamps = albumService.GetUnlockedTravelStamps(album);
        var cards = albumService.GetUnlockedFactCards(album);

        Assert.Empty(pages);
        Assert.Empty(stamps);
        Assert.Empty(cards);
    }

    [Fact]
    public void AwardJournalPage_AppearsInUnlockedPagesWithRichMetadata()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        bool awarded = albumService.AwardJournalPage(album, "JournalPage_Hanoi_01");

        Assert.True(awarded);
        var pages = albumService.GetUnlockedJournalPages(album);
        Assert.Single(pages);

        var page = pages[0];
        Assert.Equal("JournalPage_Hanoi_01", page.Id);
        Assert.Equal("Hương Vị Phố Cổ & Nhịp Cầu Thời Gian", page.Title);
        Assert.Equal(RegionIds.Hanoi, page.Region);
        Assert.Contains("nước phở", page.StoryText);
    }

    [Fact]
    public void AwardJournalPage_DuplicateAward_IsIdempotent()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        bool first = albumService.AwardJournalPage(album, "JournalPage_Hanoi_01");
        bool second = albumService.AwardJournalPage(album, "JournalPage_Hanoi_01");

        Assert.True(first);
        Assert.False(second);
        Assert.Single(album.CollectedJournalPages);
    }

    [Fact]
    public void AwardTravelStamp_DeliversLotusStampWithAuthenticPostmark()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        bool awarded = albumService.AwardTravelStamp(album, "Stamp_HoaSen_Hanoi");

        Assert.True(awarded);
        var stamps = albumService.GetUnlockedTravelStamps(album);
        Assert.Single(stamps);

        var stamp = stamps[0];
        Assert.Equal("Stamp_HoaSen_Hanoi", stamp.Id);
        Assert.Equal("Tem Hoa Sen Hà Nội", stamp.Name);
        Assert.Equal("HÀ NỘI - 1000 NĂM VĂN HIẾN", stamp.PostmarkText);
    }

    [Fact]
    public void UnlockFactCards_UnlocksAll4HanoiCulturalTriviaCards()
    {
        var albumService = new AlbumService();
        var album = CollectionAlbum.CreateDefault();

        albumService.UnlockFactCard(album, "FactCard_PhoHaNoi");
        albumService.UnlockFactCard(album, "FactCard_CauLongBien");
        albumService.UnlockFactCard(album, "FactCard_HoaSenTayHo");
        albumService.UnlockFactCard(album, "FactCard_36PhoPhuong");

        var cards = albumService.GetUnlockedFactCards(album);
        Assert.Equal(4, cards.Count);

        Assert.Contains(cards, c => c.Title == "Nét Tinh Túy Phở Hà Nội");
        Assert.Contains(cards, c => c.Title.Contains("Cầu Long Biên"));
        Assert.Contains(cards, c => c.Title.Contains("Hoa Sen Tây Hồ"));
        Assert.Contains(cards, c => c.Title.Contains("36 Phố Phường"));
    }
}
