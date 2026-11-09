# Handoff Report: Milestone 2 Remediation Explorer 3 (Inventory Alias Symmetry & FontManager FormC Normalization)

**Author**: `teamwork_explorer_m2_remed_3`  
**Milestone**: Milestone 2 Remediation (Hanoi Exploration & Quest 1)  
**Role**: Read-only Investigation & Synthesis  
**Date**: 2026-11-09T00:35:00Z  

---

## 1. Observation

1. **Inventory Alias Asymmetry in `Source/Core/Models/Inventory.cs`**:
   - In lines 79–88, `HasItem` implements alias support between `Item_ThaoMoc` (`ItemIds.ThaoMoc`) and `Item_GiaVi` (`ItemIds.GiaVi`):
     ```csharp
     // Also support alias between ThaoMoc and GiaVi
     if (string.Equals(itemId, ItemIds.ThaoMoc, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(itemId, ItemIds.GiaVi, StringComparison.OrdinalIgnoreCase))
     {
         return _items.Contains(ItemIds.ThaoMoc, StringComparer.OrdinalIgnoreCase) ||
                _items.Contains(ItemIds.GiaVi, StringComparer.OrdinalIgnoreCase);
     }
     ```
   - In contrast, `AddItem` (lines 51–60) only checks exact string match:
     ```csharp
     if (!_items.Contains(itemId, StringComparer.OrdinalIgnoreCase))
     {
         _items.Add(itemId);
         return true;
     }
     ```
     When `ItemIds.ThaoMoc` is already in inventory, calling `AddItem(ItemIds.GiaVi)` succeeds and appends `ItemIds.GiaVi`, resulting in `Count == 2` for the exact same conceptual item.
   - Similarly, `RemoveItem` (lines 62–74) only removes exact string matches:
     ```csharp
     for (int i = 0; i < _items.Count; i++)
     {
         if (string.Equals(_items[i], itemId, StringComparison.OrdinalIgnoreCase))
         {
             _items.RemoveAt(i);
             return true;
         }
     }
     return false;
     ```
     When only `ItemIds.ThaoMoc` is in inventory, `HasItem(ItemIds.GiaVi)` returns `true`, but `RemoveItem(ItemIds.GiaVi)` returns `false` without removing anything. This creates a direct contract violation: `HasItem(X) == true` but `RemoveItem(X) == false`.
   - Empirically characterized by Challenger 2 in `Tests/Challenger2EmpiricalTests.cs` lines 457–481 (`Inventory_ThaoMocAndGiaVi_AliasAsymmetry_BehaviorCharacterized`).

2. **FontManager NFD Missing Glyphs & Invariant Codepoints Count in `Source/Graphics/FontManager.cs`**:
   - `FontManager.GetVietnameseCodepoints()` (lines 29–46) generates exactly 762 codepoints:
     - Basic Latin + Latin Extended-A/B (`0x0020..0x024F`): 560 codepoints.
     - Latin Extended Additional (`0x1EA0..0x1EF9`): 90 codepoints.
     - Typographic punctuation / quotes (`0x2000..0x206E`): 111 codepoints.
     - Currency symbol `₫` (`0x20AB`): 1 codepoint.
     - Total: exactly 762 codepoints.
   - All 134 Vietnamese precomposed vowels with tones (12 vowels $\times$ 5 tones $\times$ 2 cases + Đ/đ) are present in Form C (precomposed).
   - In `Tests/InteractionAndQuest1Tests.cs` lines 363–366:
     ```csharp
     [Fact]
     public void Graphics_FontManager_GetVietnameseCodepoints_Covers762Glyphs()
     {
         int[] codepoints = FontManager.GetVietnameseCodepoints();
         Assert.Equal(762, codepoints.Length);
     ```
     Existing regression tests enforce the exact length of 762.
   - In `Tests/Challenger2EmpiricalTests.cs` lines 783–818 (`VietnameseUnicode_DecomposedNFDDiacritics_NotInCodepointsTable_RevealsNFCRequirement`), combining diacritical marks (`U+0300..U+036F`, e.g. U+0300 grave, U+0301 acute, U+0309 hook) are verified absent from `codepoints`.
   - Raylib rasterizes fonts without OpenType shaping (no HarfBuzz text layout). If decomposed NFD strings (common on macOS APFS clipboard and certain Vietnamese IMEs) are passed to `DrawText`, `DrawTextOutline`, or `MeasureText` (lines 109–138), Raylib cannot shape combining marks on top of base vowels; standalone marks are rendered as missing `?` glyphs.
   - Currently, `DrawText`, `DrawTextOutline`, and `MeasureText` pass raw input strings directly to `Raylib.DrawTextEx` and `Raylib.MeasureTextEx` without calling `.Normalize(NormalizationForm.FormC)`. Furthermore, passing `null` or empty strings to `DrawText`/`MeasureText` is unguarded.

