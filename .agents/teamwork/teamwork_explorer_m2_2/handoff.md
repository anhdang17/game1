# Handoff Report: Visual Design, Vector Rendering & Font Manager (Milestone 2)

**Agent**: `teamwork_explorer_m2_2`  
**Milestone**: M2 — Hanoi Exploration & Quest 1 (Phở Gathering)  
**Target Recipient**: Worker (`teamwork_worker_m2_1` / Orchestrator)  
**Date**: 2026-11-08  

---

## 1. Observation

### 1.1 Project Structure & Raylib Configuration
- `GameProject.csproj` specifies `.NET 10.0`, `Raylib-cs` version `8.1.0`, and `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`.
- `GameProject.Tests.csproj` runs `xUnit 2.9.2` headlessly in ~93 ms (192 tests passing). Pure C# domain logic in `Source/Core/` is completely separated from presentation in `Source/Graphics/`, `Source/UI/`, `Source/Stages/`.
- `Source/Graphics/` is designated in `PROJECT.md` (lines 26–29) for:
  - `VectorRenderer.cs` (Feature 27 & cartoon presentation)
  - `FontManager.cs` (Vietnamese UTF-8 TTF font loader)
  - `ColorBloomEffect.cs` (Features 2, 12, 16 color restoration ripple effect)

### 1.2 Raylib-cs 8.1.0 API Signatures (Verified from Nuget XML Docs)
Inspected `/Users/mac/.nuget/packages/raylib-cs/8.1.0/lib/net10.0/Raylib-cs.xml`:
1. **Font Loading**:
   ```csharp
   Raylib.LoadFontEx(string fileName, int fontSize, int[]? fontChars, int glyphCount)
   Raylib.LoadFontFromMemory(string fileType, byte[] fileData, int fontSize, int[]? fontChars, int glyphCount)
   Raylib.DrawTextEx(Font font, string text, Vector2 position, float fontSize, float spacing, Color tint)
   Raylib.MeasureTextEx(Font font, string text, float fontSize, float spacing)
   Raylib.SetTextureFilter(Texture2D texture, TextureFilter filter)
   ```
2. **Vector Shapes**:
   ```csharp
   Raylib.DrawRectangleRounded(Rectangle rec, float roundness, int segments, Color color)
   Raylib.DrawRectangleRoundedLinesEx(Rectangle rec, float roundness, int segments, float lineThick, Color color)
   Raylib.DrawCircleV(Vector2 center, float radius, Color color)
   Raylib.DrawCircleSector(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color)
   Raylib.DrawRing(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color)
   Raylib.DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color) // Counter-clockwise order
   Raylib.DrawLineEx(Vector2 startPos, Vector2 endPos, float thick, Color color)
   Raylib.DrawLineBezier(Vector2 startPos, Vector2 endPos, float thick, Color color)
   Raylib.ColorAlpha(Color color, float alpha)
   ```
3. **Render Texture & Shaders**:
   ```csharp
   Raylib.LoadRenderTexture(int width, int height)
   Raylib.BeginTextureMode(RenderTexture2D target)
   Raylib.EndTextureMode()
   Raylib.LoadShaderFromMemory(string? vsCode, string? fsCode)
   Raylib.GetShaderLocation(Shader shader, string uniformName)
   Raylib.SetShaderValue<T>(Shader shader, int locIndex, T value, ShaderUniformDataType uniformType)
   Raylib.BeginShaderMode(Shader shader)
   Raylib.EndShaderMode()
   Raylib.IsShaderValid(Shader shader)
   ```

### 1.3 System Font Availability on macOS
Inspected local macOS environment:
- `/System/Library/Fonts/Supplemental/Arial.ttf` (773 KB — contains full Latin Extended and Vietnamese Unicode ranges).
- `/Library/Fonts/Arial Unicode.ttf` (23.2 MB — universal glyph support).
- `/System/Library/Fonts/Supplemental/Verdana.ttf` and `Arial Rounded Bold.ttf` are present.

### 1.4 Milestone 2 Domain Events and State Coupling
- `Source/Core/Events/GameEvents.cs`:
  - `ColorRestoredEvent(string RegionId, float OldPercentage, float NewPercentage, string SourceTrigger)`
  - `RegionColorUpdatedEvent(string RegionId, float NewPercentage)`
  - `QuestProgressUpdatedEvent`, `ItemCollectedEvent`
- `Source/Core/Models/RegionProgress.cs`:
  - `ColorPercent`: Float in range `[0.0f, 100.0f]`. Default start for Hanoi = `0.0f`.
