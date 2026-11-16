using System;
using System.Numerics;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Static, deterministic world-space backdrop of the Hanoi Old Quarter at golden hour (1600x900):
/// tube-house facades, tiled sidewalks, asphalt street with markings, trees, utility poles and wires,
/// street lamps and the Long Bien Bridge approach. Only lanterns, lamp glow and puddle ripples animate.
/// Walkable area is Y 110..850; every object here is purely decorative (no collision).
/// </summary>
public static class HanoiStreetBackdrop
{
    private const int WorldW = 1600;
    private const int WorldH = 900;
    private const int WallH = 110;       // facade bottom / sidewalk top
    private const int CurbTop = 172;     // top sidewalk edge
    private const int RoadTop = 180;
    private const int RoadBottom = 792;
    private const int FacadeEnd = 1320;  // bridge sky begins here
    private const int BridgeDeckX = 1440;

    private readonly record struct Building(int X, int W, int RoofY, Color Wall, Color Roof, Color Shutter, Color Sign, string Shop);

    private static Color C(int r, int g, int b, int a = 255) => new(r, g, b, a);

    private static readonly Building[] Buildings =
    [
        new(0,    150, 14, C(229, 182, 76),  C(188, 84, 58),  C(46, 90, 68),   C(176, 48, 44),  "Cà Phê Phố"),
        new(150,  130, 26, C(236, 222, 186), C(150, 62, 44),  C(54, 98, 132),  C(48, 98, 74),   "Tạp Hóa"),
        new(280,  170, 8,  C(214, 150, 128), C(176, 76, 52),  C(46, 90, 68),   C(235, 196, 90), "Hiệu Vàng"),
        new(450,  140, 22, C(178, 205, 175), C(160, 70, 48),  C(120, 70, 50),  C(190, 70, 60),  "Bún Chả"),
        new(590,  160, 12, C(222, 176, 70),  C(196, 90, 60),  C(46, 90, 68),   C(52, 92, 120),  "Sách Cũ"),
        new(750,  130, 30, C(150, 178, 196), C(140, 60, 44),  C(236, 226, 200),C(220, 160, 60), "Nón Lá"),
        new(880,  180, 6,  C(238, 206, 150), C(184, 80, 54),  C(62, 110, 84),  C(168, 52, 70),  "Bánh Mì"),
        new(1060, 120, 24, C(205, 134, 110), C(168, 72, 50),  C(46, 90, 68),   C(60, 100, 90),  "Trà Đá"),
        new(1180, 140, 16, C(229, 190, 96),  C(190, 86, 58),  C(54, 98, 132),  C(200, 90, 50),  "May Đo"),
    ];

    private static readonly (float X, float Y, float Rx, float Ry)[] RainPuddles =
    [
        (340f, 640f, 38f, 15f), (780f, 560f, 44f, 17f), (1120f, 440f, 40f, 16f), (1360f, 700f, 35f, 14f)
    ];

    private static readonly (float X, float Y)[] LotusPetals =
    [
        (120f, 530f), (260f, 410f), (410f, 670f), (530f, 510f), (690f, 320f), (820f, 690f),
        (960f, 490f), (1080f, 620f), (1210f, 380f), (1330f, 540f), (1250f, 710f), (610f, 740f)
    ];

    private static readonly (float X, float Y)[] AlmondLeaves =
    [
        (150f, 380f), (290f, 620f), (470f, 360f), (610f, 590f), (760f, 410f), (890f, 630f),
        (1030f, 350f), (1170f, 570f), (1280f, 730f), (1400f, 360f), (80f, 720f)
    ];

    private static int Hash(int a, int b) => ((a * 73856093) ^ (b * 19349663)) & 0x7fffffff;

    public static void Draw(float t)
    {
        DrawSky();
        for (int i = 0; i < Buildings.Length; i++) DrawBuilding(Buildings[i], i);
        DrawBridgeTruss();
        DrawSidewalk();
        DrawRoad();
        DrawRoadDetails(t);
        DrawBridgeDeck();
        DrawUtilityPoles();
        DrawTree(330f);
        DrawTree(850f);
        DrawTree(1215f);
        DrawLanterns(t);
        DrawBottomEdge(t);
        DrawLighting();
    }

    // ---- Sky & facades ----------------------------------------------------------------------

