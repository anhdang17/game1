using System;
using System.Collections.Generic;
using System.Linq;
using GameProject.Core.Models;

namespace GameProject.Core.WorldMap;

public enum RegionStatus
{
    Locked = 0,
    Unlocked = 1,
    Completed = 2
}

public sealed class RegionNode
{
    public string RegionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public RegionStatus Status { get; set; } = RegionStatus.Locked;
    public float ColorRestorationPercent { get; set; } = 0.0f;
}

public interface IWorldMapService
{
    IReadOnlyList<RegionNode> GetRegionNodes(WorldProgression progression);
    IReadOnlyList<RegionNode> GetWorldWonderNodes(WorldProgression progression);
    bool TrySelectRegion(string regionId, WorldProgression progression, out string failureReason);
}

public sealed class WorldMapService : IWorldMapService
{
    private static readonly List<RegionNode> BaseNodes = new()
    {
        new RegionNode
        {
            RegionId = RegionIds.Hanoi,
            DisplayName = "Hà Nội",
            Description = "Thủ đô nghìn năm văn hiến, phố cổ rêu phong và hương vị phở nồng ấm."
        },
        new RegionNode
        {
            RegionId = RegionIds.Hue,
            DisplayName = "Cố Đô Huế",
            Description = "Quần thể di tích cung đình cổ kính bên dòng sông Hương thơ mộng."
        },
        new RegionNode
        {
            RegionId = RegionIds.HoiAn,
            DisplayName = "Phố Cổ Hội An",
            Description = "Thương cảng cổ rực rỡ đèn lồng và kiến trúc giao thoa độc đáo."
        },
        new RegionNode
        {
            RegionId = RegionIds.SaiGon,
            DisplayName = "Sài Gòn - Chợ Lớn",
            Description = "Đô thị sôi động, dòng sông Sài Gòn và văn hóa cà phê vợt đặc trưng."
        }
    };

    public static readonly List<RegionNode> SevenWonderNodes = new()
    {
        new RegionNode
        {
            RegionId = RegionIds.Hanoi,
            DisplayName = "Hà Nội (Việt Nam)",
            Description = "Trang Khởi Đầu — Phố cổ 36 phố phường, ngói rêu phong và hương vị phở Bà An."
        },
        new RegionNode
        {
            RegionId = RegionIds.Kyoto,
            DisplayName = "Kyoto (Nhật Bản)",
            Description = "Trang Mùa Xuân — Cổng Torii đỏ son, phố cổ Gion và rặng hoa anh đào bay trong gió."
        },
        new RegionNode
        {
            RegionId = RegionIds.Cairo,
            DisplayName = "Cairo (Ai Cập)",
            Description = "Trang Cát Vàng — Kim Tự Tháp nghìn năm, sông Nile huyền bí và tượng Nhân Sư trầm mặc."
        },
        new RegionNode
        {
            RegionId = RegionIds.Paris,
            DisplayName = "Paris (Pháp)",
            Description = "Trang Ánh Sáng — Tháp Eiffel lấp lánh, dòng sông Seine thơ mộng và quán cà phê hè phố."
        },
        new RegionNode
        {
            RegionId = RegionIds.Rio,
            DisplayName = "Rio de Janeiro (Brazil)",
            Description = "Trang Âm Nhạc — Rừng mưa Amazon, tượng Chúa Cứu Thế trên đỉnh mây và vũ điệu Samba."
        },
        new RegionNode
        {
            RegionId = RegionIds.BacCuc,
            DisplayName = "Bắc Cực (The Arctic)",
            Description = "Trang Băng Tuyết — Cực quang huyền ảo nhảy múa trên vương quốc băng tuyết vĩnh cửu."
        },
        new RegionNode
        {
            RegionId = RegionIds.BauTroiKyUc,
            DisplayName = "Bầu Trời Ký Ức",
            Description = "Trang Hồi Sinh — Cung điện mây bồng bềnh nơi mọi sắc màu bị đánh cắp tụ hội."
        }
    };

    public IReadOnlyList<RegionNode> GetRegionNodes(WorldProgression progression)
    {
        return BuildNodeList(BaseNodes, progression);
    }

    public IReadOnlyList<RegionNode> GetWorldWonderNodes(WorldProgression progression)
    {
        return BuildNodeList(SevenWonderNodes, progression);
    }

    private static List<RegionNode> BuildNodeList(IEnumerable<RegionNode> templates, WorldProgression progression)
    {
        var result = new List<RegionNode>();

        foreach (var baseNode in templates)
        {
            var node = new RegionNode
            {
                RegionId = baseNode.RegionId,
                DisplayName = baseNode.DisplayName,
                Description = baseNode.Description
            };

            if (progression.IsRegionCompleted(baseNode.RegionId))
            {
                node.Status = RegionStatus.Completed;
                node.ColorRestorationPercent = 100.0f;
            }
            else if (progression.IsRegionUnlocked(baseNode.RegionId))
            {
                node.Status = RegionStatus.Unlocked;
                node.ColorRestorationPercent = progression.GetColorRestoration(baseNode.RegionId);
            }
            else
            {
                node.Status = RegionStatus.Locked;
                node.ColorRestorationPercent = 0.0f;
            }

            result.Add(node);
        }

        return result;
    }

    public bool TrySelectRegion(string regionId, WorldProgression progression, out string failureReason)
    {
        failureReason = string.Empty;

        var node = GetWorldWonderNodes(progression).FirstOrDefault(n => string.Equals(n.RegionId, regionId, StringComparison.OrdinalIgnoreCase))
                ?? GetRegionNodes(progression).FirstOrDefault(n => string.Equals(n.RegionId, regionId, StringComparison.OrdinalIgnoreCase));
        if (node == null)
        {
            failureReason = "Vùng đất không tồn tại trên bản đồ!";
            return false;
        }

        if (node.Status == RegionStatus.Locked)
        {
            failureReason = "Vùng đất này còn bị bao phủ trong bóng xám! Hãy hoàn thành các nhiệm vụ trước để mở khóa.";
            return false;
        }

        return true;
    }
}