- `Source/Core/Puzzles/SpatialGridEngine.cs`:
  - `Direction`: Enum with values `Up`, `Down`, `Left`, `Right`.
- `Source/Core/Quests/HanoiQuests.cs`:
  - Ingredient IDs: `Item_BanhPho`, `Item_ThitBo`, `Item_NuocDung`, `Item_HanhLa`, `Item_ThaoMoc`.

---

## 2. Logic Chain

### 2.1 The Visual Identity: Hanoi Old Quarter Flat Cartoon
1. **Art Style Rationale**:
   The prompt mandates a *flat cartoon style with vibrant colors and rounded shapes*. Procedural vector rendering directly in Raylib primitives (`DrawRectangleRounded`, `DrawCircleV`, `DrawTriangle`, `DrawRing`) fulfills this mandate without external PNG asset dependencies, preventing texture missing errors and enabling crisp scaling to 4K.
2. **Color Palette Mapping**:
   The architectural palette must encapsulate French colonial patina, Vietnamese vernacular materials, and lively street-food culture:
   - **Ochre Yellow Walls (`#E5B64C`)**: Iconic warm golden yellow of Hanoi French colonial villas and shophouses. In shade: `#C79738`; highlight: `#F6D378`.
   - **Moss Green Shutters (`#2E5A44`)**: Louvre wooden windows that flank yellow plaster. Dark slat shadow: `#1D3D2D`; trim highlight: `#3E7B5C`.
   - **Terracotta Red Roof Tiles (`#C84C32`)**: Weathered "ngói âm dương" curved clay tiles with dark ridge shadows (`#963520`).
   - **Dark Rusted Iron (`#382C29` / `#6B4235`)**: French wrought iron balconies, street lampposts, and Long Biên Bridge steel girders.
   - **Warm Stone Streets (`#A39A8E`)**: Flagstone and granite paving cobbles with kerbstone borders (`#80776D`) and mortar lines (`#5E564F`).
   - **Blue Plastic Street Tables (`#1E88E5`)**: The quintessential Vietnamese street food table (Song Long plastic blue), paired with red plastic stools (`#E53935`) or low wooden stools (`#8D5B3A`).
   - **Lotus Bicycle (`#F48FB1`, `#43A047`, `#2B4C48`)**: A vintage dark green Thống Nhất bicycle carrying two twin woven bamboo panniers (`#D7B476`) overflowing with lotus blossoms and wide circular green leaves.
   - **Vintage Cyclo (`#4E5D52`, `#8B261E`, `#2C3539`, `#D2D7DF`)**: Traditional three-wheel cyclo with olive canopy hood, crimson cushioned seat, and silver spoked wheels.

### 2.2 Character & Companion Visual Design
1. **Bé Mây**:
   - Conical leaf hat (*Nón Lá*): Straw cream (`#F5E5C9`) with circular concentric stitching lines and a pastel chin ribbon (`#FF8A80`).
   - Outfit: Cheerful Sky Blue pastel tunic (*Áo Bà Ba*) (`#81D4FA`) with rounded hems and white collar (`#FFFFFF`), cropped charcoal linen trousers (`#455A64`), and soft cloth slippers.
   - Accessories: Cross-body satchel carrying the Memory Journal (*Cuốn Sổ Ký Ức*) in warm ochre leather (`#FFA000`).
   - Animation: Foot bounce walk cycle with arm sway and directional hat orientation (when facing Up/Away, nón lá covers back; when facing Down, hat reveals face with cartoon blush cheeks `#FFAB91`).
2. **Bút Chì (The Floating Companion)**:
   - Floating companion sprite hovering above/behind Bé Mây.
   - Body: Hexagonal pencil shaft in bright sunny yellow (`#FFD54F`), sharpened birch collar (`#FFE0B2`), soft charcoal graphite tip (`#263238`) emitting a glowing cyan magical sparkle (`#00E5FF`).
   - Top: Salmon pink eraser (`#FF8A80`) with metallic silver ferrule collar (`#CFD8DC`).
   - Animation: Harmonic vertical hovering sine wave, slight tilt sway, expressive blinking cartoon eyes, and dynamic ground shadow that scales inversely with hover altitude.

### 2.3 Vector Rendering Engine (`VectorRenderer.cs`)
1. **Drop Shadows**:
   Cartoon depth is achieved by drawing an offset silhouette (`x + 3`, `y + 4`) with `Raylib.ColorAlpha(BLACK, 0.22f)` before drawing the main shape.
2. **Cartoon Outlines**:
   Every shape has a distinct, darker outline (1.5–2.5 px) using `DrawRectangleRoundedLinesEx` or `DrawCircleLines` or contrasting darker border color.
