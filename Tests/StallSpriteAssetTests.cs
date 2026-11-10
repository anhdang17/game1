using System;
using System.IO;
using System.Linq;
using GameProject.Core.Interactions;
using GameProject.Graphics;
using GameProject.Stages;
using Xunit;

namespace GameProject.Tests;

public class StallSpriteAssetTests
{
    private static readonly string[] RequiredSpriteFiles =
    [
        "stall_baan_pho.png",
        "stall_bactu_beef.png",
        "stall_colan_veggie.png"
    ];

    [Theory]
    [InlineData("stall_baan_pho.png")]
    [InlineData("stall_bactu_beef.png")]
    [InlineData("stall_colan_veggie.png")]
    public void StallSprite_ExistsInProjectDataDirectory(string filename)
    {
        string path = VectorRenderer.ResolveTexturePath(filename);
        Assert.True(File.Exists(path), $"Sprite texture file {filename} not found at resolved path: {path}");
    }

    [Theory]
    [InlineData("stall_baan_pho.png")]
    [InlineData("stall_bactu_beef.png")]
    [InlineData("stall_colan_veggie.png")]
    public void StallSprite_IsValidPngWithRgbaChannels(string filename)
    {
        string path = VectorRenderer.ResolveTexturePath(filename);
        byte[] bytes = File.ReadAllBytes(path);

        // Standard PNG 8-byte header: 0x89, 'P', 'N', 'G', 0x0D, 0x0A, 0x1A, 0x0A
        byte[] expectedHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        Assert.True(bytes.Length > 32, "PNG file too small");
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(expectedHeader[i], bytes[i]);
        }

        // IHDR chunk: starts at byte 12 (length 4 bytes), chunk type "IHDR" (bytes 12..15)
        // Width: bytes 16..19 (big-endian), Height: bytes 20..23 (big-endian)
        int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        byte bitDepth = bytes[24];
        byte colorType = bytes[25]; // 6 = RGBA (truecolor with alpha)

        Assert.True(width >= 800, $"Expected sprite width >= 800, got {width}");
        Assert.True(height >= 700, $"Expected sprite height >= 700, got {height}");
        Assert.Equal(8, bitDepth);
        Assert.Equal(6, colorType); // RGBA format
    }

    [Fact]
    public void HanoiExplorationScene_AllStallsInteractablesAndCollidersPreserved()
    {
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();

        Assert.NotNull(baAn);
        Assert.Equal(new System.Numerics.Vector2(200f, 300f), baAn.Position);

        var thitBo = merchants.FirstOrDefault(m => m.Id == "Stall_ThitBo");
        Assert.NotNull(thitBo);
        Assert.Equal(new System.Numerics.Vector2(560f, 240f), thitBo.Position);
        Assert.Equal("Quầy Thịt Bò Bác Tư", thitBo.DisplayName);

        var hanhLa = merchants.FirstOrDefault(m => m.Id == "Stall_HanhLa");
        Assert.NotNull(hanhLa);
        Assert.Equal(new System.Numerics.Vector2(440f, 420f), hanhLa.Position);
        Assert.Equal("Gánh Rau Tươi Cô Lan", hanhLa.DisplayName);
    }

    [Fact]
    public void VectorRenderer_UnloadStallTextures_ExecutesSafelyWhenUninitialized()
    {
        // Safe to call even if window is not ready / headless
        VectorRenderer.UnloadStallTextures();
    }

    [Fact]
    public void VectorRenderer_UnloadStallTextures_IsIdempotent()
    {
        // Multiple consecutive unloads should never crash or throw
        VectorRenderer.UnloadStallTextures();
        VectorRenderer.UnloadStallTextures();
        VectorRenderer.UnloadStallTextures();
    }

    [Fact]
    public void VectorRenderer_DrawStalls_ExecuteSafelyHeadless()
    {
        var dummyPos = new System.Numerics.Vector2(100f, 100f);
        // None of these should throw when Raylib window is uninitialized
        VectorRenderer.DrawBaAnPhoStall(dummyPos, "Bà An", 0f, 1.0f, true);
        VectorRenderer.DrawBaAnPhoStall(dummyPos, "Bà An", 0f, 1.0f, false);
        VectorRenderer.DrawBacTuBeefStall(dummyPos, "Bác Tư", 0f, 1.0f, true);
        VectorRenderer.DrawBacTuBeefStall(dummyPos, "Bác Tư", 0f, 1.0f, false);
        VectorRenderer.DrawCoLanVeggieStall(dummyPos, "Cô Lan", 0f, 1.0f, true);
        VectorRenderer.DrawCoLanVeggieStall(dummyPos, "Cô Lan", 0f, 1.0f, false);
    }

    [Theory]
    [InlineData("stall_baan_pho.png")]
    [InlineData("stall_bactu_beef.png")]
    [InlineData("stall_colan_veggie.png")]
    public void StallSprite_DataAndSourceData_AreIdentical(string filename)
    {
        string p1 = Path.Combine("Data", filename);
        string p2 = Path.Combine("Source", "Data", filename);
        if (File.Exists(p1) && File.Exists(p2))
        {
            byte[] b1 = File.ReadAllBytes(p1);
            byte[] b2 = File.ReadAllBytes(p2);
            Assert.Equal(b1.Length, b2.Length);
            Assert.True(b1.SequenceEqual(b2), $"Assets {p1} and {p2} differ!");
        }
    }
}
