using System;
using System.Collections.Generic;
using System.Numerics;
using GameProject.Audio;
using GameProject.Core.Dialogue;
using GameProject.Core.Events;
using GameProject.Core.Hints;
using GameProject.Core.Interactions;
using GameProject.Core.Models;
using GameProject.Core.Movement;
using GameProject.Core.Ports;
using GameProject.Core.Quests;
using GameProject.Core.Spatial;
using GameProject.Core.State;
using GameProject.Graphics;
using GameProject.UI;
using Raylib_cs;

namespace GameProject.Stages;

/// <summary>
/// Exploration stage recreating the Hanoi Old Quarter (Phố Hàng Buồm, Lãn Ông).
/// Integrates character kinematics, collision sliding, 2D camera tracking, dynamic Y-sorting,
/// interaction bubble prompts, market merchants, Quest 1 gathering loop, and post-processing bloom.
/// </summary>
public sealed class HanoiExplorationScene : IScene
{
    // Domain Services & State
    public SaveData SaveData { get; }
    public Inventory Inventory { get; }
    public QuestManager QuestManager { get; }
    public QuestFlags QuestFlags => SaveData.Level1State.QuestFlags;
    public GameStateManager StateManager { get; }
    public IInputService InputService { get; }
    public IAudioService AudioService { get; }
    public IEventBus EventBus { get; }
    public IDialogueService DialogueService { get; }
    public DialogueBox DialogueBox { get; }
    public IHintService HintService { get; }
    public ButChiGuideUI ButChiGuide { get; }

    // Spatial & Kinematics
    public CollisionWorld CollisionWorld { get; }
    public CharacterKinematics Kinematics { get; }
    public Camera2DTracker CameraTracker { get; }
    public InteractionManager InteractionManager { get; }

    // Visual & UI
    public ColorBloomEffect BloomEffect { get; }
    public InventoryBar InventoryBar { get; }
    public QuestHUD QuestHUD { get; }

    // World Entities
    public BaAnNpc BaAn { get; }
    public IReadOnlyList<PhoIngredientMerchant> Merchants { get; }
    public LongBienGateInteractable LongBienGate { get; } = new();

    // Scene & Transition State
    public bool RequestWorldMap { get; set; }
    private PhoAssemblyScene? _phoAssemblyModal;
    private StallPuzzleScene? _stallPuzzleModal;
    private AlbumViewerScene? _albumViewerModal;
    private readonly GrayShadowLetterModal _grayShadowLetterModal = new();

    // Scenery props
    public Vector2 FlowerBicyclePos { get; } = new(180f, 480f);
    public Vector2 VintageCycloPos { get; } = new(920f, 360f);
    public Vector2 TablePos1 { get; } = new(280f, 360f);
    public Vector2 TablePos2 { get; } = new(520f, 350f);

    // Particle system & timing
    private readonly ParticleEmitter _particles = new();
    public ParticleEmitter Particles => _particles;
    private float _puffTimer;
    private float _sparkleTimer;

    // Deterministic environmental layout data
    private static readonly (float X, float Y, float Rx, float Ry, int Shade)[] MossPatches =
    [
        (45f, 111f, 16f, 5f, 0), (95f, 112f, 12f, 4f, 1), (180f, 111f, 22f, 6f, 0),
        (270f, 112f, 14f, 5f, 2), (360f, 111f, 18f, 6f, 1), (490f, 112f, 24f, 7f, 0),
        (620f, 111f, 15f, 5f, 2), (730f, 112f, 20f, 6f, 1), (850f, 111f, 26f, 7f, 0),
        (990f, 112f, 16f, 5f, 2), (1110f, 111f, 22f, 6f, 1), (1240f, 112f, 18f, 5f, 0),
        (70f, 848f, 18f, 6f, 1), (190f, 849f, 24f, 7f, 0), (320f, 848f, 15f, 5f, 2),
        (450f, 849f, 20f, 6f, 1), (580f, 848f, 28f, 7f, 0), (710f, 849f, 16f, 5f, 2),
        (840f, 848f, 22f, 6f, 1), (970f, 849f, 25f, 7f, 0), (1100f, 848f, 17f, 5f, 2),
        (1230f, 849f, 21f, 6f, 1), (1360f, 848f, 26f, 7f, 0), (1490f, 849f, 19f, 6f, 2)
    ];

    private static readonly (float X, float Y, float Rx, float Ry)[] RainPuddles =
    [
        (340f, 640f, 38f, 15f),
        (780f, 560f, 44f, 17f),
        (1120f, 440f, 40f, 16f),
        (1360f, 680f, 35f, 14f)
    ];

    private static readonly (float X, float Y)[] LotusPetals =
    [
        (120f, 530f), (260f, 410f), (410f, 670f), (530f, 510f),
        (690f, 320f), (820f, 690f), (960f, 490f), (1080f, 620f),
        (1210f, 380f), (1330f, 540f), (1450f, 710f), (1530f, 430f)
    ];

    private static readonly (float X, float Y)[] AlmondLeaves =
    [
        (150f, 380f), (290f, 620f), (470f, 360f), (610f, 590f),
        (760f, 410f), (890f, 630f), (1030f, 350f), (1170f, 570f),
        (1280f, 730f), (1410f, 460f), (1510f, 600f)
    ];

    private float _totalTime;
    private InteractionResult? _activeDialogue;
    private IInteractable? _interactingTarget;

