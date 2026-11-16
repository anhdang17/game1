using System;
using System.Numerics;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Static, deterministic world-space backdrop of Cố Đô Huế at dusk (1600x900):
/// layered Trường Sơn ridges, Kỳ Đài flag tower, the full Ngọ Môn citadel wall with Lầu Ngũ Phụng,
/// Thiên Mụ pagoda on its pine hill, a ceremonial flagstone courtyard, the Perfume River with
/// dragon boat, floating lanterns and lotus, and the Trường Tiền steel bridge.
/// Gameplay geometry (collision bands, dock, bridge channel, barrier X) is unchanged:
/// wall band Y&lt;320, river from Y 620, bridge deck Y 490..570 across X 1200..1600.
/// </summary>
public static class HueCitadelBackdrop
{
    private const int WorldW = 1600;

    private static Color C(int r, int g, int b, int a = 255) => new(r, g, b, a);
    private static int Hash(int a, int b) => ((a * 73856093) ^ (b * 19349663)) & 0x7fffffff;

    private static readonly (float X, float Y)[] LotusSpots =
    [
        (160f, 680f), (280f, 750f), (440f, 720f), (760f, 740f), (920f, 690f), (1080f, 760f), (1380f, 710f)
    ];

    private static readonly (float X, float Y)[] LanternSeeds =
    [
        (120f, 700f), (320f, 670f), (500f, 730f), (700f, 680f), (860f, 720f),
        (1020f, 670f), (1180f, 740f), (1340f, 680f), (1480f, 720f)
    ];

    public static void Draw(float t, bool gateUnlocked)
    {
        DrawSky(t);
        DrawMountainRidges();
        DrawThienMuHill();
        DrawCitadelWall(t);
        DrawCourtyard();
        DrawPerfumeRiver(t);
        DrawTrangTienBridge(t, gateUnlocked);
        DrawLighting();
    }

    // ---- Sky ---------------------------------------------------------------------------------

    private static void DrawSky(float t)
    {
        Raylib.DrawRectangleGradientV(0, 0, WorldW, 330, HuePalette.DuskSkyTop, HuePalette.DuskSkyBottom);

        // Low dusk sun over the river mouth, with haloes
        Vector2 sun = new(1150f, 150f);
        Raylib.DrawCircleV(sun, 60f, Raylib.ColorAlpha(C(255, 190, 120), 0.25f));
        Raylib.DrawCircleV(sun, 38f, Raylib.ColorAlpha(C(255, 214, 150), 0.5f));
        Raylib.DrawCircleV(sun, 24f, C(255, 236, 190));

        // Slow drifting dusk clouds
        for (int i = 0; i < 5; i++)
        {
            float cx = (i * 340f + t * 6f) % (WorldW + 200f) - 100f;
            float cy = 54f + (i % 3) * 38f;
            Color cc = Raylib.ColorAlpha(C(236, 190, 190), 0.35f);
            Raylib.DrawEllipse((int)cx, (int)cy, 58f, 12f, cc);
            Raylib.DrawEllipse((int)(cx + 34f), (int)(cy + 6f), 40f, 9f, cc);
        }

        // Birds flying home
        for (int i = 0; i < 3; i++)
        {
            float bx = 700f + i * 46f + MathF.Sin(t * 0.7f) * 20f;
            float by = 80f + i * 14f + MathF.Sin(t * 2f + i) * 4f;
            Color bc = C(60, 40, 60, 210);
            Raylib.DrawLineEx(new Vector2(bx - 7f, by), new Vector2(bx, by - 4f), 1.6f, bc);
            Raylib.DrawLineEx(new Vector2(bx, by - 4f), new Vector2(bx + 7f, by), 1.6f, bc);
        }
    }

    private static void DrawMountainRidges()
    {
        // Far ridge (Dãy Bạch Mã) — pale violet chain
        DrawRidge(190f, new[] { (0f, 300f), (210f, 210f), (420f, 285f), (640f, 195f), (900f, 290f), (1130f, 215f), (1380f, 285f), (1600f, 230f) }, C(146, 102, 138, 170));
        // Near ridge (Núi Ngự Bình) — deeper violet
        DrawRidge(240f, new[] { (0f, 318f), (260f, 248f), (520f, 312f), (760f, 238f), (1040f, 315f), (1300f, 258f), (1600f, 312f) }, C(110, 70, 105, 210));
    }

