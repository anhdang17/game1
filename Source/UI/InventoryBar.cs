using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.UI;

/// <summary>
/// Top-centered inventory HUD hotbar displaying the 5 authentic Phở ingredients with procedural icons.
/// </summary>
public sealed class InventoryBar
{
    public sealed class SlotViewModel
    {
        public string ItemId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public bool IsCollected { get; set; }
        public float ScalePulse { get; set; } = 1.0f;
        public float RippleRadius { get; set; }
        public float RippleAlpha { get; set; }
        public Rectangle Bounds { get; set; }
    }

    private readonly List<SlotViewModel> _slots = new();
    public IReadOnlyList<SlotViewModel> Slots => _slots;
    public Rectangle Bounds { get; private set; }

    private int _hoveredIndex = -1;

    public InventoryBar(IEventBus eventBus)
    {
        _slots.Add(new SlotViewModel { ItemId = ItemIds.BanhPho, DisplayName = "Bánh Phở tươi" });
        _slots.Add(new SlotViewModel { ItemId = ItemIds.ThitBo, DisplayName = "Thịt Bò tươi" });
        _slots.Add(new SlotViewModel { ItemId = ItemIds.NuocDung, DisplayName = "Nước Dùng hầm xương" });
        _slots.Add(new SlotViewModel { ItemId = ItemIds.HanhLa, DisplayName = "Hành Lá & Rau Mùi" });
        _slots.Add(new SlotViewModel { ItemId = ItemIds.ThaoMoc, DisplayName = "Thảo Mộc Lãn Ông" });

        eventBus.Subscribe<ItemCollectedEvent>(OnItemCollected);
    }