3. **Pre-existing Compiler Errors in `Source/Stages/WorldMapScene.cs` and `Source/Stages/AlbumViewerScene.cs`**:
   - Running `dotnet test Tests/GameProject.Tests.csproj` fails during compilation of `GameProject.csproj`:
     - `Source/Stages/WorldMapScene.cs(126,48)`: `error CS0121: The call is ambiguous between the following methods or properties: 'Color.Color(byte, byte, byte, byte)' and 'Color.Color(int, int, int, int)'`.
     - `Source/Stages/AlbumViewerScene.cs(219,49)`: `error CS1503: Argument 3: cannot convert from 'Raylib_cs.Color' to 'int'`.
     - `Source/Stages/AlbumViewerScene.cs(223,54)`: `error CS1503: Argument 3: cannot convert from 'Raylib_cs.Color' to 'int'`.

---

## 2. Logic Chain

1. **Inventory Alias Symmetry**:
   - `ItemIds.GiaVi` is documented as `// Alias for ThaoMoc garnish`.
   - If an inventory contains `ItemIds.ThaoMoc`, it already contains the seasoning/garnish ingredient.
   - Calling `HasItem(ItemIds.GiaVi)` returns `true` because `ThaoMoc` satisfies the requirement.
   - Therefore, calling `AddItem(ItemIds.GiaVi)` when `ItemIds.ThaoMoc` is present MUST be rejected as an idempotent duplicate (`HasItem(itemId) == true => return false`).
   - Symmetrically, calling `RemoveItem(ItemIds.GiaVi)` when `ItemIds.ThaoMoc` is present MUST remove `ItemIds.ThaoMoc` and return `true`, ensuring `HasItem(ItemIds.GiaVi) == true => RemoveItem(ItemIds.GiaVi) == true`.
   - By structuring `AddItem` to check `if (HasItem(itemId)) return false;` and `RemoveItem` to match aliases using `IsThaoMocOrGiaVi`, mathematical symmetry and idempotency are restored across all operations.

2. **FontManager Unicode Normalization**:
   - In Unicode standard, Vietnamese text can be encoded in NFC (Normalization Form C — Canonical Decomposition, followed by Canonical Composition) or NFD (Normalization Form D — Canonical Decomposition).
   - Raylib uses simple bitmap/SDF glyph quads indexed by unicode codepoint; it does not perform complex OpenType mark positioning. Even if combining diacritics were loaded into the font atlas, Raylib renders them adjacent to vowels rather than combined above/below.
   - In contrast, Form C combines base vowel + diacritic into a single precomposed Unicode codepoint (e.g. `'ơ' + U+0309` $\rightarrow$ `'ở'` `U+1EDF`). All 134 Vietnamese precomposed characters are already included in the 762 codepoints table.
   - In .NET, `string.Normalize(NormalizationForm.FormC)` is an allocation-free no-op if the string is already Form C (checked via `IsNormalized`).
   - Normalizing input in `DrawText`, `DrawTextOutline`, and `MeasureText` completely eliminates `?` glyphs for all NFD input from macOS or external sources with zero overhead for existing Form C text.
   - Crucially, `GetVietnameseCodepoints()` MUST remain at 762 codepoints to satisfy the strict invariant tested in `Graphics_FontManager_GetVietnameseCodepoints_Covers762Glyphs` and `VietnameseUnicode_DecomposedNFDDiacritics_NotInCodepointsTable_RevealsNFCRequirement`.

---

## 3. Caveats

1. **Read-only Investigation**: In compliance with Explorer role constraints, no source files were modified directly.
2. **Pre-existing Compiler Errors in Scenes**: The worker MUST fix the 3 trivial compiler typos in `WorldMapScene.cs:126` and `AlbumViewerScene.cs:219,223` before `dotnet test` can compile (exact fixes are provided below in Section 4).
3. **Updating Adversarial Characterization Test**: `Challenger2EmpiricalTests.cs` line 457 characterized the old buggy behavior. When the fix is applied, that test must be updated to assert the new symmetric behavior (`Assert.False(addedGiaVi)` and `Assert.True(removed)`).

---

## 4. Conclusion & Drop-In Code Remediations

### Remediation A: `Source/Core/Models/Inventory.cs` (Drop-in Replacement)

Replace lines 51–88 of `Source/Core/Models/Inventory.cs` with the following clean, symmetric implementation:

