using System;
using Raylib_cs;

namespace GameProject.Graphics;

/// <summary>
/// Cultural color palette capturing the historical and natural beauty of Cố Đô Huế:
/// Imperial citadel architecture (ngói hoàng lưu ly, thanh lưu ly), Perfume River emerald-teal,
/// twilight purple (sắc tím hoàng hôn xứ Huế), Tràng Tiền steel patina, and floating flower lanterns.
/// </summary>
public static class HuePalette
{
    // --- Imperial Citadel (Ngọ Môn / Đại Nội) ---
    public static readonly Color ImperialCrimson       = new(155, 43, 32, 255);   // #9B2B20 Imperial lacquered crimson
    public static readonly Color ImperialCrimsonDark   = new(115, 30, 22, 255);   // Deep shade of crimson
    public static readonly Color ImperialRampartBrick  = new(155, 65, 50, 255);   // Terracotta brick of the fortress
    public static readonly Color ImperialRampartTrim   = new(90, 40, 30, 255);    // Dark granite foundation border
    public static readonly Color RoyalGold             = new(245, 195, 45, 255);  // #F5C32D Ngói Hoàng Lưu Ly (yellow glazed roof)
    public static readonly Color RoyalGoldShadow       = new(190, 140, 25, 255);  // Roof tile depth shadow
    public static readonly Color RoyalGlazedGreen      = new(45, 135, 110, 255);  // Ngói Thanh Lưu Ly (green glazed accents)
    public static readonly Color LacqueredPillarRed    = new(185, 45, 35, 255);   // Lầu Ngũ Phụng pavilion pillars
    public static readonly Color LacqueredPillarDark   = new(120, 30, 25, 255);   // Pillar outline and shadow

    // --- Perfume River (Sông Hương) ---
    public static readonly Color PerfumeRiverTeal      = new(46, 125, 138, 255);  // #2E7D8A Emerald-teal clear flowing water
    public static readonly Color PerfumeRiverDeep      = new(30, 70, 95, 255);    // Evening twilight deep water
    public static readonly Color PerfumeRiverShimmer   = new(145, 210, 225, 180); // Animated wave crest reflections
    public static readonly Color PierStone             = new(135, 125, 110, 255); // Ancient stone embankment and wharf
    public static readonly Color PierStoneDark         = new(85, 75, 65, 255);    // Pier edge shadow

    // --- Twilight Atmosphere (Sắc Tím Xứ Huế) ---
    public static readonly Color TwilightPurple        = new(106, 61, 104, 255);  // #6A3D68 Poetic evening purple haze
    public static readonly Color DuskSkyTop            = new(130, 80, 120, 255);  // Upper gradient of evening sky
    public static readonly Color DuskSkyBottom         = new(240, 180, 130, 255); // Horizon warm sunset orange

    // --- Cầu Tràng Tiền ---
    public static readonly Color TrangTienSteel        = new(196, 203, 212, 255); // #C4CBD4 Gustave Eiffel steel truss
    public static readonly Color TrangTienSteelDark    = new(140, 148, 158, 255); // Steel cross-brace shading
    public static readonly Color TrangTienPierStone    = new(160, 155, 150, 255); // Concrete & stone river pier
    public static readonly Color TrangTienDeck         = new(110, 105, 100, 255); // Pedestrian asphalt deck
    public static readonly Color TrangTienRailing      = new(175, 180, 190, 255); // Steel guard rails

    // --- Chùa Thiên Mụ & Hà Khê Hill ---
    public static readonly Color HaKhePineGreen        = new(55, 85, 50, 255);    // Hà Khê hill ancient pine canopy
    public static readonly Color HaKhePineDark         = new(35, 60, 35, 255);    // Pine trunk and deep shadow
    public static readonly Color ThienMuBrick          = new(180, 60, 45, 255);   // Octagonal pagoda fired brick
    public static readonly Color ThienMuTrim           = new(130, 40, 30, 255);   // Pagoda cornice and eaves shadow
    public static readonly Color SpireGold             = new(245, 205, 60, 255);  // Golden lotus bud pagoda spire

    // --- Thuyền Rồng & Đèn Hoa Đăng ---
    public static readonly Color DragonBoatGold        = new(230, 175, 40, 255);  // Carved golden dragon head
    public static readonly Color DragonBoatRed         = new(195, 40, 35, 255);   // Royal boat canopy & hull trim
    public static readonly Color LanternPaperYellow    = new(255, 200, 80, 240);  // Flower lantern yellow paper
    public static readonly Color LanternPaperPink      = new(244, 143, 177, 240); // Flower lantern pink lotus paper
    public static readonly Color LanternPaperOrange    = new(255, 167, 38, 240);  // Warm orange lantern paper
    public static readonly Color LanternFlame          = new(255, 245, 160, 255); // Glowing candle flame inside lantern

    // --- Flagstone Courtyard & Streets ---
    public static readonly Color FlagstoneFloor        = new(175, 165, 150, 255); // Imperial flagstone pavement
    public static readonly Color FlagstoneLine         = new(140, 130, 115, 120); // Joint lines between flags
}
