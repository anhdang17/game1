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

    public IReadOnlyList<RegionNode> GetRegionNodes(WorldProgression progression)
    {
        var result = new List<RegionNode>();

        foreach (var baseNode in BaseNodes)
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

        var node = GetRegionNodes(progression).FirstOrDefault(n => n.RegionId == regionId);
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
