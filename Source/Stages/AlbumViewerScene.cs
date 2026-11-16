using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Album Viewer Scene — 3-tab scrapbook showing Cuốn Sổ Ký Ức, Tem Du Lịch, and Thẻ Văn Hóa.
/// Supports keyboard (Q/E, 1-3, A/D, Left/Right, Tab/Esc), mouse, and headless input via IInputService.
/// Replaces all Unicode emojis with robust vector badges for zero missing-glyph glitches.
/// </summary>
public sealed class AlbumViewerScene : IScene
{
    private readonly SaveData _saveData;
    private readonly IEventBus? _eventBus;
    private readonly IInputService? _inputService;
    private readonly AlbumService _albumService = new();

    private int _activeTab;   // 0=Cuốn Sổ Ký Ức, 1=Tem Du Lịch, 2=Thẻ Văn Hóa
    private int _pageIndex;
    private float _totalTime;
    private float _transitionAlpha = 0f;

    public bool RequestClose { get; private set; }
    public int ActiveTab => _activeTab;
    public int PageIndex => _pageIndex;

    public static readonly string[] TabNames = ["Cuốn Sổ Ký Ức", "Tem Du Lịch", "Thẻ Văn Hóa"];

    public AlbumViewerScene(SaveData saveData, IEventBus? eventBus = null, IInputService? inputService = null)
    {
        _saveData = saveData ?? throw new ArgumentNullException(nameof(saveData));
        _eventBus = eventBus;
        _inputService = inputService;
    }

