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

        // 1-3. Full dusk citadel backdrop (sky, Ngọ Môn, Thiên Mụ, courtyard, river, bridge)
        HueCitadelBackdrop.Draw(_totalTime, QuestFlags.Gate_HoiAn_Unlocked || QuestFlags.Hue_Q3_Completed);

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
