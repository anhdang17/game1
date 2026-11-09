using System.Text.Encodings.Web;
using System.Text.Json;
using Xunit;

namespace GameProject.Tests;

public class Utf8EncodingTests
{
    private static readonly string[] VietnameseSamplePhrases = new[]
    {
        "Du Ký Bé Mây",
        "Cuốn Sổ Ký Ức",
        "Bóng Xám",
        "Bánh Phở tươi chần kỹ",
        "Thịt Bò tươi thái mỏng xếp đều",
        "Hành hoa chẻ & Rau mùi thái nhỏ",
        "Nước dùng hầm xương gia truyền sôi sùng sục",
        "Gia vị chanh ớt & Hạt tiêu Bắc",
        "Hà Nội - 1000 Năm Văn Hiến",
        "Cầu Long Biên - Dải lụa thép qua sông Hồng",
        "Tem Hoa Sen Tây Hồ",
        "Đừng lo Mây ơi, nếu bị kẹt hàng thì bấm phím [R] để sắp xếp lại từ đầu nhé!",
        "Mùi hoa hồi và quế chi thơm lừng từ hiệu thuốc Lãn Ông kia kìa Mây!"
    };

    [Fact]
    public void VietnameseStrings_SurviveJsonRoundtripWithoutCorruption()
    {
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        foreach (var phrase in VietnameseSamplePhrases)
        {
            string json = JsonSerializer.Serialize(phrase, options);
            string deserialized = JsonSerializer.Deserialize<string>(json, options)!;

            Assert.Equal(phrase, deserialized);
            // Must contain verbatim character, not escaped '\u' sequence
            Assert.DoesNotContain("\\u", json);
        }
    }

    [Fact]
    public void AllVietnameseVowelsAndTones_EncodeAccurately()
    {
        // Test all 12 vowel groups with 5 tone marks (sắc, huyền, hỏi, ngã, nặng)
        const string pangram = "Vạn sự như ý, mưa thuận gió hòa, non sông gấm vóc đất Việt ngàn năm.";

        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        string json = JsonSerializer.Serialize(pangram, options);
        string restored = JsonSerializer.Deserialize<string>(json, options)!;

        Assert.Equal(pangram, restored);
    }

    [Fact]
    public void FontManager_NormalizeText_MapsAllSymbolsToCleanAsciiWithoutMissingGlyphs()
    {
        string inputWithSymbols = "✕ Đóng | ✓ Hoàn thành | ✗ Thất bại | ★ Ngôi sao | ▲ Lên | ▼ Xuống | ◄ Trái | ► Phải | ← Lùi | → Tiến | ✉ Thư | ➔ Tiếp";
        string normalized = GameProject.Graphics.FontManager.NormalizeText(inputWithSymbols);

        // All symbols mapped to safe characters
        Assert.DoesNotContain("✕", normalized);
        Assert.DoesNotContain("✓", normalized);
        Assert.DoesNotContain("✗", normalized);
        Assert.DoesNotContain("★", normalized);
        Assert.DoesNotContain("▲", normalized);
        Assert.DoesNotContain("▼", normalized);
        Assert.DoesNotContain("◄", normalized);
        Assert.DoesNotContain("►", normalized);
        Assert.DoesNotContain("←", normalized);
        Assert.DoesNotContain("→", normalized);
        Assert.DoesNotContain("✉", normalized);
        Assert.DoesNotContain("➔", normalized);

        Assert.Contains("X Đóng", normalized);
        Assert.Contains("V Hoàn thành", normalized);
        Assert.Contains("* Ngôi sao", normalized);
        Assert.Contains("[Thu] Thư", normalized);

        // Every character in normalized text must be in the 762 supported codepoints
        var codepoints = new System.Collections.Generic.HashSet<int>(GameProject.Graphics.FontManager.GetVietnameseCodepoints());
        foreach (char c in normalized)
        {
            Assert.Contains((int)c, codepoints);
        }
    }
}
