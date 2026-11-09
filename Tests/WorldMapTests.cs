using System.Linq;
using GameProject.Core.Models;
using GameProject.Core.WorldMap;
using Xunit;

namespace GameProject.Tests;

public class WorldMapTests
{
    [Fact]
    public void WorldMap_InitialState_HanoiUnlocked0Percent_HueLocked()
    {
        var service = new WorldMapService();
        var progression = WorldProgression.CreateDefault();

        var nodes = service.GetRegionNodes(progression);

        Assert.Equal(4, nodes.Count);

        var hanoi = nodes.First(n => n.RegionId == RegionIds.Hanoi);
        Assert.Equal("Hà Nội", hanoi.DisplayName);
        Assert.Equal(RegionStatus.Unlocked, hanoi.Status);
        Assert.Equal(0.0f, hanoi.ColorRestorationPercent);

        var hue = nodes.First(n => n.RegionId == RegionIds.Hue);
        Assert.Equal("Cố Đô Huế", hue.DisplayName);
        Assert.Equal(RegionStatus.Locked, hue.Status);
        Assert.Equal(0.0f, hue.ColorRestorationPercent);
    }

    [Fact]
    public void TrySelectRegion_UnlockedHanoi_Succeeds()
    {
        var service = new WorldMapService();
        var progression = WorldProgression.CreateDefault();

        bool success = service.TrySelectRegion(RegionIds.Hanoi, progression, out string reason);

        Assert.True(success);
        Assert.Empty(reason);
    }

    [Fact]
    public void TrySelectRegion_LockedHue_FailsWithDescriptiveReason()
    {
        var service = new WorldMapService();
        var progression = WorldProgression.CreateDefault();

        bool success = service.TrySelectRegion(RegionIds.Hue, progression, out string reason);

        Assert.False(success);
        Assert.Contains("bóng xám", reason);
    }

    [Fact]
    public void TrySelectRegion_NonExistentRegion_FailsWithReason()
    {
        var service = new WorldMapService();
        var progression = WorldProgression.CreateDefault();

        bool success = service.TrySelectRegion("Atlantis", progression, out string reason);

        Assert.False(success);
        Assert.Contains("không tồn tại", reason);
    }

    [Fact]
    public void ColorRestoration_ClampsToZeroAndOneHundred()
    {
        var progression = WorldProgression.CreateDefault();

        progression.SetColorRestoration(RegionIds.Hanoi, 150.0f);
        Assert.Equal(100.0f, progression.GetColorRestoration(RegionIds.Hanoi));

        progression.SetColorRestoration(RegionIds.Hanoi, -25.0f);
        Assert.Equal(0.0f, progression.GetColorRestoration(RegionIds.Hanoi));

        progression.SetColorRestoration(RegionIds.Hanoi, 33.3f);
        Assert.Equal(33.3f, progression.GetColorRestoration(RegionIds.Hanoi));
    }

    [Fact]
    public void CompleteRegion_Sets100PercentAndUnlocksHue()
    {
        var service = new WorldMapService();
        var progression = WorldProgression.CreateDefault();

        // Complete Hanoi
        progression.CompleteRegion(RegionIds.Hanoi);
        progression.UnlockRegion(RegionIds.Hue);

        var nodes = service.GetRegionNodes(progression);

        var hanoi = nodes.First(n => n.RegionId == RegionIds.Hanoi);
        Assert.Equal(RegionStatus.Completed, hanoi.Status);
        Assert.Equal(100.0f, hanoi.ColorRestorationPercent);

        var hue = nodes.First(n => n.RegionId == RegionIds.Hue);
        Assert.Equal(RegionStatus.Unlocked, hue.Status);
        Assert.Equal(0.0f, hue.ColorRestorationPercent);

        bool canSelectHue = service.TrySelectRegion(RegionIds.Hue, progression, out _);
        Assert.True(canSelectHue);
    }
}
