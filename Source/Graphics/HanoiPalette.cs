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

    // --- Bé Mây (Revamp) ---
    public static readonly Color BeMayCloudWhite    = new(250, 252, 255, 255); // #FAFCFF Fluffy milk-white cloud
    public static readonly Color BeMayCloudShadow   = new(212, 224, 248, 255); // #D4E0F8 Pastel periwinkle underside shadow
    public static readonly Color BeMayCloudSwirl    = new(195, 210, 240, 255); // #C3D2F0 Subtle cloud body swirl
    public static readonly Color BeMayHatCream      = new(250, 244, 232, 255); // #FAF4E8 Party hat cream/ivory
    public static readonly Color BeMayHatYellow     = new(245, 222, 156, 255); // #F5DE9C Party hat pastel yellow band
    public static readonly Color BeMayHatPink       = new(239, 167, 181, 255); // #EFA7B5 Party hat pastel pink band
    public static readonly Color BeMayGourdBrown     = new(110, 68, 42, 255);   // #6E442A Leather gourd/pouch
    public static readonly Color BeMayGourdHighlight = new(228, 178, 135, 255); // #E4B287 Leather gourd volume highlight
    public static readonly Color BeMaySunlightGold  = new(255, 215, 64, 255);  // #FFD740 Golden sunlight crest
    public static readonly Color BeMayLeatherBrown  = new(139, 84, 45, 255);   // #8B542D Leather diary satchel
    public static readonly Color BeMayLeatherDark   = new(90, 56, 34, 255);    // #5A3822 Leather strap

    // --- Quầy Phở ---
    public static readonly Color PhoAwningGreen     = new(54, 106, 82, 255);   // #366A52 Green canopy stripe
    public static readonly Color PhoAwningCream     = new(245, 238, 217, 255); // #F5EED9 Cream canopy stripe
    public static readonly Color PhoWoodLight       = new(236, 203, 152, 255); // #ECCB98 Honey signboard wood
    public static readonly Color PhoWoodMedium      = new(184, 115, 64, 255);  // #B87340 Countertop & timber
    public static readonly Color PhoWoodDark        = new(110, 62, 30, 255);   // #6E3E1E Shadow wood & grooves
    public static readonly Color PhoBannerCream     = new(248, 243, 226, 255); // #F8F3E2 Linen counter banner
    public static readonly Color PhoLanternGold     = new(255, 202, 40, 255);  // #FFCA28 Glowing golden lantern
    public static readonly Color PhoPotSilver       = new(218, 226, 234, 255); // #DAE2EA Polished stainless pot
    public static readonly Color PhoBowlBlue        = new(43, 102, 184, 255);  // #2B66B8 Bat Trang porcelain blue

    // --- Quầy Thịt Bò ---
    public static readonly Color BeefRed            = new(195, 45, 55, 255);   // #C32D37 Fresh raw beef
    public static readonly Color BeefFatWhite       = new(250, 240, 235, 255); // #FAF0EB Beef fat marbling
    public static readonly Color ButcherBlue        = new(65, 115, 165, 255);  // #4173A5 Bác Tư shirt blue
    public static readonly Color ButcherApronBrown  = new(125, 75, 45, 255);   // #7D4B2D Leather butcher apron
    public static readonly Color ScaleGreen         = new(45, 135, 75, 255);   // #2D874B Nhơn Hòa spring scale green

    // --- Gánh Rau Củ Quả (Cô Lan) ---
    public static readonly Color VeggieWateringCanBlue = new(60, 125, 175, 255); // #3C7DAF Watering can
    public static readonly Color VeggieCarrotOrange   = new(235, 110, 35, 255);  // #EB6E23 Carrot orange
    public static readonly Color VeggieTomatoRed      = new(225, 50, 45, 255);   // #E1322D Tomato red
    public static readonly Color VeggieEggplantPurple = new(110, 50, 120, 255);  // #6E3278 Eggplant purple
    public static readonly Color VeggieCabbageGreen   = new(145, 195, 105, 255); // #91C369 Cabbage light green
    public static readonly Color VeggieBananaYellow   = new(250, 215, 60, 255);  // #FAD73C Banana yellow
    public static readonly Color VeggiePotatoBrown    = new(205, 165, 115, 255); // #CDA573 Potato & ginger
    public static readonly Color VeggieChiliRed       = new(220, 35, 30, 255);   // #DC231E Hanging chili bunch

    // --- Bóng Xám ---
    public static readonly Color BongXamStormBody   = new(120, 115, 145, 255); // #787391 Dusky storm violet
    public static readonly Color BongXamStormShadow = new(85, 80, 110, 255);   // #55506E Deep dusk shadow
    public static readonly Color BongXamHighlight   = new(155, 150, 180, 255); // #9B96B4 Misty lavender highlight
    public static readonly Color BongXamOutline     = new(55, 50, 72, 255);    // #373248 Storm cloud outline

    // --- Cartoon Outline & Shadows ---
    public static readonly Color CartoonOutline     = new(34, 34, 38, 255);    // #222226 Deep outline
    public static readonly Color DropShadow         = new(0, 0, 0, 55);        // 22% alpha shadow

    /// <summary>
    /// Linearly desaturates a vibrant color towards its perceptual luminance gray base using Rec. 709 weights.
    /// colorPercent: 0.0f (pure gray) to 100.0f (full vibrant color).
    /// </summary>
    public static Color ApplyDesaturation(Color c, float colorPercent)
    {
        float t = Math.Clamp(colorPercent / 100.0f, 0.0f, 1.0f);
        if (t >= 0.999f) return c;
        float lum = 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
        byte r = (byte)Math.Clamp(MathF.Round(lum + (c.R - lum) * t), 0, 255);
        byte g = (byte)Math.Clamp(MathF.Round(lum + (c.G - lum) * t), 0, 255);
        byte b = (byte)Math.Clamp(MathF.Round(lum + (c.B - lum) * t), 0, 255);
        return new Color(r, g, b, c.A);
    }
}

