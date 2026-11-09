using System;
using System.Numerics;
using GameProject.Core.Movement;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Procedural vector rendering engine for rounded shapes, cartoon outlines, and Hanoi cultural elements.
/// </summary>
public static class VectorRenderer
{
    // --- Primitives ---

    public static void DrawDropShadow(Rectangle bounds, float roundness, float offsetY = 4f, float alpha = 0.22f)
    {
        Rectangle shadowRec = new(bounds.X + 2f, bounds.Y + offsetY, bounds.Width, bounds.Height);
        Raylib.DrawRectangleRounded(shadowRec, roundness, 6, Raylib.ColorAlpha(Color.Black, alpha));
    }

    public static void DrawRoundedRect(Rectangle bounds, float roundness, Color fillColor, Color outlineColor, float outlineWidth = 2f, bool withShadow = true)
    {
        if (withShadow) DrawDropShadow(bounds, roundness);
        Raylib.DrawRectangleRounded(bounds, roundness, 8, fillColor);
        if (outlineWidth > 0f)
        {
            Raylib.DrawRectangleRoundedLinesEx(bounds, roundness, 8, outlineWidth, outlineColor);
        }
    }

    public static void DrawCartoonCircle(Vector2 center, float radius, Color fillColor, Color outlineColor, float outlineWidth = 2f, bool withShadow = true)
    {
        if (withShadow)
        {
            Raylib.DrawCircleV(new Vector2(center.X + 2f, center.Y + 3f), radius, Raylib.ColorAlpha(Color.Black, 0.22f));
        }
        Raylib.DrawCircleV(center, radius, fillColor);
        if (outlineWidth > 0f)
        {
            Raylib.DrawRing(center, radius - outlineWidth, radius, 0f, 360f, 32, outlineColor);
        }
    }

    public static void DrawPatternedAwning(Rectangle bounds, Color color1, Color color2, int stripes = 6)
    {
        float stripeWidth = bounds.Width / stripes;
        for (int i = 0; i < stripes; i++)
        {
            Color c = (i % 2 == 0) ? color1 : color2;
            Rectangle stripeRec = new(bounds.X + i * stripeWidth, bounds.Y, stripeWidth, bounds.Height - 8f);
            Raylib.DrawRectangleRec(stripeRec, c);

            // Scalloped bottom ruffle
            Vector2 scallopCenter = new(bounds.X + (i + 0.5f) * stripeWidth, bounds.Y + bounds.Height - 8f);
            Raylib.DrawCircleSector(scallopCenter, stripeWidth * 0.5f, 0f, 180f, 12, c);
        }
        Raylib.DrawRectangleLinesEx(bounds, 1.5f, HanoiPalette.CartoonOutline);
    }

    // --- Iconic Hanoi Cultural Elements ---

