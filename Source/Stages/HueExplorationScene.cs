using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Audio;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Graphics;
using GameProject.UI;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Exploration stage for Cố Đô Huế (Chapter 2: Cố Đô Huế), the Central Realm of Vietnam.
/// Recreates the majestic Imperial Citadel (Ngọ Môn / Đại Nội), ancient Chùa Thiên Mụ & Tháp Phước Duyên on Hà Khê Hill,
/// the poetic Perfume River (Sông Hương) with dragon boats and floating flower lanterns, and iconic Cầu Tràng Tiền.
/// Integrates character kinematics, collision sliding, 2D camera tracking, dynamic Y-sorting,
/// interaction bubble prompts, market merchants, authentic Bún Bò Huế & Thả Đèn Hoa Đăng minigame modals,
/// regional color restoration bloom, and transition to Hội An.
/// </summary>
public sealed class HueExplorationScene : IScene
{
    // Domain Services & State
    public SaveData SaveData { get; }
    public Inventory Inventory { get; }
    public QuestManager QuestManager { get; }
    public QuestFlags QuestFlags => SaveData.QuestFlags;
    public IInputService InputService { get; }
    public IAudioService AudioService { get; }
    public IEventBus EventBus { get; }
    public IDialogueService DialogueService { get; }
    public DialogueBox DialogueBox { get; }
    private readonly LevelProgressionService _progression;

    // Spatial & Kinematics
    public CollisionWorld CollisionWorld { get; }
    public CharacterKinematics Kinematics { get; }
    public Camera2DTracker CameraTracker { get; }
    public InteractionManager InteractionManager { get; }

    // Visual, Effects & Particles
    public ColorBloomEffect BloomEffect { get; }
    private readonly ParticleEmitter _particles = new();
    public ParticleEmitter Particles => _particles;

    // Cultural Entities & Landmarks
    public MeBichNpc MeBich { get; }
    public IReadOnlyList<HueIngredientVendor> Vendors { get; }
    public SongHuongBoatmanNpc Boatman { get; }
    public NgoMonGateInteractable NgoMon { get; }
    public ThienMuPagodaInteractable ThienMu { get; }
    public TrangTienBridgeInteractable TrangTien { get; }

    // Modals & Navigation
    public bool RequestWorldMap { get; set; }
    private BunBoHueAssemblyScene? _bunBoAssemblyModal;
    private HoaDangRiverScene? _hoaDangModal;

    public BunBoHueAssemblyScene? BunBoAssemblyModal => _bunBoAssemblyModal;
    public HoaDangRiverScene? HoaDangModal => _hoaDangModal;

    // Timing & State variables
    private float _totalTime;
    private bool _hasWelcomed;
    private float _mistTimer;
    private float _incenseTimer;
    private float _sparkleTimer;
    private bool _bunBoWaveTriggered;
    private bool _hoaDangWaveTriggered;

    // Deterministic Lotus Positions on Sông Hương
    private readonly List<Vector2> _lotusPositions = new()
    {
        new Vector2(160f, 680f),
        new Vector2(280f, 750f),
        new Vector2(440f, 720f),
        new Vector2(760f, 740f),
        new Vector2(920f, 690f),
        new Vector2(1080f, 760f),
        new Vector2(1380f, 710f)
    };

    // Lantern Seed Offsets
    private readonly List<Vector2> _lanternSeeds = new()
    {
        new Vector2(120f, 700f),
        new Vector2(320f, 670f),
        new Vector2(500f, 730f),
        new Vector2(700f, 680f),
        new Vector2(860f, 720f),
        new Vector2(1020f, 670f),
        new Vector2(1180f, 740f),
        new Vector2(1340f, 680f),
        new Vector2(1480f, 720f)
    };

    private sealed class RenderableEntity : IYSortable
    {
        public string EntityId { get; init; } = "";
        public float FootY { get; init; }
        public int SubLayerPriority { get; init; }
        public Action<float> DrawAction { get; init; } = _ => { };
    }