    private static void DrawRidge(float baseTopY, (float X, float Y)[] peaks, Color col)
    {
        for (int i = 0; i < peaks.Length - 1; i++)
        {
            Vector2 a = new(peaks[i].X, peaks[i].Y);
            Vector2 b = new(peaks[i + 1].X, peaks[i + 1].Y);
            // Fill down to the wall line at 330
            Raylib.DrawTriangle(a, new Vector2(a.X, 330f), new Vector2(b.X, 330f), col);
            Raylib.DrawTriangle(a, new Vector2(b.X, 330f), b, col);
        }
    }

    // ---- Thiên Mụ hill (west) ----------------------------------------------------------------

    private static void DrawThienMuHill()
    {
        // Pine hill as soft blob chain
        Color hill = HuePalette.HaKhePineGreen;
        foreach (var (x, y, r) in new[] { (90f, 310f, 56f), (200f, 285f, 72f), (320f, 305f, 62f), (410f, 320f, 44f) })
        {
            Raylib.DrawCircleV(new Vector2(x, y), r, hill);
        }
        Raylib.DrawRectangle(0, 300, 450, 36, hill);

        // Pines
        foreach (var (px, py) in new[] { (80f, 258f), (135f, 242f), (305f, 252f), (365f, 268f) })
        {
            Raylib.DrawRectangle((int)px - 3, (int)py, 6, 25, HuePalette.HaKhePineDark);
            Raylib.DrawTriangle(new Vector2(px - 16f, py + 12f), new Vector2(px, py - 18f), new Vector2(px + 16f, py + 12f), C(45, 75, 40));
            Raylib.DrawTriangle(new Vector2(px - 12f, py), new Vector2(px, py - 26f), new Vector2(px + 12f, py), C(55, 95, 50));
        }

        // Tháp Phước Duyên — 7-tier octagonal tower at x=220
        float bx = 220f, by = 300f;
        for (int tier = 0; tier < 7; tier++)
        {
            float tY = by - tier * 24f;
            float tW = 76f - tier * 7f;
            Rectangle rec = new(bx - tW * 0.5f, tY - 22f, tW, 22f);
            Raylib.DrawRectangleRec(rec, HuePalette.ThienMuBrick);
            Raylib.DrawRectangleLinesEx(rec, 1.5f, HuePalette.ThienMuTrim);
            // Flared golden eave with upturned tips
            float eW = tW + 16f;
            Raylib.DrawLineEx(new Vector2(bx - eW * 0.5f, tY - 22f), new Vector2(bx + eW * 0.5f, tY - 22f), 2.6f, HuePalette.RoyalGold);
            Raylib.DrawLineEx(new Vector2(bx - eW * 0.5f, tY - 22f), new Vector2(bx - eW * 0.5f - 4f, tY - 27f), 2.2f, HuePalette.RoyalGold);
            Raylib.DrawLineEx(new Vector2(bx + eW * 0.5f, tY - 22f), new Vector2(bx + eW * 0.5f + 4f, tY - 27f), 2.2f, HuePalette.RoyalGold);
            Rectangle win = new(bx - 5f, tY - 16f, 10f, 12f);
            Raylib.DrawRectangleRounded(win, 0.45f, 4, C(40, 25, 20));
        }
        Vector2 spireTop = new(bx, by - 7 * 24f - 18f);
        Raylib.DrawLineEx(new Vector2(bx, by - 7 * 24f), spireTop, 3f, HuePalette.SpireGold);
        Raylib.DrawCircleV(spireTop, 4.5f, HuePalette.SpireGold);

        // Stele pavilion & stone turtle
        Rectangle pav = new(320f, 275f, 44f, 40f);
        Raylib.DrawRectangleRec(pav, C(170, 75, 60));
        Raylib.DrawRectangleLinesEx(pav, 1.5f, HuePalette.ImperialCrimsonDark);
        Raylib.DrawTriangle(new Vector2(342f, 258f), new Vector2(308f, 275f), new Vector2(376f, 275f), HuePalette.RoyalGold);
        Raylib.DrawEllipse(342, 310, 14f, 7f, C(130, 125, 120));

        // Stone stair down toward the pier
        for (int s = 0; s < 4; s++)
        {
            Raylib.DrawRectangle(190 - s * 4, 320 + s * 8, 60 + s * 8, 6, HuePalette.PierStone);
            Raylib.DrawRectangleLines(190 - s * 4, 320 + s * 8, 60 + s * 8, 6, HuePalette.PierStoneDark);
        }
    }

    // ---- Citadel wall & Ngọ Môn --------------------------------------------------------------