    public static void DrawBeMay(Vector2 feetPos, FacingDirection facing, float animTime, bool isMoving, float scale = 1.0f)
    {
        float bob = isMoving ? MathF.Abs(MathF.Sin(animTime * 10f)) * 3f * scale : MathF.Sin(animTime * 2f) * 1f * scale;
        Vector2 basePos = new(feetPos.X, feetPos.Y - bob);

        // Ground contact shadow
        Raylib.DrawEllipse((int)feetPos.X, (int)feetPos.Y, 16f * scale, 8f * scale, Raylib.ColorAlpha(Color.Black, 0.28f));

        // Legs / Pants
        Rectangle leftLeg = new(basePos.X - 8f * scale, basePos.Y - 14f * scale, 6f * scale, 12f * scale);
        Rectangle rightLeg = new(basePos.X + 2f * scale, basePos.Y - 14f * scale, 6f * scale, 12f * scale);
        Raylib.DrawRectangleRounded(leftLeg, 0.4f, 4, HanoiPalette.BeMayPantsGrey);
        Raylib.DrawRectangleRounded(rightLeg, 0.4f, 4, HanoiPalette.BeMayPantsGrey);

        // Tunic (Áo Bà Ba)
        Rectangle torso = new(basePos.X - 12f * scale, basePos.Y - 36f * scale, 24f * scale, 24f * scale);
        DrawRoundedRect(torso, 0.35f, HanoiPalette.BeMayShirtBlue, HanoiPalette.CartoonOutline, 1.5f * scale, withShadow: false);

        // Satchel (Cuốn Sổ Ký Ức bag)
        Rectangle satchel = new(basePos.X + 4f * scale, basePos.Y - 26f * scale, 10f * scale, 10f * scale);
        DrawRoundedRect(satchel, 0.3f, HanoiPalette.SatchelMustard, HanoiPalette.CartoonOutline, 1.2f * scale, withShadow: false);
        Raylib.DrawLineEx(new Vector2(basePos.X - 10f * scale, basePos.Y - 34f * scale), new Vector2(basePos.X + 8f * scale, basePos.Y - 22f * scale), 2f * scale, new Color(109, 76, 65, 255));

        // Head
        Vector2 headCenter = new(basePos.X, basePos.Y - 44f * scale);
        Raylib.DrawCircleV(headCenter, 11f * scale, HanoiPalette.SkinPeach);

        // Facial Features (Only visible when facing Down, Left, or Right)
        if (facing != FacingDirection.Up)
        {
            // Cheeks
            Raylib.DrawCircleV(new Vector2(headCenter.X - 6f * scale, headCenter.Y + 2f * scale), 2.5f * scale, HanoiPalette.CheekBlush);
            Raylib.DrawCircleV(new Vector2(headCenter.X + 6f * scale, headCenter.Y + 2f * scale), 2.5f * scale, HanoiPalette.CheekBlush);

            // Eyes
            float eyeXOff = (facing == FacingDirection.Left) ? -2f * scale : (facing == FacingDirection.Right ? 2f * scale : 0f);
            Raylib.DrawCircleV(new Vector2(headCenter.X - 4f * scale + eyeXOff, headCenter.Y - 1f * scale), 1.8f * scale, HanoiPalette.CartoonOutline);
            Raylib.DrawCircleV(new Vector2(headCenter.X + 4f * scale + eyeXOff, headCenter.Y - 1f * scale), 1.8f * scale, HanoiPalette.CartoonOutline);
        }

        // Conical Hat (Nón Lá)
        Vector2 hatApex = new(headCenter.X, headCenter.Y - 16f * scale);
        Vector2 hatLeft = new(headCenter.X - 18f * scale, headCenter.Y - 4f * scale);
        Vector2 hatRight = new(headCenter.X + 18f * scale, headCenter.Y - 4f * scale);
        Raylib.DrawTriangle(hatRight, hatApex, hatLeft, HanoiPalette.NonLaStraw); // Counter-clockwise
        Raylib.DrawTriangleLines(hatRight, hatApex, hatLeft, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(new Vector2(headCenter.X, headCenter.Y - 8f * scale), 4f * scale, HanoiPalette.NonLaStraw);
    }

    public static void DrawButChi(Vector2 floatingPos, float animTime, float scale = 1.0f)
    {
        float hoverY = MathF.Sin(animTime * 3.5f) * 6f * scale;
        Vector2 center = new(floatingPos.X, floatingPos.Y + hoverY);

        // Ground shadow scales inversely with altitude
        float shadowAlpha = 0.25f - (hoverY / (30f * scale));
        Raylib.DrawEllipse((int)floatingPos.X, (int)(floatingPos.Y + 24f * scale), 10f * scale, 5f * scale, Raylib.ColorAlpha(Color.Black, Math.Clamp(shadowAlpha, 0.1f, 0.35f)));

        // Hexagonal Pencil Body
        Rectangle body = new(center.X - 5f * scale, center.Y - 12f * scale, 10f * scale, 22f * scale);
        DrawRoundedRect(body, 0.2f, HanoiPalette.ButChiYellow, HanoiPalette.CartoonOutline, 1.2f * scale, withShadow: false);

        // Eraser & Collar
        Rectangle collar = new(center.X - 5f * scale, center.Y - 16f * scale, 10f * scale, 4f * scale);
        Raylib.DrawRectangleRec(collar, HanoiPalette.ButChiFerrule);
        Rectangle eraser = new(center.X - 5f * scale, center.Y - 21f * scale, 10f * scale, 5f * scale);
        Raylib.DrawRectangleRounded(eraser, 0.4f, 4, HanoiPalette.ButChiEraserPink);

        // Sharpened Wooden Tip & Graphite
        Vector2 tipApex = new(center.X, center.Y + 18f * scale);
        Vector2 tipLeft = new(center.X - 5f * scale, center.Y + 10f * scale);
        Vector2 tipRight = new(center.X + 5f * scale, center.Y + 10f * scale);
        Raylib.DrawTriangle(tipRight, tipApex, tipLeft, HanoiPalette.ButChiWoodCollar);
        Raylib.DrawTriangle(tipRight, tipApex, new Vector2(center.X, center.Y + 14f * scale), HanoiPalette.ButChiGraphite);

        // Glowing Magical Cyan Sparkle at tip
        float sparkPulse = 2f + MathF.Sin(animTime * 8f) * 1.5f;
        Raylib.DrawCircleV(tipApex, sparkPulse * scale, HanoiPalette.ButChiMagicCyan);

        // Blinking Cartoon Eyes
        bool isBlinking = (animTime % 4.0f) < 0.15f;
        if (!isBlinking)
        {
            Raylib.DrawCircleV(new Vector2(center.X - 2.5f * scale, center.Y - 2f * scale), 1.5f * scale, HanoiPalette.CartoonOutline);
            Raylib.DrawCircleV(new Vector2(center.X + 2.5f * scale, center.Y - 2f * scale), 1.5f * scale, HanoiPalette.CartoonOutline);
            Raylib.DrawCircleV(new Vector2(center.X - 2.2f * scale, center.Y - 2.5f * scale), 0.5f * scale, Color.White);
            Raylib.DrawCircleV(new Vector2(center.X + 2.8f * scale, center.Y - 2.5f * scale), 0.5f * scale, Color.White);
        }
        else
        {
            Raylib.DrawLineEx(new Vector2(center.X - 4f * scale, center.Y - 2f * scale), new Vector2(center.X - 1f * scale, center.Y - 2f * scale), 1f, HanoiPalette.CartoonOutline);
            Raylib.DrawLineEx(new Vector2(center.X + 1f * scale, center.Y - 2f * scale), new Vector2(center.X + 4f * scale, center.Y - 2f * scale), 1f, HanoiPalette.CartoonOutline);
        }
    }

    public static void DrawFlowerBicycle(Vector2 pos, float animTime, float scale = 1.0f)
    {
        // Ground shadow
        Raylib.DrawEllipse((int)pos.X, (int)pos.Y, 38f * scale, 10f * scale, Raylib.ColorAlpha(Color.Black, 0.25f));

        // Two spoked wheels
        Vector2 rearWheel = new(pos.X - 24f * scale, pos.Y - 14f * scale);
        Vector2 frontWheel = new(pos.X + 24f * scale, pos.Y - 14f * scale);
        DrawCartoonCircle(rearWheel, 13f * scale, Color.Blank, HanoiPalette.CartoonOutline, 2f * scale, withShadow: false);
        DrawCartoonCircle(frontWheel, 13f * scale, Color.Blank, HanoiPalette.CartoonOutline, 2f * scale, withShadow: false);

        // Frame
        Vector2 crank = new(pos.X - 2f * scale, pos.Y - 14f * scale);
        Vector2 saddle = new(pos.X - 8f * scale, pos.Y - 30f * scale);
        Vector2 headset = new(pos.X + 16f * scale, pos.Y - 32f * scale);
        Raylib.DrawLineEx(rearWheel, crank, 2.5f * scale, HanoiPalette.BicycleFrameTeal);
        Raylib.DrawLineEx(crank, saddle, 2.5f * scale, HanoiPalette.BicycleFrameTeal);
        Raylib.DrawLineEx(saddle, headset, 2.5f * scale, HanoiPalette.BicycleFrameTeal);
        Raylib.DrawLineEx(crank, headset, 2.5f * scale, HanoiPalette.BicycleFrameTeal);
        Raylib.DrawLineEx(headset, frontWheel, 2.5f * scale, HanoiPalette.BicycleFrameTeal);

        // Twin Woven Bamboo Baskets
        Rectangle basketRec = new(pos.X - 32f * scale, pos.Y - 34f * scale, 22f * scale, 18f * scale);
        DrawRoundedRect(basketRec, 0.4f, HanoiPalette.BambooWoven, HanoiPalette.CartoonOutline, 1.5f * scale);

        // Lotus Flowers overflowing
        Vector2[] lotusCenters =
        [
            new(pos.X - 26f * scale, pos.Y - 38f * scale),
            new(pos.X - 18f * scale, pos.Y - 42f * scale),
            new(pos.X - 12f * scale, pos.Y - 36f * scale)
        ];
        for (int i = 0; i < lotusCenters.Length; i++)
        {
            var lc = lotusCenters[i];
            Raylib.DrawCircleV(lc, 6f * scale, HanoiPalette.LotusPink);
            Raylib.DrawCircleV(new Vector2(lc.X, lc.Y - 1f), 3f * scale, HanoiPalette.LotusRose);
        }
        // Lotus leaves
        Raylib.DrawCircleV(new Vector2(pos.X - 32f * scale, pos.Y - 36f * scale), 7f * scale, HanoiPalette.LotusLeafGreen);
    }

    public static void DrawVintageCyclo(Vector2 pos, float scale = 1.0f)
    {
        // Ground shadow
        Raylib.DrawEllipse((int)pos.X, (int)pos.Y, 46f * scale, 12f * scale, Raylib.ColorAlpha(Color.Black, 0.28f));

        // Wheels: 2 front wheels + 1 rear
        Vector2 frontWheel1 = new(pos.X - 26f * scale, pos.Y - 14f * scale);
        Vector2 frontWheel2 = new(pos.X - 16f * scale, pos.Y - 12f * scale);
        Vector2 rearWheel = new(pos.X + 28f * scale, pos.Y - 16f * scale);
        DrawCartoonCircle(frontWheel1, 14f * scale, Color.Blank, HanoiPalette.CartoonOutline, 2f * scale, withShadow: false);
        DrawCartoonCircle(frontWheel2, 14f * scale, Color.Blank, HanoiPalette.CartoonOutline, 2f * scale, withShadow: false);
        DrawCartoonCircle(rearWheel, 16f * scale, Color.Blank, HanoiPalette.CartoonOutline, 2f * scale, withShadow: false);

        // Passenger Seat Carriage
        Rectangle seatRec = new(pos.X - 28f * scale, pos.Y - 36f * scale, 26f * scale, 22f * scale);
        DrawRoundedRect(seatRec, 0.3f, HanoiPalette.CycloSeatRed, HanoiPalette.CartoonOutline, 2f * scale);

        // Canopy Hood
        Vector2 canopyApex = new(pos.X - 32f * scale, pos.Y - 52f * scale);
        Vector2 canopyBack = new(pos.X - 6f * scale, pos.Y - 38f * scale);
        Vector2 canopyFront = new(pos.X - 34f * scale, pos.Y - 38f * scale);
        Raylib.DrawTriangle(canopyBack, canopyApex, canopyFront, HanoiPalette.CycloCanopyOlive);
        Raylib.DrawTriangleLines(canopyBack, canopyApex, canopyFront, HanoiPalette.CartoonOutline);
    }

    public static void DrawBluePlasticTableAndStools(Vector2 pos, float scale = 1.0f)
    {
        // Ground shadow
        Raylib.DrawEllipse((int)pos.X, (int)pos.Y, 24f * scale, 8f * scale, Raylib.ColorAlpha(Color.Black, 0.22f));

        // Two low plastic stools
        Rectangle leftStool = new(pos.X - 22f * scale, pos.Y - 14f * scale, 12f * scale, 10f * scale);
        Rectangle rightStool = new(pos.X + 10f * scale, pos.Y - 14f * scale, 12f * scale, 10f * scale);
        DrawRoundedRect(leftStool, 0.3f, HanoiPalette.PlasticStoolRed, HanoiPalette.CartoonOutline, 1.2f * scale);
        DrawRoundedRect(rightStool, 0.3f, HanoiPalette.PlasticStoolRed, HanoiPalette.CartoonOutline, 1.2f * scale);

        // Table Top (Blue plastic)
        Rectangle tableRec = new(pos.X - 16f * scale, pos.Y - 26f * scale, 32f * scale, 20f * scale);
        DrawRoundedRect(tableRec, 0.35f, HanoiPalette.PlasticTableBlue, HanoiPalette.CartoonOutline, 1.5f * scale);
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(tableRec.X + 2f, tableRec.Y + 2f, tableRec.Width - 4f, 4f), 0.5f, 4, 1f, HanoiPalette.PlasticTableLight);
    }

