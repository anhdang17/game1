using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Audio;
using GameProject.Core.Events;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Spatial;
using GameProject.Graphics;
using GameProject.UI;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Exploration stage for Cố Đô Huế (Imperial Citadel Ngọ Môn & Sông Hương), the Central Realm
/// unlocked after completing Hanoi. Featuring authentic Vietnamese royal architectural motifs,
/// lotus water gardens on Sông Hương, Bé Mây & Bút Chì presence, welcoming dialogue, and seamless
/// return navigation to the World Map.
/// </summary>
public sealed class HueExplorationScene : IScene
{
    public SaveData SaveData { get; }
    public IAudioService AudioService { get; }
    public IInputService InputService { get; }
    public IEventBus EventBus { get; }

    public CharacterKinematics Kinematics { get; }
    public Camera2DTracker CameraTracker { get; }
    public CollisionWorld CollisionWorld { get; }
    public DialogueBox DialogueBox { get; }

    public bool RequestWorldMap { get; set; }

    private float _totalTime;
    private bool _hasWelcomed;

    // Lotus flower positions on Sông Hương
    private readonly List<Vector2> _lotusPositions = new()
    {
        new Vector2(220f, 680f),
        new Vector2(460f, 740f),
        new Vector2(780f, 710f),
        new Vector2(1040f, 760f),
        new Vector2(1380f, 690f)
    };

