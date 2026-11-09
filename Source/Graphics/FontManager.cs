using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Font management service with full Vietnamese Unicode glyph support (762 codepoints)
/// and multi-scale font atlas caching (Header 36px, Title 24px, Body 20px, Small 14px).
/// </summary>
public sealed class FontManager : IDisposable
{
    private static FontManager? _instance;
    public static FontManager Instance => _instance ??= new FontManager();

    public Font HeaderFont { get; private set; } // 36px
    public Font TitleFont { get; private set; }  // 24px
    public Font BodyFont { get; private set; }   // 20px
    public Font SmallFont { get; private set; }  // 14px

    public bool IsLoaded { get; private set; }
    private readonly List<Font> _loadedFonts = new();

    /// <summary>
    /// Normalizes Unicode string to Form C (canonical composition) to ensure decomposed NFD
    /// characters (e.g. from macOS/clipboard) resolve to precomposed Vietnamese font glyphs.
    /// </summary>
    public static string NormalizeText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Generates exact 762 codepoints required for flawless Vietnamese UTF-8 rendering.
    /// </summary>
    public static int[] GetVietnameseCodepoints()
    {
        var list = new List<int>(762);

        // 1. Basic Latin, Latin-1 Supplement, Latin Extended-A & B (32 to 591: 560 codepoints)
        for (int cp = 0x0020; cp <= 0x024F; cp++) list.Add(cp);

        // 2. Latin Extended Additional (Vietnamese compound diacritics: 7840 to 7929: 90 codepoints)
        for (int cp = 0x1EA0; cp <= 0x1EF9; cp++) list.Add(cp);

        // 3. Typographic punctuation, dashes, quotes (0x2000 to 0x206E: 111 codepoints)
        for (int cp = 0x2000; cp <= 0x206E; cp++) list.Add(cp);

        // 4. Vietnamese currency symbol ₫ (0x20AB: 1 codepoint)
        list.Add(0x20AB);

        return list.ToArray();
    }

    public void Initialize()
    {
        if (IsLoaded) return;

        string? fontPath = ResolveFontPath();
        int[] codepoints = GetVietnameseCodepoints();

        if (fontPath != null && File.Exists(fontPath) && Raylib.IsWindowReady())
        {
            try
            {
                HeaderFont = LoadFontWithFilter(fontPath, 36, codepoints);
                TitleFont = LoadFontWithFilter(fontPath, 24, codepoints);
                BodyFont = LoadFontWithFilter(fontPath, 20, codepoints);
                SmallFont = LoadFontWithFilter(fontPath, 14, codepoints);
                IsLoaded = true;
                return;
            }
            catch
            {
                // Fallback to default if load fails
            }
        }

        if (Raylib.IsWindowReady())
        {
            Font def = Raylib.GetFontDefault();
            HeaderFont = TitleFont = BodyFont = SmallFont = def;
        }
        IsLoaded = false;
    }

    private Font LoadFontWithFilter(string path, int size, int[] codepoints)
    {
        Font font = Raylib.LoadFontEx(path, size, codepoints, codepoints.Length);
        Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        _loadedFonts.Add(font);
        return font;
    }

    private static string? ResolveFontPath()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Data", "Fonts", "Inter-Medium.ttf"),
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/Library/Fonts/Arial Unicode.ttf",
            "/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf",
            "/System/Library/Fonts/Supplemental/Verdana.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "C:\\Windows\\Fonts\\arial.ttf"
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    public void DrawText(string text, Vector2 pos, float size, Color color, Font? font = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        string normalized = NormalizeText(text);
        Font activeFont = font ?? SelectFontBySize(size);
        Raylib.DrawTextEx(activeFont, normalized, pos, size, 1.0f, color);
    }

    public void DrawTextOutline(string text, Vector2 pos, float size, Color color, Color outlineColor, float outlineThick = 2.0f, Font? font = null)
    {
        if (string.IsNullOrEmpty(text)) return;
        string normalized = NormalizeText(text);
        Font activeFont = font ?? SelectFontBySize(size);
        // 8-directional shadow stamping for crisp comic outline
        Vector2[] offsets =
        [
            new(-outlineThick, 0f), new(outlineThick, 0f),
            new(0f, -outlineThick), new(0f, outlineThick),
            new(-outlineThick, -outlineThick), new(outlineThick, -outlineThick),
            new(-outlineThick, outlineThick), new(outlineThick, outlineThick)
        ];

        for (int i = 0; i < offsets.Length; i++)
        {
            Raylib.DrawTextEx(activeFont, normalized, pos + offsets[i], size, 1.0f, outlineColor);
        }
        Raylib.DrawTextEx(activeFont, normalized, pos, size, 1.0f, color);
    }

    public Vector2 MeasureText(string text, float size, Font? font = null)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        string normalized = NormalizeText(text);
        Font activeFont = font ?? SelectFontBySize(size);
        return Raylib.MeasureTextEx(activeFont, normalized, size, 1.0f);
    }

    public Font SelectFontBySize(float size)
    {
        if (size >= 30f) return HeaderFont;
        if (size >= 22f) return TitleFont;
        if (size >= 16f) return BodyFont;
        return SmallFont;
    }

    public void Dispose()
    {
        for (int i = 0; i < _loadedFonts.Count; i++)
        {
            Raylib.UnloadFont(_loadedFonts[i]);
        }
        _loadedFonts.Clear();
        IsLoaded = false;
        _instance = null;
    }
}