    public static void DrawMarketStall(Vector2 pos, string stallName, Color awning1, Color awning2, float scale = 1.0f)
    {
        // Ground shadow
        Raylib.DrawEllipse((int)pos.X, (int)pos.Y, 50f * scale, 14f * scale, Raylib.ColorAlpha(Color.Black, 0.26f));

        // Wooden counter
        Rectangle counterRec = new(pos.X - 36f * scale, pos.Y - 32f * scale, 72f * scale, 30f * scale);
        DrawRoundedRect(counterRec, 0.2f, HanoiPalette.WoodStoolBrown, HanoiPalette.CartoonOutline, 2f * scale);
        // Counter panels
        Raylib.DrawRectangleLinesEx(new Rectangle(counterRec.X + 6f * scale, counterRec.Y + 4f * scale, counterRec.Width - 12f * scale, counterRec.Height - 8f * scale), 1.5f, HanoiPalette.WoodStoolDark);

        // Awning support posts
        Raylib.DrawLineEx(new Vector2(pos.X - 32f * scale, pos.Y - 32f * scale), new Vector2(pos.X - 34f * scale, pos.Y - 60f * scale), 3f * scale, HanoiPalette.WoodStoolDark);
        Raylib.DrawLineEx(new Vector2(pos.X + 32f * scale, pos.Y - 32f * scale), new Vector2(pos.X + 34f * scale, pos.Y - 60f * scale), 3f * scale, HanoiPalette.WoodStoolDark);

        // Striped Awning
        Rectangle awningRec = new(pos.X - 42f * scale, pos.Y - 68f * scale, 84f * scale, 22f * scale);
        DrawPatternedAwning(awningRec, awning1, awning2, stripes: 6);

        // Signboard
        if (!string.IsNullOrEmpty(stallName))
        {
            Rectangle signRec = new(pos.X - 30f * scale, pos.Y - 24f * scale, 60f * scale, 14f * scale);
            DrawRoundedRect(signRec, 0.25f, HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1f * scale, withShadow: false);
            Vector2 textPos = new(signRec.X + 4f * scale, signRec.Y + 1f * scale);
            FontManager.Instance.DrawText(stallName, textPos, 10f * scale, HanoiPalette.CartoonOutline);
        }
    }