    public void Enter()
    {
        RequestClose = false;
        _transitionAlpha = 0f;
        _activeTab = 0;
        _pageIndex = 0;
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < TabNames.Length)
        {
            _activeTab = index;
            _pageIndex = 0;
        }
    }

    public void NextTab()
    {
        _activeTab = (_activeTab + 1) % TabNames.Length;
        _pageIndex = 0;
    }

    public void PrevTab()
    {
        _activeTab = (_activeTab - 1 + TabNames.Length) % TabNames.Length;
        _pageIndex = 0;
    }

    public void NextPage()
    {
        int count = GetCurrentTabCount();
        if (count > 0)
        {
            _pageIndex = Math.Clamp(_pageIndex + 1, 0, count - 1);
        }
    }

    public void PrevPage()
    {
        int count = GetCurrentTabCount();
        if (count > 0)
        {
            _pageIndex = Math.Clamp(_pageIndex - 1, 0, count - 1);
        }
    }

    public void Close()
    {
        RequestClose = true;
    }

    public void Update(float dt)
    {
        _totalTime += dt;
        _transitionAlpha = Math.Clamp(_transitionAlpha + dt * 3f, 0f, 1f);

        // Check close triggers
        if (_inputService?.IsCancelJustPressed == true ||
            _inputService?.IsAlbumJustPressed == true)
        {
            RequestClose = true;
            return;
        }

        if (Raylib.IsWindowReady())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Tab) || Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                RequestClose = true;
                return;
            }

            // Tab cycling via Q / E
            if (Raylib.IsKeyPressed(KeyboardKey.Q)) PrevTab();
            if (Raylib.IsKeyPressed(KeyboardKey.E)) NextTab();

            // Direct numeric tab jumps
            if (Raylib.IsKeyPressed(KeyboardKey.One))   SelectTab(0);
            if (Raylib.IsKeyPressed(KeyboardKey.Two))   SelectTab(1);
            if (Raylib.IsKeyPressed(KeyboardKey.Three)) SelectTab(2);

            // Item / Page navigation
            if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D)) NextPage();
            if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A))  PrevPage();

            // Mouse interaction on tab headers
            Vector2 mouse = Raylib.GetMousePosition();
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                for (int i = 0; i < 3; i++)
                {
                    Rectangle tabRect = GetTabRect(i);
                    if (Raylib.CheckCollisionPointRec(mouse, tabRect))
                    {
                        SelectTab(i);
                        break;
                    }
                }

                // Close button [X] at top-right
                Rectangle closeBtn = new(1150f, 22f, 40f, 36f);
                if (Raylib.CheckCollisionPointRec(mouse, closeBtn))
                {
                    RequestClose = true;
                }
            }
        }
    }

    public int GetCurrentTabCount() => _activeTab switch
    {
        0 => _albumService.GetAllJournalPages().Count,
        1 => _albumService.GetAllRegisteredStamps().Count,
        2 => _albumService.GetAllRegisteredFactCards().Count,
        _ => 0
    };

    private static Rectangle GetTabRect(int index)
    {
        float tabW = 230f;
        float tabX = 180f + index * (tabW + 12f);
        return new Rectangle(tabX, 80f, tabW, 44f);
    }

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        // Leather journal cover behind a parchment spread
        Color bg = new(255, 248, 230, 255);
        Raylib.ClearBackground(new Color(110, 68, 42, 255));
        Raylib.DrawRectangleGradientV(0, 0, 1280, 720, new Color(124, 78, 48, 255), new Color(86, 52, 32, 255));
        Raylib.DrawRectangleLinesEx(new Rectangle(14, 14, 1252, 692), 2f, new Color(206, 160, 96, 160));
        Rectangle paper = new(30f, 10f, 1220f, 700f);
        VectorRenderer.DrawRoundedRect(paper, 0.03f, bg, new Color(90, 56, 34, 255), 2f);
        for (int ly = 120; ly < 700; ly += 28)
        {
            Raylib.DrawLineEx(new Vector2(46f, ly), new Vector2(1234f, ly), 1f, new Color(214, 196, 160, 70));
        }
        // Binding rings on the left edge
        for (int ry = 70; ry < 680; ry += 60)
        {
            Raylib.DrawCircleV(new Vector2(30f, ry), 8f, new Color(196, 198, 204, 255));
            Raylib.DrawCircleLines(30, ry, 8f, HanoiPalette.CartoonOutline);
            Raylib.DrawCircleV(new Vector2(30f, ry), 3f, new Color(86, 52, 32, 255));
        }
        Raylib.DrawRectangle(0, 0, 1280, 720, Raylib.ColorAlpha(bg, 1f - _transitionAlpha));

        DrawHeader();
        DrawTabs();
        DrawContent();
        DrawNavigation();
        DrawCloseHint();
    }

    private void DrawHeader()
    {
        string title = "Cuốn Sổ Ký Ức Du Ký Bé Mây";
        Vector2 sz = FontManager.Instance.MeasureText(title, 28f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(640f - sz.X / 2f, 22f), 28f,
            HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2f);

        // Close button [X]
        Rectangle closeBtn = new(1150f, 22f, 40f, 36f);
        VectorRenderer.DrawRoundedRect(closeBtn, 0.35f, new Color(240, 100, 80, 240), HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        VectorRenderer.DrawCloseCross(new Vector2(closeBtn.X + closeBtn.Width * 0.5f, closeBtn.Y + closeBtn.Height * 0.5f), 14f, Color.White);

        // Decorative line
        Raylib.DrawLineEx(new Vector2(80f, 68f), new Vector2(1200f, 68f), 2f,
            Raylib.ColorAlpha(HanoiPalette.CartoonOutline, 0.3f));
    }

    private void DrawTabs()
    {
        for (int i = 0; i < 3; i++)
        {
            Rectangle tabRect = GetTabRect(i);
            bool active = _activeTab == i;
            Color tabBg = active ? new Color(255, 240, 180, 255) : new Color(230, 220, 195, 255);
            Color tabOutline = active ? HanoiPalette.WallYellow : HanoiPalette.CartoonOutline;
            float outlineW = active ? 2.5f : 1f;

            VectorRenderer.DrawRoundedRect(tabRect, 0.35f, tabBg, tabOutline, outlineW, withShadow: active);

            // Vector badge icon on tab
            float iconX = tabRect.X + 18f;
            float iconY = tabRect.Y + 22f;
            DrawTabVectorIcon(i, new Vector2(iconX, iconY), active);

            Vector2 tSz = FontManager.Instance.MeasureText(TabNames[i], 14f);
            FontManager.Instance.DrawText(TabNames[i],
                new Vector2(tabRect.X + 42f + (tabRect.Width - 42f - tSz.X) * 0.5f, tabRect.Y + 13f), 14f,
                active ? HanoiPalette.CartoonOutline : new Color(100, 85, 70, 200));

            // Shortcut hint
            FontManager.Instance.DrawText($"[{i + 1}]",
                new Vector2(tabRect.X + 8f, tabRect.Y + 4f), 9f, new Color(150, 130, 100, 180));
        }
    }

    private static void DrawTabVectorIcon(int tabIndex, Vector2 center, bool active)
    {
        Color primary = active ? HanoiPalette.TerracottaRoof : new Color(140, 110, 90, 255);
        switch (tabIndex)
        {
            case 0: // Book/Journal
                Raylib.DrawRectangle((int)center.X - 8, (int)center.Y - 9, 16, 18, primary);
                Raylib.DrawRectangleLines((int)center.X - 8, (int)center.Y - 9, 16, 18, HanoiPalette.CartoonOutline);
                Raylib.DrawLine((int)center.X, (int)center.Y - 9, (int)center.X, (int)center.Y + 9, Color.White);
                break;
            case 1: // Stamp/Tag
                Raylib.DrawCircleV(center, 9f, primary);
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, 9f, HanoiPalette.CartoonOutline);
                Raylib.DrawCircleV(center, 4f, Color.White);
                break;
            case 2: // Fact Card
                Rectangle r = new(center.X - 8f, center.Y - 10f, 16f, 20f);
                Raylib.DrawRectangleRounded(r, 0.2f, 2, primary);
                Raylib.DrawRectangleRoundedLinesEx(r, 0.2f, 2, 1.0f, HanoiPalette.CartoonOutline);
                break;
        }
    }

    private void DrawContent()
    {
        Rectangle contentBox = new(80f, 138f, 1120f, 500f);
        VectorRenderer.DrawRoundedRect(contentBox, 0.08f, new Color(255, 253, 244, 255),
            HanoiPalette.CartoonOutline, 1.5f);

        switch (_activeTab)
        {
            case 0: DrawJournalTab(contentBox); break;
            case 1: DrawStampsTab(contentBox); break;
            case 2: DrawFactsTab(contentBox); break;
        }
    }

    private void DrawJournalTab(Rectangle box)
    {
        var allPages = _albumService.GetAllJournalPages();
        if (_pageIndex >= allPages.Count) _pageIndex = 0;
        var page = allPages[_pageIndex];
        bool isUnlocked = _albumService.IsJournalPageUnlocked(_saveData.CollectionAlbum, page.Id);

        // Page illustration box
        Rectangle illRect = new(box.X + 40f, box.Y + 30f, 420f, 380f);
        VectorRenderer.DrawRoundedRect(illRect, 0.08f, new Color(240, 230, 200, 255),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);

        if (_pageIndex == 0)
        {
            // Page 1: Hanoi
            DrawHanoiIllustration(illRect, isUnlocked);
        }
        else
        {
            // Page 2: Hue
            DrawHueIllustration(illRect, isUnlocked);
        }

        // Text side
        float textX = illRect.X + illRect.Width + 40f;
        float textY = box.Y + 30f;

        // Page number badge
        Rectangle pageBadge = new(textX, textY, 130f, 28f);
        Color badgeCol = isUnlocked ? HanoiPalette.TerracottaRoof : new Color(130, 130, 130, 255);
        VectorRenderer.DrawRoundedRect(pageBadge, 0.5f, badgeCol, Color.White, 0f, withShadow: false);
        FontManager.Instance.DrawText(isUnlocked ? $"Trang {_pageIndex + 1}: MỞ KHÓA" : $"Trang {_pageIndex + 1}: CHƯA MỞ",
            new Vector2(textX + 10f, textY + 7f), 11f, Color.White);

        // Title
        string title = isUnlocked ? page.Title : (page.Id == "JournalPage_Hue_02" ? "Ký Ức Cố Đô Huế (Đang Khóa)" : "Trang Sổ Ký Ức Bí Ẩn");
        FontManager.Instance.DrawTextOutline(title, new Vector2(textX, textY + 40f), 20f,
            isUnlocked ? HanoiPalette.WallYellow : new Color(200, 200, 200, 255), HanoiPalette.CartoonOutline, 1.5f);

        // Region
        string regLabel = isUnlocked ? $"Địa danh: {page.Region}" : "Địa danh: ??? (Cần hoàn thành màn trước)";
        FontManager.Instance.DrawText(regLabel,
            new Vector2(textX, textY + 74f), 13f, new Color(80, 100, 60, 220));

        // Story text
        string story = isUnlocked
            ? page.StoryText
            : "Trang sổ ký ức này hiện vẫn đang bị Bóng Xám bao phủ. Hoàn thành toàn bộ nhiệm vụ tại Hà Nội và mở lối qua Cầu Long Biên để bước vào Cố Đô Huế và khai mở trang ký ức này!";
        DrawWrappedText(story, new Vector2(textX, textY + 106f), 14f, 540f,
            isUnlocked ? new Color(60, 50, 40, 230) : new Color(110, 100, 90, 200));

        // Postmark or Lock badge
        if (isUnlocked)
        {
            float stampAlpha = 0.7f + MathF.Sin(_totalTime * 2f) * 0.1f;
            VectorRenderer.DrawCartoonCircle(
                new Vector2(box.X + box.Width - 90f, box.Y + box.Height - 80f), 55f,
                Raylib.ColorAlpha(HanoiPalette.LotusPink, stampAlpha * 0.6f),
                Raylib.ColorAlpha(HanoiPalette.LotusRose, stampAlpha * 0.8f), 2f, withShadow: false);
            string postmark = (_pageIndex == 0) ? "HÀ NỘI" : "HUẾ";
            Vector2 pmSz = FontManager.Instance.MeasureText(postmark, 12f);
            FontManager.Instance.DrawText(postmark,
                new Vector2(box.X + box.Width - 90f - pmSz.X / 2f, box.Y + box.Height - 86f), 12f,
                Raylib.ColorAlpha(HanoiPalette.LotusRose, stampAlpha));
        }
        else
        {
            DrawVectorPadlock(new Vector2(box.X + box.Width - 90f, box.Y + box.Height - 80f), 26f);
        }
    }

    // Postcard illustrations: painted in full colour, then desaturated to grey while the page is locked.
    private static Color Paint(Color c, bool isUnlocked) => isUnlocked ? c : HanoiPalette.ApplyDesaturation(c, 8f);

    private static void DrawPostcardFrame(Rectangle rect, bool isUnlocked, string caption)
    {
        // Scalloped stamp-edge border and caption ribbon
        Raylib.DrawRectangleRoundedLinesEx(rect, 0.08f, 4, 6f, new Color(255, 252, 240, 255));
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(rect.X + 6f, rect.Y + 6f, rect.Width - 12f, rect.Height - 12f), 0.07f, 4, 1.2f, Raylib.ColorAlpha(HanoiPalette.CartoonOutline, 0.35f));
        Vector2 sz = FontManager.Instance.MeasureText(caption, 13f);
        Rectangle ribbon = new(rect.X + rect.Width * 0.5f - sz.X * 0.5f - 16f, rect.Y + rect.Height - 34f, sz.X + 32f, 24f);
        VectorRenderer.DrawRoundedRect(ribbon, 0.5f, Paint(new Color(200, 76, 50, 245), isUnlocked), HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(caption, new Vector2(ribbon.X + 16f, ribbon.Y + 5f), 13f, new Color(255, 248, 226, 255));
    }

    private static void DrawHanoiIllustration(Rectangle rect, bool isUnlocked)
    {
        int x = (int)rect.X, y = (int)rect.Y, w = (int)rect.Width, h = (int)rect.Height;
        float horizon = rect.Y + rect.Height * 0.58f;

        Raylib.BeginScissorMode(x, y, w, h);
        // Golden-hour sky and sun
        Raylib.DrawRectangleGradientV(x, y, w, (int)(horizon - y), Paint(new Color(120, 150, 200, 255), isUnlocked), Paint(new Color(255, 196, 140, 255), isUnlocked));
        Vector2 sun = new(rect.X + rect.Width * 0.72f, horizon - 70f);
        Raylib.DrawCircleV(sun, 46f, Raylib.ColorAlpha(Paint(new Color(255, 220, 150, 255), isUnlocked), 0.35f));
        Raylib.DrawCircleV(sun, 26f, Paint(new Color(255, 238, 190, 255), isUnlocked));

        // Distant city & banyan silhouettes on the far bank
        Color far = Paint(new Color(150, 110, 120, 255), isUnlocked);
        for (int i = 0; i < 9; i++)
        {
            int bw = 30 + (i * 37) % 26, bh = 18 + (i * 53) % 30;
            Raylib.DrawRectangle(x + i * 48, (int)horizon - bh, bw, bh, far);
        }
        Raylib.DrawCircleV(new Vector2(rect.X + 60f, horizon - 22f), 28f, Paint(new Color(90, 120, 80, 255), isUnlocked));

        // Red River
        Raylib.DrawRectangleGradientV(x, (int)horizon, w, (int)(rect.Y + rect.Height - horizon), Paint(new Color(214, 160, 120, 255), isUnlocked), Paint(new Color(120, 104, 110, 255), isUnlocked));
        Raylib.DrawRectangle((int)sun.X - 8, (int)horizon + 6, 16, 80, Raylib.ColorAlpha(Paint(new Color(255, 230, 180, 255), isUnlocked), 0.35f));

        // Long Biên bridge in perspective: deck + cantilever truss spans
        Color iron = Paint(HanoiPalette.RustedIron, isUnlocked);
        float deckY = horizon - 4f;
        Raylib.DrawLineEx(new Vector2(rect.X, deckY), new Vector2(rect.X + rect.Width, deckY - 26f), 5f, iron);
        for (int s = 0; s < 4; s++)
        {
            float u0 = s / 4f, u1 = (s + 1) / 4f;
            Vector2 a = new(rect.X + rect.Width * u0, deckY - 26f * u0);
            Vector2 b = new(rect.X + rect.Width * u1, deckY - 26f * u1);
            float height = 70f - s * 10f;
            Vector2 apex = (a + b) * 0.5f + new Vector2(0f, -height);
            Raylib.DrawLineEx(a, apex, 3.5f, iron);
            Raylib.DrawLineEx(apex, b, 3.5f, iron);
            for (int k = 1; k < 4; k++)
            {
                Vector2 onDeck = Vector2.Lerp(a, b, k / 4f);
                Vector2 onChord = k <= 2 ? Vector2.Lerp(a, apex, k / 2f) : Vector2.Lerp(apex, b, (k - 2) / 2f);
                Raylib.DrawLineEx(onDeck, onChord, 1.6f, iron);
            }
            // Pier into the water
            Raylib.DrawRectangle((int)a.X - 5, (int)a.Y, 10, 46, Paint(new Color(110, 92, 84, 255), isUnlocked));
        }

        // Foreground lotus pond with petals
        float fy = rect.Y + rect.Height - 70f;
        foreach (var (dx, r) in new[] { (60f, 26f), (170f, 20f), (300f, 24f), (380f, 16f) })
        {
            Raylib.DrawEllipse((int)(rect.X + dx), (int)fy, r, r * 0.38f, Paint(HanoiPalette.LotusLeafGreen, isUnlocked));
        }
        VectorRenderer.DrawCartoonCircle(new Vector2(rect.X + 70f, fy - 12f), 11f, Paint(HanoiPalette.LotusPink, isUnlocked), Paint(HanoiPalette.LotusRose, isUnlocked), 1.5f, withShadow: false);
        VectorRenderer.DrawCartoonCircle(new Vector2(rect.X + 302f, fy - 10f), 9f, Paint(HanoiPalette.LotusWhite, isUnlocked), Paint(HanoiPalette.LotusPink, isUnlocked), 1.5f, withShadow: false);

        // Bé Mây drifting over the river
        if (isUnlocked)
        {
            VectorRenderer.DrawBeMay(new Vector2(rect.X + rect.Width * 0.32f, horizon - 92f), FacingDirection.Right, 0f, false, 0.8f, withShadow: false);
        }
        Raylib.EndScissorMode();

        DrawPostcardFrame(rect, isUnlocked, "Cầu Long Biên · Hà Nội");
    }

    private static void DrawHueIllustration(Rectangle rect, bool isUnlocked)
    {
        int x = (int)rect.X, y = (int)rect.Y, w = (int)rect.Width, h = (int)rect.Height;
        float horizon = rect.Y + rect.Height * 0.62f;
        float cx = rect.X + rect.Width / 2f;

        Raylib.BeginScissorMode(x, y, w, h);
        // Violet dusk sky with Ngự Bình mountain
        Raylib.DrawRectangleGradientV(x, y, w, (int)(horizon - y), Paint(HuePalette.DuskSkyTop, isUnlocked), Paint(HuePalette.DuskSkyBottom, isUnlocked));
        Raylib.DrawTriangle(new Vector2(rect.X - 20f, horizon - 40f), new Vector2(rect.X + 110f, horizon - 130f), new Vector2(rect.X + 240f, horizon - 40f), Paint(new Color(120, 78, 112, 220), isUnlocked));
        Raylib.DrawTriangle(new Vector2(rect.X + 220f, horizon - 40f), new Vector2(rect.X + 360f, horizon - 120f), new Vector2(rect.X + rect.Width + 30f, horizon - 40f), Paint(new Color(100, 64, 96, 220), isUnlocked));

        // Ngọ Môn: brick rampart, three gates, pavilion and curved golden roof
        float baseY = horizon;
        Color brick = Paint(HuePalette.ImperialRampartBrick, isUnlocked);
        Raylib.DrawRectangle((int)cx - 150, (int)baseY - 70, 300, 70, brick);
        Raylib.DrawRectangleLines((int)cx - 150, (int)baseY - 70, 300, 70, Paint(HuePalette.ImperialRampartTrim, isUnlocked));
        foreach (var (gx, gw) in new[] { (cx - 80f, 34f), (cx, 42f), (cx + 80f, 34f) })
        {
            Raylib.DrawRectangleRounded(new Rectangle(gx - gw * 0.5f, baseY - 50f, gw, 50f), 0.5f, 6, new Color(44, 28, 24, 255));
        }
        Raylib.DrawRectangle((int)cx - 110, (int)baseY - 112, 220, 42, Paint(HuePalette.LacqueredPillarRed, isUnlocked));
        for (float px = cx - 100f; px <= cx + 95f; px += 28f)
        {
            Raylib.DrawRectangle((int)px, (int)baseY - 108, 6, 38, Paint(HuePalette.LacqueredPillarDark, isUnlocked));
        }
        Color gold = Paint(HuePalette.RoyalGold, isUnlocked);
        Raylib.DrawTriangle(new Vector2(cx, baseY - 150f), new Vector2(cx - 140f, baseY - 110f), new Vector2(cx + 140f, baseY - 110f), gold);
        Raylib.DrawLineEx(new Vector2(cx - 140f, baseY - 110f), new Vector2(cx - 156f, baseY - 122f), 4f, gold);
        Raylib.DrawLineEx(new Vector2(cx + 140f, baseY - 110f), new Vector2(cx + 156f, baseY - 122f), 4f, gold);
        Raylib.DrawCircleV(new Vector2(cx, baseY - 152f), 4f, Paint(HuePalette.RoyalGlazedGreen, isUnlocked));

        // Perfume River with lantern reflections
        Raylib.DrawRectangleGradientV(x, (int)horizon, w, (int)(rect.Y + rect.Height - horizon), Paint(HuePalette.PerfumeRiverTeal, isUnlocked), Paint(HuePalette.PerfumeRiverDeep, isUnlocked));
        for (int i = 0; i < 6; i++)
        {
            float lx = rect.X + 40f + i * 66f;
            float ly = horizon + 30f + (i % 2) * 34f;
            Raylib.DrawCircleV(new Vector2(lx, ly + 6f), 12f, Raylib.ColorAlpha(Paint(new Color(255, 210, 110, 255), isUnlocked), 0.25f));
            Raylib.DrawCircleV(new Vector2(lx, ly), 6f, Paint(i % 2 == 0 ? HuePalette.LanternPaperPink : HuePalette.LanternPaperYellow, isUnlocked));
            Raylib.DrawCircleV(new Vector2(lx, ly - 3f), 2f, Paint(HuePalette.LanternFlame, isUnlocked));
        }
        Raylib.EndScissorMode();

        DrawPostcardFrame(rect, isUnlocked, "Ngọ Môn · Cố Đô Huế");
        if (!isUnlocked)
        {
            DrawVectorPadlock(new Vector2(cx, baseY - 60f), 24f);
        }
    }

    private void DrawStampsTab(Rectangle box)
    {
        var stamps = _albumService.GetAllRegisteredStamps();
        float stampW = 210f;
        float stampH = 260f;
        float gapX = 35f;
        float startX = box.X + 60f;
        float startY = box.Y + 45f;

        for (int i = 0; i < stamps.Count; i++)
        {
            var stamp = stamps[i];
            bool isUnlocked = _albumService.IsStampUnlocked(_saveData.CollectionAlbum, stamp.Id);
            bool isSelected = (_pageIndex == i);

            float sx = startX + i * (stampW + gapX);
            float sy = startY;
            Rectangle stampRect = new(sx, sy, stampW, stampH);

            // Perforated border dots
            for (int d = 0; d < 20; d++)
            {
                float dx = sx + d * (stampW / 19f);
                Raylib.DrawCircleV(new Vector2(dx, sy), 3f, new Color(255, 253, 244, 255));
                Raylib.DrawCircleV(new Vector2(dx, sy + stampH), 3f, new Color(255, 253, 244, 255));
            }

            Color stampBg = isUnlocked ? new Color(255, 250, 235, 255) : new Color(230, 230, 230, 200);
            Color outline = isSelected ? HanoiPalette.WallYellow : (isUnlocked ? HanoiPalette.CartoonOutline : new Color(170, 165, 160, 255));
            VectorRenderer.DrawRoundedRect(stampRect, 0.08f, stampBg, outline, isSelected ? 3.0f : 1.5f, withShadow: isSelected);

            if (isUnlocked)
            {
                // Lotus circle illustration
                VectorRenderer.DrawCartoonCircle(new Vector2(sx + stampW / 2f, sy + 80f), 45f,
                    HanoiPalette.LotusPink, HanoiPalette.LotusRose, 2f, withShadow: false);
                VectorRenderer.DrawCartoonCircle(new Vector2(sx + stampW / 2f, sy + 80f), 18f,
                    HanoiPalette.WallYellow, HanoiPalette.TerracottaRoof, 1.5f, withShadow: false);

                // Stamp Name
                Vector2 nSz = FontManager.Instance.MeasureText(stamp.Name, 13f);
                FontManager.Instance.DrawText(stamp.Name, new Vector2(sx + (stampW - nSz.X) / 2f, sy + 145f), 13f, HanoiPalette.CartoonOutline);

                // Subtitle
                Vector2 sSz = FontManager.Instance.MeasureText(stamp.Subtitle, 10f);
                FontManager.Instance.DrawText(stamp.Subtitle, new Vector2(sx + (stampW - sSz.X) / 2f, sy + 168f), 10f, new Color(120, 100, 80, 220));

                // Postmark text
                Vector2 pSz = FontManager.Instance.MeasureText(stamp.PostmarkText, 9f);
                FontManager.Instance.DrawText(stamp.PostmarkText, new Vector2(sx + (stampW - pSz.X) / 2f, sy + 225f), 9f, new Color(180, 50, 50, 220));
            }
            else
            {
                // Locked stamp silhouette & padlock
                DrawVectorPadlock(new Vector2(sx + stampW / 2f, sy + 80f), 30f);

                string lockTitle = stamp.RegionId switch
                {
                    RegionIds.Hue => "Tem Cố Đô Huế",
                    RegionIds.HoiAn => "Tem Phố Cổ Hội An",
                    RegionIds.SaiGon => "Tem Sài Gòn",
                    _ => stamp.Name
                };
                Vector2 nSz = FontManager.Instance.MeasureText(lockTitle, 12f);
                FontManager.Instance.DrawText(lockTitle, new Vector2(sx + (stampW - nSz.X) / 2f, sy + 145f), 12f, new Color(130, 120, 115, 255));

                string lockSub = "Chưa mở khóa";
                Vector2 lSz = FontManager.Instance.MeasureText(lockSub, 10f);
                FontManager.Instance.DrawText(lockSub, new Vector2(sx + (stampW - lSz.X) / 2f, sy + 170f), 10f, new Color(160, 150, 140, 255));
            }
        }
    }

    private void DrawFactsTab(Rectangle box)
    {
        var facts = _albumService.GetAllRegisteredFactCards();
        if (_pageIndex >= facts.Count) _pageIndex = 0;
        var fact = facts[_pageIndex];
        bool isUnlocked = _albumService.IsFactCardUnlocked(_saveData.CollectionAlbum, fact.Id);

        // Card container
        Rectangle cardRect = new(box.X + 160f, box.Y + 50f, 800f, 390f);
        Color cardBg = isUnlocked ? Color.White : new Color(238, 235, 228, 255);
        VectorRenderer.DrawRoundedRect(cardRect, 0.08f, cardBg, HanoiPalette.CartoonOutline, 2f);

        if (isUnlocked)
        {
            // Category badge
            Rectangle catBadge = new(cardRect.X + 24f, cardRect.Y - 14f, 150f, 28f);
            VectorRenderer.DrawRoundedRect(catBadge, 0.5f, HanoiPalette.TerracottaRoof, Color.White, 0f, withShadow: false);
            FontManager.Instance.DrawText(fact.Category, new Vector2(catBadge.X + 14f, catBadge.Y + 7f), 12f, Color.White);

            // "ĐÃ KHÁM PHÁ" badge on top right
            Rectangle okBadge = new(cardRect.X + cardRect.Width - 160f, cardRect.Y + 16f, 140f, 26f);
            VectorRenderer.DrawRoundedRect(okBadge, 0.4f, new Color(76, 175, 80, 240), Color.White, 0f, withShadow: false);
            VectorRenderer.DrawCheckmark(new Vector2(okBadge.X + 18f, okBadge.Y + 13f), 10f, Color.White);
            FontManager.Instance.DrawText("ĐÃ KHÁM PHÁ", new Vector2(okBadge.X + 28f, okBadge.Y + 6f), 11f, Color.White);

            // Title
            FontManager.Instance.DrawTextOutline(fact.Title,
                new Vector2(cardRect.X + 24f, cardRect.Y + 36f), 22f,
                HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1.5f);

            // Divider
            Raylib.DrawLineEx(new Vector2(cardRect.X + 24f, cardRect.Y + 74f),
                new Vector2(cardRect.X + cardRect.Width - 24f, cardRect.Y + 74f), 2f,
                Raylib.ColorAlpha(HanoiPalette.CartoonOutline, 0.2f));

            // Content
            DrawWrappedText(fact.Content, new Vector2(cardRect.X + 24f, cardRect.Y + 95f), 16f,
                cardRect.Width - 48f, new Color(50, 40, 30, 230));

            // Decorative star watermark
            VectorRenderer.DrawCartoonCircle(new Vector2(cardRect.X + cardRect.Width - 70f, cardRect.Y + 310f), 32f,
                new Color(255, 240, 180, 200), HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
            VectorRenderer.DrawFivePointStar(new Vector2(cardRect.X + cardRect.Width - 70f, cardRect.Y + 310f), 14f, HanoiPalette.WallYellow, HanoiPalette.CartoonOutline);
        }
        else
        {
            // Locked mystery card back
            DrawVectorPadlock(new Vector2(cardRect.X + cardRect.Width / 2f, cardRect.Y + 130f), 36f);

            string lockTitle = "Thẻ Kiến Thức Chưa Khám Phá";
            Vector2 tSz = FontManager.Instance.MeasureText(lockTitle, 20f);
            FontManager.Instance.DrawText(lockTitle, new Vector2(cardRect.X + (cardRect.Width - tSz.X) / 2f, cardRect.Y + 190f), 20f,
                new Color(100, 90, 80, 240));

            string lockClue = "Hãy trò chuyện với các nhân vật và giải các câu đố trên phố cổ Hà Nội để khám phá những nét đẹp văn hóa này!";
            DrawWrappedText(lockClue, new Vector2(cardRect.X + 80f, cardRect.Y + 235f), 14f, cardRect.Width - 160f,
                new Color(130, 120, 110, 220));
        }
    }

    private static void DrawVectorPadlock(Vector2 center, float radius)
    {
        float w = radius * 1.3f;
        float h = radius * 1.1f;
        Rectangle body = new(center.X - w / 2f, center.Y - h / 4f, w, h);

        // Shackle
        Raylib.DrawCircleLines((int)center.X, (int)(center.Y - h / 4f), radius * 0.45f, HanoiPalette.CartoonOutline);
        // Body
        Raylib.DrawRectangleRounded(body, 0.25f, 4, new Color(255, 215, 64, 255));
        Raylib.DrawRectangleRoundedLinesEx(body, 0.25f, 4, 1.8f, HanoiPalette.CartoonOutline);
        // Keyhole
        Raylib.DrawCircle((int)center.X, (int)(center.Y + h / 6f), 3.5f, HanoiPalette.CartoonOutline);
        Raylib.DrawRectangle((int)center.X - 1, (int)(center.Y + h / 6f), 3, 6, HanoiPalette.CartoonOutline);
    }

    private void DrawNavigation()
    {
        int count = GetCurrentTabCount();
        if (count <= 1) return;

        string navText = $"{_pageIndex + 1} / {count}   [A/D] hoặc [Mũi tên Trái/Phải]";
        Vector2 nSz = FontManager.Instance.MeasureText(navText, 13f);
        FontManager.Instance.DrawText(navText, new Vector2(640f - nSz.X / 2f, 652f), 13f,
            new Color(100, 85, 70, 220));
    }

    private void DrawCloseHint()
    {
        string hint = "[Q/E] Chuyển Thẻ  ·  [Tab] hoặc [Esc] Đóng Album";
        Vector2 hSz = FontManager.Instance.MeasureText(hint, 12f);
        FontManager.Instance.DrawText(hint, new Vector2(640f - hSz.X / 2f, 690f), 12f,
            new Color(120, 100, 80, 180));
    }

    private static void DrawWrappedText(string text, Vector2 origin, float size, float maxWidth, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;
        string[] words = text.Split(' ');
        string line = "";
        float y = origin.Y;
        float lineH = size + 6f;

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