    public HanoiExplorationScene(
        SaveData? saveData = null,
        Inventory? inventory = null,
        QuestManager? questManager = null,
        GameStateManager? stateManager = null,
        IInputService? inputService = null,
        IAudioService? audioService = null,
        IEventBus? eventBus = null,
        IDialogueService? dialogueService = null,
        IHintService? hintService = null)
    {
        EventBus = eventBus ?? new EventBus();
        SaveData = saveData ?? SaveData.CreateDefault();
        Inventory = inventory ?? new Inventory(SaveData.Level1State.InventoryItems);

        QuestManager = questManager ?? new QuestManager(EventBus);
        if (questManager == null || QuestManager.GetQuest(HanoiQuests.Quest1Id) == null)
        {
            HanoiQuests.RegisterAll(QuestManager);
        }
        QuestManager.HydrateFromFlags(QuestFlags);

        StateManager = stateManager ?? new GameStateManager(EventBus, GameProject.Core.State.GameState.Exploration);
        InputService = inputService ?? new RaylibInputService();
        AudioService = audioService ?? (Raylib.IsWindowReady() ? new RaylibAudioService() : new NullAudioService());

        DialogueService = dialogueService ?? new DialogueService(EventBus);
        DialogueService.LoadDialogueFile("Data/dialogues_hanoi.json");
        DialogueService.OnActionTriggered += HandleDialogueAction;
        DialogueBox = new DialogueBox(AudioService);
        HintService = hintService ?? new HintManager();
        ButChiGuide = new ButChiGuideUI(HintService, AudioService, InputService);
        EventBus.Subscribe<ItemCollectedEvent>(_ => ButChiGuide.NotifyProgress());

        // 1. Setup Collision World & Map Bounds
        CollisionWorld = new CollisionWorld
        {
            MapBounds = new AABB(0, 0, 1600, 900)
        };

        // 2. Setup Kinematics (smooth cloud glide friction)
        Kinematics = new CharacterKinematics
        {
            Position = new Vector2((float)SaveData.Level1State.PlayerSpawnX, (float)SaveData.Level1State.PlayerSpawnY),
            MaxSpeed = 200.0f,
            Friction = 1200.0f
        };

        // 3. Setup Camera Tracker
        CameraTracker = new Camera2DTracker
        {
            ViewportSize = new Vector2(1280, 720),
            MapBounds = CollisionWorld.MapBounds
        };
        CameraTracker.SnapToTarget(Kinematics.Position);

        // 4. Setup Interactables & Factory
        var (baAn, merchants) = HanoiInteractableFactory.CreateHanoiInteractables();
        BaAn = baAn;
        Merchants = merchants;

        InteractionManager = new InteractionManager();
        InteractionManager.Register(BaAn);
        InteractionManager.Register(LongBienGate);
        for (int i = 0; i < Merchants.Count; i++)
        {
            InteractionManager.Register(Merchants[i]);
        }

        // 5. Register Static Obstacle Colliders
        RegisterObstacles();

        // 6. Presentation Components
        BloomEffect = new ColorBloomEffect();
        InventoryBar = new InventoryBar(EventBus);
        QuestHUD = new QuestHUD(EventBus);

        // Sync items collected to SaveData
        EventBus.Subscribe<ItemCollectedEvent>(ev =>
        {
            if (!SaveData.Level1State.InventoryItems.Contains(ev.ItemId))
            {
                SaveData.Level1State.InventoryItems.Add(ev.ItemId);
            }
        });
    }