    private static void DrawSky()
    {
        Raylib.DrawRectangleGradientV(0, 0, WorldW, WallH, C(104, 128, 176), C(255, 192, 132));
        Raylib.DrawRectangle(0, WallH, WorldW, WorldH - WallH, C(120, 114, 110));
    }

    private static void DrawBuilding(in Building b, int idx)
    {
        int x = b.X, w = b.W, top = b.RoofY;
        Color outline = HanoiPalette.CartoonOutline;

        Raylib.DrawRectangle(x, top, w, WallH - top, b.Wall);
        Raylib.DrawRectangle(x + w - 10, top, 10, WallH - top, Raylib.ColorAlpha(Color.Black, 0.12f));
        for (int k = 0; k < 4; k++)
        {
            int h = Hash(idx, k);
            Raylib.DrawRectangle(x + 10 + k * (w / 4) + h % 8, top + 18, 3, 24 + h % 22, Raylib.ColorAlpha(Color.Black, 0.07f));
        }

        // Tiled roof with overhang and ridge cap
        Raylib.DrawRectangle(x - 4, top, w + 8, 14, b.Roof);
        for (int rx = x; rx < x + w; rx += 10)
        {
            Raylib.DrawLineEx(new Vector2(rx, top + 1), new Vector2(rx, top + 13), 1.5f, Raylib.ColorAlpha(Color.White, 0.22f));
        }
        Raylib.DrawRectangle(x - 4, top, w + 8, 3, Raylib.ColorAlpha(Color.Black, 0.25f));
        Raylib.DrawRectangle(x - 4, top + 14, w + 8, 3, Raylib.ColorAlpha(Color.Black, 0.3f));

        // Arched shuttered windows with balcony rail
        int n = Math.Max(2, w / 52);
        float spacing = w / (float)n;
        for (int i = 0; i < n; i++)
        {
            int wx = (int)(x + spacing * (i + 0.5f) - 14f);
            bool lit = Hash(idx, i + 9) % 3 == 0;
            Raylib.DrawRectangle(wx, 50, 28, 26, b.Shutter);
            Raylib.DrawCircleV(new Vector2(wx + 14, 50), 14f, b.Shutter);
            Raylib.DrawRectangle(wx + 7, 48, 14, 28, lit ? C(255, 214, 120) : C(44, 54, 70));
            Raylib.DrawCircleV(new Vector2(wx + 14, 48), 7f, lit ? C(255, 214, 120) : C(44, 54, 70));
            Raylib.DrawLineEx(new Vector2(wx + 3, 56), new Vector2(wx + 6, 56), 1.5f, Raylib.ColorAlpha(Color.Black, 0.35f));
            Raylib.DrawLineEx(new Vector2(wx + 3, 64), new Vector2(wx + 6, 64), 1.5f, Raylib.ColorAlpha(Color.Black, 0.35f));
            Raylib.DrawLineEx(new Vector2(wx + 22, 56), new Vector2(wx + 25, 56), 1.5f, Raylib.ColorAlpha(Color.Black, 0.35f));
            Raylib.DrawLineEx(new Vector2(wx + 22, 64), new Vector2(wx + 25, 64), 1.5f, Raylib.ColorAlpha(Color.Black, 0.35f));
            Raylib.DrawRectangle(wx - 4, 76, 36, 4, Raylib.ColorAlpha(Color.Black, 0.28f));
            for (int px = wx - 2; px <= wx + 30; px += 6)
            {
                Raylib.DrawLineEx(new Vector2(px, 68), new Vector2(px, 76), 1f, HanoiPalette.RustedIron);
            }
            Raylib.DrawLineEx(new Vector2(wx - 3, 68), new Vector2(wx + 31, 68), 1.4f, HanoiPalette.RustedIron);
            if (Hash(idx, i + 3) % 2 == 0)
            {
                Raylib.DrawCircleV(new Vector2(wx + 6, 66), 4f, C(52, 120, 66));
                Raylib.DrawCircleV(new Vector2(wx + 8, 64), 2.6f, C(236, 70, 130));
            }
        }

        // Air-conditioner box on some houses
        if (idx % 3 == 1)
        {
            Raylib.DrawRectangle(x + w - 34, 38, 20, 12, C(214, 218, 222));
            Raylib.DrawCircleV(new Vector2(x + w - 24, 44), 4f, C(120, 126, 134));
        }

        // Shop sign (shrinks to fit) and striped awning
        Rectangle sign = new(x + 10, 81, w - 20, 14);
        VectorRenderer.DrawRoundedRect(sign, 0.25f, b.Sign, outline, 1.2f, withShadow: false);
        float fs = 10f;
        Vector2 sz = FontManager.Instance.MeasureText(b.Shop, fs);
        if (sz.X > sign.Width - 8f) { fs = 8f; sz = FontManager.Instance.MeasureText(b.Shop, fs); }
        FontManager.Instance.DrawText(b.Shop, new Vector2(sign.X + (sign.Width - sz.X) * 0.5f, sign.Y + (sign.Height - sz.Y) * 0.5f), fs, C(255, 248, 226));
        VectorRenderer.DrawPatternedAwning(new Rectangle(x + 8, 96, w - 16, 12), b.Shutter, C(246, 238, 216), 8);

        // Party wall and outline
        Raylib.DrawLineEx(new Vector2(x, top), new Vector2(x, WallH), 2f, outline);
        Raylib.DrawLineEx(new Vector2(x + w, top), new Vector2(x + w, WallH), 2f, outline);
    }