3. **Compound Procedural Prefabs**:
   Rather than loading sprite sheets, Hanoi elements are drawn via modular procedural routines parameterized by `(Vector2 position, float scale, float animTime)`:
   - `DrawBeMay(...)`
   - `DrawButChi(...)`
   - `DrawFlowerBicycle(...)`
   - `DrawVintageCyclo(...)`
   - `DrawPhoStall(...)`, `DrawBeefStall(...)`, `DrawHerbStall(...)`, `DrawSpiceStall(...)`
   - `DrawBluePlasticTableAndStools(...)`
   - `DrawVendorSign(...)`
   - `DrawInteractionPrompt(...)`

### 2.4 Vietnamese Font Management (`FontManager.cs`)
1. **The Unicode Problem**:
   Standard Raylib `LoadFont` only creates texture glyphs for ASCII `32..126`. Vietnamese requires accented vowels in Latin Extended Additional (`0x1EA0 - 0x1EF9`) such as `ở, ỗ, ế, ặ, ủ, ỹ, ₫`. Without explicit codepoints, all Vietnamese text appears as `?` or missing glyphs.
2. **The Exact Codepoint Table**:
   `FontManager` builds a unified codepoint array containing:
   - Range 1: `0x0020 - 0x024F` (560 codepoints): Basic Latin, Latin-1 Supplement, Latin Extended-A & B (covers `Đ, đ, Ơ, ơ, Ư, ư`, circumflexes, carons).
   - Range 2: `0x1EA0 - 0x1EF9` (90 codepoints): Latin Extended Additional (all Vietnamese acute, grave, hook, tilde, and dot-below compound vowels).
   - Range 3: `0x2000 - 0x206F` (112 codepoints): Typographic quotes, dashes, ellipsis, and the Vietnamese currency symbol `₫` (`0x20AB`).
   - Total: 762 codepoints. This generates a compact ~512×512 or 1024×1024 texture atlas.
3. **Multi-Scale Font Hierarchy**:
   To ensure crisp rendering without GPU texture resampling blur, `FontManager` generates distinct font sizes:
   - `HeaderFont`: 36px (screen titles, level victory banners)
   - `TitleFont`: 24px (shop signs, quest headers, dialogue speaker names)
   - `BodyFont`: 20px (dialogue body text, hint text)
   - `SmallFont`: 14px (item counts, HUD mini-labels)
4. **Cross-Platform Fallback Strategy**:
   `FontManager.Initialize()` probes candidate font paths in order:
   - Project directory: `Data/Fonts/Inter-Medium.ttf` or `Source/Data/Fonts/...`
   - macOS: `/System/Library/Fonts/Supplemental/Arial.ttf`, `/Library/Fonts/Arial Unicode.ttf`, `/System/Library/Fonts/Supplemental/Arial Rounded Bold.ttf`
   - Linux: `/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf`, `/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf`
   - Windows: `C:\Windows\Fonts\arial.ttf`
   - Safe Fallback: `Raylib.GetFontDefault()` if no TTF file is found.

### 2.5 Color Desaturation vs Color Bloom Presentation (`ColorBloomEffect.cs`)
1. **Narrative & Aesthetic Mechanic**:
   - The game begins in a desaturated, monochrome Hanoi (0% color) caused by the Gray Shadow (*Bóng Xám*).
   - As Bé Mây completes quests (collecting ingredients, cooking Phở, opening Long Biên), color returns in stages:
     - Stage 0: 0% (Monochrome/sepia)
     - Stage 1: 33.3% (Phở Gathering / Grandma An dialogue completed)
     - Stage 2: 66.6% (Phở Bowl Assembly completed)
     - Stage 3: 100.0% (Long Biên cleared and bridge restored)
2. **GPU Full-Screen Post-Processing Shader (Primary)**:
   - Render the game world to a `RenderTexture2D` framebuffer.
   - Render the texture to screen using a custom GLSL 330 Fragment Shader (`desaturation_bloom.fs`).
   - The shader converts RGB to perceptual luminance using Rec. 709 weights `vec3(0.2126, 0.7152, 0.0722)`.
   - The shader receives `u_ColorPercent`, `u_BloomCenter`, `u_BloomRadius`, `u_BloomIntensity`.
   - When a milestone triggers, `u_BloomRadius` animates outward from the quest origin in a circular shockwave.
   - At the wave crest, a warm chromatic bloom glow (`vec3(1.1, 0.95, 0.70)`) flashes, trailing into vibrant color.
