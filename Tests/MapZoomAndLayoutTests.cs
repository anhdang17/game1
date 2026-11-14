using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Models;
using GameProject.Stages;
using Raylib_cs;
using Xunit;

namespace GameProject.Tests;

public sealed class MapZoomAndLayoutTests
{
    [Fact]
    public void HanoiExploration_CameraZoom_Is085_AndKeepsVisibleWorldWithinMapBounds()
    {
        var data = SaveData.CreateDefault();
        var scene = new HanoiExplorationScene(data);

        Assert.Equal(0.85f, scene.CameraTracker.Zoom);

        // Visible world width and height at 0.85 zoom
        float visW = scene.CameraTracker.ViewportSize.X / scene.CameraTracker.Zoom;
        float visH = scene.CameraTracker.ViewportSize.Y / scene.CameraTracker.Zoom;

        Assert.True(visW > 1400f, $"Visible width should exceed 1400px (was {visW})");
        Assert.True(visH > 800f, $"Visible height should exceed 800px (was {visH})");

        // Test clamping bounds at multiple corners
        Vector2[] testTargets =
        {
            new Vector2(0f, 0f),
            new Vector2(800f, 450f),
            new Vector2(1600f, 900f)
        };

        foreach (var target in testTargets)
        {
            scene.CameraTracker.SnapToTarget(target);
            var bounds = scene.CameraTracker.GetVisibleWorldBounds();

            Assert.True(bounds.MinX >= 0f, $"MinX must be >= 0 (was {bounds.MinX})");
            Assert.True(bounds.MinY >= 0f, $"MinY must be >= 0 (was {bounds.MinY})");
            Assert.True(bounds.MaxX <= 1600f, $"MaxX must be <= 1600 (was {bounds.MaxX})");
            Assert.True(bounds.MaxY <= 900f, $"MaxY must be <= 900 (was {bounds.MaxY})");
        }
    }

    [Fact]
    public void WorldMap_AllNodes_PairwiseDistancesExceed64px()
    {
        var pins = WorldMapScene.PinPositions;
        var list = new List<(string Id, Vector2 Pos)>();
        foreach (var kvp in pins)
        {
            list.Add((kvp.Key, kvp.Value));
        }

        for (int i = 0; i < list.Count; i++)
        {
            for (int j = i + 1; j < list.Count; j++)
            {
                float dist = Vector2.Distance(list[i].Pos, list[j].Pos);
                Assert.True(dist >= 64.0f,
                    $"Pair ({list[i].Id}, {list[j].Id}) distance {dist:F1}px must be >= 64.0px to prevent overlapping hitboxes.");
            }
        }
    }

    [Fact]
    public void WorldMap_BacCucPin_DoesNotOverlapHeaderOrInfoText()
    {
        Vector2 bacCucPos = WorldMapScene.PinPositions["BacCuc"];
        Assert.Equal(440f, bacCucPos.X);
        Assert.Equal(115f, bacCucPos.Y);

        // Header info text is drawn at Y = 66f with font size 12f (bottom edge at ~78f)
        float infoTextBottom = 78f;
        float pinTop = bacCucPos.Y - 22f - 4f; // Radius 22f + max bobbing 4f = 89f

        Assert.True(pinTop > infoTextBottom,
            $"BacCuc top edge ({pinTop}px) must clear header info text bottom ({infoTextBottom}px). Clearance = {pinTop - infoTextBottom}px.");
    }

    [Fact]
    public void WorldMap_RioPin_DoesNotOverlapLegendBoxOrAlbumButton()
    {
        Vector2 rioPos = WorldMapScene.PinPositions["Rio"];
        Assert.Equal(165f, rioPos.X);
        Assert.Equal(410f, rioPos.Y);

        Rectangle legendBox = new(30f, 500f, 200f, 120f);
        Rectangle albumBtn = new(30f, 630f, 200f, 44f);

        // Label pill bottom
        float rioPillBottom = rioPos.Y + 22f + 20f + 4f; // ~456px
        Assert.True(rioPillBottom < legendBox.Y,
            $"Rio label pill bottom ({rioPillBottom}px) must clear Legend Box top ({legendBox.Y}px).");
    }

    [Fact]
    public void WorldMap_HueAndHoiAn_ClickHitboxesAreDisjoint()
    {
        Vector2 huePos = WorldMapScene.PinPositions["Hue"];
        Vector2 hoiAnPos = WorldMapScene.PinPositions["HoiAn"];

        float dist = Vector2.Distance(huePos, hoiAnPos);
        Assert.True(dist >= 64f, $"Distance between Hue and HoiAn must be >= 64px (was {dist:F1}px)");

        var data = SaveData.CreateDefault();
        var scene = new WorldMapScene(data);
        scene.Enter();

        Assert.Equal("Hue", scene.GetPinAtMouse(huePos));
        Assert.Equal("HoiAn", scene.GetPinAtMouse(hoiAnPos));

        // Midpoint should not register either pin (no ambiguous overlapping clicks)
        Vector2 mid = (huePos + hoiAnPos) * 0.5f;
        Assert.Null(scene.GetPinAtMouse(mid));
    }
}