    // ---- Long Bien sky truss ---------------------------------------------------------------

    private static void DrawBridgeTruss()
    {
        Raylib.DrawCircleV(new Vector2(1530f, 52f), 40f, Raylib.ColorAlpha(C(255, 215, 130), 0.35f));
        Raylib.DrawCircleV(new Vector2(1530f, 52f), 24f, Raylib.ColorAlpha(C(255, 244, 180), 0.6f));

        Color iron = HanoiPalette.RustedIron;
        Raylib.DrawLineEx(new Vector2(FacadeEnd, WallH - 2), new Vector2(WorldW, WallH - 2), 5f, iron);
        Raylib.DrawLineEx(new Vector2(1380, WallH), new Vector2(1380, 22), 4.5f, iron);
        Raylib.DrawLineEx(new Vector2(1520, WallH), new Vector2(1520, 22), 4.5f, iron);
        Vector2[] chord = [new(1320, 96), new(1350, 62), new(1380, 22), new(1415, 52), new(1450, 76), new(1485, 52), new(1520, 22), new(1560, 60), new(1600, 96)];
        for (int i = 0; i < chord.Length - 1; i++)
        {
            Raylib.DrawLineEx(chord[i], chord[i + 1], 4f, iron);
            Raylib.DrawLineEx(chord[i], new Vector2(chord[i].X, WallH), 1.6f, HanoiPalette.RustedIronPatina);
            Raylib.DrawLineEx(chord[i], new Vector2(chord[i + 1].X, WallH), 1.6f, HanoiPalette.RustedIronPatina);
        }
        Raylib.DrawLineEx(chord[^1], new Vector2(chord[^1].X, WallH), 1.6f, HanoiPalette.RustedIronPatina);
    }

    // ---- Ground ------------------------------------------------------------------------------

    private static void DrawSidewalk()
    {
        Raylib.DrawRectangle(0, WallH, WorldW, CurbTop - WallH, C(208, 196, 176));
        int row = 0;
        for (int y = WallH; y < CurbTop; y += 24, row++)
        {
            int h = Math.Min(24, CurbTop - y);
            Raylib.DrawLineEx(new Vector2(0, y), new Vector2(WorldW, y), 1.4f, C(160, 148, 130));
            for (int x = (row % 2) * 24; x < WorldW; x += 48)
            {
                Raylib.DrawLineEx(new Vector2(x, y), new Vector2(x, y + h), 1.2f, C(160, 148, 130));
                if (Hash(x, y) % 5 == 0) Raylib.DrawRectangle(x + 2, y + 2, 44, h - 2, Raylib.ColorAlpha(Color.White, 0.18f));
            }
        }
        Raylib.DrawRectangleGradientV(0, WallH, WorldW, 26, Raylib.ColorAlpha(Color.Black, 0.32f), Raylib.ColorAlpha(Color.Black, 0f));

        // Black/white painted kerb
        for (int x = 0; x < WorldW; x += 24)
        {
            Raylib.DrawRectangle(x, CurbTop, 24, 8, (x / 24) % 2 == 0 ? C(240, 236, 226) : C(40, 40, 44));
        }
        Raylib.DrawLineEx(new Vector2(0, CurbTop), new Vector2(WorldW, CurbTop), 2f, HanoiPalette.CartoonOutline);
        Raylib.DrawLineEx(new Vector2(0, RoadTop), new Vector2(WorldW, RoadTop), 2f, HanoiPalette.CartoonOutline);
    }

