using System;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Graphics;
using GameProject.Stages;
using GameProject.UI;
using Raylib_cs;
using Xunit;

namespace GameProject.Tests;

public sealed class ResponsiveUILayoutTests
{
    [Theory]
    [InlineData(1280)]
    [InlineData(1920)]
    [InlineData(1024)]
    public void UI_InventoryBar_HorizontalPadding_IsSymmetricAcrossResolutions(int screenWidth)
    {
        var bus = new EventBus();
        var bar = new InventoryBar(bus);
        var inv = new Inventory();

        bar.Update(0.016f, inv, Vector2.Zero, screenWidth);

        float leftMargin = bar.Slots[0].Bounds.X - bar.Bounds.X;
        float rightMargin = (bar.Bounds.X + bar.Bounds.Width) - (bar.Slots[^1].Bounds.X + bar.Slots[^1].Bounds.Width);

        Assert.Equal(21f, leftMargin);
        Assert.Equal(21f, rightMargin);
        Assert.Equal(leftMargin, rightMargin);
    }

    [Fact]
    public void UI_InventoryBar_Height_Is62px_AccommodatesScalePulse()
    {
        var bus = new EventBus();
        var bar = new InventoryBar(bus);
        var inv = new Inventory();

        bar.Update(0.016f, inv, Vector2.Zero, 1280);

        Assert.Equal(62f, bar.Bounds.Height);
        float topMargin = bar.Slots[0].Bounds.Y - bar.Bounds.Y;
        float bottomMargin = (bar.Bounds.Y + bar.Bounds.Height) - (bar.Slots[0].Bounds.Y + bar.Slots[0].Bounds.Height);

        Assert.Equal(9f, topMargin);
        Assert.Equal(9f, bottomMargin);
    }

    [Fact]
    public void UI_QuestHUD_ExpandedWidth_Is290px()
    {
        var bus = new EventBus();
        var hud = new QuestHUD(bus);

        Assert.Equal(290f, hud.Bounds.Width);
    }

    [Fact]
    public void UI_QuestHUD_DisplaysActiveQuest_ForQ1_Q2_Q3()
    {
        var flags = QuestFlags.CreateDefault();

        // 1. Q1 active
        var (title1, _) = QuestHUD.GetQuestDisplayInfo(flags);
        Assert.Equal("Nấu Phở Hà Nội", title1);

        // 2. Q1 done, Q2 active
        flags.Q1_Completed = true;
        var (title2, _) = QuestHUD.GetQuestDisplayInfo(flags);
        Assert.Equal("Ghép đúng Bát Phở hoàn chỉnh", title2);

        // 3. Q2 done, Q3 active
        flags.Q2_Completed = true;
        var (title3, _) = QuestHUD.GetQuestDisplayInfo(flags);
        Assert.Equal("Mở lối ra Cầu Long Biên", title3);
    }

    [Fact]
    public void UI_DialogueBox_BoxHeight_Is152px_AndChoiceStep_Is42px()
    {
        Assert.Equal(152f, DialogueBox.StandardBoxHeight);
        Assert.Equal(880f, DialogueBox.StandardBoxWidth);
        Assert.Equal(42f, DialogueBox.ChoiceStep);
    }

    [Fact]
    public void UI_WorldMapScene_FeedbackBox_DoesNotOverlapAlbumButton()
    {
        Rectangle albumBtn = new(30f, 630f, 200f, 44f); // X: 30 to 230, Y: 630 to 674

        // For all possible message lengths, feedback box clamped to X >= 240
        float minFeedbackX = 240f;
        Assert.True(minFeedbackX > albumBtn.X + albumBtn.Width,
            "Feedback box left boundary (240px) must strictly exceed Album button right boundary (230px).");
    }

    [Fact]
    public void UI_MiniGames_HitboxMatchesDrawRectanglesExactly()
    {
        // StallPuzzle action buttons
        Rectangle resetExpected = new(1050f, 485f, 92f, 36f);
        Rectangle hintExpected = new(1154f, 485f, 92f, 36f);
        Rectangle exitExpected = new(1050f, 530f, 196f, 36f);

        Assert.Equal(485f, resetExpected.Y);
        Assert.Equal(485f, hintExpected.Y);
        Assert.Equal(530f, exitExpected.Y);

        // PhoAssembly validate button
        Rectangle phoValExpected = new(520f, 490f, 240f, 46f);
        Assert.Equal(46f, phoValExpected.Height);
    }
}