    public HueExplorationScene(
        SaveData? saveData = null,
        IAudioService? audioService = null,
        IInputService? inputService = null,
        IEventBus? eventBus = null,
        Inventory? inventory = null,
        QuestManager? questManager = null,
        IDialogueService? dialogueService = null)
    {
        EventBus = eventBus ?? new EventBus();
        SaveData = saveData ?? SaveData.CreateDefault();
        Inventory = inventory ?? new Inventory(SaveData.Level1State.InventoryItems);

        QuestManager = questManager ?? new QuestManager(EventBus);
        if (QuestManager.GetQuest(HueQuests.Quest1Id) == null)
        {
            HueQuests.RegisterAll(QuestManager);
        }

        InputService = inputService ?? new RaylibInputService();
        AudioService = audioService ?? (Raylib.IsWindowReady() ? new RaylibAudioService() : new NullAudioService());

        DialogueService = dialogueService ?? new DialogueService(EventBus);
        DialogueService.LoadDialogueFile("Data/dialogues_hue.json");
        DialogueService.OnActionTriggered += HandleDialogueAction;
        DialogueBox = new DialogueBox(AudioService);

        _progression = new LevelProgressionService(SaveData, EventBus);

        // 1. Collision World & Map Boundary (1600x900)
        CollisionWorld = new CollisionWorld
        {
            MapBounds = new AABB(0, 0, 1600, 900)
        };

        // 2. Character Kinematics (Bé Mây spawns in flagstone courtyard before Ngọ Môn)
        Kinematics = new CharacterKinematics
        {
            Position = new Vector2(400f, 480f),
            MaxSpeed = 200.0f,
            Friction = 1200.0f
        };

        // 3. Camera Tracker with 0.85f zoom (hard regression invariant)
        CameraTracker = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280, 720),
            MapBounds = CollisionWorld.MapBounds,
            Zoom = 0.85f
        };
        CameraTracker.SnapToTarget(Kinematics.Position);

        // 4. Cultural Entities & Interactables
        var (meBich, vendors, boatman, ngoMon, thienMu, trangTien) = HueInteractableFactory.CreateHueInteractables();
        MeBich = meBich;
        Vendors = vendors;
        Boatman = boatman;
        NgoMon = ngoMon;
        ThienMu = thienMu;
        TrangTien = trangTien;

        InteractionManager = new InteractionManager();
        InteractionManager.Register(MeBich);
        InteractionManager.Register(Boatman);
        InteractionManager.Register(NgoMon);
        InteractionManager.Register(ThienMu);
        InteractionManager.Register(TrangTien);
        for (int i = 0; i < Vendors.Count; i++)
        {
            InteractionManager.Register(Vendors[i]);
        }

        // 5. Register Static Obstacle Colliders
        RegisterObstacles();

        // 6. Color Bloom Effect
        BloomEffect = new ColorBloomEffect();

        // Sync collected items to SaveData
        EventBus.Subscribe<ItemCollectedEvent>(ev =>
        {
            if (!SaveData.Level1State.InventoryItems.Contains(ev.ItemId))
            {
                SaveData.Level1State.InventoryItems.Add(ev.ItemId);
            }
        });
    }

    public void RegisterObstacles()
    {
        CollisionWorld.ClearObstacles();

        // 1. Static Level Boundaries (North Citadel wall line & South deep river curb)
        CollisionWorld.AddObstacle(new AABB(0, 0, 1600, 320));      // Citadel wall line
        CollisionWorld.AddObstacle(new AABB(0, 820, 1600, 900));    // Deep river curb

        // 2. Cầu Tràng Tiền railings (channel Y: 490..570)
        CollisionWorld.AddObstacle(new AABB(1200, 460, 1600, 485));  // North railing
        CollisionWorld.AddObstacle(new AABB(1200, 575, 1600, 600));  // South railing

        // 3. Climax Gate barrier across Cầu Tràng Tiền when locked
        if (!QuestFlags.Gate_HoiAn_Unlocked && !QuestFlags.Hue_Q3_Completed)
        {
            CollisionWorld.AddObstacle(new AABB(1480, 485, 1510, 575));
        }

        // 4. Stalls and NPC tables colliders
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(MeBich.Position + new Vector2(0, -10), new Vector2(36, 14)));
        for (int i = 0; i < Vendors.Count; i++)
        {
            var v = Vendors[i];
            CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(v.Position + new Vector2(0, -10), new Vector2(36, 14)));
        }
    }

    public void Enter()
    {
        RequestWorldMap = false;
        FontManager.Instance.Initialize();
        BloomEffect.Initialize(1280, 720);

        // Sync regional bloom color
        if (QuestFlags.Hue_Q3_Completed || QuestFlags.Gate_HoiAn_Unlocked)
        {
            BloomEffect.SetColorPercent(100f);
        }
        else if (QuestFlags.Hue_Q2_Completed)
        {
            BloomEffect.SetColorPercent(33.3f);
        }
        else
        {
            BloomEffect.SetColorPercent(SaveData.WorldProgression.GetColorRestoration(WorldProgression.RegionHue));
        }

        RegisterObstacles();

        // Play authentic background melody
        AudioService.PlayMusic(MusicTrack.HanoiStreetsTheme);

        DialogueService.LoadDialogueFile("Data/dialogues_hue.json");
        DialogueService.OnActionTriggered -= HandleDialogueAction;
        DialogueService.OnActionTriggered += HandleDialogueAction;

        // Auto-trigger welcome dialogue once on entering Hue
        if (!_hasWelcomed)
        {
            _hasWelcomed = true;
            DialogueBox.Show(
                "Bút Chì",
                "Oa, Bé Mây nhìn kìa! Chúng ta đã đặt chân đến Cố Đô Huế rồi!\nDòng Sông Hương êm đềm và Ngọ Môn Hoàng thành tráng lệ đang đón chào chúng ta.\nHãy cùng nhau thu thập lại những sắc màu rực rỡ của Miền Trung nhé!",
                "Avatar_ButChi_Happy"
            );
        }
    }

    private void HandleDialogueAction(string action)
    {
        switch (action)
        {
            case "Start_Hue_Quest_1":
                if (!QuestFlags.Hue_Q1_Started)
                {
                    QuestManager.StartQuest(HueQuests.Quest1Id);
                    QuestFlags.Hue_Q1_Started = true;
                }
                break;

            case "Start_Hue_Quest_2":
                OpenBunBoAssemblyModal();
                break;

            case "Start_Hue_Quest_3":
                OpenHoaDangRiverModal();
                break;

            case "Collect_BunSoiTo":
                Inventory.AddItem(ItemIds.BunSoiTo);
                QuestFlags.Hue_Q1_BunSoiTo_Collected = true;
                QuestManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.BunSoiTo, 1);
                break;

            case "Collect_BapBo":
                Inventory.AddItem(ItemIds.BapBo);
                QuestFlags.Hue_Q1_BapBo_Collected = true;
                QuestManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.BapBo, 1);
                break;

            case "Collect_ChaCua":
                Inventory.AddItem(ItemIds.ChaCua);
                QuestFlags.Hue_Q1_ChaCua_Collected = true;
                QuestManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.ChaCua, 1);
                break;

            case "Collect_NuocDungHue":
                Inventory.AddItem(ItemIds.NuocDungHue);
                QuestFlags.Hue_Q1_NuocDungHue_Collected = true;
                QuestManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.NuocDungHue, 1);
                break;

            case "Collect_RauSongHue":
                Inventory.AddItem(ItemIds.RauSongHue);
                QuestFlags.Hue_Q1_RauSongHue_Collected = true;
                QuestManager.RecordObjectiveProgress(HueQuests.Quest1Id, ItemIds.RauSongHue, 1);
                break;

            case "Complete_Hue_Climax":
                _progression.CompleteHue();
                RegisterObstacles();
                break;

            case "Open_World_Map":
                RequestWorldMap = true;
                break;
        }
    }

    public void OpenBunBoAssemblyModal()
    {
        _bunBoWaveTriggered = false;
        _bunBoAssemblyModal = new BunBoHueAssemblyScene(SaveData, QuestManager, EventBus, AudioService, InputService);
        _bunBoAssemblyModal.Enter();
    }

    public void OpenHoaDangRiverModal()
    {
        _hoaDangWaveTriggered = false;
        _hoaDangModal = new HoaDangRiverScene(SaveData, QuestManager, EventBus, AudioService, InputService);
        _hoaDangModal.Enter();
    }

    public void Update(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        _totalTime += deltaTime;

        // 1. Return to World Map hotkeys & buttons
        if (Raylib.IsWindowReady())
        {
            if (Raylib.IsKeyPressed(KeyboardKey.M) ||
                Raylib.IsKeyPressed(KeyboardKey.Escape) ||
                Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                RequestWorldMap = true;
                return;
            }

            Vector2 mouse = Raylib.GetMousePosition();
            Rectangle mapBtn = new(24f, 20f, 180f, 40f);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mouse, mapBtn))
            {
                RequestWorldMap = true;
                return;
            }
        }

        // 2. Modals update
        if (_bunBoAssemblyModal != null)
        {
            _bunBoAssemblyModal.Update(deltaTime);
            if (_bunBoAssemblyModal.IsSolved && !QuestFlags.Hue_Q2_Completed)
            {
                QuestFlags.Hue_Q2_Completed = true;
                QuestFlags.Hue_Q2_BunBoCooked = true;
                _progression.OnHueQuest2Completed();
                BloomEffect.TriggerWave(Kinematics.Position, 33.3f);
                _bunBoWaveTriggered = true;
                RegisterObstacles();
            }
            if (_bunBoAssemblyModal.IsSolved && !_bunBoWaveTriggered)
            {
                _bunBoWaveTriggered = true;
                BloomEffect.TriggerWave(Kinematics.Position, 33.3f);
                RegisterObstacles();
            }
            BloomEffect.Update(deltaTime);
            if (_bunBoAssemblyModal.RequestExit || _bunBoAssemblyModal.RequestNextScene)
            {
                _bunBoAssemblyModal = null;
                RegisterObstacles();
            }
            return;
        }

        if (_hoaDangModal != null)
        {
            _hoaDangModal.Update(deltaTime);
            if (_hoaDangModal.IsSolved && !QuestFlags.Hue_Q3_Completed)
            {
                QuestFlags.Hue_Q3_Completed = true;
                QuestFlags.Hue_Q3_PuzzleSolved = true;
                _progression.CompleteHue();
                BloomEffect.TriggerWave(Kinematics.Position, 100f);
                _hoaDangWaveTriggered = true;
                RegisterObstacles();
            }
            if (_hoaDangModal.IsSolved && !_hoaDangWaveTriggered)
            {
                _hoaDangWaveTriggered = true;
                BloomEffect.TriggerWave(Kinematics.Position, 100f);
                RegisterObstacles();
            }
            BloomEffect.Update(deltaTime);
            if (_hoaDangModal.RequestExit || _hoaDangModal.RequestNextScene)
            {
                _hoaDangModal = null;
                RegisterObstacles();
            }
            return;
        }

        // 3. Dialogue box update
        if (DialogueBox.IsActive)
        {
            DialogueBox.Update(deltaTime, InputService);
            return;
        }

        // 4. Dash execution
        if (InputService.IsDashJustPressed && Kinematics.TryDash(InputService.MovementDirection))
        {
            _particles.EmitCloudPuff(Kinematics.Position, count: 4);
        }

        // 5. Character movement & collision
        Kinematics.Update(InputService.MovementDirection, CollisionWorld, deltaTime);
        if (Kinematics.CheckFootstepTrigger())
        {
            AudioService.PlaySfx(SfxType.StepStreet, 0.35f);
        }

        // 6. Camera tracker
        Vector2 focus = Kinematics.Position + new Vector2(0, -20);
        CameraTracker.Update(focus, deltaTime);

        // 7. Ambient particles
        _particles.Update(deltaTime);
        _mistTimer += deltaTime;
        if (_mistTimer >= 0.8f)
        {
            _mistTimer = 0f;
            float rx = 200f + (float)Random.Shared.NextDouble() * 1200f;
            _particles.EmitSteam(new Vector2(rx, 640f), deltaTime);
        }

        _incenseTimer += deltaTime;
        if (_incenseTimer >= 1.2f)
        {
            _incenseTimer = 0f;
            _particles.EmitSteam(new Vector2(330f, 270f), deltaTime);
        }

        _sparkleTimer += deltaTime;
        if (_sparkleTimer >= 0.5f)
        {
            _sparkleTimer = 0f;
            Vector2 butChiPos = Kinematics.Position + new Vector2(28f, -36f);
            _particles.EmitSparkles(butChiPos, new Color(255, 220, 100, 200), 2);
        }

        // 8. Bloom effect update
        BloomEffect.Update(deltaTime);

        // 9. Interaction manager proximity & input
        InteractionContext context = new(
            Kinematics.Position,
            Inventory,
            QuestManager,
            QuestFlags,
            EventBus,
            AudioService
        );
        InteractionManager.Update(deltaTime, Kinematics.Position, context);

        bool interactPressed = InputService.IsInteractJustPressed ||
                               (Raylib.IsWindowReady() && (Raylib.IsKeyPressed(KeyboardKey.E) || Raylib.IsKeyPressed(KeyboardKey.Space)));

        if (interactPressed && InteractionManager.CurrentTarget != null)
        {
            var target = InteractionManager.CurrentTarget;

            // Direct modal launches
            if (target is MeBichNpc && QuestFlags.Hue_Q1_Completed && !QuestFlags.Hue_Q2_Completed)
            {
                OpenBunBoAssemblyModal();
                return;
            }

            if (target is SongHuongBoatmanNpc && QuestFlags.Hue_Q2_Completed && !QuestFlags.Hue_Q3_Completed)
            {
                OpenHoaDangRiverModal();
                return;
            }

            if (target is TrangTienBridgeInteractable && (QuestFlags.Gate_HoiAn_Unlocked || QuestFlags.Hue_Q3_Completed))
            {
                _progression.CompleteHue();
                RequestWorldMap = true;
                return;
            }

            var result = target.Interact(context);
            if (result.Success && !string.IsNullOrEmpty(result.DialogueText))
            {
                DialogueBox.Show(result.DialogueSpeaker, result.DialogueText, "Avatar_May");
            }
        }

        // 10. Spatial exit walking across Cầu Tràng Tiền
        if (Kinematics.Position.X >= 1520f && (QuestFlags.Gate_HoiAn_Unlocked || QuestFlags.Hue_Q3_Completed))
        {
            _progression.CompleteHue();
            RequestWorldMap = true;
        }
    }

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        // Render modal overlay if active
        if (_bunBoAssemblyModal != null)
        {
            _bunBoAssemblyModal.Draw();
            return;
        }

        if (_hoaDangModal != null)
        {
            _hoaDangModal.Draw();
            return;
        }

        // Color restoration bloom post-process pass
        BloomEffect.BeginRender();

        Camera2D rayCam = new()
        {
            Target = CameraTracker.CurrentPosition,
            Offset = new Vector2(1280f * 0.5f, 720f * 0.5f),
            Rotation = 0f,
            Zoom = CameraTracker.Zoom
        };

        Raylib.BeginMode2D(rayCam);

        // 1. Imperial Dusk Sky & Mountains
        DrawSkyAndDistantMountains();

        // 2. Flagstone Courtyard Ground
        DrawFlagstoneCourtyard();

        // 3. Cultural Landmarks (Background components)
        DrawThienMuPagoda();
        DrawNgoMonCitadel();
        DrawPerfumeRiver();
        DrawTrangTienBridge();

        // 4. Dynamic Y-Sorting Entity Queue
        DrawYSortedEntities();

        // 5. Environmental particles
        _particles.Draw();

        Raylib.EndMode2D();

        BloomEffect.EndRenderAndDraw();

        // 6. Screen-Space UI Overlay
        DrawHUD();

        // 7. Interaction Bubble Prompt
        if (InteractionManager.CurrentTarget != null)
        {
            var target = InteractionManager.CurrentTarget;
            string prompt = target.GetPromptText(new InteractionContext(
                Kinematics.Position, Inventory, QuestManager, QuestFlags, EventBus, AudioService
            ));
            InteractionBubbleUI.Draw(
                target.InteractionCenter,
                target.VisualHeight,
                prompt,
                InteractionManager.BubbleAlpha,
                InteractionManager.BubbleBobTimer
            );
        }

        // 8. Dialogue Box
        if (DialogueBox.IsActive)
        {
            DialogueBox.Draw();
        }
    }

    private void DrawSkyAndDistantMountains()
    {
        // Gradient Dusk Sky (Tím Huế dusk sky)
        Raylib.DrawRectangleGradientV(0, 0, 1600, 400, HuePalette.DuskSkyTop, HuePalette.DuskSkyBottom);

        // Distant mountain ridge (Núi Ngự Bình & Dãy Bạch Mã)
        Vector2[] ridge1 = { new(200f, 320f), new(460f, 160f), new(780f, 320f) };
        Raylib.DrawTriangle(ridge1[0], ridge1[1], ridge1[2], new Color(110, 70, 105, 180));

        Vector2[] ridge2 = { new(680f, 320f), new(980f, 180f), new(1350f, 320f) };
        Raylib.DrawTriangle(ridge2[0], ridge2[1], ridge2[2], new Color(95, 60, 90, 160));

        Vector2[] ridge3 = { new(1150f, 320f), new(1420f, 210f), new(1600f, 320f) };
        Raylib.DrawTriangle(ridge3[0], ridge3[1], ridge3[2], new Color(85, 55, 80, 150));
    }

    private void DrawFlagstoneCourtyard()
    {
        // Courtyard flagstone base
        Raylib.DrawRectangle(0, 320, 1600, 305, HuePalette.FlagstoneFloor);

        // Subtle paving lines
        for (int y = 340; y < 620; y += 28)
        {
            Raylib.DrawLineEx(new Vector2(0, y), new Vector2(1600, y), 1f, HuePalette.FlagstoneLine);
        }
        for (int x = 40; x < 1600; x += 90)
        {
            Raylib.DrawLineEx(new Vector2(x, 320), new Vector2(x, 620), 1f, HuePalette.FlagstoneLine);
        }
    }

    private void DrawNgoMonCitadel()
    {
        // 1. Brick Rampart Base
        Rectangle rampart = new(480f, 150f, 680f, 180f);
        Raylib.DrawRectangleRec(rampart, HuePalette.ImperialRampartBrick);
        Raylib.DrawRectangleLinesEx(rampart, 3f, HuePalette.ImperialRampartTrim);

        // Rampart battlements (crenellations) along top
        for (float bx = 485f; bx < 1155f; bx += 32f)
        {
            Raylib.DrawRectangle((int)bx, 142, 18, 10, HuePalette.ImperialRampartBrick);
            Raylib.DrawRectangleLines((int)bx, 142, 18, 10, HuePalette.ImperialRampartTrim);
        }

        // 2. 3 Royal Arched Gates (Cửa Ngọ Môn, Tả Giáp Môn, Hữu Giáp Môn)
        float[] gateXs = { 650f, 820f, 990f };
        for (int i = 0; i < gateXs.Length; i++)
        {
            float gx = gateXs[i];
            Rectangle arch = new(gx - 40f, 240f, 80f, 95f);
            Raylib.DrawRectangleRounded(arch, 0.45f, 6, new Color(40, 25, 20, 255));
            Raylib.DrawRectangleRoundedLinesEx(arch, 0.45f, 6, 2.5f, HuePalette.RoyalGold);
        }

        // 3. Lầu Ngũ Phụng (Upper Timber Pavilion)
        Rectangle pavilion = new(540f, 75f, 560f, 85f);
        Raylib.DrawRectangleRec(pavilion, HuePalette.LacqueredPillarRed);
        Raylib.DrawRectangleLinesEx(pavilion, 2.5f, HuePalette.ImperialCrimsonDark);

        // Lacquered Red Pillars
        for (float px = 560f; px <= 1080f; px += 58f)
        {
            Raylib.DrawRectangle((int)px, 85, 12, 75, HuePalette.LacqueredPillarRed);
            Raylib.DrawRectangleLines((int)px, 85, 12, 75, HuePalette.LacqueredPillarDark);
        }

        // Balcony railing
        Raylib.DrawRectangle(540, 145, 560, 12, HuePalette.LacqueredPillarDark);
        Raylib.DrawLineEx(new Vector2(540, 151), new Vector2(1100, 151), 2f, HuePalette.RoyalGold);

        // 4. Double Curved Imperial Glazed Roof (Ngói Hoàng Lưu Ly & Thanh Lưu Ly)
        Vector2 rTop = new(820f, 18f);
        Vector2 rLeft = new(480f, 78f);
        Vector2 rRight = new(1160f, 78f);
        Raylib.DrawTriangle(rTop, rLeft, rRight, HuePalette.RoyalGold);
        Raylib.DrawTriangleLines(rTop, rLeft, rRight, HuePalette.RoyalGoldShadow);

        // Curved green glazed ridge caps
        Raylib.DrawLineEx(rLeft + new Vector2(-12f, -4f), rLeft + new Vector2(10f, 0f), 3.5f, HuePalette.RoyalGlazedGreen);
        Raylib.DrawLineEx(rRight + new Vector2(12f, -4f), rRight + new Vector2(-10f, 0f), 3.5f, HuePalette.RoyalGlazedGreen);
        Raylib.DrawCircleV(rTop + new Vector2(0, -2f), 5f, HuePalette.RoyalGlazedGreen);

        // 5. Imperial Banner / Inscription Plaque
        Rectangle plaque = new(750f, 92f, 140f, 32f);
        Raylib.DrawRectangleRounded(plaque, 0.25f, 4, new Color(35, 30, 25, 245));
        Raylib.DrawRectangleRoundedLinesEx(plaque, 0.25f, 4, 1.5f, HuePalette.RoyalGold);
        FontManager.Instance.DrawText("NGỌ MÔN", new Vector2(778f, 100f), 15f, new Color(255, 230, 90, 255));
    }

    private void DrawThienMuPagoda()
    {
        // 1. Hà Khê Pine Hill Backdrop
        Vector2 hillP1 = new(40f, 330f);
        Vector2 hillApex = new(220f, 210f);
        Vector2 hillP2 = new(420f, 330f);
        Raylib.DrawTriangle(hillP1, hillApex, hillP2, HuePalette.HaKhePineGreen);
        Raylib.DrawRectangle(40, 280, 380, 50, HuePalette.HaKhePineGreen);

        // Pine trees on hill slope
        float[] pineXs = { 80f, 130f, 310f, 360f };
        for (int i = 0; i < pineXs.Length; i++)
        {
            float px = pineXs[i];
            float py = 250f + (i % 2) * 20f;
            Raylib.DrawRectangle((int)px - 3, (int)py, 6, 25, HuePalette.HaKhePineDark);
            Raylib.DrawTriangle(new Vector2(px - 16f, py + 12f), new Vector2(px, py - 18f), new Vector2(px + 16f, py + 12f), new Color(45, 75, 40, 255));
            Raylib.DrawTriangle(new Vector2(px - 12f, py), new Vector2(px, py - 26f), new Vector2(px + 12f, py), new Color(55, 95, 50, 255));
        }

        // 2. Tháp Phước Duyên (7-tiered octagonal pagoda centered at X = 220f)
        float towerBaseX = 220f;
        float towerBaseY = 300f;

        for (int tier = 0; tier < 7; tier++)
        {
            float tY = towerBaseY - tier * 24f;
            float tW = 76f - tier * 7f;
            float tH = 22f;

            Rectangle tierRec = new(towerBaseX - tW * 0.5f, tY - tH, tW, tH);
            Raylib.DrawRectangleRec(tierRec, HuePalette.ThienMuBrick);
            Raylib.DrawRectangleLinesEx(tierRec, 1.5f, HuePalette.ThienMuTrim);

            // Flared eaves
            float eW = tW + 14f;
            Raylib.DrawLineEx(new Vector2(towerBaseX - eW * 0.5f, tY - tH), new Vector2(towerBaseX + eW * 0.5f, tY - tH), 2.5f, HuePalette.RoyalGold);

            // Arched window niche
            Rectangle winRec = new(towerBaseX - 5f, tY - tH + 6f, 10f, 12f);
            Raylib.DrawRectangleRounded(winRec, 0.45f, 4, new Color(40, 25, 20, 255));
        }

        // Golden Lotus Spire at tower apex
        Vector2 spireTop = new(towerBaseX, towerBaseY - 7 * 24f - 18f);
        Raylib.DrawLineEx(new Vector2(towerBaseX, towerBaseY - 7 * 24f), spireTop, 3f, HuePalette.SpireGold);
        Raylib.DrawCircleV(spireTop, 4.5f, HuePalette.SpireGold);

        // 3. Stele Pavilion (Bia đá rùa đội) beside pagoda
        Rectangle pavilion = new(320f, 275f, 44f, 40f);
        Raylib.DrawRectangleRec(pavilion, new Color(170, 75, 60, 255));
        Raylib.DrawRectangleLinesEx(pavilion, 1.5f, HuePalette.ImperialCrimsonDark);
        // Roof
        Raylib.DrawTriangle(new Vector2(342f, 258f), new Vector2(310f, 275f), new Vector2(374f, 275f), HuePalette.RoyalGold);
        // Stone turtle
        Raylib.DrawEllipse(342, 310, 14f, 7f, new Color(130, 125, 120, 255));

        // 4. Stone Stairway descending to Sông Hương pier
        for (int s = 0; s < 4; s++)
        {
            Raylib.DrawRectangle(190 - s * 4, 320 + s * 8, 60 + s * 8, 6, HuePalette.PierStone);
            Raylib.DrawRectangleLines(190 - s * 4, 320 + s * 8, 60 + s * 8, 6, HuePalette.PierStoneDark);
        }
    }

    private void DrawPerfumeRiver()
    {
        // 1. Water Surface
        Rectangle river = new(0, 620, 1600, 280);
        Raylib.DrawRectangleGradientV((int)river.X, (int)river.Y, (int)river.Width, (int)river.Height,
            HuePalette.PerfumeRiverTeal, HuePalette.PerfumeRiverDeep);

        // 2. River Bank Stone Pier Embankment
        Raylib.DrawRectangle(0, 620, 1600, 12, HuePalette.PierStone);
        Raylib.DrawLineEx(new Vector2(0, 632), new Vector2(1600, 632), 1.5f, HuePalette.PierStoneDark);

        // Mooring stone dock at X: 520..680
        Rectangle boatDock = new(520f, 610f, 160f, 24f);
        Raylib.DrawRectangleRec(boatDock, HuePalette.PierStone);
        Raylib.DrawRectangleLinesEx(boatDock, 1.5f, HuePalette.PierStoneDark);
        for (int i = 0; i < 4; i++)
        {
            Raylib.DrawLineEx(new Vector2(530f + i * 40f, 610f), new Vector2(530f + i * 40f, 634f), 1f, HuePalette.PierStoneDark);
        }

        // 3. Animated Wave Shimmers
        for (int i = 0; i < 20; i++)
        {
            float wx = (i * 85f + _totalTime * 22f) % 1600f;
            float wy = 645f + (i % 6) * 36f + MathF.Sin(_totalTime * 2.2f + i) * 4f;
            Raylib.DrawLineEx(new Vector2(wx, wy), new Vector2(wx + 48f, wy), 2f, HuePalette.PerfumeRiverShimmer);
        }

        // 4. Moored Dragon Boat (Thuyền Rồng Huế) at stone dock
        Vector2 boatPos = new(600f, 642f);
        // Boat Hull
        Rectangle hull = new(boatPos.X - 60f, boatPos.Y, 120f, 24f);
        Raylib.DrawRectangleRounded(hull, 0.4f, 4, new Color(140, 85, 45, 255));
        Raylib.DrawRectangleRoundedLinesEx(hull, 0.4f, 4, 1.5f, new Color(85, 45, 20, 255));

        // Golden Carved Dragon Prow
        Vector2 prow = new(boatPos.X - 65f, boatPos.Y - 6f);
        Raylib.DrawCircleV(prow, 12f, HuePalette.DragonBoatGold);
        Raylib.DrawCircleV(prow + new Vector2(-4f, -4f), 3f, Color.Black);
        Raylib.DrawTriangle(prow, prow + new Vector2(-14f, -8f), prow + new Vector2(-10f, 4f), HuePalette.DragonBoatGold);

        // Dragon Stern Tail
        Vector2 stern = new(boatPos.X + 65f, boatPos.Y - 4f);
        Raylib.DrawCircleV(stern, 9f, HuePalette.DragonBoatGold);
        Raylib.DrawTriangle(stern, stern + new Vector2(12f, -10f), stern + new Vector2(6f, 6f), HuePalette.DragonBoatGold);

        // Royal Canopy
        Rectangle canopy = new(boatPos.X - 35f, boatPos.Y - 18f, 70f, 18f);
        Raylib.DrawRectangleRec(canopy, HuePalette.DragonBoatRed);
        Raylib.DrawRectangleLinesEx(canopy, 1.2f, HuePalette.RoyalGold);
        Raylib.DrawTriangle(new Vector2(boatPos.X, boatPos.Y - 26f), new Vector2(boatPos.X - 40f, boatPos.Y - 18f), new Vector2(boatPos.X + 40f, boatPos.Y - 18f), HuePalette.RoyalGold);

        // 5. Floating Flower Lanterns (Hoa Đăng)
        for (int i = 0; i < _lanternSeeds.Count; i++)
        {
            Vector2 seed = _lanternSeeds[i];
            float driftX = (seed.X + _totalTime * 14f) % 1600f;
            float bob = MathF.Sin(_totalTime * 3f + i * 1.1f) * 3f;
            Vector2 lPos = new(driftX, seed.Y + bob);

            // Warm water reflection glow
            Raylib.DrawCircleV(new Vector2(lPos.X, lPos.Y + 4f), 14f, new Color(255, 210, 100, 60));

            // Lotus Paper Petals
            Color petalCol = (i % 2 == 0) ? HuePalette.LanternPaperPink : HuePalette.LanternPaperYellow;
            Raylib.DrawCircleV(lPos, 7f, petalCol);
            Raylib.DrawTriangle(lPos + new Vector2(-7f, 0), lPos + new Vector2(0, -6f), lPos + new Vector2(7f, 0), petalCol);

            // Flickering Candle Flame
            float flicker = MathF.Sin(_totalTime * 12f + i * 3f) * 1.5f;
            Raylib.DrawCircleV(lPos + new Vector2(0, -4f + flicker), 2.5f, HuePalette.LanternFlame);
        }

        // 6. Floating Lotus Flowers & Leaves
        for (int i = 0; i < _lotusPositions.Count; i++)
        {
            Vector2 lPos = _lotusPositions[i];
            float bob = MathF.Sin(_totalTime * 2.5f + i * 1.2f) * 3f;
            Vector2 center = new(lPos.X, lPos.Y + bob);

            // Lotus Leaf (Lily pad)
            Raylib.DrawEllipse((int)center.X, (int)center.Y, 22f, 9f, HanoiPalette.LotusLeafGreen);
            Raylib.DrawEllipseLines((int)center.X, (int)center.Y, 22f, 9f, new Color(40, 95, 45, 255));

            // Lotus Blossom
            VectorRenderer.DrawCartoonCircle(center + new Vector2(0, -5f), 8f, HuePalette.LanternPaperPink, HanoiPalette.LotusRose, 1.5f, withShadow: false);
            Raylib.DrawCircleV(center + new Vector2(0, -5f), 3.5f, HanoiPalette.LotusWhite);
        }
    }

    private void DrawTrangTienBridge()
    {
        // Cầu Tràng Tiền spans X: 1200..1600, Y: 460..780
        // 1. Concrete & Stone Piers into river
        float[] pierXs = { 1200f, 1330f, 1460f, 1590f };
        for (int i = 0; i < pierXs.Length; i++)
        {
            float px = pierXs[i];
            Rectangle pRec = new(px - 14f, 490f, 28f, 140f);
            Raylib.DrawRectangleRec(pRec, HuePalette.TrangTienPierStone);
            Raylib.DrawRectangleLinesEx(pRec, 1.5f, HuePalette.TrangTienSteelDark);
        }

        // 2. Pedestrian Deck
        Rectangle deck = new(1200f, 490f, 400f, 80f);
        Raylib.DrawRectangleRec(deck, HuePalette.TrangTienDeck);
        Raylib.DrawRectangleLinesEx(deck, 2f, HuePalette.TrangTienSteelDark);

        // Deck central dashed divider line
        for (float dx = 1210f; dx < 1600f; dx += 24f)
        {
            Raylib.DrawLineEx(new Vector2(dx, 530f), new Vector2(dx + 12f, 530f), 2f, new Color(240, 230, 200, 180));
        }

        // North & South Railings
        Raylib.DrawRectangle(1200, 480, 400, 10, HuePalette.TrangTienRailing);
        Raylib.DrawRectangle(1200, 570, 400, 10, HuePalette.TrangTienRailing);

        // 3. 3 Visible Steel Truss Arches (Sáu vài mười hai nhịp)
        for (int span = 0; span < 3; span++)
        {
            float sLeft = 1200f + span * 130f;
            float sRight = sLeft + 130f;
            float sCenter = sLeft + 65f;
            float archApexY = 430f;

            // Parabolic upper steel chord
            Vector2 pLeft = new(sLeft, 485f);
            Vector2 pApex = new(sCenter, archApexY);
            Vector2 pRight = new(sRight, 485f);

            // Draw segmented steel chord
            int segments = 10;
            Vector2 prevPoint = pLeft;
            for (int seg = 1; seg <= segments; seg++)
            {
                float t = seg / (float)segments;
                float x = sLeft + (sRight - sLeft) * t;
                float y = (1 - t) * (1 - t) * pLeft.Y + 2 * (1 - t) * t * pApex.Y + t * t * pRight.Y;
                Vector2 curPoint = new(x, y);

                Raylib.DrawLineEx(prevPoint, curPoint, 3.5f, HuePalette.TrangTienSteel);

                // Vertical suspender ties down to deck
                Raylib.DrawLineEx(curPoint, new Vector2(curPoint.X, 485f), 1.5f, HuePalette.TrangTienSteelDark);

                // X-cross bracing
                if (seg % 2 == 0)
                {
                    Raylib.DrawLineEx(prevPoint, new Vector2(curPoint.X, 485f), 1.0f, HuePalette.TrangTienSteelDark);
                    Raylib.DrawLineEx(new Vector2(prevPoint.X, 485f), curPoint, 1.0f, HuePalette.TrangTienSteelDark);
                }

                prevPoint = curPoint;
            }
        }

        // 4. Vintage Lampposts at Pier Junctions
        for (int i = 0; i < pierXs.Length; i++)
        {
            float px = pierXs[i];
            // Post
            Raylib.DrawLineEx(new Vector2(px, 480f), new Vector2(px, 445f), 2.5f, HuePalette.TrangTienSteelDark);
            // Lamp glass & warm glow
            Raylib.DrawCircleV(new Vector2(px, 442f), 5f, HuePalette.LanternFlame);
            Raylib.DrawCircleV(new Vector2(px, 442f), 16f, new Color(255, 230, 140, 60));
        }

        // 5. Climax Exit Barrier or Passage Aura
        if (!QuestFlags.Gate_HoiAn_Unlocked && !QuestFlags.Hue_Q3_Completed)
        {
            // Ceremonial barrier when locked
            Rectangle barrier = new(1480f, 490f, 20f, 80f);
            Raylib.DrawRectangleRec(barrier, new Color(185, 45, 35, 240));
            Raylib.DrawRectangleLinesEx(barrier, 2f, HuePalette.RoyalGold);
            Raylib.DrawLineEx(new Vector2(1480f, 530f), new Vector2(1500f, 530f), 2f, HuePalette.RoyalGold);
        }
        else
        {
            // Shimmering Golden Gateway Portal when unlocked
            float auraBob = MathF.Sin(_totalTime * 4f) * 4f;
            Rectangle portal = new(1500f + auraBob, 490f, 30f, 80f);
            Raylib.DrawRectangleGradientH((int)portal.X, (int)portal.Y, (int)portal.Width, (int)portal.Height,
                new Color(255, 215, 60, 180), new Color(255, 240, 160, 0));
        }
    }

    private void DrawYSortedEntities()
    {
        var yQueue = new YSortQueue<RenderableEntity>();

        // Player Shadow
        Vector2 playerPos = Kinematics.Position;
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_Shadow",
            FootY = playerPos.Y,
            SubLayerPriority = -1,
            DrawAction = _ => Raylib.DrawEllipse((int)playerPos.X, (int)playerPos.Y, 16f, 8f, Raylib.ColorAlpha(Color.Black, 0.28f))
        });

        // Bé Mây
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_BeMay",
            FootY = playerPos.Y,
            SubLayerPriority = 0,
            DrawAction = t =>
            {
                FacingDirection facing = Kinematics.Velocity.X >= 0 ? FacingDirection.Right : FacingDirection.Left;
                VectorRenderer.DrawBeMay(playerPos, facing, t, Kinematics.IsMoving, 1.0f);
            }
        });

        // Companion Bút Chì
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_Companion_ButChi",
            FootY = playerPos.Y,
            SubLayerPriority = 1,
            DrawAction = t =>
            {
                Vector2 butChiPos = playerPos + new Vector2(30f, -45f + MathF.Sin(t * 3.5f) * 6f);
                VectorRenderer.DrawButChi(butChiPos, t, 1.0f);
            }
        });

        // Mệ Bích NPC & Bún Bò Stall
        yQueue.Add(new RenderableEntity
        {
            EntityId = MeBich.Id,
            FootY = MeBich.Position.Y,
            SubLayerPriority = 0,
            DrawAction = _ =>
            {
                VectorRenderer.DrawMarketStall(MeBich.Position, "Quầy Bún Bò Mệ Bích", HuePalette.ImperialCrimson, HuePalette.RoyalGold, 1.0f, true);
            }
        });

        // 5 Ingredient Vendors
        for (int i = 0; i < Vendors.Count; i++)
        {
            var v = Vendors[i];
            Color c1 = (i % 2 == 0) ? HuePalette.HaKhePineGreen : HuePalette.ImperialCrimson;
            Color c2 = (i % 2 == 0) ? HuePalette.RoyalGold : HuePalette.FlagstoneFloor;
            yQueue.Add(new RenderableEntity
            {
                EntityId = v.Id,
                FootY = v.Position.Y,
                SubLayerPriority = 0,
                DrawAction = _ =>
                {
                    VectorRenderer.DrawMarketStall(v.Position, v.DisplayName, c1, c2, 0.95f, true);
                }
            });
        }

        // Sông Hương Boatman NPC
        yQueue.Add(new RenderableEntity
        {
            EntityId = Boatman.Id,
            FootY = Boatman.Position.Y,
            SubLayerPriority = 0,
            DrawAction = _ =>
            {
                // Boatman figure on dock
                Vector2 bPos = Boatman.Position;
                Raylib.DrawEllipse((int)bPos.X, (int)bPos.Y, 12f, 6f, Raylib.ColorAlpha(Color.Black, 0.3f));
                // Blue áo bà ba shirt & nón lá
                Raylib.DrawRectangle((int)bPos.X - 8, (int)bPos.Y - 30, 16, 24, new Color(45, 95, 140, 255));
                Raylib.DrawTriangle(new Vector2(bPos.X - 16f, bPos.Y - 30f), new Vector2(bPos.X, bPos.Y - 44f), new Vector2(bPos.X + 16f, bPos.Y - 30f), HanoiPalette.NonLaStraw);
                Raylib.DrawTriangleLines(new Vector2(bPos.X - 16f, bPos.Y - 30f), new Vector2(bPos.X, bPos.Y - 44f), new Vector2(bPos.X + 16f, bPos.Y - 30f), HanoiPalette.CartoonOutline);
            }
        });

        yQueue.Sort();
        for (int i = 0; i < yQueue.Count; i++)
        {
            yQueue.Entities[i].DrawAction(_totalTime);
        }
    }

    private void DrawHUD()
    {
        // 1. Back to World Map Button
        Rectangle mapBtn = new(24f, 20f, 180f, 40f);
        VectorRenderer.DrawDropShadow(mapBtn, 0.35f, offsetY: 2f, alpha: 0.25f);
        VectorRenderer.DrawRoundedRect(mapBtn, 0.35f, new Color(252, 248, 238, 245), new Color(145, 115, 80, 255), 1.5f, withShadow: false);
        FontManager.Instance.DrawText("[M] Bản đồ thế giới", new Vector2(mapBtn.X + 16f, mapBtn.Y + 12f), 13f, new Color(60, 45, 30, 255));

        // 2. Realm Title Banner (Top-Center)
        string title = "CỐ ĐÔ HUẾ — HOÀNG THÀNH & SÔNG HƯƠNG";
        Vector2 tSz = FontManager.Instance.MeasureText(title, 14f);
        Rectangle banner = new(1280f * 0.5f - tSz.X * 0.5f - 20f, 20f, tSz.X + 40f, 36f);
        VectorRenderer.DrawRoundedRect(banner, 0.35f, new Color(45, 35, 30, 220), HuePalette.RoyalGold, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(title, new Vector2(banner.X + 20f, banner.Y + 10f), 14f, new Color(255, 225, 90, 255));

        // 3. Hue Quest HUD Card (Top-Left under Map button)
        Rectangle questCard = new(24f, 68f, 290f, 74f);
        VectorRenderer.DrawDropShadow(questCard, 0.25f, offsetY: 2f, alpha: 0.22f);
        VectorRenderer.DrawRoundedRect(questCard, 0.2f, new Color(252, 248, 238, 245), HuePalette.RoyalGold, 1.5f, withShadow: false);

        string qTitle = "NHIỆM VỤ: ";
        string qDesc = "";
        if (!QuestFlags.Hue_Q1_Completed)
        {
            qTitle += "Bún Bò Huế (Q1)";
            qDesc = QuestFlags.AreAllHueQ1IngredientsCollected()
                ? "Mang 5 nguyên liệu về cho Mệ Bích!"
                : $"Thu thập nguyên liệu: ({QuestFlags.GetHueQ1CollectedCount()}/5)";
        }
        else if (!QuestFlags.Hue_Q2_Completed)
        {
            qTitle += "Nấu Bún Bò Huế (Q2)";
            qDesc = "Gặp Mệ Bích để bắt đầu nấu bún bò!";
        }
        else if (!QuestFlags.Hue_Q3_Completed)
        {
            qTitle += "Thả Đèn Sông Hương (Q3)";
            qDesc = "Gặp Bác Lái Đò tại bến sông Hương!";
        }
        else
        {
            qTitle += "Vượt Cầu Tràng Tiền";
            qDesc = "Đi qua Cầu Tràng Tiền để đến Hội An!";
        }

        FontManager.Instance.DrawText(qTitle, new Vector2(questCard.X + 14f, questCard.Y + 12f), 12f, HuePalette.ImperialCrimson);
        FontManager.Instance.DrawText(qDesc, new Vector2(questCard.X + 14f, questCard.Y + 38f), 12f, new Color(40, 35, 30, 255));

        // 4. Hue Inventory Bar (Top-Center under Realm Title)
        float invW = 320f;
        float invH = 50f;
        Rectangle invCard = new((1280f - invW) * 0.5f, 62f, invW, invH);
        VectorRenderer.DrawDropShadow(invCard, 0.25f, offsetY: 2f, alpha: 0.22f);
        VectorRenderer.DrawRoundedRect(invCard, 0.25f, new Color(252, 248, 238, 245), new Color(145, 115, 80, 255), 1.5f, withShadow: false);

        string[] hueItemIds = { ItemIds.BunSoiTo, ItemIds.BapBo, ItemIds.ChaCua, ItemIds.NuocDungHue, ItemIds.RauSongHue };
        string[] hueNames = { "Bún", "Bò", "Chả", "Nước", "Rau" };

        for (int i = 0; i < hueItemIds.Length; i++)
        {
            float slotX = invCard.X + 14f + i * 60f;
            float slotY = invCard.Y + 7f;
            Rectangle slotRec = new(slotX, slotY, 48f, 36f);

            bool collected = Inventory.HasItem(hueItemIds[i]);
            Color slotBg = collected ? new Color(245, 230, 180, 255) : new Color(220, 215, 205, 160);
            Color border = collected ? HuePalette.RoyalGold : new Color(160, 150, 140, 200);

            Raylib.DrawRectangleRounded(slotRec, 0.3f, 4, slotBg);
            Raylib.DrawRectangleRoundedLinesEx(slotRec, 0.3f, 4, 1.2f, border);
            FontManager.Instance.DrawText(hueNames[i], new Vector2(slotX + 12f, slotY + 10f), 11f, collected ? HuePalette.ImperialCrimson : Color.Gray);
        }
    }

    public void Exit()
    {
        AudioService.StopMusic();
        DialogueService.OnActionTriggered -= HandleDialogueAction;
    }

    public void Dispose()
    {
        BloomEffect.Dispose();
        _bunBoAssemblyModal?.Dispose();
        _hoaDangModal?.Dispose();
    }
}