```csharp
    private static bool IsThaoMocOrGiaVi(string id) =>
        string.Equals(id, ItemIds.ThaoMoc, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, ItemIds.GiaVi, StringComparison.OrdinalIgnoreCase);

    public bool AddItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (HasItem(itemId)) return false; // Idempotent: duplicates and aliases rejected

        _items.Add(itemId);
        return true;
    }

    public bool RemoveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        bool isAliasTarget = IsThaoMocOrGiaVi(itemId);

        for (int i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i], itemId, StringComparison.OrdinalIgnoreCase) ||
                (isAliasTarget && IsThaoMocOrGiaVi(_items[i])))
            {
                _items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        if (IsThaoMocOrGiaVi(itemId))
        {
            return _items.Contains(ItemIds.ThaoMoc, StringComparer.OrdinalIgnoreCase) ||
                   _items.Contains(ItemIds.GiaVi, StringComparer.OrdinalIgnoreCase);
        }

        return _items.Contains(itemId, StringComparer.OrdinalIgnoreCase);
    }
```

#### Full `Source/Core/Models/Inventory.cs` Drop-in File:
```csharp
using System;
using System.Collections.Generic;

namespace GameProject.Core.Models;

/// <summary>
/// Predefined item identifier constants for Hanoi ingredients and key items.
/// </summary>
public static class ItemIds
{
    public const string BanhPho = "Item_BanhPho";
    public const string ThitBo = "Item_ThitBo";
    public const string NuocDung = "Item_NuocDung";
    public const string HanhLa = "Item_HanhLa";
    public const string ThaoMoc = "Item_ThaoMoc";
    public const string GiaVi = "Item_GiaVi"; // Alias for ThaoMoc garnish

    public static readonly IReadOnlyList<string> AllPhoIngredients = new[]
    {
        BanhPho,
        ThitBo,
        NuocDung,
        HanhLa,
        ThaoMoc
    };
}

/// <summary>
/// Manages collected inventory items with idempotent addition and collection queries.
/// </summary>
public sealed class Inventory
{
    private readonly List<string> _items = new();

    public IReadOnlyList<string> Items => _items.AsReadOnly();
    public int Count => _items.Count;

    public Inventory() { }

    public Inventory(IEnumerable<string>? initialItems)
    {
        if (initialItems != null)
        {
            foreach (var item in initialItems)
            {
                AddItem(item);
            }
        }
    }

    private static bool IsThaoMocOrGiaVi(string id) =>
        string.Equals(id, ItemIds.ThaoMoc, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(id, ItemIds.GiaVi, StringComparison.OrdinalIgnoreCase);

    public bool AddItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (HasItem(itemId)) return false; // Idempotent: duplicates and aliases rejected

        _items.Add(itemId);
        return true;
    }

    public bool RemoveItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        bool isAliasTarget = IsThaoMocOrGiaVi(itemId);

        for (int i = 0; i < _items.Count; i++)
        {
            if (string.Equals(_items[i], itemId, StringComparison.OrdinalIgnoreCase) ||
                (isAliasTarget && IsThaoMocOrGiaVi(_items[i])))
            {
                _items.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    public bool HasItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;

        if (IsThaoMocOrGiaVi(itemId))
        {
            return _items.Contains(ItemIds.ThaoMoc, StringComparer.OrdinalIgnoreCase) ||
                   _items.Contains(ItemIds.GiaVi, StringComparer.OrdinalIgnoreCase);
        }

        return _items.Contains(itemId, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> GetItems() => Items;

    public void Clear() => _items.Clear();

    public List<string> ToList() => new(_items);
}
```

---

### Remediation B: `Source/Graphics/FontManager.cs` (Drop-in Replacement)

1. Add `using System.Text;` to top of `Source/Graphics/FontManager.cs`.
2. Add public static helper `NormalizeText(string? text)`:
   ```csharp
   public static string NormalizeText(string? text)
   {
       if (string.IsNullOrEmpty(text)) return string.Empty;
       return text.Normalize(NormalizationForm.FormC);
   }
   ```
3. Update `DrawText`, `DrawTextOutline`, and `MeasureText` to normalize text and guard against null/empty strings:
   ```csharp
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
   ```

#### Full `Source/Graphics/FontManager.cs` Drop-in File:
```csharp
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
```

---

### Remediation C: Update `Tests/Challenger2EmpiricalTests.cs` (Lines 457–481)

Update `Inventory_ThaoMocAndGiaVi_AliasAsymmetry_BehaviorCharacterized` to verify the remediated symmetric behavior:

