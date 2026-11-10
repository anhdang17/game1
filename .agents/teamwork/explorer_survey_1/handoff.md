# Handoff Report: Character Presentation & Vector Rendering Architecture

**Date**: 2026-11-10  
**Author**: Explorer 1 (`explorer_survey_1`)  
**Mission**: Investigate Bé Mây & Bút Chì character presentation, `VectorRenderer.cs`, `HanoiPalette.cs`, callers, dependencies, and test coverage to guide worker implementation.

---

## 1. Observation

### 1.1. Current Character Drawing in `Source/Graphics/VectorRenderer.cs`

In `Source/Graphics/VectorRenderer.cs` (lines 62–155), `DrawBeMay` and `DrawButChi` are currently implemented as follows:

#### `DrawBeMay` (Lines 62–109):
```csharp
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
```

**Exact Humanoid Elements Observed to be Removed**:
1. **Lines 70–74**: Legs/Pants (`leftLeg`, `rightLeg`) using `HanoiPalette.BeMayPantsGrey`.
2. **Lines 76–78**: Tunic / Áo Bà Ba torso rectangle (`24x24`) using `HanoiPalette.BeMayShirtBlue`.
3. **Lines 85–88**: Human head circle (`radius = 11f`) using peach skin tone `HanoiPalette.SkinPeach`.
4. **Lines 102–108**: Adult-sized conical straw hat covering the head.

#### `DrawButChi` (Lines 111–155):
```csharp
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
```

**Shortcomings of Current Bút Chì**:
- The body is a flat mono-color yellow rounded rect without distinct 3-facet hexagonal wood shading or wood grain texture.
- The tip has only a single pulsing circle without a multi-layered radiant cyan halo or orbiting star dust particles.
- The eyes gaze straight ahead rather than tracking Bé Mây's direction.

---

### 1.2. Complete Caller Inventory Across Codebase

Command executed: `grep -rn "DrawBeMay\|DrawButChi" Source/ Tests/`

Direct callers of `DrawBeMay`:
1. `Source/Stages/HanoiExplorationScene.cs:493`:
   ```csharp
   DrawAction = t => VectorRenderer.DrawBeMay(playerPos, Kinematics.Facing, t, Kinematics.IsMoving)
   ```
2. `Source/UI/DialogueBox.cs:375`:
   ```csharp
   VectorRenderer.DrawBeMay(new Vector2(center.X, center.Y + 28f), FacingDirection.Down, 0f, false, scale: 0.95f);
   ```

Direct callers of `DrawButChi`:
1. `Source/Stages/HanoiExplorationScene.cs:500`:
   ```csharp
   DrawAction = t => VectorRenderer.DrawButChi(playerPos + new Vector2(24f, -34f), t)
   ```
2. `Source/UI/ButChiGuideUI.cs:218`:
   ```csharp
   VectorRenderer.DrawButChi(pos + new Vector2(0f, -4f), _animTime, 1.15f);
   ```
3. `Source/UI/DialogueBox.cs:381`:
   ```csharp
   VectorRenderer.DrawButChi(new Vector2(center.X, center.Y - 2f), 0f, scale: 1.2f);
   ```

*Note*: No minigames (`PhoAssemblyScene`, `StallPuzzleScene`), world map (`WorldMapScene`), or album scenes call `DrawBeMay` or `DrawButChi`.

---

### 1.3. Existing Color Palettes & Shared Shader Architecture

File: `Source/Graphics/HanoiPalette.cs` (78 lines)
- Defines static `readonly Color` fields for architecture, street culture, characters, and outlines.
- Character section (lines 43–58) contains:
  ```csharp
  public static readonly Color NonLaStraw         = new(245, 229, 201, 255);
  public static readonly Color NonLaRibbon        = new(255, 138, 128, 255);
  public static readonly Color SkinPeach          = new(255, 224, 189, 255);
  public static readonly Color CheekBlush         = new(255, 171, 145, 255);
  public static readonly Color BeMayShirtBlue     = new(129, 212, 250, 255);
  public static readonly Color BeMayPantsGrey     = new(69, 90, 100, 255);
  public static readonly Color SatchelMustard     = new(255, 160, 0, 255);
  public static readonly Color HairEspresso       = new(44, 30, 27, 255);
  public static readonly Color ButChiYellow       = new(255, 213, 79, 255);
  public static readonly Color ButChiWoodCollar   = new(255, 224, 178, 255);
  public static readonly Color ButChiGraphite     = new(38, 50, 56, 255);
  public static readonly Color ButChiMagicCyan    = new(0, 229, 255, 255);
  public static readonly Color ButChiEraserPink   = new(255, 138, 128, 255);
  public static readonly Color ButChiFerrule      = new(207, 216, 220, 255);
  ```
