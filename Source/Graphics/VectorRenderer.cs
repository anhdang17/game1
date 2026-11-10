using System;
using System.IO;
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
        if (!Raylib.IsWindowReady()) return;
        Rectangle shadowRec = new(bounds.X + 2f, bounds.Y + offsetY, bounds.Width, bounds.Height);
        Raylib.DrawRectangleRounded(shadowRec, roundness, 6, Raylib.ColorAlpha(Color.Black, alpha));
    }

    public static void DrawRoundedRect(Rectangle bounds, float roundness, Color fillColor, Color outlineColor, float outlineWidth = 2f, bool withShadow = true)
    {
        if (!Raylib.IsWindowReady()) return;
        if (withShadow) DrawDropShadow(bounds, roundness);
        Raylib.DrawRectangleRounded(bounds, roundness, 8, fillColor);
        if (outlineWidth > 0f)
        {
            Raylib.DrawRectangleRoundedLinesEx(bounds, roundness, 8, outlineWidth, outlineColor);
        }
    }

    public static void DrawCartoonCircle(Vector2 center, float radius, Color fillColor, Color outlineColor, float outlineWidth = 2f, bool withShadow = true)
    {
        if (!Raylib.IsWindowReady()) return;
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
        if (!Raylib.IsWindowReady()) return;
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

    public static void DrawBeMay(Vector2 feetPos, FacingDirection facing, float animTime, bool isMoving, float scale = 1.0f, bool withShadow = true)
    {
        if (!Raylib.IsWindowReady()) return;

        // Idle floating hover vs moving bouncy glide
        float hoverBob = isMoving
            ? MathF.Abs(MathF.Sin(animTime * 8f)) * 3.5f * scale
            : MathF.Sin(animTime * 2.8f) * 4.0f * scale;

        // Squash & stretch: compresses on downward motion, stretches when floating upward
        float stretchCycle = isMoving ? MathF.Sin(animTime * 8f) : MathF.Sin(animTime * 2.8f);
        float stretchX = 1.0f + (isMoving ? 0.08f : 0.035f) * stretchCycle;
        float stretchY = 1.0f - (isMoving ? 0.08f : 0.035f) * stretchCycle;

        // Center of cloud cluster anchored above ground
        Vector2 cloudCenter = new(feetPos.X, feetPos.Y - 26f * scale - hoverBob);

        // Ground shadow scales inversely with altitude
        if (withShadow)
        {
            float shadowAlpha = Math.Clamp(0.38f - (hoverBob / (45f * scale)), 0.18f, 0.45f);
            float shadowW = Math.Max(6f * scale, (24f * scale) * (1.0f - hoverBob / (60f * scale)));
            float shadowH = Math.Max(2.5f * scale, 9.0f * scale);
            Raylib.DrawEllipse((int)feetPos.X, (int)feetPos.Y, shadowW, shadowH, Raylib.ColorAlpha(new Color(25, 25, 30, 255), shadowAlpha));
        }

        // Ambient micro cloud puffs & floating droplets beneath the cloud
        float dropletBob = MathF.Sin(animTime * 3.2f) * 1.5f * scale;
        Vector2 puffCluster = new(cloudCenter.X - 19f * scale, feetPos.Y - 7.5f * scale + dropletBob);
        Raylib.DrawCircleV(puffCluster, 3.8f * scale + 1.2f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(puffCluster + new Vector2(0f, 1.0f * scale), 3.8f * scale, HanoiPalette.BeMayCloudShadow);
        Raylib.DrawCircleV(puffCluster, 3.8f * scale, HanoiPalette.BeMayCloudWhite);
        Raylib.DrawCircleV(new Vector2(puffCluster.X - 3.5f * scale, puffCluster.Y + 0.5f * scale), 2.4f * scale, HanoiPalette.BeMayCloudWhite);

        Vector2 dropLeft = new(cloudCenter.X - 28f * scale, feetPos.Y - 4.5f * scale + MathF.Sin(animTime * 2.6f) * 1.2f * scale);
        Raylib.DrawCircleV(dropLeft, 1.4f * scale, HanoiPalette.BeMayCloudWhite);

        Vector2 puffRight = new(cloudCenter.X + 17f * scale, feetPos.Y - 6.5f * scale + MathF.Sin(animTime * 2.8f + 1f) * 1.4f * scale);
        Raylib.DrawCircleV(puffRight, 2.8f * scale + 1.0f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(puffRight + new Vector2(0f, 0.8f * scale), 2.8f * scale, HanoiPalette.BeMayCloudShadow);
        Raylib.DrawCircleV(puffRight, 2.8f * scale, HanoiPalette.BeMayCloudWhite);

        Vector2 dropRight = new(cloudCenter.X + 25f * scale, feetPos.Y - 3.5f * scale + MathF.Sin(animTime * 3.0f + 2f) * 1.0f * scale);
        Raylib.DrawCircleV(dropRight, 1.3f * scale, HanoiPalette.BeMayCloudWhite);

        // Fluffy multi-lobed cloud body silhouette
        (Vector2 Offset, float Radius)[] puffs =
        [
            (new Vector2(0f, 0f), 17.0f * scale),                                              // Center core
            (new Vector2(-14.0f * scale * stretchX, 1.0f * scale * stretchY), 13.5f * scale),  // Mid-left
            (new Vector2(+14.0f * scale * stretchX, 1.0f * scale * stretchY), 13.5f * scale),  // Mid-right
            (new Vector2(-22.0f * scale * stretchX, 3.5f * scale * stretchY), 10.5f * scale),  // Far-left cheek puff
            (new Vector2(+22.0f * scale * stretchX, 3.5f * scale * stretchY), 10.5f * scale),  // Far-right cheek puff
            (new Vector2(-16.0f * scale * stretchX, -8.5f * scale * stretchY), 11.0f * scale), // Upper-left lobe
            (new Vector2(+16.0f * scale * stretchX, -8.5f * scale * stretchY), 11.0f * scale), // Upper-right lobe
            (new Vector2(-8.0f * scale * stretchX, -15.0f * scale * stretchY), 10.5f * scale), // Top-left shoulder
            (new Vector2(+8.0f * scale * stretchX, -15.0f * scale * stretchY), 10.5f * scale), // Top-right shoulder
            (new Vector2(0f, -14.0f * scale * stretchY), 11.5f * scale),                       // Top crown plateau
            (new Vector2(-12.5f * scale * stretchX, 11.5f * scale * stretchY), 9.0f * scale),  // Lower-left underbelly
            (new Vector2(+12.5f * scale * stretchX, 11.5f * scale * stretchY), 9.0f * scale),  // Lower-right underbelly
            (new Vector2(-4.0f * scale * stretchX, 13.5f * scale * stretchY), 7.5f * scale),   // Bottom ripple left
            (new Vector2(+4.0f * scale * stretchX, 13.5f * scale * stretchY), 7.5f * scale)    // Bottom ripple right
        ];

        // Pass 1: Outer cartoon outline silhouette
        for (int i = 0; i < puffs.Length; i++)
        {
            Raylib.DrawCircleV(cloudCenter + puffs[i].Offset, puffs[i].Radius + 2.0f * scale, HanoiPalette.CartoonOutline);
        }

        // Pass 2: Pale lavender-blue underbelly shadow puffs
        Vector2 shadowShift = new(0f, 3.2f * scale * stretchY);
        for (int i = 0; i < puffs.Length; i++)
        {
            Raylib.DrawCircleV(cloudCenter + puffs[i].Offset + shadowShift, puffs[i].Radius, HanoiPalette.BeMayCloudShadow);
        }

        // Pass 3: Fluffy milk-white pastel cloud body puffs
        for (int i = 0; i < puffs.Length; i++)
        {
            Raylib.DrawCircleV(cloudCenter + puffs[i].Offset, puffs[i].Radius, HanoiPalette.BeMayCloudWhite);
        }

        // Pass 4: Subtle decorative cloud body swirl on upper right
        Vector2 swirlCenter = new(cloudCenter.X + 15.0f * scale * stretchX, cloudCenter.Y - 6.5f * scale * stretchY);
        Raylib.DrawRing(swirlCenter, 2.5f * scale, 3.8f * scale, 120f, 330f, 16, HanoiPalette.BeMayCloudSwirl);
        Raylib.DrawRing(new Vector2(swirlCenter.X - 1.2f * scale, swirlCenter.Y - 0.8f * scale), 1.0f * scale, 2.2f * scale, 270f, 460f, 12, HanoiPalette.BeMayCloudSwirl);

        // Party Cone Hat with Pom-Pom perched on head
        float crestFacingOff = (facing == FacingDirection.Left ? -2f : (facing == FacingDirection.Right ? 2f : 0f)) * scale;
        Vector2 hatBaseCenter = new(cloudCenter.X + crestFacingOff, cloudCenter.Y - 14.5f * scale * stretchY);
        Vector2 hatApex = hatBaseCenter + new Vector2(0.5f * scale, -22.0f * scale);
        Vector2 hatBaseLeft = hatBaseCenter + new Vector2(-8.5f * scale, 0.5f * scale);
        Vector2 hatBaseRight = hatBaseCenter + new Vector2(8.5f * scale, 0.5f * scale);

        // Interpolated stripe bands for party hat (Cream top, Yellow middle, Pink bottom)
        Vector2 band1Left = Vector2.Lerp(hatApex, hatBaseLeft, 0.52f);
        Vector2 band1Right = Vector2.Lerp(hatApex, hatBaseRight, 0.52f);
        Vector2 band2Left = Vector2.Lerp(hatApex, hatBaseLeft, 0.78f);
        Vector2 band2Right = Vector2.Lerp(hatApex, hatBaseRight, 0.78f);

        // Fill bands
        Raylib.DrawTriangle(band1Right, hatApex, band1Left, HanoiPalette.BeMayHatCream);
        Raylib.DrawTriangle(band2Right, band1Right, band1Left, HanoiPalette.BeMayHatYellow);
        Raylib.DrawTriangle(band1Left, band2Left, band2Right, HanoiPalette.BeMayHatYellow);
        Raylib.DrawTriangle(hatBaseRight, band2Right, band2Left, HanoiPalette.BeMayHatPink);
        Raylib.DrawTriangle(band2Left, hatBaseLeft, hatBaseRight, HanoiPalette.BeMayHatPink);

        // Outer hat outlines and band dividers
        Raylib.DrawTriangleLines(hatBaseRight, hatApex, hatBaseLeft, HanoiPalette.CartoonOutline);
        Raylib.DrawLineEx(band1Left, band1Right, 1.2f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawLineEx(band2Left, band2Right, 1.2f * scale, HanoiPalette.CartoonOutline);

        // Fluffy pom-pom on top of party hat
        Vector2 pomCenter = hatApex + new Vector2(0f, -0.8f * scale);
        Raylib.DrawCircleV(pomCenter, 3.2f * scale + 1.2f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(pomCenter, 3.2f * scale, HanoiPalette.BeMayHatCream);
        Raylib.DrawCircleV(new Vector2(pomCenter.X - 0.8f * scale, pomCenter.Y - 1.0f * scale), 1.0f * scale, Color.White);

        // Diagonal leather strap from upper left chest to lower right pouch
        Vector2 strapStart = new(cloudCenter.X - 14.5f * scale * stretchX, cloudCenter.Y - 7.5f * scale * stretchY);
        Vector2 strapEnd = new(cloudCenter.X + 4.5f * scale * stretchX, cloudCenter.Y + 8.5f * scale * stretchY);
        Raylib.DrawLineEx(strapStart, strapEnd, 3.6f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawLineEx(strapStart, strapEnd, 2.4f * scale, HanoiPalette.BeMayLeatherDark);

        // Small buckle ring connector
        Raylib.DrawCircleV(strapEnd, 2.0f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawCircleV(strapEnd, 1.2f * scale, Color.White);

        // Rounded leather gourd/canteen pouch hanging on the strap
        float gourdX = strapEnd.X + (facing == FacingDirection.Left ? -10.5f : 6.5f) * scale * stretchX;
        float gourdY = strapEnd.Y + 2.5f * scale * stretchY;
        Vector2 gourdCenter = new(gourdX, gourdY);

        // Gourd neck / cap
        Rectangle neckRec = new(gourdCenter.X - 2.2f * scale, gourdCenter.Y - 5.5f * scale, 4.4f * scale, 3.2f * scale);
        DrawRoundedRect(neckRec, 0.35f, HanoiPalette.BeMayLeatherDark, HanoiPalette.CartoonOutline, 1.0f * scale, withShadow: false);

        // Gourd rounded body & highlight
        DrawCartoonCircle(gourdCenter, 5.8f * scale, HanoiPalette.BeMayGourdBrown, HanoiPalette.CartoonOutline, 1.2f * scale, withShadow: false);
        Raylib.DrawCircleV(gourdCenter + new Vector2(-1.4f * scale, -1.2f * scale), 2.2f * scale, HanoiPalette.BeMayGourdHighlight);
        Raylib.DrawCircleV(gourdCenter + new Vector2(-1.8f * scale, -1.6f * scale), 0.9f * scale, Color.White);

        // Facial Features (Visible when not facing Away/Up)
        if (facing != FacingDirection.Up)
        {
            float eyeXOff = (facing == FacingDirection.Left) ? -2.8f * scale : (facing == FacingDirection.Right ? 2.8f * scale : 0f);

            // Left blush cheek with anime blush hatching
            Vector2 leftCheek = new(cloudCenter.X - 15.0f * scale + eyeXOff * 0.6f, cloudCenter.Y + 4.5f * scale * stretchY);
            Raylib.DrawEllipse((int)leftCheek.X, (int)leftCheek.Y, 4.5f * scale, 3.5f * scale, new Color(243, 167, 181, 230));
            Raylib.DrawLineEx(new Vector2(leftCheek.X - 1.5f * scale, leftCheek.Y - 2.0f * scale), new Vector2(leftCheek.X - 1.0f * scale, leftCheek.Y + 2.0f * scale), 1.0f * scale, new Color(225, 140, 155, 230));
            Raylib.DrawLineEx(new Vector2(leftCheek.X + 1.0f * scale, leftCheek.Y - 2.0f * scale), new Vector2(leftCheek.X + 1.5f * scale, leftCheek.Y + 2.0f * scale), 1.0f * scale, new Color(225, 140, 155, 230));

            // Chibi anime eyes with catchlights & blinking
            Vector2 leftEye = new(cloudCenter.X - 7.5f * scale + eyeXOff, cloudCenter.Y - 1.5f * scale * stretchY);
            Vector2 rightEye = new(cloudCenter.X + 4.5f * scale + eyeXOff, cloudCenter.Y - 1.5f * scale * stretchY);

            bool isBlinking = (animTime % 3.6f) < 0.14f;
            if (isBlinking)
            {
                Raylib.DrawLineEx(new Vector2(leftEye.X - 2.8f * scale, leftEye.Y), new Vector2(leftEye.X + 2.8f * scale, leftEye.Y), 1.8f * scale, HanoiPalette.CartoonOutline);
                Raylib.DrawLineEx(new Vector2(rightEye.X - 2.8f * scale, rightEye.Y), new Vector2(rightEye.X + 2.8f * scale, rightEye.Y), 1.8f * scale, HanoiPalette.CartoonOutline);
            }
            else
            {
                // Expressive dark eye bodies with rounded rectangle contour
                Rectangle leftEyeRec = new(leftEye.X - 2.8f * scale, leftEye.Y - 3.0f * scale, 5.6f * scale, 6.0f * scale);
                Rectangle rightEyeRec = new(rightEye.X - 2.8f * scale, rightEye.Y - 3.0f * scale, 5.6f * scale, 6.0f * scale);
                Raylib.DrawRectangleRounded(leftEyeRec, 0.45f, 6, HanoiPalette.CartoonOutline);
                Raylib.DrawRectangleRounded(rightEyeRec, 0.45f, 6, HanoiPalette.CartoonOutline);

                // Determined brow tilts on top of eyes
                Raylib.DrawLineEx(new Vector2(leftEyeRec.X - 0.4f * scale, leftEyeRec.Y - 0.2f * scale), new Vector2(leftEyeRec.X + leftEyeRec.Width + 0.6f * scale, leftEyeRec.Y + 0.8f * scale), 1.2f * scale, HanoiPalette.CartoonOutline);
                Raylib.DrawLineEx(new Vector2(rightEyeRec.X - 0.6f * scale, rightEyeRec.Y + 0.8f * scale), new Vector2(rightEyeRec.X + rightEyeRec.Width + 0.4f * scale, rightEyeRec.Y - 0.2f * scale), 1.2f * scale, HanoiPalette.CartoonOutline);

                // Primary bright sparkle highlights
                Raylib.DrawCircleV(new Vector2(leftEye.X + 1.1f * scale, leftEye.Y - 1.1f * scale), 1.5f * scale, Color.White);
                Raylib.DrawCircleV(new Vector2(rightEye.X + 1.1f * scale, rightEye.Y - 1.1f * scale), 1.5f * scale, Color.White);

                // Secondary subtle catchlights
                Raylib.DrawCircleV(new Vector2(leftEye.X - 1.1f * scale, leftEye.Y + 1.1f * scale), 0.8f * scale, Color.White);
                Raylib.DrawCircleV(new Vector2(rightEye.X - 1.1f * scale, rightEye.Y + 1.1f * scale), 0.8f * scale, Color.White);
            }

            // Cheerful open smile
            Vector2 mouthCenter = new(cloudCenter.X - 1.5f * scale + eyeXOff * 0.5f, cloudCenter.Y + 3.2f * scale * stretchY);
            Rectangle mouthRec = new(mouthCenter.X - 2.4f * scale, mouthCenter.Y - 0.8f * scale, 4.8f * scale, 3.4f * scale);
            Raylib.DrawRectangleRounded(mouthRec, 0.7f, 6, Color.White);
            Raylib.DrawRectangleRoundedLinesEx(mouthRec, 0.7f, 6, 1.4f * scale, HanoiPalette.CartoonOutline);
        }
    }

    public static void DrawButChi(Vector2 floatingPos, float animTime, float scale = 1.0f)
    {
        if (!Raylib.IsWindowReady()) return;

        float hoverY = MathF.Sin(animTime * 3.5f) * 6f * scale;
        Vector2 center = new(floatingPos.X, floatingPos.Y + hoverY);

        // Ground shadow scales inversely with altitude
        float shadowAlpha = Math.Clamp(0.28f - (hoverY / (30f * scale)), 0.10f, 0.38f);
        Raylib.DrawEllipse((int)floatingPos.X, (int)(floatingPos.Y + 24f * scale), 10f * scale, 5f * scale, Raylib.ColorAlpha(Color.Black, shadowAlpha));

        // 3-facet hexagonal wooden pencil body
        Rectangle leftFacet = new(center.X - 5.5f * scale, center.Y - 12f * scale, 3.5f * scale, 22f * scale);
        Rectangle centerFacet = new(center.X - 2.0f * scale, center.Y - 12f * scale, 4.0f * scale, 22f * scale);
        Rectangle rightFacet = new(center.X + 2.0f * scale, center.Y - 12f * scale, 3.5f * scale, 22f * scale);

        Raylib.DrawRectangleRec(leftFacet, new Color(230, 180, 50, 255));   // Warm shadow facet
        Raylib.DrawRectangleRec(centerFacet, HanoiPalette.ButChiYellow);     // Sunny body facet
        Raylib.DrawRectangleRec(rightFacet, new Color(255, 238, 130, 255));  // Sunlit highlight facet

        // Glowing cyan horizontal magic accent band in center
        Rectangle magicCore = new(center.X - 5.5f * scale, center.Y - 2.0f * scale, 11f * scale, 4.0f * scale);
        Raylib.DrawRectangleRec(magicCore, Raylib.ColorAlpha(HanoiPalette.ButChiMagicCyan, 0.35f));
        Raylib.DrawLineEx(new Vector2(center.X - 4.5f * scale, center.Y), new Vector2(center.X + 4.5f * scale, center.Y), 2.2f * scale, HanoiPalette.ButChiMagicCyan);
        Raylib.DrawCircleV(new Vector2(center.X, center.Y), 1.5f * scale, Color.White);

        // Facet seams and outer body outline
        Raylib.DrawLineEx(new Vector2(center.X - 2.0f * scale, center.Y - 12f * scale), new Vector2(center.X - 2.0f * scale, center.Y + 10f * scale), 1.0f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawLineEx(new Vector2(center.X + 2.0f * scale, center.Y - 12f * scale), new Vector2(center.X + 2.0f * scale, center.Y + 10f * scale), 1.0f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawRectangleLinesEx(new Rectangle(center.X - 5.5f * scale, center.Y - 12f * scale, 11f * scale, 22f * scale), 1.2f * scale, HanoiPalette.CartoonOutline);

        // Dark ferrule band with silver highlight
        Rectangle ferrule = new(center.X - 5.5f * scale, center.Y - 16.5f * scale, 11f * scale, 4.5f * scale);
        Raylib.DrawRectangleRec(ferrule, new Color(42, 44, 52, 255));
        Raylib.DrawRectangleLinesEx(ferrule, 1.2f * scale, HanoiPalette.CartoonOutline);
        Raylib.DrawLineEx(new Vector2(ferrule.X + 1f * scale, ferrule.Y + 2.2f * scale), new Vector2(ferrule.X + ferrule.Width - 1f * scale, ferrule.Y + 2.2f * scale), 1.0f * scale, HanoiPalette.ButChiFerrule);
        Raylib.DrawLineEx(new Vector2(center.X + 2f * scale, ferrule.Y), new Vector2(center.X + 2f * scale, ferrule.Y + ferrule.Height), 1.0f * scale, Raylib.ColorAlpha(Color.White, 0.75f));

        // Rounded pink eraser
        Rectangle eraser = new(center.X - 5.5f * scale, center.Y - 22f * scale, 11f * scale, 5.5f * scale);
        Raylib.DrawRectangleRounded(eraser, 0.45f, 6, HanoiPalette.ButChiEraserPink);
        Raylib.DrawRectangleRoundedLinesEx(eraser, 0.45f, 6, 1.2f * scale, HanoiPalette.CartoonOutline);

        // Sharpened wooden collar & graphite tip
        Vector2 tipApex = new(center.X, center.Y + 20f * scale);
        Vector2 tipLeft = new(center.X - 5.5f * scale, center.Y + 10f * scale);
        Vector2 tipRight = new(center.X + 5.5f * scale, center.Y + 10f * scale);
        Raylib.DrawTriangle(tipRight, tipApex, tipLeft, HanoiPalette.ButChiWoodCollar);
        Raylib.DrawTriangleLines(tipRight, tipApex, tipLeft, HanoiPalette.CartoonOutline);

        // Graphite tip
        Vector2 graphLeft = new(center.X - 2.8f * scale, center.Y + 14.5f * scale);
        Vector2 graphRight = new(center.X + 2.8f * scale, center.Y + 14.5f * scale);
        Raylib.DrawTriangle(graphRight, tipApex, graphLeft, HanoiPalette.ButChiGraphite);

        // Brilliant glowing cyan magical orb below tip
        Vector2 orbCenter = tipApex + new Vector2(0f, 6.0f * scale);
        float sparkPulse = 1.0f + 0.18f * MathF.Sin(animTime * 6f);
        Raylib.DrawCircleV(orbCenter, 10.0f * scale * sparkPulse, Raylib.ColorAlpha(HanoiPalette.ButChiMagicCyan, 0.18f));
        Raylib.DrawCircleV(orbCenter, 6.0f * scale * sparkPulse, Raylib.ColorAlpha(HanoiPalette.ButChiMagicCyan, 0.45f));
        Raylib.DrawCircleV(orbCenter, 3.8f * scale * sparkPulse, HanoiPalette.ButChiMagicCyan);
        Raylib.DrawCircleV(orbCenter, 1.6f * scale, Color.White);

        // Dynamic swirling magic energy vortex rings
        Raylib.DrawRing(orbCenter, 7.5f * scale, 9.5f * scale, animTime * 140f, animTime * 140f + 250f, 24, Raylib.ColorAlpha(HanoiPalette.ButChiMagicCyan, 0.55f));
        Raylib.DrawRing(orbCenter, 9.5f * scale, 11.0f * scale, -animTime * 110f, -animTime * 110f + 210f, 24, Raylib.ColorAlpha(HanoiPalette.ButChiMagicCyan, 0.35f));

        // Procedural 4-point magic cross sparkles (+)
        DrawMagicCrossSparkle(new Vector2(center.X + 16.5f * scale, center.Y + 4.0f * scale), 4.8f * scale, HanoiPalette.ButChiMagicCyan);
        DrawMagicCrossSparkle(new Vector2(orbCenter.X + 10.0f * scale, orbCenter.Y - 5.0f * scale), 3.2f * scale, HanoiPalette.ButChiMagicCyan);
        DrawMagicCrossSparkle(new Vector2(orbCenter.X - 9.5f * scale, orbCenter.Y + 7.0f * scale), 2.6f * scale, HanoiPalette.ButChiMagicCyan);
    }

    private static void DrawMagicCrossSparkle(Vector2 center, float size, Color color)
    {
        Raylib.DrawLineEx(new Vector2(center.X - size, center.Y), new Vector2(center.X + size, center.Y), 1.6f, color);
        Raylib.DrawLineEx(new Vector2(center.X, center.Y - size), new Vector2(center.X, center.Y + size), 1.6f, color);
        Raylib.DrawCircleV(center, 0.9f, Color.White);
    }

    public static void DrawBongXam(Vector2 centerPos, float animTime, float scale = 1.0f)
    {
        if (!Raylib.IsWindowReady()) return;

        // Heavy somber floating motion
        float hoverY = MathF.Sin(animTime * 1.8f) * 5.0f * scale;
        Vector2 pos = new(centerPos.X, centerPos.Y + hoverY);

        // Ground shadow
        float shadowAlpha = Math.Clamp(0.30f - (hoverY / (40f * scale)), 0.15f, 0.40f);
        Raylib.DrawEllipse((int)centerPos.X, (int)(centerPos.Y + 28f * scale), 22f * scale, 8f * scale, Raylib.ColorAlpha(Color.Black, shadowAlpha));

        // 5 storm violet puffs
        (Vector2 Offset, float Radius)[] puffs =
        [
            (new Vector2(0f, 0f), 17.0f * scale),
            (new Vector2(-14.0f * scale, 2.0f * scale), 13.0f * scale),
            (new Vector2(+14.0f * scale, 2.0f * scale), 13.0f * scale),
            (new Vector2(-8.0f * scale, -10.0f * scale), 11.0f * scale),
            (new Vector2(+8.0f * scale, -9.0f * scale), 12.0f * scale)
        ];

        // Pass 1: Dark outline silhouette
        for (int i = 0; i < puffs.Length; i++)
        {
            Raylib.DrawCircleV(pos + puffs[i].Offset, puffs[i].Radius + 1.8f * scale, HanoiPalette.BongXamOutline);
        }

        // Pass 2: Deep storm shadow puffs
        Vector2 shadowShift = new(0f, 3.0f * scale);
        for (int i = 0; i < puffs.Length; i++)
        {
            Raylib.DrawCircleV(pos + puffs[i].Offset + shadowShift, puffs[i].Radius, HanoiPalette.BongXamStormShadow);
        }

        // Pass 3: Storm cloud body puffs
        for (int i = 0; i < puffs.Length; i++)
        {
            Raylib.DrawCircleV(pos + puffs[i].Offset, puffs[i].Radius, HanoiPalette.BongXamStormBody);
        }

        // Pass 4: Misty lavender highlight puffs
        for (int i = 0; i < puffs.Length; i++)
        {
            Raylib.DrawCircleV(pos + puffs[i].Offset + new Vector2(0f, -2.5f * scale), puffs[i].Radius * 0.7f, Raylib.ColorAlpha(HanoiPalette.BongXamHighlight, 0.45f));
        }

        // Soulful sad expressive eyes with teary gloss
        Vector2 leftEye = new(pos.X - 6.0f * scale, pos.Y - 2.0f * scale);
        Vector2 rightEye = new(pos.X + 6.0f * scale, pos.Y - 2.0f * scale);

        // Sad drooping brow shadows
        Raylib.DrawLineEx(new Vector2(leftEye.X - 3.5f * scale, leftEye.Y - 4.5f * scale), new Vector2(leftEye.X + 2.5f * scale, leftEye.Y - 3.0f * scale), 1.5f * scale, HanoiPalette.BongXamOutline);
        Raylib.DrawLineEx(new Vector2(rightEye.X + 3.5f * scale, rightEye.Y - 4.5f * scale), new Vector2(rightEye.X - 2.5f * scale, rightEye.Y - 3.0f * scale), 1.5f * scale, HanoiPalette.BongXamOutline);

        // Deep dark pupils
        Raylib.DrawCircleV(leftEye, 2.8f * scale, HanoiPalette.BongXamOutline);
        Raylib.DrawCircleV(rightEye, 2.8f * scale, HanoiPalette.BongXamOutline);

        // Tear sheen at bottom of eyes
        Raylib.DrawCircleSector(new Vector2(leftEye.X, leftEye.Y + 0.8f * scale), 2.0f * scale, 0f, 180f, 8, Raylib.ColorAlpha(new Color(190, 215, 255, 255), 0.8f));
        Raylib.DrawCircleSector(new Vector2(rightEye.X, rightEye.Y + 0.8f * scale), 2.0f * scale, 0f, 180f, 8, Raylib.ColorAlpha(new Color(190, 215, 255, 255), 0.8f));

        // Small glimmer
        Raylib.DrawCircleV(new Vector2(leftEye.X - 0.8f * scale, leftEye.Y - 0.8f * scale), 0.8f * scale, Color.White);
        Raylib.DrawCircleV(new Vector2(rightEye.X - 0.8f * scale, rightEye.Y - 0.8f * scale), 0.8f * scale, Color.White);

        // Sad mouth
        Vector2 mouthCenter = new(pos.X, pos.Y + 5.5f * scale);
        Raylib.DrawCircleSector(mouthCenter, 2.0f * scale, 180f, 360f, 8, HanoiPalette.BongXamOutline);

        // Falling mist droplets
        for (int k = 0; k < 4; k++)
        {
            float dropCycle = (animTime * 1.4f + k * 0.35f) % 1.0f;
            float dropX = pos.X + ((k - 1.5f) * 8.5f * scale);
            float dropY = pos.Y + (13.0f * scale) + dropCycle * (22.0f * scale);
            float dropAlpha = (1.0f - dropCycle) * 0.7f;
            Raylib.DrawEllipse((int)dropX, (int)dropY, 1.2f * scale, 2.6f * scale, Raylib.ColorAlpha(HanoiPalette.BongXamHighlight, dropAlpha));
        }
    }

    // =========================================================================
    // Authentic Pixel Art Stall Sprites (Ba An Pho, Bac Tu Beef, Co Lan Veggie)
    // =========================================================================

    private static Texture2D _texBaAnPho;
    private static bool _texBaAnPhoLoaded;
    private static Texture2D _texBacTuBeef;
    private static bool _texBacTuBeefLoaded;
    private static Texture2D _texCoLanVeggie;
    private static bool _texCoLanVeggieLoaded;

    public static string ResolveTexturePath(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return string.Empty;

        try
        {
            if (filename.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || filename.Contains('\0'))
            {
                return string.Empty;
            }

            if (File.Exists(filename)) return Path.GetFullPath(filename);

            string cleanName = Path.GetFileName(filename);
            if (string.IsNullOrEmpty(cleanName) || cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return string.Empty;
            }

            string[] candidates =
            [
                Path.Combine("Data", cleanName),
                Path.Combine("Data", "Sprites", cleanName),
                Path.Combine("Source", "Data", cleanName),
                Path.Combine("Source", "Data", "Sprites", cleanName),
                Path.Combine(AppContext.BaseDirectory, "Data", cleanName),
                Path.Combine(AppContext.BaseDirectory, "Data", "Sprites", cleanName),
                Path.Combine(AppContext.BaseDirectory, cleanName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", cleanName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Source", "Data", cleanName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "Sprites", cleanName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Data", cleanName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Source", "Data", cleanName),
                filename
            ];
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return Path.GetFullPath(c);
            }
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Texture2D GetOrCreateStallTexture(ref Texture2D texture, ref bool loaded, string filename)
    {
        if (!loaded && Raylib.IsWindowReady())
        {
            try
            {
                string path = ResolveTexturePath(filename);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    texture = Raylib.LoadTexture(path);
                    if (texture.Id > 0)
                    {
                        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
                    }
                }
            }
            catch
            {
                // Fall back gracefully to procedural vector rendering if load throws
            }
            loaded = true;
        }
        return texture;
    }

    public static void UnloadStallTextures()
    {
        if (_texBaAnPhoLoaded)
        {
            if (_texBaAnPho.Id > 0 && Raylib.IsWindowReady())
            {
                try { Raylib.UnloadTexture(_texBaAnPho); } catch { }
            }
            _texBaAnPho = default;
            _texBaAnPhoLoaded = false;
        }
        if (_texBacTuBeefLoaded)
        {
            if (_texBacTuBeef.Id > 0 && Raylib.IsWindowReady())
            {
                try { Raylib.UnloadTexture(_texBacTuBeef); } catch { }
            }
            _texBacTuBeef = default;
            _texBacTuBeefLoaded = false;
        }
        if (_texCoLanVeggieLoaded)
        {
            if (_texCoLanVeggie.Id > 0 && Raylib.IsWindowReady())
            {
                try { Raylib.UnloadTexture(_texCoLanVeggie); } catch { }
            }
            _texCoLanVeggie = default;
            _texCoLanVeggieLoaded = false;
        }
    }

    public static void DrawStallSprite(Texture2D texture, Vector2 pos, float scale = 1.0f, bool withShadow = true)
    {
        if (!Raylib.IsWindowReady() || texture.Id == 0 || texture.Width <= 0 || texture.Height <= 0 ||
            scale <= 0f || !float.IsFinite(scale) || !float.IsFinite(pos.X) || !float.IsFinite(pos.Y)) return;

        // Ground shadow
        if (withShadow)
        {
            Raylib.DrawEllipse((int)pos.X, (int)pos.Y, 56f * scale, 12f * scale, Raylib.ColorAlpha(Color.Black, 0.28f));
        }

        float renderWidth = 124f * scale;
        float renderHeight = renderWidth * ((float)texture.Height / texture.Width);

        Rectangle srcRec = new(0f, 0f, texture.Width, texture.Height);
        Rectangle destRec = new(pos.X - renderWidth * 0.5f, pos.Y - renderHeight, renderWidth, renderHeight);

        Raylib.DrawTexturePro(texture, srcRec, destRec, Vector2.Zero, 0.0f, Color.White);
    }

    public static void DrawBaAnPhoStall(Vector2 pos, string stallName, float animTime)
    {
        DrawBaAnPhoStall(pos, stallName, animTime, 1.0f, true);
    }

    public static void DrawBaAnPhoStall(Vector2 pos, string stallName, float animTime, float scale)
    {
        DrawBaAnPhoStall(pos, stallName, animTime, scale, true);
    }

    public static void DrawBaAnPhoStall(Vector2 pos, string stallName, float animTime, float scale, bool withShadow)
    {
        if (!Raylib.IsWindowReady()) return;
        var tex = GetOrCreateStallTexture(ref _texBaAnPho, ref _texBaAnPhoLoaded, "stall_baan_pho.png");
        if (tex.Id > 0)
        {
            DrawStallSprite(tex, pos, scale, withShadow);
        }
        else
        {
            DrawMarketStall(pos, stallName, HanoiPalette.TerracottaRoof, HanoiPalette.WallYellow, scale, withShadow);
        }
    }

    public static void DrawBacTuBeefStall(Vector2 pos, string stallName, float animTime)
    {
        DrawBacTuBeefStall(pos, stallName, animTime, 1.0f, true);
    }

    public static void DrawBacTuBeefStall(Vector2 pos, string stallName, float animTime, float scale)
    {
        DrawBacTuBeefStall(pos, stallName, animTime, scale, true);
    }

    public static void DrawBacTuBeefStall(Vector2 pos, string stallName, float animTime, float scale, bool withShadow)
    {
        if (!Raylib.IsWindowReady()) return;
        var tex = GetOrCreateStallTexture(ref _texBacTuBeef, ref _texBacTuBeefLoaded, "stall_bactu_beef.png");
        if (tex.Id > 0)
        {
            DrawStallSprite(tex, pos, scale, withShadow);
        }
        else
        {
            DrawMarketStall(pos, stallName, HanoiPalette.ShutterGreen, HanoiPalette.WallYellowLight, scale, withShadow);
        }
    }

    public static void DrawCoLanVeggieStall(Vector2 pos, string stallName, float animTime)
    {
        DrawCoLanVeggieStall(pos, stallName, animTime, 1.0f, true);
    }

    public static void DrawCoLanVeggieStall(Vector2 pos, string stallName, float animTime, float scale)
    {
        DrawCoLanVeggieStall(pos, stallName, animTime, scale, true);
    }

    public static void DrawCoLanVeggieStall(Vector2 pos, string stallName, float animTime, float scale, bool withShadow)
    {
        if (!Raylib.IsWindowReady()) return;
        var tex = GetOrCreateStallTexture(ref _texCoLanVeggie, ref _texCoLanVeggieLoaded, "stall_colan_veggie.png");
        if (tex.Id > 0)
        {
            DrawStallSprite(tex, pos, scale, withShadow);
        }
        else
        {
            DrawMarketStall(pos, stallName, HanoiPalette.TerracottaRoof, HanoiPalette.WallYellowLight, scale, withShadow);
        }
    }

    public static void DrawFlowerBicycle(Vector2 pos, float animTime, float scale = 1.0f)
    {
        if (!Raylib.IsWindowReady()) return;
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
        if (!Raylib.IsWindowReady()) return;
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
        if (!Raylib.IsWindowReady()) return;
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
        DrawMarketStall(pos, stallName, awning1, awning2, scale, true);
    }

    public static void DrawMarketStall(Vector2 pos, string stallName, Color awning1, Color awning2, float scale, bool withShadow)
    {
        if (!Raylib.IsWindowReady() || scale <= 0f || !float.IsFinite(scale) || !float.IsFinite(pos.X) || !float.IsFinite(pos.Y)) return;
        // Ground shadow
        if (withShadow)
        {
            Raylib.DrawEllipse((int)pos.X, (int)pos.Y, 50f * scale, 14f * scale, Raylib.ColorAlpha(Color.Black, 0.26f));
        }

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
        if (!Raylib.IsWindowReady()) return;
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
        if (!Raylib.IsWindowReady()) return;
        float half = size * 0.5f;
        Raylib.DrawLineEx(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half), thickness, color);
        Raylib.DrawLineEx(new Vector2(center.X + half, center.Y - half), new Vector2(center.X - half, center.Y + half), thickness, color);
    }

    public static void DrawCheckmark(Vector2 center, float size, Color color, float thickness = 2.0f)
    {
        if (!Raylib.IsWindowReady()) return;
        Vector2 start = new(center.X - size * 0.45f, center.Y);
        Vector2 mid = new(center.X - size * 0.1f, center.Y + size * 0.35f);
        Vector2 end = new(center.X + size * 0.45f, center.Y - size * 0.45f);
        Raylib.DrawLineEx(start, mid, thickness, color);
        Raylib.DrawLineEx(mid, end, thickness, color);
    }

    public static void DrawFivePointStar(Vector2 center, float radius, Color fillColor, Color? outlineColor = null, float outlineWidth = 1.5f)
    {
        if (!Raylib.IsWindowReady()) return;
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
        if (!Raylib.IsWindowReady()) return;
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