    private static void DrawRoad()
    {
        Raylib.DrawRectangle(0, RoadTop, WorldW, RoadBottom - RoadTop, C(112, 108, 110));

        for (int i = 0; i < 16; i++)
        {
            int h = Hash(i, 7);
            float px = h % WorldW, py = RoadTop + 40 + Hash(i, 11) % (RoadBottom - RoadTop - 80);
            Raylib.DrawEllipse((int)px, (int)py, 90 + h % 80, 28 + h % 20, Raylib.ColorAlpha(i % 2 == 0 ? C(132, 126, 124) : C(92, 88, 92), 0.09f));
        }
        for (int i = 0; i < 380; i++)
        {
            int h = Hash(i, 3);
            int px = h % WorldW;
            int py = RoadTop + 4 + Hash(i, 5) % (RoadBottom - RoadTop - 8);
            Raylib.DrawRectangle(px, py, 2, 2, Raylib.ColorAlpha(h % 2 == 0 ? C(168, 162, 156) : C(66, 62, 66), 0.35f));
        }

        // Hairline cracks
        Vector2[][] cracks =
        [
            [new(200, 300), new(238, 316), new(262, 308), new(300, 334)],
            [new(1180, 560), new(1214, 580), new(1236, 572), new(1280, 600)],
            [new(620, 690), new(660, 676), new(690, 690), new(730, 672)],
        ];
        foreach (var c in cracks)
        {
            for (int i = 0; i < c.Length - 1; i++) Raylib.DrawLineEx(c[i], c[i + 1], 1.4f, Raylib.ColorAlpha(C(40, 38, 42), 0.6f));
        }

        // Lane paint: solid edge lines, dashed centre, zebra crossing
        Raylib.DrawLineEx(new Vector2(0, RoadTop + 10), new Vector2(BridgeDeckX, RoadTop + 10), 3f, Raylib.ColorAlpha(C(246, 242, 230), 0.75f));
        Raylib.DrawLineEx(new Vector2(0, RoadBottom - 10), new Vector2(BridgeDeckX, RoadBottom - 10), 3f, Raylib.ColorAlpha(C(246, 242, 230), 0.75f));
        for (int x = 20; x < BridgeDeckX - 40; x += 110)
        {
            Raylib.DrawRectangle(x, 484, 60, 6, Raylib.ColorAlpha(C(246, 220, 120), 0.85f));
        }
        for (int y = RoadTop + 20; y < RoadBottom - 30; y += 28)
        {
            Raylib.DrawRectangle(1000, y, 90, 14, Raylib.ColorAlpha(C(246, 242, 230), 0.82f));
        }
    }

    private static void DrawRoadDetails(float t)
    {
        // Manholes
        (float X, float Y)[] manholes = [(700f, 640f), (250f, 720f), (1260f, 260f)];
        foreach (var m in manholes)
        {
            Raylib.DrawEllipse((int)m.X, (int)m.Y, 22f, 11f, C(52, 50, 54));
            Raylib.DrawEllipse((int)m.X, (int)m.Y, 17f, 8f, C(86, 82, 84));
            Raylib.DrawEllipseLines((int)m.X, (int)m.Y, 10f, 5f, C(52, 50, 54));
        }

        for (int i = 0; i < RainPuddles.Length; i++)
        {
            var p = RainPuddles[i];
            Raylib.DrawEllipse((int)p.X, (int)p.Y, p.Rx + 3f, p.Ry + 2f, C(70, 68, 74, 200));
            Raylib.DrawEllipse((int)p.X, (int)p.Y, p.Rx, p.Ry, C(150, 186, 214, 215));
            Raylib.DrawEllipse((int)(p.X - p.Rx * 0.2f), (int)(p.Y - p.Ry * 0.25f), p.Rx * 0.5f, p.Ry * 0.35f, C(222, 238, 250, 170));
            for (int r = 0; r < 2; r++)
            {
                float u = ((t * 0.8f + p.X * 0.01f + r * 1.25f) % 2.5f) / 2.5f;
                Raylib.DrawEllipseLines((int)p.X, (int)p.Y, p.Rx * u, p.Ry * u, Raylib.ColorAlpha(Color.White, 0.4f * (1f - u)));
            }
        }

        for (int i = 0; i < LotusPetals.Length; i++)
        {
            var p = LotusPetals[i];
            Raylib.DrawEllipse((int)p.X, (int)p.Y, 5f, 2.8f, HanoiPalette.LotusPink);
            Raylib.DrawCircleV(new Vector2(p.X - 1.5f, p.Y), 1.8f, HanoiPalette.LotusRose);
        }
        for (int i = 0; i < AlmondLeaves.Length; i++)
        {
            var p = AlmondLeaves[i];
            Raylib.DrawEllipse((int)p.X, (int)p.Y, 6.5f, 3.8f, C(180, 42, 32, 240));
            Raylib.DrawLineEx(new Vector2(p.X - 4f, p.Y), new Vector2(p.X + 4f, p.Y), 1.2f, C(220, 85, 65, 230));
        }
    }

