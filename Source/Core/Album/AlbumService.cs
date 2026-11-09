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

    IReadOnlyList<JournalPage> GetAllJournalPages();
    IReadOnlyList<TravelStamp> GetAllRegisteredStamps();
    IReadOnlyList<FactCard> GetAllRegisteredFactCards();

    bool IsJournalPageUnlocked(CollectionAlbum album, string pageId);
    bool IsStampUnlocked(CollectionAlbum album, string stampId);
    bool IsFactCardUnlocked(CollectionAlbum album, string cardId);

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

    public static readonly JournalPage HuePage2 = new()
    {
        Id = "JournalPage_Hue_02",
        Title = "Dòng Sông Hương & Bóng Cung Thành Cổ",
        Subtitle = "Trang Sổ 2: Ký ức Cố Đô Huế",
        Region = RegionIds.Hue,
        StoryText = "Cố Đô Huế - Dòng sông Hương lững lờ trôi qua những lăng tẩm trầm mặc và Hoàng thành rêu phong. Sau khi phục hồi sắc màu cho Hà Nội, Bé Mây và Bút Chì hướng về phương Nam...",
        IllustrationKey = "Art_Journal_Hue_02"
    };

    public static readonly TravelStamp HanoiLotusStamp = new()
    {
        Id = AlbumIds.HanoiLotusStamp,
        Name = "Tem Hoa Sen Hà Nội",
        Subtitle = "Biểu Tượng Tinh Hoa Đất Thăng Long",
        Region = RegionIds.Hanoi,
        DateEarned = "09/11/2026",
        PostmarkText = "HÀ NỘI - 1000 NĂM VĂN HIẾN",
        IconKey = "Icon_Stamp_HoaSen"
    };

    public static readonly TravelStamp HueStamp = new()
    {
        Id = "Stamp_Hue_ThienMu",
        Name = "Tem Cố Đô Huế",
        Subtitle = "Ngàn Năm Di Sản Cố Đô",
        Region = RegionIds.Hue,
        DateEarned = "",
        PostmarkText = "CỐ ĐÔ HUẾ - DI SẢN THẾ GIỚI",
        IconKey = "Icon_Stamp_Hue"
    };

    public static readonly TravelStamp HoiAnStamp = new()
    {
        Id = "Stamp_HoiAn_DenLong",
        Name = "Tem Phố Cổ Hội An",
        Subtitle = "Thương Cảng Cổ Lung Linh",
        Region = RegionIds.HoiAn,
        DateEarned = "",
        PostmarkText = "HỘI AN - PHỐ ĐÈN LỒNG",
        IconKey = "Icon_Stamp_HoiAn"
    };

    public static readonly TravelStamp SaiGonStamp = new()
    {
        Id = "Stamp_SaiGon_BenThanh",
        Name = "Tem Nắng Phương Nam",
        Subtitle = "Sài Gòn Hoa Lệ & Năng Động",
        Region = RegionIds.SaiGon,
        DateEarned = "",
        PostmarkText = "SÀI GÒN - BẾN THÀNH",
        IconKey = "Icon_Stamp_SaiGon"
    };

    private static readonly Dictionary<string, JournalPage> AllJournalPages = new(StringComparer.OrdinalIgnoreCase)
    {
        { AlbumIds.HanoiJournalPage, HanoiPage1 },
        { HuePage2.Id, HuePage2 }
    };

    private static readonly Dictionary<string, TravelStamp> AllStamps = new(StringComparer.OrdinalIgnoreCase)
    {
        { AlbumIds.HanoiLotusStamp, HanoiLotusStamp },
        { HueStamp.Id, HueStamp },
        { HoiAnStamp.Id, HoiAnStamp },
        { SaiGonStamp.Id, SaiGonStamp }
    };

    private static readonly FactCard PhoCard = new()
    {
        Id = AlbumIds.FactCardPho,
        Title = "Nét Tinh Túy Phở Hà Nội",
        Category = "Ẩm Thực",
        Content = "Phở Hà Nội nổi tiếng với nước dùng trong và ngọt sâu từ xương bò hầm nhiều giờ, thoang thoảng hương hồi, quế, thảo quả nướng."
    };

    private static readonly FactCard LongBienCard = new()
    {
        Id = AlbumIds.FactCardLongBien,
        Title = "Cầu Long Biên - Dải Lụa Thép Qua Sông Hồng",
        Category = "Lịch Sử",
        Content = "Được xây dựng từ năm 1898-1902, cây cầu thép huyền thoại đã chứng kiến bao thăng trầm lịch sử của thủ đô nghìn năm văn hiến."
    };

    private static readonly FactCard SenTayHoCard = new()
    {
        Id = AlbumIds.FactCardSenTayHo,
        Title = "Hoa Sen Tây Hồ & Văn Hóa Trà Sen",
        Category = "Văn Hóa",
        Content = "Sen bách diệp Hồ Tây có trăm cánh hồng thắm, được dùng để ướp thứ trà sen thượng hạng nức tiếng chốn kinh kỳ."
    };

    private static readonly FactCard PhoCoCard = new()
    {
        Id = AlbumIds.FactCardPhoCo,
        Title = "Chuyện 36 Phố Phường",
        Category = "Di Sản",
        Content = "Mỗi con phố xưa gắn liền với một nghề thủ công truyền thống: Hàng Bạc làm vàng bạc, Hàng Gai dệt lụa tơ tằm, Hàng Buồm bán buồm thuyền."
    };

    private static readonly Dictionary<string, FactCard> AllFactCards = new(StringComparer.OrdinalIgnoreCase)
    {
        { "FactCard_PhoHaNoi", PhoCard },
        { AlbumIds.FactCardPho, PhoCard },

        { "FactCard_CauLongBien", LongBienCard },
        { AlbumIds.FactCardLongBien, LongBienCard },

        { "FactCard_HoaSenTayHo", SenTayHoCard },
        { AlbumIds.FactCardSenTayHo, SenTayHoCard },

        { "FactCard_36PhoPhuong", PhoCoCard },
        { AlbumIds.FactCardPhoCo, PhoCoCard }
    };

    public IReadOnlyList<JournalPage> GetAllJournalPages() => new[] { HanoiPage1, HuePage2 };

    public IReadOnlyList<TravelStamp> GetAllRegisteredStamps() => new[] { HanoiLotusStamp, HueStamp, HoiAnStamp, SaiGonStamp };

    public IReadOnlyList<FactCard> GetAllRegisteredFactCards() => new[] { PhoCard, LongBienCard, SenTayHoCard, PhoCoCard };

    public bool IsJournalPageUnlocked(CollectionAlbum album, string pageId)
    {
        if (album == null || string.IsNullOrWhiteSpace(pageId)) return false;
        if (AllJournalPages.TryGetValue(pageId, out var page))
        {
            foreach (var kvp in AllJournalPages)
            {
                if (ReferenceEquals(kvp.Value, page) &&
                    album.CollectedJournalPages.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return album.CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsStampUnlocked(CollectionAlbum album, string stampId)
    {
        if (album == null || string.IsNullOrWhiteSpace(stampId)) return false;
        if (AllStamps.TryGetValue(stampId, out var stamp))
        {
            foreach (var kvp in AllStamps)
            {
                if (ReferenceEquals(kvp.Value, stamp) &&
                    album.CollectedStamps.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return album.CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsFactCardUnlocked(CollectionAlbum album, string cardId)
    {
        if (album == null || string.IsNullOrWhiteSpace(cardId)) return false;
        if (AllFactCards.TryGetValue(cardId, out var card))
        {
            foreach (var kvp in AllFactCards)
            {
                if (ReferenceEquals(kvp.Value, card) &&
                    album.UnlockedFactCards.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return album.UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<JournalPage> GetUnlockedJournalPages(CollectionAlbum album)
    {
        if (album == null) return Array.Empty<JournalPage>();
        var results = new List<JournalPage>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in album.CollectedJournalPages)
        {
            if (AllJournalPages.TryGetValue(id, out var page) && seenIds.Add(page.Id))
            {
                results.Add(page);
            }
        }
        return results;
    }

    public IReadOnlyList<TravelStamp> GetUnlockedTravelStamps(CollectionAlbum album)
    {
        if (album == null) return Array.Empty<TravelStamp>();
        var results = new List<TravelStamp>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in album.CollectedStamps)
        {
            if (AllStamps.TryGetValue(id, out var stamp) && seenIds.Add(stamp.Id))
            {
                results.Add(stamp);
            }
        }
        return results;
    }

    public IReadOnlyList<FactCard> GetUnlockedFactCards(CollectionAlbum album)
    {
        if (album == null) return Array.Empty<FactCard>();
        var results = new List<FactCard>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in album.UnlockedFactCards)
        {
            if (AllFactCards.TryGetValue(id, out var card) && seenIds.Add(card.Id))
            {
                results.Add(card);
            }
        }
        return results;
    }

    public bool AwardJournalPage(CollectionAlbum album, string pageId)
    {
        if (album == null || string.IsNullOrEmpty(pageId)) return false;
        if (!AllJournalPages.ContainsKey(pageId)) return false;

        if (!album.CollectedJournalPages.Contains(pageId, StringComparer.OrdinalIgnoreCase))
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

        if (!album.CollectedStamps.Contains(stampId, StringComparer.OrdinalIgnoreCase))
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

        if (!album.UnlockedFactCards.Contains(cardId, StringComparer.OrdinalIgnoreCase))
        {
            album.UnlockedFactCards.Add(cardId);
            return true;
        }
        return false;
    }
}
