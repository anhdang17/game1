using System;
using System.Collections.Generic;
using GameProject.Core.Models;

namespace GameProject.Core.Quests;

/// <summary>
/// Factory defining authentic Hue Chapter 2 quests (Q1, Q2, Q3).
/// </summary>
public static class HueQuests
{
    public const string Quest1Id = "Quest_Hue_01";
    public const string Quest2Id = "Quest_Hue_02";
    public const string Quest3Id = "Quest_Hue_03";

    public static QuestDefinition CreateQuest1() => new()
    {
        Id = Quest1Id,
        Title = "Thu thập nguyên liệu nấu Bún Bò Huế",
        Description = "Gặp gỡ các tiểu thương tại chợ Đông Ba và các phường nghề truyền thống để thu thập đủ 5 nguyên liệu nấu Bún Bò Huế cho Mệ Bích.",
        RegionId = WorldProgression.RegionHue,
        PrerequisiteQuestIds = Array.Empty<string>(),
        Objectives = new List<QuestObjective>
        {
            new(ItemIds.BunSoiTo, "Bún sợi to làng Vân Cù", targetCount: 1),
            new(ItemIds.BapBo, "Bắp bò hoa tươi ngon", targetCount: 1),
            new(ItemIds.ChaCua, "Chả cua quết nhuyễn đậm đà", targetCount: 1),
            new(ItemIds.NuocDungHue, "Nước dùng ruốc sả thơm nồng", targetCount: 1),
            new(ItemIds.RauSongHue, "Rau sống, bắp chuối bào thanh mát", targetCount: 1)
        }
    };

    public static QuestDefinition CreateQuest2() => new()
    {
        Id = Quest2Id,
        Title = "Nấu Bún Bò Huế chuẩn vị Cố Đô",
        Description = "Hoàn thành quy trình nấu và bày biện tô Bún Bò Huế theo đúng hương vị truyền thống của Mệ Bích.",
        RegionId = WorldProgression.RegionHue,
        PrerequisiteQuestIds = new[] { Quest1Id },
        Objectives = new List<QuestObjective>
        {
            new("cook_bun_bo", "Nấu và hoàn thiện tô Bún Bò Huế", targetCount: 1)
        }
    };

    public static QuestDefinition CreateQuest3() => new()
    {
        Id = Quest3Id,
        Title = "Thả Đèn Hoa Đăng trên Sông Hương",
        Description = "Thắp sáng và thả đèn hoa đăng lung linh xuôi dòng Hương Giang, phục hồi sắc màu rực rỡ cho Cố Đô.",
        RegionId = WorldProgression.RegionHue,
        PrerequisiteQuestIds = new[] { Quest2Id },
        Objectives = new List<QuestObjective>
        {
            new("release_hoa_dang", "Thắp sáng hoa đăng và giải mã câu đố trên dòng sông Hương", targetCount: 1)
        }
    };

    /// <summary>
    /// Registers all Hue quests into the provided QuestManager.
    /// </summary>
    public static void RegisterAll(QuestManager questManager)
    {
        ArgumentNullException.ThrowIfNull(questManager);
        questManager.RegisterQuest(CreateQuest1());
        questManager.RegisterQuest(CreateQuest2());
        questManager.RegisterQuest(CreateQuest3());
    }
}
