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
/// World Map Scene — displays the 7 World Wonders (and legacy Vietnamese regions)
/// on an authentic vintage parchment sea chart with navigational rhumb lines,
/// compass rose, sweeping golden flight arcs, and diorama wonder pins.
/// Preserves legacy keyboard navigation for test compatibility.
/// </summary>
public sealed class WorldMapScene : IScene
{
    private readonly SaveData _saveData;
    private readonly IEventBus? _eventBus;
    private readonly IInputService? _inputService;
    private readonly WorldMapService _mapService = new();

    private static readonly string[] RegionOrder = ["Hanoi", "Hue", "HoiAn", "SaiGon"];
    public static readonly string[] WonderOrder = ["Hanoi", "Kyoto", "Cairo", "Paris", "Rio", "BacCuc", "BauTroiKyUc"];

    private int _selectedIndex = 0;
    private string? _activeWonderId;
    private string? _hoveredRegionId;
    private string? _feedbackMsg;
    private float _feedbackTimer;
    private float _totalTime;

    /// Set to RegionId when player confirms travel to an unlocked region
    public string? RequestTravelTo { get; private set; }
    /// Set true when player requests to open Collection Album
    public bool RequestAlbum { get; private set; }

    public string SelectedRegionId => _activeWonderId ?? RegionOrder[_selectedIndex];
    public string? HoveredRegionId => _hoveredRegionId;
    public string? FeedbackMessage => _feedbackMsg;

    // Pin anchor coordinates across 1280x720 canvas
    private static readonly Dictionary<string, Vector2> PinPositions = new()
    {
        // 7 World Wonders
        { "Hanoi",       new Vector2(490f, 255f) },
        { "Kyoto",       new Vector2(670f, 215f) },
        { "Cairo",       new Vector2(340f, 320f) },
        { "Paris",       new Vector2(245f, 190f) },
        { "Rio",         new Vector2(185f, 480f) },
        { "BacCuc",      new Vector2(440f, 95f)  },
        { "BauTroiKyUc", new Vector2(645f, 440f) },

        // Legacy Vietnamese regions for backward compatibility
        { "Hue",         new Vector2(520f, 325f) },
        { "HoiAn",       new Vector2(545f, 365f) },
        { "SaiGon",      new Vector2(510f, 420f) },
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
        _activeWonderId = null;
        _feedbackMsg = "Chào mừng đến Bản Đồ Du Ký 7 Kỳ Quan! Dùng [W/S / Mũi tên] hoặc chuột để chọn địa danh.";
        _feedbackTimer = 5f;
    }

    public void SelectRegion(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return;
        int idx = Array.IndexOf(RegionOrder, regionId);
        if (idx >= 0)
        {
            _selectedIndex = idx;
            _activeWonderId = null;
        }
    }

    public void NavigateNext()
    {
        _activeWonderId = null;
        _selectedIndex = (_selectedIndex + 1) % RegionOrder.Length;
    }

    public void NavigatePrev()
    {
        _activeWonderId = null;
        _selectedIndex = (_selectedIndex - 1 + RegionOrder.Length) % RegionOrder.Length;
    }