    public static void DrawInteractionPrompt(Vector2 pos, string keyText, string actionText, float animTime, float alpha = 1.0f)
    {
        if (alpha <= 0.01f) return;

        float floatOffset = MathF.Sin(animTime * 4.2f) * 3.5f;
        Vector2 center = new(pos.X, pos.Y - 60f + floatOffset);

        string fullText = $"[{keyText}] {actionText}";
        Vector2 size = FontManager.Instance.MeasureText(fullText, 14f);
        Rectangle bubble = new(center.X - size.X * 0.5f - 8f, center.Y - size.Y * 0.5f - 4f, size.X + 16f, size.Y + 8f);

        // Speech bubble body
        Color fill = Raylib.ColorAlpha(new Color(252, 250, 242, 255), alpha);
        Color border = Raylib.ColorAlpha(HanoiPalette.CartoonOutline, alpha);
        DrawRoundedRect(bubble, 0.45f, fill, border, 1.8f, withShadow: true);

        // Downward notch pointer
        Vector2 tip = new(center.X, bubble.Y + bubble.Height + 6f);
        Vector2 p1 = new(center.X - 5f, bubble.Y + bubble.Height - 1f);
        Vector2 p2 = new(center.X + 5f, bubble.Y + bubble.Height - 1f);
        Raylib.DrawTriangle(p2, tip, p1, fill);
        Raylib.DrawLineEx(p1, tip, 1.8f, border);
        Raylib.DrawLineEx(p2, tip, 1.8f, border);

        // Text
        FontManager.Instance.DrawText(fullText, new Vector2(bubble.X + 8f, bubble.Y + 4f), 14f, border);
    }