3. **CPU Fallback Palette Desaturation (Zero-Driver Resilience)**:
   - If shaders are unavailable or running in headless/mock mode, `HanoiPalette.ApplyDesaturation(Color, float percent)` provides exact linear interpolation between gray luminance and vibrant RGB:
     ```csharp
     float lum = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
     byte r = (byte)(lum + (c.R - lum) * (percent / 100f));
     byte g = (byte)(lum + (c.G - lum) * (percent / 100f));
     byte b = (byte)(lum + (c.B - lum) * (percent / 100f));
     return new Color(r, g, b, c.A);
     ```

---

## 3. Caveats

1. **System Font Resolution on Headless Servers / CI**:
   On macOS, `/System/Library/Fonts/Supplemental/Arial.ttf` is always present. On a stripped Linux CI container or Windows build machine, system font paths differ. `FontManager` must therefore support the search-order fallback chain and gracefully fallback to default font rather than throwing `FileNotFoundException`.
2. **OpenGL Context Lifecycles**:
   Raylib font loading (`LoadFontEx`) and shader compilation (`LoadShaderFromMemory`) require an active window and OpenGL context (`Raylib.InitWindow`). They MUST NOT be invoked in headless test suites (`GameProject.Tests`). Domain tests must remain 100% headless.
3. **RenderTexture Y-Flipping in Raylib**:
   In OpenGL / Raylib, drawing a `RenderTexture2D` to screen requires drawing with negative height (`sourceRec.Height = -renderTexture.Texture.Height`) because OpenGL framebuffers are vertically inverted relative to screen coordinates. `ColorBloomEffect.cs` must invert the Y axis in `sourceRec`.

---

## 4. Conclusion & Implementation Strategy for Worker

The visual layer for Milestone 2 should be implemented across four key classes in `Source/Graphics/`:
1. `Source/Graphics/HanoiPalette.cs`: Static color registry, hex constants, and desaturation math.
2. `Source/Graphics/FontManager.cs`: Robust Vietnamese TTF font loader caching 4 sizes (36, 24, 20, 14 px) with 762 codepoints, text measurement, and cartoon outline rendering.
3. `Source/Graphics/VectorRenderer.cs`: Comprehensive procedural vector drawing library with primitives and Hanoi cultural prefabs (Bé Mây, Bút Chì, Flower Bicycle, Cyclo, Stalls, Tables, Signs, Prompts).
4. `Source/Graphics/ColorBloomEffect.cs`: Dual-mode color restoration engine featuring GLSL 330 ripple bloom shader and CPU fallback.

### Detailed Class Signatures & Implementation Code