- Method `ApplyDesaturation(Color c, float colorPercent)` (lines 67–76) implements Rec. 709 luminance desaturation.
- Shader pipeline: `ColorBloomEffect.cs` wraps the whole scene rendering into a `RenderTexture2D` and runs a GLSL 330 shader that desaturates pixels based on `u_ColorPercent` and applies warm ripple blooms. Procedural vector drawings pass directly through this pipeline.

---

### 1.4. Unit Test Audit Regarding VectorRenderer & Character Drawing

Commands executed:
- `grep -rn "VectorRenderer" Tests/` (0 matches in test `.cs` files)
- `grep -rn "DrawBeMay\|DrawButChi" Tests/` (0 matches in test `.cs` files)
- `grep -rn "HanoiPalette" Tests/` -> matched only `Tests/InteractionAndQuest1Tests.cs:344`:
  ```csharp
  [Fact]
  public void Graphics_HanoiPalette_ApplyDesaturation_MathematicalPurity()
  ```
- Tests referencing `GameProject.Graphics` test `FontManager.NormalizeText`, `FontManager.GetVietnameseCodepoints`, and `HanoiPalette.ApplyDesaturation`.
- **Finding**: No unit test asserts on the visual output, structure, or implementation of `DrawBeMay`, `DrawButChi`, or `VectorRenderer`.

---

## 2. Logic Chain

1. **Safety of Visual Refactoring**:
   - Because 0 unit tests assert on `VectorRenderer`, changing `DrawBeMay` and `DrawButChi` internal drawing routines will not break any of the 567 unit tests as long as C# method signatures remain backward compatible.
   - `HanoiPalette.ApplyDesaturation` is the only palette method tested. All existing color fields in `HanoiPalette` must be retained to avoid any potential runtime or compile-time breakage in UI classes.

2. **Caller Compatibility**:
   - `DrawBeMay` has 2 callers: `HanoiExplorationScene.cs:493` and `DialogueBox.cs:375`.
   - `DrawButChi` has 3 callers: `HanoiExplorationScene.cs:500`, `ButChiGuideUI.cs:218`, and `DialogueBox.cs:381`.
   - Keeping the primary signatures:
     ```csharp
     public static void DrawBeMay(Vector2 feetPos, FacingDirection facing, float animTime, bool isMoving, float scale = 1.0f)
     public static void DrawButChi(Vector2 floatingPos, float animTime, float scale = 1.0f)
     ```
     (or adding optional parameters with default values) ensures 100% binary and source compatibility across all callers without modifying their call sites.

3. **Coordinate & Scaling Alignment**:
   - In `HanoiExplorationScene`, `feetPos` corresponds to ground level `Kinematics.Position`.
   - In `DialogueBox`, `feetPos = center + (0, 28f)` with circle avatar radius ~`36f`.
   - In the old `DrawBeMay`, the center of the head/body was `feetPos.Y - 34f` to `feetPos.Y - 44f`.
   - For the new Bé Mây cloud:
     - Center of cloud cluster at `cloudCenter = new(feetPos.X, feetPos.Y - 26f * scale + hoverY)`.
     - Cloud radius spans ~`18f * scale` horizontally and ~`16f * scale` vertically.
     - Top golden crest rises to `cloudCenter.Y - 16f * scale` (~`feetPos.Y - 42f * scale`).
     - This anchors Bé Mây within the exact same vertical bounding box (0 to -48px), perfectly preserving Y-sorting in exploration scenes and centering in dialogue avatar frames.