    private void RegisterObstacles()
    {
        CollisionWorld.ClearObstacles();

        // Level borders
        CollisionWorld.AddObstacle(new AABB(0, 0, 1600, 110)); // Top building wall line
        CollisionWorld.AddObstacle(new AABB(0, 850, 1600, 900)); // Bottom curb

        // Market Stalls static foot collision boxes
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(BaAn.Position + new Vector2(0, -10), new Vector2(36, 14)));
        for (int i = 0; i < Merchants.Count; i++)
        {
            var m = Merchants[i];
            CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(m.Position + new Vector2(0, -10), new Vector2(36, 14)));
        }

        // Scenery props colliders
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(TablePos1 + new Vector2(0, -8), new Vector2(20, 10)));
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(TablePos2 + new Vector2(0, -8), new Vector2(20, 10)));
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(FlowerBicyclePos + new Vector2(0, -8), new Vector2(24, 10)));
        CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(VintageCycloPos + new Vector2(0, -8), new Vector2(28, 12)));

        // Long Bien Bridge Gate barrier when locked
        if (!QuestFlags.Gate_LongBien_Unlocked)
        {
            CollisionWorld.AddObstacle(AABB.FromCenterAndExtents(new Vector2(1480f, 400f), new Vector2(30f, 300f)));
        }
    }

    public void Enter()
    {
        FontManager.Instance.Initialize();
        BloomEffect.Initialize(1280, 720);

        // Start Hanoi theme if audio ready
        AudioService.PlayMusic(MusicTrack.HanoiStreetsTheme);

        DialogueService.LoadDialogueFile("Data/dialogues_hanoi.json");
        DialogueService.OnActionTriggered -= HandleDialogueAction;
        DialogueService.OnActionTriggered += HandleDialogueAction;
        ButChiGuide.ResetIdleTimer();

        // Sync initial quest flags
        if (QuestFlags.Q3_Completed || QuestFlags.Gate_LongBien_Unlocked)
        {
            BloomEffect.SetColorPercent(100f);
            RegisterObstacles();
        }
        else if (QuestFlags.Q2_Completed)
        {
            BloomEffect.SetColorPercent(33.3f);
        }
        else if (QuestFlags.Q1_Completed)
        {
            BloomEffect.SetColorPercent(0f);
        }
        else
        {
            BloomEffect.SetColorPercent(0f); // Start monochrome
        }
    }

    private void HandleDialogueAction(string action)
    {
        switch (action)
        {
            case "Start_Quest_1":
                if (!QuestFlags.Q1_Started)
                {
                    QuestManager.StartQuest(HanoiQuests.Quest1Id);
                    QuestFlags.Q1_Started = true;
                }
                break;
            case "Complete_Quest_1":
                if (QuestFlags.Q1_Started && !QuestFlags.Q1_Completed)
                {
                    QuestManager.CompleteQuest(HanoiQuests.Quest1Id);
                    QuestFlags.Q1_Completed = true;
                }
                break;
            case "Collect_BanhPho":
                Inventory.AddItem(ItemIds.BanhPho);
                QuestFlags.Q1_BanhPho_Collected = true;
                break;
            case "Collect_ThitBo":
                Inventory.AddItem(ItemIds.ThitBo);
                QuestFlags.Q1_ThitBo_Collected = true;
                break;
            case "Collect_NuocDung":
                Inventory.AddItem(ItemIds.NuocDung);
                QuestFlags.Q1_NuocDung_Collected = true;
                break;
            case "Collect_HanhLa":
                Inventory.AddItem(ItemIds.HanhLa);
                QuestFlags.Q1_HanhLa_Collected = true;
                break;
            case "Collect_ThaoMoc":
                Inventory.AddItem(ItemIds.ThaoMoc);
                QuestFlags.Q1_ThaoMoc_Collected = true;
                break;
            case "Open_World_Map":
                RequestWorldMap = true;
                break;
        }
    }

    public void Update(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        _totalTime += deltaTime;

        // 1. Process Input Service
        InputService.Update(deltaTime);

        // 2. Mini-Game, Album & Cutscene Modal Overlays
        if (StateManager.CurrentState == GameState.Album)
        {
            _albumViewerModal ??= new AlbumViewerScene(SaveData, EventBus, InputService);
            _albumViewerModal.Update(deltaTime);
            if (_albumViewerModal.RequestClose)
            {
                StateManager.PopState();
            }
            return;
        }

        if (InputService.IsAlbumJustPressed && StateManager.CanOpenAlbum)
        {
            _albumViewerModal ??= new AlbumViewerScene(SaveData, EventBus, InputService);
            _albumViewerModal.Enter();
            StateManager.PushState(GameState.Album);
            return;
        }

        if (StateManager.CurrentState == GameState.MiniGamePho && _phoAssemblyModal != null)
        {
            _phoAssemblyModal.Update(deltaTime);
            if (_phoAssemblyModal.RequestExit || _phoAssemblyModal.RequestNextScene)
            {
                if (_phoAssemblyModal.IsSolved)
                {
                    BloomEffect.TriggerWave(BaAn.Position, 33.3f, 2.0f);
                    AudioService.PlaySfx(SfxType.ColorBloomWave);
                }
                StateManager.PopState();
            }
            return;
        }

        if (StateManager.CurrentState == GameState.MiniGameStalls && _stallPuzzleModal != null)
        {
            _stallPuzzleModal.Update(deltaTime);
            if (_stallPuzzleModal.RequestNextScene || _stallPuzzleModal.RequestExit)
            {
                if (_stallPuzzleModal.IsSolved)
                {
                    BloomEffect.TriggerWave(new Vector2(1480f, 480f), 100f, 2.5f);
                    AudioService.PlaySfx(SfxType.ColorBloomWave);
                    AudioService.PlaySfx(SfxType.GateUnlocked);
                    RegisterObstacles();
                }
                StateManager.PopState();
            }
            return;
        }

        if (StateManager.CurrentState == GameState.Cutscene)
        {
            _grayShadowLetterModal.Update(deltaTime,
                isConfirmPressed: InputService.IsConfirmJustPressed,
                isSkipPressed: InputService.IsCancelJustPressed);
            if (_grayShadowLetterModal.RequestClose)
            {
                SaveData.Level1State.QuestFlags.GrayShadow_LetterRead = true;
                EventBus.Publish(new CutsceneEndedEvent("GrayShadow_Letter"));
                EventBus.Publish(new LetterOpenedEvent("Letter_GrayShadow_Hanoi", "Bóng Xám"));
                StateManager.PopState();
            }
            return;
        }

        // Companion Guide Update
        if (InputService.MovementDirection != Vector2.Zero)
        {
            ButChiGuide.ResetIdleTimer();
        }
        string activeQuest = SaveData.Level1State.ActiveQuestId ?? (QuestManager.GetQuestsByState(QuestState.InProgress).FirstOrDefault()?.Id ?? QuestIds.Hanoi01);
        ButChiGuide.Update(deltaTime, activeQuest, SaveData.Level1State);

        // 3. Build Interaction Context
        var context = new InteractionContext(
            PlayerPosition: Kinematics.Position,
            Inventory: Inventory,
            QuestManager: QuestManager,
            QuestFlags: QuestFlags,
            EventBus: EventBus,
            AudioService: AudioService
        );

        // 4. Handle Active Dialogue Dismissal / Progression
        if (DialogueBox.IsActive || _activeDialogue != null)
        {
            DialogueBox.Update(deltaTime, InputService);

            bool inputDismiss = InputService.IsInteractJustPressed || InputService.IsConfirmJustPressed || InputService.IsCancelJustPressed;
            bool choiceWaiting = DialogueBox.CurrentBranches.Count > 0 && !DialogueBox.RequestClose;

            if (DialogueBox.RequestClose || !DialogueBox.IsActive || (inputDismiss && !choiceWaiting))
            {
                AudioService.PlaySfx(SfxType.ButtonClick);
                string speaker = _activeDialogue?.DialogueSpeaker ?? DialogueBox.CurrentSpeaker;
                var currentTarget = InteractionManager.CurrentTarget ?? _interactingTarget;
                var targetToCheck = _interactingTarget ?? currentTarget;
                _activeDialogue = null;
                _interactingTarget = null;
                DialogueBox.Dismiss();

                // Launch Pho Assembly after completing Q1 dialogue with Ba An
                if ((speaker == BaAn.DisplayName || speaker == "Bà An") &&
                    QuestFlags.Q1_Completed && !QuestFlags.Q2_Completed)
                {
                    _phoAssemblyModal ??= new PhoAssemblyScene(SaveData, QuestManager, EventBus, AudioService, InputService);
                    _phoAssemblyModal.Enter();
                    StateManager.PushState(GameState.MiniGamePho);
                }
                else if (targetToCheck is LongBienGateInteractable || currentTarget is LongBienGateInteractable)
                {
                    if (QuestFlags.Q2_Completed && !QuestFlags.Q3_Completed)
                    {
                        _stallPuzzleModal ??= new StallPuzzleScene(SaveData, QuestManager, EventBus, AudioService, InputService);
                        _stallPuzzleModal.Enter();
                        StateManager.PushState(GameState.MiniGameStalls);
                    }
                    else if (QuestFlags.Gate_LongBien_Unlocked)
                    {
                        if (!QuestFlags.GrayShadow_LetterRead)
                        {
                            _grayShadowLetterModal.Show();
                            StateManager.PushState(GameState.Cutscene);
                        }
                        else
                        {
                            RequestWorldMap = true;
                        }
                    }
                }
            }
            _particles.EmitSteam(BaAn.Position + new Vector2(16f, -24f), deltaTime);
            _particles.Update(deltaTime);
            return;
        }

        // 5. Update Movement & Kinematics when not in modal dialogue
        if (InputService.IsDashJustPressed && Kinematics.TryDash(InputService.MovementDirection))
        {
            _particles.EmitCloudPuff(Kinematics.Position, count: 4);
        }

        Kinematics.Update(InputService.MovementDirection, CollisionWorld, deltaTime);

        Vector2 playerPos = Kinematics.Position;
        if (Kinematics.IsDashing)
        {
            _particles.EmitCloudPuff(playerPos, count: 4);
        }
        else if (Kinematics.IsMoving)
        {
            _puffTimer += deltaTime;
            if (_puffTimer >= 0.12f)
            {
                _puffTimer = 0f;
                _particles.EmitCloudPuff(playerPos, count: 4);
            }

            if (Kinematics.CheckFootstepTrigger())
            {
                AudioService.PlaySfx(SfxType.StepStreet, 0.4f);
            }
        }

        // Companion guide sparkles at Bút Chì's tip
        _sparkleTimer += deltaTime;
        if (_sparkleTimer >= 0.35f)
        {
            _sparkleTimer = 0f;
            _particles.EmitSparkles(playerPos + new Vector2(24f, -34f), HanoiPalette.ButChiMagicCyan, count: 2);
        }

        // Broth pot steam at Bà An's stall
        _particles.EmitSteam(BaAn.Position + new Vector2(16f, -24f), deltaTime);

        // Advance particle simulation
        _particles.Update(deltaTime);

        // 6. Update Interaction Manager
        InteractionManager.Update(deltaTime, Kinematics.Position, context);

        if (InputService.IsInteractJustPressed)
        {
            var target = InteractionManager.CurrentTarget;
            _interactingTarget = target;
            var result = InteractionManager.TryInteract(context);
            if (result != null)
            {
                _activeDialogue = result;
                string? graphId = GetDialogueGraphIdForTarget(target);
                if (graphId != null && DialogueService.LoadedGraphs.ContainsKey(graphId))
                {
                    DialogueService.StartDialogue(graphId);
                }
                DialogueBox.Show(result.DialogueSpeaker, result.DialogueText, GetAvatarForSpeaker(result.DialogueSpeaker));

                // Trigger color wave if quest completed
                if (result.QuestCompleted)
                {
                    BloomEffect.TriggerWave(BaAn.Position, 33.3f, 2.0f);
                }
            }
        }

        // 6. Update Camera Tracker
        Vector2 cameraFocus = Kinematics.Position + new Vector2(0, -20);
        CameraTracker.Update(cameraFocus, deltaTime);

        // 7. Update UI HUD
        InventoryBar.Update(deltaTime, Inventory, InputService.PointerPosition, 1280);
        QuestHUD.Update(deltaTime, QuestManager, QuestFlags);

        // 8. Update Bloom Shaders
        BloomEffect.Update(deltaTime);
    }

    private sealed class RenderableEntity : IYSortable
    {
        public string EntityId { get; init; } = "";
        public float FootY { get; init; }
        public int SubLayerPriority { get; init; }
        public Action<float> DrawAction { get; init; } = _ => { };
    }

    public void Draw()
    {
        if (!Raylib.IsWindowReady()) return;

        // Begin full-screen render pass through color restoration bloom shader
        BloomEffect.BeginRender();

        Camera2D rayCamera = new()
        {
            Target = CameraTracker.CurrentPosition,
            Offset = new Vector2(1280f * 0.5f, 720f * 0.5f),
            Rotation = 0f,
            Zoom = CameraTracker.Zoom
        };

        Raylib.BeginMode2D(rayCamera);

        // --- 1. Draw Street Environment & Buildings ---
        DrawStreetEnvironment();

        // --- 2. Dynamic Y-Sorting Entity Queue ---
        var yQueue = new YSortQueue<RenderableEntity>();

        // Player components
        Vector2 playerPos = Kinematics.Position;
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_Shadow",
            FootY = playerPos.Y,
            SubLayerPriority = -1,
            DrawAction = _ => Raylib.DrawEllipse((int)playerPos.X, (int)playerPos.Y, 16f, 8f, Raylib.ColorAlpha(Color.Black, 0.28f))
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_BeMay",
            FootY = playerPos.Y,
            SubLayerPriority = 0,
            DrawAction = t => VectorRenderer.DrawBeMay(playerPos, Kinematics.Facing, t, Kinematics.IsMoving)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Player_Companion_ButChi",
            FootY = playerPos.Y,
            SubLayerPriority = 1,
            DrawAction = t => VectorRenderer.DrawButChi(playerPos + new Vector2(24f, -34f), t)
        });

        // Bà An NPC & Stall
        yQueue.Add(new RenderableEntity
        {
            EntityId = BaAn.Id,
            FootY = BaAn.Position.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawBaAnPhoStall(BaAn.Position, "Quầy Phở", _totalTime)
        });

        // 5 Market Merchants
        for (int i = 0; i < Merchants.Count; i++)
        {
            var m = Merchants[i];
            Color c1 = (i % 2 == 0) ? HanoiPalette.ShutterGreen : HanoiPalette.TerracottaRoof;
            Color c2 = (i % 2 == 0) ? HanoiPalette.WallYellowLight : HanoiPalette.WallYellow;
            yQueue.Add(new RenderableEntity
            {
                EntityId = m.Id,
                FootY = m.Position.Y,
                SubLayerPriority = 0,
                DrawAction = _ =>
                {
                    if (m.Id == "Stall_ThitBo")
                    {
                        VectorRenderer.DrawBacTuBeefStall(m.Position, m.DisplayName, _totalTime);
                    }
                    else if (m.Id == "Stall_HanhLa")
                    {
                        VectorRenderer.DrawCoLanVeggieStall(m.Position, m.DisplayName, _totalTime);
                    }
                    else
                    {
                        VectorRenderer.DrawMarketStall(m.Position, m.DisplayName, c1, c2);
                    }
                }
            });
        }

        // Street props
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Bicycle",
            FootY = FlowerBicyclePos.Y,
            SubLayerPriority = 0,
            DrawAction = t => VectorRenderer.DrawFlowerBicycle(FlowerBicyclePos, t)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Cyclo",
            FootY = VintageCycloPos.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawVintageCyclo(VintageCycloPos)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Table1",
            FootY = TablePos1.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawBluePlasticTableAndStools(TablePos1)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = "Prop_Table2",
            FootY = TablePos2.Y,
            SubLayerPriority = 0,
            DrawAction = _ => VectorRenderer.DrawBluePlasticTableAndStools(TablePos2)
        });
        yQueue.Add(new RenderableEntity
        {
            EntityId = LongBienGate.Id,
            FootY = LongBienGate.Position.Y,
            SubLayerPriority = 0,
            DrawAction = t => DrawLongBienGateEntity(LongBienGate.Position, t)
        });

        // Sort entities deterministically and render
        yQueue.Sort();
        for (int i = 0; i < yQueue.Count; i++)
        {
            yQueue.Entities[i].DrawAction(_totalTime);
        }

        // --- 2.5 World-Space Particle Effects (Steam, Cloud Puffs, Sparkles) ---
        _particles.Draw();

        // --- 3. Interaction Prompt Bubble (Drawn above target in world space) ---
        var target = InteractionManager.CurrentTarget;
        if (target != null && InteractionManager.BubbleAlpha > 0.01f)
        {
            var ctx = new InteractionContext(
                Kinematics.Position,
                Inventory,
                QuestManager,
                QuestFlags,
                EventBus,
                AudioService
            );
            string promptText = target.GetPromptText(ctx);
            string actionText = promptText.Replace("[E] ", "").Trim();
            InteractionBubbleUI.Draw(target.InteractionCenter, target.VisualHeight, actionText, InteractionManager.BubbleAlpha, InteractionManager.BubbleBobTimer);
        }

        Raylib.EndMode2D();

        // End color restoration framebuffer pass and draw to screen
        BloomEffect.EndRenderAndDraw();

        // --- 4. Screen-Space UI Passes ---
        InventoryBar.Draw();
        QuestHUD.Draw(QuestFlags, 1280);
        ButChiGuide.Draw(1280, 720);

        // --- 5. Modal Screen Overlays ---
        if (StateManager.CurrentState == GameState.Album && _albumViewerModal != null)
        {
            _albumViewerModal.Draw();
        }
        else if (StateManager.CurrentState == GameState.MiniGamePho && _phoAssemblyModal != null)
        {
            _phoAssemblyModal.Draw();
        }
        else if (StateManager.CurrentState == GameState.MiniGameStalls && _stallPuzzleModal != null)
        {
            _stallPuzzleModal.Draw();
        }
        else if (StateManager.CurrentState == GameState.Cutscene)
        {
            _grayShadowLetterModal.Draw();
        }
        else if (DialogueBox.IsActive)
        {
            DialogueBox.Draw();
        }
        else if (_activeDialogue != null)
        {
            DrawDialogueModal(_activeDialogue);
        }
    }

    private static string GetAvatarForSpeaker(string speaker)
    {
        if (speaker.Contains("Bà")) return "Avatar_BaAn";
        if (speaker.Contains("Mây")) return "Avatar_May";
        if (speaker.Contains("Bút Chì")) return "Avatar_ButChi";
        if (speaker.Contains("Bác Tư")) return "Avatar_BacTu";
        if (speaker.Contains("Chú Long")) return "Avatar_ChuLong";
        if (speaker.Contains("Cô Lan")) return "Avatar_CoLan";
        if (speaker.Contains("Lãn Ông")) return "Avatar_ThaoMoc";
        return "Avatar_Merchant";
    }

    private string? GetDialogueGraphIdForTarget(IInteractable? target)
    {
        if (target is BaAnNpc)
        {
            if (QuestFlags.Q1_Completed && !QuestFlags.Q2_Completed) return "Dialogue_BaAn_TurnIn";
            if (QuestFlags.Q2_Completed) return "Dialogue_BaAn_PostPho";
            if (QuestFlags.Q1_Started) return "Dialogue_BaAn_InProgress";
            return "Dialogue_BaAn_Intro";
        }
        if (target is PhoIngredientMerchant stall)
        {
            return stall.ItemId switch
            {
                ItemIds.BanhPho => "Dialogue_Stall_BanhPho",
                ItemIds.ThitBo => "Dialogue_Stall_ThitBo",
                ItemIds.NuocDung => "Dialogue_Stall_NuocDung",
                ItemIds.HanhLa => "Dialogue_Stall_HanhLa",
                ItemIds.ThaoMoc => "Dialogue_Stall_ThaoMoc",
                _ => null
            };
        }
        if (target is LongBienGateInteractable)
        {
            return "Dialogue_Gate_LongBien";
        }
        return null;
    }

    private void DrawLongBienGateEntity(Vector2 pos, float animTime)
    {
        if (!Raylib.IsWindowReady()) return;

        if (!QuestFlags.Gate_LongBien_Unlocked)
        {
            // Barricade: Wooden sawhorse barrier
            Rectangle sawhorse = new(pos.X - 30f, pos.Y - 18f, 60f, 22f);
            VectorRenderer.DrawRoundedRect(sawhorse, 0.2f, new Color(175, 120, 70, 255), HanoiPalette.CartoonOutline, 2f, withShadow: false);
            // Caution stripes
            for (int s = -20; s <= 20; s += 14)
            {
                Raylib.DrawLineEx(new Vector2(pos.X + s, pos.Y - 14f), new Vector2(pos.X + s + 6f, pos.Y + 2f), 3f, new Color(255, 220, 60, 255));
            }
            // Vector Padlock icon
            Rectangle lockBody = new(pos.X - 7f, pos.Y - 32f, 14f, 12f);
            VectorRenderer.DrawRoundedRect(lockBody, 0.2f, new Color(255, 215, 64, 255), HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
            Raylib.DrawCircleLines((int)pos.X, (int)pos.Y - 33, 4.5f, HanoiPalette.CartoonOutline);
        }
        else
        {
            // Unlocked: Glowing cyan/gold portal aura with animated expanding ripple rings
            float pulse = 0.5f + 0.5f * MathF.Sin(animTime * 3.5f);
            Raylib.DrawCircleV(pos, 26f + pulse * 6f, Raylib.ColorAlpha(new Color(100, 220, 255, 255), 0.35f));
            Raylib.DrawCircleLines((int)pos.X, (int)pos.Y, 32f + pulse * 8f, new Color(255, 230, 100, 200));

            // Floating lotus sparks
            for (int p = 0; p < 4; p++)
            {
                float a = p * (MathF.PI * 2f / 4f) + animTime * 1.5f;
                Vector2 sp = pos + new Vector2(MathF.Cos(a) * 22f, MathF.Sin(a) * 14f - 10f);
                Raylib.DrawCircleV(sp, 2.5f, new Color(255, 182, 193, 230));
            }

            FontManager.Instance.DrawTextOutline("Cổng Cầu Long Biên", new Vector2(pos.X - 55f, pos.Y - 48f), 12f,
                HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1.5f);
        }
    }

    private void DrawStreetEnvironment()
    {
        if (!Raylib.IsWindowReady()) return;

        // Ground stone cobblestones base
        Raylib.ClearBackground(HanoiPalette.StoneStreet);

        // 1. Organic Cobblestone Ground: Deterministic staggered paver slabs with soft mortar joints (X: 0..1600, Y: 110..850)
        Raylib.DrawRectangle(0, 110, 1600, 740, HanoiPalette.StoneMortar);
        const float slabW = 54f;
        const float slabH = 28f;
        const float mortarGap = 3f;
        int row = 0;
        for (float y = 112f; y < 848f; y += slabH, row++)
        {
            float xOffset = (row % 2 == 1) ? (slabW * 0.5f) : 0f;
            for (float x = -slabW + xOffset; x < 1620f; x += slabW)
            {
                Rectangle slabRec = new(x + mortarGap, y + mortarGap, slabW - mortarGap, slabH - mortarGap);
                int hash = Math.Abs((int)(x * 73 + y * 31)) % 4;
                Color stoneColor = hash switch
                {
                    0 => HanoiPalette.StoneStreet,
                    1 => new Color(174, 165, 153, 255), // Sun-warmed flagstone
                    2 => new Color(152, 143, 131, 255), // Shadowed slate
                    _ => new Color(160, 151, 139, 255)  // Weathered granite
                };
                Raylib.DrawRectangleRounded(slabRec, 0.18f, 2, stoneColor);
            }
        }

        // 2. Moss Patches along colonial wall base (Y: 110) and curb base (Y: 850)
        for (int i = 0; i < MossPatches.Length; i++)
        {
            var p = MossPatches[i];
            Color mossColor = p.Shade switch
            {
                0 => new Color(48, 78, 42, 220),
                1 => new Color(68, 105, 52, 225),
                _ => new Color(90, 126, 68, 200)
            };
            Raylib.DrawEllipse((int)p.X, (int)p.Y, p.Rx, p.Ry, mossColor);
            Raylib.DrawEllipse((int)p.X, (int)(p.Y - 1f), p.Rx * 0.6f, p.Ry * 0.5f, new Color(110, 148, 85, 180));
        }

        // 3. 4 Deterministic Rain Puddles reflecting sky color with gentle ripple arcs
        for (int i = 0; i < RainPuddles.Length; i++)
        {
            var p = RainPuddles[i];
            // Outer wet border
            Raylib.DrawEllipse((int)p.X, (int)p.Y, p.Rx + 3f, p.Ry + 2f, new Color(112, 104, 96, 180));
            // Sky reflection surface
            Raylib.DrawEllipse((int)p.X, (int)p.Y, p.Rx, p.Ry, new Color(145, 188, 218, 200));
            // Glare highlight
            Raylib.DrawEllipse((int)(p.X - p.Rx * 0.2f), (int)(p.Y - p.Ry * 0.2f), p.Rx * 0.5f, p.Ry * 0.4f, new Color(195, 225, 245, 160));

            // Gentle ripple arcs
            float rip1 = ((_totalTime * 0.8f + p.X * 0.01f) % 2.5f) / 2.5f;
            float rip2 = ((_totalTime * 0.8f + p.X * 0.01f + 1.25f) % 2.5f) / 2.5f;
            Raylib.DrawEllipseLines((int)p.X, (int)p.Y, p.Rx * rip1, p.Ry * rip1, Raylib.ColorAlpha(Color.White, 0.35f * (1f - rip1)));
            Raylib.DrawEllipseLines((int)p.X, (int)p.Y, p.Rx * rip2, p.Ry * rip2, Raylib.ColorAlpha(Color.White, 0.35f * (1f - rip2)));
        }

        // 4. Scattered Pink Lotus Petals and Red Almond Leaves
        for (int i = 0; i < LotusPetals.Length; i++)
        {
            var p = LotusPetals[i];
            Raylib.DrawEllipse((int)p.X, (int)p.Y, 5f, 2.8f, HanoiPalette.LotusPink);
            Raylib.DrawCircleV(new Vector2(p.X - 1.5f, p.Y), 1.8f, HanoiPalette.LotusRose);
        }

        for (int i = 0; i < AlmondLeaves.Length; i++)
        {
            var p = AlmondLeaves[i];
            Raylib.DrawEllipse((int)p.X, (int)p.Y, 6.5f, 3.8f, new Color(180, 42, 32, 240));
            Raylib.DrawLineEx(new Vector2(p.X - 4f, p.Y), new Vector2(p.X + 4f, p.Y), 1.2f, new Color(220, 85, 65, 230));
        }

        // 5. Distant Long Biên Bridge Cantilever Steel Trusses with Warm Sunset Backlighting (Right Edge: X 1300..1600)
        // Sunset sky backdrop
        Raylib.DrawRectangleGradientV(1300, 0, 300, 110, new Color(245, 135, 95, 210), new Color(255, 195, 120, 160));
        // Soft glowing setting sun
        Raylib.DrawCircleV(new Vector2(1530f, 48f), 36f, Raylib.ColorAlpha(new Color(255, 215, 130, 255), 0.35f));
        Raylib.DrawCircleV(new Vector2(1530f, 48f), 22f, Raylib.ColorAlpha(new Color(255, 240, 170, 255), 0.55f));

        // Long Bien Bridge Cantilever Truss Spans
        Raylib.DrawLineEx(new Vector2(1300, 110), new Vector2(1600, 110), 4.5f, HanoiPalette.RustedIron);
        // Tower piers
        Raylib.DrawLineEx(new Vector2(1380, 110), new Vector2(1380, 22), 4.5f, HanoiPalette.RustedIron);
        Raylib.DrawLineEx(new Vector2(1520, 110), new Vector2(1520, 22), 4.5f, HanoiPalette.RustedIron);
        // Arched cantilever upper chord
        Vector2[] upperChord1 = [new(1300, 95), new(1340, 60), new(1380, 22), new(1415, 52), new(1450, 75)];
        Vector2[] upperChord2 = [new(1450, 75), new(1485, 52), new(1520, 22), new(1560, 60), new(1600, 95)];
        for (int i = 0; i < upperChord1.Length - 1; i++)
        {
            Raylib.DrawLineEx(upperChord1[i], upperChord1[i + 1], 4f, HanoiPalette.RustedIron);
            Raylib.DrawLineEx(new Vector2(upperChord1[i].X, upperChord1[i].Y - 1f), new Vector2(upperChord1[i + 1].X, upperChord1[i + 1].Y - 1f), 1.5f, new Color(225, 140, 100, 180));
        }
        for (int i = 0; i < upperChord2.Length - 1; i++)
        {
            Raylib.DrawLineEx(upperChord2[i], upperChord2[i + 1], 4f, HanoiPalette.RustedIron);
            Raylib.DrawLineEx(new Vector2(upperChord2[i].X, upperChord2[i].Y - 1f), new Vector2(upperChord2[i + 1].X, upperChord2[i + 1].Y - 1f), 1.5f, new Color(225, 140, 100, 180));
        }
        // Lattice cross bracing (X-truss diagonals)
        (Vector2 A, Vector2 B)[] trussStruts =
        [
            (new(1300, 95), new(1340, 110)), (new(1340, 60), new(1300, 110)),
            (new(1340, 60), new(1380, 110)), (new(1380, 22), new(1340, 110)),
            (new(1380, 22), new(1415, 110)), (new(1415, 52), new(1380, 110)),
            (new(1415, 52), new(1450, 110)), (new(1450, 75), new(1415, 110)),
            (new(1450, 75), new(1485, 110)), (new(1485, 52), new(1450, 110)),
            (new(1485, 52), new(1520, 110)), (new(1520, 22), new(1485, 110)),
            (new(1520, 22), new(1560, 110)), (new(1560, 60), new(1520, 110)),
            (new(1560, 60), new(1600, 110)), (new(1600, 95), new(1560, 110))
        ];
        for (int i = 0; i < trussStruts.Length; i++)
        {
            Raylib.DrawLineEx(trussStruts[i].A, trussStruts[i].B, 2.2f, HanoiPalette.RustedIronPatina);
        }
        // Rivet nodes
        Raylib.DrawCircleV(new Vector2(1380, 22), 4.5f, HanoiPalette.RustedIron);
        Raylib.DrawCircleV(new Vector2(1520, 22), 4.5f, HanoiPalette.RustedIron);
        Raylib.DrawCircleV(new Vector2(1450, 75), 3.5f, HanoiPalette.RustedIron);

        // 6. Upgraded Colonial Facades (Warm yellow walls, decorative cornices) (X: 0..1300)
        Rectangle facade = new(0, 0, 1300, 110);
        Raylib.DrawRectangleRec(facade, HanoiPalette.WallYellow);
        Raylib.DrawRectangle(0, 102, 1300, 8, HanoiPalette.WallYellowShadow);
        Raylib.DrawRectangleLinesEx(facade, 2f, HanoiPalette.CartoonOutline);

        // Terracotta Roof overhang with ridge tiles
        Rectangle roof = new(0, 0, 1300, 36);
        Raylib.DrawRectangleRec(roof, HanoiPalette.TerracottaRoof);
        for (int rx = 12; rx < 1300; rx += 24)
        {
            Raylib.DrawLineEx(new Vector2(rx, 0), new Vector2(rx, 34), 2f, HanoiPalette.TerracottaLight);
        }
        Raylib.DrawLineEx(new Vector2(0, 36), new Vector2(1300, 36), 3.5f, HanoiPalette.TerracottaDark);

        // Decorative molded cornice with dentil blocks
        Raylib.DrawRectangle(0, 36, 1300, 8, HanoiPalette.WallYellowLight);
        for (int dx = 10; dx < 1300; dx += 20)
        {
            Raylib.DrawRectangle(dx, 38, 7, 5, HanoiPalette.WallYellowShadow);
        }
        Raylib.DrawLineEx(new Vector2(0, 44), new Vector2(1300, 44), 2f, HanoiPalette.CartoonOutline);

        // Green louvered shutters with bougainvillea flower boxes (X: 80..1260)
        for (int sx = 80; sx < 1260; sx += 180)
        {
            Rectangle shutter = new(sx, 48, 48, 48);
            VectorRenderer.DrawRoundedRect(shutter, 0.15f, HanoiPalette.ShutterGreen, HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
            // Slat shadows
            Raylib.DrawLineEx(new Vector2(sx + 5, 57), new Vector2(sx + 43, 57), 1.5f, HanoiPalette.ShutterGreenDark);
            Raylib.DrawLineEx(new Vector2(sx + 5, 66), new Vector2(sx + 43, 66), 1.5f, HanoiPalette.ShutterGreenDark);
            Raylib.DrawLineEx(new Vector2(sx + 5, 75), new Vector2(sx + 43, 75), 1.5f, HanoiPalette.ShutterGreenDark);
            Raylib.DrawLineEx(new Vector2(sx + 5, 84), new Vector2(sx + 43, 84), 1.5f, HanoiPalette.ShutterGreenDark);
            // Brass center latch
            Raylib.DrawCircleV(new Vector2(sx + 24, 71), 2f, new Color(220, 180, 60, 255));

            // Bougainvillea flower box at window sill
            Rectangle planter = new(sx - 3, 94, 54, 10);
            VectorRenderer.DrawRoundedRect(planter, 0.2f, HanoiPalette.TerracottaDark, HanoiPalette.CartoonOutline, 1.2f, withShadow: false);
            // Foliage leaves
            Raylib.DrawCircleV(new Vector2(sx + 6, 95), 5f, new Color(42, 95, 55, 255));
            Raylib.DrawCircleV(new Vector2(sx + 20, 94), 6f, new Color(52, 115, 65, 255));
            Raylib.DrawCircleV(new Vector2(sx + 34, 95), 5f, new Color(42, 95, 55, 255));
            Raylib.DrawCircleV(new Vector2(sx + 46, 96), 4.5f, new Color(35, 80, 45, 255));
            // Vibrant bougainvillea flowers
            Raylib.DrawCircleV(new Vector2(sx + 4, 97), 3f, new Color(230, 40, 120, 255));
            Raylib.DrawCircleV(new Vector2(sx + 14, 98), 3.5f, new Color(245, 75, 150, 255));
            Raylib.DrawCircleV(new Vector2(sx + 25, 96), 3.2f, new Color(225, 35, 110, 255));
            Raylib.DrawCircleV(new Vector2(sx + 37, 98), 3.5f, new Color(250, 85, 160, 255));
            Raylib.DrawCircleV(new Vector2(sx + 47, 97), 2.8f, new Color(220, 30, 105, 255));
            // Cascading flower droplets
            Raylib.DrawCircleV(new Vector2(sx + 16, 104), 2.5f, new Color(230, 40, 120, 255));
            Raylib.DrawCircleV(new Vector2(sx + 35, 105), 2.2f, new Color(245, 75, 150, 255));
        }

        // 7. Warm Red Lanterns swaying gently in the breeze with 3-tier radial ambient glow halos (Warm Glow)
        int lanternIndex = 0;
        for (int lx = 170; lx < 1260; lx += 180, lanternIndex++)
        {
            float sway = MathF.Sin(_totalTime * 2.2f + lanternIndex * 0.8f) * 4.0f;
            Raylib.DrawLineEx(new Vector2(lx, 44), new Vector2(lx + sway * 0.4f, 62), 1.2f, HanoiPalette.CartoonOutline);
            Vector2 lCenter = new(lx + sway, 76f);

            // 3-tier radial ambient glow halos
            float pulse = 0.88f + 0.12f * MathF.Sin(_totalTime * 3.2f + lanternIndex);
            Raylib.DrawCircleV(lCenter, 42f * pulse, Raylib.ColorAlpha(new Color(255, 175, 55, 255), 0.12f));
            Raylib.DrawCircleV(lCenter, 26f * pulse, Raylib.ColorAlpha(new Color(255, 195, 75, 255), 0.22f));
            Raylib.DrawCircleV(lCenter, 14f * pulse, Raylib.ColorAlpha(new Color(255, 225, 120, 255), 0.38f));

            // Lantern body
            Raylib.DrawEllipse((int)lCenter.X, (int)lCenter.Y, 9f, 13f, new Color(220, 40, 40, 255));
            Raylib.DrawEllipseLines((int)lCenter.X, (int)lCenter.Y, 5f, 13f, new Color(175, 25, 25, 255));
            Raylib.DrawLineEx(new Vector2(lCenter.X, lCenter.Y - 13f), new Vector2(lCenter.X, lCenter.Y + 13f), 1.2f, new Color(175, 25, 25, 255));
            Raylib.DrawEllipseLines((int)lCenter.X, (int)lCenter.Y, 9f, 13f, HanoiPalette.CartoonOutline);

            // Gold caps and tassel
            Raylib.DrawRectangle((int)(lCenter.X - 4f), (int)(lCenter.Y - 15f), 8, 3, new Color(230, 190, 70, 255));
            Raylib.DrawRectangle((int)(lCenter.X - 4f), (int)(lCenter.Y + 12f), 8, 3, new Color(230, 190, 70, 255));
            Raylib.DrawLineEx(new Vector2(lCenter.X, lCenter.Y + 15f), new Vector2(lCenter.X + sway * 0.3f, lCenter.Y + 25f), 1.5f, new Color(235, 195, 75, 255));
        }

        // 8. Bottom sidewalk curb (Y: 850..900)
        Rectangle bottomCurb = new(0, 850, 1600, 50);
        Raylib.DrawRectangleRec(bottomCurb, HanoiPalette.StoneCurb);
        Raylib.DrawLineEx(new Vector2(0, 850), new Vector2(1600, 850), 3f, HanoiPalette.CartoonOutline);
        for (int kx = 60; kx < 1600; kx += 60)
        {
            Raylib.DrawLineEx(new Vector2(kx, 850), new Vector2(kx, 900), 1.5f, HanoiPalette.StoneMortar);
        }
    }

    private static void DrawDialogueModal(InteractionResult result)
    {
        if (!Raylib.IsWindowReady()) return;
        float boxW = 820f;
        float boxH = 124f;
        float boxX = (1280f - boxW) * 0.5f;
        float boxY = 566f;

        Rectangle dialogRec = new(boxX, boxY, boxW, boxH);
        VectorRenderer.DrawRoundedRect(dialogRec, 0.25f, new Color(254, 252, 246, 250), HanoiPalette.CartoonOutline, 2.0f);

        // Speaker Name Badge
        Vector2 nameSz = FontManager.Instance.MeasureText(result.DialogueSpeaker, 14f);
        Rectangle nameBadge = new(boxX + 20f, boxY - 14f, nameSz.X + 24f, 28f);
        VectorRenderer.DrawRoundedRect(nameBadge, 0.35f, HanoiPalette.WallYellow, HanoiPalette.CartoonOutline, 1.5f, withShadow: false);
        FontManager.Instance.DrawText(result.DialogueSpeaker, new Vector2(nameBadge.X + 12f, nameBadge.Y + 6f), 14f, HanoiPalette.CartoonOutline);

        // Dialogue text
        FontManager.Instance.DrawText(result.DialogueText, new Vector2(boxX + 24f, boxY + 24f), 15f, new Color(35, 30, 28, 255));

        // Advance prompt
        string advanceHint = "[Phím Trắng / Enter] Tiếp tục";
        Vector2 hintSz = FontManager.Instance.MeasureText(advanceHint, 12f);
        FontManager.Instance.DrawText(advanceHint, new Vector2(boxX + boxW - hintSz.X - 20f, boxY + boxH - hintSz.Y - 10f), 12f, new Color(130, 110, 95, 255));
    }

    public void Exit()
    {
        AudioService.StopMusic();
    }

    public void Dispose()
    {
        BloomEffect.Dispose();
        VectorRenderer.UnloadStallTextures();
    }
}