    public bool ConfirmTravel()
    {
        string currentId = SelectedRegionId;
        bool canSelect = _mapService.TrySelectRegion(currentId, _saveData.WorldProgression, out string failReason);

        if (canSelect)
        {
            RequestTravelTo = currentId;
            _feedbackMsg = $"Đang du hành đến {GetDisplayName(currentId)}...";
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
                if (Array.IndexOf(RegionOrder, _hoveredRegionId) >= 0)
                {
                    SelectRegion(_hoveredRegionId);
                }
                else
                {
                    _activeWonderId = _hoveredRegionId;
                }

                if (_hoveredRegionId == SelectedRegionId)
                {
                    ConfirmTravel();
                }
            }

            // Click on travel button in preview card
            Rectangle travelBtn = new(840f + 20f, 120f + 440f, 360f, 50f);
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
            if (Vector2.Distance(mouse, pos) <= 32f)
                return id;
        }
        return null;
    }

    public static string GetDisplayName(string regionId) => regionId switch
    {
        "Hanoi"       => "Thủ Đô Hà Nội",
        "Hue"         => "Cố Đô Huế",
        "HoiAn"       => "Phố Cổ Hội An",
        "SaiGon"      => "Sài Gòn - Bến Thành",
        "Kyoto"       => "Kyoto (Nhật Bản)",
        "Cairo"       => "Cairo (Ai Cập)",
        "Paris"       => "Paris (Pháp)",
        "Rio"         => "Rio de Janeiro (Brazil)",
        "BacCuc"      => "Bắc Cực (The Arctic)",
        "BauTroiKyUc" => "Bầu Trời Ký Ức",
        _             => regionId
    };

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        DrawParchmentSeaChartBackground();
        DrawNavigationalRhumbLines();
        DrawCompassRose(new Vector2(115f, 235f), 45f);
        DrawContinentsAndLands();
        DrawArchipelagos();
        DrawFlightTrajectoryArcs();
        DrawRegionPins();
        DrawTitle();
        DrawPreviewCard();
        DrawLegend();
        DrawFeedback();
        DrawBottomBar();
    }

    private void DrawParchmentSeaChartBackground()
    {
        // 1. Vintage parchment paper gradient (#F5ECD7 to #EBDDBF)
        for (int y = 0; y < 720; y += 4)
        {
            float t = y / 720f;
            byte r = (byte)(245 + (233 - 245) * t);
            byte g = (byte)(236 + (221 - 236) * t);
            byte b = (byte)(215 + (191 - 215) * t);
            Raylib.DrawRectangle(0, y, 1280, 4, new Color((int)r, (int)g, (int)b, 255));
        }

        // 2. Subtle parchment texture lines
        for (int x = 20; x < 1260; x += 32)
        {
            Raylib.DrawLineEx(new Vector2(x, 0), new Vector2(x, 720), 1.0f, new Color(220, 205, 175, 20));
        }

        // 3. Aged vignette border
        Raylib.DrawRectangleLinesEx(new Rectangle(0, 0, 1280, 720), 12f, new Color(175, 145, 105, 75));
        Raylib.DrawRectangleLinesEx(new Rectangle(12, 12, 1256, 696), 2f, new Color(135, 105, 70, 110));
    }

    private void DrawNavigationalRhumbLines()
    {
        // Faint criss-crossing sea chart lines radiating from nautical nodes
        Vector2 hub1 = new(115f, 235f);
        Vector2 hub2 = new(660f, 320f);
        Color lineColor = new(185, 155, 120, 36);

        for (int angleDeg = 0; angleDeg < 360; angleDeg += 30)
        {
            float rad = angleDeg * MathF.PI / 180f;
            Vector2 end1 = hub1 + new Vector2(MathF.Cos(rad) * 900f, MathF.Sin(rad) * 900f);
            Raylib.DrawLineEx(hub1, end1, 1.0f, lineColor);

            Vector2 end2 = hub2 + new Vector2(MathF.Cos(rad) * 900f, MathF.Sin(rad) * 900f);
            Raylib.DrawLineEx(hub2, end2, 1.0f, lineColor);
        }
    }

    private void DrawCompassRose(Vector2 center, float radius)
    {
        // Outer rings
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius, new Color(160, 125, 75, 140));
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius * 0.75f, new Color(160, 125, 75, 100));

        // 8 points
        Color goldShade = new(212, 175, 55, 230);
        Color darkBronze = new(115, 80, 40, 230);

        // Primary 4 cardinal points (N, E, S, W)
        DrawCompassBlade(center, 0f, radius * 0.95f, radius * 0.22f, goldShade, darkBronze);
        DrawCompassBlade(center, 90f, radius * 0.95f, radius * 0.22f, goldShade, darkBronze);
        DrawCompassBlade(center, 180f, radius * 0.95f, radius * 0.22f, goldShade, darkBronze);
        DrawCompassBlade(center, 270f, radius * 0.95f, radius * 0.22f, goldShade, darkBronze);

        // Secondary 4 ordinal points (NE, SE, SW, NW)
        DrawCompassBlade(center, 45f, radius * 0.68f, radius * 0.16f, darkBronze, goldShade);
        DrawCompassBlade(center, 135f, radius * 0.68f, radius * 0.16f, darkBronze, goldShade);
        DrawCompassBlade(center, 225f, radius * 0.68f, radius * 0.16f, darkBronze, goldShade);
        DrawCompassBlade(center, 315f, radius * 0.68f, radius * 0.16f, darkBronze, goldShade);

        // Center hub
        Raylib.DrawCircleV(center, radius * 0.18f, new Color(240, 210, 110, 255));
        Raylib.DrawCircleLines((int)center.X, (int)center.Y, radius * 0.18f, darkBronze);

        // Cardinal Lettering
        FontManager.Instance.DrawText("N", center + new Vector2(-4f, -radius - 16f), 13f, new Color(130, 80, 35, 255));
        FontManager.Instance.DrawText("S", center + new Vector2(-4f, radius + 4f), 11f, new Color(140, 110, 75, 220));
        FontManager.Instance.DrawText("E", center + new Vector2(radius + 5f, -6f), 11f, new Color(140, 110, 75, 220));
        FontManager.Instance.DrawText("W", center + new Vector2(-radius - 15f, -6f), 11f, new Color(140, 110, 75, 220));
    }

    private static void DrawCompassBlade(Vector2 center, float angleDeg, float length, float halfWidth, Color c1, Color c2)
    {
        float rad = (angleDeg - 90f) * MathF.PI / 180f;
        float perpRad = rad + MathF.PI * 0.5f;

        Vector2 tip = center + new Vector2(MathF.Cos(rad) * length, MathF.Sin(rad) * length);
        Vector2 left = center + new Vector2(MathF.Cos(perpRad) * halfWidth, MathF.Sin(perpRad) * halfWidth);
        Vector2 right = center - new Vector2(MathF.Cos(perpRad) * halfWidth, MathF.Sin(perpRad) * halfWidth);

        Raylib.DrawTriangle(center, left, tip, c1);
        Raylib.DrawTriangle(center, tip, right, c2);
    }

    private void DrawContinentsAndLands()
    {
        Color landWash = new(218, 204, 178, 215);
        Color landBorder = new(155, 125, 90, 190);

        // Europe / Paris landmass
        VectorRenderer.DrawRoundedRect(new Rectangle(200f, 150f, 110f, 90f), 0.4f, landWash, landBorder, 1.2f, withShadow: false);
        // Egypt / Cairo landmass
        VectorRenderer.DrawRoundedRect(new Rectangle(295f, 275f, 100f, 105f), 0.35f, landWash, landBorder, 1.2f, withShadow: false);
        // South America / Rio landmass
        VectorRenderer.DrawRoundedRect(new Rectangle(145f, 430f, 95f, 120f), 0.45f, landWash, landBorder, 1.2f, withShadow: false);
        // Arctic ice shelf (North)
        VectorRenderer.DrawRoundedRect(new Rectangle(360f, 65f, 170f, 65f), 0.5f, new Color(230, 238, 245, 230), new Color(130, 160, 190, 180), 1.2f, withShadow: false);
        // Japan / Kyoto island arc
        VectorRenderer.DrawRoundedRect(new Rectangle(645f, 175f, 65f, 95f), 0.5f, landWash, landBorder, 1.2f, withShadow: false);
        // Mythical Sky Realm / Bầu Trời Ký Ức floating cloud land
        VectorRenderer.DrawRoundedRect(new Rectangle(595f, 395f, 125f, 85f), 0.55f, new Color(248, 242, 224, 230), new Color(212, 175, 55, 180), 1.5f, withShadow: false);

        // Vietnam silhouette
        float hanoiColor = _saveData.WorldProgression.GetColorRestoration("Hanoi");
        Color vnLand = HanoiPalette.ApplyDesaturation(new Color(175, 195, 145, 255), hanoiColor * 0.4f + 35f);
        Color vnBorder = new(100, 125, 80, 240);

        // Northern (Hanoi)
        VectorRenderer.DrawRoundedRect(new Rectangle(450f, 210f, 90f, 80f), 0.38f, vnLand, vnBorder, 1.3f, withShadow: false);
        // Central (Hue, HoiAn)
        VectorRenderer.DrawRoundedRect(new Rectangle(495f, 290f, 75f, 95f), 0.32f, vnLand, vnBorder, 1.3f, withShadow: false);
        // Southern (SaiGon)
        VectorRenderer.DrawRoundedRect(new Rectangle(475f, 385f, 85f, 75f), 0.4f, vnLand, vnBorder, 1.3f, withShadow: false);

        // Biển Đông & gentle ocean waves
        Color seaText = new(110, 140, 160, 210);
        FontManager.Instance.DrawText("Biển Đông", new Vector2(560f, 305f), 14f, seaText);
        for (int i = 0; i < 5; i++)
        {
            float wx = 550f + i * 22f;
            float wy = 330f + MathF.Sin(_totalTime * 1.5f + i * 0.8f) * 3f;
            Raylib.DrawCircleV(new Vector2(wx, wy), 2f, Raylib.ColorAlpha(seaText, 0.6f));
        }
    }

    private void DrawArchipelagos()
    {
        Color islandColor = new(140, 175, 120, 255);
        Color seaColor = new(115, 145, 165, 210);

        // Hoàng Sa
        Vector2 hoangSa = new(595f, 275f);
        Raylib.DrawCircleV(hoangSa, 5f, islandColor);
        Raylib.DrawCircleLines((int)hoangSa.X, (int)hoangSa.Y, 5f, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(hoangSa + new Vector2(8f, 5f), 3.5f, islandColor);
        FontManager.Instance.DrawText("Q.Đ Hoàng Sa", hoangSa + new Vector2(14f, -5f), 10f, seaColor);

        // Trường Sa
        Vector2 truongSa = new(590f, 375f);
        Raylib.DrawCircleV(truongSa, 5f, islandColor);
        Raylib.DrawCircleLines((int)truongSa.X, (int)truongSa.Y, 5f, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(truongSa + new Vector2(-6f, 8f), 3.5f, islandColor);
        Raylib.DrawCircleV(truongSa + new Vector2(9f, 10f), 4f, islandColor);
        FontManager.Instance.DrawText("Q.Đ Trường Sa", truongSa + new Vector2(15f, 0f), 10f, seaColor);
    }

    private void DrawFlightTrajectoryArcs()
    {
        if (!PinPositions.TryGetValue("Hanoi", out Vector2 hanoiPos)) return;

        string[] targets = ["Kyoto", "Cairo", "Paris", "Rio", "BacCuc", "BauTroiKyUc"];
        Color arcColor = new(218, 170, 50, 160);
        Color pulseColor = new(255, 235, 120, 255);

        foreach (string tgt in targets)
        {
            if (!PinPositions.TryGetValue(tgt, out Vector2 tgtPos)) continue;

            // Draw curved flight trajectory arc with bezier mid-point
            Vector2 mid = (hanoiPos + tgtPos) * 0.5f;
            float dist = Vector2.Distance(hanoiPos, tgtPos);
            Vector2 perp = new(-(tgtPos.Y - hanoiPos.Y), tgtPos.X - hanoiPos.X);
            if (perp.LengthSquared() > 0.001f) perp = Vector2.Normalize(perp);
            Vector2 ctrl = mid - perp * (dist * 0.22f);

            // Draw segmented curve
            int segments = 24;
            Vector2 prev = hanoiPos;
            for (int s = 1; s <= segments; s++)
            {
                float t = s / (float)segments;
                // Quadratic bezier formula
                Vector2 pt = (1f - t) * (1f - t) * hanoiPos + 2f * (1f - t) * t * ctrl + t * t * tgtPos;
                if (s % 2 == 0)
                {
                    Raylib.DrawLineEx(prev, pt, 1.8f, arcColor);
                }
                prev = pt;
            }

            // Traveling gold light spark along the arc
            float sparkT = (_totalTime * 0.28f + (tgt.GetHashCode() % 10) * 0.1f) % 1.0f;
            Vector2 sparkPos = (1f - sparkT) * (1f - sparkT) * hanoiPos + 2f * (1f - sparkT) * sparkT * ctrl + sparkT * sparkT * tgtPos;
            Raylib.DrawCircleV(sparkPos, 3.5f, pulseColor);
            Raylib.DrawCircleLines((int)sparkPos.X, (int)sparkPos.Y, 5f, Raylib.ColorAlpha(pulseColor, 0.45f));
        }
    }

    private void DrawRegionPins()
    {
        var wonderNodes = _mapService.GetWorldWonderNodes(_saveData.WorldProgression);
        var legacyNodes = _mapService.GetRegionNodes(_saveData.WorldProgression);

        // Combine unique nodes
        var allNodes = new List<RegionNode>(wonderNodes);
        foreach (var leg in legacyNodes)
        {
            if (!allNodes.Any(n => n.RegionId == leg.RegionId))
            {
                allNodes.Add(leg);
            }
        }

        foreach (var node in allNodes)
        {
            if (!PinPositions.TryGetValue(node.RegionId, out Vector2 pos)) continue;

            bool isSelected = string.Equals(SelectedRegionId, node.RegionId, StringComparison.OrdinalIgnoreCase);
            bool isHovered = string.Equals(_hoveredRegionId, node.RegionId, StringComparison.OrdinalIgnoreCase);
            bool isUnlocked = node.Status != RegionStatus.Locked;
            bool isCompleted = node.Status == RegionStatus.Completed;

            float bob = (isSelected || isHovered) ? MathF.Sin(_totalTime * 4.5f) * 4f : 0f;
            Vector2 pinPos = new(pos.X, pos.Y + bob);

            // Pin ground shadow
            Raylib.DrawEllipse((int)pinPos.X + 2, (int)pinPos.Y + 28, 14f, 6f, Raylib.ColorAlpha(Color.Black, 0.25f));

            // Selection aura / glow
            if (isSelected)
            {
                float auraPulse = 28f + MathF.Sin(_totalTime * 6f) * 4f;
                Raylib.DrawCircleV(pinPos, auraPulse, new Color(255, 215, 60, 60));
                Raylib.DrawCircleLines((int)pinPos.X, (int)pinPos.Y, auraPulse, new Color(218, 165, 32, 140));
            }

            // Pin Outer circle
            float pinR = isSelected ? 22f : (isHovered ? 20f : 18f);
            Color pinColor = isCompleted ? new Color(245, 195, 55, 255) :
                             isUnlocked  ? new Color(75, 175, 95, 255) :
                             new Color(135, 130, 125, 210);

            VectorRenderer.DrawCartoonCircle(pinPos, pinR, pinColor, HanoiPalette.CartoonOutline, isSelected ? 3.0f : 2.0f);

            // Distinct diorama wonder emblem or status
            if (isCompleted)
            {
                VectorRenderer.DrawCheckmark(pinPos, 13f, Color.White, 2.5f);
            }
            else if (isUnlocked)
            {
                float pct = node.ColorRestorationPercent / 100f;
                if (pct > 0f)
                {
                    Raylib.DrawRing(pinPos, pinR - 5f, pinR - 2f, -90f, -90f + 360f * pct, 24, Color.White);
                }
                DrawWonderMiniSymbol(node.RegionId, pinPos);
            }
            else
            {
                DrawMiniPadlock(pinPos, 12f);
            }

            // Region label pill
            string displayName = node.DisplayName;
            Vector2 nameSz = FontManager.Instance.MeasureText(displayName, 11f);
            Rectangle nameBg = new(pinPos.X - nameSz.X / 2f - 7f, pinPos.Y + pinR + 4f, nameSz.X + 14f, 20f);

            Color pillFill = isSelected ? new Color(255, 245, 185, 250) :
                             (isUnlocked ? new Color(252, 248, 235, 235) : new Color(210, 205, 195, 210));
            Color pillBorder = isSelected ? new Color(212, 165, 35, 255) : new Color(130, 105, 75, 220);

            VectorRenderer.DrawRoundedRect(nameBg, 0.4f, pillFill, pillBorder, isSelected ? 2.0f : 1.0f, withShadow: false);
            FontManager.Instance.DrawText(displayName, new Vector2(pinPos.X - nameSz.X / 2f, pinPos.Y + pinR + 5f), 11f,
                isUnlocked ? new Color(45, 35, 25, 255) : new Color(105, 100, 95, 220));
        }
    }

    private static void DrawWonderMiniSymbol(string regionId, Vector2 pos)
    {
        switch (regionId)
        {
            case "Kyoto":
                // Mini Torii crossbars
                Raylib.DrawLineEx(new Vector2(pos.X - 6f, pos.Y - 4f), new Vector2(pos.X + 6f, pos.Y - 4f), 2f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X - 5f, pos.Y - 1f), new Vector2(pos.X + 5f, pos.Y - 1f), 1.5f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X - 4f, pos.Y - 4f), new Vector2(pos.X - 4f, pos.Y + 5f), 1.5f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X + 4f, pos.Y - 4f), new Vector2(pos.X + 4f, pos.Y + 5f), 1.5f, Color.White);
                break;

            case "Cairo":
                // Mini Pyramid
                Vector2 top = new(pos.X, pos.Y - 5f);
                Vector2 left = new(pos.X - 6f, pos.Y + 5f);
                Vector2 right = new(pos.X + 6f, pos.Y + 5f);
                Raylib.DrawTriangle(top, left, right, Color.White);
                break;

            case "Paris":
                // Mini Eiffel Tower
                Raylib.DrawLineEx(new Vector2(pos.X, pos.Y - 7f), new Vector2(pos.X, pos.Y - 3f), 2f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X - 4f, pos.Y + 5f), new Vector2(pos.X, pos.Y - 3f), 1.5f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X + 4f, pos.Y + 5f), new Vector2(pos.X, pos.Y - 3f), 1.5f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X - 3f, pos.Y + 1f), new Vector2(pos.X + 3f, pos.Y + 1f), 1.5f, Color.White);
                break;

            case "Rio":
                // Mini Cross / Redeemer arms
                Raylib.DrawLineEx(new Vector2(pos.X, pos.Y - 6f), new Vector2(pos.X, pos.Y + 6f), 2f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X - 6f, pos.Y - 2f), new Vector2(pos.X + 6f, pos.Y - 2f), 2f, Color.White);
                break;

            case "BacCuc":
                // Mini Snowflake
                Raylib.DrawLineEx(new Vector2(pos.X, pos.Y - 6f), new Vector2(pos.X, pos.Y + 6f), 1.8f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X - 5f, pos.Y - 3f), new Vector2(pos.X + 5f, pos.Y + 3f), 1.8f, Color.White);
                Raylib.DrawLineEx(new Vector2(pos.X - 5f, pos.Y + 3f), new Vector2(pos.X + 5f, pos.Y - 3f), 1.8f, Color.White);
                break;

            case "BauTroiKyUc":
                // Celestial star
                VectorRenderer.DrawFivePointStar(pos, 8f, Color.White);
                break;

            default:
                // Hanoi / Lotus star
                VectorRenderer.DrawFivePointStar(pos, 8f, Color.White);
                break;
        }
    }

    private void DrawTitle()
    {
        string title = "Hải Đồ Du Ký 7 Kỳ Quan Thế Giới";
        Vector2 sz = FontManager.Instance.MeasureText(title, 28f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(440f - sz.X / 2f, 18f), 28f,
            new Color(255, 225, 75, 255), new Color(90, 60, 30, 255), 2.5f);

        string sub = "Sử dụng [W/S / Mũi tên] hoặc chuột để chọn kỳ quan  ·  [Enter] Du hành  ·  [Tab] Sổ Ký Ức";
        Vector2 subSz = FontManager.Instance.MeasureText(sub, 12f);
        FontManager.Instance.DrawText(sub, new Vector2(440f - subSz.X / 2f, 52f), 12f, new Color(75, 55, 35, 220));
    }

    private void DrawPreviewCard()
    {
        Rectangle card = new(840f, 120f, 400f, 520f);
        // Storybook folio card background with warm paper tone
        VectorRenderer.DrawRoundedRect(card, 0.08f, new Color(252, 248, 238, 252), new Color(145, 115, 80, 255), 2.0f);
        // Gold foil corner trim
        Raylib.DrawRectangleLinesEx(new Rectangle(card.X + 6f, card.Y + 6f, card.Width - 12f, card.Height - 12f), 1.0f, new Color(212, 175, 55, 140));

        var nodes = _mapService.GetWorldWonderNodes(_saveData.WorldProgression);
        var activeNode = nodes.FirstOrDefault(n => string.Equals(n.RegionId, SelectedRegionId, StringComparison.OrdinalIgnoreCase))
                      ?? _mapService.GetRegionNodes(_saveData.WorldProgression).FirstOrDefault(n => string.Equals(n.RegionId, SelectedRegionId, StringComparison.OrdinalIgnoreCase))
                      ?? nodes[0];

        bool isUnlocked = activeNode.Status != RegionStatus.Locked;
        bool isCompleted = activeNode.Status == RegionStatus.Completed;

        // Card Header Banner
        Rectangle banner = new(card.X + 16f, card.Y + 16f, card.Width - 32f, 54f);
        Color banCol = isCompleted ? new Color(255, 235, 160, 255) :
                       isUnlocked ? new Color(215, 245, 215, 255) : new Color(230, 228, 225, 255);
        VectorRenderer.DrawRoundedRect(banner, 0.2f, banCol, new Color(135, 105, 75, 255), 1.5f, withShadow: false);

        string regName = activeNode.DisplayName;
        FontManager.Instance.DrawTextOutline(regName, new Vector2(banner.X + 16f, banner.Y + 14f), 20f,
            isUnlocked ? new Color(50, 40, 30, 255) : new Color(110, 105, 100, 255), Color.White, 1.0f);

        // Status Badge Pill
        string statusText = isCompleted ? "HOÀN THÀNH" : (isUnlocked ? "ĐANG KHÁM PHÁ" : "CHƯA MỞ KHÓA");
        Color statBg = isCompleted ? new Color(245, 185, 30, 240) :
                       isUnlocked ? new Color(60, 165, 85, 240) : new Color(150, 145, 140, 240);
        Vector2 stSz = FontManager.Instance.MeasureText(statusText, 11f);
        Rectangle stPill = new(banner.X + banner.Width - stSz.X - 24f, banner.Y + 14f, stSz.X + 16f, 26f);
        VectorRenderer.DrawRoundedRect(stPill, 0.4f, statBg, Color.White, 0f, withShadow: false);
        FontManager.Instance.DrawText(statusText, new Vector2(stPill.X + 8f, stPill.Y + 6f), 11f, Color.White);

        // Lore Description
        float descY = card.Y + 85f;
        FontManager.Instance.DrawText("Giới thiệu kỳ quan:", new Vector2(card.X + 20f, descY), 13f, new Color(125, 95, 60, 255));
        string lore = GetRegionLore(activeNode.RegionId);
        DrawWrappedText(lore, new Vector2(card.X + 20f, descY + 22f), 13f, card.Width - 40f, new Color(45, 38, 30, 240));

        // Color Restoration Progress Section
        float barY = card.Y + 280f;
        FontManager.Instance.DrawText("Mức độ phục hồi màu sắc:", new Vector2(card.X + 20f, barY), 13f, new Color(125, 95, 60, 255));

        Rectangle barBg = new(card.X + 20f, barY + 24f, card.Width - 40f, 26f);
        Raylib.DrawRectangleRounded(barBg, 0.4f, 4, new Color(225, 218, 205, 255));
        Raylib.DrawRectangleRoundedLinesEx(barBg, 0.4f, 4, 1.2f, new Color(150, 120, 85, 200));

        float pct = activeNode.ColorRestorationPercent;
        if (pct > 0f)
        {
            Rectangle barFill = new(barBg.X + 2f, barBg.Y + 2f, (barBg.Width - 4f) * (pct / 100f), barBg.Height - 4f);
            Color fillGrad = isCompleted ? new Color(250, 205, 55, 255) : new Color(75, 185, 100, 255);
            Raylib.DrawRectangleRounded(barFill, 0.4f, 4, fillGrad);
        }

        string pctLabel = $"{pct:F1}%";
        Vector2 plSz = FontManager.Instance.MeasureText(pctLabel, 12f);
        FontManager.Instance.DrawText(pctLabel, new Vector2(barBg.X + (barBg.Width - plSz.X) * 0.5f, barBg.Y + 6f), 12f,
            new Color(40, 30, 20, 255));

        // Climax / Unlock Note
        float noteY = card.Y + 350f;
        string note = activeNode.RegionId switch
        {
            "Hanoi" => "Khu phố cổ nghìn năm văn hiến. Hãy giúp Bà An và cư dân khôi phục sắc màu đầu tiên!",
            "Hue" => isUnlocked
                ? "Cố Đô Huế trầm mặc đã mở khóa! Tiếp tục hành trình cùng Bé Mây và Bút Chì."
                : "Hoàn thành 3 nhiệm vụ tại Hà Nội và mở Cầu Long Biên để mở khóa vùng đất này.",
            "Kyoto" => isUnlocked
                ? "Cổng Torii son và phố hoa Kyoto đã rực rỡ chào đón Bé Mây!"
                : "Vùng đất phương Đông bị bao phủ trong bóng xám. Hãy hoàn thành các kỳ quan trước để mở khóa.",
            _ => "Vùng đất kỳ quan đang bị bao phủ trong bóng xám! Hãy hoàn thành các nhiệm vụ trước để mở khóa."
        };
        DrawWrappedText(note, new Vector2(card.X + 20f, noteY), 12f, card.Width - 40f, new Color(110, 90, 75, 230));

        // Travel Action Button
        Rectangle travelBtn = new(card.X + 20f, card.Y + 440f, card.Width - 40f, 50f);
        if (isUnlocked)
        {
            VectorRenderer.DrawRoundedRect(travelBtn, 0.3f, new Color(65, 160, 80, 255), new Color(40, 105, 50, 255), 2.0f);
            string btnLabel = "[Enter] Du Hành Đến Vùng Đất";
            Vector2 blSz = FontManager.Instance.MeasureText(btnLabel, 15f);
            FontManager.Instance.DrawText(btnLabel, new Vector2(travelBtn.X + (travelBtn.Width - blSz.X) * 0.5f, travelBtn.Y + 16f), 15f, Color.White);
        }
        else
        {
            VectorRenderer.DrawRoundedRect(travelBtn, 0.3f, new Color(195, 190, 185, 200), new Color(145, 140, 135, 255), 1.5f, withShadow: false);
            string btnLabel = "Kỳ Quan Còn Bị Khóa";
            Vector2 blSz = FontManager.Instance.MeasureText(btnLabel, 14f);
            FontManager.Instance.DrawText(btnLabel, new Vector2(travelBtn.X + (travelBtn.Width - blSz.X) * 0.5f, travelBtn.Y + 17f), 14f, new Color(105, 100, 95, 255));
        }
    }

    private static string GetRegionLore(string regionId) => regionId switch
    {
        "Hanoi"       => "Hà Nội mùa thu thơm ngát hương hoa sữa và ấm nồng hương phở truyền thống. Những mái ngói rêu phong 36 phố phường và Cầu Long Biên trăm năm lịch sử.",
        "Hue"         => "Cố Đô Huế - Dòng sông Hương lững lờ trôi qua Ngọ Môn và những lăng tẩm cổ kính trầm mặc, nơi lưu giữ nhã nhạc cung đình và nét thanh lịch dịu dàng.",
        "HoiAn"       => "Phố Cổ Hội An - Thương cảng cổ kính rực rỡ sắc màu đèn lồng soi bóng dòng sông Hoài thơ mộng, với những ngôi nhà cổ tường vàng hoa giấy.",
        "SaiGon"      => "Sài Gòn - Hòn ngọc viễn đông năng động và hoa lệ, nơi giao thoa nhịp sống hiện đại cùng những di tích lịch sử như Chợ Bến Thành và Nhà thờ Đức Bà.",
        "Kyoto"       => "Trang Mùa Xuân — Cổng Torii đỏ son, phố cổ Gion và rặng hoa anh đào bay trong gió đưa Bé Mây vào thế giới thi ca xứ Phù Tang.",
        "Cairo"       => "Trang Cát Vàng — Kim Tự Tháp nghìn năm, sông Nile huyền bí và tượng Nhân Sư trầm mặc giữa sa mạc mênh mông.",
        "Paris"       => "Trang Ánh Sáng — Tháp Eiffel lấp lánh, dòng sông Seine thơ mộng và những quán cà phê hè phố nồng nàn nghệ thuật.",
        "Rio"         => "Trang Âm Nhạc — Rừng mưa Amazon xanh thẳm, tượng Chúa Cứu Thế trên đỉnh mây và vũ điệu Samba bùng cháy nhiệt huyết.",
        "BacCuc"      => "Trang Băng Tuyết — Cực quang huyền ảo nhảy múa trên bầu trời đêm, soi rọi vương quốc băng tuyết vĩnh cửu tinh khôi.",
        "BauTroiKyUc" => "Trang Hồi Sinh — Cung điện mây bồng bềnh nơi mọi sắc màu bị đánh cắp tụ hội, hồi sinh ký ức của toàn thế giới.",
        _             => "Vùng đất huyền diệu trong hành trình du ký của Bé Mây và Bút Chì."
    };

    private static void DrawMiniPadlock(Vector2 center, float size)
    {
        Raylib.DrawCircleLines((int)center.X, (int)center.Y - 2, size * 0.35f, Color.White);
        Rectangle body = new(center.X - size * 0.4f, center.Y - 1f, size * 0.8f, size * 0.7f);
        Raylib.DrawRectangleRounded(body, 0.25f, 2, new Color(245, 205, 60, 255));
        Raylib.DrawRectangleRoundedLinesEx(body, 0.25f, 2, 1.0f, HanoiPalette.CartoonOutline);
    }

    private void DrawLegend()
    {
        Rectangle legendBox = new(30f, 500f, 200f, 120f);
        VectorRenderer.DrawRoundedRect(legendBox, 0.25f, new Color(252, 248, 235, 230),
            new Color(150, 120, 85, 220), 1.5f);
        FontManager.Instance.DrawText("Trạng thái kỳ quan:", new Vector2(46f, 512f), 12f, new Color(85, 65, 45, 255));

        // Completed
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 540f), 9f, new Color(245, 195, 55, 255), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
        FontManager.Instance.DrawText("Đã hoàn thành", new Vector2(74f, 533f), 11f, new Color(60, 50, 35, 255));

        // Unlocked
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 565f), 9f, new Color(75, 175, 95, 255), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
        FontManager.Instance.DrawText("Đang khám phá", new Vector2(74f, 558f), 11f, new Color(60, 50, 35, 255));

        // Locked
        VectorRenderer.DrawCartoonCircle(new Vector2(56f, 590f), 9f, new Color(135, 130, 125, 200), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
        FontManager.Instance.DrawText("Chưa mở khóa", new Vector2(74f, 583f), 11f, new Color(105, 95, 85, 210));

        // Album Button
        Rectangle albumBtn = new(30f, 630f, 200f, 44f);
        VectorRenderer.DrawRoundedRect(albumBtn, 0.35f, new Color(85, 135, 205, 255), new Color(55, 90, 140, 255), 1.5f);
        FontManager.Instance.DrawText("[Tab] Mở Sổ Ký Ức", new Vector2(46f, 644f), 13f, Color.White);
    }

    private void DrawFeedback()
    {
        if (string.IsNullOrEmpty(_feedbackMsg) || _feedbackTimer <= 0f) return;
        float alpha = Math.Clamp(_feedbackTimer, 0f, 1f);
        Vector2 sz = FontManager.Instance.MeasureText(_feedbackMsg, 14f);
        float bw = Math.Min(sz.X + 36f, 800f);
        Rectangle box = new(440f - bw / 2f, 668f, bw, 36f);
        VectorRenderer.DrawRoundedRect(box, 0.35f,
            Raylib.ColorAlpha(new Color(45, 85, 145, 255), alpha * 0.94f),
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
        FontManager.Instance.DrawText(info, new Vector2(440f - infoSz.X / 2f, 78f), 12f,
            new Color(75, 55, 40, 210));
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
