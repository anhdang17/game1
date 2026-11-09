using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.WorldMap;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// World Map Scene — displays all 4 Vietnamese regions as location pins on a stylised Vietnam map.
/// Regions are desaturated (gray) until unlocked; Hanoi starts unlocked.
/// Player can click an unlocked region to travel to it.
/// For the Vertical Slice, only Hanoi is enterable — others show a "coming soon" message.
/// </summary>
public sealed class WorldMapScene : IScene
{
    private readonly SaveData _saveData;
    private readonly IEventBus _eventBus;
    private readonly WorldMapService _mapService = new();

    private string? _hoveredRegionId;
    private string? _selectedRegionId;
    private string? _feedbackMsg;
    private float _feedbackTimer;
    private float _totalTime;

    /// Set to RegionId when player confirms travel to a region
    public string? RequestTravelTo { get; private set; }
    /// Set true when player presses [Tab]/[A] to open Album
    public bool RequestAlbum { get; private set; }

    // Map pin positions (as fractions of 1280x720)
    private static readonly Dictionary<string, Vector2> PinPositions = new()
    {
        { "Hanoi",   new Vector2(590f, 155f) },
        { "Hue",     new Vector2(640f, 400f) },
        { "HoiAn",   new Vector2(680f, 460f) },
        { "SaiGon",  new Vector2(610f, 590f) },
    };

    public WorldMapScene(SaveData saveData, IEventBus eventBus)
    {
        _saveData = saveData;
        _eventBus = eventBus;
    }

    public void Enter()
    {
        RequestTravelTo = null;
        RequestAlbum = false;
        _feedbackMsg = "Chào mừng đến Bản Đồ Du Ký! Chọn địa danh để bắt đầu hành trình tiếp theo.";
        _feedbackTimer = 5f;
    }