    private static void DrawCitadelWall(float t)
    {
        // Full-width lower curtain wall (east of the hill) with brick courses & crenellations
        Rectangle wall = new(430f, 240f, WorldW - 430f, 92f);
        Raylib.DrawRectangleRec(wall, HuePalette.ImperialRampartBrick);
        for (int y = 252; y < 330; y += 14)
        {
            Raylib.DrawLineEx(new Vector2(430f, y), new Vector2(WorldW, y), 1f, Raylib.ColorAlpha(HuePalette.ImperialRampartTrim, 0.4f));
        }
        for (int x = 450; x < WorldW; x += 46)
        {
            Raylib.DrawLineEx(new Vector2(x, 240f + ((x / 46) % 2) * 7f), new Vector2(x, 330f), 1f, Raylib.ColorAlpha(HuePalette.ImperialRampartTrim, 0.3f));
        }
        Raylib.DrawRectangleLinesEx(wall, 2.5f, HuePalette.ImperialRampartTrim);
        for (float bx2 = 436f; bx2 < WorldW; bx2 += 32f)
        {
            Raylib.DrawRectangle((int)bx2, 232, 18, 10, HuePalette.ImperialRampartBrick);
            Raylib.DrawRectangleLines((int)bx2, 232, 18, 10, HuePalette.ImperialRampartTrim);
        }

        // Kỳ Đài flag tower (west of the gate), three tapering terraces + flag
        float fx = 500f;
        for (int i = 0; i < 3; i++)
        {
            float w = 110f - i * 28f;
            Rectangle terr = new(fx - w * 0.5f, 232f - (i + 1) * 18f, w, 18f);
            Raylib.DrawRectangleRec(terr, HuePalette.ImperialRampartBrick);
            Raylib.DrawRectangleLinesEx(terr, 1.6f, HuePalette.ImperialRampartTrim);
        }
        Raylib.DrawLineEx(new Vector2(fx, 178f), new Vector2(fx, 118f), 3f, C(70, 50, 45));
        float wave = MathF.Sin(t * 4f) * 3f;
        Raylib.DrawTriangle(new Vector2(fx, 118f), new Vector2(fx, 140f), new Vector2(fx + 34f + wave, 129f), C(205, 40, 40));
        VectorRenderer.DrawFivePointStar(new Vector2(fx + 14f, 129f), 5f, C(255, 220, 60));

        // --- Ngọ Môn centerpiece (rampart X 580..1120) ---
        Rectangle rampart = new(580f, 150f, 540f, 180f);
        Raylib.DrawRectangleRec(rampart, HuePalette.ImperialRampartBrick);
        for (int y = 162; y < 330; y += 14)
        {
            Raylib.DrawLineEx(new Vector2(580f, y), new Vector2(1120f, y), 1f, Raylib.ColorAlpha(HuePalette.ImperialRampartTrim, 0.45f));
        }
        Raylib.DrawRectangleLinesEx(rampart, 3f, HuePalette.ImperialRampartTrim);
        for (float bx = 585f; bx < 1115f; bx += 32f)
        {
            Raylib.DrawRectangle((int)bx, 142, 18, 10, HuePalette.ImperialRampartBrick);
            Raylib.DrawRectangleLines((int)bx, 142, 18, 10, HuePalette.ImperialRampartTrim);
        }

        // 3 royal arched gates (center gate taller), gold trim, door leaves
        foreach (var (gx, gw, gh) in new[] { (650f, 76f, 90f), (820f, 88f, 100f), (990f, 76f, 90f) })
        {
            Rectangle arch = new(gx - gw * 0.5f, 335f - gh, gw, gh);
            Raylib.DrawRectangleRounded(arch, 0.45f, 6, C(40, 25, 20));
            Raylib.DrawRectangleRoundedLinesEx(arch, 0.45f, 6, 2.5f, HuePalette.RoyalGold);
            // Lacquered double doors
            Raylib.DrawRectangle((int)(gx - gw * 0.5f + 8f), (int)(335f - gh * 0.55f), (int)(gw * 0.5f - 9f), (int)(gh * 0.55f) - 6, C(96, 28, 24));
            Raylib.DrawRectangle((int)(gx + 1f), (int)(335f - gh * 0.55f), (int)(gw * 0.5f - 9f), (int)(gh * 0.55f) - 6, C(96, 28, 24));
            Raylib.DrawCircleV(new Vector2(gx - 8f, 335f - gh * 0.3f), 2f, HuePalette.RoyalGold);
            Raylib.DrawCircleV(new Vector2(gx + 8f, 335f - gh * 0.3f), 2f, HuePalette.RoyalGold);
        }

        // Lầu Ngũ Phụng pavilion — two-tier curved roof with upturned eaves
        Rectangle pavilion = new(620f, 86f, 460f, 68f);
        Raylib.DrawRectangleRec(pavilion, HuePalette.LacqueredPillarRed);
        Raylib.DrawRectangleLinesEx(pavilion, 2.5f, HuePalette.ImperialCrimsonDark);
        for (float px = 640f; px <= 1060f; px += 52f)
        {
            Raylib.DrawRectangle((int)px, 94, 10, 60, HuePalette.LacqueredPillarRed);
            Raylib.DrawRectangleLines((int)px, 94, 10, 60, HuePalette.LacqueredPillarDark);
        }
        // Balcony
        Raylib.DrawRectangle(620, 148, 460, 10, HuePalette.LacqueredPillarDark);
        Raylib.DrawLineEx(new Vector2(620, 153), new Vector2(1080, 153), 2f, HuePalette.RoyalGold);

        // Lower roof skirt
        DrawCurvedRoof(new Vector2(850f, 70f), 510f, 24f, HuePalette.RoyalGold, HuePalette.RoyalGoldShadow);
        // Upper tier
        Rectangle upper = new(718f, 40f, 264f, 32f);
        Raylib.DrawRectangleRec(upper, HuePalette.LacqueredPillarRed);
        Raylib.DrawRectangleLinesEx(upper, 2f, HuePalette.ImperialCrimsonDark);
        DrawCurvedRoof(new Vector2(850f, 22f), 310f, 22f, HuePalette.RoyalGold, HuePalette.RoyalGoldShadow);
        // Green glazed ridge finials
        Raylib.DrawCircleV(new Vector2(850f, 18f), 5f, HuePalette.RoyalGlazedGreen);

        // Plaque
        Rectangle plaque = new(780f, 98f, 140f, 32f);
        Raylib.DrawRectangleRounded(plaque, 0.25f, 4, C(35, 30, 25, 245));
        Raylib.DrawRectangleRoundedLinesEx(plaque, 0.25f, 4, 1.5f, HuePalette.RoyalGold);
        Vector2 nSz = FontManager.Instance.MeasureText("NGỌ MÔN", 15f);
        FontManager.Instance.DrawText("NGỌ MÔN", new Vector2(plaque.X + (plaque.Width - nSz.X) * 0.5f, plaque.Y + 8f), 15f, C(255, 230, 90));

        // Red lanterns along the wall walk
        for (int i = 0; i < 6; i++)
        {
            float lx = 620f + i * 76f;
            float sway = MathF.Sin(t * 2.2f + i) * 2.5f;
            Vector2 c = new(lx + sway, 176f);
            Raylib.DrawCircleV(c, 16f, Raylib.ColorAlpha(C(255, 180, 70), 0.18f));
            Raylib.DrawEllipse((int)c.X, (int)c.Y, 6f, 9f, C(220, 40, 40));
            Raylib.DrawEllipseLines((int)c.X, (int)c.Y, 6f, 9f, HanoiPalette.CartoonOutline);
        }
    }