#### 4.1 `Source/Graphics/HanoiPalette.cs`
```csharp
using System;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Authentic color palette recreating Hanoi Old Quarter flat cartoon aesthetics.
/// </summary>
public static class HanoiPalette
{
    // --- Architecture & Street ---
    public static readonly Color WallYellow         = new(229, 182, 76, 255);  // #E5B64C Warm French ochre
    public static readonly Color WallYellowShadow   = new(199, 151, 56, 255);  // #C79738 Ochre shadow
    public static readonly Color WallYellowLight    = new(246, 211, 120, 255); // #F6D378 Sunlit highlight
    public static readonly Color ShutterGreen       = new(46, 90, 68, 255);    // #2E5A44 Moss green louvre
    public static readonly Color ShutterGreenDark   = new(29, 61, 45, 255);    // #1D3D2D Slat shadow
    public static readonly Color ShutterGreenLight  = new(62, 123, 92, 255);   // #3E7B5C Trim highlight
    public static readonly Color TerracottaRoof     = new(200, 76, 50, 255);   // #C84C32 Curved clay tiles
    public static readonly Color TerracottaDark     = new(150, 53, 32, 255);   // #963520 Weathered tile shadow
    public static readonly Color TerracottaLight    = new(224, 104, 76, 255);  // #E0684C Ridge highlight
    public static readonly Color RustedIron         = new(56, 44, 41, 255);    // #382C29 Long Bien steel
    public static readonly Color RustedIronPatina   = new(107, 66, 53, 255);   // #6B4235 Oxidized iron
    public static readonly Color StoneStreet        = new(163, 154, 142, 255); // #A39A8E Flagstone paving
    public static readonly Color StoneCurb          = new(128, 119, 109, 255); // #80776D Kerbstone border
    public static readonly Color StoneMortar        = new(94, 86, 79, 255);    // #5E564F Pavement joints

    // --- Street Life & Culture ---
    public static readonly Color PlasticTableBlue   = new(30, 136, 229, 255);  // #1E88E5 Song Long blue table
    public static readonly Color PlasticTableLight  = new(66, 165, 245, 255);  // #42A5F5 Table rim highlight
    public static readonly Color PlasticStoolRed    = new(229, 57, 53, 255);   // #E53935 Red plastic stool
    public static readonly Color WoodStoolBrown     = new(141, 91, 58, 255);   // #8D5B3A Low wooden stool
    public static readonly Color WoodStoolDark      = new(93, 56, 32, 255);    // #5D3820 Wood grain
    public static readonly Color LotusPink          = new(244, 143, 177, 255); // #F48FB1 Lotus petal
    public static readonly Color LotusRose          = new(233, 30, 99, 255);   // #E91E63 Deep lotus rose
    public static readonly Color LotusWhite         = new(255, 243, 224, 255); // #FFF3E0 Lotus tip
    public static readonly Color LotusLeafGreen     = new(67, 160, 71, 255);   // #43A047 Large flat leaf
    public static readonly Color BambooWoven        = new(215, 180, 118, 255); // #D7B476 Woven basket
    public static readonly Color BicycleFrameTeal   = new(43, 76, 72, 255);    // #2B4C48 Thong Nhat frame
    public static readonly Color CycloCanopyOlive   = new(78, 93, 82, 255);    // #4E5D52 Canvas hood
    public static readonly Color CycloSeatRed       = new(139, 38, 30, 255);   // #8B261E Passenger cushion
    public static readonly Color ChromeSilver       = new(210, 215, 223, 255); // #D2D7DF Wheel spokes

    // --- Bé Mây & Bút Chì ---
    public static readonly Color NonLaStraw         = new(245, 229, 201, 255); // #F5E5C9 Conical hat straw
    public static readonly Color NonLaRibbon        = new(255, 138, 128, 255); // #FF8A80 Chin ribbon
    public static readonly Color SkinPeach          = new(255, 224, 189, 255); // #FFE0BD Warm peach skin
    public static readonly Color CheekBlush         = new(255, 171, 145, 255); // #FFAB91 Cheerful blush
    public static readonly Color BeMayShirtBlue     = new(129, 212, 250, 255); // #81D4FA Ao Ba Ba sky blue
    public static readonly Color BeMayPantsGrey     = new(69, 90, 100, 255);   // #455A64 Charcoal trousers
    public static readonly Color SatchelMustard     = new(255, 160, 0, 255);   // #FFA000 Memory journal bag
    public static readonly Color HairEspresso       = new(44, 30, 27, 255);    // #2C1E1B Braided dark hair
    public static readonly Color ButChiYellow       = new(255, 213, 79, 255);  // #FFD54F Sunny pencil body
    public static readonly Color ButChiWoodCollar   = new(255, 224, 178, 255); // #FFE0B2 Carved wood
    public static readonly Color ButChiGraphite     = new(38, 50, 56, 255);    // #263238 Soft lead tip
    public static readonly Color ButChiMagicCyan    = new(0, 229, 255, 255);   // #00E5FF Sparkle tip
    public static readonly Color ButChiEraserPink   = new(255, 138, 128, 255); // #FF8A80 Eraser
    public static readonly Color ButChiFerrule      = new(207, 216, 220, 255); // #CFD8DC Metal collar

    // --- Cartoon Outline & Shadows ---
    public static readonly Color CartoonOutline     = new(34, 34, 38, 255);    // #222226 Deep outline
    public static readonly Color DropShadow         = new(0, 0, 0, 55);        // 22% alpha shadow

    /// <summary>
    /// Linearly desaturates a vibrant color towards its perceptual luminance gray base.
    /// colorPercent: 0.0f (pure gray) to 100.0f (full vibrant color).
    /// </summary>
    public static Color ApplyDesaturation(Color c, float colorPercent)
    {
        float t = Math.Clamp(colorPercent / 100.0f, 0.0f, 1.0f);
        if (t >= 0.999f) return c;
        float lum = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        byte r = (byte)Math.Clamp(lum + (c.R - lum) * t, 0, 255);
        byte g = (byte)Math.Clamp(lum + (c.G - lum) * t, 0, 255);
        byte b = (byte)Math.Clamp(lum + (c.B - lum) * t, 0, 255);
        return new Color(r, g, b, c.A);
    }
}
```

#### 4.2 `Source/Graphics/FontManager.cs`
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