    private void OnItemCollected(ItemCollectedEvent ev)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].ItemId == ev.ItemId)
            {
                _slots[i].IsCollected = true;
                _slots[i].ScalePulse = 1.35f;
                _slots[i].RippleRadius = 10f;
                _slots[i].RippleAlpha = 1.0f;
            }
        }
    }

    public void Update(float deltaTime, Inventory inventory, Vector2 pointerPos, int screenWidth = 1280)
    {
        float totalWidth = 310f;
        float totalHeight = 58f;
        float startX = (screenWidth - totalWidth) * 0.5f;
        float startY = 16f;
        Bounds = new Rectangle(startX, startY, totalWidth, totalHeight);

        float slotSize = 44f;
        float spacing = 12f;
        float initialX = startX + 17f;
        float slotY = startY + 7f;

        _hoveredIndex = -1;

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            slot.IsCollected = inventory.HasItem(slot.ItemId);

            float currentSlotX = initialX + i * (slotSize + spacing);
            slot.Bounds = new Rectangle(currentSlotX, slotY, slotSize, slotSize);

            // Animate pulse
            if (slot.ScalePulse > 1.0f)
            {
                slot.ScalePulse = Math.Clamp(slot.ScalePulse - deltaTime * 3.5f, 1.0f, 2.0f);
            }
            if (slot.RippleAlpha > 0f)
            {
                slot.RippleRadius += deltaTime * 50f;
                slot.RippleAlpha = Math.Clamp(slot.RippleAlpha - deltaTime * 2.5f, 0f, 1f);
            }

            // Hover check
            if (Raylib.CheckCollisionPointRec(pointerPos, slot.Bounds))
            {
                _hoveredIndex = i;
            }
        }
    }

    public void Draw()
    {
        if (Bounds.Width <= 0f) return;

        // Container Shadow & Body
        VectorRenderer.DrawDropShadow(Bounds, 0.35f, offsetY: 3f, alpha: 0.28f);
        Raylib.DrawRectangleRounded(Bounds, 0.35f, 8, new Color(24, 28, 36, 225));
        Raylib.DrawRectangleRoundedLinesEx(Bounds, 0.35f, 8, 1.5f, new Color(180, 160, 130, 200));

        // Draw 5 slots
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            Rectangle r = slot.Bounds;

            // Apply pulse scale around center
            if (slot.ScalePulse > 1.001f)
            {
                float cx = r.X + r.Width * 0.5f;
                float cy = r.Y + r.Height * 0.5f;
                float nw = r.Width * slot.ScalePulse;
                float nh = r.Height * slot.ScalePulse;
                r = new Rectangle(cx - nw * 0.5f, cy - nh * 0.5f, nw, nh);
            }

            // Ripple effect
            if (slot.RippleAlpha > 0.01f)
            {
                Vector2 center = new(r.X + r.Width * 0.5f, r.Y + r.Height * 0.5f);
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, slot.RippleRadius, Raylib.ColorAlpha(HanoiPalette.WallYellowLight, slot.RippleAlpha));
            }

            if (slot.IsCollected)
            {
                // Collected: Ivory background, golden rim
                Raylib.DrawRectangleRounded(r, 0.3f, 6, new Color(254, 250, 240, 255));
                Raylib.DrawRectangleRoundedLinesEx(r, 0.3f, 6, 2.0f, HanoiPalette.WallYellow);

                // Draw procedural icon
                DrawIngredientVectorIcon(slot.ItemId, r, isCollected: true);

                // Corner checkmark pip
                Vector2 pipCenter = new(r.X + r.Width - 6f, r.Y + 6f);
                Raylib.DrawCircleV(pipCenter, 4f, new Color(46, 133, 64, 255));
            }
            else
            {
                // Uncollected: Dark cavity, subtle dashed border
                Raylib.DrawRectangleRounded(r, 0.3f, 6, new Color(36, 42, 52, 160));
                Raylib.DrawRectangleRoundedLinesEx(r, 0.3f, 6, 1.0f, new Color(80, 90, 105, 140));

                // Draw muted silhouette
                DrawIngredientVectorIcon(slot.ItemId, r, isCollected: false);
            }
        }

        // Tooltip
        if (_hoveredIndex >= 0 && _hoveredIndex < _slots.Count)
        {
            var slot = _slots[_hoveredIndex];
            string line1 = slot.DisplayName;
            string line2 = slot.IsCollected ? "Đã thu thập" : "Chưa có (Tìm tại chợ)";

            Vector2 sz1 = FontManager.Instance.MeasureText(line1, 13f);
            Vector2 sz2 = FontManager.Instance.MeasureText(line2, 11f);
            float tipW = MathF.Max(sz1.X, sz2.X) + 16f;
            float tipH = sz1.Y + sz2.Y + 14f;

            float tipX = Math.Clamp(slot.Bounds.X + (slot.Bounds.Width - tipW) * 0.5f, 10f, 1280f - tipW - 10f);
            float tipY = Bounds.Y + Bounds.Height + 6f;

            Rectangle tipRec = new(tipX, tipY, tipW, tipH);
            VectorRenderer.DrawRoundedRect(tipRec, 0.25f, new Color(20, 24, 30, 240), HanoiPalette.CartoonOutline, 1.2f);

            FontManager.Instance.DrawText(line1, new Vector2(tipX + 8f, tipY + 4f), 13f, Color.White);
            Color statusColor = slot.IsCollected ? new Color(76, 175, 80, 255) : new Color(180, 180, 180, 255);
            FontManager.Instance.DrawText(line2, new Vector2(tipX + 8f, tipY + sz1.Y + 6f), 11f, statusColor);
        }
    }

    /// <summary>Public entry point for external scenes (e.g. PhoAssemblyScene) to draw ingredient icons.</summary>
    public static void DrawIngredientIcon(string itemId, Vector2 center, float size, bool isCollected)
    {
        Rectangle r = new(center.X - size, center.Y - size, size * 2f, size * 2f);
        DrawIngredientVectorIcon(itemId, r, isCollected);
    }

    private static void DrawIngredientVectorIcon(string itemId, Rectangle r, bool isCollected)
    {
        Vector2 center = new(r.X + r.Width * 0.5f, r.Y + r.Height * 0.5f);

        switch (itemId)
        {
            case ItemIds.BanhPho:
                // Tender white/ivory noodles
                Color noodleColor = isCollected ? new Color(252, 250, 242, 255) : new Color(120, 125, 135, 160);
                Color noodleShadow = isCollected ? new Color(220, 215, 200, 255) : new Color(80, 85, 95, 160);
                Raylib.DrawCircleV(center, 14f, noodleShadow);
                Raylib.DrawCircleV(new Vector2(center.X, center.Y - 1f), 13f, noodleColor);
                // Ribbon folds
                Color lineCol = isCollected ? new Color(200, 190, 170, 255) : new Color(70, 75, 85, 160);
                Raylib.DrawLineEx(new Vector2(center.X - 8f, center.Y - 4f), new Vector2(center.X + 6f, center.Y - 2f), 2f, lineCol);
                Raylib.DrawLineEx(new Vector2(center.X - 6f, center.Y + 2f), new Vector2(center.X + 8f, center.Y + 3f), 2f, lineCol);
                break;

            case ItemIds.ThitBo:
                // Red meat slice with pink marbling
                Color meatRed = isCollected ? new Color(197, 48, 48, 255) : new Color(110, 80, 80, 160);
                Color marbling = isCollected ? new Color(254, 215, 215, 255) : new Color(140, 120, 120, 160);
                Rectangle meatRec = new(center.X - 12f, center.Y - 10f, 24f, 20f);
                Raylib.DrawRectangleRounded(meatRec, 0.45f, 4, meatRed);
                Raylib.DrawLineEx(new Vector2(meatRec.X + 4f, meatRec.Y + 6f), new Vector2(meatRec.X + 18f, meatRec.Y + 8f), 2f, marbling);
                Raylib.DrawLineEx(new Vector2(meatRec.X + 6f, meatRec.Y + 13f), new Vector2(meatRec.X + 16f, meatRec.Y + 14f), 1.5f, marbling);
                break;

            case ItemIds.NuocDung:
                // Golden amber broth droplet / pot
                Color brothAmber = isCollected ? new Color(214, 158, 46, 255) : new Color(115, 105, 80, 160);
                Raylib.DrawCircleV(new Vector2(center.X, center.Y + 2f), 11f, brothAmber);
                Vector2 dropApex = new(center.X, center.Y - 12f);
                Raylib.DrawTriangle(new Vector2(center.X + 9f, center.Y - 1f), dropApex, new Vector2(center.X - 9f, center.Y - 1f), brothAmber);
                if (isCollected)
                {
                    // Steam curl
                    Raylib.DrawCircleV(new Vector2(center.X - 3f, center.Y - 7f), 2.5f, new Color(255, 248, 220, 200));
                }
                break;

            case ItemIds.HanhLa:
                // Chopped green scallion rings + herbs
                Color outerGreen = isCollected ? new Color(47, 133, 90, 255) : new Color(80, 105, 90, 160);
                Color innerLime = isCollected ? new Color(154, 230, 180, 255) : new Color(110, 135, 120, 160);
                Vector2 scallionCenter = new(center.X - 4f, center.Y);
                Raylib.DrawCircleV(scallionCenter, 9f, outerGreen);
                Raylib.DrawCircleV(scallionCenter, 5f, innerLime);
                // Second mini round
                Raylib.DrawCircleV(new Vector2(center.X + 7f, center.Y + 2f), 6f, outerGreen);
                Raylib.DrawCircleV(new Vector2(center.X + 7f, center.Y + 2f), 3f, innerLime);
                break;

            case ItemIds.ThaoMoc:
            case ItemIds.GiaVi:
                // Star anise shape & cinnamon stick
                Color aniseBrown = isCollected ? new Color(123, 52, 30, 255) : new Color(95, 80, 75, 160);
                Color seedDot = isCollected ? new Color(246, 173, 85, 255) : new Color(130, 120, 115, 160);
                Raylib.DrawCircleV(center, 9f, aniseBrown);
                // 4 cross petals
                for (int p = 0; p < 4; p++)
                {
                    float angle = p * MathF.PI * 0.25f;
                    Vector2 off = new(MathF.Cos(angle) * 7f, MathF.Sin(angle) * 7f);
                    Raylib.DrawCircleV(center + off, 4f, aniseBrown);
                }
                Raylib.DrawCircleV(center, 3f, seedDot);
                break;
        }
    }
}