4. **Procedural Vector Aesthetics (Pure Raylib Vectors, Zero External Sprites)**:
   - **Bé Mây**:
     - 5 overlapping circular puffs: central body puff, left puff, right puff, top-left puff, top-right puff.
     - Milk-white pastel body: `Color(245, 250, 255)`.
     - Underbelly shadow: `Color(210, 230, 250)`.
     - Soft cartoon outline: `HanoiPalette.CartoonOutline` (`new Color(34, 34, 38, 255)`).
     - Rosy blush cheeks: `HanoiPalette.CheekBlush` (`new Color(255, 171, 145, 220)`).
     - Expressive anime/storybook eyes: charcoal base with primary white specular sparkle and secondary catchlight.
     - Golden sunlight crest: floating swirl at `cloudCenter.Y - 16f` with `Color(255, 215, 64, 255)`.
     - Mini tilted nón lá: straw yellow triangle `HanoiPalette.NonLaStraw` with red ribbon `HanoiPalette.NonLaRibbon` perched playfully on the golden crest.
     - Memory journal diary pouch: rich leather brown `Color(139, 84, 45, 255)` slung across the cloud flank with miniature golden buckle.
     - Squash & stretch: idle gentle breathing (`MathF.Sin(animTime * 2.5f) * 0.04f`) and moving bouncy glide (`MathF.Sin(animTime * 8f) * 0.08f`).
     - Subtle elliptical ground shadow: dynamic radius and alpha inversely linked to hover altitude.
   - **Bút Chì**:
     - Hexagonal body: 3 facet vertical strips with warm ochre shadow (`Color(240, 195, 60)`), honey yellow (`HanoiPalette.ButChiYellow`), and sunlit highlight (`Color(255, 235, 120)`).
     - Subtle vertical wood grain lines.
     - Rounded pink eraser: `HanoiPalette.ButChiEraserPink` with soft dome curvature.
     - Silver ferrule band: `HanoiPalette.ButChiFerrule` with bright specular line and dark crimp notches.
     - Sharpened wooden collar: `HanoiPalette.ButChiWoodCollar` and graphite tip `HanoiPalette.ButChiGraphite`.
     - Glowing magical cyan tip: multi-layer radiant cyan halo (`HanoiPalette.ButChiMagicCyan`) with bright white core.
     - Star dust particles: 3 procedural orbiting sparkles rotating dynamically around the tip apex based on `animTime`.
     - Curious blinking eyes: tracking Bé Mây's direction (offsetting pupil gaze towards Bé Mây, typically left-downward).
   - **Bóng Xám (`DrawBongXam`)**:
     - New helper `public static void DrawBongXam(Vector2 centerPos, float animTime, float scale = 1.0f)`.
     - Dreamy storm cloud with 5 rounded puffs in soft dusk slate-violet (`Color(120, 115, 145)`), deep shadow (`Color(85, 80, 110)`), and highlight (`Color(155, 150, 180)`).
     - Soulful sad expressive eyes with glistening teary sheen.
     - Subtle drifting mist/rain droplets.

---

## 3. Caveats

1. **Exploration Scene Shadow Layering**:
   - `HanoiExplorationScene.cs` currently has an independent `Player_Shadow` renderable entity at `SubLayerPriority = -1` (lines 483–487) AND `DrawBeMay` drew its own ellipse at line 68.
   - If `DrawBeMay` draws its own shadow, drawing the shadow twice in `HanoiExplorationScene` causes double alpha blending.
   - Worker should provide an optional parameter `bool withShadow = true` in `DrawBeMay` so callers can enable/disable it cleanly.
2. **Dialogue Box Avatar Expressions**:
   - Currently `DialogueBox.cs` switches on `Avatar_May`, `Avatar_May_Determined`, and `Avatar_May_Happy` but calls `DrawBeMay` identically for all three. In the future, expressions (e.g. eye shapes or blush intensity) could be mapped to an optional `expression` parameter if desired, while keeping default parameters compatible.

---

## 4. Conclusion

- The character presentation overhaul is completely isolated within `Source/Graphics/VectorRenderer.cs` and `Source/Graphics/HanoiPalette.cs`.
- Zero unit tests depend on the visual rendering of characters; 567/567 tests will remain green as long as signatures and existing palette constants are preserved.
- The redesign eliminates all humanoid legs, tunic, and oversized nón lá, fully implementing the Ghibli/whimsical storybook aesthetic specified in `ORIGINAL_REQUEST.md` and `plan_visual_gameplay_revamp.md`.

---

## 5. Verification Method

