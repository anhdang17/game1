using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Core.Album;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Album Viewer Scene — 3-tab scrapbook showing Journal Pages, Travel Stamps, and Cultural Fact Cards.
/// Press [Tab] or [Esc] to close and return to World Map.
/// </summary>
public sealed class AlbumViewerScene : IScene
{
    private readonly SaveData _saveData;
    private readonly AlbumService _albumService = new();

    private int _activeTab;   // 0=Journal, 1=Stamps, 2=Facts
    private int _pageIndex;
    private float _totalTime;
    private float _transitionAlpha = 0f;

    public bool RequestClose { get; private set; }

    private static readonly string[] TabNames = ["📖 Trang Sổ", "🏷️ Tem Du Lịch", "🃏 Thẻ Văn Hóa"];

    public AlbumViewerScene(SaveData saveData, IEventBus eventBus)
    {
        _saveData = saveData;
    }

    public void Enter()
    {
        RequestClose = false;
        _transitionAlpha = 0f;
        _activeTab = 0;
        _pageIndex = 0;
    }

    public void Update(float dt)
    {
        _totalTime += dt;
        _transitionAlpha = Math.Clamp(_transitionAlpha + dt * 3f, 0f, 1f);

        if (Raylib.IsKeyPressed(KeyboardKey.Tab) || Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            RequestClose = true;
            return;
        }

        // Tab switching
        if (Raylib.IsKeyPressed(KeyboardKey.One))   { _activeTab = 0; _pageIndex = 0; }
        if (Raylib.IsKeyPressed(KeyboardKey.Two))   { _activeTab = 1; _pageIndex = 0; }
        if (Raylib.IsKeyPressed(KeyboardKey.Three)) { _activeTab = 2; _pageIndex = 0; }

        // Page navigation
        int itemCount = GetCurrentTabCount();
        if (Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressed(KeyboardKey.D))
            _pageIndex = Math.Clamp(_pageIndex + 1, 0, Math.Max(0, itemCount - 1));
        if (Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressed(KeyboardKey.A))
            _pageIndex = Math.Clamp(_pageIndex - 1, 0, Math.Max(0, itemCount - 1));

        // Mouse click on tabs
        for (int i = 0; i < 3; i++)
        {
            Rectangle tabRect = GetTabRect(i);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) &&
                Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), tabRect))
            {
                _activeTab = i;
                _pageIndex = 0;
            }
        }
    }

    private int GetCurrentTabCount() => _activeTab switch
    {
        0 => _albumService.GetUnlockedJournalPages(_saveData.CollectionAlbum).Count,
        1 => _albumService.GetUnlockedTravelStamps(_saveData.CollectionAlbum).Count,
        2 => _albumService.GetUnlockedFactCards(_saveData.CollectionAlbum).Count,
        _ => 0
    };

    private static Rectangle GetTabRect(int index)
    {
        float tabW = 220f;
        float tabX = 180f + index * (tabW + 10f);
        return new Rectangle(tabX, 80f, tabW, 44f);
    }

    public void Draw()
    {
        // Parchment background
        Color bg = new Color(255, 248, 230, 255);
        Raylib.ClearBackground(bg);
        Raylib.DrawRectangle(0, 0, 1280, 720, Raylib.ColorAlpha(bg, _transitionAlpha));

        DrawHeader();
        DrawTabs();
        DrawContent();
        DrawNavigation();
        DrawCloseHint();
    }

    private void DrawHeader()
    {
        string title = "Sổ Ký Ức Du Ký Bé Mây";
        Vector2 sz = FontManager.Instance.MeasureText(title, 30f);
        FontManager.Instance.DrawTextOutline(title, new Vector2(640f - sz.X / 2f, 22f), 30f,
            HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 2f);

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

            Vector2 tSz = FontManager.Instance.MeasureText(TabNames[i], 14f);
            FontManager.Instance.DrawText(TabNames[i],
                new Vector2(tabRect.X + tabRect.Width / 2f - tSz.X / 2f, tabRect.Y + 13f), 14f,
                active ? HanoiPalette.CartoonOutline : new Color(100, 85, 70, 200));

            // Shortcut hint
            FontManager.Instance.DrawText($"[{i + 1}]",
                new Vector2(tabRect.X + 8f, tabRect.Y + 4f), 9f, new Color(150, 130, 100, 180));
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
        var pages = _albumService.GetUnlockedJournalPages(_saveData.CollectionAlbum);

        if (pages.Count == 0)
        {
            DrawEmptyState(box, "Chưa có trang sổ nào.", "Hoàn thành các địa danh để thu thập trang sổ ký ức!");
            return;
        }

        if (_pageIndex >= pages.Count) _pageIndex = 0;
        var page = pages[_pageIndex];

        // Page illustration (procedural)
        Rectangle illRect = new(box.X + 40f, box.Y + 30f, 400f, 300f);
        VectorRenderer.DrawRoundedRect(illRect, 0.1f, new Color(240, 230, 200, 255),
            HanoiPalette.CartoonOutline, 1.5f, withShadow: false);

        // Draw Hanoi illustration
        DrawHanoiIllustration(illRect);

        // Text side
        float textX = illRect.X + illRect.Width + 40f;
        float textY = box.Y + 30f;

        // Page number badge
        Rectangle pageBadge = new(textX, textY, 80f, 28f);
        VectorRenderer.DrawRoundedRect(pageBadge, 0.5f, HanoiPalette.TerracottaRoof, Color.White, 0f, withShadow: false);
        FontManager.Instance.DrawText($"Trang {_pageIndex + 1}", new Vector2(textX + 6f, textY + 7f), 11f, Color.White);

        // Title
        FontManager.Instance.DrawTextOutline(page.Title, new Vector2(textX, textY + 40f), 22f,
            HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1.5f);

        // Region
        FontManager.Instance.DrawText($"📍 {page.Region}",
            new Vector2(textX, textY + 72f), 13f, new Color(80, 100, 60, 220));

        // Story text (word-wrapped)
        DrawWrappedText(page.StoryText, new Vector2(textX, textY + 100f), 14f, 580f,
            new Color(60, 50, 40, 230));

        // Decorative stamp in corner
        float stampAlpha = 0.6f + MathF.Sin(_totalTime * 2f) * 0.1f;
        VectorRenderer.DrawCartoonCircle(
            new Vector2(box.X + box.Width - 90f, box.Y + box.Height - 80f), 55f,
            Raylib.ColorAlpha(HanoiPalette.LotusPink, stampAlpha * 0.6f),
            Raylib.ColorAlpha(HanoiPalette.LotusRose, stampAlpha * 0.8f), 2f, withShadow: false);
        string postmark = "HÀ NỘI";
        Vector2 pmSz = FontManager.Instance.MeasureText(postmark, 11f);
        FontManager.Instance.DrawText(postmark,
            new Vector2(box.X + box.Width - 90f - pmSz.X / 2f, box.Y + box.Height - 86f), 11f,
            Raylib.ColorAlpha(HanoiPalette.LotusRose, stampAlpha));
    }

    private void DrawHanoiIllustration(Rectangle rect)
    {
        // Simple vector scene: Long Bien bridge + lotus
        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;

        // Sky
        Raylib.DrawRectangleRounded(rect, 0.1f, 4, new Color(200, 230, 255, 255));

        // Ground / River
        Rectangle riverRect = new(rect.X, rect.Y + rect.Height * 0.65f, rect.Width, rect.Height * 0.35f);
        Raylib.DrawRectangleRounded(riverRect, 0.1f, 4, new Color(120, 180, 220, 255));

        // Bridge
        Color bridgeCol = HanoiPalette.RustedIron;
        Raylib.DrawRectangle((int)(rect.X + 20f), (int)(rect.Y + rect.Height * 0.55f), (int)(rect.Width - 40f), 8, bridgeCol);
        for (int i = 0; i < 6; i++)
        {
            float tx = rect.X + 30f + i * (rect.Width - 60f) / 5f;
            Raylib.DrawRectangle((int)tx, (int)(rect.Y + rect.Height * 0.3f), 5, (int)(rect.Height * 0.26f), bridgeCol);
        }

        // Lotus flowers
        VectorRenderer.DrawCartoonCircle(new Vector2(cx - 60f, rect.Y + rect.Height * 0.8f), 18f,
            HanoiPalette.LotusPink, HanoiPalette.LotusRose, 1.5f, withShadow: false);
        VectorRenderer.DrawCartoonCircle(new Vector2(cx + 50f, rect.Y + rect.Height * 0.82f), 14f,
            HanoiPalette.LotusWhite, HanoiPalette.LotusPink, 1.5f, withShadow: false);
    }

    private void DrawStampsTab(Rectangle box)
    {
        var stamps = _albumService.GetUnlockedTravelStamps(_saveData.CollectionAlbum);

        if (stamps.Count == 0)
        {
            DrawEmptyState(box, "Chưa có tem nào.", "Hoàn thành các địa danh để nhận tem du lịch!");
            return;
        }

        // Draw all stamps in a grid
        float stampSize = 160f;
        float gapX = 40f, gapY = 40f;
        float startX = box.X + 60f, startY = box.Y + 50f;

        for (int i = 0; i < stamps.Count; i++)
        {
            var stamp = stamps[i];
            float sx = startX + (i % 5) * (stampSize + gapX);
            float sy = startY + (i / 5) * (stampSize + gapY);

            Rectangle stampRect = new(sx, sy, stampSize, stampSize);
            bool isSelected = _pageIndex == i;

            // Perforated stamp border (dashed)
            for (int d = 0; d < 24; d++)
            {
                float angle = d * (360f / 24f) * MathF.PI / 180f;
                float px = sx + stampSize / 2f + MathF.Cos(angle) * (stampSize * 0.52f);
                float py = sy + stampSize / 2f + MathF.Sin(angle) * (stampSize * 0.52f);
                Raylib.DrawCircleV(new Vector2(px, py), 5f, new Color(255, 253, 244, 255));
            }

            VectorRenderer.DrawRoundedRect(stampRect, 0.1f, new Color(255, 250, 230, 255),
                isSelected ? HanoiPalette.WallYellow : HanoiPalette.CartoonOutline,
                isSelected ? 3f : 1.5f, withShadow: isSelected);

            // Lotus stamp illustration
            VectorRenderer.DrawCartoonCircle(new Vector2(sx + stampSize / 2f, sy + stampSize / 2f - 15f), 50f,
                HanoiPalette.LotusPink, HanoiPalette.LotusRose, 2f, withShadow: false);

            // Inner lotus
            VectorRenderer.DrawCartoonCircle(new Vector2(sx + stampSize / 2f, sy + stampSize / 2f - 15f), 22f,
                HanoiPalette.WallYellow, HanoiPalette.TerracottaRoof, 1.5f, withShadow: false);

            // Stamp name
            Vector2 nSz = FontManager.Instance.MeasureText(stamp.Name, 11f);
            FontManager.Instance.DrawText(stamp.Name,
                new Vector2(sx + stampSize / 2f - nSz.X / 2f, sy + stampSize - 28f), 11f,
                HanoiPalette.CartoonOutline);

            // Postmark
            FontManager.Instance.DrawText(stamp.PostmarkText ?? "",
                new Vector2(sx + 8f, sy + 6f), 9f, new Color(150, 50, 50, 180));
        }
    }

    private void DrawFactsTab(Rectangle box)
    {
        var facts = _albumService.GetUnlockedFactCards(_saveData.CollectionAlbum);

        if (facts.Count == 0)
        {
            DrawEmptyState(box, "Chưa mở thẻ kiến thức nào.", "Hoàn thành nhiệm vụ để khám phá sự thật thú vị!");
            return;
        }

        if (_pageIndex >= facts.Count) _pageIndex = 0;
        var fact = facts[_pageIndex];

        // Card
        Rectangle cardRect = new(box.X + 160f, box.Y + 60f, 800f, 380f);
        VectorRenderer.DrawRoundedRect(cardRect, 0.1f, new Color(255, 255, 255, 255),
            HanoiPalette.CartoonOutline, 2f);

        // Category badge
        Rectangle catBadge = new(cardRect.X + 20f, cardRect.Y - 14f, 160f, 28f);
        VectorRenderer.DrawRoundedRect(catBadge, 0.5f, HanoiPalette.TerracottaRoof, Color.White, 0f, withShadow: false);
        FontManager.Instance.DrawText(fact.Category, new Vector2(catBadge.X + 10f, catBadge.Y + 7f), 12f, Color.White);

        // Title
        FontManager.Instance.DrawTextOutline(fact.Title,
            new Vector2(cardRect.X + 24f, cardRect.Y + 30f), 22f,
            HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1.5f);

        // Divider
        Raylib.DrawLineEx(new Vector2(cardRect.X + 24f, cardRect.Y + 68f),
            new Vector2(cardRect.X + cardRect.Width - 24f, cardRect.Y + 68f), 2f,
            Raylib.ColorAlpha(HanoiPalette.CartoonOutline, 0.2f));

        // Content
        DrawWrappedText(fact.Content, new Vector2(cardRect.X + 24f, cardRect.Y + 85f), 16f,
            cardRect.Width - 48f, new Color(50, 40, 30, 230));

        // Fun emoji decoration
        string[] emojis = ["🏮", "🌸", "🏛️", "🌊", "🎵", "⛄"];
        string emoji = emojis[Math.Abs(fact.Id.GetHashCode()) % emojis.Length];
        FontManager.Instance.DrawText(emoji,
            new Vector2(cardRect.X + cardRect.Width - 80f, cardRect.Y + 300f), 36f,
            Raylib.ColorAlpha(Color.White, 0.5f));
    }

    private static void DrawEmptyState(Rectangle box, string line1, string line2)
    {
        Vector2 center = new(box.X + box.Width / 2f, box.Y + box.Height / 2f);
        Vector2 s1 = FontManager.Instance.MeasureText(line1, 20f);
        Vector2 s2 = FontManager.Instance.MeasureText(line2, 14f);
        FontManager.Instance.DrawText(line1, new Vector2(center.X - s1.X / 2f, center.Y - 30f), 20f,
            new Color(150, 130, 110, 200));
        FontManager.Instance.DrawText(line2, new Vector2(center.X - s2.X / 2f, center.Y + 10f), 14f,
            new Color(130, 110, 90, 180));
    }

    private void DrawNavigation()
    {
        int count = GetCurrentTabCount();
        if (count <= 1) return;

        string navText = $"{_pageIndex + 1} / {count}  ← [A]  [D] →";
        Vector2 nSz = FontManager.Instance.MeasureText(navText, 13f);
        FontManager.Instance.DrawText(navText, new Vector2(640f - nSz.X / 2f, 652f), 13f,
            new Color(100, 85, 70, 220));
    }

    private void DrawCloseHint()
    {
        string hint = "[Tab] hoặc [Esc] để đóng Album";
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
        float lineH = size + 4f;

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
            FontManager.Instance.DrawText(line, new Vector2(origin.X, y), size, color);
    }

    public void Exit() { }
    public void Dispose() { }
}