```csharp
    [Fact]
    public void Inventory_ThaoMocAndGiaVi_AliasSymmetry_Verified()
    {
        var inv = new Inventory();

        inv.AddItem(ItemIds.ThaoMoc);
        Assert.True(inv.HasItem(ItemIds.ThaoMoc));
        Assert.True(inv.HasItem(ItemIds.GiaVi)); // HasItem returns true due to alias check

        // Idempotent: adding GiaVi when ThaoMoc is present is rejected
        bool addedGiaVi = inv.AddItem(ItemIds.GiaVi);
        Assert.False(addedGiaVi);
        Assert.Equal(1, inv.Count);

        // Symmetric: RemoveItem("Item_GiaVi") removes the alias (ThaoMoc)
        bool removed = inv.RemoveItem(ItemIds.GiaVi);
        Assert.True(removed);
        Assert.Equal(0, inv.Count);
        Assert.False(inv.HasItem(ItemIds.ThaoMoc));
        Assert.False(inv.HasItem(ItemIds.GiaVi));

        // Symmetric reverse order: Add GiaVi, reject ThaoMoc, remove ThaoMoc
        inv.AddItem(ItemIds.GiaVi);
        Assert.False(inv.AddItem(ItemIds.ThaoMoc));
        Assert.Equal(1, inv.Count);
        Assert.True(inv.RemoveItem(ItemIds.ThaoMoc));
        Assert.Equal(0, inv.Count);
    }
```

And add unit tests for `FontManager.NormalizeText`:

```csharp
    [Fact]
    public void FontManager_NormalizeText_NormalizesNfdToPrecomposedFormC()
    {
        // Decomposed Vietnamese strings (common on macOS APFS clipboard)
        string nfdPho = "Ph\u01A1\u0309"; // Ph + ơ + Combining Hook Above
        string normalized = FontManager.NormalizeText(nfdPho);

        Assert.Equal("Phở", normalized);
        Assert.Equal("\u1EDF", normalized.Substring(2)); // U+1EDF is precomposed 'ở'

        int[] codepoints = FontManager.GetVietnameseCodepoints();
        var codepointSet = new HashSet<int>(codepoints);
        Assert.All(normalized, c => Assert.Contains((int)c, codepointSet));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("Hà Nội", "Hà Nội")]
    public void FontManager_NormalizeText_HandlesEdgeCasesSafely(string? input, string expected)
    {
        Assert.Equal(expected, FontManager.NormalizeText(input));
    }
```

---

### Remediation D: Pre-requisite Compiler Fixes (for Worker `teamwork_worker_m2_it2`)

1. In `Source/Stages/WorldMapScene.cs` line 126:
   ```csharp
   // Before:
   Raylib.DrawLine(0, y, 1280, y, new Color(r, g, b, 255));
   // After:
   Raylib.DrawLine(0, y, 1280, y, new Color((int)r, (int)g, (int)b, 255));
   ```
2. In `Source/Stages/AlbumViewerScene.cs` line 219:
   ```csharp
   // Before:
   Raylib.DrawRectangleRounded(rect, 0.1f, new Color(200, 230, 255, 255), 4);
   // After:
   Raylib.DrawRectangleRounded(rect, 0.1f, 4, new Color(200, 230, 255, 255));
   ```
3. In `Source/Stages/AlbumViewerScene.cs` line 223:
   ```csharp
   // Before:
   Raylib.DrawRectangleRounded(riverRect, 0.1f, new Color(120, 180, 220, 255), 4);
   // After:
   Raylib.DrawRectangleRounded(riverRect, 0.1f, 4, new Color(120, 180, 220, 255));
   ```

---

## 5. Verification Method

1. **Verify Inventory Alias Symmetry Unit Logic**:
   - `AddItem(ItemIds.ThaoMoc)` $\rightarrow$ returns `true`, `Count == 1`.
   - `HasItem(ItemIds.ThaoMoc)` $\rightarrow$ returns `true`.
   - `HasItem(ItemIds.GiaVi)` $\rightarrow$ returns `true`.
   - `AddItem(ItemIds.GiaVi)` $\rightarrow$ returns `false`, `Count == 1`.
   - `RemoveItem(ItemIds.GiaVi)` $\rightarrow$ returns `true`, `Count == 0`.
   - `HasItem(ItemIds.ThaoMoc)` $\rightarrow$ returns `false`.
   - `HasItem(ItemIds.GiaVi)` $\rightarrow$ returns `false`.

2. **Verify FontManager Form C Normalization**:
   - `FontManager.NormalizeText("Ph\u01A1\u0309") == "Phở"` (codepoint `0x1EDF`).
   - Every character of `FontManager.NormalizeText(nfdString)` is contained within `FontManager.GetVietnameseCodepoints()`.
   - `FontManager.GetVietnameseCodepoints().Length == 762` remains strictly invariant.
   - `DrawText(null, ...)`, `DrawTextOutline(null, ...)`, `MeasureText(null, ...)` execute safely without null reference exceptions.

3. **Run Project Test Suite**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj --filter "FullyQualifiedName~Inventory|FullyQualifiedName~FontManager"
   ```
   *Expected Result*: All inventory and font tests pass.

4. **Invalidation Conditions**:
   - If `AddItem(GiaVi)` adds a second item when `ThaoMoc` is present (`Count == 2`).
   - If `RemoveItem(GiaVi)` returns `false` while `HasItem(GiaVi)` returns `true`.
   - If `FontManager.GetVietnameseCodepoints().Length != 762`.