    public static void DrawCloseCross(Vector2 center, float size, Color color, float thickness = 2.0f)
    {
        float half = size * 0.5f;
        Raylib.DrawLineEx(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half), thickness, color);
        Raylib.DrawLineEx(new Vector2(center.X + half, center.Y - half), new Vector2(center.X - half, center.Y + half), thickness, color);
    }

    public static void DrawCheckmark(Vector2 center, float size, Color color, float thickness = 2.0f)
    {
        Vector2 start = new(center.X - size * 0.45f, center.Y);
        Vector2 mid = new(center.X - size * 0.1f, center.Y + size * 0.35f);
        Vector2 end = new(center.X + size * 0.45f, center.Y - size * 0.45f);
        Raylib.DrawLineEx(start, mid, thickness, color);
        Raylib.DrawLineEx(mid, end, thickness, color);
    }

    public static void DrawFivePointStar(Vector2 center, float radius, Color fillColor, Color? outlineColor = null, float outlineWidth = 1.5f)
    {
        Span<Vector2> points = stackalloc Vector2[10];
        float innerRadius = radius * 0.42f;
        float angleStep = MathF.PI / 5f;
        float startAngle = -MathF.PI * 0.5f;

        for (int i = 0; i < 10; i++)
        {
            float r = (i % 2 == 0) ? radius : innerRadius;
            float angle = startAngle + i * angleStep;
            points[i] = new Vector2(center.X + MathF.Cos(angle) * r, center.Y + MathF.Sin(angle) * r);
        }

        for (int i = 0; i < 10; i++)
        {
            int next = (i + 1) % 10;
            Raylib.DrawTriangle(center, points[i], points[next], fillColor);
        }

        if (outlineColor.HasValue && outlineWidth > 0f)
        {
            for (int i = 0; i < 10; i++)
            {
                int next = (i + 1) % 10;
                Raylib.DrawLineEx(points[i], points[next], outlineWidth, outlineColor.Value);
            }
        }
    }

    public static void DrawEnvelopeIcon(Vector2 center, float width, float height, Color bodyColor, Color flapColor, Color outlineColor)
    {
        Rectangle envRect = new(center.X - width * 0.5f, center.Y - height * 0.5f, width, height);
        Raylib.DrawRectangleRec(envRect, bodyColor);
        Raylib.DrawRectangleLinesEx(envRect, 1.2f, outlineColor);

        // Flap lines
        Vector2 topLeft = new(envRect.X, envRect.Y);
        Vector2 topRight = new(envRect.X + envRect.Width, envRect.Y);
        Vector2 flapTip = new(center.X, center.Y + 2f);

        Raylib.DrawTriangle(topLeft, flapTip, topRight, flapColor);
        Raylib.DrawLineEx(topLeft, flapTip, 1.2f, outlineColor);
        Raylib.DrawLineEx(topRight, flapTip, 1.2f, outlineColor);
    }
}