namespace GameProject.Graphics;

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

    public static int[] GetVietnameseCodepoints()
    {
        var list = new List<int>();
        // 1. Basic Latin, Latin-1 Supplement, Latin Extended-A & B (32 to 591)
        for (int cp = 0x0020; cp <= 0x024F; cp++) list.Add(cp);

        // 2. Latin Extended Additional (Vietnamese diacritics: 7840 to 7929)
        for (int cp = 0x1EA0; cp <= 0x1EF9; cp++) list.Add(cp);

        // 3. Typographic punctuation, dashes, quotes, and VND currency symbol ₫ (0x20AB)
        for (int cp = 0x2000; cp <= 0x206F; cp++) list.Add(cp);

        return list.ToArray();
    }

    public void Initialize()
    {
        if (IsLoaded) return;

        string? fontPath = ResolveFontPath();
        int[] codepoints = GetVietnameseCodepoints();

        if (fontPath != null && File.Exists(fontPath))
        {
            HeaderFont = LoadFontWithFilter(fontPath, 36, codepoints);
            TitleFont = LoadFontWithFilter(fontPath, 24, codepoints);
            BodyFont = LoadFontWithFilter(fontPath, 20, codepoints);
            SmallFont = LoadFontWithFilter(fontPath, 14, codepoints);
            IsLoaded = true;
        }
        else
        {
            Font def = Raylib.GetFontDefault();
            HeaderFont = TitleFont = BodyFont = SmallFont = def;
            IsLoaded = false;
        }
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
        Font activeFont = font ?? SelectFontBySize(size);
        Raylib.DrawTextEx(activeFont, text, pos, size, 1.0f, color);
    }

    public void DrawTextOutline(string text, Vector2 pos, float size, Color color, Color outlineColor, float outlineThick = 2.0f, Font? font = null)
    {
        Font activeFont = font ?? SelectFontBySize(size);
        // 8-directional shadow stamping for crisp comic outline
        Vector2[] offsets =
        [
            new(-outlineThick, 0), new(outlineThick, 0),
            new(0, -outlineThick), new(0, outlineThick),
            new(-outlineThick, -outlineThick), new(outlineThick, -outlineThick),
            new(-outlineThick, outlineThick), new(outlineThick, outlineThick)
        ];

        foreach (var off in offsets)
        {
            Raylib.DrawTextEx(activeFont, text, pos + off, size, 1.0f, outlineColor);
        }
        Raylib.DrawTextEx(activeFont, text, pos, size, 1.0f, color);
    }

    public Vector2 MeasureText(string text, float size, Font? font = null)
    {
        Font activeFont = font ?? SelectFontBySize(size);
        return Raylib.MeasureTextEx(activeFont, text, size, 1.0f);
    }

    private Font SelectFontBySize(float size)
    {
        if (size >= 30) return HeaderFont;
        if (size >= 22) return TitleFont;
        if (size >= 16) return BodyFont;
        return SmallFont;
    }

    public void Dispose()
    {
        foreach (var font in _loadedFonts)
        {
            Raylib.UnloadFont(font);
        }
        _loadedFonts.Clear();
        IsLoaded = false;
        _instance = null;
    }
}
```

#### 4.3 `Source/Graphics/VectorRenderer.cs`
```csharp
using System;
using System.Numerics;
using Raylib_cs;
using GameProject.Core.Puzzles; // For Direction enum

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

    public static void DrawBeMay(Vector2 feetPos, Direction facing, float animTime, bool isMoving, float scale = 1.0f)
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
        if (facing != Direction.Up)
        {
            // Cheeks
            Raylib.DrawCircleV(new Vector2(headCenter.X - 6f * scale, headCenter.Y + 2f * scale), 2.5f * scale, HanoiPalette.CheekBlush);
            Raylib.DrawCircleV(new Vector2(headCenter.X + 6f * scale, headCenter.Y + 2f * scale), 2.5f * scale, HanoiPalette.CheekBlush);

            // Eyes
            float eyeXOff = (facing == Direction.Left) ? -2f * scale : (facing == Direction.Right ? 2f * scale : 0f);
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
        float tilt = MathF.Sin(animTime * 2.5f) * 6f; // degrees
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

        // Twin Woven Bamboo Baskets (Thúng tre chở hoa)
        Rectangle basketRec = new(pos.X - 32f * scale, pos.Y - 34f * scale, 22f * scale, 18f * scale);
        DrawRoundedRect(basketRec, 0.4f, HanoiPalette.BambooWoven, HanoiPalette.CartoonOutline, 1.5f * scale);

        // Lotus Flowers overflowing
        Vector2[] lotusCenters =
        [
            new(pos.X - 26f * scale, pos.Y - 38f * scale),
            new(pos.X - 18f * scale, pos.Y - 42f * scale),
            new(pos.X - 12f * scale, pos.Y - 36f * scale)
        ];
        foreach (var lc in lotusCenters)
        {
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

        // Canopy Hood (Mui bạt gập)
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
        // Table rim shine
        Raylib.DrawRectangleRoundedLinesEx(new Rectangle(tableRec.X + 2f, tableRec.Y + 2f, tableRec.Width - 4f, 4f), 0.5f, 4, 1f, HanoiPalette.PlasticTableLight);
    }

    public static void DrawInteractionPrompt(Vector2 pos, string keyText, string actionText, float animTime)
    {
        float floatOffset = MathF.Sin(animTime * 4f) * 3f;
        Vector2 center = new(pos.X, pos.Y - 60f + floatOffset);

        string fullText = $"[{keyText}] {actionText}";
        Vector2 size = FontManager.Instance.MeasureText(fullText, 14f);
        Rectangle bubble = new(center.X - size.X * 0.5f - 8f, center.Y - size.Y * 0.5f - 4f, size.X + 16f, size.Y + 8f);

        // Speech bubble body
        DrawRoundedRect(bubble, 0.45f, Color.White, HanoiPalette.CartoonOutline, 2f, withShadow: true);

        // Little triangular arrow pointing down
        Vector2 tip = new(center.X, bubble.Y + bubble.Height + 5f);
        Vector2 p1 = new(center.X - 4f, bubble.Y + bubble.Height - 1f);
        Vector2 p2 = new(center.X + 4f, bubble.Y + bubble.Height - 1f);
        Raylib.DrawTriangle(p2, tip, p1, Color.White);

        // Text
        FontManager.Instance.DrawText(fullText, new Vector2(bubble.X + 8f, bubble.Y + 4f), 14f, HanoiPalette.CartoonOutline);
    }
}
```

#### 4.4 `Source/Graphics/ColorBloomEffect.cs`
```csharp
using System;
using System.Numerics;
using Raylib_cs;

