using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Models;

namespace GameProject.Core.Album;

public sealed class JournalPage
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Region { get; set; } = "";
    public string StoryText { get; set; } = "";
    public string IllustrationKey { get; set; } = "";

    public string NarrativeText { get => StoryText; set => StoryText = value; }
    public string ImageKey { get => IllustrationKey; set => IllustrationKey = value; }
    public string RegionId { get => Region; set => Region = value; }
}

public sealed class TravelStamp
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Region { get; set; } = "";
    public string DateEarned { get; set; } = "";
    public string PostmarkText { get; set; } = "";
    public string IconKey { get; set; } = "";

    public string PostmarkDate { get => DateEarned; set => DateEarned = value; }
    public string RegionId { get => Region; set => Region = value; }
}

public sealed class FactCard
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = "";
    public string ImageKey { get; set; } = "";

    public string FullContent { get => Content; set => Content = value; }
    public string Description { get => Summary; set => Summary = value; }
}

public interface IAlbumService
{
    IReadOnlyList<JournalPage> GetUnlockedJournalPages(CollectionAlbum album);
    IReadOnlyList<TravelStamp> GetUnlockedTravelStamps(CollectionAlbum album);
    IReadOnlyList<FactCard> GetUnlockedFactCards(CollectionAlbum album);

    bool AwardJournalPage(CollectionAlbum album, string pageId);
    bool AwardTravelStamp(CollectionAlbum album, string stampId);
    bool UnlockFactCard(CollectionAlbum album, string cardId);
}

public sealed class AlbumService : IAlbumService
{
    public static readonly JournalPage HanoiPage1 = new()
    {
        Id = AlbumIds.HanoiJournalPage,
        Title = "Hương Vị Phố Cổ & Nhịp Cầu Thời Gian",
        Subtitle = "Trang Sổ 1: Ký ức Phố Cổ Hà Nội",
        Region = RegionIds.Hanoi,
        StoryText = "Hà Nội - Những ngày thu thơm mùi hoa sữa và ấm nồng nước phở. Khi sắc màu trở lại, những nụ cười trên phố cổ lại bừng sáng dưới nhịp cầu Long Biên trăm năm lịch sử. Bé Mây và Bút Chì đã giúp cả khu phố tìm lại những ký ức ngọt ngào nhất.",
        IllustrationKey = "Art_Journal_Hanoi_01"
    };

    public static readonly TravelStamp HanoiLotusStamp = new()
    {
        Id = AlbumIds.HanoiLotusStamp,
        Name = "Tem Hoa Sen Hà Nội",
        Subtitle = "Biểu Tượng Tinh Hoa Đất Thăng Long",
        Region = RegionIds.Hanoi,
        DateEarned = DateTime.UtcNow.ToString("dd/MM/yyyy"),
        PostmarkText = "HÀ NỘI - 1000 NĂM VĂN HIẾN",
        IconKey = "Icon_Stamp_HoaSen"
    };

    private static readonly Dictionary<string, JournalPage> AllJournalPages = new()
    {
        {
            AlbumIds.HanoiJournalPage,
            HanoiPage1
        }
    };

    private static readonly Dictionary<string, TravelStamp> AllStamps = new()
    {
        {
            AlbumIds.HanoiLotusStamp,
            HanoiLotusStamp
        }
    };

    private static readonly Dictionary<string, FactCard> AllFactCards = new()
    {
        {
            "FactCard_PhoHaNoi",
            new FactCard
            {
                Id = "FactCard_PhoHaNoi",
                Title = "Nét Tinh Túy Phở Hà Nội",
                Category = "Ẩm Thực",
                Content = "Phở Hà Nội nổi tiếng với nước dùng trong và ngọt sâu từ xương bò hầm nhiều giờ, thoang thoảng hương hồi, quế, thảo quả nướng."
            }
        },
        {
            "FactCard_CauLongBien",
            new FactCard
            {
                Id = "FactCard_CauLongBien",
                Title = "Cầu Long Biên - Dải Lụa Thép Qua Sông Hồng",
                Category = "Lịch Sử",
                Content = "Được xây dựng từ năm 1898-1902, cây cầu thép huyền thoại đã chứng kiến bao thăng trầm lịch sử của thủ đô nghìn năm văn hiến."
            }
        },
        {
            "FactCard_HoaSenTayHo",
            new FactCard
            {
                Id = "FactCard_HoaSenTayHo",
                Title = "Hoa Sen Tây Hồ & Văn Hóa Trà Sen",
                Category = "Văn Hóa",
                Content = "Sen bách diệp Hồ Tây có trăm cánh hồng thắm, được dùng để ướp thứ trà sen thượng hạng nức tiếng chốn kinh kỳ."
            }
        },
        {
            "FactCard_36PhoPhuong",
            new FactCard
            {
                Id = "FactCard_36PhoPhuong",
                Title = "Chuyện 36 Phố Phường",
                Category = "Di Sản",
                Content = "Mỗi con phố xưa gắn liền với một nghề thủ công truyền thống: Hàng Bạc làm vàng bạc, Hàng Gai dệt lụa tơ tằm, Hàng Buồm bán buồm thuyền."
            }
        }
    };

    public IReadOnlyList<JournalPage> GetUnlockedJournalPages(CollectionAlbum album)
    {
        if (album == null) return Array.Empty<JournalPage>();
        return album.CollectedJournalPages
            .Where(id => AllJournalPages.ContainsKey(id))
            .Select(id => AllJournalPages[id])
            .ToList();
    }

    public IReadOnlyList<TravelStamp> GetUnlockedTravelStamps(CollectionAlbum album)
    {
        if (album == null) return Array.Empty<TravelStamp>();
        return album.CollectedStamps
            .Where(id => AllStamps.ContainsKey(id))
            .Select(id => AllStamps[id])
            .ToList();
    }

    public IReadOnlyList<FactCard> GetUnlockedFactCards(CollectionAlbum album)
    {
        if (album == null) return Array.Empty<FactCard>();
        return album.UnlockedFactCards
            .Where(id => AllFactCards.ContainsKey(id))
            .Select(id => AllFactCards[id])
            .ToList();
    }

    public bool AwardJournalPage(CollectionAlbum album, string pageId)
    {
        if (album == null || string.IsNullOrEmpty(pageId)) return false;
        if (!AllJournalPages.ContainsKey(pageId)) return false;

        if (!album.CollectedJournalPages.Contains(pageId))
        {
            album.CollectedJournalPages.Add(pageId);
            return true;
        }
        return false;
    }

    public bool AwardTravelStamp(CollectionAlbum album, string stampId)
    {
        if (album == null || string.IsNullOrEmpty(stampId)) return false;
        if (!AllStamps.ContainsKey(stampId)) return false;

        if (!album.CollectedStamps.Contains(stampId))
        {
            album.CollectedStamps.Add(stampId);
            return true;
        }
        return false;
    }

    public bool UnlockFactCard(CollectionAlbum album, string cardId)
    {
        if (album == null || string.IsNullOrEmpty(cardId)) return false;
        if (!AllFactCards.ContainsKey(cardId)) return false;

        if (!album.UnlockedFactCards.Contains(cardId))
        {
            album.UnlockedFactCards.Add(cardId);
            return true;
        }
        return false;
    }
}
