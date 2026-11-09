using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Ports;
using GameProject.Core.WorldMap;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// World Map Scene — displays the 4 Vietnamese regions on a stylized 2D vector map of Vietnam.
/// Features Arrow-key and mouse navigation, right-hand region preview panel, travel validation,
/// color restoration progress bars, Biển Đông and archipelago markers.
/// Decoupled from hardware input via IInputService for headless testability.
/// </summary>
public sealed class WorldMapScene : IScene
{
    private readonly SaveData _saveData;
    private readonly IEventBus? _eventBus;
    private readonly IInputService? _inputService;
    private readonly WorldMapService _mapService = new();

    private static readonly string[] RegionOrder = ["Hanoi", "Hue", "HoiAn", "SaiGon"];

    private int _selectedIndex = 0;
    private string? _hoveredRegionId;
    private string? _feedbackMsg;
    private float _feedbackTimer;
    private float _totalTime;

    /// Set to RegionId when player confirms travel to an unlocked region
    public string? RequestTravelTo { get; private set; }
    /// Set true when player requests to open Collection Album
    public bool RequestAlbum { get; private set; }

    public string SelectedRegionId => RegionOrder[_selectedIndex];
    public string? HoveredRegionId => _hoveredRegionId;
    public string? FeedbackMessage => _feedbackMsg;

    // Regional pin anchor coordinates on 1280x720 canvas
    private static readonly Dictionary<string, Vector2> PinPositions = new()
    {
        { "Hanoi",   new Vector2(490f, 155f) },
        { "Hue",     new Vector2(540f, 370f) },
        { "HoiAn",   new Vector2(580f, 430f) },
        { "SaiGon",  new Vector2(510f, 570f) },
    };