namespace GameProject.Graphics;

public sealed class ColorBloomEffect : IDisposable
{
    private RenderTexture2D _sceneTarget;
    private Shader _desatShader;
    private bool _shaderSupported;
    private int _screenWidth;
    private int _screenHeight;

    private int _locColorPercent;
    private int _locBloomCenter;
    private int _locBloomRadius;
    private int _locBloomIntensity;

    private float _currentColorPercent;
    private float _targetColorPercent;
    private Vector2 _bloomCenter;
    private float _bloomRadius;
    private float _bloomIntensity;
    private float _waveDuration = 1.5f;
    private float _waveElapsed;
    private bool _isWaveActive;

    public float CurrentColorPercent => _currentColorPercent;

    private const string FragmentShaderCode = @"
#version 330
in vec2 fragTexCoord;
in vec4 fragColor;
out vec4 finalColor;
uniform sampler2D texture0;
uniform float u_ColorPercent;
uniform vec2 u_BloomCenter;
uniform float u_BloomRadius;
uniform float u_BloomIntensity;

const vec3 LUMINANCE = vec3(0.2126, 0.7152, 0.0722);

void main()
{
    vec4 texel = texture(texture0, fragTexCoord);
    float gray = dot(texel.rgb, LUMINANCE);

    float dist = distance(gl_FragCoord.xy, u_BloomCenter);
    float ripple = 0.0;
    if (dist < u_BloomRadius && u_BloomRadius > 0.0)
    {
        ripple = smoothstep(u_BloomRadius, max(0.0, u_BloomRadius - 80.0), dist);
    }

    float effective = clamp(u_ColorPercent + ripple * 0.35, 0.0, 1.0);
    vec3 baseColor = mix(vec3(gray), texel.rgb, effective);

    if (ripple > 0.0 && effective < 1.0)
    {
        vec3 warmGlow = vec3(1.15, 0.95, 0.70) * sin(ripple * 3.14159) * u_BloomIntensity;
        baseColor += warmGlow * (1.0 - effective);
    }

    finalColor = vec4(baseColor, texel.a) * fragColor;
}";

    public void Initialize(int width, int height)
    {
        _screenWidth = width;
        _screenHeight = height;
        _sceneTarget = Raylib.LoadRenderTexture(width, height);

        _desatShader = Raylib.LoadShaderFromMemory(null, FragmentShaderCode);
        _shaderSupported = Raylib.IsShaderValid(_desatShader);

        if (_shaderSupported)
        {
            _locColorPercent = Raylib.GetShaderLocation(_desatShader, "u_ColorPercent");
            _locBloomCenter = Raylib.GetShaderLocation(_desatShader, "u_BloomCenter");
            _locBloomRadius = Raylib.GetShaderLocation(_desatShader, "u_BloomRadius");
            _locBloomIntensity = Raylib.GetShaderLocation(_desatShader, "u_BloomIntensity");
        }
    }