    public HueExplorationScene(
        SaveData saveData,
        IAudioService? audioService = null,
        IInputService? inputService = null,
        IEventBus? eventBus = null)
    {
        SaveData = saveData ?? SaveData.CreateDefault();
        AudioService = audioService ?? (Raylib.IsWindowReady() ? new RaylibAudioService() : new NullAudioService());
        InputService = inputService ?? new RaylibInputService();
        EventBus = eventBus ?? new EventBus();

        // 1. Collision World & Map Boundary (1600x900)
        CollisionWorld = new CollisionWorld
        {
            MapBounds = new AABB(0, 0, 1600, 900)
        };

        // Static boundary colliders (North citadel moat/wall, South river deep water)
        CollisionWorld.AddObstacle(new AABB(0, 0, 1600, 320));  // Citadel wall line
        CollisionWorld.AddObstacle(new AABB(0, 820, 1600, 900)); // Deep river curb

        // 2. Character Kinematics (Bé Mây spawns in courtyard before Ngọ Môn)
        Kinematics = new CharacterKinematics
        {
            Position = new Vector2(400f, 480f),
            MaxSpeed = 200.0f,
            Friction = 1200.0f
        };

        // 3. Camera Tracker with 0.85f zoom
        CameraTracker = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280, 720),
            MapBounds = CollisionWorld.MapBounds,
            Zoom = 0.85f
        };
        CameraTracker.SnapToTarget(Kinematics.Position);

        // 4. Storybook Dialogue Box
        DialogueBox = new DialogueBox(AudioService);
    }

    public void Enter()
    {
        RequestWorldMap = false;

        // Auto-trigger welcome dialogue once on entering Hue
        if (!_hasWelcomed)
        {
            _hasWelcomed = true;
            DialogueBox.Show(
                "Bút Chì",
                "Oa, Bé Mây nhìn kìa! Chúng ta đã đặt chân đến Cố Đô Huế rồi!\nDòng Sông Hương êm đềm và Ngọ Môn Hoàng thành tráng lệ đang đón chào chúng ta.\nHãy cùng nhau thu thập lại những sắc màu rực rỡ của Miền Trung nhé!",
                "ButChi"
            );
        }
    }

    public void Update(float deltaTime)
    {
        _totalTime += deltaTime;

        // 1. Handle return to World Map hotkeys
        if (Raylib.IsWindowReady())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.M) ||
                Raylib.IsKeyPressed(KeyboardKey.Escape) ||
                Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                RequestWorldMap = true;
                return;
            }

            // Click on Top-Left World Map Button
            Vector2 mouse = Raylib.GetMousePosition();
            Rectangle mapBtn = new(24f, 20f, 180f, 40f);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse, mapBtn))
            {
                RequestWorldMap = true;
                return;
            }
        }

        // 2. Update dialogue box if active
        if (DialogueBox.IsActive)
        {
            DialogueBox.Update(deltaTime, InputService);
            return;
        }

        // 3. Update character movement
        Kinematics.Update(InputService.MovementDirection, CollisionWorld, deltaTime);

        // 4. Update camera
        Vector2 focus = Kinematics.Position + new Vector2(0, -20);
        CameraTracker.Update(focus, deltaTime);
    }

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        // Set up 2D camera viewport
        Camera2D rayCam = new()
        {
            Target = CameraTracker.CurrentPosition,
            Offset = new Vector2(1280f * 0.5f, 720f * 0.5f),
            Rotation = 0f,
            Zoom = CameraTracker.Zoom
        };

        Raylib.BeginMode2D(rayCam);

        // 1. Imperial Dusk Sky
        Raylib.DrawRectangleGradientV(0, 0, 1600, 400, new Color(130, 80, 120, 255), new Color(240, 180, 130, 255));

        // 2. Ngọ Môn Citadel & Imperial Architecture
        DrawNgoMonCitadel();

        // 3. Flagstone Courtyard
        Raylib.DrawRectangle(0, 340, 1600, 280, new Color(175, 165, 150, 255));
        for (int y = 350; y < 620; y += 30)
        {
            Raylib.DrawLineEx(new Vector2(0, y), new Vector2(1600, y), 1f, new Color(140, 130, 115, 120));
        }

        // 4. Sông Hương River (Lower Section)
        DrawPerfumeRiver();

        // 5. Bé Mây and Bút Chì companion
        FacingDirection facing = Kinematics.Velocity.X >= 0 ? FacingDirection.Right : FacingDirection.Left;
        VectorRenderer.DrawBeMay(Kinematics.Position, facing, _totalTime, Kinematics.IsMoving, 1.0f);

        Vector2 butChiPos = Kinematics.Position + new Vector2(30f, -45f + MathF.Sin(_totalTime * 3.5f) * 6f);
        VectorRenderer.DrawButChi(butChiPos, _totalTime, 1.0f);

        Raylib.EndMode2D();

        // Screen-Space UI Overlay
        DrawHUD();

        // Dialogue Box
        if (DialogueBox.IsActive)
        {
            DialogueBox.Draw();
        }
    }

    private void DrawNgoMonCitadel()
    {
        // Brick Citadel Base Rampart
        Rectangle rampart = new(100f, 140f, 1400f, 200f);
        Raylib.DrawRectangleRec(rampart, new Color(155, 65, 50, 255));
        Raylib.DrawRectangleLinesEx(rampart, 2f, new Color(90, 40, 30, 255));

        // Central Archways (Ngọ Môn 3 arched gates)
        float[] gateXs = { 650f, 770f, 890f };
        for (int i = 0; i < gateXs.Length; i++)
        {
            float gx = gateXs[i];
            Rectangle arch = new(gx - 40f, 240f, 80f, 100f);
            Raylib.DrawRectangleRounded(arch, 0.45f, 6, new Color(40, 25, 20, 255));
            Raylib.DrawRectangleRoundedLinesEx(arch, 0.45f, 6, 2f, new Color(212, 175, 55, 200));
        }

        // Lầu Ngũ Phụng (Upper Pavilion)
        Rectangle pavilion = new(480f, 60f, 640f, 90f);
        Raylib.DrawRectangleRec(pavilion, new Color(185, 45, 35, 255));
        Raylib.DrawRectangleLinesEx(pavilion, 2f, new Color(90, 40, 30, 255));

        // Lacquered Red Pillars
        for (float px = 520f; px <= 1080f; px += 70f)
        {
            Raylib.DrawRectangle((int)px, 80, 10, 70, new Color(200, 50, 35, 255));
            Raylib.DrawRectangleLines((int)px, 80, 10, 70, new Color(90, 30, 20, 255));
        }

        // Imperial Curved Terracotta & Golden Glazed Roof
        Vector2 rTop = new(800f, 15f);
        Vector2 rLeft = new(430f, 65f);
        Vector2 rRight = new(1170f, 65f);
        Raylib.DrawTriangle(rTop, rLeft, rRight, new Color(245, 195, 45, 255));
        Raylib.DrawTriangleLines(rTop, rLeft, rRight, new Color(150, 95, 20, 255));

        // Imperial Banner / Royal Inscription
        Rectangle plaque = new(730f, 90f, 140f, 32f);
        Raylib.DrawRectangleRounded(plaque, 0.2f, 4, new Color(35, 30, 25, 240));
        Raylib.DrawRectangleRoundedLinesEx(plaque, 0.2f, 4, 1.5f, new Color(255, 215, 60, 255));
        FontManager.Instance.DrawText("NGỌ MÔN", new Vector2(758f, 98f), 15f, new Color(255, 225, 75, 255));
    }

    private void DrawPerfumeRiver()
    {
        // Flowing River Base (Greenish-Blue Sông Hương)
        Rectangle river = new(0, 620, 1600, 280);
        Raylib.DrawRectangleRec(river, new Color(70, 140, 155, 255));

        // River Bank Stone Pier
        Raylib.DrawRectangle(0, 620, 1600, 12, new Color(135, 125, 110, 255));
        Raylib.DrawLineEx(new Vector2(0, 632), new Vector2(1600, 632), 1.5f, new Color(85, 75, 65, 255));

        // Animated Waves Shimmer
        for (int i = 0; i < 18; i++)
        {
            float wx = (i * 95f + _totalTime * 25f) % 1600f;
            float wy = 650f + (i % 5) * 32f + MathF.Sin(_totalTime * 2f + i) * 4f;
            Raylib.DrawLineEx(new Vector2(wx, wy), new Vector2(wx + 45f, wy), 2f, new Color(145, 210, 225, 180));
        }

        // Floating Lotus Flowers & Leaves
        for (int i = 0; i < _lotusPositions.Count; i++)
        {
            Vector2 lPos = _lotusPositions[i];
            float bob = MathF.Sin(_totalTime * 2.5f + i * 1.2f) * 3f;
            Vector2 center = new(lPos.X, lPos.Y + bob);

            // Lotus Leaf (Flat lily pad)
            Raylib.DrawEllipse((int)center.X, (int)center.Y, 24f, 10f, HanoiPalette.LotusLeafGreen);
            Raylib.DrawEllipseLines((int)center.X, (int)center.Y, 24f, 10f, new Color(40, 95, 45, 255));

            // Lotus Blossom
            VectorRenderer.DrawCartoonCircle(center + new Vector2(0, -6f), 9f, HanoiPalette.LotusPink, HanoiPalette.LotusRose, 1.5f, withShadow: false);
            Raylib.DrawCircleV(center + new Vector2(0, -6f), 4f, HanoiPalette.LotusWhite);
        }
    }

    private void DrawHUD()
    {
        // 1. Back to World Map Button
        Rectangle mapBtn = new(24f, 20f, 180f, 40f);
        VectorRenderer.DrawDropShadow(mapBtn, 0.35f, offsetY: 2f, alpha: 0.25f);
        VectorRenderer.DrawRoundedRect(mapBtn, 0.35f, new Color(252, 248, 238, 245), new Color(145, 115, 80, 255), 1.5f, withShadow: false);
        FontManager.Instance.DrawText("[M] Bản đồ thế giới", new Vector2(mapBtn.X + 16f, mapBtn.Y + 12f), 13f, new Color(60, 45, 30, 255));

        // 2. Realm Title Banner
        string title = "CỐ ĐÔ HUẾ — HOÀNG THÀNH & SÔNG HƯƠNG";
        Vector2 tSz = FontManager.Instance.MeasureText(title, 14f);
        Rectangle banner = new(1280f * 0.5f - tSz.X * 0.5f - 20f, 20f, tSz.X + 40f, 36f);
        VectorRenderer.DrawRoundedRect(banner, 0.35f, new Color(45, 35, 30, 220), new Color(212, 175, 55, 240), 1.5f, withShadow: false);
        FontManager.Instance.DrawText(title, new Vector2(banner.X + 20f, banner.Y + 10f), 14f, new Color(255, 225, 90, 255));
    }

    public void Exit()
    {
    }

    public void Dispose()
    {
    }
}