    private static void DrawCurvedRoof(Vector2 ridgeCenter, float width, float height, Color fill, Color shade)
    {
        float half = width * 0.5f;
        Vector2 l = ridgeCenter + new Vector2(-half, height);
        Vector2 r = ridgeCenter + new Vector2(half, height);
        Raylib.DrawTriangle(ridgeCenter + new Vector2(0f, -2f), l, r, fill);
        // Upturned eave tips
        Raylib.DrawLineEx(l, l + new Vector2(-14f, -8f), 4f, fill);
        Raylib.DrawLineEx(r, r + new Vector2(14f, -8f), 4f, fill);
        Raylib.DrawLineEx(l + new Vector2(-14f, -8f), r + new Vector2(14f, -8f), 0f, shade);
        Raylib.DrawLineEx(l, r, 2.5f, shade);
        // Ridge ornament line
        Raylib.DrawLineEx(ridgeCenter + new Vector2(-half * 0.4f, height * 0.4f), ridgeCenter + new Vector2(half * 0.4f, height * 0.4f), 1.5f, Raylib.ColorAlpha(shade, 0.6f));
    }

    // ---- Courtyard ---------------------------------------------------------------------------

    private static void DrawCourtyard()
    {
        Raylib.DrawRectangle(0, 320, WorldW, 300, HuePalette.FlagstoneFloor);

        // Varied flagstone slabs
        int row = 0;
        for (int y = 322; y < 616; y += 30, row++)
        {
            float xOff = (row % 2 == 1) ? 45f : 0f;
            for (float x = -90f + xOff; x < WorldW + 90f; x += 90f)
            {
                int h = Hash((int)x, y);
                Color slab = (h % 4) switch
                {
                    0 => C(183, 173, 158),
                    1 => C(170, 160, 146),
                    2 => C(176, 168, 152),
                    _ => HuePalette.FlagstoneFloor
                };
                Raylib.DrawRectangleRounded(new Rectangle(x + 2f, y + 2f, 86f, 26f), 0.15f, 2, slab);
            }
        }

        // Ceremonial central walkway (đường Thần đạo) from the middle gate down to the dock
        Rectangle walk = new(790f, 335f, 60f, 285f);
        Raylib.DrawRectangleRec(walk, C(198, 186, 162));
        Raylib.DrawRectangleLinesEx(walk, 2f, C(150, 138, 118));
        for (int y = 350; y < 612; y += 26)
        {
            Raylib.DrawLineEx(new Vector2(792f, y), new Vector2(848f, y), 1f, C(160, 148, 126, 160));
        }

        // Flanking stone lions at gate foot
        foreach (float lx in new[] { 770f, 870f })
        {
            Raylib.DrawEllipse((int)lx, 352, 12f, 5f, Raylib.ColorAlpha(Color.Black, 0.25f));
            Raylib.DrawRectangleRounded(new Rectangle(lx - 8f, 328f, 16f, 22f), 0.4f, 4, C(150, 144, 134));
            Raylib.DrawCircleV(new Vector2(lx, 324f), 8f, C(160, 154, 144));
            Raylib.DrawCircleV(new Vector2(lx - 3f, 322f), 1.5f, C(50, 45, 42));
            Raylib.DrawCircleV(new Vector2(lx + 3f, 322f), 1.5f, C(50, 45, 42));
        }

        // Bronze incense urn on the walkway
        Raylib.DrawEllipse(820, 470, 16f, 6f, Raylib.ColorAlpha(Color.Black, 0.25f));
        Raylib.DrawRectangleRounded(new Rectangle(806f, 440f, 28f, 28f), 0.35f, 4, C(96, 84, 58));
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(806f, 440f, 28f, 28f), 0.35f, 4, 1.6f, C(60, 52, 36));
        Raylib.DrawLineEx(new Vector2(810f, 440f), new Vector2(810f, 432f), 2f, C(60, 52, 36));
        Raylib.DrawLineEx(new Vector2(830f, 440f), new Vector2(830f, 432f), 2f, C(60, 52, 36));

