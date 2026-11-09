using System;
using System.Collections.Generic;
using GameProject.Core.Models;

namespace GameProject.Core.Quests;

/// <summary>
/// Factory defining authentic Hanoi Vertical Slice quests (Q1, Q2, Q3).
/// </summary>
public static class HanoiQuests
{
    public const string Quest1Id = "Quest_Hanoi_01";
    public const string Quest2Id = "Quest_Hanoi_02";
    public const string Quest3Id = "Quest_Hanoi_03";

    public static QuestDefinition CreateQuest1() => new()
    {
        Id = Quest1Id,
        Title = "Thu thập nguyên liệu nấu Phở",
        Description = "Gặp gỡ các tiểu thương trên phố Hàng Buồm và Lãn Ông để thu thập đủ 5 nguyên liệu nấu phở cho Bà An.",
        RegionId = "Hanoi",
        PrerequisiteQuestIds = Array.Empty<string>(),
        Objectives = new List<QuestObjective>
        {
            new(ItemIds.BanhPho, "Bánh Phở tươi chần kỹ", targetCount: 1),
            new(ItemIds.ThitBo, "Thịt Bò tươi thái mỏng", targetCount: 1),
            new(ItemIds.NuocDung, "Nước dùng hầm xương gia truyền", targetCount: 1),
            new(ItemIds.HanhLa, "Hành lá & Rau mùi tươi", targetCount: 1),
            new(ItemIds.ThaoMoc, "Túi Thảo mộc hồi quế Lãn Ông", targetCount: 1)
        }
    };

    public static QuestDefinition CreateQuest2() => new()
    {
        Id = Quest2Id,
        Title = "Ghép đúng Bát Phở hoàn chỉnh",
        Description = "Sắp xếp 5 nguyên liệu vào bát phở theo đúng trình tự ẩm thực truyền thống Hà Nội.",
        RegionId = "Hanoi",
        PrerequisiteQuestIds = new[] { Quest1Id },
        Objectives = new List<QuestObjective>
        {
            new("assemble_pho", "Xếp các lớp phở đúng thứ tự 5 bước", targetCount: 1)
        }
    };

    public static QuestDefinition CreateQuest3() => new()
    {
        Id = Quest3Id,
        Title = "Sắp xếp gian hàng mở lối ra Cầu Long Biên",
        Description = "Đẩy các sạp hàng hoa, xe xích lô và thùng gốm dọn thông lối đi ra Cầu Long Biên.",
        RegionId = "Hanoi",
        PrerequisiteQuestIds = new[] { Quest2Id },
        Objectives = new List<QuestObjective>
        {
            new("clear_long_bien_path", "Dọn thông đường đi từ đầu phố đến cổng cầu", targetCount: 1)
        }
    };

    /// <summary>
    /// Registers all Hanoi quests into the provided QuestManager.
    /// </summary>
    public static void RegisterAll(QuestManager questManager)
    {
        ArgumentNullException.ThrowIfNull(questManager);
        questManager.RegisterQuest(CreateQuest1());
        questManager.RegisterQuest(CreateQuest2());
        questManager.RegisterQuest(CreateQuest3());
    }
}