    // ---- Bridge approach -----------------------------------------------------------------------

    private static void DrawBridgeDeck()
    {
        int w = WorldW - BridgeDeckX;
        Raylib.DrawRectangle(BridgeDeckX, RoadTop, w, RoadBottom - RoadTop, C(88, 80, 78));
        for (int x = BridgeDeckX + 8; x < WorldW; x += 16)
        {
            Raylib.DrawLineEx(new Vector2(x, RoadTop), new Vector2(x, RoadBottom), 1.2f, Raylib.ColorAlpha(Color.Black, 0.22f));
        }
        // Rail track through the middle (Long Bien carries a railway)
        Raylib.DrawLineEx(new Vector2(BridgeDeckX, 462), new Vector2(WorldW, 462), 4f, C(168, 150, 136));
        Raylib.DrawLineEx(new Vector2(BridgeDeckX, 498), new Vector2(WorldW, 498), 4f, C(168, 150, 136));
        for (int x = BridgeDeckX + 4; x < WorldW; x += 22)
        {
            Raylib.DrawRectangle(x, 456, 8, 48, Raylib.ColorAlpha(C(70, 48, 36), 0.85f));
        }
        Raylib.DrawLineEx(new Vector2(BridgeDeckX, 462), new Vector2(WorldW, 462), 2f, C(214, 200, 186));
        Raylib.DrawLineEx(new Vector2(BridgeDeckX, 498), new Vector2(WorldW, 498), 2f, C(214, 200, 186));

        // Steel side trusses
        foreach (int yEdge in new[] { RoadTop + 4, RoadBottom - 4 })
        {
            Raylib.DrawLineEx(new Vector2(BridgeDeckX, yEdge), new Vector2(WorldW, yEdge), 6f, HanoiPalette.RustedIron);
            for (int x = BridgeDeckX + 10; x < WorldW; x += 32)
            {
                int dir = yEdge < 400 ? 1 : -1;
                Raylib.DrawLineEx(new Vector2(x, yEdge), new Vector2(x, yEdge + 24 * dir), 3f, HanoiPalette.RustedIronPatina);
                Raylib.DrawLineEx(new Vector2(x, yEdge), new Vector2(x + 32, yEdge + 24 * dir), 2f, HanoiPalette.RustedIronPatina);
            }
        }
        Raylib.DrawLineEx(new Vector2(BridgeDeckX, RoadTop), new Vector2(BridgeDeckX, RoadBottom), 3f, HanoiPalette.RustedIron);

        // Destination plaque hung over the sidewalk
        Rectangle plaque = new(1432, 120, 156, 26);
        VectorRenderer.DrawRoundedRect(plaque, 0.25f, C(30, 92, 70), HanoiPalette.CartoonOutline, 2f);
        string label = "CẦU LONG BIÊN  ›";
        Vector2 sz = FontManager.Instance.MeasureText(label, 13f);
        FontManager.Instance.DrawText(label, new Vector2(plaque.X + (plaque.Width - sz.X) * 0.5f, plaque.Y + (plaque.Height - sz.Y) * 0.5f), 13f, C(255, 248, 226));
    }

    // ---- Street furniture ----------------------------------------------------------------------