    public void TriggerWave(Vector2 epicenter, float targetPercent, float duration = 1.5f)
    {
        _bloomCenter = epicenter;
        _targetColorPercent = Math.Clamp(targetPercent, 0f, 100f);
        _waveDuration = duration;
        _waveElapsed = 0f;
        _isWaveActive = true;
        _bloomIntensity = 1.0f;
    }

    public void Update(float dt)
    {
        if (_isWaveActive)
        {
            _waveElapsed += dt;
            float progress = Math.Clamp(_waveElapsed / _waveDuration, 0f, 1f);
            float ease = 1f - MathF.Pow(1f - progress, 3f); // Cubic out

            _bloomRadius = ease * MathF.Max(_screenWidth, _screenHeight) * 1.5f;
            _currentColorPercent = MathHelper.Lerp(_currentColorPercent, _targetColorPercent, ease);
            _bloomIntensity = 1f - progress;

            if (progress >= 1f)
            {
                _isWaveActive = false;
                _bloomRadius = 0f;
                _currentColorPercent = _targetColorPercent;
            }
        }
    }

    public void BeginRender()
    {
        Raylib.BeginTextureMode(_sceneTarget);
        Raylib.ClearBackground(Color.Black);
    }

    public void EndRenderAndDraw()
    {
        Raylib.EndTextureMode();

        // Raylib/OpenGL RenderTexture is inverted on Y-axis
        Rectangle sourceRec = new(0, 0, _sceneTarget.Texture.Width, -_sceneTarget.Texture.Height);
        Rectangle destRec = new(0, 0, _screenWidth, _screenHeight);

        if (_shaderSupported)
        {
            float normColor = _currentColorPercent / 100f;
            Raylib.SetShaderValue(_desatShader, _locColorPercent, normColor, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(_desatShader, _locBloomCenter, _bloomCenter, ShaderUniformDataType.Vec2);
            Raylib.SetShaderValue(_desatShader, _locBloomRadius, _bloomRadius, ShaderUniformDataType.Float);
            Raylib.SetShaderValue(_desatShader, _locBloomIntensity, _bloomIntensity, ShaderUniformDataType.Float);

            Raylib.BeginShaderMode(_desatShader);
            Raylib.DrawTexturePro(_sceneTarget.Texture, sourceRec, destRec, Vector2.Zero, 0f, Color.White);
            Raylib.EndShaderMode();
        }
        else
        {
            // Shader fallback: render plain texture
            Raylib.DrawTexturePro(_sceneTarget.Texture, sourceRec, destRec, Vector2.Zero, 0f, Color.White);
        }
    }

    public void Dispose()
    {
        if (Raylib.IsRenderTextureValid(_sceneTarget)) Raylib.UnloadRenderTexture(_sceneTarget);
        if (_shaderSupported) Raylib.UnloadShader(_desatShader);
    }
}
```

---

## 5. Verification Method

1. **Codepoint Generator Verification**:
   Execute `FontManager.GetVietnameseCodepoints()` in a unit test:
   - Verify array contains `0x0020` (space), `0x0041` ('A'), `0x0110` ('Đ'), `0x0111` ('đ'), `0x01A0` ('Ơ'), `0x01AF` ('Ư').
   - Verify array contains all compound diacritics: `0x1EA0` ('Ạ'), `0x1EAF` ('ắ'), `0x1EF9` ('ỹ').
   - Verify array contains `0x20AB` ('₫').
   - Invalidation condition: Any missing character code results in missing glyph rendering in dialogue.
2. **Color Desaturation Mathematical Purity**:
   - For `ApplyDesaturation(c, 0.0f)`, verify that `R == G == B == (byte)(0.2126*R + 0.7152*G + 0.0722*B)`.
   - For `ApplyDesaturation(c, 100.0f)`, verify that returned color equals original `c`.
3. **Headless Safety**:
   - Run `dotnet test Tests/GameProject.Tests.csproj`.
   - All 192 existing domain tests MUST continue to pass in < 200 ms with 0 warnings.
4. **Visual Run Verification (Presentation)**:
   - When launching the game window, Vietnamese text `"Du Ký Bé Mây"`, `"Phố Hàng Buồm"`, `"Bánh Phở tươi chần kỹ"` must render with proper accents and zero missing glyph boxes (`?`).