        // Frangipani (hoa sứ) trees & bonsai pots scattered on the court
        foreach (var (tx, ty) in new[] { (120f, 420f), (1400f, 400f), (300f, 580f), (1250f, 600f) })
        {
            Raylib.DrawEllipse((int)tx, (int)ty, 20f, 7f, Raylib.ColorAlpha(Color.Black, 0.22f));
            Raylib.DrawLineEx(new Vector2(tx, ty), new Vector2(tx - 4f, ty - 26f), 5f, C(118, 96, 76));
            Raylib.DrawLineEx(new Vector2(tx - 4f, ty - 26f), new Vector2(tx + 8f, ty - 38f), 4f, C(118, 96, 76));
            foreach (var (dx, dy, r) in new[] { (-12f, -36f, 13f), (4f, -46f, 15f), (16f, -34f, 12f) })
            {
                Raylib.DrawCircleV(new Vector2(tx + dx, ty + dy), r, C(74, 128, 72));
            }
            Raylib.DrawCircleV(new Vector2(tx + 2f, ty - 44f), 3f, C(252, 244, 220));
            Raylib.DrawCircleV(new Vector2(tx - 9f, ty - 34f), 2.6f, C(252, 244, 220));
        }

        // Stone lanterns lining the courtyard edge
        foreach (float sx in new[] { 560f, 1080f })
        {
            Raylib.DrawEllipse((int)sx, 596, 10f, 4f, Raylib.ColorAlpha(Color.Black, 0.22f));
            Raylib.DrawRectangle((int)sx - 3, 568, 6, 26, C(150, 144, 134));
            Raylib.DrawRectangleRounded(new Rectangle(sx - 8f, 552f, 16f, 16f), 0.3f, 3, C(160, 154, 144));
            Raylib.DrawRectangle((int)sx - 4, 556, 8, 8, C(255, 226, 150));
            Raylib.DrawTriangle(new Vector2(sx - 11f, 552f), new Vector2(sx + 11f, 552f), new Vector2(sx, 544f), C(140, 134, 124));
        }
    }

    // ---- Perfume River -----------------------------------------------------------------------

    private static void DrawPerfumeRiver(float t)
    {
        Raylib.DrawRectangleGradientV(0, 620, WorldW, 280, HuePalette.PerfumeRiverTeal, HuePalette.PerfumeRiverDeep);

        // Dusk sun reflection path on the water
        Raylib.DrawRectangleGradientV(1080, 620, 160, 200, Raylib.ColorAlpha(C(255, 200, 130), 0.22f), Raylib.ColorAlpha(C(255, 200, 130), 0f));

        // Stone embankment
        Raylib.DrawRectangle(0, 620, WorldW, 12, HuePalette.PierStone);
        Raylib.DrawLineEx(new Vector2(0, 632), new Vector2(WorldW, 632), 1.5f, HuePalette.PierStoneDark);
        for (int x = 0; x < WorldW; x += 36)
        {
            Raylib.DrawLineEx(new Vector2(x, 620), new Vector2(x, 632), 1f, Raylib.ColorAlpha(HuePalette.PierStoneDark, 0.5f));
        }

        // Mooring dock (X 520..680 — gameplay anchor, unchanged)
        Rectangle dock = new(520f, 610f, 160f, 24f);
        Raylib.DrawRectangleRec(dock, HuePalette.PierStone);
        Raylib.DrawRectangleLinesEx(dock, 1.5f, HuePalette.PierStoneDark);
        for (int i = 0; i < 4; i++)
        {
            Raylib.DrawLineEx(new Vector2(530f + i * 40f, 610f), new Vector2(530f + i * 40f, 634f), 1f, HuePalette.PierStoneDark);
        }
        // Mooring posts
        Raylib.DrawRectangle(524, 600, 6, 14, C(96, 74, 52));
        Raylib.DrawRectangle(668, 600, 6, 14, C(96, 74, 52));

        // Animated wave shimmers
        for (int i = 0; i < 20; i++)
        {
            float wx = (i * 85f + t * 22f) % WorldW;
            float wy = 645f + (i % 6) * 38f + MathF.Sin(t * 2.2f + i) * 4f;
            Raylib.DrawLineEx(new Vector2(wx, wy), new Vector2(wx + 48f, wy), 2f, HuePalette.PerfumeRiverShimmer);
        }

        // Drifting river mist
        for (int i = 0; i < 4; i++)
        {
            float mx = (i * 420f + t * 10f) % (WorldW + 240f) - 120f;
            float my = 700f + (i % 2) * 70f;
            Raylib.DrawEllipse((int)mx, (int)my, 90f, 14f, Raylib.ColorAlpha(C(220, 230, 235), 0.10f));
        }

        // Dragon boat (Thuyền Rồng) at the dock
        Vector2 bp = new(600f, 642f);
        float bob = MathF.Sin(t * 1.8f) * 2f;
        bp.Y += bob;
        Raylib.DrawEllipse((int)bp.X, (int)(bp.Y + 22f), 70f, 9f, Raylib.ColorAlpha(Color.Black, 0.2f));
        Rectangle hull = new(bp.X - 60f, bp.Y, 120f, 24f);
        Raylib.DrawRectangleRounded(hull, 0.4f, 4, C(140, 85, 45));
        Raylib.DrawRectangleRoundedLinesEx(hull, 0.4f, 4, 1.5f, C(85, 45, 20));
        Raylib.DrawLineEx(new Vector2(bp.X - 54f, bp.Y + 8f), new Vector2(bp.X + 54f, bp.Y + 8f), 1.4f, C(96, 56, 28));
        // Dragon prow & stern
        Vector2 prow = new(bp.X - 65f, bp.Y - 6f);
        Raylib.DrawCircleV(prow, 12f, HuePalette.DragonBoatGold);
        Raylib.DrawCircleV(prow + new Vector2(-4f, -4f), 3f, Color.Black);
        Raylib.DrawTriangle(prow, prow + new Vector2(-16f, -10f), prow + new Vector2(-10f, 4f), HuePalette.DragonBoatGold);
        Raylib.DrawTriangle(prow + new Vector2(2f, -10f), prow + new Vector2(8f, -18f), prow + new Vector2(10f, -6f), C(255, 120, 60));
        Vector2 stern = new(bp.X + 65f, bp.Y - 4f);
        Raylib.DrawCircleV(stern, 9f, HuePalette.DragonBoatGold);
        Raylib.DrawTriangle(stern, stern + new Vector2(12f, -12f), stern + new Vector2(6f, 6f), HuePalette.DragonBoatGold);
        // Royal canopy
        Rectangle canopy = new(bp.X - 35f, bp.Y - 18f, 70f, 18f);
        Raylib.DrawRectangleRec(canopy, HuePalette.DragonBoatRed);
        Raylib.DrawRectangleLinesEx(canopy, 1.2f, HuePalette.RoyalGold);
        Raylib.DrawTriangle(new Vector2(bp.X, bp.Y - 28f), new Vector2(bp.X - 40f, bp.Y - 18f), new Vector2(bp.X + 40f, bp.Y - 18f), HuePalette.RoyalGold);

        // Floating flower lanterns with reflections
        for (int i = 0; i < LanternSeeds.Length; i++)
        {
            var seed = LanternSeeds[i];
            float driftX = (seed.X + t * 14f) % WorldW;
            float lbob = MathF.Sin(t * 3f + i * 1.1f) * 3f;
            Vector2 lp = new(driftX, seed.Y + lbob);
            Raylib.DrawCircleV(new Vector2(lp.X, lp.Y + 5f), 14f, C(255, 210, 100, 55));
            Raylib.DrawEllipse((int)lp.X, (int)(lp.Y + 10f), 5f, 8f, C(255, 200, 90, 40));
            Color petal = (i % 3) switch { 0 => HuePalette.LanternPaperPink, 1 => HuePalette.LanternPaperYellow, _ => HuePalette.LanternPaperOrange };
            Raylib.DrawCircleV(lp, 7f, petal);
            Raylib.DrawTriangle(lp + new Vector2(-7f, 0), lp + new Vector2(0, -6f), lp + new Vector2(7f, 0), petal);
            float flicker = MathF.Sin(t * 12f + i * 3f) * 1.5f;
            Raylib.DrawCircleV(lp + new Vector2(0, -4f + flicker), 2.5f, HuePalette.LanternFlame);
        }

        // Lotus clusters
        for (int i = 0; i < LotusSpots.Length; i++)
        {
            var sp = LotusSpots[i];
            float lbob = MathF.Sin(t * 2.5f + i * 1.2f) * 3f;
            Vector2 c = new(sp.X, sp.Y + lbob);
            Raylib.DrawEllipse((int)c.X, (int)c.Y, 22f, 9f, HanoiPalette.LotusLeafGreen);
            Raylib.DrawEllipseLines((int)c.X, (int)c.Y, 22f, 9f, C(40, 95, 45));
            Raylib.DrawEllipse((int)(c.X + 26f), (int)(c.Y + 5f), 12f, 5f, Raylib.ColorAlpha(HanoiPalette.LotusLeafGreen, 0.8f));
            VectorRenderer.DrawCartoonCircle(c + new Vector2(0, -5f), 8f, HuePalette.LanternPaperPink, HanoiPalette.LotusRose, 1.5f, withShadow: false);
            Raylib.DrawCircleV(c + new Vector2(0, -5f), 3.5f, HanoiPalette.LotusWhite);
        }
    }

    // ---- Trường Tiền bridge (X 1200..1600; deck 490..570 unchanged) ---------------------------

    private static void DrawTrangTienBridge(float t, bool gateUnlocked)
    {
        // Piers
        foreach (float px in new[] { 1200f, 1330f, 1460f, 1590f })
        {
            Rectangle pRec = new(px - 14f, 490f, 28f, 140f);
            Raylib.DrawRectangleRec(pRec, HuePalette.TrangTienPierStone);
            Raylib.DrawRectangleLinesEx(pRec, 1.5f, HuePalette.TrangTienSteelDark);
            Raylib.DrawRectangle((int)px - 16, 620, 32, 8, Raylib.ColorAlpha(C(30, 50, 60), 0.5f));
        }

        // Deck
        Rectangle deck = new(1200f, 490f, 400f, 80f);
        Raylib.DrawRectangleRec(deck, HuePalette.TrangTienDeck);
        Raylib.DrawRectangleLinesEx(deck, 2f, HuePalette.TrangTienSteelDark);
        for (float dx = 1210f; dx < 1600f; dx += 24f)
        {
            Raylib.DrawLineEx(new Vector2(dx, 530f), new Vector2(dx + 12f, 530f), 2f, C(240, 230, 200, 180));
        }
        Raylib.DrawRectangle(1200, 480, 400, 10, HuePalette.TrangTienRailing);
        Raylib.DrawRectangle(1200, 570, 400, 10, HuePalette.TrangTienRailing);

        // 3 steel truss arches with suspenders & cross bracing
        for (int span = 0; span < 3; span++)
        {
            float sLeft = 1200f + span * 130f;
            float sRight = sLeft + 130f;
            float sCenter = sLeft + 65f;
            Vector2 pLeft = new(sLeft, 485f);
            Vector2 pApex = new(sCenter, 430f);
            Vector2 pRight = new(sRight, 485f);
            int segments = 10;
            Vector2 prev = pLeft;
            for (int seg = 1; seg <= segments; seg++)
            {
                float u = seg / (float)segments;
                float x = sLeft + (sRight - sLeft) * u;
                float y = (1 - u) * (1 - u) * pLeft.Y + 2 * (1 - u) * u * pApex.Y + u * u * pRight.Y;
                Vector2 cur = new(x, y);
                Raylib.DrawLineEx(prev, cur, 3.5f, HuePalette.TrangTienSteel);
                Raylib.DrawLineEx(cur, new Vector2(cur.X, 485f), 1.5f, HuePalette.TrangTienSteelDark);
                if (seg % 2 == 0)
                {
                    Raylib.DrawLineEx(prev, new Vector2(cur.X, 485f), 1.0f, HuePalette.TrangTienSteelDark);
                    Raylib.DrawLineEx(new Vector2(prev.X, 485f), cur, 1.0f, HuePalette.TrangTienSteelDark);
                }
                prev = cur;
            }
        }

        // Lampposts
        foreach (float px in new[] { 1200f, 1330f, 1460f, 1590f })
        {
            Raylib.DrawLineEx(new Vector2(px, 480f), new Vector2(px, 445f), 2.5f, HuePalette.TrangTienSteelDark);
            float pulse = 0.9f + 0.1f * MathF.Sin(t * 2.6f + px);
            Raylib.DrawCircleV(new Vector2(px, 442f), 5f, HuePalette.LanternFlame);
            Raylib.DrawCircleV(new Vector2(px, 442f), 16f * pulse, C(255, 230, 140, 60));
        }

        // Climax barrier or golden portal (geometry unchanged)
        if (!gateUnlocked)
        {
            Rectangle barrier = new(1480f, 490f, 20f, 80f);
            Raylib.DrawRectangleRec(barrier, C(185, 45, 35, 240));
            Raylib.DrawRectangleLinesEx(barrier, 2f, HuePalette.RoyalGold);
            Raylib.DrawLineEx(new Vector2(1480f, 530f), new Vector2(1500f, 530f), 2f, HuePalette.RoyalGold);
        }
        else
        {
            float auraBob = MathF.Sin(t * 4f) * 4f;
            Rectangle portal = new(1500f + auraBob, 490f, 30f, 80f);
            Raylib.DrawRectangleGradientH((int)portal.X, (int)portal.Y, (int)portal.Width, (int)portal.Height,
                C(255, 215, 60, 180), C(255, 240, 160, 0));
        }

        // Destination sign
        Rectangle sign = new(1432f, 452f, 150f, 24f);
        VectorRenderer.DrawRoundedRect(sign, 0.3f, C(30, 92, 70, 235), HanoiPalette.CartoonOutline, 1.6f, withShadow: false);
        string label = "HỘI AN  ›";
        Vector2 sz = FontManager.Instance.MeasureText(label, 12f);
        FontManager.Instance.DrawText(label, new Vector2(sign.X + (sign.Width - sz.X) * 0.5f, sign.Y + 5f), 12f, C(255, 248, 226));
    }

    private static void DrawLighting()
    {
        // Warm dusk spill from the west sun, cool shade on the far east... reversed: sun near x=1150
        Raylib.DrawRectangleGradientH(1000, 320, 600, 580, Raylib.ColorAlpha(C(255, 170, 100), 0f), Raylib.ColorAlpha(C(255, 170, 100), 0.12f));
        Raylib.DrawRectangleGradientH(0, 320, 280, 580, Raylib.ColorAlpha(C(60, 40, 90), 0.12f), Raylib.ColorAlpha(C(60, 40, 90), 0f));
        Raylib.DrawRectangleGradientV(0, 840, WorldW, 60, Raylib.ColorAlpha(Color.Black, 0f), Raylib.ColorAlpha(Color.Black, 0.25f));
    }
}