    private static void DrawUtilityPoles()
    {
        float[] poleX = [540f, 1040f];
        Color wire = Raylib.ColorAlpha(HanoiPalette.CartoonOutline, 0.75f);
        float[] anchors = [-40f, poleX[0], poleX[1], WorldW + 40f];

        for (int w = 0; w < 3; w++)
        {
            float y0 = 14f + w * 7f;
            for (int a = 0; a < anchors.Length - 1; a++)
            {
                Vector2 prev = new(anchors[a], y0);
                for (int s = 1; s <= 16; s++)
                {
                    float u = s / 16f;
                    Vector2 p = new(anchors[a] + (anchors[a + 1] - anchors[a]) * u, y0 + 14f * 4f * u * (1f - u));
                    Raylib.DrawLineEx(prev, p, 1.3f, wire);
                    prev = p;
                }
            }
        }
        foreach (float px in poleX)
        {
            Raylib.DrawEllipse((int)px, CurbTop - 2, 14f, 5f, Raylib.ColorAlpha(Color.Black, 0.3f));
            Raylib.DrawRectangle((int)px - 3, 6, 6, CurbTop - 8, C(88, 70, 58));
            Raylib.DrawRectangle((int)px - 3, 6, 2, CurbTop - 8, C(124, 102, 84));
            Raylib.DrawRectangle((int)px - 22, 12, 44, 4, C(70, 56, 46));
            for (int k = -1; k <= 1; k++) Raylib.DrawCircleV(new Vector2(px + k * 16f, 12f), 2.4f, C(204, 218, 226));
        }
    }

    private static void DrawTree(float x)
    {
        Raylib.DrawEllipse((int)x, CurbTop - 4, 34f, 8f, Raylib.ColorAlpha(Color.Black, 0.28f));
        Raylib.DrawRectangle((int)x - 7, 90, 14, CurbTop - 94, C(100, 72, 52));
        Raylib.DrawRectangle((int)x - 7, 90, 4, CurbTop - 94, C(138, 104, 78));
        Raylib.DrawRectangle((int)x - 11, CurbTop - 14, 22, 12, C(190, 80, 56));
        Raylib.DrawRectangleLinesEx(new Rectangle(x - 11, CurbTop - 14, 22, 12), 1.5f, HanoiPalette.CartoonOutline);

        (float dx, float dy, float r, int tone)[] blobs =
        [
            (-32, 70, 28, 0), (30, 72, 27, 0), (0, 52, 34, 1), (-22, 36, 26, 2), (22, 38, 27, 2), (0, 18, 24, 1), (-8, 70, 30, 1),
        ];
        foreach (var b in blobs)
        {
            Color c = b.tone switch { 0 => C(40, 96, 56, 238), 1 => C(56, 122, 66, 238), _ => C(88, 150, 80, 238) };
            Raylib.DrawCircleV(new Vector2(x + b.dx, b.dy), b.r, c);
        }
        for (int i = 0; i < 9; i++)
        {
            int h = Hash((int)x, i);
            Raylib.DrawCircleV(new Vector2(x - 34 + h % 68, 28 + Hash(i, (int)x) % 52), 3f, Raylib.ColorAlpha(C(255, 236, 150), 0.55f));
        }
    }

    private static void DrawLanterns(float t)
    {
        int idx = 0;
        for (int i = 1; i < Buildings.Length; i++, idx++)
        {
            float lx = Buildings[i].X;
            float sway = MathF.Sin(t * 2.2f + idx * 0.8f) * 3.5f;
            Vector2 c = new(lx + sway, 66f);
            Raylib.DrawLineEx(new Vector2(lx, 30), new Vector2(c.X, c.Y - 14f), 1.2f, HanoiPalette.CartoonOutline);
            float pulse = 0.88f + 0.12f * MathF.Sin(t * 3.2f + idx);
            Raylib.DrawCircleV(c, 40f * pulse, Raylib.ColorAlpha(C(255, 175, 55), 0.10f));
            Raylib.DrawCircleV(c, 24f * pulse, Raylib.ColorAlpha(C(255, 195, 75), 0.20f));
            Raylib.DrawEllipse((int)c.X, (int)c.Y, 8f, 12f, C(220, 40, 40));
            Raylib.DrawEllipseLines((int)c.X, (int)c.Y, 8f, 12f, HanoiPalette.CartoonOutline);
            Raylib.DrawRectangle((int)c.X - 4, (int)c.Y - 14, 8, 3, C(230, 190, 70));
            Raylib.DrawRectangle((int)c.X - 4, (int)c.Y + 11, 8, 3, C(230, 190, 70));
            Raylib.DrawLineEx(new Vector2(c.X, c.Y + 14f), new Vector2(c.X + sway * 0.3f, c.Y + 22f), 1.5f, C(235, 195, 75));
        }
    }

