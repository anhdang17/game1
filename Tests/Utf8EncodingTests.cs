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
}
