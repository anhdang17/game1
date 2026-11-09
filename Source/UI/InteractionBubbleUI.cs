using System;
using System.Numerics;
using GameProject.Graphics;
using Raylib_cs;

namespace GameProject.UI;

/// <summary>
/// Floating cartoon interaction prompt bubble with keycap badge [E] and action verb.
/// </summary>
public static class InteractionBubbleUI
{
    public static void Draw(Vector2 entityScreenPos, float visualHeight, string actionText, float alpha, float bobTimer)
    {
        if (alpha <= 0.01f || string.IsNullOrEmpty(actionText)) return;

        float bobOffset = MathF.Sin(bobTimer * 4.2f) * 3.5f;
        Vector2 center = new(entityScreenPos.X, entityScreenPos.Y - visualHeight - 16f + bobOffset);

        // Calculate size needed
        Vector2 textSize = FontManager.Instance.MeasureText(actionText, 13f);
        float badgeSize = 22f;
        float padX = 10f;
        float padY = 6f;
        float totalWidth = badgeSize + 8f + textSize.X + padX * 2f;
        float totalHeight = MathF.Max(badgeSize, textSize.Y) + padY * 2f;

        Rectangle bubbleRec = new(center.X - totalWidth * 0.5f, center.Y - totalHeight * 0.5f, totalWidth, totalHeight);

        // Colors modulated by alpha
        Color fillBg = Raylib.ColorAlpha(new Color(252, 250, 242, 250), alpha);
        Color borderCol = Raylib.ColorAlpha(HanoiPalette.CartoonOutline, alpha);
        Color badgeBg = Raylib.ColorAlpha(new Color(230, 161, 59, 255), alpha);
        Color badgeTextCol = Raylib.ColorAlpha(Color.White, alpha);
        Color labelCol = Raylib.ColorAlpha(new Color(40, 36, 34, 255), alpha);

        // Drop shadow
        Rectangle shadowRec = new(bubbleRec.X + 2f, bubbleRec.Y + 3f, bubbleRec.Width, bubbleRec.Height);
        Raylib.DrawRectangleRounded(shadowRec, 0.45f, 6, Raylib.ColorAlpha(Color.Black, 0.22f * alpha));

        // Pill body
        Raylib.DrawRectangleRounded(bubbleRec, 0.45f, 6, fillBg);
        Raylib.DrawRectangleRoundedLinesEx(bubbleRec, 0.45f, 6, 1.5f, borderCol);

        // Downward notch pointer
        Vector2 tip = new(center.X, bubbleRec.Y + bubbleRec.Height + 6f);
        Vector2 p1 = new(center.X - 5f, bubbleRec.Y + bubbleRec.Height - 1f);
        Vector2 p2 = new(center.X + 5f, bubbleRec.Y + bubbleRec.Height - 1f);
        Raylib.DrawTriangle(p2, tip, p1, fillBg);
        Raylib.DrawLineEx(p1, tip, 1.5f, borderCol);
        Raylib.DrawLineEx(p2, tip, 1.5f, borderCol);

        // Golden Keycap badge [E]
        Rectangle badgeRec = new(bubbleRec.X + padX, bubbleRec.Y + (bubbleRec.Height - badgeSize) * 0.5f, badgeSize, badgeSize);
        Raylib.DrawRectangleRounded(badgeRec, 0.35f, 4, badgeBg);
        Raylib.DrawRectangleRoundedLinesEx(badgeRec, 0.35f, 4, 1.0f, borderCol);
        Vector2 ePos = new(badgeRec.X + 6f, badgeRec.Y + 4f);
        FontManager.Instance.DrawText("E", ePos, 13f, badgeTextCol);

        // Action Label Text
        Vector2 labelPos = new(badgeRec.X + badgeSize + 8f, bubbleRec.Y + (bubbleRec.Height - textSize.Y) * 0.5f);
        FontManager.Instance.DrawText(actionText, labelPos, 13f, labelCol);
    }
}