    public void Update(float dt)
    {
        _totalTime += dt;
        if (_feedbackTimer > 0f) _feedbackTimer -= dt;

        Vector2 mouse = Raylib.GetMousePosition();
        _hoveredRegionId = GetPinAtMouse(mouse);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (_hoveredRegionId != null)
            {
                var nodes = _mapService.GetRegionNodes(_saveData.WorldProgression);
                bool canSelect = _mapService.TrySelectRegion(_hoveredRegionId, _saveData.WorldProgression, out string failReason);
                if (canSelect)
                {
                    _selectedRegionId = _hoveredRegionId;
                    _feedbackMsg = $"Đến {GetDisplayName(_hoveredRegionId)}? Nhấn [Enter] để xác nhận.";
                    _feedbackTimer = 4f;
                }
                else
                {
                    _feedbackMsg = failReason;
                    _feedbackTimer = 3f;
                    _selectedRegionId = null;
                }
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) && _selectedRegionId != null)
        {
            RequestTravelTo = _selectedRegionId;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Tab) || Raylib.IsKeyPressed(KeyboardKey.A))
        {
            RequestAlbum = true;
        }
    }

    private string? GetPinAtMouse(Vector2 mouse)
    {
        foreach (var (id, pos) in PinPositions)
        {
            if (Vector2.Distance(mouse, pos) <= 28f)
                return id;
        }
        return null;
    }

    private static string GetDisplayName(string regionId) => regionId switch
    {
        "Hanoi"  => "Hà Nội",
        "Hue"    => "Cố Đô Huế",
        "HoiAn"  => "Phố Cổ Hội An",
        "SaiGon" => "Sài Gòn",
        _        => regionId
    };

    public void Draw()
    {
        // Sky gradient background
        for (int y = 0; y < 720; y++)
        {
            float t = y / 720f;
            byte r = (byte)(180 + (200 - 180) * t);
            byte g = (byte)(220 + (240 - 220) * t);
            byte b = (byte)(255);
            Raylib.DrawLine(0, y, 1280, y, new Color((int)r, (int)g, (int)b, 255));
        }

        DrawTitle();
        DrawVietnamSilhouette();
        DrawRegionPins();
        DrawLegend();
        DrawFeedback();
        DrawBottomBar();
    }

    private void DrawTitle()
    {
        string title = "Bản Đồ Du Ký Bé Mây";
        Vector2 sz = FontManager.Instance.MeasureText(title, 32f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(640f - sz.X / 2f, 20f), 32f,
            new Color(255, 220, 60, 255), HanoiPalette.CartoonOutline, 2.5f);

        string sub = "Click chọn địa danh · [Enter] Du hành · [Tab] Mở Album Sưu Tập";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 12f);
        FontManager.Instance.DrawText(sub, new Vector2(640f - subSz.X / 2f, 62f), 12f, new Color(60, 50, 40, 200));
    }

    private void DrawVietnamSilhouette()
    {
        // Stylised Vietnam S-curve shape using colored boxes + circles
        Color landColor = HanoiPalette.ApplyDesaturation(new Color(144, 190, 109, 255),
            _saveData.WorldProgression.GetColorRestoration("Hanoi") * 0.5f + 20f);
        Color borderColor = new Color(80, 110, 60, 255);

        // Northern region (Hanoi area)
        VectorRenderer.DrawRoundedRect(new Rectangle(500f, 120f, 180f, 140f), 0.3f, landColor, borderColor, 1.5f, withShadow: false);
        // Central narrow
        VectorRenderer.DrawRoundedRect(new Rectangle(560f, 260f, 130f, 180f), 0.2f, landColor, borderColor, 1.5f, withShadow: false);
        // Central wide (Hue / Da Nang)
        VectorRenderer.DrawRoundedRect(new Rectangle(540f, 380f, 200f, 100f), 0.25f, landColor, borderColor, 1.5f, withShadow: false);
        // Southern (Ho Chi Minh / Mekong)
        VectorRenderer.DrawRoundedRect(new Rectangle(500f, 490f, 240f, 140f), 0.35f, landColor, borderColor, 1.5f, withShadow: false);

        // Ocean
        Color seaColor = HanoiPalette.ApplyDesaturation(new Color(100, 180, 220, 200), 50f);
        FontManager.Instance.DrawText("Biển Đông", new Vector2(820f, 380f), 16f, seaColor);

        // Animated wave dots
        for (int i = 0; i < 6; i++)
        {
            float wx = 790f + i * 30f;
            float wy = 420f + MathF.Sin(_totalTime * 1.5f + i * 0.8f) * 5f;
            Raylib.DrawCircleV(new Vector2(wx, wy), 3f, Raylib.ColorAlpha(seaColor, 0.7f));
        }
    }

    private void DrawRegionPins()
    {
        var nodes = _mapService.GetRegionNodes(_saveData.WorldProgression);

        foreach (var node in nodes)
        {
            if (!PinPositions.TryGetValue(node.RegionId, out Vector2 pos)) continue;

            bool isHovered = _hoveredRegionId == node.RegionId;
            bool isSelected = _selectedRegionId == node.RegionId;
            bool isUnlocked = node.Status != RegionStatus.Locked;
            bool isCompleted = node.Status == RegionStatus.Completed;

            float bob = isHovered ? MathF.Sin(_totalTime * 4f) * 4f : 0f;
            Vector2 pinPos = new(pos.X, pos.Y + bob);

            // Shadow
            Raylib.DrawEllipse((int)pinPos.X + 2, (int)pinPos.Y + 30, 14f, 6f, Raylib.ColorAlpha(Color.Black, 0.22f));

            // Pin body
            float pinR = isSelected ? 24f : (isHovered ? 22f : 20f);
            Color pinColor = isCompleted ? new Color(255, 220, 50, 255) :
                             isUnlocked ? new Color(60, 180, 100, 255) :
                             new Color(130, 130, 130, 180);
            VectorRenderer.DrawCartoonCircle(pinPos, pinR, pinColor, HanoiPalette.CartoonOutline, 2f);

            // Icon inside pin
            if (isCompleted)
            {
                string star = "✓";
                Vector2 starSz = FontManager.Instance.MeasureText(star, 18f);
                FontManager.Instance.DrawText(star, new Vector2(pinPos.X - starSz.X / 2f, pinPos.Y - 11f), 18f, Color.White);
            }
            else if (isUnlocked)
            {
                // Color restoration arc
                float pct = node.ColorRestorationPercent / 100f;
                if (pct > 0f)
                    Raylib.DrawRing(pinPos, pinR - 7f, pinR - 3f, -90f, -90f + 360f * pct, 24, new Color(255, 255, 255, 180));
            }
            else
            {
                // Lock icon
                string lockStr = "🔒";
                Vector2 lkSz = FontManager.Instance.MeasureText(lockStr, 14f);
                FontManager.Instance.DrawText(lockStr, new Vector2(pinPos.X - lkSz.X / 2f, pinPos.Y - 9f), 14f,
                    new Color(200, 200, 200, 200));
            }

            // Region name label
            string displayName = node.DisplayName;
            Vector2 nameSz = FontManager.Instance.MeasureText(displayName, 13f);
            Rectangle nameBg = new(pinPos.X - nameSz.X / 2f - 6f, pinPos.Y + pinR + 4f, nameSz.X + 12f, 22f);
            VectorRenderer.DrawRoundedRect(nameBg, 0.4f,
                isUnlocked ? new Color(255, 252, 240, 230) : new Color(180, 180, 180, 180),
                HanoiPalette.CartoonOutline, 1f, withShadow: false);
            FontManager.Instance.DrawText(displayName,
                new Vector2(pinPos.X - nameSz.X / 2f, pinPos.Y + pinR + 6f), 13f,
                isUnlocked ? HanoiPalette.CartoonOutline : new Color(100, 100, 100, 200));

            // Color % for unlocked
            if (isUnlocked && !isCompleted)
            {
                string pctStr = $"{node.ColorRestorationPercent:F0}%";
                Vector2 pSz = FontManager.Instance.MeasureText(pctStr, 10f);
                FontManager.Instance.DrawText(pctStr,
                    new Vector2(pinPos.X - pSz.X / 2f, pinPos.Y + pinR + 24f), 10f,
                    new Color(50, 150, 80, 220));
            }
        }
    }

    private void DrawLegend()
    {
        Rectangle legendBox = new(30f, 500f, 200f, 190f);
        VectorRenderer.DrawRoundedRect(legendBox, 0.25f, new Color(255, 252, 240, 220),
            HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("Chú thích:", new Vector2(46f, 514f), 13f, new Color(80, 60, 40, 255));

        // Completed
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 548f), 12f, new Color(255, 220, 50, 255),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText("Đã hoàn thành", new Vector2(76f, 540f), 12f, new Color(60, 50, 35, 255));

        // Unlocked
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 578f), 12f, new Color(60, 180, 100, 255),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText("Đã mở khóa", new Vector2(76f, 570f), 12f, new Color(60, 50, 35, 255));

        // Locked
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 608f), 12f, new Color(130, 130, 130, 180),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText("Chưa mở khóa", new Vector2(76f, 600f), 12f, new Color(100, 90, 85, 200));

        // Album hint
        Rectangle albumBtn = new(30f, 630f, 200f, 40f);
        VectorRenderer.DrawRoundedRect(albumBtn, 0.4f, new Color(100, 160, 240, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[Tab] Mở Album", new Vector2(46f, 642f), 13f, Color.White);
    }

    private void DrawFeedback()
    {
        if (string.IsNullOrEmpty(_feedbackMsg) || _feedbackTimer <= 0f) return;
        float alpha = Math.Clamp(_feedbackTimer, 0f, 1f);
        Vector2 sz = FontManager.Instance.MeasureText(_feedbackMsg, 14f);
        float bw = Math.Min(sz.X + 32f, 900f);
        Rectangle box = new(640f - bw / 2f, 672f, bw, 36f);
        VectorRenderer.DrawRoundedRect(box, 0.35f,
            Raylib.ColorAlpha(new Color(50, 100, 180, 255), alpha * 0.9f),
            Color.White, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(_feedbackMsg, new Vector2(box.X + 16f, box.Y + 10f), 14f,
            Raylib.ColorAlpha(Color.White, alpha));
    }

    private void DrawBottomBar()
    {
        int pagesCount = _saveData.CollectionAlbum.CollectedJournalPages.Count;
        int stampsCount = _saveData.CollectionAlbum.CollectedStamps.Count;
        string info = $"Trang Sổ: {pagesCount}/7  ·  Tem: {stampsCount}";
        Vector2 infoSz = FontManager.Instance.MeasureText(info, 13f);
        FontManager.Instance.DrawText(info, new Vector2(640f - infoSz.X / 2f, 86f), 13f,
            new Color(60, 50, 40, 200));
    }

    public void Exit() { }
    public void Dispose() { }
}