    private static void DrawBottomEdge(float t)
    {
        // Lower sidewalk
        Raylib.DrawRectangle(0, RoadBottom, WorldW, 850 - RoadBottom, C(204, 192, 172));
        for (int x = 0; x < WorldW; x += 24)
        {
            Raylib.DrawRectangle(x, RoadBottom, 24, 8, (x / 24) % 2 == 0 ? C(240, 236, 226) : C(40, 40, 44));
        }
        for (int x = 0; x < WorldW; x += 48)
        {
            Raylib.DrawLineEx(new Vector2(x, RoadBottom + 8), new Vector2(x, 850), 1.2f, C(160, 148, 130));
        }
        Raylib.DrawLineEx(new Vector2(0, RoadBottom), new Vector2(WorldW, RoadBottom), 2f, HanoiPalette.CartoonOutline);

        // Hedge with flowers and wrought-iron railing
        Raylib.DrawRectangle(0, 850, WorldW, 50, C(40, 92, 54));
        for (int x = 0; x < WorldW; x += 22)
        {
            int h = Hash(x, 1);
            Raylib.DrawCircleV(new Vector2(x + 11, 856 + h % 5), 13f, h % 2 == 0 ? C(52, 112, 64) : C(66, 130, 74));
            if (h % 4 == 0) Raylib.DrawCircleV(new Vector2(x + 8, 852), 3.4f, C(240, 80, 140));
        }
        Raylib.DrawLineEx(new Vector2(0, 850), new Vector2(WorldW, 850), 3f, HanoiPalette.CartoonOutline);

        // Cast-iron street lamps with warm glow
        for (int i = 0; i < 4; i++)
        {
            float lx = 150f + i * 370f;
            float pulse = 0.9f + 0.1f * MathF.Sin(t * 2.6f + i * 1.7f);
            Raylib.DrawEllipse((int)lx, 826, 70f * pulse, 18f, Raylib.ColorAlpha(C(255, 200, 110), 0.16f));
            Raylib.DrawRectangle((int)lx - 3, 772, 6, 78, HanoiPalette.RustedIron);
            Raylib.DrawRectangle((int)lx - 8, 842, 16, 8, HanoiPalette.RustedIron);
            Raylib.DrawCircleV(new Vector2(lx, 766f), 22f * pulse, Raylib.ColorAlpha(C(255, 200, 110), 0.22f));
            VectorRenderer.DrawRoundedRect(new Rectangle(lx - 8, 754, 16, 18), 0.3f, C(255, 226, 150), HanoiPalette.CartoonOutline, 1.6f, withShadow: false);
            Raylib.DrawTriangle(new Vector2(lx - 11, 755), new Vector2(lx + 11, 755), new Vector2(lx, 744), HanoiPalette.RustedIron);
        }
    }

    private static void DrawLighting()
    {
        Raylib.DrawRectangleGradientH(1000, WallH, 600, 740, Raylib.ColorAlpha(C(255, 170, 90), 0f), Raylib.ColorAlpha(C(255, 170, 90), 0.18f));
        Raylib.DrawRectangleGradientH(0, WallH, 300, 740, Raylib.ColorAlpha(C(40, 60, 100), 0.16f), Raylib.ColorAlpha(C(40, 60, 100), 0f));
        // Soft vignette
        Raylib.DrawRectangleGradientH(0, 0, 90, 900, Raylib.ColorAlpha(Color.Black, 0.3f), Raylib.ColorAlpha(Color.Black, 0f));
        Raylib.DrawRectangleGradientH(WorldW - 90, 0, 90, 900, Raylib.ColorAlpha(Color.Black, 0f), Raylib.ColorAlpha(Color.Black, 0.3f));
        Raylib.DrawRectangleGradientV(0, 820, WorldW, 80, Raylib.ColorAlpha(Color.Black, 0f), Raylib.ColorAlpha(Color.Black, 0.3f));
    }
}