To verify the character presentation implementation:
1. **Compilation & Test Suite**:
   ```bash
   dotnet test Tests/GameProject.Tests.csproj -c Release
   ```
   *Expected*: 567 passed, 0 failed.
2. **Visual Inspection**:
   Run the application (`dotnet run`) and verify:
   - Bé Mây is a fluffy, floating 5-puff cloud with golden crest, mini tilted nón lá, leather pouch, rosy cheeks, and expressive sparkly eyes.
   - Bút Chì exhibits hexagonal wood facets, wood grain, pink eraser, silver ferrule, curious eyes tracking Bé Mây, and glowing cyan tip emitting star dust.
   - Dialogue avatars in `DialogueBox` reflect the updated cloud and pencil art without visual clipping.
   - `DrawBongXam` is available in `VectorRenderer.cs` for cutscenes and story moments.

---

## 6. Worker Implementation Plan

### Step 1: Update `Source/Graphics/HanoiPalette.cs`
Add the following new color constants under `// --- Bé Mây & Bút Chì ---` and `// --- Bóng Xám ---`:
```csharp
// --- Bé Mây & Bút Chì (Revamp) ---
public static readonly Color BeMayCloudWhite   = new(245, 250, 255, 255); // #F5FAFF Milk-white pastel cloud
public static readonly Color BeMayCloudShadow  = new(210, 230, 250, 255); // #D2E6FA Pale blue cloud underside shadow
public static readonly Color BeMaySunlightGold = new(255, 215, 64, 255);  // #FFD740 Golden sunlight crest
public static readonly Color BeMayLeatherBrown = new(139, 84, 45, 255);   // #8B542D Leather diary satchel
public static readonly Color BeMayLeatherDark  = new(100, 55, 25, 255);   // #643719 Leather strap

// --- Bóng Xám ---
public static readonly Color BongXamStormBody   = new(120, 115, 145, 255); // #787391 Dusky storm violet
public static readonly Color BongXamStormShadow = new(85, 80, 110, 255);   // #55506E Deep dusk shadow
public static readonly Color BongXamHighlight   = new(155, 150, 180, 255); // #9B96B4 Misty lavender highlight
public static readonly Color BongXamOutline     = new(55, 50, 72, 255);    // #373248 Storm cloud outline
```
*Note*: Keep all existing constants (`BeMayShirtBlue`, `BeMayPantsGrey`, etc.) in place.

### Step 2: Rewrite `DrawBeMay` in `Source/Graphics/VectorRenderer.cs`
- Replace lines 62–109 with the fluffy 5-puff magical cloud architecture:
  - Sinusoidal hover bob + squash & stretch calculation.
  - Subtle elliptical ground shadow (with optional `bool withShadow = true`).
  - Pass 1: Outline contour using expanded circle radius.
  - Pass 2: Pale blue underbelly shadow puffs (`BeMayCloudShadow`).
  - Pass 3: Milk-white pastel body puffs (`BeMayCloudWhite`).
  - Golden sunlight crest swirl on top with dynamic breathing bob.
  - Mini jaunty conical hat perched on the golden crest.
  - Cross-body leather strap and mini diary pouch with golden brass closure.
  - Expressive eyes with multi-highlight sparkles and blinking logic.
  - Cheerful blush cheeks (`CheekBlush`) and adorable smile arc.

### Step 3: Upgrade `DrawButChi` in `Source/Graphics/VectorRenderer.cs`
- Upgrade lines 111–155:
  - 3-facet hexagonal wooden body with facet shading (`ButChiYellow`, ochre, highlight) and vertical wood grain lines.
  - Soft domed pink eraser and silver ferrule with crimp notches and specular highlight.
  - Conical wood collar and graphite tip.
  - Multi-tier glowing cyan tip halo (`ButChiMagicCyan`).
  - Orbiting star dust sparkles (3 procedural particles dynamically orbiting the tip apex).
  - Curious eyes tracking Bé Mây's direction (pupils looking slightly left-downward by default, or tracking `lookDir`).

### Step 4: Implement `DrawBongXam` in `Source/Graphics/VectorRenderer.cs`
- Add:
  ```csharp
  public static void DrawBongXam(Vector2 centerPos, float animTime, float scale = 1.0f)
  ```
  with slow heavy floating hover, 5 storm violet puffs, soulful sad eyes, and subtle falling mist particles.