    public WorldMapScene(SaveData saveData, IEventBus? eventBus = null, IInputService? inputService = null)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _eventBus = eventBus;
        _inputService = inputService;
    }

    public void Enter()
    {
        RequestTravelTo = null;
        RequestAlbum = false;
        _selectedIndex = 0;
        _feedbackMsg = "Chào mừng đến Bản Đồ Du Ký! Sử dụng [W/S / Mũi tên] hoặc chuột để chọn địa danh.";
        _feedbackTimer = 5f;
    }

    public void SelectRegion(string regionId)
    {
        int idx = Array.IndexOf(RegionOrder, regionId);
        if (idx >= 0)
        {
            _selectedIndex = idx;
        }
    }

    public void NavigateNext()
    {
        _selectedIndex = (_selectedIndex + 1) % RegionOrder.Length;
    }

    public void NavigatePrev()
    {
        _selectedIndex = (_selectedIndex - 1 + RegionOrder.Length) % RegionOrder.Length;
    }

    public bool ConfirmTravel()
    {
        string currentId = SelectedRegionId;
        bool canSelect = _mapService.TrySelectRegion(currentId, _saveData.WorldProgression, out string failReason);

        if (canSelect)
        {
            RequestTravelTo = currentId;
            _feedbackMsg = $"Đang di chuyển đến {GetDisplayName(currentId)}...";
            _feedbackTimer = 3f;
            return true;
        }
        else
        {
            _feedbackMsg = failReason;
            _feedbackTimer = 4f;
            return false;
        }
    }

    public void OpenAlbum()
    {
        RequestAlbum = true;
    }

    public void Update(float dt)
    {
        _totalTime += dt;
        if (_feedbackTimer > 0f) _feedbackTimer -= dt;

        // Check InputService
        if (_inputService?.IsAlbumJustPressed == true)
        {
            OpenAlbum();
            return;
        }

        if (Raylib.IsWindowReady())
        {
            // Arrow key / W / S navigation
            if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.S)) NavigateNext();
            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.W))   NavigatePrev();

            // Confirm travel
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Space))
            {
                ConfirmTravel();
            }

            // Album hotkey
            if (Raylib.IsKeyPressed(KeyboardKey.Tab) || Raylib.IsKeyPressed(KeyboardKey.A) || Raylib.IsKeyPressed(KeyboardKey.J))
            {
                OpenAlbum();
            }

            // Mouse hover & click
            Vector2 mouse = Raylib.GetMousePosition();
            _hoveredRegionId = GetPinAtMouse(mouse);

            if (_hoveredRegionId != null && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                SelectRegion(_hoveredRegionId);
                // If clicked directly on already selected pin -> confirm travel
                if (_hoveredRegionId == SelectedRegionId)
                {
                    ConfirmTravel();
                }
            }

            // Click on travel button in preview card
            Rectangle travelBtn = new(840f + 30f, 130f + 420f, 340f, 44f);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse, travelBtn))
            {
                ConfirmTravel();
            }

            // Click on Album button
            Rectangle albumBtn = new(30f, 630f, 200f, 44f);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse, albumBtn))
            {
                OpenAlbum();
            }
        }
    }

    private string? GetPinAtMouse(Vector2 mouse)
    {
        foreach (var (id, pos) in PinPositions)
        {
            if (Vector2.Distance(mouse, pos) <= 30f)
                return id;
        }
        return null;
    }

    public static string GetDisplayName(string regionId) => regionId switch
    {
        "Hanoi"  => "Thủ Đô Hà Nội",
        "Hue"    => "Cố Đô Huế",
        "HoiAn"  => "Phố Cổ Hội An",
        "SaiGon" => "Sài Gòn - Bến Thành",
        _        => regionId
    };

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        // Sky gradient background
        for (int y = 0; y < 720; y += 4)
        {
            float t = y / 720f;
            byte r = (byte)(180 + (205 - 180) * t);
            byte g = (byte)(220 + (245 - 220) * t);
            byte b = (byte)(255);
            Raylib.DrawRectangle(0, y, 1280, 4, new Color((int)r, (int)g, (int)b, 255));
        }

        DrawTitle();
        DrawVietnamSilhouette();
        DrawArchipelagos();
        DrawRegionPins();
        DrawPreviewCard();
        DrawLegend();
        DrawFeedback();
        DrawBottomBar();
    }

    private void DrawTitle()
    {
        string title = "Bản Đồ Thế Giới Du Ký Bé Mây";
        Vector2 sz = FontManager.Instance.MeasureText(title, 30f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(440f - sz.X / 2f, 20f), 30f,
            new Color(255, 220, 60, 255), HanoiPalette.CartoonOutline, 2.5f);

        string sub = "Sử dụng [W/S / Mũi tên] hoặc chuột để chọn vùng đất  ·  [Enter] Du hành  ·  [Tab] Mở Album";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 12f);
        FontManager.Instance.DrawText(sub, new Vector2(440f - subSz.X / 2f, 60f), 12f, new Color(60, 50, 40, 200));
    }

    private void DrawVietnamSilhouette()
    {
        float hanoiColor = _saveData.WorldProgression.GetColorRestoration("Hanoi");
        Color landColor = HanoiPalette.ApplyDesaturation(new Color(130, 195, 110, 255), hanoiColor * 0.5f + 25f);
        Color borderColor = new(70, 105, 55, 255);

        // Northern region (Hanoi area)
        VectorRenderer.DrawRoundedRect(new Rectangle(410f, 110f, 170f, 140f), 0.35f, landColor, borderColor, 1.5f, withShadow: false);
        // North-Central narrow
        VectorRenderer.DrawRoundedRect(new Rectangle(465f, 245f, 120f, 170f), 0.25f, landColor, borderColor, 1.5f, withShadow: false);
        // Central region (Hue / Da Nang)
        VectorRenderer.DrawRoundedRect(new Rectangle(460f, 360f, 170f, 100f), 0.28f, landColor, borderColor, 1.5f, withShadow: false);
        // South-Central & Southern region (Sai Gon)
        VectorRenderer.DrawRoundedRect(new Rectangle(430f, 480f, 200f, 140f), 0.35f, landColor, borderColor, 1.5f, withShadow: false);

        // Biển Đông & waves
        Color seaColor = HanoiPalette.ApplyDesaturation(new Color(85, 165, 215, 220), 40f);
        FontManager.Instance.DrawText("Biển Đông", new Vector2(660f, 350f), 18f, seaColor);

        for (int i = 0; i < 7; i++)
        {
            float wx = 640f + i * 25f;
            float wy = 385f + MathF.Sin(_totalTime * 1.6f + i * 0.7f) * 4f;
            Raylib.DrawCircleV(new Vector2(wx, wy), 2.5f, Raylib.ColorAlpha(seaColor, 0.7f));
        }
    }

    private void DrawArchipelagos()
    {
        Color islandColor = new(120, 180, 100, 255);
        Color seaColor = new(70, 140, 190, 200);

        // Hoàng Sa archipelago
        Vector2 hoangSa = new(720f, 280f);
        Raylib.DrawCircleV(hoangSa, 6f, islandColor);
        Raylib.DrawCircleLines((int)hoangSa.X, (int)hoangSa.Y, 6f, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(hoangSa + new Vector2(10f, 6f), 4f, islandColor);
        FontManager.Instance.DrawText("Q.Đ Hoàng Sa", hoangSa + new Vector2(16f, -6f), 11f, seaColor);

        // Trường Sa archipelago
        Vector2 truongSa = new(710f, 480f);
        Raylib.DrawCircleV(truongSa, 6f, islandColor);
        Raylib.DrawCircleLines((int)truongSa.X, (int)truongSa.Y, 6f, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(truongSa + new Vector2(-8f, 10f), 4f, islandColor);
        Raylib.DrawCircleV(truongSa + new Vector2(12f, 14f), 5f, islandColor);
        FontManager.Instance.DrawText("Q.Đ Trường Sa", truongSa + new Vector2(18f, 0f), 11f, seaColor);
    }

    private void DrawRegionPins()
    {
        var nodes = _mapService.GetRegionNodes(_saveData.WorldProgression);

        foreach (var node in nodes)
        {
            if (!PinPositions.TryGetValue(node.RegionId, out Vector2 pos)) continue;

            bool isSelected = (SelectedRegionId == node.RegionId);
            bool isHovered = (_hoveredRegionId == node.RegionId);
            bool isUnlocked = node.Status != RegionStatus.Locked;
            bool isCompleted = node.Status == RegionStatus.Completed;

            float bob = (isSelected || isHovered) ? MathF.Sin(_totalTime * 4.5f) * 4f : 0f;
            Vector2 pinPos = new(pos.X, pos.Y + bob);

            // Ground shadow
            Raylib.DrawEllipse((int)pinPos.X + 2, (int)pinPos.Y + 28, 14f, 6f, Raylib.ColorAlpha(Color.Black, 0.22f));

            // Pin Outer circle
            float pinR = isSelected ? 24f : (isHovered ? 22f : 20f);
            Color pinColor = isCompleted ? new Color(255, 215, 60, 255) :
                             isUnlocked ? new Color(70, 190, 110, 255) :
                             new Color(135, 135, 140, 200);

            VectorRenderer.DrawCartoonCircle(pinPos, pinR, pinColor, HanoiPalette.CartoonOutline, isSelected ? 3.0f : 2.0f);

            // Pin badge icon
            if (isCompleted)
            {
                VectorRenderer.DrawCheckmark(pinPos, 14f, Color.White, 2.5f);
            }
            else if (isUnlocked)
            {
                // Ring progress
                float pct = node.ColorRestorationPercent / 100f;
                if (pct > 0f)
                {
                    Raylib.DrawRing(pinPos, pinR - 6f, pinR - 2f, -90f, -90f + 360f * pct, 24, Color.White);
                }
                VectorRenderer.DrawFivePointStar(pinPos, 8f, Color.White);
            }
            else
            {
                // Vector padlock icon
                DrawMiniPadlock(pinPos, 12f);
            }

            // Region label pill
            string displayName = node.DisplayName;
            Vector2 nameSz = FontManager.Instance.MeasureText(displayName, 12f);
            Rectangle nameBg = new(pinPos.X - nameSz.X / 2f - 8f, pinPos.Y + pinR + 4f, nameSz.X + 16f, 22f);

            Color pillFill = isSelected ? new Color(255, 240, 170, 250) :
                             (isUnlocked ? new Color(255, 252, 240, 230) : new Color(200, 200, 200, 200));
            Color pillBorder = isSelected ? HanoiPalette.WallYellow : HanoiPalette.CartoonOutline;

            VectorRenderer.DrawRoundedRect(nameBg, 0.4f, pillFill, pillBorder, isSelected ? 2.0f : 1.0f, withShadow: false);
            FontManager.Instance.DrawText(displayName, new Vector2(pinPos.X - nameSz.X / 2f, pinPos.Y + pinR + 6f), 12f,
                isUnlocked ? HanoiPalette.CartoonOutline : new Color(90, 90, 95, 220));
        }
    }

    private void DrawPreviewCard()
    {
        Rectangle card = new(840f, 120f, 400f, 520f);
        VectorRenderer.DrawRoundedRect(card, 0.08f, new Color(255, 253, 246, 252), HanoiPalette.CartoonOutline, 2.0f);

        var nodes = _mapService.GetRegionNodes(_saveData.WorldProgression);
        var activeNode = nodes.FirstOrDefault(n => n.RegionId == SelectedRegionId) ?? nodes[0];

        bool isUnlocked = activeNode.Status != RegionStatus.Locked;
        bool isCompleted = activeNode.Status == RegionStatus.Completed;

        // Card Header Banner
        Rectangle banner = new(card.X + 16f, card.Y + 16f, card.Width - 32f, 54f);
        Color banCol = isCompleted ? new Color(255, 235, 160, 255) :
                       isUnlocked ? new Color(210, 245, 215, 255) : new Color(225, 225, 225, 255);
        VectorRenderer.DrawRoundedRect(banner, 0.2f, banCol, HanoiPalette.CartoonOutline, 1.5f, withShadow: false);

        string regName = activeNode.DisplayName;
        FontManager.Instance.DrawTextOutline(regName, new Vector2(banner.X + 16f, banner.Y + 14f), 22f,
            isUnlocked ? HanoiPalette.CartoonOutline : new Color(110, 105, 100, 255), Color.White, 1.0f);

        // Status Badge Pill
        string statusText = isCompleted ? "HOÀN THÀNH" : (isUnlocked ? "ĐANG KHÁM PHÁ" : "CHƯA MỞ KHÓA");
        Color statBg = isCompleted ? new Color(255, 193, 7, 240) :
                       isUnlocked ? new Color(76, 175, 80, 240) : new Color(158, 158, 158, 240);
        Vector2 stSz = FontManager.Instance.MeasureText(statusText, 11f);
        Rectangle stPill = new(banner.X + banner.Width - stSz.X - 24f, banner.Y + 14f, stSz.X + 16f, 26f);
        VectorRenderer.DrawRoundedRect(stPill, 0.4f, statBg, Color.White, 0f, withShadow: false);
        FontManager.Instance.DrawText(statusText, new Vector2(stPill.X + 8f, stPill.Y + 6f), 11f, Color.White);

        // Lore Description
        float descY = card.Y + 85f;
        FontManager.Instance.DrawText("Giới thiệu vùng đất:", new Vector2(card.X + 20f, descY), 13f, new Color(120, 100, 85, 255));
        string lore = GetRegionLore(activeNode.RegionId);
        DrawWrappedText(lore, new Vector2(card.X + 20f, descY + 22f), 13f, card.Width - 40f, new Color(50, 42, 38, 230));

        // Color Restoration Progress Section
        float barY = card.Y + 280f;
        FontManager.Instance.DrawText("Mức độ hồi phục màu sắc:", new Vector2(card.X + 20f, barY), 13f, new Color(120, 100, 85, 255));

        Rectangle barBg = new(card.X + 20f, barY + 24f, card.Width - 40f, 26f);
        Raylib.DrawRectangleRounded(barBg, 0.4f, 4, new Color(220, 215, 205, 255));
        Raylib.DrawRectangleRoundedLinesEx(barBg, 0.4f, 4, 1.2f, HanoiPalette.CartoonOutline);

        float pct = activeNode.ColorRestorationPercent;
        if (pct > 0f)
        {
            Rectangle barFill = new(barBg.X + 2f, barBg.Y + 2f, (barBg.Width - 4f) * (pct / 100f), barBg.Height - 4f);
            Color fillGrad = isCompleted ? new Color(255, 215, 64, 255) : new Color(80, 200, 120, 255);
            Raylib.DrawRectangleRounded(barFill, 0.4f, 4, fillGrad);
        }

        string pctLabel = $"{pct:F1}%";
        Vector2 plSz = FontManager.Instance.MeasureText(pctLabel, 12f);
        FontManager.Instance.DrawText(pctLabel, new Vector2(barBg.X + (barBg.Width - plSz.X) * 0.5f, barBg.Y + 6f), 12f,
            HanoiPalette.CartoonOutline);

        // Climax / Unlock Note
        float noteY = card.Y + 350f;
        string note = activeNode.RegionId switch
        {
            "Hanoi" => "Khu phố cổ nghìn năm văn hiến. Hãy giúp Bà An và cư dân khôi phục màu sắc!",
            "Hue" => isUnlocked
                ? "Cố Đô Huế trầm mặc đã mở khóa! Tiếp tục hành trình cùng Bé Mây và Bút Chì."
                : "Hoàn thành 3 nhiệm vụ tại Hà Nội và mở Cầu Long Biên để mở khóa vùng đất này.",
            _ => "Vùng đất phương Nam đang bị bao phủ trong bóng xám. Hãy khám phá các vùng đất trước."
        };
        DrawWrappedText(note, new Vector2(card.X + 20f, noteY), 12f, card.Width - 40f, new Color(110, 95, 80, 220));

        // Travel Confirmation Action Button
        Rectangle travelBtn = new(card.X + 20f, card.Y + 440f, card.Width - 40f, 50f);
        if (isUnlocked)
        {
            VectorRenderer.DrawRoundedRect(travelBtn, 0.3f, new Color(76, 175, 80, 255), HanoiPalette.CartoonOutline, 2.0f);
            string btnLabel = "[Enter] Du Hành Đến Vùng Đất";
            Vector2 blSz = FontManager.Instance.MeasureText(btnLabel, 15f);
            FontManager.Instance.DrawText(btnLabel, new Vector2(travelBtn.X + (travelBtn.Width - blSz.X) * 0.5f, travelBtn.Y + 16f), 15f, Color.White);
        }
        else
        {
            VectorRenderer.DrawRoundedRect(travelBtn, 0.3f, new Color(190, 190, 190, 200), new Color(140, 140, 140, 255), 1.5f, withShadow: false);
            string btnLabel = "Vùng Đất Còn Bị Khóa";
            Vector2 blSz = FontManager.Instance.MeasureText(btnLabel, 14f);
            FontManager.Instance.DrawText(btnLabel, new Vector2(travelBtn.X + (travelBtn.Width - blSz.X) * 0.5f, travelBtn.Y + 17f), 14f, new Color(100, 100, 100, 255));
        }
    }

    private static string GetRegionLore(string regionId) => regionId switch
    {
        "Hanoi"  => "Hà Nội mùa thu thơm ngát hương hoa sữa và ấm nồng hương phở truyền thống. Những mái ngói rêu phong 36 phố phường và Cầu Long Biên trăm năm lịch sử.",
        "Hue"    => "Cố Đô Huế - Dòng sông Hương lững lờ trôi qua Ngọ Môn và những lăng tẩm cổ kính trầm mặc, nơi lưu giữ nhã nhạc cung đình và nét thanh lịch dịu dàng.",
        "HoiAn"  => "Phố Cổ Hội An - Thương cảng cổ kính rực rỡ sắc màu đèn lồng soi bóng dòng sông Hoài thơ mộng, với những ngôi nhà cổ tường vàng hoa giấy.",
        "SaiGon" => "Sài Gòn - Hòn ngọc viễn đông năng động và hoa lệ, nơi giao thoa nhịp sống hiện đại cùng những di tích lịch sử như Chợ Bến Thành và Nhà thờ Đức Bà.",
        _        => "Vùng đất huyền diệu của đất nước Việt Nam."
    };

    private static void DrawMiniPadlock(Vector2 center, float size)
    {
        Raylib.DrawCircleLines((int)center.X, (int)center.Y - 2, size * 0.35f, Color.White);
        Rectangle body = new(center.X - size * 0.4f, center.Y - 1f, size * 0.8f, size * 0.7f);
        Raylib.DrawRectangleRounded(body, 0.25f, 2, new Color(255, 215, 64, 255));
        Raylib.DrawRectangleRoundedLinesEx(body, 0.25f, 2, 1.0f, HanoiPalette.CartoonOutline);
    }

    private void DrawLegend()
    {
        Rectangle legendBox = new(30f, 500f, 200f, 120f);
        VectorRenderer.DrawRoundedRect(legendBox, 0.25f, new Color(255, 252, 240, 220),
            HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("Trạng thái:", new Vector2(46f, 512f), 12f, new Color(80, 60, 40, 255));

        // Completed
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 540f), 9f, new Color(255, 215, 60, 255), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
        FontManager.Instance.DrawText("Đã hoàn thành", new Vector2(74f, 533f), 11f, new Color(60, 50, 35, 255));

        // Unlocked
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 565f), 9f, new Color(70, 190, 110, 255), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
        FontManager.Instance.DrawText("Đang khám phá", new Vector2(74f, 558f), 11f, new Color(60, 50, 35, 255));

        // Locked
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 590f), 9f, new Color(135, 135, 140, 200), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
        FontManager.Instance.DrawText("Chưa mở khóa", new Vector2(74f, 583f), 11f, new Color(100, 90, 85, 200));

        // Album Button
        Rectangle albumBtn = new(30f, 630f, 200f, 44f);
        VectorRenderer.DrawRoundedRect(albumBtn, 0.35f, new Color(90, 150, 235, 255), HanoiPalette.CartoonOutline, 1.5f);
        FontManager.Instance.DrawText("[Tab] Mở Album Sổ", new Vector2(46f, 644f), 13f, Color.White);
    }

    private void DrawFeedback()
    {
        if (string.IsNullOrEmpty(_feedbackMsg) || _feedbackTimer <= 0f) return;
        float alpha = Math.Clamp(_feedbackTimer, 0f, 1f);
        Vector2 sz = FontManager.Instance.MeasureText(_feedbackMsg, 14f);
        float bw = Math.Min(sz.X + 36f, 800f);
        Rectangle box = new(440f - bw / 2f, 668f, bw, 36f);
        VectorRenderer.DrawRoundedRect(box, 0.35f,
            Raylib.ColorAlpha(new Color(40, 90, 160, 255), alpha * 0.92f),
            Color.White, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(_feedbackMsg, new Vector2(box.X + 18f, box.Y + 10f), 14f,
            Raylib.ColorAlpha(Color.White, alpha));
    }

    private void DrawBottomBar()
    {
        int pagesCount = _saveData.CollectionAlbum.CollectedJournalPages.Count;
        int stampsCount = _saveData.CollectionAlbum.CollectedStamps.Count;
        string info = $"Sổ Ký Ức: {pagesCount}/2 Trang  ·  Tem: {stampsCount}/4 Tem";
        Vector2 infoSz = FontManager.Instance.MeasureText(info, 12f);
        FontManager.Instance.DrawText(info, new Vector2(440f - infoSz.X / 2f, 84f), 12f,
            new Color(60, 50, 40, 200));
    }

    private static void DrawWrappedText(string text, Vector2 origin, float size, float maxWidth, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] words = text.Split(' ');
        string line = "";
        float y = origin.Y;
        float lineH = size + 5f;

        foreach (string word in words)
        {
            string testLine = string.IsNullOrEmpty(line) ? word : line + " " + word;
            Vector2 testSz = FontManager.Instance.MeasureText(testLine, size);
            if (testSz.X > maxWidth && !string.IsNullOrEmpty(line))
            {
                FontManager.Instance.DrawText(line, new Vector2(origin.X, y), size, color);
                y += lineH;
                line = word;
            }
            else
            {
                line = testLine;
            }
        }
        if (!string.IsNullOrEmpty(line))
        {
            FontManager.Instance.DrawText(line, new Vector2(origin.X, y), size, color);
        }
    }

    public void Exit() { }
    public void Dispose() { }
}
